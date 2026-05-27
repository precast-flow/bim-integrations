using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BimPrefabExport.Core;

public sealed class ProductRecord
{
    [JsonPropertyName("productId")]
    public Guid ProductId { get; set; }

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = "";

    [JsonPropertyName("code")]
    public string Code { get; set; } = "";

    [JsonPropertyName("quantity")]
    public double Quantity { get; set; }

    [JsonPropertyName("unit")]
    public string Unit { get; set; } = "adet";

    [JsonPropertyName("revision")]
    public int Revision { get; set; }

    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; } = PrefabConstants.ManifestSchemaVersion;

    [JsonPropertyName("attributes")]
    public Dictionary<string, string> Attributes { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("detailRefs")]
    public List<DetailRef> DetailRefs { get; set; } = new();

    [JsonPropertyName("materials")]
    public List<MaterialLine> Materials { get; set; } = new();

    [JsonPropertyName("rebars")]
    public List<RebarLine> Rebars { get; set; } = new();

    /// <summary>Eski tek çit (okuma uyumu); yeni kayıtlar <see cref="LinkFences"/> kullanır.</summary>
    [JsonPropertyName("linkFenceMinX")]
    public double? LinkFenceMinX { get; set; }

    [JsonPropertyName("linkFenceMinY")]
    public double? LinkFenceMinY { get; set; }

    [JsonPropertyName("linkFenceMaxX")]
    public double? LinkFenceMaxX { get; set; }

    [JsonPropertyName("linkFenceMaxY")]
    public double? LinkFenceMaxY { get; set; }

    /// <summary>Polyline sınır atamaları (birden fazla). PDF penceresi birleşik sınır olarak kullanılır.</summary>
    [JsonPropertyName("linkFences")]
    public List<LinkFenceBox> LinkFences { get; set; } = new();

    /// <summary>Her geçerli çit için PDF kağıdı / yön / dosya adı parçası ve son PDF üretim zamanı.</summary>
    [JsonPropertyName("pdfDrawings")]
    public List<ProductPdfDrawing> PdfDrawings { get; set; } = new();

    [JsonPropertyName("lastExportedContentHash")]
    public string? LastExportedContentHash { get; set; }

    [JsonPropertyName("lastExportUtc")]
    public DateTime? LastExportUtc { get; set; }

    /// <summary>PDF çıktı kağıdı (ISO A serisi: A3, A4, …). Plot: sığdır + ortala; PDF plotter ortamı buna göre seçilir.</summary>
    [JsonPropertyName("plotPaperSize")]
    public string PlotPaperSize { get; set; } = PlotPaperSizes.Default;

    /// <summary>Kağıt / önizleme yönü: false = dikey (portrait), true = yatay (landscape).</summary>
    [JsonPropertyName("plotLandscape")]
    public bool PlotLandscape { get; set; }

    /// <summary>Yazdır diyaloğundan kaydedilen CTB/STB adı (opsiyonel).</summary>
    [JsonPropertyName("plotStyleSheet")]
    public string? PlotStyleSheet { get; set; }

    /// <summary>Eski kayıtlar: COLUMN, WALL, OTHER, … (categories.json v1). Yeni kayıtta genelde boş.</summary>
    [JsonPropertyName("productCategoryId")]
    public string? ProductCategoryId { get; set; }

    /// <summary>Eleman tipi (kolon, kiriş, …) — <c>categories.json</c> <c>elementTypes</c>.</summary>
    [JsonPropertyName("prefabElementTypeId")]
    public string? PrefabElementTypeId { get; set; }

    /// <summary>Seçili tipoloji (ör. beam-rect); öznitelikler buna göre <c>identifyingDimensions</c>.</summary>
    [JsonPropertyName("prefabTypologyId")]
    public string? PrefabTypologyId { get; set; }

    public static string Serialize(ProductRecord record)
    {
        return JsonSerializer.Serialize(record, JsonOptions);
    }

    public static ProductRecord? Deserialize(string json)
    {
        return JsonSerializer.Deserialize<ProductRecord>(json, JsonOptions);
    }

    public static ProductRecord DeepClone(ProductRecord source)
    {
        return Deserialize(Serialize(source)) ?? throw new InvalidOperationException("Clone failed.");
    }

    /// <summary>Eski tek alan kaydını listeye taşır (bellekte; kayıtta <see cref="LinkFences"/> kalır).</summary>
    public void NormalizeLinkFencesFromLegacy()
    {
        if (LinkFences.Count > 0)
            return;
        if (!LinkFenceMinX.HasValue || !LinkFenceMinY.HasValue || !LinkFenceMaxX.HasValue || !LinkFenceMaxY.HasValue)
            return;
        if (LinkFenceMaxX.Value <= LinkFenceMinX.Value || LinkFenceMaxY.Value <= LinkFenceMinY.Value)
            return;

        LinkFences.Add(new LinkFenceBox
        {
            FenceId = Guid.NewGuid(),
            MinX = LinkFenceMinX.Value,
            MinY = LinkFenceMinY.Value,
            MaxX = LinkFenceMaxX.Value,
            MaxY = LinkFenceMaxY.Value,
        });
        LinkFenceMinX = LinkFenceMinY = LinkFenceMaxX = LinkFenceMaxY = null;
    }

    public int GetDrawingReferenceCount()
    {
        var n = LinkFences.Count(b => b.IsValid());
        if (n > 0)
            return n;
        if (!LinkFenceMinX.HasValue || !LinkFenceMinY.HasValue || !LinkFenceMaxX.HasValue || !LinkFenceMaxY.HasValue)
            return 0;
        return LinkFenceMaxX.Value > LinkFenceMinX.Value && LinkFenceMaxY.Value > LinkFenceMinY.Value ? 1 : 0;
    }

    public string ComputeContentHash()
    {
        NormalizeLinkFencesFromLegacy();
        var normalized = new
        {
            ProductId,
            DisplayName,
            Code,
            Quantity,
            Unit,
            Revision,
            SchemaVersion,
            ProductCategoryId = string.IsNullOrWhiteSpace(ProductCategoryId)
                ? null
                : ProductCategoryId.Trim(),
            PrefabElementTypeId = string.IsNullOrWhiteSpace(PrefabElementTypeId)
                ? null
                : PrefabElementTypeId.Trim(),
            PrefabTypologyId = string.IsNullOrWhiteSpace(PrefabTypologyId) ? null : PrefabTypologyId.Trim(),
            Attributes = Attributes.OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .ToDictionary(kv => kv.Key, kv => kv.Value),
            DetailRefs = DetailRefs
                .OrderBy(d => d.Kind, StringComparer.Ordinal)
                .ThenBy(d => d.LayoutName ?? "", StringComparer.Ordinal)
                .ThenBy(d => d.ViewName ?? "", StringComparer.Ordinal)
                .Select(d => new { d.Kind, d.LayoutName, d.ViewName, d.SheetId })
                .ToList(),
            Materials = (Materials ?? [])
                .OrderBy(m => m.Category, StringComparer.Ordinal)
                .ThenBy(m => m.Code, StringComparer.Ordinal)
                .Select(m => new { m.Category, m.Code, m.Description, m.Quantity, m.Unit, m.Notes })
                .ToList(),
            Rebars = (Rebars ?? [])
                .OrderBy(r => r.PozNo, StringComparer.OrdinalIgnoreCase)
                .ThenBy(r => r.DiameterMm)
                .Select(r => new
                {
                    r.PozNo,
                    r.DiameterMm,
                    r.Count,
                    r.LengthH_mm,
                    r.LengthL_mm,
                    r.Notes,
                })
                .ToList(),
            LinkFences = LinkFences
                .Where(b => b.IsValid())
                .OrderBy(b => b.MinX)
                .ThenBy(b => b.MinY)
                .Select(b => new { b.FenceId, b.MinX, b.MinY, b.MaxX, b.MaxY })
                .ToList(),
            PdfDrawings = (PdfDrawings ?? [])
                .OrderBy(d => d.FenceId)
                .Select(d => new
                {
                    d.FenceId,
                    PdfTitle = string.IsNullOrWhiteSpace(d.PdfTitle) ? null : d.PdfTitle.Trim(),
                    d.PdfRevision,
                    PlotPaperSize = PlotPaperSizes.NormalizeOrDefault(d.PlotPaperSize),
                    d.PlotLandscape,
                    UploadedPdfRelativePath = string.IsNullOrWhiteSpace(d.UploadedPdfRelativePath)
                        ? null
                        : d.UploadedPdfRelativePath.Trim().Replace('\\', '/'),
                })
                .ToList(),
            PlotPaperSize = PlotPaperSizes.NormalizeOrDefault(PlotPaperSize),
            PlotLandscape,
            PlotStyleSheet = string.IsNullOrWhiteSpace(PlotStyleSheet) ? null : PlotStyleSheet.Trim(),
        };

        var json = JsonSerializer.Serialize(normalized, JsonOptions);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}

public sealed class DetailRef
{
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = "detail";

    [JsonPropertyName("layoutName")]
    public string? LayoutName { get; set; }

    [JsonPropertyName("viewName")]
    public string? ViewName { get; set; }

    [JsonPropertyName("sheetId")]
    public string? SheetId { get; set; }
}
