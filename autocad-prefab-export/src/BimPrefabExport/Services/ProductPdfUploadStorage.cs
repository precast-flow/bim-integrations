using System.IO;
using Autodesk.AutoCAD.DatabaseServices;

namespace BimPrefabExport.Services;

/// <summary>Çizim klasörü altında saklanan kullanıcı PDF yüklemeleri (plot yerine kopyalanır).</summary>
internal static class ProductPdfUploadStorage
{
    public const long MaxFileBytes = 48L * 1024 * 1024;

    /// <summary>Kaynak PDF’yi DWG yanına kopyalar; <paramref name="relativePathFromDwg"/> çizim klasörüne göredir.</summary>
    public static bool TryInstallUserPdf(
        Database db,
        Guid productId,
        Guid fenceId,
        string sourcePdfPath,
        out string relativePathFromDwg,
        out string? error)
    {
        relativePathFromDwg = "";
        error = null;
        if (string.IsNullOrWhiteSpace(sourcePdfPath) || !File.Exists(sourcePdfPath))
        {
            error = "PDF dosyası bulunamadı.";
            return false;
        }

        if (!string.Equals(Path.GetExtension(sourcePdfPath), ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            error = "Yalnızca .pdf dosyası seçilebilir.";
            return false;
        }

        long len;
        try
        {
            len = new FileInfo(sourcePdfPath).Length;
        }
        catch (Exception ex)
        {
            error = "Dosya boyutu okunamadı: " + ex.Message;
            return false;
        }

        if (len > MaxFileBytes)
        {
            error = $"PDF çok büyük (>{MaxFileBytes / 1024 / 1024} MB).";
            return false;
        }

        var dwgDir = Path.GetDirectoryName(db.Filename);
        if (string.IsNullOrEmpty(dwgDir))
        {
            error = "Çizim dosya yolu bilinmiyor (kaydedilmemiş çizim?).";
            return false;
        }

        dwgDir = Path.GetFullPath(dwgDir);
        var destDir = Path.Combine(dwgDir, "BimPrefab", "user-pdf", productId.ToString("N"));
        try
        {
            Directory.CreateDirectory(destDir);
        }
        catch (Exception ex)
        {
            error = "Hedef klasör oluşturulamadı: " + ex.Message;
            return false;
        }

        var destFile = Path.Combine(destDir, fenceId.ToString("N") + ".pdf");
        try
        {
            File.Copy(sourcePdfPath, destFile, true);
        }
        catch (Exception ex)
        {
            error = "PDF kopyalanamadı: " + ex.Message;
            return false;
        }

        relativePathFromDwg = Path.GetRelativePath(dwgDir, Path.GetFullPath(destFile));
        return true;
    }

    public static bool TryResolveStoredPdf(Database db, string? relativePathFromDwg, out string? fullPath)
    {
        fullPath = null;
        if (string.IsNullOrWhiteSpace(relativePathFromDwg))
            return false;

        var dwgDir = Path.GetDirectoryName(db.Filename);
        if (string.IsNullOrEmpty(dwgDir))
            return false;

        dwgDir = Path.GetFullPath(dwgDir);
        var candidate = Path.GetFullPath(Path.Combine(dwgDir, relativePathFromDwg));
        var rel = Path.GetRelativePath(dwgDir, candidate);
        if (rel.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(rel))
            return false;

        if (!File.Exists(candidate))
            return false;

        fullPath = candidate;
        return true;
    }
}
