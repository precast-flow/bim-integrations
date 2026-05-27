using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using BimPrefabExport.Core;

namespace BimPrefabExport.Services;

/// <summary>
/// PDF için model alanında ürün sınırı (polyline çit veya bağlı nesneler) hesaplar.
/// </summary>
public static class ProductModelPlotExtentsService
{
    private const double ExtentMarginRatio = 0.03;

    public static bool TryGetModelSpacePlotWindow(
        Transaction tr,
        Database db,
        ProductRecord product,
        out Extents3d ext,
        out ObjectId modelLayoutId)
    {
        ext = new Extents3d();
        modelLayoutId = ObjectId.Null;

        var hasFence = TryGetExtentsFromStoredFence(product, out var fenceExt);
        TryCountLinkedModelEntities(tr, db, product.ProductId, out var entityCount);
        var hasEntities = entityCount > 0;
        if (!hasEntities && !hasFence)
            return false;

        if (hasFence)
            ext = fenceExt;
        else if (!TryComputeModelExtentsForProduct(tr, db, product.ProductId, out ext, out _))
            return false;

        AddMargin(ref ext, ExtentMarginRatio);

        var msId = SymbolUtilityServices.GetBlockModelSpaceId(db);
        var ms = (BlockTableRecord)tr.GetObject(msId, OpenMode.ForRead);
        modelLayoutId = ms.LayoutId;
        return modelLayoutId.IsValid;
    }

    private static bool TryGetExtentsFromStoredFence(ProductRecord product, out Extents3d ext)
    {
        ext = new Extents3d();
        var boxes = product.LinkFences.Where(b => b.IsValid()).ToList();
        if (boxes.Count == 0 && TryLegacyFenceAsBox(product, out var legacyBox))
            boxes.Add(legacyBox);

        if (boxes.Count > 0)
        {
            var has = false;
            foreach (var b in boxes)
            {
                var e = new Extents3d(new Point3d(b.MinX, b.MinY, 0), new Point3d(b.MaxX, b.MaxY, 0));
                if (!has)
                {
                    ext = e;
                    has = true;
                }
                else
                {
                    ext.AddExtents(e);
                }
            }

            return has;
        }

        return false;
    }

    private static bool TryLegacyFenceAsBox(ProductRecord product, out LinkFenceBox box)
    {
        box = new LinkFenceBox();
        if (!product.LinkFenceMinX.HasValue || !product.LinkFenceMinY.HasValue ||
            !product.LinkFenceMaxX.HasValue || !product.LinkFenceMaxY.HasValue)
            return false;
        if (product.LinkFenceMaxX.Value <= product.LinkFenceMinX.Value ||
            product.LinkFenceMaxY.Value <= product.LinkFenceMinY.Value)
            return false;
        box.MinX = product.LinkFenceMinX.Value;
        box.MinY = product.LinkFenceMinY.Value;
        box.MaxX = product.LinkFenceMaxX.Value;
        box.MaxY = product.LinkFenceMaxY.Value;
        return true;
    }

    private static bool TryCountLinkedModelEntities(Transaction tr, Database db, Guid productId, out int count)
    {
        count = 0;
        var xdata = new XDataService();
        var msId = SymbolUtilityServices.GetBlockModelSpaceId(db);
        var ms = (BlockTableRecord)tr.GetObject(msId, OpenMode.ForRead);

        foreach (ObjectId id in ms)
        {
            if (tr.GetObject(id, OpenMode.ForRead, false) is not Entity ent)
                continue;
            if (!xdata.TryGetProductLink(ent, out var pid, out _) || pid != productId)
                continue;
            count++;
        }

        return count > 0;
    }

    private static bool TryComputeModelExtentsForProduct(
        Transaction tr,
        Database db,
        Guid productId,
        out Extents3d ext,
        out int entityCount)
    {
        ext = new Extents3d();
        entityCount = 0;
        var xdata = new XDataService();
        var msId = SymbolUtilityServices.GetBlockModelSpaceId(db);
        var ms = (BlockTableRecord)tr.GetObject(msId, OpenMode.ForRead);

        var has = false;
        foreach (ObjectId id in ms)
        {
            if (tr.GetObject(id, OpenMode.ForRead, false) is not Entity ent)
                continue;

            if (!xdata.TryGetProductLink(ent, out var pid, out _) || pid != productId)
                continue;

            entityCount++;
            try
            {
                var e = ent.GeometricExtents;
                if (!has)
                {
                    ext = e;
                    has = true;
                }
                else
                {
                    ext.AddExtents(e);
                }
            }
            catch
            {
                // geometrik sınırı olmayan nesneler
            }
        }

        return has;
    }

    private static void AddMargin(ref Extents3d ext, double ratio)
    {
        var min = ext.MinPoint;
        var max = ext.MaxPoint;
        var dx = (max.X - min.X) * ratio;
        var dy = (max.Y - min.Y) * ratio;
        if (dx <= 0) dx = 1;
        if (dy <= 0) dy = 1;
        ext = new Extents3d(
            new Point3d(min.X - dx, min.Y - dy, min.Z),
            new Point3d(max.X + dx, max.Y + dy, max.Z));
    }
}
