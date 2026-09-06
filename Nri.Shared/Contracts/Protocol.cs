using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using Nri.Shared.Diagnostics;

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
    public const string ContextCurrentGet = "context.current.get";
    public const string ContextCharacterSwitch = "context.character.switch";
    public const string GameContextGet = "gameContext.get";
    public const string GameContextCampaignsList = "gameContext.campaigns.list";
    public const string GameContextSelectCampaign = "gameContext.selectCampaign";
    public const string GameContextSessionsList = "gameContext.sessions.list";
    public const string GameContextSelectSession = "gameContext.selectSession";
    public const string GameContextCharactersListEligible = "gameContext.characters.listEligible";
    public const string GameContextSelectCharacter = "gameContext.selectCharacter";
    public const string GameContextClearSession = "gameContext.clearSession";
    public const string GameContextClearCharacter = "gameContext.clearCharacter";
    public const string GameContextRestoreLast = "gameContext.restoreLast";
    public const string GameContextSuperAdminOverrideStart = "gameContext.superAdminOverride.start";
    public const string GameContextSuperAdminOverrideEnd = "gameContext.superAdminOverride.end";
    public const string GameContextSuperAdminCampaignsList = "gameContext.superAdmin.campaigns.list";
    public const string CampaignMembershipList = "campaign.membership.list";
    public const string CampaignMembershipUpsert = "campaign.membership.upsert";
    public const string CampaignMembershipSetStatus = "campaign.membership.setStatus";
    public const string CampaignOwnershipTransfer = "campaign.ownership.transfer";
    public const string CampaignMembershipMigrateLegacy = "campaign.membership.migrateLegacy";
    public const string CampaignSuperAdminCreate = "campaign.superAdmin.create";
    public const string SessionParticipationList = "session.participation.list";
    public const string SessionParticipationUpsert = "session.participation.upsert";
    public const string SessionAttentionGet = "session.attention.get";
    public const string AutomationPolicyList = "automation.policy.list";
    public const string AutomationPolicyUpdate = "automation.policy.update";
    public const string AutomationPolicyDryRun = "automation.policy.dryRun";
    public const string AutomationExecutionList = "automation.execution.list";
    public const string CharacterGetSummary = "character.get.summary";
    public const string CharacterGetCompanions = "character.get.companions";
    public const string CharacterGetInventory = "character.get.inventory";
    public const string CharacterGetReputation = "character.get.reputation";
    public const string CharacterGetHoldings = "character.get.holdings";
    public const string CharacterLanguageSummaryGet = "character.language.summary.get";
    public const string CharacterLanguageTrainingRequirementsGet = "character.language.training.requirements.get";
    public const string CharacterLanguageTrainingStart = "character.language.training.start";
    public const string CharacterLanguageTrainingComplete = "character.language.training.complete";
    public const string CharacterAdminLanguageTrainingCredit = "character.admin.language.training.credit";
    public const string CharacterAdminLanguageTrainingSourceApprove = "character.admin.language.training.source.approve";
    public const string CharacterAdminLanguageGrant = "character.admin.language.grant";
    public const string ContentDefinitionPlayerLanguagesList = "contentDefinition.player.languages.list";
    public const string ContentDefinitionPlayerLanguageGet = "contentDefinition.player.language.get";
    public const string ContentDefinitionAdminLanguageSeedApply = "contentDefinition.admin.languageSeed.apply";
    public const string LanguageComprehensionEvaluate = "language.comprehension.evaluate";

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
    public const string CharacterProfileMigrateMissing = "character.profile.migrateMissing";

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
    public const string CharacterCreationPolicyGet = "characterCreation.policy.get";
    public const string CharacterCreationPolicyUpdate = "characterCreation.policy.update";
    public const string CharacterCreationDefinitionsList = "characterCreation.definitions.list";
    public const string CharacterCreationDraftList = "characterCreation.draft.list";
    public const string CharacterCreationDraftGet = "characterCreation.draft.get";
    public const string CharacterCreationDraftSave = "characterCreation.draft.save";
    public const string CharacterCreationPreview = "characterCreation.preview";
    public const string CharacterCreationSubmit = "characterCreation.submit";
    public const string CharacterCreationCancel = "characterCreation.cancel";
    public const string CharacterCreationAdminPending = "characterCreation.admin.pending";
    public const string CharacterCreationAdminReturn = "characterCreation.admin.return";
    public const string CharacterCreationFinalize = "characterCreation.finalize";
    public const string CharacterCreationAdminStructuralPreview = "characterCreation.admin.structuralPreview";
    public const string CharacterCreationAdminStructuralApply = "characterCreation.admin.structuralApply";
    public const string CharacterCreationFinalizedUpdatePublic = "characterCreation.finalized.updatePublic";
    public const string CharacterTitleList = "character.title.list";
    public const string CharacterTitleSelect = "character.title.select";
    public const string CharacterTitleAdminGrant = "character.title.admin.grant";
    public const string CharacterTitleAdminRevoke = "character.title.admin.revoke";
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

    public const string CharacterPlayerLiveStateGet = "character.player.liveState.get";
    public const string CharacterPlayerActionsGet = "character.player.actions.get";
    public const string CharacterPlayerWeaponStatesGet = "character.player.weaponStates.get";
    public const string CharacterPlayerCapabilitiesGetEffective = "character.player.capabilities.getEffective";
    public const string CharacterPlayerEffectsGet = "character.player.effects.get";
    public const string CharacterPlayerExecutionsGet = "character.player.executions.get";
    public const string CharacterPlayerLoadoutGet = "character.player.loadout.get";
    public const string CharacterPlayerLiveHistoryGet = "character.player.liveHistory.get";
    public const string CharacterPlayerCompanionsLiveSummaryGet = "character.player.companions.liveSummary.get";
    public const string CharacterPlayerActionPreview = "character.player.action.preview";
    public const string CharacterPlayerActionExecute = "character.player.action.execute";
    public const string CharacterPlayerWeaponReloadPreview = "character.player.weapon.reload.preview";
    public const string CharacterPlayerWeaponReload = "character.player.weapon.reload";
    public const string CharacterPlayerWeaponUnload = "character.player.weapon.unload";
    public const string CharacterPlayerWeaponSelectAmmo = "character.player.weapon.selectAmmo";
    public const string CharacterPlayerLoadoutSelectActiveWeapon = "character.player.loadout.selectActiveWeapon";
    public const string CharacterPlayerLoadoutSelectAttackProfile = "character.player.loadout.selectAttackProfile";
    public const string CharacterPlayerActionPrepare = "character.player.action.prepare";
    public const string CharacterPlayerActionInterrupt = "character.player.action.interrupt";
    public const string CharacterPlayerActionStopSustain = "character.player.action.stopSustain";
    public const string CharacterPlayerWeaponConsume = "character.player.weapon.consume";
    public const string CharacterAdminLiveStateGet = "character.admin.liveState.get";
    public const string CharacterAdminLiveStateGetPlayerPreview = "character.admin.liveState.getPlayerPreview";
    public const string ActorAdminLiveStateGet = "actor.admin.liveState.get";
    public const string ActorAdminPartyBoardGet = "actor.admin.partyBoard.get";
    public const string ActorAdminCapacityProfileSet = "actor.admin.capacityProfile.set";
    public const string ActorAdminLiveHistoryGet = "actor.admin.liveHistory.get";
    public const string ActorAdminCapabilitiesGetEffective = "actor.admin.capabilities.getEffective";
    public const string CharacterAdminResourceAdjust = "character.admin.resource.adjust";
    public const string CharacterAdminResourceSet = "character.admin.resource.set";
    public const string ActorAdminEffectApply = "actor.admin.effect.apply";
    public const string ActorAdminEffectRemove = "actor.admin.effect.remove";
    public const string CharacterAdminActionStateAdjust = "character.admin.actionState.adjust";
    public const string CharacterAdminConditionApply = "character.admin.condition.apply";
    public const string CharacterAdminConditionRemove = "character.admin.condition.remove";
    public const string CharacterAdminWeaponStateAdjust = "character.admin.weaponState.adjust";
    public const string ActorAdminLifeStateTransition = "actor.admin.lifeState.transition";
    public const string ActorAdminExecutionAdjust = "actor.admin.execution.adjust";
    public const string ActorAdminLoadoutAdjust = "actor.admin.loadout.adjust";
    public const string ActorAdminLiveStateCompensate = "actor.admin.liveState.compensate";
    public const string ActorAdminRuntimeAdvanceRound = "actor.admin.runtime.advanceRound";
    public const string ActorAdminRuntimeApplyRest = "actor.admin.runtime.applyRest";
    public const string ActorAdminReservationAdjust = "actor.admin.reservation.adjust";

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
    public const string ProjectCraftRecipeList = "project.craft.recipe.list";
    public const string ProjectCraftPreview = "project.craft.preview";
    public const string ProjectCraftCreate = "project.craft.create";
    public const string ProjectCraftSubmit = "project.craft.submit";
    public const string ProjectCraftList = "project.craft.list";
    public const string ProjectCraftGet = "project.craft.get";
    public const string ProjectCraftRequirementConfirm = "project.craft.requirement.confirm";
    public const string ProjectCraftApprove = "project.craft.approve";
    public const string ProjectCraftReject = "project.craft.reject";
    public const string ProjectCraftReserve = "project.craft.reserve";
    public const string ProjectCraftStart = "project.craft.start";
    public const string ProjectCraftStageComplete = "project.craft.stage.complete";
    public const string ProjectCraftComplete = "project.craft.complete";
    public const string ProjectCraftCancel = "project.craft.cancel";
    public const string ProjectCraftFail = "project.craft.fail";
    public const string ProjectCraftAudit = "project.craft.audit";
    public const string ProjectResearchTechnologyList = "project.research.technology.list";
    public const string ProjectResearchPreview = "project.research.preview";
    public const string ProjectResearchCreate = "project.research.create";
    public const string ProjectResearchSubmit = "project.research.submit";
    public const string ProjectResearchList = "project.research.list";
    public const string ProjectResearchGet = "project.research.get";
    public const string ProjectResearchRequirementConfirm = "project.research.requirement.confirm";
    public const string ProjectResearchApprove = "project.research.approve";
    public const string ProjectResearchReject = "project.research.reject";
    public const string ProjectResearchReserve = "project.research.reserve";
    public const string ProjectResearchStart = "project.research.start";
    public const string ProjectResearchStageComplete = "project.research.stage.complete";
    public const string ProjectResearchComplete = "project.research.complete";
    public const string ProjectResearchCancel = "project.research.cancel";
    public const string ProjectResearchFail = "project.research.fail";
    public const string ProjectResearchAudit = "project.research.audit";
    public const string ProjectReverseEngineeringSourceList = "project.reverseEngineering.source.list";
    public const string ProjectReverseEngineeringPreview = "project.reverseEngineering.preview";
    public const string ProjectReverseEngineeringCreate = "project.reverseEngineering.create";
    public const string ProjectReverseEngineeringSubmit = "project.reverseEngineering.submit";
    public const string ProjectReverseEngineeringList = "project.reverseEngineering.list";
    public const string ProjectReverseEngineeringGet = "project.reverseEngineering.get";
    public const string ProjectReverseEngineeringRequirementConfirm = "project.reverseEngineering.requirement.confirm";
    public const string ProjectReverseEngineeringApprove = "project.reverseEngineering.approve";
    public const string ProjectReverseEngineeringReject = "project.reverseEngineering.reject";
    public const string ProjectReverseEngineeringReserve = "project.reverseEngineering.reserve";
    public const string ProjectReverseEngineeringStart = "project.reverseEngineering.start";
    public const string ProjectReverseEngineeringStageComplete = "project.reverseEngineering.stage.complete";
    public const string ProjectReverseEngineeringComplete = "project.reverseEngineering.complete";
    public const string ProjectReverseEngineeringCancel = "project.reverseEngineering.cancel";
    public const string ProjectReverseEngineeringFail = "project.reverseEngineering.fail";
    public const string ProjectReverseEngineeringAudit = "project.reverseEngineering.audit";
    public const string ProjectPrototypeBlueprintList = "project.prototype.blueprint.list";
    public const string ProjectPrototypePreview = "project.prototype.preview";
    public const string ProjectPrototypeCreate = "project.prototype.create";
    public const string ProjectPrototypeSubmit = "project.prototype.submit";
    public const string ProjectPrototypeList = "project.prototype.list";
    public const string ProjectPrototypeGet = "project.prototype.get";
    public const string ProjectPrototypeRequirementConfirm = "project.prototype.requirement.confirm";
    public const string ProjectPrototypeApprove = "project.prototype.approve";
    public const string ProjectPrototypeReject = "project.prototype.reject";
    public const string ProjectPrototypeReserve = "project.prototype.reserve";
    public const string ProjectPrototypeStart = "project.prototype.start";
    public const string ProjectPrototypeStageComplete = "project.prototype.stage.complete";
    public const string ProjectPrototypeTestExecute = "project.prototype.test.execute";
    public const string ProjectPrototypeComplete = "project.prototype.complete";
    public const string ProjectPrototypeCancel = "project.prototype.cancel";
    public const string ProjectPrototypeFail = "project.prototype.fail";
    public const string ProjectPrototypeAudit = "project.prototype.audit";
    public const string ProjectPrototypeRepairAvailableList = "project.prototype.repair.available.list";
    public const string ProjectPrototypeRepairPreview = "project.prototype.repair.preview";
    public const string ProjectPrototypeRepairCreate = "project.prototype.repair.create";
    public const string ProjectPrototypeRepairSubmit = "project.prototype.repair.submit";
    public const string ProjectPrototypeRepairList = "project.prototype.repair.list";
    public const string ProjectPrototypeRepairGet = "project.prototype.repair.get";
    public const string ProjectPrototypeRepairRequirementConfirm = "project.prototype.repair.requirement.confirm";
    public const string ProjectPrototypeRepairApprove = "project.prototype.repair.approve";
    public const string ProjectPrototypeRepairReject = "project.prototype.repair.reject";
    public const string ProjectPrototypeRepairReserve = "project.prototype.repair.reserve";
    public const string ProjectPrototypeRepairStart = "project.prototype.repair.start";
    public const string ProjectPrototypeRepairStageComplete = "project.prototype.repair.stage.complete";
    public const string ProjectPrototypeRepairCancel = "project.prototype.repair.cancel";
    public const string ProjectPrototypeRepairFail = "project.prototype.repair.fail";
    public const string ProjectPrototypeRepairAudit = "project.prototype.repair.audit";
    public const string ProjectPrototypeRetestExecute = "project.prototype.retest.execute";
    public const string ProjectPrototypeProductionApprove = "project.prototype.production.approve";
    public const string ProjectLimitedProductionAvailableList = "project.production.limited.available.list";
    public const string ProjectLimitedProductionPreview = "project.production.limited.preview";
    public const string ProjectLimitedProductionCreate = "project.production.limited.create";
    public const string ProjectLimitedProductionSubmit = "project.production.limited.submit";
    public const string ProjectLimitedProductionList = "project.production.limited.list";
    public const string ProjectLimitedProductionGet = "project.production.limited.get";
    public const string ProjectLimitedProductionRequirementConfirm = "project.production.limited.requirement.confirm";
    public const string ProjectLimitedProductionApprove = "project.production.limited.approve";
    public const string ProjectLimitedProductionReject = "project.production.limited.reject";
    public const string ProjectLimitedProductionReserve = "project.production.limited.reserve";
    public const string ProjectLimitedProductionStart = "project.production.limited.start";
    public const string ProjectLimitedProductionStageComplete = "project.production.limited.stage.complete";
    public const string ProjectLimitedProductionComplete = "project.production.limited.complete";
    public const string ProjectLimitedProductionCancel = "project.production.limited.cancel";
    public const string ProjectLimitedProductionFail = "project.production.limited.fail";
    public const string ProjectLimitedProductionAudit = "project.production.limited.audit";
    public const string ProjectAssetConstructionAvailableList = "project.assetConstruction.available.list";
    public const string ProjectAssetConstructionPreview = "project.assetConstruction.preview";
    public const string ProjectAssetConstructionCreate = "project.assetConstruction.create";
    public const string ProjectAssetConstructionSubmit = "project.assetConstruction.submit";
    public const string ProjectAssetConstructionList = "project.assetConstruction.list";
    public const string ProjectAssetConstructionGet = "project.assetConstruction.get";
    public const string ProjectAssetConstructionRequirementConfirm = "project.assetConstruction.requirement.confirm";
    public const string ProjectAssetConstructionApprove = "project.assetConstruction.approve";
    public const string ProjectAssetConstructionReject = "project.assetConstruction.reject";
    public const string ProjectAssetConstructionReserve = "project.assetConstruction.reserve";
    public const string ProjectAssetConstructionStart = "project.assetConstruction.start";
    public const string ProjectAssetConstructionStageComplete = "project.assetConstruction.stage.complete";
    public const string ProjectAssetConstructionComplete = "project.assetConstruction.complete";
    public const string ProjectAssetConstructionCancel = "project.assetConstruction.cancel";
    public const string ProjectAssetConstructionFail = "project.assetConstruction.fail";
    public const string ProjectAssetConstructionAudit = "project.assetConstruction.audit";
    public const string AssetOperationList = "asset.operation.list";
    public const string AssetOperationGet = "asset.operation.get";
    public const string AssetOperationActivationRequest = "asset.operation.activation.request";
    public const string AssetOperationRequirementConfirm = "asset.operation.requirement.confirm";
    public const string AssetOperationReferenceOptions = "asset.operation.referenceOptions";
    public const string AssetOperationReferencesUpdate = "asset.operation.references.update";
    public const string AssetOperationActivate = "asset.operation.activate";
    public const string AssetMaintenanceMarkDue = "asset.maintenance.markDue";
    public const string ProjectAssetMaintenanceCreate = "project.assetMaintenance.create";
    public const string ProjectAssetMaintenanceSubmit = "project.assetMaintenance.submit";
    public const string ProjectAssetMaintenanceList = "project.assetMaintenance.list";
    public const string ProjectAssetMaintenanceGet = "project.assetMaintenance.get";
    public const string ProjectAssetMaintenanceRequirementConfirm = "project.assetMaintenance.requirement.confirm";
    public const string ProjectAssetMaintenanceApprove = "project.assetMaintenance.approve";
    public const string ProjectAssetMaintenanceReject = "project.assetMaintenance.reject";
    public const string ProjectAssetMaintenanceReserve = "project.assetMaintenance.reserve";
    public const string ProjectAssetMaintenanceStart = "project.assetMaintenance.start";
    public const string ProjectAssetMaintenanceStageComplete = "project.assetMaintenance.stage.complete";
    public const string ProjectAssetMaintenanceComplete = "project.assetMaintenance.complete";
    public const string ProjectAssetMaintenanceCancel = "project.assetMaintenance.cancel";
    public const string ProjectAssetMaintenanceFail = "project.assetMaintenance.fail";
    public const string ProjectAssetMaintenanceAudit = "project.assetMaintenance.audit";

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

    public const string WorldPlayerWeatherGet = "world.player.weather.get";
    public const string WorldPlayerEnvironmentGet = "world.player.environment.get";
    public const string WorldPlayerForecastGet = "world.player.forecast.get";
    public const string WorldPlayerTravelGet = "world.player.travel.get";
    public const string WorldPlayerTravelPreview = "world.player.travel.preview";
    public const string WorldPlayerObserveCurrent = "world.player.observe.current";
    public const string WorldPlayerMeasureEnvironment = "world.player.measure.environment";
    public const string WorldPlayerEstimateDistance = "world.player.estimate.distance";
    public const string WorldPlayerObservationHistoryGet = "world.player.observation.history.get";
    public const string ActorPlayerEnvironmentAssessmentGet = "actor.player.environmentAssessment.get";
    public const string WorldAdminWeatherGet = "world.admin.weather.get";
    public const string WorldAdminEnvironmentGet = "world.admin.environment.get";
    public const string WorldAdminForecastPreview = "world.admin.forecast.preview";
    public const string WorldAdminTravelGet = "world.admin.travel.get";
    public const string WorldAdminTravelPreview = "world.admin.travel.preview";
    public const string WorldAdminWeatherOverride = "world.admin.weather.override";
    public const string WorldAdminWeatherLock = "world.admin.weather.lock";
    public const string WorldAdminWeatherUnlock = "world.admin.weather.unlock";
    public const string WorldAdminForecastPublish = "world.admin.forecast.publish";
    public const string WorldAdminTravelCreate = "world.admin.travel.create";
    public const string WorldAdminTravelStart = "world.admin.travel.start";
    public const string WorldAdminTravelPause = "world.admin.travel.pause";
    public const string WorldAdminTravelResume = "world.admin.travel.resume";
    public const string WorldAdminTravelSegmentComplete = "world.admin.travel.segment.complete";
    public const string WorldAdminTravelCancel = "world.admin.travel.cancel";
    public const string WorldAdminWeatherFixtureEnsure = "world.admin.weather.fixture.ensure";
    public const string WorldAdminEnvironmentFatePreview = "world.admin.environment.fatePreview";
    public const string WorldAdminExposureApprove = "world.admin.exposure.approve";
    public const string WorldAdminMeasurementPreview = "world.admin.measurement.preview";
    public const string ActorAdminEnvironmentAssessmentGet = "actor.admin.environmentAssessment.get";
    public const string WorldAdminEnvironmentImpactPartyGet = "world.admin.environmentImpact.party.get";

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
    public const string CombatV1EncounterList = "combat.v1.encounter.list";
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
    public const string CombatV1PreparedActionTrigger = "combat.v1.action.prepared.trigger";
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
    public const string CombatAdminListForSession = "combat.admin.listForSession";
    public const string CombatAdminGet = "combat.admin.get";
    public const string CombatAdminCreate = "combat.admin.create";
    public const string CombatAdminUpdate = "combat.admin.update";
    public const string CombatAdminArchive = "combat.admin.archive";
    public const string CombatAdminAddParticipant = "combat.admin.addParticipant";
    public const string CombatAdminUpdateParticipant = "combat.admin.updateParticipant";
    public const string CombatAdminRemoveParticipant = "combat.admin.removeParticipant";
    public const string CombatAdminSetParticipantVisibility = "combat.admin.setParticipantVisibility";
    public const string CombatAdminLinkMapToken = "combat.admin.linkMapToken";
    public const string CombatAdminUnlinkMapToken = "combat.admin.unlinkMapToken";
    public const string CombatAdminRollInitiative = "combat.admin.rollInitiative";
    public const string CombatAdminRerollTie = "combat.admin.rerollTie";
    public const string CombatAdminSetInitiativeOrder = "combat.admin.setInitiativeOrder";
    public const string CombatAdminStart = "combat.admin.start";
    public const string CombatAdminPause = "combat.admin.pause";
    public const string CombatAdminResume = "combat.admin.resume";
    public const string CombatAdminNextTurn = "combat.admin.nextTurn";
    public const string CombatAdminSkipTurn = "combat.admin.skipTurn";
    public const string CombatAdminPreviousTurn = "combat.admin.previousTurn";
    public const string CombatAdminEnd = "combat.admin.end";
    public const string CombatAdminAddTurnEvent = "combat.admin.addTurnEvent";
    public const string CombatAdminGetLog = "combat.admin.getLog";
    public const string CombatPlayerGetActiveForSession = "combat.player.getActiveForSession";
    public const string CombatPlayerGetMyTurnState = "combat.player.getMyTurnState";
    public const string CombatPlayerGetLog = "combat.player.getLog";
    public const string CombatMapAdminGetActiveSceneMapOverlay = "combatMap.admin.getActiveSceneMapOverlay";
    public const string CombatMapAdminListJoinableTokens = "combatMap.admin.listJoinableTokens";
    public const string CombatMapAdminAddParticipantFromToken = "combatMap.admin.addParticipantFromToken";
    public const string CombatMapAdminAddParticipantsFromTokens = "combatMap.admin.addParticipantsFromTokens";
    public const string CombatMapAdminLinkParticipantToken = "combatMap.admin.linkParticipantToken";
    public const string CombatMapAdminUnlinkParticipantToken = "combatMap.admin.unlinkParticipantToken";
    public const string CombatMapAdminSyncParticipantVisibilityFromToken = "combatMap.admin.syncParticipantVisibilityFromToken";
    public const string CombatMapAdminSetParticipantMapBadge = "combatMap.admin.setParticipantMapBadge";
    public const string CombatMapAdminFocusParticipantToken = "combatMap.admin.focusParticipantToken";
    public const string CombatMapAdminGetLinkAudit = "combatMap.admin.getLinkAudit";
    public const string CombatMapPlayerGetActiveSceneMapOverlay = "combatMap.player.getActiveSceneMapOverlay";
    public const string CombatMapPlayerGetMyVisibleCombatTokens = "combatMap.player.getMyVisibleCombatTokens";
    public const string CombatMapPlayerMoveMyToken = "combatMap.player.moveMyToken";
    public const string MapSpaceNodeList = "map.spaceNode.list";
    public const string MapSpaceNodeCreate = "map.spaceNode.create";
    public const string MapSceneList = "map.scene.list";
    public const string MapSceneCreate = "map.scene.create";
    public const string MapIdentityResolve = "map.identity.resolve";
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
    public const string WorldMapAdminList = "worldMap.admin.list";
    public const string WorldMapAdminCreate = "worldMap.admin.create";
    public const string WorldMapAdminUpdate = "worldMap.admin.update";
    public const string WorldMapAdminArchive = "worldMap.admin.archive";
    public const string WorldMapAdminSetSessionActive = "worldMap.admin.setSessionActive";
    public const string WorldMapAdminAddMarker = "worldMap.admin.addMarker";
    public const string WorldMapAdminUpdateMarker = "worldMap.admin.updateMarker";
    public const string WorldMapAdminArchiveMarker = "worldMap.admin.archiveMarker";
    public const string WorldMapAdminSeedMvp = "worldMap.admin.seedMvp";
    public const string WorldMapAdminCreateOrUpdateLocation = "worldMap.admin.createOrUpdateLocation";
    public const string WorldMapAdminCreateOrUpdateRegion = "worldMap.admin.createOrUpdateRegion";
    public const string WorldMapAdminUpdateVisibility = "worldMap.admin.updateVisibility";
    public const string WorldMapAdminValidate = "worldMap.admin.validate";
    public const string WorldMapPlayerGetSessionActive = "worldMap.player.getSessionActive";
    public const string WorldMapPlayerLocationGet = "worldMap.player.location.get";
    public const string WorldMapPlayerRegionGet = "worldMap.player.region.get";
    public const string WorldAdminMapsList0218 = "world.admin.maps.list";
    public const string WorldAdminMapGet0218 = "world.admin.map.get";
    public const string WorldAdminMapCreate0218 = "world.admin.map.create";
    public const string WorldAdminMapUpdate0218 = "world.admin.map.update";
    public const string WorldAdminMapValidate0218 = "world.admin.map.validate";
    public const string WorldAdminMapPlayerPreview0218 = "world.admin.map.playerPreview";
    public const string WorldAdminMapHierarchyGet0218 = "world.admin.map.hierarchy.get";
    public const string WorldAdminMapBindingUpdate0218 = "world.admin.map.binding.update";
    public const string WorldAdminMapFeatureCreate0218 = "world.admin.map.feature.create";
    public const string WorldAdminMapFeatureUpdate0218 = "world.admin.map.feature.update";
    public const string WorldAdminMapFeatureArchive0218 = "world.admin.map.feature.archive";
    public const string WorldAdminMapLayerUpdate0218 = "world.admin.map.layer.update";
    public const string WorldAdminMapPortalCreate0218 = "world.admin.map.portal.create";
    public const string WorldAdminMapPortalUpdate0218 = "world.admin.map.portal.update";
    public const string WorldAdminMapDiscoveryGrant0218 = "world.admin.map.discovery.grant";
    public const string WorldAdminMapGeneratePreview0218 = "world.admin.map.generate.preview";
    public const string WorldAdminMapGenerateValidate0218 = "world.admin.map.generate.validate";
    public const string WorldAdminMapGenerateAccept0218 = "world.admin.map.generate.accept";
    public const string WorldAdminMapGenerateRegeneratePreview0218 = "world.admin.map.generate.regeneratePreview";
    public const string WorldAdminMapGeneratePartialPreview0218 = "world.admin.map.generate.partialPreview";
    public const string WorldAdminMapExport0218 = "world.admin.map.export";
    public const string WorldAdminMapImportDryRun0218 = "world.admin.map.import.dryRun";
    public const string WorldAdminMapImportApply0218 = "world.admin.map.import.apply";
    public const string WorldPlayerMapsList0218 = "world.player.maps.list";
    public const string WorldPlayerMapGet0218 = "world.player.map.get";
    public const string WorldPlayerMapChildren0218 = "world.player.map.children";
    public const string WorldPlayerMapPortalOpen0218 = "world.player.map.portal.open";
    public const string WorldPlayerMapFeatureGet0218 = "world.player.map.feature.get";
    public const string WorldPlayerMapDistancePreview0218 = "world.player.map.distance.preview";
    public const string WorldPlayerMapDiscoveryGet0218 = "world.player.map.discovery.get";
    public const string SceneMapAdminList = "sceneMap.admin.list";
    public const string SceneMapAdminGet = "sceneMap.admin.get";
    public const string SceneMapAdminCreate = "sceneMap.admin.create";
    public const string SceneMapAdminUpdate = "sceneMap.admin.update";
    public const string SceneMapAdminArchive = "sceneMap.admin.archive";
    public const string SceneMapAdminSetSessionActive = "sceneMap.admin.setSessionActive";
    public const string SceneMapAdminGetSessionActive = "sceneMap.admin.getSessionActive";
    public const string SceneMapAdminClearSessionActive = "sceneMap.admin.clearSessionActive";
    public const string SceneMapAdminAddMarker = "sceneMap.admin.addMarker";
    public const string SceneMapAdminUpdateMarker = "sceneMap.admin.updateMarker";
    public const string SceneMapAdminArchiveMarker = "sceneMap.admin.archiveMarker";
    public const string SceneMapPlayerGetSessionActive = "sceneMap.player.getSessionActive";
    public const string SceneMapLayerAdminList = "sceneMapLayer.admin.list";
    public const string SceneMapLayerAdminCreate = "sceneMapLayer.admin.create";
    public const string SceneMapLayerAdminUpdate = "sceneMapLayer.admin.update";
    public const string SceneMapLayerAdminArchive = "sceneMapLayer.admin.archive";
    public const string SceneMapLayerAdminReorder = "sceneMapLayer.admin.reorder";
    public const string SceneMapLayerAdminSetVisibility = "sceneMapLayer.admin.setVisibility";
    public const string SceneMapLayerPlayerListForActiveSceneMap = "sceneMapLayer.player.listForActiveSceneMap";
    public const string SceneMapShapeAdminList = "sceneMapShape.admin.list";
    public const string SceneMapShapeAdminGet = "sceneMapShape.admin.get";
    public const string SceneMapShapeAdminCreate = "sceneMapShape.admin.create";
    public const string SceneMapShapeAdminUpdate = "sceneMapShape.admin.update";
    public const string SceneMapShapeAdminMove = "sceneMapShape.admin.move";
    public const string SceneMapShapeAdminResize = "sceneMapShape.admin.resize";
    public const string SceneMapShapeAdminDuplicate = "sceneMapShape.admin.duplicate";
    public const string SceneMapShapeAdminArchive = "sceneMapShape.admin.archive";
    public const string SceneMapShapeAdminSetVisibility = "sceneMapShape.admin.setVisibility";
    public const string SceneMapShapeAdminReorder = "sceneMapShape.admin.reorder";
    public const string SceneMapShapePlayerListForActiveSceneMap = "sceneMapShape.player.listForActiveSceneMap";
    public const string SceneMapTileLayerAdminList = "sceneMapTileLayer.admin.list";
    public const string SceneMapTileLayerAdminCreate = "sceneMapTileLayer.admin.create";
    public const string SceneMapTileLayerAdminUpdate = "sceneMapTileLayer.admin.update";
    public const string SceneMapTileLayerAdminArchive = "sceneMapTileLayer.admin.archive";
    public const string SceneMapTilePatchAdminList = "sceneMapTilePatch.admin.list";
    public const string SceneMapTilePatchAdminPaint = "sceneMapTilePatch.admin.paint";
    public const string SceneMapTilePatchAdminArchive = "sceneMapTilePatch.admin.archive";
    public const string SceneMapAssetInstanceAdminList = "sceneMapAssetInstance.admin.list";
    public const string SceneMapAssetInstanceAdminCreate = "sceneMapAssetInstance.admin.create";
    public const string SceneMapAssetInstanceAdminUpdate = "sceneMapAssetInstance.admin.update";
    public const string SceneMapAssetInstanceAdminArchive = "sceneMapAssetInstance.admin.archive";
    public const string SceneMapTilePatchPlayerListForActiveSceneMap = "sceneMapTilePatch.player.listForActiveSceneMap";
    public const string SceneMapAssetInstancePlayerListForActiveSceneMap = "sceneMapAssetInstance.player.listForActiveSceneMap";
    public const string MapEditorAdminGetState = "mapEditor.admin.getState";
    public const string MapEditorAdminMutate = "mapEditor.admin.mutate";
    public const string SceneMapGeneratorAdminListPresets = "sceneMapGenerator.admin.listPresets";
    public const string SceneMapGeneratorAdminGetPreset = "sceneMapGenerator.admin.getPreset";
    public const string SceneMapGeneratorAdminCreatePreset = "sceneMapGenerator.admin.createPreset";
    public const string SceneMapGeneratorAdminUpdatePreset = "sceneMapGenerator.admin.updatePreset";
    public const string SceneMapGeneratorAdminArchivePreset = "sceneMapGenerator.admin.archivePreset";
    public const string SceneMapGeneratorAdminListTemplates = "sceneMapGenerator.admin.listTemplates";
    public const string SceneMapGeneratorAdminGetTemplate = "sceneMapGenerator.admin.getTemplate";
    public const string SceneMapGeneratorAdminCreateTemplateFromMap = "sceneMapGenerator.admin.createTemplateFromMap";
    public const string SceneMapGeneratorAdminArchiveTemplate = "sceneMapGenerator.admin.archiveTemplate";
    public const string SceneMapGeneratorAdminPreview = "sceneMapGenerator.admin.preview";
    public const string SceneMapGeneratorAdminCancelPreview = "sceneMapGenerator.admin.cancelPreview";
    public const string SceneMapGeneratorAdminGenerate = "sceneMapGenerator.admin.generate";
    public const string SceneMapGeneratorAdminRegenerate = "sceneMapGenerator.admin.regenerate";
    public const string SceneMapGeneratorAdminSavePreviewAsSceneMap = "sceneMapGenerator.admin.savePreviewAsSceneMap";
    public const string SceneMapGeneratorAdminGenerateAndSetSessionActive = "sceneMapGenerator.admin.generateAndSetSessionActive";
    public const string SceneMapGeneratorAdminGetGenerationRun = "sceneMapGenerator.admin.getGenerationRun";
    public const string MapTokenAdminListForMap = "mapToken.admin.listForMap";
    public const string MapTokenAdminGet = "mapToken.admin.get";
    public const string MapTokenAdminCreate = "mapToken.admin.create";
    public const string MapTokenAdminUpdate = "mapToken.admin.update";
    public const string MapTokenAdminMove = "mapToken.admin.move";
    public const string MapTokenAdminArchive = "mapToken.admin.archive";
    public const string MapTokenAdminSetVisibility = "mapToken.admin.setVisibility";
    public const string MapTokenAdminRevealToPlayers = "mapToken.admin.revealToPlayers";
    public const string MapTokenAdminHideFromPlayers = "mapToken.admin.hideFromPlayers";
    public const string MapTokenPlayerListForActiveWorldMap = "mapToken.player.listForActiveWorldMap";
    public const string MapTokenPlayerListForActiveSceneMap = "mapToken.player.listForActiveSceneMap";
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
    public const string MapPlayerSceneSync = "map.player.scene.sync";
    public const string MapAdminPlayerPreviewGet = "map.admin.playerPreview.get";


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
    public const string CoreEquipmentAdminList = "definitions.equipment.admin.list";
    public const string CoreEquipmentAdminGet = "definitions.equipment.admin.get";
    public const string CoreEquipmentAdminSave = "definitions.equipment.admin.save";
    public const string CoreEquipmentAdminClone = "definitions.equipment.admin.clone";
    public const string CoreEquipmentAdminSetArchived = "definitions.equipment.admin.setArchived";
    public const string CoreEquipmentAdminReferences = "definitions.equipment.admin.references";
    public const string CoreEquipmentPlayerList = "definitions.equipment.player.list";
    public const string CoreEquipmentPlayerGet = "definitions.equipment.player.get";
    public const string MagicDefinitionsAdminList = "definitions.magic.admin.list";
    public const string MagicDefinitionsAdminGet = "definitions.magic.admin.get";
    public const string MagicDefinitionsAdminSave = "definitions.magic.admin.save";
    public const string MagicDefinitionsAdminClone = "definitions.magic.admin.clone";
    public const string MagicDefinitionsAdminSetArchived = "definitions.magic.admin.setArchived";
    public const string MagicDefinitionsAdminReferences = "definitions.magic.admin.references";
    public const string MagicDefinitionsPlayerList = "definitions.magic.player.list";
    public const string MagicDefinitionsPlayerGet = "definitions.magic.player.get";
    public const string WorldLoreCalendarPlayerList = "definitions.worldLoreCalendar.player.list";
    public const string WorldLoreCalendarPlayerGet = "definitions.worldLoreCalendar.player.get";
    public const string FactionOrganizationEconomyPlayerList = "definitions.factionOrganizationEconomy.player.list";
    public const string FactionOrganizationEconomyPlayerGet = "definitions.factionOrganizationEconomy.player.get";
    public const string TechnologyRecipeBlueprintProjectPlayerList = "definitions.technologyRecipeBlueprintProject.player.list";
    public const string TechnologyRecipeBlueprintProjectPlayerGet = "definitions.technologyRecipeBlueprintProject.player.get";
    public const string TechnologyBlueprintAdminPrepareFromAsset = "definitions.technologyBlueprint.admin.prepareFromAsset";
    public const string DefinitionsSkillGet = "definitions.skill.get";
    public const string DefinitionsSkillSave = "definitions.skill.save";
    public const string DefinitionsSkillArchive = "definitions.skill.archive";
    public const string SkillsSave = "skills.save";
    public const string SkillsArchive = "skills.archive";
    public const string DefinitionsReload = "definitions.reload";
    public const string DefinitionsVersionGet = "definitions.version.get";
    public const string DefinitionsContentStatus = "definitions.content.status";
    public const string DefinitionsPackDryRun = "definitions.pack.dryRun";
    public const string DefinitionPackAdminPreview = "definitionPack.admin.preview";
    public const string DefinitionPackAdminApply = "definitionPack.admin.apply";
    public const string DefinitionPackAdminStatus = "definitionPack.admin.status";
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
    public const string DevelopmentHexagonPlayerGetProductProjection = "development.hexagon.player.getProductProjection";
    public const string DevelopmentHexagonPlayerAdvanceProductPath = "development.hexagon.player.advanceProductPath";
    public const string DevelopmentHexagonAdminGetProductPreview = "development.hexagon.admin.getProductPreview";
    public const string InitialDevelopmentPlayerGet = "development.initial.player.get";
    public const string InitialDevelopmentPlayerComplete = "development.initial.player.complete";
    public const string InitialDevelopmentAdminReset = "development.initial.admin.reset";
    public const string InitialDevelopmentAdminPolicyGet = "development.initial.admin.policy.get";
    public const string MagicTargetScopeEvaluate = "magic.targetScope.evaluate";
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

    public const string ContentDefinitionAdminListProfiles = "contentDefinition.admin.listProfiles";
    public const string ContentDefinitionAdminGetProfile = "contentDefinition.admin.getProfile";
    public const string ContentDefinitionAdminCreateProfile = "contentDefinition.admin.createProfile";
    public const string ContentDefinitionAdminUpdateProfile = "contentDefinition.admin.updateProfile";
    public const string ContentDefinitionAdminArchiveProfile = "contentDefinition.admin.archiveProfile";
    public const string ContentDefinitionAdminList = "contentDefinition.admin.list";
    public const string ContentDefinitionAdminGet = "contentDefinition.admin.get";
    public const string ContentDefinitionAdminCreate = "contentDefinition.admin.create";
    public const string ContentDefinitionAdminUpdate = "contentDefinition.admin.update";
    public const string ContentDefinitionAdminClone = "contentDefinition.admin.clone";
    public const string ContentDefinitionAdminArchive = "contentDefinition.admin.archive";
    public const string ContentDefinitionAdminRestore = "contentDefinition.admin.restore";
    public const string ContentDefinitionAdminValidate = "contentDefinition.admin.validate";
    public const string ContentDefinitionAdminPreviewAsPlayer = "contentDefinition.admin.previewAsPlayer";
    public const string ContentDefinitionAdminListAudit = "contentDefinition.admin.listAudit";
    public const string ContentDefinitionAdminFindReferences = "contentDefinition.admin.findReferences";
    public const string ContentDefinitionAdminSearchReferenceOptions = "contentDefinition.admin.searchReferenceOptions";
    public const string ContentDefinitionAdminCheckBrokenReferences = "contentDefinition.admin.checkBrokenReferences";
    public const string ContentDefinitionAdminExportProfile = "contentDefinition.admin.exportProfile";
    public const string ContentDefinitionAdminImportProfile = "contentDefinition.admin.importProfile";
    public const string ContentDefinitionPlayerListVisible = "contentDefinition.player.listVisible";
    public const string ContentDefinitionPlayerGetVisible = "contentDefinition.player.getVisible";
    public const string ContentDefinitionPlayerSearchVisible = "contentDefinition.player.searchVisible";
    public const string ContentDefinitionAdminListRaceFamily = "contentDefinition.admin.listRaceFamily";
    public const string ContentDefinitionAdminValidateRaceFamily = "contentDefinition.admin.validateRaceFamily";
    public const string ContentDefinitionAdminPreviewRaceAsPlayer = "contentDefinition.admin.previewRaceAsPlayer";
    public const string ContentDefinitionAdminListAttributeFamily = "contentDefinition.admin.listAttributeFamily";
    public const string ContentDefinitionAdminValidateAttributeFamily = "contentDefinition.admin.validateAttributeFamily";
    public const string ContentDefinitionAdminListSkillDefinitions = "contentDefinition.admin.listSkillDefinitions";
    public const string ContentDefinitionAdminValidateSkillDefinition = "contentDefinition.admin.validateSkillDefinition";
    public const string ContentDefinitionAdminPreviewSkillRow = "contentDefinition.admin.previewSkillRow";
    public const string ContentDefinitionAdminListDevelopmentDefinitions = "contentDefinition.admin.listDevelopmentDefinitions";
    public const string ContentDefinitionAdminValidateDevelopmentNode = "contentDefinition.admin.validateDevelopmentNode";
    public const string ContentDefinitionAdminPreviewDevelopmentNode = "contentDefinition.admin.previewDevelopmentNode";
    public const string ContentDefinitionPlayerListPlayableRaces = "contentDefinition.player.listPlayableRaces";
    public const string ContentDefinitionPlayerGetPlayableRace = "contentDefinition.player.getPlayableRace";
    public const string ContentDefinitionPlayerListVisibleSkills = "contentDefinition.player.listVisibleSkills";
    public const string ContentDefinitionPlayerListVisibleDevelopmentNodes = "contentDefinition.player.listVisibleDevelopmentNodes";

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

    public const string QuestAdminListDefinitions = "quest.admin.listDefinitions";
    public const string QuestAdminGetDefinition = "quest.admin.getDefinition";
    public const string QuestAdminCreateDefinition = "quest.admin.createDefinition";
    public const string QuestAdminUpdateDefinition = "quest.admin.updateDefinition";
    public const string QuestAdminArchiveDefinition = "quest.admin.archiveDefinition";
    public const string QuestAdminListForCampaign = "quest.admin.listForCampaign";
    public const string QuestAdminListForSession = "quest.admin.listForSession";
    public const string QuestAdminGet = "quest.admin.get";
    public const string QuestAdminCreate = "quest.admin.create";
    public const string QuestAdminUpdate = "quest.admin.update";
    public const string QuestAdminArchive = "quest.admin.archive";
    public const string QuestAdminAssign = "quest.admin.assign";
    public const string QuestAdminSetVisibility = "quest.admin.setVisibility";
    public const string QuestAdminAddObjective = "quest.admin.addObjective";
    public const string QuestAdminUpdateObjective = "quest.admin.updateObjective";
    public const string QuestAdminSetObjectiveStatus = "quest.admin.setObjectiveStatus";
    public const string QuestAdminSetObjectiveProgress = "quest.admin.setObjectiveProgress";
    public const string QuestAdminReorderObjectives = "quest.admin.reorderObjectives";
    public const string QuestAdminSetStatus = "quest.admin.setStatus";
    public const string QuestAdminComplete = "quest.admin.complete";
    public const string QuestAdminFail = "quest.admin.fail";
    public const string QuestAdminCancel = "quest.admin.cancel";
    public const string QuestAdminCreateRewardBundle = "quest.admin.createRewardBundle";
    public const string QuestAdminUpdateRewardBundle = "quest.admin.updateRewardBundle";
    public const string QuestAdminPreviewRewards = "quest.admin.previewRewards";
    public const string QuestAdminCreateRewardGrant = "quest.admin.createRewardGrant";
    public const string QuestAdminApplyRewardGrant = "quest.admin.applyRewardGrant";
    public const string QuestAdminGetAudit = "quest.admin.getAudit";
    public const string QuestPlayerListActive = "quest.player.listActive";
    public const string QuestPlayerListAvailable = "quest.player.listAvailable";
    public const string QuestPlayerGet = "quest.player.get";
    public const string QuestPlayerGetJournal = "quest.player.getJournal";
    public const string QuestPlayerGetRewardGrants = "quest.player.getRewardGrants";

    public const string ShopAdminList = "shop.admin.list";
    public const string ShopAdminCreateShop = "shop.admin.createShop";
    public const string ShopAdminGet = "shop.admin.get";
    public const string ShopAdminUpdateShop = "shop.admin.updateShop";
    public const string ShopAdminArchiveShop = "shop.admin.archiveShop";
    public const string ShopAdminCreateOffer = "shop.admin.createOffer";
    public const string ShopAdminUpdateOffer = "shop.admin.updateOffer";
    public const string ShopAdminAdjustStock = "shop.admin.adjustStock";
    public const string ShopAdminListPurchaseRequests = "shop.admin.purchaseRequests.list";
    public const string ShopAdminGetPurchaseRequest = "shop.admin.purchaseRequests.get";
    public const string ShopAdminApprovePurchase = "shop.admin.purchase.approve";
    public const string ShopAdminRejectPurchase = "shop.admin.purchase.reject";
    public const string ShopAdminCompletePurchase = "shop.admin.purchase.complete";
    public const string ShopAdminMarkRequiresProject = "shop.admin.purchase.markRequiresProject";
    public const string ShopAdminGetAudit = "shop.admin.audit";
    public const string ShopPlayerListShops = "shop.player.listShops";
    public const string ShopPlayerListOffers = "shop.player.listOffers";
    public const string ShopPlayerGetOffer = "shop.player.getOffer";
    public const string ShopPlayerRequestPurchase = "shop.player.requestPurchase";
    public const string ShopPlayerRequestSale = "shop.player.requestSale";
    public const string ShopPlayerPurchaseHistory = "shop.player.purchaseHistory";

    public const string RestAdminListForSession = "rest.admin.listForSession";
    public const string RestAdminGet = "rest.admin.get";
    public const string RestAdminCreate = "rest.admin.create";
    public const string RestAdminUpdate = "rest.admin.update";
    public const string RestAdminArchive = "rest.admin.archive";
    public const string RestAdminAddParticipant = "rest.admin.addParticipant";
    public const string RestAdminRemoveParticipant = "rest.admin.removeParticipant";
    public const string RestAdminSetParticipantStatus = "rest.admin.setParticipantStatus";
    public const string RestAdminStart = "rest.admin.start";
    public const string RestAdminComplete = "rest.admin.complete";
    public const string RestAdminInterrupt = "rest.admin.interrupt";
    public const string RestAdminCancel = "rest.admin.cancel";
    public const string RestAdminSetDisturbance = "rest.admin.setDisturbance";
    public const string RestAdminCreateDowntimeAction = "rest.admin.createDowntimeAction";
    public const string RestAdminUpdateDowntimeAction = "rest.admin.updateDowntimeAction";
    public const string RestAdminApproveDowntimeAction = "rest.admin.approveDowntimeAction";
    public const string RestAdminRejectDowntimeAction = "rest.admin.rejectDowntimeAction";
    public const string RestAdminCompleteDowntimeAction = "rest.admin.completeDowntimeAction";
    public const string RestAdminCreateRecoveryGrant = "rest.admin.createRecoveryGrant";
    public const string RestAdminApplyRecoveryGrant = "rest.admin.applyRecoveryGrant";
    public const string RestAdminGetAudit = "rest.admin.getAudit";
    public const string RestPlayerGetActiveForSession = "rest.player.getActiveForSession";
    public const string RestPlayerGetMyRestStatus = "rest.player.getMyRestStatus";
    public const string RestPlayerListMyDowntimeActions = "rest.player.listMyDowntimeActions";
    public const string RestPlayerSubmitDowntimeAction = "rest.player.submitDowntimeAction";
    public const string RestPlayerGetRecoveryGrants = "rest.player.getRecoveryGrants";

    public const string GameplayAdminGetResolutionQueue = "gameplay.admin.getResolutionQueue";
    public const string GameplayAdminResolveQueueItem = "gameplay.admin.resolveQueueItem";
    public const string GameplayPlayerGetMyGameplayStatus = "gameplay.player.getMyGameplayStatus";

    public const string AssetBlueprintPlayerList = "asset_blueprint.player.list";
    public const string AssetBlueprintPlayerGet = "asset_blueprint.player.get";
    public const string AssetBlueprintPlayerCreate = "asset_blueprint.player.create";
    public const string AssetBlueprintPlayerUpdate = "asset_blueprint.player.update";
    public const string AssetBlueprintPlayerDuplicate = "asset_blueprint.player.duplicate";
    public const string AssetBlueprintPlayerArchive = "asset_blueprint.player.archive";
    public const string AssetBlueprintAdminList = "asset_blueprint.admin.list";
    public const string AssetBlueprintAdminGet = "asset_blueprint.admin.get";

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
    public string ClientType { get; set; } = string.Empty;
    public long ConnectionGeneration { get; set; }
    public ClientRuntimeDiagnostics0214? ClientDiagnostics { get; set; }
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
                ClientType = request.ClientType,
                ConnectionGeneration = request.ConnectionGeneration,
                ClientDiagnostics = request.ClientDiagnostics,
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
