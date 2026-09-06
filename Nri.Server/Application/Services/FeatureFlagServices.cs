using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MongoDB.Bson;
using MongoDB.Driver;
using Nri.Server.Infrastructure;
using Nri.Server.Logging;
using Nri.Shared.Configuration;
using Nri.Shared.Domain;

namespace Nri.Server.Application.Services;

public interface IFeatureFlagProvider
{
    bool IsEnabled(string flagName);
    bool GetBool(string flagName, bool defaultValue);
    IReadOnlyCollection<string> GetKnownFlags();
    FeatureFlagSnapshot GetFeatureFlagSnapshot();
    FeatureFlagSnapshotItem? GetFeatureFlag(string flagName);
    FeatureFlagSnapshotItem SetOverride(string flagName, bool value, string updatedByUserId, string reason);
    FeatureFlagSnapshotItem ClearOverride(string flagName, string updatedByUserId);
}

public sealed class FeatureFlagSnapshot
{
    public string Environment { get; set; } = string.Empty;
    public string ProfileName { get; set; } = FeatureProfiles.MinimalSafe;
    public bool OverridesAllowed { get; set; }
    public List<FeatureFlagSnapshotItem> Flags { get; set; } = new List<FeatureFlagSnapshotItem>();
}

public sealed class FeatureFlagSnapshotItem
{
    public string Name { get; set; } = string.Empty;
    public string CanonicalKey { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public bool DefaultValue { get; set; }
    public bool EffectiveValue { get; set; }
    public string Source { get; set; } = "default";
    public string Description { get; set; } = string.Empty;
    public DateTime? UpdatedAtUtc { get; set; }
    public string UpdatedByUserId { get; set; } = string.Empty;
    public List<string> Aliases { get; set; } = new List<string>();
    public bool AliasesDeprecated { get; set; } = true;
    public string IntendedPreReleaseState { get; set; } = "intentionally_disabled";
}

public sealed class RuntimeFeatureFlagProvider : IFeatureFlagProvider
{
    private const string EnvPrefix = "NRI_FEATUREFLAG_";
    private readonly Dictionary<string, FeatureFlagDefinition> _definitions;
    private readonly Dictionary<string, bool> _configOverrides;
    private readonly Dictionary<string, bool> _envOverrides;
    private readonly IRepository<FeatureFlagOverrideState>? _databaseOverrides;
    private readonly IMongoCollection<BsonDocument>? _databaseOverrideDocuments;
    private readonly bool _configOverridesAllowed;
    private readonly bool _databaseOverridesAllowed;
    private readonly string _environment;
    private readonly IServerLogger _logger;

    public RuntimeFeatureFlagProvider(
        ServerConfig config,
        IServerLogger logger,
        IRepository<FeatureFlagOverrideState>? databaseOverrides = null,
        IMongoCollection<BsonDocument>? databaseOverrideDocuments = null)
    {
        config ??= new ServerConfig();
        _logger = logger;
        _environment = string.IsNullOrWhiteSpace(config.Environment) ? "Production" : config.Environment.Trim();
        _configOverridesAllowed = config.AllowRuntimeFeatureFlagOverrides || IsDevelopmentOrTest(_environment);
        _databaseOverridesAllowed = databaseOverrides != null;
        _databaseOverrides = databaseOverrides;
        _databaseOverrideDocuments = databaseOverrideDocuments;
        _definitions = BuildDefinitions(_environment);
        _configOverrides = NormalizeOverrides(config.FeatureFlagOverrides, "config");
        _envOverrides = ReadEnvironmentOverrides();

        if (!_configOverridesAllowed && (_configOverrides.Count > 0 || _envOverrides.Count > 0))
            _logger.Admin($"feature_flags.overrides_ignored environment={_environment} configCount={_configOverrides.Count} envCount={_envOverrides.Count}");
        else
            _logger.Debug($"feature_flags.initialized environment={_environment} configOverridesAllowed={_configOverridesAllowed} databaseOverridesAllowed={_databaseOverridesAllowed} known={_definitions.Count}");
    }

    public bool IsEnabled(string flagName) => GetBool(flagName, false);

    public bool GetBool(string flagName, bool defaultValue)
    {
        var normalized = NormalizeKey(flagName);
        if (string.IsNullOrWhiteSpace(normalized)) return defaultValue;

        if (!_definitions.TryGetValue(normalized, out var definition))
            return defaultValue;

        var key = NormalizeKey(definition.CanonicalName);
        if (_configOverridesAllowed && _envOverrides.TryGetValue(key, out var envValue))
            return envValue;

        var dbOverride = LoadDatabaseOverride(key);
        if (dbOverride != null)
            return dbOverride.Value;

        if (_configOverridesAllowed && _configOverrides.TryGetValue(key, out var configValue))
            return configValue;

        return definition.DefaultValue;
    }

    public IReadOnlyCollection<string> GetKnownFlags()
    {
        return _definitions.Values
            .Where(x => x.IsCanonical)
            .Select(x => x.Name)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public FeatureFlagSnapshot GetFeatureFlagSnapshot()
    {
        var items = _definitions.Values
            .Where(x => x.IsCanonical)
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Select(x =>
            {
                return BuildSnapshotItem(x);
            })
            .ToList();

        return new FeatureFlagSnapshot
        {
            Environment = _environment,
            ProfileName = IsDevelopmentOrTest(_environment) ? FeatureProfiles.DevelopmentIntegrated : FeatureProfiles.MinimalSafe,
            OverridesAllowed = _databaseOverridesAllowed,
            Flags = items
        };
    }

    public FeatureFlagSnapshotItem? GetFeatureFlag(string flagName)
    {
        var definition = ResolveDefinition(flagName);
        return definition == null ? null : BuildSnapshotItem(definition);
    }

    public FeatureFlagSnapshotItem SetOverride(string flagName, bool value, string updatedByUserId, string reason)
    {
        if (!_databaseOverridesAllowed || _databaseOverrides == null)
            throw new InvalidOperationException("Database feature flag overrides are not available.");

        var definition = ResolveDefinition(flagName) ?? throw new InvalidOperationException("Unknown feature flag.");
        var key = NormalizeKey(definition.CanonicalName);
        var now = DateTime.UtcNow;
        var existing = LoadDatabaseOverride(key);
        var item = existing ?? new FeatureFlagOverrideState
        {
            FlagName = definition.CanonicalName,
            NormalizedName = key,
            CreatedUtc = now
        };

        item.FlagName = definition.CanonicalName;
        item.NormalizedName = key;
        item.Value = value;
        item.Source = "database";
        item.Deleted = false;
        item.Archived = false;
        item.UpdatedAtUtc = now;
        item.UpdatedUtc = now;
        item.UpdatedByUserId = updatedByUserId ?? string.Empty;
        item.Reason = reason ?? string.Empty;

        SaveDatabaseOverride(item, existing == null);

        _logger.Admin($"feature_flags.override_set flag={definition.CanonicalName} value={value} user={item.UpdatedByUserId}");
        return BuildSnapshotItem(definition);
    }

    public FeatureFlagSnapshotItem ClearOverride(string flagName, string updatedByUserId)
    {
        if (!_databaseOverridesAllowed || _databaseOverrides == null)
            throw new InvalidOperationException("Database feature flag overrides are not available.");

        var definition = ResolveDefinition(flagName) ?? throw new InvalidOperationException("Unknown feature flag.");
        var existing = LoadDatabaseOverride(NormalizeKey(definition.CanonicalName));
        if (existing != null)
        {
            existing.Deleted = true;
            existing.Archived = true;
            existing.UpdatedAtUtc = DateTime.UtcNow;
            existing.UpdatedUtc = existing.UpdatedAtUtc;
            existing.UpdatedByUserId = updatedByUserId ?? string.Empty;
            SaveDatabaseOverride(existing, false);
        }

        _logger.Admin($"feature_flags.override_cleared flag={definition.CanonicalName} user={updatedByUserId}");
        return BuildSnapshotItem(definition);
    }

    private FeatureFlagSnapshotItem BuildSnapshotItem(FeatureFlagDefinition definition)
    {
        var source = "default";
        var effective = definition.DefaultValue;
        DateTime? updatedAt = null;
        var updatedBy = string.Empty;
        var key = NormalizeKey(definition.CanonicalName);

        if (_configOverridesAllowed && _envOverrides.TryGetValue(key, out var envValue))
        {
            effective = envValue;
            source = "env";
        }
        else
        {
            var dbOverride = LoadDatabaseOverride(key);
            if (dbOverride != null)
            {
                effective = dbOverride.Value;
                source = "database override";
                updatedAt = dbOverride.UpdatedAtUtc;
                updatedBy = dbOverride.UpdatedByUserId ?? string.Empty;
            }
            else if (_configOverridesAllowed && _configOverrides.TryGetValue(key, out var configValue))
            {
                effective = configValue;
                source = "config";
            }
        }

        return new FeatureFlagSnapshotItem
        {
            Name = definition.CanonicalName,
            CanonicalKey = definition.CanonicalName,
            Category = definition.Category,
            DefaultValue = definition.DefaultValue,
            EffectiveValue = effective,
            Source = source,
            Description = definition.Description,
            UpdatedAtUtc = updatedAt,
            UpdatedByUserId = updatedBy,
            Aliases = _definitions.Values
                .Where(x => !x.IsCanonical && string.Equals(x.CanonicalName, definition.CanonicalName, StringComparison.OrdinalIgnoreCase))
                .Select(x => x.Name)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            AliasesDeprecated = true,
            IntendedPreReleaseState = definition.DefaultValue ? "enabled_by_default" : "intentionally_disabled"
        };
    }

    private FeatureFlagOverrideState? LoadDatabaseOverride(string normalizedName)
    {
        if (!_databaseOverridesAllowed || _databaseOverrides == null || string.IsNullOrWhiteSpace(normalizedName))
            return null;

        if (_databaseOverrideDocuments != null)
        {
            var documentOverride = LoadDatabaseOverrideDocument(normalizedName);
            if (documentOverride != null) return documentOverride;
        }

        try
        {
            return _databaseOverrides
                .Find(Builders<FeatureFlagOverrideState>.Filter.Eq(x => x.NormalizedName, normalizedName))
                .Where(x => !x.Deleted && !x.Archived)
                .OrderByDescending(x => x.UpdatedAtUtc)
                .FirstOrDefault();
        }
        catch (FormatException ex)
        {
            _logger.Debug($"feature_flags.override.typed_read_failed flag={normalizedName} reason={ex.GetType().Name}");
            return LoadDatabaseOverrideDocument(normalizedName);
        }
        catch (MongoException ex)
        {
            _logger.Debug($"feature_flags.override.typed_read_failed flag={normalizedName} reason={ex.GetType().Name}");
            return LoadDatabaseOverrideDocument(normalizedName);
        }
    }

    private FeatureFlagOverrideState? LoadDatabaseOverrideDocument(string normalizedName)
    {
        if (_databaseOverrideDocuments == null || string.IsNullOrWhiteSpace(normalizedName))
            return null;

        var filter = Builders<BsonDocument>.Filter.Eq("NormalizedName", normalizedName)
            & Builders<BsonDocument>.Filter.Ne("Deleted", true)
            & Builders<BsonDocument>.Filter.Ne("Archived", true);
        var sort = Builders<BsonDocument>.Sort.Descending("UpdatedAtUtc");
        var doc = _databaseOverrideDocuments.Find(filter).Sort(sort).FirstOrDefault();
        if (doc == null) return null;

        return new FeatureFlagOverrideState
        {
            Id = ReadDocumentId(doc),
            FlagName = ReadString(doc, "FlagName"),
            NormalizedName = ReadString(doc, "NormalizedName"),
            Value = ReadBool(doc, "Value"),
            Source = ReadString(doc, "Source", "database"),
            UpdatedByUserId = ReadString(doc, "UpdatedByUserId"),
            UpdatedAtUtc = ReadDateTime(doc, "UpdatedAtUtc"),
            UpdatedUtc = ReadDateTime(doc, "UpdatedUtc"),
            CreatedUtc = ReadDateTime(doc, "CreatedUtc"),
            Reason = ReadString(doc, "Reason"),
            Deleted = ReadBool(doc, "Deleted"),
            Archived = ReadBool(doc, "Archived")
        };
    }

    private void SaveDatabaseOverride(FeatureFlagOverrideState item, bool insert)
    {
        if (_databaseOverrideDocuments != null)
        {
            if (string.IsNullOrWhiteSpace(item.Id))
                item.Id = Guid.NewGuid().ToString("N");

            var filter = Builders<BsonDocument>.Filter.Eq("NormalizedName", item.NormalizedName ?? string.Empty);
            var update = Builders<BsonDocument>.Update
                .SetOnInsert("_id", item.Id)
                .SetOnInsert("Id", item.Id)
                .SetOnInsert("CreatedUtc", item.CreatedUtc)
                .Set("SchemaVersion", item.SchemaVersion)
                .Set("Deleted", item.Deleted)
                .Set("Archived", item.Archived)
                .Set("UpdatedUtc", item.UpdatedUtc)
                .Set("FlagName", item.FlagName ?? string.Empty)
                .Set("NormalizedName", item.NormalizedName ?? string.Empty)
                .Set("Value", item.Value)
                .Set("Source", item.Source ?? "database")
                .Set("UpdatedByUserId", item.UpdatedByUserId ?? string.Empty)
                .Set("UpdatedAtUtc", item.UpdatedAtUtc)
                .Set("Reason", item.Reason ?? string.Empty);
            _databaseOverrideDocuments.UpdateOne(filter, update, new UpdateOptions { IsUpsert = true });
            return;
        }

        if (_databaseOverrides == null) return;
        if (insert) _databaseOverrides.Insert(item);
        else _databaseOverrides.Replace(item);
    }

    private static string ReadDocumentId(BsonDocument doc)
    {
        if (!doc.TryGetValue("_id", out var id) || id == BsonNull.Value) return string.Empty;
        return id.IsObjectId ? id.AsObjectId.ToString() : id.ToString();
    }

    private static string ReadString(BsonDocument doc, string name, string fallback = "")
    {
        if (!doc.TryGetValue(name, out var value) || value == BsonNull.Value) return fallback;
        return value.IsString ? value.AsString : value.ToString();
    }

    private static bool ReadBool(BsonDocument doc, string name)
    {
        if (!doc.TryGetValue(name, out var value) || value == BsonNull.Value) return false;
        if (value.IsBoolean) return value.AsBoolean;
        if (value.IsString && bool.TryParse(value.AsString, out var parsed)) return parsed;
        return false;
    }

    private static DateTime ReadDateTime(BsonDocument doc, string name)
    {
        if (!doc.TryGetValue(name, out var value) || value == BsonNull.Value) return DateTime.UtcNow;
        if (value.IsValidDateTime) return value.ToUniversalTime();
        if (value.IsString && DateTime.TryParse(value.AsString, out var parsed)) return parsed.ToUniversalTime();
        return DateTime.UtcNow;
    }

    private FeatureFlagDefinition? ResolveDefinition(string flagName)
    {
        var normalized = NormalizeKey(flagName);
        if (string.IsNullOrWhiteSpace(normalized)) return null;
        return _definitions.TryGetValue(normalized, out var definition) ? definition : null;
    }

    private static Dictionary<string, FeatureFlagDefinition> BuildDefinitions(string environment)
    {
        var definitions = new Dictionary<string, FeatureFlagDefinition>(StringComparer.OrdinalIgnoreCase);
        AddDefinitions(definitions, "Combat", typeof(CombatFeatureFlags));
        AddDefinitions(definitions, "Economy", typeof(EconomyFeatureFlags));
        AddDefinitions(definitions, "Inventory", typeof(InventoryFeatureFlags));
        AddDefinitions(definitions, "Profile", typeof(ProfileFeatureFlags));
        AddDefinitions(definitions, "Definition", typeof(DefinitionFeatureFlags));
        AddDefinitions(definitions, "Maps", typeof(MapFeatureFlags));
        AddDefinitions(definitions, "Sessions", typeof(SessionFeatureFlags));
        AddDefinitions(definitions, "CharacterGroups", typeof(CharacterGroupFeatureFlags));
        AddDefinitions(definitions, "CharacterOwnership", typeof(CharacterOwnershipFeatureFlags));
        AddDefinitions(definitions, "PlayerRequests", typeof(PlayerRequestFeatureFlags));
        AddDefinitions(definitions, "WorldCalendar", typeof(WorldCalendarFeatureFlags));
        AddDefinitions(definitions, "RealSchedule", typeof(RealScheduleFeatureFlags));
        AddDefinitions(definitions, "GMNotes", typeof(GMNotesFeatureFlags));
        AddDefinitions(definitions, "EventJournal", typeof(EventJournalFeatureFlags));
        AddDefinitions(definitions, "BackupRestore", typeof(BackupRestoreFeatureFlags));
        AddDefinitions(definitions, "GlobalSearch", typeof(GlobalSearchFeatureFlags));
        AddDefinitions(definitions, "Audio", typeof(AudioFeatureFlags));
        AddDefinitions(definitions, "Fate", typeof(FateFeatureFlags));
        AddDefinitions(definitions, "Projects", typeof(ProjectFoundationFeatureFlags));
        AddDefinitions(definitions, "Projects", typeof(UnifiedProjectRuntimeFeatureFlags));
        AddDefinitions(definitions, "KnowledgeResearch", typeof(KnowledgeResearchFeatureFlags));
        AddDefinitions(definitions, "Development", typeof(DevelopmentFeatureFlags));
        AddDefinitions(definitions, "Crafting", typeof(CraftingFeatureFlags));
        AddDefinitions(definitions, "Engineering", typeof(EngineeringFeatureFlags));
        AddDefinitions(definitions, "Production", typeof(ProductionFeatureFlags));
        AddDefinitions(definitions, "Manufacturing", typeof(ManufacturingFeatureFlags));
        AddDefinitions(definitions, "ClientFunctionalization", typeof(ClientFunctionalizationFeatureFlags));
        AddDefinitions(definitions, "LiveActor", typeof(LiveActorFeatureFlags));
        AddDefinitions(definitions, "Legal", typeof(LegalFeatureFlags));
        AddDefinitions(definitions, "Proposals", typeof(ProposalFeatureFlags));
        AddEnvironmentSpecificDefinitions(definitions, environment);
        return definitions;
    }

    private static void AddEnvironmentSpecificDefinitions(Dictionary<string, FeatureFlagDefinition> definitions, string environment)
    {
        if (!IsDevelopmentOrTest(environment)) return;

        AddDefinition(
            definitions,
            "Dev.FeatureFlags.AcceptanceTest",
            "Dev.FeatureFlags.AcceptanceTest",
            false,
            true,
            "Dev");
    }

    private static void AddDefinitions(Dictionary<string, FeatureFlagDefinition> definitions, string area, Type type)
    {
        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.FieldType != typeof(bool) || !field.IsLiteral) continue;
            var value = (bool)(field.GetRawConstantValue() ?? false);
            var canonical = $"{area}.{field.Name}";
            AddDefinition(definitions, canonical, canonical, value, true, area);
            AddDefinition(definitions, $"{type.Name}.{field.Name}", canonical, value, false, area);
            AddDefinition(definitions, field.Name, canonical, value, false, area);
        }
    }

    private static void AddDefinition(Dictionary<string, FeatureFlagDefinition> definitions, string key, string canonicalName, bool value, bool canonical, string category)
    {
        var normalized = NormalizeKey(key);
        if (definitions.ContainsKey(normalized)) return;
        definitions[normalized] = new FeatureFlagDefinition
        {
            Name = key,
            CanonicalName = canonicalName,
            Category = category,
            DefaultValue = value,
            IsCanonical = canonical,
            Description = $"{category} / {canonicalName}"
        };
    }

    private Dictionary<string, bool> NormalizeOverrides(Dictionary<string, bool> overrides, string source)
    {
        var result = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in overrides ?? new Dictionary<string, bool>())
        {
            var definition = ResolveDefinition(pair.Key);
            if (definition == null) continue;
            var key = NormalizeKey(definition.CanonicalName);
            if (result.TryGetValue(key, out var existing) && existing != pair.Value)
                throw new InvalidOperationException($"Feature flag alias conflict in {source}: {definition.CanonicalName} has conflicting values.");
            result[key] = pair.Value;
        }

        return result;
    }

    private Dictionary<string, bool> ReadEnvironmentOverrides()
    {
        var result = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in _definitions.Values)
        {
            var envName = EnvPrefix + definition.Name.Replace(".", "__");
            var raw = Environment.GetEnvironmentVariable(envName);
            if (bool.TryParse(raw, out var value))
            {
                var canonical = NormalizeKey(definition.CanonicalName);
                if (result.TryGetValue(canonical, out var existing) && existing != value)
                    throw new InvalidOperationException($"Feature flag alias conflict in environment: {definition.CanonicalName} has conflicting values.");
                result[canonical] = value;
            }
        }

        return result;
    }

    private static bool IsDevelopmentOrTest(string environment)
    {
        return string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase)
            || string.Equals(environment, "Dev", StringComparison.OrdinalIgnoreCase)
            || string.Equals(environment, "Test", StringComparison.OrdinalIgnoreCase)
            || string.Equals(environment, "Testing", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeKey(string flagName)
    {
        return (flagName ?? string.Empty).Trim();
    }

    private sealed class FeatureFlagDefinition
    {
        public string Name { get; set; } = string.Empty;
        public string CanonicalName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public bool DefaultValue { get; set; }
        public bool IsCanonical { get; set; }
        public string Description { get; set; } = string.Empty;
    }
}

public static class CombatFeatureGate
{
    private static IFeatureFlagProvider? _provider;

    public static void Configure(IFeatureFlagProvider provider)
    {
        _provider = provider;
    }

    public static bool IsEnabled(string flagName)
    {
        return _provider == null
            ? DefaultValue(flagName)
            : _provider.IsEnabled(flagName);
    }

    private static bool DefaultValue(string flagName)
    {
        var field = typeof(CombatFeatureFlags).GetField(flagName ?? string.Empty, BindingFlags.Public | BindingFlags.Static);
        return field != null && field.FieldType == typeof(bool) && (bool)(field.GetRawConstantValue() ?? false);
    }
}
