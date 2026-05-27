using BimPrefabExport.Core;

namespace BimPrefabExport.Services;

/// <summary>Paket PDF ve kayıt öncesi: ürün kodu ve çizim adı benzersizliği.</summary>
public static class PrefabExportValidation
{
    public static bool TryValidateUniqueProductCodes(IReadOnlyList<ProductRecord> products, out string? error)
    {
        error = null;
        var byCode = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in products)
        {
            var code = p.Code?.Trim() ?? "";
            if (!byCode.TryGetValue(code, out var list))
            {
                list = [];
                byCode[code] = list;
            }

            var label = string.IsNullOrWhiteSpace(p.DisplayName)
                ? (string.IsNullOrWhiteSpace(code) ? $"Ürün {p.ProductId:D}" : code)
                : p.DisplayName.Trim();
            list.Add(string.IsNullOrEmpty(code) ? $"{label} (boş kod)" : $"{label}  [{code}]");
        }

        foreach (var kv in byCode)
        {
            if (kv.Value.Count <= 1)
                continue;
            if (string.IsNullOrEmpty(kv.Key))
            {
                error =
                    "Birden fazla ürünün kodu boş. Paket PDF ve liste için her ürüne benzersiz bir kod verin:\n\n" +
                    string.Join("\n", kv.Value);
                return false;
            }

            error = $"Ürün kodu «{kv.Key}» yineleniyor. Aynı kod iki üründe kullanılamaz:\n\n" + string.Join("\n", kv.Value);
            return false;
        }

        return true;
    }

    public static bool TryValidateUniquePdfTitlesPerProduct(ProductRecord product, out string? error)
    {
        error = null;
        ProductPdfDrawingSync.NormalizeProductRecord(product);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var d in product.PdfDrawings)
        {
            var t = string.IsNullOrWhiteSpace(d.PdfTitle) ? "cizim" : d.PdfTitle.Trim();
            if (!seen.Add(t))
            {
                error =
                    $"Ürün «{product.DisplayName}» içinde PDF çizim adı «{t}» birden fazla kez kullanılmış. " +
                    "Paket dışa aktarmada dosya adı çakışmasını önlemek için her çizim için farklı bir ad girin.";
                return false;
            }
        }

        return true;
    }

    public static bool TryValidateBundle(IReadOnlyList<ProductRecord> products, out string? error)
    {
        if (!TryValidateUniqueProductCodes(products, out error))
            return false;
        foreach (var p in products)
        {
            if (!TryValidateUniquePdfTitlesPerProduct(p, out error))
                return false;
        }

        return true;
    }
}
