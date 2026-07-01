using System;
using System.Collections.Generic;
using Nri.Shared.Domain;

namespace Nri.Server.Application;

public interface IRaceOrSpeciesProfileShadowBuilder
{
    RaceOrSpeciesProfile BuildFromLegacyCharacter(Character character);
    RaceOrSpeciesProfile BuildEmpty(string characterId, string ruleSetId);
    RaceOrSpeciesProfileComparisonResult CompareLegacyToProfile(Character character, RaceOrSpeciesProfile profile);
}

public sealed class RaceOrSpeciesProfileShadowBuilder : IRaceOrSpeciesProfileShadowBuilder
{
    public RaceOrSpeciesProfile BuildFromLegacyCharacter(Character character)
    {
        if (character == null) throw new ArgumentNullException(nameof(character));

        var profile = BuildEmpty(character.Id, RuleSetIds.FantasyNriDefault);
        profile.RaceCode = character.RaceCode ?? string.Empty;
        profile.RaceName = character.Race ?? string.Empty;
        profile.DisplayName = FirstNonEmpty(character.Race ?? string.Empty, character.RaceCode ?? string.Empty);
        profile.Source = "legacy_shadow";
        return profile;
    }

    public RaceOrSpeciesProfile BuildEmpty(string characterId, string ruleSetId)
    {
        return new RaceOrSpeciesProfile
        {
            CharacterId = characterId ?? string.Empty,
            RuleSetId = string.IsNullOrWhiteSpace(ruleSetId) ? RuleSetIds.FantasyNriDefault : ruleSetId,
            Source = "legacy_shadow",
            Tags = new List<string>(),
            SchemaVersion = 1
        };
    }

    public RaceOrSpeciesProfileComparisonResult CompareLegacyToProfile(Character character, RaceOrSpeciesProfile profile)
    {
        var expected = character == null
            ? BuildEmpty(string.Empty, RuleSetIds.FantasyNriDefault)
            : BuildFromLegacyCharacter(character);
        var actual = profile ?? BuildEmpty(character?.Id ?? string.Empty, RuleSetIds.FantasyNriDefault);
        var diffs = new List<string>();

        if (!TextEquals(expected.RaceCode, actual.RaceCode))
            diffs.Add("raceCode.differs");

        if (!string.IsNullOrWhiteSpace(expected.RaceName) &&
            !TextEquals(expected.RaceName, actual.RaceName) &&
            !TextEquals(expected.RaceName, actual.DisplayName))
            diffs.Add("raceName.missing_or_differs");

        if (!string.IsNullOrWhiteSpace(expected.DisplayName) &&
            string.IsNullOrWhiteSpace(actual.DisplayName) &&
            string.IsNullOrWhiteSpace(actual.RaceName))
            diffs.Add("displayName.missing");

        if (!string.IsNullOrWhiteSpace(actual.SubspeciesId))
            diffs.Add("subspecies.present_without_legacy_source");
        if (!string.IsNullOrWhiteSpace(actual.HybridId) || !string.IsNullOrWhiteSpace(actual.HybridSubtypeId))
            diffs.Add("hybrid.present_without_legacy_source");

        return new RaceOrSpeciesProfileComparisonResult
        {
            CharacterId = character?.Id ?? actual.CharacterId,
            IsEquivalent = diffs.Count == 0,
            Differences = diffs,
            ComparedAtUtc = DateTime.UtcNow
        };
    }

    private static string FirstNonEmpty(params string[] values)
    {
        foreach (var value in values)
            if (!string.IsNullOrWhiteSpace(value)) return value;
        return string.Empty;
    }

    private static bool TextEquals(string left, string right)
    {
        return string.Equals(left ?? string.Empty, right ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class RaceOrSpeciesProfileComparisonResult
{
    public string CharacterId { get; set; } = string.Empty;
    public bool IsEquivalent { get; set; }
    public List<string> Differences { get; set; } = new List<string>();
    public DateTime ComparedAtUtc { get; set; } = DateTime.UtcNow;
}
