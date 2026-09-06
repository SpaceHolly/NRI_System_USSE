using System;
using System.Globalization;
using System.Text;

namespace Nri.AssetConfigurators.Core.Common;

public static class LegacyKey
{
    public static string Create(string scope, string group, string displayName)
    {
        var source = string.Join("|", scope ?? string.Empty, group ?? string.Empty, displayName ?? string.Empty);
        uint hash = 2166136261;

        foreach (var value in Encoding.UTF8.GetBytes(source))
        {
            hash ^= value;
            hash *= 16777619;
        }

        return string.Concat(
            Slug(scope ?? string.Empty),
            ".",
            Slug(group ?? string.Empty),
            ".",
            hash.ToString("x8", CultureInfo.InvariantCulture));
    }

    private static string Slug(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "legacy";

        var builder = new StringBuilder(value.Length);
        foreach (var character in value.Trim().ToLowerInvariant())
        {
            if ((character >= 'a' && character <= 'z') ||
                (character >= '0' && character <= '9'))
            {
                builder.Append(character);
            }
            else if (builder.Length > 0 && builder[builder.Length - 1] != '-')
            {
                builder.Append('-');
            }
        }

        return builder.ToString().Trim('-');
    }
}
