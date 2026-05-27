using System.Windows;
using System.Windows.Interop;

namespace BimPrefabExport.Interop;

/// <summary>Prefab Flow ile aynı: WPF penceresini AutoCAD ana penceresine bağlar.</summary>
internal static class AcadWindowInterop
{
    public static void SetOwnerToAcadMainWindow(Window window)
    {
        var acadMain = Autodesk.AutoCAD.ApplicationServices.Application.MainWindow;
        if (acadMain is null)
            return;

        _ = new WindowInteropHelper(window)
        {
            Owner = acadMain.Handle,
        };
    }
}
