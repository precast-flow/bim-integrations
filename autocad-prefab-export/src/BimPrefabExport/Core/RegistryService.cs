using System;
using System.Collections.Generic;
using System.Collections;
using Autodesk.AutoCAD.DatabaseServices;
using BimPrefabExport.Services;

namespace BimPrefabExport.Core;

public sealed class RegistryService
{
    public ObjectId GetOrCreateRegistryRoot(Transaction tr, Database db)
    {
        var nod = (DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForWrite);
        if (nod.Contains(PrefabConstants.RegistryDictionaryName))
            return nod.GetAt(PrefabConstants.RegistryDictionaryName);

        var products = new DBDictionary();
        var id = nod.SetAt(PrefabConstants.RegistryDictionaryName, products);
        tr.AddNewlyCreatedDBObject(products, true);
        return id;
    }

    public ObjectId SaveProduct(Transaction tr, Database db, ProductRecord record)
    {
        var rootId = GetOrCreateRegistryRoot(tr, db);
        var root = (DBDictionary)tr.GetObject(rootId, OpenMode.ForWrite);
        var key = record.ProductId.ToString("D");

        Xrecord xrec;
        if (root.Contains(key))
        {
            xrec = (Xrecord)tr.GetObject(root.GetAt(key), OpenMode.ForWrite);
        }
        else
        {
            xrec = new Xrecord();
            root.SetAt(key, xrec);
            tr.AddNewlyCreatedDBObject(xrec, true);
        }

        xrec.Data = new ResultBuffer(new TypedValue((int)DxfCode.Text, ProductRecord.Serialize(record)));
        return xrec.ObjectId;
    }

    public bool TryGetProduct(Transaction tr, Database db, Guid productId, out ProductRecord? record)
    {
        record = null;
        var nod = (DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForRead);
        if (!nod.Contains(PrefabConstants.RegistryDictionaryName))
            return false;

        var root = (DBDictionary)tr.GetObject(nod.GetAt(PrefabConstants.RegistryDictionaryName), OpenMode.ForRead);
        var key = productId.ToString("D");
        if (!root.Contains(key))
            return false;

        var xrec = (Xrecord)tr.GetObject(root.GetAt(key), OpenMode.ForRead);
        foreach (TypedValue tv in xrec.Data)
        {
            if (tv.TypeCode == (int)DxfCode.Text && tv.Value is string s)
            {
                record = ProductRecord.Deserialize(s);
                if (record is not null)
                {
                    record.NormalizeLinkFencesFromLegacy();
                    ProductPdfDrawingSync.NormalizeProductRecord(record);
                }

                return record != null;
            }
        }

        return false;
    }

    public IReadOnlyList<ProductRecord> ListProducts(Transaction tr, Database db)
    {
        var list = new List<ProductRecord>();
        var nod = (DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForRead);
        if (!nod.Contains(PrefabConstants.RegistryDictionaryName))
            return list;

        var root = (DBDictionary)tr.GetObject(nod.GetAt(PrefabConstants.RegistryDictionaryName), OpenMode.ForRead);
        foreach (DictionaryEntry de in root)
        {
            if (de.Value is not ObjectId oid)
                continue;

            var xrec = (Xrecord)tr.GetObject(oid, OpenMode.ForRead);
            foreach (TypedValue tv in xrec.Data)
            {
                if (tv.TypeCode == (int)DxfCode.Text && tv.Value is string s)
                {
                    var p = ProductRecord.Deserialize(s);
                    if (p != null)
                    {
                        p.NormalizeLinkFencesFromLegacy();
                        ProductPdfDrawingSync.NormalizeProductRecord(p);
                        list.Add(p);
                    }
                    break;
                }
            }
        }

        list.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));
        return list;
    }

    public bool DeleteProduct(Transaction tr, Database db, Guid productId)
    {
        var nod = (DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForWrite);
        if (!nod.Contains(PrefabConstants.RegistryDictionaryName))
            return false;

        var root = (DBDictionary)tr.GetObject(nod.GetAt(PrefabConstants.RegistryDictionaryName), OpenMode.ForWrite);
        var key = productId.ToString("D");
        if (!root.Contains(key))
            return false;

        var xrecId = root.GetAt(key);
        root.Remove(key);
        tr.GetObject(xrecId, OpenMode.ForWrite).Erase();
        return true;
    }
}
