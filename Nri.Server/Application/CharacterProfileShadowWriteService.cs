using System;
using System.Threading.Tasks;
using MongoDB.Driver;
using Nri.Server.Infrastructure;
using Nri.Server.Logging;
using Nri.Shared.Domain;

namespace Nri.Server.Application;

public sealed class ShadowWriteResult
{
    public string ProfileType { get; set; } = string.Empty;
    public string CharacterId { get; set; } = string.Empty;
    public bool Skipped { get; set; }
    public bool Success { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public DateTime WrittenAtUtc { get; set; } = DateTime.UtcNow;
}

public interface ICharacterProfileShadowWriteService
{
    Task<ShadowWriteResult> WriteAttributeProfileShadowAsync(Character character, string actorUserId, string requestId);
    Task<ShadowWriteResult> WriteWalletProfileShadowAsync(Character character, string actorUserId, string requestId);
    Task<ShadowWriteResult> WriteSkillProfileShadowAsync(Character character, string actorUserId, string requestId);
    Task<ShadowWriteResult> WriteDevelopmentProfileShadowAsync(Character character, string actorUserId, string requestId);
    Task<ShadowWriteResult> WriteInventoryProfileShadowAsync(Character character, string actorUserId, string requestId);
    Task<ShadowWriteResult> WriteRaceOrSpeciesProfileShadowAsync(Character character, string actorUserId, string requestId);
    Task<ShadowWriteResult> WriteBodyProfileShadowAsync(Character character, string actorUserId, string requestId);
}

public sealed class CharacterProfileShadowWriteService : ICharacterProfileShadowWriteService
{
    private readonly MongoContext _mongo;
    private readonly IServerLogger _logger;
    private readonly ICharacterAttributeProfileFactory _attributeFactory;
    private readonly ICharacterWalletProfileFactory _walletFactory;
    private readonly ICharacterSkillProfileFactory _skillFactory;
    private readonly ICharacterDevelopmentProfileFactory _developmentFactory;
    private readonly ICharacterInventoryProfileFactory _inventoryFactory;
    private readonly IRaceOrSpeciesProfileShadowBuilder _raceOrSpeciesBuilder;
    private readonly IBodyProfileShadowBuilder _bodyBuilder;
    private readonly ICharacterProfileConsistencyService _consistencyService;

    public CharacterProfileShadowWriteService(MongoContext mongo, IServerLogger logger, ICharacterAttributeProfileFactory attributeFactory, ICharacterWalletProfileFactory walletFactory, ICharacterSkillProfileFactory skillFactory, ICharacterDevelopmentProfileFactory developmentFactory, ICharacterInventoryProfileFactory inventoryFactory, IRaceOrSpeciesProfileShadowBuilder raceOrSpeciesBuilder, IBodyProfileShadowBuilder bodyBuilder, ICharacterProfileConsistencyService consistencyService)
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
        _consistencyService = consistencyService;
    }

    public Task<ShadowWriteResult> WriteAttributeProfileShadowAsync(Character character, string actorUserId, string requestId) =>
        TryShadowWriteAsync("attribute", character, ProfileFeatureFlags.UseAttributeProfileShadowWrite, () =>
        {
            var profile = _attributeFactory.BuildFromLegacyCharacter(character);
            UpsertByCharacterId(_mongo.CharacterAttributeProfiles, character.Id, new CharacterAttributeProfileDocument { CharacterId = character.Id, Profile = profile });
        }, actorUserId, requestId);

    public Task<ShadowWriteResult> WriteWalletProfileShadowAsync(Character character, string actorUserId, string requestId) =>
        TryShadowWriteAsync("wallet", character, ProfileFeatureFlags.UseWalletProfileShadowWrite, () =>
        {
            var profile = _walletFactory.BuildFromLegacyCharacter(character);
            UpsertByCharacterId(_mongo.CharacterWalletProfiles, character.Id, new CharacterWalletProfileDocument { CharacterId = character.Id, Profile = profile });
        }, actorUserId, requestId);

    public Task<ShadowWriteResult> WriteSkillProfileShadowAsync(Character character, string actorUserId, string requestId) =>
        TryShadowWriteAsync("skill", character, ProfileFeatureFlags.UseSkillProfileShadowWrite, () =>
        {
            var profile = _skillFactory.BuildFromLegacyCharacter(character);
            UpsertByCharacterId(_mongo.CharacterSkillProfiles, character.Id, new CharacterSkillProfileDocument { CharacterId = character.Id, Profile = profile });
        }, actorUserId, requestId);

    public Task<ShadowWriteResult> WriteDevelopmentProfileShadowAsync(Character character, string actorUserId, string requestId) =>
        TryShadowWriteAsync("development", character, ProfileFeatureFlags.UseDevelopmentProfileShadowWrite, () =>
        {
            var profile = _developmentFactory.BuildFromLegacyCharacter(character);
            UpsertByCharacterId(_mongo.CharacterDevelopmentProfiles, character.Id, new CharacterDevelopmentProfileDocument { CharacterId = character.Id, Profile = profile });
        }, actorUserId, requestId);

    public Task<ShadowWriteResult> WriteInventoryProfileShadowAsync(Character character, string actorUserId, string requestId) =>
        TryShadowWriteAsync("inventory", character, ProfileFeatureFlags.UseInventoryProfileShadowWrite, () =>
        {
            var profile = _inventoryFactory.BuildFromLegacyCharacter(character);
            UpsertByCharacterId(_mongo.CharacterInventoryProfiles, character.Id, new CharacterInventoryProfileDocument { CharacterId = character.Id, Profile = profile });
        }, actorUserId, requestId);

    public Task<ShadowWriteResult> WriteRaceOrSpeciesProfileShadowAsync(Character character, string actorUserId, string requestId) =>
        TryShadowWriteAsync("raceOrSpecies", character, ProfileFeatureFlags.UseRaceOrSpeciesProfileShadowWrite, () =>
        {
            var profile = _raceOrSpeciesBuilder.BuildFromLegacyCharacter(character);
            UpsertByCharacterId(_mongo.CharacterRaceOrSpeciesProfiles, character.Id, new CharacterRaceOrSpeciesProfileDocument { CharacterId = character.Id, Profile = profile });
        }, actorUserId, requestId);

    public Task<ShadowWriteResult> WriteBodyProfileShadowAsync(Character character, string actorUserId, string requestId) =>
        TryShadowWriteAsync("body", character, ProfileFeatureFlags.UseBodyProfileShadowWrite, () =>
        {
            var profile = _bodyBuilder.BuildFromLegacyCharacter(character);
            UpsertByCharacterId(_mongo.CharacterBodyProfiles, character.Id, new CharacterBodyProfileDocument { CharacterId = character.Id, Profile = profile });
        }, actorUserId, requestId);

    private Task<ShadowWriteResult> TryShadowWriteAsync(string profileType, Character character, bool profileFlagEnabled, Action writeAction, string actorUserId, string requestId)
    {
        var characterId = character?.Id ?? string.Empty;
        if (!ProfileFeatureFlags.UseRuleSetProfilesWriteShadow || !profileFlagEnabled)
        {
            _logger.Debug($"profile.shadow.write.skipped profile={profileType} characterId={characterId} reason=flag_disabled");
            return Task.FromResult(new ShadowWriteResult { ProfileType = profileType, CharacterId = characterId, Skipped = true, Success = true, WrittenAtUtc = DateTime.UtcNow });
        }

        try
        {
            _logger.Debug($"profile.shadow.write.start profile={profileType} characterId={characterId}");
            writeAction();
            _logger.Debug($"profile.shadow.write.done profile={profileType} characterId={characterId}");
            if (ProfileFeatureFlags.UseRuleSetProfilesWriteShadow && ProfileFeatureFlags.UseCharacterProfileConsistencyVerification)
            {
                _ = _consistencyService.VerifyCharacterAsync(characterId, actorUserId, requestId).GetAwaiter().GetResult();
            }
            // TODO(F0.5.11): evaluate safe sync event publication for profile shadow writes.
            return Task.FromResult(new ShadowWriteResult { ProfileType = profileType, CharacterId = characterId, Success = true, WrittenAtUtc = DateTime.UtcNow });
        }
        catch (Exception ex)
        {
            _logger.Debug($"profile.shadow.write.error profile={profileType} characterId={characterId} message={ex.Message}");
            return Task.FromResult(new ShadowWriteResult { ProfileType = profileType, CharacterId = characterId, Success = false, ErrorMessage = ex.Message, WrittenAtUtc = DateTime.UtcNow });
        }
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
        collection.ReplaceOne(Builders<TDoc>.Filter.Eq("CharacterId", characterId), doc, new ReplaceOptions { IsUpsert = true });
    }
}
