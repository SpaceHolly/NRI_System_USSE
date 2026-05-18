using System;
using System.Collections.Generic;
using System.Linq;
using Nri.Shared.Domain;

namespace Nri.Server.Application;

// Transitional bridge for migration from legacy Character payloads to profile-first model.
// Legacy Character remains source of truth until ProfileFeatureFlags.UseProfileFirstCharacterDetails is enabled.
public interface ILegacyCharacterViewAdapter
{
    LegacyCharacterDetailsView BuildFromLegacyCharacter(Character character);
    LegacyCharacterDetailsView BuildFromProfileBundle(Character legacyCharacter, CharacterProfileBundle profileBundle);
    LegacyCharacterViewComparison CompareLegacyAndProfileView(Character character, CharacterProfileBundle profileBundle);
    CharacterShadowCompareResult CompareLegacyAndProfileShadow(Character character);
}

public class LegacyCharacterDetailsView
{
    public string CharacterId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string OwnerUserId { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = string.Empty;
    public Dictionary<string, object> Stats { get; set; } = new Dictionary<string, object>();
    public Dictionary<string, object> Money { get; set; } = new Dictionary<string, object>();
    public List<Dictionary<string, object>> Inventory { get; set; } = new List<Dictionary<string, object>>();
    public List<Dictionary<string, object>> Skills { get; set; } = new List<Dictionary<string, object>>();
    public List<Dictionary<string, object>> Classes { get; set; } = new List<Dictionary<string, object>>();
    public List<Dictionary<string, object>> Companions { get; set; } = new List<Dictionary<string, object>>();
    public List<Dictionary<string, object>> Holdings { get; set; } = new List<Dictionary<string, object>>();
    public List<Dictionary<string, object>> Reputation { get; set; } = new List<Dictionary<string, object>>();
    public string Notes { get; set; } = string.Empty;
    public int SchemaVersion { get; set; } = 1;
}

public class LegacyCharacterViewComparison
{
    public bool IsEquivalent { get; set; }
    public List<string> Differences { get; set; } = new List<string>();
}

public sealed class LegacyCharacterViewAdapter : ILegacyCharacterViewAdapter
{
    private readonly ICharacterProfileShadowBuilder _shadowBuilder;

    public LegacyCharacterViewAdapter(ICharacterProfileShadowBuilder shadowBuilder)
    {
        _shadowBuilder = shadowBuilder;
    }

    public LegacyCharacterDetailsView BuildFromLegacyCharacter(Character character)
    {
        var source = character ?? new Character();
        return new LegacyCharacterDetailsView
        {
            CharacterId = source.Id,
            Name = source.Name,
            OwnerUserId = source.OwnerUserId,
            RuleSetId = RuleSetIds.FantasyNriDefault,
            Stats = new Dictionary<string, object>
            {
                { "health", source.Stats.Health },
                { "physicalArmor", source.Stats.PhysicalArmor },
                { "magicalArmor", source.Stats.MagicalArmor },
                { "morale", source.Stats.Morale },
                { "strength", source.Stats.Strength },
                { "dexterity", source.Stats.Dexterity },
                { "endurance", source.Stats.Endurance },
                { "wisdom", source.Stats.Wisdom },
                { "intellect", source.Stats.Intellect },
                { "charisma", source.Stats.Charisma }
            },
            Money = source.Wallet.Balance.Amounts.ToDictionary(x => x.Key, x => (object)x.Value, StringComparer.OrdinalIgnoreCase),
            Inventory = source.Inventory.Select(item => new Dictionary<string, object>
            {
                { "id", item.Id }, { "itemCode", item.ItemCode }, { "name", item.Name }, { "quantity", item.Quantity }, { "isEquipped", item.IsEquipped }
            }).ToList(),
            Skills = source.CharacterSkills.Select(skill => new Dictionary<string, object>
            {
                { "skillCode", skill.SkillCode }, { "tier", skill.Tier }, { "level", skill.Level }, { "acquired", skill.Acquired }
            }).ToList(),
            Classes = source.ClassProgress.Select(item => new Dictionary<string, object>
            {
                { "classCode", item.ClassCode }, { "level", item.Level }, { "experience", item.Experience }
            }).ToList(),
            Companions = source.Companions.Select(item => new Dictionary<string, object>
            {
                { "id", item.Id }, { "name", item.Name }, { "type", item.Type }
            }).ToList(),
            Holdings = source.Holdings.Select(item => new Dictionary<string, object>
            {
                { "id", item.Id }, { "name", item.Name }, { "type", item.Type }
            }).ToList(),
            Reputation = source.Reputation.Select(item => new Dictionary<string, object>
            {
                { "id", item.Id }, { "targetName", item.TargetName }, { "value", item.Value }
            }).ToList(),
            Notes = source.Description,
            SchemaVersion = source.SchemaVersion
        };
    }

    public LegacyCharacterDetailsView BuildFromProfileBundle(Character legacyCharacter, CharacterProfileBundle profileBundle)
    {
        // Profile-first mode is disabled by default; keep legacy projection as canonical fallback.
        // TODO(F0.4.3): when UseProfileFirstCharacterDetails=true, map bundle modules into full legacy-equivalent view.
        var view = BuildFromLegacyCharacter(legacyCharacter);
        if (profileBundle != null && !string.IsNullOrWhiteSpace(profileBundle.RuleSetId))
        {
            view.RuleSetId = profileBundle.RuleSetId;
        }
        return view;
    }

    public LegacyCharacterViewComparison CompareLegacyAndProfileView(Character character, CharacterProfileBundle profileBundle)
    {
        // Dry-run comparison helper for migration diagnostics; not invoked in request path yet.
        var legacyView = BuildFromLegacyCharacter(character);
        var profileView = BuildFromProfileBundle(character, profileBundle);
        var differences = new List<string>();

        if (!string.Equals(legacyView.CharacterId, profileView.CharacterId, StringComparison.Ordinal)) differences.Add("characterId");
        if (!string.Equals(legacyView.Name, profileView.Name, StringComparison.Ordinal)) differences.Add("name");
        if (legacyView.Skills.Count != profileView.Skills.Count) differences.Add("skills.count");
        if (legacyView.Classes.Count != profileView.Classes.Count) differences.Add("classes.count");
        if (legacyView.Inventory.Count != profileView.Inventory.Count) differences.Add("inventory.count");

        return new LegacyCharacterViewComparison
        {
            IsEquivalent = differences.Count == 0,
            Differences = differences
        };
    }

    public CharacterShadowCompareResult CompareLegacyAndProfileShadow(Character character)
    {
        return _shadowBuilder.CompareLegacyToShadow(character);
    }
}
