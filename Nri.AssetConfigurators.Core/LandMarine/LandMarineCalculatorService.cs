using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using Nri.AssetConfigurators.Core.Common;
using Nri.AssetConfigurators.Core.LegacyCatalogs;

namespace Nri.AssetConfigurators.Core.LandMarine;

public sealed class LandMarineCalculatorService
{
    private static readonly string[] SizeOrder = { "C", "S", "M", "L", "VL", "A", "X", "XL", "XXL" };

    public LandMarineCalculationResult Calculate(LandMarineInput input)
    {
        if (input == null)
            throw new ArgumentNullException(nameof(input));

        var validation = Validate(input);
        var warnings = CalculationHelpers.ModeWarnings(input.Mode);
        if (!validation.IsValid)
            return Empty(validation, warnings);

        var type = LandMarineCatalog.Name(input.TypeKey);
        var size = LandMarineCatalog.Name(input.SizeKey);
        var vehicleClass = LandMarineCatalog.Name(input.ClassKey);
        var quality = LandMarineCatalog.Name(input.QualityKey);
        var qualityMultiplier = LandMarineLegacySpecs.QualityCost[quality];
        var classMultipliers = LandMarineLegacySpecs.ClassMultipliers[vehicleClass];
        var armorThickness = Math.Max(0, Math.Min(1500, input.ArmorThicknessPercent));
        var armorThicknessMultiplier = armorThickness / 100.0;
        var armorManeuverPenalty = 2.0 - armorThickness / 1500.0 * 1.9;

        var hull = (int)Math.Round(LandMarineLegacySpecs.HullBySize[size] * qualityMultiplier);
        if (HasAux(input, "Корпус из Бориформия"))
            hull *= 2;

        var armor = LandMarineLegacySpecs.ArmorSize[size] *
                    qualityMultiplier *
                    classMultipliers.ArmorMod *
                    armorThicknessMultiplier;
        if (HasAux(input, "Броня из Сталиниума"))
            armor *= 2;
        if (CalculationHelpers.Has(input.Components, LandMarineCatalog.Index, "Усилитель брони"))
            armor *= 2;

        var shields = LandMarineLegacySpecs.ShieldSize[size] *
                      qualityMultiplier *
                      classMultipliers.ShieldMod;
        if (HasAux(input, "Щиты из Хассатия-Б"))
            shields *= 2;
        if (CalculationHelpers.Has(input.Components, LandMarineCatalog.Index, "Усилитель щита"))
            shields *= 2;

        var landEngine = LandMarineCatalog.Name(input.LandEngineKey);
        var waterEngine = LandMarineCatalog.Name(input.WaterEngineKey);
        var landLevel = LandMarineCatalog.Name(input.LandEngineLevelKey);
        var waterLevel = LandMarineCatalog.Name(input.WaterEngineLevelKey);
        var baseManeuverability = LandMarineLegacySpecs.ManeuverabilityBySize[size];
        var landManeuverability = Maneuverability(
            baseManeuverability,
            classMultipliers.ManeuverabilityMod,
            qualityMultiplier,
            armorManeuverPenalty,
            landEngine,
            landLevel);
        var waterManeuverability = Maneuverability(
            baseManeuverability,
            classMultipliers.ManeuverabilityMod,
            qualityMultiplier,
            armorManeuverPenalty,
            waterEngine,
            waterLevel);
        if (CalculationHelpers.Has(input.Components, LandMarineCatalog.Index, "Система Прод. Маневрирования"))
        {
            landManeuverability *= 2;
            waterManeuverability *= 2;
        }

        var sizeRank = vehicleClass == "Экраноплан" ? 1 : Array.IndexOf(SizeOrder, size) + 1;
        if (sizeRank <= 0)
            sizeRank = 1;
        var landSpeed = Speed(30.0, sizeRank, landEngine, landLevel, armorManeuverPenalty);
        var waterSpeed = Speed(10.0, sizeRank, waterEngine, waterLevel, armorManeuverPenalty);
        var underwaterSpeed = Speed(5.0, sizeRank, waterEngine, waterLevel, armorManeuverPenalty);
        if (CalculationHelpers.Has(input.Components, LandMarineCatalog.Index, "Ускоритель выхлопа"))
        {
            landSpeed *= 2;
            waterSpeed *= 2;
            underwaterSpeed *= 2;
        }

        var reactorType = LandMarineCatalog.Name(input.ReactorTypeKey);
        var reactorLevel = LandMarineCatalog.Name(input.ReactorLevelKey);
        var reactorLevelMultiplier = LandMarineLegacySpecs.LevelMultiplier[reactorLevel];
        var energyProduced = (int)Math.Round(
            LandMarineLegacySpecs.ReatorPowerAndCost[reactorType].RPower *
            LandMarineLegacySpecs.ReactorPowerMod[size] *
            classMultipliers.ReactorMod *
            reactorLevelMultiplier);
        var energyConsumed = input.Components.Sum(item =>
            LandMarineCatalog.Index.RequireComponent(item.ComponentKey).Energy * item.Quantity);

        var capacities = LandMarineLegacySpecs.ClassCapacity[vehicleClass];
        var civilianUsed = UsedSlots(input, AssetComponentCategory.CivilianModule);
        var specialUsed = UsedSlots(input, AssetComponentCategory.SpecialModule);
        var forwardUsed = UsedSlots(input, AssetComponentCategory.ForwardWeapon);
        var turretUsed = UsedSlots(input, AssetComponentCategory.TurretWeapon);
        validation = Capacity(validation, "civilian-slots", "Гражданские ячейки", civilianUsed, capacities.CivCellsCap);
        validation = Capacity(validation, "special-slots", "Специальные ячейки", specialUsed, capacities.SpecialCellsCap);
        validation = Capacity(validation, "forward-slots", "Курсовое вооружение", forwardUsed, capacities.FrontWeapCap);
        validation = Capacity(validation, "turret-slots", "Турельное вооружение", turretUsed, capacities.TurretWeapCap);
        if (energyConsumed > energyProduced)
        {
            validation = Append(validation, new ValidationIssue(
                "energy-deficit",
                "Потребление энергии превышает выработку реактора.",
                ValidationSeverity.Error,
                "Reactor"));
        }

        var sizeCost = LandMarineLegacySpecs.SizeCost[size];
        var classCostMultiplier = LandMarineLegacySpecs.ClassCost[vehicleClass];
        var baseCost = sizeCost * (1 + classCostMultiplier);
        var armorCostMultiplier = 0.75 + armorThickness / 1500.0 * 1.25;
        var auxiliaryCost = input.AuxiliaryHullModuleKeys.Count(key =>
                                IsExpensiveAux(LandMarineCatalog.Name(key))) *
                            baseCost *
                            0.3;
        var landEngineCost = EngineCost(baseCost, landEngine, landLevel);
        var waterEngineCost = EngineCost(baseCost, waterEngine, waterLevel);
        var reactorCost = LandMarineLegacySpecs.ReatorPowerAndCost[reactorType].RCost * reactorLevelMultiplier;
        var pilot = LandMarineCatalog.Name(input.PilotSystemKey);
        var pilotCost = LandMarineLegacySpecs.PilotSystemCost[pilot];
        var coreCost = baseCost * armorCostMultiplier +
                       auxiliaryCost +
                       landEngineCost +
                       waterEngineCost +
                       reactorCost +
                       pilotCost;
        var modifiedCoreCost = coreCost * qualityMultiplier;
        var componentCost = input.Components.Sum(item =>
            LandMarineCatalog.Index.RequireComponent(item.ComponentKey).Cost * item.Quantity);

        long sensorCost = 0;
        if (LandMarineLegacySpecs.SensorCostBySize.TryGetValue(size, out var sensorCatalog))
        {
            sensorCost = input.SensorKeys
                .Select(LandMarineCatalog.Name)
                .Where(sensorCatalog.ContainsKey)
                .Sum(item => (long)sensorCatalog[item]);
        }

        var priceTier = LandMarineCatalog.Name(input.PriceTierKey);
        var totalCost = (long)Math.Round(
            (modifiedCoreCost + componentCost + sensorCost) *
            LandMarineLegacySpecs.AdditionalCost[priceTier]);

        var storage = Storage(size, input.Components);
        var minimumCrew = Crew(size);
        var breakdown = new[]
        {
            new BreakdownRow("base", "Корпус и класс", (decimal)(baseCost * armorCostMultiplier), "АР"),
            new BreakdownRow("auxiliary", "Модули корпуса", (decimal)auxiliaryCost, "АР"),
            new BreakdownRow("engines", "Двигатели", (decimal)(landEngineCost + waterEngineCost), "АР"),
            new BreakdownRow("reactor", "Реактор", reactorCost, "АР"),
            new BreakdownRow("pilot", "Система пилотирования", pilotCost, "АР"),
            new BreakdownRow("quality", "Блок после качества", (decimal)modifiedCoreCost, "АР"),
            new BreakdownRow("components", "Ячейки и вооружение", componentCost, "АР"),
            new BreakdownRow("sensors", "Сенсоры", sensorCost, "АР"),
            new BreakdownRow("total", "Итог с наценкой", totalCost, "АР", priceTier)
        };
        var summary = BuildSummary(
            input,
            type,
            vehicleClass,
            totalCost,
            hull,
            (int)Math.Round(armor),
            (int)Math.Round(shields),
            energyProduced,
            energyConsumed);

        return new LandMarineCalculationResult(
            validation,
            breakdown,
            warnings,
            totalCost,
            energyProduced,
            energyConsumed,
            summary,
            hull,
            (int)Math.Round(armor),
            (int)Math.Round(shields),
            (int)Math.Round(landManeuverability),
            (int)Math.Round(waterManeuverability),
            SupportsLand(type) ? (int)Math.Round(landSpeed) : 0,
            SupportsWater(type) ? (int)Math.Round(waterSpeed) : 0,
            type == "Подводный" ? (int)Math.Round(underwaterSpeed) : 0,
            minimumCrew,
            civilianUsed,
            capacities.CivCellsCap,
            specialUsed,
            capacities.SpecialCellsCap,
            forwardUsed,
            capacities.FrontWeapCap,
            turretUsed,
            capacities.TurretWeapCap,
            new ReadOnlyDictionary<string, int>(storage));
    }

    private static ValidationResult Validate(LandMarineInput input)
    {
        var issues = new List<ValidationIssue>();
        Required(input.ConfigurationName, "name", "Укажите название конфигурации.", issues);
        Option(input.TypeKey, "type", LandMarineCatalog.Types, issues);
        Option(input.SizeKey, "size", LandMarineCatalog.Sizes, issues);
        Option(input.ClassKey, "class", LandMarineCatalog.Classes, issues);
        Option(input.QualityKey, "quality", LandMarineCatalog.Qualities, issues);
        Option(input.ReactorTypeKey, "reactor", LandMarineCatalog.ReactorTypes, issues);
        Option(input.ReactorLevelKey, "reactor-level", LandMarineCatalog.Levels, issues);
        Option(input.PilotSystemKey, "pilot", LandMarineCatalog.PilotSystems, issues);
        Option(input.PriceTierKey, "price-tier", LandMarineCatalog.PriceTiers, issues);

        if (input.ArmorThicknessPercent < 0 || input.ArmorThicknessPercent > 1500)
        {
            issues.Add(new ValidationIssue(
                "armor-thickness",
                "Толщина брони должна быть от 0 до 1500%.",
                ValidationSeverity.Error,
                "ArmorThicknessPercent"));
        }

        if (!string.IsNullOrWhiteSpace(input.TypeKey) &&
            !string.IsNullOrWhiteSpace(input.ClassKey) &&
            !LandMarineCatalog.ClassesForType(input.TypeKey).Any(item => item.Key == input.ClassKey))
        {
            issues.Add(new ValidationIssue(
                "class-type",
                "Выбранный класс недоступен для этого типа техники.",
                ValidationSeverity.Error,
                "ClassKey"));
        }

        return new ValidationResult(issues);
    }

    private static LandMarineCalculationResult Empty(
        ValidationResult validation,
        IEnumerable<AssetWarning> warnings)
    {
        return new LandMarineCalculationResult(
            validation,
            new BreakdownRow[0],
            warnings,
            0, 0, 0,
            "Расчёт недоступен: исправьте обязательные поля.",
            0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0,
            new ReadOnlyDictionary<string, int>(new Dictionary<string, int>()));
    }

    private static double Maneuverability(
        int baseValue,
        double classMultiplier,
        double qualityMultiplier,
        double armorPenalty,
        string engine,
        string level)
    {
        if (string.IsNullOrWhiteSpace(engine) ||
            !LandMarineLegacySpecs.EngineManeuverabilityAndSpeedMultiplier.TryGetValue(engine, out var engineData))
            return 0;

        return baseValue *
               classMultiplier *
               qualityMultiplier *
               armorPenalty *
               engineData.ManeuverabilityBaseMod *
               ManeuverLevel(level);
    }

    private static double Speed(
        double baseValue,
        int sizeRank,
        string engine,
        string level,
        double armorPenalty)
    {
        if (string.IsNullOrWhiteSpace(engine) ||
            !LandMarineLegacySpecs.EngineManeuverabilityAndSpeedMultiplier.TryGetValue(engine, out var engineData))
            return 0;

        return baseValue / sizeRank * SpeedLevel(level) * engineData.SpeedBaseMod * armorPenalty;
    }

    private static int ManeuverLevel(string level)
    {
        switch (level)
        {
            case "1 Уровень": return 1;
            case "2 Уровень": return 2;
            case "3 Уровень": return 3;
            case "4 Уровень": return 6;
            default: return 1;
        }
    }

    private static int SpeedLevel(string level)
    {
        switch (level)
        {
            case "1 Уровень": return 1;
            case "2 Уровень": return 2;
            case "3 Уровень": return 4;
            case "4 Уровень": return 6;
            default: return 1;
        }
    }

    private static double EngineCost(double baseCost, string engine, string level)
    {
        if (string.IsNullOrWhiteSpace(engine))
            return 0;
        return baseCost / 10 *
               LandMarineLegacySpecs.EngineCostMultiplier[engine] *
               LandMarineLegacySpecs.LevelMultiplier[level];
    }

    private static Dictionary<string, int> Storage(string size, IEnumerable<SelectedComponent> selected)
    {
        var defaultBonus = size == "C" ? 5 : 30;
        var storageSize = LandMarineLegacySpecs.StorageCapacitiesBySize[size];
        var generalCount = CalculationHelpers.QuantityOf(selected, LandMarineCatalog.Index, "Склад общий");
        var ammunitionCount = CalculationHelpers.QuantityOf(selected, LandMarineCatalog.Index, "Склад боеприпасов");
        var medicalCount = CalculationHelpers.QuantityOf(selected, LandMarineCatalog.Index, "Склад медицины");
        var fuelCount = CalculationHelpers.QuantityOf(selected, LandMarineCatalog.Index, "Склад топлива");
        var hangarCount = CalculationHelpers.QuantityOf(selected, LandMarineCatalog.Index, "Ангар общего назначения");

        return new Dictionary<string, int>
        {
            ["Общий склад"] = generalCount > 0
                ? CalculationHelpers.BinaryTernaryCapacity(generalCount) * storageSize + defaultBonus
                : 0,
            ["Оружейный склад"] = ammunitionCount > 0
                ? CalculationHelpers.BinaryTernaryCapacity(ammunitionCount) * storageSize
                : 0,
            ["Медицинский склад"] = medicalCount > 0
                ? CalculationHelpers.BinaryTernaryCapacity(medicalCount) * storageSize
                : 0,
            ["Топливный склад"] = fuelCount > 0
                ? CalculationHelpers.BinaryTernaryCapacity(fuelCount) * (size == "C" ? 10 : 30) + defaultBonus
                : 0,
            ["Ангар"] = hangarCount > 0
                ? CalculationHelpers.BinaryTernaryCapacity(hangarCount)
                : 0
        };
    }

    private static int UsedSlots(LandMarineInput input, AssetComponentCategory category)
    {
        return input.Components
            .Where(item => item.Category == category)
            .Sum(item => LandMarineCatalog.Index.RequireComponent(item.ComponentKey).SlotSize * item.Quantity);
    }

    private static bool HasAux(LandMarineInput input, string name)
    {
        return input.AuxiliaryHullModuleKeys.Any(key => LandMarineCatalog.Name(key) == name);
    }

    private static bool IsExpensiveAux(string name)
    {
        return name == "Корпус из Бориформия" ||
               name == "Броня из Сталиниума" ||
               name == "Щиты из Хассатия-Б";
    }

    private static bool SupportsLand(string type) => type == "Наземный" || type == "Гибрид";
    private static bool SupportsWater(string type) =>
        type == "Водный" || type == "Гибрид" || type == "Подводный";

    private static int Crew(string size)
    {
        switch (size)
        {
            case "C": return 1;
            case "S": return 2;
            case "M": return 3;
            case "L":
            case "VL": return 4;
            case "A": return 5;
            case "X":
            case "XL":
            case "XXL": return 7;
            default: return 1;
        }
    }

    private static string BuildSummary(
        LandMarineInput input,
        string type,
        string vehicleClass,
        long cost,
        int hull,
        int armor,
        int shields,
        int energyProduced,
        int energyConsumed)
    {
        var text = new StringBuilder();
        text.AppendLine(input.ConfigurationName);
        text.AppendLine(type + ": " + vehicleClass);
        text.AppendLine("Корпус/броня/щиты: " + hull + "/" + armor + "/" + shields);
        text.AppendLine("Энергия: " + energyConsumed + "/" + energyProduced + " (потр./выр.)");
        text.Append("Стоимость: " + cost + " АР");
        return text.ToString();
    }

    private static ValidationResult Capacity(
        ValidationResult current,
        string code,
        string label,
        int used,
        int available)
    {
        return used <= available
            ? current
            : Append(current, new ValidationIssue(
                code,
                label + ": занято " + used + ", доступно " + available + ".",
                ValidationSeverity.Error,
                code));
    }

    private static ValidationResult Append(ValidationResult current, ValidationIssue issue) =>
        new ValidationResult(current.Issues.Concat(new[] { issue }));

    private static void Required(
        string value,
        string field,
        string message,
        ICollection<ValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(value))
            issues.Add(new ValidationIssue("required", message, ValidationSeverity.Error, field));
    }

    private static void Option(
        string key,
        string field,
        IEnumerable<CatalogOption> options,
        ICollection<ValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(key) || options.All(item => item.Key != key))
            issues.Add(new ValidationIssue("required-option", "Выберите значение из каталога.", ValidationSeverity.Error, field));
    }
}
