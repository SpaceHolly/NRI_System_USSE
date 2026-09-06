using System;
using System.Collections;
using System.Collections.Generic;

namespace Nri.AdminClient.ViewModels;

internal sealed class CanonicalBlueprintDraft0187
{
    public string ProfileId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string PublicDescription { get; set; } = string.Empty;
    public string GMDescription { get; set; } = string.Empty;
    public string VisibilityRule { get; set; } = "gm_only";
    public int ResolvedComponentCount { get; set; }
    public int UnresolvedComponentCount { get; set; }
    public Dictionary<string, object> CustomFields { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

internal static class CanonicalBlueprintDraftTransfer0187
{
    private static readonly object Sync = new();
    private static CanonicalBlueprintDraft0187? _pending;

    public static void Store(IDictionary<string, object> payload)
    {
        var draft = AsMap(payload.TryGetValue("draft", out var value) ? value : null);
        var customFields = AsMap(draft.TryGetValue("customFields", out var custom) ? custom : null);
        var item = new CanonicalBlueprintDraft0187
        {
            ProfileId = Text(payload, "profileId"),
            Name = Text(draft, "name"),
            DisplayName = Text(draft, "displayName"),
            PublicDescription = Text(draft, "publicDescription"),
            GMDescription = Text(draft, "gmDescription"),
            VisibilityRule = First(Text(draft, "visibilityRule"), "gm_only"),
            ResolvedComponentCount = Number(payload, "resolvedComponentCount"),
            UnresolvedComponentCount = Number(payload, "unresolvedComponentCount"),
            CustomFields = customFields
        };
        lock (Sync) _pending = item;
    }

    public static CanonicalBlueprintDraft0187? Take()
    {
        lock (Sync)
        {
            var value = _pending;
            _pending = null;
            return value;
        }
    }

    private static Dictionary<string, object> AsMap(object? value)
    {
        if (value is Dictionary<string, object> typed)
            return new Dictionary<string, object>(typed, StringComparer.OrdinalIgnoreCase);
        var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        if (value is IDictionary dictionary)
            foreach (DictionaryEntry pair in dictionary)
            {
                var key = Convert.ToString(pair.Key);
                if (!string.IsNullOrWhiteSpace(key)) result[key] = pair.Value ?? string.Empty;
            }
        return result;
    }

    private static string Text(IDictionary<string, object> map, string key)
        => map.TryGetValue(key, out var value) ? Convert.ToString(value) ?? string.Empty : string.Empty;

    private static int Number(IDictionary<string, object> map, string key)
        => int.TryParse(Text(map, key), out var value) ? value : 0;

    private static string First(params string[] values)
    {
        foreach (var value in values)
            if (!string.IsNullOrWhiteSpace(value)) return value;
        return string.Empty;
    }
}
