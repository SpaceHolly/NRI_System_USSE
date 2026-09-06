using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Nri.AssetConfigurators.Core.Building;
using Nri.AssetConfigurators.Core.Common;
using Nri.AssetConfigurators.Core.LandMarine;
using Nri.AssetConfigurators.Core.Spacecraft;

namespace Nri.AssetConfigurators.Wpf.Services;

public static class AssetConfiguratorPayloadCodec
{
    public static Dictionary<string, object> ToPayload(object input)
    {
        if (input is SpacecraftInput spacecraft)
            return SpacecraftPayload(spacecraft);
        if (input is LandMarineInput landMarine)
            return LandMarinePayload(landMarine);
        if (input is BuildingInput building)
            return BuildingPayload(building);
        throw new ArgumentException("Неизвестный тип конфигурации.", nameof(input));
    }

    public static object FromPayload(string kind, IDictionary<string, object> map)
    {
        if (string.Equals(kind, "spacecraft", StringComparison.OrdinalIgnoreCase))
            return SpacecraftInputFrom(map);
        if (string.Equals(kind, "land_marine", StringComparison.OrdinalIgnoreCase))
            return LandMarineInputFrom(map);
        if (string.Equals(kind, "building", StringComparison.OrdinalIgnoreCase))
            return BuildingInputFrom(map);
        throw new ArgumentException("Неизвестный тип конфигуратора.", nameof(kind));
    }

    private static Dictionary<string, object> SpacecraftPayload(SpacecraftInput value) =>
        new Dictionary<string, object>
        {
            ["configurationName"] = value.ConfigurationName,
            ["mode"] = Mode(value.Mode),
            ["sizeKey"] = value.SizeKey,
            ["classKey"] = value.ClassKey,
            ["qualityKey"] = value.QualityKey,
            ["priceTierKey"] = value.PriceTierKey,
            ["controlSystemKey"] = value.ControlSystemKey,
            ["reactorTypeKey"] = value.ReactorTypeKey,
            ["reactorLevelKey"] = value.ReactorLevelKey,
            ["armorThicknessPercent"] = value.ArmorThicknessPercent,
            ["engines"] = value.Engines.Select(engine => (object)new Dictionary<string, object>
            {
                ["typeKey"] = engine.TypeKey,
                ["sizeKey"] = engine.SizeKey,
                ["levelKey"] = engine.LevelKey,
                ["quantity"] = engine.Quantity
            }).ToArray(),
            ["sensorKeys"] = value.SensorKeys.Cast<object>().ToArray(),
            ["auxiliaryHullModuleKeys"] = value.AuxiliaryHullModuleKeys.Cast<object>().ToArray(),
            ["components"] = ComponentPayload(value.Components)
        };

    private static Dictionary<string, object> LandMarinePayload(LandMarineInput value) =>
        new Dictionary<string, object>
        {
            ["configurationName"] = value.ConfigurationName,
            ["mode"] = Mode(value.Mode),
            ["typeKey"] = value.TypeKey,
            ["sizeKey"] = value.SizeKey,
            ["classKey"] = value.ClassKey,
            ["qualityKey"] = value.QualityKey,
            ["landEngineKey"] = value.LandEngineKey,
            ["landEngineLevelKey"] = value.LandEngineLevelKey,
            ["waterEngineKey"] = value.WaterEngineKey,
            ["waterEngineLevelKey"] = value.WaterEngineLevelKey,
            ["reactorTypeKey"] = value.ReactorTypeKey,
            ["reactorLevelKey"] = value.ReactorLevelKey,
            ["pilotSystemKey"] = value.PilotSystemKey,
            ["priceTierKey"] = value.PriceTierKey,
            ["armorThicknessPercent"] = value.ArmorThicknessPercent,
            ["sensorKeys"] = value.SensorKeys.Cast<object>().ToArray(),
            ["auxiliaryHullModuleKeys"] = value.AuxiliaryHullModuleKeys.Cast<object>().ToArray(),
            ["components"] = ComponentPayload(value.Components)
        };

    private static Dictionary<string, object> BuildingPayload(BuildingInput value) =>
        new Dictionary<string, object>
        {
            ["configurationName"] = value.ConfigurationName,
            ["mode"] = Mode(value.Mode),
            ["buildingTypeKey"] = value.BuildingTypeKey,
            ["floorSizeKey"] = value.FloorSizeKey,
            ["floorCount"] = value.FloorCount,
            ["constructionMethodKey"] = value.ConstructionMethodKey,
            ["hullMaterialKey"] = value.HullMaterialKey,
            ["armorMaterialKey"] = value.ArmorMaterialKey,
            ["shieldMaterialKey"] = value.ShieldMaterialKey,
            ["qualityKey"] = value.QualityKey,
            ["reactorTypeKey"] = value.ReactorTypeKey,
            ["reactorLevelKey"] = value.ReactorLevelKey,
            ["locationDescription"] = value.LocationDescription,
            ["purpose"] = value.Purpose,
            ["components"] = ComponentPayload(value.Components)
        };

    private static object[] ComponentPayload(IEnumerable<SelectedComponent> components) =>
        components.Select(component => (object)new Dictionary<string, object>
        {
            ["componentKey"] = component.ComponentKey,
            ["quantity"] = component.Quantity,
            ["category"] = component.Category.ToString()
        }).ToArray();

    private static SpacecraftInput SpacecraftInputFrom(IDictionary<string, object> map)
    {
        var result = new SpacecraftInput
        {
            ConfigurationName = Text(map, "configurationName"),
            Mode = ParseMode(Text(map, "mode")),
            SizeKey = Text(map, "sizeKey"),
            ClassKey = Text(map, "classKey"),
            QualityKey = Text(map, "qualityKey"),
            PriceTierKey = Text(map, "priceTierKey"),
            ControlSystemKey = Text(map, "controlSystemKey"),
            ReactorTypeKey = Text(map, "reactorTypeKey"),
            ReactorLevelKey = Text(map, "reactorLevelKey"),
            ArmorThicknessPercent = Integer(map, "armorThicknessPercent", 100)
        };
        foreach (var raw in List(map, "engines"))
        {
            var engine = Map(raw);
            result.Engines.Add(new SpacecraftEngineSelection(
                Text(engine, "typeKey"),
                Text(engine, "sizeKey"),
                Text(engine, "levelKey"),
                Integer(engine, "quantity", 1)));
        }
        foreach (var key in Strings(map, "sensorKeys")) result.SensorKeys.Add(key);
        foreach (var key in Strings(map, "auxiliaryHullModuleKeys")) result.AuxiliaryHullModuleKeys.Add(key);
        foreach (var component in Components(map)) result.Components.Add(component);
        return result;
    }

    private static LandMarineInput LandMarineInputFrom(IDictionary<string, object> map)
    {
        var result = new LandMarineInput
        {
            ConfigurationName = Text(map, "configurationName"),
            Mode = ParseMode(Text(map, "mode")),
            TypeKey = Text(map, "typeKey"),
            SizeKey = Text(map, "sizeKey"),
            ClassKey = Text(map, "classKey"),
            QualityKey = Text(map, "qualityKey"),
            LandEngineKey = Text(map, "landEngineKey"),
            LandEngineLevelKey = Text(map, "landEngineLevelKey"),
            WaterEngineKey = Text(map, "waterEngineKey"),
            WaterEngineLevelKey = Text(map, "waterEngineLevelKey"),
            ReactorTypeKey = Text(map, "reactorTypeKey"),
            ReactorLevelKey = Text(map, "reactorLevelKey"),
            PilotSystemKey = Text(map, "pilotSystemKey"),
            PriceTierKey = Text(map, "priceTierKey"),
            ArmorThicknessPercent = Integer(map, "armorThicknessPercent", 100)
        };
        foreach (var key in Strings(map, "sensorKeys")) result.SensorKeys.Add(key);
        foreach (var key in Strings(map, "auxiliaryHullModuleKeys")) result.AuxiliaryHullModuleKeys.Add(key);
        foreach (var component in Components(map)) result.Components.Add(component);
        return result;
    }

    private static BuildingInput BuildingInputFrom(IDictionary<string, object> map)
    {
        var result = new BuildingInput
        {
            ConfigurationName = Text(map, "configurationName"),
            Mode = ParseMode(Text(map, "mode")),
            BuildingTypeKey = Text(map, "buildingTypeKey"),
            FloorSizeKey = Text(map, "floorSizeKey"),
            FloorCount = Integer(map, "floorCount", 1),
            ConstructionMethodKey = Text(map, "constructionMethodKey"),
            HullMaterialKey = Text(map, "hullMaterialKey"),
            ArmorMaterialKey = Text(map, "armorMaterialKey"),
            ShieldMaterialKey = Text(map, "shieldMaterialKey"),
            QualityKey = Text(map, "qualityKey"),
            ReactorTypeKey = Text(map, "reactorTypeKey"),
            ReactorLevelKey = Text(map, "reactorLevelKey"),
            LocationDescription = Text(map, "locationDescription"),
            Purpose = Text(map, "purpose"),
            GmComment = string.Empty
        };
        foreach (var component in Components(map)) result.Components.Add(component);
        return result;
    }

    private static IEnumerable<SelectedComponent> Components(IDictionary<string, object> map)
    {
        foreach (var raw in List(map, "components"))
        {
            var component = Map(raw);
            if (!Enum.TryParse(Text(component, "category"), true, out AssetComponentCategory category))
                category = AssetComponentCategory.CivilianModule;
            yield return new SelectedComponent(
                Text(component, "componentKey"),
                Math.Max(1, Integer(component, "quantity", 1)),
                category);
        }
    }

    private static string Mode(AssetConfiguratorMode mode) =>
        mode == AssetConfiguratorMode.NriSystemUsse ? "nri" : "classic";

    private static AssetConfiguratorMode ParseMode(string value) =>
        string.Equals(value, "nri", StringComparison.OrdinalIgnoreCase)
            ? AssetConfiguratorMode.NriSystemUsse
            : AssetConfiguratorMode.Classic;

    private static string Text(IDictionary<string, object> map, string key) =>
        map.TryGetValue(key, out var raw) ? Convert.ToString(raw) ?? string.Empty : string.Empty;

    private static int Integer(IDictionary<string, object> map, string key, int fallback) =>
        map.TryGetValue(key, out var raw) && int.TryParse(Convert.ToString(raw), out var value)
            ? value
            : fallback;

    private static IEnumerable<string> Strings(IDictionary<string, object> map, string key) =>
        List(map, key).Select(Convert.ToString).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!);

    private static IEnumerable<object> List(IDictionary<string, object> map, string key)
    {
        if (!map.TryGetValue(key, out var raw) || raw == null || raw is string)
            return Array.Empty<object>();
        if (raw is IEnumerable enumerable)
            return enumerable.Cast<object>();
        return Array.Empty<object>();
    }

    private static Dictionary<string, object> Map(object raw)
    {
        if (raw is Dictionary<string, object> typed)
            return typed;
        if (raw is IDictionary dictionary)
        {
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry entry in dictionary)
            {
                var key = Convert.ToString(entry.Key);
                if (!string.IsNullOrWhiteSpace(key))
                    result[key] = entry.Value!;
            }
            return result;
        }
        throw new ArgumentException("Некорректное описание конфигурации.");
    }
}
