using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Nri.PlayerClient.Diagnostics;
using Nri.Shared.Contracts;

namespace Nri.PlayerClient.Networking;

public class CommandApi
{
    private readonly IJsonTcpClient _client;

    public CommandApi(IJsonTcpClient client)
    {
        _client = client;
    }

    public ResponseEnvelope Register(string login, string password) => Send(CommandNames.AuthRegister, new Dictionary<string, object> { { "login", login }, { "password", password } });
    public ResponseEnvelope Login(string login, string password) => Send(CommandNames.AuthLogin, new Dictionary<string, object> { { "login", login }, { "password", password } });
    public ResponseEnvelope ChangePassword(string oldPassword, string newPassword) => Send(CommandNames.AuthChangePassword, new Dictionary<string, object> { { "oldPassword", oldPassword }, { "newPassword", newPassword } });
    public ResponseEnvelope SendSystemFeatureFlagsSnapshotForPlayer() => Send(CommandNames.SystemFeatureFlagsSnapshot);
    public ResponseEnvelope ValidateSession() => Send(CommandNames.SessionValidate);
    public ResponseEnvelope PlayerDashboardGet() => Send(CommandNames.PlayerDashboardGet);
    public ResponseEnvelope PlayerActiveProcessesList() => Send(CommandNames.PlayerActiveProcessesList);
    public ResponseEnvelope PlayerNextActionsList() => Send(CommandNames.PlayerNextActionsList);
    public ResponseEnvelope CharacterPlayerHubGet(string characterId = "") => Send(CommandNames.CharacterPlayerHubGet, new Dictionary<string, object> { { "characterId", characterId } });
    public ResponseEnvelope SearchPlayerQuery(Dictionary<string, object> payload) => Send(CommandNames.SearchPlayerQuery, payload);
    public ResponseEnvelope SearchPlayerOpenTarget(Dictionary<string, object> payload) => Send(CommandNames.SearchPlayerOpenTarget, payload);
    public ResponseEnvelope ContentDefinitionPlayerListVisible(Dictionary<string, object>? payload = null) => Send(CommandNames.ContentDefinitionPlayerListVisible, payload ?? new Dictionary<string, object>());
    public ResponseEnvelope ContentDefinitionPlayerGetVisible(Dictionary<string, object> payload) => Send(CommandNames.ContentDefinitionPlayerGetVisible, payload);
    public ResponseEnvelope ContentDefinitionPlayerSearchVisible(Dictionary<string, object> payload) => Send(CommandNames.ContentDefinitionPlayerSearchVisible, payload);
    public ResponseEnvelope GetProfile() => Send(CommandNames.ProfileGet);
    public ResponseEnvelope UpdateProfile(string displayName, string race, int age, string description, string backstory) => Send(CommandNames.ProfileUpdate, new Dictionary<string, object> { { "displayName", displayName }, { "race", race }, { "age", age }, { "description", description }, { "backstory", backstory } });
    public ResponseEnvelope GetMyCharacters() => Send(CommandNames.CharacterListMine);
    public ResponseEnvelope GetAssignedCharacters(Dictionary<string, object>? payload = null) => Send(CommandNames.CharacterPlayerAssignedList, payload ?? new Dictionary<string, object>());
    public ResponseEnvelope GetAssignedCharacter(string characterId) => Send(CommandNames.CharacterPlayerAssignedGet, new Dictionary<string, object> { { "characterId", characterId } });
    public ResponseEnvelope GetActiveCharacter() => Send(CommandNames.CharacterGetActive);
    public ResponseEnvelope GetApplicationContext() => Send(CommandNames.ContextCurrentGet);
    public ResponseEnvelope GameContextCampaignsList() => Send(CommandNames.GameContextCampaignsList);
    public ResponseEnvelope GameContextSelectCampaign(string campaignId, long revision) => Send(CommandNames.GameContextSelectCampaign, new Dictionary<string, object> { { "campaignId", campaignId }, { "expectedContextRevision", revision } });
    public ResponseEnvelope GameContextSessionsList(string campaignId) => Send(CommandNames.GameContextSessionsList, new Dictionary<string, object> { { "campaignId", campaignId } });
    public ResponseEnvelope GameContextSelectSession(string sessionId, long revision) => Send(CommandNames.GameContextSelectSession, new Dictionary<string, object> { { "sessionId", sessionId }, { "expectedContextRevision", revision } });
    public ResponseEnvelope GameContextCharactersListEligible() => Send(CommandNames.GameContextCharactersListEligible);
    public ResponseEnvelope GameContextSelectCharacter(string characterId, long revision) => Send(CommandNames.GameContextSelectCharacter, new Dictionary<string, object> { { "characterId", characterId }, { "expectedContextRevision", revision } });
    public ResponseEnvelope GameContextRestoreLast() => Send(CommandNames.GameContextRestoreLast);
    public ResponseEnvelope SwitchApplicationContextCharacter(string characterId, long expectedContextRevision) => Send(CommandNames.ContextCharacterSwitch, new Dictionary<string, object> { { "characterId", characterId }, { "expectedContextRevision", expectedContextRevision } });
    public ResponseEnvelope SetActiveCharacter(string characterId) => SwitchApplicationContextCharacter(characterId, -1);
    public ResponseEnvelope CharacterInventoryGet(string characterId) => Send(CommandNames.CharacterInventoryGet, new Dictionary<string, object> { { "characterId", characterId } });
    public ResponseEnvelope CharacterCompanionsGet(string characterId) => Send(CommandNames.CharacterCompanionsGet, new Dictionary<string, object> { { "characterId", characterId } });
    public ResponseEnvelope CharacterHoldingsGet(string characterId) => Send(CommandNames.CharacterHoldingsGet, new Dictionary<string, object> { { "characterId", characterId } });
    public ResponseEnvelope CharacterReputationGet(string characterId) => Send(CommandNames.CharacterReputationGet, new Dictionary<string, object> { { "characterId", characterId } });
    public ResponseEnvelope CharacterSkillsGet(string characterId) => Send(CommandNames.CharacterSkillsGet, new Dictionary<string, object> { { "characterId", characterId } });
    public ResponseEnvelope CharacterLanguageSummaryGet(string characterId) => Send(CommandNames.CharacterLanguageSummaryGet, new Dictionary<string, object> { { "characterId", characterId } });
    public ResponseEnvelope CharacterLanguageTrainingRequirementsGet(string characterId, string languageId) => Send(CommandNames.CharacterLanguageTrainingRequirementsGet, new Dictionary<string, object> { { "characterId", characterId }, { "languageId", languageId } });
    public ResponseEnvelope CharacterLanguageTrainingStart(Dictionary<string, object> payload) => Send(CommandNames.CharacterLanguageTrainingStart, payload);
    public ResponseEnvelope CharacterLanguageTrainingComplete(Dictionary<string, object> payload) => Send(CommandNames.CharacterLanguageTrainingComplete, payload);
    public ResponseEnvelope ContentDefinitionPlayerLanguagesList() => Send(CommandNames.ContentDefinitionPlayerLanguagesList);
    public ResponseEnvelope ContentDefinitionPlayerLanguageGet(string languageId) => Send(CommandNames.ContentDefinitionPlayerLanguageGet, new Dictionary<string, object> { { "languageId", languageId } });
    public ResponseEnvelope CharacterPlayerLiveStateGet(string characterId) => Send(CommandNames.CharacterPlayerLiveStateGet, new Dictionary<string, object> { { "characterId", characterId } });
    public ResponseEnvelope CharacterPlayerCompanionsLiveSummaryGet(string characterId) => Send(CommandNames.CharacterPlayerCompanionsLiveSummaryGet, new Dictionary<string, object> { { "characterId", characterId } });
    public ResponseEnvelope CharacterPlayerActionExecute(Dictionary<string, object> payload) => Send(CommandNames.CharacterPlayerActionExecute, payload);
    public ResponseEnvelope CharacterPlayerWeaponReload(Dictionary<string, object> payload) => Send(CommandNames.CharacterPlayerWeaponReload, payload);
    public ResponseEnvelope CharacterSubAttributesGet(string characterId) => Send(CommandNames.CharacterSubAttributesGet, new Dictionary<string, object> { { "characterId", characterId } });
    public ResponseEnvelope CharacterSkillCheckRoll(string characterId, string skillCode) => Send(CommandNames.CharacterSkillCheckRoll, new Dictionary<string, object> { { "characterId", characterId }, { "skillCode", skillCode } });
    public ResponseEnvelope CharacterSkillCheckRoll(string characterId, string skillCode, string subAttributeId) => Send(CommandNames.CharacterSkillCheckRoll, new Dictionary<string, object> { { "characterId", characterId }, { "skillCode", skillCode }, { "subAttributeId", subAttributeId } });

    public ResponseEnvelope CreateCharacter(Dictionary<string, object> payload) => Send(CommandNames.CharacterCreate, payload);
    public ResponseEnvelope CharacterCreationPolicyGet(Dictionary<string, object> payload) => Send(CommandNames.CharacterCreationPolicyGet, payload);
    public ResponseEnvelope CharacterCreationDefinitionsList(Dictionary<string, object> payload) => Send(CommandNames.CharacterCreationDefinitionsList, payload);
    public ResponseEnvelope CharacterCreationDraftList(Dictionary<string, object> payload) => Send(CommandNames.CharacterCreationDraftList, payload);
    public ResponseEnvelope CharacterCreationDraftGet(Dictionary<string, object> payload) => Send(CommandNames.CharacterCreationDraftGet, payload);
    public ResponseEnvelope CharacterCreationDraftSave(Dictionary<string, object> payload) => Send(CommandNames.CharacterCreationDraftSave, payload);
    public ResponseEnvelope CharacterCreationPreview(Dictionary<string, object> payload) => Send(CommandNames.CharacterCreationPreview, payload);
    public ResponseEnvelope CharacterCreationSubmit(Dictionary<string, object> payload) => Send(CommandNames.CharacterCreationSubmit, payload);
    public ResponseEnvelope CharacterCreationCancel(Dictionary<string, object> payload) => Send(CommandNames.CharacterCreationCancel, payload);
    public ResponseEnvelope CharacterFinalizedUpdatePublic(Dictionary<string, object> payload) => Send(CommandNames.CharacterCreationFinalizedUpdatePublic, payload);
    public ResponseEnvelope CharacterTitleList(Dictionary<string, object> payload) => Send(CommandNames.CharacterTitleList, payload);
    public ResponseEnvelope CharacterTitleSelect(Dictionary<string, object> payload) => Send(CommandNames.CharacterTitleSelect, payload);
    public ResponseEnvelope DiceRollStandard(string formula, string visibility, string description, string? characterId = null)
    {
        var payload = new Dictionary<string, object> { { "formula", formula }, { "visibility", visibility }, { "description", description } };
        if (!string.IsNullOrWhiteSpace(characterId)) payload["characterId"] = characterId;
        return Send(CommandNames.DiceRollStandard, payload);
    }

    public ResponseEnvelope DiceRollTest(string formula, string visibility, string description, string? characterId = null)
    {
        var payload = new Dictionary<string, object> { { "formula", formula }, { "visibility", visibility }, { "description", description } };
        if (!string.IsNullOrWhiteSpace(characterId)) payload["characterId"] = characterId;
        return Send(CommandNames.DiceRollTest, payload);
    }
    public ResponseEnvelope DiceTestGetCurrent() => Send(CommandNames.DiceTestGetCurrent);
    public ResponseEnvelope CreateDiceRequest(string characterId, string formula, string visibility, string description) => Send(CommandNames.DiceRequest, new Dictionary<string, object> { { "characterId", characterId }, { "formula", formula }, { "visibility", visibility }, { "description", description } });
    public ResponseEnvelope CreatePlayerRequest(Dictionary<string, object> payload) => Send(CommandNames.PlayerRequestCreate, payload);
    public ResponseEnvelope SubmitPlayerRequest(string requestId) => Send(CommandNames.PlayerRequestSubmit, new Dictionary<string, object> { { "requestId", requestId } });
    public ResponseEnvelope ResubmitPlayerRequest(Dictionary<string, object> payload) => Send(CommandNames.PlayerRequestResubmit, payload);
    public ResponseEnvelope GetPlayerRequest(string requestId) => Send(CommandNames.PlayerRequestGetMine, new Dictionary<string, object> { { "requestId", requestId } });
    public ResponseEnvelope CommentPlayerRequest(string requestId, string text) => Send(CommandNames.PlayerRequestComment, new Dictionary<string, object> { { "requestId", requestId }, { "text", text } });
    public ResponseEnvelope CancelRequest(string requestId) => Send(CommandNames.PlayerRequestCancel, new Dictionary<string, object> { { "requestId", requestId } });
    public ResponseEnvelope ListMyRequests() => Send(CommandNames.PlayerRequestListMine);
    public ResponseEnvelope ProjectPlayerList(Dictionary<string, object> payload) => Send(CommandNames.ProjectPlayerList, payload);
    public ResponseEnvelope ProjectPlayerGet(Dictionary<string, object> payload) => Send(CommandNames.ProjectPlayerGet, payload);
    public ResponseEnvelope ProjectPlayerDraftCreate(Dictionary<string, object> payload) => Send(CommandNames.ProjectPlayerDraftCreate, payload);
    public ResponseEnvelope ProjectPlayerDraftUpdate(Dictionary<string, object> payload) => Send(CommandNames.ProjectPlayerDraftUpdate, payload);
    public ResponseEnvelope ProjectPlayerDraftSubmit(Dictionary<string, object> payload) => Send(CommandNames.ProjectPlayerDraftSubmit, payload);
    public ResponseEnvelope ProjectPlayerDraftCancel(Dictionary<string, object> payload) => Send(CommandNames.ProjectPlayerDraftCancel, payload);
    public ResponseEnvelope ProposalTypesList(Dictionary<string, object>? payload = null) => Send(CommandNames.ProposalTypesList, payload ?? new Dictionary<string, object>());
    public ResponseEnvelope ProposalStatusExplain(Dictionary<string, object>? payload = null) => Send(CommandNames.ProposalStatusExplain, payload ?? new Dictionary<string, object>());
    public ResponseEnvelope ProposalPlayerTemplateList(Dictionary<string, object>? payload = null) => Send(CommandNames.ProposalPlayerTemplateList, payload ?? new Dictionary<string, object>());
    public ResponseEnvelope ProposalPlayerDraftListMine(Dictionary<string, object>? payload = null) => Send(CommandNames.ProposalPlayerDraftListMine, payload ?? new Dictionary<string, object>());
    public ResponseEnvelope ProposalPlayerDraftGetMine(Dictionary<string, object> payload) => Send(CommandNames.ProposalPlayerDraftGetMine, payload);
    public ResponseEnvelope ProposalPlayerDraftCreate(Dictionary<string, object> payload) => Send(CommandNames.ProposalPlayerDraftCreate, payload);
    public ResponseEnvelope ProposalPlayerDraftUpdate(Dictionary<string, object> payload) => Send(CommandNames.ProposalPlayerDraftUpdate, payload);
    public ResponseEnvelope ProposalPlayerDraftValidate(Dictionary<string, object> payload) => Send(CommandNames.ProposalPlayerDraftValidate, payload);
    public ResponseEnvelope ProposalPlayerDraftPreview(Dictionary<string, object> payload) => Send(CommandNames.ProposalPlayerDraftPreview, payload);
    public ResponseEnvelope ProposalPlayerDraftSubmit(Dictionary<string, object> payload) => Send(CommandNames.ProposalPlayerDraftSubmit, payload);
    public ResponseEnvelope ProposalPlayerDraftCancel(Dictionary<string, object> payload) => Send(CommandNames.ProposalPlayerDraftCancel, payload);
    public ResponseEnvelope ProposalPlayerDraftArchive(Dictionary<string, object> payload) => Send(CommandNames.ProposalPlayerDraftArchive, payload);
    public ResponseEnvelope ProposalPlayerDraftResubmitAfterChanges(Dictionary<string, object> payload) => Send(CommandNames.ProposalPlayerDraftResubmitAfterChanges, payload);
    public ResponseEnvelope ProposalPlayerLinkedOpen(Dictionary<string, object> payload) => Send(CommandNames.ProposalPlayerLinkedOpen, payload);
    public ResponseEnvelope KnowledgePlayerEntityList(Dictionary<string, object> payload) => Send(CommandNames.KnowledgePlayerEntityList, payload);
    public ResponseEnvelope ResearchPlayerList(Dictionary<string, object> payload) => Send(CommandNames.ResearchPlayerList, payload);
    public ResponseEnvelope ResearchPlayerGet(Dictionary<string, object> payload) => Send(CommandNames.ResearchPlayerGet, payload);
    public ResponseEnvelope ResearchPlayerDraftCreate(Dictionary<string, object> payload) => Send(CommandNames.ResearchPlayerDraftCreate, payload);
    public ResponseEnvelope ResearchPlayerDraftSubmit(Dictionary<string, object> payload) => Send(CommandNames.ResearchPlayerDraftSubmit, payload);
    public ResponseEnvelope CraftingPlayerRecipeList(Dictionary<string, object> payload) => Send(CommandNames.CraftingPlayerRecipeList, payload);
    public ResponseEnvelope CraftingPlayerProjectList(Dictionary<string, object> payload) => Send(CommandNames.CraftingPlayerProjectList, payload);
    public ResponseEnvelope CraftingPlayerProjectGet(Dictionary<string, object> payload) => Send(CommandNames.CraftingPlayerProjectGet, payload);
    public ResponseEnvelope CraftingPlayerDraftCreate(Dictionary<string, object> payload) => Send(CommandNames.CraftingPlayerDraftCreate, payload);
    public ResponseEnvelope CraftingPlayerDraftSubmit(Dictionary<string, object> payload) => Send(CommandNames.CraftingPlayerDraftSubmit, payload);
    public ResponseEnvelope ProjectCraftRecipeList(Dictionary<string, object> payload) => Send(CommandNames.ProjectCraftRecipeList, payload);
    public ResponseEnvelope ProjectCraftPreview(Dictionary<string, object> payload) => Send(CommandNames.ProjectCraftPreview, payload);
    public ResponseEnvelope ProjectCraftCreate(Dictionary<string, object> payload) => Send(CommandNames.ProjectCraftCreate, payload);
    public ResponseEnvelope ProjectCraftSubmit(Dictionary<string, object> payload) => Send(CommandNames.ProjectCraftSubmit, payload);
    public ResponseEnvelope ProjectCraftList(Dictionary<string, object> payload) => Send(CommandNames.ProjectCraftList, payload);
    public ResponseEnvelope ProjectCraftGet(Dictionary<string, object> payload) => Send(CommandNames.ProjectCraftGet, payload);
    public ResponseEnvelope ProjectCraftCancel(Dictionary<string, object> payload) => Send(CommandNames.ProjectCraftCancel, payload);
    public ResponseEnvelope ProjectAssetConstructionAvailableList(Dictionary<string, object> payload) => Send(CommandNames.ProjectAssetConstructionAvailableList, payload);
    public ResponseEnvelope ProjectAssetConstructionPreview(Dictionary<string, object> payload) => Send(CommandNames.ProjectAssetConstructionPreview, payload);
    public ResponseEnvelope ProjectAssetConstructionCreate(Dictionary<string, object> payload) => Send(CommandNames.ProjectAssetConstructionCreate, payload);
    public ResponseEnvelope ProjectAssetConstructionSubmit(Dictionary<string, object> payload) => Send(CommandNames.ProjectAssetConstructionSubmit, payload);
    public ResponseEnvelope ProjectAssetConstructionList(Dictionary<string, object> payload) => Send(CommandNames.ProjectAssetConstructionList, payload);
    public ResponseEnvelope ProjectAssetConstructionGet(Dictionary<string, object> payload) => Send(CommandNames.ProjectAssetConstructionGet, payload);
    public ResponseEnvelope ProjectAssetConstructionCancel(Dictionary<string, object> payload) => Send(CommandNames.ProjectAssetConstructionCancel, payload);
    public ResponseEnvelope AssetOperationList(Dictionary<string, object> payload) => Send(CommandNames.AssetOperationList, payload);
    public ResponseEnvelope AssetOperationGet(Dictionary<string, object> payload) => Send(CommandNames.AssetOperationGet, payload);
    public ResponseEnvelope AssetOperationActivationRequest(Dictionary<string, object> payload) => Send(CommandNames.AssetOperationActivationRequest, payload);
    public ResponseEnvelope ProjectAssetMaintenanceCreate(Dictionary<string, object> payload) => Send(CommandNames.ProjectAssetMaintenanceCreate, payload);
    public ResponseEnvelope ProjectAssetMaintenanceSubmit(Dictionary<string, object> payload) => Send(CommandNames.ProjectAssetMaintenanceSubmit, payload);
    public ResponseEnvelope ProjectAssetMaintenanceList(Dictionary<string, object> payload) => Send(CommandNames.ProjectAssetMaintenanceList, payload);
    public ResponseEnvelope ProjectAssetMaintenanceGet(Dictionary<string, object> payload) => Send(CommandNames.ProjectAssetMaintenanceGet, payload);
    public ResponseEnvelope ProjectAssetMaintenanceCancel(Dictionary<string, object> payload) => Send(CommandNames.ProjectAssetMaintenanceCancel, payload);
    public ResponseEnvelope ProjectResearchTechnologyList(Dictionary<string, object> payload) => Send(CommandNames.ProjectResearchTechnologyList, payload);
    public ResponseEnvelope ProjectResearchPreview(Dictionary<string, object> payload) => Send(CommandNames.ProjectResearchPreview, payload);
    public ResponseEnvelope ProjectResearchCreate(Dictionary<string, object> payload) => Send(CommandNames.ProjectResearchCreate, payload);
    public ResponseEnvelope ProjectResearchSubmit(Dictionary<string, object> payload) => Send(CommandNames.ProjectResearchSubmit, payload);
    public ResponseEnvelope ProjectResearchList(Dictionary<string, object> payload) => Send(CommandNames.ProjectResearchList, payload);
    public ResponseEnvelope ProjectResearchGet(Dictionary<string, object> payload) => Send(CommandNames.ProjectResearchGet, payload);
    public ResponseEnvelope ProjectResearchCancel(Dictionary<string, object> payload) => Send(CommandNames.ProjectResearchCancel, payload);
    public ResponseEnvelope ProjectReverseEngineeringSourceList(Dictionary<string, object> payload) => Send(CommandNames.ProjectReverseEngineeringSourceList, payload);
    public ResponseEnvelope ProjectReverseEngineeringPreview(Dictionary<string, object> payload) => Send(CommandNames.ProjectReverseEngineeringPreview, payload);
    public ResponseEnvelope ProjectReverseEngineeringCreate(Dictionary<string, object> payload) => Send(CommandNames.ProjectReverseEngineeringCreate, payload);
    public ResponseEnvelope ProjectReverseEngineeringSubmit(Dictionary<string, object> payload) => Send(CommandNames.ProjectReverseEngineeringSubmit, payload);
    public ResponseEnvelope ProjectReverseEngineeringList(Dictionary<string, object> payload) => Send(CommandNames.ProjectReverseEngineeringList, payload);
    public ResponseEnvelope ProjectReverseEngineeringGet(Dictionary<string, object> payload) => Send(CommandNames.ProjectReverseEngineeringGet, payload);
    public ResponseEnvelope ProjectReverseEngineeringCancel(Dictionary<string, object> payload) => Send(CommandNames.ProjectReverseEngineeringCancel, payload);
    public ResponseEnvelope ProjectPrototypeBlueprintList(Dictionary<string, object> payload) => Send(CommandNames.ProjectPrototypeBlueprintList, payload);
    public ResponseEnvelope ProjectPrototypePreview(Dictionary<string, object> payload) => Send(CommandNames.ProjectPrototypePreview, payload);
    public ResponseEnvelope ProjectPrototypeCreate(Dictionary<string, object> payload) => Send(CommandNames.ProjectPrototypeCreate, payload);
    public ResponseEnvelope ProjectPrototypeSubmit(Dictionary<string, object> payload) => Send(CommandNames.ProjectPrototypeSubmit, payload);
    public ResponseEnvelope ProjectPrototypeList(Dictionary<string, object> payload) => Send(CommandNames.ProjectPrototypeList, payload);
    public ResponseEnvelope ProjectPrototypeGet(Dictionary<string, object> payload) => Send(CommandNames.ProjectPrototypeGet, payload);
    public ResponseEnvelope ProjectPrototypeCancel(Dictionary<string, object> payload) => Send(CommandNames.ProjectPrototypeCancel, payload);
    public ResponseEnvelope ProjectPrototypeRepairAvailableList(Dictionary<string, object> payload) => Send(CommandNames.ProjectPrototypeRepairAvailableList, payload);
    public ResponseEnvelope ProjectPrototypeRepairPreview(Dictionary<string, object> payload) => Send(CommandNames.ProjectPrototypeRepairPreview, payload);
    public ResponseEnvelope ProjectPrototypeRepairCreate(Dictionary<string, object> payload) => Send(CommandNames.ProjectPrototypeRepairCreate, payload);
    public ResponseEnvelope ProjectPrototypeRepairSubmit(Dictionary<string, object> payload) => Send(CommandNames.ProjectPrototypeRepairSubmit, payload);
    public ResponseEnvelope ProjectPrototypeRepairList(Dictionary<string, object> payload) => Send(CommandNames.ProjectPrototypeRepairList, payload);
    public ResponseEnvelope ProjectPrototypeRepairGet(Dictionary<string, object> payload) => Send(CommandNames.ProjectPrototypeRepairGet, payload);
    public ResponseEnvelope ProjectPrototypeRepairCancel(Dictionary<string, object> payload) => Send(CommandNames.ProjectPrototypeRepairCancel, payload);
    public ResponseEnvelope ProjectLimitedProductionAvailableList(Dictionary<string, object> payload) => Send(CommandNames.ProjectLimitedProductionAvailableList, payload);
    public ResponseEnvelope ProjectLimitedProductionPreview(Dictionary<string, object> payload) => Send(CommandNames.ProjectLimitedProductionPreview, payload);
    public ResponseEnvelope ProjectLimitedProductionCreate(Dictionary<string, object> payload) => Send(CommandNames.ProjectLimitedProductionCreate, payload);
    public ResponseEnvelope ProjectLimitedProductionSubmit(Dictionary<string, object> payload) => Send(CommandNames.ProjectLimitedProductionSubmit, payload);
    public ResponseEnvelope ProjectLimitedProductionList(Dictionary<string, object> payload) => Send(CommandNames.ProjectLimitedProductionList, payload);
    public ResponseEnvelope ProjectLimitedProductionGet(Dictionary<string, object> payload) => Send(CommandNames.ProjectLimitedProductionGet, payload);
    public ResponseEnvelope ProjectLimitedProductionCancel(Dictionary<string, object> payload) => Send(CommandNames.ProjectLimitedProductionCancel, payload);
    public ResponseEnvelope EngineeringPlayerPlatformList(Dictionary<string, object> payload) => Send(CommandNames.EngineeringPlayerPlatformList, payload);
    public ResponseEnvelope EngineeringPlayerModuleList(Dictionary<string, object> payload) => Send(CommandNames.EngineeringPlayerModuleList, payload);
    public ResponseEnvelope EngineeringPlayerPresetList(Dictionary<string, object> payload) => Send(CommandNames.EngineeringPlayerPresetList, payload);
    public ResponseEnvelope EngineeringPlayerDraftList(Dictionary<string, object> payload) => Send(CommandNames.EngineeringPlayerDraftList, payload);
    public ResponseEnvelope EngineeringPlayerDraftCreate(Dictionary<string, object> payload) => Send(CommandNames.EngineeringPlayerDraftCreate, payload);
    public ResponseEnvelope EngineeringPlayerDraftUpdate(Dictionary<string, object> payload) => Send(CommandNames.EngineeringPlayerDraftUpdate, payload);
    public ResponseEnvelope EngineeringPlayerDraftValidate(Dictionary<string, object> payload) => Send(CommandNames.EngineeringPlayerDraftValidate, payload);
    public ResponseEnvelope EngineeringPlayerDraftSubmit(Dictionary<string, object> payload) => Send(CommandNames.EngineeringPlayerDraftSubmit, payload);
    public ResponseEnvelope EngineeringPlayerProjectList(Dictionary<string, object> payload) => Send(CommandNames.EngineeringPlayerProjectList, payload);
    public ResponseEnvelope EngineeringPlayerBlueprintList(Dictionary<string, object> payload) => Send(CommandNames.EngineeringPlayerBlueprintList, payload);
    public ResponseEnvelope EngineeringPlayerBlueprintGet(Dictionary<string, object> payload) => Send(CommandNames.EngineeringPlayerBlueprintGet, payload);
    public ResponseEnvelope ProductionPlayerFacilityList(Dictionary<string, object> payload) => Send(CommandNames.ProductionPlayerFacilityList, payload);
    public ResponseEnvelope FactoryPlayerQuoteList(Dictionary<string, object> payload) => Send(CommandNames.FactoryPlayerQuoteList, payload);
    public ResponseEnvelope FactoryPlayerQuoteRequest(Dictionary<string, object> payload) => Send(CommandNames.FactoryPlayerQuoteRequest, payload);
    public ResponseEnvelope FactoryPlayerQuoteAccept(Dictionary<string, object> payload) => Send(CommandNames.FactoryPlayerQuoteAccept, payload);
    public ResponseEnvelope FactoryPlayerQuoteReject(Dictionary<string, object> payload) => Send(CommandNames.FactoryPlayerQuoteReject, payload);
    public ResponseEnvelope FactoryPlayerOrderList(Dictionary<string, object> payload) => Send(CommandNames.FactoryPlayerOrderList, payload);
    public ResponseEnvelope FactoryPlayerOrderRequest(Dictionary<string, object> payload) => Send(CommandNames.FactoryPlayerOrderRequest, payload);
    public ResponseEnvelope ManufacturingPlayerProjectList(Dictionary<string, object> payload) => Send(CommandNames.ManufacturingPlayerProjectList, payload);
    public ResponseEnvelope ManufacturingPlayerProjectGet(Dictionary<string, object> payload) => Send(CommandNames.ManufacturingPlayerProjectGet, payload);
    public ResponseEnvelope ManufacturingPlayerAssetList(Dictionary<string, object> payload) => Send(CommandNames.ManufacturingPlayerAssetList, payload);
    public ResponseEnvelope ManufacturingPlayerContributionSubmit(Dictionary<string, object> payload) => Send(CommandNames.ManufacturingPlayerContributionSubmit, payload);
    public ResponseEnvelope DiceHistory() => Send(CommandNames.DiceHistory);
    public ResponseEnvelope DiceVisibleFeed() => Send(CommandNames.DiceVisibleFeed);


    public ResponseEnvelope ClassTreeGet(string characterId) => Send(CommandNames.ClassTreeGet, new Dictionary<string, object> { { "characterId", characterId } });
    public ResponseEnvelope ClassTreeAvailable(string characterId) => Send(CommandNames.ClassTreeAvailableGet, new Dictionary<string, object> { { "characterId", characterId } });
    public ResponseEnvelope ClassTreeAcquireNode(string characterId, string nodeId) => Send(CommandNames.ClassTreeAcquireNode, new Dictionary<string, object> { { "characterId", characterId }, { "nodeId", nodeId } });
    public ResponseEnvelope DevelopmentPlayerHexagonGet(string characterId) => Send(CommandNames.DevelopmentPlayerHexagonGet, new Dictionary<string, object> { { "characterId", characterId } });
    public ResponseEnvelope DevelopmentHexagonPlayerList(string characterId) => Send(CommandNames.DevelopmentHexagonPlayerList, new Dictionary<string, object> { { "characterId", characterId } });
    public ResponseEnvelope DevelopmentHexagonPlayerGetLayout(string characterId, string hexagonId) => Send(CommandNames.DevelopmentHexagonPlayerGetLayout, new Dictionary<string, object> { { "characterId", characterId }, { "hexagonId", hexagonId } });
    public ResponseEnvelope DevelopmentHexagonPlayerGetNodeDetails(string characterId, string nodeId, string hexagonId = "") => Send(CommandNames.DevelopmentHexagonPlayerGetNodeDetails, new Dictionary<string, object> { { "characterId", characterId }, { "nodeId", nodeId }, { "hexagonId", hexagonId } });
    public ResponseEnvelope DevelopmentHexagonPlayerGetProductProjection(string characterId, string hexagonId, string viewMode = "overview", string directionKey = "", string pathKey = "") => Send(CommandNames.DevelopmentHexagonPlayerGetProductProjection, new Dictionary<string, object> { { "characterId", characterId }, { "hexagonId", hexagonId }, { "viewMode", viewMode }, { "directionKey", directionKey }, { "pathKey", pathKey } });
    public ResponseEnvelope DevelopmentHexagonPlayerAdvanceProductPath(string characterId, string hexagonId, string presentationKey, int expectedRevision, string operationId) => Send(CommandNames.DevelopmentHexagonPlayerAdvanceProductPath, new Dictionary<string, object> { { "characterId", characterId }, { "hexagonId", hexagonId }, { "presentationKey", presentationKey }, { "expectedRevision", expectedRevision }, { "operationId", operationId } });
    public ResponseEnvelope InitialDevelopmentGet(string characterId) => Send(CommandNames.InitialDevelopmentPlayerGet, new Dictionary<string, object> { ["characterId"] = characterId });
    public ResponseEnvelope InitialDevelopmentComplete(string characterId, int expectedRevision, string operationId, IEnumerable<Dictionary<string, object>> classGrants, string magicMethodNodeId, string basicMagicDirectionNodeId) => Send(CommandNames.InitialDevelopmentPlayerComplete, new Dictionary<string, object>
    {
        ["characterId"] = characterId,
        ["expectedRevision"] = expectedRevision,
        ["operationId"] = operationId,
        ["classGrants"] = classGrants.Cast<object>().ToArray(),
        ["magicMethodNodeId"] = magicMethodNodeId,
        ["basicMagicDirectionNodeId"] = basicMagicDirectionNodeId
    });
    public ResponseEnvelope DevelopmentPlayerPurchase(string characterId, string nodeId, string hexagonId = "") => Send(CommandNames.DevelopmentPlayerPurchase, new Dictionary<string, object> { { "characterId", characterId }, { "nodeId", nodeId }, { "hexagonId", hexagonId } });
    public ResponseEnvelope DevelopmentPlayerRequestPurchase(string characterId, string nodeId, string comment = "", string hexagonId = "") => Send(CommandNames.DevelopmentPlayerRequestPurchase, new Dictionary<string, object> { { "characterId", characterId }, { "nodeId", nodeId }, { "hexagonId", hexagonId }, { "comment", comment } });
    public ResponseEnvelope DevelopmentXpLedgerList(string characterId) => Send(CommandNames.DevelopmentXpLedgerList, new Dictionary<string, object> { { "characterId", characterId } });
    public ResponseEnvelope ProgressionAvailableSkills(string characterId) => Send(CommandNames.ProgressionAvailableSkills, new Dictionary<string, object> { { "characterId", characterId } });
    public ResponseEnvelope SkillsList(string characterId) => Send(CommandNames.SkillsList, new Dictionary<string, object> { { "characterId", characterId } });
    public ResponseEnvelope SkillsAcquire(string characterId, string skillId) => Send(CommandNames.SkillsAcquire, new Dictionary<string, object> { { "characterId", characterId }, { "skillCode", skillId } });

    public ResponseEnvelope CombatVisibleState(string sessionId) => Send(CommandNames.CombatVisibleState, new Dictionary<string, object> { { "sessionId", sessionId } });
    public ResponseEnvelope CombatTimeline(string sessionId) => Send(CommandNames.CombatTimeline, new Dictionary<string, object> { { "sessionId", sessionId } });
    public ResponseEnvelope CombatV1PlayerSnapshot(Dictionary<string, object> payload) => Send(CommandNames.CombatV1PlayerSnapshot, payload);
    public ResponseEnvelope CombatV1AttackRoll(Dictionary<string, object> payload) => Send(CommandNames.CombatV1AttackRoll, payload);
    public ResponseEnvelope CombatV1WeaponAttackResolve(Dictionary<string, object> payload) => Send(CommandNames.CombatV1WeaponAttackResolve, payload);
    public ResponseEnvelope CombatV1ActionDeclare(Dictionary<string, object> payload) => Send(CommandNames.CombatV1ActionDeclare, payload);
    public ResponseEnvelope CombatV1PlayerFeed(Dictionary<string, object> payload) => Send(CommandNames.CombatV1PlayerFeed, payload);
    public ResponseEnvelope CombatPlayerGetActiveForSession(Dictionary<string, object> payload) => Send(CommandNames.CombatPlayerGetActiveForSession, payload);
    public ResponseEnvelope CombatPlayerGetMyTurnState(Dictionary<string, object> payload) => Send(CommandNames.CombatPlayerGetMyTurnState, payload);
    public ResponseEnvelope CombatPlayerGetLog(Dictionary<string, object> payload) => Send(CommandNames.CombatPlayerGetLog, payload);
    public ResponseEnvelope CombatMapPlayerGetActiveSceneMapOverlay(Dictionary<string, object> payload) => Send(CommandNames.CombatMapPlayerGetActiveSceneMapOverlay, payload);
    public ResponseEnvelope CombatMapPlayerGetMyVisibleCombatTokens(Dictionary<string, object> payload) => Send(CommandNames.CombatMapPlayerGetMyVisibleCombatTokens, payload);
    public ResponseEnvelope CombatMapPlayerMoveMyToken(Dictionary<string, object> payload) => Send(CommandNames.CombatMapPlayerMoveMyToken, payload);
    public ResponseEnvelope SessionPlayerCurrentGet(Dictionary<string, object> payload) => Send(CommandNames.SessionPlayerCurrentGet, payload);
    public ResponseEnvelope GroupPlayerActiveGet(Dictionary<string, object> payload) => Send(CommandNames.GroupPlayerActiveGet, payload);
    public ResponseEnvelope GroupPlayerListVisible(Dictionary<string, object> payload) => Send(CommandNames.GroupPlayerListVisible, payload);
    public ResponseEnvelope GroupPlayerGetVisible(Dictionary<string, object> payload) => Send(CommandNames.GroupPlayerGetVisible, payload);
    public ResponseEnvelope MapPlayerSceneGet(Dictionary<string, object> payload) => Send(CommandNames.MapPlayerSceneGet, payload);
    public ResponseEnvelope MapPlayerSceneActiveGet(Dictionary<string, object> payload) => Send(CommandNames.MapPlayerSceneActiveGet, payload);
    public ResponseEnvelope MapPlayerSceneSync(Dictionary<string, object> payload) => Send(CommandNames.MapPlayerSceneSync, payload);
    public ResponseEnvelope MapPlayerWorldList(Dictionary<string, object> payload) => Send(CommandNames.MapPlayerWorldList, payload);
    public ResponseEnvelope MapPlayerWorldGet(Dictionary<string, object> payload) => Send(CommandNames.MapPlayerWorldGet, payload);
    public ResponseEnvelope WorldMapPlayerGetSessionActive(Dictionary<string, object> payload) => Send(CommandNames.WorldMapPlayerGetSessionActive, payload);
    public ResponseEnvelope MapTokenPlayerListForActiveWorldMap(Dictionary<string, object> payload) => Send(CommandNames.MapTokenPlayerListForActiveWorldMap, payload);
    public ResponseEnvelope MapTokenPlayerListForActiveSceneMap(Dictionary<string, object> payload) => Send(CommandNames.MapTokenPlayerListForActiveSceneMap, payload);
    public ResponseEnvelope SceneMapLayerPlayerListForActiveSceneMap(Dictionary<string, object> payload) => Send(CommandNames.SceneMapLayerPlayerListForActiveSceneMap, payload);
    public ResponseEnvelope SceneMapShapePlayerListForActiveSceneMap(Dictionary<string, object> payload) => Send(CommandNames.SceneMapShapePlayerListForActiveSceneMap, payload);
    public ResponseEnvelope SceneMapTilePatchPlayerListForActiveSceneMap(Dictionary<string, object> payload) => Send(CommandNames.SceneMapTilePatchPlayerListForActiveSceneMap, payload);
    public ResponseEnvelope SceneMapAssetInstancePlayerListForActiveSceneMap(Dictionary<string, object> payload) => Send(CommandNames.SceneMapAssetInstancePlayerListForActiveSceneMap, payload);
    public ResponseEnvelope WorldMapPlayerLocationGet(Dictionary<string, object> payload) => Send(CommandNames.WorldMapPlayerLocationGet, payload);
    public ResponseEnvelope WorldMapPlayerRegionGet(Dictionary<string, object> payload) => Send(CommandNames.WorldMapPlayerRegionGet, payload);
    public ResponseEnvelope WorldPlayerMapsList0218(Dictionary<string, object> payload) => Send(CommandNames.WorldPlayerMapsList0218, payload);
    public ResponseEnvelope WorldPlayerMapGet0218(Dictionary<string, object> payload) => Send(CommandNames.WorldPlayerMapGet0218, payload);
    public ResponseEnvelope WorldPlayerMapChildren0218(Dictionary<string, object> payload) => Send(CommandNames.WorldPlayerMapChildren0218, payload);
    public ResponseEnvelope WorldPlayerMapPortalOpen0218(Dictionary<string, object> payload) => Send(CommandNames.WorldPlayerMapPortalOpen0218, payload);
    public ResponseEnvelope WorldPlayerMapDistancePreview0218(Dictionary<string, object> payload) => Send(CommandNames.WorldPlayerMapDistancePreview0218, payload);
    public ResponseEnvelope MapPlayerRoomList(Dictionary<string, object> payload) => Send(CommandNames.MapPlayerRoomList, payload);
    public ResponseEnvelope MapPlayerRoomGet(Dictionary<string, object> payload) => Send(CommandNames.MapPlayerRoomGet, payload);
    public ResponseEnvelope WorldCalendarPlayerGet(Dictionary<string, object> payload) => Send(CommandNames.WorldCalendarPlayerGet, payload);
    public ResponseEnvelope RealSchedulePlayerList(Dictionary<string, object> payload) => Send(CommandNames.RealSchedulePlayerList, payload);
    public ResponseEnvelope RealSchedulePlayerNext(Dictionary<string, object> payload) => Send(CommandNames.RealSchedulePlayerNext, payload);
    public ResponseEnvelope RealSchedulePlayerGet(Dictionary<string, object> payload) => Send(CommandNames.RealSchedulePlayerGet, payload);
    public ResponseEnvelope JournalEventPlayerList(Dictionary<string, object> payload) => Send(CommandNames.JournalEventPlayerList, payload);
    public ResponseEnvelope JournalEventPlayerGet(Dictionary<string, object> payload) => Send(CommandNames.JournalEventPlayerGet, payload);
    public ResponseEnvelope LegalPlayerSummary(Dictionary<string, object> payload) => Send(CommandNames.LegalPlayerSummary, payload);
    public ResponseEnvelope LegalPlayerLicenseList(Dictionary<string, object> payload) => Send(CommandNames.LegalPlayerLicenseList, payload);
    public ResponseEnvelope LegalPlayerApplicationList(Dictionary<string, object> payload) => Send(CommandNames.LegalPlayerApplicationList, payload);
    public ResponseEnvelope LegalPlayerApplicationSubmit(Dictionary<string, object> payload) => Send(CommandNames.LegalPlayerApplicationSubmit, payload);
    public ResponseEnvelope LegalPlayerCheckRequest(Dictionary<string, object> payload) => Send(CommandNames.LegalPlayerCheckRequest, payload);


    public ResponseEnvelope ChatSend(string sessionId, string type, string text) => Send(CommandNames.ChatSend, new Dictionary<string, object> { { "sessionId", sessionId }, { "type", type }, { "text", text } });
    public ResponseEnvelope ChatHistoryGet(string sessionId, int limit = 50, long beforeTicks = 0) => Send(CommandNames.ChatHistoryGet, new Dictionary<string, object> { { "sessionId", sessionId }, { "limit", limit }, { "beforeTicks", beforeTicks } });
    public ResponseEnvelope ChatHistoryLoadMore(string sessionId, int limit = 50, long beforeTicks = 0) => Send(CommandNames.ChatHistoryLoadMore, new Dictionary<string, object> { { "sessionId", sessionId }, { "limit", limit }, { "beforeTicks", beforeTicks } });
    public ResponseEnvelope ChatVisibleFeed(string sessionId, int limit = 50) => Send(CommandNames.ChatVisibleFeed, new Dictionary<string, object> { { "sessionId", sessionId }, { "limit", limit } });
    public ResponseEnvelope ChatMarkRead(string sessionId, string upToMessageId = "") => Send(CommandNames.ChatMarkRead, new Dictionary<string, object> { { "sessionId", sessionId }, { "upToMessageId", upToMessageId } });
    public ResponseEnvelope ChatUnreadGet(string sessionId) => Send(CommandNames.ChatUnreadGet, new Dictionary<string, object> { { "sessionId", sessionId } });


    public ResponseEnvelope AudioStateGet(string sessionId) => Send(CommandNames.AudioStateGet, new Dictionary<string, object> { { "sessionId", sessionId } });
    public ResponseEnvelope AudioStateSync(string sessionId) => Send(CommandNames.AudioStateSync, new Dictionary<string, object> { { "sessionId", sessionId } });
    public ResponseEnvelope AudioClientSettingsGet() => Send(CommandNames.AudioClientSettingsGet);
    public ResponseEnvelope AudioClientSettingsSet(double volume, bool muted) => Send(CommandNames.AudioClientSettingsSet, new Dictionary<string, object> { { "volume", volume }, { "muted", muted } });
    public ResponseEnvelope AudioPlayerStateGet(string sessionId) => Send(CommandNames.AudioPlayerStateGet, new Dictionary<string, object> { { "sessionId", sessionId } });
    public ResponseEnvelope AudioPlayerTracksVisible(string sessionId) => Send(CommandNames.AudioPlayerTracksVisible, new Dictionary<string, object> { { "sessionId", sessionId } });
    public ResponseEnvelope AudioPlayerClientSettingsGet(string sessionId) => Send(CommandNames.AudioPlayerClientSettingsGet, new Dictionary<string, object> { { "sessionId", sessionId } });
    public ResponseEnvelope AudioPlayerClientSettingsUpdate(string sessionId, double volume, bool muted) => Send(CommandNames.AudioPlayerClientSettingsUpdate, new Dictionary<string, object> { { "sessionId", sessionId }, { "localVolume", volume }, { "isMuted", muted } });


    public ResponseEnvelope VisibilityGet(string characterId) => Send(CommandNames.VisibilityGet, new Dictionary<string, object> { { "characterId", characterId } });
    public ResponseEnvelope VisibilityUpdate(Dictionary<string, object> payload) => Send(CommandNames.VisibilityUpdate, payload);
    public ResponseEnvelope CharacterPublicViewGet(string characterId) => Send(CommandNames.CharacterPublicViewGet, new Dictionary<string, object> { { "characterId", characterId } });

    public ResponseEnvelope NotesCreate(Dictionary<string, object> payload) => Send(CommandNames.NotesCreate, payload);
    public ResponseEnvelope NotesList(Dictionary<string, object> payload) => Send(CommandNames.NotesList, payload);
    public ResponseEnvelope NotesUpdate(Dictionary<string, object> payload) => Send(CommandNames.NotesUpdate, payload);
    public ResponseEnvelope NotesArchive(string noteId) => Send(CommandNames.NotesArchive, new Dictionary<string, object> { { "noteId", noteId } });
    public ResponseEnvelope SyncChangesGet(long afterRevision, string[] scopes, int limit = 100) => Send(CommandNames.SyncChangesGet, new Dictionary<string, object> { { "afterRevision", afterRevision }, { "scopes", scopes }, { "limit", limit } });
    public ResponseEnvelope QuestPlayerListActive(Dictionary<string, object> payload) => Send(CommandNames.QuestPlayerListActive, payload);
    public ResponseEnvelope QuestPlayerListAvailable(Dictionary<string, object> payload) => Send(CommandNames.QuestPlayerListAvailable, payload);
    public ResponseEnvelope QuestPlayerGet(string questId) => Send(CommandNames.QuestPlayerGet, new Dictionary<string, object> { { "questId", questId } });
    public ResponseEnvelope QuestPlayerGetJournal(Dictionary<string, object> payload) => Send(CommandNames.QuestPlayerGetJournal, payload);
    public ResponseEnvelope QuestPlayerGetRewardGrants(Dictionary<string, object> payload) => Send(CommandNames.QuestPlayerGetRewardGrants, payload);
    public ResponseEnvelope ShopPlayerListShops(Dictionary<string, object>? payload = null) => Send(CommandNames.ShopPlayerListShops, payload ?? new Dictionary<string, object>());
    public ResponseEnvelope ShopPlayerListOffers(string shopId) => Send(CommandNames.ShopPlayerListOffers, new Dictionary<string, object> { { "shopId", shopId } });
    public ResponseEnvelope ShopPlayerGetOffer(string offerId) => Send(CommandNames.ShopPlayerGetOffer, new Dictionary<string, object> { { "offerId", offerId } });
    public ResponseEnvelope ShopPlayerRequestPurchase(Dictionary<string, object> payload) => Send(CommandNames.ShopPlayerRequestPurchase, payload);
    public ResponseEnvelope ShopPlayerPurchaseHistory(Dictionary<string, object>? payload = null) => Send(CommandNames.ShopPlayerPurchaseHistory, payload ?? new Dictionary<string, object>());
    public ResponseEnvelope RestPlayerGetActiveForSession(Dictionary<string, object>? payload = null) => Send(CommandNames.RestPlayerGetActiveForSession, payload ?? new Dictionary<string, object>());
    public ResponseEnvelope RestPlayerGetMyRestStatus(Dictionary<string, object> payload) => Send(CommandNames.RestPlayerGetMyRestStatus, payload);
    public ResponseEnvelope RestPlayerListMyDowntimeActions(Dictionary<string, object>? payload = null) => Send(CommandNames.RestPlayerListMyDowntimeActions, payload ?? new Dictionary<string, object>());
    public ResponseEnvelope RestPlayerSubmitDowntimeAction(Dictionary<string, object> payload) => Send(CommandNames.RestPlayerSubmitDowntimeAction, payload);
    public ResponseEnvelope RestPlayerGetRecoveryGrants(Dictionary<string, object>? payload = null) => Send(CommandNames.RestPlayerGetRecoveryGrants, payload ?? new Dictionary<string, object>());
    public ResponseEnvelope GameplayPlayerGetMyGameplayStatus(Dictionary<string, object>? payload = null) => Send(CommandNames.GameplayPlayerGetMyGameplayStatus, payload ?? new Dictionary<string, object>());
    public ResponseEnvelope AssetBlueprintPlayerList(Dictionary<string, object>? payload = null) => Send(CommandNames.AssetBlueprintPlayerList, payload ?? new Dictionary<string, object>());
    public ResponseEnvelope AssetBlueprintPlayerGet(Dictionary<string, object> payload) => Send(CommandNames.AssetBlueprintPlayerGet, payload);
    public ResponseEnvelope AssetBlueprintPlayerCreate(Dictionary<string, object> payload) => Send(CommandNames.AssetBlueprintPlayerCreate, payload);
    public ResponseEnvelope AssetBlueprintPlayerUpdate(Dictionary<string, object> payload) => Send(CommandNames.AssetBlueprintPlayerUpdate, payload);
    public ResponseEnvelope AssetBlueprintPlayerDuplicate(Dictionary<string, object> payload) => Send(CommandNames.AssetBlueprintPlayerDuplicate, payload);
    public ResponseEnvelope AssetBlueprintPlayerArchive(Dictionary<string, object> payload) => Send(CommandNames.AssetBlueprintPlayerArchive, payload);
    public ResponseEnvelope CoreEquipmentPlayerList(Dictionary<string, object>? payload = null) => Send(CommandNames.CoreEquipmentPlayerList, payload ?? new Dictionary<string, object>());
    public ResponseEnvelope CoreEquipmentPlayerGet(Dictionary<string, object> payload) => Send(CommandNames.CoreEquipmentPlayerGet, payload);
    public ResponseEnvelope MagicDefinitionsPlayerList(Dictionary<string, object>? payload = null) => Send(CommandNames.MagicDefinitionsPlayerList, payload ?? new Dictionary<string, object>());
    public ResponseEnvelope MagicDefinitionsPlayerGet(Dictionary<string, object> payload) => Send(CommandNames.MagicDefinitionsPlayerGet, payload);
    public ResponseEnvelope WorldLoreCalendarPlayerList(Dictionary<string, object>? payload = null) => Send(CommandNames.WorldLoreCalendarPlayerList, payload ?? new Dictionary<string, object>());
    public ResponseEnvelope WorldLoreCalendarPlayerGet(Dictionary<string, object> payload) => Send(CommandNames.WorldLoreCalendarPlayerGet, payload);
    public ResponseEnvelope FactionOrganizationEconomyPlayerList(Dictionary<string, object>? payload = null) => Send(CommandNames.FactionOrganizationEconomyPlayerList, payload ?? new Dictionary<string, object>());
    public ResponseEnvelope FactionOrganizationEconomyPlayerGet(Dictionary<string, object> payload) => Send(CommandNames.FactionOrganizationEconomyPlayerGet, payload);
    public ResponseEnvelope TechnologyRecipeBlueprintProjectPlayerList(Dictionary<string, object>? payload = null) => Send(CommandNames.TechnologyRecipeBlueprintProjectPlayerList, payload ?? new Dictionary<string, object>());
    public ResponseEnvelope TechnologyRecipeBlueprintProjectPlayerGet(Dictionary<string, object> payload) => Send(CommandNames.TechnologyRecipeBlueprintProjectPlayerGet, payload);
    public ResponseEnvelope WorldPlayerWeatherGet(Dictionary<string, object> payload) => Send(CommandNames.WorldPlayerWeatherGet, payload);
    public ResponseEnvelope WorldPlayerEnvironmentGet(Dictionary<string, object> payload) => Send(CommandNames.WorldPlayerEnvironmentGet, payload);
    public ResponseEnvelope WorldPlayerForecastGet(Dictionary<string, object> payload) => Send(CommandNames.WorldPlayerForecastGet, payload);
    public ResponseEnvelope WorldPlayerTravelGet(Dictionary<string, object> payload) => Send(CommandNames.WorldPlayerTravelGet, payload);
    public ResponseEnvelope WorldPlayerTravelPreview(Dictionary<string, object> payload) => Send(CommandNames.WorldPlayerTravelPreview, payload);
    public ResponseEnvelope WorldPlayerObserveCurrent(Dictionary<string, object> payload) => Send(CommandNames.WorldPlayerObserveCurrent, payload);
    public ResponseEnvelope WorldPlayerMeasureEnvironment(Dictionary<string, object> payload) => Send(CommandNames.WorldPlayerMeasureEnvironment, payload);
    public ResponseEnvelope WorldPlayerEstimateDistance(Dictionary<string, object> payload) => Send(CommandNames.WorldPlayerEstimateDistance, payload);
    public ResponseEnvelope WorldPlayerObservationHistoryGet(Dictionary<string, object> payload) => Send(CommandNames.WorldPlayerObservationHistoryGet, payload);
    public ResponseEnvelope ActorPlayerEnvironmentAssessmentGet(Dictionary<string, object> payload) => Send(CommandNames.ActorPlayerEnvironmentAssessmentGet, payload);

    private ResponseEnvelope Send(string command, Dictionary<string, object>? payload = null)
    {
        var body = payload ?? new Dictionary<string, object>();
        var requestId = Guid.NewGuid().ToString("N");
        var startedUtc = DateTime.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        ClientLogService.Instance.Debug($"request.start command={command}; requestId={requestId}; startedUtc={startedUtc:O}; payloadKeys={body.Count}");
        try
        {
            var response = _client.Send(new RequestEnvelope { Command = command, RequestId = requestId, Payload = body });
            var responseRequestId = string.IsNullOrWhiteSpace(response.RequestId) ? requestId : response.RequestId;
            if (ConflictResponseParser.TryParseConflict(response, out var conflict))
            {
                ClientLogService.Instance.Warn($"conflict.received command={command} entityType={conflict.EntityType} entityId={conflict.EntityId} expected={conflict.ExpectedRevision} current={conflict.CurrentRevision} requestId={responseRequestId}");
            }
            ClientLogService.Instance.Debug($"request.end command={command}; requestId={responseRequestId}; status={response.Status}; success={(response.Status == ResponseStatus.Ok)}; elapsedMs={stopwatch.ElapsedMilliseconds}; message={response.Message}");
            return response;
        }
        catch (Exception ex)
        {
            ClientLogService.Instance.Error($"request.error command={command}; requestId={requestId}; elapsedMs={stopwatch.ElapsedMilliseconds}", ex);
            throw;
        }
    }
}
