using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using BimPrefabExport.Core;

namespace BimPrefabExport.Services;

/// <summary>Ürün polyline / XData bağlantılarını ve kayıtlı çit kutularını temizler.</summary>
public static class ProductLinkClearService
{
    public static int ClearForProducts(Document doc, IReadOnlyCollection<Guid> productIds)
    {
        if (productIds.Count == 0)
            return 0;

        var set = productIds.ToHashSet();
        var xdata = new XDataService();
        var reg = new RegistryService();
        var clearedLinks = 0;

        using var docLock = doc.LockDocument();
        using (var tr = doc.Database.TransactionManager.StartTransaction())
        {
            foreach (var pid in set)
            {
                if (!reg.TryGetProduct(tr, doc.Database, pid, out var pr) || pr is null)
                    continue;

                pr.LinkFences.Clear();
                pr.PdfDrawings.Clear();
                pr.LinkFenceMinX = pr.LinkFenceMinY = pr.LinkFenceMaxX = pr.LinkFenceMaxY = null;
                reg.SaveProduct(tr, doc.Database, pr);
            }

            var msId = SymbolUtilityServices.GetBlockModelSpaceId(doc.Database);
            var ms = (BlockTableRecord)tr.GetObject(msId, OpenMode.ForRead);
            foreach (ObjectId oid in ms)
            {
                if (tr.GetObject(oid, OpenMode.ForWrite, false) is not Entity ent)
                    continue;
                if (!xdata.TryGetProductLink(ent, out var pid, out _) || !set.Contains(pid))
                    continue;
                xdata.ClearProductLink(ent);
                clearedLinks++;
            }

            tr.Commit();
        }

        return clearedLinks;
    }
}
