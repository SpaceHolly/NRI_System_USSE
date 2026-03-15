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
