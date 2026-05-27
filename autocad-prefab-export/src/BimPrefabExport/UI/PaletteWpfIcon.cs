using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BimPrefabExport.UI;

internal static class PaletteWpfIcon
{
    public static ImageSource? FromBitmap(Image? bmp)
    {
        if (bmp is null)
            return null;

        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        ms.Position = 0;
        var img = new BitmapImage();
        img.BeginInit();
        img.StreamSource = ms;
        img.CacheOption = BitmapCacheOption.OnLoad;
        img.EndInit();
        img.Freeze();
        return img;
    }
}
