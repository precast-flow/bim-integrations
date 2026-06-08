using System.Globalization;

namespace BimPrefabExport.Core;

/// <summary>TS 708 uyumlu donatı birim ağırlığı — frontend rebarUnitWeights.ts ile aynı mantık.</summary>
public static class RebarWeightHelper
{
    private static readonly Dictionary<int, double> UnitWeightTableKgPerM = new()
    {
        [6] = 0.222,
        [8] = 0.395,
        [10] = 0.617,
        [12] = 0.888,
        [14] = 1.208,
        [16] = 1.578,
        [18] = 1.998,
        [20] = 2.466,
        [22] = 2.984,
        [24] = 3.551,
        [25] = 3.853,
        [26] = 4.168,
        [28] = 4.834,
        [30] = 5.549,
        [32] = 6.313,
        [36] = 7.99,
        [40] = 9.865,
        [50] = 15.432,
    };

    public static double UnitWeightKgPerM(double diameterMm)
    {
        var d = (int)Math.Round(diameterMm);
        if (UnitWeightTableKgPerM.TryGetValue(d, out var fromTable))
            return fromTable;
        if (d <= 0)
            return 0;
        return (d * d) / 162.0;
    }

    public static double ComputeDevelopedLengthMm(RebarLine row)
    {
        var l = row.LengthL_mm ?? 0;
        var h = row.LengthH_mm ?? 0;
        return l > 0 ? l : h;
    }

    public static double ComputeRowWeightKg(RebarLine row)
    {
        var diameter = row.DiameterMm ?? 0;
        var lengthMm = row.DevelopedLengthMm ?? ComputeDevelopedLengthMm(row);
        var count = row.Count > 0 ? row.Count : 1;
        var lengthM = Math.Max(0, lengthMm) / 1000.0;
        var kg = UnitWeightKgPerM(diameter) * lengthM * count;
        return Math.Round(kg, 3, MidpointRounding.AwayFromZero);
    }

    public static void NormalizeRebarRow(RebarLine row, int pozIndex)
    {
        if (string.IsNullOrWhiteSpace(row.PozNo))
            row.PozNo = pozIndex.ToString(CultureInfo.InvariantCulture);
        if (string.IsNullOrWhiteSpace(row.SteelGrade))
            row.SteelGrade = "B500C";
        if (string.IsNullOrWhiteSpace(row.Shape))
            row.Shape = "straight";
        row.DevelopedLengthMm ??= ComputeDevelopedLengthMm(row);
        row.TotalWeightKg ??= ComputeRowWeightKg(row);
    }
}
