using System;
using System.Collections.Generic;
using System.Linq;

namespace Nri.Shared.Domain;

public static class WorldCalendarDefaults
{
    public const int DaysPerWeek = 7;
    public const int MonthsPerYear = 12;
    public const int DaysPerYear = 336;
    public const int DaysPerMonth = 28;
    public const int HoursPerDay = 24;
    public const int MinutesPerHour = 60;
    public const int SecondsPerMinute = 60;
    public const string EraName = "Новая Эра";
    public const string EraShortName = "Н.Э.";
    public const string BeforeEraName = "до нашей эры";
    public const string BeforeEraShortName = "до н.э.";
    public const string DefaultCalendarName = "Календарь Новой Эры";
    public const string YearZeroDescription = "Начало Новой Эры: падение Первой Империи Человечества и рабовладельческого строя государства.";
}

public static class WorldCalendarEraIds
{
    public const string CommonEra = "ce";
    public const string BeforeCommonEra = "bce";
}

public static class WorldCalendarEraLabels
{
    public const string CommonEra = "наша эра";
    public const string BeforeCommonEra = "до нашей эры";
}

public static class WorldCalendarEventTypeIds
{
    public const string Fixed = "fixed";
    public const string Flexible = "flexible";
    public const string Conditional = "conditional";
    public const string Optional = "optional";
    public const string Cancelled = "cancelled";
}

public static class WorldCalendarEventStatusIds
{
    public const string Planned = "planned";
    public const string Upcoming = "upcoming";
    public const string Occurred = "occurred";
    public const string Resolved = "resolved";
    public const string Cancelled = "cancelled";
    public const string Archived = "archived";
}

public static class WorldCalendarRevealPolicyIds
{
    public const string Immediate = "immediate";
    public const string AtDate = "at_date";
    public const string Manual = "manual";
    public const string Hidden = "hidden";
}

public static class WorldCalendarVisibilityModeIds
{
    public const string PlayerVisible = "player_visible";
    public const string GmOnly = "gm_only";
    public const string AdminOnly = "admin_only";
    public const string SuperAdminOnly = "superadmin_only";
    public const string ServerOnly = "server_only";
}

public static class WorldCalendarVersionTypeIds
{
    public const string OfficialHistory = "official_history";
    public const string Truth = "truth";
    public const string PlayerVisible = "player_visible";
    public const string GmOnly = "gm_only";
    public const string Alternative = "alternative";
    public const string Custom = "custom";
}

public sealed class WorldCalendarDefinition : EntityBase
{
    public string CampaignId { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = string.Empty;
    public string Name { get; set; } = WorldCalendarDefaults.DefaultCalendarName;
    public string Description { get; set; } = string.Empty;
    public int DaysPerWeek { get; set; } = WorldCalendarDefaults.DaysPerWeek;
    public int MonthsPerYear { get; set; } = WorldCalendarDefaults.MonthsPerYear;
    public int DaysPerYear { get; set; } = WorldCalendarDefaults.DaysPerYear;
    public int HoursPerDay { get; set; } = WorldCalendarDefaults.HoursPerDay;
    public int MinutesPerHour { get; set; } = WorldCalendarDefaults.MinutesPerHour;
    public int SecondsPerMinute { get; set; } = WorldCalendarDefaults.SecondsPerMinute;
    public string EraName { get; set; } = WorldCalendarDefaults.EraName;
    public string EraShortName { get; set; } = WorldCalendarDefaults.EraShortName;
    public string BeforeEraName { get; set; } = WorldCalendarDefaults.BeforeEraName;
    public string BeforeEraShortName { get; set; } = WorldCalendarDefaults.BeforeEraShortName;
    public string YearZeroDescription { get; set; } = WorldCalendarDefaults.YearZeroDescription;
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsArchived { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
    public string UpdatedByUserId { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public Dictionary<string, object> ExtraData { get; set; } = new();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new();
}

public sealed class WorldCalendarSeasonDefinition : EntityBase
{
    public string CalendarId { get; set; } = string.Empty;
    public int Order { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int? StartMonthOrder { get; set; }
    public int MonthCount { get; set; } = 3;
    public string ColorKey { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public Dictionary<string, object> ExtraData { get; set; } = new();
}

public sealed class WorldCalendarMonthDefinition : EntityBase
{
    public string CalendarId { get; set; } = string.Empty;
    public int Order { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int DaysInMonth { get; set; } = WorldCalendarDefaults.DaysPerMonth;
    public string SeasonId { get; set; } = string.Empty;
    public string ColorKey { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public Dictionary<string, object> ExtraData { get; set; } = new();
}

public sealed class WorldDateTimeValue
{
    public string CalendarId { get; set; } = string.Empty;
    public int Year { get; set; }
    public int MonthOrder { get; set; } = 1;
    public int DayOfMonth { get; set; } = 1;
    public int Hour { get; set; }
    public int Minute { get; set; }
    public int Second { get; set; }
    public int AbsoluteDayIndex { get; set; }
    public long AbsoluteSecondIndex { get; set; }
}

public sealed class CampaignWorldTimeState : EntityBase
{
    public string CampaignId { get; set; } = string.Empty;
    public string CalendarId { get; set; } = string.Empty;
    public WorldDateTimeValue CurrentDateTime { get; set; } = new();
    public bool IsPaused { get; set; }
    public DateTime? LastAdvancedAtUtc { get; set; }
    public string LastAdvancedByUserId { get; set; } = string.Empty;
    public string LastAdvanceReason { get; set; } = string.Empty;
    public int Revision { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object> ExtraData { get; set; } = new();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new();
}

public sealed class WorldCalendarEventState : EntityBase
{
    public string CampaignId { get; set; } = string.Empty;
    public string CalendarId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string EventType { get; set; } = WorldCalendarEventTypeIds.Fixed;
    public string Status { get; set; } = WorldCalendarEventStatusIds.Planned;
    public WorldDateTimeValue StartWorldDateTime { get; set; } = new();
    public WorldDateTimeValue? EndWorldDateTime { get; set; }
    public bool IsFutureEvent { get; set; }
    public bool IsPlayerVisible { get; set; }
    public string VisibilityMode { get; set; } = WorldCalendarVisibilityModeIds.GmOnly;
    public string RevealPolicy { get; set; } = WorldCalendarRevealPolicyIds.Manual;
    public WorldDateTimeValue? RevealAtWorldDateTime { get; set; }
    public string AuthorUserId { get; set; } = string.Empty;
    public string AuthorDisplayName { get; set; } = string.Empty;
    public string LinkedSessionId { get; set; } = string.Empty;
    public string LinkedGroupId { get; set; } = string.Empty;
    public string LinkedCharacterId { get; set; } = string.Empty;
    public string LinkedLocationId { get; set; } = string.Empty;
    public string LinkedSpaceNodeId { get; set; } = string.Empty;
    public string LinkedMapId { get; set; } = string.Empty;
    public string LinkedRoomId { get; set; } = string.Empty;
    public string LinkedRealScheduleEventId { get; set; } = string.Empty;
    public string PublicSummary { get; set; } = string.Empty;
    public string GMSummary { get; set; } = string.Empty;
    public string ConditionsSummary { get; set; } = string.Empty;
    public bool ReminderEnabled { get; set; }
    public WorldDateTimeValue? ReminderAtWorldDateTime { get; set; }
    public bool IsArchived { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public List<string> Tags { get; set; } = new();
    public Dictionary<string, object> ExtraData { get; set; } = new();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new();
}

public sealed class WorldCalendarEventVersionState : EntityBase
{
    public string EventId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string CalendarId { get; set; } = string.Empty;
    public string VersionType { get; set; } = WorldCalendarVersionTypeIds.OfficialHistory;
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public bool IsPlayerVisible { get; set; }
    public string VisibilityMode { get; set; } = WorldCalendarVisibilityModeIds.GmOnly;
    public string RevealPolicy { get; set; } = WorldCalendarRevealPolicyIds.Manual;
    public WorldDateTimeValue? RevealAtWorldDateTime { get; set; }
    public string AuthorUserId { get; set; } = string.Empty;
    public string AuthorDisplayName { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object> ExtraData { get; set; } = new();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new();
}

public sealed class WorldCalendarHolidayDefinition : EntityBase
{
    public string CalendarId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int MonthOrder { get; set; } = 1;
    public int DayOfMonth { get; set; } = 1;
    public bool IsPlayerVisible { get; set; } = true;
    public bool IsArchived { get; set; }
    public string ColorKey { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public Dictionary<string, object> ExtraData { get; set; } = new();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new();
}

public sealed class WorldCalendarReminderState : EntityBase
{
    public string CampaignId { get; set; } = string.Empty;
    public string CalendarId { get; set; } = string.Empty;
    public string EventId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public WorldDateTimeValue ReminderAtWorldDateTime { get; set; } = new();
    public bool IsDismissed { get; set; }
    public DateTime? DismissedAtUtc { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object> ExtraData { get; set; } = new();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new();
}

public static class WorldCalendarTemplate
{
    public static readonly (string Name, string Description, int StartMonth, string ColorKey)[] Seasons =
    {
        ("Аки", "Зима", 1, "season.winter"),
        ("Джуно", "Весна", 4, "season.spring"),
        ("Лейти", "Лето", 7, "season.summer"),
        ("Креа", "Осень", 10, "season.autumn")
    };

    public static readonly (string Name, string Description)[] Months =
    {
        ("Умбра", "тьма"),
        ("Винтер", "холод"),
        ("Инцио", "нижний мир"),
        ("Шай", "почва и плодородие"),
        ("Ли", "жизнь"),
        ("Бирд", "небо"),
        ("Монсун", "главный бог небесных тел"),
        ("Крим", "море"),
        ("Кателад", "труд"),
        ("Нерос", "обман"),
        ("Слурик", "сокровенные желания"),
        ("Кип", "первый император Первой Империи Человечества")
    };

    public static readonly string[] WeekDays = Enumerable.Range(1, WorldCalendarDefaults.DaysPerWeek)
        .Select(x => $"День {x}")
        .ToArray();

    public static WorldCalendarDefinition CreateDefaultCalendar(string campaignId, string ruleSetId, string actorUserId)
    {
        var now = DateTime.UtcNow;
        return new WorldCalendarDefinition
        {
            CampaignId = campaignId ?? string.Empty,
            RuleSetId = ruleSetId ?? string.Empty,
            Name = WorldCalendarDefaults.DefaultCalendarName,
            Description = "Базовый календарь мира: 336 дней, 12 месяцев по 28 дней, 4 сезона.",
            IsDefault = true,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            CreatedByUserId = actorUserId ?? string.Empty,
            UpdatedByUserId = actorUserId ?? string.Empty
        };
    }

    public static WorldDateTimeValue CreateDefaultDate(string calendarId)
        => WorldCalendarMath.Normalize(new WorldDateTimeValue
        {
            CalendarId = calendarId ?? string.Empty,
            Year = 1612,
            MonthOrder = 4,
            DayOfMonth = 18,
            Hour = 12
        }, WorldCalendarDefaults.HoursPerDay, WorldCalendarDefaults.MinutesPerHour, WorldCalendarDefaults.SecondsPerMinute);
}

public static class WorldCalendarMath
{
    public static WorldDateTimeValue Normalize(WorldDateTimeValue value, int hoursPerDay, int minutesPerHour, int secondsPerMinute)
    {
        value ??= new WorldDateTimeValue();
        var month = Math.Max(1, Math.Min(WorldCalendarDefaults.MonthsPerYear, value.MonthOrder));
        var day = Math.Max(1, Math.Min(WorldCalendarDefaults.DaysPerMonth, value.DayOfMonth));
        var hour = Math.Max(0, Math.Min(Math.Max(1, hoursPerDay) - 1, value.Hour));
        var minute = Math.Max(0, Math.Min(Math.Max(1, minutesPerHour) - 1, value.Minute));
        var second = Math.Max(0, Math.Min(Math.Max(1, secondsPerMinute) - 1, value.Second));
        var absoluteDay = (value.Year * WorldCalendarDefaults.DaysPerYear) + ((month - 1) * WorldCalendarDefaults.DaysPerMonth) + (day - 1);
        var absoluteSecond = ((long)absoluteDay * hoursPerDay * minutesPerHour * secondsPerMinute) + (hour * minutesPerHour * secondsPerMinute) + (minute * secondsPerMinute) + second;
        return new WorldDateTimeValue
        {
            CalendarId = value.CalendarId ?? string.Empty,
            Year = value.Year,
            MonthOrder = month,
            DayOfMonth = day,
            Hour = hour,
            Minute = minute,
            Second = second,
            AbsoluteDayIndex = absoluteDay,
            AbsoluteSecondIndex = absoluteSecond
        };
    }

    public static WorldDateTimeValue FromAbsoluteSeconds(string calendarId, long absoluteSecond, int hoursPerDay, int minutesPerHour, int secondsPerMinute)
    {
        var secondsInDay = Math.Max(1, hoursPerDay) * Math.Max(1, minutesPerHour) * Math.Max(1, secondsPerMinute);
        var absoluteDay = (int)Math.Floor(absoluteSecond / (double)secondsInDay);
        var daySecond = (int)(absoluteSecond - ((long)absoluteDay * secondsInDay));
        var year = (int)Math.Floor(absoluteDay / (double)WorldCalendarDefaults.DaysPerYear);
        var dayOfYear = absoluteDay - (year * WorldCalendarDefaults.DaysPerYear);
        var month = (dayOfYear / WorldCalendarDefaults.DaysPerMonth) + 1;
        var day = (dayOfYear % WorldCalendarDefaults.DaysPerMonth) + 1;
        var hour = daySecond / (minutesPerHour * secondsPerMinute);
        var minute = (daySecond % (minutesPerHour * secondsPerMinute)) / secondsPerMinute;
        var second = daySecond % secondsPerMinute;
        return Normalize(new WorldDateTimeValue
        {
            CalendarId = calendarId ?? string.Empty,
            Year = year,
            MonthOrder = month,
            DayOfMonth = day,
            Hour = hour,
            Minute = minute,
            Second = second
        }, hoursPerDay, minutesPerHour, secondsPerMinute);
    }

    public static string Format(WorldDateTimeValue? value, WorldCalendarDefinition? calendar = null, bool includeTime = true)
    {
        if (value == null) return "—";
        var monthName = WorldCalendarTemplate.Months.ElementAtOrDefault(Math.Max(0, value.MonthOrder - 1)).Name;
        if (string.IsNullOrWhiteSpace(monthName)) monthName = $"Месяц {value.MonthOrder}";
        var era = EraShortName(value, calendar);
        var text = $"{value.DayOfMonth} {monthName}, {DisplayYear(value)} {era}";
        return includeTime ? $"{text}, {value.Hour:00}:{value.Minute:00}" : text;
    }

    public static int DisplayYear(WorldDateTimeValue? value)
        => value == null ? 0 : value.Year >= 1 ? value.Year : 1 - value.Year;

    public static string EraId(WorldDateTimeValue? value)
        => value != null && value.Year < 1 ? WorldCalendarEraIds.BeforeCommonEra : WorldCalendarEraIds.CommonEra;

    public static string EraName(WorldDateTimeValue? value, WorldCalendarDefinition? calendar = null)
    {
        if (EraId(value) == WorldCalendarEraIds.BeforeCommonEra)
            return string.IsNullOrWhiteSpace(calendar?.BeforeEraName) ? WorldCalendarDefaults.BeforeEraName : calendar!.BeforeEraName;
        return string.IsNullOrWhiteSpace(calendar?.EraName) ? WorldCalendarDefaults.EraName : calendar!.EraName;
    }

    public static string EraShortName(WorldDateTimeValue? value, WorldCalendarDefinition? calendar = null)
    {
        if (EraId(value) == WorldCalendarEraIds.BeforeCommonEra)
            return string.IsNullOrWhiteSpace(calendar?.BeforeEraShortName) ? WorldCalendarDefaults.BeforeEraShortName : calendar!.BeforeEraShortName;
        return string.IsNullOrWhiteSpace(calendar?.EraShortName) ? WorldCalendarDefaults.EraShortName : calendar!.EraShortName;
    }

    public static int ToSignedYear(int displayYear, string? era)
    {
        var safeYear = Math.Max(1, Math.Abs(displayYear));
        var normalized = (era ?? string.Empty).Trim().ToLowerInvariant();
        return normalized == WorldCalendarEraIds.BeforeCommonEra
               || normalized == "before_common_era"
               || normalized == "до нашей эры"
               || normalized == "до н.э."
               || normalized == "до н. э."
            ? 1 - safeYear
            : safeYear;
    }

    public static string SeasonName(WorldDateTimeValue? value)
    {
        if (value == null) return "—";
        var index = Math.Max(0, Math.Min(3, (value.MonthOrder - 1) / 3));
        return WorldCalendarTemplate.Seasons[index].Name;
    }

    public static string WeekDayName(WorldDateTimeValue? value)
    {
        if (value == null) return "—";
        var index = ((value.AbsoluteDayIndex % WorldCalendarDefaults.DaysPerWeek) + WorldCalendarDefaults.DaysPerWeek) % WorldCalendarDefaults.DaysPerWeek;
        return WorldCalendarTemplate.WeekDays[index];
    }
}
