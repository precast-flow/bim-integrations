using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using BimPrefabExport.Core;

namespace BimPrefabExport.UI;

/// <summary>Malzeme / donatı listelerini CSV olarak dışa aktarır (Excel TR: noktalı virgül + tüm alanlar tırnaklı).</summary>
internal static class BomTabularExportHelper
{
    private const char Delim = ';';
    private static readonly UTF8Encoding Utf8Bom = new(true);

    public static void TrySaveMaterialsCsv(System.Windows.Window owner, string suggestedBaseName, IReadOnlyList<MaterialLine> lines)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "CSV|*.csv",
            DefaultExt = ".csv",
            FileName = SanitizeFileName(suggestedBaseName) + "_malzemeler.csv",
            Title = "Malzeme listesini CSV olarak kaydet",
        };

        if (dlg.ShowDialog(owner) != true || string.IsNullOrWhiteSpace(dlg.FileName))
            return;

        try
        {
            using var sw = new StreamWriter(dlg.FileName, false, Utf8Bom);
            sw.WriteLine(JoinRow(
                "Kategori",
                "Kod",
                "Aciklama",
                "Miktar",
                "Birim",
                "Not"));

            foreach (var m in lines)
            {
                sw.WriteLine(JoinRow(
                    m.Category,
                    m.Code,
                    m.Description,
                    m.Quantity.ToString("G", CultureInfo.InvariantCulture),
                    m.Unit,
                    m.Notes));
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show("CSV yazılamadı: " + ex.Message, "BIM Prefab",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
        }
    }

    public static void TrySaveRebarsCsv(System.Windows.Window owner, string suggestedBaseName, IReadOnlyList<RebarLine> lines)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "CSV|*.csv",
            DefaultExt = ".csv",
            FileName = SanitizeFileName(suggestedBaseName) + "_donatilar.csv",
            Title = "Donatı listesini CSV olarak kaydet",
        };

        if (dlg.ShowDialog(owner) != true || string.IsNullOrWhiteSpace(dlg.FileName))
            return;

        try
        {
            using var sw = new StreamWriter(dlg.FileName, false, Utf8Bom);
            sw.WriteLine(JoinRow("Poz", "Cap_mm", "Adet", "H_mm", "L_mm", "Not"));

            foreach (var r in lines)
            {
                sw.WriteLine(JoinRow(
                    r.PozNo,
                    r.DiameterMm?.ToString("G", CultureInfo.InvariantCulture) ?? "",
                    r.Count.ToString("G", CultureInfo.InvariantCulture),
                    r.LengthH_mm?.ToString("G", CultureInfo.InvariantCulture) ?? "",
                    r.LengthL_mm?.ToString("G", CultureInfo.InvariantCulture) ?? "",
                    r.Notes));
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show("CSV yazılamadı: " + ex.Message, "BIM Prefab",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
        }
    }

    private static string JoinRow(params string?[] fields) =>
        string.Join(Delim, fields.Select(QuoteField));

    private static string QuoteField(string? value)
    {
        var s = (value ?? "")
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Replace("\t", " ", StringComparison.Ordinal)
            .Replace("\0", "", StringComparison.Ordinal);
        return "\"" + s.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }

    private static string SanitizeFileName(string baseName)
    {
        var t = string.IsNullOrWhiteSpace(baseName) ? "urun" : baseName.Trim();
        foreach (var c in Path.GetInvalidFileNameChars())
            t = t.Replace(c, '_');
        if (t.Length > 80)
            t = t[..80];
        return string.IsNullOrEmpty(t) ? "urun" : t;
    }
}
