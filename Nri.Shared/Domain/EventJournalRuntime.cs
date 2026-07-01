using System;
using System.Collections.Generic;

namespace Nri.Shared.Domain;

public sealed class EventJournalEntryState : EntityBase
{
    public string CampaignId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string GroupId { get; set; } = string.Empty;
    public string CharacterId { get; set; } = string.Empty;
    public string SourceModule { get; set; } = string.Empty;
    public string SourceEventType { get; set; } = string.Empty;
    public string SourceEventId { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public string EntryType { get; set; } = EventJournalEntryTypeIds.Manual;
    public string Category { get; set; } = EventJournalCategoryIds.Custom;
    public string Severity { get; set; } = EventJournalSeverityIds.Information;
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string PlayerSummary { get; set; } = string.Empty;
    public string GMDetails { get; set; } = string.Empty;
    public string VisibilityMode { get; set; } = EventJournalVisibilityModeIds.GMOnly;
    public bool IsPlayerVisible { get; set; }
    public bool IsAutomatic { get; set; }
    public bool IsCorrection { get; set; }
    public string CorrectsEntryId { get; set; } = string.Empty;
    public string RelatedEntryId { get; set; } = string.Empty;
    public string ActorUserId { get; set; } = string.Empty;
    public string ActorDisplayName { get; set; } = string.Empty;
    public string SubjectEntityType { get; set; } = string.Empty;
    public string SubjectEntityId { get; set; } = string.Empty;
    public string SubjectDisplayName { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
    public string WorldDateTimeSnapshot { get; set; } = string.Empty;
    public long SequenceNumber { get; set; }
    public bool IsArchived { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public List<string> Tags { get; set; } = new List<string>();
    public string MetadataSummary { get; set; } = string.Empty;
    public Dictionary<string, object> ExtraData { get; set; } = new Dictionary<string, object>();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new Dictionary<string, object>();
}

public sealed class EventJournalEntityLinkState : EntityBase
{
    public string EntryId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string EntityType { get; set; } = EventJournalEntityTypeIds.Custom;
    public string EntityId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string LinkRole { get; set; } = EventJournalLinkRoleIds.Related;
    public bool IsPlayerVisible { get; set; }
    public bool IsArchived { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object> ExtraData { get; set; } = new Dictionary<string, object>();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new Dictionary<string, object>();
}

public sealed class EventJournalAnnotationState : EntityBase
{
    public string EntryId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string AuthorUserId { get; set; } = string.Empty;
    public string AuthorDisplayName { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public bool IsPlayerVisible { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
    public bool IsArchived { get; set; }
    public Dictionary<string, object> ExtraData { get; set; } = new Dictionary<string, object>();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new Dictionary<string, object>();
}

public sealed class EventJournalAuditEntry : EntityBase
{
    public string EntryId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string ActionType { get; set; } = string.Empty;
    public string PerformedByUserId { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public DateTime PerformedAtUtc { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object> ExtraData { get; set; } = new Dictionary<string, object>();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new Dictionary<string, object>();
}
