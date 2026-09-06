using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using Nri.Shared.Domain;

namespace Nri.EnvironmentObservation.Contracts;

internal static class Program
{
    private static readonly Dictionary<string, bool> Checks = new(StringComparer.OrdinalIgnoreCase);
    private static readonly List<string> Errors = new();

    private static int Main(string[] args)
    {
        var outputDirectory = Path.GetFullPath(args.Length > 0 ? args[0] : "obj/0_21_7B");
        Directory.CreateDirectory(outputDirectory);
        try
        {
            Check("legacy.18kmhEquals5mps", EnvironmentMeasurementMath.MetersPerSecondFromKilometersPerHour(18m) == 5m);
            Check("legacy.45kmhEquals12_5mps", EnvironmentMeasurementMath.MetersPerSecondFromKilometersPerHour(45m) == 12.5m);

            var northWind = WindVectorSnapshot.FromMeteorological(10m, 0m);
            Check("wind.fromNorthFlowsSouth", Near(northWind.VectorEastMps, 0m) && Near(northWind.VectorNorthMps, -10m));
            Check("wind.northRouteHeadwind", Near(northWind.ResolveRelativeWind(0m).HeadwindComponentMps, 10m));
            Check("wind.southRouteTailwind", Near(northWind.ResolveRelativeWind(180m).TailwindComponentMps, 10m));
            Check("wind.eastRouteCrosswind", Near(northWind.ResolveRelativeWind(90m).CrosswindComponentMps, 10m));

            var eastWind = WindVectorSnapshot.FromMeteorological(8m, 90m);
            Check("wind.fromEastFlowsWest", Near(eastWind.VectorEastMps, -8m) && Near(eastWind.VectorNorthMps, 0m));
            Check("wind.directionNormalized", WindVectorSnapshot.FromMeteorological(1m, -45m).DirectionFromDegrees == 315m);

            var valid = ValidTolerance();
            Check("tolerance.validProfileAccepted", EnvironmentalToleranceRules.Validate(valid).Count == 0);
            valid.TemperatureSafeMinC = 30m;
            Check("tolerance.overlapRejected", EnvironmentalToleranceRules.Validate(valid).Count > 0);
            valid = ValidTolerance();
            valid.ColdSensitivityMultiplier = -1m;
            Check("tolerance.negativeMultiplierRejected", EnvironmentalToleranceRules.Validate(valid).Count > 0);

            var errorA = EnvironmentMeasurementMath.DeterministicError("operation-0217b", 0.5m);
            var errorB = EnvironmentMeasurementMath.DeterministicError("operation-0217b", 0.5m);
            Check("measurement.errorDeterministic", errorA == errorB);
            Check("measurement.errorWithinAccuracy", Math.Abs(errorA) <= 0.5m);
            Check("measurement.quantization", EnvironmentMeasurementMath.Quantize(12.26m, 0.5m) == 12.5m);
        }
        catch (Exception ex)
        {
            Errors.Add(ex.ToString());
        }

        var status = Errors.Count == 0 && Checks.Count >= 14 && Checks.Values.All(value => value) ? "PASS" : "NOT_PASS";
        File.WriteAllText(Path.Combine(outputDirectory, "environment_observation_contract_audit.json"), new JavaScriptSerializer().Serialize(new
        {
            status,
            windConvention = "meteorological direction FROM true north; clockwise degrees",
            canonicalWindUnit = "m/s",
            checks = Checks,
            errors = Errors
        }));
        Console.WriteLine("Environment observation contracts: " + status);
        return status == "PASS" ? 0 : 1;
    }

    private static EnvironmentalToleranceProfileDefinition ValidTolerance() => new()
    {
        DisplayName = "Contract profile",
        TemperatureCriticalMinC = -40m,
        TemperatureDangerMinC = -20m,
        TemperatureSafeMinC = 0m,
        TemperatureComfortMinC = 16m,
        TemperatureComfortMaxC = 26m,
        TemperatureSafeMaxC = 35m,
        TemperatureDangerMaxC = 45m,
        TemperatureCriticalMaxC = 60m
    };

    private static bool Near(decimal actual, decimal expected) => Math.Abs(actual - expected) < 0.001m;

    private static void Check(string name, bool passed)
    {
        Checks[name] = passed;
        if (!passed) Errors.Add(name + " failed");
    }
}
