using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using Nri.AssetConfigurators.Core.Common;
using Nri.AssetConfigurators.Core.LegacyCatalogs;

namespace Nri.AssetConfigurators.Core.Building;

public sealed class BuildingCalculatorService
{
    public BuildingCalculationResult Calculate(BuildingInput input)
    {
        if (input == null)
            throw new ArgumentNullException(nameof(input));

        var validation = Validate(input);
        var warnings = CalculationHelpers.ModeWarnings(input.Mode);
        if (!validation.IsValid)
            return Empty(validation, warnings);

        var type = BuildingCatalog.Name(input.BuildingTypeKey);
        var floorSizeName = BuildingCatalog.Name(input.FloorSizeKey);
        var method = BuildingCatalog.Name(input.ConstructionMethodKey);
        var hullMaterial = BuildingCatalog.Name(input.HullMaterialKey);
        var armorMaterial = BuildingCatalog.Name(input.ArmorMaterialKey);
        var shieldMaterial = BuildingCatalog.Name(input.ShieldMaterialKey);
        var quality = BuildingCatalog.Name(input.QualityKey);
        var reactorType = BuildingCatalog.Name(input.ReactorTypeKey);
        var reactorLevel = BuildingCatalog.Name(input.ReactorLevelKey);

        var floorArea = BuildingLegacySpecs.FloorSize[floorSizeName];
        var qualityMultiplier = BuildingLegacySpecs.QualityCost[quality];
        var structuralIntegrity = (int)(
            floorArea *
            BuildingLegacySpecs.HPResourcesTypes[hullMaterial] *
            qualityMultiplier *
            input.FloorCount);
        var armorIntegrity = (int)(
            floorArea *
            BuildingLegacySpecs.APResourcesTypes[armorMaterial] *
            qualityMultiplier *
            input.FloorCount);
        var floorIndex = BuildingLegacySpecs.FloorSize.Keys.ToList().IndexOf(floorSizeName) + 1;
        var shieldIntegrity = (int)(
            floorIndex *
            BuildingLegacySpecs.SPResourcesTypes[shieldMaterial] *
            qualityMultiplier *
            input.FloorCount);

        var energyProduced = BuildingLegacySpecs.ReatorPowerAndCost[reactorType].RPower *
                             BuildingLegacySpecs.LevelMultiplier[reactorLevel];
        var energyConsumed = input.Components.Sum(item =>
            BuildingCatalog.Index.RequireComponent(item.ComponentKey).Energy * item.Quantity);

        var internalSlotsUsed = input.Components
            .Where(item => item.Category == AssetComponentCategory.InternalModule)
            .Sum(item => item.Quantity);
        var internalSlotsAvailable = input.FloorCount;
        var weaponSlotsUsed = input.Components
            .Where(item => item.Category == AssetComponentCategory.DefensiveWeapon)
            .Sum(item => BuildingCatalog.Index.RequireComponent(item.ComponentKey).SlotSize * item.Quantity);
        var weaponSlotsAvailable = BuildingLegacySpecs.BuildingSizeWeaponsCount[floorSizeName];
        validation = Capacity(validation, "internal-slots", "Внутренние модули", internalSlotsUsed, internalSlotsAvailable);
        validation = Capacity(validation, "weapon-slots", "Оборонительное вооружение", weaponSlotsUsed, weaponSlotsAvailable);
        if (energyConsumed > energyProduced)
        {
            validation = Append(validation, new ValidationIssue(
                "energy-deficit",
                "Потребление энергии превышает выработку реактора.",
                ValidationSeverity.Error,
                "Reactor"));
        }

        var side = Math.Sqrt(floorArea);
        var perimeter = 4 * side;
        var wallArea = perimeter * BuildingLegacySpecs.FloorHeight;
        var slabArea = floorArea;
        var isStandardHull = hullMaterial == "Ст.металлы" || hullMaterial == "Станд.металлы";
        var structuralVolume = (
            wallArea * BuildingLegacySpecs.Thickness["structural"] +
            2 * slabArea * BuildingLegacySpecs.Thickness["structural"]) *
            input.FloorCount;
        var armorVolume = !isStandardHull && armorMaterial != "Нет"
            ? wallArea * BuildingLegacySpecs.Thickness["armor"] * input.FloorCount
            : 0;
        var shieldVolume = !isStandardHull && shieldMaterial != "Нет"
            ? wallArea * BuildingLegacySpecs.Thickness["shield"] * input.FloorCount
            : 0;

        var typeCostMultiplier = 1.0;
        var inertVolume = 0.0;
        switch (type)
        {
            case "Бункер":
                typeCostMultiplier = 2.0;
                break;
            case "Надводное":
                typeCostMultiplier = 1.5;
                inertVolume = structuralVolume + armorVolume + shieldVolume;
                break;
            case "Подводное":
                typeCostMultiplier = 2.5;
                break;
            case "Атмосферное":
                typeCostMultiplier = 3.0;
                inertVolume = (structuralVolume + armorVolume + shieldVolume) * 2;
                break;
            case "Космическое":
                typeCostMultiplier = 1.1;
                break;
        }

        var canonicalHullMaterial = hullMaterial == "Ст.металлы" ? "Станд.металлы" : hullMaterial;
        var hullCost = structuralVolume * 1000 * BuildingLegacySpecs.ResourcesCost[canonicalHullMaterial];
        var armorCost = armorVolume > 0
            ? armorVolume * 1000 * BuildingLegacySpecs.ResourcesCost[armorMaterial]
            : 0;
        var shieldCost = shieldVolume > 0
            ? shieldVolume * 1000 * BuildingLegacySpecs.ResourcesCost[shieldMaterial]
            : 0;
        var inertCost = inertVolume > 0
            ? inertVolume * 1000 * BuildingLegacySpecs.ResourcesCost["Инерт.газы"]
            : 0;
        var totalResourceCost = (hullCost + armorCost + shieldCost + inertCost) * typeCostMultiplier;
        var reactorCost = BuildingLegacySpecs.ReatorPowerAndCost[reactorType].RCost *
                          BuildingLegacySpecs.LevelMultiplier[reactorLevel] *
                          qualityMultiplier;
        var componentCost = input.Components.Sum(item =>
            BuildingCatalog.Index.RequireComponent(item.ComponentKey).Cost * item.Quantity);

        var resourceCost = 0.0;
        var laborCost = 0.0;
        switch (method)
        {
            case "Собств.силами":
                resourceCost = 0;
                laborCost = 0;
                break;
            case "Найм строител.":
                laborCost = (totalResourceCost + reactorCost) * 0.25;
                break;
            case "Подрядчики":
                resourceCost = (totalResourceCost + reactorCost) * 1.25;
                break;
        }

        var resourceCostAr = resourceCost / BuildingLegacySpecs.UsdPerAr;
        var laborCostAr = laborCost / BuildingLegacySpecs.UsdPerAr;
        var totalCost = (long)Math.Round(resourceCostAr + laborCostAr + reactorCost + componentCost);
        var resources = new Dictionary<string, decimal>
        {
            [hullMaterial] = (decimal)(structuralVolume * 1000)
        };
        if (armorVolume > 0)
            resources[armorMaterial] = (decimal)(armorVolume * 1000);
        if (shieldVolume > 0)
            resources[shieldMaterial] = (decimal)(shieldVolume * 1000);
        if (inertVolume > 0)
            resources["Инерт.газы"] = (decimal)(inertVolume * 1000);

        var storage = Storage(floorArea, input.Components);
        var breakdown = new[]
        {
            new BreakdownRow("resources", "Материалы по типу здания", (decimal)totalResourceCost, "USD"),
            new BreakdownRow("resource-purchase", "Закупка ресурсов", (decimal)resourceCostAr, "АР", method),
            new BreakdownRow("labor", "Работы", (decimal)laborCostAr, "АР", method),
            new BreakdownRow("reactor", "Реактор", (decimal)reactorCost, "АР"),
            new BreakdownRow("components", "Модули и вооружение", componentCost, "АР"),
            new BreakdownRow("total", "Итог", totalCost, "АР")
        };
        var summary = BuildSummary(
            input,
            type,
            floorArea,
            totalCost,
            structuralIntegrity,
            armorIntegrity,
            shieldIntegrity,
            energyProduced,
            energyConsumed);

        return new BuildingCalculationResult(
            validation,
            breakdown,
            warnings,
            totalCost,
            energyProduced,
            energyConsumed,
            summary,
            floorArea,
            floorArea * input.FloorCount,
            structuralIntegrity,
            armorIntegrity,
            shieldIntegrity,
            internalSlotsUsed,
            internalSlotsAvailable,
            weaponSlotsUsed,
            weaponSlotsAvailable,
            new ReadOnlyDictionary<string, decimal>(resources),
            new ReadOnlyDictionary<string, int>(storage));
    }

    private static ValidationResult Validate(BuildingInput input)
    {
        var issues = new List<ValidationIssue>();
        Required(input.ConfigurationName, "name", "Укажите название конфигурации.", issues);
        Option(input.BuildingTypeKey, "type", BuildingCatalog.BuildingTypes, issues);
        Option(input.FloorSizeKey, "floor-size", BuildingCatalog.FloorSizes, issues);
        Option(input.ConstructionMethodKey, "method", BuildingCatalog.ConstructionMethods, issues);
        Option(input.HullMaterialKey, "hull", BuildingCatalog.HullMaterials, issues);
        Option(input.ArmorMaterialKey, "armor", BuildingCatalog.ArmorMaterials, issues);
        Option(input.ShieldMaterialKey, "shield", BuildingCatalog.ShieldMaterials, issues);
        Option(input.QualityKey, "quality", BuildingCatalog.Qualities, issues);
        Option(input.ReactorTypeKey, "reactor", BuildingCatalog.ReactorTypes, issues);
        Option(input.ReactorLevelKey, "reactor-level", BuildingCatalog.Levels, issues);

        if (input.FloorCount <= 0 || input.FloorCount > 1000)
        {
            issues.Add(new ValidationIssue(
                "floor-count",
                "Количество этажей должно быть от 1 до 1000.",
                ValidationSeverity.Error,
                "FloorCount"));
        }

        return new ValidationResult(issues);
    }

    private static BuildingCalculationResult Empty(
        ValidationResult validation,
        IEnumerable<AssetWarning> warnings)
    {
        return new BuildingCalculationResult(
            validation,
            new BreakdownRow[0],
            warnings,
            0, 0, 0,
            "Расчёт недоступен: исправьте обязательные поля.",
            0, 0, 0, 0, 0, 0, 0, 0, 0,
            new ReadOnlyDictionary<string, decimal>(new Dictionary<string, decimal>()),
            new ReadOnlyDictionary<string, int>(new Dictionary<string, int>()));
    }

    private static Dictionary<string, int> Storage(
        int floorArea,
        IEnumerable<SelectedComponent> selected)
    {
        return new Dictionary<string, int>
        {
            ["Общий склад"] = CalculationHelpers.QuantityOf(selected, BuildingCatalog.Index, "Склад общий") * floorArea * 10,
            ["Медицинский склад"] = CalculationHelpers.QuantityOf(selected, BuildingCatalog.Index, "Склад медицины") * floorArea * 10,
            ["Оружейный склад"] = CalculationHelpers.QuantityOf(selected, BuildingCatalog.Index, "Склад боеприпасов") * floorArea * 10,
            ["Топливный склад"] = CalculationHelpers.QuantityOf(selected, BuildingCatalog.Index, "Склад топлива") * floorArea * 10,
            ["Ангар"] = (
                CalculationHelpers.QuantityOf(selected, BuildingCatalog.Index, "Ангар для челноков") +
                CalculationHelpers.QuantityOf(selected, BuildingCatalog.Index, "Ангар общего назначения")) *
                floorArea
        };
    }

    private static string BuildSummary(
        BuildingInput input,
        string type,
        int floorArea,
        long cost,
        int hull,
        int armor,
        int shields,
        int energyProduced,
        int energyConsumed)
    {
        var text = new StringBuilder();
        text.AppendLine(input.ConfigurationName);
        text.AppendLine(type + ": " + input.FloorCount + " эт., " + floorArea + " м² на этаж");
        text.AppendLine("Целостность/броня/щиты: " + hull + "/" + armor + "/" + shields);
        text.AppendLine("Энергия: " + energyConsumed + "/" + energyProduced + " (потр./выр.)");
        if (!string.IsNullOrWhiteSpace(input.LocationDescription))
            text.AppendLine("Место: " + input.LocationDescription);
        if (!string.IsNullOrWhiteSpace(input.Purpose))
            text.AppendLine("Назначение: " + input.Purpose);
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
