using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nri.Server.Infrastructure;
using Nri.Server.Logging;
using Nri.Shared.Domain;

namespace Nri.Server.Application.Services;

public interface IEconomyRuntimeRelationService
{
    Task<EconomyRelationGraphResponse> BuildGraphAsync(EconomyRelationGraphRequest request, UserAccount actor);
    Task<EconomyRelationGraphResponse> GetFactionRelationsAsync(string campaignId, string factionId, UserAccount actor);
    Task<EconomyRelationGraphResponse> GetOrganizationRelationsAsync(string campaignId, string organizationId, UserAccount actor);
    Task<EconomyRelationGraphResponse> GetCountryRelationsAsync(string campaignId, string countryId, UserAccount actor);
    Task<EconomyRelationGraphResponse> GetCityStateRelationsAsync(string campaignId, string cityStateId, UserAccount actor);
    Task<EconomyRelationGraphResponse> GetLocationRelationsAsync(string campaignId, string locationId, UserAccount actor);
}

public sealed class EconomyRuntimeRelationService : IEconomyRuntimeRelationService
{
    private readonly INriRepositoryFactory _repositories;
    private readonly IServerLogger _logger;

    public EconomyRuntimeRelationService(INriRepositoryFactory repositories, IServerLogger logger)
    {
        _repositories = repositories;
        _logger = logger;
    }

    public Task<EconomyRelationGraphResponse> GetFactionRelationsAsync(string campaignId, string factionId, UserAccount actor)
        => BuildGraphAsync(CreateRequest(campaignId, EconomyRuntimeKinds.Faction, factionId), actor);

    public Task<EconomyRelationGraphResponse> GetOrganizationRelationsAsync(string campaignId, string organizationId, UserAccount actor)
        => BuildGraphAsync(CreateRequest(campaignId, EconomyRuntimeKinds.Organization, organizationId), actor);

    public Task<EconomyRelationGraphResponse> GetCountryRelationsAsync(string campaignId, string countryId, UserAccount actor)
        => BuildGraphAsync(CreateRequest(campaignId, "country", countryId), actor);

    public Task<EconomyRelationGraphResponse> GetCityStateRelationsAsync(string campaignId, string cityStateId, UserAccount actor)
        => BuildGraphAsync(CreateRequest(campaignId, "cityState", cityStateId), actor);

    public Task<EconomyRelationGraphResponse> GetLocationRelationsAsync(string campaignId, string locationId, UserAccount actor)
        => BuildGraphAsync(CreateRequest(campaignId, "location", locationId), actor);

    public async Task<EconomyRelationGraphResponse> BuildGraphAsync(EconomyRelationGraphRequest request, UserAccount actor)
    {
        var safeRequest = Normalize(request);
        var response = new EconomyRelationGraphResponse
        {
            CampaignId = safeRequest.CampaignId,
            RootType = safeRequest.RootType,
            RootId = safeRequest.RootId,
            BuiltAtUtc = DateTime.UtcNow
        };

        _logger.Debug($"economy.relations.graph.start rootType={safeRequest.RootType} rootId={safeRequest.RootId} campaignId={safeRequest.CampaignId}");
        if (string.IsNullOrWhiteSpace(safeRequest.CampaignId))
        {
            response.Warnings.Add("campaign_id_required");
            return response;
        }

        var nodes = new Dictionary<string, EconomyRelationNode>(StringComparer.OrdinalIgnoreCase);
        var edges = new List<EconomyRelationEdge>();
        var factions = safeRequest.IncludeFactions ? (await _repositories.FactionStates.ListByCampaignAsync(safeRequest.CampaignId, 500)).ToList() : new List<FactionState>();
        var organizations = safeRequest.IncludeOrganizations ? (await _repositories.OrganizationStates.ListByCampaignAsync(safeRequest.CampaignId, 500)).ToList() : new List<OrganizationState>();
        var markets = safeRequest.IncludeMarkets ? (await _repositories.MarketStates.ListByCampaignAsync(safeRequest.CampaignId, 500)).ToList() : new List<MarketState>();
        var laws = safeRequest.IncludeLaws ? (await _repositories.LawStates.ListByCampaignAsync(safeRequest.CampaignId, 500)).ToList() : new List<LawState>();
        var restrictions = safeRequest.IncludeRestrictions ? (await _repositories.RestrictionStates.ListByCampaignAsync(safeRequest.CampaignId, 500)).ToList() : new List<RestrictionState>();
        var scopes = safeRequest.IncludeScopes ? (await _repositories.EconomyScopeStates.ListByCampaignAsync(safeRequest.CampaignId, 500)).ToList() : new List<EconomyScopeState>();

        foreach (var state in factions) AddNode(nodes, RuntimeNode(state));
        foreach (var state in organizations) AddNode(nodes, RuntimeNode(state));
        foreach (var state in markets) AddNode(nodes, RuntimeNode(state));
        foreach (var state in laws) AddNode(nodes, RuntimeNode(state));
        foreach (var state in restrictions) AddNode(nodes, RuntimeNode(state));
        foreach (var state in scopes) AddNode(nodes, RuntimeNode(state));

        foreach (var faction in factions)
        {
            AddDefinitionEdge(nodes, edges, response, faction.Id, "country", faction.CountryId, "located_in_country", "out", "CountryId", true);
            AddDefinitionEdge(nodes, edges, response, faction.Id, "cityState", faction.CityStateId, "located_in_city_state", "out", "CityStateId", true);
            foreach (var relation in faction.RelationStates ?? new List<FactionRelationState>())
            {
                var targetId = ResolveRuntimeId(relation.TargetFactionId, factions.Select(x => (x.Id, x.DefinitionId)));
                if (string.IsNullOrWhiteSpace(targetId))
                {
                    targetId = AddDefinitionRef(nodes, response, relation.TargetFactionId, faction.Id);
                }

                AddEdge(edges, faction.Id, targetId, FirstNonEmpty(relation.RelationType, "faction_relation"), "out", "RelationStates", true);
            }
        }

        foreach (var organization in organizations)
        {
            var factionId = ResolveRuntimeId(organization.ParentFactionId, factions.Select(x => (x.Id, x.DefinitionId)));
            if (string.IsNullOrWhiteSpace(factionId) && !string.IsNullOrWhiteSpace(organization.ParentFactionId))
            {
                factionId = AddDefinitionRef(nodes, response, organization.ParentFactionId, organization.Id);
            }

            AddEdgeIfPresent(edges, organization.Id, factionId, "belongs_to_faction", "out", "ParentFactionId", true);
            AddDefinitionEdge(nodes, edges, response, organization.Id, "country", organization.CountryId, "located_in_country", "out", "CountryId", true);
            AddDefinitionEdge(nodes, edges, response, organization.Id, "cityState", organization.CityStateId, "located_in_city_state", "out", "CityStateId", true);
            foreach (var locationId in organization.LocationIds ?? new List<string>())
            {
                AddDefinitionEdge(nodes, edges, response, organization.Id, "location", locationId, "uses_location", "out", "LocationIds", true);
            }
        }

        foreach (var law in laws)
        {
            foreach (var restrictionId in law.RelatedRestrictionIds ?? new List<string>())
            {
                var targetId = ResolveRuntimeId(restrictionId, restrictions.Select(x => (x.Id, x.DefinitionId)));
                if (string.IsNullOrWhiteSpace(targetId)) targetId = AddDefinitionRef(nodes, response, restrictionId, law.Id);
                AddEdge(edges, law.Id, targetId, "law_restricts", "out", "RelatedRestrictionIds", true);
            }

            foreach (var countryId in law.CountryIds ?? new List<string>()) AddDefinitionEdge(nodes, edges, response, law.Id, "country", countryId, "applies_to_country", "out", "CountryIds", true);
            foreach (var cityStateId in law.CityStateIds ?? new List<string>()) AddDefinitionEdge(nodes, edges, response, law.Id, "cityState", cityStateId, "applies_to_city_state", "out", "CityStateIds", true);
        }

        foreach (var restriction in restrictions)
        {
            foreach (var lawId in restriction.RelatedLawIds ?? new List<string>())
            {
                var targetId = ResolveRuntimeId(lawId, laws.Select(x => (x.Id, x.DefinitionId)));
                if (string.IsNullOrWhiteSpace(targetId)) targetId = AddDefinitionRef(nodes, response, lawId, restriction.Id);
                AddEdge(edges, restriction.Id, targetId, "restriction_from_law", "out", "RelatedLawIds", true);
            }

            foreach (var countryId in restriction.CountryIds ?? new List<string>()) AddDefinitionEdge(nodes, edges, response, restriction.Id, "country", countryId, "applies_to_country", "out", "CountryIds", true);
            foreach (var cityStateId in restriction.CityStateIds ?? new List<string>()) AddDefinitionEdge(nodes, edges, response, restriction.Id, "cityState", cityStateId, "applies_to_city_state", "out", "CityStateIds", true);
        }

        foreach (var market in markets)
        {
            AddDefinitionEdge(nodes, edges, response, market.Id, "country", market.CountryId, "market_in_country", "out", "CountryId", true);
            AddDefinitionEdge(nodes, edges, response, market.Id, "cityState", market.CityStateId, "market_in_city_state", "out", "CityStateId", true);
            AddDefinitionEdge(nodes, edges, response, market.Id, "location", market.LocationId, "market_at_location", "out", "LocationId", true);
            foreach (var marketTagId in market.MarketTagIds ?? new List<string>())
            {
                AddDefinitionEdge(nodes, edges, response, market.Id, "marketTag", marketTagId, "uses_market_tag", "out", "MarketTagIds", true);
            }
        }

        foreach (var scope in scopes)
        {
            AddDefinitionEdge(nodes, edges, response, scope.Id, "country", scope.CountryId, "scope_country", "out", "CountryId", true);
            AddDefinitionEdge(nodes, edges, response, scope.Id, "cityState", scope.CityStateId, "scope_city_state", "out", "CityStateId", true);
            AddDefinitionEdge(nodes, edges, response, scope.Id, "region", scope.RegionId, "scope_region", "out", "RegionId", true);
            foreach (var marketId in scope.MarketIds ?? new List<string>())
            {
                var targetId = ResolveRuntimeId(marketId, markets.Select(x => (x.Id, x.DefinitionId)));
                if (string.IsNullOrWhiteSpace(targetId)) targetId = AddDefinitionRef(nodes, response, marketId, scope.Id);
                AddEdge(edges, scope.Id, targetId, "scope_market", "out", "MarketIds", true);
            }

            foreach (var lawId in scope.ActiveLawIds ?? new List<string>())
            {
                var targetId = ResolveRuntimeId(lawId, laws.Select(x => (x.Id, x.DefinitionId)));
                if (string.IsNullOrWhiteSpace(targetId)) targetId = AddDefinitionRef(nodes, response, lawId, scope.Id);
                AddEdge(edges, scope.Id, targetId, "scope_active_law", "out", "ActiveLawIds", true);
            }

            foreach (var restrictionId in scope.ActiveRestrictionIds ?? new List<string>())
            {
                var targetId = ResolveRuntimeId(restrictionId, restrictions.Select(x => (x.Id, x.DefinitionId)));
                if (string.IsNullOrWhiteSpace(targetId)) targetId = AddDefinitionRef(nodes, response, restrictionId, scope.Id);
                AddEdge(edges, scope.Id, targetId, "scope_active_restriction", "out", "ActiveRestrictionIds", true);
            }
        }

        var rootId = ResolveRootId(safeRequest, nodes);
        if (!nodes.ContainsKey(rootId))
        {
            AddNode(nodes, new EconomyRelationNode { Id = rootId, RuntimeType = NormalizeRootType(safeRequest.RootType), DefinitionId = safeRequest.RootId, Name = safeRequest.RootId, Visibility = "unknown" });
            response.Warnings.Add($"root_not_found:{safeRequest.RootType}:{safeRequest.RootId}");
        }

        ApplyDepthAndLimit(response, nodes, edges, rootId, safeRequest.MaxDepth, safeRequest.Limit);
        _logger.Debug($"economy.relations.graph.done nodes={response.Nodes.Count} edges={response.Edges.Count} warnings={response.Warnings.Count}");
        return response;
    }

    private static EconomyRelationGraphRequest CreateRequest(string campaignId, string rootType, string rootId)
    {
        return new EconomyRelationGraphRequest { CampaignId = campaignId, RootType = rootType, RootId = rootId };
    }

    private static EconomyRelationGraphRequest Normalize(EconomyRelationGraphRequest request)
    {
        var safe = request ?? new EconomyRelationGraphRequest();
        safe.CampaignId = safe.CampaignId ?? string.Empty;
        safe.RootType = NormalizeRootType(safe.RootType);
        safe.RootId = safe.RootId ?? string.Empty;
        safe.MaxDepth = Math.Max(0, Math.Min(safe.MaxDepth <= 0 ? 2 : safe.MaxDepth, 5));
        safe.Limit = Math.Max(1, Math.Min(safe.Limit <= 0 ? 200 : safe.Limit, 500));
        return safe;
    }

    private static string NormalizeRootType(string rootType)
    {
        var value = (rootType ?? string.Empty).Trim();
        if (string.Equals(value, "city_state", StringComparison.OrdinalIgnoreCase)) return "cityState";
        if (string.Equals(value, "scope", StringComparison.OrdinalIgnoreCase)) return EconomyRuntimeKinds.EconomyScope;
        return value;
    }

    private static string ResolveRootId(EconomyRelationGraphRequest request, Dictionary<string, EconomyRelationNode> nodes)
    {
        if (nodes.ContainsKey(request.RootId)) return request.RootId;
        var match = nodes.Values.FirstOrDefault(x => string.Equals(x.RuntimeType, request.RootType, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.DefinitionId, request.RootId, StringComparison.OrdinalIgnoreCase));
        return match?.Id ?? request.RootId;
    }

    private void ApplyDepthAndLimit(EconomyRelationGraphResponse response, Dictionary<string, EconomyRelationNode> nodes, List<EconomyRelationEdge> edges, string rootId, int maxDepth, int limit)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<(string Id, int Depth)>();
        visited.Add(rootId);
        queue.Enqueue((rootId, 0));
        while (queue.Count > 0 && visited.Count < limit)
        {
            var current = queue.Dequeue();
            if (current.Depth >= maxDepth) continue;
            foreach (var edge in edges.Where(x => string.Equals(x.FromId, current.Id, StringComparison.OrdinalIgnoreCase) || string.Equals(x.ToId, current.Id, StringComparison.OrdinalIgnoreCase)))
            {
                var next = string.Equals(edge.FromId, current.Id, StringComparison.OrdinalIgnoreCase) ? edge.ToId : edge.FromId;
                if (visited.Add(next))
                {
                    queue.Enqueue((next, current.Depth + 1));
                    if (visited.Count >= limit) break;
                }
            }
        }

        if (visited.Count >= limit) response.Warnings.Add("graph_limit_reached");
        response.Nodes = visited.Where(nodes.ContainsKey).Select(x => nodes[x]).ToList();
        response.Edges = edges.Where(x => visited.Contains(x.FromId) && visited.Contains(x.ToId)).ToList();
    }

    private static EconomyRelationNode RuntimeNode(FactionState state)
        => RuntimeNode(state, EconomyRuntimeKinds.Faction, state.DefinitionId, state.Name, state.Tags, VisibilityFromSecrecy(state.SecrecyLevel));

    private static EconomyRelationNode RuntimeNode(OrganizationState state)
        => RuntimeNode(state, EconomyRuntimeKinds.Organization, state.DefinitionId, state.Name, state.Tags, VisibilityFromSecrecy(state.SecrecyLevel));

    private static EconomyRelationNode RuntimeNode(MarketState state)
        => RuntimeNode(state, EconomyRuntimeKinds.Market, state.DefinitionId, state.Name, state.Tags, state.IsBlackMarket ? "restricted" : "public");

    private static EconomyRelationNode RuntimeNode(LawState state)
        => RuntimeNode(state, EconomyRuntimeKinds.Law, state.DefinitionId, state.Name, state.Tags, state.IsPubliclyKnown ? "public" : "restricted");

    private static EconomyRelationNode RuntimeNode(RestrictionState state)
        => RuntimeNode(state, EconomyRuntimeKinds.Restriction, state.DefinitionId, state.Name, state.Tags, state.GMApprovalRequired ? "restricted" : "public");

    private static EconomyRelationNode RuntimeNode(EconomyScopeState state)
        => RuntimeNode(state, EconomyRuntimeKinds.EconomyScope, string.Empty, state.ScopeType, new List<string>(), "public");

    private static EconomyRelationNode RuntimeNode(EntityBase state, string runtimeType, string definitionId, string name, List<string> tags, string visibility)
    {
        return new EconomyRelationNode
        {
            Id = state.Id,
            RuntimeType = runtimeType,
            DefinitionId = definitionId,
            Name = name,
            Visibility = visibility,
            Tags = tags ?? new List<string>()
        };
    }

    private void AddDefinitionEdge(Dictionary<string, EconomyRelationNode> nodes, List<EconomyRelationEdge> edges, EconomyRelationGraphResponse response, string fromId, string runtimeType, string id, string relationType, string direction, string sourceField, bool explicitRelation)
    {
        if (string.IsNullOrWhiteSpace(id)) return;
        AddNode(nodes, new EconomyRelationNode { Id = id, RuntimeType = runtimeType, DefinitionId = id, Name = id, Visibility = "definition_ref" });
        AddEdge(edges, fromId, id, relationType, direction, sourceField, explicitRelation);
    }

    private string AddDefinitionRef(Dictionary<string, EconomyRelationNode> nodes, EconomyRelationGraphResponse response, string id, string source)
    {
        if (string.IsNullOrWhiteSpace(id)) return string.Empty;
        AddNode(nodes, new EconomyRelationNode { Id = id, RuntimeType = "definition_ref", DefinitionId = id, Name = id, Visibility = "definition_ref" });
        response.Warnings.Add($"runtime_state_missing_for_definition_ref:{id}");
        _logger.Debug($"economy.relations.missing_ref id={id} source={source}");
        return id;
    }

    private static void AddNode(Dictionary<string, EconomyRelationNode> nodes, EconomyRelationNode node)
    {
        if (string.IsNullOrWhiteSpace(node.Id) || nodes.ContainsKey(node.Id)) return;
        nodes[node.Id] = node;
    }

    private static void AddEdgeIfPresent(List<EconomyRelationEdge> edges, string fromId, string toId, string relationType, string direction, string sourceField, bool explicitRelation)
    {
        if (string.IsNullOrWhiteSpace(toId)) return;
        AddEdge(edges, fromId, toId, relationType, direction, sourceField, explicitRelation);
    }

    private static void AddEdge(List<EconomyRelationEdge> edges, string fromId, string toId, string relationType, string direction, string sourceField, bool explicitRelation)
    {
        if (string.IsNullOrWhiteSpace(fromId) || string.IsNullOrWhiteSpace(toId)) return;
        if (edges.Any(x => x.FromId == fromId && x.ToId == toId && x.RelationType == relationType && x.SourceField == sourceField)) return;
        edges.Add(new EconomyRelationEdge
        {
            FromId = fromId,
            ToId = toId,
            RelationType = relationType,
            Direction = direction,
            SourceField = sourceField,
            IsExplicit = explicitRelation
        });
    }

    private static string ResolveRuntimeId(string id, IEnumerable<(string Id, string DefinitionId)> runtimeStates)
    {
        if (string.IsNullOrWhiteSpace(id)) return string.Empty;
        var match = runtimeStates.FirstOrDefault(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase) || string.Equals(x.DefinitionId, id, StringComparison.OrdinalIgnoreCase));
        return match.Id ?? string.Empty;
    }

    private static string VisibilityFromSecrecy(string secrecyLevel)
    {
        if (string.IsNullOrWhiteSpace(secrecyLevel)) return "public";
        var value = secrecyLevel.Trim().ToLowerInvariant();
        return value.Contains("hidden") || value.Contains("secret") ? "restricted" : "public";
    }

    private static string FirstNonEmpty(params string[] values)
        => values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;
}
