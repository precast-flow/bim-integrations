using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace BimPrefabExport.Services;

/// <summary>
/// Çit (WCS XY dikdörtgen) içinde kalan model uzayı geometrisini hafif tel kafes segmentleri olarak toplar (WPF önizleme).
/// </summary>
public static class ModelFenceWireframePreview
{
    private const int MaxEntities = 1400;
    private const int MaxSegments = 7000;
    private const int CircleDivisions = 36;
    private const int ArcDivisions = 14;
    private const int SplineSamples = 24;
    private const int BulgeArcSamples = 10;

    public static List<(Point2d A, Point2d B)> CollectSegments(
        Transaction tr,
        Database db,
        double clipMinX,
        double clipMinY,
        double clipMaxX,
        double clipMaxY)
    {
        var outSegs = new List<(Point2d, Point2d)>(512);
        var msId = SymbolUtilityServices.GetBlockModelSpaceId(db);
        var ms = (BlockTableRecord)tr.GetObject(msId, OpenMode.ForRead);

        var nEnt = 0;
        foreach (ObjectId id in ms)
        {
            if (nEnt++ > MaxEntities || outSegs.Count >= MaxSegments)
                break;

            if (tr.GetObject(id, OpenMode.ForRead, false) is not Entity ent)
                continue;
            if (ent is Viewport or BlockReference)
                continue;

            Extents3d ext;
            try
            {
                ext = ent.GeometricExtents;
            }
            catch
            {
                continue;
            }

            if (!OverlapsXY(ext, clipMinX, clipMinY, clipMaxX, clipMaxY))
                continue;

            switch (ent)
            {
                case Line ln:
                    TryAddClippedSegment(new Point2d(ln.StartPoint.X, ln.StartPoint.Y),
                        new Point2d(ln.EndPoint.X, ln.EndPoint.Y), clipMinX, clipMinY, clipMaxX, clipMaxY, outSegs);
                    break;
                case Polyline pl:
                    TryAddPolyline(pl, clipMinX, clipMinY, clipMaxX, clipMaxY, outSegs);
                    break;
                case Arc arc:
                    TryAddArc(arc, clipMinX, clipMinY, clipMaxX, clipMaxY, outSegs);
                    break;
                case Circle cir:
                    TryAddCircle(cir, clipMinX, clipMinY, clipMaxX, clipMaxY, outSegs);
                    break;
                case Ellipse elli:
                    TryAddEllipse(elli, clipMinX, clipMinY, clipMaxX, clipMaxY, outSegs);
                    break;
                case Spline spl:
                    TryAddSpline(spl, clipMinX, clipMinY, clipMaxX, clipMaxY, outSegs);
                    break;
            }
        }

        return outSegs;
    }

    private static bool OverlapsXY(Extents3d e, double minX, double minY, double maxX, double maxY)
    {
        var ex0 = Math.Min(e.MinPoint.X, e.MaxPoint.X);
        var ex1 = Math.Max(e.MinPoint.X, e.MaxPoint.X);
        var ey0 = Math.Min(e.MinPoint.Y, e.MaxPoint.Y);
        var ey1 = Math.Max(e.MinPoint.Y, e.MaxPoint.Y);
        return !(ex1 < minX || ex0 > maxX || ey1 < minY || ey0 > maxY);
    }

    private static void TryAddPolyline(
        Polyline pl,
        double clipMinX,
        double clipMinY,
        double clipMaxX,
        double clipMaxY,
        List<(Point2d, Point2d)> outSegs)
    {
        var n = pl.NumberOfVertices;
        if (n < 2)
            return;
        var last = pl.Closed ? n : n - 1;
        for (var i = 0; i < last; i++)
        {
            if (outSegs.Count >= MaxSegments)
                return;
            var iNext = (i + 1) % n;
            var p0 = pl.GetPoint2dAt(i);
            var p1 = pl.GetPoint2dAt(iNext);
            var bulge = pl.GetBulgeAt(i);
            if (Math.Abs(bulge) < 1e-12)
                TryAddClippedSegment(p0, p1, clipMinX, clipMinY, clipMaxX, clipMaxY, outSegs);
            else
                TryAddBulgeChain(p0, p1, bulge, clipMinX, clipMinY, clipMaxX, clipMaxY, outSegs);
        }
    }

    private static void TryAddBulgeChain(
        Point2d p0,
        Point2d p1,
        double bulge,
        double clipMinX,
        double clipMinY,
        double clipMaxX,
        double clipMaxY,
        List<(Point2d, Point2d)> outSegs)
    {
        var pts = BulgeArcPoints(p0, p1, bulge, BulgeArcSamples);
        for (var k = 0; k < pts.Count - 1; k++)
        {
            if (outSegs.Count >= MaxSegments)
                return;
            TryAddClippedSegment(pts[k], pts[k + 1], clipMinX, clipMinY, clipMaxX, clipMaxY, outSegs);
        }
    }

    private static List<Point2d> BulgeArcPoints(Point2d p0, Point2d p1, double bulge, int samples)
    {
        var list = new List<Point2d>(samples + 1) { p0 };
        if (Math.Abs(bulge) < 1e-12 || samples < 2)
        {
            list.Add(p1);
            return list;
        }

        var theta = 4.0 * Math.Atan(bulge);
        var chord = p0.GetDistanceTo(p1);
        if (chord < 1e-12)
        {
            list.Add(p1);
            return list;
        }

        var sinHalf = Math.Sin(theta / 2.0);
        if (Math.Abs(sinHalf) < 1e-12)
        {
            list.Add(p1);
            return list;
        }

        var radius = chord / (2.0 * sinHalf);
        var mid = new Point2d((p0.X + p1.X) / 2.0, (p0.Y + p1.Y) / 2.0);
        var ux = (p1.X - p0.X) / chord;
        var uy = (p1.Y - p0.Y) / chord;
        var px = -uy;
        var py = ux;
        var h = radius * Math.Cos(theta / 2.0);
        var side = bulge > 0 ? 1.0 : -1.0;
        var cx = mid.X + side * px * h;
        var cy = mid.Y + side * py * h;
        var a0 = Math.Atan2(p0.Y - cy, p0.X - cx);
        var a1 = Math.Atan2(p1.Y - cy, p1.X - cx);
        var sweep = theta;
        if (bulge < 0)
            sweep = -Math.Abs(theta);
        for (var i = 1; i <= samples; i++)
        {
            var t = i / (double)samples;
            var ang = a0 + sweep * t;
            list.Add(new Point2d(cx + radius * Math.Cos(ang), cy + radius * Math.Sin(ang)));
        }

        return list;
    }

    private static void TryAddArc(Arc arc, double clipMinX, double clipMinY, double clipMaxX, double clipMaxY,
        List<(Point2d, Point2d)> outSegs)
    {
        var center = arc.Center;
        var r = arc.Radius;
        var start = arc.StartAngle;
        var end = arc.EndAngle;
        var span = end - start;
        if (span < 0)
            span += Math.PI * 2;
        for (var i = 0; i < ArcDivisions; i++)
        {
            if (outSegs.Count >= MaxSegments)
                return;
            var t0 = start + span * (i / (double)ArcDivisions);
            var t1 = start + span * ((i + 1) / (double)ArcDivisions);
            var p0 = Polar(center, r, t0);
            var p1 = Polar(center, r, t1);
            TryAddClippedSegment(p0, p1, clipMinX, clipMinY, clipMaxX, clipMaxY, outSegs);
        }
    }

    private static Point2d Polar(Point3d c, double r, double ang) =>
        new(c.X + r * Math.Cos(ang), c.Y + r * Math.Sin(ang));

    private static void TryAddCircle(Circle cir, double clipMinX, double clipMinY, double clipMaxX, double clipMaxY,
        List<(Point2d, Point2d)> outSegs)
    {
        var c = cir.Center;
        var r = cir.Radius;
        for (var i = 0; i < CircleDivisions; i++)
        {
            if (outSegs.Count >= MaxSegments)
                return;
            var t0 = (i / (double)CircleDivisions) * Math.PI * 2;
            var t1 = ((i + 1) / (double)CircleDivisions) * Math.PI * 2;
            var p0 = Polar(c, r, t0);
            var p1 = Polar(c, r, t1);
            TryAddClippedSegment(p0, p1, clipMinX, clipMinY, clipMaxX, clipMaxY, outSegs);
        }
    }

    private static void TryAddEllipse(Ellipse elli, double clipMinX, double clipMinY, double clipMaxX, double clipMaxY,
        List<(Point2d, Point2d)> outSegs)
    {
        try
        {
            var s0 = elli.StartParam;
            var s1 = elli.EndParam;
            var span = s1 - s0;
            if (span <= 1e-9)
                return;
            for (var i = 0; i < CircleDivisions; i++)
            {
                if (outSegs.Count >= MaxSegments)
                    return;
                var u0 = s0 + span * (i / (double)CircleDivisions);
                var u1 = s0 + span * ((i + 1) / (double)CircleDivisions);
                var p0 = elli.GetPointAtParameter(u0);
                var p1 = elli.GetPointAtParameter(u1);
                TryAddClippedSegment(new Point2d(p0.X, p0.Y), new Point2d(p1.X, p1.Y), clipMinX, clipMinY, clipMaxX,
                    clipMaxY, outSegs);
            }
        }
        catch
        {
            /* yoksay */
        }
    }

    private static void TryAddSpline(Spline spl, double clipMinX, double clipMinY, double clipMaxX, double clipMaxY,
        List<(Point2d, Point2d)> outSegs)
    {
        try
        {
            var s0 = spl.StartParam;
            var s1 = spl.EndParam;
            for (var i = 0; i < SplineSamples; i++)
            {
                if (outSegs.Count >= MaxSegments)
                    return;
                var u0 = s0 + (s1 - s0) * (i / (double)SplineSamples);
                var u1 = s0 + (s1 - s0) * ((i + 1) / (double)SplineSamples);
                var p0 = spl.GetPointAtParameter(u0);
                var p1 = spl.GetPointAtParameter(u1);
                TryAddClippedSegment(new Point2d(p0.X, p0.Y), new Point2d(p1.X, p1.Y), clipMinX, clipMinY, clipMaxX,
                    clipMaxY, outSegs);
            }
        }
        catch
        {
            /* yoksay */
        }
    }

    private static void TryAddClippedSegment(
        Point2d a,
        Point2d b,
        double clipMinX,
        double clipMinY,
        double clipMaxX,
        double clipMaxY,
        List<(Point2d, Point2d)> outSegs)
    {
        if (CohenSutherlandClip(ref a, ref b, clipMinX, clipMinY, clipMaxX, clipMaxY))
            outSegs.Add((a, b));
    }

    private static bool CohenSutherlandClip(ref Point2d p0, ref Point2d p1, double xmin, double ymin, double xmax,
        double ymax)
    {
        const double Eps = 1e-9;
        const int Inside = 0, Left = 1, Right = 2, Bottom = 4, Top = 8;

        int Code(double x, double y)
        {
            var c = Inside;
            if (x < xmin) c |= Left;
            else if (x > xmax) c |= Right;
            if (y < ymin) c |= Bottom;
            else if (y > ymax) c |= Top;
            return c;
        }

        var x0 = p0.X;
        var y0 = p0.Y;
        var x1 = p1.X;
        var y1 = p1.Y;
        var c0 = Code(x0, y0);
        var c1 = Code(x1, y1);

        for (var guard = 0; guard < 12; guard++)
        {
            if ((c0 | c1) == 0)
            {
                p0 = new Point2d(x0, y0);
                p1 = new Point2d(x1, y1);
                return (p1.X - p0.X) * (p1.X - p0.X) + (p1.Y - p0.Y) * (p1.Y - p0.Y) > Eps * Eps;
            }

            if ((c0 & c1) != 0)
                return false;

            var c = c0 != 0 ? c0 : c1;
            double x = 0, y = 0;
            if ((c & Top) != 0)
            {
                if (Math.Abs(y1 - y0) < Eps)
                    return false;
                x = x0 + (x1 - x0) * (ymax - y0) / (y1 - y0);
                y = ymax;
            }
            else if ((c & Bottom) != 0)
            {
                if (Math.Abs(y1 - y0) < Eps)
                    return false;
                x = x0 + (x1 - x0) * (ymin - y0) / (y1 - y0);
                y = ymin;
            }
            else if ((c & Right) != 0)
            {
                if (Math.Abs(x1 - x0) < Eps)
                    return false;
                y = y0 + (y1 - y0) * (xmax - x0) / (x1 - x0);
                x = xmax;
            }
            else if ((c & Left) != 0)
            {
                if (Math.Abs(x1 - x0) < Eps)
                    return false;
                y = y0 + (y1 - y0) * (xmin - x0) / (x1 - x0);
                x = xmin;
            }

            if (c == c0)
            {
                x0 = x;
                y0 = y;
                c0 = Code(x0, y0);
            }
            else
            {
                x1 = x;
                y1 = y;
                c1 = Code(x1, y1);
            }
        }

        return false;
    }
}
