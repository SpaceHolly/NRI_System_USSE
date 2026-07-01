using System;
using System.Collections.Generic;
using System.Linq;

namespace Nri.Shared.Domain;

public static class InventoryFeatureFlags
{
    public const bool UseInventorySystemV1 = false;
    public const bool UseEquipmentSystemV1 = false;
    public const bool UseInventoryItemDefinitionValidation = false;
    public const bool UseEquipmentSlotValidation = false;
    public const bool UseInventoryStackValidation = false;
    public const bool UseInventoryQuantityValidation = false;
    public const bool UseInventoryDurabilityValidation = false;
    public const bool UseInventoryContainerValidation = false;
    public const bool UseAmmoStackValidation = false;
    public const bool UseConsumableStackValidation = false;
    public const bool UseItemDefinitionResolver = false;
    public const bool UseEquipmentDefinitionResolver = false;
    public const bool UseDefinitionBasedInventoryValidation = false;
    public const bool UseTwoHandedWeaponValidation = false;
    public const bool UseShieldSlotValidation = false;
    public const bool UseArmorSlotValidation = false;
    public const bool UseAccessorySlotValidation = false;
    public const bool UseWeaponAmmoCompatibilityValidation = false;
    public const bool UseWeaponSkillLinkValidation = false;
    public const bool UseWeaponSlotCompatibilityValidation = false;
    public const bool UseArmorSlotCompatibilityValidation = false;
    public const bool UseArmorSizeFitValidation = false;
    public const bool UseInventoryLegalityReadModel = false;
    public const bool UseInventoryRestrictionReadModel = false;
    public const bool UseInventoryMarketTagReadModel = false;
    public const bool UseRuntimeLawRestrictionLookup = false;
    public const bool UseInventoryDiagnosticsEndpoints = false;
}

public static class InventoryLegalityStatusIds
{
    public const string Unknown = "unknown";
    public const string Legal = "legal";
    public const string Restricted = "restricted";
    public const string LicenseRequired = "license_required";
    public const string GmApprovalRequired = "gm_approval_required";
    public const string Forbidden = "forbidden";
    public const string BlackMarketOnly = "black_market_only";
    public const string ContextMissing = "context_missing";
}

public sealed class InventoryItemInstanceState
{
    public string ItemInstanceId { get; set; } = string.Empty;
    public string DefinitionId { get; set; } = string.Empty;
    public string ItemCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ItemType { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public bool Stackable { get; set; }
    public int MaxStack { get; set; } = 1;
    public int Durability { get; set; }
    public int MaxDurability { get; set; }
    public bool IsEquipped { get; set; }
    public string EquipmentSlotId { get; set; } = string.Empty;
    public string ContainerId { get; set; } = string.Empty;
    public string ParentItemInstanceId { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new List<string>();
    public string Notes { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class EquipmentSlotAssignmentState
{
    public string EquipmentSlotId { get; set; } = string.Empty;
    public string ItemInstanceId { get; set; } = string.Empty;
    public string DefinitionId { get; set; } = string.Empty;
    public bool IsLocked { get; set; }
    public string Source { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
}

public sealed class EquipmentLoadoutState
{
    public string CharacterId { get; set; } = string.Empty;
    public string LoadoutId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<EquipmentSlotAssignmentState> SlotAssignments { get; set; } = new List<EquipmentSlotAssignmentState>();
    public string MainHandItemInstanceId { get; set; } = string.Empty;
    public string OffHandItemInstanceId { get; set; } = string.Empty;
    public string TwoHandedItemInstanceId { get; set; } = string.Empty;
    public List<string> ArmorItemInstanceIds { get; set; } = new List<string>();
    public List<string> AccessoryItemInstanceIds { get; set; } = new List<string>();
    public string BackpackItemInstanceId { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new List<string>();
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class InventoryContainerState
{
    public string ContainerId { get; set; } = string.Empty;
    public string ContainerItemInstanceId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal MaxWeightKg { get; set; }
    public decimal MaxVolumeUnits { get; set; }
    public List<string> AllowedItemTags { get; set; } = new List<string>();
    public List<string> BlockedItemTags { get; set; } = new List<string>();
    public string Notes { get; set; } = string.Empty;
}

public sealed class EquipmentSlotRule
{
    public string EquipmentSlotId { get; set; } = string.Empty;
    public int MaxItems { get; set; } = 1;
    public bool IsBodySlot { get; set; }
    public bool IsContainerSlot { get; set; }
    public List<string> AllowedItemTypes { get; set; } = new List<string>();
    public List<string> BlockedItemTypes { get; set; } = new List<string>();
}

public sealed class InventoryValidationIssue
{
    public string Code { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string ItemInstanceId { get; set; } = string.Empty;
    public string DefinitionId { get; set; } = string.Empty;
}

public sealed class InventoryValidationResult
{
    public bool IsValid { get; set; } = true;
    public List<string> Errors { get; set; } = new List<string>();
    public List<string> Warnings { get; set; } = new List<string>();
    public List<InventoryValidationIssue> Issues { get; set; } = new List<InventoryValidationIssue>();
    public string Section { get; set; } = string.Empty;
    public DateTime CheckedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class EquipmentValidationRequest
{
    public string CharacterId { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = string.Empty;
    public List<InventoryItemInstanceState> Items { get; set; } = new List<InventoryItemInstanceState>();
    public EquipmentLoadoutState Loadout { get; set; } = new EquipmentLoadoutState();
    public bool StrictMode { get; set; }
    public string RequestId { get; set; } = string.Empty;
}

public sealed class EquipmentValidationResult
{
    public bool IsValid { get; set; } = true;
    public List<InventoryValidationIssue> Errors { get; set; } = new List<InventoryValidationIssue>();
    public List<InventoryValidationIssue> Warnings { get; set; } = new List<InventoryValidationIssue>();
    public DateTime CheckedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class InventoryLegalityContext
{
    public string RuleSetId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string CountryId { get; set; } = string.Empty;
    public string CityStateId { get; set; } = string.Empty;
    public string LocationId { get; set; } = string.Empty;
    public string ActorCharacterId { get; set; } = string.Empty;
    public string ActorOrganizationId { get; set; } = string.Empty;
    public string ActorFactionId { get; set; } = string.Empty;
    public bool IncludeRuntimeStates { get; set; }
    public bool StrictMode { get; set; }
    public string RequestId { get; set; } = string.Empty;
}

public sealed class InventoryItemLegalityRequest
{
    public InventoryItemInstanceState Item { get; set; } = new InventoryItemInstanceState();
    public InventoryLegalityContext Context { get; set; } = new InventoryLegalityContext();
}

public sealed class InventoryLegalityBatchRequest
{
    public List<InventoryItemInstanceState> Items { get; set; } = new List<InventoryItemInstanceState>();
    public InventoryLegalityContext Context { get; set; } = new InventoryLegalityContext();
}

public sealed class InventoryItemLegalityResult
{
    public string ItemInstanceId { get; set; } = string.Empty;
    public string DefinitionId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string LegalityStatus { get; set; } = InventoryLegalityStatusIds.Unknown;
    public bool RequiresLicense { get; set; }
    public bool RequiresGMApproval { get; set; }
    public bool IsRestricted { get; set; }
    public bool IsForbidden { get; set; }
    public bool IsBlackMarketRelevant { get; set; }
    public List<string> MatchedLawIds { get; set; } = new List<string>();
    public List<string> MatchedRestrictionIds { get; set; } = new List<string>();
    public List<string> MatchedMarketTagIds { get; set; } = new List<string>();
    public List<InventoryValidationIssue> Warnings { get; set; } = new List<InventoryValidationIssue>();
    public List<InventoryValidationIssue> Errors { get; set; } = new List<InventoryValidationIssue>();
    public DateTime CheckedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class InventoryLegalityBatchResult
{
    public bool IsValid { get; set; } = true;
    public List<InventoryItemLegalityResult> Items { get; set; } = new List<InventoryItemLegalityResult>();
    public List<InventoryValidationIssue> Warnings { get; set; } = new List<InventoryValidationIssue>();
    public List<InventoryValidationIssue> Errors { get; set; } = new List<InventoryValidationIssue>();
    public DateTime CheckedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class InventoryDiagnosticsRequest
{
    public string CharacterId { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string CountryId { get; set; } = string.Empty;
    public string CityStateId { get; set; } = string.Empty;
    public string LocationId { get; set; } = string.Empty;
    public bool StrictMode { get; set; }
    public bool IncludeSlotValidation { get; set; } = true;
    public bool IncludeItemStateValidation { get; set; } = true;
    public bool IncludeCompatibilityValidation { get; set; } = true;
    public bool IncludeLegalityValidation { get; set; } = true;
    public bool IncludeWarnings { get; set; } = true;
    public string RequestId { get; set; } = string.Empty;
}

public sealed class InventoryDiagnosticsResponse
{
    public string CharacterId { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public bool IsValid { get; set; } = true;
    public List<InventoryDiagnosticsSection> Sections { get; set; } = new List<InventoryDiagnosticsSection>();
    public List<InventoryValidationIssue> Errors { get; set; } = new List<InventoryValidationIssue>();
    public List<InventoryValidationIssue> Warnings { get; set; } = new List<InventoryValidationIssue>();
    public InventoryDiagnosticsSummary Summary { get; set; } = new InventoryDiagnosticsSummary();
    public DateTime CheckedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class InventoryDiagnosticsSection
{
    public string Section { get; set; } = string.Empty;
    public bool IsValid { get; set; } = true;
    public List<InventoryValidationIssue> Errors { get; set; } = new List<InventoryValidationIssue>();
    public List<InventoryValidationIssue> Warnings { get; set; } = new List<InventoryValidationIssue>();
}

public sealed class InventoryDiagnosticsSummary
{
    public int ItemCount { get; set; }
    public int EquippedItemCount { get; set; }
    public int ErrorCount { get; set; }
    public int WarningCount { get; set; }
    public int SlotErrorCount { get; set; }
    public int ItemStateErrorCount { get; set; }
    public int CompatibilityErrorCount { get; set; }
    public int LegalityWarningCount { get; set; }
}

public static class InventoryDomainMapper
{
    public static InventoryItemInstanceState ToItemInstanceState(CharacterInventoryItemProfileValue item)
    {
        if (item == null) return new InventoryItemInstanceState();
        var definitionId = item.DefinitionId ?? string.Empty;
        return new InventoryItemInstanceState
        {
            ItemInstanceId = item.ItemId ?? string.Empty,
            DefinitionId = definitionId,
            ItemCode = definitionId,
            DisplayName = item.Name ?? string.Empty,
            Quantity = item.Quantity,
            Durability = item.Durability,
            MaxDurability = item.MaxDurability,
            IsEquipped = item.IsEquipped,
            EquipmentSlotId = item.SlotId ?? string.Empty,
            Tags = item.Tags == null ? new List<string>() : new List<string>(item.Tags),
            Notes = item.Notes ?? string.Empty,
            Source = string.IsNullOrWhiteSpace(item.Source) ? "inventory_profile" : item.Source,
            UpdatedAtUtc = DateTime.UtcNow
        };
    }

    public static CharacterInventoryItemProfileValue ToProfileItemValue(InventoryItemInstanceState item)
    {
        if (item == null) return new CharacterInventoryItemProfileValue();
        var definitionId = FirstNonEmpty(item.DefinitionId, item.ItemCode);
        return new CharacterInventoryItemProfileValue
        {
            ItemId = item.ItemInstanceId ?? string.Empty,
            DefinitionId = definitionId,
            Name = item.DisplayName ?? string.Empty,
            Quantity = Math.Max(0, item.Quantity),
            Durability = Math.Max(0, item.Durability),
            MaxDurability = Math.Max(0, item.MaxDurability),
            IsEquipped = item.IsEquipped,
            SlotId = item.EquipmentSlotId ?? string.Empty,
            Source = string.IsNullOrWhiteSpace(item.Source) ? "inventory_system_v1_mapper" : item.Source,
            Notes = item.Notes ?? string.Empty,
            Tags = item.Tags == null ? new List<string>() : new List<string>(item.Tags)
        };
    }

    public static EquipmentLoadoutState BuildLoadoutFromInventoryProfile(InventoryProfile profile)
    {
        var loadout = new EquipmentLoadoutState
        {
            CharacterId = profile?.CharacterId ?? string.Empty,
            LoadoutId = "default",
            Name = "Default",
            UpdatedAtUtc = DateTime.UtcNow
        };

        foreach (var item in (profile?.Items ?? new List<CharacterInventoryItemProfileValue>())
            .Where(x => x != null && x.IsEquipped))
        {
            var slotId = item.SlotId ?? string.Empty;
            var itemId = item.ItemId ?? string.Empty;
            loadout.SlotAssignments.Add(new EquipmentSlotAssignmentState
            {
                EquipmentSlotId = slotId,
                ItemInstanceId = itemId,
                DefinitionId = item.DefinitionId ?? string.Empty,
                Source = "inventory_profile"
            });

            if (string.Equals(slotId, "main_hand", StringComparison.OrdinalIgnoreCase)) loadout.MainHandItemInstanceId = itemId;
            else if (string.Equals(slotId, "off_hand", StringComparison.OrdinalIgnoreCase)) loadout.OffHandItemInstanceId = itemId;
            else if (string.Equals(slotId, "two_handed", StringComparison.OrdinalIgnoreCase)) loadout.TwoHandedItemInstanceId = itemId;
            else if (string.Equals(slotId, "accessory", StringComparison.OrdinalIgnoreCase)) loadout.AccessoryItemInstanceIds.Add(itemId);
            else if (string.Equals(slotId, "backpack", StringComparison.OrdinalIgnoreCase)) loadout.BackpackItemInstanceId = itemId;
            else if (IsArmorSlot(slotId)) loadout.ArmorItemInstanceIds.Add(itemId);
        }

        return loadout;
    }

    public static InventoryProfile BuildInventoryProfileFromItemInstances(string characterId, string ruleSetId, IEnumerable<InventoryItemInstanceState> items)
    {
        return new InventoryProfile
        {
            CharacterId = characterId ?? string.Empty,
            RuleSetId = string.IsNullOrWhiteSpace(ruleSetId) ? RuleSetIds.FantasyNriDefault : ruleSetId,
            Items = (items ?? Enumerable.Empty<InventoryItemInstanceState>()).Select(ToProfileItemValue).ToList(),
            SchemaVersion = 1
        };
    }

    private static bool IsArmorSlot(string slotId)
    {
        return string.Equals(slotId, "head", StringComparison.OrdinalIgnoreCase)
            || string.Equals(slotId, "torso", StringComparison.OrdinalIgnoreCase)
            || string.Equals(slotId, "legs", StringComparison.OrdinalIgnoreCase)
            || string.Equals(slotId, "feet", StringComparison.OrdinalIgnoreCase)
            || string.Equals(slotId, "hands", StringComparison.OrdinalIgnoreCase);
    }

    private static string FirstNonEmpty(params string[] values)
    {
        foreach (var value in values ?? Array.Empty<string>())
        {
            if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
        }

        return string.Empty;
    }
}

public static class InventoryDomainValidator
{
    public static InventoryValidationResult ValidateItemInstance(InventoryItemInstanceState item)
    {
        var result = CreateResult("item_instance");
        if (item == null)
        {
            AddError(result, "item_null", "Inventory item instance is null.", string.Empty, string.Empty);
            return result;
        }

        if (string.IsNullOrWhiteSpace(item.ItemInstanceId)) AddError(result, "item_instance_id_required", "ItemInstanceId is required.", item.ItemInstanceId, item.DefinitionId);
        if (item.Quantity < 0) AddError(result, "quantity_negative", "Quantity must be greater than or equal to zero.", item.ItemInstanceId, item.DefinitionId);
        if (item.IsEquipped && string.IsNullOrWhiteSpace(item.EquipmentSlotId)) AddWarning(result, "equipped_slot_missing", "Equipped item should have EquipmentSlotId.", item.ItemInstanceId, item.DefinitionId);
        if (item.Tags == null) AddError(result, "tags_null", "Tags collection must not be null.", item.ItemInstanceId, item.DefinitionId);

        Merge(result, ValidateStack(item));
        Merge(result, ValidateDurability(item));
        return result;
    }

    public static InventoryValidationResult ValidateStack(InventoryItemInstanceState item)
    {
        var result = CreateResult("stack");
        if (item == null)
        {
            AddError(result, "item_null", "Inventory item instance is null.", string.Empty, string.Empty);
            return result;
        }

        if (item.Stackable)
        {
            if (item.MaxStack < 1) AddError(result, "max_stack_invalid", "Stackable item must have MaxStack >= 1.", item.ItemInstanceId, item.DefinitionId);
            if (item.MaxStack >= 1 && item.Quantity > item.MaxStack) AddError(result, "quantity_exceeds_max_stack", "Quantity exceeds MaxStack.", item.ItemInstanceId, item.DefinitionId);
        }
        else if (item.Quantity > 1)
        {
            AddWarning(result, "non_stackable_quantity_gt_one", "Non-stackable item has Quantity greater than one.", item.ItemInstanceId, item.DefinitionId);
        }

        return result;
    }

    public static InventoryValidationResult ValidateDurability(InventoryItemInstanceState item)
    {
        var result = CreateResult("durability");
        if (item == null)
        {
            AddError(result, "item_null", "Inventory item instance is null.", string.Empty, string.Empty);
            return result;
        }

        if (item.Durability < 0) AddError(result, "durability_negative", "Durability must be greater than or equal to zero.", item.ItemInstanceId, item.DefinitionId);
        if (item.MaxDurability < 0) AddError(result, "max_durability_negative", "MaxDurability must be greater than or equal to zero.", item.ItemInstanceId, item.DefinitionId);
        if (item.MaxDurability > 0 && item.MaxDurability < item.Durability) AddError(result, "max_durability_less_than_durability", "MaxDurability must not be lower than Durability.", item.ItemInstanceId, item.DefinitionId);
        return result;
    }

    public static InventoryValidationResult ValidateLoadout(EquipmentLoadoutState loadout, IEnumerable<InventoryItemInstanceState> items)
    {
        var result = CreateResult("loadout");
        if (loadout == null)
        {
            AddError(result, "loadout_null", "Equipment loadout is null.", string.Empty, string.Empty);
            return result;
        }

        if (loadout.SlotAssignments == null) AddError(result, "slot_assignments_null", "SlotAssignments collection must not be null.", string.Empty, string.Empty);
        if (loadout.ArmorItemInstanceIds == null) AddError(result, "armor_item_ids_null", "ArmorItemInstanceIds collection must not be null.", string.Empty, string.Empty);
        if (loadout.AccessoryItemInstanceIds == null) AddError(result, "accessory_item_ids_null", "AccessoryItemInstanceIds collection must not be null.", string.Empty, string.Empty);
        if (loadout.Tags == null) AddError(result, "loadout_tags_null", "Tags collection must not be null.", string.Empty, string.Empty);

        var itemIds = (items ?? Enumerable.Empty<InventoryItemInstanceState>())
            .Where(x => x != null && !string.IsNullOrWhiteSpace(x.ItemInstanceId))
            .Select(x => x.ItemInstanceId.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var assignment in loadout.SlotAssignments ?? new List<EquipmentSlotAssignmentState>())
        {
            if (assignment == null) continue;
            if (string.IsNullOrWhiteSpace(assignment.EquipmentSlotId)) AddWarning(result, "assignment_slot_missing", "Slot assignment should have EquipmentSlotId.", assignment.ItemInstanceId, assignment.DefinitionId);
            if (string.IsNullOrWhiteSpace(assignment.ItemInstanceId)) AddError(result, "assignment_item_id_required", "Slot assignment must have ItemInstanceId.", assignment.ItemInstanceId, assignment.DefinitionId);
            else if (itemIds.Count > 0 && !itemIds.Contains(assignment.ItemInstanceId.Trim())) AddWarning(result, "assignment_item_missing", "Slot assignment points to item that is not in inventory items.", assignment.ItemInstanceId, assignment.DefinitionId);
        }

        foreach (var duplicate in (loadout.SlotAssignments ?? new List<EquipmentSlotAssignmentState>())
            .Where(x => x != null && !string.IsNullOrWhiteSpace(x.EquipmentSlotId))
            .GroupBy(x => x.EquipmentSlotId.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(x => x.Count() > 1))
        {
            AddError(result, "duplicate_slot_assignment", $"Multiple items assigned to slot '{duplicate.Key}'.", string.Empty, string.Empty);
        }

        return result;
    }

    public static InventoryValidationResult ValidateContainer(InventoryContainerState container)
    {
        var result = CreateResult("container");
        if (container == null)
        {
            AddError(result, "container_null", "Inventory container is null.", string.Empty, string.Empty);
            return result;
        }

        if (string.IsNullOrWhiteSpace(container.ContainerId)) AddError(result, "container_id_required", "ContainerId is required.", string.Empty, string.Empty);
        if (container.MaxWeightKg < 0) AddError(result, "max_weight_negative", "MaxWeightKg must be greater than or equal to zero.", string.Empty, string.Empty);
        if (container.MaxVolumeUnits < 0) AddError(result, "max_volume_negative", "MaxVolumeUnits must be greater than or equal to zero.", string.Empty, string.Empty);
        if (container.AllowedItemTags == null) AddError(result, "allowed_item_tags_null", "AllowedItemTags collection must not be null.", string.Empty, string.Empty);
        if (container.BlockedItemTags == null) AddError(result, "blocked_item_tags_null", "BlockedItemTags collection must not be null.", string.Empty, string.Empty);
        return result;
    }

    public static InventoryValidationResult ValidateInventory(IEnumerable<InventoryItemInstanceState> items, EquipmentLoadoutState loadout)
    {
        var result = CreateResult("inventory");
        var list = (items ?? Enumerable.Empty<InventoryItemInstanceState>()).Where(x => x != null).ToList();
        foreach (var item in list)
        {
            Merge(result, ValidateItemInstance(item));
        }

        foreach (var duplicate in list
            .Where(x => !string.IsNullOrWhiteSpace(x.ItemInstanceId))
            .GroupBy(x => x.ItemInstanceId.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(x => x.Count() > 1))
        {
            AddError(result, "duplicate_item_instance_id", $"Duplicate ItemInstanceId '{duplicate.Key}'.", duplicate.Key, string.Empty);
        }

        foreach (var duplicate in list
            .Where(x => x.IsEquipped && !string.IsNullOrWhiteSpace(x.EquipmentSlotId))
            .GroupBy(x => x.EquipmentSlotId.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(x => x.Count() > 1))
        {
            AddError(result, "duplicate_equipped_slot", $"Multiple equipped items use slot '{duplicate.Key}'.", string.Empty, string.Empty);
        }

        if (loadout != null) Merge(result, ValidateLoadout(loadout, list));
        return result;
    }

    public static InventoryValidationResult ValidateEquipmentSlotRule(EquipmentSlotRule rule)
    {
        var result = CreateResult("equipment_slot_rule");
        if (rule == null)
        {
            AddError(result, "slot_rule_null", "Equipment slot rule is null.", string.Empty, string.Empty);
            return result;
        }

        if (string.IsNullOrWhiteSpace(rule.EquipmentSlotId)) AddError(result, "equipment_slot_id_required", "EquipmentSlotId is required.", string.Empty, string.Empty);
        if (rule.MaxItems < 1) AddError(result, "max_items_invalid", "MaxItems must be at least one.", string.Empty, string.Empty);
        if (rule.AllowedItemTypes == null) AddError(result, "allowed_item_types_null", "AllowedItemTypes collection must not be null.", string.Empty, string.Empty);
        if (rule.BlockedItemTypes == null) AddError(result, "blocked_item_types_null", "BlockedItemTypes collection must not be null.", string.Empty, string.Empty);
        return result;
    }

    private static InventoryValidationResult CreateResult(string section)
    {
        return new InventoryValidationResult { Section = section ?? string.Empty, CheckedAtUtc = DateTime.UtcNow };
    }

    private static void AddError(InventoryValidationResult result, string code, string message, string itemInstanceId, string definitionId)
    {
        result.IsValid = false;
        result.Errors.Add(code);
        result.Issues.Add(new InventoryValidationIssue
        {
            Code = code,
            Severity = "error",
            Message = message,
            ItemInstanceId = itemInstanceId ?? string.Empty,
            DefinitionId = definitionId ?? string.Empty
        });
    }

    private static void AddWarning(InventoryValidationResult result, string code, string message, string itemInstanceId, string definitionId)
    {
        result.Warnings.Add(code);
        result.Issues.Add(new InventoryValidationIssue
        {
            Code = code,
            Severity = "warning",
            Message = message,
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
}
