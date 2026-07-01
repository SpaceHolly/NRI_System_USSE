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
    public ResponseEnvelope EconomyFactionsList(CommandContext context) => EconomyRuntimeList(context, EconomyRuntimeKinds.Faction);
    public ResponseEnvelope EconomyFactionGet(CommandContext context) => EconomyRuntimeGet(context, EconomyRuntimeKinds.Faction);
    public ResponseEnvelope EconomyOrganizationsList(CommandContext context) => EconomyRuntimeList(context, EconomyRuntimeKinds.Organization);
    public ResponseEnvelope EconomyOrganizationGet(CommandContext context) => EconomyRuntimeGet(context, EconomyRuntimeKinds.Organization);
    public ResponseEnvelope EconomyMarketsList(CommandContext context) => EconomyRuntimeList(context, EconomyRuntimeKinds.Market);
    public ResponseEnvelope EconomyMarketGet(CommandContext context) => EconomyRuntimeGet(context, EconomyRuntimeKinds.Market);
    public ResponseEnvelope EconomyLawsList(CommandContext context) => EconomyRuntimeList(context, EconomyRuntimeKinds.Law);
    public ResponseEnvelope EconomyLawGet(CommandContext context) => EconomyRuntimeGet(context, EconomyRuntimeKinds.Law);
    public ResponseEnvelope EconomyRestrictionsList(CommandContext context) => EconomyRuntimeList(context, EconomyRuntimeKinds.Restriction);
    public ResponseEnvelope EconomyRestrictionGet(CommandContext context) => EconomyRuntimeGet(context, EconomyRuntimeKinds.Restriction);
    public ResponseEnvelope EconomyScopesList(CommandContext context) => EconomyRuntimeList(context, EconomyRuntimeKinds.EconomyScope);
    public ResponseEnvelope EconomyScopeGet(CommandContext context) => EconomyRuntimeGet(context, EconomyRuntimeKinds.EconomyScope);

    private ResponseEnvelope EconomyRuntimeList(CommandContext context, string runtimeType)
    {
        UserAccount actor;
        try
        {
            actor = RequireAdmin(context);
        }
        catch
        {
            _logger.Admin($"economy.read.forbidden command={context.Request.Command} actor=unknown");
            throw;
        }

        if (!EconomyFeatureFlags.UseEconomyRuntimeReadEndpoints)
        {
            _logger.Admin($"economy.read.disabled command={context.Request.Command}");
            return Error("economy runtime read endpoints disabled", ResponseStatus.Forbidden, ErrorCode.Forbidden);
        }

        var request = ParseEconomyRuntimeListRequest(context.Request.Payload);
        if (string.IsNullOrWhiteSpace(request.CampaignId))
        {
            return Error("campaignId is required", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);
        }

        _logger.Debug($"economy.read.list.start type={runtimeType} campaignId={request.CampaignId} limit={request.Limit}");
        var service = new EconomyRuntimeReadService(_repositories, _logger);
        var result = runtimeType switch
        {
            EconomyRuntimeKinds.Faction => service.ListFactionsAsync(request, actor).GetAwaiter().GetResult(),
            EconomyRuntimeKinds.Organization => service.ListOrganizationsAsync(request, actor).GetAwaiter().GetResult(),
            EconomyRuntimeKinds.Market => service.ListMarketsAsync(request, actor).GetAwaiter().GetResult(),
            EconomyRuntimeKinds.Law => service.ListLawsAsync(request, actor).GetAwaiter().GetResult(),
            EconomyRuntimeKinds.Restriction => service.ListRestrictionsAsync(request, actor).GetAwaiter().GetResult(),
            EconomyRuntimeKinds.EconomyScope => service.ListEconomyScopesAsync(request, actor).GetAwaiter().GetResult(),
            _ => throw new InvalidOperationException("Unsupported economy runtime type.")
        };

        return Ok("Economy runtime states loaded.", EconomyRuntimeListPayload(result));
    }

    private ResponseEnvelope EconomyRuntimeGet(CommandContext context, string runtimeType)
    {
        UserAccount actor;
        try
        {
            actor = RequireAdmin(context);
        }
        catch
        {
            _logger.Admin($"economy.read.forbidden command={context.Request.Command} actor=unknown");
            throw;
        }

        if (!EconomyFeatureFlags.UseEconomyRuntimeReadEndpoints)
        {
            _logger.Admin($"economy.read.disabled command={context.Request.Command}");
            return Error("economy runtime read endpoints disabled", ResponseStatus.Forbidden, ErrorCode.Forbidden);
        }

        var id = PayloadReader.GetString(context.Request.Payload, "id") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(id))
        {
            return Error("id is required", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);
        }

        var campaignId = PayloadReader.GetString(context.Request.Payload, "campaignId") ?? string.Empty;
        _logger.Debug($"economy.read.get.start type={runtimeType} id={id}");
        var service = new EconomyRuntimeReadService(_repositories, _logger);
        var details = runtimeType switch
        {
            EconomyRuntimeKinds.Faction => service.GetFactionAsync(id, actor).GetAwaiter().GetResult(),
            EconomyRuntimeKinds.Organization => service.GetOrganizationAsync(id, actor).GetAwaiter().GetResult(),
            EconomyRuntimeKinds.Market => service.GetMarketAsync(id, actor).GetAwaiter().GetResult(),
            EconomyRuntimeKinds.Law => service.GetLawAsync(id, actor).GetAwaiter().GetResult(),
            EconomyRuntimeKinds.Restriction => service.GetRestrictionAsync(id, actor).GetAwaiter().GetResult(),
            EconomyRuntimeKinds.EconomyScope => service.GetEconomyScopeAsync(id, actor).GetAwaiter().GetResult(),
            _ => throw new InvalidOperationException("Unsupported economy runtime type.")
        };

        if (details == null || (!string.IsNullOrWhiteSpace(campaignId) && !string.Equals(details.CampaignId, campaignId, StringComparison.OrdinalIgnoreCase)))
        {
            _logger.Debug($"economy.read.get.done type={runtimeType} found=false");
            return Error("Economy runtime state not found.", ResponseStatus.NotFound, ErrorCode.NotFound);
        }

        _logger.Debug($"economy.read.get.done type={runtimeType} found=true");
        return Ok("Economy runtime state loaded.", new Dictionary<string, object> { { "item", EconomyRuntimeDetailsPayload(details) } });
    }

    private static EconomyRuntimeListRequest ParseEconomyRuntimeListRequest(IDictionary<string, object> payload)
    {
        return new EconomyRuntimeListRequest
        {
            CampaignId = PayloadReader.GetString(payload, "campaignId") ?? string.Empty,
            RuleSetId = PayloadReader.GetString(payload, "ruleSetId") ?? string.Empty,
            CountryId = PayloadReader.GetString(payload, "countryId") ?? string.Empty,
            CityStateId = PayloadReader.GetString(payload, "cityStateId") ?? string.Empty,
            LocationId = PayloadReader.GetString(payload, "locationId") ?? string.Empty,
            DefinitionId = PayloadReader.GetString(payload, "definitionId") ?? string.Empty,
            IncludeArchived = PayloadReader.GetBool(payload, "includeArchived"),
            Limit = Math.Max(1, Math.Min(PayloadReader.GetInt(payload, "limit") ?? 100, 500)),
            Offset = Math.Max(0, PayloadReader.GetInt(payload, "offset") ?? 0)
        };
    }

    private static Dictionary<string, object> EconomyRuntimeListPayload(EconomyRuntimeListResponse response)
    {
        return new Dictionary<string, object>
        {
            { "items", response.Items.Select(EconomyRuntimeSummaryPayload).Cast<object>().ToArray() },
            { "total", response.Total },
            { "limit", response.Limit },
            { "offset", response.Offset },
            { "hasMore", response.HasMore }
        };
    }

    private static Dictionary<string, object> EconomyRuntimeSummaryPayload(EconomyRuntimeStateSummary item)
    {
        return new Dictionary<string, object>
        {
            { "id", item.Id },
            { "runtimeType", item.RuntimeType },
            { "definitionId", item.DefinitionId },
            { "name", item.Name },
            { "campaignId", item.CampaignId },
            { "ruleSetId", item.RuleSetId },
            { "countryId", item.CountryId },
            { "cityStateId", item.CityStateId },
            { "locationId", item.LocationId },
            { "tags", item.Tags.Cast<object>().ToArray() },
            { "isActive", item.IsActive },
            { "isArchived", item.IsArchived },
            { "visibility", item.Visibility }
        };
    }

    private static Dictionary<string, object> EconomyRuntimeDetailsPayload(EconomyRuntimeStateDetails item)
    {
        return new Dictionary<string, object>
        {
            { "id", item.Id },
            { "runtimeType", item.RuntimeType },
            { "definitionId", item.DefinitionId },
            { "name", item.Name },
            { "campaignId", item.CampaignId },
            { "ruleSetId", item.RuleSetId },
            { "countryId", item.CountryId },
            { "cityStateId", item.CityStateId },
            { "locationIds", item.LocationIds.Cast<object>().ToArray() },
            { "locationId", item.LocationId },
            { "tags", item.Tags.Cast<object>().ToArray() },
            { "notes", item.Notes },
            { "publicFields", item.PublicFields },
            { "hiddenFields", item.HiddenFields },
            { "schemaVersion", item.SchemaVersion }
        };
    }
}
