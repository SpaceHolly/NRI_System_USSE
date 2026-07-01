using System;
using System.Collections.Generic;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;
using Nri.Server.Logging;
using Nri.Server.Infrastructure.Mongo.Repositories;
using Nri.Shared.Configuration;
using Nri.Shared.Domain;

namespace Nri.Server.Infrastructure;

public interface IRepository<T> where T : EntityBase
{
    T? GetById(string id);
    IReadOnlyCollection<T> Find(FilterDefinition<T> filter);
    void Insert(T entity);
    void Replace(T entity);
}

public interface INriRepositoryFactory
{
    IRepository<UserAccount> Accounts { get; }
    IRepository<UserProfile> Profiles { get; }
    IRepository<Character> Characters { get; }
    IRepository<SessionUserState> Presence { get; }
    IRepository<CurrentSessionState> CurrentSessions { get; }
    IRepository<CharacterGroupState> CharacterGroups { get; }
    IRepository<CharacterGroupMemberState> CharacterGroupMembers { get; }
    IRepository<CharacterOwnershipState> CharacterOwnerships { get; }
    IRepository<CharacterOwnershipAuditEntry> CharacterOwnershipAudit { get; }
    IRepository<EntityLock> Locks { get; }
    IRepository<AuditLogEntry> AuditLogs { get; }
    IRepository<ActionRequest> ActionRequests { get; }
    IRepository<DiceRollRequest> DiceRequests { get; }
    IRepository<PlayerRequestState> PlayerRequests { get; }
    IRepository<PlayerRequestCommentState> PlayerRequestComments { get; }
    IRepository<WorldCalendarDefinition> WorldCalendarDefinitions { get; }
    IRepository<WorldCalendarSeasonDefinition> WorldCalendarSeasons { get; }
    IRepository<WorldCalendarMonthDefinition> WorldCalendarMonths { get; }
    IRepository<CampaignWorldTimeState> CampaignWorldTimes { get; }
    IRepository<WorldCalendarEventState> WorldCalendarEvents { get; }
    IRepository<WorldCalendarEventVersionState> WorldCalendarEventVersions { get; }
    IRepository<WorldCalendarHolidayDefinition> WorldCalendarHolidays { get; }
    IRepository<WorldCalendarReminderState> WorldCalendarReminders { get; }
    IRepository<RealScheduleEventState> RealScheduleEvents { get; }
    IRepository<RealScheduleParticipantState> RealScheduleParticipants { get; }
    IRepository<GMNoteState> GMNotes { get; }
    IRepository<GMNoteFolderState> GMNoteFolders { get; }
    IRepository<GMNoteEntityLinkState> GMNoteLinks { get; }
    IRepository<GMNoteAuditEntry> GMNoteAudit { get; }
    IRepository<EventJournalEntryState> EventJournalEntries { get; }
    IRepository<EventJournalEntityLinkState> EventJournalLinks { get; }
    IRepository<EventJournalAnnotationState> EventJournalAnnotations { get; }
    IRepository<EventJournalAuditEntry> EventJournalAudit { get; }
    IRepository<ChatMessage> ChatMessages { get; }
    IRepository<ChatReadState> ChatReadStates { get; }
    IRepository<SessionChatSettings> SessionChatSettings { get; }
    IRepository<ChatUserThrottleState> ChatThrottleStates { get; }
    IRepository<SessionAudioState> AudioStates { get; }
    IRepository<AudioTrackDefinition> AudioTracks { get; }
    IRepository<AudioClientSettingsState> AudioClientSettings { get; }
    IRepository<CombatState> Combats { get; }
    IRepository<CombatLogEntry> CombatLogs { get; }
    IRepository<ClassTreeDefinition> ClassTrees { get; }
    IRepository<SkillDefinitionRecord> SkillDefinitions { get; }
    IRepository<DefinitionVersion> DefinitionVersions { get; }
    IRepository<Note> Notes { get; }
    IRepository<ReferenceEntry> References { get; }
    IRepository<UpdateVersionInfo> UpdateVersions { get; }
    IRepository<BackupSnapshot> Backups { get; }
    IRepository<BackupRecordState> BackupRecords { get; }
    IRepository<BackupRestoreOperationState> BackupRestoreOperations { get; }
    IRepository<BackupMaintenanceState> BackupMaintenanceStates { get; }
    IRepository<FeatureFlagOverrideState> FeatureFlagOverrides { get; }
    IRepository<ProjectBaseState> Projects { get; }
    IRepository<ProjectStageState> ProjectStages { get; }
    IRepository<ProjectParticipantState> ProjectParticipants { get; }
    IRepository<ProjectRequirementState> ProjectRequirements { get; }
    IRepository<ProjectResourceRequirementState> ProjectResourceRequirements { get; }
    IRepository<ProjectProgressEntryState> ProjectProgressEntries { get; }
    IRepository<ProjectApprovalState> ProjectApprovals { get; }
    IRepository<ProjectAuditEntryState> ProjectAuditEntries { get; }
    IRepository<ProjectEntityLinkState> ProjectEntityLinks { get; }
    IRepository<ProjectProposalBoundaryState> ProjectProposals { get; }
    IRepository<KnowledgeDefinition> KnowledgeDefinitions { get; }
    IRepository<EntityKnowledgeState> EntityKnowledgeStates { get; }
    IRepository<AppliedKnowledgeDefinition> AppliedKnowledgeDefinitions { get; }
    IRepository<KnowledgeSourceState> KnowledgeSources { get; }
    IRepository<ResearchResultState> ResearchResults { get; }
    IRepository<ExperienceCoinLedgerEntry> ExperienceCoinLedger { get; }
    IRepository<CraftingRecipeDefinition> CraftingRecipes { get; }
    IRepository<RecipeIngredientRequirement> CraftingRecipeIngredients { get; }
    IRepository<RecipeToolRequirement> CraftingRecipeTools { get; }
    IRepository<RecipeFacilityRequirement> CraftingRecipeFacilities { get; }
    IRepository<RecipeKnowledgeRequirement> CraftingRecipeKnowledgeRequirements { get; }
    IRepository<CraftingProjectState> CraftingProjects { get; }
    IRepository<CraftingResourceReservationState> CraftingReservations { get; }
    IRepository<CraftingProjectItemResult> CraftingResults { get; }
    IRepository<EngineeringPlatformDefinition> EngineeringPlatforms { get; }
    IRepository<EngineeringPlatformSizeClassDefinition> EngineeringSizeClasses { get; }
    IRepository<EngineeringModuleDefinition> EngineeringModules { get; }
    IRepository<EngineeringModuleSlotRequirement> EngineeringModuleSlotRequirements { get; }
    IRepository<EngineeringModuleCompatibilityRule> EngineeringModuleCompatibilityRules { get; }
    IRepository<EngineeringPowerProfileDefinition> EngineeringPowerProfiles { get; }
    IRepository<EngineeringWeaponProfileDefinition> EngineeringWeaponProfiles { get; }
    IRepository<PresetVehicleDesignDefinition> EngineeringPresets { get; }
    IRepository<VehicleDesignDraft> EngineeringDesignDrafts { get; }
    IRepository<EngineeringDesignProjectState> EngineeringProjects { get; }
    IRepository<EngineeringDesignValidationResult> EngineeringValidationResults { get; }
    IRepository<EngineeringDesignCostEstimate> EngineeringCostEstimates { get; }
    IRepository<VehicleDesignBlueprint> EngineeringBlueprints { get; }
    IRepository<EngineeringBlueprintReference> EngineeringBlueprintReferences { get; }
    IRepository<ProductionFacilityDefinition> ProductionFacilityDefinitions { get; }
    IRepository<ProductionFacilityState> ProductionFacilities { get; }
    IRepository<ProductionFacilityCapabilityState> ProductionCapabilities { get; }
    IRepository<ProductionProcessDefinition> ProductionProcesses { get; }
    IRepository<ProductionFacilityCapacityState> ProductionCapacities { get; }
    IRepository<ProductionQueueSlotState> ProductionQueueSlots { get; }
    IRepository<FactoryQuoteState> FactoryQuotes { get; }
    IRepository<FactoryOrderState> FactoryOrders { get; }
    IRepository<FactoryOrderLineState> FactoryOrderLines { get; }
    IRepository<FactoryOrderTermState> FactoryOrderTerms { get; }
    IRepository<FactoryOrderPaymentPlanState> FactoryPaymentPlans { get; }
    IRepository<ManufacturingProjectState> ManufacturingProjects { get; }
    IRepository<ManufacturingStageState> ManufacturingStages { get; }
    IRepository<ManufacturingResourcePlanState> ManufacturingResourcePlans { get; }
    IRepository<ManufacturingResourceReservationState> ManufacturingResourceReservations { get; }
    IRepository<ManufacturingCostLedgerEntry> ManufacturingCostLedger { get; }
    IRepository<ManufacturingPaymentState> ManufacturingPayments { get; }
    IRepository<ManufacturingProgressEntry> ManufacturingProgressEntries { get; }
    IRepository<ManufacturingTestPlanState> ManufacturingTestPlans { get; }
    IRepository<ManufacturingTestResultState> ManufacturingTestResults { get; }
    IRepository<ManufacturingDefectState> ManufacturingDefects { get; }
    IRepository<ManufacturingAcceptanceState> ManufacturingAcceptances { get; }
    IRepository<ManufacturedAssetState> ManufacturedAssets { get; }
    IRepository<SyncEvent> SyncEvents { get; }
    IRepository<SyncCounter> SyncCounters { get; }
    IClassDefinitionRepository ClassDefinitions { get; }
    IRaceDefinitionRepository RaceDefinitions { get; }
    ISkillDefinitionRepository DefinitionSkills { get; }
    IFactionStateRepository FactionStates { get; }
    IOrganizationStateRepository OrganizationStates { get; }
    IMarketStateRepository MarketStates { get; }
    ILawStateRepository LawStates { get; }
    IRestrictionStateRepository RestrictionStates { get; }
    IAssetStateRepository AssetStates { get; }
    IEconomyScopeStateRepository EconomyScopeStates { get; }
    ICombatEncounterRepository CombatEncounters { get; }
    ICombatParticipantRepository CombatParticipants { get; }
    ICombatTurnRepository CombatTurns { get; }
    ICombatRoundRepository CombatRounds { get; }
    ICombatActionRepository CombatActions { get; }
    ICombatLogRepository CombatRuntimeLogs { get; }
    ICombatReplayEventRepository CombatReplayEvents { get; }
    IMapSpaceNodeRepository MapSpaceNodes { get; }
    IMapCanvasRepository MapCanvases { get; }
    IRoomInteriorRepository RoomInteriors { get; }
    IWorldMapStateRepository WorldMaps { get; }
    IMapMarkerRepository MapMarkers { get; }
    IMapMarkerBindingRepository MapMarkerBindings { get; }
    IWorldMapLayerRepository WorldMapLayers { get; }
    IWorldMapLegendRepository WorldMapLegends { get; }
    IMapFogLayerRepository MapFogLayers { get; }
    ISceneMapActiveLinkRepository SceneMapActiveLinks { get; }
}

public class MongoContext
{
    private static readonly object ConventionLock = new object();
    private static bool _serializationConventionsRegistered;

    public IMongoDatabase Database { get; }
    public IMongoCollection<UserAccount> Accounts { get; }
    public IMongoCollection<UserProfile> Profiles { get; }
    public IMongoCollection<Character> Characters { get; }
    public IMongoCollection<SessionUserState> Presence { get; }
    public IMongoCollection<CurrentSessionState> CurrentSessions { get; }
    public IMongoCollection<CharacterGroupState> CharacterGroups { get; }
    public IMongoCollection<CharacterGroupMemberState> CharacterGroupMembers { get; }
    public IMongoCollection<CharacterOwnershipState> CharacterOwnerships { get; }
    public IMongoCollection<CharacterOwnershipAuditEntry> CharacterOwnershipAudit { get; }
    public IMongoCollection<EntityLock> Locks { get; }
    public IMongoCollection<AuditLogEntry> AuditLogs { get; }
    public IMongoCollection<ActionRequest> ActionRequests { get; }
    public IMongoCollection<DiceRollRequest> DiceRequests { get; }
    public IMongoCollection<PlayerRequestState> PlayerRequests { get; }
    public IMongoCollection<PlayerRequestCommentState> PlayerRequestComments { get; }
    public IMongoCollection<WorldCalendarDefinition> WorldCalendarDefinitions { get; }
    public IMongoCollection<WorldCalendarSeasonDefinition> WorldCalendarSeasons { get; }
    public IMongoCollection<WorldCalendarMonthDefinition> WorldCalendarMonths { get; }
    public IMongoCollection<CampaignWorldTimeState> CampaignWorldTimes { get; }
    public IMongoCollection<WorldCalendarEventState> WorldCalendarEvents { get; }
    public IMongoCollection<WorldCalendarEventVersionState> WorldCalendarEventVersions { get; }
    public IMongoCollection<WorldCalendarHolidayDefinition> WorldCalendarHolidays { get; }
    public IMongoCollection<WorldCalendarReminderState> WorldCalendarReminders { get; }
    public IMongoCollection<RealScheduleEventState> RealScheduleEvents { get; }
    public IMongoCollection<RealScheduleParticipantState> RealScheduleParticipants { get; }
    public IMongoCollection<GMNoteState> GMNotes { get; }
    public IMongoCollection<GMNoteFolderState> GMNoteFolders { get; }
    public IMongoCollection<GMNoteEntityLinkState> GMNoteLinks { get; }
    public IMongoCollection<GMNoteAuditEntry> GMNoteAudit { get; }
    public IMongoCollection<EventJournalEntryState> EventJournalEntries { get; }
    public IMongoCollection<EventJournalEntityLinkState> EventJournalLinks { get; }
    public IMongoCollection<EventJournalAnnotationState> EventJournalAnnotations { get; }
    public IMongoCollection<EventJournalAuditEntry> EventJournalAudit { get; }
    public IMongoCollection<ChatMessage> ChatMessages { get; }
    public IMongoCollection<ChatReadState> ChatReadStates { get; }
    public IMongoCollection<SessionChatSettings> SessionChatSettings { get; }
    public IMongoCollection<ChatUserThrottleState> ChatThrottleStates { get; }
    public IMongoCollection<SessionAudioState> AudioStates { get; }
    public IMongoCollection<AudioTrackDefinition> AudioTracks { get; }
    public IMongoCollection<AudioClientSettingsState> AudioClientSettings { get; }
    public IMongoCollection<CombatState> Combats { get; }
    public IMongoCollection<CombatLogEntry> CombatLogs { get; }
    public IMongoCollection<ClassTreeDefinition> ClassTrees { get; }
    public IMongoCollection<SkillDefinitionRecord> SkillDefinitions { get; }
    public IMongoCollection<DefinitionVersion> DefinitionVersions { get; }
    public IMongoCollection<Note> Notes { get; }
    public IMongoCollection<ReferenceEntry> References { get; }
    public IMongoCollection<UpdateVersionInfo> UpdateVersions { get; }
    public IMongoCollection<BackupSnapshot> Backups { get; }
    public IMongoCollection<BackupRecordState> BackupRecords { get; }
    public IMongoCollection<BackupRestoreOperationState> BackupRestoreOperations { get; }
    public IMongoCollection<BackupMaintenanceState> BackupMaintenanceStates { get; }
    public IMongoCollection<FeatureFlagOverrideState> FeatureFlagOverrides { get; }
    public IMongoCollection<ProjectBaseState> Projects { get; }
    public IMongoCollection<ProjectStageState> ProjectStages { get; }
    public IMongoCollection<ProjectParticipantState> ProjectParticipants { get; }
    public IMongoCollection<ProjectRequirementState> ProjectRequirements { get; }
    public IMongoCollection<ProjectResourceRequirementState> ProjectResourceRequirements { get; }
    public IMongoCollection<ProjectProgressEntryState> ProjectProgressEntries { get; }
    public IMongoCollection<ProjectApprovalState> ProjectApprovals { get; }
    public IMongoCollection<ProjectAuditEntryState> ProjectAuditEntries { get; }
    public IMongoCollection<ProjectEntityLinkState> ProjectEntityLinks { get; }
    public IMongoCollection<ProjectProposalBoundaryState> ProjectProposals { get; }
    public IMongoCollection<KnowledgeDefinition> KnowledgeDefinitions { get; }
    public IMongoCollection<EntityKnowledgeState> EntityKnowledgeStates { get; }
    public IMongoCollection<AppliedKnowledgeDefinition> AppliedKnowledgeDefinitions { get; }
    public IMongoCollection<KnowledgeSourceState> KnowledgeSources { get; }
    public IMongoCollection<ResearchResultState> ResearchResults { get; }
    public IMongoCollection<ExperienceCoinLedgerEntry> ExperienceCoinLedger { get; }
    public IMongoCollection<CraftingRecipeDefinition> CraftingRecipes { get; }
    public IMongoCollection<RecipeIngredientRequirement> CraftingRecipeIngredients { get; }
    public IMongoCollection<RecipeToolRequirement> CraftingRecipeTools { get; }
    public IMongoCollection<RecipeFacilityRequirement> CraftingRecipeFacilities { get; }
    public IMongoCollection<RecipeKnowledgeRequirement> CraftingRecipeKnowledgeRequirements { get; }
    public IMongoCollection<CraftingProjectState> CraftingProjects { get; }
    public IMongoCollection<CraftingResourceReservationState> CraftingReservations { get; }
    public IMongoCollection<CraftingProjectItemResult> CraftingResults { get; }
    public IMongoCollection<EngineeringPlatformDefinition> EngineeringPlatforms { get; }
    public IMongoCollection<EngineeringPlatformSizeClassDefinition> EngineeringSizeClasses { get; }
    public IMongoCollection<EngineeringModuleDefinition> EngineeringModules { get; }
    public IMongoCollection<EngineeringModuleSlotRequirement> EngineeringModuleSlotRequirements { get; }
    public IMongoCollection<EngineeringModuleCompatibilityRule> EngineeringModuleCompatibilityRules { get; }
    public IMongoCollection<EngineeringPowerProfileDefinition> EngineeringPowerProfiles { get; }
    public IMongoCollection<EngineeringWeaponProfileDefinition> EngineeringWeaponProfiles { get; }
    public IMongoCollection<PresetVehicleDesignDefinition> EngineeringPresets { get; }
    public IMongoCollection<VehicleDesignDraft> EngineeringDesignDrafts { get; }
    public IMongoCollection<EngineeringDesignProjectState> EngineeringProjects { get; }
    public IMongoCollection<EngineeringDesignValidationResult> EngineeringValidationResults { get; }
    public IMongoCollection<EngineeringDesignCostEstimate> EngineeringCostEstimates { get; }
    public IMongoCollection<VehicleDesignBlueprint> EngineeringBlueprints { get; }
    public IMongoCollection<EngineeringBlueprintReference> EngineeringBlueprintReferences { get; }
    public IMongoCollection<ProductionFacilityDefinition> ProductionFacilityDefinitions { get; }
    public IMongoCollection<ProductionFacilityState> ProductionFacilities { get; }
    public IMongoCollection<ProductionFacilityCapabilityState> ProductionCapabilities { get; }
    public IMongoCollection<ProductionProcessDefinition> ProductionProcesses { get; }
    public IMongoCollection<ProductionFacilityCapacityState> ProductionCapacities { get; }
    public IMongoCollection<ProductionQueueSlotState> ProductionQueueSlots { get; }
    public IMongoCollection<FactoryQuoteState> FactoryQuotes { get; }
    public IMongoCollection<FactoryOrderState> FactoryOrders { get; }
    public IMongoCollection<FactoryOrderLineState> FactoryOrderLines { get; }
    public IMongoCollection<FactoryOrderTermState> FactoryOrderTerms { get; }
    public IMongoCollection<FactoryOrderPaymentPlanState> FactoryPaymentPlans { get; }
    public IMongoCollection<ManufacturingProjectState> ManufacturingProjects { get; }
    public IMongoCollection<ManufacturingStageState> ManufacturingStages { get; }
    public IMongoCollection<ManufacturingResourcePlanState> ManufacturingResourcePlans { get; }
    public IMongoCollection<ManufacturingResourceReservationState> ManufacturingResourceReservations { get; }
    public IMongoCollection<ManufacturingCostLedgerEntry> ManufacturingCostLedger { get; }
    public IMongoCollection<ManufacturingPaymentState> ManufacturingPayments { get; }
    public IMongoCollection<ManufacturingProgressEntry> ManufacturingProgressEntries { get; }
    public IMongoCollection<ManufacturingTestPlanState> ManufacturingTestPlans { get; }
    public IMongoCollection<ManufacturingTestResultState> ManufacturingTestResults { get; }
    public IMongoCollection<ManufacturingDefectState> ManufacturingDefects { get; }
    public IMongoCollection<ManufacturingAcceptanceState> ManufacturingAcceptances { get; }
    public IMongoCollection<ManufacturedAssetState> ManufacturedAssets { get; }
    public IMongoCollection<CharacterModuleStateDocument> CharacterModuleStates { get; }
    public IMongoCollection<CharacterAttributeProfileDocument> CharacterAttributeProfiles { get; }
    public IMongoCollection<CharacterSubAttributeProfileDocument> CharacterSubAttributeProfiles { get; }
    public IMongoCollection<CharacterSkillProfileDocument> CharacterSkillProfiles { get; }
    public IMongoCollection<CharacterDevelopmentProfileDocument> CharacterDevelopmentProfiles { get; }
    public IMongoCollection<CharacterWalletProfileDocument> CharacterWalletProfiles { get; }
    public IMongoCollection<CharacterInventoryProfileDocument> CharacterInventoryProfiles { get; }
    public IMongoCollection<CharacterReputationProfileDocument> CharacterReputationProfiles { get; }
    public IMongoCollection<CharacterHoldingsProfileDocument> CharacterHoldingsProfiles { get; }
    public IMongoCollection<CharacterCompanionProfileDocument> CharacterCompanionProfiles { get; }
    public IMongoCollection<CharacterRaceOrSpeciesProfileDocument> CharacterRaceOrSpeciesProfiles { get; }
    public IMongoCollection<CharacterBodyProfileDocument> CharacterBodyProfiles { get; }
    public IMongoCollection<CharacterKnowledgeProfileDocument> CharacterKnowledgeProfiles { get; }
    public IMongoCollection<CharacterConditionProfileDocument> CharacterConditionProfiles { get; }
    public IMongoCollection<SyncEvent> SyncEvents { get; }
    public IMongoCollection<SyncCounter> SyncCounters { get; }
    public IMongoCollection<ClassDefinition> ClassDefinitions { get; }
    public IMongoCollection<RaceDefinition> RaceDefinitions { get; }
    public IMongoCollection<SkillDefinition> DefinitionSkills { get; }
    public IMongoCollection<UnifiedDefinitionDocument> UnifiedDefinitions { get; }
    public IMongoCollection<FactionState> FactionStates { get; }
    public IMongoCollection<OrganizationState> OrganizationStates { get; }
    public IMongoCollection<MarketState> MarketStates { get; }
    public IMongoCollection<LawState> LawStates { get; }
    public IMongoCollection<RestrictionState> RestrictionStates { get; }
    public IMongoCollection<AssetState> AssetStates { get; }
    public IMongoCollection<EconomyScopeState> EconomyScopeStates { get; }
    public IMongoCollection<CombatEncounterState> CombatEncounters { get; }
    public IMongoCollection<CombatParticipantState> CombatParticipants { get; }
    public IMongoCollection<CombatTurnState> CombatTurns { get; }
    public IMongoCollection<CombatRoundRuntimeState> CombatRounds { get; }
    public IMongoCollection<CombatActionState> CombatActions { get; }
    public IMongoCollection<CombatRuntimeLogEntry> CombatRuntimeLogs { get; }
    public IMongoCollection<CombatReplayEvent> CombatReplayEvents { get; }
    public IMongoCollection<MapSpaceNodeState> MapSpaceNodes { get; }
    public IMongoCollection<MapCanvasState> MapCanvases { get; }
    public IMongoCollection<RoomInteriorState> RoomInteriors { get; }
    public IMongoCollection<WorldMapState> WorldMaps { get; }
    public IMongoCollection<MapMarkerState> MapMarkers { get; }
    public IMongoCollection<MapMarkerBindingState> MapMarkerBindings { get; }
    public IMongoCollection<WorldMapLayerState> WorldMapLayers { get; }
    public IMongoCollection<WorldMapLegendState> WorldMapLegends { get; }
    public IMongoCollection<FogOfWarState> MapFogLayers { get; }
    public IMongoCollection<SceneMapActiveLinkState> SceneMapActiveLinks { get; }
    public IMongoCollection<JurisdictionDefinition> LegalJurisdictions { get; }
    public IMongoCollection<LegalProfileState> LegalProfiles { get; }
    public IMongoCollection<LegalRuleDefinition> LegalRules { get; }
    public IMongoCollection<LegalSubjectClassifier> LegalSubjectClassifiers { get; }
    public IMongoCollection<LegalRestrictionState> LegalRestrictions { get; }
    public IMongoCollection<LegalRequirementState> LegalRequirements { get; }
    public IMongoCollection<LicenseDefinition> LegalLicenseDefinitions { get; }
    public IMongoCollection<EntityLicenseState> LegalEntityLicenses { get; }
    public IMongoCollection<LicenseApplicationState> LegalLicenseApplications { get; }
    public IMongoCollection<PermitState> LegalPermits { get; }
    public IMongoCollection<LegalCheckRecordState> LegalCheckRecords { get; }
    public IMongoCollection<EnforcementRiskProfile> LegalEnforcementRiskProfiles { get; }
    public IMongoCollection<DeJureDeFactoLawState> LegalDeJureDeFactoStates { get; }
    public IMongoCollection<ProductionLegalityState> LegalProductionLegalityStates { get; }
    public IMongoCollection<PlayerProposalDraftState> PlayerProposalDrafts { get; }
    public IMongoCollection<PlayerProposalFieldState> PlayerProposalFields { get; }
    public IMongoCollection<PlayerProposalValidationResult> PlayerProposalValidations { get; }
    public IMongoCollection<PlayerProposalReviewState> PlayerProposalReviews { get; }
    public IMongoCollection<PlayerProposalConversionState> PlayerProposalConversions { get; }
    public IMongoCollection<ProposalTemplateDefinition> ProposalTemplateDefinitions { get; }
    public IMongoCollection<PlayerProposalAttachmentLinkState> ProposalAttachmentLinks { get; }

    private static void EnsureSerializationConventions()
    {
        lock (ConventionLock)
        {
            if (_serializationConventionsRegistered) return;

            var pack = new ConventionPack
            {
                new IgnoreExtraElementsConvention(true)
            };
            ConventionRegistry.Register(
                "nri-server-ignore-extra-elements",
                pack,
                type => type.Namespace != null && type.Namespace.StartsWith("Nri.Shared.Domain", StringComparison.Ordinal));

            _serializationConventionsRegistered = true;
        }
    }

    public MongoContext(ServerConfig config, IServerLogger logger)
    {
        EnsureSerializationConventions();

        var client = new MongoClient(config.Mongo.ConnectionString);
        var db = client.GetDatabase(config.Mongo.DatabaseName);
        Database = db;

        Console.WriteLine(db.Client.Cluster.Description);

        Accounts = db.GetCollection<UserAccount>("accounts");
        Profiles = db.GetCollection<UserProfile>("profiles");
        Characters = db.GetCollection<Character>("characters");
        Presence = db.GetCollection<SessionUserState>("sessions");
        CurrentSessions = db.GetCollection<CurrentSessionState>("current_sessions");
        CharacterGroups = db.GetCollection<CharacterGroupState>("character_groups");
        CharacterGroupMembers = db.GetCollection<CharacterGroupMemberState>("character_group_members");
        CharacterOwnerships = db.GetCollection<CharacterOwnershipState>("character_ownerships");
        CharacterOwnershipAudit = db.GetCollection<CharacterOwnershipAuditEntry>("character_ownership_audit");
        Locks = db.GetCollection<EntityLock>("locks");
        AuditLogs = db.GetCollection<AuditLogEntry>("audit_logs");
        ActionRequests = db.GetCollection<ActionRequest>("action_requests");
        DiceRequests = db.GetCollection<DiceRollRequest>("dice_requests");
        PlayerRequests = db.GetCollection<PlayerRequestState>("player_requests");
        PlayerRequestComments = db.GetCollection<PlayerRequestCommentState>("player_request_comments");
        WorldCalendarDefinitions = db.GetCollection<WorldCalendarDefinition>("world_calendar_definitions");
        WorldCalendarSeasons = db.GetCollection<WorldCalendarSeasonDefinition>("world_calendar_seasons");
        WorldCalendarMonths = db.GetCollection<WorldCalendarMonthDefinition>("world_calendar_months");
        CampaignWorldTimes = db.GetCollection<CampaignWorldTimeState>("campaign_world_times");
        WorldCalendarEvents = db.GetCollection<WorldCalendarEventState>("world_calendar_events");
        WorldCalendarEventVersions = db.GetCollection<WorldCalendarEventVersionState>("world_calendar_event_versions");
        WorldCalendarHolidays = db.GetCollection<WorldCalendarHolidayDefinition>("world_calendar_holidays");
        WorldCalendarReminders = db.GetCollection<WorldCalendarReminderState>("world_calendar_reminders");
        RealScheduleEvents = db.GetCollection<RealScheduleEventState>("real_schedule_events");
        RealScheduleParticipants = db.GetCollection<RealScheduleParticipantState>("real_schedule_participants");
        GMNotes = db.GetCollection<GMNoteState>("gm_notes");
        GMNoteFolders = db.GetCollection<GMNoteFolderState>("gm_note_folders");
        GMNoteLinks = db.GetCollection<GMNoteEntityLinkState>("gm_note_links");
        GMNoteAudit = db.GetCollection<GMNoteAuditEntry>("gm_note_audit");
        EventJournalEntries = db.GetCollection<EventJournalEntryState>("event_journal_entries");
        EventJournalLinks = db.GetCollection<EventJournalEntityLinkState>("event_journal_links");
        EventJournalAnnotations = db.GetCollection<EventJournalAnnotationState>("event_journal_annotations");
        EventJournalAudit = db.GetCollection<EventJournalAuditEntry>("event_journal_audit");
        ChatMessages = db.GetCollection<ChatMessage>("chat_messages");
        ChatReadStates = db.GetCollection<ChatReadState>("chat_read_states");
        SessionChatSettings = db.GetCollection<SessionChatSettings>("session_chat_settings");
        ChatThrottleStates = db.GetCollection<ChatUserThrottleState>("chat_throttle_states");
        AudioStates = db.GetCollection<SessionAudioState>("audio_states");
        AudioTracks = db.GetCollection<AudioTrackDefinition>("audio_tracks");
        AudioClientSettings = db.GetCollection<AudioClientSettingsState>("audio_client_settings");
        Combats = db.GetCollection<CombatState>("combat_states");
        CombatLogs = db.GetCollection<CombatLogEntry>("combat_logs");
        ClassTrees = db.GetCollection<ClassTreeDefinition>("class_tree_definitions");
        SkillDefinitions = db.GetCollection<SkillDefinitionRecord>("skill_definitions");
        DefinitionVersions = db.GetCollection<DefinitionVersion>("definition_versions");
        Notes = db.GetCollection<Note>("notes");
        References = db.GetCollection<ReferenceEntry>("references");
        UpdateVersions = db.GetCollection<UpdateVersionInfo>("update_versions");
        Backups = db.GetCollection<BackupSnapshot>("backups");
        BackupRecords = db.GetCollection<BackupRecordState>("backup_records");
        BackupRestoreOperations = db.GetCollection<BackupRestoreOperationState>("backup_restore_operations");
        BackupMaintenanceStates = db.GetCollection<BackupMaintenanceState>("backup_maintenance_states");
        FeatureFlagOverrides = db.GetCollection<FeatureFlagOverrideState>("feature_flag_overrides");
        Projects = db.GetCollection<ProjectBaseState>("project_base_states");
        ProjectStages = db.GetCollection<ProjectStageState>("project_stages");
        ProjectParticipants = db.GetCollection<ProjectParticipantState>("project_participants");
        ProjectRequirements = db.GetCollection<ProjectRequirementState>("project_requirements");
        ProjectResourceRequirements = db.GetCollection<ProjectResourceRequirementState>("project_resource_requirements");
        ProjectProgressEntries = db.GetCollection<ProjectProgressEntryState>("project_progress_entries");
        ProjectApprovals = db.GetCollection<ProjectApprovalState>("project_approvals");
        ProjectAuditEntries = db.GetCollection<ProjectAuditEntryState>("project_audit_entries");
        ProjectEntityLinks = db.GetCollection<ProjectEntityLinkState>("project_entity_links");
        ProjectProposals = db.GetCollection<ProjectProposalBoundaryState>("project_proposals");
        KnowledgeDefinitions = db.GetCollection<KnowledgeDefinition>("knowledge_definitions");
        EntityKnowledgeStates = db.GetCollection<EntityKnowledgeState>("entity_knowledge_states");
        AppliedKnowledgeDefinitions = db.GetCollection<AppliedKnowledgeDefinition>("applied_knowledge_definitions");
        KnowledgeSources = db.GetCollection<KnowledgeSourceState>("knowledge_sources");
        ResearchResults = db.GetCollection<ResearchResultState>("research_results");
        ExperienceCoinLedger = db.GetCollection<ExperienceCoinLedgerEntry>("experience_coin_ledger");
        CraftingRecipes = db.GetCollection<CraftingRecipeDefinition>("crafting_recipes");
        CraftingRecipeIngredients = db.GetCollection<RecipeIngredientRequirement>("crafting_recipe_ingredients");
        CraftingRecipeTools = db.GetCollection<RecipeToolRequirement>("crafting_recipe_tools");
        CraftingRecipeFacilities = db.GetCollection<RecipeFacilityRequirement>("crafting_recipe_facilities");
        CraftingRecipeKnowledgeRequirements = db.GetCollection<RecipeKnowledgeRequirement>("crafting_recipe_knowledge_requirements");
        CraftingProjects = db.GetCollection<CraftingProjectState>("crafting_projects");
        CraftingReservations = db.GetCollection<CraftingResourceReservationState>("crafting_reservations");
        CraftingResults = db.GetCollection<CraftingProjectItemResult>("crafting_results");
        EngineeringPlatforms = db.GetCollection<EngineeringPlatformDefinition>("engineering_platforms");
        EngineeringSizeClasses = db.GetCollection<EngineeringPlatformSizeClassDefinition>("engineering_size_classes");
        EngineeringModules = db.GetCollection<EngineeringModuleDefinition>("engineering_modules");
        EngineeringModuleSlotRequirements = db.GetCollection<EngineeringModuleSlotRequirement>("engineering_module_slot_requirements");
        EngineeringModuleCompatibilityRules = db.GetCollection<EngineeringModuleCompatibilityRule>("engineering_module_compatibility_rules");
        EngineeringPowerProfiles = db.GetCollection<EngineeringPowerProfileDefinition>("engineering_power_profiles");
        EngineeringWeaponProfiles = db.GetCollection<EngineeringWeaponProfileDefinition>("engineering_weapon_profiles");
        EngineeringPresets = db.GetCollection<PresetVehicleDesignDefinition>("engineering_presets");
        EngineeringDesignDrafts = db.GetCollection<VehicleDesignDraft>("engineering_design_drafts");
        EngineeringProjects = db.GetCollection<EngineeringDesignProjectState>("engineering_projects");
        EngineeringValidationResults = db.GetCollection<EngineeringDesignValidationResult>("engineering_validation_results");
        EngineeringCostEstimates = db.GetCollection<EngineeringDesignCostEstimate>("engineering_cost_estimates");
        EngineeringBlueprints = db.GetCollection<VehicleDesignBlueprint>("engineering_blueprints");
        EngineeringBlueprintReferences = db.GetCollection<EngineeringBlueprintReference>("engineering_blueprint_references");
        ProductionFacilityDefinitions = db.GetCollection<ProductionFacilityDefinition>("production_facility_definitions");
        ProductionFacilities = db.GetCollection<ProductionFacilityState>("production_facilities");
        ProductionCapabilities = db.GetCollection<ProductionFacilityCapabilityState>("production_facility_capabilities");
        ProductionProcesses = db.GetCollection<ProductionProcessDefinition>("production_processes");
        ProductionCapacities = db.GetCollection<ProductionFacilityCapacityState>("production_facility_capacities");
        ProductionQueueSlots = db.GetCollection<ProductionQueueSlotState>("production_queue_slots");
        FactoryQuotes = db.GetCollection<FactoryQuoteState>("factory_quotes");
        FactoryOrders = db.GetCollection<FactoryOrderState>("factory_orders");
        FactoryOrderLines = db.GetCollection<FactoryOrderLineState>("factory_order_lines");
        FactoryOrderTerms = db.GetCollection<FactoryOrderTermState>("factory_order_terms");
        FactoryPaymentPlans = db.GetCollection<FactoryOrderPaymentPlanState>("factory_payment_plans");
        ManufacturingProjects = db.GetCollection<ManufacturingProjectState>("manufacturing_projects");
        ManufacturingStages = db.GetCollection<ManufacturingStageState>("manufacturing_stages");
        ManufacturingResourcePlans = db.GetCollection<ManufacturingResourcePlanState>("manufacturing_resource_plans");
        ManufacturingResourceReservations = db.GetCollection<ManufacturingResourceReservationState>("manufacturing_resource_reservations");
        ManufacturingCostLedger = db.GetCollection<ManufacturingCostLedgerEntry>("manufacturing_cost_ledger");
        ManufacturingPayments = db.GetCollection<ManufacturingPaymentState>("manufacturing_payments");
        ManufacturingProgressEntries = db.GetCollection<ManufacturingProgressEntry>("manufacturing_progress_entries");
        ManufacturingTestPlans = db.GetCollection<ManufacturingTestPlanState>("manufacturing_test_plans");
        ManufacturingTestResults = db.GetCollection<ManufacturingTestResultState>("manufacturing_test_results");
        ManufacturingDefects = db.GetCollection<ManufacturingDefectState>("manufacturing_defects");
        ManufacturingAcceptances = db.GetCollection<ManufacturingAcceptanceState>("manufacturing_acceptances");
        ManufacturedAssets = db.GetCollection<ManufacturedAssetState>("manufactured_assets");
        CharacterModuleStates = db.GetCollection<CharacterModuleStateDocument>("character_module_states");
        CharacterAttributeProfiles = db.GetCollection<CharacterAttributeProfileDocument>("character_attribute_profiles");
        CharacterSubAttributeProfiles = db.GetCollection<CharacterSubAttributeProfileDocument>("character_subattribute_profiles");
        CharacterSkillProfiles = db.GetCollection<CharacterSkillProfileDocument>("character_skill_profiles");
        CharacterDevelopmentProfiles = db.GetCollection<CharacterDevelopmentProfileDocument>("character_development_profiles");
        CharacterWalletProfiles = db.GetCollection<CharacterWalletProfileDocument>("character_wallet_profiles");
        CharacterInventoryProfiles = db.GetCollection<CharacterInventoryProfileDocument>("character_inventory_profiles");
        CharacterReputationProfiles = db.GetCollection<CharacterReputationProfileDocument>("character_reputation_profiles");
        CharacterHoldingsProfiles = db.GetCollection<CharacterHoldingsProfileDocument>("character_holdings_profiles");
        CharacterCompanionProfiles = db.GetCollection<CharacterCompanionProfileDocument>("character_companion_profiles");
        CharacterRaceOrSpeciesProfiles = db.GetCollection<CharacterRaceOrSpeciesProfileDocument>("character_race_or_species_profiles");
        CharacterBodyProfiles = db.GetCollection<CharacterBodyProfileDocument>("character_body_profiles");
        CharacterKnowledgeProfiles = db.GetCollection<CharacterKnowledgeProfileDocument>("character_knowledge_profiles");
        CharacterConditionProfiles = db.GetCollection<CharacterConditionProfileDocument>("character_condition_profiles");
        SyncEvents = db.GetCollection<SyncEvent>("sync_events");
        SyncCounters = db.GetCollection<SyncCounter>("sync_counters");
        ClassDefinitions = db.GetCollection<ClassDefinition>("class_definitions");
        RaceDefinitions = db.GetCollection<RaceDefinition>("race_definitions");
        DefinitionSkills = db.GetCollection<SkillDefinition>("skill_definition_documents");
        UnifiedDefinitions = db.GetCollection<UnifiedDefinitionDocument>("unified_definitions");
        FactionStates = db.GetCollection<FactionState>("faction_states");
        OrganizationStates = db.GetCollection<OrganizationState>("organization_states");
        MarketStates = db.GetCollection<MarketState>("market_states");
        LawStates = db.GetCollection<LawState>("law_states");
        RestrictionStates = db.GetCollection<RestrictionState>("restriction_states");
        AssetStates = db.GetCollection<AssetState>("asset_states");
        EconomyScopeStates = db.GetCollection<EconomyScopeState>("economy_scope_states");
        CombatEncounters = db.GetCollection<CombatEncounterState>("combat_encounters");
        CombatParticipants = db.GetCollection<CombatParticipantState>("combat_participants");
        CombatTurns = db.GetCollection<CombatTurnState>("combat_turns");
        CombatRounds = db.GetCollection<CombatRoundRuntimeState>("combat_rounds");
        CombatActions = db.GetCollection<CombatActionState>("combat_actions");
        CombatRuntimeLogs = db.GetCollection<CombatRuntimeLogEntry>("combat_runtime_logs");
        CombatReplayEvents = db.GetCollection<CombatReplayEvent>("combat_replay_events");
        MapSpaceNodes = db.GetCollection<MapSpaceNodeState>("map_space_nodes");
        MapCanvases = db.GetCollection<MapCanvasState>("map_states");
        RoomInteriors = db.GetCollection<RoomInteriorState>("map_room_interiors");
        WorldMaps = db.GetCollection<WorldMapState>("world_map_states");
        MapMarkers = db.GetCollection<MapMarkerState>("map_markers");
        MapMarkerBindings = db.GetCollection<MapMarkerBindingState>("map_marker_bindings");
        WorldMapLayers = db.GetCollection<WorldMapLayerState>("world_map_layers");
        WorldMapLegends = db.GetCollection<WorldMapLegendState>("world_map_legends");
        MapFogLayers = db.GetCollection<FogOfWarState>("map_fog_layers");
        SceneMapActiveLinks = db.GetCollection<SceneMapActiveLinkState>("map_scene_active_links");
        LegalJurisdictions = db.GetCollection<JurisdictionDefinition>("legal_jurisdictions");
        LegalProfiles = db.GetCollection<LegalProfileState>("legal_profiles");
        LegalRules = db.GetCollection<LegalRuleDefinition>("legal_rules");
        LegalSubjectClassifiers = db.GetCollection<LegalSubjectClassifier>("legal_subject_classifiers");
        LegalRestrictions = db.GetCollection<LegalRestrictionState>("legal_restrictions");
        LegalRequirements = db.GetCollection<LegalRequirementState>("legal_requirements");
        LegalLicenseDefinitions = db.GetCollection<LicenseDefinition>("legal_license_definitions");
        LegalEntityLicenses = db.GetCollection<EntityLicenseState>("legal_entity_licenses");
        LegalLicenseApplications = db.GetCollection<LicenseApplicationState>("legal_license_applications");
        LegalPermits = db.GetCollection<PermitState>("legal_permits");
        LegalCheckRecords = db.GetCollection<LegalCheckRecordState>("legal_check_records");
        LegalEnforcementRiskProfiles = db.GetCollection<EnforcementRiskProfile>("legal_enforcement_risk_profiles");
        LegalDeJureDeFactoStates = db.GetCollection<DeJureDeFactoLawState>("legal_de_jure_de_facto");
        LegalProductionLegalityStates = db.GetCollection<ProductionLegalityState>("legal_production_legality");
        PlayerProposalDrafts = db.GetCollection<PlayerProposalDraftState>("player_proposal_drafts");
        PlayerProposalFields = db.GetCollection<PlayerProposalFieldState>("player_proposal_fields");
        PlayerProposalValidations = db.GetCollection<PlayerProposalValidationResult>("player_proposal_validations");
        PlayerProposalReviews = db.GetCollection<PlayerProposalReviewState>("player_proposal_reviews");
        PlayerProposalConversions = db.GetCollection<PlayerProposalConversionState>("player_proposal_conversions");
        ProposalTemplateDefinitions = db.GetCollection<ProposalTemplateDefinition>("proposal_template_definitions");
        ProposalAttachmentLinks = db.GetCollection<PlayerProposalAttachmentLinkState>("proposal_attachment_links");

        EnsureIndexes(logger);
        logger.Debug("Mongo context initialized.");
    }

    private void EnsureIndexes(IServerLogger logger)
    {
        Accounts.Indexes.CreateOne(new CreateIndexModel<UserAccount>(Builders<UserAccount>.IndexKeys.Ascending(x => x.Login), new CreateIndexOptions { Unique = true }));
        Presence.Indexes.CreateOne(new CreateIndexModel<SessionUserState>(Builders<SessionUserState>.IndexKeys.Ascending(x => x.AuthToken), new CreateIndexOptions { Unique = true }));
        CurrentSessions.Indexes.CreateOne(new CreateIndexModel<CurrentSessionState>(Builders<CurrentSessionState>.IndexKeys.Ascending(x => x.CampaignId)));
        CurrentSessions.Indexes.CreateOne(new CreateIndexModel<CurrentSessionState>(Builders<CurrentSessionState>.IndexKeys.Ascending(x => x.SessionId)));
        CurrentSessions.Indexes.CreateOne(new CreateIndexModel<CurrentSessionState>(Builders<CurrentSessionState>.IndexKeys.Ascending(x => x.Status)));
        CurrentSessions.Indexes.CreateOne(new CreateIndexModel<CurrentSessionState>(Builders<CurrentSessionState>.IndexKeys.Ascending(x => x.CampaignId).Ascending(x => x.Status)));
        CurrentSessions.Indexes.CreateOne(new CreateIndexModel<CurrentSessionState>(Builders<CurrentSessionState>.IndexKeys.Ascending(x => x.IsArchived)));
        CurrentSessions.Indexes.CreateOne(new CreateIndexModel<CurrentSessionState>(Builders<CurrentSessionState>.IndexKeys.Descending(x => x.UpdatedAtUtc)));
        CurrentSessions.Indexes.CreateOne(new CreateIndexModel<CurrentSessionState>(Builders<CurrentSessionState>.IndexKeys.Ascending(x => x.GMUserId)));
        CharacterGroups.Indexes.CreateOne(new CreateIndexModel<CharacterGroupState>(Builders<CharacterGroupState>.IndexKeys.Ascending(x => x.CampaignId)));
        CharacterGroups.Indexes.CreateOne(new CreateIndexModel<CharacterGroupState>(Builders<CharacterGroupState>.IndexKeys.Ascending(x => x.SessionId)));
        CharacterGroups.Indexes.CreateOne(new CreateIndexModel<CharacterGroupState>(Builders<CharacterGroupState>.IndexKeys.Ascending(x => x.IsActive)));
        CharacterGroups.Indexes.CreateOne(new CreateIndexModel<CharacterGroupState>(Builders<CharacterGroupState>.IndexKeys.Ascending(x => x.IsArchived)));
        CharacterGroups.Indexes.CreateOne(new CreateIndexModel<CharacterGroupState>(Builders<CharacterGroupState>.IndexKeys.Ascending(x => x.CampaignId).Ascending(x => x.IsActive)));
        CharacterGroups.Indexes.CreateOne(new CreateIndexModel<CharacterGroupState>(Builders<CharacterGroupState>.IndexKeys.Descending(x => x.UpdatedAtUtc)));
        CharacterGroupMembers.Indexes.CreateOne(new CreateIndexModel<CharacterGroupMemberState>(Builders<CharacterGroupMemberState>.IndexKeys.Ascending(x => x.CampaignId)));
        CharacterGroupMembers.Indexes.CreateOne(new CreateIndexModel<CharacterGroupMemberState>(Builders<CharacterGroupMemberState>.IndexKeys.Ascending(x => x.GroupId)));
        CharacterGroupMembers.Indexes.CreateOne(new CreateIndexModel<CharacterGroupMemberState>(Builders<CharacterGroupMemberState>.IndexKeys.Ascending(x => x.GroupId).Ascending(x => x.EntityType).Ascending(x => x.EntityId)));
        CharacterGroupMembers.Indexes.CreateOne(new CreateIndexModel<CharacterGroupMemberState>(Builders<CharacterGroupMemberState>.IndexKeys.Ascending(x => x.RemovedAtUtc)));
        CharacterGroupMembers.Indexes.CreateOne(new CreateIndexModel<CharacterGroupMemberState>(Builders<CharacterGroupMemberState>.IndexKeys.Descending(x => x.JoinedAtUtc)));
        CharacterOwnerships.Indexes.CreateOne(new CreateIndexModel<CharacterOwnershipState>(Builders<CharacterOwnershipState>.IndexKeys.Ascending(x => x.CampaignId)));
        CharacterOwnerships.Indexes.CreateOne(new CreateIndexModel<CharacterOwnershipState>(Builders<CharacterOwnershipState>.IndexKeys.Ascending(x => x.CharacterId), new CreateIndexOptions { Unique = true }));
        CharacterOwnerships.Indexes.CreateOne(new CreateIndexModel<CharacterOwnershipState>(Builders<CharacterOwnershipState>.IndexKeys.Ascending(x => x.CampaignId).Ascending(x => x.CharacterId), new CreateIndexOptions { Unique = true }));
        CharacterOwnerships.Indexes.CreateOne(new CreateIndexModel<CharacterOwnershipState>(Builders<CharacterOwnershipState>.IndexKeys.Ascending(x => x.OwnerUserId)));
        CharacterOwnerships.Indexes.CreateOne(new CreateIndexModel<CharacterOwnershipState>(Builders<CharacterOwnershipState>.IndexKeys.Ascending(x => x.ControlledByUserId)));
        CharacterOwnerships.Indexes.CreateOne(new CreateIndexModel<CharacterOwnershipState>(Builders<CharacterOwnershipState>.IndexKeys.Ascending(x => x.CharacterRole)));
        CharacterOwnerships.Indexes.CreateOne(new CreateIndexModel<CharacterOwnershipState>(Builders<CharacterOwnershipState>.IndexKeys.Ascending(x => x.AssignmentStatus)));
        CharacterOwnerships.Indexes.CreateOne(new CreateIndexModel<CharacterOwnershipState>(Builders<CharacterOwnershipState>.IndexKeys.Descending(x => x.UpdatedAtUtc)));
        CharacterOwnerships.Indexes.CreateOne(new CreateIndexModel<CharacterOwnershipState>(Builders<CharacterOwnershipState>.IndexKeys.Ascending(x => x.CampaignId).Ascending(x => x.OwnerUserId)));
        CharacterOwnerships.Indexes.CreateOne(new CreateIndexModel<CharacterOwnershipState>(Builders<CharacterOwnershipState>.IndexKeys.Ascending(x => x.CampaignId).Ascending(x => x.CharacterRole)));
        CharacterOwnershipAudit.Indexes.CreateOne(new CreateIndexModel<CharacterOwnershipAuditEntry>(Builders<CharacterOwnershipAuditEntry>.IndexKeys.Ascending(x => x.CampaignId)));
        CharacterOwnershipAudit.Indexes.CreateOne(new CreateIndexModel<CharacterOwnershipAuditEntry>(Builders<CharacterOwnershipAuditEntry>.IndexKeys.Ascending(x => x.CharacterId)));
        CharacterOwnershipAudit.Indexes.CreateOne(new CreateIndexModel<CharacterOwnershipAuditEntry>(Builders<CharacterOwnershipAuditEntry>.IndexKeys.Ascending(x => x.ToOwnerUserId)));
        CharacterOwnershipAudit.Indexes.CreateOne(new CreateIndexModel<CharacterOwnershipAuditEntry>(Builders<CharacterOwnershipAuditEntry>.IndexKeys.Ascending(x => x.ActionType)));
        CharacterOwnershipAudit.Indexes.CreateOne(new CreateIndexModel<CharacterOwnershipAuditEntry>(Builders<CharacterOwnershipAuditEntry>.IndexKeys.Descending(x => x.PerformedAtUtc)));
        Characters.Indexes.CreateOne(new CreateIndexModel<Character>(Builders<Character>.IndexKeys.Ascending(x => x.OwnerUserId)));
        Locks.Indexes.CreateOne(new CreateIndexModel<EntityLock>(Builders<EntityLock>.IndexKeys.Ascending(x => x.EntityType).Ascending(x => x.EntityId), new CreateIndexOptions { Unique = true }));
        ActionRequests.Indexes.CreateOne(new CreateIndexModel<ActionRequest>(Builders<ActionRequest>.IndexKeys.Ascending(x => x.CreatorUserId).Ascending(x => x.Fingerprint).Ascending(x => x.Status)));
        DiceRequests.Indexes.CreateOne(new CreateIndexModel<DiceRollRequest>(Builders<DiceRollRequest>.IndexKeys.Ascending(x => x.CreatorUserId).Ascending(x => x.Fingerprint).Ascending(x => x.Status)));
        CreateIndexes(PlayerRequests,
            Asc<PlayerRequestState>("CampaignId"),
            Asc<PlayerRequestState>("SessionId"),
            Asc<PlayerRequestState>("GroupId"),
            Asc<PlayerRequestState>("CharacterId"),
            Asc<PlayerRequestState>("CreatedByUserId"),
            Asc<PlayerRequestState>("Status"),
            Asc<PlayerRequestState>("RequestNumber"),
            Asc<PlayerRequestState>("RequestType"),
            Asc<PlayerRequestState>("Priority"),
            Desc<PlayerRequestState>("UpdatedAtUtc"),
            Compound<PlayerRequestState>("CampaignId", "Status"),
            Compound<PlayerRequestState>("CampaignId", "CreatedByUserId"),
            Compound<PlayerRequestState>("CampaignId", "CharacterId"));
        CreateIndexes(PlayerRequestComments,
            Asc<PlayerRequestCommentState>("RequestId"),
            Asc<PlayerRequestCommentState>("CampaignId"),
            Asc<PlayerRequestCommentState>("AuthorUserId"),
            Asc<PlayerRequestCommentState>("IsPlayerVisible"),
            Asc<PlayerRequestCommentState>("IsArchived"),
            Asc<PlayerRequestCommentState>("CreatedAtUtc"),
            Compound<PlayerRequestCommentState>("RequestId", "CreatedAtUtc"));

        CreateIndexes(FeatureFlagOverrides,
            Asc<FeatureFlagOverrideState>("NormalizedName"),
            Asc<FeatureFlagOverrideState>("Deleted"),
            Asc<FeatureFlagOverrideState>("Archived"),
            Desc<FeatureFlagOverrideState>("UpdatedAtUtc"),
            Compound<FeatureFlagOverrideState>("NormalizedName", "Deleted", "Archived"));

        CreateIndexes(WorldCalendarDefinitions,
            Asc<WorldCalendarDefinition>("CampaignId"),
            Asc<WorldCalendarDefinition>("RuleSetId"),
            Asc<WorldCalendarDefinition>("IsActive"),
            Asc<WorldCalendarDefinition>("IsDefault"),
            Asc<WorldCalendarDefinition>("IsArchived"),
            Compound<WorldCalendarDefinition>("CampaignId", "IsActive"));

        CreateIndexes(WorldCalendarSeasons,
            Asc<WorldCalendarSeasonDefinition>("CalendarId"),
            Asc<WorldCalendarSeasonDefinition>("Order"),
            Compound<WorldCalendarSeasonDefinition>("CalendarId", "Order"));

        CreateIndexes(WorldCalendarMonths,
            Asc<WorldCalendarMonthDefinition>("CalendarId"),
            Asc<WorldCalendarMonthDefinition>("Order"),
            Asc<WorldCalendarMonthDefinition>("SeasonId"),
            Compound<WorldCalendarMonthDefinition>("CalendarId", "Order"));

        CreateIndexes(CampaignWorldTimes,
            Asc<CampaignWorldTimeState>("CampaignId"),
            Asc<CampaignWorldTimeState>("CalendarId"),
            Asc<CampaignWorldTimeState>("UpdatedAtUtc"),
            Compound<CampaignWorldTimeState>("CampaignId", "CalendarId"));

        CreateIndexes(WorldCalendarEvents,
            Asc<WorldCalendarEventState>("CampaignId"),
            Asc<WorldCalendarEventState>("CalendarId"),
            Asc<WorldCalendarEventState>("Status"),
            Asc<WorldCalendarEventState>("EventType"),
            Asc<WorldCalendarEventState>("IsPlayerVisible"),
            Asc<WorldCalendarEventState>("IsFutureEvent"),
            Asc<WorldCalendarEventState>("AuthorUserId"),
            Asc<WorldCalendarEventState>("LinkedSessionId"),
            Asc<WorldCalendarEventState>("IsArchived"),
            Asc<WorldCalendarEventState>("StartWorldDateTime.AbsoluteDayIndex"),
            Compound<WorldCalendarEventState>("CampaignId", "CalendarId"),
            Compound<WorldCalendarEventState>("CampaignId", "Status"));

        CreateIndexes(WorldCalendarEventVersions,
            Asc<WorldCalendarEventVersionState>("EventId"),
            Asc<WorldCalendarEventVersionState>("CampaignId"),
            Asc<WorldCalendarEventVersionState>("VersionType"),
            Asc<WorldCalendarEventVersionState>("IsPlayerVisible"),
            Compound<WorldCalendarEventVersionState>("EventId", "VersionType"));

        CreateIndexes(WorldCalendarHolidays,
            Asc<WorldCalendarHolidayDefinition>("CalendarId"),
            Asc<WorldCalendarHolidayDefinition>("CampaignId"),
            Asc<WorldCalendarHolidayDefinition>("MonthOrder"),
            Asc<WorldCalendarHolidayDefinition>("DayOfMonth"),
            Asc<WorldCalendarHolidayDefinition>("IsPlayerVisible"),
            Asc<WorldCalendarHolidayDefinition>("IsArchived"));

        CreateIndexes(WorldCalendarReminders,
            Asc<WorldCalendarReminderState>("CampaignId"),
            Asc<WorldCalendarReminderState>("CalendarId"),
            Asc<WorldCalendarReminderState>("EventId"),
            Asc<WorldCalendarReminderState>("IsDismissed"),
            Asc<WorldCalendarReminderState>("ReminderAtWorldDateTime.AbsoluteDayIndex"));

        CreateIndexes(RealScheduleEvents,
            Asc<RealScheduleEventState>("CampaignId"),
            Asc<RealScheduleEventState>("SessionId"),
            Asc<RealScheduleEventState>("GroupId"),
            Asc<RealScheduleEventState>("Status"),
            Asc<RealScheduleEventState>("EventType"),
            Asc<RealScheduleEventState>("StartUtc"),
            Asc<RealScheduleEventState>("EndUtc"),
            Asc<RealScheduleEventState>("GMUserId"),
            Asc<RealScheduleEventState>("IsPlayerVisible"),
            Asc<RealScheduleEventState>("VisibilityMode"),
            Asc<RealScheduleEventState>("LinkedWorldCalendarEventId"),
            Desc<RealScheduleEventState>("UpdatedAtUtc"),
            Compound<RealScheduleEventState>("CampaignId", "StartUtc"),
            Compound<RealScheduleEventState>("CampaignId", "Status"),
            Compound<RealScheduleEventState>("CampaignId", "IsPlayerVisible"));

        CreateIndexes(RealScheduleParticipants,
            Asc<RealScheduleParticipantState>("EventId"),
            Asc<RealScheduleParticipantState>("CampaignId"),
            Asc<RealScheduleParticipantState>("UserId"),
            Asc<RealScheduleParticipantState>("ParticipantRole"),
            Asc<RealScheduleParticipantState>("ResponseStatus"),
            Asc<RealScheduleParticipantState>("IsPlayerVisible"),
            Asc<RealScheduleParticipantState>("IsArchived"),
            Compound<RealScheduleParticipantState>("EventId", "UserId"));

        CreateIndexes(GMNotes,
            Asc<GMNoteState>("CampaignId"),
            Asc<GMNoteState>("SessionId"),
            Asc<GMNoteState>("FolderId"),
            Asc<GMNoteState>("AuthorUserId"),
            Asc<GMNoteState>("VisibilityMode"),
            Asc<GMNoteState>("NoteType"),
            Asc<GMNoteState>("IsPinned"),
            Asc<GMNoteState>("IsQuickNote"),
            Asc<GMNoteState>("IsArchived"),
            Desc<GMNoteState>("UpdatedAtUtc"),
            Compound<GMNoteState>("CampaignId", "IsArchived"),
            Compound<GMNoteState>("CampaignId", "FolderId"),
            Compound<GMNoteState>("CampaignId", "VisibilityMode"));

        CreateIndexes(GMNoteFolders,
            Asc<GMNoteFolderState>("CampaignId"),
            Asc<GMNoteFolderState>("ParentFolderId"),
            Asc<GMNoteFolderState>("OwnerUserId"),
            Asc<GMNoteFolderState>("VisibilityMode"),
            Asc<GMNoteFolderState>("IsArchived"),
            Asc<GMNoteFolderState>("SortOrder"),
            Compound<GMNoteFolderState>("CampaignId", "ParentFolderId"));

        CreateIndexes(GMNoteLinks,
            Asc<GMNoteEntityLinkState>("NoteId"),
            Asc<GMNoteEntityLinkState>("CampaignId"),
            Asc<GMNoteEntityLinkState>("EntityType"),
            Asc<GMNoteEntityLinkState>("EntityId"),
            Asc<GMNoteEntityLinkState>("LinkRole"),
            Desc<GMNoteEntityLinkState>("CreatedAtUtc"),
            Compound<GMNoteEntityLinkState>("EntityType", "EntityId"));

        CreateIndexes(GMNoteAudit,
            Asc<GMNoteAuditEntry>("CampaignId"),
            Asc<GMNoteAuditEntry>("NoteId"),
            Asc<GMNoteAuditEntry>("ActionType"),
            Asc<GMNoteAuditEntry>("PerformedByUserId"),
            Desc<GMNoteAuditEntry>("PerformedAtUtc"));

        CreateIndexes(EventJournalEntries,
            Asc<EventJournalEntryState>("CampaignId"),
            Asc<EventJournalEntryState>("SessionId"),
            Asc<EventJournalEntryState>("GroupId"),
            Asc<EventJournalEntryState>("CharacterId"),
            Asc<EventJournalEntryState>("Category"),
            Asc<EventJournalEntryState>("SourceModule"),
            Asc<EventJournalEntryState>("SourceEventType"),
            Asc<EventJournalEntryState>("SourceEventId"),
            Asc<EventJournalEntryState>("CorrelationId"),
            Asc<EventJournalEntryState>("VisibilityMode"),
            Asc<EventJournalEntryState>("IsPlayerVisible"),
            Asc<EventJournalEntryState>("IsAutomatic"),
            Asc<EventJournalEntryState>("IsArchived"),
            Desc<EventJournalEntryState>("OccurredAtUtc"),
            Desc<EventJournalEntryState>("SequenceNumber"),
            Compound<EventJournalEntryState>("CampaignId", "SequenceNumber"),
            Compound<EventJournalEntryState>("CampaignId", "SessionId"),
            Compound<EventJournalEntryState>("CampaignId", "Category"),
            Compound<EventJournalEntryState>("SourceModule", "SourceEventId"),
            Compound<EventJournalEntryState>("CorrelationId", "SourceEventType"));

        CreateIndexes(EventJournalLinks,
            Asc<EventJournalEntityLinkState>("EntryId"),
            Asc<EventJournalEntityLinkState>("CampaignId"),
            Asc<EventJournalEntityLinkState>("EntityType"),
            Asc<EventJournalEntityLinkState>("EntityId"),
            Asc<EventJournalEntityLinkState>("LinkRole"),
            Asc<EventJournalEntityLinkState>("IsPlayerVisible"),
            Asc<EventJournalEntityLinkState>("IsArchived"),
            Compound<EventJournalEntityLinkState>("EntityType", "EntityId"));

        CreateIndexes(EventJournalAnnotations,
            Asc<EventJournalAnnotationState>("EntryId"),
            Asc<EventJournalAnnotationState>("CampaignId"),
            Asc<EventJournalAnnotationState>("AuthorUserId"),
            Asc<EventJournalAnnotationState>("IsPlayerVisible"),
            Asc<EventJournalAnnotationState>("IsArchived"),
            Desc<EventJournalAnnotationState>("CreatedAtUtc"));

        CreateIndexes(EventJournalAudit,
            Asc<EventJournalAuditEntry>("CampaignId"),
            Asc<EventJournalAuditEntry>("EntryId"),
            Asc<EventJournalAuditEntry>("ActionType"),
            Asc<EventJournalAuditEntry>("PerformedByUserId"),
            Desc<EventJournalAuditEntry>("PerformedAtUtc"));
        Combats.Indexes.CreateOne(new CreateIndexModel<CombatState>(Builders<CombatState>.IndexKeys.Ascending(x => x.SessionId), new CreateIndexOptions { Unique = true }));
        CombatLogs.Indexes.CreateOne(new CreateIndexModel<CombatLogEntry>(Builders<CombatLogEntry>.IndexKeys.Ascending(x => x.CombatId).Descending(x => x.CreatedUtc)));
        ChatMessages.Indexes.CreateOne(new CreateIndexModel<ChatMessage>(Builders<ChatMessage>.IndexKeys.Ascending(x => x.SessionId).Descending(x => x.CreatedUtc)));
        ChatReadStates.Indexes.CreateOne(new CreateIndexModel<ChatReadState>(Builders<ChatReadState>.IndexKeys.Ascending(x => x.SessionId).Ascending(x => x.UserId), new CreateIndexOptions { Unique = true }));
        SessionChatSettings.Indexes.CreateOne(new CreateIndexModel<SessionChatSettings>(Builders<SessionChatSettings>.IndexKeys.Ascending(x => x.SessionId), new CreateIndexOptions { Unique = true }));
        ChatThrottleStates.Indexes.CreateOne(new CreateIndexModel<ChatUserThrottleState>(Builders<ChatUserThrottleState>.IndexKeys.Ascending(x => x.SessionId).Ascending(x => x.UserId).Ascending(x => x.MessageType), new CreateIndexOptions { Unique = true }));
        AudioTracks.Indexes.CreateOne(new CreateIndexModel<AudioTrackDefinition>(Builders<AudioTrackDefinition>.IndexKeys.Ascending(x => x.FilePath), new CreateIndexOptions { Unique = true }));
        AudioClientSettings.Indexes.CreateOne(new CreateIndexModel<AudioClientSettingsState>(Builders<AudioClientSettingsState>.IndexKeys.Ascending(x => x.UserId), new CreateIndexOptions { Unique = true }));
        ClassTrees.Indexes.CreateOne(new CreateIndexModel<ClassTreeDefinition>(Builders<ClassTreeDefinition>.IndexKeys.Ascending(x => x.DirectionId), new CreateIndexOptions { Unique = true }));
        SkillDefinitions.Indexes.CreateOne(new CreateIndexModel<SkillDefinitionRecord>(Builders<SkillDefinitionRecord>.IndexKeys.Ascending(x => x.SkillId), new CreateIndexOptions { Unique = true }));
        DefinitionVersions.Indexes.CreateOne(new CreateIndexModel<DefinitionVersion>(Builders<DefinitionVersion>.IndexKeys.Ascending(x => x.ContentName), new CreateIndexOptions { Unique = true }));
        Notes.Indexes.CreateOne(new CreateIndexModel<Note>(Builders<Note>.IndexKeys.Ascending(x => x.SessionId).Descending(x => x.CreatedUtc)));
        References.Indexes.CreateOne(new CreateIndexModel<ReferenceEntry>(Builders<ReferenceEntry>.IndexKeys.Ascending(x => x.WorldId).Ascending(x => x.ReferenceType).Ascending(x => x.Key), new CreateIndexOptions { Unique = true }));
        UpdateVersions.Indexes.CreateOne(new CreateIndexModel<UpdateVersionInfo>(Builders<UpdateVersionInfo>.IndexKeys.Ascending(x => x.ClientChannel), new CreateIndexOptions { Unique = true }));
        Backups.Indexes.CreateOne(new CreateIndexModel<BackupSnapshot>(Builders<BackupSnapshot>.IndexKeys.Descending(x => x.CreatedUtc)));
        CreateIndexes(BackupRecords,
            Asc<BackupRecordState>("BackupId"),
            Asc<BackupRecordState>("Scope"),
            Asc<BackupRecordState>("Status"),
            Asc<BackupRecordState>("VerificationStatus"),
            Asc<BackupRecordState>("IsVerified"),
            Asc<BackupRecordState>("IsArchived"),
            Asc<BackupRecordState>("IsPreRestoreSafetyBackup"),
            Desc<BackupRecordState>("CreatedUtc"),
            Desc<BackupRecordState>("CompletedAtUtc"));
        CreateIndexes(BackupRestoreOperations,
            Asc<BackupRestoreOperationState>("OperationId"),
            Asc<BackupRestoreOperationState>("BackupId"),
            Asc<BackupRestoreOperationState>("Status"),
            Asc<BackupRestoreOperationState>("RequestedByUserId"),
            Desc<BackupRestoreOperationState>("RequestedAtUtc"));
        CreateIndexes(BackupMaintenanceStates,
            Asc<BackupMaintenanceState>("IsEnabled"),
            Desc<BackupMaintenanceState>("UpdatedAtUtc"));
        CreateIndexes(Projects,
            Asc<ProjectBaseState>("CampaignId"),
            Asc<ProjectBaseState>("SessionId"),
            Asc<ProjectBaseState>("ActiveGroupId"),
            Asc<ProjectBaseState>("ProjectType"),
            Asc<ProjectBaseState>("Status"),
            Asc<ProjectBaseState>("ApprovalStatus"),
            Asc<ProjectBaseState>("OwnerUserId"),
            Asc<ProjectBaseState>("OwnerCharacterId"),
            Asc<ProjectBaseState>("IsPlayerVisible"),
            Asc<ProjectBaseState>("IsArchived"),
            Desc<ProjectBaseState>("UpdatedAtUtc"),
            Compound<ProjectBaseState>("CampaignId", "Status"),
            Compound<ProjectBaseState>("CampaignId", "OwnerUserId"),
            Compound<ProjectBaseState>("CampaignId", "ProjectType"));
        CreateIndexes(ProjectStages,
            Asc<ProjectStageState>("ProjectId"),
            Asc<ProjectStageState>("CampaignId"),
            Asc<ProjectStageState>("StageType"),
            Asc<ProjectStageState>("Status"),
            Asc<ProjectStageState>("SortOrder"),
            Asc<ProjectStageState>("IsPlayerVisible"),
            Compound<ProjectStageState>("ProjectId", "SortOrder"));
        CreateIndexes(ProjectParticipants,
            Asc<ProjectParticipantState>("ProjectId"),
            Asc<ProjectParticipantState>("CampaignId"),
            Asc<ProjectParticipantState>("EntityType"),
            Asc<ProjectParticipantState>("EntityId"),
            Asc<ProjectParticipantState>("OwnerUserId"),
            Asc<ProjectParticipantState>("ParticipantRole"),
            Asc<ProjectParticipantState>("IsPlayerVisible"),
            Compound<ProjectParticipantState>("ProjectId", "EntityType", "EntityId"));
        CreateIndexes(ProjectRequirements,
            Asc<ProjectRequirementState>("ProjectId"),
            Asc<ProjectRequirementState>("CampaignId"),
            Asc<ProjectRequirementState>("RequirementType"),
            Asc<ProjectRequirementState>("Status"),
            Asc<ProjectRequirementState>("IsPlayerVisible"));
        CreateIndexes(ProjectResourceRequirements,
            Asc<ProjectResourceRequirementState>("ProjectId"),
            Asc<ProjectResourceRequirementState>("CampaignId"),
            Asc<ProjectResourceRequirementState>("ResourceType"),
            Asc<ProjectResourceRequirementState>("ResourceId"),
            Asc<ProjectResourceRequirementState>("Status"),
            Asc<ProjectResourceRequirementState>("IsPlayerVisible"));
        CreateIndexes(ProjectProgressEntries,
            Asc<ProjectProgressEntryState>("ProjectId"),
            Asc<ProjectProgressEntryState>("CampaignId"),
            Asc<ProjectProgressEntryState>("StageId"),
            Asc<ProjectProgressEntryState>("CreatedAtUtc"),
            Asc<ProjectProgressEntryState>("IsPlayerVisible"),
            Compound<ProjectProgressEntryState>("ProjectId", "CreatedAtUtc"));
        CreateIndexes(ProjectApprovals,
            Asc<ProjectApprovalState>("ProjectId"),
            Asc<ProjectApprovalState>("CampaignId"),
            Asc<ProjectApprovalState>("Status"),
            Asc<ProjectApprovalState>("RequestedByUserId"),
            Asc<ProjectApprovalState>("ReviewedByUserId"),
            Asc<ProjectApprovalState>("IsPlayerVisible"));
        CreateIndexes(ProjectAuditEntries,
            Asc<ProjectAuditEntryState>("ProjectId"),
            Asc<ProjectAuditEntryState>("CampaignId"),
            Asc<ProjectAuditEntryState>("ActionType"),
            Desc<ProjectAuditEntryState>("CreatedAtUtc"));
        CreateIndexes(ProjectEntityLinks,
            Asc<ProjectEntityLinkState>("ProjectId"),
            Asc<ProjectEntityLinkState>("CampaignId"),
            Asc<ProjectEntityLinkState>("LinkType"),
            Asc<ProjectEntityLinkState>("EntityId"),
            Asc<ProjectEntityLinkState>("IsPlayerVisible"),
            Compound<ProjectEntityLinkState>("ProjectId", "LinkType", "EntityId"));
        CreateIndexes(ProjectProposals,
            Asc<ProjectProposalBoundaryState>("CampaignId"),
            Asc<ProjectProposalBoundaryState>("ProjectId"),
            Asc<ProjectProposalBoundaryState>("ProposalType"),
            Asc<ProjectProposalBoundaryState>("Status"),
            Desc<ProjectProposalBoundaryState>("UpdatedAtUtc"));
        CreateIndexes(KnowledgeDefinitions,
            Asc<KnowledgeDefinition>("CampaignId"),
            Asc<KnowledgeDefinition>("RuleSetId"),
            Asc<KnowledgeDefinition>("KnowledgeId"),
            Asc<KnowledgeDefinition>("Category"),
            Asc<KnowledgeDefinition>("KnowledgeType"),
            Asc<KnowledgeDefinition>("KnowledgeDomain"),
            Asc<KnowledgeDefinition>("IsAppliedKnowledge"),
            Asc<KnowledgeDefinition>("IsSecret"),
            Asc<KnowledgeDefinition>("IsArchived"),
            Desc<KnowledgeDefinition>("UpdatedAtUtc"),
            Compound<KnowledgeDefinition>("CampaignId", "KnowledgeId"),
            Compound<KnowledgeDefinition>("CampaignId", "KnowledgeType"),
            Compound<KnowledgeDefinition>("CampaignId", "KnowledgeDomain"));
        CreateIndexes(EntityKnowledgeStates,
            Asc<EntityKnowledgeState>("CampaignId"),
            Asc<EntityKnowledgeState>("KnowledgeDefinitionId"),
            Asc<EntityKnowledgeState>("KnowledgeId"),
            Asc<EntityKnowledgeState>("EntityType"),
            Asc<EntityKnowledgeState>("EntityId"),
            Asc<EntityKnowledgeState>("OwnerUserId"),
            Asc<EntityKnowledgeState>("Level"),
            Asc<EntityKnowledgeState>("TruthRelation"),
            Asc<EntityKnowledgeState>("IsPlayerVisible"),
            Asc<EntityKnowledgeState>("IsArchived"),
            Desc<EntityKnowledgeState>("UpdatedAtUtc"),
            Compound<EntityKnowledgeState>("EntityType", "EntityId"),
            Compound<EntityKnowledgeState>("CampaignId", "EntityType", "EntityId"),
            Compound<EntityKnowledgeState>("CampaignId", "KnowledgeDefinitionId"));
        CreateIndexes(AppliedKnowledgeDefinitions,
            Asc<AppliedKnowledgeDefinition>("CampaignId"),
            Asc<AppliedKnowledgeDefinition>("KnowledgeDefinitionId"),
            Asc<AppliedKnowledgeDefinition>("AppliedType"),
            Asc<AppliedKnowledgeDefinition>("IsPlayerVisible"),
            Asc<AppliedKnowledgeDefinition>("IsArchived"),
            Desc<AppliedKnowledgeDefinition>("UpdatedAtUtc"));
        CreateIndexes(KnowledgeSources,
            Asc<KnowledgeSourceState>("CampaignId"),
            Asc<KnowledgeSourceState>("KnowledgeDefinitionId"),
            Asc<KnowledgeSourceState>("EntityKnowledgeId"),
            Asc<KnowledgeSourceState>("SourceType"),
            Asc<KnowledgeSourceState>("LinkedEntityType"),
            Asc<KnowledgeSourceState>("LinkedEntityId"),
            Asc<KnowledgeSourceState>("IsPlayerVisible"),
            Asc<KnowledgeSourceState>("IsArchived"),
            Desc<KnowledgeSourceState>("CreatedAtUtc"));
        CreateIndexes(ResearchResults,
            Asc<ResearchResultState>("CampaignId"),
            Asc<ResearchResultState>("ProjectId"),
            Asc<ResearchResultState>("ResultType"),
            Asc<ResearchResultState>("Status"),
            Asc<ResearchResultState>("KnowledgeDefinitionId"),
            Asc<ResearchResultState>("AppliedKnowledgeId"),
            Asc<ResearchResultState>("TargetEntityType"),
            Asc<ResearchResultState>("TargetEntityId"),
            Asc<ResearchResultState>("IsPlayerVisible"),
            Asc<ResearchResultState>("IsArchived"),
            Desc<ResearchResultState>("PreparedAtUtc"));
        CreateIndexes(ExperienceCoinLedger,
            Asc<ExperienceCoinLedgerEntry>("CampaignId"),
            Asc<ExperienceCoinLedgerEntry>("CharacterId"),
            Asc<ExperienceCoinLedgerEntry>("ActorUserId"),
            Asc<ExperienceCoinLedgerEntry>("EntryType"),
            Asc<ExperienceCoinLedgerEntry>("DevelopmentNodeId"),
            Asc<ExperienceCoinLedgerEntry>("IsPlayerVisible"),
            Desc<ExperienceCoinLedgerEntry>("CreatedAtUtc"),
            Compound<ExperienceCoinLedgerEntry>("CharacterId", "CreatedAtUtc"),
            Compound<ExperienceCoinLedgerEntry>("CharacterId", "DevelopmentNodeId"));
        CreateIndexes(CraftingRecipes,
            Asc<CraftingRecipeDefinition>("CampaignId"),
            Asc<CraftingRecipeDefinition>("RuleSetId"),
            Asc<CraftingRecipeDefinition>("RecipeId"),
            Asc<CraftingRecipeDefinition>("RecipeCategory"),
            Asc<CraftingRecipeDefinition>("RecipeType"),
            Asc<CraftingRecipeDefinition>("IsPlayerVisible"),
            Asc<CraftingRecipeDefinition>("IsArchived"),
            Desc<CraftingRecipeDefinition>("UpdatedAtUtc"),
            Compound<CraftingRecipeDefinition>("CampaignId", "RecipeId"));
        CreateIndexes(CraftingRecipeIngredients,
            Asc<RecipeIngredientRequirement>("CampaignId"),
            Asc<RecipeIngredientRequirement>("RecipeId"),
            Asc<RecipeIngredientRequirement>("IngredientType"),
            Asc<RecipeIngredientRequirement>("IngredientDefinitionId"),
            Asc<RecipeIngredientRequirement>("IsPlayerVisible"));
        CreateIndexes(CraftingRecipeTools,
            Asc<RecipeToolRequirement>("CampaignId"),
            Asc<RecipeToolRequirement>("RecipeId"),
            Asc<RecipeToolRequirement>("ToolDefinitionId"),
            Asc<RecipeToolRequirement>("IsPlayerVisible"));
        CreateIndexes(CraftingRecipeFacilities,
            Asc<RecipeFacilityRequirement>("CampaignId"),
            Asc<RecipeFacilityRequirement>("RecipeId"),
            Asc<RecipeFacilityRequirement>("FacilityType"),
            Asc<RecipeFacilityRequirement>("IsPlayerVisible"));
        CreateIndexes(CraftingRecipeKnowledgeRequirements,
            Asc<RecipeKnowledgeRequirement>("CampaignId"),
            Asc<RecipeKnowledgeRequirement>("RecipeId"),
            Asc<RecipeKnowledgeRequirement>("KnowledgeDefinitionId"),
            Asc<RecipeKnowledgeRequirement>("AppliedKnowledgeId"),
            Asc<RecipeKnowledgeRequirement>("IsPlayerVisible"));
        CreateIndexes(CraftingProjects,
            Asc<CraftingProjectState>("CampaignId"),
            Asc<CraftingProjectState>("ProjectId"),
            Asc<CraftingProjectState>("RecipeId"),
            Asc<CraftingProjectState>("OwnerUserId"),
            Asc<CraftingProjectState>("ActorEntityId"),
            Asc<CraftingProjectState>("TargetInventoryCharacterId"),
            Asc<CraftingProjectState>("Status"),
            Asc<CraftingProjectState>("IsPlayerVisible"),
            Desc<CraftingProjectState>("UpdatedAtUtc"),
            Compound<CraftingProjectState>("CampaignId", "Status"),
            Compound<CraftingProjectState>("CampaignId", "OwnerUserId"));
        CreateIndexes(CraftingReservations,
            Asc<CraftingResourceReservationState>("CampaignId"),
            Asc<CraftingResourceReservationState>("ProjectId"),
            Asc<CraftingResourceReservationState>("CraftingProjectId"),
            Asc<CraftingResourceReservationState>("CharacterId"),
            Asc<CraftingResourceReservationState>("ItemInstanceId"),
            Asc<CraftingResourceReservationState>("Status"),
            Desc<CraftingResourceReservationState>("ReservedAtUtc"),
            Compound<CraftingResourceReservationState>("CharacterId", "ItemInstanceId"),
            Compound<CraftingResourceReservationState>("CraftingProjectId", "Status"));
        CreateIndexes(CraftingResults,
            Asc<CraftingProjectItemResult>("CampaignId"),
            Asc<CraftingProjectItemResult>("CraftingProjectId"),
            Asc<CraftingProjectItemResult>("ProjectId"),
            Asc<CraftingProjectItemResult>("TargetCharacterId"),
            Asc<CraftingProjectItemResult>("Status"),
            Desc<CraftingProjectItemResult>("PreparedAtUtc"));
        CreateIndexes(EngineeringPlatforms,
            Asc<EngineeringPlatformDefinition>("CampaignId"),
            Asc<EngineeringPlatformDefinition>("RuleSetId"),
            Asc<EngineeringPlatformDefinition>("PlatformId"),
            Asc<EngineeringPlatformDefinition>("PlatformKind"),
            Asc<EngineeringPlatformDefinition>("SizeClassId"),
            Asc<EngineeringPlatformDefinition>("IsPlayerVisible"),
            Asc<EngineeringPlatformDefinition>("IsArchived"),
            Desc<EngineeringPlatformDefinition>("UpdatedAtUtc"),
            Compound<EngineeringPlatformDefinition>("CampaignId", "PlatformId"));
        CreateIndexes(EngineeringSizeClasses,
            Asc<EngineeringPlatformSizeClassDefinition>("CampaignId"),
            Asc<EngineeringPlatformSizeClassDefinition>("RuleSetId"),
            Asc<EngineeringPlatformSizeClassDefinition>("SizeClassId"),
            Asc<EngineeringPlatformSizeClassDefinition>("IsPlayerVisible"),
            Asc<EngineeringPlatformSizeClassDefinition>("IsArchived"));
        CreateIndexes(EngineeringModules,
            Asc<EngineeringModuleDefinition>("CampaignId"),
            Asc<EngineeringModuleDefinition>("RuleSetId"),
            Asc<EngineeringModuleDefinition>("ModuleId"),
            Asc<EngineeringModuleDefinition>("ModuleCategory"),
            Asc<EngineeringModuleDefinition>("SlotType"),
            Asc<EngineeringModuleDefinition>("IsPlayerVisible"),
            Asc<EngineeringModuleDefinition>("IsArchived"),
            Desc<EngineeringModuleDefinition>("UpdatedAtUtc"),
            Compound<EngineeringModuleDefinition>("CampaignId", "ModuleId"));
        CreateIndexes(EngineeringModuleSlotRequirements,
            Asc<EngineeringModuleSlotRequirement>("CampaignId"),
            Asc<EngineeringModuleSlotRequirement>("ModuleId"),
            Asc<EngineeringModuleSlotRequirement>("SlotType"),
            Asc<EngineeringModuleSlotRequirement>("IsPlayerVisible"));
        CreateIndexes(EngineeringModuleCompatibilityRules,
            Asc<EngineeringModuleCompatibilityRule>("CampaignId"),
            Asc<EngineeringModuleCompatibilityRule>("ModuleId"),
            Asc<EngineeringModuleCompatibilityRule>("TargetModuleId"),
            Asc<EngineeringModuleCompatibilityRule>("PlatformKind"),
            Asc<EngineeringModuleCompatibilityRule>("RuleType"));
        CreateIndexes(EngineeringPowerProfiles,
            Asc<EngineeringPowerProfileDefinition>("CampaignId"),
            Asc<EngineeringPowerProfileDefinition>("RuleSetId"),
            Asc<EngineeringPowerProfileDefinition>("PowerProfileId"),
            Asc<EngineeringPowerProfileDefinition>("IsPlayerVisible"));
        CreateIndexes(EngineeringWeaponProfiles,
            Asc<EngineeringWeaponProfileDefinition>("CampaignId"),
            Asc<EngineeringWeaponProfileDefinition>("ModuleId"),
            Asc<EngineeringWeaponProfileDefinition>("WeaponProfileId"),
            Asc<EngineeringWeaponProfileDefinition>("IsPlayerVisible"));
        CreateIndexes(EngineeringPresets,
            Asc<PresetVehicleDesignDefinition>("CampaignId"),
            Asc<PresetVehicleDesignDefinition>("RuleSetId"),
            Asc<PresetVehicleDesignDefinition>("PresetId"),
            Asc<PresetVehicleDesignDefinition>("PlatformId"),
            Asc<PresetVehicleDesignDefinition>("IsPlayerVisible"),
            Asc<PresetVehicleDesignDefinition>("IsArchived"),
            Desc<PresetVehicleDesignDefinition>("UpdatedAtUtc"));
        CreateIndexes(EngineeringDesignDrafts,
            Asc<VehicleDesignDraft>("CampaignId"),
            Asc<VehicleDesignDraft>("DraftId"),
            Asc<VehicleDesignDraft>("ProjectId"),
            Asc<VehicleDesignDraft>("OwnerUserId"),
            Asc<VehicleDesignDraft>("OwnerCharacterId"),
            Asc<VehicleDesignDraft>("Status"),
            Desc<VehicleDesignDraft>("UpdatedAtUtc"));
        CreateIndexes(EngineeringProjects,
            Asc<EngineeringDesignProjectState>("CampaignId"),
            Asc<EngineeringDesignProjectState>("ProjectId"),
            Asc<EngineeringDesignProjectState>("ProjectBaseId"),
            Asc<EngineeringDesignProjectState>("DraftId"),
            Asc<EngineeringDesignProjectState>("OwnerUserId"),
            Asc<EngineeringDesignProjectState>("ActorEntityId"),
            Asc<EngineeringDesignProjectState>("Status"),
            Asc<EngineeringDesignProjectState>("IsPlayerVisible"),
            Desc<EngineeringDesignProjectState>("UpdatedAtUtc"),
            Compound<EngineeringDesignProjectState>("CampaignId", "Status"),
            Compound<EngineeringDesignProjectState>("CampaignId", "OwnerUserId"));
        CreateIndexes(EngineeringValidationResults,
            Asc<EngineeringDesignValidationResult>("CampaignId"),
            Asc<EngineeringDesignValidationResult>("DraftId"),
            Asc<EngineeringDesignValidationResult>("ProjectId"),
            Asc<EngineeringDesignValidationResult>("Status"),
            Desc<EngineeringDesignValidationResult>("BuiltAtUtc"));
        CreateIndexes(EngineeringCostEstimates,
            Asc<EngineeringDesignCostEstimate>("CampaignId"),
            Asc<EngineeringDesignCostEstimate>("DraftId"),
            Asc<EngineeringDesignCostEstimate>("ProjectId"),
            Desc<EngineeringDesignCostEstimate>("BuiltAtUtc"));
        CreateIndexes(EngineeringBlueprints,
            Asc<VehicleDesignBlueprint>("CampaignId"),
            Asc<VehicleDesignBlueprint>("BlueprintId"),
            Asc<VehicleDesignBlueprint>("ProjectId"),
            Asc<VehicleDesignBlueprint>("DraftId"),
            Asc<VehicleDesignBlueprint>("Status"),
            Asc<VehicleDesignBlueprint>("IsPlayerVisible"),
            Desc<VehicleDesignBlueprint>("PreparedAtUtc"));
        CreateIndexes(EngineeringBlueprintReferences,
            Asc<EngineeringBlueprintReference>("CampaignId"),
            Asc<EngineeringBlueprintReference>("BlueprintId"),
            Asc<EngineeringBlueprintReference>("ProjectId"),
            Asc<EngineeringBlueprintReference>("IsPlayerVisible"),
            Desc<EngineeringBlueprintReference>("CreatedAtUtc"));
        CreateIndexes(ProductionFacilityDefinitions,
            Asc<ProductionFacilityDefinition>("CampaignId"),
            Asc<ProductionFacilityDefinition>("RuleSetId"),
            Asc<ProductionFacilityDefinition>("FacilityDefinitionId"),
            Asc<ProductionFacilityDefinition>("FacilityCategory"),
            Asc<ProductionFacilityDefinition>("FacilityType"),
            Asc<ProductionFacilityDefinition>("IsPlayerVisible"),
            Asc<ProductionFacilityDefinition>("IsArchived"),
            Desc<ProductionFacilityDefinition>("UpdatedAtUtc"));
        CreateIndexes(ProductionFacilities,
            Asc<ProductionFacilityState>("CampaignId"),
            Asc<ProductionFacilityState>("FacilityId"),
            Asc<ProductionFacilityState>("FacilityDefinitionId"),
            Asc<ProductionFacilityState>("FacilityCategory"),
            Asc<ProductionFacilityState>("OperationalStatus"),
            Asc<ProductionFacilityState>("IsPlayerVisible"),
            Asc<ProductionFacilityState>("IsArchived"),
            Desc<ProductionFacilityState>("UpdatedAtUtc"),
            Compound<ProductionFacilityState>("CampaignId", "OperationalStatus"));
        CreateIndexes(ProductionCapabilities,
            Asc<ProductionFacilityCapabilityState>("CampaignId"),
            Asc<ProductionFacilityCapabilityState>("FacilityId"),
            Asc<ProductionFacilityCapabilityState>("ProductionDomain"),
            Asc<ProductionFacilityCapabilityState>("IsPlayerVisible"),
            Desc<ProductionFacilityCapabilityState>("UpdatedAtUtc"));
        CreateIndexes(ProductionProcesses,
            Asc<ProductionProcessDefinition>("CampaignId"),
            Asc<ProductionProcessDefinition>("RuleSetId"),
            Asc<ProductionProcessDefinition>("ProcessId"),
            Asc<ProductionProcessDefinition>("ProductionDomain"),
            Asc<ProductionProcessDefinition>("IsPlayerVisible"),
            Asc<ProductionProcessDefinition>("IsArchived"));
        CreateIndexes(ProductionCapacities,
            Asc<ProductionFacilityCapacityState>("CampaignId"),
            Asc<ProductionFacilityCapacityState>("FacilityId"),
            Desc<ProductionFacilityCapacityState>("UpdatedAtUtc"));
        CreateIndexes(ProductionQueueSlots,
            Asc<ProductionQueueSlotState>("CampaignId"),
            Asc<ProductionQueueSlotState>("FacilityId"),
            Asc<ProductionQueueSlotState>("QuoteId"),
            Asc<ProductionQueueSlotState>("OrderId"),
            Asc<ProductionQueueSlotState>("Status"),
            Desc<ProductionQueueSlotState>("UpdatedAtUtc"));
        CreateIndexes(FactoryQuotes,
            Asc<FactoryQuoteState>("CampaignId"),
            Asc<FactoryQuoteState>("QuoteId"),
            Asc<FactoryQuoteState>("FacilityId"),
            Asc<FactoryQuoteState>("BlueprintId"),
            Asc<FactoryQuoteState>("PresetId"),
            Asc<FactoryQuoteState>("OwnerUserId"),
            Asc<FactoryQuoteState>("Status"),
            Asc<FactoryQuoteState>("IsPlayerVisible"),
            Desc<FactoryQuoteState>("UpdatedAtUtc"));
        CreateIndexes(FactoryOrders,
            Asc<FactoryOrderState>("CampaignId"),
            Asc<FactoryOrderState>("OrderId"),
            Asc<FactoryOrderState>("QuoteId"),
            Asc<FactoryOrderState>("FacilityId"),
            Asc<FactoryOrderState>("OwnerUserId"),
            Asc<FactoryOrderState>("Status"),
            Asc<FactoryOrderState>("IsPlayerVisible"),
            Desc<FactoryOrderState>("UpdatedAtUtc"));
        CreateIndexes(FactoryOrderLines,
            Asc<FactoryOrderLineState>("CampaignId"),
            Asc<FactoryOrderLineState>("OrderId"));
        CreateIndexes(FactoryOrderTerms,
            Asc<FactoryOrderTermState>("CampaignId"),
            Asc<FactoryOrderTermState>("QuoteId"),
            Asc<FactoryOrderTermState>("OrderId"),
            Asc<FactoryOrderTermState>("IsPlayerVisible"));
        CreateIndexes(FactoryPaymentPlans,
            Asc<FactoryOrderPaymentPlanState>("CampaignId"),
            Asc<FactoryOrderPaymentPlanState>("QuoteId"),
            Asc<FactoryOrderPaymentPlanState>("OrderId"),
            Asc<FactoryOrderPaymentPlanState>("IsPlayerVisible"));
        CreateIndexes(ManufacturingProjects,
            Asc<ManufacturingProjectState>("CampaignId"),
            Asc<ManufacturingProjectState>("ProjectId"),
            Asc<ManufacturingProjectState>("FactoryOrderId"),
            Asc<ManufacturingProjectState>("FacilityId"),
            Asc<ManufacturingProjectState>("SourceBlueprintId"),
            Asc<ManufacturingProjectState>("OwnerEntityId"),
            Asc<ManufacturingProjectState>("ManufacturingStatus"),
            Asc<ManufacturingProjectState>("IsPlayerVisible"),
            Asc<ManufacturingProjectState>("IsArchived"),
            Desc<ManufacturingProjectState>("UpdatedAtUtc"));
        CreateIndexes(ManufacturingStages,
            Asc<ManufacturingStageState>("CampaignId"),
            Asc<ManufacturingStageState>("ManufacturingProjectId"),
            Asc<ManufacturingStageState>("StageType"),
            Asc<ManufacturingStageState>("Status"),
            Asc<ManufacturingStageState>("SortOrder"));
        CreateIndexes(ManufacturingResourcePlans,
            Asc<ManufacturingResourcePlanState>("CampaignId"),
            Asc<ManufacturingResourcePlanState>("ManufacturingProjectId"),
            Asc<ManufacturingResourcePlanState>("StageId"),
            Asc<ManufacturingResourcePlanState>("ResourceId"),
            Asc<ManufacturingResourcePlanState>("Status"));
        CreateIndexes(ManufacturingResourceReservations,
            Asc<ManufacturingResourceReservationState>("CampaignId"),
            Asc<ManufacturingResourceReservationState>("ManufacturingProjectId"),
            Asc<ManufacturingResourceReservationState>("ResourcePlanId"),
            Asc<ManufacturingResourceReservationState>("InventoryItemId"),
            Asc<ManufacturingResourceReservationState>("Status"));
        CreateIndexes(ManufacturingCostLedger,
            Asc<ManufacturingCostLedgerEntry>("CampaignId"),
            Asc<ManufacturingCostLedgerEntry>("ManufacturingProjectId"),
            Asc<ManufacturingCostLedgerEntry>("CostType"),
            Desc<ManufacturingCostLedgerEntry>("CreatedAtUtc"));
        CreateIndexes(ManufacturingPayments,
            Asc<ManufacturingPaymentState>("CampaignId"),
            Asc<ManufacturingPaymentState>("ManufacturingProjectId"),
            Asc<ManufacturingPaymentState>("PaymentKind"),
            Asc<ManufacturingPaymentState>("Status"));
        CreateIndexes(ManufacturingProgressEntries,
            Asc<ManufacturingProgressEntry>("CampaignId"),
            Asc<ManufacturingProgressEntry>("ManufacturingProjectId"),
            Asc<ManufacturingProgressEntry>("StageId"),
            Desc<ManufacturingProgressEntry>("CreatedAtUtc"));
        CreateIndexes(ManufacturingTestPlans,
            Asc<ManufacturingTestPlanState>("CampaignId"),
            Asc<ManufacturingTestPlanState>("ManufacturingProjectId"),
            Asc<ManufacturingTestPlanState>("Status"));
        CreateIndexes(ManufacturingTestResults,
            Asc<ManufacturingTestResultState>("CampaignId"),
            Asc<ManufacturingTestResultState>("ManufacturingProjectId"),
            Asc<ManufacturingTestResultState>("TestPlanId"),
            Asc<ManufacturingTestResultState>("Result"));
        CreateIndexes(ManufacturingDefects,
            Asc<ManufacturingDefectState>("CampaignId"),
            Asc<ManufacturingDefectState>("ManufacturingProjectId"),
            Asc<ManufacturingDefectState>("Status"),
            Asc<ManufacturingDefectState>("IsCritical"),
            Asc<ManufacturingDefectState>("IsPlayerVisible"));
        CreateIndexes(ManufacturingAcceptances,
            Asc<ManufacturingAcceptanceState>("CampaignId"),
            Asc<ManufacturingAcceptanceState>("ManufacturingProjectId"),
            Asc<ManufacturingAcceptanceState>("Status"));
        CreateIndexes(ManufacturedAssets,
            Asc<ManufacturedAssetState>("CampaignId"),
            Asc<ManufacturedAssetState>("ManufacturingProjectId"),
            Asc<ManufacturedAssetState>("AssetStateId"),
            Asc<ManufacturedAssetState>("OwnerEntityId"),
            Asc<ManufacturedAssetState>("Status"),
            Asc<ManufacturedAssetState>("IsPlayerVisible"));
        CharacterModuleStates.Indexes.CreateOne(new CreateIndexModel<CharacterModuleStateDocument>(Builders<CharacterModuleStateDocument>.IndexKeys.Ascending(x => x.CharacterId), new CreateIndexOptions { Unique = true }));
        CharacterAttributeProfiles.Indexes.CreateOne(new CreateIndexModel<CharacterAttributeProfileDocument>(Builders<CharacterAttributeProfileDocument>.IndexKeys.Ascending(x => x.CharacterId), new CreateIndexOptions { Unique = true }));
        CharacterSubAttributeProfiles.Indexes.CreateOne(new CreateIndexModel<CharacterSubAttributeProfileDocument>(Builders<CharacterSubAttributeProfileDocument>.IndexKeys.Ascending(x => x.CharacterId), new CreateIndexOptions { Unique = true }));
        CharacterSkillProfiles.Indexes.CreateOne(new CreateIndexModel<CharacterSkillProfileDocument>(Builders<CharacterSkillProfileDocument>.IndexKeys.Ascending(x => x.CharacterId), new CreateIndexOptions { Unique = true }));
        CharacterDevelopmentProfiles.Indexes.CreateOne(new CreateIndexModel<CharacterDevelopmentProfileDocument>(Builders<CharacterDevelopmentProfileDocument>.IndexKeys.Ascending(x => x.CharacterId), new CreateIndexOptions { Unique = true }));
        CharacterWalletProfiles.Indexes.CreateOne(new CreateIndexModel<CharacterWalletProfileDocument>(Builders<CharacterWalletProfileDocument>.IndexKeys.Ascending(x => x.CharacterId), new CreateIndexOptions { Unique = true }));
        CharacterInventoryProfiles.Indexes.CreateOne(new CreateIndexModel<CharacterInventoryProfileDocument>(Builders<CharacterInventoryProfileDocument>.IndexKeys.Ascending(x => x.CharacterId), new CreateIndexOptions { Unique = true }));
        CharacterReputationProfiles.Indexes.CreateOne(new CreateIndexModel<CharacterReputationProfileDocument>(Builders<CharacterReputationProfileDocument>.IndexKeys.Ascending(x => x.CharacterId), new CreateIndexOptions { Unique = true }));
        CharacterHoldingsProfiles.Indexes.CreateOne(new CreateIndexModel<CharacterHoldingsProfileDocument>(Builders<CharacterHoldingsProfileDocument>.IndexKeys.Ascending(x => x.CharacterId), new CreateIndexOptions { Unique = true }));
        CharacterCompanionProfiles.Indexes.CreateOne(new CreateIndexModel<CharacterCompanionProfileDocument>(Builders<CharacterCompanionProfileDocument>.IndexKeys.Ascending(x => x.CharacterId), new CreateIndexOptions { Unique = true }));
        CharacterRaceOrSpeciesProfiles.Indexes.CreateOne(new CreateIndexModel<CharacterRaceOrSpeciesProfileDocument>(Builders<CharacterRaceOrSpeciesProfileDocument>.IndexKeys.Ascending(x => x.CharacterId)));
        CharacterBodyProfiles.Indexes.CreateOne(new CreateIndexModel<CharacterBodyProfileDocument>(Builders<CharacterBodyProfileDocument>.IndexKeys.Ascending(x => x.CharacterId), new CreateIndexOptions { Unique = true }));
        CharacterKnowledgeProfiles.Indexes.CreateOne(new CreateIndexModel<CharacterKnowledgeProfileDocument>(Builders<CharacterKnowledgeProfileDocument>.IndexKeys.Ascending(x => x.CharacterId), new CreateIndexOptions { Unique = true }));
        CharacterConditionProfiles.Indexes.CreateOne(new CreateIndexModel<CharacterConditionProfileDocument>(Builders<CharacterConditionProfileDocument>.IndexKeys.Ascending(x => x.CharacterId), new CreateIndexOptions { Unique = true }));
        SyncEvents.Indexes.CreateOne(new CreateIndexModel<SyncEvent>(Builders<SyncEvent>.IndexKeys.Ascending(x => x.Revision), new CreateIndexOptions { Unique = true }));
        SyncEvents.Indexes.CreateOne(new CreateIndexModel<SyncEvent>(Builders<SyncEvent>.IndexKeys.Ascending(x => x.Scope).Ascending(x => x.Revision)));
        SyncEvents.Indexes.CreateOne(new CreateIndexModel<SyncEvent>(Builders<SyncEvent>.IndexKeys.Descending(x => x.CreatedUtc)));
        SyncCounters.Indexes.CreateOne(new CreateIndexModel<SyncCounter>(Builders<SyncCounter>.IndexKeys.Ascending(x => x.CounterKey), new CreateIndexOptions { Unique = true }));
        ClassDefinitions.Indexes.CreateOne(new CreateIndexModel<ClassDefinition>(Builders<ClassDefinition>.IndexKeys.Ascending(x => x.Code), new CreateIndexOptions { Unique = true }));
        RaceDefinitions.Indexes.CreateOne(new CreateIndexModel<RaceDefinition>(Builders<RaceDefinition>.IndexKeys.Ascending(x => x.Code), new CreateIndexOptions { Unique = true }));
        DefinitionSkills.Indexes.CreateOne(new CreateIndexModel<SkillDefinition>(Builders<SkillDefinition>.IndexKeys.Ascending(x => x.Code), new CreateIndexOptions { Unique = true }));
        UnifiedDefinitions.Indexes.CreateOne(new CreateIndexModel<UnifiedDefinitionDocument>(Builders<UnifiedDefinitionDocument>.IndexKeys.Ascending(x => x.Category).Ascending(x => x.Id), new CreateIndexOptions { Unique = true }));
        EnsureEconomyRuntimeIndexes(logger);
        EnsureCombatRuntimeIndexes(logger);
        EnsureMapRuntimeIndexes(logger);
    }

    private void EnsureEconomyRuntimeIndexes(IServerLogger logger)
    {
        logger.Debug("economy.index.ensure.start");

        CreateIndexes(FactionStates,
            Asc<FactionState>("CampaignId"),
            Asc<FactionState>("RuleSetId"),
            Asc<FactionState>("DefinitionId"),
            Asc<FactionState>("CountryId"),
            Asc<FactionState>("CityStateId"),
            Asc<FactionState>("Tags"),
            Compound<FactionState>("CampaignId", "DefinitionId"),
            Compound<FactionState>("CampaignId", "CountryId"),
            Compound<FactionState>("CampaignId", "CityStateId"));

        CreateIndexes(OrganizationStates,
            Asc<OrganizationState>("CampaignId"),
            Asc<OrganizationState>("RuleSetId"),
            Asc<OrganizationState>("DefinitionId"),
            Asc<OrganizationState>("ParentFactionId"),
            Asc<OrganizationState>("CountryId"),
            Asc<OrganizationState>("CityStateId"),
            Asc<OrganizationState>("LocationIds"),
            Asc<OrganizationState>("Tags"),
            Compound<OrganizationState>("CampaignId", "DefinitionId"),
            Compound<OrganizationState>("CampaignId", "ParentFactionId"));

        CreateIndexes(MarketStates,
            Asc<MarketState>("CampaignId"),
            Asc<MarketState>("RuleSetId"),
            Asc<MarketState>("DefinitionId"),
            Asc<MarketState>("CountryId"),
            Asc<MarketState>("CityStateId"),
            Asc<MarketState>("LocationId"),
            Asc<MarketState>("MarketTagIds"),
            Asc<MarketState>("IsBlackMarket"),
            Asc<MarketState>("IsActive"),
            Compound<MarketState>("CampaignId", "LocationId"),
            Compound<MarketState>("CampaignId", "CountryId"));

        CreateIndexes(LawStates,
            Asc<LawState>("CampaignId"),
            Asc<LawState>("RuleSetId"),
            Asc<LawState>("DefinitionId"),
            Asc<LawState>("CountryIds"),
            Asc<LawState>("CityStateIds"),
            Asc<LawState>("LawType"),
            Asc<LawState>("IsActive"),
            Compound<LawState>("CampaignId", "DefinitionId"));

        CreateIndexes(RestrictionStates,
            Asc<RestrictionState>("CampaignId"),
            Asc<RestrictionState>("RuleSetId"),
            Asc<RestrictionState>("DefinitionId"),
            Asc<RestrictionState>("CountryIds"),
            Asc<RestrictionState>("CityStateIds"),
            Asc<RestrictionState>("RelatedLawIds"),
            Asc<RestrictionState>("RestrictionType"),
            Asc<RestrictionState>("IsActive"));

        CreateIndexes(AssetStates,
            Asc<AssetState>("CampaignId"),
            Asc<AssetState>("RuleSetId"),
            Asc<AssetState>("DefinitionId"),
            Asc<AssetState>("AssetType"),
            Asc<AssetState>("CountryId"),
            Asc<AssetState>("CityStateId"),
            Asc<AssetState>("LocationId"),
            Asc<AssetState>("OwnerCharacterIds"),
            Asc<AssetState>("OwnerOrganizationIds"),
            Asc<AssetState>("OwnerFactionIds"),
            Asc<AssetState>("IsActive"),
            Compound<AssetState>("CampaignId", "LocationId"),
            Compound<AssetState>("CampaignId", "AssetType"));

        CreateIndexes(EconomyScopeStates,
            Asc<EconomyScopeState>("CampaignId"),
            Asc<EconomyScopeState>("RuleSetId"),
            Asc<EconomyScopeState>("ScopeType"),
            Asc<EconomyScopeState>("CountryId"),
            Asc<EconomyScopeState>("CityStateId"),
            Asc<EconomyScopeState>("RegionId"),
            Compound<EconomyScopeState>("CampaignId", "ScopeType"));

        logger.Debug("economy.index.ensure.done");
    }

    private void EnsureCombatRuntimeIndexes(IServerLogger logger)
    {
        logger.Debug("combat.index.ensure.start");

        CreateIndexes(CombatEncounters,
            Asc<CombatEncounterState>("CampaignId"),
            Asc<CombatEncounterState>("SessionId"),
            Asc<CombatEncounterState>("Status"),
            Asc<CombatEncounterState>("RuleSetId"),
            Asc<CombatEncounterState>("StartedAtUtc"),
            Asc<CombatEncounterState>("CreatedByUserId"),
            Compound<CombatEncounterState>("CampaignId", "Status"),
            Compound<CombatEncounterState>("CampaignId", "SessionId"));

        CreateIndexes(CombatParticipants,
            Asc<CombatParticipantState>("EncounterId"),
            Asc<CombatParticipantState>("CharacterId"),
            Asc<CombatParticipantState>("TeamId"),
            Asc<CombatParticipantState>("ControllerUserId"),
            Asc<CombatParticipantState>("IsNpc"),
            Asc<CombatParticipantState>("IsPlayerControlled"),
            Asc<CombatParticipantState>("IsActive"),
            Asc<CombatParticipantState>("IsDefeated"),
            Compound<CombatParticipantState>("EncounterId", "CharacterId"),
            Compound<CombatParticipantState>("EncounterId", "TeamId"));

        CreateIndexes(CombatTurns,
            Asc<CombatTurnState>("EncounterId"),
            Asc<CombatTurnState>("RoundNumber"),
            Asc<CombatTurnState>("TurnIndex"),
            Asc<CombatTurnState>("ParticipantId"),
            Asc<CombatTurnState>("Status"),
            Compound<CombatTurnState>("EncounterId", "RoundNumber", "TurnIndex"));

        CreateIndexes(CombatRounds,
            Asc<CombatRoundRuntimeState>("EncounterId"),
            Asc<CombatRoundRuntimeState>("RoundNumber"),
            Compound<CombatRoundRuntimeState>("EncounterId", "RoundNumber"));

        CreateIndexes(CombatActions,
            Asc<CombatActionState>("EncounterId"),
            Asc<CombatActionState>("RoundNumber"),
            Asc<CombatActionState>("TurnIndex"),
            Asc<CombatActionState>("ActorParticipantId"),
            Asc<CombatActionState>("ActorUserId"),
            Asc<CombatActionState>("ActionType"),
            Asc<CombatActionState>("CreatedAtUtc"),
            Asc<CombatActionState>("RequestId"),
            Compound<CombatActionState>("EncounterId", "RoundNumber", "TurnIndex"));

        CreateIndexes(CombatRuntimeLogs,
            Asc<CombatRuntimeLogEntry>("EncounterId"),
            Asc<CombatRuntimeLogEntry>("CampaignId"),
            Asc<CombatRuntimeLogEntry>("SessionId"),
            Asc<CombatRuntimeLogEntry>("RoundNumber"),
            Asc<CombatRuntimeLogEntry>("TurnIndex"),
            Asc<CombatRuntimeLogEntry>("ActorParticipantId"),
            Asc<CombatRuntimeLogEntry>("EventType"),
            Asc<CombatRuntimeLogEntry>("Visibility"),
            Asc<CombatRuntimeLogEntry>("CreatedAtUtc"),
            Asc<CombatRuntimeLogEntry>("RequestId"),
            Compound<CombatRuntimeLogEntry>("EncounterId", "CreatedAtUtc"),
            Compound<CombatRuntimeLogEntry>("EncounterId", "RoundNumber", "TurnIndex"));

        CreateIndexes(CombatReplayEvents,
            Asc<CombatReplayEvent>("EncounterId"),
            Asc<CombatReplayEvent>("SequenceNumber"),
            Asc<CombatReplayEvent>("EventType"),
            Asc<CombatReplayEvent>("Visibility"),
            Asc<CombatReplayEvent>("CreatedAtUtc"),
            Asc<CombatReplayEvent>("RequestId"),
            Compound<CombatReplayEvent>("EncounterId", "SequenceNumber"));

        logger.Debug("combat.index.ensure.done");
    }

    private void EnsureMapRuntimeIndexes(IServerLogger logger)
    {
        logger.Debug("map.index.ensure.start");

        CreateIndexes(MapSpaceNodes,
            Asc<MapSpaceNodeState>("CampaignId"),
            Asc<MapSpaceNodeState>("RuleSetId"),
            Asc<MapSpaceNodeState>("ParentId"),
            Asc<MapSpaceNodeState>("NodeType"),
            Asc<MapSpaceNodeState>("SortOrder"),
            Asc<MapSpaceNodeState>("Visibility"),
            Compound<MapSpaceNodeState>("CampaignId", "ParentId"),
            Compound<MapSpaceNodeState>("CampaignId", "NodeType"));

        CreateIndexes(MapCanvases,
            Asc<MapCanvasState>("CampaignId"),
            Asc<MapCanvasState>("RuleSetId"),
            Asc<MapCanvasState>("SpaceNodeId"),
            Asc<MapCanvasState>("MapType"),
            Asc<MapCanvasState>("VisibilityMode"),
            Compound<MapCanvasState>("CampaignId", "SpaceNodeId"),
            Compound<MapCanvasState>("CampaignId", "MapType"));

        CreateIndexes(RoomInteriors,
            Asc<RoomInteriorState>("CampaignId"),
            Asc<RoomInteriorState>("RuleSetId"),
            Asc<RoomInteriorState>("ParentLocationId"),
            Asc<RoomInteriorState>("ParentSceneMapId"),
            Asc<RoomInteriorState>("ParentWorldMapId"),
            Asc<RoomInteriorState>("ParentSpaceNodeId"),
            Asc<RoomInteriorState>("SpaceNodeId"),
            Asc<RoomInteriorState>("RoomType"),
            Asc<RoomInteriorState>("InteriorType"),
            Asc<RoomInteriorState>("VisibilityMode"),
            Asc<RoomInteriorState>("IsArchived"),
            Compound<RoomInteriorState>("CampaignId", "ParentLocationId"),
            Compound<RoomInteriorState>("CampaignId", "ParentSceneMapId"));

        CreateIndexes(WorldMaps,
            Asc<WorldMapState>("CampaignId"),
            Asc<WorldMapState>("RuleSetId"),
            Asc<WorldMapState>("SpaceNodeId"),
            Asc<WorldMapState>("ProjectionMode"),
            Asc<WorldMapState>("VisibilityMode"),
            Asc<WorldMapState>("IsArchived"),
            Compound<WorldMapState>("CampaignId", "SpaceNodeId"),
            Compound<WorldMapState>("CampaignId", "ProjectionMode"));

        CreateIndexes(MapMarkers,
            Asc<MapMarkerState>("MapId"),
            Asc<MapMarkerState>("CampaignId"),
            Asc<MapMarkerState>("MarkerType"),
            Asc<MapMarkerState>("VisibilityMode"),
            Asc<MapMarkerState>("Layer"),
            Asc<MapMarkerState>("LinkedCharacterId"),
            Asc<MapMarkerState>("LinkedCombatParticipantId"),
            Compound<MapMarkerState>("MapId", "MarkerType"),
            Compound<MapMarkerState>("MapId", "Layer"));

        CreateIndexes(MapMarkerBindings,
            Asc<MapMarkerBindingState>("MapId"),
            Asc<MapMarkerBindingState>("MarkerId"),
            Asc<MapMarkerBindingState>("BindingType"),
            Asc<MapMarkerBindingState>("EntityId"),
            Compound<MapMarkerBindingState>("MapId", "MarkerId"));

        CreateIndexes(WorldMapLayers,
            Asc<WorldMapLayerState>("CampaignId"),
            Asc<WorldMapLayerState>("WorldMapId"),
            Asc<WorldMapLayerState>("LayerType"),
            Asc<WorldMapLayerState>("SortOrder"),
            Compound<WorldMapLayerState>("WorldMapId", "LayerType"));

        CreateIndexes(WorldMapLegends,
            Asc<WorldMapLegendState>("MapId"),
            Asc<WorldMapLegendState>("LayerType"),
            Compound<WorldMapLegendState>("MapId", "LayerType"));

        CreateIndexes(MapFogLayers,
            Asc<FogOfWarState>("MapId"),
            Asc<FogOfWarState>("Mode"));

        CreateIndexes(SceneMapActiveLinks,
            Asc<SceneMapActiveLinkState>("CampaignId"),
            Asc<SceneMapActiveLinkState>("MapId"),
            Asc<SceneMapActiveLinkState>("UpdatedAtUtc"),
            Compound<SceneMapActiveLinkState>("CampaignId", "SessionId", "ActiveGroupId", "SceneId", "IsActive"));

        CreateIndexes(LegalJurisdictions,
            Asc<JurisdictionDefinition>("CampaignId"),
            Asc<JurisdictionDefinition>("RuleSetId"),
            Asc<JurisdictionDefinition>("ParentJurisdictionId"),
            Asc<JurisdictionDefinition>("LinkedEntityType"),
            Asc<JurisdictionDefinition>("LinkedEntityId"),
            Asc<JurisdictionDefinition>("IsArchived"),
            Compound<JurisdictionDefinition>("CampaignId", "JurisdictionType"));

        CreateIndexes(LegalProfiles,
            Asc<LegalProfileState>("CampaignId"),
            Asc<LegalProfileState>("JurisdictionId"),
            Asc<LegalProfileState>("IsActive"),
            Asc<LegalProfileState>("IsArchived"),
            Compound<LegalProfileState>("CampaignId", "JurisdictionId", "IsActive"));

        CreateIndexes(LegalRules,
            Asc<LegalRuleDefinition>("CampaignId"),
            Asc<LegalRuleDefinition>("JurisdictionId"),
            Asc<LegalRuleDefinition>("LegalProfileId"),
            Asc<LegalRuleDefinition>("ActionType"),
            Asc<LegalRuleDefinition>("ObjectType"),
            Asc<LegalRuleDefinition>("ObjectCategory"),
            Asc<LegalRuleDefinition>("Priority"),
            Asc<LegalRuleDefinition>("IsArchived"),
            Compound<LegalRuleDefinition>("CampaignId", "JurisdictionId", "ActionType"));

        CreateIndexes(LegalSubjectClassifiers,
            Asc<LegalSubjectClassifier>("CampaignId"),
            Asc<LegalSubjectClassifier>("EntityType"),
            Asc<LegalSubjectClassifier>("EntityId"),
            Asc<LegalSubjectClassifier>("SubjectKind"),
            Compound<LegalSubjectClassifier>("CampaignId", "EntityType", "EntityId"));

        CreateIndexes(LegalRestrictions,
            Asc<LegalRestrictionState>("CampaignId"),
            Asc<LegalRestrictionState>("SourceEntityType"),
            Asc<LegalRestrictionState>("SourceEntityId"),
            Asc<LegalRestrictionState>("LegalStatus"));

        CreateIndexes(LegalRequirements,
            Asc<LegalRequirementState>("CampaignId"),
            Asc<LegalRequirementState>("SourceEntityType"),
            Asc<LegalRequirementState>("SourceEntityId"),
            Asc<LegalRequirementState>("RequirementType"));

        CreateIndexes(LegalLicenseDefinitions,
            Asc<LicenseDefinition>("CampaignId"),
            Asc<LicenseDefinition>("JurisdictionId"),
            Asc<LicenseDefinition>("LicenseType"),
            Asc<LicenseDefinition>("IsArchived"),
            Compound<LicenseDefinition>("CampaignId", "JurisdictionId", "LicenseType"));

        CreateIndexes(LegalEntityLicenses,
            Asc<EntityLicenseState>("CampaignId"),
            Asc<EntityLicenseState>("LicenseDefinitionId"),
            Asc<EntityLicenseState>("JurisdictionId"),
            Asc<EntityLicenseState>("HolderEntityType"),
            Asc<EntityLicenseState>("HolderEntityId"),
            Asc<EntityLicenseState>("HolderUserId"),
            Asc<EntityLicenseState>("Status"),
            Compound<EntityLicenseState>("CampaignId", "HolderEntityType", "HolderEntityId"),
            Compound<EntityLicenseState>("LicenseDefinitionId", "HolderEntityType", "HolderEntityId", "Status"));

        CreateIndexes(LegalLicenseApplications,
            Asc<LicenseApplicationState>("CampaignId"),
            Asc<LicenseApplicationState>("LicenseDefinitionId"),
            Asc<LicenseApplicationState>("ApplicantUserId"),
            Asc<LicenseApplicationState>("ApplicantEntityType"),
            Asc<LicenseApplicationState>("ApplicantEntityId"),
            Asc<LicenseApplicationState>("Status"),
            Desc<LicenseApplicationState>("UpdatedAtUtc"),
            Compound<LicenseApplicationState>("CampaignId", "ApplicantUserId", "Status"));

        CreateIndexes(LegalPermits,
            Asc<PermitState>("CampaignId"),
            Asc<PermitState>("PermitType"),
            Asc<PermitState>("HolderEntityType"),
            Asc<PermitState>("HolderEntityId"),
            Asc<PermitState>("JurisdictionId"),
            Asc<PermitState>("Status"));

        CreateIndexes(LegalCheckRecords,
            Asc<LegalCheckRecordState>("CampaignId"),
            Asc<LegalCheckRecordState>("JurisdictionId"),
            Asc<LegalCheckRecordState>("ActorUserId"),
            Asc<LegalCheckRecordState>("ActionType"),
            Asc<LegalCheckRecordState>("LegalStatus"),
            Asc<LegalCheckRecordState>("RequiresGMReview"),
            Desc<LegalCheckRecordState>("CheckedAtUtc"),
            Compound<LegalCheckRecordState>("CampaignId", "RequiresGMReview", "LegalStatus"));

        CreateIndexes(LegalEnforcementRiskProfiles,
            Asc<EnforcementRiskProfile>("CampaignId"),
            Asc<EnforcementRiskProfile>("JurisdictionId"),
            Asc<EnforcementRiskProfile>("RiskLevel"),
            Asc<EnforcementRiskProfile>("IsArchived"));

        CreateIndexes(LegalDeJureDeFactoStates,
            Asc<DeJureDeFactoLawState>("CampaignId"),
            Asc<DeJureDeFactoLawState>("JurisdictionId"),
            Asc<DeJureDeFactoLawState>("LegalProfileId"));

        CreateIndexes(LegalProductionLegalityStates,
            Asc<ProductionLegalityState>("CampaignId"),
            Asc<ProductionLegalityState>("SourceEntityType"),
            Asc<ProductionLegalityState>("SourceEntityId"),
            Asc<ProductionLegalityState>("ProductionMode"),
            Asc<ProductionLegalityState>("LegalStatus"),
            Compound<ProductionLegalityState>("CampaignId", "SourceEntityType", "SourceEntityId"));

        CreateIndexes(PlayerProposalDrafts,
            Asc<PlayerProposalDraftState>("ProposalDraftId"),
            Asc<PlayerProposalDraftState>("CampaignId"),
            Asc<PlayerProposalDraftState>("CreatedByUserId"),
            Asc<PlayerProposalDraftState>("CharacterId"),
            Asc<PlayerProposalDraftState>("CompanionId"),
            Asc<PlayerProposalDraftState>("GroupId"),
            Asc<PlayerProposalDraftState>("ProposalType"),
            Asc<PlayerProposalDraftState>("ProposalStatus"),
            Asc<PlayerProposalDraftState>("LinkedPlayerRequestId"),
            Asc<PlayerProposalDraftState>("LinkedProjectId"),
            Desc<PlayerProposalDraftState>("UpdatedAtUtc"),
            Compound<PlayerProposalDraftState>("CampaignId", "ProposalStatus"),
            Compound<PlayerProposalDraftState>("CampaignId", "CreatedByUserId"),
            Compound<PlayerProposalDraftState>("CampaignId", "ProposalType"));

        CreateIndexes(PlayerProposalFields,
            Asc<PlayerProposalFieldState>("ProposalDraftId"),
            Asc<PlayerProposalFieldState>("FieldKey"),
            Asc<PlayerProposalFieldState>("IsGMOnly"));

        CreateIndexes(PlayerProposalValidations,
            Asc<PlayerProposalValidationResult>("ValidationId"),
            Asc<PlayerProposalValidationResult>("ProposalDraftId"),
            Asc<PlayerProposalValidationResult>("CampaignId"),
            Asc<PlayerProposalValidationResult>("Status"),
            Desc<PlayerProposalValidationResult>("CheckedAtUtc"));

        CreateIndexes(PlayerProposalReviews,
            Asc<PlayerProposalReviewState>("ReviewId"),
            Asc<PlayerProposalReviewState>("ProposalDraftId"),
            Asc<PlayerProposalReviewState>("LinkedPlayerRequestId"),
            Asc<PlayerProposalReviewState>("CampaignId"),
            Asc<PlayerProposalReviewState>("ReviewStatus"),
            Desc<PlayerProposalReviewState>("UpdatedAtUtc"));

        CreateIndexes(PlayerProposalConversions,
            Asc<PlayerProposalConversionState>("ConversionId"),
            Asc<PlayerProposalConversionState>("ProposalDraftId"),
            Asc<PlayerProposalConversionState>("ConversionType"),
            Asc<PlayerProposalConversionState>("TargetEntityType"),
            Asc<PlayerProposalConversionState>("TargetEntityId"),
            Desc<PlayerProposalConversionState>("ConvertedAtUtc"));

        CreateIndexes(ProposalTemplateDefinitions,
            Asc<ProposalTemplateDefinition>("ProposalTemplateId"),
            Asc<ProposalTemplateDefinition>("CampaignId"),
            Asc<ProposalTemplateDefinition>("RuleSetId"),
            Asc<ProposalTemplateDefinition>("ProposalType"),
            Asc<ProposalTemplateDefinition>("IsPlayerVisible"),
            Asc<ProposalTemplateDefinition>("IsArchived"));

        CreateIndexes(ProposalAttachmentLinks,
            Asc<PlayerProposalAttachmentLinkState>("ProposalDraftId"),
            Asc<PlayerProposalAttachmentLinkState>("AttachmentType"),
            Asc<PlayerProposalAttachmentLinkState>("EntityId"),
            Asc<PlayerProposalAttachmentLinkState>("IsPlayerVisible"));

        logger.Debug("map.index.ensure.done");
    }

    private static IndexKeysDefinition<T> Asc<T>(string field)
    {
        return Builders<T>.IndexKeys.Ascending(field);
    }

    private static IndexKeysDefinition<T> Desc<T>(string field)
    {
        return Builders<T>.IndexKeys.Descending(field);
    }

    private static IndexKeysDefinition<T> Compound<T>(params string[] fields)
    {
        var keys = new List<IndexKeysDefinition<T>>();
        foreach (var field in fields)
        {
            keys.Add(Builders<T>.IndexKeys.Ascending(field));
        }

        return Builders<T>.IndexKeys.Combine(keys);
    }

    private static void CreateIndexes<T>(IMongoCollection<T> collection, params IndexKeysDefinition<T>[] keys)
    {
        foreach (var key in keys)
        {
            collection.Indexes.CreateOne(new CreateIndexModel<T>(key));
        }
    }
}

public class MongoRepository<T> : IRepository<T> where T : EntityBase
{
    private readonly IMongoCollection<T> _collection;

    public MongoRepository(IMongoCollection<T> collection)
    {
        _collection = collection;
    }

    public T? GetById(string id)
    {
        return _collection.Find(x => x.Id == id).FirstOrDefault();
    }

    public IReadOnlyCollection<T> Find(FilterDefinition<T> filter)
    {
        return _collection.Find(filter).ToList();
    }

    public void Insert(T entity)
    {
        entity.CreatedUtc = DateTime.UtcNow;
        entity.UpdatedUtc = DateTime.UtcNow;
        _collection.InsertOne(entity);
    }

    public void Replace(T entity)
    {
        entity.UpdatedUtc = DateTime.UtcNow;
        _collection.ReplaceOne(x => x.Id == entity.Id, entity, new ReplaceOptions { IsUpsert = false });
    }
}

public class MongoRepositoryFactory : INriRepositoryFactory
{
    public MongoRepositoryFactory(MongoContext context, IServerLogger logger)
    {
        Accounts = new MongoRepository<UserAccount>(context.Accounts);
        Profiles = new MongoRepository<UserProfile>(context.Profiles);
        Characters = new MongoRepository<Character>(context.Characters);
        Presence = new MongoRepository<SessionUserState>(context.Presence);
        CurrentSessions = new MongoRepository<CurrentSessionState>(context.CurrentSessions);
        CharacterGroups = new MongoRepository<CharacterGroupState>(context.CharacterGroups);
        CharacterGroupMembers = new MongoRepository<CharacterGroupMemberState>(context.CharacterGroupMembers);
        CharacterOwnerships = new MongoRepository<CharacterOwnershipState>(context.CharacterOwnerships);
        CharacterOwnershipAudit = new MongoRepository<CharacterOwnershipAuditEntry>(context.CharacterOwnershipAudit);
        Locks = new MongoRepository<EntityLock>(context.Locks);
        AuditLogs = new MongoRepository<AuditLogEntry>(context.AuditLogs);
        ActionRequests = new MongoRepository<ActionRequest>(context.ActionRequests);
        DiceRequests = new MongoRepository<DiceRollRequest>(context.DiceRequests);
        PlayerRequests = new MongoRepository<PlayerRequestState>(context.PlayerRequests);
        PlayerRequestComments = new MongoRepository<PlayerRequestCommentState>(context.PlayerRequestComments);
        WorldCalendarDefinitions = new MongoRepository<WorldCalendarDefinition>(context.WorldCalendarDefinitions);
        WorldCalendarSeasons = new MongoRepository<WorldCalendarSeasonDefinition>(context.WorldCalendarSeasons);
        WorldCalendarMonths = new MongoRepository<WorldCalendarMonthDefinition>(context.WorldCalendarMonths);
        CampaignWorldTimes = new MongoRepository<CampaignWorldTimeState>(context.CampaignWorldTimes);
        WorldCalendarEvents = new MongoRepository<WorldCalendarEventState>(context.WorldCalendarEvents);
        WorldCalendarEventVersions = new MongoRepository<WorldCalendarEventVersionState>(context.WorldCalendarEventVersions);
        WorldCalendarHolidays = new MongoRepository<WorldCalendarHolidayDefinition>(context.WorldCalendarHolidays);
        WorldCalendarReminders = new MongoRepository<WorldCalendarReminderState>(context.WorldCalendarReminders);
        RealScheduleEvents = new MongoRepository<RealScheduleEventState>(context.RealScheduleEvents);
        RealScheduleParticipants = new MongoRepository<RealScheduleParticipantState>(context.RealScheduleParticipants);
        GMNotes = new MongoRepository<GMNoteState>(context.GMNotes);
        GMNoteFolders = new MongoRepository<GMNoteFolderState>(context.GMNoteFolders);
        GMNoteLinks = new MongoRepository<GMNoteEntityLinkState>(context.GMNoteLinks);
        GMNoteAudit = new MongoRepository<GMNoteAuditEntry>(context.GMNoteAudit);
        EventJournalEntries = new MongoRepository<EventJournalEntryState>(context.EventJournalEntries);
        EventJournalLinks = new MongoRepository<EventJournalEntityLinkState>(context.EventJournalLinks);
        EventJournalAnnotations = new MongoRepository<EventJournalAnnotationState>(context.EventJournalAnnotations);
        EventJournalAudit = new MongoRepository<EventJournalAuditEntry>(context.EventJournalAudit);
        ChatMessages = new MongoRepository<ChatMessage>(context.ChatMessages);
        ChatReadStates = new MongoRepository<ChatReadState>(context.ChatReadStates);
        SessionChatSettings = new MongoRepository<SessionChatSettings>(context.SessionChatSettings);
        ChatThrottleStates = new MongoRepository<ChatUserThrottleState>(context.ChatThrottleStates);
        AudioStates = new MongoRepository<SessionAudioState>(context.AudioStates);
        AudioTracks = new MongoRepository<AudioTrackDefinition>(context.AudioTracks);
        AudioClientSettings = new MongoRepository<AudioClientSettingsState>(context.AudioClientSettings);
        Combats = new MongoRepository<CombatState>(context.Combats);
        CombatLogs = new MongoRepository<CombatLogEntry>(context.CombatLogs);
        ClassTrees = new MongoRepository<ClassTreeDefinition>(context.ClassTrees);
        SkillDefinitions = new MongoRepository<SkillDefinitionRecord>(context.SkillDefinitions);
        DefinitionVersions = new MongoRepository<DefinitionVersion>(context.DefinitionVersions);
        Notes = new MongoRepository<Note>(context.Notes);
        References = new MongoRepository<ReferenceEntry>(context.References);
        UpdateVersions = new MongoRepository<UpdateVersionInfo>(context.UpdateVersions);
        Backups = new MongoRepository<BackupSnapshot>(context.Backups);
        BackupRecords = new MongoRepository<BackupRecordState>(context.BackupRecords);
        BackupRestoreOperations = new MongoRepository<BackupRestoreOperationState>(context.BackupRestoreOperations);
        BackupMaintenanceStates = new MongoRepository<BackupMaintenanceState>(context.BackupMaintenanceStates);
        FeatureFlagOverrides = new MongoRepository<FeatureFlagOverrideState>(context.FeatureFlagOverrides);
        Projects = new MongoRepository<ProjectBaseState>(context.Projects);
        ProjectStages = new MongoRepository<ProjectStageState>(context.ProjectStages);
        ProjectParticipants = new MongoRepository<ProjectParticipantState>(context.ProjectParticipants);
        ProjectRequirements = new MongoRepository<ProjectRequirementState>(context.ProjectRequirements);
        ProjectResourceRequirements = new MongoRepository<ProjectResourceRequirementState>(context.ProjectResourceRequirements);
        ProjectProgressEntries = new MongoRepository<ProjectProgressEntryState>(context.ProjectProgressEntries);
        ProjectApprovals = new MongoRepository<ProjectApprovalState>(context.ProjectApprovals);
        ProjectAuditEntries = new MongoRepository<ProjectAuditEntryState>(context.ProjectAuditEntries);
        ProjectEntityLinks = new MongoRepository<ProjectEntityLinkState>(context.ProjectEntityLinks);
        ProjectProposals = new MongoRepository<ProjectProposalBoundaryState>(context.ProjectProposals);
        KnowledgeDefinitions = new MongoRepository<KnowledgeDefinition>(context.KnowledgeDefinitions);
        EntityKnowledgeStates = new MongoRepository<EntityKnowledgeState>(context.EntityKnowledgeStates);
        AppliedKnowledgeDefinitions = new MongoRepository<AppliedKnowledgeDefinition>(context.AppliedKnowledgeDefinitions);
        KnowledgeSources = new MongoRepository<KnowledgeSourceState>(context.KnowledgeSources);
        ResearchResults = new MongoRepository<ResearchResultState>(context.ResearchResults);
        ExperienceCoinLedger = new MongoRepository<ExperienceCoinLedgerEntry>(context.ExperienceCoinLedger);
        CraftingRecipes = new MongoRepository<CraftingRecipeDefinition>(context.CraftingRecipes);
        CraftingRecipeIngredients = new MongoRepository<RecipeIngredientRequirement>(context.CraftingRecipeIngredients);
        CraftingRecipeTools = new MongoRepository<RecipeToolRequirement>(context.CraftingRecipeTools);
        CraftingRecipeFacilities = new MongoRepository<RecipeFacilityRequirement>(context.CraftingRecipeFacilities);
        CraftingRecipeKnowledgeRequirements = new MongoRepository<RecipeKnowledgeRequirement>(context.CraftingRecipeKnowledgeRequirements);
        CraftingProjects = new MongoRepository<CraftingProjectState>(context.CraftingProjects);
        CraftingReservations = new MongoRepository<CraftingResourceReservationState>(context.CraftingReservations);
        CraftingResults = new MongoRepository<CraftingProjectItemResult>(context.CraftingResults);
        EngineeringPlatforms = new MongoRepository<EngineeringPlatformDefinition>(context.EngineeringPlatforms);
        EngineeringSizeClasses = new MongoRepository<EngineeringPlatformSizeClassDefinition>(context.EngineeringSizeClasses);
        EngineeringModules = new MongoRepository<EngineeringModuleDefinition>(context.EngineeringModules);
        EngineeringModuleSlotRequirements = new MongoRepository<EngineeringModuleSlotRequirement>(context.EngineeringModuleSlotRequirements);
        EngineeringModuleCompatibilityRules = new MongoRepository<EngineeringModuleCompatibilityRule>(context.EngineeringModuleCompatibilityRules);
        EngineeringPowerProfiles = new MongoRepository<EngineeringPowerProfileDefinition>(context.EngineeringPowerProfiles);
        EngineeringWeaponProfiles = new MongoRepository<EngineeringWeaponProfileDefinition>(context.EngineeringWeaponProfiles);
        EngineeringPresets = new MongoRepository<PresetVehicleDesignDefinition>(context.EngineeringPresets);
        EngineeringDesignDrafts = new MongoRepository<VehicleDesignDraft>(context.EngineeringDesignDrafts);
        EngineeringProjects = new MongoRepository<EngineeringDesignProjectState>(context.EngineeringProjects);
        EngineeringValidationResults = new MongoRepository<EngineeringDesignValidationResult>(context.EngineeringValidationResults);
        EngineeringCostEstimates = new MongoRepository<EngineeringDesignCostEstimate>(context.EngineeringCostEstimates);
        EngineeringBlueprints = new MongoRepository<VehicleDesignBlueprint>(context.EngineeringBlueprints);
        EngineeringBlueprintReferences = new MongoRepository<EngineeringBlueprintReference>(context.EngineeringBlueprintReferences);
        ProductionFacilityDefinitions = new MongoRepository<ProductionFacilityDefinition>(context.ProductionFacilityDefinitions);
        ProductionFacilities = new MongoRepository<ProductionFacilityState>(context.ProductionFacilities);
        ProductionCapabilities = new MongoRepository<ProductionFacilityCapabilityState>(context.ProductionCapabilities);
        ProductionProcesses = new MongoRepository<ProductionProcessDefinition>(context.ProductionProcesses);
        ProductionCapacities = new MongoRepository<ProductionFacilityCapacityState>(context.ProductionCapacities);
        ProductionQueueSlots = new MongoRepository<ProductionQueueSlotState>(context.ProductionQueueSlots);
        FactoryQuotes = new MongoRepository<FactoryQuoteState>(context.FactoryQuotes);
        FactoryOrders = new MongoRepository<FactoryOrderState>(context.FactoryOrders);
        FactoryOrderLines = new MongoRepository<FactoryOrderLineState>(context.FactoryOrderLines);
        FactoryOrderTerms = new MongoRepository<FactoryOrderTermState>(context.FactoryOrderTerms);
        FactoryPaymentPlans = new MongoRepository<FactoryOrderPaymentPlanState>(context.FactoryPaymentPlans);
        ManufacturingProjects = new MongoRepository<ManufacturingProjectState>(context.ManufacturingProjects);
        ManufacturingStages = new MongoRepository<ManufacturingStageState>(context.ManufacturingStages);
        ManufacturingResourcePlans = new MongoRepository<ManufacturingResourcePlanState>(context.ManufacturingResourcePlans);
        ManufacturingResourceReservations = new MongoRepository<ManufacturingResourceReservationState>(context.ManufacturingResourceReservations);
        ManufacturingCostLedger = new MongoRepository<ManufacturingCostLedgerEntry>(context.ManufacturingCostLedger);
        ManufacturingPayments = new MongoRepository<ManufacturingPaymentState>(context.ManufacturingPayments);
        ManufacturingProgressEntries = new MongoRepository<ManufacturingProgressEntry>(context.ManufacturingProgressEntries);
        ManufacturingTestPlans = new MongoRepository<ManufacturingTestPlanState>(context.ManufacturingTestPlans);
        ManufacturingTestResults = new MongoRepository<ManufacturingTestResultState>(context.ManufacturingTestResults);
        ManufacturingDefects = new MongoRepository<ManufacturingDefectState>(context.ManufacturingDefects);
        ManufacturingAcceptances = new MongoRepository<ManufacturingAcceptanceState>(context.ManufacturingAcceptances);
        ManufacturedAssets = new MongoRepository<ManufacturedAssetState>(context.ManufacturedAssets);
        SyncEvents = new MongoRepository<SyncEvent>(context.SyncEvents);
        SyncCounters = new MongoRepository<SyncCounter>(context.SyncCounters);
        ClassDefinitions = new ClassDefinitionRepository(context.ClassDefinitions);
        RaceDefinitions = new RaceDefinitionRepository(context.RaceDefinitions);
        DefinitionSkills = new SkillDefinitionRepository(context.DefinitionSkills);
        FactionStates = new FactionStateRepository(context.FactionStates, logger);
        OrganizationStates = new OrganizationStateRepository(context.OrganizationStates, logger);
        MarketStates = new MarketStateRepository(context.MarketStates, logger);
        LawStates = new LawStateRepository(context.LawStates, logger);
        RestrictionStates = new RestrictionStateRepository(context.RestrictionStates, logger);
        AssetStates = new AssetStateRepository(context.AssetStates, logger);
        EconomyScopeStates = new EconomyScopeStateRepository(context.EconomyScopeStates, logger);
        CombatEncounters = new CombatEncounterRepository(context.CombatEncounters, logger);
        CombatParticipants = new CombatParticipantRepository(context.CombatParticipants, logger);
        CombatTurns = new CombatTurnRepository(context.CombatTurns, logger);
        CombatRounds = new CombatRoundRepository(context.CombatRounds, logger);
        CombatActions = new CombatActionRepository(context.CombatActions, logger);
        CombatRuntimeLogs = new CombatLogRepository(context.CombatRuntimeLogs, logger);
        CombatReplayEvents = new CombatReplayEventRepository(context.CombatReplayEvents, logger);
        MapSpaceNodes = new MapSpaceNodeRepository(context.MapSpaceNodes, logger);
        MapCanvases = new MapCanvasRepository(context.MapCanvases, logger);
        RoomInteriors = new RoomInteriorRepository(context.RoomInteriors, logger);
        WorldMaps = new WorldMapStateRepository(context.WorldMaps, logger);
        MapMarkers = new MapMarkerRepository(context.MapMarkers, logger);
        MapMarkerBindings = new MapMarkerBindingRepository(context.MapMarkerBindings, logger);
        WorldMapLayers = new WorldMapLayerRepository(context.WorldMapLayers, logger);
        WorldMapLegends = new WorldMapLegendRepository(context.WorldMapLegends, logger);
        MapFogLayers = new MapFogLayerRepository(context.MapFogLayers, logger);
        SceneMapActiveLinks = new SceneMapActiveLinkRepository(context.SceneMapActiveLinks, logger);
    }

    public IRepository<UserAccount> Accounts { get; }
    public IRepository<UserProfile> Profiles { get; }
    public IRepository<Character> Characters { get; }
    public IRepository<SessionUserState> Presence { get; }
    public IRepository<CurrentSessionState> CurrentSessions { get; }
    public IRepository<CharacterGroupState> CharacterGroups { get; }
    public IRepository<CharacterGroupMemberState> CharacterGroupMembers { get; }
    public IRepository<CharacterOwnershipState> CharacterOwnerships { get; }
    public IRepository<CharacterOwnershipAuditEntry> CharacterOwnershipAudit { get; }
    public IRepository<EntityLock> Locks { get; }
    public IRepository<AuditLogEntry> AuditLogs { get; }
    public IRepository<ActionRequest> ActionRequests { get; }
    public IRepository<DiceRollRequest> DiceRequests { get; }
    public IRepository<PlayerRequestState> PlayerRequests { get; }
    public IRepository<PlayerRequestCommentState> PlayerRequestComments { get; }
    public IRepository<WorldCalendarDefinition> WorldCalendarDefinitions { get; }
    public IRepository<WorldCalendarSeasonDefinition> WorldCalendarSeasons { get; }
    public IRepository<WorldCalendarMonthDefinition> WorldCalendarMonths { get; }
    public IRepository<CampaignWorldTimeState> CampaignWorldTimes { get; }
    public IRepository<WorldCalendarEventState> WorldCalendarEvents { get; }
    public IRepository<WorldCalendarEventVersionState> WorldCalendarEventVersions { get; }
    public IRepository<WorldCalendarHolidayDefinition> WorldCalendarHolidays { get; }
    public IRepository<WorldCalendarReminderState> WorldCalendarReminders { get; }
    public IRepository<RealScheduleEventState> RealScheduleEvents { get; }
    public IRepository<RealScheduleParticipantState> RealScheduleParticipants { get; }
    public IRepository<GMNoteState> GMNotes { get; }
    public IRepository<GMNoteFolderState> GMNoteFolders { get; }
    public IRepository<GMNoteEntityLinkState> GMNoteLinks { get; }
    public IRepository<GMNoteAuditEntry> GMNoteAudit { get; }
    public IRepository<EventJournalEntryState> EventJournalEntries { get; }
    public IRepository<EventJournalEntityLinkState> EventJournalLinks { get; }
    public IRepository<EventJournalAnnotationState> EventJournalAnnotations { get; }
    public IRepository<EventJournalAuditEntry> EventJournalAudit { get; }
    public IRepository<ChatMessage> ChatMessages { get; }
    public IRepository<ChatReadState> ChatReadStates { get; }
    public IRepository<SessionChatSettings> SessionChatSettings { get; }
    public IRepository<ChatUserThrottleState> ChatThrottleStates { get; }
    public IRepository<SessionAudioState> AudioStates { get; }
    public IRepository<AudioTrackDefinition> AudioTracks { get; }
    public IRepository<AudioClientSettingsState> AudioClientSettings { get; }
    public IRepository<CombatState> Combats { get; }
    public IRepository<CombatLogEntry> CombatLogs { get; }
    public IRepository<ClassTreeDefinition> ClassTrees { get; }
    public IRepository<SkillDefinitionRecord> SkillDefinitions { get; }
    public IRepository<DefinitionVersion> DefinitionVersions { get; }
    public IRepository<Note> Notes { get; }
    public IRepository<ReferenceEntry> References { get; }
    public IRepository<UpdateVersionInfo> UpdateVersions { get; }
    public IRepository<BackupSnapshot> Backups { get; }
    public IRepository<BackupRecordState> BackupRecords { get; }
    public IRepository<BackupRestoreOperationState> BackupRestoreOperations { get; }
    public IRepository<BackupMaintenanceState> BackupMaintenanceStates { get; }
    public IRepository<FeatureFlagOverrideState> FeatureFlagOverrides { get; }
    public IRepository<ProjectBaseState> Projects { get; }
    public IRepository<ProjectStageState> ProjectStages { get; }
    public IRepository<ProjectParticipantState> ProjectParticipants { get; }
    public IRepository<ProjectRequirementState> ProjectRequirements { get; }
    public IRepository<ProjectResourceRequirementState> ProjectResourceRequirements { get; }
    public IRepository<ProjectProgressEntryState> ProjectProgressEntries { get; }
    public IRepository<ProjectApprovalState> ProjectApprovals { get; }
    public IRepository<ProjectAuditEntryState> ProjectAuditEntries { get; }
    public IRepository<ProjectEntityLinkState> ProjectEntityLinks { get; }
    public IRepository<ProjectProposalBoundaryState> ProjectProposals { get; }
    public IRepository<KnowledgeDefinition> KnowledgeDefinitions { get; }
    public IRepository<EntityKnowledgeState> EntityKnowledgeStates { get; }
    public IRepository<AppliedKnowledgeDefinition> AppliedKnowledgeDefinitions { get; }
    public IRepository<KnowledgeSourceState> KnowledgeSources { get; }
    public IRepository<ResearchResultState> ResearchResults { get; }
    public IRepository<ExperienceCoinLedgerEntry> ExperienceCoinLedger { get; }
    public IRepository<CraftingRecipeDefinition> CraftingRecipes { get; }
    public IRepository<RecipeIngredientRequirement> CraftingRecipeIngredients { get; }
    public IRepository<RecipeToolRequirement> CraftingRecipeTools { get; }
    public IRepository<RecipeFacilityRequirement> CraftingRecipeFacilities { get; }
    public IRepository<RecipeKnowledgeRequirement> CraftingRecipeKnowledgeRequirements { get; }
    public IRepository<CraftingProjectState> CraftingProjects { get; }
    public IRepository<CraftingResourceReservationState> CraftingReservations { get; }
    public IRepository<CraftingProjectItemResult> CraftingResults { get; }
    public IRepository<EngineeringPlatformDefinition> EngineeringPlatforms { get; }
    public IRepository<EngineeringPlatformSizeClassDefinition> EngineeringSizeClasses { get; }
    public IRepository<EngineeringModuleDefinition> EngineeringModules { get; }
    public IRepository<EngineeringModuleSlotRequirement> EngineeringModuleSlotRequirements { get; }
    public IRepository<EngineeringModuleCompatibilityRule> EngineeringModuleCompatibilityRules { get; }
    public IRepository<EngineeringPowerProfileDefinition> EngineeringPowerProfiles { get; }
    public IRepository<EngineeringWeaponProfileDefinition> EngineeringWeaponProfiles { get; }
    public IRepository<PresetVehicleDesignDefinition> EngineeringPresets { get; }
    public IRepository<VehicleDesignDraft> EngineeringDesignDrafts { get; }
    public IRepository<EngineeringDesignProjectState> EngineeringProjects { get; }
    public IRepository<EngineeringDesignValidationResult> EngineeringValidationResults { get; }
    public IRepository<EngineeringDesignCostEstimate> EngineeringCostEstimates { get; }
    public IRepository<VehicleDesignBlueprint> EngineeringBlueprints { get; }
    public IRepository<EngineeringBlueprintReference> EngineeringBlueprintReferences { get; }
    public IRepository<ProductionFacilityDefinition> ProductionFacilityDefinitions { get; }
    public IRepository<ProductionFacilityState> ProductionFacilities { get; }
    public IRepository<ProductionFacilityCapabilityState> ProductionCapabilities { get; }
    public IRepository<ProductionProcessDefinition> ProductionProcesses { get; }
    public IRepository<ProductionFacilityCapacityState> ProductionCapacities { get; }
    public IRepository<ProductionQueueSlotState> ProductionQueueSlots { get; }
    public IRepository<FactoryQuoteState> FactoryQuotes { get; }
    public IRepository<FactoryOrderState> FactoryOrders { get; }
    public IRepository<FactoryOrderLineState> FactoryOrderLines { get; }
    public IRepository<FactoryOrderTermState> FactoryOrderTerms { get; }
    public IRepository<FactoryOrderPaymentPlanState> FactoryPaymentPlans { get; }
    public IRepository<ManufacturingProjectState> ManufacturingProjects { get; }
    public IRepository<ManufacturingStageState> ManufacturingStages { get; }
    public IRepository<ManufacturingResourcePlanState> ManufacturingResourcePlans { get; }
    public IRepository<ManufacturingResourceReservationState> ManufacturingResourceReservations { get; }
    public IRepository<ManufacturingCostLedgerEntry> ManufacturingCostLedger { get; }
    public IRepository<ManufacturingPaymentState> ManufacturingPayments { get; }
    public IRepository<ManufacturingProgressEntry> ManufacturingProgressEntries { get; }
    public IRepository<ManufacturingTestPlanState> ManufacturingTestPlans { get; }
    public IRepository<ManufacturingTestResultState> ManufacturingTestResults { get; }
    public IRepository<ManufacturingDefectState> ManufacturingDefects { get; }
    public IRepository<ManufacturingAcceptanceState> ManufacturingAcceptances { get; }
    public IRepository<ManufacturedAssetState> ManufacturedAssets { get; }
    public IRepository<SyncEvent> SyncEvents { get; }
    public IRepository<SyncCounter> SyncCounters { get; }
    public IClassDefinitionRepository ClassDefinitions { get; }
    public IRaceDefinitionRepository RaceDefinitions { get; }
    public ISkillDefinitionRepository DefinitionSkills { get; }
    public IFactionStateRepository FactionStates { get; }
    public IOrganizationStateRepository OrganizationStates { get; }
    public IMarketStateRepository MarketStates { get; }
    public ILawStateRepository LawStates { get; }
    public IRestrictionStateRepository RestrictionStates { get; }
    public IAssetStateRepository AssetStates { get; }
    public IEconomyScopeStateRepository EconomyScopeStates { get; }
    public ICombatEncounterRepository CombatEncounters { get; }
    public ICombatParticipantRepository CombatParticipants { get; }
    public ICombatTurnRepository CombatTurns { get; }
    public ICombatRoundRepository CombatRounds { get; }
    public ICombatActionRepository CombatActions { get; }
    public ICombatLogRepository CombatRuntimeLogs { get; }
    public ICombatReplayEventRepository CombatReplayEvents { get; }
    public IMapSpaceNodeRepository MapSpaceNodes { get; }
    public IMapCanvasRepository MapCanvases { get; }
    public IRoomInteriorRepository RoomInteriors { get; }
    public IWorldMapStateRepository WorldMaps { get; }
    public IMapMarkerRepository MapMarkers { get; }
    public IMapMarkerBindingRepository MapMarkerBindings { get; }
    public IWorldMapLayerRepository WorldMapLayers { get; }
    public IWorldMapLegendRepository WorldMapLegends { get; }
    public IMapFogLayerRepository MapFogLayers { get; }
    public ISceneMapActiveLinkRepository SceneMapActiveLinks { get; }
}
