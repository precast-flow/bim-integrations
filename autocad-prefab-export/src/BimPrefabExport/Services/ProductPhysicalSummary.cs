using System.Globalization;
using BimPrefabExport.Core;

namespace BimPrefabExport.Services;

/// <summary>Donatı ağırlığı ve özniteliklerden özet değerler (ürün düzenleyici özet kutusu).</summary>
internal static class ProductPhysicalSummary
{
    /// <summary>Çelik yoğunluğu kg/m³ (yapı çeliği).</summary>
    private const double SteelDensityKgPerM3 = 7850.0;

    /// <summary>Çap mm için kg/m (π·(d/2000)²·ρ).</summary>
    public static double KgPerMeterForDiameterMm(double diameterMm)
    {
        if (diameterMm <= 0)
            return 0;
        var radiusM = diameterMm / 2000.0;
        return Math.PI * radiusM * radiusM * SteelDensityKgPerM3;
    }

    /// <summary>
    /// Donatı satırlarından toplam çelik kütlesi (kg).
    /// Her satır: toplam boy (m) = adet × (H_mm + L_mm) / 1000; H veya L eksikse 0 alınır.
    /// Çap yoksa satır atlanır.
    /// </summary>
    public static double ComputeRebarSteelWeightKg(IEnumerable<RebarLine> rebars)
    {
        double sum = 0;
        foreach (var r in rebars ?? [])
        {
            if (!r.DiameterMm.HasValue || r.DiameterMm.Value <= 0)
                continue;
            if (r.Count <= 0)
                continue;

            var h = r.LengthH_mm ?? 0;
            var l = r.LengthL_mm ?? 0;
            var totalMm = r.Count * (h + l);
            if (totalMm <= 0)
                continue;

            var lengthM = totalMm / 1000.0;
            sum += KgPerMeterForDiameterMm(r.DiameterMm.Value) * lengthM;
        }

        return sum;
    }

    public static bool TryParseAttributeDouble(
        IReadOnlyDictionary<string, string> attributes,
        out double value,
        params string[] keys)
    {
        value = 0;
        foreach (var key in keys)
        {
            if (string.IsNullOrWhiteSpace(key))
                continue;
            foreach (var kv in attributes)
            {
                if (!string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (TryParseDouble(kv.Value, out value))
                    return true;
            }
        }

        return false;
    }

    private static bool TryParseDouble(string? text, out double value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text))
            return false;
        var t = text.Trim().Replace(',', '.');
        return double.TryParse(t, NumberStyles.Any, CultureInfo.InvariantCulture, out value)
               || double.TryParse(text.Trim(), NumberStyles.Any, CultureInfo.CurrentCulture, out value);
    }

    public static string FormatKg(double kg) =>
        kg.ToString("N2", CultureInfo.CurrentCulture) + " kg";

    public static string FormatM3(double m3) =>
        m3.ToString("N3", CultureInfo.CurrentCulture) + " m³";
}
