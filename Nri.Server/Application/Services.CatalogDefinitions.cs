using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MongoDB.Driver;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;
using Nri.Shared.Utilities;

namespace Nri.Server.Application;

public partial class ServiceHub
{
    public ResponseEnvelope CatalogAdminItemsList(CommandContext context) => CatalogAdminList(context, DefinitionCategoryIds.Item);
    public ResponseEnvelope CatalogAdminItemsGet(CommandContext context) => CatalogAdminGet(context, DefinitionCategoryIds.Item);
    public ResponseEnvelope CatalogAdminItemsCreate(CommandContext context) => CatalogAdminUpsert(context, DefinitionCategoryIds.Item, true);
    public ResponseEnvelope CatalogAdminItemsUpdate(CommandContext context) => CatalogAdminUpsert(context, DefinitionCategoryIds.Item, false);
    public ResponseEnvelope CatalogAdminItemsArchive(CommandContext context) => CatalogAdminArchive(context, DefinitionCategoryIds.Item);

    public ResponseEnvelope CatalogAdminWeaponsList(CommandContext context) => CatalogAdminList(context, DefinitionCategoryIds.Weapon);
    public ResponseEnvelope CatalogAdminWeaponsGet(CommandContext context) => CatalogAdminGet(context, DefinitionCategoryIds.Weapon);
    public ResponseEnvelope CatalogAdminWeaponsCreate(CommandContext context) => CatalogAdminUpsert(context, DefinitionCategoryIds.Weapon, true);
    public ResponseEnvelope CatalogAdminWeaponsUpdate(CommandContext context) => CatalogAdminUpsert(context, DefinitionCategoryIds.Weapon, false);
    public ResponseEnvelope CatalogAdminWeaponsArchive(CommandContext context) => CatalogAdminArchive(context, DefinitionCategoryIds.Weapon);

    public ResponseEnvelope CatalogAdminArmorList(CommandContext context) => CatalogAdminList(context, DefinitionCategoryIds.Armor);
    public ResponseEnvelope CatalogAdminArmorGet(CommandContext context) => CatalogAdminGet(context, DefinitionCategoryIds.Armor);
    public ResponseEnvelope CatalogAdminArmorCreate(CommandContext context) => CatalogAdminUpsert(context, DefinitionCategoryIds.Armor, true);
    public ResponseEnvelope CatalogAdminArmorUpdate(CommandContext context) => CatalogAdminUpsert(context, DefinitionCategoryIds.Armor, false);
    public ResponseEnvelope CatalogAdminArmorArchive(CommandContext context) => CatalogAdminArchive(context, DefinitionCategoryIds.Armor);

    public ResponseEnvelope CatalogAdminAmmoList(CommandContext context) => CatalogAdminList(context, DefinitionCategoryIds.Ammo);
    public ResponseEnvelope CatalogAdminAmmoGet(CommandContext context) => CatalogAdminGet(context, DefinitionCategoryIds.Ammo);
    public ResponseEnvelope CatalogAdminAmmoCreate(CommandContext context) => CatalogAdminUpsert(context, DefinitionCategoryIds.Ammo, true);
    public ResponseEnvelope CatalogAdminAmmoUpdate(CommandContext context) => CatalogAdminUpsert(context, DefinitionCategoryIds.Ammo, false);
    public ResponseEnvelope CatalogAdminAmmoArchive(CommandContext context) => CatalogAdminArchive(context, DefinitionCategoryIds.Ammo);

    public ResponseEnvelope CatalogAdminEquipmentSlotsList(CommandContext context) => CatalogAdminList(context, DefinitionCategoryIds.EquipmentSlot);
    public ResponseEnvelope CatalogAdminEquipmentSlotsGet(CommandContext context) => CatalogAdminGet(context, DefinitionCategoryIds.EquipmentSlot);
    public ResponseEnvelope CatalogAdminEquipmentSlotsCreate(CommandContext context) => CatalogAdminUpsert(context, DefinitionCategoryIds.EquipmentSlot, true);
    public ResponseEnvelope CatalogAdminEquipmentSlotsUpdate(CommandContext context) => CatalogAdminUpsert(context, DefinitionCategoryIds.EquipmentSlot, false);
    public ResponseEnvelope CatalogAdminEquipmentSlotsArchive(CommandContext context) => CatalogAdminArchive(context, DefinitionCategoryIds.EquipmentSlot);

    public ResponseEnvelope CatalogPlayerItemsVisibleList(CommandContext context)
    {
        GetCurrentAccount(context);
        var categories = new[] { DefinitionCategoryIds.Item, DefinitionCategoryIds.Weapon, DefinitionCategoryIds.Armor, DefinitionCategoryIds.Ammo };
        var items = LoadCatalogDefinitions(categories, context.Request.Payload, includeArchived: false)
            .Where(IsCatalogDefinitionPlayerVisible)
            .Select(x => (object)CatalogDefinitionPayload(x, admin: false))
            .ToArray();
        return Ok("Visible item catalog loaded.", new Dictionary<string, object> { { "items", items } });
    }

    public ResponseEnvelope CatalogPlayerItemGetVisible(CommandContext context)
    {
        GetCurrentAccount(context);
        var code = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "code"), PayloadReader.GetString(context.Request.Payload, "definitionId"), PayloadReader.GetString(context.Request.Payload, "id"));
        var category = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "category"), DefinitionCategoryIds.Item);
        if (!CatalogCategories().Contains(category, StringComparer.OrdinalIgnoreCase)) return Error("Unsupported catalog category.", ResponseStatus.Error, ErrorCode.ValidationFailed);
        var doc = FindCatalogDefinition(category, code);
        if (doc == null || doc.IsArchived || !IsCatalogDefinitionPlayerVisible(doc)) return Error("Definition not found.", ResponseStatus.NotFound, ErrorCode.NotFound);
        return Ok("Visible catalog definition loaded.", new Dictionary<string, object> { { "item", CatalogDefinitionPayload(doc, admin: false) } });
    }

    public ResponseEnvelope CatalogPlayerEquipmentSlotsVisibleList(CommandContext context)
    {
        GetCurrentAccount(context);
        var items = LoadCatalogDefinitions(new[] { DefinitionCategoryIds.EquipmentSlot }, context.Request.Payload, includeArchived: false)
            .Where(IsCatalogDefinitionPlayerVisible)
            .Select(x => (object)CatalogDefinitionPayload(x, admin: false))
            .ToArray();
        return Ok("Visible equipment slots loaded.", new Dictionary<string, object> { { "items", items } });
    }

    public ResponseEnvelope CharacterInventoryItemAddFromCatalog(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var characterId = RequireLength(PayloadReader.GetString(payload, "characterId"), 8, 128, "characterId");
        var definitionId = NormalizeCatalogCode(FirstNonEmpty(
            PayloadReader.GetString(payload, "itemDefinitionId"),
            PayloadReader.GetString(payload, "definitionId"),
            PayloadReader.GetString(payload, "definitionCode"),
            PayloadReader.GetString(payload, "code")));
        var definitionCategory = FirstNonEmpty(PayloadReader.GetString(payload, "definitionCategory"), PayloadReader.GetString(payload, "category"), DefinitionCategoryIds.Item);
        if (!CatalogItemCategories().Contains(definitionCategory, StringComparer.OrdinalIgnoreCase))
        {
            return Error("Unsupported catalog definition category.", ResponseStatus.Error, ErrorCode.ValidationFailed);
        }

        var definition = FindCatalogDefinition(definitionCategory, definitionId);
        if (definition == null || definition.IsArchived)
        {
            return Error("Catalog definition not found.", ResponseStatus.NotFound, ErrorCode.NotFound);
        }

        var itemPayload = BuildInventoryItemPayloadFromCatalog(definition, payload);
        var native = _profileNativeWriteService.AddInventoryItemProfileNativeAsync(characterId, new Dictionary<string, object>
        {
            { "characterId", characterId },
            { "item", itemPayload }
        }, actor.Id, context.Request.RequestId ?? string.Empty).GetAwaiter().GetResult();

        if (!native.ProfileWritten || !native.LegacyFacadeSynced)
        {
            return Error("Character inventory profile write failed.", ResponseStatus.Error, ErrorCode.InternalError);
        }

        _logger.Admin($"character.inventory.item.addFromCatalog response=ok characterId={characterId} itemId={native.ItemId} definitionId={definition.Id} category={definition.Category}");
        return Ok("Catalog definition added to character inventory.", new Dictionary<string, object>
        {
            { "itemId", native.ItemId },
            { "itemDefinitionId", definition.Id },
            { "definitionCategory", definition.Category },
            { "definitionCode", definition.Id },
            { "snapshotDisplayName", FirstNonEmpty(definition.Name, definition.Id) },
            { "snapshotCategory", CatalogInventoryCategory(definition) },
            { "snapshotDescription", definition.PublicDescription ?? string.Empty }
        });
    }

    private ResponseEnvelope CatalogAdminList(CommandContext context, string category)
    {
        RequireAdmin(context);
        var includeArchived = PayloadReader.GetBool(context.Request.Payload, "includeArchived");
        var items = LoadCatalogDefinitions(new[] { category }, context.Request.Payload, includeArchived)
            .Select(x => (object)CatalogDefinitionPayload(x, admin: true))
            .ToArray();
        _logger.Admin($"catalog.admin.list category={category} count={items.Length}");
        return Ok("Catalog definitions loaded.", new Dictionary<string, object> { { "items", items }, { "category", category } });
    }

    private ResponseEnvelope CatalogAdminGet(CommandContext context, string category)
    {
        RequireAdmin(context);
        var code = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "code"), PayloadReader.GetString(context.Request.Payload, "definitionId"), PayloadReader.GetString(context.Request.Payload, "id"));
        var doc = FindCatalogDefinition(category, code);
        if (doc == null) return Error("Catalog definition not found.", ResponseStatus.NotFound, ErrorCode.NotFound);
        return Ok("Catalog definition loaded.", new Dictionary<string, object> { { "item", CatalogDefinitionPayload(doc, admin: true) } });
    }

    private ResponseEnvelope CatalogAdminUpsert(CommandContext context, string category, bool create)
    {
        var actor = RequireAdmin(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var code = NormalizeCatalogCode(FirstNonEmpty(PayloadReader.GetString(payload, "code"), PayloadReader.GetString(payload, "definitionId"), PayloadReader.GetString(payload, "id")));
        if (string.IsNullOrWhiteSpace(code)) return Error("Code is required.", ResponseStatus.Error, ErrorCode.ValidationFailed);
        var displayName = FirstNonEmpty(PayloadReader.GetString(payload, "displayName"), PayloadReader.GetString(payload, "name"));
        if (string.IsNullOrWhiteSpace(displayName)) return Error("Display name is required.", ResponseStatus.Error, ErrorCode.ValidationFailed);

        var now = DateTime.UtcNow;
        var existing = FindCatalogDefinition(category, code);
        var doc = existing ?? new UnifiedDefinitionDocument
        {
            Id = code,
            Category = category,
            CreatedAtUtc = now,
            SourceDocument = "admin_catalog_gui"
        };

        if (create && existing != null && !existing.IsArchived)
        {
            return Error("Catalog definition already exists.", ResponseStatus.Error, ErrorCode.ValidationFailed);
        }

        doc.Id = code;
        doc.Category = category;
        doc.Name = displayName.Trim();
        doc.PublicDescription = PayloadReader.GetString(payload, "description") ?? doc.PublicDescription ?? string.Empty;
        doc.RuleSetIds = NormalizeCatalogList(FirstNonEmpty(PayloadReader.GetString(payload, "ruleSetId"), RuleSetIds.FantasyNriDefault));
        doc.Tags = NormalizeCatalogList(PayloadReader.GetString(payload, "tags"));
        doc.VisibilityRule = PayloadReader.GetBool(payload, "isPlayerVisible") ? VisibilityRuleIds.Public : VisibilityRuleIds.GmOnly;
        doc.IsArchived = false;
        doc.UpdatedAtUtc = now;
        if (doc.CreatedAtUtc == default(DateTime)) doc.CreatedAtUtc = now;
        doc.ExtraData = BuildCatalogExtraData(category, payload, doc.ExtraData);
        doc.ServerOnlyData = doc.ServerOnlyData ?? new Dictionary<string, object>();
        doc.ServerOnlyData["lastAdminEditorUserId"] = actor.Id;
        doc.ServerOnlyData["lastAdminEditorRequestId"] = context.Request.RequestId ?? string.Empty;

        _mongo.UnifiedDefinitions.ReplaceOne(
            Builders<UnifiedDefinitionDocument>.Filter.Eq(x => x.Category, category) & Builders<UnifiedDefinitionDocument>.Filter.Eq(x => x.Id, code),
            doc,
            new ReplaceOptions { IsUpsert = true });

        _logger.Admin($"catalog.admin.{(create ? "create" : "update")} category={category} code={code} actor={actor.Id}");
        return Ok(create ? "Catalog definition created." : "Catalog definition updated.", new Dictionary<string, object> { { "item", CatalogDefinitionPayload(doc, admin: true) } });
    }

    private ResponseEnvelope CatalogAdminArchive(CommandContext context, string category)
    {
        var actor = RequireAdmin(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var code = NormalizeCatalogCode(FirstNonEmpty(PayloadReader.GetString(payload, "code"), PayloadReader.GetString(payload, "definitionId"), PayloadReader.GetString(payload, "id")));
        if (string.IsNullOrWhiteSpace(code)) return Error("Code is required.", ResponseStatus.Error, ErrorCode.ValidationFailed);
        var doc = FindCatalogDefinition(category, code);
        if (doc == null) return Error("Catalog definition not found.", ResponseStatus.NotFound, ErrorCode.NotFound);
        doc.IsArchived = true;
        doc.UpdatedAtUtc = DateTime.UtcNow;
        doc.ServerOnlyData ??= new Dictionary<string, object>();
        doc.ServerOnlyData["archivedByUserId"] = actor.Id;
        doc.ServerOnlyData["archiveRequestId"] = context.Request.RequestId ?? string.Empty;
        _mongo.UnifiedDefinitions.ReplaceOne(Builders<UnifiedDefinitionDocument>.Filter.Eq(x => x.Category, category) & Builders<UnifiedDefinitionDocument>.Filter.Eq(x => x.Id, code), doc);
        _logger.Admin($"catalog.admin.archive category={category} code={code} actor={actor.Id}");
        return Ok("Catalog definition archived.", new Dictionary<string, object> { { "item", CatalogDefinitionPayload(doc, admin: true) } });
    }

    private IReadOnlyCollection<UnifiedDefinitionDocument> LoadCatalogDefinitions(string[] categories, IDictionary<string, object> payload, bool includeArchived)
    {
        var filter = Builders<UnifiedDefinitionDocument>.Filter.In(x => x.Category, categories);
        if (!includeArchived) filter &= Builders<UnifiedDefinitionDocument>.Filter.Eq(x => x.IsArchived, false);
        var search = PayloadReader.GetString(payload, "search") ?? string.Empty;
        var ruleSetId = PayloadReader.GetString(payload, "ruleSetId") ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(ruleSetId)) filter &= Builders<UnifiedDefinitionDocument>.Filter.AnyEq(x => x.RuleSetIds, ruleSetId);
        var list = _mongo.UnifiedDefinitions.Find(filter).ToList();
        if (!string.IsNullOrWhiteSpace(search))
        {
            list = list.Where(x => IndexOfIgnoreCase(x.Id, search) >= 0
                || IndexOfIgnoreCase(x.Name, search) >= 0
                || IndexOfIgnoreCase(x.PublicDescription, search) >= 0
                || (x.Tags ?? new List<string>()).Any(t => IndexOfIgnoreCase(t, search) >= 0)).ToList();
        }

        return list.OrderBy(x => CatalogExtraString(x, "sortOrder")).ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.Id, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private UnifiedDefinitionDocument? FindCatalogDefinition(string category, string code)
    {
        code = NormalizeCatalogCode(code);
        if (string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(code)) return null;
        return _mongo.UnifiedDefinitions.Find(Builders<UnifiedDefinitionDocument>.Filter.Eq(x => x.Category, category) & Builders<UnifiedDefinitionDocument>.Filter.Eq(x => x.Id, code)).FirstOrDefault();
    }

    private static Dictionary<string, object> CatalogDefinitionPayload(UnifiedDefinitionDocument doc, bool admin)
    {
        var extra = doc.ExtraData ?? new Dictionary<string, object>();
        var payload = new Dictionary<string, object>
        {
            { "definitionId", doc.Id ?? string.Empty },
            { "id", doc.Id ?? string.Empty },
            { "code", doc.Id ?? string.Empty },
            { "category", doc.Category ?? string.Empty },
            { "displayName", doc.Name ?? string.Empty },
            { "name", doc.Name ?? string.Empty },
            { "description", doc.PublicDescription ?? string.Empty },
            { "ruleSetId", (doc.RuleSetIds ?? new List<string>()).FirstOrDefault() ?? string.Empty },
            { "ruleSetIds", (doc.RuleSetIds ?? new List<string>()).Cast<object>().ToArray() },
            { "isPlayerVisible", IsCatalogDefinitionPlayerVisible(doc) },
            { "isArchived", doc.IsArchived },
            { "tags", (doc.Tags ?? new List<string>()).Cast<object>().ToArray() },
            { "tagsText", string.Join(",", doc.Tags ?? new List<string>()) },
            { "sortOrder", CatalogExtraInt(doc, "sortOrder") },
            { "createdAtUtc", doc.CreatedAtUtc },
            { "updatedAtUtc", doc.UpdatedAtUtc }
        };

        foreach (var key in CatalogExtraKeys())
        {
            payload[key] = extra.TryGetValue(key, out var value) && value != null ? value : string.Empty;
        }

        if (admin)
        {
            payload["gmDescription"] = doc.GMDescription ?? string.Empty;
            payload["visibilityRule"] = doc.VisibilityRule ?? string.Empty;
            payload["sourceDocument"] = doc.SourceDocument ?? string.Empty;
        }

        return payload;
    }

    private static Dictionary<string, object> BuildCatalogExtraData(string category, IDictionary<string, object> payload, Dictionary<string, object>? existing)
    {
        var extra = existing == null
            ? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, object>(existing, StringComparer.OrdinalIgnoreCase);

        void SetString(string key) => extra[key] = PayloadReader.GetString(payload, key) ?? string.Empty;
        void SetInt(string key) => extra[key] = PayloadReader.GetInt(payload, key);
        void SetDecimal(string key) => extra[key] = CatalogDecimal(payload, key);
        void SetBool(string key) => extra[key] = PayloadReader.GetBool(payload, key);

        extra["catalogType"] = category;
        SetString("itemKind");
        SetString("weaponKind");
        SetString("weaponType");
        SetString("damageDraft");
        SetString("damageType");
        SetString("range");
        SetString("rangeType");
        SetString("hands");
        SetString("ammoType");
        SetString("armorKind");
        SetString("armorType");
        SetString("coverage");
        SetString("caliber");
        SetString("damageModifier");
        SetString("slotId");
        SetString("slotGroup");
        SetString("allowedItemCategories");
        SetString("allowedTags");
        SetString("bodyCompatibilityTags");
        SetString("compatibleSlots");
        SetString("compatibleAmmoTags");
        SetString("compatibilityTags");
        SetString("linkedSkillIds");
        SetString("rarity");
        SetInt("stackSize");
        SetInt("value");
        SetInt("physicalArmor");
        SetInt("magicalArmor");
        SetInt("durability");
        SetInt("ammo");
        SetInt("sortOrder");
        SetDecimal("weight");
        SetBool("isConsumable");
        SetBool("isEquipment");
        SetBool("isTwoHanded");
        SetBool("isExclusive");

        extra["itemType"] = FirstNonEmpty(PayloadReader.GetString(payload, "itemKind"), PayloadReader.GetString(payload, "category"), category);
        extra["weightKg"] = CatalogDecimal(payload, "weight");
        extra["maxStack"] = PayloadReader.GetInt(payload, "stackSize");
        extra["stackable"] = PayloadReader.GetInt(payload, "stackSize") > 1;
        extra["valueAmountDraft"] = PayloadReader.GetInt(payload, "value");
        extra["physicalArmorDraft"] = Convert.ToString(PayloadReader.GetInt(payload, "physicalArmor"), CultureInfo.InvariantCulture) ?? "0";
        extra["magicArmorDraft"] = Convert.ToString(PayloadReader.GetInt(payload, "magicalArmor"), CultureInfo.InvariantCulture) ?? "0";
        extra["equipmentSlotIds"] = SplitCatalogList(PayloadReader.GetString(payload, "compatibleSlots")).Cast<object>().ToArray();
        extra["linkedSkillIds"] = SplitCatalogList(PayloadReader.GetString(payload, "linkedSkillIds")).Cast<object>().ToArray();
        extra["compatibleWeaponIds"] = SplitCatalogList(PayloadReader.GetString(payload, "compatibilityTags")).Cast<object>().ToArray();
        return extra;
    }

    private static string[] CatalogCategories() => new[]
    {
        DefinitionCategoryIds.Item,
        DefinitionCategoryIds.Weapon,
        DefinitionCategoryIds.Armor,
        DefinitionCategoryIds.Ammo,
        DefinitionCategoryIds.EquipmentSlot
    };

    private static string[] CatalogItemCategories() => new[]
    {
        DefinitionCategoryIds.Item,
        DefinitionCategoryIds.Weapon,
        DefinitionCategoryIds.Armor,
        DefinitionCategoryIds.Ammo
    };

    private static Dictionary<string, object> BuildInventoryItemPayloadFromCatalog(UnifiedDefinitionDocument definition, IDictionary<string, object> request)
    {
        var displayName = FirstNonEmpty(PayloadReader.GetString(request, "displayNameOverride"), definition.Name, definition.Id);
        var description = FirstNonEmpty(PayloadReader.GetString(request, "descriptionOverride"), definition.PublicDescription);
        var category = CatalogInventoryCategory(definition);
        var quantity = PayloadReader.GetInt(request, "quantity") ?? 1;
        var requestedPlayerVisible = !request.ContainsKey("isPlayerVisible") || PayloadReader.GetBool(request, "isPlayerVisible");
        var isPlayerVisible = requestedPlayerVisible && IsCatalogDefinitionPlayerVisible(definition);
        var slotId = FirstNonEmpty(PayloadReader.GetString(request, "slotId"), PayloadReader.GetString(request, "slot"));
        var isEquipped = PayloadReader.GetBool(request, "isEquipped") || PayloadReader.GetBool(request, "equipped");
        var tags = definition.Tags ?? new List<string>();

        return new Dictionary<string, object>
        {
            { "itemId", PayloadReader.GetString(request, "itemId") ?? string.Empty },
            { "definitionId", definition.Id },
            { "itemDefinitionId", definition.Id },
            { "definitionCategory", definition.Category },
            { "definitionCode", definition.Id },
            { "name", displayName },
            { "displayName", displayName },
            { "snapshotDisplayName", displayName },
            { "category", category },
            { "snapshotCategory", category },
            { "description", description },
            { "snapshotDescription", description },
            { "quantity", Math.Max(0, quantity) },
            { "isEquipped", isEquipped },
            { "equipped", isEquipped },
            { "slotId", slotId },
            { "slot", slotId },
            { "durability", PayloadReader.GetInt(request, "durability") ?? CatalogExtraInt(definition, "durability") },
            { "ammo", PayloadReader.GetInt(request, "ammo") ?? CatalogExtraInt(definition, "ammo") },
            { "isPlayerVisible", isPlayerVisible },
            { "source", "catalog_definition" },
            { "notes", PayloadReader.GetString(request, "notes") ?? string.Empty },
            { "tagsText", string.Join(",", tags) },
            { "snapshotTagsText", string.Join(",", tags) }
        };
    }

    private static string CatalogInventoryCategory(UnifiedDefinitionDocument definition)
    {
        if (definition == null) return string.Empty;
        var extra = definition.ExtraData ?? new Dictionary<string, object>();
        string Extra(string key) => extra.TryGetValue(key, out var value) && value != null ? Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty : string.Empty;
        return FirstNonEmpty(
            Extra("itemKind"),
            Extra("weaponKind"),
            Extra("armorKind"),
            Extra("ammoType"),
            Extra("itemType"),
            definition.Category);
    }

    private static string[] CatalogExtraKeys() => new[]
    {
        "itemKind", "stackSize", "weight", "value", "rarity", "isConsumable", "isEquipment",
        "weaponKind", "weaponType", "damageDraft", "damageType", "range", "rangeType", "hands", "linkedSkillIds", "ammoType", "compatibleAmmoTags", "isTwoHanded",
        "armorKind", "armorType", "physicalArmor", "magicalArmor", "coverage", "compatibleSlots", "durability",
        "compatibilityTags", "caliber", "damageModifier",
        "slotId", "slotGroup", "allowedItemCategories", "allowedTags", "bodyCompatibilityTags", "isExclusive"
    };

    private static bool IsCatalogDefinitionPlayerVisible(UnifiedDefinitionDocument doc)
    {
        if (doc == null || doc.IsArchived) return false;
        var visibility = (doc.VisibilityRule ?? string.Empty).Trim();
        return string.Equals(visibility, VisibilityRuleIds.Public, StringComparison.OrdinalIgnoreCase)
            || string.Equals(visibility, VisibilityRuleIds.PlayerVisible, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeCatalogCode(string value)
    {
        return (value ?? string.Empty).Trim();
    }

    private static List<string> NormalizeCatalogList(string value)
    {
        var list = SplitCatalogList(value);
        return list.Count == 0 ? new List<string>() : list;
    }

    private static List<string> SplitCatalogList(string? value)
    {
        return (value ?? string.Empty)
            .Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static decimal CatalogDecimal(IDictionary<string, object> payload, string key)
    {
        if (payload == null || !payload.TryGetValue(key, out var value) || value == null) return 0m;
        if (value is decimal d) return d;
        if (value is double db) return Convert.ToDecimal(db, CultureInfo.InvariantCulture);
        if (value is float f) return Convert.ToDecimal(f, CultureInfo.InvariantCulture);
        if (value is int i) return i;
        if (value is long l) return l;
        return decimal.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0m;
    }

    private static string CatalogExtraString(UnifiedDefinitionDocument doc, string key)
    {
        if (doc?.ExtraData == null || !doc.ExtraData.TryGetValue(key, out var value) || value == null) return string.Empty;
        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static int CatalogExtraInt(UnifiedDefinitionDocument doc, string key)
    {
        var raw = CatalogExtraString(doc, key);
        return int.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
    }
}
