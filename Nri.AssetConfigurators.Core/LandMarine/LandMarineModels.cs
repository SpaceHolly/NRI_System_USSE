using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Nri.AssetConfigurators.Core.Common;

namespace Nri.AssetConfigurators.Core.LandMarine;

public sealed class LandMarineInput
{
    public LandMarineInput()
    {
        ConfigurationName = "Новая техника";
        Mode = AssetConfiguratorMode.Classic;
        ArmorThicknessPercent = 100;
        SensorKeys = new List<string>();
        AuxiliaryHullModuleKeys = new List<string>();
        Components = new List<SelectedComponent>();
    }

    public string ConfigurationName { get; set; }
    public AssetConfiguratorMode Mode { get; set; }
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
    public int ArmorThicknessPercent { get; set; }
    public IList<string> SensorKeys { get; }
    public IList<string> AuxiliaryHullModuleKeys { get; }
    public IList<SelectedComponent> Components { get; }
}

public sealed class LandMarineCalculationResult : CalculationResult
{
    public LandMarineCalculationResult(
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
        int landManeuverability,
        int waterManeuverability,
        int landSpeed,
        int waterSpeed,
        int underwaterSpeed,
        int minimumCrew,
        int civilianSlotsUsed,
        int civilianSlotsAvailable,
        int specialSlotsUsed,
        int specialSlotsAvailable,
        int forwardWeaponSlotsUsed,
        int forwardWeaponSlotsAvailable,
        int turretWeaponSlotsUsed,
        int turretWeaponSlotsAvailable,
        IReadOnlyDictionary<string, int> storage)
        : base(validation, breakdown, warnings, totalCost, energyProduced, energyConsumed, summary)
    {
        Hull = hull;
        Armor = armor;
        Shields = shields;
        LandManeuverability = landManeuverability;
        WaterManeuverability = waterManeuverability;
        LandSpeed = landSpeed;
        WaterSpeed = waterSpeed;
        UnderwaterSpeed = underwaterSpeed;
        MinimumCrew = minimumCrew;
        CivilianSlotsUsed = civilianSlotsUsed;
        CivilianSlotsAvailable = civilianSlotsAvailable;
        SpecialSlotsUsed = specialSlotsUsed;
        SpecialSlotsAvailable = specialSlotsAvailable;
        ForwardWeaponSlotsUsed = forwardWeaponSlotsUsed;
        ForwardWeaponSlotsAvailable = forwardWeaponSlotsAvailable;
        TurretWeaponSlotsUsed = turretWeaponSlotsUsed;
        TurretWeaponSlotsAvailable = turretWeaponSlotsAvailable;
        Storage = storage;
    }

    public int Hull { get; }
    public int Armor { get; }
    public int Shields { get; }
    public int LandManeuverability { get; }
    public int WaterManeuverability { get; }
    public int LandSpeed { get; }
    public int WaterSpeed { get; }
    public int UnderwaterSpeed { get; }
    public int MinimumCrew { get; }
    public int CivilianSlotsUsed { get; }
    public int CivilianSlotsAvailable { get; }
    public int SpecialSlotsUsed { get; }
    public int SpecialSlotsAvailable { get; }
    public int ForwardWeaponSlotsUsed { get; }
    public int ForwardWeaponSlotsAvailable { get; }
    public int TurretWeaponSlotsUsed { get; }
    public int TurretWeaponSlotsAvailable { get; }
    public IReadOnlyDictionary<string, int> Storage { get; }

    public IReadOnlyDictionary<string, decimal> Metrics()
    {
        return new ReadOnlyDictionary<string, decimal>(new Dictionary<string, decimal>
        {
            ["hull"] = Hull,
            ["armor"] = Armor,
            ["shields"] = Shields,
            ["landManeuverability"] = LandManeuverability,
            ["waterManeuverability"] = WaterManeuverability,
            ["landSpeed"] = LandSpeed,
            ["waterSpeed"] = WaterSpeed,
            ["underwaterSpeed"] = UnderwaterSpeed
        });
    }
}
