using System;
using System.Collections.Generic;
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
    public ResponseEnvelope ValidateSession() => Send(CommandNames.SessionValidate);
    public ResponseEnvelope GetProfile() => Send(CommandNames.ProfileGet);
    public ResponseEnvelope UpdateProfile(string displayName, string race, int age, string description, string backstory) => Send(CommandNames.ProfileUpdate, new Dictionary<string, object> { { "displayName", displayName }, { "race", race }, { "age", age }, { "description", description }, { "backstory", backstory } });
    public ResponseEnvelope GetMyCharacters() => Send(CommandNames.CharacterListMine);
    public ResponseEnvelope GetActiveCharacter() => Send(CommandNames.CharacterGetActive);
    public ResponseEnvelope GetCharacterDetails(string characterId) => Send(CommandNames.CharacterGetDetails, new Dictionary<string, object> { { "characterId", characterId } });
    public ResponseEnvelope SetActiveCharacter(string characterId) => Send(CommandNames.CharacterSetActive, new Dictionary<string, object> { { "characterId", characterId } });
    public ResponseEnvelope CharacterInventoryGet(string characterId) => Send(CommandNames.CharacterInventoryGet, new Dictionary<string, object> { { "characterId", characterId } });
    public ResponseEnvelope CharacterCompanionsGet(string characterId) => Send(CommandNames.CharacterCompanionsGet, new Dictionary<string, object> { { "characterId", characterId } });
    public ResponseEnvelope CharacterHoldingsGet(string characterId) => Send(CommandNames.CharacterHoldingsGet, new Dictionary<string, object> { { "characterId", characterId } });
    public ResponseEnvelope CharacterReputationGet(string characterId) => Send(CommandNames.CharacterReputationGet, new Dictionary<string, object> { { "characterId", characterId } });
    public ResponseEnvelope CharacterSkillsGet(string characterId) => Send(CommandNames.CharacterSkillsGet, new Dictionary<string, object> { { "characterId", characterId } });

    public ResponseEnvelope CreateCharacter(Dictionary<string, object> payload) => Send(CommandNames.CharacterCreate, payload);
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
    public ResponseEnvelope CancelRequest(string requestId) => Send(CommandNames.RequestCancel, new Dictionary<string, object> { { "requestId", requestId } });
    public ResponseEnvelope ListMyRequests() => Send(CommandNames.RequestListMine);
    public ResponseEnvelope DiceHistory() => Send(CommandNames.DiceHistory);
    public ResponseEnvelope DiceVisibleFeed() => Send(CommandNames.DiceVisibleFeed);


    public ResponseEnvelope ClassTreeGet(string characterId) => Send(CommandNames.ClassTreeGet, new Dictionary<string, object> { { "characterId", characterId } });
    public ResponseEnvelope ClassTreeAvailable(string characterId) => Send(CommandNames.ClassTreeAvailableGet, new Dictionary<string, object> { { "characterId", characterId } });
    public ResponseEnvelope ClassTreeAcquireNode(string characterId, string nodeId) => Send(CommandNames.ClassTreeAcquireNode, new Dictionary<string, object> { { "characterId", characterId }, { "nodeId", nodeId } });
    public ResponseEnvelope ProgressionAvailableSkills(string characterId) => Send(CommandNames.ProgressionAvailableSkills, new Dictionary<string, object> { { "characterId", characterId } });
    public ResponseEnvelope SkillsList(string characterId) => Send(CommandNames.SkillsList, new Dictionary<string, object> { { "characterId", characterId } });
    public ResponseEnvelope SkillsAcquire(string characterId, string skillId) => Send(CommandNames.SkillsAcquire, new Dictionary<string, object> { { "characterId", characterId }, { "skillCode", skillId } });

    public ResponseEnvelope CombatVisibleState(string sessionId) => Send(CommandNames.CombatVisibleState, new Dictionary<string, object> { { "sessionId", sessionId } });
    public ResponseEnvelope CombatTimeline(string sessionId) => Send(CommandNames.CombatTimeline, new Dictionary<string, object> { { "sessionId", sessionId } });


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


    public ResponseEnvelope VisibilityGet(string characterId) => Send(CommandNames.VisibilityGet, new Dictionary<string, object> { { "characterId", characterId } });
    public ResponseEnvelope VisibilityUpdate(Dictionary<string, object> payload) => Send(CommandNames.VisibilityUpdate, payload);
    public ResponseEnvelope CharacterPublicViewGet(string characterId) => Send(CommandNames.CharacterPublicViewGet, new Dictionary<string, object> { { "characterId", characterId } });

    public ResponseEnvelope NotesCreate(Dictionary<string, object> payload) => Send(CommandNames.NotesCreate, payload);
    public ResponseEnvelope NotesList(Dictionary<string, object> payload) => Send(CommandNames.NotesList, payload);
    public ResponseEnvelope NotesUpdate(Dictionary<string, object> payload) => Send(CommandNames.NotesUpdate, payload);
    public ResponseEnvelope NotesArchive(string noteId) => Send(CommandNames.NotesArchive, new Dictionary<string, object> { { "noteId", noteId } });

    private ResponseEnvelope Send(string command, Dictionary<string, object>? payload = null)
    {
        var body = payload ?? new Dictionary<string, object>();
        ClientLogService.Instance.Debug($"Command send: {command}; payloadKeys={body.Count}");
        try
        {
            var response = _client.Send(new RequestEnvelope { Command = command, Payload = body });
            ClientLogService.Instance.Debug($"Command response: {command}; status={response.Status}; message={response.Message}");
            return response;
        }
        catch (Exception ex)
        {
            ClientLogService.Instance.Error($"Command failed: {command}", ex);
            throw;
        }
    }
}
