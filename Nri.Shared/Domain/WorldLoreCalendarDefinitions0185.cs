using System;
using System.Collections.Generic;

namespace Nri.Shared.Domain;

public static class WorldLoreCalendarDefinitionCategories
{
    public const string World = "world_definition";
    public const string Location = "location_definition";
    public const string Language = "language_definition";
    public const string LanguageScript = "language_script_definition";
    public const string LanguageFamily = "language_family_definition";
    public const string LanguageOriginTradition = "language_origin_tradition_definition";
    public const string KnowledgeType = "knowledge_type_definition";
    public const string LoreEntry = "lore_entry_definition";
    public const string Calendar = "calendar_definition";
    public const string Era = "era_definition";
    public const string EventType = "event_type_definition";

    public static readonly string[] All =
    {
        World,
        Location,
        Language,
        LanguageScript,
        LanguageFamily,
        LanguageOriginTradition,
        KnowledgeType,
        LoreEntry,
        Calendar,
        Era,
        EventType
    };

    public static bool IsSupported(string value)
        => Array.Exists(All, x => string.Equals(x, value, StringComparison.OrdinalIgnoreCase));
}

public static class LocationKindIds0185
{
    public const string World = "world";
    public const string Continent = "continent";
    public const string Region = "region";
    public const string State = "state";
    public const string Settlement = "settlement";
    public const string District = "district";
    public const string Location = "location";
    public const string SubLocation = "sub_location";
    public const string Custom = "custom";
}

public abstract class WorldLoreCalendarDefinitionProfile
{
    public string DefinitionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = RuleSetIds.FantasyNriDefault;
    public string PublicDescription { get; set; } = string.Empty;
    public string GMDescription { get; set; } = string.Empty;
    public string VisibilityRule { get; set; } = VisibilityRuleIds.Public;
    public List<string> Tags { get; set; } = new List<string>();
    public bool IsArchived { get; set; }
    public int Revision { get; set; } = 1;
}

public sealed class WorldDefinitionProfile : WorldLoreCalendarDefinitionProfile
{
    public List<string> RuleSetIds { get; set; } = new List<string>();
    public string DefaultCalendarDefinitionId { get; set; } = string.Empty;
    public string DefaultEraDefinitionId { get; set; } = string.Empty;
    public List<string> TopLevelLocationDefinitionIds { get; set; } = new List<string>();
    public List<string> DefaultLanguageDefinitionIds { get; set; } = new List<string>();
    public List<string> Themes { get; set; } = new List<string>();
}

public sealed class LocationDefinitionProfile : WorldLoreCalendarDefinitionProfile
{
    public string LocationKind { get; set; } = LocationKindIds0185.Location;
    public string ParentLocationDefinitionId { get; set; } = string.Empty;
    public string WorldDefinitionId { get; set; } = string.Empty;
    public List<string> CultureReferences { get; set; } = new List<string>();
    public List<string> LanguageDefinitionIds { get; set; } = new List<string>();
    public List<string> ClimateTerrainTags { get; set; } = new List<string>();
    public string TravelMetadata { get; set; } = string.Empty;
    public string RelatedMapReference { get; set; } = string.Empty;
    public List<string> KnownConnectionDefinitionIds { get; set; } = new List<string>();
    public List<string> HiddenConnectionDefinitionIds { get; set; } = new List<string>();
    public string JurisdictionReference { get; set; } = string.Empty;
    public bool AllowCustomHierarchy { get; set; }
}

public sealed class LanguageDefinitionProfile : WorldLoreCalendarDefinitionProfile
{
    public string LanguageFamily { get; set; } = string.Empty;
    public string LanguageFamilyDefinitionId { get; set; } = string.Empty;
    public string PrimaryScriptDefinitionId { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = new List<string>();
    public List<LanguageWritingSystemDefinition0185> WritingSystems { get; set; } = new List<LanguageWritingSystemDefinition0185>();
    public List<string> RegionLocationDefinitionIds { get; set; } = new List<string>();
    public List<string> Cultures { get; set; } = new List<string>();
    public List<string> StateOrganizationDefinitionIds { get; set; } = new List<string>();
    public List<string> AncestorLanguageDefinitionIds { get; set; } = new List<string>();
    public List<string> ContactInfluenceLanguageDefinitionIds { get; set; } = new List<string>();
    public List<string> ProficiencyAspects { get; set; } = new List<string>();
    public List<string> ProficiencyLevelDescriptions { get; set; } = new List<string>();
    public string ProfessionalTerminology { get; set; } = string.Empty;
    public string RitualMagicalApplication { get; set; } = string.Empty;
    public string TranslationRules { get; set; } = string.Empty;
    public string UsageLimitations { get; set; } = string.Empty;
    public string CostClass { get; set; } = LanguageCostClassIds.Modern;
    public List<string> RelatedLanguageDefinitionIds { get; set; } = new List<string>();
}

public sealed class LanguageScriptDefinitionProfile : WorldLoreCalendarDefinitionProfile
{
    public string MainUse { get; set; } = string.Empty;
    public string WritingDirection { get; set; } = "left_to_right";
}

public sealed class LanguageFamilyDefinitionProfile : WorldLoreCalendarDefinitionProfile
{
    public string ParentFamilyDefinitionId { get; set; } = string.Empty;
}

public sealed class LanguageOriginTraditionDefinitionProfile : WorldLoreCalendarDefinitionProfile
{
    public string LanguageDefinitionId { get; set; } = string.Empty;
    public List<string> CultureStateReligionReferences { get; set; } = new List<string>();
    public string ClaimedOriginType { get; set; } = LanguageClaimedOriginTypeIds.Unknown;
    public string ClaimedGiverName { get; set; } = string.Empty;
}

public static class LanguageCostClassIds
{
    public const string Modern = "modern";
    public const string Religious = "religious";
    public const string Ancient = "ancient";
}

public static class LanguageClaimedOriginTypeIds
{
    public const string Deity = "deity";
    public const string Hero = "hero";
    public const string Dragon = "dragon";
    public const string Titan = "titan";
    public const string Leviathan = "leviathan";
    public const string Spirit = "spirit";
    public const string MythicCreature = "mythic_creature";
    public const string Unknown = "unknown";
    public const string Other = "other";
}

public sealed class LanguageWritingSystemDefinition0185
{
    public string Name { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
}

public sealed class KnowledgeTypeDefinitionProfile : WorldLoreCalendarDefinitionProfile
{
    public int Order { get; set; }
    public decimal ReliabilityMinimum { get; set; }
    public decimal ReliabilityMaximum { get; set; } = 100;
    public bool AllowsPracticalUse { get; set; }
    public bool AllowsIdentification { get; set; }
    public bool AllowsDetails { get; set; }
    public string PlayerVisibleLabel { get; set; } = string.Empty;
}

public sealed class LoreEntryDefinitionProfile : WorldLoreCalendarDefinitionProfile
{
    public string LoreKind { get; set; } = "lore";
    public string SubjectType { get; set; } = "custom";
    public string SubjectDefinitionId { get; set; } = string.Empty;
    public List<string> SourceReferences { get; set; } = new List<string>();
    public List<string> LocationDefinitionIds { get; set; } = new List<string>();
    public List<string> LanguageDefinitionIds { get; set; } = new List<string>();
    public List<string> EraDefinitionIds { get; set; } = new List<string>();
    public List<string> EventTypeDefinitionIds { get; set; } = new List<string>();
    public List<LoreInformationVersion0185> InformationVersions { get; set; } = new List<LoreInformationVersion0185>();
}

public sealed class LoreInformationVersion0185
{
    public string VersionKind { get; set; } = "official";
    public string KnowledgeTypeDefinitionId { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public decimal Reliability { get; set; }
    public string SourceAttribution { get; set; } = string.Empty;
    public string ValidFromWorldDate { get; set; } = string.Empty;
    public string ValidToWorldDate { get; set; } = string.Empty;
    public bool IsOutdated { get; set; }
    public bool IsPlayerVisibleEligible { get; set; }
    public List<string> ContextReferences { get; set; } = new List<string>();
}

public sealed class CalendarDefinitionProfile : WorldLoreCalendarDefinitionProfile
{
    public string YearNumberingModel { get; set; } = "era_based";
    public int DaysPerWeek { get; set; } = 7;
    public List<CalendarWeekdayDefinition0185> Weekdays { get; set; } = new List<CalendarWeekdayDefinition0185>();
    public List<CalendarMonthDefinition0185> Months { get; set; } = new List<CalendarMonthDefinition0185>();
    public List<CalendarSeasonDefinition0185> Seasons { get; set; } = new List<CalendarSeasonDefinition0185>();
    public List<CalendarSpecialDayDefinition0185> SpecialDays { get; set; } = new List<CalendarSpecialDayDefinition0185>();
    public int DeclaredDaysPerYear { get; set; }
    public string DefaultEraDefinitionId { get; set; } = string.Empty;
    public string DateDisplayFormat { get; set; } = "dd MMMM yyyy";
}

public sealed class CalendarWeekdayDefinition0185
{
    public int Order { get; set; }
    public string Name { get; set; } = string.Empty;
}

public sealed class CalendarMonthDefinition0185
{
    public int Order { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Days { get; set; }
    public string SeasonLabel { get; set; } = string.Empty;
}

public sealed class CalendarSeasonDefinition0185
{
    public int Order { get; set; }
    public string Name { get; set; } = string.Empty;
    public int StartDayOfYear { get; set; }
    public int EndDayOfYear { get; set; }
}

public sealed class CalendarSpecialDayDefinition0185
{
    public string Name { get; set; } = string.Empty;
    public int AfterMonthOrder { get; set; }
    public int Days { get; set; } = 1;
}

public sealed class EraDefinitionProfile : WorldLoreCalendarDefinitionProfile
{
    public string CalendarDefinitionId { get; set; } = string.Empty;
    public string YearZeroPolicy { get; set; } = "has_year_zero";
    public string CountingDirection { get; set; } = "forward";
    public string StartBoundary { get; set; } = string.Empty;
    public string EndBoundary { get; set; } = string.Empty;
    public string DisplayPrefix { get; set; } = string.Empty;
    public string DisplaySuffix { get; set; } = string.Empty;
    public List<string> HistoricalTags { get; set; } = new List<string>();
}

public sealed class EventTypeDefinitionProfile : WorldLoreCalendarDefinitionProfile
{
    public string EventCategory { get; set; } = "custom";
    public string DefaultSeverity { get; set; } = "normal";
    public string DefaultVisibility { get; set; } = VisibilityRuleIds.Public;
    public List<string> AllowedVersionKinds { get; set; } = new List<string>();
    public string IconKey { get; set; } = string.Empty;
    public List<string> ApplicableLocationKinds { get; set; } = new List<string>();
    public List<string> ApplicableSubjectKinds { get; set; } = new List<string>();
}
