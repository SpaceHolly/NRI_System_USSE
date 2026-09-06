using System;
using System.Collections.Generic;

namespace Nri.Shared.Domain;

public static class FactionOrganizationEconomyDefinitionCategories
{
    public const string Faction = "faction_definition";
    public const string Organization = "organization_definition";
    public const string Jurisdiction = "jurisdiction_definition";
    public const string Law = "law_definition";
    public const string License = "license_definition";
    public const string Currency = "currency_definition";
    public const string Market = "market_definition";
    public const string BusinessProfile = "business_profile_definition";
    public const string ControlLevel = "control_level_option_definition";
    public const string EconomicScale = "economic_scale_option_definition";
    public const string MarketOfferKind = "market_offer_kind_option_definition";

    public static readonly string[] Core =
    {
        Faction,
        Organization,
        Jurisdiction,
        Law,
        License,
        Currency,
        Market,
        BusinessProfile
    };

    public static readonly string[] RuleSetOptions =
    {
        ControlLevel,
        EconomicScale,
        MarketOfferKind
    };

    public static readonly string[] All =
    {
        Faction,
        Organization,
        Jurisdiction,
        Law,
        License,
        Currency,
        Market,
        BusinessProfile,
        ControlLevel,
        EconomicScale,
        MarketOfferKind
    };

    public static bool IsSupported(string value)
        => Array.Exists(All, x => string.Equals(x, value, StringComparison.OrdinalIgnoreCase));
}

public abstract class FactionOrganizationEconomyDefinitionProfile
{
    public string DefinitionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = RuleSetIds.FantasyNriDefault;
    public string PublicDescription { get; set; } = string.Empty;
    public string GMDescription { get; set; } = string.Empty;
    public string VisibilityRule { get; set; } = VisibilityRuleIds.Public;
    public List<string> Tags { get; set; } = new();
    public bool IsArchived { get; set; }
    public int Revision { get; set; } = 1;
}

public sealed class FactionDefinitionProfile : FactionOrganizationEconomyDefinitionProfile
{
    public string Category { get; set; } = "custom";
    public string ParentFactionDefinitionId { get; set; } = string.Empty;
    public List<string> RelatedFactionDefinitionIds { get; set; } = new();
    public List<string> AlliedFactionDefinitionIds { get; set; } = new();
    public List<string> RivalFactionDefinitionIds { get; set; } = new();
    public string PublicIdentity { get; set; } = string.Empty;
    public string PublicGoals { get; set; } = string.Empty;
    public string HiddenGoals { get; set; } = string.Empty;
    public List<string> IdeologyTags { get; set; } = new();
    public List<string> HomeLocationDefinitionIds { get; set; } = new();
    public List<string> ClaimedLocationDefinitionIds { get; set; } = new();
    public List<string> LanguageDefinitionIds { get; set; } = new();
    public List<string> JurisdictionDefinitionIds { get; set; } = new();
    public List<string> CurrencyDefinitionIds { get; set; } = new();
    public List<string> OrganizationDefinitionIds { get; set; } = new();
    public List<FactionRelationshipLabelDefinition0186> DefaultRelationshipLabels { get; set; } = new();
}

public sealed class FactionRelationshipLabelDefinition0186
{
    public string Key { get; set; } = string.Empty;
    public string PublicLabel { get; set; } = string.Empty;
    public string GMDescription { get; set; } = string.Empty;
}

public sealed class OrganizationDefinitionProfile : FactionOrganizationEconomyDefinitionProfile
{
    public string OrganizationKind { get; set; } = "custom";
    public string ParentOrganizationDefinitionId { get; set; } = string.Empty;
    public List<string> ControllingFactionDefinitionIds { get; set; } = new();
    public string PublicImage { get; set; } = string.Empty;
    public string LegalStatus { get; set; } = string.Empty;
    public string DeclaredActivity { get; set; } = string.Empty;
    public string ActualActivity { get; set; } = string.Empty;
    public string HiddenActivity { get; set; } = string.Empty;
    public List<string> HeadquartersLocationDefinitionIds { get; set; } = new();
    public List<string> OperatingLocationDefinitionIds { get; set; } = new();
    public string BusinessProfileDefinitionId { get; set; } = string.Empty;
    public List<string> CurrencyDefinitionIds { get; set; } = new();
    public List<string> MarketDefinitionIds { get; set; } = new();
    public List<string> LicenseDefinitionIds { get; set; } = new();
    public List<string> DefaultPersonnelRoles { get; set; } = new();
    public List<string> SupplierCustomerTags { get; set; } = new();
    public bool AllowIndependentOrganization { get; set; }
}

public sealed class JurisdictionDefinitionProfile : FactionOrganizationEconomyDefinitionProfile
{
    public string JurisdictionKind { get; set; } = "custom";
    public string GoverningFactionDefinitionId { get; set; } = string.Empty;
    public string GoverningOrganizationDefinitionId { get; set; } = string.Empty;
    public List<string> LocationDefinitionIds { get; set; } = new();
    public string ParentJurisdictionDefinitionId { get; set; } = string.Empty;
    public List<string> DefaultLawDefinitionIds { get; set; } = new();
    public string DefaultControlLevelDefinitionId { get; set; } = string.Empty;
    public List<string> RecognizedLicenseDefinitionIds { get; set; } = new();
    public List<string> CurrencyDefinitionIds { get; set; } = new();
    public string EnforcementLevel { get; set; } = string.Empty;
    public string AppealExceptionMetadata { get; set; } = string.Empty;
}

public sealed class LawDefinitionProfile : FactionOrganizationEconomyDefinitionProfile
{
    public string Category { get; set; } = "custom";
    public List<string> JurisdictionDefinitionIds { get; set; } = new();
    public List<string> ApplicableDefinitionCategories { get; set; } = new();
    public List<LawActionRuleDefinition0186> ActionRules { get; set; } = new();
    public string DefaultControlLevelDefinitionId { get; set; } = string.Empty;
    public List<string> RequiredLicenseDefinitionIds { get; set; } = new();
    public List<string> ExemptionTags { get; set; } = new();
    public List<string> ProhibitedTags { get; set; } = new();
    public List<string> RestrictedTags { get; set; } = new();
    public bool IsMilitary { get; set; }
    public bool IsStrategic { get; set; }
    public string PublicConsequence { get; set; } = string.Empty;
    public string GMConsequence { get; set; } = string.Empty;
    public string EnforcementGuidance { get; set; } = string.Empty;
}

public sealed class LawActionRuleDefinition0186
{
    public string ActionKind { get; set; } = "own";
    public string ControlLevelDefinitionId { get; set; } = string.Empty;
    public List<string> LicenseDefinitionIds { get; set; } = new();
    public List<string> AllowedSubjectTags { get; set; } = new();
    public List<string> RestrictedSubjectTags { get; set; } = new();
    public List<string> AllowedLocationTags { get; set; } = new();
    public List<string> RestrictedLocationTags { get; set; } = new();
    public string Result { get; set; } = "allowed";
    public string PublicWarning { get; set; } = string.Empty;
    public string GMNotes { get; set; } = string.Empty;
}

public sealed class LicenseDefinitionProfile : FactionOrganizationEconomyDefinitionProfile
{
    public string LicenseKind { get; set; } = "custom";
    public string IssuerFactionDefinitionId { get; set; } = string.Empty;
    public string IssuerOrganizationDefinitionId { get; set; } = string.Empty;
    public string IssuerJurisdictionDefinitionId { get; set; } = string.Empty;
    public List<string> LawDefinitionIds { get; set; } = new();
    public List<string> CoveredActions { get; set; } = new();
    public List<string> CoveredCategoryTags { get; set; } = new();
    public List<string> PrerequisiteLicenseDefinitionIds { get; set; } = new();
    public string FeeValueMetadata { get; set; } = string.Empty;
    public string ValidityModel { get; set; } = string.Empty;
    public string RenewalRules { get; set; } = string.Empty;
    public bool IsTransferable { get; set; }
    public bool IsRevocable { get; set; } = true;
    public string PublicRequirements { get; set; } = string.Empty;
    public string HiddenRequirements { get; set; } = string.Empty;
}

public sealed class CurrencyDefinitionProfile : FactionOrganizationEconomyDefinitionProfile
{
    public string Symbol { get; set; } = string.Empty;
    public string IssuerReference { get; set; } = string.Empty;
    public string CurrencyKind { get; set; } = "physical";
    public int DecimalPrecision { get; set; }
    public List<CurrencyDenominationDefinition0186> Denominations { get; set; } = new();
    public List<string> JurisdictionDefinitionIds { get; set; } = new();
    public List<string> MarketDefinitionIds { get; set; } = new();
    public string Legality { get; set; } = string.Empty;
    public List<string> RarityStabilityTags { get; set; } = new();
}

public sealed class CurrencyDenominationDefinition0186
{
    public string Name { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public decimal Multiplier { get; set; } = 1;
}

public sealed class MarketDefinitionProfile : FactionOrganizationEconomyDefinitionProfile
{
    public string MarketKind { get; set; } = "custom";
    public List<string> LocationDefinitionIds { get; set; } = new();
    public List<string> JurisdictionDefinitionIds { get; set; } = new();
    public List<string> FactionDefinitionIds { get; set; } = new();
    public List<string> OrganizationDefinitionIds { get; set; } = new();
    public List<string> CurrencyDefinitionIds { get; set; } = new();
    public List<string> OfferKindDefinitionIds { get; set; } = new();
    public List<string> AllowedDefinitionCategories { get; set; } = new();
    public List<string> RestrictedCategories { get; set; } = new();
    public List<string> ProhibitedCategories { get; set; } = new();
    public string DefaultLegalPolicy { get; set; } = string.Empty;
    public string PriceBandModifiers { get; set; } = string.Empty;
    public string AvailabilityRarityBands { get; set; } = string.Empty;
    public string ScheduleAccessRequirements { get; set; } = string.Empty;
    public string DefaultRiskSummary { get; set; } = string.Empty;
    public string PersonnelLivingOfferPolicy { get; set; } = string.Empty;
    public string LargeAssetPolicy { get; set; } = string.Empty;
}

public sealed class BusinessProfileDefinitionProfile : FactionOrganizationEconomyDefinitionProfile
{
    public string BusinessKind { get; set; } = "custom";
    public string EconomicScaleDefinitionId { get; set; } = string.Empty;
    public List<string> TypicalDeclaredActivities { get; set; } = new();
    public List<string> PossibleActualActivities { get; set; } = new();
    public List<string> RequiredLocationDefinitionIds { get; set; } = new();
    public List<string> RequiredFacilityCategories { get; set; } = new();
    public string PersonnelRequirements { get; set; } = string.Empty;
    public string ResourceRequirements { get; set; } = string.Empty;
    public EconomicBandDefinition0186 IncomeBand { get; set; } = new();
    public EconomicBandDefinition0186 ExpenseBand { get; set; } = new();
    public EconomicBandDefinition0186 TaxRentBand { get; set; } = new();
    public string SecurityRequirements { get; set; } = string.Empty;
    public string MaintenanceRequirements { get; set; } = string.Empty;
    public List<string> LicenseDefinitionIds { get; set; } = new();
    public List<string> SupplierCustomerCategories { get; set; } = new();
    public List<string> RiskTags { get; set; } = new();
}

public sealed class EconomicBandDefinition0186
{
    public decimal Minimum { get; set; }
    public decimal Maximum { get; set; }
    public string CurrencyDefinitionId { get; set; } = string.Empty;
    public string Period { get; set; } = string.Empty;
}

public abstract class OrderedRuleSetOptionDefinitionProfile0186 : FactionOrganizationEconomyDefinitionProfile
{
    public int Order { get; set; }
    public string PlayerLabel { get; set; } = string.Empty;
}

public sealed class ControlLevelDefinitionProfile0186 : OrderedRuleSetOptionDefinitionProfile0186
{
    public int Rank { get; set; }
}

public sealed class EconomicScaleDefinitionProfile0186 : OrderedRuleSetOptionDefinitionProfile0186
{
    public int Rank { get; set; }
}

public sealed class MarketOfferKindDefinitionProfile0186 : OrderedRuleSetOptionDefinitionProfile0186
{
    public string OfferCategory { get; set; } = "custom";
}
