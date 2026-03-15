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
    public const string AdminPlayersList = "admin.players.list";

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
    public const string DefinitionsSkillsGet = "definitions.skills.get";
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
