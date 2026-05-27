using BimPrefabExport.Interop;

namespace BimPrefabExport.UI;

/// <summary>Yüzen WPF paleti açar (Prefab Flow sihirbaz pencereleriyle aynı genel stil).</summary>
public static class PrefabPalette
{
    private static BimPrefabPaletteWindow? s_window;

    public static void EnsureShown()
    {
        if (s_window is null)
        {
            s_window = new BimPrefabPaletteWindow();
            s_window.Closed += (_, _) => s_window = null;
            AcadWindowInterop.SetOwnerToAcadMainWindow(s_window);
            s_window.Show();
        }
        else
        {
            s_window.Activate();
        }

        s_window.RefreshFromActiveDocument();
    }

    public static void TryRefresh()
    {
        s_window?.RefreshFromActiveDocument();
    }
}
