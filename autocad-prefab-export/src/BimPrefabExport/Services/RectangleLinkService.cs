using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using BimPrefabExport.Core;

namespace BimPrefabExport.Services;

public static class RectangleLinkService
{
    /// <summary>
    /// Tek kapalı polyline: kesişen nesneleri ürüne bağlar; ürün çit alanı yalnızca bu polyline ile tek kayıt olur.
    /// </summary>
    public static int AssignByClosedPolyline(Document doc, Guid productId, string? role, out string? error)
    {
        error = null;
        var ed = doc.Editor;
        var peo = new PromptEntityOptions("\nSınır polyline seçin (kapalı, tek alan): ")
        {
            AllowObjectOnLockedLayer = false,
        };
        peo.SetRejectMessage("\nYalnızca Polyline.");
        peo.AddAllowedClass(typeof(Polyline), true);

        var per = ed.GetEntity(peo);
        if (per.Status != PromptStatus.OK)
        {
            error = "İptal.";
            return 0;
        }

        if (!TryProcessOnePolyline(doc, productId, role, per, out error, out var countOne))
            return 0;

        ed.WriteMessage($"\n[BIM_PREFAB] {countOne} nesne bağlandı; çizim sınırı listeye eklendi (birden fazla alan mümkün).");
        return countOne;
    }

    private static bool TryProcessOnePolyline(
        Document doc,
        Guid productId,
        string? role,
        PromptEntityResult per,
        out string? error,
        out int count)
    {
        error = null;
        count = 0;
        var boundaryId = per.ObjectId;
        Point3dCollection fence;
        Extents3d fenceExtents;

        using (var tr = doc.Database.TransactionManager.StartTransaction())
        {
            if (tr.GetObject(per.ObjectId, OpenMode.ForRead) is not Polyline pl)
            {
                tr.Commit();
                error = "Polyline okunamadı.";
                return false;
            }

            if (!pl.Closed)
            {
                tr.Commit();
                error = "Polyline kapalı değil.";
                return false;
            }

            var n = pl.NumberOfVertices;
            if (n < 3)
            {
                tr.Commit();
                error = "En az 3 köşe gerekli.";
                return false;
            }

            fence = new Point3dCollection();
            for (int i = 0; i < n; i++)
                fence.Add(pl.GetPoint3dAt(i));

            try
            {
                fenceExtents = pl.GeometricExtents;
            }
            catch
            {
                fenceExtents = ExtentsFromPoints(fence);
            }

            tr.Commit();
        }

        var sel = doc.Editor.SelectCrossingPolygon(fence);
        if (sel.Status != PromptStatus.OK || sel.Value is null || sel.Value.Count == 0)
        {
            error = "Çit ile kesişen nesne yok.";
            return false;
        }

        var ids = sel.Value.GetObjectIds().Where(id => !id.IsNull).ToList();
        if (!ids.Contains(boundaryId))
            ids.Add(boundaryId);

        count = LinkObjectIds(doc, productId, role, ids.ToArray(), fenceExtents);
        return count > 0;
    }

    private static Extents3d ExtentsFromPoints(Point3dCollection pts)
    {
        if (pts.Count == 0)
            return new Extents3d(Point3d.Origin, Point3d.Origin);
        var min = pts[0];
        var max = pts[0];
        for (var i = 1; i < pts.Count; i++)
        {
            var p = pts[i];
            min = new Point3d(Math.Min(min.X, p.X), Math.Min(min.Y, p.Y), Math.Min(min.Z, p.Z));
            max = new Point3d(Math.Max(max.X, p.X), Math.Max(max.Y, p.Y), Math.Max(max.Z, p.Z));
        }

        return new Extents3d(min, max);
    }

    public static int LinkObjectIds(Document doc, Guid productId, string? role, ObjectId[] objectIds, Extents3d? linkFenceExtents = null)
    {
        var xdata = new XDataService();
        var reg = new RegistryService();
        var count = 0;

        using var docLock = doc.LockDocument();
        using (var tr = doc.Database.TransactionManager.StartTransaction())
        {
            if (!reg.TryGetProduct(tr, doc.Database, productId, out var pr) || pr == null)
            {
                tr.Commit();
                return 0;
            }

            xdata.EnsureRegApp(tr, doc.Database);

            foreach (var oid in objectIds)
            {
                if (oid.IsNull)
                    continue;

                var obj = tr.GetObject(oid, OpenMode.ForWrite, false);
                if (obj is not Entity ent)
                    continue;

                if (ent is Viewport)
                    continue;

                xdata.SetProductLink(ent, productId, role, tr, doc.Database);
                count++;
            }

            if (linkFenceExtents is { } fe)
            {
                pr.NormalizeLinkFencesFromLegacy();
                pr.LinkFenceMinX = pr.LinkFenceMinY = pr.LinkFenceMaxX = pr.LinkFenceMaxY = null;
                pr.LinkFences.Add(new LinkFenceBox
                {
                    FenceId = Guid.NewGuid(),
                    MinX = fe.MinPoint.X,
                    MinY = fe.MinPoint.Y,
                    MaxX = fe.MaxPoint.X,
                    MaxY = fe.MaxPoint.Y,
                });
                ProductPdfDrawingSync.NormalizeProductRecord(pr);
                reg.SaveProduct(tr, doc.Database, pr);
            }

            tr.Commit();
        }

        return count;
    }
}
