namespace BimPrefabExport.Services;

public sealed class FirmTypologySettingEntry
{
    public string TypologyCode { get; set; } = "";
    public List<string> IdentifyingDimensionCodes { get; set; } = [];
    public bool? ShowInUserFilterOverride { get; set; }
}

/// <summary>Tenant firma tipoloji boyut/görünürlük override önbelleği (API).</summary>
public static class FirmTypologySettingsCache
{
    private static Dictionary<string, FirmTypologySettingEntry> _byCode =
        new(StringComparer.OrdinalIgnoreCase);

    public static void Install(IEnumerable<FirmTypologySettingEntry>? rows)
    {
        _byCode = new Dictionary<string, FirmTypologySettingEntry>(StringComparer.OrdinalIgnoreCase);
        if (rows is null)
            return;
        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.TypologyCode))
                continue;
            _byCode[row.TypologyCode.Trim()] = row;
        }
    }

    public static void Clear() => _byCode = new Dictionary<string, FirmTypologySettingEntry>(StringComparer.OrdinalIgnoreCase);

    public static FirmTypologySettingEntry? TryGet(string? typologyCode)
    {
        if (string.IsNullOrWhiteSpace(typologyCode))
            return null;
        return _byCode.TryGetValue(typologyCode.Trim(), out var row) ? row : null;
    }
}
