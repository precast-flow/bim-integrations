using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using BimPrefabExport.Commands;
using BimPrefabExport.Core;
using BimPrefabExport.Services;

namespace BimPrefabExport.UI;

public partial class SharedDrawingsWindow : Window
{
    private readonly ObservableCollection<SharedDrawingRowVm> _rows = new();

    public SharedDrawingsWindow()
    {
        InitializeComponent();
        DrawingsGrid.ItemsSource = _rows;
        Loaded += (_, _) => ReloadFromDocument();
    }

    public void ReloadFromDocument()
    {
        var doc = AcadApp.DocumentManager.MdiActiveDocument;
        if (doc is null)
        {
            SetStatus("Aktif çizim yok.");
            return;
        }

        DrawingInitService.EnsureInit(doc);
        SharedDrawingsDocument model;
        using (var docLock = doc.LockDocument())
        using (var tr = doc.Database.TransactionManager.StartTransaction())
        {
            model = SharedDrawingsRegistry.Load(tr, doc.Database);
            tr.Commit();
        }

        _rows.Clear();
        foreach (var e in model.Drawings)
            _rows.Add(new SharedDrawingRowVm(e));

        SetStatus($"{_rows.Count} çizim kaydı.");
    }

    private void SetStatus(string msg) => StatusLine.Text = msg;

    private void FlushToDatabase()
    {
        var doc = AcadApp.DocumentManager.MdiActiveDocument;
        if (doc is null)
            throw new InvalidOperationException("Aktif çizim yok.");

        DrawingInitService.EnsureInit(doc);
        var utc = DateTime.UtcNow;
        foreach (var r in _rows)
        {
            r.Model.CreatedUtc ??= utc;
            r.Model.ModifiedUtc = utc;
        }

        var model = new SharedDrawingsDocument
        {
            Drawings = _rows.Select(r => r.Model).ToList(),
        };

        using var docLock = doc.LockDocument();
        using (var tr = doc.Database.TransactionManager.StartTransaction())
        {
            SharedDrawingsRegistry.Save(tr, doc.Database, model);
            tr.Commit();
        }
    }

    private void OnAddDrawing(object sender, RoutedEventArgs e)
    {
        var utc = DateTime.UtcNow;
        _rows.Add(new SharedDrawingRowVm(new SharedDrawingEntry
        {
            DrawingId = Guid.NewGuid(),
            DisplayName = "Yeni çizim",
            Category = "GENEL",
            PlotPaperSize = PlotPaperSizes.Default,
            CreatedUtc = utc,
            ModifiedUtc = utc,
        }));
        SetStatus("Yeni satır eklendi; Kaydet ile çizime yazın.");
    }

    private void OnRemoveDrawing(object sender, RoutedEventArgs e)
    {
        if (DrawingsGrid.SelectedItem is not SharedDrawingRowVm row)
        {
            System.Windows.MessageBox.Show("Silmek için listede bir satır seçin.", Title, MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        _rows.Remove(row);
        SetStatus("Satır kaldırıldı; Kaydet ile çizime yansıtın.");
    }

    private void OnPickFence(object sender, RoutedEventArgs e)
    {
        if (DrawingsGrid.SelectedItem is not SharedDrawingRowVm row)
        {
            System.Windows.MessageBox.Show("Önce bir çizim satırı seçin.", Title, MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var doc = AcadApp.DocumentManager.MdiActiveDocument;
        if (doc is null)
            return;

        try
        {
            FlushToDatabase();
        }
        catch (System.Exception ex)
        {
            System.Windows.MessageBox.Show("Kaydedilemedi: " + ex.Message, Title, MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        PrefabUiSession.SharedDrawingFencePickTargetId = row.Model.DrawingId;
        doc.SendStringToExecute("BIM_PREFAB_SHARED_DRAWING_RECT ", true, false, false);
        SetStatus("AutoCAD komut satırında kapalı polyline seçin.");
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        var doc = AcadApp.DocumentManager.MdiActiveDocument;
        if (doc is null)
            return;

        try
        {
            foreach (var r in _rows)
                r.Model.PlotPaperSize = PlotPaperSizes.NormalizeOrDefault(r.Model.PlotPaperSize);

            FlushToDatabase();
            foreach (var r in _rows)
            {
                r.NotifyFenceChanged();
                r.NotifyDatesChanged();
            }

            SetStatus("Kaydedildi.");
        }
        catch (System.Exception ex)
        {
            System.Windows.MessageBox.Show("Kayıt hatası: " + ex.Message, Title, MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void OnExportPdfFolder(object sender, RoutedEventArgs e)
    {
        var doc = AcadApp.DocumentManager.MdiActiveDocument;
        if (doc is null)
            return;

        try
        {
            FlushToDatabase();
        }
        catch (System.Exception ex)
        {
            System.Windows.MessageBox.Show("Önce kayıt gerekli: " + ex.Message, Title, MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        PrefabExportInteraction.RunSharedDrawingsPdfBulk(doc, this);
        ReloadFromDocument();
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}

public sealed class SharedDrawingRowVm : INotifyPropertyChanged
{
    public SharedDrawingRowVm(SharedDrawingEntry model) => Model = model;

    public SharedDrawingEntry Model { get; }

    public string DisplayName
    {
        get => Model.DisplayName;
        set
        {
            if (Model.DisplayName == value)
                return;
            Model.DisplayName = value;
            OnPropertyChanged();
        }
    }

    public string Category
    {
        get => Model.Category;
        set
        {
            if (Model.Category == value)
                return;
            Model.Category = value;
            OnPropertyChanged();
        }
    }

    public string PlotPaperSize
    {
        get => Model.PlotPaperSize;
        set
        {
            if (Model.PlotPaperSize == value)
                return;
            Model.PlotPaperSize = value;
            OnPropertyChanged();
        }
    }

    public bool PlotLandscape
    {
        get => Model.PlotLandscape;
        set
        {
            if (Model.PlotLandscape == value)
                return;
            Model.PlotLandscape = value;
            OnPropertyChanged();
        }
    }

    public string FenceStatus =>
        Model.LinkFences.Any(f => f.IsValid()) ? "Var" : "—";

    public string CreatedText => FormatUtc(Model.CreatedUtc);

    public string ModifiedText => FormatUtc(Model.ModifiedUtc);

    public event PropertyChangedEventHandler? PropertyChanged;

    public void NotifyFenceChanged() => OnPropertyChanged(nameof(FenceStatus));

    public void NotifyDatesChanged()
    {
        OnPropertyChanged(nameof(CreatedText));
        OnPropertyChanged(nameof(ModifiedText));
    }

    private static string FormatUtc(DateTime? utc) =>
        utc is null
            ? "—"
            : utc.Value.ToLocalTime().ToString("dd.MM.yyyy HH:mm", CultureInfo.CurrentCulture);

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
