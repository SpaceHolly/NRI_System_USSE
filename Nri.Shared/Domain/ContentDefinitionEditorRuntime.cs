using System;
using System.Collections.Generic;

namespace Nri.Shared.Domain;

public static class ContentDefinitionStorageModes
{
    public const string GenericContentDefinitionRecord = "GenericContentDefinitionRecord";
    public const string ExistingCollectionAdapter = "ExistingCollectionAdapter";
    public const string ReadOnlyAdapter = "ReadOnlyAdapter";
}

public static class ContentDefinitionFieldTypes
{
    public const string String = "String";
    public const string LongText = "LongText";
    public const string Integer = "Integer";
    public const string Decimal = "Decimal";
    public const string Boolean = "Boolean";
    public const string Enum = "Enum";
    public const string Tags = "Tags";
    public const string Reference = "Reference";
    public const string ReferenceList = "ReferenceList";
    public const string JsonObject = "JsonObject";
    public const string VisibilityRule = "VisibilityRule";
    public const string LocalizedText = "LocalizedText";
    public const string Custom = "Custom";
}

public static class ContentDefinitionValidationStatuses
{
    public const string Valid = "Valid";
    public const string Warning = "Warning";
    public const string Invalid = "Invalid";
}

public static class ContentDefinitionVisibilityRules
{
    public const string Public = "public";
    public const string PlayerVisible = "player_visible";
    public const string GmOnly = "gm_only";
    public const string Hidden = "hidden";
}

public sealed class DefinitionEditorProfile : EntityBase
{
    public string WorldId { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string StorageMode { get; set; } = ContentDefinitionStorageModes.GenericContentDefinitionRecord;
    public string BackingCollectionName { get; set; } = "content_definition_records";
    public List<string> AllowedRoles { get; set; } = new List<string> { UserRole.Admin.ToString(), UserRole.SuperAdmin.ToString() };
    public bool CanCreate { get; set; } = true;
    public bool CanEdit { get; set; } = true;
    public bool CanArchive { get; set; } = true;
    public bool CanClone { get; set; } = true;
    public bool CanPreviewAsPlayer { get; set; } = true;
    public List<DefinitionFieldSchema> FieldSchemas { get; set; } = new List<DefinitionFieldSchema>();
    public List<string> RequiredBaseFields { get; set; } = new List<string> { "Name", "DisplayName" };
    public List<string> ValidationRules { get; set; } = new List<string>();
    public List<DefinitionReferenceRule> ReferenceRules { get; set; } = new List<DefinitionReferenceRule>();
    public string DefaultVisibilityRule { get; set; } = ContentDefinitionVisibilityRules.PlayerVisible;
    public List<string> DefaultTags { get; set; } = new List<string>();
    public bool IsArchived { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class DefinitionFieldSchema
{
    public string FieldName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ShortLabel { get; set; } = string.Empty;
    public string FieldType { get; set; } = ContentDefinitionFieldTypes.String;
    public bool IsRequired { get; set; }
    public bool IsPlayerVisible { get; set; } = true;
    public bool IsGmOnly { get; set; }
    public bool IsServerOnly { get; set; }
    public string SectionKey { get; set; } = string.Empty;
    public List<string> AllowedValues { get; set; } = new List<string>();
    public Dictionary<string, string> OptionLabels { get; set; } = new Dictionary<string, string>();
    public string ReferenceCategory { get; set; } = string.Empty;
    public List<string> ReferenceTargetTypes { get; set; } = new List<string>();
    public string ReferenceSelectionMode { get; set; } = string.Empty;
    public decimal? MinValue { get; set; }
    public decimal? MaxValue { get; set; }
    public decimal? Minimum { get; set; }
    public decimal? Maximum { get; set; }
    public decimal? Step { get; set; }
    public string UnitLabel { get; set; } = string.Empty;
    public string DefaultValue { get; set; } = string.Empty;
    public string HelpText { get; set; } = string.Empty;
    public string Placeholder { get; set; } = string.Empty;
    public string SectionTitle { get; set; } = "Правила и свойства";
    public string EditorKind { get; set; } = string.Empty;
    public bool AllowEmpty { get; set; } = true;
    public bool IsMultiline { get; set; }
    public bool IsAdvanced { get; set; }
    public bool IsReadOnly { get; set; }
    public bool IsSecret { get; set; }
    public bool IsSearchable { get; set; }
    public bool SupportsUnknownLegacyValue { get; set; } = true;
    public string UnknownValuePolicy { get; set; } = "PreserveAndWarn";
    public Dictionary<string, string> LocalizedValueLabels { get; set; } = new Dictionary<string, string>();
    public int DisplayOrder { get; set; }
    public string ValidationRegex { get; set; } = string.Empty;
    public int SchemaVersion { get; set; } = 1;
}

public sealed class DefinitionReferenceRule
{
    public string FieldName { get; set; } = string.Empty;
    public string ReferenceCategory { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public bool MustBePlayerVisibleWhenFieldIsPlayerVisible { get; set; } = true;
}

public sealed class ContentDefinitionRecord : EntityBase
{
    public string WorldId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string DefinitionType { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ShortCode { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new List<string>();
    public string PublicDescription { get; set; } = string.Empty;
    public string GMDescription { get; set; } = string.Empty;
    public Dictionary<string, object> ServerOnlyData { get; set; } = new Dictionary<string, object>();
    public string VisibilityRule { get; set; } = ContentDefinitionVisibilityRules.PlayerVisible;
    public List<string> AllowedRuleSetIds { get; set; } = new List<string>();
    public List<string> ForbiddenRuleSetIds { get; set; } = new List<string>();
    public List<string> RequiredModules { get; set; } = new List<string>();
    public List<string> CompatibilityTags { get; set; } = new List<string>();
    public string SourceDocument { get; set; } = string.Empty;
    public string SourceVersion { get; set; } = string.Empty;
    public string CalculationVersion { get; set; } = string.Empty;
    public string MigrationPolicy { get; set; } = string.Empty;
    public Dictionary<string, object> CustomFields { get; set; } = new Dictionary<string, object>();
    public List<string> ReferenceIds { get; set; } = new List<string>();
    public int Revision { get; set; } = 1;
    public bool IsArchived { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
    public string UpdatedByUserId { get; set; } = string.Empty;
    public string ContentStatus { get; set; } = string.Empty;
    public string DefinitionPackId { get; set; } = string.Empty;
    public string DefinitionPackVersion { get; set; } = string.Empty;
    public string StableKey { get; set; } = string.Empty;
    public string RecordVersion { get; set; } = string.Empty;
    public string PackRecordChecksum { get; set; } = string.Empty;
    public string PackAppliedContentChecksum { get; set; } = string.Empty;
    public DateTime? PackAppliedAtUtc { get; set; }
}

public sealed class ContentDefinitionAuditEvent : EntityBase
{
    public string ActorUserId { get; set; } = string.Empty;
    public string ActorRole { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public string DefinitionId { get; set; } = string.Empty;
    public string ProfileId { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string OldValueSummary { get; set; } = string.Empty;
    public string NewValueSummary { get; set; } = string.Empty;
    public List<string> ChangedFields { get; set; } = new List<string>();
    public string Reason { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class ContentDefinitionValidationResult : EntityBase
{
    public string DefinitionId { get; set; } = string.Empty;
    public string ProfileId { get; set; } = string.Empty;
    public string Status { get; set; } = ContentDefinitionValidationStatuses.Valid;
    public List<string> Errors { get; set; } = new List<string>();
    public List<string> Warnings { get; set; } = new List<string>();
    public List<string> BrokenReferences { get; set; } = new List<string>();
    public List<string> VisibilityWarnings { get; set; } = new List<string>();
    public List<string> SchemaWarnings { get; set; } = new List<string>();
    public DateTime ValidatedAtUtc { get; set; } = DateTime.UtcNow;
}
