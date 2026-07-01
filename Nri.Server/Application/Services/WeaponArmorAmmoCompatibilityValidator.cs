using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nri.Server.Logging;
using Nri.Shared.Domain;

namespace Nri.Server.Application.Services;

public interface IWeaponArmorAmmoCompatibilityValidator
{
    Task<InventoryValidationResult> ValidateWeaponAsync(InventoryItemInstanceState weaponItem, string ruleSetId, bool strictMode);
    Task<InventoryValidationResult> ValidateArmorAsync(InventoryItemInstanceState armorItem, string ruleSetId, bool strictMode);
    Task<InventoryValidationResult> ValidateAmmoAsync(InventoryItemInstanceState ammoItem, string ruleSetId, bool strictMode);
    Task<InventoryValidationResult> ValidateAmmoForWeaponAsync(InventoryItemInstanceState ammoItem, InventoryItemInstanceState weaponItem, string ruleSetId, bool strictMode);
    Task<InventoryValidationResult> ValidateEquippedWeaponAmmoSetAsync(IEnumerable<InventoryItemInstanceState> items, EquipmentLoadoutState loadout, string ruleSetId, bool strictMode);
    Task<InventoryValidationResult> ValidateArmorSizeFitAsync(InventoryItemInstanceState armorItem, string characterSizeCategory, int characterHeightCm, string ruleSetId, bool strictMode);
}

public sealed class WeaponArmorAmmoCompatibilityValidator : IWeaponArmorAmmoCompatibilityValidator
{
    private static readonly HashSet<string> BodySlots = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "head", "torso", "legs", "feet", "hands", "back"
    };

    private readonly IItemEquipmentDefinitionResolver _definitionResolver;
    private readonly IEquipmentSlotValidator? _equipmentSlotValidator;
    private readonly IServerLogger? _logger;

    public WeaponArmorAmmoCompatibilityValidator(IItemEquipmentDefinitionResolver definitionResolver, IServerLogger? logger = null)
        : this(definitionResolver, null, logger)
    {
    }

    public WeaponArmorAmmoCompatibilityValidator(IItemEquipmentDefinitionResolver definitionResolver, IEquipmentSlotValidator? equipmentSlotValidator, IServerLogger? logger = null)
    {
        _definitionResolver = definitionResolver ?? throw new ArgumentNullException(nameof(definitionResolver));
        _equipmentSlotValidator = equipmentSlotValidator;
        _logger = logger;
    }

    public async Task<InventoryValidationResult> ValidateWeaponAsync(InventoryItemInstanceState weaponItem, string ruleSetId, bool strictMode)
    {
        var result = CreateResult("weapon_compatibility");
        var weapon = await ResolveWeaponOrIssue(weaponItem, ruleSetId, result, strictMode);
        if (weapon == null) return result;

        if (weapon.EquipmentSlotIds == null || weapon.EquipmentSlotIds.Count == 0)
        {
            AddByMode(result, strictMode, "weapon_slot_ids_missing", "Weapon definition should declare equipmentSlotIds.", weaponItem.ItemInstanceId, weaponItem.DefinitionId);
        }

        if (string.IsNullOrWhiteSpace(weapon.Handedness))
        {
            AddByMode(result, strictMode, "weapon_handedness_missing", "Weapon definition should declare handedness.", weaponItem.ItemInstanceId, weaponItem.DefinitionId);
        }
        else if (string.Equals(weapon.Handedness, "two_handed", StringComparison.OrdinalIgnoreCase)
            && !(weapon.EquipmentSlotIds ?? new List<string>()).Contains("two_handed", StringComparer.OrdinalIgnoreCase))
        {
            AddWarning(result, "weapon_two_handed_slot_mismatch", "Two-handed weapon should allow the two_handed equipment slot.", weaponItem.ItemInstanceId, weaponItem.DefinitionId);
        }

        if (weapon.LinkedSkillIds == null || weapon.LinkedSkillIds.Count == 0)
        {
            AddWarning(result, "weapon_linked_skills_missing", "Weapon definition should declare linkedSkillIds for future roll validation.", weaponItem.ItemInstanceId, weaponItem.DefinitionId);
        }

        if ((weapon.TechTags ?? new List<string>()).Any(x => string.Equals(x, "gunpowder", StringComparison.OrdinalIgnoreCase)))
        {
            AddWarning(result, "weapon_gunpowder_warning", "Weapon techTags include gunpowder; fantasy starter should avoid ordinary gunpowder weapons.", weaponItem.ItemInstanceId, weaponItem.DefinitionId);
        }

        return result;
    }

    public async Task<InventoryValidationResult> ValidateArmorAsync(InventoryItemInstanceState armorItem, string ruleSetId, bool strictMode)
    {
        var result = CreateResult("armor_compatibility");
        var armor = await ResolveArmorOrIssue(armorItem, ruleSetId, result, strictMode);
        if (armor == null) return result;

        var slots = armor.EquipmentSlotIds ?? new List<string>();
        if (slots.Count == 0)
        {
            AddByMode(result, strictMode, "armor_slot_ids_missing", "Armor definition should declare equipmentSlotIds.", armorItem.ItemInstanceId, armorItem.DefinitionId);
        }

        if (string.IsNullOrWhiteSpace(armor.ArmorType))
        {
            AddWarning(result, "armor_type_missing", "Armor definition should declare armorType.", armorItem.ItemInstanceId, armorItem.DefinitionId);
        }

        if (IsShield(armor))
        {
            if (!slots.Contains("off_hand", StringComparer.OrdinalIgnoreCase))
            {
                AddByMode(result, strictMode, "shield_slot_mismatch", "Shield armor should be compatible with off_hand.", armorItem.ItemInstanceId, armorItem.DefinitionId);
            }

            if (slots.Any(x => BodySlots.Contains(x) && !string.Equals(x, "hands", StringComparison.OrdinalIgnoreCase)))
            {
                AddWarning(result, "shield_slot_mismatch", "Shield should not require torso/head/legs/feet body armor slots.", armorItem.ItemInstanceId, armorItem.DefinitionId);
            }
        }
        else
        {
            if (slots.Count > 0 && !slots.Any(x => BodySlots.Contains(x)))
            {
                AddByMode(result, strictMode, "wearable_armor_slot_mismatch", "Wearable armor should use a body slot.", armorItem.ItemInstanceId, armorItem.DefinitionId);
            }

            if (string.IsNullOrWhiteSpace(armor.HeightFitMode))
            {
                AddWarning(result, "armor_height_fit_mode_missing", "Wearable armor should declare heightFitMode for future size-fit validation.", armorItem.ItemInstanceId, armorItem.DefinitionId);
            }
        }

        return result;
    }

    public async Task<InventoryValidationResult> ValidateAmmoAsync(InventoryItemInstanceState ammoItem, string ruleSetId, bool strictMode)
    {
        var result = CreateResult("ammo_compatibility");
        var ammo = await ResolveAmmoOrIssue(ammoItem, ruleSetId, result, strictMode);
        if (ammo == null) return result;

        if (ammo.CompatibleWeaponIds == null)
        {
            AddByMode(result, strictMode, "ammo_weapon_compatibility_unknown", "Ammo definition has null CompatibleWeaponIds.", ammoItem.ItemInstanceId, ammoItem.DefinitionId);
        }
        else if (ammo.CompatibleWeaponIds.Count == 0)
        {
            AddWarning(result, "ammo_weapon_compatibility_unknown", "Ammo definition does not declare compatible weapons.", ammoItem.ItemInstanceId, ammoItem.DefinitionId);
        }

        return result;
    }

    public async Task<InventoryValidationResult> ValidateAmmoForWeaponAsync(InventoryItemInstanceState ammoItem, InventoryItemInstanceState weaponItem, string ruleSetId, bool strictMode)
    {
        var result = CreateResult("ammo_weapon_compatibility");
        var ammo = await ResolveAmmoOrIssue(ammoItem, ruleSetId, result, strictMode);
        var weapon = await ResolveWeaponOrIssue(weaponItem, ruleSetId, result, strictMode);
        if (ammo == null || weapon == null) return result;

        var weaponAmmoIds = weapon.AmmoDefinitionIds ?? new List<string>();
        var ammoWeaponIds = ammo.CompatibleWeaponIds ?? new List<string>();
        var weaponListsAmmo = weaponAmmoIds.Contains(ammo.DefinitionId, StringComparer.OrdinalIgnoreCase);
        var ammoListsWeapon = ammoWeaponIds.Contains(weapon.DefinitionId, StringComparer.OrdinalIgnoreCase);

        if (IsMeleeWeapon(weapon) && !IsEmptyAmmo(ammo))
        {
            AddByMode(result, strictMode, "ammo_for_melee_weapon", "Ammo was checked against a melee weapon.", ammoItem.ItemInstanceId, ammoItem.DefinitionId);
        }

        if (weaponAmmoIds.Count == 0 && ammoWeaponIds.Count == 0)
        {
            AddByMode(result, strictMode, "ammo_weapon_compatibility_unknown", "Neither weapon nor ammo declares compatibility lists.", ammoItem.ItemInstanceId, ammoItem.DefinitionId);
            return result;
        }

        if (weaponAmmoIds.Count == 0 && ammoWeaponIds.Count > 0 && !ammoListsWeapon)
        {
            AddError(result, "ammo_not_compatible_with_weapon", "Ammo compatibleWeaponIds does not include weapon definition.", ammoItem.ItemInstanceId, ammoItem.DefinitionId);
            return result;
        }

        if (weaponAmmoIds.Count > 0 && ammoWeaponIds.Count == 0 && !weaponListsAmmo)
        {
            AddError(result, "ammo_not_compatible_with_weapon", "Weapon ammoDefinitionIds does not include ammo definition.", ammoItem.ItemInstanceId, ammoItem.DefinitionId);
            return result;
        }

        if (weaponAmmoIds.Count > 0 && ammoWeaponIds.Count > 0 && (!weaponListsAmmo || !ammoListsWeapon))
        {
            AddError(result, "ammo_not_compatible_with_weapon", "Weapon and ammo compatibility lists do not agree.", ammoItem.ItemInstanceId, ammoItem.DefinitionId);
        }

        return result;
    }

    public async Task<InventoryValidationResult> ValidateEquippedWeaponAmmoSetAsync(IEnumerable<InventoryItemInstanceState> items, EquipmentLoadoutState loadout, string ruleSetId, bool strictMode)
    {
        var list = (items ?? Enumerable.Empty<InventoryItemInstanceState>()).Where(x => x != null).ToList();
        _logger?.Debug($"equipment.compat.validation.start itemCount={list.Count}");
        var result = CreateResult("equipped_weapon_ammo_set");

        var equippedWeapons = new List<Tuple<InventoryItemInstanceState, WeaponDefinitionView>>();
        var ammoItems = new List<Tuple<InventoryItemInstanceState, AmmoDefinitionView>>();

        foreach (var item in list)
        {
            var weapon = await _definitionResolver.ResolveWeaponAsync(item.DefinitionId, ruleSetId);
            if (weapon.Success && weapon.Value != null)
            {
                if (item.IsEquipped) equippedWeapons.Add(Tuple.Create(item, weapon.Value));
                continue;
            }

            var ammo = await _definitionResolver.ResolveAmmoAsync(item.DefinitionId, ruleSetId);
            if (ammo.Success && ammo.Value != null)
            {
                ammoItems.Add(Tuple.Create(item, ammo.Value));
            }
        }

        foreach (var weapon in equippedWeapons)
        {
            Merge(result, await ValidateWeaponAsync(weapon.Item1, ruleSetId, strictMode));
            var requiredAmmo = weapon.Item2.AmmoDefinitionIds ?? new List<string>();
            if (requiredAmmo.Count == 0) continue;

            var hasCompatibleAmmo = ammoItems.Any(x =>
                requiredAmmo.Contains(x.Item2.DefinitionId, StringComparer.OrdinalIgnoreCase)
                || (x.Item2.CompatibleWeaponIds ?? new List<string>()).Contains(weapon.Item2.DefinitionId, StringComparer.OrdinalIgnoreCase));
            if (!hasCompatibleAmmo)
            {
                AddWarning(result, "ranged_weapon_without_compatible_ammo", "Equipped weapon declares ammoDefinitionIds, but inventory has no compatible ammo.", weapon.Item1.ItemInstanceId, weapon.Item1.DefinitionId);
            }
        }

        foreach (var ammo in ammoItems.Where(x => x.Item1.IsEquipped))
        {
            var hasWeapon = equippedWeapons.Any(x =>
                (x.Item2.AmmoDefinitionIds ?? new List<string>()).Contains(ammo.Item2.DefinitionId, StringComparer.OrdinalIgnoreCase)
                || (ammo.Item2.CompatibleWeaponIds ?? new List<string>()).Contains(x.Item2.DefinitionId, StringComparer.OrdinalIgnoreCase));
            if (!hasWeapon)
            {
                AddWarning(result, "ammo_weapon_compatibility_unknown", "Equipped ammo-like item has no compatible equipped weapon.", ammo.Item1.ItemInstanceId, ammo.Item1.DefinitionId);
            }
        }

        if (_equipmentSlotValidator != null && loadout != null)
        {
            var equipmentResult = _equipmentSlotValidator.ValidateTwoHandedConflicts(loadout, list);
            Merge(result, equipmentResult);
        }

        _logger?.Debug($"equipment.compat.validation.done valid={result.IsValid} errors={result.Errors.Count} warnings={result.Warnings.Count}");
        return result;
    }

    public async Task<InventoryValidationResult> ValidateArmorSizeFitAsync(InventoryItemInstanceState armorItem, string characterSizeCategory, int characterHeightCm, string ruleSetId, bool strictMode)
    {
        var result = CreateResult("armor_size_fit");
        var armor = await ResolveArmorOrIssue(armorItem, ruleSetId, result, strictMode);
        if (armor == null) return result;

        if (string.IsNullOrWhiteSpace(characterSizeCategory))
        {
            AddWarning(result, "armor_size_fit_unknown", "Character size category is empty; armor size fit was not fully checked.", armorItem.ItemInstanceId, armorItem.DefinitionId);
        }

        if (characterHeightCm <= 0)
        {
            AddWarning(result, "armor_size_fit_unknown", "Character height is empty or invalid; armor height fit was not fully checked.", armorItem.ItemInstanceId, armorItem.DefinitionId);
        }

        var allowed = armor.SizeCategoryAllowed ?? new List<string>();
        if (allowed.Count > 0 && !string.IsNullOrWhiteSpace(characterSizeCategory) && !allowed.Contains(characterSizeCategory, StringComparer.OrdinalIgnoreCase))
        {
            AddError(result, "armor_size_category_mismatch", "Armor sizeCategoryAllowed does not include character size category.", armorItem.ItemInstanceId, armorItem.DefinitionId);
        }

        if (string.Equals(armor.HeightFitMode, "requires_size_fit", StringComparison.OrdinalIgnoreCase) && allowed.Count == 0)
        {
            AddByMode(result, strictMode, "armor_size_fit_unknown", "Armor requires size fit but has no sizeCategoryAllowed metadata.", armorItem.ItemInstanceId, armorItem.DefinitionId);
        }

        return result;
    }

    private async Task<WeaponDefinitionView?> ResolveWeaponOrIssue(InventoryItemInstanceState item, string ruleSetId, InventoryValidationResult result, bool strictMode)
    {
        if (item == null)
        {
            AddError(result, "weapon_definition_missing", "Weapon item is null.", string.Empty, string.Empty);
            return null;
        }

        if (string.IsNullOrWhiteSpace(item.DefinitionId))
        {
            AddByMode(result, strictMode, "weapon_definition_missing", "Weapon item DefinitionId is missing.", item.ItemInstanceId, item.DefinitionId);
            return null;
        }

        var resolved = await _definitionResolver.ResolveWeaponAsync(item.DefinitionId, ruleSetId);
        if (!resolved.Success || resolved.Value == null)
        {
            AddByMode(result, strictMode, "weapon_definition_missing", $"Weapon definition '{item.DefinitionId}' was not resolved.", item.ItemInstanceId, item.DefinitionId);
            AddWarnings(result, resolved.Warnings, item.ItemInstanceId, item.DefinitionId);
            return null;
        }

        AddWarnings(result, resolved.Warnings, item.ItemInstanceId, item.DefinitionId);
        return resolved.Value;
    }

    private async Task<ArmorDefinitionView?> ResolveArmorOrIssue(InventoryItemInstanceState item, string ruleSetId, InventoryValidationResult result, bool strictMode)
    {
        if (item == null)
        {
            AddError(result, "armor_definition_missing", "Armor item is null.", string.Empty, string.Empty);
            return null;
        }

        if (string.IsNullOrWhiteSpace(item.DefinitionId))
        {
            AddByMode(result, strictMode, "armor_definition_missing", "Armor item DefinitionId is missing.", item.ItemInstanceId, item.DefinitionId);
            return null;
        }

        var resolved = await _definitionResolver.ResolveArmorAsync(item.DefinitionId, ruleSetId);
        if (!resolved.Success || resolved.Value == null)
        {
            AddByMode(result, strictMode, "armor_definition_missing", $"Armor definition '{item.DefinitionId}' was not resolved.", item.ItemInstanceId, item.DefinitionId);
            AddWarnings(result, resolved.Warnings, item.ItemInstanceId, item.DefinitionId);
            return null;
        }

        AddWarnings(result, resolved.Warnings, item.ItemInstanceId, item.DefinitionId);
        return resolved.Value;
    }

    private async Task<AmmoDefinitionView?> ResolveAmmoOrIssue(InventoryItemInstanceState item, string ruleSetId, InventoryValidationResult result, bool strictMode)
    {
        if (item == null)
        {
            AddError(result, "ammo_definition_missing", "Ammo item is null.", string.Empty, string.Empty);
            return null;
        }

        if (string.IsNullOrWhiteSpace(item.DefinitionId))
        {
            AddByMode(result, strictMode, "ammo_definition_missing", "Ammo item DefinitionId is missing.", item.ItemInstanceId, item.DefinitionId);
            return null;
        }

        var resolved = await _definitionResolver.ResolveAmmoAsync(item.DefinitionId, ruleSetId);
        if (!resolved.Success || resolved.Value == null)
        {
            AddByMode(result, strictMode, "ammo_definition_missing", $"Ammo definition '{item.DefinitionId}' was not resolved.", item.ItemInstanceId, item.DefinitionId);
            AddWarnings(result, resolved.Warnings, item.ItemInstanceId, item.DefinitionId);
            return null;
        }

        AddWarnings(result, resolved.Warnings, item.ItemInstanceId, item.DefinitionId);
        return resolved.Value;
    }

    private static bool IsShield(ArmorDefinitionView armor)
    {
        return string.Equals(armor.ArmorType, "shield", StringComparison.OrdinalIgnoreCase)
            || (armor.Tags ?? new List<string>()).Any(x => string.Equals(x, "shield", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsMeleeWeapon(WeaponDefinitionView weapon)
    {
        var rangeType = weapon.RangeType ?? string.Empty;
        return rangeType.IndexOf("melee", StringComparison.OrdinalIgnoreCase) >= 0
            && rangeType.IndexOf("ranged", StringComparison.OrdinalIgnoreCase) < 0
            && rangeType.IndexOf("thrown", StringComparison.OrdinalIgnoreCase) < 0;
    }

    private static bool IsEmptyAmmo(AmmoDefinitionView ammo)
    {
        return string.IsNullOrWhiteSpace(ammo.DefinitionId);
    }

    private static InventoryValidationResult CreateResult(string section)
    {
        return new InventoryValidationResult { Section = section ?? string.Empty, CheckedAtUtc = DateTime.UtcNow };
    }

    private static void AddByMode(InventoryValidationResult result, bool strictMode, string code, string message, string itemInstanceId, string definitionId)
    {
        if (strictMode) AddError(result, code, message, itemInstanceId, definitionId);
        else AddWarning(result, code, message, itemInstanceId, definitionId);
    }

    private static void AddWarnings(InventoryValidationResult result, IEnumerable<string> warnings, string itemInstanceId, string definitionId)
    {
        foreach (var warning in warnings ?? Enumerable.Empty<string>())
        {
            AddWarning(result, warning, warning, itemInstanceId, definitionId);
        }
    }

    private static void AddError(InventoryValidationResult result, string code, string message, string itemInstanceId, string definitionId)
    {
        result.IsValid = false;
        result.Errors.Add(code ?? string.Empty);
        result.Issues.Add(new InventoryValidationIssue
        {
            Code = code ?? string.Empty,
            Severity = "error",
            Message = message ?? string.Empty,
            ItemInstanceId = itemInstanceId ?? string.Empty,
            DefinitionId = definitionId ?? string.Empty
        });
    }

    private static void AddWarning(InventoryValidationResult result, string code, string message, string itemInstanceId, string definitionId)
    {
        result.Warnings.Add(code ?? string.Empty);
        result.Issues.Add(new InventoryValidationIssue
        {
            Code = code ?? string.Empty,
            Severity = "warning",
            Message = message ?? string.Empty,
            ItemInstanceId = itemInstanceId ?? string.Empty,
            DefinitionId = definitionId ?? string.Empty
        });
    }

    private static void Merge(InventoryValidationResult target, InventoryValidationResult source)
    {
        if (source == null) return;
        if (!source.IsValid) target.IsValid = false;
        target.Errors.AddRange(source.Errors ?? new List<string>());
        target.Warnings.AddRange(source.Warnings ?? new List<string>());
        target.Issues.AddRange(source.Issues ?? new List<InventoryValidationIssue>());
    }

    private static void Merge(InventoryValidationResult target, EquipmentValidationResult source)
    {
        if (source == null) return;
        if (!source.IsValid) target.IsValid = false;
        foreach (var error in source.Errors ?? new List<InventoryValidationIssue>())
        {
            target.Errors.Add(error.Code ?? string.Empty);
            target.Issues.Add(error);
        }

        foreach (var warning in source.Warnings ?? new List<InventoryValidationIssue>())
        {
            target.Warnings.Add(warning.Code ?? string.Empty);
            target.Issues.Add(warning);
        }
    }
}
