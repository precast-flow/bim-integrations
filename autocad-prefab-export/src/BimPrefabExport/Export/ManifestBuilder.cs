using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using BimPrefabExport.Core;
using BimPrefabExport.Services;

namespace BimPrefabExport.Export;

public static class ManifestBuilder
{
    public static void WriteManifestJson(
        string outputPath,
        DocumentSummary drawing,
        IReadOnlyList<ProductRecord> products,
        IReadOnlyList<ExportDrawingEntry>? drawings = null)
    {
        var drawingByProduct = (drawings ?? Array.Empty<ExportDrawingEntry>())
            .GroupBy(d => d.ProductId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var dto = new ManifestDto
        {
            SchemaVersion = PrefabConstants.ManifestSchemaVersion,
            ExportedAtUtc = DateTime.UtcNow,
            Drawing = drawing,
            Products = new List<ManifestProductDto>(),
            SharedDetails = new List<ManifestSharedDetailDto>(),
        };

        var cat = AttributeCatalogService.Default;
        foreach (var p in products)
        {
            var contentHash = p.ComputeContentHash();
            p.LastExportedContentHash = contentHash;
            p.LastExportUtc = DateTime.UtcNow;

            var elementTypeId = p.PrefabElementTypeId?.Trim() ?? "";
            var typologyId = p.PrefabTypologyId?.Trim() ?? "";
            var categoryId = p.ElementCategoryId?.Trim()
                             ?? cat.GetCategoryIdForElementType(elementTypeId)
                             ?? "";

            var dimensions = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in p.Attributes)
            {
                if (double.TryParse(kv.Value?.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var n))
                    dimensions[kv.Key] = n;
            }

            var manifestProduct = new ManifestProductDto
            {
                ProductId = p.ProductId,
                Revision = p.Revision,
                ContentHash = contentHash,
                DisplayName = p.DisplayName,
                Code = p.Code,
                Quantity = p.Quantity,
                Unit = p.Unit,
                ElementTypeId = elementTypeId,
                TypologyId = typologyId,
                ElementCategoryId = categoryId,
                LifecycleStatus = "tasarim",
                Note = p.Note ?? "",
                Attributes = new Dictionary<string, string>(p.Attributes, StringComparer.OrdinalIgnoreCase),
                Dimensions = dimensions,
                Materials = MapMaterials(p),
                RebarSchedule = MapRebars(p),
                Documents = new List<ManifestDocumentDto>(),
            };

            if (drawingByProduct.TryGetValue(p.ProductId, out var docs))
            {
                foreach (var d in docs)
                {
                    manifestProduct.Documents.Add(new ManifestDocumentDto
                    {
                        Type = "pdf",
                        Path = d.RelativePath,
                        Role = "drawing",
                        ProductCode = d.ProductCode,
                        Title = d.PdfTitle,
                        Revision = d.Revision,
                        FileName = d.FileName,
                    });
                }
            }

            dto.Products.Add(manifestProduct);
        }

        var json = JsonSerializer.Serialize(dto, JsonOptions);
        File.WriteAllText(outputPath, json);
    }

    private static List<ManifestMaterialDto> MapMaterials(ProductRecord p)
    {
        var list = new List<ManifestMaterialDto>();
        foreach (var m in p.Materials ?? [])
        {
            double? volumeM3 = null;
            var unit = m.Unit?.Trim() ?? "";
            if (unit.Equals("m3", StringComparison.OrdinalIgnoreCase)
                || unit.Equals("m³", StringComparison.OrdinalIgnoreCase))
                volumeM3 = m.Quantity;

            list.Add(new ManifestMaterialDto
            {
                Category = m.Category,
                Code = m.Code,
                Name = string.IsNullOrWhiteSpace(m.Description) ? m.Code : m.Description,
                Quantity = m.Quantity,
                Unit = m.Unit,
                Notes = m.Notes,
                MaterialCatalogCode = m.MaterialCatalogCode,
                VolumeM3 = volumeM3,
            });
        }

        return list;
    }

    private static List<ManifestRebarDto> MapRebars(ProductRecord p)
    {
        var list = new List<ManifestRebarDto>();
        var poz = 0;
        foreach (var r in p.Rebars ?? [])
        {
            poz++;
            RebarWeightHelper.NormalizeRebarRow(r, poz);
            list.Add(new ManifestRebarDto
            {
                Position = r.PozNo,
                DiameterMm = r.DiameterMm ?? 0,
                SteelGrade = r.SteelGrade,
                Shape = r.Shape,
                DevelopedLengthMm = r.DevelopedLengthMm ?? RebarWeightHelper.ComputeDevelopedLengthMm(r),
                Count = (int)Math.Max(1, Math.Round(r.Count)),
                TotalWeightKg = r.TotalWeightKg ?? RebarWeightHelper.ComputeRowWeightKg(r),
                Notes = r.Notes,
            });
        }

        return list;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}

internal sealed class ManifestDto
{
    public int SchemaVersion { get; set; }
    public DateTime ExportedAtUtc { get; set; }
    public DocumentSummary Drawing { get; set; } = new();
    public List<ManifestProductDto> Products { get; set; } = new();
    public List<ManifestSharedDetailDto> SharedDetails { get; set; } = new();
}

internal sealed class ManifestProductDto
{
    public Guid ProductId { get; set; }
    public int Revision { get; set; }
    public string ContentHash { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Code { get; set; } = "";
    public double Quantity { get; set; }
    public string Unit { get; set; } = "";
    public string ElementTypeId { get; set; } = "";
    public string TypologyId { get; set; } = "";
    public string ElementCategoryId { get; set; } = "";
    public string LifecycleStatus { get; set; } = "";
    public string Note { get; set; } = "";
    public Dictionary<string, string> Attributes { get; set; } = new();
    public Dictionary<string, double> Dimensions { get; set; } = new();
    public List<ManifestMaterialDto> Materials { get; set; } = new();
    public List<ManifestRebarDto> RebarSchedule { get; set; } = new();
    public List<ManifestDocumentDto> Documents { get; set; } = new();
}

internal sealed class ManifestMaterialDto
{
    public string Category { get; set; } = "";
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public double Quantity { get; set; }
    public string Unit { get; set; } = "";
    public string Notes { get; set; } = "";
    public string MaterialCatalogCode { get; set; } = "";
    public double? VolumeM3 { get; set; }
}

internal sealed class ManifestRebarDto
{
    public string Position { get; set; } = "";
    public double DiameterMm { get; set; }
    public string SteelGrade { get; set; } = "";
    public string Shape { get; set; } = "";
    public double DevelopedLengthMm { get; set; }
    public int Count { get; set; }
    public double TotalWeightKg { get; set; }
    public string Notes { get; set; } = "";
}

internal sealed class ManifestDocumentDto
{
    public string Type { get; set; } = "";
    public string Path { get; set; } = "";
    public string Role { get; set; } = "";
    public string ProductCode { get; set; } = "";
    public string Title { get; set; } = "";
    public int Revision { get; set; }
    public string FileName { get; set; } = "";
}

internal sealed class ManifestSharedDetailDto
{
    public Guid SharedDetailId { get; set; }
    public string Path { get; set; } = "";
    public List<Guid> ReferencedByProductIds { get; set; } = new();
}
