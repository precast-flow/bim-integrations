using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using BimPrefabExport.Core;

namespace BimPrefabExport.Services;

public static class DrawingInitService
{
    public static void EnsureInit(Document doc)
    {
        using var docLock = doc.LockDocument();
        using (var tr = doc.Database.TransactionManager.StartTransaction())
        {
            new RegistryService().GetOrCreateRegistryRoot(tr, doc.Database);
            SharedDrawingsRegistry.EnsureRoot(tr, doc.Database);
            new XDataService().EnsureRegApp(tr, doc.Database);
            tr.Commit();
        }
    }
}
