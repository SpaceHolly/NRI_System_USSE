using System;
using System.Collections.Generic;
using MongoDB.Driver;
using Nri.Server.Infrastructure;
using Nri.Server.Logging;
using Nri.Shared.Domain;

namespace Nri.Server.Application;

public interface ICharacterProfileService
{
    AttributeProfile GetAttributeProfile(string characterId);
    SkillProfile GetSkillProfile(string characterId);
    DevelopmentProfile GetDevelopmentProfile(string characterId);
    WalletProfile GetWalletProfile(string characterId);
    RaceOrSpeciesProfile GetRaceOrSpeciesProfile(string characterId);
    BodyProfile GetBodyProfile(string characterId);
    KnowledgeProfile GetKnowledgeProfile(string characterId);
    ConditionProfile GetConditionProfile(string characterId);
    InventoryProfile GetInventoryProfile(string characterId);
    ReputationProfile GetReputationProfile(string characterId);
    HoldingsProfile GetHoldingsProfile(string characterId);
    CompanionProfile GetCompanionProfile(string characterId);
    CharacterModuleState GetEnabledModules(string characterId);
    CharacterProfileBundle GetProfileBundle(string characterId);
    AttributeProfile GetAttributeProfileShadow(string characterId);
    AttributeProfileComparisonResult CompareAttributeProfileShadow(string characterId);
    WalletProfile GetWalletProfileShadow(string characterId);
    WalletProfileComparisonResult CompareWalletProfileShadow(string characterId);
    SkillProfile GetSkillProfileShadow(string characterId);
    SkillProfileComparisonResult CompareSkillProfileShadow(string characterId);
    DevelopmentProfile GetDevelopmentProfileShadow(string characterId);
    DevelopmentProfileComparisonResult CompareDevelopmentProfileShadow(string characterId);
    InventoryProfile GetInventoryProfileShadow(string characterId);
    InventoryProfileComparisonResult CompareInventoryProfileShadow(string characterId);
    RaceOrSpeciesProfile GetRaceOrSpeciesProfileShadow(string characterId);
    RaceOrSpeciesProfileComparisonResult CompareRaceOrSpeciesProfileShadow(string characterId);
    BodyProfile GetBodyProfileShadow(string characterId);
    BodyProfileComparisonResult CompareBodyProfileShadow(string characterId);
    ReputationProfile GetReputationProfileShadow(string characterId);
    ReputationProfileComparisonResult CompareReputationProfileShadow(string characterId);
    HoldingsProfile GetHoldingsProfileShadow(string characterId);
    HoldingsProfileComparisonResult CompareHoldingsProfileShadow(string characterId);
    CompanionProfile GetCompanionProfileShadow(string characterId);
    CompanionProfileComparisonResult CompareCompanionProfileShadow(string characterId);
}

public class CharacterProfileBundle
{
    public string CharacterId { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = string.Empty;
    public Dictionary<string, bool> Modules { get; set; } = new Dictionary<string, bool>();
    public AttributeProfile AttributeProfile { get; set; } = new AttributeProfile();
    public SkillProfile SkillProfile { get; set; } = new SkillProfile();
    public DevelopmentProfile DevelopmentProfile { get; set; } = new DevelopmentProfile();
    public WalletProfile WalletProfile { get; set; } = new WalletProfile();
    public RaceOrSpeciesProfile RaceOrSpeciesProfile { get; set; } = new RaceOrSpeciesProfile();
    public BodyProfile BodyProfile { get; set; } = new BodyProfile();
    public KnowledgeProfile KnowledgeProfile { get; set; } = new KnowledgeProfile();
    public ConditionProfile ConditionProfile { get; set; } = new ConditionProfile();
    public InventoryProfile InventoryProfile { get; set; } = new InventoryProfile();
    public ReputationProfile ReputationProfile { get; set; } = new ReputationProfile();
    public HoldingsProfile HoldingsProfile { get; set; } = new HoldingsProfile();
    public CompanionProfile CompanionProfile { get; set; } = new CompanionProfile();
    public int SchemaVersion { get; set; } = 1;
}

public static class CharacterProfileDefaults
{
    // Empty defaults are safe read fallbacks and must not be auto-persisted.
    public static AttributeProfile EmptyAttributeProfile() => new AttributeProfile();
    public static SkillProfile EmptySkillProfile() => new SkillProfile();
    public static DevelopmentProfile EmptyDevelopmentProfile() => new DevelopmentProfile();
    public static WalletProfile EmptyWalletProfile() => new WalletProfile();
    public static RaceOrSpeciesProfile EmptyRaceOrSpeciesProfile() => new RaceOrSpeciesProfile();
    public static BodyProfile EmptyBodyProfile() => new BodyProfile();
    public static KnowledgeProfile EmptyKnowledgeProfile() => new KnowledgeProfile();
    public static ConditionProfile EmptyConditionProfile() => new ConditionProfile();
    public static InventoryProfile EmptyInventoryProfile() => new InventoryProfile();
    public static ReputationProfile EmptyReputationProfile() => new ReputationProfile();
    public static HoldingsProfile EmptyHoldingsProfile() => new HoldingsProfile();
    public static CompanionProfile EmptyCompanionProfile() => new CompanionProfile();

    public static CharacterProfileBundle EmptyBundle(string characterId)
    {
        return new CharacterProfileBundle
        {
            CharacterId = characterId ?? string.Empty,
            RuleSetId = RuleSetIds.FantasyNriDefault,
            Modules = new Dictionary<string, bool>(),
            AttributeProfile = EmptyAttributeProfile(),
            SkillProfile = EmptySkillProfile(),
            DevelopmentProfile = EmptyDevelopmentProfile(),
            WalletProfile = EmptyWalletProfile(),
            RaceOrSpeciesProfile = EmptyRaceOrSpeciesProfile(),
            BodyProfile = EmptyBodyProfile(),
            KnowledgeProfile = EmptyKnowledgeProfile(),
            ConditionProfile = EmptyConditionProfile(),
            InventoryProfile = EmptyInventoryProfile(),
            ReputationProfile = EmptyReputationProfile(),
            HoldingsProfile = EmptyHoldingsProfile(),
            CompanionProfile = EmptyCompanionProfile(),
            SchemaVersion = 1
        };
    }
}

// Read-only ProfileService skeleton.
// It is not a source of truth yet; legacy Character remains authoritative while feature flags are disabled.
public sealed class CharacterProfileService : ICharacterProfileService
{
    private readonly MongoContext _mongo;
    private readonly IServerLogger _logger;
    private readonly ICharacterAttributeProfileFactory _attributeProfileFactory;
    private readonly ICharacterWalletProfileFactory _walletProfileFactory;
    private readonly ICharacterSkillProfileFactory _skillProfileFactory;
    private readonly ICharacterDevelopmentProfileFactory _developmentProfileFactory;
    private readonly ICharacterInventoryProfileFactory _inventoryProfileFactory;
    private readonly IRaceOrSpeciesProfileShadowBuilder _raceOrSpeciesProfileBuilder;
    private readonly IBodyProfileShadowBuilder _bodyProfileBuilder;
    private readonly ICharacterReputationProfileFactory _reputationProfileFactory;
    private readonly ICharacterHoldingsProfileFactory _holdingsProfileFactory;
    private readonly ICharacterCompanionProfileFactory _companionProfileFactory;

    public CharacterProfileService(MongoContext mongo, IServerLogger logger, ICharacterAttributeProfileFactory attributeProfileFactory, ICharacterWalletProfileFactory walletProfileFactory, ICharacterSkillProfileFactory skillProfileFactory, ICharacterDevelopmentProfileFactory developmentProfileFactory, ICharacterInventoryProfileFactory inventoryProfileFactory, IRaceOrSpeciesProfileShadowBuilder raceOrSpeciesProfileBuilder, IBodyProfileShadowBuilder bodyProfileBuilder, ICharacterReputationProfileFactory reputationProfileFactory, ICharacterHoldingsProfileFactory holdingsProfileFactory, ICharacterCompanionProfileFactory companionProfileFactory)
    {
        _mongo = mongo;
        _logger = logger;
        _attributeProfileFactory = attributeProfileFactory;
        _walletProfileFactory = walletProfileFactory;
        _skillProfileFactory = skillProfileFactory;
        _developmentProfileFactory = developmentProfileFactory;
        _inventoryProfileFactory = inventoryProfileFactory;
        _raceOrSpeciesProfileBuilder = raceOrSpeciesProfileBuilder;
        _bodyProfileBuilder = bodyProfileBuilder;
        _reputationProfileFactory = reputationProfileFactory;
        _holdingsProfileFactory = holdingsProfileFactory;
        _companionProfileFactory = companionProfileFactory;
    }

    public AttributeProfile GetAttributeProfile(string characterId)
    {
        var doc = _mongo.CharacterAttributeProfiles.Find(Builders<CharacterAttributeProfileDocument>.Filter.Eq(x => x.CharacterId, characterId)).FirstOrDefault();
        return doc?.Profile ?? CharacterProfileDefaults.EmptyAttributeProfile();
    }

    public SkillProfile GetSkillProfile(string characterId)
    {
        var doc = _mongo.CharacterSkillProfiles.Find(Builders<CharacterSkillProfileDocument>.Filter.Eq(x => x.CharacterId, characterId)).FirstOrDefault();
        return doc?.Profile ?? CharacterProfileDefaults.EmptySkillProfile();
    }

    public DevelopmentProfile GetDevelopmentProfile(string characterId)
    {
        var doc = _mongo.CharacterDevelopmentProfiles.Find(Builders<CharacterDevelopmentProfileDocument>.Filter.Eq(x => x.CharacterId, characterId)).FirstOrDefault();
        return doc?.Profile ?? CharacterProfileDefaults.EmptyDevelopmentProfile();
    }

    public WalletProfile GetWalletProfile(string characterId)
    {
        var doc = _mongo.CharacterWalletProfiles.Find(Builders<CharacterWalletProfileDocument>.Filter.Eq(x => x.CharacterId, characterId)).FirstOrDefault();
        return doc?.Profile ?? CharacterProfileDefaults.EmptyWalletProfile();
    }

    public RaceOrSpeciesProfile GetRaceOrSpeciesProfile(string characterId)
    {
        var doc = _mongo.CharacterRaceOrSpeciesProfiles.Find(Builders<CharacterRaceOrSpeciesProfileDocument>.Filter.Eq(x => x.CharacterId, characterId)).FirstOrDefault();
        return doc?.Profile ?? CharacterProfileDefaults.EmptyRaceOrSpeciesProfile();
    }

    public BodyProfile GetBodyProfile(string characterId)
    {
        var doc = _mongo.CharacterBodyProfiles.Find(Builders<CharacterBodyProfileDocument>.Filter.Eq(x => x.CharacterId, characterId)).FirstOrDefault();
        return doc?.Profile ?? CharacterProfileDefaults.EmptyBodyProfile();
    }

    public KnowledgeProfile GetKnowledgeProfile(string characterId)
    {
        var doc = _mongo.CharacterKnowledgeProfiles.Find(Builders<CharacterKnowledgeProfileDocument>.Filter.Eq(x => x.CharacterId, characterId)).FirstOrDefault();
        return doc?.Profile ?? CharacterProfileDefaults.EmptyKnowledgeProfile();
    }

    public ConditionProfile GetConditionProfile(string characterId)
    {
        var doc = _mongo.CharacterConditionProfiles.Find(Builders<CharacterConditionProfileDocument>.Filter.Eq(x => x.CharacterId, characterId)).FirstOrDefault();
        return doc?.Profile ?? CharacterProfileDefaults.EmptyConditionProfile();
    }

    public InventoryProfile GetInventoryProfile(string characterId)
    {
        var doc = _mongo.CharacterInventoryProfiles.Find(Builders<CharacterInventoryProfileDocument>.Filter.Eq(x => x.CharacterId, characterId)).FirstOrDefault();
        if (doc == null)
        {
            _logger.Debug($"inventory.profile.read characterId={characterId} source=empty_profile reason=missing_persisted");
            return _inventoryProfileFactory.BuildEmpty(characterId, RuleSetIds.FantasyNriDefault);
        }

        if (!TryGetValidInventoryProfile(doc, characterId, out var profile, out var invalidReason))
        {
            _logger.Debug($"inventory.profile.invalid characterId={characterId} reason={invalidReason}");
            _logger.Debug($"inventory.profile.read characterId={characterId} source=empty_profile reason=invalid_persisted");
            return _inventoryProfileFactory.BuildEmpty(characterId, RuleSetIds.FantasyNriDefault);
        }

        _logger.Debug($"inventory.profile.read characterId={characterId} source=persisted");
        return profile;
    }

    public ReputationProfile GetReputationProfile(string characterId)
    {
        var doc = _mongo.CharacterReputationProfiles.Find(Builders<CharacterReputationProfileDocument>.Filter.Eq(x => x.CharacterId, characterId)).FirstOrDefault();
        return doc?.Profile ?? new ReputationProfile { CharacterId = characterId ?? string.Empty, RuleSetId = RuleSetIds.FantasyNriDefault };
    }

    public HoldingsProfile GetHoldingsProfile(string characterId)
    {
        var doc = _mongo.CharacterHoldingsProfiles.Find(Builders<CharacterHoldingsProfileDocument>.Filter.Eq(x => x.CharacterId, characterId)).FirstOrDefault();
        return doc?.Profile ?? new HoldingsProfile { CharacterId = characterId ?? string.Empty, RuleSetId = RuleSetIds.FantasyNriDefault };
    }

    public CompanionProfile GetCompanionProfile(string characterId)
    {
        var doc = _mongo.CharacterCompanionProfiles.Find(Builders<CharacterCompanionProfileDocument>.Filter.Eq(x => x.CharacterId, characterId)).FirstOrDefault();
        return doc?.Profile ?? new CompanionProfile { CharacterId = characterId ?? string.Empty, RuleSetId = RuleSetIds.FantasyNriDefault };
    }

    public CharacterModuleState GetEnabledModules(string characterId)
    {
        var doc = _mongo.CharacterModuleStates.Find(Builders<CharacterModuleStateDocument>.Filter.Eq(x => x.CharacterId, characterId)).FirstOrDefault();
        return doc?.ModuleState ?? new CharacterModuleState { RuleSetCode = RuleSetIds.FantasyNriDefault, Modules = new Dictionary<string, bool>() };
    }

    public CharacterProfileBundle GetProfileBundle(string characterId)
    {
        var moduleState = GetEnabledModules(characterId);
        var bundle = CharacterProfileDefaults.EmptyBundle(characterId);
        bundle.RuleSetId = string.IsNullOrWhiteSpace(moduleState.RuleSetCode) ? RuleSetIds.FantasyNriDefault : moduleState.RuleSetCode;
        bundle.Modules = moduleState.Modules ?? new Dictionary<string, bool>();
        bundle.AttributeProfile = GetAttributeProfile(characterId);
        bundle.SkillProfile = GetSkillProfile(characterId);
        bundle.DevelopmentProfile = GetDevelopmentProfile(characterId);
        bundle.WalletProfile = GetWalletProfile(characterId);
        bundle.RaceOrSpeciesProfile = GetRaceOrSpeciesProfile(characterId);
        bundle.BodyProfile = GetBodyProfile(characterId);
        bundle.KnowledgeProfile = GetKnowledgeProfile(characterId);
        bundle.ConditionProfile = GetConditionProfile(characterId);
        bundle.InventoryProfile = GetInventoryProfile(characterId);
        bundle.ReputationProfile = GetReputationProfile(characterId);
        bundle.HoldingsProfile = GetHoldingsProfile(characterId);
        bundle.CompanionProfile = GetCompanionProfile(characterId);
        return bundle;
    }

    public AttributeProfile GetAttributeProfileShadow(string characterId)
    {
        var character = _mongo.Characters.Find(Builders<Character>.Filter.Eq(x => x.Id, characterId)).FirstOrDefault();
        if (character == null)
        {
            return _attributeProfileFactory.BuildEmpty(characterId, RuleSetIds.FantasyNriDefault);
        }

        var profile = _attributeProfileFactory.BuildFromLegacyCharacter(character);
        _logger.Debug($"attribute.shadow.build characterId={profile.CharacterId} ruleSetId={profile.RuleSetId} valuesCount={profile.Values.Count}");
        return profile;
    }

    public AttributeProfileComparisonResult CompareAttributeProfileShadow(string characterId)
    {
        var character = _mongo.Characters.Find(Builders<Character>.Filter.Eq(x => x.Id, characterId)).FirstOrDefault();
        if (character == null)
        {
            return new AttributeProfileComparisonResult { CharacterId = characterId ?? string.Empty, IsEquivalent = true, ComparedAtUtc = System.DateTime.UtcNow };
        }

        var persisted = GetAttributeProfile(characterId);
        var comparison = _attributeProfileFactory.CompareLegacyToProfile(character, persisted);
        _logger.Debug($"attribute.shadow.compare characterId={comparison.CharacterId} equivalent={comparison.IsEquivalent} diffCount={comparison.Differences.Count}");
        foreach (var diff in comparison.Differences)
        {
            _logger.Debug($"attribute.shadow.diff characterId={comparison.CharacterId} diff={diff}");
        }

        return comparison;
    }

    public WalletProfile GetWalletProfileShadow(string characterId)
    {
        var character = _mongo.Characters.Find(Builders<Character>.Filter.Eq(x => x.Id, characterId)).FirstOrDefault();
        if (character == null)
        {
            return _walletProfileFactory.BuildEmpty(characterId, RuleSetIds.FantasyNriDefault);
        }

        var profile = _walletProfileFactory.BuildFromLegacyCharacter(character);
        _logger.Debug($"wallet.shadow.build characterId={profile.CharacterId} ruleSetId={profile.RuleSetId} walletsCount={profile.Wallets.Count}");
        return profile;
    }

    public WalletProfileComparisonResult CompareWalletProfileShadow(string characterId)
    {
        var character = _mongo.Characters.Find(Builders<Character>.Filter.Eq(x => x.Id, characterId)).FirstOrDefault();
        if (character == null)
        {
            return new WalletProfileComparisonResult { CharacterId = characterId ?? string.Empty, IsEquivalent = true, ComparedAtUtc = System.DateTime.UtcNow };
        }

        var persisted = GetWalletProfile(characterId);
        var comparison = _walletProfileFactory.CompareLegacyToProfile(character, persisted);
        _logger.Debug($"wallet.shadow.compare characterId={comparison.CharacterId} equivalent={comparison.IsEquivalent} diffCount={comparison.Differences.Count}");
        if (comparison.Differences.Count > 0)
        {
            _logger.Debug($"wallet.shadow.diff characterId={comparison.CharacterId} diffCount={comparison.Differences.Count}");
        }

        return comparison;
    }

    public SkillProfile GetSkillProfileShadow(string characterId)
    {
        var character = _mongo.Characters.Find(Builders<Character>.Filter.Eq(x => x.Id, characterId)).FirstOrDefault();
        if (character == null)
        {
            return _skillProfileFactory.BuildEmpty(characterId, RuleSetIds.FantasyNriDefault);
        }

        var profile = _skillProfileFactory.BuildFromLegacyCharacter(character);
        _logger.Debug($"skill.shadow.build characterId={profile.CharacterId} ruleSetId={profile.RuleSetId} count={profile.Skills.Count}");
        return profile;
    }

    public SkillProfileComparisonResult CompareSkillProfileShadow(string characterId)
    {
        var character = _mongo.Characters.Find(Builders<Character>.Filter.Eq(x => x.Id, characterId)).FirstOrDefault();
        if (character == null)
        {
            return new SkillProfileComparisonResult { CharacterId = characterId ?? string.Empty, IsEquivalent = true, ComparedAtUtc = System.DateTime.UtcNow };
        }

        var persisted = GetSkillProfile(characterId);
        var comparison = _skillProfileFactory.CompareLegacyToProfile(character, persisted);
        _logger.Debug($"skill.shadow.compare characterId={comparison.CharacterId} equivalent={comparison.IsEquivalent} diffCount={comparison.Differences.Count}");
        if (comparison.Differences.Count > 0)
        {
            _logger.Debug($"skill.shadow.diff characterId={comparison.CharacterId} diffCount={comparison.Differences.Count}");
        }

        return comparison;
    }

    public DevelopmentProfile GetDevelopmentProfileShadow(string characterId)
    {
        var character = _mongo.Characters.Find(Builders<Character>.Filter.Eq(x => x.Id, characterId)).FirstOrDefault();
        if (character == null)
        {
            return _developmentProfileFactory.BuildEmpty(characterId, RuleSetIds.FantasyNriDefault);
        }

        var profile = _developmentProfileFactory.BuildFromLegacyCharacter(character);
        _logger.Debug($"development.shadow.build characterId={profile.CharacterId} ruleSetId={profile.RuleSetId} count={profile.Nodes.Count}");
        return profile;
    }

    public DevelopmentProfileComparisonResult CompareDevelopmentProfileShadow(string characterId)
    {
        var character = _mongo.Characters.Find(Builders<Character>.Filter.Eq(x => x.Id, characterId)).FirstOrDefault();
        if (character == null)
        {
            return new DevelopmentProfileComparisonResult { CharacterId = characterId ?? string.Empty, IsEquivalent = true, ComparedAtUtc = System.DateTime.UtcNow };
        }

        var persisted = GetDevelopmentProfile(characterId);
        var comparison = _developmentProfileFactory.CompareLegacyToProfile(character, persisted);
        _logger.Debug($"development.shadow.compare characterId={comparison.CharacterId} equivalent={comparison.IsEquivalent} diffCount={comparison.Differences.Count}");
        if (comparison.Differences.Count > 0)
        {
            _logger.Debug($"development.shadow.diff characterId={comparison.CharacterId} diffCount={comparison.Differences.Count}");
        }

        return comparison;
    }

    public InventoryProfile GetInventoryProfileShadow(string characterId)
    {
        var character = _mongo.Characters.Find(Builders<Character>.Filter.Eq(x => x.Id, characterId)).FirstOrDefault();
        if (character == null)
        {
            return _inventoryProfileFactory.BuildEmpty(characterId, RuleSetIds.FantasyNriDefault);
        }

        var profile = _inventoryProfileFactory.BuildFromLegacyCharacter(character);
        _logger.Debug($"inventory.shadow.build characterId={profile.CharacterId} ruleSetId={profile.RuleSetId} count={profile.Items.Count}");
        return profile;
    }

    public InventoryProfileComparisonResult CompareInventoryProfileShadow(string characterId)
    {
        var character = _mongo.Characters.Find(Builders<Character>.Filter.Eq(x => x.Id, characterId)).FirstOrDefault();
        if (character == null)
        {
            return new InventoryProfileComparisonResult { CharacterId = characterId ?? string.Empty, IsEquivalent = true, ComparedAtUtc = System.DateTime.UtcNow };
        }

        var doc = _mongo.CharacterInventoryProfiles.Find(Builders<CharacterInventoryProfileDocument>.Filter.Eq(x => x.CharacterId, characterId)).FirstOrDefault();
        if (doc == null)
        {
            return new InventoryProfileComparisonResult
            {
                CharacterId = characterId ?? string.Empty,
                IsEquivalent = false,
                Differences = new List<string> { "persisted.missing" },
                ComparedAtUtc = System.DateTime.UtcNow
            };
        }

        if (!TryGetValidInventoryProfile(doc, characterId, out var persisted, out var invalidReason))
        {
            _logger.Debug($"inventory.profile.invalid characterId={characterId} reason={invalidReason}");
            return new InventoryProfileComparisonResult
            {
                CharacterId = characterId ?? string.Empty,
                IsEquivalent = false,
                Differences = new List<string> { $"persisted.invalid:{invalidReason}" },
                ComparedAtUtc = System.DateTime.UtcNow
            };
        }

        var comparison = _inventoryProfileFactory.CompareLegacyToProfile(character, persisted);
        _logger.Debug($"inventory.shadow.compare characterId={comparison.CharacterId} equivalent={comparison.IsEquivalent} diffCount={comparison.Differences.Count}");
        if (comparison.Differences.Count > 0)
        {
            _logger.Debug($"inventory.shadow.diff characterId={comparison.CharacterId} diffCount={comparison.Differences.Count}");
        }

        return comparison;
    }

    private static bool TryGetValidInventoryProfile(CharacterInventoryProfileDocument doc, string characterId, out InventoryProfile profile, out string reason)
    {
        profile = doc?.Profile ?? new InventoryProfile();
        reason = string.Empty;

        if (doc == null)
        {
            reason = "missing_document";
            return false;
        }

        if (doc.Profile == null)
        {
            reason = "profile_null";
            return false;
        }

        if (doc.Profile.SchemaVersion < 1)
        {
            reason = "schema_version_invalid";
            return false;
        }

        if (!string.Equals(doc.Profile.CharacterId, characterId, StringComparison.Ordinal))
        {
            reason = "character_id_mismatch";
            return false;
        }

        if (doc.Profile.Items == null)
        {
            reason = "items_null";
            return false;
        }

        return true;
    }

    public RaceOrSpeciesProfile GetRaceOrSpeciesProfileShadow(string characterId)
    {
        var character = _mongo.Characters.Find(Builders<Character>.Filter.Eq(x => x.Id, characterId)).FirstOrDefault();
        if (character == null)
        {
            return _raceOrSpeciesProfileBuilder.BuildEmpty(characterId, RuleSetIds.FantasyNriDefault);
        }

        var profile = _raceOrSpeciesProfileBuilder.BuildFromLegacyCharacter(character);
        _logger.Debug($"race.shadow.build characterId={profile.CharacterId} ruleSetId={profile.RuleSetId}");
        return profile;
    }

    public RaceOrSpeciesProfileComparisonResult CompareRaceOrSpeciesProfileShadow(string characterId)
    {
        var character = _mongo.Characters.Find(Builders<Character>.Filter.Eq(x => x.Id, characterId)).FirstOrDefault();
        if (character == null)
        {
            return new RaceOrSpeciesProfileComparisonResult { CharacterId = characterId ?? string.Empty, IsEquivalent = true, ComparedAtUtc = System.DateTime.UtcNow };
        }

        var comparison = _raceOrSpeciesProfileBuilder.CompareLegacyToProfile(character, GetRaceOrSpeciesProfile(characterId));
        _logger.Debug($"race.shadow.compare characterId={comparison.CharacterId} equivalent={comparison.IsEquivalent} diffCount={comparison.Differences.Count}");
        return comparison;
    }

    public BodyProfile GetBodyProfileShadow(string characterId)
    {
        var character = _mongo.Characters.Find(Builders<Character>.Filter.Eq(x => x.Id, characterId)).FirstOrDefault();
        if (character == null)
        {
            return _bodyProfileBuilder.BuildEmpty(characterId, RuleSetIds.FantasyNriDefault);
        }

        var profile = _bodyProfileBuilder.BuildFromLegacyCharacter(character);
        _logger.Debug($"body.shadow.build characterId={profile.CharacterId} ruleSetId={profile.RuleSetId}");
        return profile;
    }

    public BodyProfileComparisonResult CompareBodyProfileShadow(string characterId)
    {
        var character = _mongo.Characters.Find(Builders<Character>.Filter.Eq(x => x.Id, characterId)).FirstOrDefault();
        if (character == null)
        {
            return new BodyProfileComparisonResult { CharacterId = characterId ?? string.Empty, IsEquivalent = true, ComparedAtUtc = System.DateTime.UtcNow };
        }

        var comparison = _bodyProfileBuilder.CompareLegacyToProfile(character, GetBodyProfile(characterId));
        _logger.Debug($"body.shadow.compare characterId={comparison.CharacterId} equivalent={comparison.IsEquivalent} diffCount={comparison.Differences.Count}");
        return comparison;
    }

    public ReputationProfile GetReputationProfileShadow(string characterId)
    {
        var c = _mongo.Characters.Find(Builders<Character>.Filter.Eq(x => x.Id, characterId)).FirstOrDefault();
        if (c == null) return _reputationProfileFactory.BuildEmpty(characterId, RuleSetIds.FantasyNriDefault);
        var p = _reputationProfileFactory.BuildFromLegacyCharacter(c);
        _logger.Debug($"reputation.shadow.build characterId={p.CharacterId} ruleSetId={p.RuleSetId} count={p.Entries.Count}");
        return p;
    }

    public ReputationProfileComparisonResult CompareReputationProfileShadow(string characterId)
    {
        var c = _mongo.Characters.Find(Builders<Character>.Filter.Eq(x => x.Id, characterId)).FirstOrDefault();
        if (c == null) return new ReputationProfileComparisonResult { CharacterId = characterId ?? string.Empty, IsEquivalent = true, ComparedAtUtc = System.DateTime.UtcNow };
        var cmp = _reputationProfileFactory.CompareLegacyToProfile(c, GetReputationProfile(characterId));
        _logger.Debug($"reputation.shadow.compare characterId={cmp.CharacterId} equivalent={cmp.IsEquivalent} diffCount={cmp.Differences.Count}");
        return cmp;
    }

    public HoldingsProfile GetHoldingsProfileShadow(string characterId)
    {
        var c = _mongo.Characters.Find(Builders<Character>.Filter.Eq(x => x.Id, characterId)).FirstOrDefault();
        if (c == null) return _holdingsProfileFactory.BuildEmpty(characterId, RuleSetIds.FantasyNriDefault);
        var p = _holdingsProfileFactory.BuildFromLegacyCharacter(c);
        _logger.Debug($"holdings.shadow.build characterId={p.CharacterId} ruleSetId={p.RuleSetId} count={p.Holdings.Count}");
        return p;
    }

    public HoldingsProfileComparisonResult CompareHoldingsProfileShadow(string characterId)
    {
        var c = _mongo.Characters.Find(Builders<Character>.Filter.Eq(x => x.Id, characterId)).FirstOrDefault();
        if (c == null) return new HoldingsProfileComparisonResult { CharacterId = characterId ?? string.Empty, IsEquivalent = true, ComparedAtUtc = System.DateTime.UtcNow };
        var cmp = _holdingsProfileFactory.CompareLegacyToProfile(c, GetHoldingsProfile(characterId));
        _logger.Debug($"holdings.shadow.compare characterId={cmp.CharacterId} equivalent={cmp.IsEquivalent} diffCount={cmp.Differences.Count}");
        return cmp;
    }

    public CompanionProfile GetCompanionProfileShadow(string characterId)
    {
        var c = _mongo.Characters.Find(Builders<Character>.Filter.Eq(x => x.Id, characterId)).FirstOrDefault();
        if (c == null) return _companionProfileFactory.BuildEmpty(characterId, RuleSetIds.FantasyNriDefault);
        var p = _companionProfileFactory.BuildFromLegacyCharacter(c);
        _logger.Debug($"companion.shadow.build characterId={p.CharacterId} ruleSetId={p.RuleSetId} count={p.Companions.Count}");
        return p;
    }

    public CompanionProfileComparisonResult CompareCompanionProfileShadow(string characterId)
    {
        var c = _mongo.Characters.Find(Builders<Character>.Filter.Eq(x => x.Id, characterId)).FirstOrDefault();
        if (c == null) return new CompanionProfileComparisonResult { CharacterId = characterId ?? string.Empty, IsEquivalent = true, ComparedAtUtc = System.DateTime.UtcNow };
        var cmp = _companionProfileFactory.CompareLegacyToProfile(c, GetCompanionProfile(characterId));
        _logger.Debug($"companion.shadow.compare characterId={cmp.CharacterId} equivalent={cmp.IsEquivalent} diffCount={cmp.Differences.Count}");
        return cmp;
    }

}
