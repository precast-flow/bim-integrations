using Autodesk.AutoCAD.DatabaseServices;

namespace BimPrefabExport.Core;

/// <summary>NamedObjects içinde ürün sözlüğünden ayrı tutulan ortak çizim listesi.</summary>
public static class SharedDrawingsRegistry
{
    public const string DictionaryName = "BIM_PREFAB_SHARED_DRAWINGS";
    private const string DataKey = "DOCUMENT";

    public static void EnsureRoot(Transaction tr, Database db)
    {
        _ = GetOrCreateDictionary(tr, db);
    }

    public static SharedDrawingsDocument Load(Transaction tr, Database db)
    {
        var nod = (DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForRead);
        if (!nod.Contains(DictionaryName))
            return new SharedDrawingsDocument();

        var root = (DBDictionary)tr.GetObject(nod.GetAt(DictionaryName), OpenMode.ForRead);
        if (!root.Contains(DataKey))
            return new SharedDrawingsDocument();

        var xrec = (Xrecord)tr.GetObject(root.GetAt(DataKey), OpenMode.ForRead);
        foreach (TypedValue tv in xrec.Data)
        {
            if (tv.TypeCode == (int)DxfCode.Text && tv.Value is string s)
                return SharedDrawingsDocument.Deserialize(s);
        }

        return new SharedDrawingsDocument();
    }

    public static void Save(Transaction tr, Database db, SharedDrawingsDocument document)
    {
        var root = (DBDictionary)tr.GetObject(GetOrCreateDictionary(tr, db), OpenMode.ForWrite);

        Xrecord xrec;
        if (root.Contains(DataKey))
            xrec = (Xrecord)tr.GetObject(root.GetAt(DataKey), OpenMode.ForWrite);
        else
        {
            xrec = new Xrecord();
            root.SetAt(DataKey, xrec);
            tr.AddNewlyCreatedDBObject(xrec, true);
        }

        xrec.Data = new ResultBuffer(new TypedValue((int)DxfCode.Text, SharedDrawingsDocument.Serialize(document)));
    }

    private static ObjectId GetOrCreateDictionary(Transaction tr, Database db)
    {
        var nod = (DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForWrite);
        if (nod.Contains(DictionaryName))
            return nod.GetAt(DictionaryName);

        var inner = new DBDictionary();
        var id = nod.SetAt(DictionaryName, inner);
        tr.AddNewlyCreatedDBObject(inner, true);
        return id;
    }
}
