using System;
using System.Collections.Generic;

namespace Nri.Shared.Domain;

public sealed class CharacterListCardView
{
    public string CharacterId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string OwnerUserId { get; set; } = string.Empty;
    public string Race { get; set; } = string.Empty;
    public string DevelopmentSummary { get; set; } = string.Empty;
    public string Health { get; set; } = string.Empty;
    public string Armor { get; set; } = string.Empty;
    public long XpCoins { get; set; }
    public string ProfileSource { get; set; } = "character_v2_profiles";
    public bool IsArchived { get; set; }
    public DateTime BuiltAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class PlayerCharacterView
{
    public string CharacterId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Race { get; set; } = string.Empty;
    public string Height { get; set; } = string.Empty;
    public IReadOnlyList<CharacterAttributeView> Attributes { get; set; } = Array.Empty<CharacterAttributeView>();
    public IReadOnlyList<CharacterCurrencyView> Currencies { get; set; } = Array.Empty<CharacterCurrencyView>();
    public CharacterExperienceCoinsView ExperienceCoins { get; set; } = new CharacterExperienceCoinsView();
    public IReadOnlyList<CharacterInventoryItemView> Inventory { get; set; } = Array.Empty<CharacterInventoryItemView>();
    public string ProfileSource { get; set; } = "character_v2_profiles";
    public DateTime BuiltAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class AdminCharacterEditorView
{
    public string CharacterId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string OwnerUserId { get; set; } = string.Empty;
    public IReadOnlyList<CharacterAttributeView> Attributes { get; set; } = Array.Empty<CharacterAttributeView>();
    public IReadOnlyList<CharacterCurrencyView> Currencies { get; set; } = Array.Empty<CharacterCurrencyView>();
    public CharacterExperienceCoinsView ExperienceCoins { get; set; } = new CharacterExperienceCoinsView();
    public IReadOnlyList<CharacterInventoryItemView> Inventory { get; set; } = Array.Empty<CharacterInventoryItemView>();
    public IReadOnlyList<string> MissingProfileSections { get; set; } = Array.Empty<string>();
    public string ProfileSource { get; set; } = "character_v2_profiles";
    public DateTime BuiltAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class CharacterAttributeView
{
    public string AttributeId { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int CurrentValue { get; set; }
    public int Value { get; set; }
    public int BaseValue { get; set; }
    public int MinValue { get; set; }
    public int MaxValue { get; set; }
    public int DefaultValue { get; set; }
    public int SortOrder { get; set; }
    public string AttributeSetId { get; set; } = string.Empty;
    public string SourceRuleSetId { get; set; } = string.Empty;
    public bool IsPlayerVisible { get; set; } = true;
    public bool IsEditableByGM { get; set; } = true;
}

public sealed class CharacterCurrencyView
{
    public string CurrencyId { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public long Amount { get; set; }
    public long DefaultValue { get; set; }
    public long MinValue { get; set; }
    public long? MaxValue { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string IconKey { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsPlayerVisible { get; set; } = true;
    public bool IsEditableByGM { get; set; } = true;
    public string SourceRuleSetId { get; set; } = string.Empty;
    public string SourceCurrencySetId { get; set; } = string.Empty;
}

public sealed class CharacterExperienceCoinsView
{
    public long Balance { get; set; }
    public long? TotalEarned { get; set; }
    public long? TotalSpent { get; set; }
    public string Source { get; set; } = "character_wallet_profiles";
    public DateTime? LastChangedAtUtc { get; set; }
    public bool IsEditableByGM { get; set; } = true;
    public bool IsPlayerVisible { get; set; } = true;
}

public sealed class CharacterInventoryItemView
{
    public string ItemId { get; set; } = string.Empty;
    public string ItemDefinitionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string SlotId { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public int Durability { get; set; }
    public string Condition { get; set; } = string.Empty;
    public int Ammo { get; set; }
    public bool IsEquipped { get; set; }
    public string Notes { get; set; } = string.Empty;
    public bool IsPlayerVisible { get; set; } = true;
    public int SortOrder { get; set; }
}
