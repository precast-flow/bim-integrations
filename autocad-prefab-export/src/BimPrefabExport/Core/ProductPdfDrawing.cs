using System.Text.Json.Serialization;

namespace BimPrefabExport.Core;

/// <summary>Ürün başına bir polyline çit alanı için PDF çıktı ayarları ve son dışa aktarma zamanı.</summary>
public sealed class ProductPdfDrawing
{
    [JsonPropertyName("fenceId")]
    public Guid FenceId { get; set; }

    /// <summary>Dosya adı parçası: çıktı her zaman <c>kod_{pdfTitle}.pdf</c> biçiminde.</summary>
    [JsonPropertyName("pdfTitle")]
    public string PdfTitle { get; set; } = "cizim";

    [JsonPropertyName("plotPaperSize")]
    public string PlotPaperSize { get; set; } = PlotPaperSizes.Default;

    [JsonPropertyName("plotLandscape")]
    public bool PlotLandscape { get; set; }

    [JsonPropertyName("lastPdfExportUtc")]
    public DateTime? LastPdfExportUtc { get; set; }

    /// <summary>PDF çizim revizyonu; dosya adında <c>_r{n}</c> olarak kullanılır (n &gt; 0).</summary>
    [JsonPropertyName("pdfRevision")]
    public int PdfRevision { get; set; }

    /// <summary>DWG dizinine göreli yol; doluysa dışa aktarmada plot yerine bu dosya kopyalanır.</summary>
    [JsonPropertyName("uploadedPdfRelativePath")]
    public string? UploadedPdfRelativePath { get; set; }
}
