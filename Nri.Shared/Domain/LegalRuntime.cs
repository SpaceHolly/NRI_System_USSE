using System;
using System.Collections.Generic;

namespace Nri.Shared.Domain;

public static class LegalFeatureFlags
{
    public const bool UseLegalMvp = false;
    public const bool UseJurisdictionProfiles = false;
    public const bool UseLegalActionChecks = false;
    public const bool UseLicenseDefinitions = false;
    public const bool UseEntityLicenses = false;
    public const bool UseLicenseApplications = false;
    public const bool UsePermits = false;
    public const bool UseLegalRestrictions = false;
    public const bool UseLegalRequirementChecks = false;
    public const bool UseDeJureDeFactoLaw = false;
    public const bool UseEnforcementRisk = false;
    public const bool UseWhiteGrayShadowProduction = false;
    public const bool UseLegalCraftingIntegration = false;
    public const bool UseLegalEngineeringIntegration = false;
    public const bool UseLegalFactoryOrderIntegration = false;
    public const bool UseLegalManufacturingIntegration = false;
    public const bool UseLegalInventoryAssetIntegration = false;
    public const bool UseLegalPlayerView = false;
    public const bool UseLegalAdminView = false;
    public const bool UseLegalRequestIntegration = false;
    public const bool UseLegalJournalIntegration = false;
    public const bool UseLegalSearchIntegration = false;
    public const bool UseLegalSyncEvents = false;
}

public static class LegalActionTypeIds
{
    public const string Research = "research";
    public const string Design = "design";
    public const string Craft = "craft";
    public const string Manufacture = "manufacture";
    public const string Buy = "buy";
    public const string Sell = "sell";
    public const string Own = "own";
    public const string CarryPublic = "carry_public";
    public const string Store = "store";
    public const string Transport = "transport";
    public const string Operate = "operate";
    public const string Use = "use";
    public const string Import = "import";
    public const string Export = "export";
    public const string Transfer = "transfer";
    public const string FactoryOrder = "factory_order";
}

public static class LegalStatusIds
{
    public const string Legal = "legal";
    public const string Restricted = "restricted";
    public const string LicenseRequired = "license_required";
    public const string PermitRequired = "permit_required";
    public const string GMReviewRequired = "gm_review_required";
    public const string Illegal = "illegal";
    public const string Unknown = "unknown";
}

public static class LegalRiskLevelIds
{
    public const string None = "none";
    public const string Low = "low";
    public const string Medium = "medium";
    public const string High = "high";
    public const string Severe = "severe";
    public const string Unknown = "unknown";
}

public static class ProductionLegalityModeIds
{
    public const string White = "white";
    public const string Gray = "gray";
    public const string Shadow = "shadow";
}

public static class LegalSubjectKindIds
{
    public const string Any = "any";
    public const string Character = "character";
    public const string Group = "group";
    public const string Organization = "organization";
    public const string Faction = "faction";
    public const string Facility = "facility";
    public const string Company = "company";
    public const string State = "state";
    public const string Custom = "custom";
}

public static class LicenseStatusIds
{
    public const string Active = "active";
    public const string Suspended = "suspended";
    public const string Revoked = "revoked";
    public const string Expired = "expired";
}

public static class LicenseApplicationStatusIds
{
    public const string Draft = "draft";
    public const string Submitted = "submitted";
    public const string InReview = "in_review";
    public const string Approved = "approved";
    public const string Rejected = "rejected";
    public const string Issued = "issued";
    public const string Cancelled = "cancelled";
    public const string Archived = "archived";
}

public sealed class LegalSubjectClassifier : EntityBase
{
    public string CampaignId { get; set; } = "default";
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string SubjectKind { get; set; } = LegalSubjectKindIds.Custom;
    public string SubjectStatus { get; set; } = string.Empty;
    public string JurisdictionId { get; set; } = string.Empty;
    public bool IsPlayerVisible { get; set; }
    public bool IsArchived { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string UpdatedByUserId { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public Dictionary<string, object> ExtraData { get; set; } = new();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new();
}

public sealed class JurisdictionDefinition : EntityBase
{
    public string CampaignId { get; set; } = "default";
    public string RuleSetId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string JurisdictionType { get; set; } = "country";
    public string ParentJurisdictionId { get; set; } = string.Empty;
    public string LinkedEntityType { get; set; } = string.Empty;
    public string LinkedEntityId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string PublicSummary { get; set; } = string.Empty;
    public bool IsPlayerVisible { get; set; }
    public string VisibilityMode { get; set; } = "gm_only";
    public bool IsArchived { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
    public string UpdatedByUserId { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public Dictionary<string, object> ExtraData { get; set; } = new();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new();
}

public sealed class LegalProfileState : EntityBase
{
    public string CampaignId { get; set; } = "default";
    public string JurisdictionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool IsArchived { get; set; }
    public DateTime EffectiveFromUtc { get; set; } = DateTime.UtcNow;
    public DateTime? EffectiveToUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
    public string UpdatedByUserId { get; set; } = string.Empty;
    public Dictionary<string, object> ExtraData { get; set; } = new();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new();
}

public sealed class LegalRuleDefinition : EntityBase
{
    public string CampaignId { get; set; } = "default";
    public string JurisdictionId { get; set; } = string.Empty;
    public string LegalProfileId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ActionType { get; set; } = LegalActionTypeIds.Own;
    public string SubjectKind { get; set; } = "any";
    public string SubjectStatus { get; set; } = string.Empty;
    public string ObjectType { get; set; } = "any";
    public string ObjectCategory { get; set; } = string.Empty;
    public List<string> ObjectTags { get; set; } = new();
    public string LegalStatus { get; set; } = LegalStatusIds.Unknown;
    public string DeJureStatus { get; set; } = LegalStatusIds.Unknown;
    public string DeFactoStatus { get; set; } = LegalStatusIds.Unknown;
    public string RequiredLicenseDefinitionId { get; set; } = string.Empty;
    public string RequiredPermitType { get; set; } = string.Empty;
    public bool RequiresGMReview { get; set; }
    public bool IsBlocked { get; set; }
    public string RiskLevel { get; set; } = LegalRiskLevelIds.None;
    public int Priority { get; set; }
    public bool IsPlayerVisible { get; set; }
    public string PublicWarning { get; set; } = string.Empty;
    public string AdminNotes { get; set; } = string.Empty;
    public string GMHiddenLegalTerms { get; set; } = string.Empty;
    public bool IsArchived { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
    public string UpdatedByUserId { get; set; } = string.Empty;
    public Dictionary<string, object> ExtraData { get; set; } = new();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new();
}

public sealed class LicenseDefinition : EntityBase
{
    public string CampaignId { get; set; } = "default";
    public string JurisdictionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string LicenseType { get; set; } = "general";
    public string AppliesToActionType { get; set; } = string.Empty;
    public string AppliesToObjectType { get; set; } = string.Empty;
    public string AppliesToObjectCategory { get; set; } = string.Empty;
    public string PublicSummary { get; set; } = string.Empty;
    public string AdminNotes { get; set; } = string.Empty;
    public bool RequiresGMApproval { get; set; } = true;
    public bool IsPlayerVisible { get; set; } = true;
    public bool IsArchived { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
    public string UpdatedByUserId { get; set; } = string.Empty;
    public Dictionary<string, object> ExtraData { get; set; } = new();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new();
}

public sealed class EntityLicenseState : EntityBase
{
    public string CampaignId { get; set; } = "default";
    public string LicenseDefinitionId { get; set; } = string.Empty;
    public string JurisdictionId { get; set; } = string.Empty;
    public string HolderEntityType { get; set; } = "character";
    public string HolderEntityId { get; set; } = string.Empty;
    public string HolderUserId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Status { get; set; } = "active";
    public DateTime IssuedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAtUtc { get; set; }
    public string IssuedByUserId { get; set; } = string.Empty;
    public string PublicNotes { get; set; } = string.Empty;
    public string GMNotes { get; set; } = string.Empty;
    public bool IsPlayerVisible { get; set; } = true;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object> ExtraData { get; set; } = new();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new();
}

public sealed class LicenseApplicationState : EntityBase
{
    public string CampaignId { get; set; } = "default";
    public string LicenseDefinitionId { get; set; } = string.Empty;
    public string JurisdictionId { get; set; } = string.Empty;
    public string ApplicantUserId { get; set; } = string.Empty;
    public string ApplicantEntityType { get; set; } = "character";
    public string ApplicantEntityId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = LicenseApplicationStatusIds.Submitted;
    public string LinkedRequestId { get; set; } = string.Empty;
    public string GMResponse { get; set; } = string.Empty;
    public string GMNotes { get; set; } = string.Empty;
    public bool IsPlayerVisible { get; set; } = true;
    public DateTime SubmittedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string ReviewedByUserId { get; set; } = string.Empty;
    public DateTime? ReviewedAtUtc { get; set; }
    public Dictionary<string, object> ExtraData { get; set; } = new();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new();
}

public sealed class PermitState : EntityBase
{
    public string CampaignId { get; set; } = "default";
    public string PermitType { get; set; } = string.Empty;
    public string HolderEntityType { get; set; } = string.Empty;
    public string HolderEntityId { get; set; } = string.Empty;
    public string JurisdictionId { get; set; } = string.Empty;
    public string Status { get; set; } = "active";
    public bool IsPlayerVisible { get; set; } = true;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class LegalRequirementState : EntityBase
{
    public string CampaignId { get; set; } = "default";
    public string SourceEntityType { get; set; } = string.Empty;
    public string SourceEntityId { get; set; } = string.Empty;
    public string RequirementType { get; set; } = string.Empty;
    public string RequirementSummary { get; set; } = string.Empty;
    public bool IsSatisfied { get; set; }
    public bool IsPlayerVisible { get; set; } = true;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class LegalRestrictionState : EntityBase
{
    public string CampaignId { get; set; } = "default";
    public string SourceEntityType { get; set; } = string.Empty;
    public string SourceEntityId { get; set; } = string.Empty;
    public string RestrictionType { get; set; } = string.Empty;
    public string LegalStatus { get; set; } = LegalStatusIds.Unknown;
    public string PublicSummary { get; set; } = string.Empty;
    public string GMNotes { get; set; } = string.Empty;
    public bool IsPlayerVisible { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class EnforcementRiskProfile : EntityBase
{
    public string CampaignId { get; set; } = "default";
    public string JurisdictionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string RiskLevel { get; set; } = LegalRiskLevelIds.Low;
    public string DeJureSummary { get; set; } = string.Empty;
    public string DeFactoSummary { get; set; } = string.Empty;
    public string PlayerSafeSummary { get; set; } = string.Empty;
    public string GMOnlyDetails { get; set; } = string.Empty;
    public bool IsPlayerVisible { get; set; }
    public bool IsArchived { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class DeJureDeFactoLawState : EntityBase
{
    public string CampaignId { get; set; } = "default";
    public string JurisdictionId { get; set; } = string.Empty;
    public string LegalProfileId { get; set; } = string.Empty;
    public string DeJureSummary { get; set; } = string.Empty;
    public string DeFactoSummary { get; set; } = string.Empty;
    public bool IsPlayerVisible { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class ProductionLegalityState : EntityBase
{
    public string CampaignId { get; set; } = "default";
    public string SourceEntityType { get; set; } = "factory_order";
    public string SourceEntityId { get; set; } = string.Empty;
    public string JurisdictionId { get; set; } = string.Empty;
    public string ProductionMode { get; set; } = ProductionLegalityModeIds.White;
    public string LegalStatus { get; set; } = LegalStatusIds.Unknown;
    public string PublicSummary { get; set; } = string.Empty;
    public string GMNotes { get; set; } = string.Empty;
    public string ApprovedByUserId { get; set; } = string.Empty;
    public DateTime? ApprovedAtUtc { get; set; }
    public bool IsPlayerVisible { get; set; } = true;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object> ExtraData { get; set; } = new();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new();
}

public sealed class LegalCheckRecordState : EntityBase
{
    public string CampaignId { get; set; } = "default";
    public string JurisdictionId { get; set; } = string.Empty;
    public string ActorUserId { get; set; } = string.Empty;
    public string ActorEntityType { get; set; } = string.Empty;
    public string ActorEntityId { get; set; } = string.Empty;
    public string ActionType { get; set; } = LegalActionTypeIds.Own;
    public string ObjectType { get; set; } = string.Empty;
    public string ObjectCategory { get; set; } = string.Empty;
    public string ObjectEntityId { get; set; } = string.Empty;
    public string ObjectDisplayName { get; set; } = string.Empty;
    public string LegalStatus { get; set; } = LegalStatusIds.Unknown;
    public string DeJureStatus { get; set; } = LegalStatusIds.Unknown;
    public string DeFactoStatus { get; set; } = LegalStatusIds.Unknown;
    public bool IsBlocked { get; set; }
    public bool CanProceedWithWarning { get; set; }
    public bool RequiresGMReview { get; set; }
    public string RiskLevel { get; set; } = LegalRiskLevelIds.None;
    public string RequiredLicenseDefinitionId { get; set; } = string.Empty;
    public string MatchedRuleId { get; set; } = string.Empty;
    public string PublicSummary { get; set; } = string.Empty;
    public string AdminSummary { get; set; } = string.Empty;
    public string GMOnlyDetails { get; set; } = string.Empty;
    public bool IsPlayerVisible { get; set; } = true;
    public DateTime CheckedAtUtc { get; set; } = DateTime.UtcNow;
    public string CheckedByUserId { get; set; } = string.Empty;
    public Dictionary<string, object> ExtraData { get; set; } = new();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new();
}

public sealed class LegalCheckRequest
{
    public string CampaignId { get; set; } = "default";
    public string JurisdictionId { get; set; } = string.Empty;
    public string ActorUserId { get; set; } = string.Empty;
    public string ActorEntityType { get; set; } = string.Empty;
    public string ActorEntityId { get; set; } = string.Empty;
    public string SubjectKind { get; set; } = LegalSubjectKindIds.Any;
    public string SubjectStatus { get; set; } = string.Empty;
    public string ActionType { get; set; } = LegalActionTypeIds.Own;
    public string ObjectType { get; set; } = string.Empty;
    public string ObjectCategory { get; set; } = string.Empty;
    public string ObjectEntityId { get; set; } = string.Empty;
    public string ObjectDisplayName { get; set; } = string.Empty;
    public string ProductionMode { get; set; } = string.Empty;
    public List<string> ObjectTags { get; set; } = new();
}

public sealed class LegalCheckResult
{
    public string CampaignId { get; set; } = "default";
    public string JurisdictionId { get; set; } = string.Empty;
    public string LegalProfileId { get; set; } = string.Empty;
    public string MatchedRuleId { get; set; } = string.Empty;
    public string ActionType { get; set; } = LegalActionTypeIds.Own;
    public string ObjectType { get; set; } = string.Empty;
    public string ObjectCategory { get; set; } = string.Empty;
    public string LegalStatus { get; set; } = LegalStatusIds.Unknown;
    public string DeJureStatus { get; set; } = LegalStatusIds.Unknown;
    public string DeFactoStatus { get; set; } = LegalStatusIds.Unknown;
    public bool IsBlocked { get; set; }
    public bool CanProceedWithWarning { get; set; }
    public bool RequiresGMReview { get; set; }
    public string RiskLevel { get; set; } = LegalRiskLevelIds.None;
    public string RequiredLicenseDefinitionId { get; set; } = string.Empty;
    public bool HasRequiredLicense { get; set; }
    public string PlayerSafeMessage { get; set; } = string.Empty;
    public string AdminSummary { get; set; } = string.Empty;
    public List<string> Warnings { get; set; } = new();
    public DateTime BuiltAtUtc { get; set; } = DateTime.UtcNow;
}
