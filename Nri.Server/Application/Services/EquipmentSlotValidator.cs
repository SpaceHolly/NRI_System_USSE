using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nri.Server.Logging;
using Nri.Shared.Domain;

namespace Nri.Server.Application.Services;

public interface IEquipmentSlotValidator
{
    Task<EquipmentValidationResult> ValidateLoadoutAsync(EquipmentValidationRequest request);
    Task<EquipmentValidationResult> ValidateSlotAssignmentAsync(EquipmentSlotAssignmentState assignment, EquipmentValidationRequest request);
    Task<EquipmentValidationResult> ValidateItemCanUseSlotAsync(InventoryItemInstanceState item, string equipmentSlotId, string ruleSetId);
    EquipmentValidationResult ValidateTwoHandedConflicts(EquipmentLoadoutState loadout, IEnumerable<InventoryItemInstanceState> items);
    EquipmentValidationResult ValidateSlotCapacity(EquipmentLoadoutState loadout, IEnumerable<EquipmentSlotDefinitionView> slotDefinitions);
}

public sealed class EquipmentSlotValidator : IEquipmentSlotValidator
{
    private readonly IItemEquipmentDefinitionResolver _definitionResolver;
    private readonly IServerLogger? _logger;

    public EquipmentSlotValidator(IItemEquipmentDefinitionResolver definitionResolver, IServerLogger? logger = null)
    {
        _definitionResolver = definitionResolver ?? throw new ArgumentNullException(nameof(definitionResolver));
        _logger = logger;
    }

    public async Task<EquipmentValidationResult> ValidateLoadoutAsync(EquipmentValidationRequest request)
    {
        var result = CreateResult();
        if (request == null)
        {
            AddError(result, "equipment_request_null", "Equipment validation request is null.", string.Empty, string.Empty);
            return result;
        }

        _logger?.Debug($"equipment.validation.start characterId={request.CharacterId} itemCount={(request.Items == null ? 0 : request.Items.Count)}");

        if (request.Items == null) AddError(result, "items_null", "Items collection must not be null.", string.Empty, string.Empty);
        if (request.Loadout == null)
        {
            AddError(result, "loadout_null", "Equipment loadout is null.", string.Empty, string.Empty);
            FinishLog(request, result);
            return result;
        }

        if (request.Loadout.SlotAssignments == null)
        {
            AddError(result, "slot_assignments_null", "SlotAssignments collection must not be null.", string.Empty, string.Empty);
            FinishLog(request, result);
            return result;
        }

        foreach (var assignment in request.Loadout.SlotAssignments.Where(x => x != null))
        {
            Merge(result, await ValidateSlotAssignmentAsync(assignment, request));
        }

        var slotDefinitions = new List<EquipmentSlotDefinitionView>();
        foreach (var slotId in request.Loadout.SlotAssignments
            .Where(x => x != null && !string.IsNullOrWhiteSpace(x.EquipmentSlotId))
            .Select(x => x.EquipmentSlotId.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var slot = await _definitionResolver.ResolveEquipmentSlotAsync(slotId, request.RuleSetId);
            if (slot.Success && slot.Value != null) slotDefinitions.Add(slot.Value);
        }

        Merge(result, ValidateSlotCapacity(request.Loadout, slotDefinitions));
        Merge(result, ValidateTwoHandedConflicts(request.Loadout, request.Items ?? new List<InventoryItemInstanceState>()));

        FinishLog(request, result);
        return result;
    }

    public async Task<EquipmentValidationResult> ValidateSlotAssignmentAsync(EquipmentSlotAssignmentState assignment, EquipmentValidationRequest request)
    {
        var result = CreateResult();
        if (assignment == null)
        {
            AddError(result, "slot_assignment_null", "Slot assignment is null.", string.Empty, string.Empty);
            return result;
        }

        var slotId = assignment.EquipmentSlotId ?? string.Empty;
        var itemInstanceId = assignment.ItemInstanceId ?? string.Empty;
        if (string.IsNullOrWhiteSpace(slotId)) AddError(result, "equipment_slot_id_required", "EquipmentSlotId is required.", itemInstanceId, assignment.DefinitionId);
        if (string.IsNullOrWhiteSpace(itemInstanceId)) AddError(result, "item_instance_id_required", "ItemInstanceId is required.", itemInstanceId, assignment.DefinitionId);
        if (string.IsNullOrWhiteSpace(slotId) || string.IsNullOrWhiteSpace(itemInstanceId)) return result;

        var item = (request?.Items ?? new List<InventoryItemInstanceState>())
            .FirstOrDefault(x => x != null && string.Equals(x.ItemInstanceId, itemInstanceId, StringComparison.OrdinalIgnoreCase));
        if (item == null)
        {
            AddError(result, "slot_assignment_item_missing", "Slot assignment points to an item that is not present in request.Items.", itemInstanceId, assignment.DefinitionId);
            return result;
        }

        var slot = await _definitionResolver.ResolveEquipmentSlotAsync(slotId, request?.RuleSetId ?? string.Empty);
        if (!slot.Success)
        {
            AddError(result, "equipment_slot_not_found", $"Equipment slot '{slotId}' was not resolved.", item.ItemInstanceId, FirstNonEmpty(item.DefinitionId, assignment.DefinitionId));
            AddWarnings(result, slot.Warnings, item.ItemInstanceId, FirstNonEmpty(item.DefinitionId, assignment.DefinitionId));
            _logger?.Debug($"equipment.validation.slot_conflict slotId={slotId}");
            return result;
        }

        if (string.IsNullOrWhiteSpace(item.DefinitionId) && string.IsNullOrWhiteSpace(assignment.DefinitionId))
        {
            AddDefinitionMissing(result, request?.StrictMode ?? false, "item_definition_id_required", "Equipped item should have DefinitionId.", item.ItemInstanceId, string.Empty);
            return result;
        }

        Merge(result, await ValidateItemCanUseSlotCoreAsync(item, slotId, request?.RuleSetId ?? string.Empty, request?.StrictMode ?? false));
        return result;
    }

    public async Task<EquipmentValidationResult> ValidateItemCanUseSlotAsync(InventoryItemInstanceState item, string equipmentSlotId, string ruleSetId)
    {
        return await ValidateItemCanUseSlotCoreAsync(item, equipmentSlotId, ruleSetId, strictMode: false);
    }

    private async Task<EquipmentValidationResult> ValidateItemCanUseSlotCoreAsync(InventoryItemInstanceState item, string equipmentSlotId, string ruleSetId, bool strictMode)
    {
        var result = CreateResult();
        if (item == null)
        {
            AddError(result, "item_null", "Inventory item instance is null.", string.Empty, string.Empty);
            return result;
        }

        if (string.IsNullOrWhiteSpace(equipmentSlotId))
        {
            AddError(result, "equipment_slot_id_required", "EquipmentSlotId is required.", item.ItemInstanceId, item.DefinitionId);
            return result;
        }

        var slot = await _definitionResolver.ResolveEquipmentSlotAsync(equipmentSlotId, ruleSetId);
        if (!slot.Success)
        {
            AddError(result, "equipment_slot_not_found", $"Equipment slot '{equipmentSlotId}' was not resolved.", item.ItemInstanceId, item.DefinitionId);
            AddWarnings(result, slot.Warnings, item.ItemInstanceId, item.DefinitionId);
            return result;
        }

        var definition = await ResolveDefinitionForItemAsync(item, ruleSetId);
        if (definition.Snapshot == null)
        {
            AddDefinitionMissing(result, strictMode, "item_definition_missing", $"Item definition '{item.DefinitionId}' was not resolved.", item.ItemInstanceId, item.DefinitionId);
            AddWarnings(result, definition.Warnings, item.ItemInstanceId, item.DefinitionId);
            _logger?.Debug($"equipment.validation.missing_definition itemInstanceId={item.ItemInstanceId} definitionId={item.DefinitionId}");
            return result;
        }

        AddWarnings(result, definition.Warnings, item.ItemInstanceId, item.DefinitionId);
        var allowedSlots = definition.Snapshot.EquipmentSlotIds;
        if (allowedSlots.Count == 0)
        {
            AddWarning(result, "item_has_no_equipment_slots", "Definition does not declare equipmentSlotIds; item should not be equipped without explicit slot metadata.", item.ItemInstanceId, item.DefinitionId);
            return result;
        }

        if (!allowedSlots.Contains(equipmentSlotId, StringComparer.OrdinalIgnoreCase))
        {
            AddError(result, "item_slot_not_allowed", $"Definition '{item.DefinitionId}' cannot use equipment slot '{equipmentSlotId}'.", item.ItemInstanceId, item.DefinitionId);
        }

        if (definition.Snapshot.IsConsumable && allowedSlots.Count == 0)
        {
            AddWarning(result, "consumable_equipped_without_slot", "Consumable item should not be equipped without explicit slot metadata.", item.ItemInstanceId, item.DefinitionId);
        }

        if (definition.Snapshot.Kind == "armor")
        {
            ValidateArmorSlotCompatibility(result, item, definition.Snapshot, equipmentSlotId, slot.Value);
        }

        if (definition.Snapshot.IsShield)
        {
            ValidateShieldSlotCompatibility(result, item, definition.Snapshot, equipmentSlotId);
        }

        return result;
    }

    public EquipmentValidationResult ValidateTwoHandedConflicts(EquipmentLoadoutState loadout, IEnumerable<InventoryItemInstanceState> items)
    {
        var result = CreateResult();
        if (loadout == null) return result;

        var assignments = (loadout.SlotAssignments ?? new List<EquipmentSlotAssignmentState>())
            .Where(x => x != null)
            .ToList();
        var itemById = (items ?? Enumerable.Empty<InventoryItemInstanceState>())
            .Where(x => x != null && !string.IsNullOrWhiteSpace(x.ItemInstanceId))
            .GroupBy(x => x.ItemInstanceId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

        var twoHandedAssignments = assignments.Where(x => IsSlot(x.EquipmentSlotId, "two_handed")).ToList();
        var mainHandAssignments = assignments.Where(x => IsSlot(x.EquipmentSlotId, "main_hand")).ToList();
        var offHandAssignments = assignments.Where(x => IsSlot(x.EquipmentSlotId, "off_hand")).ToList();

        if (twoHandedAssignments.Count > 0 && mainHandAssignments.Count > 0)
        {
            AddError(result, "two_handed_conflicts_main_hand", "Two-handed slot conflicts with occupied main_hand.", twoHandedAssignments[0].ItemInstanceId, twoHandedAssignments[0].DefinitionId);
        }

        if (twoHandedAssignments.Count > 0 && offHandAssignments.Count > 0)
        {
            AddError(result, "two_handed_conflicts_off_hand", "Two-handed slot conflicts with occupied off_hand or shield.", twoHandedAssignments[0].ItemInstanceId, twoHandedAssignments[0].DefinitionId);
        }

        if (!string.IsNullOrWhiteSpace(loadout.TwoHandedItemInstanceId)
            && (!string.IsNullOrWhiteSpace(loadout.MainHandItemInstanceId) || !string.IsNullOrWhiteSpace(loadout.OffHandItemInstanceId)))
        {
            AddError(result, "two_handed_loadout_field_conflict", "TwoHandedItemInstanceId conflicts with MainHandItemInstanceId or OffHandItemInstanceId.", loadout.TwoHandedItemInstanceId, string.Empty);
        }

        foreach (var assignment in assignments)
        {
            if (!itemById.TryGetValue(assignment.ItemInstanceId ?? string.Empty, out var item)) continue;
            var handedness = item.Tags == null ? string.Empty : item.Tags.FirstOrDefault(x => string.Equals(x, "two_handed", StringComparison.OrdinalIgnoreCase)) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(handedness)) continue;
            if (!IsSlot(assignment.EquipmentSlotId, "two_handed"))
            {
                AddError(result, "two_handed_item_wrong_slot", "Item tagged as two_handed should use the two_handed slot.", item.ItemInstanceId, item.DefinitionId);
            }
        }

        if (result.Errors.Count > 0) _logger?.Debug($"equipment.validation.two_handed_conflict characterId={loadout.CharacterId}");
        return result;
    }

    public EquipmentValidationResult ValidateSlotCapacity(EquipmentLoadoutState loadout, IEnumerable<EquipmentSlotDefinitionView> slotDefinitions)
    {
        var result = CreateResult();
        if (loadout == null) return result;

        var definitions = (slotDefinitions ?? Enumerable.Empty<EquipmentSlotDefinitionView>())
            .Where(x => x != null && !string.IsNullOrWhiteSpace(x.DefinitionId))
            .GroupBy(x => x.DefinitionId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var group in (loadout.SlotAssignments ?? new List<EquipmentSlotAssignmentState>())
            .Where(x => x != null && !string.IsNullOrWhiteSpace(x.EquipmentSlotId))
            .GroupBy(x => x.EquipmentSlotId.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            var maxItems = 1;
            if (definitions.TryGetValue(group.Key, out var slot))
            {
                maxItems = slot.MaxItems <= 0 ? 1 : slot.MaxItems;
                if (slot.MaxItems <= 0) AddWarning(result, "slot_max_items_invalid_defaulted", $"Slot '{group.Key}' has MaxItems <= 0; validator treated it as 1.", string.Empty, string.Empty);
            }
            else
            {
                AddWarning(result, "slot_definition_missing_for_capacity", $"Slot definition '{group.Key}' was not available for capacity validation.", string.Empty, string.Empty);
            }

            if (group.Count() > maxItems)
            {
                AddError(result, "slot_capacity_exceeded", $"Slot '{group.Key}' has {group.Count()} assignments but allows {maxItems}.", string.Empty, string.Empty);
                _logger?.Debug($"equipment.validation.slot_conflict slotId={group.Key}");
            }
        }

        return result;
    }

    private async Task<EquipmentDefinitionLookupResult> ResolveDefinitionForItemAsync(InventoryItemInstanceState item, string ruleSetId)
    {
        var result = new EquipmentDefinitionLookupResult();
        var definitionId = item.DefinitionId ?? string.Empty;
        if (string.IsNullOrWhiteSpace(definitionId))
        {
            result.Warnings.Add("definition_id_missing");
            return result;
        }

        foreach (var category in GetCategorySearchOrder(item))
        {
            if (string.Equals(category, DefinitionCategoryIds.Weapon, StringComparison.OrdinalIgnoreCase))
            {
                var weapon = await _definitionResolver.ResolveWeaponAsync(definitionId, ruleSetId);
                if (weapon.Success)
                {
                    result.Snapshot = EquipmentDefinitionSnapshot.FromWeapon(weapon.Value);
                    return result;
                }
            }
            else if (string.Equals(category, DefinitionCategoryIds.Armor, StringComparison.OrdinalIgnoreCase))
            {
                var armor = await _definitionResolver.ResolveArmorAsync(definitionId, ruleSetId);
                if (armor.Success)
                {
                    result.Snapshot = EquipmentDefinitionSnapshot.FromArmor(armor.Value);
                    return result;
                }
            }
            else if (string.Equals(category, DefinitionCategoryIds.Item, StringComparison.OrdinalIgnoreCase))
            {
                var generic = await _definitionResolver.ResolveItemAsync(definitionId, ruleSetId);
                if (generic.Success)
                {
                    result.Snapshot = EquipmentDefinitionSnapshot.FromItem(generic.Value);
                    return result;
                }
            }
            else if (string.Equals(category, DefinitionCategoryIds.Ammo, StringComparison.OrdinalIgnoreCase))
            {
                var ammo = await _definitionResolver.ResolveAmmoAsync(definitionId, ruleSetId);
                if (ammo.Success)
                {
                    result.Snapshot = EquipmentDefinitionSnapshot.FromAmmo(ammo.Value);
                    return result;
                }
            }
        }

        result.Warnings.Add("definition_not_resolved");
        return result;
    }

    private static IEnumerable<string> GetCategorySearchOrder(InventoryItemInstanceState item)
    {
        var hint = (item.ItemType ?? string.Empty).Trim();
        if (MatchesAny(hint, "weapon")) return new[] { DefinitionCategoryIds.Weapon, DefinitionCategoryIds.Item, DefinitionCategoryIds.Armor, DefinitionCategoryIds.Ammo };
        if (MatchesAny(hint, "armor", "shield")) return new[] { DefinitionCategoryIds.Armor, DefinitionCategoryIds.Item, DefinitionCategoryIds.Weapon, DefinitionCategoryIds.Ammo };
        if (MatchesAny(hint, "ammo", "ammunition")) return new[] { DefinitionCategoryIds.Ammo, DefinitionCategoryIds.Item, DefinitionCategoryIds.Weapon, DefinitionCategoryIds.Armor };
        if (MatchesAny(hint, "item", "consumable", "tool")) return new[] { DefinitionCategoryIds.Item, DefinitionCategoryIds.Weapon, DefinitionCategoryIds.Armor, DefinitionCategoryIds.Ammo };
        return new[] { DefinitionCategoryIds.Weapon, DefinitionCategoryIds.Armor, DefinitionCategoryIds.Item, DefinitionCategoryIds.Ammo };
    }

    private static void ValidateArmorSlotCompatibility(EquipmentValidationResult result, InventoryItemInstanceState item, EquipmentDefinitionSnapshot snapshot, string slotId, EquipmentSlotDefinitionView slotDefinition)
    {
        if (snapshot.IsShield)
        {
            ValidateShieldSlotCompatibility(result, item, snapshot, slotId);
            return;
        }

        if (!slotDefinition.IsBodySlot)
        {
            AddError(result, "armor_requires_body_slot", "Armor should be assigned to a body slot.", item.ItemInstanceId, item.DefinitionId);
        }

        if (IsSlot(slotId, "accessory"))
        {
            AddError(result, "armor_in_accessory_slot", "Armor cannot be assigned to accessory slot unless explicitly modeled as accessory equipment.", item.ItemInstanceId, item.DefinitionId);
        }
    }

    private static void ValidateShieldSlotCompatibility(EquipmentValidationResult result, InventoryItemInstanceState item, EquipmentDefinitionSnapshot snapshot, string slotId)
    {
        if (!IsSlot(slotId, "off_hand") && !snapshot.EquipmentSlotIds.Contains(slotId, StringComparer.OrdinalIgnoreCase))
        {
            AddError(result, "shield_slot_invalid", "Shield should use off_hand or an explicitly allowed shield slot.", item.ItemInstanceId, item.DefinitionId);
        }
    }

    private static EquipmentValidationResult CreateResult()
    {
        return new EquipmentValidationResult { CheckedAtUtc = DateTime.UtcNow };
    }

    private static void AddDefinitionMissing(EquipmentValidationResult result, bool strictMode, string code, string message, string itemInstanceId, string definitionId)
    {
        if (strictMode) AddError(result, code, message, itemInstanceId, definitionId);
        else AddWarning(result, code, message, itemInstanceId, definitionId);
    }

    private static void AddWarnings(EquipmentValidationResult result, IEnumerable<string> warnings, string itemInstanceId, string definitionId)
    {
        foreach (var warning in warnings ?? Enumerable.Empty<string>())
        {
            AddWarning(result, warning, warning, itemInstanceId, definitionId);
        }
    }

    private static void AddError(EquipmentValidationResult result, string code, string message, string itemInstanceId, string definitionId)
    {
        result.IsValid = false;
        result.Errors.Add(new InventoryValidationIssue
        {
            Code = code ?? string.Empty,
            Severity = "error",
            Message = message ?? string.Empty,
            ItemInstanceId = itemInstanceId ?? string.Empty,
            DefinitionId = definitionId ?? string.Empty
        });
    }

    private static void AddWarning(EquipmentValidationResult result, string code, string message, string itemInstanceId, string definitionId)
    {
        result.Warnings.Add(new InventoryValidationIssue
        {
            Code = code ?? string.Empty,
            Severity = "warning",
            Message = message ?? string.Empty,
            ItemInstanceId = itemInstanceId ?? string.Empty,
            DefinitionId = definitionId ?? string.Empty
        });
    }

    private static void Merge(EquipmentValidationResult target, EquipmentValidationResult source)
    {
        if (source == null) return;
        if (!source.IsValid) target.IsValid = false;
        target.Errors.AddRange(source.Errors ?? new List<InventoryValidationIssue>());
        target.Warnings.AddRange(source.Warnings ?? new List<InventoryValidationIssue>());
    }

    private void FinishLog(EquipmentValidationRequest request, EquipmentValidationResult result)
    {
        _logger?.Debug($"equipment.validation.done characterId={request?.CharacterId} valid={result.IsValid} errors={result.Errors.Count} warnings={result.Warnings.Count}");
    }

    private static bool IsSlot(string value, string expected)
    {
        return string.Equals(value ?? string.Empty, expected ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesAny(string value, params string[] expected)
    {
        return expected.Any(x => string.Equals(value, x, StringComparison.OrdinalIgnoreCase));
    }

    private static string FirstNonEmpty(params string[] values)
    {
        foreach (var value in values ?? Array.Empty<string>())
        {
            if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
        }

        return string.Empty;
    }

    private sealed class EquipmentDefinitionLookupResult
    {
        public EquipmentDefinitionSnapshot? Snapshot { get; set; }
        public List<string> Warnings { get; } = new List<string>();
    }

    private sealed class EquipmentDefinitionSnapshot
    {
        public string Kind { get; set; } = string.Empty;
        public string DefinitionId { get; set; } = string.Empty;
        public string Handedness { get; set; } = string.Empty;
        public string ArmorType { get; set; } = string.Empty;
        public bool IsConsumable { get; set; }
        public bool IsShield { get; set; }
        public List<string> EquipmentSlotIds { get; set; } = new List<string>();
        public List<string> Tags { get; set; } = new List<string>();

        public static EquipmentDefinitionSnapshot FromWeapon(WeaponDefinitionView view)
        {
            return new EquipmentDefinitionSnapshot
            {
                Kind = "weapon",
                DefinitionId = view?.DefinitionId ?? string.Empty,
                Handedness = view?.Handedness ?? string.Empty,
                EquipmentSlotIds = view?.EquipmentSlotIds == null ? new List<string>() : new List<string>(view.EquipmentSlotIds),
                Tags = view?.Tags == null ? new List<string>() : new List<string>(view.Tags)
            };
        }

        public static EquipmentDefinitionSnapshot FromArmor(ArmorDefinitionView view)
        {
            var tags = view?.Tags == null ? new List<string>() : new List<string>(view.Tags);
            var armorType = view?.ArmorType ?? string.Empty;
            return new EquipmentDefinitionSnapshot
            {
                Kind = "armor",
                DefinitionId = view?.DefinitionId ?? string.Empty,
                ArmorType = armorType,
                EquipmentSlotIds = view?.EquipmentSlotIds == null ? new List<string>() : new List<string>(view.EquipmentSlotIds),
                Tags = tags,
                IsShield = string.Equals(armorType, "shield", StringComparison.OrdinalIgnoreCase)
                    || tags.Any(x => string.Equals(x, "shield", StringComparison.OrdinalIgnoreCase))
            };
        }

        public static EquipmentDefinitionSnapshot FromItem(ItemDefinitionView view)
        {
            return new EquipmentDefinitionSnapshot
            {
                Kind = "item",
                DefinitionId = view?.DefinitionId ?? string.Empty,
                IsConsumable = view?.IsConsumable ?? false,
                Tags = view?.Tags == null ? new List<string>() : new List<string>(view.Tags)
            };
        }

        public static EquipmentDefinitionSnapshot FromAmmo(AmmoDefinitionView view)
        {
            return new EquipmentDefinitionSnapshot
            {
                Kind = "ammo",
                DefinitionId = view?.DefinitionId ?? string.Empty,
                IsConsumable = view?.IsConsumable ?? true,
                Tags = view?.Tags == null ? new List<string>() : new List<string>(view.Tags)
            };
        }
    }
}
