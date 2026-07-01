using System;
using System.Collections.Generic;

namespace Nri.Shared.Domain;

public static class RealScheduleVisibilityModeIds
{
    public const string PlayerVisible = "player_visible";
    public const string GmOnly = "gm_only";
    public const string AdminOnly = "admin_only";
    public const string ServerOnly = "server_only";
}

public sealed class RealScheduleEventState : EntityBase
{
    public string CampaignId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string GroupId { get; set; } = string.Empty;
    public string LinkedWorldCalendarEventId { get; set; } = string.Empty;
    public WorldDateTimeValue? LinkedWorldDateTime { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string EventType { get; set; } = RealScheduleEventTypeIds.GameSession;
    public string Status { get; set; } = RealScheduleEventStatusIds.Planned;
    public DateTime StartUtc { get; set; } = DateTime.UtcNow;
    public DateTime? EndUtc { get; set; }
    public string TimeZoneId { get; set; } = "UTC";
    public string GMUserId { get; set; } = string.Empty;
    public string GMDisplayName { get; set; } = string.Empty;
    public string OrganizerUserId { get; set; } = string.Empty;
    public string OrganizerDisplayName { get; set; } = string.Empty;
    public string LocationText { get; set; } = string.Empty;
    public string ConnectionInfoSummary { get; set; } = string.Empty;
    public bool IsPlayerVisible { get; set; }
    public string VisibilityMode { get; set; } = RealScheduleVisibilityModeIds.GmOnly;
    public bool ReminderEnabled { get; set; }
    public int? ReminderBeforeMinutes { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
    public string UpdatedByUserId { get; set; } = string.Empty;
    public DateTime? CancelledAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string PublicNotes { get; set; } = string.Empty;
    public string GMNotes { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public Dictionary<string, object> ExtraData { get; set; } = new();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new();
}

public sealed class RealScheduleParticipantState : EntityBase
{
    public string EventId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ParticipantRole { get; set; } = RealScheduleParticipantRoleIds.Player;
    public string ResponseStatus { get; set; } = RealScheduleParticipantResponseIds.Unknown;
    public bool IsPlayerVisible { get; set; } = true;
    public DateTime AddedAtUtc { get; set; } = DateTime.UtcNow;
    public string AddedByUserId { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public bool IsArchived { get; set; }
    public Dictionary<string, object> ExtraData { get; set; } = new();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new();
}
