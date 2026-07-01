using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Nri.Shared.Domain;

namespace Nri.Server.Application.Services;

public sealed class DefinitionPackIndex
{
    public Dictionary<string, UnifiedDefinitionDocument> ById { get; set; } = new Dictionary<string, UnifiedDefinitionDocument>(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, List<UnifiedDefinitionDocument>> ByCategory { get; set; } = new Dictionary<string, List<UnifiedDefinitionDocument>>(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> AllIds { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> AttributeIds { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> DerivedStatIds { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> CurrencyIds { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> SkillIds { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> DevelopmentNodeIds { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> RaceIds { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> SubspeciesIds { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> HybridIds { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> HybridSubtypeIds { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> RaceTraitIds { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> LanguageIds { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> ContinentIds { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> CountryIds { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> CityStateIds { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> ItemIds { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> WeaponIds { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> ArmorIds { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> AmmoIds { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> EquipmentSlotIds { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> ConditionIds { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> ConditionGroupIds { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> RegionIds { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> LocationIds { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> LocationTypeIds { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> FactionIds { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> OrganizationIds { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> LawIds { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> RestrictionIds { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> MarketTagIds { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
}

public sealed class DefinitionPackCrossReferenceValidator
{
    public DefinitionPackIndex BuildIndex(IEnumerable<UnifiedDefinitionDocument> definitions)
    {
        var index = new DefinitionPackIndex();
        foreach (var definition in definitions ?? Enumerable.Empty<UnifiedDefinitionDocument>())
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.Id))
            {
                continue;
            }

            var id = definition.Id.Trim();
            if (!index.ById.ContainsKey(id))
            {
                index.ById[id] = definition;
            }

            index.AllIds.Add(id);

            var category = (definition.Category ?? string.Empty).Trim();
            if (!index.ByCategory.TryGetValue(category, out var categoryItems))
            {
                categoryItems = new List<UnifiedDefinitionDocument>();
                index.ByCategory[category] = categoryItems;
            }

            categoryItems.Add(definition);
            AddCategoryId(index, category, id);
        }

        return index;
    }

    public DefinitionPackValidationResult ValidateReferences(DefinitionPackIndex index, IEnumerable<UnifiedDefinitionDocument> definitions, string expectedRuleSetId = "")
    {
        var result = new DefinitionPackValidationResult();
        var list = (definitions ?? Enumerable.Empty<UnifiedDefinitionDocument>()).Where(x => x != null).ToList();
        var duplicateIds = list
            .Where(x => !string.IsNullOrWhiteSpace(x.Id))
            .GroupBy(x => x.Id.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(x => x.Count() > 1)
            .Select(x => x.Key)
            .ToList();

        foreach (var duplicateId in duplicateIds)
        {
            result.CrossReferenceErrors.Add($"duplicate_id:{duplicateId}");
        }

        foreach (var definition in list)
        {
            ValidateCommon(definition, expectedRuleSetId, result);
            ValidateCategoryReferences(index, definition, result);
        }

        result.IsValid = result.Errors.Count == 0 && result.CrossReferenceErrors.Count == 0;
        result.DefinitionCount = list.Count;
        return result;
    }

    private static void ValidateCommon(UnifiedDefinitionDocument definition, string expectedRuleSetId, DefinitionPackValidationResult result)
    {
        var label = Label(definition);
        if (string.IsNullOrWhiteSpace(definition.Id)) result.Errors.Add("definition_id_required");
        if (string.IsNullOrWhiteSpace(definition.Category)) result.Errors.Add($"definition_category_required:{definition.Id}");
        if (string.IsNullOrWhiteSpace(definition.Name)) result.Errors.Add($"definition_name_required:{label}");
        if (definition.SchemaVersion < 1) result.Errors.Add($"definition_schema_version_invalid:{label}");
        if (definition.RuleSetIds == null || definition.RuleSetIds.Count == 0) result.Errors.Add($"definition_rulesets_required:{label}");
        if (!string.IsNullOrWhiteSpace(expectedRuleSetId)
            && (definition.RuleSetIds == null || !definition.RuleSetIds.Contains(expectedRuleSetId, StringComparer.OrdinalIgnoreCase)))
        {
            result.CrossReferenceErrors.Add($"definition_ruleset_missing:{label}:{expectedRuleSetId}");
        }

        if (definition.Tags == null) result.Errors.Add($"definition_tags_null:{label}");
        if (definition.ExtraData == null) result.Errors.Add($"definition_extra_data_null:{label}");
        if (definition.ServerOnlyData == null) result.Errors.Add($"definition_server_only_data_null:{label}");
    }

    private static void ValidateCategoryReferences(DefinitionPackIndex index, UnifiedDefinitionDocument definition, DefinitionPackValidationResult result)
    {
        var category = definition.Category ?? string.Empty;
        if (EqualsCategory(category, DefinitionCategoryIds.Skill))
        {
            RequireReference(index.AttributeIds, definition, "defaultAttribute", GetString(definition, "defaultAttribute"), result);
            RequireReferences(index.AttributeIds, definition, "allowedAttributes", GetStringList(definition, "allowedAttributes"), result);
            return;
        }

        if (EqualsCategory(category, DefinitionCategoryIds.DevelopmentNode))
        {
            RequireReferences(index.SkillIds, definition, "linkedSkillIds", GetStringList(definition, "linkedSkillIds"), result, required: false);
            RequireReferences(index.AttributeIds, definition, "linkedAttributeIds", GetStringList(definition, "linkedAttributeIds"), result, required: false);
            RequireReference(index.DevelopmentNodeIds, definition, "parentNodeId", GetString(definition, "parentNodeId"), result, required: false);
            return;
        }

        if (EqualsCategory(category, DefinitionCategoryIds.DevelopmentHexagon))
        {
            RequireReference(index.DevelopmentNodeIds, definition, "rootNodeId", GetString(definition, "rootNodeId"), result);
            RequireReferences(index.DevelopmentNodeIds, definition, "nodeIds", GetStringList(definition, "nodeIds"), result);
            return;
        }

        if (EqualsCategory(category, DefinitionCategoryIds.Race))
        {
            RequireReferences(index.SubspeciesIds, definition, "allowedSubspeciesIds", GetStringList(definition, "allowedSubspeciesIds"), result, required: false);
            RequireReferences(index.HybridIds, definition, "allowedHybridIds", GetStringList(definition, "allowedHybridIds"), result, required: false);
            ValidateRaceTraitsAndModifiers(index, definition, result);
            return;
        }

        if (EqualsCategory(category, DefinitionCategoryIds.Subspecies))
        {
            RequireReference(index.RaceIds, definition, "parentRaceId", GetString(definition, "parentRaceId"), result);
            ValidateRaceTraitsAndModifiers(index, definition, result);
            return;
        }

        if (EqualsCategory(category, DefinitionCategoryIds.Hybrid))
        {
            RequireReferences(index.RaceIds, definition, "parentRaceIds", GetStringList(definition, "parentRaceIds"), result);
            RequireReferences(index.HybridSubtypeIds, definition, "allowedSubtypeIds", GetStringList(definition, "allowedSubtypeIds"), result, required: false);
            ValidateRaceTraitsAndModifiers(index, definition, result);
            return;
        }

        if (EqualsCategory(category, DefinitionCategoryIds.HybridSubtype))
        {
            RequireReference(index.HybridIds, definition, "parentHybridId", GetString(definition, "parentHybridId"), result);
            RequireReference(index.RaceIds, definition, "dominantLineageRaceId", GetString(definition, "dominantLineageRaceId"), result, required: false);
            RequireReference(index.RaceIds, definition, "secondaryLineageRaceId", GetString(definition, "secondaryLineageRaceId"), result, required: false);
            ValidateRaceTraitsAndModifiers(index, definition, result);
            return;
        }

        if (EqualsCategory(category, DefinitionCategoryIds.Language))
        {
            RequireReference(index.ContinentIds, definition, "continentId", GetString(definition, "continentId"), result, required: false);
            RequireReferences(index.CountryIds, definition, "primaryCountryIds", GetStringList(definition, "primaryCountryIds"), result, required: false);
            RequireReferences(index.CountryIds, definition, "secondaryCountryIds", GetStringList(definition, "secondaryCountryIds"), result, required: false);
            RequireReferences(index.CityStateIds, definition, "cityStateIds", GetStringList(definition, "cityStateIds"), result, required: false);
            return;
        }

        if (EqualsCategory(category, DefinitionCategoryIds.Continent))
        {
            RequireReferences(index.LanguageIds, definition, "mainLanguageIds", GetStringList(definition, "mainLanguageIds"), result, required: false);
            RequireReferences(index.CountryIds, definition, "countryIds", GetStringList(definition, "countryIds"), result, required: false);
            RequireReferences(index.CityStateIds, definition, "cityStateIds", GetStringList(definition, "cityStateIds"), result, required: false);
            return;
        }

        if (EqualsCategory(category, DefinitionCategoryIds.Country))
        {
            RequireReference(index.ContinentIds, definition, "continentId", GetString(definition, "continentId"), result);
            RequireReferences(index.LanguageIds, definition, "primaryLanguageIds", GetStringList(definition, "primaryLanguageIds"), result, required: false);
            RequireReferences(index.LanguageIds, definition, "secondaryLanguageIds", GetStringList(definition, "secondaryLanguageIds"), result, required: false);
            return;
        }

        if (EqualsCategory(category, DefinitionCategoryIds.CityState))
        {
            RequireReference(index.ContinentIds, definition, "continentId", GetString(definition, "continentId"), result);
            RequireReferences(index.LanguageIds, definition, "languageIds", GetStringList(definition, "languageIds"), result);
            RequireReferences(index.CountryIds, definition, "neighboringCountryIds", GetStringList(definition, "neighboringCountryIds"), result, required: false);
            return;
        }

        if (EqualsCategory(category, DefinitionCategoryIds.Region))
        {
            RequireReference(index.ContinentIds, definition, "continentId", GetString(definition, "continentId"), result);
            RequireReferences(index.CountryIds, definition, "countryIds", GetStringList(definition, "countryIds"), result, required: false);
            return;
        }

        if (EqualsCategory(category, DefinitionCategoryIds.Location))
        {
            RequireReference(index.ContinentIds, definition, "continentId", GetString(definition, "continentId"), result);
            RequireReference(index.CountryIds, definition, "countryId", GetString(definition, "countryId"), result, required: false);
            RequireReference(index.CityStateIds, definition, "cityStateId", GetString(definition, "cityStateId"), result, required: false);
            RequireReference(index.RegionIds, definition, "regionId", GetString(definition, "regionId"), result, required: false);
            RequireReferences(index.LocationTypeIds, definition, "locationTypeIds", GetStringList(definition, "locationTypeIds"), result);
            RequireReferences(index.LanguageIds, definition, "knownLanguageIds", GetStringList(definition, "knownLanguageIds"), result, required: false);
            RequireReferences(index.CountryIds, definition, "linkedCountryIds", GetStringList(definition, "linkedCountryIds"), result, required: false);
            RequireReferences(index.CityStateIds, definition, "linkedCityStateIds", GetStringList(definition, "linkedCityStateIds"), result, required: false);
            return;
        }

        if (EqualsCategory(category, DefinitionCategoryIds.Item))
        {
            RequireReference(index.CurrencyIds, definition, "valueCurrencyId", GetString(definition, "valueCurrencyId"), result, required: false);
            return;
        }

        if (EqualsCategory(category, DefinitionCategoryIds.Weapon))
        {
            RequireReferences(index.SkillIds, definition, "linkedSkillIds", GetStringList(definition, "linkedSkillIds"), result, required: false);
            RequireReferences(index.AmmoIds, definition, "ammoDefinitionIds", GetStringList(definition, "ammoDefinitionIds"), result, required: false);
            RequireReferences(index.EquipmentSlotIds, definition, "equipmentSlotIds", GetStringList(definition, "equipmentSlotIds"), result, required: false);
            RequireReference(index.CurrencyIds, definition, "valueCurrencyId", GetString(definition, "valueCurrencyId"), result, required: false);
            return;
        }

        if (EqualsCategory(category, DefinitionCategoryIds.Armor))
        {
            RequireReferences(index.EquipmentSlotIds, definition, "equipmentSlotIds", GetStringList(definition, "equipmentSlotIds"), result, required: false);
            RequireReference(index.CurrencyIds, definition, "valueCurrencyId", GetString(definition, "valueCurrencyId"), result, required: false);
            return;
        }

        if (EqualsCategory(category, DefinitionCategoryIds.Ammo))
        {
            RequireReferences(index.WeaponIds, definition, "compatibleWeaponIds", GetStringList(definition, "compatibleWeaponIds"), result, required: false);
            RequireReference(index.CurrencyIds, definition, "valueCurrencyId", GetString(definition, "valueCurrencyId"), result, required: false);
            return;
        }

        if (EqualsCategory(category, DefinitionCategoryIds.Condition))
        {
            RequireReference(index.ConditionGroupIds, definition, "conditionGroup", GetString(definition, "conditionGroup"), result);
            RequireReferences(index.DerivedStatIds, definition, "linkedDerivedStatIds", GetStringList(definition, "linkedDerivedStatIds"), result, required: false);
            RequireReferences(index.AttributeIds, definition, "linkedAttributeIds", GetStringList(definition, "linkedAttributeIds"), result, required: false);
            return;
        }

        if (EqualsCategory(category, DefinitionCategoryIds.Faction))
        {
            RequireReference(index.CountryIds, definition, "countryId", GetString(definition, "countryId"), result, required: false);
            RequireReference(index.CityStateIds, definition, "cityStateId", GetString(definition, "cityStateId"), result, required: false);
            RequireReferences(index.LanguageIds, definition, "primaryLanguageIds", GetStringList(definition, "primaryLanguageIds"), result, required: false);
            RequireReferences(index.OrganizationIds, definition, "associatedOrganizationIds", GetStringList(definition, "associatedOrganizationIds"), result, required: false);
            return;
        }

        if (EqualsCategory(category, DefinitionCategoryIds.Organization))
        {
            RequireReference(index.FactionIds, definition, "parentFactionId", GetString(definition, "parentFactionId"), result, required: false);
            RequireReference(index.CountryIds, definition, "countryId", GetString(definition, "countryId"), result, required: false);
            RequireReference(index.CityStateIds, definition, "cityStateId", GetString(definition, "cityStateId"), result, required: false);
            RequireReferences(index.LocationIds, definition, "locationIds", GetStringList(definition, "locationIds"), result, required: false);
            RequireReferences(index.LanguageIds, definition, "languageIds", GetStringList(definition, "languageIds"), result, required: false);
            return;
        }

        if (EqualsCategory(category, DefinitionCategoryIds.Law))
        {
            RequireReferences(index.CountryIds, definition, "countryIds", GetStringList(definition, "countryIds"), result, required: false);
            RequireReferences(index.CityStateIds, definition, "cityStateIds", GetStringList(definition, "cityStateIds"), result, required: false);
            RequireReferences(index.OrganizationIds, definition, "affectedOrganizationIds", GetStringList(definition, "affectedOrganizationIds"), result, required: false);
            return;
        }

        if (EqualsCategory(category, DefinitionCategoryIds.Restriction))
        {
            RequireReferences(index.CountryIds, definition, "countryIds", GetStringList(definition, "countryIds"), result, required: false);
            RequireReferences(index.CityStateIds, definition, "cityStateIds", GetStringList(definition, "cityStateIds"), result, required: false);
            RequireReferences(index.LawIds, definition, "relatedLawIds", GetStringList(definition, "relatedLawIds"), result, required: false);
            return;
        }

        if (EqualsCategory(category, DefinitionCategoryIds.MarketTag))
        {
            RequireReferences(index.CountryIds, definition, "commonCountryIds", GetStringList(definition, "commonCountryIds"), result, required: false);
            RequireReferences(index.CountryIds, definition, "restrictedCountryIds", GetStringList(definition, "restrictedCountryIds"), result, required: false);
            RequireReferences(index.RestrictionIds, definition, "relatedRestrictionIds", GetStringList(definition, "relatedRestrictionIds"), result, required: false);
        }
    }

    private static void ValidateRaceTraitsAndModifiers(DefinitionPackIndex index, UnifiedDefinitionDocument definition, DefinitionPackValidationResult result)
    {
        RequireReferences(index.RaceTraitIds, definition, "traitIds", GetStringList(definition, "traitIds"), result, required: false);
        foreach (var key in GetObjectKeys(definition, "attributeModifiers"))
        {
            if (!index.AttributeIds.Contains(key))
            {
                result.CrossReferenceErrors.Add($"missing_reference:{Label(definition)}:attributeModifiers.{key}");
            }
        }
    }

    private static void RequireReference(HashSet<string> targetIds, UnifiedDefinitionDocument definition, string field, string value, DefinitionPackValidationResult result, bool required = true)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            if (required)
            {
                result.CrossReferenceErrors.Add($"missing_required_reference:{Label(definition)}:{field}");
            }

            return;
        }

        if (!targetIds.Contains(value.Trim()))
        {
            result.CrossReferenceErrors.Add($"missing_reference:{Label(definition)}:{field}:{value.Trim()}");
        }
    }

    private static void RequireReferences(HashSet<string> targetIds, UnifiedDefinitionDocument definition, string field, IEnumerable<string> values, DefinitionPackValidationResult result, bool required = true)
    {
        var list = values.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (list.Count == 0)
        {
            if (required)
            {
                result.CrossReferenceErrors.Add($"missing_required_reference:{Label(definition)}:{field}");
            }

            return;
        }

        foreach (var value in list)
        {
            if (!targetIds.Contains(value))
            {
                result.CrossReferenceErrors.Add($"missing_reference:{Label(definition)}:{field}:{value}");
            }
        }
    }

    private static string GetString(UnifiedDefinitionDocument definition, string key)
    {
        if (definition.ExtraData == null || !definition.ExtraData.TryGetValue(key, out var value) || value == null)
        {
            return string.Empty;
        }

        if (value is string s) return s.Trim();
        if (value is JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.String) return (element.GetString() ?? string.Empty).Trim();
            if (element.ValueKind == JsonValueKind.Number || element.ValueKind == JsonValueKind.True || element.ValueKind == JsonValueKind.False) return element.ToString().Trim();
            return string.Empty;
        }

        return Convert.ToString(value)?.Trim() ?? string.Empty;
    }

    private static List<string> GetStringList(UnifiedDefinitionDocument definition, string key)
    {
        var values = new List<string>();
        if (definition.ExtraData == null || !definition.ExtraData.TryGetValue(key, out var value) || value == null)
        {
            return values;
        }

        if (value is JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray())
                {
                    var text = item.ValueKind == JsonValueKind.String ? item.GetString() : item.ToString();
                    if (!string.IsNullOrWhiteSpace(text)) values.Add(text.Trim());
                }
            }
            else if (element.ValueKind == JsonValueKind.String)
            {
                var text = element.GetString();
                if (!string.IsNullOrWhiteSpace(text)) values.Add(text.Trim());
            }

            return values;
        }

        if (value is IEnumerable<string> strings)
        {
            values.AddRange(strings.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()));
            return values;
        }

        if (value is System.Collections.IEnumerable enumerable && value is not string)
        {
            foreach (var item in enumerable)
            {
                var text = Convert.ToString(item);
                if (!string.IsNullOrWhiteSpace(text)) values.Add(text.Trim());
            }

            return values;
        }

        var single = Convert.ToString(value);
        if (!string.IsNullOrWhiteSpace(single)) values.Add(single.Trim());
        return values;
    }

    private static List<string> GetObjectKeys(UnifiedDefinitionDocument definition, string key)
    {
        var keys = new List<string>();
        if (definition.ExtraData == null || !definition.ExtraData.TryGetValue(key, out var value) || value == null)
        {
            return keys;
        }

        if (value is JsonElement element && element.ValueKind == JsonValueKind.Object)
        {
            keys.AddRange(element.EnumerateObject().Select(x => x.Name).Where(x => !string.IsNullOrWhiteSpace(x)));
            return keys;
        }

        if (value is IDictionary<string, object> map)
        {
            keys.AddRange(map.Keys.Where(x => !string.IsNullOrWhiteSpace(x)));
        }

        return keys;
    }

    private static void AddCategoryId(DefinitionPackIndex index, string category, string id)
    {
        if (EqualsCategory(category, DefinitionCategoryIds.Attribute)) index.AttributeIds.Add(id);
        else if (EqualsCategory(category, DefinitionCategoryIds.DerivedStat)) index.DerivedStatIds.Add(id);
        else if (EqualsCategory(category, DefinitionCategoryIds.Currency)) index.CurrencyIds.Add(id);
        else if (EqualsCategory(category, DefinitionCategoryIds.Skill)) index.SkillIds.Add(id);
        else if (EqualsCategory(category, DefinitionCategoryIds.DevelopmentNode)) index.DevelopmentNodeIds.Add(id);
        else if (EqualsCategory(category, DefinitionCategoryIds.Race)) index.RaceIds.Add(id);
        else if (EqualsCategory(category, DefinitionCategoryIds.Subspecies)) index.SubspeciesIds.Add(id);
        else if (EqualsCategory(category, DefinitionCategoryIds.Hybrid)) index.HybridIds.Add(id);
        else if (EqualsCategory(category, DefinitionCategoryIds.HybridSubtype)) index.HybridSubtypeIds.Add(id);
        else if (EqualsCategory(category, DefinitionCategoryIds.RaceTrait)) index.RaceTraitIds.Add(id);
        else if (EqualsCategory(category, DefinitionCategoryIds.Language)) index.LanguageIds.Add(id);
        else if (EqualsCategory(category, DefinitionCategoryIds.Continent)) index.ContinentIds.Add(id);
        else if (EqualsCategory(category, DefinitionCategoryIds.Country)) index.CountryIds.Add(id);
        else if (EqualsCategory(category, DefinitionCategoryIds.CityState)) index.CityStateIds.Add(id);
        else if (EqualsCategory(category, DefinitionCategoryIds.Item)) index.ItemIds.Add(id);
        else if (EqualsCategory(category, DefinitionCategoryIds.Weapon)) index.WeaponIds.Add(id);
        else if (EqualsCategory(category, DefinitionCategoryIds.Armor)) index.ArmorIds.Add(id);
        else if (EqualsCategory(category, DefinitionCategoryIds.Ammo)) index.AmmoIds.Add(id);
        else if (EqualsCategory(category, DefinitionCategoryIds.EquipmentSlot)) index.EquipmentSlotIds.Add(id);
        else if (EqualsCategory(category, DefinitionCategoryIds.Condition)) index.ConditionIds.Add(id);
        else if (EqualsCategory(category, DefinitionCategoryIds.ConditionGroup)) index.ConditionGroupIds.Add(id);
        else if (EqualsCategory(category, DefinitionCategoryIds.Region)) index.RegionIds.Add(id);
        else if (EqualsCategory(category, DefinitionCategoryIds.Location)) index.LocationIds.Add(id);
        else if (EqualsCategory(category, DefinitionCategoryIds.LocationType)) index.LocationTypeIds.Add(id);
        else if (EqualsCategory(category, DefinitionCategoryIds.Faction)) index.FactionIds.Add(id);
        else if (EqualsCategory(category, DefinitionCategoryIds.Organization)) index.OrganizationIds.Add(id);
        else if (EqualsCategory(category, DefinitionCategoryIds.Law)) index.LawIds.Add(id);
        else if (EqualsCategory(category, DefinitionCategoryIds.Restriction)) index.RestrictionIds.Add(id);
        else if (EqualsCategory(category, DefinitionCategoryIds.MarketTag)) index.MarketTagIds.Add(id);
    }

    private static bool EqualsCategory(string actual, string expected)
        => string.Equals(actual ?? string.Empty, expected, StringComparison.OrdinalIgnoreCase);

    private static string Label(UnifiedDefinitionDocument definition)
        => $"{definition.Category}:{definition.Id}";
}
