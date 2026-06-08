using System.Globalization;
using System.IO;
using System.Text;
using BimPrefabExport.Core;
using BimPrefabExport.Services;

namespace BimPrefabExport.Export;

/// <summary>UTF-8 (BOM) CSV; Excel ile açılabilir.</summary>
public static class CsvListExportBuilder
{
    public sealed class ExportResult
    {
        public string ProductsPath { get; init; } = "";
        public string MaterialsPath { get; init; } = "";
        public string RebarsPath { get; init; } = "";
        public string DrawingsPath { get; init; } = "";
    }

    /// <param name="basePathWithoutExtension">Örn. <c>C:\Rapor\liste</c> → <c>liste_urunler.csv</c>, …</param>
    public static ExportResult Write(
        string basePathWithoutExtension,
        DocumentSummary drawing,
        IReadOnlyList<ProductRecord> products,
        IReadOnlyList<ExportDrawingEntry>? drawings = null)
    {
        var enc = new UTF8Encoding(true);
        var result = new ExportResult
        {
            ProductsPath = basePathWithoutExtension + "_urunler.csv",
            MaterialsPath = basePathWithoutExtension + "_materyaller.csv",
            RebarsPath = basePathWithoutExtension + "_donatilar.csv",
            DrawingsPath = basePathWithoutExtension + "_cizimler.csv",
        };

        var attrKeys = products
            .SelectMany(p => p.Attributes.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
            .ToList();

        WriteProductsCsv(result.ProductsPath, enc, drawing, products, attrKeys);
        WriteMaterialsCsv(result.MaterialsPath, enc, products);
        WriteRebarsCsv(result.RebarsPath, enc, products);
        WriteDrawingsCsv(result.DrawingsPath, enc, drawings ?? Array.Empty<ExportDrawingEntry>());

        return result;
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
            "KategoriId",
            "ElemanTipiId",
            "ElemanTipi",
            "TipolojiId",
            "Tipoloji",
            "ÜrünKategorisiId",
            "ÜrünKategorisi",
            "Not",
            "İçerikÖzeti",
        };
        headers.AddRange(attrKeys);
        w.WriteLine(string.Join(",", headers.Select(Escape)));

        var cat = AttributeCatalogService.Default;
        foreach (var p in products)
        {
            var elementTypeId = p.PrefabElementTypeId?.Trim() ?? "";
            var typId = p.PrefabTypologyId?.Trim() ?? "";
            var elementTypeName = string.IsNullOrEmpty(elementTypeId) ? "" : cat.GetDisplayNameOrId(elementTypeId);
            var typName = string.IsNullOrEmpty(typId) ? "" : cat.GetDisplayNameOrId(typId);
            var legacyCatId = p.ProductCategoryId?.Trim() ?? "";
            var legacyCatName = string.IsNullOrEmpty(legacyCatId)
                ? ""
                : cat.GetDisplayNameOrId(legacyCatId);
            var categoryId = p.ElementCategoryId?.Trim()
                             ?? cat.GetCategoryIdForElementType(elementTypeId)
                             ?? "";
            var cells = new List<string>
            {
                drawing.FileName,
                p.ProductId.ToString("D"),
                p.DisplayName,
                p.Code,
                p.Quantity.ToString(CultureInfo.InvariantCulture),
                p.Unit,
                p.Revision.ToString(CultureInfo.InvariantCulture),
                categoryId,
                elementTypeId,
                elementTypeName,
                typId,
                typName,
                legacyCatId,
                legacyCatName,
                p.Note ?? "",
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
        var headers = new[]
        {
            "ÜrünId", "ÜrünKodu", "ÜrünAdı", "Kategori", "Kod", "KatalogKodu", "Açıklama", "Miktar", "Birim", "Not",
        };
        w.WriteLine(string.Join(",", headers.Select(Escape)));

        foreach (var p in products)
        {
            foreach (var m in p.Materials ?? [])
            {
                var cells = new[]
                {
                    p.ProductId.ToString("D"),
                    p.Code,
                    p.DisplayName,
                    m.Category,
                    m.Code,
                    m.MaterialCatalogCode,
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
            "ÜrünId", "ÜrünKodu", "ÜrünAdı", "Poz", "Çap_mm", "Adet", "H_mm", "L_mm",
            "CelikSinifi", "Sekil", "GelismeUzunlugu_mm", "ToplamKg", "Not",
        };
        w.WriteLine(string.Join(",", headers.Select(Escape)));

        var poz = 0;
        foreach (var p in products)
        {
            poz = 0;
            foreach (var r in p.Rebars ?? [])
            {
                poz++;
                RebarWeightHelper.NormalizeRebarRow(r, poz);
                var cells = new[]
                {
                    p.ProductId.ToString("D"),
                    p.Code,
                    p.DisplayName,
                    r.PozNo,
                    r.DiameterMm?.ToString(CultureInfo.InvariantCulture) ?? "",
                    r.Count.ToString(CultureInfo.InvariantCulture),
                    r.LengthH_mm?.ToString(CultureInfo.InvariantCulture) ?? "",
                    r.LengthL_mm?.ToString(CultureInfo.InvariantCulture) ?? "",
                    r.SteelGrade,
                    r.Shape,
                    r.DevelopedLengthMm?.ToString(CultureInfo.InvariantCulture) ?? "",
                    r.TotalWeightKg?.ToString(CultureInfo.InvariantCulture) ?? "",
                    r.Notes,
                };
                w.WriteLine(string.Join(",", cells.Select(Escape)));
            }
        }
    }

    private static void WriteDrawingsCsv(string path, Encoding enc, IReadOnlyList<ExportDrawingEntry> drawings)
    {
        using var w = new StreamWriter(path, false, enc);
        var headers = new[] { "ÜrünId", "ÜrünKodu", "DosyaAdi", "PdfBaslik", "Revizyon", "GoreceliYol" };
        w.WriteLine(string.Join(",", headers.Select(Escape)));

        foreach (var d in drawings)
        {
            var cells = new[]
            {
                d.ProductId.ToString("D"),
                d.ProductCode,
                d.FileName,
                d.PdfTitle,
                d.Revision.ToString(CultureInfo.InvariantCulture),
                d.RelativePath,
            };
            w.WriteLine(string.Join(",", cells.Select(Escape)));
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
