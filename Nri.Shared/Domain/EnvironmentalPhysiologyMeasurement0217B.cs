using System;
using System.Collections.Generic;
using System.Linq;

namespace Nri.Shared.Domain;

public static class EnvironmentMeasurementTypeIds
{
    public const string Temperature = "temperature";
    public const string WindSpeed = "wind_speed";
    public const string WindDirection = "wind_direction";
    public const string Distance = "distance";
    public const string Pressure = "pressure";
    public const string Humidity = "humidity";
    public const string Radiation = "radiation";
    public const string Toxicity = "toxicity";
    public const string AtmosphericComposition = "atmospheric_composition";
    public const string Light = "light";
    public const string Sound = "sound";
    public const string Custom = "custom";
}

public static class EnvironmentComfortStateIds
{
    public const string Comfortable = "comfortable";
    public const string Cool = "cool";
    public const string ColdStress = "cold_stress";
    public const string DangerousCold = "dangerous_cold";
    public const string CriticalCold = "critical_cold";
    public const string Warm = "warm";
    public const string HeatStress = "heat_stress";
    public const string DangerousHeat = "dangerous_heat";
    public const string CriticalHeat = "critical_heat";
    public const string Ignored = "ignored";
    public const string Custom = "custom";
}

public sealed class RelativeWindSnapshot
{
    public decimal HeadwindComponentMps { get; set; }
    public decimal TailwindComponentMps { get; set; }
    public decimal CrosswindComponentMps { get; set; }
    public string CrosswindSide { get; set; } = "None";
}

public sealed class WindVectorSnapshot
{
    public decimal SpeedMps { get; set; }
    public decimal DirectionFromDegrees { get; set; }
    public decimal FlowDirectionDegrees { get; set; }
    public decimal VectorEastMps { get; set; }
    public decimal VectorNorthMps { get; set; }
    public decimal? GustSpeedMps { get; set; }
    public string CardinalDirectionLabel { get; set; } = string.Empty;
    public string CalculationVersion { get; set; } = "wind-vector-0217b-v1";

    public static WindVectorSnapshot FromMeteorological(decimal speedMps, decimal directionFromDegrees, decimal? gustSpeedMps = null)
    {
        var from = NormalizeDegrees(directionFromDegrees);
        var flow = NormalizeDegrees(from + 180m);
        var radians = (double)flow * Math.PI / 180d;
        return new WindVectorSnapshot
        {
            SpeedMps = Math.Max(0m, speedMps),
            DirectionFromDegrees = from,
            FlowDirectionDegrees = flow,
            VectorEastMps = Round((decimal)(Math.Sin(radians) * (double)Math.Max(0m, speedMps))),
            VectorNorthMps = Round((decimal)(Math.Cos(radians) * (double)Math.Max(0m, speedMps))),
            GustSpeedMps = gustSpeedMps,
            CardinalDirectionLabel = CardinalLabel(from)
        };
    }

    public RelativeWindSnapshot ResolveRelativeWind(decimal routeBearingDegrees)
    {
        var bearing = NormalizeDegrees(routeBearingDegrees);
        var radians = (double)bearing * Math.PI / 180d;
        var routeEast = (decimal)Math.Sin(radians);
        var routeNorth = (decimal)Math.Cos(radians);
        var along = VectorEastMps * routeEast + VectorNorthMps * routeNorth;
        var cross = VectorEastMps * routeNorth - VectorNorthMps * routeEast;
        return new RelativeWindSnapshot
        {
            HeadwindComponentMps = Round(Math.Max(0m, -along)),
            TailwindComponentMps = Round(Math.Max(0m, along)),
            CrosswindComponentMps = Round(Math.Abs(cross)),
            CrosswindSide = Math.Abs(cross) < 0.0001m ? "None" : cross > 0 ? "Left" : "Right"
        };
    }

    public static decimal NormalizeDegrees(decimal value)
    {
        var normalized = value % 360m;
        return normalized < 0m ? normalized + 360m : normalized;
    }

    private static decimal Round(decimal value) => Math.Round(value, 4, MidpointRounding.AwayFromZero);

    private static string CardinalLabel(decimal degrees)
    {
        var labels = new[] { "С", "СВ", "В", "ЮВ", "Ю", "ЮЗ", "З", "СЗ" };
        var index = (int)Math.Floor((double)((NormalizeDegrees(degrees) + 22.5m) / 45m)) % 8;
        return labels[index];
    }
}

public sealed class EnvironmentalToleranceProfileDefinition : EntityBase
{
    public string RuleSetId { get; set; } = RuleSetIds.FantasyNriDefault;
    public string DisplayName { get; set; } = string.Empty;
    public List<string> ApplicableDimensions { get; set; } = new();
    public decimal TemperatureComfortMinC { get; set; }
    public decimal TemperatureComfortMaxC { get; set; }
    public decimal TemperatureSafeMinC { get; set; }
    public decimal TemperatureSafeMaxC { get; set; }
    public decimal TemperatureDangerMinC { get; set; }
    public decimal TemperatureDangerMaxC { get; set; }
    public decimal TemperatureCriticalMinC { get; set; }
    public decimal TemperatureCriticalMaxC { get; set; }
    public decimal ColdSensitivityMultiplier { get; set; } = 1m;
    public decimal HeatSensitivityMultiplier { get; set; } = 1m;
    public decimal WetSensitivityMultiplier { get; set; } = 1m;
    public decimal WindSensitivityMultiplier { get; set; } = 1m;
    public decimal HumiditySensitivityMultiplier { get; set; } = 1m;
    public decimal HypoxiaSensitivityMultiplier { get; set; } = 1m;
    public decimal HydrationConsumptionMultiplier { get; set; } = 1m;
    public List<string> IgnoredDimensions { get; set; } = new();
    public string PublicDescription { get; set; } = string.Empty;
    public string GMDescription { get; set; } = string.Empty;
    public int DefinitionVersion { get; set; } = 1;
    public bool IsArchived { get; set; }
}

public static class EnvironmentalToleranceRules
{
    public static IReadOnlyList<string> Validate(EnvironmentalToleranceProfileDefinition profile)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(profile.DisplayName)) errors.Add("Название профиля обязательно.");
        if (profile.IgnoredDimensions.Contains("temperature", StringComparer.OrdinalIgnoreCase)) return errors;
        if (!(profile.TemperatureCriticalMinC <= profile.TemperatureDangerMinC &&
              profile.TemperatureDangerMinC <= profile.TemperatureSafeMinC &&
              profile.TemperatureSafeMinC <= profile.TemperatureComfortMinC &&
              profile.TemperatureComfortMinC <= profile.TemperatureComfortMaxC &&
              profile.TemperatureComfortMaxC <= profile.TemperatureSafeMaxC &&
              profile.TemperatureSafeMaxC <= profile.TemperatureDangerMaxC &&
              profile.TemperatureDangerMaxC <= profile.TemperatureCriticalMaxC))
            errors.Add("Температурные диапазоны должны идти от критического холода к критической жаре без пересечений.");
        if (profile.ColdSensitivityMultiplier < 0m || profile.HeatSensitivityMultiplier < 0m || profile.WetSensitivityMultiplier < 0m || profile.WindSensitivityMultiplier < 0m)
            errors.Add("Коэффициенты чувствительности не могут быть отрицательными.");
        return errors;
    }
}

public static class EnvironmentMeasurementMath
{
    public static decimal MetersPerSecondFromKilometersPerHour(decimal kilometersPerHour) => kilometersPerHour / 3.6m;

    public static decimal Quantize(decimal value, decimal resolution) => resolution <= 0m ? value : Math.Round(value / resolution, 0, MidpointRounding.AwayFromZero) * resolution;

    public static decimal DeterministicError(string stableOperationKey, decimal absoluteAccuracy)
    {
        if (absoluteAccuracy <= 0m) return 0m;
        unchecked
        {
            uint hash = 2166136261;
            foreach (var character in stableOperationKey ?? string.Empty) hash = (hash ^ character) * 16777619;
            var unit = (hash % 20001) / 10000m - 1m;
            return Math.Round(unit * absoluteAccuracy, 6, MidpointRounding.AwayFromZero);
        }
    }
}

public sealed class EnvironmentalToleranceModifier
{
    public string SourceType { get; set; } = string.Empty;
    public string SourceDisplayName { get; set; } = string.Empty;
    public decimal ComfortMinDeltaC { get; set; }
    public decimal ComfortMaxDeltaC { get; set; }
    public decimal ColdSensitivityMultiplier { get; set; } = 1m;
    public decimal HeatSensitivityMultiplier { get; set; } = 1m;
    public decimal WetSensitivityMultiplier { get; set; } = 1m;
    public decimal WindSensitivityMultiplier { get; set; } = 1m;
    public decimal HumiditySensitivityMultiplier { get; set; } = 1m;
    public decimal HypoxiaSensitivityMultiplier { get; set; } = 1m;
    public decimal HydrationConsumptionMultiplier { get; set; } = 1m;
    public List<string> IgnoredDimensions { get; set; } = new();
    public bool IsPlayerVisible { get; set; } = true;
}

public sealed class ActorEnvironmentalToleranceSnapshot
{
    public string SubjectId { get; set; } = string.Empty;
    public string ProfileId { get; set; } = string.Empty;
    public string ProfileDisplayName { get; set; } = string.Empty;
    public decimal ComfortMinC { get; set; }
    public decimal ComfortMaxC { get; set; }
    public decimal SafeMinC { get; set; }
    public decimal SafeMaxC { get; set; }
    public decimal DangerMinC { get; set; }
    public decimal DangerMaxC { get; set; }
    public decimal CriticalMinC { get; set; }
    public decimal CriticalMaxC { get; set; }
    public decimal ColdSensitivityMultiplier { get; set; } = 1m;
    public decimal HeatSensitivityMultiplier { get; set; } = 1m;
    public decimal WetSensitivityMultiplier { get; set; } = 1m;
    public decimal WindSensitivityMultiplier { get; set; } = 1m;
    public decimal HumiditySensitivityMultiplier { get; set; } = 1m;
    public decimal HypoxiaSensitivityMultiplier { get; set; } = 1m;
    public decimal HydrationConsumptionMultiplier { get; set; } = 1m;
    public List<string> IgnoredDimensions { get; set; } = new();
    public List<EnvironmentalToleranceModifier> ModifierBreakdown { get; set; } = new();
    public string CalculationVersion { get; set; } = "environment-tolerance-0217b-v1";
}

public sealed class ActorEnvironmentAssessment
{
    public string SubjectId { get; set; } = string.Empty;
    public int EnvironmentRevision { get; set; }
    public decimal TemperatureC { get; set; }
    public decimal EffectiveThermalContextC { get; set; }
    public string ComfortState { get; set; } = EnvironmentComfortStateIds.Comfortable;
    public decimal ColdStressRate { get; set; }
    public decimal HeatStressRate { get; set; }
    public decimal WetExposureRate { get; set; }
    public decimal WindExposureRate { get; set; }
    public string ProtectionSummary { get; set; } = string.Empty;
    public List<string> RelevantWarnings { get; set; } = new();
    public string PublicExplanation { get; set; } = string.Empty;
    public string GMExplanation { get; set; } = string.Empty;
    public string CalculationVersion { get; set; } = "actor-environment-assessment-0217b-v1";
}

public sealed class MeasurementInstrumentProfileDefinition : EntityBase
{
    public string RuleSetId { get; set; } = RuleSetIds.FantasyNriDefault;
    public string DisplayName { get; set; } = string.Empty;
    public List<string> MeasurementTypes { get; set; } = new();
    public decimal MinimumValue { get; set; }
    public decimal MaximumValue { get; set; }
    public decimal Resolution { get; set; }
    public decimal AbsoluteAccuracy { get; set; }
    public decimal? RelativeAccuracy { get; set; }
    public int ResponseTimeSeconds { get; set; }
    public bool RequiresSetup { get; set; }
    public bool RequiresCalibration { get; set; }
    public decimal CalibrationOffset { get; set; }
    public decimal? OperatingTemperatureMinC { get; set; }
    public decimal? OperatingTemperatureMaxC { get; set; }
    public string AvailabilityRequirement { get; set; } = "held_or_equipped";
    public string RequiredSkillContext { get; set; } = string.Empty;
    public string PublicDescription { get; set; } = string.Empty;
    public int DefinitionVersion { get; set; } = 1;
    public bool IsArchived { get; set; }
}

public sealed class EnvironmentalProtectionProfileDefinition : EntityBase
{
    public string RuleSetId { get; set; } = RuleSetIds.FantasyNriDefault;
    public string DisplayName { get; set; } = string.Empty;
    public decimal ThermalInsulationC { get; set; }
    public decimal WindProtectionRatio { get; set; }
    public decimal WaterProtectionRatio { get; set; }
    public decimal HeatProtectionC { get; set; }
    public decimal RadiationProtectionRatio { get; set; }
    public decimal ToxicProtectionRatio { get; set; }
    public decimal PressureProtectionRatio { get; set; }
    public List<string> RequiredCoverageTags { get; set; } = new();
    public bool RequiresEquipped { get; set; } = true;
    public string PublicDescription { get; set; } = string.Empty;
    public int DefinitionVersion { get; set; } = 1;
    public bool IsArchived { get; set; }
}

public sealed class EnvironmentObservationRecord : EntityBase
{
    public string CampaignId { get; set; } = string.Empty;
    public string OwnerUserId { get; set; } = string.Empty;
    public string ObserverCharacterId { get; set; } = string.Empty;
    public string ScopeId { get; set; } = string.Empty;
    public string TargetReference { get; set; } = string.Empty;
    public string MeasurementType { get; set; } = string.Empty;
    public decimal? MeasuredValue { get; set; }
    public decimal? EstimatedMinValue { get; set; }
    public decimal? EstimatedMaxValue { get; set; }
    public string Unit { get; set; } = string.Empty;
    public decimal Uncertainty { get; set; }
    public decimal Confidence { get; set; }
    public string QualitativeLabel { get; set; } = string.Empty;
    public string SourceType { get; set; } = "sense";
    public string SourceDisplayName { get; set; } = string.Empty;
    public string InstrumentItemInstanceId { get; set; } = string.Empty;
    public string InstrumentProfileId { get; set; } = string.Empty;
    public long ObservedAtWorldSecond { get; set; }
    public int WeatherRevision { get; set; }
    public long StaleAfterWorldSecond { get; set; }
    public bool IsOutdated { get; set; }
    public string PlayerSafeText { get; set; } = string.Empty;
    public string OperationId { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public int RecordVersion { get; set; } = 1;
}
