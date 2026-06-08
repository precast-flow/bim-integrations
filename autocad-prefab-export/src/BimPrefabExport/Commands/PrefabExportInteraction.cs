using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using BimPrefabExport.Core;
using BimPrefabExport.Export;
using BimPrefabExport.Services;

namespace BimPrefabExport.Commands;

/// <summary>WPF paletten doğrudan çağrı (SendStringToExecute yerine; tekli PDF vb. güvenilir çalışır).</summary>
internal static class PrefabExportInteraction
{
    /// <summary>Önce işaretli ürünler; yoksa liste seçimi; o da yoksa tümü.</summary>
    private static List<ProductRecord> ResolveProductsForListExport(List<ProductRecord> all)
    {
        var check = PrefabUiSession.ExportCheckedProductIds;
        if (check.Count > 0)
            return all.Where(p => check.Contains(p.ProductId)).ToList();

        var sel = PrefabUiSession.SelectedProductIds;
        if (sel.Count > 0)
        {
            var filtered = all.Where(p => sel.Contains(p.ProductId)).ToList();
            if (filtered.Count > 0)
                return filtered;
        }

        return all;
    }

    private static System.Windows.Forms.IWin32Window? GetWin32Owner(System.Windows.Window? wpf)
    {
        if (wpf is not null)
        {
            var h = new WindowInteropHelper(wpf).Handle;
            if (h != IntPtr.Zero)
                return new Win32Wrap(h);
        }

        var m = AcadApp.MainWindow;
        return m is null ? null : new Win32Wrap(m.Handle);
    }

    private sealed class Win32Wrap(IntPtr handle) : System.Windows.Forms.IWin32Window
    {
        public IntPtr Handle { get; } = handle;
    }

    /// <summary>Ürünün her PDF çizim tanımı için ayrı dosya (<c>kod_{ad}.pdf</c>) üretir.</summary>
    private static int ExportAllPdfDrawingsForProduct(
        Document doc,
        Guid productId,
        string pdfDirectory,
        Editor ed,
        out string? firstError,
        IList<ExportDrawingEntry>? drawingIndex = null,
        string pdfRelativePrefix = "PDF")
    {
        firstError = null;
        var ok = 0;
        var reg = new RegistryService();
        List<Guid> fenceOrder;
        using (var tr = doc.Database.TransactionManager.StartTransaction())
        {
            if (!reg.TryGetProduct(tr, doc.Database, productId, out var probe) || probe is null)
            {
                tr.Commit();
                return 0;
            }

            ProductPdfDrawingSync.NormalizeProductRecord(probe);
            if (!PrefabExportValidation.TryValidateUniquePdfTitlesPerProduct(probe, out var titleErr))
            {
                tr.Commit();
                firstError = titleErr;
                return 0;
            }

            fenceOrder = probe.PdfDrawings.Select(d => d.FenceId).ToList();
            tr.Commit();
        }

        foreach (var fenceId in fenceOrder)
        {
            ProductRecord? p;
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                if (!reg.TryGetProduct(tr, doc.Database, productId, out p) || p is null)
                {
                    tr.Commit();
                    continue;
                }

                tr.Commit();
            }

            ProductPdfDrawingSync.NormalizeProductRecord(p);
            var spec = ProductPdfDrawingSync.FindDrawing(p, fenceId);
            if (spec is null || ProductPdfDrawingSync.FindFence(p, fenceId) is null)
                continue;

            var fn = ProductPdfExportService.BuildProductPdfFileName(p, spec);
            var path = ProductPdfExportService.EnsureUniquePdfPath(Path.Combine(pdfDirectory, fn));
            if (!ProductPdfExportService.TryExportProductPdfDrawing(doc, p, fenceId, path, out var err))
            {
                if (firstError is null)
                    firstError = err;
                ed.WriteMessage($"\n[BIM_PREFAB] PDF atlandı ({p.DisplayName} / {spec.PdfTitle}): {err}");
                continue;
            }

            ProductPdfDrawingSync.TouchLastPdfExportUtc(doc, productId, fenceId);
            ok++;
            var fileName = Path.GetFileName(path);
            drawingIndex?.Add(new ExportDrawingEntry
            {
                ProductId = p.ProductId,
                ProductCode = string.IsNullOrWhiteSpace(p.Code) ? p.DisplayName : p.Code,
                FileName = fileName,
                PdfTitle = spec.PdfTitle ?? "",
                Revision = spec.PdfRevision,
                RelativePath = $"{pdfRelativePrefix}/{fileName}".Replace('\\', '/'),
            });
            ed.WriteMessage($"\n[BIM_PREFAB] PDF: {path}");
        }

        return ok;
    }

    public static void RunPdfSingle(Document? doc, System.Windows.Window? wpfOwner)
    {
        if (doc is null)
            return;

        var ed = doc.Editor;
        if (!PrefabUiSession.SelectedProductId.HasValue)
        {
            ed.WriteMessage("\n[BIM_PREFAB] Önce paletten bir ürün seçin.");
            return;
        }

        DrawingInitService.EnsureInit(doc);
        ProductRecord? product;
        using (var tr = doc.Database.TransactionManager.StartTransaction())
        {
            if (!new RegistryService().TryGetProduct(tr, doc.Database, PrefabUiSession.SelectedProductId.Value, out product) ||
                product is null)
            {
                tr.Commit();
                ed.WriteMessage("\n[BIM_PREFAB] Ürün bulunamadı.");
                return;
            }

            tr.Commit();
        }

        ProductPdfDrawingSync.NormalizeProductRecord(product);
        if (product.PdfDrawings.Count == 0)
        {
            ed.WriteMessage("\n[BIM_PREFAB] PDF çizimi yok. Önce «Çizim sınırı seç» ile en az bir alan ekleyin.");
            return;
        }

        if (!PrefabExportValidation.TryValidateUniquePdfTitlesPerProduct(product, out var pdfErr))
        {
            if (wpfOwner is not null)
                System.Windows.MessageBox.Show(wpfOwner, pdfErr, "BIM Prefab", MessageBoxButton.OK, MessageBoxImage.Warning);
            else
                System.Windows.MessageBox.Show(pdfErr, "BIM Prefab", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        using var fbd = new FolderBrowserDialog
        {
            Description = "Seçili ürünün tüm PDF çizimleri (kod_çizimAdı.pdf) bu klasöre yazılır",
            UseDescriptionForTitle = true,
        };

        if (fbd.ShowDialog(GetWin32Owner(wpfOwner)) != DialogResult.OK || string.IsNullOrWhiteSpace(fbd.SelectedPath))
            return;

        var dir = fbd.SelectedPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var n = ExportAllPdfDrawingsForProduct(doc, product.ProductId, dir, ed, out var err0);
        if (n == 0)
        {
            ed.WriteMessage("\n[BIM_PREFAB] PDF oluşturulamadı: " + (err0 ?? "çit veya çizim kaydı eksik"));
            ed.WriteMessage($"\n[BIM_PREFAB] Tanılama logu: {BimPrefabLog.LogFilePath}");
        }
        else
        {
            ed.WriteMessage($"\n[BIM_PREFAB] {n} PDF yazıldı: {dir}");
        }
    }

    public static void RunPdfBulk(Document? doc, System.Windows.Window? wpfOwner)
    {
        if (doc is null)
            return;

        var ed = doc.Editor;
        using var fbd = new FolderBrowserDialog
        {
            Description = "Tüm ürünlerin PDF'lerini kaydetmek için klasör seçin",
            UseDescriptionForTitle = true,
        };

        if (fbd.ShowDialog(GetWin32Owner(wpfOwner)) != DialogResult.OK || string.IsNullOrWhiteSpace(fbd.SelectedPath))
            return;

        DrawingInitService.EnsureInit(doc);
        IReadOnlyList<ProductRecord> products;
        using (var tr = doc.Database.TransactionManager.StartTransaction())
        {
            products = new RegistryService().ListProducts(tr, doc.Database);
            tr.Commit();
        }

        if (products.Count == 0)
        {
            ed.WriteMessage("\n[BIM_PREFAB] Çizimde ürün yok.");
            return;
        }

        if (!PrefabExportValidation.TryValidateUniqueProductCodes(products.ToList(), out var codeErr))
        {
            if (wpfOwner is not null)
                System.Windows.MessageBox.Show(wpfOwner, codeErr, "BIM Prefab", MessageBoxButton.OK, MessageBoxImage.Warning);
            else
                System.Windows.MessageBox.Show(codeErr, "BIM Prefab", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        foreach (var p in products)
        {
            if (!PrefabExportValidation.TryValidateUniquePdfTitlesPerProduct(p, out var pdfErr))
            {
                if (wpfOwner is not null)
                    System.Windows.MessageBox.Show(wpfOwner, pdfErr, "BIM Prefab", MessageBoxButton.OK, MessageBoxImage.Warning);
                else
                    System.Windows.MessageBox.Show(pdfErr, "BIM Prefab", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        var ok = 0;
        var fail = 0;
        var pdfRoot = fbd.SelectedPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        foreach (var p in products)
        {
            var n = ExportAllPdfDrawingsForProduct(doc, p.ProductId, pdfRoot, ed, out var err);
            if (n > 0)
                ok += n;
            else
            {
                fail++;
                ed.WriteMessage($"\n[BIM_PREFAB] Atlandı ({p.DisplayName}): {err ?? "PDF çizimi yok veya hata"}");
            }
        }

        ed.WriteMessage($"\n[BIM_PREFAB] PDF özeti: {ok} dosya, {fail} ürün atlandı.");
        if (fail > 0)
            ed.WriteMessage($"\n[BIM_PREFAB] Tanılama logu: {BimPrefabLog.LogFilePath}");
    }

    public static void RunCsvExport(Document? doc, System.Windows.Window? wpfOwner)
    {
        if (doc is null)
            return;

        var ed = doc.Editor;
        using var dlg = new SaveFileDialog
        {
            Filter = "CSV (*.csv)|*.csv|Tüm dosyalar (*.*)|*.*",
            FileName = "urun-listesi.csv",
            Title = "Liste dışa aktar (CSV)",
            OverwritePrompt = true,
        };

        if (dlg.ShowDialog(GetWin32Owner(wpfOwner)) != DialogResult.OK)
            return;

        DrawingInitService.EnsureInit(doc);
        List<ProductRecord> allProducts;
        using (var tr = doc.Database.TransactionManager.StartTransaction())
        {
            allProducts = new RegistryService().ListProducts(tr, doc.Database).ToList();
            tr.Commit();
        }

        var products = ResolveProductsForListExport(allProducts);

        if (products.Count == 0)
        {
            ed.WriteMessage("\n[BIM_PREFAB] Çizimde dışa aktarılacak ürün yok.");
            return;
        }

        try
        {
            var summary = new DocumentSummary
            {
                FileName = doc.Name,
                DatabasePath = doc.Database.Filename,
            };
            var path = dlg.FileName;
            if (!path.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                path += ".csv";

            var dir = Path.GetDirectoryName(Path.GetFullPath(path)) ?? "";
            var stem = Path.GetFileNameWithoutExtension(path);
            var baseNoExt = Path.Combine(dir, stem);

            var csvResult = CsvListExportBuilder.Write(baseNoExt, summary, products);
            ed.WriteMessage($"\n[BIM_PREFAB] CSV yazıldı ({products.Count} ürün):");
            ed.WriteMessage($"\n  • {csvResult.ProductsPath}");
            ed.WriteMessage($"\n  • {csvResult.MaterialsPath}");
            ed.WriteMessage($"\n  • {csvResult.RebarsPath}");
            ed.WriteMessage($"\n  • {csvResult.DrawingsPath}");
        }
        catch (System.Exception ex)
        {
            ed.WriteMessage("\n[BIM_PREFAB] CSV hatası: " + ex.Message);
        }
    }

    /// <summary>
    /// İşaretli ürünler için: üst klasörde CSV (ürünler + malzemeler); alt klasör <c>PDF</c> içinde PDF’ler.
    /// </summary>
    public static void RunExportBundle(Document? doc, System.Windows.Window? wpfOwner)
    {
        if (doc is null)
            return;

        var ed = doc.Editor;
        using var fbd = new FolderBrowserDialog
        {
            Description = "Paket klasörünün oluşturulacağı üst klasörü seçin (içinde yeni bir alt klasör açılır)",
            UseDescriptionForTitle = true,
        };

        if (fbd.ShowDialog(GetWin32Owner(wpfOwner)) != DialogResult.OK || string.IsNullOrWhiteSpace(fbd.SelectedPath))
            return;

        DrawingInitService.EnsureInit(doc);
        List<ProductRecord> allProducts;
        using (var tr = doc.Database.TransactionManager.StartTransaction())
        {
            allProducts = new RegistryService().ListProducts(tr, doc.Database).ToList();
            tr.Commit();
        }

        var check = PrefabUiSession.ExportCheckedProductIds.ToHashSet();
        var products = check.Count > 0
            ? allProducts.Where(p => check.Contains(p.ProductId)).ToList()
            : allProducts;
        if (check.Count == 0)
            ed.WriteMessage("\n[BIM_PREFAB] Paket: paletten işaret yok; tüm ürünler dahil ediliyor (komut satırı).");

        if (products.Count == 0)
        {
            ed.WriteMessage("\n[BIM_PREFAB] Çizimde paketlenecek ürün yok.");
            return;
        }

        if (!PrefabExportValidation.TryValidateBundle(products, out var bundleErr))
        {
            if (wpfOwner is not null)
                System.Windows.MessageBox.Show(wpfOwner, bundleErr ?? "", "BIM Prefab", MessageBoxButton.OK, MessageBoxImage.Warning);
            else
                ed.WriteMessage("\n[BIM_PREFAB] Paket iptal: " + (bundleErr ?? "").Replace("\n", " ", StringComparison.Ordinal));
            return;
        }

        var stem = ProductPdfExportService.SanitizeFileName(Path.GetFileNameWithoutExtension(doc.Name));
        if (string.IsNullOrWhiteSpace(stem))
            stem = "cizim";

        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        var root = Path.Combine(fbd.SelectedPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            $"{stem}_BimPrefab_{stamp}");

        try
        {
            Directory.CreateDirectory(root);
            var pdfDir = Path.Combine(root, "PDF");
            Directory.CreateDirectory(pdfDir);

            var summary = new DocumentSummary
            {
                FileName = doc.Name,
                DatabasePath = doc.Database.Filename,
            };

            var baseNoExt = Path.Combine(root, stem);
            var drawingIndex = new List<ExportDrawingEntry>();

            var ok = 0;
            var fail = 0;
            foreach (var p in products)
            {
                var n = ExportAllPdfDrawingsForProduct(doc, p.ProductId, pdfDir, ed, out var err, drawingIndex, "PDF");
                if (n > 0)
                    ok += n;
                else
                {
                    fail++;
                    ed.WriteMessage($"\n[BIM_PREFAB] PDF atlandı ({p.DisplayName}): {err ?? "PDF çizimi yok veya hata"}");
                }
            }

            var csvResult = CsvListExportBuilder.Write(baseNoExt, summary, products, drawingIndex);
            var manifestPath = Path.Combine(root, "manifest.json");
            ManifestBuilder.WriteManifestJson(manifestPath, summary, products, drawingIndex);

            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                var reg = new RegistryService();
                foreach (var p in products)
                {
                    if (!reg.TryGetProduct(tr, doc.Database, p.ProductId, out var stored) || stored is null)
                        continue;
                    stored.LastExportedContentHash = p.LastExportedContentHash;
                    stored.LastExportUtc = p.LastExportUtc;
                    reg.SaveProduct(tr, doc.Database, stored);
                }

                tr.Commit();
            }

            ed.WriteMessage($"\n[BIM_PREFAB] Paket klasörü: {root}");
            ed.WriteMessage($"\n  • {manifestPath}");
            ed.WriteMessage($"\n  • {csvResult.ProductsPath}");
            ed.WriteMessage($"\n  • {csvResult.MaterialsPath}");
            ed.WriteMessage($"\n  • {csvResult.RebarsPath}");
            ed.WriteMessage($"\n  • {csvResult.DrawingsPath}");
            ed.WriteMessage($"\n[BIM_PREFAB] PDF alt klasörü: {pdfDir} ({ok} dosya, {fail} ürün atlandı).");

            IReadOnlyList<SharedDrawingEntry> sharedList;
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                sharedList = SharedDrawingsRegistry.Load(tr, doc.Database).Drawings;
                tr.Commit();
            }

            var sOk = 0;
            var sFail = 0;
            if (sharedList.Any(s => s.LinkFences.Any(f => f.IsValid())))
            {
                var commonDir = Path.Combine(root, "OrtakCizimler_PDF");
                Directory.CreateDirectory(commonDir);
                foreach (var se in sharedList)
                {
                    if (!se.LinkFences.Any(f => f.IsValid()))
                        continue;
                    var baseName = ProductPdfExportService.SanitizeFileName(se.DisplayName) + ".pdf";
                    var path = ProductPdfExportService.EnsureUniquePdfPath(Path.Combine(commonDir, baseName));
                    if (ProductPdfExportService.TryExportSharedDrawingFence(doc, se, path, out var sErr))
                    {
                        sOk++;
                        ed.WriteMessage($"\n[BIM_PREFAB] Ortak PDF: {path}");
                    }
                    else
                    {
                        sFail++;
                        ed.WriteMessage($"\n[BIM_PREFAB] Ortak PDF atlandı ({se.DisplayName}): {sErr}");
                    }
                }

                ed.WriteMessage($"\n[BIM_PREFAB] Ortak çizim PDF klasörü: {commonDir} ({sOk} tamam, {sFail} hata).");
            }

            if (fail > 0 || sFail > 0)
                ed.WriteMessage($"\n[BIM_PREFAB] Tanılama logu: {BimPrefabLog.LogFilePath}");
        }
        catch (System.Exception ex)
        {
            ed.WriteMessage("\n[BIM_PREFAB] Paket hatası: " + ex.Message);
        }
    }

    /// <summary>Ortak çizimleri (çit tanımlı olanlar) seçilen klasöre ayrı PDF olarak yazar.</summary>
    public static void RunSharedDrawingsPdfBulk(Document? doc, System.Windows.Window? wpfOwner)
    {
        if (doc is null)
            return;

        var ed = doc.Editor;
        using var fbd = new FolderBrowserDialog
        {
            Description = "Ortak çizim PDF’lerini kaydetmek için klasör seçin",
            UseDescriptionForTitle = true,
        };

        if (fbd.ShowDialog(GetWin32Owner(wpfOwner)) != DialogResult.OK || string.IsNullOrWhiteSpace(fbd.SelectedPath))
            return;

        DrawingInitService.EnsureInit(doc);
        IReadOnlyList<SharedDrawingEntry> sharedList;
        using (var tr = doc.Database.TransactionManager.StartTransaction())
        {
            sharedList = SharedDrawingsRegistry.Load(tr, doc.Database).Drawings;
            tr.Commit();
        }

        if (sharedList.Count == 0)
        {
            ed.WriteMessage("\n[BIM_PREFAB] Ortak çizim listesi boş (Genel ve detay çizimler penceresinden ekleyin).");
            return;
        }

        var ok = 0;
        var fail = 0;
        var skip = 0;
        foreach (var se in sharedList)
        {
            if (!se.LinkFences.Any(f => f.IsValid()))
            {
                skip++;
                continue;
            }

            var baseName = ProductPdfExportService.SanitizeFileName(se.DisplayName) + ".pdf";
            var path = ProductPdfExportService.EnsureUniquePdfPath(Path.Combine(fbd.SelectedPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), baseName));
            if (ProductPdfExportService.TryExportSharedDrawingFence(doc, se, path, out var err))
            {
                ok++;
                ed.WriteMessage($"\n[BIM_PREFAB] Ortak PDF: {path}");
            }
            else
            {
                fail++;
                ed.WriteMessage($"\n[BIM_PREFAB] Ortak PDF atlandı ({se.DisplayName}): {err}");
            }
        }

        ed.WriteMessage($"\n[BIM_PREFAB] Ortak çizim PDF özeti: {ok} tamam, {fail} hata, {skip} çitsiz atlandı.");
        if (fail > 0)
            ed.WriteMessage($"\n[BIM_PREFAB] Tanılama logu: {BimPrefabLog.LogFilePath}");
    }
}
