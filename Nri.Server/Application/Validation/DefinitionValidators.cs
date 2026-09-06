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
        if (definition.RequirementExpression != null) RequirementExpressionEvaluator0219.Validate(definition.RequirementExpression);
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
        SkillDefinitionV2Defaults.Normalize(definition);
        if (string.IsNullOrWhiteSpace(definition.Code)) throw new ArgumentException("Skill code is required.");
        if (string.IsNullOrWhiteSpace(definition.Name)) throw new ArgumentException("Skill name is required.");
        if (definition.Tier <= 0) throw new ArgumentException("Tier must be greater than zero.");
        if (definition.MaxLevel < 1) throw new ArgumentException("MaxLevel must be at least 1.");
        if (definition.XpCoinCost < 0) throw new ArgumentException("XpCoinCost must be non-negative.");
        if (definition.RankMin < 0) throw new ArgumentException("RankMin must be non-negative.");
        if (definition.RankMax < definition.RankMin) throw new ArgumentException("RankMax must be greater than or equal to RankMin.");
        if (definition.RankMax > 20) throw new ArgumentException("RankMax must not exceed 20 for the active fantasy RuleSet.");
        if (definition.RequirementExpression != null) RequirementExpressionEvaluator0219.Validate(definition.RequirementExpression);
        foreach (var milestone in definition.RankMilestones ?? new List<SkillRankMilestoneDefinition>())
        {
            if (milestone.Rank < definition.RankMin || milestone.Rank > definition.RankMax) throw new ArgumentException("Skill milestone rank is outside the configured rank range.");
            if (milestone.RequirementExpression != null) RequirementExpressionEvaluator0219.Validate(milestone.RequirementExpression);
        }
        foreach (var technique in definition.Techniques ?? new List<SkillTechniqueDefinition>())
        {
            if (string.IsNullOrWhiteSpace(technique.Id) || string.IsNullOrWhiteSpace(technique.DisplayName)) throw new ArgumentException("Skill technique id and display name are required.");
            if (!string.Equals(technique.SkillId, definition.Code, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Skill technique must reference its owning skill.");
            if (technique.MinimumRank < definition.RankMin || technique.MinimumRank > definition.RankMax) throw new ArgumentException("Skill technique rank is outside the configured rank range.");
            if (technique.MaximumRank.HasValue && technique.MaximumRank.Value < technique.MinimumRank) throw new ArgumentException("Skill technique maximum rank must not be below minimum rank.");
            if (technique.RequirementExpression != null) RequirementExpressionEvaluator0219.Validate(technique.RequirementExpression);
        }
        if (definition.IsRollable && string.IsNullOrWhiteSpace(definition.DefaultAttribute)) throw new ArgumentException("DefaultAttribute is required for rollable skills.");
        if (!string.IsNullOrWhiteSpace(definition.DefaultAttribute) &&
            definition.AllowedAttributes.All(x => !string.Equals(x, definition.DefaultAttribute, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException("DefaultAttribute must be present in AllowedAttributes.");
        }
        if (definition.AllowedAttributes.Any(x => string.IsNullOrWhiteSpace(x)))
        {
            throw new ArgumentException("AllowedAttributes must not contain empty values.");
        }
        if (!string.IsNullOrWhiteSpace(definition.DefaultSubAttribute) &&
            definition.AllowedSubAttributes.All(x => !string.Equals(x, definition.DefaultSubAttribute, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException("DefaultSubAttribute must be present in AllowedSubAttributes.");
        }
        if (definition.AllowedSubAttributes.Any(x => string.IsNullOrWhiteSpace(x)))
        {
            throw new ArgumentException("AllowedSubAttributes must not contain empty values.");
        }
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
