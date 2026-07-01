using System;
using System.Collections.Generic;
using System.Linq;
using Nri.Server.Application.Services;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;
using Nri.Shared.Utilities;

namespace Nri.Server.Application;

public partial class ServiceHub
{
    public ResponseEnvelope EconomyHoldingsAssetsCharacterBridge(CommandContext context)
    {
        var actor = RequireHoldingsAssetBridge(context);
        if (actor == null) return Error("holdings asset bridge disabled", ResponseStatus.Forbidden, ErrorCode.Forbidden);
        var request = ParseHoldingsAssetBridgeRequest(context.Request.Payload);
        var service = new HoldingsAssetBridgeService(_repositories, _logger);
        var result = service.BuildBridgeForCharacterAsync(request, actor).GetAwaiter().GetResult();
        return Ok("Holdings asset bridge built.", HoldingsAssetBridgePayload(result));
    }

    public ResponseEnvelope EconomyAssetsByCharacter(CommandContext context)
    {
        var actor = RequireHoldingsAssetBridge(context);
        if (actor == null) return Error("holdings asset bridge disabled", ResponseStatus.Forbidden, ErrorCode.Forbidden);
        var request = ParseHoldingsAssetBridgeRequest(context.Request.Payload);
        var service = new HoldingsAssetBridgeService(_repositories, _logger);
        var items = service.ListAssetsForCharacterAsync(request.CharacterId, request.CampaignId, request.Limit, request.Offset).GetAwaiter().GetResult();
        return Ok("Character economy assets loaded.", new Dictionary<string, object> { { "items", items.Select(EconomyAssetBridgeItemPayload).Cast<object>().ToArray() } });
    }

    public ResponseEnvelope EconomyAssetsByOrganization(CommandContext context)
    {
        var actor = RequireHoldingsAssetBridge(context);
        if (actor == null) return Error("holdings asset bridge disabled", ResponseStatus.Forbidden, ErrorCode.Forbidden);
        var organizationId = PayloadReader.GetString(context.Request.Payload, "organizationId") ?? PayloadReader.GetString(context.Request.Payload, "id") ?? string.Empty;
        var campaignId = PayloadReader.GetString(context.Request.Payload, "campaignId") ?? string.Empty;
        var limit = Math.Max(1, Math.Min(PayloadReader.GetInt(context.Request.Payload, "limit") ?? 100, 500));
        var offset = Math.Max(0, PayloadReader.GetInt(context.Request.Payload, "offset") ?? 0);
        var service = new HoldingsAssetBridgeService(_repositories, _logger);
        var items = service.ListAssetsForOrganizationAsync(organizationId, campaignId, limit, offset).GetAwaiter().GetResult();
        return Ok("Organization economy assets loaded.", new Dictionary<string, object> { { "items", items.Select(EconomyAssetBridgeItemPayload).Cast<object>().ToArray() } });
    }

    public ResponseEnvelope EconomyAssetsByFaction(CommandContext context)
    {
        var actor = RequireHoldingsAssetBridge(context);
        if (actor == null) return Error("holdings asset bridge disabled", ResponseStatus.Forbidden, ErrorCode.Forbidden);
        var factionId = PayloadReader.GetString(context.Request.Payload, "factionId") ?? PayloadReader.GetString(context.Request.Payload, "id") ?? string.Empty;
        var campaignId = PayloadReader.GetString(context.Request.Payload, "campaignId") ?? string.Empty;
        var limit = Math.Max(1, Math.Min(PayloadReader.GetInt(context.Request.Payload, "limit") ?? 100, 500));
        var offset = Math.Max(0, PayloadReader.GetInt(context.Request.Payload, "offset") ?? 0);
        var service = new HoldingsAssetBridgeService(_repositories, _logger);
        var items = service.ListAssetsForFactionAsync(factionId, campaignId, limit, offset).GetAwaiter().GetResult();
        return Ok("Faction economy assets loaded.", new Dictionary<string, object> { { "items", items.Select(EconomyAssetBridgeItemPayload).Cast<object>().ToArray() } });
    }

    private UserAccount? RequireHoldingsAssetBridge(CommandContext context)
    {
        try
        {
            var actor = RequireAdmin(context);
            if (!EconomyFeatureFlags.UseHoldingsAssetReadBridge)
            {
                _logger.Admin($"holdings.asset.bridge.disabled command={context.Request.Command}");
                return null;
            }

            return actor;
        }
        catch
        {
            _logger.Admin($"holdings.asset.bridge.forbidden command={context.Request.Command}");
            throw;
        }
    }

    private static HoldingsAssetBridgeRequest ParseHoldingsAssetBridgeRequest(IDictionary<string, object> payload)
    {
        return new HoldingsAssetBridgeRequest
        {
            CharacterId = PayloadReader.GetString(payload, "characterId") ?? PayloadReader.GetString(payload, "id") ?? string.Empty,
            CampaignId = PayloadReader.GetString(payload, "campaignId") ?? string.Empty,
            IncludeCharacterHoldings = GetBoolDefault(payload, "includeCharacterHoldings", true),
            IncludeEconomyAssets = GetBoolDefault(payload, "includeEconomyAssets", true),
            IncludePotentialLinks = GetBoolDefault(payload, "includePotentialLinks", true),
            Limit = Math.Max(1, Math.Min(PayloadReader.GetInt(payload, "limit") ?? 100, 500)),
            Offset = Math.Max(0, PayloadReader.GetInt(payload, "offset") ?? 0)
        };
    }

    private static Dictionary<string, object> HoldingsAssetBridgePayload(HoldingsAssetBridgeResponse response)
    {
        return new Dictionary<string, object>
        {
            { "characterId", response.CharacterId },
            { "campaignId", response.CampaignId },
            { "characterHoldings", response.CharacterHoldings.Select(CharacterHoldingBridgeItemPayload).Cast<object>().ToArray() },
            { "linkedAssets", response.LinkedAssets.Select(EconomyAssetBridgeItemPayload).Cast<object>().ToArray() },
            { "potentialLinks", response.PotentialLinks.Select(AssetPotentialLinkPayload).Cast<object>().ToArray() },
            { "warnings", response.Warnings.Cast<object>().ToArray() },
            { "builtAtUtc", response.BuiltAtUtc }
        };
    }

    private static Dictionary<string, object> CharacterHoldingBridgeItemPayload(CharacterHoldingBridgeItem item)
    {
        return new Dictionary<string, object>
        {
            { "holdingId", item.HoldingId },
            { "name", item.Name },
            { "holdingType", item.HoldingType },
            { "locationId", item.LocationId },
            { "countryId", item.CountryId },
            { "cityStateId", item.CityStateId },
            { "estimatedValueCurrencyId", item.EstimatedValueCurrencyId },
            { "estimatedValueAmount", item.EstimatedValueAmount },
            { "source", item.Source },
            { "notes", item.Notes }
        };
    }

    private static Dictionary<string, object> EconomyAssetBridgeItemPayload(EconomyAssetBridgeItem item)
    {
        return new Dictionary<string, object>
        {
            { "assetId", item.AssetId },
            { "definitionId", item.DefinitionId },
            { "name", item.Name },
            { "assetType", item.AssetType },
            { "locationId", item.LocationId },
            { "countryId", item.CountryId },
            { "cityStateId", item.CityStateId },
            { "ownerCharacterIds", item.OwnerCharacterIds.Cast<object>().ToArray() },
            { "ownerOrganizationIds", item.OwnerOrganizationIds.Cast<object>().ToArray() },
            { "ownerFactionIds", item.OwnerFactionIds.Cast<object>().ToArray() },
            { "estimatedValueCurrencyId", item.EstimatedValueCurrencyId },
            { "estimatedValueAmount", item.EstimatedValueAmount },
            { "legalStatus", item.LegalStatus },
            { "actualStatus", item.ActualStatus },
            { "isActive", item.IsActive }
        };
    }

    private static Dictionary<string, object> AssetPotentialLinkPayload(AssetPotentialLink link)
    {
        return new Dictionary<string, object>
        {
            { "holdingId", link.HoldingId },
            { "assetId", link.AssetId },
            { "confidence", link.Confidence },
            { "reason", link.Reason }
        };
    }
}
