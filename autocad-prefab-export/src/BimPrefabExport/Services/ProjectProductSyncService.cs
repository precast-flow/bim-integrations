using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using BimPrefabExport.Core;

namespace BimPrefabExport.Services;

public enum SyncConflictChoice
{
    UseServer,
    UseLocal,
    Skip,
}

public sealed class ProjectProductSyncResult
{
    public int Created { get; set; }
    public int Updated { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }
    public int Pulled { get; set; }
    public int Conflicts { get; set; }
    public List<string> Errors { get; } = new();
    public List<string> Warnings { get; } = new();

    public int TotalProcessed => Created + Updated + Skipped;
}

public static class ProjectProductSyncService
{
    public static async Task<ProjectProductSyncResult> PushCurrentDrawingAsync(
        PrecastApiClient client,
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var result = new ProjectProductSyncResult();
        var doc = AcadApp.DocumentManager.MdiActiveDocument;
        if (doc is null)
        {
            result.Errors.Add("Aktif AutoCAD çizimi yok.");
            result.Failed = 1;
            return result;
        }

        IReadOnlyList<ProductRecord> localProducts;
        using (doc.LockDocument())
        using (var tr = doc.Database.TransactionManager.StartTransaction())
        {
            localProducts = new RegistryService().ListProducts(tr, doc.Database);
            tr.Commit();
        }

        if (localProducts.Count == 0)
        {
            result.Warnings.Add("Çizimde gönderilecek ürün bulunamadı.");
            return result;
        }

        var remoteProducts = await client.ListProjectProductsAsync(projectId, cancellationToken).ConfigureAwait(false);
        var lookup = BuildRemoteLookup(remoteProducts);
        var drawingFileName = string.IsNullOrWhiteSpace(doc.Name) ? null : System.IO.Path.GetFileName(doc.Name);

        foreach (var record in localProducts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await PushSingleRecordAsync(doc, client, projectId, record, lookup, drawingFileName, result, cancellationToken)
                .ConfigureAwait(false);
        }

        return result;
    }

    public static async Task<ProjectProductSyncResult> PushSingleProductAsync(
        PrecastApiClient client,
        Guid projectId,
        ProductRecord record,
        Func<string, SyncConflictChoice>? resolveConflict = null,
        CancellationToken cancellationToken = default)
    {
        var result = new ProjectProductSyncResult();
        var doc = AcadApp.DocumentManager.MdiActiveDocument;
        if (doc is null)
        {
            result.Errors.Add("Aktif AutoCAD çizimi yok.");
            result.Failed = 1;
            return result;
        }

        if (string.IsNullOrWhiteSpace(record.Code?.Trim()) && string.IsNullOrWhiteSpace(record.DisplayName?.Trim()))
        {
            result.Skipped++;
            result.Warnings.Add("Ürün kodu veya adı yok; sunucuya gönderilmedi.");
            return result;
        }

        var remoteProducts = await client.ListProjectProductsAsync(projectId, cancellationToken).ConfigureAwait(false);
        var lookup = BuildRemoteLookup(remoteProducts);
        var drawingFileName = string.IsNullOrWhiteSpace(doc.Name) ? null : System.IO.Path.GetFileName(doc.Name);

        if (resolveConflict is not null)
        {
            var existing = FindExisting(record, lookup);
            if (existing is not null && HasConflict(record, existing))
            {
                var label = record.Code?.Trim() ?? record.ProductId.ToString("D");
                var choice = resolveConflict(label);
                if (choice == SyncConflictChoice.UseServer)
                {
                    result.Skipped++;
                    result.Warnings.Add($"{label}: çakışma — sunucu sürümü korundu (yerel push atlandı).");
                    return result;
                }

                if (choice == SyncConflictChoice.Skip)
                {
                    result.Skipped++;
                    return result;
                }
            }
        }

        await PushSingleRecordAsync(doc, client, projectId, record, lookup, drawingFileName, result, cancellationToken)
            .ConfigureAwait(false);
        return result;
    }

    public static async Task<ProjectProductSyncResult> PullAndMergeAsync(
        PrecastApiClient client,
        Guid projectId,
        Func<string, SyncConflictChoice>? resolveConflict = null,
        CancellationToken cancellationToken = default)
    {
        var result = new ProjectProductSyncResult();
        var doc = AcadApp.DocumentManager.MdiActiveDocument;
        if (doc is null)
        {
            result.Errors.Add("Aktif AutoCAD çizimi yok.");
            result.Failed = 1;
            return result;
        }

        var remoteProducts = await client.ListProjectProductsAsync(projectId, cancellationToken).ConfigureAwait(false);
        if (remoteProducts.Count == 0)
        {
            result.Warnings.Add("Sunucuda bu projeye ait ürün bulunamadı.");
            return result;
        }

        DrawingInitService.EnsureInit(doc);
        using var docLock = doc.LockDocument();
        using var tr = doc.Database.TransactionManager.StartTransaction();
        var registry = new RegistryService();
        var localProducts = registry.ListProducts(tr, doc.Database).ToList();
        var localByCadId = localProducts.ToDictionary(p => p.ProductId.ToString("D"), StringComparer.OrdinalIgnoreCase);
        var localByServerId = localProducts
            .Where(p => p.ServerProductId.HasValue)
            .ToDictionary(p => p.ServerProductId!.Value, p => p);
        var localByCode = localProducts
            .Where(p => !string.IsNullOrWhiteSpace(p.Code))
            .GroupBy(p => p.Code.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var remote in remoteProducts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var cadId = ProjectProductSyncMapper.TryGetCadProductId(remote.DimensionsJson);
            ProductRecord? existing = null;
            if (!string.IsNullOrWhiteSpace(cadId) && localByCadId.TryGetValue(cadId, out var byCad))
                existing = byCad;
            else if (localByServerId.TryGetValue(remote.Id, out var byServer))
                existing = byServer;
            else if (!string.IsNullOrWhiteSpace(remote.Code) && localByCode.TryGetValue(remote.Code.Trim(), out var byCode))
                existing = byCode;

            if (existing is null)
            {
                var created = ProjectProductSyncMapper.MapFromRemoteDto(remote, null);
                ProductDirtyTracker.ApplyRemoteBaseline(created, ProjectProductSyncMapper.TryGetContentHash(remote.DimensionsJson));
                registry.SaveProduct(tr, doc.Database, created);
                localByCadId[created.ProductId.ToString("D")] = created;
                if (created.ServerProductId.HasValue)
                    localByServerId[created.ServerProductId.Value] = created;
                result.Pulled++;
                BimPrefabLog.Info($"Sunucudan eklendi: {created.Code}");
                continue;
            }

            if (HasConflict(existing, remote))
            {
                result.Conflicts++;
                var label = existing.Code?.Trim() ?? existing.ProductId.ToString("D");
                var choice = resolveConflict?.Invoke(label) ?? SyncConflictChoice.UseServer;
                if (choice == SyncConflictChoice.UseLocal)
                {
                    result.Skipped++;
                    continue;
                }

                if (choice == SyncConflictChoice.Skip)
                {
                    result.Skipped++;
                    continue;
                }
            }
            else if (IsLocalNewer(existing, remote))
            {
                result.Skipped++;
                continue;
            }

            var fences = existing.LinkFences?.ToList() ?? [];
            var pdfDrawings = existing.PdfDrawings?.ToList() ?? [];
            var merged = ProjectProductSyncMapper.MapFromRemoteDto(remote, existing);
            merged.LinkFences = fences;
            merged.PdfDrawings = pdfDrawings;
            ProductDirtyTracker.ApplyRemoteBaseline(merged, ProjectProductSyncMapper.TryGetContentHash(remote.DimensionsJson));
            registry.SaveProduct(tr, doc.Database, merged);
            result.Pulled++;
            BimPrefabLog.Info($"Sunucudan güncellendi: {merged.Code}");
        }

        tr.Commit();
        return result;
    }

    public static async Task TryUploadPdfsAsync(
        PrecastApiClient client,
        Guid projectId,
        ProductRecord record,
        Document doc,
        string? commitMessage = null,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (record.ServerProductId is not Guid serverId || record.PdfDrawings is not { Count: > 0 })
            return;

        var uploads = await ProductPdfSyncService.ExportAndUploadAllDrawingsAsync(
            doc,
            client,
            projectId,
            serverId,
            record,
            progress,
            cancellationToken).ConfigureAwait(false);

        if (uploads.Count == 0)
            return;

        var existingRevisions = ProjectProductSyncMapper.FromServerDrawingRevisions(record.ServerDrawingRevisions);

        var drawingFileName = string.IsNullOrWhiteSpace(doc.Name) ? null : System.IO.Path.GetFileName(doc.Name);
        var productionSequence = ProjectProductSyncMapper.TryParseProductionSequenceFromAttributes(record.Attributes);
        var payload = ProjectProductSyncMapper.MapToWriteDto(
            record,
            projectId,
            drawingFileName,
            commitMessage,
            existingRevisions,
            uploads,
            productionSequence);
        payload.Id = serverId;
        payload.ProjectId = projectId;

        var saved = await client.ReplaceProjectProductAsync(serverId, payload, cancellationToken).ConfigureAwait(false);
        record.ServerDrawingRevisions = ProjectProductSyncMapper.ToServerDrawingRevisions(
            ProjectProductSyncMapper.TryGetDrawingRevisions(saved.DimensionsJson));
    }

    private static async Task PushSingleRecordAsync(
        Document doc,
        PrecastApiClient client,
        Guid projectId,
        ProductRecord record,
        RemoteLookup lookup,
        string? drawingFileName,
        ProjectProductSyncResult result,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(record.Code?.Trim()) && string.IsNullOrWhiteSpace(record.DisplayName?.Trim()))
        {
            result.Skipped++;
            result.Warnings.Add($"Ürün {record.ProductId:D}: kod/ad yok, atlandı.");
            return;
        }

        try
        {
            record.LocalUpdatedAtUtc = DateTime.UtcNow;
            var cadKey = record.ProductId.ToString("D");
            var existing = FindExisting(record, lookup);
            var existingRevisions = existing is null
                ? []
                : ProjectProductSyncMapper.TryGetDrawingRevisions(existing.DimensionsJson);
            var productionSequence = ProjectProductSyncMapper.TryParseProductionSequenceFromAttributes(record.Attributes)
                ?? existing?.ProductionSequence;
            var payload = ProjectProductSyncMapper.MapToWriteDto(
                record,
                projectId,
                drawingFileName,
                null,
                existingRevisions,
                null,
                productionSequence);

            ProjectProductDto saved;
            if (existing is null)
            {
                saved = await client.CreateProjectProductAsync(payload, cancellationToken).ConfigureAwait(false);
                lookup.ByCadId[cadKey] = saved;
                if (!string.IsNullOrWhiteSpace(saved.Code))
                    lookup.ByCode[saved.Code.Trim()] = saved;
                result.Created++;
                BimPrefabLog.Info($"Sunucuya eklendi: {payload.Code} ({cadKey})");
            }
            else
            {
                payload.Id = existing.Id;
                payload.ProjectId = projectId;
                saved = await client.ReplaceProjectProductAsync(existing.Id, payload, cancellationToken).ConfigureAwait(false);
                lookup.ByCadId[cadKey] = saved;
                if (!string.IsNullOrWhiteSpace(saved.Code))
                    lookup.ByCode[saved.Code.Trim()] = saved;
                result.Updated++;
                BimPrefabLog.Info($"Sunucuda güncellendi: {payload.Code} ({cadKey})");
            }

            ProductRecord recordForPdf = record;
            using (doc.LockDocument())
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                if (new RegistryService().TryGetProduct(tr, doc.Database, record.ProductId, out var current) && current is not null)
                {
                    ProjectProductSyncMapper.ApplyServerMetaToRecord(current, saved);
                    ProductDirtyTracker.MarkCommitted(current);
                    new RegistryService().SaveProduct(tr, doc.Database, current);
                    recordForPdf = current;
                }

                tr.Commit();
            }

            await TryUploadPdfsAsync(client, projectId, recordForPdf, doc, null, null, cancellationToken)
                .ConfigureAwait(false);

            using (doc.LockDocument())
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                if (new RegistryService().TryGetProduct(tr, doc.Database, record.ProductId, out var updated)
                    && updated is not null)
                {
                    updated.ServerDrawingRevisions = recordForPdf.ServerDrawingRevisions;
                    new RegistryService().SaveProduct(tr, doc.Database, updated);
                }

                tr.Commit();
            }
        }
        catch (PrecastApiException ex)
        {
            result.Failed++;
            var label = string.IsNullOrWhiteSpace(record.Code) ? record.ProductId.ToString("D") : record.Code.Trim();
            result.Errors.Add($"{label}: {ex.Message}");
            BimPrefabLog.Info($"Sunucu sync hatası ({label}): {ex.Message}");
        }
        catch (Exception ex)
        {
            result.Failed++;
            var label = string.IsNullOrWhiteSpace(record.Code) ? record.ProductId.ToString("D") : record.Code.Trim();
            result.Errors.Add($"{label}: {ex.Message}");
            BimPrefabLog.Info($"Sync hatası ({label}): {ex}");
        }
    }

    private static bool HasConflict(ProductRecord local, ProjectProductDto remote)
    {
        if (string.Equals(local.SyncStatus, "conflict", StringComparison.OrdinalIgnoreCase))
            return true;

        var localHash = local.ComputeContentHash();
        var remoteHash = ProjectProductSyncMapper.TryGetContentHash(remote.DimensionsJson);
        if (string.IsNullOrWhiteSpace(remoteHash))
            return false;

        if (string.Equals(localHash, remoteHash, StringComparison.OrdinalIgnoreCase))
            return false;

        var localUpdated = local.LocalUpdatedAtUtc ?? DateTime.MinValue;
        var remoteUpdated = remote.UpdatedAtUtc ?? DateTime.MinValue;
        return Math.Abs((localUpdated - remoteUpdated).TotalSeconds) > 1;
    }

    private static bool IsLocalNewer(ProductRecord local, ProjectProductDto remote)
    {
        var localUpdated = local.LocalUpdatedAtUtc ?? DateTime.MinValue;
        var remoteUpdated = remote.UpdatedAtUtc ?? DateTime.MinValue;
        return localUpdated > remoteUpdated.AddSeconds(1);
    }

    private static ProjectProductDto? FindExisting(ProductRecord record, RemoteLookup lookup)
    {
        var cadKey = record.ProductId.ToString("D");
        if (lookup.ByCadId.TryGetValue(cadKey, out var byId))
            return byId;
        if (record.ServerProductId.HasValue && lookup.ByServerId.TryGetValue(record.ServerProductId.Value, out var byServer))
            return byServer;
        var code = record.Code?.Trim();
        if (!string.IsNullOrWhiteSpace(code) && lookup.ByCode.TryGetValue(code, out var byCode))
            return byCode;
        return null;
    }

    private static RemoteLookup BuildRemoteLookup(IReadOnlyList<ProjectProductDto> remoteProducts)
    {
        var lookup = new RemoteLookup();
        foreach (var remote in remoteProducts)
        {
            lookup.ByServerId[remote.Id] = remote;
            var cadId = ProjectProductSyncMapper.TryGetCadProductId(remote.DimensionsJson);
            if (!string.IsNullOrWhiteSpace(cadId) && !lookup.ByCadId.ContainsKey(cadId))
                lookup.ByCadId[cadId] = remote;

            var code = remote.Code?.Trim();
            if (!string.IsNullOrWhiteSpace(code) && !lookup.ByCode.ContainsKey(code))
                lookup.ByCode[code] = remote;
        }

        return lookup;
    }

    public static async Task<ProjectProductSyncResult> PushDirtyProductsWithCommitAsync(
        PrecastApiClient client,
        Guid projectId,
        string commitMessage,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var result = new ProjectProductSyncResult();
        var doc = AcadApp.DocumentManager.MdiActiveDocument;
        if (doc is null)
        {
            result.Errors.Add("Aktif AutoCAD çizimi yok.");
            result.Failed = 1;
            return result;
        }

        IReadOnlyList<ProductRecord> localProducts;
        using (doc.LockDocument())
        using (var tr = doc.Database.TransactionManager.StartTransaction())
        {
            localProducts = new RegistryService().ListProducts(tr, doc.Database);
            tr.Commit();
        }

        var dirtyProducts = localProducts.Where(ProductDirtyTracker.IsDirty).ToList();
        if (dirtyProducts.Count == 0)
        {
            result.Warnings.Add("Gönderilecek değişiklik yok (tüm ürünler commit edilmiş).");
            return result;
        }

        var remoteProducts = await client.ListProjectProductsAsync(projectId, cancellationToken).ConfigureAwait(false);
        var lookup = BuildRemoteLookup(remoteProducts);
        var drawingFileName = string.IsNullOrWhiteSpace(doc.Name) ? null : System.IO.Path.GetFileName(doc.Name);

        var changeRequests = new List<ProjectProductCommitChangeRequestDto>();
        foreach (var record in dirtyProducts)
        {
            var summary = ProductDirtyTracker.BuildChangeSummary(record);
            var existing = FindExisting(record, lookup);
            var existingRevisions = existing is null
                ? []
                : ProjectProductSyncMapper.TryGetDrawingRevisions(existing.DimensionsJson);
            var productionSequence = ProjectProductSyncMapper.TryParseProductionSequenceFromAttributes(record.Attributes)
                ?? existing?.ProductionSequence;
            var payload = ProjectProductSyncMapper.MapToWriteDto(
                record,
                projectId,
                drawingFileName,
                commitMessage,
                existingRevisions,
                null,
                productionSequence);
            if (existing is not null)
            {
                payload.Id = existing.Id;
                payload.ProjectId = projectId;
            }

            changeRequests.Add(new ProjectProductCommitChangeRequestDto
            {
                CadProductId = record.ProductId.ToString("D"),
                ProductCode = summary.ProductCode,
                ChangeType = summary.ChangeType,
                ChangedFieldsJson = summary.ChangedFieldsJson,
                RevisionBefore = summary.RevisionBefore,
                RevisionAfter = summary.RevisionAfter,
                ContentHashBefore = summary.ContentHashBefore,
                ContentHashAfter = summary.ContentHashAfter,
                Product = payload,
            });
        }

        var commitResponse = await client.CreateProjectProductCommitAsync(
            projectId,
            new ProjectProductCommitRequestDto
            {
                Message = commitMessage,
                Source = "cad",
                Changes = changeRequests,
            },
            cancellationToken).ConfigureAwait(false);

        using (doc.LockDocument())
        using (var tr = doc.Database.TransactionManager.StartTransaction())
        {
            var registry = new RegistryService();
            foreach (var saved in commitResponse.Changes)
            {
                if (string.IsNullOrWhiteSpace(saved.CadProductId)
                    || !Guid.TryParse(saved.CadProductId, out var cadId))
                    continue;

                if (!registry.TryGetProduct(tr, doc.Database, cadId, out var current) || current is null)
                    continue;

                if (saved.ProductId is Guid serverId && serverId != Guid.Empty)
                    current.ServerProductId = serverId;

                var committedChange = changeRequests.FirstOrDefault(c =>
                    string.Equals(c.CadProductId, saved.CadProductId, StringComparison.OrdinalIgnoreCase));
                if (committedChange?.Product.DimensionsJson is { Length: > 0 } dimsJson)
                {
                    current.ServerDrawingRevisions = ProjectProductSyncMapper.ToServerDrawingRevisions(
                        ProjectProductSyncMapper.TryGetDrawingRevisions(dimsJson));
                }

                ProductDirtyTracker.MarkCommitted(current);
                registry.SaveProduct(tr, doc.Database, current);
            }

            tr.Commit();
        }

        foreach (var record in dirtyProducts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ProductRecord recordForPdf = record;
            using (doc.LockDocument())
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                var registry = new RegistryService();
                if (registry.TryGetProduct(tr, doc.Database, record.ProductId, out var fresh) && fresh is not null)
                    recordForPdf = fresh;
                tr.Commit();
            }

            try
            {
                progress?.Report($"{recordForPdf.Code}: PDF hazırlanıyor ve yükleniyor…");
                await TryUploadPdfsAsync(
                    client,
                    projectId,
                    recordForPdf,
                    doc,
                    commitMessage,
                    progress,
                    cancellationToken).ConfigureAwait(false);

                using (doc.LockDocument())
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    if (new RegistryService().TryGetProduct(tr, doc.Database, recordForPdf.ProductId, out var updated)
                        && updated is not null)
                    {
                        updated.ServerDrawingRevisions = recordForPdf.ServerDrawingRevisions;
                        new RegistryService().SaveProduct(tr, doc.Database, updated);
                    }

                    tr.Commit();
                }
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"{record.Code}: PDF yükleme — {ex.Message}");
            }
        }

        result.Created = commitResponse.Changes.Count(c =>
            string.Equals(c.ChangeType, "created", StringComparison.OrdinalIgnoreCase));
        result.Updated = commitResponse.Changes.Count - result.Created;

        return result;
    }

    private sealed class RemoteLookup
    {
        public Dictionary<string, ProjectProductDto> ByCadId { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, ProjectProductDto> ByCode { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<Guid, ProjectProductDto> ByServerId { get; } = new();
    }
}
