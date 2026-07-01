using System;
using System.Collections.Generic;

namespace Nri.Shared.Domain;

public enum RequestStatus
{
    Pending,
    Approved,
    Rejected,
    Cancelled,
    Expired,
    Archived
}

public enum RequestVisibility
{
    Public = 0,
    HiddenToAdmins = 1,
    AdminOnly = 2,
    PlayerShadow = 1,
    AdminOnlyShadow = 2
}

public class RequestDecision
{
    public string? DecidedByUserId { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
    public string AdminComment { get; set; } = string.Empty;
}

public class RequestHistoryEntry
{
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public string ActorUserId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
}

public abstract class RequestBase : EntityBase
{
    public string RequestType { get; set; } = string.Empty;
    public string CreatorUserId { get; set; } = string.Empty;
    public string RelatedUserId { get; set; } = string.Empty;
    public string? CharacterId { get; set; }
    public RequestStatus Status { get; set; } = RequestStatus.Pending;
    public string Description { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
    public string Fingerprint { get; set; } = string.Empty;
    public int RejectionCountForFingerprint { get; set; }
    public RequestDecision Decision { get; set; } = new RequestDecision();
    public List<RequestHistoryEntry> History { get; set; } = new List<RequestHistoryEntry>();
}

public class ActionRequest : RequestBase
{
    public string ActionCode { get; set; } = string.Empty;
}

public class DiceFormulaSpec
{
    public int DiceCount { get; set; }
    public int DiceSides { get; set; }
    public int Modifier { get; set; }
    public string Normalized { get; set; } = "1d20";
}

public class DiceRollResult
{
    public string NormalizedFormula { get; set; } = "1d20";
    public List<int> Rolls { get; set; } = new List<int>();
    public List<int> BaseRolls { get; set; } = new List<int>();
    public List<int?> FateRolls { get; set; } = new List<int?>();
    public List<bool> FateAppliedByDie { get; set; } = new List<bool>();
    public int Modifier { get; set; }
    public int Total { get; set; }
    public RequestVisibility Visibility { get; set; } = RequestVisibility.Public;
    public string ApprovedByUserId { get; set; } = string.Empty;
    public DateTime ApprovedAtUtc { get; set; } = DateTime.UtcNow;
    public string SoundKey { get; set; } = "dice_1";
    public bool SoundEasterTriggered { get; set; }
}

public class DiceRollRequest : RequestBase
{
    public string RawFormula { get; set; } = "1d20";
    public DiceFormulaSpec Formula { get; set; } = new DiceFormulaSpec();
    public RequestVisibility Visibility { get; set; } = RequestVisibility.Public;
    public bool IsTestRoll { get; set; }
    public string TestRollOwnerUserId { get; set; } = string.Empty;
    public DiceRollResult? Result { get; set; }
}

public class CharacterApplicationRequest : RequestBase
{
    public string ApplicantUserId { get; set; } = string.Empty;
    public string CharacterConcept { get; set; } = string.Empty;
}

public static class PlayerRequestTypeIds
{
    public const string GenericAction = "generic_action";
    public const string DevelopmentUnlock = "development_unlock";
    public const string ItemRequest = "item_request";
    public const string RulesQuestion = "rules_question";
    public const string General = "general";
    public const string Action = "action";
    public const string Question = "question";
    public const string CharacterChange = "character_change";
    public const string CharacterAssignment = "character_assignment";
    public const string OwnershipTransfer = "ownership_transfer";
    public const string Purchase = "purchase";
    public const string Research = "research";
    public const string Crafting = "crafting";
    public const string EngineeringDesign = "engineering_design";
    public const string FactoryQuote = "factory_quote";
    public const string FactoryOrder = "factory_order";
    public const string ItemCreation = "item_creation";
    public const string EquipmentChange = "equipment_change";
    public const string SceneAction = "scene_action";
    public const string InformationRequest = "information_request";
    public const string MapRequest = "map_request";
    public const string Custom = "custom";
}

public static class PlayerRequestStatusIds
{
    public const string Draft = "draft";
    public const string Submitted = "submitted";
    public const string InReview = "in_review";
    public const string ChangesRequested = "changes_requested";
    public const string Approved = "approved";
    public const string Rejected = "rejected";
    public const string Cancelled = "cancelled";
    public const string Fulfilled = "fulfilled";
    public const string Archived = "archived";
}

public static class PlayerRequestPriorityIds
{
    public const string Low = "low";
    public const string Normal = "normal";
    public const string High = "high";
    public const string Urgent = "urgent";
}

public static class PlayerRequestCommentAuthorRoleIds
{
    public const string Player = "player";
    public const string GM = "gm";
    public const string System = "system";
}

public sealed class PlayerRequestState : EntityBase
{
    public string RequestNumber { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string GroupId { get; set; } = string.Empty;
    public string CharacterId { get; set; } = string.Empty;
    public string CharacterName { get; set; } = string.Empty;
    public string CompanionId { get; set; } = string.Empty;
    public string OwnerUserId { get; set; } = string.Empty;
    public string CreatedByUserId { get; set; } = string.Empty;
    public string CreatedByDisplayName { get; set; } = string.Empty;
    public string RequestType { get; set; } = PlayerRequestTypeIds.General;
    public string Category { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = PlayerRequestStatusIds.Draft;
    public string Priority { get; set; } = PlayerRequestPriorityIds.Normal;
    public string VisibilityMode { get; set; } = "party";
    public bool IsPlayerVisible { get; set; } = true;
    public string LinkedEntityType { get; set; } = string.Empty;
    public string LinkedEntityId { get; set; } = string.Empty;
    public string ProposalType { get; set; } = string.Empty;
    public string ProposalPayloadSummary { get; set; } = string.Empty;
    public PlayerRequestProposalDraft ProposalPayload { get; set; } = new PlayerRequestProposalDraft();
    public string GMResponse { get; set; } = string.Empty;
    public string ResolutionReason { get; set; } = string.Empty;
    public string PlayerVisibleText { get; set; } = string.Empty;
    public string AdminOnlyNotes { get; set; } = string.Empty;
    public string Decision { get; set; } = string.Empty;
    public string DecisionCommentPlayerVisible { get; set; } = string.Empty;
    public string DecisionCommentGMOnly { get; set; } = string.Empty;
    public string AssignedAdminUserId { get; set; } = string.Empty;
    public string ReviewedByUserId { get; set; } = string.Empty;
    public string ReviewedByDisplayName { get; set; } = string.Empty;
    public string DecidedByUserId { get; set; } = string.Empty;
    public string DecidedByDisplayName { get; set; } = string.Empty;
    public string CancelledByUserId { get; set; } = string.Empty;
    public string CancelledByDisplayName { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? SubmittedAtUtc { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public string ResubmittedFromRequestId { get; set; } = string.Empty;
    public int ResubmissionCount { get; set; }
    public int Revision { get; set; }
    public bool IsArchived { get; set; }
    public bool IsDeleted { get; set; }
    public string PublicNotes { get; set; } = string.Empty;
    public string GMNotes { get; set; } = string.Empty;
    public List<PlayerRequestAuditEntry> AuditTrail { get; set; } = new List<PlayerRequestAuditEntry>();
    public List<string> Tags { get; set; } = new List<string>();
    public Dictionary<string, object> ExtraData { get; set; } = new Dictionary<string, object>();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new Dictionary<string, object>();
}

public sealed class PlayerRequestAuditEntry
{
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public string ActorUserId { get; set; } = string.Empty;
    public string ActorDisplayName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string FromStatus { get; set; } = string.Empty;
    public string ToStatus { get; set; } = string.Empty;
    public string PlayerVisibleComment { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
}

public sealed class PlayerRequestCommentState : EntityBase
{
    public string RequestId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string AuthorUserId { get; set; } = string.Empty;
    public string AuthorDisplayName { get; set; } = string.Empty;
    public string AuthorRole { get; set; } = PlayerRequestCommentAuthorRoleIds.Player;
    public string Text { get; set; } = string.Empty;
    public bool IsPlayerVisible { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
    public bool IsArchived { get; set; }
    public List<string> Tags { get; set; } = new List<string>();
    public Dictionary<string, object> ExtraData { get; set; } = new Dictionary<string, object>();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new Dictionary<string, object>();
}

public sealed class PlayerRequestProposalDraft
{
    public string ProposalType { get; set; } = string.Empty;
    public int SchemaVersion { get; set; } = 1;
    public string DisplaySummary { get; set; } = string.Empty;
    public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
    public string EstimatedResult { get; set; } = string.Empty;
    public List<string> Warnings { get; set; } = new List<string>();
    public bool RequiresGMApproval { get; set; } = true;
}
