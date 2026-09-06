using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Nri.AssetConfigurators.Core.Common;

namespace Nri.AssetConfigurators.Core.Comparison;

public static class SnapshotComparer
{
    public static SnapshotComparison Compare(
        string baselineName,
        CalculationResult baseline,
        CalculationResult current,
        IReadOnlyDictionary<string, decimal> baselineMetrics,
        IReadOnlyDictionary<string, decimal> currentMetrics)
    {
        var deltas = new Dictionary<string, decimal>(StringComparer.Ordinal);
        foreach (var metric in currentMetrics)
        {
            baselineMetrics.TryGetValue(metric.Key, out var previous);
            deltas[metric.Key] = metric.Value - previous;
        }

        return new SnapshotComparison(
            baselineName,
            current.TotalCost - baseline.TotalCost,
            current.EnergyRemaining - baseline.EnergyRemaining,
            new ReadOnlyDictionary<string, decimal>(deltas));
    }
}
