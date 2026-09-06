using System;
using System.Collections.Generic;
using System.Linq;

namespace Nri.AssetConfigurators.Core.Common;

internal static class CalculationHelpers
{
    public static int BinaryTernaryCapacity(int count)
    {
        var result = 0;
        var power = 0;
        while (count > 0)
        {
            if ((count & 1) == 1)
                result += (int)Math.Pow(3, power);
            count >>= 1;
            power++;
        }

        return result;
    }

    public static int QuantityOf(
        IEnumerable<SelectedComponent> selected,
        LegacyCatalogIndex index,
        string displayName)
    {
        return selected
            .Where(item => string.Equals(index.DisplayName(item.ComponentKey), displayName, StringComparison.Ordinal))
            .Sum(item => item.Quantity);
    }

    public static bool Has(
        IEnumerable<SelectedComponent> selected,
        LegacyCatalogIndex index,
        string displayName)
    {
        return QuantityOf(selected, index, displayName) > 0;
    }

    public static List<AssetWarning> ModeWarnings(AssetConfiguratorMode mode)
    {
        var warnings = new List<AssetWarning>();
        if (mode == AssetConfiguratorMode.NriSystemUsse)
        {
            warnings.Add(new AssetWarning(
                "classic-catalog",
                "Для этого компонента пока используется классический каталог."));
        }

        return warnings;
    }
}
