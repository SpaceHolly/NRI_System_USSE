using System;
using System.Collections.Generic;

namespace Nri.Shared.Domain;

public sealed class EngineeringPlatformDefinition : EntityBase
{
    public string PlatformId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string PublicDescription { get; set; } = string.Empty;
    public string GMDescription { get; set; } = string.Empty;
    public string PlatformKind { get; set; } = EngineeringPlatformKindIds.Custom;
    public string SizeClassId { get; set; } = EngineeringSizeClassIds.Medium;
    public decimal BaseMassKg { get; set; }
    public decimal BaseVolumeM3 { get; set; }
    public int BaseSlots { get; set; }
    public int BaseHardpoints { get; set; }
    public decimal BasePowerOutput { get; set; }
    public decimal BasePowerLoad { get; set; }
    public int BaseCrewMin { get; set; }
    public int BaseCrewMax { get; set; }
    public decimal BaseCost { get; set; }
    public int DifficultyTier { get; set; }
    public bool IsPlayerVisible { get; set; }
    public string VisibilityMode { get; set; } = ProjectVisibilityModeIds.GmOnly;
    public bool IsArchived { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
    public string UpdatedByUserId { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public Dictionary<string, object> ExtraData { get; set; } = new();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new();
}

public sealed class EngineeringPlatformSizeClassDefinition : EntityBase
{
    public string SizeClassId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal MinLengthMeters { get; set; }
    public decimal MaxLengthMeters { get; set; }
    public decimal MinMassKg { get; set; }
    public decimal MaxMassKg { get; set; }
    public decimal MaxVolumeM3 { get; set; }
    public int MaxSlots { get; set; }
    public int MaxHardpoints { get; set; }
    public bool IsPlayerVisible { get; set; }
    public string VisibilityMode { get; set; } = ProjectVisibilityModeIds.GmOnly;
    public bool IsArchived { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
    public string UpdatedByUserId { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public Dictionary<string, object> ExtraData { get; set; } = new();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new();
}

public sealed class EngineeringModuleDefinition : EntityBase
{
    public string ModuleId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string PublicDescription { get; set; } = string.Empty;
    public string GMDescription { get; set; } = string.Empty;
    public string ModuleCategory { get; set; } = EngineeringModuleCategoryIds.Custom;
    public string SlotType { get; set; } = EngineeringModuleSlotTypeIds.Internal;
    public int SlotCost { get; set; }
    public int HardpointCost { get; set; }
    public decimal MassKg { get; set; }
    public decimal VolumeM3 { get; set; }
    public decimal PowerOutput { get; set; }
    public decimal PowerLoad { get; set; }
    public int CrewRequired { get; set; }
    public decimal Cost { get; set; }
    public int DifficultyTier { get; set; }
    public string DiceExpression { get; set; } = string.Empty;
    public string WeaponProfileId { get; set; } = string.Empty;
    public bool IsRestricted { get; set; }
    public bool IsMilitary { get; set; }
    public bool IsPlayerVisible { get; set; }
    public string VisibilityMode { get; set; } = ProjectVisibilityModeIds.GmOnly;
    public bool IsArchived { get; set; }
    public List<string> CompatiblePlatformKinds { get; set; } = new();
    public List<string> RequiredTags { get; set; } = new();
    public List<string> IncompatibleModuleIds { get; set; } = new();
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
    public string UpdatedByUserId { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public Dictionary<string, object> ExtraData { get; set; } = new();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new();
}

public sealed class EngineeringModuleSlotRequirement : EntityBase
{
    public string ModuleId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string SlotType { get; set; } = EngineeringModuleSlotTypeIds.Internal;
    public int RequiredSlots { get; set; }
    public bool IsMandatory { get; set; } = true;
    public bool IsPlayerVisible { get; set; } = true;
    public string PublicSummary { get; set; } = string.Empty;
    public string GMSummary { get; set; } = string.Empty;
    public Dictionary<string, object> ExtraData { get; set; } = new();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new();
}

public sealed class EngineeringModuleCompatibilityRule : EntityBase
{
    public string RuleId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string ModuleId { get; set; } = string.Empty;
    public string TargetModuleId { get; set; } = string.Empty;
    public string PlatformKind { get; set; } = string.Empty;
    public string SizeClassId { get; set; } = string.Empty;
    public string RuleType { get; set; } = EngineeringCompatibilityRuleTypeIds.Allowed;
    public string Message { get; set; } = string.Empty;
    public bool IsPlayerVisible { get; set; }
    public Dictionary<string, object> ExtraData { get; set; } = new();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new();
}

public sealed class EngineeringPowerProfileDefinition : EntityBase
{
    public string PowerProfileId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal PowerOutput { get; set; }
    public decimal PowerLoad { get; set; }
    public decimal OverloadCapacity { get; set; }
    public int OverloadDurationRounds { get; set; }
    public bool IsOverloadAllowed { get; set; }
    public bool IsPlayerVisible { get; set; }
    public string PublicNotes { get; set; } = string.Empty;
    public string GMNotes { get; set; } = string.Empty;
    public Dictionary<string, object> ExtraData { get; set; } = new();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new();
}

public sealed class EngineeringWeaponProfileDefinition : EntityBase
{
    public string WeaponProfileId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string ModuleId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DiceExpression { get; set; } = string.Empty;
    public string RangeSummary { get; set; } = string.Empty;
    public string AmmoSummary { get; set; } = string.Empty;
    public List<string> Traits { get; set; } = new();
    public bool IsPlayerVisible { get; set; }
    public string PublicNotes { get; set; } = string.Empty;
    public string GMNotes { get; set; } = string.Empty;
    public Dictionary<string, object> ExtraData { get; set; } = new();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new();
}

public sealed class PresetVehicleDesignDefinition : EntityBase
{
    public string PresetId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string PlatformId { get; set; } = string.Empty;
    public string SizeClassId { get; set; } = string.Empty;
    public List<string> ModuleIds { get; set; } = new();
    public string RoleSummary { get; set; } = string.Empty;
    public bool IsPlayerVisible { get; set; }
    public string VisibilityMode { get; set; } = ProjectVisibilityModeIds.GmOnly;
    public bool IsArchived { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
    public string UpdatedByUserId { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public Dictionary<string, object> ExtraData { get; set; } = new();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new();
}

public sealed class VehicleDesignDraft : EntityBase
{
    public string DraftId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string PresetId { get; set; } = string.Empty;
    public string OwnerUserId { get; set; } = string.Empty;
    public string OwnerCharacterId { get; set; } = string.Empty;
    public string ActorEntityType { get; set; } = ProjectParticipantEntityTypeIds.PlayerCharacter;
    public string ActorEntityId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string PlatformId { get; set; } = string.Empty;
    public string SizeClassId { get; set; } = string.Empty;
    public List<string> ModuleIds { get; set; } = new();
    public string IntendedRole { get; set; } = string.Empty;
    public string Status { get; set; } = EngineeringDesignStatusIds.Draft;
    public bool IsPlayerVisible { get; set; } = true;
    public string VisibilityMode { get; set; } = ProjectVisibilityModeIds.OwnerOnly;
    public string ValidationSummary { get; set; } = string.Empty;
    public string CostSummary { get; set; } = string.Empty;
    public string PlayerNotes { get; set; } = string.Empty;
    public string GMNotes { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
    public string UpdatedByUserId { get; set; } = string.Empty;
    public Dictionary<string, object> ExtraData { get; set; } = new();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new();
}

public sealed class EngineeringDesignProjectState : EntityBase
{
    public string CampaignId { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string ProjectBaseId { get; set; } = string.Empty;
    public string DraftId { get; set; } = string.Empty;
    public string PresetId { get; set; } = string.Empty;
    public string PlatformId { get; set; } = string.Empty;
    public string SizeClassId { get; set; } = string.Empty;
    public List<string> ModuleIds { get; set; } = new();
    public string OwnerUserId { get; set; } = string.Empty;
    public string ActorEntityType { get; set; } = ProjectParticipantEntityTypeIds.PlayerCharacter;
    public string ActorEntityId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string IntendedRole { get; set; } = string.Empty;
    public string Status { get; set; } = EngineeringDesignStatusIds.Draft;
    public int ProgressPercent { get; set; }
    public int WorkPointsDone { get; set; }
    public int WorkPointsRequired { get; set; } = 100;
    public string ValidationStatus { get; set; } = EngineeringValidationStatusIds.NotChecked;
    public string BlueprintStatus { get; set; } = EngineeringBlueprintStatusIds.Draft;
    public bool IsPlayerVisible { get; set; } = true;
    public string VisibilityMode { get; set; } = ProjectVisibilityModeIds.PlayerVisible;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public string UpdatedByUserId { get; set; } = string.Empty;
    public string PublicNotes { get; set; } = string.Empty;
    public string GMNotes { get; set; } = string.Empty;
    public Dictionary<string, object> ExtraData { get; set; } = new();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new();
}

public sealed class EngineeringDesignValidationResult : EntityBase
{
    public string ValidationId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string DraftId { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string PlatformId { get; set; } = string.Empty;
    public List<string> ModuleIds { get; set; } = new();
    public string Status { get; set; } = EngineeringValidationStatusIds.NotChecked;
    public decimal TotalMassKg { get; set; }
    public decimal TotalVolumeM3 { get; set; }
    public int TotalSlots { get; set; }
    public int TotalHardpoints { get; set; }
    public decimal TotalPowerOutput { get; set; }
    public decimal TotalPowerLoad { get; set; }
    public int TotalCrewRequired { get; set; }
    public decimal TotalCost { get; set; }
    public int ComplexityScore { get; set; }
    public List<EngineeringValidationIssueValue> Issues { get; set; } = new();
    public DateTime BuiltAtUtc { get; set; } = DateTime.UtcNow;
    public string BuiltByUserId { get; set; } = string.Empty;
    public Dictionary<string, object> ExtraData { get; set; } = new();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new();
}

public sealed class EngineeringValidationIssueValue
{
    public string Severity { get; set; } = EngineeringValidationSeverityIds.Info;
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsPlayerVisible { get; set; } = true;
}

public sealed class EngineeringDesignCostEstimate : EntityBase
{
    public string EstimateId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string DraftId { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public decimal BaseCost { get; set; }
    public decimal ModuleCost { get; set; }
    public decimal ComplexityCost { get; set; }
    public decimal TotalCost { get; set; }
    public int EstimatedWorkDays { get; set; }
    public string MaterialSummary { get; set; } = string.Empty;
    public DateTime BuiltAtUtc { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object> ExtraData { get; set; } = new();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new();
}

public sealed class VehicleDesignBlueprint : EntityBase
{
    public string BlueprintId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string DraftId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string PlatformId { get; set; } = string.Empty;
    public string SizeClassId { get; set; } = string.Empty;
    public List<string> ModuleIds { get; set; } = new();
    public string PublicSummary { get; set; } = string.Empty;
    public string GMNotes { get; set; } = string.Empty;
    public string Status { get; set; } = EngineeringBlueprintStatusIds.Prepared;
    public bool IsPlayerVisible { get; set; } = true;
    public string VisibilityMode { get; set; } = ProjectVisibilityModeIds.PlayerVisible;
    public DateTime PreparedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? AcceptedAtUtc { get; set; }
    public string PreparedByUserId { get; set; } = string.Empty;
    public string AcceptedByUserId { get; set; } = string.Empty;
    public Dictionary<string, object> ExtraData { get; set; } = new();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new();
}

public sealed class EngineeringBlueprintReference : EntityBase
{
    public string BlueprintId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ReferenceType { get; set; } = "engineering_blueprint";
    public bool IsPlayerVisible { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
    public Dictionary<string, object> ExtraData { get; set; } = new();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new();
}
