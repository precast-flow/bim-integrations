using System.Drawing;
using System.Drawing.Drawing2D;

namespace BimPrefabExport.UI;

/// <summary>Küçük araç çubuğu simgeleri (harici dosya gerekmez).</summary>
internal static class PaletteIcons
{
    private static readonly Color Accent = Color.FromArgb(0, 114, 198);
    private static readonly Color Ink = Color.FromArgb(40, 40, 40);

    public static Image Refresh => DrawIcon(g =>
    {
        g.DrawArc(new Pen(Accent, 2f), 2, 3, 12, 10, 45, 270);
        g.FillPolygon(new SolidBrush(Accent), new[] { new Point(12, 2), new Point(16, 2), new Point(14, 6) });
    });

    public static Image Add => DrawIcon(g =>
    {
        g.FillEllipse(new SolidBrush(Color.FromArgb(46, 125, 50)), 1, 1, 14, 14);
        g.DrawLine(new Pen(Color.White, 2f), 8, 4, 8, 12);
        g.DrawLine(new Pen(Color.White, 2f), 4, 8, 12, 8);
    });

    public static Image Save => DrawIcon(g =>
    {
        g.FillRectangle(new SolidBrush(Accent), 4, 2, 8, 6);
        g.FillRectangle(new SolidBrush(Color.FromArgb(255, 193, 7)), 3, 8, 10, 7);
        g.DrawRectangle(new Pen(Ink, 1f), 3, 8, 10, 7);
    });

    public static Image Delete => DrawIcon(g =>
    {
        g.DrawRectangle(new Pen(Ink, 1.5f), 4, 4, 8, 10);
        g.DrawLine(new Pen(Color.FromArgb(198, 40, 40), 2f), 2, 4, 14, 4);
        g.DrawLine(new Pen(Color.FromArgb(198, 40, 40), 1.5f), 7, 7, 7, 12);
        g.DrawLine(new Pen(Color.FromArgb(198, 40, 40), 1.5f), 9, 7, 9, 12);
    });

    public static Image Polyline => DrawIcon(g =>
    {
        g.DrawLines(new Pen(Accent, 2f), new[] { new Point(2, 12), new Point(6, 4), new Point(10, 10), new Point(14, 3) });
    });

    public static Image Zoom => DrawIcon(g =>
    {
        g.DrawEllipse(new Pen(Accent, 2f), 2, 3, 9, 9);
        g.DrawLine(new Pen(Accent, 2f), 11, 11, 15, 15);
    });

    public static Image Pdf => DrawIcon(g =>
    {
        g.DrawRectangle(new Pen(Ink, 1f), 3, 2, 9, 12);
        g.DrawString("A", new Font("Arial", 7f, FontStyle.Bold), Brushes.Crimson, 4, 5);
    });

    public static Image PdfBulk => DrawIcon(g =>
    {
        g.DrawRectangle(new Pen(Ink, 1f), 2, 4, 7, 10);
        g.DrawRectangle(new Pen(Ink, 1f), 6, 2, 7, 10);
    });

    public static Image Excel => DrawIcon(g =>
    {
        g.FillRectangle(new SolidBrush(Color.FromArgb(33, 115, 70)), 2, 2, 12, 12);
        g.DrawString("X", new Font("Arial", 8f, FontStyle.Bold), Brushes.White, 4, 3);
    });

    /// <summary>Form / detay düzenleme.</summary>
    public static Image Edit => DrawIcon(g =>
    {
        g.DrawRectangle(new Pen(Ink, 1.5f), 4, 3, 9, 11);
        g.DrawLine(new Pen(Accent, 1.8f), 5, 6, 12, 6);
        g.DrawLine(new Pen(Accent, 1.8f), 5, 8, 10, 8);
        g.DrawLine(new Pen(Accent, 1.8f), 5, 10, 11, 10);
    });

    private static Image DrawIcon(Action<Graphics> draw)
    {
        var bmp = new Bitmap(18, 18);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);
        draw(g);
        return bmp;
    }
}
