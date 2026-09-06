using Nri.AssetConfigurators.Core.Building;
using Nri.AssetConfigurators.Core.Common;
using Nri.AssetConfigurators.Core.LandMarine;
using Nri.AssetConfigurators.Core.Spacecraft;

namespace Nri.AssetConfigurators.Core.Presets;

public static class DemoPresets
{
    public static SpacecraftInput Spacecraft()
    {
        var input = new SpacecraftInput
        {
            ConfigurationName = "Экспедиционный корвет «Пилигрим»",
            SizeKey = Option(SpacecraftCatalog.Index, "S", "size"),
            ClassKey = Option(SpacecraftCatalog.Index, "Корвет", "class"),
            QualityKey = Option(SpacecraftCatalog.Index, "Стандартное", "quality"),
            PriceTierKey = Option(SpacecraftCatalog.Index, "Верфь - средняя", "price-tier"),
            ControlSystemKey = Option(SpacecraftCatalog.Index, "Гибрид", "control"),
            ReactorTypeKey = Option(SpacecraftCatalog.Index, "Факелевый", "reactor"),
            ReactorLevelKey = Option(SpacecraftCatalog.Index, "4 уровень", "level"),
            ArmorThicknessPercent = 125
        };
        input.Engines.Add(new SpacecraftEngineSelection(
            Option(SpacecraftCatalog.Index, "Космический", "engine-type"),
            Option(SpacecraftCatalog.Index, "Средний", "engine-size"),
            Option(SpacecraftCatalog.Index, "3 уровень", "level"),
            2));
        input.SensorKeys.Add(Option(SpacecraftCatalog.Index, "Радио", "sensor"));
        input.AuxiliaryHullModuleKeys.Add(Option(SpacecraftCatalog.Index, "Корпус из Бориформия", "aux-hull"));
        input.AuxiliaryHullModuleKeys.Add(Option(SpacecraftCatalog.Index, "Броня из Сталиниума", "aux-hull"));
        input.Components.Add(Selected(SpacecraftCatalog.Index, "BGS-127 - Basic Gun System", 1, AssetComponentCategory.ForwardWeapon));
        input.Components.Add(Selected(SpacecraftCatalog.Index, "SGS-30 - Small Gun System", 1, AssetComponentCategory.TurretWeapon));
        input.Components.Add(Selected(SpacecraftCatalog.Index, "Склад общий", 2, AssetComponentCategory.CivilianModule));
        input.Components.Add(Selected(SpacecraftCatalog.Index, "Склад медицины", 2, AssetComponentCategory.CivilianModule));
        input.Components.Add(Selected(SpacecraftCatalog.Index, "Комната на 2 человека", 2, AssetComponentCategory.CivilianModule));
        input.Components.Add(Selected(SpacecraftCatalog.Index, "Склад топлива", 2, AssetComponentCategory.CivilianModule));
        input.Components.Add(Selected(SpacecraftCatalog.Index, "Клетка Фарадея", 1, AssetComponentCategory.SpecialModule));
        input.Components.Add(Selected(SpacecraftCatalog.Index, "Модуль магнитной маскировки", 1, AssetComponentCategory.SpecialModule));
        return input;
    }

    public static LandMarineInput LandMarine()
    {
        var input = new LandMarineInput
        {
            ConfigurationName = "Тяжёлая амфибийная разведывательно-боевая машина",
            TypeKey = Option(LandMarineCatalog.Index, "Гибрид", "type"),
            SizeKey = Option(LandMarineCatalog.Index, "L", "size"),
            ClassKey = Option(LandMarineCatalog.Index, "Лёгкий танк(гиб.)", "class"),
            QualityKey = Option(LandMarineCatalog.Index, "Стандартное", "quality"),
            LandEngineKey = Option(LandMarineCatalog.Index, "Гусеницы", "land-engine"),
            LandEngineLevelKey = Option(LandMarineCatalog.Index, "3 Уровень", "level"),
            WaterEngineKey = Option(LandMarineCatalog.Index, "2 Пропеллера", "water-engine"),
            WaterEngineLevelKey = Option(LandMarineCatalog.Index, "3 Уровень", "level"),
            ReactorTypeKey = Option(LandMarineCatalog.Index, "Факелевый", "reactor"),
            ReactorLevelKey = Option(LandMarineCatalog.Index, "4 Уровень", "level"),
            PilotSystemKey = Option(LandMarineCatalog.Index, "Гибрид", "pilot"),
            PriceTierKey = Option(LandMarineCatalog.Index, "Завод - средний", "price-tier"),
            ArmorThicknessPercent = 150
        };
        input.SensorKeys.Add(Option(LandMarineCatalog.Index, "Радиолокационные", "sensor"));
        input.AuxiliaryHullModuleKeys.Add(Option(LandMarineCatalog.Index, "Корпус из Бориформия", "aux-hull"));
        input.Components.Add(Selected(LandMarineCatalog.Index, "BGS-127 - Basic Gun System", 1, AssetComponentCategory.ForwardWeapon));
        input.Components.Add(Selected(LandMarineCatalog.Index, "SGS-30 - Small Gun System", 1, AssetComponentCategory.TurretWeapon));
        input.Components.Add(Selected(LandMarineCatalog.Index, "Склад общий", 2, AssetComponentCategory.CivilianModule));
        input.Components.Add(Selected(LandMarineCatalog.Index, "Склад медицины", 2, AssetComponentCategory.CivilianModule));
        input.Components.Add(Selected(LandMarineCatalog.Index, "Комната на 2 человека", 2, AssetComponentCategory.CivilianModule));
        input.Components.Add(Selected(LandMarineCatalog.Index, "Клетка Фарадея", 1, AssetComponentCategory.SpecialModule));
        input.Components.Add(Selected(LandMarineCatalog.Index, "Модуль магнитной маскировки", 1, AssetComponentCategory.SpecialModule));
        return input;
    }

    public static BuildingInput Building()
    {
        var input = new BuildingInput
        {
            ConfigurationName = "Автономный укреплённый исследовательский комплекс",
            BuildingTypeKey = Option(BuildingCatalog.Index, "Бункер", "type"),
            FloorSizeKey = Option(BuildingCatalog.Index, "L", "floor-size"),
            FloorCount = 12,
            ConstructionMethodKey = Option(BuildingCatalog.Index, "Подрядчики", "method"),
            HullMaterialKey = Option(BuildingCatalog.Index, "Бориформий", "hull"),
            ArmorMaterialKey = Option(BuildingCatalog.Index, "Сталиниум", "armor"),
            ShieldMaterialKey = Option(BuildingCatalog.Index, "Хассатий-Б", "shield"),
            QualityKey = Option(BuildingCatalog.Index, "Надёжное", "quality"),
            ReactorTypeKey = Option(BuildingCatalog.Index, "Факелевый", "reactor"),
            ReactorLevelKey = Option(BuildingCatalog.Index, "4 Ур.", "level"),
            LocationDescription = "Удалённый скальный массив, автономный периметр",
            Purpose = "Исследования, хранение образцов и оборона экспедиции",
            GmComment = "GM: резервный эвакуационный уровень скрыт от игроков."
        };
        input.Components.Add(Selected(BuildingCatalog.Index, "Склад общий", 2, AssetComponentCategory.InternalModule));
        input.Components.Add(Selected(BuildingCatalog.Index, "Ангар для челноков", 1, AssetComponentCategory.InternalModule));
        input.Components.Add(Selected(BuildingCatalog.Index, "Лаборатория", 2, AssetComponentCategory.InternalModule));
        input.Components.Add(Selected(BuildingCatalog.Index, "Медблок", 1, AssetComponentCategory.InternalModule));
        input.Components.Add(Selected(BuildingCatalog.Index, "Суперкомпьютер", 1, AssetComponentCategory.InternalModule));
        input.Components.Add(Selected(BuildingCatalog.Index, "BGS-127 - Basic Gun System", 1, AssetComponentCategory.DefensiveWeapon));
        input.Components.Add(Selected(BuildingCatalog.Index, "SGS-30 - Small Gun System", 1, AssetComponentCategory.DefensiveWeapon));
        return input;
    }

    private static string Option(LegacyCatalogIndex index, string displayName, string category) =>
        index.RequireOptionByDisplayName(displayName, category).Key;

    private static SelectedComponent Selected(
        LegacyCatalogIndex index,
        string displayName,
        int quantity,
        AssetComponentCategory category)
    {
        var component = index.RequireComponentByDisplayName(displayName);
        return new SelectedComponent(component.Key, quantity, category);
    }
}
