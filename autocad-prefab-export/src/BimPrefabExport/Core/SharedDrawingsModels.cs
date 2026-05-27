using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BimPrefabExport.Core;

/// <summary>Çizime özel ortak çizim (genel plan, detaylar vb.); ürünlerden bağımsız.</summary>
public sealed class SharedDrawingEntry
{
    [JsonPropertyName("drawingId")]
    public Guid DrawingId { get; set; }

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = "";

    /// <summary>GENEL veya DETAY (serbest metin; önerilen değerler).</summary>
    [JsonPropertyName("category")]
    public string Category { get; set; } = "GENEL";

    [JsonPropertyName("linkFences")]
    public List<LinkFenceBox> LinkFences { get; set; } = new();

    [JsonPropertyName("plotPaperSize")]
    public string PlotPaperSize { get; set; } = PlotPaperSizes.Default;

    [JsonPropertyName("plotLandscape")]
    public bool PlotLandscape { get; set; }

    [JsonPropertyName("createdUtc")]
    public DateTime? CreatedUtc { get; set; }

    [JsonPropertyName("modifiedUtc")]
    public DateTime? ModifiedUtc { get; set; }
}

public sealed class SharedDrawingsDocument
{
    [JsonPropertyName("drawings")]
    public List<SharedDrawingEntry> Drawings { get; set; } = new();

    public static string Serialize(SharedDrawingsDocument doc) =>
        JsonSerializer.Serialize(doc, JsonOptions);

    public static SharedDrawingsDocument Deserialize(string json)
    {
        var d = JsonSerializer.Deserialize<SharedDrawingsDocument>(json, JsonOptions);
        return d ?? new SharedDrawingsDocument();
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}
