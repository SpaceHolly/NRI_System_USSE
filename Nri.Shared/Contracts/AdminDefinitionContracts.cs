using System;
using System.Collections.Generic;
using Nri.Shared.Domain;

namespace Nri.Shared.Contracts;

public class GetClassListRequest
{
    public bool IncludeArchived { get; set; }
}

public class GetRaceListRequest
{
    public bool IncludeArchived { get; set; }
}

public class GetRaceByCodeRequest
{
    public string Code { get; set; } = string.Empty;
}

public class SaveRaceRequest
{
    public RaceDefinitionDto Definition { get; set; } = new RaceDefinitionDto();
}

public class ArchiveRaceRequest
{
    public string Code { get; set; } = string.Empty;
}

public class GetClassByCodeRequest
{
    public string Code { get; set; } = string.Empty;
}

public class SaveClassRequest
{
    public ClassDefinitionDto Definition { get; set; } = new ClassDefinitionDto();
}

public class ArchiveClassRequest
{
    public string Code { get; set; } = string.Empty;
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

public class ArchiveSkillRequest
{
    public string Code { get; set; } = string.Empty;
}

public class GetClassListResponse
{
    public IReadOnlyCollection<ClassDefinitionDto> Items { get; set; } = Array.Empty<ClassDefinitionDto>();
}

public class GetRaceListResponse
{
    public IReadOnlyCollection<RaceDefinitionDto> Items { get; set; } = Array.Empty<RaceDefinitionDto>();
}

public class GetRaceByCodeResponse
{
    public RaceDefinitionDto? Item { get; set; }
}

public class SaveRaceResponse
{
    public RaceDefinitionDto Item { get; set; } = new RaceDefinitionDto();
    public bool Created { get; set; }
}

public class ArchiveRaceResponse
{
    public string Code { get; set; } = string.Empty;
    public bool Archived { get; set; }
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

public class ArchiveClassResponse
{
    public string Code { get; set; } = string.Empty;
    public bool Archived { get; set; }
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

public class ArchiveSkillResponse
{
    public string Code { get; set; } = string.Empty;
    public bool Archived { get; set; }
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
    public int UnlockLevel { get; set; } = 1;
    public int MaxLevel { get; set; } = 1;
    public List<string> RequiredRaceCodes { get; set; } = new List<string>();
    public List<string> GrantedSkillCodes { get; set; } = new List<string>();
    public List<string> RequiredClassCodes { get; set; } = new List<string>();
    public List<string> RequiredSkillCodes { get; set; } = new List<string>();
    public int RequiredCharacterLevel { get; set; }
    public int XpCoinCost { get; set; }
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
    public List<string> RequiredRaceCodes { get; set; } = new List<string>();
    public List<string> RequiredClassCodes { get; set; } = new List<string>();
    public List<string> RequiredSkillCodes { get; set; } = new List<string>();
    public int RequiredCharacterLevel { get; set; }
    public int XpCoinCost { get; set; }
    public List<SkillLevelDefinition> Levels { get; set; } = new List<SkillLevelDefinition>();
    public bool IsActive { get; set; } = true;
    public DefinitionStatus Status { get; set; } = DefinitionStatus.Draft;
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}

public class RaceDefinitionDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Dictionary<string, int> Bonuses { get; set; } = new Dictionary<string, int>();
    public List<string> Restrictions { get; set; } = new List<string>();
    public bool IsActive { get; set; } = true;
    public DefinitionStatus Status { get; set; } = DefinitionStatus.Draft;
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}
