using Autodesk.AutoCAD.DatabaseServices;

namespace BimPrefabExport.Services;

/// <summary>
/// Yerelleştirilmiş kurulumlarda PDF plotter (.pc3) adı değişebilir; bilinen adları dener.
/// </summary>
public static class PdfPlotterResolver
{
    /// <summary>Bilinen PDF .pc3 adları (liste doldurma / yedek).</summary>
    public static IReadOnlyList<string> CandidatePdfPlotterNames => s_candidates;

    private static readonly string[] s_candidates =
    {
        "DWG To PDF.pc3",
        "DWG to PDF.pc3",
        "AutoCAD PDF (General Documentation).pc3",
        "AutoCAD PDF (High Quality Print).pc3",
        "PDF.pc3",
    };

    /// <summary>Geçerli bir PDF cihazı seçilir; yoksa null.</summary>
    public static string? TryApplyPdfPlotter(PlotSettings ps, Action<string> log)
    {
        var psv = PlotSettingsValidator.Current;

        foreach (var name in s_candidates)
        {
            try
            {
                psv.SetPlotConfigurationName(ps, name, null);
                psv.RefreshLists(ps);
                var media = psv.GetCanonicalMediaNameList(ps);
                if (media is { Count: > 0 })
                {
                    log($"PDF plotter seçildi: {name} ({media.Count} ortam; kağıt ayrı uygulanır).");
                    return name;
                }

                log($"PDF plotter atlandı (ortam listesi boş): {name}");
            }
            catch (System.Exception ex)
            {
                log($"PDF plotter denemesi başarısız ({name}): {ex.Message}");
            }
        }

        log("Hiçbir PDF .pc3 cihazı kullanılamadı. AutoCAD Plotter Manager’da PDF sürücüsü var mı kontrol edin.");
        return null;
    }
}
