namespace BimPrefabExport.Core;

/// <summary>Export paketindeki PDF dosyası — manifest ve cizimler.csv için.</summary>
public sealed class ExportDrawingEntry
{
    public Guid ProductId { get; set; }
    public string ProductCode { get; set; } = "";
    public string FileName { get; set; } = "";
    public string PdfTitle { get; set; } = "";
    public int Revision { get; set; }
    /// <summary>Bundle köküne göre yol, örn. <c>PDF/K-01_plan_r1.pdf</c>.</summary>
    public string RelativePath { get; set; } = "";
}
