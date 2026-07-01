using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Nri.AdminClient.Converters;

public sealed class AdminNavigationIconPathConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var key = System.Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        return Geometry.Parse(PathFor(key));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;

    private static string PathFor(string key)
    {
        switch ((key ?? string.Empty).Trim().ToUpperInvariant())
        {
            case "D":
                return "M3,3 L21,3 L21,21 L3,21 Z M6,7 L18,7 M6,12 L14,12 M6,17 L16,17";
            case "U":
                return "M8,7 A4,4 0 1 0 16,7 A4,4 0 1 0 8,7 M5,21 C5,16 19,16 19,21";
            case "C":
                return "M12,3 L20,7 L20,17 L12,21 L4,17 L4,7 Z M8,9 L16,9 M8,13 L16,13 M8,17 L13,17";
            case "R":
                return "M5,4 L17,4 L21,8 L21,20 L5,20 Z M17,4 L17,8 L21,8 M8,10 L18,10 M8,14 L16,14";
            case "I":
                return "M7,4 L17,4 L17,20 L7,20 Z M10,7 L14,7 M10,12 L14,12 M10,17 L14,17";
            case "K":
                return "M4,20 L20,4 M6,6 L18,18 M5,4 L9,8 M15,16 L19,20";
            case "A":
                return "M12,3 L20,21 L16,21 L14,16 L10,16 L8,21 L4,21 Z M10.8,13 L13.2,13";
            case "W":
                return "M12,3 A9,9 0 1 0 12,21 A9,9 0 1 0 12,3 M3,12 L21,12 M12,3 C9,7 9,17 12,21 M12,3 C15,7 15,17 12,21";
            case "F":
                return "M5,4 L19,4 L19,8 L9,8 L9,12 L17,12 L17,16 L9,16 L9,21 L5,21 Z";
            case "E":
                return "M4,6 L20,6 M4,12 L20,12 M4,18 L20,18 M8,3 L8,21 M16,3 L16,21";
            case "H":
                return "M5,4 L19,4 L19,20 L5,20 Z M8,8 L16,8 M8,12 L16,12 M8,16 L13,16";
            case "S":
                return "M12,4 L20,8 L20,16 L12,20 L4,16 L4,8 Z M8,12 L16,12";
            case "G":
                return "M5,7 A3,3 0 1 0 11,7 A3,3 0 1 0 5,7 M13,7 A3,3 0 1 0 19,7 A3,3 0 1 0 13,7 M4,20 C4,15 12,15 12,20 M12,20 C12,15 20,15 20,20";
            case "B":
                return "M5,5 L19,19 M19,5 L5,19 M8,4 L4,8 M16,4 L20,8";
            case "Q":
                return "M4,5 L20,5 L20,16 L9,16 L4,21 Z";
            case "M":
                return "M4,5 L10,3 L16,5 L22,3 L22,19 L16,21 L10,19 L4,21 Z M10,3 L10,19 M16,5 L16,21";
            case "J":
                return "M7,4 L17,4 L17,17 C17,20 14,21 12,19 M12,19 C10,21 7,20 7,17 M7,8 L17,8 M7,12 L17,12";
            case "!":
                return "M12,3 L21,20 L3,20 Z M12,8 L12,14 M12,17 L12.1,17";
            case "N":
                return "M5,4 L19,4 L19,20 L5,20 Z M8,8 L16,8 M8,12 L16,12 M8,16 L13,16";
            case "L":
                return "M5,4 L9,4 L9,20 L5,20 Z M11,4 L19,4 L19,8 L11,8 Z M11,10 L19,10 L19,20 L11,20 Z";
            case "T":
                return "M4,6 L20,6 M12,6 L12,20 M8,20 L16,20";
            default:
                return "M12,3 A9,9 0 1 0 12,21 A9,9 0 1 0 12,3 M8,12 L16,12";
        }
    }
}
