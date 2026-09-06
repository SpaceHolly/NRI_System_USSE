using System;
using System.Collections.Generic;

namespace Nri.Shared.Domain;

public static class AssetConstructionRuntimeIds0197
{
    public const string RuntimeKind = "asset_construction_0197";
    public const string AssetKindBuilding = "building";
    public const string OwnerKindCharacter = "character";
}

public static class ConstructionSiteStatusIds0197
{
    public const string Planned = "planned";
    public const string ResourcesReserved = "resources_reserved";
    public const string InConstruction = "in_construction";
    public const string Completed = "completed";
    public const string Cancelled = "cancelled";
    public const string Failed = "failed";
}

public static class ConstructionReservationStatusIds0197
{
    public const string Reserved = "reserved";
    public const string PartiallyConsumed = "partially_consumed";
    public const string Consumed = "consumed";
    public const string Released = "released";
}

public static class LargeAssetLifecycleStatusIds0197
{
    public const string Operational = "operational";
}

public static class LargeAssetMaintenanceStatusIds0197
{
    public const string NotScheduled = "not_scheduled";
    public const string Current = "current";
    public const string Due = "due";
    public const string InMaintenance = "in_maintenance";
    public const string Overdue = "overdue";
}

public sealed class AssetConstructionSnapshot0197
{
    public string BlueprintId { get; set; } = string.Empty;
    public string BlueprintStableKey { get; set; } = string.Empty;
    public int BlueprintRevision { get; set; }
    public string BlueprintName { get; set; } = string.Empty;
    public string AssetKind { get; set; } = AssetConstructionRuntimeIds0197.AssetKindBuilding;
    public string ConfigurationSummary { get; set; } = string.Empty;
    public string CalculationVersion { get; set; } = string.Empty;
    public string BuildingType { get; set; } = string.Empty;
    public int FloorCount { get; set; }
    public decimal TotalArea { get; set; }
    public string ConstructionMethod { get; set; } = string.Empty;
    public string Quality { get; set; } = string.Empty;
    public int StructuralIntegrity { get; set; }
    public string EnergyProfileSummary { get; set; } = string.Empty;
    public string StorageCapacitySummary { get; set; } = string.Empty;
    public List<string> ModuleReferences { get; set; } = new List<string>();
    public string TargetOwnerKind { get; set; } = AssetConstructionRuntimeIds0197.OwnerKindCharacter;
    public string TargetOwnerId { get; set; } = string.Empty;
    public string TargetOwnerDisplayName { get; set; } = string.Empty;
    public string LocationId { get; set; } = string.Empty;
    public string LocationName { get; set; } = string.Empty;
    public string ProjectTemplateKey { get; set; } = "reference.asset_construction.building.v1";
    public string ProjectTemplateName { get; set; } = "Строительство здания";
    public string FacilitySummary { get; set; } = string.Empty;
    public string PersonnelSummary { get; set; } = string.Empty;
    public string LicenseSummary { get; set; } = string.Empty;
    public string ExpectedAssetKind { get; set; } = AssetConstructionRuntimeIds0197.AssetKindBuilding;
    public string RuleSetId { get; set; } = string.Empty;
    public string PublicWarning { get; set; } = "Это крупный актив, а не предмет инвентаря.";
    public List<AssetConstructionStageSnapshot0197> Stages { get; set; } = new List<AssetConstructionStageSnapshot0197>();
    public List<ProjectRequirementSnapshot0191> Requirements { get; set; } = new List<ProjectRequirementSnapshot0191>();
    public List<ProjectMaterialSnapshot0191> Materials { get; set; } = new List<ProjectMaterialSnapshot0191>();
    public string SnapshotChecksum { get; set; } = string.Empty;
}

public sealed class AssetConstructionStageSnapshot0197
{
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int Order { get; set; }
    public string PublicSummary { get; set; } = string.Empty;
    public List<ProjectMaterialSnapshot0191> Resources { get; set; } = new List<ProjectMaterialSnapshot0191>();
}

public sealed class ConstructionSiteState0197 : EntityBase
{
    public string ProjectId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string BlueprintId { get; set; } = string.Empty;
    public string BlueprintStableKey { get; set; } = string.Empty;
    public int BlueprintRevision { get; set; }
    public string BlueprintName { get; set; } = string.Empty;
    public string OwnerKind { get; set; } = AssetConstructionRuntimeIds0197.OwnerKindCharacter;
    public string OwnerId { get; set; } = string.Empty;
    public string OwnerUserId { get; set; } = string.Empty;
    public string LocationId { get; set; } = string.Empty;
    public string LocationName { get; set; } = string.Empty;
    public string Status { get; set; } = ConstructionSiteStatusIds0197.Planned;
    public int ProgressPercent { get; set; }
    public string CurrentStageKey { get; set; } = string.Empty;
    public string CurrentStageName { get; set; } = string.Empty;
    public List<string> CompletedStageKeys { get; set; } = new List<string>();
    public List<ProjectMaterialSnapshot0191> ConsumedResources { get; set; } = new List<ProjectMaterialSnapshot0191>();
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string AssetInstanceId { get; set; } = string.Empty;
    public int Revision { get; set; } = 1;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string UpdatedByUserId { get; set; } = string.Empty;
}

public sealed class ConstructionResourceReservationState0197 : EntityBase
{
    public string ProjectId { get; set; } = string.Empty;
    public string ConstructionSiteId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string CharacterId { get; set; } = string.Empty;
    public string StageKey { get; set; } = string.Empty;
    public string ResourceDefinitionId { get; set; } = string.Empty;
    public string ResourceDisplayName { get; set; } = string.Empty;
    public string InventoryItemId { get; set; } = string.Empty;
    public decimal QuantityReserved { get; set; }
    public decimal QuantityConsumed { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string Status { get; set; } = ConstructionReservationStatusIds0197.Reserved;
    public string ReservationOperationId { get; set; } = string.Empty;
    public DateTime ReservedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ConsumedAtUtc { get; set; }
    public DateTime? ReleasedAtUtc { get; set; }
    public int Revision { get; set; } = 1;
}

public sealed class ConstructionStageConsumptionState0197 : EntityBase
{
    public string ProjectId { get; set; } = string.Empty;
    public string ConstructionSiteId { get; set; } = string.Empty;
    public string StageKey { get; set; } = string.Empty;
    public string StageName { get; set; } = string.Empty;
    public List<ProjectMaterialSnapshot0191> Resources { get; set; } = new List<ProjectMaterialSnapshot0191>();
    public string OperationId { get; set; } = string.Empty;
    public string ConsumedByUserId { get; set; } = string.Empty;
    public DateTime ConsumedAtUtc { get; set; } = DateTime.UtcNow;
    public int Revision { get; set; } = 1;
}

public sealed class LargeAssetMaintenanceProfileState0197 : EntityBase
{
    public string AssetInstanceId { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string Status { get; set; } = LargeAssetMaintenanceStatusIds0197.NotScheduled;
    public string PersonnelRequirementsSummary { get; set; } = string.Empty;
    public List<string> ResourceFuelCategories { get; set; } = new List<string>();
    public string StorageSecurityRequirements { get; set; } = string.Empty;
    public string LicenseDocumentRequirements { get; set; } = string.Empty;
    public string MaintenanceIntervalDefinitionReference { get; set; } = string.Empty;
    public List<AssetMaintenanceRequirementState0198> Requirements { get; set; } = new List<AssetMaintenanceRequirementState0198>();
    public string KeySpecialistCharacterId { get; set; } = string.Empty;
    public string KeySpecialistDisplayName { get; set; } = string.Empty;
    public List<string> LicenseDocumentReferences { get; set; } = new List<string>();
    public DateTime? LastMaintenanceCompletedAtUtc { get; set; }
    public DateTime? NextMaintenanceDueAtUtc { get; set; }
    public string LastOperationId { get; set; } = string.Empty;
    public int Revision { get; set; } = 1;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
