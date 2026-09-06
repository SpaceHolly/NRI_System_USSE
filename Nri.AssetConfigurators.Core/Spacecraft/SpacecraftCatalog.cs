using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Nri.AssetConfigurators.Core.Common;
using Nri.AssetConfigurators.Core.LegacyCatalogs;

namespace Nri.AssetConfigurators.Core.Spacecraft;

public static class SpacecraftCatalog
{
    public static readonly LegacySourceInfo Source = new LegacySourceInfo(
        "https://github.com/SpaceHolly/Space_ship_configurator",
        "94c38ae1e3ffbaf2c98e39dedbc5f59c369ab1b3",
        "classic-3.0");

    public static readonly IReadOnlyList<CatalogOption> Sizes =
        Options("size", SpacecraftLegacySpecs.Sizes);
    public static readonly IReadOnlyList<CatalogOption> Qualities =
        Options("quality", SpacecraftLegacySpecs.QualityCost.Keys);
    public static readonly IReadOnlyList<CatalogOption> Classes =
        Options("class", SpacecraftLegacySpecs.Classes.Values.SelectMany(value => value).Distinct());
    public static readonly IReadOnlyList<CatalogOption> ReactorTypes =
        Options("reactor", SpacecraftLegacySpecs.ReatorPowerAndCost.Keys);
    public static readonly IReadOnlyList<CatalogOption> Levels =
        Options("level", SpacecraftLegacySpecs.EngineLvl.Keys);
    public static readonly IReadOnlyList<CatalogOption> ControlSystems =
        Options("control", SpacecraftLegacySpecs.ControlUnitCosts.Keys);
    public static readonly IReadOnlyList<CatalogOption> PriceTiers =
        Options("price-tier", SpacecraftLegacySpecs.PriceAdd.Keys);
    public static readonly IReadOnlyList<CatalogOption> EngineTypes =
        Options("engine-type", SpacecraftLegacySpecs.EngineTypeModifiers.Keys);
    public static readonly IReadOnlyList<CatalogOption> EngineSizes =
        Options("engine-size", SpacecraftLegacySpecs.EngineSizeModifiers.Keys);
    public static readonly IReadOnlyList<CatalogOption> AuxiliaryHullModules =
        Options("aux-hull", SpacecraftLegacySpecs.AuxModules);
    public static readonly IReadOnlyList<CatalogOption> Sensors =
        Options("sensor", SpacecraftLegacySpecs.SensorCostsBySize.Values.SelectMany(value => value.Keys).Distinct());

    public static readonly IReadOnlyList<ComponentDefinition> Components =
        new ReadOnlyCollection<ComponentDefinition>(CreateComponents().ToList());

    public static readonly LegacyCatalogIndex Index =
        new LegacyCatalogIndex(
            Sizes.Concat(Qualities)
                .Concat(Classes)
                .Concat(ReactorTypes)
                .Concat(Levels)
                .Concat(ControlSystems)
                .Concat(PriceTiers)
                .Concat(EngineTypes)
                .Concat(EngineSizes)
                .Concat(AuxiliaryHullModules)
                .Concat(Sensors),
            Components);

    public static IReadOnlyList<CatalogOption> ClassesForSize(string sizeKey)
    {
        var size = Index.RequireOption(sizeKey).DisplayName;
        if (!SpacecraftLegacySpecs.Classes.TryGetValue(size, out var classes))
            return new ReadOnlyCollection<CatalogOption>(new List<CatalogOption>());
        return Options("class", classes);
    }

    internal static string Name(string key) => Index.DisplayName(key);

    private static IReadOnlyList<CatalogOption> Options(string group, IEnumerable<string> values)
    {
        return new ReadOnlyCollection<CatalogOption>(
            values.Select(value => new CatalogOption(
                    LegacyKey.Create("spacecraft", group, value),
                    value,
                    group))
                .ToList());
    }

    private static IEnumerable<ComponentDefinition> CreateComponents()
    {
        foreach (var item in SpacecraftLegacySpecs.Weapons)
        {
            var slot = SpacecraftLegacySpecs.WeaponsSizeAndConsumtion[item.Key];
            yield return Component(
                "weapon",
                item.Key,
                AssetComponentCategory.ForwardWeapon,
                item.Value.Cost,
                slot.WeponSize,
                slot.WeaponConsumtion,
                "Вооружение");
        }

        foreach (var item in SpacecraftLegacySpecs.CivilianCellCosts)
        {
            var cell = SpacecraftLegacySpecs.EnergyCostAndSizeByCell[item.Key];
            yield return Component(
                "civilian",
                item.Key,
                AssetComponentCategory.CivilianModule,
                item.Value,
                cell.CellSize,
                cell.EnergyCostCell,
                SpacecraftLegacySpecs.CivCellCategory.TryGetValue(item.Key, out var group) ? group : "Гражданские");
        }

        foreach (var item in SpacecraftLegacySpecs.SpecialCellCosts)
        {
            var cell = SpacecraftLegacySpecs.EnergyCostAndSizeByCell[item.Key];
            yield return Component(
                "special",
                item.Key,
                AssetComponentCategory.SpecialModule,
                item.Value,
                cell.CellSize,
                cell.EnergyCostCell,
                SpacecraftLegacySpecs.SpecialCellCategory.TryGetValue(item.Key, out var group) ? group : "Специальные");
        }
    }

    private static ComponentDefinition Component(
        string groupKey,
        string name,
        AssetComponentCategory category,
        long cost,
        int slots,
        int energy,
        string group)
    {
        return new ComponentDefinition(
            LegacyKey.Create("spacecraft", groupKey, name),
            name,
            category,
            cost,
            slots,
            energy,
            group);
    }
}
