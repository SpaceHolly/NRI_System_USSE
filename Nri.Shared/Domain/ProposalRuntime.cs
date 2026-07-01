using System;
using System.Collections.Generic;

namespace Nri.Shared.Domain;

public static class ProposalFeatureFlags
{
    public const bool UsePlayerProposalCenter = false;
    public const bool UseStructuredProposalDrafts = false;
    public const bool UseProposalEditors = false;
    public const bool UseProposalValidation = false;
    public const bool UseProposalPreview = false;
    public const bool UseProposalSubmitFlow = false;
    public const bool UseProposalReviewWorkspace = false;
    public const bool UseProposalConversionFlow = false;
    public const bool UseResearchProposalEditor = false;
    public const bool UseCraftingProposalEditor = false;
    public const bool UseEngineeringProposalEditor = false;
    public const bool UseFactoryOrderProposalEditor = false;
    public const bool UseManufacturingProposalEditor = false;
    public const bool UseLegalProposalEditor = false;
    public const bool UseDevelopmentProposalEditor = false;
    public const bool UseCustomProposalEditor = false;
    public const bool UseProposalRequestIntegration = false;
    public const bool UseProposalProjectIntegration = false;
    public const bool UseProposalNextActions = false;
    public const bool UseProposalActiveProcesses = false;
    public const bool UseProposalJournalIntegration = false;
    public const bool UseProposalSearchIntegration = false;
    public const bool UseProposalSyncEvents = false;
}

public static class ProposalTypeIds
{
    public const string Research = "research";
    public const string Crafting = "crafting";
    public const string EngineeringDesign = "engineering_design";
    public const string FactoryQuote = "factory_quote";
    public const string FactoryOrder = "factory_order";
    public const string Manufacturing = "manufacturing";
    public const string LegalCheck = "legal_check";
    public const string LicenseApplication = "license_application";
    public const string DevelopmentPurchase = "development_purchase";
    public const string InventoryAction = "inventory_action";
    public const string AssetTransfer = "asset_transfer";
    public const string CustomProject = "custom_project";
    public const string GenericGmRequest = "generic_gm_request";
    public const string Custom = "custom";
}

public static class ProposalCategoryIds
{
    public const string Knowledge = "knowledge";
    public const string Item = "item";
    public const string Technology = "technology";
    public const string Vehicle = "vehicle";
    public const string Production = "production";
    public const string Law = "law";
    public const string CharacterDevelopment = "character_development";
    public const string Inventory = "inventory";
    public const string Asset = "asset";
    public const string WorldAction = "world_action";
    public const string Custom = "custom";
}

public static class ProposalStatusIds
{
    public const string Draft = "draft";
    public const string ReadyToSubmit = "ready_to_submit";
    public const string Submitted = "submitted";
    public const string LinkedToRequest = "linked_to_request";
    public const string InGmReview = "in_gm_review";
    public const string ChangesRequested = "changes_requested";
    public const string Approved = "approved";
    public const string Rejected = "rejected";
    public const string Converted = "converted";
    public const string Cancelled = "cancelled";
    public const string Archived = "archived";
}

public static class ProposalValidationStatusIds
{
    public const string Valid = "valid";
    public const string ValidWithWarnings = "valid_with_warnings";
    public const string MissingRequiredFields = "missing_required_fields";
    public const string InvalidReferences = "invalid_references";
    public const string Forbidden = "forbidden";
    public const string RequiresGmReview = "requires_gm_review";
    public const string Blocked = "blocked";
}

public static class ProposalReviewStatusIds
{
    public const string Pending = "pending";
    public const string InReview = "in_review";
    public const string ChangesRequested = "changes_requested";
    public const string Approved = "approved";
    public const string Rejected = "rejected";
    public const string Converted = "converted";
    public const string Cancelled = "cancelled";
}

public static class ProposalConversionTypeIds
{
    public const string CreateResearchProject = "create_research_project";
    public const string CreateCraftingProject = "create_crafting_project";
    public const string CreateEngineeringProject = "create_engineering_project";
    public const string CreateFactoryQuote = "create_factory_quote";
    public const string CreateFactoryOrder = "create_factory_order";
    public const string CreateManufacturingProject = "create_manufacturing_project";
    public const string CreateLegalCheck = "create_legal_check";
    public const string CreateLicenseApplication = "create_license_application";
    public const string CreateDevelopmentPurchaseRequest = "create_development_purchase_request";
    public const string CreateGenericProject = "create_generic_project";
    public const string LinkExistingEntity = "link_existing_entity";
    public const string RejectNoConversion = "reject_no_conversion";
    public const string Custom = "custom";
}

public sealed class PlayerProposalDraftState : EntityBase
{
    public string ProposalDraftId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string CreatedByUserId { get; set; } = string.Empty;
    public string CreatedByDisplayName { get; set; } = string.Empty;
    public string OwnerEntityType { get; set; } = string.Empty;
    public string OwnerEntityId { get; set; } = string.Empty;
    public string CharacterId { get; set; } = string.Empty;
    public string CompanionId { get; set; } = string.Empty;
    public string GroupId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ProposalType { get; set; } = ProposalTypeIds.GenericGmRequest;
    public string ProposalCategory { get; set; } = ProposalCategoryIds.Custom;
    public string ProposalStatus { get; set; } = ProposalStatusIds.Draft;
    public string Priority { get; set; } = PlayerRequestPriorityIds.Normal;
    public string SourceView { get; set; } = string.Empty;
    public string SourceEntityType { get; set; } = string.Empty;
    public string SourceEntityId { get; set; } = string.Empty;
    public List<string> RelatedKnowledgeIds { get; set; } = new List<string>();
    public List<string> RelatedRecipeIds { get; set; } = new List<string>();
    public List<string> RelatedBlueprintIds { get; set; } = new List<string>();
    public List<string> RelatedAssetIds { get; set; } = new List<string>();
    public List<string> RelatedProjectIds { get; set; } = new List<string>();
    public List<string> RelatedInventoryItemIds { get; set; } = new List<string>();
    public List<string> RelatedLicenseIds { get; set; } = new List<string>();
    public List<string> RelatedFacilityIds { get; set; } = new List<string>();
    public Dictionary<string, object> StructuredPayload { get; set; } = new Dictionary<string, object>();
    public string PublicSummary { get; set; } = string.Empty;
    public string PlayerComment { get; set; } = string.Empty;
    public string GMReviewSummary { get; set; } = string.Empty;
    public string ValidationSummary { get; set; } = string.Empty;
    public string LinkedPlayerRequestId { get; set; } = string.Empty;
    public string LinkedProjectId { get; set; } = string.Empty;
    public string LinkedSpecializedEntityType { get; set; } = string.Empty;
    public string LinkedSpecializedEntityId { get; set; } = string.Empty;
    public string LinkedResultEntityType { get; set; } = string.Empty;
    public string LinkedResultEntityId { get; set; } = string.Empty;
    public DateTime? ConvertedAtUtc { get; set; }
    public string ConvertedByUserId { get; set; } = string.Empty;
    public bool IsPlayerVisible { get; set; } = true;
    public string VisibilityMode { get; set; } = ProjectVisibilityModeIds.OwnerOnly;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? SubmittedAtUtc { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public DateTime? ArchivedAtUtc { get; set; }
    public List<string> Tags { get; set; } = new List<string>();
    public Dictionary<string, object> ExtraData { get; set; } = new Dictionary<string, object>();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new Dictionary<string, object>();
}

public sealed class PlayerProposalFieldState : EntityBase
{
    public string FieldId { get; set; } = string.Empty;
    public string ProposalDraftId { get; set; } = string.Empty;
    public string FieldKey { get; set; } = string.Empty;
    public string FieldLabel { get; set; } = string.Empty;
    public string FieldType { get; set; } = "text";
    public string FieldValue { get; set; } = string.Empty;
    public string DisplayValue { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public bool IsPlayerEditable { get; set; } = true;
    public bool IsGMOnly { get; set; }
    public string ValidationStatus { get; set; } = ProposalValidationStatusIds.Valid;
    public string ValidationMessage { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new List<string>();
    public Dictionary<string, object> ExtraData { get; set; } = new Dictionary<string, object>();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new Dictionary<string, object>();
}

public sealed class PlayerProposalAttachmentLinkState : EntityBase
{
    public string ProposalDraftId { get; set; } = string.Empty;
    public string AttachmentType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsPlayerVisible { get; set; } = true;
    public string VisibilityMode { get; set; } = ProjectVisibilityModeIds.OwnerOnly;
    public Dictionary<string, object> ExtraData { get; set; } = new Dictionary<string, object>();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new Dictionary<string, object>();
}

public sealed class PlayerProposalValidationResult : EntityBase
{
    public string ValidationId { get; set; } = string.Empty;
    public string ProposalDraftId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string Status { get; set; } = ProposalValidationStatusIds.Valid;
    public string Summary { get; set; } = string.Empty;
    public List<string> Errors { get; set; } = new List<string>();
    public List<string> Warnings { get; set; } = new List<string>();
    public List<string> MissingFields { get; set; } = new List<string>();
    public List<string> MissingReferences { get; set; } = new List<string>();
    public List<string> LegalWarnings { get; set; } = new List<string>();
    public List<string> ResourceWarnings { get; set; } = new List<string>();
    public List<string> VisibilityWarnings { get; set; } = new List<string>();
    public bool CanSubmit { get; set; }
    public bool RequiresGMReview { get; set; } = true;
    public DateTime CheckedAtUtc { get; set; } = DateTime.UtcNow;
    public string CheckedByUserId { get; set; } = string.Empty;
    public bool IsPlayerVisible { get; set; } = true;
    public string VisibilityMode { get; set; } = ProjectVisibilityModeIds.OwnerOnly;
    public Dictionary<string, object> ExtraData { get; set; } = new Dictionary<string, object>();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new Dictionary<string, object>();
}

public sealed class PlayerProposalReviewState : EntityBase
{
    public string ReviewId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string ProposalDraftId { get; set; } = string.Empty;
    public string LinkedPlayerRequestId { get; set; } = string.Empty;
    public string ReviewedByUserId { get; set; } = string.Empty;
    public string ReviewStatus { get; set; } = ProposalReviewStatusIds.Pending;
    public string GMComment { get; set; } = string.Empty;
    public string PlayerVisibleComment { get; set; } = string.Empty;
    public string RequestedChanges { get; set; } = string.Empty;
    public string DecisionReason { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAtUtc { get; set; }
    public List<string> Tags { get; set; } = new List<string>();
    public Dictionary<string, object> ExtraData { get; set; } = new Dictionary<string, object>();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new Dictionary<string, object>();
}

public sealed class PlayerProposalConversionState : EntityBase
{
    public string ConversionId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string ProposalDraftId { get; set; } = string.Empty;
    public string LinkedPlayerRequestId { get; set; } = string.Empty;
    public string ConversionType { get; set; } = ProposalConversionTypeIds.CreateGenericProject;
    public string TargetEntityType { get; set; } = string.Empty;
    public string TargetEntityId { get; set; } = string.Empty;
    public string SourceSummary { get; set; } = string.Empty;
    public string ConversionSummary { get; set; } = string.Empty;
    public string ConvertedByUserId { get; set; } = string.Empty;
    public DateTime ConvertedAtUtc { get; set; } = DateTime.UtcNow;
    public bool RequiresFollowUp { get; set; } = true;
    public string NextActionKind { get; set; } = string.Empty;
    public string PublicSummary { get; set; } = string.Empty;
    public string GMSummary { get; set; } = string.Empty;
    public bool IsPlayerVisible { get; set; } = true;
    public string VisibilityMode { get; set; } = ProjectVisibilityModeIds.OwnerOnly;
    public List<string> Tags { get; set; } = new List<string>();
    public Dictionary<string, object> ExtraData { get; set; } = new Dictionary<string, object>();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new Dictionary<string, object>();
}

public sealed class ProposalTemplateDefinition : EntityBase
{
    public string ProposalTemplateId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = string.Empty;
    public string ProposalType { get; set; } = ProposalTypeIds.Custom;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string PublicSummary { get; set; } = string.Empty;
    public string GMSummary { get; set; } = string.Empty;
    public List<string> RequiredFields { get; set; } = new List<string>();
    public List<string> OptionalFields { get; set; } = new List<string>();
    public List<string> SupportedSourceEntityTypes { get; set; } = new List<string>();
    public List<string> SupportedTargetEntityTypes { get; set; } = new List<string>();
    public string CreatesPlayerRequestType { get; set; } = PlayerRequestTypeIds.General;
    public List<string> SupportedConversionTargets { get; set; } = new List<string>();
    public bool RequiresGMApproval { get; set; } = true;
    public bool IsPlayerVisible { get; set; } = true;
    public string VisibilityMode { get; set; } = ProjectVisibilityModeIds.PlayerVisible;
    public bool IsArchived { get; set; }
    public List<string> Tags { get; set; } = new List<string>();
    public Dictionary<string, object> ExtraData { get; set; } = new Dictionary<string, object>();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new Dictionary<string, object>();
}

public sealed class ProposalEditorDefinition : EntityBase
{
    public string EditorId { get; set; } = string.Empty;
    public string ProposalType { get; set; } = ProposalTypeIds.Custom;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> FieldOrder { get; set; } = new List<string>();
    public bool IsPlayerVisible { get; set; } = true;
    public string VisibilityMode { get; set; } = ProjectVisibilityModeIds.PlayerVisible;
    public Dictionary<string, object> ExtraData { get; set; } = new Dictionary<string, object>();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new Dictionary<string, object>();
}
