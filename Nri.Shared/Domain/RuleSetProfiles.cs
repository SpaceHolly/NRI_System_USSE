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
    public const bool UseProfileFirstCharacterDetails = true;
    public const bool UseProfileFirstCharacterCreation = true;
    public const bool UseProfileFirstCreationCleanup = false;
    public const bool UseAttributeProfileReadShadow = false;
    public const bool UseWalletProfileReadShadow = false;
    public const bool UseSkillProfileReadShadow = false;
    public const bool UseDevelopmentProfileReadShadow = false;
    public const bool UseInventoryProfileReadShadow = false;
    public const bool UseRaceOrSpeciesProfileReadShadow = false;
    public const bool UseBodyProfileReadShadow = false;
    public const bool UseReputationProfileReadShadow = false;
    public const bool UseHoldingsProfileReadShadow = false;
    public const bool UseCompanionProfileReadShadow = false;
    public const bool UseCharacterProfileShadowCompare = false;
    public const bool UseAttributeProfileShadowWrite = false;
    public const bool UseWalletProfileShadowWrite = false;
    public const bool UseSkillProfileShadowWrite = false;
    public const bool UseDevelopmentProfileShadowWrite = false;
    public const bool UseInventoryProfileShadowWrite = false;
    public const bool UseRaceOrSpeciesProfileShadowWrite = false;
    public const bool UseBodyProfileShadowWrite = false;
    public const bool UseProfileNativeCharacterWrites = true;
    public const bool UseProfileNativeStatsWrite = true;
    public const bool UseProfileNativeWalletWrite = true;
    public const bool UseProfileNativeSkillWrite = true;
    public const bool UseProfileNativeDevelopmentWrite = true;
    public const bool UseProfileNativeInventoryWrite = true;
    public const bool UseProfileNativeRaceBodyWrite = true;
    public const bool UseProfileNativeRaceOrSpeciesWrite = true;
    public const bool UseProfileNativeBodyWrite = true;
    public const bool UseCharacterProfileConsistencyVerification = false;
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

public sealed class SubAttributeProfile
{
    public string CharacterId { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = RuleSetIds.FantasyNriDefault;
    public int ProfileVersion { get; set; } = 1;
    public List<CharacterSubAttributeValue> SubAttributes { get; set; } = new List<CharacterSubAttributeValue>();
    public int Revision { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public bool IsArchived { get; set; }
    public int SchemaVersion { get; set; } = 1;
}

public sealed class CharacterSubAttributeValue
{
    public string SubAttributeId { get; set; } = string.Empty;
    public string ParentAttributeId { get; set; } = string.Empty;
    public int BaseValue { get; set; }
    public int CurrentValue { get; set; }
    public int ManualBonus { get; set; }
    public string Source { get; set; } = "ruleset_default";
    public bool IsVisibleToPlayer { get; set; } = true;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
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
    public int ManualBonus { get; set; }
    public string TrainingState { get; set; } = "trained";
    public bool IsPlayerVisible { get; set; } = true;
    public bool IsUnlocked { get; set; }
    public bool IsLearned { get; set; }
    public string Source { get; set; } = "legacy_shadow";
    public DateTime LearnedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string Notes { get; set; } = string.Empty;
}

// Generic development graph state (classes/professions/specializations/etc).
public sealed class DevelopmentProfile
{
    public string CharacterId { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = RuleSetIds.FantasyNriDefault;
    public string ActiveHexagonId { get; set; } = string.Empty;
    public List<string> ActiveHexagonIds { get; set; } = new List<string>();
    public List<CharacterDevelopmentHexagonState> Hexagons { get; set; } = new List<CharacterDevelopmentHexagonState>();
    public List<CharacterDevelopmentNodeState> Nodes { get; set; } = new List<CharacterDevelopmentNodeState>();
    public string Vocation { get; set; } = string.Empty;
    public int TotalXpSpent { get; set; }
    public int Revision { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public int SchemaVersion { get; set; } = 1;
}

public class CharacterDevelopmentHexagonState
{
    public string HexagonId { get; set; } = string.Empty;
    public string HexagonType { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsUnlocked { get; set; }
    public bool IsPlayerVisible { get; set; } = true;
    public bool IsMain { get; set; }
    public int SortOrder { get; set; }
    public List<CharacterDevelopmentNodeState> Nodes { get; set; } = new List<CharacterDevelopmentNodeState>();
}

public class CharacterDevelopmentNodeState : EntityBase
{
    public string CharacterId { get; set; } = string.Empty;
    public string HexagonId { get; set; } = "main_development_hexagon";
    public string DevelopmentNodeId { get; set; } = string.Empty;
    public string ClassId { get; set; } = string.Empty;

    // Тип узла развития: class, branch, subbranch, skill, profession, magic_path и т.д.
    public string NodeType { get; set; } = string.Empty;

    public int CurrentTier { get; set; }
    public int MaxTier { get; set; }

    public bool IsUnlocked { get; set; }
    public bool IsPurchased { get; set; }
    public bool IsHidden { get; set; }
    public bool IsAvailable { get; set; }
    public string State { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }

    public string PurchasedAtWorldDate { get; set; } = string.Empty;
    public DateTime PurchasedAtUtc { get; set; } = DateTime.UtcNow;
    public int CostPaid { get; set; }
    public string CurrencyId { get; set; } = CharacterCurrencyIds.XpCoin;

    public string Source { get; set; } = string.Empty;
    public string GMApprovalStatus { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
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

// Canonical race/species identity profile. Legacy Character remains authoritative.
public sealed class RaceOrSpeciesProfile
{
    public string CharacterId { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = RuleSetIds.FantasyNriDefault;
    public string RaceId { get; set; } = string.Empty;
    public string RaceCode { get; set; } = string.Empty;
    public string RaceName { get; set; } = string.Empty;
    public string SubspeciesId { get; set; } = string.Empty;
    public string HybridId { get; set; } = string.Empty;
    public string HybridSubtypeId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Source { get; set; } = "legacy_shadow";
    public string Notes { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new List<string>();
    public int SchemaVersion { get; set; } = 1;
}

// Body/health-like derived values (system-agnostic).
public sealed class BodyProfile
{
    public string CharacterId { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = RuleSetIds.FantasyNriDefault;
    public string BodyType { get; set; } = string.Empty;
    public string SpeciesBodyType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Backstory { get; set; } = string.Empty;
    public int HeightCm { get; set; }
    public string HeightText { get; set; } = string.Empty;
    public int AgeYears { get; set; }
    public string AgeText { get; set; } = string.Empty;
    public string SizeCategory { get; set; } = string.Empty;
    public List<string> BodyTags { get; set; } = new List<string>();
    public List<string> EquipmentCompatibilityTags { get; set; } = new List<string>();
    public string Notes { get; set; } = string.Empty;
    public string Source { get; set; } = "legacy_shadow";
    public Dictionary<string, int> BodyStats { get; set; } = new Dictionary<string, int>();
    public int SchemaVersion { get; set; } = 1;
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

public sealed class InventoryProfile
{
    public string CharacterId { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = RuleSetIds.FantasyNriDefault;
    public List<CharacterInventoryItemProfileValue> Items { get; set; } = new List<CharacterInventoryItemProfileValue>();
    public int SchemaVersion { get; set; } = 1;
}

public sealed class CharacterInventoryItemProfileValue
{
    public string ItemId { get; set; } = string.Empty;
    public string DefinitionId { get; set; } = string.Empty;
    public string ItemDefinitionId { get; set; } = string.Empty;
    public string DefinitionCategory { get; set; } = string.Empty;
    public string DefinitionCode { get; set; } = string.Empty;
    public string SnapshotDisplayName { get; set; } = string.Empty;
    public string SnapshotCategory { get; set; } = string.Empty;
    public string SnapshotDescription { get; set; } = string.Empty;
    public List<string> SnapshotTags { get; set; } = new List<string>();
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public int Durability { get; set; }
    public int MaxDurability { get; set; }
    public string Condition { get; set; } = string.Empty;
    public int Ammo { get; set; }
    public bool IsEquipped { get; set; }
    public string SlotId { get; set; } = string.Empty;
    public bool IsPlayerVisible { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string Source { get; set; } = "legacy_shadow";
    public string Notes { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new List<string>();
}


public sealed class ReputationProfile
{
    public string CharacterId { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = RuleSetIds.FantasyNriDefault;
    public List<CharacterReputationProfileValue> Entries { get; set; } = new List<CharacterReputationProfileValue>();
    public int SchemaVersion { get; set; } = 1;
}

public sealed class CharacterReputationProfileValue
{
    public string EntryId { get; set; } = string.Empty;
    public string Scope { get; set; } = "Personal";
    public string ScopeType { get; set; } = "Character";
    public string TargetType { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Value { get; set; }
    public int GroupValue { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public bool IsPlayerVisible { get; set; } = true;
    public bool IsArchived { get; set; }
    public List<string> Tags { get; set; } = new List<string>();
    public string Source { get; set; } = "legacy_shadow";
}

public sealed class HoldingsProfile
{
    public string CharacterId { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = RuleSetIds.FantasyNriDefault;
    public List<CharacterHoldingProfileValue> Holdings { get; set; } = new List<CharacterHoldingProfileValue>();
    public int SchemaVersion { get; set; } = 1;
}

public sealed class CharacterHoldingProfileValue
{
    public string HoldingId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string HoldingType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string LocationId { get; set; } = string.Empty;
    public string LocationName { get; set; } = string.Empty;
    public List<string> OwnerUserIds { get; set; } = new List<string>();
    public List<string> OwnerCharacterIds { get; set; } = new List<string>();
    public string OwnerDisplayName { get; set; } = string.Empty;
    public string LegalStatus { get; set; } = string.Empty;
    public string ActualStatus { get; set; } = string.Empty;
    public bool IsPlayerVisible { get; set; } = true;
    public bool IsArchived { get; set; }
    public string Notes { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new List<string>();
    public string Source { get; set; } = "legacy_shadow";
}

public sealed class CompanionProfile
{
    public string CharacterId { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = RuleSetIds.FantasyNriDefault;
    public List<CharacterCompanionProfileValue> Companions { get; set; } = new List<CharacterCompanionProfileValue>();
    public int SchemaVersion { get; set; } = 1;
}

public sealed class CharacterCompanionProfileValue
{
    public string CompanionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string RaceOrSpeciesId { get; set; } = string.Empty;
    public string CompanionType { get; set; } = string.Empty;
    public string OwnerCharacterId { get; set; } = string.Empty;
    public string OwnerDisplayName { get; set; } = string.Empty;
    public string InitiativeMode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool HasSeparateInventory { get; set; }
    public bool IsPlayerVisible { get; set; } = true;
    public bool IsArchived { get; set; }
    public string Notes { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new List<string>();
    public string Source { get; set; } = "legacy_shadow";
}
