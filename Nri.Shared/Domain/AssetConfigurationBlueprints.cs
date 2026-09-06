using System;
using System.Collections.Generic;

namespace Nri.Shared.Domain;

public static class AssetConfiguratorKindIds
{
    public const string Spacecraft = "spacecraft";
    public const string LandMarine = "land_marine";
    public const string Building = "building";
}

public static class AssetBlueprintStatusIds
{
    public const string Draft = "draft";
    public const string Ready = "ready";
    public const string Archived = "archived";
}

public static class AssetBlueprintVisibilityIds
{
    public const string Private = "private";
    public const string Shared = "shared";
}

public sealed class AssetConfigurationBlueprintState : EntityBase
{
    public string OwnerUserId { get; set; } = string.Empty;
    public string OwnerLoginSnapshot { get; set; } = string.Empty;
    public string OwnerCharacterId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ConfiguratorKind { get; set; } = AssetConfiguratorKindIds.Spacecraft;
    public string CalculationMode { get; set; } = "classic";
    public string CatalogSource { get; set; } = string.Empty;
    public string CatalogVersion { get; set; } = string.Empty;
    public string CatalogCommitSha { get; set; } = string.Empty;
    public AssetBlueprintConfigurationState Configuration { get; set; } = new AssetBlueprintConfigurationState();
    public AssetBlueprintCalculationState ServerCalculation { get; set; } = new AssetBlueprintCalculationState();
    public string ReadableSummary { get; set; } = string.Empty;
    public string Status { get; set; } = AssetBlueprintStatusIds.Draft;
    public string Visibility { get; set; } = AssetBlueprintVisibilityIds.Private;
    public int Revision { get; set; } = 1;
    public string ClientOperationId { get; set; } = string.Empty;
    public string AdminGmNotes { get; set; } = string.Empty;
    public string LastCalculatedBy { get; set; } = "server";
}

public sealed class AssetBlueprintConfigurationState
{
    public string Kind { get; set; } = AssetConfiguratorKindIds.Spacecraft;
    public SpacecraftBlueprintConfigurationState? Spacecraft { get; set; }
    public LandMarineBlueprintConfigurationState? LandMarine { get; set; }
    public BuildingBlueprintConfigurationState? Building { get; set; }
}

public sealed class AssetBlueprintComponentState
{
    public string ComponentKey { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public string Category { get; set; } = string.Empty;
}

public sealed class SpacecraftBlueprintEngineState
{
    public string TypeKey { get; set; } = string.Empty;
    public string SizeKey { get; set; } = string.Empty;
    public string LevelKey { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
}

public sealed class SpacecraftBlueprintConfigurationState
{
    public string ConfigurationName { get; set; } = string.Empty;
    public string Mode { get; set; } = "classic";
    public string SizeKey { get; set; } = string.Empty;
    public string ClassKey { get; set; } = string.Empty;
    public string QualityKey { get; set; } = string.Empty;
    public string PriceTierKey { get; set; } = string.Empty;
    public string ControlSystemKey { get; set; } = string.Empty;
    public string ReactorTypeKey { get; set; } = string.Empty;
    public string ReactorLevelKey { get; set; } = string.Empty;
    public int ArmorThicknessPercent { get; set; } = 100;
    public List<SpacecraftBlueprintEngineState> Engines { get; set; } = new List<SpacecraftBlueprintEngineState>();
    public List<string> SensorKeys { get; set; } = new List<string>();
    public List<string> AuxiliaryHullModuleKeys { get; set; } = new List<string>();
    public List<AssetBlueprintComponentState> Components { get; set; } = new List<AssetBlueprintComponentState>();
}

public sealed class LandMarineBlueprintConfigurationState
{
    public string ConfigurationName { get; set; } = string.Empty;
    public string Mode { get; set; } = "classic";
    public string TypeKey { get; set; } = string.Empty;
    public string SizeKey { get; set; } = string.Empty;
    public string ClassKey { get; set; } = string.Empty;
    public string QualityKey { get; set; } = string.Empty;
    public string LandEngineKey { get; set; } = string.Empty;
    public string LandEngineLevelKey { get; set; } = string.Empty;
    public string WaterEngineKey { get; set; } = string.Empty;
    public string WaterEngineLevelKey { get; set; } = string.Empty;
    public string ReactorTypeKey { get; set; } = string.Empty;
    public string ReactorLevelKey { get; set; } = string.Empty;
    public string PilotSystemKey { get; set; } = string.Empty;
    public string PriceTierKey { get; set; } = string.Empty;
    public int ArmorThicknessPercent { get; set; } = 100;
    public List<string> SensorKeys { get; set; } = new List<string>();
    public List<string> AuxiliaryHullModuleKeys { get; set; } = new List<string>();
    public List<AssetBlueprintComponentState> Components { get; set; } = new List<AssetBlueprintComponentState>();
}

public sealed class BuildingBlueprintConfigurationState
{
    public string ConfigurationName { get; set; } = string.Empty;
    public string Mode { get; set; } = "classic";
    public string BuildingTypeKey { get; set; } = string.Empty;
    public string FloorSizeKey { get; set; } = string.Empty;
    public int FloorCount { get; set; } = 1;
    public string ConstructionMethodKey { get; set; } = string.Empty;
    public string HullMaterialKey { get; set; } = string.Empty;
    public string ArmorMaterialKey { get; set; } = string.Empty;
    public string ShieldMaterialKey { get; set; } = string.Empty;
    public string QualityKey { get; set; } = string.Empty;
    public string ReactorTypeKey { get; set; } = string.Empty;
    public string ReactorLevelKey { get; set; } = string.Empty;
    public string LocationDescription { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public List<AssetBlueprintComponentState> Components { get; set; } = new List<AssetBlueprintComponentState>();
}

public sealed class AssetBlueprintCalculationState
{
    public bool IsValid { get; set; }
    public long TotalCost { get; set; }
    public int EnergyProduced { get; set; }
    public int EnergyConsumed { get; set; }
    public string Summary { get; set; } = string.Empty;
    public List<AssetBlueprintMetricState> Metrics { get; set; } = new List<AssetBlueprintMetricState>();
    public List<AssetBlueprintBreakdownState> Breakdown { get; set; } = new List<AssetBlueprintBreakdownState>();
    public List<AssetBlueprintValidationState> Validation { get; set; } = new List<AssetBlueprintValidationState>();
    public List<string> Warnings { get; set; } = new List<string>();
    public DateTime CalculatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class AssetBlueprintMetricState
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public string Unit { get; set; } = string.Empty;
}

public sealed class AssetBlueprintBreakdownState
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
}

public sealed class AssetBlueprintValidationState
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Field { get; set; } = string.Empty;
}
