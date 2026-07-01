using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using MongoDB.Driver;
using Nri.Server.Infrastructure;
using Nri.Shared.Domain;

namespace Nri.Server.Application;

internal sealed class SubAttributeDefinitionProjection
{
    public string SubAttributeId { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ParentAttributeId { get; set; } = string.Empty;
    public string AttributeSetId { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public int MinValue { get; set; }
    public int MaxValue { get; set; } = 30;
    public int DefaultValue { get; set; }
    public bool IsPlayerVisible { get; set; } = true;
    public bool IsEditableByGM { get; set; } = true;
    public bool IsRollableModifier { get; set; } = true;
    public bool AppliesToSkillChecks { get; set; } = true;
    public string PublicDescription { get; set; } = string.Empty;
    public string GMDescription { get; set; } = string.Empty;
    public string SourceRuleSetId { get; set; } = RuleSetIds.FantasyNriDefault;
}

internal static class CharacterSubAttributeRuntime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static List<SubAttributeDefinitionProjection> LoadDefinitions(MongoContext mongo, string ruleSetId, bool includeHidden)
    {
        if (mongo == null) return new List<SubAttributeDefinitionProjection>();
        ruleSetId = string.IsNullOrWhiteSpace(ruleSetId) ? RuleSetIds.FantasyNriDefault : ruleSetId;
        EnsureStarterDefinitions(mongo, ruleSetId);

        var categories = new[] { DefinitionCategoryIds.SubAttribute, "subAttribute" }.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var filter = Builders<UnifiedDefinitionDocument>.Filter.In(x => x.Category, categories)
            & Builders<UnifiedDefinitionDocument>.Filter.Eq(x => x.IsArchived, false);
        if (!string.IsNullOrWhiteSpace(ruleSetId))
        {
            filter &= Builders<UnifiedDefinitionDocument>.Filter.AnyEq(x => x.RuleSetIds, ruleSetId);
        }

        var definitions = mongo.UnifiedDefinitions.Find(filter).ToList()
            .Select(doc => ToProjection(doc, ruleSetId))
            .Where(x => !string.IsNullOrWhiteSpace(x.SubAttributeId) && !string.IsNullOrWhiteSpace(x.ParentAttributeId))
            .Where(x => includeHidden || x.IsPlayerVisible)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return definitions;
    }

    public static CharacterSubAttributeProfileDocument EnsureProfile(MongoContext mongo, string characterId, string ruleSetId)
    {
        if (mongo == null) throw new ArgumentNullException(nameof(mongo));
        if (string.IsNullOrWhiteSpace(characterId)) throw new ArgumentException("characterId is required.", nameof(characterId));

        ruleSetId = string.IsNullOrWhiteSpace(ruleSetId) ? RuleSetIds.FantasyNriDefault : ruleSetId;
        var definitions = LoadDefinitions(mongo, ruleSetId, includeHidden: true);
        var filter = Builders<CharacterSubAttributeProfileDocument>.Filter.Eq(x => x.CharacterId, characterId);
        var document = mongo.CharacterSubAttributeProfiles.Find(filter).FirstOrDefault();
        var now = DateTime.UtcNow;

        if (document == null)
        {
            document = new CharacterSubAttributeProfileDocument
            {
                CharacterId = characterId,
                Profile = new SubAttributeProfile
                {
                    CharacterId = characterId,
                    RuleSetId = ruleSetId,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now,
                    ProfileVersion = 1,
                    SchemaVersion = 1
                }
            };
        }

        document.CharacterId = characterId;
        document.Profile ??= new SubAttributeProfile();
        document.Profile.CharacterId = characterId;
        if (string.IsNullOrWhiteSpace(document.Profile.RuleSetId)) document.Profile.RuleSetId = ruleSetId;
        if (document.Profile.CreatedAtUtc == default) document.Profile.CreatedAtUtc = now;
        if (document.Profile.SubAttributes == null) document.Profile.SubAttributes = new List<CharacterSubAttributeValue>();

        var changed = false;
        var existing = document.Profile.SubAttributes
            .Where(x => !string.IsNullOrWhiteSpace(x.SubAttributeId))
            .GroupBy(x => x.SubAttributeId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Last(), StringComparer.OrdinalIgnoreCase);

        foreach (var definition in definitions)
        {
            if (!existing.TryGetValue(definition.SubAttributeId, out var value))
            {
                value = new CharacterSubAttributeValue
                {
                    SubAttributeId = definition.SubAttributeId,
                    ParentAttributeId = definition.ParentAttributeId,
                    BaseValue = definition.DefaultValue,
                    CurrentValue = definition.DefaultValue,
                    ManualBonus = 0,
                    Source = "ruleset_default",
                    IsVisibleToPlayer = definition.IsPlayerVisible,
                    UpdatedAtUtc = now
                };
                document.Profile.SubAttributes.Add(value);
                existing[definition.SubAttributeId] = value;
                changed = true;
                continue;
            }

            if (!string.Equals(value.ParentAttributeId, definition.ParentAttributeId, StringComparison.OrdinalIgnoreCase))
            {
                value.ParentAttributeId = definition.ParentAttributeId;
                changed = true;
            }

            if (value.IsVisibleToPlayer != definition.IsPlayerVisible)
            {
                value.IsVisibleToPlayer = definition.IsPlayerVisible;
                changed = true;
            }

            if (value.UpdatedAtUtc == default)
            {
                value.UpdatedAtUtc = now;
                changed = true;
            }
        }

        if (changed || string.IsNullOrWhiteSpace(document.Id))
        {
            document.Profile.UpdatedAtUtc = now;
            document.Profile.Revision++;
            mongo.CharacterSubAttributeProfiles.ReplaceOne(filter, document, new ReplaceOptions { IsUpsert = true });
        }

        return document;
    }

    public static Dictionary<string, object[]> BuildSubAttributeViewMap(MongoContext mongo, string characterId, string ruleSetId, bool includeHidden)
    {
        var definitions = LoadDefinitions(mongo, ruleSetId, includeHidden);
        var profile = EnsureProfile(mongo, characterId, ruleSetId).Profile;
        var values = (profile.SubAttributes ?? new List<CharacterSubAttributeValue>())
            .Where(x => !string.IsNullOrWhiteSpace(x.SubAttributeId))
            .GroupBy(x => x.SubAttributeId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Last(), StringComparer.OrdinalIgnoreCase);

        return definitions
            .Where(def => includeHidden || def.IsPlayerVisible)
            .GroupBy(def => def.ParentAttributeId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderBy(def => def.SortOrder)
                    .ThenBy(def => def.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .Select(def =>
                    {
                        values.TryGetValue(def.SubAttributeId, out var current);
                        var value = current?.CurrentValue ?? def.DefaultValue;
                        var item = new Dictionary<string, object>
                        {
                            { "subAttributeId", def.SubAttributeId },
                            { "id", def.SubAttributeId },
                            { "code", def.Code },
                            { "parentAttributeId", def.ParentAttributeId },
                            { "displayName", def.DisplayName },
                            { "label", def.DisplayName },
                            { "publicDescription", def.PublicDescription },
                            { "description", def.PublicDescription },
                            { "baseValue", current?.BaseValue ?? value },
                            { "currentValue", value },
                            { "value", value },
                            { "manualBonus", current?.ManualBonus ?? 0 },
                            { "minValue", def.MinValue },
                            { "maxValue", def.MaxValue },
                            { "defaultValue", def.DefaultValue },
                            { "sortOrder", def.SortOrder },
                            { "attributeSetId", def.AttributeSetId },
                            { "sourceRuleSetId", def.SourceRuleSetId },
                            { "isPlayerVisible", def.IsPlayerVisible && (current?.IsVisibleToPlayer ?? true) },
                            { "isEditableByGM", def.IsEditableByGM },
                            { "source", current?.Source ?? "ruleset_default" },
                            { "sourceOfTruth", "character_subattribute_profiles" }
                        };
                        if (includeHidden) item["gmDescription"] = def.GMDescription;
                        return (object)item;
                    })
                    .ToArray(),
                StringComparer.OrdinalIgnoreCase);
    }

    public static Dictionary<string, CharacterSubAttributeValue> BuildValueMap(MongoContext mongo, string characterId, string ruleSetId)
    {
        var profile = EnsureProfile(mongo, characterId, ruleSetId).Profile;
        return (profile.SubAttributes ?? new List<CharacterSubAttributeValue>())
            .Where(x => !string.IsNullOrWhiteSpace(x.SubAttributeId))
            .GroupBy(x => x.SubAttributeId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Last(), StringComparer.OrdinalIgnoreCase);
    }

    public static void UpsertDefinition(MongoContext mongo, UnifiedDefinitionDocument definition)
    {
        if (mongo == null || definition == null || string.IsNullOrWhiteSpace(definition.Id)) return;
        definition.Category = string.IsNullOrWhiteSpace(definition.Category) ? DefinitionCategoryIds.SubAttribute : definition.Category;
        definition.UpdatedAtUtc = DateTime.UtcNow;
        if (definition.CreatedAtUtc == default) definition.CreatedAtUtc = definition.UpdatedAtUtc;
        definition.ExtraData ??= new Dictionary<string, object>();
        definition.RuleSetIds ??= new List<string>();
        if (!definition.RuleSetIds.Contains(RuleSetIds.FantasyNriDefault, StringComparer.OrdinalIgnoreCase))
        {
            definition.RuleSetIds.Add(RuleSetIds.FantasyNriDefault);
        }

        mongo.UnifiedDefinitions.ReplaceOne(
            Builders<UnifiedDefinitionDocument>.Filter.Eq(x => x.Category, definition.Category) & Builders<UnifiedDefinitionDocument>.Filter.Eq(x => x.Id, definition.Id),
            definition,
            new ReplaceOptions { IsUpsert = true });
    }

    private static void EnsureStarterDefinitions(MongoContext mongo, string ruleSetId)
    {
        if (!string.Equals(ruleSetId, RuleSetIds.FantasyNriDefault, StringComparison.OrdinalIgnoreCase)) return;
        var exists = mongo.UnifiedDefinitions
            .Find(Builders<UnifiedDefinitionDocument>.Filter.In(x => x.Category, new[] { DefinitionCategoryIds.SubAttribute, "subAttribute" })
                & Builders<UnifiedDefinitionDocument>.Filter.AnyEq(x => x.RuleSetIds, ruleSetId)
                & Builders<UnifiedDefinitionDocument>.Filter.Eq(x => x.IsArchived, false))
            .Limit(1)
            .Any();
        if (exists) return;

        var path = ResolveStarterSubAttributesPath();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
        var json = File.ReadAllText(path);
        var loaded = JsonSerializer.Deserialize<List<UnifiedDefinitionDocument>>(json, JsonOptions) ?? new List<UnifiedDefinitionDocument>();
        foreach (var definition in loaded)
        {
            NormalizeLoadedDefinition(definition, ruleSetId);
            UpsertDefinition(mongo, definition);
        }
    }

    private static string ResolveStarterSubAttributesPath()
    {
        var relative = Path.Combine("Nri.Server", "Content", "DefinitionPacks", "fantasy_nri_default_starter", "subattributes.json");
        var probes = new List<string>
        {
            Path.Combine(Environment.CurrentDirectory, relative),
            Path.Combine(Environment.CurrentDirectory, "Content", "DefinitionPacks", "fantasy_nri_default_starter", "subattributes.json"),
            Path.Combine(AppContext.BaseDirectory, "Content", "DefinitionPacks", "fantasy_nri_default_starter", "subattributes.json")
        };

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 8 && dir != null; i++, dir = dir.Parent)
        {
            probes.Add(Path.Combine(dir.FullName, relative));
            probes.Add(Path.Combine(dir.FullName, "Content", "DefinitionPacks", "fantasy_nri_default_starter", "subattributes.json"));
        }

        return probes.FirstOrDefault(File.Exists) ?? string.Empty;
    }

    private static void NormalizeLoadedDefinition(UnifiedDefinitionDocument definition, string ruleSetId)
    {
        definition.Id = (definition.Id ?? string.Empty).Trim();
        definition.Category = string.IsNullOrWhiteSpace(definition.Category) ? DefinitionCategoryIds.SubAttribute : definition.Category.Trim();
        definition.RuleSetIds ??= new List<string>();
        if (!definition.RuleSetIds.Contains(ruleSetId, StringComparer.OrdinalIgnoreCase)) definition.RuleSetIds.Add(ruleSetId);
        definition.Name = (definition.Name ?? string.Empty).Trim();
        definition.PublicDescription ??= string.Empty;
        definition.GMDescription ??= string.Empty;
        definition.VisibilityRule = string.IsNullOrWhiteSpace(definition.VisibilityRule) ? VisibilityRuleIds.Public : definition.VisibilityRule.Trim();
        definition.Tags ??= new List<string>();
        definition.ServerOnlyData = ConvertDictionary(definition.ServerOnlyData);
        definition.ExtraData = ConvertDictionary(definition.ExtraData);
        if (definition.CreatedAtUtc == default) definition.CreatedAtUtc = DateTime.UtcNow;
        if (definition.UpdatedAtUtc == default) definition.UpdatedAtUtc = DateTime.UtcNow;
        definition.SourceDocument = string.IsNullOrWhiteSpace(definition.SourceDocument) ? "fantasy_nri_default_starter/subattributes.json" : definition.SourceDocument;
    }

    private static Dictionary<string, object> ConvertDictionary(Dictionary<string, object>? source)
    {
        var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in source ?? new Dictionary<string, object>())
        {
            result[pair.Key] = ConvertJsonValue(pair.Value);
        }
        return result;
    }

    private static object ConvertJsonValue(object? value)
    {
        if (value is JsonElement json)
        {
            return json.ValueKind switch
            {
                JsonValueKind.String => json.GetString() ?? string.Empty,
                JsonValueKind.Number => json.TryGetInt32(out var i) ? i : json.TryGetInt64(out var l) ? l : json.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Array => json.EnumerateArray().Select(x => ConvertJsonValue(x)).ToArray(),
                JsonValueKind.Object => json.EnumerateObject().ToDictionary(p => p.Name, p => ConvertJsonValue(p.Value), StringComparer.OrdinalIgnoreCase),
                _ => string.Empty
            };
        }

        return value ?? string.Empty;
    }

    private static SubAttributeDefinitionProjection ToProjection(UnifiedDefinitionDocument definition, string ruleSetId)
    {
        var extra = definition.ExtraData ?? new Dictionary<string, object>();
        var id = FirstNonEmpty(GetExtraString(extra, "subAttributeId"), GetExtraString(extra, "code"), definition.Id);
        var displayName = FirstNonEmpty(GetExtraString(extra, "displayName"), GetExtraString(extra, "displayNameRu"), definition.Name, id);
        return new SubAttributeDefinitionProjection
        {
            SubAttributeId = id,
            Code = FirstNonEmpty(GetExtraString(extra, "code"), id),
            DisplayName = displayName,
            ParentAttributeId = FirstNonEmpty(GetExtraString(extra, "parentAttributeId"), GetExtraString(extra, "attributeId")),
            AttributeSetId = FirstNonEmpty(GetExtraString(extra, "attributeSetId"), "fantasy_primary"),
            SortOrder = GetExtraInt(extra, "sortOrder", 1000),
            MinValue = GetExtraInt(extra, "minValue", 0),
            MaxValue = GetExtraInt(extra, "maxValue", 30),
            DefaultValue = GetExtraInt(extra, "defaultValue", 0),
            IsPlayerVisible = !IsHiddenVisibility(definition.VisibilityRule) && GetExtraBool(extra, "isPlayerVisible", true),
            IsEditableByGM = GetExtraBool(extra, "isEditableByGM", true),
            IsRollableModifier = GetExtraBool(extra, "isRollableModifier", true),
            AppliesToSkillChecks = GetExtraBool(extra, "appliesToSkillChecks", true),
            PublicDescription = FirstNonEmpty(definition.PublicDescription, GetExtraString(extra, "description")),
            GMDescription = definition.GMDescription ?? string.Empty,
            SourceRuleSetId = (definition.RuleSetIds ?? new List<string>()).FirstOrDefault(x => string.Equals(x, ruleSetId, StringComparison.OrdinalIgnoreCase)) ?? ruleSetId
        };
    }

    private static bool IsHiddenVisibility(string? visibility)
    {
        var normalized = (visibility ?? string.Empty).Trim().ToLowerInvariant();
        return normalized is "hidden" or "gm_only" or "server_only" or "super_admin_only";
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    }

    private static string GetExtraString(IDictionary<string, object> extra, string key)
    {
        if (extra == null || string.IsNullOrWhiteSpace(key)) return string.Empty;
        if (!TryGetExtra(extra, key, out var value) || value == null) return string.Empty;
        if (value is JsonElement json) return Convert.ToString(ConvertJsonValue(json), CultureInfo.InvariantCulture) ?? string.Empty;
        if (value is IEnumerable enumerable && value is not string)
        {
            return string.Join(",", enumerable.Cast<object>().Select(x => Convert.ToString(x, CultureInfo.InvariantCulture)).Where(x => !string.IsNullOrWhiteSpace(x)));
        }
        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static int GetExtraInt(IDictionary<string, object> extra, string key, int fallback)
    {
        var raw = GetExtraString(extra, key);
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : fallback;
    }

    private static bool GetExtraBool(IDictionary<string, object> extra, string key, bool fallback)
    {
        if (extra != null && TryGetExtra(extra, key, out var value))
        {
            if (value is bool typed) return typed;
            if (value is JsonElement json)
            {
                if (json.ValueKind == JsonValueKind.True) return true;
                if (json.ValueKind == JsonValueKind.False) return false;
            }
        }

        var raw = GetExtraString(extra, key);
        return bool.TryParse(raw, out var parsed) ? parsed : fallback;
    }

    private static bool TryGetExtra(IDictionary<string, object> extra, string key, out object? value)
    {
        foreach (var pair in extra)
        {
            if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                value = pair.Value;
                return true;
            }
        }

        value = null;
        return false;
    }
}
