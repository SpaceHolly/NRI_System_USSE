using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Nri.Server.Logging;
using Nri.Shared.Domain;

namespace Nri.Server.Application.Services;

public interface IItemEquipmentDefinitionResolver
{
    Task<DefinitionResolveResult<ItemDefinitionView>> ResolveItemAsync(string definitionId, string ruleSetId);
    Task<DefinitionResolveResult<WeaponDefinitionView>> ResolveWeaponAsync(string definitionId, string ruleSetId);
    Task<DefinitionResolveResult<ArmorDefinitionView>> ResolveArmorAsync(string definitionId, string ruleSetId);
    Task<DefinitionResolveResult<AmmoDefinitionView>> ResolveAmmoAsync(string definitionId, string ruleSetId);
    Task<DefinitionResolveResult<EquipmentSlotDefinitionView>> ResolveEquipmentSlotAsync(string definitionId, string ruleSetId);
    Task<DefinitionBatchResolveResult<ItemDefinitionView>> ResolveItemsAsync(IEnumerable<string> definitionIds, string ruleSetId);
    Task<DefinitionBatchResolveResult<WeaponDefinitionView>> ResolveWeaponsAsync(IEnumerable<string> definitionIds, string ruleSetId);
    Task<DefinitionBatchResolveResult<ArmorDefinitionView>> ResolveArmorAsync(IEnumerable<string> definitionIds, string ruleSetId);
    Task<DefinitionBatchResolveResult<AmmoDefinitionView>> ResolveAmmoAsync(IEnumerable<string> definitionIds, string ruleSetId);
    Task<DefinitionBatchResolveResult<EquipmentSlotDefinitionView>> ResolveEquipmentSlotsAsync(IEnumerable<string> definitionIds, string ruleSetId);
    Task<DefinitionResolveResult<UnifiedDefinitionDocument>> ResolveByCategoryAsync(string definitionId, string category, string ruleSetId);
}

public sealed class ItemEquipmentDefinitionResolver : IItemEquipmentDefinitionResolver
{
    private readonly IDefinitionRepositoryV2? _repository;
    private readonly List<UnifiedDefinitionDocument> _definitions;
    private readonly IServerLogger? _logger;

    public ItemEquipmentDefinitionResolver(IDefinitionRepositoryV2 repository, IServerLogger? logger = null)
    {
        _repository = repository;
        _definitions = new List<UnifiedDefinitionDocument>();
        _logger = logger;
    }

    public ItemEquipmentDefinitionResolver(IEnumerable<UnifiedDefinitionDocument> definitions, IServerLogger? logger = null)
    {
        _definitions = (definitions ?? Enumerable.Empty<UnifiedDefinitionDocument>()).Where(x => x != null).ToList();
        _logger = logger;
    }

    public Task<DefinitionResolveResult<ItemDefinitionView>> ResolveItemAsync(string definitionId, string ruleSetId)
    {
        return ResolveTypedAsync<ItemDefinitionView>(definitionId, DefinitionCategoryIds.Item, ruleSetId, MapItem);
    }

    public Task<DefinitionResolveResult<WeaponDefinitionView>> ResolveWeaponAsync(string definitionId, string ruleSetId)
    {
        return ResolveTypedAsync<WeaponDefinitionView>(definitionId, DefinitionCategoryIds.Weapon, ruleSetId, MapWeapon);
    }

    public Task<DefinitionResolveResult<ArmorDefinitionView>> ResolveArmorAsync(string definitionId, string ruleSetId)
    {
        return ResolveTypedAsync<ArmorDefinitionView>(definitionId, DefinitionCategoryIds.Armor, ruleSetId, MapArmor);
    }

    public Task<DefinitionResolveResult<AmmoDefinitionView>> ResolveAmmoAsync(string definitionId, string ruleSetId)
    {
        return ResolveTypedAsync<AmmoDefinitionView>(definitionId, DefinitionCategoryIds.Ammo, ruleSetId, MapAmmo);
    }

    public Task<DefinitionResolveResult<EquipmentSlotDefinitionView>> ResolveEquipmentSlotAsync(string definitionId, string ruleSetId)
    {
        return ResolveTypedAsync<EquipmentSlotDefinitionView>(definitionId, DefinitionCategoryIds.EquipmentSlot, ruleSetId, MapEquipmentSlot);
    }

    public Task<DefinitionBatchResolveResult<ItemDefinitionView>> ResolveItemsAsync(IEnumerable<string> definitionIds, string ruleSetId)
    {
        return ResolveBatchAsync(definitionIds, ruleSetId, ResolveItemAsync);
    }

    public Task<DefinitionBatchResolveResult<WeaponDefinitionView>> ResolveWeaponsAsync(IEnumerable<string> definitionIds, string ruleSetId)
    {
        return ResolveBatchAsync(definitionIds, ruleSetId, ResolveWeaponAsync);
    }

    public Task<DefinitionBatchResolveResult<ArmorDefinitionView>> ResolveArmorAsync(IEnumerable<string> definitionIds, string ruleSetId)
    {
        return ResolveBatchAsync(definitionIds, ruleSetId, ResolveArmorAsync);
    }

    public Task<DefinitionBatchResolveResult<AmmoDefinitionView>> ResolveAmmoAsync(IEnumerable<string> definitionIds, string ruleSetId)
    {
        return ResolveBatchAsync(definitionIds, ruleSetId, ResolveAmmoAsync);
    }

    public Task<DefinitionBatchResolveResult<EquipmentSlotDefinitionView>> ResolveEquipmentSlotsAsync(IEnumerable<string> definitionIds, string ruleSetId)
    {
        return ResolveBatchAsync(definitionIds, ruleSetId, ResolveEquipmentSlotAsync);
    }

    public Task<DefinitionResolveResult<UnifiedDefinitionDocument>> ResolveByCategoryAsync(string definitionId, string category, string ruleSetId)
    {
        var result = CreateResult<UnifiedDefinitionDocument>(definitionId, category);
        var doc = LoadDefinition(definitionId, category);
        ValidateCommon(doc, definitionId, category, ruleSetId, result);
        if (result.Errors.Count == 0 && doc != null)
        {
            result.Value = doc;
        }

        Finish(result);
        return Task.FromResult(result);
    }

    public Task<DefinitionBatchResolveResult<UnifiedDefinitionDocument>> ValidateStarterEquipmentDefinitionsAsync(string ruleSetId)
    {
        var idsByCategory = _definitions
            .Where(x => IsSupportedCategory(x.Category))
            .Select(x => new { x.Id, x.Category })
            .ToList();

        var result = new DefinitionBatchResolveResult<UnifiedDefinitionDocument>();
        foreach (var item in idsByCategory)
        {
            var resolved = ResolveByCategoryAsync(item.Id, item.Category, ruleSetId).GetAwaiter().GetResult();
            result.Errors.AddRange(resolved.Errors);
            result.Warnings.AddRange(resolved.Warnings);
            if (resolved.Success) result.Values.Add(resolved.Value);
        }

        result.Success = result.Errors.Count == 0;
        return Task.FromResult(result);
    }

    private Task<DefinitionResolveResult<T>> ResolveTypedAsync<T>(string definitionId, string category, string ruleSetId, Func<UnifiedDefinitionDocument, DefinitionExtraDataReader, DefinitionResolveResult<T>, T> mapper)
    {
        _logger?.Debug($"item.definition.resolve.start id={definitionId} category={category}");
        var result = CreateResult<T>(definitionId, category);
        var doc = LoadDefinition(definitionId, category);
        ValidateCommon(doc, definitionId, category, ruleSetId, result);
        if (result.Errors.Count == 0 && doc != null)
        {
            var reader = new DefinitionExtraDataReader(doc.ExtraData);
            result.Value = mapper(doc, reader, result);
            result.Warnings.AddRange(reader.Warnings);
            result.Errors.AddRange(reader.Errors);
            ValidateMappedView(result.Value, result);
        }

        Finish(result);
        if (result.Errors.Count > 0) _logger?.Debug($"item.definition.resolve.error id={definitionId} errorCount={result.Errors.Count}");
        else if (result.Warnings.Count > 0) _logger?.Debug($"item.definition.resolve.warning id={definitionId} warningCount={result.Warnings.Count}");
        _logger?.Debug($"item.definition.resolve.done id={definitionId} success={result.Success}");
        return Task.FromResult(result);
    }

    private static async Task<DefinitionBatchResolveResult<T>> ResolveBatchAsync<T>(IEnumerable<string> definitionIds, string ruleSetId, Func<string, string, Task<DefinitionResolveResult<T>>> resolver)
    {
        var result = new DefinitionBatchResolveResult<T>();
        var ids = (definitionIds ?? Enumerable.Empty<string>())
            .Select(x => (x ?? string.Empty).Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var id in ids)
        {
            var item = await resolver(id, ruleSetId);
            result.Errors.AddRange(item.Errors);
            result.Warnings.AddRange(item.Warnings);
            if (item.Success) result.Values.Add(item.Value);
        }

        result.Success = result.Errors.Count == 0;
        return result;
    }

    private UnifiedDefinitionDocument? LoadDefinition(string definitionId, string category)
    {
        var id = (definitionId ?? string.Empty).Trim();
        var expectedCategory = (category ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(expectedCategory)) return null;
        if (_repository != null) return _repository.GetByIdAsync(expectedCategory, id);
        return _definitions.FirstOrDefault(x =>
            string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.Category, expectedCategory, StringComparison.OrdinalIgnoreCase));
    }

    private static void ValidateCommon<T>(UnifiedDefinitionDocument? doc, string definitionId, string category, string ruleSetId, DefinitionResolveResult<T> result)
    {
        if (string.IsNullOrWhiteSpace(definitionId)) result.Errors.Add("definition_id_required");
        if (string.IsNullOrWhiteSpace(category)) result.Errors.Add("category_required");
        if (doc == null)
        {
            result.Errors.Add("definition_not_found");
            return;
        }

        result.DefinitionId = doc.Id ?? string.Empty;
        result.Category = doc.Category ?? string.Empty;
        if (!string.Equals(doc.Category, category, StringComparison.OrdinalIgnoreCase)) result.Errors.Add($"category_mismatch:{doc.Category}");
        if (doc.IsArchived) result.Errors.Add("definition_archived");
        if (string.IsNullOrWhiteSpace(doc.Name)) result.Errors.Add("name_required");
        if (doc.SchemaVersion < 1) result.Errors.Add("schema_version_invalid");
        if (doc.ExtraData == null) result.Errors.Add("extra_data_null");
        if (!string.IsNullOrWhiteSpace(ruleSetId) && doc.RuleSetIds != null && doc.RuleSetIds.Count > 0 && !doc.RuleSetIds.Any(x => string.Equals(x, ruleSetId, StringComparison.OrdinalIgnoreCase))) result.Errors.Add($"ruleset_mismatch:{ruleSetId}");
    }

    private static ItemDefinitionView MapItem(UnifiedDefinitionDocument doc, DefinitionExtraDataReader reader, DefinitionResolveResult<ItemDefinitionView> result)
    {
        var view = new ItemDefinitionView
        {
            DefinitionId = doc.Id,
            Category = doc.Category,
            Name = doc.Name,
            DisplayNameRu = reader.GetString("displayNameRu", string.Empty),
            ItemType = reader.GetString("itemType", string.Empty),
            WeightKg = reader.GetDecimal("weightKg", 0m),
            Stackable = reader.GetBool("stackable", false),
            MaxStack = reader.GetInt("maxStack", 1),
            DefaultQuantity = reader.GetInt("defaultQuantity", 1),
            ValueCurrencyId = reader.GetString("valueCurrencyId", string.Empty),
            ValueAmountDraft = reader.GetLong("valueAmountDraft", 0L),
            IsConsumable = reader.GetBool("isConsumable", false),
            IsMagical = reader.GetBool("isMagical", false),
            IsRestricted = reader.GetBool("isRestricted", false),
            Tags = reader.GetStringList("tags"),
            SourceDefinitionTags = doc.Tags == null ? new List<string>() : new List<string>(doc.Tags),
            SchemaVersion = doc.SchemaVersion
        };

        if (view.Stackable && view.MaxStack < 1) result.Errors.Add("max_stack_invalid");
        if (view.DefaultQuantity < 0) result.Errors.Add("default_quantity_negative");
        if (view.WeightKg < 0m) result.Errors.Add("weight_kg_negative");
        if (view.ValueAmountDraft < 0) result.Errors.Add("value_amount_draft_negative");
        return view;
    }

    private static WeaponDefinitionView MapWeapon(UnifiedDefinitionDocument doc, DefinitionExtraDataReader reader, DefinitionResolveResult<WeaponDefinitionView> result)
    {
        var view = new WeaponDefinitionView
        {
            DefinitionId = doc.Id,
            Name = doc.Name,
            DisplayNameRu = reader.GetString("displayNameRu", string.Empty),
            WeaponType = FirstNonEmpty(reader.GetString("weaponType", string.Empty), reader.GetString("weaponCategory", string.Empty)),
            Handedness = reader.GetString("handedness", string.Empty),
            RangeType = reader.GetString("rangeType", string.Empty),
            DamageDraft = reader.GetString("damageDraft", string.Empty),
            AccuracyDraft = reader.GetString("accuracyDraft", string.Empty),
            PenetrationDraft = reader.GetString("penetrationDraft", string.Empty),
            LinkedSkillIds = FirstNonEmptyList(reader.GetStringList("linkedSkillIds"), reader.GetStringList("requiredSkillIds")),
            AttributeHints = FirstNonEmptyList(reader.GetStringList("attributeHints"), reader.GetStringList("requiredAttributeIds")),
            AmmoDefinitionIds = reader.GetStringList("ammoDefinitionIds"),
            EquipmentSlotIds = FirstNonEmptyList(reader.GetStringList("equipmentSlotIds"), reader.GetStringList("bodyRequirements")),
            AttackProfiles = reader.GetDictionaryList("attackProfiles").Select(MapAttackProfile).ToList(),
            WeightKg = reader.GetDecimal("weightKg", 0m),
            ValueCurrencyId = reader.GetString("valueCurrencyId", string.Empty),
            ValueAmountDraft = reader.GetLong("valueAmountDraft", 0L),
            TechTags = reader.GetStringList("techTags"),
            MagicTags = reader.GetStringList("magicTags"),
            LegalTags = reader.GetStringList("legalTags"),
            Tags = doc.Tags == null ? new List<string>() : new List<string>(doc.Tags),
            SchemaVersion = doc.SchemaVersion
        };

        if (view.LinkedSkillIds == null) result.Errors.Add("linked_skill_ids_null");
        if (view.EquipmentSlotIds == null) result.Errors.Add("equipment_slot_ids_null");
        if (view.WeightKg < 0m) result.Errors.Add("weight_kg_negative");
        if (view.ValueAmountDraft < 0) result.Errors.Add("value_amount_draft_negative");
        if ((view.TechTags ?? new List<string>()).Any(x => string.Equals(x, "gunpowder", StringComparison.OrdinalIgnoreCase))) result.Warnings.Add("tech_tags_contains_gunpowder");
        return view;
    }

    private static AttackProfileDefinition MapAttackProfile(Dictionary<string, object> map)
    {
        var reader = new DefinitionExtraDataReader(map);
        return new AttackProfileDefinition
        {
            ProfileId = reader.GetString("profileId", string.Empty),
            Name = reader.GetString("name", string.Empty),
            AttackType = reader.GetString("attackType", string.Empty),
            ActionCost = reader.GetInt("actionCost", 1),
            AttackRollType = reader.GetString("attackRollType", "d20"),
            SkillDefinitionId = reader.GetString("skillDefinitionId", string.Empty),
            SubAttributeDefinitionId = reader.GetString("subAttributeDefinitionId", string.Empty),
            AccuracyModifier = reader.GetInt("accuracyModifier", 0),
            Range = reader.GetString("range", string.Empty),
            DamageExpression = reader.GetString("damageExpression", string.Empty),
            DamageTypeDefinitionIds = reader.GetStringList("damageTypeDefinitionIds"),
            PhysicalPenetration = reader.GetInt("physicalPenetration", 0),
            ArmorPenetration = reader.GetInt("armorPenetration", 0),
            MagicPenetration = reader.GetInt("magicPenetration", 0),
            MoralePenetration = reader.GetInt("moralePenetration", 0),
            Area = reader.GetString("area", string.Empty),
            FireMode = reader.GetString("fireMode", string.Empty),
            ReloadCost = reader.GetInt("reloadCost", 0),
            AmmoCost = reader.GetInt("ammoCost", 0),
            CanReact = reader.GetBool("canReact", false),
            CanReturnFire = reader.GetBool("canReturnFire", false),
            CanParry = reader.GetBool("canParry", false),
            CanBlock = reader.GetBool("canBlock", false)
        };
    }

    private static ArmorDefinitionView MapArmor(UnifiedDefinitionDocument doc, DefinitionExtraDataReader reader, DefinitionResolveResult<ArmorDefinitionView> result)
    {
        var view = new ArmorDefinitionView
        {
            DefinitionId = doc.Id,
            Name = doc.Name,
            DisplayNameRu = reader.GetString("displayNameRu", string.Empty),
            ArmorType = FirstNonEmpty(reader.GetString("armorType", string.Empty), reader.GetString("armorCategory", string.Empty)),
            EquipmentSlotIds = FirstNonEmptyList(reader.GetStringList("equipmentSlotIds"), reader.GetStringList("protectedBodyZones")),
            PhysicalArmorDraft = FirstNonEmpty(reader.GetString("physicalArmorDraft", string.Empty), reader.GetString("physicalDefense", string.Empty)),
            ArmorRating = reader.GetInt("armorRating", reader.GetInt("physicalDefense", 0)),
            PenetrationResistanceByBodyZone = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                [BodyZoneIds.Head] = reader.GetInt("headPenetrationResistance", 0),
                [BodyZoneIds.Torso] = reader.GetInt("torsoPenetrationResistance", 0),
                [BodyZoneIds.LeftArm] = reader.GetInt("armsPenetrationResistance", 0), [BodyZoneIds.RightArm] = reader.GetInt("armsPenetrationResistance", 0),
                [BodyZoneIds.LeftLeg] = reader.GetInt("legsPenetrationResistance", 0), [BodyZoneIds.RightLeg] = reader.GetInt("legsPenetrationResistance", 0)
            },
            MagicArmorDraft = FirstNonEmpty(reader.GetString("magicArmorDraft", string.Empty), reader.GetString("magicalDefense", string.Empty)),
            MobilityPenaltyDraft = reader.GetString("mobilityPenaltyDraft", string.Empty),
            StealthPenaltyDraft = FirstNonEmpty(reader.GetString("stealthPenaltyDraft", string.Empty), reader.GetString("stealthPenalty", string.Empty)),
            HeightFitMode = reader.GetString("heightFitMode", string.Empty),
            SizeCategoryAllowed = reader.GetStringList("sizeCategoryAllowed"),
            WeightKg = reader.GetDecimal("weightKg", 0m),
            ValueCurrencyId = reader.GetString("valueCurrencyId", string.Empty),
            ValueAmountDraft = reader.GetLong("valueAmountDraft", 0L),
            Tags = doc.Tags == null ? new List<string>() : new List<string>(doc.Tags),
            SchemaVersion = doc.SchemaVersion
        };

        if (view.EquipmentSlotIds == null) result.Errors.Add("equipment_slot_ids_null");
        if (view.WeightKg < 0m) result.Errors.Add("weight_kg_negative");
        if (view.ValueAmountDraft < 0) result.Errors.Add("value_amount_draft_negative");
        if (!string.IsNullOrWhiteSpace(view.ArmorType) && !string.Equals(view.ArmorType, "shield", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(view.HeightFitMode)) result.Warnings.Add("height_fit_mode_missing");
        return view;
    }

    private static AmmoDefinitionView MapAmmo(UnifiedDefinitionDocument doc, DefinitionExtraDataReader reader, DefinitionResolveResult<AmmoDefinitionView> result)
    {
        var view = new AmmoDefinitionView
        {
            DefinitionId = doc.Id,
            Name = doc.Name,
            DisplayNameRu = reader.GetString("displayNameRu", string.Empty),
            AmmoType = reader.GetString("ammoType", string.Empty),
            CompatibleWeaponIds = reader.GetStringList("compatibleWeaponIds"),
            Stackable = reader.GetBool("stackable", true),
            MaxStack = reader.GetInt("maxStack", 1),
            DamageModifierDraft = reader.GetString("damageModifierDraft", string.Empty),
            PenetrationModifierDraft = reader.GetString("penetrationModifierDraft", string.Empty),
            IsMagical = reader.GetBool("isMagical", false),
            IsConsumable = reader.GetBool("isConsumable", true),
            ValueCurrencyId = reader.GetString("valueCurrencyId", string.Empty),
            ValueAmountDraft = reader.GetLong("valueAmountDraft", 0L),
            Tags = doc.Tags == null ? new List<string>() : new List<string>(doc.Tags),
            SchemaVersion = doc.SchemaVersion
        };

        if (view.CompatibleWeaponIds == null) result.Errors.Add("compatible_weapon_ids_null");
        if (view.MaxStack < 1) result.Errors.Add("max_stack_invalid");
        if (view.ValueAmountDraft < 0) result.Errors.Add("value_amount_draft_negative");
        return view;
    }

    private static EquipmentSlotDefinitionView MapEquipmentSlot(UnifiedDefinitionDocument doc, DefinitionExtraDataReader reader, DefinitionResolveResult<EquipmentSlotDefinitionView> result)
    {
        var view = new EquipmentSlotDefinitionView
        {
            DefinitionId = doc.Id,
            Name = doc.Name,
            DisplayNameRu = reader.GetString("displayNameRu", string.Empty),
            SlotGroup = reader.GetString("slotGroup", string.Empty),
            MaxItems = reader.GetInt("maxItems", 1),
            IsBodySlot = reader.GetBool("isBodySlot", false),
            IsContainerSlot = reader.GetBool("isContainerSlot", false),
            Tags = doc.Tags == null ? new List<string>() : new List<string>(doc.Tags),
            SchemaVersion = doc.SchemaVersion
        };

        if (view.MaxItems < 1) result.Errors.Add("max_items_invalid");
        return view;
    }

    private static void ValidateMappedView<T>(T view, DefinitionResolveResult<T> result)
    {
        if (view == null) result.Errors.Add("definition_mapping_failed");
    }

    private static DefinitionResolveResult<T> CreateResult<T>(string definitionId, string category)
    {
        return new DefinitionResolveResult<T>
        {
            DefinitionId = definitionId ?? string.Empty,
            Category = category ?? string.Empty
        };
    }

    private static void Finish<T>(DefinitionResolveResult<T> result)
    {
        result.Success = result.Errors.Count == 0;
    }

    private static string FirstNonEmpty(params string[] values)
        => values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;

    private static List<string> FirstNonEmptyList(params List<string>[] values)
        => values.FirstOrDefault(x => x != null && x.Count > 0) ?? new List<string>();

    private static bool IsSupportedCategory(string category)
    {
        return string.Equals(category, DefinitionCategoryIds.Item, StringComparison.OrdinalIgnoreCase)
            || string.Equals(category, DefinitionCategoryIds.Weapon, StringComparison.OrdinalIgnoreCase)
            || string.Equals(category, DefinitionCategoryIds.Armor, StringComparison.OrdinalIgnoreCase)
            || string.Equals(category, DefinitionCategoryIds.Ammo, StringComparison.OrdinalIgnoreCase)
            || string.Equals(category, DefinitionCategoryIds.EquipmentSlot, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class DefinitionExtraDataReader
{
    private readonly IDictionary<string, object> _data;

    public DefinitionExtraDataReader(IDictionary<string, object> data)
    {
        _data = data ?? new Dictionary<string, object>();
    }

    public List<string> Errors { get; } = new List<string>();
    public List<string> Warnings { get; } = new List<string>();

    public bool HasKey(string key)
    {
        return TryGetRaw(key, out _);
    }

    public string GetString(string key, string defaultValue)
    {
        if (!TryGetRaw(key, out var value)) return defaultValue;
        if (value == null) return defaultValue;
        try
        {
            if (value is string s) return s.Trim();
            if (value is JsonElement element)
            {
                if (element.ValueKind == JsonValueKind.String) return (element.GetString() ?? defaultValue).Trim();
                if (element.ValueKind == JsonValueKind.Number || element.ValueKind == JsonValueKind.True || element.ValueKind == JsonValueKind.False) return element.ToString().Trim();
                Warnings.Add($"field_not_scalar:{key}");
                return defaultValue;
            }

            if (IsScalar(value)) return Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim() ?? defaultValue;
            Warnings.Add($"field_not_scalar:{key}");
            return defaultValue;
        }
        catch
        {
            Warnings.Add($"field_parse_failed:{key}");
            return defaultValue;
        }
    }

    public bool GetBool(string key, bool defaultValue)
    {
        if (!TryGetRaw(key, out var value)) return defaultValue;
        try
        {
            if (value is bool b) return b;
            if (value is JsonElement element)
            {
                if (element.ValueKind == JsonValueKind.True) return true;
                if (element.ValueKind == JsonValueKind.False) return false;
                if (element.ValueKind == JsonValueKind.String && bool.TryParse(element.GetString(), out var parsed)) return parsed;
                Warnings.Add($"field_bool_parse_failed:{key}");
                return defaultValue;
            }

            if (bool.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out var parsedValue)) return parsedValue;
            Warnings.Add($"field_bool_parse_failed:{key}");
            return defaultValue;
        }
        catch
        {
            Warnings.Add($"field_bool_parse_failed:{key}");
            return defaultValue;
        }
    }

    public int GetInt(string key, int defaultValue)
    {
        if (!TryGetRaw(key, out var value)) return defaultValue;
        if (TryGetDecimalValue(key, value, out var parsed)) return (int)parsed;
        return defaultValue;
    }

    public long GetLong(string key, long defaultValue)
    {
        if (!TryGetRaw(key, out var value)) return defaultValue;
        if (TryGetDecimalValue(key, value, out var parsed)) return (long)parsed;
        return defaultValue;
    }

    public decimal GetDecimal(string key, decimal defaultValue)
    {
        if (!TryGetRaw(key, out var value)) return defaultValue;
        return TryGetDecimalValue(key, value, out var parsed) ? parsed : defaultValue;
    }

    public List<string> GetStringList(string key)
    {
        if (!TryGetRaw(key, out var value) || value == null) return new List<string>();
        try
        {
            if (value is JsonElement element)
            {
                if (element.ValueKind == JsonValueKind.Array)
                {
                    return element.EnumerateArray()
                        .Select(x => x.ValueKind == JsonValueKind.String ? x.GetString() : x.ToString())
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Select(x => x!.Trim())
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();
                }

                if (element.ValueKind == JsonValueKind.String)
                {
                    var single = element.GetString();
                    return string.IsNullOrWhiteSpace(single) ? new List<string>() : new List<string> { single.Trim() };
                }

                Warnings.Add($"field_list_parse_failed:{key}");
                return new List<string>();
            }

            if (value is string singleString)
            {
                return string.IsNullOrWhiteSpace(singleString) ? new List<string>() : new List<string> { singleString.Trim() };
            }

            if (value is IEnumerable enumerable)
            {
                return enumerable
                    .Cast<object>()
                    .Select(x => Convert.ToString(x, CultureInfo.InvariantCulture))
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x!.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            Warnings.Add($"field_list_parse_failed:{key}");
            return new List<string>();
        }
        catch
        {
            Warnings.Add($"field_list_parse_failed:{key}");
            return new List<string>();
        }
    }

    public List<Dictionary<string, object>> GetDictionaryList(string key)
    {
        if (!TryGetRaw(key, out var value) || value == null) return new List<Dictionary<string, object>>();
        try
        {
            if (value is JsonElement element && element.ValueKind == JsonValueKind.Array)
            {
                return element.EnumerateArray()
                    .Where(x => x.ValueKind == JsonValueKind.Object)
                    .Select(x => JsonSerializer.Deserialize<Dictionary<string, object>>(x.GetRawText()) ?? new Dictionary<string, object>())
                    .ToList();
            }

            if (value is IEnumerable enumerable)
            {
                return enumerable.Cast<object>()
                    .Select(x => x as Dictionary<string, object> ?? (x is IDictionary<string, object> map ? new Dictionary<string, object>(map) : null))
                    .Where(x => x != null)
                    .Cast<Dictionary<string, object>>()
                    .ToList();
            }
        }
        catch
        {
            Warnings.Add($"field_object_list_parse_failed:{key}");
        }
        return new List<Dictionary<string, object>>();
    }

    private bool TryGetRaw(string key, out object value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(key) || _data == null) return false;
        if (_data.TryGetValue(key, out value)) return true;
        var pair = _data.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(pair.Key)) return false;
        value = pair.Value;
        return true;
    }

    private bool TryGetDecimalValue(string key, object value, out decimal parsed)
    {
        parsed = 0m;
        try
        {
            if (value is decimal dec)
            {
                parsed = dec;
                return true;
            }

            if (value is int i)
            {
                parsed = i;
                return true;
            }

            if (value is long l)
            {
                parsed = l;
                return true;
            }

            if (value is double d)
            {
                parsed = Convert.ToDecimal(d, CultureInfo.InvariantCulture);
                return true;
            }

            if (value is float f)
            {
                parsed = Convert.ToDecimal(f, CultureInfo.InvariantCulture);
                return true;
            }

            if (value is JsonElement element)
            {
                if (element.ValueKind == JsonValueKind.Number && element.TryGetDecimal(out parsed)) return true;
                if (element.ValueKind == JsonValueKind.String && decimal.TryParse(element.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out parsed)) return true;
                Warnings.Add($"field_number_parse_failed:{key}");
                return false;
            }

            if (decimal.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out parsed)) return true;
            Warnings.Add($"field_number_parse_failed:{key}");
            return false;
        }
        catch
        {
            Warnings.Add($"field_number_parse_failed:{key}");
            return false;
        }
    }

    private static bool IsScalar(object value)
    {
        return value is string
            || value is bool
            || value is int
            || value is long
            || value is decimal
            || value is double
            || value is float;
    }
}
