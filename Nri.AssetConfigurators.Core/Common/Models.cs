using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Nri.AssetConfigurators.Core.Common;

public enum AssetConfiguratorMode
{
    Classic,
    NriSystemUsse
}

public enum AssetComponentCategory
{
    Engine,
    Reactor,
    Sensor,
    ForwardWeapon,
    TurretWeapon,
    CivilianModule,
    SpecialModule,
    HullModule,
    DefensiveWeapon,
    InternalModule
}

public enum ValidationSeverity
{
    Information,
    Warning,
    Error
}

public sealed class LegacySourceInfo
{
    public LegacySourceInfo(string repositoryUrl, string commitSha, string catalogVersion)
    {
        RepositoryUrl = repositoryUrl ?? string.Empty;
        CommitSha = commitSha ?? string.Empty;
        CatalogVersion = catalogVersion ?? string.Empty;
    }

    public string RepositoryUrl { get; }
    public string CommitSha { get; }
    public string CatalogVersion { get; }
}

public sealed class CatalogOption
{
    public CatalogOption(string key, string displayName, string category = "", string description = "")
    {
        Key = key ?? string.Empty;
        DisplayName = displayName ?? string.Empty;
        Category = category ?? string.Empty;
        Description = description ?? string.Empty;
    }

    public string Key { get; }
    public string DisplayName { get; }
    public string Category { get; }
    public string Description { get; }
    public override string ToString() => DisplayName;
}

public sealed class ComponentDefinition
{
    public ComponentDefinition(
        string key,
        string displayName,
        AssetComponentCategory category,
        long cost,
        int slotSize,
        int energy,
        string group = "",
        string description = "")
    {
        Key = key ?? string.Empty;
        DisplayName = displayName ?? string.Empty;
        Category = category;
        Cost = cost;
        SlotSize = Math.Max(0, slotSize);
        Energy = energy;
        Group = group ?? string.Empty;
        Description = description ?? string.Empty;
    }

    public string Key { get; }
    public string DisplayName { get; }
    public AssetComponentCategory Category { get; }
    public long Cost { get; }
    public int SlotSize { get; }
    public int Energy { get; }
    public string Group { get; }
    public string Description { get; }
    public override string ToString() => DisplayName;
}

public sealed class SelectedComponent
{
    public SelectedComponent(string componentKey, int quantity, AssetComponentCategory category)
    {
        ComponentKey = componentKey ?? string.Empty;
        Quantity = Math.Max(1, quantity);
        Category = category;
    }

    public string ComponentKey { get; }
    public int Quantity { get; }
    public AssetComponentCategory Category { get; }
}

public sealed class ValidationIssue
{
    public ValidationIssue(string code, string message, ValidationSeverity severity, string field = "")
    {
        Code = code ?? string.Empty;
        Message = message ?? string.Empty;
        Severity = severity;
        Field = field ?? string.Empty;
    }

    public string Code { get; }
    public string Message { get; }
    public ValidationSeverity Severity { get; }
    public string Field { get; }
}

public sealed class ValidationResult
{
    public ValidationResult(IEnumerable<ValidationIssue>? issues = null)
    {
        Issues = new ReadOnlyCollection<ValidationIssue>((issues ?? Enumerable.Empty<ValidationIssue>()).ToList());
    }

    public IReadOnlyList<ValidationIssue> Issues { get; }
    public bool IsValid => Issues.All(issue => issue.Severity != ValidationSeverity.Error);
}

public sealed class BreakdownRow
{
    public BreakdownRow(string key, string label, decimal value, string unit, string note = "")
    {
        Key = key ?? string.Empty;
        Label = label ?? string.Empty;
        Value = value;
        Unit = unit ?? string.Empty;
        Note = note ?? string.Empty;
    }

    public string Key { get; }
    public string Label { get; }
    public decimal Value { get; }
    public string Unit { get; }
    public string Note { get; }
}

public sealed class AssetWarning
{
    public AssetWarning(string code, string message)
    {
        Code = code ?? string.Empty;
        Message = message ?? string.Empty;
    }

    public string Code { get; }
    public string Message { get; }
}

public abstract class CalculationResult
{
    protected CalculationResult(
        ValidationResult validation,
        IEnumerable<BreakdownRow> breakdown,
        IEnumerable<AssetWarning> warnings,
        long totalCost,
        int energyProduced,
        int energyConsumed,
        string summary)
    {
        Validation = validation;
        Breakdown = new ReadOnlyCollection<BreakdownRow>(breakdown.ToList());
        Warnings = new ReadOnlyCollection<AssetWarning>(warnings.ToList());
        TotalCost = totalCost;
        EnergyProduced = energyProduced;
        EnergyConsumed = energyConsumed;
        Summary = summary ?? string.Empty;
    }

    public ValidationResult Validation { get; }
    public IReadOnlyList<BreakdownRow> Breakdown { get; }
    public IReadOnlyList<AssetWarning> Warnings { get; }
    public long TotalCost { get; }
    public int EnergyProduced { get; }
    public int EnergyConsumed { get; }
    public int EnergyRemaining => EnergyProduced - EnergyConsumed;
    public string Summary { get; }
}

public sealed class SnapshotComparison
{
    public SnapshotComparison(string baselineName, long costDelta, int energyDelta, IReadOnlyDictionary<string, decimal> metricDeltas)
    {
        BaselineName = baselineName ?? string.Empty;
        CostDelta = costDelta;
        EnergyDelta = energyDelta;
        MetricDeltas = metricDeltas ?? new ReadOnlyDictionary<string, decimal>(new Dictionary<string, decimal>());
    }

    public string BaselineName { get; }
    public long CostDelta { get; }
    public int EnergyDelta { get; }
    public IReadOnlyDictionary<string, decimal> MetricDeltas { get; }
}
