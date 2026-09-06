using System;
using System.Collections.Generic;

namespace Nri.Shared.Domain;

public sealed class DefinitionPackManifest
{
    public string PackId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new List<string>();
    public List<DefinitionPackFile> Files { get; set; } = new List<DefinitionPackFile>();
    public int SchemaVersion { get; set; } = 1;
}

public static class DefinitionPackContentStatuses
{
    public const string ReferenceDemo = "ReferenceDemo";
}

public static class DefinitionPackConflictPolicies
{
    public const string PreserveUserChanges = "PreserveUserChanges";
}

public static class DefinitionPackRecordClassifications
{
    public const string Create = "Create";
    public const string AlreadyCurrent = "AlreadyCurrent";
    public const string SafeUpdate = "SafeUpdate";
    public const string UserModifiedConflict = "UserModifiedConflict";
    public const string MissingDependency = "MissingDependency";
    public const string InvalidReference = "InvalidReference";
    public const string ArchivedTarget = "ArchivedTarget";
    public const string IncompatibleSchema = "IncompatibleSchema";
}

public static class DefinitionPackStorageKinds
{
    public const string UnifiedDefinition = "UnifiedDefinition";
    public const string ContentDefinition = "ContentDefinition";
}

public sealed class ReferenceDefinitionPackManifest
{
    public string PackId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string SemanticVersion { get; set; } = string.Empty;
    public int SchemaVersion { get; set; } = 1;
    public string ContentStatus { get; set; } = DefinitionPackContentStatuses.ReferenceDemo;
    public string TargetRuleSet { get; set; } = string.Empty;
    public List<string> Dependencies { get; set; } = new List<string>();
    public List<string> SourceMilestones { get; set; } = new List<string>();
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public List<ReferenceDefinitionPackRecordDescriptor> Records { get; set; } = new List<ReferenceDefinitionPackRecordDescriptor>();
    public string StableKey { get; set; } = string.Empty;
    public string RecordVersion { get; set; } = string.Empty;
    public string ConflictPolicy { get; set; } = DefinitionPackConflictPolicies.PreserveUserChanges;
    public string Checksum { get; set; } = string.Empty;
    public string RecordsFile { get; set; } = "records.json";
}

public sealed class ReferenceDefinitionPackRecordDescriptor
{
    public string StableKey { get; set; } = string.Empty;
    public string RecordVersion { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string StorageKind { get; set; } = DefinitionPackStorageKinds.ContentDefinition;
    public string Checksum { get; set; } = string.Empty;
}

public sealed class ReferenceDefinitionPackRecord
{
    public string StableKey { get; set; } = string.Empty;
    public string RecordVersion { get; set; } = "1.0.0";
    public string Category { get; set; } = string.Empty;
    public string StorageKind { get; set; } = DefinitionPackStorageKinds.ContentDefinition;
    public string Name { get; set; } = string.Empty;
    public string PublicDescription { get; set; } = string.Empty;
    public string GMDescription { get; set; } = string.Empty;
    public string VisibilityRule { get; set; } = ContentDefinitionVisibilityRules.PlayerVisible;
    public List<string> Tags { get; set; } = new List<string>();
    public Dictionary<string, object> Fields { get; set; } = new Dictionary<string, object>();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new Dictionary<string, object>();
    public List<string> References { get; set; } = new List<string>();
    public string Checksum { get; set; } = string.Empty;
}

public sealed class DefinitionPackRecordPlan
{
    public string StableKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Classification { get; set; } = DefinitionPackRecordClassifications.Create;
    public string ExistingRecordId { get; set; } = string.Empty;
    public string PersistenceVersion { get; set; } = string.Empty;
    public int ReferenceCount { get; set; }
    public string ReferenceSummary { get; set; } = string.Empty;
    public List<string> Findings { get; set; } = new List<string>();
}

public sealed class ReferenceDefinitionPackPlan
{
    public string PackId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string SemanticVersion { get; set; } = string.Empty;
    public string ContentStatus { get; set; } = string.Empty;
    public string TargetRuleSet { get; set; } = string.Empty;
    public string ConflictPolicy { get; set; } = string.Empty;
    public string Checksum { get; set; } = string.Empty;
    public bool IsValid { get; set; }
    public bool IsDryRun { get; set; } = true;
    public bool Applied { get; set; }
    public int CreatedCount { get; set; }
    public int UpdatedCount { get; set; }
    public int SkippedCount { get; set; }
    public Dictionary<string, int> Counts { get; set; } = new Dictionary<string, int>();
    public List<DefinitionPackRecordPlan> Records { get; set; } = new List<DefinitionPackRecordPlan>();
    public List<string> Errors { get; set; } = new List<string>();
    public List<string> Warnings { get; set; } = new List<string>();
    public DateTime BuiltAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class DefinitionPackFile
{
    public string Category { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public bool Required { get; set; }
    public int ExpectedMinCount { get; set; }
    public string Notes { get; set; } = string.Empty;
}

public sealed class DefinitionPackLoadResult
{
    public string PackId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public List<string> LoadedFiles { get; set; } = new List<string>();
    public int LoadedDefinitions { get; set; }
    public List<string> Errors { get; set; } = new List<string>();
    public List<string> Warnings { get; set; } = new List<string>();
    public List<string> CrossReferenceErrors { get; set; } = new List<string>();
    public List<string> CrossReferenceWarnings { get; set; } = new List<string>();
    public List<DefinitionPackFileValidationResult> FileResults { get; set; } = new List<DefinitionPackFileValidationResult>();
    public DateTime LoadedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class DefinitionPackValidationResult
{
    public bool IsValid { get; set; }
    public int DefinitionCount { get; set; }
    public List<string> Errors { get; set; } = new List<string>();
    public List<string> Warnings { get; set; } = new List<string>();
    public List<string> CrossReferenceErrors { get; set; } = new List<string>();
    public List<string> CrossReferenceWarnings { get; set; } = new List<string>();
    public List<DefinitionPackFileValidationResult> FileResults { get; set; } = new List<DefinitionPackFileValidationResult>();
}

public sealed class DefinitionPackFileValidationResult
{
    public string Category { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public int DefinitionCount { get; set; }
    public List<string> Errors { get; set; } = new List<string>();
    public List<string> Warnings { get; set; } = new List<string>();
}

public sealed class DefinitionPackImportOptions
{
    public bool DryRun { get; set; } = true;
    public bool AllowOverwrite { get; set; }
    public bool IncludeArchived { get; set; }
    public bool ValidateOnly { get; set; }
    public string ActorUserId { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
}
