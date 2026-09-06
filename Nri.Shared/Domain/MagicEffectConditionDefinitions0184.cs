using System;
using System.Collections.Generic;

namespace Nri.Shared.Domain;

public static class MagicEffectConditionDefinitionFamilies
{
    public static readonly string[] All =
    {
        DefinitionCategoryIds.MagicMethod,
        DefinitionCategoryIds.MagicDirection,
        DefinitionCategoryIds.Spell,
        DefinitionCategoryIds.Seal,
        DefinitionCategoryIds.ArcanaForm,
        DefinitionCategoryIds.Ritual,
        DefinitionCategoryIds.Effect,
        DefinitionCategoryIds.Condition
    };

    public static bool IsSupported(string value)
    {
        foreach (var family in All)
        {
            if (string.Equals(family, value, StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }
}

public abstract class MagicEffectConditionDefinitionProfile
{
    public string DefinitionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = RuleSetIds.FantasyNriDefault;
    public List<string> Tags { get; set; } = new List<string>();
    public string PublicDescription { get; set; } = string.Empty;
    public string GMDescription { get; set; } = string.Empty;
    public string VisibilityRule { get; set; } = VisibilityRuleIds.Public;
    public bool IsArchived { get; set; }
}

public sealed class MagicMethodDefinitionProfile : MagicEffectConditionDefinitionProfile
{
    public string MethodCategory { get; set; } = string.Empty;
    public string ResourceModel { get; set; } = string.Empty;
    public string PreparationModel { get; set; } = string.Empty;
    public string CastingModel { get; set; } = string.Empty;
    public List<string> PrimarySkillIds { get; set; } = new List<string>();
    public List<string> AllowedAttributeIds { get; set; } = new List<string>();
    public List<string> AllowedSubAttributeIds { get; set; } = new List<string>();
    public List<string> CompatibleDirectionIds { get; set; } = new List<string>();
    public List<string> ResourceDefinitionIds { get; set; } = new List<string>();
    public List<string> DevelopmentNodeIds { get; set; } = new List<string>();
    public List<string> AllowedTargetScopes { get; set; } = new List<string>();
    public string DefaultRiskProfile { get; set; } = string.Empty;
    public string Legality { get; set; } = string.Empty;
}

public sealed class MagicDirectionDefinitionProfile : MagicEffectConditionDefinitionProfile
{
    public string DirectionKind { get; set; } = string.Empty;
    public List<string> ParentDirectionIds { get; set; } = new List<string>();
    public List<string> RelatedDirectionIds { get; set; } = new List<string>();
    public List<string> OpposedDirectionIds { get; set; } = new List<string>();
    public List<string> CompatibleMethodIds { get; set; } = new List<string>();
    public List<string> DamageTypeDefinitionIds { get; set; } = new List<string>();
    public List<string> EffectTags { get; set; } = new List<string>();
    public string Legality { get; set; } = string.Empty;
    public string Rarity { get; set; } = string.Empty;
}

public sealed class MagicResourceCostDefinition
{
    public string ResourceDefinitionId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Requirement { get; set; } = string.Empty;
}

public sealed class SpellDefinitionProfile : MagicEffectConditionDefinitionProfile
{
    public string SpellCategory { get; set; } = string.Empty;
    public int Tier { get; set; }
    public List<string> MagicMethodIds { get; set; } = new List<string>();
    public List<string> MagicDirectionIds { get; set; } = new List<string>();
    public List<string> RequiredSkillIds { get; set; } = new List<string>();
    public List<string> AllowedAttributeIds { get; set; } = new List<string>();
    public List<string> AllowedSubAttributeIds { get; set; } = new List<string>();
    public string CheckType { get; set; } = string.Empty;
    public string RollProfile { get; set; } = string.Empty;
    public string CastingTime { get; set; } = string.Empty;
    public int ActionCost { get; set; }
    public string PreparationRequirements { get; set; } = string.Empty;
    public string Range { get; set; } = string.Empty;
    public string TargetModel { get; set; } = string.Empty;
    public List<string> AllowedTargetScopes { get; set; } = new List<string>();
    public string Area { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
    public bool RequiresConcentration { get; set; }
    public bool RequiresChanneling { get; set; }
    public List<MagicResourceCostDefinition> ResourceCosts { get; set; } = new List<MagicResourceCostDefinition>();
    public List<string> MaterialItemIds { get; set; } = new List<string>();
    public List<string> MaterialResourceIds { get; set; } = new List<string>();
    public List<string> EffectDefinitionIds { get; set; } = new List<string>();
    public List<string> ConditionDefinitionIds { get; set; } = new List<string>();
    public List<string> DamageTypeDefinitionIds { get; set; } = new List<string>();
    public List<string> DevelopmentNodeIds { get; set; } = new List<string>();
    public string FailureMetadata { get; set; } = string.Empty;
    public string RiskMetadata { get; set; } = string.Empty;
    public bool IsInterruptible { get; set; }
    public string Legality { get; set; } = string.Empty;
    public string License { get; set; } = string.Empty;
}

public sealed class SealDefinitionProfile : MagicEffectConditionDefinitionProfile
{
    public List<string> MagicMethodIds { get; set; } = new List<string>();
    public List<string> MagicDirectionIds { get; set; } = new List<string>();
    public string PreparationTime { get; set; } = string.Empty;
    public string TriggerType { get; set; } = string.Empty;
    public string ActivationRequirements { get; set; } = string.Empty;
    public string TargetModel { get; set; } = string.Empty;
    public List<string> AllowedTargetScopes { get; set; } = new List<string>();
    public string Area { get; set; } = string.Empty;
    public string Persistence { get; set; } = string.Empty;
    public int Charges { get; set; }
    public List<MagicResourceCostDefinition> ResourceCosts { get; set; } = new List<MagicResourceCostDefinition>();
    public List<string> MaterialItemIds { get; set; } = new List<string>();
    public List<string> MaterialResourceIds { get; set; } = new List<string>();
    public List<string> EffectDefinitionIds { get; set; } = new List<string>();
    public List<string> ConditionDefinitionIds { get; set; } = new List<string>();
    public string InterruptionRules { get; set; } = string.Empty;
    public string DestructionRules { get; set; } = string.Empty;
    public string Legality { get; set; } = string.Empty;
}

public sealed class ArcanaFormDefinitionProfile : MagicEffectConditionDefinitionProfile
{
    public string FormCategory { get; set; } = string.Empty;
    public List<string> CompatibleDirectionIds { get; set; } = new List<string>();
    public decimal ArcanaCost { get; set; }
    public string ChannelTime { get; set; } = string.Empty;
    public string Overload { get; set; } = string.Empty;
    public string Stability { get; set; } = string.Empty;
    public string Risk { get; set; } = string.Empty;
    public string TargetModel { get; set; } = string.Empty;
    public List<string> AllowedTargetScopes { get; set; } = new List<string>();
    public string Area { get; set; } = string.Empty;
    public List<string> EffectDefinitionIds { get; set; } = new List<string>();
    public List<string> ConditionDefinitionIds { get; set; } = new List<string>();
    public string Requirements { get; set; } = string.Empty;
    public string Legality { get; set; } = string.Empty;
}

public sealed class RitualStageDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
    public string Requirements { get; set; } = string.Empty;
}

public sealed class RitualDefinitionProfile : MagicEffectConditionDefinitionProfile
{
    public string RitualCategory { get; set; } = string.Empty;
    public List<string> MagicMethodIds { get; set; } = new List<string>();
    public List<string> MagicDirectionIds { get; set; } = new List<string>();
    public int RequiredParticipants { get; set; } = 1;
    public List<string> ParticipantRoles { get; set; } = new List<string>();
    public string PreparationTime { get; set; } = string.Empty;
    public string ExecutionDuration { get; set; } = string.Empty;
    public string LocationRequirements { get; set; } = string.Empty;
    public List<string> AllowedTargetScopes { get; set; } = new List<string>();
    public List<string> MaterialItemIds { get; set; } = new List<string>();
    public List<string> MaterialResourceIds { get; set; } = new List<string>();
    public List<RitualStageDefinition> Stages { get; set; } = new List<RitualStageDefinition>();
    public string InterruptionRules { get; set; } = string.Empty;
    public string FailureConsequences { get; set; } = string.Empty;
    public List<string> EffectDefinitionIds { get; set; } = new List<string>();
    public List<string> ConditionDefinitionIds { get; set; } = new List<string>();
    public string ResultDuration { get; set; } = string.Empty;
    public string Legality { get; set; } = string.Empty;
}

public sealed class EffectDefinitionProfile0184 : MagicEffectConditionDefinitionProfile
{
    public string EffectKind { get; set; } = string.Empty;
    public string TargetSelector { get; set; } = string.Empty;
    public string Timing { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public string ValueExpression { get; set; } = string.Empty;
    public string DamageTypeDefinitionId { get; set; } = string.Empty;
    public string ResourceDefinitionId { get; set; } = string.Empty;
    public string DerivedStatDefinitionId { get; set; } = string.Empty;
    public string AttributeDefinitionId { get; set; } = string.Empty;
    public string SubAttributeDefinitionId { get; set; } = string.Empty;
    public string SkillDefinitionId { get; set; } = string.Empty;
    public string ConditionDefinitionId { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
    public string Interval { get; set; } = string.Empty;
    public string StackingBehavior { get; set; } = string.Empty;
    public string SourceRestrictions { get; set; } = string.Empty;
    public string ManualResolution { get; set; } = string.Empty;
}

public sealed class ConditionDefinitionProfile0184 : MagicEffectConditionDefinitionProfile
{
    public string ConditionCategory { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string DurationModel { get; set; } = string.Empty;
    public string DefaultDuration { get; set; } = string.Empty;
    public string StackingModel { get; set; } = string.Empty;
    public int MaximumStacks { get; set; } = 1;
    public string RefreshReplaceRules { get; set; } = string.Empty;
    public bool IsHiddenState { get; set; }
    public string DispelRemovalRules { get; set; } = string.Empty;
    public List<string> ImmunityTags { get; set; } = new List<string>();
    public List<string> ResistanceTags { get; set; } = new List<string>();
    public List<string> EffectsOnApplyIds { get; set; } = new List<string>();
    public List<string> PeriodicEffectIds { get; set; } = new List<string>();
    public List<string> EffectsOnRemoveIds { get; set; } = new List<string>();
    public string IconKey { get; set; } = string.Empty;
}

public sealed class MagicEffectConditionReferenceView
{
    public string DefinitionId { get; set; } = string.Empty;
    public string Family { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public bool IsPlayerVisible { get; set; }
    public bool IsArchived { get; set; }
}
