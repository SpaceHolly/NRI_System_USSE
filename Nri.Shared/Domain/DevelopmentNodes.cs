using System;
using System.Collections.Generic;

namespace Nri.Shared.Domain;

// DevelopmentNode model is foundation-only for now.
// Legacy class/skill/character endpoints remain active until UseDevelopmentNodeModel is enabled.

public static class DevelopmentNodeTypes
{
    public const string Class = "class";
    public const string Branch = "branch";
    public const string Subbranch = "subbranch";
    public const string Skill = "skill";
    public const string UnclassedSkill = "unclassed_skill";
    public const string Profession = "profession";
    public const string Specialization = "specialization";
    public const string MagicPath = "magic_path";
    public const string SpellSchool = "spell_school";
    public const string CombatDoctrine = "combat_doctrine";
    public const string License = "license";
    public const string Training = "training";
    public const string FactionSchool = "faction_school";
    public const string TechnologyAccess = "technology_access";
    public const string Augmentation = "augmentation";
    public const string Implant = "implant";
    public const string Cyberware = "cyberware";
    public const string ResearchDiscipline = "research_discipline";
    public const string SocialStatus = "social_status";
    public const string HiddenDevelopment = "hidden_development";
    public const string Other = "other";
}

public static class DevelopmentDirectionIds
{
    // Сила — Натиск
    public const string StrengthAssault = "strength_assault";
    // Ловкость — Манёвр
    public const string DexterityManeuver = "dexterity_maneuver";
    // Выносливость — Стойкость
    public const string EnduranceResilience = "endurance_resilience";
    // Интеллект — Разум
    public const string IntellectReason = "intellect_reason";
    // Мудрость — Путь
    public const string WisdomPath = "wisdom_path";
    // Харизма — Влияние
    public const string CharismaInfluence = "charisma_influence";
}

public class DevelopmentNodeDefinition : EntityBase
{
    public string RuleSetId { get; set; } = string.Empty;
    public string NodeType { get; set; } = DevelopmentNodeTypes.Other;
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string DirectionId { get; set; } = string.Empty;
    public string ParentNodeId { get; set; } = string.Empty;
    public int Tier { get; set; }
    public int MaxTier { get; set; } = 1;
    public List<DevelopmentRequirement> Requirements { get; set; } = new List<DevelopmentRequirement>();
    public DevelopmentCost Cost { get; set; } = new DevelopmentCost();
    public List<DevelopmentReward> Rewards { get; set; } = new List<DevelopmentReward>();
    public List<string> LinkedSkillIds { get; set; } = new List<string>();
    public List<string> LinkedAttributeIds { get; set; } = new List<string>();
    public List<string> LinkedSubAttributeIds { get; set; } = new List<string>();
    public List<string> LinkedModuleIds { get; set; } = new List<string>();
    public string VisibilityRule { get; set; } = "default";
    public bool IsHidden { get; set; }
    public string HiddenDisplayName { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new List<string>();
    public bool IsArchived { get; set; }
}

public class DevelopmentRequirement
{
    public string RequirementType { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
    public int MinTier { get; set; }
    public int MinRank { get; set; }
    public string RequiredValue { get; set; } = string.Empty;
    public bool IsAnyOf { get; set; }
    public bool IsHidden { get; set; }
    public string Notes { get; set; } = string.Empty;
}

public class DevelopmentCost
{
    // For fantasy_nri_default this can be xp_coins, but model is ruleset-agnostic.
    public string CurrencyId { get; set; } = string.Empty;
    public int Amount { get; set; }
    public string CostModel { get; set; } = string.Empty;
    public bool ManualOverride { get; set; }
    public string Notes { get; set; } = string.Empty;
}

public class DevelopmentReward
{
    public string RewardType { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
    public int Value { get; set; }
    public string Text { get; set; } = string.Empty;
    public string VisibilityRule { get; set; } = "default";
    public List<string> Tags { get; set; } = new List<string>();
}

public class DevelopmentHexagonDefinition : EntityBase
{
    public string RuleSetId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string HexagonType { get; set; } = string.Empty;
    public List<string> DirectionIds { get; set; } = new List<string>();
    public string RootNodeId { get; set; } = string.Empty;
    public List<string> NodeIds { get; set; } = new List<string>();
    public string DisplayMode { get; set; } = "simple";
    public string VisibilityRule { get; set; } = "default";
    public List<string> Tags { get; set; } = new List<string>();
    public bool IsArchived { get; set; }
}

// Legacy adapter skeleton: maps existing ClassDefinition into DevelopmentNodeDefinition.
// This is not wired to repositories/services/endpoints yet.
public static class LegacyClassToDevelopmentNodeAdapter
{
    public static DevelopmentNodeDefinition FromClassDefinition(ClassDefinition source, string ruleSetId)
    {
        var classCode = source == null ? string.Empty : source.Code ?? string.Empty;
        var className = source == null ? string.Empty : source.Name ?? string.Empty;
        var classDescription = source == null ? string.Empty : source.Description ?? string.Empty;
        var branchCode = source == null ? string.Empty : source.BranchCode ?? string.Empty;
        var parentClassCode = source == null ? string.Empty : source.ParentClassCode ?? string.Empty;
        var maxTier = source == null ? 1 : (source.MaxLevel <= 0 ? 1 : source.MaxLevel);

        return new DevelopmentNodeDefinition
        {
            Id = classCode,
            RuleSetId = ruleSetId ?? string.Empty,
            NodeType = DevelopmentNodeTypes.Class,
            Name = className,
            DisplayName = className,
            Description = classDescription,
            DirectionId = branchCode,
            ParentNodeId = parentClassCode,
            Tier = 1,
            MaxTier = maxTier,
            LinkedSkillIds = source == null ? new List<string>() : new List<string>(source.GrantedSkillCodes ?? new List<string>()),
            LinkedModuleIds = new List<string> { CharacterModuleIds.Development },
            VisibilityRule = "default",
            IsHidden = false,
            IsArchived = source != null && (source.Archived || source.Status == DefinitionStatus.Archived)
        };
    }
}
