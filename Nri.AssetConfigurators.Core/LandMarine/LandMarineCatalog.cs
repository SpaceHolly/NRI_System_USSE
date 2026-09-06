using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Nri.AssetConfigurators.Core.Common;
using Nri.AssetConfigurators.Core.LegacyCatalogs;

namespace Nri.AssetConfigurators.Core.LandMarine;

public static class LandMarineCatalog
{
    public static readonly LegacySourceInfo Source = new LegacySourceInfo(
        "https://github.com/SpaceHolly/Land-Marine_Vessel_Configurator",
        "5da7c5a2f2ac7117c23197d1bbf14c7debce68b5",
        "classic-1.0");

    public static readonly IReadOnlyList<CatalogOption> Types = Options("type", LandMarineLegacySpecs.Types);
    public static readonly IReadOnlyList<CatalogOption> Sizes = Options("size", LandMarineLegacySpecs.Sizes);
    public static readonly IReadOnlyList<CatalogOption> Qualities = Options("quality", LandMarineLegacySpecs.Quality);
    public static readonly IReadOnlyList<CatalogOption> Classes =
        Options("class", LandMarineLegacySpecs.Classes.Values.SelectMany(value => value).Distinct());
    public static readonly IReadOnlyList<CatalogOption> ReactorTypes =
        Options("reactor", LandMarineLegacySpecs.ReatorPowerAndCost.Keys);
    public static readonly IReadOnlyList<CatalogOption> Levels =
        Options("level", LandMarineLegacySpecs.LevelMultiplier.Keys);
    public static readonly IReadOnlyList<CatalogOption> PilotSystems =
        Options("pilot", LandMarineLegacySpecs.PilotSystemCost.Keys);
    public static readonly IReadOnlyList<CatalogOption> PriceTiers =
        Options("price-tier", LandMarineLegacySpecs.AdditionalCost.Keys);
    public static readonly IReadOnlyList<CatalogOption> LandEngines =
        Options("land-engine", LandMarineLegacySpecs.EngineCostMultiplier.Keys);
    public static readonly IReadOnlyList<CatalogOption> WaterEngines =
        Options("water-engine", LandMarineLegacySpecs.EngineCostMultiplier.Keys);
    public static readonly IReadOnlyList<CatalogOption> AuxiliaryHullModules =
        Options("aux-hull", LandMarineLegacySpecs.AuxHull);
    public static readonly IReadOnlyList<CatalogOption> Sensors =
        Options("sensor", LandMarineLegacySpecs.SensorCostBySize.Values.SelectMany(value => value.Keys).Distinct());

    public static readonly IReadOnlyList<ComponentDefinition> Components =
        new ReadOnlyCollection<ComponentDefinition>(CreateComponents().ToList());

    public static readonly LegacyCatalogIndex Index =
        new LegacyCatalogIndex(
            Types.Concat(Sizes)
                .Concat(Qualities)
                .Concat(Classes)
                .Concat(ReactorTypes)
                .Concat(Levels)
                .Concat(PilotSystems)
                .Concat(PriceTiers)
                .Concat(LandEngines)
                .Concat(WaterEngines)
                .Concat(AuxiliaryHullModules)
                .Concat(Sensors),
            Components);

    public static IReadOnlyList<CatalogOption> ClassesForType(string typeKey)
    {
        var type = Index.RequireOption(typeKey).DisplayName;
        return LandMarineLegacySpecs.ClassesByType.TryGetValue(type, out var values)
            ? Options("class", values)
            : new ReadOnlyCollection<CatalogOption>(new List<CatalogOption>());
    }

    internal static string Name(string key) => Index.DisplayName(key);

    private static IReadOnlyList<CatalogOption> Options(string group, IEnumerable<string> values)
    {
        return new ReadOnlyCollection<CatalogOption>(
            values.Select(value => new CatalogOption(
                    LegacyKey.Create("land-marine", group, value),
                    value,
                    group))
                .ToList());
    }

    private static IEnumerable<ComponentDefinition> CreateComponents()
    {
        foreach (var item in LandMarineLegacySpecs.Weapons)
        {
            var slot = LandMarineLegacySpecs.WeaponsSizeAndConsumtion[item.Key];
            yield return Component(
                "weapon",
                item.Key,
                AssetComponentCategory.ForwardWeapon,
                item.Value.Cost,
                slot.WeponSize,
                slot.WeaponConsumtion,
                "Вооружение");
        }

        foreach (var item in LandMarineLegacySpecs.CivilianCellCosts)
        {
            var cell = LandMarineLegacySpecs.EnergyCostAndSizeByItem[item.Key];
            yield return Component(
                "civilian",
                item.Key,
                AssetComponentCategory.CivilianModule,
                item.Value,
                cell.CellSize,
                cell.EnergyCostCell,
                "Гражданские");
        }

        foreach (var item in LandMarineLegacySpecs.SpecialCellCosts)
        {
            var cell = LandMarineLegacySpecs.EnergyCostAndSizeByItem[item.Key];
            yield return Component(
                "special",
                item.Key,
                AssetComponentCategory.SpecialModule,
                item.Value,
                cell.CellSize,
                cell.EnergyCostCell,
                "Специальные");
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
            LegacyKey.Create("land-marine", groupKey, name),
            name,
            category,
            cost,
            slots,
            energy,
            group);
    }
}
