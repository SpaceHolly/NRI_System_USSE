using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Driver;
using Nri.Server.Infrastructure;
using Nri.Server.Logging;
using Nri.Shared.Domain;
using Nri.Shared.Utilities;

namespace Nri.Server.Application;

public sealed class ProfileNativeWriteResult
{
    public string CharacterId { get; set; } = string.Empty;
    public string Section { get; set; } = string.Empty;
    public bool UsedProfileNative { get; set; }
    public bool UsedFallback { get; set; }
    public bool ProfileFound { get; set; }
    public bool ProfileCreatedFromLegacy { get; set; }
    public bool ProfileWritten { get; set; }
    public bool LegacyFacadeSynced { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public DateTime WrittenAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class ProfileNativeKnowledgeWriteResult
{
    public string CharacterId { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public bool UsedProfileNative { get; set; }
    public bool ProfileWritten { get; set; }
    public bool TopicAdded { get; set; }
    public bool AlreadyKnown { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public DateTime WrittenAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class ProfileNativeWriteDiagnosticResult
{
    public string CharacterId { get; set; } = string.Empty;
    public string Section { get; set; } = string.Empty;
    public bool UsedProfileNative { get; set; }
    public bool UsedFallback { get; set; }
    public bool ProfileFound { get; set; }
    public bool ProfileCreatedFromLegacy { get; set; }
    public bool ProfileWritten { get; set; }
    public bool LegacyFacadeSynced { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public DateTime WrittenAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class ProfileNativeSkillWriteResult
{
    public string CharacterId { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public string SkillId { get; set; } = string.Empty;
    public bool UsedProfileNative { get; set; }
    public bool UsedFallback { get; set; }
    public bool ProfileFound { get; set; }
    public bool ProfileCreatedFromLegacy { get; set; }
    public bool ProfileWritten { get; set; }
    public bool LegacyFacadeSynced { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public DateTime WrittenAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class ProfileNativeDevelopmentWriteResult
{
    public string CharacterId { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public string DevelopmentNodeId { get; set; } = string.Empty;
    public bool UsedProfileNative { get; set; }
    public bool UsedFallback { get; set; }
    public bool ProfileFound { get; set; }
    public bool ProfileCreatedFromLegacy { get; set; }
    public bool ProfileWritten { get; set; }
    public bool LegacyFacadeSynced { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public DateTime WrittenAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class ProfileNativeInventoryWriteResult
{
    public string CharacterId { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public string ItemId { get; set; } = string.Empty;
    public bool UsedProfileNative { get; set; }
    public bool UsedFallback { get; set; }
    public bool ProfileFound { get; set; }
    public bool ProfileCreatedFromLegacy { get; set; }
    public bool ProfileWritten { get; set; }
    public bool LegacyFacadeSynced { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public DateTime WrittenAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class ProfileNativeRaceBodyWriteResult
{
    public string CharacterId { get; set; } = string.Empty;
    public bool UsedProfileNative { get; set; }
    public bool UsedFallback { get; set; }
    public bool RaceProfileFound { get; set; }
    public bool RaceProfileCreatedFromLegacy { get; set; }
    public bool BodyProfileFound { get; set; }
    public bool BodyProfileCreatedFromLegacy { get; set; }
    public bool RaceProfileWritten { get; set; }
    public bool BodyProfileWritten { get; set; }
    public bool LegacyFacadeSynced { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public DateTime WrittenAtUtc { get; set; } = DateTime.UtcNow;
}

public interface ICharacterProfileNativeWriteService
{
    Task<ProfileNativeWriteResult> UpdateAttributeProfileAsync(string characterId, Dictionary<string, object> payload, string actorUserId, string requestId);
    Task<ProfileNativeWriteResult> UpdateWalletProfileAsync(string characterId, Dictionary<string, object> payload, string actorUserId, string requestId);
    Task<ProfileNativeSkillWriteResult> AddSkillProfileNativeAsync(string characterId, Dictionary<string, object> payload, string actorUserId, string requestId);
    Task<ProfileNativeSkillWriteResult> UpdateSkillProfileNativeAsync(string characterId, Dictionary<string, object> payload, string actorUserId, string requestId);
    Task<ProfileNativeSkillWriteResult> RemoveSkillProfileNativeAsync(string characterId, Dictionary<string, object> payload, string actorUserId, string requestId);
    Task<ProfileNativeDevelopmentWriteResult> AssignClassProfileNativeAsync(string characterId, Dictionary<string, object> payload, string actorUserId, string requestId);
    Task<ProfileNativeInventoryWriteResult> UpdateInventoryProfileNativeAsync(string characterId, Dictionary<string, object> payload, string actorUserId, string requestId);
    Task<ProfileNativeInventoryWriteResult> AddInventoryItemProfileNativeAsync(string characterId, Dictionary<string, object> payload, string actorUserId, string requestId);
    Task<ProfileNativeInventoryWriteResult> UpdateInventoryItemProfileNativeAsync(string characterId, Dictionary<string, object> payload, string actorUserId, string requestId);
    Task<ProfileNativeInventoryWriteResult> RemoveInventoryItemProfileNativeAsync(string characterId, Dictionary<string, object> payload, string actorUserId, string requestId);
    Task<ProfileNativeInventoryWriteResult> ToggleEquipInventoryItemProfileNativeAsync(string characterId, Dictionary<string, object> payload, string actorUserId, string requestId);
    Task<ProfileNativeRaceBodyWriteResult> UpdateRaceOrSpeciesProfileNativeAsync(string characterId, Dictionary<string, object> payload, string actorUserId, string requestId);
    Task<ProfileNativeRaceBodyWriteResult> UpdateBodyProfileNativeAsync(string characterId, Dictionary<string, object> payload, string actorUserId, string requestId);
    Task<ProfileNativeRaceBodyWriteResult> UpdateRaceBodyProfilesNativeAsync(string characterId, Dictionary<string, object> payload, string actorUserId, string requestId);
    Task<ProfileNativeRaceBodyWriteResult> UpdateBiographyProfileNativeAsync(string characterId, Dictionary<string, object> payload, string actorUserId, string requestId);
    Task<ProfileNativeKnowledgeWriteResult> UnlockKnowledgeTopicProfileNativeAsync(string characterId, string topic, string actorUserId, string requestId);
    Task<ProfileNativeWriteResult> SyncLegacyStatsFacadeAsync(string characterId, AttributeProfile profile, string actorUserId, string requestId);
    Task<ProfileNativeWriteResult> SyncLegacyWalletFacadeAsync(string characterId, WalletProfile profile, string actorUserId, string requestId);
    Task<ProfileNativeSkillWriteResult> SyncLegacySkillFacadeAsync(string characterId, SkillProfile profile, string actorUserId, string requestId);
    Task<ProfileNativeDevelopmentWriteResult> SyncLegacyDevelopmentFacadeAsync(string characterId, DevelopmentProfile profile, string actorUserId, string requestId);
    Task<ProfileNativeInventoryWriteResult> SyncLegacyInventoryFacadeAsync(string characterId, InventoryProfile profile, string actorUserId, string requestId);
    Task<ProfileNativeRaceBodyWriteResult> SyncLegacyRaceBodyFacadeAsync(string characterId, RaceOrSpeciesProfile raceProfile, BodyProfile bodyProfile, string actorUserId, string requestId);
}

public sealed class CharacterProfileNativeWriteService : ICharacterProfileNativeWriteService
{
    private const string StatsSection = "stats";
    private const string WalletSection = "wallet";
    private const string SkillsSection = "skills";
    private const string SkillAddOperation = "add";
    private const string SkillUpdateOperation = "update";
    private const string SkillRemoveOperation = "remove";
    private const string DevelopmentSection = "development";
    private const string ClassAssignOperation = "assign";
    private const string InventorySection = "inventory";
    private const string InventoryUpdateOperation = "update";
    private const string InventoryAddOperation = "add";
    private const string InventoryRemoveOperation = "remove";
    private const string InventoryToggleEquipOperation = "toggleEquip";
    private const string RaceBodySection = "raceBody";
    private const string RaceSection = "race";
    private const string BodySection = "body";
    private const string BiographySection = "biography";
    private const string KnowledgeSection = "knowledge";

    private static readonly Dictionary<string, string> StatPayloadToAttributeIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        { "health", CharacterAttributeIds.Health },
        { "physicalArmor", CharacterAttributeIds.PhysicalArmor },
        { "magicalArmor", CharacterAttributeIds.MagicArmor },
        { "morale", CharacterAttributeIds.Morale },
        { "strength", CharacterAttributeIds.Strength },
        { "dexterity", CharacterAttributeIds.Dexterity },
        { "endurance", CharacterAttributeIds.Endurance },
        { "wisdom", CharacterAttributeIds.Wisdom },
        { "intellect", CharacterAttributeIds.Intellect },
        { "charisma", CharacterAttributeIds.Charisma }
    };

    private static readonly Dictionary<CurrencyDenomination, string> DenominationToCurrencyIds = new Dictionary<CurrencyDenomination, string>
    {
        { CurrencyDenomination.Iron, CharacterCurrencyIds.IronCoin },
        { CurrencyDenomination.Bronze, CharacterCurrencyIds.BronzeCoin },
        { CurrencyDenomination.Silver, CharacterCurrencyIds.SilverCoin },
        { CurrencyDenomination.Gold, CharacterCurrencyIds.GoldCoin },
        { CurrencyDenomination.Platinum, CharacterCurrencyIds.PlatinumCoin },
        { CurrencyDenomination.Orichalcum, CharacterCurrencyIds.OrichalcumCoin },
        { CurrencyDenomination.Adamant, CharacterCurrencyIds.AdamantCoin },
        { CurrencyDenomination.Sovereign, CharacterCurrencyIds.SovereignCoin }
    };

    private readonly MongoContext _mongo;
    private readonly IServerLogger _logger;
    private readonly ICharacterAttributeProfileFactory _attributeFactory;
    private readonly ICharacterWalletProfileFactory _walletFactory;
    private readonly ICharacterSkillProfileFactory _skillFactory;
    private readonly ICharacterDevelopmentProfileFactory _developmentFactory;
    private readonly ICharacterInventoryProfileFactory _inventoryFactory;
    private readonly IRaceOrSpeciesProfileShadowBuilder _raceOrSpeciesBuilder;
    private readonly IBodyProfileShadowBuilder _bodyBuilder;

    public CharacterProfileNativeWriteService(MongoContext mongo, IServerLogger logger, ICharacterAttributeProfileFactory attributeFactory, ICharacterWalletProfileFactory walletFactory, ICharacterSkillProfileFactory skillFactory, ICharacterDevelopmentProfileFactory developmentFactory, ICharacterInventoryProfileFactory inventoryFactory, IRaceOrSpeciesProfileShadowBuilder raceOrSpeciesBuilder, IBodyProfileShadowBuilder bodyBuilder)
    {
        _mongo = mongo;
        _logger = logger;
        _attributeFactory = attributeFactory;
        _walletFactory = walletFactory;
        _skillFactory = skillFactory;
        _developmentFactory = developmentFactory;
        _inventoryFactory = inventoryFactory;
        _raceOrSpeciesBuilder = raceOrSpeciesBuilder;
        _bodyBuilder = bodyBuilder;
    }

    public Task<ProfileNativeWriteResult> UpdateAttributeProfileAsync(string characterId, Dictionary<string, object> payload, string actorUserId, string requestId)
    {
        _logger.Debug($"profile.native.write.start section={StatsSection} characterId={characterId}");
        try
        {
            var character = LoadCharacter(characterId);
            if (character == null) return Task.FromResult(Fallback(characterId, StatsSection, "legacy_character_missing"));

            var loaded = LoadAttributeProfileOrShadow(character);
            if (!loaded.IsValid)
            {
                LogProfileInvalid(StatsSection, characterId, loaded.ErrorMessage);
                return Task.FromResult(Fallback(characterId, StatsSection, "invalid_profile"));
            }

            var profile = loaded.Profile!;

            var updated = 0;
            var dynamicAttributes = ReadAttributeRows(payload);
            if (dynamicAttributes.Count > 0)
            {
                var definitions = LoadAttributeDefinitions(profile.RuleSetId);
                foreach (var row in dynamicAttributes)
                {
                    if (string.IsNullOrWhiteSpace(row.AttributeId)) return Task.FromResult(Fallback(characterId, StatsSection, "missing_payload:attributeId"));
                    if (!row.Value.HasValue) return Task.FromResult(Fallback(characterId, StatsSection, $"missing_payload:value:{row.AttributeId}"));

                    if (!definitions.TryGetValue(row.AttributeId, out var definition))
                    {
                        return Task.FromResult(Fallback(characterId, StatsSection, $"unknown_attribute:{row.AttributeId}"));
                    }

                    if (row.Value.Value < definition.MinValue || row.Value.Value > definition.MaxValue)
                    {
                        return Task.FromResult(Fallback(characterId, StatsSection, $"invalid_payload:{row.AttributeId}"));
                    }

                    UpsertAttribute(profile, row.AttributeId, row.Value.Value);
                    updated++;
                }
            }
            else
            {
                foreach (var pair in StatPayloadToAttributeIds)
                {
                    var value = PayloadReader.GetInt(payload, pair.Key);
                    if (!value.HasValue) return Task.FromResult(Fallback(characterId, StatsSection, $"missing_payload:{pair.Key}"));
                    if (value.Value < 0 || value.Value > 999) return Task.FromResult(Fallback(characterId, StatsSection, $"invalid_payload:{pair.Key}"));
                    UpsertAttribute(profile, pair.Value, value.Value);
                    updated++;
                }
            }

            if (updated == 0) return Task.FromResult(Fallback(characterId, StatsSection, "no_valid_payload"));

            UpsertByCharacterId(_mongo.CharacterAttributeProfiles, characterId, new CharacterAttributeProfileDocument { CharacterId = characterId, Profile = profile });
            var sync = SyncLegacyStatsFacadeAsync(characterId, profile, actorUserId, requestId).GetAwaiter().GetResult();
            if (!sync.LegacyFacadeSynced)
            {
                _logger.Debug($"profile.native.write.facade_sync_error section={StatsSection} characterId={characterId} message={sync.ErrorMessage}");
                return Task.FromResult(new ProfileNativeWriteResult { CharacterId = characterId, Section = StatsSection, UsedProfileNative = true, ProfileFound = loaded.ProfileFound, ProfileCreatedFromLegacy = loaded.ProfileCreatedFromLegacy, ProfileWritten = true, LegacyFacadeSynced = false, ErrorMessage = sync.ErrorMessage, WrittenAtUtc = DateTime.UtcNow });
            }

            _logger.Debug($"profile.native.write.done section={StatsSection} characterId={characterId}");
            return Task.FromResult(new ProfileNativeWriteResult { CharacterId = characterId, Section = StatsSection, UsedProfileNative = true, ProfileFound = loaded.ProfileFound, ProfileCreatedFromLegacy = loaded.ProfileCreatedFromLegacy, ProfileWritten = true, LegacyFacadeSynced = true, WrittenAtUtc = DateTime.UtcNow });
        }
        catch (Exception ex)
        {
            return Task.FromResult(Fallback(characterId, StatsSection, ex.Message));
        }
    }

    public Task<ProfileNativeWriteResult> UpdateWalletProfileAsync(string characterId, Dictionary<string, object> payload, string actorUserId, string requestId)
    {
        _logger.Debug($"profile.native.write.start section={WalletSection} characterId={characterId}");
        try
        {
            var character = LoadCharacter(characterId);
            if (character == null) return Task.FromResult(Fallback(characterId, WalletSection, "legacy_character_missing"));

            var moneyPayload = PayloadReader.GetDictionary(payload, "money") ?? new Dictionary<string, object>();
            var loaded = LoadWalletProfileOrShadow(character);
            if (!loaded.IsValid)
            {
                LogProfileInvalid(WalletSection, characterId, loaded.ErrorMessage);
                return Task.FromResult(Fallback(characterId, WalletSection, "invalid_profile"));
            }

            var profile = loaded.Profile!;
            var definitions = LoadCurrencyDefinitions(profile.RuleSetId);

            var updated = 0;
            foreach (var row in ReadCurrencyRows(payload))
            {
                var resolved = ResolveCurrencyDefinition(definitions, row.CurrencyId, row.Code);
                var currencyId = resolved?.CurrencyId ?? FirstNonEmpty(row.CurrencyId, row.Code);
                if (string.IsNullOrWhiteSpace(currencyId)) continue;
                var value = row.Amount;
                if (!value.HasValue) continue;
                if (value.Value < (resolved?.MinValue ?? 0)) return Task.FromResult(Fallback(characterId, WalletSection, $"invalid_payload:{currencyId}"));
                if (resolved?.MaxValue.HasValue == true && value.Value > resolved.MaxValue.Value) return Task.FromResult(Fallback(characterId, WalletSection, $"invalid_payload:{currencyId}:max"));
                UpsertWallet(profile, currencyId, value.Value);
                updated++;
            }

            foreach (var pair in DenominationToCurrencyIds)
            {
                var value = PayloadReader.GetLong(moneyPayload, pair.Key.ToString());
                if (!value.HasValue) continue;
                if (value.Value < 0) return Task.FromResult(Fallback(characterId, WalletSection, $"invalid_payload:{pair.Key}"));
                UpsertWallet(profile, pair.Value, value.Value);
                updated++;
            }

            foreach (var definition in definitions.Values)
            {
                var value = PayloadReader.GetLong(moneyPayload, definition.CurrencyId)
                    ?? PayloadReader.GetLong(moneyPayload, definition.Code)
                    ?? (string.IsNullOrWhiteSpace(definition.LegacyKey) ? null : PayloadReader.GetLong(moneyPayload, definition.LegacyKey));
                if (!value.HasValue) continue;
                if (value.Value < definition.MinValue) return Task.FromResult(Fallback(characterId, WalletSection, $"invalid_payload:{definition.CurrencyId}"));
                if (definition.MaxValue.HasValue && value.Value > definition.MaxValue.Value) return Task.FromResult(Fallback(characterId, WalletSection, $"invalid_payload:{definition.CurrencyId}:max"));
                UpsertWallet(profile, definition.CurrencyId, value.Value);
                updated++;
            }

            var xpCoins = PayloadReader.GetLong(payload, "xpCoins") ?? PayloadReader.GetLong(moneyPayload, "XpCoins") ?? PayloadReader.GetLong(moneyPayload, "xpCoins");
            if (xpCoins.HasValue)
            {
                if (xpCoins.Value < 0) return Task.FromResult(Fallback(characterId, WalletSection, "invalid_payload:xpCoins"));
                UpsertWallet(profile, CharacterCurrencyIds.XpCoin, xpCoins.Value);
                updated++;
            }

            if (updated == 0) return Task.FromResult(Fallback(characterId, WalletSection, "no_valid_payload"));

            UpsertByCharacterId(_mongo.CharacterWalletProfiles, characterId, new CharacterWalletProfileDocument { CharacterId = characterId, Profile = profile });
            var sync = SyncLegacyWalletFacadeAsync(characterId, profile, actorUserId, requestId).GetAwaiter().GetResult();
            if (!sync.LegacyFacadeSynced)
            {
                _logger.Debug($"profile.native.write.facade_sync_error section={WalletSection} characterId={characterId} message={sync.ErrorMessage}");
                return Task.FromResult(new ProfileNativeWriteResult { CharacterId = characterId, Section = WalletSection, UsedProfileNative = true, ProfileFound = loaded.ProfileFound, ProfileCreatedFromLegacy = loaded.ProfileCreatedFromLegacy, ProfileWritten = true, LegacyFacadeSynced = false, ErrorMessage = sync.ErrorMessage, WrittenAtUtc = DateTime.UtcNow });
            }

            _logger.Debug($"profile.native.write.done section={WalletSection} characterId={characterId}");
            return Task.FromResult(new ProfileNativeWriteResult { CharacterId = characterId, Section = WalletSection, UsedProfileNative = true, ProfileFound = loaded.ProfileFound, ProfileCreatedFromLegacy = loaded.ProfileCreatedFromLegacy, ProfileWritten = true, LegacyFacadeSynced = true, WrittenAtUtc = DateTime.UtcNow });
        }
        catch (Exception ex)
        {
            return Task.FromResult(Fallback(characterId, WalletSection, ex.Message));
        }
    }

    public Task<ProfileNativeSkillWriteResult> AddSkillProfileNativeAsync(string characterId, Dictionary<string, object> payload, string actorUserId, string requestId)
    {
        var skillId = ResolveSkillId(payload);
        _logger.Debug($"profile.native.write.start section={SkillsSection} operation={SkillAddOperation} characterId={characterId} skillId={skillId}");
        try
        {
            if (string.IsNullOrWhiteSpace(skillId)) return Task.FromResult(SkillFallback(characterId, SkillAddOperation, skillId, "missing_skill_id"));
            var character = LoadCharacter(characterId);
            if (character == null) return Task.FromResult(SkillFallback(characterId, SkillAddOperation, skillId, "legacy_character_missing"));

            var loaded = LoadSkillProfileOrShadow(character);
            if (!loaded.IsValid)
            {
                LogProfileInvalid(SkillsSection, characterId, loaded.ErrorMessage);
                return Task.FromResult(SkillFallback(characterId, SkillAddOperation, skillId, "invalid_profile"));
            }

            var profile = loaded.Profile!;
            var definition = LoadSkillDefinition(skillId);
            if (definition == null) return Task.FromResult(SkillFallback(characterId, SkillAddOperation, skillId, "skill_definition_missing"));
            if (FindSkill(profile, skillId) != null) return Task.FromResult(SkillFallback(characterId, SkillAddOperation, skillId, "skill_already_exists"));

            var level = PayloadReader.GetInt(payload, "level");
            if (!level.HasValue) return Task.FromResult(SkillFallback(characterId, SkillAddOperation, skillId, "missing_level"));
            var rank = ResolveRequestedSkillRank(level.Value, definition);
            if (!ValidateSkillRank(skillId, rank, definition, out var rankError)) return Task.FromResult(SkillFallback(characterId, SkillAddOperation, skillId, rankError));

            profile.Skills.Add(new CharacterSkillProfileValue
            {
                SkillId = skillId,
                Rank = rank,
                ManualBonus = Math.Max(-999, Math.Min(999, PayloadReader.GetInt(payload, "manualBonus") ?? 0)),
                TrainingState = FirstNonEmpty(PayloadReader.GetString(payload, "trainingState"), "trained"),
                IsPlayerVisible = !payload.ContainsKey("isPlayerVisible") || PayloadReader.GetBool(payload, "isPlayerVisible"),
                IsUnlocked = true,
                IsLearned = true,
                Source = "profile_native",
                LearnedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow,
                Notes = PayloadReader.GetString(payload, "notes") ?? string.Empty
            });

            return Task.FromResult(WriteSkillProfileAndSync(characterId, SkillAddOperation, skillId, profile, loaded, actorUserId, requestId));
        }
        catch (Exception ex)
        {
            return Task.FromResult(SkillFallback(characterId, SkillAddOperation, skillId, ex.Message));
        }
    }

    public Task<ProfileNativeSkillWriteResult> UpdateSkillProfileNativeAsync(string characterId, Dictionary<string, object> payload, string actorUserId, string requestId)
    {
        var skillId = ResolveSkillId(payload);
        _logger.Debug($"profile.native.write.start section={SkillsSection} operation={SkillUpdateOperation} characterId={characterId} skillId={skillId}");
        try
        {
            if (string.IsNullOrWhiteSpace(skillId)) return Task.FromResult(SkillFallback(characterId, SkillUpdateOperation, skillId, "missing_skill_id"));
            var character = LoadCharacter(characterId);
            if (character == null) return Task.FromResult(SkillFallback(characterId, SkillUpdateOperation, skillId, "legacy_character_missing"));

            var loaded = LoadSkillProfileOrShadow(character);
            if (!loaded.IsValid)
            {
                LogProfileInvalid(SkillsSection, characterId, loaded.ErrorMessage);
                return Task.FromResult(SkillFallback(characterId, SkillUpdateOperation, skillId, "invalid_profile"));
            }

            var profile = loaded.Profile!;
            var row = FindSkill(profile, skillId);
            if (row == null) return Task.FromResult(SkillFallback(characterId, SkillUpdateOperation, skillId, "skill_not_found"));
            var level = PayloadReader.GetInt(payload, "level");
            if (!level.HasValue) return Task.FromResult(SkillFallback(characterId, SkillUpdateOperation, skillId, "missing_level"));
            var definition = LoadSkillDefinition(skillId);
            if (definition == null) return Task.FromResult(SkillFallback(characterId, SkillUpdateOperation, skillId, "skill_definition_missing"));
            var rank = ResolveRequestedSkillRank(level.Value, definition);
            if (!ValidateSkillRank(skillId, rank, definition, out var rankError)) return Task.FromResult(SkillFallback(characterId, SkillUpdateOperation, skillId, rankError));

            row.Rank = rank;
            row.ManualBonus = Math.Max(-999, Math.Min(999, PayloadReader.GetInt(payload, "manualBonus") ?? row.ManualBonus));
            row.TrainingState = FirstNonEmpty(PayloadReader.GetString(payload, "trainingState"), row.TrainingState, "trained");
            if (payload.ContainsKey("isPlayerVisible")) row.IsPlayerVisible = PayloadReader.GetBool(payload, "isPlayerVisible");
            row.IsUnlocked = true;
            row.IsLearned = true;
            row.Source = "profile_native";
            if (row.LearnedAtUtc == default) row.LearnedAtUtc = DateTime.UtcNow;
            row.UpdatedAtUtc = DateTime.UtcNow;
            if (payload.ContainsKey("notes")) row.Notes = PayloadReader.GetString(payload, "notes") ?? string.Empty;

            return Task.FromResult(WriteSkillProfileAndSync(characterId, SkillUpdateOperation, skillId, profile, loaded, actorUserId, requestId));
        }
        catch (Exception ex)
        {
            return Task.FromResult(SkillFallback(characterId, SkillUpdateOperation, skillId, ex.Message));
        }
    }

    public Task<ProfileNativeSkillWriteResult> RemoveSkillProfileNativeAsync(string characterId, Dictionary<string, object> payload, string actorUserId, string requestId)
    {
        var skillId = ResolveSkillId(payload);
        _logger.Debug($"profile.native.write.start section={SkillsSection} operation={SkillRemoveOperation} characterId={characterId} skillId={skillId}");
        try
        {
            if (string.IsNullOrWhiteSpace(skillId)) return Task.FromResult(SkillFallback(characterId, SkillRemoveOperation, skillId, "missing_skill_id"));
            var character = LoadCharacter(characterId);
            if (character == null) return Task.FromResult(SkillFallback(characterId, SkillRemoveOperation, skillId, "legacy_character_missing"));

            var loaded = LoadSkillProfileOrShadow(character);
            if (!loaded.IsValid)
            {
                LogProfileInvalid(SkillsSection, characterId, loaded.ErrorMessage);
                return Task.FromResult(SkillFallback(characterId, SkillRemoveOperation, skillId, "invalid_profile"));
            }

            var profile = loaded.Profile!;
            var removed = profile.Skills.RemoveAll(x => string.Equals((x.SkillId ?? string.Empty).Trim(), skillId, StringComparison.OrdinalIgnoreCase));
            if (removed == 0) return Task.FromResult(SkillFallback(characterId, SkillRemoveOperation, skillId, "skill_not_found"));

            return Task.FromResult(WriteSkillProfileAndSync(characterId, SkillRemoveOperation, skillId, profile, loaded, actorUserId, requestId));
        }
        catch (Exception ex)
        {
            return Task.FromResult(SkillFallback(characterId, SkillRemoveOperation, skillId, ex.Message));
        }
    }

    public Task<ProfileNativeDevelopmentWriteResult> AssignClassProfileNativeAsync(string characterId, Dictionary<string, object> payload, string actorUserId, string requestId)
    {
        var nodeId = ResolveDevelopmentNodeId(payload);
        _logger.Debug($"profile.native.write.start section={DevelopmentSection} operation={ClassAssignOperation} characterId={characterId} nodeId={nodeId}");
        try
        {
            if (string.IsNullOrWhiteSpace(nodeId)) return Task.FromResult(DevelopmentFallback(characterId, ClassAssignOperation, nodeId, "missing_development_node_id"));
            var character = LoadCharacter(characterId);
            if (character == null) return Task.FromResult(DevelopmentFallback(characterId, ClassAssignOperation, nodeId, "legacy_character_missing"));

            var loaded = LoadDevelopmentProfileOrShadow(character);
            if (!loaded.IsValid)
            {
                LogProfileInvalid(DevelopmentSection, characterId, loaded.ErrorMessage);
                return Task.FromResult(DevelopmentFallback(characterId, ClassAssignOperation, nodeId, "invalid_profile"));
            }

            var level = PayloadReader.GetInt(payload, "level") ?? 1;
            if (level < 1) return Task.FromResult(DevelopmentFallback(characterId, ClassAssignOperation, nodeId, "invalid_level"));

            var profile = loaded.Profile!;
            var node = FindDevelopmentNode(profile, nodeId);
            if (node == null)
            {
                node = new CharacterDevelopmentNodeState
                {
                    CharacterId = characterId,
                    HexagonId = (PayloadReader.GetString(payload, "hexagonId") ?? "main_development_hexagon").Trim(),
                    DevelopmentNodeId = nodeId,
                    ClassId = (PayloadReader.GetString(payload, "classId") ?? PayloadReader.GetString(payload, "classCode") ?? nodeId).Trim(),
                    NodeType = DevelopmentNodeTypes.Class,
                    Source = "profile_native",
                    PurchasedAtUtc = DateTime.UtcNow
                };
                profile.Nodes.Add(node);
            }

            node.CharacterId = characterId;
            node.HexagonId = string.IsNullOrWhiteSpace(node.HexagonId) ? (PayloadReader.GetString(payload, "hexagonId") ?? "main_development_hexagon").Trim() : node.HexagonId;
            node.DevelopmentNodeId = nodeId;
            node.ClassId = string.IsNullOrWhiteSpace(node.ClassId) ? (PayloadReader.GetString(payload, "classId") ?? PayloadReader.GetString(payload, "classCode") ?? nodeId).Trim() : node.ClassId;
            node.NodeType = string.IsNullOrWhiteSpace(node.NodeType) ? DevelopmentNodeTypes.Class : node.NodeType;
            node.CurrentTier = level;
            node.MaxTier = Math.Max(node.MaxTier, level);
            node.IsUnlocked = true;
            node.IsPurchased = true;
            node.Source = string.IsNullOrWhiteSpace(node.Source) ? "profile_native" : node.Source;
            if (node.PurchasedAtUtc == default) node.PurchasedAtUtc = DateTime.UtcNow;

            var writeReason = ValidateDevelopmentProfile(profile, characterId);
            if (!string.IsNullOrEmpty(writeReason))
            {
                LogProfileInvalid(DevelopmentSection, characterId, writeReason);
                return Task.FromResult(DevelopmentFallback(characterId, ClassAssignOperation, nodeId, writeReason));
            }

            return Task.FromResult(WriteDevelopmentProfileAndSync(characterId, ClassAssignOperation, nodeId, profile, loaded, actorUserId, requestId));
        }
        catch (Exception ex)
        {
            return Task.FromResult(DevelopmentFallback(characterId, ClassAssignOperation, nodeId, ex.Message));
        }
    }

    public Task<ProfileNativeInventoryWriteResult> UpdateInventoryProfileNativeAsync(string characterId, Dictionary<string, object> payload, string actorUserId, string requestId)
    {
        _logger.Debug($"profile.native.write.start section={InventorySection} operation={InventoryUpdateOperation} characterId={characterId}");
        try
        {
            var character = LoadCharacter(characterId);
            if (character == null) return Task.FromResult(InventoryFallback(characterId, InventoryUpdateOperation, string.Empty, "legacy_character_missing"));
            var loaded = LoadInventoryProfileOrShadow(character);
            if (!loaded.IsValid)
            {
                LogProfileInvalid(InventorySection, characterId, loaded.ErrorMessage);
                return Task.FromResult(InventoryFallback(characterId, InventoryUpdateOperation, string.Empty, "invalid_profile"));
            }

            var profile = loaded.Profile!;
            var items = PayloadReader.GetList(payload, "inventory") ?? new List<object>();
            profile.Items = items
                .OfType<Dictionary<string, object>>()
                .Select(ParseInventoryProfileItem)
                .GroupBy(x => (x.ItemId ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(x => x.Last())
                .ToList();

            var reason = ValidateInventoryProfile(profile, characterId);
            if (!string.IsNullOrEmpty(reason))
            {
                LogProfileInvalid(InventorySection, characterId, reason);
                return Task.FromResult(InventoryFallback(characterId, InventoryUpdateOperation, string.Empty, reason));
            }

            return Task.FromResult(WriteInventoryProfileAndSync(characterId, InventoryUpdateOperation, string.Empty, profile, loaded, actorUserId, requestId));
        }
        catch (Exception ex)
        {
            return Task.FromResult(InventoryFallback(characterId, InventoryUpdateOperation, string.Empty, ex.Message));
        }
    }

    public Task<ProfileNativeInventoryWriteResult> AddInventoryItemProfileNativeAsync(string characterId, Dictionary<string, object> payload, string actorUserId, string requestId)
    {
        var source = PayloadReader.GetDictionary(payload, "item") ?? payload;
        var itemId = ResolveInventoryItemId(source, allowGenerated: true);
        _logger.Debug($"profile.native.write.start section={InventorySection} operation={InventoryAddOperation} characterId={characterId} itemId={itemId}");
        try
        {
            var character = LoadCharacter(characterId);
            if (character == null) return Task.FromResult(InventoryFallback(characterId, InventoryAddOperation, itemId, "legacy_character_missing"));
            var loaded = LoadInventoryProfileOrShadow(character);
            if (!loaded.IsValid)
            {
                LogProfileInvalid(InventorySection, characterId, loaded.ErrorMessage);
                return Task.FromResult(InventoryFallback(characterId, InventoryAddOperation, itemId, "invalid_profile"));
            }

            var profile = loaded.Profile!;
            var item = ParseInventoryProfileItem(source);
            item.ItemId = itemId;
            var existing = FindInventoryItem(profile, item.ItemId);
            if (existing == null)
            {
                profile.Items.Add(item);
            }
            else
            {
                CopyInventoryItem(item, existing);
            }

            return Task.FromResult(WriteInventoryProfileAndSync(characterId, InventoryAddOperation, item.ItemId, profile, loaded, actorUserId, requestId));
        }
        catch (Exception ex)
        {
            return Task.FromResult(InventoryFallback(characterId, InventoryAddOperation, itemId, ex.Message));
        }
    }

    public Task<ProfileNativeInventoryWriteResult> UpdateInventoryItemProfileNativeAsync(string characterId, Dictionary<string, object> payload, string actorUserId, string requestId)
    {
        var source = PayloadReader.GetDictionary(payload, "item") ?? payload;
        var itemId = ResolveInventoryItemId(payload, allowGenerated: false);
        if (string.IsNullOrWhiteSpace(itemId)) itemId = ResolveInventoryItemId(source, allowGenerated: false);
        _logger.Debug($"profile.native.write.start section={InventorySection} operation={InventoryUpdateOperation} characterId={characterId} itemId={itemId}");
        try
        {
            if (string.IsNullOrWhiteSpace(itemId)) return Task.FromResult(InventoryFallback(characterId, InventoryUpdateOperation, itemId, "missing_item_id"));
            var character = LoadCharacter(characterId);
            if (character == null) return Task.FromResult(InventoryFallback(characterId, InventoryUpdateOperation, itemId, "legacy_character_missing"));
            var loaded = LoadInventoryProfileOrShadow(character);
            if (!loaded.IsValid)
            {
                LogProfileInvalid(InventorySection, characterId, loaded.ErrorMessage);
                return Task.FromResult(InventoryFallback(characterId, InventoryUpdateOperation, itemId, "invalid_profile"));
            }

            var profile = loaded.Profile!;
            var existing = FindInventoryItem(profile, itemId);
            if (existing == null) return Task.FromResult(InventoryFallback(characterId, InventoryUpdateOperation, itemId, "item_not_found"));
            var incoming = ParseInventoryProfileItem(source);
            incoming.ItemId = existing.ItemId;
            CopyInventoryItem(incoming, existing);
            return Task.FromResult(WriteInventoryProfileAndSync(characterId, InventoryUpdateOperation, itemId, profile, loaded, actorUserId, requestId));
        }
        catch (Exception ex)
        {
            return Task.FromResult(InventoryFallback(characterId, InventoryUpdateOperation, itemId, ex.Message));
        }
    }

    public Task<ProfileNativeInventoryWriteResult> RemoveInventoryItemProfileNativeAsync(string characterId, Dictionary<string, object> payload, string actorUserId, string requestId)
    {
        var itemId = ResolveInventoryItemId(payload, allowGenerated: false);
        _logger.Debug($"profile.native.write.start section={InventorySection} operation={InventoryRemoveOperation} characterId={characterId} itemId={itemId}");
        try
        {
            if (string.IsNullOrWhiteSpace(itemId)) return Task.FromResult(InventoryFallback(characterId, InventoryRemoveOperation, itemId, "missing_item_id"));
            var character = LoadCharacter(characterId);
            if (character == null) return Task.FromResult(InventoryFallback(characterId, InventoryRemoveOperation, itemId, "legacy_character_missing"));
            var loaded = LoadInventoryProfileOrShadow(character);
            if (!loaded.IsValid)
            {
                LogProfileInvalid(InventorySection, characterId, loaded.ErrorMessage);
                return Task.FromResult(InventoryFallback(characterId, InventoryRemoveOperation, itemId, "invalid_profile"));
            }

            var profile = loaded.Profile!;
            profile.Items.RemoveAll(x => string.Equals((x.ItemId ?? string.Empty).Trim(), itemId, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(WriteInventoryProfileAndSync(characterId, InventoryRemoveOperation, itemId, profile, loaded, actorUserId, requestId));
        }
        catch (Exception ex)
        {
            return Task.FromResult(InventoryFallback(characterId, InventoryRemoveOperation, itemId, ex.Message));
        }
    }

    public Task<ProfileNativeInventoryWriteResult> ToggleEquipInventoryItemProfileNativeAsync(string characterId, Dictionary<string, object> payload, string actorUserId, string requestId)
    {
        var itemId = ResolveInventoryItemId(payload, allowGenerated: false);
        _logger.Debug($"profile.native.write.start section={InventorySection} operation={InventoryToggleEquipOperation} characterId={characterId} itemId={itemId}");
        try
        {
            if (string.IsNullOrWhiteSpace(itemId)) return Task.FromResult(InventoryFallback(characterId, InventoryToggleEquipOperation, itemId, "missing_item_id"));
            var character = LoadCharacter(characterId);
            if (character == null) return Task.FromResult(InventoryFallback(characterId, InventoryToggleEquipOperation, itemId, "legacy_character_missing"));
            var loaded = LoadInventoryProfileOrShadow(character);
            if (!loaded.IsValid)
            {
                LogProfileInvalid(InventorySection, characterId, loaded.ErrorMessage);
                return Task.FromResult(InventoryFallback(characterId, InventoryToggleEquipOperation, itemId, "invalid_profile"));
            }

            var profile = loaded.Profile!;
            var item = FindInventoryItem(profile, itemId);
            if (item == null) return Task.FromResult(InventoryFallback(characterId, InventoryToggleEquipOperation, itemId, "item_not_found"));
            item.IsEquipped = !item.IsEquipped;
            item.Tags ??= new List<string>();
            if (item.IsEquipped && !item.Tags.Any(x => string.Equals(x, "equipped", StringComparison.OrdinalIgnoreCase))) item.Tags.Add("equipped");
            if (!item.IsEquipped) item.Tags.RemoveAll(x => string.Equals(x, "equipped", StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(WriteInventoryProfileAndSync(characterId, InventoryToggleEquipOperation, itemId, profile, loaded, actorUserId, requestId));
        }
        catch (Exception ex)
        {
            return Task.FromResult(InventoryFallback(characterId, InventoryToggleEquipOperation, itemId, ex.Message));
        }
    }

    public Task<ProfileNativeRaceBodyWriteResult> UpdateRaceOrSpeciesProfileNativeAsync(string characterId, Dictionary<string, object> payload, string actorUserId, string requestId)
    {
        return UpdateRaceBodyProfilesNativeAsync(characterId, payload, actorUserId, requestId, writeRace: true, writeBody: false);
    }

    public Task<ProfileNativeRaceBodyWriteResult> UpdateBodyProfileNativeAsync(string characterId, Dictionary<string, object> payload, string actorUserId, string requestId)
    {
        return UpdateRaceBodyProfilesNativeAsync(characterId, payload, actorUserId, requestId, writeRace: false, writeBody: true);
    }

    public Task<ProfileNativeRaceBodyWriteResult> UpdateRaceBodyProfilesNativeAsync(string characterId, Dictionary<string, object> payload, string actorUserId, string requestId)
    {
        return UpdateRaceBodyProfilesNativeAsync(characterId, payload, actorUserId, requestId, writeRace: true, writeBody: true);
    }

    public Task<ProfileNativeRaceBodyWriteResult> UpdateBiographyProfileNativeAsync(string characterId, Dictionary<string, object> payload, string actorUserId, string requestId)
    {
        _logger.Debug($"profile.native.write.start section={BiographySection} characterId={characterId}");
        var result = UpdateRaceBodyProfilesNativeAsync(characterId, payload, actorUserId, requestId, writeRace: false, writeBody: true).GetAwaiter().GetResult();
        if (result.BodyProfileWritten && result.LegacyFacadeSynced && !result.UsedFallback)
        {
            _logger.Debug($"profile.native.write.done section={BiographySection} characterId={characterId}");
        }
        else
        {
            _logger.Debug($"profile.native.write.fallback section={BiographySection} characterId={characterId} reason={result.ErrorMessage}");
        }

        return Task.FromResult(result);
    }

    public Task<ProfileNativeKnowledgeWriteResult> UnlockKnowledgeTopicProfileNativeAsync(string characterId, string topic, string actorUserId, string requestId)
    {
        topic = (topic ?? string.Empty).Trim();
        _logger.Debug($"profile.native.write.start section={KnowledgeSection} characterId={characterId} requestId={requestId}");
        if (string.IsNullOrWhiteSpace(characterId) || string.IsNullOrWhiteSpace(topic))
        {
            return Task.FromResult(new ProfileNativeKnowledgeWriteResult
            {
                CharacterId = characterId,
                Topic = topic,
                UsedProfileNative = true,
                ErrorMessage = "missing_character_or_topic"
            });
        }

        try
        {
            var characterFilter = Builders<CharacterKnowledgeProfileDocument>.Filter.Eq(x => x.CharacterId, characterId);
            if (!_mongo.CharacterKnowledgeProfiles.Find(characterFilter).Any())
            {
                try
                {
                    _mongo.CharacterKnowledgeProfiles.InsertOne(new CharacterKnowledgeProfileDocument
                    {
                        CharacterId = characterId,
                        Profile = new KnowledgeProfile()
                    });
                }
                catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
                {
                    // Another request initialized the same Character v2 profile first.
                }
            }

            var notKnownFilter = characterFilter
                & Builders<CharacterKnowledgeProfileDocument>.Filter.Ne("Profile.KnownTopics", topic);
            var now = DateTime.UtcNow;
            var update = Builders<CharacterKnowledgeProfileDocument>.Update
                .AddToSet("Profile.KnownTopics", topic)
                .Set(x => x.UpdatedUtc, now);
            var write = _mongo.CharacterKnowledgeProfiles.UpdateOne(notKnownFilter, update);
            if (!write.IsAcknowledged)
            {
                throw new InvalidOperationException("knowledge_profile_update_not_acknowledged");
            }

            var added = write.ModifiedCount == 1;
            var exists = _mongo.CharacterKnowledgeProfiles.Find(
                    characterFilter
                    & Builders<CharacterKnowledgeProfileDocument>.Filter.Eq("Profile.KnownTopics", topic))
                .Any();
            if (!exists)
            {
                throw new InvalidOperationException("knowledge_topic_not_persisted");
            }

            _logger.Debug($"profile.native.write.done section={KnowledgeSection} characterId={characterId} topicAdded={added} actorUserId={actorUserId}");
            return Task.FromResult(new ProfileNativeKnowledgeWriteResult
            {
                CharacterId = characterId,
                Topic = topic,
                UsedProfileNative = true,
                ProfileWritten = true,
                TopicAdded = added,
                AlreadyKnown = !added,
                WrittenAtUtc = now
            });
        }
        catch (Exception ex)
        {
            _logger.Debug($"profile.native.write.error section={KnowledgeSection} characterId={characterId} message={ex.Message}");
            return Task.FromResult(new ProfileNativeKnowledgeWriteResult
            {
                CharacterId = characterId,
                Topic = topic,
                UsedProfileNative = true,
                ErrorMessage = ex.Message
            });
        }
    }

    private Task<ProfileNativeRaceBodyWriteResult> UpdateRaceBodyProfilesNativeAsync(string characterId, Dictionary<string, object> payload, string actorUserId, string requestId, bool writeRace, bool writeBody)
    {
        _logger.Debug($"profile.native.write.start section={RaceBodySection} characterId={characterId}");
        try
        {
            var character = LoadCharacter(characterId);
            if (character == null) return Task.FromResult(RaceBodyFallback(characterId, "legacy_character_missing"));

            var raceLoaded = LoadRaceOrSpeciesProfileOrShadow(character);
            var bodyLoaded = LoadBodyProfileOrShadow(character);
            if (!raceLoaded.IsValid)
            {
                LogProfileInvalid(RaceSection, characterId, raceLoaded.ErrorMessage);
                return Task.FromResult(RaceBodyFallback(characterId, "invalid_race_profile"));
            }

            if (!bodyLoaded.IsValid)
            {
                LogProfileInvalid(BodySection, characterId, bodyLoaded.ErrorMessage);
                return Task.FromResult(RaceBodyFallback(characterId, "invalid_body_profile"));
            }

            var raceProfile = raceLoaded.Profile!;
            var bodyProfile = bodyLoaded.Profile!;
            if (writeRace) ApplyRacePayload(raceProfile, payload);
            if (writeBody) ApplyBodyPayload(bodyProfile, payload);

            var raceReason = ValidateRaceOrSpeciesProfile(raceProfile, characterId);
            if (!string.IsNullOrEmpty(raceReason))
            {
                LogProfileInvalid(RaceSection, characterId, raceReason);
                return Task.FromResult(RaceBodyFallback(characterId, raceReason));
            }

            var bodyReason = ValidateBodyProfile(bodyProfile, characterId);
            if (!string.IsNullOrEmpty(bodyReason))
            {
                LogProfileInvalid(BodySection, characterId, bodyReason);
                return Task.FromResult(RaceBodyFallback(characterId, bodyReason));
            }

            return Task.FromResult(WriteRaceBodyProfilesAndSync(characterId, raceProfile, bodyProfile, raceLoaded, bodyLoaded, writeRace, writeBody, actorUserId, requestId));
        }
        catch (Exception ex)
        {
            return Task.FromResult(RaceBodyFallback(characterId, ex.Message));
        }
    }

    public Task<ProfileNativeWriteResult> SyncLegacyStatsFacadeAsync(string characterId, AttributeProfile profile, string actorUserId, string requestId)
    {
        _logger.Debug($"profile.native.write.facade_sync.start section={StatsSection} characterId={characterId}");
        try
        {
            var character = LoadCharacter(characterId);
            if (character == null) throw new KeyNotFoundException("legacy_character_missing");
            character.Stats ??= new CharacterStats();
            var map = AttributeMap(profile);
            character.Stats.Health = GetFirstAttribute(map, CharacterVitalStatIds.HealthCurrent, CharacterAttributeIds.Health);
            character.Stats.PhysicalArmor = GetFirstAttribute(map, CharacterVitalStatIds.PhysicalDefense, CharacterAttributeIds.PhysicalArmor);
            character.Stats.MagicalArmor = GetFirstAttribute(map, CharacterVitalStatIds.MagicalDefense, CharacterAttributeIds.MagicArmor);
            character.Stats.Morale = GetFirstAttribute(map, CharacterVitalStatIds.Morale, CharacterAttributeIds.Morale);
            character.Stats.Strength = GetAttribute(map, CharacterAttributeIds.Strength);
            character.Stats.Dexterity = GetAttribute(map, CharacterAttributeIds.Dexterity);
            character.Stats.Endurance = GetAttribute(map, CharacterAttributeIds.Endurance);
            character.Stats.Wisdom = GetAttribute(map, CharacterAttributeIds.Wisdom);
            character.Stats.Intellect = GetAttribute(map, CharacterAttributeIds.Intellect);
            character.Stats.Charisma = GetAttribute(map, CharacterAttributeIds.Charisma);
            character.UpdatedUtc = DateTime.UtcNow;
            EnsureReplaceSucceeded(_mongo.Characters.ReplaceOne(Builders<Character>.Filter.Eq(x => x.Id, characterId), character), "legacy_stats_facade_replace_failed");
            _logger.Debug($"profile.native.write.facade_sync.done section={StatsSection} characterId={characterId}");
            return Task.FromResult(new ProfileNativeWriteResult { CharacterId = characterId, Section = StatsSection, UsedProfileNative = true, LegacyFacadeSynced = true, WrittenAtUtc = DateTime.UtcNow });
        }
        catch (Exception ex)
        {
            _logger.Debug($"profile.native.write.facade_sync_error section={StatsSection} characterId={characterId} message={ex.Message}");
            return Task.FromResult(new ProfileNativeWriteResult { CharacterId = characterId, Section = StatsSection, UsedProfileNative = true, LegacyFacadeSynced = false, ErrorMessage = ex.Message, WrittenAtUtc = DateTime.UtcNow });
        }
    }

    public Task<ProfileNativeWriteResult> SyncLegacyWalletFacadeAsync(string characterId, WalletProfile profile, string actorUserId, string requestId)
    {
        _logger.Debug($"profile.native.write.facade_sync.start section={WalletSection} characterId={characterId}");
        try
        {
            var character = LoadCharacter(characterId);
            if (character == null) throw new KeyNotFoundException("legacy_character_missing");
            character.Wallet ??= new Wallet();
            character.Wallet.EnsureAllDenominations();
            var map = WalletMap(profile);
            foreach (var pair in DenominationToCurrencyIds)
            {
                character.Wallet.Balance.Amounts[pair.Key.ToString()] = GetWalletAmount(map, pair.Value);
            }
            if (map.TryGetValue(CharacterCurrencyIds.XpCoin, out var xpCoin))
            {
                character.XpCoins = checked((int)xpCoin.Amount);
            }
            character.Wallet.NormalizeUpward();
            character.UpdatedUtc = DateTime.UtcNow;
            EnsureReplaceSucceeded(_mongo.Characters.ReplaceOne(Builders<Character>.Filter.Eq(x => x.Id, characterId), character), "legacy_wallet_facade_replace_failed");
            _logger.Debug($"profile.native.write.facade_sync.done section={WalletSection} characterId={characterId}");
            return Task.FromResult(new ProfileNativeWriteResult { CharacterId = characterId, Section = WalletSection, UsedProfileNative = true, LegacyFacadeSynced = true, WrittenAtUtc = DateTime.UtcNow });
        }
        catch (Exception ex)
        {
            _logger.Debug($"profile.native.write.facade_sync_error section={WalletSection} characterId={characterId} message={ex.Message}");
            return Task.FromResult(new ProfileNativeWriteResult { CharacterId = characterId, Section = WalletSection, UsedProfileNative = true, LegacyFacadeSynced = false, ErrorMessage = ex.Message, WrittenAtUtc = DateTime.UtcNow });
        }
    }

    public Task<ProfileNativeSkillWriteResult> SyncLegacySkillFacadeAsync(string characterId, SkillProfile profile, string actorUserId, string requestId)
    {
        _logger.Debug($"profile.native.write.facade_sync.start section={SkillsSection} characterId={characterId}");
        try
        {
            var character = LoadCharacter(characterId);
            if (character == null) throw new KeyNotFoundException("legacy_character_missing");
            character.CharacterSkills = BuildLegacySkillFacade(profile);
            character.UpdatedUtc = DateTime.UtcNow;
            EnsureReplaceSucceeded(_mongo.Characters.ReplaceOne(Builders<Character>.Filter.Eq(x => x.Id, characterId), character), "legacy_skills_facade_replace_failed");
            _logger.Debug($"profile.native.write.facade_sync.done section={SkillsSection} characterId={characterId}");
            return Task.FromResult(new ProfileNativeSkillWriteResult { CharacterId = characterId, Operation = "sync", UsedProfileNative = true, LegacyFacadeSynced = true, WrittenAtUtc = DateTime.UtcNow });
        }
        catch (Exception ex)
        {
            _logger.Debug($"profile.native.write.facade_sync_error section={SkillsSection} characterId={characterId} message={ex.Message}");
            return Task.FromResult(new ProfileNativeSkillWriteResult { CharacterId = characterId, Operation = "sync", UsedProfileNative = true, LegacyFacadeSynced = false, ErrorMessage = ex.Message, WrittenAtUtc = DateTime.UtcNow });
        }
    }

    public Task<ProfileNativeDevelopmentWriteResult> SyncLegacyDevelopmentFacadeAsync(string characterId, DevelopmentProfile profile, string actorUserId, string requestId)
    {
        _logger.Debug($"profile.native.write.facade_sync.start section={DevelopmentSection} characterId={characterId}");
        try
        {
            var character = LoadCharacter(characterId);
            if (character == null) throw new KeyNotFoundException("legacy_character_missing");
            var classNodes = ClassDevelopmentNodes(profile).ToList();
            character.CharacterClasses = BuildLegacyCharacterClasses(classNodes);
            character.ClassProgress = BuildLegacyClassProgress(classNodes);
            character.UpdatedUtc = DateTime.UtcNow;
            EnsureReplaceSucceeded(_mongo.Characters.ReplaceOne(Builders<Character>.Filter.Eq(x => x.Id, characterId), character), "legacy_development_facade_replace_failed");
            _logger.Debug($"profile.native.write.facade_sync.done section={DevelopmentSection} characterId={characterId}");
            return Task.FromResult(new ProfileNativeDevelopmentWriteResult { CharacterId = characterId, Operation = "sync", UsedProfileNative = true, LegacyFacadeSynced = true, WrittenAtUtc = DateTime.UtcNow });
        }
        catch (Exception ex)
        {
            _logger.Debug($"profile.native.write.facade_sync_error section={DevelopmentSection} characterId={characterId} message={ex.Message}");
            return Task.FromResult(new ProfileNativeDevelopmentWriteResult { CharacterId = characterId, Operation = "sync", UsedProfileNative = true, LegacyFacadeSynced = false, ErrorMessage = ex.Message, WrittenAtUtc = DateTime.UtcNow });
        }
    }

    public Task<ProfileNativeInventoryWriteResult> SyncLegacyInventoryFacadeAsync(string characterId, InventoryProfile profile, string actorUserId, string requestId)
    {
        _logger.Debug($"profile.native.write.facade_sync.start section={InventorySection} characterId={characterId}");
        try
        {
            var character = LoadCharacter(characterId);
            if (character == null) throw new KeyNotFoundException("legacy_character_missing");
            character.Inventory = BuildLegacyInventoryFacade(profile, character.Inventory ?? new List<InventoryItem>());
            character.UpdatedUtc = DateTime.UtcNow;
            EnsureReplaceSucceeded(_mongo.Characters.ReplaceOne(Builders<Character>.Filter.Eq(x => x.Id, characterId), character), "legacy_inventory_facade_replace_failed");
            _logger.Debug($"profile.native.write.facade_sync.done section={InventorySection} characterId={characterId}");
            return Task.FromResult(new ProfileNativeInventoryWriteResult { CharacterId = characterId, Operation = "sync", UsedProfileNative = true, LegacyFacadeSynced = true, WrittenAtUtc = DateTime.UtcNow });
        }
        catch (Exception ex)
        {
            _logger.Debug($"profile.native.write.facade_sync_error section={InventorySection} characterId={characterId} message={ex.Message}");
            return Task.FromResult(new ProfileNativeInventoryWriteResult { CharacterId = characterId, Operation = "sync", UsedProfileNative = true, LegacyFacadeSynced = false, ErrorMessage = ex.Message, WrittenAtUtc = DateTime.UtcNow });
        }
    }

    public Task<ProfileNativeRaceBodyWriteResult> SyncLegacyRaceBodyFacadeAsync(string characterId, RaceOrSpeciesProfile raceProfile, BodyProfile bodyProfile, string actorUserId, string requestId)
    {
        return SyncLegacyRaceBodyFacadeAsync(characterId, raceProfile, bodyProfile, actorUserId, requestId, writeRace: true, writeBody: true);
    }

    private Task<ProfileNativeRaceBodyWriteResult> SyncLegacyRaceBodyFacadeAsync(string characterId, RaceOrSpeciesProfile raceProfile, BodyProfile bodyProfile, string actorUserId, string requestId, bool writeRace, bool writeBody)
    {
        _logger.Debug($"profile.native.write.facade_sync.start section={RaceBodySection} characterId={characterId}");
        try
        {
            var character = LoadCharacter(characterId);
            if (character == null) throw new KeyNotFoundException("legacy_character_missing");

            if (writeRace)
            {
                var raceCode = (raceProfile?.RaceCode ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(raceCode)) character.RaceCode = raceCode;
                var raceName = FirstNonEmpty(raceProfile?.RaceName ?? string.Empty, raceProfile?.DisplayName ?? string.Empty);
                if (!string.IsNullOrWhiteSpace(raceName)) character.Race = raceName;
            }

            if (writeBody && bodyProfile != null)
            {
                character.Description = bodyProfile.Description ?? string.Empty;
                character.Backstory = bodyProfile.Backstory ?? string.Empty;

                if (!string.IsNullOrWhiteSpace(bodyProfile.HeightText))
                {
                    character.Height = bodyProfile.HeightText.Trim();
                }
                else if (bodyProfile.HeightCm > 0)
                {
                    character.Height = bodyProfile.HeightCm.ToString(System.Globalization.CultureInfo.InvariantCulture);
                }

                if (bodyProfile.AgeYears > 0)
                {
                    character.Age = bodyProfile.AgeYears;
                }
                else if (int.TryParse((bodyProfile.AgeText ?? string.Empty).Trim(), out var parsedAge) && parsedAge >= 0)
                {
                    character.Age = parsedAge;
                }
            }

            character.UpdatedUtc = DateTime.UtcNow;
            EnsureReplaceSucceeded(_mongo.Characters.ReplaceOne(Builders<Character>.Filter.Eq(x => x.Id, characterId), character), "legacy_race_body_facade_replace_failed");
            _logger.Debug($"profile.native.write.facade_sync.done section={RaceBodySection} characterId={characterId}");
            return Task.FromResult(new ProfileNativeRaceBodyWriteResult { CharacterId = characterId, UsedProfileNative = true, LegacyFacadeSynced = true, WrittenAtUtc = DateTime.UtcNow });
        }
        catch (Exception ex)
        {
            _logger.Debug($"profile.native.write.facade_sync_error section={RaceBodySection} characterId={characterId} message={ex.Message}");
            return Task.FromResult(new ProfileNativeRaceBodyWriteResult { CharacterId = characterId, UsedProfileNative = true, LegacyFacadeSynced = false, ErrorMessage = ex.Message, WrittenAtUtc = DateTime.UtcNow });
        }
    }

    private Character? LoadCharacter(string characterId)
    {
        if (string.IsNullOrWhiteSpace(characterId)) return null;
        return _mongo.Characters.Find(Builders<Character>.Filter.Eq(x => x.Id, characterId)).FirstOrDefault();
    }

    private ProfileLoadResult<AttributeProfile> LoadAttributeProfileOrShadow(Character character)
    {
        var document = _mongo.CharacterAttributeProfiles.Find(Builders<CharacterAttributeProfileDocument>.Filter.Eq(x => x.CharacterId, character.Id)).FirstOrDefault();
        if (document == null)
        {
            _logger.Debug($"profile.native.write.profile_missing section={StatsSection} characterId={character.Id}");
            var shadow = _attributeFactory.BuildFromLegacyCharacter(character);
            _logger.Debug($"profile.native.write.profile_created_from_legacy section={StatsSection} characterId={character.Id}");
            var shadowReason = ValidateAttributeProfile(shadow, character.Id);
            return string.IsNullOrEmpty(shadowReason)
                ? ProfileLoadResult<AttributeProfile>.Valid(shadow, false, true)
                : ProfileLoadResult<AttributeProfile>.Invalid(shadowReason, false, true);
        }

        var reason = ValidateAttributeProfile(document.Profile, character.Id);
        return string.IsNullOrEmpty(reason)
            ? ProfileLoadResult<AttributeProfile>.Valid(document.Profile, true, false)
            : ProfileLoadResult<AttributeProfile>.Invalid(reason, true, false);
    }

    private ProfileLoadResult<WalletProfile> LoadWalletProfileOrShadow(Character character)
    {
        var document = _mongo.CharacterWalletProfiles.Find(Builders<CharacterWalletProfileDocument>.Filter.Eq(x => x.CharacterId, character.Id)).FirstOrDefault();
        if (document == null)
        {
            _logger.Debug($"profile.native.write.profile_missing section={WalletSection} characterId={character.Id}");
            var shadow = _walletFactory.BuildFromLegacyCharacter(character);
            _logger.Debug($"profile.native.write.profile_created_from_legacy section={WalletSection} characterId={character.Id}");
            var shadowReason = ValidateWalletProfile(shadow, character.Id);
            return string.IsNullOrEmpty(shadowReason)
                ? ProfileLoadResult<WalletProfile>.Valid(shadow, false, true)
                : ProfileLoadResult<WalletProfile>.Invalid(shadowReason, false, true);
        }

        var reason = ValidateWalletProfile(document.Profile, character.Id);
        return string.IsNullOrEmpty(reason)
            ? ProfileLoadResult<WalletProfile>.Valid(document.Profile, true, false)
            : ProfileLoadResult<WalletProfile>.Invalid(reason, true, false);
    }

    private ProfileLoadResult<SkillProfile> LoadSkillProfileOrShadow(Character character)
    {
        var document = _mongo.CharacterSkillProfiles.Find(Builders<CharacterSkillProfileDocument>.Filter.Eq(x => x.CharacterId, character.Id)).FirstOrDefault();
        if (document == null)
        {
            _logger.Debug($"profile.native.write.profile_missing section={SkillsSection} characterId={character.Id}");
            var shadow = _skillFactory.BuildFromLegacyCharacter(character);
            _logger.Debug($"profile.native.write.profile_created_from_legacy section={SkillsSection} characterId={character.Id}");
            var shadowReason = ValidateSkillProfile(shadow, character.Id);
            return string.IsNullOrEmpty(shadowReason)
                ? ProfileLoadResult<SkillProfile>.Valid(shadow, false, true)
                : ProfileLoadResult<SkillProfile>.Invalid(shadowReason, false, true);
        }

        var reason = ValidateSkillProfile(document.Profile, character.Id);
        return string.IsNullOrEmpty(reason)
            ? ProfileLoadResult<SkillProfile>.Valid(document.Profile, true, false)
            : ProfileLoadResult<SkillProfile>.Invalid(reason, true, false);
    }

    private ProfileLoadResult<DevelopmentProfile> LoadDevelopmentProfileOrShadow(Character character)
    {
        var document = _mongo.CharacterDevelopmentProfiles.Find(Builders<CharacterDevelopmentProfileDocument>.Filter.Eq(x => x.CharacterId, character.Id)).FirstOrDefault();
        if (document == null)
        {
            _logger.Debug($"profile.native.write.profile_missing section={DevelopmentSection} characterId={character.Id}");
            var shadow = _developmentFactory.BuildFromLegacyCharacter(character);
            _logger.Debug($"profile.native.write.profile_created_from_legacy section={DevelopmentSection} characterId={character.Id}");
            var shadowReason = ValidateDevelopmentProfile(shadow, character.Id);
            return string.IsNullOrEmpty(shadowReason)
                ? ProfileLoadResult<DevelopmentProfile>.Valid(shadow, false, true)
                : ProfileLoadResult<DevelopmentProfile>.Invalid(shadowReason, false, true);
        }

        var reason = ValidateDevelopmentProfile(document.Profile, character.Id);
        return string.IsNullOrEmpty(reason)
            ? ProfileLoadResult<DevelopmentProfile>.Valid(document.Profile, true, false)
            : ProfileLoadResult<DevelopmentProfile>.Invalid(reason, true, false);
    }

    private ProfileLoadResult<InventoryProfile> LoadInventoryProfileOrShadow(Character character)
    {
        var document = _mongo.CharacterInventoryProfiles.Find(Builders<CharacterInventoryProfileDocument>.Filter.Eq(x => x.CharacterId, character.Id)).FirstOrDefault();
        if (document == null)
        {
            _logger.Debug($"profile.native.write.profile_missing section={InventorySection} characterId={character.Id}");
            var empty = _inventoryFactory.BuildEmpty(character.Id, RuleSetIds.FantasyNriDefault);
            _logger.Debug($"profile.native.write.profile_created_empty section={InventorySection} characterId={character.Id}");
            var emptyReason = ValidateInventoryProfile(empty, character.Id);
            return string.IsNullOrEmpty(emptyReason)
                ? ProfileLoadResult<InventoryProfile>.Valid(empty, false, false)
                : ProfileLoadResult<InventoryProfile>.Invalid(emptyReason, false, false);
        }

        var reason = ValidateInventoryProfile(document.Profile, character.Id);
        return string.IsNullOrEmpty(reason)
            ? ProfileLoadResult<InventoryProfile>.Valid(document.Profile, true, false)
            : ProfileLoadResult<InventoryProfile>.Invalid(reason, true, false);
    }

    private ProfileLoadResult<RaceOrSpeciesProfile> LoadRaceOrSpeciesProfileOrShadow(Character character)
    {
        var document = _mongo.CharacterRaceOrSpeciesProfiles.Find(Builders<CharacterRaceOrSpeciesProfileDocument>.Filter.Eq(x => x.CharacterId, character.Id)).FirstOrDefault();
        if (document == null)
        {
            _logger.Debug($"profile.native.write.profile_missing section={RaceSection} characterId={character.Id}");
            var shadow = _raceOrSpeciesBuilder.BuildFromLegacyCharacter(character);
            _logger.Debug($"profile.native.write.profile_created_from_legacy section={RaceSection} characterId={character.Id}");
            var shadowReason = ValidateRaceOrSpeciesProfile(shadow, character.Id);
            return string.IsNullOrEmpty(shadowReason)
                ? ProfileLoadResult<RaceOrSpeciesProfile>.Valid(shadow, false, true)
                : ProfileLoadResult<RaceOrSpeciesProfile>.Invalid(shadowReason, false, true);
        }

        var reason = ValidateRaceOrSpeciesProfile(document.Profile, character.Id);
        return string.IsNullOrEmpty(reason)
            ? ProfileLoadResult<RaceOrSpeciesProfile>.Valid(document.Profile, true, false)
            : ProfileLoadResult<RaceOrSpeciesProfile>.Invalid(reason, true, false);
    }

    private ProfileLoadResult<BodyProfile> LoadBodyProfileOrShadow(Character character)
    {
        var document = _mongo.CharacterBodyProfiles.Find(Builders<CharacterBodyProfileDocument>.Filter.Eq(x => x.CharacterId, character.Id)).FirstOrDefault();
        if (document == null)
        {
            _logger.Debug($"profile.native.write.profile_missing section={BodySection} characterId={character.Id}");
            var shadow = _bodyBuilder.BuildFromLegacyCharacter(character);
            _logger.Debug($"profile.native.write.profile_created_from_legacy section={BodySection} characterId={character.Id}");
            var shadowReason = ValidateBodyProfile(shadow, character.Id);
            return string.IsNullOrEmpty(shadowReason)
                ? ProfileLoadResult<BodyProfile>.Valid(shadow, false, true)
                : ProfileLoadResult<BodyProfile>.Invalid(shadowReason, false, true);
        }

        var reason = ValidateBodyProfile(document.Profile, character.Id);
        return string.IsNullOrEmpty(reason)
            ? ProfileLoadResult<BodyProfile>.Valid(document.Profile, true, false)
            : ProfileLoadResult<BodyProfile>.Invalid(reason, true, false);
    }

    private ProfileNativeWriteResult Fallback(string characterId, string section, string reason)
    {
        _logger.Debug($"profile.native.write.fallback section={section} characterId={characterId} reason={reason}");
        return new ProfileNativeWriteResult { CharacterId = characterId ?? string.Empty, Section = section, UsedFallback = true, ErrorMessage = reason ?? string.Empty, WrittenAtUtc = DateTime.UtcNow };
    }

    private ProfileNativeSkillWriteResult SkillFallback(string characterId, string operation, string skillId, string reason)
    {
        _logger.Debug($"profile.native.write.fallback section={SkillsSection} operation={operation} characterId={characterId} skillId={skillId} reason={reason}");
        return new ProfileNativeSkillWriteResult { CharacterId = characterId ?? string.Empty, Operation = operation ?? string.Empty, SkillId = skillId ?? string.Empty, UsedFallback = true, ErrorMessage = reason ?? string.Empty, WrittenAtUtc = DateTime.UtcNow };
    }

    private ProfileNativeDevelopmentWriteResult DevelopmentFallback(string characterId, string operation, string nodeId, string reason)
    {
        _logger.Debug($"profile.native.write.fallback section={DevelopmentSection} operation={operation} characterId={characterId} nodeId={nodeId} reason={reason}");
        return new ProfileNativeDevelopmentWriteResult { CharacterId = characterId ?? string.Empty, Operation = operation ?? string.Empty, DevelopmentNodeId = nodeId ?? string.Empty, UsedFallback = true, ErrorMessage = reason ?? string.Empty, WrittenAtUtc = DateTime.UtcNow };
    }

    private ProfileNativeInventoryWriteResult InventoryFallback(string characterId, string operation, string itemId, string reason)
    {
        _logger.Debug($"profile.native.write.fallback section={InventorySection} operation={operation} characterId={characterId} itemId={itemId} reason={reason}");
        return new ProfileNativeInventoryWriteResult { CharacterId = characterId ?? string.Empty, Operation = operation ?? string.Empty, ItemId = itemId ?? string.Empty, UsedFallback = true, ErrorMessage = reason ?? string.Empty, WrittenAtUtc = DateTime.UtcNow };
    }

    private ProfileNativeRaceBodyWriteResult RaceBodyFallback(string characterId, string reason)
    {
        _logger.Debug($"profile.native.write.fallback section={RaceBodySection} characterId={characterId} reason={reason}");
        return new ProfileNativeRaceBodyWriteResult { CharacterId = characterId ?? string.Empty, UsedFallback = true, ErrorMessage = reason ?? string.Empty, WrittenAtUtc = DateTime.UtcNow };
    }

    private void LogProfileInvalid(string section, string characterId, string reason)
    {
        _logger.Debug($"profile.native.write.profile_invalid section={section} characterId={characterId} reason={reason}");
    }

    private static string ValidateAttributeProfile(AttributeProfile profile, string characterId)
    {
        if (profile == null) return "profile_null";
        if (!string.Equals(profile.CharacterId, characterId, StringComparison.Ordinal)) return "character_id_mismatch";
        if (profile.SchemaVersion < 1) return "schema_version_invalid";
        if (profile.Values == null) return "values_null";
        return string.Empty;
    }

    private static string ValidateWalletProfile(WalletProfile profile, string characterId)
    {
        if (profile == null) return "profile_null";
        if (!string.Equals(profile.CharacterId, characterId, StringComparison.Ordinal)) return "character_id_mismatch";
        if (profile.SchemaVersion < 1) return "schema_version_invalid";
        if (profile.Wallets == null) return "wallets_null";
        return string.Empty;
    }

    private static string ValidateSkillProfile(SkillProfile profile, string characterId)
    {
        if (profile == null) return "profile_null";
        if (!string.Equals(profile.CharacterId, characterId, StringComparison.Ordinal)) return "character_id_mismatch";
        if (profile.SchemaVersion < 1) return "schema_version_invalid";
        if (profile.Skills == null) return "skills_null";
        if (profile.Skills.Any(x => string.IsNullOrWhiteSpace(x.SkillId))) return "skill_id_empty";
        if (profile.Skills.Any(x => x.Rank < 0)) return "rank_negative";
        if (profile.Skills.Select(x => (x.SkillId ?? string.Empty).Trim()).GroupBy(x => x, StringComparer.OrdinalIgnoreCase).Any(x => x.Count() > 1)) return "duplicate_skill_id";
        return string.Empty;
    }

    private static string ValidateDevelopmentProfile(DevelopmentProfile profile, string characterId)
    {
        if (profile == null) return "profile_null";
        if (!string.Equals(profile.CharacterId, characterId, StringComparison.Ordinal)) return "character_id_mismatch";
        if (profile.SchemaVersion < 1) return "schema_version_invalid";
        if (profile.Nodes == null) return "nodes_null";
        if (profile.Nodes.Any(x => !string.IsNullOrWhiteSpace(x.CharacterId) && !string.Equals(x.CharacterId, characterId, StringComparison.Ordinal))) return "node_character_id_mismatch";
        if (profile.Nodes.Any(x => string.IsNullOrWhiteSpace(x.DevelopmentNodeId))) return "development_node_id_empty";
        if (profile.Nodes.Any(x => x.CurrentTier < 0)) return "current_tier_negative";
        if (profile.Nodes.Any(x => x.MaxTier > 0 && x.MaxTier < x.CurrentTier)) return "max_tier_less_than_current";
        if (profile.Nodes.Select(x => (x.DevelopmentNodeId ?? string.Empty).Trim()).GroupBy(x => x, StringComparer.OrdinalIgnoreCase).Any(x => x.Count() > 1)) return "duplicate_development_node_id";
        return string.Empty;
    }

    private static string ValidateInventoryProfile(InventoryProfile profile, string characterId)
    {
        if (profile == null) return "profile_null";
        if (!string.Equals(profile.CharacterId, characterId, StringComparison.Ordinal)) return "character_id_mismatch";
        if (profile.SchemaVersion < 1) return "schema_version_invalid";
        if (profile.Items == null) return "items_null";
        if (profile.Items.Any(x => string.IsNullOrWhiteSpace(x.ItemId))) return "item_id_empty";
        if (profile.Items.Any(x => x.Quantity < 0)) return "quantity_negative";
        if (profile.Items.Any(x => x.Durability < 0)) return "durability_negative";
        if (profile.Items.Any(x => x.MaxDurability > 0 && x.MaxDurability < x.Durability)) return "max_durability_less_than_durability";
        if (profile.Items.Select(x => (x.ItemId ?? string.Empty).Trim()).GroupBy(x => x, StringComparer.OrdinalIgnoreCase).Any(x => x.Count() > 1)) return "duplicate_item_id";
        return string.Empty;
    }

    private static string ValidateRaceOrSpeciesProfile(RaceOrSpeciesProfile profile, string characterId)
    {
        if (profile == null) return "profile_null";
        if (!string.Equals(profile.CharacterId, characterId, StringComparison.Ordinal)) return "character_id_mismatch";
        if (profile.SchemaVersion < 1) return "schema_version_invalid";
        if (profile.Tags == null) return "tags_null";
        return string.Empty;
    }

    private static string ValidateBodyProfile(BodyProfile profile, string characterId)
    {
        if (profile == null) return "profile_null";
        if (!string.Equals(profile.CharacterId, characterId, StringComparison.Ordinal)) return "character_id_mismatch";
        if (profile.SchemaVersion < 1) return "schema_version_invalid";
        if (profile.BodyTags == null) return "body_tags_null";
        if (profile.EquipmentCompatibilityTags == null) return "equipment_compatibility_tags_null";
        if (profile.HeightCm < 0) return "height_cm_negative";
        if (profile.AgeYears < 0) return "age_years_negative";
        return string.Empty;
    }

    private ProfileNativeSkillWriteResult WriteSkillProfileAndSync(string characterId, string operation, string skillId, SkillProfile profile, ProfileLoadResult<SkillProfile> loaded, string actorUserId, string requestId)
    {
        UpsertByCharacterId(_mongo.CharacterSkillProfiles, characterId, new CharacterSkillProfileDocument { CharacterId = characterId, Profile = profile });
        var sync = SyncLegacySkillFacadeAsync(characterId, profile, actorUserId, requestId).GetAwaiter().GetResult();
        if (!sync.LegacyFacadeSynced)
        {
            _logger.Debug($"profile.native.write.facade_sync_error section={SkillsSection} characterId={characterId} message={sync.ErrorMessage}");
            return new ProfileNativeSkillWriteResult { CharacterId = characterId, Operation = operation, SkillId = skillId, UsedProfileNative = true, ProfileFound = loaded.ProfileFound, ProfileCreatedFromLegacy = loaded.ProfileCreatedFromLegacy, ProfileWritten = true, LegacyFacadeSynced = false, ErrorMessage = sync.ErrorMessage, WrittenAtUtc = DateTime.UtcNow };
        }

        _logger.Debug($"profile.native.write.done section={SkillsSection} operation={operation} characterId={characterId} skillId={skillId}");
        return new ProfileNativeSkillWriteResult { CharacterId = characterId, Operation = operation, SkillId = skillId, UsedProfileNative = true, ProfileFound = loaded.ProfileFound, ProfileCreatedFromLegacy = loaded.ProfileCreatedFromLegacy, ProfileWritten = true, LegacyFacadeSynced = true, WrittenAtUtc = DateTime.UtcNow };
    }

    private ProfileNativeDevelopmentWriteResult WriteDevelopmentProfileAndSync(string characterId, string operation, string nodeId, DevelopmentProfile profile, ProfileLoadResult<DevelopmentProfile> loaded, string actorUserId, string requestId)
    {
        UpsertByCharacterId(_mongo.CharacterDevelopmentProfiles, characterId, new CharacterDevelopmentProfileDocument { CharacterId = characterId, Profile = profile });
        var sync = SyncLegacyDevelopmentFacadeAsync(characterId, profile, actorUserId, requestId).GetAwaiter().GetResult();
        if (!sync.LegacyFacadeSynced)
        {
            _logger.Debug($"profile.native.write.facade_sync_error section={DevelopmentSection} characterId={characterId} message={sync.ErrorMessage}");
            return new ProfileNativeDevelopmentWriteResult { CharacterId = characterId, Operation = operation, DevelopmentNodeId = nodeId, UsedProfileNative = true, ProfileFound = loaded.ProfileFound, ProfileCreatedFromLegacy = loaded.ProfileCreatedFromLegacy, ProfileWritten = true, LegacyFacadeSynced = false, ErrorMessage = sync.ErrorMessage, WrittenAtUtc = DateTime.UtcNow };
        }

        _logger.Debug($"profile.native.write.done section={DevelopmentSection} operation={operation} characterId={characterId} nodeId={nodeId}");
        return new ProfileNativeDevelopmentWriteResult { CharacterId = characterId, Operation = operation, DevelopmentNodeId = nodeId, UsedProfileNative = true, ProfileFound = loaded.ProfileFound, ProfileCreatedFromLegacy = loaded.ProfileCreatedFromLegacy, ProfileWritten = true, LegacyFacadeSynced = true, WrittenAtUtc = DateTime.UtcNow };
    }

    private ProfileNativeInventoryWriteResult WriteInventoryProfileAndSync(string characterId, string operation, string itemId, InventoryProfile profile, ProfileLoadResult<InventoryProfile> loaded, string actorUserId, string requestId)
    {
        var reason = ValidateInventoryProfile(profile, characterId);
        if (!string.IsNullOrEmpty(reason))
        {
            LogProfileInvalid(InventorySection, characterId, reason);
            return InventoryFallback(characterId, operation, itemId, reason);
        }

        UpsertByCharacterId(_mongo.CharacterInventoryProfiles, characterId, new CharacterInventoryProfileDocument { CharacterId = characterId, Profile = profile });
        var sync = SyncLegacyInventoryFacadeAsync(characterId, profile, actorUserId, requestId).GetAwaiter().GetResult();
        if (!sync.LegacyFacadeSynced)
        {
            _logger.Debug($"profile.native.write.facade_sync_error section={InventorySection} characterId={characterId} message={sync.ErrorMessage}");
            return new ProfileNativeInventoryWriteResult { CharacterId = characterId, Operation = operation, ItemId = itemId, UsedProfileNative = true, ProfileFound = loaded.ProfileFound, ProfileCreatedFromLegacy = loaded.ProfileCreatedFromLegacy, ProfileWritten = true, LegacyFacadeSynced = false, ErrorMessage = sync.ErrorMessage, WrittenAtUtc = DateTime.UtcNow };
        }

        _logger.Debug($"profile.native.write.done section={InventorySection} operation={operation} characterId={characterId} itemId={itemId}");
        return new ProfileNativeInventoryWriteResult { CharacterId = characterId, Operation = operation, ItemId = itemId, UsedProfileNative = true, ProfileFound = loaded.ProfileFound, ProfileCreatedFromLegacy = loaded.ProfileCreatedFromLegacy, ProfileWritten = true, LegacyFacadeSynced = true, WrittenAtUtc = DateTime.UtcNow };
    }

    private ProfileNativeRaceBodyWriteResult WriteRaceBodyProfilesAndSync(string characterId, RaceOrSpeciesProfile raceProfile, BodyProfile bodyProfile, ProfileLoadResult<RaceOrSpeciesProfile> raceLoaded, ProfileLoadResult<BodyProfile> bodyLoaded, bool writeRace, bool writeBody, string actorUserId, string requestId)
    {
        if (writeRace)
        {
            UpsertByCharacterId(_mongo.CharacterRaceOrSpeciesProfiles, characterId, new CharacterRaceOrSpeciesProfileDocument { CharacterId = characterId, Profile = raceProfile });
        }

        if (writeBody)
        {
            UpsertByCharacterId(_mongo.CharacterBodyProfiles, characterId, new CharacterBodyProfileDocument { CharacterId = characterId, Profile = bodyProfile });
        }

        var sync = SyncLegacyRaceBodyFacadeAsync(characterId, raceProfile, bodyProfile, actorUserId, requestId, writeRace, writeBody).GetAwaiter().GetResult();
        if (!sync.LegacyFacadeSynced)
        {
            _logger.Debug($"profile.native.write.facade_sync_error section={RaceBodySection} characterId={characterId} message={sync.ErrorMessage}");
            return new ProfileNativeRaceBodyWriteResult
            {
                CharacterId = characterId,
                UsedProfileNative = true,
                RaceProfileFound = raceLoaded.ProfileFound,
                RaceProfileCreatedFromLegacy = raceLoaded.ProfileCreatedFromLegacy,
                BodyProfileFound = bodyLoaded.ProfileFound,
                BodyProfileCreatedFromLegacy = bodyLoaded.ProfileCreatedFromLegacy,
                RaceProfileWritten = writeRace,
                BodyProfileWritten = writeBody,
                LegacyFacadeSynced = false,
                ErrorMessage = sync.ErrorMessage,
                WrittenAtUtc = DateTime.UtcNow
            };
        }

        _logger.Debug($"profile.native.write.done section={RaceBodySection} characterId={characterId}");
        return new ProfileNativeRaceBodyWriteResult
        {
            CharacterId = characterId,
            UsedProfileNative = true,
            RaceProfileFound = raceLoaded.ProfileFound,
            RaceProfileCreatedFromLegacy = raceLoaded.ProfileCreatedFromLegacy,
            BodyProfileFound = bodyLoaded.ProfileFound,
            BodyProfileCreatedFromLegacy = bodyLoaded.ProfileCreatedFromLegacy,
            RaceProfileWritten = writeRace,
            BodyProfileWritten = writeBody,
            LegacyFacadeSynced = true,
            WrittenAtUtc = DateTime.UtcNow
        };
    }

    private static CharacterDevelopmentNodeState? FindDevelopmentNode(DevelopmentProfile profile, string nodeId) =>
        profile.Nodes.FirstOrDefault(x => string.Equals((x.DevelopmentNodeId ?? string.Empty).Trim(), nodeId, StringComparison.OrdinalIgnoreCase));

    private static string ResolveDevelopmentNodeId(Dictionary<string, object> payload)
    {
        if (payload == null) return string.Empty;
        var nodeId = (PayloadReader.GetString(payload, "nodeId") ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(nodeId)) return nodeId;
        var requiredNodeId = (PayloadReader.GetString(payload, "requiredNodeId") ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(requiredNodeId)) return requiredNodeId;
        var classId = (PayloadReader.GetString(payload, "classId") ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(classId)) return classId;
        return (PayloadReader.GetString(payload, "classCode") ?? string.Empty).Trim();
    }

    private static CharacterSkillProfileValue? FindSkill(SkillProfile profile, string skillId) =>
        profile.Skills.FirstOrDefault(x => string.Equals((x.SkillId ?? string.Empty).Trim(), skillId, StringComparison.OrdinalIgnoreCase));

    private SkillDefinition? LoadSkillDefinition(string skillId)
    {
        if (string.IsNullOrWhiteSpace(skillId)) return null;
        return _mongo.DefinitionSkills.Find(Builders<SkillDefinition>.Filter.Eq(x => x.Code, skillId)).FirstOrDefault();
    }

    private static int ResolveRequestedSkillRank(int requestedRank, SkillDefinition definition)
    {
        var minimum = Math.Max(0, definition?.RankMin ?? 0);
        var maximum = Math.Max(minimum, definition?.RankMax ?? 20);
        return Math.Min(Math.Max(minimum, requestedRank), maximum);
    }

    private static bool ValidateSkillRank(string skillId, int rank, SkillDefinition? definition, out string error)
    {
        error = string.Empty;
        if (rank < 0)
        {
            error = "rank_negative";
            return false;
        }

        if (definition != null)
        {
            var min = Math.Max(0, definition.RankMin);
            var max = Math.Max(min, definition.RankMax);
            if (rank < min || rank > max)
            {
                error = $"rank_out_of_range:{skillId}";
                return false;
            }
        }

        return true;
    }

    private static string ResolveSkillId(Dictionary<string, object> payload)
    {
        var skillId = (PayloadReader.GetString(payload, "skillId") ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(skillId)) return skillId;
        return (PayloadReader.GetString(payload, "skillCode") ?? string.Empty).Trim();
    }

    private List<CharacterSkillState> BuildLegacySkillFacade(SkillProfile profile)
    {
        var rows = new List<CharacterSkillState>();
        foreach (var skill in profile.Skills ?? new List<CharacterSkillProfileValue>())
        {
            var skillId = (skill.SkillId ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(skillId)) continue;
            var definition = LoadSkillDefinition(skillId);
            rows.Add(new CharacterSkillState
            {
                SkillCode = skillId,
                Tier = definition?.Tier ?? 1,
                Level = Math.Max(1, skill.Rank),
                Acquired = skill.IsLearned,
                Available = skill.IsUnlocked && !skill.IsLearned,
                LearnedUtc = skill.LearnedAtUtc == default ? DateTime.UtcNow : skill.LearnedAtUtc
            });
        }

        return rows;
    }

    private static IEnumerable<CharacterDevelopmentNodeState> ClassDevelopmentNodes(DevelopmentProfile profile)
    {
        return (profile?.Nodes ?? new List<CharacterDevelopmentNodeState>())
            .Where(x => !string.IsNullOrWhiteSpace(x.DevelopmentNodeId))
            .Where(x => string.Equals(x.NodeType, DevelopmentNodeTypes.Class, StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(x.NodeType))
            .Where(x => x.IsPurchased || x.IsUnlocked || x.CurrentTier > 0)
            .GroupBy(x => x.DevelopmentNodeId.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(x => x.Last());
    }

    private static List<CharacterClassState> BuildLegacyCharacterClasses(List<CharacterDevelopmentNodeState> classNodes)
    {
        return classNodes
            .Where(x => x.IsPurchased || x.IsUnlocked)
            .Select(x => new CharacterClassState
            {
                ClassCode = (x.DevelopmentNodeId ?? string.Empty).Trim(),
                Level = Math.Max(1, x.CurrentTier),
                LearnedUtc = x.PurchasedAtUtc == default ? DateTime.UtcNow : x.PurchasedAtUtc
            })
            .ToList();
    }

    private static List<CharacterClassProgress> BuildLegacyClassProgress(List<CharacterDevelopmentNodeState> classNodes)
    {
        return classNodes
            .Select(x => new CharacterClassProgress
            {
                ClassCode = (x.DevelopmentNodeId ?? string.Empty).Trim(),
                Level = Math.Max(1, x.CurrentTier),
                Experience = 0
            })
            .ToList();
    }

    private static void ApplyRacePayload(RaceOrSpeciesProfile profile, Dictionary<string, object> payload)
    {
        var raceId = GetTrimmed(payload, "raceId");
        if (!string.IsNullOrWhiteSpace(raceId)) profile.RaceId = raceId;
        var raceCode = GetTrimmed(payload, "raceCode");
        if (!string.IsNullOrWhiteSpace(raceCode)) profile.RaceCode = raceCode;
        var raceName = FirstNonEmpty(GetTrimmed(payload, "race"), GetTrimmed(payload, "raceName"));
        if (!string.IsNullOrWhiteSpace(raceName))
        {
            profile.RaceName = raceName;
            profile.DisplayName = raceName;
        }

        var subspeciesId = GetTrimmed(payload, "subspeciesId");
        if (string.IsNullOrWhiteSpace(subspeciesId)) subspeciesId = GetTrimmed(payload, "subspecies");
        if (!string.IsNullOrWhiteSpace(subspeciesId)) profile.SubspeciesId = subspeciesId;
        var hybridId = GetTrimmed(payload, "hybridId");
        if (string.IsNullOrWhiteSpace(hybridId)) hybridId = GetTrimmed(payload, "hybrid");
        if (!string.IsNullOrWhiteSpace(hybridId)) profile.HybridId = hybridId;
        var hybridSubtypeId = GetTrimmed(payload, "hybridSubtypeId");
        if (string.IsNullOrWhiteSpace(hybridSubtypeId)) hybridSubtypeId = GetTrimmed(payload, "hybridSubtype");
        if (!string.IsNullOrWhiteSpace(hybridSubtypeId)) profile.HybridSubtypeId = hybridSubtypeId;
        profile.Source = "profile_native";
        profile.Tags ??= new List<string>();
    }

    private static void ApplyBodyPayload(BodyProfile profile, Dictionary<string, object> payload)
    {
        if (payload.ContainsKey("description"))
        {
            profile.Description = PayloadReader.GetString(payload, "description") ?? string.Empty;
        }

        if (payload.ContainsKey("backstory"))
        {
            profile.Backstory = PayloadReader.GetString(payload, "backstory") ?? string.Empty;
        }

        var heightCm = PayloadReader.GetInt(payload, "heightCm");
        if (heightCm.HasValue && heightCm.Value >= 0) profile.HeightCm = heightCm.Value;
        var heightText = GetTrimmed(payload, "height");
        if (!string.IsNullOrWhiteSpace(heightText))
        {
            profile.HeightText = heightText;
            var parsedHeight = ParsePlainInt(heightText);
            if (parsedHeight >= 0) profile.HeightCm = parsedHeight;
        }

        var ageYears = PayloadReader.GetInt(payload, "ageYears") ?? PayloadReader.GetInt(payload, "age");
        if (ageYears.HasValue && ageYears.Value >= 0)
        {
            profile.AgeYears = ageYears.Value;
            profile.AgeText = ageYears.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        var ageText = GetTrimmed(payload, "ageText");
        if (!string.IsNullOrWhiteSpace(ageText))
        {
            profile.AgeText = ageText;
            var parsedAge = ParsePlainInt(ageText);
            if (parsedAge >= 0) profile.AgeYears = parsedAge;
        }

        var bodyType = GetTrimmed(payload, "bodyType");
        if (!string.IsNullOrWhiteSpace(bodyType)) profile.BodyType = bodyType;
        var speciesBodyType = GetTrimmed(payload, "speciesBodyType");
        if (!string.IsNullOrWhiteSpace(speciesBodyType)) profile.SpeciesBodyType = speciesBodyType;
        var sizeCategory = GetTrimmed(payload, "sizeCategory");
        if (!string.IsNullOrWhiteSpace(sizeCategory)) profile.SizeCategory = sizeCategory;
        profile.Source = "profile_native";
        profile.BodyTags ??= new List<string>();
        profile.EquipmentCompatibilityTags ??= new List<string>();
        profile.BodyStats ??= new Dictionary<string, int>();
        var bodyStats = PayloadReader.GetDictionary(payload, "bodyStats");
        if (bodyStats != null)
        {
            foreach (var pair in bodyStats)
            {
                if (int.TryParse(Convert.ToString(pair.Value, System.Globalization.CultureInfo.InvariantCulture), out var value) && value >= 0)
                    profile.BodyStats[pair.Key] = value;
            }
        }
    }

    private static string GetTrimmed(Dictionary<string, object> payload, string key)
    {
        return (PayloadReader.GetString(payload, key) ?? string.Empty).Trim();
    }

    private static string FirstNonEmpty(params string[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
        }

        return string.Empty;
    }

    private static int ParsePlainInt(string value)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed)) return -1;
        foreach (var ch in trimmed)
        {
            if (!char.IsDigit(ch)) return -1;
        }

        return int.TryParse(trimmed, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var parsed) ? parsed : -1;
    }

    private static CharacterInventoryItemProfileValue? FindInventoryItem(InventoryProfile profile, string itemId) =>
        profile.Items.FirstOrDefault(x => string.Equals((x.ItemId ?? string.Empty).Trim(), itemId, StringComparison.OrdinalIgnoreCase));

    private static string ResolveInventoryItemId(Dictionary<string, object> payload, bool allowGenerated)
    {
        if (payload == null) return allowGenerated ? Guid.NewGuid().ToString("N") : string.Empty;
        var itemId = (PayloadReader.GetString(payload, "itemId") ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(itemId)) return itemId;
        var id = (PayloadReader.GetString(payload, "id") ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(id)) return id;
        return allowGenerated ? Guid.NewGuid().ToString("N") : string.Empty;
    }

    private static string ResolveInventoryDefinitionId(Dictionary<string, object> payload)
    {
        if (payload == null) return string.Empty;
        var itemDefinitionId = (PayloadReader.GetString(payload, "itemDefinitionId") ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(itemDefinitionId)) return itemDefinitionId;
        var itemCode = (PayloadReader.GetString(payload, "itemCode") ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(itemCode)) return itemCode;
        return (PayloadReader.GetString(payload, "definitionId") ?? string.Empty).Trim();
    }

    private static CharacterInventoryItemProfileValue ParseInventoryProfileItem(Dictionary<string, object> payload)
    {
        var itemId = ResolveInventoryItemId(payload, allowGenerated: true);
        var quantity = PayloadReader.GetInt(payload, "quantity") ?? 1;
        var durability = PayloadReader.GetInt(payload, "durability") ?? PayloadReader.GetInt(payload, "durabilityOrHealth") ?? 0;
        var ammo = PayloadReader.GetInt(payload, "ammo") ?? PayloadReader.GetInt(payload, "consumptionPerUse") ?? 0;
        var isEquipped = PayloadReader.GetBool(payload, "isEquipped") || PayloadReader.GetBool(payload, "equipped");
        var isPlayerVisible = !payload.ContainsKey("isPlayerVisible") || PayloadReader.GetBool(payload, "isPlayerVisible");
        var name = PayloadReader.GetString(payload, "name") ?? string.Empty;
        var label = PayloadReader.GetString(payload, "label") ?? string.Empty;
        var displayName = PayloadReader.GetString(payload, "displayName") ?? string.Empty;
        var definitionId = ResolveInventoryDefinitionId(payload);
        var definitionCategory = PayloadReader.GetString(payload, "definitionCategory") ?? string.Empty;
        var definitionCode = PayloadReader.GetString(payload, "definitionCode") ?? definitionId;
        var snapshotDisplayName = PayloadReader.GetString(payload, "snapshotDisplayName") ?? displayName;
        var snapshotCategory = PayloadReader.GetString(payload, "snapshotCategory") ?? PayloadReader.GetString(payload, "category") ?? string.Empty;
        var snapshotDescription = PayloadReader.GetString(payload, "snapshotDescription") ?? PayloadReader.GetString(payload, "description") ?? string.Empty;
        var now = DateTime.UtcNow;
        return new CharacterInventoryItemProfileValue
        {
            ItemId = itemId,
            DefinitionId = definitionId,
            ItemDefinitionId = definitionId,
            DefinitionCategory = definitionCategory,
            DefinitionCode = definitionCode,
            SnapshotDisplayName = FirstNonEmpty(snapshotDisplayName, displayName, name, label),
            SnapshotCategory = snapshotCategory,
            SnapshotDescription = snapshotDescription,
            SnapshotTags = SplitInventoryTags(PayloadReader.GetString(payload, "snapshotTagsText") ?? PayloadReader.GetString(payload, "tagsText") ?? string.Empty),
            Name = FirstNonEmpty(name, displayName, label, snapshotDisplayName),
            DisplayName = FirstNonEmpty(displayName, snapshotDisplayName, name, label),
            Category = PayloadReader.GetString(payload, "category") ?? snapshotCategory,
            Description = PayloadReader.GetString(payload, "description") ?? snapshotDescription,
            Quantity = Math.Max(0, quantity),
            Durability = Math.Max(0, durability),
            MaxDurability = Math.Max(0, durability),
            Condition = PayloadReader.GetString(payload, "condition") ?? string.Empty,
            Ammo = Math.Max(0, ammo),
            IsEquipped = isEquipped,
            SlotId = PayloadReader.GetString(payload, "slotId") ?? PayloadReader.GetString(payload, "slot") ?? PayloadReader.GetString(payload, "properties") ?? string.Empty,
            IsPlayerVisible = isPlayerVisible,
            SortOrder = PayloadReader.GetInt(payload, "sortOrder") ?? 0,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            Source = PayloadReader.GetString(payload, "source") ?? "profile_native",
            Notes = PayloadReader.GetString(payload, "notes") ?? string.Empty,
            Tags = BuildInventoryTags(payload, isEquipped)
        };
    }

    private static List<string> BuildInventoryTags(Dictionary<string, object> payload, bool isEquipped)
    {
        var tags = SplitInventoryTags(PayloadReader.GetString(payload, "tagsText") ?? string.Empty);
        if (PayloadReader.GetBool(payload, "usesAmmoOrConsumable")) tags.Add("consumable");
        if ((PayloadReader.GetInt(payload, "consumptionPerUse") ?? 0) > 0) tags.Add("ammo");
        if (isEquipped) tags.Add("equipped");
        return tags.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static List<string> SplitInventoryTags(string? value)
    {
        return (value ?? string.Empty)
            .Split(new[] { ',', ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void CopyInventoryItem(CharacterInventoryItemProfileValue source, CharacterInventoryItemProfileValue target)
    {
        target.ItemId = source.ItemId;
        target.DefinitionId = source.DefinitionId;
        target.ItemDefinitionId = source.ItemDefinitionId;
        target.DefinitionCategory = source.DefinitionCategory;
        target.DefinitionCode = source.DefinitionCode;
        target.SnapshotDisplayName = source.SnapshotDisplayName;
        target.SnapshotCategory = source.SnapshotCategory;
        target.SnapshotDescription = source.SnapshotDescription;
        target.SnapshotTags = source.SnapshotTags ?? new List<string>();
        target.Name = source.Name;
        target.DisplayName = source.DisplayName;
        target.Category = source.Category;
        target.Description = source.Description;
        target.Quantity = source.Quantity;
        target.Durability = source.Durability;
        target.MaxDurability = source.MaxDurability;
        target.Condition = source.Condition;
        target.Ammo = source.Ammo;
        target.IsEquipped = source.IsEquipped;
        target.SlotId = source.SlotId;
        target.IsPlayerVisible = source.IsPlayerVisible;
        target.SortOrder = source.SortOrder;
        if (target.CreatedAtUtc == default) target.CreatedAtUtc = source.CreatedAtUtc == default ? DateTime.UtcNow : source.CreatedAtUtc;
        target.UpdatedAtUtc = DateTime.UtcNow;
        target.Source = source.Source;
        target.Notes = source.Notes;
        target.Tags = source.Tags ?? new List<string>();
    }

    private static List<InventoryItem> BuildLegacyInventoryFacade(InventoryProfile profile, List<InventoryItem> existingLegacy)
    {
        var existingById = (existingLegacy ?? new List<InventoryItem>())
            .Where(x => !string.IsNullOrWhiteSpace(x.Id))
            .GroupBy(x => x.Id.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Last(), StringComparer.OrdinalIgnoreCase);

        return (profile?.Items ?? new List<CharacterInventoryItemProfileValue>())
            .Where(x => !string.IsNullOrWhiteSpace(x.ItemId))
            .GroupBy(x => x.ItemId.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(x => BuildLegacyInventoryItem(x.Last(), existingById))
            .ToList();
    }

    private static InventoryItem BuildLegacyInventoryItem(CharacterInventoryItemProfileValue profileItem, Dictionary<string, InventoryItem> existingById)
    {
        var itemId = (profileItem.ItemId ?? string.Empty).Trim();
        existingById.TryGetValue(itemId, out var existing);
        var item = existing == null ? new InventoryItem() : CloneInventoryItem(existing);
        item.Id = itemId;
        item.ItemCode = FirstNonEmpty(profileItem.ItemDefinitionId, profileItem.DefinitionId, profileItem.DefinitionCode);
        var displayName = FirstNonEmpty(profileItem.SnapshotDisplayName, profileItem.DisplayName, profileItem.Name);
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            item.Name = displayName;
            if (string.IsNullOrWhiteSpace(item.Label)) item.Label = displayName;
        }

        item.Quantity = Math.Max(0, profileItem.Quantity);
        item.Durability = Math.Max(0, profileItem.Durability);
        item.DurabilityOrHealth = Math.Max(0, profileItem.Durability);
        item.Description = FirstNonEmpty(profileItem.SnapshotDescription, profileItem.Description);
        item.Category = FirstNonEmpty(profileItem.SnapshotCategory, profileItem.Category, profileItem.DefinitionCategory);
        item.IsEquipped = profileItem.IsEquipped;
        item.Equipped = profileItem.IsEquipped;
        if (!string.IsNullOrWhiteSpace(profileItem.SlotId)) item.Properties = profileItem.SlotId;
        if (!string.IsNullOrWhiteSpace(profileItem.Notes)) item.Notes = profileItem.Notes;
        var tags = profileItem.Tags ?? new List<string>();
        if (existing == null)
        {
            item.UsesAmmoOrConsumable = tags.Any(x => string.Equals(x, "consumable", StringComparison.OrdinalIgnoreCase));
            item.ConsumptionPerUse = Math.Max(0, profileItem.Ammo);
        }

        return item;
    }

    private static InventoryItem CloneInventoryItem(InventoryItem source)
    {
        return new InventoryItem
        {
            Id = source.Id,
            ItemCode = source.ItemCode,
            Name = source.Name,
            Label = source.Label,
            Description = source.Description,
            Category = source.Category,
            Quantity = source.Quantity,
            DurabilityOrHealth = source.DurabilityOrHealth,
            Durability = source.Durability,
            IsEquipped = source.IsEquipped,
            Equipped = source.Equipped,
            UsesAmmoOrConsumable = source.UsesAmmoOrConsumable,
            ConsumptionPerUse = source.ConsumptionPerUse,
            Properties = source.Properties,
            Notes = source.Notes,
            Archived = source.Archived,
            Deleted = source.Deleted
        };
    }

    private Dictionary<string, AttributeDefinitionBounds> LoadAttributeDefinitions(string ruleSetId)
    {
        var filter = Builders<UnifiedDefinitionDocument>.Filter.In(x => x.Category, new[] { DefinitionCategoryIds.Attribute, DefinitionCategoryIds.DerivedStat })
            & Builders<UnifiedDefinitionDocument>.Filter.Eq(x => x.IsArchived, false);
        if (!string.IsNullOrWhiteSpace(ruleSetId))
        {
            filter &= Builders<UnifiedDefinitionDocument>.Filter.AnyEq(x => x.RuleSetIds, ruleSetId);
        }

        var loaded = _mongo.UnifiedDefinitions.Find(filter).ToList()
            .Select(ToAttributeDefinitionBounds)
            .Where(x => !string.IsNullOrWhiteSpace(x.AttributeId))
            .GroupBy(x => x.AttributeId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Last(), StringComparer.OrdinalIgnoreCase);

        var fallback = new[]
        {
            DefaultAttributeBounds(CharacterAttributeIds.Strength),
            DefaultAttributeBounds(CharacterAttributeIds.Dexterity),
            DefaultAttributeBounds(CharacterAttributeIds.Endurance),
            DefaultAttributeBounds(CharacterAttributeIds.Intellect),
            DefaultAttributeBounds(CharacterAttributeIds.Wisdom),
            DefaultAttributeBounds(CharacterAttributeIds.Charisma),
            DefaultAttributeBounds(CharacterVitalStatIds.HealthCurrent, 999),
            DefaultAttributeBounds(CharacterVitalStatIds.HealthMax, 999),
            DefaultAttributeBounds(CharacterVitalStatIds.PhysicalDefense, 999),
            DefaultAttributeBounds(CharacterVitalStatIds.MagicalDefense, 999),
            DefaultAttributeBounds(CharacterVitalStatIds.Morale, 999),
            DefaultAttributeBounds(CharacterVitalStatIds.Initiative, 999),
            DefaultAttributeBounds(CharacterVitalStatIds.Movement, 999),
            DefaultAttributeBounds(CharacterVitalStatIds.CarryingCapacity, 999),
            DefaultAttributeBounds("dev_acceptance_derived", 999)
        }.ToDictionary(x => x.AttributeId, x => x, StringComparer.OrdinalIgnoreCase);

        foreach (var pair in loaded)
        {
            fallback[pair.Key] = pair.Value;
        }

        return fallback;
    }

    private static AttributeDefinitionBounds ToAttributeDefinitionBounds(UnifiedDefinitionDocument definition)
    {
        var extra = definition.ExtraData ?? new Dictionary<string, object>();
        return new AttributeDefinitionBounds
        {
            AttributeId = FirstNonEmpty(GetExtraString(extra, "attributeId"), definition.Id),
            MinValue = GetExtraInt(extra, "minValue", 0),
            MaxValue = GetExtraInt(extra, "maxValue", 30)
        };
    }

    private static AttributeDefinitionBounds DefaultAttributeBounds(string attributeId, int maxValue = 30) => new AttributeDefinitionBounds { AttributeId = attributeId, MinValue = 0, MaxValue = maxValue };

    private Dictionary<string, CurrencyDefinitionBounds> LoadCurrencyDefinitions(string ruleSetId)
    {
        var filter = Builders<UnifiedDefinitionDocument>.Filter.Eq(x => x.Category, DefinitionCategoryIds.Currency)
            & Builders<UnifiedDefinitionDocument>.Filter.Eq(x => x.IsArchived, false);
        if (!string.IsNullOrWhiteSpace(ruleSetId))
        {
            filter &= Builders<UnifiedDefinitionDocument>.Filter.AnyEq(x => x.RuleSetIds, ruleSetId);
        }

        var loaded = _mongo.UnifiedDefinitions.Find(filter).ToList()
            .Select(ToCurrencyDefinitionBounds)
            .Where(x => !string.IsNullOrWhiteSpace(x.CurrencyId))
            .GroupBy(x => x.CurrencyId, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.Last())
            .ToDictionary(x => x.CurrencyId, x => x, StringComparer.OrdinalIgnoreCase);

        if (loaded.Count > 0) return loaded;

        return new[]
        {
            DefaultCurrencyBounds(CharacterCurrencyIds.IronCoin, "iron", "Iron"),
            DefaultCurrencyBounds(CharacterCurrencyIds.BronzeCoin, "bronze", "Bronze"),
            DefaultCurrencyBounds(CharacterCurrencyIds.SilverCoin, "silver", "Silver"),
            DefaultCurrencyBounds(CharacterCurrencyIds.GoldCoin, "gold", "Gold"),
            DefaultCurrencyBounds(CharacterCurrencyIds.PlatinumCoin, "platinum", "Platinum"),
            DefaultCurrencyBounds(CharacterCurrencyIds.OrichalcumCoin, "orichalcum", "Orichalcum"),
            DefaultCurrencyBounds(CharacterCurrencyIds.AdamantCoin, "adamant", "Adamant"),
            DefaultCurrencyBounds(CharacterCurrencyIds.SovereignCoin, "sovereign", "Sovereign"),
            DefaultCurrencyBounds(CharacterCurrencyIds.XpCoin, "xp_coin", "XpCoins")
        }.ToDictionary(x => x.CurrencyId, x => x, StringComparer.OrdinalIgnoreCase);
    }

    private static CurrencyDefinitionBounds ToCurrencyDefinitionBounds(UnifiedDefinitionDocument definition)
    {
        var extra = definition.ExtraData ?? new Dictionary<string, object>();
        var id = FirstNonEmpty(GetExtraString(extra, "currencyId"), definition.Id);
        var code = FirstNonEmpty(GetExtraString(extra, "code"), id);
        return new CurrencyDefinitionBounds
        {
            CurrencyId = id,
            Code = code,
            MinValue = GetExtraLong(extra, "minValue", 0),
            MaxValue = GetExtraNullableLong(extra, "maxValue"),
            LegacyKey = FirstNonEmpty(GetExtraString(extra, "legacyKey"), LegacyCurrencyKey(id, code))
        };
    }

    private static CurrencyDefinitionBounds DefaultCurrencyBounds(string currencyId, string code, string legacyKey) =>
        new CurrencyDefinitionBounds { CurrencyId = currencyId, Code = code, LegacyKey = legacyKey, MinValue = 0 };

    private static CurrencyDefinitionBounds? ResolveCurrencyDefinition(Dictionary<string, CurrencyDefinitionBounds> definitions, string currencyId, string code)
    {
        if (!string.IsNullOrWhiteSpace(currencyId) && definitions.TryGetValue(currencyId, out var direct)) return direct;
        if (!string.IsNullOrWhiteSpace(code))
        {
            var byCode = definitions.Values.FirstOrDefault(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase)
                || string.Equals(x.LegacyKey, code, StringComparison.OrdinalIgnoreCase));
            if (byCode != null) return byCode;
        }

        return null;
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

    private static List<AttributeWriteRow> ReadAttributeRows(Dictionary<string, object> payload)
    {
        if (payload == null || !TryGetPayloadValue(payload, "attributes", out var value) || value == null) return new List<AttributeWriteRow>();
        var rows = new List<AttributeWriteRow>();
        foreach (var item in EnumerateItems(value))
        {
            var map = ToObjectMap(item);
            if (map.Count == 0) continue;
            var attributeId = FirstNonEmpty(GetMapString(map, "attributeId"), GetMapString(map, "id"), GetMapString(map, "code"));
            rows.Add(new AttributeWriteRow
            {
                AttributeId = attributeId,
                Value = GetMapInt(map, "value") ?? GetMapInt(map, "currentValue")
            });
        }

        return rows;
    }

    private static List<CurrencyWriteRow> ReadCurrencyRows(Dictionary<string, object> payload)
    {
        if (payload == null || !TryGetPayloadValue(payload, "currencies", out var value) || value == null) return new List<CurrencyWriteRow>();
        var rows = new List<CurrencyWriteRow>();
        foreach (var item in EnumerateItems(value))
        {
            var map = ToObjectMap(item);
            if (map.Count == 0) continue;
            var currencyId = FirstNonEmpty(GetMapString(map, "currencyId"), GetMapString(map, "id"));
            var code = FirstNonEmpty(GetMapString(map, "code"), currencyId);
            rows.Add(new CurrencyWriteRow
            {
                CurrencyId = currencyId,
                Code = code,
                Amount = GetMapLong(map, "amount") ?? GetMapLong(map, "value")
            });
        }

        return rows;
    }

    private static bool TryGetPayloadValue(Dictionary<string, object> payload, string key, out object? value)
    {
        value = null;
        if (payload.TryGetValue(key, out var direct))
        {
            value = direct;
            return true;
        }

        foreach (var pair in payload)
        {
            if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                value = pair.Value;
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<object> EnumerateItems(object value)
    {
        if (value is string) yield break;
        if (value is IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                if (item != null) yield return item;
            }
        }
    }

    private static Dictionary<string, object> ToObjectMap(object value)
    {
        if (value is Dictionary<string, object> typed) return new Dictionary<string, object>(typed, StringComparer.OrdinalIgnoreCase);
        if (value is IDictionary dictionary)
        {
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry entry in dictionary)
            {
                var key = Convert.ToString(entry.Key);
                if (!string.IsNullOrWhiteSpace(key)) result[key] = entry.Value!;
            }
            return result;
        }

        if (value is IEnumerable enumerable && value is not string)
        {
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            var sequentialItems = new List<object?>();
            foreach (var item in enumerable)
            {
                sequentialItems.Add(item);
                if (TryReadKeyValueEntry(item, out var key, out var entryValue))
                {
                    result[key] = entryValue!;
                }
            }

            if (result.Count == 0 && sequentialItems.Count % 2 == 0)
            {
                for (var i = 0; i < sequentialItems.Count; i += 2)
                {
                    var key = Convert.ToString(sequentialItems[i]);
                    if (!string.IsNullOrWhiteSpace(key)) result[key] = sequentialItems[i + 1]!;
                }
            }

            if (result.Count > 0) return result;
        }

        return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
    }

    private static bool TryReadKeyValueEntry(object? value, out string key, out object? entryValue)
    {
        key = string.Empty;
        entryValue = null;
        if (value == null) return false;

        if (value is DictionaryEntry dictionaryEntry)
        {
            key = Convert.ToString(dictionaryEntry.Key) ?? string.Empty;
            entryValue = dictionaryEntry.Value;
            return !string.IsNullOrWhiteSpace(key);
        }

        if (value is IDictionary dictionary)
        {
            object? keyCandidate = null;
            object? valueCandidate = null;
            foreach (DictionaryEntry entry in dictionary)
            {
                var entryKey = Convert.ToString(entry.Key);
                if (string.Equals(entryKey, "key", StringComparison.OrdinalIgnoreCase)) keyCandidate = entry.Value;
                if (string.Equals(entryKey, "value", StringComparison.OrdinalIgnoreCase)) valueCandidate = entry.Value;
            }

            key = Convert.ToString(keyCandidate) ?? string.Empty;
            entryValue = valueCandidate;
            return !string.IsNullOrWhiteSpace(key);
        }

        var type = value.GetType();
        var keyProperty = type.GetProperty("Key") ?? type.GetProperty("key");
        var valueProperty = type.GetProperty("Value") ?? type.GetProperty("value");
        if (keyProperty == null || valueProperty == null) return false;

        key = Convert.ToString(keyProperty.GetValue(value)) ?? string.Empty;
        entryValue = valueProperty.GetValue(value);
        return !string.IsNullOrWhiteSpace(key);
    }

    private static string GetMapString(Dictionary<string, object> map, string key)
    {
        return map.TryGetValue(key, out var value) && value != null ? Convert.ToString(value) ?? string.Empty : string.Empty;
    }

    private static int? GetMapInt(Dictionary<string, object> map, string key)
    {
        if (!map.TryGetValue(key, out var value) || value == null) return null;
        if (value is int i) return i;
        if (value is long l) return checked((int)l);
        if (value is double d) return (int)d;
        if (value is decimal m) return (int)m;
        var raw = Convert.ToString(value);
        return int.TryParse(raw, out var parsed) ? parsed : (int?)null;
    }

    private static long? GetMapLong(Dictionary<string, object> map, string key)
    {
        if (!map.TryGetValue(key, out var value) || value == null) return null;
        if (value is long l) return l;
        if (value is int i) return i;
        if (value is double d) return (long)d;
        if (value is decimal m) return (long)m;
        var raw = Convert.ToString(value);
        return long.TryParse(raw, out var parsed) ? parsed : (long?)null;
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

    private static void UpsertAttribute(AttributeProfile profile, string attributeId, int value)
    {
        var row = profile.Values.FirstOrDefault(x => string.Equals(x.AttributeId, attributeId, StringComparison.OrdinalIgnoreCase));
        if (row == null)
        {
            profile.Values.Add(new CharacterAttributeValue { AttributeId = attributeId, Source = "profile_native" });
            row = profile.Values[profile.Values.Count - 1];
        }

        row.BaseValue = value;
        row.CurrentValue = value;
        row.ManualModifier = 0;
        row.Source = "profile_native";
    }

    private static void UpsertWallet(WalletProfile profile, string currencyId, long amount)
    {
        var row = profile.Wallets.FirstOrDefault(x => string.Equals(x.CurrencyId, currencyId, StringComparison.OrdinalIgnoreCase));
        if (row == null)
        {
            profile.Wallets.Add(new CharacterWalletValue { CurrencyId = currencyId, Source = "profile_native" });
            row = profile.Wallets[profile.Wallets.Count - 1];
        }

        row.Amount = amount;
        row.Source = "profile_native";
    }

    private static Dictionary<string, CharacterAttributeValue> AttributeMap(AttributeProfile profile) =>
        (profile.Values ?? new List<CharacterAttributeValue>())
            .Where(x => !string.IsNullOrWhiteSpace(x.AttributeId))
            .GroupBy(x => x.AttributeId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Last(), StringComparer.OrdinalIgnoreCase);

    private static int GetAttribute(Dictionary<string, CharacterAttributeValue> map, string attributeId) =>
        map.TryGetValue(attributeId, out var row) ? row.CurrentValue : 0;

    private static int GetFirstAttribute(Dictionary<string, CharacterAttributeValue> map, params string[] attributeIds)
    {
        foreach (var attributeId in attributeIds)
        {
            if (!string.IsNullOrWhiteSpace(attributeId) && map.TryGetValue(attributeId, out var row)) return row.CurrentValue;
        }

        return 0;
    }

    private static Dictionary<string, CharacterWalletValue> WalletMap(WalletProfile profile) =>
        (profile.Wallets ?? new List<CharacterWalletValue>())
            .Where(x => !string.IsNullOrWhiteSpace(x.CurrencyId))
            .GroupBy(x => x.CurrencyId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Last(), StringComparer.OrdinalIgnoreCase);

    private static long GetWalletAmount(Dictionary<string, CharacterWalletValue> map, string currencyId) =>
        map.TryGetValue(currencyId, out var row) ? row.Amount : 0L;

    private sealed class AttributeWriteRow
    {
        public string AttributeId { get; set; } = string.Empty;
        public int? Value { get; set; }
    }

    private sealed class AttributeDefinitionBounds
    {
        public string AttributeId { get; set; } = string.Empty;
        public int MinValue { get; set; }
        public int MaxValue { get; set; } = 30;
    }

    private sealed class CurrencyWriteRow
    {
        public string CurrencyId { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public long? Amount { get; set; }
    }

    private sealed class CurrencyDefinitionBounds
    {
        public string CurrencyId { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public long MinValue { get; set; }
        public long? MaxValue { get; set; }
        public string LegacyKey { get; set; } = string.Empty;
    }

    private static void UpsertByCharacterId<TDoc>(IMongoCollection<TDoc> collection, string characterId, TDoc doc) where TDoc : EntityBase
    {
        var existing = collection.Find(Builders<TDoc>.Filter.Eq("CharacterId", characterId)).FirstOrDefault();
        if (existing != null)
        {
            doc.Id = existing.Id;
            doc.CreatedUtc = existing.CreatedUtc;
        }

        doc.UpdatedUtc = DateTime.UtcNow;
        var result = collection.ReplaceOne(Builders<TDoc>.Filter.Eq("CharacterId", characterId), doc, new ReplaceOptions { IsUpsert = true });
        if (!result.IsAcknowledged) throw new InvalidOperationException("profile_replace_not_acknowledged");
    }

    private static void EnsureReplaceSucceeded(ReplaceOneResult result, string message)
    {
        if (!result.IsAcknowledged || result.MatchedCount == 0)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class ProfileLoadResult<TProfile>
    {
        public TProfile Profile { get; private set; } = default!;
        public bool IsValid { get; private set; }
        public bool ProfileFound { get; private set; }
        public bool ProfileCreatedFromLegacy { get; private set; }
        public string ErrorMessage { get; private set; } = string.Empty;

        public static ProfileLoadResult<TProfile> Valid(TProfile profile, bool profileFound, bool profileCreatedFromLegacy) =>
            new ProfileLoadResult<TProfile> { Profile = profile, IsValid = true, ProfileFound = profileFound, ProfileCreatedFromLegacy = profileCreatedFromLegacy };

        public static ProfileLoadResult<TProfile> Invalid(string errorMessage, bool profileFound, bool profileCreatedFromLegacy) =>
            new ProfileLoadResult<TProfile> { IsValid = false, ProfileFound = profileFound, ProfileCreatedFromLegacy = profileCreatedFromLegacy, ErrorMessage = errorMessage ?? string.Empty };
    }
}
