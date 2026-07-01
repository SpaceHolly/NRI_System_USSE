using System;
using System.Collections.Generic;
using System.Globalization;
using Nri.Shared.Domain;

namespace Nri.Server.Application;

public interface IBodyProfileShadowBuilder
{
    BodyProfile BuildFromLegacyCharacter(Character character);
    BodyProfile BuildEmpty(string characterId, string ruleSetId);
    BodyProfileComparisonResult CompareLegacyToProfile(Character character, BodyProfile profile);
}

public sealed class BodyProfileShadowBuilder : IBodyProfileShadowBuilder
{
    public BodyProfile BuildFromLegacyCharacter(Character character)
    {
        if (character == null) throw new ArgumentNullException(nameof(character));

        var profile = BuildEmpty(character.Id, RuleSetIds.FantasyNriDefault);
        profile.Description = character.Description ?? string.Empty;
        profile.Backstory = character.Backstory ?? string.Empty;
        profile.HeightText = character.Height ?? string.Empty;
        profile.HeightCm = ParsePlainCentimeters(character.Height ?? string.Empty);
        profile.AgeYears = character.Age ?? 0;
        profile.AgeText = character.Age.HasValue ? character.Age.Value.ToString(CultureInfo.InvariantCulture) : string.Empty;
        profile.Source = "legacy_shadow";
        return profile;
    }

    public BodyProfile BuildEmpty(string characterId, string ruleSetId)
    {
        return new BodyProfile
        {
            CharacterId = characterId ?? string.Empty,
            RuleSetId = string.IsNullOrWhiteSpace(ruleSetId) ? RuleSetIds.FantasyNriDefault : ruleSetId,
            Source = "legacy_shadow",
            BodyTags = new List<string>(),
            EquipmentCompatibilityTags = new List<string>(),
            BodyStats = new Dictionary<string, int>(),
            SchemaVersion = 1
        };
    }

    public BodyProfileComparisonResult CompareLegacyToProfile(Character character, BodyProfile profile)
    {
        var expected = character == null
            ? BuildEmpty(string.Empty, RuleSetIds.FantasyNriDefault)
            : BuildFromLegacyCharacter(character);
        var actual = profile ?? BuildEmpty(character?.Id ?? string.Empty, RuleSetIds.FantasyNriDefault);
        var diffs = new List<string>();

        if (!string.IsNullOrWhiteSpace(expected.HeightText) &&
            !string.Equals(expected.HeightText, actual.HeightText ?? string.Empty, StringComparison.OrdinalIgnoreCase))
            diffs.Add("heightText.differs_or_missing");

        if (expected.HeightCm > 0 && expected.HeightCm != actual.HeightCm)
            diffs.Add("heightCm.differs");

        if (expected.AgeYears > 0 && expected.AgeYears != actual.AgeYears)
            diffs.Add("ageYears.differs");

        if (!string.IsNullOrWhiteSpace(expected.AgeText) &&
            !string.Equals(expected.AgeText, actual.AgeText ?? string.Empty, StringComparison.OrdinalIgnoreCase))
            diffs.Add("ageText.differs_or_missing");

        if (!string.IsNullOrWhiteSpace(expected.Description) &&
            !string.Equals(expected.Description, actual.Description ?? string.Empty, StringComparison.Ordinal))
            diffs.Add("description.differs_or_missing");

        if (!string.IsNullOrWhiteSpace(expected.Backstory) &&
            !string.Equals(expected.Backstory, actual.Backstory ?? string.Empty, StringComparison.Ordinal))
            diffs.Add("backstory.differs_or_missing");

        return new BodyProfileComparisonResult
        {
            CharacterId = character?.Id ?? actual.CharacterId,
            IsEquivalent = diffs.Count == 0,
            Differences = diffs,
            ComparedAtUtc = DateTime.UtcNow
        };
    }

    private static int ParsePlainCentimeters(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return 0;
        var trimmed = value.Trim();
        foreach (var ch in trimmed)
        {
            if (!char.IsDigit(ch)) return 0;
        }

        return int.TryParse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
    }
}

public sealed class BodyProfileComparisonResult
{
    public string CharacterId { get; set; } = string.Empty;
    public bool IsEquivalent { get; set; }
    public List<string> Differences { get; set; } = new List<string>();
    public DateTime ComparedAtUtc { get; set; } = DateTime.UtcNow;
}
