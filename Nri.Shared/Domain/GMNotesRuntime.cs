using System;
using System.Collections.Generic;

namespace Nri.Shared.Domain;

public sealed class GMNoteState : EntityBase
{
    public string CampaignId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string FolderId { get; set; } = string.Empty;
    public string AuthorUserId { get; set; } = string.Empty;
    public string AuthorDisplayName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string NoteType { get; set; } = GMNoteTypeIds.Quick;
    public string Priority { get; set; } = "normal";
    public string ScopeType { get; set; } = string.Empty;
    public string ScopeEntityId { get; set; } = string.Empty;
    public string ScopeDisplayName { get; set; } = string.Empty;
    public string VisibilityMode { get; set; } = GMNoteVisibilityModeIds.AuthorOnly;
    public bool IsSharedWithGMs { get; set; }
    public bool IsPinned { get; set; }
    public bool IsQuickNote { get; set; }
    public bool IsArchived { get; set; }
    public int Revision { get; set; } = 1;
    public int SortOrder { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ArchivedAtUtc { get; set; }
    public string PublicSummary { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new List<string>();
    public Dictionary<string, object> ExtraData { get; set; } = new Dictionary<string, object>();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new Dictionary<string, object>();
}

public sealed class GMNoteFolderState : EntityBase
{
    public string CampaignId { get; set; } = string.Empty;
    public string ParentFolderId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string VisibilityMode { get; set; } = GMNoteVisibilityModeIds.AuthorOnly;
    public string OwnerUserId { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsArchived { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
    public string UpdatedByUserId { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new List<string>();
    public Dictionary<string, object> ExtraData { get; set; } = new Dictionary<string, object>();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new Dictionary<string, object>();
}

public sealed class GMNoteEntityLinkState : EntityBase
{
    public string NoteId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string EntityType { get; set; } = GMNoteEntityTypeIds.Custom;
    public string EntityId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string LinkRole { get; set; } = GMNoteLinkRoleIds.Related;
    public bool IsArchived { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
    public Dictionary<string, object> ExtraData { get; set; } = new Dictionary<string, object>();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new Dictionary<string, object>();
}

public sealed class GMNoteAuditEntry : EntityBase
{
    public string NoteId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string ActionType { get; set; } = string.Empty;
    public string PerformedByUserId { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public DateTime PerformedAtUtc { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object> ExtraData { get; set; } = new Dictionary<string, object>();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new Dictionary<string, object>();
}
