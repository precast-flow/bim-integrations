using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.PlottingServices;
using BimPrefabExport.Core;

namespace BimPrefabExport.Services;

/// <summary>
/// Ürün PDF’i: model uzayında ürün sınırına göre <see cref="PlotType.Window"/> ile doğrudan plot (ayrı kağıt yerleşimi yok).
/// </summary>
public static class ProductPdfExportService
{
    private static void Log(string msg) => BimPrefabLog.Info(msg);

    public static string SanitizeFileName(string displayName)
    {
        var name = displayName.Trim();
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        if (string.IsNullOrWhiteSpace(name))
            name = "urun";
        return name;
    }

    public static string EnsureUniquePdfPath(string fullPath)
    {
        if (!fullPath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            fullPath += ".pdf";
        if (!File.Exists(fullPath))
            return fullPath;

        var dir = Path.GetDirectoryName(fullPath) ?? "";
        var name = Path.GetFileNameWithoutExtension(fullPath);
        for (var i = 2; i < 1000; i++)
        {
            var p = Path.Combine(dir, $"{name} ({i}).pdf");
            if (!File.Exists(p))
                return p;
        }

        return fullPath;
    }

    /// <summary>Ortak çizim: yalnızca kayıtlı çit kutuları ile model penceresi PDF (XData gerekmez).</summary>
    public static bool TryExportSharedDrawingFence(Document doc, SharedDrawingEntry entry, string outputPdfPath,
        out string? error)
    {
        var fences = (entry.LinkFences ?? []).Where(f => f.IsValid()).ToList();
        if (fences.Count == 0)
        {
            error = "Bu çizim için kayıtlı sınır yok.";
            return false;
        }

        var syn = new ProductRecord
        {
            ProductId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            DisplayName = entry.DisplayName,
            LinkFences = fences,
            PlotPaperSize = PlotPaperSizes.NormalizeOrDefault(entry.PlotPaperSize),
            PlotLandscape = entry.PlotLandscape,
        };

        return TryExportProduct(doc, syn, outputPdfPath, out error);
    }

    /// <summary>Ürünün tek bir çit alanı için PDF (dosya adı genelde <c>kod_{pdfTitle}.pdf</c>).</summary>
    public static bool TryExportProductPdfDrawing(
        Document doc,
        ProductRecord product,
        Guid fenceId,
        string outputPdfPath,
        out string? error)
    {
        error = null;
        ProductPdfDrawingSync.NormalizeProductRecord(product);
        var spec = ProductPdfDrawingSync.FindDrawing(product, fenceId);
        var fence = ProductPdfDrawingSync.FindFence(product, fenceId);
        if (spec is null || fence is null)
        {
            error = "PDF çizimi veya çit kaydı bulunamadı.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(spec.UploadedPdfRelativePath))
        {
            if (!ProductPdfUploadStorage.TryResolveStoredPdf(doc.Database, spec.UploadedPdfRelativePath, out var src) ||
                src is null)
            {
                error =
                    "Yüklenmiş PDF bulunamadı (dosya silinmiş veya çizim taşınmış olabilir). Paletten yeniden yükleyin veya yolu kontrol edin.";
                return false;
            }

            try
            {
                if (!outputPdfPath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                    outputPdfPath += ".pdf";
                var dir = Path.GetDirectoryName(Path.GetFullPath(outputPdfPath));
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);
                File.Copy(src, outputPdfPath, true);
                Log($"PDF: yüklenmiş dosya kopyalandı → {outputPdfPath}");
                return true;
            }
            catch (Exception ex)
            {
                error = "Yüklenmiş PDF hedefe kopyalanamadı: " + ex.Message;
                Log($"EXPORT kopya hata: {ex}");
                return false;
            }
        }

        var clone = new ProductRecord
        {
            ProductId = product.ProductId,
            DisplayName = product.DisplayName,
            Code = product.Code,
            LinkFences = new List<LinkFenceBox>
            {
                new()
                {
                    FenceId = fence.FenceId,
                    MinX = fence.MinX,
                    MinY = fence.MinY,
                    MaxX = fence.MaxX,
                    MaxY = fence.MaxY,
                },
            },
            PlotPaperSize = PlotPaperSizes.NormalizeOrDefault(spec.PlotPaperSize),
            PlotLandscape = spec.PlotLandscape,
        };

        return TryExportProduct(doc, clone, outputPdfPath, out error);
    }

    /// <summary>Çıktı kök adı: <c>kod_çizimAdı.pdf</c>; <see cref="ProductPdfDrawing.PdfRevision"/> &gt; 0 ise <c>_r{n}</c> eklenir.</summary>
    public static string BuildProductPdfFileName(ProductRecord product, ProductPdfDrawing spec)
    {
        var codeSrc = string.IsNullOrWhiteSpace(product.Code) ? product.DisplayName : product.Code;
        var codePart = SanitizeFileName(codeSrc);
        if (string.IsNullOrWhiteSpace(codePart))
            codePart = "urun";
        var titlePart = string.IsNullOrWhiteSpace(spec.PdfTitle) ? "cizim" : SanitizeFileName(spec.PdfTitle);
        var rev = spec.PdfRevision;
        var revPart = rev > 0 ? $"_r{rev}" : "";
        return $"{codePart}_{titlePart}{revPart}.pdf";
    }

    public static bool TryExportProduct(Document doc, ProductRecord product, string outputPdfPath, out string? error)
    {
        error = null;
        if (!outputPdfPath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            outputPdfPath += ".pdf";

        var dir = Path.GetDirectoryName(Path.GetFullPath(outputPdfPath));
        if (!string.IsNullOrEmpty(dir))
        {
            try
            {
                Directory.CreateDirectory(dir);
            }
            catch (System.Exception ex)
            {
                error = "Klasör oluşturulamadı: " + ex.Message;
                Log($"EXPORT HATA: {error}");
                return false;
            }
        }

        Log($"PDF export başlıyor: ürün={product.DisplayName} ({product.ProductId:D}), hedef={outputPdfPath}");

        try
        {
            using var docLock = doc.LockDocument();
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                if (!ProductModelPlotExtentsService.TryGetModelSpacePlotWindow(
                        tr,
                        doc.Database,
                        product,
                        out var winExt,
                        out var modelLid))
                {
                    error =
                        "Modelde bu ürüne bağlı nesne yok ve polyline çit kaydı yok. Önce «Çizim sınırı seç» ile bağlayın.";
                    Log($"EXPORT iptal: {error}");
                    tr.Commit();
                    return false;
                }

                tr.Commit();
                Log("PDF: model pencere plot.");
                return PlotModelWindowToPdf(doc, modelLid, winExt, product, outputPdfPath, out error);
            }
        }
        catch (System.Exception ex)
        {
            error = ex.Message;
            Log($"EXPORT istisna: {ex}");
            return false;
        }
    }

    private static bool PlotModelWindowToPdf(
        Document doc,
        ObjectId modelLayoutId,
        Extents3d ext,
        ProductRecord product,
        string outputPdfPath,
        out string? error)
    {
        error = null;
        var db = doc.Database;

        using (var tr = db.TransactionManager.StartTransaction())
        {
            var layout = (Layout)tr.GetObject(modelLayoutId, OpenMode.ForRead);
            var ps = new PlotSettings(layout.ModelType);
            ps.CopyFrom(layout);

            var psv = PlotSettingsValidator.Current;
            if (PdfPlotterResolver.TryApplyPdfPlotter(ps, Log) is null)
            {
                error = "PDF plotter (.pc3) seçilemedi veya ortam listesi boş.";
                tr.Commit();
                return false;
            }

            if (!PlotPaperMediaResolver.TryApplyPaper(ps, psv, product.PlotPaperSize, Log))
            {
                error = "PDF kağıt ortamı seçilemedi.";
                tr.Commit();
                return false;
            }

            try
            {
                psv.SetPlotRotation(ps, product.PlotLandscape ? PlotRotation.Degrees090 : PlotRotation.Degrees000);
            }
            catch (System.Exception exRot)
            {
                Log($"PlotRotation ayarı atlandı: {exRot.Message}");
            }

            var win2d = new Extents2d(
                new Point2d(ext.MinPoint.X, ext.MinPoint.Y),
                new Point2d(ext.MaxPoint.X, ext.MaxPoint.Y));

            psv.SetPlotWindowArea(ps, win2d);
            psv.SetPlotType(ps, Autodesk.AutoCAD.DatabaseServices.PlotType.Window);

            try
            {
                psv.SetUseStandardScale(ps, true);
                psv.SetStdScaleType(ps, StdScaleType.ScaleToFit);
            }
            catch (System.Exception ex)
            {
                Log($"ScaleToFit ayarı: {ex.Message}");
                try
                {
                    psv.SetUseStandardScale(ps, true);
                    psv.SetStdScaleType(ps, StdScaleType.ScaleToFit);
                }
                catch
                {
                    // yoksay
                }
            }

            try
            {
                psv.SetPlotCentered(ps, true);
            }
            catch (System.Exception ex)
            {
                Log($"Plot ortalanamadı (PlotCentered): {ex.Message}");
            }

            var pi = new PlotInfo
            {
                Layout = modelLayoutId,
                OverrideSettings = ps,
            };

            var piv = new PlotInfoValidator { MediaMatchingPolicy = MatchingPolicy.MatchEnabled };
            try
            {
                piv.Validate(pi);
            }
            catch (System.Exception ex)
            {
                error = "Plot doğrulama hatası: " + ex.Message;
                Log($"PlotInfoValidator (model pencere): {ex}");
                tr.Commit();
                return false;
            }

            tr.Commit();
            if (!ExecutePlot(doc, pi, outputPdfPath, out error))
                return false;
        }

        return true;
    }

    private static bool ExecutePlot(Document doc, PlotInfo pi, string outputPdfPath, out string? error)
    {
        error = null;

        var wait = Stopwatch.StartNew();
        while (PlotFactory.ProcessPlotState != ProcessPlotState.NotPlotting && wait.ElapsedMilliseconds < 120_000)
            Thread.Sleep(150);

        if (PlotFactory.ProcessPlotState != ProcessPlotState.NotPlotting)
        {
            error = "Başka bir plot işlemi sürüyor (zaman aşımı).";
            Log(error);
            return false;
        }

        var finalPath = Path.GetFullPath(outputPdfPath);
        var tempPlot = Path.Combine(Path.GetTempPath(), "bimpf-" + Guid.NewGuid().ToString("N") + ".pdf");

        object? oldBackplot = null;
        try
        {
            oldBackplot = Autodesk.AutoCAD.ApplicationServices.Core.Application.GetSystemVariable("BACKGROUNDPLOT");
            Autodesk.AutoCAD.ApplicationServices.Core.Application.SetSystemVariable("BACKGROUNDPLOT", 0);
        }
        catch (System.Exception ex)
        {
            Log($"BACKGROUNDPLOT ayarı atlandı: {ex.Message}");
        }

        Log($"PlotEngine başlıyor, geçici çıktı={tempPlot}, hedef={finalPath}");

        using var pe = PlotFactory.CreatePublishEngine();
        using var ppd = new PlotProgressDialog(false, 1, true);
        try
        {
            pe.BeginPlot(ppd, null);
            pe.BeginDocument(pi, doc.Name, null, 1, true, tempPlot);

            var ppi = new PlotPageInfo();
            pe.BeginPage(ppi, pi, true, null);
            pe.BeginGenerateGraphics(null);
            pe.EndGenerateGraphics(null);
            pe.EndPage(null);
            pe.EndDocument(null);
            pe.EndPlot(null);
        }
        catch (System.Exception ex)
        {
            error = "Plot motoru: " + ex.Message;
            Log($"PlotEngine istisna: {ex}");
            TryRestoreBackplot(oldBackplot);
            TryDelete(tempPlot);
            return false;
        }

        TryRestoreBackplot(oldBackplot);

        if (!File.Exists(tempPlot))
        {
            error = "Plot tamamlandı ancak geçici PDF oluşmadı. Ayrıntı: " + BimPrefabLog.LogFilePath;
            Log(error);
            return false;
        }

        try
        {
            var outDir = Path.GetDirectoryName(finalPath);
            if (!string.IsNullOrEmpty(outDir))
                Directory.CreateDirectory(outDir);
            File.Copy(tempPlot, finalPath, true);
        }
        catch (System.Exception ex)
        {
            error = "PDF hedefe kopyalanamadı: " + ex.Message;
            Log(error + $" (geçici: {tempPlot})");
            return false;
        }
        finally
        {
            TryDelete(tempPlot);
        }

        if (File.Exists(finalPath))
        {
            Log($"PDF oluştu: {finalPath} ({new FileInfo(finalPath).Length} bayt)");
            return true;
        }

        error = "PDF kopyası doğrulanamadı: " + finalPath;
        Log(error);
        return false;
    }

    private static void TryRestoreBackplot(object? oldValue)
    {
        if (oldValue is null)
            return;
        try
        {
            Autodesk.AutoCAD.ApplicationServices.Core.Application.SetSystemVariable("BACKGROUNDPLOT", oldValue);
        }
        catch
        {
            // yoksay
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // yoksay
        }
    }
}
