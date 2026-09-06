using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Nri.AssetConfigurators.Core.Common;

namespace Nri.AssetConfigurators.Core.Spacecraft;

public sealed class SpacecraftEngineSelection
{
    public SpacecraftEngineSelection(string typeKey, string sizeKey, string levelKey, int quantity = 1)
    {
        TypeKey = typeKey ?? string.Empty;
        SizeKey = sizeKey ?? string.Empty;
        LevelKey = levelKey ?? string.Empty;
        Quantity = Math.Max(1, quantity);
    }

    public string TypeKey { get; }
    public string SizeKey { get; }
    public string LevelKey { get; }
    public int Quantity { get; }
}

public sealed class SpacecraftInput
{
    public SpacecraftInput()
    {
        ConfigurationName = "Новый корабль";
        Mode = AssetConfiguratorMode.Classic;
        Engines = new List<SpacecraftEngineSelection>();
        SensorKeys = new List<string>();
        AuxiliaryHullModuleKeys = new List<string>();
        Components = new List<SelectedComponent>();
        ArmorThicknessPercent = 100;
    }

    public string ConfigurationName { get; set; }
    public AssetConfiguratorMode Mode { get; set; }
    public string SizeKey { get; set; } = string.Empty;
    public string ClassKey { get; set; } = string.Empty;
    public string QualityKey { get; set; } = string.Empty;
    public string PriceTierKey { get; set; } = string.Empty;
    public string ControlSystemKey { get; set; } = string.Empty;
    public string ReactorTypeKey { get; set; } = string.Empty;
    public string ReactorLevelKey { get; set; } = string.Empty;
    public int ArmorThicknessPercent { get; set; }
    public IList<SpacecraftEngineSelection> Engines { get; }
    public IList<string> SensorKeys { get; }
    public IList<string> AuxiliaryHullModuleKeys { get; }
    public IList<SelectedComponent> Components { get; }
}

public sealed class SpacecraftCalculationResult : CalculationResult
{
    public SpacecraftCalculationResult(
        ValidationResult validation,
        IEnumerable<BreakdownRow> breakdown,
        IEnumerable<AssetWarning> warnings,
        long totalCost,
        int energyProduced,
        int energyConsumed,
        string summary,
        int hull,
        int armor,
        int shields,
        int barrier,
        int maneuverability,
        int minimumCrew,
        int civilianSlotsUsed,
        int civilianSlotsAvailable,
        int specialSlotsUsed,
        int specialSlotsAvailable,
        int forwardWeaponSlotsUsed,
        int forwardWeaponSlotsAvailable,
        int turretWeaponSlotsUsed,
        int turretWeaponSlotsAvailable,
        IReadOnlyDictionary<string, int> speeds,
        IReadOnlyDictionary<string, int> storage)
        : base(validation, breakdown, warnings, totalCost, energyProduced, energyConsumed, summary)
    {
        Hull = hull;
        Armor = armor;
        Shields = shields;
        Barrier = barrier;
        Maneuverability = maneuverability;
        MinimumCrew = minimumCrew;
        CivilianSlotsUsed = civilianSlotsUsed;
        CivilianSlotsAvailable = civilianSlotsAvailable;
        SpecialSlotsUsed = specialSlotsUsed;
        SpecialSlotsAvailable = specialSlotsAvailable;
        ForwardWeaponSlotsUsed = forwardWeaponSlotsUsed;
        ForwardWeaponSlotsAvailable = forwardWeaponSlotsAvailable;
        TurretWeaponSlotsUsed = turretWeaponSlotsUsed;
        TurretWeaponSlotsAvailable = turretWeaponSlotsAvailable;
        Speeds = speeds;
        Storage = storage;
    }

    public int Hull { get; }
    public int Armor { get; }
    public int Shields { get; }
    public int Barrier { get; }
    public int Maneuverability { get; }
    public int MinimumCrew { get; }
    public int CivilianSlotsUsed { get; }
    public int CivilianSlotsAvailable { get; }
    public int SpecialSlotsUsed { get; }
    public int SpecialSlotsAvailable { get; }
    public int ForwardWeaponSlotsUsed { get; }
    public int ForwardWeaponSlotsAvailable { get; }
    public int TurretWeaponSlotsUsed { get; }
    public int TurretWeaponSlotsAvailable { get; }
    public IReadOnlyDictionary<string, int> Speeds { get; }
    public IReadOnlyDictionary<string, int> Storage { get; }

    public IReadOnlyDictionary<string, decimal> Metrics()
    {
        return new ReadOnlyDictionary<string, decimal>(new Dictionary<string, decimal>
        {
            ["hull"] = Hull,
            ["armor"] = Armor,
            ["shields"] = Shields,
            ["barrier"] = Barrier,
            ["maneuverability"] = Maneuverability
        });
    }
}
