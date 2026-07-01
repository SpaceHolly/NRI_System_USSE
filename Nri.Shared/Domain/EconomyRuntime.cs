using System;
using System.Collections.Generic;

namespace Nri.Shared.Domain;

public static class EconomyFeatureFlags
{
    public const bool UseEconomySystemV1 = false;
    public const bool UseFactionSystemV1 = false;
    public const bool UseOrganizationSystemV1 = false;
    public const bool UseMarketSystemV1 = false;
    public const bool UseLawRestrictionSystemV1 = false;
    public const bool UseAssetHoldingSystemV1 = false;
    public const bool UseEconomyRuntimeSeed = false;
    public const bool UseEconomyRuntimeSeedDryRun = false;
    public const bool UseEconomyRuntimeSeedWrite = false;
    public const bool UseEconomyRuntimeReadEndpoints = false;
    public const bool UseEconomyRuntimeRelationRead = false;
    public const bool UseHoldingsAssetReadBridge = false;
}

public static class EconomyRuntimeKinds
{
    public const string Faction = "faction";
    public const string Organization = "organization";
    public const string Market = "market";
    public const string Law = "law";
    public const string Restriction = "restriction";
    public const string Asset = "asset";
    public const string EconomyScope = "economy_scope";
}

public static class FactionRelationTypes
{
    public const string Allied = "allied";
    public const string Friendly = "friendly";
    public const string Neutral = "neutral";
    public const string Tense = "tense";
    public const string Hostile = "hostile";
    public const string War = "war";
    public const string Hidden = "hidden";
}

public static class EconomyAccessLevels
{
    public const string Public = "public";
    public const string Restricted = "restricted";
    public const string Licensed = "licensed";
    public const string MilitaryOnly = "military_only";
    public const string GmOnly = "gm_only";
    public const string HiddenUntilDiscovered = "hidden_until_discovered";
}

public sealed class FactionState : EntityBase
{
    public string DefinitionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string CountryId { get; set; } = string.Empty;
    public string CityStateId { get; set; } = string.Empty;
    public List<string> LocationIds { get; set; } = new List<string>();
    public string PublicAlignment { get; set; } = string.Empty;
    public string SecrecyLevel { get; set; } = string.Empty;
    public int InfluenceLevel { get; set; }
    public int MilitaryInfluence { get; set; }
    public int EconomicInfluence { get; set; }
    public int PoliticalInfluence { get; set; }
    public int MagicInfluence { get; set; }
    public string ReputationTargetId { get; set; } = string.Empty;
    public List<FactionRelationState> RelationStates { get; set; } = new List<FactionRelationState>();
    public List<string> Tags { get; set; } = new List<string>();
    public string Notes { get; set; } = string.Empty;
}

public sealed class FactionRelationState
{
    public string TargetFactionId { get; set; } = string.Empty;
    public int RelationValue { get; set; }
    public string RelationType { get; set; } = string.Empty;
    public bool IsPublic { get; set; }
    public string Notes { get; set; } = string.Empty;
}

public sealed class OrganizationState : EntityBase
{
    public string DefinitionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string ParentFactionId { get; set; } = string.Empty;
    public string CountryId { get; set; } = string.Empty;
    public string CityStateId { get; set; } = string.Empty;
    public List<string> LocationIds { get; set; } = new List<string>();
    public string PublicStatus { get; set; } = string.Empty;
    public string LegalStatus { get; set; } = string.Empty;
    public string AccessLevel { get; set; } = string.Empty;
    public string SecrecyLevel { get; set; } = string.Empty;
    public List<string> ServiceTags { get; set; } = new List<string>();
    public List<string> ResourceTags { get; set; } = new List<string>();
    public List<string> RecruitmentTags { get; set; } = new List<string>();
    public string ReputationTargetId { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new List<string>();
    public string Notes { get; set; } = string.Empty;
}

public sealed class MarketState : EntityBase
{
    public string DefinitionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string CountryId { get; set; } = string.Empty;
    public string CityStateId { get; set; } = string.Empty;
    public string LocationId { get; set; } = string.Empty;
    public List<string> MarketTagIds { get; set; } = new List<string>();
    public List<string> LegalTagIds { get; set; } = new List<string>();
    public List<string> RestrictedTagIds { get; set; } = new List<string>();
    public List<string> AvailableCurrencyIds { get; set; } = new List<string>();
    public string AvailabilityProfile { get; set; } = string.Empty;
    public string PricePolicy { get; set; } = string.Empty;
    public bool IsBlackMarket { get; set; }
    public bool IsActive { get; set; } = true;
    public List<string> Tags { get; set; } = new List<string>();
    public string Notes { get; set; } = string.Empty;
}

public sealed class LawState : EntityBase
{
    public string DefinitionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public List<string> CountryIds { get; set; } = new List<string>();
    public List<string> CityStateIds { get; set; } = new List<string>();
    public string LawType { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string EnforcementLevel { get; set; } = string.Empty;
    public List<string> RelatedRestrictionIds { get; set; } = new List<string>();
    public bool IsActive { get; set; } = true;
    public bool IsPubliclyKnown { get; set; } = true;
    public List<string> Tags { get; set; } = new List<string>();
    public string Notes { get; set; } = string.Empty;
}

public sealed class RestrictionState : EntityBase
{
    public string DefinitionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string RestrictionType { get; set; } = string.Empty;
    public List<string> AppliesToTags { get; set; } = new List<string>();
    public List<string> CountryIds { get; set; } = new List<string>();
    public List<string> CityStateIds { get; set; } = new List<string>();
    public List<string> RelatedLawIds { get; set; } = new List<string>();
    public bool LicenseRequired { get; set; }
    public bool GMApprovalRequired { get; set; }
    public bool IsActive { get; set; } = true;
    public List<string> Tags { get; set; } = new List<string>();
    public string Notes { get; set; } = string.Empty;
}

public sealed class AssetState : EntityBase
{
    public string DefinitionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string AssetType { get; set; } = string.Empty;
    public string CountryId { get; set; } = string.Empty;
    public string CityStateId { get; set; } = string.Empty;
    public string LocationId { get; set; } = string.Empty;
    public List<string> OwnerCharacterIds { get; set; } = new List<string>();
    public List<string> OwnerOrganizationIds { get; set; } = new List<string>();
    public List<string> OwnerFactionIds { get; set; } = new List<string>();
    public string LegalStatus { get; set; } = string.Empty;
    public string ActualStatus { get; set; } = string.Empty;
    public List<string> MarketTagIds { get; set; } = new List<string>();
    public string EstimatedValueCurrencyId { get; set; } = string.Empty;
    public long EstimatedValueAmount { get; set; }
    public bool IsActive { get; set; } = true;
    public List<string> Tags { get; set; } = new List<string>();
    public string Notes { get; set; } = string.Empty;
}

public sealed class EconomyScopeState : EntityBase
{
    public string RuleSetId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string ScopeType { get; set; } = string.Empty;
    public string CountryId { get; set; } = string.Empty;
    public string CityStateId { get; set; } = string.Empty;
    public string RegionId { get; set; } = string.Empty;
    public List<string> CurrencyIds { get; set; } = new List<string>();
    public List<string> MarketIds { get; set; } = new List<string>();
    public List<string> ActiveLawIds { get; set; } = new List<string>();
    public List<string> ActiveRestrictionIds { get; set; } = new List<string>();
    public string Notes { get; set; } = string.Empty;
}

public sealed class EconomyRuntimeValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new List<string>();
    public List<string> Warnings { get; set; } = new List<string>();
}

public static class EconomyRuntimeValidator
{
    public static EconomyRuntimeValidationResult ValidateFactionState(FactionState state)
    {
        var result = CreateResult();
        ValidateEntity(state, result, EconomyRuntimeKinds.Faction);
        ValidateDefinitionOrName(state?.DefinitionId, state?.Name, result, EconomyRuntimeKinds.Faction);
        ValidateRuleSetAndCampaign(state?.RuleSetId, state?.CampaignId, result, EconomyRuntimeKinds.Faction);
        ValidateCollection(state?.LocationIds, "faction.locationIds", result);
        ValidateCollection(state?.RelationStates, "faction.relationStates", result);
        ValidateCollection(state?.Tags, "faction.tags", result);
        Finish(result);
        return result;
    }

    public static EconomyRuntimeValidationResult ValidateOrganizationState(OrganizationState state)
    {
        var result = CreateResult();
        ValidateEntity(state, result, EconomyRuntimeKinds.Organization);
        ValidateDefinitionOrName(state?.DefinitionId, state?.Name, result, EconomyRuntimeKinds.Organization);
        ValidateRuleSetAndCampaign(state?.RuleSetId, state?.CampaignId, result, EconomyRuntimeKinds.Organization);
        ValidateCollection(state?.LocationIds, "organization.locationIds", result);
        ValidateCollection(state?.ServiceTags, "organization.serviceTags", result);
        ValidateCollection(state?.ResourceTags, "organization.resourceTags", result);
        ValidateCollection(state?.RecruitmentTags, "organization.recruitmentTags", result);
        ValidateCollection(state?.Tags, "organization.tags", result);
        Finish(result);
        return result;
    }

    public static EconomyRuntimeValidationResult ValidateMarketState(MarketState state)
    {
        var result = CreateResult();
        ValidateEntity(state, result, EconomyRuntimeKinds.Market);
        ValidateDefinitionOrName(state?.DefinitionId, state?.Name, result, EconomyRuntimeKinds.Market);
        ValidateRuleSetAndCampaign(state?.RuleSetId, state?.CampaignId, result, EconomyRuntimeKinds.Market);
        ValidateCollection(state?.MarketTagIds, "market.marketTagIds", result);
        ValidateCollection(state?.LegalTagIds, "market.legalTagIds", result);
        ValidateCollection(state?.RestrictedTagIds, "market.restrictedTagIds", result);
        ValidateCollection(state?.AvailableCurrencyIds, "market.availableCurrencyIds", result);
        ValidateCollection(state?.Tags, "market.tags", result);
        Finish(result);
        return result;
    }

    public static EconomyRuntimeValidationResult ValidateLawState(LawState state)
    {
        var result = CreateResult();
        ValidateEntity(state, result, EconomyRuntimeKinds.Law);
        ValidateDefinitionOrName(state?.DefinitionId, state?.Name, result, EconomyRuntimeKinds.Law);
        ValidateRuleSetAndCampaign(state?.RuleSetId, state?.CampaignId, result, EconomyRuntimeKinds.Law);
        ValidateCollection(state?.CountryIds, "law.countryIds", result);
        ValidateCollection(state?.CityStateIds, "law.cityStateIds", result);
        ValidateCollection(state?.RelatedRestrictionIds, "law.relatedRestrictionIds", result);
        ValidateCollection(state?.Tags, "law.tags", result);
        Finish(result);
        return result;
    }

    public static EconomyRuntimeValidationResult ValidateRestrictionState(RestrictionState state)
    {
        var result = CreateResult();
        ValidateEntity(state, result, EconomyRuntimeKinds.Restriction);
        ValidateDefinitionOrName(state?.DefinitionId, state?.Name, result, EconomyRuntimeKinds.Restriction);
        ValidateRuleSetAndCampaign(state?.RuleSetId, state?.CampaignId, result, EconomyRuntimeKinds.Restriction);
        ValidateCollection(state?.AppliesToTags, "restriction.appliesToTags", result);
        ValidateCollection(state?.CountryIds, "restriction.countryIds", result);
        ValidateCollection(state?.CityStateIds, "restriction.cityStateIds", result);
        ValidateCollection(state?.RelatedLawIds, "restriction.relatedLawIds", result);
        ValidateCollection(state?.Tags, "restriction.tags", result);
        Finish(result);
        return result;
    }

    public static EconomyRuntimeValidationResult ValidateAssetState(AssetState state)
    {
        var result = CreateResult();
        ValidateEntity(state, result, EconomyRuntimeKinds.Asset);
        ValidateDefinitionOrName(state?.DefinitionId, state?.Name, result, EconomyRuntimeKinds.Asset);
        ValidateRuleSetAndCampaign(state?.RuleSetId, state?.CampaignId, result, EconomyRuntimeKinds.Asset);
        ValidateCollection(state?.OwnerCharacterIds, "asset.ownerCharacterIds", result);
        ValidateCollection(state?.OwnerOrganizationIds, "asset.ownerOrganizationIds", result);
        ValidateCollection(state?.OwnerFactionIds, "asset.ownerFactionIds", result);
        ValidateCollection(state?.MarketTagIds, "asset.marketTagIds", result);
        ValidateCollection(state?.Tags, "asset.tags", result);
        if (state != null && state.EstimatedValueAmount < 0) result.Errors.Add("asset.estimatedValueAmount_negative");
        Finish(result);
        return result;
    }

    public static EconomyRuntimeValidationResult ValidateEconomyScopeState(EconomyScopeState state)
    {
        var result = CreateResult();
        ValidateEntity(state, result, EconomyRuntimeKinds.EconomyScope);
        ValidateRuleSetAndCampaign(state?.RuleSetId, state?.CampaignId, result, EconomyRuntimeKinds.EconomyScope);
        ValidateCollection(state?.CurrencyIds, "economyScope.currencyIds", result);
        ValidateCollection(state?.MarketIds, "economyScope.marketIds", result);
        ValidateCollection(state?.ActiveLawIds, "economyScope.activeLawIds", result);
        ValidateCollection(state?.ActiveRestrictionIds, "economyScope.activeRestrictionIds", result);
        Finish(result);
        return result;
    }

    private static EconomyRuntimeValidationResult CreateResult() => new EconomyRuntimeValidationResult { IsValid = true };

    private static void ValidateEntity(EntityBase? state, EconomyRuntimeValidationResult result, string kind)
    {
        if (state == null)
        {
            result.Errors.Add($"{kind}.state_null");
            return;
        }

        if (string.IsNullOrWhiteSpace(state.Id)) result.Errors.Add($"{kind}.id_required");
        if (state.SchemaVersion < 1) result.Errors.Add($"{kind}.schema_version_invalid");
    }

    private static void ValidateDefinitionOrName(string? definitionId, string? name, EconomyRuntimeValidationResult result, string kind)
    {
        if (string.IsNullOrWhiteSpace(definitionId) && string.IsNullOrWhiteSpace(name))
        {
            result.Warnings.Add($"{kind}.definition_or_name_recommended");
        }
    }

    private static void ValidateRuleSetAndCampaign(string? ruleSetId, string? campaignId, EconomyRuntimeValidationResult result, string kind)
    {
        if (string.IsNullOrWhiteSpace(ruleSetId)) result.Errors.Add($"{kind}.ruleset_required");
        if (string.IsNullOrWhiteSpace(campaignId)) result.Warnings.Add($"{kind}.campaign_missing_skeleton_allowed");
    }

    private static void ValidateCollection<T>(ICollection<T>? values, string field, EconomyRuntimeValidationResult result)
    {
        if (values == null) result.Errors.Add($"{field}_null");
    }

    private static void Finish(EconomyRuntimeValidationResult result)
    {
        result.IsValid = result.Errors.Count == 0;
    }
}
