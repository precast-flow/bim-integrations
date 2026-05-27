using System.Globalization;
using System.Text.RegularExpressions;
using BimPrefabExport.Core;

namespace BimPrefabExport.Services;

/// <summary>Excel / CSV satırlarını malzeme ve donatı satırlarına eşler (sabit sütun sırası).</summary>
internal static class BomImportMapper
{
    /// <summary>Sütun sırası: Kategori, Kod, Açıklama, Miktar, Birim, Not (en az 3: Kod, Açıklama, Miktar).</summary>
    public static List<MaterialLine> MapMaterials(IEnumerable<string[]> rows)
    {
        var list = new List<MaterialLine>();
        foreach (var raw in rows)
        {
            var row = TrimTrailing(raw);
            if (row.Length == 0 || IsAllEmpty(row))
                continue;

            MaterialLine m;
            if (row.Length >= 6)
            {
                m = new MaterialLine
                {
                    Category = Cell(row, 0),
                    Code = Cell(row, 1),
                    Description = Cell(row, 2),
                    Quantity = ParseDouble(Cell(row, 3)) ?? 0,
                    Unit = string.IsNullOrWhiteSpace(Cell(row, 4)) ? "ea" : Cell(row, 4),
                    Notes = Cell(row, 5),
                };
            }
            else if (row.Length >= 4)
            {
                m = new MaterialLine
                {
                    Category = "Malzeme",
                    Code = Cell(row, 0),
                    Description = Cell(row, 1),
                    Quantity = ParseDouble(Cell(row, 2)) ?? 0,
                    Unit = string.IsNullOrWhiteSpace(Cell(row, 3)) ? "ea" : Cell(row, 3),
                    Notes = row.Length > 4 ? string.Join(" ", row.Skip(4)) : "",
                };
            }
            else if (row.Length >= 3)
            {
                m = new MaterialLine
                {
                    Category = "Malzeme",
                    Code = Cell(row, 0),
                    Description = Cell(row, 1),
                    Quantity = ParseDouble(Cell(row, 2)) ?? 0,
                    Unit = "ea",
                    Notes = "",
                };
            }
            else
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(m.Code) && string.IsNullOrWhiteSpace(m.Description) && m.Quantity == 0)
                continue;

            if (m.Quantity is < -1e12 or > 1e12)
                m.Quantity = 0;
            m.Notes = m.Notes.Length > 4000 ? m.Notes[..4000] : m.Notes;

            list.Add(m);
        }

        return list;
    }

    /// <summary>Sütun sırası: Poz, Çap (mm), Adet, H (mm), L (mm), Not (isteğe bağlı).</summary>
    public static List<RebarLine> MapRebars(IEnumerable<string[]> rows)
    {
        var list = new List<RebarLine>();
        foreach (var raw in rows)
        {
            var row = TrimTrailing(raw);
            if (row.Length == 0 || IsAllEmpty(row))
                continue;

            var poz = Cell(row, 0);
            var cap = Cell(row, 1);
            var adet = Cell(row, 2);
            var h = Cell(row, 3);
            var l = Cell(row, 4);
            var notes = row.Length > 5 ? Cell(row, 5) : "";

            if (string.IsNullOrWhiteSpace(poz) && string.IsNullOrWhiteSpace(cap) && string.IsNullOrWhiteSpace(adet))
                continue;

            var cnt = ParseDouble(adet) ?? 0;
            if (cnt is < 0 or > 1_000_000)
                cnt = 0;

            list.Add(new RebarLine
            {
                PozNo = poz,
                DiameterMm = ParseDiameterMm(cap),
                Count = cnt,
                LengthH_mm = ClampMm(ParseDouble(h)),
                LengthL_mm = ClampMm(ParseDouble(l)),
                Notes = notes.Length > 4000 ? notes[..4000] : notes,
            });
        }

        return list;
    }

    private static string Cell(string[] row, int i) =>
        i < row.Length ? (row[i]?.Trim() ?? "") : "";

    private static string[] TrimTrailing(string[] row)
    {
        var len = row.Length;
        while (len > 0 && string.IsNullOrWhiteSpace(row[len - 1]))
            len--;
        if (len == row.Length)
            return row;
        var a = new string[len];
        Array.Copy(row, a, len);
        return a;
    }

    private static bool IsAllEmpty(string[] row) => row.All(string.IsNullOrWhiteSpace);

    private static double? ParseDouble(string? s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return null;
        var t = s.Trim().Replace(',', '.');
        if (double.TryParse(t, NumberStyles.Any, CultureInfo.InvariantCulture, out var v))
            return v;
        return double.TryParse(s.Trim(), NumberStyles.Any, CultureInfo.CurrentCulture, out v) ? v : null;
    }

    private static double? ClampMm(double? mm)
    {
        if (!mm.HasValue)
            return null;
        var v = mm.Value;
        if (double.IsNaN(v) || double.IsInfinity(v))
            return null;
        if (v is < -1e7 or > 1e7)
            return null;
        return v;
    }

    private static double? ParseDiameterMm(string? s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return null;
        var t = s.Trim();
        t = Regex.Replace(t, @"^(Ø|Φ|φ|D\.?|d\.?)\s*", "", RegexOptions.IgnoreCase);
        t = t.Replace("mm", "", StringComparison.OrdinalIgnoreCase).Trim();
        t = t.Replace(',', '.');
        if (double.TryParse(t, NumberStyles.Any, CultureInfo.InvariantCulture, out var v))
            return v;
        return double.TryParse(t, NumberStyles.Any, CultureInfo.CurrentCulture, out v) ? v : null;
    }
}
