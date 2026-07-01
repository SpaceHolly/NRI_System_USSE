using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Driver;
using Nri.Shared.Configuration;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;
using Nri.Shared.Utilities;

namespace Nri.Server.Application;

public partial class ServiceHub
{
    private const string BackupDataFileName = "data.json";
    private const string BackupManifestFileName = "manifest.json";
    private const int BackupMaxPreviewCollections = 300;

    public ResponseEnvelope BackupGet(CommandContext context)
    {
        RequireAdmin(context);
        if (!BackupBaseEnabled()) return BackupDisabled();
        var backupId = RequireBackupId(PayloadReader.GetString(context.Request.Payload, "backupId"));
        var record = FindBackupRecord(backupId) ?? throw new KeyNotFoundException("Backup not found.");
        return Ok("Backup loaded.", new Dictionary<string, object> { { "item", BackupRecordPayload(record, includeDetails: true) } });
    }

    public ResponseEnvelope BackupVerify(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!BackupBaseEnabled()) return BackupDisabled();
        if (!BackupVerificationEnabled()) return BackupFeatureDisabled("Backup verification is disabled by feature flags.");
        var backupId = RequireBackupId(PayloadReader.GetString(context.Request.Payload, "backupId"));
        var record = FindBackupRecord(backupId) ?? throw new KeyNotFoundException("Backup not found.");
        VerifyBackupRecord(record, actor, writeAudit: true);
        return Ok("Backup verification completed.", new Dictionary<string, object> { { "item", BackupRecordPayload(record, includeDetails: true) } });
    }

    public ResponseEnvelope BackupRestorePreview(CommandContext context)
    {
        var actor = RequireSuperAdmin(context);
        if (!BackupBaseEnabled()) return BackupDisabled();
        if (!BackupRestorePreviewEnabled()) return BackupFeatureDisabled("Restore preview is disabled by feature flags.");
        var backupId = RequireBackupId(PayloadReader.GetString(context.Request.Payload, "backupId"));
        var reason = SafeText(PayloadReader.GetString(context.Request.Payload, "reason"), 500);
        var operation = BuildRestorePreview(actor, backupId, reason);
        _repositories.BackupRestoreOperations.Insert(operation);
        WriteAudit("backup_restore", actor.Id, "preview", operation.BackupId);
        TryWriteBackupJournal("backup.restore.previewed", "Restore preview created", $"Backup {operation.BackupId}: blockers={operation.Blockers.Count}", actor.Id, operation.BackupId);
        return Ok(operation.HasBlockers ? "Restore preview has blockers." : "Restore preview passed.", RestoreOperationPayload(operation));
    }

    public ResponseEnvelope BackupRestoreExecute(CommandContext context)
    {
        var actor = RequireSuperAdmin(context);
        if (!BackupBaseEnabled()) return BackupDisabled();
        if (!BackupRestoreExecutionEnabled()) return BackupFeatureDisabled("Restore execution is disabled by feature flags.");
        if (!_serverConfig.BackupStorage.AllowRestoreExecution)
            return Error("Restore execution is disabled by server configuration.", ResponseStatus.Forbidden, ErrorCode.Forbidden);
        if (!IsEnvironmentAllowed())
            return Error("Restore execution is disabled for the current server environment.", ResponseStatus.Forbidden, ErrorCode.Forbidden);

        var backupId = RequireBackupId(PayloadReader.GetString(context.Request.Payload, "backupId"));
        var reason = SafeText(PayloadReader.GetString(context.Request.Payload, "reason"), 500);
        var confirmation = PayloadReader.GetString(context.Request.Payload, "confirmation") ?? string.Empty;
        if (!string.Equals(confirmation, "RESTORE", StringComparison.Ordinal))
            return Error("Restore confirmation phrase is invalid.", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);

        var preview = BuildRestorePreview(actor, backupId, reason);
        if (preview.HasBlockers)
        {
            preview.Status = BackupRestoreOperationStatusIds.Blocked;
            _repositories.BackupRestoreOperations.Insert(preview);
            WriteAudit("backup_restore", actor.Id, "blocked", backupId);
            return BackupRestoreError("Restore is blocked. Run preview and resolve blockers first.", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed, RestoreOperationPayload(preview));
        }

        preview.Status = BackupRestoreOperationStatusIds.Running;
        preview.StartedAtUtc = DateTime.UtcNow;
        _repositories.BackupRestoreOperations.Insert(preview);
        WriteAudit("backup_restore", actor.Id, "execute.requested", backupId);
        TryWriteBackupJournal("backup.restore.started", "Restore started", $"Backup {backupId}", actor.Id, backupId);

        try
        {
            BackupRecordState? safetyBackup = null;
            if (_serverConfig.BackupStorage.RequirePreRestoreBackup)
            {
                var safety = CreateFullServerBackup(actor, "pre-restore-safety-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss"), "Safety backup before restore.", isSafety: true, sourceRestoreOperationId: preview.OperationId);
                safetyBackup = safety;
                preview.SafetyBackupId = safety.BackupId;
                _repositories.BackupRestoreOperations.Replace(preview);
            }

            RestoreFullServerBackup(backupId);
            if (safetyBackup != null)
                UpsertBackupRecordAfterRestore(safetyBackup);
            preview.Status = BackupRestoreOperationStatusIds.Completed;
            preview.CompletedAtUtc = DateTime.UtcNow;
            preview.Summary["restoredAtUtc"] = preview.CompletedAtUtc.ToString("O");
            preview.Summary["safetyBackupId"] = preview.SafetyBackupId;
            UpsertBackupRestoreOperationAfterRestore(preview);
            WriteAudit("backup_restore", actor.Id, "execute.completed", backupId);
            TryWriteBackupJournal("backup.restore.completed", "Restore completed", $"Backup {backupId}; safety backup {preview.SafetyBackupId}", actor.Id, backupId, preview.SafetyBackupId);
            return Ok("Backup restored. Reconnect clients after restore.", RestoreOperationPayload(preview));
        }
        catch (Exception ex)
        {
            preview.Status = BackupRestoreOperationStatusIds.Failed;
            preview.CompletedAtUtc = DateTime.UtcNow;
            preview.Warnings.Add("Restore failed: " + SafeText(ex.Message, 200));
            _repositories.BackupRestoreOperations.Replace(preview);
            WriteAudit("backup_restore", actor.Id, "execute.failed", backupId);
            TryWriteBackupJournal("backup.restore.failed", "Restore failed", $"Backup {backupId}", actor.Id, backupId);
            return BackupRestoreError("Restore failed before completion.", ResponseStatus.Error, ErrorCode.InternalError, RestoreOperationPayload(preview));
        }
    }

    public ResponseEnvelope BackupMaintenanceGet(CommandContext context)
    {
        RequireAdmin(context);
        if (!BackupBaseEnabled()) return BackupDisabled();
        return Ok("Maintenance state loaded.", new Dictionary<string, object> { { "maintenance", MaintenancePayload(GetMaintenanceState()) } });
    }

    public ResponseEnvelope BackupMaintenanceSet(CommandContext context)
    {
        var actor = RequireSuperAdmin(context);
        if (!BackupBaseEnabled()) return BackupDisabled();
        if (!BackupMaintenanceEnabled()) return BackupFeatureDisabled("Backup maintenance mode is disabled by feature flags.");
        var enabled = PayloadReader.GetBool(context.Request.Payload, "enabled");
        var reason = SafeText(PayloadReader.GetString(context.Request.Payload, "reason"), 500);
        var state = GetMaintenanceState();
        state.IsEnabled = enabled;
        state.Reason = reason;
        state.UpdatedByUserId = actor.Id;
        state.UpdatedByDisplayName = ActorDisplayName(actor);
        state.UpdatedAtUtc = DateTime.UtcNow;
        UpsertBackupMaintenance(state);
        WriteAudit("backup_restore", actor.Id, enabled ? "maintenance.enabled" : "maintenance.disabled", state.Id);
        TryWriteBackupJournal(enabled ? "maintenance.enabled" : "maintenance.disabled", enabled ? "Maintenance mode enabled" : "Maintenance mode disabled", reason, actor.Id);
        return Ok("Maintenance state updated.", new Dictionary<string, object> { { "maintenance", MaintenancePayload(state) } });
    }

    public ResponseEnvelope BackupOperationGet(CommandContext context)
    {
        RequireAdmin(context);
        if (!BackupBaseEnabled()) return BackupDisabled();
        var operationId = RequireLength(PayloadReader.GetString(context.Request.Payload, "operationId"), 8, 128, "operationId");
        var operation = _repositories.BackupRestoreOperations.Find(Builders<BackupRestoreOperationState>.Filter.Eq(x => x.OperationId, operationId)).FirstOrDefault()
            ?? _repositories.BackupRestoreOperations.GetById(operationId)
            ?? throw new KeyNotFoundException("Backup operation not found.");
        return Ok("Backup operation loaded.", RestoreOperationPayload(operation));
    }

    private BackupRecordState CreateFullServerBackup(UserAccount actor, string displayName, string description, bool isSafety, string sourceRestoreOperationId)
    {
        if (!BackupBaseEnabled()) throw new InvalidOperationException("Backup/Restore MVP is disabled by feature flags.");
        if (!_serverConfig.BackupStorage.AllowManualBackupCreation && !isSafety) throw new InvalidOperationException("Manual backup creation is disabled by server configuration.");
        EnsureBackupStorage();

        var backupId = "bkp_" + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + "_" + Guid.NewGuid().ToString("N").Substring(0, 10);
        var backupDir = ResolveBackupDirectory(backupId);
        Directory.CreateDirectory(backupDir);
        var dataPath = Path.Combine(backupDir, BackupDataFileName);
        var manifestPath = Path.Combine(backupDir, BackupManifestFileName);

        var record = new BackupRecordState
        {
            BackupId = backupId,
            Scope = BackupScopeIds.FullServer,
            Status = BackupStatusIds.Creating,
            DisplayName = FirstNonEmpty(SafeText(displayName, 160), backupId),
            Description = SafeText(description, 500),
            CreatedByUserId = actor.Id,
            CreatedByDisplayName = ActorDisplayName(actor),
            EnvironmentName = _serverConfig.Environment,
            ServerVersion = typeof(ServiceHub).Assembly.GetName().Version?.ToString() ?? "unknown",
            FileName = BackupDataFileName,
            RelativeStoragePath = backupId,
            IsPreRestoreSafetyBackup = isSafety,
            SourceRestoreOperationId = sourceRestoreOperationId
        };
        _repositories.BackupRecords.Insert(record);

        try
        {
            var backupData = BuildBackupDataFile();
            var dataJson = JsonSerializer.Serialize(backupData, BackupJsonOptions());
            File.WriteAllText(dataPath, dataJson, Encoding.UTF8);
            var checksum = Sha256File(dataPath);
            var size = new FileInfo(dataPath).Length;

            var manifest = BuildManifest(backupId, backupData, checksum);
            File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, BackupJsonOptions()), Encoding.UTF8);

            record.Status = BackupStatusIds.Complete;
            record.CompletedAtUtc = DateTime.UtcNow;
            record.SizeBytes = size + new FileInfo(manifestPath).Length;
            record.Checksum = checksum;
            record.CollectionCount = backupData.Collections.Count;
            record.DocumentCount = backupData.Collections.Sum(x => (long)x.Documents.Count);
            record.IsVerified = false;
            record.VerificationStatus = BackupVerificationStatusIds.NotVerified;
            _repositories.BackupRecords.Replace(record);

            WriteAudit("backup", actor.Id, isSafety ? "create.safety" : "create", record.BackupId);
            TryWriteBackupJournal("backup.created", isSafety ? "Safety backup created" : "Backup created", $"Backup {record.BackupId}: collections={record.CollectionCount}, documents={record.DocumentCount}", actor.Id, record.BackupId, sourceRestoreOperationId);

            if (BackupVerificationEnabled())
                VerifyBackupRecord(record, actor, writeAudit: true);

            return record;
        }
        catch (Exception ex)
        {
            record.Status = BackupStatusIds.Failed;
            record.VerificationStatus = BackupVerificationStatusIds.Failed;
            record.VerificationMessage = SafeText(ex.Message, 300);
            _repositories.BackupRecords.Replace(record);
            WriteAudit("backup", actor.Id, "create.failed", record.BackupId);
            TryWriteBackupJournal("backup.failed", "Backup failed", $"Backup {record.BackupId}", actor.Id, record.BackupId);
            throw;
        }
    }

    private BackupRestoreOperationState BuildRestorePreview(UserAccount actor, string backupId, string reason)
    {
        var record = FindBackupRecord(backupId) ?? throw new KeyNotFoundException("Backup not found.");
        if (_serverConfig.BackupStorage.VerificationRequired && record.Status == BackupStatusIds.Complete)
        {
            VerifyBackupRecord(record, actor, writeAudit: false);
            record = FindBackupRecord(backupId) ?? throw new KeyNotFoundException("Backup not found.");
        }
        var maintenance = GetMaintenanceState();
        var blockers = new List<string>();
        var warnings = new List<string>();

        if (record.Status != BackupStatusIds.Complete) blockers.Add("Backup is not complete.");
        if (record.IsArchived || record.Status == BackupStatusIds.Archived) blockers.Add("Backup is archived.");
        if (_serverConfig.BackupStorage.VerificationRequired && (!record.IsVerified || record.VerificationStatus != BackupVerificationStatusIds.Valid)) blockers.Add("Backup must be verified before restore.");
        if (_serverConfig.BackupStorage.RequireMaintenanceModeForRestore && !maintenance.IsEnabled) blockers.Add("Maintenance mode is required before restore.");
        if (HasActiveSessionOrCombat()) blockers.Add("Active session or combat is running.");
        if (!BackupDataFilesExist(record)) blockers.Add("Backup files are missing.");
        if (!IsEnvironmentAllowed()) blockers.Add("Current environment is not listed in AllowedEnvironments.");

        var manifestCollections = new List<Dictionary<string, object>>();
        if (BackupDataFilesExist(record))
        {
            try
            {
                var manifestPath = Path.Combine(ResolveBackupDirectory(record.BackupId), BackupManifestFileName);
                var manifest = JsonSerializer.Deserialize<BackupManifest>(File.ReadAllText(manifestPath, Encoding.UTF8), BackupJsonOptions());
                if (manifest != null)
                {
                    manifestCollections = manifest.Collections
                        .OrderBy(x => x.CollectionName, StringComparer.OrdinalIgnoreCase)
                        .Select(x => new Dictionary<string, object>
                        {
                            { "collectionName", x.CollectionName },
                            { "documentCount", x.DocumentCount },
                            { "dataChecksum", x.DataChecksum }
                        })
                        .ToList();
                }
            }
            catch (Exception ex)
            {
                warnings.Add("Manifest collection preview unavailable: " + SafeText(ex.Message, 160));
            }
        }

        var operation = new BackupRestoreOperationState
        {
            OperationId = "restore_" + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + "_" + Guid.NewGuid().ToString("N").Substring(0, 10),
            BackupId = record.BackupId,
            Status = blockers.Count > 0 ? BackupRestoreOperationStatusIds.Blocked : BackupRestoreOperationStatusIds.Previewed,
            RequestedByUserId = actor.Id,
            RequestedByDisplayName = ActorDisplayName(actor),
            RequestedAtUtc = DateTime.UtcNow,
            Reason = reason,
            MaintenanceModeRequired = _serverConfig.BackupStorage.RequireMaintenanceModeForRestore,
            MaintenanceModeWasEnabled = maintenance.IsEnabled,
            PreRestoreBackupRequired = _serverConfig.BackupStorage.RequirePreRestoreBackup,
            VerificationRequired = _serverConfig.BackupStorage.VerificationRequired,
            VerificationPassed = record.IsVerified && record.VerificationStatus == BackupVerificationStatusIds.Valid,
            HasBlockers = blockers.Count > 0,
            Blockers = blockers,
            Warnings = warnings,
            Summary = new Dictionary<string, object>
            {
                { "backupId", record.BackupId },
                { "displayName", record.DisplayName },
                { "collectionCount", record.CollectionCount },
                { "documentCount", record.DocumentCount },
                { "sizeBytes", record.SizeBytes },
                { "maintenanceEnabled", maintenance.IsEnabled },
                { "includedCollections", manifestCollections.Cast<object>().ToArray() },
                { "collectionNames", manifestCollections.Select(x => (object)Convert.ToString(x["collectionName"])!).ToArray() }
            }
        };

        return operation;
    }

    private void RestoreFullServerBackup(string backupId)
    {
        var record = FindBackupRecord(backupId) ?? throw new KeyNotFoundException("Backup not found.");
        var dataPath = Path.Combine(ResolveBackupDirectory(record.BackupId), BackupDataFileName);
        var json = File.ReadAllText(dataPath, Encoding.UTF8);
        var data = JsonSerializer.Deserialize<BackupDataFile>(json, BackupJsonOptions()) ?? throw new InvalidOperationException("Backup data file is invalid.");
        foreach (var collection in data.Collections)
        {
            if (string.IsNullOrWhiteSpace(collection.Name) || collection.Name.StartsWith("system.", StringComparison.OrdinalIgnoreCase)) continue;
            var mongoCollection = _mongo.Database.GetCollection<BsonDocument>(collection.Name);
            mongoCollection.DeleteMany(FilterDefinition<BsonDocument>.Empty);
            var docs = collection.Documents
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(BsonDocument.Parse)
                .ToList();
            if (docs.Count > 0) mongoCollection.InsertMany(docs);
        }
    }

    private BackupDataFile BuildBackupDataFile()
    {
        var data = new BackupDataFile
        {
            CreatedAtUtc = DateTime.UtcNow,
            DatabaseName = _serverConfig.Mongo.DatabaseName
        };
        var settings = new JsonWriterSettings { OutputMode = JsonOutputMode.CanonicalExtendedJson };
        var names = _mongo.Database.ListCollectionNames().ToList()
            .Where(x => !x.StartsWith("system.", StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (names.Count > BackupMaxPreviewCollections)
            throw new InvalidOperationException("Too many Mongo collections for MVP backup.");

        foreach (var name in names)
        {
            var collection = _mongo.Database.GetCollection<BsonDocument>(name);
            var documents = collection.Find(FilterDefinition<BsonDocument>.Empty)
                .ToList()
                .Select(x => x.ToJson(settings))
                .ToList();
            data.Collections.Add(new BackupDataCollection { Name = name, Documents = documents });
        }
        return data;
    }

    private BackupManifest BuildManifest(string backupId, BackupDataFile data, string checksum)
    {
        var manifest = new BackupManifest
        {
            BackupId = backupId,
            Scope = BackupScopeIds.FullServer,
            CreatedAtUtc = data.CreatedAtUtc,
            ServerVersion = typeof(ServiceHub).Assembly.GetName().Version?.ToString() ?? "unknown",
            EnvironmentName = _serverConfig.Environment,
            ArchiveChecksum = checksum,
            DocumentCount = data.Collections.Sum(x => (long)x.Documents.Count)
        };
        foreach (var collection in data.Collections)
        {
            manifest.Collections.Add(new BackupManifestCollectionEntry
            {
                CollectionName = collection.Name,
                DocumentCount = collection.Documents.Count,
                SafeSchemaSummary = "documents:" + collection.Documents.Count,
                DataChecksum = Sha256Text(string.Join("\n", collection.Documents))
            });
        }
        manifest.FeatureModuleSummaries["backup_restore"] = "mvp";
        manifest.ModuleVersions["server"] = manifest.ServerVersion;
        return manifest;
    }

    private void VerifyBackupRecord(BackupRecordState record, UserAccount actor, bool writeAudit)
    {
        record.Status = record.Status == BackupStatusIds.Complete ? BackupStatusIds.Verifying : record.Status;
        _repositories.BackupRecords.Replace(record);
        try
        {
            var backupDir = ResolveBackupDirectory(record.BackupId);
            var dataPath = Path.Combine(backupDir, BackupDataFileName);
            var manifestPath = Path.Combine(backupDir, BackupManifestFileName);
            if (!File.Exists(dataPath) || !File.Exists(manifestPath))
            {
                ApplyVerification(record, BackupVerificationStatusIds.MissingFile, "Backup data or manifest file is missing.", false);
                return;
            }

            var manifest = JsonSerializer.Deserialize<BackupManifest>(File.ReadAllText(manifestPath, Encoding.UTF8), BackupJsonOptions());
            if (manifest == null || manifest.BackupId != record.BackupId || manifest.Collections.Count == 0)
            {
                ApplyVerification(record, BackupVerificationStatusIds.InvalidManifest, "Manifest is invalid.", false);
                return;
            }

            var checksum = Sha256File(dataPath);
            if (!string.Equals(checksum, manifest.ArchiveChecksum, StringComparison.OrdinalIgnoreCase))
            {
                ApplyVerification(record, BackupVerificationStatusIds.InvalidChecksum, "Checksum mismatch.", false);
                return;
            }

            record.Checksum = checksum;
            record.CollectionCount = manifest.Collections.Count;
            record.DocumentCount = manifest.DocumentCount;
            record.SizeBytes = new FileInfo(dataPath).Length + new FileInfo(manifestPath).Length;
            ApplyVerification(record, BackupVerificationStatusIds.Valid, "Backup is valid.", true);
            if (writeAudit) WriteAudit("backup", actor.Id, "verify", record.BackupId);
            TryWriteBackupJournal("backup.verified", "Backup verified", $"Backup {record.BackupId}: collections={record.CollectionCount}, documents={record.DocumentCount}", actor.Id, record.BackupId);
        }
        catch (Exception ex)
        {
            ApplyVerification(record, BackupVerificationStatusIds.Failed, SafeText(ex.Message, 300), false);
        }
    }

    private void ApplyVerification(BackupRecordState record, string status, string message, bool valid)
    {
        record.Status = valid ? BackupStatusIds.Complete : BackupStatusIds.Corrupted;
        record.IsVerified = valid;
        record.VerifiedAtUtc = DateTime.UtcNow;
        record.VerificationStatus = status;
        record.VerificationMessage = message;
        _repositories.BackupRecords.Replace(record);
    }

    private BackupRecordState? FindBackupRecord(string backupId)
        => _repositories.BackupRecords.Find(Builders<BackupRecordState>.Filter.Eq(x => x.BackupId, backupId)).FirstOrDefault()
           ?? _repositories.BackupRecords.GetById(backupId);

    private bool BackupDataFilesExist(BackupRecordState record)
    {
        var dir = ResolveBackupDirectory(record.BackupId);
        return File.Exists(Path.Combine(dir, BackupDataFileName)) && File.Exists(Path.Combine(dir, BackupManifestFileName));
    }

    private BackupMaintenanceState GetMaintenanceState()
    {
        var state = _repositories.BackupMaintenanceStates.Find(FilterDefinition<BackupMaintenanceState>.Empty)
            .OrderByDescending(x => x.UpdatedAtUtc)
            .FirstOrDefault();
        return state ?? new BackupMaintenanceState { Id = "backup_maintenance", UpdatedAtUtc = DateTime.UtcNow };
    }

    private void UpsertBackupMaintenance(BackupMaintenanceState state)
    {
        var collection = _mongo.BackupMaintenanceStates;
        collection.ReplaceOne(x => x.Id == state.Id, state, new ReplaceOptions { IsUpsert = true });
    }

    private void UpsertBackupRecordAfterRestore(BackupRecordState record)
    {
        record.UpdatedUtc = DateTime.UtcNow;
        _mongo.BackupRecords.ReplaceOne(x => x.Id == record.Id, record, new ReplaceOptions { IsUpsert = true });
    }

    private void UpsertBackupRestoreOperationAfterRestore(BackupRestoreOperationState operation)
    {
        operation.UpdatedUtc = DateTime.UtcNow;
        _mongo.BackupRestoreOperations.ReplaceOne(x => x.Id == operation.Id, operation, new ReplaceOptions { IsUpsert = true });
    }

    private bool HasActiveSessionOrCombat()
    {
        return _repositories.CurrentSessions.Find(FilterDefinition<CurrentSessionState>.Empty)
            .Any(x => !x.IsArchived
                && !string.Equals(x.Status, CurrentSessionStatusIds.Completed, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(x.Status, CurrentSessionStatusIds.Cancelled, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(x.Status, CurrentSessionStatusIds.Archived, StringComparison.OrdinalIgnoreCase)
                && (string.Equals(x.Status, CurrentSessionStatusIds.Active, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(x.Status, CurrentSessionStatusIds.Paused, StringComparison.OrdinalIgnoreCase)
                    || !string.IsNullOrWhiteSpace(x.ActiveCombatEncounterId)));
    }

    private bool IsEnvironmentAllowed()
    {
        var allowed = _serverConfig.BackupStorage.AllowedEnvironments ?? new List<string>();
        return allowed.Count == 0 || allowed.Any(x => string.Equals(x, _serverConfig.Environment, StringComparison.OrdinalIgnoreCase));
    }

    private void EnsureBackupStorage()
    {
        Directory.CreateDirectory(BackupRootPath());
        Directory.CreateDirectory(BackupTempPath());
    }

    private string BackupRootPath() => Path.GetFullPath(_serverConfig.BackupStorage.BackupRootDirectory ?? "./backups");
    private string BackupTempPath() => Path.GetFullPath(_serverConfig.BackupStorage.TemporaryDirectory ?? Path.Combine(BackupRootPath(), "tmp"));

    private string ResolveBackupDirectory(string backupId)
    {
        var safeId = RequireBackupId(backupId);
        var root = BackupRootPath().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var path = Path.GetFullPath(Path.Combine(root, safeId));
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Backup path validation failed.");
        return path;
    }

    private static string RequireBackupId(string? backupId)
    {
        var id = RequireLength(backupId, 8, 128, "backupId");
        if (id.Contains("..") || id.Contains("/") || id.Contains("\\") || Path.IsPathRooted(id))
            throw new InvalidOperationException("Invalid backupId.");
        return id;
    }

    private static string Sha256File(string path)
    {
        using var sha = SHA256.Create();
        using var stream = File.OpenRead(path);
        return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
    }

    private static string Sha256Text(string text)
    {
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text ?? string.Empty))).Replace("-", string.Empty).ToLowerInvariant();
    }

    private static JsonSerializerOptions BackupJsonOptions() => new JsonSerializerOptions { WriteIndented = true };

    private Dictionary<string, object> BackupRecordPayload(BackupRecordState record, bool includeDetails) => new Dictionary<string, object>
    {
        { "backupId", record.BackupId },
        { "scope", record.Scope },
        { "status", record.Status },
        { "displayName", record.DisplayName },
        { "description", includeDetails ? record.Description : string.Empty },
        { "createdAtUtc", SafeUtcString(record.CreatedUtc) },
        { "completedAtUtc", SafeUtcString(record.CompletedAtUtc) },
        { "createdByUserId", record.CreatedByUserId },
        { "createdByDisplayName", record.CreatedByDisplayName },
        { "serverVersion", record.ServerVersion },
        { "schemaVersion", record.SchemaVersion },
        { "environmentName", record.EnvironmentName },
        { "sizeBytes", record.SizeBytes },
        { "checksumAlgorithm", record.ChecksumAlgorithm },
        { "checksumPreview", string.IsNullOrWhiteSpace(record.Checksum) ? string.Empty : record.Checksum.Substring(0, Math.Min(12, record.Checksum.Length)) },
        { "collectionCount", record.CollectionCount },
        { "documentCount", record.DocumentCount },
        { "isVerified", record.IsVerified },
        { "verifiedAtUtc", SafeUtcString(record.VerifiedAtUtc) },
        { "verificationStatus", record.VerificationStatus },
        { "verificationMessage", record.VerificationMessage },
        { "isPreRestoreSafetyBackup", record.IsPreRestoreSafetyBackup },
        { "isArchived", record.IsArchived }
    };

    private Dictionary<string, object> RestoreOperationPayload(BackupRestoreOperationState operation) => new Dictionary<string, object>
    {
        { "operationId", operation.OperationId },
        { "backupId", operation.BackupId },
        { "status", operation.Status },
        { "requestedByUserId", operation.RequestedByUserId },
        { "requestedByDisplayName", operation.RequestedByDisplayName },
        { "requestedAtUtc", SafeUtcString(operation.RequestedAtUtc) },
        { "startedAtUtc", SafeUtcString(operation.StartedAtUtc) },
        { "completedAtUtc", SafeUtcString(operation.CompletedAtUtc) },
        { "reason", operation.Reason },
        { "maintenanceModeRequired", operation.MaintenanceModeRequired },
        { "maintenanceModeWasEnabled", operation.MaintenanceModeWasEnabled },
        { "preRestoreBackupRequired", operation.PreRestoreBackupRequired },
        { "safetyBackupId", operation.SafetyBackupId },
        { "verificationRequired", operation.VerificationRequired },
        { "verificationPassed", operation.VerificationPassed },
        { "hasBlockers", operation.HasBlockers },
        { "blockers", operation.Blockers.Cast<object>().ToArray() },
        { "warnings", operation.Warnings.Cast<object>().ToArray() },
        { "summary", operation.Summary }
    };

    private Dictionary<string, object> MaintenancePayload(BackupMaintenanceState state) => new Dictionary<string, object>
    {
        { "isEnabled", state.IsEnabled },
        { "reason", state.Reason },
        { "updatedByUserId", state.UpdatedByUserId },
        { "updatedByDisplayName", state.UpdatedByDisplayName },
        { "updatedAtUtc", SafeUtcString(state.UpdatedAtUtc) }
    };

    private static string SafeUtcString(DateTime value)
    {
        if (value == default(DateTime) || value == DateTime.MinValue || value == DateTime.MaxValue)
            return string.Empty;

        if (value.Kind == DateTimeKind.Utc)
            return value.ToString("O");

        if (value.Kind == DateTimeKind.Local)
            return value.ToUniversalTime().ToString("O");

        return DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString("O");
    }

    private static string ActorDisplayName(UserAccount actor) => FirstNonEmpty(actor.Login, actor.Id);
    private static string SafeText(string? text, int maxLength)
    {
        var value = (text ?? string.Empty).Trim();
        if (value.Length <= maxLength) return value;
        return value.Substring(0, maxLength);
    }

    private void TryWriteBackupJournal(string sourceEventType, string title, string summary, string actorUserId, string sourceEventId = "", string safetyBackupId = "")
    {
        try
        {
            if (!_featureFlags.IsEnabled(nameof(EventJournalFeatureFlags.UseEventJournalMvp)) ||
                !_featureFlags.IsEnabled(nameof(EventJournalFeatureFlags.UseEventJournalAutomaticIngestion)))
                return;

            _repositories.EventJournalEntries.Insert(new EventJournalEntryState
            {
                CampaignId = "system",
                EntryType = EventJournalEntryTypeIds.Automatic,
                Category = EventJournalCategoryIds.System,
                Severity = EventJournalSeverityIds.Information,
                Title = title,
                Summary = summary,
                PlayerSummary = string.Empty,
                VisibilityMode = EventJournalVisibilityModeIds.GMOnly,
                IsPlayerVisible = false,
                IsAutomatic = true,
                SourceModule = "backup_restore",
                SourceEventType = sourceEventType,
                SourceEventId = string.IsNullOrWhiteSpace(sourceEventId) ? Guid.NewGuid().ToString("N") : sourceEventId,
                ActorUserId = actorUserId,
                ActorDisplayName = BackupActorDisplayName(actorUserId),
                OccurredAtUtc = DateTime.UtcNow,
                Tags = new List<string> { "backup_restore" },
                ExtraData = new Dictionary<string, object>
                {
                    { "backupId", sourceEventId },
                    { "safetyBackupId", safetyBackupId }
                }
            });
        }
        catch (Exception ex)
        {
            _logger.Debug("backup.event_journal.write_failed message=" + ex.Message);
        }
    }

    private string BackupActorDisplayName(string actorUserId)
    {
        if (string.IsNullOrWhiteSpace(actorUserId)) return string.Empty;
        try
        {
            var actor = _repositories.Accounts.GetById(actorUserId);
            return FirstNonEmpty(actor?.Login, actorUserId);
        }
        catch
        {
            return actorUserId;
        }
    }

    private bool BackupBaseEnabled() => _featureFlags.IsEnabled(nameof(BackupRestoreFeatureFlags.UseBackupRestoreMvp));
    private bool BackupManualCreationEnabled() => BackupBaseEnabled() && _featureFlags.IsEnabled(nameof(BackupRestoreFeatureFlags.UseManualBackupCreation));
    private bool BackupVerificationEnabled() => BackupBaseEnabled() && _featureFlags.IsEnabled(nameof(BackupRestoreFeatureFlags.UseBackupVerification));
    private bool BackupRestorePreviewEnabled() => BackupBaseEnabled() && _featureFlags.IsEnabled(nameof(BackupRestoreFeatureFlags.UseBackupRestorePreview));
    private bool BackupRestoreExecutionEnabled() => BackupBaseEnabled() && _featureFlags.IsEnabled(nameof(BackupRestoreFeatureFlags.UseBackupRestoreExecution));
    private bool BackupMaintenanceEnabled() => BackupBaseEnabled() && _featureFlags.IsEnabled(nameof(BackupRestoreFeatureFlags.UseBackupMaintenanceMode));
    private static ResponseEnvelope BackupDisabled() => BackupFeatureDisabled("Backup / Restore MVP is disabled by feature flags.");
    private static ResponseEnvelope BackupFeatureDisabled(string message) => Error(message, ResponseStatus.Forbidden, ErrorCode.Forbidden);
    private static ResponseEnvelope BackupRestoreError(string message, ResponseStatus status, ErrorCode code, Dictionary<string, object> payload)
        => new ResponseEnvelope { Status = status, ErrorCode = code, Message = message, Payload = payload ?? new Dictionary<string, object>() };

    private sealed class BackupDataFile
    {
        public DateTime CreatedAtUtc { get; set; }
        public string DatabaseName { get; set; } = string.Empty;
        public List<BackupDataCollection> Collections { get; set; } = new List<BackupDataCollection>();
    }

    private sealed class BackupDataCollection
    {
        public string Name { get; set; } = string.Empty;
        public List<string> Documents { get; set; } = new List<string>();
    }
}
