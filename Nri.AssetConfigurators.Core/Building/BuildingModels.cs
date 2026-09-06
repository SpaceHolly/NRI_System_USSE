using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Nri.AssetConfigurators.Core.Common;

namespace Nri.AssetConfigurators.Core.Building;

public sealed class BuildingInput
{
    public BuildingInput()
    {
        ConfigurationName = "Новое здание";
        Mode = AssetConfiguratorMode.Classic;
        FloorCount = 1;
        Components = new List<SelectedComponent>();
    }

    public string ConfigurationName { get; set; }
    public AssetConfiguratorMode Mode { get; set; }
    public string BuildingTypeKey { get; set; } = string.Empty;
    public string FloorSizeKey { get; set; } = string.Empty;
    public int FloorCount { get; set; }
    public string ConstructionMethodKey { get; set; } = string.Empty;
    public string HullMaterialKey { get; set; } = string.Empty;
    public string ArmorMaterialKey { get; set; } = string.Empty;
    public string ShieldMaterialKey { get; set; } = string.Empty;
    public string QualityKey { get; set; } = string.Empty;
    public string ReactorTypeKey { get; set; } = string.Empty;
    public string ReactorLevelKey { get; set; } = string.Empty;
    public string LocationDescription { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public string GmComment { get; set; } = string.Empty;
    public IList<SelectedComponent> Components { get; }
}

public sealed class BuildingCalculationResult : CalculationResult
{
    public BuildingCalculationResult(
        ValidationResult validation,
        IEnumerable<BreakdownRow> breakdown,
        IEnumerable<AssetWarning> warnings,
        long totalCost,
        int energyProduced,
        int energyConsumed,
        string summary,
        int floorArea,
        int totalArea,
        int structuralIntegrity,
        int armorIntegrity,
        int shieldIntegrity,
        int internalSlotsUsed,
        int internalSlotsAvailable,
        int weaponSlotsUsed,
        int weaponSlotsAvailable,
        IReadOnlyDictionary<string, decimal> requiredResources,
        IReadOnlyDictionary<string, int> storage)
        : base(validation, breakdown, warnings, totalCost, energyProduced, energyConsumed, summary)
    {
        FloorArea = floorArea;
        TotalArea = totalArea;
        StructuralIntegrity = structuralIntegrity;
        ArmorIntegrity = armorIntegrity;
        ShieldIntegrity = shieldIntegrity;
        InternalSlotsUsed = internalSlotsUsed;
        InternalSlotsAvailable = internalSlotsAvailable;
        WeaponSlotsUsed = weaponSlotsUsed;
        WeaponSlotsAvailable = weaponSlotsAvailable;
        RequiredResources = requiredResources;
        Storage = storage;
    }

    public int FloorArea { get; }
    public int TotalArea { get; }
    public int StructuralIntegrity { get; }
    public int ArmorIntegrity { get; }
    public int ShieldIntegrity { get; }
    public int InternalSlotsUsed { get; }
    public int InternalSlotsAvailable { get; }
    public int WeaponSlotsUsed { get; }
    public int WeaponSlotsAvailable { get; }
    public IReadOnlyDictionary<string, decimal> RequiredResources { get; }
    public IReadOnlyDictionary<string, int> Storage { get; }

    public IReadOnlyDictionary<string, decimal> Metrics()
    {
        return new ReadOnlyDictionary<string, decimal>(new Dictionary<string, decimal>
        {
            ["totalArea"] = TotalArea,
            ["structuralIntegrity"] = StructuralIntegrity,
            ["armorIntegrity"] = ArmorIntegrity,
            ["shieldIntegrity"] = ShieldIntegrity
        });
    }
}
