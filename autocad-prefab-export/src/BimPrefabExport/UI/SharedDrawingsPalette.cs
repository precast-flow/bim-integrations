using BimPrefabExport.Interop;

namespace BimPrefabExport.UI;

public static class SharedDrawingsPalette
{
    private static SharedDrawingsWindow? s_window;

    public static void EnsureShown()
    {
        if (s_window is { IsLoaded: true })
        {
            s_window.Activate();
            return;
        }

        var win = new SharedDrawingsWindow();
        s_window = win;
        AcadWindowInterop.SetOwnerToAcadMainWindow(win);
        win.Closed += (_, _) =>
        {
            if (ReferenceEquals(s_window, win))
                s_window = null;
        };
        win.Show();
    }

    public static void ReloadIfOpen() => s_window?.ReloadFromDocument();
}
