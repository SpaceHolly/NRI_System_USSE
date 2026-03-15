using System;
using System.Collections.Generic;

namespace Nri.Shared.Utilities;

public static class PayloadReader
{
    public static string? GetString(IDictionary<string, object> payload, string key)
    {
        if (!payload.ContainsKey(key) || payload[key] == null)
        {
            return null;
        }

        return Convert.ToString(payload[key]);
    }

    public static int? GetInt(IDictionary<string, object> payload, string key)
    {
        if (!payload.ContainsKey(key) || payload[key] == null)
        {
            return null;
        }

        int value;
        return int.TryParse(Convert.ToString(payload[key]), out value) ? value : (int?)null;
    }
}
