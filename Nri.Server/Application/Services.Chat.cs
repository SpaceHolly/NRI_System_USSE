using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Driver;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;
using Nri.Shared.Utilities;

namespace Nri.Server.Application;

public partial class ServiceHub
{
    private const int MaxChatLength = 1500;

    public ResponseEnvelope ChatSend(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var sessionId = RequireLength(PayloadReader.GetString(context.Request.Payload, "sessionId"), 1, 128, "sessionId");
        var text = (PayloadReader.GetString(context.Request.Payload, "text") ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("text is required");
        if (text.Length > MaxChatLength) throw new ArgumentException($"text max length is {MaxChatLength}");

        var type = ParseChatType(RequireLength(PayloadReader.GetString(context.Request.Payload, "type"), 3, 64, "type"));
        EnsureCanSendChatType(actor, type);

        var settings = GetOrCreateChatSettings(sessionId);
        EnsureChatRestrictions(actor, settings);
        EnsureSlowMode(actor, sessionId, type, settings);

        var profile = _repositories.Profiles.GetById(actor.ProfileId);
        var msg = new ChatMessage
        {
            SessionId = sessionId,
            SenderUserId = actor.Id,
            SenderDisplayName = profile != null && !string.IsNullOrWhiteSpace(profile.DisplayName) ? profile.DisplayName : actor.Login,
            Text = text,
            MessageType = type,
            VisibilityChannel = type.ToString(),
            ModerationState = ChatModerationState.Active
        };

        _repositories.ChatMessages.Insert(msg);
        TouchThrottle(sessionId, actor.Id, type);
        _logger.Session($"chat.send session={sessionId} actor={actor.Id} type={type}");
        WriteAudit("chat", actor.Id, "send", msg.Id);

        return Ok("Chat message sent.", new Dictionary<string, object> { { "item", ChatPayload(msg) } });
    }

    public ResponseEnvelope ChatHistoryGet(CommandContext context) => ChatHistoryCore(context, false);
    public ResponseEnvelope ChatHistoryLoadMore(CommandContext context) => ChatHistoryCore(context, true);
    public ResponseEnvelope ChatVisibleFeed(CommandContext context) => ChatHistoryCore(context, false);

    public ResponseEnvelope ChatMarkRead(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var sessionId = RequireLength(PayloadReader.GetString(context.Request.Payload, "sessionId"), 1, 128, "sessionId");
        var upToId = PayloadReader.GetString(context.Request.Payload, "upToMessageId") ?? string.Empty;
        var visible = _repositories.ChatMessages.Find(Builders<ChatMessage>.Filter.Eq(x => x.SessionId, sessionId))
            .Where(x => CanViewChatMessage(actor, x))
            .OrderBy(x => x.CreatedUtc)
            .ToList();

        if (visible.Count == 0) return Ok("Marked as read.");
        var target = string.IsNullOrWhiteSpace(upToId) ? visible.Last() : visible.FirstOrDefault(x => x.Id == upToId) ?? visible.Last();
        UpdateReadState(sessionId, actor.Id, target);
        return Ok("Marked as read.", new Dictionary<string, object> { { "lastReadMessageId", target.Id } });
    }

    public ResponseEnvelope ChatUnreadGet(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var sessionId = RequireLength(PayloadReader.GetString(context.Request.Payload, "sessionId"), 1, 128, "sessionId");
        var read = GetReadState(sessionId, actor.Id);
        var visible = _repositories.ChatMessages.Find(Builders<ChatMessage>.Filter.Eq(x => x.SessionId, sessionId)).Where(x => CanViewChatMessage(actor, x));
        var unread = read?.LastReadMessageUtc == null ? visible.Count() : visible.Count(x => x.CreatedUtc > read.LastReadMessageUtc.Value);
        return Ok("Unread loaded.", new Dictionary<string, object> { { "count", unread } });
    }

    public ResponseEnvelope ChatSlowModeGet(CommandContext context)
    {
        GetCurrentAccount(context);
        var sessionId = RequireLength(PayloadReader.GetString(context.Request.Payload, "sessionId"), 1, 128, "sessionId");
        var s = GetOrCreateChatSettings(sessionId);
        return Ok("Slow mode loaded.", SlowModePayload(s));
    }

    public ResponseEnvelope ChatSlowModeSet(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var sessionId = RequireLength(PayloadReader.GetString(context.Request.Payload, "sessionId"), 1, 128, "sessionId");
        var s = GetOrCreateChatSettings(sessionId);
        s.SlowMode.PublicSeconds = Math.Max(0, PayloadReader.GetInt(context.Request.Payload, "publicSeconds") ?? s.SlowMode.PublicSeconds);
        s.SlowMode.HiddenToAdminsSeconds = Math.Max(0, PayloadReader.GetInt(context.Request.Payload, "hiddenToAdminsSeconds") ?? s.SlowMode.HiddenToAdminsSeconds);
        s.SlowMode.AdminOnlySeconds = Math.Max(0, PayloadReader.GetInt(context.Request.Payload, "adminOnlySeconds") ?? s.SlowMode.AdminOnlySeconds);
        _repositories.SessionChatSettings.Replace(s);
        WriteAudit("chat", actor.Id, "slowMode.set", sessionId);
        _logger.Admin($"chat.slowmode actor={actor.Id} session={sessionId}");
        return Ok("Slow mode updated.", SlowModePayload(s));
    }

    public ResponseEnvelope ChatRestrictionsGet(CommandContext context)
    {
        RequireAdmin(context);
        var sessionId = RequireLength(PayloadReader.GetString(context.Request.Payload, "sessionId"), 1, 128, "sessionId");
        var s = GetOrCreateChatSettings(sessionId);
        return Ok("Chat restrictions loaded.", RestrictionsPayload(s));
    }

    public ResponseEnvelope ChatRestrictionsMuteUser(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var sessionId = RequireLength(PayloadReader.GetString(context.Request.Payload, "sessionId"), 1, 128, "sessionId");
        var userId = RequireLength(PayloadReader.GetString(context.Request.Payload, "userId"), 8, 128, "userId");
        var reason = RequireLength(PayloadReader.GetString(context.Request.Payload, "reason"), 0, 512, "reason");
        var s = GetOrCreateChatSettings(sessionId);
        var entry = s.Restrictions.FirstOrDefault(x => x.UserId == userId);
        if (entry == null)
        {
            entry = new ChatRestrictionEntry { UserId = userId };
            s.Restrictions.Add(entry);
        }
        entry.Muted = true;
        entry.Reason = reason;
        entry.ChangedByUserId = actor.Id;
        entry.ChangedAtUtc = DateTime.UtcNow;
        _repositories.SessionChatSettings.Replace(s);
        WriteAudit("chat", actor.Id, "muteUser", sessionId + ":" + userId);
        return Ok("User muted.", RestrictionsPayload(s));
    }

    public ResponseEnvelope ChatRestrictionsUnmuteUser(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var sessionId = RequireLength(PayloadReader.GetString(context.Request.Payload, "sessionId"), 1, 128, "sessionId");
        var userId = RequireLength(PayloadReader.GetString(context.Request.Payload, "userId"), 8, 128, "userId");
        var s = GetOrCreateChatSettings(sessionId);
        var entry = s.Restrictions.FirstOrDefault(x => x.UserId == userId);
        if (entry != null)
        {
            entry.Muted = false;
            entry.Reason = string.Empty;
            entry.ChangedByUserId = actor.Id;
            entry.ChangedAtUtc = DateTime.UtcNow;
            _repositories.SessionChatSettings.Replace(s);
        }
        WriteAudit("chat", actor.Id, "unmuteUser", sessionId + ":" + userId);
        return Ok("User unmuted.", RestrictionsPayload(s));
    }

    public ResponseEnvelope ChatRestrictionsLockPlayers(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var sessionId = RequireLength(PayloadReader.GetString(context.Request.Payload, "sessionId"), 1, 128, "sessionId");
        var s = GetOrCreateChatSettings(sessionId);
        s.LockPlayers = true;
        _repositories.SessionChatSettings.Replace(s);
        WriteAudit("chat", actor.Id, "lockPlayers", sessionId);
        return Ok("Players locked.", RestrictionsPayload(s));
    }

    public ResponseEnvelope ChatRestrictionsUnlockPlayers(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var sessionId = RequireLength(PayloadReader.GetString(context.Request.Payload, "sessionId"), 1, 128, "sessionId");
        var s = GetOrCreateChatSettings(sessionId);
        s.LockPlayers = false;
        _repositories.SessionChatSettings.Replace(s);
        WriteAudit("chat", actor.Id, "unlockPlayers", sessionId);
        return Ok("Players unlocked.", RestrictionsPayload(s));
    }

    private ResponseEnvelope ChatHistoryCore(CommandContext context, bool older)
    {
        var actor = GetCurrentAccount(context);
        var sessionId = RequireLength(PayloadReader.GetString(context.Request.Payload, "sessionId"), 1, 128, "sessionId");
        var limit = Math.Max(1, Math.Min(200, PayloadReader.GetInt(context.Request.Payload, "limit") ?? 50));
        var beforeTicks = PayloadReader.GetLong(context.Request.Payload, "beforeTicks");

        var items = _repositories.ChatMessages.Find(Builders<ChatMessage>.Filter.Eq(x => x.SessionId, sessionId))
            .Where(x => CanViewChatMessage(actor, x));
        if (beforeTicks.HasValue)
        {
            var dt = new DateTime(beforeTicks.Value, DateTimeKind.Utc);
            items = items.Where(x => x.CreatedUtc < dt);
        }

        var page = items.OrderByDescending(x => x.CreatedUtc).Take(limit).OrderBy(x => x.CreatedUtc).ToList();
        if (page.Count > 0)
        {
            UpdateReadState(sessionId, actor.Id, page.Last());
        }

        return Ok("Chat history loaded.", new Dictionary<string, object>
        {
            { "items", page.Select(ChatPayload).Cast<object>().ToArray() },
            { "nextBeforeTicks", page.Count > 0 ? (object)page.First().CreatedUtc.Ticks : 0L }
        });
    }

    private static ChatMessageType ParseChatType(string raw)
    {
        if (Enum.TryParse<ChatMessageType>(raw, true, out var t)) return t;
        throw new ArgumentException("Unsupported chat type.");
    }

    private void EnsureCanSendChatType(UserAccount actor, ChatMessageType type)
    {
        if (actor.Roles.Contains(UserRole.Observer)) throw new UnauthorizedAccessException("Observer cannot send chat messages.");
        var isAdmin = actor.Roles.Contains(UserRole.Admin) || actor.Roles.Contains(UserRole.SuperAdmin);
        if (!isAdmin && type == ChatMessageType.AdminOnly) throw new UnauthorizedAccessException("AdminOnly message is restricted.");
        if (type == ChatMessageType.System) throw new UnauthorizedAccessException("System message cannot be sent by clients.");
    }

    private void EnsureChatRestrictions(UserAccount actor, SessionChatSettings settings)
    {
        var isAdmin = actor.Roles.Contains(UserRole.Admin) || actor.Roles.Contains(UserRole.SuperAdmin);
        if (isAdmin) return;
        if (settings.LockPlayers) throw new InvalidOperationException("Chat is locked for players by admin.");
        var restriction = settings.Restrictions.FirstOrDefault(x => x.UserId == actor.Id);
        if (restriction != null && restriction.Muted) throw new InvalidOperationException("You are muted in session chat.");
    }

    private void EnsureSlowMode(UserAccount actor, string sessionId, ChatMessageType type, SessionChatSettings settings)
    {
        var isAdmin = actor.Roles.Contains(UserRole.Admin) || actor.Roles.Contains(UserRole.SuperAdmin);
        if (isAdmin) return;

        var cooldown = 0;
        if (type == ChatMessageType.Public) cooldown = settings.SlowMode.PublicSeconds;
        else if (type == ChatMessageType.HiddenToAdmins) cooldown = settings.SlowMode.HiddenToAdminsSeconds;
        else if (type == ChatMessageType.AdminOnly) cooldown = settings.SlowMode.AdminOnlySeconds;

        if (cooldown <= 0) return;
        var state = _repositories.ChatThrottleStates.Find(Builders<ChatUserThrottleState>.Filter.Eq(x => x.SessionId, sessionId) & Builders<ChatUserThrottleState>.Filter.Eq(x => x.UserId, actor.Id) & Builders<ChatUserThrottleState>.Filter.Eq(x => x.MessageType, type)).FirstOrDefault();
        if (state == null) return;
        var wait = (state.LastSentUtc.AddSeconds(cooldown) - DateTime.UtcNow).TotalSeconds;
        if (wait > 0)
        {
            _logger.Session($"chat.slowmode.violation user={actor.Id} session={sessionId} type={type} wait={wait:0}");
            throw new InvalidOperationException($"Slow mode active for {type}. Wait {Math.Ceiling(wait)}s.");
        }
    }

    private void TouchThrottle(string sessionId, string userId, ChatMessageType type)
    {
        var existing = _repositories.ChatThrottleStates.Find(Builders<ChatUserThrottleState>.Filter.Eq(x => x.SessionId, sessionId) & Builders<ChatUserThrottleState>.Filter.Eq(x => x.UserId, userId) & Builders<ChatUserThrottleState>.Filter.Eq(x => x.MessageType, type)).FirstOrDefault();
        if (existing == null)
        {
            _repositories.ChatThrottleStates.Insert(new ChatUserThrottleState { SessionId = sessionId, UserId = userId, MessageType = type, LastSentUtc = DateTime.UtcNow });
            return;
        }
        existing.LastSentUtc = DateTime.UtcNow;
        _repositories.ChatThrottleStates.Replace(existing);
    }

    private ChatReadState GetReadState(string sessionId, string userId)
    {
        return _repositories.ChatReadStates.Find(Builders<ChatReadState>.Filter.Eq(x => x.SessionId, sessionId) & Builders<ChatReadState>.Filter.Eq(x => x.UserId, userId)).FirstOrDefault();
    }

    private void UpdateReadState(string sessionId, string userId, ChatMessage message)
    {
        var read = GetReadState(sessionId, userId);
        if (read == null)
        {
            _repositories.ChatReadStates.Insert(new ChatReadState { SessionId = sessionId, UserId = userId, LastReadMessageId = message.Id, LastReadMessageUtc = message.CreatedUtc });
            return;
        }
        if (!read.LastReadMessageUtc.HasValue || message.CreatedUtc >= read.LastReadMessageUtc.Value)
        {
            read.LastReadMessageUtc = message.CreatedUtc;
            read.LastReadMessageId = message.Id;
            _repositories.ChatReadStates.Replace(read);
        }
    }

    private bool CanViewChatMessage(UserAccount actor, ChatMessage m)
    {
        var isAdmin = actor.Roles.Contains(UserRole.Admin) || actor.Roles.Contains(UserRole.SuperAdmin);
        if (m.MessageType == ChatMessageType.Public || m.MessageType == ChatMessageType.System) return true;
        if (m.MessageType == ChatMessageType.AdminOnly) return isAdmin;
        if (m.MessageType == ChatMessageType.HiddenToAdmins) return isAdmin || m.SenderUserId == actor.Id;
        return false;
    }

    private SessionChatSettings GetOrCreateChatSettings(string sessionId)
    {
        var settings = _repositories.SessionChatSettings.Find(Builders<SessionChatSettings>.Filter.Eq(x => x.SessionId, sessionId)).FirstOrDefault();
        if (settings != null) return settings;
        settings = new SessionChatSettings { SessionId = sessionId };
        _repositories.SessionChatSettings.Insert(settings);
        return settings;
    }

    private Dictionary<string, object> SlowModePayload(SessionChatSettings s)
    {
        return new Dictionary<string, object>
        {
            { "sessionId", s.SessionId },
            { "publicSeconds", s.SlowMode.PublicSeconds },
            { "hiddenToAdminsSeconds", s.SlowMode.HiddenToAdminsSeconds },
            { "adminOnlySeconds", s.SlowMode.AdminOnlySeconds }
        };
    }

    private Dictionary<string, object> RestrictionsPayload(SessionChatSettings s)
    {
        return new Dictionary<string, object>
        {
            { "sessionId", s.SessionId },
            { "lockPlayers", s.LockPlayers },
            { "restrictions", s.Restrictions.Select(x => new Dictionary<string, object>{{"userId",x.UserId},{"muted",x.Muted},{"reason",x.Reason},{"changedBy",x.ChangedByUserId},{"changedAt",x.ChangedAtUtc}}).Cast<object>().ToArray() }
        };
    }

    private Dictionary<string, object> ChatPayload(ChatMessage m)
    {
        return new Dictionary<string, object>
        {
            { "messageId", m.Id },
            { "sessionId", m.SessionId },
            { "senderUserId", m.SenderUserId },
            { "senderDisplayName", m.SenderDisplayName },
            { "type", m.MessageType.ToString() },
            { "text", m.Text },
            { "createdUtc", m.CreatedUtc },
            { "updatedUtc", m.UpdatedUtc },
            { "visibility", m.VisibilityChannel },
            { "schemaVersion", m.SchemaVersion }
        };
    }

    private void PublishSystemMessage(string sessionId, string text)
    {
        var msg = new ChatMessage
        {
            SessionId = sessionId,
            SenderUserId = "system",
            SenderDisplayName = "System",
            MessageType = ChatMessageType.System,
            VisibilityChannel = ChatMessageType.System.ToString(),
            Text = text,
            ModerationState = ChatModerationState.Active
        };
        _repositories.ChatMessages.Insert(msg);
        _logger.Session($"chat.system session={sessionId} text={text}");
    }
}
