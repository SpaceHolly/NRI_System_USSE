using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MongoDB.Driver;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;
using Nri.Shared.Utilities;

namespace Nri.Server.Application;

public partial class ServiceHub
{
    private const string CoreReferencePackId0188 = "nri.reference.demo.core";
    private static readonly Regex ReferenceToken0188 = new Regex(@"\$\{reference:([^}]+)\}", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly JsonSerializerOptions ReferencePackJson0188 = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    };

    public ResponseEnvelope DefinitionPackAdminPreview0188(CommandContext context)
    {
        RequireAdmin(context);
        var pack = LoadReferencePack0188(RequireReferencePackId0188(context.Request.Payload));
        var plan = BuildReferencePackPlan0188(pack.Manifest, pack.Records);
        _logger.Admin($"definitionPack.admin.preview pack={plan.PackId} valid={plan.IsValid} records={plan.Records.Count}");
        return Ok("Предварительная проверка пакета завершена.", ReferencePackPlanPayload0188(plan));
    }

    public ResponseEnvelope DefinitionPackAdminStatus0188(CommandContext context)
    {
        RequireAdmin(context);
        var pack = LoadReferencePack0188(RequireReferencePackId0188(context.Request.Payload));
        var plan = BuildReferencePackPlan0188(pack.Manifest, pack.Records);
        plan.IsDryRun = false;
        _logger.Admin($"definitionPack.admin.status pack={plan.PackId} valid={plan.IsValid} records={plan.Records.Count}");
        return Ok("Состояние пакета загружено.", ReferencePackPlanPayload0188(plan));
    }

    public ResponseEnvelope DefinitionPackAdminApply0188(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var pack = LoadReferencePack0188(RequireReferencePackId0188(context.Request.Payload));
        var plan = BuildReferencePackPlan0188(pack.Manifest, pack.Records);
        if (!plan.IsValid)
        {
            var response = Error(
                "Пакет не применён: исправьте ошибки предварительной проверки.",
                ResponseStatus.ValidationFailed,
                ErrorCode.ValidationFailed);
            response.Payload = ReferencePackPlanPayload0188(plan);
            return response;
        }

        if (plan.Records.Any(x => string.Equals(x.Classification, DefinitionPackRecordClassifications.UserModifiedConflict, StringComparison.OrdinalIgnoreCase)))
        {
            var response = Error(
                "Пакет не применён: обнаружены записи, изменённые пользователем. Они не перезаписываются автоматически.",
                ResponseStatus.Conflict,
                ErrorCode.Conflict);
            response.Payload = ReferencePackPlanPayload0188(plan);
            return response;
        }

        var ids = BuildReferencePackIdMap0188(pack.Records);
        foreach (var record in pack.Records)
        {
            var row = plan.Records.First(x => string.Equals(x.StableKey, record.StableKey, StringComparison.OrdinalIgnoreCase));
            if (string.Equals(row.Classification, DefinitionPackRecordClassifications.AlreadyCurrent, StringComparison.OrdinalIgnoreCase))
            {
                plan.SkippedCount++;
                continue;
            }
            if (!string.Equals(row.Classification, DefinitionPackRecordClassifications.Create, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(row.Classification, DefinitionPackRecordClassifications.SafeUpdate, StringComparison.OrdinalIgnoreCase))
            {
                plan.SkippedCount++;
                continue;
            }

            if (string.Equals(record.StorageKind, DefinitionPackStorageKinds.UnifiedDefinition, StringComparison.OrdinalIgnoreCase))
                ApplyUnifiedReferenceRecord0188(pack.Manifest, record, ids, actor.Id, context.Request.RequestId);
            else
                ApplyContentReferenceRecord0188(pack.Manifest, record, ids, actor.Id, context.Request.RequestId);

            if (string.Equals(row.Classification, DefinitionPackRecordClassifications.Create, StringComparison.OrdinalIgnoreCase))
                plan.CreatedCount++;
            else
                plan.UpdatedCount++;
        }

        plan.Applied = true;
        plan.IsDryRun = false;
        var refreshed = BuildReferencePackPlan0188(pack.Manifest, pack.Records);
        plan.Counts = refreshed.Counts;
        plan.Records = refreshed.Records;
        plan.Warnings.AddRange(refreshed.Warnings);
        _logger.Admin($"definitionPack.admin.apply pack={plan.PackId} created={plan.CreatedCount} updated={plan.UpdatedCount} skipped={plan.SkippedCount} actor={actor.Id}");
        return Ok("Эталонный пакет применён.", ReferencePackPlanPayload0188(plan));
    }

    private ReferenceDefinitionPackPlan BuildReferencePackPlan0188(
        ReferenceDefinitionPackManifest manifest,
        IReadOnlyCollection<ReferenceDefinitionPackRecord> records)
    {
        EnsureInitialDefinitionEditorProfiles0181();
        var plan = new ReferenceDefinitionPackPlan
        {
            PackId = manifest.PackId,
            DisplayName = manifest.DisplayName,
            SemanticVersion = manifest.SemanticVersion,
            ContentStatus = manifest.ContentStatus,
            TargetRuleSet = manifest.TargetRuleSet,
            ConflictPolicy = manifest.ConflictPolicy,
            Checksum = manifest.Checksum,
            IsDryRun = true
        };

        ValidateReferenceManifest0188(manifest, records, plan);
        var recordIndex = records
            .Where(x => !string.IsNullOrWhiteSpace(x.StableKey))
            .GroupBy(x => x.StableKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var record in records)
        {
            var row = new DefinitionPackRecordPlan
            {
                StableKey = record.StableKey,
                DisplayName = record.Name,
                Category = record.Category,
                ReferenceCount = record.References.Count,
                ReferenceSummary = string.Join(
                    ", ",
                    record.References
                        .Select(ReferenceKey0188)
                        .Where(recordIndex.ContainsKey)
                        .Select(key => recordIndex[key].Name)
                        .Distinct(StringComparer.OrdinalIgnoreCase))
            };
            ValidateReferenceRecord0188(manifest, record, recordIndex, row, plan);
            if (row.Findings.Count > 0
                && row.Findings.Any(x => x.StartsWith("missing:", StringComparison.OrdinalIgnoreCase)))
                row.Classification = DefinitionPackRecordClassifications.MissingDependency;
            else if (row.Findings.Count > 0)
                row.Classification = DefinitionPackRecordClassifications.InvalidReference;
            else
                ClassifyReferenceRecord0188(record, row);
            plan.Records.Add(row);
        }

        DetectReferenceCycles0188(records, plan);
        if (plan.Errors.Count > 0)
        {
            foreach (var row in plan.Records.Where(x => string.Equals(x.Classification, DefinitionPackRecordClassifications.Create, StringComparison.OrdinalIgnoreCase)))
                if (row.Findings.Count > 0) row.Classification = DefinitionPackRecordClassifications.InvalidReference;
        }

        plan.Counts = plan.Records
            .GroupBy(x => x.Classification, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Count(), StringComparer.OrdinalIgnoreCase);
        plan.IsValid = plan.Errors.Count == 0
                       && plan.Records.All(x => !string.Equals(x.Classification, DefinitionPackRecordClassifications.MissingDependency, StringComparison.OrdinalIgnoreCase)
                                                && !string.Equals(x.Classification, DefinitionPackRecordClassifications.InvalidReference, StringComparison.OrdinalIgnoreCase)
                                                && !string.Equals(x.Classification, DefinitionPackRecordClassifications.IncompatibleSchema, StringComparison.OrdinalIgnoreCase));
        return plan;
    }

    private static void ValidateReferenceManifest0188(
        ReferenceDefinitionPackManifest manifest,
        IReadOnlyCollection<ReferenceDefinitionPackRecord> records,
        ReferenceDefinitionPackPlan plan)
    {
        if (!string.Equals(manifest.PackId, CoreReferencePackId0188, StringComparison.Ordinal))
            plan.Errors.Add("Неизвестный PackId.");
        if (string.IsNullOrWhiteSpace(manifest.DisplayName)) plan.Errors.Add("DisplayName пакета обязателен.");
        if (string.IsNullOrWhiteSpace(manifest.SemanticVersion)
            || !Version.TryParse(manifest.SemanticVersion, out _))
            plan.Errors.Add("SemanticVersion пакета должен быть корректной семантической версией.");
        if (manifest.SchemaVersion != 1) plan.Errors.Add("SchemaVersion пакета несовместим.");
        if (string.IsNullOrWhiteSpace(manifest.TargetRuleSet))
            plan.Errors.Add("TargetRuleSet пакета обязателен.");
        if (!string.Equals(manifest.ContentStatus, DefinitionPackContentStatuses.ReferenceDemo, StringComparison.Ordinal))
            plan.Errors.Add("Эталонный пакет должен иметь ContentStatus=ReferenceDemo.");
        if (!string.Equals(manifest.ConflictPolicy, DefinitionPackConflictPolicies.PreserveUserChanges, StringComparison.Ordinal))
            plan.Errors.Add("Поддерживается только безопасная политика PreserveUserChanges.");
        if (records.Count == 0) plan.Errors.Add("Пакет не содержит записей.");
        if (records.GroupBy(x => x.StableKey, StringComparer.OrdinalIgnoreCase).Any(x => string.IsNullOrWhiteSpace(x.Key) || x.Count() > 1))
            plan.Errors.Add("StableKey записей должны быть заполнены и уникальны.");
        if (manifest.Records.Count != records.Count)
            plan.Errors.Add("Manifest Records не совпадает с records.json.");
        var descriptors = manifest.Records
            .GroupBy(x => x.StableKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
        foreach (var record in records)
        {
            if (!descriptors.TryGetValue(record.StableKey, out var descriptor))
            {
                plan.Errors.Add($"{record.StableKey}: запись отсутствует в manifest.");
                continue;
            }
            if (!string.Equals(descriptor.RecordVersion, record.RecordVersion, StringComparison.Ordinal)
                || !string.Equals(descriptor.Category, record.Category, StringComparison.Ordinal)
                || !string.Equals(descriptor.StorageKind, record.StorageKind, StringComparison.Ordinal)
                || !string.Equals(descriptor.Checksum, record.Checksum, StringComparison.OrdinalIgnoreCase))
                plan.Errors.Add($"{record.StableKey}: descriptor manifest не совпадает с records.json.");
        }

        var actualManifestChecksum = ManifestChecksum0188(records);
        if (!string.Equals(manifest.Checksum, actualManifestChecksum, StringComparison.OrdinalIgnoreCase))
            plan.Errors.Add("Checksum manifest не совпадает с содержимым пакета.");
    }

    private void ValidateReferenceRecord0188(
        ReferenceDefinitionPackManifest manifest,
        ReferenceDefinitionPackRecord record,
        IReadOnlyDictionary<string, ReferenceDefinitionPackRecord> index,
        DefinitionPackRecordPlan row,
        ReferenceDefinitionPackPlan plan)
    {
        if (string.IsNullOrWhiteSpace(record.Name)) row.Findings.Add("invalid:name");
        if (string.IsNullOrWhiteSpace(record.Category)) row.Findings.Add("invalid:category");
        if (!string.Equals(record.StorageKind, DefinitionPackStorageKinds.UnifiedDefinition, StringComparison.Ordinal)
            && !string.Equals(record.StorageKind, DefinitionPackStorageKinds.ContentDefinition, StringComparison.Ordinal))
            row.Findings.Add("invalid:storage");
        if (string.IsNullOrWhiteSpace(record.RecordVersion)) row.Findings.Add("invalid:recordVersion");
        if (!string.Equals(record.Checksum, RecordChecksum0188(record), StringComparison.OrdinalIgnoreCase))
            row.Findings.Add("invalid:checksum");
        if (string.Equals(record.StorageKind, DefinitionPackStorageKinds.UnifiedDefinition, StringComparison.Ordinal)
            && !CoreEquipmentDefinitionFamilies.IsSupported(record.Category)
            && !MagicEffectConditionDefinitionFamilies.IsSupported(record.Category))
            row.Findings.Add("invalid:unified-category");
        if (string.Equals(record.StorageKind, DefinitionPackStorageKinds.ContentDefinition, StringComparison.Ordinal)
            && !WorldLoreCalendarDefinitionCategories.IsSupported(record.Category)
            && !FactionOrganizationEconomyDefinitionCategories.IsSupported(record.Category)
            && !TechnologyRecipeBlueprintProjectDefinitionCategories.IsSupported(record.Category))
            row.Findings.Add("invalid:content-category");
        if (string.Equals(record.StorageKind, DefinitionPackStorageKinds.ContentDefinition, StringComparison.Ordinal))
            ValidateRequiredProfileFields0188(record, row);
        if (PersistedStableKeyCount0188(record) > 1)
            row.Findings.Add("invalid:duplicate-persisted-stable-key");

        foreach (var reference in record.References)
        {
            var targetKey = ReferenceKey0188(reference);
            var expectedCategory = ReferenceCategory0188(reference);
            if (!index.TryGetValue(targetKey, out var target))
            {
                row.Findings.Add("missing:" + targetKey);
                continue;
            }
            if (!string.IsNullOrWhiteSpace(expectedCategory)
                && !string.Equals(target.Category, expectedCategory, StringComparison.OrdinalIgnoreCase))
                row.Findings.Add($"wrong-type:{targetKey}:{expectedCategory}");
            if (IsPlayerVisible0188(record.VisibilityRule) && !IsPlayerVisible0188(target.VisibilityRule))
                row.Findings.Add("hidden-target:" + targetKey);
        }

        foreach (var token in ExtractReferenceTokens0188(record.Fields))
        {
            if (!index.ContainsKey(token)) row.Findings.Add("missing-token:" + token);
        }
        ValidateEmbeddedGraphFields0188(record, row);

        foreach (var finding in row.Findings)
            plan.Errors.Add($"{record.StableKey}: {finding}");
    }

    private void ValidateRequiredProfileFields0188(ReferenceDefinitionPackRecord record, DefinitionPackRecordPlan row)
    {
        var profile = _mongo.DefinitionEditorProfiles
            .Find(Builders<DefinitionEditorProfile>.Filter.Eq(x => x.Category, record.Category))
            .FirstOrDefault();
        if (profile == null)
        {
            row.Findings.Add("invalid:missing-editor-profile");
            return;
        }

        foreach (var field in profile.FieldSchemas.Where(x => x.IsRequired))
        {
            if (!record.Fields.TryGetValue(field.FieldName, out var value)
                || string.IsNullOrWhiteSpace(CanonicalValue0188(value)))
                row.Findings.Add("invalid:required-field:" + field.FieldName);
        }
    }

    private static void ValidateEmbeddedGraphFields0188(
        ReferenceDefinitionPackRecord record,
        DefinitionPackRecordPlan row)
    {
        if (!string.Equals(
                record.Category,
                TechnologyRecipeBlueprintProjectDefinitionCategories.ProjectTemplate,
                StringComparison.OrdinalIgnoreCase)
            || !record.Fields.TryGetValue("stageRows", out var stageRowsValue))
            return;

        var rows = CanonicalValue0188(stageRowsValue)
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Split('|').Select(part => part.Trim()).ToArray())
            .Where(x => x.Length > 0 && !string.IsNullOrWhiteSpace(x[0]))
            .ToList();
        var keys = new HashSet<string>(rows.Select(x => x[0]), StringComparer.OrdinalIgnoreCase);
        var edges = rows.ToDictionary(
            x => x[0],
            x => x.Length > 3
                ? x[3].Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(value => value.Trim())
                    .Where(value => value.Length > 0)
                    .ToList()
                : new List<string>(),
            StringComparer.OrdinalIgnoreCase);
        foreach (var dependency in edges.SelectMany(x => x.Value))
            if (!keys.Contains(dependency))
                row.Findings.Add("missing-stage:" + dependency);

        var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        bool Visit(string key)
        {
            if (visiting.Contains(key)) return true;
            if (!visited.Add(key)) return false;
            visiting.Add(key);
            foreach (var target in edges.TryGetValue(key, out var targets) ? targets : new List<string>())
                if (keys.Contains(target) && Visit(target)) return true;
            visiting.Remove(key);
            return false;
        }

        if (keys.Any(Visit)) row.Findings.Add("invalid:stage-cycle");
    }

    private void ClassifyReferenceRecord0188(ReferenceDefinitionPackRecord record, DefinitionPackRecordPlan row)
    {
        if (string.Equals(record.StorageKind, DefinitionPackStorageKinds.UnifiedDefinition, StringComparison.OrdinalIgnoreCase))
        {
            var existing = FindUnifiedReferenceRecord0188(record.StableKey);
            if (existing == null) return;
            row.ExistingRecordId = existing.Id;
            row.PersistenceVersion = existing.UpdatedAtUtc.ToString("O", CultureInfo.InvariantCulture);
            if (existing.IsArchived || existing.Archived)
            {
                row.Classification = DefinitionPackRecordClassifications.ArchivedTarget;
                return;
            }
            var current = PersistedUnifiedChecksum0188(existing);
            if (!string.IsNullOrWhiteSpace(existing.PackAppliedContentChecksum)
                && !string.Equals(current, existing.PackAppliedContentChecksum, StringComparison.OrdinalIgnoreCase))
            {
                row.Classification = DefinitionPackRecordClassifications.UserModifiedConflict;
                return;
            }
            row.Classification = string.Equals(existing.PackRecordChecksum, record.Checksum, StringComparison.OrdinalIgnoreCase)
                ? DefinitionPackRecordClassifications.AlreadyCurrent
                : DefinitionPackRecordClassifications.SafeUpdate;
            return;
        }

        var content = FindContentReferenceRecord0188(record.StableKey);
        if (content == null) return;
        row.ExistingRecordId = content.Id;
        row.PersistenceVersion = content.Revision.ToString(CultureInfo.InvariantCulture);
        if (content.IsArchived || content.Archived)
        {
            row.Classification = DefinitionPackRecordClassifications.ArchivedTarget;
            return;
        }
        var persisted = PersistedContentChecksum0188(content);
        if (!string.IsNullOrWhiteSpace(content.PackAppliedContentChecksum)
            && !string.Equals(persisted, content.PackAppliedContentChecksum, StringComparison.OrdinalIgnoreCase))
        {
            row.Classification = DefinitionPackRecordClassifications.UserModifiedConflict;
            return;
        }
        row.Classification = string.Equals(content.PackRecordChecksum, record.Checksum, StringComparison.OrdinalIgnoreCase)
            ? DefinitionPackRecordClassifications.AlreadyCurrent
            : DefinitionPackRecordClassifications.SafeUpdate;
    }

    private Dictionary<string, string> BuildReferencePackIdMap0188(IEnumerable<ReferenceDefinitionPackRecord> records)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var record in records)
        {
            var existingId = string.Equals(record.StorageKind, DefinitionPackStorageKinds.UnifiedDefinition, StringComparison.OrdinalIgnoreCase)
                ? FindUnifiedReferenceRecord0188(record.StableKey)?.Id
                : FindContentReferenceRecord0188(record.StableKey)?.Id;
            result[record.StableKey] = string.IsNullOrWhiteSpace(existingId) ? Guid.NewGuid().ToString("N") : existingId;
        }
        return result;
    }

    private void ApplyUnifiedReferenceRecord0188(
        ReferenceDefinitionPackManifest manifest,
        ReferenceDefinitionPackRecord source,
        IReadOnlyDictionary<string, string> ids,
        string actorId,
        string? requestId)
    {
        var existing = FindUnifiedReferenceRecord0188(source.StableKey);
        var now = DateTime.UtcNow;
        var document = existing ?? new UnifiedDefinitionDocument
        {
            Id = ids[source.StableKey],
            CreatedAtUtc = now,
            CreatedUtc = now
        };
        document.Category = source.Category;
        document.RuleSetIds = new List<string> { manifest.TargetRuleSet };
        document.Name = source.Name;
        document.PublicDescription = source.PublicDescription;
        document.GMDescription = source.GMDescription;
        document.VisibilityRule = source.VisibilityRule;
        document.Tags = source.Tags.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        document.ExtraData = ResolveReferenceObjectMap0188(source.Fields, ids);
        document.ServerOnlyData = ResolveReferenceObjectMap0188(source.ServerOnlyData, ids);
        document.ServerOnlyData["lastDefinitionPackActorId"] = actorId;
        document.ServerOnlyData["lastDefinitionPackRequestId"] = requestId ?? string.Empty;
        document.IsArchived = false;
        document.Archived = false;
        document.SourceDocument = "definition_pack:" + manifest.PackId;
        document.ContentStatus = DefinitionPackContentStatuses.ReferenceDemo;
        document.DefinitionPackId = manifest.PackId;
        document.DefinitionPackVersion = manifest.SemanticVersion;
        document.StableKey = source.StableKey;
        document.RecordVersion = source.RecordVersion;
        document.PackRecordChecksum = source.Checksum;
        document.PackAppliedAtUtc = now;
        document.UpdatedAtUtc = now;
        document.UpdatedUtc = now;
        document.PackAppliedContentChecksum = PersistedUnifiedChecksum0188(document);
        _mongo.UnifiedDefinitions.ReplaceOne(
            Builders<UnifiedDefinitionDocument>.Filter.Eq(x => x.Id, document.Id),
            document,
            new ReplaceOptions { IsUpsert = true });
    }

    private void ApplyContentReferenceRecord0188(
        ReferenceDefinitionPackManifest manifest,
        ReferenceDefinitionPackRecord source,
        IReadOnlyDictionary<string, string> ids,
        string actorId,
        string? requestId)
    {
        var existing = FindContentReferenceRecord0188(source.StableKey);
        var now = DateTime.UtcNow;
        var record = existing ?? new ContentDefinitionRecord
        {
            Id = ids[source.StableKey],
            CreatedAtUtc = now,
            CreatedUtc = now,
            CreatedByUserId = actorId
        };
        record.RuleSetId = manifest.TargetRuleSet;
        record.Category = source.Category;
        record.DefinitionType = source.Category;
        record.Name = source.Name;
        record.DisplayName = source.Name;
        record.ShortCode = source.StableKey;
        record.Tags = source.Tags.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        record.PublicDescription = source.PublicDescription;
        record.GMDescription = source.GMDescription;
        record.VisibilityRule = source.VisibilityRule;
        record.AllowedRuleSetIds = new List<string> { manifest.TargetRuleSet };
        record.SourceDocument = "definition_pack:" + manifest.PackId;
        record.SourceVersion = manifest.SemanticVersion;
        record.MigrationPolicy = DefinitionPackConflictPolicies.PreserveUserChanges;
        record.CustomFields = ResolveReferenceObjectMap0188(source.Fields, ids);
        record.ServerOnlyData = ResolveReferenceObjectMap0188(source.ServerOnlyData, ids);
        record.ServerOnlyData["lastDefinitionPackActorId"] = actorId;
        record.ServerOnlyData["lastDefinitionPackRequestId"] = requestId ?? string.Empty;
        record.ReferenceIds = source.References
            .Select(ReferenceKey0188)
            .Where(ids.ContainsKey)
            .Select(x => ids[x])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        record.IsArchived = false;
        record.Archived = false;
        record.ContentStatus = DefinitionPackContentStatuses.ReferenceDemo;
        record.DefinitionPackId = manifest.PackId;
        record.DefinitionPackVersion = manifest.SemanticVersion;
        record.StableKey = source.StableKey;
        record.RecordVersion = source.RecordVersion;
        record.PackRecordChecksum = source.Checksum;
        record.PackAppliedAtUtc = now;
        record.Revision = existing == null ? 1 : Math.Max(1, existing.Revision + 1);
        record.UpdatedAtUtc = now;
        record.UpdatedUtc = now;
        record.UpdatedByUserId = actorId;
        record.PackAppliedContentChecksum = PersistedContentChecksum0188(record);
        _mongo.ContentDefinitionRecords.ReplaceOne(
            Builders<ContentDefinitionRecord>.Filter.Eq(x => x.Id, record.Id),
            record,
            new ReplaceOptions { IsUpsert = true });

        WriteContentDefinitionAudit0181(
            GetAccountById0188(actorId),
            CommandNames.DefinitionPackAdminApply,
            record.Id,
            source.Category,
            source.Category,
            existing == null ? string.Empty : $"{existing.DisplayName}:revision={existing.Revision}",
            $"{record.DisplayName}:revision={record.Revision}",
            new[] { "DefinitionPackId", "StableKey", "RecordVersion", "CustomFields" },
            "Versioned reference seed pack apply");
    }

    private UserAccount GetAccountById0188(string id)
        => _mongo.Accounts.Find(Builders<UserAccount>.Filter.Eq(x => x.Id, id)).FirstOrDefault()
           ?? new UserAccount { Id = id, Login = "system", Roles = new List<UserRole> { UserRole.Admin } };

    private UnifiedDefinitionDocument? FindUnifiedReferenceRecord0188(string stableKey)
        => _mongo.UnifiedDefinitions.Find(
                Builders<UnifiedDefinitionDocument>.Filter.Eq(x => x.StableKey, stableKey)
                | (Builders<UnifiedDefinitionDocument>.Filter.Eq(x => x.SourceDocument, "definition_pack:" + CoreReferencePackId0188)
                   & Builders<UnifiedDefinitionDocument>.Filter.Eq(x => x.Id, stableKey)))
            .FirstOrDefault();

    private ContentDefinitionRecord? FindContentReferenceRecord0188(string stableKey)
        => _mongo.ContentDefinitionRecords.Find(
                Builders<ContentDefinitionRecord>.Filter.Eq(x => x.StableKey, stableKey)
                | (Builders<ContentDefinitionRecord>.Filter.Eq(x => x.DefinitionPackId, CoreReferencePackId0188)
                   & Builders<ContentDefinitionRecord>.Filter.Eq(x => x.ShortCode, stableKey)))
            .FirstOrDefault();

    private long PersistedStableKeyCount0188(ReferenceDefinitionPackRecord record)
    {
        if (string.Equals(record.StorageKind, DefinitionPackStorageKinds.UnifiedDefinition, StringComparison.OrdinalIgnoreCase))
        {
            var filter = Builders<UnifiedDefinitionDocument>.Filter.Eq(x => x.DefinitionPackId, CoreReferencePackId0188)
                         & Builders<UnifiedDefinitionDocument>.Filter.Eq(x => x.StableKey, record.StableKey);
            return _mongo.UnifiedDefinitions.CountDocuments(filter);
        }

        var contentFilter = Builders<ContentDefinitionRecord>.Filter.Eq(x => x.DefinitionPackId, CoreReferencePackId0188)
                            & Builders<ContentDefinitionRecord>.Filter.Eq(x => x.StableKey, record.StableKey);
        return _mongo.ContentDefinitionRecords.CountDocuments(contentFilter);
    }

    private static void DetectReferenceCycles0188(
        IReadOnlyCollection<ReferenceDefinitionPackRecord> records,
        ReferenceDefinitionPackPlan plan)
    {
        var hierarchicalCategories = new HashSet<string>(new[]
        {
            WorldLoreCalendarDefinitionCategories.Location,
            FactionOrganizationEconomyDefinitionCategories.Faction,
            FactionOrganizationEconomyDefinitionCategories.Organization,
            FactionOrganizationEconomyDefinitionCategories.Jurisdiction,
            FactionOrganizationEconomyDefinitionCategories.License,
            DefinitionCategoryIds.Effect,
            DefinitionCategoryIds.Condition,
            TechnologyRecipeBlueprintProjectDefinitionCategories.Technology,
            TechnologyRecipeBlueprintProjectDefinitionCategories.Blueprint
        }, StringComparer.OrdinalIgnoreCase);
        var categories = records.ToDictionary(x => x.StableKey, x => x.Category, StringComparer.OrdinalIgnoreCase);
        var edges = records.ToDictionary(
            x => x.StableKey,
            x => hierarchicalCategories.Contains(x.Category)
                ? x.References.Select(ReferenceKey0188)
                    .Where(y => !string.IsNullOrWhiteSpace(y)
                                && categories.TryGetValue(y, out var category)
                                && string.Equals(category, x.Category, StringComparison.OrdinalIgnoreCase))
                    .ToList()
                : new List<string>(),
            StringComparer.OrdinalIgnoreCase);
        var state = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var stack = new Stack<string>();
        bool Visit(string key)
        {
            if (state.TryGetValue(key, out var current)) return current == 1;
            state[key] = 1;
            stack.Push(key);
            foreach (var target in edges.TryGetValue(key, out var targets) ? targets : new List<string>())
            {
                if (!edges.ContainsKey(target)) continue;
                if (Visit(target))
                {
                    plan.Errors.Add("Цикл ссылок: " + string.Join(" -> ", stack.Reverse()) + " -> " + target);
                    return true;
                }
            }
            stack.Pop();
            state[key] = 2;
            return false;
        }
        foreach (var key in edges.Keys)
            if (!state.ContainsKey(key) && Visit(key)) break;
    }

    private static Dictionary<string, object> ResolveReferenceObjectMap0188(
        IDictionary<string, object> source,
        IReadOnlyDictionary<string, string> ids)
    {
        var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in source) result[pair.Key] = ResolveReferenceValue0188(pair.Value, ids);
        return result;
    }

    private static object ResolveReferenceValue0188(object? value, IReadOnlyDictionary<string, string> ids)
    {
        if (value == null) return string.Empty;
        if (value is JsonElement json) return ResolveJsonElement0188(json, ids);
        if (value is string text)
        {
            return ReferenceToken0188.Replace(text, match =>
            {
                var key = match.Groups[1].Value.Trim();
                return ids.TryGetValue(key, out var id) ? id : match.Value;
            });
        }
        if (value is IDictionary dictionary)
        {
            var map = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry entry in dictionary)
                map[Convert.ToString(entry.Key, CultureInfo.InvariantCulture) ?? string.Empty] = ResolveReferenceValue0188(entry.Value, ids);
            return map;
        }
        if (value is IEnumerable enumerable)
            return enumerable.Cast<object?>().Select(x => ResolveReferenceValue0188(x, ids)).ToArray();
        return value;
    }

    private static object ResolveJsonElement0188(JsonElement value, IReadOnlyDictionary<string, string> ids)
    {
        return value.ValueKind switch
        {
            JsonValueKind.Object => value.EnumerateObject().ToDictionary(x => x.Name, x => ResolveJsonElement0188(x.Value, ids), StringComparer.OrdinalIgnoreCase),
            JsonValueKind.Array => value.EnumerateArray().Select(x => ResolveJsonElement0188(x, ids)).ToArray(),
            JsonValueKind.String => ResolveReferenceValue0188(value.GetString() ?? string.Empty, ids),
            JsonValueKind.Number when value.TryGetInt64(out var integer) => integer,
            JsonValueKind.Number when value.TryGetDecimal(out var number) => number,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => string.Empty
        };
    }

    private static IEnumerable<string> ExtractReferenceTokens0188(object? value)
    {
        if (value == null) yield break;
        if (value is JsonElement json)
        {
            if (json.ValueKind == JsonValueKind.Object)
                foreach (var property in json.EnumerateObject())
                    foreach (var token in ExtractReferenceTokens0188(property.Value)) yield return token;
            else if (json.ValueKind == JsonValueKind.Array)
                foreach (var item in json.EnumerateArray())
                    foreach (var token in ExtractReferenceTokens0188(item)) yield return token;
            else if (json.ValueKind == JsonValueKind.String)
                foreach (Match match in ReferenceToken0188.Matches(json.GetString() ?? string.Empty))
                    yield return match.Groups[1].Value.Trim();
            yield break;
        }
        if (value is string text)
        {
            foreach (Match match in ReferenceToken0188.Matches(text)) yield return match.Groups[1].Value.Trim();
            yield break;
        }
        if (value is IDictionary dictionary)
        {
            foreach (DictionaryEntry entry in dictionary)
                foreach (var token in ExtractReferenceTokens0188(entry.Value)) yield return token;
            yield break;
        }
        if (value is IEnumerable enumerable)
            foreach (var item in enumerable)
                foreach (var token in ExtractReferenceTokens0188(item)) yield return token;
    }

    private static string PersistedUnifiedChecksum0188(UnifiedDefinitionDocument document)
        => Hash0188(string.Join("|",
            document.Category,
            document.Name,
            document.PublicDescription,
            document.GMDescription,
            document.VisibilityRule,
            CanonicalValue0188(document.Tags),
            CanonicalValue0188(document.ExtraData)));

    private static string PersistedContentChecksum0188(ContentDefinitionRecord record)
        => Hash0188(string.Join("|",
            record.Category,
            record.DisplayName,
            record.PublicDescription,
            record.GMDescription,
            record.VisibilityRule,
            CanonicalValue0188(record.Tags),
            CanonicalValue0188(record.CustomFields)));

    private static string RecordChecksum0188(ReferenceDefinitionPackRecord record)
        => Hash0188(string.Join("|",
            record.StableKey,
            record.RecordVersion,
            record.Category,
            record.StorageKind,
            record.Name,
            record.PublicDescription,
            record.GMDescription,
            record.VisibilityRule,
            CanonicalValue0188(record.Tags),
            CanonicalValue0188(record.Fields),
            CanonicalValue0188(record.References)));

    private static string ManifestChecksum0188(IEnumerable<ReferenceDefinitionPackRecord> records)
        => Hash0188(string.Join("|", records.OrderBy(x => x.StableKey, StringComparer.Ordinal).Select(x => x.Checksum)));

    private static string CanonicalValue0188(object? value)
    {
        if (value == null) return "null";
        if (value is JsonElement json)
        {
            return json.ValueKind switch
            {
                JsonValueKind.Object => "{" + string.Join(",", json.EnumerateObject().OrderBy(x => x.Name, StringComparer.Ordinal).Select(x => x.Name + ":" + CanonicalValue0188(x.Value))) + "}",
                JsonValueKind.Array => "[" + string.Join(",", json.EnumerateArray().Select(x => CanonicalValue0188(x))) + "]",
                JsonValueKind.String => json.GetString() ?? string.Empty,
                _ => json.ToString()
            };
        }
        if (value is IDictionary dictionary)
        {
            var pairs = new List<KeyValuePair<string, object?>>();
            foreach (DictionaryEntry entry in dictionary)
                pairs.Add(new KeyValuePair<string, object?>(Convert.ToString(entry.Key, CultureInfo.InvariantCulture) ?? string.Empty, entry.Value));
            return "{" + string.Join(",", pairs.OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => x.Key + ":" + CanonicalValue0188(x.Value))) + "}";
        }
        if (value is string text) return text;
        if (value is IEnumerable enumerable) return "[" + string.Join(",", enumerable.Cast<object?>().Select(CanonicalValue0188)) + "]";
        if (value is IFormattable formattable) return formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty;
        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static string Hash0188(string value)
    {
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty)))
            .Replace("-", string.Empty)
            .ToLowerInvariant();
    }

    private static bool IsPlayerVisible0188(string visibility)
        => string.Equals(visibility, ContentDefinitionVisibilityRules.Public, StringComparison.OrdinalIgnoreCase)
           || string.Equals(visibility, ContentDefinitionVisibilityRules.PlayerVisible, StringComparison.OrdinalIgnoreCase)
           || string.Equals(visibility, VisibilityRuleIds.Public, StringComparison.OrdinalIgnoreCase)
           || string.Equals(visibility, VisibilityRuleIds.PlayerVisible, StringComparison.OrdinalIgnoreCase);

    private static string ReferenceKey0188(string value)
        => (value ?? string.Empty).Split(new[] { '#' }, 2)[0].Trim();

    private static string ReferenceCategory0188(string value)
    {
        var parts = (value ?? string.Empty).Split(new[] { '#' }, 2);
        return parts.Length == 2 ? parts[1].Trim() : string.Empty;
    }

    private static string RequireReferencePackId0188(IDictionary<string, object> payload)
    {
        var id = PayloadReader.GetString(payload, "packId") ?? CoreReferencePackId0188;
        if (!string.Equals(id, CoreReferencePackId0188, StringComparison.Ordinal))
            throw new KeyNotFoundException("Эталонный пакет не найден.");
        return id;
    }

    private static ReferencePackFiles0188 LoadReferencePack0188(string packId)
    {
        var directory = ResolveReferencePackDirectory0188(packId);
        var manifestPath = Path.Combine(directory, "manifest.json");
        var recordsPath = Path.Combine(directory, "records.json");
        if (!File.Exists(manifestPath) || !File.Exists(recordsPath))
            throw new FileNotFoundException("Файлы эталонного пакета не найдены.");
        var manifest = JsonSerializer.Deserialize<ReferenceDefinitionPackManifest>(
                           File.ReadAllText(manifestPath, Encoding.UTF8),
                           ReferencePackJson0188)
                       ?? throw new InvalidDataException("Manifest пакета повреждён.");
        var records = JsonSerializer.Deserialize<List<ReferenceDefinitionPackRecord>>(
                          File.ReadAllText(recordsPath, Encoding.UTF8),
                          ReferencePackJson0188)
                      ?? throw new InvalidDataException("Records пакета повреждены.");
        return new ReferencePackFiles0188(manifest, records);
    }

    private static string ResolveReferencePackDirectory0188(string packId)
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Content", "DefinitionPacks", packId),
            Path.Combine(Directory.GetCurrentDirectory(), "Nri.Server", "Content", "DefinitionPacks", packId),
            Path.Combine(Directory.GetCurrentDirectory(), "Content", "DefinitionPacks", packId)
        };
        return candidates.FirstOrDefault(Directory.Exists) ?? candidates[0];
    }

    private static Dictionary<string, object> ReferencePackPlanPayload0188(ReferenceDefinitionPackPlan plan)
        => new Dictionary<string, object>
        {
            ["packId"] = plan.PackId,
            ["displayName"] = plan.DisplayName,
            ["semanticVersion"] = plan.SemanticVersion,
            ["contentStatus"] = plan.ContentStatus,
            ["targetRuleSet"] = plan.TargetRuleSet,
            ["conflictPolicy"] = plan.ConflictPolicy,
            ["checksum"] = plan.Checksum,
            ["isValid"] = plan.IsValid,
            ["isDryRun"] = plan.IsDryRun,
            ["applied"] = plan.Applied,
            ["createdCount"] = plan.CreatedCount,
            ["updatedCount"] = plan.UpdatedCount,
            ["skippedCount"] = plan.SkippedCount,
            ["counts"] = plan.Counts.ToDictionary(x => x.Key, x => (object)x.Value),
            ["records"] = plan.Records.Select(x => (object)new Dictionary<string, object>
            {
                ["stableKey"] = x.StableKey,
                ["displayName"] = x.DisplayName,
                ["category"] = x.Category,
                ["classification"] = x.Classification,
                ["persistenceVersion"] = x.PersistenceVersion,
                ["referenceCount"] = x.ReferenceCount,
                ["referenceSummary"] = x.ReferenceSummary,
                ["findings"] = x.Findings.Cast<object>().ToArray()
            }).ToArray(),
            ["errors"] = plan.Errors.Cast<object>().ToArray(),
            ["warnings"] = plan.Warnings.Cast<object>().ToArray(),
            ["builtAtUtc"] = plan.BuiltAtUtc
        };

    private sealed class ReferencePackFiles0188
    {
        public ReferencePackFiles0188(ReferenceDefinitionPackManifest manifest, List<ReferenceDefinitionPackRecord> records)
        {
            Manifest = manifest;
            Records = records;
        }

        public ReferenceDefinitionPackManifest Manifest { get; }
        public List<ReferenceDefinitionPackRecord> Records { get; }
    }
}
