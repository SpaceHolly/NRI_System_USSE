using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Nri.FateControlClient.Models;
using Nri.Shared.Contracts;

namespace Nri.FateControlClient.Networking;

public sealed class FateApiClient
{
    private readonly JsonTcpClient _client;

    public FateApiClient(JsonTcpClient client)
    {
        _client = client;
    }

    public string? AuthToken { get; private set; }

    public void SetEndpoint(string host, int port) => _client.SetEndpoint(host, port);
    public bool IsConnected => _client.IsConnected;

    public void Connect() => _client.Connect();

    public ResponseEnvelope Login(string login, string password)
    {
        var response = Send(CommandNames.AuthLogin, new Dictionary<string, object>
        {
            { "login", login },
            { "password", password }
        }, includeAuth: false);

        if (response.Status == ResponseStatus.Ok && response.Payload.TryGetValue("authToken", out var tokenRaw))
        {
            AuthToken = Convert.ToString(tokenRaw);
        }

        return response;
    }

    public ResponseEnvelope GetFateStatus() => Send(CommandNames.FateStatusGet, new Dictionary<string, object>());
    public ResponseEnvelope GetFateSettings() => Send(CommandNames.FateSettingsGet, new Dictionary<string, object>());
    public ResponseEnvelope GetFateEffects() => Send(CommandNames.FateEffectsList, new Dictionary<string, object>());
    public ResponseEnvelope GetFateEffectsByLayer(int layerNumber) => Send(CommandNames.FateEffectsByLayer, new Dictionary<string, object> { { "layerNumber", layerNumber } });

    public ResponseEnvelope UpdateFateSettings(bool enabled, IEnumerable<FateLayerRow> layers)
    {
        var payloadLayers = layers
            .OrderBy(x => x.LayerNumber)
            .Select(x => new Dictionary<string, object>
            {
                { "layerNumber", x.LayerNumber },
                { "displayName", x.DisplayName },
                { "enabled", x.Enabled },
                { "flatModifier", x.FlatModifier },
                { "intensity", x.Intensity },
                { "mode", x.Mode },
                { "effectCode", x.EffectCode }
            })
            .Cast<object>()
            .ToArray();

        var settings = new Dictionary<string, object>
        {
            { "enabled", enabled },
            { "layers", payloadLayers }
        };

        return Send(CommandNames.FateSettingsUpdate, new Dictionary<string, object>
        {
            { "enabled", enabled },
            { "layers", payloadLayers },
            { "settings", settings }
        });
    }

    public ResponseEnvelope TestRoll(int dieSides, int baseRoll)
    {
        return Send(CommandNames.FateTestRoll, new Dictionary<string, object>
        {
            { "dieSides", dieSides },
            { "baseRoll", baseRoll },
            { "rollType", "fate-control" }
        });
    }

    public List<FateLayerRow> ParseSettings(ResponseEnvelope response, out bool engineEnabled)
    {
        engineEnabled = false;
        var rows = CreateDefaultLayers();
        if (response.Status != ResponseStatus.Ok)
        {
            return rows;
        }

        var payload = UnwrapSettingsRoot(response.Payload);

        if (TryReadValue(payload, "enabled", out var enabledRaw))
        {
            engineEnabled = ConvertToBool(enabledRaw);
        }

        if (!TryReadValue(payload, "layers", out var layersRaw))
        {
            return rows;
        }

        var layerItems = ToObjectList(layersRaw);
        foreach (var item in layerItems)
        {
            var map = ToDictionary(item);
            if (map.Count == 0) continue;

            var layerNumber = ConvertToInt(ReadValue(map, "layerNumber"));
            if (layerNumber < 1 || layerNumber > 5) continue;

            var row = rows[layerNumber - 1];
            row.DisplayName = Convert.ToString(ReadValue(map, "displayName")) ?? row.DisplayName;
            row.Enabled = ConvertToBool(ReadValue(map, "enabled"));
            row.FlatModifier = ConvertToInt(ReadValue(map, "flatModifier"));
            row.Intensity = ConvertToDouble(ReadValue(map, "intensity"));
            row.Mode = Convert.ToString(ReadValue(map, "mode")) ?? row.Mode;
            row.EffectCode = Convert.ToString(ReadValue(map, "effectCode")) ?? row.EffectCode;
        }

        return rows;
    }


    public List<FateEffectRow> ParseEffects(ResponseEnvelope response)
    {
        var effects = new List<FateEffectRow>();
        if (response.Status != ResponseStatus.Ok)
        {
            return effects;
        }

        if (!TryReadValue(response.Payload, "items", out var itemsRaw))
        {
            return effects;
        }

        foreach (var item in ToObjectList(itemsRaw))
        {
            var map = ToDictionary(item);
            if (map.Count == 0) continue;

            effects.Add(new FateEffectRow
            {
                LayerNumber = ConvertToInt(ReadValue(map, "layerNumber")),
                LayerName = Convert.ToString(ReadValue(map, "layerName")) ?? string.Empty,
                EffectCode = Convert.ToString(ReadValue(map, "effectCode")) ?? string.Empty,
                DisplayName = Convert.ToString(ReadValue(map, "displayName")) ?? string.Empty,
                InfluenceType = Convert.ToString(ReadValue(map, "influenceType")) ?? string.Empty,
                Strength = Convert.ToString(ReadValue(map, "strength")) ?? string.Empty,
                CanUseChaos = ConvertToBool(ReadValue(map, "canUseChaos")),
                CanUseAnomaly = ConvertToBool(ReadValue(map, "canUseAnomaly")),
                Description = Convert.ToString(ReadValue(map, "description")) ?? string.Empty
            });
        }

        return effects.OrderBy(x => x.LayerNumber).ThenBy(x => x.EffectCode).ToList();
    }

    public List<FateLayerTraceRow> ParseTrace(ResponseEnvelope response, out int fateValue, out bool applied, out string skippedReason)
    {
        fateValue = ConvertToInt(ReadValue(response.Payload, "fateValue"));
        applied = ConvertToBool(ReadValue(response.Payload, "applied"));
        skippedReason = Convert.ToString(ReadValue(response.Payload, "skippedReason")) ?? string.Empty;

        var trace = new List<FateLayerTraceRow>();
        var layersRaw = ReadValue(response.Payload, "layers");
        foreach (var item in ToObjectList(layersRaw))
        {
            var map = ToDictionary(item);
            if (map.Count == 0) continue;

            trace.Add(new FateLayerTraceRow
            {
                LayerNumber = ConvertToInt(ReadValue(map, "layerNumber")),
                LayerName = Convert.ToString(ReadValue(map, "layerName")) ?? string.Empty,
                EffectCode = Convert.ToString(ReadValue(map, "effectCode")) ?? string.Empty,
                EffectDisplayName = Convert.ToString(ReadValue(map, "effectDisplayName")) ?? string.Empty,
                InfluenceType = Convert.ToString(ReadValue(map, "influenceType")) ?? string.Empty,
                Strength = Convert.ToString(ReadValue(map, "strength")) ?? string.Empty,
                Applied = ConvertToBool(ReadValue(map, "applied")),
                AllowedForDie = ConvertToBool(ReadValue(map, "allowedForDie")),
                InputValue = ConvertToInt(ReadValue(map, "inputValue")),
                Modifier = ConvertToInt(ReadValue(map, "modifier")),
                OutputValue = ConvertToInt(ReadValue(map, "outputValue")),
                Reason = Convert.ToString(ReadValue(map, "reason")) ?? string.Empty
            });
        }

        return trace.OrderBy(x => x.LayerNumber).ToList();
    }

    private ResponseEnvelope Send(string command, Dictionary<string, object> payload, bool includeAuth = true)
    {
        var request = new RequestEnvelope
        {
            Command = command,
            Payload = payload,
            AuthToken = includeAuth ? AuthToken : null
        };

        return _client.Send(request);
    }

    private static Dictionary<string, object> UnwrapSettingsRoot(Dictionary<string, object> payload)
    {
        var root = ToDictionary(payload);
        foreach (var key in new[] { "settings", "fateSettings", "state" })
        {
            if (TryReadValue(root, key, out var nested))
            {
                var nestedMap = ToDictionary(nested);
                if (nestedMap.Count > 0)
                {
                    return nestedMap;
                }
            }
        }

        return root;
    }

    private static List<FateLayerRow> CreateDefaultLayers()
    {
        return Enumerable.Range(1, 5)
            .Select(x => new FateLayerRow
            {
                LayerNumber = x,
                DisplayName = $"Layer {x}",
                Enabled = true,
                FlatModifier = 0,
                Intensity = 1.0,
                Mode = "flat",
                EffectCode = x == 1 ? "CalmArea" : x == 5 ? "Empty" : "None"
            })
            .ToList();
    }

    private static object? ReadValue(IDictionary<string, object> map, string key)
    {
        TryReadValue(map, key, out var value);
        return value;
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

    private static int ConvertToInt(object? raw)
    {
        return int.TryParse(Convert.ToString(raw), out var value) ? value : 0;
    }

    private static double ConvertToDouble(object? raw)
    {
        return double.TryParse(Convert.ToString(raw), out var value) ? value : 1.0;
    }

    private static bool ConvertToBool(object? raw)
    {
        return bool.TryParse(Convert.ToString(raw), out var value) && value;
    }

    private static List<object> ToObjectList(object? raw)
    {
        var result = new List<object>();
        if (raw is null) return result;

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

    private static Dictionary<string, object> ToDictionary(object? raw)
    {
        if (raw is null)
        {
            return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        }

        if (raw is Dictionary<string, object> typed)
        {
            return new Dictionary<string, object>(typed, StringComparer.OrdinalIgnoreCase);
        }

        if (raw is IDictionary<string, object> generic)
        {
            return new Dictionary<string, object>(generic, StringComparer.OrdinalIgnoreCase);
        }

        if (raw is IDictionary dictionary)
        {
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry item in dictionary)
            {
                var key = Convert.ToString(item.Key);
                if (string.IsNullOrWhiteSpace(key)) continue;
                result[key] = item.Value!;
            }

            return result;
        }

        if (raw is IEnumerable enumerable && raw is not string)
        {
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in enumerable)
            {
                if (item == null) continue;
                if (item is DictionaryEntry pair)
                {
                    var key = Convert.ToString(pair.Key);
                    if (string.IsNullOrWhiteSpace(key)) continue;
                    result[key] = pair.Value!;
                    continue;
                }

                var itemType = item.GetType();
                var keyProperty = itemType.GetProperty("Key");
                var valueProperty = itemType.GetProperty("Value");
                if (keyProperty == null || valueProperty == null) continue;

                var keyValue = Convert.ToString(keyProperty.GetValue(item));
                if (string.IsNullOrWhiteSpace(keyValue)) continue;
                result[keyValue] = valueProperty.GetValue(item)!;
            }

            return result;
        }

        return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
    }
}
