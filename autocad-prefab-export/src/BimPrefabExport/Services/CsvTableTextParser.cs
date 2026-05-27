using System.Linq;
using System.Text;

namespace BimPrefabExport.Services;

/// <summary>Tırnak içi satır sonlarını dikkate alarak CSV metnini satırlara ve hücrelere böler.</summary>
internal static class CsvTableTextParser
{
    /// <summary>Ham metin boyutu üst sınırı (Excel dışa aktarma / büyük dosya koruması).</summary>
    public const int MaxSourceChars = 6 * 1024 * 1024;

    public const int MaxDataRows = 50_000;
    public const int MaxColumns = 64;
    public const int MaxCellChars = 8000;

    /// <summary>Tırnak içindeki \n / \r\n birleşik satırları koruyarak mantıksal satır listesi üretir.</summary>
    public static List<string> SplitLogicalLines(string text)
    {
        var lines = new List<string>();
        var sb = new StringBuilder(text.Length > 0 ? Math.Min(text.Length, 256 * 1024) : 64);
        var inQ = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '"')
            {
                if (inQ && i + 1 < text.Length && text[i + 1] == '"')
                {
                    sb.Append('"');
                    i++;
                    continue;
                }

                inQ = !inQ;
                sb.Append('"');
                continue;
            }

            if (!inQ && c == '\r')
            {
                if (i + 1 < text.Length && text[i + 1] == '\n')
                    i++;
                lines.Add(sb.ToString());
                sb.Clear();
                continue;
            }

            if (!inQ && c == '\n')
            {
                lines.Add(sb.ToString());
                sb.Clear();
                continue;
            }

            sb.Append(c);
        }

        lines.Add(sb.ToString());
        return lines;
    }

    public static char PickDelimiter(string line)
    {
        var tabs = CountUnquoted(line, '\t');
        var semi = CountUnquoted(line, ';');
        var comma = CountUnquoted(line, ',');
        if (tabs > 0 && tabs >= semi && tabs >= comma)
            return '\t';
        if (semi > 0 && semi >= comma)
            return ';';
        return ',';
    }

    public static List<string> SplitRowIntoCells(string line, char delim)
    {
        if (delim == '\t')
            return line.Split('\t').Select(s => s.Trim()).ToList();

        var r = new List<string>();
        var sb = new StringBuilder();
        var inQ = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                if (inQ && i + 1 < line.Length && line[i + 1] == '"')
                {
                    sb.Append('"');
                    i++;
                }
                else
                {
                    inQ = !inQ;
                }
            }
            else if (!inQ && c == delim)
            {
                r.Add(NormalizeCell(sb.ToString()));
                sb.Clear();
            }
            else
            {
                sb.Append(c);
            }
        }

        r.Add(NormalizeCell(sb.ToString()));
        return r;
    }

    private static string NormalizeCell(string s)
    {
        s = s.Trim();
        if (s.IndexOf('\0') >= 0)
            s = s.Replace("\0", "", StringComparison.Ordinal);
        if (s.Length > MaxCellChars)
            s = s[..MaxCellChars];
        return s;
    }

    private static int CountUnquoted(string line, char ch)
    {
        var n = 0;
        var inQ = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                if (inQ && i + 1 < line.Length && line[i + 1] == '"')
                {
                    i++;
                    continue;
                }

                inQ = !inQ;
            }
            else if (!inQ && c == ch)
            {
                n++;
            }
        }

        return n;
    }

    private static string[] TrimTrailingEmpty(List<string> cells)
    {
        var len = cells.Count;
        while (len > 0 && string.IsNullOrWhiteSpace(cells[len - 1]))
            len--;
        if (len == cells.Count)
            return cells.ToArray();
        return cells.Take(len).ToArray();
    }

    public static bool TryParseToRows(string text, out List<string[]> rows, out string? error)
    {
        rows = new List<string[]>();
        error = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            error = "Dosya boş.";
            return false;
        }

        var logical = SplitLogicalLines(text);
        var firstIdx = -1;
        for (var i = 0; i < logical.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(logical[i]))
            {
                firstIdx = i;
                break;
            }
        }

        if (firstIdx < 0)
        {
            error = "Dosyada veri satırı bulunamadı.";
            return false;
        }

        var delim = PickDelimiter(logical[firstIdx]!);
        var colCounts = new List<int>();

        for (var li = firstIdx; li < logical.Count; li++)
        {
            var line = logical[li];
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var cells = SplitRowIntoCells(line, delim);
            var arr = TrimTrailingEmpty(cells);
            if (arr.Length == 0 || arr.All(string.IsNullOrWhiteSpace))
                continue;

            if (arr.Length > MaxColumns)
            {
                error =
                    $"En fazla {MaxColumns} sütun desteklenir (satır {rows.Count + 1}: {arr.Length} sütun). Ayırıcı veya dosya biçimi hatalı olabilir.";
                rows.Clear();
                return false;
            }

            rows.Add(arr);
            if (colCounts.Count < 40)
                colCounts.Add(arr.Length);

            if (rows.Count > MaxDataRows)
            {
                error = $"En fazla {MaxDataRows:N0} veri satırı okunabilir; dosya kesildi.";
                return false;
            }
        }

        if (rows.Count == 0)
        {
            error = "Dosyada veri satırı bulunamadı.";
            return false;
        }

        if (colCounts.Count >= 8)
        {
            var spread = colCounts.Max() - colCounts.Min();
            if (spread > 24)
            {
                error =
                    "Satırlar arasında sütun sayısı çok oynuyor; ayraç (; / ,) veya tırnaklar Excel’de bozulmuş olabilir. " +
                    "Paletten «CSV indir» ile şablon alıp aynı biçimde doldurmanız önerilir.";
                rows.Clear();
                return false;
            }
        }

        return true;
    }
}
