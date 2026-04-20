using System;
using System.Collections;
using System.Collections.Generic;

namespace Nri.Shared.Utilities;

public static class PayloadReader
{
    public static string? GetString(IDictionary<string, object> payload, string key)
    {
        if (!payload.ContainsKey(key) || payload[key] == null) return null;
        return Convert.ToString(payload[key]);
    }

    public static int? GetInt(IDictionary<string, object> payload, string key)
    {
        if (!payload.ContainsKey(key) || payload[key] == null) return null;
        int value;
        return int.TryParse(Convert.ToString(payload[key]), out value) ? value : (int?)null;
    }

    public static long? GetLong(IDictionary<string, object> payload, string key)
    {
        if (!payload.ContainsKey(key) || payload[key] == null) return null;
        long value;
        return long.TryParse(Convert.ToString(payload[key]), out value) ? value : (long?)null;
    }


    public static double? GetDouble(IDictionary<string, object> payload, string key)
    {
        if (!payload.ContainsKey(key) || payload[key] == null) return null;
        double value;
        return double.TryParse(Convert.ToString(payload[key]), out value) ? value : (double?)null;
    }

    public static bool GetBool(IDictionary<string, object> payload, string key)
    {
        if (!payload.ContainsKey(key) || payload[key] == null) return false;
        bool value;
        return bool.TryParse(Convert.ToString(payload[key]), out value) && value;
    }

    public static Dictionary<string, object>? GetDictionary(IDictionary<string, object> payload, string key)
    {
        if (!payload.ContainsKey(key) || payload[key] == null) return null;
        if (payload[key] is Dictionary<string, object> typed) return typed;
        if (payload[key] is IDictionary dictionary)
        {
            var result = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (DictionaryEntry entry in dictionary)
            {
                var mapKey = Convert.ToString(entry.Key);
                if (string.IsNullOrWhiteSpace(mapKey)) continue;
                result[mapKey] = entry.Value!;
            }

            return result;
        }

        if (payload[key] is IEnumerable enumerable && payload[key] is not string)
        {
            var result = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var item in enumerable)
            {
                if (item == null) continue;

                if (item is DictionaryEntry entry)
                {
                    var pairKey = Convert.ToString(entry.Key);
                    if (string.IsNullOrWhiteSpace(pairKey)) continue;
                    result[pairKey] = entry.Value!;
                    continue;
                }

                if (item is object[] arrayPair && arrayPair.Length == 2)
                {
                    var pairKey = Convert.ToString(arrayPair[0]);
                    if (string.IsNullOrWhiteSpace(pairKey)) continue;
                    result[pairKey] = arrayPair[1]!;
                    continue;
                }

                if (item is IList listPair && listPair.Count == 2)
                {
                    var pairKey = Convert.ToString(listPair[0]);
                    if (string.IsNullOrWhiteSpace(pairKey)) continue;
                    result[pairKey] = listPair[1]!;
                    continue;
                }

                var valueType = item.GetType();
                var keyProperty = valueType.GetProperty("Key");
                var valueProperty = valueType.GetProperty("Value");
                if (keyProperty == null || valueProperty == null) continue;
                var reflectedKey = Convert.ToString(keyProperty.GetValue(item));
                if (string.IsNullOrWhiteSpace(reflectedKey)) continue;
                result[reflectedKey] = valueProperty.GetValue(item)!;
            }

            return result.Count > 0 ? result : null;
        }

        return null;
    }

    public static IList<object>? GetList(IDictionary<string, object> payload, string key)
    {
        if (!payload.ContainsKey(key) || payload[key] == null) return null;

        var list = payload[key] as IList<object>;
        if (list != null) return list;

        var arrayList = payload[key] as ArrayList;
        if (arrayList == null) return null;

        var result = new List<object>();
        foreach (var item in arrayList) result.Add(item!);
        return result;
    }
}
