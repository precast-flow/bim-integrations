using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;

namespace BimPrefabExport.Services;

/// <summary>
/// Ribbon <c>SendStringToExecute</c> sonrası pickfirst seçiminin kaybolmasını telafi eder.
/// </summary>
public static class PickFirstBridge
{
    private static readonly object Gate = new();
    private static Document? s_document;
    private static ObjectId[]? s_objectIds;
    private static string? s_forCommand;

    public static void StashFromImplied(Document doc, string globalCommandName)
    {
        var implied = doc.Editor.SelectImplied();
        if (implied.Status != PromptStatus.OK || implied.Value is null || implied.Value.Count == 0)
            return;

        var copy = implied.Value.GetObjectIds().ToArray();
        lock (Gate)
        {
            s_document = doc;
            s_objectIds = copy;
            s_forCommand = globalCommandName;
        }
    }

    public static bool TryConsume(Document doc, string globalCommandName, out ObjectId[]? objectIds)
    {
        objectIds = null;
        lock (Gate)
        {
            if (s_document != doc || s_objectIds is null || s_objectIds.Length == 0)
                return false;

            if (!string.Equals(s_forCommand, globalCommandName, StringComparison.OrdinalIgnoreCase))
            {
                s_document = null;
                s_objectIds = null;
                s_forCommand = null;
                return false;
            }

            objectIds = s_objectIds;
            s_document = null;
            s_objectIds = null;
            s_forCommand = null;
            return true;
        }
    }
}
