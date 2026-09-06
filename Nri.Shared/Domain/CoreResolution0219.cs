using System;
using System.Collections.Generic;
using System.Linq;

namespace Nri.Shared.Domain;

public static class CoreResolutionRollModes
{
    public const string Normal = "normal";
    public const string Advantage = "advantage";
    public const string Hindrance = "hindrance";
}

public static class CoreResolutionDegreeIds
{
    public const string Failure = "failure";
    public const string Ordinary = "ordinary";
    public const string Strong = "strong";
    public const string Exceptional = "exceptional";
}

public static class CoreResolutionModifierCategories
{
    public const string Ability = "ability";
    public const string Proficiency = "proficiency";
    public const string Equipment = "equipment";
    public const string Enhancement = "enhancement";
    public const string Circumstance = "circumstance";
    public const string Assistance = "assistance";
    public const string Development = "development";
    public const string Condition = "condition";
}

public sealed class CheckResolutionRecord : EntityBase
{
    public string RequestId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string ActorId { get; set; } = string.Empty;
    public string CharacterId { get; set; } = string.Empty;
    public string ActionDefinitionId { get; set; } = string.Empty;
    public string AbilitySourceId { get; set; } = string.Empty;
    public string PrimaryProficiencyId { get; set; } = string.Empty;
    public int SkillRank { get; set; }
    public string MasteryBand { get; set; } = string.Empty;
    public int NaturalRoll { get; set; }
    public int? SecondNaturalRoll { get; set; }
    public string RollMode { get; set; } = CoreResolutionRollModes.Normal;
    public int AbilityModifier { get; set; }
    public int ProficiencyModifier { get; set; }
    public int TemporaryModifier { get; set; }
    public int Difficulty { get; set; }
    public int Total { get; set; }
    public int Margin { get; set; }
    public string Degree { get; set; } = CoreResolutionDegreeIds.Failure;
    public bool AttemptGatePassed { get; set; }
    public bool IsSuccess { get; set; }
    public DateTime ResolvedAtUtc { get; set; } = DateTime.UtcNow;
    public int Revision { get; set; } = 1;
}

public sealed class CoreResolutionModifier
{
    public string Category { get; set; } = string.Empty;
    public int Value { get; set; }
    public string PublicLabel { get; set; } = string.Empty;
    public bool IsPlayerVisible { get; set; } = true;
}

public abstract class RuleSetResolutionDefinition0219 : DefinitionDocumentBase
{
    public string RuleSetId { get; set; } = string.Empty;
    public int Revision { get; set; } = 1;
    public string PublicDescription { get; set; } = string.Empty;
    public string GMDescription { get; set; } = string.Empty;
}

public sealed class ResolutionProfileDefinition : RuleSetResolutionDefinition0219
{
    public string PrimaryDie { get; set; } = "1d20";
    public string NaturalCriticalFailurePolicy { get; set; } = "natural_1_fails";
    public string NaturalCriticalSuccessPolicy { get; set; } = "natural_20_requires_attempt_gate";
    public string AbilityContributionPolicy { get; set; } = "attribute_or_subattribute";
    public string AbilityModifierProfileId { get; set; } = string.Empty;
    public string SkillMasteryProfileId { get; set; } = string.Empty;
    public string ModifierCategoryProfileId { get; set; } = string.Empty;
    public string AdvantagePolicyId { get; set; } = string.Empty;
    public string DifficultyProfileId { get; set; } = string.Empty;
    public string DegreeOfSuccessProfileId { get; set; } = string.Empty;
    public string AttemptGateProfileId { get; set; } = string.Empty;
    public string FateRoutingProfileId { get; set; } = string.Empty;
}

public sealed class AbilityModifierProfileDefinition : RuleSetResolutionDefinition0219
{
    public string MappingMode { get; set; } = "score_to_modifier";
    public int MinimumModifier { get; set; } = -4;
    public int MaximumModifier { get; set; } = 4;
    public Dictionary<int, int> LookupTable { get; set; } = new Dictionary<int, int>();
}

public sealed class SkillMasteryBandDefinition0219
{
    public int MinimumRank { get; set; }
    public int MaximumRank { get; set; }
    public int ProficiencyModifier { get; set; }
    public string PublicLabel { get; set; } = string.Empty;
}

public sealed class SkillMasteryProfileDefinition : RuleSetResolutionDefinition0219
{
    public int MinimumRank { get; set; }
    public int MaximumRank { get; set; } = 20;
    public List<SkillMasteryBandDefinition0219> Bands { get; set; } = new List<SkillMasteryBandDefinition0219>();
}

public sealed class ModifierCategoryRuleDefinition0219
{
    public string Category { get; set; } = string.Empty;
    public string StackingPolicy { get; set; } = "strongest_positive_and_negative";
    public int MaximumPositive { get; set; }
    public int MinimumNegative { get; set; }
}

public sealed class ModifierCategoryProfileDefinition : RuleSetResolutionDefinition0219
{
    public int MaximumPositiveTemporaryTotal { get; set; } = 4;
    public List<ModifierCategoryRuleDefinition0219> Categories { get; set; } = new List<ModifierCategoryRuleDefinition0219>();
}

public sealed class AdvantagePolicyDefinition : RuleSetResolutionDefinition0219
{
    public string AdvantageMode { get; set; } = "highest_of_2d20";
    public string HindranceMode { get; set; } = "lowest_of_2d20";
    public bool OpposedStatesCancel { get; set; } = true;
}

public sealed class DifficultyBandDefinition0219
{
    public string Id { get; set; } = string.Empty;
    public string PublicLabel { get; set; } = string.Empty;
    public int Difficulty { get; set; }
}

public sealed class DifficultyProfileDefinition : RuleSetResolutionDefinition0219
{
    public List<DifficultyBandDefinition0219> Bands { get; set; } = new List<DifficultyBandDefinition0219>();
}

public sealed class DegreeOfSuccessBandDefinition0219
{
    public string Id { get; set; } = string.Empty;
    public string PublicLabel { get; set; } = string.Empty;
    public int MinimumMargin { get; set; }
}

public sealed class DegreeOfSuccessProfileDefinition : RuleSetResolutionDefinition0219
{
    public List<DegreeOfSuccessBandDefinition0219> SuccessBands { get; set; } = new List<DegreeOfSuccessBandDefinition0219>();
}

public sealed class AttemptGateProfileDefinition : RuleSetResolutionDefinition0219
{
    public bool RejectMissingKnowledge { get; set; } = true;
    public bool RejectMissingTool { get; set; } = true;
    public bool RejectBodyIncompatibility { get; set; } = true;
    public bool NaturalTwentyBypassesGate { get; set; }
}

public sealed class HitResolutionProfileDefinition : RuleSetResolutionDefinition0219
{
    public int PassiveDefenseBase { get; set; } = 10;
    public bool NaturalTwentyGuaranteesHit { get; set; } = true;
    public bool NaturalTwentyGuaranteesPenetration { get; set; }
    public bool ArmorAddsToHitDefense { get; set; }
}

public sealed class PenetrationDamageProfileDefinition : RuleSetResolutionDefinition0219
{
    public bool HitAndPenetrationAreSeparate { get; set; } = true;
    public bool MitigationAppliesAfterPenetration { get; set; } = true;
    public List<string> PenetrationTypes { get; set; } = new List<string>();
}

public static class FantasyNriDefaultResolutionProfiles0219
{
    public const string RuleSetId = "fantasy_nri_default";

    public static ResolutionProfileDefinition Resolution() => new ResolutionProfileDefinition
    {
        Id = "resolution_fantasy_default_0219",
        Code = "resolution_fantasy_default_0219",
        Name = "Базовая проверка d20",
        RuleSetId = RuleSetId,
        AbilityModifierProfileId = "ability_modifier_fantasy_default_0219",
        SkillMasteryProfileId = "skill_mastery_fantasy_default_0219",
        ModifierCategoryProfileId = "modifier_categories_fantasy_default_0219",
        AdvantagePolicyId = "advantage_fantasy_default_0219",
        DifficultyProfileId = "difficulty_fantasy_default_0219",
        DegreeOfSuccessProfileId = "degree_fantasy_default_0219",
        AttemptGateProfileId = "attempt_gate_fantasy_default_0219",
        FateRoutingProfileId = "fate_separate_dec_003",
        Status = DefinitionStatus.Active,
        IsActive = true
    };

    public static AbilityModifierProfileDefinition Ability() => new AbilityModifierProfileDefinition
    {
        Id = "ability_modifier_fantasy_default_0219", Code = "ability_modifier_fantasy_default_0219", Name = "Ограниченная характеристика −4…+4",
        RuleSetId = RuleSetId, MappingMode = "score_to_modifier", MinimumModifier = -4, MaximumModifier = 4, Status = DefinitionStatus.Active, IsActive = true
    };

    public static SkillMasteryProfileDefinition Mastery() => new SkillMasteryProfileDefinition
    {
        Id = "skill_mastery_fantasy_default_0219", Code = "skill_mastery_fantasy_default_0219", Name = "Мастерство навыка 0–20", RuleSetId = RuleSetId,
        Status = DefinitionStatus.Active, IsActive = true,
        Bands = new List<SkillMasteryBandDefinition0219>
        {
            new SkillMasteryBandDefinition0219 { MinimumRank = 0, MaximumRank = 0, ProficiencyModifier = 0, PublicLabel = "Не обучен" },
            new SkillMasteryBandDefinition0219 { MinimumRank = 1, MaximumRank = 4, ProficiencyModifier = 1, PublicLabel = "Новичок" },
            new SkillMasteryBandDefinition0219 { MinimumRank = 5, MaximumRank = 8, ProficiencyModifier = 2, PublicLabel = "Обученный" },
            new SkillMasteryBandDefinition0219 { MinimumRank = 9, MaximumRank = 12, ProficiencyModifier = 3, PublicLabel = "Профессионал" },
            new SkillMasteryBandDefinition0219 { MinimumRank = 13, MaximumRank = 16, ProficiencyModifier = 4, PublicLabel = "Эксперт" },
            new SkillMasteryBandDefinition0219 { MinimumRank = 17, MaximumRank = 20, ProficiencyModifier = 5, PublicLabel = "Мастер" }
        }
    };
}

public sealed class CoreResolutionAttempt
{
    public int NaturalRoll { get; set; }
    public int? SecondNaturalRoll { get; set; }
    public string RollMode { get; set; } = CoreResolutionRollModes.Normal;
    public int Difficulty { get; set; } = 12;
    public bool AttemptGatePassed { get; set; } = true;
    public string AttemptGateReason { get; set; } = string.Empty;
    public int AbilityModifier { get; set; }
    public int SkillRank { get; set; }
    public List<CoreResolutionModifier> Modifiers { get; set; } = new List<CoreResolutionModifier>();
}

public sealed class CoreResolutionResult
{
    public int SelectedNaturalRoll { get; set; }
    public int AbilityModifier { get; set; }
    public int SkillRank { get; set; }
    public int ProficiencyBonus { get; set; }
    public int TemporaryModifier { get; set; }
    public int Total { get; set; }
    public int Difficulty { get; set; }
    public int Margin { get; set; }
    public bool AttemptGatePassed { get; set; }
    public bool IsSuccess { get; set; }
    public bool IsNaturalOne { get; set; }
    public bool IsNaturalTwenty { get; set; }
    public string Degree { get; set; } = CoreResolutionDegreeIds.Failure;
    public List<string> Warnings { get; set; } = new List<string>();
}

public static class CoreResolutionPolicy0219
{
    public const int MinimumAbilityModifier = -4;
    public const int MaximumAbilityModifier = 4;

    public static int MasteryBonus(int rank)
    {
        var bounded = Math.Max(0, Math.Min(20, rank));
        if (bounded == 0) return 0;
        if (bounded <= 4) return 1;
        if (bounded <= 8) return 2;
        if (bounded <= 12) return 3;
        if (bounded <= 16) return 4;
        return 5;
    }

    public static string MasteryBand(int rank)
    {
        var bounded = Math.Max(0, Math.Min(20, rank));
        if (bounded == 0) return "Не обучен";
        if (bounded <= 4) return "Новичок";
        if (bounded <= 8) return "Обученный";
        if (bounded <= 12) return "Профессионал";
        if (bounded <= 16) return "Эксперт";
        return "Мастер";
    }

    public static CoreResolutionResult Resolve(CoreResolutionAttempt attempt)
    {
        if (attempt == null) throw new ArgumentNullException(nameof(attempt));
        ValidateNaturalRoll(attempt.NaturalRoll);
        if (attempt.SecondNaturalRoll.HasValue) ValidateNaturalRoll(attempt.SecondNaturalRoll.Value);
        var selected = SelectRoll(attempt.NaturalRoll, attempt.SecondNaturalRoll, attempt.RollMode);
        var ability = Math.Max(MinimumAbilityModifier, Math.Min(MaximumAbilityModifier, attempt.AbilityModifier));
        var temporary = CalculateTemporaryModifier(attempt.Modifiers);
        var proficiency = MasteryBonus(attempt.SkillRank);
        var total = selected + ability + proficiency + temporary;
        var margin = total - attempt.Difficulty;
        var naturalOne = selected == 1;
        var naturalTwenty = selected == 20;
        var success = attempt.AttemptGatePassed && !naturalOne && (naturalTwenty || margin >= 0);
        var degree = success ? DegreeForMargin(margin) : CoreResolutionDegreeIds.Failure;
        if (success && naturalTwenty) degree = UpgradeDegree(degree);
        return new CoreResolutionResult
        {
            SelectedNaturalRoll = selected,
            AbilityModifier = ability,
            SkillRank = Math.Max(0, Math.Min(20, attempt.SkillRank)),
            ProficiencyBonus = proficiency,
            TemporaryModifier = temporary,
            Total = total,
            Difficulty = attempt.Difficulty,
            Margin = margin,
            AttemptGatePassed = attempt.AttemptGatePassed,
            IsSuccess = success,
            IsNaturalOne = naturalOne,
            IsNaturalTwenty = naturalTwenty,
            Degree = degree,
            Warnings = ability != attempt.AbilityModifier ? new List<string> { "ability_modifier_bounded" } : new List<string>()
        };
    }

    public static int CalculateTemporaryModifier(IEnumerable<CoreResolutionModifier>? modifiers)
    {
        var sum = 0;
        foreach (var group in (modifiers ?? Enumerable.Empty<CoreResolutionModifier>()).GroupBy(x => Normalize(x.Category), StringComparer.OrdinalIgnoreCase))
        {
            var strongestPositive = group.Where(x => x.Value > 0).Select(x => x.Value).DefaultIfEmpty(0).Max();
            var strongestNegative = group.Where(x => x.Value < 0).Select(x => x.Value).DefaultIfEmpty(0).Min();
            var caps = CategoryCaps(group.Key);
            sum += Math.Min(strongestPositive, caps.positive);
            sum += Math.Max(strongestNegative, caps.negative);
        }
        return sum;
    }

    public static string SelectPrimaryProficiency(IEnumerable<CoreResolutionProficiencyCandidate>? candidates)
    {
        return (candidates ?? Enumerable.Empty<CoreResolutionProficiencyCandidate>())
            .Where(x => x.IsEligible)
            .OrderByDescending(x => MasteryBonus(x.Rank))
            .ThenByDescending(x => x.Rank)
            .ThenBy(x => x.SkillId, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.SkillId)
            .FirstOrDefault() ?? string.Empty;
    }

    public static string ClassifyDegree(int margin) => margin < 0 ? CoreResolutionDegreeIds.Failure : DegreeForMargin(margin);

    private static int SelectRoll(int first, int? second, string mode)
    {
        if (!second.HasValue || string.Equals(mode, CoreResolutionRollModes.Normal, StringComparison.OrdinalIgnoreCase)) return first;
        if (string.Equals(mode, CoreResolutionRollModes.Advantage, StringComparison.OrdinalIgnoreCase)) return Math.Max(first, second.Value);
        if (string.Equals(mode, CoreResolutionRollModes.Hindrance, StringComparison.OrdinalIgnoreCase)) return Math.Min(first, second.Value);
        throw new ArgumentException("Unknown roll mode.", nameof(mode));
    }

    private static (int positive, int negative) CategoryCaps(string category)
    {
        if (category == CoreResolutionModifierCategories.Equipment) return (1, -2);
        if (category == CoreResolutionModifierCategories.Enhancement) return (2, -2);
        if (category == CoreResolutionModifierCategories.Circumstance) return (1, -2);
        if (category == CoreResolutionModifierCategories.Condition) return (0, -3);
        return (0, 0);
    }

    private static string DegreeForMargin(int margin) => margin >= 8 ? CoreResolutionDegreeIds.Exceptional : margin >= 4 ? CoreResolutionDegreeIds.Strong : CoreResolutionDegreeIds.Ordinary;
    private static string UpgradeDegree(string degree) => degree == CoreResolutionDegreeIds.Ordinary ? CoreResolutionDegreeIds.Strong : degree == CoreResolutionDegreeIds.Strong ? CoreResolutionDegreeIds.Exceptional : degree;
    private static string Normalize(string value) => (value ?? string.Empty).Trim().ToLowerInvariant();
    private static void ValidateNaturalRoll(int value) { if (value < 1 || value > 20) throw new ArgumentOutOfRangeException(nameof(value), "Natural d20 result must be between 1 and 20."); }
}

public sealed class CoreResolutionProficiencyCandidate
{
    public string SkillId { get; set; } = string.Empty;
    public int Rank { get; set; }
    public bool IsEligible { get; set; }
}

public sealed class CombatActionCost0219
{
    public int HalfActions { get; set; }
    public int Reactions { get; set; }
    public bool ReservesPreparedAction { get; set; }
}

public static class CombatActionEconomyPolicy0219
{
    public const int HalfActionsPerTurn = 2;
    public const int ReactionsPerRound = 1;
    public const int RoundDurationSeconds = 5;

    public static CombatActionCost0219 CostFor(string actionType)
    {
        if (string.Equals(actionType, CombatActionTypes.Move, StringComparison.OrdinalIgnoreCase)) return new CombatActionCost0219 { HalfActions = 1 };
        if (string.Equals(actionType, CombatActionTypes.Interact, StringComparison.OrdinalIgnoreCase)) return new CombatActionCost0219 { HalfActions = 1 };
        if (string.Equals(actionType, CombatActionTypes.Prepare, StringComparison.OrdinalIgnoreCase)) return new CombatActionCost0219 { HalfActions = 2, ReservesPreparedAction = true };
        if (string.Equals(actionType, CombatActionTypes.Reaction, StringComparison.OrdinalIgnoreCase)) return new CombatActionCost0219 { Reactions = 1 };
        if (string.Equals(actionType, CombatActionTypes.Wait, StringComparison.OrdinalIgnoreCase)
            || string.Equals(actionType, CombatActionTypes.Skip, StringComparison.OrdinalIgnoreCase)
            || string.Equals(actionType, CombatActionTypes.GmNote, StringComparison.OrdinalIgnoreCase)) return new CombatActionCost0219();
        return new CombatActionCost0219 { HalfActions = 1 };
    }
}

public static class CombatPenetrationTypes0219
{
    public const string Physical = "physical";
    public const string Armor = "armor";
    public const string Magic = "magic";
    public const string Morale = "morale";
}

public sealed class CombatPenetrationContext0219
{
    public string PenetrationType { get; set; } = CombatPenetrationTypes0219.Armor;
    public int AttackProfilePenetration { get; set; }
    public int AmmoPenetration { get; set; }
    public int TechniquePenetration { get; set; }
    public int ConditionModifier { get; set; }
    public int TargetProtection { get; set; }
}

public sealed class CombatPenetrationResult0219
{
    public string PenetrationType { get; set; } = string.Empty;
    public int TotalPenetration { get; set; }
    public int TargetProtection { get; set; }
    public int EffectiveProtection { get; set; }
    public bool IsPenetrated { get; set; }
}

public static class CombatPenetrationPolicy0219
{
    public static CombatPenetrationResult0219 Resolve(CombatPenetrationContext0219 context)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));
        var total = Math.Max(0, context.AttackProfilePenetration + context.AmmoPenetration + context.TechniquePenetration + context.ConditionModifier);
        var protection = Math.Max(0, context.TargetProtection);
        var penetrated = total >= protection;
        return new CombatPenetrationResult0219
        {
            PenetrationType = context.PenetrationType,
            TotalPenetration = total,
            TargetProtection = protection,
            EffectiveProtection = penetrated ? 0 : Math.Max(0, protection - total),
            IsPenetrated = penetrated
        };
    }
}

public static class RequirementExpressionKinds
{
    public const string Leaf = "leaf";
    public const string AllOf = "all_of";
    public const string AnyOf = "any_of";
    public const string AtLeast = "at_least";
}

public static class RequirementLeafTypes
{
    public const string DevelopmentNode = "development_node";
    public const string DevelopmentNodeRank = "development_node_rank";
    public const string DevelopmentPath = "development_path";
    public const string SkillRank = "skill_rank";
    public const string MasteryBand = "mastery_band";
    public const string SkillAcquired = "skill_acquired";
    public const string Attribute = "attribute";
    public const string SubAttribute = "subattribute";
    public const string Technique = "technique";
    public const string Action = "action";
    public const string Knowledge = "knowledge";
    public const string Race = "race";
    public const string Subspecies = "subspecies";
    public const string Trait = "trait";
    public const string BodyCompatibility = "body_compatibility";
    public const string EquipmentTag = "equipment_tag";
    public const string Tool = "tool";
    public const string WeaponTag = "weapon_tag";
    public const string ArmorProficiency = "armor_proficiency";
    public const string WorldState = "world_state";
    public const string Custom = "custom";
}

public sealed class RequirementExpression
{
    public string Kind { get; set; } = RequirementExpressionKinds.Leaf;
    public string LeafType { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
    public int MinimumValue { get; set; }
    public int RequiredCount { get; set; }
    public string PublicLabel { get; set; } = string.Empty;
    public string GMLabel { get; set; } = string.Empty;
    public bool IsHidden { get; set; }
    public List<RequirementExpression> Children { get; set; } = new List<RequirementExpression>();
}

public sealed class RequirementFactSnapshot
{
    public HashSet<string> DevelopmentNodeIds { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> DevelopmentNodeRanks { get; set; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> DevelopmentPathIds { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> SkillRanks { get; set; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> Attributes { get; set; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> SubAttributes { get; set; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> TechniqueIds { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> ActionIds { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> KnowledgeIds { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> RaceIds { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> SubspeciesIds { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> TraitIds { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> BodyCompatibilityIds { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> EquipmentTags { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> ToolIds { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> WeaponTags { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> ArmorProficiencyIds { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> WorldStateValues { get; set; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> CustomValues { get; set; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
}

public sealed class RequirementEvaluationResult
{
    public bool IsSatisfied { get; set; }
    public string Kind { get; set; } = string.Empty;
    public int RequiredCount { get; set; }
    public int SatisfiedCount { get; set; }
    public string PublicReason { get; set; } = string.Empty;
    public string GMReason { get; set; } = string.Empty;
    public string SafeTargetReference { get; set; } = string.Empty;
    public List<RequirementEvaluationResult> Children { get; set; } = new List<RequirementEvaluationResult>();
}

public sealed class RequirementReferenceCatalog
{
    public HashSet<string> ActiveReferences { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> ArchivedReferences { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public static string Key(string leafType, string targetId) => $"{leafType}:{targetId}";
}

public static class RequirementExpressionEvaluator0219
{
    public static RequirementEvaluationResult Evaluate(RequirementExpression expression, RequirementFactSnapshot facts, bool playerSafe)
    {
        if (expression == null) throw new ArgumentNullException(nameof(expression));
        if (facts == null) throw new ArgumentNullException(nameof(facts));
        Validate(expression);
        if (expression.Kind == RequirementExpressionKinds.Leaf) return EvaluateLeaf(expression, facts, playerSafe);
        var children = expression.Children.Select(child => Evaluate(child, facts, playerSafe)).ToList();
        var satisfied = children.Count(x => x.IsSatisfied);
        var required = expression.Kind == RequirementExpressionKinds.AllOf ? children.Count
            : expression.Kind == RequirementExpressionKinds.AnyOf ? 1
            : expression.RequiredCount;
        return new RequirementEvaluationResult
        {
            IsSatisfied = satisfied >= required,
            Kind = expression.Kind,
            RequiredCount = required,
            SatisfiedCount = satisfied,
            PublicReason = GroupReason(expression.Kind, required, satisfied, playerSafe),
            GMReason = GroupReason(expression.Kind, required, satisfied, false),
            Children = children
        };
    }

    public static List<string> Validate(RequirementExpression expression)
    {
        var errors = new List<string>();
        ValidateRecursive(expression, errors, new HashSet<RequirementExpression>());
        if (errors.Count > 0) throw new ArgumentException(string.Join("; ", errors), nameof(expression));
        return errors;
    }

    public static RequirementExpression MigrateLegacy(IEnumerable<UnlockRequirement>? requirements, string? knownGroupKind = null)
    {
        var leaves = (requirements ?? Enumerable.Empty<UnlockRequirement>()).Select(r => new RequirementExpression
        {
            Kind = RequirementExpressionKinds.Leaf,
            LeafType = LegacyLeafType(r.RequirementType),
            TargetId = r.Key ?? string.Empty,
            MinimumValue = ParseMinimum(r.Value),
            PublicLabel = r.Key ?? string.Empty,
            GMLabel = r.Key ?? string.Empty
        }).ToList();
        if (leaves.Count == 0) return new RequirementExpression { Kind = RequirementExpressionKinds.AllOf };
        if (leaves.Count == 1) return leaves[0];
        if (knownGroupKind != RequirementExpressionKinds.AllOf && knownGroupKind != RequirementExpressionKinds.AnyOf)
            throw new ArgumentException("ambiguous_legacy_requirement_semantics", nameof(knownGroupKind));
        return new RequirementExpression { Kind = knownGroupKind, Children = leaves };
    }

    public static List<string> ValidateReferences(RequirementExpression expression, RequirementReferenceCatalog catalog)
    {
        if (expression == null) throw new ArgumentNullException(nameof(expression));
        if (catalog == null) throw new ArgumentNullException(nameof(catalog));
        Validate(expression);
        var errors = new List<string>();
        ValidateReferencesRecursive(expression, catalog, errors);
        return errors;
    }

    private static RequirementEvaluationResult EvaluateLeaf(RequirementExpression expression, RequirementFactSnapshot facts, bool playerSafe)
    {
        var actual = 0;
        var satisfied = false;
        switch (expression.LeafType)
        {
            case RequirementLeafTypes.DevelopmentNode: satisfied = facts.DevelopmentNodeIds.Contains(expression.TargetId); actual = satisfied ? 1 : 0; break;
            case RequirementLeafTypes.DevelopmentNodeRank: actual = facts.DevelopmentNodeRanks.TryGetValue(expression.TargetId, out var nodeRank) ? nodeRank : 0; satisfied = actual >= expression.MinimumValue; break;
            case RequirementLeafTypes.DevelopmentPath: satisfied = facts.DevelopmentPathIds.Contains(expression.TargetId); actual = satisfied ? 1 : 0; break;
            case RequirementLeafTypes.SkillRank: facts.SkillRanks.TryGetValue(expression.TargetId, out actual); satisfied = actual >= expression.MinimumValue; break;
            case RequirementLeafTypes.MasteryBand: facts.SkillRanks.TryGetValue(expression.TargetId, out actual); satisfied = CoreResolutionPolicy0219.MasteryBonus(actual) >= expression.MinimumValue; break;
            case RequirementLeafTypes.SkillAcquired: facts.SkillRanks.TryGetValue(expression.TargetId, out actual); satisfied = actual > 0; break;
            case RequirementLeafTypes.Attribute: facts.Attributes.TryGetValue(expression.TargetId, out actual); satisfied = actual >= expression.MinimumValue; break;
            case RequirementLeafTypes.SubAttribute: facts.SubAttributes.TryGetValue(expression.TargetId, out actual); satisfied = actual >= expression.MinimumValue; break;
            case RequirementLeafTypes.Technique: satisfied = facts.TechniqueIds.Contains(expression.TargetId); actual = satisfied ? 1 : 0; break;
            case RequirementLeafTypes.Action: satisfied = facts.ActionIds.Contains(expression.TargetId); actual = satisfied ? 1 : 0; break;
            case RequirementLeafTypes.Knowledge: satisfied = facts.KnowledgeIds.Contains(expression.TargetId); actual = satisfied ? 1 : 0; break;
            case RequirementLeafTypes.Race: satisfied = facts.RaceIds.Contains(expression.TargetId); actual = satisfied ? 1 : 0; break;
            case RequirementLeafTypes.Subspecies: satisfied = facts.SubspeciesIds.Contains(expression.TargetId); actual = satisfied ? 1 : 0; break;
            case RequirementLeafTypes.Trait: satisfied = facts.TraitIds.Contains(expression.TargetId); actual = satisfied ? 1 : 0; break;
            case RequirementLeafTypes.BodyCompatibility: satisfied = facts.BodyCompatibilityIds.Contains(expression.TargetId); actual = satisfied ? 1 : 0; break;
            case RequirementLeafTypes.EquipmentTag: satisfied = facts.EquipmentTags.Contains(expression.TargetId); actual = satisfied ? 1 : 0; break;
            case RequirementLeafTypes.Tool: satisfied = facts.ToolIds.Contains(expression.TargetId); actual = satisfied ? 1 : 0; break;
            case RequirementLeafTypes.WeaponTag: satisfied = facts.WeaponTags.Contains(expression.TargetId); actual = satisfied ? 1 : 0; break;
            case RequirementLeafTypes.ArmorProficiency: satisfied = facts.ArmorProficiencyIds.Contains(expression.TargetId); actual = satisfied ? 1 : 0; break;
            case RequirementLeafTypes.WorldState: facts.WorldStateValues.TryGetValue(expression.TargetId, out actual); satisfied = actual >= expression.MinimumValue; break;
            case RequirementLeafTypes.Custom: facts.CustomValues.TryGetValue(expression.TargetId, out actual); satisfied = actual >= expression.MinimumValue; break;
            default: throw new ArgumentException("Unknown requirement leaf type: " + expression.LeafType);
        }
        var hidden = playerSafe && expression.IsHidden;
        var publicLabel = hidden ? "Скрытое условие" : First(expression.PublicLabel, "Условие развития");
        return new RequirementEvaluationResult
        {
            IsSatisfied = satisfied,
            Kind = RequirementExpressionKinds.Leaf,
            RequiredCount = Math.Max(1, expression.MinimumValue),
            SatisfiedCount = actual,
            PublicReason = publicLabel + (satisfied ? ": выполнено" : ": пока не выполнено"),
            GMReason = First(expression.GMLabel, expression.PublicLabel, expression.TargetId) + $" ({actual}/{Math.Max(1, expression.MinimumValue)})",
            SafeTargetReference = hidden ? string.Empty : expression.TargetId
        };
    }

    private static void ValidateRecursive(RequirementExpression expression, List<string> errors, HashSet<RequirementExpression> path)
    {
        if (expression == null) { errors.Add("requirement_expression_null"); return; }
        if (!path.Add(expression)) { errors.Add("requirement_expression_cycle"); return; }
        if (expression.Kind == RequirementExpressionKinds.Leaf)
        {
            if (string.IsNullOrWhiteSpace(expression.LeafType)) errors.Add("requirement_leaf_type_missing");
            if (string.IsNullOrWhiteSpace(expression.TargetId)) errors.Add("requirement_target_missing");
            if (expression.Children.Count > 0) errors.Add("requirement_leaf_children_forbidden");
        }
        else if (expression.Kind == RequirementExpressionKinds.AllOf || expression.Kind == RequirementExpressionKinds.AnyOf || expression.Kind == RequirementExpressionKinds.AtLeast)
        {
            if (expression.Children.Count == 0) errors.Add("requirement_group_empty");
            if (expression.Kind == RequirementExpressionKinds.AtLeast && (expression.RequiredCount < 1 || expression.RequiredCount > expression.Children.Count)) errors.Add("requirement_at_least_invalid_count");
            foreach (var child in expression.Children) ValidateRecursive(child, errors, path);
        }
        else errors.Add("requirement_kind_unknown");
        path.Remove(expression);
    }

    private static void ValidateReferencesRecursive(RequirementExpression expression, RequirementReferenceCatalog catalog, List<string> errors)
    {
        if (expression.Kind == RequirementExpressionKinds.Leaf)
        {
            var key = RequirementReferenceCatalog.Key(expression.LeafType, expression.TargetId);
            if (catalog.ArchivedReferences.Contains(key)) errors.Add("requirement_reference_archived:" + key);
            else if (!catalog.ActiveReferences.Contains(key)) errors.Add("requirement_reference_missing:" + key);
            return;
        }
        foreach (var child in expression.Children) ValidateReferencesRecursive(child, catalog, errors);
    }

    private static string LegacyLeafType(string type)
    {
        if (string.Equals(type, "node", StringComparison.OrdinalIgnoreCase)) return RequirementLeafTypes.DevelopmentNode;
        if (string.Equals(type, "skill", StringComparison.OrdinalIgnoreCase)) return RequirementLeafTypes.SkillRank;
        if (string.Equals(type, "stat", StringComparison.OrdinalIgnoreCase)) return RequirementLeafTypes.Attribute;
        return string.IsNullOrWhiteSpace(type) ? RequirementLeafTypes.DevelopmentNode : type.Trim().ToLowerInvariant();
    }

    private static int ParseMinimum(string value) => int.TryParse(value, out var parsed) ? parsed : 1;
    private static string GroupReason(string kind, int required, int actual, bool playerSafe) => $"{GroupLabel(kind, playerSafe)}: {actual} из {required}";
    private static string GroupLabel(string kind, bool playerSafe) => kind == RequirementExpressionKinds.AllOf ? "Все условия" : kind == RequirementExpressionKinds.AnyOf ? "Любое условие" : "Необходимое число условий";
    private static string First(params string[] values) => values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;
}
