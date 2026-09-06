using System;
using System.Collections.Generic;

namespace Nri.Shared.Domain;

public static class AssetMaintenanceRuntimeIds0198
{
    public const string RuntimeKind = "asset_maintenance_0198";
    public const string TemplateKey = "reference.asset_maintenance.building.v1";
}

public static class AssetOperationStatusIds0198
{
    public const string Inactive = "inactive";
    public const string Operational = "operational";
    public const string Restricted = "restricted";
    public const string Suspended = "suspended";
}

public static class AssetReadinessStatusIds0198
{
    public const string Ready = "ready";
    public const string ReadyWithWarnings = "ready_with_warnings";
    public const string Blocked = "blocked";
}

public static class AssetMaintenanceStatusIds0198
{
    public const string Current = "current";
    public const string Due = "due";
    public const string InMaintenance = "in_maintenance";
    public const string Overdue = "overdue";
}

public static class AssetMaintenanceRequirementKindIds0198
{
    public const string Personnel = "personnel";
    public const string FuelAndResources = "fuel_and_resources";
    public const string RepairAndService = "repair_and_service";
    public const string Storage = "storage";
    public const string Security = "security";
    public const string TaxesOrRent = "taxes_or_rent";
    public const string LicensesAndDocuments = "licenses_and_documents";
    public const string MagicOrAnomalyService = "magic_or_anomaly_service";
    public const string Interval = "interval";
}

public static class AssetRequirementResolutionKindIds0198
{
    public const string Reference = "reference";
    public const string Resource = "resource";
    public const string ManualGm = "manual_gm";
    public const string NotApplicable = "not_applicable";
}

public static class AssetMaintenanceReservationStatusIds0198
{
    public const string Reserved = "reserved";
    public const string PartiallyConsumed = "partially_consumed";
    public const string Consumed = "consumed";
    public const string Released = "released";
}

public sealed class AssetMaintenanceRequirementState0198
{
    public string RequirementKind { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ResolutionKind { get; set; } = AssetRequirementResolutionKindIds0198.ManualGm;
    public string ReferenceId { get; set; } = string.Empty;
    public string ReferenceDisplayName { get; set; } = string.Empty;
    public decimal RequiredQuantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string PublicStatus { get; set; } = ProjectRequirementStatusIds.Open;
    public string GMStatus { get; set; } = string.Empty;
    public string GMDetails { get; set; } = string.Empty;
    public bool IsBlocking { get; set; } = true;
    public bool IsPlayerVisible { get; set; } = true;
    public int Revision { get; set; } = 1;
}

public sealed class AssetOperationState0198 : EntityBase
{
    public string CampaignId { get; set; } = string.Empty;
    public string AssetId { get; set; } = string.Empty;
    public string OwnerKind { get; set; } = AssetConstructionRuntimeIds0197.OwnerKindCharacter;
    public string OwnerId { get; set; } = string.Empty;
    public string OwnerUserId { get; set; } = string.Empty;
    public string OperationStatus { get; set; } = AssetOperationStatusIds0198.Inactive;
    public string ReadinessStatus { get; set; } = AssetReadinessStatusIds0198.Blocked;
    public List<string> PublicBlockerSummaries { get; set; } = new();
    public List<string> GMBlockerSummaries { get; set; } = new();
    public List<string> ActivePersonnelReferences { get; set; } = new();
    public List<string> LicenseDocumentReferences { get; set; } = new();
    public List<AssetMaintenanceRequirementState0198> OperationalRequirementSnapshot { get; set; } = new();
    public string ActivationRequestOperationId { get; set; } = string.Empty;
    public DateTime? ActivationRequestedAtUtc { get; set; }
    public DateTime? ActivatedAtUtc { get; set; }
    public DateTime? RestrictedAtUtc { get; set; }
    public string LastOperationId { get; set; } = string.Empty;
    public string LastOperationCommand { get; set; } = string.Empty;
    public int Revision { get; set; } = 1;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string UpdatedByUserId { get; set; } = string.Empty;
}

public sealed class AssetMaintenanceSnapshot0198
{
    public string AssetId { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public string AssetKind { get; set; } = string.Empty;
    public string BlueprintStableKey { get; set; } = string.Empty;
    public int BlueprintRevision { get; set; }
    public string LocationId { get; set; } = string.Empty;
    public string LocationName { get; set; } = string.Empty;
    public string OwnerKind { get; set; } = string.Empty;
    public string OwnerId { get; set; } = string.Empty;
    public string OwnerDisplayName { get; set; } = string.Empty;
    public string PreviousOperationStatus { get; set; } = string.Empty;
    public string PreviousMaintenanceStatus { get; set; } = string.Empty;
    public string MaintenanceInterval { get; set; } = string.Empty;
    public string SpecialistReferenceId { get; set; } = string.Empty;
    public string SpecialistDisplayName { get; set; } = string.Empty;
    public List<string> LicenseDocumentReferences { get; set; } = new();
    public List<ProjectMaterialSnapshot0191> Materials { get; set; } = new();
    public List<ProjectRequirementSnapshot0191> Requirements { get; set; } = new();
    public List<ProjectStageSnapshot0191> Stages { get; set; } = new();
    public string ProjectTemplateKey { get; set; } = AssetMaintenanceRuntimeIds0198.TemplateKey;
    public string ProjectTemplateName { get; set; } = "Обслуживание крупного актива";
    public string RuleSetId { get; set; } = string.Empty;
    public string ExpectedResultSummary { get; set; } = string.Empty;
    public string SnapshotChecksum { get; set; } = string.Empty;
}

public sealed class AssetMaintenanceReservationState0198 : EntityBase
{
    public string ProjectId { get; set; } = string.Empty;
    public string AssetId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string OwnerCharacterId { get; set; } = string.Empty;
    public string ResourceDefinitionId { get; set; } = string.Empty;
    public string ResourceDisplayName { get; set; } = string.Empty;
    public string InventoryItemId { get; set; } = string.Empty;
    public decimal QuantityReserved { get; set; }
    public decimal QuantityConsumed { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string StageKey { get; set; } = string.Empty;
    public string Status { get; set; } = AssetMaintenanceReservationStatusIds0198.Reserved;
    public string OperationId { get; set; } = string.Empty;
    public DateTime? ConsumedAtUtc { get; set; }
    public DateTime? ReleasedAtUtc { get; set; }
    public int Revision { get; set; } = 1;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class AssetMaintenanceStageConsumptionState0198 : EntityBase
{
    public string ProjectId { get; set; } = string.Empty;
    public string AssetId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string StageKey { get; set; } = string.Empty;
    public string OperationId { get; set; } = string.Empty;
    public List<ProjectMaterialSnapshot0191> Resources { get; set; } = new();
    public DateTime ConsumedAtUtc { get; set; } = DateTime.UtcNow;
    public string ConsumedByUserId { get; set; } = string.Empty;
}

public sealed class MaintenanceServiceRecordState0198 : EntityBase
{
    public string CampaignId { get; set; } = string.Empty;
    public string AssetId { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string PreviousMaintenanceStatus { get; set; } = string.Empty;
    public string ResultingMaintenanceStatus { get; set; } = AssetMaintenanceStatusIds0198.Current;
    public string SpecialistReferenceId { get; set; } = string.Empty;
    public string SpecialistDisplayName { get; set; } = string.Empty;
    public List<ProjectMaterialSnapshot0191> ConsumedResources { get; set; } = new();
    public List<string> CompletedStages { get; set; } = new();
    public string PublicSummary { get; set; } = string.Empty;
    public string GMSummary { get; set; } = string.Empty;
    public DateTime CompletedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? NextDueAtUtc { get; set; }
    public string NextDueIntervalSnapshot { get; set; } = string.Empty;
    public int Revision { get; set; } = 1;
}
