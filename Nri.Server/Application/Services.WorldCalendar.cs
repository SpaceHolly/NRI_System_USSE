using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Driver;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;
using Nri.Shared.Utilities;

namespace Nri.Server.Application;

public partial class ServiceHub
{
    private const int WorldCalendarGmFutureWindowDays = 7;

    public ResponseEnvelope WorldCalendarDefaultEnsure(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!WorldCalendarBaseEnabled()) return WorldCalendarDisabled(context.Request.Command);

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var campaignId = RequireLength(PayloadReader.GetString(payload, "campaignId") ?? "default", 1, 128, "campaignId");
        var ruleSetId = RequireLength(PayloadReader.GetString(payload, "ruleSetId") ?? string.Empty, 0, 128, "ruleSetId");
        var calendar = FindActiveWorldCalendar(campaignId) ?? CreateDefaultWorldCalendar(campaignId, ruleSetId, actor.Id);
        EnsureCanonicalWorldCalendarTemplate(calendar, actor.Id);
        var worldTime = EnsureWorldTime(campaignId, calendar, actor.Id);
        _logger.Admin($"world.calendar.default.ensure campaignId={campaignId} calendarId={calendar.Id} actor={actor.Login}");
        return Ok("World calendar is ready.", WorldCalendarEnvelope(calendar, worldTime, actor, includeAdminFields: true));
    }

    public ResponseEnvelope WorldCalendarDefinitionGet(CommandContext context)
    {
        RequireAdmin(context);
        if (!WorldCalendarBaseEnabled()) return WorldCalendarDisabled(context.Request.Command);
        var calendar = RequireWorldCalendar(context);
        var worldTime = EnsureWorldTime(calendar.CampaignId, calendar, string.Empty);
        return Ok("World calendar definition loaded.", WorldCalendarEnvelope(calendar, worldTime, GetCurrentAccount(context), includeAdminFields: true));
    }

    public ResponseEnvelope WorldCalendarCurrentGet(CommandContext context)
    {
        RequireAdmin(context);
        if (!WorldCalendarCurrentEnabled()) return WorldCalendarDisabled(context.Request.Command);
        var calendar = RequireWorldCalendar(context);
        var worldTime = EnsureWorldTime(calendar.CampaignId, calendar, string.Empty);
        return Ok("World calendar current date loaded.", WorldCalendarEnvelope(calendar, worldTime, GetCurrentAccount(context), includeAdminFields: true));
    }

    public ResponseEnvelope WorldCalendarCurrentSet(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!WorldCalendarCurrentEnabled()) return WorldCalendarDisabled(context.Request.Command);
        var calendar = RequireWorldCalendar(context);
        var worldTime = EnsureWorldTime(calendar.CampaignId, calendar, actor.Id);
        worldTime.CurrentDateTime = ReadWorldDateTime(context.Request.Payload ?? new Dictionary<string, object>(), calendar, worldTime.CurrentDateTime);
        worldTime.LastAdvancedAtUtc = DateTime.UtcNow;
        worldTime.LastAdvancedByUserId = actor.Id;
        worldTime.LastAdvanceReason = RequireLength(PayloadReader.GetString(context.Request.Payload, "reason"), 0, 512, "reason");
        TouchWorldTime(worldTime);
        _repositories.CampaignWorldTimes.Replace(worldTime);
        Weather0217ReconcileCampaign(calendar.CampaignId, "world_time_set", actor.Id, context.Request.RequestId ?? string.Empty);
        SyncCurrentSessionWorldDate(calendar.CampaignId, WorldCalendarMath.Format(worldTime.CurrentDateTime, calendar));
        WriteAudit("world_calendar", actor.Id, "calendar.date.changed", worldTime.Id);
        _logger.Admin($"world.calendar.current.set.done campaignId={calendar.CampaignId} date={worldTime.CurrentDateTime.AbsoluteDayIndex}");
        return Ok("World date updated.", WorldCalendarEnvelope(calendar, worldTime, actor, includeAdminFields: true));
    }

    public ResponseEnvelope WorldCalendarCurrentAdvance(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!WorldCalendarCurrentEnabled()) return WorldCalendarDisabled(context.Request.Command);
        var calendar = RequireWorldCalendar(context);
        var worldTime = EnsureWorldTime(calendar.CampaignId, calendar, actor.Id);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var days = PayloadReader.GetInt(payload, "days") ?? 0;
        var hours = PayloadReader.GetInt(payload, "hours") ?? 0;
        var minutes = PayloadReader.GetInt(payload, "minutes") ?? 0;
        var seconds = PayloadReader.GetInt(payload, "seconds") ?? 0;
        var delta = ((long)days * calendar.HoursPerDay * calendar.MinutesPerHour * calendar.SecondsPerMinute)
            + (hours * calendar.MinutesPerHour * calendar.SecondsPerMinute)
            + (minutes * calendar.SecondsPerMinute)
            + seconds;
        if (delta < 0) return Error("World time cannot be advanced by a negative value.", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);
        worldTime.CurrentDateTime = WorldCalendarMath.FromAbsoluteSeconds(calendar.Id, worldTime.CurrentDateTime.AbsoluteSecondIndex + delta, calendar.HoursPerDay, calendar.MinutesPerHour, calendar.SecondsPerMinute);
        worldTime.LastAdvancedAtUtc = DateTime.UtcNow;
        worldTime.LastAdvancedByUserId = actor.Id;
        worldTime.LastAdvanceReason = RequireLength(PayloadReader.GetString(payload, "reason"), 0, 512, "reason");
        TouchWorldTime(worldTime);
        _repositories.CampaignWorldTimes.Replace(worldTime);
        Weather0217ReconcileCampaign(calendar.CampaignId, "world_time_advance", actor.Id, context.Request.RequestId ?? string.Empty);
        SyncCurrentSessionWorldDate(calendar.CampaignId, WorldCalendarMath.Format(worldTime.CurrentDateTime, calendar));
        WriteAudit("world_calendar", actor.Id, "calendar.time.advanced", worldTime.Id);
        _logger.Admin($"world.calendar.current.advance.done campaignId={calendar.CampaignId} deltaSeconds={delta}");
        return Ok("World time advanced.", WorldCalendarEnvelope(calendar, worldTime, actor, includeAdminFields: true));
    }

    public ResponseEnvelope WorldCalendarEventList(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!WorldCalendarChronicleEnabled()) return WorldCalendarDisabled(context.Request.Command);
        var calendar = RequireWorldCalendar(context);
        var worldTime = EnsureWorldTime(calendar.CampaignId, calendar, actor.Id);
        var events = LoadWorldEvents(calendar, includeArchived: PayloadReader.GetBool(context.Request.Payload ?? new Dictionary<string, object>(), "includeArchived"))
            .Where(e => CanAdminSeeWorldCalendarEvent(actor, e, worldTime))
            .OrderBy(e => e.StartWorldDateTime.AbsoluteDayIndex)
            .Take(400)
            .Select(e => (object)WorldCalendarEventPayload(e, calendar, actor, includeAdminFields: true))
            .ToArray();
        return Ok("World calendar events loaded.", new Dictionary<string, object> { { "items", events }, { "current", WorldTimePayload(worldTime, calendar) } });
    }

    public ResponseEnvelope WorldCalendarEventCreate(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!WorldCalendarEventsEnabled()) return WorldCalendarDisabled(context.Request.Command);
        var calendar = RequireWorldCalendar(context);
        var worldTime = EnsureWorldTime(calendar.CampaignId, calendar, actor.Id);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var title = RequireLength(PayloadReader.GetString(payload, "title") ?? string.Empty, 2, 180, "title");
        var start = ReadWorldDateTime(payload, calendar, worldTime.CurrentDateTime);
        var now = DateTime.UtcNow;
        var item = new WorldCalendarEventState
        {
            CampaignId = calendar.CampaignId,
            CalendarId = calendar.Id,
            Title = title,
            Description = RequireLength(PayloadReader.GetString(payload, "description"), 0, 4096, "description"),
            EventType = RequireWorldEventType(PayloadReader.GetString(payload, "eventType")),
            Status = RequireWorldEventStatus(PayloadReader.GetString(payload, "status")),
            StartWorldDateTime = start,
            IsFutureEvent = start.AbsoluteDayIndex > worldTime.CurrentDateTime.AbsoluteDayIndex,
            IsPlayerVisible = PayloadReader.GetBool(payload, "isPlayerVisible"),
            VisibilityMode = RequireWorldCalendarVisibility(PayloadReader.GetString(payload, "visibilityMode")),
            RevealPolicy = RequireRevealPolicy(PayloadReader.GetString(payload, "revealPolicy")),
            AuthorUserId = actor.Id,
            AuthorDisplayName = FirstNonEmpty(actor.Login, actor.Id),
            LinkedSessionId = RequireLength(PayloadReader.GetString(payload, "linkedSessionId"), 0, 128, "linkedSessionId"),
            LinkedGroupId = RequireLength(PayloadReader.GetString(payload, "linkedGroupId"), 0, 128, "linkedGroupId"),
            LinkedCharacterId = RequireLength(PayloadReader.GetString(payload, "linkedCharacterId"), 0, 128, "linkedCharacterId"),
            LinkedLocationId = RequireLength(PayloadReader.GetString(payload, "linkedLocationId"), 0, 128, "linkedLocationId"),
            LinkedSpaceNodeId = RequireLength(PayloadReader.GetString(payload, "linkedSpaceNodeId"), 0, 128, "linkedSpaceNodeId"),
            LinkedMapId = RequireLength(PayloadReader.GetString(payload, "linkedMapId"), 0, 128, "linkedMapId"),
            LinkedRoomId = RequireLength(PayloadReader.GetString(payload, "linkedRoomId"), 0, 128, "linkedRoomId"),
            LinkedRealScheduleEventId = RequireLength(PayloadReader.GetString(payload, "linkedRealScheduleEventId"), 0, 128, "linkedRealScheduleEventId"),
            PublicSummary = RequireLength(PayloadReader.GetString(payload, "publicSummary"), 0, 2048, "publicSummary"),
            GMSummary = RequireLength(PayloadReader.GetString(payload, "gmSummary"), 0, 2048, "gmSummary"),
            ConditionsSummary = RequireLength(PayloadReader.GetString(payload, "conditionsSummary"), 0, 2048, "conditionsSummary"),
            ReminderEnabled = PayloadReader.GetBool(payload, "reminderEnabled"),
            ReminderAtWorldDateTime = PayloadReader.GetBool(payload, "reminderEnabled") ? ReadWorldDateTime(payload, calendar, start, "reminder") : null,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        _repositories.WorldCalendarEvents.Insert(item);
        AddDefaultWorldEventVersion(item, actor, item.PublicSummary);
        if (item.ReminderEnabled)
            CreateReminderForEvent(item, actor, item.ReminderAtWorldDateTime ?? item.StartWorldDateTime, "Авто-напоминание события.");
        WriteAudit("world_calendar", actor.Id, "chronicle.event.created", item.Id);
        WriteCalendarJournalEntry(actor, item, calendar, "created");
        _logger.Admin($"world.calendar.event.create eventId={item.Id} title={item.Title}");
        return Ok("World calendar event created.", new Dictionary<string, object> { { "item", WorldCalendarEventPayload(item, calendar, actor, includeAdminFields: true) } });
    }

    public ResponseEnvelope WorldCalendarEventUpdate(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!WorldCalendarEventsEnabled()) return WorldCalendarDisabled(context.Request.Command);
        var item = RequireWorldEvent(context);
        var calendar = RequireCalendarById(item.CalendarId);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        if (payload.ContainsKey("title")) item.Title = RequireLength(PayloadReader.GetString(payload, "title"), 2, 180, "title");
        if (payload.ContainsKey("description")) item.Description = RequireLength(PayloadReader.GetString(payload, "description"), 0, 4096, "description");
        if (payload.ContainsKey("eventType")) item.EventType = RequireWorldEventType(PayloadReader.GetString(payload, "eventType"));
        if (payload.ContainsKey("status")) item.Status = RequireWorldEventStatus(PayloadReader.GetString(payload, "status"));
        if (payload.ContainsKey("isPlayerVisible")) item.IsPlayerVisible = PayloadReader.GetBool(payload, "isPlayerVisible");
        if (payload.ContainsKey("visibilityMode")) item.VisibilityMode = RequireWorldCalendarVisibility(PayloadReader.GetString(payload, "visibilityMode"));
        if (payload.ContainsKey("revealPolicy")) item.RevealPolicy = RequireRevealPolicy(PayloadReader.GetString(payload, "revealPolicy"));
        if (payload.ContainsKey("publicSummary")) item.PublicSummary = RequireLength(PayloadReader.GetString(payload, "publicSummary"), 0, 2048, "publicSummary");
        if (payload.ContainsKey("gmSummary")) item.GMSummary = RequireLength(PayloadReader.GetString(payload, "gmSummary"), 0, 2048, "gmSummary");
        if (payload.ContainsKey("year") || payload.ContainsKey("monthOrder") || payload.ContainsKey("dayOfMonth"))
        {
            item.StartWorldDateTime = ReadWorldDateTime(payload, calendar, item.StartWorldDateTime);
            var worldTime = EnsureWorldTime(calendar.CampaignId, calendar, actor.Id);
            item.IsFutureEvent = item.StartWorldDateTime.AbsoluteDayIndex > worldTime.CurrentDateTime.AbsoluteDayIndex;
        }
        item.UpdatedAtUtc = DateTime.UtcNow;
        _repositories.WorldCalendarEvents.Replace(item);
        WriteAudit("world_calendar", actor.Id, "chronicle.event.updated", item.Id);
        WriteCalendarJournalEntry(actor, item, calendar, "updated");
        return Ok("World calendar event updated.", new Dictionary<string, object> { { "item", WorldCalendarEventPayload(item, calendar, actor, includeAdminFields: true) } });
    }

    public ResponseEnvelope WorldCalendarEventCancel(CommandContext context)
        => SetWorldEventStatus(context, WorldCalendarEventStatusIds.Cancelled, "chronicle.event.cancelled", "World calendar event cancelled.");

    public ResponseEnvelope WorldCalendarEventArchive(CommandContext context)
        => SetWorldEventStatus(context, WorldCalendarEventStatusIds.Archived, "chronicle.event.archived", "World calendar event archived.");

    public ResponseEnvelope WorldCalendarEventVersionAdd(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!WorldCalendarChronicleEnabled()) return WorldCalendarDisabled(context.Request.Command);
        var item = RequireWorldEvent(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var version = new WorldCalendarEventVersionState
        {
            EventId = item.Id,
            CampaignId = item.CampaignId,
            CalendarId = item.CalendarId,
            VersionType = NormalizeVersionType(PayloadReader.GetString(payload, "versionType")),
            Title = RequireLength(FirstNonEmpty(PayloadReader.GetString(payload, "title"), item.Title), 1, 180, "title"),
            Summary = RequireLength(PayloadReader.GetString(payload, "summary"), 0, 2048, "summary"),
            Body = RequireLength(PayloadReader.GetString(payload, "body"), 0, 8192, "body"),
            IsPlayerVisible = PayloadReader.GetBool(payload, "isPlayerVisible"),
            VisibilityMode = RequireWorldCalendarVisibility(PayloadReader.GetString(payload, "visibilityMode")),
            RevealPolicy = RequireRevealPolicy(PayloadReader.GetString(payload, "revealPolicy")),
            AuthorUserId = actor.Id,
            AuthorDisplayName = FirstNonEmpty(actor.Login, actor.Id),
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        _repositories.WorldCalendarEventVersions.Insert(version);
        WriteAudit("world_calendar", actor.Id, "chronicle.event.version.add", version.Id);
        return Ok("World calendar event version added.", new Dictionary<string, object> { { "version", VersionPayload(version, includeAdminFields: true) } });
    }

    public ResponseEnvelope WorldCalendarHolidayList(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!WorldCalendarHolidaysEnabled()) return WorldCalendarDisabled(context.Request.Command);
        var calendar = RequireWorldCalendar(context);
        var items = _repositories.WorldCalendarHolidays.Find(Builders<WorldCalendarHolidayDefinition>.Filter.Eq(x => x.CalendarId, calendar.Id))
            .Where(x => !x.IsArchived)
            .Where(x => IsAdminLike(actor) || x.IsPlayerVisible)
            .OrderBy(x => x.MonthOrder).ThenBy(x => x.DayOfMonth)
            .Select(x => (object)HolidayPayload(x, includeAdminFields: IsAdminLike(actor)))
            .ToArray();
        return Ok("World calendar holidays loaded.", new Dictionary<string, object> { { "items", items } });
    }

    public ResponseEnvelope WorldCalendarHolidayCreate(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!WorldCalendarHolidaysEnabled()) return WorldCalendarDisabled(context.Request.Command);
        var calendar = RequireWorldCalendar(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var holiday = new WorldCalendarHolidayDefinition
        {
            CalendarId = calendar.Id,
            CampaignId = calendar.CampaignId,
            Name = RequireLength(PayloadReader.GetString(payload, "name"), 2, 160, "name"),
            Description = RequireLength(PayloadReader.GetString(payload, "description"), 0, 2048, "description"),
            MonthOrder = Clamp(PayloadReader.GetInt(payload, "monthOrder") ?? 1, 1, WorldCalendarDefaults.MonthsPerYear),
            DayOfMonth = Clamp(PayloadReader.GetInt(payload, "dayOfMonth") ?? 1, 1, WorldCalendarDefaults.DaysPerMonth),
            IsPlayerVisible = !payload.ContainsKey("isPlayerVisible") || PayloadReader.GetBool(payload, "isPlayerVisible"),
            ColorKey = RequireLength(PayloadReader.GetString(payload, "colorKey"), 0, 64, "colorKey")
        };
        _repositories.WorldCalendarHolidays.Insert(holiday);
        return Ok("World calendar holiday created.", new Dictionary<string, object> { { "item", HolidayPayload(holiday, includeAdminFields: true) } });
    }

    public ResponseEnvelope WorldCalendarHolidayUpdate(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!WorldCalendarHolidaysEnabled()) return WorldCalendarDisabled(context.Request.Command);
        var holiday = RequireHoliday(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        if (payload.ContainsKey("name")) holiday.Name = RequireLength(PayloadReader.GetString(payload, "name"), 2, 160, "name");
        if (payload.ContainsKey("description")) holiday.Description = RequireLength(PayloadReader.GetString(payload, "description"), 0, 2048, "description");
        if (payload.ContainsKey("monthOrder")) holiday.MonthOrder = Clamp(PayloadReader.GetInt(payload, "monthOrder") ?? holiday.MonthOrder, 1, WorldCalendarDefaults.MonthsPerYear);
        if (payload.ContainsKey("dayOfMonth")) holiday.DayOfMonth = Clamp(PayloadReader.GetInt(payload, "dayOfMonth") ?? holiday.DayOfMonth, 1, WorldCalendarDefaults.DaysPerMonth);
        if (payload.ContainsKey("isPlayerVisible")) holiday.IsPlayerVisible = PayloadReader.GetBool(payload, "isPlayerVisible");
        _repositories.WorldCalendarHolidays.Replace(holiday);
        _logger.Admin($"world.calendar.holiday.update holidayId={holiday.Id} actor={actor.Login}");
        return Ok("World calendar holiday updated.", new Dictionary<string, object> { { "item", HolidayPayload(holiday, includeAdminFields: true) } });
    }

    public ResponseEnvelope WorldCalendarHolidayArchive(CommandContext context)
    {
        RequireAdmin(context);
        if (!WorldCalendarHolidaysEnabled()) return WorldCalendarDisabled(context.Request.Command);
        var holiday = RequireHoliday(context);
        holiday.IsArchived = true;
        _repositories.WorldCalendarHolidays.Replace(holiday);
        return Ok("World calendar holiday archived.", new Dictionary<string, object> { { "item", HolidayPayload(holiday, includeAdminFields: true) } });
    }

    public ResponseEnvelope WorldCalendarReminderList(CommandContext context)
    {
        RequireAdmin(context);
        if (!WorldCalendarRemindersEnabled()) return WorldCalendarDisabled(context.Request.Command);
        var calendar = RequireWorldCalendar(context);
        var items = _repositories.WorldCalendarReminders.Find(Builders<WorldCalendarReminderState>.Filter.Eq(x => x.CalendarId, calendar.Id))
            .Where(x => !x.IsDismissed)
            .OrderBy(x => x.ReminderAtWorldDateTime.AbsoluteDayIndex)
            .Take(200)
            .Select(x => (object)ReminderPayload(x))
            .ToArray();
        return Ok("World calendar reminders loaded.", new Dictionary<string, object> { { "items", items } });
    }

    public ResponseEnvelope WorldCalendarReminderCreate(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!WorldCalendarRemindersEnabled()) return WorldCalendarDisabled(context.Request.Command);
        var calendar = RequireWorldCalendar(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var reminder = new WorldCalendarReminderState
        {
            CampaignId = calendar.CampaignId,
            CalendarId = calendar.Id,
            EventId = RequireLength(PayloadReader.GetString(payload, "eventId"), 0, 128, "eventId"),
            Title = RequireLength(PayloadReader.GetString(payload, "title"), 2, 180, "title"),
            Notes = RequireLength(PayloadReader.GetString(payload, "notes"), 0, 2048, "notes"),
            ReminderAtWorldDateTime = ReadWorldDateTime(payload, calendar, WorldCalendarTemplate.CreateDefaultDate(calendar.Id)),
            CreatedByUserId = actor.Id,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        _repositories.WorldCalendarReminders.Insert(reminder);
        return Ok("World calendar reminder created.", new Dictionary<string, object> { { "item", ReminderPayload(reminder) } });
    }

    public ResponseEnvelope WorldCalendarReminderDismiss(CommandContext context)
    {
        RequireAdmin(context);
        if (!WorldCalendarRemindersEnabled()) return WorldCalendarDisabled(context.Request.Command);
        var reminderId = RequireLength(PayloadReader.GetString(context.Request.Payload, "reminderId"), 1, 128, "reminderId");
        var reminder = _repositories.WorldCalendarReminders.GetById(reminderId) ?? throw new InvalidOperationException("reminder not found");
        reminder.IsDismissed = true;
        reminder.DismissedAtUtc = DateTime.UtcNow;
        reminder.UpdatedAtUtc = DateTime.UtcNow;
        _repositories.WorldCalendarReminders.Replace(reminder);
        return Ok("World calendar reminder dismissed.", new Dictionary<string, object> { { "item", ReminderPayload(reminder) } });
    }

    public ResponseEnvelope WorldCalendarPlayerGet(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!WorldCalendarPlayerEnabled()) return WorldCalendarDisabled(context.Request.Command);
        var calendar = RequireWorldCalendar(context);
        var worldTime = EnsureWorldTime(calendar.CampaignId, calendar, actor.Id);
        var events = LoadWorldEvents(calendar, includeArchived: false)
            .Where(e => CanPlayerSeeWorldCalendarEvent(e, worldTime))
            .OrderBy(e => e.StartWorldDateTime.AbsoluteDayIndex)
            .Take(120)
            .Select(e => (object)WorldCalendarEventPayload(e, calendar, actor, includeAdminFields: false))
            .ToArray();
        var holidays = _repositories.WorldCalendarHolidays.Find(Builders<WorldCalendarHolidayDefinition>.Filter.Eq(x => x.CalendarId, calendar.Id))
            .Where(x => !x.IsArchived && x.IsPlayerVisible)
            .OrderBy(x => x.MonthOrder).ThenBy(x => x.DayOfMonth)
            .Select(x => (object)HolidayPayload(x, includeAdminFields: false))
            .ToArray();
        _logger.Admin($"world.calendar.player.get actor={actor.Login} events={events.Length} holidays={holidays.Length}");
        return Ok("Player world calendar loaded.", new Dictionary<string, object>
        {
            { "calendar", CalendarPayload(calendar, includeAdminFields: false) },
            { "current", WorldTimePayload(worldTime, calendar) },
            { "events", events },
            { "holidays", holidays },
            { "realSchedulePlaceholder", "Расписание игр подключено во вкладке «Расписание игр» рядом с календарём мира." }
        });
    }

    private ResponseEnvelope SetWorldEventStatus(CommandContext context, string status, string auditAction, string message)
    {
        var actor = RequireAdmin(context);
        if (!WorldCalendarEventsEnabled()) return WorldCalendarDisabled(context.Request.Command);
        var item = RequireWorldEvent(context);
        item.Status = status;
        item.IsArchived = status == WorldCalendarEventStatusIds.Archived;
        item.UpdatedAtUtc = DateTime.UtcNow;
        _repositories.WorldCalendarEvents.Replace(item);
        WriteAudit("world_calendar", actor.Id, auditAction, item.Id);
        var calendar = RequireCalendarById(item.CalendarId);
        WriteCalendarJournalEntry(actor, item, calendar, status);
        return Ok(message, new Dictionary<string, object> { { "item", WorldCalendarEventPayload(item, calendar, actor, includeAdminFields: true) } });
    }

    private WorldCalendarDefinition CreateDefaultWorldCalendar(string campaignId, string ruleSetId, string actorUserId)
    {
        var calendar = WorldCalendarTemplate.CreateDefaultCalendar(campaignId, ruleSetId, actorUserId);
        _repositories.WorldCalendarDefinitions.Insert(calendar);
        var seasons = new List<WorldCalendarSeasonDefinition>();
        for (var i = 0; i < WorldCalendarTemplate.Seasons.Length; i++)
        {
            var season = WorldCalendarTemplate.Seasons[i];
            var item = new WorldCalendarSeasonDefinition
            {
                CalendarId = calendar.Id,
                Order = i + 1,
                Name = season.Name,
                Description = season.Description,
                StartMonthOrder = season.StartMonth,
                MonthCount = 3,
                ColorKey = season.ColorKey
            };
            _repositories.WorldCalendarSeasons.Insert(item);
            seasons.Add(item);
        }
        for (var i = 0; i < WorldCalendarTemplate.Months.Length; i++)
        {
            var month = WorldCalendarTemplate.Months[i];
            var season = seasons.ElementAtOrDefault(i / 3);
            _repositories.WorldCalendarMonths.Insert(new WorldCalendarMonthDefinition
            {
                CalendarId = calendar.Id,
                Order = i + 1,
                Name = month.Name,
                ShortName = month.Name,
                Description = month.Description,
                DaysInMonth = WorldCalendarDefaults.DaysPerMonth,
                SeasonId = season?.Id ?? string.Empty,
                ColorKey = season?.ColorKey ?? string.Empty
            });
        }
        return calendar;
    }

    private void EnsureCanonicalWorldCalendarTemplate(WorldCalendarDefinition calendar, string actorUserId)
    {
        var changed = false;
        if (!string.Equals(calendar.Name, WorldCalendarDefaults.DefaultCalendarName, StringComparison.Ordinal))
        {
            calendar.Name = WorldCalendarDefaults.DefaultCalendarName;
            changed = true;
        }
        if (!string.Equals(calendar.EraName, WorldCalendarDefaults.EraName, StringComparison.Ordinal))
        {
            calendar.EraName = WorldCalendarDefaults.EraName;
            changed = true;
        }
        if (!string.Equals(calendar.EraShortName, WorldCalendarDefaults.EraShortName, StringComparison.Ordinal))
        {
            calendar.EraShortName = WorldCalendarDefaults.EraShortName;
            changed = true;
        }
        if (!string.Equals(calendar.BeforeEraName, WorldCalendarDefaults.BeforeEraName, StringComparison.Ordinal))
        {
            calendar.BeforeEraName = WorldCalendarDefaults.BeforeEraName;
            changed = true;
        }
        if (!string.Equals(calendar.BeforeEraShortName, WorldCalendarDefaults.BeforeEraShortName, StringComparison.Ordinal))
        {
            calendar.BeforeEraShortName = WorldCalendarDefaults.BeforeEraShortName;
            changed = true;
        }
        if (calendar.DaysPerYear != WorldCalendarDefaults.DaysPerYear
            || calendar.MonthsPerYear != WorldCalendarDefaults.MonthsPerYear
            || calendar.DaysPerWeek != WorldCalendarDefaults.DaysPerWeek
            || calendar.HoursPerDay != WorldCalendarDefaults.HoursPerDay
            || calendar.MinutesPerHour != WorldCalendarDefaults.MinutesPerHour)
        {
            calendar.DaysPerWeek = WorldCalendarDefaults.DaysPerWeek;
            calendar.MonthsPerYear = WorldCalendarDefaults.MonthsPerYear;
            calendar.DaysPerYear = WorldCalendarDefaults.DaysPerYear;
            calendar.HoursPerDay = WorldCalendarDefaults.HoursPerDay;
            calendar.MinutesPerHour = WorldCalendarDefaults.MinutesPerHour;
            calendar.SecondsPerMinute = WorldCalendarDefaults.SecondsPerMinute;
            changed = true;
        }
        if (changed)
        {
            calendar.UpdatedAtUtc = DateTime.UtcNow;
            calendar.UpdatedByUserId = actorUserId ?? string.Empty;
            _repositories.WorldCalendarDefinitions.Replace(calendar);
        }

        var seasons = _repositories.WorldCalendarSeasons.Find(Builders<WorldCalendarSeasonDefinition>.Filter.Eq(x => x.CalendarId, calendar.Id)).ToList();
        for (var i = 0; i < WorldCalendarTemplate.Seasons.Length; i++)
        {
            var template = WorldCalendarTemplate.Seasons[i];
            var order = i + 1;
            var item = seasons.FirstOrDefault(x => x.Order == order);
            var isNew = item == null;
            if (item == null)
            {
                item = new WorldCalendarSeasonDefinition { CalendarId = calendar.Id, Order = order };
                seasons.Add(item);
            }
            item.Name = template.Name;
            item.Description = template.Description;
            item.StartMonthOrder = template.StartMonth;
            item.MonthCount = 3;
            item.ColorKey = template.ColorKey;
            if (isNew) _repositories.WorldCalendarSeasons.Insert(item);
            else _repositories.WorldCalendarSeasons.Replace(item);
        }

        var refreshedSeasons = _repositories.WorldCalendarSeasons.Find(Builders<WorldCalendarSeasonDefinition>.Filter.Eq(x => x.CalendarId, calendar.Id)).ToList();
        var months = _repositories.WorldCalendarMonths.Find(Builders<WorldCalendarMonthDefinition>.Filter.Eq(x => x.CalendarId, calendar.Id)).ToList();
        for (var i = 0; i < WorldCalendarTemplate.Months.Length; i++)
        {
            var template = WorldCalendarTemplate.Months[i];
            var order = i + 1;
            var season = refreshedSeasons.FirstOrDefault(x => x.Order == (i / 3) + 1);
            var item = months.FirstOrDefault(x => x.Order == order);
            var isNew = item == null;
            if (item == null)
            {
                item = new WorldCalendarMonthDefinition { CalendarId = calendar.Id, Order = order };
                months.Add(item);
            }
            item.Name = template.Name;
            item.ShortName = template.Name;
            item.Description = template.Description;
            item.DaysInMonth = WorldCalendarDefaults.DaysPerMonth;
            item.SeasonId = season?.Id ?? string.Empty;
            item.ColorKey = season?.ColorKey ?? string.Empty;
            if (isNew) _repositories.WorldCalendarMonths.Insert(item);
            else _repositories.WorldCalendarMonths.Replace(item);
        }
    }

    private CampaignWorldTimeState EnsureWorldTime(string campaignId, WorldCalendarDefinition calendar, string actorUserId)
    {
        var existing = _repositories.CampaignWorldTimes.Find(Builders<CampaignWorldTimeState>.Filter.Eq(x => x.CampaignId, campaignId))
            .Where(x => !x.Deleted && !x.Archived)
            .OrderByDescending(x => x.UpdatedAtUtc)
            .FirstOrDefault();
        if (existing != null) return existing;
        var now = DateTime.UtcNow;
        var state = new CampaignWorldTimeState
        {
            CampaignId = campaignId,
            CalendarId = calendar.Id,
            CurrentDateTime = WorldCalendarTemplate.CreateDefaultDate(calendar.Id),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            LastAdvancedAtUtc = now,
            LastAdvancedByUserId = actorUserId ?? string.Empty,
            Revision = 1
        };
        _repositories.CampaignWorldTimes.Insert(state);
        return state;
    }

    private WorldCalendarDefinition RequireWorldCalendar(CommandContext context)
    {
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var calendarId = PayloadReader.GetString(payload, "calendarId");
        if (!string.IsNullOrWhiteSpace(calendarId)) return RequireCalendarById(calendarId);
        var campaignId = RequireLength(PayloadReader.GetString(payload, "campaignId") ?? "default", 1, 128, "campaignId");
        var calendar = FindActiveWorldCalendar(campaignId);
        if (calendar == null) throw new InvalidOperationException("world calendar not found");
        return calendar;
    }

    private WorldCalendarDefinition RequireCalendarById(string calendarId)
    {
        var calendar = _repositories.WorldCalendarDefinitions.GetById(calendarId ?? string.Empty);
        if (calendar == null || calendar.Deleted || calendar.Archived || calendar.IsArchived) throw new InvalidOperationException("world calendar not found");
        return calendar;
    }

    private WorldCalendarDefinition? FindActiveWorldCalendar(string campaignId)
        => _repositories.WorldCalendarDefinitions.Find(Builders<WorldCalendarDefinition>.Filter.Eq(x => x.CampaignId, campaignId ?? string.Empty))
            .Where(x => !x.IsArchived && !x.Deleted && !x.Archived)
            .OrderByDescending(x => x.IsActive)
            .ThenByDescending(x => x.IsDefault)
            .ThenByDescending(x => x.UpdatedAtUtc)
            .FirstOrDefault();

    private WorldCalendarEventState RequireWorldEvent(CommandContext context)
    {
        var eventId = RequireLength(PayloadReader.GetString(context.Request.Payload, "eventId"), 1, 128, "eventId");
        var item = _repositories.WorldCalendarEvents.GetById(eventId);
        if (item == null || item.Deleted || item.Archived) throw new InvalidOperationException("world calendar event not found");
        return item;
    }

    private WorldCalendarHolidayDefinition RequireHoliday(CommandContext context)
    {
        var holidayId = RequireLength(PayloadReader.GetString(context.Request.Payload, "holidayId"), 1, 128, "holidayId");
        var item = _repositories.WorldCalendarHolidays.GetById(holidayId);
        if (item == null || item.Deleted || item.Archived) throw new InvalidOperationException("world calendar holiday not found");
        return item;
    }

    private IReadOnlyCollection<WorldCalendarEventState> LoadWorldEvents(WorldCalendarDefinition calendar, bool includeArchived)
    {
        var filter = Builders<WorldCalendarEventState>.Filter.Eq(x => x.CalendarId, calendar.Id);
        if (!includeArchived) filter &= Builders<WorldCalendarEventState>.Filter.Ne(x => x.Status, WorldCalendarEventStatusIds.Archived);
        return _repositories.WorldCalendarEvents.Find(filter);
    }

    private WorldDateTimeValue ReadWorldDateTime(Dictionary<string, object> payload, WorldCalendarDefinition calendar, WorldDateTimeValue fallback, string prefix = "")
    {
        string Key(string name) => string.IsNullOrWhiteSpace(prefix) ? name : prefix + char.ToUpperInvariant(name[0]) + name.Substring(1);
        var year = PayloadReader.GetInt(payload, Key("year")) ?? fallback.Year;
        var era = PayloadReader.GetString(payload, Key("era"));
        if (!string.IsNullOrWhiteSpace(era))
            year = WorldCalendarMath.ToSignedYear(year, era);
        var value = new WorldDateTimeValue
        {
            CalendarId = calendar.Id,
            Year = year,
            MonthOrder = PayloadReader.GetInt(payload, Key("monthOrder")) ?? fallback.MonthOrder,
            DayOfMonth = PayloadReader.GetInt(payload, Key("dayOfMonth")) ?? fallback.DayOfMonth,
            Hour = PayloadReader.GetInt(payload, Key("hour")) ?? fallback.Hour,
            Minute = PayloadReader.GetInt(payload, Key("minute")) ?? fallback.Minute,
            Second = PayloadReader.GetInt(payload, Key("second")) ?? fallback.Second
        };
        ValidateWorldDateTime(value, calendar, prefix);
        return WorldCalendarMath.Normalize(value, calendar.HoursPerDay, calendar.MinutesPerHour, calendar.SecondsPerMinute);
    }

    private static void ValidateWorldDateTime(WorldDateTimeValue value, WorldCalendarDefinition calendar, string prefix)
    {
        var label = string.IsNullOrWhiteSpace(prefix) ? "world date" : prefix + " world date";
        if (value.MonthOrder < 1 || value.MonthOrder > WorldCalendarDefaults.MonthsPerYear)
            throw new InvalidOperationException($"{label}: month must be between 1 and {WorldCalendarDefaults.MonthsPerYear}.");
        if (value.DayOfMonth < 1 || value.DayOfMonth > WorldCalendarDefaults.DaysPerMonth)
            throw new InvalidOperationException($"{label}: day must be between 1 and {WorldCalendarDefaults.DaysPerMonth}.");
        if (value.Hour < 0 || value.Hour >= Math.Max(1, calendar.HoursPerDay))
            throw new InvalidOperationException($"{label}: hour must be between 0 and {Math.Max(1, calendar.HoursPerDay) - 1}.");
        if (value.Minute < 0 || value.Minute >= Math.Max(1, calendar.MinutesPerHour))
            throw new InvalidOperationException($"{label}: minute must be between 0 and {Math.Max(1, calendar.MinutesPerHour) - 1}.");
        if (value.Second < 0 || value.Second >= Math.Max(1, calendar.SecondsPerMinute))
            throw new InvalidOperationException($"{label}: second must be between 0 and {Math.Max(1, calendar.SecondsPerMinute) - 1}.");
    }

    private bool CanAdminSeeWorldCalendarEvent(UserAccount actor, WorldCalendarEventState item, CampaignWorldTimeState worldTime)
    {
        if (actor.Roles.Contains(UserRole.SuperAdmin)) return true;
        if (string.Equals(item.VisibilityMode, WorldCalendarVisibilityModeIds.SuperAdminOnly, StringComparison.OrdinalIgnoreCase)) return false;
        if (string.Equals(item.VisibilityMode, WorldCalendarVisibilityModeIds.ServerOnly, StringComparison.OrdinalIgnoreCase)) return false;
        if (string.Equals(item.AuthorUserId, actor.Id, StringComparison.OrdinalIgnoreCase)) return true;
        if (!WorldCalendarFutureVisibilityEnabled()) return true;
        if (!item.IsFutureEvent) return true;
        return item.StartWorldDateTime.AbsoluteDayIndex <= worldTime.CurrentDateTime.AbsoluteDayIndex + WorldCalendarGmFutureWindowDays;
    }

    private bool CanPlayerSeeWorldCalendarEvent(WorldCalendarEventState item, CampaignWorldTimeState worldTime)
    {
        if (!item.IsPlayerVisible) return false;
        if (!string.Equals(item.VisibilityMode, WorldCalendarVisibilityModeIds.PlayerVisible, StringComparison.OrdinalIgnoreCase)) return false;
        if (string.Equals(item.RevealPolicy, WorldCalendarRevealPolicyIds.Hidden, StringComparison.OrdinalIgnoreCase)) return false;
        if (item.IsFutureEvent && !string.Equals(item.RevealPolicy, WorldCalendarRevealPolicyIds.Immediate, StringComparison.OrdinalIgnoreCase))
        {
            if (item.RevealAtWorldDateTime == null) return false;
            return item.RevealAtWorldDateTime.AbsoluteDayIndex <= worldTime.CurrentDateTime.AbsoluteDayIndex;
        }
        return true;
    }

    private bool CanPlayerSeeVersion(WorldCalendarEventVersionState version, CampaignWorldTimeState worldTime)
    {
        if (!version.IsPlayerVisible) return false;
        if (string.Equals(version.VersionType, WorldCalendarVersionTypeIds.Truth, StringComparison.OrdinalIgnoreCase)) return false;
        if (string.Equals(version.VersionType, WorldCalendarVersionTypeIds.GmOnly, StringComparison.OrdinalIgnoreCase)) return false;
        if (!string.Equals(version.VisibilityMode, WorldCalendarVisibilityModeIds.PlayerVisible, StringComparison.OrdinalIgnoreCase)) return false;
        if (string.Equals(version.RevealPolicy, WorldCalendarRevealPolicyIds.Hidden, StringComparison.OrdinalIgnoreCase)) return false;
        if (version.RevealAtWorldDateTime != null && version.RevealAtWorldDateTime.AbsoluteDayIndex > worldTime.CurrentDateTime.AbsoluteDayIndex) return false;
        return true;
    }

    private Dictionary<string, object> WorldCalendarEnvelope(WorldCalendarDefinition calendar, CampaignWorldTimeState worldTime, UserAccount actor, bool includeAdminFields)
    {
        return new Dictionary<string, object>
        {
            { "calendar", CalendarPayload(calendar, includeAdminFields) },
            { "current", WorldTimePayload(worldTime, calendar) },
            { "seasons", _repositories.WorldCalendarSeasons.Find(Builders<WorldCalendarSeasonDefinition>.Filter.Eq(x => x.CalendarId, calendar.Id)).OrderBy(x => x.Order).Select(x => (object)SeasonPayload(x)).ToArray() },
            { "months", _repositories.WorldCalendarMonths.Find(Builders<WorldCalendarMonthDefinition>.Filter.Eq(x => x.CalendarId, calendar.Id)).OrderBy(x => x.Order).Select(x => (object)MonthPayload(x)).ToArray() },
            { "events", LoadWorldEvents(calendar, false).Where(e => includeAdminFields ? CanAdminSeeWorldCalendarEvent(actor, e, worldTime) : CanPlayerSeeWorldCalendarEvent(e, worldTime)).OrderBy(x => x.StartWorldDateTime.AbsoluteDayIndex).Take(200).Select(x => (object)WorldCalendarEventPayload(x, calendar, actor, includeAdminFields)).ToArray() },
            { "holidays", _repositories.WorldCalendarHolidays.Find(Builders<WorldCalendarHolidayDefinition>.Filter.Eq(x => x.CalendarId, calendar.Id)).Where(x => !x.IsArchived && (includeAdminFields || x.IsPlayerVisible)).OrderBy(x => x.MonthOrder).ThenBy(x => x.DayOfMonth).Select(x => (object)HolidayPayload(x, includeAdminFields)).ToArray() },
            { "reminders", includeAdminFields ? _repositories.WorldCalendarReminders.Find(Builders<WorldCalendarReminderState>.Filter.Eq(x => x.CalendarId, calendar.Id)).Where(x => !x.IsDismissed).OrderBy(x => x.ReminderAtWorldDateTime.AbsoluteDayIndex).Take(100).Select(x => (object)ReminderPayload(x)).ToArray() : Array.Empty<object>() },
            { "realSchedulePlaceholder", "Расписание игр подключено во вкладке «Расписание игр» рядом с календарём мира." }
        };
    }

    private Dictionary<string, object> CalendarPayload(WorldCalendarDefinition calendar, bool includeAdminFields)
    {
        var result = new Dictionary<string, object>
        {
            { "calendarId", calendar.Id },
            { "campaignId", calendar.CampaignId },
            { "ruleSetId", calendar.RuleSetId },
            { "name", calendar.Name },
            { "description", calendar.Description },
            { "daysPerWeek", calendar.DaysPerWeek },
            { "monthsPerYear", calendar.MonthsPerYear },
            { "daysPerYear", calendar.DaysPerYear },
            { "hoursPerDay", calendar.HoursPerDay },
            { "minutesPerHour", calendar.MinutesPerHour },
            { "secondsPerMinute", calendar.SecondsPerMinute },
            { "eraName", calendar.EraName },
            { "eraShortName", calendar.EraShortName },
            { "beforeEraName", calendar.BeforeEraName },
            { "beforeEraShortName", calendar.BeforeEraShortName },
            { "yearZeroDescription", calendar.YearZeroDescription },
            { "isDefault", calendar.IsDefault },
            { "isActive", calendar.IsActive }
        };
        if (includeAdminFields) result["updatedAtUtc"] = calendar.UpdatedAtUtc;
        return result;
    }

    private static Dictionary<string, object> WorldTimePayload(CampaignWorldTimeState worldTime, WorldCalendarDefinition calendar)
    {
        return new Dictionary<string, object>
        {
            { "stateId", worldTime.Id },
            { "campaignId", worldTime.CampaignId },
            { "calendarId", worldTime.CalendarId },
            { "dateTime", DatePayload(worldTime.CurrentDateTime, calendar) },
            { "display", WorldCalendarMath.Format(worldTime.CurrentDateTime, calendar) },
            { "season", WorldCalendarMath.SeasonName(worldTime.CurrentDateTime) },
            { "weekDay", WorldCalendarMath.WeekDayName(worldTime.CurrentDateTime) },
            { "isPaused", worldTime.IsPaused },
            { "revision", worldTime.Revision },
            { "lastAdvanceReason", worldTime.LastAdvanceReason }
        };
    }

    private Dictionary<string, object> WorldCalendarEventPayload(WorldCalendarEventState item, WorldCalendarDefinition calendar, UserAccount actor, bool includeAdminFields)
    {
        var versions = _repositories.WorldCalendarEventVersions.Find(Builders<WorldCalendarEventVersionState>.Filter.Eq(x => x.EventId, item.Id))
            .OrderBy(x => x.CreatedAtUtc)
            .Where(v => includeAdminFields || CanPlayerSeeVersion(v, EnsureWorldTime(calendar.CampaignId, calendar, actor.Id)))
            .Select(v => (object)VersionPayload(v, includeAdminFields))
            .ToArray();
        var result = new Dictionary<string, object>
        {
            { "eventId", item.Id },
            { "title", item.Title },
            { "description", includeAdminFields ? item.Description : item.PublicSummary },
            { "eventType", item.EventType },
            { "eventTypeDisplay", EventTypeDisplay(item.EventType) },
            { "status", item.Status },
            { "statusDisplay", StatusDisplay(item.Status) },
            { "start", DatePayload(item.StartWorldDateTime, calendar) },
            { "startDisplay", WorldCalendarMath.Format(item.StartWorldDateTime, calendar) },
            { "isFutureEvent", item.IsFutureEvent },
            { "isPlayerVisible", item.IsPlayerVisible },
            { "visibilityMode", item.VisibilityMode },
            { "revealPolicy", item.RevealPolicy },
            { "authorDisplayName", item.AuthorDisplayName },
            { "publicSummary", item.PublicSummary },
            { "versions", versions },
            { "linkedSessionId", includeAdminFields ? item.LinkedSessionId : string.Empty },
            { "linkedGroupId", includeAdminFields ? item.LinkedGroupId : string.Empty }
        };
        if (includeAdminFields)
        {
            result["gmSummary"] = item.GMSummary;
            result["conditionsSummary"] = item.ConditionsSummary;
            result["linkedCharacterId"] = item.LinkedCharacterId;
            result["linkedLocationId"] = item.LinkedLocationId;
            result["linkedSpaceNodeId"] = item.LinkedSpaceNodeId;
            result["linkedMapId"] = item.LinkedMapId;
            result["linkedRoomId"] = item.LinkedRoomId;
            result["reminderEnabled"] = item.ReminderEnabled;
            result["createdAtUtc"] = item.CreatedAtUtc;
            result["updatedAtUtc"] = item.UpdatedAtUtc;
        }
        return result;
    }

    private static Dictionary<string, object> VersionPayload(WorldCalendarEventVersionState version, bool includeAdminFields)
    {
        var result = new Dictionary<string, object>
        {
            { "versionId", version.Id },
            { "versionType", version.VersionType },
            { "versionTypeDisplay", VersionTypeDisplay(version.VersionType) },
            { "title", version.Title },
            { "summary", version.Summary },
            { "isPlayerVisible", version.IsPlayerVisible },
            { "visibilityMode", version.VisibilityMode },
            { "authorDisplayName", version.AuthorDisplayName },
            { "createdAtUtc", version.CreatedAtUtc }
        };
        if (includeAdminFields) result["body"] = version.Body;
        return result;
    }

    private static Dictionary<string, object> DatePayload(WorldDateTimeValue value, WorldCalendarDefinition calendar)
        => new()
        {
            { "calendarId", value.CalendarId },
            { "year", value.Year },
            { "displayYear", WorldCalendarMath.DisplayYear(value) },
            { "era", WorldCalendarMath.EraId(value) },
            { "eraName", WorldCalendarMath.EraName(value, calendar) },
            { "eraShortName", WorldCalendarMath.EraShortName(value, calendar) },
            { "monthOrder", value.MonthOrder },
            { "dayOfMonth", value.DayOfMonth },
            { "hour", value.Hour },
            { "minute", value.Minute },
            { "second", value.Second },
            { "absoluteDayIndex", value.AbsoluteDayIndex },
            { "absoluteSecondIndex", value.AbsoluteSecondIndex },
            { "display", WorldCalendarMath.Format(value, calendar) },
            { "season", WorldCalendarMath.SeasonName(value) },
            { "weekDay", WorldCalendarMath.WeekDayName(value) }
        };

    private static Dictionary<string, object> SeasonPayload(WorldCalendarSeasonDefinition season)
        => new() { { "seasonId", season.Id }, { "order", season.Order }, { "name", season.Name }, { "description", season.Description }, { "startMonthOrder", season.StartMonthOrder ?? 0 }, { "monthCount", season.MonthCount }, { "colorKey", season.ColorKey } };

    private static Dictionary<string, object> MonthPayload(WorldCalendarMonthDefinition month)
        => new() { { "monthId", month.Id }, { "order", month.Order }, { "name", month.Name }, { "shortName", month.ShortName }, { "description", month.Description }, { "daysInMonth", month.DaysInMonth }, { "seasonId", month.SeasonId }, { "colorKey", month.ColorKey } };

    private static Dictionary<string, object> HolidayPayload(WorldCalendarHolidayDefinition holiday, bool includeAdminFields)
    {
        var result = new Dictionary<string, object> { { "holidayId", holiday.Id }, { "name", holiday.Name }, { "description", holiday.Description }, { "monthOrder", holiday.MonthOrder }, { "dayOfMonth", holiday.DayOfMonth }, { "isPlayerVisible", holiday.IsPlayerVisible }, { "colorKey", holiday.ColorKey } };
        if (includeAdminFields) result["isArchived"] = holiday.IsArchived;
        return result;
    }

    private static Dictionary<string, object> ReminderPayload(WorldCalendarReminderState reminder)
        => new() { { "reminderId", reminder.Id }, { "eventId", reminder.EventId }, { "title", reminder.Title }, { "notes", reminder.Notes }, { "dateDisplay", WorldCalendarMath.Format(reminder.ReminderAtWorldDateTime) }, { "isDismissed", reminder.IsDismissed } };

    private void AddDefaultWorldEventVersion(WorldCalendarEventState item, UserAccount actor, string summary)
    {
        _repositories.WorldCalendarEventVersions.Insert(new WorldCalendarEventVersionState
        {
            EventId = item.Id,
            CampaignId = item.CampaignId,
            CalendarId = item.CalendarId,
            VersionType = item.IsPlayerVisible ? WorldCalendarVersionTypeIds.PlayerVisible : WorldCalendarVersionTypeIds.OfficialHistory,
            Title = item.Title,
            Summary = FirstNonEmpty(summary, item.PublicSummary, item.Description),
            Body = item.Description,
            IsPlayerVisible = item.IsPlayerVisible,
            VisibilityMode = item.VisibilityMode,
            RevealPolicy = item.RevealPolicy,
            AuthorUserId = actor.Id,
            AuthorDisplayName = FirstNonEmpty(actor.Login, actor.Id),
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
    }

    private void CreateReminderForEvent(WorldCalendarEventState item, UserAccount actor, WorldDateTimeValue date, string notes)
    {
        if (!WorldCalendarRemindersEnabled()) return;
        _repositories.WorldCalendarReminders.Insert(new WorldCalendarReminderState
        {
            CampaignId = item.CampaignId,
            CalendarId = item.CalendarId,
            EventId = item.Id,
            Title = item.Title,
            Notes = notes,
            ReminderAtWorldDateTime = date,
            CreatedByUserId = actor.Id,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
    }

    private void WriteCalendarJournalEntry(UserAccount actor, WorldCalendarEventState item, WorldCalendarDefinition calendar, string action)
    {
        if (!EventJournalCalendarIntegrationEnabled()) return;
        var playerVisible = CanCalendarJournalEntryBePlayerVisible(item);
        var now = DateTime.UtcNow;
        var entry = new EventJournalEntryState
        {
            CampaignId = item.CampaignId,
            SessionId = item.LinkedSessionId,
            GroupId = item.LinkedGroupId,
            CharacterId = item.LinkedCharacterId,
            SourceModule = "world_calendar",
            SourceEventType = $"world_calendar.event.{action}",
            SourceEventId = item.Id,
            CorrelationId = $"world_calendar:{item.Id}:{action}:{item.UpdatedAtUtc.Ticks}",
            EntryType = EventJournalEntryTypeIds.Automatic,
            Category = EventJournalCategoryIds.WorldCalendar,
            Severity = EventJournalSeverityIds.Information,
            Title = $"Календарь мира: {item.Title}",
            Summary = FirstNonEmpty(item.PublicSummary, item.Description, item.Title),
            PlayerSummary = playerVisible ? FirstNonEmpty(item.PublicSummary, item.Title) : string.Empty,
            GMDetails = item.GMSummary,
            VisibilityMode = playerVisible ? EventJournalVisibilityModeIds.PlayerVisible : EventJournalVisibilityModeIds.GMOnly,
            IsPlayerVisible = playerVisible,
            IsAutomatic = true,
            ActorUserId = actor.Id,
            ActorDisplayName = FirstNonEmpty(actor.Login, actor.Id),
            SubjectEntityType = EventJournalEntityTypeIds.WorldCalendarEvent,
            SubjectEntityId = item.Id,
            SubjectDisplayName = item.Title,
            OccurredAtUtc = now,
            WorldDateTimeSnapshot = WorldCalendarMath.Format(item.StartWorldDateTime, calendar),
            CreatedAtUtc = now,
            CreatedByUserId = actor.Id,
            UpdatedAtUtc = now,
            MetadataSummary = $"source=world_calendar; action={action}; date={WorldCalendarMath.Format(item.StartWorldDateTime, calendar)}"
        };
        InsertJournalEntry(actor, entry, "calendar_linked");
    }

    private static bool CanCalendarJournalEntryBePlayerVisible(WorldCalendarEventState item)
        => item.IsPlayerVisible
           && string.Equals(item.VisibilityMode, WorldCalendarVisibilityModeIds.PlayerVisible, StringComparison.OrdinalIgnoreCase)
           && !string.Equals(item.RevealPolicy, WorldCalendarRevealPolicyIds.Hidden, StringComparison.OrdinalIgnoreCase);

    private void SyncCurrentSessionWorldDate(string campaignId, string display)
    {
        if (!WorldCalendarSessionLinkEnabled()) return;
        var sessions = _repositories.CurrentSessions.Find(Builders<CurrentSessionState>.Filter.Eq(x => x.CampaignId, campaignId ?? string.Empty));
        foreach (var session in sessions.Where(x => !x.IsArchived && (x.Status == CurrentSessionStatusIds.Active || x.Status == CurrentSessionStatusIds.Paused)))
        {
            session.CurrentWorldDate = display;
            session.UpdatedAtUtc = DateTime.UtcNow;
            _repositories.CurrentSessions.Replace(session);
        }
    }

    private void TouchWorldTime(CampaignWorldTimeState worldTime)
    {
        worldTime.Revision++;
        worldTime.UpdatedAtUtc = DateTime.UtcNow;
    }

    private ResponseEnvelope WorldCalendarDisabled(string command)
    {
        _logger.Admin($"world.calendar.disabled command={command}");
        return Error("World Calendar MVP is disabled by feature flags.", ResponseStatus.Forbidden, ErrorCode.Forbidden);
    }

    private bool WorldCalendarBaseEnabled() => _featureFlags.IsEnabled(nameof(WorldCalendarFeatureFlags.UseWorldCalendarMvp));
    private bool WorldCalendarCurrentEnabled() => WorldCalendarBaseEnabled() && _featureFlags.IsEnabled(nameof(WorldCalendarFeatureFlags.UseWorldCalendarCurrentDate));
    private bool WorldCalendarEventsEnabled() => WorldCalendarBaseEnabled() && _featureFlags.IsEnabled(nameof(WorldCalendarFeatureFlags.UseWorldCalendarEvents));
    private bool WorldCalendarChronicleEnabled() => WorldCalendarBaseEnabled() && _featureFlags.IsEnabled(nameof(WorldCalendarFeatureFlags.UseWorldCalendarChronicle));
    private bool WorldCalendarPlayerEnabled() => WorldCalendarBaseEnabled() && _featureFlags.IsEnabled(nameof(WorldCalendarFeatureFlags.UseWorldCalendarPlayerView));
    private bool WorldCalendarFutureVisibilityEnabled() => WorldCalendarBaseEnabled() && _featureFlags.IsEnabled(nameof(WorldCalendarFeatureFlags.UseWorldCalendarFutureVisibility));
    private bool WorldCalendarRemindersEnabled() => WorldCalendarBaseEnabled() && _featureFlags.IsEnabled(nameof(WorldCalendarFeatureFlags.UseWorldCalendarReminders));
    private bool WorldCalendarHolidaysEnabled() => WorldCalendarBaseEnabled() && _featureFlags.IsEnabled(nameof(WorldCalendarFeatureFlags.UseWorldCalendarHolidays));
    private bool WorldCalendarSessionLinkEnabled() => WorldCalendarBaseEnabled() && _featureFlags.IsEnabled(nameof(WorldCalendarFeatureFlags.UseWorldCalendarSessionLink));
    private bool EventJournalCalendarIntegrationEnabled()
        => _featureFlags.IsEnabled(nameof(EventJournalFeatureFlags.UseEventJournalMvp))
           && _featureFlags.IsEnabled(nameof(EventJournalFeatureFlags.UseEventJournalAutomaticIngestion))
           && _featureFlags.IsEnabled(nameof(EventJournalFeatureFlags.UseEventJournalCalendarIntegration));

    private static bool IsAdminLike(UserAccount actor) => actor.Roles.Contains(UserRole.Admin) || actor.Roles.Contains(UserRole.SuperAdmin);
    private static int Clamp(int value, int min, int max) => Math.Max(min, Math.Min(max, value));
    private static string RequireWorldEventType(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "" => WorldCalendarEventTypeIds.Fixed,
        WorldCalendarEventTypeIds.Fixed => WorldCalendarEventTypeIds.Fixed,
        WorldCalendarEventTypeIds.Flexible => WorldCalendarEventTypeIds.Flexible,
        WorldCalendarEventTypeIds.Conditional => WorldCalendarEventTypeIds.Conditional,
        WorldCalendarEventTypeIds.Optional => WorldCalendarEventTypeIds.Optional,
        WorldCalendarEventTypeIds.Cancelled => WorldCalendarEventTypeIds.Cancelled,
        _ => throw new InvalidOperationException($"Invalid world calendar event type: {value}.")
    };

    private static string RequireWorldEventStatus(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "" => WorldCalendarEventStatusIds.Planned,
        WorldCalendarEventStatusIds.Planned => WorldCalendarEventStatusIds.Planned,
        WorldCalendarEventStatusIds.Upcoming => WorldCalendarEventStatusIds.Upcoming,
        WorldCalendarEventStatusIds.Occurred => WorldCalendarEventStatusIds.Occurred,
        WorldCalendarEventStatusIds.Resolved => WorldCalendarEventStatusIds.Resolved,
        WorldCalendarEventStatusIds.Cancelled => WorldCalendarEventStatusIds.Cancelled,
        WorldCalendarEventStatusIds.Archived => WorldCalendarEventStatusIds.Archived,
        _ => throw new InvalidOperationException($"Invalid world calendar event status: {value}.")
    };

    private static string RequireRevealPolicy(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "" => WorldCalendarRevealPolicyIds.Manual,
        WorldCalendarRevealPolicyIds.Immediate => WorldCalendarRevealPolicyIds.Immediate,
        WorldCalendarRevealPolicyIds.AtDate => WorldCalendarRevealPolicyIds.AtDate,
        WorldCalendarRevealPolicyIds.Manual => WorldCalendarRevealPolicyIds.Manual,
        WorldCalendarRevealPolicyIds.Hidden => WorldCalendarRevealPolicyIds.Hidden,
        _ => throw new InvalidOperationException($"Invalid world calendar reveal policy: {value}.")
    };

    private static string NormalizeVersionType(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        WorldCalendarVersionTypeIds.Truth => WorldCalendarVersionTypeIds.Truth,
        WorldCalendarVersionTypeIds.PlayerVisible => WorldCalendarVersionTypeIds.PlayerVisible,
        WorldCalendarVersionTypeIds.GmOnly => WorldCalendarVersionTypeIds.GmOnly,
        WorldCalendarVersionTypeIds.Alternative => WorldCalendarVersionTypeIds.Alternative,
        WorldCalendarVersionTypeIds.Custom => WorldCalendarVersionTypeIds.Custom,
        _ => WorldCalendarVersionTypeIds.OfficialHistory
    };

    private static string RequireWorldCalendarVisibility(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized)) return WorldCalendarVisibilityModeIds.GmOnly;
        if (normalized == WorldCalendarVisibilityModeIds.PlayerVisible) return WorldCalendarVisibilityModeIds.PlayerVisible;
        if (normalized == WorldCalendarVisibilityModeIds.GmOnly) return WorldCalendarVisibilityModeIds.GmOnly;
        if (normalized == WorldCalendarVisibilityModeIds.AdminOnly) return WorldCalendarVisibilityModeIds.AdminOnly;
        if (normalized == WorldCalendarVisibilityModeIds.SuperAdminOnly) return WorldCalendarVisibilityModeIds.SuperAdminOnly;
        if (normalized == WorldCalendarVisibilityModeIds.ServerOnly) return WorldCalendarVisibilityModeIds.ServerOnly;
        throw new InvalidOperationException($"Invalid world calendar visibility mode: {value}.");
    }

    private static string EventTypeDisplay(string value) => value switch
    {
        WorldCalendarEventTypeIds.Flexible => "Гибкое",
        WorldCalendarEventTypeIds.Conditional => "Условное",
        WorldCalendarEventTypeIds.Optional => "Опциональное",
        WorldCalendarEventTypeIds.Cancelled => "Отменённое",
        _ => "Фиксированное"
    };

    private static string StatusDisplay(string value) => value switch
    {
        WorldCalendarEventStatusIds.Upcoming => "Скоро",
        WorldCalendarEventStatusIds.Occurred => "Произошло",
        WorldCalendarEventStatusIds.Resolved => "Закрыто",
        WorldCalendarEventStatusIds.Cancelled => "Отменено",
        WorldCalendarEventStatusIds.Archived => "Архив",
        _ => "Запланировано"
    };

    private static string VersionTypeDisplay(string value) => value switch
    {
        WorldCalendarVersionTypeIds.Truth => "Истина",
        WorldCalendarVersionTypeIds.PlayerVisible => "Версия для игроков",
        WorldCalendarVersionTypeIds.GmOnly => "Версия GM",
        WorldCalendarVersionTypeIds.Alternative => "Дополнительная версия",
        WorldCalendarVersionTypeIds.Custom => "Другое",
        _ => "Официальная история"
    };
}
