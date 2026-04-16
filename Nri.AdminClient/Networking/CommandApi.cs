using System;
using System.Collections.Generic;
using Nri.AdminClient.Diagnostics;
using Nri.Shared.Contracts;

namespace Nri.AdminClient.Networking;

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
    public ResponseEnvelope ResetPassword(string accountId, string newPassword) => Send(CommandNames.AdminAccountResetPassword, new Dictionary<string, object> { { "accountId", accountId }, { "newPassword", newPassword } });

    public ResponseEnvelope GetPendingAccounts() => Send(CommandNames.AdminAccountsPending);
    public ResponseEnvelope ApproveAccount(string accountId) => Send(CommandNames.AdminAccountsApprove, new Dictionary<string, object> { { "accountId", accountId } });
    public ResponseEnvelope RejectAccount(string accountId) => Send(CommandNames.AdminAccountReject, new Dictionary<string, object> { { "accountId", accountId } });
    public ResponseEnvelope BlockAccount(string accountId) => Send(CommandNames.AdminAccountBlock, new Dictionary<string, object> { { "accountId", accountId } });
    public ResponseEnvelope UnblockAccount(string accountId) => Send(CommandNames.AdminAccountUnblock, new Dictionary<string, object> { { "accountId", accountId } });
    public ResponseEnvelope ArchiveAccount(string accountId) => Send(CommandNames.AdminAccountsArchive, new Dictionary<string, object> { { "accountId", accountId } });
    public ResponseEnvelope GetPlayers() => Send(CommandNames.AdminPlayersList);

    public ResponseEnvelope GetCharactersByOwner(string ownerUserId) => Send(CommandNames.CharacterListByOwner, new Dictionary<string, object> { { "ownerUserId", ownerUserId } });
    public ResponseEnvelope GetCharacterDetails(string characterId) => Send(CommandNames.CharacterGetDetails, new Dictionary<string, object> { { "characterId", characterId } });
    public ResponseEnvelope UpdateCharacterBasicInfo(Dictionary<string, object> payload) => Send(CommandNames.CharacterUpdateBasicInfo, payload);
    public ResponseEnvelope UpdateCharacterStats(Dictionary<string, object> payload) => Send(CommandNames.CharacterUpdateStats, payload);
    public ResponseEnvelope UpdateCharacterVisibility(Dictionary<string, object> payload) => Send(CommandNames.CharacterUpdateVisibility, payload);
    public ResponseEnvelope UpdateCharacterMoney(Dictionary<string, object> payload) => Send(CommandNames.CharacterUpdateMoney, payload);
    public ResponseEnvelope UpdateCharacterInventory(Dictionary<string, object> payload) => Send(CommandNames.CharacterUpdateInventory, payload);
    public ResponseEnvelope UpdateCharacterReputation(Dictionary<string, object> payload) => Send(CommandNames.CharacterUpdateReputation, payload);
    public ResponseEnvelope UpdateCharacterHoldings(Dictionary<string, object> payload) => Send(CommandNames.CharacterUpdateHoldings, payload);
    public ResponseEnvelope AssignActive(string userId, string characterId) => Send(CommandNames.CharacterAssignActive, new Dictionary<string, object> { { "userId", userId }, { "characterId", characterId } });
    public ResponseEnvelope TransferCharacter(string characterId, string targetUserId) => Send(CommandNames.CharacterTransfer, new Dictionary<string, object> { { "characterId", characterId }, { "targetUserId", targetUserId } });
    public ResponseEnvelope ArchiveCharacter(string characterId) => Send(CommandNames.CharacterArchive, new Dictionary<string, object> { { "characterId", characterId } });
    public ResponseEnvelope RestoreCharacter(string characterId) => Send(CommandNames.CharacterRestore, new Dictionary<string, object> { { "characterId", characterId } });
    public ResponseEnvelope CreateCharacter(Dictionary<string, object> payload) => Send(CommandNames.CharacterAdminCreate, payload);
    public ResponseEnvelope AssignOwner(string characterId, string ownerUserId) => Send(CommandNames.CharacterAssignOwner, new Dictionary<string, object> { { "characterId", characterId }, { "ownerUserId", ownerUserId } });

    public ResponseEnvelope AcquireCharacterLock(string characterId) => Send(CommandNames.LockAcquire, new Dictionary<string, object> { { "entityType", "character" }, { "entityId", characterId } });
    public ResponseEnvelope ReleaseCharacterLock(string characterId) => Send(CommandNames.LockRelease, new Dictionary<string, object> { { "entityType", "character" }, { "entityId", characterId } });
    public ResponseEnvelope ForceReleaseCharacterLock(string characterId) => Send(CommandNames.LockForceRelease, new Dictionary<string, object> { { "entityType", "character" }, { "entityId", characterId } });
    public ResponseEnvelope LockStatus(string characterId) => Send(CommandNames.LockStatus, new Dictionary<string, object> { { "entityType", "character" }, { "entityId", characterId } });


    public ResponseEnvelope ListPendingRequests() => Send(CommandNames.RequestListPending);
    public ResponseEnvelope ApproveRequest(string requestId, string comment) => Send(CommandNames.RequestApprove, new Dictionary<string, object> { { "requestId", requestId }, { "comment", comment } });
    public ResponseEnvelope RejectRequest(string requestId, string comment) => Send(CommandNames.RequestReject, new Dictionary<string, object> { { "requestId", requestId }, { "comment", comment } });
    public ResponseEnvelope RequestHistory() => Send(CommandNames.RequestHistory);
    public ResponseEnvelope DiceHistory() => Send(CommandNames.DiceHistory);
    public ResponseEnvelope DiceVisibleFeed() => Send(CommandNames.DiceVisibleFeed);
    public ResponseEnvelope DiceRollStandard(string characterId, string formula, string visibility, string description) => Send(CommandNames.DiceRollStandard, new Dictionary<string, object> { { "characterId", characterId }, { "formula", formula }, { "visibility", visibility }, { "description", description } });
    public ResponseEnvelope DiceRollTest(string characterId, string formula, string visibility, string description) => Send(CommandNames.DiceRollTest, new Dictionary<string, object> { { "characterId", characterId }, { "formula", formula }, { "visibility", visibility }, { "description", description } });
    public ResponseEnvelope DiceTestGetCurrent(string userId = "") => Send(CommandNames.DiceTestGetCurrent, new Dictionary<string, object> { { "userId", userId } });


    public ResponseEnvelope CombatStart(string sessionId, Dictionary<string, object>[] participants) => Send(CommandNames.CombatStart, new Dictionary<string, object> { { "sessionId", sessionId }, { "participants", participants } });
    public ResponseEnvelope CombatEnd(string sessionId) => Send(CommandNames.CombatEnd, new Dictionary<string, object> { { "sessionId", sessionId } });
    public ResponseEnvelope CombatGetState(string sessionId) => Send(CommandNames.CombatGetState, new Dictionary<string, object> { { "sessionId", sessionId } });
    public ResponseEnvelope CombatGetHistory(string sessionId) => Send(CommandNames.CombatGetHistory, new Dictionary<string, object> { { "sessionId", sessionId } });
    public ResponseEnvelope CombatNextTurn(string sessionId) => Send(CommandNames.CombatNextTurn, new Dictionary<string, object> { { "sessionId", sessionId } });
    public ResponseEnvelope CombatPreviousTurn(string sessionId) => Send(CommandNames.CombatPreviousTurn, new Dictionary<string, object> { { "sessionId", sessionId } });
    public ResponseEnvelope CombatNextRound(string sessionId) => Send(CommandNames.CombatNextRound, new Dictionary<string, object> { { "sessionId", sessionId } });
    public ResponseEnvelope CombatSkipTurn(string sessionId) => Send(CommandNames.CombatSkipTurn, new Dictionary<string, object> { { "sessionId", sessionId } });
    public ResponseEnvelope CombatSelectActive(string sessionId, string slotId) => Send(CommandNames.CombatSelectActive, new Dictionary<string, object> { { "sessionId", sessionId }, { "slotId", slotId } });
    public ResponseEnvelope CombatAddParticipant(string sessionId, Dictionary<string, object>[] participants) => Send(CommandNames.CombatAddParticipant, new Dictionary<string, object> { { "sessionId", sessionId }, { "participants", participants } });
    public ResponseEnvelope CombatRemoveParticipant(string sessionId, string participantId) => Send(CommandNames.CombatRemoveParticipant, new Dictionary<string, object> { { "sessionId", sessionId }, { "participantId", participantId } });
    public ResponseEnvelope CombatDetachCompanion(string sessionId, string participantId) => Send(CommandNames.CombatDetachCompanion, new Dictionary<string, object> { { "sessionId", sessionId }, { "participantId", participantId } });


    public ResponseEnvelope DefinitionsClassesGet(bool includeArchived = false) => Send(CommandNames.DefinitionsClassesGet, new Dictionary<string, object> { { "includeArchived", includeArchived } });
    public ResponseEnvelope DefinitionClassGet(string code) => Send(CommandNames.DefinitionsClassGet, new Dictionary<string, object> { { "code", code } });
    public ResponseEnvelope DefinitionClassSave(Dictionary<string, object> definition) => Send(CommandNames.DefinitionsClassSave, new Dictionary<string, object> { { "definition", definition } });
    public ResponseEnvelope DefinitionClassArchive(string code) => Send(CommandNames.DefinitionsClassArchive, new Dictionary<string, object> { { "code", code } });
    public ResponseEnvelope DefinitionsSkillsGet(bool includeArchived = false) => Send(CommandNames.DefinitionsSkillsGet, new Dictionary<string, object> { { "includeArchived", includeArchived } });
    public ResponseEnvelope DefinitionSkillGet(string code) => Send(CommandNames.DefinitionsSkillGet, new Dictionary<string, object> { { "code", code } });
    public ResponseEnvelope DefinitionSkillSave(Dictionary<string, object> definition) => Send(CommandNames.DefinitionsSkillSave, new Dictionary<string, object> { { "definition", definition } });
    public ResponseEnvelope DefinitionSkillArchive(string code) => Send(CommandNames.DefinitionsSkillArchive, new Dictionary<string, object> { { "code", code } });
    public ResponseEnvelope DefinitionsReload() => Send(CommandNames.DefinitionsReload);
    public ResponseEnvelope DefinitionsVersionGet() => Send(CommandNames.DefinitionsVersionGet);

    public ResponseEnvelope ClassTreeGet(string characterId) => Send(CommandNames.ClassTreeGet, new Dictionary<string, object> { { "characterId", characterId } });
    public ResponseEnvelope ClassTreeAvailable(string characterId) => Send(CommandNames.ClassTreeAvailableGet, new Dictionary<string, object> { { "characterId", characterId } });
    public ResponseEnvelope ClassTreeAcquireNode(string characterId, string nodeId) => Send(CommandNames.ClassTreeAcquireNode, new Dictionary<string, object> { { "characterId", characterId }, { "nodeId", nodeId } });
    public ResponseEnvelope ClassTreeRecalculate(string characterId) => Send(CommandNames.ClassTreeRecalculate, new Dictionary<string, object> { { "characterId", characterId } });

    public ResponseEnvelope SkillsList(string characterId) => Send(CommandNames.SkillsList, new Dictionary<string, object> { { "characterId", characterId } });
    public ResponseEnvelope SkillsAcquire(string characterId, string skillId) => Send(CommandNames.SkillsAcquire, new Dictionary<string, object> { { "characterId", characterId }, { "skillId", skillId } });

    public ResponseEnvelope AdminClassTreeSetState(string characterId, string directionId, string branchId, string nodeId) => Send(CommandNames.AdminClassTreeSetState, new Dictionary<string, object> { { "characterId", characterId }, { "directionId", directionId }, { "branchId", branchId }, { "nodeId", nodeId } });
    public ResponseEnvelope AdminSkillsSetState(string characterId, string skillId, bool acquired) => Send(CommandNames.AdminSkillsSetState, new Dictionary<string, object> { { "characterId", characterId }, { "skillId", skillId }, { "acquired", acquired } });
    public ResponseEnvelope AdminCharacterProgressRecalculate(string characterId) => Send(CommandNames.AdminCharacterProgressRecalculate, new Dictionary<string, object> { { "characterId", characterId } });

    public ResponseEnvelope PresenceList() => Send(CommandNames.PresenceList);


    public ResponseEnvelope ChatSend(string sessionId, string type, string text) => Send(CommandNames.ChatSend, new Dictionary<string, object> { { "sessionId", sessionId }, { "type", type }, { "text", text } });
    public ResponseEnvelope ChatHistoryGet(string sessionId, int limit = 50, long beforeTicks = 0) => Send(CommandNames.ChatHistoryGet, new Dictionary<string, object> { { "sessionId", sessionId }, { "limit", limit }, { "beforeTicks", beforeTicks } });
    public ResponseEnvelope ChatHistoryLoadMore(string sessionId, int limit = 50, long beforeTicks = 0) => Send(CommandNames.ChatHistoryLoadMore, new Dictionary<string, object> { { "sessionId", sessionId }, { "limit", limit }, { "beforeTicks", beforeTicks } });
    public ResponseEnvelope ChatVisibleFeed(string sessionId, int limit = 50) => Send(CommandNames.ChatVisibleFeed, new Dictionary<string, object> { { "sessionId", sessionId }, { "limit", limit } });
    public ResponseEnvelope ChatMarkRead(string sessionId, string upToMessageId = "") => Send(CommandNames.ChatMarkRead, new Dictionary<string, object> { { "sessionId", sessionId }, { "upToMessageId", upToMessageId } });
    public ResponseEnvelope ChatUnreadGet(string sessionId) => Send(CommandNames.ChatUnreadGet, new Dictionary<string, object> { { "sessionId", sessionId } });

    public ResponseEnvelope ChatSlowModeGet(string sessionId) => Send(CommandNames.ChatSlowModeGet, new Dictionary<string, object> { { "sessionId", sessionId } });
    public ResponseEnvelope ChatSlowModeSet(string sessionId, int pub, int hidden, int admin) => Send(CommandNames.ChatSlowModeSet, new Dictionary<string, object> { { "sessionId", sessionId }, { "publicSeconds", pub }, { "hiddenToAdminsSeconds", hidden }, { "adminOnlySeconds", admin } });
    public ResponseEnvelope ChatRestrictionsGet(string sessionId) => Send(CommandNames.ChatRestrictionsGet, new Dictionary<string, object> { { "sessionId", sessionId } });
    public ResponseEnvelope ChatRestrictionsMuteUser(string sessionId, string userId, string reason) => Send(CommandNames.ChatRestrictionsMuteUser, new Dictionary<string, object> { { "sessionId", sessionId }, { "userId", userId }, { "reason", reason } });
    public ResponseEnvelope ChatRestrictionsUnmuteUser(string sessionId, string userId) => Send(CommandNames.ChatRestrictionsUnmuteUser, new Dictionary<string, object> { { "sessionId", sessionId }, { "userId", userId } });
    public ResponseEnvelope ChatRestrictionsLockPlayers(string sessionId) => Send(CommandNames.ChatRestrictionsLockPlayers, new Dictionary<string, object> { { "sessionId", sessionId } });
    public ResponseEnvelope ChatRestrictionsUnlockPlayers(string sessionId) => Send(CommandNames.ChatRestrictionsUnlockPlayers, new Dictionary<string, object> { { "sessionId", sessionId } });


    public ResponseEnvelope AudioStateGet(string sessionId) => Send(CommandNames.AudioStateGet, new Dictionary<string, object> { { "sessionId", sessionId } });
    public ResponseEnvelope AudioStateSync(string sessionId) => Send(CommandNames.AudioStateSync, new Dictionary<string, object> { { "sessionId", sessionId } });
    public ResponseEnvelope AudioModeGet(string sessionId) => Send(CommandNames.AudioModeGet, new Dictionary<string, object> { { "sessionId", sessionId } });
    public ResponseEnvelope AudioModeSet(string sessionId, string mode, string category) => Send(CommandNames.AudioModeSet, new Dictionary<string, object> { { "sessionId", sessionId }, { "mode", mode }, { "category", category } });
    public ResponseEnvelope AudioOverrideClear(string sessionId) => Send(CommandNames.AudioOverrideClear, new Dictionary<string, object> { { "sessionId", sessionId } });
    public ResponseEnvelope AudioLibraryGet() => Send(CommandNames.AudioLibraryGet);
    public ResponseEnvelope AudioTrackSelect(string sessionId, string trackId) => Send(CommandNames.AudioTrackSelect, new Dictionary<string, object> { { "sessionId", sessionId }, { "trackId", trackId } });
    public ResponseEnvelope AudioTrackNext(string sessionId) => Send(CommandNames.AudioTrackNext, new Dictionary<string, object> { { "sessionId", sessionId } });
    public ResponseEnvelope AudioTrackReload() => Send(CommandNames.AudioTrackReload);


    public ResponseEnvelope VisibilityGet(string characterId) => Send(CommandNames.VisibilityGet, new Dictionary<string, object> { { "characterId", characterId } });
    public ResponseEnvelope VisibilityUpdate(Dictionary<string, object> payload) => Send(CommandNames.VisibilityUpdate, payload);
    public ResponseEnvelope CharacterPublicViewGet(string characterId) => Send(CommandNames.CharacterPublicViewGet, new Dictionary<string, object> { { "characterId", characterId } });

    public ResponseEnvelope NotesCreate(Dictionary<string, object> payload) => Send(CommandNames.NotesCreate, payload);
    public ResponseEnvelope NotesList(Dictionary<string, object> payload) => Send(CommandNames.NotesList, payload);
    public ResponseEnvelope NotesUpdate(Dictionary<string, object> payload) => Send(CommandNames.NotesUpdate, payload);
    public ResponseEnvelope NotesArchive(string noteId) => Send(CommandNames.NotesArchive, new Dictionary<string, object> { { "noteId", noteId } });

    public ResponseEnvelope ReferenceList(string worldId, string type) => Send(CommandNames.ReferenceList, new Dictionary<string, object> { { "worldId", worldId }, { "referenceType", type } });
    public ResponseEnvelope ReferenceCreate(Dictionary<string, object> payload) => Send(CommandNames.ReferenceCreate, payload);
    public ResponseEnvelope ReferenceUpdate(Dictionary<string, object> payload) => Send(CommandNames.ReferenceUpdate, payload);
    public ResponseEnvelope ReferenceArchive(string referenceId) => Send(CommandNames.ReferenceArchive, new Dictionary<string, object> { { "referenceId", referenceId } });
    public ResponseEnvelope ReferenceReload() => Send(CommandNames.ReferenceReload);

    public ResponseEnvelope UpdateVersionGet(string channel) => Send(CommandNames.UpdateVersionGet, new Dictionary<string, object> { { "channel", channel } });
    public ResponseEnvelope UpdateManifestGet(string channel) => Send(CommandNames.UpdateManifestGet, new Dictionary<string, object> { { "channel", channel } });
    public ResponseEnvelope BackupCreate(string label) => Send(CommandNames.BackupCreate, new Dictionary<string, object> { { "label", label } });
    public ResponseEnvelope BackupList() => Send(CommandNames.BackupList);
    public ResponseEnvelope BackupRestore(string backupId) => Send(CommandNames.BackupRestore, new Dictionary<string, object> { { "backupId", backupId } });
    public ResponseEnvelope BackupExport(string backupId) => Send(CommandNames.BackupExport, new Dictionary<string, object> { { "backupId", backupId } });

    public ResponseEnvelope AdminLocksList() => Send(CommandNames.AdminLocksList);
    public ResponseEnvelope AdminServerStatus() => Send(CommandNames.AdminServerStatus);
    public ResponseEnvelope AdminSessionsList() => Send(CommandNames.AdminSessionsList);
    public ResponseEnvelope AdminDiagnosticsGet() => Send(CommandNames.AdminDiagnosticsGet);

    private ResponseEnvelope Send(string command, Dictionary<string, object>? payload = null)
    {
        var body = payload ?? new Dictionary<string, object>();
        ClientLogService.Instance.Info($"Command send: {command}; payloadKeys={body.Count}");
        try
        {
            var response = _client.Send(new RequestEnvelope { Command = command, Payload = body });
            ClientLogService.Instance.Info($"Command response: {command}; status={response.Status}; message={response.Message}");
            return response;
        }
        catch (Exception ex)
        {
            ClientLogService.Instance.Error($"Command failed: {command}", ex);
            throw;
        }
    }
}
