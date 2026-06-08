using System.Text.Json.Serialization;

namespace BimPrefabExport.Core;

/// <summary>Donatı satırı (poz, çap, adet, boylar). CSV <c>*_donatilar.csv</c> ve Excel içe aktarma.</summary>
public sealed class RebarLine
{
    [JsonPropertyName("pozNo")]
    public string PozNo { get; set; } = "";

    /// <summary>Çap (mm), örn. 12.</summary>
    [JsonPropertyName("diameterMm")]
    public double? DiameterMm { get; set; }

    [JsonPropertyName("count")]
    public double Count { get; set; }

    [JsonPropertyName("lengthH_mm")]
    public double? LengthH_mm { get; set; }

    [JsonPropertyName("lengthL_mm")]
    public double? LengthL_mm { get; set; }

    [JsonPropertyName("notes")]
    public string Notes { get; set; } = "";

    [JsonPropertyName("steelGrade")]
    public string SteelGrade { get; set; } = "B500C";

    [JsonPropertyName("shape")]
    public string Shape { get; set; } = "straight";

    [JsonPropertyName("developedLengthMm")]
    public double? DevelopedLengthMm { get; set; }

    [JsonPropertyName("totalWeightKg")]
    public double? TotalWeightKg { get; set; }
}
