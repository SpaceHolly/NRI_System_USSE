using System;
using System.Collections.Generic;

namespace Nri.Shared.Domain;

public static class WeatherDefinitionFamilyIds
{
    public const string Climate = "climate_profile";
    public const string WeatherPattern = "weather_pattern";
    public const string WeatherTransition = "weather_transition";
    public const string Environment = "environment_profile";
    public const string EnvironmentInteraction = "environment_interaction";
    public const string Exposure = "exposure_profile";
    public const string Shelter = "shelter_profile";
    public const string Forecast = "forecast_profile";
    public const string TravelMode = "travel_mode";
    public const string TerrainTravel = "terrain_travel_profile";

    public static readonly string[] All =
    {
        Climate, WeatherPattern, WeatherTransition, Environment, EnvironmentInteraction,
        Exposure, Shelter, Forecast, TravelMode, TerrainTravel
    };
}

public static class WeatherScopeTypeIds
{
    public const string World = "world";
    public const string Region = "region";
    public const string Location = "location";
    public const string Scene = "scene";
    public const string Custom = "custom";
}

public static class WeatherSourceTypeIds
{
    public const string Natural = "natural";
    public const string Magic = "magic";
    public const string Anomaly = "anomaly";
    public const string Technology = "technology";
    public const string GmOverride = "gm_override";
    public const string Custom = "custom";
}

public static class EnvironmentApplicationChannelIds
{
    public const string PresentationOnly = "presentation_only";
    public const string DeterministicModifier = "deterministic_modifier";
    public const string FateLayer = "fate_layer";
    public const string RuntimeEffect = "runtime_effect";
    public const string Travel = "travel";
    public const string MultipleExplicit = "multiple_explicit";
}

public static class ExposureAutomationModeIds
{
    public const string TrackOnly = "track_only";
    public const string SuggestCheck = "suggest_check";
    public const string RequiresGmApproval = "requires_gm_approval";
    public const string AutoApplyPreauthorized = "auto_apply_preauthorized";
    public const string Blocked = "blocked";
}

public static class TravelStatusIds
{
    public const string Draft = "draft";
    public const string Prepared = "prepared";
    public const string Active = "active";
    public const string Paused = "paused";
    public const string Interrupted = "interrupted";
    public const string Arrived = "arrived";
    public const string Cancelled = "cancelled";
    public const string Archived = "archived";
}

public sealed class WeatherScopeReference
{
    public string ScopeType { get; set; } = WeatherScopeTypeIds.World;
    public string ScopeId { get; set; } = string.Empty;
    public string WorldId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
}

public abstract class WeatherDefinitionProfile
{
    public string DefinitionId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = string.Empty;
    public string PublicDescription { get; set; } = string.Empty;
    public string GmDescription { get; set; } = string.Empty;
    public string VisibilityRule { get; set; } = VisibilityRuleIds.Public;
    public int Version { get; set; } = 1;
    public bool IsArchived { get; set; }
    public List<string> Tags { get; set; } = new();
}

public sealed class ClimateProfileDefinition : WeatherDefinitionProfile
{
    public List<string> ApplicableScopeTags { get; set; } = new();
    public List<string> SeasonBindings { get; set; } = new();
    public string BaselineTemperatureProfile { get; set; } = string.Empty;
    public string MoistureProfile { get; set; } = string.Empty;
    public string WindProfile { get; set; } = string.Empty;
    public List<string> AllowedPatternIds { get; set; } = new();
    public Dictionary<string, int> SeasonalPatternWeights { get; set; } = new();
    public bool AllowsSevereWeather { get; set; }
    public string TransitionProfileId { get; set; } = string.Empty;
    public string DefaultForecastProfileId { get; set; } = string.Empty;
}

public sealed class WeatherPatternDefinition : WeatherDefinitionProfile
{
    public string Category { get; set; } = string.Empty;
    public string Severity { get; set; } = "minor";
    public decimal TemperatureC { get; set; }
    public string PrecipitationKind { get; set; } = "none";
    public string PrecipitationIntensity { get; set; } = "none";
    public decimal WindSpeedMetersPerSecond { get; set; }
    public decimal WindDirectionDegreesFromNorth { get; set; }
    public decimal? WindGustMetersPerSecond { get; set; }
    public int VisibilityM { get; set; }
    public string CloudCover { get; set; } = string.Empty;
    public string SurfaceCondition { get; set; } = string.Empty;
    public decimal HumidityPercent { get; set; }
    public int MinDurationMinutes { get; set; } = 60;
    public int MaxDurationMinutes { get; set; } = 240;
    public List<string> PossibleNextPatternIds { get; set; } = new();
    public string EnvironmentInteractionProfileId { get; set; } = string.Empty;
    public string PublicObservationTemplate { get; set; } = string.Empty;
}

public sealed class WeightedWeatherDestination
{
    public string PatternId { get; set; } = string.Empty;
    public int Weight { get; set; } = 1;
}

public sealed class WeatherTransitionProfileDefinition : WeatherDefinitionProfile
{
    public string SourcePatternId { get; set; } = string.Empty;
    public List<WeightedWeatherDestination> Destinations { get; set; } = new();
    public Dictionary<string, decimal> SeasonModifiers { get; set; } = new();
    public int MinDurationMinutes { get; set; } = 60;
    public int MaxDurationMinutes { get; set; } = 360;
    public int RepetitionCooldown { get; set; }
    public string DeterministicSeedScope { get; set; } = "scope";
}

public sealed class EnvironmentProfileDefinition : WeatherDefinitionProfile
{
    public string Medium { get; set; } = "air";
    public decimal TemperatureC { get; set; }
    public decimal PressureKpa { get; set; } = 101.325m;
    public bool IsBreathable { get; set; } = true;
    public decimal Radiation { get; set; }
    public decimal Toxicity { get; set; }
    public string GravityBand { get; set; } = "normal";
    public string LightBand { get; set; } = "normal";
    public string SoundProfile { get; set; } = "normal";
    public string AnomalousField { get; set; } = string.Empty;
    public string SurfaceState { get; set; } = string.Empty;
    public bool IsIndoor { get; set; }
}

public sealed class EnvironmentInteractionRuleDefinition : WeatherDefinitionProfile
{
    public List<string> TargetTags { get; set; } = new();
    public List<string> RequiredEnvironmentTags { get; set; } = new();
    public string ApplicationChannel { get; set; } = EnvironmentApplicationChannelIds.PresentationOnly;
    public List<string> ExplicitChannels { get; set; } = new();
    public decimal MovementMultiplier { get; set; } = 1m;
    public decimal CapabilityModifier { get; set; }
    public string FateEnvironmentProfileId { get; set; } = string.Empty;
    public string AvailabilityPolicy { get; set; } = string.Empty;
    public string ExposureProfileId { get; set; } = string.Empty;
    public string PlayerExplanation { get; set; } = string.Empty;
}

public sealed class ExposureProfileDefinition : WeatherDefinitionProfile
{
    public string ExposureKind { get; set; } = string.Empty;
    public List<string> SourceTags { get; set; } = new();
    public decimal AccumulationPerHour { get; set; }
    public List<decimal> Thresholds { get; set; } = new();
    public decimal DecayPerHour { get; set; }
    public decimal ShelterReduction { get; set; }
    public decimal EquipmentReduction { get; set; }
    public string SuggestedCheckTag { get; set; } = string.Empty;
    public List<string> RuntimeEffectDefinitionIds { get; set; } = new();
    public string AutomationMode { get; set; } = ExposureAutomationModeIds.RequiresGmApproval;
    public string PublicWarning { get; set; } = string.Empty;
}

public sealed class ShelterProfileDefinition : WeatherDefinitionProfile
{
    public string ShelterType { get; set; } = string.Empty;
    public List<string> ApplicableDimensions { get; set; } = new();
    public Dictionary<string, decimal> ProtectionValues { get; set; } = new();
    public int Capacity { get; set; }
    public string SetupRequirement { get; set; } = string.Empty;
    public int SetupMinutes { get; set; }
    public List<string> ReferenceTags { get; set; } = new();
}

public sealed class ForecastProfileDefinition : WeatherDefinitionProfile
{
    public int HorizonMinutes { get; set; } = 360;
    public string SourceType { get; set; } = string.Empty;
    public decimal BaseReliability { get; set; } = 0.5m;
    public decimal ReliabilityLossPerHour { get; set; }
    public string UncertaintyPolicy { get; set; } = "qualitative";
    public string PublicTemplate { get; set; } = string.Empty;
    public int StaleAfterMinutes { get; set; } = 360;
    public string RequiredKnowledgeLevel { get; set; } = KnowledgeLevelIds.Partial;
}

public sealed class TravelModeDefinition : WeatherDefinitionProfile
{
    public string MovementMedium { get; set; } = "land";
    public decimal BaseSpeedKmh { get; set; } = 4m;
    public List<string> ActorOrAssetRequirements { get; set; } = new();
    public List<string> TerrainCompatibility { get; set; } = new();
    public List<string> WeatherCompatibility { get; set; } = new();
    public List<string> RequiredSkillOrToolTags { get; set; } = new();
    public List<string> RequiredSupplyCategories { get; set; } = new();
    public List<string> EnvironmentProtectionProfileIds { get; set; } = new();
}

public sealed class TerrainTravelProfileDefinition : WeatherDefinitionProfile
{
    public List<string> TerrainTags { get; set; } = new();
    public decimal MovementMultiplier { get; set; } = 1m;
    public List<string> AllowedModeIds { get; set; } = new();
    public List<string> HazardTags { get; set; } = new();
    public List<string> ShelterAvailabilityHints { get; set; } = new();
    public string NavigationDifficultyContext { get; set; } = string.Empty;
}

public sealed class WeatherStateDocument : EntityBase
{
    public string WorldId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public WeatherScopeReference Scope { get; set; } = new();
    public string ClimateProfileId { get; set; } = string.Empty;
    public string CurrentPatternId { get; set; } = string.Empty;
    public string CurrentPatternName { get; set; } = string.Empty;
    public decimal TrueTemperatureC { get; set; }
    public string TruePrecipitation { get; set; } = string.Empty;
    public decimal TrueWindSpeedMetersPerSecond { get; set; }
    public decimal TrueWindDirectionDegreesFromNorth { get; set; }
    public decimal? TrueWindGustMetersPerSecond { get; set; }
    // Read-only migration input for 0.21.7 documents. New runtime writes leave this at zero.
    public decimal TrueWindKmh { get; set; }
    public int WindUnitSchemaVersion { get; set; } = 2;
    public int TrueVisibilityM { get; set; }
    public string TrueCloudCover { get; set; } = string.Empty;
    public string TrueSurfaceCondition { get; set; } = string.Empty;
    public string Severity { get; set; } = "minor";
    public string SourceType { get; set; } = WeatherSourceTypeIds.Natural;
    public string SourceId { get; set; } = string.Empty;
    public long StartedAtWorldSecond { get; set; }
    public long ScheduledTransitionAtWorldSecond { get; set; }
    public long GenerationSeed { get; set; }
    public string RandomAlgorithmId { get; set; } = WeatherDeterministicRandom.AlgorithmId;
    public int RandomAlgorithmVersion { get; set; } = WeatherDeterministicRandom.AlgorithmVersion;
    public long TransitionIndex { get; set; }
    public bool IsLocked { get; set; }
    public long? LockUntilWorldSecond { get; set; }
    public string OverrideReason { get; set; } = string.Empty;
    public int EntityRevision { get; set; } = 1;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string UpdatedBy { get; set; } = string.Empty;
}

public sealed class TravelRouteSegment
{
    public int Order { get; set; }
    public string FromLocationId { get; set; } = string.Empty;
    public string FromLocationName { get; set; } = string.Empty;
    public string ToLocationId { get; set; } = string.Empty;
    public string ToLocationName { get; set; } = string.Empty;
    public decimal DistanceKm { get; set; }
    public string TerrainProfileId { get; set; } = string.Empty;
    public string TerrainName { get; set; } = string.Empty;
    public string WeatherScopeId { get; set; } = string.Empty;
    public string WeatherPatternId { get; set; } = string.Empty;
    public string WeatherPatternName { get; set; } = string.Empty;
    public decimal EffectiveSpeedKmh { get; set; }
    public decimal ModeMultiplier { get; set; } = 1m;
    public decimal TerrainMultiplier { get; set; } = 1m;
    public decimal WeatherMultiplier { get; set; } = 1m;
    public decimal LoadMultiplier { get; set; } = 1m;
    public int AuthoritativeDurationMinutes { get; set; }
    public int PlayerEtaMinMinutes { get; set; }
    public int PlayerEtaMaxMinutes { get; set; }
    public string NavigationContext { get; set; } = string.Empty;
    public List<string> ShelterProfileIds { get; set; } = new();
    public List<string> HazardTags { get; set; } = new();
    public bool IsCompleted { get; set; }
    public string CompletionOperationId { get; set; } = string.Empty;
    public long? CompletedAtWorldSecond { get; set; }
}

public sealed class TravelSession : EntityBase
{
    public string WorldId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string PartyId { get; set; } = string.Empty;
    public string PartyName { get; set; } = string.Empty;
    public List<string> PartyActorIds { get; set; } = new();
    public List<string> PartyOwnerUserIds { get; set; } = new();
    public List<string> PartyMemberNames { get; set; } = new();
    public string OriginLocationId { get; set; } = string.Empty;
    public string OriginLocationName { get; set; } = string.Empty;
    public string DestinationLocationId { get; set; } = string.Empty;
    public string DestinationLocationName { get; set; } = string.Empty;
    public string ModeDefinitionId { get; set; } = string.Empty;
    public string ModeName { get; set; } = string.Empty;
    public decimal ModeBaseSpeedKmh { get; set; } = 4m;
    public List<TravelRouteSegment> Segments { get; set; } = new();
    public string Status { get; set; } = TravelStatusIds.Draft;
    public long DepartureWorldSecond { get; set; }
    public int CurrentSegmentIndex { get; set; }
    public decimal Progress { get; set; }
    public int AuthoritativeEstimatedMinutes { get; set; }
    public int PlayerEtaMinMinutes { get; set; }
    public int PlayerEtaMaxMinutes { get; set; }
    public List<string> RequiredSupplyCategories { get; set; } = new();
    public List<string> AvailableSupplySummary { get; set; } = new();
    public List<string> ActiveInterruptions { get; set; } = new();
    public int Revision { get; set; } = 1;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string UpdatedByUserId { get; set; } = string.Empty;
}

public sealed class EnvironmentSnapshot
{
    public string CampaignId { get; set; } = string.Empty;
    public string ScopeId { get; set; } = string.Empty;
    public long WorldSecond { get; set; }
    public string PatternName { get; set; } = string.Empty;
    public decimal EffectiveTemperatureC { get; set; }
    public WindVectorSnapshot Wind { get; set; } = new();
    public int WeatherRevision { get; set; }
    public int VisibilityM { get; set; }
    public decimal MovementMultiplier { get; set; } = 1m;
    public string SurfaceCondition { get; set; } = string.Empty;
    public bool IsIndoor { get; set; }
    public string ShelterName { get; set; } = string.Empty;
    public decimal ExposureMultiplier { get; set; } = 1m;
    public List<string> ExposureSources { get; set; } = new();
    public List<string> PublicWarnings { get; set; } = new();
    public List<string> GmDiagnostics { get; set; } = new();
    public string CalculationVersion { get; set; } = "environment-0217b-v2";
}

public sealed class WeatherObservationProjection
{
    public string ScopeLabel { get; set; } = string.Empty;
    public string PatternName { get; set; } = string.Empty;
    public string TemperatureBand { get; set; } = string.Empty;
    public decimal? ExactTemperatureC { get; set; }
    public string Precipitation { get; set; } = string.Empty;
    public string WindBand { get; set; } = string.Empty;
    public string VisibilityBand { get; set; } = string.Empty;
    public string SurfaceCondition { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public long ObservedAtWorldSecond { get; set; }
    public decimal Confidence { get; set; }
    public string Summary { get; set; } = string.Empty;
}

public static class WeatherDeterministicRandom
{
    public const string AlgorithmId = "splitmix64";
    public const int AlgorithmVersion = 1;

    public static ulong Value(long seed, long transitionIndex, ulong stream = 0)
    {
        unchecked
        {
            var state = (ulong)seed + (0x9E3779B97F4A7C15UL * ((ulong)transitionIndex + 1UL)) + stream;
            state = (state ^ (state >> 30)) * 0xBF58476D1CE4E5B9UL;
            state = (state ^ (state >> 27)) * 0x94D049BB133111EBUL;
            return state ^ (state >> 31);
        }
    }

    public static decimal Unit(long seed, long transitionIndex, ulong stream = 0)
        => (Value(seed, transitionIndex, stream) >> 11) / (decimal)(1UL << 53);

    public static int Range(long seed, long transitionIndex, int minInclusive, int maxExclusive, ulong stream = 0)
    {
        if (maxExclusive <= minInclusive) return minInclusive;
        return minInclusive + (int)(Value(seed, transitionIndex, stream) % (uint)(maxExclusive - minInclusive));
    }
}
