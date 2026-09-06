using System;
using System.Collections.Generic;

namespace Nri.Shared.Domain;

public static class CampaignMembershipStatusIds
{
    public const string Invited = "invited";
    public const string Active = "active";
    public const string Suspended = "suspended";
    public const string Left = "left";
    public const string Removed = "removed";
    public const string Archived = "archived";
}

public static class CampaignRoleIds
{
    public const string OwnerGM = "owner_gm";
    public const string CoGM = "co_gm";
    public const string Editor = "editor";
    public const string Player = "player";
    public const string Observer = "observer";
}

public static class CampaignCapabilityIds
{
    public const string CampaignView = "Campaign.View";
    public const string CampaignViewGMData = "Campaign.ViewGMData";
    public const string CampaignManageSettings = "Campaign.ManageSettings";
    public const string CampaignManageMemberships = "Campaign.ManageMemberships";
    public const string CampaignTransferOwnership = "Campaign.TransferOwnership";
    public const string CampaignEditContent = "Campaign.EditContent";
    public const string CampaignViewAudit = "Campaign.ViewAudit";
    public const string SessionView = "Session.View";
    public const string SessionCreate = "Session.Create";
    public const string SessionRun = "Session.Run";
    public const string SessionEdit = "Session.Edit";
    public const string SessionManageParticipants = "Session.ManageParticipants";
    public const string SessionViewGMData = "Session.ViewGMData";
    public const string CharacterViewPlayerSafe = "Character.ViewPlayerSafe";
    public const string CharacterManageOwned = "Character.ManageOwned";
    public const string CharacterManageAnyInCampaign = "Character.ManageAnyInCampaign";
    public const string MapViewGM = "Map.ViewGM";
    public const string MapEdit = "Map.Edit";
    public const string CombatRun = "Combat.Run";
    public const string TravelRun = "Travel.Run";
    public const string WeatherRun = "Weather.Run";
    public const string AutomationView = "Automation.View";
    public const string AutomationManage = "Automation.Manage";
    public const string AutomationApprove = "Automation.Approve";
    public const string ReferenceDataEditCampaignBound = "ReferenceData.EditCampaignBound";
}

public sealed class CampaignMembership : EntityBase
{
    public string CampaignId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string PrimaryRoleId { get; set; } = CampaignRoleIds.Player;
    public List<string> AdditionalRoleIds { get; set; } = new();
    public List<string> CapabilityGrants { get; set; } = new();
    public List<string> CapabilityDenials { get; set; } = new();
    public string Status { get; set; } = CampaignMembershipStatusIds.Active;
    public DateTime JoinedAtUtc { get; set; } = DateTime.UtcNow;
    public string InvitedByUserId { get; set; } = string.Empty;
    public DateTime? AcceptedAtUtc { get; set; }
    public long EntityRevision { get; set; } = 1;
    public bool IsArchived { get; set; }
    public string ArchiveReason { get; set; } = string.Empty;
}

public sealed class CampaignCapabilityDefinition : EntityBase
{
    public string CapabilityId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsHighRisk { get; set; }
}

public static class SessionParticipationRoleIds
{
    public const string LeadGM = "lead_gm";
    public const string AssistantGM = "assistant_gm";
    public const string Player = "player";
    public const string Observer = "observer";
}

public sealed class SessionParticipation : EntityBase
{
    public string CampaignId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string ParticipationRoleId { get; set; } = SessionParticipationRoleIds.Player;
    public List<string> AllowedCharacterIds { get; set; } = new();
    public string ActiveCharacterId { get; set; } = string.Empty;
    public List<string> CapabilityGrants { get; set; } = new();
    public List<string> CapabilityDenials { get; set; } = new();
    public string Status { get; set; } = CampaignMembershipStatusIds.Active;
    public DateTime JoinedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LeftAtUtc { get; set; }
    public long EntityRevision { get; set; } = 1;
}

public sealed class ActiveGameContext
{
    public string AuthSessionId { get; set; } = string.Empty;
    public string ConnectionId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string ActiveCharacterId { get; set; } = string.Empty;
    public long ContextRevision { get; set; }
    public DateTime SelectedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastValidatedAtUtc { get; set; } = DateTime.UtcNow;
    public bool SuperAdminOverrideActive { get; set; }
    public string SuperAdminOverrideReason { get; set; } = string.Empty;
}

public sealed class ActiveGameContextPreference : EntityBase
{
    public string UserId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string CharacterId { get; set; } = string.Empty;
    public DateTime LastUsedAtUtc { get; set; } = DateTime.UtcNow;
}

public static class AutomationDecisionModeIds
{
    public const string AutoApplySafe = "auto_apply_safe";
    public const string RequireGMApproval = "require_gm_approval";
    public const string NotifyOnly = "notify_only";
    public const string Disabled = "disabled";
}

public sealed class AutomationPolicyDefinition : EntityBase
{
    public string CampaignId { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string TriggerKind { get; set; } = string.Empty;
    public string TargetDomainAction { get; set; } = string.Empty;
    public string DecisionMode { get; set; } = AutomationDecisionModeIds.Disabled;
    public List<string> Preconditions { get; set; } = new();
    public int CooldownSeconds { get; set; }
    public List<string> SessionModeFilter { get; set; } = new();
    public string Visibility { get; set; } = "gm";
    public bool Enabled { get; set; }
    public int Priority { get; set; }
    public long EntityRevision { get; set; } = 1;
    public string PublicDescription { get; set; } = string.Empty;
    public string GMDescription { get; set; } = string.Empty;
}

public static class AutomationExecutionStatusIds
{
    public const string Proposed = "proposed";
    public const string Approved = "approved";
    public const string Declined = "declined";
    public const string Applied = "applied";
    public const string Failed = "failed";
    public const string Superseded = "superseded";
}

public sealed class AutomationExecutionRecord : EntityBase
{
    public string CampaignId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string PolicyId { get; set; } = string.Empty;
    public string Trigger { get; set; } = string.Empty;
    public string DecisionMode { get; set; } = AutomationDecisionModeIds.Disabled;
    public string TargetAction { get; set; } = string.Empty;
    public string Status { get; set; } = AutomationExecutionStatusIds.Proposed;
    public string OperationId { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public string CausationId { get; set; } = string.Empty;
    public int AutomationDepth { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
    public DateTime? AppliedAtUtc { get; set; }
    public string DecidedByUserId { get; set; } = string.Empty;
    public string ReadableResult { get; set; } = string.Empty;
    public string FailureCategory { get; set; } = string.Empty;
    public long EntityRevision { get; set; } = 1;
}

public sealed class SessionAttentionItem
{
    public string SourceType { get; set; } = string.Empty;
    public string SourceId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Severity { get; set; } = "normal";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string ActorDisplayName { get; set; } = string.Empty;
    public string ActionRoute { get; set; } = string.Empty;
}
