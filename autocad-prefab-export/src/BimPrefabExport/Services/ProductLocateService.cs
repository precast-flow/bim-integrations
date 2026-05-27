using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using BimPrefabExport.Core;

namespace BimPrefabExport.Services;

/// <summary>
/// Seçili ürüne bağlı nesneleri bulur, yerleşimi açar, seçer ve görünümü yakınlaştırır.
/// </summary>
public static class ProductLocateService
{
    public static bool TryShowProduct(Document doc, Guid productId, out string? userMessage)
    {
        userMessage = null;
        using var docLock = doc.LockDocument();
        using (var tr = doc.Database.TransactionManager.StartTransaction())
        {
            if (!TryBuildShowContext(tr, doc.Database, productId, out var layoutName, out var ids, out var ext))
            {
                userMessage = "Bu ürüne XData ile bağlı nesne yok (Model ve tüm yerleşimler tarandı).";
                tr.Commit();
                return false;
            }

            tr.Commit();

            LayoutManager.Current.CurrentLayout = layoutName;
            var ed = doc.Editor;
            ed.SetImpliedSelection(ids);
            ZoomToExtents(ed, ext);
            userMessage = $"{ids.Length} nesne seçildi; yerleşim: {layoutName}";
            return true;
        }
    }

    private static bool TryBuildShowContext(
        Transaction tr,
        Database db,
        Guid productId,
        out string layoutName,
        out ObjectId[] ids,
        out Extents3d ext)
    {
        layoutName = "Model";
        ids = [];
        ext = new Extents3d();

        var xdata = new XDataService();
        var byLayout = new Dictionary<string, List<ObjectId>>(StringComparer.OrdinalIgnoreCase);
        var extentsByLayout = new Dictionary<string, Extents3d>(StringComparer.OrdinalIgnoreCase);

        void add(string lay, ObjectId oid, Entity ent)
        {
            if (!byLayout.TryGetValue(lay, out var list))
            {
                list = [];
                byLayout[lay] = list;
            }

            list.Add(oid);
            try
            {
                var e = ent.GeometricExtents;
                if (!extentsByLayout.TryGetValue(lay, out var ex))
                {
                    extentsByLayout[lay] = e;
                }
                else
                {
                    ex.AddExtents(e);
                    extentsByLayout[lay] = ex;
                }
            }
            catch
            {
                // atla
            }
        }

        void scanBtr(BlockTableRecord btr, string layName)
        {
            foreach (ObjectId oid in btr)
            {
                if (tr.GetObject(oid, OpenMode.ForRead, false) is not Entity ent)
                    continue;

                if (!xdata.TryGetProductLink(ent, out var pid, out _) || pid != productId)
                    continue;

                add(layName, oid, ent);
            }
        }

        var msId = SymbolUtilityServices.GetBlockModelSpaceId(db);
        var ms = (BlockTableRecord)tr.GetObject(msId, OpenMode.ForRead);
        var modelLo = (Layout)tr.GetObject(ms.LayoutId, OpenMode.ForRead);
        scanBtr(ms, modelLo.LayoutName);

        var ld = (DBDictionary)tr.GetObject(db.LayoutDictionaryId, OpenMode.ForRead);
        foreach (var entry in ld)
        {
            var lo = (Layout)tr.GetObject(ld.GetAt(entry.Key), OpenMode.ForRead);
            if (string.Equals(lo.LayoutName, "Model", StringComparison.OrdinalIgnoreCase))
                continue;

            var btr = (BlockTableRecord)tr.GetObject(lo.BlockTableRecordId, OpenMode.ForRead);
            scanBtr(btr, lo.LayoutName);
        }

        if (byLayout.Count == 0)
            return false;

        // En çok nesnenin olduğu yerleşimi göster
        var best = byLayout.OrderByDescending(kv => kv.Value.Count).First();
        layoutName = best.Key;
        ids = best.Value.ToArray();
        if (!extentsByLayout.TryGetValue(layoutName, out ext))
            ext = new Extents3d(Point3d.Origin, new Point3d(100, 100, 0));
        return true;
    }

    private static void ZoomToExtents(Editor ed, Extents3d ext)
    {
        var min = ext.MinPoint;
        var max = ext.MaxPoint;
        var cx = (min.X + max.X) * 0.5;
        var cy = (min.Y + max.Y) * 0.5;
        var w = Math.Max(max.X - min.X, 1e-3) * 1.15;
        var h = Math.Max(max.Y - min.Y, 1e-3) * 1.15;

        var view = ed.GetCurrentView();
        view.CenterPoint = new Point2d(cx, cy);
        view.Width = w;
        view.Height = h;
        ed.SetCurrentView(view);
    }
}
