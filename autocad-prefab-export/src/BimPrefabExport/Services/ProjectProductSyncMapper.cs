using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using BimPrefabExport.Core;

namespace BimPrefabExport.Services;

/// <summary>
/// <see cref="ProductRecord"/> → PrecastFlow <c>ProjectProduct</c> / <c>dimensionsJson</c>.
/// Frontend: projectProductMappers.ts + bimPrefabBundleImporter.ts ile uyumlu.
/// </summary>
public static class ProjectProductSyncMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static ProjectProductWriteDto MapToWriteDto(
        ProductRecord record,
        Guid projectId,
        string? drawingFileName,
        string? commitMessage = null,
        IReadOnlyList<ProductDrawingRevisionDto>? existingDrawingRevisions = null,
        IReadOnlyList<ProductPdfUploadResult>? pdfUploadResults = null,
        int? productionSequence = null)
    {
        record.NormalizeLinkFencesFromLegacy();
        ProductPdfDrawingSync.NormalizeProductRecord(record);

        var contentHash = record.ComputeContentHash();
        var dimensions = ParseNumericAttributes(record.Attributes);
        var materials = MapMaterials(record.Materials);
        var rebarSchedule = MapRebars(record.Rebars);
        var rebarSummary = rebarSchedule.Count > 0 ? ComputeRebarSummary(rebarSchedule) : null;
        var volumeM3 = RollupConcreteVolumeM3(materials);
        var rebarWeightKg = rebarSummary?.TotalWeightKg;
        var concreteWeightKg = EstimateConcreteWeightKg(materials, volumeM3);
        double? totalWeightKg = (rebarWeightKg ?? 0) + (concreteWeightKg ?? 0);
        if (totalWeightKg <= 0)
            totalWeightKg = null;

        var noteParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(drawingFileName))
            noteParts.Add($"AutoCAD: {drawingFileName.Trim()}");
        else
            noteParts.Add("AutoCAD sync");
        if (!string.IsNullOrWhiteSpace(record.Note))
            noteParts.Add(record.Note.Trim());
        if (!string.IsNullOrWhiteSpace(record.DisplayName)
            && !string.Equals(record.DisplayName.Trim(), record.Code.Trim(), StringComparison.OrdinalIgnoreCase))
            noteParts.Add($"Görünen ad: {record.DisplayName.Trim()}");

        var details = new StoredProductDetailsDto
        {
            CadProductId = record.ProductId.ToString("D"),
            Dimensions = dimensions,
            Materials = materials,
            RebarSchedule = rebarSchedule,
            RebarSummary = rebarSummary,
            TotalWeightKg = totalWeightKg,
            ConcreteWeightKg = concreteWeightKg,
            RebarWeightKg = rebarWeightKg,
            Definition = string.IsNullOrWhiteSpace(contentHash) ? null : $"contentHash:{contentHash}",
            Revision = record.Revision > 0 ? record.Revision : 1,
            Status = "active",
            DrawingRevisions = MapDrawingRevisions(record, commitMessage, existingDrawingRevisions, pdfUploadResults),
            CadContentHash = contentHash,
            LocalUpdatedAtUtc = (record.LocalUpdatedAtUtc ?? DateTime.UtcNow).ToString("o", CultureInfo.InvariantCulture),
            ServerUpdatedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
            SyncStatus = "synced",
        };

        var code = record.Code.Trim();
        if (string.IsNullOrWhiteSpace(code))
            code = record.DisplayName.Trim();
        if (string.IsNullOrWhiteSpace(code))
            code = record.ProductId.ToString("D");

        NormalizePrefabIdentityFields(record);

        return new ProjectProductWriteDto
        {
            ProjectId = projectId,
            Code = code,
            Name = code,
            ElementTypeCode = record.PrefabElementTypeId?.Trim() ?? "",
            TypologyCode = record.PrefabTypologyId?.Trim() ?? "",
            Source = "cad",
            LifecycleStatus = "tasarim",
            VolumeCubicM = volumeM3.HasValue ? Math.Round((decimal)volumeM3.Value, 3) : 0,
            Quantity = Math.Max(1, (int)Math.Round(record.Quantity <= 0 ? 1 : record.Quantity)),
            ProductionSequence = productionSequence,
            DimensionsJson = JsonSerializer.Serialize(details, JsonOptions),
            Notes = string.Join(" · ", noteParts),
        };
    }

    public static int? TryParseProductionSequenceFromAttributes(IReadOnlyDictionary<string, string> attributes)
    {
        foreach (var key in new[] { "productionSequence", "ProductionSequence", "uretimSirasi", "UretimSirasi", "assemblyOrder" })
        {
            if (!attributes.TryGetValue(key, out var raw))
                continue;
            if (int.TryParse(raw?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed > 0)
                return parsed;
        }

        return null;
    }

    public static int SuggestNextProductionSequence(IEnumerable<ProjectProductDto> remotes)
    {
        var max = 0;
        foreach (var remote in remotes)
        {
            if (remote.ProductionSequence is > max)
                max = remote.ProductionSequence.Value;
        }

        return max + 1;
    }

    public static string? TryGetCadProductId(string? dimensionsJson)
    {
        if (string.IsNullOrWhiteSpace(dimensionsJson))
            return null;
        try
        {
            var details = JsonSerializer.Deserialize<StoredProductDetailsDto>(dimensionsJson, JsonOptions);
            return string.IsNullOrWhiteSpace(details?.CadProductId) ? null : details.CadProductId.Trim();
        }
        catch
        {
            return null;
        }
    }

    public static string? TryGetContentHash(string? dimensionsJson)
    {
        var details = TryParseDetails(dimensionsJson);
        if (!string.IsNullOrWhiteSpace(details?.CadContentHash))
            return details.CadContentHash.Trim();
        var def = details?.Definition?.Trim();
        if (def is not null && def.StartsWith("contentHash:", StringComparison.OrdinalIgnoreCase))
            return def["contentHash:".Length..].Trim();
        return null;
    }

    public static DateTime? TryGetServerUpdatedAtUtc(string? dimensionsJson)
    {
        var details = TryParseDetails(dimensionsJson);
        if (details?.ServerUpdatedAtUtc is null)
            return null;
        return DateTime.TryParse(details.ServerUpdatedAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt)
            ? dt
            : null;
    }

    /// <summary>Sunucu kaydını yerel ürüne uygular; mevcut linkFences korunur.</summary>
    public static ProductRecord MapFromRemoteDto(ProjectProductDto remote, ProductRecord? existingLocal)
    {
        var details = TryParseDetails(remote.DimensionsJson) ?? new StoredProductDetailsDto();
        var cadIdText = details.CadProductId;
        Guid productId;
        if (!string.IsNullOrWhiteSpace(cadIdText) && Guid.TryParse(cadIdText, out var parsed))
            productId = parsed;
        else if (existingLocal is not null)
            productId = existingLocal.ProductId;
        else
            productId = Guid.NewGuid();

        var record = existingLocal is not null ? ProductRecord.DeepClone(existingLocal) : new ProductRecord { ProductId = productId };
        record.ProductId = productId;
        record.ServerProductId = remote.Id;
        record.Code = remote.Code?.Trim().ToUpperInvariant() ?? "";
        record.DisplayName = string.IsNullOrWhiteSpace(remote.Name) ? record.Code : remote.Name.Trim();
        record.Quantity = remote.Quantity > 0 ? remote.Quantity : 1;
        record.Revision = details.Revision ?? record.Revision;
        if (record.Revision <= 0)
            record.Revision = 1;
        record.Note = ExtractNoteFromRemoteNotes(remote.Notes);
        record.PrefabElementTypeId = string.IsNullOrWhiteSpace(remote.ElementTypeCode) ? null : remote.ElementTypeCode.Trim();
        record.PrefabTypologyId = string.IsNullOrWhiteSpace(remote.TypologyCode) ? null : remote.TypologyCode.Trim();
        NormalizePrefabIdentityFields(record);
        record.SyncStatus = "synced";
        record.LocalUpdatedAtUtc = remote.UpdatedAtUtc ?? DateTime.UtcNow;

        record.Attributes.Clear();
        if (details.Dimensions is not null)
        {
            foreach (var kv in details.Dimensions)
                record.Attributes[kv.Key] = kv.Value.ToString(CultureInfo.InvariantCulture);
        }
        if (remote.ProductionSequence is > 0)
            record.Attributes["productionSequence"] = remote.ProductionSequence.Value.ToString(CultureInfo.InvariantCulture);

        record.Materials = MapMaterialsFromRemote(details.Materials);
        record.Rebars = MapRebarsFromRemote(details.RebarSchedule);
        record.ServerDrawingRevisions = MapDrawingRevisionsToLocal(details.DrawingRevisions);
        return record;
    }

    public static IReadOnlyList<ProductDrawingRevisionDto> TryGetDrawingRevisions(string? dimensionsJson)
    {
        var details = TryParseDetails(dimensionsJson);
        return details?.DrawingRevisions ?? [];
    }

    public static List<ServerDrawingRevision> ToServerDrawingRevisions(IReadOnlyList<ProductDrawingRevisionDto>? revisions) =>
        MapDrawingRevisionsToLocal(revisions);

    public static List<ProductDrawingRevisionDto> FromServerDrawingRevisions(IReadOnlyList<ServerDrawingRevision> revisions) =>
        revisions.Select(r => new ProductDrawingRevisionDto
        {
            Id = r.Id,
            Revision = r.Revision,
            Title = r.Title,
            UpdatedAt = r.UpdatedAt,
            UpdatedBy = r.UpdatedBy,
            ChangeNote = r.ChangeNote,
            PdfUrl = r.PdfUrl,
            FileName = r.FileName,
            FileId = r.FileId,
        }).ToList();

    private static List<ServerDrawingRevision> MapDrawingRevisionsToLocal(IReadOnlyList<ProductDrawingRevisionDto>? revisions)
    {
        if (revisions is not { Count: > 0 })
            return [];

        return revisions.Select(r => new ServerDrawingRevision
        {
            Id = r.Id,
            Revision = r.Revision,
            Title = r.Title,
            UpdatedAt = r.UpdatedAt,
            UpdatedBy = r.UpdatedBy,
            ChangeNote = r.ChangeNote,
            PdfUrl = r.PdfUrl,
            FileName = r.FileName,
            FileId = r.FileId,
        }).ToList();
    }

    private static StoredProductDetailsDto? TryParseDetails(string? dimensionsJson)
    {
        if (string.IsNullOrWhiteSpace(dimensionsJson))
            return null;
        try
        {
            return JsonSerializer.Deserialize<StoredProductDetailsDto>(dimensionsJson, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private static string ExtractNoteFromRemoteNotes(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
            return "";
        var parts = notes.Split('·', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        foreach (var p in parts)
        {
            if (p.StartsWith("AutoCAD:", StringComparison.OrdinalIgnoreCase))
                continue;
            if (p.StartsWith("Görünen ad:", StringComparison.OrdinalIgnoreCase))
                continue;
            return p;
        }

        return parts.Length > 0 ? parts[^1] : notes.Trim();
    }

    private static List<MaterialLine> MapMaterialsFromRemote(IReadOnlyList<ProductMaterialEntryDto>? materials)
    {
        var list = new List<MaterialLine>();
        if (materials is null)
            return list;

        foreach (var m in materials)
        {
            list.Add(new MaterialLine
            {
                Category = m.Category,
                Code = m.Specification,
                Description = m.Name,
                Quantity = m.Quantity,
                Unit = m.Unit,
                MaterialCatalogCode = m.Specification,
            });
        }

        return list;
    }

    private static List<RebarLine> MapRebarsFromRemote(IReadOnlyList<ProductRebarEntryDto>? rows)
    {
        var list = new List<RebarLine>();
        if (rows is null)
            return list;

        foreach (var r in rows)
        {
            list.Add(new RebarLine
            {
                PozNo = r.Position,
                DiameterMm = r.DiameterMm,
                SteelGrade = r.SteelGrade,
                Shape = r.Shape,
                DevelopedLengthMm = r.DevelopedLengthMm,
                Count = r.Count,
                TotalWeightKg = r.TotalWeightKg,
                Notes = r.Notes ?? "",
            });
        }

        return list;
    }

    /// <summary>Tipoloji → eleman tipi ve kategori türetimi; sunucu/çekme sonrası combo seçimleri için.</summary>
    public static void NormalizePrefabIdentityFields(ProductRecord record)
    {
        if (string.IsNullOrWhiteSpace(record.PrefabElementTypeId)
            && !string.IsNullOrWhiteSpace(record.PrefabTypologyId)
            && AttributeCatalogService.Default.TryGetTypology(record.PrefabTypologyId) is { ElementTypeId: { Length: > 0 } etId })
        {
            record.PrefabElementTypeId = etId.Trim();
        }

        if (!string.IsNullOrWhiteSpace(record.PrefabElementTypeId))
        {
            record.ElementCategoryId ??=
                AttributeCatalogService.Default.GetCategoryIdForElementType(record.PrefabElementTypeId);
        }
    }

    public static void ApplyServerMetaToRecord(ProductRecord record, ProjectProductDto remote)
    {
        record.ServerProductId = remote.Id;
        record.LocalUpdatedAtUtc = remote.UpdatedAtUtc ?? DateTime.UtcNow;
        record.ServerDrawingRevisions = MapDrawingRevisionsToLocal(TryGetDrawingRevisions(remote.DimensionsJson));
        ProductDirtyTracker.ApplyRemoteBaseline(record, TryGetContentHash(remote.DimensionsJson));
    }

    private static Dictionary<string, double> ParseNumericAttributes(IReadOnlyDictionary<string, string> attributes)
    {
        var dimensions = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in attributes)
        {
            if (double.TryParse(kv.Value?.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var n))
                dimensions[kv.Key] = n;
        }

        return dimensions;
    }

    private static List<ProductMaterialEntryDto> MapMaterials(IReadOnlyList<MaterialLine>? lines)
    {
        var list = new List<ProductMaterialEntryDto>();
        if (lines is null)
            return list;

        for (var i = 0; i < lines.Count; i++)
        {
            var m = lines[i];
            var unit = string.IsNullOrWhiteSpace(m.Unit) ? "ea" : m.Unit.Trim();
            var catalogCode = !string.IsNullOrWhiteSpace(m.MaterialCatalogCode)
                ? m.MaterialCatalogCode.Trim()
                : m.Code?.Trim() ?? "";
            var name = !string.IsNullOrWhiteSpace(m.Description) ? m.Description.Trim() : catalogCode;
            if (string.IsNullOrWhiteSpace(name))
                name = "Malzeme";

            double? volumeM3 = null;
            if (unit.Equals("m3", StringComparison.OrdinalIgnoreCase) || unit == "m³")
                volumeM3 = m.Quantity;

            list.Add(new ProductMaterialEntryDto
            {
                Id = $"cad-mat-{i}",
                Category = string.IsNullOrWhiteSpace(m.Category) ? "Malzeme" : m.Category.Trim(),
                Name = name,
                Specification = catalogCode,
                Quantity = m.Quantity,
                Unit = unit,
                VolumeM3 = volumeM3,
            });
        }

        return list;
    }

    private static List<ProductRebarEntryDto> MapRebars(IReadOnlyList<RebarLine>? lines)
    {
        var list = new List<ProductRebarEntryDto>();
        if (lines is null)
            return list;

        for (var i = 0; i < lines.Count; i++)
        {
            var r = lines[i];
            RebarWeightHelper.NormalizeRebarRow(r, i + 1);
            list.Add(new ProductRebarEntryDto
            {
                Id = $"cad-rebar-{i}",
                Position = string.IsNullOrWhiteSpace(r.PozNo) ? (i + 1).ToString(CultureInfo.InvariantCulture) : r.PozNo.Trim(),
                DiameterMm = r.DiameterMm ?? 0,
                SteelGrade = string.IsNullOrWhiteSpace(r.SteelGrade) ? "B500C" : r.SteelGrade.Trim(),
                Shape = string.IsNullOrWhiteSpace(r.Shape) ? "straight" : r.Shape.Trim(),
                DevelopedLengthMm = r.DevelopedLengthMm ?? RebarWeightHelper.ComputeDevelopedLengthMm(r),
                Count = Math.Max(1, (int)Math.Round(r.Count <= 0 ? 1 : r.Count)),
                TotalWeightKg = r.TotalWeightKg ?? RebarWeightHelper.ComputeRowWeightKg(r),
                Notes = string.IsNullOrWhiteSpace(r.Notes) ? null : r.Notes.Trim(),
            });
        }

        return list;
    }

    private static ProductRebarSummaryDto ComputeRebarSummary(IReadOnlyList<ProductRebarEntryDto> rows)
    {
        double totalWeightKg = 0;
        var straightBarCount = 0;
        var shapedBarCount = 0;
        double totalDevelopedLengthM = 0;

        foreach (var r in rows)
        {
            totalWeightKg += r.TotalWeightKg;
            totalDevelopedLengthM += r.DevelopedLengthMm * r.Count / 1000.0;
            if (string.Equals(r.Shape, "straight", StringComparison.OrdinalIgnoreCase))
                straightBarCount += r.Count;
            else
                shapedBarCount += r.Count;
        }

        return new ProductRebarSummaryDto
        {
            TotalWeightKg = Math.Round(totalWeightKg, 3),
            StraightBarCount = straightBarCount,
            ShapedBarCount = shapedBarCount,
            TotalDevelopedLengthM = totalDevelopedLengthM,
        };
    }

    private static double? RollupConcreteVolumeM3(IReadOnlyList<ProductMaterialEntryDto> materials)
    {
        double total = 0;
        var found = false;
        foreach (var m in materials)
        {
            var vol = m.VolumeM3;
            if (vol is null or <= 0)
            {
                var unit = m.Unit.ToLowerInvariant();
                if (unit is "m3" or "m³")
                    vol = m.Quantity;
            }

            if (vol is > 0)
            {
                total += vol.Value;
                found = true;
            }
        }

        return found ? Math.Round(total, 3) : null;
    }

    private static double? EstimateConcreteWeightKg(IReadOnlyList<ProductMaterialEntryDto> materials, double? volumeM3)
    {
        if (volumeM3 is > 0)
            return Math.Round(volumeM3.Value * 2400.0, 1);

        foreach (var m in materials)
        {
            var cat = m.Category.ToLowerInvariant();
            if (!cat.Contains("beton") && cat != "concrete")
                continue;
            if (m.VolumeM3 is > 0)
                return Math.Round(m.VolumeM3.Value * 2400.0, 1);
            if (m.Unit.Equals("m3", StringComparison.OrdinalIgnoreCase) || m.Unit == "m³")
                return Math.Round(m.Quantity * 2400.0, 1);
        }

        return null;
    }

    private static List<ProductDrawingRevisionDto>? MapDrawingRevisions(
        ProductRecord record,
        string? commitMessage,
        IReadOnlyList<ProductDrawingRevisionDto>? existingDrawingRevisions,
        IReadOnlyList<ProductPdfUploadResult>? pdfUploadResults)
    {
        if (record.PdfDrawings is not { Count: > 0 } && pdfUploadResults is not { Count: > 0 })
            return existingDrawingRevisions is { Count: > 0 } ? existingDrawingRevisions.ToList() : null;

        var list = existingDrawingRevisions?.Select(CloneDrawingRevision).ToList() ?? new List<ProductDrawingRevisionDto>();
        var uploadsByFence = (pdfUploadResults ?? [])
            .GroupBy(u => u.FenceId)
            .ToDictionary(g => g.Key, g => g.Last());

        ProductPdfDrawingSync.NormalizeProductRecord(record);
        for (var i = 0; i < record.PdfDrawings.Count; i++)
        {
            var d = record.PdfDrawings[i];
            var title = string.IsNullOrWhiteSpace(d.PdfTitle) ? $"Çizim {i + 1}" : d.PdfTitle.Trim();
            var rev = d.PdfRevision > 0 ? $"R{d.PdfRevision}" : "R1";
            uploadsByFence.TryGetValue(d.FenceId, out var upload);

            if (upload is not null)
            {
                list.RemoveAll(r =>
                    string.Equals(r.Title, upload.Title, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(r.Revision, upload.Revision, StringComparison.OrdinalIgnoreCase)
                    && string.IsNullOrWhiteSpace(r.PdfUrl));
                list.Insert(0, new ProductDrawingRevisionDto
                {
                    Id = $"dr-cad-{record.ProductId:D}-{d.FenceId:N}-{DateTime.UtcNow.Ticks}",
                    Revision = upload.Revision,
                    Title = upload.Title,
                    UpdatedAt = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                    UpdatedBy = "AutoCAD",
                    ChangeNote = commitMessage?.Trim() ?? "",
                    PdfUrl = upload.PdfUrl,
                    FileName = upload.FileName,
                    FileId = upload.FileId == Guid.Empty ? null : upload.FileId.ToString("D"),
                });
                continue;
            }

            if (existingDrawingRevisions?.Any(r =>
                    string.Equals(r.Title, title, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(r.Revision, rev, StringComparison.OrdinalIgnoreCase)) == true)
                continue;

            list.Add(new ProductDrawingRevisionDto
            {
                Id = $"dr-cad-{record.ProductId:D}-{i}",
                Revision = rev,
                Title = title,
                UpdatedAt = (d.LastPdfExportUtc ?? DateTime.UtcNow).ToString("o", CultureInfo.InvariantCulture),
                UpdatedBy = "AutoCAD",
                ChangeNote = "",
                FileName = string.IsNullOrWhiteSpace(d.UploadedPdfRelativePath)
                    ? ProductPdfExportService.BuildProductPdfFileName(record, d)
                    : d.UploadedPdfRelativePath.Trim().Replace('\\', '/'),
            });
        }

        return list.Count > 0 ? list : null;
    }

    private static ProductDrawingRevisionDto CloneDrawingRevision(ProductDrawingRevisionDto source) =>
        new()
        {
            Id = source.Id,
            Revision = source.Revision,
            Title = source.Title,
            UpdatedAt = source.UpdatedAt,
            UpdatedBy = source.UpdatedBy,
            ChangeNote = source.ChangeNote,
            PdfUrl = source.PdfUrl,
            FileName = source.FileName,
            FileId = source.FileId,
        };

    private sealed class StoredProductDetailsDto
    {
        public string? CadProductId { get; set; }
        public string? CadContentHash { get; set; }
        public string? ServerUpdatedAtUtc { get; set; }
        public string? LocalUpdatedAtUtc { get; set; }
        public string? SyncStatus { get; set; }
        public Dictionary<string, double>? Dimensions { get; set; }
        public List<ProductMaterialEntryDto>? Materials { get; set; }
        public List<ProductRebarEntryDto>? RebarSchedule { get; set; }
        public ProductRebarSummaryDto? RebarSummary { get; set; }
        public double? TotalWeightKg { get; set; }
        public double? ConcreteWeightKg { get; set; }
        public double? RebarWeightKg { get; set; }
        public string? Definition { get; set; }
        public int? Revision { get; set; }
        public string? Status { get; set; }
        public List<ProductDrawingRevisionDto>? DrawingRevisions { get; set; }
    }

    private sealed class ProductMaterialEntryDto
    {
        public string Id { get; set; } = "";
        public string Category { get; set; } = "";
        public string Name { get; set; } = "";
        public string Specification { get; set; } = "";
        public double Quantity { get; set; }
        public string Unit { get; set; } = "";
        public double? VolumeM3 { get; set; }
    }

    private sealed class ProductRebarEntryDto
    {
        public string Id { get; set; } = "";
        public string Position { get; set; } = "";
        public double DiameterMm { get; set; }
        public string SteelGrade { get; set; } = "B500C";
        public string Shape { get; set; } = "straight";
        public double DevelopedLengthMm { get; set; }
        public int Count { get; set; } = 1;
        public double TotalWeightKg { get; set; }
        public string? Notes { get; set; }
    }

    private sealed class ProductRebarSummaryDto
    {
        public double TotalWeightKg { get; set; }
        public int StraightBarCount { get; set; }
        public int ShapedBarCount { get; set; }
        public double TotalDevelopedLengthM { get; set; }
    }

    public sealed class ProductDrawingRevisionDto
    {
        public string Id { get; set; } = "";
        public string Revision { get; set; } = "";
        public string Title { get; set; } = "";
        public string UpdatedAt { get; set; } = "";
        public string UpdatedBy { get; set; } = "";
        public string ChangeNote { get; set; } = "";
        public string? PdfUrl { get; set; }
        public string? FileName { get; set; }
        public string? FileId { get; set; }
    }
}
