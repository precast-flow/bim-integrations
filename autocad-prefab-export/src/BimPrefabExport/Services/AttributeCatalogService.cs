using System.Reflection;
using System.Text.Json;
using BimPrefabExport.Core;
using BimPrefabExport.Schema;

namespace BimPrefabExport.Services;

/// <summary>Gömülü <c>categories.json</c> — eleman tipleri, tipolojiler ve boyut alanları.</summary>
internal sealed class AttributeCatalogService
{
    private static readonly Lazy<AttributeCatalogService> Lazy = new(Load);

    private readonly PrefabCatalogRoot _root;
    private readonly Dictionary<string, TypologyCatalogDefinition> _typologiesById;
    private readonly Dictionary<string, ElementTypeDefinition> _elementTypesById;

    private AttributeCatalogService(PrefabCatalogRoot root)
    {
        _root = root;
        _typologiesById = new Dictionary<string, TypologyCatalogDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in root.Typologies)
        {
            if (string.IsNullOrWhiteSpace(t.Id))
                continue;
            _typologiesById[t.Id.Trim()] = t;
        }

        _elementTypesById = new Dictionary<string, ElementTypeDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var et in root.ElementTypes)
        {
            if (string.IsNullOrWhiteSpace(et.Id))
                continue;
            _elementTypesById[et.Id.Trim()] = et;
        }
    }

    public static AttributeCatalogService Default => Lazy.Value;

    public IReadOnlyList<ElementTypeDefinition> ElementTypes => _root.ElementTypes;

    public IReadOnlyList<CategoryDefinition> Categories => _root.Categories;

    public IReadOnlyList<ElementTypeDefinition> GetElementTypesForCategory(string? categoryId)
    {
        if (string.IsNullOrWhiteSpace(categoryId))
            return ElementTypes;
        var cid = categoryId.Trim();
        return ElementTypes
            .Where(et => string.Equals(GetCategoryIdForElementType(et.Id), cid, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public string? GetCategoryIdForElementType(string? elementTypeId)
    {
        if (string.IsNullOrWhiteSpace(elementTypeId))
            return null;
        if (TryGetElementType(elementTypeId) is { } et && !string.IsNullOrWhiteSpace(et.CategoryId))
            return et.CategoryId.Trim();
        return DefaultCategoryForElementType(elementTypeId.Trim());
    }

    public string? GetDisplayNameForCategory(string? categoryId)
    {
        if (string.IsNullOrWhiteSpace(categoryId))
            return null;
        var key = categoryId.Trim();
        foreach (var c in _root.Categories)
        {
            if (string.Equals(c.Id, key, StringComparison.OrdinalIgnoreCase))
                return c.DisplayName;
        }

        return key;
    }

    private static string? DefaultCategoryForElementType(string elementTypeId)
    {
        if (string.IsNullOrWhiteSpace(elementTypeId))
            return null;
        var id = elementTypeId.Trim();
        if (id is "col" or "beam" or "slab" or "wall" or "stair" or "landing" or "corbel" or "socket" or "truss")
            return "superstructure";
        if (id.StartsWith("inf-", StringComparison.OrdinalIgnoreCase))
            return "substructure";
        if (id.StartsWith("env-", StringComparison.OrdinalIgnoreCase))
            return "environmental_protection";
        if (id.StartsWith("land-", StringComparison.OrdinalIgnoreCase))
            return "landscaping";
        if (id.StartsWith("en-", StringComparison.OrdinalIgnoreCase))
            return "energy_carrier";
        if (id.StartsWith("cst-", StringComparison.OrdinalIgnoreCase))
            return "custom_prefab";
        return null;
    }

    public ElementTypeDefinition? TryGetElementType(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;
        return _elementTypesById.TryGetValue(id.Trim(), out var et) ? et : null;
    }

    public TypologyCatalogDefinition? TryGetTypology(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;
        return _typologiesById.TryGetValue(id.Trim(), out var t) ? t : null;
    }

    /// <summary><c>categories.json</c> içindeki sırayı koruyarak <paramref name="elementTypeId"/> ile eşleşen tipolojiler.</summary>
    public IReadOnlyList<TypologyCatalogDefinition> GetTypologiesForElementType(string? elementTypeId)
    {
        if (string.IsNullOrWhiteSpace(elementTypeId))
            return Array.Empty<TypologyCatalogDefinition>();
        var eid = elementTypeId.Trim();
        var list = new List<TypologyCatalogDefinition>();
        foreach (var t in _root.Typologies)
        {
            if (string.Equals(t.ElementTypeId, eid, StringComparison.OrdinalIgnoreCase))
                list.Add(t);
        }

        return list;
    }

    /// <summary>Tipoloji <see cref="TypologyCatalogDefinition.IdentifyingDimensions"/> sırasına göre alanlar.</summary>
    public IReadOnlyList<AttributeFieldDefinition> GetAttributeFieldsForTypology(string? typologyId)
    {
        var t = TryGetTypology(typologyId);
        if (t is null)
            return Array.Empty<AttributeFieldDefinition>();

        var list = new List<AttributeFieldDefinition>();
        foreach (var dimKey in t.IdentifyingDimensions)
        {
            if (string.IsNullOrWhiteSpace(dimKey))
                continue;
            var trimmed = dimKey.Trim();
            AttributeFieldDefinition? def = null;
            if (_root.DimensionFields.TryGetValue(trimmed, out var direct))
                def = direct;
            else
            {
                foreach (var kv in _root.DimensionFields)
                {
                    if (!string.Equals(kv.Key, trimmed, StringComparison.OrdinalIgnoreCase))
                        continue;
                    def = kv.Value;
                    break;
                }
            }

            if (def is not null)
            {
                list.Add(def);
                continue;
            }

            list.Add(new AttributeFieldDefinition
            {
                Tag = trimmed,
                Label = trimmed,
                Type = "number",
            });
        }

        return list;
    }

    /// <summary>CSV / görünen ad: eleman tipi veya tipoloji.</summary>
    public string GetDisplayNameOrId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return "—";
        var key = id.Trim();
        if (TryGetElementType(key) is { } et)
            return et.DisplayName;
        if (TryGetTypology(key) is { } typ)
            return typ.DisplayName;
        return key;
    }

    /// <summary>Liste özeti: «Eleman tipi › Tipoloji».</summary>
    public string GetTypologyLineSummary(string? elementTypeId, string? typologyId)
    {
        var parts = new List<string>();
        if (TryGetElementType(elementTypeId) is { } et)
            parts.Add(et.DisplayName);
        if (TryGetTypology(typologyId) is { } t)
            parts.Add(t.DisplayName);
        return parts.Count > 0 ? string.Join(" › ", parts) : "—";
    }

    /// <summary>Boş veya geçersiz seçimleri katalogdaki ilk geçerli çiftle doldurur; eski IFC <c>ProductCategoryId</c> → eleman tipi.</summary>
    public void ApplyDefaultPrefabSelection(ProductRecord record)
    {
        if (_root.ElementTypes.Count == 0 || _root.Typologies.Count == 0)
            return;

        TryMigrateLegacyIfcProductCategory(record);

        if (!string.IsNullOrWhiteSpace(record.PrefabElementTypeId)
            && !string.IsNullOrWhiteSpace(record.PrefabTypologyId))
        {
            var tv = TryGetTypology(record.PrefabTypologyId);
            if (tv is not null
                && string.Equals(tv.ElementTypeId, record.PrefabElementTypeId, StringComparison.OrdinalIgnoreCase))
                return;
        }

        var firstEt = _root.ElementTypes[0];
        record.PrefabElementTypeId ??= firstEt.Id;
        var etCurrent = TryGetElementType(record.PrefabElementTypeId) ?? firstEt;
        record.PrefabElementTypeId = etCurrent.Id;
        record.ElementCategoryId ??= GetCategoryIdForElementType(etCurrent.Id);
        var typs = GetTypologiesForElementType(etCurrent.Id);
        var pick = typs.FirstOrDefault(t =>
                       string.Equals(t.Id, record.PrefabTypologyId, StringComparison.OrdinalIgnoreCase))
                   ?? typs.FirstOrDefault();
        record.PrefabTypologyId = pick?.Id;
    }

    private void TryMigrateLegacyIfcProductCategory(ProductRecord record)
    {
        if (!string.IsNullOrWhiteSpace(record.PrefabElementTypeId))
            return;
        var cat = record.ProductCategoryId?.Trim();
        if (string.IsNullOrWhiteSpace(cat))
            return;
        var u = cat.ToUpperInvariant();
        var elementTypeId = u switch
        {
            "COLUMN" => "col",
            "BEAM" => "beam",
            "SLAB" => "slab",
            "WALL" => "wall",
            "STAIR" => "stair",
            _ => null,
        };
        if (elementTypeId is null || TryGetElementType(elementTypeId) is null)
            return;
        record.PrefabElementTypeId = elementTypeId;
        if (string.IsNullOrWhiteSpace(record.PrefabTypologyId))
            record.PrefabTypologyId = GetTypologiesForElementType(elementTypeId).FirstOrDefault()?.Id;
    }

    private static AttributeCatalogService Load()
    {
        var asm = Assembly.GetExecutingAssembly();
        var name = asm.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith("categories.json", StringComparison.OrdinalIgnoreCase));
        if (name is null)
            return new AttributeCatalogService(new PrefabCatalogRoot());

        using var s = asm.GetManifestResourceStream(name);
        if (s is null)
            return new AttributeCatalogService(new PrefabCatalogRoot());

        var root = JsonSerializer.Deserialize<PrefabCatalogRoot>(s, JsonOptions);
        return new AttributeCatalogService(root ?? new PrefabCatalogRoot());
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };
}
