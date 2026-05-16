using System;
using System.Collections.Generic;

namespace Nri.Shared.Domain;

// Identifier constants for rule sets (system-level presets).
public static class RuleSetIds
{
    public const string FantasyNriDefault = "fantasy_nri_default";
    public const string ScifiUrukVnext = "scifi_uruk_vnext";
    public const string FantasyScifiFusion = "fantasy_scifi_fusion";
    public const string CustomGmRuleset = "custom_gm_ruleset";
}

// Identifier constants for modular character blocks.
public static class CharacterModuleIds
{
    public const string Attributes = "attributes";
    public const string SubAttributes = "subAttributes";
    public const string DerivedStats = "derivedStats";
    public const string Body = "body";
    public const string RaceOrSpecies = "raceOrSpecies";
    public const string Skills = "skills";
    public const string Development = "development";
    public const string Wallet = "wallet";
    public const string Inventory = "inventory";
    public const string Equipment = "equipment";
    public const string Knowledge = "knowledge";
    public const string Languages = "languages";
    public const string Conditions = "conditions";
    public const string Reputation = "reputation";
    public const string Magic = "magic";
    public const string Augmentations = "augmentations";
    public const string Cybernetics = "cybernetics";
    public const string Licenses = "licenses";
    public const string Factions = "factions";
}

// Feature flags for gradual migration to profile-first character architecture.
public static class ProfileFeatureFlags
{
    public const bool UseRuleSetProfilesRead = false;
    public const bool UseRuleSetProfilesWriteShadow = false;
    public const bool UseProfileFirstCharacterDetails = false;
    public const bool UseAttributeProfileReadShadow = false;
    public const bool UseWalletProfileReadShadow = false;
    public const bool UseSkillProfileReadShadow = false;
    public const bool UseDevelopmentNodeModel = false;
    public const bool UseSkillDefinitionV2 = false;
}

public static class CharacterCurrencyIds
{
    public const string IronCoin = "iron_coin";
    public const string BronzeCoin = "bronze_coin";
    public const string SilverCoin = "silver_coin";
    public const string GoldCoin = "gold_coin";
    public const string PlatinumCoin = "platinum_coin";
    public const string OrichalcumCoin = "orichalcum_coin";
    public const string AdamantCoin = "adamant_coin";
    public const string SovereignCoin = "sovereign_coin";
    public const string XpCoin = "xp_coin";

    public const string Credit = "credit";
    public const string CorporateCredit = "corporate_credit";
    public const string RationToken = "ration_token";
    public const string LicensePoint = "license_point";
}

// High-level rule set descriptor. Not wired to legacy commands yet.
public sealed class RuleSetDefinition : EntityBase
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string VersionTag { get; set; } = "1.0.0";
    public bool IsActive { get; set; } = true;
    // Enables/disables profile modules for this ruleset.
    public Dictionary<string, bool> EnabledProfiles { get; set; } = new Dictionary<string, bool>();
}

// Module activation matrix per character.
public sealed class CharacterModuleState
{
    // Selected ruleset for this character instance.
    public string RuleSetCode { get; set; } = string.Empty;
    // Module on/off map by CharacterModuleIds.
    public Dictionary<string, bool> Modules { get; set; } = new Dictionary<string, bool>();
    // Local module-state revision (separate from global sync revision).
    public int Revision { get; set; }
}

// Canonical attributes profile (system-agnostic key/value store).
public sealed class AttributeProfile
{
    public string CharacterId { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = RuleSetIds.FantasyNriDefault;
    public List<CharacterAttributeValue> Values { get; set; } = new List<CharacterAttributeValue>();
    public int SchemaVersion { get; set; } = 1;
}

public sealed class CharacterAttributeValue
{
    public string AttributeId { get; set; } = string.Empty;
    public int BaseValue { get; set; }
    public int CurrentValue { get; set; }
    public int ManualModifier { get; set; }
    public string Source { get; set; } = "legacy_shadow";
    public string Notes { get; set; } = string.Empty;
}

// Canonical skills profile for learned/known skills.
public sealed class SkillProfile
{
    public string CharacterId { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = RuleSetIds.FantasyNriDefault;
    public List<CharacterSkillProfileValue> Skills { get; set; } = new List<CharacterSkillProfileValue>();
    public int SchemaVersion { get; set; } = 1;
}

public sealed class CharacterSkillProfileValue
{
    public string SkillId { get; set; } = string.Empty;
    public int Rank { get; set; }
    public bool IsUnlocked { get; set; }
    public bool IsLearned { get; set; }
    public string Source { get; set; } = "legacy_shadow";
    public DateTime LearnedAtUtc { get; set; } = DateTime.UtcNow;
    public string Notes { get; set; } = string.Empty;
}

// Generic development graph state (classes/professions/specializations/etc).
public sealed class DevelopmentProfile
{
    public List<DevelopmentNodeState> Nodes { get; set; } = new List<DevelopmentNodeState>();
    public int DevelopmentCurrency { get; set; }
}

public sealed class DevelopmentNodeState
{
    public string NodeId { get; set; } = string.Empty;
    // Free-form node type (class, branch, specialization, license, ...).
    public string NodeType { get; set; } = string.Empty;
    public int Tier { get; set; }
    public bool Acquired { get; set; }
}

// Generic wallet balances by currency code.
public sealed class WalletProfile
{
    public string CharacterId { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = RuleSetIds.FantasyNriDefault;
    public List<CharacterWalletValue> Wallets { get; set; } = new List<CharacterWalletValue>();
    public int SchemaVersion { get; set; } = 1;
}

public sealed class CharacterWalletValue
{
    public string CurrencyId { get; set; } = string.Empty;
    public long Amount { get; set; }
    public string Source { get; set; } = "legacy_shadow";
    public string Notes { get; set; } = string.Empty;
}

// Body/health-like derived values (system-agnostic).
public sealed class BodyProfile
{
    public Dictionary<string, int> BodyStats { get; set; } = new Dictionary<string, int>();
}

// Knowledge + language holder for modular rulesets.
public sealed class KnowledgeProfile
{
    public List<string> KnownTopics { get; set; } = new List<string>();
    public List<string> Languages { get; set; } = new List<string>();
}

// Current dynamic conditions (effects, statuses, traumas, etc).
public sealed class ConditionProfile
{
    public List<string> Conditions { get; set; } = new List<string>();
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}
