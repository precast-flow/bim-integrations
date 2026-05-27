using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using BimPrefabExport.Core;

namespace BimPrefabExport.Services;

/// <summary>Ortak çizim kaydı için tek kapalı polyline ile WCS çit (XData bağlamaz).</summary>
public static class SharedDrawingFenceService
{
    public static bool TryPickSingleClosedPolyline(Document doc, Guid drawingId, out string? error)
    {
        error = null;
        var ed = doc.Editor;
        var peo = new PromptEntityOptions("\nKapalı polyline seçin (ortak çizim sınırı): ")
        {
            AllowObjectOnLockedLayer = false,
        };
        peo.SetRejectMessage("\nYalnızca Polyline.");
        peo.AddAllowedClass(typeof(Polyline), true);

        var per = ed.GetEntity(peo);
        if (per.Status != PromptStatus.OK)
        {
            error = "İptal.";
            return false;
        }

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

            if (pl.NumberOfVertices < 3)
            {
                tr.Commit();
                error = "En az 3 köşe gerekli.";
                return false;
            }

            try
            {
                fenceExtents = pl.GeometricExtents;
            }
            catch
            {
                fenceExtents = ExtentsFromPolyline(pl);
            }

            tr.Commit();
        }

        using var docLock = doc.LockDocument();
        using (var tr = doc.Database.TransactionManager.StartTransaction())
        {
            var docModel = SharedDrawingsRegistry.Load(tr, doc.Database);
            var entry = docModel.Drawings.FirstOrDefault(e => e.DrawingId == drawingId);
            if (entry is null)
            {
                tr.Commit();
                error = "Çizim kaydı bulunamadı (önce listede kaydedin).";
                return false;
            }

            entry.LinkFences.Clear();
            entry.LinkFences.Add(new LinkFenceBox
            {
                FenceId = Guid.NewGuid(),
                MinX = fenceExtents.MinPoint.X,
                MinY = fenceExtents.MinPoint.Y,
                MaxX = fenceExtents.MaxPoint.X,
                MaxY = fenceExtents.MaxPoint.Y,
            });

            var utc = DateTime.UtcNow;
            entry.ModifiedUtc = utc;
            entry.CreatedUtc ??= utc;

            SharedDrawingsRegistry.Save(tr, doc.Database, docModel);
            tr.Commit();
        }

        ed.WriteMessage("\n[BIM_PREFAB] Ortak çizim sınırı kaydedildi (tek alan).");
        return true;
    }

    private static Extents3d ExtentsFromPolyline(Polyline pl)
    {
        var ext = new Extents3d();
        var has = false;
        for (var i = 0; i < pl.NumberOfVertices; i++)
        {
            var p = pl.GetPoint3dAt(i);
            if (!has)
            {
                ext = new Extents3d(p, p);
                has = true;
            }
            else
                ext.AddPoint(p);
        }

        return has ? ext : new Extents3d(Point3d.Origin, Point3d.Origin);
    }
}
