using System;
using System.Collections.Generic;
using Nri.ChatClient.Diagnostics;
using Nri.Shared.Contracts;

namespace Nri.ChatClient.Networking;

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
    public ResponseEnvelope ChatSend(string sessionId, string type, string text) => Send(CommandNames.ChatSend, new Dictionary<string, object> { { "sessionId", sessionId }, { "type", type }, { "text", text } });
    public ResponseEnvelope ChatVisibleFeed(string sessionId, int limit = 80) => Send(CommandNames.ChatVisibleFeed, new Dictionary<string, object> { { "sessionId", sessionId }, { "limit", limit } });
    public ResponseEnvelope DiceVisibleFeed() => Send(CommandNames.DiceVisibleFeed);
    public ResponseEnvelope DiceRollStandard(string formula, string visibility, string description)
        => Send(CommandNames.DiceRollStandard, new Dictionary<string, object> { { "formula", formula }, { "visibility", visibility }, { "description", description } });
    public ResponseEnvelope DiceRollTest(string formula, string visibility, string description)
        => Send(CommandNames.DiceRollTest, new Dictionary<string, object> { { "formula", formula }, { "visibility", visibility }, { "description", description } });

    private ResponseEnvelope Send(string command, Dictionary<string, object>? payload = null)
    {
        var body = payload ?? new Dictionary<string, object>();
        ClientLogService.Instance.Debug($"api.send command={command} payloadKeys={body.Count}");
        try
        {
            var response = _client.Send(new RequestEnvelope { Command = command, Payload = body });
            ClientLogService.Instance.Debug($"api.response command={command} status={response.Status}");
            return response;
        }
        catch (Exception ex)
        {
            ClientLogService.Instance.Error($"api.error command={command}", ex);
            throw;
        }
    }
}
