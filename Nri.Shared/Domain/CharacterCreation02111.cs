using System;
using System.Collections.Generic;

namespace Nri.Shared.Domain;

public static class CharacterCreationPolicyIds
{
    public const string Free = "free";
    public const string RequireGmApproval = "require_gm_approval";
    public const string GmOnly = "gm_only";
}

public static class CharacterCreationDraftStatusIds
{
    public const string Draft = "draft";
    public const string Submitted = "submitted";
    public const string ReturnedForRevision = "returned_for_revision";
    public const string Finalized = "finalized";
    public const string Cancelled = "cancelled";
}

public static class CharacterOriginKinds
{
    public const string Race = "race";
    public const string Hybrid = "hybrid";
}

public static class CharacterOriginAvailabilityIds
{
    public const string Playable = "playable";
    public const string PlayableWithCampaignPermission = "playable_with_campaign_permission";
    public const string NpcOnly = "npc_only";
    public const string MonsterOnly = "monster_only";
    public const string WildOnly = "wild_only";
    public const string Hidden = "hidden";
}

public sealed class CharacterCreationPolicyState : EntityBase
{
    public string CampaignId { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = RuleSetIds.FantasyNriDefault;
    public string Policy { get; set; } = CharacterCreationPolicyIds.RequireGmApproval;
    public bool PlayerMayRenameFinalized { get; set; } = true;
    public bool PlayerMayEditFinalizedBackstory { get; set; } = true;
    public long EntityRevision { get; set; } = 1;
    public string UpdatedByUserId { get; set; } = string.Empty;
}

public sealed class CharacterCreationDraft : EntityBase
{
    public string CampaignId { get; set; } = string.Empty;
    public string OwnerUserId { get; set; } = string.Empty;
    public string CreatedByUserId { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = RuleSetIds.FantasyNriDefault;
    public string Status { get; set; } = CharacterCreationDraftStatusIds.Draft;
    public string DisplayName { get; set; } = string.Empty;
    public string PublicBackstory { get; set; } = string.Empty;
    public string Parent1RaceId { get; set; } = string.Empty;
    public string Parent2RaceId { get; set; } = string.Empty;
    public string ResolvedOriginKind { get; set; } = string.Empty;
    public string ResolvedOriginId { get; set; } = string.Empty;
    public string ResolvedOriginName { get; set; } = string.Empty;
    public string SubtypeId { get; set; } = string.Empty;
    public int HeightCm { get; set; }
    public int AgeAnchorYears { get; set; }
    public string AgeAnchorWorldDate { get; set; } = string.Empty;
    public int AgeAnchorWorldAbsoluteDay { get; set; }
    public int AgeAnchorWorldYearLengthDays { get; set; } = WorldCalendarDefaults.DaysPerYear;
    public Dictionary<string, int> AttributeAllocation { get; set; } = new Dictionary<string, int>();
    public Dictionary<string, int> SubAttributeAllocation { get; set; } = new Dictionary<string, int>();
    public Dictionary<string, int> LanguageAllocation { get; set; } = new Dictionary<string, int>();
    public string LanguageGrantProfileId { get; set; } = CharacterLanguageGrantProfileIds022Gate3.Custom;
    public string ReturnComment { get; set; } = string.Empty;
    public string FinalCharacterId { get; set; } = string.Empty;
    public string FinalizationOperationId { get; set; } = string.Empty;
    public long EntityRevision { get; set; } = 1;
    public long ValidationRevision { get; set; }
    public string UpdatedByUserId { get; set; } = string.Empty;
    public bool IsArchived { get; set; }
}

public sealed class CharacterOriginDefinition : EntityBase
{
    public string RuleSetId { get; set; } = RuleSetIds.FantasyNriDefault;
    public string DefinitionId { get; set; } = string.Empty;
    public string OriginKind { get; set; } = CharacterOriginKinds.Race;
    public string DisplayName { get; set; } = string.Empty;
    public string PublicDescription { get; set; } = string.Empty;
    public string GmDescription { get; set; } = string.Empty;
    public string Availability { get; set; } = CharacterOriginAvailabilityIds.Playable;
    public bool IsPlayerVisible { get; set; } = true;
    public bool IsArchived { get; set; }
    public bool ParentOrderMatters { get; set; }
    public string Parent1RaceId { get; set; } = string.Empty;
    public string Parent2RaceId { get; set; } = string.Empty;
    public int MinimumHeightCm { get; set; } = 50;
    public int MaximumHeightCm { get; set; } = 350;
    public int MinimumAgeYears { get; set; } = 1;
    public int MaximumAgeYears { get; set; } = 120;
    public int AdultAgeYears { get; set; } = 18;
    public int AverageLifespanYears { get; set; } = 75;
    public int MaximumLifespanYears { get; set; } = 120;
    public int BaseHealth { get; set; } = 100;
    public int NaturalArmorRating { get; set; } = 1;
    public int NaturalPenetrationResistance { get; set; } = 1;
    public List<string> StrongSides { get; set; } = new List<string>();
    public List<string> WeakSides { get; set; } = new List<string>();
    public List<string> PublicTraits { get; set; } = new List<string>();
    public List<string> TraitDefinitionIds { get; set; } = new List<string>();
    public List<string> GmOnlyTraits { get; set; } = new List<string>();
    public List<string> Languages { get; set; } = new List<string>();
    public List<string> KnowledgeGrants { get; set; } = new List<string>();
    public List<string> EquipmentCompatibilityTags { get; set; } = new List<string>();
    public List<BodyZoneDefinition> BodyZones { get; set; } = RacePhysiologyRules022Gate2.HumanoidZones();
    public RaceEquipmentFitProfile EquipmentFit { get; set; } = new RaceEquipmentFitProfile();
    public List<RacialSenseDefinition> Senses { get; set; } = new List<RacialSenseDefinition>();
    public List<RacialMovementAbilityDefinition> MovementAbilities { get; set; } = new List<RacialMovementAbilityDefinition>();
    public List<NaturalAttackDefinition> NaturalAttacks { get; set; } = new List<NaturalAttackDefinition>();
    public List<ElementalResistanceTier> ElementalResistances { get; set; } = new List<ElementalResistanceTier>();
    public List<EnvironmentalToleranceModifier> EnvironmentalToleranceModifiers { get; set; } = new List<EnvironmentalToleranceModifier>();
    public Dictionary<string, int> AttributeBonuses { get; set; } = new Dictionary<string, int>();
    public Dictionary<string, int> SubAttributeBonuses { get; set; } = new Dictionary<string, int>();
    public long EntityRevision { get; set; } = 1;
}

public sealed class CharacterOriginSubtypeDefinition : EntityBase
{
    public string RuleSetId { get; set; } = RuleSetIds.FantasyNriDefault;
    public string DefinitionId { get; set; } = string.Empty;
    public string OriginId { get; set; } = string.Empty;
    public string OriginKind { get; set; } = CharacterOriginKinds.Race;
    public string DisplayName { get; set; } = string.Empty;
    public string PublicDescription { get; set; } = string.Empty;
    public bool IsPlayerVisible { get; set; } = true;
    public bool IsGmOnly { get; set; }
    public string Availability { get; set; } = CharacterOriginAvailabilityIds.Playable;
    public bool IsArchived { get; set; }
    public int? MinimumHeightCm { get; set; }
    public int? MaximumHeightCm { get; set; }
    public int? MinimumAgeYears { get; set; }
    public int? MaximumAgeYears { get; set; }
    public int? AdultAgeYears { get; set; }
    public int? AverageLifespanYears { get; set; }
    public int? MaximumLifespanYears { get; set; }
    public int? BaseHealth { get; set; }
    public int? NaturalArmorRating { get; set; }
    public int? NaturalPenetrationResistance { get; set; }
    public string Parent1SubtypeId { get; set; } = string.Empty;
    public string Parent2SubtypeId { get; set; } = string.Empty;
    public string ElementalLineageId { get; set; } = string.Empty;
    public string InheritedAspectId { get; set; } = string.Empty;
    public string FlightInheritancePermissionId { get; set; } = string.Empty;
    public List<string> PublicTraits { get; set; } = new List<string>();
    public List<string> TraitDefinitionIds { get; set; } = new List<string>();
    public List<BodyZoneDefinition> BodyZones { get; set; } = new List<BodyZoneDefinition>();
    public RaceEquipmentFitProfile? EquipmentFit { get; set; }
    public List<RacialSenseDefinition> Senses { get; set; } = new List<RacialSenseDefinition>();
    public List<RacialMovementAbilityDefinition> MovementAbilities { get; set; } = new List<RacialMovementAbilityDefinition>();
    public List<NaturalAttackDefinition> NaturalAttacks { get; set; } = new List<NaturalAttackDefinition>();
    public List<ElementalResistanceTier> ElementalResistances { get; set; } = new List<ElementalResistanceTier>();
    public List<EnvironmentalToleranceModifier> EnvironmentalToleranceModifiers { get; set; } = new List<EnvironmentalToleranceModifier>();
    public Dictionary<string, int> AttributeBonuses { get; set; } = new Dictionary<string, int>();
    public Dictionary<string, int> SubAttributeBonuses { get; set; } = new Dictionary<string, int>();
    public long EntityRevision { get; set; } = 1;
}

public sealed class TitleDefinition : EntityBase
{
    public string RuleSetId { get; set; } = RuleSetIds.FantasyNriDefault;
    public string DefinitionId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string PublicDescription { get; set; } = string.Empty;
    public string GmDescription { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public bool IsPlayerVisible { get; set; } = true;
    public bool IsArchived { get; set; }
    public int SortOrder { get; set; }
    public long EntityRevision { get; set; } = 1;
}

public sealed class CharacterTitleEntitlement
{
    public string TitleId { get; set; } = string.Empty;
    public string GrantSourceType { get; set; } = string.Empty;
    public string GrantSourceId { get; set; } = string.Empty;
    public string GrantedByUserId { get; set; } = string.Empty;
    public DateTime GrantedAtUtc { get; set; } = DateTime.UtcNow;
    public bool IsRevoked { get; set; }
}

public sealed class CharacterTitleProfileDocument : EntityBase
{
    public string CharacterId { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = RuleSetIds.FantasyNriDefault;
    public string SelectedTitleId { get; set; } = string.Empty;
    public List<CharacterTitleEntitlement> Entitlements { get; set; } = new List<CharacterTitleEntitlement>();
    public long EntityRevision { get; set; } = 1;
}
