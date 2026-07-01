using System;
using System.Collections.Generic;

namespace Nri.Shared.Domain;

public sealed class HoldingsAssetBridgeRequest
{
    public string CharacterId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public bool IncludeCharacterHoldings { get; set; } = true;
    public bool IncludeEconomyAssets { get; set; } = true;
    public bool IncludePotentialLinks { get; set; } = true;
    public int Limit { get; set; } = 100;
    public int Offset { get; set; }
}

public sealed class HoldingsAssetBridgeResponse
{
    public string CharacterId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public List<CharacterHoldingBridgeItem> CharacterHoldings { get; set; } = new List<CharacterHoldingBridgeItem>();
    public List<EconomyAssetBridgeItem> LinkedAssets { get; set; } = new List<EconomyAssetBridgeItem>();
    public List<AssetPotentialLink> PotentialLinks { get; set; } = new List<AssetPotentialLink>();
    public List<string> Warnings { get; set; } = new List<string>();
    public DateTime BuiltAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class CharacterHoldingBridgeItem
{
    public string HoldingId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string HoldingType { get; set; } = string.Empty;
    public string LocationId { get; set; } = string.Empty;
    public string CountryId { get; set; } = string.Empty;
    public string CityStateId { get; set; } = string.Empty;
    public string EstimatedValueCurrencyId { get; set; } = string.Empty;
    public long EstimatedValueAmount { get; set; }
    public string Source { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
}

public sealed class EconomyAssetBridgeItem
{
    public string AssetId { get; set; } = string.Empty;
    public string DefinitionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string AssetType { get; set; } = string.Empty;
    public string LocationId { get; set; } = string.Empty;
    public string CountryId { get; set; } = string.Empty;
    public string CityStateId { get; set; } = string.Empty;
    public List<string> OwnerCharacterIds { get; set; } = new List<string>();
    public List<string> OwnerOrganizationIds { get; set; } = new List<string>();
    public List<string> OwnerFactionIds { get; set; } = new List<string>();
    public string EstimatedValueCurrencyId { get; set; } = string.Empty;
    public long EstimatedValueAmount { get; set; }
    public string LegalStatus { get; set; } = string.Empty;
    public string ActualStatus { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public sealed class AssetPotentialLink
{
    public string HoldingId { get; set; } = string.Empty;
    public string AssetId { get; set; } = string.Empty;
    public string Confidence { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}
