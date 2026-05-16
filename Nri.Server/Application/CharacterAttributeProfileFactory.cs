using System;
using System.Collections.Generic;
using System.Linq;
using Nri.Shared.Domain;

namespace Nri.Server.Application;

public interface ICharacterAttributeProfileFactory
{
    AttributeProfile BuildFromLegacyCharacter(Character character);
    AttributeProfile BuildEmpty(string characterId, string ruleSetId);
    AttributeProfileComparisonResult CompareLegacyToProfile(Character character, AttributeProfile profile);
}

public sealed class CharacterAttributeProfileFactory : ICharacterAttributeProfileFactory
{
    public AttributeProfile BuildFromLegacyCharacter(Character character)
    {
        if (character == null) throw new ArgumentNullException(nameof(character));
        var ruleSetId = RuleSetIds.FantasyNriDefault;
        var profile = BuildEmpty(character.Id, ruleSetId);
        profile.Values = new List<CharacterAttributeValue>
        {
            BuildValue(CharacterAttributeIds.Health, character.Stats.Health),
            BuildValue(CharacterAttributeIds.PhysicalArmor, character.Stats.PhysicalArmor),
            BuildValue(CharacterAttributeIds.MagicArmor, character.Stats.MagicalArmor),
            BuildValue(CharacterAttributeIds.Morale, character.Stats.Morale),
            BuildValue(CharacterAttributeIds.Strength, character.Stats.Strength),
            BuildValue(CharacterAttributeIds.Dexterity, character.Stats.Dexterity),
            BuildValue(CharacterAttributeIds.Endurance, character.Stats.Endurance),
            BuildValue(CharacterAttributeIds.Intellect, character.Stats.Intellect),
            BuildValue(CharacterAttributeIds.Wisdom, character.Stats.Wisdom),
            BuildValue(CharacterAttributeIds.Charisma, character.Stats.Charisma)
        };

        return profile;
    }

    public AttributeProfile BuildEmpty(string characterId, string ruleSetId)
    {
        return new AttributeProfile
        {
            CharacterId = characterId ?? string.Empty,
            RuleSetId = string.IsNullOrWhiteSpace(ruleSetId) ? RuleSetIds.FantasyNriDefault : ruleSetId,
            Values = new List<CharacterAttributeValue>(),
            SchemaVersion = 1
        };
    }

    public AttributeProfileComparisonResult CompareLegacyToProfile(Character character, AttributeProfile profile)
    {
        var shadow = BuildFromLegacyCharacter(character);
        var differences = new List<string>();
        var map = (profile?.Values ?? new List<CharacterAttributeValue>())
            .GroupBy(x => x.AttributeId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Last(), StringComparer.OrdinalIgnoreCase);

        foreach (var expected in shadow.Values)
        {
            if (!map.TryGetValue(expected.AttributeId, out var actual))
            {
                differences.Add($"missing:{expected.AttributeId}");
                continue;
            }

            if (actual.BaseValue != expected.BaseValue) differences.Add($"base:{expected.AttributeId}:{actual.BaseValue}!={expected.BaseValue}");
            if (actual.CurrentValue != expected.CurrentValue) differences.Add($"current:{expected.AttributeId}:{actual.CurrentValue}!={expected.CurrentValue}");
            if (actual.ManualModifier != expected.ManualModifier) differences.Add($"modifier:{expected.AttributeId}:{actual.ManualModifier}!={expected.ManualModifier}");
        }

        return new AttributeProfileComparisonResult
        {
            CharacterId = character?.Id ?? string.Empty,
            IsEquivalent = differences.Count == 0,
            Differences = differences,
            ComparedAtUtc = DateTime.UtcNow
        };
    }

    private static CharacterAttributeValue BuildValue(string attributeId, int baseValue)
    {
        return new CharacterAttributeValue
        {
            AttributeId = attributeId,
            BaseValue = baseValue,
            CurrentValue = baseValue,
            ManualModifier = 0,
            Source = "legacy_shadow",
            Notes = string.Empty
        };
    }
}

public sealed class AttributeProfileComparisonResult
{
    public string CharacterId { get; set; } = string.Empty;
    public bool IsEquivalent { get; set; }
    public List<string> Differences { get; set; } = new List<string>();
    public DateTime ComparedAtUtc { get; set; }
}
