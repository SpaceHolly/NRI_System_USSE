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

        var settings = BuildTestSettings(context.Request.Payload);
        var result = new FateEnginePipeline().Process(request, settings);

        if (!result.Applied)
        {
            _logger.Debug($"fate.test.roll bypass reason={result.SkippedReason}");
        }

        _logger.Debug($"fate.test.roll baseRoll={result.BaseRoll} dieSides={result.DieSides} applied={result.Applied} fateValue={result.FateValue} layers={result.Layers.Count}");
        return Ok("Fate test roll executed.", FateResultPayload(result));
    }

    private static FateEngineSettings BuildTestSettings(IDictionary<string, object> payload)
    {
        var settings = FateEngineSettings.CreateDefault();
        var flatModifiers = PayloadReader.GetList(payload, "flatModifiers");
        if (flatModifiers == null)
        {
            return settings.Normalize();
        }

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
