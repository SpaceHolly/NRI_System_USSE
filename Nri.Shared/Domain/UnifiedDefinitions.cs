using System;
using System.Collections.Generic;
using System.Linq;

namespace Nri.Shared.Domain;

// Unified DefinitionBase skeleton for future common dictionary system.
// Legacy definitions (SkillDefinition/ClassDefinition/RaceDefinition) are intentionally not migrated in this step.
public class DefinitionBase
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = DefinitionCategoryIds.Other;
    public List<string> RuleSetIds { get; set; } = new List<string>();
    public List<string> Tags { get; set; } = new List<string>();
    public string PublicDescription { get; set; } = string.Empty;
    public string GMDescription { get; set; } = string.Empty;
    // ServerOnlyData must never be exposed to Player DTOs in future implementation.
    public Dictionary<string, object> ServerOnlyData { get; set; } = new Dictionary<string, object>();
    public Dictionary<string, object> ExtraData { get; set; } = new Dictionary<string, object>();
    // VisibilityRule is only metadata at this stage (no visibility service wiring yet).
    public string VisibilityRule { get; set; } = VisibilityRuleIds.Public;
    public int SchemaVersion { get; set; } = 1;
    public bool IsArchived { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    // Source metadata for future provenance and import pipelines.
    public string SourceDocument { get; set; } = string.Empty;
    public string SourceVersion { get; set; } = string.Empty;
    public string SourceSection { get; set; } = string.Empty;
    public string AuthorNote { get; set; } = string.Empty;
}

public static class DefinitionCategoryIds
{
    public const string Attribute = "attribute";
    public const string SubAttribute = "subAttribute";
    public const string DerivedStat = "derivedStat";
    public const string Skill = "skill";
    public const string DevelopmentNode = "developmentNode";
    public const string DevelopmentHexagon = "developmentHexagon";
    public const string Class = "class";
    public const string Race = "race";
    public const string Subspecies = "subspecies";
    public const string Hybrid = "hybrid";
    public const string HybridSubtype = "hybridSubtype";
    public const string RaceTrait = "raceTrait";
    public const string Language = "language";
    public const string Knowledge = "knowledge";
    public const string Item = "item";
    public const string Weapon = "weapon";
    public const string Armor = "armor";
    public const string Ammo = "ammo";
    public const string Resource = "resource";
    public const string Condition = "condition";
    public const string DamageType = "damageType";
    public const string Spell = "spell";
    public const string Seal = "seal";
    public const string MagicPath = "magicPath";
    public const string MagicElement = "magicElement";
    public const string MagicStone = "magicStone";
    public const string Augmentation = "augmentation";
    public const string Implant = "implant";
    public const string Cyberware = "cyberware";
    public const string Profession = "profession";
    public const string Specialization = "specialization";
    public const string License = "license";
    public const string Law = "law";
    public const string Market = "market";
    public const string Faction = "faction";
    public const string Organization = "organization";
    public const string Npc = "npc";
    public const string Continent = "continent";
    public const string Country = "country";
    public const string City = "city";
    public const string Planet = "planet";
    public const string SpaceLocation = "spaceLocation";
    public const string Location = "location";
    public const string Technology = "technology";
    public const string Blueprint = "blueprint";
    public const string ProjectTemplate = "projectTemplate";
    public const string BuildingModule = "buildingModule";
    public const string VehicleModule = "vehicleModule";
    public const string RoomType = "roomType";
    public const string ChronicleEvent = "chronicleEvent";
    public const string WorldEvent = "worldEvent";
    public const string Other = "other";
}

public static class VisibilityRuleIds
{
    public const string Public = "public";
    public const string PlayerVisible = "player_visible";
    public const string CharacterKnown = "character_known";
    public const string PartyKnown = "party_known";
    public const string FactionKnown = "faction_known";
    public const string OwnerKnown = "owner_known";
    public const string GmOnly = "gm_only";
    public const string SuperAdminOnly = "super_admin_only";
    public const string ServerOnly = "server_only";
    public const string HiddenUntilDiscovered = "hidden_until_discovered";
}

public static class DefinitionBaseNormalizer
{
    public static DefinitionBase Normalize(DefinitionBase source)
    {
        var item = source ?? new DefinitionBase();
        item.Id = (item.Id ?? string.Empty).Trim();
        item.Name = (item.Name ?? string.Empty).Trim();
        item.Category = (item.Category ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(item.Category)) item.Category = DefinitionCategoryIds.Other;

        item.Tags = NormalizeStringList(item.Tags);
        item.RuleSetIds = NormalizeStringList(item.RuleSetIds);

        if (item.SchemaVersion < 1) item.SchemaVersion = 1;
        if (item.CreatedAtUtc == default(DateTime)) item.CreatedAtUtc = DateTime.UtcNow;
        if (item.UpdatedAtUtc == default(DateTime)) item.UpdatedAtUtc = DateTime.UtcNow;
        if (string.IsNullOrWhiteSpace(item.VisibilityRule)) item.VisibilityRule = VisibilityRuleIds.Public;

        item.SourceDocument = (item.SourceDocument ?? string.Empty).Trim();
        item.SourceVersion = (item.SourceVersion ?? string.Empty).Trim();
        item.SourceSection = (item.SourceSection ?? string.Empty).Trim();
        item.AuthorNote = (item.AuthorNote ?? string.Empty).Trim();

        item.ServerOnlyData = item.ServerOnlyData ?? new Dictionary<string, object>();
        item.ExtraData = item.ExtraData ?? new Dictionary<string, object>();

        return item;
    }

    private static List<string> NormalizeStringList(List<string> source)
    {
        return (source ?? new List<string>())
            .Select(x => (x ?? string.Empty).Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}

public static class DefinitionBaseValidator
{
    public static void Validate(DefinitionBase source)
    {
        var item = DefinitionBaseNormalizer.Normalize(source);
        if (string.IsNullOrWhiteSpace(item.Id)) throw new ArgumentException("DefinitionBase.Id is required.");
        if (string.IsNullOrWhiteSpace(item.Name)) throw new ArgumentException("DefinitionBase.Name is required.");
        if (string.IsNullOrWhiteSpace(item.Category)) throw new ArgumentException("DefinitionBase.Category is required.");
        if (item.SchemaVersion < 1) throw new ArgumentException("DefinitionBase.SchemaVersion must be >= 1.");

        if (!string.IsNullOrWhiteSpace(item.VisibilityRule) && !IsKnownVisibilityRule(item.VisibilityRule))
        {
            // custom visibility strings are allowed by design for future extensions.
        }

        // TODO(F0.4.2f): enforce ServerOnlyData filtering in DTO adapters/visibility filter.
    }

    private static bool IsKnownVisibilityRule(string visibilityRule)
    {
        return string.Equals(visibilityRule, VisibilityRuleIds.Public, StringComparison.OrdinalIgnoreCase)
            || string.Equals(visibilityRule, VisibilityRuleIds.PlayerVisible, StringComparison.OrdinalIgnoreCase)
            || string.Equals(visibilityRule, VisibilityRuleIds.CharacterKnown, StringComparison.OrdinalIgnoreCase)
            || string.Equals(visibilityRule, VisibilityRuleIds.PartyKnown, StringComparison.OrdinalIgnoreCase)
            || string.Equals(visibilityRule, VisibilityRuleIds.FactionKnown, StringComparison.OrdinalIgnoreCase)
            || string.Equals(visibilityRule, VisibilityRuleIds.OwnerKnown, StringComparison.OrdinalIgnoreCase)
            || string.Equals(visibilityRule, VisibilityRuleIds.GmOnly, StringComparison.OrdinalIgnoreCase)
            || string.Equals(visibilityRule, VisibilityRuleIds.SuperAdminOnly, StringComparison.OrdinalIgnoreCase)
            || string.Equals(visibilityRule, VisibilityRuleIds.ServerOnly, StringComparison.OrdinalIgnoreCase)
            || string.Equals(visibilityRule, VisibilityRuleIds.HiddenUntilDiscovered, StringComparison.OrdinalIgnoreCase);
    }
}
