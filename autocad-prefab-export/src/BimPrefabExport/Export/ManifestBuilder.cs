using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using BimPrefabExport.Core;

namespace BimPrefabExport.Export;

public static class ManifestBuilder
{
    public static void WriteManifestJson(
        string outputPath,
        DocumentSummary drawing,
        IReadOnlyList<ProductRecord> products)
    {
        var dto = new ManifestDto
        {
            SchemaVersion = PrefabConstants.ManifestSchemaVersion,
            ExportedAtUtc = DateTime.UtcNow,
            Drawing = drawing,
            Products = new List<ManifestProductDto>(),
            SharedDetails = new List<ManifestSharedDetailDto>(),
        };

        foreach (var p in products)
        {
            dto.Products.Add(new ManifestProductDto
            {
                ProductId = p.ProductId,
                Revision = p.Revision,
                ContentHash = p.ComputeContentHash(),
                DisplayName = p.DisplayName,
                Code = p.Code,
                Quantity = p.Quantity,
                Unit = p.Unit,
                Attributes = new Dictionary<string, string>(p.Attributes, StringComparer.OrdinalIgnoreCase),
                Documents = new List<ManifestDocumentDto>(),
            });
        }

        var json = JsonSerializer.Serialize(dto, JsonOptions);
        File.WriteAllText(outputPath, json);
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
    public Dictionary<string, string> Attributes { get; set; } = new();
    public List<ManifestDocumentDto> Documents { get; set; } = new();
}

internal sealed class ManifestDocumentDto
{
    public string Type { get; set; } = "";
    public string Path { get; set; } = "";
    public string Role { get; set; } = "";
}

internal sealed class ManifestSharedDetailDto
{
    public Guid SharedDetailId { get; set; }
    public string Path { get; set; } = "";
    public List<Guid> ReferencedByProductIds { get; set; } = new();
}
