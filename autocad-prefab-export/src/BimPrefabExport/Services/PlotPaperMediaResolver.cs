using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.PlottingServices;
using BimPrefabExport.Core;

namespace BimPrefabExport.Services;

/// <summary>PDF plotter ortam listesinden ISO A kağıdını seçer.</summary>
public static class PlotPaperMediaResolver
{
    public static bool TryApplyPaper(PlotSettings ps, PlotSettingsValidator psv, string? paperSize, Action<string> log)
    {
        var paper = PlotPaperSizes.NormalizeOrDefault(paperSize);

        var media = psv.GetCanonicalMediaNameList(ps);
        if (media is null || media.Count == 0)
        {
            log("Ortam listesi boş.");
            return false;
        }

        string? pick = null;
        foreach (var mObj in media)
        {
            if (mObj is not string m || string.IsNullOrWhiteSpace(m))
                continue;
            if (ScoreMatch(m, paper) >= 2)
            {
                pick = m;
                break;
            }
        }

        if (pick is null)
        {
            foreach (var mObj in media)
            {
                if (mObj is not string m || string.IsNullOrWhiteSpace(m))
                    continue;
                if (m.IndexOf(paper, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    pick = m;
                    break;
                }
            }
        }

        if (pick is null && media[0] is string first)
        {
            log($"Kağıt '{paper}' ortam listesinde bulunamadı; ilk ortam: {first}");
            pick = first;
        }

        if (pick is null)
            return false;

        psv.SetCanonicalMediaName(ps, pick);
        log($"Kağıt: {paper} → {pick}");
        return true;
    }

    /// <summary>2: güçlü eşleşme (_A3_ gibi), 1: zayıf (yalnızca A3 geçiyor).</summary>
    private static int ScoreMatch(string canonicalMediaName, string paper)
    {
        var m = canonicalMediaName;
        if (m.Contains("_" + paper + "_", StringComparison.OrdinalIgnoreCase))
            return 2;
        if (m.Contains("(" + paper, StringComparison.OrdinalIgnoreCase))
            return 2;
        if (m.Contains("_" + paper + "(", StringComparison.OrdinalIgnoreCase))
            return 2;
        if (m.IndexOf(paper, StringComparison.OrdinalIgnoreCase) >= 0)
            return 1;
        return 0;
    }
}
