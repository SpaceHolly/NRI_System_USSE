using System;
using System.Collections.Generic;
using System.Linq;
using Nri.Shared.Domain;

namespace Nri.Server.Application;

public interface ICharacterHoldingsProfileFactory
{
    HoldingsProfile BuildFromLegacyCharacter(Character character);
    HoldingsProfile BuildEmpty(string characterId, string ruleSetId);
    HoldingsProfileComparisonResult CompareLegacyToProfile(Character character, HoldingsProfile profile);
}

public sealed class CharacterHoldingsProfileFactory : ICharacterHoldingsProfileFactory
{
    public HoldingsProfile BuildFromLegacyCharacter(Character character)
    {
        if (character == null) throw new ArgumentNullException(nameof(character));
        var profile = BuildEmpty(character.Id, RuleSetIds.FantasyNriDefault);
        profile.Holdings = (character.Holdings ?? new List<HoldingRef>()).Select(Map).ToList();
        return profile;
    }

    public HoldingsProfile BuildEmpty(string characterId, string ruleSetId) => new HoldingsProfile
    {
        CharacterId = characterId ?? string.Empty,
        RuleSetId = string.IsNullOrWhiteSpace(ruleSetId) ? RuleSetIds.FantasyNriDefault : ruleSetId,
        Holdings = new List<CharacterHoldingProfileValue>(),
        SchemaVersion = 1
    };

    public HoldingsProfileComparisonResult CompareLegacyToProfile(Character character, HoldingsProfile profile)
    {
        var expected = BuildFromLegacyCharacter(character);
        var diffs = new List<string>();
        if ((character.Holdings ?? new List<HoldingRef>()).Any(x => string.IsNullOrWhiteSpace(x.Id))) diffs.Add("warning:empty-holdingId");
        var expectedMap = expected.Holdings.ToDictionary(x => x.HoldingId, x => x, StringComparer.Ordinal);
        var actualMap = (profile?.Holdings ?? new List<CharacterHoldingProfileValue>()).GroupBy(x => x.HoldingId, StringComparer.Ordinal).ToDictionary(x => x.Key, x => x.Last(), StringComparer.Ordinal);
        foreach (var kv in expectedMap)
        {
            if (!actualMap.TryGetValue(kv.Key, out var a)) { diffs.Add($"missing:{kv.Key}"); continue; }
            if (!string.Equals(a.Name, kv.Value.Name, StringComparison.Ordinal)) diffs.Add($"name:{kv.Key}");
            if (!string.Equals(a.HoldingType, kv.Value.HoldingType, StringComparison.Ordinal)) diffs.Add($"type:{kv.Key}");
        }
        return new HoldingsProfileComparisonResult { CharacterId = character?.Id ?? string.Empty, IsEquivalent = diffs.Count == 0, Differences = diffs, ComparedAtUtc = DateTime.UtcNow };
    }

    private static CharacterHoldingProfileValue Map(HoldingRef x) => new CharacterHoldingProfileValue
    {
        HoldingId = x.Id ?? string.Empty,
        Name = x.Name ?? string.Empty,
        HoldingType = x.Type ?? string.Empty,
        Description = x.Description ?? string.Empty,
        OwnerCharacterIds = x.Owners?.ToList() ?? new List<string>(),
        Notes = x.Notes ?? string.Empty,
        Tags = x.Archived ? new List<string> { "archived" } : new List<string>(),
        Source = "legacy.holdings"
    };
}

public sealed class HoldingsProfileComparisonResult
{
    public string CharacterId { get; set; } = string.Empty;
    public bool IsEquivalent { get; set; }
    public List<string> Differences { get; set; } = new List<string>();
    public DateTime ComparedAtUtc { get; set; }
}
