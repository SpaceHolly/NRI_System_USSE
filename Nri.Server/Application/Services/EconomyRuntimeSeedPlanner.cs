using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Nri.Server.Logging;
using Nri.Shared.Domain;

namespace Nri.Server.Application.Services;

public interface IEconomyRuntimeSeedPlanner
{
    Task<EconomyRuntimeSeedDryRunResult> BuildDryRunPlanFromPackAsync(EconomyRuntimeSeedDryRunRequest request);
    Task<EconomyRuntimeSeedDryRunResult> BuildDryRunPlanFromDefinitionsAsync(IEnumerable<UnifiedDefinitionDocument> definitions, EconomyRuntimeSeedDryRunRequest request);
}

public sealed class EconomyRuntimeSeedPlanner : IEconomyRuntimeSeedPlanner
{
    private const string Planned = "planned";
    private const string HiddenDefinitionPlanned = "hidden_definition_planned";

    private readonly IDefinitionPackLoader _packLoader;
    private readonly DefinitionPackCrossReferenceValidator _crossReferenceValidator;
    private readonly IServerLogger _logger;

    public EconomyRuntimeSeedPlanner(IDefinitionPackLoader packLoader, DefinitionPackCrossReferenceValidator crossReferenceValidator, IServerLogger logger)
    {
        _packLoader = packLoader;
        _crossReferenceValidator = crossReferenceValidator;
        _logger = logger;
    }

    public async Task<EconomyRuntimeSeedDryRunResult> BuildDryRunPlanFromPackAsync(EconomyRuntimeSeedDryRunRequest request)
    {
        var safeRequest = request ?? new EconomyRuntimeSeedDryRunRequest();
        var result = CreateResult(safeRequest);
        try
        {
            _logger.Debug($"economy.seed.dryrun.start campaignId={safeRequest.CampaignId} ruleSetId={safeRequest.RuleSetId}");
            if (string.IsNullOrWhiteSpace(safeRequest.PackPath))
            {
                result.Errors.Add("pack_path_required");
                Finish(result);
                return result;
            }

            var manifest = await _packLoader.LoadManifestAsync(safeRequest.PackPath);
            result.PackId = FirstNonEmpty(safeRequest.PackId, manifest.PackId);
            if (string.IsNullOrWhiteSpace(result.RuleSetId))
            {
                result.RuleSetId = manifest.RuleSetId;
            }

            var validation = await _packLoader.ValidatePackAsync(safeRequest.PackPath);
            result.Errors.AddRange(validation.Errors);
            result.Errors.AddRange(validation.CrossReferenceErrors);
            result.Warnings.AddRange(validation.Warnings);
            result.Warnings.AddRange(validation.CrossReferenceWarnings);
            if (!validation.IsValid)
            {
                Finish(result);
                return result;
            }

            var definitions = await _packLoader.LoadDefinitionsAsync(safeRequest.PackPath, manifest);
            _logger.Debug($"economy.seed.dryrun.pack_loaded packId={result.PackId} definitions={definitions.Count}");
            var planned = await BuildDryRunPlanFromDefinitionsAsync(definitions, new EconomyRuntimeSeedDryRunRequest
            {
                RuleSetId = result.RuleSetId,
                CampaignId = result.CampaignId,
                PackId = result.PackId,
                PackPath = safeRequest.PackPath,
                IncludeFactions = safeRequest.IncludeFactions,
                IncludeOrganizations = safeRequest.IncludeOrganizations,
                IncludeLaws = safeRequest.IncludeLaws,
                IncludeRestrictions = safeRequest.IncludeRestrictions,
                IncludeMarkets = safeRequest.IncludeMarkets,
                IncludeEconomyScopes = safeRequest.IncludeEconomyScopes,
                ActorUserId = safeRequest.ActorUserId,
                RequestId = safeRequest.RequestId
            });

            result.PlannedStates = planned.PlannedStates;
            result.Errors.AddRange(planned.Errors);
            result.Warnings.AddRange(planned.Warnings);
            Finish(result);
            return result;
        }
        catch (Exception ex)
        {
            result.Errors.Add(ex.Message);
            _logger.Debug($"economy.seed.dryrun.error message={SafeLog(ex.Message)}");
            Finish(result);
            return result;
        }
    }

    public Task<EconomyRuntimeSeedDryRunResult> BuildDryRunPlanFromDefinitionsAsync(IEnumerable<UnifiedDefinitionDocument> definitions, EconomyRuntimeSeedDryRunRequest request)
    {
        var safeRequest = request ?? new EconomyRuntimeSeedDryRunRequest();
        var result = CreateResult(safeRequest);
        var list = (definitions ?? Enumerable.Empty<UnifiedDefinitionDocument>()).Where(x => x != null).ToList();
        var index = _crossReferenceValidator.BuildIndex(list);
        var validation = _crossReferenceValidator.ValidateReferences(index, list, safeRequest.RuleSetId);
        result.Errors.AddRange(validation.Errors);
        result.Errors.AddRange(validation.CrossReferenceErrors);
        result.Warnings.AddRange(validation.Warnings);
        result.Warnings.AddRange(validation.CrossReferenceWarnings);

        if (string.IsNullOrWhiteSpace(result.CampaignId))
        {
            result.Errors.Add("campaign_id_required");
        }

        if (string.IsNullOrWhiteSpace(result.RuleSetId))
        {
            result.Errors.Add("ruleset_id_required");
        }

        if (safeRequest.IncludeFactions)
        {
            AddFactionPlans(result, list.Where(x => CategoryEquals(x, DefinitionCategoryIds.Faction)));
        }

        if (safeRequest.IncludeOrganizations)
        {
            AddOrganizationPlans(result, list.Where(x => CategoryEquals(x, DefinitionCategoryIds.Organization)), index);
        }

        if (safeRequest.IncludeLaws)
        {
            AddLawPlans(result, list.Where(x => CategoryEquals(x, DefinitionCategoryIds.Law)));
        }

        if (safeRequest.IncludeRestrictions)
        {
            AddRestrictionPlans(result, list.Where(x => CategoryEquals(x, DefinitionCategoryIds.Restriction)));
        }

        if (safeRequest.IncludeMarkets)
        {
            AddMarketPlans(result, list.Where(x => CategoryEquals(x, DefinitionCategoryIds.MarketTag)), index);
        }

        if (safeRequest.IncludeEconomyScopes)
        {
            AddEconomyScopePlans(
                result,
                list.Where(x => CategoryEquals(x, DefinitionCategoryIds.Country)),
                list.Where(x => CategoryEquals(x, DefinitionCategoryIds.CityState)),
                list.Where(x => CategoryEquals(x, DefinitionCategoryIds.Law)),
                list.Where(x => CategoryEquals(x, DefinitionCategoryIds.Restriction)),
                index);
        }

        ValidatePlannedStates(result, index);
        Finish(result);
        return Task.FromResult(result);
    }

    private void AddFactionPlans(EconomyRuntimeSeedDryRunResult result, IEnumerable<UnifiedDefinitionDocument> definitions)
    {
        var count = 0;
        foreach (var definition in definitions)
        {
            var plan = CreatePlan(result, EconomyRuntimeKinds.Faction, definition, "faction_state");
            AddPreview(plan, "countryId", GetString(definition, "countryId"));
            AddPreview(plan, "cityStateId", GetString(definition, "cityStateId"));
            AddPreview(plan, "publicAlignment", GetString(definition, "publicAlignment"));
            AddPreview(plan, "secrecyLevel", GetString(definition, "secrecyLevel"));
            AddPreview(plan, "tags", definition.Tags.ToList());
            AddIntPreview(plan, definition, "influenceLevel");
            AddIntPreview(plan, definition, "militaryInfluence");
            AddIntPreview(plan, definition, "economicInfluence");
            AddIntPreview(plan, definition, "politicalInfluence");
            AddIntPreview(plan, definition, "magicInfluence");
            if (string.IsNullOrWhiteSpace(GetString(definition, "countryId")) && string.IsNullOrWhiteSpace(GetString(definition, "cityStateId")))
            {
                plan.Warnings.Add("faction_scope_missing");
            }

            result.PlannedStates.Add(plan);
            count++;
        }

        _logger.Debug($"economy.seed.dryrun.planned type={EconomyRuntimeKinds.Faction} count={count}");
    }

    private void AddOrganizationPlans(EconomyRuntimeSeedDryRunResult result, IEnumerable<UnifiedDefinitionDocument> definitions, DefinitionPackIndex index)
    {
        var count = 0;
        foreach (var definition in definitions)
        {
            var plan = CreatePlan(result, EconomyRuntimeKinds.Organization, definition, "organization_state");
            var parentFactionId = GetString(definition, "parentFactionId");
            AddPreview(plan, "parentFactionId", parentFactionId);
            AddPreview(plan, "countryId", GetString(definition, "countryId"));
            AddPreview(plan, "cityStateId", GetString(definition, "cityStateId"));
            AddPreview(plan, "locationIds", GetStringList(definition, "locationIds"));
            AddPreview(plan, "publicStatus", GetString(definition, "publicStatus"));
            AddPreview(plan, "legalStatus", GetString(definition, "legalStatus"));
            AddPreview(plan, "accessLevel", GetString(definition, "accessLevel"));
            AddPreview(plan, "secrecyLevel", GetString(definition, "secrecyLevel"));
            AddPreview(plan, "serviceTags", GetStringList(definition, "servicesTags"));
            AddPreview(plan, "resourceTags", GetStringList(definition, "resourceTags"));
            AddPreview(plan, "recruitmentTags", GetStringList(definition, "recruitmentTags"));
            AddPreview(plan, "tags", definition.Tags.ToList());
            if (string.IsNullOrWhiteSpace(parentFactionId))
            {
                plan.Warnings.Add("organization_parent_faction_missing");
            }
            else if (!index.FactionIds.Contains(parentFactionId))
            {
                plan.Errors.Add($"organization_parent_faction_missing_reference:{parentFactionId}");
            }

            if (string.IsNullOrWhiteSpace(GetString(definition, "countryId")) && string.IsNullOrWhiteSpace(GetString(definition, "cityStateId")) && GetStringList(definition, "locationIds").Count == 0)
            {
                plan.Warnings.Add("organization_scope_missing");
            }

            result.PlannedStates.Add(plan);
            count++;
        }

        _logger.Debug($"economy.seed.dryrun.planned type={EconomyRuntimeKinds.Organization} count={count}");
    }

    private void AddLawPlans(EconomyRuntimeSeedDryRunResult result, IEnumerable<UnifiedDefinitionDocument> definitions)
    {
        var count = 0;
        foreach (var definition in definitions)
        {
            var plan = CreatePlan(result, EconomyRuntimeKinds.Law, definition, "law_state");
            var countryIds = GetStringList(definition, "countryIds");
            var cityStateIds = GetStringList(definition, "cityStateIds");
            AddPreview(plan, "countryIds", countryIds);
            AddPreview(plan, "cityStateIds", cityStateIds);
            AddPreview(plan, "lawType", GetString(definition, "lawType"));
            AddPreview(plan, "severity", GetString(definition, "severity"));
            AddPreview(plan, "enforcementLevel", GetString(definition, "enforcementLevel"));
            AddPreview(plan, "relatedRestrictionIds", GetStringList(definition, "relatedRestrictionIds"));
            AddPreview(plan, "isActive", true);
            AddPreview(plan, "isPubliclyKnown", GetBool(definition, "publicKnown", true));
            AddPreview(plan, "tags", definition.Tags.ToList());
            if (countryIds.Count == 0 && cityStateIds.Count == 0) plan.Warnings.Add("law_scope_missing");
            if (string.Equals(GetString(definition, "mechanicalStatus"), "draft", StringComparison.OrdinalIgnoreCase)) plan.Warnings.Add("law_mechanics_draft");
            result.PlannedStates.Add(plan);
            count++;
        }

        _logger.Debug($"economy.seed.dryrun.planned type={EconomyRuntimeKinds.Law} count={count}");
    }

    private void AddRestrictionPlans(EconomyRuntimeSeedDryRunResult result, IEnumerable<UnifiedDefinitionDocument> definitions)
    {
        var count = 0;
        foreach (var definition in definitions)
        {
            var plan = CreatePlan(result, EconomyRuntimeKinds.Restriction, definition, "restriction_state");
            var relatedLawIds = GetStringList(definition, "relatedLawIds");
            var appliesToTags = GetStringList(definition, "appliesToTags");
            AddPreview(plan, "restrictionType", GetString(definition, "restrictionType"));
            AddPreview(plan, "appliesToTags", appliesToTags);
            AddPreview(plan, "countryIds", GetStringList(definition, "countryIds"));
            AddPreview(plan, "cityStateIds", GetStringList(definition, "cityStateIds"));
            AddPreview(plan, "relatedLawIds", relatedLawIds);
            AddPreview(plan, "licenseRequired", GetBool(definition, "licenseRequired", false));
            AddPreview(plan, "gmApprovalRequired", GetBool(definition, "gmApprovalRequired", false));
            AddPreview(plan, "isActive", true);
            AddPreview(plan, "tags", definition.Tags.ToList());
            if (relatedLawIds.Count == 0) plan.Warnings.Add("restriction_related_laws_missing");
            if (appliesToTags.Count == 0) plan.Warnings.Add("restriction_applies_to_tags_missing");
            result.PlannedStates.Add(plan);
            count++;
        }

        _logger.Debug($"economy.seed.dryrun.planned type={EconomyRuntimeKinds.Restriction} count={count}");
    }

    private void AddMarketPlans(EconomyRuntimeSeedDryRunResult result, IEnumerable<UnifiedDefinitionDocument> definitions, DefinitionPackIndex index)
    {
        var count = 0;
        var currencies = PickCurrencyIds(index);
        foreach (var definition in definitions)
        {
            var countryIds = GetStringList(definition, "commonCountryIds");
            if (countryIds.Count == 0)
            {
                result.Warnings.Add($"market_tag_common_country_ids_missing:{definition.Id}");
                continue;
            }

            foreach (var countryId in countryIds)
            {
                var plan = CreatePlan(result, EconomyRuntimeKinds.Market, definition, $"market_state:{countryId}");
                AddPreview(plan, "countryId", countryId);
                AddPreview(plan, "cityStateId", string.Empty);
                AddPreview(plan, "marketTagIds", new List<string> { definition.Id });
                AddPreview(plan, "availableCurrencyIds", currencies);
                AddPreview(plan, "availabilityProfile", "draft");
                AddPreview(plan, "pricePolicy", "draft_no_pricing");
                AddPreview(plan, "isBlackMarket", IsBlackMarket(definition));
                AddPreview(plan, "isActive", true);
                AddPreview(plan, "tags", definition.Tags.ToList());
                result.PlannedStates.Add(plan);
                count++;
            }
        }

        _logger.Debug($"economy.seed.dryrun.planned type={EconomyRuntimeKinds.Market} count={count}");
    }

    private void AddEconomyScopePlans(EconomyRuntimeSeedDryRunResult result, IEnumerable<UnifiedDefinitionDocument> countries, IEnumerable<UnifiedDefinitionDocument> cityStates, IEnumerable<UnifiedDefinitionDocument> laws, IEnumerable<UnifiedDefinitionDocument> restrictions, DefinitionPackIndex index)
    {
        var count = 0;
        var currencyIds = PickCurrencyIds(index);
        var lawList = laws.ToList();
        var restrictionList = restrictions.ToList();

        foreach (var country in countries)
        {
            var plan = CreatePlan(result, EconomyRuntimeKinds.EconomyScope, country, "economy_scope:country");
            AddPreview(plan, "scopeType", "country");
            AddPreview(plan, "countryId", country.Id);
            AddPreview(plan, "cityStateId", string.Empty);
            AddPreview(plan, "currencyIds", currencyIds);
            AddPreview(plan, "activeLawIds", lawList.Where(x => GetStringList(x, "countryIds").Contains(country.Id, StringComparer.OrdinalIgnoreCase)).Select(x => x.Id).ToList());
            AddPreview(plan, "activeRestrictionIds", restrictionList.Where(x => GetStringList(x, "countryIds").Contains(country.Id, StringComparer.OrdinalIgnoreCase)).Select(x => x.Id).ToList());
            result.PlannedStates.Add(plan);
            count++;
        }

        foreach (var cityState in cityStates)
        {
            var plan = CreatePlan(result, EconomyRuntimeKinds.EconomyScope, cityState, "economy_scope:city_state");
            AddPreview(plan, "scopeType", "city_state");
            AddPreview(plan, "countryId", string.Empty);
            AddPreview(plan, "cityStateId", cityState.Id);
            AddPreview(plan, "currencyIds", currencyIds);
            AddPreview(plan, "activeLawIds", lawList.Where(x => GetStringList(x, "cityStateIds").Contains(cityState.Id, StringComparer.OrdinalIgnoreCase)).Select(x => x.Id).ToList());
            AddPreview(plan, "activeRestrictionIds", restrictionList.Where(x => GetStringList(x, "cityStateIds").Contains(cityState.Id, StringComparer.OrdinalIgnoreCase)).Select(x => x.Id).ToList());
            result.PlannedStates.Add(plan);
            count++;
        }

        _logger.Debug($"economy.seed.dryrun.planned type={EconomyRuntimeKinds.EconomyScope} count={count}");
    }

    private static EconomyRuntimeSeedDryRunResult CreateResult(EconomyRuntimeSeedDryRunRequest request)
    {
        return new EconomyRuntimeSeedDryRunResult
        {
            RuleSetId = request.RuleSetId ?? string.Empty,
            CampaignId = request.CampaignId ?? string.Empty,
            PackId = request.PackId ?? string.Empty,
            CheckedAtUtc = DateTime.UtcNow
        };
    }

    private static EconomyRuntimeSeedPlannedState CreatePlan(EconomyRuntimeSeedDryRunResult result, string runtimeType, UnifiedDefinitionDocument definition, string prefix)
    {
        var status = IsHidden(definition) ? HiddenDefinitionPlanned : Planned;
        var plan = new EconomyRuntimeSeedPlannedState
        {
            RuntimeType = runtimeType,
            DefinitionId = definition.Id,
            ProposedId = $"{prefix}:{result.CampaignId}:{definition.Id}",
            Name = definition.Name,
            CampaignId = result.CampaignId,
            RuleSetId = FirstNonEmpty(result.RuleSetId, definition.RuleSetIds.FirstOrDefault() ?? string.Empty),
            SourceCategory = definition.Category,
            Status = status
        };

        if (status == HiddenDefinitionPlanned)
        {
            plan.Warnings.Add("hidden_definition_planned");
        }

        return plan;
    }

    private static void ValidatePlannedStates(EconomyRuntimeSeedDryRunResult result, DefinitionPackIndex index)
    {
        foreach (var plan in result.PlannedStates)
        {
            if (string.IsNullOrWhiteSpace(plan.ProposedId)) plan.Errors.Add("proposed_id_required");
            if (string.IsNullOrWhiteSpace(plan.RuntimeType)) plan.Errors.Add("runtime_type_required");
            if (string.IsNullOrWhiteSpace(plan.DefinitionId)) plan.Errors.Add("definition_id_required");
            else if (!index.AllIds.Contains(plan.DefinitionId)) plan.Errors.Add($"definition_missing:{plan.DefinitionId}");
            if (string.IsNullOrWhiteSpace(plan.CampaignId)) plan.Errors.Add("campaign_id_required");
            if (string.IsNullOrWhiteSpace(plan.RuleSetId)) plan.Errors.Add("ruleset_id_required");
        }
    }

    private static void Finish(EconomyRuntimeSeedDryRunResult result)
    {
        result.Summary = new EconomyRuntimeSeedSummary
        {
            PlannedFactionStates = result.PlannedStates.Count(x => x.RuntimeType == EconomyRuntimeKinds.Faction),
            PlannedOrganizationStates = result.PlannedStates.Count(x => x.RuntimeType == EconomyRuntimeKinds.Organization),
            PlannedLawStates = result.PlannedStates.Count(x => x.RuntimeType == EconomyRuntimeKinds.Law),
            PlannedRestrictionStates = result.PlannedStates.Count(x => x.RuntimeType == EconomyRuntimeKinds.Restriction),
            PlannedMarketStates = result.PlannedStates.Count(x => x.RuntimeType == EconomyRuntimeKinds.Market),
            PlannedEconomyScopeStates = result.PlannedStates.Count(x => x.RuntimeType == EconomyRuntimeKinds.EconomyScope)
        };
        result.Summary.ErrorCount = result.Errors.Count + result.PlannedStates.Sum(x => x.Errors.Count);
        result.Summary.WarningCount = result.Warnings.Count + result.PlannedStates.Sum(x => x.Warnings.Count);
        result.Success = result.Summary.ErrorCount == 0;
        result.CheckedAtUtc = DateTime.UtcNow;
    }

    private static void AddIntPreview(EconomyRuntimeSeedPlannedState plan, UnifiedDefinitionDocument definition, string field)
    {
        if (TryGetInt(definition, field, out var value))
        {
            AddPreview(plan, field, value);
        }
        else if (definition.ExtraData != null && definition.ExtraData.ContainsKey(field))
        {
            plan.Warnings.Add($"numeric_field_invalid:{field}");
        }
    }

    private static List<string> PickCurrencyIds(DefinitionPackIndex index)
    {
        var preferred = new[] { "silver_coin", "gold_coin" };
        var result = preferred.Where(x => index.CurrencyIds.Contains(x)).ToList();
        if (result.Count > 0) return result;
        return index.CurrencyIds.Take(2).ToList();
    }

    private static bool IsBlackMarket(UnifiedDefinitionDocument definition)
    {
        var legality = GetString(definition, "legalityDefault").ToLowerInvariant();
        return legality.Contains("black") || legality.Contains("illegal") || legality.Contains("restricted");
    }

    private static bool IsHidden(UnifiedDefinitionDocument definition)
    {
        return string.Equals(definition.VisibilityRule, VisibilityRuleIds.GmOnly, StringComparison.OrdinalIgnoreCase)
            || string.Equals(definition.VisibilityRule, VisibilityRuleIds.HiddenUntilDiscovered, StringComparison.OrdinalIgnoreCase);
    }

    private static void AddPreview(EconomyRuntimeSeedPlannedState plan, string key, object value)
    {
        plan.PreviewData[key] = value;
    }

    private static string GetString(UnifiedDefinitionDocument definition, string key)
    {
        if (definition.ExtraData == null || !definition.ExtraData.TryGetValue(key, out var value) || value == null) return string.Empty;
        if (value is string s) return s.Trim();
        if (value is JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.String) return (element.GetString() ?? string.Empty).Trim();
            if (element.ValueKind == JsonValueKind.Number || element.ValueKind == JsonValueKind.True || element.ValueKind == JsonValueKind.False) return element.ToString().Trim();
            return string.Empty;
        }

        return Convert.ToString(value)?.Trim() ?? string.Empty;
    }

    private static List<string> GetStringList(UnifiedDefinitionDocument definition, string key)
    {
        var values = new List<string>();
        if (definition.ExtraData == null || !definition.ExtraData.TryGetValue(key, out var value) || value == null) return values;
        if (value is JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray())
                {
                    var text = item.ValueKind == JsonValueKind.String ? item.GetString() ?? string.Empty : item.ToString();
                    if (!string.IsNullOrWhiteSpace(text)) values.Add(text.Trim());
                }
            }
            else if (element.ValueKind == JsonValueKind.String)
            {
                var text = element.GetString() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(text)) values.Add(text.Trim());
            }

            return values.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        if (value is IEnumerable<string> strings)
        {
            return strings.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        if (value is System.Collections.IEnumerable enumerable && value is not string)
        {
            foreach (var item in enumerable)
            {
                var text = Convert.ToString(item);
                if (!string.IsNullOrWhiteSpace(text)) values.Add(text.Trim());
            }

            return values.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        var single = Convert.ToString(value);
        if (!string.IsNullOrWhiteSpace(single)) values.Add(single.Trim());
        return values;
    }

    private static bool TryGetInt(UnifiedDefinitionDocument definition, string key, out int value)
    {
        value = 0;
        if (definition.ExtraData == null || !definition.ExtraData.TryGetValue(key, out var raw) || raw == null) return false;
        if (raw is int i)
        {
            value = i;
            return true;
        }

        if (raw is long l && l >= int.MinValue && l <= int.MaxValue)
        {
            value = (int)l;
            return true;
        }

        if (raw is JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out value)) return true;
            if (element.ValueKind == JsonValueKind.String && int.TryParse(element.GetString(), out value)) return true;
        }

        return int.TryParse(Convert.ToString(raw), out value);
    }

    private static bool GetBool(UnifiedDefinitionDocument definition, string key, bool defaultValue)
    {
        if (definition.ExtraData == null || !definition.ExtraData.TryGetValue(key, out var raw) || raw == null) return defaultValue;
        if (raw is bool b) return b;
        if (raw is JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.True) return true;
            if (element.ValueKind == JsonValueKind.False) return false;
            if (element.ValueKind == JsonValueKind.String && bool.TryParse(element.GetString(), out var parsed)) return parsed;
        }

        return bool.TryParse(Convert.ToString(raw), out var value) ? value : defaultValue;
    }

    private static bool CategoryEquals(UnifiedDefinitionDocument definition, string category)
        => string.Equals(definition.Category, category, StringComparison.OrdinalIgnoreCase);

    private static string FirstNonEmpty(params string[] values)
        => values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;

    private static string SafeLog(string value)
        => (value ?? string.Empty).Replace(Environment.NewLine, " ");
}
