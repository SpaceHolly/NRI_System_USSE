using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Driver;
using Nri.Server.Infrastructure;
using Nri.Server.Logging;
using Nri.Shared.Domain;

namespace Nri.Server.Application;

public sealed class ProfileDetailsBuildResult
{
    public string CharacterId { get; set; } = string.Empty;
    public bool UsedProfileFirst { get; set; }
    public bool UsedFallback { get; set; }
    public List<string> MissingSections { get; set; } = new List<string>();
    public string ErrorMessage { get; set; } = string.Empty;
    public DateTime BuiltAtUtc { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object> Payload { get; set; } = new Dictionary<string, object>();
}

public interface ICharacterDetailsProfileBuilder
{
    Task<ProfileDetailsBuildResult> BuildFromProfilesAsync(Character legacyCharacter, string actorUserId, string requestId);
    Task<bool> CanBuildFromProfilesAsync(string characterId);
    Dictionary<string, object> BuildProfileIdentityShell(Character legacyCharacter);
    Task<ProfileDetailsBuildResult> BuildFromProfilesAsync(Character legacyCharacter, string actorUserId, string requestId, Dictionary<string, object> legacyPayload);
    Task<ProfileDetailsBuildResult> BuildProfileDetailsDiagnosticAsync(string characterId);
}

public sealed class CharacterDetailsProfileBuilder : ICharacterDetailsProfileBuilder
{
    private static readonly string[] RequiredProfileSections = { "attributes", "wallet", "skills", "development", "inventory", "raceOrSpecies", "body" };
    private readonly MongoContext _mongo;
    private readonly ICharacterProfileConsistencyService _consistencyService;
    private readonly IServerLogger _logger;

    public CharacterDetailsProfileBuilder(MongoContext mongo, ICharacterProfileConsistencyService consistencyService, IServerLogger logger)
    {
        _mongo = mongo;
        _consistencyService = consistencyService;
        _logger = logger;
    }

    public Task<ProfileDetailsBuildResult> BuildFromProfilesAsync(Character legacyCharacter, string actorUserId, string requestId)
    {
        return BuildFromProfilesAsync(legacyCharacter, actorUserId, requestId, BuildProfileIdentityShell(legacyCharacter));
    }

    public Task<bool> CanBuildFromProfilesAsync(string characterId)
    {
        return Task.FromResult(FindMissingSections(characterId).Count == 0);
    }

    public Dictionary<string, object> BuildProfileIdentityShell(Character legacyCharacter)
    {
        var c = legacyCharacter ?? new Character();
        return new Dictionary<string, object>
        {
            { "characterId", c.Id },
            { "ownerUserId", c.OwnerUserId },
            { "name", c.Name },
            { "race", string.Empty },
            { "height", string.Empty },
            { "archived", c.Archived },
            { "deleted", c.Deleted },
            { "schemaVersion", c.SchemaVersion },
            { "profileSource", "character_v2_profiles" }
        };
    }

    public Task<ProfileDetailsBuildResult> BuildFromProfilesAsync(Character legacyCharacter, string actorUserId, string requestId, Dictionary<string, object> legacyPayload)
    {
        var characterId = legacyCharacter?.Id ?? string.Empty;
        _logger.Debug($"profile.details.build.start characterId={characterId} requestId={requestId}");

        var result = new ProfileDetailsBuildResult
        {
            CharacterId = characterId,
            Payload = CopyPayload(legacyPayload)
        };

        try
        {
            if (legacyCharacter == null)
            {
                result.ErrorMessage = ApplicationContextStates.ProfileRepairRequired;
                result.Payload = new Dictionary<string, object>();
                _logger.Debug($"profile.details.profile_required characterId={characterId} requestId={requestId} reason=identity_missing");
                return Task.FromResult(result);
            }

            var missing = FindMissingSections(characterId);
            if (missing.Count > 0)
            {
                result.MissingSections = missing;
                result.ErrorMessage = ApplicationContextStates.ProfileMigrationRequired;
                _logger.Debug($"profile.details.missing_sections characterId={characterId} requestId={requestId} sections={string.Join(",", missing)}");
                result.Payload = new Dictionary<string, object>();
                return Task.FromResult(result);
            }

            if (ProfileFeatureFlags.UseCharacterProfileConsistencyVerification)
            {
                var consistency = _consistencyService.VerifyCharacterAsync(characterId, actorUserId, requestId).GetAwaiter().GetResult();
                var hasErrorDifferences = consistency.SectionReports.Any(x =>
                    x.DifferenceCount > 0 &&
                    string.Equals(x.Severity, "error", StringComparison.OrdinalIgnoreCase));
                if (hasErrorDifferences)
                {
                    result.ErrorMessage = ApplicationContextStates.ProfileRepairRequired;
                    result.Payload = new Dictionary<string, object>();
                    _logger.Debug($"profile.details.consistency_failed characterId={characterId} requestId={requestId} differences={consistency.TotalDifferenceCount}");
                    return Task.FromResult(result);
                }
            }

            var attribute = _mongo.CharacterAttributeProfiles.Find(Builders<CharacterAttributeProfileDocument>.Filter.Eq(x => x.CharacterId, characterId)).FirstOrDefault()?.Profile
                ?? EmptyAttributeProfile(characterId);
            var wallet = _mongo.CharacterWalletProfiles.Find(Builders<CharacterWalletProfileDocument>.Filter.Eq(x => x.CharacterId, characterId)).FirstOrDefault()?.Profile
                ?? EmptyWalletProfile(characterId);
            var skills = _mongo.CharacterSkillProfiles.Find(Builders<CharacterSkillProfileDocument>.Filter.Eq(x => x.CharacterId, characterId)).FirstOrDefault()?.Profile
                ?? EmptySkillProfile(characterId);
            var development = _mongo.CharacterDevelopmentProfiles.Find(Builders<CharacterDevelopmentProfileDocument>.Filter.Eq(x => x.CharacterId, characterId)).FirstOrDefault()?.Profile
                ?? EmptyDevelopmentProfile(characterId);
            var inventory = _mongo.CharacterInventoryProfiles.Find(Builders<CharacterInventoryProfileDocument>.Filter.Eq(x => x.CharacterId, characterId)).FirstOrDefault()?.Profile
                ?? EmptyInventoryProfile(characterId);
            var reputation = _mongo.CharacterReputationProfiles.Find(Builders<CharacterReputationProfileDocument>.Filter.Eq(x => x.CharacterId, characterId)).FirstOrDefault()?.Profile
                ?? EmptyReputationProfile(characterId);
            var holdings = _mongo.CharacterHoldingsProfiles.Find(Builders<CharacterHoldingsProfileDocument>.Filter.Eq(x => x.CharacterId, characterId)).FirstOrDefault()?.Profile
                ?? EmptyHoldingsProfile(characterId);
            var companions = _mongo.CharacterCompanionProfiles.Find(Builders<CharacterCompanionProfileDocument>.Filter.Eq(x => x.CharacterId, characterId)).FirstOrDefault()?.Profile
                ?? EmptyCompanionProfile(characterId);
            var race = _mongo.CharacterRaceOrSpeciesProfiles.Find(Builders<CharacterRaceOrSpeciesProfileDocument>.Filter.Eq(x => x.CharacterId, characterId)).FirstOrDefault()?.Profile
                ?? EmptyRaceProfile(characterId);
            var body = _mongo.CharacterBodyProfiles.Find(Builders<CharacterBodyProfileDocument>.Filter.Eq(x => x.CharacterId, characterId)).FirstOrDefault()?.Profile
                ?? EmptyBodyProfile(characterId);

            var payload = BuildProfileIdentityPayload(legacyCharacter, race, body, legacyPayload);
            var statRuleSetId = string.IsNullOrWhiteSpace(attribute.RuleSetId) ? RuleSetIds.FantasyNriDefault : attribute.RuleSetId;
            var statDefinitions = LoadCharacterStatDefinitions(statRuleSetId);
            EnsureCharacterStatDefaults(characterId, attribute, statDefinitions);
            payload["stats"] = BuildStatsPayload(attribute);
            payload["attributes"] = BuildAttributeViewsPayload(attribute);
            payload["vitals"] = BuildCharacterStatViewsPayload(attribute, statDefinitions, includeVitals: true);
            payload["derivedStats"] = BuildCharacterStatViewsPayload(attribute, statDefinitions, includeVitals: false);
            payload["profileRuleSetId"] = attribute.RuleSetId;
            var currencyRuleSetId = FirstNonEmpty(wallet.RuleSetId, attribute.RuleSetId, RuleSetIds.FantasyNriDefault);
            var currencyDefinitions = LoadCurrencyDefinitions(currencyRuleSetId);
            EnsureWalletCurrencies(characterId, wallet, currencyDefinitions);
            payload["money"] = BuildMoneyPayload(wallet, currencyDefinitions);
            payload["currencies"] = BuildCurrencyListPayload(wallet, currencyDefinitions);
            payload["xpCoins"] = GetWalletAmount(wallet, CharacterCurrencyIds.XpCoin);
            payload["experienceCoins"] = BuildExperienceCoinsPayload(wallet, currencyDefinitions);
            payload["skills"] = BuildLegacySkillsPayload(skills);
            payload["characterSkills"] = BuildCharacterSkillsPayload(skills);
            payload["classProgress"] = BuildClassProgressPayload(development);
            payload["characterClasses"] = BuildCharacterClassesPayload(development);
            payload["inventory"] = BuildInventoryPayload(inventory);
            payload["reputation"] = BuildReputationPayload(reputation);
            payload["holdings"] = BuildHoldingsPayload(holdings);
            payload["companions"] = BuildCompanionsPayload(companions);
            payload["profileSource"] = "character_v2_profiles";
            payload["profileMissingSections"] = missing.Cast<object>().ToArray();

            result.Payload = payload;
            result.UsedProfileFirst = true;
            result.UsedFallback = false;
            LogProfileDifferences(characterId, legacyPayload, payload);
            _logger.Debug($"profile.details.build.done characterId={characterId} requestId={requestId} profileFirst=true fallback=false");
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            result.UsedFallback = false;
            result.ErrorMessage = ApplicationContextStates.ProfileRepairRequired;
            result.Payload = new Dictionary<string, object>();
            _logger.Debug($"profile.details.error characterId={characterId} requestId={requestId} message={ex.Message}");
            _logger.Debug($"profile.details.profile_unavailable characterId={characterId} requestId={requestId} reason=exception");
            _logger.Debug($"profile.details.build.done characterId={characterId} requestId={requestId} profileFirst=false fallback=false");
            return Task.FromResult(result);
        }
    }

    public Task<ProfileDetailsBuildResult> BuildProfileDetailsDiagnosticAsync(string characterId)
    {
        var builtAtUtc = DateTime.UtcNow;
        var legacyCharacter = string.IsNullOrWhiteSpace(characterId)
            ? null
            : _mongo.Characters.Find(Builders<Character>.Filter.Eq(x => x.Id, characterId)).FirstOrDefault();

        if (legacyCharacter == null)
        {
            return Task.FromResult(new ProfileDetailsBuildResult
            {
                CharacterId = characterId ?? string.Empty,
                UsedFallback = true,
                ErrorMessage = "legacy_character_missing",
                MissingSections = FindMissingSections(characterId ?? string.Empty),
                BuiltAtUtc = builtAtUtc
            });
        }

        return BuildFromProfilesAsync(legacyCharacter, "diagnostic", "diagnostic", BuildProfileIdentityShell(legacyCharacter));
    }

    private List<string> FindMissingSections(string characterId)
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(characterId))
        {
            missing.AddRange(RequiredProfileSections);
            return missing;
        }

        var attributes = _mongo.CharacterAttributeProfiles.Find(Builders<CharacterAttributeProfileDocument>.Filter.Eq(x => x.CharacterId, characterId)).FirstOrDefault()?.Profile;
        var wallet = _mongo.CharacterWalletProfiles.Find(Builders<CharacterWalletProfileDocument>.Filter.Eq(x => x.CharacterId, characterId)).FirstOrDefault()?.Profile;
        var skills = _mongo.CharacterSkillProfiles.Find(Builders<CharacterSkillProfileDocument>.Filter.Eq(x => x.CharacterId, characterId)).FirstOrDefault()?.Profile;
        var development = _mongo.CharacterDevelopmentProfiles.Find(Builders<CharacterDevelopmentProfileDocument>.Filter.Eq(x => x.CharacterId, characterId)).FirstOrDefault()?.Profile;
        var inventory = _mongo.CharacterInventoryProfiles.Find(Builders<CharacterInventoryProfileDocument>.Filter.Eq(x => x.CharacterId, characterId)).FirstOrDefault()?.Profile;
        var reputation = _mongo.CharacterReputationProfiles.Find(Builders<CharacterReputationProfileDocument>.Filter.Eq(x => x.CharacterId, characterId)).FirstOrDefault()?.Profile;
        var holdings = _mongo.CharacterHoldingsProfiles.Find(Builders<CharacterHoldingsProfileDocument>.Filter.Eq(x => x.CharacterId, characterId)).FirstOrDefault()?.Profile;
        var companions = _mongo.CharacterCompanionProfiles.Find(Builders<CharacterCompanionProfileDocument>.Filter.Eq(x => x.CharacterId, characterId)).FirstOrDefault()?.Profile;

        if (attributes == null || attributes.Values == null || attributes.SchemaVersion < 1 || !ProfileCharacterIdMatches(attributes.CharacterId, characterId)) missing.Add("attributes");
        if (wallet == null || wallet.Wallets == null || wallet.SchemaVersion < 1 || !ProfileCharacterIdMatches(wallet.CharacterId, characterId)) missing.Add("wallet");
        if (skills == null || skills.Skills == null || skills.SchemaVersion < 1 || !ProfileCharacterIdMatches(skills.CharacterId, characterId)) missing.Add("skills");
        if (development == null || development.ActiveHexagonIds == null || development.Nodes == null || development.SchemaVersion < 1 || !ProfileCharacterIdMatches(development.CharacterId, characterId)) missing.Add("development");
        if (inventory == null || inventory.Items == null || inventory.SchemaVersion < 1 || !ProfileCharacterIdMatches(inventory.CharacterId, characterId)) missing.Add("inventory");
        var race = _mongo.CharacterRaceOrSpeciesProfiles.Find(Builders<CharacterRaceOrSpeciesProfileDocument>.Filter.Eq(x => x.CharacterId, characterId)).FirstOrDefault()?.Profile;
        var body = _mongo.CharacterBodyProfiles.Find(Builders<CharacterBodyProfileDocument>.Filter.Eq(x => x.CharacterId, characterId)).FirstOrDefault()?.Profile;
        if (race == null || race.Tags == null || race.SchemaVersion < 1 || !ProfileCharacterIdMatches(race.CharacterId, characterId)) missing.Add("raceOrSpecies");
        if (body == null || body.BodyTags == null || body.EquipmentCompatibilityTags == null || body.SchemaVersion < 1 || !ProfileCharacterIdMatches(body.CharacterId, characterId)) missing.Add("body");
        return missing;
    }

    private static bool ProfileCharacterIdMatches(string profileCharacterId, string characterId)
    {
        return !string.IsNullOrWhiteSpace(profileCharacterId) &&
            string.Equals(profileCharacterId, characterId, StringComparison.Ordinal);
    }

    private static Dictionary<string, object> BuildStatsPayload(AttributeProfile profile)
    {
        var values = (profile?.Values ?? new List<CharacterAttributeValue>())
            .Where(x => !string.IsNullOrWhiteSpace(x.AttributeId))
            .GroupBy(x => x.AttributeId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Last().CurrentValue, StringComparer.OrdinalIgnoreCase);

        return new Dictionary<string, object>
        {
            { "health", GetFirstAttribute(values, CharacterVitalStatIds.HealthCurrent, CharacterAttributeIds.Health) },
            { "currentHealth", GetFirstAttribute(values, CharacterVitalStatIds.HealthCurrent, CharacterAttributeIds.Health) },
            { "maxHealth", GetFirstAttribute(values, CharacterVitalStatIds.HealthMax, CharacterAttributeIds.Health) },
            { "physicalArmor", GetFirstAttribute(values, CharacterVitalStatIds.PhysicalDefense, CharacterAttributeIds.PhysicalArmor) },
            { "physicalDefense", GetFirstAttribute(values, CharacterVitalStatIds.PhysicalDefense, CharacterAttributeIds.PhysicalArmor) },
            { "magicalArmor", GetFirstAttribute(values, CharacterVitalStatIds.MagicalDefense, CharacterAttributeIds.MagicArmor) },
            { "magicalDefense", GetFirstAttribute(values, CharacterVitalStatIds.MagicalDefense, CharacterAttributeIds.MagicArmor) },
            { "morale", GetFirstAttribute(values, CharacterVitalStatIds.Morale, CharacterAttributeIds.Morale) },
            { "strength", GetAttribute(values, CharacterAttributeIds.Strength) },
            { "dexterity", GetAttribute(values, CharacterAttributeIds.Dexterity) },
            { "endurance", GetAttribute(values, CharacterAttributeIds.Endurance) },
            { "wisdom", GetAttribute(values, CharacterAttributeIds.Wisdom) },
            { "intellect", GetAttribute(values, CharacterAttributeIds.Intellect) },
            { "charisma", GetAttribute(values, CharacterAttributeIds.Charisma) }
        };
    }

    private object[] BuildAttributeViewsPayload(AttributeProfile profile)
    {
        var ruleSetId = string.IsNullOrWhiteSpace(profile?.RuleSetId) ? RuleSetIds.FantasyNriDefault : profile.RuleSetId;
        var characterId = profile?.CharacterId ?? string.Empty;
        var subAttributesByParent = string.IsNullOrWhiteSpace(characterId)
            ? new Dictionary<string, object[]>(StringComparer.OrdinalIgnoreCase)
            : CharacterSubAttributeRuntime.BuildSubAttributeViewMap(_mongo, characterId, ruleSetId, includeHidden: false);
        var values = (profile?.Values ?? new List<CharacterAttributeValue>())
            .Where(x => !string.IsNullOrWhiteSpace(x.AttributeId))
            .GroupBy(x => x.AttributeId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Last(), StringComparer.OrdinalIgnoreCase);

        return LoadAttributeDefinitions(ruleSetId)
            .Where(x => x.IsPlayerVisible)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(def =>
            {
                values.TryGetValue(def.AttributeId, out var current);
                var value = current?.CurrentValue ?? def.DefaultValue;
                return (object)new Dictionary<string, object>
                {
                    { "attributeId", def.AttributeId },
                    { "code", def.Code },
                    { "label", def.DisplayName },
                    { "displayName", def.DisplayName },
                    { "description", def.Description },
                    { "currentValue", value },
                    { "value", value },
                    { "baseValue", current?.BaseValue ?? value },
                    { "minValue", def.MinValue },
                    { "maxValue", def.MaxValue },
                    { "defaultValue", def.DefaultValue },
                    { "sortOrder", def.SortOrder },
                    { "attributeSetId", def.AttributeSetId },
                    { "sourceRuleSetId", ruleSetId },
                    { "isPlayerVisible", def.IsPlayerVisible },
                    { "isEditableByGM", def.IsEditableByGM },
                    { "subAttributes", subAttributesByParent.TryGetValue(def.AttributeId, out var subAttributes) ? subAttributes : Array.Empty<object>() }
                };
            })
            .ToArray();
    }

    private void EnsureCharacterStatDefaults(string characterId, AttributeProfile profile, List<CharacterStatDefinitionProjection> definitions)
    {
        if (profile == null || definitions == null || definitions.Count == 0) return;
        profile.Values ??= new List<CharacterAttributeValue>();
        var changed = false;
        foreach (var definition in definitions)
        {
            if (string.IsNullOrWhiteSpace(definition.DefinitionId)) continue;
            if (profile.Values.Any(x => string.Equals(x.AttributeId, definition.DefinitionId, StringComparison.OrdinalIgnoreCase))) continue;
            profile.Values.Add(new CharacterAttributeValue
            {
                AttributeId = definition.DefinitionId,
                BaseValue = definition.DefaultValue,
                CurrentValue = definition.DefaultValue,
                ManualModifier = 0,
                Source = "ruleset_default"
            });
            changed = true;
        }

        if (!changed) return;
        profile.CharacterId = string.IsNullOrWhiteSpace(profile.CharacterId) ? characterId : profile.CharacterId;
        profile.RuleSetId = string.IsNullOrWhiteSpace(profile.RuleSetId) ? RuleSetIds.FantasyNriDefault : profile.RuleSetId;
        profile.SchemaVersion = Math.Max(1, profile.SchemaVersion);
        UpsertByCharacterId(_mongo.CharacterAttributeProfiles, characterId, new CharacterAttributeProfileDocument { CharacterId = characterId, Profile = profile });
        _logger.Debug($"profile.details.character_stats.defaults_saved characterId={characterId} count={definitions.Count}");
    }

    private object[] BuildCharacterStatViewsPayload(AttributeProfile profile, List<CharacterStatDefinitionProjection> definitions, bool includeVitals)
    {
        var values = (profile?.Values ?? new List<CharacterAttributeValue>())
            .Where(x => !string.IsNullOrWhiteSpace(x.AttributeId))
            .GroupBy(x => x.AttributeId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Last(), StringComparer.OrdinalIgnoreCase);

        return (definitions ?? new List<CharacterStatDefinitionProjection>())
            .Where(x => x.IsPlayerVisible)
            .Where(x => includeVitals ? x.IsVitalGroup : x.IsDerivedGroup)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(definition =>
            {
                values.TryGetValue(definition.DefinitionId, out var current);
                var value = current?.CurrentValue ?? definition.DefaultValue;
                return (object)new Dictionary<string, object>
                {
                    { "definitionId", definition.DefinitionId },
                    { "attributeId", definition.DefinitionId },
                    { "code", definition.Code },
                    { "displayName", definition.DisplayName },
                    { "label", definition.DisplayName },
                    { "description", definition.Description },
                    { "value", value },
                    { "currentValue", value },
                    { "baseValue", current?.BaseValue ?? value },
                    { "minValue", definition.MinValue },
                    { "maxValue", definition.MaxValue },
                    { "unit", definition.Unit },
                    { "category", definition.Category },
                    { "sortOrder", definition.SortOrder },
                    { "isPlayerVisible", definition.IsPlayerVisible },
                    { "isEditableByGM", definition.IsEditableByGM },
                    { "isDerived", definition.IsDerived },
                    { "isFormulaBased", !string.IsNullOrWhiteSpace(definition.Formula) },
                    { "isManualOverride", string.IsNullOrWhiteSpace(definition.Formula) },
                    { "formula", definition.Formula },
                    { "sourceRuleSetId", definition.SourceRuleSetId },
                    { "sourceProfileId", profile?.CharacterId ?? string.Empty }
                };
            })
            .ToArray();
    }

    private List<AttributeDefinitionProjection> LoadAttributeDefinitions(string ruleSetId)
    {
        var filter = Builders<UnifiedDefinitionDocument>.Filter.Eq(x => x.Category, DefinitionCategoryIds.Attribute)
            & Builders<UnifiedDefinitionDocument>.Filter.Eq(x => x.IsArchived, false);
        if (!string.IsNullOrWhiteSpace(ruleSetId))
        {
            filter &= Builders<UnifiedDefinitionDocument>.Filter.AnyEq(x => x.RuleSetIds, ruleSetId);
        }

        var definitions = _mongo.UnifiedDefinitions.Find(filter).ToList()
            .Select(ToAttributeDefinitionProjection)
            .Where(x => !string.IsNullOrWhiteSpace(x.AttributeId))
            .ToList();

        return definitions.Count > 0 ? definitions : DefaultAttributeDefinitions(ruleSetId);
    }

    private static AttributeDefinitionProjection ToAttributeDefinitionProjection(UnifiedDefinitionDocument definition)
    {
        var extra = definition.ExtraData ?? new Dictionary<string, object>();
        var id = FirstNonEmpty(GetExtraString(extra, "attributeId"), definition.Id);
        var displayName = FirstNonEmpty(GetExtraString(extra, "displayName"), GetExtraString(extra, "displayNameRu"), definition.Name, id);
        return new AttributeDefinitionProjection
        {
            AttributeId = id,
            Code = FirstNonEmpty(GetExtraString(extra, "code"), id),
            DisplayName = displayName,
            Description = FirstNonEmpty(definition.PublicDescription, GetExtraString(extra, "description")),
            MinValue = GetExtraInt(extra, "minValue", 0),
            MaxValue = GetExtraInt(extra, "maxValue", 30),
            DefaultValue = GetExtraInt(extra, "defaultValue", 10),
            SortOrder = GetExtraInt(extra, "sortOrder", 1000),
            AttributeSetId = FirstNonEmpty(GetExtraString(extra, "attributeSetId"), "fantasy_primary"),
            IsPlayerVisible = !IsHiddenVisibility(definition.VisibilityRule) && GetExtraBool(extra, "isPlayerVisible", true),
            IsEditableByGM = GetExtraBool(extra, "isEditableByGM", true)
        };
    }

    private static List<AttributeDefinitionProjection> DefaultAttributeDefinitions(string ruleSetId)
    {
        return new List<AttributeDefinitionProjection>
        {
            DefaultAttribute(CharacterAttributeIds.Strength, "strength", "Сила", 10),
            DefaultAttribute(CharacterAttributeIds.Dexterity, "dexterity", "Ловкость", 20),
            DefaultAttribute(CharacterAttributeIds.Endurance, "endurance", "Выносливость", 30),
            DefaultAttribute(CharacterAttributeIds.Intellect, "intelligence", "Интеллект", 40),
            DefaultAttribute(CharacterAttributeIds.Wisdom, "wisdom", "Мудрость", 50),
            DefaultAttribute(CharacterAttributeIds.Charisma, "charisma", "Харизма", 60)
        };

        AttributeDefinitionProjection DefaultAttribute(string attributeId, string code, string name, int sortOrder)
        {
            return new AttributeDefinitionProjection
            {
                AttributeId = attributeId,
                Code = code,
                DisplayName = name,
                MinValue = 0,
                MaxValue = 30,
                DefaultValue = 10,
                SortOrder = sortOrder,
                AttributeSetId = "fantasy_primary",
                SourceRuleSetId = ruleSetId,
                IsPlayerVisible = true,
                IsEditableByGM = true
            };
        }
    }

    private void EnsureWalletCurrencies(string characterId, WalletProfile profile, List<CurrencyDefinitionProjection> definitions)
    {
        if (profile == null || definitions == null || definitions.Count == 0) return;
        profile.Wallets ??= new List<CharacterWalletValue>();
        var changed = false;
        foreach (var definition in definitions)
        {
            if (string.IsNullOrWhiteSpace(definition.CurrencyId)) continue;
            if (profile.Wallets.Any(x => string.Equals(x.CurrencyId, definition.CurrencyId, StringComparison.OrdinalIgnoreCase))) continue;
            profile.Wallets.Add(new CharacterWalletValue
            {
                CurrencyId = definition.CurrencyId,
                Amount = definition.DefaultValue,
                Source = "ruleset_default"
            });
            changed = true;
        }

        if (!changed) return;
        profile.CharacterId = string.IsNullOrWhiteSpace(profile.CharacterId) ? characterId : profile.CharacterId;
        profile.RuleSetId = string.IsNullOrWhiteSpace(profile.RuleSetId) ? RuleSetIds.FantasyNriDefault : profile.RuleSetId;
        profile.SchemaVersion = Math.Max(1, profile.SchemaVersion);
        UpsertByCharacterId(_mongo.CharacterWalletProfiles, characterId, new CharacterWalletProfileDocument { CharacterId = characterId, Profile = profile });
        _logger.Debug($"profile.details.wallet.defaults_saved characterId={characterId} count={definitions.Count}");
    }

    private static Dictionary<string, object> BuildMoneyPayload(WalletProfile profile, List<CurrencyDefinitionProjection> definitions)
    {
        var payload = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in definitions.Where(x => x.IsMoneyCurrency))
        {
            var amount = GetWalletAmount(profile, definition.CurrencyId);
            payload[definition.Code] = amount;
            payload[definition.CurrencyId] = amount;
            if (!string.IsNullOrWhiteSpace(definition.LegacyKey)) payload[definition.LegacyKey] = amount;
        }

        return payload;
    }

    private static object[] BuildCurrencyListPayload(WalletProfile profile, List<CurrencyDefinitionProjection> definitions)
    {
        return definitions
            .Where(x => x.IsPlayerVisible)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(definition =>
            {
                var amount = GetWalletAmount(profile, definition.CurrencyId);
                return (object)new Dictionary<string, object>
                {
                    { "currencyId", definition.CurrencyId },
                    { "code", definition.Code },
                    { "label", definition.DisplayName },
                    { "displayName", definition.DisplayName },
                    { "description", definition.Description },
                    { "amount", amount },
                    { "defaultValue", definition.DefaultValue },
                    { "minValue", definition.MinValue },
                    { "maxValue", definition.MaxValue.HasValue ? (object)definition.MaxValue.Value : string.Empty },
                    { "unit", definition.Unit },
                    { "iconKey", definition.IconKey },
                    { "kind", definition.Kind },
                    { "sortOrder", definition.SortOrder },
                    { "isPlayerVisible", definition.IsPlayerVisible },
                    { "isEditableByGM", definition.IsEditableByGM },
                    { "sourceRuleSetId", definition.SourceRuleSetId },
                    { "sourceCurrencySetId", definition.CurrencySetId },
                    { "legacyKey", definition.LegacyKey }
                };
            })
            .ToArray();
    }

    private static Dictionary<string, object> BuildExperienceCoinsPayload(WalletProfile profile, List<CurrencyDefinitionProjection> definitions)
    {
        var definition = definitions.FirstOrDefault(x => string.Equals(x.CurrencyId, CharacterCurrencyIds.XpCoin, StringComparison.OrdinalIgnoreCase) || x.IsExperienceCurrency);
        var visible = definition?.IsPlayerVisible ?? true;
        var editable = definition?.IsEditableByGM ?? true;
        return new Dictionary<string, object>
        {
            { "balance", GetWalletAmount(profile, definition?.CurrencyId ?? CharacterCurrencyIds.XpCoin) },
            { "source", "character_wallet_profiles" },
            { "isEditableByGM", editable },
            { "isPlayerVisible", visible }
        };
    }

    private List<CurrencyDefinitionProjection> LoadCurrencyDefinitions(string ruleSetId)
    {
        var filter = Builders<UnifiedDefinitionDocument>.Filter.Eq(x => x.Category, DefinitionCategoryIds.Currency)
            & Builders<UnifiedDefinitionDocument>.Filter.Eq(x => x.IsArchived, false);
        if (!string.IsNullOrWhiteSpace(ruleSetId))
        {
            filter &= Builders<UnifiedDefinitionDocument>.Filter.AnyEq(x => x.RuleSetIds, ruleSetId);
        }

        var definitions = _mongo.UnifiedDefinitions.Find(filter).ToList()
            .Select(x => ToCurrencyDefinitionProjection(x, ruleSetId))
            .Where(x => !string.IsNullOrWhiteSpace(x.CurrencyId))
            .GroupBy(x => x.CurrencyId, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.Last())
            .ToList();

        return definitions.Count > 0 ? definitions : DefaultCurrencyDefinitions(ruleSetId);
    }

    private List<CharacterStatDefinitionProjection> LoadCharacterStatDefinitions(string ruleSetId)
    {
        var filter = Builders<UnifiedDefinitionDocument>.Filter.Eq(x => x.Category, DefinitionCategoryIds.DerivedStat)
            & Builders<UnifiedDefinitionDocument>.Filter.Eq(x => x.IsArchived, false);
        if (!string.IsNullOrWhiteSpace(ruleSetId))
        {
            filter &= Builders<UnifiedDefinitionDocument>.Filter.AnyEq(x => x.RuleSetIds, ruleSetId);
        }

        var definitions = _mongo.UnifiedDefinitions.Find(filter).ToList()
            .Select(x => ToCharacterStatDefinitionProjection(x, ruleSetId))
            .Where(x => !string.IsNullOrWhiteSpace(x.DefinitionId))
            .GroupBy(x => x.DefinitionId, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.Last())
            .ToList();

        return DefaultCharacterStatDefinitions(ruleSetId)
            .Concat(definitions)
            .GroupBy(x => x.DefinitionId, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.Last())
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static CharacterStatDefinitionProjection ToCharacterStatDefinitionProjection(UnifiedDefinitionDocument definition, string ruleSetId)
    {
        var extra = definition.ExtraData ?? new Dictionary<string, object>();
        var id = FirstNonEmpty(GetExtraString(extra, "definitionId"), GetExtraString(extra, "statId"), definition.Id);
        var code = FirstNonEmpty(GetExtraString(extra, "code"), id);
        var category = NormalizeStatCategory(FirstNonEmpty(GetExtraString(extra, "statCategory"), GetExtraString(extra, "characterStatCategory"), GuessStatCategory(id, code)));
        var displayName = FirstNonEmpty(GetExtraString(extra, "displayName"), GetExtraString(extra, "displayNameRu"), definition.Name, id);
        return new CharacterStatDefinitionProjection
        {
            DefinitionId = id,
            Code = code,
            DisplayName = displayName,
            Description = FirstNonEmpty(definition.PublicDescription, GetExtraString(extra, "description")),
            Category = category,
            MinValue = GetExtraInt(extra, "minValue", 0),
            MaxValue = GetExtraInt(extra, "maxValue", 999),
            DefaultValue = GetExtraInt(extra, "defaultValue", 0),
            Unit = GetExtraString(extra, "unit"),
            Formula = GetExtraString(extra, "formula"),
            SortOrder = GetExtraInt(extra, "sortOrder", 1000),
            IsPlayerVisible = !IsHiddenVisibility(definition.VisibilityRule) && GetExtraBool(extra, "isPlayerVisible", true),
            IsEditableByGM = GetExtraBool(extra, "isEditableByGM", true),
            IsDerived = string.Equals(category, "derived", StringComparison.OrdinalIgnoreCase),
            SourceRuleSetId = FirstNonEmpty(definition.RuleSetIds?.FirstOrDefault(x => string.Equals(x, ruleSetId, StringComparison.OrdinalIgnoreCase)), ruleSetId)
        };
    }

    private static List<CharacterStatDefinitionProjection> DefaultCharacterStatDefinitions(string ruleSetId)
    {
        return new List<CharacterStatDefinitionProjection>
        {
            DefaultStat(CharacterVitalStatIds.HealthCurrent, "health_current", "Текущее здоровье", "vital", 10, 0, 999, 10),
            DefaultStat(CharacterVitalStatIds.HealthMax, "health_max", "Максимальное здоровье", "vital", 20, 0, 999, 10),
            DefaultStat(CharacterVitalStatIds.PhysicalDefense, "physical_defense", "Физическая защита", "defense", 30, 0, 999, 0),
            DefaultStat(CharacterVitalStatIds.MagicalDefense, "magical_defense", "Магическая защита", "defense", 40, 0, 999, 0),
            DefaultStat(CharacterVitalStatIds.Morale, "morale", "Мораль", "morale", 50, 0, 999, 0),
            DefaultStat(CharacterVitalStatIds.Initiative, "initiative", "Инициатива", "derived", 110, 0, 999, 0),
            DefaultStat(CharacterVitalStatIds.Movement, "movement", "Перемещение", "derived", 120, 0, 999, 0),
            DefaultStat(CharacterVitalStatIds.CarryingCapacity, "carrying_capacity", "Грузоподъёмность", "derived", 130, 0, 999, 0),
            DefaultStat("dev_acceptance_derived", "dev_acceptance_derived", "Проверочный производный параметр RuleSet", "derived", 990, 0, 999, 5)
        };

        CharacterStatDefinitionProjection DefaultStat(string id, string code, string displayName, string category, int sortOrder, int min, int max, int defaultValue)
        {
            return new CharacterStatDefinitionProjection
            {
                DefinitionId = id,
                Code = code,
                DisplayName = displayName,
                Category = category,
                MinValue = min,
                MaxValue = max,
                DefaultValue = defaultValue,
                SortOrder = sortOrder,
                IsPlayerVisible = true,
                IsEditableByGM = true,
                IsDerived = string.Equals(category, "derived", StringComparison.OrdinalIgnoreCase),
                SourceRuleSetId = ruleSetId
            };
        }
    }

    private static string NormalizeStatCategory(string value)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "vitals" => "vital",
            "health" => "vital",
            "defenses" => "defense",
            "armour" => "defense",
            "armor" => "defense",
            "derived_stat" => "derived",
            "derivedstats" => "derived",
            _ => string.IsNullOrWhiteSpace(normalized) ? "derived" : normalized
        };
    }

    private static string GuessStatCategory(string id, string code)
    {
        var value = FirstNonEmpty(id, code).Trim().ToLowerInvariant();
        if (value.Contains("health")) return "vital";
        if (value.Contains("defense") || value.Contains("armor") || value.Contains("armour")) return "defense";
        if (value.Contains("morale")) return "morale";
        return "derived";
    }

    private static CurrencyDefinitionProjection ToCurrencyDefinitionProjection(UnifiedDefinitionDocument definition, string ruleSetId)
    {
        var extra = definition.ExtraData ?? new Dictionary<string, object>();
        var id = FirstNonEmpty(GetExtraString(extra, "currencyId"), definition.Id);
        var code = FirstNonEmpty(GetExtraString(extra, "code"), id);
        var displayName = FirstNonEmpty(GetExtraString(extra, "displayName"), GetExtraString(extra, "displayNameRu"), definition.Name, id);
        var isXp = string.Equals(id, CharacterCurrencyIds.XpCoin, StringComparison.OrdinalIgnoreCase) || GetExtraBool(extra, "isExperienceCurrency", false);
        var isMoney = !isXp && GetExtraBool(extra, "isMoneyCurrency", true);
        return new CurrencyDefinitionProjection
        {
            CurrencyId = id,
            Code = code,
            DisplayName = displayName,
            Description = FirstNonEmpty(definition.PublicDescription, GetExtraString(extra, "description")),
            DefaultValue = GetExtraLong(extra, "defaultValue", 0),
            MinValue = GetExtraLong(extra, "minValue", 0),
            MaxValue = GetExtraNullableLong(extra, "maxValue"),
            Unit = FirstNonEmpty(GetExtraString(extra, "unit"), GetExtraString(extra, "shortNameRu")),
            IconKey = GetExtraString(extra, "iconKey"),
            Kind = isXp ? "experience" : "money",
            SortOrder = GetExtraInt(extra, "sortOrder", 1000),
            IsPlayerVisible = !IsHiddenVisibility(definition.VisibilityRule) && GetExtraBool(extra, "isPlayerVisible", true),
            IsEditableByGM = GetExtraBool(extra, "isEditableByGM", true),
            SourceRuleSetId = FirstNonEmpty(definition.RuleSetIds?.FirstOrDefault(x => string.Equals(x, ruleSetId, StringComparison.OrdinalIgnoreCase)), ruleSetId),
            CurrencySetId = FirstNonEmpty(GetExtraString(extra, "currencySetId"), "fantasy_default_currencies"),
            IsMoneyCurrency = isMoney,
            IsExperienceCurrency = isXp,
            LegacyKey = FirstNonEmpty(GetExtraString(extra, "legacyKey"), LegacyCurrencyKey(id, code))
        };
    }

    private static List<CurrencyDefinitionProjection> DefaultCurrencyDefinitions(string ruleSetId)
    {
        return new List<CurrencyDefinitionProjection>
        {
            DefaultCurrency(CharacterCurrencyIds.IronCoin, "iron", "Iron coin", 0, 10, "Iron"),
            DefaultCurrency(CharacterCurrencyIds.BronzeCoin, "bronze", "Bronze coin", 0, 20, "Bronze"),
            DefaultCurrency(CharacterCurrencyIds.SilverCoin, "silver", "Silver coin", 0, 30, "Silver"),
            DefaultCurrency(CharacterCurrencyIds.GoldCoin, "gold", "Gold coin", 0, 40, "Gold"),
            DefaultCurrency(CharacterCurrencyIds.PlatinumCoin, "platinum", "Platinum coin", 0, 50, "Platinum"),
            DefaultCurrency(CharacterCurrencyIds.OrichalcumCoin, "orichalcum", "Orichalcum coin", 0, 60, "Orichalcum"),
            DefaultCurrency(CharacterCurrencyIds.AdamantCoin, "adamant", "Adamant coin", 0, 70, "Adamant"),
            DefaultCurrency(CharacterCurrencyIds.SovereignCoin, "sovereign", "Sovereign", 0, 80, "Sovereign"),
            DefaultCurrency(CharacterCurrencyIds.XpCoin, "xp_coin", "Experience coin", 0, 90, "XpCoins", isXp: true)
        };

        CurrencyDefinitionProjection DefaultCurrency(string currencyId, string code, string displayName, long defaultValue, int sortOrder, string legacyKey, bool isXp = false)
        {
            return new CurrencyDefinitionProjection
            {
                CurrencyId = currencyId,
                Code = code,
                DisplayName = displayName,
                DefaultValue = defaultValue,
                MinValue = 0,
                Kind = isXp ? "experience" : "money",
                SortOrder = sortOrder,
                IsPlayerVisible = true,
                IsEditableByGM = true,
                SourceRuleSetId = ruleSetId,
                CurrencySetId = "fantasy_default_currencies",
                IsMoneyCurrency = !isXp,
                IsExperienceCurrency = isXp,
                LegacyKey = legacyKey
            };
        }
    }

    private static string LegacyCurrencyKey(string currencyId, string code)
    {
        var normalized = FirstNonEmpty(currencyId, code).Replace("_coin", string.Empty).Replace("_", string.Empty).ToLowerInvariant();
        return normalized switch
        {
            "iron" => "Iron",
            "bronze" => "Bronze",
            "silver" => "Silver",
            "gold" => "Gold",
            "platinum" => "Platinum",
            "orichalcum" => "Orichalcum",
            "adamant" => "Adamant",
            "sovereign" => "Sovereign",
            "xp" => "XpCoins",
            _ => string.Empty
        };
    }

    private static object[] BuildLegacySkillsPayload(SkillProfile profile)
    {
        return (profile?.Skills ?? new List<CharacterSkillProfileValue>())
            .Where(x => !string.IsNullOrWhiteSpace(x.SkillId))
            .Select(x => (object)new Dictionary<string, object>
            {
                { "skillCode", x.SkillId },
                { "name", x.SkillId },
                { "description", x.Notes ?? string.Empty },
                { "type", SkillType.Passive.ToString() },
                { "available", x.IsUnlocked },
                { "reason", x.IsUnlocked ? string.Empty : "profile_locked" }
            })
            .ToArray();
    }

    private static object[] BuildCharacterSkillsPayload(SkillProfile profile)
    {
        return (profile?.Skills ?? new List<CharacterSkillProfileValue>())
            .Where(x => !string.IsNullOrWhiteSpace(x.SkillId))
            .Select(x => (object)new Dictionary<string, object>
            {
                { "skillCode", x.SkillId },
                { "tier", 1 },
                { "level", Math.Max(1, x.Rank) },
                { "acquired", x.IsLearned },
                { "learnedUtc", x.LearnedAtUtc }
            })
            .ToArray();
    }

    private static object[] BuildClassProgressPayload(DevelopmentProfile profile)
    {
        return ClassNodes(profile)
            .Select(x => (object)new Dictionary<string, object>
            {
                { "classCode", x.DevelopmentNodeId },
                { "level", Math.Max(1, x.CurrentTier) },
                { "experience", 0 }
            })
            .ToArray();
    }

    private static object[] BuildCharacterClassesPayload(DevelopmentProfile profile)
    {
        return ClassNodes(profile)
            .Where(x => x.IsPurchased || x.IsUnlocked)
            .Select(x => (object)new Dictionary<string, object>
            {
                { "classCode", x.DevelopmentNodeId },
                { "level", Math.Max(1, x.CurrentTier) },
                { "learnedUtc", x.PurchasedAtUtc }
            })
            .ToArray();
    }

    private static object[] BuildInventoryPayload(InventoryProfile profile)
    {
        return (profile?.Items ?? new List<CharacterInventoryItemProfileValue>())
            .Select(x => (object)new Dictionary<string, object>
            {
                { "id", x.ItemId },
                { "itemCode", FirstProfileNonEmpty(x.ItemDefinitionId, x.DefinitionId, x.DefinitionCode) },
                { "definitionId", FirstProfileNonEmpty(x.ItemDefinitionId, x.DefinitionId, x.DefinitionCode) },
                { "itemDefinitionId", FirstProfileNonEmpty(x.ItemDefinitionId, x.DefinitionId, x.DefinitionCode) },
                { "definitionCategory", x.DefinitionCategory ?? string.Empty },
                { "definitionCode", FirstProfileNonEmpty(x.DefinitionCode, x.ItemDefinitionId, x.DefinitionId) },
                { "snapshotDisplayName", x.SnapshotDisplayName ?? string.Empty },
                { "snapshotCategory", x.SnapshotCategory ?? string.Empty },
                { "snapshotDescription", x.SnapshotDescription ?? string.Empty },
                { "snapshotTags", (x.SnapshotTags ?? new List<string>()).Cast<object>().ToArray() },
                { "source", x.Source ?? string.Empty },
                { "name", FirstProfileNonEmpty(x.SnapshotDisplayName, x.DisplayName, x.Name) },
                { "displayName", FirstProfileNonEmpty(x.SnapshotDisplayName, x.DisplayName, x.Name) },
                { "label", FirstProfileNonEmpty(x.SnapshotDisplayName, x.DisplayName, x.Name) },
                { "description", FirstProfileNonEmpty(x.SnapshotDescription, x.Description) },
                { "category", FirstProfileNonEmpty(x.SnapshotCategory, x.Category, x.DefinitionCategory, string.Join(",", x.Tags ?? new List<string>())) },
                { "quantity", x.Quantity },
                { "durabilityOrHealth", x.Durability },
                { "durability", x.Durability },
                { "condition", x.Condition ?? string.Empty },
                { "ammo", x.Ammo },
                { "isEquipped", x.IsEquipped },
                { "equipped", x.IsEquipped },
                { "usesAmmoOrConsumable", (x.Tags ?? new List<string>()).Any(t => string.Equals(t, "consumable", StringComparison.OrdinalIgnoreCase)) },
                { "consumptionPerUse", x.Ammo },
                { "properties", x.SlotId ?? string.Empty },
                { "slot", x.SlotId ?? string.Empty },
                { "slotId", x.SlotId ?? string.Empty },
                { "notes", x.Notes ?? string.Empty },
                { "isPlayerVisible", x.IsPlayerVisible },
                { "sortOrder", x.SortOrder },
                { "createdAtUtc", x.CreatedAtUtc },
                { "updatedAtUtc", x.UpdatedAtUtc },
                { "archived", false },
                { "deleted", false }
            })
            .ToArray();
    }

    private static object[] BuildReputationPayload(ReputationProfile profile)
    {
        return (profile?.Entries ?? new List<CharacterReputationProfileValue>())
            .Select(x => (object)new Dictionary<string, object>
            {
                { "id", FirstProfileNonEmpty(x.EntryId, x.TargetId, Guid.NewGuid().ToString("N")) },
                { "scope", x.Scope ?? string.Empty },
                { "scopeType", FirstProfileNonEmpty(x.ScopeType, "Character") },
                { "groupKey", string.Equals(x.ScopeType, "Group", StringComparison.OrdinalIgnoreCase) ? x.Name ?? string.Empty : string.Empty },
                { "targetType", FirstProfileNonEmpty(x.TargetType, "Other") },
                { "targetId", x.TargetId ?? string.Empty },
                { "targetName", x.Name ?? string.Empty },
                { "value", x.Value },
                { "groupValue", x.GroupValue },
                { "status", x.Status ?? string.Empty },
                { "notes", x.Notes ?? string.Empty },
                { "isPlayerVisible", x.IsPlayerVisible },
                { "isHiddenForOthers", !x.IsPlayerVisible },
                { "archived", x.IsArchived },
                { "isArchived", x.IsArchived },
                { "source", x.Source ?? string.Empty }
            })
            .ToArray();
    }

    private static object[] BuildHoldingsPayload(HoldingsProfile profile)
    {
        return (profile?.Holdings ?? new List<CharacterHoldingProfileValue>())
            .Select(x => (object)new Dictionary<string, object>
            {
                { "id", FirstProfileNonEmpty(x.HoldingId, Guid.NewGuid().ToString("N")) },
                { "name", x.Name ?? string.Empty },
                { "type", x.HoldingType ?? string.Empty },
                { "holdingType", x.HoldingType ?? string.Empty },
                { "description", x.Description ?? string.Empty },
                { "locationId", x.LocationId ?? string.Empty },
                { "locationName", x.LocationName ?? string.Empty },
                { "owners", (x.OwnerCharacterIds ?? new List<string>()).Concat(x.OwnerUserIds ?? new List<string>()).Cast<object>().ToArray() },
                { "ownerDisplayName", x.OwnerDisplayName ?? string.Empty },
                { "legalStatus", x.LegalStatus ?? string.Empty },
                { "actualStatus", x.ActualStatus ?? string.Empty },
                { "status", FirstProfileNonEmpty(x.ActualStatus, x.LegalStatus) },
                { "notes", x.Notes ?? string.Empty },
                { "isPlayerVisible", x.IsPlayerVisible },
                { "archived", x.IsArchived },
                { "isArchived", x.IsArchived },
                { "source", x.Source ?? string.Empty }
            })
            .ToArray();
    }

    private static object[] BuildCompanionsPayload(CompanionProfile profile)
    {
        return (profile?.Companions ?? new List<CharacterCompanionProfileValue>())
            .Select(x => (object)new Dictionary<string, object>
            {
                { "id", FirstProfileNonEmpty(x.CompanionId, Guid.NewGuid().ToString("N")) },
                { "name", x.Name ?? string.Empty },
                { "type", FirstProfileNonEmpty(x.CompanionType, x.RaceOrSpeciesId) },
                { "species", FirstProfileNonEmpty(x.RaceOrSpeciesId, x.CompanionType) },
                { "description", x.Description ?? string.Empty },
                { "notes", x.Notes ?? string.Empty },
                { "ownerCharacterId", x.OwnerCharacterId ?? string.Empty },
                { "ownerDisplayName", x.OwnerDisplayName ?? string.Empty },
                { "status", x.Status ?? string.Empty },
                { "initiativeMode", x.InitiativeMode ?? string.Empty },
                { "hasSeparateInventory", x.HasSeparateInventory },
                { "isPlayerVisible", x.IsPlayerVisible },
                { "isArchived", x.IsArchived },
                { "archived", x.IsArchived },
                { "inventory", Array.Empty<object>() },
                { "holdings", Array.Empty<object>() },
                { "reputation", Array.Empty<object>() },
                { "source", x.Source ?? string.Empty }
            })
            .ToArray();
    }

    private Dictionary<string, object> BuildProfileIdentityPayload(Character character, RaceOrSpeciesProfile race, BodyProfile body, Dictionary<string, object>? shell)
    {
        var payload = CopyPayload(shell);
        var ownership = string.IsNullOrWhiteSpace(character?.Id)
            ? null
            : _mongo.CharacterOwnerships.Find(Builders<CharacterOwnershipState>.Filter.Eq(x => x.CharacterId, character.Id)).FirstOrDefault();
        var ownerUserId = ownership?.OwnerUserId ?? character?.OwnerUserId ?? string.Empty;
        var kind = FirstProfileNonEmpty(ownership?.CharacterKind ?? string.Empty, MapProfileRoleToCharacterKind(ownership?.CharacterRole));
        var status = FirstProfileNonEmpty(ownership?.CharacterStatus ?? string.Empty, ownership?.IsArchived == true ? CharacterStatusIds.Archived : ownership?.IsActive == false ? CharacterStatusIds.Inactive : CharacterStatusIds.Active);
        payload["characterId"] = character?.Id ?? string.Empty;
        payload["ownerUserId"] = ownerUserId;
        payload["ownerDisplayName"] = ownership?.OwnerDisplayName ?? string.Empty;
        payload["controlledByUserId"] = ownership?.ControlledByUserId ?? string.Empty;
        payload["controlledByDisplayName"] = ownership?.ControlledByDisplayName ?? string.Empty;
        payload["characterKind"] = kind;
        payload["characterKindDisplayName"] = CharacterKindDisplayName(kind);
        payload["characterRole"] = ownership?.CharacterRole ?? CharacterOwnershipRoleIds.PlayerCharacter;
        payload["characterStatus"] = status;
        payload["characterStatusDisplayName"] = CharacterStatusDisplayName(status);
        payload["isActive"] = ownership?.IsActive ?? !(character?.Archived ?? false);
        payload["isArchived"] = ownership?.IsArchived ?? (character?.Archived ?? false);
        payload["name"] = FirstProfileNonEmpty(ownership?.CharacterDisplayName ?? string.Empty, character?.Name ?? string.Empty);
        payload["race"] = FirstProfileNonEmpty(race.DisplayName, race.RaceName, race.RaceCode, race.RaceId, "—");
        payload["raceCode"] = FirstProfileNonEmpty(race.RaceCode, race.RaceId);
        payload["age"] = ProjectWorldAge(character?.Id ?? string.Empty, body);
        payload["height"] = FirstProfileNonEmpty(body.HeightText, body.HeightCm > 0 ? body.HeightCm + " cm" : string.Empty);
        payload["description"] = body.Description ?? string.Empty;
        payload["backstory"] = body.Backstory ?? string.Empty;
        payload["originBaseHealth"] = body.BaseHealth;
        payload["originNaturalArmorRating"] = body.NaturalArmorRating;
        payload["originNaturalPenetrationResistance"] = body.NaturalPenetrationResistance;
        payload["originLifespanDisplay"] = body.AdultAgeYears > 0
            ? $"Взросление: {body.AdultAgeYears}; ожидаемая жизнь: {body.AverageLifespanYears}; предельная: {body.MaximumLifespanYears}"
            : string.Empty;
        payload["originTraitNames"] = ResolvePlayerVisibleOriginTraitNames(race).Cast<object>().ToArray();
        payload["originSenseNames"] = (body.RacialSenses ?? new List<RacialSenseDefinition>()).Select(x => x.DisplayName).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal).Cast<object>().ToArray();
        payload["originMovementNames"] = (body.MovementAbilities ?? new List<RacialMovementAbilityDefinition>()).Select(x => x.DisplayName).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal).Cast<object>().ToArray();
        payload["originEquipmentFitWarning"] = body.EquipmentFit?.PublicWarning ?? string.Empty;
        payload["archived"] = ownership?.IsArchived ?? (character?.Archived ?? false);
        payload["deleted"] = character?.Deleted ?? false;
        payload["schemaVersion"] = character?.SchemaVersion ?? 1;
        payload["visibility"] = new Dictionary<string, object>
        {
            { "hideDescriptionForOthers", character?.Visibility?.HideDescriptionForOthers ?? false },
            { "hideBackstoryForOthers", character?.Visibility?.HideBackstoryForOthers ?? false },
            { "hideStatsForOthers", character?.Visibility?.HideStatsForOthers ?? false },
            { "hideReputationForOthers", character?.Visibility?.HideReputationForOthers ?? false },
            { "hideRaceForOthers", character?.Visibility?.HideRaceForOthers ?? false },
            { "hideHeightForOthers", character?.Visibility?.HideHeightForOthers ?? false },
            { "hideInventoryForOthers", character?.Visibility?.HideInventoryForOthers ?? false }
        };
        return payload;
    }

    private List<string> ResolvePlayerVisibleOriginTraitNames(RaceOrSpeciesProfile race)
    {
        var ids = race?.ResolvedTraitIds?.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal).ToList() ?? new List<string>();
        if (ids.Count == 0) return new List<string>();
        return _mongo.ContentDefinitionRecords.Find(x => x.Category == "race_trait_definition" && !x.IsArchived
                && (ids.Contains(x.StableKey) || ids.Contains(x.ShortCode) || ids.Contains(x.Id))
                && (x.VisibilityRule == ContentDefinitionVisibilityRules.Public || x.VisibilityRule == ContentDefinitionVisibilityRules.PlayerVisible))
            .ToList().Select(x => FirstProfileNonEmpty(x.DisplayName, x.Name)).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal).ToList();
    }

    private object ProjectWorldAge(string characterId, BodyProfile body)
    {
        var baseAge = body.AgeAnchorYears > 0 ? body.AgeAnchorYears : body.AgeYears;
        if (baseAge <= 0) return FirstProfileNonEmpty(body.AgeText);
        if (body.AgeAnchorWorldYearLengthDays <= 0 || string.IsNullOrWhiteSpace(characterId)) return baseAge;
        var ownership = _mongo.CharacterOwnerships.Find(x => x.CharacterId == characterId).FirstOrDefault();
        if (ownership == null) return baseAge;
        var worldTime = _mongo.CampaignWorldTimes.Find(x => x.CampaignId == ownership.CampaignId && !x.Deleted && !x.Archived)
            .SortByDescending(x => x.UpdatedAtUtc).FirstOrDefault();
        if (worldTime == null) return baseAge;
        var elapsedDays = Math.Max(0, worldTime.CurrentDateTime.AbsoluteDayIndex - body.AgeAnchorWorldAbsoluteDay);
        return baseAge + (elapsedDays / body.AgeAnchorWorldYearLengthDays);
    }

    private static AttributeProfile EmptyAttributeProfile(string characterId) => new AttributeProfile { CharacterId = characterId ?? string.Empty, RuleSetId = RuleSetIds.FantasyNriDefault, Values = new List<CharacterAttributeValue>(), SchemaVersion = 1 };
    private static WalletProfile EmptyWalletProfile(string characterId) => new WalletProfile { CharacterId = characterId ?? string.Empty, RuleSetId = RuleSetIds.FantasyNriDefault, Wallets = new List<CharacterWalletValue>(), SchemaVersion = 1 };
    private static SkillProfile EmptySkillProfile(string characterId) => new SkillProfile { CharacterId = characterId ?? string.Empty, RuleSetId = RuleSetIds.FantasyNriDefault, Skills = new List<CharacterSkillProfileValue>(), SchemaVersion = 1 };
    private static DevelopmentProfile EmptyDevelopmentProfile(string characterId) => new DevelopmentProfile { CharacterId = characterId ?? string.Empty, RuleSetId = RuleSetIds.FantasyNriDefault, Nodes = new List<CharacterDevelopmentNodeState>(), ActiveHexagonIds = new List<string>(), SchemaVersion = 1 };
    private static InventoryProfile EmptyInventoryProfile(string characterId) => new InventoryProfile { CharacterId = characterId ?? string.Empty, RuleSetId = RuleSetIds.FantasyNriDefault, Items = new List<CharacterInventoryItemProfileValue>(), SchemaVersion = 1 };
    private static ReputationProfile EmptyReputationProfile(string characterId) => new ReputationProfile { CharacterId = characterId ?? string.Empty, RuleSetId = RuleSetIds.FantasyNriDefault, Entries = new List<CharacterReputationProfileValue>(), SchemaVersion = 1 };
    private static HoldingsProfile EmptyHoldingsProfile(string characterId) => new HoldingsProfile { CharacterId = characterId ?? string.Empty, RuleSetId = RuleSetIds.FantasyNriDefault, Holdings = new List<CharacterHoldingProfileValue>(), SchemaVersion = 1 };
    private static CompanionProfile EmptyCompanionProfile(string characterId) => new CompanionProfile { CharacterId = characterId ?? string.Empty, RuleSetId = RuleSetIds.FantasyNriDefault, Companions = new List<CharacterCompanionProfileValue>(), SchemaVersion = 1 };
    private static RaceOrSpeciesProfile EmptyRaceProfile(string characterId) => new RaceOrSpeciesProfile { CharacterId = characterId ?? string.Empty, RuleSetId = RuleSetIds.FantasyNriDefault, Tags = new List<string>(), SchemaVersion = 1 };
    private static BodyProfile EmptyBodyProfile(string characterId) => new BodyProfile { CharacterId = characterId ?? string.Empty, RuleSetId = RuleSetIds.FantasyNriDefault, BodyTags = new List<string>(), EquipmentCompatibilityTags = new List<string>(), SchemaVersion = 1 };

    private static string FirstProfileNonEmpty(params string[] values) => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private static string FirstNonEmpty(params string[] values) => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private static string MapProfileRoleToCharacterKind(string? role)
    {
        return (role ?? string.Empty).Trim().ToLowerInvariant().Replace("_", string.Empty) switch
        {
            "npc" => CharacterKindIds.Npc,
            "companion" => CharacterKindIds.Companion,
            "temporaryally" => CharacterKindIds.TemporaryAlly,
            "enemy" => CharacterKindIds.Enemy,
            "neutral" => CharacterKindIds.Neutral,
            "custom" => CharacterKindIds.Custom,
            _ => CharacterKindIds.PlayerCharacter
        };
    }

    private static string CharacterKindDisplayName(string value)
    {
        return (value ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            CharacterKindIds.Npc => "NPC",
            CharacterKindIds.Companion => "Компаньон",
            CharacterKindIds.TemporaryAlly => "Временный союзник",
            CharacterKindIds.Enemy => "Враг",
            CharacterKindIds.Neutral => "Нейтральный",
            CharacterKindIds.Custom => "Другое",
            _ => "Персонаж игрока"
        };
    }

    private static string CharacterStatusDisplayName(string value)
    {
        return (value ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            CharacterStatusIds.Inactive => "Неактивен",
            CharacterStatusIds.Archived => "В архиве",
            _ => "Активен"
        };
    }

    private static string GetExtraString(Dictionary<string, object> extra, string key)
    {
        if (extra == null || !extra.TryGetValue(key, out var value) || value == null) return string.Empty;
        return Convert.ToString(value) ?? string.Empty;
    }

    private static int GetExtraInt(Dictionary<string, object> extra, string key, int fallback)
    {
        var raw = GetExtraString(extra, key);
        return int.TryParse(raw, out var value) ? value : fallback;
    }

    private static long GetExtraLong(Dictionary<string, object> extra, string key, long fallback)
    {
        var raw = GetExtraString(extra, key);
        return long.TryParse(raw, out var value) ? value : fallback;
    }

    private static long? GetExtraNullableLong(Dictionary<string, object> extra, string key)
    {
        var raw = GetExtraString(extra, key);
        return long.TryParse(raw, out var value) ? value : null;
    }

    private static bool GetExtraBool(Dictionary<string, object> extra, string key, bool fallback)
    {
        var raw = GetExtraString(extra, key);
        return bool.TryParse(raw, out var value) ? value : fallback;
    }

    private static bool IsHiddenVisibility(string visibilityRule)
    {
        return string.Equals(visibilityRule, VisibilityRuleIds.GmOnly, StringComparison.OrdinalIgnoreCase)
            || string.Equals(visibilityRule, VisibilityRuleIds.ServerOnly, StringComparison.OrdinalIgnoreCase)
            || string.Equals(visibilityRule, VisibilityRuleIds.SuperAdminOnly, StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<CharacterDevelopmentNodeState> ClassNodes(DevelopmentProfile profile)
    {
        return (profile?.Nodes ?? new List<CharacterDevelopmentNodeState>())
            .Where(x => !string.IsNullOrWhiteSpace(x.DevelopmentNodeId))
            .Where(x => string.Equals(x.NodeType, DevelopmentNodeTypes.Class, StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(x.NodeType))
            .GroupBy(x => x.DevelopmentNodeId, StringComparer.Ordinal)
            .Select(x => x.Last());
    }

    private static int GetAttribute(Dictionary<string, int> values, string key)
    {
        return values.TryGetValue(key, out var value) ? value : 0;
    }

    private static int GetFirstAttribute(Dictionary<string, int> values, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!string.IsNullOrWhiteSpace(key) && values.TryGetValue(key, out var value)) return value;
        }

        return 0;
    }

    private static long GetWalletAmount(WalletProfile? profile, string currencyId)
    {
        return (profile?.Wallets ?? new List<CharacterWalletValue>())
            .Where(x => string.Equals(x.CurrencyId, currencyId, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Amount)
            .DefaultIfEmpty(0L)
            .Last();
    }

    private static Dictionary<string, object> CurrencyRow(string code, long amount, string kind)
    {
        return new Dictionary<string, object> { { "code", code }, { "amount", amount }, { "kind", kind } };
    }

    private void LogProfileDifferences(string characterId, Dictionary<string, object> legacyPayload, Dictionary<string, object> profilePayload)
    {
        LogSectionDifference(characterId, "stats", legacyPayload, profilePayload);
        LogSectionDifference(characterId, "money", legacyPayload, profilePayload);
        LogSectionDifference(characterId, "inventory", legacyPayload, profilePayload);
        LogSectionDifference(characterId, "skills", legacyPayload, profilePayload);
        LogSectionDifference(characterId, "characterSkills", legacyPayload, profilePayload);
        LogSectionDifference(characterId, "classProgress", legacyPayload, profilePayload);
        LogSectionDifference(characterId, "characterClasses", legacyPayload, profilePayload);
    }

    private void LogSectionDifference(string characterId, string section, Dictionary<string, object> legacyPayload, Dictionary<string, object> profilePayload)
    {
        legacyPayload.TryGetValue(section, out var legacyValue);
        profilePayload.TryGetValue(section, out var profileValue);
        var legacyCount = CountPayloadItems(legacyValue);
        var profileCount = CountPayloadItems(profileValue);
        if (legacyCount != profileCount)
        {
            _logger.Debug($"profile.details.diff characterId={characterId} section={section} legacyCount={legacyCount} profileCount={profileCount}");
        }
    }

    private static int CountPayloadItems(object? value)
    {
        if (value == null || value is string) return 0;
        if (value is IDictionary<string, object> dict) return dict.Count;
        if (value is System.Collections.ICollection collection) return collection.Count;
        return 1;
    }

    private static Dictionary<string, object> CopyPayload(Dictionary<string, object>? source)
    {
        return source == null
            ? new Dictionary<string, object>()
            : new Dictionary<string, object>(source, StringComparer.Ordinal);
    }

    private static void UpsertByCharacterId<TDoc>(MongoDB.Driver.IMongoCollection<TDoc> collection, string characterId, TDoc doc) where TDoc : EntityBase
    {
        var filter = Builders<TDoc>.Filter.Eq("CharacterId", characterId);
        var existing = collection.Find(filter).FirstOrDefault();
        if (existing != null)
        {
            doc.Id = existing.Id;
            doc.CreatedUtc = existing.CreatedUtc;
        }

        doc.UpdatedUtc = DateTime.UtcNow;
        var result = collection.ReplaceOne(filter, doc, new ReplaceOptions { IsUpsert = true });
        if (!result.IsAcknowledged) throw new InvalidOperationException("profile_replace_not_acknowledged");
    }

    private sealed class AttributeDefinitionProjection
    {
        public string AttributeId { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int MinValue { get; set; }
        public int MaxValue { get; set; } = 30;
        public int DefaultValue { get; set; } = 10;
        public int SortOrder { get; set; }
        public string AttributeSetId { get; set; } = string.Empty;
        public string SourceRuleSetId { get; set; } = string.Empty;
        public bool IsPlayerVisible { get; set; } = true;
        public bool IsEditableByGM { get; set; } = true;
    }

    private sealed class CharacterStatDefinitionProjection
    {
        public string DefinitionId { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = "derived";
        public int MinValue { get; set; }
        public int MaxValue { get; set; } = 999;
        public int DefaultValue { get; set; }
        public string Unit { get; set; } = string.Empty;
        public string Formula { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public bool IsPlayerVisible { get; set; } = true;
        public bool IsEditableByGM { get; set; } = true;
        public bool IsDerived { get; set; } = true;
        public string SourceRuleSetId { get; set; } = string.Empty;
        public bool IsVitalGroup => string.Equals(Category, "vital", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Category, "defense", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Category, "morale", StringComparison.OrdinalIgnoreCase);
        public bool IsDerivedGroup => !IsVitalGroup;
    }

    private sealed class CurrencyDefinitionProjection
    {
        public string CurrencyId { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public long DefaultValue { get; set; }
        public long MinValue { get; set; }
        public long? MaxValue { get; set; }
        public string Unit { get; set; } = string.Empty;
        public string IconKey { get; set; } = string.Empty;
        public string Kind { get; set; } = "money";
        public int SortOrder { get; set; }
        public bool IsPlayerVisible { get; set; } = true;
        public bool IsEditableByGM { get; set; } = true;
        public string SourceRuleSetId { get; set; } = string.Empty;
        public string CurrencySetId { get; set; } = string.Empty;
        public bool IsMoneyCurrency { get; set; } = true;
        public bool IsExperienceCurrency { get; set; }
        public string LegacyKey { get; set; } = string.Empty;
    }
}
