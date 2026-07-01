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
    public ResponseEnvelope CharacterSubAttributesGet(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var requestedCharacterId = PayloadReader.GetString(context.Request.Payload, "characterId");
        var character = string.IsNullOrWhiteSpace(requestedCharacterId)
            ? ResolveOwnedCharacter(context, actor)
            : GetCharacter(RequireLength(requestedCharacterId, 8, 128, "characterId"));
        var owner = GetAccount(character.OwnerUserId);
        var isAdmin = IsAdmin(actor);
        if (!isAdmin && (!string.Equals(owner.Id, actor.Id, StringComparison.OrdinalIgnoreCase) || !CanViewCharacter(actor, owner, character)))
        {
            throw new UnauthorizedAccessException("Character subattributes unavailable.");
        }

        var payload = BuildSubAttributeProjection(character, includeHidden: isAdmin);
        _logger.Admin($"character.subattributes.get actor={actor.Login} characterId={character.Id} count={CountPayloadItems(payload["items"])} includeHidden={isAdmin}");
        return Ok("Character subattributes loaded.", payload);
    }

    public ResponseEnvelope CharacterSubAttributesAdminGet(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var characterId = RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 8, 128, "characterId");
        var character = GetCharacter(characterId);
        var payload = BuildSubAttributeProjection(character, includeHidden: true);
        _logger.Admin($"character.subattributes.admin.get actor={actor.Login} characterId={character.Id} count={CountPayloadItems(payload["items"])}");
        return Ok("Character subattributes loaded.", payload);
    }

    public ResponseEnvelope CharacterSubAttributesAdminUpdate(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var characterId = RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 8, 128, "characterId");
        var character = GetCharacter(characterId);
        var ruleSetId = CharacterSubAttributeRuleSetId(character);
        var definitions = CharacterSubAttributeRuntime.LoadDefinitions(_mongo, ruleSetId, includeHidden: true)
            .GroupBy(x => x.SubAttributeId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Last(), StringComparer.OrdinalIgnoreCase);
        var document = CharacterSubAttributeRuntime.EnsureProfile(_mongo, characterId, ruleSetId);
        var profile = document.Profile;
        var rows = ReadSubAttributeRows(context.Request.Payload);
        if (rows.Count == 0) return Error("No subattribute rows supplied.", ResponseStatus.Error, ErrorCode.ValidationFailed);

        var current = (profile.SubAttributes ?? new List<CharacterSubAttributeValue>())
            .Where(x => !string.IsNullOrWhiteSpace(x.SubAttributeId))
            .GroupBy(x => x.SubAttributeId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Last(), StringComparer.OrdinalIgnoreCase);

        var updated = 0;
        var now = DateTime.UtcNow;
        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.SubAttributeId)) return Error("subAttributeId is required.", ResponseStatus.Error, ErrorCode.ValidationFailed);
            if (!definitions.TryGetValue(row.SubAttributeId, out var definition))
                return Error($"Unknown subattribute: {row.SubAttributeId}", ResponseStatus.NotFound, ErrorCode.NotFound);
            if (!definition.IsEditableByGM)
                return Error($"Subattribute is not editable: {row.SubAttributeId}", ResponseStatus.Error, ErrorCode.ValidationFailed);
            if (!string.IsNullOrWhiteSpace(row.ParentAttributeId) &&
                !string.Equals(row.ParentAttributeId, definition.ParentAttributeId, StringComparison.OrdinalIgnoreCase))
                return Error($"Wrong parentAttributeId for subattribute: {row.SubAttributeId}", ResponseStatus.Error, ErrorCode.ValidationFailed);
            if (!row.Value.HasValue) return Error($"value is required for subattribute: {row.SubAttributeId}", ResponseStatus.Error, ErrorCode.ValidationFailed);
            if (row.Value.Value < definition.MinValue || row.Value.Value > definition.MaxValue)
                return Error($"Value for {definition.DisplayName} must be between {definition.MinValue} and {definition.MaxValue}.", ResponseStatus.Error, ErrorCode.ValidationFailed);

            if (!current.TryGetValue(row.SubAttributeId, out var value))
            {
                value = new CharacterSubAttributeValue
                {
                    SubAttributeId = definition.SubAttributeId,
                    ParentAttributeId = definition.ParentAttributeId,
                    Source = "profile_native",
                    IsVisibleToPlayer = definition.IsPlayerVisible
                };
                profile.SubAttributes.Add(value);
                current[row.SubAttributeId] = value;
            }

            value.ParentAttributeId = definition.ParentAttributeId;
            value.BaseValue = row.Value.Value;
            value.CurrentValue = row.Value.Value;
            value.ManualBonus = row.ManualBonus ?? 0;
            value.IsVisibleToPlayer = definition.IsPlayerVisible;
            value.Source = "profile_native";
            value.UpdatedAtUtc = now;
            if (row.Notes != null) value.Notes = row.Notes;
            updated++;
        }

        profile.UpdatedAtUtc = now;
        profile.Revision++;
        _mongo.CharacterSubAttributeProfiles.ReplaceOne(
            Builders<CharacterSubAttributeProfileDocument>.Filter.Eq(x => x.CharacterId, characterId),
            document,
            new ReplaceOptions { IsUpsert = true });

        WriteAudit("character", actor.Id, "updateSubAttributes", characterId);
        _logger.Admin($"character.subattributes.admin.update actor={actor.Login} characterId={characterId} updated={updated} revision={profile.Revision}");
        return Ok("Character subattributes updated.", BuildSubAttributeProjection(character, includeHidden: true));
    }

    public ResponseEnvelope CharacterSubAttributesAdminResetToDefaults(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var characterId = RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 8, 128, "characterId");
        var character = GetCharacter(characterId);
        var ruleSetId = CharacterSubAttributeRuleSetId(character);
        var definitions = CharacterSubAttributeRuntime.LoadDefinitions(_mongo, ruleSetId, includeHidden: true);
        var now = DateTime.UtcNow;
        var profile = new SubAttributeProfile
        {
            CharacterId = characterId,
            RuleSetId = ruleSetId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            Revision = 1,
            SubAttributes = definitions.Select(def => new CharacterSubAttributeValue
            {
                SubAttributeId = def.SubAttributeId,
                ParentAttributeId = def.ParentAttributeId,
                BaseValue = def.DefaultValue,
                CurrentValue = def.DefaultValue,
                ManualBonus = 0,
                Source = "ruleset_default",
                IsVisibleToPlayer = def.IsPlayerVisible,
                UpdatedAtUtc = now
            }).ToList()
        };
        _mongo.CharacterSubAttributeProfiles.ReplaceOne(
            Builders<CharacterSubAttributeProfileDocument>.Filter.Eq(x => x.CharacterId, characterId),
            new CharacterSubAttributeProfileDocument { CharacterId = characterId, Profile = profile },
            new ReplaceOptions { IsUpsert = true });
        WriteAudit("character", actor.Id, "resetSubAttributes", characterId);
        return Ok("Character subattributes reset.", BuildSubAttributeProjection(character, includeHidden: true));
    }

    public ResponseEnvelope DefinitionsSubAttributesAdminList(CommandContext context)
    {
        RequireAdmin(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var ruleSetId = FirstNonEmpty(PayloadReader.GetString(payload, "ruleSetId"), RuleSetIds.FantasyNriDefault);
        var includeArchived = PayloadReader.GetBool(payload, "includeArchived");
        var categories = new[] { DefinitionCategoryIds.SubAttribute, "subAttribute" }.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var filter = Builders<UnifiedDefinitionDocument>.Filter.In(x => x.Category, categories);
        if (!includeArchived) filter &= Builders<UnifiedDefinitionDocument>.Filter.Eq(x => x.IsArchived, false);
        if (!string.IsNullOrWhiteSpace(ruleSetId)) filter &= Builders<UnifiedDefinitionDocument>.Filter.AnyEq(x => x.RuleSetIds, ruleSetId);
        CharacterSubAttributeRuntime.LoadDefinitions(_mongo, ruleSetId, includeHidden: true);
        var items = _mongo.UnifiedDefinitions.Find(filter).ToList()
            .Select(doc => SubAttributeDefinitionPayload(doc, admin: true))
            .OrderBy(x => Convert.ToString(x["parentAttributeId"]), StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => Convert.ToInt32(x["sortOrder"]))
            .ThenBy(x => Convert.ToString(x["displayName"]), StringComparer.OrdinalIgnoreCase)
            .Cast<object>()
            .ToArray();
        return Ok("Subattribute definitions loaded.", new Dictionary<string, object> { { "items", items }, { "sourceOfTruth", "unified_definitions" } });
    }

    public ResponseEnvelope DefinitionsSubAttributesAdminCreateOrUpdate(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var code = FirstNonEmpty(PayloadReader.GetString(payload, "subAttributeId"), PayloadReader.GetString(payload, "code"), PayloadReader.GetString(payload, "id"));
        if (string.IsNullOrWhiteSpace(code)) return Error("subAttributeId is required.", ResponseStatus.Error, ErrorCode.ValidationFailed);
        var parentAttributeId = FirstNonEmpty(PayloadReader.GetString(payload, "parentAttributeId"), CharacterAttributeIds.Strength);
        if (!IsKnownParentAttribute(parentAttributeId))
            return Error($"Unknown parentAttributeId: {parentAttributeId}", ResponseStatus.Error, ErrorCode.ValidationFailed);

        var min = PayloadReader.GetInt(payload, "minValue");
        var max = PayloadReader.GetInt(payload, "maxValue");
        var def = PayloadReader.GetInt(payload, "defaultValue");
        if (max < min) return Error("maxValue must be greater than or equal to minValue.", ResponseStatus.Error, ErrorCode.ValidationFailed);
        if (def < min || def > max) return Error("defaultValue must be within min/max.", ResponseStatus.Error, ErrorCode.ValidationFailed);

        var now = DateTime.UtcNow;
        var ruleSetId = FirstNonEmpty(PayloadReader.GetString(payload, "ruleSetId"), RuleSetIds.FantasyNriDefault);
        var existing = _mongo.UnifiedDefinitions.Find(Builders<UnifiedDefinitionDocument>.Filter.In(x => x.Category, new[] { DefinitionCategoryIds.SubAttribute, "subAttribute" })
            & Builders<UnifiedDefinitionDocument>.Filter.Eq(x => x.Id, code)).FirstOrDefault();
        var doc = existing ?? new UnifiedDefinitionDocument { Id = code, CreatedAtUtc = now, SourceDocument = "admin_subattribute_definition" };
        doc.Id = code;
        doc.Category = DefinitionCategoryIds.SubAttribute;
        doc.RuleSetIds = new List<string> { ruleSetId };
        doc.Name = FirstNonEmpty(PayloadReader.GetString(payload, "displayName"), PayloadReader.GetString(payload, "name"), code);
        doc.PublicDescription = PayloadReader.GetString(payload, "description") ?? doc.PublicDescription ?? string.Empty;
        doc.GMDescription = PayloadReader.GetString(payload, "gmDescription") ?? doc.GMDescription ?? string.Empty;
        doc.VisibilityRule = PayloadReader.GetBool(payload, "isPlayerVisible") ? VisibilityRuleIds.Public : VisibilityRuleIds.GmOnly;
        doc.IsArchived = false;
        doc.UpdatedAtUtc = now;
        doc.Tags = SplitCsv(PayloadReader.GetString(payload, "tags") ?? string.Empty);
        doc.ExtraData = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            { "subAttributeId", code },
            { "code", code },
            { "displayName", doc.Name },
            { "parentAttributeId", parentAttributeId },
            { "attributeSetId", FirstNonEmpty(PayloadReader.GetString(payload, "attributeSetId"), "fantasy_primary") },
            { "sortOrder", PayloadReader.GetInt(payload, "sortOrder") },
            { "minValue", min },
            { "maxValue", max },
            { "defaultValue", def },
            { "isPlayerVisible", PayloadReader.GetBool(payload, "isPlayerVisible") },
            { "isEditableByGM", !payload.ContainsKey("isEditableByGM") || PayloadReader.GetBool(payload, "isEditableByGM") },
            { "isRollableModifier", !payload.ContainsKey("isRollableModifier") || PayloadReader.GetBool(payload, "isRollableModifier") },
            { "appliesToSkillChecks", !payload.ContainsKey("appliesToSkillChecks") || PayloadReader.GetBool(payload, "appliesToSkillChecks") }
        };
        doc.ServerOnlyData ??= new Dictionary<string, object>();
        doc.ServerOnlyData["updatedByUserId"] = actor.Id;
        CharacterSubAttributeRuntime.UpsertDefinition(_mongo, doc);
        _logger.Admin($"definitions.subattributes.admin.upsert actor={actor.Login} subAttributeId={code} parent={parentAttributeId}");
        return Ok("Subattribute definition saved.", new Dictionary<string, object> { { "item", SubAttributeDefinitionPayload(doc, admin: true) }, { "sourceOfTruth", "unified_definitions" } });
    }

    public ResponseEnvelope DefinitionsSubAttributesAdminArchive(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var code = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, "subAttributeId"), PayloadReader.GetString(context.Request.Payload, "code"), PayloadReader.GetString(context.Request.Payload, "id"));
        if (string.IsNullOrWhiteSpace(code)) return Error("subAttributeId is required.", ResponseStatus.Error, ErrorCode.ValidationFailed);
        var filter = Builders<UnifiedDefinitionDocument>.Filter.In(x => x.Category, new[] { DefinitionCategoryIds.SubAttribute, "subAttribute" })
            & Builders<UnifiedDefinitionDocument>.Filter.Eq(x => x.Id, code);
        var update = Builders<UnifiedDefinitionDocument>.Update.Set(x => x.IsArchived, true).Set(x => x.UpdatedAtUtc, DateTime.UtcNow);
        var result = _mongo.UnifiedDefinitions.UpdateOne(filter, update);
        if (result.MatchedCount == 0) return Error("Subattribute definition not found.", ResponseStatus.NotFound, ErrorCode.NotFound);
        _logger.Admin($"definitions.subattributes.admin.archive actor={actor.Login} subAttributeId={code}");
        return Ok("Subattribute definition archived.", new Dictionary<string, object> { { "subAttributeId", code } });
    }

    private Dictionary<string, object> BuildSubAttributeProjection(Character character, bool includeHidden)
    {
        var ruleSetId = CharacterSubAttributeRuleSetId(character);
        var byParent = CharacterSubAttributeRuntime.BuildSubAttributeViewMap(_mongo, character.Id, ruleSetId, includeHidden);
        var items = byParent.Values.SelectMany(x => x).ToArray();
        var groups = byParent
            .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Select(x => (object)new Dictionary<string, object>
            {
                { "parentAttributeId", x.Key },
                { "items", x.Value }
            })
            .ToArray();
        return new Dictionary<string, object>
        {
            { "items", items },
            { "groups", groups },
            { "sourceOfTruth", "character_subattribute_profiles" },
            { "definitionSourceOfTruth", "unified_definitions" },
            { "ruleSetId", ruleSetId }
        };
    }

    private string CharacterSubAttributeRuleSetId(Character character)
    {
        var attributeProfile = _mongo.CharacterAttributeProfiles.Find(Builders<CharacterAttributeProfileDocument>.Filter.Eq(x => x.CharacterId, character.Id)).FirstOrDefault()?.Profile;
        return FirstNonEmpty(attributeProfile?.RuleSetId, RuleSetIds.FantasyNriDefault);
    }

    private static List<SubAttributeUpdateRow> ReadSubAttributeRows(Dictionary<string, object> payload)
    {
        var result = new List<SubAttributeUpdateRow>();
        var raw = payload != null && payload.TryGetValue("subAttributes", out var subAttributesRaw)
            ? subAttributesRaw
            : payload != null && payload.TryGetValue("items", out var itemsRaw)
                ? itemsRaw
                : null;
        var flattened = CoerceFlattenedKeyValueMap(raw);
        if (flattened != null && flattened.Count > 0)
        {
            AddSubAttributeRow(result, flattened);
            return result;
        }

        foreach (var item in ToSubAttributeObjectList(raw))
        {
            var map = CoerceSubAttributeMap(item);
            if (map == null) continue;
            AddSubAttributeRow(result, map);
        }
        return result;
    }

    private static void AddSubAttributeRow(List<SubAttributeUpdateRow> result, IDictionary<string, object> map)
    {
        var subAttributeId = FirstNonEmpty(GetMapString(map, "subAttributeId"), GetMapString(map, "id"), GetMapString(map, "code"));
        var parent = GetMapString(map, "parentAttributeId");
        var valueText = FirstNonEmpty(GetMapString(map, "value"), GetMapString(map, "currentValue"));
        result.Add(new SubAttributeUpdateRow
        {
            SubAttributeId = subAttributeId,
            ParentAttributeId = parent,
            Value = int.TryParse(valueText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null,
            ManualBonus = int.TryParse(GetMapString(map, "manualBonus"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var manual) ? manual : null,
            Notes = map.ContainsKey("notes") ? GetMapString(map, "notes") : null
        });
    }

    private static IEnumerable<object> ToSubAttributeObjectList(object? raw)
    {
        if (raw == null) yield break;
        if (raw is string) yield break;
        if (raw is IDictionary)
        {
            yield return raw;
            yield break;
        }
        if (raw is IEnumerable enumerable)
        {
            foreach (var item in enumerable) yield return item ?? string.Empty;
            yield break;
        }
        yield return raw;
    }

    private static IDictionary<string, object>? CoerceSubAttributeMap(object? item)
    {
        if (item == null) return null;
        if (item is IDictionary<string, object> typed) return typed;
        if (item is IDictionary rawMap)
        {
            var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry entry in rawMap)
            {
                if (entry.Key != null) dict[Convert.ToString(entry.Key) ?? string.Empty] = entry.Value ?? string.Empty;
            }
            return dict;
        }

        var boxed = new Dictionary<string, object> { { "item", item } };
        return PayloadReader.GetDictionary(boxed, "item");
    }

    private static Dictionary<string, object>? CoerceFlattenedKeyValueMap(object? raw)
    {
        if (raw == null || raw is string || raw is IDictionary) return null;
        if (raw is not IEnumerable enumerable) return null;

        var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        var sawAny = false;
        foreach (var item in enumerable)
        {
            if (item == null) continue;
            var type = item.GetType();
            var keyProperty = type.GetProperty("Key");
            var valueProperty = type.GetProperty("Value");
            if (keyProperty == null || valueProperty == null) return null;
            var key = Convert.ToString(keyProperty.GetValue(item), CultureInfo.InvariantCulture);
            if (string.IsNullOrWhiteSpace(key)) continue;
            result[key] = valueProperty.GetValue(item) ?? string.Empty;
            sawAny = true;
        }

        return sawAny && result.ContainsKey("subAttributeId") ? result : null;
    }

    private static Dictionary<string, object> SubAttributeDefinitionPayload(UnifiedDefinitionDocument doc, bool admin)
    {
        var extra = doc.ExtraData ?? new Dictionary<string, object>();
        var payload = new Dictionary<string, object>
        {
            { "definitionId", doc.Id ?? string.Empty },
            { "subAttributeId", FirstNonEmpty(GetMapString(extra, "subAttributeId"), doc.Id) },
            { "id", doc.Id ?? string.Empty },
            { "code", FirstNonEmpty(GetMapString(extra, "code"), doc.Id) },
            { "category", doc.Category ?? string.Empty },
            { "displayName", FirstNonEmpty(GetMapString(extra, "displayName"), doc.Name, doc.Id) },
            { "name", FirstNonEmpty(doc.Name, GetMapString(extra, "displayName"), doc.Id) },
            { "description", doc.PublicDescription ?? string.Empty },
            { "parentAttributeId", GetMapString(extra, "parentAttributeId") },
            { "attributeSetId", GetMapString(extra, "attributeSetId") },
            { "ruleSetIds", (doc.RuleSetIds ?? new List<string>()).Cast<object>().ToArray() },
            { "sortOrder", GetMapInt(extra, "sortOrder") },
            { "minValue", GetMapInt(extra, "minValue") },
            { "maxValue", GetMapInt(extra, "maxValue", 30) },
            { "defaultValue", GetMapInt(extra, "defaultValue") },
            { "isPlayerVisible", !IsHiddenVisibility(doc.VisibilityRule) && GetMapBool(extra, "isPlayerVisible", true) },
            { "isEditableByGM", GetMapBool(extra, "isEditableByGM", true) },
            { "isRollableModifier", GetMapBool(extra, "isRollableModifier", true) },
            { "appliesToSkillChecks", GetMapBool(extra, "appliesToSkillChecks", true) },
            { "isArchived", doc.IsArchived },
            { "sourceOfTruth", "unified_definitions" }
        };
        if (admin)
        {
            payload["gmDescription"] = doc.GMDescription ?? string.Empty;
            payload["visibilityRule"] = doc.VisibilityRule ?? string.Empty;
        }
        return payload;
    }

    private static bool IsKnownParentAttribute(string parentAttributeId)
    {
        return new[]
        {
            CharacterAttributeIds.Strength,
            CharacterAttributeIds.Dexterity,
            CharacterAttributeIds.Endurance,
            CharacterAttributeIds.Intellect,
            CharacterAttributeIds.Wisdom,
            CharacterAttributeIds.Charisma
        }.Contains(parentAttributeId ?? string.Empty, StringComparer.OrdinalIgnoreCase);
    }

    private static string GetMapString(IDictionary<string, object> map, string key)
    {
        if (map == null || string.IsNullOrWhiteSpace(key)) return string.Empty;
        foreach (var pair in map)
        {
            if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                return Convert.ToString(pair.Value, CultureInfo.InvariantCulture) ?? string.Empty;
            }
        }
        return string.Empty;
    }

    private static int GetMapInt(IDictionary<string, object> map, string key, int fallback = 0)
    {
        return int.TryParse(GetMapString(map, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : fallback;
    }

    private static bool GetMapBool(IDictionary<string, object> map, string key, bool fallback)
    {
        var raw = GetMapString(map, key);
        return bool.TryParse(raw, out var value) ? value : fallback;
    }

    private static bool IsHiddenVisibility(string? visibility)
    {
        var normalized = (visibility ?? string.Empty).Trim().ToLowerInvariant();
        return normalized is "hidden" or "gm_only" or "server_only" or "super_admin_only";
    }

    private static List<string> SplitCsv(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return new List<string>();
        return raw.Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private sealed class SubAttributeUpdateRow
    {
        public string SubAttributeId { get; set; } = string.Empty;
        public string ParentAttributeId { get; set; } = string.Empty;
        public int? Value { get; set; }
        public int? ManualBonus { get; set; }
        public string? Notes { get; set; }
    }
}
