using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Driver;
using Nri.Server.Infrastructure;
using Nri.Server.Logging;
using Nri.Shared.Domain;

namespace Nri.Server.Application;

public sealed class CharacterProfileConsistencySectionReport
{
    public string Section { get; set; } = string.Empty;
    public bool HasPersistedProfile { get; set; }
    public bool IsConsistent { get; set; }
    public int DifferenceCount { get; set; }
    public List<string> Differences { get; set; } = new List<string>();
    public string Severity { get; set; } = "info";
}

public sealed class CharacterProfileConsistencyReport
{
    public string CharacterId { get; set; } = string.Empty;
    public bool IsConsistent { get; set; }
    public DateTime CheckedAtUtc { get; set; } = DateTime.UtcNow;
    public List<CharacterProfileConsistencySectionReport> SectionReports { get; set; } = new List<CharacterProfileConsistencySectionReport>();
    public int TotalDifferenceCount { get; set; }
    public string Notes { get; set; } = string.Empty;
}

public interface ICharacterProfileConsistencyService
{
    Task<CharacterProfileConsistencyReport> VerifyCharacterAsync(string characterId, string actorUserId, string requestId);
    CharacterProfileConsistencyReport VerifyProfilesAgainstLegacyAsync(Character character);
    CharacterProfileConsistencyReport VerifyPersistedProfilesAgainstFreshShadowAsync(Character character);
    CharacterProfileConsistencyReport BuildConsistencyReport(Character character);
}

public sealed class CharacterProfileConsistencyService : ICharacterProfileConsistencyService
{
    private readonly MongoContext _mongo;
    private readonly ICharacterProfileShadowBuilder _shadowBuilder;
    private readonly IServerLogger _logger;

    public CharacterProfileConsistencyService(MongoContext mongo, ICharacterProfileShadowBuilder shadowBuilder, IServerLogger logger)
    {
        _mongo = mongo;
        _shadowBuilder = shadowBuilder;
        _logger = logger;
    }

    public Task<CharacterProfileConsistencyReport> VerifyCharacterAsync(string characterId, string actorUserId, string requestId)
    {
        _logger.Debug($"profile.consistency.check.start characterId={characterId}");
        try
        {
            var character = _mongo.Characters.Find(Builders<Character>.Filter.Eq(x => x.Id, characterId)).FirstOrDefault() ?? new Character { Id = characterId };
            var report = BuildConsistencyReport(character);
            _logger.Debug($"profile.consistency.check.done characterId={report.CharacterId} consistent={report.IsConsistent} differences={report.TotalDifferenceCount}");
            foreach (var section in report.SectionReports)
                _logger.Debug($"profile.consistency.section section={section.Section} consistent={section.IsConsistent} differences={section.DifferenceCount}");
            return Task.FromResult(report);
        }
        catch (Exception ex)
        {
            _logger.Debug($"profile.consistency.error characterId={characterId} message={ex.Message}");
            return Task.FromResult(new CharacterProfileConsistencyReport { CharacterId = characterId, IsConsistent = false, CheckedAtUtc = DateTime.UtcNow, Notes = "verification_error", TotalDifferenceCount = 1 });
        }
    }

    public CharacterProfileConsistencyReport VerifyProfilesAgainstLegacyAsync(Character character)
    {
        var fresh = _shadowBuilder.CompareLegacyToShadow(character);
        return new CharacterProfileConsistencyReport
        {
            CharacterId = character?.Id ?? string.Empty,
            IsConsistent = fresh.IsEquivalent,
            CheckedAtUtc = DateTime.UtcNow,
            SectionReports = fresh.SectionResults.Select(x => new CharacterProfileConsistencySectionReport
            {
                Section = x.Section,
                HasPersistedProfile = false,
                IsConsistent = x.IsEquivalent,
                DifferenceCount = x.DifferenceCount,
                Differences = x.Differences,
                Severity = x.DifferenceCount == 0 ? "info" : "error"
            }).ToList(),
            TotalDifferenceCount = fresh.Differences.Count,
            Notes = "legacy_to_fresh_shadow"
        };
    }

    public CharacterProfileConsistencyReport VerifyPersistedProfilesAgainstFreshShadowAsync(Character character)
    {
        return BuildConsistencyReport(character);
    }

    public CharacterProfileConsistencyReport BuildConsistencyReport(Character character)
    {
        var fresh = _shadowBuilder.BuildShadowBundleFromLegacy(character);
        var sections = new List<CharacterProfileConsistencySectionReport>();

        sections.Add(Section("attributes", _mongo.CharacterAttributeProfiles.Find(Builders<CharacterAttributeProfileDocument>.Filter.Eq(x => x.CharacterId, character.Id)).FirstOrDefault()?.Profile, fresh.AttributeProfile, ProfileFeatureFlags.UseAttributeProfileShadowWrite));
        sections.Add(Section("wallet", _mongo.CharacterWalletProfiles.Find(Builders<CharacterWalletProfileDocument>.Filter.Eq(x => x.CharacterId, character.Id)).FirstOrDefault()?.Profile, fresh.WalletProfile, ProfileFeatureFlags.UseWalletProfileShadowWrite));
        sections.Add(Section("skills", _mongo.CharacterSkillProfiles.Find(Builders<CharacterSkillProfileDocument>.Filter.Eq(x => x.CharacterId, character.Id)).FirstOrDefault()?.Profile, fresh.SkillProfile, ProfileFeatureFlags.UseSkillProfileShadowWrite));
        sections.Add(Section("development", _mongo.CharacterDevelopmentProfiles.Find(Builders<CharacterDevelopmentProfileDocument>.Filter.Eq(x => x.CharacterId, character.Id)).FirstOrDefault()?.Profile, fresh.DevelopmentProfile, ProfileFeatureFlags.UseDevelopmentProfileShadowWrite));
        sections.Add(InventorySection(character.Id, fresh.InventoryProfile));
        sections.Add(Section("raceOrSpecies", _mongo.CharacterRaceOrSpeciesProfiles.Find(Builders<CharacterRaceOrSpeciesProfileDocument>.Filter.Eq(x => x.CharacterId, character.Id)).FirstOrDefault()?.Profile, fresh.RaceOrSpeciesProfile, ProfileFeatureFlags.UseRaceOrSpeciesProfileReadShadow));
        sections.Add(Section("body", _mongo.CharacterBodyProfiles.Find(Builders<CharacterBodyProfileDocument>.Filter.Eq(x => x.CharacterId, character.Id)).FirstOrDefault()?.Profile, fresh.BodyProfile, ProfileFeatureFlags.UseBodyProfileReadShadow));

        if (string.IsNullOrWhiteSpace(character?.Id))
            sections.Add(new CharacterProfileConsistencySectionReport { Section = "characterId", HasPersistedProfile = false, IsConsistent = false, DifferenceCount = 1, Differences = new List<string> { "legacy.characterId.empty" }, Severity = "warning" });

        var totalDiffs = sections.Sum(x => x.DifferenceCount);
        return new CharacterProfileConsistencyReport
        {
            CharacterId = character?.Id ?? string.Empty,
            IsConsistent = totalDiffs == 0,
            CheckedAtUtc = DateTime.UtcNow,
            SectionReports = sections,
            TotalDifferenceCount = totalDiffs,
            Notes = "read_only_check; TODO(F0.5.x): reputation/holdings/companions persisted sections"
        };
    }

    private static CharacterProfileConsistencySectionReport Section<T>(string section, T? persisted, T fresh, bool specificShadowFlag) where T : class
    {
        if (persisted == null)
        {
            var sev = ProfileFeatureFlags.UseRuleSetProfilesWriteShadow && specificShadowFlag ? "warning" : "info";
            return new CharacterProfileConsistencySectionReport { Section = section, HasPersistedProfile = false, IsConsistent = false, DifferenceCount = 1, Differences = new List<string> { "persisted.missing" }, Severity = sev };
        }

        var persistedJson = System.Text.Json.JsonSerializer.Serialize(persisted);
        var freshJson = System.Text.Json.JsonSerializer.Serialize(fresh);
        if (string.Equals(persistedJson, freshJson, StringComparison.Ordinal))
            return new CharacterProfileConsistencySectionReport { Section = section, HasPersistedProfile = true, IsConsistent = true, DifferenceCount = 0, Severity = "info" };

        return new CharacterProfileConsistencySectionReport
        {
            Section = section,
            HasPersistedProfile = true,
            IsConsistent = false,
            DifferenceCount = 1,
            Differences = new List<string> { "persisted.differs.from.fresh_shadow" },
            Severity = section == "attributes" || section == "wallet" || section == "skills" ? "error" : "warning"
        };
    }

    private CharacterProfileConsistencySectionReport InventorySection(string characterId, InventoryProfile fresh)
    {
        var doc = _mongo.CharacterInventoryProfiles.Find(Builders<CharacterInventoryProfileDocument>.Filter.Eq(x => x.CharacterId, characterId)).FirstOrDefault();
        if (doc == null)
        {
            return Section("inventory", null, fresh, ProfileFeatureFlags.UseInventoryProfileShadowWrite);
        }

        if (!TryGetValidInventoryProfile(doc, characterId, out var persisted, out var invalidReason))
        {
            _logger.Debug($"inventory.profile.invalid characterId={characterId} reason={invalidReason}");
            return new CharacterProfileConsistencySectionReport
            {
                Section = "inventory",
                HasPersistedProfile = true,
                IsConsistent = false,
                DifferenceCount = 1,
                Differences = new List<string> { $"persisted.invalid:{invalidReason}" },
                Severity = "error"
            };
        }

        return Section("inventory", persisted, fresh, ProfileFeatureFlags.UseInventoryProfileShadowWrite);
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
}
