using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nri.Server.Infrastructure;
using Nri.Server.Logging;
using Nri.Shared.Domain;

namespace Nri.Server.Application.Services;

public interface IEconomyRuntimeReadService
{
    Task<EconomyRuntimeListResponse> ListFactionsAsync(EconomyRuntimeListRequest request, UserAccount actor);
    Task<EconomyRuntimeStateDetails?> GetFactionAsync(string id, UserAccount actor);
    Task<EconomyRuntimeListResponse> ListOrganizationsAsync(EconomyRuntimeListRequest request, UserAccount actor);
    Task<EconomyRuntimeStateDetails?> GetOrganizationAsync(string id, UserAccount actor);
    Task<EconomyRuntimeListResponse> ListMarketsAsync(EconomyRuntimeListRequest request, UserAccount actor);
    Task<EconomyRuntimeStateDetails?> GetMarketAsync(string id, UserAccount actor);
    Task<EconomyRuntimeListResponse> ListLawsAsync(EconomyRuntimeListRequest request, UserAccount actor);
    Task<EconomyRuntimeStateDetails?> GetLawAsync(string id, UserAccount actor);
    Task<EconomyRuntimeListResponse> ListRestrictionsAsync(EconomyRuntimeListRequest request, UserAccount actor);
    Task<EconomyRuntimeStateDetails?> GetRestrictionAsync(string id, UserAccount actor);
    Task<EconomyRuntimeListResponse> ListEconomyScopesAsync(EconomyRuntimeListRequest request, UserAccount actor);
    Task<EconomyRuntimeStateDetails?> GetEconomyScopeAsync(string id, UserAccount actor);
}

public sealed class EconomyRuntimeReadService : IEconomyRuntimeReadService
{
    private readonly INriRepositoryFactory _repositories;
    private readonly IServerLogger _logger;

    public EconomyRuntimeReadService(INriRepositoryFactory repositories, IServerLogger logger)
    {
        _repositories = repositories;
        _logger = logger;
    }

    public async Task<EconomyRuntimeListResponse> ListFactionsAsync(EconomyRuntimeListRequest request, UserAccount actor)
    {
        var items = await _repositories.FactionStates.ListByCampaignAsync(request.CampaignId, 500, request.IncludeArchived);
        return List(request, EconomyRuntimeKinds.Faction, items.Select(EconomyRuntimeReadMapper.ToSummary).Where(x => Matches(request, x)));
    }

    public async Task<EconomyRuntimeStateDetails?> GetFactionAsync(string id, UserAccount actor)
        => EconomyRuntimeReadMapper.ToDetails(await _repositories.FactionStates.GetByIdAsync(id));

    public async Task<EconomyRuntimeListResponse> ListOrganizationsAsync(EconomyRuntimeListRequest request, UserAccount actor)
    {
        var items = await _repositories.OrganizationStates.ListByCampaignAsync(request.CampaignId, 500, request.IncludeArchived);
        return List(request, EconomyRuntimeKinds.Organization, items.Select(EconomyRuntimeReadMapper.ToSummary).Where(x => Matches(request, x)));
    }

    public async Task<EconomyRuntimeStateDetails?> GetOrganizationAsync(string id, UserAccount actor)
        => EconomyRuntimeReadMapper.ToDetails(await _repositories.OrganizationStates.GetByIdAsync(id));

    public async Task<EconomyRuntimeListResponse> ListMarketsAsync(EconomyRuntimeListRequest request, UserAccount actor)
    {
        var items = await _repositories.MarketStates.ListByCampaignAsync(request.CampaignId, 500, request.IncludeArchived);
        return List(request, EconomyRuntimeKinds.Market, items.Select(EconomyRuntimeReadMapper.ToSummary).Where(x => Matches(request, x)));
    }

    public async Task<EconomyRuntimeStateDetails?> GetMarketAsync(string id, UserAccount actor)
        => EconomyRuntimeReadMapper.ToDetails(await _repositories.MarketStates.GetByIdAsync(id));

    public async Task<EconomyRuntimeListResponse> ListLawsAsync(EconomyRuntimeListRequest request, UserAccount actor)
    {
        var items = await _repositories.LawStates.ListByCampaignAsync(request.CampaignId, 500, request.IncludeArchived);
        return List(request, EconomyRuntimeKinds.Law, items.Select(EconomyRuntimeReadMapper.ToSummary).Where(x => Matches(request, x)));
    }

    public async Task<EconomyRuntimeStateDetails?> GetLawAsync(string id, UserAccount actor)
        => EconomyRuntimeReadMapper.ToDetails(await _repositories.LawStates.GetByIdAsync(id));

    public async Task<EconomyRuntimeListResponse> ListRestrictionsAsync(EconomyRuntimeListRequest request, UserAccount actor)
    {
        var items = await _repositories.RestrictionStates.ListByCampaignAsync(request.CampaignId, 500, request.IncludeArchived);
        return List(request, EconomyRuntimeKinds.Restriction, items.Select(EconomyRuntimeReadMapper.ToSummary).Where(x => Matches(request, x)));
    }

    public async Task<EconomyRuntimeStateDetails?> GetRestrictionAsync(string id, UserAccount actor)
        => EconomyRuntimeReadMapper.ToDetails(await _repositories.RestrictionStates.GetByIdAsync(id));

    public async Task<EconomyRuntimeListResponse> ListEconomyScopesAsync(EconomyRuntimeListRequest request, UserAccount actor)
    {
        var items = await _repositories.EconomyScopeStates.ListByCampaignAsync(request.CampaignId, 500, request.IncludeArchived);
        return List(request, EconomyRuntimeKinds.EconomyScope, items.Select(EconomyRuntimeReadMapper.ToSummary).Where(x => Matches(request, x)));
    }

    public async Task<EconomyRuntimeStateDetails?> GetEconomyScopeAsync(string id, UserAccount actor)
        => EconomyRuntimeReadMapper.ToDetails(await _repositories.EconomyScopeStates.GetByIdAsync(id));

    private EconomyRuntimeListResponse List(EconomyRuntimeListRequest request, string runtimeType, IEnumerable<EconomyRuntimeStateSummary> source)
    {
        var safeLimit = Math.Max(1, Math.Min(request.Limit <= 0 ? 100 : request.Limit, 500));
        var safeOffset = Math.Max(0, request.Offset);
        var filtered = source.ToList();
        _logger.Debug($"economy.read.list.done type={runtimeType} count={filtered.Count}");
        return new EconomyRuntimeListResponse
        {
            Total = filtered.Count,
            Limit = safeLimit,
            Offset = safeOffset,
            HasMore = safeOffset + safeLimit < filtered.Count,
            Items = filtered.Skip(safeOffset).Take(safeLimit).ToList()
        };
    }

    private static bool Matches(EconomyRuntimeListRequest request, EconomyRuntimeStateSummary item)
    {
        return EmptyOrEquals(request.RuleSetId, item.RuleSetId)
            && EmptyOrEquals(request.CountryId, item.CountryId)
            && EmptyOrEquals(request.CityStateId, item.CityStateId)
            && EmptyOrEquals(request.LocationId, item.LocationId)
            && EmptyOrEquals(request.DefinitionId, item.DefinitionId);
    }

    private static bool EmptyOrEquals(string expected, string actual)
        => string.IsNullOrWhiteSpace(expected) || string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase);
}

public static class EconomyRuntimeReadMapper
{
    public static EconomyRuntimeStateSummary ToSummary(FactionState state)
    {
        return BaseSummary(state, EconomyRuntimeKinds.Faction, state.DefinitionId, state.Name, state.CountryId, state.CityStateId, string.Empty, state.Tags, true, VisibilityFromSecrecy(state.SecrecyLevel));
    }

    public static EconomyRuntimeStateSummary ToSummary(OrganizationState state)
    {
        return BaseSummary(state, EconomyRuntimeKinds.Organization, state.DefinitionId, state.Name, state.CountryId, state.CityStateId, First(state.LocationIds), state.Tags, true, VisibilityFromSecrecy(state.SecrecyLevel));
    }

    public static EconomyRuntimeStateSummary ToSummary(MarketState state)
    {
        return BaseSummary(state, EconomyRuntimeKinds.Market, state.DefinitionId, state.Name, state.CountryId, state.CityStateId, state.LocationId, state.Tags, state.IsActive, state.IsBlackMarket ? "restricted" : "public");
    }

    public static EconomyRuntimeStateSummary ToSummary(LawState state)
    {
        return BaseSummary(state, EconomyRuntimeKinds.Law, state.DefinitionId, state.Name, First(state.CountryIds), First(state.CityStateIds), string.Empty, state.Tags, state.IsActive, state.IsPubliclyKnown ? "public" : "restricted");
    }

    public static EconomyRuntimeStateSummary ToSummary(RestrictionState state)
    {
        return BaseSummary(state, EconomyRuntimeKinds.Restriction, state.DefinitionId, state.Name, First(state.CountryIds), First(state.CityStateIds), string.Empty, state.Tags, state.IsActive, state.GMApprovalRequired ? "restricted" : "public");
    }

    public static EconomyRuntimeStateSummary ToSummary(EconomyScopeState state)
    {
        return BaseSummary(state, EconomyRuntimeKinds.EconomyScope, string.Empty, state.ScopeType, state.CountryId, state.CityStateId, string.Empty, new List<string>(), true, "public");
    }

    public static EconomyRuntimeStateDetails? ToDetails(FactionState? state)
    {
        if (state == null) return null;
        return BaseDetails(state, EconomyRuntimeKinds.Faction, state.DefinitionId, state.Name, state.CountryId, state.CityStateId, state.LocationIds, string.Empty, state.Tags, state.Notes, new Dictionary<string, object>
        {
            { "publicAlignment", state.PublicAlignment },
            { "secrecyLevel", state.SecrecyLevel },
            { "influenceLevel", state.InfluenceLevel },
            { "militaryInfluence", state.MilitaryInfluence },
            { "economicInfluence", state.EconomicInfluence },
            { "politicalInfluence", state.PoliticalInfluence },
            { "magicInfluence", state.MagicInfluence },
            { "relationCount", state.RelationStates.Count }
        });
    }

    public static EconomyRuntimeStateDetails? ToDetails(OrganizationState? state)
    {
        if (state == null) return null;
        return BaseDetails(state, EconomyRuntimeKinds.Organization, state.DefinitionId, state.Name, state.CountryId, state.CityStateId, state.LocationIds, string.Empty, state.Tags, state.Notes, new Dictionary<string, object>
        {
            { "parentFactionId", state.ParentFactionId },
            { "publicStatus", state.PublicStatus },
            { "legalStatus", state.LegalStatus },
            { "accessLevel", state.AccessLevel },
            { "secrecyLevel", state.SecrecyLevel },
            { "serviceTags", state.ServiceTags.Cast<object>().ToArray() },
            { "resourceTags", state.ResourceTags.Cast<object>().ToArray() }
        });
    }

    public static EconomyRuntimeStateDetails? ToDetails(MarketState? state)
    {
        if (state == null) return null;
        return BaseDetails(state, EconomyRuntimeKinds.Market, state.DefinitionId, state.Name, state.CountryId, state.CityStateId, new List<string>(), state.LocationId, state.Tags, state.Notes, new Dictionary<string, object>
        {
            { "marketTagIds", state.MarketTagIds.Cast<object>().ToArray() },
            { "legalTagIds", state.LegalTagIds.Cast<object>().ToArray() },
            { "restrictedTagIds", state.RestrictedTagIds.Cast<object>().ToArray() },
            { "availableCurrencyIds", state.AvailableCurrencyIds.Cast<object>().ToArray() },
            { "availabilityProfile", state.AvailabilityProfile },
            { "pricePolicy", state.PricePolicy },
            { "isBlackMarket", state.IsBlackMarket },
            { "isActive", state.IsActive }
        });
    }

    public static EconomyRuntimeStateDetails? ToDetails(LawState? state)
    {
        if (state == null) return null;
        return BaseDetails(state, EconomyRuntimeKinds.Law, state.DefinitionId, state.Name, First(state.CountryIds), First(state.CityStateIds), new List<string>(), string.Empty, state.Tags, state.Notes, new Dictionary<string, object>
        {
            { "lawType", state.LawType },
            { "severity", state.Severity },
            { "enforcementLevel", state.EnforcementLevel },
            { "relatedRestrictionIds", state.RelatedRestrictionIds.Cast<object>().ToArray() },
            { "isActive", state.IsActive },
            { "isPubliclyKnown", state.IsPubliclyKnown }
        });
    }

    public static EconomyRuntimeStateDetails? ToDetails(RestrictionState? state)
    {
        if (state == null) return null;
        return BaseDetails(state, EconomyRuntimeKinds.Restriction, state.DefinitionId, state.Name, First(state.CountryIds), First(state.CityStateIds), new List<string>(), string.Empty, state.Tags, state.Notes, new Dictionary<string, object>
        {
            { "restrictionType", state.RestrictionType },
            { "appliesToTags", state.AppliesToTags.Cast<object>().ToArray() },
            { "relatedLawIds", state.RelatedLawIds.Cast<object>().ToArray() },
            { "licenseRequired", state.LicenseRequired },
            { "gmApprovalRequired", state.GMApprovalRequired },
            { "isActive", state.IsActive }
        });
    }

    public static EconomyRuntimeStateDetails? ToDetails(EconomyScopeState? state)
    {
        if (state == null) return null;
        return BaseDetails(state, EconomyRuntimeKinds.EconomyScope, string.Empty, state.ScopeType, state.CountryId, state.CityStateId, new List<string>(), string.Empty, new List<string>(), state.Notes, new Dictionary<string, object>
        {
            { "scopeType", state.ScopeType },
            { "regionId", state.RegionId },
            { "currencyIds", state.CurrencyIds.Cast<object>().ToArray() },
            { "marketIds", state.MarketIds.Cast<object>().ToArray() },
            { "activeLawIds", state.ActiveLawIds.Cast<object>().ToArray() },
            { "activeRestrictionIds", state.ActiveRestrictionIds.Cast<object>().ToArray() }
        });
    }

    private static EconomyRuntimeStateSummary BaseSummary(EntityBase state, string runtimeType, string definitionId, string name, string countryId, string cityStateId, string locationId, List<string> tags, bool isActive, string visibility)
    {
        return new EconomyRuntimeStateSummary
        {
            Id = state.Id,
            RuntimeType = runtimeType,
            DefinitionId = definitionId,
            Name = name,
            CampaignId = GetStringProperty(state, "CampaignId"),
            RuleSetId = GetStringProperty(state, "RuleSetId"),
            CountryId = countryId,
            CityStateId = cityStateId,
            LocationId = locationId,
            Tags = tags ?? new List<string>(),
            IsActive = isActive,
            IsArchived = state.Archived,
            Visibility = visibility
        };
    }

    private static EconomyRuntimeStateDetails BaseDetails(EntityBase state, string runtimeType, string definitionId, string name, string countryId, string cityStateId, List<string> locationIds, string locationId, List<string> tags, string notes, Dictionary<string, object> publicFields)
    {
        return new EconomyRuntimeStateDetails
        {
            Id = state.Id,
            RuntimeType = runtimeType,
            DefinitionId = definitionId,
            Name = name,
            CampaignId = GetStringProperty(state, "CampaignId"),
            RuleSetId = GetStringProperty(state, "RuleSetId"),
            CountryId = countryId,
            CityStateId = cityStateId,
            LocationIds = locationIds ?? new List<string>(),
            LocationId = locationId,
            Tags = tags ?? new List<string>(),
            Notes = notes ?? string.Empty,
            PublicFields = publicFields,
            HiddenFields = new Dictionary<string, object>(),
            SchemaVersion = state.SchemaVersion
        };
    }

    private static string VisibilityFromSecrecy(string secrecyLevel)
    {
        if (string.IsNullOrWhiteSpace(secrecyLevel)) return "public";
        var value = secrecyLevel.Trim().ToLowerInvariant();
        return value.Contains("hidden") || value.Contains("secret") ? "restricted" : "public";
    }

    private static string First(List<string> values)
        => values == null || values.Count == 0 ? string.Empty : values[0];

    private static string GetStringProperty(EntityBase state, string propertyName)
    {
        var property = state.GetType().GetProperty(propertyName);
        return property?.GetValue(state) as string ?? string.Empty;
    }
}
