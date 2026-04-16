using System;
using System.Collections.Generic;
using System.Linq;
using Nri.Server.Application.Validation;
using Nri.Server.Audit;
using Nri.Server.Infrastructure.Mongo.Repositories;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;

namespace Nri.Server.Application.Services;

public sealed class ClassDefinitionService
{
    private readonly IClassDefinitionRepository _repository;
    private readonly DefinitionValidationService _validationService;
    private readonly AuditLogService _auditLogService;

    public ClassDefinitionService(IClassDefinitionRepository repository, DefinitionValidationService validationService, AuditLogService auditLogService)
    {
        _repository = repository;
        _validationService = validationService;
        _auditLogService = auditLogService;
    }

    public IReadOnlyCollection<ClassDefinitionDto> GetAll(bool includeArchived)
    {
        return _repository.GetAll(includeArchived).Select(Map).ToArray();
    }

    public ClassDefinitionDto GetByCode(string code)
    {
        var item = _repository.GetByCode(code) ?? throw new KeyNotFoundException("Class definition not found.");
        return Map(item);
    }

    public SaveClassResponse Save(ClassDefinitionDto dto, string actorUserId)
    {
        var existing = _repository.GetByCode(dto.Code);
        var definition = Map(dto, existing);
        if (existing != null && !string.Equals(existing.Code, dto.Code, StringComparison.Ordinal))
        {
            throw new ArgumentException("Class code is stable and cannot be changed.");
        }

        var parent = string.IsNullOrWhiteSpace(definition.ParentClassCode) ? null : _repository.GetByCode(definition.ParentClassCode);
        if (!string.IsNullOrWhiteSpace(definition.ParentClassCode) && parent == null)
        {
            throw new ArgumentException("ParentClassCode references a missing class.");
        }

        definition.UpdatedByUserId = actorUserId;
        if (existing == null)
        {
            definition.CreatedByUserId = actorUserId;
        }
        else
        {
            definition.CreatedByUserId = existing.CreatedByUserId;
            definition.CreatedUtc = existing.CreatedUtc;
            definition.Id = existing.Id;
        }

        _validationService.ValidateClass(definition, parent);
        var created = _repository.Upsert(definition);
        _auditLogService.Write("definitions.class", actorUserId, created ? "create" : "update", definition.Code, definition.Name);
        return new SaveClassResponse { Item = Map(definition), Created = created };
    }

    public bool Archive(string code, string actorUserId)
    {
        var archived = _repository.Archive(code, actorUserId);
        if (archived)
        {
            _auditLogService.Write("definitions.class", actorUserId, "archive", code, string.Empty);
        }

        return archived;
    }

    private static ClassDefinitionDto Map(ClassDefinition definition)
    {
        return new ClassDefinitionDto
        {
            Code = definition.Code,
            Name = definition.Name,
            Description = definition.Description,
            DirectionCode = definition.DirectionCode,
            BranchCode = definition.BranchCode,
            RootClassCode = definition.RootClassCode,
            ParentClassCode = definition.ParentClassCode,
            Level = definition.Level,
            UnlockLevel = definition.UnlockLevel,
            MaxLevel = definition.MaxLevel,
            RequiredRaceCodes = definition.RequiredRaceCodes.ToList(),
            GrantedSkillCodes = definition.GrantedSkillCodes.ToList(),
            RequiredClassCodes = definition.RequiredClassCodes.ToList(),
            RequiredSkillCodes = definition.RequiredSkillCodes.ToList(),
            RequiredCharacterLevel = definition.RequiredCharacterLevel,
            XpCoinCost = definition.XpCoinCost,
            IsActive = definition.IsActive,
            Status = definition.Status,
            CreatedUtc = definition.CreatedUtc,
            UpdatedUtc = definition.UpdatedUtc
        };
    }

    private static ClassDefinition Map(ClassDefinitionDto dto, ClassDefinition? existing)
    {
        var definition = existing ?? new ClassDefinition();
        definition.Code = dto.Code ?? string.Empty;
        definition.Name = dto.Name ?? string.Empty;
        definition.Description = dto.Description ?? string.Empty;
        definition.DirectionCode = dto.DirectionCode ?? string.Empty;
        definition.BranchCode = dto.BranchCode ?? string.Empty;
        definition.RootClassCode = dto.RootClassCode ?? string.Empty;
        definition.ParentClassCode = dto.ParentClassCode ?? string.Empty;
        definition.Level = dto.Level;
        definition.UnlockLevel = dto.UnlockLevel;
        definition.MaxLevel = dto.MaxLevel;
        definition.RequiredRaceCodes = dto.RequiredRaceCodes ?? new List<string>();
        definition.GrantedSkillCodes = dto.GrantedSkillCodes ?? new List<string>();
        definition.RequiredClassCodes = dto.RequiredClassCodes ?? new List<string>();
        definition.RequiredSkillCodes = dto.RequiredSkillCodes ?? new List<string>();
        definition.RequiredCharacterLevel = dto.RequiredCharacterLevel;
        definition.XpCoinCost = dto.XpCoinCost;
        definition.IsActive = dto.IsActive;
        definition.Status = dto.Status;
        return definition;
    }
}

public sealed class SkillDefinitionService
{
    private readonly ISkillDefinitionRepository _repository;
    private readonly DefinitionValidationService _validationService;
    private readonly AuditLogService _auditLogService;

    public SkillDefinitionService(ISkillDefinitionRepository repository, DefinitionValidationService validationService, AuditLogService auditLogService)
    {
        _repository = repository;
        _validationService = validationService;
        _auditLogService = auditLogService;
    }

    public IReadOnlyCollection<SkillDefinitionDto> GetAll(bool includeArchived)
    {
        return _repository.GetAll(includeArchived).Select(Map).ToArray();
    }

    public SkillDefinitionDto GetByCode(string code)
    {
        var item = _repository.GetByCode(code) ?? throw new KeyNotFoundException("Skill definition not found.");
        return Map(item);
    }

    public SaveSkillResponse Save(SkillDefinitionDto dto, string actorUserId)
    {
        var existing = _repository.GetByCode(dto.Code);
        var definition = Map(dto, existing);
        if (existing != null && !string.Equals(existing.Code, dto.Code, StringComparison.Ordinal))
        {
            throw new ArgumentException("Skill code is stable and cannot be changed.");
        }

        definition.UpdatedByUserId = actorUserId;
        if (existing == null)
        {
            definition.CreatedByUserId = actorUserId;
        }
        else
        {
            definition.CreatedByUserId = existing.CreatedByUserId;
            definition.CreatedUtc = existing.CreatedUtc;
            definition.Id = existing.Id;
        }

        _validationService.ValidateSkill(definition);
        var created = _repository.Upsert(definition);
        _auditLogService.Write("definitions.skill", actorUserId, created ? "create" : "update", definition.Code, definition.Name);
        return new SaveSkillResponse { Item = Map(definition), Created = created };
    }

    public bool Archive(string code, string actorUserId)
    {
        var archived = _repository.Archive(code, actorUserId);
        if (archived)
        {
            _auditLogService.Write("definitions.skill", actorUserId, "archive", code, string.Empty);
        }

        return archived;
    }

    private static SkillDefinitionDto Map(SkillDefinition definition)
    {
        return new SkillDefinitionDto
        {
            Code = definition.Code,
            Name = definition.Name,
            Description = definition.Description,
            Tier = definition.Tier,
            MaxLevel = definition.MaxLevel,
            SkillCategory = definition.SkillCategory,
            IsClassSkill = definition.IsClassSkill,
            RequiredRaceCodes = definition.RequiredRaceCodes.ToList(),
            RequiredClassCodes = definition.RequiredClassCodes.ToList(),
            RequiredSkillCodes = definition.RequiredSkillCodes.ToList(),
            RequiredCharacterLevel = definition.RequiredCharacterLevel,
            XpCoinCost = definition.XpCoinCost,
            Levels = definition.Levels.Select(level => new SkillLevelDefinition
            {
                Level = level.Level,
                Description = level.Description,
                Requirements = level.Requirements.ToList(),
                Effects = level.Effects.ToList()
            }).ToList(),
            IsActive = definition.IsActive,
            Status = definition.Status,
            CreatedUtc = definition.CreatedUtc,
            UpdatedUtc = definition.UpdatedUtc
        };
    }

    private static SkillDefinition Map(SkillDefinitionDto dto, SkillDefinition? existing)
    {
        var definition = existing ?? new SkillDefinition();
        definition.Code = dto.Code ?? string.Empty;
        definition.Name = dto.Name ?? string.Empty;
        definition.Description = dto.Description ?? string.Empty;
        definition.Tier = dto.Tier;
        definition.MaxLevel = dto.MaxLevel;
        definition.SkillCategory = dto.SkillCategory;
        definition.IsClassSkill = dto.IsClassSkill;
        definition.RequiredRaceCodes = dto.RequiredRaceCodes ?? new List<string>();
        definition.RequiredClassCodes = dto.RequiredClassCodes ?? new List<string>();
        definition.RequiredSkillCodes = dto.RequiredSkillCodes ?? new List<string>();
        definition.RequiredCharacterLevel = dto.RequiredCharacterLevel;
        definition.XpCoinCost = dto.XpCoinCost;
        definition.Levels = dto.Levels ?? new List<SkillLevelDefinition>();
        definition.IsActive = dto.IsActive;
        definition.Status = dto.Status;
        return definition;
    }
}

public sealed class RaceDefinitionService
{
    private readonly IRaceDefinitionRepository _repository;
    private readonly DefinitionValidationService _validationService;
    private readonly AuditLogService _auditLogService;

    public RaceDefinitionService(IRaceDefinitionRepository repository, DefinitionValidationService validationService, AuditLogService auditLogService)
    {
        _repository = repository;
        _validationService = validationService;
        _auditLogService = auditLogService;
    }

    public IReadOnlyCollection<RaceDefinitionDto> GetAll(bool includeArchived)
    {
        return _repository.GetAll(includeArchived).Select(Map).ToArray();
    }

    public RaceDefinitionDto GetByCode(string code)
    {
        var item = _repository.GetByCode(code) ?? throw new KeyNotFoundException("Race definition not found.");
        return Map(item);
    }

    public SaveRaceResponse Save(RaceDefinitionDto dto, string actorUserId)
    {
        var existing = _repository.GetByCode(dto.Code);
        var definition = Map(dto, existing);
        _validationService.ValidateRace(definition);

        definition.UpdatedByUserId = actorUserId;
        if (existing == null)
        {
            definition.CreatedByUserId = actorUserId;
        }
        else
        {
            definition.CreatedByUserId = existing.CreatedByUserId;
            definition.CreatedUtc = existing.CreatedUtc;
            definition.Id = existing.Id;
        }

        var created = _repository.Upsert(definition);
        _auditLogService.Write("definitions.race", actorUserId, created ? "create" : "update", definition.Code, definition.Name);
        return new SaveRaceResponse { Item = Map(definition), Created = created };
    }

    public bool Archive(string code, string actorUserId)
    {
        var archived = _repository.Archive(code, actorUserId);
        if (archived) _auditLogService.Write("definitions.race", actorUserId, "archive", code, string.Empty);
        return archived;
    }

    private static RaceDefinitionDto Map(RaceDefinition definition)
    {
        return new RaceDefinitionDto
        {
            Code = definition.Code,
            Name = definition.Name,
            Description = definition.Description,
            Bonuses = definition.Bonuses.ToDictionary(x => x.Key, x => x.Value),
            Restrictions = definition.Restrictions.ToList(),
            IsActive = definition.IsActive,
            Status = definition.Status,
            CreatedUtc = definition.CreatedUtc,
            UpdatedUtc = definition.UpdatedUtc
        };
    }

    private static RaceDefinition Map(RaceDefinitionDto dto, RaceDefinition? existing)
    {
        var definition = existing ?? new RaceDefinition();
        definition.Code = dto.Code ?? string.Empty;
        definition.Name = dto.Name ?? string.Empty;
        definition.Description = dto.Description ?? string.Empty;
        definition.Bonuses = dto.Bonuses ?? new Dictionary<string, int>();
        definition.Restrictions = dto.Restrictions ?? new List<string>();
        definition.IsActive = dto.IsActive;
        definition.Status = dto.Status;
        return definition;
    }
}
