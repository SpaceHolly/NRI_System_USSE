using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nri.Server.Infrastructure.Mongo.Repositories;
using Nri.Server.Logging;
using Nri.Shared.Domain;

namespace Nri.Server.Application.Services;

public interface IInventoryLegalityReadModelService
{
    Task<InventoryItemLegalityResult> EvaluateItemLegalityAsync(InventoryItemLegalityRequest request);
    Task<InventoryLegalityBatchResult> EvaluateInventoryLegalityAsync(InventoryLegalityBatchRequest request);
    Task<IReadOnlyCollection<string>> ResolveRelevantRestrictionsAsync(InventoryItemInstanceState item, InventoryLegalityContext context);
    Task<IReadOnlyCollection<string>> ResolveRelevantLawsAsync(InventoryItemInstanceState item, InventoryLegalityContext context);
    Task<IReadOnlyCollection<string>> ResolveRelevantMarketTagsAsync(InventoryItemInstanceState item, InventoryLegalityContext context);
}

public sealed class InventoryLegalityReadModelService : IInventoryLegalityReadModelService
{
    private readonly IItemEquipmentDefinitionResolver _itemResolver;
    private readonly IDefinitionRepositoryV2? _definitionRepository;
    private readonly ILawStateRepository? _lawStates;
    private readonly IRestrictionStateRepository? _restrictionStates;
    private readonly IMarketStateRepository? _marketStates;
    private readonly IEconomyScopeStateRepository? _economyScopeStates;
    private readonly IServerLogger? _logger;

    public InventoryLegalityReadModelService(
        IItemEquipmentDefinitionResolver itemResolver,
        IDefinitionRepositoryV2? definitionRepository = null,
        ILawStateRepository? lawStates = null,
        IRestrictionStateRepository? restrictionStates = null,
        IMarketStateRepository? marketStates = null,
        IEconomyScopeStateRepository? economyScopeStates = null,
        IServerLogger? logger = null)
    {
        _itemResolver = itemResolver ?? throw new ArgumentNullException(nameof(itemResolver));
        _definitionRepository = definitionRepository;
        _lawStates = lawStates;
        _restrictionStates = restrictionStates;
        _marketStates = marketStates;
        _economyScopeStates = economyScopeStates;
        _logger = logger;
    }

    public async Task<InventoryItemLegalityResult> EvaluateItemLegalityAsync(InventoryItemLegalityRequest request)
    {
        var context = request?.Context ?? new InventoryLegalityContext();
        var item = request?.Item ?? new InventoryItemInstanceState();
        _logger?.Debug($"inventory.legality.evaluate.start itemCount=1 countryId={context.CountryId} cityStateId={context.CityStateId}");

        var result = new InventoryItemLegalityResult
        {
            ItemInstanceId = item.ItemInstanceId ?? string.Empty,
            DefinitionId = item.DefinitionId ?? string.Empty,
            DisplayName = item.DisplayName ?? string.Empty,
            LegalityStatus = InventoryLegalityStatusIds.Unknown,
            CheckedAtUtc = DateTime.UtcNow
        };

        var metadata = await BuildItemMetadataAsync(item, context.RuleSetId);
        AddWarnings(result, metadata.Warnings, item.ItemInstanceId, item.DefinitionId);
        if (metadata.DefinitionMissing)
        {
            AddWarning(result, "item_definition_missing_for_legality", "Item definition was not resolved for legality analysis.", item.ItemInstanceId, item.DefinitionId);
        }

        var contextMissing = string.IsNullOrWhiteSpace(context.CountryId) && string.IsNullOrWhiteSpace(context.CityStateId);
        if (contextMissing)
        {
            result.LegalityStatus = InventoryLegalityStatusIds.ContextMissing;
            AddByMode(result, context.StrictMode, "legality_context_missing", "CountryId and CityStateId are empty; legality context is incomplete.", item.ItemInstanceId, item.DefinitionId);
        }

        var laws = ResolveLawDefinitions(metadata, context, result);
        var restrictions = ResolveRestrictionDefinitions(metadata, context, result);
        var marketTags = ResolveMarketTagDefinitions(metadata, context, result);

        foreach (var law in laws) AddUnique(result.MatchedLawIds, law.Id);
        foreach (var restriction in restrictions) AddUnique(result.MatchedRestrictionIds, restriction.Id);
        foreach (var marketTag in marketTags) AddUnique(result.MatchedMarketTagIds, marketTag.Id);

        ApplyRestrictionSignals(result, restrictions, item, context.StrictMode);
        ApplyLawSignals(result, laws, item, context.StrictMode);
        ApplyMarketSignals(result, marketTags, item);

        if (metadata.IsRestricted)
        {
            result.IsRestricted = true;
            AddWarning(result, "restricted_item_detected", "Item definition or legal tags mark this item as restricted.", item.ItemInstanceId, item.DefinitionId);
        }

        await ApplyRuntimeSignalsAsync(result, metadata, context, item);
        CalculateStatus(result, contextMissing);

        _logger?.Debug($"inventory.legality.evaluate.done items=1 warnings={result.Warnings.Count} errors={result.Errors.Count}");
        return result;
    }

    public async Task<InventoryLegalityBatchResult> EvaluateInventoryLegalityAsync(InventoryLegalityBatchRequest request)
    {
        var context = request?.Context ?? new InventoryLegalityContext();
        var items = request?.Items ?? new List<InventoryItemInstanceState>();
        _logger?.Debug($"inventory.legality.evaluate.start itemCount={items.Count} countryId={context.CountryId} cityStateId={context.CityStateId}");

        var result = new InventoryLegalityBatchResult { CheckedAtUtc = DateTime.UtcNow };
        foreach (var item in items.Where(x => x != null))
        {
            var itemResult = await EvaluateItemLegalityAsync(new InventoryItemLegalityRequest { Item = item, Context = context });
            result.Items.Add(itemResult);
            result.Warnings.AddRange(itemResult.Warnings);
            result.Errors.AddRange(itemResult.Errors);
        }

        result.IsValid = result.Errors.Count == 0;
        _logger?.Debug($"inventory.legality.evaluate.done items={result.Items.Count} warnings={result.Warnings.Count} errors={result.Errors.Count}");
        return result;
    }

    public async Task<IReadOnlyCollection<string>> ResolveRelevantRestrictionsAsync(InventoryItemInstanceState item, InventoryLegalityContext context)
    {
        var metadata = await BuildItemMetadataAsync(item ?? new InventoryItemInstanceState(), context?.RuleSetId ?? string.Empty);
        var sink = new InventoryItemLegalityResult();
        return ResolveRestrictionDefinitions(metadata, context ?? new InventoryLegalityContext(), sink).Select(x => x.Id).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    public async Task<IReadOnlyCollection<string>> ResolveRelevantLawsAsync(InventoryItemInstanceState item, InventoryLegalityContext context)
    {
        var metadata = await BuildItemMetadataAsync(item ?? new InventoryItemInstanceState(), context?.RuleSetId ?? string.Empty);
        var sink = new InventoryItemLegalityResult();
        return ResolveLawDefinitions(metadata, context ?? new InventoryLegalityContext(), sink).Select(x => x.Id).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    public async Task<IReadOnlyCollection<string>> ResolveRelevantMarketTagsAsync(InventoryItemInstanceState item, InventoryLegalityContext context)
    {
        var metadata = await BuildItemMetadataAsync(item ?? new InventoryItemInstanceState(), context?.RuleSetId ?? string.Empty);
        var sink = new InventoryItemLegalityResult();
        return ResolveMarketTagDefinitions(metadata, context ?? new InventoryLegalityContext(), sink).Select(x => x.Id).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private async Task<ItemLegalityMetadata> BuildItemMetadataAsync(InventoryItemInstanceState item, string ruleSetId)
    {
        var metadata = new ItemLegalityMetadata();
        AddTags(metadata.Tags, item.Tags);
        AddTag(metadata.Tags, item.ItemType);
        AddTag(metadata.Tags, item.ItemCode);

        if (string.IsNullOrWhiteSpace(item.DefinitionId))
        {
            metadata.DefinitionMissing = true;
            return metadata;
        }

        foreach (var category in GetCategorySearchOrder(item))
        {
            if (string.Equals(category, DefinitionCategoryIds.Weapon, StringComparison.OrdinalIgnoreCase))
            {
                var weapon = await _itemResolver.ResolveWeaponAsync(item.DefinitionId, ruleSetId);
                if (weapon.Success && weapon.Value != null)
                {
                    AddTags(metadata.Tags, weapon.Value.Tags);
                    AddTags(metadata.Tags, weapon.Value.LegalTags);
                    AddTags(metadata.Tags, weapon.Value.TechTags);
                    AddTags(metadata.Tags, weapon.Value.MagicTags);
                    AddTag(metadata.Tags, weapon.Value.WeaponType);
                    AddTag(metadata.Tags, "weapon");
                    metadata.IsRestricted = ContainsAny(weapon.Value.LegalTags, "restricted", "military", "controlled", "weapon_restricted");
                    return metadata;
                }
            }
            else if (string.Equals(category, DefinitionCategoryIds.Armor, StringComparison.OrdinalIgnoreCase))
            {
                var armor = await _itemResolver.ResolveArmorAsync(item.DefinitionId, ruleSetId);
                if (armor.Success && armor.Value != null)
                {
                    AddTags(metadata.Tags, armor.Value.Tags);
                    AddTag(metadata.Tags, armor.Value.ArmorType);
                    AddTag(metadata.Tags, "armor");
                    metadata.IsRestricted = ContainsAny(armor.Value.Tags, "restricted", "military", "controlled");
                    return metadata;
                }
            }
            else if (string.Equals(category, DefinitionCategoryIds.Ammo, StringComparison.OrdinalIgnoreCase))
            {
                var ammo = await _itemResolver.ResolveAmmoAsync(item.DefinitionId, ruleSetId);
                if (ammo.Success && ammo.Value != null)
                {
                    AddTags(metadata.Tags, ammo.Value.Tags);
                    AddTag(metadata.Tags, ammo.Value.AmmoType);
                    AddTag(metadata.Tags, "ammo");
                    metadata.IsRestricted = ContainsAny(ammo.Value.Tags, "restricted", "military", "controlled");
                    return metadata;
                }
            }
            else if (string.Equals(category, DefinitionCategoryIds.Item, StringComparison.OrdinalIgnoreCase))
            {
                var generic = await _itemResolver.ResolveItemAsync(item.DefinitionId, ruleSetId);
                if (generic.Success && generic.Value != null)
                {
                    AddTags(metadata.Tags, generic.Value.Tags);
                    AddTags(metadata.Tags, generic.Value.SourceDefinitionTags);
                    AddTag(metadata.Tags, generic.Value.ItemType);
                    AddTag(metadata.Tags, "item");
                    metadata.IsRestricted = generic.Value.IsRestricted || ContainsAny(generic.Value.Tags, "restricted", "military", "controlled");
                    return metadata;
                }
            }
        }

        metadata.DefinitionMissing = true;
        return metadata;
    }

    private List<LawDefinitionReadModel> ResolveLawDefinitions(ItemLegalityMetadata metadata, InventoryLegalityContext context, InventoryItemLegalityResult result)
    {
        var list = QueryDefinitions(DefinitionCategoryIds.Law, context.RuleSetId, result, "law_definition_missing")
            .Select(ToLawModel)
            .Where(x => x != null)
            .Cast<LawDefinitionReadModel>()
            .ToList();

        return list.Where(x => ContextApplies(x.CountryIds, x.CityStateIds, context, result, "law_definition_general_context")
            && (Intersects(metadata.Tags, x.AffectedItemTags) || Intersects(metadata.Tags, x.AffectedMagicTags))).ToList();
    }

    private List<RestrictionDefinitionReadModel> ResolveRestrictionDefinitions(ItemLegalityMetadata metadata, InventoryLegalityContext context, InventoryItemLegalityResult result)
    {
        var list = QueryDefinitions(DefinitionCategoryIds.Restriction, context.RuleSetId, result, "restriction_definition_missing")
            .Select(ToRestrictionModel)
            .Where(x => x != null)
            .Cast<RestrictionDefinitionReadModel>()
            .ToList();

        return list.Where(x => ContextApplies(x.CountryIds, x.CityStateIds, context, result, "restriction_definition_general_context")
            && Intersects(metadata.Tags, x.AppliesToTags)).ToList();
    }

    private List<MarketTagDefinitionReadModel> ResolveMarketTagDefinitions(ItemLegalityMetadata metadata, InventoryLegalityContext context, InventoryItemLegalityResult result)
    {
        var list = QueryDefinitions(DefinitionCategoryIds.MarketTag, context.RuleSetId, result, "market_tag_definition_missing")
            .Select(ToMarketTagModel)
            .Where(x => x != null)
            .Cast<MarketTagDefinitionReadModel>()
            .ToList();

        return list.Where(x => Intersects(metadata.Tags, x.RelatedItemTags)).ToList();
    }

    private IReadOnlyCollection<UnifiedDefinitionDocument> QueryDefinitions(string category, string ruleSetId, InventoryItemLegalityResult result, string missingCode)
    {
        if (_definitionRepository == null)
        {
            AddWarning(result, missingCode, $"Definition repository is not available for category '{category}'.", result.ItemInstanceId, result.DefinitionId);
            return new List<UnifiedDefinitionDocument>();
        }

        try
        {
            return _definitionRepository.QueryAsync(new DefinitionQuery
            {
                Category = category,
                RuleSetId = ruleSetId ?? string.Empty,
                IncludeArchived = false,
                Limit = 500
            });
        }
        catch
        {
            AddWarning(result, missingCode, $"Definition query failed for category '{category}'.", result.ItemInstanceId, result.DefinitionId);
            return new List<UnifiedDefinitionDocument>();
        }
    }

    private async Task ApplyRuntimeSignalsAsync(InventoryItemLegalityResult result, ItemLegalityMetadata metadata, InventoryLegalityContext context, InventoryItemInstanceState item)
    {
        if (!context.IncludeRuntimeStates) return;
        if (!RuntimeLookupEnabled())
        {
            AddWarning(result, "runtime_law_lookup_disabled", "Runtime law/restriction lookup is disabled by feature flag.", item.ItemInstanceId, item.DefinitionId);
            return;
        }

        if (string.IsNullOrWhiteSpace(context.CampaignId))
        {
            AddWarning(result, "runtime_law_state_missing", "CampaignId is empty; runtime law/restriction lookup skipped.", item.ItemInstanceId, item.DefinitionId);
            return;
        }

        var laws = _lawStates == null ? new List<LawState>() : (await _lawStates.ListByCampaignAsync(context.CampaignId, 500)).Where(x => x != null && x.IsActive).ToList();
        var restrictions = _restrictionStates == null ? new List<RestrictionState>() : (await _restrictionStates.ListByCampaignAsync(context.CampaignId, 500)).Where(x => x != null && x.IsActive).ToList();
        var markets = _marketStates == null ? new List<MarketState>() : (await _marketStates.ListByCampaignAsync(context.CampaignId, 500)).Where(x => x != null && x.IsActive).ToList();
        if (_economyScopeStates != null)
        {
            await _economyScopeStates.ListByCampaignAsync(context.CampaignId, 50);
        }

        foreach (var law in laws.Where(x => ContextApplies(x.CountryIds, x.CityStateIds, context, result, "runtime_law_general_context") && Intersects(metadata.Tags, x.Tags)))
        {
            AddUnique(result.MatchedLawIds, FirstNonEmpty(law.DefinitionId, law.Id));
            if (LooksForbidden(law.LawType) || LooksForbidden(law.Severity) || LooksForbidden(law.EnforcementLevel)) result.IsForbidden = true;
        }

        foreach (var restriction in restrictions.Where(x => ContextApplies(x.CountryIds, x.CityStateIds, context, result, "runtime_restriction_general_context") && Intersects(metadata.Tags, x.AppliesToTags.Concat(x.Tags).ToList())))
        {
            AddUnique(result.MatchedRestrictionIds, FirstNonEmpty(restriction.DefinitionId, restriction.Id));
            if (restriction.LicenseRequired) result.RequiresLicense = true;
            if (restriction.GMApprovalRequired) result.RequiresGMApproval = true;
            if (LooksForbidden(restriction.RestrictionType)) result.IsForbidden = true;
            result.IsRestricted = true;
        }

        foreach (var market in markets.Where(x => Intersects(metadata.Tags, x.Tags.Concat(x.LegalTagIds).Concat(x.RestrictedTagIds).ToList())))
        {
            foreach (var marketTagId in market.MarketTagIds ?? new List<string>()) AddUnique(result.MatchedMarketTagIds, marketTagId);
            if (market.IsBlackMarket || ContainsAny(market.RestrictedTagIds, "black", "illegal", "restricted"))
            {
                result.IsBlackMarketRelevant = true;
            }
        }

        if (laws.Count == 0 && restrictions.Count == 0)
        {
            AddWarning(result, "runtime_law_state_missing", "No active runtime law/restriction states were found for campaign.", item.ItemInstanceId, item.DefinitionId);
        }
    }

    private static LawDefinitionReadModel? ToLawModel(UnifiedDefinitionDocument definition)
    {
        if (definition == null) return null;
        var reader = new DefinitionExtraDataReader(definition.ExtraData);
        return new LawDefinitionReadModel
        {
            Id = definition.Id ?? string.Empty,
            AffectedItemTags = reader.GetStringList("affectedItemTags"),
            AffectedMagicTags = reader.GetStringList("affectedMagicTags"),
            AffectedOrganizationIds = reader.GetStringList("affectedOrganizationIds"),
            CountryIds = reader.GetStringList("countryIds"),
            CityStateIds = reader.GetStringList("cityStateIds"),
            Severity = reader.GetString("severity", string.Empty),
            EnforcementLevel = reader.GetString("enforcementLevel", string.Empty),
            LawType = reader.GetString("lawType", string.Empty),
            RequiresLicense = reader.GetBool("requiresLicense", false)
        };
    }

    private static RestrictionDefinitionReadModel? ToRestrictionModel(UnifiedDefinitionDocument definition)
    {
        if (definition == null) return null;
        var reader = new DefinitionExtraDataReader(definition.ExtraData);
        return new RestrictionDefinitionReadModel
        {
            Id = definition.Id ?? string.Empty,
            AppliesToTags = reader.GetStringList("appliesToTags"),
            CountryIds = reader.GetStringList("countryIds"),
            CityStateIds = reader.GetStringList("cityStateIds"),
            RelatedLawIds = reader.GetStringList("relatedLawIds"),
            LicenseRequired = reader.GetBool("licenseRequired", false),
            GMApprovalRequired = reader.GetBool("gmApprovalRequired", false),
            RestrictionType = reader.GetString("restrictionType", string.Empty)
        };
    }

    private static MarketTagDefinitionReadModel? ToMarketTagModel(UnifiedDefinitionDocument definition)
    {
        if (definition == null) return null;
        var reader = new DefinitionExtraDataReader(definition.ExtraData);
        return new MarketTagDefinitionReadModel
        {
            Id = definition.Id ?? string.Empty,
            LegalityDefault = reader.GetString("legalityDefault", string.Empty),
            RelatedItemTags = reader.GetStringList("relatedItemTags"),
            RelatedRestrictionIds = reader.GetStringList("relatedRestrictionIds")
        };
    }

    private static void ApplyRestrictionSignals(InventoryItemLegalityResult result, IEnumerable<RestrictionDefinitionReadModel> restrictions, InventoryItemInstanceState item, bool strictMode)
    {
        foreach (var restriction in restrictions)
        {
            result.IsRestricted = true;
            AddWarning(result, "restricted_item_detected", $"Matched restriction '{restriction.Id}'.", item.ItemInstanceId, item.DefinitionId);
            if (restriction.LicenseRequired)
            {
                result.RequiresLicense = true;
                AddWarning(result, "license_required_detected", $"Matched restriction '{restriction.Id}' requires license.", item.ItemInstanceId, item.DefinitionId);
            }

            if (restriction.GMApprovalRequired)
            {
                result.RequiresGMApproval = true;
                AddWarning(result, "gm_approval_required_detected", $"Matched restriction '{restriction.Id}' requires GM approval.", item.ItemInstanceId, item.DefinitionId);
            }

            if (LooksForbidden(restriction.RestrictionType))
            {
                result.IsForbidden = true;
                AddByMode(result, strictMode, "forbidden_item_detected", $"Matched restriction '{restriction.Id}' looks forbidden.", item.ItemInstanceId, item.DefinitionId);
            }
        }
    }

    private static void ApplyLawSignals(InventoryItemLegalityResult result, IEnumerable<LawDefinitionReadModel> laws, InventoryItemInstanceState item, bool strictMode)
    {
        foreach (var law in laws)
        {
            if (law.RequiresLicense)
            {
                result.RequiresLicense = true;
                AddWarning(result, "license_required_detected", $"Matched law '{law.Id}' requires license.", item.ItemInstanceId, item.DefinitionId);
            }

            if (LooksForbidden(law.LawType) || LooksForbidden(law.Severity) || LooksForbidden(law.EnforcementLevel))
            {
                result.IsForbidden = true;
                AddByMode(result, strictMode, "forbidden_item_detected", $"Matched law '{law.Id}' looks forbidden.", item.ItemInstanceId, item.DefinitionId);
            }
        }
    }

    private static void ApplyMarketSignals(InventoryItemLegalityResult result, IEnumerable<MarketTagDefinitionReadModel> marketTags, InventoryItemInstanceState item)
    {
        foreach (var tag in marketTags)
        {
            if (LooksBlackMarket(tag.LegalityDefault))
            {
                result.IsBlackMarketRelevant = true;
                AddWarning(result, "black_market_relevant_item", $"Matched marketTag '{tag.Id}' indicates black/illegal/restricted market relevance.", item.ItemInstanceId, item.DefinitionId);
            }
        }
    }

    private static void CalculateStatus(InventoryItemLegalityResult result, bool contextMissing)
    {
        if (contextMissing)
        {
            result.LegalityStatus = InventoryLegalityStatusIds.ContextMissing;
            return;
        }

        if (result.IsForbidden) result.LegalityStatus = InventoryLegalityStatusIds.Forbidden;
        else if (result.RequiresGMApproval) result.LegalityStatus = InventoryLegalityStatusIds.GmApprovalRequired;
        else if (result.RequiresLicense) result.LegalityStatus = InventoryLegalityStatusIds.LicenseRequired;
        else if (result.IsBlackMarketRelevant) result.LegalityStatus = InventoryLegalityStatusIds.BlackMarketOnly;
        else if (result.IsRestricted) result.LegalityStatus = InventoryLegalityStatusIds.Restricted;
        else if (result.MatchedLawIds.Count == 0 && result.MatchedRestrictionIds.Count == 0 && result.MatchedMarketTagIds.Count == 0) result.LegalityStatus = InventoryLegalityStatusIds.Legal;
        else result.LegalityStatus = InventoryLegalityStatusIds.Unknown;

        if (result.LegalityStatus == InventoryLegalityStatusIds.Unknown)
        {
            AddWarning(result, "legality_unknown", "Legality status could not be resolved from matched metadata.", result.ItemInstanceId, result.DefinitionId);
        }
    }

    private static bool ContextApplies(List<string> countryIds, List<string> cityStateIds, InventoryLegalityContext context, InventoryItemLegalityResult result, string generalWarningCode)
    {
        var countries = countryIds ?? new List<string>();
        var cityStates = cityStateIds ?? new List<string>();
        if (countries.Count == 0 && cityStates.Count == 0)
        {
            AddWarning(result, generalWarningCode, "Law/restriction has no countryIds or cityStateIds and is treated as general/draft applicable.", result.ItemInstanceId, result.DefinitionId);
            return true;
        }

        var countryMatches = !string.IsNullOrWhiteSpace(context.CountryId) && countries.Contains(context.CountryId, StringComparer.OrdinalIgnoreCase);
        var cityMatches = !string.IsNullOrWhiteSpace(context.CityStateId) && cityStates.Contains(context.CityStateId, StringComparer.OrdinalIgnoreCase);
        return countryMatches || cityMatches;
    }

    private static IEnumerable<string> GetCategorySearchOrder(InventoryItemInstanceState item)
    {
        var hint = (item?.ItemType ?? string.Empty).Trim();
        if (string.Equals(hint, "weapon", StringComparison.OrdinalIgnoreCase)) return new[] { DefinitionCategoryIds.Weapon, DefinitionCategoryIds.Item, DefinitionCategoryIds.Armor, DefinitionCategoryIds.Ammo };
        if (string.Equals(hint, "armor", StringComparison.OrdinalIgnoreCase) || string.Equals(hint, "shield", StringComparison.OrdinalIgnoreCase)) return new[] { DefinitionCategoryIds.Armor, DefinitionCategoryIds.Item, DefinitionCategoryIds.Weapon, DefinitionCategoryIds.Ammo };
        if (string.Equals(hint, "ammo", StringComparison.OrdinalIgnoreCase) || string.Equals(hint, "ammunition", StringComparison.OrdinalIgnoreCase)) return new[] { DefinitionCategoryIds.Ammo, DefinitionCategoryIds.Item, DefinitionCategoryIds.Weapon, DefinitionCategoryIds.Armor };
        return new[] { DefinitionCategoryIds.Item, DefinitionCategoryIds.Weapon, DefinitionCategoryIds.Armor, DefinitionCategoryIds.Ammo };
    }

    private static bool RuntimeLookupEnabled()
    {
        return InventoryFeatureFlags.UseRuntimeLawRestrictionLookup;
    }

    private static bool LooksForbidden(string value)
    {
        return ContainsAny(new[] { value ?? string.Empty }, "forbidden", "ban", "banned", "death_penalty", "illegal");
    }

    private static bool LooksBlackMarket(string value)
    {
        return ContainsAny(new[] { value ?? string.Empty }, "black", "illegal", "restricted");
    }

    private static bool ContainsAny(IEnumerable<string> values, params string[] expected)
    {
        return (values ?? Enumerable.Empty<string>()).Any(value => expected.Any(x => value?.IndexOf(x, StringComparison.OrdinalIgnoreCase) >= 0));
    }

    private static bool Intersects(IEnumerable<string> left, IEnumerable<string> right)
    {
        var set = new HashSet<string>((left ?? Enumerable.Empty<string>()).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()), StringComparer.OrdinalIgnoreCase);
        return (right ?? Enumerable.Empty<string>()).Any(x => !string.IsNullOrWhiteSpace(x) && set.Contains(x.Trim()));
    }

    private static void AddTags(HashSet<string> target, IEnumerable<string> tags)
    {
        foreach (var tag in tags ?? Enumerable.Empty<string>()) AddTag(target, tag);
    }

    private static void AddTag(HashSet<string> target, string tag)
    {
        if (!string.IsNullOrWhiteSpace(tag)) target.Add(tag.Trim());
    }

    private static void AddUnique(List<string> target, string value)
    {
        if (!string.IsNullOrWhiteSpace(value) && !target.Contains(value, StringComparer.OrdinalIgnoreCase)) target.Add(value.Trim());
    }

    private static string FirstNonEmpty(params string[] values)
    {
        foreach (var value in values ?? Array.Empty<string>())
        {
            if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
        }

        return string.Empty;
    }

    private static void AddByMode(InventoryItemLegalityResult result, bool strictMode, string code, string message, string itemInstanceId, string definitionId)
    {
        if (strictMode) AddError(result, code, message, itemInstanceId, definitionId);
        else AddWarning(result, code, message, itemInstanceId, definitionId);
    }

    private static void AddWarnings(InventoryItemLegalityResult result, IEnumerable<string> warnings, string itemInstanceId, string definitionId)
    {
        foreach (var warning in warnings ?? Enumerable.Empty<string>())
        {
            AddWarning(result, warning, warning, itemInstanceId, definitionId);
        }
    }

    private static void AddError(InventoryItemLegalityResult result, string code, string message, string itemInstanceId, string definitionId)
    {
        result.Errors.Add(new InventoryValidationIssue
        {
            Code = code ?? string.Empty,
            Severity = "error",
            Message = message ?? string.Empty,
            ItemInstanceId = itemInstanceId ?? string.Empty,
            DefinitionId = definitionId ?? string.Empty
        });
        _ = message;
    }

    private static void AddWarning(InventoryItemLegalityResult result, string code, string message, string itemInstanceId, string definitionId)
    {
        result.Warnings.Add(new InventoryValidationIssue
        {
            Code = code ?? string.Empty,
            Severity = "warning",
            Message = message ?? string.Empty,
            ItemInstanceId = itemInstanceId ?? string.Empty,
            DefinitionId = definitionId ?? string.Empty
        });
        _ = message;
    }

    private sealed class ItemLegalityMetadata
    {
        public HashSet<string> Tags { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public bool IsRestricted { get; set; }
        public bool DefinitionMissing { get; set; }
        public List<string> Warnings { get; } = new List<string>();
    }

    private sealed class LawDefinitionReadModel
    {
        public string Id { get; set; } = string.Empty;
        public List<string> AffectedItemTags { get; set; } = new List<string>();
        public List<string> AffectedMagicTags { get; set; } = new List<string>();
        public List<string> AffectedOrganizationIds { get; set; } = new List<string>();
        public List<string> CountryIds { get; set; } = new List<string>();
        public List<string> CityStateIds { get; set; } = new List<string>();
        public string Severity { get; set; } = string.Empty;
        public string EnforcementLevel { get; set; } = string.Empty;
        public string LawType { get; set; } = string.Empty;
        public bool RequiresLicense { get; set; }
    }

    private sealed class RestrictionDefinitionReadModel
    {
        public string Id { get; set; } = string.Empty;
        public List<string> AppliesToTags { get; set; } = new List<string>();
        public List<string> CountryIds { get; set; } = new List<string>();
        public List<string> CityStateIds { get; set; } = new List<string>();
        public List<string> RelatedLawIds { get; set; } = new List<string>();
        public bool LicenseRequired { get; set; }
        public bool GMApprovalRequired { get; set; }
        public string RestrictionType { get; set; } = string.Empty;
    }

    private sealed class MarketTagDefinitionReadModel
    {
        public string Id { get; set; } = string.Empty;
        public string LegalityDefault { get; set; } = string.Empty;
        public List<string> RelatedItemTags { get; set; } = new List<string>();
        public List<string> RelatedRestrictionIds { get; set; } = new List<string>();
    }
}
