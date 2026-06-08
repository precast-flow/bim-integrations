using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Windows;
using Autodesk.AutoCAD.Geometry;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using BimPrefabExport.Commands;
using BimPrefabExport.Core;
using BimPrefabExport.Schema;
using BimPrefabExport.Services;

namespace BimPrefabExport.UI;

public partial class BimPrefabPaletteWindow : Window
{
    private readonly ObservableCollection<ProductPaletteRow> _productItems = new();
    private readonly ObservableCollection<MaterialLine> _materials = new();
    private readonly ObservableCollection<RebarLine> _rebars = new();
    private readonly ObservableCollection<AttributeValueRow> _paletteDimRows = new();
    private bool _loading;
    private bool _suppressSelectAll;
    private bool _paletteTypologyLoading;
    private bool _paletteCategoryLoading;
    private bool _pdfDrawingUiSync;
    private System.Windows.Controls.Button? _uploadPdfButton;

    public BimPrefabPaletteWindow()
    {
        InitializeComponent();
        ProductGrid.ItemsSource = _productItems;
        MaterialsGrid.ItemsSource = _materials;
        RebarsGrid.ItemsSource = _rebars;
        PaperSizeCombo.ItemsSource = PlotPaperSizes.PaletteOrder;
        PaperSizeCombo.SelectedItem = PlotPaperSizes.Default;
        _ = AttributeCatalogService.Default;
        TypologyAttributeGridSetup.ConfigureValueColumns(PaletteDimGrid);
        PaletteDimGrid.ItemsSource = _paletteDimRows;
        PopulatePaletteCategoryCombo();
        RebarsGrid.CellEditEnding += OnRebarsGridCellEditEnding;
        BuildMainToolbar();
        BuildProductActionBar();

        Loaded += (_, _) =>
        {
            var doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc is not null)
                DrawingInitService.EnsureInit(doc);
        };
    }

    private static System.Windows.Controls.Button CreateToolbarButton(string text, ImageSource? icon, RoutedEventHandler click)
    {
        var b = new System.Windows.Controls.Button
        {
            MinWidth = 92,
            Margin = new Thickness(0, 0, 8, 8),
            Padding = new Thickness(10, 6, 10, 6),
            Background = System.Windows.Media.Brushes.White,
            BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(190, 200, 210)),
            Cursor = System.Windows.Input.Cursors.Hand,
        };
        var sp = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
        if (icon is not null)
        {
            sp.Children.Add(new System.Windows.Controls.Image
            {
                Source = icon,
                Width = 16,
                Height = 16,
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center,
            });
        }

        sp.Children.Add(new TextBlock
        {
            Text = text,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 12.5,
        });
        b.Content = sp;
        b.Click += click;
        return b;
    }

    private void BuildMainToolbar()
    {
        ToolbarPanel.Children.Add(CreateToolbarButton("Yeni ürün", PaletteWpfIcon.FromBitmap(PaletteIcons.Add), OnNewProduct));
        ToolbarPanel.Children.Add(CreateToolbarButton("Ürünü sil", PaletteWpfIcon.FromBitmap(PaletteIcons.Delete), OnDeleteProduct));
        ToolbarPanel.Children.Add(CreateToolbarButton("Çerçeveyi sil", PaletteWpfIcon.FromBitmap(PaletteIcons.Delete),
            OnClearProductLinks));
        ToolbarPanel.Children.Add(CreateToolbarButton("Paket (CSV+PDF)…", PaletteWpfIcon.FromBitmap(PaletteIcons.PdfBulk),
            OnExportBundle));
    }

    private void BuildProductActionBar()
    {
        ProductActionPanel.Children.Add(CreateToolbarButton("Kaydet", PaletteWpfIcon.FromBitmap(PaletteIcons.Save), OnSaveProduct));
        ProductActionPanel.Children.Add(CreateToolbarButton("Çizim sınırı seç", PaletteWpfIcon.FromBitmap(PaletteIcons.Polyline),
            OnRectPoly));
        _uploadPdfButton = CreateToolbarButton("PDF yükle…", PaletteWpfIcon.FromBitmap(PaletteIcons.Pdf), OnUploadUserPdf);
        ProductActionPanel.Children.Add(_uploadPdfButton);
        ProductActionPanel.Children.Add(CreateToolbarButton("Ürünü göster", PaletteWpfIcon.FromBitmap(PaletteIcons.Zoom), OnShowProduct));
    }

    private static string FormatProductNamesBulletSection(IReadOnlyList<string> names, int maxLines = 28)
    {
        var distinct = names
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        if (distinct.Count == 0)
            return "• (isim yok)";
        var sb = new StringBuilder();
        for (var i = 0; i < Math.Min(distinct.Count, maxLines); i++)
            sb.Append("• ").AppendLine(distinct[i]);
        if (distinct.Count > maxLines)
            sb.Append("• … ve ").Append(distinct.Count - maxLines).AppendLine(" ürün daha");
        return sb.ToString().TrimEnd();
    }

    private IEnumerable<string> EnumerateDisplayNamesForProductIds(IReadOnlyList<Guid> ids)
    {
        foreach (var id in ids)
        {
            var row = _productItems.FirstOrDefault(r => r.Record.ProductId == id);
            if (row is null)
            {
                yield return $"Ürün {id:D}";
                continue;
            }

            var n = row.Record.DisplayName?.Trim();
            if (string.IsNullOrWhiteSpace(n))
                n = row.Record.Code?.Trim();
            if (string.IsNullOrWhiteSpace(n))
                n = $"Ürün {id:D}";
            yield return n;
        }
    }

    private void UpdateProductActionButtonsEnabled()
    {
        var one = ProductGrid.SelectedItems.Count == 1;
        var hasPdfDrawing = false;
        if (one && ProductGrid.SelectedItem is ProductPaletteRow prSel)
        {
            ProductPdfDrawingSync.NormalizeProductRecord(prSel.Record);
            hasPdfDrawing = prSel.Record.PdfDrawings.Count > 0;
        }
        foreach (var child in ProductActionPanel.Children)
        {
            if (child is not System.Windows.Controls.Button b)
                continue;
            if (_uploadPdfButton is not null && ReferenceEquals(b, _uploadPdfButton))
                b.IsEnabled = hasPdfDrawing;
            else
                b.IsEnabled = one;
        }

        if (RemovePdfDrawingButton is not null)
            RemovePdfDrawingButton.IsEnabled = one && PdfDrawingsList.SelectedItem is ListBoxItem;
    }

    private static string FormatPdfDrawingListLabel(ProductPdfDrawing d)
    {
        var last = d.LastPdfExportUtc.HasValue
            ? d.LastPdfExportUtc.Value.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)
            : "—";
        var up = string.IsNullOrWhiteSpace(d.UploadedPdfRelativePath) ? "" : "  ·  Harici PDF";
        var rev = d.PdfRevision > 0 ? $"  ·  PDF rev {d.PdfRevision}" : "";
        return $"{d.PdfTitle}  ·  {PlotPaperSizes.NormalizeOrDefault(d.PlotPaperSize)}{rev}  ·  Son PDF: {last}{up}";
    }

    private void RebuildPdfDrawingsList(ProductPaletteRow pi)
    {
        _pdfDrawingUiSync = true;
        try
        {
            PdfDrawingsList.Items.Clear();
            ProductPdfDrawingSync.NormalizeProductRecord(pi.Record);
            foreach (var d in pi.Record.PdfDrawings)
            {
                PdfDrawingsList.Items.Add(new ListBoxItem
                {
                    Tag = d.FenceId,
                    Content = FormatPdfDrawingListLabel(d),
                });
            }

            if (PdfDrawingsList.Items.Count > 0)
                PdfDrawingsList.SelectedIndex = 0;
        }
        finally
        {
            _pdfDrawingUiSync = false;
        }

        LoadPdfDrawingEditorFromSelection(pi);
    }

    private ProductPdfDrawing? GetSelectedPdfDrawing(ProductPaletteRow pi)
    {
        ProductPdfDrawingSync.NormalizeProductRecord(pi.Record);
        if (PdfDrawingsList.SelectedItem is ListBoxItem lbi && lbi.Tag is Guid g)
            return ProductPdfDrawingSync.FindDrawing(pi.Record, g);
        return pi.Record.PdfDrawings.FirstOrDefault();
    }

    private void LoadPdfDrawingEditorFromSelection(ProductPaletteRow pi)
    {
        if (PdfDrawingTitleBox is null || PdfDrawingExportInfoText is null || PdfDrawingRevisionBox is null)
            return;
        if (PdfDrawingsList.SelectedItem is not ListBoxItem lbi || lbi.Tag is not Guid fid)
        {
            PdfDrawingTitleBox.Text = "";
            PdfDrawingRevisionBox.Text = "";
            PdfDrawingExportInfoText.Text = "";
            return;
        }

        var spec = ProductPdfDrawingSync.FindDrawing(pi.Record, fid);
        if (spec is null)
        {
            PdfDrawingTitleBox.Text = "";
            PdfDrawingRevisionBox.Text = "";
            PdfDrawingExportInfoText.Text = "";
            return;
        }

        _pdfDrawingUiSync = true;
        try
        {
            PdfDrawingTitleBox.Text = spec.PdfTitle;
            PdfDrawingRevisionBox.Text = spec.PdfRevision.ToString(CultureInfo.InvariantCulture);
            var paper = PlotPaperSizes.NormalizeOrDefault(spec.PlotPaperSize);
            PaperSizeCombo.SelectedItem = PlotPaperSizes.PaletteOrder.Contains(paper) ? paper : PlotPaperSizes.Default;
            if (PlotOrientationPortraitRadio is not null && PlotOrientationLandscapeRadio is not null)
            {
                PlotOrientationPortraitRadio.IsChecked = !spec.PlotLandscape;
                PlotOrientationLandscapeRadio.IsChecked = spec.PlotLandscape;
            }

            var lastLine = spec.LastPdfExportUtc.HasValue
                ? "Son dışa aktarma (yerel): " +
                  spec.LastPdfExportUtc.Value.ToLocalTime().ToString("F", CultureInfo.CurrentCulture)
                : "Son dışa aktarma: henüz yok.";
            var fn = ProductPdfExportService.BuildProductPdfFileName(pi.Record, spec);
            PdfDrawingExportInfoText.Text = lastLine + "\nDosya adı: " + fn;
        }
        finally
        {
            _pdfDrawingUiSync = false;
        }
    }

    private void ApplyPdfDrawingUiToSelectedRecord(ProductPaletteRow pi)
    {
        if (PdfDrawingsList.SelectedItem is not ListBoxItem lbi || lbi.Tag is not Guid fid)
            return;
        var spec = ProductPdfDrawingSync.FindDrawing(pi.Record, fid);
        if (spec is null)
            return;
        spec.PdfTitle = string.IsNullOrWhiteSpace(PdfDrawingTitleBox.Text) ? "cizim" : PdfDrawingTitleBox.Text.Trim();
        var revText = PdfDrawingRevisionBox.Text?.Trim() ?? "0";
        if (!int.TryParse(revText, NumberStyles.Integer, CultureInfo.CurrentCulture, out var pdfRev)
            && !int.TryParse(revText, NumberStyles.Integer, CultureInfo.InvariantCulture, out pdfRev))
            pdfRev = 0;
        if (pdfRev < 0)
            pdfRev = 0;
        spec.PdfRevision = pdfRev;
        if (PaperSizeCombo.SelectedItem is string pap)
            spec.PlotPaperSize = PlotPaperSizes.NormalizeOrDefault(pap);
        if (PlotOrientationLandscapeRadio is not null)
            spec.PlotLandscape = PlotOrientationLandscapeRadio.IsChecked == true;
        lbi.Content = FormatPdfDrawingListLabel(spec);
        ProductPdfDrawingSync.SyncRootPlotFieldsFromFirstDrawing(pi.Record);
    }

    private static void CopyFenceAndPdfDrawingState(ProductRecord from, ProductRecord to)
    {
        to.LinkFences = from.LinkFences.Select(f => new LinkFenceBox
        {
            FenceId = f.FenceId == Guid.Empty ? Guid.NewGuid() : f.FenceId,
            MinX = f.MinX,
            MinY = f.MinY,
            MaxX = f.MaxX,
            MaxY = f.MaxY,
        }).ToList();

        to.PdfDrawings = from.PdfDrawings.Select(d => new ProductPdfDrawing
        {
            FenceId = d.FenceId,
            PdfTitle = d.PdfTitle,
            PdfRevision = d.PdfRevision,
            PlotPaperSize = PlotPaperSizes.NormalizeOrDefault(d.PlotPaperSize),
            PlotLandscape = d.PlotLandscape,
            LastPdfExportUtc = d.LastPdfExportUtc,
            UploadedPdfRelativePath = string.IsNullOrWhiteSpace(d.UploadedPdfRelativePath)
                ? null
                : d.UploadedPdfRelativePath.Trim(),
        }).ToList();
    }

    private void OnPdfDrawingsListSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || _pdfDrawingUiSync)
            return;
        if (ProductGrid.SelectedItem is not ProductPaletteRow pi)
            return;
        LoadPdfDrawingEditorFromSelection(pi);
        UpdatePlotOrientationUi();
        UpdateLinkFencePreview();
        UpdateProductActionButtonsEnabled();
    }

    private void OnRemovePdfDrawingClick(object sender, RoutedEventArgs e)
    {
        if (_loading || ProductGrid.SelectedItems.Count != 1 || ProductGrid.SelectedItem is not ProductPaletteRow pi)
            return;
        if (PdfDrawingsList.SelectedItem is not ListBoxItem lbi || lbi.Tag is not Guid fenceId)
        {
            System.Windows.MessageBox.Show(
                "Kaldırmak için listeden bir PDF çizimi seçin.",
                "BIM Prefab",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        ProductPdfDrawingSync.NormalizeProductRecord(pi.Record);
        var fence = pi.Record.LinkFences.FirstOrDefault(f => f.FenceId == fenceId && f.IsValid());
        var drawing = pi.Record.PdfDrawings.FirstOrDefault(d => d.FenceId == fenceId);
        if (fence is null && drawing is null)
        {
            System.Windows.MessageBox.Show(
                "Seçili çit / çizim bulunamadı.",
                "BIM Prefab",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        if (System.Windows.MessageBox.Show(
                "Seçili PDF çizimi ve ilgili çit alanı kaldırılacak. Kaydet ile çizime yazılır. Devam edilsin mi?",
                "BIM Prefab",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        if (fence is not null)
            pi.Record.LinkFences.Remove(fence);
        if (drawing is not null)
            pi.Record.PdfDrawings.Remove(drawing);

        ProductPdfDrawingSync.NormalizeProductRecord(pi.Record);
        RebuildPdfDrawingsList(pi);
        UpdatePlotOrientationUi();
        UpdateLinkFencePreview();
        UpdateProductActionButtonsEnabled();
    }

    private void OnPdfDrawingTitleBoxTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_loading || _pdfDrawingUiSync)
            return;
        if (ProductGrid.SelectedItem is not ProductPaletteRow pi)
            return;
        if (PdfDrawingsList.SelectedItem is not ListBoxItem lbi || lbi.Tag is not Guid fid)
            return;
        var spec = ProductPdfDrawingSync.FindDrawing(pi.Record, fid);
        if (spec is null)
            return;
        spec.PdfTitle = string.IsNullOrWhiteSpace(PdfDrawingTitleBox.Text) ? "cizim" : PdfDrawingTitleBox.Text.Trim();
        lbi.Content = FormatPdfDrawingListLabel(spec);
        if (PdfDrawingExportInfoText is not null)
        {
            var lastLine = spec.LastPdfExportUtc.HasValue
                ? "Son dışa aktarma (yerel): " +
                  spec.LastPdfExportUtc.Value.ToLocalTime().ToString("F", CultureInfo.CurrentCulture)
                : "Son dışa aktarma: henüz yok.";
            PdfDrawingExportInfoText.Text = lastLine + "\nDosya adı: " + ProductPdfExportService.BuildProductPdfFileName(pi.Record, spec);
        }
    }

    private void OnPdfDrawingRevisionBoxLostFocus(object sender, RoutedEventArgs e)
    {
        if (_loading || _pdfDrawingUiSync)
            return;
        if (PdfDrawingRevisionBox is null || PdfDrawingExportInfoText is null)
            return;
        if (ProductGrid.SelectedItem is not ProductPaletteRow pi)
            return;
        if (PdfDrawingsList.SelectedItem is not ListBoxItem lbi || lbi.Tag is not Guid fid)
            return;
        var spec = ProductPdfDrawingSync.FindDrawing(pi.Record, fid);
        if (spec is null)
            return;

        var revText = PdfDrawingRevisionBox.Text?.Trim() ?? "0";
        if (!int.TryParse(revText, NumberStyles.Integer, CultureInfo.CurrentCulture, out var pdfRev)
            && !int.TryParse(revText, NumberStyles.Integer, CultureInfo.InvariantCulture, out pdfRev))
            pdfRev = 0;
        if (pdfRev < 0)
            pdfRev = 0;
        spec.PdfRevision = pdfRev;

        _pdfDrawingUiSync = true;
        try
        {
            PdfDrawingRevisionBox.Text = pdfRev.ToString(CultureInfo.InvariantCulture);
        }
        finally
        {
            _pdfDrawingUiSync = false;
        }

        lbi.Content = FormatPdfDrawingListLabel(spec);
        var lastLine = spec.LastPdfExportUtc.HasValue
            ? "Son dışa aktarma (yerel): " +
              spec.LastPdfExportUtc.Value.ToLocalTime().ToString("F", CultureInfo.CurrentCulture)
            : "Son dışa aktarma: henüz yok.";
        PdfDrawingExportInfoText.Text = lastLine + "\nDosya adı: " + ProductPdfExportService.BuildProductPdfFileName(pi.Record, spec);
    }

    private void OnPdfDrawingPaperSizeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || _pdfDrawingUiSync)
            return;
        if (ProductGrid.SelectedItem is not ProductPaletteRow pi)
            return;
        if (PdfDrawingsList.SelectedItem is not ListBoxItem lbi || lbi.Tag is not Guid fid)
            return;
        var spec = ProductPdfDrawingSync.FindDrawing(pi.Record, fid);
        if (spec is null || PaperSizeCombo.SelectedItem is not string pap)
            return;
        spec.PlotPaperSize = PlotPaperSizes.NormalizeOrDefault(pap);
        ProductPdfDrawingSync.SyncRootPlotFieldsFromFirstDrawing(pi.Record);
        lbi.Content = FormatPdfDrawingListLabel(spec);
        UpdateLinkFencePreview();
    }

    private void OnPlotOrientationChanged(object sender, RoutedEventArgs e)
    {
        if (_loading || _pdfDrawingUiSync)
            return;
        if (ProductGrid.SelectedItems.Count != 1 || ProductGrid.SelectedItem is not ProductPaletteRow pi)
            return;
        if (PlotOrientationLandscapeRadio is null || PlotOrientationPortraitRadio is null)
            return;
        var spec = GetSelectedPdfDrawing(pi);
        if (spec is null)
            return;
        spec.PlotLandscape = PlotOrientationLandscapeRadio.IsChecked == true;
        ProductPdfDrawingSync.SyncRootPlotFieldsFromFirstDrawing(pi.Record);
        if (PdfDrawingsList.SelectedItem is ListBoxItem lbi && lbi.Tag is Guid fid &&
            ProductPdfDrawingSync.FindDrawing(pi.Record, fid) is { } d)
            lbi.Content = FormatPdfDrawingListLabel(d);
        UpdateLinkFencePreview();
    }

    private void OnLinkFencePreviewHostSizeChanged(object sender, SizeChangedEventArgs e) =>
        UpdateLinkFencePreview();

    private void PushCheckedToSession()
    {
        PrefabUiSession.SetExportCheckedProductIds(_productItems.Where(r => r.IsChecked).Select(r => r.Record.ProductId));
    }

    private void OnProductRowCheckedChanged(object? sender, EventArgs e)
    {
        if (_loading || _suppressSelectAll)
            return;
        PushCheckedToSession();
        UpdateSelectAllFromRows();
    }

    private void UpdateSelectAllFromRows()
    {
        if (SelectAllProductsCheck is null)
            return;
        _suppressSelectAll = true;
        if (_productItems.Count == 0)
            SelectAllProductsCheck.IsChecked = false;
        else if (_productItems.All(r => r.IsChecked))
            SelectAllProductsCheck.IsChecked = true;
        else if (_productItems.Any(r => r.IsChecked))
            SelectAllProductsCheck.IsChecked = null;
        else
            SelectAllProductsCheck.IsChecked = false;
        _suppressSelectAll = false;
    }

    private void OnSelectAllProductsClick(object sender, RoutedEventArgs e)
    {
        if (_loading || sender is not System.Windows.Controls.CheckBox cb)
            return;

        var mark = cb.IsChecked == true;
        _suppressSelectAll = true;
        foreach (var r in _productItems)
            r.IsChecked = mark;
        _suppressSelectAll = false;
        PushCheckedToSession();
    }

    private void OnClearProductLinks(object sender, RoutedEventArgs e)
    {
        var doc = AcadApp.DocumentManager.MdiActiveDocument;
        if (doc is null)
            return;

        PushCheckedToSession();
        var ids = GetTargetProductIdsForBulkAction();
        if (ids.Count == 0)
        {
            System.Windows.MessageBox.Show(
                "Önce ürün işaretleyin veya listede satır seçin (polyline / XData temizlenecek ürünler).",
                "BIM Prefab",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var nameLines = EnumerateDisplayNamesForProductIds(ids.Distinct().ToList()).ToList();
        var bullet = FormatProductNamesBulletSection(nameLines);
        var msg =
            "Aşağıdaki ürünlerin çerçeve (polyline çit) kayıtları ve çizim bağlantıları (XData) kaldırılacak:\n\n" +
            bullet +
            "\n\nDevam edilsin mi?";

        if (System.Windows.MessageBox.Show(msg, "BIM Prefab", MessageBoxButton.YesNo, MessageBoxImage.Question) !=
            MessageBoxResult.Yes)
            return;

        try
        {
            var n = ProductLinkClearService.ClearForProducts(doc, ids);
            RefreshFromActiveDocument();
            SetStatus($"{ids.Count} ürün kaydı güncellendi; {n} nesne bağlantısı kaldırıldı.");
        }
        catch (System.Exception ex)
        {
            System.Windows.MessageBox.Show("Temizleme hatası: " + ex.Message, "BIM Prefab", MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private static IReadOnlyList<Guid> GetTargetProductIdsForBulkAction()
    {
        if (PrefabUiSession.ExportCheckedProductIds.Count > 0)
            return PrefabUiSession.ExportCheckedProductIds.ToList();
        return PrefabUiSession.SelectedProductIds.ToList();
    }

    private void SyncListSelectionToSession()
    {
        var ids = new List<Guid>();
        foreach (var item in ProductGrid.SelectedItems.Cast<object>())
        {
            if (item is ProductPaletteRow pi)
                ids.Add(pi.Record.ProductId);
        }

        PrefabUiSession.SetSelectedProductIds(ids);
    }

    private bool TryGetSelectedProductId(out Guid id)
    {
        id = Guid.Empty;
        SyncListSelectionToSession();
        if (!PrefabUiSession.SelectedProductId.HasValue)
            return false;
        id = PrefabUiSession.SelectedProductId.Value;
        return true;
    }

    private void OnProductGridSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading)
            return;
        ApplySelectionToFields();
    }

    private void ApplySelectionToFields()
    {
        SyncListSelectionToSession();
        var n = ProductGrid.SelectedItems.Count;

        _loading = true;
        try
        {
            if (n == 1 && ProductGrid.SelectedItem is ProductPaletteRow pi)
            {
                NameBox.IsReadOnly = false;
                CodeBox.IsReadOnly = false;
                QtyBox.IsReadOnly = false;
                PaperSizeCombo.IsEnabled = true;
                PdfDrawingsList.IsEnabled = true;
                PdfDrawingTitleBox.IsEnabled = true;
                if (PdfDrawingRevisionBox is not null)
                    PdfDrawingRevisionBox.IsEnabled = true;
                NameBox.Text = pi.Record.DisplayName;
                CodeBox.Text = pi.Record.Code;
                QtyBox.Text = pi.Record.Quantity.ToString(CultureInfo.CurrentCulture);
                RevisionBox.Text = pi.Record.Revision.ToString(CultureInfo.CurrentCulture);
                NoteBox.Text = pi.Record.Note ?? "";
                RevisionBox.IsReadOnly = false;
                MaterialsGrid.IsReadOnly = false;
                MaterialsGrid.IsEnabled = true;
                RebarsGrid.IsReadOnly = false;
                RebarsGrid.IsEnabled = true;
                LoadMaterialsForProduct(pi.Record);
                LoadRebarsForProduct(pi.Record);
                LoadPaletteTypology(pi.Record);
                RebuildPdfDrawingsList(pi);
                SetStatus($"Seçili: {pi.Record.DisplayName}");
            }
            else if (n > 1)
            {
                NameBox.IsReadOnly = true;
                CodeBox.IsReadOnly = true;
                QtyBox.IsReadOnly = true;
                PaperSizeCombo.IsEnabled = false;
                PdfDrawingsList.IsEnabled = false;
                PdfDrawingTitleBox.IsEnabled = false;
                if (PdfDrawingRevisionBox is not null)
                {
                    PdfDrawingRevisionBox.IsEnabled = false;
                    PdfDrawingRevisionBox.Text = "";
                }
                PdfDrawingsList.Items.Clear();
                PdfDrawingTitleBox.Text = "";
                if (PdfDrawingExportInfoText is not null)
                    PdfDrawingExportInfoText.Text = "";
                NameBox.Text = $"({n} ürün seçili)";
                CodeBox.Text = "";
                QtyBox.Text = "0";
                RevisionBox.Text = "0";
                RevisionBox.IsReadOnly = true;
                _materials.Clear();
                _rebars.Clear();
                MaterialsGrid.IsEnabled = false;
                RebarsGrid.IsEnabled = false;
                ClearPaletteTypologyUi();
                SetStatus($"{n} ürün seçili. Dışa aktarma: işaret kutuları (veya liste seçimi yoksa tümü).");
            }
            else
            {
                NameBox.IsReadOnly = false;
                CodeBox.IsReadOnly = false;
                QtyBox.IsReadOnly = false;
                PaperSizeCombo.IsEnabled = false;
                PdfDrawingsList.IsEnabled = false;
                PdfDrawingTitleBox.IsEnabled = false;
                if (PdfDrawingRevisionBox is not null)
                {
                    PdfDrawingRevisionBox.IsEnabled = false;
                    PdfDrawingRevisionBox.Text = "";
                }
                PdfDrawingsList.Items.Clear();
                PdfDrawingTitleBox.Text = "";
                if (PdfDrawingExportInfoText is not null)
                    PdfDrawingExportInfoText.Text = "";
                NameBox.Text = "";
                CodeBox.Text = "";
                QtyBox.Text = "0";
                RevisionBox.Text = "0";
                PaperSizeCombo.SelectedItem = PlotPaperSizes.Default;
                RevisionBox.IsReadOnly = true;
                _materials.Clear();
                _rebars.Clear();
                MaterialsGrid.IsEnabled = false;
                RebarsGrid.IsEnabled = false;
                ClearPaletteTypologyUi();
                SetStatus("Ürün seçin veya yeni oluşturun.");
            }
        }
        finally
        {
            _loading = false;
        }

        UpdateProductActionButtonsEnabled();
        UpdatePlotOrientationUi();
        UpdateLinkFencePreview();
    }

    private void UpdatePlotOrientationUi()
    {
        if (PlotOrientationPortraitRadio is null || PlotOrientationLandscapeRadio is null)
            return;
        if (ProductGrid.SelectedItems.Count == 1 && ProductGrid.SelectedItem is ProductPaletteRow pi)
        {
            var spec = GetSelectedPdfDrawing(pi);
            PlotOrientationPortraitRadio.IsEnabled = spec is not null;
            PlotOrientationLandscapeRadio.IsEnabled = spec is not null;
            if (spec is not null)
            {
                PlotOrientationPortraitRadio.IsChecked = !spec.PlotLandscape;
                PlotOrientationLandscapeRadio.IsChecked = spec.PlotLandscape;
            }
            else
            {
                PlotOrientationPortraitRadio.IsChecked = true;
                PlotOrientationLandscapeRadio.IsChecked = false;
            }
        }
        else
        {
            PlotOrientationPortraitRadio.IsEnabled = false;
            PlotOrientationLandscapeRadio.IsEnabled = false;
            PlotOrientationPortraitRadio.IsChecked = true;
            PlotOrientationLandscapeRadio.IsChecked = false;
        }
    }

    private void UpdateLinkFencePreview()
    {
        if (LinkFencePreviewCanvas is null || FencePreviewOverlayText is null || FencePreviewCaptionText is null)
            return;

        LinkFencePreviewCanvas.Children.Clear();
        FencePreviewOverlayText.Visibility = Visibility.Visible;

        if (ProductGrid.SelectedItems.Count != 1 || ProductGrid.SelectedItem is not ProductPaletteRow pi)
        {
            FencePreviewOverlayText.Text = "Ürün seçildiğinde kayıtlı çitler burada görünür.";
            FencePreviewCaptionText.Text = "";
            return;
        }

        var rec = pi.Record;
        ProductPdfDrawingSync.NormalizeProductRecord(rec);
        var allBoxes = rec.LinkFences.Where(b => b.IsValid()).ToList();
        if (allBoxes.Count == 0)
        {
            FencePreviewOverlayText.Text =
                "Kayıtlı çizim sınırı yok. Üstte «Çizim sınırı seç» ile kapalı polyline atayın.";
            FencePreviewCaptionText.Text = "";
            return;
        }

        List<LinkFenceBox> boxes;
        if (PdfDrawingsList.SelectedItem is ListBoxItem selItem && selItem.Tag is Guid fid)
        {
            var one = allBoxes.FirstOrDefault(b => b.FenceId == fid);
            boxes = one is not null ? new List<LinkFenceBox> { one } : allBoxes;
        }
        else
        {
            boxes = allBoxes;
        }

        var spec = GetSelectedPdfDrawing(pi);
        var useLandscape = spec?.PlotLandscape ?? rec.PlotLandscape;

        FencePreviewOverlayText.Visibility = Visibility.Collapsed;

        var host = LinkFencePreviewHost;
        if (host is null)
            return;
        var cw = host.ActualWidth;
        var ch = host.ActualHeight;
        if (cw < 16 || ch < 16)
            return;

        LinkFencePreviewCanvas.Width = cw;
        LinkFencePreviewCanvas.Height = ch;

        var minX = boxes.Min(b => b.MinX);
        var maxX = boxes.Max(b => b.MaxX);
        var minY = boxes.Min(b => b.MinY);
        var maxY = boxes.Max(b => b.MaxY);
        var dx = maxX - minX;
        var dy = maxY - minY;
        if (dx <= 1e-9 || dy <= 1e-9)
        {
            FencePreviewOverlayText.Visibility = Visibility.Visible;
            FencePreviewOverlayText.Text = "Çit boyutu okunamadı.";
            FencePreviewCaptionText.Text = "";
            return;
        }

        const double margin = 8;
        var innerW = cw - 2 * margin;
        var innerH = ch - 2 * margin;
        var frameAspect = useLandscape ? 297.0 / 210.0 : 210.0 / 297.0;
        double frameW, frameH;
        if (innerW / innerH > frameAspect)
        {
            frameH = innerH;
            frameW = frameH * frameAspect;
        }
        else
        {
            frameW = innerW;
            frameH = frameW / frameAspect;
        }

        var offX = margin + (innerW - frameW) / 2;
        var offY = margin + (innerH - frameH) / 2;

        const double inset = 10;
        var sx = (frameW - 2 * inset) / dx;
        var sy = (frameH - 2 * inset) / dy;
        var sc = Math.Min(sx, sy);
        var wcx = (minX + maxX) / 2;
        var wcy = (minY + maxY) / 2;
        var frameCx = offX + frameW / 2;
        var frameCy = offY + frameH / 2;

        System.Windows.Point Map(double wx, double wy) =>
            new(frameCx + (wx - wcx) * sc, frameCy - (wy - wcy) * sc);

        List<(Point2d A, Point2d B)>? wireSegs = null;
        try
        {
            var doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc is not null)
            {
                using (doc.LockDocument())
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    wireSegs = ModelFenceWireframePreview.CollectSegments(tr, doc.Database, minX, minY, maxX, maxY);
                    tr.Commit();
                }
            }
        }
        catch
        {
            wireSegs = null;
        }

        var paper = new System.Windows.Shapes.Rectangle
        {
            Width = frameW,
            Height = frameH,
            Fill = System.Windows.Media.Brushes.White,
            Stroke = new SolidColorBrush(System.Windows.Media.Color.FromRgb(180, 190, 200)),
            StrokeThickness = 1,
        };
        Canvas.SetLeft(paper, offX);
        Canvas.SetTop(paper, offY);
        LinkFencePreviewCanvas.Children.Add(paper);

        var wireDrawn = 0;
        if (wireSegs is { Count: > 0 })
        {
            const int maxDraw = 4000;
            var list = wireSegs.Count > maxDraw ? wireSegs.Take(maxDraw).ToList() : wireSegs;
            wireDrawn = list.Count;
            var pg = new PathGeometry { FillRule = FillRule.Nonzero };
            foreach (var seg in list)
            {
                var aM = Map(seg.A.X, seg.A.Y);
                var bM = Map(seg.B.X, seg.B.Y);
                var fig = new PathFigure { StartPoint = aM, IsClosed = false };
                fig.Segments.Add(new LineSegment(bM, true));
                pg.Figures.Add(fig);
            }

            var clipRect = new RectangleGeometry(new Rect(
                offX + inset,
                offY + inset,
                Math.Max(1, frameW - 2 * inset),
                Math.Max(1, frameH - 2 * inset)));
            var wirePath = new Path
            {
                Data = pg,
                Stroke = new SolidColorBrush(System.Windows.Media.Color.FromRgb(60, 72, 88)),
                StrokeThickness = 0.55,
                Opacity = 0.92,
                Clip = clipRect,
                SnapsToDevicePixels = true,
            };
            Canvas.SetLeft(wirePath, 0);
            Canvas.SetTop(wirePath, 0);
            LinkFencePreviewCanvas.Children.Add(wirePath);
        }

        for (var i = 0; i < boxes.Count; i++)
        {
            var box = boxes[i];
            var hue = (40 + i * 55) % 200;
            var poly = new Polygon
            {
                Stroke = new SolidColorBrush(System.Windows.Media.Color.FromRgb((byte)Math.Min(255, 30 + hue), 90, 160)),
                StrokeThickness = 1.5,
                Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(85, (byte)Math.Min(255, 80 + hue), 120, 200)),
            };
            poly.Points.Add(Map(box.MinX, box.MinY));
            poly.Points.Add(Map(box.MaxX, box.MinY));
            poly.Points.Add(Map(box.MaxX, box.MaxY));
            poly.Points.Add(Map(box.MinX, box.MaxY));
            LinkFencePreviewCanvas.Children.Add(poly);
        }

        string cap;
        if (boxes.Count == 1 && spec is not null)
        {
            cap =
                $"Seçili PDF çizimi «{spec.PdfTitle}» — {PlotPaperSizes.NormalizeOrDefault(spec.PlotPaperSize)}, " +
                $"{dx:0.##} × {dy:0.##} birim. " +
                (useLandscape ? "Yatay (landscape) çerçeve." : "Dikey (portrait) çerçeve.");
        }
        else
        {
            cap =
                $"{boxes.Count} çit bölgesi — WCS birleşik alan ≈ {dx:0.##} × {dy:0.##} (birim). " +
                (useLandscape ? "Yatay (landscape) çerçeve." : "Dikey (portrait) çerçeve.");
        }

        if (wireDrawn > 0)
            cap += $" Model tel kafes: {wireDrawn} çizgi segmenti (PDF penceresi ile aynı WCS alanı).";
        FencePreviewCaptionText.Text = cap;
    }

    private void LoadMaterialsForProduct(ProductRecord record)
    {
        _materials.Clear();
        foreach (var m in record.Materials ?? [])
        {
            _materials.Add(new MaterialLine
            {
                Category = m.Category,
                Code = m.Code,
                MaterialCatalogCode = m.MaterialCatalogCode,
                Description = m.Description,
                Quantity = m.Quantity,
                Unit = m.Unit,
                Notes = m.Notes,
            });
        }
    }

    private void CopyMaterialsFromGridTo(ProductRecord target)
    {
        target.Materials = _materials.Select(m => new MaterialLine
        {
            Category = m.Category ?? "",
            Code = m.Code ?? "",
            MaterialCatalogCode = m.MaterialCatalogCode ?? "",
            Description = m.Description ?? "",
            Quantity = m.Quantity,
            Unit = m.Unit ?? "",
            Notes = m.Notes ?? "",
        }).ToList();
    }

    private void LoadRebarsForProduct(ProductRecord record)
    {
        _rebars.Clear();
        var poz = 0;
        foreach (var r in record.Rebars ?? [])
        {
            poz++;
            var row = new RebarLine
            {
                PozNo = r.PozNo,
                DiameterMm = r.DiameterMm,
                Count = r.Count,
                LengthH_mm = r.LengthH_mm,
                LengthL_mm = r.LengthL_mm,
                Notes = r.Notes,
                SteelGrade = r.SteelGrade,
                Shape = r.Shape,
                DevelopedLengthMm = r.DevelopedLengthMm,
                TotalWeightKg = r.TotalWeightKg,
            };
            RebarWeightHelper.NormalizeRebarRow(row, poz);
            _rebars.Add(row);
        }
    }

    private void CopyRebarsFromGridTo(ProductRecord target)
    {
        var list = new List<RebarLine>();
        var poz = 0;
        foreach (var r in _rebars)
        {
            poz++;
            var row = new RebarLine
            {
                PozNo = r.PozNo ?? "",
                DiameterMm = r.DiameterMm,
                Count = r.Count,
                LengthH_mm = r.LengthH_mm,
                LengthL_mm = r.LengthL_mm,
                Notes = r.Notes ?? "",
                SteelGrade = string.IsNullOrWhiteSpace(r.SteelGrade) ? "B500C" : r.SteelGrade,
                Shape = string.IsNullOrWhiteSpace(r.Shape) ? "straight" : r.Shape,
            };
            RebarWeightHelper.NormalizeRebarRow(row, poz);
            list.Add(row);
        }

        target.Rebars = list;
    }

    private void OnRebarsGridCellEditEnding(object? sender, DataGridCellEditEndingEventArgs e)
    {
        if (e.Row.Item is not RebarLine row)
            return;
        var poz = _rebars.IndexOf(row) + 1;
        if (poz <= 0)
            poz = 1;
        RebarWeightHelper.NormalizeRebarRow(row, poz);
        RebarsGrid.Items.Refresh();
    }

    private void PopulatePaletteCategoryCombo()
    {
        var cat = AttributeCatalogService.Default;
        var categories = cat.Categories.Count > 0
            ? cat.Categories.ToList()
            :
            [
                new CategoryDefinition { Id = "superstructure", DisplayName = "Üstyapı" },
                new CategoryDefinition { Id = "substructure", DisplayName = "Altyapı" },
            ];
        PaletteCategoryCombo.ItemsSource = categories;
    }

    private void PopulatePaletteElementTypeCombo(string? categoryId)
    {
        var list = AttributeCatalogService.Default.GetElementTypesForCategory(categoryId).ToList();
        if (list.Count == 0)
            list = AttributeCatalogService.Default.ElementTypes.ToList();
        PaletteElementTypeCombo.ItemsSource = list;
    }

    private void OnPaletteCategoryComboSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!PaletteCategoryCombo.IsEnabled || _loading || _paletteCategoryLoading || _paletteTypologyLoading)
            return;
        if (PaletteCategoryCombo.SelectedItem is not CategoryDefinition catDef)
            return;

        _paletteCategoryLoading = true;
        try
        {
            PopulatePaletteElementTypeCombo(catDef.Id);
            if (ProductGrid.SelectedItem is ProductPaletteRow pi)
                pi.Record.ElementCategoryId = catDef.Id;
            if (PaletteElementTypeCombo.Items.Count > 0)
                PaletteElementTypeCombo.SelectedIndex = 0;
        }
        finally
        {
            _paletteCategoryLoading = false;
        }
    }

    private void ClearPaletteTypologyUi()
    {
        _paletteTypologyLoading = true;
        try
        {
            PaletteCategoryCombo.IsEnabled = true;
            PaletteElementTypeCombo.IsEnabled = false;
            PaletteTypologyCombo.IsEnabled = false;
            PaletteDimGrid.IsEnabled = false;
            _paletteDimRows.Clear();
            PaletteElementTypeCombo.ItemsSource = null;
            PaletteElementTypeCombo.SelectedItem = null;
            PaletteTypologyCombo.ItemsSource = null;
            PaletteTypologyCombo.SelectedItem = null;
        }
        finally
        {
            _paletteTypologyLoading = false;
        }
    }

    private void LoadPaletteTypology(ProductRecord record)
    {
        _paletteTypologyLoading = true;
        try
        {
            AttributeCatalogService.Default.ApplyDefaultPrefabSelection(record);
            SelectPaletteCombosFromRecord(record);
            RebuildPaletteAttributeRows(record);
            PaletteCategoryCombo.IsEnabled = true;
            PaletteElementTypeCombo.IsEnabled = true;
            PaletteTypologyCombo.IsEnabled = true;
            PaletteDimGrid.IsEnabled = true;
        }
        finally
        {
            _paletteTypologyLoading = false;
        }
    }

    private void SelectPaletteCombosFromRecord(ProductRecord record)
    {
        var cat = AttributeCatalogService.Default;
        var categoryId = record.ElementCategoryId?.Trim()
                         ?? cat.GetCategoryIdForElementType(record.PrefabElementTypeId)
                         ?? "superstructure";
        record.ElementCategoryId = categoryId;

        _paletteCategoryLoading = true;
        try
        {
            PopulatePaletteElementTypeCombo(categoryId);
            var categories = PaletteCategoryCombo.ItemsSource?.Cast<CategoryDefinition>().ToList() ?? [];
            PaletteCategoryCombo.SelectedItem = categories.FirstOrDefault(c =>
                                                  string.Equals(c.Id, categoryId, StringComparison.OrdinalIgnoreCase))
                                              ?? categories.FirstOrDefault();
        }
        finally
        {
            _paletteCategoryLoading = false;
        }

        var et = cat.TryGetElementType(record.PrefabElementTypeId)
                 ?? cat.GetElementTypesForCategory(categoryId).FirstOrDefault()
                 ?? cat.ElementTypes.FirstOrDefault();
        if (et is null)
            return;
        record.PrefabElementTypeId = et.Id;
        PaletteElementTypeCombo.SelectedItem = et;
        PopulatePaletteTypologyCombo(et.Id);

        var typList = PaletteTypologyCombo.ItemsSource?.Cast<TypologyCatalogDefinition>().ToList() ?? [];
        var typ = typList.FirstOrDefault(t =>
                      string.Equals(t.Id, record.PrefabTypologyId, StringComparison.OrdinalIgnoreCase))
                  ?? typList.FirstOrDefault();
        if (typ is not null)
        {
            record.PrefabTypologyId = typ.Id;
            PaletteTypologyCombo.SelectedItem = typ;
        }
        else
        {
            record.PrefabTypologyId = null;
            PaletteTypologyCombo.SelectedItem = null;
        }
    }

    private void PopulatePaletteTypologyCombo(string elementTypeId)
    {
        var list = AttributeCatalogService.Default.GetTypologiesForElementType(elementTypeId).ToList();
        PaletteTypologyCombo.ItemsSource = list;
    }

    private void RebuildPaletteAttributeRows(ProductRecord record)
    {
        _paletteDimRows.Clear();
        foreach (var f in AttributeCatalogService.Default.GetAttributeFieldsForTypology(record.PrefabTypologyId))
        {
            var row = new AttributeValueRow(f.Tag, f.Label, f.Type);
            if (record.Attributes.TryGetValue(f.Tag, out var v))
                row.Value = v ?? "";
            _paletteDimRows.Add(row);
        }
    }

    private void OnPaletteElementTypeComboSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!PaletteElementTypeCombo.IsEnabled || _loading || _paletteTypologyLoading)
            return;
        if (ProductGrid.SelectedItem is not ProductPaletteRow pi)
            return;
        if (PaletteElementTypeCombo.SelectedItem is not ElementTypeDefinition et)
            return;

        pi.Record.PrefabElementTypeId = et.Id;
        pi.Record.ElementCategoryId = AttributeCatalogService.Default.GetCategoryIdForElementType(et.Id);
        PopulatePaletteTypologyCombo(et.Id);
        var typ = (PaletteTypologyCombo.ItemsSource as IEnumerable<TypologyCatalogDefinition>)?.FirstOrDefault();
        PaletteTypologyCombo.SelectedItem = typ;
        pi.Record.PrefabTypologyId = typ?.Id;
        RebuildPaletteAttributeRows(pi.Record);
    }

    private void OnPaletteTypologyComboSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!PaletteTypologyCombo.IsEnabled || _loading || _paletteTypologyLoading)
            return;
        if (ProductGrid.SelectedItem is not ProductPaletteRow pi)
            return;
        if (PaletteTypologyCombo.SelectedItem is not TypologyCatalogDefinition typ)
            return;
        pi.Record.PrefabTypologyId = typ.Id;
        RebuildPaletteAttributeRows(pi.Record);
    }

    private static void RemoveAttributeKeyInsensitive(Dictionary<string, string> attributes, string tag)
    {
        var match = attributes.Keys.FirstOrDefault(k =>
            string.Equals(k, tag, StringComparison.OrdinalIgnoreCase));
        if (match is not null)
            attributes.Remove(match);
    }

    private void FlushPaletteTypologyToRecord(ProductRecord target)
    {
        foreach (var r in _paletteDimRows)
        {
            var trimmed = r.Value?.Trim() ?? "";
            if (trimmed.Length == 0)
                RemoveAttributeKeyInsensitive(target.Attributes, r.Tag);
            else
                target.Attributes[r.Tag] = trimmed;
        }

        if (PaletteElementTypeCombo.SelectedItem is ElementTypeDefinition et)
            target.PrefabElementTypeId = et.Id;
        if (PaletteTypologyCombo.SelectedItem is TypologyCatalogDefinition t)
            target.PrefabTypologyId = t.Id;
        target.ProductCategoryId = string.IsNullOrWhiteSpace(target.PrefabElementTypeId)
            ? null
            : target.PrefabElementTypeId.Trim();
    }

    private void OnDetailTabsSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DetailTabs.SelectedIndex == 0)
            UpdateLinkFencePreview();
    }

    private static bool TryParseQty(string? text, out double qty)
    {
        qty = 0;
        if (string.IsNullOrWhiteSpace(text))
            return true;
        return double.TryParse(text.Trim(), NumberStyles.Any, CultureInfo.CurrentCulture, out qty)
               || double.TryParse(text.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out qty);
    }

    private void OnNewProduct(object sender, RoutedEventArgs e)
    {
        var doc = AcadApp.DocumentManager.MdiActiveDocument;
        if (doc is null)
        {
            System.Windows.MessageBox.Show("Aktif çizim yok.", "BIM Prefab", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var name = string.IsNullOrWhiteSpace(NameBox.Text) ? "Yeni ürün" : NameBox.Text.Trim();
        var code = CodeBox.Text?.Trim() ?? "";
        if (!TryParseQty(QtyBox.Text, out var qty))
            qty = 1;

        var record = new ProductRecord
        {
            ProductId = Guid.NewGuid(),
            DisplayName = name,
            Code = code,
            Quantity = qty,
            Unit = "adet",
            Revision = 0,
        };
        AttributeCatalogService.Default.ApplyDefaultPrefabSelection(record);

        DrawingInitService.EnsureInit(doc);
        using var docLock = doc.LockDocument();
        using (var tr = doc.Database.TransactionManager.StartTransaction())
        {
            new RegistryService().SaveProduct(tr, doc.Database, record);
            new XDataService().EnsureRegApp(tr, doc.Database);
            tr.Commit();
        }

        PrefabUiSession.SetSelectedProductId(record.ProductId);
        RefreshFromActiveDocument();
        SelectProductInList(record.ProductId);
        if (ProductGrid.SelectedItem is ProductPaletteRow nr)
        {
            nr.IsChecked = true;
            PushCheckedToSession();
            UpdateSelectAllFromRows();
        }

        SetStatus($"Yeni ürün: {record.DisplayName}");
    }

    private void OnSaveProduct(object sender, RoutedEventArgs e)
    {
        var doc = AcadApp.DocumentManager.MdiActiveDocument;
        if (doc is null || ProductGrid.SelectedItems.Count != 1 || ProductGrid.SelectedItem is not ProductPaletteRow pi)
        {
            System.Windows.MessageBox.Show(
                "Kaydetmek için listede tam bir ürün seçin (çoklu seçimde ürün alanları kapalıdır).",
                "BIM Prefab",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        if (!TryParseQty(QtyBox.Text, out var qty))
        {
            System.Windows.MessageBox.Show("Adet sayı olarak okunamadı.", "BIM Prefab", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var revText = RevisionBox.Text?.Trim() ?? "0";
        if (!int.TryParse(revText, NumberStyles.Integer, CultureInfo.CurrentCulture, out var revision)
            && !int.TryParse(revText, NumberStyles.Integer, CultureInfo.InvariantCulture, out revision))
        {
            System.Windows.MessageBox.Show("Revizyon tam sayı olarak okunamadı.", "BIM Prefab", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (revision < 0)
        {
            System.Windows.MessageBox.Show("Revizyon negatif olamaz.", "BIM Prefab", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        DrawingInitService.EnsureInit(doc);
        using var docLock = doc.LockDocument();
        using (var tr = doc.Database.TransactionManager.StartTransaction())
        {
            if (!new RegistryService().TryGetProduct(tr, doc.Database, pi.Record.ProductId, out var current) || current is null)
            {
                tr.Commit();
                System.Windows.MessageBox.Show("Ürün bulunamadı.", "BIM Prefab", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ApplyPdfDrawingUiToSelectedRecord(pi);
            current.DisplayName = string.IsNullOrWhiteSpace(NameBox.Text) ? current.DisplayName : NameBox.Text.Trim();
            current.Code = CodeBox.Text?.Trim().ToUpperInvariant() ?? "";
            current.Quantity = qty;
            current.Unit = "adet";
            current.Revision = revision;
            current.Note = NoteBox.Text?.Trim() ?? "";
            CopyFenceAndPdfDrawingState(pi.Record, current);
            ProductPdfDrawingSync.NormalizeProductRecord(current);
            ProductPdfDrawingSync.SyncRootPlotFieldsFromFirstDrawing(current);
            current.PlotPaperSize =
                PlotPaperSizes.NormalizeOrDefault(current.PdfDrawings.FirstOrDefault()?.PlotPaperSize ??
                                                  PlotPaperSizes.Default);
            current.PlotLandscape = current.PdfDrawings.FirstOrDefault()?.PlotLandscape ?? false;
            FlushPaletteTypologyToRecord(current);
            CopyMaterialsFromGridTo(current);
            CopyRebarsFromGridTo(current);

            var allForValidate = new RegistryService().ListProducts(tr, doc.Database).ToList();
            var ix = allForValidate.FindIndex(x => x.ProductId == current.ProductId);
            if (ix >= 0)
                allForValidate[ix] = current;
            else
                allForValidate.Add(current);

            if (!PrefabExportValidation.TryValidateUniqueProductCodes(allForValidate, out var errCodes))
            {
                tr.Commit();
                System.Windows.MessageBox.Show(errCodes, "BIM Prefab", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!PrefabExportValidation.TryValidateUniquePdfTitlesPerProduct(current, out var errPdfTitles))
            {
                tr.Commit();
                System.Windows.MessageBox.Show(errPdfTitles, "BIM Prefab", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            new RegistryService().SaveProduct(tr, doc.Database, current);
            tr.Commit();
        }

        RefreshFromActiveDocument();
        SelectProductInList(pi.Record.ProductId);
        SetStatus("Ürün, malzemeler ve donatılar kaydedildi.");
    }

    /// <summary>Önce işaretli satırlar; yoksa grid’de vurgulu seçili satırlar.</summary>
    private List<ProductPaletteRow> GetProductsTargetedForBulkOrDelete()
    {
        var byCheck = _productItems.Where(r => r.IsChecked).ToList();
        if (byCheck.Count > 0)
            return byCheck;
        return ProductGrid.SelectedItems.Cast<object>().OfType<ProductPaletteRow>().Distinct().ToList();
    }

    private void OnDeleteProduct(object sender, RoutedEventArgs e)
    {
        var doc = AcadApp.DocumentManager.MdiActiveDocument;
        if (doc is null)
        {
            System.Windows.MessageBox.Show("Aktif çizim yok.", "BIM Prefab", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var toDelete = GetProductsTargetedForBulkOrDelete();
        if (toDelete.Count == 0)
        {
            System.Windows.MessageBox.Show(
                "Silinecek ürün yok. Satırdaki onay kutularını işaretleyin veya listede ürün satırı seçin.",
                "BIM Prefab",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var deleteNames = toDelete
            .Select(r =>
                string.IsNullOrWhiteSpace(r.Record.DisplayName)
                    ? (string.IsNullOrWhiteSpace(r.Record.Code) ? $"Ürün {r.Record.ProductId:D}" : r.Record.Code.Trim())
                    : r.Record.DisplayName.Trim())
            .ToList();
        var bullet = FormatProductNamesBulletSection(deleteNames);
        var msg =
            "Aşağıdaki ürün(ler) kalıcı olarak silinecek:\n\n" +
            bullet +
            "\n\nBu işlem geri alınamaz. Devam edilsin mi?";

        if (System.Windows.MessageBox.Show(msg, "BIM Prefab", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        DrawingInitService.EnsureInit(doc);
        using var docLock = doc.LockDocument();
        using (var tr = doc.Database.TransactionManager.StartTransaction())
        {
            foreach (var item in toDelete)
                new RegistryService().DeleteProduct(tr, doc.Database, item.Record.ProductId);
            tr.Commit();
        }

        PrefabUiSession.SetSelectedProductId(null);
        RefreshFromActiveDocument();
        SetStatus("Ürün(ler) silindi.");
    }

    private void OnAddMaterialRow(object sender, RoutedEventArgs e)
    {
        if (ProductGrid.SelectedItems.Count != 1)
        {
            System.Windows.MessageBox.Show("Malzeme eklemek için listede tek ürün seçin.", "BIM Prefab", MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        _materials.Add(new MaterialLine { Category = "Malzeme", Unit = "ea" });
    }

    private void OnRemoveMaterialRow(object sender, RoutedEventArgs e)
    {
        if (MaterialsGrid.SelectedItem is not MaterialLine line)
            return;
        var i = _materials.IndexOf(line);
        if (i >= 0 && i < _materials.Count)
            _materials.RemoveAt(i);
    }

    private static string GetSuggestedTabularExportBaseName(ProductPaletteRow? row)
    {
        if (row is null)
            return "urun";
        var n = row.Record.DisplayName?.Trim();
        if (!string.IsNullOrWhiteSpace(n))
            return n;
        n = row.Record.Code?.Trim();
        return string.IsNullOrWhiteSpace(n) ? "urun" : n;
    }

    private void OnImportMaterialsCsv(object sender, RoutedEventArgs e)
    {
        if (ProductGrid.SelectedItems.Count != 1)
        {
            System.Windows.MessageBox.Show("CSV içe aktarmak için listede tek ürün seçin.", "BIM Prefab",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        BomImportUiHelper.TryAppendMaterialsFromFile(this, _materials);
    }

    private void OnExportMaterialsCsv(object sender, RoutedEventArgs e)
    {
        if (ProductGrid.SelectedItems.Count != 1 || ProductGrid.SelectedItem is not ProductPaletteRow row)
        {
            System.Windows.MessageBox.Show("CSV indirmek için listede tek ürün seçin.", "BIM Prefab",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        BomTabularExportHelper.TrySaveMaterialsCsv(this, GetSuggestedTabularExportBaseName(row), _materials.ToList());
    }

    private void OnAddRebarRow(object sender, RoutedEventArgs e)
    {
        if (ProductGrid.SelectedItems.Count != 1)
        {
            System.Windows.MessageBox.Show("Donatı eklemek için listede tek ürün seçin.", "BIM Prefab",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _rebars.Add(new RebarLine { PozNo = "", Count = 0 });
    }

    private void OnRemoveRebarRow(object sender, RoutedEventArgs e)
    {
        if (RebarsGrid.SelectedItem is not RebarLine line)
            return;
        var i = _rebars.IndexOf(line);
        if (i >= 0 && i < _rebars.Count)
            _rebars.RemoveAt(i);
    }

    private void OnImportRebarsCsv(object sender, RoutedEventArgs e)
    {
        if (ProductGrid.SelectedItems.Count != 1)
        {
            System.Windows.MessageBox.Show("CSV içe aktarmak için listede tek ürün seçin.", "BIM Prefab",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        BomImportUiHelper.TryAppendRebarsFromFile(this, _rebars);
    }

    private void OnExportRebarsCsv(object sender, RoutedEventArgs e)
    {
        if (ProductGrid.SelectedItems.Count != 1 || ProductGrid.SelectedItem is not ProductPaletteRow row)
        {
            System.Windows.MessageBox.Show("CSV indirmek için listede tek ürün seçin.", "BIM Prefab",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        BomTabularExportHelper.TrySaveRebarsCsv(this, GetSuggestedTabularExportBaseName(row), _rebars.ToList());
    }

    private void OnRectPoly(object sender, RoutedEventArgs e)
    {
        if (!TryGetSelectedProductId(out _))
        {
            System.Windows.MessageBox.Show("Önce listeden ürün seçin.", "BIM Prefab", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var doc = AcadApp.DocumentManager.MdiActiveDocument;
        if (doc is null)
            return;

        doc.SendStringToExecute("BIM_PREFAB_RECT_POLY ", true, false, false);
    }

    private void OnUploadUserPdf(object sender, RoutedEventArgs e)
    {
        var doc = AcadApp.DocumentManager.MdiActiveDocument;
        if (doc is null || ProductGrid.SelectedItems.Count != 1 || ProductGrid.SelectedItem is not ProductPaletteRow pi)
        {
            System.Windows.MessageBox.Show(
                "PDF yüklemek için listede tek ürün seçin ve en az bir çizim sınırı (PDF çizimi) tanımlı olsun.",
                "BIM Prefab",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        ProductPdfDrawingSync.NormalizeProductRecord(pi.Record);
        var spec = GetSelectedPdfDrawing(pi);
        if (spec is null)
        {
            System.Windows.MessageBox.Show(
                "Önce «Çizim sınırı seç» ile sınır oluşturun; ardından soldaki PDF çizimleri listesinden hedef satırı seçin.",
                "BIM Prefab",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Harici PDF seç",
            Filter = "PDF (*.pdf)|*.pdf",
            Multiselect = false,
        };
        if (dlg.ShowDialog(this) != true || string.IsNullOrWhiteSpace(dlg.FileName))
            return;

        DrawingInitService.EnsureInit(doc);
        using var docLock = doc.LockDocument();
        using (var tr = doc.Database.TransactionManager.StartTransaction())
        {
            if (!new RegistryService().TryGetProduct(tr, doc.Database, pi.Record.ProductId, out var current) || current is null)
            {
                tr.Commit();
                System.Windows.MessageBox.Show("Ürün bulunamadı.", "BIM Prefab", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ProductPdfDrawingSync.NormalizeProductRecord(current);
            var match = ProductPdfDrawingSync.FindDrawing(current, spec.FenceId);
            if (match is null)
            {
                tr.Commit();
                System.Windows.MessageBox.Show("Seçili PDF çizimi kayıtta bulunamadı. Paleti yenileyip tekrar deneyin.",
                    "BIM Prefab", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!ProductPdfUploadStorage.TryInstallUserPdf(doc.Database, current.ProductId, match.FenceId, dlg.FileName,
                    out var rel, out var err) || !string.IsNullOrEmpty(err))
            {
                tr.Commit();
                System.Windows.MessageBox.Show(err ?? "PDF yüklenemedi.", "BIM Prefab", MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            match.UploadedPdfRelativePath = rel;
            ProductPdfDrawingSync.SyncRootPlotFieldsFromFirstDrawing(current);
            new RegistryService().SaveProduct(tr, doc.Database, current);
            tr.Commit();
        }

        RefreshFromActiveDocument();
        SelectProductInList(pi.Record.ProductId);
        SetStatus("Harici PDF bu PDF çizimine bağlandı; dışa aktarmada plot yerine kopyalanır.");
    }

    private void OnShowProduct(object sender, RoutedEventArgs e)
    {
        if (!TryGetSelectedProductId(out _))
        {
            System.Windows.MessageBox.Show("Önce listeden ürün seçin.", "BIM Prefab", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var doc = AcadApp.DocumentManager.MdiActiveDocument;
        if (doc is null)
            return;

        doc.SendStringToExecute("BIM_PREFAB_SHOW_PRODUCT ", true, false, false);
    }

    private void OnExportBundle(object sender, RoutedEventArgs e)
    {
        var doc = AcadApp.DocumentManager.MdiActiveDocument;
        if (doc is null)
            return;

        PushCheckedToSession();
        if (!_productItems.Any(r => r.IsChecked))
        {
            System.Windows.MessageBox.Show(
                "Paket dışa aktarma için listede en az bir ürünü işaretleyin (satırdaki onay kutusu veya üstteki «Tüm ürünleri işaretle»).",
                "BIM Prefab",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        PrefabExportInteraction.RunExportBundle(doc, this);
    }

    public void RefreshFromActiveDocument()
    {
        var doc = AcadApp.DocumentManager.MdiActiveDocument;
        var keepIds = PrefabUiSession.SelectedProductIds.ToList();
        var checkedIds = _productItems.Where(r => r.IsChecked).Select(r => r.Record.ProductId).ToHashSet();
        _loading = true;
        _productItems.Clear();

        try
        {
            if (doc is null)
            {
                SetStatus("Çizim yok.");
                return;
            }

            using var docLock = doc.LockDocument();
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                foreach (var p in new RegistryService().ListProducts(tr, doc.Database))
                {
                    var row = new ProductPaletteRow(p);
                    if (checkedIds.Contains(p.ProductId))
                        row.IsChecked = true;
                    row.CheckedChanged += OnProductRowCheckedChanged;
                    _productItems.Add(row);
                }

                tr.Commit();
            }

            ProductGrid.SelectedItems.Clear();
            foreach (var item in _productItems.Where(pi => keepIds.Contains(pi.Record.ProductId)))
                ProductGrid.SelectedItems.Add(item);

            if (ProductGrid.SelectedItems.Count == 0 && _productItems.Count > 0)
                ProductGrid.SelectedItem = _productItems[0];

            SyncListSelectionToSession();
            PushCheckedToSession();
            UpdateSelectAllFromRows();
            SetStatus($"{_productItems.Count} ürün.");
        }
        catch (System.Exception ex)
        {
            SetStatus("Yenileme hatası: " + ex.Message);
        }
        finally
        {
            _loading = false;
        }

        ApplySelectionToFields();
    }

    private void SelectProductInList(Guid productId)
    {
        ProductGrid.SelectedItems.Clear();
        foreach (var item in _productItems.Where(pi => pi.Record.ProductId == productId))
        {
            ProductGrid.SelectedItems.Add(item);
            ProductGrid.ScrollIntoView(item);
            return;
        }
    }

    private void SetStatus(string msg) => StatusText.Text = msg;
}
