using System.Windows.Input;
using Autodesk.AutoCAD.ApplicationServices;
using BimPrefabExport.Services;

namespace BimPrefabExport.Ribbon;

/// <summary>Ribbon düğmesinden AutoCAD komutunu çalıştırır; pickfirst seçimini korur.</summary>
internal sealed class RibbonAcadCommand : ICommand
{
    private readonly string _globalCommandName;

    public RibbonAcadCommand(string globalCommandName)
    {
        _globalCommandName = globalCommandName;
    }

    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter)
    {
        var doc = AcadApp.DocumentManager.MdiActiveDocument;
        if (doc is null)
            return;

        PickFirstBridge.StashFromImplied(doc, _globalCommandName);
        doc.SendStringToExecute(_globalCommandName + " ", true, false, false);
    }
}
