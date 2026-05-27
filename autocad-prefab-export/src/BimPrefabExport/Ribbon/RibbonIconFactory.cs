using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Media.Imaging;

namespace BimPrefabExport.Ribbon;

/// <summary>Ribbon için PNG tabanlı simgeler (32×32 büyük, 16×16 küçük).</summary>
internal static class RibbonIconFactory
{
    private static readonly System.Drawing.Color Accent = System.Drawing.Color.FromArgb(0, 114, 198);
    private static readonly System.Drawing.Color Ink = System.Drawing.Color.FromArgb(45, 45, 48);
    public static System.Windows.Media.ImageSource ToImageSource(Bitmap bmp)
    {
        using var ms = new MemoryStream();
        bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        ms.Position = 0;
        var img = new BitmapImage();
        img.BeginInit();
        img.StreamSource = ms;
        img.CacheOption = BitmapCacheOption.OnLoad;
        img.EndInit();
        img.Freeze();
        return img;
    }

    private static Bitmap Draw(int px, Action<Graphics, float> draw)
    {
        var bmp = new Bitmap(px, px);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(System.Drawing.Color.Transparent);
        var s = px / 16f;
        draw(g, s);
        return bmp;
    }

    public static (System.Windows.Media.ImageSource Large, System.Windows.Media.ImageSource Small) Palette()
    {
        void draw(Graphics g, float s)
        {
            var pen = new System.Drawing.Pen(Ink, 1.8f * s);
            g.DrawRectangle(pen, 2.5f * s, 3 * s, 11 * s, 10 * s);
            g.DrawLine(pen, 5 * s, 6 * s, 13 * s, 6 * s);
            g.DrawLine(pen, 5 * s, 9 * s, 11 * s, 9 * s);
            g.DrawLine(pen, 5 * s, 12 * s, 9 * s, 12 * s);
        }

        using var l = Draw(32, draw);
        using var sm = Draw(16, draw);
        return (ToImageSource(l), ToImageSource(sm));
    }

    public static (System.Windows.Media.ImageSource Large, System.Windows.Media.ImageSource Small) PolylineBoundary()
    {
        void draw(Graphics g, float s)
        {
            using var p = new System.Drawing.Pen(Accent, 2.2f * s);
            g.DrawLines(p, new[]
            {
                new PointF(2 * s, 12 * s),
                new PointF(6 * s, 4 * s),
                new PointF(10 * s, 11 * s),
                new PointF(14 * s, 5 * s),
            });
        }

        using var l = Draw(32, draw);
        using var sm = Draw(16, draw);
        return (ToImageSource(l), ToImageSource(sm));
    }

    public static (System.Windows.Media.ImageSource Large, System.Windows.Media.ImageSource Small) ShowProduct()
    {
        void draw(Graphics g, float s)
        {
            using var p = new System.Drawing.Pen(Accent, 2f * s);
            g.DrawEllipse(p, 3 * s, 4 * s, 9 * s, 9 * s);
            g.DrawLine(p, 11 * s, 11 * s, 14.5f * s, 14.5f * s);
        }

        using var l = Draw(32, draw);
        using var sm = Draw(16, draw);
        return (ToImageSource(l), ToImageSource(sm));
    }

    public static (System.Windows.Media.ImageSource Large, System.Windows.Media.ImageSource Small) PdfSingle()
    {
        void draw(Graphics g, float s)
        {
            g.DrawRectangle(new System.Drawing.Pen(Ink, 1.5f * s), 3 * s, 2 * s, 10 * s, 12 * s);
            using var f = new System.Drawing.Font("Segoe UI", 7f * s, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Pixel);
            g.DrawString("A", f, System.Drawing.Brushes.IndianRed, 5.2f * s, 5 * s);
        }

        using var l = Draw(32, draw);
        using var sm = Draw(16, draw);
        return (ToImageSource(l), ToImageSource(sm));
    }

    public static (System.Windows.Media.ImageSource Large, System.Windows.Media.ImageSource Small) PdfBulk()
    {
        void draw(Graphics g, float s)
        {
            g.DrawRectangle(new System.Drawing.Pen(Ink, 1.3f * s), 2 * s, 4 * s, 6.5f * s, 9 * s);
            g.DrawRectangle(new System.Drawing.Pen(Accent, 1.3f * s), 6.5f * s, 2 * s, 7 * s, 9 * s);
        }

        using var l = Draw(32, draw);
        using var sm = Draw(16, draw);
        return (ToImageSource(l), ToImageSource(sm));
    }

    public static (System.Windows.Media.ImageSource Large, System.Windows.Media.ImageSource Small) SharedDrawings()
    {
        void draw(Graphics g, float s)
        {
            using var p = new System.Drawing.Pen(Ink, 1.4f * s);
            g.DrawRectangle(p, 2.5f * s, 3 * s, 6 * s, 10 * s);
            using var p2 = new System.Drawing.Pen(Accent, 1.4f * s);
            g.DrawRectangle(p2, 7.5f * s, 2 * s, 6.5f * s, 11 * s);
        }

        using var l = Draw(32, draw);
        using var sm = Draw(16, draw);
        return (ToImageSource(l), ToImageSource(sm));
    }

    public static (System.Windows.Media.ImageSource Large, System.Windows.Media.ImageSource Small) ExcelList()
    {
        void draw(Graphics g, float s)
        {
            g.FillRectangle(new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(33, 115, 70)), 2 * s, 2 * s, 12 * s, 12 * s);
            using var f = new System.Drawing.Font("Arial", 8f * s, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Pixel);
            g.DrawString("X", f, System.Drawing.Brushes.White, 4.5f * s, 3 * s);
        }

        using var l = Draw(32, draw);
        using var sm = Draw(16, draw);
        return (ToImageSource(l), ToImageSource(sm));
    }
}
