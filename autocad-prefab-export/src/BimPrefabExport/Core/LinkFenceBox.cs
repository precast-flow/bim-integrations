using System.Text.Json.Serialization;

namespace BimPrefabExport.Core;

/// <summary>Polyline sınırı ile kaydedilen tek bir WCS dikdörtgen çit (PDF penceresi birleşimi için).</summary>
public sealed class LinkFenceBox
{
    /// <summary>Çit kimliği; <see cref="ProductPdfDrawing.FenceId"/> ile eşleşir. Eski kayıtta boş olabilir (yüklenince atanır).</summary>
    [JsonPropertyName("fenceId")]
    public Guid FenceId { get; set; }

    [JsonPropertyName("minX")]
    public double MinX { get; set; }

    [JsonPropertyName("minY")]
    public double MinY { get; set; }

    [JsonPropertyName("maxX")]
    public double MaxX { get; set; }

    [JsonPropertyName("maxY")]
    public double MaxY { get; set; }

    public bool IsValid() =>
        MaxX > MinX && MaxY > MinY;
}
