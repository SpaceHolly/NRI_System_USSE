using System;
using System.Collections.Generic;
using System.Linq;
using Nri.Shared.Domain;

namespace Nri.Server.Application;

public interface ICharacterInventoryProfileFactory
{
    InventoryProfile BuildFromLegacyCharacter(Character character);
    InventoryProfile BuildEmpty(string characterId, string ruleSetId);
    InventoryProfileComparisonResult CompareLegacyToProfile(Character character, InventoryProfile profile);
}

public sealed class CharacterInventoryProfileFactory : ICharacterInventoryProfileFactory
{
    public InventoryProfile BuildFromLegacyCharacter(Character character)
    {
        if (character == null) throw new ArgumentNullException(nameof(character));
        var profile = BuildEmpty(character.Id, RuleSetIds.FantasyNriDefault);
        profile.Items = (character.Inventory ?? new List<InventoryItem>())
            .Select((item, index) => BuildItem(item, index))
            .ToList();
        return profile;
    }

    public InventoryProfile BuildEmpty(string characterId, string ruleSetId)
    {
        return new InventoryProfile
        {
            CharacterId = characterId ?? string.Empty,
            RuleSetId = string.IsNullOrWhiteSpace(ruleSetId) ? RuleSetIds.FantasyNriDefault : ruleSetId,
            Items = new List<CharacterInventoryItemProfileValue>(),
            SchemaVersion = 1
        };
    }

    public InventoryProfileComparisonResult CompareLegacyToProfile(Character character, InventoryProfile profile)
    {
        var expected = BuildFromLegacyCharacter(character);
        var differences = new List<string>();

        if ((character.Inventory ?? new List<InventoryItem>()).Any(x => string.IsNullOrWhiteSpace(x.Id)))
        {
            differences.Add("warning:empty-itemId");
        }

        var expectedMap = expected.Items.ToDictionary(GetCompareKey, x => x, StringComparer.Ordinal);
        var actualMap = (profile?.Items ?? new List<CharacterInventoryItemProfileValue>())
            .GroupBy(GetCompareKey, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.Last(), StringComparer.Ordinal);

        foreach (var kv in expectedMap)
        {
            if (!actualMap.TryGetValue(kv.Key, out var actual))
            {
                differences.Add($"missing:{kv.Key}");
                continue;
            }

            if (actual.Quantity != kv.Value.Quantity) differences.Add($"quantity:{kv.Key}");
            if (actual.Durability != kv.Value.Durability) differences.Add($"durability:{kv.Key}");
            if (actual.IsEquipped != kv.Value.IsEquipped) differences.Add($"isEquipped:{kv.Key}");
        }

        foreach (var actual in actualMap.Values)
        {
            var key = GetCompareKey(actual);
            if (expectedMap.ContainsKey(key)) continue;
            if (!string.Equals(actual.Source, "legacy_shadow", StringComparison.OrdinalIgnoreCase))
            {
                differences.Add($"extra:{key}");
            }
        }

        return new InventoryProfileComparisonResult
        {
            CharacterId = character?.Id ?? string.Empty,
            IsEquivalent = differences.Count == 0,
            Differences = differences,
            ComparedAtUtc = DateTime.UtcNow
        };
    }

    private static CharacterInventoryItemProfileValue BuildItem(InventoryItem item, int index)
    {
        var hasId = !string.IsNullOrWhiteSpace(item.Id);
        var shadowId = hasId ? item.Id : $"shadow:{index}";
        var durability = item.Durability ?? item.DurabilityOrHealth ?? 0;
        return new CharacterInventoryItemProfileValue
        {
            ItemId = shadowId,
            DefinitionId = item.ItemCode ?? string.Empty,
            Name = string.IsNullOrWhiteSpace(item.Name) ? item.Label : item.Name,
            Quantity = item.Quantity,
            Durability = durability,
            MaxDurability = durability,
            IsEquipped = item.IsEquipped || item.Equipped,
            SlotId = string.Empty,
            Source = hasId ? "legacy.inventory" : "legacy_shadow",
            Notes = hasId ? string.Empty : "missing legacy item id",
            Tags = BuildTags(item)
        };
    }

    private static List<string> BuildTags(InventoryItem item)
    {
        var tags = new List<string>();
        if (item.UsesAmmoOrConsumable) tags.Add("consumable");
        if ((item.ConsumptionPerUse ?? 0) > 0) tags.Add("ammo");
        if (item.IsEquipped || item.Equipped) tags.Add("equipped");
        return tags;
    }

    private static string GetCompareKey(CharacterInventoryItemProfileValue item)
    {
        if (!string.IsNullOrWhiteSpace(item.ItemId)) return item.ItemId.Trim();
        if (!string.IsNullOrWhiteSpace(item.DefinitionId)) return $"def:{item.DefinitionId.Trim()}";
        return $"name:{(item.Name ?? string.Empty).Trim()}";
    }
}

public sealed class InventoryProfileComparisonResult
{
    public string CharacterId { get; set; } = string.Empty;
    public bool IsEquivalent { get; set; }
    public List<string> Differences { get; set; } = new List<string>();
    public DateTime ComparedAtUtc { get; set; }
}
