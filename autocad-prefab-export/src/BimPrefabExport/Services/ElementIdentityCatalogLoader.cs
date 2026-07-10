using BimPrefabExport.Schema;

namespace BimPrefabExport.Services;

/// <summary>OData element identity katalogunu <see cref="PrefabCatalogRoot"/> formatına çevirir.</summary>
public static class ElementIdentityCatalogLoader
{
    public static async Task<bool> TryLoadFromApiAsync(PrecastApiClient client, CancellationToken cancellationToken = default)
    {
        try
        {
            var bundle = await client.FetchElementIdentityCatalogAsync(cancellationToken).ConfigureAwait(false);
            var root = MapToPrefabCatalogRoot(bundle);
            AttributeCatalogService.InstallRemoteCatalog(root);
            try
            {
                var firmSettings = await client.FetchFirmTypologySettingsAsync(cancellationToken).ConfigureAwait(false);
                FirmTypologySettingsCache.Install(firmSettings);
                BimPrefabLog.Info($"Firma tipoloji ayarları yüklendi: {firmSettings.Count} kayıt.");
            }
            catch (Exception ex)
            {
                FirmTypologySettingsCache.Clear();
                BimPrefabLog.Info($"Firma tipoloji ayarları yüklenemedi: {ex.Message}");
            }
            BimPrefabLog.Info(
                $"Katalog sunucudan yüklendi: {root.Categories.Count} kategori, {root.ElementTypes.Count} tip, {root.Typologies.Count} tipoloji.");
            return true;
        }
        catch (Exception ex)
        {
            BimPrefabLog.Info($"Sunucu katalog yüklenemedi: {ex.Message}");
            AttributeCatalogService.ClearRemoteCatalog();
            return false;
        }
    }

    private static PrefabCatalogRoot MapToPrefabCatalogRoot(ElementIdentityCatalogBundle bundle)
    {
        var root = new PrefabCatalogRoot();

        foreach (var c in bundle.Categories.OrderBy(x => x.SortOrder))
        {
            root.Categories.Add(new CategoryDefinition
            {
                Id = c.Code,
                DisplayName = c.NameTr,
                DisplayNameEn = c.NameEn,
            });
        }

        foreach (var et in bundle.ElementTypes.OrderBy(x => x.SortOrder))
        {
            root.ElementTypes.Add(new ElementTypeDefinition
            {
                Id = et.Code,
                CategoryId = et.Category,
                DisplayName = et.NameTr,
                DisplayNameEn = et.NameEn,
            });
        }

        foreach (var t in bundle.Typologies.OrderBy(x => x.Code, StringComparer.OrdinalIgnoreCase))
        {
            root.Typologies.Add(new TypologyCatalogDefinition
            {
                Id = t.Code,
                ElementTypeId = t.ElementTypeCode,
                DisplayName = t.NameTr,
                DisplayNameEn = t.NameEn,
                IdentifyingDimensions = t.DimensionCodes.ToList(),
                ShowInUserFilter = t.ShowInUserFilter,
            });
        }

        foreach (var d in bundle.IdentifyingDimensions.OrderBy(x => x.Code, StringComparer.OrdinalIgnoreCase))
        {
            root.DimensionFields[d.Code] = new AttributeFieldDefinition
            {
                Tag = d.Code,
                Label = string.IsNullOrWhiteSpace(d.NameTr) ? d.Code : d.NameTr,
                Type = MapDimensionFieldType(d.UnitCategoryCode, d.Unit),
                UnitCategoryCode = d.UnitCategoryCode,
                Unit = d.Unit,
            };
        }

        return root;
    }

    private static string MapDimensionFieldType(string? unitCategoryCode, string? unit)
    {
        if (string.Equals(unitCategoryCode, "count", StringComparison.OrdinalIgnoreCase)
            || string.Equals(unitCategoryCode, "angle", StringComparison.OrdinalIgnoreCase))
            return "number";

        if (string.IsNullOrWhiteSpace(unit))
            return "number";

        var cat = unitCategoryCode?.Trim().ToLowerInvariant();
        if (cat is "length" or "weight" or "area" or "volume" or "time" or "pressure" or "force")
            return "number";

        var u = unit.Trim().ToLowerInvariant();
        return u is "mm" or "m" or "m2" or "m3" or "m²" or "m³" or "ad" or "ea" or "count" or "adet" or "deg" or "°"
            ? "number"
            : "string";
    }
}

public sealed class ElementIdentityCatalogBundle
{
    public List<CatalogCategoryDto> Categories { get; set; } = [];
    public List<CatalogElementTypeDto> ElementTypes { get; set; } = [];
    public List<CatalogTypologyDto> Typologies { get; set; } = [];
    public List<CatalogIdentifyingDimensionDto> IdentifyingDimensions { get; set; } = [];
}

public sealed class CatalogCategoryDto
{
    public string Code { get; set; } = "";
    public string NameTr { get; set; } = "";
    public string? NameEn { get; set; }
    public int SortOrder { get; set; }
}

public sealed class CatalogElementTypeDto
{
    public string Code { get; set; } = "";
    public string Category { get; set; } = "";
    public string NameTr { get; set; } = "";
    public string? NameEn { get; set; }
    public int SortOrder { get; set; }
}

public sealed class CatalogTypologyDto
{
    public string Code { get; set; } = "";
    public string ElementTypeCode { get; set; } = "";
    public string NameTr { get; set; } = "";
    public string? NameEn { get; set; }
    public bool ShowInUserFilter { get; set; } = true;
    public IReadOnlyList<string> DimensionCodes { get; set; } = Array.Empty<string>();
}

public sealed class CatalogIdentifyingDimensionDto
{
    public string Code { get; set; } = "";
    public string NameTr { get; set; } = "";
    public string? NameEn { get; set; }
    public string UnitCategoryCode { get; set; } = "length";
    public string Unit { get; set; } = "mm";
}
