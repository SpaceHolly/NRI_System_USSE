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

    public const string ProfileGet = "profile.get";
    public const string ProfileUpdate = "profile.update";

    public const string AdminAccountsPending = "admin.accounts.pending";
    public const string AdminAccountsApprove = "admin.accounts.approve";
    public const string AdminAccountsArchive = "admin.accounts.archive";
    public const string AdminAccountProfile = "admin.accounts.profile";

    public const string CharacterListMine = "character.list.mine";
    public const string CharacterListByOwner = "character.list.byOwner";
    public const string CharacterGetActive = "character.get.active";
    public const string CharacterCreate = "character.create";
    public const string CharacterArchive = "character.archive";
    public const string CharacterRestore = "character.restore";
    public const string CharacterTransfer = "character.transfer";
    public const string CharacterAssignActive = "character.assignActive";

    public const string PresenceList = "presence.list";
    public const string SessionValidate = "session.validate";

    public const string LockAcquire = "lock.acquire";
    public const string LockRelease = "lock.release";
    public const string LockStatus = "lock.status";
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
