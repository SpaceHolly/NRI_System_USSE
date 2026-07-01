using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;

namespace Nri.Shared.Contracts;

public enum ResponseStatus
{
    Ok,
    Error,
    Unauthorized,
    Forbidden,
    ValidationFailed,
    NotFound,
    Conflict
}

public enum ErrorCode
{
    None,
    InvalidRequest,
    Unauthorized,
    Forbidden,
    ValidationFailed,
    NotFound,
    Conflict,
    InternalError,
    InvalidCommand,
    InvalidToken
}

public static class CommandNames
{
    public const string AuthRegister = "auth.register";
    public const string AuthLogin = "auth.login";
    public const string AuthLogout = "auth.logout";
    public const string AuthChangePassword = "auth.changePassword";

    public const string ProfileGet = "profile.get";
    public const string ProfileUpdate = "profile.update";

    public const string AdminAccountsPending = "admin.accounts.pending";
    public const string AdminAccountsApprove = "admin.accounts.approve";
    public const string AdminAccountsArchive = "admin.accounts.archive";
    public const string AdminAccountProfile = "admin.accounts.profile";
    public const string AdminPlayersList = "admin.players.list";
    public const string AdminAccountRolesSet = "admin.account.roles.set";
    public const string AdminAccountGrantAdmin = "admin.account.grantAdmin";
    public const string AdminAccountRevokeAdmin = "admin.account.revokeAdmin";
    public const string AdminAccountReject = "admin.account.reject";
    public const string AdminAccountBlock = "admin.account.block";
    public const string AdminAccountUnblock = "admin.account.unblock";
    public const string AdminAccountResetPassword = "admin.account.resetPassword";

    public const string CharacterListMine = "character.list.mine";
    public const string CharacterListByOwner = "character.list.byOwner";
    public const string CharacterGetActive = "character.get.active";
    public const string CharacterGetSummary = "character.get.summary";
    public const string CharacterGetCompanions = "character.get.companions";
    public const string CharacterGetInventory = "character.get.inventory";
    public const string CharacterGetReputation = "character.get.reputation";
    public const string CharacterGetHoldings = "character.get.holdings";

    public const string CharacterInventoryGet = "character.inventory.get";
    public const string CharacterInventoryItemAdd = "character.inventory.item.add";
    public const string CharacterInventoryItemAddFromCatalog = "character.inventory.item.addFromCatalog";
    public const string CharacterInventoryItemUpdate = "character.inventory.item.update";
    public const string CharacterInventoryItemRemove = "character.inventory.item.remove";
    public const string CharacterInventoryItemToggleEquip = "character.inventory.item.toggleEquip";
    public const string InventoryDiagnosticsFull = "inventory.diagnostics.full";
    public const string InventoryDiagnosticsSlots = "inventory.diagnostics.slots";
    public const string InventoryDiagnosticsItems = "inventory.diagnostics.items";
    public const string InventoryDiagnosticsCompatibility = "inventory.diagnostics.compatibility";
    public const string InventoryDiagnosticsLegality = "inventory.diagnostics.legality";

    public const string CharacterCompanionsGet = "character.companions.get";
    public const string CharacterCompanionAdd = "character.companion.add";
    public const string CharacterCompanionUpdate = "character.companion.update";
    public const string CharacterCompanionRemove = "character.companion.remove";

    public const string CharacterHoldingsGet = "character.holdings.get";
    public const string CharacterHoldingAdd = "character.holding.add";
    public const string CharacterHoldingUpdate = "character.holding.update";
    public const string CharacterHoldingRemove = "character.holding.remove";

    public const string CharacterReputationGet = "character.reputation.get";
    public const string CharacterReputationEntryAdd = "character.reputation.entry.add";
    public const string CharacterReputationEntryUpdate = "character.reputation.entry.update";
    public const string CharacterReputationEntryRemove = "character.reputation.entry.remove";
    public const string CharacterSkillsGet = "character.skills.get";
    public const string CharacterSubAttributesGet = "character.subattributes.get";
    public const string CharacterSubAttributesAdminGet = "character.subattributes.admin.get";
    public const string CharacterSubAttributesAdminUpdate = "character.subattributes.admin.update";
    public const string CharacterSubAttributesAdminResetToDefaults = "character.subattributes.admin.resetToDefaults";
    public const string DefinitionsSubAttributesAdminList = "definitions.subattributes.admin.list";
    public const string DefinitionsSubAttributesAdminCreateOrUpdate = "definitions.subattributes.admin.createOrUpdate";
    public const string DefinitionsSubAttributesAdminArchive = "definitions.subattributes.admin.archive";
    public const string CharacterClassesGet = "character.classes.get";
    public const string CharacterClassAssign = "character.class.assign";
    public const string CharacterSkillAdd = "character.skill.add";
    public const string CharacterSkillUpdateLevel = "character.skill.updateLevel";
    public const string CharacterSkillRemove = "character.skill.remove";
    public const string CharacterSkillCheckRoll = "character.skill.check.roll";

    public const string CharacterProfileConsistencyVerify = "character.profile.consistency.verify";

    public const string CharacterUpdateBasicInfo = "character.update.basicInfo";
    public const string CharacterUpdateStats = "character.update.stats";
    public const string CharacterUpdateVisibility = "character.update.visibility";
    public const string CharacterUpdateMoney = "character.update.money";
    public const string CharacterUpdateXpCoins = "character.update.xpCoins";
    public const string CharacterUpdateInventory = "character.update.inventory";
    public const string CharacterUpdateReputation = "character.update.reputation";
    public const string CharacterUpdateHoldings = "character.update.holdings";

    public const string CharacterCreate = "character.create";
    public const string CharacterAdminCreate = "character.admin.create";
    public const string CharacterSetActive = "character.set.active";
    public const string CharacterAssignOwner = "character.assignOwner";
    public const string CharacterArchive = "character.archive";
    public const string CharacterRestore = "character.restore";
    public const string CharacterTransfer = "character.transfer";
    public const string CharacterAssignActive = "character.assignActive";

    public const string CharacterOwnershipGet = "character.ownership.get";
    public const string CharacterOwnershipList = "character.ownership.list";
    public const string CharacterOwnershipAssignOwner = "character.ownership.assignOwner";
    public const string CharacterOwnershipReassignOwner = "character.ownership.reassignOwner";
    public const string CharacterOwnershipClearOwner = "character.ownership.clearOwner";
    public const string CharacterOwnershipSetController = "character.ownership.setController";
    public const string CharacterOwnershipClearController = "character.ownership.clearController";
    public const string CharacterOwnershipSetRole = "character.ownership.setRole";
    public const string CharacterOwnershipConvertToPlayerCharacter = "character.ownership.convertToPlayerCharacter";
    public const string CharacterOwnershipConvertToNpc = "character.ownership.convertToNpc";
    public const string CharacterOwnershipConvertToCompanion = "character.ownership.convertToCompanion";
    public const string CharacterOwnershipSetVisibility = "character.ownership.setVisibility";
    public const string CharacterOwnershipAuditList = "character.ownership.audit.list";
    public const string CharacterPlayerAssignedList = "character.player.assigned.list";
    public const string CharacterPlayerAssignedGet = "character.player.assigned.get";


    public const string CharacterAdminList = "character.admin.list";
    public const string CharacterAdminSearch = "character.admin.search";
    public const string CharacterAdminGet = "character.admin.get";
    public const string CharacterAdminSaveBasic = "character.admin.save.basic";
    public const string CharacterAdminSaveBiography = "character.admin.save.biography";
    public const string CharacterAdminSaveStats = "character.admin.save.stats";
    public const string CharacterAdminSaveMoney = "character.admin.save.money";
    public const string CharacterAdminSaveProgression = "character.admin.save.progression";
    public const string CharacterAdminSaveVisibility = "character.admin.save.visibility";
    public const string CharacterAdminGetNotesContext = "character.admin.get.notesContext";

    public const string CharacterSelfGet = "character.self.get";
    public const string CharacterSelfSaveBasic = "character.self.save.basic";
    public const string CharacterSelfSaveStats = "character.self.save.stats";
    public const string CharacterSelfSaveMoney = "character.self.save.money";
    public const string CharacterSelfGetProgression = "character.self.get.progression";

    public const string CharacterLockAcquire = "character.lock.acquire";
    public const string CharacterLockRelease = "character.lock.release";
    public const string CharacterLockForceRelease = "character.lock.forceRelease";
    public const string CharacterLockGet = "character.lock.get";

    public const string PresenceList = "presence.list";
    public const string SessionValidate = "session.validate";
    public const string SystemFeatureFlagsSnapshot = "system.featureFlags.snapshot";
    public const string FeatureFlagsAdminList = "featureFlags.admin.list";
    public const string FeatureFlagsAdminGet = "featureFlags.admin.get";
    public const string FeatureFlagsAdminSetOverride = "featureFlags.admin.setOverride";
    public const string FeatureFlagsAdminClearOverride = "featureFlags.admin.clearOverride";
    public const string FeatureFlagsAdminRefresh = "featureFlags.admin.refresh";
    public const string SearchAdminQuery = "search.admin.query";
    public const string SearchPlayerQuery = "search.player.query";
    public const string SearchAdminOpenTarget = "search.admin.openTarget";
    public const string SearchPlayerOpenTarget = "search.player.openTarget";
    public const string SearchAdminDiagnostics = "search.admin.diagnostics";
    public const string AdminDashboardGet = "admin.dashboard.get";
    public const string PlayerDashboardGet = "player.dashboard.get";
    public const string AdminActiveProcessesList = "admin.activeProcesses.list";
    public const string PlayerActiveProcessesList = "player.activeProcesses.list";
    public const string AdminNextActionsList = "admin.nextActions.list";
    public const string PlayerNextActionsList = "player.nextActions.list";
    public const string CharacterPlayerHubGet = "character.player.hub.get";
    public const string CharacterAdminHubGet = "character.admin.hub.get";
    public const string SessionCurrentGet = "session.current.get";
    public const string SessionCurrentCreate = "session.current.create";
    public const string SessionCurrentUpdate = "session.current.update";
    public const string SessionCurrentStart = "session.current.start";
    public const string SessionCurrentPause = "session.current.pause";
    public const string SessionCurrentResume = "session.current.resume";
    public const string SessionCurrentComplete = "session.current.complete";
    public const string SessionCurrentCancel = "session.current.cancel";
    public const string SessionCurrentSetScene = "session.current.setScene";
    public const string SessionCurrentSetMode = "session.current.setMode";
    public const string SessionCurrentSetActiveSceneMap = "session.current.setActiveSceneMap";
    public const string SessionCurrentSetActiveCombat = "session.current.setActiveCombat";
    public const string SessionCurrentClearActiveCombat = "session.current.clearActiveCombat";
    public const string SessionCurrentSetNotes = "session.current.setNotes";
    public const string SessionPlayerCurrentGet = "session.player.current.get";
    public const string GroupCharacterList = "group.character.list";
    public const string GroupCharacterCreate = "group.character.create";
    public const string GroupCharacterGet = "group.character.get";
    public const string GroupCharacterUpdate = "group.character.update";
    public const string GroupCharacterArchive = "group.character.archive";
    public const string GroupCharacterMemberAdd = "group.character.member.add";
    public const string GroupCharacterMemberRemove = "group.character.member.remove";
    public const string GroupCharacterMemberUpdate = "group.character.member.update";
    public const string GroupCharacterMemberMove = "group.character.member.move";
    public const string GroupCharacterSetActive = "group.character.setActive";
    public const string GroupCharacterClearActive = "group.character.clearActive";
    public const string GroupPlayerActiveGet = "group.player.active.get";
    public const string GroupPlayerListVisible = "group.player.listVisible";
    public const string GroupPlayerGetVisible = "group.player.getVisible";


    public const string RequestCreate = "request.create";
    public const string RequestCancel = "request.cancel";
    public const string RequestListMine = "request.list.mine";
    public const string RequestListPending = "request.list.pending";
    public const string RequestGetDetails = "request.get.details";
    public const string RequestApprove = "request.approve";
    public const string RequestReject = "request.reject";
    public const string RequestHistory = "request.history";

    public const string PlayerRequestCreate = "request.player.create";
    public const string PlayerRequestSubmit = "request.player.submit";
    public const string PlayerRequestListMine = "request.player.listMine";
    public const string PlayerRequestGetMine = "request.player.getMine";
    public const string PlayerRequestComment = "request.player.comment";
    public const string PlayerRequestCancel = "request.player.cancel";
    public const string PlayerRequestResubmit = "request.player.resubmit";
    public const string AdminRequestList = "request.admin.list";
    public const string AdminRequestGet = "request.admin.get";
    public const string AdminRequestSetInReview = "request.admin.setInReview";
    public const string AdminRequestApprove = "request.admin.approve";
    public const string AdminRequestReject = "request.admin.reject";
    public const string AdminRequestRequestChanges = "request.admin.requestChanges";
    public const string AdminRequestComment = "request.admin.comment";
    public const string AdminRequestMarkFulfilled = "request.admin.markFulfilled";
    public const string AdminRequestArchive = "request.admin.archive";
    public const string RequestStatusGet = "request.status.get";
    public const string RequestsPlayerCreate = "requests.player.create";
    public const string RequestsPlayerMine = "requests.player.mine";
    public const string RequestsPlayerGet = "requests.player.get";
    public const string RequestsPlayerCancel = "requests.player.cancel";
    public const string RequestsPlayerResubmit = "requests.player.resubmit";
    public const string RequestsAdminList = "requests.admin.list";
    public const string RequestsAdminGet = "requests.admin.get";
    public const string RequestsAdminMarkInReview = "requests.admin.markInReview";
    public const string RequestsAdminApprove = "requests.admin.approve";
    public const string RequestsAdminReject = "requests.admin.reject";
    public const string RequestsAdminRequestChanges = "requests.admin.requestChanges";
    public const string RequestsAdminArchive = "requests.admin.archive";

    public const string ProjectList = "project.list";
    public const string ProjectGet = "project.get";
    public const string ProjectCreate = "project.create";
    public const string ProjectUpdate = "project.update";
    public const string ProjectStatusSet = "project.status.set";
    public const string ProjectStageAdd = "project.stage.add";
    public const string ProjectStageUpdate = "project.stage.update";
    public const string ProjectStageComplete = "project.stage.complete";
    public const string ProjectParticipantAdd = "project.participant.add";
    public const string ProjectParticipantUpdate = "project.participant.update";
    public const string ProjectParticipantRemove = "project.participant.remove";
    public const string ProjectRequirementAdd = "project.requirement.add";
    public const string ProjectRequirementVerify = "project.requirement.verify";
    public const string ProjectRequirementWaive = "project.requirement.waive";
    public const string ProjectResourceAdd = "project.resource.add";
    public const string ProjectResourceMarkReserved = "project.resource.markReserved";
    public const string ProjectResourceMarkConsumed = "project.resource.markConsumed";
    public const string ProjectProgressAdd = "project.progress.add";
    public const string ProjectApprovalCreate = "project.approval.create";
    public const string ProjectApprovalResolve = "project.approval.resolve";
    public const string ProjectLinkAdd = "project.link.add";
    public const string ProjectAuditList = "project.audit.list";
    public const string ProjectPlayerList = "project.player.list";
    public const string ProjectPlayerGet = "project.player.get";
    public const string ProjectPlayerDraftCreate = "project.player.draft.create";
    public const string ProjectPlayerDraftUpdate = "project.player.draft.update";
    public const string ProjectPlayerDraftSubmit = "project.player.draft.submit";
    public const string ProjectPlayerDraftCancel = "project.player.draft.cancel";

    public const string ProposalPlayerTemplateList = "proposal.player.template.list";
    public const string ProposalPlayerDraftListMine = "proposal.player.draft.listMine";
    public const string ProposalPlayerDraftGetMine = "proposal.player.draft.getMine";
    public const string ProposalPlayerDraftCreate = "proposal.player.draft.create";
    public const string ProposalPlayerDraftUpdate = "proposal.player.draft.update";
    public const string ProposalPlayerDraftValidate = "proposal.player.draft.validate";
    public const string ProposalPlayerDraftPreview = "proposal.player.draft.preview";
    public const string ProposalPlayerDraftSubmit = "proposal.player.draft.submit";
    public const string ProposalPlayerDraftCancel = "proposal.player.draft.cancel";
    public const string ProposalPlayerDraftArchive = "proposal.player.draft.archive";
    public const string ProposalPlayerDraftResubmitAfterChanges = "proposal.player.draft.resubmitAfterChanges";
    public const string ProposalPlayerLinkedOpen = "proposal.player.linked.open";
    public const string ProposalAdminList = "proposal.admin.list";
    public const string ProposalAdminGet = "proposal.admin.get";
    public const string ProposalAdminReviewStart = "proposal.admin.review.start";
    public const string ProposalAdminReviewRequestChanges = "proposal.admin.review.requestChanges";
    public const string ProposalAdminReviewApprove = "proposal.admin.review.approve";
    public const string ProposalAdminReviewReject = "proposal.admin.review.reject";
    public const string ProposalAdminConvertToResearch = "proposal.admin.convert.toResearch";
    public const string ProposalAdminConvertToCrafting = "proposal.admin.convert.toCrafting";
    public const string ProposalAdminConvertToEngineering = "proposal.admin.convert.toEngineering";
    public const string ProposalAdminConvertToFactoryQuote = "proposal.admin.convert.toFactoryQuote";
    public const string ProposalAdminConvertToFactoryOrder = "proposal.admin.convert.toFactoryOrder";
    public const string ProposalAdminConvertToManufacturing = "proposal.admin.convert.toManufacturing";
    public const string ProposalAdminConvertToLegalCheck = "proposal.admin.convert.toLegalCheck";
    public const string ProposalAdminConvertToLicenseApplication = "proposal.admin.convert.toLicenseApplication";
    public const string ProposalAdminConvertToDevelopmentPurchase = "proposal.admin.convert.toDevelopmentPurchase";
    public const string ProposalAdminConvertToGenericProject = "proposal.admin.convert.toGenericProject";
    public const string ProposalAdminLinkExisting = "proposal.admin.linkExisting";
    public const string ProposalAdminArchive = "proposal.admin.archive";
    public const string ProposalAdminValidationRun = "proposal.admin.validation.run";
    public const string ProposalAdminTemplateList = "proposal.admin.template.list";
    public const string ProposalAdminTemplateCreate = "proposal.admin.template.create";
    public const string ProposalAdminTemplateUpdate = "proposal.admin.template.update";
    public const string ProposalAdminTemplateArchive = "proposal.admin.template.archive";
    public const string ProposalTypesList = "proposal.types.list";
    public const string ProposalStatusExplain = "proposal.status.explain";

    public const string CraftingRecipeList = "crafting.recipe.list";
    public const string CraftingRecipeGet = "crafting.recipe.get";
    public const string CraftingRecipeCreate = "crafting.recipe.create";
    public const string CraftingRecipeUpdate = "crafting.recipe.update";
    public const string CraftingRecipeArchive = "crafting.recipe.archive";
    public const string CraftingPlayerRecipeList = "crafting.player.recipe.list";
    public const string CraftingProjectList = "crafting.project.list";
    public const string CraftingProjectGet = "crafting.project.get";
    public const string CraftingProjectCreate = "crafting.project.create";
    public const string CraftingProjectStart = "crafting.project.start";
    public const string CraftingProjectProgressAdd = "crafting.project.progress.add";
    public const string CraftingProjectCancel = "crafting.project.cancel";
    public const string CraftingProjectFail = "crafting.project.fail";
    public const string CraftingProjectComplete = "crafting.project.complete";
    public const string CraftingReservationPreview = "crafting.reservation.preview";
    public const string CraftingReservationReserve = "crafting.reservation.reserve";
    public const string CraftingReservationRelease = "crafting.reservation.release";
    public const string CraftingReservationConsume = "crafting.reservation.consume";
    public const string CraftingResultPrepare = "crafting.result.prepare";
    public const string CraftingResultAccept = "crafting.result.accept";
    public const string CraftingPlayerProjectList = "crafting.player.project.list";
    public const string CraftingPlayerProjectGet = "crafting.player.project.get";
    public const string CraftingPlayerDraftCreate = "crafting.player.draft.create";
    public const string CraftingPlayerDraftSubmit = "crafting.player.draft.submit";

    public const string EngineeringPlatformList = "engineering.platform.list";
    public const string EngineeringPlatformGet = "engineering.platform.get";
    public const string EngineeringPlatformCreate = "engineering.platform.create";
    public const string EngineeringPlatformUpdate = "engineering.platform.update";
    public const string EngineeringPlatformArchive = "engineering.platform.archive";
    public const string EngineeringSizeClassList = "engineering.sizeClass.list";
    public const string EngineeringSizeClassCreate = "engineering.sizeClass.create";
    public const string EngineeringSizeClassUpdate = "engineering.sizeClass.update";
    public const string EngineeringSizeClassArchive = "engineering.sizeClass.archive";
    public const string EngineeringModuleList = "engineering.module.list";
    public const string EngineeringModuleGet = "engineering.module.get";
    public const string EngineeringModuleCreate = "engineering.module.create";
    public const string EngineeringModuleUpdate = "engineering.module.update";
    public const string EngineeringModuleArchive = "engineering.module.archive";
    public const string EngineeringPresetList = "engineering.preset.list";
    public const string EngineeringPresetCreate = "engineering.preset.create";
    public const string EngineeringPresetUpdate = "engineering.preset.update";
    public const string EngineeringPresetArchive = "engineering.preset.archive";
    public const string EngineeringDesignValidate = "engineering.design.validate";
    public const string EngineeringProjectList = "engineering.project.list";
    public const string EngineeringProjectGet = "engineering.project.get";
    public const string EngineeringProjectCreate = "engineering.project.create";
    public const string EngineeringProjectStart = "engineering.project.start";
    public const string EngineeringProjectProgressAdd = "engineering.project.progress.add";
    public const string EngineeringProjectCancel = "engineering.project.cancel";
    public const string EngineeringProjectFail = "engineering.project.fail";
    public const string EngineeringProjectComplete = "engineering.project.complete";
    public const string EngineeringBlueprintPrepare = "engineering.blueprint.prepare";
    public const string EngineeringBlueprintAccept = "engineering.blueprint.accept";
    public const string EngineeringBlueprintArchive = "engineering.blueprint.archive";
    public const string EngineeringPlayerPlatformList = "engineering.player.platform.list";
    public const string EngineeringPlayerModuleList = "engineering.player.module.list";
    public const string EngineeringPlayerPresetList = "engineering.player.preset.list";
    public const string EngineeringPlayerDraftList = "engineering.player.draft.list";
    public const string EngineeringPlayerDraftGet = "engineering.player.draft.get";
    public const string EngineeringPlayerDraftCreate = "engineering.player.draft.create";
    public const string EngineeringPlayerDraftUpdate = "engineering.player.draft.update";
    public const string EngineeringPlayerDraftValidate = "engineering.player.draft.validate";
    public const string EngineeringPlayerDraftSubmit = "engineering.player.draft.submit";
    public const string EngineeringPlayerProjectList = "engineering.player.project.list";
    public const string EngineeringPlayerProjectGet = "engineering.player.project.get";
    public const string EngineeringPlayerBlueprintList = "engineering.player.blueprint.list";
    public const string EngineeringPlayerBlueprintGet = "engineering.player.blueprint.get";

    public const string ProductionFacilityDefinitionList = "production.facilityDefinition.list";
    public const string ProductionFacilityDefinitionCreate = "production.facilityDefinition.create";
    public const string ProductionFacilityDefinitionUpdate = "production.facilityDefinition.update";
    public const string ProductionFacilityDefinitionArchive = "production.facilityDefinition.archive";
    public const string ProductionFacilityList = "production.facility.list";
    public const string ProductionFacilityGet = "production.facility.get";
    public const string ProductionFacilityCreate = "production.facility.create";
    public const string ProductionFacilityUpdate = "production.facility.update";
    public const string ProductionFacilityArchive = "production.facility.archive";
    public const string ProductionCapabilityAdd = "production.capability.add";
    public const string ProductionCapabilityUpdate = "production.capability.update";
    public const string ProductionCapabilityRemove = "production.capability.remove";
    public const string ProductionCapacityUpdate = "production.capacity.update";
    public const string ProductionProcessList = "production.process.list";
    public const string ProductionProcessCreate = "production.process.create";
    public const string ProductionProcessUpdate = "production.process.update";
    public const string ProductionProcessArchive = "production.process.archive";
    public const string FactoryQuoteList = "factory.quote.list";
    public const string FactoryQuoteGet = "factory.quote.get";
    public const string FactoryQuoteGenerate = "factory.quote.generate";
    public const string FactoryQuoteUpdate = "factory.quote.update";
    public const string FactoryQuoteOffer = "factory.quote.offer";
    public const string FactoryQuoteAccept = "factory.quote.accept";
    public const string FactoryQuoteReject = "factory.quote.reject";
    public const string FactoryQuoteExpire = "factory.quote.expire";
    public const string FactoryQuoteConvertToOrder = "factory.quote.convertToOrder";
    public const string FactoryQuoteArchive = "factory.quote.archive";
    public const string FactoryOrderList = "factory.order.list";
    public const string FactoryOrderGet = "factory.order.get";
    public const string FactoryOrderCreate = "factory.order.create";
    public const string FactoryOrderApprove = "factory.order.approve";
    public const string FactoryOrderReject = "factory.order.reject";
    public const string FactoryOrderSchedule = "factory.order.schedule";
    public const string FactoryOrderCancel = "factory.order.cancel";
    public const string FactoryOrderArchive = "factory.order.archive";
    public const string FactoryQueueReserve = "factory.queue.reserve";
    public const string FactoryQueueList = "factory.queue.list";
    public const string ProductionPlayerFacilityList = "production.player.facility.list";
    public const string FactoryPlayerQuoteList = "factory.player.quote.list";
    public const string FactoryPlayerQuoteRequest = "factory.player.quote.request";
    public const string FactoryPlayerQuoteAccept = "factory.player.quote.accept";
    public const string FactoryPlayerQuoteReject = "factory.player.quote.reject";
    public const string FactoryPlayerOrderList = "factory.player.order.list";
    public const string FactoryPlayerOrderRequest = "factory.player.order.request";
    public const string ManufacturingProjectList = "manufacturing.project.list";
    public const string ManufacturingProjectGet = "manufacturing.project.get";
    public const string ManufacturingProjectCreateFromOrder = "manufacturing.project.createFromOrder";
    public const string ManufacturingProjectCreateManual = "manufacturing.project.createManual";
    public const string ManufacturingProjectStart = "manufacturing.project.start";
    public const string ManufacturingProjectPause = "manufacturing.project.pause";
    public const string ManufacturingProjectResume = "manufacturing.project.resume";
    public const string ManufacturingProjectCancel = "manufacturing.project.cancel";
    public const string ManufacturingProjectComplete = "manufacturing.project.complete";
    public const string ManufacturingStageAdd = "manufacturing.stage.add";
    public const string ManufacturingStageUpdate = "manufacturing.stage.update";
    public const string ManufacturingStageStart = "manufacturing.stage.start";
    public const string ManufacturingStageComplete = "manufacturing.stage.complete";
    public const string ManufacturingResourcePlanAdd = "manufacturing.resourcePlan.add";
    public const string ManufacturingResourceReserve = "manufacturing.resource.reserve";
    public const string ManufacturingResourceRelease = "manufacturing.resource.release";
    public const string ManufacturingResourceConsume = "manufacturing.resource.consume";
    public const string ManufacturingCostAdd = "manufacturing.cost.add";
    public const string ManufacturingPaymentAdd = "manufacturing.payment.add";
    public const string ManufacturingPaymentMarkPaid = "manufacturing.payment.markPaid";
    public const string ManufacturingProgressAdd = "manufacturing.progress.add";
    public const string ManufacturingTestPlanCreate = "manufacturing.testPlan.create";
    public const string ManufacturingTestResultAdd = "manufacturing.testResult.add";
    public const string ManufacturingDefectCreate = "manufacturing.defect.create";
    public const string ManufacturingDefectResolve = "manufacturing.defect.resolve";
    public const string ManufacturingAcceptancePrepare = "manufacturing.acceptance.prepare";
    public const string ManufacturingAcceptanceAccept = "manufacturing.acceptance.accept";
    public const string ManufacturingAcceptanceReject = "manufacturing.acceptance.reject";
    public const string ManufacturingAssetCreate = "manufacturing.asset.create";
    public const string ManufacturingAssetTransfer = "manufacturing.asset.transfer";
    public const string ManufacturingAssetCommission = "manufacturing.asset.commission";
    public const string ManufacturingAssetList = "manufacturing.asset.list";
    public const string ManufacturingPlayerProjectList = "manufacturing.player.project.list";
    public const string ManufacturingPlayerProjectGet = "manufacturing.player.project.get";
    public const string ManufacturingPlayerAssetList = "manufacturing.player.asset.list";
    public const string ManufacturingPlayerContributionSubmit = "manufacturing.player.contribution.submit";

    public const string KnowledgeDefinitionList = "knowledge.definition.list";
    public const string KnowledgeDefinitionGet = "knowledge.definition.get";
    public const string KnowledgeDefinitionCreate = "knowledge.definition.create";
    public const string KnowledgeDefinitionUpdate = "knowledge.definition.update";
    public const string KnowledgeDefinitionArchive = "knowledge.definition.archive";
    public const string EntityKnowledgeList = "knowledge.entity.list";
    public const string EntityKnowledgeGrant = "knowledge.entity.grant";
    public const string EntityKnowledgeUpdate = "knowledge.entity.update";
    public const string EntityKnowledgeReveal = "knowledge.entity.reveal";
    public const string EntityKnowledgeHide = "knowledge.entity.hide";
    public const string EntityKnowledgeCorrect = "knowledge.entity.correct";
    public const string EntityKnowledgeArchive = "knowledge.entity.archive";
    public const string KnowledgeSourceList = "knowledge.source.list";
    public const string KnowledgeSourceAdd = "knowledge.source.add";
    public const string AppliedKnowledgeList = "knowledge.applied.list";
    public const string AppliedKnowledgeCreate = "knowledge.applied.create";
    public const string AppliedKnowledgeUpdate = "knowledge.applied.update";
    public const string AppliedKnowledgeArchive = "knowledge.applied.archive";
    public const string ResearchList = "research.list";
    public const string ResearchGet = "research.get";
    public const string ResearchCreate = "research.create";
    public const string ResearchUpdate = "research.update";
    public const string ResearchProgressAdd = "research.progress.add";
    public const string ResearchResultPrepare = "research.result.prepare";
    public const string ResearchResultResolve = "research.result.resolve";
    public const string ResearchResultApply = "research.result.apply";
    public const string ResearchPlayerList = "research.player.list";
    public const string ResearchPlayerGet = "research.player.get";
    public const string ResearchPlayerDraftCreate = "research.player.draft.create";
    public const string ResearchPlayerDraftSubmit = "research.player.draft.submit";
    public const string KnowledgePlayerEntityList = "knowledge.player.entity.list";

    public const string WorldCalendarDefinitionGet = "world.calendar.definition.get";
    public const string WorldCalendarDefaultEnsure = "world.calendar.default.ensure";
    public const string WorldCalendarCurrentGet = "world.calendar.current.get";
    public const string WorldCalendarCurrentSet = "world.calendar.current.set";
    public const string WorldCalendarCurrentAdvance = "world.calendar.current.advance";
    public const string WorldCalendarEventList = "world.calendar.event.list";
    public const string WorldCalendarEventCreate = "world.calendar.event.create";
    public const string WorldCalendarEventUpdate = "world.calendar.event.update";
    public const string WorldCalendarEventCancel = "world.calendar.event.cancel";
    public const string WorldCalendarEventArchive = "world.calendar.event.archive";
    public const string WorldCalendarEventVersionAdd = "world.calendar.event.version.add";
    public const string WorldCalendarHolidayList = "world.calendar.holiday.list";
    public const string WorldCalendarHolidayCreate = "world.calendar.holiday.create";
    public const string WorldCalendarHolidayUpdate = "world.calendar.holiday.update";
    public const string WorldCalendarHolidayArchive = "world.calendar.holiday.archive";
    public const string WorldCalendarReminderList = "world.calendar.reminder.list";
    public const string WorldCalendarReminderCreate = "world.calendar.reminder.create";
    public const string WorldCalendarReminderDismiss = "world.calendar.reminder.dismiss";
    public const string WorldCalendarPlayerGet = "world.calendar.player.get";

    public const string RealScheduleList = "schedule.real.list";
    public const string RealScheduleCreate = "schedule.real.create";
    public const string RealScheduleGet = "schedule.real.get";
    public const string RealScheduleUpdate = "schedule.real.update";
    public const string RealScheduleReschedule = "schedule.real.reschedule";
    public const string RealScheduleCancel = "schedule.real.cancel";
    public const string RealScheduleStart = "schedule.real.start";
    public const string RealScheduleComplete = "schedule.real.complete";
    public const string RealScheduleArchive = "schedule.real.archive";
    public const string RealScheduleParticipantList = "schedule.real.participant.list";
    public const string RealScheduleParticipantAdd = "schedule.real.participant.add";
    public const string RealScheduleParticipantUpdate = "schedule.real.participant.update";
    public const string RealScheduleParticipantRemove = "schedule.real.participant.remove";
    public const string RealSchedulePlayerList = "schedule.real.player.list";
    public const string RealSchedulePlayerNext = "schedule.real.player.next";
    public const string RealSchedulePlayerGet = "schedule.real.player.get";

    public const string GMNoteList = "gm.note.list";
    public const string GMNoteCreate = "gm.note.create";
    public const string GMNoteGet = "gm.note.get";
    public const string GMNoteUpdate = "gm.note.update";
    public const string GMNoteArchive = "gm.note.archive";
    public const string GMNoteRestore = "gm.note.restore";
    public const string GMNotePin = "gm.note.pin";
    public const string GMNoteUnpin = "gm.note.unpin";
    public const string GMNoteMove = "gm.note.move";
    public const string GMNoteSearch = "gm.note.search";
    public const string GMNoteFolderList = "gm.note.folder.list";
    public const string GMNoteFolderCreate = "gm.note.folder.create";
    public const string GMNoteFolderUpdate = "gm.note.folder.update";
    public const string GMNoteFolderArchive = "gm.note.folder.archive";
    public const string GMNoteLinkList = "gm.note.link.list";
    public const string GMNoteLinkAdd = "gm.note.link.add";
    public const string GMNoteLinkRemove = "gm.note.link.remove";
    public const string GMNoteAuditList = "gm.note.audit.list";
    public const string GMNotesAdminList = "gmNotes.admin.list";
    public const string GMNotesAdminCreate = "gmNotes.admin.create";
    public const string GMNotesAdminGet = "gmNotes.admin.get";
    public const string GMNotesAdminUpdate = "gmNotes.admin.update";
    public const string GMNotesAdminArchive = "gmNotes.admin.archive";
    public const string GMNotesAdminPin = "gmNotes.admin.pin";
    public const string GMNotesAdminUnpin = "gmNotes.admin.unpin";
    public const string GMNotesAdminRestore = "gmNotes.admin.restore";

    public const string JournalEventList = "journal.event.list";
    public const string JournalEventSearch = "journal.event.search";
    public const string JournalEventGet = "journal.event.get";
    public const string JournalEventIngest = "journal.event.ingest";
    public const string JournalEventManualCreate = "journal.event.manual.create";
    public const string JournalEventManualUpdate = "journal.event.manual.update";
    public const string JournalEventCorrectionCreate = "journal.event.correction.create";
    public const string JournalEventAnnotationAdd = "journal.event.annotation.add";
    public const string JournalEventVisibilitySet = "journal.event.visibility.set";
    public const string JournalEventArchive = "journal.event.archive";
    public const string JournalEventRestore = "journal.event.restore";
    public const string JournalEventLinkList = "journal.event.link.list";
    public const string JournalEventLinkAdd = "journal.event.link.add";
    public const string JournalEventLinkRemove = "journal.event.link.remove";
    public const string JournalEventPlayerList = "journal.player.event.list";
    public const string JournalEventPlayerGet = "journal.player.event.get";
    public const string EventJournalAdminList = "eventJournal.admin.list";
    public const string EventJournalAdminGet = "eventJournal.admin.get";
    public const string EventJournalAdminCreate = "eventJournal.admin.create";
    public const string EventJournalAdminUpdate = "eventJournal.admin.update";
    public const string EventJournalAdminArchive = "eventJournal.admin.archive";
    public const string EventJournalAdminRestore = "eventJournal.admin.restore";
    public const string EventJournalAdminSetVisibility = "eventJournal.admin.setVisibility";
    public const string EventJournalPlayerVisibleList = "eventJournal.player.visibleList";
    public const string EventJournalPlayerGetVisible = "eventJournal.player.getVisible";

    public const string DiceRequest = "dice.request";
    public const string DiceRollStandard = "dice.roll.standard";
    public const string DiceRollTest = "dice.roll.test";
    public const string DiceTestGetCurrent = "dice.test.getCurrent";
    public const string FateTestRoll = "fate.test.roll";
    public const string FateStatusGet = "fate.status.get";
    public const string FateSettingsGet = "fate.settings.get";
    public const string FateSettingsUpdate = "fate.settings.update";
    public const string FateEffectsList = "fate.effects.list";
    public const string FateEffectsByLayer = "fate.effects.byLayer";
    public const string FateAdminStateGet = "fate.admin.state.get";
    public const string FateAdminStateUpdate = "fate.admin.state.update";
    public const string FateAdminProfileList = "fate.admin.profile.list";
    public const string FateAdminProfileGet = "fate.admin.profile.get";
    public const string FateAdminProfileSetActive = "fate.admin.profile.setActive";
    public const string FateAdminLayerRulesList = "fate.admin.layerRules.list";
    public const string FateAdminModifierRulesList = "fate.admin.modifierRules.list";
    public const string FateAdminRollLogsList = "fate.admin.rollLogs.list";
    public const string FateAdminRollLogsGet = "fate.admin.rollLogs.get";
    public const string FateAdminSimulateRoll = "fate.admin.simulateRoll";
    public const string FateAdminConfidenceGet = "fate.admin.confidence.get";
    public const string FateAdminSeedAcceptanceData = "fate.admin.seedAcceptanceData";
    public const string FateControlLayoutGet = "fate.control.layout.get";
    public const string FateControlLayoutSave = "fate.control.layout.save";
    public const string FateControlLayoutReset = "fate.control.layout.reset";
    public const string FateControlPanelsList = "fate.control.panels.list";
    public const string DiceHistory = "dice.history";
    public const string DiceVisibleFeed = "dice.visibleFeed";
    public const string DiceGetDetails = "dice.get.details";


    public const string CombatStart = "combat.start";
    public const string CombatEnd = "combat.end";
    public const string CombatGetState = "combat.getState";
    public const string CombatGetHistory = "combat.getHistory";
    public const string CombatNextTurn = "combat.nextTurn";
    public const string CombatPreviousTurn = "combat.previousTurn";
    public const string CombatNextRound = "combat.nextRound";
    public const string CombatSkipTurn = "combat.skipTurn";
    public const string CombatSelectActive = "combat.selectActive";
    public const string CombatReorderBeforeStart = "combat.reorderBeforeStart";
    public const string CombatReorderSlotMembers = "combat.reorderSlotMembers";
    public const string CombatAddParticipant = "combat.addParticipant";
    public const string CombatRemoveParticipant = "combat.removeParticipant";
    public const string CombatDetachCompanion = "combat.detachCompanion";
    public const string CombatVisibleState = "combat.visibleState";
    public const string CombatParticipants = "combat.participants";
    public const string CombatTimeline = "combat.timeline";
    public const string CombatV1EncounterCreate = "combat.v1.encounter.create";
    public const string CombatV1EncounterEnd = "combat.v1.encounter.end";
    public const string CombatV1EncounterCancel = "combat.v1.encounter.cancel";
    public const string CombatV1ParticipantAdd = "combat.v1.participant.add";
    public const string CombatV1ParticipantRemove = "combat.v1.participant.remove";
    public const string CombatV1EncounterSnapshot = "combat.v1.encounter.snapshot";
    public const string CombatV1InitiativeSort = "combat.v1.initiative.sort";
    public const string CombatV1RoundStart = "combat.v1.round.start";
    public const string CombatV1TurnStart = "combat.v1.turn.start";
    public const string CombatV1TurnEnd = "combat.v1.turn.end";
    public const string CombatV1TurnNext = "combat.v1.turn.next";
    public const string CombatV1RoundNext = "combat.v1.round.next";
    public const string CombatV1TurnSkip = "combat.v1.turn.skip";
    public const string CombatV1TurnDelay = "combat.v1.turn.delay";
    public const string CombatV1LogsList = "combat.v1.logs.list";
    public const string CombatV1ReplayList = "combat.v1.replay.list";
    public const string CombatV1SnapshotFull = "combat.v1.snapshot.full";
    public const string CombatV1DiagnosticsRun = "combat.v1.diagnostics.run";
    public const string CombatV1ActionDeclare = "combat.v1.action.declare";
    public const string CombatV1ActionComplete = "combat.v1.action.complete";
    public const string CombatV1ActionCancel = "combat.v1.action.cancel";
    public const string CombatV1ActionSpend = "combat.v1.action.spend";
    public const string CombatV1AttackRoll = "combat.v1.attack.roll";
    public const string CombatV1DefensePreview = "combat.v1.defense.preview";
    public const string CombatV1ParticipantVitalsSet = "combat.v1.participant.vitals.set";
    public const string CombatV1DamageApply = "combat.v1.damage.apply";
    public const string CombatV1ConditionApply = "combat.v1.condition.apply";
    public const string CombatV1ConditionRemove = "combat.v1.condition.remove";
    public const string CombatV1ConditionList = "combat.v1.condition.list";
    public const string CombatV1WeaponAttackResolve = "combat.v1.weaponAttack.resolve";
    public const string CombatV1FatePreview = "combat.v1.fate.preview";
    public const string CombatV1SmokeRun = "combat.v1.smoke.run";
    public const string CombatV1PlayerSnapshot = "combat.v1.player.snapshot";
    public const string CombatV1PlayerFeed = "combat.v1.player.feed";
    public const string MapSpaceNodeList = "map.spaceNode.list";
    public const string MapSpaceNodeCreate = "map.spaceNode.create";
    public const string MapSceneList = "map.scene.list";
    public const string MapSceneCreate = "map.scene.create";
    public const string MapSceneGet = "map.scene.get";
    public const string MapSceneUpdateSettings = "map.scene.updateSettings";
    public const string MapSceneArchive = "map.scene.archive";
    public const string MapSceneMarkerList = "map.scene.marker.list";
    public const string MapSceneMarkerAdd = "map.scene.marker.add";
    public const string MapSceneMarkerMove = "map.scene.marker.move";
    public const string MapSceneMarkerUpdate = "map.scene.marker.update";
    public const string MapSceneMarkerRemove = "map.scene.marker.remove";
    public const string MapSceneFogGet = "map.scene.fog.get";
    public const string MapSceneFogSetMode = "map.scene.fog.setMode";
    public const string MapSceneFogPaint = "map.scene.fog.paint";
    public const string MapSceneFogReveal = "map.scene.fog.reveal";
    public const string MapSceneFogHide = "map.scene.fog.hide";
    public const string MapSceneFogClear = "map.scene.fog.clear";
    public const string MapSceneFogFill = "map.scene.fog.fill";
    public const string MapSceneFogReset = "map.scene.fog.reset";
    public const string MapSceneActiveSet = "map.scene.active.set";
    public const string MapSceneActiveGet = "map.scene.active.get";
    public const string MapSceneActiveClear = "map.scene.active.clear";
    public const string MapPlayerSceneActiveGet = "map.player.scene.active.get";
    public const string MapWorldList = "map.world.list";
    public const string MapWorldCreate = "map.world.create";
    public const string MapWorldGet = "map.world.get";
    public const string MapWorldUpdateSettings = "map.world.updateSettings";
    public const string MapWorldArchive = "map.world.archive";
    public const string MapWorldLayerGet = "map.world.layer.get";
    public const string MapWorldLayerUpdateCell = "map.world.layer.updateCell";
    public const string MapWorldLayerPaint = "map.world.layer.paint";
    public const string MapWorldLayerClear = "map.world.layer.clear";
    public const string MapWorldLayerSetVisibility = "map.world.layer.setVisibility";
    public const string MapWorldMarkerList = "map.world.marker.list";
    public const string MapWorldMarkerAdd = "map.world.marker.add";
    public const string MapWorldMarkerMove = "map.world.marker.move";
    public const string MapWorldMarkerUpdate = "map.world.marker.update";
    public const string MapWorldMarkerRemove = "map.world.marker.remove";
    public const string MapPlayerWorldList = "map.player.world.list";
    public const string MapPlayerWorldGet = "map.player.world.get";
    public const string WorldMapAdminGet = "worldMap.admin.get";
    public const string WorldMapAdminSeedMvp = "worldMap.admin.seedMvp";
    public const string WorldMapAdminCreateOrUpdateLocation = "worldMap.admin.createOrUpdateLocation";
    public const string WorldMapAdminCreateOrUpdateRegion = "worldMap.admin.createOrUpdateRegion";
    public const string WorldMapAdminUpdateVisibility = "worldMap.admin.updateVisibility";
    public const string WorldMapAdminValidate = "worldMap.admin.validate";
    public const string WorldMapPlayerLocationGet = "worldMap.player.location.get";
    public const string WorldMapPlayerRegionGet = "worldMap.player.region.get";
    public const string MapRoomList = "map.room.list";
    public const string MapRoomCreate = "map.room.create";
    public const string MapRoomGet = "map.room.get";
    public const string MapRoomUpdate = "map.room.update";
    public const string MapRoomArchive = "map.room.archive";
    public const string MapRoomMarkerList = "map.room.marker.list";
    public const string MapRoomMarkerAdd = "map.room.marker.add";
    public const string MapRoomMarkerMove = "map.room.marker.move";
    public const string MapRoomMarkerUpdate = "map.room.marker.update";
    public const string MapRoomMarkerRemove = "map.room.marker.remove";
    public const string MapPlayerRoomGet = "map.player.room.get";
    public const string MapPlayerRoomList = "map.player.room.list";
    public const string MapMarkerAdd = MapSceneMarkerAdd;
    public const string MapMarkerMove = MapSceneMarkerMove;
    public const string MapMarkerUpdate = MapSceneMarkerUpdate;
    public const string MapMarkerRemove = MapSceneMarkerRemove;
    public const string MapFogGet = MapSceneFogGet;
    public const string MapFogPaint = MapSceneFogPaint;
    public const string MapFogReveal = MapSceneFogReveal;
    public const string MapPlayerSceneGet = "map.player.scene.get";


    public const string DefinitionsClassesGet = "definitions.classes.get";
    public const string DefinitionsRacesGet = "definitions.races.get";
    public const string DefinitionsRaceGet = "definitions.race.get";
    public const string DefinitionsRaceSave = "definitions.race.save";
    public const string DefinitionsRaceArchive = "definitions.race.archive";
    public const string DefinitionsClassGet = "definitions.class.get";
    public const string DefinitionsClassSave = "definitions.class.save";
    public const string DefinitionsClassArchive = "definitions.class.archive";
    public const string DefinitionsSkillsGet = "definitions.skills.get";
    public const string DefinitionsItemsGet = "definitions.items.get";
    public const string CatalogAdminItemsList = "catalog.admin.items.list";
    public const string CatalogAdminItemsGet = "catalog.admin.items.get";
    public const string CatalogAdminItemsCreate = "catalog.admin.items.create";
    public const string CatalogAdminItemsUpdate = "catalog.admin.items.update";
    public const string CatalogAdminItemsArchive = "catalog.admin.items.archive";
    public const string CatalogAdminWeaponsList = "catalog.admin.weapons.list";
    public const string CatalogAdminWeaponsGet = "catalog.admin.weapons.get";
    public const string CatalogAdminWeaponsCreate = "catalog.admin.weapons.create";
    public const string CatalogAdminWeaponsUpdate = "catalog.admin.weapons.update";
    public const string CatalogAdminWeaponsArchive = "catalog.admin.weapons.archive";
    public const string CatalogAdminArmorList = "catalog.admin.armor.list";
    public const string CatalogAdminArmorGet = "catalog.admin.armor.get";
    public const string CatalogAdminArmorCreate = "catalog.admin.armor.create";
    public const string CatalogAdminArmorUpdate = "catalog.admin.armor.update";
    public const string CatalogAdminArmorArchive = "catalog.admin.armor.archive";
    public const string CatalogAdminAmmoList = "catalog.admin.ammo.list";
    public const string CatalogAdminAmmoGet = "catalog.admin.ammo.get";
    public const string CatalogAdminAmmoCreate = "catalog.admin.ammo.create";
    public const string CatalogAdminAmmoUpdate = "catalog.admin.ammo.update";
    public const string CatalogAdminAmmoArchive = "catalog.admin.ammo.archive";
    public const string CatalogAdminEquipmentSlotsList = "catalog.admin.equipmentSlots.list";
    public const string CatalogAdminEquipmentSlotsGet = "catalog.admin.equipmentSlots.get";
    public const string CatalogAdminEquipmentSlotsCreate = "catalog.admin.equipmentSlots.create";
    public const string CatalogAdminEquipmentSlotsUpdate = "catalog.admin.equipmentSlots.update";
    public const string CatalogAdminEquipmentSlotsArchive = "catalog.admin.equipmentSlots.archive";
    public const string CatalogPlayerItemsVisibleList = "catalog.player.items.visibleList";
    public const string CatalogPlayerItemGetVisible = "catalog.player.item.getVisible";
    public const string CatalogPlayerEquipmentSlotsVisibleList = "catalog.player.equipmentSlots.visibleList";
    public const string DefinitionsSkillGet = "definitions.skill.get";
    public const string DefinitionsSkillSave = "definitions.skill.save";
    public const string DefinitionsSkillArchive = "definitions.skill.archive";
    public const string SkillsSave = "skills.save";
    public const string SkillsArchive = "skills.archive";
    public const string DefinitionsReload = "definitions.reload";
    public const string DefinitionsVersionGet = "definitions.version.get";
    public const string DefinitionsContentStatus = "definitions.content.status";
    public const string DefinitionsPackDryRun = "definitions.pack.dryRun";
    public const string EconomyRuntimeSeedApply = "economy.runtimeSeed.apply";
    public const string EconomyFactionsList = "economy.factions.list";
    public const string EconomyFactionGet = "economy.faction.get";
    public const string EconomyOrganizationsList = "economy.organizations.list";
    public const string EconomyOrganizationGet = "economy.organization.get";
    public const string EconomyMarketsList = "economy.markets.list";
    public const string EconomyMarketGet = "economy.market.get";
    public const string EconomyLawsList = "economy.laws.list";
    public const string EconomyLawGet = "economy.law.get";
    public const string EconomyRestrictionsList = "economy.restrictions.list";
    public const string EconomyRestrictionGet = "economy.restriction.get";
    public const string EconomyScopesList = "economy.scopes.list";
    public const string EconomyScopeGet = "economy.scope.get";
    public const string EconomyRelationsGraph = "economy.relations.graph";
    public const string EconomyRelationsFaction = "economy.relations.faction";
    public const string EconomyRelationsOrganization = "economy.relations.organization";
    public const string EconomyRelationsCountry = "economy.relations.country";
    public const string EconomyRelationsCityState = "economy.relations.cityState";
    public const string EconomyRelationsLocation = "economy.relations.location";
    public const string EconomyHoldingsAssetsCharacterBridge = "economy.holdingsAssets.characterBridge";
    public const string EconomyAssetsByCharacter = "economy.assets.byCharacter";
    public const string EconomyAssetsByOrganization = "economy.assets.byOrganization";
    public const string EconomyAssetsByFaction = "economy.assets.byFaction";

    public const string ClassTreeGet = "classTree.get";
    public const string ClassTreeNodeGet = "classTree.node.get";
    public const string ClassTreeAvailableGet = "classTree.available.get";
    public const string ClassTreeAcquireNode = "classTree.acquireNode";
    public const string ClassTreeRecalculate = "classTree.recalculate";

    public const string DevelopmentHexagonGet = "development.hexagon.get";
    public const string DevelopmentNodeList = "development.node.list";
    public const string DevelopmentCharacterGet = "development.character.get";
    public const string DevelopmentCharacterInitialize = "development.character.initialize";
    public const string DevelopmentXpLedgerList = "development.xp.ledger.list";
    public const string DevelopmentXpGrant = "development.xp.grant";
    public const string DevelopmentXpRefund = "development.xp.refund";
    public const string DevelopmentXpCorrect = "development.xp.correct";
    public const string DevelopmentNodePurchase = "development.node.purchase";
    public const string DevelopmentNodeRequestPurchase = "development.node.requestPurchase";
    public const string DevelopmentNodeReveal = "development.node.reveal";
    public const string DevelopmentNodeHide = "development.node.hide";
    public const string DevelopmentNodeUnlock = "development.node.unlock";
    public const string DevelopmentNodeRevoke = "development.node.revoke";
    public const string DevelopmentAdminHexagonGet = "development.admin.hexagon.get";
    public const string DevelopmentAdminNodeUpdate = "development.admin.node.update";
    public const string DevelopmentAdminNodeComplete = "development.admin.node.complete";
    public const string DevelopmentHexagonAdminList = "development.hexagon.admin.list";
    public const string DevelopmentHexagonAdminGetLayout = "development.hexagon.admin.getLayout";
    public const string DevelopmentHexagonAdminGetEditableGraph = "development.hexagon.admin.getEditableGraph";
    public const string DevelopmentHexagonAdminSaveLayout = "development.hexagon.admin.saveLayout";
    public const string DevelopmentHexagonAdminSaveNodeEdit = "development.hexagon.admin.saveNodeEdit";
    public const string DevelopmentHexagonAdminCreateNode = "development.hexagon.admin.createNode";
    public const string DevelopmentHexagonAdminArchiveNode = "development.hexagon.admin.archiveNode";
    public const string DevelopmentHexagonAdminRestoreNode = "development.hexagon.admin.restoreNode";
    public const string DevelopmentHexagonAdminAddRequirementLink = "development.hexagon.admin.addRequirementLink";
    public const string DevelopmentHexagonAdminRemoveRequirementLink = "development.hexagon.admin.removeRequirementLink";
    public const string DevelopmentHexagonAdminValidateGraph = "development.hexagon.admin.validateGraph";
    public const string DevelopmentHexagonAdminResetLayout = "development.hexagon.admin.resetLayout";
    public const string DevelopmentHexagonAdminValidateLayout = "development.hexagon.admin.validateLayout";
    public const string DevelopmentHexagonAdminPreviewLayout = "development.hexagon.admin.previewLayout";
    public const string DevelopmentHexagonAdminPreviewBaselineLayout = "development.hexagon.admin.previewBaselineLayout";
    public const string DevelopmentHexagonAdminApplyBaselineLayout = "development.hexagon.admin.applyBaselineLayout";
    public const string DevelopmentHexagonAdminCreateLayoutSnapshot = "development.hexagon.admin.createLayoutSnapshot";
    public const string DevelopmentHexagonAdminRestoreLayoutSnapshot = "development.hexagon.admin.restoreLayoutSnapshot";
    public const string DevelopmentHexagonAdminGetLayoutQualityReport = "development.hexagon.admin.getLayoutQualityReport";
    public const string DevelopmentHexagonAdminSeedLargeTestTree = "development.hexagon.admin.seedLargeTestTree";
    public const string DevelopmentVocationSet = "development.vocation.set";
    public const string DevelopmentPurchaseRequestApprove = "development.purchaseRequest.approve";
    public const string DevelopmentPurchaseRequestReject = "development.purchaseRequest.reject";
    public const string DevelopmentPlayerHexagonGet = "development.player.hexagon.get";
    public const string DevelopmentHexagonPlayerList = "development.hexagon.player.list";
    public const string DevelopmentHexagonPlayerGetLayout = "development.hexagon.player.getLayout";
    public const string DevelopmentHexagonPlayerGetNodeDetails = "development.hexagon.player.getNodeDetails";
    public const string DevelopmentPlayerPurchase = "development.player.node.purchase";
    public const string DevelopmentPlayerRequestPurchase = "development.player.node.requestPurchase";

    public const string SkillsList = "skills.list";
    public const string SkillsAvailable = "skills.available";
    public const string SkillsGet = "skills.get";
    public const string SkillsAcquire = "skills.acquire";

    public const string AdminClassTreeSetState = "admin.classTree.setState";
    public const string AdminSkillsSetState = "admin.skills.setState";
    public const string AdminCharacterProgressRecalculate = "admin.character.progress.recalculate";

    public const string AdminDefinitionsClassList = "admin.definitions.class.list";
    public const string AdminDefinitionsRaceList = "admin.definitions.race.list";
    public const string AdminDefinitionsRaceGet = "admin.definitions.race.get";
    public const string AdminDefinitionsRaceSave = "admin.definitions.race.save";
    public const string AdminDefinitionsClassGet = "admin.definitions.class.get";
    public const string AdminDefinitionsClassSave = "admin.definitions.class.save";
    public const string AdminDefinitionsSkillList = "admin.definitions.skill.list";
    public const string AdminDefinitionsSkillGet = "admin.definitions.skill.get";
    public const string AdminDefinitionsSkillSave = "admin.definitions.skill.save";

    public const string ProgressionAvailableRaces = "progression.available.races";
    public const string ProgressionAvailableClasses = "progression.available.classes";
    public const string ProgressionAvailableSkills = "progression.available.skills";
    public const string ProgressionPreview = "progression.preview";
    public const string ProgressionSetRace = "progression.set.race";
    public const string ProgressionLearnClass = "progression.learn.class";
    public const string ProgressionLearnSkill = "progression.learn.skill";
    public const string CharacterProgressionGet = "character.progression.get";

    public const string ChatSend = "chat.send";
    public const string ChatHistoryGet = "chat.history.get";
    public const string ChatHistoryLoadMore = "chat.history.loadMore";
    public const string ChatVisibleFeed = "chat.visibleFeed";
    public const string ChatMarkRead = "chat.markRead";
    public const string ChatUnreadGet = "chat.unread.get";

    public const string ChatSlowModeGet = "chat.slowMode.get";
    public const string ChatSlowModeSet = "chat.slowMode.set";
    public const string ChatRestrictionsGet = "chat.restrictions.get";
    public const string ChatRestrictionsMuteUser = "chat.restrictions.muteUser";
    public const string ChatRestrictionsUnmuteUser = "chat.restrictions.unmuteUser";
    public const string ChatRestrictionsLockPlayers = "chat.restrictions.lockPlayers";
    public const string ChatRestrictionsUnlockPlayers = "chat.restrictions.unlockPlayers";


    public const string AudioStateGet = "audio.state.get";
    public const string AudioStateSync = "audio.state.sync";
    public const string AudioModeGet = "audio.mode.get";
    public const string AudioModeSet = "audio.mode.set";
    public const string AudioOverrideClear = "audio.override.clear";

    public const string AudioLibraryGet = "audio.library.get";
    public const string AudioTrackSelect = "audio.track.select";
    public const string AudioTrackNext = "audio.track.next";
    public const string AudioTrackReload = "audio.track.reload";

    public const string AudioClientSettingsGet = "audio.clientSettings.get";
    public const string AudioClientSettingsSet = "audio.clientSettings.set";

    public const string AudioPlayerStateGet = "audio.player.state.get";
    public const string AudioPlayerTracksVisible = "audio.player.tracks.visible";
    public const string AudioPlayerClientSettingsGet = "audio.player.clientSettings.get";
    public const string AudioPlayerClientSettingsUpdate = "audio.player.clientSettings.update";

    public const string AudioAdminTracksList = "audio.admin.tracks.list";
    public const string AudioAdminTracksCreateOrUpdate = "audio.admin.tracks.createOrUpdate";
    public const string AudioAdminStateGet = "audio.admin.state.get";
    public const string AudioAdminStatePlay = "audio.admin.state.play";
    public const string AudioAdminStatePause = "audio.admin.state.pause";
    public const string AudioAdminStateStop = "audio.admin.state.stop";
    public const string AudioAdminStateNext = "audio.admin.state.next";
    public const string AudioAdminStateSetCategory = "audio.admin.state.setCategory";
    public const string AudioAdminStateSetLoopMode = "audio.admin.state.setLoopMode";
    public const string AudioAdminStateSetFade = "audio.admin.state.setFade";
    public const string AudioAdminStateResync = "audio.admin.state.resync";


    public const string VisibilityGet = "visibility.get";
    public const string VisibilityUpdate = "visibility.update";
    public const string CharacterPublicViewGet = "character.publicView.get";
    public const string CharacterVisibleToMeGet = "character.visibleToMe.get";

    public const string NotesCreate = "notes.create";
    public const string NotesList = "notes.list";
    public const string NotesGet = "notes.get";
    public const string NotesUpdate = "notes.update";
    public const string NotesArchive = "notes.archive";

    public const string ReferenceList = "reference.list";
    public const string ReferenceGet = "reference.get";
    public const string ReferenceCreate = "reference.create";
    public const string ReferenceUpdate = "reference.update";
    public const string ReferenceArchive = "reference.archive";
    public const string ReferenceReload = "reference.reload";

    public const string UpdateVersionGet = "update.version.get";
    public const string UpdateManifestGet = "update.manifest.get";
    public const string UpdateClientDownloadInfo = "update.client.downloadInfo";

    public const string BackupCreate = "backups.admin.create";
    public const string BackupList = "backups.admin.list";
    public const string BackupGet = "backups.admin.get";
    public const string BackupVerify = "backups.admin.verify";
    public const string BackupRestorePreview = "backups.admin.restorePreview";
    public const string BackupRestoreExecute = "backups.admin.restore";
    public const string BackupMaintenanceGet = "backups.admin.maintenance.get";
    public const string BackupMaintenanceSet = "backups.admin.maintenance.set";
    public const string BackupOperationGet = "backups.admin.operation.get";
    public const string BackupRestore = "backups.admin.restore.legacy";
    public const string BackupExport = "backups.admin.export";

    public const string DevAccessAdminStatus = "devAccess.admin.status";
    public const string DevAccessAdminResetKnownAccounts = "devAccess.admin.resetKnownAccounts";
    public const string DevAccessAdminPrintKnownCredentials = "devAccess.admin.printKnownCredentials";
    public const string DevAccessAdminVerifyKnownLogin = "devAccess.admin.verifyKnownLogin";
    public const string DevAccessAdminDisableKnownCredentials = "devAccess.admin.disableKnownCredentials";

    public const string DataPortabilityAdminExportDefinitions = "dataPortability.admin.exportDefinitions";
    public const string DataPortabilityAdminValidatePackage = "dataPortability.admin.validatePackage";
    public const string DataPortabilityAdminImportDefinitionsDryRun = "dataPortability.admin.importDefinitionsDryRun";
    public const string DataPortabilityAdminImportDefinitions = "dataPortability.admin.importDefinitions";
    public const string DataPortabilityAdminExportCampaignData = "dataPortability.admin.exportCampaignData";
    public const string DataPortabilityAdminImportCampaignDataDryRun = "dataPortability.admin.importCampaignDataDryRun";
    public const string DataPortabilityAdminImportCampaignData = "dataPortability.admin.importCampaignData";
    public const string DataPortabilityAdminImportPreview = "dataPortability.admin.importPreview";
    public const string DataPortabilityAdminExportList = "dataPortability.admin.exportList";
    public const string DataPortabilityAdminImportList = "dataPortability.admin.importList";

    public const string LegacyBackupCreate = "backup.create";
    public const string LegacyBackupList = "backup.list";
    public const string LegacyBackupGet = "backup.get";
    public const string LegacyBackupVerify = "backup.verify";
    public const string LegacyBackupRestorePreview = "backup.restore.preview";
    public const string LegacyBackupRestoreExecute = "backup.restore.execute";
    public const string LegacyBackupMaintenanceGet = "backup.maintenance.get";
    public const string LegacyBackupMaintenanceSet = "backup.maintenance.set";
    public const string LegacyBackupOperationGet = "backup.operation.get";
    public const string LegacyBackupRestore = "backup.restore";
    public const string LegacyBackupExport = "backup.export";

    public const string LegalAdminDashboardGet = "legal.admin.dashboard.get";
    public const string LegalJurisdictionList = "legal.jurisdiction.list";
    public const string LegalJurisdictionCreate = "legal.jurisdiction.create";
    public const string LegalJurisdictionUpdate = "legal.jurisdiction.update";
    public const string LegalJurisdictionArchive = "legal.jurisdiction.archive";
    public const string LegalProfileList = "legal.profile.list";
    public const string LegalProfileCreate = "legal.profile.create";
    public const string LegalProfileSetActive = "legal.profile.setActive";
    public const string LegalRuleList = "legal.rule.list";
    public const string LegalRuleCreate = "legal.rule.create";
    public const string LegalRuleUpdate = "legal.rule.update";
    public const string LegalRuleArchive = "legal.rule.archive";
    public const string LegalLicenseDefinitionList = "legal.licenseDefinition.list";
    public const string LegalLicenseDefinitionCreate = "legal.licenseDefinition.create";
    public const string LegalLicenseDefinitionUpdate = "legal.licenseDefinition.update";
    public const string LegalLicenseDefinitionArchive = "legal.licenseDefinition.archive";
    public const string LegalEntityLicenseList = "legal.entityLicense.list";
    public const string LegalEntityLicenseIssue = "legal.entityLicense.issue";
    public const string LegalEntityLicenseSuspend = "legal.entityLicense.suspend";
    public const string LegalEntityLicenseRevoke = "legal.entityLicense.revoke";
    public const string LegalApplicationList = "legal.application.list";
    public const string LegalApplicationReview = "legal.application.review";
    public const string LegalCheckRun = "legal.check.run";
    public const string LegalProductionModeSet = "legal.productionMode.set";
    public const string LegalPlayerSummary = "legal.player.summary";
    public const string LegalPlayerLicenseList = "legal.player.license.list";
    public const string LegalPlayerApplicationList = "legal.player.application.list";
    public const string LegalPlayerApplicationSubmit = "legal.player.application.submit";
    public const string LegalPlayerCheckRequest = "legal.player.check.request";

    public const string AdminLocksList = "admin.locks.list";
    public const string AdminLocksForceRelease = "admin.locks.forceRelease";
    public const string AdminServerStatus = "admin.server.status";
    public const string AdminSessionsList = "admin.sessions.list";
    public const string AdminDiagnosticsGet = "admin.diagnostics.get";

    public const string LockAcquire = "lock.acquire";
    public const string LockRelease = "lock.release";
    public const string LockForceRelease = "lock.forceRelease";
    public const string LockStatus = "lock.status";
    public const string SyncSnapshotGet = "sync.snapshot.get";
    public const string SyncChangesGet = "sync.changes.get";
}

public class RequestEnvelope
{
    public string Command { get; set; } = string.Empty;
    public string? RequestId { get; set; }
    public string? AuthToken { get; set; }
    public string? SessionId { get; set; }
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public int Version { get; set; } = 1;
    public Dictionary<string, object> Payload { get; set; } = new Dictionary<string, object>();
}

public class ResponseEnvelope
{
    public string? RequestId { get; set; }
    public ResponseStatus Status { get; set; } = ResponseStatus.Ok;
    public ErrorCode ErrorCode { get; set; } = ErrorCode.None;
    public string Message { get; set; } = string.Empty;
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public DateTime ServerUtc { get; set; } = DateTime.UtcNow;
    public int Version { get; set; } = 1;
    public Dictionary<string, object> Payload { get; set; } = new Dictionary<string, object>();
}

public static class JsonProtocolSerializer
{
    private static readonly Type[] KnownPayloadTypes =
    {
        typeof(string[]),
        typeof(object[]),
        typeof(int[]),
        typeof(long[]),
        typeof(double[]),
        typeof(bool[]),
        typeof(Dictionary<string, object>),
        typeof(Dictionary<string, string>),
        typeof(Dictionary<string, int>),
        typeof(Dictionary<string, long>),
        typeof(Dictionary<string, double>),
        typeof(Dictionary<string, bool>),
        typeof(Dictionary<string, string[]>),
        typeof(Dictionary<string, object[]>)
    };

    private static readonly DataContractJsonSerializerSettings SerializerSettings = new DataContractJsonSerializerSettings
    {
        UseSimpleDictionaryFormat = true,
        KnownTypes = KnownPayloadTypes
    };

    public static string Serialize<T>(T value)
    {
        var serializer = new DataContractJsonSerializer(typeof(T), SerializerSettings);
        object? payloadSafeValue = NormalizeEnvelopePayload(value);

        using (var stream = new MemoryStream())
        {
            serializer.WriteObject(stream, payloadSafeValue);
            return Encoding.UTF8.GetString(stream.ToArray());
        }
    }

    public static T? Deserialize<T>(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return default;

        var serializer = new DataContractJsonSerializer(typeof(T), SerializerSettings);

        using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
        {
            var value = serializer.ReadObject(stream);
            if (value is ResponseEnvelope response)
            {
                response.Payload = NormalizeDeserializedDictionary(response.Payload);
                if (response is T typedResponse) return typedResponse;
            }

            if (value is RequestEnvelope request)
            {
                request.Payload = NormalizeDeserializedDictionary(request.Payload);
                if (request is T typedRequest) return typedRequest;
            }

            if (value is T typed) return typed;
            return default;
        }
    }

    private static object? NormalizeEnvelopePayload<T>(T value)
    {
        if (value is IDictionary<string, object> rootDictionary)
        {
            return NormalizeDictionary(new Dictionary<string, object>(rootDictionary));
        }

        if (value is ResponseEnvelope response)
        {
            return new ResponseEnvelope
            {
                RequestId = response.RequestId,
                Status = response.Status,
                ErrorCode = response.ErrorCode,
                Message = response.Message,
                TimestampUtc = response.TimestampUtc,
                Version = response.Version,
                Payload = NormalizeDictionary(response.Payload)
            };
        }

        if (value is RequestEnvelope request)
        {
            return new RequestEnvelope
            {
                Command = request.Command,
                RequestId = request.RequestId,
                AuthToken = request.AuthToken,
                SessionId = request.SessionId,
                TimestampUtc = request.TimestampUtc,
                Version = request.Version,
                Payload = NormalizeDictionary(request.Payload)
            };
        }

        if (value is IEnumerable enumerable && value is not string)
        {
            return enumerable.Cast<object?>().Select(NormalizeValue).ToArray();
        }

        return value;
    }

    private static Dictionary<string, object> NormalizeDictionary(Dictionary<string, object>? payload)
    {
        var source = payload ?? new Dictionary<string, object>();
        var result = new Dictionary<string, object>(source.Count, StringComparer.Ordinal);
        foreach (var item in source)
        {
            result[item.Key] = NormalizeValue(item.Value);
        }

        return result;
    }

    private static object? NormalizeValue(object? value)
    {
        if (value == null)
        {
            return null;
        }

        if (value is string || value is bool || value is byte || value is sbyte ||
            value is short || value is ushort || value is int || value is uint ||
            value is long || value is ulong || value is float || value is double ||
            value is decimal || value is DateTime || value is Guid)
        {
            return value;
        }

        if (value is IDictionary<string, object> map)
        {
            return NormalizeDictionary(new Dictionary<string, object>(map));
        }

        if (value is IDictionary dictionary)
        {
            var normalized = new Dictionary<string, object>();
            foreach (DictionaryEntry entry in dictionary)
            {
                var key = Convert.ToString(entry.Key) ?? string.Empty;
                normalized[key] = NormalizeValue(entry.Value);
            }

            return normalized;
        }

        if (value is IEnumerable enumerable && value is not string)
        {
            return enumerable.Cast<object?>().Select(NormalizeValue).ToArray();
        }

        return value;
    }

    private static Dictionary<string, object> NormalizeDeserializedDictionary(Dictionary<string, object>? payload)
    {
        var source = payload ?? new Dictionary<string, object>();
        var result = new Dictionary<string, object>(source.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var item in source)
        {
            result[item.Key] = NormalizeDeserializedValue(item.Value) ?? string.Empty;
        }

        return result;
    }

    private static object? NormalizeDeserializedValue(object? value)
    {
        if (value == null) return null;

        if (value is Dictionary<string, object> typed)
            return NormalizeDeserializedDictionary(typed);

        if (value is IDictionary dictionary)
        {
            var plain = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry entry in dictionary)
            {
                var key = Convert.ToString(entry.Key);
                if (string.IsNullOrWhiteSpace(key)) continue;
                plain[key] = NormalizeDeserializedValue(entry.Value) ?? string.Empty;
            }

            if (plain.ContainsKey("key") && plain.ContainsKey("value") && plain.Count <= 3)
                return plain;

            return plain;
        }

        if (TryReadKeyValueObject(value, out var objectKey, out var objectValue))
        {
            return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["key"] = objectKey ?? string.Empty,
                ["value"] = NormalizeDeserializedValue(objectValue) ?? string.Empty
            };
        }

        if (value is IEnumerable enumerable && value is not string)
        {
            var items = enumerable.Cast<object?>()
                .Select(NormalizeDeserializedValue)
                .ToArray();
            var keyValueMap = TryReadKeyValuePairArray(items);
            return keyValueMap != null ? (object)keyValueMap : items;
        }

        return value;
    }

    private static Dictionary<string, object>? TryReadKeyValuePairArray(object?[] items)
    {
        if (items.Length == 0) return null;

        var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            object? keyValue = null;
            object? valueValue = null;
            var hasKey = false;
            var hasValue = false;

            if (item is IDictionary dictionary)
            {
                foreach (DictionaryEntry entry in dictionary)
                {
                    var name = Convert.ToString(entry.Key);
                    if (string.Equals(name, "key", StringComparison.OrdinalIgnoreCase))
                    {
                        keyValue = entry.Value;
                        hasKey = true;
                    }
                    else if (string.Equals(name, "value", StringComparison.OrdinalIgnoreCase))
                    {
                        valueValue = entry.Value;
                        hasValue = true;
                    }
                }
            }
            else if (TryReadKeyValueObject(item, out var objectKey, out var objectValue))
            {
                keyValue = objectKey;
                valueValue = objectValue;
                hasKey = true;
                hasValue = true;
            }
            else
            {
                return null;
            }

            if (!hasKey || !hasValue) return null;
            var key = Convert.ToString(keyValue);
            if (string.IsNullOrWhiteSpace(key)) continue;
            result[key] = valueValue ?? string.Empty;
        }

        return result;
    }

    private static bool TryReadKeyValueObject(object? value, out object? key, out object? itemValue)
    {
        key = null;
        itemValue = null;
        if (value == null) return false;

        var type = value.GetType();
        var keyProperty = type.GetProperty("Key") ?? type.GetProperty("key");
        var valueProperty = type.GetProperty("Value") ?? type.GetProperty("value");
        if (keyProperty == null || valueProperty == null) return false;

        key = keyProperty.GetValue(value, null);
        itemValue = valueProperty.GetValue(value, null);
        return key != null;
    }
}
