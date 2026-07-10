using System.Reflection;
using System.Text.Json;
using BimPrefabExport.Core;
using BimPrefabExport.Schema;

namespace BimPrefabExport.Services;

/// <summary>Gömülü <c>materials.json</c> — frontend malzeme kataloğu ile uyumlu BOM seçenekleri.</summary>
internal sealed class MaterialCatalogService
{
    private static readonly Lazy<MaterialCatalogService> Lazy = new(Load);

    private readonly MaterialCatalogRoot _root;
    private readonly Dictionary<string, MaterialCatalogEntry> _byCode;

    private MaterialCatalogService(MaterialCatalogRoot root, IReadOnlyList<MaterialCatalogPickerOption> pickerOptions)
    {
        _root = root;
        _byCode = new Dictionary<string, MaterialCatalogEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in root.Materials)
        {
            if (string.IsNullOrWhiteSpace(m.Code))
                continue;
            _byCode[m.Code.Trim()] = m;
        }

        CatalogPickerOptions = pickerOptions;
    }

    public static MaterialCatalogService Default => Lazy.Value;

    public IReadOnlyList<MaterialCatalogEntry> Materials => _root.Materials;

    public IReadOnlyList<RebarShapeOption> RebarShapes => _root.RebarShapes;

    public IReadOnlyList<string> SteelGrades => _root.SteelGrades;

    /// <summary>Katalog ComboBox — boş seçenek özel / eski satırlar için.</summary>
    public IReadOnlyList<MaterialCatalogPickerOption> CatalogPickerOptions { get; }

    public MaterialCatalogEntry? TryGetByCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;
        return _byCode.TryGetValue(code.Trim(), out var m) ? m : null;
    }

    public void ApplyCatalogSelection(MaterialLine line)
    {
        var entry = TryGetByCode(line.MaterialCatalogCode);
        if (entry is null)
            return;

        line.Category = string.IsNullOrWhiteSpace(entry.CategoryLabel) ? entry.Category : entry.CategoryLabel;
        line.Code = entry.Code;
        line.Description = entry.Name;
        line.Unit = string.IsNullOrWhiteSpace(entry.DefaultUnit) ? "ad" : entry.DefaultUnit;
        if (!string.IsNullOrWhiteSpace(entry.Specification))
            line.Notes = entry.Specification;
    }

    private static MaterialCatalogService Load()
    {
        var asm = Assembly.GetExecutingAssembly();
        var name = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("materials.json", StringComparison.OrdinalIgnoreCase));
        if (name is null)
            return new MaterialCatalogService(new MaterialCatalogRoot(), [MaterialCatalogPickerOption.Blank]);

        using var s = asm.GetManifestResourceStream(name);
        if (s is null)
            return new MaterialCatalogService(new MaterialCatalogRoot(), [MaterialCatalogPickerOption.Blank]);

        var root = JsonSerializer.Deserialize<MaterialCatalogRoot>(s, JsonOptions) ?? new MaterialCatalogRoot();
        var picker = new List<MaterialCatalogPickerOption> { MaterialCatalogPickerOption.Blank };
        foreach (var m in root.Materials)
        {
            if (string.IsNullOrWhiteSpace(m.Code))
                continue;
            picker.Add(new MaterialCatalogPickerOption(m.Code.Trim(), $"{m.Code.Trim()} — {m.Name}"));
        }

        return new MaterialCatalogService(root, picker);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };
}

internal sealed class MaterialCatalogPickerOption
{
    public static MaterialCatalogPickerOption Blank { get; } = new("", "— Özel / boş —");

    public MaterialCatalogPickerOption(string code, string displayLabel)
    {
        Code = code;
        DisplayLabel = displayLabel;
    }

    public string Code { get; }
    public string DisplayLabel { get; }
}
