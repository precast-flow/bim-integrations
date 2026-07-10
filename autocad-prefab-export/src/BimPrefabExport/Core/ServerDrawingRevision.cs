using System.Text.Json.Serialization;

namespace BimPrefabExport.Core;

/// <summary>PrecastFlow sunucusunda saklanan çizim revizyonu (dimensionsJson.drawingRevisions).</summary>
public sealed class ServerDrawingRevision
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("revision")]
    public string Revision { get; set; } = "";

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("updatedAt")]
    public string UpdatedAt { get; set; } = "";

    [JsonPropertyName("updatedBy")]
    public string UpdatedBy { get; set; } = "";

    [JsonPropertyName("changeNote")]
    public string ChangeNote { get; set; } = "";

    [JsonPropertyName("pdfUrl")]
    public string? PdfUrl { get; set; }

    [JsonPropertyName("fileName")]
    public string? FileName { get; set; }

    [JsonPropertyName("fileId")]
    public string? FileId { get; set; }
}
