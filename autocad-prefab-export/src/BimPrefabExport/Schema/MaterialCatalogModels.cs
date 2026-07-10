using System.Text.Json.Serialization;

namespace BimPrefabExport.Schema;

public sealed class MaterialCatalogRoot
{
    [JsonPropertyName("materials")]
    public List<MaterialCatalogEntry> Materials { get; set; } = new();

    [JsonPropertyName("steelGrades")]
    public List<string> SteelGrades { get; set; } = new();

    [JsonPropertyName("rebarShapes")]
    public List<RebarShapeOption> RebarShapes { get; set; } = new();
}

public sealed class MaterialCatalogEntry
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("code")]
    public string Code { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("category")]
    public string Category { get; set; } = "";

    [JsonPropertyName("categoryLabel")]
    public string CategoryLabel { get; set; } = "";

    [JsonPropertyName("defaultUnit")]
    public string DefaultUnit { get; set; } = "";

    [JsonPropertyName("concreteRecipeId")]
    public string? ConcreteRecipeId { get; set; }

    [JsonPropertyName("specification")]
    public string? Specification { get; set; }

    [JsonPropertyName("unitWeightKgPerM3")]
    public double? UnitWeightKgPerM3 { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

public sealed class RebarShapeOption
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("label")]
    public string Label { get; set; } = "";
}
