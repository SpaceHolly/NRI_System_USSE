using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Nri.AssetConfigurators.Wpf.Models;

public sealed class AssetBlueprintPresentation
{
    public string BlueprintId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ConfiguratorKind { get; set; } = string.Empty;
    public string ConfiguratorKindLabel { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string StatusLabel { get; set; } = string.Empty;
    public string Visibility { get; set; } = string.Empty;
    public int Revision { get; set; }
    public string ReadableSummary { get; set; } = string.Empty;
    public bool IsValid { get; set; }
    public long TotalCost { get; set; }
    public int EnergyProduced { get; set; }
    public int EnergyConsumed { get; set; }
    public string OwnerLogin { get; set; } = string.Empty;
    public string AdminGmNotes { get; set; } = string.Empty;
    public string UpdatedText { get; set; } = string.Empty;
    public Dictionary<string, object> Configuration { get; set; } = new Dictionary<string, object>();
    public IReadOnlyList<string> Breakdown { get; set; } = Array.Empty<string>();

    public string CostText => $"{TotalCost:N0} АР";
    public string EnergyText => $"{EnergyConsumed:N0} / {EnergyProduced:N0} (потр./выр.)";
    public string ValidationText => IsValid ? "Проверка пройдена" : "Требует исправлений";
    public string OwnerText => string.IsNullOrWhiteSpace(OwnerLogin) ? "Текущий игрок" : OwnerLogin;
    public string AccessibleSummary =>
        $"{Name}. {ConfiguratorKindLabel}. {StatusLabel}. {ValidationText}. {CostText}.";
}

public static class AssetBlueprintPresentationParser
{
    public static IReadOnlyList<AssetBlueprintPresentation> ParseItems(
        IDictionary<string, object> payload,
        string key = "items")
    {
        return List(payload, key)
            .Select(Map)
            .Select(ParseItem)
            .ToList();
    }

    public static AssetBlueprintPresentation? ParseSingle(
        IDictionary<string, object> payload,
        string key = "item")
    {
        if (!payload.TryGetValue(key, out var raw) || raw == null)
            return null;
        return ParseItem(Map(raw));
    }

    private static AssetBlueprintPresentation ParseItem(IDictionary<string, object> map)
    {
        var calculation = NestedMap(map, "serverCalculation");
        var updated = DateTime.TryParse(
            Text(map, "updatedAtUtc"),
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var updatedUtc)
            ? updatedUtc.ToLocalTime().ToString("dd.MM.yyyy HH:mm")
            : "время не указано";

        var breakdown = List(calculation, "breakdown")
            .Select(Map)
            .Select(item =>
            {
                var label = Text(item, "label");
                var value = Text(item, "value");
                var unit = Text(item, "unit");
                return string.Join(" ", new[] { label, value, unit }.Where(part => !string.IsNullOrWhiteSpace(part)));
            })
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();

        return new AssetBlueprintPresentation
        {
            BlueprintId = Text(map, "blueprintId"),
            Name = Text(map, "name"),
            ConfiguratorKind = Text(map, "configuratorKind"),
            ConfiguratorKindLabel = Text(map, "configuratorKindLabel"),
            Status = Text(map, "status"),
            StatusLabel = Text(map, "statusLabel"),
            Visibility = Text(map, "visibility"),
            Revision = Integer(map, "revision"),
            ReadableSummary = Text(map, "readableSummary"),
            IsValid = Boolean(calculation, "isValid"),
            TotalCost = Long(calculation, "totalCost"),
            EnergyProduced = Integer(calculation, "energyProduced"),
            EnergyConsumed = Integer(calculation, "energyConsumed"),
            OwnerLogin = Text(map, "ownerLogin"),
            AdminGmNotes = Text(map, "adminGmNotes"),
            UpdatedText = updated,
            Configuration = NestedMap(map, "configuration"),
            Breakdown = breakdown
        };
    }

    private static Dictionary<string, object> NestedMap(IDictionary<string, object> map, string key)
    {
        if (!map.TryGetValue(key, out var raw) || raw == null)
            return new Dictionary<string, object>();
        return Map(raw);
    }

    private static Dictionary<string, object> Map(object raw)
    {
        if (raw is Dictionary<string, object> typed)
            return typed;
        if (raw is IDictionary dictionary)
        {
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry entry in dictionary)
            {
                var key = Convert.ToString(entry.Key);
                if (!string.IsNullOrWhiteSpace(key))
                    result[key] = entry.Value!;
            }
            return result;
        }
        return new Dictionary<string, object>();
    }

    private static IEnumerable<object> List(IDictionary<string, object> map, string key)
    {
        if (!map.TryGetValue(key, out var raw) || raw == null || raw is string)
            return Array.Empty<object>();
        return raw is IEnumerable enumerable ? enumerable.Cast<object>() : Array.Empty<object>();
    }

    private static string Text(IDictionary<string, object> map, string key) =>
        map.TryGetValue(key, out var raw) ? Convert.ToString(raw) ?? string.Empty : string.Empty;

    private static int Integer(IDictionary<string, object> map, string key) =>
        map.TryGetValue(key, out var raw) && int.TryParse(Convert.ToString(raw), out var value) ? value : 0;

    private static long Long(IDictionary<string, object> map, string key) =>
        map.TryGetValue(key, out var raw) && long.TryParse(Convert.ToString(raw), out var value) ? value : 0L;

    private static bool Boolean(IDictionary<string, object> map, string key) =>
        map.TryGetValue(key, out var raw) && bool.TryParse(Convert.ToString(raw), out var value) && value;
}
