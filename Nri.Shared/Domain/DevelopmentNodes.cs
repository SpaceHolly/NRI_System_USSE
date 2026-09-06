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

public static class DevelopmentHexagonIds
{
    public const string Main = "main_development_hexagon";
    public const string Magic = "magic_development_hexagon";
    public const string LargeTest0154 = "large_development_hexagon_0154";
}

public static class DevelopmentHexagonTypes
{
    public const string Main = "main";
    public const string Magic = "magic";
    public const string Thematic = "thematic";
    public const string Profession = "profession";
    public const string Faction = "faction";
    public const string Custom = "custom";
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
    public string DevelopmentNodeId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string HexagonId { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = string.Empty;
    public string NodeType { get; set; } = DevelopmentNodeTypes.Other;
    public string NodeRole { get; set; } = DevelopmentNodeRoleIds.Custom;
    public string Name { get; set; } = string.Empty;
    public string PublicName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string HiddenName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string PublicDescription { get; set; } = string.Empty;
    public string GMDescription { get; set; } = string.Empty;
    public string DirectionId { get; set; } = string.Empty;
    public string ParentNodeId { get; set; } = string.Empty;
    public string BranchId { get; set; } = string.Empty;
    public string SubBranchId { get; set; } = string.Empty;
    public int Tier { get; set; }
    public int MaxTier { get; set; } = 1;
    public bool IsRoot { get; set; }
    public bool IsMainBranch { get; set; }
    public bool IsSubBranch { get; set; }
    public bool IsRepeatable { get; set; }
    public List<DevelopmentRequirement> Requirements { get; set; } = new List<DevelopmentRequirement>();
    public RequirementExpression? RequirementExpression { get; set; }
    public DevelopmentCost Cost { get; set; } = new DevelopmentCost();
    public int CostExperienceCoins { get; set; }
    public string CostFormulaId { get; set; } = string.Empty;
    public bool ManualCostOverride { get; set; }
    public bool RequiresGMApproval { get; set; }
    public bool RequiresPlayerRequest { get; set; }
    public string UnlockPolicy { get; set; } = DevelopmentUnlockPolicyIds.VisibleByDefault;
    public string PurchasePolicy { get; set; } = DevelopmentPurchasePolicyIds.AutomaticIfRequirementsMet;
    public string RequirementSummary { get; set; } = string.Empty;
    public string RewardSummary { get; set; } = string.Empty;
    public List<DevelopmentReward> Rewards { get; set; } = new List<DevelopmentReward>();
    public List<string> LinkedSkillIds { get; set; } = new List<string>();
    public List<string> LinkedAttributeIds { get; set; } = new List<string>();
    public List<string> LinkedSubAttributeIds { get; set; } = new List<string>();
    public List<string> LinkedModuleIds { get; set; } = new List<string>();
    public string VisibilityRule { get; set; } = "default";
    public bool IsHidden { get; set; }
    public string HiddenDisplayName { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public int GridX { get; set; }
    public int GridY { get; set; }
    public double Angle { get; set; }
    public int Ring { get; set; }
    public string IconKey { get; set; } = string.Empty;
    public string ColorKey { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new List<string>();
    public bool IsArchived { get; set; }
    public string CalculationVersion { get; set; } = string.Empty;
    public Dictionary<string, object> ExtraData { get; set; } = new Dictionary<string, object>();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new Dictionary<string, object>();
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
    public string HexagonId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string HexagonType { get; set; } = string.Empty;
    public bool IsMainHexagon { get; set; }
    public bool IsDefaultForRuleSet { get; set; }
    public List<string> DirectionIds { get; set; } = new List<string>();
    public string RootNodeId { get; set; } = string.Empty;
    public string CenterNodeId { get; set; } = string.Empty;
    public List<DevelopmentDirectionDefinition> Directions { get; set; } = new List<DevelopmentDirectionDefinition>();
    public List<string> NodeIds { get; set; } = new List<string>();
    public string DisplayMode { get; set; } = "simple";
    public string VisibilityRule { get; set; } = "default";
    public List<string> Tags { get; set; } = new List<string>();
    public bool IsArchived { get; set; }
    public Dictionary<string, object> ExtraData { get; set; } = new Dictionary<string, object>();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new Dictionary<string, object>();
}

public class DevelopmentDirectionDefinition
{
    public string DirectionId { get; set; } = string.Empty;
    public string HexagonId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string AtmosphericName { get; set; } = string.Empty;
    public string AttributeId { get; set; } = string.Empty;
    public List<string> LinkedSubAttributeIds { get; set; } = new List<string>();
    public string Description { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public double AngleDegrees { get; set; }
    public string IconKey { get; set; } = string.Empty;
    public string ColorKey { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new List<string>();
    public Dictionary<string, object> ExtraData { get; set; } = new Dictionary<string, object>();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new Dictionary<string, object>();
}

public class ExperienceCoinLedgerEntry : EntityBase
{
    public string CampaignId { get; set; } = string.Empty;
    public string CharacterId { get; set; } = string.Empty;
    public string CharacterNameSnapshot { get; set; } = string.Empty;
    public string ActorUserId { get; set; } = string.Empty;
    public string EntryType { get; set; } = ExperienceCoinLedgerEntryTypeIds.Correction;
    public int Amount { get; set; }
    public int BalanceAfter { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public string SourceId { get; set; } = string.Empty;
    public string DevelopmentNodeId { get; set; } = string.Empty;
    public bool IsPlayerVisible { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object> ExtraData { get; set; } = new Dictionary<string, object>();
    public Dictionary<string, object> ServerOnlyData { get; set; } = new Dictionary<string, object>();
}

public static class DevelopmentNodeRoleIds
{
    public const string NoviceRoot = "novice_root";
    public const string MagicRoot = "magic_root";
    public const string PrimaryMagicClass = "primary_magic_class";
    public const string MagicElement = "magic_element";
    public const string MagicDirection = "magic_direction";
    public const string MainBranchLevel = "main_branch_level";
    public const string SubbranchLevel = "subbranch_level";
    public const string StandaloneSkill = "standalone_skill";
    public const string UnlockNode = "unlock_node";
    public const string HiddenNode = "hidden_node";
    public const string ThematicNode = "thematic_node";
    public const string Custom = "custom";
}

public static class DevelopmentUnlockPolicyIds
{
    public const string VisibleByDefault = "visible_by_default";
    public const string HiddenUntilRequirement = "hidden_until_requirement";
    public const string HiddenUntilGMReveal = "hidden_until_gm_reveal";
    public const string VisibleAsUnknown = "visible_as_unknown";
    public const string GMOnly = "gm_only";
    public const string Custom = "custom";
}

public static class DevelopmentPurchasePolicyIds
{
    public const string AutomaticIfRequirementsMet = "automatic_if_requirements_met";
    public const string UnavailableUntilDefined = "unavailable_until_defined";
    public const string RequiresGMApproval = "requires_gm_approval";
    public const string RequestOnly = "request_only";
    public const string GMOnly = "gm_only";
    public const string Custom = "custom";
}

public static class DevelopmentApprovalPolicy
{
    public static bool RequiresGMApproval(ClassNodeDefinition node)
        => node.RequiresGMApproval || string.Equals(node.PurchasePolicy, DevelopmentPurchasePolicyIds.RequiresGMApproval, StringComparison.OrdinalIgnoreCase);

    public static bool RequiresPlayerRequest(ClassNodeDefinition node)
        => node.RequiresPlayerRequest || string.Equals(node.PurchasePolicy, DevelopmentPurchasePolicyIds.RequestOnly, StringComparison.OrdinalIgnoreCase);
}

public static class ExperienceCoinLedgerEntryTypeIds
{
    public const string Grant = "grant";
    public const string Spend = "spend";
    public const string Refund = "refund";
    public const string Correction = "correction";
    public const string Purchase = "purchase";
    public const string GMOverride = "gm_override";
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
