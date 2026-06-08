using System.Text.Json.Serialization;

namespace BimPrefabExport.Core;

/// <summary>Ürün BOM / malzeme satırı (CSV *_materyaller).</summary>
public sealed class MaterialLine
{
    [JsonPropertyName("category")]
    public string Category { get; set; } = "";

    [JsonPropertyName("code")]
    public string Code { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("quantity")]
    public double Quantity { get; set; }

    [JsonPropertyName("unit")]
    public string Unit { get; set; } = "";

    [JsonPropertyName("notes")]
    public string Notes { get; set; } = "";

    /// <summary>Malzeme kataloğu kodu (PrecastFlow eşlemesi).</summary>
    [JsonPropertyName("materialCatalogCode")]
    public string MaterialCatalogCode { get; set; } = "";
}
