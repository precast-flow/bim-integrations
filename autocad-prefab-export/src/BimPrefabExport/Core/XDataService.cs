using System;
using Autodesk.AutoCAD.DatabaseServices;

namespace BimPrefabExport.Core;

public sealed class XDataService
{
    public void EnsureRegApp(Transaction tr, Database db)
    {
        var rat = (RegAppTable)tr.GetObject(db.RegAppTableId, OpenMode.ForWrite);
        if (rat.Has(PrefabConstants.XDataAppName))
            return;

        var rec = new RegAppTableRecord { Name = PrefabConstants.XDataAppName };
        rat.Add(rec);
        tr.AddNewlyCreatedDBObject(rec, true);
    }

    public void SetProductLink(Entity ent, Guid productId, string? role, Transaction tr, Database db)
    {
        EnsureRegApp(tr, db);
        ent.UpgradeOpen();
        ent.XData = new ResultBuffer(
            new TypedValue(1001, PrefabConstants.XDataAppName),
            new TypedValue(1000, productId.ToString("D")),
            new TypedValue(1000, role ?? "")
        );
    }

    /// <summary>Ürün XData bağlantısını kaldırır.</summary>
    public void ClearProductLink(Entity ent)
    {
        ent.UpgradeOpen();
        ent.XData = new ResultBuffer(new TypedValue(1001, PrefabConstants.XDataAppName));
    }

    public bool TryGetProductLink(Entity ent, out Guid productId, out string? role)
    {
        productId = Guid.Empty;
        role = null;

        var rb = ent.GetXDataForApplication(PrefabConstants.XDataAppName);
        if (rb == null)
            return false;

        try
        {
            string? idStr = null;
            string? roleStr = null;
            foreach (TypedValue tv in rb)
            {
                if (tv.TypeCode == 1000 && tv.Value is string s)
                {
                    if (idStr == null)
                        idStr = s;
                    else if (roleStr == null)
                        roleStr = s;
                }
            }

            if (string.IsNullOrWhiteSpace(idStr) || !Guid.TryParse(idStr, out productId))
                return false;

            role = string.IsNullOrEmpty(roleStr) ? null : roleStr;
            return true;
        }
        finally
        {
            rb.Dispose();
        }
    }
}
