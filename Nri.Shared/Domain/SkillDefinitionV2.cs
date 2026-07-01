using System;
using System.Collections.Generic;
using System.Linq;

namespace Nri.Shared.Domain;

// Attribute identifiers for fantasy_nri_default ruleset (not hardwired into Character).
public static class CharacterAttributeIds
{
    public const string Health = "health";
    public const string PhysicalArmor = "physical_armor";
    public const string MagicArmor = "magic_armor";
    public const string Morale = "morale";
    public const string Strength = "strength";
    public const string Dexterity = "dexterity";
    public const string Endurance = "endurance";
    public const string Intellect = "intellect";
    public const string Wisdom = "wisdom";
    public const string Charisma = "charisma";
}

public static class CharacterVitalStatIds
{
    public const string HealthCurrent = "health_current";
    public const string HealthMax = "health_max";
    public const string PhysicalDefense = "physical_defense";
    public const string MagicalDefense = "magical_defense";
    public const string Morale = "morale";
    public const string Initiative = "initiative";
    public const string Movement = "movement";
    public const string CarryingCapacity = "carrying_capacity";
}

// Display groups for skill presentation and filtering.
public static class SkillDisplayGroups
{
    public const string Physical = "physical";
    public const string Dexterity = "dexterity";
    public const string Endurance = "endurance";
    public const string Knowledge = "knowledge";
    public const string Technical = "technical";
    public const string Field = "field";
    public const string Military = "military";
    public const string Social = "social";
    public const string Piloting = "piloting";
    public const string Magic = "magic";
    public const string Other = "other";
}

// V2 defaults/normalization used during read/write adaptation without DB migration.
public static class SkillDefinitionV2Defaults
{
    public static SkillDefinition Normalize(SkillDefinition source)
    {
        var item = source ?? new SkillDefinition();
        item.DisplayGroup = NormalizeDisplayGroup(item.DisplayGroup);
        item.DefaultAttribute = NormalizeString(item.DefaultAttribute);
        item.AllowedAttributes = NormalizeAllowedAttributes(item.AllowedAttributes, item.DefaultAttribute);
        item.DefaultSubAttribute = NormalizeString(item.DefaultSubAttribute);
        item.AllowedSubAttributes = NormalizeAllowedSubAttributes(item.AllowedSubAttributes, item.DefaultSubAttribute);
        item.SubAttributeMode = NormalizeSubAttributeMode(item.SubAttributeMode, item.DefaultSubAttribute);
        if (item.RankMin < 0) item.RankMin = 0;
        if (item.RankMax <= 0) item.RankMax = 20;
        if (item.RankMax < item.RankMin) item.RankMax = item.RankMin;
        if (item.SchemaVersion < 1) item.SchemaVersion = 1;
        if (string.IsNullOrWhiteSpace(item.VisibilityRule)) item.VisibilityRule = "default";
        if (!item.IsRollableExplicitlySet) item.IsRollable = true;
        return item;
    }

    private static string NormalizeDisplayGroup(string value)
    {
        var normalized = NormalizeString(value);
        return string.IsNullOrWhiteSpace(normalized) ? SkillDisplayGroups.Other : normalized;
    }

    private static List<string> NormalizeAllowedAttributes(List<string> source, string defaultAttribute)
    {
        var result = (source ?? new List<string>())
            .Select(NormalizeString)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (result.Count == 0 && !string.IsNullOrWhiteSpace(defaultAttribute))
        {
            result.Add(defaultAttribute);
        }

        return result;
    }

    private static List<string> NormalizeAllowedSubAttributes(List<string> source, string defaultSubAttribute)
    {
        var result = (source ?? new List<string>())
            .Select(NormalizeString)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (result.Count == 0 && !string.IsNullOrWhiteSpace(defaultSubAttribute))
        {
            result.Add(defaultSubAttribute);
        }

        return result;
    }

    private static string NormalizeSubAttributeMode(string value, string defaultSubAttribute)
    {
        var normalized = NormalizeString(value).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.IsNullOrWhiteSpace(defaultSubAttribute) ? "none" : "defaultFromSkill";
        }

        return normalized;
    }

    private static string NormalizeString(string value)
    {
        return (value ?? string.Empty).Trim();
    }
}
