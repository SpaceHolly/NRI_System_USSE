using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using MongoDB.Bson;
using MongoDB.Driver;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;
using Nri.Shared.Utilities;

namespace Nri.Server.Application;

public sealed class ContentDefinitionProjectionService0181
{
    public Dictionary<string, object> ProfilePayload(DefinitionEditorProfile profile, bool includeServerConfig)
    {
        var payload = new Dictionary<string, object>
        {
            ["profileId"] = profile.Id,
            ["id"] = profile.Id,
            ["worldId"] = profile.WorldId,
            ["ruleSetId"] = profile.RuleSetId,
            ["category"] = profile.Category,
            ["displayName"] = profile.DisplayName,
            ["description"] = profile.Description,
            ["storageMode"] = profile.StorageMode,
            ["backingCollectionName"] = includeServerConfig ? profile.BackingCollectionName : string.Empty,
            ["canCreate"] = profile.CanCreate,
            ["canEdit"] = profile.CanEdit,
            ["canArchive"] = profile.CanArchive,
            ["canClone"] = profile.CanClone,
            ["canPreviewAsPlayer"] = profile.CanPreviewAsPlayer,
            ["defaultVisibilityRule"] = profile.DefaultVisibilityRule,
            ["defaultTags"] = profile.DefaultTags.Cast<object>().ToArray(),
            ["schemaVersion"] = profile.SchemaVersion,
            ["isArchived"] = profile.IsArchived,
            ["updatedAtUtc"] = profile.UpdatedAtUtc,
            ["fieldSchemas"] = profile.FieldSchemas
                .OrderBy(x => x.DisplayOrder)
                .Select(FieldSchemaPayload)
                .Cast<object>()
                .ToArray()
        };

        if (includeServerConfig)
        {
            payload["allowedRoles"] = profile.AllowedRoles.Cast<object>().ToArray();
            payload["requiredBaseFields"] = profile.RequiredBaseFields.Cast<object>().ToArray();
            payload["validationRules"] = profile.ValidationRules.Cast<object>().ToArray();
            payload["referenceRules"] = profile.ReferenceRules.Select(ReferenceRulePayload).Cast<object>().ToArray();
        }

        return payload;
    }

    public Dictionary<string, object> AdminRecordPayload(ContentDefinitionRecord record, DefinitionEditorProfile? profile, ContentDefinitionValidationResult? validation, bool includeAuditSummary)
    {
        return new Dictionary<string, object>
        {
            ["definitionId"] = record.Id,
            ["id"] = record.Id,
            ["worldId"] = record.WorldId,
            ["campaignId"] = record.CampaignId,
            ["ruleSetId"] = record.RuleSetId,
            ["category"] = record.Category,
            ["definitionType"] = record.DefinitionType,
            ["name"] = record.Name,
            ["displayName"] = record.DisplayName,
            ["shortCode"] = record.ShortCode,
            ["tags"] = record.Tags.Cast<object>().ToArray(),
            ["publicTags"] = record.Tags.Where(IsPlayerSafeTag).Select(ToPublicTag).Where(x => !string.IsNullOrWhiteSpace(x)).Cast<object>().ToArray(),
            ["systemTags"] = record.Tags.Where(IsSystemTag).Select(x => x.Trim()).Where(x => !string.IsNullOrWhiteSpace(x)).Cast<object>().ToArray(),
            ["publicDescription"] = record.PublicDescription,
            ["gmDescription"] = record.GMDescription,
            ["serverOnlyData"] = CloneDictionary(record.ServerOnlyData),
            ["visibilityRule"] = record.VisibilityRule,
            ["allowedRuleSetIds"] = record.AllowedRuleSetIds.Cast<object>().ToArray(),
            ["forbiddenRuleSetIds"] = record.ForbiddenRuleSetIds.Cast<object>().ToArray(),
            ["requiredModules"] = record.RequiredModules.Cast<object>().ToArray(),
            ["compatibilityTags"] = record.CompatibilityTags.Cast<object>().ToArray(),
            ["sourceDocument"] = record.SourceDocument,
            ["sourceVersion"] = record.SourceVersion,
            ["calculationVersion"] = record.CalculationVersion,
            ["migrationPolicy"] = record.MigrationPolicy,
            ["customFields"] = CloneDictionary(record.CustomFields),
            ["referenceIds"] = record.ReferenceIds.Cast<object>().ToArray(),
            ["entityRevision"] = record.Revision,
            ["schemaVersion"] = record.SchemaVersion,
            ["isArchived"] = record.IsArchived,
            ["createdAtUtc"] = record.CreatedAtUtc,
            ["updatedAtUtc"] = record.UpdatedAtUtc,
            ["createdByUserId"] = record.CreatedByUserId,
            ["updatedByUserId"] = record.UpdatedByUserId,
            ["profile"] = profile == null ? new Dictionary<string, object>() : ProfilePayload(profile, includeServerConfig: true),
            ["validation"] = validation == null ? new Dictionary<string, object>() : ValidationPayload(validation),
            ["auditSummaryIncluded"] = includeAuditSummary
        };
    }

    public Dictionary<string, object> PlayerRecordPayload(ContentDefinitionRecord record, DefinitionEditorProfile? profile)
    {
        return new Dictionary<string, object>
        {
            ["definitionId"] = record.Id,
            ["id"] = record.Id,
            ["displayName"] = record.DisplayName,
            ["category"] = record.Category,
            ["publicDescription"] = PlayerDescription0181(record),
            ["updatedAtUtc"] = record.UpdatedAtUtc
        };
    }

    public Dictionary<string, object> PlayerDetailPayload(ContentDefinitionRecord record, DefinitionEditorProfile? profile)
    {
        var facts = new List<object>();
        var isInternalMagicDirection = record.Tags.Contains("magic_method_internal_direction", StringComparer.OrdinalIgnoreCase);
        if (profile != null && !isInternalMagicDirection)
        {
            foreach (var schema in profile.FieldSchemas.OrderBy(x => x.DisplayOrder))
            {
                if (!schema.IsPlayerVisible || schema.IsGmOnly || schema.IsServerOnly) continue;
                if (!PlayerFactAllowed0181(record.Category, schema.FieldName)) continue;
                if (!record.CustomFields.TryGetValue(schema.FieldName, out var value)) continue;
                var displayValue = PlayerDisplayValue0181(schema, value);
                if (string.IsNullOrWhiteSpace(displayValue)) continue;

                facts.Add(new Dictionary<string, object>
                {
                    ["label"] = PlayerFactLabel0181(schema.FieldName, schema.DisplayName),
                    ["value"] = displayValue
                });
            }
        }

        if (isInternalMagicDirection)
        {
            var parent = Regex.Match(record.PublicDescription ?? string.Empty, "Родительский метод:\\s*[«\"](?<name>[^»\"]+)[»\"]", RegexOptions.IgnoreCase);
            if (parent.Success)
                facts.Insert(0, new Dictionary<string, object> { ["label"] = "Родительский метод", ["value"] = parent.Groups["name"].Value });
            facts.Add(new Dictionary<string, object>
            {
                ["label"] = "Развитие",
                ["value"] = "Условия развития и стоимость пока не утверждены."
            });
        }

        return new Dictionary<string, object>
        {
            ["displayName"] = record.DisplayName,
            ["categoryLabel"] = PlayerCategoryLabel0181(record.Category, profile?.DisplayName),
            ["publicDescription"] = PlayerDescription0181(record),
            ["playerFacts"] = facts.ToArray()
        };
    }

    private static bool PlayerFactAllowed0181(string category, string fieldName)
    {
        var fields = (category ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "attribute_definition" or "subattribute_definition" => new[] { "minValue", "maxValue", "defaultValue" },
            "class_definition" => new[] { "maxRank", "isPlayable" },
            "skill_definition" => new[] { "rankMin", "rankMax", "isRollable" },
            "development_node_definition" => new[] { "tier", "maxTier", "requirements", "cost", "rewards" },
            "magic_method_definition" => new[] { "preparationTime", "resourceCost", "difficulty" },
            "magic_element_definition" => new[] { "opposedElement", "description" },
            "currency_definition" => new[] { "symbol", "decimalPlaces", "isPrimary" },
            _ => Array.Empty<string>()
        };
        return fields.Contains(fieldName, StringComparer.OrdinalIgnoreCase);
    }

    private static string PlayerFactLabel0181(string fieldName, string fallback)
        => (fieldName ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "minvalue" => "Минимальное значение",
            "maxvalue" => "Максимальное значение",
            "defaultvalue" => "Начальное значение",
            "maxrank" or "rankmax" => "Максимальный ранг",
            "rankmin" => "Начальный ранг",
            "isplayable" => "Доступно персонажу",
            "isrollable" => "Можно использовать в проверках",
            "tier" => "Уровень",
            "maxtier" => "Максимальный уровень",
            "requirements" => "Условия развития",
            "cost" or "resourcecost" => "Стоимость",
            "rewards" => "Результат",
            "preparationtime" => "Подготовка",
            "difficulty" => "Сложность",
            "opposedelement" => "Противоположная стихия",
            "description" => "Описание",
            "symbol" => "Обозначение",
            "decimalplaces" => "Точность расчёта",
            "isprimary" => "Основная валюта",
            _ => fallback
        };

    private static string PlayerCategoryLabel0181(string category, string? fallback)
        => (category ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "attribute_definition" => "Характеристика",
            "subattribute_definition" => "Подхарактеристика",
            "class_definition" => "Класс",
            "skill_definition" => "Навык",
            "development_node_definition" => "Узел развития",
            "magic_method_definition" => "Метод магии",
            "magic_element_definition" => "Стихия",
            "currency_definition" => "Валюта",
            _ => string.IsNullOrWhiteSpace(fallback) ? "Справочник" : fallback
        };

    private static string PlayerDescription0181(ContentDefinitionRecord record)
    {
        var description = (record.PublicDescription ?? string.Empty).Trim();
        description = Regex.Replace(description, @"\s+для правил\s+[a-z0-9_.-]+\s*\.?", ".", RegexOptions.IgnoreCase);
        return string.IsNullOrWhiteSpace(description)
            ? $"Общедоступное описание записи «{record.DisplayName}» пока не добавлено."
            : description;
    }

    private static string PlayerDisplayValue0181(DefinitionFieldSchema schema, object value)
    {
        if (string.Equals(schema.FieldType, ContentDefinitionFieldTypes.Reference, StringComparison.OrdinalIgnoreCase)
            || string.Equals(schema.FieldType, ContentDefinitionFieldTypes.ReferenceList, StringComparison.OrdinalIgnoreCase))
            return string.Empty;
        if (string.Equals(schema.FieldType, ContentDefinitionFieldTypes.Boolean, StringComparison.OrdinalIgnoreCase)
            && bool.TryParse(Convert.ToString(value), out var boolValue))
            return boolValue ? "Да" : "Нет";

        var raw = Convert.ToString(value) ?? string.Empty;
        var labels = new Dictionary<string, string>(schema.LocalizedValueLabels, StringComparer.OrdinalIgnoreCase);
        foreach (var pair in schema.OptionLabels) labels[pair.Key] = pair.Value;
        if (labels.TryGetValue(raw, out var localized)) return localized;
        return SafePlayerValue(value)?.ToString() ?? string.Empty;
    }

    public Dictionary<string, object> ValidationPayload(ContentDefinitionValidationResult validation)
        => new Dictionary<string, object>
        {
            ["validationId"] = validation.Id,
            ["definitionId"] = validation.DefinitionId,
            ["profileId"] = validation.ProfileId,
            ["status"] = validation.Status,
            ["errors"] = validation.Errors.Cast<object>().ToArray(),
            ["warnings"] = validation.Warnings.Cast<object>().ToArray(),
            ["brokenReferences"] = validation.BrokenReferences.Cast<object>().ToArray(),
            ["visibilityWarnings"] = validation.VisibilityWarnings.Cast<object>().ToArray(),
            ["schemaWarnings"] = validation.SchemaWarnings.Cast<object>().ToArray(),
            ["validatedAtUtc"] = validation.ValidatedAtUtc,
            ["schemaVersion"] = validation.SchemaVersion
        };

    public Dictionary<string, object> AuditPayload(ContentDefinitionAuditEvent audit)
        => new Dictionary<string, object>
        {
            ["auditId"] = audit.Id,
            ["actorUserId"] = audit.ActorUserId,
            ["actorRole"] = audit.ActorRole,
            ["command"] = audit.Command,
            ["definitionId"] = audit.DefinitionId,
            ["profileId"] = audit.ProfileId,
            ["category"] = audit.Category,
            ["oldValueSummary"] = audit.OldValueSummary,
            ["newValueSummary"] = audit.NewValueSummary,
            ["changedFields"] = audit.ChangedFields.Cast<object>().ToArray(),
            ["reason"] = audit.Reason,
            ["createdAtUtc"] = audit.CreatedAtUtc
        };

    private static Dictionary<string, object> FieldSchemaPayload(DefinitionFieldSchema schema)
        => new Dictionary<string, object>
        {
            ["fieldName"] = schema.FieldName,
            ["displayName"] = schema.DisplayName,
            ["shortLabel"] = schema.ShortLabel,
            ["fieldType"] = schema.FieldType,
            ["isRequired"] = schema.IsRequired,
            ["isPlayerVisible"] = schema.IsPlayerVisible,
            ["isGmOnly"] = schema.IsGmOnly,
            ["isServerOnly"] = schema.IsServerOnly,
            ["sectionKey"] = schema.SectionKey,
            ["allowedValues"] = schema.AllowedValues.Cast<object>().ToArray(),
            ["optionLabels"] = schema.OptionLabels.Count > 0
                ? schema.OptionLabels.ToDictionary(x => x.Key, x => (object)x.Value, StringComparer.OrdinalIgnoreCase)
                : schema.LocalizedValueLabels.ToDictionary(x => x.Key, x => (object)x.Value, StringComparer.OrdinalIgnoreCase),
            ["referenceCategory"] = schema.ReferenceCategory,
            ["referenceTargetTypes"] = schema.ReferenceTargetTypes.Cast<object>().ToArray(),
            ["referenceSelectionMode"] = schema.ReferenceSelectionMode,
            ["minValue"] = schema.MinValue.HasValue ? (object)schema.MinValue.Value : string.Empty,
            ["maxValue"] = schema.MaxValue.HasValue ? (object)schema.MaxValue.Value : string.Empty,
            ["minimum"] = schema.Minimum.HasValue ? (object)schema.Minimum.Value : schema.MinValue.HasValue ? schema.MinValue.Value : string.Empty,
            ["maximum"] = schema.Maximum.HasValue ? (object)schema.Maximum.Value : schema.MaxValue.HasValue ? schema.MaxValue.Value : string.Empty,
            ["step"] = schema.Step.HasValue ? (object)schema.Step.Value : string.Empty,
            ["unitLabel"] = schema.UnitLabel,
            ["defaultValue"] = schema.DefaultValue,
            ["helpText"] = schema.HelpText,
            ["placeholder"] = schema.Placeholder,
            ["sectionTitle"] = schema.SectionTitle,
            ["editorKind"] = string.IsNullOrWhiteSpace(schema.EditorKind) ? EditorKindFor0181(schema.FieldType) : schema.EditorKind,
            ["allowEmpty"] = schema.AllowEmpty,
            ["isMultiline"] = schema.IsMultiline,
            ["isAdvanced"] = schema.IsAdvanced,
            ["isReadOnly"] = schema.IsReadOnly,
            ["isSecret"] = schema.IsSecret,
            ["isSearchable"] = schema.IsSearchable,
            ["supportsUnknownLegacyValue"] = schema.SupportsUnknownLegacyValue,
            ["unknownValuePolicy"] = schema.UnknownValuePolicy,
            ["localizedValueLabels"] = schema.LocalizedValueLabels.ToDictionary(x => x.Key, x => (object)x.Value, StringComparer.OrdinalIgnoreCase),
            ["displayOrder"] = schema.DisplayOrder,
            ["validationRegex"] = schema.ValidationRegex,
            ["schemaVersion"] = schema.SchemaVersion
        };

    private static Dictionary<string, object> ReferenceRulePayload(DefinitionReferenceRule rule)
        => new Dictionary<string, object>
        {
            ["fieldName"] = rule.FieldName,
            ["referenceCategory"] = rule.ReferenceCategory,
            ["isRequired"] = rule.IsRequired,
            ["mustBePlayerVisibleWhenFieldIsPlayerVisible"] = rule.MustBePlayerVisibleWhenFieldIsPlayerVisible
        };

    private static string EditorKindFor0181(string fieldType)
    {
        if (string.Equals(fieldType, ContentDefinitionFieldTypes.LongText, StringComparison.OrdinalIgnoreCase)) return "multiline_text";
        if (string.Equals(fieldType, ContentDefinitionFieldTypes.Boolean, StringComparison.OrdinalIgnoreCase)) return "toggle";
        if (string.Equals(fieldType, ContentDefinitionFieldTypes.Enum, StringComparison.OrdinalIgnoreCase)) return "select";
        if (string.Equals(fieldType, ContentDefinitionFieldTypes.Reference, StringComparison.OrdinalIgnoreCase)) return "reference_picker";
        if (string.Equals(fieldType, ContentDefinitionFieldTypes.ReferenceList, StringComparison.OrdinalIgnoreCase)) return "reference_picker_multiple";
        if (string.Equals(fieldType, ContentDefinitionFieldTypes.Integer, StringComparison.OrdinalIgnoreCase)) return "integer";
        if (string.Equals(fieldType, ContentDefinitionFieldTypes.Decimal, StringComparison.OrdinalIgnoreCase)) return "decimal";
        return "text";
    }

    private static Dictionary<string, object> CloneDictionary(IDictionary<string, object> values)
    {
        var result = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var item in values)
            result[item.Key] = item.Value ?? string.Empty;
        return result;
    }

    private static object SafePlayerValue(object value)
    {
        if (value is IDictionary<string, object> map)
            return CloneDictionary(map);
        if (value is IDictionary dictionary)
        {
            var result = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (DictionaryEntry entry in dictionary)
            {
                var key = Convert.ToString(entry.Key) ?? string.Empty;
                if (key.StartsWith("gm", StringComparison.OrdinalIgnoreCase) || key.StartsWith("server", StringComparison.OrdinalIgnoreCase)) continue;
                result[key] = entry.Value ?? string.Empty;
            }
            return result;
        }
        return value ?? string.Empty;
    }

    private static IEnumerable<string> PlayerSafeReferences(IEnumerable<string> references)
        => references.Where(x => !string.IsNullOrWhiteSpace(x)
                                  && x.IndexOf("GM_ONLY", StringComparison.OrdinalIgnoreCase) < 0
                                  && x.IndexOf("SERVER_ONLY", StringComparison.OrdinalIgnoreCase) < 0);

    private static bool IsPlayerSafeTag(string tag)
        => !tag.StartsWith("gm:", StringComparison.OrdinalIgnoreCase)
           && !tag.StartsWith("server:", StringComparison.OrdinalIgnoreCase)
           && !tag.StartsWith("hidden:", StringComparison.OrdinalIgnoreCase)
           && !tag.StartsWith("foundation_", StringComparison.OrdinalIgnoreCase)
           && !tag.StartsWith("dev", StringComparison.OrdinalIgnoreCase)
           && !tag.StartsWith("test", StringComparison.OrdinalIgnoreCase)
           && !tag.Equals("character_foundation", StringComparison.OrdinalIgnoreCase)
           && !tag.StartsWith("acceptance", StringComparison.OrdinalIgnoreCase)
           && tag.IndexOf("0182", StringComparison.OrdinalIgnoreCase) < 0
           && !tag.EndsWith("_definition", StringComparison.OrdinalIgnoreCase);

    private static string ToPublicTag(string tag)
        => tag?.Trim() ?? string.Empty;

    private static bool IsSystemTag(string tag) => !IsPlayerSafeTag(tag ?? string.Empty);
}

public partial class ServiceHub
{
    private readonly ContentDefinitionProjectionService0181 _contentDefinitionProjection0181 = new ContentDefinitionProjectionService0181();

    public ResponseEnvelope ContentDefinitionAdminListProfiles(CommandContext context)
    {
        RequireAdmin(context);
        EnsureContentDefinitionEditorIndexes0181();
        EnsureInitialDefinitionEditorProfiles0181();
        var includeArchived = PayloadReader.GetBool(context.Request.Payload, "includeArchived");
        var filter = includeArchived
            ? FilterDefinition<DefinitionEditorProfile>.Empty
            : Builders<DefinitionEditorProfile>.Filter.Ne(x => x.IsArchived, true);
        var items = _mongo.DefinitionEditorProfiles.Find(filter).ToList()
            .OrderBy(x => x.Category, StringComparer.OrdinalIgnoreCase)
            .Select(x => _contentDefinitionProjection0181.ProfilePayload(x, includeServerConfig: true))
            .Cast<object>()
            .ToArray();
        return Ok("Definition editor profiles loaded.", new Dictionary<string, object> { ["profiles"] = items, ["count"] = items.Length });
    }

    public ResponseEnvelope ContentDefinitionAdminGetProfile(CommandContext context)
    {
        RequireAdmin(context);
        EnsureInitialDefinitionEditorProfiles0181();
        var profile = GetDefinitionProfile0181(RequireDefinitionProfileId0181(context.Request.Payload));
        return Ok("Definition editor profile loaded.", new Dictionary<string, object> { ["profile"] = _contentDefinitionProjection0181.ProfilePayload(profile, includeServerConfig: true) });
    }

    public ResponseEnvelope ContentDefinitionAdminCreateProfile(CommandContext context)
    {
        var actor = RequireSuperAdmin0181(context);
        EnsureContentDefinitionEditorIndexes0181();
        EnsureInitialDefinitionEditorProfiles0181();
        var profile = BuildProfileFromPayload0181(context.Request.Payload, existing: null);
        profile.CreatedAtUtc = DateTime.UtcNow;
        profile.UpdatedAtUtc = DateTime.UtcNow;
        profile.IsArchived = false;
        _mongo.DefinitionEditorProfiles.InsertOne(profile);
        WriteContentDefinitionAudit0181(actor, context.Request.Command, string.Empty, profile.Id, profile.Category, string.Empty, $"profile:{profile.DisplayName}", new[] { "profile" }, "profile created");
        PublishDefinitionSync0181("definitions.profile.created", "definition_editor_profile", profile.Id, "created", actor.Id, context.Request.RequestId);
        return Ok("Definition editor profile created.", new Dictionary<string, object> { ["profile"] = _contentDefinitionProjection0181.ProfilePayload(profile, includeServerConfig: true) });
    }

    public ResponseEnvelope ContentDefinitionAdminUpdateProfile(CommandContext context)
    {
        var actor = RequireSuperAdmin0181(context);
        EnsureInitialDefinitionEditorProfiles0181();
        var profile = GetDefinitionProfile0181(RequireDefinitionProfileId0181(context.Request.Payload));
        var before = $"{profile.Category}:{profile.DisplayName}:{profile.SchemaVersion}";
        profile = BuildProfileFromPayload0181(context.Request.Payload, profile);
        profile.UpdatedAtUtc = DateTime.UtcNow;
        _mongo.DefinitionEditorProfiles.ReplaceOne(Builders<DefinitionEditorProfile>.Filter.Eq(x => x.Id, profile.Id), profile);
        WriteContentDefinitionAudit0181(actor, context.Request.Command, string.Empty, profile.Id, profile.Category, before, $"{profile.Category}:{profile.DisplayName}:{profile.SchemaVersion}", new[] { "profile" }, "profile updated");
        PublishDefinitionSync0181("definitions.profile.updated", "definition_editor_profile", profile.Id, "updated", actor.Id, context.Request.RequestId);
        return Ok("Definition editor profile updated.", new Dictionary<string, object> { ["profile"] = _contentDefinitionProjection0181.ProfilePayload(profile, includeServerConfig: true) });
    }

    public ResponseEnvelope ContentDefinitionAdminArchiveProfile(CommandContext context)
    {
        var actor = RequireSuperAdmin0181(context);
        EnsureInitialDefinitionEditorProfiles0181();
        var profile = GetDefinitionProfile0181(RequireDefinitionProfileId0181(context.Request.Payload));
        profile.IsArchived = true;
        profile.Archived = true;
        profile.UpdatedAtUtc = DateTime.UtcNow;
        profile.UpdatedUtc = DateTime.UtcNow;
        _mongo.DefinitionEditorProfiles.ReplaceOne(Builders<DefinitionEditorProfile>.Filter.Eq(x => x.Id, profile.Id), profile);
        WriteContentDefinitionAudit0181(actor, context.Request.Command, string.Empty, profile.Id, profile.Category, "active", "archived", new[] { "IsArchived" }, "profile archived");
        PublishDefinitionSync0181("definitions.profile.archived", "definition_editor_profile", profile.Id, "archived", actor.Id, context.Request.RequestId);
        return Ok("Definition editor profile archived.", new Dictionary<string, object> { ["profileId"] = profile.Id, ["isArchived"] = true });
    }

    public ResponseEnvelope ContentDefinitionAdminList(CommandContext context)
    {
        RequireAdmin(context);
        EnsureInitialDefinitionEditorProfiles0181();
        var category = PayloadReader.GetString(context.Request.Payload, "category") ?? string.Empty;
        var search = PayloadReader.GetString(context.Request.Payload, "search") ?? string.Empty;
        var includeArchived = PayloadReader.GetBool(context.Request.Payload, "includeArchived");
        var filter = Builders<ContentDefinitionRecord>.Filter.Empty;
        if (!string.IsNullOrWhiteSpace(category))
            filter &= Builders<ContentDefinitionRecord>.Filter.Eq(x => x.Category, category);
        if (!includeArchived)
            filter &= Builders<ContentDefinitionRecord>.Filter.Ne(x => x.IsArchived, true);
        var records = _mongo.ContentDefinitionRecords.Find(filter).ToList();
        if (!string.IsNullOrWhiteSpace(search))
            records = records.Where(x => ContainsIgnoreCase0181(x.Name, search) || ContainsIgnoreCase0181(x.DisplayName, search) || ContainsIgnoreCase0181(x.ShortCode, search) || x.Tags.Any(t => ContainsIgnoreCase0181(t, search))).ToList();
        var profiles = BuildDefinitionProfileLookup0181();
        var items = records.OrderBy(x => x.Category, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(x => _contentDefinitionProjection0181.AdminRecordPayload(x, profiles.TryGetValue(x.Category, out var p) ? p : null, GetLatestValidation0181(x.Id), includeAuditSummary: false))
            .Cast<object>()
            .ToArray();
        return Ok("Content definitions loaded.", new Dictionary<string, object> { ["definitions"] = items, ["count"] = items.Length });
    }

    public ResponseEnvelope ContentDefinitionAdminGet(CommandContext context)
    {
        RequireAdmin(context);
        EnsureInitialDefinitionEditorProfiles0181();
        var record = GetContentDefinitionRecord0181(RequireDefinitionId0181(context.Request.Payload));
        var profile = FindDefinitionProfileByCategory0181(record.Category);
        var audit = _mongo.ContentDefinitionAuditEvents.Find(Builders<ContentDefinitionAuditEvent>.Filter.Eq(x => x.DefinitionId, record.Id)).SortByDescending(x => x.CreatedAtUtc).Limit(20).ToList()
            .Select(_contentDefinitionProjection0181.AuditPayload).Cast<object>().ToArray();
        return Ok("Content definition loaded.", new Dictionary<string, object>
        {
            ["definition"] = _contentDefinitionProjection0181.AdminRecordPayload(record, profile, GetLatestValidation0181(record.Id), includeAuditSummary: true),
            ["audit"] = audit
        });
    }

    public ResponseEnvelope ContentDefinitionAdminCreate(CommandContext context)
    {
        var actor = RequireAdmin(context);
        EnsureContentDefinitionEditorIndexes0181();
        EnsureInitialDefinitionEditorProfiles0181();
        var category = RequireLength(PayloadReader.GetString(context.Request.Payload, "category"), 1, 128, "category");
        var profile = FindDefinitionProfileByCategory0181(category) ?? throw new KeyNotFoundException("Definition editor profile not found.");
        if (!profile.CanCreate) throw new UnauthorizedAccessException("Profile does not allow create.");
        var record = BuildRecordFromPayload0181(context.Request.Payload, profile, null, actor.Id);
        ValidateDuplicateDefinitionId0181(record.Id);
        EnsureWorldLoreCalendarDefinitionCanPersist0185(record, profile);
        EnsureFactionOrganizationEconomyDefinitionCanPersist0186(record, profile);
        EnsureTechnologyRecipeBlueprintProjectDefinitionCanPersist0187(record, profile);
        _mongo.ContentDefinitionRecords.InsertOne(record);
        var validation = ValidateAndStoreContentDefinition0181(record, profile, actor.Id);
        WriteContentDefinitionAudit0181(actor, context.Request.Command, record.Id, profile.Id, record.Category, string.Empty, Summary0181(record), new[] { "definition" }, "definition created");
        PublishDefinitionSync0181("definitions.entry.created", "content_definition", record.Id, "created", actor.Id, context.Request.RequestId);
        return Ok("Content definition created.", new Dictionary<string, object>
        {
            ["definition"] = _contentDefinitionProjection0181.AdminRecordPayload(record, profile, validation, includeAuditSummary: false)
        });
    }

    public ResponseEnvelope ContentDefinitionAdminUpdate(CommandContext context)
    {
        var actor = RequireAdmin(context);
        EnsureInitialDefinitionEditorProfiles0181();
        var record = GetContentDefinitionRecord0181(RequireDefinitionId0181(context.Request.Payload));
        var expectedRevision = ReadOptionalInt0181(context.Request.Payload, "expectedRevision");
        if (expectedRevision.HasValue && expectedRevision.Value != record.Revision)
            throw new InvalidOperationException($"Definition was changed by another editor. Expected revision {expectedRevision.Value}, current revision {record.Revision}.");
        var profile = FindDefinitionProfileByCategory0181(record.Category) ?? throw new KeyNotFoundException("Definition editor profile not found.");
        if (!profile.CanEdit) throw new UnauthorizedAccessException("Profile does not allow edit.");
        var before = Summary0181(record);
        var previousRevision = record.Revision;
        record = BuildRecordFromPayload0181(context.Request.Payload, profile, record, actor.Id);
        record.Revision = previousRevision + 1;
        EnsureWorldLoreCalendarDefinitionCanPersist0185(record, profile);
        EnsureFactionOrganizationEconomyDefinitionCanPersist0186(record, profile);
        EnsureTechnologyRecipeBlueprintProjectDefinitionCanPersist0187(record, profile);
        var replaceFilter = Builders<ContentDefinitionRecord>.Filter.Eq(x => x.Id, record.Id)
                            & Builders<ContentDefinitionRecord>.Filter.Eq(x => x.Revision, previousRevision);
        var replace = _mongo.ContentDefinitionRecords.ReplaceOne(replaceFilter, record);
        if (replace.ModifiedCount != 1)
            throw new InvalidOperationException("Definition was changed by another editor. Refresh the record before saving.");
        var validation = ValidateAndStoreContentDefinition0181(record, profile, actor.Id);
        WriteContentDefinitionAudit0181(actor, context.Request.Command, record.Id, profile.Id, record.Category, before, Summary0181(record), ChangedFields0181(context.Request.Payload), "definition updated");
        PublishDefinitionSync0181("definitions.entry.updated", "content_definition", record.Id, "updated", actor.Id, context.Request.RequestId);
        return Ok("Content definition updated.", new Dictionary<string, object> { ["definition"] = _contentDefinitionProjection0181.AdminRecordPayload(record, profile, validation, includeAuditSummary: false) });
    }

    public ResponseEnvelope ContentDefinitionAdminClone(CommandContext context)
    {
        var actor = RequireAdmin(context);
        EnsureInitialDefinitionEditorProfiles0181();
        var source = GetContentDefinitionRecord0181(RequireDefinitionId0181(context.Request.Payload));
        var profile = FindDefinitionProfileByCategory0181(source.Category) ?? throw new KeyNotFoundException("Definition editor profile not found.");
        if (!profile.CanClone) throw new UnauthorizedAccessException("Profile does not allow clone.");
        var clone = CloneRecord0181(source, actor.Id);
        clone.Name = FirstNonEmpty0181(PayloadReader.GetString(context.Request.Payload, "name"), source.Name + "_copy");
        clone.DisplayName = FirstNonEmpty0181(PayloadReader.GetString(context.Request.Payload, "displayName"), source.DisplayName + " (копия)");
        clone.ShortCode = FirstNonEmpty0181(PayloadReader.GetString(context.Request.Payload, "shortCode"), source.ShortCode + "_copy");
        _mongo.ContentDefinitionRecords.InsertOne(clone);
        var validation = ValidateAndStoreContentDefinition0181(clone, profile, actor.Id);
        WriteContentDefinitionAudit0181(actor, context.Request.Command, clone.Id, profile.Id, clone.Category, Summary0181(source), Summary0181(clone), new[] { "clone" }, "definition cloned");
        PublishDefinitionSync0181("definitions.entry.cloned", "content_definition", clone.Id, "cloned", actor.Id, context.Request.RequestId);
        return Ok("Content definition cloned.", new Dictionary<string, object> { ["definition"] = _contentDefinitionProjection0181.AdminRecordPayload(clone, profile, validation, includeAuditSummary: false) });
    }

    public ResponseEnvelope ContentDefinitionAdminArchive(CommandContext context)
    {
        return SetDefinitionArchiveState0181(context, archive: true);
    }

    public ResponseEnvelope ContentDefinitionAdminRestore(CommandContext context)
    {
        return SetDefinitionArchiveState0181(context, archive: false);
    }

    public ResponseEnvelope ContentDefinitionAdminValidate(CommandContext context)
    {
        var actor = RequireAdmin(context);
        EnsureInitialDefinitionEditorProfiles0181();
        var record = GetContentDefinitionRecord0181(RequireDefinitionId0181(context.Request.Payload));
        var profile = FindDefinitionProfileByCategory0181(record.Category) ?? throw new KeyNotFoundException("Definition editor profile not found.");
        var validation = ValidateAndStoreContentDefinition0181(record, profile, actor.Id);
        WriteContentDefinitionAudit0181(actor, context.Request.Command, record.Id, profile.Id, record.Category, string.Empty, validation.Status, new[] { "validation" }, "validation run");
        PublishDefinitionSync0181("definitions.validation.updated", "content_definition_validation", validation.Id, "updated", actor.Id, context.Request.RequestId);
        return Ok("Content definition validated.", new Dictionary<string, object> { ["validation"] = _contentDefinitionProjection0181.ValidationPayload(validation) });
    }

    public ResponseEnvelope ContentDefinitionAdminPreviewAsPlayer(CommandContext context)
    {
        RequireAdmin(context);
        EnsureInitialDefinitionEditorProfiles0181();
        var record = GetContentDefinitionRecord0181(RequireDefinitionId0181(context.Request.Payload));
        if (WorldLoreCalendarDefinitionCategories.IsSupported(record.Category))
        {
            var lookup = _mongo.ContentDefinitionRecords.Find(Builders<ContentDefinitionRecord>.Filter.Empty)
                .ToList()
                .ToDictionary(x => x.Id, x => x, StringComparer.OrdinalIgnoreCase);
            return Ok("Player-safe world and lore preview built.", new Dictionary<string, object>
            {
                ["definition"] = WorldLore0185PlayerPayload(record, lookup)
            });
        }
        var profile = FindDefinitionProfileByCategory0181(record.Category);
        return Ok("Player-safe content definition preview built.", new Dictionary<string, object>
        {
            ["definition"] = _contentDefinitionProjection0181.PlayerDetailPayload(record, profile)
        });
    }

    public ResponseEnvelope ContentDefinitionAdminListAudit(CommandContext context)
    {
        RequireAdmin(context);
        var definitionId = PayloadReader.GetString(context.Request.Payload, "definitionId") ?? string.Empty;
        var profileId = PayloadReader.GetString(context.Request.Payload, "profileId") ?? string.Empty;
        var filter = Builders<ContentDefinitionAuditEvent>.Filter.Empty;
        if (!string.IsNullOrWhiteSpace(definitionId)) filter &= Builders<ContentDefinitionAuditEvent>.Filter.Eq(x => x.DefinitionId, definitionId);
        if (!string.IsNullOrWhiteSpace(profileId)) filter &= Builders<ContentDefinitionAuditEvent>.Filter.Eq(x => x.ProfileId, profileId);
        var items = _mongo.ContentDefinitionAuditEvents.Find(filter).SortByDescending(x => x.CreatedAtUtc).Limit(100).ToList()
            .Select(_contentDefinitionProjection0181.AuditPayload).Cast<object>().ToArray();
        return Ok("Content definition audit loaded.", new Dictionary<string, object> { ["audit"] = items, ["count"] = items.Length });
    }

    public ResponseEnvelope ContentDefinitionAdminFindReferences(CommandContext context)
    {
        RequireAdmin(context);
        var definitionId = RequireDefinitionId0181(context.Request.Payload);
        var items = _mongo.ContentDefinitionRecords.Find(Builders<ContentDefinitionRecord>.Filter.AnyEq(x => x.ReferenceIds, definitionId)).ToList()
            .Select(x => new Dictionary<string, object> { ["definitionId"] = x.Id, ["displayName"] = x.DisplayName, ["category"] = x.Category, ["isArchived"] = x.IsArchived })
            .Cast<object>().ToArray();
        return Ok("Content definition references loaded.", new Dictionary<string, object> { ["references"] = items, ["count"] = items.Length });
    }

    public ResponseEnvelope ContentDefinitionAdminSearchReferenceOptions(CommandContext context)
    {
        RequireAdmin(context);
        EnsureInitialDefinitionEditorProfiles0181();
        var search = PayloadReader.GetString(context.Request.Payload, "search") ?? string.Empty;
        var referenceCategory = PayloadReader.GetString(context.Request.Payload, "referenceCategory") ?? string.Empty;
        var ruleSetId = PayloadReader.GetString(context.Request.Payload, "ruleSetId") ?? string.Empty;
        var excludeDefinitionId = PayloadReader.GetString(context.Request.Payload, "excludeDefinitionId") ?? string.Empty;
        var includeArchived = PayloadReader.GetBool(context.Request.Payload, "includeArchived");
        var requestedLimit = ReadOptionalInt0181(context.Request.Payload, "limit") ?? 30;
        var limit = Math.Max(1, Math.Min(requestedLimit, 50));

        var filter = Builders<ContentDefinitionRecord>.Filter.Empty;
        if (!includeArchived)
            filter &= Builders<ContentDefinitionRecord>.Filter.Ne(x => x.IsArchived, true);
        if (!string.IsNullOrWhiteSpace(referenceCategory))
            filter &= Builders<ContentDefinitionRecord>.Filter.Eq(x => x.Category, referenceCategory);
        if (!string.IsNullOrWhiteSpace(ruleSetId))
            filter &= Builders<ContentDefinitionRecord>.Filter.Or(
                Builders<ContentDefinitionRecord>.Filter.Eq(x => x.RuleSetId, ruleSetId),
                Builders<ContentDefinitionRecord>.Filter.Eq(x => x.RuleSetId, string.Empty),
                Builders<ContentDefinitionRecord>.Filter.AnyEq(x => x.AllowedRuleSetIds, ruleSetId));
        if (!string.IsNullOrWhiteSpace(excludeDefinitionId))
            filter &= Builders<ContentDefinitionRecord>.Filter.Ne(x => x.Id, excludeDefinitionId);

        var records = _mongo.ContentDefinitionRecords.Find(filter).Limit(250).ToList();
        if (!string.IsNullOrWhiteSpace(search))
            records = records.Where(x => string.Equals(x.Id, search, StringComparison.OrdinalIgnoreCase)
                                      || ContainsIgnoreCase0181(x.DisplayName, search)
                                      || ContainsIgnoreCase0181(x.Name, search)
                                      || ContainsIgnoreCase0181(x.ShortCode, search)
                                      || x.Tags.Any(t => ContainsIgnoreCase0181(t, search))).ToList();
        var options = records
            .OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .Select(x => new Dictionary<string, object>
            {
                ["definitionId"] = x.Id,
                ["displayName"] = x.DisplayName,
                ["shortCode"] = x.ShortCode,
                ["category"] = x.Category,
                ["categoryLabel"] = FindDefinitionProfileByCategory0181(x.Category)?.DisplayName ?? x.Category,
                ["ruleSetId"] = x.RuleSetId,
                ["isArchived"] = x.IsArchived,
                ["visibilityRule"] = x.VisibilityRule
            })
            .Cast<object>()
            .ToArray();
        return Ok("Reference options loaded.", new Dictionary<string, object>
        {
            ["options"] = options,
            ["count"] = options.Length,
            ["limit"] = limit,
            ["searchMode"] = "server_bounded"
        });
    }

    public ResponseEnvelope ContentDefinitionAdminCheckBrokenReferences(CommandContext context)
    {
        RequireAdmin(context);
        EnsureInitialDefinitionEditorProfiles0181();
        var records = _mongo.ContentDefinitionRecords.Find(Builders<ContentDefinitionRecord>.Filter.Ne(x => x.IsArchived, true)).ToList();
        var ids = new HashSet<string>(records.Select(x => x.Id), StringComparer.OrdinalIgnoreCase);
        var broken = new List<object>();
        foreach (var record in records)
        {
            foreach (var reference in record.ReferenceIds.Where(r => !string.IsNullOrWhiteSpace(r) && !ids.Contains(r)))
                broken.Add(new Dictionary<string, object> { ["definitionId"] = record.Id, ["displayName"] = record.DisplayName, ["missingReferenceId"] = reference });
        }
        return Ok("Broken content definition references checked.", new Dictionary<string, object> { ["brokenReferences"] = broken.ToArray(), ["count"] = broken.Count });
    }

    public ResponseEnvelope ContentDefinitionAdminExportProfile(CommandContext context)
    {
        RequireAdmin(context);
        EnsureInitialDefinitionEditorProfiles0181();
        var profile = GetDefinitionProfile0181(RequireDefinitionProfileId0181(context.Request.Payload));
        var definitions = _mongo.ContentDefinitionRecords.Find(Builders<ContentDefinitionRecord>.Filter.Eq(x => x.Category, profile.Category)).ToList()
            .Select(x => _contentDefinitionProjection0181.AdminRecordPayload(x, profile, GetLatestValidation0181(x.Id), includeAuditSummary: false)).Cast<object>().ToArray();
        return Ok("Definition editor profile exported in-memory.", new Dictionary<string, object>
        {
            ["profile"] = _contentDefinitionProjection0181.ProfilePayload(profile, includeServerConfig: true),
            ["definitions"] = definitions,
            ["collectionNames"] = new object[] { "definition_editor_profiles", "content_definition_records", "content_definition_audit_events", "content_definition_validation_results" }
        });
    }

    public ResponseEnvelope ContentDefinitionAdminImportProfile(CommandContext context)
    {
        var actor = RequireSuperAdmin0181(context);
        EnsureContentDefinitionEditorIndexes0181();
        EnsureInitialDefinitionEditorProfiles0181();
        WriteContentDefinitionAudit0181(actor, context.Request.Command, string.Empty, string.Empty, "profile", string.Empty, "importProfile placeholder accepted", new[] { "importProfile" }, "0.18.1 MVP import profile command is controlled");
        return Ok("Definition editor profile import accepted as controlled 0.18.1 MVP placeholder.", new Dictionary<string, object>
        {
            ["imported"] = false,
            ["reason"] = "0.18.1 supports data portability through dataPortability.admin.exportDefinitions/importDefinitions; typed profile package import remains future work."
        });
    }

    public ResponseEnvelope ContentDefinitionPlayerListVisible(CommandContext context)
    {
        GetCurrentAccount(context);
        EnsureInitialDefinitionEditorProfiles0181();
        var category = PayloadReader.GetString(context.Request.Payload, "category") ?? string.Empty;
        var search = PayloadReader.GetString(context.Request.Payload, "search") ?? string.Empty;
        var filter = Builders<ContentDefinitionRecord>.Filter.Ne(x => x.IsArchived, true)
                     & Builders<ContentDefinitionRecord>.Filter.In(x => x.VisibilityRule, new[] { ContentDefinitionVisibilityRules.Public, ContentDefinitionVisibilityRules.PlayerVisible });
        if (!string.IsNullOrWhiteSpace(category))
            filter &= Builders<ContentDefinitionRecord>.Filter.Eq(x => x.Category, category);
        // Keep list/search/detail on the same authoritative player visibility policy.
        // The Mongo filter is only a coarse pre-filter; typed availability and hidden
        // definition rules are evaluated before anything is projected to PlayerClient.
        var records = _mongo.ContentDefinitionRecords.Find(filter).ToList()
            .Where(IsDefinitionPlayerVisible0181)
            .Where(x => !WorldLoreCalendarDefinitionCategories.IsSupported(x.Category))
            .ToList();
        var profiles = BuildDefinitionProfileLookup0181();
        if (!string.IsNullOrWhiteSpace(search))
            records = records.Where(x => MatchesPlayerVisibleSearch0181(x, profiles.TryGetValue(x.Category, out var profile) ? profile : null, search)).ToList();
        var items = records.OrderBy(x => x.Category, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(x => _contentDefinitionProjection0181.PlayerRecordPayload(x, profiles.TryGetValue(x.Category, out var p) ? p : null)).Cast<object>().ToArray();
        return Ok("Справочник загружен.", new Dictionary<string, object> { ["definitions"] = items, ["count"] = items.Length });
    }

    public ResponseEnvelope ContentDefinitionPlayerGetVisible(CommandContext context)
    {
        GetCurrentAccount(context);
        EnsureInitialDefinitionEditorProfiles0181();
        var record = GetContentDefinitionRecord0181(RequireDefinitionId0181(context.Request.Payload));
        if (record.IsArchived || !IsDefinitionPlayerVisible0181(record))
            throw new KeyNotFoundException("Visible content definition not found.");
        if (WorldLoreCalendarDefinitionCategories.IsSupported(record.Category))
        {
            var lookup = _mongo.ContentDefinitionRecords.Find(Builders<ContentDefinitionRecord>.Filter.Empty)
                .ToList()
                .ToDictionary(x => x.Id, x => x, StringComparer.OrdinalIgnoreCase);
            return Ok("Запись открыта.", new Dictionary<string, object>
            {
                ["definition"] = WorldLore0185PlayerPayload(record, lookup)
            });
        }
        var profile = FindDefinitionProfileByCategory0181(record.Category);
        var payload = _contentDefinitionProjection0181.PlayerDetailPayload(record, profile);
        return Ok("Запись открыта.", new Dictionary<string, object>
        {
            ["definition"] = payload
        });
    }

    public ResponseEnvelope ContentDefinitionPlayerSearchVisible(CommandContext context)
    {
        return ContentDefinitionPlayerListVisible(context);
    }

    private ResponseEnvelope SetDefinitionArchiveState0181(CommandContext context, bool archive)
    {
        var actor = RequireAdmin(context);
        EnsureInitialDefinitionEditorProfiles0181();
        var record = GetContentDefinitionRecord0181(RequireDefinitionId0181(context.Request.Payload));
        var profile = FindDefinitionProfileByCategory0181(record.Category) ?? throw new KeyNotFoundException("Definition editor profile not found.");
        if (!profile.CanArchive) throw new UnauthorizedAccessException("Profile does not allow archive/restore.");
        var before = record.IsArchived ? "archived" : "active";
        record.IsArchived = archive;
        record.Archived = archive;
        record.UpdatedAtUtc = DateTime.UtcNow;
        record.UpdatedUtc = DateTime.UtcNow;
        record.UpdatedByUserId = actor.Id;
        _mongo.ContentDefinitionRecords.ReplaceOne(Builders<ContentDefinitionRecord>.Filter.Eq(x => x.Id, record.Id), record);
        WriteContentDefinitionAudit0181(actor, context.Request.Command, record.Id, profile.Id, record.Category, before, archive ? "archived" : "active", new[] { "IsArchived" }, archive ? "definition archived" : "definition restored");
        PublishDefinitionSync0181(archive ? "definitions.entry.archived" : "definitions.entry.restored", "content_definition", record.Id, archive ? "archived" : "restored", actor.Id, context.Request.RequestId);
        return Ok(archive ? "Content definition archived." : "Content definition restored.", new Dictionary<string, object> { ["definitionId"] = record.Id, ["isArchived"] = record.IsArchived });
    }

    private void EnsureInitialDefinitionEditorProfiles0181()
    {
        EnsureContentDefinitionEditorIndexes0181();
        foreach (var profile in BuildInitialDefinitionEditorProfiles0181())
        {
            var exists = _mongo.DefinitionEditorProfiles.Find(Builders<DefinitionEditorProfile>.Filter.Eq(x => x.Id, profile.Id)).FirstOrDefault();
            if (exists == null)
            {
                _mongo.DefinitionEditorProfiles.InsertOne(profile);
                continue;
            }

            // Profiles are server-owned metadata. Refresh the generated schema so
            // localized labels added in code also reach an already initialized dev DB.
            profile.CreatedAtUtc = exists.CreatedAtUtc;
            profile.CreatedUtc = exists.CreatedUtc;
            profile.UpdatedAtUtc = DateTime.UtcNow;
            _mongo.DefinitionEditorProfiles.ReplaceOne(
                Builders<DefinitionEditorProfile>.Filter.Eq(x => x.Id, profile.Id),
                profile);
        }
    }

    private void EnsureContentDefinitionEditorIndexes0181()
    {
        // Indexes for the 0.18.1 definition editor collections are created by MongoContext.
        // Keeping endpoint-level CreateIndex calls here caused duplicate key-spec conflicts
        // when the repository layer had already initialized equivalent indexes.
    }

    private static List<DefinitionEditorProfile> BuildInitialDefinitionEditorProfiles0181()
    {
        var profiles = new List<DefinitionEditorProfile>
        {
            Profile0181("generic_item_definition", "item", "Предметы", "Generic item definitions.", new[]
            {
                Field0181("itemKind", "Тип предмета", ContentDefinitionFieldTypes.Enum, true, new[] { "tool", "weapon", "armor", "consumable", "material", "misc", "custom" }),
                Field0181("rarity", "Редкость", ContentDefinitionFieldTypes.Enum, true, new[] { "common", "uncommon", "rare", "epic", "legendary", "custom" }),
                Field0181("quality", "Качество", ContentDefinitionFieldTypes.Enum, false, new[] { "poor", "standard", "fine", "masterwork", "custom" }),
                Field0181("legalControl", "Правовой контроль", ContentDefinitionFieldTypes.Enum, false, new[] { "free", "licensed", "restricted", "forbidden", "custom" }),
                Field0181("weight", "Вес", ContentDefinitionFieldTypes.Decimal, false, min: 0, max: 100000),
                Field0181("stackable", "Складывается", ContentDefinitionFieldTypes.Boolean, false),
                Field0181("gmText", "Заметки GM", ContentDefinitionFieldTypes.LongText, false, isPlayerVisible: false, isGmOnly: true)
            }),
            Profile0181("generic_resource_definition", "resource", "Ресурсы", "Generic resource definitions.", new[]
            {
                Field0181("category", "Категория", ContentDefinitionFieldTypes.Enum, true, new[] { "ore", "plant", "chemical", "component", "energy", "food", "custom" }),
                Field0181("tier", "Уровень", ContentDefinitionFieldTypes.Integer, true, min: 0, max: 20),
                Field0181("rarity", "Редкость", ContentDefinitionFieldTypes.Enum, true, new[] { "common", "uncommon", "rare", "strategic", "custom" }),
                Field0181("storageRequirements", "Хранение", ContentDefinitionFieldTypes.LongText, false),
                Field0181("transportRequirements", "Транспортировка", ContentDefinitionFieldTypes.LongText, false),
                Field0181("legalStatus", "Правовой статус", ContentDefinitionFieldTypes.Enum, false, new[] { "free", "licensed", "restricted", "forbidden", "custom" })
            }),
            Profile0181("generic_language_definition", "language", "Языки", "Generic language definitions.", new[]
            {
                Field0181("languageFamily", "Семья языков", ContentDefinitionFieldTypes.String, true),
                Field0181("script", "Письменность", ContentDefinitionFieldTypes.String, false),
                Field0181("region", "Регион", ContentDefinitionFieldTypes.String, false),
                Field0181("gmNotes", "Заметки GM", ContentDefinitionFieldTypes.LongText, false, isPlayerVisible: false, isGmOnly: true)
            }),
            Profile0181("generic_law_definition", "law", "Законы", "Generic law definitions.", new[]
            {
                Field0181("jurisdiction", "Юрисдикция", ContentDefinitionFieldTypes.String, true),
                Field0181("controlLevel", "Уровень контроля", ContentDefinitionFieldTypes.Enum, true, new[] { "none", "low", "medium", "high", "absolute", "custom" }),
                Field0181("actionType", "Тип действия", ContentDefinitionFieldTypes.String, true),
                Field0181("publicExplanation", "Публичное объяснение", ContentDefinitionFieldTypes.LongText, false),
                Field0181("gmExplanation", "Объяснение GM", ContentDefinitionFieldTypes.LongText, false, isPlayerVisible: false, isGmOnly: true)
            }),
            Profile0181("generic_technology_definition", "technology", "Технологии", "Generic technology definitions.", new[]
            {
                Field0181("technologyTier", "Технологический уровень", ContentDefinitionFieldTypes.Integer, true, min: 0, max: 20),
                Field0181("region", "Регион", ContentDefinitionFieldTypes.String, false),
                Field0181("requiredKnowledge", "Требуемые знания", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: "knowledge"),
                Field0181("hiddenGmNotes", "Скрытые заметки GM", ContentDefinitionFieldTypes.LongText, false, isPlayerVisible: false, isGmOnly: true),
                Field0181("serverReviewToken", "Служебные данные", ContentDefinitionFieldTypes.String, false, isPlayerVisible: false, isServerOnly: true)
            }),
            Profile0181("generic_recipe_definition", "recipe", "Рецепты", "Generic recipe definitions.", new[]
            {
                Field0181("outputItem", "Выходной предмет", ContentDefinitionFieldTypes.Reference, true, referenceCategory: "item"),
                Field0181("requiredResources", "Требуемые ресурсы", ContentDefinitionFieldTypes.ReferenceList, true, referenceCategory: "resource"),
                Field0181("requiredTools", "Требуемые инструменты", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: "item"),
                Field0181("requiredKnowledge", "Требуемые знания", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: "knowledge"),
                Field0181("recipeVisibility", "Видимость рецепта", ContentDefinitionFieldTypes.VisibilityRule, false)
            }),
            Profile0181("generic_blueprint_definition", "blueprint", "Чертежи", "Generic blueprint definitions.", new[]
            {
                Field0181("targetType", "Тип цели", ContentDefinitionFieldTypes.Enum, true, new[] { "item", "vehicle", "building", "asset", "custom" }),
                Field0181("requiredKnowledge", "Требуемые знания", ContentDefinitionFieldTypes.ReferenceList, false, referenceCategory: "knowledge"),
                Field0181("requiredFacility", "Требуемая инфраструктура", ContentDefinitionFieldTypes.Reference, false, referenceCategory: "facility"),
                Field0181("hiddenDefects", "Скрытые дефекты", ContentDefinitionFieldTypes.LongText, false, isPlayerVisible: false, isGmOnly: true)
            })
        };
        profiles.AddRange(BuildCharacterDefinitionEditorProfiles0182());
        profiles.AddRange(BuildWorldLoreCalendarDefinitionEditorProfiles0185());
        profiles.AddRange(BuildFactionOrganizationEconomyDefinitionEditorProfiles0186());
        profiles.AddRange(BuildTechnologyRecipeBlueprintProjectDefinitionEditorProfiles0187());
        profiles.AddRange(BuildWeatherEnvironmentTravelDefinitionEditorProfiles0217());
        return profiles;
    }

    private static DefinitionEditorProfile Profile0181(string id, string category, string displayName, string description, IEnumerable<DefinitionFieldSchema> fields)
    {
        var profile = new DefinitionEditorProfile
        {
            Id = id,
            Category = category,
            DisplayName = displayName,
            Description = description,
            StorageMode = ContentDefinitionStorageModes.GenericContentDefinitionRecord,
            BackingCollectionName = "content_definition_records",
            DefaultVisibilityRule = ContentDefinitionVisibilityRules.PlayerVisible,
            DefaultTags = new List<string> { "foundation_0_18_1", category },
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            SchemaVersion = 1,
            ValidationRules = new List<string> { "required-fields", "enum-values", "numeric-min-max", "references" }
        };
        var order = 10;
        foreach (var field in fields)
        {
            field.DisplayOrder = order;
            order += 10;
            profile.FieldSchemas.Add(field);
            field.OptionLabels = new Dictionary<string, string>(field.LocalizedValueLabels, StringComparer.OrdinalIgnoreCase);
            if (field.FieldType == ContentDefinitionFieldTypes.Reference || field.FieldType == ContentDefinitionFieldTypes.ReferenceList)
                profile.ReferenceRules.Add(new DefinitionReferenceRule { FieldName = field.FieldName, ReferenceCategory = field.ReferenceCategory, IsRequired = field.IsRequired });
        }
        return profile;
    }

    private static DefinitionFieldSchema Field0181(string name, string displayName, string type, bool required, IEnumerable<string>? values = null, decimal? min = null, decimal? max = null, bool isPlayerVisible = true, bool isGmOnly = false, bool isServerOnly = false, string referenceCategory = "")
        => new DefinitionFieldSchema
        {
            FieldName = name,
            DisplayName = displayName,
            FieldType = type,
            IsRequired = required,
            AllowedValues = values?.ToList() ?? new List<string>(),
            MinValue = min,
            MaxValue = max,
            Minimum = min,
            Maximum = max,
            Step = type == ContentDefinitionFieldTypes.Decimal ? 0.1m : 1m,
            IsPlayerVisible = isPlayerVisible,
            IsGmOnly = isGmOnly,
            IsServerOnly = isServerOnly,
            ReferenceCategory = referenceCategory,
            ReferenceSelectionMode = type == ContentDefinitionFieldTypes.ReferenceList ? "multiple" : type == ContentDefinitionFieldTypes.Reference ? "single" : string.Empty,
            HelpText = required ? "Обязательное поле." : "Поле можно оставить пустым.",
            SectionTitle = isServerOnly ? "Технические сведения" : isGmOnly ? "Сведения мастера" : "Правила и свойства",
            EditorKind = DefinitionEditorKind0181(type),
            IsAdvanced = isServerOnly,
            AllowEmpty = !required,
            SupportsUnknownLegacyValue = true,
            UnknownValuePolicy = "PreserveAndWarn",
            LocalizedValueLabels = (values ?? Array.Empty<string>())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x, LocalizedDefinitionValue0181, StringComparer.OrdinalIgnoreCase)
        };

    private static string DefinitionEditorKind0181(string fieldType)
    {
        if (string.Equals(fieldType, ContentDefinitionFieldTypes.LongText, StringComparison.OrdinalIgnoreCase)) return "multiline_text";
        if (string.Equals(fieldType, ContentDefinitionFieldTypes.Boolean, StringComparison.OrdinalIgnoreCase)) return "toggle";
        if (string.Equals(fieldType, ContentDefinitionFieldTypes.Enum, StringComparison.OrdinalIgnoreCase) || string.Equals(fieldType, ContentDefinitionFieldTypes.VisibilityRule, StringComparison.OrdinalIgnoreCase)) return "select";
        if (string.Equals(fieldType, ContentDefinitionFieldTypes.Reference, StringComparison.OrdinalIgnoreCase)) return "reference_picker";
        if (string.Equals(fieldType, ContentDefinitionFieldTypes.ReferenceList, StringComparison.OrdinalIgnoreCase)) return "reference_picker_multiple";
        if (string.Equals(fieldType, ContentDefinitionFieldTypes.Integer, StringComparison.OrdinalIgnoreCase)) return "integer";
        if (string.Equals(fieldType, ContentDefinitionFieldTypes.Decimal, StringComparison.OrdinalIgnoreCase)) return "decimal";
        return "text";
    }

    private static string LocalizedDefinitionValue0181(string value)
    {
        // Keep the editor's newly introduced semantic values localized even
        // when an older generated profile still contains legacy label data.
        var semanticLabels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Playable"] = "Играбельная",
            ["PlayableWithCampaignPermission"] = "С разрешения мастера",
            ["GMOnly"] = "Только мастер",
            ["NPCOnly"] = "Только NPC",
            ["MonsterOnly"] = "Только противники",
            ["WildOnly"] = "Только дикие",
            ["Hidden"] = "Скрыта",
            ["Archived"] = "В архиве",
            ["Normal"] = "Обычная",
            ["Medium"] = "Средняя",
            ["Hard"] = "Высокая",
            ["Extreme"] = "Экстремальная",
            ["Physical"] = "Физические",
            ["Dexterity"] = "Ловкость",
            ["Endurance"] = "Выносливость",
            ["Knowledge"] = "Знания",
            ["Technical"] = "Технические",
            ["Field"] = "Полевые",
            ["Military"] = "Военные",
            ["Social"] = "Социальные",
            ["Vehicle/Control"] = "Техника и управление",
            ["Magic"] = "Магия",
            ["Custom"] = "Другое"
        };
        if (semanticLabels.TryGetValue(value, out var semanticLabel)) return semanticLabel;

        var labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["common"] = "Обычное", ["uncommon"] = "Необычное", ["rare"] = "Редкое", ["epic"] = "Эпическое", ["legendary"] = "Легендарное",
            ["poor"] = "Низкое", ["standard"] = "Стандартное", ["fine"] = "Хорошее", ["masterwork"] = "Мастерское",
            ["free"] = "Свободно", ["licensed"] = "По лицензии", ["restricted"] = "Ограничено", ["forbidden"] = "Запрещено",
            ["none"] = "Нет", ["low"] = "Низкий", ["medium"] = "Средний", ["high"] = "Высокий", ["absolute"] = "Абсолютный",
            ["custom"] = "Другое",
            ["Playable"] = "Играбельная",
            ["PlayableWithCampaignPermission"] = "С разрешения мастера",
            ["GMOnly"] = "Только мастер",
            ["NPCOnly"] = "Только NPC",
            ["MonsterOnly"] = "Только противники",
            ["WildOnly"] = "Только дикие",
            ["Hidden"] = "Скрыта",
            ["Archived"] = "В архиве",
            ["Normal"] = "Обычная",
            ["Medium"] = "Средняя",
            ["Hard"] = "Высокая",
            ["Extreme"] = "Экстремальная",
            ["Physical"] = "Физические",
            ["Dexterity"] = "Ловкость",
            ["Endurance"] = "Выносливость",
            ["Knowledge"] = "Знания",
            ["Technical"] = "Технические",
            ["Field"] = "Полевые",
            ["Military"] = "Военные",
            ["Social"] = "Социальные",
            ["Vehicle/Control"] = "Техника и управление",
            ["Magic"] = "Магия",
            ["world"] = "Мир",
            ["continent"] = "Материк",
            ["region"] = "Регион",
            ["state"] = "Государство",
            ["settlement"] = "Поселение",
            ["district"] = "Район",
            ["location"] = "Локация",
            ["sub_location"] = "Вложенная локация",
            ["lore"] = "Знание о мире",
            ["rumor"] = "Слух",
            ["document"] = "Документ",
            ["doctrine"] = "Доктрина",
            ["method"] = "Метод",
            ["historical_account"] = "Историческая запись",
            ["language"] = "Язык",
            ["era"] = "Эпоха",
            ["event_type"] = "Тип события",
            ["person"] = "Персона",
            ["faction"] = "Фракция",
            ["organization"] = "Организация",
            ["era_based"] = "По эпохам",
            ["continuous"] = "Непрерывная",
            ["has_year_zero"] = "С нулевым годом",
            ["no_year_zero"] = "Без нулевого года",
            ["forward"] = "Вперёд",
            ["backward"] = "Назад",
            ["minor"] = "Незначительная",
            ["major"] = "Значительная",
            ["critical"] = "Критическая",
            ["moderate"] = "Умеренная",
            ["severe"] = "Опасная",
            ["extreme"] = "Экстремальная",
            ["light_rain"] = "Небольшой дождь",
            ["moderate_rain"] = "Умеренный дождь",
            ["heavy_rain"] = "Сильный дождь",
            ["snow"] = "Снег",
            ["hail"] = "Град",
            ["scope"] = "Текущая область",
            ["campaign"] = "Кампания",
            ["air"] = "Воздух",
            ["water"] = "Вода",
            ["vacuum"] = "Вакуум",
            ["land"] = "Суша",
            ["space"] = "Космос",
            ["presentation_only"] = "Только отображение",
            ["deterministic_modifier"] = "Явная числовая поправка",
            ["fate_layer"] = "Слой Fate",
            ["runtime_effect"] = "Эффект состояния",
            ["travel"] = "Путешествие",
            ["multiple_explicit"] = "Несколько явно выбранных каналов",
            ["track_only"] = "Только отслеживать",
            ["suggest_check"] = "Предложить проверку",
            ["requires_gm_approval"] = "Требует решения мастера",
            ["auto_apply_preauthorized"] = "Применять по заранее утверждённому правилу",
            ["blocked"] = "Запрещено",
            ["exact"] = "Точный",
            ["approximate"] = "Приблизительный",
            ["qualitative"] = "Качественное описание",
            ["safe_auto"] = "Безопасно автоматически",
            ["confirmation"] = "С подтверждением",
            ["gm_approval"] = "После решения мастера",
            ["gm_only"] = "Только мастер",
            ["physical_currency"] = "Физическая",
            ["digital_currency"] = "Цифровая",
            ["commodity_currency"] = "Товарная"
        };
        return labels.TryGetValue(value, out var label) ? label : value.Replace('_', ' ');
    }

    private DefinitionEditorProfile BuildProfileFromPayload0181(IDictionary<string, object> payload, DefinitionEditorProfile? existing)
    {
        var source = existing ?? new DefinitionEditorProfile();
        source.Id = FirstNonEmpty0181(PayloadReader.GetString(payload, "profileId"), PayloadReader.GetString(payload, "id"), source.Id);
        source.WorldId = FirstNonEmpty0181(PayloadReader.GetString(payload, "worldId"), source.WorldId);
        source.RuleSetId = FirstNonEmpty0181(PayloadReader.GetString(payload, "ruleSetId"), source.RuleSetId);
        source.Category = RequireLength(FirstNonEmpty0181(PayloadReader.GetString(payload, "category"), source.Category), 1, 128, "category");
        source.DisplayName = RequireLength(FirstNonEmpty0181(PayloadReader.GetString(payload, "displayName"), source.DisplayName), 1, 128, "displayName");
        source.Description = FirstNonEmpty0181(PayloadReader.GetString(payload, "description"), source.Description);
        source.StorageMode = FirstNonEmpty0181(PayloadReader.GetString(payload, "storageMode"), source.StorageMode);
        source.BackingCollectionName = FirstNonEmpty0181(PayloadReader.GetString(payload, "backingCollectionName"), source.BackingCollectionName);
        source.DefaultVisibilityRule = FirstNonEmpty0181(PayloadReader.GetString(payload, "defaultVisibilityRule"), source.DefaultVisibilityRule);
        source.CanCreate = payload.ContainsKey("canCreate") ? PayloadReader.GetBool(payload, "canCreate") : source.CanCreate;
        source.CanEdit = payload.ContainsKey("canEdit") ? PayloadReader.GetBool(payload, "canEdit") : source.CanEdit;
        source.CanArchive = payload.ContainsKey("canArchive") ? PayloadReader.GetBool(payload, "canArchive") : source.CanArchive;
        source.CanClone = payload.ContainsKey("canClone") ? PayloadReader.GetBool(payload, "canClone") : source.CanClone;
        source.CanPreviewAsPlayer = payload.ContainsKey("canPreviewAsPlayer") ? PayloadReader.GetBool(payload, "canPreviewAsPlayer") : source.CanPreviewAsPlayer;
        return source;
    }

    private ContentDefinitionRecord BuildRecordFromPayload0181(IDictionary<string, object> payload, DefinitionEditorProfile profile, ContentDefinitionRecord? existing, string actorUserId)
    {
        var record = existing ?? new ContentDefinitionRecord();
        var now = DateTime.UtcNow;
        if (existing == null)
        {
            record.Id = FirstNonEmpty0181(PayloadReader.GetString(payload, "definitionId"), PayloadReader.GetString(payload, "id"), Guid.NewGuid().ToString("N"));
            record.CreatedAtUtc = now;
            record.CreatedUtc = now;
            record.CreatedByUserId = actorUserId;
        }
        record.WorldId = FirstNonEmpty0181(PayloadReader.GetString(payload, "worldId"), record.WorldId);
        record.CampaignId = FirstNonEmpty0181(PayloadReader.GetString(payload, "campaignId"), record.CampaignId);
        record.RuleSetId = FirstNonEmpty0181(PayloadReader.GetString(payload, "ruleSetId"), record.RuleSetId);
        record.Category = profile.Category;
        record.DefinitionType = FirstNonEmpty0181(PayloadReader.GetString(payload, "definitionType"), record.DefinitionType, profile.Category);
        record.Name = RequireLength(FirstNonEmpty0181(PayloadReader.GetString(payload, "name"), record.Name), 1, 128, "name");
        record.DisplayName = RequireLength(FirstNonEmpty0181(PayloadReader.GetString(payload, "displayName"), record.DisplayName, record.Name), 1, 128, "displayName");
        record.ShortCode = FirstNonEmpty0181(PayloadReader.GetString(payload, "shortCode"), record.ShortCode, Slug0181(record.Name));
        record.PublicDescription = FirstNonEmpty0181(PayloadReader.GetString(payload, "publicDescription"), record.PublicDescription);
        record.GMDescription = FirstNonEmpty0181(PayloadReader.GetString(payload, "gmDescription"), PayloadReader.GetString(payload, "GMDescription"), record.GMDescription);
        record.VisibilityRule = FirstNonEmpty0181(PayloadReader.GetString(payload, "visibilityRule"), record.VisibilityRule, profile.DefaultVisibilityRule);
        record.SourceDocument = FirstNonEmpty0181(PayloadReader.GetString(payload, "sourceDocument"), record.SourceDocument);
        record.SourceVersion = FirstNonEmpty0181(PayloadReader.GetString(payload, "sourceVersion"), record.SourceVersion);
        record.CalculationVersion = FirstNonEmpty0181(PayloadReader.GetString(payload, "calculationVersion"), record.CalculationVersion);
        record.MigrationPolicy = FirstNonEmpty0181(PayloadReader.GetString(payload, "migrationPolicy"), record.MigrationPolicy);
        var existingSystemTags = record.Tags.Where(IsSystemTag).ToList();
        var requestedSystemTags = ReadStringList0181(payload, "systemTags", existingSystemTags).Where(IsSystemTag);
        var requestedPublicTags = ReadStringList0181(payload, "tags", record.Tags).Where(IsPlayerSafeTag);
        record.Tags = MergeTags0181(profile.DefaultTags.Concat(existingSystemTags).Concat(requestedSystemTags), requestedPublicTags);
        record.ReferenceIds = ReadStringList0181(payload, "referenceIds", record.ReferenceIds);
        record.AllowedRuleSetIds = ReadStringList0181(payload, "allowedRuleSetIds", record.AllowedRuleSetIds);
        record.ForbiddenRuleSetIds = ReadStringList0181(payload, "forbiddenRuleSetIds", record.ForbiddenRuleSetIds);
        record.RequiredModules = ReadStringList0181(payload, "requiredModules", record.RequiredModules);
        record.CompatibilityTags = ReadStringList0181(payload, "compatibilityTags", record.CompatibilityTags);
        var incomingCustomFields = PayloadReader.GetDictionary(payload, "customFields") ?? new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var schema in profile.FieldSchemas)
        {
            if (payload.TryGetValue("customField_" + schema.FieldName, out var prefixedValue) && prefixedValue != null)
                incomingCustomFields[schema.FieldName] = prefixedValue;
        }
        record.CustomFields = MergeCustomFields0181(record.CustomFields, incomingCustomFields);
        record.ServerOnlyData = MergeCustomFields0181(record.ServerOnlyData, PayloadReader.GetDictionary(payload, "serverOnlyData"));
        record.UpdatedAtUtc = now;
        record.UpdatedUtc = now;
        record.UpdatedByUserId = actorUserId;
        return record;
    }

    private static bool IsPlayerSafeTag(string tag)
        => !tag.StartsWith("gm:", StringComparison.OrdinalIgnoreCase)
           && !tag.StartsWith("server:", StringComparison.OrdinalIgnoreCase)
           && !tag.StartsWith("hidden:", StringComparison.OrdinalIgnoreCase)
           && !tag.StartsWith("foundation_", StringComparison.OrdinalIgnoreCase)
           && !tag.StartsWith("dev", StringComparison.OrdinalIgnoreCase)
           && !tag.StartsWith("test", StringComparison.OrdinalIgnoreCase)
           && !tag.Equals("character_foundation", StringComparison.OrdinalIgnoreCase)
           && !tag.StartsWith("acceptance", StringComparison.OrdinalIgnoreCase)
           && tag.IndexOf("0182", StringComparison.OrdinalIgnoreCase) < 0
           && !tag.EndsWith("_definition", StringComparison.OrdinalIgnoreCase);

    private static bool IsSystemTag(string tag) => !IsPlayerSafeTag(tag ?? string.Empty);

    private ContentDefinitionValidationResult ValidateAndStoreContentDefinition0181(ContentDefinitionRecord record, DefinitionEditorProfile profile, string actorUserId)
    {
        var result = ValidateContentDefinition0181(record, profile);
        var previous = GetLatestValidation0181(record.Id);
        if (previous != null)
        {
            result.Id = previous.Id;
            result.CreatedUtc = previous.CreatedUtc;
        }
        _mongo.ContentDefinitionValidationResults.ReplaceOne(Builders<ContentDefinitionValidationResult>.Filter.Eq(x => x.Id, result.Id), result, new ReplaceOptions { IsUpsert = true });
        return result;
    }

    private ContentDefinitionValidationResult ValidateContentDefinition0181(ContentDefinitionRecord record, DefinitionEditorProfile profile)
    {
        var result = new ContentDefinitionValidationResult
        {
            DefinitionId = record.Id,
            ProfileId = profile.Id,
            ValidatedAtUtc = DateTime.UtcNow,
            SchemaVersion = profile.SchemaVersion
        };

        if (string.IsNullOrWhiteSpace(record.Name)) result.Errors.Add("Name is required.");
        if (string.IsNullOrWhiteSpace(record.DisplayName)) result.Errors.Add("DisplayName is required.");
        if (record.SchemaVersion < profile.SchemaVersion) result.SchemaWarnings.Add("Definition schema version is older than profile schema version.");
        if (record.ServerOnlyData.Count > 0 && record.VisibilityRule == ContentDefinitionVisibilityRules.PlayerVisible)
            result.VisibilityWarnings.Add("ServerOnlyData exists and must be stripped from player projection.");

        foreach (var schema in profile.FieldSchemas)
        {
            record.CustomFields.TryGetValue(schema.FieldName, out var value);
            var text = Convert.ToString(value) ?? string.Empty;
            if (schema.IsRequired && string.IsNullOrWhiteSpace(text))
                result.Errors.Add($"Required field '{schema.DisplayName}' is missing.");
            if (schema.AllowedValues.Count > 0 && !string.IsNullOrWhiteSpace(text) && !schema.AllowedValues.Contains(text, StringComparer.OrdinalIgnoreCase))
                result.Errors.Add($"Field '{schema.DisplayName}' has invalid value '{text}'.");
            if ((schema.FieldType == ContentDefinitionFieldTypes.Integer || schema.FieldType == ContentDefinitionFieldTypes.Decimal) && !string.IsNullOrWhiteSpace(text))
            {
                if (!decimal.TryParse(text, out var number))
                    result.Errors.Add($"Field '{schema.DisplayName}' must be numeric.");
                else
                {
                    if (schema.MinValue.HasValue && number < schema.MinValue.Value) result.Errors.Add($"Field '{schema.DisplayName}' is below min {schema.MinValue.Value}.");
                    if (schema.MaxValue.HasValue && number > schema.MaxValue.Value) result.Errors.Add($"Field '{schema.DisplayName}' is above max {schema.MaxValue.Value}.");
                }
            }
            if (!string.IsNullOrWhiteSpace(schema.ValidationRegex) && !Regex.IsMatch(text, schema.ValidationRegex))
                result.Errors.Add($"Field '{schema.DisplayName}' does not match validation regex.");
            if (schema.IsPlayerVisible && (schema.IsGmOnly || schema.IsServerOnly))
                result.VisibilityWarnings.Add($"Field '{schema.DisplayName}' is marked player-visible and GM/server-only; projection will keep it hidden.");
            if ((schema.FieldType == ContentDefinitionFieldTypes.Reference || schema.FieldType == ContentDefinitionFieldTypes.ReferenceList) && !string.IsNullOrWhiteSpace(text))
            {
                foreach (var referenceId in SplitRefs0181(text))
                    CheckReference0181(referenceId, schema, result);
            }
        }

        foreach (var referenceId in record.ReferenceIds)
            CheckReference0181(referenceId, null, result);

        ApplyCharacterDefinitionValidation0182(record, profile, result);
        ApplyWorldLoreCalendarDefinitionValidation0185(record, profile, result);
        ApplyFactionOrganizationEconomyDefinitionValidation0186(record, profile, result);
        ApplyTechnologyRecipeBlueprintProjectDefinitionValidation0187(record, profile, result);

        var duplicateShortCode = _mongo.ContentDefinitionRecords.Find(Builders<ContentDefinitionRecord>.Filter.Eq(x => x.Category, record.Category) & Builders<ContentDefinitionRecord>.Filter.Eq(x => x.ShortCode, record.ShortCode) & Builders<ContentDefinitionRecord>.Filter.Ne(x => x.Id, record.Id)).FirstOrDefault();
        if (duplicateShortCode != null)
            result.Warnings.Add($"Duplicate ShortCode '{record.ShortCode}' in category '{record.Category}'.");

        result.Status = result.Errors.Count > 0
            ? ContentDefinitionValidationStatuses.Invalid
            : (result.Warnings.Count + result.BrokenReferences.Count + result.VisibilityWarnings.Count + result.SchemaWarnings.Count > 0
                ? ContentDefinitionValidationStatuses.Warning
                : ContentDefinitionValidationStatuses.Valid);
        return result;
    }

    private void CheckReference0181(string referenceId, DefinitionFieldSchema? schema, ContentDefinitionValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(referenceId)) return;
        var referenced = _mongo.ContentDefinitionRecords.Find(Builders<ContentDefinitionRecord>.Filter.Eq(x => x.Id, referenceId)).FirstOrDefault()
                         ?? _mongo.ContentDefinitionRecords.Find(Builders<ContentDefinitionRecord>.Filter.Eq(x => x.ShortCode, referenceId)).FirstOrDefault();
        var unified = referenced == null
            ? _mongo.UnifiedDefinitions.Find(Builders<UnifiedDefinitionDocument>.Filter.Eq(x => x.Id, referenceId)).FirstOrDefault()
            : null;
        if (referenced == null && unified == null)
        {
            result.BrokenReferences.Add(referenceId);
            return;
        }
        var category = referenced?.Category ?? unified?.Category ?? string.Empty;
        var isArchived = referenced?.IsArchived == true || unified?.IsArchived == true;
        var visibility = referenced?.VisibilityRule ?? unified?.VisibilityRule ?? string.Empty;
        if (isArchived)
            result.Warnings.Add($"Reference '{referenceId}' points to archived definition.");
        if (schema != null
            && !string.IsNullOrWhiteSpace(schema.ReferenceCategory)
            && !string.Equals(schema.ReferenceCategory, category, StringComparison.OrdinalIgnoreCase)
            && !(schema.ReferenceTargetTypes ?? new List<string>()).Contains(category, StringComparer.OrdinalIgnoreCase))
            result.Errors.Add($"Field '{schema.DisplayName}' references incompatible category '{category}'.");
        if (schema != null
            && (schema.ReferenceTargetTypes ?? new List<string>()).Count > 0
            && !(schema.ReferenceTargetTypes ?? new List<string>()).Contains(category, StringComparer.OrdinalIgnoreCase))
            result.Errors.Add($"Field '{schema.DisplayName}' references unsupported category '{category}'.");
        var playerVisible = string.Equals(visibility, ContentDefinitionVisibilityRules.Public, StringComparison.OrdinalIgnoreCase)
                            || string.Equals(visibility, ContentDefinitionVisibilityRules.PlayerVisible, StringComparison.OrdinalIgnoreCase)
                            || string.Equals(visibility, VisibilityRuleIds.Public, StringComparison.OrdinalIgnoreCase)
                            || string.Equals(visibility, VisibilityRuleIds.PlayerVisible, StringComparison.OrdinalIgnoreCase);
        if (schema != null && schema.IsPlayerVisible && !playerVisible)
            result.VisibilityWarnings.Add($"Player-visible field '{schema.DisplayName}' references hidden/GM-only definition '{referenceId}'.");
    }

    private DefinitionEditorProfile GetDefinitionProfile0181(string id)
        => _mongo.DefinitionEditorProfiles.Find(Builders<DefinitionEditorProfile>.Filter.Eq(x => x.Id, id)).FirstOrDefault()
           ?? _mongo.DefinitionEditorProfiles.Find(Builders<DefinitionEditorProfile>.Filter.Eq(x => x.Category, id)).FirstOrDefault()
           ?? throw new KeyNotFoundException("Definition editor profile not found.");

    private DefinitionEditorProfile? FindDefinitionProfileByCategory0181(string category)
        => _mongo.DefinitionEditorProfiles.Find(Builders<DefinitionEditorProfile>.Filter.Eq(x => x.Category, category))
            .ToList()
            .OrderBy(x => x.IsArchived)
            .ThenByDescending(x => x.UpdatedAtUtc)
            .FirstOrDefault();

    private Dictionary<string, DefinitionEditorProfile> BuildDefinitionProfileLookup0181()
        => _mongo.DefinitionEditorProfiles.Find(FilterDefinition<DefinitionEditorProfile>.Empty)
            .ToList()
            .Where(x => !string.IsNullOrWhiteSpace(x.Category))
            .GroupBy(x => x.Category, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                x => x.Key,
                x => x.OrderBy(y => y.IsArchived).ThenByDescending(y => y.UpdatedAtUtc).First(),
                StringComparer.OrdinalIgnoreCase);

    private ContentDefinitionRecord GetContentDefinitionRecord0181(string id)
        => _mongo.ContentDefinitionRecords.Find(Builders<ContentDefinitionRecord>.Filter.Eq(x => x.Id, id)).FirstOrDefault()
           ?? _mongo.ContentDefinitionRecords.Find(Builders<ContentDefinitionRecord>.Filter.Eq(x => x.ShortCode, id)).FirstOrDefault()
           ?? throw new KeyNotFoundException("Content definition not found.");

    private ContentDefinitionValidationResult? GetLatestValidation0181(string definitionId)
        => _mongo.ContentDefinitionValidationResults.Find(Builders<ContentDefinitionValidationResult>.Filter.Eq(x => x.DefinitionId, definitionId)).SortByDescending(x => x.ValidatedAtUtc).FirstOrDefault();

    private UserAccount RequireSuperAdmin0181(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        RoleGuard.EnsureRole(actor, UserRole.SuperAdmin);
        return actor;
    }

    private void ValidateDuplicateDefinitionId0181(string definitionId)
    {
        if (_mongo.ContentDefinitionRecords.Find(Builders<ContentDefinitionRecord>.Filter.Eq(x => x.Id, definitionId)).Any())
            throw new InvalidOperationException("Definition id already exists.");
    }

    private void WriteContentDefinitionAudit0181(UserAccount actor, string command, string definitionId, string profileId, string category, string oldValue, string newValue, IEnumerable<string> changedFields, string reason)
    {
        _mongo.ContentDefinitionAuditEvents.InsertOne(new ContentDefinitionAuditEvent
        {
            ActorUserId = actor.Id,
            ActorRole = string.Join(",", actor.Roles.Select(x => x.ToString())),
            Command = command,
            DefinitionId = definitionId,
            ProfileId = profileId,
            Category = category,
            OldValueSummary = oldValue,
            NewValueSummary = newValue,
            ChangedFields = changedFields.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Reason = reason,
            CreatedAtUtc = DateTime.UtcNow
        });
    }

    private void PublishDefinitionSync0181(string eventType, string entityType, string entityId, string operation, string actorUserId, string? requestId)
    {
        TryPublishSyncEvent(eventType, "definitions", entityType, entityId, operation, actorUserId, new Dictionary<string, object>
        {
            ["entityId"] = entityId,
            ["operation"] = operation
        }, requestId ?? string.Empty);
    }

    private static string RequireDefinitionId0181(IDictionary<string, object> payload)
        => RequireLength(FirstNonEmpty0181(PayloadReader.GetString(payload, "definitionId"), PayloadReader.GetString(payload, "id")), 1, 128, "definitionId");

    private static string RequireDefinitionProfileId0181(IDictionary<string, object> payload)
        => RequireLength(FirstNonEmpty0181(PayloadReader.GetString(payload, "profileId"), PayloadReader.GetString(payload, "id"), PayloadReader.GetString(payload, "category")), 1, 128, "profileId");

    private static string Summary0181(ContentDefinitionRecord record)
        => $"{record.Category}:{record.ShortCode}:{record.DisplayName}:archived={record.IsArchived}";

    private static bool IsDefinitionPlayerVisible0181(ContentDefinitionRecord record)
    {
        if (record.IsArchived || record.Archived) return false;
        if (!string.Equals(record.VisibilityRule, ContentDefinitionVisibilityRules.Public, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(record.VisibilityRule, ContentDefinitionVisibilityRules.PlayerVisible, StringComparison.OrdinalIgnoreCase))
            return false;

        // Availability is a second, typed visibility boundary. It must be
        // enforced even when the coarse record visibility is player_visible.
        var availabilityEntry = record.CustomFields.FirstOrDefault(x => string.Equals(x.Key, "availabilityType", StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(availabilityEntry.Key))
        {
            var availability = Convert.ToString(availabilityEntry.Value) ?? string.Empty;
            if (availability.Equals("Hidden", StringComparison.OrdinalIgnoreCase)
                || availability.Equals("GMOnly", StringComparison.OrdinalIgnoreCase)
                || availability.Equals("MonsterOnly", StringComparison.OrdinalIgnoreCase)
                || availability.Equals("NPCOnly", StringComparison.OrdinalIgnoreCase)
                || availability.Equals("WildOnly", StringComparison.OrdinalIgnoreCase)
                || availability.Equals("Archived", StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return IsCharacterDefinitionPlayerVisible0182(record);
    }

    private static bool ContainsIgnoreCase0181(string? value, string search)
        => !string.IsNullOrWhiteSpace(value) && value.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;

    private static bool MatchesPlayerVisibleSearch0181(ContentDefinitionRecord record, DefinitionEditorProfile? profile, string search)
    {
        if (string.IsNullOrWhiteSpace(search)) return true;
        var candidates = new List<string>
        {
            record.DisplayName,
            record.PublicDescription
        };
        candidates.AddRange(record.Tags.Where(IsPlayerSafeTag).Select(x => x.Trim()));
        if (profile != null)
        {
            candidates.Add(profile.DisplayName);
            foreach (var schema in profile.FieldSchemas.Where(x => x.IsPlayerVisible && !x.IsGmOnly && !x.IsServerOnly))
            {
                candidates.Add(schema.DisplayName);
                if (record.CustomFields.TryGetValue(schema.FieldName, out var value))
                    candidates.AddRange(PlayerVisibleSearchValues0181(value));
            }
        }
        return candidates.Any(x => ContainsIgnoreCase0181(x, search));
    }

    private static IEnumerable<string> PlayerVisibleSearchValues0181(object? value)
    {
        if (value == null) yield break;
        if (value is string text)
        {
            if (!string.IsNullOrWhiteSpace(text)) yield return text;
            yield break;
        }
        if (value is IDictionary dictionary)
        {
            foreach (DictionaryEntry entry in dictionary)
            {
                var key = Convert.ToString(entry.Key) ?? string.Empty;
                if (key.StartsWith("gm", StringComparison.OrdinalIgnoreCase) || key.StartsWith("server", StringComparison.OrdinalIgnoreCase)) continue;
                foreach (var nested in PlayerVisibleSearchValues0181(entry.Value)) yield return nested;
            }
            yield break;
        }
        if (value is IEnumerable enumerable)
        {
            foreach (var item in enumerable)
                foreach (var nested in PlayerVisibleSearchValues0181(item)) yield return nested;
            yield break;
        }
        var converted = Convert.ToString(value);
        if (!string.IsNullOrWhiteSpace(converted)) yield return converted;
    }

    private static string FirstNonEmpty0181(params string?[] values)
        => values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;

    private static string Slug0181(string value)
    {
        var normalized = Regex.Replace(value.Trim().ToLowerInvariant(), "[^a-z0-9а-яё]+", "_");
        normalized = Regex.Replace(normalized, "_+", "_").Trim('_');
        return string.IsNullOrWhiteSpace(normalized) ? Guid.NewGuid().ToString("N").Substring(0, 8) : normalized;
    }

    private static List<string> ReadStringList0181(IDictionary<string, object> payload, string key, IEnumerable<string> fallback)
    {
        if (!payload.ContainsKey(key)) return fallback.ToList();
        var list = PayloadReader.GetList(payload, key);
        if (list != null)
            return list.Select(Convert.ToString).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var text = PayloadReader.GetString(payload, key) ?? string.Empty;
        return SplitRefs0181(text).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static IEnumerable<string> SplitRefs0181(string text)
        => (text ?? string.Empty).Split(new[] { ',', ';', '\n', '\r', '|' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(x => x.Length > 0);

    private static int? ReadOptionalInt0181(IDictionary<string, object> payload, string key)
    {
        if (!payload.TryGetValue(key, out var raw) || raw == null) return null;
        return int.TryParse(Convert.ToString(raw), out var value) ? value : null;
    }

    private static List<string> MergeTags0181(IEnumerable<string> defaults, IEnumerable<string> requested)
        => defaults.Concat(requested).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    private static Dictionary<string, object> MergeCustomFields0181(Dictionary<string, object> existing, Dictionary<string, object>? incoming)
    {
        var result = new Dictionary<string, object>(existing, StringComparer.Ordinal);
        if (incoming == null) return result;
        foreach (var item in incoming)
            result[item.Key] = NormalizeCustomFieldValue0181(item.Value);
        return result;
    }

    private static object NormalizeCustomFieldValue0181(object? value)
    {
        if (value == null) return string.Empty;
        if (value is string || value is bool || value is int || value is long || value is double || value is decimal) return value;
        if (value is IDictionary dictionary)
        {
            var result = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (DictionaryEntry entry in dictionary)
            {
                var key = Convert.ToString(entry.Key);
                if (string.IsNullOrWhiteSpace(key)) continue;
                result[key] = NormalizeCustomFieldValue0181(entry.Value);
            }
            return result;
        }
        if (value is IEnumerable enumerable && value is not string)
        {
            var items = new List<object>();
            foreach (var item in enumerable)
                items.Add(NormalizeCustomFieldValue0181(item));
            return items.ToArray();
        }
        return Convert.ToString(value) ?? string.Empty;
    }

    private static string[] ChangedFields0181(IDictionary<string, object> payload)
        => payload.Keys.Where(x => !string.Equals(x, "definitionId", StringComparison.OrdinalIgnoreCase) && !string.Equals(x, "id", StringComparison.OrdinalIgnoreCase)).ToArray();

    private static ContentDefinitionRecord CloneRecord0181(ContentDefinitionRecord source, string actorUserId)
    {
        return new ContentDefinitionRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            WorldId = source.WorldId,
            CampaignId = source.CampaignId,
            RuleSetId = source.RuleSetId,
            Category = source.Category,
            DefinitionType = source.DefinitionType,
            Name = source.Name,
            DisplayName = source.DisplayName,
            ShortCode = source.ShortCode,
            Tags = source.Tags.ToList(),
            PublicDescription = source.PublicDescription,
            GMDescription = source.GMDescription,
            ServerOnlyData = new Dictionary<string, object>(source.ServerOnlyData),
            VisibilityRule = source.VisibilityRule,
            AllowedRuleSetIds = source.AllowedRuleSetIds.ToList(),
            ForbiddenRuleSetIds = source.ForbiddenRuleSetIds.ToList(),
            RequiredModules = source.RequiredModules.ToList(),
            CompatibilityTags = source.CompatibilityTags.ToList(),
            SourceDocument = source.SourceDocument,
            SourceVersion = source.SourceVersion,
            CalculationVersion = source.CalculationVersion,
            MigrationPolicy = source.MigrationPolicy,
            CustomFields = new Dictionary<string, object>(source.CustomFields),
            ReferenceIds = source.ReferenceIds.ToList(),
            SchemaVersion = source.SchemaVersion,
            IsArchived = false,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = actorUserId,
            UpdatedByUserId = actorUserId
        };
    }
}
