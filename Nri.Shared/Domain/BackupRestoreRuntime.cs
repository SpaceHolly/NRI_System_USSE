using System;
using System.Collections.Generic;

namespace Nri.Shared.Domain;

public static class BackupScopeIds
{
    public const string FullServer = "full_server";
    public const string CampaignExperimental = "campaign_experimental";
    public const string CustomFuture = "custom_future";
}

public static class BackupStatusIds
{
    public const string Creating = "creating";
    public const string Complete = "complete";
    public const string Failed = "failed";
    public const string Incomplete = "incomplete";
    public const string Verifying = "verifying";
    public const string Corrupted = "corrupted";
    public const string Archived = "archived";
}

public static class BackupVerificationStatusIds
{
    public const string NotVerified = "not_verified";
    public const string Valid = "valid";
    public const string InvalidChecksum = "invalid_checksum";
    public const string InvalidManifest = "invalid_manifest";
    public const string Incompatible = "incompatible";
    public const string MissingFile = "missing_file";
    public const string Failed = "failed";
}

public static class BackupRestoreOperationStatusIds
{
    public const string Previewed = "previewed";
    public const string Blocked = "blocked";
    public const string Running = "running";
    public const string Completed = "completed";
    public const string Failed = "failed";
}

public sealed class BackupRecordState : EntityBase
{
    public string BackupId { get; set; } = string.Empty;
    public string Scope { get; set; } = BackupScopeIds.FullServer;
    public string Status { get; set; } = BackupStatusIds.Creating;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CompletedAtUtc { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public string CreatedByDisplayName { get; set; } = string.Empty;
    public string ServerVersion { get; set; } = string.Empty;
    public string DatabaseVersion { get; set; } = string.Empty;
    public string EnvironmentName { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string RelativeStoragePath { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string ChecksumAlgorithm { get; set; } = "SHA256";
    public string Checksum { get; set; } = string.Empty;
    public int CollectionCount { get; set; }
    public long DocumentCount { get; set; }
    public bool IsVerified { get; set; }
    public DateTime VerifiedAtUtc { get; set; }
    public string VerificationStatus { get; set; } = BackupVerificationStatusIds.NotVerified;
    public string VerificationMessage { get; set; } = string.Empty;
    public bool IsPreRestoreSafetyBackup { get; set; }
    public string SourceRestoreOperationId { get; set; } = string.Empty;
    public bool IsArchived { get; set; }
    public List<string> Tags { get; set; } = new List<string>();
    public Dictionary<string, object> ExtraData { get; set; } = new Dictionary<string, object>();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new Dictionary<string, object>();
}

public sealed class BackupManifest
{
    public int ManifestVersion { get; set; } = 1;
    public string BackupId { get; set; } = string.Empty;
    public string Scope { get; set; } = BackupScopeIds.FullServer;
    public DateTime CreatedAtUtc { get; set; }
    public string ServerVersion { get; set; } = string.Empty;
    public string SchemaVersion { get; set; } = "1";
    public string EnvironmentName { get; set; } = string.Empty;
    public string DatabaseEngine { get; set; } = "MongoDB";
    public List<BackupManifestCollectionEntry> Collections { get; set; } = new List<BackupManifestCollectionEntry>();
    public List<string> IndexSummaries { get; set; } = new List<string>();
    public Dictionary<string, string> ModuleVersions { get; set; } = new Dictionary<string, string>();
    public Dictionary<string, string> FeatureModuleSummaries { get; set; } = new Dictionary<string, string>();
    public long DocumentCount { get; set; }
    public string ChecksumAlgorithm { get; set; } = "SHA256";
    public string ArchiveChecksum { get; set; } = string.Empty;
    public string BackupToolStrategy { get; set; } = "mongo_driver_json_snapshot";
    public List<string> Warnings { get; set; } = new List<string>();
    public string CompatibilityMinimumServerVersion { get; set; } = string.Empty;
    public string CompatibilityMaximumServerVersion { get; set; } = string.Empty;
}

public sealed class BackupManifestCollectionEntry
{
    public string CollectionName { get; set; } = string.Empty;
    public long DocumentCount { get; set; }
    public string SafeSchemaSummary { get; set; } = string.Empty;
    public string DataChecksum { get; set; } = string.Empty;
}

public sealed class BackupRestoreOperationState : EntityBase
{
    public string OperationId { get; set; } = string.Empty;
    public string BackupId { get; set; } = string.Empty;
    public string Status { get; set; } = BackupRestoreOperationStatusIds.Previewed;
    public string RequestedByUserId { get; set; } = string.Empty;
    public string RequestedByDisplayName { get; set; } = string.Empty;
    public DateTime RequestedAtUtc { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime CompletedAtUtc { get; set; }
    public string Reason { get; set; } = string.Empty;
    public bool MaintenanceModeRequired { get; set; } = true;
    public bool MaintenanceModeWasEnabled { get; set; }
    public bool PreRestoreBackupRequired { get; set; } = true;
    public string SafetyBackupId { get; set; } = string.Empty;
    public bool VerificationRequired { get; set; } = true;
    public bool VerificationPassed { get; set; }
    public bool HasBlockers { get; set; }
    public List<string> Blockers { get; set; } = new List<string>();
    public List<string> Warnings { get; set; } = new List<string>();
    public Dictionary<string, object> Summary { get; set; } = new Dictionary<string, object>();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new Dictionary<string, object>();
}

public sealed class BackupMaintenanceState : EntityBase
{
    public bool IsEnabled { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string UpdatedByUserId { get; set; } = string.Empty;
    public string UpdatedByDisplayName { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; }
}
