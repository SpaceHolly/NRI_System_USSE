using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Nri.AssetConfigurators.Core.Common;
using Nri.AssetConfigurators.Core.LegacyCatalogs;

namespace Nri.AssetConfigurators.Core.Building;

public static class BuildingCatalog
{
    public static readonly LegacySourceInfo Source = new LegacySourceInfo(
        "https://github.com/SpaceHolly/Building_Configurator",
        "2fbcb20dde957991c5329d0e1f8ee9d8dcf2bc63",
        "classic-1.0");

    public static readonly IReadOnlyList<CatalogOption> BuildingTypes =
        Options("type", BuildingLegacySpecs.BuildingTypes);
    public static readonly IReadOnlyList<CatalogOption> FloorSizes =
        Options("floor-size", BuildingLegacySpecs.FloorSize.Keys);
    public static readonly IReadOnlyList<CatalogOption> ConstructionMethods =
        Options("method", BuildingLegacySpecs.BuildingMethodsTypes);
    public static readonly IReadOnlyList<CatalogOption> HullMaterials =
        Options("hull", BuildingLegacySpecs.HPResourcesTypes.Keys);
    public static readonly IReadOnlyList<CatalogOption> ArmorMaterials =
        Options("armor", BuildingLegacySpecs.APResourcesTypes.Keys);
    public static readonly IReadOnlyList<CatalogOption> ShieldMaterials =
        Options("shield", BuildingLegacySpecs.SPResourcesTypes.Keys);
    public static readonly IReadOnlyList<CatalogOption> Qualities =
        Options("quality", BuildingLegacySpecs.QualityCost.Keys);
    public static readonly IReadOnlyList<CatalogOption> ReactorTypes =
        Options("reactor", BuildingLegacySpecs.ReatorPowerAndCost.Keys);
    public static readonly IReadOnlyList<CatalogOption> Levels =
        Options("level", BuildingLegacySpecs.LevelMultiplier.Keys);

    public static readonly IReadOnlyList<ComponentDefinition> Components =
        new ReadOnlyCollection<ComponentDefinition>(CreateComponents().ToList());

    public static readonly LegacyCatalogIndex Index =
        new LegacyCatalogIndex(
            BuildingTypes.Concat(FloorSizes)
                .Concat(ConstructionMethods)
                .Concat(HullMaterials)
                .Concat(ArmorMaterials)
                .Concat(ShieldMaterials)
                .Concat(Qualities)
                .Concat(ReactorTypes)
                .Concat(Levels),
            Components);

    internal static string Name(string key) => Index.DisplayName(key);

    private static IReadOnlyList<CatalogOption> Options(string group, IEnumerable<string> values)
    {
        return new ReadOnlyCollection<CatalogOption>(
            values.Select(value => new CatalogOption(
                    LegacyKey.Create("building", group, value),
                    value,
                    group))
                .ToList());
    }

    private static IEnumerable<ComponentDefinition> CreateComponents()
    {
        foreach (var item in BuildingLegacySpecs.Weapons)
        {
            var slot = BuildingLegacySpecs.WeaponsSizeAndConsumtion[item.Key];
            yield return Component(
                "weapon",
                item.Key,
                AssetComponentCategory.DefensiveWeapon,
                item.Value.Cost,
                slot.WeponSize,
                slot.WeaponConsumtion,
                "Оборона");
        }

        foreach (var item in BuildingLegacySpecs.CellCosts)
        {
            yield return Component(
                "cell",
                item.Key,
                AssetComponentCategory.InternalModule,
                item.Value,
                1,
                BuildingLegacySpecs.EnergyCostByItem.TryGetValue(item.Key, out var energy) ? energy : 0,
                "Внутренние модули");
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
            LegacyKey.Create("building", groupKey, name),
            name,
            category,
            cost,
            slots,
            energy,
            group);
    }
}
