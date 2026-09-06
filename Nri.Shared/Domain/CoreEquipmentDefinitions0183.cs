using System;
using System.Collections.Generic;

namespace Nri.Shared.Domain;

public static class CoreEquipmentDefinitionFamilies
{
    public static readonly string[] All =
    {
        DefinitionCategoryIds.Resource,
        DefinitionCategoryIds.Item,
        DefinitionCategoryIds.DamageType,
        DefinitionCategoryIds.Weapon,
        DefinitionCategoryIds.Ammo,
        DefinitionCategoryIds.Armor
    };

    public static bool IsSupported(string value)
    {
        foreach (var family in All)
        {
            if (string.Equals(family, value, StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }
}

public abstract class CoreEquipmentDefinitionProfile
{
    public string DefinitionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new List<string>();
    public string PublicDescription { get; set; } = string.Empty;
    public string GMDescription { get; set; } = string.Empty;
    public string VisibilityRule { get; set; } = VisibilityRuleIds.Public;
    public bool IsArchived { get; set; }
}

public sealed class ResourceDefinitionProfile : CoreEquipmentDefinitionProfile
{
    public string ResourceCategory { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public string PhysicalState { get; set; } = string.Empty;
    public decimal MassPerUnit { get; set; }
    public decimal VolumePerUnit { get; set; }
    public string Rarity { get; set; } = string.Empty;
    public bool SupportsQuality { get; set; }
    public decimal BaseValue { get; set; }
    public string Legality { get; set; } = string.Empty;
    public string StorageRequirements { get; set; } = string.Empty;
}

public sealed class ItemDefinitionProfile : CoreEquipmentDefinitionProfile
{
    public string ItemType { get; set; } = string.Empty;
    public decimal Mass { get; set; }
    public string Size { get; set; } = string.Empty;
    public bool Stackable { get; set; }
    public int MaxStack { get; set; } = 1;
    public int Durability { get; set; }
    public string Quality { get; set; } = string.Empty;
    public string Rarity { get; set; } = string.Empty;
    public decimal BaseValue { get; set; }
    public List<string> BodyCompatibilityTags { get; set; } = new List<string>();
    public string Legality { get; set; } = string.Empty;
}

public sealed class DamageTypeDefinitionProfile : CoreEquipmentDefinitionProfile
{
    public string Nature { get; set; } = string.Empty;
    public string Classification { get; set; } = string.Empty;
    public List<string> ResistanceTags { get; set; } = new List<string>();
    public List<string> VulnerabilityTags { get; set; } = new List<string>();
    public List<string> ImmunityTags { get; set; } = new List<string>();
}

public sealed class AttackProfileDefinition
{
    public string ProfileId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string AttackType { get; set; } = string.Empty;
    public int ActionCost { get; set; } = 1;
    public string AttackRollType { get; set; } = string.Empty;
    public string SkillDefinitionId { get; set; } = string.Empty;
    public string SubAttributeDefinitionId { get; set; } = string.Empty;
    public int AccuracyModifier { get; set; }
    public string Range { get; set; } = string.Empty;
    public string DamageExpression { get; set; } = string.Empty;
    public int DiceCount { get; set; } = 1;
    public int DieSides { get; set; } = 2;
    public int PerDieModifier { get; set; }
    public int TotalModifier { get; set; }
    public List<string> DamageTypeDefinitionIds { get; set; } = new List<string>();
    public int PhysicalPenetration { get; set; }
    public decimal FailedPenetrationDamageTransfer { get; set; }
    public int ArmorPenetration { get; set; }
    public int MagicPenetration { get; set; }
    public int MoralePenetration { get; set; }
    public string Area { get; set; } = string.Empty;
    public string AreaShape { get; set; } = "single";
    public decimal AreaAngleDegrees { get; set; }
    public decimal AreaWidthMeters { get; set; }
    public int CooldownRounds { get; set; }
    public bool FateEligibleForHitCheck { get; set; } = true;
    public bool FateEligibleForDamage { get; set; }
    public string FireMode { get; set; } = string.Empty;
    public int ReloadCost { get; set; }
    public int AmmoCost { get; set; }
    public bool CanReact { get; set; }
    public bool CanReturnFire { get; set; }
    public bool CanParry { get; set; }
    public bool CanBlock { get; set; }
}

public sealed class WeaponDefinitionProfile : CoreEquipmentDefinitionProfile
{
    public string WeaponCategory { get; set; } = string.Empty;
    public string Scale { get; set; } = string.Empty;
    public List<string> WeaponNatures { get; set; } = new List<string>();
    public List<string> RequiredSkillIds { get; set; } = new List<string>();
    public List<string> RequiredAttributeIds { get; set; } = new List<string>();
    public List<string> BodyRequirements { get; set; } = new List<string>();
    public string Range { get; set; } = string.Empty;
    public string ReloadRules { get; set; } = string.Empty;
    public List<string> AmmoDefinitionIds { get; set; } = new List<string>();
    public List<AttackProfileDefinition> AttackProfiles { get; set; } = new List<AttackProfileDefinition>();
    public string Legality { get; set; } = string.Empty;
}

public sealed class AmmoDefinitionProfile : CoreEquipmentDefinitionProfile
{
    public string AmmoType { get; set; } = string.Empty;
    public string Caliber { get; set; } = string.Empty;
    public List<string> CompatibilityTags { get; set; } = new List<string>();
    public List<string> AllowedWeaponIds { get; set; } = new List<string>();
    public List<string> ForbiddenWeaponIds { get; set; } = new List<string>();
    public List<string> RequiredFireModes { get; set; } = new List<string>();
    public List<string> DamageTypeAdditions { get; set; } = new List<string>();
    public List<string> DamageTypeReplacements { get; set; } = new List<string>();
    public int PhysicalPenetrationModifier { get; set; }
    public int ArmorPenetrationModifier { get; set; }
    public int MagicPenetrationModifier { get; set; }
    public int MoralePenetrationModifier { get; set; }
    public string ConsumptionModel { get; set; } = string.Empty;
    public string ChargeModel { get; set; } = string.Empty;
    public string Quality { get; set; } = string.Empty;
    public string FailureMetadata { get; set; } = string.Empty;
    public string Legality { get; set; } = string.Empty;
}

public sealed class ArmorDefinitionProfile : CoreEquipmentDefinitionProfile
{
    public string ArmorCategory { get; set; } = string.Empty;
    public List<string> ProtectedBodyZones { get; set; } = new List<string>();
    public List<string> BodyCompatibilityTags { get; set; } = new List<string>();
    public string DesignedSize { get; set; } = string.Empty;
    public int PhysicalDefense { get; set; }
    public int ArmorRating { get; set; }
    public Dictionary<string, int> PenetrationResistanceByBodyZone { get; set; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    public int MagicalDefense { get; set; }
    public List<string> SpecialResistanceTags { get; set; } = new List<string>();
    public int Durability { get; set; }
    public int StealthPenalty { get; set; }
    public int Noise { get; set; }
    public string Concealability { get; set; } = string.Empty;
    public int StrengthRequirement { get; set; }
    public string Legality { get; set; } = string.Empty;
    public bool HasShieldProfile { get; set; }
    public List<AttackProfileDefinition> ShieldAttackProfiles { get; set; } = new List<AttackProfileDefinition>();
}

public sealed class CoreEquipmentReferenceView
{
    public string DefinitionId { get; set; } = string.Empty;
    public string Family { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public bool IsPlayerVisible { get; set; }
    public bool IsArchived { get; set; }
}
