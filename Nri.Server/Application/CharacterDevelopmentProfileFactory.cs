using System;
using System.Collections.Generic;
using System.Linq;
using Nri.Shared.Domain;

namespace Nri.Server.Application;

public interface ICharacterDevelopmentProfileFactory
{
    DevelopmentProfile BuildFromLegacyCharacter(Character character);
    DevelopmentProfile BuildEmpty(string characterId, string ruleSetId);
    DevelopmentProfileComparisonResult CompareLegacyToProfile(Character character, DevelopmentProfile profile);
}

public sealed class CharacterDevelopmentProfileFactory : ICharacterDevelopmentProfileFactory
{
    public DevelopmentProfile BuildFromLegacyCharacter(Character character)
    {
        if (character == null) throw new ArgumentNullException(nameof(character));
        var profile = BuildEmpty(character.Id, RuleSetIds.FantasyNriDefault);
        var nodes = new List<CharacterDevelopmentNodeState>();

        foreach (var cls in character.CharacterClasses ?? new List<CharacterClassState>())
        {
            var nodeId = (cls.ClassCode ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(nodeId)) continue;
            nodes.Add(new CharacterDevelopmentNodeState
            {
                DevelopmentNodeId = nodeId,
                NodeType = DevelopmentNodeTypes.Class,
                CurrentTier = cls.Level,
                MaxTier = cls.Level,
                IsUnlocked = true,
                IsPurchased = true,
                IsHidden = false,
                Source = "legacy.characterClasses",
                PurchasedAtUtc = cls.LearnedUtc
            });
        }

        foreach (var p in character.ClassProgress ?? new List<CharacterClassProgress>())
        {
            var nodeId = (p.ClassCode ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(nodeId)) continue;
            var existing = nodes.FirstOrDefault(x => string.Equals(x.DevelopmentNodeId, nodeId, StringComparison.Ordinal));
            if (existing == null)
            {
                nodes.Add(new CharacterDevelopmentNodeState
                {
                    DevelopmentNodeId = nodeId,
                    NodeType = DevelopmentNodeTypes.Class,
                    CurrentTier = p.Level,
                    MaxTier = p.Level,
                    IsUnlocked = p.Level > 0 || p.Experience > 0,
                    IsPurchased = p.Level > 0,
                    IsHidden = false,
                    Source = "legacy.classProgress",
                    PurchasedAtUtc = DateTime.UtcNow,
                    Notes = p.Experience > 0 ? $"experience={p.Experience}" : string.Empty
                });
                continue;
            }

            if (p.Level > existing.CurrentTier) existing.CurrentTier = p.Level;
            if (p.Level > existing.MaxTier) existing.MaxTier = p.Level;
        }

        profile.Nodes = nodes;
        return profile;
    }

    public DevelopmentProfile BuildEmpty(string characterId, string ruleSetId)
    {
        return new DevelopmentProfile
        {
            CharacterId = characterId ?? string.Empty,
            RuleSetId = string.IsNullOrWhiteSpace(ruleSetId) ? RuleSetIds.FantasyNriDefault : ruleSetId,
            ActiveHexagonIds = new List<string>(),
            Nodes = new List<CharacterDevelopmentNodeState>(),
            Vocation = string.Empty,
            SchemaVersion = 1
        };
    }

    public DevelopmentProfileComparisonResult CompareLegacyToProfile(Character character, DevelopmentProfile profile)
    {
        var expected = BuildFromLegacyCharacter(character);
        var differences = new List<string>();

        if ((character.CharacterClasses ?? new List<CharacterClassState>()).Any(x => string.IsNullOrWhiteSpace((x.ClassCode ?? string.Empty).Trim())) ||
            (character.ClassProgress ?? new List<CharacterClassProgress>()).Any(x => string.IsNullOrWhiteSpace((x.ClassCode ?? string.Empty).Trim())))
        {
            differences.Add("warning:empty-classCode");
        }

        var expectedMap = expected.Nodes.ToDictionary(x => x.DevelopmentNodeId, x => x, StringComparer.Ordinal);
        var actualMap = (profile?.Nodes ?? new List<CharacterDevelopmentNodeState>())
            .Where(x => !string.IsNullOrWhiteSpace(x.DevelopmentNodeId))
            .GroupBy(x => x.DevelopmentNodeId.Trim(), StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.Last(), StringComparer.Ordinal);

        foreach (var kv in expectedMap)
        {
            if (!actualMap.TryGetValue(kv.Key, out var actual))
            {
                differences.Add($"missing:{kv.Key}");
                continue;
            }

            if (actual.CurrentTier != kv.Value.CurrentTier) differences.Add($"currentTier:{kv.Key}");
            if (actual.MaxTier != kv.Value.MaxTier) differences.Add($"maxTier:{kv.Key}");
            if (actual.IsUnlocked != kv.Value.IsUnlocked) differences.Add($"isUnlocked:{kv.Key}");
            if (actual.IsPurchased != kv.Value.IsPurchased) differences.Add($"isPurchased:{kv.Key}");
            if (actual.IsHidden != kv.Value.IsHidden) differences.Add($"isHidden:{kv.Key}");
        }

        foreach (var actual in actualMap.Values)
        {
            if (expectedMap.ContainsKey(actual.DevelopmentNodeId)) continue;
            if (!string.Equals(actual.Source, "legacy_shadow", StringComparison.OrdinalIgnoreCase))
            {
                differences.Add($"extra:{actual.DevelopmentNodeId}");
            }
        }

        return new DevelopmentProfileComparisonResult
        {
            CharacterId = character?.Id ?? string.Empty,
            IsEquivalent = differences.Count == 0,
            Differences = differences,
            ComparedAtUtc = DateTime.UtcNow
        };
    }
}

public sealed class DevelopmentProfileComparisonResult
{
    public string CharacterId { get; set; } = string.Empty;
    public bool IsEquivalent { get; set; }
    public List<string> Differences { get; set; } = new List<string>();
    public DateTime ComparedAtUtc { get; set; }
}
