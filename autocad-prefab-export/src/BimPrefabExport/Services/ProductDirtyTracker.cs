using System.Globalization;
using System.Text.Json;
using BimPrefabExport.Core;

namespace BimPrefabExport.Services;

public sealed class ProductChangeSummary
{
    public Guid ProductId { get; init; }
    public string ProductCode { get; init; } = "";
    public string ChangeType { get; init; } = "updated";
    public int RevisionBefore { get; init; }
    public int RevisionAfter { get; init; }
    public string? ContentHashBefore { get; init; }
    public string ContentHashAfter { get; init; } = "";
    public IReadOnlyList<string> ChangedFields { get; init; } = Array.Empty<string>();
    public string ChangedFieldsJson { get; init; } = "[]";
    public string SummaryText { get; init; } = "";
}

public static class ProductDirtyTracker
{
    public static bool IsDirty(ProductRecord record)
    {
        var current = record.ComputeContentHash();
        if (string.IsNullOrWhiteSpace(record.LastCommittedContentHash))
            return true;

        return !string.Equals(current, record.LastCommittedContentHash, StringComparison.OrdinalIgnoreCase);
    }

    public static void MarkCommitted(ProductRecord record)
    {
        record.LastCommittedContentHash = record.ComputeContentHash();
        record.LastCommittedSnapshotJson = SerializeSnapshot(record);
        record.LastCommittedAtUtc = DateTime.UtcNow;
        record.SyncStatus = "synced";
    }

    public static void ApplyRemoteBaseline(ProductRecord record, string? remoteContentHash)
    {
        if (!string.IsNullOrWhiteSpace(remoteContentHash))
            record.LastCommittedContentHash = remoteContentHash.Trim();
        else
            record.LastCommittedContentHash = record.ComputeContentHash();

        record.LastCommittedSnapshotJson = SerializeSnapshot(record);
        record.LastCommittedAtUtc = DateTime.UtcNow;
        record.SyncStatus = "synced";
    }

    public static ProductRecord? TryGetBaseline(ProductRecord current)
    {
        if (string.IsNullOrWhiteSpace(current.LastCommittedSnapshotJson))
            return null;

        return ProductRecord.Deserialize(current.LastCommittedSnapshotJson);
    }

    private static string SerializeSnapshot(ProductRecord record)
    {
        var clone = ProductRecord.DeepClone(record);
        clone.LastCommittedContentHash = null;
        clone.LastCommittedSnapshotJson = null;
        clone.LastCommittedAtUtc = null;
        clone.SyncStatus = "synced";
        return ProductRecord.Serialize(clone);
    }

    public static ProductChangeSummary BuildChangeSummary(ProductRecord current, ProductRecord? baseline)
    {
        var hashAfter = current.ComputeContentHash();
        var hashBefore = baseline?.ComputeContentHash();
        var fields = baseline is null
            ? new List<string> { "created" }
            : DetectChangedFields(baseline, current);

        var code = string.IsNullOrWhiteSpace(current.Code)
            ? current.DisplayName.Trim()
            : current.Code.Trim();
        if (string.IsNullOrWhiteSpace(code))
            code = current.ProductId.ToString("D");

        var summaryParts = baseline is null
            ? new List<string> { "yeni ürün" }
            : fields.Select(TranslateFieldLabel).ToList();

        return new ProductChangeSummary
        {
            ProductId = current.ProductId,
            ProductCode = code,
            ChangeType = baseline is null ? "created" : "updated",
            RevisionBefore = baseline?.Revision ?? 0,
            RevisionAfter = current.Revision,
            ContentHashBefore = hashBefore,
            ContentHashAfter = hashAfter,
            ChangedFields = fields,
            ChangedFieldsJson = JsonSerializer.Serialize(fields),
            SummaryText = string.Join(", ", summaryParts),
        };
    }

    public static ProductChangeSummary BuildChangeSummary(ProductRecord current) =>
        BuildChangeSummary(current, TryGetBaseline(current));

    private static List<string> DetectChangedFields(ProductRecord before, ProductRecord after)
    {
        var fields = new List<string>();
        if (!string.Equals(N(before.Code), N(after.Code), StringComparison.OrdinalIgnoreCase))
            fields.Add("code");
        if (!string.Equals(N(before.DisplayName), N(after.DisplayName), StringComparison.OrdinalIgnoreCase))
            fields.Add("displayName");
        if (Math.Abs(before.Quantity - after.Quantity) > 0.0001)
            fields.Add("quantity");
        if (before.Revision != after.Revision)
            fields.Add("revision");
        if (!string.Equals(N(before.Note), N(after.Note), StringComparison.Ordinal))
            fields.Add("note");
        if (!string.Equals(N(before.PrefabElementTypeId), N(after.PrefabElementTypeId), StringComparison.OrdinalIgnoreCase))
            fields.Add("elementType");
        if (!string.Equals(N(before.PrefabTypologyId), N(after.PrefabTypologyId), StringComparison.OrdinalIgnoreCase))
            fields.Add("typology");
        if (!string.Equals(N(before.ElementCategoryId), N(after.ElementCategoryId), StringComparison.OrdinalIgnoreCase))
            fields.Add("elementCategory");
        if (!AttributesEqual(before.Attributes, after.Attributes))
            fields.Add("dimensions");
        if (!MaterialsEqual(before.Materials, after.Materials))
            fields.Add("materials");
        if (!RebarsEqual(before.Rebars, after.Rebars))
            fields.Add("rebars");
        if (!DrawingFieldsEqual(before, after))
            fields.Add("drawings");
        return fields;
    }

    private static bool DrawingFieldsEqual(ProductRecord before, ProductRecord after)
    {
        before.NormalizeLinkFencesFromLegacy();
        after.NormalizeLinkFencesFromLegacy();
        if (!string.Equals(N(before.PlotPaperSize), N(after.PlotPaperSize), StringComparison.OrdinalIgnoreCase))
            return false;
        if (before.PlotLandscape != after.PlotLandscape)
            return false;
        if (!string.Equals(N(before.PlotStyleSheet), N(after.PlotStyleSheet), StringComparison.Ordinal))
            return false;
        if (before.LinkFences.Count != after.LinkFences.Count)
            return false;
        var bf = before.LinkFences.OrderBy(b => b.MinX).ThenBy(b => b.MinY).ToList();
        var af = after.LinkFences.OrderBy(b => b.MinX).ThenBy(b => b.MinY).ToList();
        for (var i = 0; i < bf.Count; i++)
        {
            if (Math.Abs(bf[i].MinX - af[i].MinX) > 0.001
                || Math.Abs(bf[i].MinY - af[i].MinY) > 0.001
                || Math.Abs(bf[i].MaxX - af[i].MaxX) > 0.001
                || Math.Abs(bf[i].MaxY - af[i].MaxY) > 0.001)
                return false;
        }

        var bp = (before.PdfDrawings ?? []).OrderBy(d => d.FenceId).ToList();
        var ap = (after.PdfDrawings ?? []).OrderBy(d => d.FenceId).ToList();
        if (bp.Count != ap.Count)
            return false;
        for (var i = 0; i < bp.Count; i++)
        {
            if (bp[i].FenceId != ap[i].FenceId)
                return false;
            if (!string.Equals(N(bp[i].PdfTitle), N(ap[i].PdfTitle), StringComparison.Ordinal))
                return false;
            if (bp[i].PdfRevision != ap[i].PdfRevision)
                return false;
            if (!string.Equals(N(bp[i].UploadedPdfRelativePath), N(ap[i].UploadedPdfRelativePath), StringComparison.OrdinalIgnoreCase))
                return false;
            if (!string.Equals(
                    PlotPaperSizes.NormalizeOrDefault(bp[i].PlotPaperSize),
                    PlotPaperSizes.NormalizeOrDefault(ap[i].PlotPaperSize),
                    StringComparison.OrdinalIgnoreCase))
                return false;
            if (bp[i].PlotLandscape != ap[i].PlotLandscape)
                return false;
        }

        return true;
    }

    private static bool AttributesEqual(IReadOnlyDictionary<string, string> a, IReadOnlyDictionary<string, string> b)
    {
        if (a.Count != b.Count)
            return false;
        foreach (var kv in a)
        {
            if (!b.TryGetValue(kv.Key, out var bv))
                return false;
            if (!string.Equals(N(kv.Value), N(bv), StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    private static bool MaterialsEqual(IReadOnlyList<MaterialLine>? a, IReadOnlyList<MaterialLine>? b)
    {
        var left = a ?? [];
        var right = b ?? [];
        if (left.Count != right.Count)
            return false;
        for (var i = 0; i < left.Count; i++)
        {
            var x = left[i];
            var y = right[i];
            if (!string.Equals(N(x.Category), N(y.Category), StringComparison.OrdinalIgnoreCase))
                return false;
            if (!string.Equals(N(x.Code), N(y.Code), StringComparison.OrdinalIgnoreCase))
                return false;
            if (Math.Abs(x.Quantity - y.Quantity) > 0.0001)
                return false;
        }

        return true;
    }

    private static bool RebarsEqual(IReadOnlyList<RebarLine>? a, IReadOnlyList<RebarLine>? b)
    {
        var left = a ?? [];
        var right = b ?? [];
        if (left.Count != right.Count)
            return false;
        for (var i = 0; i < left.Count; i++)
        {
            var x = left[i];
            var y = right[i];
            if (!string.Equals(N(x.PozNo), N(y.PozNo), StringComparison.OrdinalIgnoreCase))
                return false;
            if (x.DiameterMm != y.DiameterMm || x.Count != y.Count)
                return false;
        }

        return true;
    }

    private static string TranslateFieldLabel(string field) => field switch
    {
        "code" => "kod",
        "displayName" => "ad",
        "quantity" => "adet",
        "revision" => "revizyon",
        "note" => "not",
        "elementType" => "eleman tipi",
        "typology" => "tipoloji",
        "elementCategory" => "kategori",
        "dimensions" => "boyutlar",
        "materials" => "malzemeler",
        "rebars" => "demir",
        "drawings" => "çizim/PDF",
        "created" => "yeni",
        _ => field,
    };

    private static string N(string? s) => string.IsNullOrWhiteSpace(s) ? "" : s.Trim();
}
