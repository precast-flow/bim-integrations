namespace BimPrefabExport.Core;

/// <summary>Ürün başına PDF çıktı kağıdı (ISO A serisi). Palet ve plot ortam eşlemesi için.</summary>
public static class PlotPaperSizes
{
    public const string Default = "A3";

    /// <summary>Palet ComboBox sırası: varsayılan A3 ilk.</summary>
    public static readonly string[] PaletteOrder = ["A3", "A4", "A2", "A1", "A0"];

    public static bool TryNormalize(string? input, out string iso)
    {
        iso = Default;
        if (string.IsNullOrWhiteSpace(input))
            return false;
        var t = input.Trim().ToUpperInvariant();
        if (t is "A0" or "A1" or "A2" or "A3" or "A4")
        {
            iso = t;
            return true;
        }

        return false;
    }

    public static string NormalizeOrDefault(string? input) =>
        TryNormalize(input, out var p) ? p : Default;
}
