using System;
using System.Collections.Generic;
using System.Linq;

namespace Nri.Server.FateEngine;

public sealed class FateEnginePipeline
{
    private static readonly FateEffectCatalog EffectCatalog = new FateEffectCatalog();
    private static readonly object SharedRandomSync = new object();
    private static readonly Random SharedRandom = new Random();

    public FateEngineResult Process(FateEngineRequest request, FateEngineSettings? settings)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var normalizedSettings = (settings ?? FateEngineSettings.CreateDefault()).Normalize();
        var result = new FateEngineResult
        {
            BaseRoll = request.BaseRoll,
            DieSides = request.DieSides,
            FateValue = request.BaseRoll
        };

        if (!normalizedSettings.Enabled)
        {
            result.Applied = false;
            result.SkippedReason = "Fate Engine is disabled.";
            return result;
        }

        if (!FateEngineRules.IsFateEligible(request.DieSides))
        {
            result.Applied = false;
            result.SkippedReason = "Die d4 and lower bypass Fate Engine.";
            return result;
        }

        var currentValue = request.BaseRoll;
        Random? seededRandom = request.Seed.HasValue ? new Random(request.Seed.Value) : null;

        foreach (var layer in normalizedSettings.Layers)
        {
            var allowedForDie = FateEngineRules.IsLayerAllowedForDie(request.DieSides, layer.LayerNumber);
            var layerResult = new FateLayerResult
            {
                LayerNumber = layer.LayerNumber,
                LayerName = string.IsNullOrWhiteSpace(layer.DisplayName) ? $"Layer {layer.LayerNumber}" : layer.DisplayName,
                EffectCode = string.IsNullOrWhiteSpace(layer.EffectCode) ? "None" : layer.EffectCode,
                Enabled = layer.Enabled,
                AllowedForDie = allowedForDie,
                InputValue = currentValue,
                OutputValue = currentValue,
                Modifier = 0,
                SelectedValue = currentValue
            };

            var effectDefinition = EffectCatalog.Find(layer.LayerNumber, layerResult.EffectCode);
            if (effectDefinition != null)
            {
                layerResult.EffectDisplayName = effectDefinition.DisplayName;
                layerResult.InfluenceType = effectDefinition.InfluenceType;
                layerResult.Strength = effectDefinition.Strength;
            }
            else
            {
                layerResult.EffectDisplayName = "not found";
                layerResult.InfluenceType = "Unknown";
                layerResult.Strength = "Unknown";
            }

            layerResult.CandidateRolls.Add(currentValue);

            if (!layer.Enabled)
            {
                layerResult.Applied = false;
                layerResult.Reason = effectDefinition == null
                    ? "Layer disabled in settings; effect definition not found."
                    : "Layer disabled in settings.";
                result.Layers.Add(layerResult);
                continue;
            }

            if (!allowedForDie)
            {
                layerResult.Applied = false;
                layerResult.Reason = effectDefinition == null
                    ? "Layer is not allowed for current die size; effect definition not found."
                    : "Layer is not allowed for current die size.";
                result.Layers.Add(layerResult);
                continue;
            }

            var influenceType = (layerResult.InfluenceType ?? string.Empty).Trim();
            var strength = (layerResult.Strength ?? string.Empty).Trim();
            var calcDetails = new List<string>();
            var valueAfterInfluence = currentValue;
            var allowOutOfBounds = false;
            var specialFallback = false;

            switch (influenceType.ToLowerInvariant())
            {
                case "none":
                    calcDetails.Add("Influence None: value unchanged.");
                    break;
                case "biasup":
                    valueAfterInfluence = ApplyBias(
                        currentValue,
                        request.DieSides,
                        strength,
                        biasUp: true,
                        layerResult,
                        seededRandom,
                        calcDetails);
                    break;
                case "biasdown":
                    valueAfterInfluence = ApplyBias(
                        currentValue,
                        request.DieSides,
                        strength,
                        biasUp: false,
                        layerResult,
                        seededRandom,
                        calcDetails);
                    break;
                case "pulltomiddle":
                    valueAfterInfluence = ApplyPullToMiddle(currentValue, request.DieSides, StrengthPercent(strength), layerResult, calcDetails, "PullToMiddle");
                    break;
                case "pulltoextreme":
                    valueAfterInfluence = ApplyPullToExtreme(currentValue, request.DieSides, StrengthPercent(strength), layerResult, calcDetails, "PullToExtreme");
                    break;
                case "stabilize":
                    valueAfterInfluence = ApplyPullToMiddle(currentValue, request.DieSides, StrengthPercentStabilize(strength), layerResult, calcDetails, "Stabilize");
                    break;
                case "stabilizeup":
                    valueAfterInfluence = ApplyPullToMiddle(currentValue, request.DieSides, StrengthPercentStabilize(strength), layerResult, calcDetails, "StabilizeUp base");
                    {
                        var upShift = ComputeTowardTopShift(valueAfterInfluence, request.DieSides, StrengthPercentStabilize(strength));
                        valueAfterInfluence += upShift;
                        layerResult.DistributionShift += upShift;
                        calcDetails.Add($"StabilizeUp upward shift={upShift}.");
                    }
                    break;
                case "destabilize":
                    valueAfterInfluence = ApplyPullToExtreme(currentValue, request.DieSides, StrengthPercent(strength), layerResult, calcDetails, "Destabilize");
                    break;
                case "destabilizedown":
                    valueAfterInfluence = ApplyPullToExtreme(currentValue, request.DieSides, StrengthPercent(strength), layerResult, calcDetails, "DestabilizeDown base");
                    {
                        var downShift = ComputeTowardBottomShift(valueAfterInfluence, request.DieSides, StrengthPercent(strength));
                        valueAfterInfluence += downShift;
                        layerResult.DistributionShift += downShift;
                        calcDetails.Add($"DestabilizeDown downward shift={downShift}.");
                    }
                    break;
                case "biasdownandextreme":
                    valueAfterInfluence = ApplyBias(
                        currentValue,
                        request.DieSides,
                        strength,
                        biasUp: false,
                        layerResult,
                        seededRandom,
                        calcDetails);
                    valueAfterInfluence = ApplyPullToExtreme(valueAfterInfluence, request.DieSides, 0.15, layerResult, calcDetails, "BiasDownAndExtreme");
                    break;
                case "biasupandextreme":
                    valueAfterInfluence = ApplyBias(
                        currentValue,
                        request.DieSides,
                        strength,
                        biasUp: true,
                        layerResult,
                        seededRandom,
                        calcDetails);
                    valueAfterInfluence = ApplyPullToExtreme(valueAfterInfluence, request.DieSides, 0.15, layerResult, calcDetails, "BiasUpAndExtreme");
                    break;
                case "anomaly":
                    {
                        var maxShiftPercent = strength.ToLowerInvariant() switch
                        {
                            "weak" => 0.25,
                            "medium" => 0.50,
                            "strong" => 1.00,
                            "special" => 1.50,
                            _ => 0.25
                        };
                        var maxShift = Math.Max(1, (int)Math.Round(request.DieSides * maxShiftPercent));
                        var anomalyShift = NextInt(-maxShift, maxShift + 1, seededRandom);
                        valueAfterInfluence += anomalyShift;
                        layerResult.AnomalyShift = anomalyShift;
                        allowOutOfBounds = true;
                        calcDetails.Add($"Anomaly shift={anomalyShift} range=[{-maxShift};{maxShift}] (no clamp).");
                    }
                    break;
                case "chaos":
                    valueAfterInfluence = ApplyChaos(currentValue, request.DieSides, strength, layerResult, seededRandom, calcDetails);
                    break;
                default:
                    specialFallback = true;
                    calcDetails.Add("Special influence type is not implemented yet; flat modifier only.");
                    break;
            }

            if (!allowOutOfBounds)
            {
                var clamped = ClampToDie(valueAfterInfluence, request.DieSides);
                if (clamped != valueAfterInfluence)
                {
                    calcDetails.Add($"Clamp after influence: {valueAfterInfluence} -> {clamped}.");
                }
                valueAfterInfluence = clamped;
            }

            var modifier = layer.FlatModifier;
            var output = valueAfterInfluence + modifier;
            if (!allowOutOfBounds)
            {
                var clampedOutput = ClampToDie(output, request.DieSides);
                if (clampedOutput != output)
                {
                    calcDetails.Add($"Clamp after flat modifier: {output} -> {clampedOutput}.");
                }
                output = clampedOutput;
            }
            currentValue = output;

            layerResult.Applied = true;
            layerResult.Modifier = modifier;
            layerResult.OutputValue = currentValue;
            layerResult.SelectedValue = valueAfterInfluence;
            layerResult.CalculationDetails = string.Join(" ", calcDetails);
            layerResult.Reason = specialFallback
                ? "Special influence type is not implemented yet; flat modifier only."
                : effectDefinition == null
                    ? "Applied effect math + flat modifier; effect definition not found."
                    : "Applied effect math + flat modifier.";

            result.Layers.Add(layerResult);
        }

        result.Applied = result.Layers.Exists(static x => x.Applied);
        result.FateValue = currentValue;

        if (!result.Applied)
        {
            result.SkippedReason = "No Fate layers were applied.";
        }

        return result;
    }

    private static int ApplyBias(
        int currentValue,
        int dieSides,
        string strength,
        bool biasUp,
        FateLayerResult layerResult,
        Random? seededRandom,
        List<string> calcDetails)
    {
        var extraRolls = StrengthToExtraCandidates(strength);
        for (var i = 0; i < extraRolls; i++)
        {
            layerResult.CandidateRolls.Add(NextInt(1, dieSides + 1, seededRandom));
        }

        var selected = biasUp
            ? layerResult.CandidateRolls.Max()
            : layerResult.CandidateRolls.Min();

        calcDetails.Add($"{(biasUp ? "BiasUp" : "BiasDown")} {strength}: selected {(biasUp ? "max" : "min")} of {layerResult.CandidateRolls.Count} values.");
        return selected;
    }

    private static int ApplyPullToMiddle(
        int currentValue,
        int dieSides,
        double percent,
        FateLayerResult layerResult,
        List<string> calcDetails,
        string label)
    {
        var midpoint = (dieSides + 1) / 2.0;
        var shift = (int)Math.Round((midpoint - currentValue) * percent);
        layerResult.DistributionShift += shift;
        calcDetails.Add($"{label}: midpoint={midpoint:F2}, shift={shift}, percent={percent:P0}.");
        return currentValue + shift;
    }

    private static int ApplyPullToExtreme(
        int currentValue,
        int dieSides,
        double percent,
        FateLayerResult layerResult,
        List<string> calcDetails,
        string label)
    {
        var midpoint = (dieSides + 1) / 2.0;

        var shift = currentValue < midpoint
            ? (int)Math.Round((currentValue - 1) * percent * -1.0)
            : (int)Math.Round((dieSides - currentValue) * percent);

        layerResult.DistributionShift += shift;
        calcDetails.Add($"{label}: midpoint={midpoint:F2}, shift={shift}, percent={percent:P0}.");
        return currentValue + shift;
    }

    private static int ApplyChaos(
        int currentValue,
        int dieSides,
        string strength,
        FateLayerResult layerResult,
        Random? seededRandom,
        List<string> calcDetails)
    {
        var chaosScale = strength.ToLowerInvariant() switch
        {
            "weak" => 0.5,
            "medium" => 1.0,
            "strong" => 1.5,
            _ => 1.0
        };

        var variant = NextInt(0, 5, seededRandom);
        calcDetails.Add($"Chaos {strength}: variant={variant}.");

        return variant switch
        {
            0 => ApplyBias(currentValue, dieSides, "Weak", true, layerResult, seededRandom, calcDetails),
            1 => ApplyBias(currentValue, dieSides, "Weak", false, layerResult, seededRandom, calcDetails),
            2 => ApplyPullToMiddle(currentValue, dieSides, 0.10, layerResult, calcDetails, "Chaos->PullToMiddle"),
            3 => ApplyPullToExtreme(currentValue, dieSides, 0.15, layerResult, calcDetails, "Chaos->PullToExtreme"),
            _ => ApplyChaosRandomShift(currentValue, dieSides, chaosScale, layerResult, seededRandom, calcDetails)
        };
    }

    private static int ApplyChaosRandomShift(
        int currentValue,
        int dieSides,
        double chaosScale,
        FateLayerResult layerResult,
        Random? seededRandom,
        List<string> calcDetails)
    {
        var maxShift = Math.Max(1, (int)Math.Round(dieSides * 0.30 * chaosScale));
        var shift = NextInt(-maxShift, maxShift + 1, seededRandom);
        layerResult.ChaosShift = shift;
        calcDetails.Add($"Chaos random shift={shift} range=[{-maxShift};{maxShift}].");
        return currentValue + shift;
    }

    private static int StrengthToExtraCandidates(string strength)
    {
        return strength.ToLowerInvariant() switch
        {
            "weak" => 1,
            "medium" => 2,
            "strong" => 3,
            _ => 1
        };
    }

    private static double StrengthPercent(string strength)
    {
        return strength.ToLowerInvariant() switch
        {
            "weak" => 0.15,
            "medium" => 0.30,
            "strong" => 0.45,
            _ => 0.15
        };
    }

    private static double StrengthPercentStabilize(string strength)
    {
        return strength.ToLowerInvariant() switch
        {
            "weak" => 0.10,
            "medium" => 0.20,
            "strong" => 0.30,
            _ => 0.10
        };
    }

    private static int ComputeTowardTopShift(int value, int dieSides, double percent)
    {
        var distance = dieSides - value;
        return (int)Math.Round(Math.Max(0, distance) * percent);
    }

    private static int ComputeTowardBottomShift(int value, int dieSides, double percent)
    {
        var midpoint = (dieSides + 1) / 2.0;
        if (value >= midpoint)
        {
            var down = (int)Math.Round((value - midpoint) * percent);
            return -Math.Max(0, down);
        }

        var towardBottom = (int)Math.Round((value - 1) * percent);
        return -Math.Max(0, towardBottom);
    }

    private static int ClampToDie(int value, int dieSides)
    {
        return Math.Max(1, Math.Min(dieSides, value));
    }

    private static int NextInt(int minInclusive, int maxExclusive, Random? seededRandom)
    {
        if (seededRandom != null)
        {
            return seededRandom.Next(minInclusive, maxExclusive);
        }

        lock (SharedRandomSync)
        {
            return SharedRandom.Next(minInclusive, maxExclusive);
        }
    }
}
