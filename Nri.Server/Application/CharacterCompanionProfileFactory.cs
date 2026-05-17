using System;
using System.Collections.Generic;
using System.Linq;
using Nri.Shared.Domain;

namespace Nri.Server.Application;

public interface ICharacterCompanionProfileFactory
{
    CompanionProfile BuildFromLegacyCharacter(Character character);
    CompanionProfile BuildEmpty(string characterId, string ruleSetId);
    CompanionProfileComparisonResult CompareLegacyToProfile(Character character, CompanionProfile profile);
}

public sealed class CharacterCompanionProfileFactory : ICharacterCompanionProfileFactory
{
    public CompanionProfile BuildFromLegacyCharacter(Character character)
    {
        if (character == null) throw new ArgumentNullException(nameof(character));
        var profile = BuildEmpty(character.Id, RuleSetIds.FantasyNriDefault);
        profile.Companions = (character.Companions ?? new List<Companion>()).Select(Map).ToList();
        return profile;
    }

    public CompanionProfile BuildEmpty(string characterId, string ruleSetId) => new CompanionProfile
    {
        CharacterId = characterId ?? string.Empty,
        RuleSetId = string.IsNullOrWhiteSpace(ruleSetId) ? RuleSetIds.FantasyNriDefault : ruleSetId,
        Companions = new List<CharacterCompanionProfileValue>(),
        SchemaVersion = 1
    };

    public CompanionProfileComparisonResult CompareLegacyToProfile(Character character, CompanionProfile profile)
    {
        var expected = BuildFromLegacyCharacter(character);
        var diffs = new List<string>();
        if ((character.Companions ?? new List<Companion>()).Any(x => string.IsNullOrWhiteSpace(x.Id))) diffs.Add("warning:empty-companionId");
        var em = expected.Companions.ToDictionary(x => x.CompanionId, x => x, StringComparer.Ordinal);
        var am = (profile?.Companions ?? new List<CharacterCompanionProfileValue>()).GroupBy(x => x.CompanionId, StringComparer.Ordinal).ToDictionary(x => x.Key, x => x.Last(), StringComparer.Ordinal);
        foreach (var kv in em)
        {
            if (!am.TryGetValue(kv.Key, out var a)) { diffs.Add($"missing:{kv.Key}"); continue; }
            if (!string.Equals(a.OwnerCharacterId, kv.Value.OwnerCharacterId, StringComparison.Ordinal)) diffs.Add($"owner:{kv.Key}");
            if (a.HasSeparateInventory != kv.Value.HasSeparateInventory) diffs.Add($"inventoryFlag:{kv.Key}");
        }
        return new CompanionProfileComparisonResult { CharacterId = character?.Id ?? string.Empty, IsEquivalent = diffs.Count == 0, Differences = diffs, ComparedAtUtc = DateTime.UtcNow };
    }

    private static CharacterCompanionProfileValue Map(Companion x) => new CharacterCompanionProfileValue
    {
        CompanionId = x.Id ?? string.Empty,
        Name = x.Name ?? string.Empty,
        Description = x.Description ?? string.Empty,
        RaceOrSpeciesId = x.Species ?? string.Empty,
        OwnerCharacterId = x.OwnerCharacterId ?? string.Empty,
        InitiativeMode = string.Empty,
        HasSeparateInventory = (x.Inventory?.Count ?? 0) > 0,
        Notes = x.Notes ?? string.Empty,
        Tags = x.IsArchived ? new List<string> { "archived" } : new List<string>(),
        Source = "legacy.companions"
    };
}

public sealed class CompanionProfileComparisonResult
{
    public string CharacterId { get; set; } = string.Empty;
    public bool IsEquivalent { get; set; }
    public List<string> Differences { get; set; } = new List<string>();
    public DateTime ComparedAtUtc { get; set; }
}
