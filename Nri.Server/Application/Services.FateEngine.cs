using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Nri.Server.FateEngine;
using Nri.Shared.Contracts;
using Nri.Shared.Utilities;

namespace Nri.Server.Application;

public partial class ServiceHub
{
    private static readonly FateEffectCatalog EffectCatalog = new FateEffectCatalog();

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

    public ResponseEnvelope FateEffectsList(CommandContext context)
    {
        GetCurrentAccount(context);

        var items = EffectCatalog.GetAll()
            .Select(EffectPayload)
            .Cast<object>()
            .ToArray();

        return Ok("Fate effects loaded.", new Dictionary<string, object> { { "items", items } });
    }

    public ResponseEnvelope FateEffectsByLayer(CommandContext context)
    {
        GetCurrentAccount(context);

        var layerNumber = PayloadReader.GetInt(context.Request.Payload, "layerNumber")
            ?? throw new ArgumentException("layerNumber is required.");

        if (layerNumber < 1 || layerNumber > FateEngineSettings.LayerCount)
        {
            throw new ArgumentException($"layerNumber must be 1..{FateEngineSettings.LayerCount}.");
        }

        var items = EffectCatalog.GetByLayer(layerNumber)
            .Select(EffectPayload)
            .Cast<object>()
            .ToArray();

        return Ok("Fate layer effects loaded.", new Dictionary<string, object> { { "items", items } });
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
        _logger.Debug($"fate.settings.get effects={BuildEffectSummary(settings.Layers)}");
        return Ok("Fate settings loaded.", FateSettingsPayload(settings));
    }

    public ResponseEnvelope FateSettingsUpdate(CommandContext context)
    {
        GetCurrentAccount(context);

        var payloadKeys = string.Join(",", context.Request.Payload.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
        _logger.Debug($"fate.settings.update payload.keys={payloadKeys}");

        var current = _fateState.GetSnapshot();
        var settings = ParseSettingsFromPayload(context.Request.Payload, current, out var parsedLayersCount, out var parsedMods, out var unwrapSource, out var rawLayersType, out var rawLayersCount);
        var parsedEffects = BuildEffectSummary(settings.Layers);
        var updated = _fateState.Update(settings);
        var savedEffects = BuildEffectSummary(updated.Layers);

        _logger.Debug($"fate.settings.update settings.unwrap={unwrapSource}");
        _logger.Debug($"fate.settings.update rawLayersType={rawLayersType}");
        _logger.Debug($"fate.settings.update rawLayersCount={rawLayersCount}");
        _logger.Debug($"fate.settings.update parsedLayersCount={parsedLayersCount}");
        _logger.Debug($"fate.settings.update parsedMods={parsedMods}");
        _logger.Debug($"fate.settings.update parsedEffects={parsedEffects}");

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

        var savedMods = string.Join("/", updated.Layers.OrderBy(x => x.LayerNumber).Select(x => x.FlatModifier));
        _logger.Debug($"fate.settings.update savedMods={savedMods}");
        _logger.Debug($"fate.settings.update savedEffects={savedEffects}");
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

    private FateEngineSettings ParseSettingsFromPayload(
        IDictionary<string, object> payload,
        FateEngineSettings fallback,
        out int parsedLayersCount,
        out string parsedMods,
        out string unwrapSource,
        out string rawLayersType,
        out int rawLayersCount)
    {
        parsedLayersCount = 0;
        parsedMods = string.Empty;
        unwrapSource = "root";
        rawLayersType = "null";
        rawLayersCount = 0;

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
                    FlatModifier = layer.FlatModifier,
                    EffectCode = layer.EffectCode
                })
                .ToList()
        };

        var source = ToObjectDictionary(payload);
        if (TryReadValue(source, "settings", out var settingsRaw))
        {
            var settingsMap = ToObjectDictionary(settingsRaw);
            if (settingsMap.Count > 0)
            {
                source = settingsMap;
                unwrapSource = "settings";
            }
        }

        if (TryReadValue(source, "enabled", out var enabledRaw))
        {
            result.Enabled = ConvertToBool(enabledRaw);
        }

        TryReadValue(source, "layers", out var layersRaw);
        rawLayersType = layersRaw?.GetType().FullName ?? "null";

        var layers = ToObjectList(layersRaw);
        rawLayersCount = layers.Count;

        foreach (var item in layers)
        {
            var rawLayer = ToObjectDictionary(item);
            if (rawLayer.Count == 0)
            {
                continue;
            }

            var layerNumber = ConvertToInt(ReadValue(rawLayer, "layerNumber"));
            if (layerNumber < 1 || layerNumber > FateEngineSettings.LayerCount)
            {
                continue;
            }

            var layer = result.Layers[layerNumber - 1];

            if (ContainsKey(rawLayer, "enabled"))
            {
                layer.Enabled = ConvertToBool(ReadValue(rawLayer, "enabled"));
            }

            if (ContainsKey(rawLayer, "flatModifier"))
            {
                layer.FlatModifier = ConvertToInt(ReadValue(rawLayer, "flatModifier"));
            }

            if (ContainsKey(rawLayer, "intensity"))
            {
                layer.Intensity = ConvertToInt(ReadValue(rawLayer, "intensity"));
            }

            if (ContainsKey(rawLayer, "mode"))
            {
                layer.Mode = Convert.ToString(ReadValue(rawLayer, "mode")) ?? layer.Mode;
            }

            if (ContainsKey(rawLayer, "displayName"))
            {
                layer.DisplayName = Convert.ToString(ReadValue(rawLayer, "displayName")) ?? layer.DisplayName;
            }

            if (ContainsKey(rawLayer, "effectCode"))
            {
                layer.EffectCode = Convert.ToString(ReadValue(rawLayer, "effectCode")) ?? layer.EffectCode;
            }

            parsedLayersCount++;
        }

        parsedMods = string.Join("/", result.Layers.OrderBy(x => x.LayerNumber).Select(x => x.FlatModifier));
        return result.Normalize();
    }

    private static bool ContainsKey(IDictionary<string, object> map, string key)
    {
        return map.ContainsKey(key) || map.Keys.Any(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryReadValue(IDictionary<string, object> map, string key, out object? value)
    {
        if (map.TryGetValue(key, out value))
        {
            return true;
        }

        var found = map.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(found.Key))
        {
            value = found.Value;
            return true;
        }

        value = null;
        return false;
    }

    private static object? ReadValue(IDictionary<string, object> map, string key)
    {
        TryReadValue(map, key, out var value);
        return value;
    }

    private static int ConvertToInt(object? raw)
    {
        if (raw is null)
        {
            return 0;
        }

        if (raw is int i)
        {
            return i;
        }

        if (raw is long l)
        {
            return (int)l;
        }

        if (raw is double d)
        {
            return (int)Math.Round(d);
        }

        if (raw is decimal dc)
        {
            return (int)Math.Round(dc);
        }

        if (double.TryParse(Convert.ToString(raw), out var parsedDouble))
        {
            return (int)Math.Round(parsedDouble);
        }

        return 0;
    }

    private static bool ConvertToBool(object? raw)
    {
        return bool.TryParse(Convert.ToString(raw), out var value) && value;
    }

    private static List<object> ToObjectList(object? raw)
    {
        var result = new List<object>();
        if (raw is null)
        {
            return result;
        }

        if (raw is IList<object> typedList)
        {
            result.AddRange(typedList);
            return result;
        }

        if (raw is object[] array)
        {
            result.AddRange(array);
            return result;
        }

        if (raw is IEnumerable enumerable && raw is not string)
        {
            foreach (var item in enumerable)
            {
                if (item != null)
                {
                    result.Add(item);
                }
            }
        }

        return result;
    }

    private static Dictionary<string, object> ToObjectDictionary(object? raw)
    {
        if (raw is null)
        {
            return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        }

        if (raw is Dictionary<string, object> direct)
        {
            return new Dictionary<string, object>(direct, StringComparer.OrdinalIgnoreCase);
        }

        if (raw is IDictionary<string, object> generic)
        {
            return new Dictionary<string, object>(generic, StringComparer.OrdinalIgnoreCase);
        }

        if (raw is IDictionary dictionary)
        {
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry entry in dictionary)
            {
                var key = Convert.ToString(entry.Key);
                if (string.IsNullOrWhiteSpace(key)) continue;
                result[key] = entry.Value!;
            }

            return result;
        }

        if (raw is IEnumerable enumerable && raw is not string)
        {
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in enumerable)
            {
                if (item == null) continue;

                if (item is DictionaryEntry entry)
                {
                    var key = Convert.ToString(entry.Key);
                    if (string.IsNullOrWhiteSpace(key)) continue;
                    result[key] = entry.Value!;
                    continue;
                }

                if (item is object[] arrayPair && arrayPair.Length == 2)
                {
                    var key = Convert.ToString(arrayPair[0]);
                    if (string.IsNullOrWhiteSpace(key)) continue;
                    result[key] = arrayPair[1]!;
                    continue;
                }

                if (item is IList listPair && listPair.Count == 2)
                {
                    var key = Convert.ToString(listPair[0]);
                    if (string.IsNullOrWhiteSpace(key)) continue;
                    result[key] = listPair[1]!;
                    continue;
                }

                var itemType = item.GetType();
                var keyProperty = itemType.GetProperty("Key");
                var valueProperty = itemType.GetProperty("Value");
                if (keyProperty == null || valueProperty == null) continue;

                var reflectedKey = Convert.ToString(keyProperty.GetValue(item));
                if (string.IsNullOrWhiteSpace(reflectedKey)) continue;
                result[reflectedKey] = valueProperty.GetValue(item)!;
            }

            return result;
        }

        return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
    }

    private static Dictionary<string, object> EffectPayload(FateLayerEffectDefinition effect)
    {
        return new Dictionary<string, object>
        {
            { "layerNumber", effect.LayerNumber },
            { "layerName", effect.LayerName },
            { "effectCode", effect.EffectCode },
            { "displayName", effect.DisplayName },
            { "influenceType", effect.InfluenceType },
            { "strength", effect.Strength },
            { "canUseChaos", effect.CanUseChaos },
            { "canUseAnomaly", effect.CanUseAnomaly },
            { "description", effect.Description }
        };
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
                        { "mode", layer.Mode },
                        { "effectCode", layer.EffectCode }
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
                        { "effectCode", layer.EffectCode },
                        { "effectDisplayName", layer.EffectDisplayName },
                        { "influenceType", layer.InfluenceType },
                        { "strength", layer.Strength },
                        { "enabled", layer.Enabled },
                        { "allowedForDie", layer.AllowedForDie },
                        { "applied", layer.Applied },
                        { "inputValue", layer.InputValue },
                        { "modifier", layer.Modifier },
                        { "outputValue", layer.OutputValue },
                        { "candidateRolls", layer.CandidateRolls.Cast<object>().ToArray() },
                        { "selectedValue", layer.SelectedValue },
                        { "distributionShift", layer.DistributionShift },
                        { "anomalyShift", layer.AnomalyShift },
                        { "chaosShift", layer.ChaosShift },
                        { "calculationDetails", layer.CalculationDetails },
                        { "reason", layer.Reason }
                    })
                    .Cast<object>()
                    .ToArray()
            }
        };
    }

    private static string BuildEffectSummary(IEnumerable<FateLayerSettings> layers)
    {
        return string.Join(" ", layers.OrderBy(x => x.LayerNumber).Select(x => $"layer{x.LayerNumber}={x.EffectCode}"));
    }
}
