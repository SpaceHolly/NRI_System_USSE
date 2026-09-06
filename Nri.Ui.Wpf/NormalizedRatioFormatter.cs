using System;
using System.Globalization;

namespace Nri.Ui.Wpf;

public static class NormalizedRatioFormatter
{
    public static string Format(object? normalizedValue)
        => Format(ToDecimal(normalizedValue), CultureInfo.CurrentCulture);

    public static string Format(decimal normalizedValue)
        => Format(normalizedValue, CultureInfo.CurrentCulture);

    public static string Format(decimal normalizedValue, CultureInfo? culture)
    {
        culture ??= CultureInfo.CurrentCulture;
        var semanticPercent = normalizedValue * 100m;
        return semanticPercent.ToString("0.#", culture) + "%";
    }

    public static string Format(double normalizedValue)
    {
        if (double.IsNaN(normalizedValue) || double.IsInfinity(normalizedValue)) return "—";
        return Format(Convert.ToDecimal(normalizedValue, CultureInfo.InvariantCulture));
    }

    public static decimal ToDecimal(object? value)
    {
        if (value == null) return 0m;
        if (value is decimal decimalValue) return decimalValue;
        if (value is double doubleValue && !double.IsNaN(doubleValue) && !double.IsInfinity(doubleValue)) return Convert.ToDecimal(doubleValue, CultureInfo.InvariantCulture);
        if (value is float floatValue && !float.IsNaN(floatValue) && !float.IsInfinity(floatValue)) return Convert.ToDecimal(floatValue, CultureInfo.InvariantCulture);
        if (value is byte or sbyte or short or ushort or int or uint or long or ulong) return Convert.ToDecimal(value, CultureInfo.InvariantCulture);

        var text = Convert.ToString(value, CultureInfo.CurrentCulture) ?? string.Empty;
        if (decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var invariantValue)) return invariantValue;
        return decimal.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out var currentValue) ? currentValue : 0m;
    }
}
