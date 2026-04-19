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
