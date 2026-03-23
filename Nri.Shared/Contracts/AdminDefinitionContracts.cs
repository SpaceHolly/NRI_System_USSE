using System;
using System.Collections.Generic;
using Nri.Shared.Domain;

namespace Nri.Shared.Contracts;

public class GetClassListRequest
{
    public bool IncludeArchived { get; set; }
}

public class GetClassByCodeRequest
{
    public string Code { get; set; } = string.Empty;
}

public class SaveClassRequest
{
    public ClassDefinitionDto Definition { get; set; } = new ClassDefinitionDto();
}

public class GetSkillListRequest
{
    public bool IncludeArchived { get; set; }
}

public class GetSkillByCodeRequest
{
    public string Code { get; set; } = string.Empty;
}

public class SaveSkillRequest
{
    public SkillDefinitionDto Definition { get; set; } = new SkillDefinitionDto();
}

public class GetClassListResponse
{
    public IReadOnlyCollection<ClassDefinitionDto> Items { get; set; } = Array.Empty<ClassDefinitionDto>();
}

public class GetClassByCodeResponse
{
    public ClassDefinitionDto? Item { get; set; }
}

public class SaveClassResponse
{
    public ClassDefinitionDto Item { get; set; } = new ClassDefinitionDto();
    public bool Created { get; set; }
}

public class GetSkillListResponse
{
    public IReadOnlyCollection<SkillDefinitionDto> Items { get; set; } = Array.Empty<SkillDefinitionDto>();
}

public class GetSkillByCodeResponse
{
    public SkillDefinitionDto? Item { get; set; }
}

public class SaveSkillResponse
{
    public SkillDefinitionDto Item { get; set; } = new SkillDefinitionDto();
    public bool Created { get; set; }
}

public class ClassDefinitionDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string DirectionCode { get; set; } = string.Empty;
    public string BranchCode { get; set; } = string.Empty;
    public string RootClassCode { get; set; } = string.Empty;
    public string ParentClassCode { get; set; } = string.Empty;
    public int Level { get; set; }
    public List<string> GrantedSkillCodes { get; set; } = new List<string>();
    public List<string> RequiredClassCodes { get; set; } = new List<string>();
    public bool IsActive { get; set; } = true;
    public DefinitionStatus Status { get; set; } = DefinitionStatus.Draft;
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}

public class SkillDefinitionDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Tier { get; set; }
    public int MaxLevel { get; set; }
    public SkillCategory SkillCategory { get; set; } = SkillCategory.Undefined;
    public bool IsClassSkill { get; set; }
    public List<string> RequiredClassCodes { get; set; } = new List<string>();
    public List<string> RequiredSkillCodes { get; set; } = new List<string>();
    public List<SkillLevelDefinition> Levels { get; set; } = new List<SkillLevelDefinition>();
    public bool IsActive { get; set; } = true;
    public DefinitionStatus Status { get; set; } = DefinitionStatus.Draft;
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}
