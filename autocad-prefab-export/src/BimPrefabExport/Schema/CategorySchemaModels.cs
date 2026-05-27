using System.Text.Json.Serialization;



namespace BimPrefabExport.Schema;



/// <summary><c>categories.json</c> — bina eleman tipi (kolon, kiriş, …), tipolojiler ve boyut alanları.</summary>

public sealed class PrefabCatalogRoot

{

    [JsonPropertyName("elementTypes")]

    public List<ElementTypeDefinition> ElementTypes { get; set; } = new();



    [JsonPropertyName("typologies")]

    public List<TypologyCatalogDefinition> Typologies { get; set; } = new();



    [JsonPropertyName("dimensionFields")]

    public Dictionary<string, AttributeFieldDefinition> DimensionFields { get; set; } = new();

}



public sealed class ElementTypeDefinition

{

    [JsonPropertyName("id")]

    public string Id { get; set; } = "";



    [JsonPropertyName("displayName")]

    public string DisplayName { get; set; } = "";



    [JsonPropertyName("displayNameEn")]

    public string? DisplayNameEn { get; set; }

}



public sealed class TypologyCatalogDefinition

{

    [JsonPropertyName("id")]

    public string Id { get; set; } = "";



    [JsonPropertyName("displayName")]

    public string DisplayName { get; set; } = "";



    [JsonPropertyName("displayNameEn")]

    public string? DisplayNameEn { get; set; }



    [JsonPropertyName("elementTypeId")]

    public string? ElementTypeId { get; set; }



    [JsonPropertyName("identifyingDimensions")]

    public List<string> IdentifyingDimensions { get; set; } = new();

}



public sealed class AttributeFieldDefinition

{

    [JsonPropertyName("tag")]

    public string Tag { get; set; } = "";



    [JsonPropertyName("label")]

    public string Label { get; set; } = "";



    [JsonPropertyName("type")]

    public string Type { get; set; } = "string";

}


