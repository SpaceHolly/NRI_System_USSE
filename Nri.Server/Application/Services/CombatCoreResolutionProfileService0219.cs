using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Driver;
using Nri.Server.Infrastructure;
using Nri.Shared.Domain;

namespace Nri.Server.Application.Services;

public sealed class CombatCoreProfileResolution0219
{
    public int AbilityModifier { get; set; }
    public int SkillRank { get; set; }
    public int ProficiencyBonus { get; set; }
    public string AbilitySourceId { get; set; } = string.Empty;
    public string AbilitySourceKind { get; set; } = string.Empty;
    public string PrimarySkillId { get; set; } = string.Empty;
    public string ResolutionProfileId { get; set; } = string.Empty;
    public string AbilityModifierProfileId { get; set; } = string.Empty;
    public string SkillMasteryProfileId { get; set; } = string.Empty;
    public List<string> Warnings { get; set; } = new List<string>();
}

public interface ICombatCoreResolutionProfileService0219
{
    CombatCoreProfileResolution0219 Resolve(CombatParticipantState participant, string abilityOrSubAttributeId, string requestedSkillId, string ruleSetId, IEnumerable<string>? eligibleSkillIds = null);
}

public sealed class CombatCoreResolutionProfileService0219 : ICombatCoreResolutionProfileService0219
{
    private readonly ICharacterProfileService _profiles;
    private readonly MongoContext _mongo;
    private readonly object _cacheLock = new object();
    private readonly Dictionary<string, CachedResolutionProfiles0219> _cache = new Dictionary<string, CachedResolutionProfiles0219>(StringComparer.OrdinalIgnoreCase);

    public CombatCoreResolutionProfileService0219(ICharacterProfileService profiles, MongoContext mongo)
    {
        _profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
        _mongo = mongo ?? throw new ArgumentNullException(nameof(mongo));
    }

    public CombatCoreProfileResolution0219 Resolve(CombatParticipantState participant, string abilityOrSubAttributeId, string requestedSkillId, string ruleSetId, IEnumerable<string>? eligibleSkillIds = null)
    {
        var result = new CombatCoreProfileResolution0219();
        var selectedProfiles = ResolveProfiles(ruleSetId);
        result.ResolutionProfileId = selectedProfiles.Resolution.Id;
        result.AbilityModifierProfileId = selectedProfiles.Ability.Id;
        result.SkillMasteryProfileId = selectedProfiles.Mastery.Id;
        if (participant == null || string.IsNullOrWhiteSpace(participant.CharacterId))
        {
            result.Warnings.Add("profile_character_missing");
            return result;
        }

        ResolveAbility(participant.CharacterId, abilityOrSubAttributeId, selectedProfiles.Ability, result);
        ResolvePrimarySkill(participant.CharacterId, requestedSkillId, eligibleSkillIds, result);
        result.ProficiencyBonus = MasteryBonus(selectedProfiles.Mastery, result.SkillRank);
        return result;
    }

    private void ResolveAbility(string characterId, string sourceId, AbilityModifierProfileDefinition profile, CombatCoreProfileResolution0219 result)
    {
        if (string.IsNullOrWhiteSpace(sourceId))
        {
            result.Warnings.Add("attack_ability_not_selected");
            return;
        }

        var attributes = _profiles.GetAttributeProfile(characterId);
        var attribute = attributes.Values.FirstOrDefault(x => string.Equals(x.AttributeId, sourceId, StringComparison.OrdinalIgnoreCase));
        if (attribute != null)
        {
            result.AbilityModifier = MapAbility(attribute.CurrentValue + attribute.ManualModifier, profile, result.Warnings);
            result.AbilitySourceId = attribute.AttributeId;
            result.AbilitySourceKind = RequirementLeafTypes.Attribute;
            return;
        }

        var subAttributes = _profiles.GetSubAttributeProfile(characterId);
        var subAttribute = subAttributes.SubAttributes.FirstOrDefault(x => string.Equals(x.SubAttributeId, sourceId, StringComparison.OrdinalIgnoreCase));
        if (subAttribute != null)
        {
            result.AbilityModifier = MapAbility(subAttribute.CurrentValue + subAttribute.ManualBonus, profile, result.Warnings);
            result.AbilitySourceId = subAttribute.SubAttributeId;
            result.AbilitySourceKind = RequirementLeafTypes.SubAttribute;
            return;
        }

        result.Warnings.Add("attack_ability_not_found_in_character_v2_profile");
    }

    private void ResolvePrimarySkill(string characterId, string requestedSkillId, IEnumerable<string>? eligibleSkillIds, CombatCoreProfileResolution0219 result)
    {
        var skills = _profiles.GetSkillProfile(characterId).Skills
            .Where(x => x.IsLearned || x.IsUnlocked)
            .ToList();
        var eligible = new HashSet<string>((eligibleSkillIds ?? Enumerable.Empty<string>()).Where(x => !string.IsNullOrWhiteSpace(x)), StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(requestedSkillId)) eligible.Add(requestedSkillId);
        var candidates = skills
            .Where(x => eligible.Count == 0 || eligible.Contains(x.SkillId))
            .Select(x => new CoreResolutionProficiencyCandidate { SkillId = x.SkillId, Rank = x.Rank, IsEligible = true })
            .ToList();
        var primary = CoreResolutionPolicy0219.SelectPrimaryProficiency(candidates);
        if (string.IsNullOrWhiteSpace(primary))
        {
            if (!string.IsNullOrWhiteSpace(requestedSkillId)) result.Warnings.Add("attack_skill_not_found_in_character_v2_profile");
            return;
        }
        var skill = skills.First(x => string.Equals(x.SkillId, primary, StringComparison.OrdinalIgnoreCase));
        result.PrimarySkillId = skill.SkillId;
        result.SkillRank = Math.Max(0, Math.Min(20, skill.Rank));
    }

    private static int BoundAbility(int value, AbilityModifierProfileDefinition profile, List<string> warnings)
    {
        var bounded = Math.Max(profile.MinimumModifier, Math.Min(profile.MaximumModifier, value));
        if (bounded != value) warnings.Add("ability_modifier_bounded");
        return bounded;
    }

    private static int MapAbility(int rawValue, AbilityModifierProfileDefinition profile, List<string> warnings)
    {
        var mapped = rawValue;
        if (string.Equals(profile.MappingMode, "score_to_modifier", StringComparison.OrdinalIgnoreCase))
        {
            mapped = (int)Math.Floor((rawValue - 10) / 2d);
        }
        else if (string.Equals(profile.MappingMode, "lookup_table", StringComparison.OrdinalIgnoreCase)
                 && profile.LookupTable.TryGetValue(rawValue, out var lookupValue))
        {
            mapped = lookupValue;
        }
        return BoundAbility(mapped, profile, warnings);
    }

    private CachedResolutionProfiles0219 ResolveProfiles(string ruleSetId)
    {
        var selectedRuleSet = string.IsNullOrWhiteSpace(ruleSetId) ? FantasyNriDefaultResolutionProfiles0219.RuleSetId : ruleSetId.Trim();
        lock (_cacheLock)
        {
            if (_cache.TryGetValue(selectedRuleSet, out var cached) && cached.ExpiresAtUtc > DateTime.UtcNow) return cached;
        }

        var categories = new[] { "resolution_profile", "ability_modifier_profile", "skill_mastery_profile" };
        var records = _mongo.ContentDefinitionRecords.Find(
            Builders<ContentDefinitionRecord>.Filter.In(x => x.Category, categories)
            & Builders<ContentDefinitionRecord>.Filter.Eq(x => x.IsArchived, false)
            & Builders<ContentDefinitionRecord>.Filter.Eq(x => x.RuleSetId, selectedRuleSet)).ToList();
        var resolutionRecord = records.Where(x => x.Category == "resolution_profile").OrderByDescending(x => x.UpdatedAtUtc).FirstOrDefault();
        var resolution = resolutionRecord == null ? FantasyNriDefaultResolutionProfiles0219.Resolution() : new ResolutionProfileDefinition
        {
            Id = resolutionRecord.Id,
            RuleSetId = selectedRuleSet,
            PrimaryDie = FieldString(resolutionRecord, "primaryDie", "1d20"),
            AbilityContributionPolicy = FieldString(resolutionRecord, "abilityContributionPolicy", "attribute_or_subattribute"),
            AbilityModifierProfileId = FieldString(resolutionRecord, "abilityModifierProfileId", string.Empty),
            SkillMasteryProfileId = FieldString(resolutionRecord, "skillMasteryProfileId", string.Empty)
        };
        var abilityRecord = FindLinked(records, "ability_modifier_profile", resolution.AbilityModifierProfileId);
        var masteryRecord = FindLinked(records, "skill_mastery_profile", resolution.SkillMasteryProfileId);
        var ability = abilityRecord == null ? FantasyNriDefaultResolutionProfiles0219.Ability() : new AbilityModifierProfileDefinition
        {
            Id = abilityRecord.Id,
            RuleSetId = selectedRuleSet,
            MappingMode = FieldString(abilityRecord, "mappingMode", "score_to_modifier"),
            MinimumModifier = FieldInt(abilityRecord, "minimumModifier", -4),
            MaximumModifier = FieldInt(abilityRecord, "maximumModifier", 4)
        };
        var mastery = masteryRecord == null ? FantasyNriDefaultResolutionProfiles0219.Mastery() : BuildMastery(masteryRecord, selectedRuleSet);
        var loaded = new CachedResolutionProfiles0219(resolution, ability, mastery, DateTime.UtcNow.AddSeconds(30));
        lock (_cacheLock) _cache[selectedRuleSet] = loaded;
        return loaded;
    }

    private static ContentDefinitionRecord? FindLinked(IEnumerable<ContentDefinitionRecord> records, string category, string id)
        => records.Where(x => x.Category == category && (string.IsNullOrWhiteSpace(id) || string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase) || string.Equals(x.StableKey, id, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(x => x.UpdatedAtUtc).FirstOrDefault();

    private static SkillMasteryProfileDefinition BuildMastery(ContentDefinitionRecord record, string ruleSetId)
        => new SkillMasteryProfileDefinition
        {
            Id = record.Id,
            RuleSetId = ruleSetId,
            MinimumRank = FieldInt(record, "minimumRank", 0),
            MaximumRank = FieldInt(record, "maximumRank", 20),
            Bands = new List<SkillMasteryBandDefinition0219>
            {
                new SkillMasteryBandDefinition0219 { MinimumRank=0, MaximumRank=0, ProficiencyModifier=0, PublicLabel="Не обучен" },
                new SkillMasteryBandDefinition0219 { MinimumRank=1, MaximumRank=4, ProficiencyModifier=FieldInt(record,"rank1To4Bonus",1), PublicLabel="Новичок" },
                new SkillMasteryBandDefinition0219 { MinimumRank=5, MaximumRank=8, ProficiencyModifier=FieldInt(record,"rank5To8Bonus",2), PublicLabel="Обученный" },
                new SkillMasteryBandDefinition0219 { MinimumRank=9, MaximumRank=12, ProficiencyModifier=FieldInt(record,"rank9To12Bonus",3), PublicLabel="Профессионал" },
                new SkillMasteryBandDefinition0219 { MinimumRank=13, MaximumRank=16, ProficiencyModifier=FieldInt(record,"rank13To16Bonus",4), PublicLabel="Эксперт" },
                new SkillMasteryBandDefinition0219 { MinimumRank=17, MaximumRank=20, ProficiencyModifier=FieldInt(record,"rank17To20Bonus",5), PublicLabel="Мастер" }
            }
        };

    private static int MasteryBonus(SkillMasteryProfileDefinition profile, int rank)
    {
        var bounded = Math.Max(profile.MinimumRank, Math.Min(profile.MaximumRank, rank));
        return profile.Bands.Where(x => bounded >= x.MinimumRank && bounded <= x.MaximumRank).Select(x => x.ProficiencyModifier).FirstOrDefault();
    }

    private static string FieldString(ContentDefinitionRecord record, string name, string fallback)
        => record.CustomFields.TryGetValue(name, out var value) && value != null && !string.IsNullOrWhiteSpace(Convert.ToString(value)) ? Convert.ToString(value)! : fallback;

    private static int FieldInt(ContentDefinitionRecord record, string name, int fallback)
        => record.CustomFields.TryGetValue(name, out var value) && value != null && int.TryParse(Convert.ToString(value), out var parsed) ? parsed : fallback;

    private sealed class CachedResolutionProfiles0219
    {
        public CachedResolutionProfiles0219(ResolutionProfileDefinition resolution, AbilityModifierProfileDefinition ability, SkillMasteryProfileDefinition mastery, DateTime expiresAtUtc)
        { Resolution=resolution; Ability=ability; Mastery=mastery; ExpiresAtUtc=expiresAtUtc; }
        public ResolutionProfileDefinition Resolution { get; }
        public AbilityModifierProfileDefinition Ability { get; }
        public SkillMasteryProfileDefinition Mastery { get; }
        public DateTime ExpiresAtUtc { get; }
    }
}
