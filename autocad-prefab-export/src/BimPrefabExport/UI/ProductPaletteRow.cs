using System.ComponentModel;
using System.Runtime.CompilerServices;
using BimPrefabExport.Core;
using BimPrefabExport.Services;

namespace BimPrefabExport.UI;

/// <summary>Palet ürün satırı: çizim kaydı + dışa aktarma için işaret kutusu.</summary>
public sealed class ProductPaletteRow : INotifyPropertyChanged
{
    public ProductPaletteRow(ProductRecord record)
    {
        Record = record;
    }

    public ProductRecord Record { get; }

    public string DisplayText =>
        $"{Record.DisplayName}  |  {Record.Code}  |  rev {Record.Revision}";

    /// <summary>Tablo sütunu: çizim (polyline) referans sayısı.</summary>
    public string DrawingReferenceCaption
    {
        get
        {
            var n = Record.GetDrawingReferenceCount();
            if (n <= 0)
                return "—";
            var pd = Record.PdfDrawings?.Count ?? 0;
            return n == 1
                ? $"1 çit · {pd} PDF çizimi"
                : $"{n} çit · {pd} PDF çizimi";
        }
    }

    /// <summary>PDF sütunu için kısa metin (Unicode simge).</summary>
    public string PdfDrawingSummary => "📄  " + DrawingReferenceCaption;

    /// <summary>Tipoloji özeti: eleman tipi › tipoloji.</summary>
    public string CategorySummary =>
        AttributeCatalogService.Default.GetTypologyLineSummary(
            Record.PrefabElementTypeId,
            Record.PrefabTypologyId);

    private bool _isChecked;

    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            if (_isChecked == value)
                return;
            _isChecked = value;
            OnPropertyChanged();
            CheckedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? CheckedChanged;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
