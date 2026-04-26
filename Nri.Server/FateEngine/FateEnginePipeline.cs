using System;

namespace Nri.Server.FateEngine;

public sealed class FateEnginePipeline
{
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

        foreach (var layer in normalizedSettings.Layers)
        {
            var allowedForDie = FateEngineRules.IsLayerAllowedForDie(request.DieSides, layer.LayerNumber);
            var layerResult = new FateLayerResult
            {
                LayerNumber = layer.LayerNumber,
                LayerName = string.IsNullOrWhiteSpace(layer.DisplayName) ? $"Layer {layer.LayerNumber}" : layer.DisplayName,
                Enabled = layer.Enabled,
                AllowedForDie = allowedForDie,
                InputValue = currentValue,
                OutputValue = currentValue,
                Modifier = 0
            };

            if (!layer.Enabled)
            {
                layerResult.Applied = false;
                layerResult.Reason = "Layer disabled in settings.";
                result.Layers.Add(layerResult);
                continue;
            }

            if (!allowedForDie)
            {
                layerResult.Applied = false;
                layerResult.Reason = "Layer is not allowed for current die size.";
                result.Layers.Add(layerResult);
                continue;
            }

            var modifier = layer.FlatModifier;
            currentValue += modifier;

            layerResult.Applied = true;
            layerResult.Modifier = modifier;
            layerResult.OutputValue = currentValue;
            layerResult.Reason = "Applied flat modifier.";

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
}
