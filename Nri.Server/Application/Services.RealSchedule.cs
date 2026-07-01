using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MongoDB.Driver;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;
using Nri.Shared.Utilities;

namespace Nri.Server.Application;

public partial class ServiceHub
{
    private const int RealScheduleDefaultListDays = 120;

    public ResponseEnvelope RealScheduleList(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!RealScheduleEventsEnabled()) return RealScheduleDisabled(context.Request.Command);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var campaignId = PayloadReader.GetString(payload, "campaignId") ?? string.Empty;
        var includeArchived = PayloadReader.GetBool(payload, "includeArchived");
        var items = LoadRealScheduleEvents(campaignId, includeArchived)
            .OrderBy(x => x.StartUtc)
            .Take(500)
            .Select(x => (object)RealScheduleEventPayload(x, includeAdminFields: true))
            .ToArray();
        _logger.Admin($"schedule.real.list actor={actor.Login} campaignId={campaignId} count={items.Length}");
        var response = RealScheduleClockPayload(DateTime.UtcNow);
        response["items"] = items;
        return Ok("Real schedule events loaded.", response);
    }

    public ResponseEnvelope RealScheduleCreate(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!RealScheduleEventsEnabled()) return RealScheduleDisabled(context.Request.Command);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var startUtc = ReadUtc(payload, "startUtc", DateTime.UtcNow.AddDays(7));
        var endUtc = ReadOptionalUtc(payload, "endUtc");
        ValidateRealScheduleRange(startUtc, endUtc);
        var visibility = RequireRealScheduleVisibility(PayloadReader.GetString(payload, "visibilityMode"), PayloadReader.GetBool(payload, "isPlayerVisible"));
        var now = DateTime.UtcNow;
        var item = new RealScheduleEventState
        {
            CampaignId = RequireLength(PayloadReader.GetString(payload, "campaignId") ?? "default", 1, 128, "campaignId"),
            SessionId = RequireLength(PayloadReader.GetString(payload, "sessionId"), 0, 128, "sessionId"),
            GroupId = RequireLength(PayloadReader.GetString(payload, "groupId"), 0, 128, "groupId"),
            LinkedWorldCalendarEventId = RequireLength(PayloadReader.GetString(payload, "linkedWorldCalendarEventId"), 0, 128, "linkedWorldCalendarEventId"),
            LinkedWorldDateTime = ReadLinkedWorldDateTime(payload),
            Title = RequireLength(PayloadReader.GetString(payload, "title") ?? string.Empty, 2, 180, "title"),
            Description = RequireLength(PayloadReader.GetString(payload, "description"), 0, 4096, "description"),
            EventType = RequireRealScheduleEventType(PayloadReader.GetString(payload, "eventType")),
            Status = RequireRealScheduleStatus(PayloadReader.GetString(payload, "status")),
            StartUtc = startUtc,
            EndUtc = endUtc,
            TimeZoneId = NormalizeTimeZoneId(PayloadReader.GetString(payload, "timeZoneId")),
            GMUserId = RequireLength(PayloadReader.GetString(payload, "gmUserId"), 0, 128, "gmUserId"),
            GMDisplayName = ResolveSafeDisplayName(PayloadReader.GetString(payload, "gmDisplayName"), PayloadReader.GetString(payload, "gmUserId"), "GM не указан"),
            OrganizerUserId = RequireLength(PayloadReader.GetString(payload, "organizerUserId"), 0, 128, "organizerUserId"),
            OrganizerDisplayName = ResolveSafeDisplayName(PayloadReader.GetString(payload, "organizerDisplayName"), PayloadReader.GetString(payload, "organizerUserId"), string.Empty),
            LocationText = RequireLength(PayloadReader.GetString(payload, "locationText"), 0, 512, "locationText"),
            ConnectionInfoSummary = RequireLength(PayloadReader.GetString(payload, "connectionInfoSummary"), 0, 1024, "connectionInfoSummary"),
            IsPlayerVisible = IsRealScheduleVisibleToPlayers(visibility),
            VisibilityMode = visibility,
            ReminderEnabled = PayloadReader.GetBool(payload, "reminderEnabled"),
            ReminderBeforeMinutes = ReadReminderBeforeMinutes(payload),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            CreatedByUserId = actor.Id,
            UpdatedByUserId = actor.Id,
            PublicNotes = RequireLength(PayloadReader.GetString(payload, "publicNotes"), 0, 2048, "publicNotes"),
            GMNotes = RequireLength(PayloadReader.GetString(payload, "gmNotes"), 0, 4096, "gmNotes")
        };
        _repositories.RealScheduleEvents.Insert(item);
        UpsertRealScheduleSessionLink(item);
        WriteAudit("real_schedule", actor.Id, "schedule.event.created", item.Id);
        WriteRealScheduleJournalEntry(actor, item, "created");
        _logger.Admin($"schedule.real.create eventId={item.Id} campaignId={item.CampaignId} startUtc={item.StartUtc:o}");
        return Ok("Real schedule event created.", new Dictionary<string, object> { { "item", RealScheduleEventPayload(item, includeAdminFields: true) } });
    }

    public ResponseEnvelope RealScheduleGet(CommandContext context)
    {
        RequireAdmin(context);
        if (!RealScheduleEventsEnabled()) return RealScheduleDisabled(context.Request.Command);
        var item = RequireRealScheduleEvent(context);
        return Ok("Real schedule event loaded.", new Dictionary<string, object> { { "item", RealScheduleEventPayload(item, includeAdminFields: true) }, { "participants", RealScheduleParticipantsPayload(item.Id, includeAdminFields: true) } });
    }

    public ResponseEnvelope RealScheduleUpdate(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!RealScheduleEventsEnabled()) return RealScheduleDisabled(context.Request.Command);
        var item = RequireRealScheduleEvent(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        if (payload.ContainsKey("title")) item.Title = RequireLength(PayloadReader.GetString(payload, "title"), 2, 180, "title");
        if (payload.ContainsKey("description")) item.Description = RequireLength(PayloadReader.GetString(payload, "description"), 0, 4096, "description");
        if (payload.ContainsKey("eventType")) item.EventType = RequireRealScheduleEventType(PayloadReader.GetString(payload, "eventType"));
        if (payload.ContainsKey("status")) item.Status = RequireRealScheduleStatus(PayloadReader.GetString(payload, "status"));
        if (payload.ContainsKey("sessionId")) item.SessionId = RequireLength(PayloadReader.GetString(payload, "sessionId"), 0, 128, "sessionId");
        if (payload.ContainsKey("groupId")) item.GroupId = RequireLength(PayloadReader.GetString(payload, "groupId"), 0, 128, "groupId");
        if (payload.ContainsKey("linkedWorldCalendarEventId")) item.LinkedWorldCalendarEventId = RequireLength(PayloadReader.GetString(payload, "linkedWorldCalendarEventId"), 0, 128, "linkedWorldCalendarEventId");
        if (payload.ContainsKey("gmUserId")) item.GMUserId = RequireLength(PayloadReader.GetString(payload, "gmUserId"), 0, 128, "gmUserId");
        if (payload.ContainsKey("gmDisplayName")) item.GMDisplayName = ResolveSafeDisplayName(PayloadReader.GetString(payload, "gmDisplayName"), item.GMUserId, "GM не указан");
        if (payload.ContainsKey("organizerUserId")) item.OrganizerUserId = RequireLength(PayloadReader.GetString(payload, "organizerUserId"), 0, 128, "organizerUserId");
        if (payload.ContainsKey("organizerDisplayName")) item.OrganizerDisplayName = ResolveSafeDisplayName(PayloadReader.GetString(payload, "organizerDisplayName"), item.OrganizerUserId, string.Empty);
        if (payload.ContainsKey("locationText")) item.LocationText = RequireLength(PayloadReader.GetString(payload, "locationText"), 0, 512, "locationText");
        if (payload.ContainsKey("connectionInfoSummary")) item.ConnectionInfoSummary = RequireLength(PayloadReader.GetString(payload, "connectionInfoSummary"), 0, 1024, "connectionInfoSummary");
        if (payload.ContainsKey("publicNotes")) item.PublicNotes = RequireLength(PayloadReader.GetString(payload, "publicNotes"), 0, 2048, "publicNotes");
        if (payload.ContainsKey("gmNotes")) item.GMNotes = RequireLength(PayloadReader.GetString(payload, "gmNotes"), 0, 4096, "gmNotes");
        if (payload.ContainsKey("isPlayerVisible") || payload.ContainsKey("visibilityMode"))
        {
            item.VisibilityMode = RequireRealScheduleVisibility(PayloadReader.GetString(payload, "visibilityMode"), PayloadReader.GetBool(payload, "isPlayerVisible"));
            item.IsPlayerVisible = IsRealScheduleVisibleToPlayers(item.VisibilityMode);
        }
        if (payload.ContainsKey("reminderEnabled")) item.ReminderEnabled = PayloadReader.GetBool(payload, "reminderEnabled");
        if (payload.ContainsKey("reminderBeforeMinutes")) item.ReminderBeforeMinutes = ReadReminderBeforeMinutes(payload);
        item.LinkedWorldDateTime = ReadLinkedWorldDateTime(payload) ?? item.LinkedWorldDateTime;
        TouchRealScheduleEvent(item, actor.Id);
        _repositories.RealScheduleEvents.Replace(item);
        UpsertRealScheduleSessionLink(item);
        WriteAudit("real_schedule", actor.Id, "schedule.event.updated", item.Id);
        WriteRealScheduleJournalEntry(actor, item, "updated");
        return Ok("Real schedule event updated.", new Dictionary<string, object> { { "item", RealScheduleEventPayload(item, includeAdminFields: true) } });
    }

    public ResponseEnvelope RealScheduleReschedule(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!RealScheduleEventsEnabled()) return RealScheduleDisabled(context.Request.Command);
        var item = RequireRealScheduleEvent(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var startUtc = ReadUtc(payload, "startUtc", item.StartUtc);
        var endUtc = ReadOptionalUtc(payload, "endUtc");
        ValidateRealScheduleRange(startUtc, endUtc);
        item.StartUtc = startUtc;
        item.EndUtc = endUtc;
        item.TimeZoneId = NormalizeTimeZoneId(PayloadReader.GetString(payload, "timeZoneId") ?? item.TimeZoneId);
        item.Status = RealScheduleEventStatusIds.Rescheduled;
        TouchRealScheduleEvent(item, actor.Id);
        _repositories.RealScheduleEvents.Replace(item);
        UpsertRealScheduleSessionLink(item);
        WriteAudit("real_schedule", actor.Id, "schedule.event.rescheduled", item.Id);
        WriteRealScheduleJournalEntry(actor, item, "rescheduled");
        return Ok("Real schedule event rescheduled.", new Dictionary<string, object> { { "item", RealScheduleEventPayload(item, includeAdminFields: true) } });
    }

    public ResponseEnvelope RealScheduleCancel(CommandContext context)
        => SetRealScheduleStatus(context, RealScheduleEventStatusIds.Cancelled, "schedule.event.cancelled", "Real schedule event cancelled.");

    public ResponseEnvelope RealScheduleStart(CommandContext context)
        => SetRealScheduleStatus(context, RealScheduleEventStatusIds.InProgress, "schedule.event.started", "Real schedule event started.");

    public ResponseEnvelope RealScheduleComplete(CommandContext context)
        => SetRealScheduleStatus(context, RealScheduleEventStatusIds.Completed, "schedule.event.completed", "Real schedule event completed.");

    public ResponseEnvelope RealScheduleArchive(CommandContext context)
        => SetRealScheduleStatus(context, RealScheduleEventStatusIds.Archived, "schedule.event.archived", "Real schedule event archived.");

    public ResponseEnvelope RealScheduleParticipantList(CommandContext context)
    {
        RequireAdmin(context);
        if (!RealScheduleEventsEnabled()) return RealScheduleDisabled(context.Request.Command);
        var item = RequireRealScheduleEvent(context);
        return Ok("Real schedule participants loaded.", new Dictionary<string, object> { { "items", RealScheduleParticipantsPayload(item.Id, includeAdminFields: true) } });
    }

    public ResponseEnvelope RealScheduleParticipantAdd(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!RealScheduleEventsEnabled()) return RealScheduleDisabled(context.Request.Command);
        var item = RequireRealScheduleEvent(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var participant = new RealScheduleParticipantState
        {
            EventId = item.Id,
            CampaignId = item.CampaignId,
            UserId = RequireLength(PayloadReader.GetString(payload, "userId"), 0, 128, "userId"),
            DisplayName = RequireLength(FirstNonEmpty(PayloadReader.GetString(payload, "displayName"), "Участник"), 1, 180, "displayName"),
            ParticipantRole = NormalizeParticipantRole(PayloadReader.GetString(payload, "participantRole")),
            ResponseStatus = NormalizeParticipantResponse(PayloadReader.GetString(payload, "responseStatus")),
            IsPlayerVisible = PayloadReader.GetBool(payload, "isPlayerVisible"),
            AddedAtUtc = DateTime.UtcNow,
            AddedByUserId = actor.Id,
            UpdatedAtUtc = DateTime.UtcNow
        };
        _repositories.RealScheduleParticipants.Insert(participant);
        WriteAudit("real_schedule", actor.Id, "schedule.participant.added", participant.Id);
        _logger.Admin($"schedule.real.participant.add eventId={item.Id} participantId={participant.Id}");
        return Ok("Real schedule participant added.", new Dictionary<string, object> { { "participant", RealScheduleParticipantPayload(participant, includeAdminFields: true) } });
    }

    public ResponseEnvelope RealScheduleParticipantUpdate(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!RealScheduleEventsEnabled()) return RealScheduleDisabled(context.Request.Command);
        var participant = RequireRealScheduleParticipant(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        if (payload.ContainsKey("displayName")) participant.DisplayName = RequireLength(PayloadReader.GetString(payload, "displayName"), 1, 180, "displayName");
        if (payload.ContainsKey("participantRole")) participant.ParticipantRole = NormalizeParticipantRole(PayloadReader.GetString(payload, "participantRole"));
        if (payload.ContainsKey("responseStatus")) participant.ResponseStatus = NormalizeParticipantResponse(PayloadReader.GetString(payload, "responseStatus"));
        if (payload.ContainsKey("isPlayerVisible")) participant.IsPlayerVisible = PayloadReader.GetBool(payload, "isPlayerVisible");
        participant.UpdatedAtUtc = DateTime.UtcNow;
        _repositories.RealScheduleParticipants.Replace(participant);
        WriteAudit("real_schedule", actor.Id, "schedule.participant.updated", participant.Id);
        return Ok("Real schedule participant updated.", new Dictionary<string, object> { { "participant", RealScheduleParticipantPayload(participant, includeAdminFields: true) } });
    }

    public ResponseEnvelope RealScheduleParticipantRemove(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!RealScheduleEventsEnabled()) return RealScheduleDisabled(context.Request.Command);
        var participant = RequireRealScheduleParticipant(context);
        participant.IsArchived = true;
        participant.UpdatedAtUtc = DateTime.UtcNow;
        _repositories.RealScheduleParticipants.Replace(participant);
        WriteAudit("real_schedule", actor.Id, "schedule.participant.removed", participant.Id);
        return Ok("Real schedule participant removed.", new Dictionary<string, object> { { "participantId", participant.Id } });
    }

    public ResponseEnvelope RealSchedulePlayerList(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!RealSchedulePlayerEnabled()) return RealScheduleDisabled(context.Request.Command);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var campaignId = PayloadReader.GetString(payload, "campaignId") ?? string.Empty;
        var now = DateTime.UtcNow;
        var fromUtc = ReadUtc(payload, "fromUtc", now.AddDays(-1));
        var untilUtc = ReadUtc(payload, "untilUtc", now.AddDays(RealScheduleDefaultListDays));
        var items = LoadRealScheduleEvents(campaignId, includeArchived: false)
            .Where(CanPlayerSeeRealScheduleEvent)
            .Where(x => x.StartUtc >= fromUtc && x.StartUtc <= untilUtc)
            .OrderBy(x => x.StartUtc)
            .Take(100)
            .Select(x => (object)RealScheduleEventPayload(x, includeAdminFields: false))
            .ToArray();
        _logger.Admin($"schedule.real.player.list actor={actor.Login} campaignId={campaignId} count={items.Length}");
        var response = RealScheduleClockPayload(now);
        response["items"] = items;
        return Ok("Real schedule player events loaded.", response);
    }

    public ResponseEnvelope RealSchedulePlayerNext(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!RealSchedulePlayerEnabled()) return RealScheduleDisabled(context.Request.Command);
        var campaignId = PayloadReader.GetString(context.Request.Payload ?? new Dictionary<string, object>(), "campaignId") ?? string.Empty;
        var now = DateTime.UtcNow;
        var item = LoadRealScheduleEvents(campaignId, includeArchived: false)
            .Where(CanPlayerSeeRealScheduleEvent)
            .Where(x => x.StartUtc >= now && x.Status != RealScheduleEventStatusIds.Cancelled)
            .OrderBy(x => x.StartUtc)
            .FirstOrDefault();
        _logger.Admin($"schedule.real.player.next actor={actor.Login} campaignId={campaignId} hasNext={item != null}");
        return Ok("Real schedule next event loaded.", new Dictionary<string, object>
        {
            { "hasNext", item != null },
            { "item", item == null ? new Dictionary<string, object>() : RealScheduleEventPayload(item, includeAdminFields: false) },
            { "serverLocalTime", ToRealScheduleLocal(now, string.Empty) },
            { "serverLocalDisplay", FormatRealScheduleLocal(now, string.Empty) },
            { "serverTimeZoneId", TimeZoneInfo.Local.Id },
            { "serverTimeZoneDisplayName", TimeZoneInfo.Local.DisplayName }
        });
    }

    public ResponseEnvelope RealSchedulePlayerGet(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!RealSchedulePlayerEnabled()) return RealScheduleDisabled(context.Request.Command);
        var eventId = PayloadReader.GetString(context.Request.Payload ?? new Dictionary<string, object>(), "eventId")
            ?? PayloadReader.GetString(context.Request.Payload ?? new Dictionary<string, object>(), "scheduleEventId")
            ?? string.Empty;
        var item = _repositories.RealScheduleEvents.GetById(eventId);
        if (item == null || !CanPlayerSeeRealScheduleEvent(item))
        {
            _logger.Admin($"schedule.real.player.get.hidden actor={actor.Login} eventId={eventId}");
            return Error("Real schedule event not found.", ResponseStatus.NotFound, ErrorCode.NotFound);
        }
        return Ok("Real schedule player event loaded.", new Dictionary<string, object> { { "item", RealScheduleEventPayload(item, includeAdminFields: false) } });
    }

    private ResponseEnvelope SetRealScheduleStatus(CommandContext context, string status, string auditAction, string message)
    {
        var actor = RequireAdmin(context);
        if (!RealScheduleEventsEnabled()) return RealScheduleDisabled(context.Request.Command);
        var item = RequireRealScheduleEvent(context);
        item.Status = status;
        var now = DateTime.UtcNow;
        if (status == RealScheduleEventStatusIds.Cancelled) item.CancelledAtUtc = now;
        if (status == RealScheduleEventStatusIds.Completed) item.CompletedAtUtc = now;
        TouchRealScheduleEvent(item, actor.Id);
        _repositories.RealScheduleEvents.Replace(item);
        WriteAudit("real_schedule", actor.Id, auditAction, item.Id);
        WriteRealScheduleJournalEntry(actor, item, status);
        _logger.Admin($"schedule.real.status eventId={item.Id} status={status}");
        return Ok(message, new Dictionary<string, object> { { "item", RealScheduleEventPayload(item, includeAdminFields: true) } });
    }

    private IReadOnlyCollection<RealScheduleEventState> LoadRealScheduleEvents(string campaignId, bool includeArchived)
    {
        var filter = string.IsNullOrWhiteSpace(campaignId)
            ? Builders<RealScheduleEventState>.Filter.Empty
            : Builders<RealScheduleEventState>.Filter.Eq(x => x.CampaignId, campaignId);
        if (!includeArchived) filter &= Builders<RealScheduleEventState>.Filter.Ne(x => x.Status, RealScheduleEventStatusIds.Archived);
        return _repositories.RealScheduleEvents.Find(filter);
    }

    private RealScheduleEventState RequireRealScheduleEvent(CommandContext context)
    {
        var eventId = PayloadReader.GetString(context.Request.Payload ?? new Dictionary<string, object>(), "eventId")
            ?? PayloadReader.GetString(context.Request.Payload ?? new Dictionary<string, object>(), "scheduleEventId")
            ?? string.Empty;
        var item = _repositories.RealScheduleEvents.GetById(eventId);
        if (item == null) throw new KeyNotFoundException("Real schedule event not found.");
        return item;
    }

    private RealScheduleParticipantState RequireRealScheduleParticipant(CommandContext context)
    {
        var participantId = PayloadReader.GetString(context.Request.Payload ?? new Dictionary<string, object>(), "participantId") ?? string.Empty;
        var item = _repositories.RealScheduleParticipants.GetById(participantId);
        if (item == null) throw new KeyNotFoundException("Real schedule participant not found.");
        return item;
    }

    private void TouchRealScheduleEvent(RealScheduleEventState item, string actorUserId)
    {
        item.UpdatedAtUtc = DateTime.UtcNow;
        item.UpdatedByUserId = actorUserId ?? string.Empty;
    }

    private void UpsertRealScheduleSessionLink(RealScheduleEventState item)
    {
        if (!RealScheduleSessionLinkEnabled() || string.IsNullOrWhiteSpace(item.SessionId)) return;
        var sessions = _repositories.CurrentSessions.Find(Builders<CurrentSessionState>.Filter.Eq(x => x.SessionId, item.SessionId));
        var session = sessions.FirstOrDefault(x => x.CampaignId == item.CampaignId && !x.IsArchived);
        if (session == null) return;
        session.CurrentRealStartUtc = item.StartUtc;
        session.CurrentRealEndUtc = item.EndUtc;
        session.GMUserId = FirstNonEmpty(session.GMUserId, item.GMUserId);
        session.GMDisplayName = FirstNonEmpty(session.GMDisplayName, item.GMDisplayName);
        session.UpdatedAtUtc = DateTime.UtcNow;
        _repositories.CurrentSessions.Replace(session);
    }

    private static bool CanPlayerSeeRealScheduleEvent(RealScheduleEventState item)
        => !item.Status.Equals(RealScheduleEventStatusIds.Archived, StringComparison.OrdinalIgnoreCase)
           && item.IsPlayerVisible
           && IsRealScheduleVisibleToPlayers(item.VisibilityMode);

    private Dictionary<string, object> RealScheduleEventPayload(RealScheduleEventState item, bool includeAdminFields)
    {
        var participants = _repositories.RealScheduleParticipants.Find(Builders<RealScheduleParticipantState>.Filter.Eq(x => x.EventId, item.Id))
            .Where(x => !x.IsArchived && (includeAdminFields || x.IsPlayerVisible))
            .Select(x => (object)RealScheduleParticipantPayload(x, includeAdminFields))
            .ToArray();
        var payload = new Dictionary<string, object>
        {
            { "eventId", item.Id },
            { "campaignId", item.CampaignId },
            { "sessionId", includeAdminFields ? item.SessionId : string.Empty },
            { "groupId", includeAdminFields || item.IsPlayerVisible ? item.GroupId : string.Empty },
            { "title", item.Title },
            { "description", includeAdminFields ? item.Description : item.PublicNotes },
            { "eventType", item.EventType },
            { "eventTypeDisplay", RealScheduleEventTypeDisplay(item.EventType) },
            { "status", item.Status },
            { "statusDisplay", RealScheduleStatusDisplay(item.Status) },
            { "startUtc", item.StartUtc },
            { "endUtc", item.EndUtc ?? (object)string.Empty },
            { "startLocal", ToRealScheduleLocal(item.StartUtc, item.TimeZoneId) },
            { "endLocal", item.EndUtc.HasValue ? ToRealScheduleLocal(item.EndUtc.Value, item.TimeZoneId) : string.Empty },
            { "timeZoneId", item.TimeZoneId },
            { "localStartDisplay", FormatRealScheduleLocal(item.StartUtc, item.TimeZoneId) },
            { "localEndDisplay", item.EndUtc.HasValue ? FormatRealScheduleLocal(item.EndUtc.Value, item.TimeZoneId) : string.Empty },
            { "countdownText", RealScheduleCountdownText(item, DateTime.UtcNow) },
            { "gmDisplayName", FirstNonEmpty(item.GMDisplayName, "GM не указан") },
            { "organizerDisplayName", item.OrganizerDisplayName },
            { "locationText", item.LocationText },
            { "connectionInfoSummary", item.ConnectionInfoSummary },
            { "isPlayerVisible", item.IsPlayerVisible },
            { "visibilityMode", includeAdminFields ? item.VisibilityMode : string.Empty },
            { "reminderEnabled", item.ReminderEnabled },
            { "reminderBeforeMinutes", item.ReminderBeforeMinutes ?? 0 },
            { "publicNotes", item.PublicNotes },
            { "participants", participants },
            { "createdAtUtc", item.CreatedAtUtc },
            { "updatedAtUtc", item.UpdatedAtUtc }
        };
        if (item.LinkedWorldDateTime != null)
        {
            payload["linkedWorldDateTime"] = new Dictionary<string, object>
            {
                { "year", item.LinkedWorldDateTime.Year },
                { "displayYear", WorldCalendarMath.DisplayYear(item.LinkedWorldDateTime) },
                { "era", WorldCalendarMath.EraId(item.LinkedWorldDateTime) },
                { "eraName", WorldCalendarMath.EraName(item.LinkedWorldDateTime) },
                { "eraShortName", WorldCalendarMath.EraShortName(item.LinkedWorldDateTime) },
                { "monthOrder", item.LinkedWorldDateTime.MonthOrder },
                { "dayOfMonth", item.LinkedWorldDateTime.DayOfMonth },
                { "hour", item.LinkedWorldDateTime.Hour },
                { "minute", item.LinkedWorldDateTime.Minute }
            };
        }
        if (includeAdminFields)
        {
            payload["gmUserId"] = item.GMUserId;
            payload["organizerUserId"] = item.OrganizerUserId;
            payload["linkedWorldCalendarEventId"] = item.LinkedWorldCalendarEventId;
            payload["gmNotes"] = item.GMNotes;
            payload["cancelledAtUtc"] = item.CancelledAtUtc ?? (object)string.Empty;
            payload["completedAtUtc"] = item.CompletedAtUtc ?? (object)string.Empty;
        }
        return payload;
    }

    private object[] RealScheduleParticipantsPayload(string eventId, bool includeAdminFields)
        => _repositories.RealScheduleParticipants.Find(Builders<RealScheduleParticipantState>.Filter.Eq(x => x.EventId, eventId))
            .Where(x => !x.IsArchived && (includeAdminFields || x.IsPlayerVisible))
            .OrderBy(x => x.ParticipantRole)
            .ThenBy(x => x.DisplayName)
            .Select(x => (object)RealScheduleParticipantPayload(x, includeAdminFields))
            .ToArray();

    private static Dictionary<string, object> RealScheduleParticipantPayload(RealScheduleParticipantState item, bool includeAdminFields)
    {
        var payload = new Dictionary<string, object>
        {
            { "participantId", item.Id },
            { "displayName", item.DisplayName },
            { "participantRole", item.ParticipantRole },
            { "participantRoleDisplay", ParticipantRoleDisplay(item.ParticipantRole) },
            { "responseStatus", item.ResponseStatus },
            { "responseStatusDisplay", ParticipantResponseDisplay(item.ResponseStatus) },
            { "isPlayerVisible", item.IsPlayerVisible }
        };
        if (includeAdminFields) payload["userId"] = item.UserId;
        return payload;
    }

    private static DateTime ReadUtc(Dictionary<string, object> payload, string key, DateTime fallback)
    {
        var raw = PayloadReader.GetString(payload, key);
        if (string.IsNullOrWhiteSpace(raw)) return DateTime.SpecifyKind(fallback, DateTimeKind.Utc);
        if (!DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
            throw new InvalidOperationException($"Некорректная дата/время: {key}.");
        return DateTime.SpecifyKind(parsed.ToUniversalTime(), DateTimeKind.Utc);
    }

    private static DateTime? ReadOptionalUtc(Dictionary<string, object> payload, string key)
    {
        var raw = PayloadReader.GetString(payload, key);
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return ReadUtc(payload, key, DateTime.UtcNow);
    }

    private static void ValidateRealScheduleRange(DateTime startUtc, DateTime? endUtc)
    {
        if (startUtc.Year < 2000 || startUtc.Year > 2200) throw new InvalidOperationException("Real schedule date is outside the supported MVP range.");
        if (endUtc.HasValue && endUtc.Value < startUtc) throw new InvalidOperationException("Event end time cannot be earlier than start time.");
    }

    private static string NormalizeTimeZoneId(string? value)
    {
        return ResolveRealScheduleTimeZone(value).Id;
    }

    private static DateTime ToRealScheduleLocal(DateTime utc, string timeZoneId)
    {
        var zone = ResolveRealScheduleTimeZone(timeZoneId);
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone);
    }

    private static string FormatRealScheduleLocal(DateTime utc, string timeZoneId)
    {
        var zone = ResolveRealScheduleTimeZone(timeZoneId);
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone);
        return $"{local:dd.MM.yyyy HH:mm} ({RealScheduleTimeZoneLabel(zone)})";
    }

    private static TimeZoneInfo ResolveRealScheduleTimeZone(string? value)
    {
        var id = value?.Trim();
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(id))
        {
            var requestedId = id!;
            if (requestedId.Equals("UTC", StringComparison.OrdinalIgnoreCase)
                || requestedId.Equals("Etc/UTC", StringComparison.OrdinalIgnoreCase)
                || requestedId.Equals("Coordinated Universal Time", StringComparison.OrdinalIgnoreCase))
                return TimeZoneInfo.Local;
            candidates.Add(requestedId);
            if (requestedId.Equals("Europe/Moscow", StringComparison.OrdinalIgnoreCase)) candidates.Add("Russian Standard Time");
            if (requestedId.Equals("Russian Standard Time", StringComparison.OrdinalIgnoreCase)) candidates.Add("Europe/Moscow");
        }

        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(candidate); }
            catch { }
        }

        return TimeZoneInfo.Local;
    }

    private static string RealScheduleTimeZoneLabel(TimeZoneInfo zone)
    {
        if (zone.Id.Equals(TimeZoneInfo.Local.Id, StringComparison.OrdinalIgnoreCase)) return "местное время";
        if (zone.Id.Equals("Russian Standard Time", StringComparison.OrdinalIgnoreCase)
            || zone.Id.Equals("Europe/Moscow", StringComparison.OrdinalIgnoreCase))
            return "московское время";
        return zone.Id;
    }

    private static Dictionary<string, object> RealScheduleClockPayload(DateTime nowUtc)
    {
        var zone = ResolveRealScheduleTimeZone(string.Empty);
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc), zone);
        var zoneLabel = RealScheduleTimeZoneLabel(zone);
        return new Dictionary<string, object>
        {
            { "serverLocalTime", local },
            { "serverLocalDisplay", $"{local:dd.MM.yyyy HH:mm} ({zoneLabel})" },
            { "serverTimeZoneId", zone.Id },
            { "serverTimeZoneDisplayName", zoneLabel }
        };
    }

    private static string RealScheduleCountdownText(RealScheduleEventState item, DateTime nowUtc)
    {
        if (item.Status == RealScheduleEventStatusIds.InProgress) return "Идёт сейчас";
        if (item.Status == RealScheduleEventStatusIds.Completed || item.Status == RealScheduleEventStatusIds.Cancelled) return "Прошло";
        var delta = item.StartUtc - nowUtc;
        if (delta.TotalMinutes <= 0) return "Начинается сейчас";
        if (delta.TotalDays >= 1) return $"Через {(int)delta.TotalDays} дн. {delta.Hours} ч.";
        if (delta.TotalHours >= 1) return $"Через {(int)delta.TotalHours} ч. {delta.Minutes} мин.";
        return $"Через {Math.Max(1, (int)delta.TotalMinutes)} мин.";
    }

    private static int? ReadReminderBeforeMinutes(Dictionary<string, object> payload)
    {
        var value = PayloadReader.GetInt(payload, "reminderBeforeMinutes");
        if (!value.HasValue || value.Value <= 0) return null;
        if (value.Value > 60 * 24 * 30) throw new InvalidOperationException("Reminder lead time is too large for MVP.");
        return value.Value;
    }

    private static WorldDateTimeValue? ReadLinkedWorldDateTime(Dictionary<string, object> payload)
    {
        if (!payload.ContainsKey("worldYear") && !payload.ContainsKey("worldMonthOrder") && !payload.ContainsKey("worldDayOfMonth")) return null;
        var year = PayloadReader.GetInt(payload, "worldYear") ?? 0;
        var era = PayloadReader.GetString(payload, "worldEra");
        if (!string.IsNullOrWhiteSpace(era))
            year = WorldCalendarMath.ToSignedYear(year, era);
        var value = new WorldDateTimeValue
        {
            Year = year,
            MonthOrder = PayloadReader.GetInt(payload, "worldMonthOrder") ?? 1,
            DayOfMonth = PayloadReader.GetInt(payload, "worldDayOfMonth") ?? 1,
            Hour = PayloadReader.GetInt(payload, "worldHour") ?? 0,
            Minute = PayloadReader.GetInt(payload, "worldMinute") ?? 0
        };
        if (value.MonthOrder < 1 || value.MonthOrder > WorldCalendarDefaults.MonthsPerYear)
            throw new InvalidOperationException("Linked world date month is invalid.");
        if (value.DayOfMonth < 1 || value.DayOfMonth > WorldCalendarDefaults.DaysPerMonth)
            throw new InvalidOperationException("Linked world date day is invalid.");
        return value;
    }

    private string ResolveSafeDisplayName(string? displayName, string? userId, string fallback)
    {
        var explicitName = FirstNonEmpty(displayName);
        if (!string.IsNullOrWhiteSpace(explicitName)) return RequireLength(explicitName, 0, 180, "displayName");
        if (!string.IsNullOrWhiteSpace(userId))
        {
            var profile = _repositories.Profiles.GetById(userId);
            if (profile != null && !string.IsNullOrWhiteSpace(profile.DisplayName)) return profile.DisplayName;
            var account = _repositories.Accounts.GetById(userId);
            if (account != null && !string.IsNullOrWhiteSpace(account.Login)) return account.Login;
        }
        return fallback;
    }

    private void WriteRealScheduleJournalEntry(UserAccount actor, RealScheduleEventState item, string action)
    {
        if (!EventJournalScheduleIntegrationEnabled()) return;
        var playerVisible = CanPlayerSeeRealScheduleEvent(item);
        var now = DateTime.UtcNow;
        var entry = new EventJournalEntryState
        {
            CampaignId = item.CampaignId,
            SessionId = item.SessionId,
            GroupId = item.GroupId,
            SourceModule = "real_schedule",
            SourceEventType = $"real_schedule.event.{action}",
            SourceEventId = item.Id,
            CorrelationId = $"real_schedule:{item.Id}:{action}:{item.UpdatedAtUtc.Ticks}",
            EntryType = EventJournalEntryTypeIds.Automatic,
            Category = EventJournalCategoryIds.RealSchedule,
            Severity = EventJournalSeverityIds.Information,
            Title = $"Расписание: {item.Title}",
            Summary = FirstNonEmpty(item.PublicNotes, item.Description, item.Title),
            PlayerSummary = playerVisible ? FirstNonEmpty(item.PublicNotes, item.Title) : string.Empty,
            GMDetails = item.GMNotes,
            VisibilityMode = playerVisible ? EventJournalVisibilityModeIds.PlayerVisible : EventJournalVisibilityModeIds.GMOnly,
            IsPlayerVisible = playerVisible,
            IsAutomatic = true,
            ActorUserId = actor.Id,
            ActorDisplayName = FirstNonEmpty(actor.Login, actor.Id),
            SubjectEntityType = EventJournalEntityTypeIds.RealScheduleEvent,
            SubjectEntityId = item.Id,
            SubjectDisplayName = item.Title,
            OccurredAtUtc = now,
            CreatedAtUtc = now,
            CreatedByUserId = actor.Id,
            UpdatedAtUtc = now,
            MetadataSummary = $"source=real_schedule; action={action}; startUtc={item.StartUtc:o}"
        };
        InsertJournalEntry(actor, entry, "schedule_linked");
    }

    private ResponseEnvelope RealScheduleDisabled(string command)
    {
        _logger.Admin($"schedule.real.disabled command={command}");
        return Error("Расписание игр пока недоступно.", ResponseStatus.Forbidden, ErrorCode.Forbidden);
    }

    private bool RealScheduleBaseEnabled() => _featureFlags.IsEnabled(nameof(RealScheduleFeatureFlags.UseRealScheduleCalendarMvp));
    private bool RealScheduleEventsEnabled() => RealScheduleBaseEnabled() && _featureFlags.IsEnabled(nameof(RealScheduleFeatureFlags.UseRealScheduleEvents));
    private bool RealSchedulePlayerEnabled() => RealScheduleBaseEnabled() && _featureFlags.IsEnabled(nameof(RealScheduleFeatureFlags.UseRealSchedulePlayerView));
    private bool RealScheduleSessionLinkEnabled() => RealScheduleBaseEnabled() && _featureFlags.IsEnabled(nameof(RealScheduleFeatureFlags.UseRealScheduleSessionLink));
    private bool EventJournalScheduleIntegrationEnabled()
        => _featureFlags.IsEnabled(nameof(EventJournalFeatureFlags.UseEventJournalMvp))
           && _featureFlags.IsEnabled(nameof(EventJournalFeatureFlags.UseEventJournalAutomaticIngestion))
           && _featureFlags.IsEnabled(nameof(EventJournalFeatureFlags.UseEventJournalScheduleIntegration));

    private static string RequireRealScheduleEventType(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized)) return RealScheduleEventTypeIds.GameSession;
        return normalized switch
        {
            RealScheduleEventTypeIds.GameSession => RealScheduleEventTypeIds.GameSession,
            RealScheduleEventTypeIds.CampaignSession => RealScheduleEventTypeIds.CampaignSession,
            RealScheduleEventTypeIds.OneShot => RealScheduleEventTypeIds.OneShot,
            RealScheduleEventTypeIds.Preparation => RealScheduleEventTypeIds.Preparation,
            RealScheduleEventTypeIds.Maintenance => RealScheduleEventTypeIds.Maintenance,
            RealScheduleEventTypeIds.TechnicalWork => RealScheduleEventTypeIds.TechnicalWork,
            RealScheduleEventTypeIds.Meeting => RealScheduleEventTypeIds.Meeting,
            RealScheduleEventTypeIds.Announcement => RealScheduleEventTypeIds.Announcement,
            RealScheduleEventTypeIds.Custom => RealScheduleEventTypeIds.Custom,
            _ => throw new InvalidOperationException($"Invalid real schedule event type: {value}.")
        };
    }

    private static string RequireRealScheduleStatus(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized)) return RealScheduleEventStatusIds.Planned;
        return normalized switch
        {
            RealScheduleEventStatusIds.Planned => RealScheduleEventStatusIds.Planned,
            RealScheduleEventStatusIds.Confirmed => RealScheduleEventStatusIds.Confirmed,
            RealScheduleEventStatusIds.Rescheduled => RealScheduleEventStatusIds.Rescheduled,
            RealScheduleEventStatusIds.InProgress => RealScheduleEventStatusIds.InProgress,
            RealScheduleEventStatusIds.Completed => RealScheduleEventStatusIds.Completed,
            RealScheduleEventStatusIds.Cancelled => RealScheduleEventStatusIds.Cancelled,
            RealScheduleEventStatusIds.Archived => RealScheduleEventStatusIds.Archived,
            _ => throw new InvalidOperationException($"Invalid real schedule status: {value}.")
        };
    }

    private static string RequireRealScheduleVisibility(string? value, bool isPlayerVisibleFallback)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized)) return isPlayerVisibleFallback ? RealScheduleVisibilityModeIds.PlayerVisible : RealScheduleVisibilityModeIds.GmOnly;
        return normalized switch
        {
            RealScheduleVisibilityModeIds.PlayerVisible => RealScheduleVisibilityModeIds.PlayerVisible,
            RealScheduleVisibilityModeIds.GmOnly => RealScheduleVisibilityModeIds.GmOnly,
            RealScheduleVisibilityModeIds.AdminOnly => RealScheduleVisibilityModeIds.AdminOnly,
            RealScheduleVisibilityModeIds.ServerOnly => RealScheduleVisibilityModeIds.ServerOnly,
            _ => throw new InvalidOperationException($"Invalid real schedule visibility mode: {value}.")
        };
    }

    private static bool IsRealScheduleVisibleToPlayers(string visibility)
        => string.Equals(visibility, RealScheduleVisibilityModeIds.PlayerVisible, StringComparison.OrdinalIgnoreCase);

    private static string NormalizeParticipantRole(string? value) => (value ?? string.Empty).Trim() switch
    {
        RealScheduleParticipantRoleIds.Gm => RealScheduleParticipantRoleIds.Gm,
        RealScheduleParticipantRoleIds.Observer => RealScheduleParticipantRoleIds.Observer,
        RealScheduleParticipantRoleIds.Assistant => RealScheduleParticipantRoleIds.Assistant,
        RealScheduleParticipantRoleIds.Organizer => RealScheduleParticipantRoleIds.Organizer,
        RealScheduleParticipantRoleIds.Custom => RealScheduleParticipantRoleIds.Custom,
        _ => RealScheduleParticipantRoleIds.Player
    };

    private static string NormalizeParticipantResponse(string? value) => (value ?? string.Empty).Trim() switch
    {
        RealScheduleParticipantResponseIds.Invited => RealScheduleParticipantResponseIds.Invited,
        RealScheduleParticipantResponseIds.Accepted => RealScheduleParticipantResponseIds.Accepted,
        RealScheduleParticipantResponseIds.Tentative => RealScheduleParticipantResponseIds.Tentative,
        RealScheduleParticipantResponseIds.Declined => RealScheduleParticipantResponseIds.Declined,
        _ => RealScheduleParticipantResponseIds.Unknown
    };

    private static string RealScheduleEventTypeDisplay(string type) => type switch
    {
        RealScheduleEventTypeIds.CampaignSession => "Сессия кампании",
        RealScheduleEventTypeIds.OneShot => "One-shot",
        RealScheduleEventTypeIds.Preparation => "Подготовка",
        RealScheduleEventTypeIds.Maintenance => "Техработы",
        RealScheduleEventTypeIds.TechnicalWork => "Техническое событие",
        RealScheduleEventTypeIds.Meeting => "Встреча",
        RealScheduleEventTypeIds.Announcement => "Объявление",
        RealScheduleEventTypeIds.Custom => "Другое",
        _ => "Игра"
    };

    private static string RealScheduleStatusDisplay(string status) => status switch
    {
        RealScheduleEventStatusIds.Confirmed => "Подтверждено",
        RealScheduleEventStatusIds.Rescheduled => "Перенесено",
        RealScheduleEventStatusIds.InProgress => "Идёт сейчас",
        RealScheduleEventStatusIds.Completed => "Завершено",
        RealScheduleEventStatusIds.Cancelled => "Отменено",
        RealScheduleEventStatusIds.Archived => "Архив",
        _ => "Запланировано"
    };

    private static string ParticipantRoleDisplay(string role) => role switch
    {
        RealScheduleParticipantRoleIds.Gm => "GM",
        RealScheduleParticipantRoleIds.Observer => "Наблюдатель",
        RealScheduleParticipantRoleIds.Assistant => "Ассистент",
        RealScheduleParticipantRoleIds.Organizer => "Организатор",
        RealScheduleParticipantRoleIds.Custom => "Другое",
        _ => "Игрок"
    };

    private static string ParticipantResponseDisplay(string status) => status switch
    {
        RealScheduleParticipantResponseIds.Invited => "Приглашён",
        RealScheduleParticipantResponseIds.Accepted => "Принял",
        RealScheduleParticipantResponseIds.Tentative => "Под вопросом",
        RealScheduleParticipantResponseIds.Declined => "Отклонил",
        _ => "Неизвестно"
    };
}
