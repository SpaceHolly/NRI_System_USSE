using System;
using System.Collections.Generic;

namespace Nri.Shared.Domain;

public sealed class ProductionFacilityDefinition : EntityBase
{
    public string FacilityDefinitionId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string FacilityCategory { get; set; } = ProductionFacilityCategoryIds.Custom;
    public string FacilityType { get; set; } = ProductionFacilityTypeIds.Custom;
    public List<string> SupportedProductionDomains { get; set; } = new();
    public List<string> SupportedPlatformCategories { get; set; } = new();
    public List<string> SupportedSizeClassIds { get; set; } = new();
    public List<string> SupportedModuleCategories { get; set; } = new();
    public List<string> SupportedProcessIds { get; set; } = new();
    public int BaseQualityTier { get; set; } = 1;
    public int BaseCapacityRating { get; set; } = 1;
    public int BaseComplexityHandling { get; set; } = 1;
    public List<string> BaseSpecializationTags { get; set; } = new();
    public string RequiredStaffSummary { get; set; } = string.Empty;
    public string RequiredEquipmentSummary { get; set; } = string.Empty;
    public string RequiredInfrastructureSummary { get; set; } = string.Empty;
    public decimal BaseCostMultiplier { get; set; } = 1m;
    public decimal BaseTimeMultiplier { get; set; } = 1m;
    public decimal BaseRiskMultiplier { get; set; } = 1m;
    public string LegalCategoryHint { get; set; } = string.Empty;
    public bool IsPlayerVisible { get; set; }
    public string VisibilityMode { get; set; } = ProjectVisibilityModeIds.GmOnly;
    public bool IsArchived { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public List<string> Tags { get; set; } = new();
    public Dictionary<string, object> ExtraData { get; set; } = new();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new();
}

public sealed class ProductionFacilityState : EntityBase
{
    public string FacilityId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string FacilityDefinitionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string OwnerEntityType { get; set; } = string.Empty;
    public string OwnerEntityId { get; set; } = string.Empty;
    public string OperatorEntityType { get; set; } = string.Empty;
    public string OperatorEntityId { get; set; } = string.Empty;
    public string LocationEntityType { get; set; } = string.Empty;
    public string LocationEntityId { get; set; } = string.Empty;
    public string CountryId { get; set; } = string.Empty;
    public string RegionId { get; set; } = string.Empty;
    public string CityId { get; set; } = string.Empty;
    public string JurisdictionId { get; set; } = string.Empty;
    public string FacilityCategory { get; set; } = ProductionFacilityCategoryIds.Custom;
    public string FacilityType { get; set; } = ProductionFacilityTypeIds.Custom;
    public string OperationalStatus { get; set; } = ProductionFacilityStatusIds.Planned;
    public string VisibilityMode { get; set; } = ProjectVisibilityModeIds.GmOnly;
    public bool IsPlayerVisible { get; set; }
    public bool IsArchived { get; set; }
    public List<string> SupportedProductionDomains { get; set; } = new();
    public List<string> SupportedPlatformCategories { get; set; } = new();
    public List<string> SupportedSizeClassIds { get; set; } = new();
    public List<string> SupportedModuleCategories { get; set; } = new();
    public List<string> SupportedProcessIds { get; set; } = new();
    public int QualityTier { get; set; } = 1;
    public int CapacityRating { get; set; } = 1;
    public int ComplexityHandling { get; set; } = 1;
    public List<string> SpecializationTags { get; set; } = new();
    public int CurrentLoadPercent { get; set; }
    public int QueueLength { get; set; }
    public DateTime? NextAvailableWorldDateTime { get; set; }
    public string MaintenanceStatus { get; set; } = ProductionMaintenanceStatusIds.Normal;
    public string StaffStatus { get; set; } = ProductionResourceStatusIds.Normal;
    public string EquipmentStatus { get; set; } = ProductionResourceStatusIds.Normal;
    public string ResourceAccessSummary { get; set; } = string.Empty;
    public string LegalStatusHint { get; set; } = string.Empty;
    public string DeFactoStatusHint { get; set; } = string.Empty;
    public string FacilityLegalityModeHint { get; set; } = string.Empty;
    public string RiskSummary { get; set; } = string.Empty;
    public string GMHiddenTermsSummary { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
    public string UpdatedByUserId { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public Dictionary<string, object> ExtraData { get; set; } = new();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new();
}

public sealed class ProductionFacilityCapabilityState : EntityBase
{
    public string CapabilityId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string FacilityId { get; set; } = string.Empty;
    public string ProductionDomain { get; set; } = ProductionDomainIds.Custom;
    public List<string> SupportedPlatformCategories { get; set; } = new();
    public List<string> SupportedSizeClassIds { get; set; } = new();
    public List<string> SupportedModuleCategories { get; set; } = new();
    public List<string> SupportedProcessIds { get; set; } = new();
    public int QualityTier { get; set; } = 1;
    public int CapacityRating { get; set; } = 1;
    public int ComplexityHandling { get; set; } = 1;
    public decimal CostMultiplier { get; set; } = 1m;
    public decimal TimeMultiplier { get; set; } = 1m;
    public decimal RiskMultiplier { get; set; } = 1m;
    public bool IsPlayerVisible { get; set; }
    public string PublicSummary { get; set; } = string.Empty;
    public string GMSummary { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object> ExtraData { get; set; } = new();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new();
}

public sealed class ProductionProcessDefinition : EntityBase
{
    public string ProcessId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ProductionDomain { get; set; } = ProductionDomainIds.Custom;
    public string Description { get; set; } = string.Empty;
    public int ComplexityTier { get; set; } = 1;
    public int BaseWorkPoints { get; set; } = 100;
    public decimal BaseCostMultiplier { get; set; } = 1m;
    public decimal BaseTimeMultiplier { get; set; } = 1m;
    public bool IsPlayerVisible { get; set; }
    public bool IsArchived { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object> ExtraData { get; set; } = new();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new();
}

public sealed class ProductionFacilityCapacityState : EntityBase
{
    public string CapacityId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string FacilityId { get; set; } = string.Empty;
    public int CapacityRating { get; set; } = 1;
    public int MaxQueueSlots { get; set; } = 1;
    public int ReservedQueueSlots { get; set; }
    public int CurrentLoadPercent { get; set; }
    public DateTime? NextAvailableWorldDateTime { get; set; }
    public string CapacityNotes { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string UpdatedByUserId { get; set; } = string.Empty;
    public Dictionary<string, object> ExtraData { get; set; } = new();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new();
}

public sealed class ProductionQueueSlotState : EntityBase
{
    public string QueueSlotId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string FacilityId { get; set; } = string.Empty;
    public string QuoteId { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
    public string Status { get; set; } = ProductionQueueSlotStatusIds.Reserved;
    public int QueuePosition { get; set; }
    public DateTime? EstimatedStartUtc { get; set; }
    public DateTime? EstimatedReadyUtc { get; set; }
    public string PublicSummary { get; set; } = string.Empty;
    public string GMNotes { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
    public Dictionary<string, object> ExtraData { get; set; } = new();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new();
}

public sealed class FactoryQuoteState : EntityBase
{
    public string QuoteId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string FacilityId { get; set; } = string.Empty;
    public string BlueprintId { get; set; } = string.Empty;
    public string PresetId { get; set; } = string.Empty;
    public string DraftId { get; set; } = string.Empty;
    public string SourceType { get; set; } = FactoryOrderSourceTypeIds.Custom;
    public string RequestId { get; set; } = string.Empty;
    public string OwnerUserId { get; set; } = string.Empty;
    public string OwnerCharacterId { get; set; } = string.Empty;
    public string Status { get; set; } = FactoryQuoteStatusIds.Draft;
    public string Name { get; set; } = string.Empty;
    public decimal EstimatedCost { get; set; }
    public int EstimatedWorkPoints { get; set; }
    public int EstimatedDays { get; set; }
    public int QueuePosition { get; set; }
    public string RiskSummary { get; set; } = string.Empty;
    public string PublicTermsSummary { get; set; } = string.Empty;
    public string GMTermsSummary { get; set; } = string.Empty;
    public string RequiredResourcesSummary { get; set; } = string.Empty;
    public string RequiredPermitsSummary { get; set; } = string.Empty;
    public string LegalStatusHint { get; set; } = string.Empty;
    public string FacilityValidationStatus { get; set; } = FactoryValidationStatusIds.NotChecked;
    public List<string> Warnings { get; set; } = new();
    public bool IsPlayerVisible { get; set; } = true;
    public string VisibilityMode { get; set; } = ProjectVisibilityModeIds.OwnerOnly;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? OfferedAtUtc { get; set; }
    public DateTime? ValidUntilUtc { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public string UpdatedByUserId { get; set; } = string.Empty;
    public Dictionary<string, object> ExtraData { get; set; } = new();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new();
}

public sealed class FactoryOrderState : EntityBase
{
    public string OrderId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string QuoteId { get; set; } = string.Empty;
    public string FacilityId { get; set; } = string.Empty;
    public string QueueSlotId { get; set; } = string.Empty;
    public string BlueprintId { get; set; } = string.Empty;
    public string PresetId { get; set; } = string.Empty;
    public string DraftId { get; set; } = string.Empty;
    public string SourceType { get; set; } = FactoryOrderSourceTypeIds.Custom;
    public string ProjectBaseId { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
    public string OwnerUserId { get; set; } = string.Empty;
    public string OwnerCharacterId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = FactoryOrderStatusIds.Draft;
    public decimal EstimatedCost { get; set; }
    public int EstimatedWorkPoints { get; set; }
    public int EstimatedDays { get; set; }
    public string PublicStatusSummary { get; set; } = string.Empty;
    public string RequiredResourcesSummary { get; set; } = string.Empty;
    public string LegalStatusHint { get; set; } = string.Empty;
    public string RiskSummary { get; set; } = string.Empty;
    public string GMHiddenTermsSummary { get; set; } = string.Empty;
    public bool IsPlayerVisible { get; set; } = true;
    public string VisibilityMode { get; set; } = ProjectVisibilityModeIds.OwnerOnly;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ScheduledAtUtc { get; set; }
    public DateTime? EstimatedReadyUtc { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public string UpdatedByUserId { get; set; } = string.Empty;
    public Dictionary<string, object> ExtraData { get; set; } = new();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new();
}

public sealed class FactoryOrderLineState : EntityBase
{
    public string OrderLineId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
    public string ItemKind { get; set; } = string.Empty;
    public string LinkedEntityId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public decimal EstimatedUnitCost { get; set; }
    public string PublicSummary { get; set; } = string.Empty;
    public string GMNotes { get; set; } = string.Empty;
    public Dictionary<string, object> ExtraData { get; set; } = new();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new();
}

public sealed class FactoryOrderTermState : EntityBase
{
    public string TermId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string QuoteId { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
    public string TermType { get; set; } = string.Empty;
    public string PublicText { get; set; } = string.Empty;
    public string GMText { get; set; } = string.Empty;
    public bool IsPlayerVisible { get; set; } = true;
    public Dictionary<string, object> ExtraData { get; set; } = new();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new();
}

public sealed class FactoryOrderPaymentPlanState : EntityBase
{
    public string PaymentPlanId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string QuoteId { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
    public decimal TotalEstimatedCost { get; set; }
    public decimal DepositRequired { get; set; }
    public string CurrencyCode { get; set; } = "MO";
    public string PublicSummary { get; set; } = string.Empty;
    public string GMNotes { get; set; } = string.Empty;
    public bool IsPlayerVisible { get; set; } = true;
    public Dictionary<string, object> ExtraData { get; set; } = new();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new();
}

public sealed class ManufacturingProjectState : EntityBase
{
    public string ManufacturingProjectId { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string FactoryOrderId { get; set; } = string.Empty;
    public string FacilityId { get; set; } = string.Empty;
    public string SourceBlueprintId { get; set; } = string.Empty;
    public string SourcePresetDesignId { get; set; } = string.Empty;
    public string SourceEngineeringProjectId { get; set; } = string.Empty;
    public string CustomerEntityType { get; set; } = string.Empty;
    public string CustomerEntityId { get; set; } = string.Empty;
    public string OwnerEntityType { get; set; } = string.Empty;
    public string OwnerEntityId { get; set; } = string.Empty;
    public string OperatorEntityType { get; set; } = string.Empty;
    public string OperatorEntityId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ManufacturingType { get; set; } = ManufacturingTypeIds.Custom;
    public string ProductionDomain { get; set; } = ProductionDomainIds.Custom;
    public string OrderKind { get; set; } = ManufacturingOrderKindIds.Custom;
    public int Quantity { get; set; } = 1;
    public string TargetQuality { get; set; } = string.Empty;
    public string ActualQuality { get; set; } = string.Empty;
    public string ManufacturingStatus { get; set; } = ManufacturingStatusIds.Planning;
    public string ResourceStatus { get; set; } = ManufacturingResourceStatusIds.Planned;
    public string PaymentStatus { get; set; } = ManufacturingPaymentStatusIds.Planned;
    public string TestingStatus { get; set; } = ManufacturingTestingStatusIds.NotRequired;
    public string AcceptanceStatus { get; set; } = ManufacturingAcceptanceStatusIds.NotReady;
    public string AssetCreationStatus { get; set; } = ManufacturingAssetCreationStatusIds.NotReady;
    public DateTime? PlannedStartWorldDateTime { get; set; }
    public DateTime? PlannedEndWorldDateTime { get; set; }
    public DateTime? ActualStartWorldDateTime { get; set; }
    public DateTime? ActualEndWorldDateTime { get; set; }
    public long EstimatedDurationWorldSeconds { get; set; }
    public int RequiredProgressTotal { get; set; } = 100;
    public int CurrentManufacturingProgress { get; set; }
    public decimal ProgressPercent { get; set; }
    public string CurrentStageId { get; set; } = string.Empty;
    public decimal EstimatedTotalCost { get; set; }
    public decimal ActualTotalCost { get; set; }
    public string CurrencyCode { get; set; } = "MO";
    public string CostBreakdownSummary { get; set; } = string.Empty;
    public string PaymentPlanSummary { get; set; } = string.Empty;
    public string ResourceRequirementSummary { get; set; } = string.Empty;
    public string ReservedResourceSummary { get; set; } = string.Empty;
    public string ConsumedResourceSummary { get; set; } = string.Empty;
    public string ExpectedResultSummary { get; set; } = string.Empty;
    public string ActualResultSummary { get; set; } = string.Empty;
    public List<string> CreatedAssetIds { get; set; } = new();
    public string FailureSummary { get; set; } = string.Empty;
    public string DefectSummary { get; set; } = string.Empty;
    public string ManufacturingRiskRating { get; set; } = string.Empty;
    public string LegalBoundarySummary { get; set; } = string.Empty;
    public string GMHiddenRiskSummary { get; set; } = string.Empty;
    public bool IsPlayerVisible { get; set; } = true;
    public string VisibilityMode { get; set; } = ProjectVisibilityModeIds.OwnerOnly;
    public bool IsArchived { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
    public string UpdatedByUserId { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public Dictionary<string, object> ExtraData { get; set; } = new();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new();
}

public sealed class ManufacturingStageState : EntityBase
{
    public string ManufacturingProjectId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string StageType { get; set; } = ManufacturingStageTypeIds.Custom;
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = ManufacturingStageStatusIds.Planned;
    public int SortOrder { get; set; }
    public int RequiredProgress { get; set; } = 20;
    public int CurrentProgress { get; set; }
    public bool RequiresResources { get; set; } = true;
    public bool RequiresPayment { get; set; }
    public bool RequiresTesting { get; set; }
    public bool IsPlayerVisible { get; set; } = true;
    public string PublicSummary { get; set; } = string.Empty;
    public string GMNotes { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string UpdatedByUserId { get; set; } = string.Empty;
    public Dictionary<string, object> ExtraData { get; set; } = new();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new();
}

public sealed class ManufacturingResourcePlanState : EntityBase
{
    public string ManufacturingProjectId { get; set; } = string.Empty;
    public string StageId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public string ResourceId { get; set; } = string.Empty;
    public string ResourceName { get; set; } = string.Empty;
    public decimal RequiredQuantity { get; set; }
    public decimal ReservedQuantity { get; set; }
    public decimal ConsumedQuantity { get; set; }
    public string Unit { get; set; } = "pcs";
    public string SourceType { get; set; } = string.Empty;
    public string SourceId { get; set; } = string.Empty;
    public string Status { get; set; } = ManufacturingResourceStatusIds.Planned;
    public bool IsPlayerVisible { get; set; } = true;
    public string PublicSummary { get; set; } = string.Empty;
    public string GMNotes { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string UpdatedByUserId { get; set; } = string.Empty;
    public Dictionary<string, object> ExtraData { get; set; } = new();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new();
}

public sealed class ManufacturingResourceReservationState : EntityBase
{
    public string ManufacturingProjectId { get; set; } = string.Empty;
    public string ResourcePlanId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public decimal ReservedQuantity { get; set; }
    public decimal ConsumedQuantity { get; set; }
    public string Unit { get; set; } = "pcs";
    public string InventoryItemId { get; set; } = string.Empty;
    public string SourceEntityType { get; set; } = string.Empty;
    public string SourceEntityId { get; set; } = string.Empty;
    public string Status { get; set; } = ManufacturingReservationStatusIds.Reserved;
    public bool IsPlayerVisible { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string UpdatedByUserId { get; set; } = string.Empty;
    public Dictionary<string, object> ExtraData { get; set; } = new();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new();
}

public sealed class ManufacturingCostLedgerEntry : EntityBase
{
    public string ManufacturingProjectId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string CostType { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = "MO";
    public bool IsEstimated { get; set; } = true;
    public bool IsPlayerVisible { get; set; } = true;
    public string PublicSummary { get; set; } = string.Empty;
    public string GMNotes { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
    public Dictionary<string, object> ExtraData { get; set; } = new();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new();
}

public sealed class ManufacturingPaymentState : EntityBase
{
    public string ManufacturingProjectId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string PaymentKind { get; set; } = ManufacturingPaymentKindIds.Deposit;
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = "MO";
    public string Status { get; set; } = ManufacturingPaymentStatusIds.Planned;
    public DateTime? PaidAtUtc { get; set; }
    public string ConfirmedByUserId { get; set; } = string.Empty;
    public bool IsPlayerVisible { get; set; } = true;
    public string PublicSummary { get; set; } = string.Empty;
    public string GMNotes { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object> ExtraData { get; set; } = new();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new();
}

public sealed class ManufacturingProgressEntry : EntityBase
{
    public string ManufacturingProjectId { get; set; } = string.Empty;
    public string StageId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public int ProgressDelta { get; set; }
    public string PublicSummary { get; set; } = string.Empty;
    public string GMNotes { get; set; } = string.Empty;
    public bool IsPlayerVisible { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
    public Dictionary<string, object> ExtraData { get; set; } = new();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new();
}

public sealed class ManufacturingTestPlanState : EntityBase
{
    public string ManufacturingProjectId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = ManufacturingTestingStatusIds.Planned;
    public string PublicSummary { get; set; } = string.Empty;
    public string GMNotes { get; set; } = string.Empty;
    public bool IsPlayerVisible { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object> ExtraData { get; set; } = new();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new();
}

public sealed class ManufacturingTestResultState : EntityBase
{
    public string ManufacturingProjectId { get; set; } = string.Empty;
    public string TestPlanId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string Result { get; set; } = ManufacturingTestResultIds.Planned;
    public string PublicSummary { get; set; } = string.Empty;
    public string GMNotes { get; set; } = string.Empty;
    public bool IsPlayerVisible { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
    public Dictionary<string, object> ExtraData { get; set; } = new();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new();
}

public sealed class ManufacturingDefectState : EntityBase
{
    public string ManufacturingProjectId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string Severity { get; set; } = "minor";
    public string Status { get; set; } = ManufacturingDefectStatusIds.Open;
    public bool IsCritical { get; set; }
    public string PublicSummary { get; set; } = string.Empty;
    public string GMNotes { get; set; } = string.Empty;
    public bool IsPlayerVisible { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string UpdatedByUserId { get; set; } = string.Empty;
    public Dictionary<string, object> ExtraData { get; set; } = new();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new();
}

public sealed class ManufacturingAcceptanceState : EntityBase
{
    public string ManufacturingProjectId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string Status { get; set; } = ManufacturingAcceptanceStatusIds.NotReady;
    public bool AcceptedWithDefects { get; set; }
    public bool GMOverride { get; set; }
    public string PublicSummary { get; set; } = string.Empty;
    public string GMNotes { get; set; } = string.Empty;
    public bool IsPlayerVisible { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string ReviewedByUserId { get; set; } = string.Empty;
    public Dictionary<string, object> ExtraData { get; set; } = new();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new();
}

public sealed class ManufacturedAssetState : EntityBase
{
    public string ManufacturingProjectId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string AssetStateId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string AssetType { get; set; } = "manufactured_asset";
    public string BlueprintId { get; set; } = string.Empty;
    public string OwnerEntityType { get; set; } = string.Empty;
    public string OwnerEntityId { get; set; } = string.Empty;
    public string OperatorEntityType { get; set; } = string.Empty;
    public string OperatorEntityId { get; set; } = string.Empty;
    public string Status { get; set; } = ManufacturedAssetStatusIds.Created;
    public bool IsPlayerVisible { get; set; } = true;
    public string VisibilityMode { get; set; } = ProjectVisibilityModeIds.OwnerOnly;
    public string PublicSummary { get; set; } = string.Empty;
    public string GMNotes { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
    public string UpdatedByUserId { get; set; } = string.Empty;
    public Dictionary<string, object> ExtraData { get; set; } = new();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new();
}
