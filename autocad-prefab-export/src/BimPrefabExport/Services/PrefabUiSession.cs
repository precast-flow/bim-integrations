namespace BimPrefabExport.Services;

/// <summary>Palet ve ribbon komutları arasında paylaşılan durum.</summary>
public static class PrefabUiSession
{
    private static readonly List<Guid> s_selected = new();
    private static readonly List<Guid> s_exportChecked = new();

    /// <summary>Liste satırı seçimi (Ctrl/Shift). Polyline / göster / tek satır düzenleme.</summary>
    public static IReadOnlyList<Guid> SelectedProductIds => s_selected;

    /// <summary>İşaret kutusu ile işaretlenen ürünler (CSV / paket dışa aktarma).</summary>
    public static IReadOnlyList<Guid> ExportCheckedProductIds => s_exportChecked;

    public static Guid? SelectedProductId => s_selected.Count > 0 ? s_selected[0] : null;

    public static void SetSelectedProductIds(IEnumerable<Guid> ids)
    {
        s_selected.Clear();
        foreach (var id in ids)
        {
            if (!s_selected.Contains(id))
                s_selected.Add(id);
        }
    }

    public static void SetSelectedProductId(Guid? id)
    {
        s_selected.Clear();
        if (id.HasValue)
            s_selected.Add(id.Value);
    }

    public static void SetExportCheckedProductIds(IEnumerable<Guid> ids)
    {
        s_exportChecked.Clear();
        foreach (var id in ids)
        {
            if (!s_exportChecked.Contains(id))
                s_exportChecked.Add(id);
        }
    }

    /// <summary>XData rol alanı (opsiyonel).</summary>
    public static string? DefaultRole { get; set; }

    /// <summary>Ortak çizim «Çizim sınırı seç» komutu için hedef kayıt kimliği.</summary>
    public static Guid? SharedDrawingFencePickTargetId { get; set; }
}
