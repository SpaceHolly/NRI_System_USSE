using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Nri.Server.Infrastructure;
using Nri.Server.Logging;
using Nri.Shared.Domain;

namespace Nri.Server.Application.Services;

public interface IEconomyRuntimeSeedService
{
    Task<EconomyRuntimeSeedResult> SeedFromDefinitionsAsync(EconomyRuntimeSeedRequest request);
    Task<EconomyRuntimeSeedResult> ValidateSeedRequestAsync(EconomyRuntimeSeedRequest request);
    EntityBase? ConvertPlannedStateToRuntimeState(EconomyRuntimeSeedPlannedState planned);
    Task<bool> StateExistsAsync(EconomyRuntimeSeedPlannedState planned);
    Task<EconomyRuntimeSeedCreatedState?> CreateStateAsync(EconomyRuntimeSeedPlannedState planned);
}

public sealed class EconomyRuntimeSeedService : IEconomyRuntimeSeedService
{
    private readonly IEconomyRuntimeSeedPlanner _planner;
    private readonly INriRepositoryFactory _repositories;
    private readonly IServerLogger _logger;

    public EconomyRuntimeSeedService(IEconomyRuntimeSeedPlanner planner, INriRepositoryFactory repositories, IServerLogger logger)
    {
        _planner = planner;
        _repositories = repositories;
        _logger = logger;
    }

    public async Task<EconomyRuntimeSeedResult> ValidateSeedRequestAsync(EconomyRuntimeSeedRequest request)
    {
        var result = CreateResult(request ?? new EconomyRuntimeSeedRequest());
        var safeRequest = request ?? new EconomyRuntimeSeedRequest();
        if (string.IsNullOrWhiteSpace(safeRequest.RuleSetId)) result.Errors.Add("ruleset_id_required");
        if (string.IsNullOrWhiteSpace(safeRequest.CampaignId)) result.Errors.Add("campaign_id_required");
        if (string.IsNullOrWhiteSpace(safeRequest.PackPath)) result.Errors.Add("pack_path_required");
        if (safeRequest.AllowOverwrite) result.Errors.Add("overwrite_not_supported");
        Finish(result);
        return await Task.FromResult(result);
    }

    public async Task<EconomyRuntimeSeedResult> SeedFromDefinitionsAsync(EconomyRuntimeSeedRequest request)
    {
        var safeRequest = request ?? new EconomyRuntimeSeedRequest();
        var result = CreateResult(safeRequest);
        try
        {
            _logger.Admin($"economy.seed.apply.start campaignId={safeRequest.CampaignId} ruleSetId={safeRequest.RuleSetId}");
            var requestValidation = await ValidateSeedRequestAsync(safeRequest);
            result.Errors.AddRange(requestValidation.Errors);
            result.Warnings.AddRange(requestValidation.Warnings);
            if (result.Errors.Count > 0)
            {
                Finish(result);
                WriteAuditSafe(safeRequest, result);
                return result;
            }

            var dryRun = await _planner.BuildDryRunPlanFromPackAsync(ToDryRunRequest(safeRequest));
            result.PackId = FirstNonEmpty(safeRequest.PackId, dryRun.PackId);
            result.Errors.AddRange(dryRun.Errors);
            result.Warnings.AddRange(dryRun.Warnings);
            result.Warnings.AddRange(dryRun.PlannedStates.SelectMany(x => x.Warnings));

            var dryRunPlanErrors = dryRun.PlannedStates.SelectMany(x => x.Errors).ToList();
            if (safeRequest.RequireDryRunSuccess && (dryRun.Errors.Count > 0 || dryRunPlanErrors.Count > 0))
            {
                result.Errors.AddRange(dryRunPlanErrors);
                _logger.Admin($"economy.seed.apply.dryrun_failed errors={dryRun.Errors.Count + dryRunPlanErrors.Count}");
                Finish(result);
                WriteAuditSafe(safeRequest, result);
                return result;
            }

            if (safeRequest.ValidateOnly)
            {
                result.Warnings.Add("validate_only_no_writes");
                Finish(result);
                WriteAuditSafe(safeRequest, result);
                return result;
            }

            foreach (var planned in dryRun.PlannedStates)
            {
                var planErrors = ValidatePlannedState(planned);
                if (planErrors.Count > 0)
                {
                    result.Errors.AddRange(planErrors);
                    _logger.Admin($"economy.seed.apply.error type={planned.RuntimeType} id={planned.ProposedId} message=planned_state_invalid");
                    break;
                }

                if (await StateExistsAsync(planned))
                {
                    result.SkippedStates.Add(new EconomyRuntimeSeedSkippedState
                    {
                        RuntimeType = planned.RuntimeType,
                        ProposedId = planned.ProposedId,
                        DefinitionId = planned.DefinitionId,
                        Reason = "already_exists"
                    });
                    _logger.Admin($"economy.seed.apply.skipped type={planned.RuntimeType} id={planned.ProposedId} reason=already_exists");
                    continue;
                }

                try
                {
                    var created = await CreateStateAsync(planned);
                    if (created == null)
                    {
                        result.Errors.Add($"unsupported_runtime_type:{planned.RuntimeType}");
                        _logger.Admin($"economy.seed.apply.error type={planned.RuntimeType} id={planned.ProposedId} message=unsupported_runtime_type");
                        break;
                    }

                    result.CreatedStates.Add(created);
                    _logger.Admin($"economy.seed.apply.created type={created.RuntimeType} id={created.Id} definitionId={created.DefinitionId}");
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"write_failed:{planned.RuntimeType}:{planned.ProposedId}:{SafeLog(ex.Message)}");
                    _logger.Admin($"economy.seed.apply.error type={planned.RuntimeType} id={planned.ProposedId} message={SafeLog(ex.Message)}");
                    break;
                }
            }

            Finish(result);
            WriteAuditSafe(safeRequest, result);
            _logger.Admin($"economy.seed.apply.done success={result.Success} created={result.CreatedStates.Count} skipped={result.SkippedStates.Count} errors={result.Errors.Count}");
            return result;
        }
        catch (Exception ex)
        {
            result.Errors.Add(SafeLog(ex.Message));
            Finish(result);
            _logger.Admin($"economy.seed.apply.error message={SafeLog(ex.Message)}");
            WriteAuditSafe(safeRequest, result);
            return result;
        }
    }

    public EntityBase? ConvertPlannedStateToRuntimeState(EconomyRuntimeSeedPlannedState planned)
    {
        if (planned == null) return null;
        return planned.RuntimeType switch
        {
            EconomyRuntimeKinds.Faction => CreateFactionState(planned),
            EconomyRuntimeKinds.Organization => CreateOrganizationState(planned),
            EconomyRuntimeKinds.Law => CreateLawState(planned),
            EconomyRuntimeKinds.Restriction => CreateRestrictionState(planned),
            EconomyRuntimeKinds.Market => CreateMarketState(planned),
            EconomyRuntimeKinds.EconomyScope => CreateEconomyScopeState(planned),
            _ => null
        };
    }

    public async Task<bool> StateExistsAsync(EconomyRuntimeSeedPlannedState planned)
    {
        if (planned == null || string.IsNullOrWhiteSpace(planned.ProposedId)) return false;
        return planned.RuntimeType switch
        {
            EconomyRuntimeKinds.Faction => await _repositories.FactionStates.GetByIdAsync(planned.ProposedId) != null,
            EconomyRuntimeKinds.Organization => await _repositories.OrganizationStates.GetByIdAsync(planned.ProposedId) != null,
            EconomyRuntimeKinds.Law => await _repositories.LawStates.GetByIdAsync(planned.ProposedId) != null,
            EconomyRuntimeKinds.Restriction => await _repositories.RestrictionStates.GetByIdAsync(planned.ProposedId) != null,
            EconomyRuntimeKinds.Market => await _repositories.MarketStates.GetByIdAsync(planned.ProposedId) != null,
            EconomyRuntimeKinds.EconomyScope => await _repositories.EconomyScopeStates.GetByIdAsync(planned.ProposedId) != null,
            _ => false
        };
    }

    public async Task<EconomyRuntimeSeedCreatedState?> CreateStateAsync(EconomyRuntimeSeedPlannedState planned)
    {
        var state = ConvertPlannedStateToRuntimeState(planned);
        if (state == null) return null;
        var validation = ValidateRuntimeState(state);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(string.Join("; ", validation.Errors));
        }

        switch (state)
        {
            case FactionState faction:
                await _repositories.FactionStates.UpsertAsync(faction);
                return Created(planned, faction.Id, "faction_states");
            case OrganizationState organization:
                await _repositories.OrganizationStates.UpsertAsync(organization);
                return Created(planned, organization.Id, "organization_states");
            case LawState law:
                await _repositories.LawStates.UpsertAsync(law);
                return Created(planned, law.Id, "law_states");
            case RestrictionState restriction:
                await _repositories.RestrictionStates.UpsertAsync(restriction);
                return Created(planned, restriction.Id, "restriction_states");
            case MarketState market:
                await _repositories.MarketStates.UpsertAsync(market);
                return Created(planned, market.Id, "market_states");
            case EconomyScopeState scope:
                await _repositories.EconomyScopeStates.UpsertAsync(scope);
                return Created(planned, scope.Id, "economy_scope_states");
            default:
                return null;
        }
    }

    private static FactionState CreateFactionState(EconomyRuntimeSeedPlannedState planned)
    {
        return new FactionState
        {
            Id = planned.ProposedId,
            DefinitionId = planned.DefinitionId,
            Name = planned.Name,
            RuleSetId = planned.RuleSetId,
            CampaignId = planned.CampaignId,
            CountryId = GetPreviewString(planned, "countryId"),
            CityStateId = GetPreviewString(planned, "cityStateId"),
            PublicAlignment = GetPreviewString(planned, "publicAlignment"),
            SecrecyLevel = GetPreviewString(planned, "secrecyLevel"),
            InfluenceLevel = GetPreviewInt(planned, "influenceLevel"),
            MilitaryInfluence = GetPreviewInt(planned, "militaryInfluence"),
            EconomicInfluence = GetPreviewInt(planned, "economicInfluence"),
            PoliticalInfluence = GetPreviewInt(planned, "politicalInfluence"),
            MagicInfluence = GetPreviewInt(planned, "magicInfluence"),
            Tags = GetPreviewStringList(planned, "tags")
        };
    }

    private static OrganizationState CreateOrganizationState(EconomyRuntimeSeedPlannedState planned)
    {
        return new OrganizationState
        {
            Id = planned.ProposedId,
            DefinitionId = planned.DefinitionId,
            Name = planned.Name,
            RuleSetId = planned.RuleSetId,
            CampaignId = planned.CampaignId,
            ParentFactionId = GetPreviewString(planned, "parentFactionId"),
            CountryId = GetPreviewString(planned, "countryId"),
            CityStateId = GetPreviewString(planned, "cityStateId"),
            LocationIds = GetPreviewStringList(planned, "locationIds"),
            PublicStatus = GetPreviewString(planned, "publicStatus"),
            LegalStatus = GetPreviewString(planned, "legalStatus"),
            AccessLevel = GetPreviewString(planned, "accessLevel"),
            SecrecyLevel = GetPreviewString(planned, "secrecyLevel"),
            ServiceTags = GetPreviewStringList(planned, "serviceTags"),
            ResourceTags = GetPreviewStringList(planned, "resourceTags"),
            RecruitmentTags = GetPreviewStringList(planned, "recruitmentTags"),
            Tags = GetPreviewStringList(planned, "tags")
        };
    }

    private static LawState CreateLawState(EconomyRuntimeSeedPlannedState planned)
    {
        return new LawState
        {
            Id = planned.ProposedId,
            DefinitionId = planned.DefinitionId,
            Name = planned.Name,
            RuleSetId = planned.RuleSetId,
            CampaignId = planned.CampaignId,
            CountryIds = GetPreviewStringList(planned, "countryIds"),
            CityStateIds = GetPreviewStringList(planned, "cityStateIds"),
            LawType = GetPreviewString(planned, "lawType"),
            Severity = GetPreviewString(planned, "severity"),
            EnforcementLevel = GetPreviewString(planned, "enforcementLevel"),
            RelatedRestrictionIds = GetPreviewStringList(planned, "relatedRestrictionIds"),
            IsActive = GetPreviewBool(planned, "isActive", true),
            IsPubliclyKnown = GetPreviewBool(planned, "isPubliclyKnown", true),
            Tags = GetPreviewStringList(planned, "tags")
        };
    }

    private static RestrictionState CreateRestrictionState(EconomyRuntimeSeedPlannedState planned)
    {
        return new RestrictionState
        {
            Id = planned.ProposedId,
            DefinitionId = planned.DefinitionId,
            Name = planned.Name,
            RuleSetId = planned.RuleSetId,
            CampaignId = planned.CampaignId,
            RestrictionType = GetPreviewString(planned, "restrictionType"),
            AppliesToTags = GetPreviewStringList(planned, "appliesToTags"),
            CountryIds = GetPreviewStringList(planned, "countryIds"),
            CityStateIds = GetPreviewStringList(planned, "cityStateIds"),
            RelatedLawIds = GetPreviewStringList(planned, "relatedLawIds"),
            LicenseRequired = GetPreviewBool(planned, "licenseRequired", false),
            GMApprovalRequired = GetPreviewBool(planned, "gmApprovalRequired", false),
            IsActive = GetPreviewBool(planned, "isActive", true),
            Tags = GetPreviewStringList(planned, "tags")
        };
    }

    private static MarketState CreateMarketState(EconomyRuntimeSeedPlannedState planned)
    {
        return new MarketState
        {
            Id = planned.ProposedId,
            DefinitionId = planned.DefinitionId,
            Name = planned.Name,
            RuleSetId = planned.RuleSetId,
            CampaignId = planned.CampaignId,
            CountryId = GetPreviewString(planned, "countryId"),
            CityStateId = GetPreviewString(planned, "cityStateId"),
            MarketTagIds = GetPreviewStringList(planned, "marketTagIds"),
            AvailableCurrencyIds = GetPreviewStringList(planned, "availableCurrencyIds"),
            AvailabilityProfile = GetPreviewString(planned, "availabilityProfile"),
            PricePolicy = GetPreviewString(planned, "pricePolicy"),
            IsBlackMarket = GetPreviewBool(planned, "isBlackMarket", false),
            IsActive = GetPreviewBool(planned, "isActive", true),
            Tags = GetPreviewStringList(planned, "tags")
        };
    }

    private static EconomyScopeState CreateEconomyScopeState(EconomyRuntimeSeedPlannedState planned)
    {
        return new EconomyScopeState
        {
            Id = planned.ProposedId,
            RuleSetId = planned.RuleSetId,
            CampaignId = planned.CampaignId,
            ScopeType = GetPreviewString(planned, "scopeType"),
            CountryId = GetPreviewString(planned, "countryId"),
            CityStateId = GetPreviewString(planned, "cityStateId"),
            CurrencyIds = GetPreviewStringList(planned, "currencyIds"),
            ActiveLawIds = GetPreviewStringList(planned, "activeLawIds"),
            ActiveRestrictionIds = GetPreviewStringList(planned, "activeRestrictionIds")
        };
    }

    private static EconomyRuntimeValidationResult ValidateRuntimeState(EntityBase state)
    {
        return state switch
        {
            FactionState faction => EconomyRuntimeValidator.ValidateFactionState(faction),
            OrganizationState organization => EconomyRuntimeValidator.ValidateOrganizationState(organization),
            LawState law => EconomyRuntimeValidator.ValidateLawState(law),
            RestrictionState restriction => EconomyRuntimeValidator.ValidateRestrictionState(restriction),
            MarketState market => EconomyRuntimeValidator.ValidateMarketState(market),
            EconomyScopeState scope => EconomyRuntimeValidator.ValidateEconomyScopeState(scope),
            _ => new EconomyRuntimeValidationResult { IsValid = false, Errors = new List<string> { "unsupported_runtime_state" } }
        };
    }

    private static EconomyRuntimeSeedDryRunRequest ToDryRunRequest(EconomyRuntimeSeedRequest request)
    {
        return new EconomyRuntimeSeedDryRunRequest
        {
            RuleSetId = request.RuleSetId,
            CampaignId = request.CampaignId,
            PackId = request.PackId,
            PackPath = request.PackPath,
            IncludeFactions = request.IncludeFactions,
            IncludeOrganizations = request.IncludeOrganizations,
            IncludeLaws = request.IncludeLaws,
            IncludeRestrictions = request.IncludeRestrictions,
            IncludeMarkets = request.IncludeMarkets,
            IncludeEconomyScopes = request.IncludeEconomyScopes,
            ActorUserId = request.ActorUserId,
            RequestId = request.RequestId
        };
    }

    private static List<string> ValidatePlannedState(EconomyRuntimeSeedPlannedState planned)
    {
        var errors = new List<string>();
        if (planned == null)
        {
            errors.Add("planned_state_null");
            return errors;
        }

        if (string.IsNullOrWhiteSpace(planned.ProposedId)) errors.Add("proposed_id_required");
        if (string.IsNullOrWhiteSpace(planned.RuntimeType)) errors.Add("runtime_type_required");
        if (string.IsNullOrWhiteSpace(planned.DefinitionId)) errors.Add("definition_id_required");
        if (string.IsNullOrWhiteSpace(planned.CampaignId)) errors.Add("campaign_id_required");
        if (string.IsNullOrWhiteSpace(planned.RuleSetId)) errors.Add("ruleset_id_required");
        if (!IsSupportedRuntimeType(planned.RuntimeType)) errors.Add($"unsupported_runtime_type:{planned.RuntimeType}");
        errors.AddRange(planned.Errors);
        return errors;
    }

    private static bool IsSupportedRuntimeType(string runtimeType)
    {
        return runtimeType == EconomyRuntimeKinds.Faction
            || runtimeType == EconomyRuntimeKinds.Organization
            || runtimeType == EconomyRuntimeKinds.Law
            || runtimeType == EconomyRuntimeKinds.Restriction
            || runtimeType == EconomyRuntimeKinds.Market
            || runtimeType == EconomyRuntimeKinds.EconomyScope;
    }

    private static EconomyRuntimeSeedResult CreateResult(EconomyRuntimeSeedRequest request)
    {
        return new EconomyRuntimeSeedResult
        {
            RuleSetId = request.RuleSetId ?? string.Empty,
            CampaignId = request.CampaignId ?? string.Empty,
            PackId = request.PackId ?? string.Empty,
            SeededAtUtc = DateTime.UtcNow
        };
    }

    private static void Finish(EconomyRuntimeSeedResult result)
    {
        result.Summary = new EconomyRuntimeSeedWriteSummary
        {
            CreatedFactions = result.CreatedStates.Count(x => x.RuntimeType == EconomyRuntimeKinds.Faction),
            CreatedOrganizations = result.CreatedStates.Count(x => x.RuntimeType == EconomyRuntimeKinds.Organization),
            CreatedLaws = result.CreatedStates.Count(x => x.RuntimeType == EconomyRuntimeKinds.Law),
            CreatedRestrictions = result.CreatedStates.Count(x => x.RuntimeType == EconomyRuntimeKinds.Restriction),
            CreatedMarkets = result.CreatedStates.Count(x => x.RuntimeType == EconomyRuntimeKinds.Market),
            CreatedEconomyScopes = result.CreatedStates.Count(x => x.RuntimeType == EconomyRuntimeKinds.EconomyScope),
            SkippedExisting = result.SkippedStates.Count(x => x.Reason == "already_exists"),
            ErrorCount = result.Errors.Count,
            WarningCount = result.Warnings.Count
        };
        result.Success = result.Errors.Count == 0;
        result.SeededAtUtc = DateTime.UtcNow;
    }

    private void WriteAuditSafe(EconomyRuntimeSeedRequest request, EconomyRuntimeSeedResult result)
    {
        try
        {
            _repositories.AuditLogs.Insert(new AuditLogEntry
            {
                Category = "economy.runtime_seed",
                ActorUserId = request.ActorUserId ?? string.Empty,
                Action = "apply",
                Target = request.CampaignId ?? string.Empty,
                DetailsJson = JsonSerializer.Serialize(new
                {
                    requestId = request.RequestId,
                    campaignId = request.CampaignId,
                    ruleSetId = request.RuleSetId,
                    packId = result.PackId,
                    createdCount = result.CreatedStates.Count,
                    skippedCount = result.SkippedStates.Count,
                    errorCount = result.Errors.Count
                })
            });
        }
        catch (Exception ex)
        {
            _logger.Debug($"economy.seed.apply.audit_error message={SafeLog(ex.Message)}");
        }
    }

    private static EconomyRuntimeSeedCreatedState Created(EconomyRuntimeSeedPlannedState planned, string id, string collectionName)
    {
        return new EconomyRuntimeSeedCreatedState
        {
            RuntimeType = planned.RuntimeType,
            Id = id,
            DefinitionId = planned.DefinitionId,
            Name = planned.Name,
            CollectionName = collectionName
        };
    }

    private static string GetPreviewString(EconomyRuntimeSeedPlannedState planned, string key)
    {
        if (planned.PreviewData == null || !planned.PreviewData.TryGetValue(key, out var value) || value == null) return string.Empty;
        if (value is string s) return s.Trim();
        if (value is JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.String) return (element.GetString() ?? string.Empty).Trim();
            if (element.ValueKind == JsonValueKind.Number || element.ValueKind == JsonValueKind.True || element.ValueKind == JsonValueKind.False) return element.ToString().Trim();
            return string.Empty;
        }

        return Convert.ToString(value)?.Trim() ?? string.Empty;
    }

    private static List<string> GetPreviewStringList(EconomyRuntimeSeedPlannedState planned, string key)
    {
        var values = new List<string>();
        if (planned.PreviewData == null || !planned.PreviewData.TryGetValue(key, out var value) || value == null) return values;
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

    private static int GetPreviewInt(EconomyRuntimeSeedPlannedState planned, string key)
    {
        if (planned.PreviewData == null || !planned.PreviewData.TryGetValue(key, out var value) || value == null) return 0;
        if (value is int i) return i;
        if (value is long l && l >= int.MinValue && l <= int.MaxValue) return (int)l;
        if (value is JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var parsed)) return parsed;
            if (element.ValueKind == JsonValueKind.String && int.TryParse(element.GetString(), out parsed)) return parsed;
        }

        return int.TryParse(Convert.ToString(value), out var result) ? result : 0;
    }

    private static bool GetPreviewBool(EconomyRuntimeSeedPlannedState planned, string key, bool defaultValue)
    {
        if (planned.PreviewData == null || !planned.PreviewData.TryGetValue(key, out var value) || value == null) return defaultValue;
        if (value is bool b) return b;
        if (value is JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.True) return true;
            if (element.ValueKind == JsonValueKind.False) return false;
            if (element.ValueKind == JsonValueKind.String && bool.TryParse(element.GetString(), out var parsed)) return parsed;
        }

        return bool.TryParse(Convert.ToString(value), out var result) ? result : defaultValue;
    }

    private static string FirstNonEmpty(params string[] values)
        => values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;

    private static string SafeLog(string value)
        => (value ?? string.Empty).Replace(Environment.NewLine, " ");
}
