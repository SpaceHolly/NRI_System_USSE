using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nri.Server.Infrastructure;
using Nri.Server.Logging;
using Nri.Shared.Domain;

namespace Nri.Server.Application.Services;

public interface IHoldingsAssetBridgeService
{
    Task<HoldingsAssetBridgeResponse> BuildBridgeForCharacterAsync(HoldingsAssetBridgeRequest request, UserAccount actor);
    Task<List<EconomyAssetBridgeItem>> ListAssetsForCharacterAsync(string characterId, string campaignId, int limit, int offset);
    Task<List<CharacterHoldingBridgeItem>> ListCharacterHoldingsAsync(string characterId);
    List<AssetPotentialLink> FindPotentialLinksAsync(IEnumerable<CharacterHoldingBridgeItem> characterHoldings, IEnumerable<EconomyAssetBridgeItem> assetStates);
}

public sealed class HoldingsAssetBridgeService : IHoldingsAssetBridgeService
{
    private readonly INriRepositoryFactory _repositories;
    private readonly IServerLogger _logger;

    public HoldingsAssetBridgeService(INriRepositoryFactory repositories, IServerLogger logger)
    {
        _repositories = repositories;
        _logger = logger;
    }

    public async Task<HoldingsAssetBridgeResponse> BuildBridgeForCharacterAsync(HoldingsAssetBridgeRequest request, UserAccount actor)
    {
        var safeRequest = Normalize(request);
        var response = new HoldingsAssetBridgeResponse
        {
            CharacterId = safeRequest.CharacterId,
            CampaignId = safeRequest.CampaignId,
            BuiltAtUtc = DateTime.UtcNow
        };

        _logger.Debug($"holdings.asset.bridge.start characterId={safeRequest.CharacterId} campaignId={safeRequest.CampaignId}");
        if (string.IsNullOrWhiteSpace(safeRequest.CharacterId))
        {
            response.Warnings.Add("character_id_required");
            return response;
        }

        if (string.IsNullOrWhiteSpace(safeRequest.CampaignId))
        {
            response.Warnings.Add("campaign_id_required");
            return response;
        }

        if (safeRequest.IncludeCharacterHoldings)
        {
            response.CharacterHoldings = await ListCharacterHoldingsAsync(safeRequest.CharacterId);
            if (response.CharacterHoldings.Count == 0 && _repositories.Characters.GetById(safeRequest.CharacterId) == null)
            {
                response.Warnings.Add("character_not_found");
            }
        }

        if (safeRequest.IncludeEconomyAssets)
        {
            response.LinkedAssets = await ListAssetsForCharacterAsync(safeRequest.CharacterId, safeRequest.CampaignId, safeRequest.Limit, safeRequest.Offset);
        }

        if (safeRequest.IncludePotentialLinks)
        {
            response.PotentialLinks = FindPotentialLinksAsync(response.CharacterHoldings, response.LinkedAssets);
        }

        _logger.Debug($"holdings.asset.bridge.done characterId={safeRequest.CharacterId} holdings={response.CharacterHoldings.Count} assets={response.LinkedAssets.Count} potentialLinks={response.PotentialLinks.Count}");
        return response;
    }

    public Task<List<EconomyAssetBridgeItem>> ListAssetsForCharacterAsync(string characterId, string campaignId, int limit, int offset)
    {
        return ListAssetsAsync(campaignId, limit, offset, asset => Contains(asset.OwnerCharacterIds, characterId));
    }

    public async Task<List<CharacterHoldingBridgeItem>> ListCharacterHoldingsAsync(string characterId)
    {
        var character = _repositories.Characters.GetById(characterId);
        if (character == null) return await Task.FromResult(new List<CharacterHoldingBridgeItem>());
        var items = (character.Holdings ?? new List<HoldingRef>())
            .Where(x => x != null && !x.Archived)
            .Select(ToHoldingBridgeItem)
            .ToList();
        return await Task.FromResult(items);
    }

    public List<AssetPotentialLink> FindPotentialLinksAsync(IEnumerable<CharacterHoldingBridgeItem> characterHoldings, IEnumerable<EconomyAssetBridgeItem> assetStates)
    {
        var result = new List<AssetPotentialLink>();
        foreach (var holding in characterHoldings ?? Enumerable.Empty<CharacterHoldingBridgeItem>())
        {
            foreach (var asset in assetStates ?? Enumerable.Empty<EconomyAssetBridgeItem>())
            {
                var link = BuildPotentialLink(holding, asset);
                if (link != null) result.Add(link);
            }
        }

        return result
            .GroupBy(x => x.HoldingId + ":" + x.AssetId, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .ToList();
    }

    public Task<List<EconomyAssetBridgeItem>> ListAssetsForOrganizationAsync(string organizationId, string campaignId, int limit, int offset)
    {
        return ListAssetsAsync(campaignId, limit, offset, asset => Contains(asset.OwnerOrganizationIds, organizationId));
    }

    public Task<List<EconomyAssetBridgeItem>> ListAssetsForFactionAsync(string factionId, string campaignId, int limit, int offset)
    {
        return ListAssetsAsync(campaignId, limit, offset, asset => Contains(asset.OwnerFactionIds, factionId));
    }

    private async Task<List<EconomyAssetBridgeItem>> ListAssetsAsync(string campaignId, int limit, int offset, Func<AssetState, bool> predicate)
    {
        if (string.IsNullOrWhiteSpace(campaignId)) return new List<EconomyAssetBridgeItem>();
        var safeLimit = Math.Max(1, Math.Min(limit <= 0 ? 100 : limit, 500));
        var safeOffset = Math.Max(0, offset);
        var assets = await _repositories.AssetStates.ListByCampaignAsync(campaignId, 500);
        return assets
            .Where(x => x != null && x.IsActive && predicate(x))
            .Skip(safeOffset)
            .Take(safeLimit)
            .Select(ToAssetBridgeItem)
            .ToList();
    }

    private static HoldingsAssetBridgeRequest Normalize(HoldingsAssetBridgeRequest request)
    {
        var safe = request ?? new HoldingsAssetBridgeRequest();
        safe.CharacterId = safe.CharacterId ?? string.Empty;
        safe.CampaignId = safe.CampaignId ?? string.Empty;
        safe.Limit = Math.Max(1, Math.Min(safe.Limit <= 0 ? 100 : safe.Limit, 500));
        safe.Offset = Math.Max(0, safe.Offset);
        return safe;
    }

    private static CharacterHoldingBridgeItem ToHoldingBridgeItem(HoldingRef holding)
    {
        return new CharacterHoldingBridgeItem
        {
            HoldingId = holding.Id ?? string.Empty,
            Name = holding.Name ?? string.Empty,
            HoldingType = holding.Type ?? string.Empty,
            LocationId = string.Empty,
            CountryId = string.Empty,
            CityStateId = string.Empty,
            EstimatedValueCurrencyId = string.Empty,
            EstimatedValueAmount = 0,
            Source = "legacy.character.holdings",
            Notes = holding.Notes ?? string.Empty
        };
    }

    private static EconomyAssetBridgeItem ToAssetBridgeItem(AssetState asset)
    {
        return new EconomyAssetBridgeItem
        {
            AssetId = asset.Id,
            DefinitionId = asset.DefinitionId,
            Name = asset.Name,
            AssetType = asset.AssetType,
            LocationId = asset.LocationId,
            CountryId = asset.CountryId,
            CityStateId = asset.CityStateId,
            OwnerCharacterIds = asset.OwnerCharacterIds ?? new List<string>(),
            OwnerOrganizationIds = asset.OwnerOrganizationIds ?? new List<string>(),
            OwnerFactionIds = asset.OwnerFactionIds ?? new List<string>(),
            EstimatedValueCurrencyId = asset.EstimatedValueCurrencyId,
            EstimatedValueAmount = asset.EstimatedValueAmount,
            LegalStatus = asset.LegalStatus,
            ActualStatus = asset.ActualStatus,
            IsActive = asset.IsActive
        };
    }

    private static AssetPotentialLink? BuildPotentialLink(CharacterHoldingBridgeItem holding, EconomyAssetBridgeItem asset)
    {
        if (!string.IsNullOrWhiteSpace(holding.LocationId) && string.Equals(holding.LocationId, asset.LocationId, StringComparison.OrdinalIgnoreCase))
        {
            return Link(holding, asset, "high", "location_id_match");
        }

        if (!string.IsNullOrWhiteSpace(holding.CountryId)
            && string.Equals(holding.CountryId, asset.CountryId, StringComparison.OrdinalIgnoreCase)
            && NamesSimilar(holding.Name, asset.Name))
        {
            return Link(holding, asset, "medium", "country_id_and_name_match");
        }

        if ((!string.IsNullOrWhiteSpace(holding.CountryId) && string.Equals(holding.CountryId, asset.CountryId, StringComparison.OrdinalIgnoreCase))
            || (!string.IsNullOrWhiteSpace(holding.CityStateId) && string.Equals(holding.CityStateId, asset.CityStateId, StringComparison.OrdinalIgnoreCase)))
        {
            return Link(holding, asset, "low", "country_or_city_state_match");
        }

        return null;
    }

    private static AssetPotentialLink Link(CharacterHoldingBridgeItem holding, EconomyAssetBridgeItem asset, string confidence, string reason)
    {
        return new AssetPotentialLink
        {
            HoldingId = holding.HoldingId,
            AssetId = asset.AssetId,
            Confidence = confidence,
            Reason = reason
        };
    }

    private static bool NamesSimilar(string left, string right)
    {
        var a = NormalizeName(left);
        var b = NormalizeName(right);
        return !string.IsNullOrWhiteSpace(a)
            && !string.IsNullOrWhiteSpace(b)
            && (a.Contains(b) || b.Contains(a));
    }

    private static string NormalizeName(string value)
        => new string((value ?? string.Empty).Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private static bool Contains(IEnumerable<string> values, string expected)
    {
        return !string.IsNullOrWhiteSpace(expected)
            && (values ?? Enumerable.Empty<string>()).Any(x => string.Equals(x, expected, StringComparison.OrdinalIgnoreCase));
    }
}
