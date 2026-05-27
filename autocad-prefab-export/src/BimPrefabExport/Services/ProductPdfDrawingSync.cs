using Autodesk.AutoCAD.ApplicationServices;
using BimPrefabExport.Core;

namespace BimPrefabExport.Services;

/// <summary>Çit kutuları ile <see cref="ProductPdfDrawing"/> kayıtlarını eşitler ve kök plot alanlarını günceller.</summary>
public static class ProductPdfDrawingSync
{
    public static void NormalizeProductRecord(ProductRecord p)
    {
        p.NormalizeLinkFencesFromLegacy();
        p.PdfDrawings ??= new List<ProductPdfDrawing>();

        foreach (var f in p.LinkFences.Where(x => x.IsValid()))
        {
            if (f.FenceId == Guid.Empty)
                f.FenceId = Guid.NewGuid();
        }

        var validFences = p.LinkFences.Where(f => f.IsValid())
            .OrderBy(f => f.MinX)
            .ThenBy(f => f.MinY)
            .ThenBy(f => f.MaxX)
            .ToList();

        var idSet = validFences.Select(f => f.FenceId).ToHashSet();
        p.PdfDrawings.RemoveAll(d => d.FenceId == Guid.Empty || !idSet.Contains(d.FenceId));

        var n = 0;
        foreach (var fence in validFences)
        {
            n++;
            if (p.PdfDrawings.Any(d => d.FenceId == fence.FenceId))
                continue;

            p.PdfDrawings.Add(new ProductPdfDrawing
            {
                FenceId = fence.FenceId,
                PdfTitle = $"cizim{n}",
                PlotPaperSize = PlotPaperSizes.NormalizeOrDefault(p.PlotPaperSize),
                PlotLandscape = p.PlotLandscape,
            });
        }

        foreach (var d in p.PdfDrawings)
            d.PlotPaperSize = PlotPaperSizes.NormalizeOrDefault(d.PlotPaperSize);

        var order = validFences.Select((f, i) => (f.FenceId, i)).ToDictionary(t => t.FenceId, t => t.i);
        p.PdfDrawings.Sort((a, b) =>
            order.GetValueOrDefault(a.FenceId, 999).CompareTo(order.GetValueOrDefault(b.FenceId, 999)));

        SyncRootPlotFieldsFromFirstDrawing(p);
    }

    public static void SyncRootPlotFieldsFromFirstDrawing(ProductRecord p)
    {
        var first = p.PdfDrawings.FirstOrDefault();
        if (first is null)
            return;
        p.PlotPaperSize = first.PlotPaperSize;
        p.PlotLandscape = first.PlotLandscape;
    }

    public static ProductPdfDrawing? FindDrawing(ProductRecord p, Guid fenceId) =>
        p.PdfDrawings.FirstOrDefault(d => d.FenceId == fenceId);

    public static LinkFenceBox? FindFence(ProductRecord p, Guid fenceId) =>
        p.LinkFences.FirstOrDefault(f => f.IsValid() && f.FenceId == fenceId);

    public static void TouchLastPdfExportUtc(Document doc, Guid productId, Guid fenceId)
    {
        var reg = new RegistryService();
        using var docLock = doc.LockDocument();
        using (var tr = doc.Database.TransactionManager.StartTransaction())
        {
            if (!reg.TryGetProduct(tr, doc.Database, productId, out var p) || p is null)
            {
                tr.Commit();
                return;
            }

            NormalizeProductRecord(p);
            var d = p.PdfDrawings.FirstOrDefault(x => x.FenceId == fenceId);
            if (d is not null)
                d.LastPdfExportUtc = DateTime.UtcNow;
            reg.SaveProduct(tr, doc.Database, p);
            tr.Commit();
        }
    }
}
