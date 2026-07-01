using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Driver;
using Nri.Server.Infrastructure;
using Nri.Server.Logging;
using Nri.Shared.Domain;

namespace Nri.Server.Application;

public sealed class ProfileFirstCharacterCreationResult
{
    public string CharacterId { get; set; } = string.Empty;
    public bool UsedProfileFirst { get; set; }
    public bool Success { get; set; }
    public List<string> CreatedProfiles { get; set; } = new List<string>();
    public List<string> MissingProfiles { get; set; } = new List<string>();
    public string ErrorMessage { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public ProfileFirstCharacterCreationDiagnosticResult Diagnostic { get; set; } = new ProfileFirstCharacterCreationDiagnosticResult();
}

public static class ProfileFirstCreationFailurePolicy
{
    public const string KeepLegacyAndReport = "keep_legacy_and_report";
    public const string ArchiveLegacyOnProfileFailure = "archive_legacy_on_profile_failure";
    public const string DeleteLegacyOnProfileFailure = "delete_legacy_on_profile_failure";
    public const string Default = KeepLegacyAndReport;
}

public sealed class ProfileFirstCharacterCreationDiagnosticResult
{
    public string CharacterId { get; set; } = string.Empty;
    public bool UsedProfileFirstCreation { get; set; }
    public bool Success { get; set; }
    public bool CreatedLegacyCharacter { get; set; }
    public List<string> CreatedProfiles { get; set; } = new List<string>();
    public List<string> MissingProfiles { get; set; } = new List<string>();
    public bool CleanupAttempted { get; set; }
    public string CleanupPolicy { get; set; } = ProfileFirstCreationFailurePolicy.Default;
    public string CleanupResult { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class ProfileFirstCreationReadinessReport
{
    public bool IsReady { get; set; }
    public List<string> MissingServices { get; set; } = new List<string>();
    public List<string> MissingCollections { get; set; } = new List<string>();
    public List<string> RequiredProfiles { get; set; } = new List<string>();
    public Dictionary<string, bool> FeatureFlags { get; set; } = new Dictionary<string, bool>();
    public List<string> Risks { get; set; } = new List<string>();
    public DateTime CheckedAtUtc { get; set; } = DateTime.UtcNow;
}

public interface ICharacterProfileCreationService
{
    Task<ProfileFirstCharacterCreationResult> CreateProfileBundleForNewCharacterAsync(Character character, string actorUserId, string requestId);
    Task<ProfileFirstCharacterCreationResult> ValidateCreatedProfilesAsync(string characterId);
    Task<ProfileFirstCreationReadinessReport> BuildProfileFirstCreationReadinessReportAsync();
    ProfileFirstCharacterCreationResult BuildCreationResult(string characterId, bool success, List<string> createdProfiles, List<string> missingProfiles, string errorMessage);
}

public sealed class CharacterProfileCreationService : ICharacterProfileCreationService
{
    private static readonly string[] RequiredProfiles =
    {
        "attributes",
        "wallet",
        "skills",
        "development",
        "inventory",
        "raceOrSpecies",
        "body"
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
    private readonly ICharacterProfileConsistencyService _consistencyService;

    public CharacterProfileCreationService(MongoContext mongo, IServerLogger logger, ICharacterAttributeProfileFactory attributeFactory, ICharacterWalletProfileFactory walletFactory, ICharacterSkillProfileFactory skillFactory, ICharacterDevelopmentProfileFactory developmentFactory, ICharacterInventoryProfileFactory inventoryFactory, IRaceOrSpeciesProfileShadowBuilder raceOrSpeciesBuilder, IBodyProfileShadowBuilder bodyBuilder, ICharacterProfileConsistencyService consistencyService)
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

    public Task<ProfileFirstCharacterCreationResult> CreateProfileBundleForNewCharacterAsync(Character character, string actorUserId, string requestId)
    {
        var characterId = character?.Id ?? string.Empty;
        var createdProfiles = new List<string>();
        var policy = GetFailurePolicy();
        _logger.Debug($"profile.create.policy policy={policy}");
        _logger.Debug($"profile.create.start characterId={characterId} requestId={requestId}");

        try
        {
            if (character == null)
            {
                _logger.Debug($"profile.create.error characterId={characterId} message=legacy_character_missing");
                var result = BuildCreationResult(characterId, false, createdProfiles, RequiredProfiles.ToList(), "legacy_character_missing");
                result.Diagnostic.CleanupPolicy = policy;
                return Task.FromResult(result);
            }

            if (string.IsNullOrWhiteSpace(character.Id))
            {
                _logger.Debug($"profile.create.error characterId={characterId} message=character_id_missing");
                var result = BuildCreationResult(characterId, false, createdProfiles, RequiredProfiles.ToList(), "character_id_missing");
                result.Diagnostic.CreatedLegacyCharacter = true;
                result.Diagnostic.CleanupPolicy = policy;
                return Task.FromResult(result);
            }

            WriteProfile(character.Id, "attributes", createdProfiles, () =>
            {
                var profile = _attributeFactory.BuildFromLegacyCharacter(character);
                UpsertByCharacterId(_mongo.CharacterAttributeProfiles, character.Id, new CharacterAttributeProfileDocument { CharacterId = character.Id, Profile = profile });
            });

            WriteProfile(character.Id, "wallet", createdProfiles, () =>
            {
                var profile = _walletFactory.BuildFromLegacyCharacter(character);
                UpsertByCharacterId(_mongo.CharacterWalletProfiles, character.Id, new CharacterWalletProfileDocument { CharacterId = character.Id, Profile = profile });
            });

            WriteProfile(character.Id, "skills", createdProfiles, () =>
            {
                var profile = _skillFactory.BuildFromLegacyCharacter(character);
                UpsertByCharacterId(_mongo.CharacterSkillProfiles, character.Id, new CharacterSkillProfileDocument { CharacterId = character.Id, Profile = profile });
            });

            WriteProfile(character.Id, "development", createdProfiles, () =>
            {
                var profile = _developmentFactory.BuildFromLegacyCharacter(character);
                UpsertByCharacterId(_mongo.CharacterDevelopmentProfiles, character.Id, new CharacterDevelopmentProfileDocument { CharacterId = character.Id, Profile = profile });
            });

            WriteProfile(character.Id, "inventory", createdProfiles, () =>
            {
                var profile = _inventoryFactory.BuildFromLegacyCharacter(character);
                UpsertByCharacterId(_mongo.CharacterInventoryProfiles, character.Id, new CharacterInventoryProfileDocument { CharacterId = character.Id, Profile = profile });
            });

            WriteProfile(character.Id, "raceOrSpecies", createdProfiles, () =>
            {
                var profile = _raceOrSpeciesBuilder.BuildFromLegacyCharacter(character);
                UpsertByCharacterId(_mongo.CharacterRaceOrSpeciesProfiles, character.Id, new CharacterRaceOrSpeciesProfileDocument { CharacterId = character.Id, Profile = profile });
            });

            WriteProfile(character.Id, "body", createdProfiles, () =>
            {
                var profile = _bodyBuilder.BuildFromLegacyCharacter(character);
                UpsertByCharacterId(_mongo.CharacterBodyProfiles, character.Id, new CharacterBodyProfileDocument { CharacterId = character.Id, Profile = profile });
            });

            var validation = ValidateCreatedProfilesAsync(character.Id).GetAwaiter().GetResult();
            if (!validation.Success)
            {
                _logger.Debug($"profile.create.error characterId={character.Id} message=created_profile_validation_failed");
                _logger.Debug($"profile.create.partial_failure characterId={character.Id} writtenProfiles={string.Join(",", createdProfiles)}");
                var result = BuildCreationResult(character.Id, false, createdProfiles, validation.MissingProfiles, "created_profile_validation_failed");
                result.Diagnostic.CreatedLegacyCharacter = true;
                TryCleanupPartialProfileFirstCreationAsync(character.Id, actorUserId, requestId, createdProfiles, result.Diagnostic).GetAwaiter().GetResult();
                return Task.FromResult(result);
            }

            if (IsCharacterProfileConsistencyVerificationEnabled())
            {
                _ = _consistencyService.VerifyCharacterAsync(character.Id, actorUserId, requestId).GetAwaiter().GetResult();
            }

            _logger.Debug($"profile.create.done characterId={character.Id} profiles={string.Join(",", createdProfiles)}");
            var success = BuildCreationResult(character.Id, true, createdProfiles, new List<string>(), string.Empty);
            success.Diagnostic.CreatedLegacyCharacter = true;
            return Task.FromResult(success);
        }
        catch (Exception ex)
        {
            var missingProfiles = RequiredProfiles.Where(x => !createdProfiles.Contains(x, StringComparer.OrdinalIgnoreCase)).ToList();
            _logger.Debug($"profile.create.error characterId={characterId} message={ex.Message}");
            _logger.Debug($"profile.create.partial_failure characterId={characterId} writtenProfiles={string.Join(",", createdProfiles)}");
            var result = BuildCreationResult(characterId, false, createdProfiles, missingProfiles, ex.Message);
            result.Diagnostic.CreatedLegacyCharacter = !string.IsNullOrWhiteSpace(characterId);
            TryCleanupPartialProfileFirstCreationAsync(characterId, actorUserId, requestId, createdProfiles, result.Diagnostic).GetAwaiter().GetResult();
            return Task.FromResult(result);
        }
    }

    public Task<ProfileFirstCharacterCreationResult> ValidateCreatedProfilesAsync(string characterId)
    {
        _logger.Debug($"profile.create.validation.start characterId={characterId}");
        var missing = new List<string>();
        if (!IsValidAttributeProfile(_mongo.CharacterAttributeProfiles.Find(Builders<CharacterAttributeProfileDocument>.Filter.Eq(x => x.CharacterId, characterId)).FirstOrDefault(), characterId)) missing.Add("attributes");
        if (!IsValidWalletProfile(_mongo.CharacterWalletProfiles.Find(Builders<CharacterWalletProfileDocument>.Filter.Eq(x => x.CharacterId, characterId)).FirstOrDefault(), characterId)) missing.Add("wallet");
        if (!IsValidSkillProfile(_mongo.CharacterSkillProfiles.Find(Builders<CharacterSkillProfileDocument>.Filter.Eq(x => x.CharacterId, characterId)).FirstOrDefault(), characterId)) missing.Add("skills");
        if (!IsValidDevelopmentProfile(_mongo.CharacterDevelopmentProfiles.Find(Builders<CharacterDevelopmentProfileDocument>.Filter.Eq(x => x.CharacterId, characterId)).FirstOrDefault(), characterId)) missing.Add("development");
        if (!IsValidInventoryProfile(_mongo.CharacterInventoryProfiles.Find(Builders<CharacterInventoryProfileDocument>.Filter.Eq(x => x.CharacterId, characterId)).FirstOrDefault(), characterId)) missing.Add("inventory");
        if (!IsValidRaceOrSpeciesProfile(_mongo.CharacterRaceOrSpeciesProfiles.Find(Builders<CharacterRaceOrSpeciesProfileDocument>.Filter.Eq(x => x.CharacterId, characterId)).FirstOrDefault(), characterId)) missing.Add("raceOrSpecies");
        if (!IsValidBodyProfile(_mongo.CharacterBodyProfiles.Find(Builders<CharacterBodyProfileDocument>.Filter.Eq(x => x.CharacterId, characterId)).FirstOrDefault(), characterId)) missing.Add("body");

        var valid = missing.Count == 0;
        _logger.Debug($"profile.create.validation.done characterId={characterId} valid={valid}");
        if (!valid)
        {
            _logger.Debug($"profile.create.validation_failed characterId={characterId} missing={string.Join(",", missing)}");
        }

        return Task.FromResult(BuildCreationResult(characterId, valid, RequiredProfiles.Except(missing).ToList(), missing, valid ? string.Empty : "missing_or_invalid_created_profiles"));
    }

    public Task<ProfileFirstCreationReadinessReport> BuildProfileFirstCreationReadinessReportAsync()
    {
        var missingServices = new List<string>();
        if (_mongo == null) missingServices.Add("MongoContext");
        if (_logger == null) missingServices.Add("IServerLogger");
        if (_attributeFactory == null) missingServices.Add("ICharacterAttributeProfileFactory");
        if (_walletFactory == null) missingServices.Add("ICharacterWalletProfileFactory");
        if (_skillFactory == null) missingServices.Add("ICharacterSkillProfileFactory");
        if (_developmentFactory == null) missingServices.Add("ICharacterDevelopmentProfileFactory");
        if (_inventoryFactory == null) missingServices.Add("ICharacterInventoryProfileFactory");
        if (_raceOrSpeciesBuilder == null) missingServices.Add("IRaceOrSpeciesProfileShadowBuilder");
        if (_bodyBuilder == null) missingServices.Add("IBodyProfileShadowBuilder");
        if (_consistencyService == null) missingServices.Add("ICharacterProfileConsistencyService");

        var missingCollections = new List<string>();
        if (_mongo?.CharacterAttributeProfiles == null) missingCollections.Add("character_attribute_profiles");
        if (_mongo?.CharacterWalletProfiles == null) missingCollections.Add("character_wallet_profiles");
        if (_mongo?.CharacterSkillProfiles == null) missingCollections.Add("character_skill_profiles");
        if (_mongo?.CharacterDevelopmentProfiles == null) missingCollections.Add("character_development_profiles");
        if (_mongo?.CharacterInventoryProfiles == null) missingCollections.Add("character_inventory_profiles");
        if (_mongo?.CharacterRaceOrSpeciesProfiles == null) missingCollections.Add("character_race_or_species_profiles");
        if (_mongo?.CharacterBodyProfiles == null) missingCollections.Add("character_body_profiles");

        var risks = new List<string>
        {
            "profile-first creation is disabled by default",
            "profile-first details is disabled by default",
            "cleanup policy defaults to keep_legacy_and_report",
            "delete cleanup policy is intentionally unsupported"
        };

        return Task.FromResult(new ProfileFirstCreationReadinessReport
        {
            IsReady = missingServices.Count == 0 && missingCollections.Count == 0,
            MissingServices = missingServices,
            MissingCollections = missingCollections,
            RequiredProfiles = RequiredProfiles.ToList(),
            FeatureFlags = BuildReadinessFeatureFlags(),
            Risks = risks,
            CheckedAtUtc = DateTime.UtcNow
        });
    }

    public ProfileFirstCharacterCreationResult BuildCreationResult(string characterId, bool success, List<string> createdProfiles, List<string> missingProfiles, string errorMessage)
    {
        return new ProfileFirstCharacterCreationResult
        {
            CharacterId = characterId ?? string.Empty,
            UsedProfileFirst = true,
            Success = success,
            CreatedProfiles = createdProfiles ?? new List<string>(),
            MissingProfiles = missingProfiles ?? new List<string>(),
            ErrorMessage = errorMessage ?? string.Empty,
            CreatedAtUtc = DateTime.UtcNow,
            Diagnostic = new ProfileFirstCharacterCreationDiagnosticResult
            {
                CharacterId = characterId ?? string.Empty,
                UsedProfileFirstCreation = true,
                Success = success,
                CreatedProfiles = createdProfiles ?? new List<string>(),
                MissingProfiles = missingProfiles ?? new List<string>(),
                CleanupPolicy = GetFailurePolicy(),
                ErrorMessage = errorMessage ?? string.Empty,
                CreatedAtUtc = DateTime.UtcNow
            }
        };
    }

    private Task TryCleanupPartialProfileFirstCreationAsync(string characterId, string actorUserId, string requestId, List<string> writtenProfiles, ProfileFirstCharacterCreationDiagnosticResult diagnostic)
    {
        var policy = GetFailurePolicy();
        diagnostic.CleanupPolicy = policy;

        if (!IsProfileFirstCreationCleanupEnabled())
        {
            diagnostic.CleanupAttempted = false;
            diagnostic.CleanupResult = "skipped:flag_disabled";
            _logger.Debug($"profile.create.cleanup.skipped characterId={characterId} reason=flag_disabled");
            return Task.CompletedTask;
        }

        diagnostic.CleanupAttempted = true;
        _logger.Debug($"profile.create.cleanup.start characterId={characterId} policy={policy}");

        try
        {
            if (string.Equals(policy, ProfileFirstCreationFailurePolicy.KeepLegacyAndReport, StringComparison.Ordinal))
            {
                diagnostic.CleanupResult = "kept_legacy";
                _logger.Debug($"profile.create.cleanup.skipped characterId={characterId} reason=keep_legacy_and_report");
                _logger.Debug($"profile.create.cleanup.done characterId={characterId}");
                return Task.CompletedTask;
            }

            if (string.Equals(policy, ProfileFirstCreationFailurePolicy.ArchiveLegacyOnProfileFailure, StringComparison.Ordinal))
            {
                var character = _mongo.Characters.Find(Builders<Character>.Filter.Eq(x => x.Id, characterId)).FirstOrDefault();
                if (character == null)
                {
                    diagnostic.CleanupResult = "skipped:legacy_missing";
                    _logger.Debug($"profile.create.cleanup.skipped characterId={characterId} reason=legacy_missing");
                    _logger.Debug($"profile.create.cleanup.done characterId={characterId}");
                    return Task.CompletedTask;
                }

                character.Archived = true;
                character.Deleted = true;
                character.UpdatedUtc = DateTime.UtcNow;
                _mongo.Characters.ReplaceOne(Builders<Character>.Filter.Eq(x => x.Id, characterId), character);
                diagnostic.CleanupResult = "archived_legacy";
                _logger.Debug($"profile.create.cleanup.done characterId={characterId}");
                return Task.CompletedTask;
            }

            diagnostic.CleanupResult = "skipped:policy_not_supported";
            _logger.Debug($"profile.create.cleanup.skipped characterId={characterId} reason=policy_not_supported");
            _logger.Debug($"profile.create.cleanup.done characterId={characterId}");
        }
        catch (Exception ex)
        {
            diagnostic.CleanupResult = "error";
            _logger.Debug($"profile.create.cleanup.error characterId={characterId} message={ex.Message}");
        }

        return Task.CompletedTask;
    }

    private void WriteProfile(string characterId, string profileType, List<string> createdProfiles, Action writeAction)
    {
        writeAction();
        createdProfiles.Add(profileType);
        _logger.Debug($"profile.create.profile_written characterId={characterId} profile={profileType}");
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

    private static string GetFailurePolicy() => ProfileFirstCreationFailurePolicy.Default;

    private static bool IsProfileFirstCreationCleanupEnabled() => ProfileFeatureFlags.UseProfileFirstCreationCleanup;

    private static bool IsCharacterProfileConsistencyVerificationEnabled() => ProfileFeatureFlags.UseCharacterProfileConsistencyVerification;

    private static Dictionary<string, bool> BuildReadinessFeatureFlags() => new Dictionary<string, bool>
    {
        { nameof(ProfileFeatureFlags.UseProfileFirstCharacterCreation), ProfileFeatureFlags.UseProfileFirstCharacterCreation },
        { nameof(ProfileFeatureFlags.UseProfileFirstCharacterDetails), ProfileFeatureFlags.UseProfileFirstCharacterDetails },
        { nameof(ProfileFeatureFlags.UseProfileFirstCreationCleanup), ProfileFeatureFlags.UseProfileFirstCreationCleanup },
        { nameof(ProfileFeatureFlags.UseRuleSetProfilesWriteShadow), ProfileFeatureFlags.UseRuleSetProfilesWriteShadow },
        { nameof(ProfileFeatureFlags.UseAttributeProfileShadowWrite), ProfileFeatureFlags.UseAttributeProfileShadowWrite },
        { nameof(ProfileFeatureFlags.UseWalletProfileShadowWrite), ProfileFeatureFlags.UseWalletProfileShadowWrite },
        { nameof(ProfileFeatureFlags.UseSkillProfileShadowWrite), ProfileFeatureFlags.UseSkillProfileShadowWrite },
        { nameof(ProfileFeatureFlags.UseDevelopmentProfileShadowWrite), ProfileFeatureFlags.UseDevelopmentProfileShadowWrite },
        { nameof(ProfileFeatureFlags.UseInventoryProfileShadowWrite), ProfileFeatureFlags.UseInventoryProfileShadowWrite },
        { nameof(ProfileFeatureFlags.UseRaceOrSpeciesProfileShadowWrite), ProfileFeatureFlags.UseRaceOrSpeciesProfileShadowWrite },
        { nameof(ProfileFeatureFlags.UseBodyProfileShadowWrite), ProfileFeatureFlags.UseBodyProfileShadowWrite }
    };

    private static bool IsValidAttributeProfile(CharacterAttributeProfileDocument doc, string characterId) =>
        doc?.Profile != null &&
        IsValidProfileHeader(doc.CharacterId, doc.Profile.CharacterId, characterId, doc.Profile.SchemaVersion) &&
        doc.Profile.Values != null;

    private static bool IsValidWalletProfile(CharacterWalletProfileDocument doc, string characterId) =>
        doc?.Profile != null &&
        IsValidProfileHeader(doc.CharacterId, doc.Profile.CharacterId, characterId, doc.Profile.SchemaVersion) &&
        doc.Profile.Wallets != null;

    private static bool IsValidSkillProfile(CharacterSkillProfileDocument doc, string characterId) =>
        doc?.Profile != null &&
        IsValidProfileHeader(doc.CharacterId, doc.Profile.CharacterId, characterId, doc.Profile.SchemaVersion) &&
        doc.Profile.Skills != null;

    private static bool IsValidDevelopmentProfile(CharacterDevelopmentProfileDocument doc, string characterId) =>
        doc?.Profile != null &&
        IsValidProfileHeader(doc.CharacterId, doc.Profile.CharacterId, characterId, doc.Profile.SchemaVersion) &&
        doc.Profile.Nodes != null &&
        doc.Profile.ActiveHexagonIds != null;

    private static bool IsValidInventoryProfile(CharacterInventoryProfileDocument doc, string characterId) =>
        doc?.Profile != null &&
        IsValidProfileHeader(doc.CharacterId, doc.Profile.CharacterId, characterId, doc.Profile.SchemaVersion) &&
        doc.Profile.Items != null;

    private static bool IsValidRaceOrSpeciesProfile(CharacterRaceOrSpeciesProfileDocument doc, string characterId) =>
        doc?.Profile != null &&
        IsValidProfileHeader(doc.CharacterId, doc.Profile.CharacterId, characterId, doc.Profile.SchemaVersion) &&
        doc.Profile.Tags != null;

    private static bool IsValidBodyProfile(CharacterBodyProfileDocument doc, string characterId) =>
        doc?.Profile != null &&
        IsValidProfileHeader(doc.CharacterId, doc.Profile.CharacterId, characterId, doc.Profile.SchemaVersion) &&
        doc.Profile.BodyTags != null &&
        doc.Profile.EquipmentCompatibilityTags != null;

    private static bool IsValidProfileHeader(string documentCharacterId, string profileCharacterId, string expectedCharacterId, int schemaVersion) =>
        !string.IsNullOrWhiteSpace(expectedCharacterId) &&
        string.Equals(documentCharacterId, expectedCharacterId, StringComparison.Ordinal) &&
        string.Equals(profileCharacterId, expectedCharacterId, StringComparison.Ordinal) &&
        schemaVersion >= 1;
}
