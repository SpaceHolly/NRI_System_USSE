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
    BodyProfile GetBodyProfile(string characterId);
    KnowledgeProfile GetKnowledgeProfile(string characterId);
    ConditionProfile GetConditionProfile(string characterId);
    CharacterModuleState GetEnabledModules(string characterId);
    CharacterProfileBundle GetProfileBundle(string characterId);
    AttributeProfile GetAttributeProfileShadow(string characterId);
    AttributeProfileComparisonResult CompareAttributeProfileShadow(string characterId);
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
    public BodyProfile BodyProfile { get; set; } = new BodyProfile();
    public KnowledgeProfile KnowledgeProfile { get; set; } = new KnowledgeProfile();
    public ConditionProfile ConditionProfile { get; set; } = new ConditionProfile();
    public int SchemaVersion { get; set; } = 1;
}

public static class CharacterProfileDefaults
{
    // Empty defaults are safe read fallbacks and must not be auto-persisted.
    public static AttributeProfile EmptyAttributeProfile() => new AttributeProfile();
    public static SkillProfile EmptySkillProfile() => new SkillProfile();
    public static DevelopmentProfile EmptyDevelopmentProfile() => new DevelopmentProfile();
    public static WalletProfile EmptyWalletProfile() => new WalletProfile();
    public static BodyProfile EmptyBodyProfile() => new BodyProfile();
    public static KnowledgeProfile EmptyKnowledgeProfile() => new KnowledgeProfile();
    public static ConditionProfile EmptyConditionProfile() => new ConditionProfile();

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
            BodyProfile = EmptyBodyProfile(),
            KnowledgeProfile = EmptyKnowledgeProfile(),
            ConditionProfile = EmptyConditionProfile(),
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

    public CharacterProfileService(MongoContext mongo, IServerLogger logger, ICharacterAttributeProfileFactory attributeProfileFactory)
    {
        _mongo = mongo;
        _logger = logger;
        _attributeProfileFactory = attributeProfileFactory;
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
        bundle.BodyProfile = GetBodyProfile(characterId);
        bundle.KnowledgeProfile = GetKnowledgeProfile(characterId);
        bundle.ConditionProfile = GetConditionProfile(characterId);
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
}
