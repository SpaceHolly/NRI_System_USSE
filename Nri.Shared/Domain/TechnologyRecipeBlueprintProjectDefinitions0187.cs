using System;
using System.Collections.Generic;

namespace Nri.Shared.Domain;

public static class TechnologyRecipeBlueprintProjectDefinitionCategories
{
    public const string Technology = "technology_definition";
    public const string ProductionMethod = "production_method_definition";
    public const string Recipe = "recipe_definition";
    public const string Blueprint = "canonical_blueprint_definition";
    public const string Facility = "facility_definition";
    public const string ProjectTemplate = "project_template_definition";
    public const string TestProtocol = "test_protocol_definition";
    public const string Defect = "defect_definition";

    public static readonly string[] All =
    {
        Technology,
        ProductionMethod,
        Recipe,
        Blueprint,
        Facility,
        ProjectTemplate,
        TestProtocol,
        Defect
    };

    public static bool IsSupported(string value)
        => Array.Exists(All, x => string.Equals(x, value, StringComparison.OrdinalIgnoreCase));
}

public abstract class TechnologyRecipeBlueprintProjectDefinitionProfile0187
{
    public string DefinitionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = RuleSetIds.FantasyNriDefault;
    public string PublicDescription { get; set; } = string.Empty;
    public string GMDescription { get; set; } = string.Empty;
    public string VisibilityRule { get; set; } = VisibilityRuleIds.Public;
    public List<string> Tags { get; set; } = new();
    public bool IsArchived { get; set; }
    public int Revision { get; set; } = 1;
}

public sealed class TechnologyDefinitionProfile : TechnologyRecipeBlueprintProjectDefinitionProfile0187
{
    public string TechnologyKind { get; set; } = "custom";
    public string FieldCategory { get; set; } = string.Empty;
    public int Tier { get; set; }
    public int Complexity { get; set; }
    public List<string> ParentTechnologyDefinitionIds { get; set; } = new();
    public List<string> PrerequisiteTechnologyDefinitionIds { get; set; } = new();
    public List<string> RelatedTechnologyDefinitionIds { get; set; } = new();
    public List<string> OpposedTechnologyDefinitionIds { get; set; } = new();
    public List<string> RequiredKnowledgeTypeDefinitionIds { get; set; } = new();
    public List<string> RequiredLoreDefinitionIds { get; set; } = new();
    public List<string> RequiredSkillDefinitionIds { get; set; } = new();
    public List<string> RequiredDevelopmentNodeDefinitionIds { get; set; } = new();
    public List<string> UnlockableMethodDefinitionIds { get; set; } = new();
    public List<string> UnlockableRecipeDefinitionIds { get; set; } = new();
    public List<string> UnlockableBlueprintDefinitionIds { get; set; } = new();
    public List<string> RequiredFacilityDefinitionIds { get; set; } = new();
    public List<string> RequiredToolDefinitionIds { get; set; } = new();
    public List<string> RequiredLicenseDefinitionIds { get; set; } = new();
    public List<string> KnownRisks { get; set; } = new();
}

public sealed class ProductionMethodDefinitionProfile : TechnologyRecipeBlueprintProjectDefinitionProfile0187
{
    public string MethodKind { get; set; } = "custom";
    public List<string> TechnologyDefinitionIds { get; set; } = new();
    public List<string> RecipeDefinitionIds { get; set; } = new();
    public List<string> BlueprintDefinitionIds { get; set; } = new();
    public List<string> RequiredSkillDefinitionIds { get; set; } = new();
    public List<string> RequiredFacilityDefinitionIds { get; set; } = new();
    public List<string> RequiredToolDefinitionIds { get; set; } = new();
    public List<string> PersonnelRoles { get; set; } = new();
    public int PreparationMinutes { get; set; }
    public string WorkDurationModel { get; set; } = string.Empty;
    public string QualityModel { get; set; } = string.Empty;
    public string ResourceLossModel { get; set; } = string.Empty;
    public List<string> RequiredLicenseDefinitionIds { get; set; } = new();
    public List<string> RiskTags { get; set; } = new();
}

public sealed class RecipeDefinitionProfile : TechnologyRecipeBlueprintProjectDefinitionProfile0187
{
    public string RecipeKind { get; set; } = "custom";
    public List<string> MethodDefinitionIds { get; set; } = new();
    public List<string> TechnologyDefinitionIds { get; set; } = new();
    public List<RecipeMaterialRow0187> Inputs { get; set; } = new();
    public List<RecipeMaterialRow0187> CatalystsAndTools { get; set; } = new();
    public List<RecipeMaterialRow0187> Outputs { get; set; } = new();
    public List<RecipeMaterialRow0187> Byproducts { get; set; } = new();
    public List<RecipeMaterialRow0187> Waste { get; set; } = new();
    public List<string> RequiredSkillDefinitionIds { get; set; } = new();
    public List<string> RequiredFacilityDefinitionIds { get; set; } = new();
    public List<string> PersonnelRoles { get; set; } = new();
    public int EstimatedDurationMinutes { get; set; }
    public string MoneyCostMetadata { get; set; } = string.Empty;
    public List<string> RequiredLicenseDefinitionIds { get; set; } = new();
    public string FailureWasteProfile { get; set; } = string.Empty;
}

public sealed class RecipeMaterialRow0187
{
    public string DefinitionId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string MinimumQuality { get; set; } = string.Empty;
    public string UsageMode { get; set; } = "consumed";
    public string SubstitutionGroup { get; set; } = string.Empty;
    public bool Optional { get; set; }
}

public sealed class BlueprintDefinitionProfile : TechnologyRecipeBlueprintProjectDefinitionProfile0187
{
    public string BlueprintKind { get; set; } = "custom";
    public string TargetDefinitionId { get; set; } = string.Empty;
    public List<string> TechnologyDefinitionIds { get; set; } = new();
    public List<string> MethodDefinitionIds { get; set; } = new();
    public List<string> RecipeDefinitionIds { get; set; } = new();
    public List<BlueprintComponentRow0187> Components { get; set; } = new();
    public List<string> RequiredFacilityDefinitionIds { get; set; } = new();
    public List<string> RequiredToolDefinitionIds { get; set; } = new();
    public List<string> PersonnelRoles { get; set; } = new();
    public List<string> RequiredLicenseDefinitionIds { get; set; } = new();
    public int EstimatedDurationMinutes { get; set; }
    public decimal EstimatedCost { get; set; }
    public string EstimatedResourceSummary { get; set; } = string.Empty;
    public string QualityTolerances { get; set; } = string.Empty;
    public List<string> TestProtocolDefinitionIds { get; set; } = new();
    public List<string> DefectDefinitionIds { get; set; } = new();
    public string ParentBlueprintDefinitionId { get; set; } = string.Empty;
    public string VersionLabel { get; set; } = string.Empty;
    public string SourceAssetBlueprintId { get; set; } = string.Empty;
}

public sealed class BlueprintComponentRow0187
{
    public string DefinitionId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public bool Required { get; set; } = true;
    public bool Resolved { get; set; } = true;
}

public sealed class FacilityDefinitionProfile : TechnologyRecipeBlueprintProjectDefinitionProfile0187
{
    public string FacilityKind { get; set; } = "custom";
    public List<string> Capabilities { get; set; } = new();
    public List<string> SupportedProjectKinds { get; set; } = new();
    public List<string> SupportedMethodDefinitionIds { get; set; } = new();
    public string Scale { get; set; } = string.Empty;
    public string CapacityBand { get; set; } = string.Empty;
    public List<string> RequiredLocationDefinitionIds { get; set; } = new();
    public List<string> PersonnelRoles { get; set; } = new();
    public List<string> RequiredToolDefinitionIds { get; set; } = new();
    public List<string> RequiredResourceDefinitionIds { get; set; } = new();
    public string EnergyRequirements { get; set; } = string.Empty;
    public string MaintenanceProfileMetadata { get; set; } = string.Empty;
    public string SecurityRequirements { get; set; } = string.Empty;
    public List<string> RequiredLicenseDefinitionIds { get; set; } = new();
}

public sealed class ProjectTemplateDefinitionProfile : TechnologyRecipeBlueprintProjectDefinitionProfile0187
{
    public string ProjectType { get; set; } = "Custom";
    public List<string> TechnologyDefinitionIds { get; set; } = new();
    public List<string> MethodDefinitionIds { get; set; } = new();
    public List<string> RecipeDefinitionIds { get; set; } = new();
    public List<string> BlueprintDefinitionIds { get; set; } = new();
    public List<ProjectStageDefinitionRow0187> Stages { get; set; } = new();
    public List<ProjectRequirementDefinitionRow0187> Requirements { get; set; } = new();
    public string ApprovalPolicy { get; set; } = string.Empty;
    public string DefaultProjectVisibility { get; set; } = string.Empty;
    public string ProgressModel { get; set; } = string.Empty;
    public string ResourceReservationPolicy { get; set; } = string.Empty;
    public string CancellationRefundPolicy { get; set; } = string.Empty;
    public List<string> TestProtocolDefinitionIds { get; set; } = new();
    public string DefectHandlingPolicy { get; set; } = string.Empty;
    public string CompletionResultKind { get; set; } = string.Empty;
}

public sealed class ProjectStageDefinitionRow0187
{
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int Order { get; set; }
    public List<string> AllowedPreviousStageKeys { get; set; } = new();
    public List<string> AllowedNextStageKeys { get; set; } = new();
    public string RequiredConditions { get; set; } = string.Empty;
    public bool RequiresGMDecision { get; set; }
    public bool IsPlayerVisible { get; set; } = true;
    public string PublicSummary { get; set; } = string.Empty;
    public string GMGuidance { get; set; } = string.Empty;
}

public sealed class ProjectRequirementDefinitionRow0187
{
    public string Kind { get; set; } = "CustomManual";
    public string DefinitionId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string MinimumQualityOrRank { get; set; } = string.Empty;
    public bool Required { get; set; } = true;
    public string ConsumptionMode { get; set; } = string.Empty;
    public string PublicExplanation { get; set; } = string.Empty;
    public string GMExplanation { get; set; } = string.Empty;
}

public sealed class TestProtocolDefinitionProfile : TechnologyRecipeBlueprintProjectDefinitionProfile0187
{
    public List<string> ApplicableBlueprintKinds { get; set; } = new();
    public List<string> ApplicableTechnologyKinds { get; set; } = new();
    public List<string> ApplicableMethodKinds { get; set; } = new();
    public string RequiredStageKey { get; set; } = string.Empty;
    public List<string> RequiredFacilityDefinitionIds { get; set; } = new();
    public List<string> RequiredToolDefinitionIds { get; set; } = new();
    public List<string> PersonnelRoles { get; set; } = new();
    public List<TestStepDefinitionRow0187> Steps { get; set; } = new();
    public List<TestMetricDefinitionRow0187> Metrics { get; set; } = new();
    public string PassCriteria { get; set; } = string.Empty;
    public string PartialPassCriteria { get; set; } = string.Empty;
    public string FailureCriteria { get; set; } = string.Empty;
    public string RepeatRules { get; set; } = string.Empty;
    public string ResourceTimeCost { get; set; } = string.Empty;
    public List<string> EffectDefinitionIds { get; set; } = new();
    public List<string> ConditionDefinitionIds { get; set; } = new();
    public string PublicResultTemplate { get; set; } = string.Empty;
    public string GMResultTemplate { get; set; } = string.Empty;
}

public sealed class TestStepDefinitionRow0187
{
    public int Order { get; set; }
    public string Name { get; set; } = string.Empty;
    public string PublicInstruction { get; set; } = string.Empty;
    public string GMInstruction { get; set; } = string.Empty;
}

public sealed class TestMetricDefinitionRow0187
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal? Minimum { get; set; }
    public decimal? Maximum { get; set; }
}

public sealed class DefectDefinitionProfile : TechnologyRecipeBlueprintProjectDefinitionProfile0187
{
    public string Category { get; set; } = "custom";
    public string Severity { get; set; } = "minor";
    public List<string> ApplicableTechnologyKinds { get; set; } = new();
    public List<string> ApplicableMethodKinds { get; set; } = new();
    public List<string> ApplicableBlueprintKinds { get; set; } = new();
    public string DetectionStageKey { get; set; } = string.Empty;
    public List<string> PossibleCauses { get; set; } = new();
    public List<string> PublicSymptoms { get; set; } = new();
    public string GMCauseDetails { get; set; } = string.Empty;
    public List<string> EffectDefinitionIds { get; set; } = new();
    public List<string> ConditionDefinitionIds { get; set; } = new();
    public List<ProjectRequirementDefinitionRow0187> RepairRetestRequirements { get; set; } = new();
    public string AddedResourceCostBand { get; set; } = string.Empty;
    public string AddedTimeCostBand { get; set; } = string.Empty;
    public List<string> LimitationTags { get; set; } = new();
}
