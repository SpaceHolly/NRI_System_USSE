using System.Collections.Generic;
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

    public ResponseEnvelope GetPendingAccounts() => Send(CommandNames.AdminAccountsPending);
    public ResponseEnvelope ApproveAccount(string accountId) => Send(CommandNames.AdminAccountsApprove, new Dictionary<string, object> { { "accountId", accountId } });
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

    public ResponseEnvelope AcquireCharacterLock(string characterId) => Send(CommandNames.LockAcquire, new Dictionary<string, object> { { "entityType", "character" }, { "entityId", characterId } });
    public ResponseEnvelope ReleaseCharacterLock(string characterId) => Send(CommandNames.LockRelease, new Dictionary<string, object> { { "entityType", "character" }, { "entityId", characterId } });
    public ResponseEnvelope ForceReleaseCharacterLock(string characterId) => Send(CommandNames.LockForceRelease, new Dictionary<string, object> { { "entityType", "character" }, { "entityId", characterId } });
    public ResponseEnvelope LockStatus(string characterId) => Send(CommandNames.LockStatus, new Dictionary<string, object> { { "entityType", "character" }, { "entityId", characterId } });

    public ResponseEnvelope PresenceList() => Send(CommandNames.PresenceList);

    private ResponseEnvelope Send(string command, Dictionary<string, object>? payload = null)
    {
        return _client.Send(new RequestEnvelope { Command = command, Payload = payload ?? new Dictionary<string, object>() });
    public ResponseEnvelope GetProfile() => Send(CommandNames.ProfileGet);
    public ResponseEnvelope UpdateProfile(string displayName, string race, int? age, string description, string backstory) => Send(CommandNames.ProfileUpdate, new Dictionary<string, object> { { "displayName", displayName }, { "race", race }, { "age", age ?? 0 }, { "description", description }, { "backstory", backstory } });
    public ResponseEnvelope GetMyCharacters() => Send(CommandNames.CharacterListMine);
    public ResponseEnvelope GetPendingAccounts() => Send(CommandNames.AdminAccountsPending);
    public ResponseEnvelope ApproveAccount(string accountId) => Send(CommandNames.AdminAccountsApprove, new Dictionary<string, object> { { "accountId", accountId } });

    private ResponseEnvelope Send(string command, Dictionary<string, object>? payload = null)
    {
        return _client.Send(new RequestEnvelope
        {
            Command = command,
            Payload = payload ?? new Dictionary<string, object>()
        });
    }
}
