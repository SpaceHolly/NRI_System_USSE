using System;
using System.Collections.Generic;
using System.Linq;
using Nri.Server.Infrastructure.Mongo.Repositories;
using Nri.Shared.Domain;

namespace Nri.Server.Application.Validation;

public sealed class ClassDefinitionValidator
{
    public void Validate(ClassDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition.Code)) throw new ArgumentException("Class code is required.");
        if (definition.Level < 1 || definition.Level > 20) throw new ArgumentException("Class level must be in range 1..20.");
        if (definition.MaxLevel <= 0) throw new ArgumentException("MaxLevel must be greater than zero.");
        if (definition.UnlockLevel <= 0) throw new ArgumentException("UnlockLevel must be greater than zero.");
        if (definition.XpCoinCost < 0) throw new ArgumentException("XpCoinCost must be non-negative.");
        if (string.IsNullOrWhiteSpace(definition.DirectionCode)) throw new ArgumentException("DirectionCode is required.");
        if (string.IsNullOrWhiteSpace(definition.BranchCode)) throw new ArgumentException("BranchCode is required.");
        if (string.IsNullOrWhiteSpace(definition.RootClassCode)) throw new ArgumentException("RootClassCode is required.");
    }
}

public sealed class RaceDefinitionValidator
{
    public void Validate(RaceDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition.Code)) throw new ArgumentException("Race code is required.");
        if (string.IsNullOrWhiteSpace(definition.Name)) throw new ArgumentException("Race name is required.");
    }
}

public sealed class SkillDefinitionValidator
{
    public void Validate(SkillDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition.Code)) throw new ArgumentException("Skill code is required.");
        if (definition.Tier <= 0) throw new ArgumentException("Tier must be greater than zero.");
        if (definition.MaxLevel < 1) throw new ArgumentException("MaxLevel must be at least 1.");
        if (definition.XpCoinCost < 0) throw new ArgumentException("XpCoinCost must be non-negative.");
        if (definition.Levels == null || definition.Levels.Count == 0) throw new ArgumentException("Skill levels are required.");

        var ordered = definition.Levels.OrderBy(x => x.Level).ToList();
        for (var index = 0; index < ordered.Count; index++)
        {
            var expected = index + 1;
            if (ordered[index].Level != expected)
            {
                throw new ArgumentException("Skill levels must be sequential starting at 1.");
            }
        }

        if (ordered.Count != definition.MaxLevel)
        {
            throw new ArgumentException("Skill levels count must match MaxLevel.");
        }
    }
}

public sealed class DefinitionReferenceValidator
{
    private readonly IClassDefinitionRepository _classRepository;
    private readonly IRaceDefinitionRepository _raceRepository;
    private readonly ISkillDefinitionRepository _skillRepository;

    public DefinitionReferenceValidator(IClassDefinitionRepository classRepository, IRaceDefinitionRepository raceRepository, ISkillDefinitionRepository skillRepository)
    {
        _classRepository = classRepository;
        _raceRepository = raceRepository;
        _skillRepository = skillRepository;
    }

    public void ValidateClassReferences(ClassDefinition definition, ClassDefinition? parent)
    {
        if (parent != null)
        {
            if (parent.Level >= 20) throw new ArgumentException("Cannot create descendants for a level 20 class.");
            if (definition.Level != parent.Level + 1) throw new ArgumentException("Child class level must equal parent level + 1.");
            EnsureNoClassCycle(definition.Code, parent.Code);
        }

        foreach (var requiredClassCode in definition.RequiredClassCodes.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!_classRepository.Exists(requiredClassCode)) throw new ArgumentException($"Required class '{requiredClassCode}' was not found.");
        }

        foreach (var grantedSkillCode in definition.GrantedSkillCodes.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!_skillRepository.Exists(grantedSkillCode)) throw new ArgumentException($"Granted skill '{grantedSkillCode}' was not found.");
        }
        foreach (var raceCode in definition.RequiredRaceCodes.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!_raceRepository.Exists(raceCode)) throw new ArgumentException($"Required race '{raceCode}' was not found.");
        }
        foreach (var skillCode in definition.RequiredSkillCodes.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!_skillRepository.Exists(skillCode)) throw new ArgumentException($"Required skill '{skillCode}' was not found.");
        }
    }

    public void ValidateSkillReferences(SkillDefinition definition)
    {
        foreach (var classCode in definition.RequiredClassCodes.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!_classRepository.Exists(classCode)) throw new ArgumentException($"Required class '{classCode}' was not found.");
        }
        foreach (var raceCode in definition.RequiredRaceCodes.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!_raceRepository.Exists(raceCode)) throw new ArgumentException($"Required race '{raceCode}' was not found.");
        }

        foreach (var skillCode in definition.RequiredSkillCodes.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!string.Equals(skillCode, definition.Code, StringComparison.OrdinalIgnoreCase) && !_skillRepository.Exists(skillCode))
            {
                throw new ArgumentException($"Required skill '{skillCode}' was not found.");
            }
        }
    }

    private void EnsureNoClassCycle(string code, string parentCode)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { code };
        var currentCode = parentCode;
        while (!string.IsNullOrWhiteSpace(currentCode))
        {
            if (!visited.Add(currentCode)) throw new ArgumentException("Cyclic class hierarchy is not allowed.");
            var current = _classRepository.GetByCode(currentCode);
            if (current == null) return;
            currentCode = current.ParentClassCode;
        }
    }
}

public sealed class DefinitionValidationService
{
    private readonly ClassDefinitionValidator _classValidator;
    private readonly RaceDefinitionValidator _raceValidator;
    private readonly SkillDefinitionValidator _skillValidator;
    private readonly DefinitionReferenceValidator _referenceValidator;

    public DefinitionValidationService(ClassDefinitionValidator classValidator, RaceDefinitionValidator raceValidator, SkillDefinitionValidator skillValidator, DefinitionReferenceValidator referenceValidator)
    {
        _classValidator = classValidator;
        _raceValidator = raceValidator;
        _skillValidator = skillValidator;
        _referenceValidator = referenceValidator;
    }

    public void ValidateRace(RaceDefinition definition)
    {
        _raceValidator.Validate(definition);
    }

    public void ValidateClass(ClassDefinition definition, ClassDefinition? parent)
    {
        _classValidator.Validate(definition);
        _referenceValidator.ValidateClassReferences(definition, parent);
    }

    public void ValidateSkill(SkillDefinition definition)
    {
        _skillValidator.Validate(definition);
        _referenceValidator.ValidateSkillReferences(definition);
    }
}
