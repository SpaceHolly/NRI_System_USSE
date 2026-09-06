using System.Text.Json;
using Nri.AssetConfigurators.Core.Building;
using Nri.AssetConfigurators.Core.Common;
using Nri.AssetConfigurators.Core.LandMarine;
using Nri.AssetConfigurators.Core.Presets;
using Nri.AssetConfigurators.Core.Spacecraft;

var capture = args.Contains("--capture", StringComparer.OrdinalIgnoreCase);
var outputPath = ValueAfter(args, "--output") ??
                 Path.Combine("obj", "0_18_2r_7", "legacy_configurator_parity_matrix.json");
var cases = new List<ParityCase>();

AddSpacecraftCases(cases);
AddLandMarineCases(cases);
AddBuildingCases(cases);
var demoCases = BuildComplexDemoCases();

var failures = new List<string>();
foreach (var item in cases)
{
    if (capture)
        continue;

    foreach (var expected in item.Expected)
    {
        if (!item.Actual.TryGetValue(expected.Key, out var actual) || actual != expected.Value)
            failures.Add($"{item.Id}: {expected.Key}, expected={expected.Value}, actual={actual}");
    }
}
foreach (var item in demoCases)
{
    foreach (var assertion in item.Assertions.Where(assertion => !assertion.Passed))
        failures.Add($"{item.Id}: {assertion.Name} — {assertion.Details}");
}

var matrix = new
{
    generatedAtUtc = DateTime.UtcNow,
    sourceCommits = new
    {
        spacecraft = SpacecraftCatalog.Source.CommitSha,
        landMarine = LandMarineCatalog.Source.CommitSha,
        building = BuildingCatalog.Source.CommitSha
    },
    caseCount = cases.Count + demoCases.Count,
    baselineParityCaseCount = cases.Count,
    complexDemoCaseCount = demoCases.Count,
    passed = failures.Count == 0 && cases.Count == 9 && demoCases.Count == 3,
    captureMode = capture,
    cases = cases.Select(item => new
    {
        item.Id,
        item.Configurator,
        item.Profile,
        item.Valid,
        item.Actual,
        item.Expected,
        pass = capture || item.Expected.All(expected =>
            item.Actual.TryGetValue(expected.Key, out var actual) && actual == expected.Value)
    }),
    complexDemoCases = demoCases.Select(item => new
    {
        item.Id,
        item.Configurator,
        item.Name,
        item.Valid,
        item.SelectedRows,
        item.SelectedQuantity,
        item.TotalCost,
        item.EnergyProduced,
        item.EnergyConsumed,
        item.Summary,
        assertions = item.Assertions,
        pass = item.Assertions.All(assertion => assertion.Passed)
    }),
    failures
};

var absoluteOutput = Path.GetFullPath(outputPath);
Directory.CreateDirectory(Path.GetDirectoryName(absoluteOutput)!);
File.WriteAllText(
    absoluteOutput,
    JsonSerializer.Serialize(matrix, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine(absoluteOutput);

if (capture)
{
    foreach (var item in cases)
        Console.WriteLine($"{item.Id}: {JsonSerializer.Serialize(item.Actual)}");
    return 0;
}

if (failures.Count > 0 || cases.Count != 9 || demoCases.Count != 3)
{
    foreach (var failure in failures)
        Console.Error.WriteLine(failure);
    return 1;
}

Console.WriteLine("12/12 asset configurator cases PASS (9 legacy parity + 3 complex demonstrations)");
return 0;

static IReadOnlyList<ComplexDemoCase> BuildComplexDemoCases()
{
    var result = new List<ComplexDemoCase>();

    var spacecraft = DemoPresets.Spacecraft();
    var spacecraftCalculation = new SpacecraftCalculatorService().Calculate(spacecraft);
    result.Add(new ComplexDemoCase(
        "complex-spacecraft-pilgrim",
        "spacecraft",
        spacecraft.ConfigurationName,
        spacecraftCalculation.Validation.IsValid,
        spacecraft.Components.Count,
        spacecraft.Components.Sum(item => item.Quantity),
        spacecraftCalculation.TotalCost,
        spacecraftCalculation.EnergyProduced,
        spacecraftCalculation.EnergyConsumed,
        spacecraftCalculation.Summary,
        new[]
        {
            Assert("canonical-name", spacecraft.ConfigurationName == "Экспедиционный корвет «Пилигрим»", spacecraft.ConfigurationName),
            Assert("valid", spacecraftCalculation.Validation.IsValid, ValidationDetails(spacecraftCalculation)),
            Assert("selected-rows", spacecraft.Components.Count >= 8, spacecraft.Components.Count.ToString()),
            Assert("selected-quantity", spacecraft.Components.Sum(item => item.Quantity) >= 12, spacecraft.Components.Sum(item => item.Quantity).ToString()),
            Assert("forward-weapon", spacecraft.Components.Any(item => item.Category == AssetComponentCategory.ForwardWeapon), "forward"),
            Assert("turret-weapon", spacecraft.Components.Any(item => item.Category == AssetComponentCategory.TurretWeapon), "turret"),
            Assert("civilian-kinds", spacecraft.Components.Count(item => item.Category == AssetComponentCategory.CivilianModule) >= 3, "civilian rows"),
            Assert("special-or-aux", spacecraft.Components.Count(item => item.Category == AssetComponentCategory.SpecialModule) + spacecraft.AuxiliaryHullModuleKeys.Count >= 2, "special/aux"),
            Assert("energy-balance", spacecraftCalculation.EnergyConsumed <= spacecraftCalculation.EnergyProduced, $"{spacecraftCalculation.EnergyConsumed}/{spacecraftCalculation.EnergyProduced}"),
            Assert("meaningful-result", spacecraftCalculation.TotalCost > 0 && spacecraftCalculation.Breakdown.Count > 4 && spacecraftCalculation.Speeds.Count > 0 && spacecraftCalculation.Storage.Count > 0, "cost/breakdown/speed/storage")
        }));

    var landMarine = DemoPresets.LandMarine();
    var landCalculation = new LandMarineCalculatorService().Calculate(landMarine);
    result.Add(new ComplexDemoCase(
        "complex-land-marine-amphibious",
        "land_marine",
        landMarine.ConfigurationName,
        landCalculation.Validation.IsValid,
        landMarine.Components.Count,
        landMarine.Components.Sum(item => item.Quantity),
        landCalculation.TotalCost,
        landCalculation.EnergyProduced,
        landCalculation.EnergyConsumed,
        landCalculation.Summary,
        new[]
        {
            Assert("canonical-name", landMarine.ConfigurationName == "Тяжёлая амфибийная разведывательно-боевая машина", landMarine.ConfigurationName),
            Assert("valid", landCalculation.Validation.IsValid, ValidationDetails(landCalculation)),
            Assert("hybrid", LandMarineCatalog.Index.DisplayName(landMarine.TypeKey) == "Гибрид", LandMarineCatalog.Index.DisplayName(landMarine.TypeKey)),
            Assert("land-water-drive", !string.IsNullOrWhiteSpace(landMarine.LandEngineKey) && !string.IsNullOrWhiteSpace(landMarine.WaterEngineKey), "land/water"),
            Assert("selected-rows", landMarine.Components.Count >= 7, landMarine.Components.Count.ToString()),
            Assert("selected-quantity", landMarine.Components.Sum(item => item.Quantity) >= 10, landMarine.Components.Sum(item => item.Quantity).ToString()),
            Assert("weapon-groups", landMarine.Components.Any(item => item.Category == AssetComponentCategory.ForwardWeapon) && landMarine.Components.Any(item => item.Category == AssetComponentCategory.TurretWeapon), "forward/turret"),
            Assert("civilian-special", landMarine.Components.Count(item => item.Category == AssetComponentCategory.CivilianModule || item.Category == AssetComponentCategory.SpecialModule) >= 3, "civilian/special rows"),
            Assert("auxiliary-hull", landMarine.AuxiliaryHullModuleKeys.Count >= 1, landMarine.AuxiliaryHullModuleKeys.Count.ToString()),
            Assert("energy-balance", landCalculation.EnergyConsumed <= landCalculation.EnergyProduced, $"{landCalculation.EnergyConsumed}/{landCalculation.EnergyProduced}"),
            Assert("meaningful-result", landCalculation.TotalCost > 0 && landCalculation.Breakdown.Count > 4 && landCalculation.LandSpeed > 0 && landCalculation.WaterSpeed > 0, "cost/breakdown/speeds")
        }));

    var building = DemoPresets.Building();
    var buildingCalculation = new BuildingCalculatorService().Calculate(building);
    result.Add(new ComplexDemoCase(
        "complex-building-research-fortress",
        "building",
        building.ConfigurationName,
        buildingCalculation.Validation.IsValid,
        building.Components.Count,
        building.Components.Sum(item => item.Quantity),
        buildingCalculation.TotalCost,
        buildingCalculation.EnergyProduced,
        buildingCalculation.EnergyConsumed,
        buildingCalculation.Summary,
        new[]
        {
            Assert("canonical-name", building.ConfigurationName == "Автономный укреплённый исследовательский комплекс", building.ConfigurationName),
            Assert("valid", buildingCalculation.Validation.IsValid, ValidationDetails(buildingCalculation)),
            Assert("floors", building.FloorCount >= 4, building.FloorCount.ToString()),
            Assert("medium-or-large", new[] { "M", "L", "VL", "A", "X", "XL", "XXL", "XXXL", "E", "XE" }.Contains(BuildingCatalog.Index.DisplayName(building.FloorSizeKey)), BuildingCatalog.Index.DisplayName(building.FloorSizeKey)),
            Assert("internal-kinds", building.Components.Where(item => item.Category == AssetComponentCategory.InternalModule).Select(item => item.ComponentKey).Distinct().Count() >= 5, "internal kinds"),
            Assert("storage-hangar", building.Components.Any(item => BuildingCatalog.Index.DisplayName(item.ComponentKey).Contains("Склад")) && building.Components.Any(item => BuildingCatalog.Index.DisplayName(item.ComponentKey).Contains("Ангар")), "storage/hangar"),
            Assert("defense", building.Components.Any(item => item.Category == AssetComponentCategory.DefensiveWeapon), "defense"),
            Assert("selected-rows", building.Components.Count >= 7, building.Components.Count.ToString()),
            Assert("purpose-location", !string.IsNullOrWhiteSpace(building.Purpose) && !string.IsNullOrWhiteSpace(building.LocationDescription), "purpose/location"),
            Assert("gm-comment", !string.IsNullOrWhiteSpace(building.GmComment), "admin-only comment"),
            Assert("resources", buildingCalculation.RequiredResources.Any(item => item.Value > 0), "required resources"),
            Assert("energy-balance", buildingCalculation.EnergyConsumed <= buildingCalculation.EnergyProduced, $"{buildingCalculation.EnergyConsumed}/{buildingCalculation.EnergyProduced}")
        }));

    return result;
}

static ComplexDemoAssertion Assert(string name, bool passed, string details) =>
    new(name, passed, details);

static string ValidationDetails(CalculationResult result) =>
    string.Join("; ", result.Validation.Issues.Select(issue => issue.Message));

static void AddSpacecraftCases(ICollection<ParityCase> cases)
{
    var calculator = new SpacecraftCalculatorService();
    var minimal = Spacecraft(
        "spacecraft-minimal",
        "C",
        "Универсал",
        "Стандартное",
        100,
        "Пилотируемый",
        "Газовый",
        "1 уровень",
        "Верфь - бедная");
    var minimalResult = calculator.Calculate(minimal);
    cases.Add(Case("spacecraft-minimal", "spacecraft", "minimal", minimalResult,
        SpacecraftMetrics(minimalResult), Expected("spacecraft-minimal")));

    var typical = Spacecraft(
        "spacecraft-typical",
        "S",
        "Корвет",
        "Качественное",
        125,
        "Гибрид",
        "Ядерный",
        "2 уровень",
        "Верфь - средняя");
    typical.Engines.Add(new SpacecraftEngineSelection(
        Option(SpacecraftCatalog.Index, "Космический", "engine-type"),
        Option(SpacecraftCatalog.Index, "Средний", "engine-size"),
        Option(SpacecraftCatalog.Index, "2 уровень", "level"),
        2));
    AddComponent(typical.Components, SpacecraftCatalog.Index, "Склад общий", 2, AssetComponentCategory.CivilianModule);
    AddComponent(typical.Components, SpacecraftCatalog.Index, "BGS-127 - Basic Gun System", 1, AssetComponentCategory.ForwardWeapon);
    var typicalResult = calculator.Calculate(typical);
    cases.Add(Case("spacecraft-typical", "spacecraft", "typical", typicalResult,
        SpacecraftMetrics(typicalResult), Expected("spacecraft-typical")));

    var heavy = Spacecraft(
        "spacecraft-heavy",
        "XL",
        "Линкор",
        "Надёжное",
        300,
        "ИИ",
        "Факелевый",
        "4 уровень",
        "Верфь - богатая");
    heavy.Engines.Add(new SpacecraftEngineSelection(
        Option(SpacecraftCatalog.Index, "Космический", "engine-type"),
        Option(SpacecraftCatalog.Index, "Большой", "engine-size"),
        Option(SpacecraftCatalog.Index, "4 уровень", "level"),
        4));
    heavy.AuxiliaryHullModuleKeys.Add(Option(SpacecraftCatalog.Index, "Корпус из Бориформия", "aux-hull"));
    heavy.AuxiliaryHullModuleKeys.Add(Option(SpacecraftCatalog.Index, "Броня из Сталиниума", "aux-hull"));
    AddComponent(heavy.Components, SpacecraftCatalog.Index, "Склад общий", 3, AssetComponentCategory.CivilianModule);
    AddComponent(heavy.Components, SpacecraftCatalog.Index, "Усилитель щита", 1, AssetComponentCategory.SpecialModule);
    var heavyResult = calculator.Calculate(heavy);
    cases.Add(Case("spacecraft-heavy", "spacecraft", "heavy", heavyResult,
        SpacecraftMetrics(heavyResult), Expected("spacecraft-heavy")));
}

static void AddLandMarineCases(ICollection<ParityCase> cases)
{
    var calculator = new LandMarineCalculatorService();
    var minimal = LandMarine(
        "land-marine-minimal",
        "Наземный",
        "C",
        "Мотоцикл",
        "Стандартное",
        "Колёса",
        "",
        "Газовый",
        "1 Уровень",
        "Пилотируемый",
        "Завод - бедный",
        100);
    var minimalResult = calculator.Calculate(minimal);
    cases.Add(Case("land-marine-minimal", "land-marine", "minimal", minimalResult,
        LandMarineMetrics(minimalResult), Expected("land-marine-minimal")));

    var typical = LandMarine(
        "land-marine-typical",
        "Гибрид",
        "M",
        "БМП(гиб.)",
        "Качественное",
        "Гусеницы",
        "1 Пропеллер",
        "Ядерный",
        "2 Уровень",
        "Гибрид",
        "Завод - средний",
        150);
    AddComponent(typical.Components, LandMarineCatalog.Index, "Склад общий", 1, AssetComponentCategory.CivilianModule);
    var typicalResult = calculator.Calculate(typical);
    cases.Add(Case("land-marine-typical", "land-marine", "typical", typicalResult,
        LandMarineMetrics(typicalResult), Expected("land-marine-typical")));

    var heavy = LandMarine(
        "land-marine-heavy",
        "Подводный",
        "XXL",
        "Подводный левиафан",
        "Надёжное",
        "",
        "4 Пропеллера",
        "Факелевый",
        "4 Уровень",
        "ИИ",
        "Завод - богатый",
        1000);
    heavy.AuxiliaryHullModuleKeys.Add(Option(LandMarineCatalog.Index, "Корпус из Бориформия", "aux-hull"));
    heavy.AuxiliaryHullModuleKeys.Add(Option(LandMarineCatalog.Index, "Броня из Сталиниума", "aux-hull"));
    AddComponent(heavy.Components, LandMarineCatalog.Index, "Склад топлива", 2, AssetComponentCategory.CivilianModule);
    var heavyResult = calculator.Calculate(heavy);
    cases.Add(Case("land-marine-heavy", "land-marine", "heavy", heavyResult,
        LandMarineMetrics(heavyResult), Expected("land-marine-heavy")));
}

static void AddBuildingCases(ICollection<ParityCase> cases)
{
    var calculator = new BuildingCalculatorService();
    var minimal = Building(
        "building-minimal",
        "Наземное",
        "C",
        1,
        "Собств.силами",
        "Ст.металлы",
        "Нет",
        "Нет",
        "Стандартное",
        "Газовый",
        "1 Ур.");
    var minimalResult = calculator.Calculate(minimal);
    cases.Add(Case("building-minimal", "building", "minimal", minimalResult,
        BuildingMetrics(minimalResult), Expected("building-minimal")));

    var typical = Building(
        "building-typical",
        "Бункер",
        "M",
        3,
        "Найм строител.",
        "Структурий",
        "Арморий",
        "Хассатий",
        "Качественное",
        "Ядерный",
        "2 Ур.");
    AddComponent(typical.Components, BuildingCatalog.Index, "Склад общий", 1, AssetComponentCategory.InternalModule);
    var typicalResult = calculator.Calculate(typical);
    cases.Add(Case("building-typical", "building", "typical", typicalResult,
        BuildingMetrics(typicalResult), Expected("building-typical")));

    var heavy = Building(
        "building-heavy",
        "Атмосферное",
        "XXXL",
        12,
        "Подрядчики",
        "Бориформий",
        "Сталиниум",
        "Хассатий-Б",
        "Надёжное",
        "Факелевый",
        "4 Ур.");
    AddComponent(heavy.Components, BuildingCatalog.Index, "Склад общий", 6, AssetComponentCategory.InternalModule);
    AddComponent(heavy.Components, BuildingCatalog.Index, "SGS-30 - Small Gun System", 2, AssetComponentCategory.DefensiveWeapon);
    var heavyResult = calculator.Calculate(heavy);
    cases.Add(Case("building-heavy", "building", "heavy", heavyResult,
        BuildingMetrics(heavyResult), Expected("building-heavy")));
}

static SpacecraftInput Spacecraft(
    string name,
    string size,
    string shipClass,
    string quality,
    int armor,
    string control,
    string reactor,
    string reactorLevel,
    string priceTier)
{
    return new SpacecraftInput
    {
        ConfigurationName = name,
        SizeKey = Option(SpacecraftCatalog.Index, size, "size"),
        ClassKey = Option(SpacecraftCatalog.Index, shipClass, "class"),
        QualityKey = Option(SpacecraftCatalog.Index, quality, "quality"),
        ArmorThicknessPercent = armor,
        ControlSystemKey = Option(SpacecraftCatalog.Index, control, "control"),
        ReactorTypeKey = Option(SpacecraftCatalog.Index, reactor, "reactor"),
        ReactorLevelKey = Option(SpacecraftCatalog.Index, reactorLevel, "level"),
        PriceTierKey = Option(SpacecraftCatalog.Index, priceTier, "price-tier")
    };
}

static LandMarineInput LandMarine(
    string name,
    string type,
    string size,
    string vehicleClass,
    string quality,
    string landEngine,
    string waterEngine,
    string reactor,
    string reactorLevel,
    string pilot,
    string priceTier,
    int armor)
{
    return new LandMarineInput
    {
        ConfigurationName = name,
        TypeKey = Option(LandMarineCatalog.Index, type, "type"),
        SizeKey = Option(LandMarineCatalog.Index, size, "size"),
        ClassKey = Option(LandMarineCatalog.Index, vehicleClass, "class"),
        QualityKey = Option(LandMarineCatalog.Index, quality, "quality"),
        LandEngineKey = string.IsNullOrEmpty(landEngine)
            ? string.Empty
            : Option(LandMarineCatalog.Index, landEngine, "land-engine"),
        LandEngineLevelKey = Option(LandMarineCatalog.Index, "2 Уровень", "level"),
        WaterEngineKey = string.IsNullOrEmpty(waterEngine)
            ? string.Empty
            : Option(LandMarineCatalog.Index, waterEngine, "water-engine"),
        WaterEngineLevelKey = Option(LandMarineCatalog.Index, "2 Уровень", "level"),
        ReactorTypeKey = Option(LandMarineCatalog.Index, reactor, "reactor"),
        ReactorLevelKey = Option(LandMarineCatalog.Index, reactorLevel, "level"),
        PilotSystemKey = Option(LandMarineCatalog.Index, pilot, "pilot"),
        PriceTierKey = Option(LandMarineCatalog.Index, priceTier, "price-tier"),
        ArmorThicknessPercent = armor
    };
}

static BuildingInput Building(
    string name,
    string type,
    string floorSize,
    int floors,
    string method,
    string hull,
    string armor,
    string shield,
    string quality,
    string reactor,
    string reactorLevel)
{
    return new BuildingInput
    {
        ConfigurationName = name,
        BuildingTypeKey = Option(BuildingCatalog.Index, type, "type"),
        FloorSizeKey = Option(BuildingCatalog.Index, floorSize, "floor-size"),
        FloorCount = floors,
        ConstructionMethodKey = Option(BuildingCatalog.Index, method, "method"),
        HullMaterialKey = Option(BuildingCatalog.Index, hull, "hull"),
        ArmorMaterialKey = Option(BuildingCatalog.Index, armor, "armor"),
        ShieldMaterialKey = Option(BuildingCatalog.Index, shield, "shield"),
        QualityKey = Option(BuildingCatalog.Index, quality, "quality"),
        ReactorTypeKey = Option(BuildingCatalog.Index, reactor, "reactor"),
        ReactorLevelKey = Option(BuildingCatalog.Index, reactorLevel, "level")
    };
}

static IReadOnlyDictionary<string, long> SpacecraftMetrics(SpacecraftCalculationResult result) =>
    new Dictionary<string, long>
    {
        ["cost"] = result.TotalCost,
        ["hull"] = result.Hull,
        ["armor"] = result.Armor,
        ["shields"] = result.Shields,
        ["barrier"] = result.Barrier,
        ["maneuverability"] = result.Maneuverability,
        ["energyProduced"] = result.EnergyProduced,
        ["energyConsumed"] = result.EnergyConsumed,
        ["civilianSlotsUsed"] = result.CivilianSlotsUsed,
        ["civilianSlotsAvailable"] = result.CivilianSlotsAvailable
    };

static IReadOnlyDictionary<string, long> LandMarineMetrics(LandMarineCalculationResult result) =>
    new Dictionary<string, long>
    {
        ["cost"] = result.TotalCost,
        ["hull"] = result.Hull,
        ["armor"] = result.Armor,
        ["shields"] = result.Shields,
        ["landSpeed"] = result.LandSpeed,
        ["waterSpeed"] = result.WaterSpeed,
        ["underwaterSpeed"] = result.UnderwaterSpeed,
        ["energyProduced"] = result.EnergyProduced,
        ["energyConsumed"] = result.EnergyConsumed,
        ["civilianSlotsAvailable"] = result.CivilianSlotsAvailable
    };

static IReadOnlyDictionary<string, long> BuildingMetrics(BuildingCalculationResult result) =>
    new Dictionary<string, long>
    {
        ["cost"] = result.TotalCost,
        ["floorArea"] = result.FloorArea,
        ["totalArea"] = result.TotalArea,
        ["structuralIntegrity"] = result.StructuralIntegrity,
        ["armorIntegrity"] = result.ArmorIntegrity,
        ["shieldIntegrity"] = result.ShieldIntegrity,
        ["energyProduced"] = result.EnergyProduced,
        ["energyConsumed"] = result.EnergyConsumed,
        ["internalSlotsAvailable"] = result.InternalSlotsAvailable,
        ["weaponSlotsAvailable"] = result.WeaponSlotsAvailable
    };

static ParityCase Case(
    string id,
    string configurator,
    string profile,
    CalculationResult result,
    IReadOnlyDictionary<string, long> actual,
    IReadOnlyDictionary<string, long> expected) =>
    new(id, configurator, profile, result.Validation.IsValid, actual, expected);

static IReadOnlyDictionary<string, long> Expected(string id)
{
    return id switch
    {
        "spacecraft-minimal" => Values(3700, 5, 5, 5, 0, 80, 10, 0, 0, 3,
            "cost", "hull", "armor", "shields", "barrier", "maneuverability",
            "energyProduced", "energyConsumed", "civilianSlotsUsed", "civilianSlotsAvailable"),
        "spacecraft-typical" => Values(283733, 38, 94, 75, 0, 50, 94, 0, 2, 30,
            "cost", "hull", "armor", "shields", "barrier", "maneuverability",
            "energyProduced", "energyConsumed", "civilianSlotsUsed", "civilianSlotsAvailable"),
        "spacecraft-heavy" => Values(116505094, 270, 1620, 540, 0, 8, 1770, 9, 3, 74,
            "cost", "hull", "armor", "shields", "barrier", "maneuverability",
            "energyProduced", "energyConsumed", "civilianSlotsUsed", "civilianSlotsAvailable"),
        "land-marine-minimal" => Values(1375, 5, 2, 0, 337, 0, 0, 1, 0, 1,
            "cost", "hull", "armor", "shields", "landSpeed", "waterSpeed",
            "underwaterSpeed", "energyProduced", "energyConsumed", "civilianSlotsAvailable"),
        "land-marine-typical" => Values(49259, 19, 31, 5, 72, 12, 0, 29, 0, 6,
            "cost", "hull", "armor", "shields", "landSpeed", "waterSpeed",
            "underwaterSpeed", "energyProduced", "energyConsumed", "civilianSlotsAvailable"),
        "land-marine-heavy" => Values(5074988, 300, 3900, 18, 0, 4, 2, 6480, 0, 30,
            "cost", "hull", "armor", "shields", "landSpeed", "waterSpeed",
            "underwaterSpeed", "energyProduced", "energyConsumed", "civilianSlotsAvailable"),
        "building-minimal" => Values(1000, 1, 1, 0, 0, 0, 50, 0, 1, 1,
            "cost", "floorArea", "totalArea", "structuralIntegrity", "armorIntegrity",
            "shieldIntegrity", "energyProduced", "energyConsumed", "internalSlotsAvailable",
            "weaponSlotsAvailable"),
        "building-typical" => Values(123756, 32, 96, 120, 120, 22, 720, 0, 3, 12,
            "cost", "floorArea", "totalArea", "structuralIntegrity", "armorIntegrity",
            "shieldIntegrity", "energyProduced", "energyConsumed", "internalSlotsAvailable",
            "weaponSlotsAvailable"),
        "building-heavy" => Values(253800885, 4096, 49152, 147456, 147456, 468, 18900, 0, 12, 26,
            "cost", "floorArea", "totalArea", "structuralIntegrity", "armorIntegrity",
            "shieldIntegrity", "energyProduced", "energyConsumed", "internalSlotsAvailable",
            "weaponSlotsAvailable"),
        _ => throw new InvalidOperationException("Unknown parity case: " + id)
    };
}

static IReadOnlyDictionary<string, long> Values(
    long value1,
    long value2,
    long value3,
    long value4,
    long value5,
    long value6,
    long value7,
    long value8,
    long value9,
    long value10,
    string key1,
    string key2,
    string key3,
    string key4,
    string key5,
    string key6,
    string key7,
    string key8,
    string key9,
    string key10)
{
    return new Dictionary<string, long>
    {
        [key1] = value1,
        [key2] = value2,
        [key3] = value3,
        [key4] = value4,
        [key5] = value5,
        [key6] = value6,
        [key7] = value7,
        [key8] = value8,
        [key9] = value9,
        [key10] = value10
    };
}

static string Option(LegacyCatalogIndex index, string displayName, string category) =>
    index.RequireOptionByDisplayName(displayName, category).Key;

static void AddComponent(
    ICollection<SelectedComponent> destination,
    LegacyCatalogIndex index,
    string displayName,
    int quantity,
    AssetComponentCategory category)
{
    destination.Add(new SelectedComponent(
        index.RequireComponentByDisplayName(displayName).Key,
        quantity,
        category));
}

static string? ValueAfter(string[] args, string name)
{
    var index = Array.FindIndex(args, value => string.Equals(value, name, StringComparison.OrdinalIgnoreCase));
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}

internal sealed record ParityCase(
    string Id,
    string Configurator,
    string Profile,
    bool Valid,
    IReadOnlyDictionary<string, long> Actual,
    IReadOnlyDictionary<string, long> Expected);

internal sealed record ComplexDemoCase(
    string Id,
    string Configurator,
    string Name,
    bool Valid,
    int SelectedRows,
    int SelectedQuantity,
    long TotalCost,
    int EnergyProduced,
    int EnergyConsumed,
    string Summary,
    IReadOnlyList<ComplexDemoAssertion> Assertions);

internal sealed record ComplexDemoAssertion(
    string Name,
    bool Passed,
    string Details);
