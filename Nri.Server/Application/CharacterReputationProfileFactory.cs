using System;
using System.Collections.Generic;
using System.Linq;
using Nri.Shared.Domain;

namespace Nri.Server.Application;

public interface ICharacterReputationProfileFactory
{
    ReputationProfile BuildFromLegacyCharacter(Character character);
    ReputationProfile BuildEmpty(string characterId, string ruleSetId);
    ReputationProfileComparisonResult CompareLegacyToProfile(Character character, ReputationProfile profile);
}

public sealed class CharacterReputationProfileFactory : ICharacterReputationProfileFactory
{
    public ReputationProfile BuildFromLegacyCharacter(Character character)
    {
        if (character == null) throw new ArgumentNullException(nameof(character));
        var profile = BuildEmpty(character.Id, RuleSetIds.FantasyNriDefault);
        profile.Entries = (character.Reputation ?? new List<ReputationRef>()).Select(Map).ToList();
        return profile;
    }

    public ReputationProfile BuildEmpty(string characterId, string ruleSetId) => new ReputationProfile
    {
        CharacterId = characterId ?? string.Empty,
        RuleSetId = string.IsNullOrWhiteSpace(ruleSetId) ? RuleSetIds.FantasyNriDefault : ruleSetId,
        Entries = new List<CharacterReputationProfileValue>(),
        SchemaVersion = 1
    };

    public ReputationProfileComparisonResult CompareLegacyToProfile(Character character, ReputationProfile profile)
    {
        var expected = BuildFromLegacyCharacter(character);
        var diffs = new List<string>();
        if ((character.Reputation ?? new List<ReputationRef>()).Any(x => string.IsNullOrWhiteSpace(x.Id))) diffs.Add("warning:empty-targetId");
        var expectedMap = expected.Entries.ToDictionary(GetKey, x => x, StringComparer.Ordinal);
        var actualMap = (profile?.Entries ?? new List<CharacterReputationProfileValue>()).GroupBy(GetKey, StringComparer.Ordinal).ToDictionary(x => x.Key, x => x.Last(), StringComparer.Ordinal);
        foreach (var kv in expectedMap)
        {
            if (!actualMap.TryGetValue(kv.Key, out var act)) { diffs.Add($"missing:{kv.Key}"); continue; }
            if (act.Value != kv.Value.Value) diffs.Add($"value:{kv.Key}");
            if (act.GroupValue != kv.Value.GroupValue) diffs.Add($"groupValue:{kv.Key}");
        }
        return new ReputationProfileComparisonResult { CharacterId = character?.Id ?? string.Empty, IsEquivalent = diffs.Count == 0, Differences = diffs, ComparedAtUtc = DateTime.UtcNow };
    }

    private static CharacterReputationProfileValue Map(ReputationRef item) => new CharacterReputationProfileValue
    {
        TargetType = item.TargetType.ToString(),
        TargetId = item.Id ?? string.Empty,
        Name = item.TargetName ?? string.Empty,
        Value = item.ScopeType == ReputationScopeType.Character ? item.Value : 0,
        GroupValue = item.ScopeType == ReputationScopeType.Group ? item.Value : 0,
        Notes = item.Notes ?? string.Empty,
        Tags = BuildTags(item),
        Source = "legacy.reputation"
    };

    private static List<string> BuildTags(ReputationRef item)
    {
        var tags = new List<string>();
        if (!string.IsNullOrWhiteSpace(item.GroupKey)) tags.Add($"group:{item.GroupKey}");
        if (item.Archived) tags.Add("archived");
        if (item.IsHiddenForOthers) tags.Add("hidden");
        return tags;
    }

    private static string GetKey(CharacterReputationProfileValue x) => string.IsNullOrWhiteSpace(x.TargetId) ? $"{x.TargetType}:{x.Name}" : x.TargetId;
}

public sealed class ReputationProfileComparisonResult
{
    public string CharacterId { get; set; } = string.Empty;
    public bool IsEquivalent { get; set; }
    public List<string> Differences { get; set; } = new List<string>();
    public DateTime ComparedAtUtc { get; set; }
}
