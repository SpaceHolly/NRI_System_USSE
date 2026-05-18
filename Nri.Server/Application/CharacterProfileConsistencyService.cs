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
            _logger.Error($"profile.consistency.error characterId={characterId} message={ex.Message}");
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
        sections.Add(Section("inventory", _mongo.CharacterInventoryProfiles.Find(Builders<CharacterInventoryProfileDocument>.Filter.Eq(x => x.CharacterId, character.Id)).FirstOrDefault()?.Profile, fresh.InventoryProfile, ProfileFeatureFlags.UseInventoryProfileShadowWrite));

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
            Notes = "read_only_check; TODO(F0.5.11): reputation/holdings/companions sections"
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
}
