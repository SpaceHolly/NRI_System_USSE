using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Driver;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;
using Nri.Shared.Utilities;

namespace Nri.Server.Application;

public partial class ServiceHub
{
    public ResponseEnvelope CharacterProgressionGet(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var character = ResolveCharacterForClassSkill(context, actor);
        EnsureProgressCollections(character);
        EnsureCharacterDefaults(character);
        return Ok("Character progression loaded.", BuildProgressionPayload(character));
    }

    public ResponseEnvelope ProgressionAvailableRaces(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var character = ResolveCharacterForClassSkill(context, actor);
        var races = _repositories.RaceDefinitions.GetAll(includeArchived: false)
            .Where(x => x.IsActive && x.Status != DefinitionStatus.Archived)
            .Select(x => new Dictionary<string, object>
            {
                { "code", x.Code }, { "name", x.Name }, { "description", x.Description },
                { "available", true }, { "reasons", Array.Empty<object>() }
            }).Cast<object>().ToArray();
        _logger.Admin($"progression.available.races actor={actor.Login} count={races.Length}");
        return Ok("Available races loaded.", new Dictionary<string, object> { { "items", races } });
    }

    public ResponseEnvelope ProgressionAvailableClasses(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var character = ResolveCharacterForClassSkill(context, actor);
        EnsureProgressCollections(character);
        var items = _repositories.ClassDefinitions.GetAll(includeArchived: false)
            .Where(x => x.IsActive && x.Status != DefinitionStatus.Archived)
            .Select(x =>
            {
                var reasons = EvaluateClassRequirements(character, x);
                return new Dictionary<string, object>
                {
                    { "code", x.Code }, { "name", x.Name }, { "description", x.Description },
                    { "available", reasons.Count == 0 }, { "reasons", reasons.Cast<object>().ToArray() },
                    { "xpCoinCost", x.XpCoinCost }, { "maxLevel", x.MaxLevel }, { "unlockLevel", x.UnlockLevel }
                };
            }).Cast<object>().ToArray();
        _logger.Admin($"progression.available.classes actor={actor.Login} count={items.Length}");
        return Ok("Available classes loaded.", new Dictionary<string, object> { { "items", items } });
    }

    public ResponseEnvelope ProgressionAvailableSkills(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var character = ResolveCharacterForClassSkill(context, actor);
        EnsureProgressCollections(character);
        var items = _repositories.DefinitionSkills.GetAll(includeArchived: false)
            .Where(x => x.IsActive && x.Status != DefinitionStatus.Archived)
            .Select(x =>
            {
                var reasons = EvaluateSkillRequirements(character, x);
                return new Dictionary<string, object>
                {
                    { "code", x.Code }, { "name", x.Name }, { "description", x.Description },
                    { "available", reasons.Count == 0 }, { "reasons", reasons.Cast<object>().ToArray() },
                    { "xpCoinCost", x.XpCoinCost }, { "tier", x.Tier }, { "maxLevel", x.MaxLevel }
                };
            }).Cast<object>().ToArray();
        _logger.Admin($"progression.available.skills actor={actor.Login} count={items.Length}");
        return Ok("Available skills loaded.", new Dictionary<string, object> { { "items", items } });
    }

    public ResponseEnvelope ProgressionPreview(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var character = ResolveCharacterForClassSkill(context, actor);
        var targetType = (PayloadReader.GetString(context.Request.Payload, "targetType") ?? string.Empty).Trim().ToLowerInvariant();
        var targetCode = RequireLength(PayloadReader.GetString(context.Request.Payload, "targetCode"), 1, 128, "targetCode");
        var reasons = new List<string>();
        var cost = 0;
        var changes = new List<string>();
        if (targetType == "race")
        {
            var race = _repositories.RaceDefinitions.GetByCode(targetCode) ?? throw new KeyNotFoundException("Race definition not found.");
            if (!race.IsActive || race.Status == DefinitionStatus.Archived) reasons.Add("Раса недоступна.");
            changes.Add($"race:{race.Code}");
        }
        else if (targetType == "class")
        {
            var definition = _repositories.ClassDefinitions.GetByCode(targetCode) ?? throw new KeyNotFoundException("Class definition not found.");
            reasons = EvaluateClassRequirements(character, definition);
            cost = definition.XpCoinCost;
            changes.Add($"class:{definition.Code}");
        }
        else if (targetType == "skill")
        {
            var definition = _repositories.DefinitionSkills.GetByCode(targetCode) ?? throw new KeyNotFoundException("Skill definition not found.");
            reasons = EvaluateSkillRequirements(character, definition);
            cost = definition.XpCoinCost;
            changes.Add($"skill:{definition.Code}");
        }
        else
        {
            throw new ArgumentException("Unsupported targetType.");
        }

        _logger.Admin($"progression.preview actor={actor.Login} type={targetType} code={targetCode} ok={reasons.Count == 0}");
        return Ok("Progression preview loaded.", new Dictionary<string, object>
        {
            { "targetType", targetType },
            { "targetCode", targetCode },
            { "allowed", reasons.Count == 0 },
            { "reasons", reasons.Cast<object>().ToArray() },
            { "denyReason", reasons.Count == 0 ? string.Empty : string.Join("; ", reasons) },
            { "cost", new Dictionary<string, object> { { "xpCoins", cost } } },
            { "result", new Dictionary<string, object> { { "changes", changes.Cast<object>().ToArray() } } }
        });
    }

    public ResponseEnvelope ProgressionSetRace(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var character = ResolveCharacterForClassSkill(context, actor);
        EnsureCharacterDefaults(character);
        var raceCode = RequireLength(PayloadReader.GetString(context.Request.Payload, "raceCode"), 1, 128, "raceCode");
        var race = _repositories.RaceDefinitions.GetByCode(raceCode) ?? throw new KeyNotFoundException("Race definition not found.");
        if (!race.IsActive || race.Status == DefinitionStatus.Archived)
        {
            _logger.Admin($"progression.denied actor={actor.Login} action=setRace character={character.Id} reason=race-inactive code={raceCode}");
            throw new InvalidOperationException("Race is not available.");
        }
        character.RaceCode = race.Code;
        character.Race = race.Name;
        _repositories.Characters.Replace(character);
        _logger.Admin($"progression.apply actor={actor.Login} action=setRace character={character.Id} race={raceCode}");
        return Ok("Race set.", BuildProgressionPayload(character));
    }

    public ResponseEnvelope ProgressionLearnClass(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var character = ResolveCharacterForClassSkill(context, actor);
        EnsureProgressCollections(character);
        EnsureCharacterDefaults(character);
        var classCode = RequireLength(PayloadReader.GetString(context.Request.Payload, "classCode"), 1, 128, "classCode");
        var definition = _repositories.ClassDefinitions.GetByCode(classCode) ?? throw new KeyNotFoundException("Class definition not found.");
        var reasons = EvaluateClassRequirements(character, definition);
        if (reasons.Count > 0)
        {
            _logger.Admin($"progression.denied actor={actor.Login} action=learnClass code={classCode} reason={string.Join(";", reasons)}");
            throw new InvalidOperationException("Class unavailable: " + string.Join(", ", reasons));
        }

        if (character.XpCoins < definition.XpCoinCost)
        {
            _logger.Admin($"progression.denied actor={actor.Login} action=learnClass code={classCode} reason=xp-insufficient have={character.XpCoins} need={definition.XpCoinCost}");
            throw new InvalidOperationException("Insufficient xp coins.");
        }

        character.XpCoins -= definition.XpCoinCost;
        character.CharacterClasses.Add(new CharacterClassState { ClassCode = classCode, Level = Math.Max(1, definition.UnlockLevel), LearnedUtc = DateTime.UtcNow });
        _repositories.Characters.Replace(character);
        _logger.Admin($"progression.apply actor={actor.Login} action=learnClass code={classCode} xpCost={definition.XpCoinCost}");
        return Ok("Class learned.", BuildProgressionPayload(character));
    }

    public ResponseEnvelope ProgressionLearnSkill(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var character = ResolveCharacterForClassSkill(context, actor);
        EnsureProgressCollections(character);
        EnsureCharacterDefaults(character);
        var skillCode = RequireLength(PayloadReader.GetString(context.Request.Payload, "skillCode"), 1, 128, "skillCode");
        var definition = _repositories.DefinitionSkills.GetByCode(skillCode) ?? throw new KeyNotFoundException("Skill definition not found.");
        var reasons = EvaluateSkillRequirements(character, definition);
        if (reasons.Count > 0)
        {
            _logger.Admin($"progression.denied actor={actor.Login} action=learnSkill code={skillCode} reason={string.Join(";", reasons)}");
            throw new InvalidOperationException("Skill unavailable: " + string.Join(", ", reasons));
        }

        if (character.XpCoins < definition.XpCoinCost)
        {
            _logger.Admin($"progression.denied actor={actor.Login} action=learnSkill code={skillCode} reason=xp-insufficient have={character.XpCoins} need={definition.XpCoinCost}");
            throw new InvalidOperationException("Insufficient xp coins.");
        }

        character.XpCoins -= definition.XpCoinCost;
        character.CharacterSkills.Add(new CharacterSkillState { SkillCode = skillCode, Tier = definition.Tier, Level = 1, Acquired = true, LearnedUtc = DateTime.UtcNow });
        _repositories.Characters.Replace(character);
        _logger.Admin($"progression.apply actor={actor.Login} action=learnSkill code={skillCode} xpCost={definition.XpCoinCost}");
        return Ok("Skill learned.", BuildProgressionPayload(character));
    }

    private static void EnsureProgressCollections(Character character)
    {
        character.CharacterClasses ??= new List<CharacterClassState>();
        character.CharacterSkills ??= new List<CharacterSkillState>();
        if (character.XpCoins < 0) character.XpCoins = 0;
    }

    private List<string> EvaluateClassRequirements(Character character, ClassDefinition definition)
    {
        var reasons = new List<string>();
        if (!definition.IsActive || definition.Status == DefinitionStatus.Archived) reasons.Add("Класс неактивен.");
        if (!string.IsNullOrWhiteSpace(definition.ParentClassCode) && character.CharacterClasses.All(x => !string.Equals(x.ClassCode, definition.ParentClassCode, StringComparison.OrdinalIgnoreCase)))
            reasons.Add("Не изучен родительский класс.");
        if (definition.RequiredCharacterLevel > 0 && GetCharacterLevel(character) < definition.RequiredCharacterLevel)
            reasons.Add("Недостаточный уровень персонажа.");
        if (definition.RequiredRaceCodes.Count > 0 && definition.RequiredRaceCodes.All(x => !string.Equals(x, character.RaceCode, StringComparison.OrdinalIgnoreCase)))
            reasons.Add("Раса не соответствует требованиям.");
        foreach (var code in definition.RequiredClassCodes.Where(x => !string.IsNullOrWhiteSpace(x)))
            if (character.CharacterClasses.All(x => !string.Equals(x.ClassCode, code, StringComparison.OrdinalIgnoreCase))) reasons.Add($"Нужен класс {code}.");
        foreach (var code in definition.RequiredSkillCodes.Where(x => !string.IsNullOrWhiteSpace(x)))
            if (character.CharacterSkills.All(x => !string.Equals(x.SkillCode, code, StringComparison.OrdinalIgnoreCase))) reasons.Add($"Нужен навык {code}.");
        if (character.CharacterClasses.Any(x => string.Equals(x.ClassCode, definition.Code, StringComparison.OrdinalIgnoreCase))) reasons.Add("Класс уже изучен.");
        return reasons;
    }

    private List<string> EvaluateSkillRequirements(Character character, SkillDefinition definition)
    {
        var reasons = new List<string>();
        if (!definition.IsActive || definition.Status == DefinitionStatus.Archived) reasons.Add("Навык неактивен.");
        if (definition.RequiredCharacterLevel > 0 && GetCharacterLevel(character) < definition.RequiredCharacterLevel)
            reasons.Add("Недостаточный уровень персонажа.");
        if (definition.RequiredRaceCodes.Count > 0 && definition.RequiredRaceCodes.All(x => !string.Equals(x, character.RaceCode, StringComparison.OrdinalIgnoreCase)))
            reasons.Add("Раса не соответствует требованиям.");
        foreach (var code in definition.RequiredClassCodes.Where(x => !string.IsNullOrWhiteSpace(x)))
            if (character.CharacterClasses.All(x => !string.Equals(x.ClassCode, code, StringComparison.OrdinalIgnoreCase))) reasons.Add($"Нужен класс {code}.");
        foreach (var code in definition.RequiredSkillCodes.Where(x => !string.IsNullOrWhiteSpace(x)))
            if (character.CharacterSkills.All(x => !string.Equals(x.SkillCode, code, StringComparison.OrdinalIgnoreCase))) reasons.Add($"Нужен навык {code}.");
        if (character.CharacterSkills.Any(x => string.Equals(x.SkillCode, definition.Code, StringComparison.OrdinalIgnoreCase))) reasons.Add("Навык уже изучен.");
        return reasons;
    }

    private static int GetCharacterLevel(Character character)
    {
        if (character.CharacterClasses == null || character.CharacterClasses.Count == 0) return 1;
        return Math.Max(1, character.CharacterClasses.Max(x => x.Level));
    }

    private static Dictionary<string, object> BuildProgressionPayload(Character character)
    {
        EnsureProgressCollections(character);
        return new Dictionary<string, object>
        {
            { "characterId", character.Id },
            { "raceCode", character.RaceCode },
            { "xpCoins", character.XpCoins },
            { "classes", character.CharacterClasses.Select(x => new Dictionary<string, object>{{"classCode", x.ClassCode},{"level", x.Level},{"learnedUtc", x.LearnedUtc}}).Cast<object>().ToArray() },
            { "skills", character.CharacterSkills.Select(x => new Dictionary<string, object>{{"skillCode", x.SkillCode},{"tier", x.Tier},{"level", x.Level},{"learnedUtc", x.LearnedUtc}}).Cast<object>().ToArray() }
        };
    }
}
