using System;
using System.Collections.Generic;

namespace Nri.Shared.Domain;

public enum DefinitionStatus
{
    Draft,
    Active,
    Archived
}

public enum SkillCategory
{
    Undefined,
    Active,
    Passive,
    Support,
    Utility,
    Ultimate
}

public class RequirementDefinition
{
    public string RequirementType { get; set; } = string.Empty;
    public string TargetCode { get; set; } = string.Empty;
    public int? MinimumValue { get; set; }
    public string Description { get; set; } = string.Empty;
}

public class EffectDefinition
{
    public string EffectType { get; set; } = string.Empty;
    public string TargetCode { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class SkillLevelDefinition
{
    public int Level { get; set; }
    public string Description { get; set; } = string.Empty;
    public List<RequirementDefinition> Requirements { get; set; } = new List<RequirementDefinition>();
    public List<EffectDefinition> Effects { get; set; } = new List<EffectDefinition>();
}

public abstract class DefinitionDocumentBase : EntityBase
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DefinitionStatus Status { get; set; } = DefinitionStatus.Draft;
    public bool IsActive { get; set; } = true;
    public string CreatedByUserId { get; set; } = string.Empty;
    public string UpdatedByUserId { get; set; } = string.Empty;
    public DateTime? ArchivedUtc { get; set; }
    public string ArchivedByUserId { get; set; } = string.Empty;
}

public class ClassDefinition : DefinitionDocumentBase
{
    public string DirectionCode { get; set; } = string.Empty;
    public string BranchCode { get; set; } = string.Empty;
    public string RootClassCode { get; set; } = string.Empty;
    public string ParentClassCode { get; set; } = string.Empty;
    public string RequiredHexagonId { get; set; } = "main_development_hexagon";
    public string RequiredNodeId { get; set; } = string.Empty;
    public string VisibilityRule { get; set; } = "hexagon-gated";
    public bool IsPlayerVisible { get; set; }
    public bool IsLockedOutsideHexagon { get; set; } = true;
    public List<string> Tags { get; set; } = new List<string>();
    public int SortOrder { get; set; }
    public int Level { get; set; }
    public int UnlockLevel { get; set; } = 1;
    public int MaxLevel { get; set; } = 1;
    public List<string> RequiredRaceCodes { get; set; } = new List<string>();
    public List<string> GrantedSkillCodes { get; set; } = new List<string>();
    public List<string> RequiredClassCodes { get; set; } = new List<string>();
    public List<string> RequiredSkillCodes { get; set; } = new List<string>();
    public int RequiredCharacterLevel { get; set; }
    public int XpCoinCost { get; set; }
}

public class SkillDefinition : DefinitionDocumentBase
{
    // v2: display grouping for client presentation.
    public string DisplayGroup { get; set; } = string.Empty;
    // v2: primary attribute for roll calculations.
    public string DefaultAttribute { get; set; } = string.Empty;
    // v2: allowed alternative attributes for this skill.
    public List<string> AllowedAttributes { get; set; } = new List<string>();
    // v2: optional default subattribute used by server-side skill checks.
    public string DefaultSubAttribute { get; set; } = string.Empty;
    // v2: allowed subattributes for player-selected checks.
    public List<string> AllowedSubAttributes { get; set; } = new List<string>();
    // v2: subattribute behavior for roll calculation.
    public string SubAttributeMode { get; set; } = "none";
    // v2: supported rank range for profile-based progression.
    public int RankMin { get; set; }
    public int RankMax { get; set; } = 20;
    // v2: whether this skill can be rolled.
    public bool IsRollable { get; set; } = true;
    // internal compatibility marker: true when value explicitly set via v2 payload.
    public bool IsRollableExplicitlySet { get; set; }
    // v2: visibility hint used by future RuleSet/profile adapters.
    public string VisibilityRule { get; set; } = "default";
    // v2: explicit archive marker for future profile-first contracts.
    public bool IsArchived { get; set; }

    public int Tier { get; set; }
    public int MaxLevel { get; set; }
    public SkillCategory SkillCategory { get; set; } = SkillCategory.Undefined;
    public bool IsClassSkill { get; set; }
    public List<string> RequiredRaceCodes { get; set; } = new List<string>();
    public List<string> RequiredClassCodes { get; set; } = new List<string>();
    public List<string> RequiredSkillCodes { get; set; } = new List<string>();
    public int RequiredCharacterLevel { get; set; }
    public int XpCoinCost { get; set; }
    public List<SkillLevelDefinition> Levels { get; set; } = new List<SkillLevelDefinition>();
}

public class RaceDefinition : DefinitionDocumentBase
{
    public Dictionary<string, int> Bonuses { get; set; } = new Dictionary<string, int>();
    public List<string> Restrictions { get; set; } = new List<string>();
}
