using System;
using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace Nri.Shared.Contracts;

public enum ResponseStatus
{
    Ok,
    Error,
    Unauthorized,
    Forbidden,
    ValidationFailed,
    NotFound
}

public enum ErrorCode
{
    None,
    Unknown,
    InvalidPayload,
    InvalidCommand,
    InvalidToken,
    AccessDenied,
    RateLimited,
    Conflict
}

public static class CommandNames
{
    public const string AuthRegister = "auth/register";
    public const string AuthLogin = "auth/login";
    public const string SessionSelect = "session/select";
    public const string CharacterGet = "character/get";
    public const string CharacterUpdate = "character/update";
    public const string RequestCreate = "request/create";
    public const string RequestCancel = "request/cancel";
    public const string RequestApprove = "request/approve";
    public const string RequestReject = "request/reject";
    public const string DiceRollRequest = "dice/roll/request";
    public const string CombatStart = "combat/start";
    public const string CombatNextTurn = "combat/nextTurn";
    public const string CombatEnd = "combat/end";
    public const string ChatSend = "chat/send";
    public const string ChatHistory = "chat/history";
    public const string AudioStateGet = "audio/state/get";
    public const string AudioStateSet = "audio/state/set";
}

public class RequestEnvelope
{
    public string Command { get; set; } = string.Empty;
    public string RequestId { get; set; } = Guid.NewGuid().ToString("N");
    public string? AuthToken { get; set; }
    public string? SessionId { get; set; }
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public int Version { get; set; } = 1;
    public Dictionary<string, object> Payload { get; set; } = new Dictionary<string, object>();
}

public class ResponseEnvelope
{
    public string RequestId { get; set; } = string.Empty;
    public ResponseStatus Status { get; set; } = ResponseStatus.Ok;
    public ErrorCode ErrorCode { get; set; } = ErrorCode.None;
    public string Message { get; set; } = string.Empty;
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public int Version { get; set; } = 1;
    public Dictionary<string, object> Payload { get; set; } = new Dictionary<string, object>();
}

public static class JsonProtocolSerializer
{
    private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();

    public static string Serialize<T>(T value)
    {
        return Serializer.Serialize(value);
    }

    public static T? Deserialize<T>(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return default;
        }

        return Serializer.Deserialize<T>(json);
    }
}
