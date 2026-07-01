using System.Collections.Generic;

namespace Nri.Shared.Domain;

public sealed class ItemDefinitionView
{
    public string DefinitionId { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DisplayNameRu { get; set; } = string.Empty;
    public string ItemType { get; set; } = string.Empty;
    public decimal WeightKg { get; set; }
    public bool Stackable { get; set; }
    public int MaxStack { get; set; } = 1;
    public int DefaultQuantity { get; set; } = 1;
    public string ValueCurrencyId { get; set; } = string.Empty;
    public long ValueAmountDraft { get; set; }
    public bool IsConsumable { get; set; }
    public bool IsMagical { get; set; }
    public bool IsRestricted { get; set; }
    public List<string> Tags { get; set; } = new List<string>();
    public List<string> SourceDefinitionTags { get; set; } = new List<string>();
    public int SchemaVersion { get; set; }
}

public sealed class WeaponDefinitionView
{
    public string DefinitionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DisplayNameRu { get; set; } = string.Empty;
    public string WeaponType { get; set; } = string.Empty;
    public string Handedness { get; set; } = string.Empty;
    public string RangeType { get; set; } = string.Empty;
    public string DamageDraft { get; set; } = string.Empty;
    public string AccuracyDraft { get; set; } = string.Empty;
    public string PenetrationDraft { get; set; } = string.Empty;
    public List<string> LinkedSkillIds { get; set; } = new List<string>();
    public List<string> AttributeHints { get; set; } = new List<string>();
    public List<string> AmmoDefinitionIds { get; set; } = new List<string>();
    public List<string> EquipmentSlotIds { get; set; } = new List<string>();
    public decimal WeightKg { get; set; }
    public string ValueCurrencyId { get; set; } = string.Empty;
    public long ValueAmountDraft { get; set; }
    public List<string> TechTags { get; set; } = new List<string>();
    public List<string> MagicTags { get; set; } = new List<string>();
    public List<string> LegalTags { get; set; } = new List<string>();
    public List<string> Tags { get; set; } = new List<string>();
    public int SchemaVersion { get; set; }
}

public sealed class ArmorDefinitionView
{
    public string DefinitionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DisplayNameRu { get; set; } = string.Empty;
    public string ArmorType { get; set; } = string.Empty;
    public List<string> EquipmentSlotIds { get; set; } = new List<string>();
    public string PhysicalArmorDraft { get; set; } = string.Empty;
    public string MagicArmorDraft { get; set; } = string.Empty;
    public string MobilityPenaltyDraft { get; set; } = string.Empty;
    public string StealthPenaltyDraft { get; set; } = string.Empty;
    public string HeightFitMode { get; set; } = string.Empty;
    public List<string> SizeCategoryAllowed { get; set; } = new List<string>();
    public decimal WeightKg { get; set; }
    public string ValueCurrencyId { get; set; } = string.Empty;
    public long ValueAmountDraft { get; set; }
    public List<string> Tags { get; set; } = new List<string>();
    public int SchemaVersion { get; set; }
}

public sealed class AmmoDefinitionView
{
    public string DefinitionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DisplayNameRu { get; set; } = string.Empty;
    public string AmmoType { get; set; } = string.Empty;
    public List<string> CompatibleWeaponIds { get; set; } = new List<string>();
    public bool Stackable { get; set; }
    public int MaxStack { get; set; } = 1;
    public string DamageModifierDraft { get; set; } = string.Empty;
    public string PenetrationModifierDraft { get; set; } = string.Empty;
    public bool IsMagical { get; set; }
    public bool IsConsumable { get; set; }
    public string ValueCurrencyId { get; set; } = string.Empty;
    public long ValueAmountDraft { get; set; }
    public List<string> Tags { get; set; } = new List<string>();
    public int SchemaVersion { get; set; }
}

public sealed class EquipmentSlotDefinitionView
{
    public string DefinitionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DisplayNameRu { get; set; } = string.Empty;
    public string SlotGroup { get; set; } = string.Empty;
    public int MaxItems { get; set; } = 1;
    public bool IsBodySlot { get; set; }
    public bool IsContainerSlot { get; set; }
    public List<string> Tags { get; set; } = new List<string>();
    public int SchemaVersion { get; set; }
}

public sealed class DefinitionResolveResult<T>
{
    public bool Success { get; set; }
    public string DefinitionId { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public T Value { get; set; } = default!;
    public List<string> Errors { get; set; } = new List<string>();
    public List<string> Warnings { get; set; } = new List<string>();
}

public sealed class DefinitionBatchResolveResult<T>
{
    public bool Success { get; set; }
    public List<T> Values { get; set; } = new List<T>();
    public List<string> Errors { get; set; } = new List<string>();
    public List<string> Warnings { get; set; } = new List<string>();
}
