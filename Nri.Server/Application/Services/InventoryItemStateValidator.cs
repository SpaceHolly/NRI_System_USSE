using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nri.Server.Logging;
using Nri.Shared.Domain;

namespace Nri.Server.Application.Services;

public interface IInventoryItemStateValidator
{
    Task<InventoryValidationResult> ValidateItemAsync(InventoryItemInstanceState item, string ruleSetId, bool strictMode);
    Task<InventoryValidationResult> ValidateItemsAsync(IEnumerable<InventoryItemInstanceState> items, string ruleSetId, bool strictMode);
    Task<InventoryValidationResult> ValidateQuantityAsync(InventoryItemInstanceState item, string ruleSetId, bool strictMode);
    Task<InventoryValidationResult> ValidateStackAsync(InventoryItemInstanceState item, string ruleSetId, bool strictMode);
    Task<InventoryValidationResult> ValidateDurabilityAsync(InventoryItemInstanceState item, string ruleSetId, bool strictMode);
    Task<InventoryValidationResult> ValidateDuplicateStacksAsync(IEnumerable<InventoryItemInstanceState> items, string ruleSetId, bool strictMode);
}

public sealed class InventoryItemStateValidator : IInventoryItemStateValidator
{
    private readonly IItemEquipmentDefinitionResolver _definitionResolver;
    private readonly IServerLogger? _logger;

    public InventoryItemStateValidator(IItemEquipmentDefinitionResolver definitionResolver, IServerLogger? logger = null)
    {
        _definitionResolver = definitionResolver ?? throw new ArgumentNullException(nameof(definitionResolver));
        _logger = logger;
    }

    public async Task<InventoryValidationResult> ValidateItemAsync(InventoryItemInstanceState item, string ruleSetId, bool strictMode)
    {
        var result = CreateResult("inventory_item_state");
        if (item == null)
        {
            AddError(result, "item_null", "Inventory item instance is null.", string.Empty, string.Empty);
            return result;
        }

        ValidateIdentity(item, result);
        Merge(result, await ValidateQuantityAsync(item, ruleSetId, strictMode));
        Merge(result, await ValidateStackAsync(item, ruleSetId, strictMode));
        Merge(result, await ValidateDurabilityAsync(item, ruleSetId, strictMode));
        return result;
    }

    public async Task<InventoryValidationResult> ValidateItemsAsync(IEnumerable<InventoryItemInstanceState> items, string ruleSetId, bool strictMode)
    {
        var list = (items ?? Enumerable.Empty<InventoryItemInstanceState>()).Where(x => x != null).ToList();
        _logger?.Debug($"inventory.item.validation.start itemCount={list.Count}");

        var result = CreateResult("inventory_items");
        foreach (var item in list)
        {
            Merge(result, await ValidateItemAsync(item, ruleSetId, strictMode));
        }

        foreach (var duplicate in list
            .Where(x => !string.IsNullOrWhiteSpace(x.ItemInstanceId))
            .GroupBy(x => x.ItemInstanceId.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(x => x.Count() > 1))
        {
            AddError(result, "duplicate_item_instance_id", $"Duplicate ItemInstanceId '{duplicate.Key}'.", duplicate.Key, string.Empty);
        }

        Merge(result, await ValidateDuplicateStacksAsync(list, ruleSetId, strictMode));
        _logger?.Debug($"inventory.item.validation.done valid={result.IsValid} errors={result.Errors.Count} warnings={result.Warnings.Count}");
        return result;
    }

    public Task<InventoryValidationResult> ValidateQuantityAsync(InventoryItemInstanceState item, string ruleSetId, bool strictMode)
    {
        var result = CreateResult("quantity");
        if (item == null)
        {
            AddError(result, "item_null", "Inventory item instance is null.", string.Empty, string.Empty);
            return Task.FromResult(result);
        }

        if (item.Quantity < 0)
        {
            AddError(result, "item_quantity_negative", "Quantity must be greater than or equal to zero.", item.ItemInstanceId, item.DefinitionId);
        }
        else if (item.Quantity == 0)
        {
            AddByMode(result, strictMode, "item_quantity_zero", "Quantity is zero; item still exists and was not removed automatically.", item.ItemInstanceId, item.DefinitionId);
        }

        if (item.IsEquipped && item.Quantity < 1)
        {
            AddError(result, "equipped_item_quantity_invalid", "Equipped item must have Quantity >= 1.", item.ItemInstanceId, item.DefinitionId);
        }

        return Task.FromResult(result);
    }

    public async Task<InventoryValidationResult> ValidateStackAsync(InventoryItemInstanceState item, string ruleSetId, bool strictMode)
    {
        var result = CreateResult("stack");
        if (item == null)
        {
            AddError(result, "item_null", "Inventory item instance is null.", string.Empty, string.Empty);
            return result;
        }

        var definition = await ResolveDefinitionForItemAsync(item, ruleSetId);
        AddWarnings(result, definition.Warnings, item.ItemInstanceId, item.DefinitionId);

        var effectiveStackable = item.Stackable;
        var effectiveMaxStack = item.MaxStack;
        if (definition.Snapshot != null)
        {
            ValidateDefinitionStackMismatch(item, definition.Snapshot, result, strictMode);
            effectiveStackable = definition.Snapshot.Stackable;
            if (definition.Snapshot.MaxStack > 0) effectiveMaxStack = definition.Snapshot.MaxStack;
            if (item.MaxStack <= 0 && definition.Snapshot.MaxStack > 0)
            {
                AddWarning(result, "item_max_stack_missing_but_definition_has_value", "Item MaxStack is missing or invalid while definition has MaxStack.", item.ItemInstanceId, item.DefinitionId);
            }
        }

        if (effectiveMaxStack <= 0)
        {
            AddByMode(result, strictMode, "max_stack_invalid", "MaxStack is invalid; stack size treated as unknown.", item.ItemInstanceId, item.DefinitionId);
            effectiveMaxStack = 0;
        }

        if (!effectiveStackable && item.Quantity > 1)
        {
            AddByMode(result, strictMode, "non_stackable_quantity_exceeds_one", "Non-stackable item has Quantity greater than one.", item.ItemInstanceId, item.DefinitionId);
        }

        if (effectiveStackable && effectiveMaxStack > 0 && item.Quantity > effectiveMaxStack)
        {
            AddError(result, "stack_quantity_exceeds_max", "Quantity exceeds MaxStack.", item.ItemInstanceId, item.DefinitionId);
        }

        if (IsAmmo(item, definition.Snapshot))
        {
            ValidateAmmoStack(item, definition.Snapshot, result, strictMode);
        }

        if (definition.Snapshot != null && definition.Snapshot.IsConsumable)
        {
            ValidateConsumableStack(item, definition.Snapshot, result, strictMode);
        }

        return result;
    }

    public Task<InventoryValidationResult> ValidateDurabilityAsync(InventoryItemInstanceState item, string ruleSetId, bool strictMode)
    {
        var result = CreateResult("durability");
        if (item == null)
        {
            AddError(result, "item_null", "Inventory item instance is null.", string.Empty, string.Empty);
            return Task.FromResult(result);
        }

        if (item.Durability < 0)
        {
            AddError(result, "durability_negative", "Durability must be greater than or equal to zero.", item.ItemInstanceId, item.DefinitionId);
        }

        if (item.MaxDurability < 0)
        {
            AddError(result, "max_durability_invalid", "MaxDurability must be greater than or equal to zero.", item.ItemInstanceId, item.DefinitionId);
        }

        if (item.MaxDurability > 0 && item.Durability > item.MaxDurability)
        {
            AddError(result, "durability_exceeds_max", "Durability must not exceed MaxDurability.", item.ItemInstanceId, item.DefinitionId);
        }

        if (item.MaxDurability == 0 && item.Durability > 0)
        {
            AddWarning(result, "durability_without_max", "Durability is set while MaxDurability is zero; durability system is treated as unknown/not applicable.", item.ItemInstanceId, item.DefinitionId);
        }

        if (item.MaxDurability > 0 && item.Durability == 0)
        {
            AddWarning(result, "item_broken_or_zero_durability", "Item has zero Durability and positive MaxDurability; treat as broken/destroyed candidate.", item.ItemInstanceId, item.DefinitionId);
            if (item.IsEquipped)
            {
                AddByMode(result, strictMode, "equipped_broken_item", "Equipped item has zero Durability.", item.ItemInstanceId, item.DefinitionId);
            }
        }

        return Task.FromResult(result);
    }

    public Task<InventoryValidationResult> ValidateDuplicateStacksAsync(IEnumerable<InventoryItemInstanceState> items, string ruleSetId, bool strictMode)
    {
        var result = CreateResult("duplicate_stacks");
        var list = (items ?? Enumerable.Empty<InventoryItemInstanceState>())
            .Where(x => x != null && !x.IsEquipped && x.Stackable && !string.IsNullOrWhiteSpace(x.DefinitionId))
            .ToList();

        foreach (var group in list
            .GroupBy(x => BuildStackMergeKey(x), StringComparer.OrdinalIgnoreCase)
            .Where(x => x.Count() > 1))
        {
            var first = group.First();
            AddWarning(result, "stackable_items_can_be_merged", $"Multiple stackable items share merge key '{group.Key}'.", first.ItemInstanceId, first.DefinitionId);
        }

        return Task.FromResult(result);
    }

    private async Task<InventoryDefinitionSnapshotResult> ResolveDefinitionForItemAsync(InventoryItemInstanceState item, string ruleSetId)
    {
        var result = new InventoryDefinitionSnapshotResult();
        var definitionId = item?.DefinitionId ?? string.Empty;
        if (string.IsNullOrWhiteSpace(definitionId))
        {
            result.Warnings.Add("item_definition_missing");
            return result;
        }

        foreach (var category in GetCategorySearchOrder(item))
        {
            if (string.Equals(category, DefinitionCategoryIds.Ammo, StringComparison.OrdinalIgnoreCase))
            {
                var ammo = await _definitionResolver.ResolveAmmoAsync(definitionId, ruleSetId);
                if (ammo.Success)
                {
                    result.Snapshot = InventoryDefinitionSnapshot.FromAmmo(ammo.Value);
                    return result;
                }
            }
            else if (string.Equals(category, DefinitionCategoryIds.Item, StringComparison.OrdinalIgnoreCase))
            {
                var generic = await _definitionResolver.ResolveItemAsync(definitionId, ruleSetId);
                if (generic.Success)
                {
                    result.Snapshot = InventoryDefinitionSnapshot.FromItem(generic.Value);
                    return result;
                }
            }
            else if (string.Equals(category, DefinitionCategoryIds.Weapon, StringComparison.OrdinalIgnoreCase))
            {
                var weapon = await _definitionResolver.ResolveWeaponAsync(definitionId, ruleSetId);
                if (weapon.Success)
                {
                    result.Snapshot = InventoryDefinitionSnapshot.FromWeapon(weapon.Value);
                    return result;
                }
            }
            else if (string.Equals(category, DefinitionCategoryIds.Armor, StringComparison.OrdinalIgnoreCase))
            {
                var armor = await _definitionResolver.ResolveArmorAsync(definitionId, ruleSetId);
                if (armor.Success)
                {
                    result.Snapshot = InventoryDefinitionSnapshot.FromArmor(armor.Value);
                    return result;
                }
            }
        }

        result.Warnings.Add("item_definition_missing");
        return result;
    }

    private static void ValidateIdentity(InventoryItemInstanceState item, InventoryValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(item.ItemInstanceId)) AddError(result, "item_id_missing", "ItemInstanceId is required.", item.ItemInstanceId, item.DefinitionId);
        if (string.IsNullOrWhiteSpace(item.DefinitionId) && string.IsNullOrWhiteSpace(item.ItemCode)) AddError(result, "item_definition_missing", "DefinitionId or ItemCode is required.", item.ItemInstanceId, item.DefinitionId);
        else if (string.IsNullOrWhiteSpace(item.DefinitionId) && !string.IsNullOrWhiteSpace(item.ItemCode)) AddWarning(result, "item_definition_missing", "DefinitionId is missing; ItemCode is present as legacy/fallback identity.", item.ItemInstanceId, item.DefinitionId);
        if (string.IsNullOrWhiteSpace(item.DisplayName)) AddWarning(result, "item_display_name_missing", "DisplayName is empty.", item.ItemInstanceId, item.DefinitionId);
        if (item.Tags == null) AddError(result, "item_tags_null", "Tags collection must not be null.", item.ItemInstanceId, item.DefinitionId);
    }

    private static void ValidateDefinitionStackMismatch(InventoryItemInstanceState item, InventoryDefinitionSnapshot definition, InventoryValidationResult result, bool strictMode)
    {
        if (definition == null) return;
        if (definition.Stackable && !item.Stackable)
        {
            AddWarning(result, "stackable_mismatch_definition", "Definition is stackable but item instance is not marked stackable.", item.ItemInstanceId, item.DefinitionId);
        }
        else if (!definition.Stackable && item.Stackable)
        {
            AddByMode(result, strictMode, "stackable_mismatch_definition", "Definition is not stackable but item instance is marked stackable.", item.ItemInstanceId, item.DefinitionId);
        }
    }

    private static void ValidateAmmoStack(InventoryItemInstanceState item, InventoryDefinitionSnapshot? definition, InventoryValidationResult result, bool strictMode)
    {
        if (!item.Stackable && (definition == null || !definition.Stackable))
        {
            AddWarning(result, "ammo_stack_warning", "Ammo should generally be stackable.", item.ItemInstanceId, item.DefinitionId);
        }

        var maxStack = definition?.MaxStack > 0 ? definition.MaxStack : item.MaxStack;
        if (maxStack < 1)
        {
            AddByMode(result, strictMode, "ammo_stack_warning", "Ammo MaxStack should be at least one.", item.ItemInstanceId, item.DefinitionId);
        }
    }

    private static void ValidateConsumableStack(InventoryItemInstanceState item, InventoryDefinitionSnapshot definition, InventoryValidationResult result, bool strictMode)
    {
        if (!definition.Stackable && item.Quantity > 1)
        {
            AddByMode(result, strictMode, "consumable_stack_warning", "Consumable non-stackable item has Quantity greater than one.", item.ItemInstanceId, item.DefinitionId);
        }
    }

    private static bool IsAmmo(InventoryItemInstanceState item, InventoryDefinitionSnapshot? definition)
    {
        return string.Equals(item?.ItemType, "ammo", StringComparison.OrdinalIgnoreCase)
            || string.Equals(definition?.Category, DefinitionCategoryIds.Ammo, StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> GetCategorySearchOrder(InventoryItemInstanceState? item)
    {
        var hint = (item?.ItemType ?? string.Empty).Trim();
        if (string.Equals(hint, "ammo", StringComparison.OrdinalIgnoreCase) || string.Equals(hint, "ammunition", StringComparison.OrdinalIgnoreCase)) return new[] { DefinitionCategoryIds.Ammo, DefinitionCategoryIds.Item, DefinitionCategoryIds.Weapon, DefinitionCategoryIds.Armor };
        if (string.Equals(hint, "weapon", StringComparison.OrdinalIgnoreCase)) return new[] { DefinitionCategoryIds.Weapon, DefinitionCategoryIds.Item, DefinitionCategoryIds.Ammo, DefinitionCategoryIds.Armor };
        if (string.Equals(hint, "armor", StringComparison.OrdinalIgnoreCase) || string.Equals(hint, "shield", StringComparison.OrdinalIgnoreCase)) return new[] { DefinitionCategoryIds.Armor, DefinitionCategoryIds.Item, DefinitionCategoryIds.Weapon, DefinitionCategoryIds.Ammo };
        if (string.Equals(hint, "item", StringComparison.OrdinalIgnoreCase) || string.Equals(hint, "consumable", StringComparison.OrdinalIgnoreCase) || string.Equals(hint, "tool", StringComparison.OrdinalIgnoreCase)) return new[] { DefinitionCategoryIds.Item, DefinitionCategoryIds.Ammo, DefinitionCategoryIds.Weapon, DefinitionCategoryIds.Armor };
        return new[] { DefinitionCategoryIds.Item, DefinitionCategoryIds.Ammo, DefinitionCategoryIds.Weapon, DefinitionCategoryIds.Armor };
    }

    private static string BuildStackMergeKey(InventoryItemInstanceState item)
    {
        var tags = item.Tags == null
            ? string.Empty
            : string.Join(",", item.Tags.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
        return string.Join("|", item.DefinitionId.Trim(), item.ContainerId ?? string.Empty, tags);
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

    private sealed class InventoryDefinitionSnapshotResult
    {
        public InventoryDefinitionSnapshot? Snapshot { get; set; }
        public List<string> Warnings { get; } = new List<string>();
    }

    private sealed class InventoryDefinitionSnapshot
    {
        public string Category { get; set; } = string.Empty;
        public string DefinitionId { get; set; } = string.Empty;
        public bool Stackable { get; set; }
        public int MaxStack { get; set; }
        public bool IsConsumable { get; set; }

        public static InventoryDefinitionSnapshot FromItem(ItemDefinitionView view)
        {
            return new InventoryDefinitionSnapshot
            {
                Category = DefinitionCategoryIds.Item,
                DefinitionId = view?.DefinitionId ?? string.Empty,
                Stackable = view?.Stackable ?? false,
                MaxStack = view?.MaxStack ?? 0,
                IsConsumable = view?.IsConsumable ?? false
            };
        }

        public static InventoryDefinitionSnapshot FromAmmo(AmmoDefinitionView view)
        {
            return new InventoryDefinitionSnapshot
            {
                Category = DefinitionCategoryIds.Ammo,
                DefinitionId = view?.DefinitionId ?? string.Empty,
                Stackable = view?.Stackable ?? true,
                MaxStack = view?.MaxStack ?? 0,
                IsConsumable = view?.IsConsumable ?? true
            };
        }

        public static InventoryDefinitionSnapshot FromWeapon(WeaponDefinitionView view)
        {
            return new InventoryDefinitionSnapshot
            {
                Category = DefinitionCategoryIds.Weapon,
                DefinitionId = view?.DefinitionId ?? string.Empty,
                Stackable = false,
                MaxStack = 1,
                IsConsumable = false
            };
        }

        public static InventoryDefinitionSnapshot FromArmor(ArmorDefinitionView view)
        {
            return new InventoryDefinitionSnapshot
            {
                Category = DefinitionCategoryIds.Armor,
                DefinitionId = view?.DefinitionId ?? string.Empty,
                Stackable = false,
                MaxStack = 1,
                IsConsumable = false
            };
        }
    }
}
