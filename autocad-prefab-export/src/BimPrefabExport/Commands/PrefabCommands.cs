using System.IO;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using BimPrefabExport.Core;
using BimPrefabExport.Export;
using BimPrefabExport.Services;
using BimPrefabExport.UI;

namespace BimPrefabExport.Commands;

public class PrefabCommands
{
    [CommandMethod("BIM_PREFAB_PANEL")]
    public void ShowPanel()
    {
        var doc = AcadApp.DocumentManager.MdiActiveDocument;
        if (doc is not null)
            DrawingInitService.EnsureInit(doc);

        PrefabPalette.EnsureShown();
    }

    [CommandMethod("BIM_PREFAB_RECT_POLY")]
    public void RectPolyAssign()
    {
        var doc = AcadApp.DocumentManager.MdiActiveDocument;
        if (doc is null)
            return;

        if (!PrefabUiSession.SelectedProductId.HasValue)
        {
            doc.Editor.WriteMessage("\n[BIM_PREFAB] Önce paletten bir ürün seçin.");
            return;
        }

        DrawingInitService.EnsureInit(doc);
        var id = PrefabUiSession.SelectedProductId.Value;
        PrefabPalette.BeginInteractivePick();
        try
        {
            var count = RectangleLinkService.AssignByClosedPolyline(doc, id, PrefabUiSession.DefaultRole, out var err);
            if (err is not null)
                doc.Editor.WriteMessage("\n[BIM_PREFAB] " + err);
            else if (count == 0 && !RegistryHasProduct(doc, id))
                doc.Editor.WriteMessage("\n[BIM_PREFAB] Ürün çizimde bulunamadı (registry).");
        }
        finally
        {
            PrefabPalette.EndInteractivePick();
            PrefabPalette.TryRefresh();
        }
    }

    [CommandMethod("BIM_PREFAB_SHARED_DRAWINGS")]
    public void SharedDrawingsDialog()
    {
        var doc = AcadApp.DocumentManager.MdiActiveDocument;
        if (doc is not null)
            DrawingInitService.EnsureInit(doc);

        SharedDrawingsPalette.EnsureShown();
    }

    [CommandMethod("BIM_PREFAB_SHARED_DRAWING_RECT")]
    public void SharedDrawingRect()
    {
        var doc = AcadApp.DocumentManager.MdiActiveDocument;
        if (doc is null)
            return;

        if (!PrefabUiSession.SharedDrawingFencePickTargetId.HasValue)
        {
            doc.Editor.WriteMessage("\n[BIM_PREFAB] Önce «Genel ve detay çizimler» penceresinden çizim seçip «Çizim sınırı seç» kullanın.");
            return;
        }

        DrawingInitService.EnsureInit(doc);
        var id = PrefabUiSession.SharedDrawingFencePickTargetId.Value;
        PrefabUiSession.SharedDrawingFencePickTargetId = null;

        if (!SharedDrawingFenceService.TryPickSingleClosedPolyline(doc, id, out var err) && err is not null)
            doc.Editor.WriteMessage("\n[BIM_PREFAB] " + err);

        SharedDrawingsPalette.ReloadIfOpen();
    }

    [CommandMethod("BIM_PREFAB_SHARED_DRAWINGS_PDF")]
    public void SharedDrawingsPdfBulk() =>
        PrefabExportInteraction.RunSharedDrawingsPdfBulk(AcadApp.DocumentManager.MdiActiveDocument, null);

    [CommandMethod("BIM_PREFAB_SHOW_PRODUCT")]
    public void ShowProduct()
    {
        var doc = AcadApp.DocumentManager.MdiActiveDocument;
        if (doc is null)
            return;

        var ed = doc.Editor;
        if (!PrefabUiSession.SelectedProductId.HasValue)
        {
            ed.WriteMessage("\n[BIM_PREFAB] Önce paletten bir ürün seçin.");
            return;
        }

        DrawingInitService.EnsureInit(doc);
        BimPrefabLog.Info($"Ürünü göster: {PrefabUiSession.SelectedProductId.Value:D}");
        if (ProductLocateService.TryShowProduct(doc, PrefabUiSession.SelectedProductId.Value, out var msg))
            ed.WriteMessage("\n[BIM_PREFAB] " + msg);
        else
            ed.WriteMessage("\n[BIM_PREFAB] " + (msg ?? "Gösterilemedi."));
    }

    [CommandMethod("BIM_PREFAB_EXPORT_MANIFEST")]
    public void ExportManifest()
    {
        var doc = AcadApp.DocumentManager.MdiActiveDocument;
        if (doc is null)
            return;

        using var dlg = new SaveFileDialog
        {
            Filter = "JSON (*.json)|*.json|Tüm dosyalar (*.*)|*.*",
            FileName = "manifest.json",
            Title = "Manifest kaydet",
        };

        if (dlg.ShowDialog() != DialogResult.OK)
            return;

        DrawingInitService.EnsureInit(doc);
        using var docLock = doc.LockDocument();
        using (var tr = doc.Database.TransactionManager.StartTransaction())
        {
            var products = new RegistryService().ListProducts(tr, doc.Database);
            var summary = new DocumentSummary
            {
                FileName = doc.Name,
                DatabasePath = doc.Database.Filename,
            };
            ManifestBuilder.WriteManifestJson(dlg.FileName, summary, products);
            tr.Commit();
        }

        doc.Editor.WriteMessage($"\n[BIM_PREFAB] Manifest yazıldı: {dlg.FileName}");
    }

    [CommandMethod("BIM_PREFAB_EXPORT_EXCEL")]
    public void ExportExcel() =>
        PrefabExportInteraction.RunCsvExport(AcadApp.DocumentManager.MdiActiveDocument, null);

    [CommandMethod("BIM_PREFAB_EXPORT_PDF_SINGLE")]
    public void ExportPdfSingle() =>
        PrefabExportInteraction.RunPdfSingle(AcadApp.DocumentManager.MdiActiveDocument, null);

    [CommandMethod("BIM_PREFAB_EXPORT_PDF_BULK")]
    public void ExportPdfBulk() =>
        PrefabExportInteraction.RunPdfBulk(AcadApp.DocumentManager.MdiActiveDocument, null);

    [CommandMethod("BIM_PREFAB_EXPORT_BUNDLE")]
    public void ExportBundle() =>
        PrefabExportInteraction.RunExportBundle(AcadApp.DocumentManager.MdiActiveDocument, null);

    private static bool RegistryHasProduct(Document doc, Guid productId)
    {
        using var tr = doc.Database.TransactionManager.StartTransaction();
        var ok = new RegistryService().TryGetProduct(tr, doc.Database, productId, out var p) && p is not null;
        tr.Commit();
        return ok;
    }
}
