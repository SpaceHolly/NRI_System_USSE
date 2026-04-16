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
