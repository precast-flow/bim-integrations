using BimPrefabExport.Schema;
using BimPrefabExport.Services;

namespace BimPrefabExport.Core;

/// <summary>Malzeme satırı ağırlık — frontend concreteRecipeWeight.ts beton hacmi mantığı.</summary>
public static class MaterialWeightHelper
{
    public static void NormalizeMaterialLine(MaterialLine line)
    {
        var catalog = MaterialCatalogService.Default.TryGetByCode(line.MaterialCatalogCode);
        line.LineWeightKg = ComputeLineWeightKg(line, catalog);
    }

    public static double ComputeLineWeightKg(MaterialLine line, MaterialCatalogEntry? catalog = null)
    {
        catalog ??= MaterialCatalogService.Default.TryGetByCode(line.MaterialCatalogCode);
        if (catalog is null || catalog.UnitWeightKgPerM3 is not double unitKgM3 || unitKgM3 <= 0)
            return 0;

        var unit = line.Unit?.Trim() ?? "";
        if (!IsVolumeUnit(unit))
            return 0;

        var vol = Math.Max(0, line.Quantity);
        return Math.Round(unitKgM3 * vol, 3, MidpointRounding.AwayFromZero);
    }

    private static bool IsVolumeUnit(string unit)
    {
        return unit.Equals("m3", StringComparison.OrdinalIgnoreCase)
               || unit.Equals("m³", StringComparison.OrdinalIgnoreCase);
    }
}
