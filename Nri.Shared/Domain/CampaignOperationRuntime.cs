using System;
using System.Collections.Generic;

namespace Nri.Shared.Domain;

public static class SessionFeatureFlags
{
    public const bool UseCurrentSessionMvp = false;
    public const bool UseSessionStateV1 = false;
    public const bool UseSessionSceneLink = false;
    public const bool UseSessionMapLink = false;
    public const bool UseSessionPlayerView = false;
    public const bool UseSessionQuickLinks = false;
}

public static class CharacterGroupFeatureFlags
{
    public const bool UseCharacterGroupsMvp = false;
    public const bool UseActiveGroupMvp = false;
    public const bool UseGroupMembershipV1 = false;
    public const bool UseGroupPlayerView = false;
    public const bool UseGroupSessionLink = false;
}

public static class CharacterOwnershipFeatureFlags
{
    public const bool UseCharacterOwnershipMvp = false;
    public const bool UseCharacterAssignmentMvp = false;
    public const bool UseCharacterRoleConversionMvp = false;
    public const bool UseCharacterOwnerPlayerView = false;
    public const bool UseCharacterOwnershipAudit = false;
    public const bool UseCharacterGroupOwnershipSync = false;
}

public static class PlayerRequestFeatureFlags
{
    public const bool UsePlayerRequestsMvp = false;
    public const bool UsePlayerRequestComments = false;
    public const bool UsePlayerRequestAdminReview = false;
    public const bool UsePlayerRequestPlayerView = false;
    public const bool UsePlayerRequestSessionLink = false;
    public const bool UsePlayerRequestCharacterLink = false;
    public const bool UsePlayerRequestProposalPayload = false;
}

public static class WorldCalendarFeatureFlags
{
    public const bool UseWorldCalendarMvp = false;
    public const bool UseWorldCalendarCurrentDate = false;
    public const bool UseWorldCalendarEvents = false;
    public const bool UseWorldCalendarChronicle = false;
    public const bool UseWorldCalendarPlayerView = false;
    public const bool UseWorldCalendarFutureVisibility = false;
    public const bool UseWorldCalendarReminders = false;
    public const bool UseWorldCalendarHolidays = false;
    public const bool UseWorldCalendarSessionLink = false;
}

public static class RealScheduleFeatureFlags
{
    public const bool UseRealScheduleCalendarMvp = false;
    public const bool UseRealScheduleEvents = false;
    public const bool UseRealSchedulePlayerView = false;
    public const bool UseRealScheduleSessionLink = false;
    public const bool UseRealScheduleGroupLink = false;
    public const bool UseRealScheduleWorldCalendarLink = false;
    public const bool UseRealScheduleReminders = false;
}

public static class GMNotesFeatureFlags
{
    public const bool UseGMNotesMvp = false;
    public const bool UseGMQuickNotes = false;
    public const bool UseGMNoteFolders = false;
    public const bool UseGMNoteEntityLinks = false;
    public const bool UseGMNoteSearch = false;
    public const bool UseGMNoteSharedVisibility = false;
    public const bool UseGMNoteAudit = false;
}

public static class EventJournalFeatureFlags
{
    public const bool UseEventJournalMvp = false;
    public const bool UseEventJournalAutomaticIngestion = false;
    public const bool UseEventJournalManualEntries = false;
    public const bool UseEventJournalPlayerView = false;
    public const bool UseEventJournalFilters = false;
    public const bool UseEventJournalCorrections = false;
    public const bool UseEventJournalSessionIntegration = false;
    public const bool UseEventJournalCombatIntegration = false;
    public const bool UseEventJournalRequestIntegration = false;
    public const bool UseEventJournalCalendarIntegration = false;
    public const bool UseEventJournalScheduleIntegration = false;
    public const bool UseEventJournalGroupIntegration = false;
    public const bool UseEventJournalOwnershipIntegration = false;
    public const bool UseEventJournalMapIntegration = false;
    public const bool UseEventJournalGMNoteSafeHooks = false;
}

public static class BackupRestoreFeatureFlags
{
    public const bool UseBackupRestoreMvp = false;
    public const bool UseManualBackupCreation = false;
    public const bool UseBackupVerification = false;
    public const bool UseBackupRestorePreview = false;
    public const bool UseBackupRestoreExecution = false;
    public const bool UseBackupRestoreAudit = false;
    public const bool UseBackupMaintenanceMode = false;
    public const bool UseCampaignScopedBackupExperimental = false;
}

public static class GlobalSearchFeatureFlags
{
    public const bool UseGlobalSearchMvp = false;
    public const bool UseGlobalSearchAdminView = false;
    public const bool UseGlobalSearchPlayerView = false;
    public const bool UseGlobalSearchOpenTargets = false;
    public const bool UseGlobalSearchDiagnostics = false;
}

public static class AudioFeatureFlags
{
    public const bool UseAudioMusicMvp = false;
    public const bool UseAudioAdminControls = false;
    public const bool UseAudioPlayerView = false;
    public const bool UseAudioClientSettings = false;
    public const bool UseAudioPlayerSafeFiltering = false;
    public const bool UseAudioEventJournalIntegration = false;
    public const bool UseAudioGlobalSearchIntegration = false;
}

public static class ProjectFoundationFeatureFlags
{
    public const bool UseProjectFoundationMvp = false;
    public const bool UseProjectBaseV1 = false;
    public const bool UseProjectStagesV1 = false;
    public const bool UseProjectProgressV1 = false;
    public const bool UseProjectParticipantsV1 = false;
    public const bool UseProjectRequirementsV1 = false;
    public const bool UseProjectResourceRequirementsV1 = false;
    public const bool UseProjectApprovalsV1 = false;
    public const bool UseProjectAuditV1 = false;
    public const bool UseProjectPlayerView = false;
    public const bool UseProjectAdminView = false;
    public const bool UseProjectRequestIntegration = false;
    public const bool UseProjectCalendarIntegration = false;
    public const bool UseProjectJournalIntegration = false;
    public const bool UseProjectInventoryReservationBoundary = false;
    public const bool UseProjectKnowledgeBoundary = false;
    public const bool UseProjectBlueprintBoundary = false;
}

public static class KnowledgeResearchFeatureFlags
{
    public const bool UseKnowledgeMvp = false;
    public const bool UseCharacterKnowledgeV1 = false;
    public const bool UseCompanionKnowledgeV1 = false;
    public const bool UseGroupKnowledgeV1 = false;
    public const bool UseAppliedKnowledgeV1 = false;
    public const bool UseKnowledgeVisibilityV1 = false;
    public const bool UseKnowledgeSourcesV1 = false;
    public const bool UseResearchMvp = false;
    public const bool UseResearchProjectV1 = false;
    public const bool UseResearchProgressV1 = false;
    public const bool UseResearchResultsV1 = false;
    public const bool UseResearchPlayerView = false;
    public const bool UseResearchAdminView = false;
    public const bool UseResearchRequestIntegration = false;
    public const bool UseResearchProjectFoundationIntegration = false;
    public const bool UseResearchCalendarIntegration = false;
    public const bool UseResearchJournalIntegration = false;
    public const bool UseResearchGlobalSearchIntegration = false;
}

public static class DevelopmentFeatureFlags
{
    public const bool UseDevelopmentHexagonMvp = false;
    public const bool UseDevelopmentProfileV1 = false;
    public const bool UseDevelopmentNodeDefinitions = false;
    public const bool UseMainDevelopmentHexagon = false;
    public const bool UseMultiDevelopmentHexagons = false;
    public const bool UseMagicDevelopmentHexagon = false;
    public const bool UseDevelopmentDirections = false;
    public const bool UseDevelopmentNodePurchase = false;
    public const bool UseExperienceCoins = false;
    public const bool UseDevelopmentRequirements = false;
    public const bool UseDevelopmentRewards = false;
    public const bool UseDevelopmentPlayerView = false;
    public const bool UseDevelopmentAdminView = false;
    public const bool UseDevelopmentRequestIntegration = false;
    public const bool UseDevelopmentKnowledgeRequirements = false;
    public const bool UseDevelopmentJournalIntegration = false;
    public const bool UseDevelopmentSearchIntegration = false;
}

public static class CraftingFeatureFlags
{
    public const bool UseCraftingMvp = false;
    public const bool UseCraftingRecipesV1 = false;
    public const bool UseCraftingProjectsV1 = false;
    public const bool UseCraftingProjectFoundationIntegration = false;
    public const bool UseCraftingInventoryReservation = false;
    public const bool UseCraftingResourceConsumption = false;
    public const bool UseCraftingResultCreation = false;
    public const bool UseCraftingQualityMvp = false;
    public const bool UseCraftingPlayerView = false;
    public const bool UseCraftingAdminView = false;
    public const bool UseCraftingRequestIntegration = false;
    public const bool UseCraftingKnowledgeIntegration = false;
    public const bool UseCraftingJournalIntegration = false;
    public const bool UseCraftingSearchIntegration = false;
    public const bool UseCraftingSyncEvents = false;
}

public static class EngineeringFeatureFlags
{
    public const bool UseEngineeringDesignMvp = false;
    public const bool UseVehicleConstructorV1 = false;
    public const bool UseEngineeringPlatformDefinitions = false;
    public const bool UseEngineeringSizeClasses = false;
    public const bool UseEngineeringModules = false;
    public const bool UseEngineeringPowerProfiles = false;
    public const bool UseEngineeringDiceExpressions = false;
    public const bool UseEngineeringPresetDesigns = false;
    public const bool UseEngineeringCustomDesigns = false;
    public const bool UseEngineeringDesignValidation = false;
    public const bool UseEngineeringCostEstimate = false;
    public const bool UseEngineeringBlueprintResult = false;
    public const bool UseEngineeringProjectFoundationIntegration = false;
    public const bool UseEngineeringResearchIntegration = false;
    public const bool UseEngineeringCraftingBoundary = false;
    public const bool UseEngineeringPlayerView = false;
    public const bool UseEngineeringAdminView = false;
    public const bool UseEngineeringRequestIntegration = false;
    public const bool UseEngineeringJournalIntegration = false;
    public const bool UseEngineeringSearchIntegration = false;
    public const bool UseEngineeringSyncEvents = false;
}

public static class ProductionFeatureFlags
{
    public const bool UseProductionFacilitiesMvp = false;
    public const bool UseProductionFacilityDefinitions = false;
    public const bool UseProductionFacilityCapabilities = false;
    public const bool UseProductionFacilityCapacity = false;
    public const bool UseFactoryQuotes = false;
    public const bool UseFactoryOrders = false;
    public const bool UseFactoryOrderQueue = false;
    public const bool UseFactoryOrderBlueprintLink = false;
    public const bool UseFactoryOrderPresetVsCustom = false;
    public const bool UseFactoryOrderPlayerView = false;
    public const bool UseFactoryOrderAdminView = false;
    public const bool UseFactoryOrderRequestIntegration = false;
    public const bool UseFactoryOrderProjectFoundationIntegration = false;
    public const bool UseFactoryOrderEngineeringIntegration = false;
    public const bool UseFactoryOrderInventoryBoundary = false;
    public const bool UseFactoryOrderJournalIntegration = false;
    public const bool UseFactoryOrderSearchIntegration = false;
    public const bool UseFactoryOrderSyncEvents = false;
}

public static class ManufacturingFeatureFlags
{
    public const bool UseManufacturingMvp = false;
    public const bool UseManufacturingProjects = false;
    public const bool UseManufacturingStages = false;
    public const bool UseManufacturingResourcePlan = false;
    public const bool UseManufacturingResourceReservation = false;
    public const bool UseManufacturingResourceConsumption = false;
    public const bool UseManufacturingCostTracking = false;
    public const bool UseManufacturingPaymentTracking = false;
    public const bool UseManufacturingProgress = false;
    public const bool UseManufacturingTesting = false;
    public const bool UseManufacturingDefects = false;
    public const bool UseManufacturingAcceptance = false;
    public const bool UseManufacturedAssets = false;
    public const bool UseManufacturingOwnershipTransfer = false;
    public const bool UseManufacturingOperationStart = false;
    public const bool UseManufacturingAdminView = false;
    public const bool UseManufacturingPlayerView = false;
    public const bool UseManufacturingFactoryOrderIntegration = false;
    public const bool UseManufacturingEngineeringIntegration = false;
    public const bool UseManufacturingInventoryIntegration = false;
    public const bool UseManufacturingJournalIntegration = false;
    public const bool UseManufacturingSearchIntegration = false;
    public const bool UseManufacturingSyncEvents = false;
}

public static class ClientFunctionalizationFeatureFlags
{
    public const bool UseFunctionalAdminDashboard = false;
    public const bool UseFunctionalPlayerDashboard = false;
    public const bool UseActiveProcessDashboard = false;
    public const bool UseNextActionCards = false;
    public const bool UseLinkedWorkflowNavigation = false;
    public const bool UsePlayerCharacterHub = false;
    public const bool UseAdminGmConsole = false;
    public const bool UseFunctionalRequestsUi = false;
    public const bool UseFunctionalProjectsUi = false;
    public const bool UseFunctionalCharacterCard = false;
    public const bool UseFunctionalInventoryUi = false;
    public const bool UseFunctionalKnowledgeResearchUi = false;
    public const bool UseFunctionalCraftingUi = false;
    public const bool UseFunctionalEngineeringUi = false;
    public const bool UseFunctionalProductionUi = false;
    public const bool UseFunctionalManufacturingUi = false;
    public const bool UseFunctionalAssetsUi = false;
    public const bool UseFunctionalDevelopmentUi = false;
    public const bool UseUiEmptyLoadingErrorStates = false;
    public const bool UseUiReadableLabels = false;
    public const bool UseDebugUiIsolation = false;
}

public static class EngineeringPlatformKindIds
{
    public const string GroundVehicle = "ground_vehicle";
    public const string Aircraft = "aircraft";
    public const string Spacecraft = "spacecraft";
    public const string Watercraft = "watercraft";
    public const string Walker = "walker";
    public const string Drone = "drone";
    public const string Building = "building";
    public const string Custom = "custom";
}

public static class EngineeringSizeClassIds
{
    public const string Tiny = "tiny";
    public const string Small = "small";
    public const string Medium = "medium";
    public const string Large = "large";
    public const string Huge = "huge";
    public const string Capital = "capital";
    public const string Custom = "custom";
}

public static class EngineeringModuleCategoryIds
{
    public const string Frame = "frame";
    public const string Engine = "engine";
    public const string PowerCore = "power_core";
    public const string Armor = "armor";
    public const string Cargo = "cargo";
    public const string Crew = "crew";
    public const string Sensor = "sensor";
    public const string Weapon = "weapon";
    public const string Medical = "medical";
    public const string Utility = "utility";
    public const string Mobility = "mobility";
    public const string Shield = "shield";
    public const string Custom = "custom";
}

public static class EngineeringModuleSlotTypeIds
{
    public const string Internal = "internal";
    public const string External = "external";
    public const string Hardpoint = "hardpoint";
    public const string Crew = "crew";
    public const string Cargo = "cargo";
    public const string Power = "power";
    public const string Custom = "custom";
}

public static class EngineeringCompatibilityRuleTypeIds
{
    public const string Allowed = "allowed";
    public const string Required = "required";
    public const string Forbidden = "forbidden";
    public const string Warning = "warning";
}

public static class EngineeringDesignStatusIds
{
    public const string Draft = "draft";
    public const string Submitted = "submitted";
    public const string GmReview = "gm_review";
    public const string Approved = "approved";
    public const string Active = "active";
    public const string AwaitingAcceptance = "awaiting_acceptance";
    public const string Completed = "completed";
    public const string Failed = "failed";
    public const string Cancelled = "cancelled";
    public const string Archived = "archived";
}

public static class EngineeringValidationStatusIds
{
    public const string NotChecked = "not_checked";
    public const string Valid = "valid";
    public const string Warnings = "warnings";
    public const string GmReview = "gm_review";
    public const string Blocked = "blocked";
}

public static class EngineeringValidationSeverityIds
{
    public const string Info = "info";
    public const string Warning = "warning";
    public const string GmReview = "gm_review";
    public const string HardBlock = "hard_block";
}

public static class EngineeringBlueprintStatusIds
{
    public const string Draft = "draft";
    public const string Prepared = "prepared";
    public const string Accepted = "accepted";
    public const string Created = "created";
    public const string Archived = "archived";
}

public static class ProductionFacilityCategoryIds
{
    public const string Workshop = "workshop";
    public const string Laboratory = "laboratory";
    public const string Forge = "forge";
    public const string AlchemyLab = "alchemy_lab";
    public const string EngineeringWorkshop = "engineering_workshop";
    public const string VehicleGarage = "vehicle_garage";
    public const string SmallShipyard = "small_shipyard";
    public const string Shipyard = "shipyard";
    public const string Drydock = "drydock";
    public const string OrbitalDock = "orbital_dock";
    public const string SpaceShipyard = "space_shipyard";
    public const string AssemblyLine = "assembly_line";
    public const string Factory = "factory";
    public const string MilitaryFactory = "military_factory";
    public const string ResearchFactory = "research_factory";
    public const string Custom = "custom";
}

public static class ProductionFacilityTypeIds
{
    public const string SmallPrivate = "small_private";
    public const string Guild = "guild";
    public const string StateOwned = "state_owned";
    public const string Corporate = "corporate";
    public const string Military = "military";
    public const string BlackMarketFuture = "black_market_future";
    public const string Mobile = "mobile";
    public const string Temporary = "temporary";
    public const string Custom = "custom";
}

public static class ProductionDomainIds
{
    public const string Crafting = "crafting";
    public const string EngineeringDesignSupport = "engineering_design_support";
    public const string ComponentManufacturing = "component_manufacturing";
    public const string VehicleManufacturing = "vehicle_manufacturing";
    public const string Shipbuilding = "shipbuilding";
    public const string SpaceshipConstruction = "spaceship_construction";
    public const string Repair = "repair";
    public const string Modification = "modification";
    public const string Prototype = "prototype";
    public const string BatchProduction = "batch_production";
    public const string Custom = "custom";
}

public static class ProductionFacilityStatusIds
{
    public const string Planned = "planned";
    public const string Active = "active";
    public const string Overloaded = "overloaded";
    public const string Maintenance = "maintenance";
    public const string Damaged = "damaged";
    public const string Inactive = "inactive";
    public const string Closed = "closed";
    public const string Hidden = "hidden";
    public const string Archived = "archived";
}

public static class ProductionMaintenanceStatusIds
{
    public const string Normal = "normal";
    public const string NeedsService = "needs_service";
    public const string Maintenance = "maintenance";
    public const string Damaged = "damaged";
    public const string Critical = "critical";
}

public static class ProductionResourceStatusIds
{
    public const string Normal = "normal";
    public const string Limited = "limited";
    public const string Shortage = "shortage";
    public const string Unavailable = "unavailable";
}

public static class FactoryValidationStatusIds
{
    public const string NotChecked = "not_checked";
    public const string Valid = "valid";
    public const string Warnings = "warnings";
    public const string GmReview = "gm_review";
    public const string Blocked = "blocked";
}

public static class FactoryQuoteStatusIds
{
    public const string Draft = "draft";
    public const string Generated = "generated";
    public const string Offered = "offered";
    public const string Accepted = "accepted";
    public const string Rejected = "rejected";
    public const string Expired = "expired";
    public const string ConvertedToOrder = "converted_to_order";
    public const string Archived = "archived";
}

public static class FactoryOrderStatusIds
{
    public const string Draft = "draft";
    public const string Submitted = "submitted";
    public const string Approved = "approved";
    public const string Scheduled = "scheduled";
    public const string WaitingManufacturing = "waiting_manufacturing";
    public const string Cancelled = "cancelled";
    public const string Archived = "archived";
}

public static class FactoryOrderSourceTypeIds
{
    public const string Blueprint = "blueprint";
    public const string Preset = "preset";
    public const string Custom = "custom";
}

public static class ManufacturingTypeIds
{
    public const string VehicleBuild = "vehicle_build";
    public const string ShipBuild = "ship_build";
    public const string SpaceshipBuild = "spaceship_build";
    public const string ComponentBuild = "component_build";
    public const string PrototypeBuild = "prototype_build";
    public const string BatchProduction = "batch_production";
    public const string Repair = "repair";
    public const string Modification = "modification";
    public const string Custom = "custom";
}

public static class ManufacturingOrderKindIds
{
    public const string PresetProduction = "preset_production";
    public const string CustomBlueprintProduction = "custom_blueprint_production";
    public const string PrototypeBuild = "prototype_build";
    public const string ComponentBatch = "component_batch";
    public const string Repair = "repair";
    public const string Modification = "modification";
    public const string Custom = "custom";
}

public static class ManufacturingStatusIds
{
    public const string Draft = "draft";
    public const string Planning = "planning";
    public const string WaitingResources = "waiting_resources";
    public const string WaitingPayment = "waiting_payment";
    public const string ReadyToStart = "ready_to_start";
    public const string Active = "active";
    public const string Paused = "paused";
    public const string Blocked = "blocked";
    public const string Testing = "testing";
    public const string Rework = "rework";
    public const string AwaitingAcceptance = "awaiting_acceptance";
    public const string Accepted = "accepted";
    public const string AssetCreated = "asset_created";
    public const string Delivered = "delivered";
    public const string Commissioned = "commissioned";
    public const string Completed = "completed";
    public const string Failed = "failed";
    public const string Cancelled = "cancelled";
    public const string Archived = "archived";
}

public static class ManufacturingResourceStatusIds
{
    public const string NotRequired = "not_required";
    public const string Planned = "planned";
    public const string PartiallyReserved = "partially_reserved";
    public const string Reserved = "reserved";
    public const string PartiallyConsumed = "partially_consumed";
    public const string Consumed = "consumed";
    public const string Released = "released";
    public const string Blocked = "blocked";
}

public static class ManufacturingPaymentStatusIds
{
    public const string NotRequired = "not_required";
    public const string Planned = "planned";
    public const string WaitingDeposit = "waiting_deposit";
    public const string PartiallyPaid = "partially_paid";
    public const string Paid = "paid";
    public const string WaivedByGm = "waived_by_gm";
    public const string Refunded = "refunded";
    public const string Blocked = "blocked";
}

public static class ManufacturingTestingStatusIds
{
    public const string NotRequired = "not_required";
    public const string Planned = "planned";
    public const string Active = "active";
    public const string Passed = "passed";
    public const string PassedWithIssues = "passed_with_issues";
    public const string Failed = "failed";
    public const string WaivedByGm = "waived_by_gm";
}

public static class ManufacturingAcceptanceStatusIds
{
    public const string NotReady = "not_ready";
    public const string ReadyForReview = "ready_for_review";
    public const string Accepted = "accepted";
    public const string Rejected = "rejected";
    public const string AcceptedWithDefects = "accepted_with_defects";
    public const string WaivedByGm = "waived_by_gm";
}

public static class ManufacturingAssetCreationStatusIds
{
    public const string NotReady = "not_ready";
    public const string Ready = "ready";
    public const string Created = "created";
    public const string Failed = "failed";
    public const string Blocked = "blocked";
}

public static class ManufacturingStageTypeIds
{
    public const string Planning = "planning";
    public const string ResourcePreparation = "resource_preparation";
    public const string Fabrication = "fabrication";
    public const string Assembly = "assembly";
    public const string Integration = "integration";
    public const string Testing = "testing";
    public const string Rework = "rework";
    public const string Acceptance = "acceptance";
    public const string Delivery = "delivery";
    public const string Commissioning = "commissioning";
    public const string Custom = "custom";
}

public static class ManufacturingStageStatusIds
{
    public const string Planned = "planned";
    public const string WaitingResources = "waiting_resources";
    public const string Ready = "ready";
    public const string Active = "active";
    public const string Paused = "paused";
    public const string Blocked = "blocked";
    public const string Completed = "completed";
    public const string Failed = "failed";
    public const string Rework = "rework";
    public const string Cancelled = "cancelled";
}

public static class ManufacturingReservationStatusIds
{
    public const string Reserved = "reserved";
    public const string PartiallyConsumed = "partially_consumed";
    public const string Consumed = "consumed";
    public const string Released = "released";
    public const string Cancelled = "cancelled";
}

public static class ManufacturingPaymentKindIds
{
    public const string Deposit = "deposit";
    public const string Milestone = "milestone";
    public const string Final = "final";
    public const string Correction = "correction";
    public const string Refund = "refund";
    public const string Custom = "custom";
}

public static class ManufacturingTestResultIds
{
    public const string Planned = "planned";
    public const string Passed = "passed";
    public const string PassedWithIssues = "passed_with_issues";
    public const string Failed = "failed";
    public const string WaivedByGm = "waived_by_gm";
}

public static class ManufacturingDefectStatusIds
{
    public const string Open = "open";
    public const string ReworkRequired = "rework_required";
    public const string Resolved = "resolved";
    public const string AcceptedAsIs = "accepted_as_is";
    public const string WaivedByGm = "waived_by_gm";
}

public static class ManufacturedAssetStatusIds
{
    public const string Draft = "draft";
    public const string Created = "created";
    public const string Transferred = "transferred";
    public const string Commissioned = "commissioned";
    public const string Archived = "archived";
}

public static class ProductionQueueSlotStatusIds
{
    public const string Reserved = "reserved";
    public const string Scheduled = "scheduled";
    public const string Released = "released";
    public const string Cancelled = "cancelled";
}

public static class CraftingRecipeCategoryIds
{
    public const string Consumable = "consumable";
    public const string Weapon = "weapon";
    public const string Armor = "armor";
    public const string Tool = "tool";
    public const string Equipment = "equipment";
    public const string Ammunition = "ammunition";
    public const string MagicalItem = "magical_item";
    public const string AlchemicalItem = "alchemical_item";
    public const string Component = "component";
    public const string MaterialProcessing = "material_processing";
    public const string Repair = "repair";
    public const string Modification = "modification";
    public const string Document = "document";
    public const string RitualComponent = "ritual_component";
    public const string Medicine = "medicine";
    public const string Food = "food";
    public const string Custom = "custom";
}

public static class CraftingRecipeTypeIds
{
    public const string Standard = "standard";
    public const string Discovered = "discovered";
    public const string ResearchUnlocked = "research_unlocked";
    public const string GmDefined = "gm_defined";
    public const string FactionSecret = "faction_secret";
    public const string Experimental = "experimental";
    public const string Custom = "custom";
}

public static class CraftingOutputTypeIds
{
    public const string InventoryItem = "inventory_item";
    public const string EquipmentItem = "equipment_item";
    public const string Material = "material";
    public const string Component = "component";
    public const string Ammo = "ammo";
    public const string Document = "document";
    public const string RecipeReference = "recipe_reference";
    public const string BlueprintReference = "blueprint_reference";
    public const string ProjectResult = "project_result";
    public const string Custom = "custom";
}

public static class CraftingIngredientTypeIds
{
    public const string Item = "item";
    public const string Material = "material";
    public const string Component = "component";
    public const string Reagent = "reagent";
    public const string Fuel = "fuel";
    public const string Ammo = "ammo";
    public const string MagicCrystal = "magic_crystal";
    public const string Document = "document";
    public const string Sample = "sample";
    public const string Catalyst = "catalyst";
    public const string Currency = "currency";
    public const string Custom = "custom";
}

public static class CraftingProjectStatusIds
{
    public const string Draft = "draft";
    public const string Submitted = "submitted";
    public const string GmReview = "gm_review";
    public const string Approved = "approved";
    public const string WaitingResources = "waiting_resources";
    public const string Active = "active";
    public const string AwaitingAcceptance = "awaiting_acceptance";
    public const string Completed = "completed";
    public const string Failed = "failed";
    public const string Cancelled = "cancelled";
    public const string Archived = "archived";
}

public static class CraftingReservationStatusIds
{
    public const string Reserved = "reserved";
    public const string Released = "released";
    public const string Consumed = "consumed";
    public const string Cancelled = "cancelled";
}

public static class CraftingResultStatusIds
{
    public const string Draft = "draft";
    public const string Prepared = "prepared";
    public const string Accepted = "accepted";
    public const string Created = "created";
    public const string Rejected = "rejected";
}

public static class ProjectTypeIds
{
    public const string Research = "research";
    public const string Crafting = "crafting";
    public const string EngineeringDesign = "engineering_design";
    public const string Manufacturing = "manufacturing";
    public const string FactoryOrder = "factory_order";
    public const string Construction = "construction";
    public const string Repair = "repair";
    public const string Modification = "modification";
    public const string ReverseEngineering = "reverse_engineering";
    public const string ProductionBatch = "production_batch";
    public const string CustomProposal = "custom_proposal";
    public const string Generic = "generic";
}

public static class ProjectStatusIds
{
    public const string Draft = "draft";
    public const string Submitted = "submitted";
    public const string InReview = "in_review";
    public const string Approved = "approved";
    public const string Preparation = "preparation";
    public const string WaitingResources = "waiting_resources";
    public const string Active = "active";
    public const string Paused = "paused";
    public const string Blocked = "blocked";
    public const string Testing = "testing";
    public const string AwaitingAcceptance = "awaiting_acceptance";
    public const string Completed = "completed";
    public const string Failed = "failed";
    public const string Cancelled = "cancelled";
    public const string Archived = "archived";
}

public static class ProjectApprovalStatusIds
{
    public const string NotRequired = "not_required";
    public const string Draft = "draft";
    public const string PendingGmReview = "pending_gm_review";
    public const string ChangesRequested = "changes_requested";
    public const string Approved = "approved";
    public const string Rejected = "rejected";
    public const string Revoked = "revoked";
    public const string Superseded = "superseded";
}

public static class ProjectProgressModeIds
{
    public const string Manual = "manual";
    public const string WorkPoints = "work_points";
    public const string StageBased = "stage_based";
    public const string CalendarDuration = "calendar_duration";
    public const string Hybrid = "hybrid";
}

public static class ProjectResultStatusIds
{
    public const string None = "none";
    public const string Expected = "expected";
    public const string ReadyForAcceptance = "ready_for_acceptance";
    public const string Accepted = "accepted";
    public const string Applied = "applied";
    public const string Rejected = "rejected";
    public const string Failed = "failed";
}

public static class ProjectResultApplicationModeIds
{
    public const string None = "none";
    public const string GmManual = "gm_manual";
    public const string CreateKnowledgeLater = "create_knowledge_later";
    public const string CreateRecipeLater = "create_recipe_later";
    public const string CreateItemLater = "create_item_later";
    public const string CreateAssetLater = "create_asset_later";
    public const string CreateBlueprintLater = "create_blueprint_later";
    public const string CreateProjectLater = "create_project_later";
    public const string CustomLater = "custom_later";
}

public static class ProjectStageTypeIds
{
    public const string Concept = "concept";
    public const string GmReview = "gm_review";
    public const string Preparation = "preparation";
    public const string Research = "research";
    public const string Design = "design";
    public const string ResourceGathering = "resource_gathering";
    public const string ResourceReservation = "resource_reservation";
    public const string Crafting = "crafting";
    public const string Construction = "construction";
    public const string Manufacturing = "manufacturing";
    public const string Prototype = "prototype";
    public const string Testing = "testing";
    public const string Revision = "revision";
    public const string Acceptance = "acceptance";
    public const string Delivery = "delivery";
    public const string OperationStart = "operation_start";
    public const string Custom = "custom";
}

public static class ProjectStageStatusIds
{
    public const string Locked = "locked";
    public const string Available = "available";
    public const string Active = "active";
    public const string Completed = "completed";
    public const string Skipped = "skipped";
    public const string Failed = "failed";
    public const string Blocked = "blocked";
    public const string Cancelled = "cancelled";
}

public static class ProjectParticipantEntityTypeIds
{
    public const string PlayerCharacter = "player_character";
    public const string Npc = "npc";
    public const string Companion = "companion";
    public const string CharacterGroup = "character_group";
    public const string Organization = "organization";
    public const string Facility = "facility";
    public const string Specialist = "specialist";
    public const string Custom = "custom";
}

public static class ProjectParticipantRoleIds
{
    public const string ProjectOwner = "project_owner";
    public const string Requester = "requester";
    public const string LeadResearcher = "lead_researcher";
    public const string LeadCrafter = "lead_crafter";
    public const string LeadEngineer = "lead_engineer";
    public const string Worker = "worker";
    public const string Assistant = "assistant";
    public const string Consultant = "consultant";
    public const string Sponsor = "sponsor";
    public const string Supplier = "supplier";
    public const string Inspector = "inspector";
    public const string GmReviewer = "gm_reviewer";
    public const string Custom = "custom";
}

public static class ProjectContributionModeIds
{
    public const string ActiveWork = "active_work";
    public const string PassiveSupport = "passive_support";
    public const string Funding = "funding";
    public const string Supervision = "supervision";
    public const string KnowledgeSource = "knowledge_source";
    public const string EquipmentProvider = "equipment_provider";
    public const string FacilityProvider = "facility_provider";
    public const string LegalCover = "legal_cover";
    public const string Custom = "custom";
}

public static class ProjectRequirementStatusIds
{
    public const string Open = "open";
    public const string Satisfied = "satisfied";
    public const string Waived = "waived";
    public const string Blocked = "blocked";
}

public static class ProjectResourceRequirementStatusIds
{
    public const string Needed = "needed";
    public const string Reserved = "reserved";
    public const string Provided = "provided";
    public const string ConsumedManually = "consumed_manually";
    public const string Waived = "waived";
}

public static class ProjectVisibilityModeIds
{
    public const string GmOnly = "gm_only";
    public const string PlayerVisible = "player_visible";
    public const string Party = "party";
    public const string OwnerOnly = "owner_only";
    public const string Hidden = "hidden";
}

public static class ProjectLinkTypeIds
{
    public const string PlayerRequest = "player_request";
    public const string Character = "character";
    public const string Companion = "companion";
    public const string Organization = "organization";
    public const string Faction = "faction";
    public const string Location = "location";
    public const string WorldCalendarEvent = "world_calendar_event";
    public const string RealScheduleEvent = "real_schedule_event";
    public const string InventoryItem = "inventory_item";
    public const string Knowledge = "knowledge";
    public const string Blueprint = "blueprint";
    public const string SceneMap = "scene_map";
    public const string WorldMap = "world_map";
    public const string Custom = "custom";
}

public static class KnowledgeTypeIds
{
    public const string Fact = "fact";
    public const string Rumor = "rumor";
    public const string Theory = "theory";
    public const string Method = "method";
    public const string Technology = "technology";
    public const string Recipe = "recipe";
    public const string Blueprint = "blueprint";
    public const string Ritual = "ritual";
    public const string Doctrine = "doctrine";
    public const string LanguageKnowledge = "language_knowledge";
    public const string LocationKnowledge = "location_knowledge";
    public const string FactionKnowledge = "faction_knowledge";
    public const string CreatureKnowledge = "creature_knowledge";
    public const string AnomalyKnowledge = "anomaly_knowledge";
    public const string MagicKnowledge = "magic_knowledge";
    public const string EngineeringKnowledge = "engineering_knowledge";
    public const string LegalKnowledge = "legal_knowledge";
    public const string Custom = "custom";
}

public static class KnowledgeDomainIds
{
    public const string Person = "person";
    public const string Creature = "creature";
    public const string Faction = "faction";
    public const string Country = "country";
    public const string City = "city";
    public const string Location = "location";
    public const string Region = "region";
    public const string Technology = "technology";
    public const string Magic = "magic";
    public const string Item = "item";
    public const string Recipe = "recipe";
    public const string Blueprint = "blueprint";
    public const string Anomaly = "anomaly";
    public const string Event = "event";
    public const string Language = "language";
    public const string Law = "law";
    public const string Market = "market";
    public const string Organization = "organization";
    public const string Doctrine = "doctrine";
    public const string Ritual = "ritual";
    public const string Map = "map";
    public const string Custom = "custom";
}

public static class KnowledgeEntityTypeIds
{
    public const string Character = "character";
    public const string Companion = "companion";
    public const string Npc = "npc";
    public const string Group = "group";
    public const string Organization = "organization";
    public const string Faction = "faction";
    public const string Custom = "custom";
}

public static class KnowledgeLevelIds
{
    public const string Unknown = "unknown";
    public const string Rumor = "rumor";
    public const string Partial = "partial";
    public const string False = "false";
    public const string Outdated = "outdated";
    public const string Official = "official";
    public const string Truth = "truth";
    public const string KnownWithoutUnderstanding = "known_without_understanding";
    public const string Applied = "applied";
}

public static class KnowledgeTruthRelationIds
{
    public const string Unknown = "unknown";
    public const string Accurate = "accurate";
    public const string Partial = "partial";
    public const string False = "false";
    public const string Outdated = "outdated";
    public const string OfficialVersion = "official_version";
    public const string GmTruth = "gm_truth";
}

public static class KnowledgeVisibilityRuleIds
{
    public const string GmOnly = "gm_only";
    public const string PlayerVisible = "player_visible";
    public const string RevealManually = "reveal_manually";
    public const string OwnerOnly = "owner_only";
    public const string Hidden = "hidden";
}

public static class KnowledgeSourceTypeIds
{
    public const string Observation = "observation";
    public const string Book = "book";
    public const string Mentor = "mentor";
    public const string Research = "research";
    public const string Rumor = "rumor";
    public const string Artifact = "artifact";
    public const string Experiment = "experiment";
    public const string OfficialRecord = "official_record";
    public const string Custom = "custom";
}

public static class AppliedKnowledgeTypeIds
{
    public const string Technology = "technology";
    public const string Method = "method";
    public const string Recipe = "recipe";
    public const string Blueprint = "blueprint";
    public const string Ritual = "ritual";
    public const string Doctrine = "doctrine";
    public const string ProductionProcess = "production_process";
    public const string Custom = "custom";
}

public static class ResearchTypeIds
{
    public const string Investigation = "investigation";
    public const string Experiment = "experiment";
    public const string ReverseEngineering = "reverse_engineering";
    public const string Invention = "invention";
    public const string Adaptation = "adaptation";
    public const string FieldStudy = "field_study";
    public const string DoctrineDevelopment = "doctrine_development";
    public const string RitualStudy = "ritual_study";
    public const string Custom = "custom";
}

public static class ResearchResultStatusIds
{
    public const string Draft = "draft";
    public const string Prepared = "prepared";
    public const string Accepted = "accepted";
    public const string Rejected = "rejected";
    public const string Applied = "applied";
}

public static class ResearchResultTypeIds
{
    public const string KnowledgeGrant = "knowledge_grant";
    public const string AppliedKnowledgeUnlock = "applied_knowledge_unlock";
    public const string RecipeReference = "recipe_reference";
    public const string BlueprintReference = "blueprint_reference";
    public const string ResearchNote = "research_note";
    public const string FutureCraftingBoundary = "future_crafting_boundary";
    public const string FutureEngineeringBoundary = "future_engineering_boundary";
    public const string Custom = "custom";
}

public static class EventJournalEntryTypeIds
{
    public const string Automatic = "automatic";
    public const string Manual = "manual";
    public const string Correction = "correction";
    public const string Annotation = "annotation";
    public const string System = "system";
}

public static class EventJournalCategoryIds
{
    public const string Session = "session";
    public const string Character = "character";
    public const string Ownership = "ownership";
    public const string Group = "group";
    public const string Request = "request";
    public const string Combat = "combat";
    public const string Map = "map";
    public const string WorldCalendar = "world_calendar";
    public const string RealSchedule = "real_schedule";
    public const string GMNote = "gm_note";
    public const string Inventory = "inventory";
    public const string System = "system";
    public const string Custom = "custom";
}

public static class EventJournalSeverityIds
{
    public const string Information = "information";
    public const string Notice = "notice";
    public const string Important = "important";
    public const string Warning = "warning";
    public const string Critical = "critical";
}

public static class EventJournalVisibilityModeIds
{
    public const string GMOnly = "gm_only";
    public const string GMTeam = "gm_team";
    public const string PlayerVisible = "player_visible";
    public const string SuperAdminOnly = "superadmin_only";
}

public static class EventJournalEntityTypeIds
{
    public const string CurrentSession = "current_session";
    public const string Session = "session";
    public const string Character = "character";
    public const string Npc = "npc";
    public const string Companion = "companion";
    public const string CharacterGroup = "character_group";
    public const string PlayerRequest = "player_request";
    public const string WorldCalendarEvent = "world_calendar_event";
    public const string RealScheduleEvent = "real_schedule_event";
    public const string SceneMap = "scene_map";
    public const string WorldMap = "world_map";
    public const string Room = "room";
    public const string MapMarker = "map_marker";
    public const string CombatEncounter = "combat_encounter";
    public const string Location = "location";
    public const string Country = "country";
    public const string Region = "region";
    public const string Faction = "faction";
    public const string Organization = "organization";
    public const string GMNote = "gm_note";
    public const string Custom = "custom";
}

public static class EventJournalLinkRoleIds
{
    public const string Actor = "actor";
    public const string Subject = "subject";
    public const string Source = "source";
    public const string Target = "target";
    public const string Related = "related";
    public const string Location = "location";
    public const string Result = "result";
    public const string CorrectionOf = "correction_of";
    public const string Custom = "custom";
}

public static class GMNoteTypeIds
{
    public const string Quick = "quick";
    public const string Preparation = "preparation";
    public const string Session = "session";
    public const string Character = "character";
    public const string Npc = "npc";
    public const string Companion = "companion";
    public const string Group = "group";
    public const string Location = "location";
    public const string Map = "map";
    public const string Combat = "combat";
    public const string Request = "request";
    public const string Calendar = "calendar";
    public const string Schedule = "schedule";
    public const string Secret = "secret";
    public const string Idea = "idea";
    public const string Todo = "todo";
    public const string Custom = "custom";
}

public static class GMNoteVisibilityModeIds
{
    public const string AuthorOnly = "author_only";
    public const string GMTeam = "gm_team";
    public const string SuperAdminOnly = "superadmin_only";
}

public static class GMNoteEntityTypeIds
{
    public const string CurrentSession = "current_session";
    public const string Session = "session";
    public const string Character = "character";
    public const string Npc = "npc";
    public const string Companion = "companion";
    public const string CharacterGroup = "character_group";
    public const string PlayerRequest = "player_request";
    public const string WorldCalendarEvent = "world_calendar_event";
    public const string RealScheduleEvent = "real_schedule_event";
    public const string SceneMap = "scene_map";
    public const string WorldMap = "world_map";
    public const string Room = "room";
    public const string MapMarker = "map_marker";
    public const string CombatEncounter = "combat_encounter";
    public const string Location = "location";
    public const string Country = "country";
    public const string Region = "region";
    public const string Faction = "faction";
    public const string Organization = "organization";
    public const string Custom = "custom";
}

public static class GMNoteLinkRoleIds
{
    public const string Related = "related";
    public const string Subject = "subject";
    public const string Source = "source";
    public const string Target = "target";
    public const string PreparationFor = "preparation_for";
    public const string FollowUp = "follow_up";
    public const string Custom = "custom";
}

public static class GMNoteAuditActionIds
{
    public const string Created = "created";
    public const string Updated = "updated";
    public const string Moved = "moved";
    public const string Pinned = "pinned";
    public const string Unpinned = "unpinned";
    public const string Shared = "shared";
    public const string MadePrivate = "made_private";
    public const string Archived = "archived";
    public const string Restored = "restored";
    public const string LinkAdded = "link_added";
    public const string LinkRemoved = "link_removed";
}

public static class RealScheduleEventTypeIds
{
    public const string GameSession = "game_session";
    public const string CampaignSession = "campaign_session";
    public const string OneShot = "one_shot";
    public const string Preparation = "preparation";
    public const string Maintenance = "maintenance";
    public const string TechnicalWork = "technical_work";
    public const string Meeting = "meeting";
    public const string Announcement = "announcement";
    public const string Custom = "custom";
}

public static class RealScheduleEventStatusIds
{
    public const string Planned = "planned";
    public const string Confirmed = "confirmed";
    public const string Rescheduled = "rescheduled";
    public const string InProgress = "in_progress";
    public const string Completed = "completed";
    public const string Cancelled = "cancelled";
    public const string Archived = "archived";
}

public static class RealScheduleParticipantRoleIds
{
    public const string Gm = "gm";
    public const string Player = "player";
    public const string Observer = "observer";
    public const string Assistant = "assistant";
    public const string Organizer = "organizer";
    public const string Custom = "custom";
}

public static class RealScheduleParticipantResponseIds
{
    public const string Invited = "invited";
    public const string Accepted = "accepted";
    public const string Tentative = "tentative";
    public const string Declined = "declined";
    public const string Unknown = "unknown";
}

public static class CurrentSessionStatusIds
{
    public const string Planned = "planned";
    public const string Active = "active";
    public const string Paused = "paused";
    public const string Completed = "completed";
    public const string Cancelled = "cancelled";
    public const string Archived = "archived";
}

public static class CurrentSessionModeIds
{
    public const string Preparation = "preparation";
    public const string NormalScene = "normal_scene";
    public const string Combat = "combat";
    public const string Travel = "travel";
    public const string ShortRest = "short_rest";
    public const string LongRest = "long_rest";
    public const string Downtime = "downtime";
    public const string Maintenance = "maintenance";
    public const string Custom = "custom";
}

public static class CharacterGroupTypeIds
{
    public const string Party = "party";
    public const string NpcGroup = "npc_group";
    public const string CompanionGroup = "companion_group";
    public const string EnemyGroup = "enemy_group";
    public const string NeutralGroup = "neutral_group";
    public const string EscortGroup = "escort_group";
    public const string TemporaryGroup = "temporary_group";
    public const string Custom = "custom";
}

public static class CharacterGroupStatusIds
{
    public const string Draft = "draft";
    public const string Active = "active";
    public const string Inactive = "inactive";
    public const string Disbanded = "disbanded";
    public const string Archived = "archived";
}

public static class CharacterGroupEntityTypeIds
{
    public const string PlayerCharacter = "player_character";
    public const string Npc = "npc";
    public const string Companion = "companion";
    public const string TemporaryAlly = "temporary_ally";
    public const string Enemy = "enemy";
    public const string Neutral = "neutral";
    public const string Custom = "custom";
}

public static class CharacterGroupCharacterRoleIds
{
    public const string PlayerCharacter = "PlayerCharacter";
    public const string NPC = "NPC";
    public const string Companion = "Companion";
    public const string TemporaryAlly = "TemporaryAlly";
    public const string Enemy = "Enemy";
    public const string Inactive = "Inactive";
    public const string Custom = "Custom";
}

public static class CharacterGroupRoleInGroupIds
{
    public const string Leader = "leader";
    public const string Member = "member";
    public const string Companion = "companion";
    public const string Guide = "guide";
    public const string Guard = "guard";
    public const string Prisoner = "prisoner";
    public const string Escort = "escort";
    public const string Enemy = "enemy";
    public const string Observer = "observer";
    public const string Custom = "custom";
}

public static class CharacterOwnershipRoleIds
{
    public const string PlayerCharacter = "PlayerCharacter";
    public const string NPC = "NPC";
    public const string Companion = "Companion";
    public const string TemporaryAlly = "TemporaryAlly";
    public const string Enemy = "Enemy";
    public const string Neutral = "Neutral";
    public const string Inactive = "Inactive";
    public const string Custom = "Custom";
}

public static class CharacterOwnershipAssignmentStatusIds
{
    public const string Unassigned = "unassigned";
    public const string Assigned = "assigned";
    public const string Transferred = "transferred";
    public const string Converted = "converted";
    public const string Archived = "archived";
}

public static class CharacterKindIds
{
    public const string PlayerCharacter = "player_character";
    public const string Npc = "npc";
    public const string Companion = "companion";
    public const string TemporaryAlly = "temporary_ally";
    public const string Enemy = "enemy";
    public const string Neutral = "neutral";
    public const string Custom = "custom";
}

public static class CharacterStatusIds
{
    public const string Active = "active";
    public const string Inactive = "inactive";
    public const string Archived = "archived";
}

public static class CharacterOwnershipAuditActionIds
{
    public const string AssignOwner = "assign_owner";
    public const string ReassignOwner = "reassign_owner";
    public const string ClearOwner = "clear_owner";
    public const string SetController = "set_controller";
    public const string ClearController = "clear_controller";
    public const string ConvertNpcToPc = "convert_npc_to_pc";
    public const string ConvertCompanionToPc = "convert_companion_to_pc";
    public const string ConvertPcToNpc = "convert_pc_to_npc";
    public const string ConvertPcToCompanion = "convert_pc_to_companion";
    public const string ConvertToCustomRole = "convert_to_custom_role";
    public const string VisibilityChanged = "visibility_changed";
    public const string Archived = "archived";
}

public sealed class CurrentSessionState : EntityBase
{
    public string CampaignId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = CurrentSessionStatusIds.Planned;
    public string Mode { get; set; } = CurrentSessionModeIds.Preparation;
    public string CurrentSceneId { get; set; } = string.Empty;
    public string CurrentSceneName { get; set; } = string.Empty;
    public string ActiveSceneMapId { get; set; } = string.Empty;
    public string ActiveSceneMapName { get; set; } = string.Empty;
    public string ActiveWorldMapId { get; set; } = string.Empty;
    public string ActiveWorldMapName { get; set; } = string.Empty;
    public string ActiveRoomId { get; set; } = string.Empty;
    public string ActiveRoomName { get; set; } = string.Empty;
    public string ActiveCombatEncounterId { get; set; } = string.Empty;
    public string ActiveCombatName { get; set; } = string.Empty;
    public string ActiveGroupId { get; set; } = string.Empty;
    public string CurrentWorldDate { get; set; } = string.Empty;
    public DateTime? CurrentRealStartUtc { get; set; }
    public DateTime? CurrentRealEndUtc { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? PausedAtUtc { get; set; }
    public DateTime? EndedAtUtc { get; set; }
    public string GMUserId { get; set; } = string.Empty;
    public string GMDisplayName { get; set; } = string.Empty;
    public string VisibilityMode { get; set; } = MapVisibilityModes.Party;
    public bool IsPlayerVisible { get; set; } = true;
    public bool IsArchived { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
    public string UpdatedByUserId { get; set; } = string.Empty;
    public string PublicNotes { get; set; } = string.Empty;
    public string GMNotes { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new List<string>();
    public Dictionary<string, object> ExtraData { get; set; } = new Dictionary<string, object>();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new Dictionary<string, object>();
}

public sealed class CharacterGroupState : EntityBase
{
    public string CampaignId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string GroupType { get; set; } = CharacterGroupTypeIds.Party;
    public string Status { get; set; } = CharacterGroupStatusIds.Draft;
    public bool IsActive { get; set; }
    public string VisibilityMode { get; set; } = MapVisibilityModes.Party;
    public bool IsPlayerVisible { get; set; } = true;
    public bool IsArchived { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
    public string UpdatedByUserId { get; set; } = string.Empty;
    public string GMNotes { get; set; } = string.Empty;
    public string PublicNotes { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new List<string>();
    public Dictionary<string, object> ExtraData { get; set; } = new Dictionary<string, object>();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new Dictionary<string, object>();
}

public sealed class CharacterGroupMemberState : EntityBase
{
    public string GroupId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string EntityType { get; set; } = CharacterGroupEntityTypeIds.PlayerCharacter;
    public string EntityId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string RoleInGroup { get; set; } = CharacterGroupRoleInGroupIds.Member;
    public string CharacterRole { get; set; } = CharacterGroupCharacterRoleIds.PlayerCharacter;
    public string OwnerUserId { get; set; } = string.Empty;
    public string ControlledByUserId { get; set; } = string.Empty;
    public bool IsLeader { get; set; }
    public bool IsPlayerVisible { get; set; } = true;
    public string VisibilityMode { get; set; } = MapVisibilityModes.Party;
    public DateTime JoinedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? RemovedAtUtc { get; set; }
    public string AddedByUserId { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public string PublicNotes { get; set; } = string.Empty;
    public string GMNotes { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new List<string>();
    public Dictionary<string, object> ExtraData { get; set; } = new Dictionary<string, object>();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new Dictionary<string, object>();
}

public sealed class CharacterOwnershipState : EntityBase
{
    public string CampaignId { get; set; } = string.Empty;
    public string CharacterId { get; set; } = string.Empty;
    public string CharacterDisplayName { get; set; } = string.Empty;
    public string CharacterRole { get; set; } = CharacterOwnershipRoleIds.PlayerCharacter;
    public string OwnerUserId { get; set; } = string.Empty;
    public string OwnerDisplayName { get; set; } = string.Empty;
    public string ControlledByUserId { get; set; } = string.Empty;
    public string ControlledByDisplayName { get; set; } = string.Empty;
    public string PreviousOwnerUserId { get; set; } = string.Empty;
    public string PreviousCharacterRole { get; set; } = string.Empty;
    public string CharacterKind { get; set; } = CharacterKindIds.PlayerCharacter;
    public string CharacterStatus { get; set; } = CharacterStatusIds.Active;
    public bool IsActive { get; set; } = true;
    public bool IsArchived { get; set; }
    public bool IsPlayerVisible { get; set; } = true;
    public string VisibilityMode { get; set; } = MapVisibilityModes.Party;
    public string AssignmentStatus { get; set; } = CharacterOwnershipAssignmentStatusIds.Unassigned;
    public DateTime? AssignedAtUtc { get; set; }
    public string AssignedByUserId { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string UpdatedByUserId { get; set; } = string.Empty;
    public string PublicNotes { get; set; } = string.Empty;
    public string GMNotes { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new List<string>();
    public Dictionary<string, object> ExtraData { get; set; } = new Dictionary<string, object>();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new Dictionary<string, object>();
}

public sealed class CharacterOwnershipAuditEntry : EntityBase
{
    public string CampaignId { get; set; } = string.Empty;
    public string CharacterId { get; set; } = string.Empty;
    public string ActionType { get; set; } = string.Empty;
    public string FromRole { get; set; } = string.Empty;
    public string ToRole { get; set; } = string.Empty;
    public string FromOwnerUserId { get; set; } = string.Empty;
    public string ToOwnerUserId { get; set; } = string.Empty;
    public string FromControlledByUserId { get; set; } = string.Empty;
    public string ToControlledByUserId { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string PerformedByUserId { get; set; } = string.Empty;
    public DateTime PerformedAtUtc { get; set; } = DateTime.UtcNow;
    public string Summary { get; set; } = string.Empty;
    public string PublicSummary { get; set; } = string.Empty;
    public string GMNotes { get; set; } = string.Empty;
    public Dictionary<string, object> ExtraData { get; set; } = new Dictionary<string, object>();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new Dictionary<string, object>();
}

public sealed class ProjectBaseState : EntityBase
{
    public string CampaignId { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string ActiveGroupId { get; set; } = string.Empty;
    public string ProjectType { get; set; } = ProjectTypeIds.Generic;
    public string Name { get; set; } = string.Empty;
    public string PublicSummary { get; set; } = string.Empty;
    public string GMSummary { get; set; } = string.Empty;
    public string Status { get; set; } = ProjectStatusIds.Draft;
    public string ApprovalStatus { get; set; } = ProjectApprovalStatusIds.Draft;
    public string ProgressMode { get; set; } = ProjectProgressModeIds.Manual;
    public string ResultStatus { get; set; } = ProjectResultStatusIds.None;
    public string ResultApplicationMode { get; set; } = ProjectResultApplicationModeIds.None;
    public int ProgressPercent { get; set; }
    public int WorkPointsDone { get; set; }
    public int WorkPointsRequired { get; set; }
    public string CurrentStageId { get; set; } = string.Empty;
    public string CurrentStageName { get; set; } = string.Empty;
    public string OwnerUserId { get; set; } = string.Empty;
    public string OwnerDisplayName { get; set; } = string.Empty;
    public string OwnerCharacterId { get; set; } = string.Empty;
    public string CreatedByUserId { get; set; } = string.Empty;
    public string UpdatedByUserId { get; set; } = string.Empty;
    public string AssignedGmUserId { get; set; } = string.Empty;
    public string VisibilityMode { get; set; } = ProjectVisibilityModeIds.PlayerVisible;
    public bool IsPlayerVisible { get; set; } = true;
    public bool IsArchived { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? SubmittedAtUtc { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string PublicNotes { get; set; } = string.Empty;
    public string GMNotes { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new List<string>();
    public Dictionary<string, object> ProposalPayload { get; set; } = new Dictionary<string, object>();
    public Dictionary<string, object> ExpectedResultSummary { get; set; } = new Dictionary<string, object>();
    public Dictionary<string, object> ExtraData { get; set; } = new Dictionary<string, object>();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new Dictionary<string, object>();
}

public sealed class ProjectStageState : EntityBase
{
    public string ProjectId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string StageType { get; set; } = ProjectStageTypeIds.Custom;
    public string Name { get; set; } = string.Empty;
    public string PublicSummary { get; set; } = string.Empty;
    public string GMSummary { get; set; } = string.Empty;
    public string Status { get; set; } = ProjectStageStatusIds.Available;
    public int SortOrder { get; set; }
    public int ProgressPercent { get; set; }
    public int WorkPointsDone { get; set; }
    public int WorkPointsRequired { get; set; }
    public bool IsPlayerVisible { get; set; } = true;
    public string VisibilityMode { get; set; } = ProjectVisibilityModeIds.PlayerVisible;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string UpdatedByUserId { get; set; } = string.Empty;
    public string PublicNotes { get; set; } = string.Empty;
    public string GMNotes { get; set; } = string.Empty;
    public Dictionary<string, object> ExtraData { get; set; } = new Dictionary<string, object>();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new Dictionary<string, object>();
}

public sealed class ProjectParticipantState : EntityBase
{
    public string ProjectId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string EntityType { get; set; } = ProjectParticipantEntityTypeIds.Custom;
    public string EntityId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ParticipantRole { get; set; } = ProjectParticipantRoleIds.Custom;
    public string ContributionMode { get; set; } = ProjectContributionModeIds.Custom;
    public string OwnerUserId { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public bool IsPlayerVisible { get; set; } = true;
    public string VisibilityMode { get; set; } = ProjectVisibilityModeIds.PlayerVisible;
    public DateTime AddedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? RemovedAtUtc { get; set; }
    public string AddedByUserId { get; set; } = string.Empty;
    public string PublicNotes { get; set; } = string.Empty;
    public string GMNotes { get; set; } = string.Empty;
    public Dictionary<string, object> ExtraData { get; set; } = new Dictionary<string, object>();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new Dictionary<string, object>();
}

public sealed class ProjectRequirementState : EntityBase
{
    public string ProjectId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string RequirementType { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string PublicSummary { get; set; } = string.Empty;
    public string GMSummary { get; set; } = string.Empty;
    public string Status { get; set; } = ProjectRequirementStatusIds.Open;
    public bool IsRequired { get; set; } = true;
    public bool IsPlayerVisible { get; set; } = true;
    public string VisibilityMode { get; set; } = ProjectVisibilityModeIds.PlayerVisible;
    public string VerifiedByUserId { get; set; } = string.Empty;
    public DateTime? VerifiedAtUtc { get; set; }
    public string PublicNotes { get; set; } = string.Empty;
    public string GMNotes { get; set; } = string.Empty;
    public Dictionary<string, object> ExtraData { get; set; } = new Dictionary<string, object>();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new Dictionary<string, object>();
}

public sealed class ProjectResourceRequirementState : EntityBase
{
    public string ProjectId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public string ResourceId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public decimal QuantityRequired { get; set; }
    public decimal QuantityReserved { get; set; }
    public decimal QuantityProvided { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string Status { get; set; } = ProjectResourceRequirementStatusIds.Needed;
    public bool IsReservationOnly { get; set; } = true;
    public bool IsPlayerVisible { get; set; } = true;
    public string VisibilityMode { get; set; } = ProjectVisibilityModeIds.PlayerVisible;
    public string UpdatedByUserId { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string PublicNotes { get; set; } = string.Empty;
    public string GMNotes { get; set; } = string.Empty;
    public Dictionary<string, object> ExtraData { get; set; } = new Dictionary<string, object>();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new Dictionary<string, object>();
}

public sealed class ProjectProgressEntryState : EntityBase
{
    public string ProjectId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string StageId { get; set; } = string.Empty;
    public string EntryType { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string PublicSummary { get; set; } = string.Empty;
    public int ProgressDeltaPercent { get; set; }
    public int WorkPointsDelta { get; set; }
    public int ResultProgressPercent { get; set; }
    public bool IsPlayerVisible { get; set; } = true;
    public string VisibilityMode { get; set; } = ProjectVisibilityModeIds.PlayerVisible;
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string GMNotes { get; set; } = string.Empty;
    public Dictionary<string, object> ExtraData { get; set; } = new Dictionary<string, object>();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new Dictionary<string, object>();
}

public sealed class ProjectApprovalState : EntityBase
{
    public string ProjectId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string ApprovalType { get; set; } = string.Empty;
    public string Status { get; set; } = ProjectApprovalStatusIds.PendingGmReview;
    public string RequestedByUserId { get; set; } = string.Empty;
    public DateTime RequestedAtUtc { get; set; } = DateTime.UtcNow;
    public string ReviewedByUserId { get; set; } = string.Empty;
    public DateTime? ReviewedAtUtc { get; set; }
    public string PublicSummary { get; set; } = string.Empty;
    public string GMSummary { get; set; } = string.Empty;
    public string PublicNotes { get; set; } = string.Empty;
    public string GMNotes { get; set; } = string.Empty;
    public bool IsPlayerVisible { get; set; } = true;
    public string VisibilityMode { get; set; } = ProjectVisibilityModeIds.PlayerVisible;
    public Dictionary<string, object> ExtraData { get; set; } = new Dictionary<string, object>();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new Dictionary<string, object>();
}

public sealed class ProjectAuditEntryState : EntityBase
{
    public string ProjectId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string ActionType { get; set; } = string.Empty;
    public string ActorUserId { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string Summary { get; set; } = string.Empty;
    public string PublicSummary { get; set; } = string.Empty;
    public bool IsPlayerVisible { get; set; }
    public string VisibilityMode { get; set; } = ProjectVisibilityModeIds.GmOnly;
    public Dictionary<string, object> ExtraData { get; set; } = new Dictionary<string, object>();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new Dictionary<string, object>();
}

public sealed class ProjectEntityLinkState : EntityBase
{
    public string ProjectId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string LinkType { get; set; } = ProjectLinkTypeIds.Custom;
    public string EntityId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string LinkRole { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public bool IsPlayerVisible { get; set; } = true;
    public string VisibilityMode { get; set; } = ProjectVisibilityModeIds.PlayerVisible;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
    public string PublicNotes { get; set; } = string.Empty;
    public string GMNotes { get; set; } = string.Empty;
    public Dictionary<string, object> ExtraData { get; set; } = new Dictionary<string, object>();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new Dictionary<string, object>();
}

public sealed class ProjectProposalBoundaryState : EntityBase
{
    public string CampaignId { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string ProposalType { get; set; } = ProjectTypeIds.Generic;
    public string Title { get; set; } = string.Empty;
    public string PublicSummary { get; set; } = string.Empty;
    public string Status { get; set; } = ProjectApprovalStatusIds.Draft;
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object> DraftPayload { get; set; } = new Dictionary<string, object>();
    public Dictionary<string, object> ExtraData { get; set; } = new Dictionary<string, object>();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new Dictionary<string, object>();
}

public sealed class KnowledgeDefinition : EntityBase
{
    public string KnowledgeId { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Subcategory { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public string PublicDescription { get; set; } = string.Empty;
    public string GMDescription { get; set; } = string.Empty;
    public string TruthDescription { get; set; } = string.Empty;
    public string OfficialDescription { get; set; } = string.Empty;
    public List<string> FalseDescriptionTemplates { get; set; } = new List<string>();
    public string KnowledgeType { get; set; } = KnowledgeTypeIds.Fact;
    public string KnowledgeDomain { get; set; } = KnowledgeDomainIds.Custom;
    public string DefaultVisibilityRule { get; set; } = KnowledgeVisibilityRuleIds.RevealManually;
    public bool IsAppliedKnowledge { get; set; }
    public bool IsSecret { get; set; }
    public bool IsPlayerDiscoverable { get; set; } = true;
    public bool IsArchived { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
    public string UpdatedByUserId { get; set; } = string.Empty;
    public string SourceDocument { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new List<string>();
    public Dictionary<string, object> ExtraData { get; set; } = new Dictionary<string, object>();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new Dictionary<string, object>();
}

public sealed class EntityKnowledgeState : EntityBase
{
    public string CampaignId { get; set; } = string.Empty;
    public string KnowledgeDefinitionId { get; set; } = string.Empty;
    public string KnowledgeId { get; set; } = string.Empty;
    public string EntityType { get; set; } = KnowledgeEntityTypeIds.Character;
    public string EntityId { get; set; } = string.Empty;
    public string EntityDisplayName { get; set; } = string.Empty;
    public string OwnerUserId { get; set; } = string.Empty;
    public string Level { get; set; } = KnowledgeLevelIds.Partial;
    public string TruthRelation { get; set; } = KnowledgeTruthRelationIds.Unknown;
    public string PlayerSummary { get; set; } = string.Empty;
    public string GMNotes { get; set; } = string.Empty;
    public string FalseOrOutdatedSummary { get; set; } = string.Empty;
    public bool IsApplied { get; set; }
    public bool IsPlayerVisible { get; set; }
    public string VisibilityMode { get; set; } = ProjectVisibilityModeIds.GmOnly;
    public string SourceId { get; set; } = string.Empty;
    public string SourceLabel { get; set; } = string.Empty;
    public string GrantedByUserId { get; set; } = string.Empty;
    public DateTime GrantedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string UpdatedByUserId { get; set; } = string.Empty;
    public bool IsArchived { get; set; }
    public List<string> Tags { get; set; } = new List<string>();
    public Dictionary<string, object> ExtraData { get; set; } = new Dictionary<string, object>();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new Dictionary<string, object>();
}

public sealed class AppliedKnowledgeDefinition : EntityBase
{
    public string CampaignId { get; set; } = string.Empty;
    public string KnowledgeDefinitionId { get; set; } = string.Empty;
    public string AppliedType { get; set; } = AppliedKnowledgeTypeIds.Custom;
    public string Name { get; set; } = string.Empty;
    public string PublicDescription { get; set; } = string.Empty;
    public string GMDescription { get; set; } = string.Empty;
    public string RecipeReferenceId { get; set; } = string.Empty;
    public string BlueprintReferenceId { get; set; } = string.Empty;
    public string FutureSystemBoundary { get; set; } = string.Empty;
    public bool IsPlayerVisible { get; set; }
    public string VisibilityMode { get; set; } = ProjectVisibilityModeIds.GmOnly;
    public bool IsArchived { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
    public string UpdatedByUserId { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new List<string>();
    public Dictionary<string, object> ExtraData { get; set; } = new Dictionary<string, object>();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new Dictionary<string, object>();
}

public sealed class KnowledgeSourceState : EntityBase
{
    public string CampaignId { get; set; } = string.Empty;
    public string KnowledgeDefinitionId { get; set; } = string.Empty;
    public string EntityKnowledgeId { get; set; } = string.Empty;
    public string SourceType { get; set; } = KnowledgeSourceTypeIds.Custom;
    public string Title { get; set; } = string.Empty;
    public string PublicSummary { get; set; } = string.Empty;
    public string GMSummary { get; set; } = string.Empty;
    public string LinkedEntityType { get; set; } = string.Empty;
    public string LinkedEntityId { get; set; } = string.Empty;
    public bool IsPlayerVisible { get; set; }
    public string VisibilityMode { get; set; } = ProjectVisibilityModeIds.GmOnly;
    public bool IsArchived { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
    public Dictionary<string, object> ExtraData { get; set; } = new Dictionary<string, object>();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new Dictionary<string, object>();
}

public sealed class ResearchResultState : EntityBase
{
    public string CampaignId { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string ResultType { get; set; } = ResearchResultTypeIds.ResearchNote;
    public string Status { get; set; } = ResearchResultStatusIds.Draft;
    public string Title { get; set; } = string.Empty;
    public string PublicSummary { get; set; } = string.Empty;
    public string GMSummary { get; set; } = string.Empty;
    public string KnowledgeDefinitionId { get; set; } = string.Empty;
    public string AppliedKnowledgeId { get; set; } = string.Empty;
    public string TargetEntityType { get; set; } = KnowledgeEntityTypeIds.Character;
    public string TargetEntityId { get; set; } = string.Empty;
    public bool IsPlayerVisible { get; set; }
    public string VisibilityMode { get; set; } = ProjectVisibilityModeIds.GmOnly;
    public string PreparedByUserId { get; set; } = string.Empty;
    public DateTime PreparedAtUtc { get; set; } = DateTime.UtcNow;
    public string ReviewedByUserId { get; set; } = string.Empty;
    public DateTime? ReviewedAtUtc { get; set; }
    public string AppliedByUserId { get; set; } = string.Empty;
    public DateTime? AppliedAtUtc { get; set; }
    public bool IsArchived { get; set; }
    public Dictionary<string, object> ResultPayload { get; set; } = new Dictionary<string, object>();
    public Dictionary<string, object> ExtraData { get; set; } = new Dictionary<string, object>();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new Dictionary<string, object>();
}

public sealed class CurrentSessionCreateRequest
{
    public string CampaignId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string GMUserId { get; set; } = string.Empty;
    public string GMDisplayName { get; set; } = string.Empty;
    public DateTime? CurrentRealStartUtc { get; set; }
    public string VisibilityMode { get; set; } = MapVisibilityModes.Party;
    public bool IsPlayerVisible { get; set; } = true;
}

public sealed class CurrentSessionUpdateRequest
{
    public string SessionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string VisibilityMode { get; set; } = string.Empty;
    public bool? IsPlayerVisible { get; set; }
    public string PublicNotes { get; set; } = string.Empty;
    public string GMNotes { get; set; } = string.Empty;
}

public sealed class CurrentSessionGetRequest
{
    public string CampaignId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
}

public sealed class CurrentSessionSetSceneRequest
{
    public string SessionId { get; set; } = string.Empty;
    public string CurrentSceneId { get; set; } = string.Empty;
    public string CurrentSceneName { get; set; } = string.Empty;
    public string ActiveRoomId { get; set; } = string.Empty;
}

public sealed class CurrentSessionSetModeRequest
{
    public string SessionId { get; set; } = string.Empty;
    public string Mode { get; set; } = CurrentSessionModeIds.NormalScene;
}

public sealed class CurrentSessionSetActiveSceneMapRequest
{
    public string SessionId { get; set; } = string.Empty;
    public string MapId { get; set; } = string.Empty;
}

public sealed class CurrentSessionSetActiveCombatRequest
{
    public string SessionId { get; set; } = string.Empty;
    public string CombatEncounterId { get; set; } = string.Empty;
}

public sealed class CurrentSessionSetNotesRequest
{
    public string SessionId { get; set; } = string.Empty;
    public string PublicNotes { get; set; } = string.Empty;
    public string GMNotes { get; set; } = string.Empty;
}

public sealed class PlayerCurrentSessionGetRequest
{
    public string CampaignId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string CharacterId { get; set; } = string.Empty;
}

public sealed class CharacterGroupCreateRequest
{
    public string CampaignId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string GroupType { get; set; } = CharacterGroupTypeIds.Party;
    public bool IsPlayerVisible { get; set; } = true;
    public string VisibilityMode { get; set; } = MapVisibilityModes.Party;
    public string PublicNotes { get; set; } = string.Empty;
    public string GMNotes { get; set; } = string.Empty;
}

public sealed class CharacterGroupUpdateRequest
{
    public string GroupId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string GroupType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool? IsPlayerVisible { get; set; }
    public string VisibilityMode { get; set; } = string.Empty;
    public string PublicNotes { get; set; } = string.Empty;
    public string GMNotes { get; set; } = string.Empty;
}

public sealed class CharacterGroupMemberAddRequest
{
    public string GroupId { get; set; } = string.Empty;
    public string EntityType { get; set; } = CharacterGroupEntityTypeIds.PlayerCharacter;
    public string EntityId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string RoleInGroup { get; set; } = CharacterGroupRoleInGroupIds.Member;
    public string CharacterRole { get; set; } = CharacterGroupCharacterRoleIds.PlayerCharacter;
    public string OwnerUserId { get; set; } = string.Empty;
    public string ControlledByUserId { get; set; } = string.Empty;
    public bool? IsLeader { get; set; }
    public bool? IsPlayerVisible { get; set; }
    public string PublicNotes { get; set; } = string.Empty;
    public string GMNotes { get; set; } = string.Empty;
}

public sealed class CharacterOwnershipGetRequest
{
    public string CharacterId { get; set; } = string.Empty;
}

public sealed class CharacterOwnershipListRequest
{
    public string CampaignId { get; set; } = string.Empty;
    public string CharacterRole { get; set; } = string.Empty;
    public string OwnerUserId { get; set; } = string.Empty;
    public bool IncludeUnassigned { get; set; }
    public bool IncludeArchived { get; set; }
}

public sealed class CharacterAssignOwnerRequest
{
    public string CharacterId { get; set; } = string.Empty;
    public string OwnerUserId { get; set; } = string.Empty;
    public string ControlledByUserId { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public bool? IsPlayerVisible { get; set; }
}

public sealed class CharacterReassignOwnerRequest
{
    public string CharacterId { get; set; } = string.Empty;
    public string NewOwnerUserId { get; set; } = string.Empty;
    public string NewControlledByUserId { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

public sealed class CharacterClearOwnerRequest
{
    public string CharacterId { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

public sealed class CharacterSetControllerRequest
{
    public string CharacterId { get; set; } = string.Empty;
    public string ControlledByUserId { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

public sealed class CharacterClearControllerRequest
{
    public string CharacterId { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

public sealed class CharacterSetRoleRequest
{
    public string CharacterId { get; set; } = string.Empty;
    public string CharacterRole { get; set; } = CharacterOwnershipRoleIds.PlayerCharacter;
    public string Reason { get; set; } = string.Empty;
}

public sealed class CharacterConvertToPlayerCharacterRequest
{
    public string CharacterId { get; set; } = string.Empty;
    public string OwnerUserId { get; set; } = string.Empty;
    public string ControlledByUserId { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public bool IsPlayerVisible { get; set; } = true;
}

public sealed class CharacterConvertToNpcRequest
{
    public string CharacterId { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public bool ClearOwner { get; set; } = true;
    public bool? IsPlayerVisible { get; set; }
}

public sealed class CharacterConvertToCompanionRequest
{
    public string CharacterId { get; set; } = string.Empty;
    public string OwnerUserId { get; set; } = string.Empty;
    public string ControlledByUserId { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public bool? IsPlayerVisible { get; set; }
}

public sealed class CharacterOwnershipSetVisibilityRequest
{
    public string CharacterId { get; set; } = string.Empty;
    public bool IsPlayerVisible { get; set; }
    public string VisibilityMode { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

public sealed class CharacterOwnershipAuditListRequest
{
    public string CampaignId { get; set; } = string.Empty;
    public string CharacterId { get; set; } = string.Empty;
    public string OwnerUserId { get; set; } = string.Empty;
    public string ActionType { get; set; } = string.Empty;
    public int? Limit { get; set; }
}

public sealed class PlayerAssignedCharactersRequest
{
    public string CampaignId { get; set; } = string.Empty;
    public bool IncludeCompanions { get; set; } = true;
}
