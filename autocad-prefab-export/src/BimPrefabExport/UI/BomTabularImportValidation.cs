using BimPrefabExport.Services;

namespace BimPrefabExport.UI;

/// <summary>Malzeme / donatı tablo içe aktarma öncesi satır ve sütun güvenlik kontrolleri.</summary>
internal static class BomTabularImportValidation
{
    public static bool TryValidateParsedRows(IReadOnlyList<string[]> rows, out string? error)
    {
        error = null;
        if (rows.Count == 0)
        {
            error = "Dosyada veri satırı bulunamadı.";
            return false;
        }

        if (rows.Count > CsvTableTextParser.MaxDataRows)
        {
            error = $"En fazla {CsvTableTextParser.MaxDataRows:N0} satır desteklenir.";
            return false;
        }

        for (var i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            if (r.Length > CsvTableTextParser.MaxColumns)
            {
                error =
                    $"Satır {i + 1}: en fazla {CsvTableTextParser.MaxColumns} sütun (şu an {r.Length}). Dosya biçimi veya yanlış ayraç olabilir.";
                return false;
            }

            foreach (var c in r)
            {
                if (c.Length > CsvTableTextParser.MaxCellChars)
                {
                    error = $"Satır {i + 1}: bir hücre {CsvTableTextParser.MaxCellChars} karakterden uzun; dosyayı sadeleştirin.";
                    return false;
                }
            }
        }

        return true;
    }
}
