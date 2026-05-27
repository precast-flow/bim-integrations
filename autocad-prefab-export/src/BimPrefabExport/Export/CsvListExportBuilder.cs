using System.Globalization;
using System.IO;
using System.Text;
using BimPrefabExport.Core;
using BimPrefabExport.Services;

namespace BimPrefabExport.Export;

/// <summary>UTF-8 (BOM) CSV; Excel ile açılabilir.</summary>
public static class CsvListExportBuilder
{
    /// <param name="basePathWithoutExtension">Örn. <c>C:\Rapor\liste</c> → <c>liste_urunler.csv</c>, <c>liste_materyaller.csv</c>, <c>liste_donatilar.csv</c>.</param>
    public static (string ProductsPath, string MaterialsPath, string RebarsPath) Write(
        string basePathWithoutExtension,
        DocumentSummary drawing,
        IReadOnlyList<ProductRecord> products)
    {
        var enc = new UTF8Encoding(true);
        var productsPath = basePathWithoutExtension + "_urunler.csv";
        var materialsPath = basePathWithoutExtension + "_materyaller.csv";
        var rebarsPath = basePathWithoutExtension + "_donatilar.csv";

        var attrKeys = products
            .SelectMany(p => p.Attributes.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
            .ToList();

        WriteProductsCsv(productsPath, enc, drawing, products, attrKeys);
        WriteMaterialsCsv(materialsPath, enc, products);
        WriteRebarsCsv(rebarsPath, enc, products);

        return (productsPath, materialsPath, rebarsPath);
    }

    private static void WriteProductsCsv(
        string path,
        Encoding enc,
        DocumentSummary drawing,
        IReadOnlyList<ProductRecord> products,
        List<string> attrKeys)
    {
        using var w = new StreamWriter(path, false, enc);
        var headers = new List<string>
        {
            "Çizim",
            "ÜrünId",
            "GörünenAd",
            "Kod",
            "Adet",
            "Birim",
            "Revizyon",
            "ElemanTipiId",
            "ElemanTipi",
            "TipolojiId",
            "Tipoloji",
            "ÜrünKategorisiId",
            "ÜrünKategorisi",
            "İçerikÖzeti",
        };
        headers.AddRange(attrKeys);
        w.WriteLine(string.Join(",", headers.Select(Escape)));

        foreach (var p in products)
        {
            var elementTypeId = p.PrefabElementTypeId?.Trim() ?? "";
            var typId = p.PrefabTypologyId?.Trim() ?? "";
            var cat = AttributeCatalogService.Default;
            var elementTypeName = string.IsNullOrEmpty(elementTypeId) ? "" : cat.GetDisplayNameOrId(elementTypeId);
            var typName = string.IsNullOrEmpty(typId) ? "" : cat.GetDisplayNameOrId(typId);
            var legacyCatId = p.ProductCategoryId?.Trim() ?? "";
            var legacyCatName = string.IsNullOrEmpty(legacyCatId)
                ? ""
                : cat.GetDisplayNameOrId(legacyCatId);
            var cells = new List<string>
            {
                drawing.FileName,
                p.ProductId.ToString("D"),
                p.DisplayName,
                p.Code,
                p.Quantity.ToString(CultureInfo.InvariantCulture),
                p.Unit,
                p.Revision.ToString(CultureInfo.InvariantCulture),
                elementTypeId,
                elementTypeName,
                typId,
                typName,
                legacyCatId,
                legacyCatName,
                p.ComputeContentHash(),
            };
            foreach (var ak in attrKeys)
            {
                p.Attributes.TryGetValue(ak, out var v);
                cells.Add(v ?? "");
            }

            w.WriteLine(string.Join(",", cells.Select(Escape)));
        }
    }

    private static void WriteMaterialsCsv(string path, Encoding enc, IReadOnlyList<ProductRecord> products)
    {
        using var w = new StreamWriter(path, false, enc);
        var headers = new[] { "ÜrünId", "ÜrünAdı", "Kategori", "Kod", "Açıklama", "Miktar", "Birim", "Not" };
        w.WriteLine(string.Join(",", headers.Select(Escape)));

        foreach (var p in products)
        {
            foreach (var m in p.Materials ?? [])
            {
                var cells = new[]
                {
                    p.ProductId.ToString("D"),
                    p.DisplayName,
                    m.Category,
                    m.Code,
                    m.Description,
                    m.Quantity.ToString(CultureInfo.InvariantCulture),
                    m.Unit,
                    m.Notes,
                };
                w.WriteLine(string.Join(",", cells.Select(Escape)));
            }
        }
    }

    private static void WriteRebarsCsv(string path, Encoding enc, IReadOnlyList<ProductRecord> products)
    {
        using var w = new StreamWriter(path, false, enc);
        var headers = new[]
        {
            "ÜrünId", "ÜrünAdı", "Poz", "Çap_mm", "Adet", "H_mm", "L_mm", "Not",
        };
        w.WriteLine(string.Join(",", headers.Select(Escape)));

        foreach (var p in products)
        {
            foreach (var r in p.Rebars ?? [])
            {
                var cells = new[]
                {
                    p.ProductId.ToString("D"),
                    p.DisplayName,
                    r.PozNo,
                    r.DiameterMm?.ToString(CultureInfo.InvariantCulture) ?? "",
                    r.Count.ToString(CultureInfo.InvariantCulture),
                    r.LengthH_mm?.ToString(CultureInfo.InvariantCulture) ?? "",
                    r.LengthL_mm?.ToString(CultureInfo.InvariantCulture) ?? "",
                    r.Notes,
                };
                w.WriteLine(string.Join(",", cells.Select(Escape)));
            }
        }
    }

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        var s = value.Replace("\"", "\"\"", StringComparison.Ordinal);

        if (s.IndexOfAny([',', '\r', '\n', '"']) >= 0)
            return $"\"{s}\"";

        return s;
    }
}
