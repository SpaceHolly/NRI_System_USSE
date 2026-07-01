using System;
using System.Collections.Generic;

namespace Nri.Shared.Domain;

public sealed class CraftingRecipeDefinition : EntityBase
{
    public string RecipeId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string PublicDescription { get; set; } = string.Empty;
    public string GMDescription { get; set; } = string.Empty;
    public string RecipeCategory { get; set; } = CraftingRecipeCategoryIds.Custom;
    public string RecipeType { get; set; } = CraftingRecipeTypeIds.Standard;
    public string OutputType { get; set; } = CraftingOutputTypeIds.InventoryItem;
    public string OutputDefinitionId { get; set; } = string.Empty;
    public string OutputName { get; set; } = string.Empty;
    public int OutputQuantity { get; set; } = 1;
    public string OutputQualityRange { get; set; } = string.Empty;
    public int BaseDifficulty { get; set; }
    public int BaseComplexity { get; set; }
    public int BaseRequiredProgress { get; set; } = 100;
    public long? BaseDurationWorldSeconds { get; set; }
    public string BaseCostSummary { get; set; } = string.Empty;
    public List<string> RequiredKnowledgeDefinitionIds { get; set; } = new List<string>();
    public List<string> RequiredAppliedKnowledgeIds { get; set; } = new List<string>();
    public bool IsKnownByDefault { get; set; }
    public bool IsPlayerDiscoverable { get; set; } = true;
    public bool IsPlayerVisible { get; set; }
    public string VisibilityMode { get; set; } = ProjectVisibilityModeIds.GmOnly;
    public bool RequiresGMApproval { get; set; } = true;
    public bool IsCustomAllowed { get; set; }
    public bool IsArchived { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
    public string UpdatedByUserId { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new List<string>();
    public Dictionary<string, object> ExtraData { get; set; } = new Dictionary<string, object>();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new Dictionary<string, object>();
}

public sealed class RecipeIngredientRequirement : EntityBase
{
    public string RecipeId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string IngredientType { get; set; } = CraftingIngredientTypeIds.Custom;
    public string IngredientDefinitionId { get; set; } = string.Empty;
    public string IngredientName { get; set; } = string.Empty;
    public decimal RequiredQuantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public bool IsConsumed { get; set; } = true;
    public bool IsSubstitutable { get; set; }
    public string SubstituteGroupId { get; set; } = string.Empty;
    public string QualityRequirement { get; set; } = string.Empty;
    public bool IsMandatory { get; set; } = true;
    public bool IsPlayerVisible { get; set; } = true;
    public string VisibilityMode { get; set; } = ProjectVisibilityModeIds.PlayerVisible;
    public string PublicSummary { get; set; } = string.Empty;
    public string GMSummary { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new List<string>();
    public Dictionary<string, object> ExtraData { get; set; } = new Dictionary<string, object>();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new Dictionary<string, object>();
}

public sealed class RecipeToolRequirement : EntityBase
{
    public string RecipeId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string ToolDefinitionId { get; set; } = string.Empty;
    public string ToolName { get; set; } = string.Empty;
    public bool IsConsumed { get; set; }
    public bool IsMandatory { get; set; } = true;
    public bool IsPlayerVisible { get; set; } = true;
    public string PublicSummary { get; set; } = string.Empty;
    public string GMSummary { get; set; } = string.Empty;
    public Dictionary<string, object> ExtraData { get; set; } = new Dictionary<string, object>();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new Dictionary<string, object>();
}

public sealed class RecipeFacilityRequirement : EntityBase
{
    public string RecipeId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string FacilityType { get; set; } = string.Empty;
    public string FacilityName { get; set; } = string.Empty;
    public bool IsMandatory { get; set; } = true;
    public bool IsPlayerVisible { get; set; } = true;
    public string PublicSummary { get; set; } = string.Empty;
    public string GMSummary { get; set; } = string.Empty;
    public Dictionary<string, object> ExtraData { get; set; } = new Dictionary<string, object>();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new Dictionary<string, object>();
}

public sealed class RecipeKnowledgeRequirement : EntityBase
{
    public string RecipeId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string KnowledgeDefinitionId { get; set; } = string.Empty;
    public string AppliedKnowledgeId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsMandatory { get; set; } = true;
    public bool IsPlayerVisible { get; set; } = true;
    public string PublicSummary { get; set; } = string.Empty;
    public string GMSummary { get; set; } = string.Empty;
    public Dictionary<string, object> ExtraData { get; set; } = new Dictionary<string, object>();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new Dictionary<string, object>();
}

public sealed class CraftingProjectState : EntityBase
{
    public string CampaignId { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string RecipeId { get; set; } = string.Empty;
    public string RecipeName { get; set; } = string.Empty;
    public string OwnerUserId { get; set; } = string.Empty;
    public string ActorEntityType { get; set; } = ProjectParticipantEntityTypeIds.PlayerCharacter;
    public string ActorEntityId { get; set; } = string.Empty;
    public string ActorDisplayName { get; set; } = string.Empty;
    public string TargetInventoryCharacterId { get; set; } = string.Empty;
    public string Status { get; set; } = CraftingProjectStatusIds.Draft;
    public int ProgressPercent { get; set; }
    public int WorkPointsDone { get; set; }
    public int WorkPointsRequired { get; set; }
    public string QualitySummary { get; set; } = string.Empty;
    public string ResultStatus { get; set; } = CraftingResultStatusIds.Draft;
    public bool IsPlayerVisible { get; set; } = true;
    public string VisibilityMode { get; set; } = ProjectVisibilityModeIds.PlayerVisible;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public string UpdatedByUserId { get; set; } = string.Empty;
    public string PublicNotes { get; set; } = string.Empty;
    public string GMNotes { get; set; } = string.Empty;
    public Dictionary<string, object> CustomProposalPayload { get; set; } = new Dictionary<string, object>();
    public Dictionary<string, object> ExtraData { get; set; } = new Dictionary<string, object>();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new Dictionary<string, object>();
}

public sealed class CraftingResourceReservationState : EntityBase
{
    public string CampaignId { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string CraftingProjectId { get; set; } = string.Empty;
    public string RequirementId { get; set; } = string.Empty;
    public string CharacterId { get; set; } = string.Empty;
    public string ItemInstanceId { get; set; } = string.Empty;
    public string DefinitionId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public decimal QuantityReserved { get; set; }
    public decimal QuantityConsumed { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string Status { get; set; } = CraftingReservationStatusIds.Reserved;
    public bool IsConsumedOnCompletion { get; set; } = true;
    public bool IsPlayerVisible { get; set; } = true;
    public DateTime ReservedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ReleasedAtUtc { get; set; }
    public DateTime? ConsumedAtUtc { get; set; }
    public string ReservedByUserId { get; set; } = string.Empty;
    public string UpdatedByUserId { get; set; } = string.Empty;
    public string PublicNotes { get; set; } = string.Empty;
    public string GMNotes { get; set; } = string.Empty;
    public Dictionary<string, object> ExtraData { get; set; } = new Dictionary<string, object>();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new Dictionary<string, object>();
}

public sealed class CraftingProjectItemResult : EntityBase
{
    public string CampaignId { get; set; } = string.Empty;
    public string CraftingProjectId { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string TargetCharacterId { get; set; } = string.Empty;
    public string ResultType { get; set; } = CraftingOutputTypeIds.InventoryItem;
    public string DefinitionId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public string QualitySummary { get; set; } = string.Empty;
    public string Status { get; set; } = CraftingResultStatusIds.Prepared;
    public string CreatedItemInstanceId { get; set; } = string.Empty;
    public bool IsPlayerVisible { get; set; } = true;
    public DateTime PreparedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? AcceptedAtUtc { get; set; }
    public DateTime? CreatedAtInventoryUtc { get; set; }
    public string PreparedByUserId { get; set; } = string.Empty;
    public string AcceptedByUserId { get; set; } = string.Empty;
    public string PublicNotes { get; set; } = string.Empty;
    public string GMNotes { get; set; } = string.Empty;
    public Dictionary<string, object> ExtraData { get; set; } = new Dictionary<string, object>();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new Dictionary<string, object>();
}
