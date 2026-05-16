using System;
using System.Collections.Generic;
using System.Linq;
using Nri.Shared.Domain;

namespace Nri.Server.Application;

public interface ICharacterSkillProfileFactory
{
    SkillProfile BuildFromLegacyCharacter(Character character);
    SkillProfile BuildEmpty(string characterId, string ruleSetId);
    SkillProfileComparisonResult CompareLegacyToProfile(Character character, SkillProfile profile);
}

public sealed class CharacterSkillProfileFactory : ICharacterSkillProfileFactory
{
    public SkillProfile BuildFromLegacyCharacter(Character character)
    {
        if (character == null) throw new ArgumentNullException(nameof(character));
        var profile = BuildEmpty(character.Id, RuleSetIds.FantasyNriDefault);
        var values = new List<CharacterSkillProfileValue>();

        foreach (var skill in character.CharacterSkills ?? new List<CharacterSkillState>())
        {
            var skillId = (skill.SkillCode ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(skillId)) continue;
            values.Add(new CharacterSkillProfileValue
            {
                SkillId = skillId,
                Rank = skill.Level,
                IsUnlocked = skill.Available || skill.Acquired,
                IsLearned = skill.Acquired,
                Source = "legacy.characterSkills",
                LearnedAtUtc = skill.LearnedUtc,
                Notes = string.Empty
            });
        }

        foreach (var skill in character.CharacterSkillStates ?? new List<CharacterSkillState>())
        {
            var skillId = (skill.SkillCode ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(skillId)) continue;
            if (values.Any(x => string.Equals(x.SkillId, skillId, StringComparison.Ordinal))) continue;
            values.Add(new CharacterSkillProfileValue
            {
                SkillId = skillId,
                Rank = skill.Level,
                IsUnlocked = skill.Available || skill.Acquired,
                IsLearned = skill.Acquired,
                Source = "legacy.characterSkillStates",
                LearnedAtUtc = skill.LearnedUtc,
                Notes = string.Empty
            });
        }

        profile.Skills = values;
        return profile;
    }

    public SkillProfile BuildEmpty(string characterId, string ruleSetId)
    {
        return new SkillProfile
        {
            CharacterId = characterId ?? string.Empty,
            RuleSetId = string.IsNullOrWhiteSpace(ruleSetId) ? RuleSetIds.FantasyNriDefault : ruleSetId,
            Skills = new List<CharacterSkillProfileValue>(),
            SchemaVersion = 1
        };
    }

    public SkillProfileComparisonResult CompareLegacyToProfile(Character character, SkillProfile profile)
    {
        var expected = BuildFromLegacyCharacter(character);
        var differences = new List<string>();

        var rawSkills = (character.CharacterSkills ?? new List<CharacterSkillState>())
            .Concat(character.CharacterSkillStates ?? new List<CharacterSkillState>())
            .ToList();
        if (rawSkills.Any(x => string.IsNullOrWhiteSpace((x.SkillCode ?? string.Empty).Trim())))
        {
            differences.Add("warning:empty-skillCode");
        }

        var expectedMap = expected.Skills.ToDictionary(x => x.SkillId, x => x, StringComparer.Ordinal);
        var actualMap = (profile?.Skills ?? new List<CharacterSkillProfileValue>())
            .Where(x => !string.IsNullOrWhiteSpace(x.SkillId))
            .GroupBy(x => x.SkillId.Trim(), StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.Last(), StringComparer.Ordinal);

        foreach (var kv in expectedMap)
        {
            if (!actualMap.TryGetValue(kv.Key, out var actual))
            {
                differences.Add($"missing:{kv.Key}");
                continue;
            }

            if (actual.Rank != kv.Value.Rank) differences.Add($"rank:{kv.Key}");
            if (actual.IsUnlocked != kv.Value.IsUnlocked) differences.Add($"isUnlocked:{kv.Key}");
            if (actual.IsLearned != kv.Value.IsLearned) differences.Add($"isLearned:{kv.Key}");
        }

        foreach (var actual in actualMap.Values)
        {
            if (expectedMap.ContainsKey(actual.SkillId)) continue;
            if (!string.Equals(actual.Source, "legacy_shadow", StringComparison.OrdinalIgnoreCase))
            {
                differences.Add($"extra:{actual.SkillId}");
            }
        }

        return new SkillProfileComparisonResult
        {
            CharacterId = character?.Id ?? string.Empty,
            IsEquivalent = differences.Count == 0,
            Differences = differences,
            ComparedAtUtc = DateTime.UtcNow
        };
    }
}

public sealed class SkillProfileComparisonResult
{
    public string CharacterId { get; set; } = string.Empty;
    public bool IsEquivalent { get; set; }
    public List<string> Differences { get; set; } = new List<string>();
    public DateTime ComparedAtUtc { get; set; }
}
