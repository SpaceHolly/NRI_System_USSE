using System;
using System.Collections.Generic;
using System.Linq;
using Nri.Server.FateEngine;
using Nri.Shared.Contracts;
using Nri.Shared.Utilities;

namespace Nri.Server.Application;

public partial class ServiceHub
{
    public ResponseEnvelope FateTestRoll(CommandContext context)
    {
        GetCurrentAccount(context);

        var baseRoll = PayloadReader.GetInt(context.Request.Payload, "baseRoll")
            ?? throw new ArgumentException("baseRoll is required.");
        var dieSides = PayloadReader.GetInt(context.Request.Payload, "dieSides")
            ?? throw new ArgumentException("dieSides is required.");

        if (dieSides <= 0)
        {
            throw new ArgumentException("dieSides must be greater than zero.");
        }

        var request = new FateEngineRequest
        {
            BaseRoll = baseRoll,
            DieSides = dieSides,
            RollType = PayloadReader.GetString(context.Request.Payload, "rollType") ?? "test",
            ActorId = PayloadReader.GetString(context.Request.Payload, "actorId") ?? string.Empty,
            SceneId = PayloadReader.GetString(context.Request.Payload, "sceneId") ?? "default",
            Seed = PayloadReader.GetInt(context.Request.Payload, "seed")
        };

        var settings = BuildRollSettings(context.Request.Payload);
        var result = new FateEnginePipeline().Process(request, settings);

        if (!result.Applied)
        {
            _logger.Debug($"fate.test.roll bypass reason={result.SkippedReason}");
        }

        _logger.Debug($"fate.test.roll baseRoll={result.BaseRoll} dieSides={result.DieSides} applied={result.Applied} fateValue={result.FateValue} layers={result.Layers.Count}");
        return Ok("Fate test roll executed.", FateResultPayload(result));
    }

    public ResponseEnvelope FateStatusGet(CommandContext context)
    {
        GetCurrentAccount(context);

        var settings = _fateState.GetSnapshot();
        var enabledLayers = settings.Layers.Where(x => x.Enabled).Select(x => x.LayerNumber).Cast<object>().ToArray();
        var flatModifiers = settings.Layers.OrderBy(x => x.LayerNumber).Select(x => x.FlatModifier).Cast<object>().ToArray();

        _logger.Debug("fate.settings.get");
        return Ok("Fate status loaded.", new Dictionary<string, object>
        {
            { "enabled", settings.Enabled },
            { "layerCount", settings.Layers.Count },
            { "enabledLayers", enabledLayers },
            { "flatModifiers", flatModifiers }
        });
    }

    public ResponseEnvelope FateSettingsGet(CommandContext context)
    {
        GetCurrentAccount(context);
        var settings = _fateState.GetSnapshot();
        _logger.Debug("fate.settings.get");
        return Ok("Fate settings loaded.", FateSettingsPayload(settings));
    }

    public ResponseEnvelope FateSettingsUpdate(CommandContext context)
    {
        GetCurrentAccount(context);

        var current = _fateState.GetSnapshot();
        var settings = ParseSettingsFromPayload(context.Request.Payload, current);
        var updated = _fateState.Update(settings);

        if (current.Enabled != updated.Enabled)
        {
            _logger.Debug($"fate.engine.enabled value={updated.Enabled}");
        }

        foreach (var layer in updated.Layers.OrderBy(x => x.LayerNumber))
        {
            var before = current.Layers[layer.LayerNumber - 1];
            if (before.Enabled != layer.Enabled || before.FlatModifier != layer.FlatModifier)
            {
                _logger.Debug($"fate.layer.update layer={layer.LayerNumber} enabled={layer.Enabled} modifier={layer.FlatModifier}");
            }
        }

        _logger.Debug($"fate.settings.update enabled={updated.Enabled} layers={updated.Layers.Count}");
        return Ok("Fate settings updated.", FateSettingsPayload(updated));
    }

    private FateEngineSettings BuildRollSettings(IDictionary<string, object> payload)
    {
        var flatModifiers = PayloadReader.GetList(payload, "flatModifiers");
        if (flatModifiers == null)
        {
            return _fateState.GetSnapshot();
        }

        var settings = FateEngineSettings.CreateDefault();
        if (flatModifiers.Count > FateEngineSettings.LayerCount)
        {
            throw new ArgumentException($"flatModifiers supports up to {FateEngineSettings.LayerCount} values.");
        }

        for (var i = 0; i < flatModifiers.Count; i++)
        {
            if (!int.TryParse(Convert.ToString(flatModifiers[i]), out var modifier))
            {
                throw new ArgumentException($"flatModifiers[{i}] must be integer.");
            }

            settings.Layers[i].FlatModifier = modifier;
        }

        return settings.Normalize();
    }

    private static FateEngineSettings ParseSettingsFromPayload(IDictionary<string, object> payload, FateEngineSettings fallback)
    {
        var result = new FateEngineSettings
        {
            Enabled = fallback.Enabled,
            Layers = fallback.Layers
                .Select(layer => new FateLayerSettings
                {
                    LayerNumber = layer.LayerNumber,
                    DisplayName = layer.DisplayName,
                    Enabled = layer.Enabled,
                    Intensity = layer.Intensity,
                    Mode = layer.Mode,
                    FlatModifier = layer.FlatModifier
                })
                .ToList()
        };

        var enabled = PayloadReader.GetBool(payload, "enabled");
        if (payload.ContainsKey("enabled"))
        {
            result.Enabled = enabled;
        }

        var layers = PayloadReader.GetList(payload, "layers");
        if (layers != null)
        {
            foreach (var item in layers)
            {
                if (item is not IDictionary<string, object> rawLayer)
                {
                    continue;
                }

                var layerNumber = PayloadReader.GetInt(rawLayer, "layerNumber")
                    ?? throw new ArgumentException("Each layer must include layerNumber.");
                if (layerNumber < 1 || layerNumber > FateEngineSettings.LayerCount)
                {
                    throw new ArgumentException($"layerNumber must be 1..{FateEngineSettings.LayerCount}.");
                }

                var layer = result.Layers[layerNumber - 1];

                if (rawLayer.ContainsKey("enabled"))
                {
                    layer.Enabled = PayloadReader.GetBool(rawLayer, "enabled");
                }

                if (rawLayer.ContainsKey("flatModifier"))
                {
                    layer.FlatModifier = PayloadReader.GetInt(rawLayer, "flatModifier")
                        ?? throw new ArgumentException($"layers[{layerNumber}].flatModifier must be integer.");
                }

                if (rawLayer.ContainsKey("intensity"))
                {
                    layer.Intensity = PayloadReader.GetInt(rawLayer, "intensity")
                        ?? throw new ArgumentException($"layers[{layerNumber}].intensity must be integer.");
                }

                if (rawLayer.ContainsKey("mode"))
                {
                    layer.Mode = PayloadReader.GetString(rawLayer, "mode") ?? layer.Mode;
                }

                if (rawLayer.ContainsKey("displayName"))
                {
                    layer.DisplayName = PayloadReader.GetString(rawLayer, "displayName") ?? layer.DisplayName;
                }
            }
        }

        return result.Normalize();
    }

    private static Dictionary<string, object> FateSettingsPayload(FateEngineSettings settings)
    {
        return new Dictionary<string, object>
        {
            { "enabled", settings.Enabled },
            {
                "layers",
                settings.Layers
                    .OrderBy(x => x.LayerNumber)
                    .Select(layer => new Dictionary<string, object>
                    {
                        { "layerNumber", layer.LayerNumber },
                        { "displayName", layer.DisplayName },
                        { "enabled", layer.Enabled },
                        { "flatModifier", layer.FlatModifier },
                        { "intensity", layer.Intensity },
                        { "mode", layer.Mode }
                    })
                    .Cast<object>()
                    .ToArray()
            }
        };
    }

    private static Dictionary<string, object> FateResultPayload(FateEngineResult result)
    {
        return new Dictionary<string, object>
        {
            { "baseRoll", result.BaseRoll },
            { "dieSides", result.DieSides },
            { "fateValue", result.FateValue },
            { "applied", result.Applied },
            { "skippedReason", result.SkippedReason },
            {
                "layers",
                result.Layers
                    .Select(layer => new Dictionary<string, object>
                    {
                        { "layerNumber", layer.LayerNumber },
                        { "layerName", layer.LayerName },
                        { "enabled", layer.Enabled },
                        { "allowedForDie", layer.AllowedForDie },
                        { "applied", layer.Applied },
                        { "inputValue", layer.InputValue },
                        { "modifier", layer.Modifier },
                        { "outputValue", layer.OutputValue },
                        { "reason", layer.Reason }
                    })
                    .Cast<object>()
                    .ToArray()
            }
        };
    }
}
