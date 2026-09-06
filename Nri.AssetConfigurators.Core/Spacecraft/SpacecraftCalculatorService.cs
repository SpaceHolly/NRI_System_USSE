using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using Nri.AssetConfigurators.Core.Common;
using Nri.AssetConfigurators.Core.LegacyCatalogs;

namespace Nri.AssetConfigurators.Core.Spacecraft;

public sealed class SpacecraftCalculatorService
{
    private static readonly string[] EngineTypes =
    {
        "Маневровый", "Космический", "Атмосферный", "Одноразовый"
    };

    public SpacecraftCalculationResult Calculate(SpacecraftInput input)
    {
        if (input == null)
            throw new ArgumentNullException(nameof(input));

        var issues = Validate(input);
        var warnings = CalculationHelpers.ModeWarnings(input.Mode);
        if (!issues.IsValid)
            return Empty(issues, warnings);

        var size = SpacecraftCatalog.Name(input.SizeKey);
        var quality = SpacecraftCatalog.Name(input.QualityKey);
        var shipClass = SpacecraftCatalog.Name(input.ClassKey);
        var reactorType = SpacecraftCatalog.Name(input.ReactorTypeKey);
        var reactorLevel = SpacecraftCatalog.Name(input.ReactorLevelKey);
        var qualityMultiplier = SpacecraftLegacySpecs.QualityCost[quality];
        var hullBase = SpacecraftLegacySpecs.HullBySize[size];

        var hull = hullBase.HP * qualityMultiplier;
        var armor = hullBase.AP * qualityMultiplier;
        var shields = hullBase.SP * qualityMultiplier;
        var barrier = HasAux(input, "Доп.щиты: Барьеры") ? hullBase.BP * qualityMultiplier : 0;

        if (SpacecraftLegacySpecs.APandSPMod.TryGetValue(shipClass, out var protection))
        {
            armor *= protection.AP;
            shields *= protection.SP;
            barrier *= protection.SP;
        }

        armor *= input.ArmorThicknessPercent / 100.0;
        if (HasAux(input, "Корпус из Бориформия"))
            hull *= 2;
        if (HasAux(input, "Броня из Сталиниума"))
            armor *= 2;
        if (HasAux(input, "Щиты из Хассатия-Б"))
        {
            shields *= 2;
            barrier *= 2;
        }

        if (CalculationHelpers.Has(input.Components, SpacecraftCatalog.Index, "Усилитель брони"))
            armor *= 2;
        if (CalculationHelpers.Has(input.Components, SpacecraftCatalog.Index, "Усилитель щита"))
        {
            shields *= 2;
            barrier *= 2;
        }

        var maneuverability = SpacecraftLegacySpecs.ManeuverabilityBySize[size] *
                              (SpacecraftLegacySpecs.ManeuverabilityMod.TryGetValue(shipClass, out var maneuverMod)
                                  ? maneuverMod
                                  : 1.0) *
                              qualityMultiplier;
        if (CalculationHelpers.Has(input.Components, SpacecraftCatalog.Index, "Система прод. маневрирования"))
            maneuverability *= 2;
        if (CalculationHelpers.Has(input.Components, SpacecraftCatalog.Index, "Ускоритель выхлопа"))
            maneuverability *= 2;

        var energyConsumed = input.Components.Sum(item =>
            SpacecraftCatalog.Index.RequireComponent(item.ComponentKey).Energy * item.Quantity);
        var reactor = SpacecraftLegacySpecs.ReactorBySize[size] +
                      (SpacecraftLegacySpecs.ReactorMod.TryGetValue(shipClass, out var reactorClassModifier)
                          ? reactorClassModifier
                          : 0);
        var levelMultiplier = SpacecraftLegacySpecs.EngineLvl.TryGetValue(reactorLevel, out var level)
            ? level
            : 1;
        reactor += (int)Math.Round(
            SpacecraftLegacySpecs.ReatorPowerAndCost[reactorType].RPower * (double)levelMultiplier);

        var civilianUsed = UsedSlots(input, AssetComponentCategory.CivilianModule);
        var specialUsed = UsedSlots(input, AssetComponentCategory.SpecialModule);
        var forwardUsed = UsedSlots(input, AssetComponentCategory.ForwardWeapon);
        var turretUsed = UsedSlots(input, AssetComponentCategory.TurretWeapon);
        var civilianAvailable = SpacecraftLegacySpecs.CivCellSize[size] +
                                (SpacecraftLegacySpecs.CivCellMod.TryGetValue(shipClass, out var civilianModifier)
                                    ? civilianModifier
                                    : 0);
        var specialAvailable = SpacecraftLegacySpecs.SpecCells.TryGetValue(shipClass, out var specialSlots)
            ? specialSlots
            : 0;
        var forwardAvailable = SpacecraftLegacySpecs.ForwardWeaponsBySize[size];
        var turretAvailable = SpacecraftLegacySpecs.TurretsBySize[size] +
                              (SpacecraftLegacySpecs.TurretWeapMod.TryGetValue(shipClass, out var turretModifier)
                                  ? turretModifier
                                  : 0);

        issues = AddCapacityIssue(issues, "civilian-slots", "Гражданские ячейки", civilianUsed, civilianAvailable);
        issues = AddCapacityIssue(issues, "special-slots", "Специальные ячейки", specialUsed, specialAvailable);
        issues = AddCapacityIssue(issues, "forward-slots", "Курсовое вооружение", forwardUsed, forwardAvailable);
        issues = AddCapacityIssue(issues, "turret-slots", "Турельное вооружение", turretUsed, turretAvailable);
        if (energyConsumed > reactor)
        {
            issues = Append(issues, new ValidationIssue(
                "energy-deficit",
                "Потребление энергии превышает выработку реактора.",
                ValidationSeverity.Error,
                "Reactor"));
        }

        var hullCostBase = SpacecraftLegacySpecs.ShipSizeCosts[size];
        var classCostMultiplier = SpacecraftLegacySpecs.ShipClassCostModifiers.TryGetValue(shipClass, out var classCost)
            ? classCost
            : 1.0;
        var thicknessCostMultiplier = input.ArmorThicknessPercent <= 100
            ? 0.5 + 0.5 * (input.ArmorThicknessPercent / 100.0)
            : 1 + (input.ArmorThicknessPercent - 100) * 0.0025;
        var hullCost = (long)Math.Round(hullCostBase * classCostMultiplier * thicknessCostMultiplier);
        var engineCost = input.Engines.Sum(engine =>
        {
            var engineLevel = SpacecraftCatalog.Name(engine.LevelKey);
            var multiplier = SpacecraftLegacySpecs.EngineLvl.TryGetValue(engineLevel, out var engineLevelValue)
                ? engineLevelValue
                : 1;
            return (long)Math.Round(hullCost * 0.10 * multiplier) * engine.Quantity;
        });
        var reactorCost = SpacecraftLegacySpecs.ReatorPowerAndCost[reactorType].RCost * (long)levelMultiplier;
        var auxiliaryCost = (long)Math.Round(hullCost * 0.10 * input.AuxiliaryHullModuleKeys.Count);
        var control = SpacecraftCatalog.Name(input.ControlSystemKey);
        var controlCost = SpacecraftLegacySpecs.ControlUnitCosts[control];
        var coreCost = (long)Math.Round(
            (hullCost + engineCost + reactorCost + auxiliaryCost + controlCost) * qualityMultiplier);

        long sensorCost = 0;
        if (SpacecraftLegacySpecs.SensorCostsBySize.TryGetValue(size, out var sensors))
        {
            sensorCost = input.SensorKeys
                .Select(SpacecraftCatalog.Name)
                .Where(sensors.ContainsKey)
                .Sum(sensor => (long)sensors[sensor]);
        }

        var componentCost = input.Components.Sum(item =>
            SpacecraftCatalog.Index.RequireComponent(item.ComponentKey).Cost * item.Quantity);
        var priceTier = SpacecraftCatalog.Name(input.PriceTierKey);
        var totalCost = (long)Math.Round(
            (coreCost + sensorCost + componentCost) * SpacecraftLegacySpecs.PriceAdd[priceTier]);

        var storage = CalculateStorage(size, shipClass, input.Components);
        var speeds = CalculateSpeeds(size, shipClass, input.Engines);
        var minimumCrew = CrewForSize(size);
        var breakdown = new[]
        {
            new BreakdownRow("hull", "Корпус и броня", hullCost, "АР"),
            new BreakdownRow("engines", "Двигатели", engineCost, "АР"),
            new BreakdownRow("reactor", "Реактор", reactorCost, "АР"),
            new BreakdownRow("auxiliary", "Модули корпуса", auxiliaryCost, "АР"),
            new BreakdownRow("control", "Система управления", controlCost, "АР"),
            new BreakdownRow("quality", "Блок после качества", coreCost, "АР"),
            new BreakdownRow("sensors", "Сенсоры", sensorCost, "АР"),
            new BreakdownRow("components", "Ячейки и вооружение", componentCost, "АР"),
            new BreakdownRow("total", "Итог с наценкой", totalCost, "АР", priceTier)
        };
        var summary = BuildSummary(
            input,
            size,
            shipClass,
            totalCost,
            reactor,
            energyConsumed,
            (int)Math.Round(hull),
            (int)Math.Round(armor),
            (int)Math.Round(shields));

        return new SpacecraftCalculationResult(
            issues,
            breakdown,
            warnings,
            totalCost,
            reactor,
            energyConsumed,
            summary,
            (int)Math.Round(hull),
            (int)Math.Round(armor),
            (int)Math.Round(shields),
            (int)Math.Round(barrier),
            (int)Math.Round(maneuverability),
            minimumCrew,
            civilianUsed,
            civilianAvailable,
            specialUsed,
            specialAvailable,
            forwardUsed,
            forwardAvailable,
            turretUsed,
            turretAvailable,
            new ReadOnlyDictionary<string, int>(speeds),
            new ReadOnlyDictionary<string, int>(storage));
    }

    private static ValidationResult Validate(SpacecraftInput input)
    {
        var issues = new List<ValidationIssue>();
        Required(input.ConfigurationName, "name", "Укажите название конфигурации.", issues);
        RequiredOption(input.SizeKey, "size", SpacecraftCatalog.Sizes, issues);
        RequiredOption(input.ClassKey, "class", SpacecraftCatalog.Classes, issues);
        RequiredOption(input.QualityKey, "quality", SpacecraftCatalog.Qualities, issues);
        RequiredOption(input.PriceTierKey, "price-tier", SpacecraftCatalog.PriceTiers, issues);
        RequiredOption(input.ControlSystemKey, "control", SpacecraftCatalog.ControlSystems, issues);
        RequiredOption(input.ReactorTypeKey, "reactor", SpacecraftCatalog.ReactorTypes, issues);
        RequiredOption(input.ReactorLevelKey, "reactor-level", SpacecraftCatalog.Levels, issues);

        if (input.ArmorThicknessPercent < 0 || input.ArmorThicknessPercent > 500)
        {
            issues.Add(new ValidationIssue(
                "armor-thickness",
                "Толщина брони должна быть от 0 до 500%.",
                ValidationSeverity.Error,
                "ArmorThicknessPercent"));
        }

        if (!string.IsNullOrWhiteSpace(input.SizeKey) &&
            !string.IsNullOrWhiteSpace(input.ClassKey) &&
            !SpacecraftCatalog.ClassesForSize(input.SizeKey).Any(item => item.Key == input.ClassKey))
        {
            issues.Add(new ValidationIssue(
                "class-size",
                "Выбранный класс недоступен для этого размера.",
                ValidationSeverity.Error,
                "ClassKey"));
        }

        if (input.Engines.Sum(item => item.Quantity) > 4)
        {
            issues.Add(new ValidationIssue(
                "engine-limit",
                "На корабль можно установить не более четырёх двигателей.",
                ValidationSeverity.Error,
                "Engines"));
        }

        return new ValidationResult(issues);
    }

    private static SpacecraftCalculationResult Empty(ValidationResult validation, IEnumerable<AssetWarning> warnings)
    {
        var empty = new ReadOnlyDictionary<string, int>(new Dictionary<string, int>());
        return new SpacecraftCalculationResult(
            validation,
            new BreakdownRow[0],
            warnings,
            0,
            0,
            0,
            "Расчёт недоступен: исправьте обязательные поля.",
            0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0,
            empty,
            empty);
    }

    private static int UsedSlots(SpacecraftInput input, AssetComponentCategory category)
    {
        return input.Components
            .Where(item => item.Category == category)
            .Sum(item => SpacecraftCatalog.Index.RequireComponent(item.ComponentKey).SlotSize * item.Quantity);
    }

    private static bool HasAux(SpacecraftInput input, string name)
    {
        return input.AuxiliaryHullModuleKeys.Any(key => SpacecraftCatalog.Name(key) == name);
    }

    private static Dictionary<string, int> CalculateStorage(
        string size,
        string shipClass,
        IEnumerable<SelectedComponent> selected)
    {
        var category = SizeCategory(size);
        var baseStorage = category == "C" ? 50 : SpacecraftLegacySpecs.StorageCapacitiesByType[category];
        var baseFuel = size == "C" ? 50 : SpacecraftLegacySpecs.StorageCapacitiesByType["Fuel"];
        var general = baseStorage * CalculationHelpers.BinaryTernaryCapacity(
            CalculationHelpers.QuantityOf(selected, SpacecraftCatalog.Index, "Склад общий"));
        var ammunition = baseStorage * CalculationHelpers.BinaryTernaryCapacity(
            CalculationHelpers.QuantityOf(selected, SpacecraftCatalog.Index, "Склад боеприпасов"));
        var medical = baseStorage * CalculationHelpers.BinaryTernaryCapacity(
            CalculationHelpers.QuantityOf(selected, SpacecraftCatalog.Index, "Склад медицины"));
        var fuel = baseFuel * CalculationHelpers.BinaryTernaryCapacity(
            CalculationHelpers.QuantityOf(selected, SpacecraftCatalog.Index, "Склад топлива"));

        if (SpacecraftLegacySpecs.ClassSpecificModifiers.TryGetValue(shipClass, out var modifiers))
        {
            if (modifiers.TryGetValue("Склад общий", out var generalModifier))
                general = (int)Math.Round(general * generalModifier);
            if (modifiers.TryGetValue("Склад боеприпасов", out var ammunitionModifier))
                ammunition = (int)Math.Round(ammunition * ammunitionModifier);
            if (modifiers.TryGetValue("Склад медицины", out var medicalModifier))
                medical = (int)Math.Round(medical * medicalModifier);
        }

        var builtIn = size == "C" ? 10 : 50;
        general += builtIn;
        fuel += builtIn;

        return new Dictionary<string, int>
        {
            ["Общий склад"] = general,
            ["Топливный склад"] = fuel,
            ["Оружейный склад"] = ammunition,
            ["Медицинский склад"] = medical,
            ["Ангар"] = CalculationHelpers.BinaryTernaryCapacity(
                CalculationHelpers.QuantityOf(selected, SpacecraftCatalog.Index, "Ангар общего назначения")),
            ["Ангар для челноков"] = CalculationHelpers.BinaryTernaryCapacity(
                CalculationHelpers.QuantityOf(selected, SpacecraftCatalog.Index, "Ангар для челноков"))
        };
    }

    private static Dictionary<string, int> CalculateSpeeds(
        string size,
        string shipClass,
        IEnumerable<SpacecraftEngineSelection> engines)
    {
        var result = new Dictionary<string, int>();
        var order = new[] { "C", "SSS", "SS", "S", "M", "L", "VL", "A", "X", "XL", "XXL", "XXXL", "E", "XE" };
        var baseValue = 100 - Array.IndexOf(order, size) * 7;

        foreach (var engineType in EngineTypes)
        {
            var selected = engines
                .Where(item => SpacecraftCatalog.Name(item.TypeKey) == engineType)
                .SelectMany(item => Enumerable.Repeat(item, item.Quantity))
                .Take(4)
                .ToList();
            if (selected.Count == 0 || baseValue <= 0)
            {
                result[engineType] = 0;
                continue;
            }

            var highest = selected
                .Select(item => SpacecraftLegacySpecs.EngineLvl[SpacecraftCatalog.Name(item.LevelKey)])
                .Max();
            var sizeMultiplier = SpacecraftLegacySpecs.EngineSizeModifiers[
                SpacecraftCatalog.Name(selected[selected.Count - 1].SizeKey)];
            var typeMultiplier = SpacecraftLegacySpecs.EngineTypeModifiers[engineType];
            var classMultiplier = SpacecraftLegacySpecs.SpeedMod.TryGetValue(shipClass, out var speed)
                ? speed
                : 1.0;

            result[engineType] = (int)Math.Round(
                baseValue * highest * selected.Count * sizeMultiplier * typeMultiplier * classMultiplier);
        }

        return result;
    }

    private static int CrewForSize(string size)
    {
        switch (size)
        {
            case "C": return 1;
            case "SSS":
            case "SS": return 2;
            case "S":
            case "M": return 3;
            case "L":
            case "XL":
            case "VL": return 4;
            case "A": return 5;
            case "X":
            case "XXL":
            case "XXXL": return 7;
            case "E":
            case "XE": return 10;
            default: return 1;
        }
    }

    private static string SizeCategory(string size)
    {
        switch (size)
        {
            case "C": return "C";
            case "SSS":
            case "SS":
            case "S": return "Light";
            case "M":
            case "L":
            case "VL":
            case "A": return "Medium";
            case "X":
            case "XL":
            case "XXL":
            case "XXXL": return "Heavy";
            case "E":
            case "XE": return "SuperHeavy";
            default: return "Medium";
        }
    }

    private static string BuildSummary(
        SpacecraftInput input,
        string size,
        string shipClass,
        long cost,
        int energyProduced,
        int energyConsumed,
        int hull,
        int armor,
        int shields)
    {
        var text = new StringBuilder();
        text.AppendLine(input.ConfigurationName);
        text.AppendLine("Космический корабль/станция: " + size + ", " + shipClass);
        text.AppendLine("Корпус/броня/щиты: " + hull + "/" + armor + "/" + shields);
        text.AppendLine("Энергия: " + energyConsumed + "/" + energyProduced + " (потр./выр.)");
        text.Append("Стоимость: " + cost + " АР");
        return text.ToString();
    }

    private static void Required(
        string value,
        string field,
        string message,
        ICollection<ValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(value))
            issues.Add(new ValidationIssue("required", message, ValidationSeverity.Error, field));
    }

    private static void RequiredOption(
        string key,
        string field,
        IEnumerable<CatalogOption> options,
        ICollection<ValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(key) || options.All(item => item.Key != key))
            issues.Add(new ValidationIssue("required-option", "Выберите значение из каталога.", ValidationSeverity.Error, field));
    }

    private static ValidationResult AddCapacityIssue(
        ValidationResult current,
        string code,
        string label,
        int used,
        int available)
    {
        if (used <= available)
            return current;

        return Append(current, new ValidationIssue(
            code,
            label + ": занято " + used + ", доступно " + available + ".",
            ValidationSeverity.Error,
            code));
    }

    private static ValidationResult Append(ValidationResult current, ValidationIssue issue)
    {
        return new ValidationResult(current.Issues.Concat(new[] { issue }));
    }
}
