using System.Collections.ObjectModel;
using System.Windows;
using BimPrefabExport.Core;
using BimPrefabExport.Services;

namespace BimPrefabExport.UI;

public enum BomImportKind
{
    Materials,
    Rebars,
}

/// <summary>Excel / CSV satırlarını eşleyip önizleme; onayda listeye eklenir.</summary>
public partial class TabularImportPreviewWindow : Window
{
    private readonly BomImportKind _kind;
    private readonly IReadOnlyList<string[]> _rawRows;
    private readonly ObservableCollection<MaterialLine> _materialPreview = new();
    private readonly ObservableCollection<RebarLine> _rebarPreview = new();

    public TabularImportPreviewWindow(BomImportKind kind, IReadOnlyList<string[]> rawRows)
    {
        InitializeComponent();
        _kind = kind;
        _rawRows = rawRows;
    }

    public IReadOnlyList<MaterialLine>? AcceptedMaterials { get; private set; }
    public IReadOnlyList<RebarLine>? AcceptedRebars { get; private set; }

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        if (_kind == BomImportKind.Materials)
        {
            HintText.Text =
                "Ayraç: noktalı virgül (;) veya virgül (,). «CSV indir» şablonu: Kategori;Kod;Aciklama;Miktar;Birim;Not. " +
                "Daha az sütun: Kod, Açıklama, Miktar veya Kod, Açıklama, Miktar, Birim.";
            MaterialPreviewGrid.Visibility = Visibility.Visible;
            MaterialPreviewGrid.ItemsSource = _materialPreview;
        }
        else
        {
            HintText.Text =
                "Ayraç: ; veya ,. «CSV indir» şablonu: Poz;Cap_mm;Adet;H_mm;L_mm;Not. " +
                "Eski başlıklar da olur: 2. sütun çap (mm), 4–5 H/L boyları. Çap örn. 12, Ø12.";
            RebarPreviewGrid.Visibility = Visibility.Visible;
            RebarPreviewGrid.ItemsSource = _rebarPreview;
        }

        RefreshPreview();
    }

    private void RefreshPreview()
    {
        OkImportButton.IsEnabled = true;
        var skip = SkipHeaderCheck.IsChecked == true;
        var seq = skip && _rawRows.Count > 0 ? _rawRows.Skip(1) : _rawRows;

        try
        {
            if (_kind == BomImportKind.Materials)
            {
                _materialPreview.Clear();
                foreach (var m in BomImportMapper.MapMaterials(seq))
                    _materialPreview.Add(m);
                SummaryText.Text = $"{_materialPreview.Count} malzeme satırı içe aktarılacak.";
            }
            else
            {
                _rebarPreview.Clear();
                foreach (var r in BomImportMapper.MapRebars(seq))
                    _rebarPreview.Add(r);
                SummaryText.Text = $"{_rebarPreview.Count} donatı satırı içe aktarılacak.";
            }
        }
        catch (Exception ex)
        {
            SummaryText.Text = "Önizleme hatası: " + ex.Message;
            if (_kind == BomImportKind.Materials)
                _materialPreview.Clear();
            else
                _rebarPreview.Clear();
            OkImportButton.IsEnabled = false;
        }
    }

    private void OnSkipHeaderChanged(object sender, RoutedEventArgs e) => RefreshPreview();

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_kind == BomImportKind.Materials)
                AcceptedMaterials = _materialPreview.ToList();
            else
                AcceptedRebars = _rebarPreview.ToList();

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            SummaryText.Text = "Listeye eklenemedi: " + ex.Message;
        }
    }
}
