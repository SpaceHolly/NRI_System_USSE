using System.Collections.Generic;
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
    public ResponseEnvelope ValidateSession() => Send(CommandNames.SessionValidate);
    public ResponseEnvelope GetProfile() => Send(CommandNames.ProfileGet);
    public ResponseEnvelope UpdateProfile(string displayName, string race, int age, string description, string backstory) => Send(CommandNames.ProfileUpdate, new Dictionary<string, object> { { "displayName", displayName }, { "race", race }, { "age", age }, { "description", description }, { "backstory", backstory } });
    public ResponseEnvelope GetMyCharacters() => Send(CommandNames.CharacterListMine);
    public ResponseEnvelope GetActiveCharacter() => Send(CommandNames.CharacterGetActive);
    public ResponseEnvelope GetCharacterDetails(string characterId) => Send(CommandNames.CharacterGetDetails, new Dictionary<string, object> { { "characterId", characterId } });

    private ResponseEnvelope Send(string command, Dictionary<string, object>? payload = null)
    {
        return _client.Send(new RequestEnvelope { Command = command, Payload = payload ?? new Dictionary<string, object>() });
    public ResponseEnvelope GetProfile() => Send(CommandNames.ProfileGet);
    public ResponseEnvelope UpdateProfile(string displayName, string race, int? age, string description, string backstory) => Send(CommandNames.ProfileUpdate, new Dictionary<string, object> { { "displayName", displayName }, { "race", race }, { "age", age ?? 0 }, { "description", description }, { "backstory", backstory } });
    public ResponseEnvelope GetMyCharacters() => Send(CommandNames.CharacterListMine);

    private ResponseEnvelope Send(string command, Dictionary<string, object>? payload = null)
    {
        return _client.Send(new RequestEnvelope
        {
            Command = command,
            Payload = payload ?? new Dictionary<string, object>()
        });
    }
}
