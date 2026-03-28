using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;

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
    public const string AdminPlayersList = "admin.players.list";
    public const string AdminAccountRolesSet = "admin.account.roles.set";
    public const string AdminAccountGrantAdmin = "admin.account.grantAdmin";
    public const string AdminAccountRevokeAdmin = "admin.account.revokeAdmin";

    public const string CharacterListMine = "character.list.mine";
    public const string CharacterListByOwner = "character.list.byOwner";
    public const string CharacterGetActive = "character.get.active";
    public const string CharacterGetDetails = "character.get.details";
    public const string CharacterGetSummary = "character.get.summary";
    public const string CharacterGetCompanions = "character.get.companions";
    public const string CharacterGetInventory = "character.get.inventory";
    public const string CharacterGetReputation = "character.get.reputation";
    public const string CharacterGetHoldings = "character.get.holdings";

    public const string CharacterUpdateBasicInfo = "character.update.basicInfo";
    public const string CharacterUpdateStats = "character.update.stats";
    public const string CharacterUpdateVisibility = "character.update.visibility";
    public const string CharacterUpdateMoney = "character.update.money";
    public const string CharacterUpdateInventory = "character.update.inventory";
    public const string CharacterUpdateReputation = "character.update.reputation";
    public const string CharacterUpdateHoldings = "character.update.holdings";

    public const string CharacterCreate = "character.create";
    public const string CharacterArchive = "character.archive";
    public const string CharacterRestore = "character.restore";
    public const string CharacterTransfer = "character.transfer";
    public const string CharacterAssignActive = "character.assignActive";

    public const string PresenceList = "presence.list";
    public const string SessionValidate = "session.validate";


    public const string RequestCreate = "request.create";
    public const string RequestCancel = "request.cancel";
    public const string RequestListMine = "request.list.mine";
    public const string RequestListPending = "request.list.pending";
    public const string RequestGetDetails = "request.get.details";
    public const string RequestApprove = "request.approve";
    public const string RequestReject = "request.reject";
    public const string RequestHistory = "request.history";

    public const string DiceRequest = "dice.request";
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


    public const string DefinitionsClassesGet = "definitions.classes.get";
    public const string DefinitionsClassGet = "definitions.class.get";
    public const string DefinitionsClassSave = "definitions.class.save";
    public const string DefinitionsClassArchive = "definitions.class.archive";
    public const string DefinitionsSkillsGet = "definitions.skills.get";
    public const string DefinitionsSkillGet = "definitions.skill.get";
    public const string DefinitionsSkillSave = "definitions.skill.save";
    public const string DefinitionsSkillArchive = "definitions.skill.archive";
    public const string DefinitionsReload = "definitions.reload";
    public const string DefinitionsVersionGet = "definitions.version.get";

    public const string ClassTreeGet = "classTree.get";
    public const string ClassTreeNodeGet = "classTree.node.get";
    public const string ClassTreeAvailableGet = "classTree.available.get";
    public const string ClassTreeAcquireNode = "classTree.acquireNode";
    public const string ClassTreeRecalculate = "classTree.recalculate";

    public const string SkillsList = "skills.list";
    public const string SkillsAvailable = "skills.available";
    public const string SkillsGet = "skills.get";
    public const string SkillsAcquire = "skills.acquire";

    public const string AdminClassTreeSetState = "admin.classTree.setState";
    public const string AdminSkillsSetState = "admin.skills.setState";
    public const string AdminCharacterProgressRecalculate = "admin.character.progress.recalculate";

    public const string AdminDefinitionsClassList = "admin.definitions.class.list";
    public const string AdminDefinitionsClassGet = "admin.definitions.class.get";
    public const string AdminDefinitionsClassSave = "admin.definitions.class.save";
    public const string AdminDefinitionsSkillList = "admin.definitions.skill.list";
    public const string AdminDefinitionsSkillGet = "admin.definitions.skill.get";
    public const string AdminDefinitionsSkillSave = "admin.definitions.skill.save";

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

    public const string UpdateVersionGet = "update.version.get";
    public const string UpdateManifestGet = "update.manifest.get";
    public const string UpdateClientDownloadInfo = "update.client.downloadInfo";

    public const string BackupCreate = "backup.create";
    public const string BackupList = "backup.list";
    public const string BackupRestore = "backup.restore";
    public const string BackupExport = "backup.export";

    public const string AdminLocksList = "admin.locks.list";
    public const string AdminLocksForceRelease = "admin.locks.forceRelease";
    public const string AdminServerStatus = "admin.server.status";
    public const string AdminSessionsList = "admin.sessions.list";
    public const string AdminDiagnosticsGet = "admin.diagnostics.get";

    public const string LockAcquire = "lock.acquire";
    public const string LockRelease = "lock.release";
    public const string LockForceRelease = "lock.forceRelease";
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
            if (value is T typed) return typed;
            return default;
        }
    }

    private static object? NormalizeEnvelopePayload<T>(T value)
    {
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
                Payload = NormalizeDictionary(request.Payload)
            };
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
}
