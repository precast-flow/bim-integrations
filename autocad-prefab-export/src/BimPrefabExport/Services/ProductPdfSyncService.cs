using System.IO;
using Autodesk.AutoCAD.ApplicationServices;
using BimPrefabExport.Core;

namespace BimPrefabExport.Services;

public sealed class ProductPdfUploadResult
{
    public Guid FenceId { get; init; }
    public Guid FileId { get; init; }
    public string PdfUrl { get; init; } = "";
    public string FileName { get; init; } = "";
    public string Title { get; init; } = "";
    public string Revision { get; init; } = "";
}

/// <summary>Plot / harici PDF dışa aktarma ve sunucuya yükleme.</summary>
public static class ProductPdfSyncService
{
    public static bool TryExportDrawingToStaging(
        Document doc,
        ProductRecord record,
        ProductPdfDrawing drawing,
        out string fullPath,
        out string? error)
    {
        fullPath = "";
        error = null;
        ProductPdfDrawingSync.NormalizeProductRecord(record);

        var dwgDir = Path.GetDirectoryName(doc.Database.Filename);
        if (string.IsNullOrEmpty(dwgDir))
        {
            error = "Çizim kaydedilmemiş; PDF dışa aktarılamıyor.";
            return false;
        }

        dwgDir = Path.GetFullPath(dwgDir);
        var stagingDir = Path.Combine(dwgDir, "BimPrefab", "staging", record.ProductId.ToString("N"));
        try
        {
            Directory.CreateDirectory(stagingDir);
        }
        catch (Exception ex)
        {
            error = "PDF hazırlık klasörü oluşturulamadı: " + ex.Message;
            return false;
        }

        var fileName = ProductPdfExportService.BuildProductPdfFileName(record, drawing);
        fullPath = Path.Combine(stagingDir, fileName);
        if (!ProductPdfExportService.TryExportProductPdfDrawing(doc, record, drawing.FenceId, fullPath, out error))
            return false;

        if (!File.Exists(fullPath) || new FileInfo(fullPath).Length <= 0)
        {
            error = "PDF oluşturuldu ancak dosya boş veya okunamıyor.";
            return false;
        }

        ProductPdfDrawingSync.TouchLastPdfExportUtc(doc, record.ProductId, drawing.FenceId);
        return true;
    }

    public static async Task<IReadOnlyList<ProductPdfUploadResult>> ExportAndUploadAllDrawingsAsync(
        Document doc,
        PrecastApiClient client,
        Guid projectId,
        Guid serverProductId,
        ProductRecord record,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var results = new List<ProductPdfUploadResult>();
        if (record.PdfDrawings is not { Count: > 0 })
            return results;

        ProductPdfDrawingSync.NormalizeProductRecord(record);
        foreach (var drawing in record.PdfDrawings)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (ProductPdfDrawingSync.FindFence(record, drawing.FenceId) is null)
                continue;

            var title = string.IsNullOrWhiteSpace(drawing.PdfTitle) ? "cizim" : drawing.PdfTitle.Trim();
            progress?.Report($"PDF hazırlanıyor: {title}…");

            if (!TryExportDrawingToStaging(doc, record, drawing, out var path, out var exportError))
            {
                BimPrefabLog.Info($"PDF dışa aktarma atlandı ({title}): {exportError}");
                continue;
            }

            progress?.Report($"PDF yükleniyor: {title}…");
            try
            {
                var upload = await client.UploadProductPdfAsync(
                    projectId,
                    serverProductId,
                    path,
                    title,
                    cancellationToken).ConfigureAwait(false);

                if (string.IsNullOrWhiteSpace(upload.Url))
                {
                    BimPrefabLog.Info($"PDF yükleme yanıtında URL yok ({title}).");
                    continue;
                }

                var rev = drawing.PdfRevision > 0 ? $"R{drawing.PdfRevision}" : "R1";
                results.Add(new ProductPdfUploadResult
                {
                    FenceId = drawing.FenceId,
                    FileId = upload.Id,
                    PdfUrl = upload.Url.Trim(),
                    FileName = Path.GetFileName(path),
                    Title = title,
                    Revision = rev,
                });
                BimPrefabLog.Info($"PDF yüklendi: {title} → {upload.Url}");
            }
            catch (Exception ex)
            {
                BimPrefabLog.Info($"PDF yükleme hatası ({title}): {ex.Message}");
            }
        }

        return results;
    }
}
