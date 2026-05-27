using Autodesk.AutoCAD.Runtime;
using BimPrefabExport.Ribbon;

[assembly: CommandClass(typeof(BimPrefabExport.Commands.PrefabCommands))]
[assembly: ExtensionApplication(typeof(BimPrefabExport.Plugin))]

namespace BimPrefabExport;

public class Plugin : IExtensionApplication
{
    private bool _ribbonEnsured;

    public void Initialize()
    {
        AcadApp.Idle += OnIdleOnce;
    }

    public void Terminate()
    {
        AcadApp.Idle -= OnIdleOnce;
    }

    private void OnIdleOnce(object? sender, EventArgs e)
    {
        if (_ribbonEnsured)
        {
            AcadApp.Idle -= OnIdleOnce;
            return;
        }

        try
        {
            if (Autodesk.Windows.ComponentManager.Ribbon is null)
                return;

            BimPrefabRibbonBuilder.EnsureTab();
            _ribbonEnsured = true;
            AcadApp.Idle -= OnIdleOnce;
        }
        catch (System.Exception ex)
        {
            AcadApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
                $"\n[BIM_PREFAB] Ribbon yüklenemedi: {ex.Message}");
        }
    }
}
