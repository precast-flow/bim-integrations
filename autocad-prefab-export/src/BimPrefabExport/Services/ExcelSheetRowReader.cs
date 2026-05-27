using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using ExcelDataReader;

namespace BimPrefabExport.Services;

/// <summary>İlk çalışma sayfası / CSV satırlarını metin hücre dizileri olarak okur (ExcelDataReader + güvenli CSV).</summary>
internal static class ExcelSheetRowReader
{
    private const int MaxExcelRows = 50_000;

    static ExcelSheetRowReader() =>
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    public static bool TryReadFirstSheet(string path, out List<string[]> rows, out string? error)
    {
        rows = new List<string[]>();
        error = null;
        if (!File.Exists(path))
        {
            error = "Dosya bulunamadı.";
            return false;
        }

        var ext = Path.GetExtension(path);
        try
        {
            if (string.Equals(ext, ".csv", StringComparison.OrdinalIgnoreCase))
                return TryReadCsv(path, out rows, out error);

            using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = ExcelReaderFactory.CreateReader(stream);
            using var ds = reader.AsDataSet(new ExcelDataSetConfiguration
            {
                ConfigureDataTable = _ => new ExcelDataTableConfiguration { UseHeaderRow = false },
            });

            if (ds.Tables.Count == 0)
            {
                error = "Çalışma kitabında sayfa yok.";
                return false;
            }

            var table = ds.Tables[0];
            var n = 0;
            foreach (DataRow dr in table.Rows)
            {
                if (n++ >= MaxExcelRows)
                    break;
                var cells = ToStringCells(dr, table.Columns.Count);
                if (cells.Length > 0 && !IsRowAllEmpty(cells))
                    rows.Add(TrimTrailingEmpty(cells));
            }

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static bool TryReadCsv(string path, out List<string[]> rows, out string? error)
    {
        rows = new List<string[]>();
        error = null;
        try
        {
            if (!TryReadCsvFileText(path, out var text, out error))
                return false;

            if (!CsvTableTextParser.TryParseToRows(text, out rows, out error))
                return false;

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            rows = new List<string[]>();
            return false;
        }
    }

    private static bool TryReadCsvFileText(string path, out string text, out string? error)
    {
        text = "";
        error = null;
        var fi = new FileInfo(path);
        if (fi.Length > CsvTableTextParser.MaxSourceChars)
        {
            error = $"CSV dosyası çok büyük (>{CsvTableTextParser.MaxSourceChars / 1024 / 1024} MB).";
            return false;
        }

        var bytes = File.ReadAllBytes(path);
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            text = Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
            return true;
        }

        var utf8 = new UTF8Encoding(false, false);
        text = utf8.GetString(bytes);
        var bad = 0;
        foreach (var ch in text)
        {
            if (ch == '\uFFFD')
                bad++;
        }

        if (bad > Math.Max(24, bytes.Length / 400))
            text = Encoding.GetEncoding(1254).GetString(bytes);

        return true;
    }

    private static string[] ToStringCells(DataRow dr, int columnCount)
    {
        var n = Math.Max(columnCount, dr.ItemArray.Length);
        var arr = new string[n];
        for (var i = 0; i < n; i++)
        {
            if (i >= dr.ItemArray.Length || dr.ItemArray[i] is DBNull)
                arr[i] = "";
            else
                arr[i] = dr.ItemArray[i]?.ToString()?.Trim() ?? "";
        }

        return TrimTrailingEmpty(arr);
    }

    private static bool IsRowAllEmpty(IReadOnlyList<string> cells) =>
        cells.All(string.IsNullOrWhiteSpace);

    private static string[] TrimTrailingEmpty(string[] cells)
    {
        var len = cells.Length;
        while (len > 0 && string.IsNullOrWhiteSpace(cells[len - 1]))
            len--;
        if (len == cells.Length)
            return cells;
        var a = new string[len];
        Array.Copy(cells, a, len);
        return a;
    }
}
