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
    public ResponseEnvelope EconomyRelationsGraph(CommandContext context)
    {
        var actor = RequireRelationRead(context);
        if (actor == null) return Error("economy runtime relation read disabled", ResponseStatus.Forbidden, ErrorCode.Forbidden);
        var request = ParseRelationGraphRequest(context.Request.Payload);
        if (string.IsNullOrWhiteSpace(request.CampaignId)) return Error("campaignId is required", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);
        if (string.IsNullOrWhiteSpace(request.RootType)) return Error("rootType is required", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);
        if (string.IsNullOrWhiteSpace(request.RootId)) return Error("rootId is required", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);

        var service = new EconomyRuntimeRelationService(_repositories, _logger);
        var result = service.BuildGraphAsync(request, actor).GetAwaiter().GetResult();
        return Ok("Economy relation graph built.", EconomyRelationGraphPayload(result));
    }

    public ResponseEnvelope EconomyRelationsFaction(CommandContext context) => EconomyRelationsForRoot(context, EconomyRuntimeKinds.Faction, "factionId");
    public ResponseEnvelope EconomyRelationsOrganization(CommandContext context) => EconomyRelationsForRoot(context, EconomyRuntimeKinds.Organization, "organizationId");
    public ResponseEnvelope EconomyRelationsCountry(CommandContext context) => EconomyRelationsForRoot(context, "country", "countryId");
    public ResponseEnvelope EconomyRelationsCityState(CommandContext context) => EconomyRelationsForRoot(context, "cityState", "cityStateId");
    public ResponseEnvelope EconomyRelationsLocation(CommandContext context) => EconomyRelationsForRoot(context, "location", "locationId");

    private ResponseEnvelope EconomyRelationsForRoot(CommandContext context, string rootType, string fieldName)
    {
        var actor = RequireRelationRead(context);
        if (actor == null) return Error("economy runtime relation read disabled", ResponseStatus.Forbidden, ErrorCode.Forbidden);
        var campaignId = PayloadReader.GetString(context.Request.Payload, "campaignId") ?? string.Empty;
        var rootId = FirstNonEmpty(PayloadReader.GetString(context.Request.Payload, fieldName) ?? string.Empty, PayloadReader.GetString(context.Request.Payload, "id") ?? string.Empty, PayloadReader.GetString(context.Request.Payload, "rootId") ?? string.Empty);
        if (string.IsNullOrWhiteSpace(campaignId)) return Error("campaignId is required", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);
        if (string.IsNullOrWhiteSpace(rootId)) return Error(fieldName + " is required", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);

        var request = ParseRelationGraphRequest(context.Request.Payload);
        request.CampaignId = campaignId;
        request.RootType = rootType;
        request.RootId = rootId;

        var service = new EconomyRuntimeRelationService(_repositories, _logger);
        var result = service.BuildGraphAsync(request, actor).GetAwaiter().GetResult();
        return Ok("Economy relation graph built.", EconomyRelationGraphPayload(result));
    }

    private UserAccount? RequireRelationRead(CommandContext context)
    {
        try
        {
            var actor = RequireAdmin(context);
            if (!EconomyFeatureFlags.UseEconomyRuntimeRelationRead)
            {
                _logger.Admin($"economy.relations.disabled command={context.Request.Command}");
                return null;
            }

            return actor;
        }
        catch
        {
            _logger.Admin($"economy.relations.forbidden command={context.Request.Command}");
            throw;
        }
    }

    private static EconomyRelationGraphRequest ParseRelationGraphRequest(IDictionary<string, object> payload)
    {
        return new EconomyRelationGraphRequest
        {
            CampaignId = PayloadReader.GetString(payload, "campaignId") ?? string.Empty,
            RootType = PayloadReader.GetString(payload, "rootType") ?? string.Empty,
            RootId = PayloadReader.GetString(payload, "rootId") ?? string.Empty,
            IncludeFactions = GetBoolDefault(payload, "includeFactions", true),
            IncludeOrganizations = GetBoolDefault(payload, "includeOrganizations", true),
            IncludeMarkets = GetBoolDefault(payload, "includeMarkets", true),
            IncludeLaws = GetBoolDefault(payload, "includeLaws", true),
            IncludeRestrictions = GetBoolDefault(payload, "includeRestrictions", true),
            IncludeScopes = GetBoolDefault(payload, "includeScopes", true),
            MaxDepth = Math.Max(0, Math.Min(PayloadReader.GetInt(payload, "maxDepth") ?? 2, 5)),
            Limit = Math.Max(1, Math.Min(PayloadReader.GetInt(payload, "limit") ?? 200, 500))
        };
    }

    private static Dictionary<string, object> EconomyRelationGraphPayload(EconomyRelationGraphResponse response)
    {
        return new Dictionary<string, object>
        {
            { "campaignId", response.CampaignId },
            { "rootType", response.RootType },
            { "rootId", response.RootId },
            { "nodes", response.Nodes.Select(EconomyRelationNodePayload).Cast<object>().ToArray() },
            { "edges", response.Edges.Select(EconomyRelationEdgePayload).Cast<object>().ToArray() },
            { "warnings", response.Warnings.Cast<object>().ToArray() },
            { "builtAtUtc", response.BuiltAtUtc }
        };
    }

    private static Dictionary<string, object> EconomyRelationNodePayload(EconomyRelationNode node)
    {
        return new Dictionary<string, object>
        {
            { "id", node.Id },
            { "runtimeType", node.RuntimeType },
            { "definitionId", node.DefinitionId },
            { "name", node.Name },
            { "visibility", node.Visibility },
            { "tags", node.Tags.Cast<object>().ToArray() }
        };
    }

    private static Dictionary<string, object> EconomyRelationEdgePayload(EconomyRelationEdge edge)
    {
        return new Dictionary<string, object>
        {
            { "fromId", edge.FromId },
            { "toId", edge.ToId },
            { "relationType", edge.RelationType },
            { "direction", edge.Direction },
            { "sourceField", edge.SourceField },
            { "isExplicit", edge.IsExplicit }
        };
    }
}
