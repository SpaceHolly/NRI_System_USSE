using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using Nri.AssetConfigurators.Core.Building;
using Nri.AssetConfigurators.Core.Common;
using Nri.AssetConfigurators.Core.Comparison;
using Nri.AssetConfigurators.Core.LandMarine;
using Nri.AssetConfigurators.Core.Presets;
using Nri.AssetConfigurators.Core.Spacecraft;

namespace Nri.AssetConfigurators.Wpf.ViewModels;

public sealed class AssetConfiguratorWorkspaceViewModel : NotifyBase
{
    private int _selectedConfiguratorIndex;

    public AssetConfiguratorWorkspaceViewModel(bool showGmFields = true)
    {
        Spacecraft = new SpacecraftConfiguratorViewModel();
        LandMarine = new LandMarineConfiguratorViewModel();
        Building = new BuildingConfiguratorViewModel(showGmFields);
    }

    public SpacecraftConfiguratorViewModel Spacecraft { get; }
    public LandMarineConfiguratorViewModel LandMarine { get; }
    public BuildingConfiguratorViewModel Building { get; }

    public int SelectedConfiguratorIndex
    {
        get => _selectedConfiguratorIndex;
        set
        {
            if (!Set(ref _selectedConfiguratorIndex, Math.Max(0, Math.Min(2, value))))
                return;
            Notify(nameof(ActiveConfiguratorKind));
        }
    }

    public string ActiveConfiguratorKind =>
        SelectedConfiguratorIndex == 1 ? "land_marine" :
        SelectedConfiguratorIndex == 2 ? "building" :
        "spacecraft";

    public object BuildActiveInput()
    {
        if (SelectedConfiguratorIndex == 1)
            return LandMarine.BuildInput();
        if (SelectedConfiguratorIndex == 2)
            return Building.BuildInput();
        return Spacecraft.BuildInput();
    }

    public void ApplyInput(string kind, object input)
    {
        if (string.Equals(kind, "spacecraft", StringComparison.OrdinalIgnoreCase) &&
            input is SpacecraftInput spacecraft)
        {
            SelectedConfiguratorIndex = 0;
            Spacecraft.ApplyInput(spacecraft);
            return;
        }
        if (string.Equals(kind, "land_marine", StringComparison.OrdinalIgnoreCase) &&
            input is LandMarineInput landMarine)
        {
            SelectedConfiguratorIndex = 1;
            LandMarine.ApplyInput(landMarine);
            return;
        }
        if (string.Equals(kind, "building", StringComparison.OrdinalIgnoreCase) &&
            input is BuildingInput building)
        {
            SelectedConfiguratorIndex = 2;
            Building.ApplyInput(building);
            return;
        }
        throw new ArgumentException("Тип конфигуратора не соответствует данным.", nameof(input));
    }
}

public sealed class ConfiguratorMountOption
{
    public ConfiguratorMountOption(AssetComponentCategory category, string title)
    {
        Category = category;
        Title = title;
    }

    public AssetComponentCategory Category { get; }
    public string Title { get; }
    public override string ToString() => Title;
}

public sealed class ConfiguratorSelectionRow : NotifyBase
{
    private readonly Action _changed;
    private int _quantity;

    public ConfiguratorSelectionRow(
        ComponentDefinition definition,
        int quantity,
        AssetComponentCategory category,
        Action changed)
    {
        Definition = definition;
        _quantity = Math.Max(1, quantity);
        Category = category;
        _changed = changed;
    }

    public ComponentDefinition Definition { get; }
    public string DisplayName => Definition.DisplayName;
    public string Group => Definition.Group;
    public long UnitCost => Definition.Cost;
    public int UnitSlots => Definition.SlotSize;
    public int UnitEnergy => Definition.Energy;
    public AssetComponentCategory Category { get; }
    public string CategoryLabel => CategoryTitle(Category);

    public int Quantity
    {
        get => _quantity;
        set
        {
            var next = Math.Max(1, value);
            if (_quantity == next)
                return;
            _quantity = next;
            Notify();
            Notify(nameof(CostSummary));
            _changed();
        }
    }

    public string CostSummary => $"{UnitCost * (long)Quantity:N0} АР";
    public SelectedComponent ToSelection() => new SelectedComponent(Definition.Key, Quantity, Category);

    private static string CategoryTitle(AssetComponentCategory category)
    {
        switch (category)
        {
            case AssetComponentCategory.ForwardWeapon: return "Курсовое";
            case AssetComponentCategory.TurretWeapon: return "Турельное";
            case AssetComponentCategory.DefensiveWeapon: return "Оборона";
            case AssetComponentCategory.CivilianModule: return "Гражданская ячейка";
            case AssetComponentCategory.SpecialModule: return "Специальная ячейка";
            case AssetComponentCategory.InternalModule: return "Внутренний модуль";
            default: return "Компонент";
        }
    }
}

public sealed class ConfiguratorEngineRow
{
    public ConfiguratorEngineRow(
        CatalogOption type,
        CatalogOption size,
        CatalogOption level,
        int quantity)
    {
        Type = type;
        Size = size;
        Level = level;
        Quantity = Math.Max(1, quantity);
    }

    public CatalogOption Type { get; }
    public CatalogOption Size { get; }
    public CatalogOption Level { get; }
    public int Quantity { get; }
    public string Summary => $"{Type.DisplayName}, {Size.DisplayName}, {Level.DisplayName} × {Quantity}";
}

public sealed class ConfiguratorOptionRow
{
    public ConfiguratorOptionRow(CatalogOption option)
    {
        Option = option;
    }

    public CatalogOption Option { get; }
    public string DisplayName => Option.DisplayName;
}

public abstract class AssetConfiguratorToolViewModel : NotifyBase
{
    private readonly IReadOnlyList<ComponentDefinition> _catalog;
    private string _searchText = string.Empty;
    private string _selectedCategory = "Все";
    private ComponentDefinition? _selectedCatalogComponent;
    private ConfiguratorMountOption? _selectedMount;
    private CalculationResult? _lastResult;
    private IReadOnlyDictionary<string, decimal> _lastMetrics =
        new Dictionary<string, decimal>();
    private CalculationResult? _snapshot;
    private IReadOnlyDictionary<string, decimal> _snapshotMetrics =
        new Dictionary<string, decimal>();
    private string _snapshotName = string.Empty;
    private string _statusMessage = "Готово к расчёту.";
    private string _comparisonSummary = "Снимок ещё не сохранён.";
    private string _resultSummary = "Выберите параметры конфигурации.";
    private string _costSummary = "0 АР";
    private string _energySummary = "0 / 0";
    private string _limitSummary = "Лимиты будут рассчитаны после выбора основы.";
    private string _resultStatusLabel = "Ожидает настройки";
    private string _routeState = "ready";

    protected AssetConfiguratorToolViewModel(IReadOnlyList<ComponentDefinition> catalog)
    {
        _catalog = catalog;
        Components = new ObservableCollection<ComponentDefinition>();
        SelectedComponents = new ObservableCollection<ConfiguratorSelectionRow>();
        Breakdown = new ObservableCollection<BreakdownRow>();
        ValidationMessages = new ObservableCollection<ValidationIssue>();
        Warnings = new ObservableCollection<AssetWarning>();
        Categories = new ObservableCollection<string>(
            new[] { "Все" }.Concat(catalog.Select(item => item.Group).Where(item => !string.IsNullOrWhiteSpace(item)).Distinct().OrderBy(item => item)));
        MountOptions = new ObservableCollection<ConfiguratorMountOption>
        {
            new ConfiguratorMountOption(AssetComponentCategory.ForwardWeapon, "Курсовое вооружение"),
            new ConfiguratorMountOption(AssetComponentCategory.TurretWeapon, "Турельное вооружение"),
            new ConfiguratorMountOption(AssetComponentCategory.DefensiveWeapon, "Оборонительное вооружение")
        };
        _selectedMount = MountOptions[0];

        AddComponentCommand = new RelayCommand(AddSelectedComponent, () => SelectedCatalogComponent != null);
        IncreaseQuantityCommand = new RelayCommand<ConfiguratorSelectionRow>(row =>
        {
            if (row != null) row.Quantity++;
        });
        DecreaseQuantityCommand = new RelayCommand<ConfiguratorSelectionRow>(row =>
        {
            if (row != null && row.Quantity > 1) row.Quantity--;
        });
        RemoveComponentCommand = new RelayCommand<ConfiguratorSelectionRow>(row =>
        {
            if (row == null) return;
            SelectedComponents.Remove(row);
            Recalculate();
        });
        ResetCommand = new RelayCommand(Reset);
        LoadDemoCommand = new RelayCommand(LoadDemo);
        SaveSnapshotCommand = new RelayCommand(SaveSnapshot, () => _lastResult != null);
        PrepareProjectCommand = new RelayCommand(PrepareProject, () => _lastResult?.Validation.IsValid == true);
        RefreshCatalog();
    }

    public ObservableCollection<ComponentDefinition> Components { get; }
    public ObservableCollection<ConfiguratorSelectionRow> SelectedComponents { get; }
    public ObservableCollection<string> Categories { get; }
    public ObservableCollection<ConfiguratorMountOption> MountOptions { get; }
    public ObservableCollection<BreakdownRow> Breakdown { get; }
    public ObservableCollection<ValidationIssue> ValidationMessages { get; }
    public ObservableCollection<AssetWarning> Warnings { get; }
    public ICommand AddComponentCommand { get; }
    public ICommand IncreaseQuantityCommand { get; }
    public ICommand DecreaseQuantityCommand { get; }
    public ICommand RemoveComponentCommand { get; }
    public ICommand ResetCommand { get; }
    public ICommand LoadDemoCommand { get; }
    public ICommand SaveSnapshotCommand { get; }
    public ICommand PrepareProjectCommand { get; }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!Set(ref _searchText, value ?? string.Empty))
                return;
            RefreshCatalog();
        }
    }

    public string SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (!Set(ref _selectedCategory, value ?? "Все"))
                return;
            RefreshCatalog();
        }
    }

    public ComponentDefinition? SelectedCatalogComponent
    {
        get => _selectedCatalogComponent;
        set
        {
            if (!Set(ref _selectedCatalogComponent, value))
                return;
            ((RelayCommand)AddComponentCommand).RaiseCanExecuteChanged();
        }
    }

    public ConfiguratorMountOption? SelectedMount
    {
        get => _selectedMount;
        set => Set(ref _selectedMount, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        protected set => Set(ref _statusMessage, value);
    }

    public string ComparisonSummary
    {
        get => _comparisonSummary;
        protected set => Set(ref _comparisonSummary, value);
    }

    public string ResultSummary
    {
        get => _resultSummary;
        protected set => Set(ref _resultSummary, value);
    }

    public string CostSummary
    {
        get => _costSummary;
        protected set => Set(ref _costSummary, value);
    }

    public string EnergySummary
    {
        get => _energySummary;
        protected set => Set(ref _energySummary, value);
    }

    public string LimitSummary
    {
        get => _limitSummary;
        protected set => Set(ref _limitSummary, value);
    }

    public string ResultStatusLabel
    {
        get => _resultStatusLabel;
        protected set => Set(ref _resultStatusLabel, value);
    }

    public string RouteState
    {
        get => _routeState;
        protected set => Set(ref _routeState, value);
    }

    protected abstract void Recalculate();
    protected abstract void Reset();
    protected abstract void LoadDemo();
    protected abstract string CurrentConfigurationName { get; }

    protected void ApplyResult(
        CalculationResult result,
        IReadOnlyDictionary<string, decimal> metrics,
        string limitSummary)
    {
        _lastResult = result;
        _lastMetrics = metrics;
        ResultSummary = result.Summary;
        CostSummary = $"{result.TotalCost:N0} АР";
        EnergySummary = $"{result.EnergyConsumed:N0} / {result.EnergyProduced:N0} (потр./выр.)";
        LimitSummary = limitSummary;
        RouteState = result.Validation.IsValid ? "populated" : "validation";
        ResultStatusLabel = result.Validation.IsValid ? "Расчёт готов" : "Проверьте параметры";
        StatusMessage = result.Validation.IsValid
            ? "Расчёт обновлён."
            : "Исправьте отмеченные ограничения.";

        Replace(Breakdown, result.Breakdown);
        Replace(ValidationMessages, result.Validation.Issues);
        Replace(Warnings, result.Warnings);
        ((RelayCommand)SaveSnapshotCommand).RaiseCanExecuteChanged();
        ((RelayCommand)PrepareProjectCommand).RaiseCanExecuteChanged();
        RefreshComparison();
    }

    protected void ReplaceSelections(IEnumerable<SelectedComponent> selections, LegacyCatalogIndex index)
    {
        SelectedComponents.Clear();
        foreach (var selection in selections)
        {
            SelectedComponents.Add(new ConfiguratorSelectionRow(
                index.RequireComponent(selection.ComponentKey),
                selection.Quantity,
                selection.Category,
                Recalculate));
        }
    }

    protected IReadOnlyList<SelectedComponent> BuildSelections() =>
        SelectedComponents.Select(item => item.ToSelection()).ToList();

    private void AddSelectedComponent()
    {
        if (SelectedCatalogComponent == null)
            return;

        var category = SelectedCatalogComponent.Category;
        if (category == AssetComponentCategory.ForwardWeapon)
            category = SelectedMount?.Category ?? AssetComponentCategory.ForwardWeapon;
        if (SelectedCatalogComponent.Category == AssetComponentCategory.DefensiveWeapon)
            category = AssetComponentCategory.DefensiveWeapon;

        var existing = SelectedComponents.FirstOrDefault(item =>
            item.Definition.Key == SelectedCatalogComponent.Key && item.Category == category);
        if (existing != null)
        {
            existing.Quantity++;
        }
        else
        {
            SelectedComponents.Add(new ConfiguratorSelectionRow(
                SelectedCatalogComponent,
                1,
                category,
                Recalculate));
            Recalculate();
        }
    }

    private void RefreshCatalog()
    {
        var query = SearchText.Trim();
        var filtered = _catalog.Where(item =>
            (SelectedCategory == "Все" || item.Group == SelectedCategory) &&
            (query.Length == 0 ||
             item.DisplayName.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0 ||
             item.Group.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0));
        Replace(Components, filtered.OrderBy(item => item.Group).ThenBy(item => item.DisplayName));
    }

    private void SaveSnapshot()
    {
        if (_lastResult == null)
            return;
        _snapshot = _lastResult;
        _snapshotMetrics = _lastMetrics.ToDictionary(item => item.Key, item => item.Value);
        _snapshotName = CurrentConfigurationName;
        StatusMessage = "Снимок сохранён в памяти текущего сеанса.";
        RefreshComparison();
    }

    private void RefreshComparison()
    {
        if (_snapshot == null || _lastResult == null)
        {
            ComparisonSummary = "Снимок ещё не сохранён.";
            return;
        }

        var comparison = SnapshotComparer.Compare(
            _snapshotName,
            _snapshot,
            _lastResult,
            _snapshotMetrics,
            _lastMetrics);
        ComparisonSummary =
            $"Сравнение с «{comparison.BaselineName}»: стоимость {Signed(comparison.CostDelta)} АР, " +
            $"остаток энергии {Signed(comparison.EnergyDelta)}.";
    }

    private void PrepareProject()
    {
        StatusMessage = "Конфигурация прошла локальную проверку и готова к сохранению как чертёж.";
    }

    private static string Signed(long value) => value >= 0 ? "+" + value.ToString("N0") : value.ToString("N0");
    private static string Signed(int value) => value >= 0 ? "+" + value.ToString("N0") : value.ToString("N0");

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> values)
    {
        target.Clear();
        foreach (var value in values)
            target.Add(value);
    }
}

public sealed class SpacecraftConfiguratorViewModel : AssetConfiguratorToolViewModel
{
    private readonly SpacecraftCalculatorService _calculator = new SpacecraftCalculatorService();
    private string _configurationName = "Новый корабль";
    private AssetConfiguratorMode _mode;
    private CatalogOption? _size;
    private CatalogOption? _shipClass;
    private CatalogOption? _quality;
    private CatalogOption? _priceTier;
    private CatalogOption? _controlSystem;
    private CatalogOption? _reactorType;
    private CatalogOption? _reactorLevel;
    private int _armorThickness = 100;
    private CatalogOption? _engineType;
    private CatalogOption? _engineSize;
    private CatalogOption? _engineLevel;
    private int _engineQuantity = 1;
    private CatalogOption? _sensor;
    private CatalogOption? _auxiliaryModule;

    public SpacecraftConfiguratorViewModel() : base(SpacecraftCatalog.Components)
    {
        Modes = ModeOptions();
        Sizes = SpacecraftCatalog.Sizes;
        Classes = new ObservableCollection<CatalogOption>();
        Qualities = SpacecraftCatalog.Qualities;
        PriceTiers = SpacecraftCatalog.PriceTiers;
        ControlSystems = SpacecraftCatalog.ControlSystems;
        ReactorTypes = SpacecraftCatalog.ReactorTypes;
        Levels = SpacecraftCatalog.Levels;
        EngineTypes = SpacecraftCatalog.EngineTypes;
        EngineSizes = SpacecraftCatalog.EngineSizes;
        Sensors = SpacecraftCatalog.Sensors;
        AuxiliaryModules = SpacecraftCatalog.AuxiliaryHullModules;
        Engines = new ObservableCollection<ConfiguratorEngineRow>();
        SelectedSensors = new ObservableCollection<ConfiguratorOptionRow>();
        SelectedAuxiliaryModules = new ObservableCollection<ConfiguratorOptionRow>();
        AddEngineCommand = new RelayCommand(AddEngine);
        RemoveEngineCommand = new RelayCommand<ConfiguratorEngineRow>(row =>
        {
            if (row == null) return;
            Engines.Remove(row);
            Recalculate();
        });
        AddSensorCommand = new RelayCommand(AddSensor);
        RemoveSensorCommand = new RelayCommand<ConfiguratorOptionRow>(row =>
        {
            if (row == null) return;
            SelectedSensors.Remove(row);
            Recalculate();
        });
        AddAuxiliaryCommand = new RelayCommand(AddAuxiliary);
        RemoveAuxiliaryCommand = new RelayCommand<ConfiguratorOptionRow>(row =>
        {
            if (row == null) return;
            SelectedAuxiliaryModules.Remove(row);
            Recalculate();
        });
        ApplyDemo(DemoPresets.Spacecraft());
    }

    public IReadOnlyList<CatalogOption> Modes { get; }
    public IReadOnlyList<CatalogOption> Sizes { get; }
    public ObservableCollection<CatalogOption> Classes { get; }
    public IReadOnlyList<CatalogOption> Qualities { get; }
    public IReadOnlyList<CatalogOption> PriceTiers { get; }
    public IReadOnlyList<CatalogOption> ControlSystems { get; }
    public IReadOnlyList<CatalogOption> ReactorTypes { get; }
    public IReadOnlyList<CatalogOption> Levels { get; }
    public IReadOnlyList<CatalogOption> EngineTypes { get; }
    public IReadOnlyList<CatalogOption> EngineSizes { get; }
    public IReadOnlyList<CatalogOption> Sensors { get; }
    public IReadOnlyList<CatalogOption> AuxiliaryModules { get; }
    public ObservableCollection<ConfiguratorEngineRow> Engines { get; }
    public ObservableCollection<ConfiguratorOptionRow> SelectedSensors { get; }
    public ObservableCollection<ConfiguratorOptionRow> SelectedAuxiliaryModules { get; }
    public ICommand AddEngineCommand { get; }
    public ICommand RemoveEngineCommand { get; }
    public ICommand AddSensorCommand { get; }
    public ICommand RemoveSensorCommand { get; }
    public ICommand AddAuxiliaryCommand { get; }
    public ICommand RemoveAuxiliaryCommand { get; }
    public string SourceVersion => $"Classic 3.0 · {SpacecraftCatalog.Source.CommitSha.Substring(0, 10)}";

    public string ConfigurationName { get => _configurationName; set { if (Set(ref _configurationName, value)) Recalculate(); } }
    public CatalogOption? SelectedMode { get => Modes[(int)_mode]; set { if (value != null) { _mode = value.Key.EndsWith("nri", StringComparison.Ordinal) ? AssetConfiguratorMode.NriSystemUsse : AssetConfiguratorMode.Classic; Notify(); Recalculate(); } } }
    public CatalogOption? Size { get => _size; set { if (Set(ref _size, value)) { RefreshClasses(); Recalculate(); } } }
    public CatalogOption? ShipClass { get => _shipClass; set { if (Set(ref _shipClass, value)) Recalculate(); } }
    public CatalogOption? Quality { get => _quality; set { if (Set(ref _quality, value)) Recalculate(); } }
    public CatalogOption? PriceTier { get => _priceTier; set { if (Set(ref _priceTier, value)) Recalculate(); } }
    public CatalogOption? ControlSystem { get => _controlSystem; set { if (Set(ref _controlSystem, value)) Recalculate(); } }
    public CatalogOption? ReactorType { get => _reactorType; set { if (Set(ref _reactorType, value)) Recalculate(); } }
    public CatalogOption? ReactorLevel { get => _reactorLevel; set { if (Set(ref _reactorLevel, value)) Recalculate(); } }
    public int ArmorThickness { get => _armorThickness; set { if (Set(ref _armorThickness, value)) Recalculate(); } }
    public decimal? ArmorThicknessValue { get => ArmorThickness; set { if (value.HasValue) ArmorThickness = decimal.ToInt32(value.Value); } }
    public CatalogOption? EngineType { get => _engineType; set => Set(ref _engineType, value); }
    public CatalogOption? EngineSize { get => _engineSize; set => Set(ref _engineSize, value); }
    public CatalogOption? EngineLevel { get => _engineLevel; set => Set(ref _engineLevel, value); }
    public int EngineQuantity { get => _engineQuantity; set => Set(ref _engineQuantity, Math.Max(1, value)); }
    public decimal? EngineQuantityValue { get => EngineQuantity; set { if (value.HasValue) EngineQuantity = decimal.ToInt32(value.Value); } }
    public CatalogOption? Sensor { get => _sensor; set => Set(ref _sensor, value); }
    public CatalogOption? AuxiliaryModule { get => _auxiliaryModule; set => Set(ref _auxiliaryModule, value); }
    protected override string CurrentConfigurationName => ConfigurationName;

    protected override void Recalculate()
    {
        var input = BuildInput();
        var result = _calculator.Calculate(input);
        ApplyResult(
            result,
            result.Metrics(),
            $"Ячейки {result.CivilianSlotsUsed}/{result.CivilianSlotsAvailable}; " +
            $"особые {result.SpecialSlotsUsed}/{result.SpecialSlotsAvailable}; " +
            $"курсовые {result.ForwardWeaponSlotsUsed}/{result.ForwardWeaponSlotsAvailable}; " +
            $"турели {result.TurretWeaponSlotsUsed}/{result.TurretWeaponSlotsAvailable}.");
    }

    protected override void Reset() => ApplyDemo(CreateEmpty());
    protected override void LoadDemo() => ApplyDemo(DemoPresets.Spacecraft());

    public SpacecraftInput BuildInput()
    {
        var input = new SpacecraftInput
        {
            ConfigurationName = ConfigurationName,
            Mode = _mode,
            SizeKey = Size?.Key ?? string.Empty,
            ClassKey = ShipClass?.Key ?? string.Empty,
            QualityKey = Quality?.Key ?? string.Empty,
            PriceTierKey = PriceTier?.Key ?? string.Empty,
            ControlSystemKey = ControlSystem?.Key ?? string.Empty,
            ReactorTypeKey = ReactorType?.Key ?? string.Empty,
            ReactorLevelKey = ReactorLevel?.Key ?? string.Empty,
            ArmorThicknessPercent = ArmorThickness
        };
        foreach (var engine in Engines)
            input.Engines.Add(new SpacecraftEngineSelection(engine.Type.Key, engine.Size.Key, engine.Level.Key, engine.Quantity));
        foreach (var sensor in SelectedSensors)
            input.SensorKeys.Add(sensor.Option.Key);
        foreach (var module in SelectedAuxiliaryModules)
            input.AuxiliaryHullModuleKeys.Add(module.Option.Key);
        foreach (var component in BuildSelections())
            input.Components.Add(component);
        return input;
    }

    public void ApplyInput(SpacecraftInput input) => ApplyDemo(input);

    private void ApplyDemo(SpacecraftInput input)
    {
        _configurationName = input.ConfigurationName;
        _mode = input.Mode;
        _size = Sizes.FirstOrDefault(item => item.Key == input.SizeKey);
        RefreshClasses();
        _shipClass = Classes.FirstOrDefault(item => item.Key == input.ClassKey);
        _quality = Qualities.FirstOrDefault(item => item.Key == input.QualityKey);
        _priceTier = PriceTiers.FirstOrDefault(item => item.Key == input.PriceTierKey);
        _controlSystem = ControlSystems.FirstOrDefault(item => item.Key == input.ControlSystemKey);
        _reactorType = ReactorTypes.FirstOrDefault(item => item.Key == input.ReactorTypeKey);
        _reactorLevel = Levels.FirstOrDefault(item => item.Key == input.ReactorLevelKey);
        _armorThickness = input.ArmorThicknessPercent;
        Engines.Clear();
        foreach (var engine in input.Engines)
        {
            Engines.Add(new ConfiguratorEngineRow(
                EngineTypes.First(item => item.Key == engine.TypeKey),
                EngineSizes.First(item => item.Key == engine.SizeKey),
                Levels.First(item => item.Key == engine.LevelKey),
                engine.Quantity));
        }
        SelectedSensors.Clear();
        foreach (var key in input.SensorKeys)
            SelectedSensors.Add(new ConfiguratorOptionRow(Sensors.First(item => item.Key == key)));
        SelectedAuxiliaryModules.Clear();
        foreach (var key in input.AuxiliaryHullModuleKeys)
            SelectedAuxiliaryModules.Add(new ConfiguratorOptionRow(AuxiliaryModules.First(item => item.Key == key)));
        ReplaceSelections(input.Components, SpacecraftCatalog.Index);
        _engineType = EngineTypes.FirstOrDefault();
        _engineSize = EngineSizes.FirstOrDefault();
        _engineLevel = Levels.FirstOrDefault();
        _sensor = Sensors.FirstOrDefault();
        _auxiliaryModule = AuxiliaryModules.FirstOrDefault();
        NotifyAll();
        Recalculate();
    }

    private SpacecraftInput CreateEmpty()
    {
        var input = DemoPresets.Spacecraft();
        input.ConfigurationName = "Новый корабль";
        input.Engines.Clear();
        input.Components.Clear();
        input.SensorKeys.Clear();
        input.AuxiliaryHullModuleKeys.Clear();
        return input;
    }

    private void RefreshClasses()
    {
        Classes.Clear();
        if (_size == null)
            return;
        foreach (var item in SpacecraftCatalog.ClassesForSize(_size.Key))
            Classes.Add(item);
        if (_shipClass == null || Classes.All(item => item.Key != _shipClass.Key))
            _shipClass = Classes.FirstOrDefault();
        Notify(nameof(ShipClass));
    }

    private void AddEngine()
    {
        if (EngineType == null || EngineSize == null || EngineLevel == null)
            return;
        Engines.Add(new ConfiguratorEngineRow(EngineType, EngineSize, EngineLevel, EngineQuantity));
        Recalculate();
    }

    private void AddSensor()
    {
        if (Sensor == null || SelectedSensors.Any(item => item.Option.Key == Sensor.Key))
            return;
        SelectedSensors.Add(new ConfiguratorOptionRow(Sensor));
        Recalculate();
    }

    private void AddAuxiliary()
    {
        if (AuxiliaryModule == null || SelectedAuxiliaryModules.Any(item => item.Option.Key == AuxiliaryModule.Key))
            return;
        SelectedAuxiliaryModules.Add(new ConfiguratorOptionRow(AuxiliaryModule));
        Recalculate();
    }

    private void NotifyAll()
    {
        Notify(nameof(ConfigurationName));
        Notify(nameof(SelectedMode));
        Notify(nameof(Size));
        Notify(nameof(ShipClass));
        Notify(nameof(Quality));
        Notify(nameof(PriceTier));
        Notify(nameof(ControlSystem));
        Notify(nameof(ReactorType));
        Notify(nameof(ReactorLevel));
        Notify(nameof(ArmorThickness));
        Notify(nameof(ArmorThicknessValue));
        Notify(nameof(EngineType));
        Notify(nameof(EngineSize));
        Notify(nameof(EngineLevel));
        Notify(nameof(EngineQuantityValue));
        Notify(nameof(Sensor));
        Notify(nameof(AuxiliaryModule));
    }

    private static IReadOnlyList<CatalogOption> ModeOptions() => new[]
    {
        new CatalogOption("mode.classic", "Классический", "mode", "Формулы исходного приложения."),
        new CatalogOption("mode.nri", "NRI System USSE", "mode", "Сопоставление с definitions, где оно доступно.")
    };
}

public sealed class LandMarineConfiguratorViewModel : AssetConfiguratorToolViewModel
{
    private readonly LandMarineCalculatorService _calculator = new LandMarineCalculatorService();
    private string _configurationName = "Новая техника";
    private AssetConfiguratorMode _mode;
    private CatalogOption? _type;
    private CatalogOption? _size;
    private CatalogOption? _vehicleClass;
    private CatalogOption? _quality;
    private CatalogOption? _landEngine;
    private CatalogOption? _landLevel;
    private CatalogOption? _waterEngine;
    private CatalogOption? _waterLevel;
    private CatalogOption? _reactorType;
    private CatalogOption? _reactorLevel;
    private CatalogOption? _pilotSystem;
    private CatalogOption? _priceTier;
    private int _armorThickness = 100;
    private CatalogOption? _sensor;
    private CatalogOption? _auxiliaryModule;

    public LandMarineConfiguratorViewModel() : base(LandMarineCatalog.Components)
    {
        Modes = SpacecraftConfiguratorViewModelModeOptions.Create();
        Types = LandMarineCatalog.Types;
        Sizes = LandMarineCatalog.Sizes;
        Classes = new ObservableCollection<CatalogOption>();
        Qualities = LandMarineCatalog.Qualities;
        Engines = LandMarineCatalog.LandEngines;
        Levels = LandMarineCatalog.Levels;
        ReactorTypes = LandMarineCatalog.ReactorTypes;
        PilotSystems = LandMarineCatalog.PilotSystems;
        PriceTiers = LandMarineCatalog.PriceTiers;
        Sensors = LandMarineCatalog.Sensors;
        AuxiliaryModules = LandMarineCatalog.AuxiliaryHullModules;
        SelectedSensors = new ObservableCollection<ConfiguratorOptionRow>();
        SelectedAuxiliaryModules = new ObservableCollection<ConfiguratorOptionRow>();
        AddSensorCommand = new RelayCommand(AddSensor);
        RemoveSensorCommand = new RelayCommand<ConfiguratorOptionRow>(row =>
        {
            if (row == null) return;
            SelectedSensors.Remove(row);
            Recalculate();
        });
        AddAuxiliaryCommand = new RelayCommand(AddAuxiliary);
        RemoveAuxiliaryCommand = new RelayCommand<ConfiguratorOptionRow>(row =>
        {
            if (row == null) return;
            SelectedAuxiliaryModules.Remove(row);
            Recalculate();
        });
        ApplyDemo(DemoPresets.LandMarine());
    }

    public IReadOnlyList<CatalogOption> Modes { get; }
    public IReadOnlyList<CatalogOption> Types { get; }
    public IReadOnlyList<CatalogOption> Sizes { get; }
    public ObservableCollection<CatalogOption> Classes { get; }
    public IReadOnlyList<CatalogOption> Qualities { get; }
    public IReadOnlyList<CatalogOption> Engines { get; }
    public IReadOnlyList<CatalogOption> Levels { get; }
    public IReadOnlyList<CatalogOption> ReactorTypes { get; }
    public IReadOnlyList<CatalogOption> PilotSystems { get; }
    public IReadOnlyList<CatalogOption> PriceTiers { get; }
    public IReadOnlyList<CatalogOption> Sensors { get; }
    public IReadOnlyList<CatalogOption> AuxiliaryModules { get; }
    public ObservableCollection<ConfiguratorOptionRow> SelectedSensors { get; }
    public ObservableCollection<ConfiguratorOptionRow> SelectedAuxiliaryModules { get; }
    public ICommand AddSensorCommand { get; }
    public ICommand RemoveSensorCommand { get; }
    public ICommand AddAuxiliaryCommand { get; }
    public ICommand RemoveAuxiliaryCommand { get; }
    public string SourceVersion => $"Classic 1.0 · {LandMarineCatalog.Source.CommitSha.Substring(0, 10)}";

    public string ConfigurationName { get => _configurationName; set { if (Set(ref _configurationName, value)) Recalculate(); } }
    public CatalogOption? SelectedMode { get => Modes[(int)_mode]; set { if (value != null) { _mode = value.Key.EndsWith("nri", StringComparison.Ordinal) ? AssetConfiguratorMode.NriSystemUsse : AssetConfiguratorMode.Classic; Notify(); Recalculate(); } } }
    public CatalogOption? Type { get => _type; set { if (Set(ref _type, value)) { RefreshClasses(); Recalculate(); } } }
    public CatalogOption? Size { get => _size; set { if (Set(ref _size, value)) Recalculate(); } }
    public CatalogOption? VehicleClass { get => _vehicleClass; set { if (Set(ref _vehicleClass, value)) Recalculate(); } }
    public CatalogOption? Quality { get => _quality; set { if (Set(ref _quality, value)) Recalculate(); } }
    public CatalogOption? LandEngine { get => _landEngine; set { if (Set(ref _landEngine, value)) Recalculate(); } }
    public CatalogOption? LandLevel { get => _landLevel; set { if (Set(ref _landLevel, value)) Recalculate(); } }
    public CatalogOption? WaterEngine { get => _waterEngine; set { if (Set(ref _waterEngine, value)) Recalculate(); } }
    public CatalogOption? WaterLevel { get => _waterLevel; set { if (Set(ref _waterLevel, value)) Recalculate(); } }
    public CatalogOption? ReactorType { get => _reactorType; set { if (Set(ref _reactorType, value)) Recalculate(); } }
    public CatalogOption? ReactorLevel { get => _reactorLevel; set { if (Set(ref _reactorLevel, value)) Recalculate(); } }
    public CatalogOption? PilotSystem { get => _pilotSystem; set { if (Set(ref _pilotSystem, value)) Recalculate(); } }
    public CatalogOption? PriceTier { get => _priceTier; set { if (Set(ref _priceTier, value)) Recalculate(); } }
    public int ArmorThickness { get => _armorThickness; set { if (Set(ref _armorThickness, value)) Recalculate(); } }
    public decimal? ArmorThicknessValue { get => ArmorThickness; set { if (value.HasValue) ArmorThickness = decimal.ToInt32(value.Value); } }
    public CatalogOption? Sensor { get => _sensor; set => Set(ref _sensor, value); }
    public CatalogOption? AuxiliaryModule { get => _auxiliaryModule; set => Set(ref _auxiliaryModule, value); }
    protected override string CurrentConfigurationName => ConfigurationName;

    protected override void Recalculate()
    {
        var result = _calculator.Calculate(BuildInput());
        ApplyResult(
            result,
            result.Metrics(),
            $"Ячейки {result.CivilianSlotsUsed}/{result.CivilianSlotsAvailable}; " +
            $"особые {result.SpecialSlotsUsed}/{result.SpecialSlotsAvailable}; " +
            $"курсовые {result.ForwardWeaponSlotsUsed}/{result.ForwardWeaponSlotsAvailable}; " +
            $"турели {result.TurretWeaponSlotsUsed}/{result.TurretWeaponSlotsAvailable}.");
    }

    protected override void Reset() => ApplyDemo(CreateEmpty());
    protected override void LoadDemo() => ApplyDemo(DemoPresets.LandMarine());

    public LandMarineInput BuildInput()
    {
        var input = new LandMarineInput
        {
            ConfigurationName = ConfigurationName,
            Mode = _mode,
            TypeKey = Type?.Key ?? string.Empty,
            SizeKey = Size?.Key ?? string.Empty,
            ClassKey = VehicleClass?.Key ?? string.Empty,
            QualityKey = Quality?.Key ?? string.Empty,
            LandEngineKey = LandEngine?.Key ?? string.Empty,
            LandEngineLevelKey = LandLevel?.Key ?? string.Empty,
            WaterEngineKey = WaterEngine?.Key ?? string.Empty,
            WaterEngineLevelKey = WaterLevel?.Key ?? string.Empty,
            ReactorTypeKey = ReactorType?.Key ?? string.Empty,
            ReactorLevelKey = ReactorLevel?.Key ?? string.Empty,
            PilotSystemKey = PilotSystem?.Key ?? string.Empty,
            PriceTierKey = PriceTier?.Key ?? string.Empty,
            ArmorThicknessPercent = ArmorThickness
        };
        foreach (var sensor in SelectedSensors)
            input.SensorKeys.Add(sensor.Option.Key);
        foreach (var module in SelectedAuxiliaryModules)
            input.AuxiliaryHullModuleKeys.Add(module.Option.Key);
        foreach (var component in BuildSelections())
            input.Components.Add(component);
        return input;
    }

    public void ApplyInput(LandMarineInput input) => ApplyDemo(input);

    private void ApplyDemo(LandMarineInput input)
    {
        _configurationName = input.ConfigurationName;
        _mode = input.Mode;
        _type = Types.FirstOrDefault(item => item.Key == input.TypeKey);
        _size = Sizes.FirstOrDefault(item => item.Key == input.SizeKey);
        RefreshClasses();
        _vehicleClass = Classes.FirstOrDefault(item => item.Key == input.ClassKey);
        _quality = Qualities.FirstOrDefault(item => item.Key == input.QualityKey);
        _landEngine = Engines.FirstOrDefault(item => item.Key == input.LandEngineKey);
        _landLevel = Levels.FirstOrDefault(item => item.Key == input.LandEngineLevelKey);
        _waterEngine = Engines.FirstOrDefault(item => item.Key == input.WaterEngineKey);
        _waterLevel = Levels.FirstOrDefault(item => item.Key == input.WaterEngineLevelKey);
        _reactorType = ReactorTypes.FirstOrDefault(item => item.Key == input.ReactorTypeKey);
        _reactorLevel = Levels.FirstOrDefault(item => item.Key == input.ReactorLevelKey);
        _pilotSystem = PilotSystems.FirstOrDefault(item => item.Key == input.PilotSystemKey);
        _priceTier = PriceTiers.FirstOrDefault(item => item.Key == input.PriceTierKey);
        _armorThickness = input.ArmorThicknessPercent;
        SelectedSensors.Clear();
        foreach (var key in input.SensorKeys)
            SelectedSensors.Add(new ConfiguratorOptionRow(Sensors.First(item => item.Key == key)));
        SelectedAuxiliaryModules.Clear();
        foreach (var key in input.AuxiliaryHullModuleKeys)
            SelectedAuxiliaryModules.Add(new ConfiguratorOptionRow(AuxiliaryModules.First(item => item.Key == key)));
        ReplaceSelections(input.Components, LandMarineCatalog.Index);
        _sensor = Sensors.FirstOrDefault();
        _auxiliaryModule = AuxiliaryModules.FirstOrDefault();
        NotifyAll();
        Recalculate();
    }

    private LandMarineInput CreateEmpty()
    {
        var input = DemoPresets.LandMarine();
        input.ConfigurationName = "Новая техника";
        input.Components.Clear();
        input.SensorKeys.Clear();
        input.AuxiliaryHullModuleKeys.Clear();
        return input;
    }

    private void RefreshClasses()
    {
        Classes.Clear();
        if (_type == null)
            return;
        foreach (var item in LandMarineCatalog.ClassesForType(_type.Key))
            Classes.Add(item);
        if (_vehicleClass == null || Classes.All(item => item.Key != _vehicleClass.Key))
            _vehicleClass = Classes.FirstOrDefault();
        Notify(nameof(VehicleClass));
    }

    private void AddSensor()
    {
        if (Sensor == null || SelectedSensors.Any(item => item.Option.Key == Sensor.Key))
            return;
        SelectedSensors.Add(new ConfiguratorOptionRow(Sensor));
        Recalculate();
    }

    private void AddAuxiliary()
    {
        if (AuxiliaryModule == null || SelectedAuxiliaryModules.Any(item => item.Option.Key == AuxiliaryModule.Key))
            return;
        SelectedAuxiliaryModules.Add(new ConfiguratorOptionRow(AuxiliaryModule));
        Recalculate();
    }

    private void NotifyAll()
    {
        Notify(nameof(ConfigurationName));
        Notify(nameof(SelectedMode));
        Notify(nameof(Type));
        Notify(nameof(Size));
        Notify(nameof(VehicleClass));
        Notify(nameof(Quality));
        Notify(nameof(LandEngine));
        Notify(nameof(LandLevel));
        Notify(nameof(WaterEngine));
        Notify(nameof(WaterLevel));
        Notify(nameof(ReactorType));
        Notify(nameof(ReactorLevel));
        Notify(nameof(PilotSystem));
        Notify(nameof(PriceTier));
        Notify(nameof(ArmorThickness));
        Notify(nameof(ArmorThicknessValue));
        Notify(nameof(Sensor));
        Notify(nameof(AuxiliaryModule));
    }
}

public sealed class BuildingConfiguratorViewModel : AssetConfiguratorToolViewModel
{
    private readonly BuildingCalculatorService _calculator = new BuildingCalculatorService();
    private string _configurationName = "Новое здание";
    private AssetConfiguratorMode _mode;
    private CatalogOption? _buildingType;
    private CatalogOption? _floorSize;
    private int _floorCount = 1;
    private CatalogOption? _constructionMethod;
    private CatalogOption? _hullMaterial;
    private CatalogOption? _armorMaterial;
    private CatalogOption? _shieldMaterial;
    private CatalogOption? _quality;
    private CatalogOption? _reactorType;
    private CatalogOption? _reactorLevel;
    private string _locationDescription = string.Empty;
    private string _purpose = string.Empty;
    private string _gmComment = string.Empty;

    public BuildingConfiguratorViewModel(bool showGmFields = true) : base(BuildingCatalog.Components)
    {
        Modes = SpacecraftConfiguratorViewModelModeOptions.Create();
        BuildingTypes = BuildingCatalog.BuildingTypes;
        FloorSizes = BuildingCatalog.FloorSizes;
        ConstructionMethods = BuildingCatalog.ConstructionMethods;
        HullMaterials = BuildingCatalog.HullMaterials;
        ArmorMaterials = BuildingCatalog.ArmorMaterials;
        ShieldMaterials = BuildingCatalog.ShieldMaterials;
        Qualities = BuildingCatalog.Qualities;
        ReactorTypes = BuildingCatalog.ReactorTypes;
        Levels = BuildingCatalog.Levels;
        GmFieldsVisibility = showGmFields ? Visibility.Visible : Visibility.Collapsed;
        ApplyDemo(DemoPresets.Building());
    }

    public IReadOnlyList<CatalogOption> Modes { get; }
    public IReadOnlyList<CatalogOption> BuildingTypes { get; }
    public IReadOnlyList<CatalogOption> FloorSizes { get; }
    public IReadOnlyList<CatalogOption> ConstructionMethods { get; }
    public IReadOnlyList<CatalogOption> HullMaterials { get; }
    public IReadOnlyList<CatalogOption> ArmorMaterials { get; }
    public IReadOnlyList<CatalogOption> ShieldMaterials { get; }
    public IReadOnlyList<CatalogOption> Qualities { get; }
    public IReadOnlyList<CatalogOption> ReactorTypes { get; }
    public IReadOnlyList<CatalogOption> Levels { get; }
    public Visibility GmFieldsVisibility { get; }
    public string SourceVersion => $"Classic 1.0 · {BuildingCatalog.Source.CommitSha.Substring(0, 10)}";

    public string ConfigurationName { get => _configurationName; set { if (Set(ref _configurationName, value)) Recalculate(); } }
    public CatalogOption? SelectedMode { get => Modes[(int)_mode]; set { if (value != null) { _mode = value.Key.EndsWith("nri", StringComparison.Ordinal) ? AssetConfiguratorMode.NriSystemUsse : AssetConfiguratorMode.Classic; Notify(); Recalculate(); } } }
    public CatalogOption? BuildingType { get => _buildingType; set { if (Set(ref _buildingType, value)) Recalculate(); } }
    public CatalogOption? FloorSize { get => _floorSize; set { if (Set(ref _floorSize, value)) Recalculate(); } }
    public int FloorCount { get => _floorCount; set { if (Set(ref _floorCount, Math.Max(1, value))) Recalculate(); } }
    public decimal? FloorCountValue { get => FloorCount; set { if (value.HasValue) FloorCount = decimal.ToInt32(value.Value); } }
    public CatalogOption? ConstructionMethod { get => _constructionMethod; set { if (Set(ref _constructionMethod, value)) Recalculate(); } }
    public CatalogOption? HullMaterial { get => _hullMaterial; set { if (Set(ref _hullMaterial, value)) Recalculate(); } }
    public CatalogOption? ArmorMaterial { get => _armorMaterial; set { if (Set(ref _armorMaterial, value)) Recalculate(); } }
    public CatalogOption? ShieldMaterial { get => _shieldMaterial; set { if (Set(ref _shieldMaterial, value)) Recalculate(); } }
    public CatalogOption? Quality { get => _quality; set { if (Set(ref _quality, value)) Recalculate(); } }
    public CatalogOption? ReactorType { get => _reactorType; set { if (Set(ref _reactorType, value)) Recalculate(); } }
    public CatalogOption? ReactorLevel { get => _reactorLevel; set { if (Set(ref _reactorLevel, value)) Recalculate(); } }
    public string LocationDescription { get => _locationDescription; set { if (Set(ref _locationDescription, value)) Recalculate(); } }
    public string Purpose { get => _purpose; set { if (Set(ref _purpose, value)) Recalculate(); } }
    public string GmComment { get => _gmComment; set => Set(ref _gmComment, value); }
    protected override string CurrentConfigurationName => ConfigurationName;

    protected override void Recalculate()
    {
        var result = _calculator.Calculate(BuildInput());
        ApplyResult(
            result,
            result.Metrics(),
            $"Внутренние модули {result.InternalSlotsUsed}/{result.InternalSlotsAvailable}; " +
            $"вооружение {result.WeaponSlotsUsed}/{result.WeaponSlotsAvailable}; " +
            $"общая площадь {result.TotalArea:N0} м².");
    }

    protected override void Reset() => ApplyDemo(CreateEmpty());
    protected override void LoadDemo() => ApplyDemo(DemoPresets.Building());

    public BuildingInput BuildInput()
    {
        var input = new BuildingInput
        {
            ConfigurationName = ConfigurationName,
            Mode = _mode,
            BuildingTypeKey = BuildingType?.Key ?? string.Empty,
            FloorSizeKey = FloorSize?.Key ?? string.Empty,
            FloorCount = FloorCount,
            ConstructionMethodKey = ConstructionMethod?.Key ?? string.Empty,
            HullMaterialKey = HullMaterial?.Key ?? string.Empty,
            ArmorMaterialKey = ArmorMaterial?.Key ?? string.Empty,
            ShieldMaterialKey = ShieldMaterial?.Key ?? string.Empty,
            QualityKey = Quality?.Key ?? string.Empty,
            ReactorTypeKey = ReactorType?.Key ?? string.Empty,
            ReactorLevelKey = ReactorLevel?.Key ?? string.Empty,
            LocationDescription = LocationDescription,
            Purpose = Purpose,
            GmComment = GmComment
        };
        foreach (var component in BuildSelections())
            input.Components.Add(component);
        return input;
    }

    public void ApplyInput(BuildingInput input) => ApplyDemo(input);

    private void ApplyDemo(BuildingInput input)
    {
        _configurationName = input.ConfigurationName;
        _mode = input.Mode;
        _buildingType = BuildingTypes.FirstOrDefault(item => item.Key == input.BuildingTypeKey);
        _floorSize = FloorSizes.FirstOrDefault(item => item.Key == input.FloorSizeKey);
        _floorCount = input.FloorCount;
        _constructionMethod = ConstructionMethods.FirstOrDefault(item => item.Key == input.ConstructionMethodKey);
        _hullMaterial = HullMaterials.FirstOrDefault(item => item.Key == input.HullMaterialKey);
        _armorMaterial = ArmorMaterials.FirstOrDefault(item => item.Key == input.ArmorMaterialKey);
        _shieldMaterial = ShieldMaterials.FirstOrDefault(item => item.Key == input.ShieldMaterialKey);
        _quality = Qualities.FirstOrDefault(item => item.Key == input.QualityKey);
        _reactorType = ReactorTypes.FirstOrDefault(item => item.Key == input.ReactorTypeKey);
        _reactorLevel = Levels.FirstOrDefault(item => item.Key == input.ReactorLevelKey);
        _locationDescription = input.LocationDescription;
        _purpose = input.Purpose;
        _gmComment = input.GmComment;
        ReplaceSelections(input.Components, BuildingCatalog.Index);
        NotifyAll();
        Recalculate();
    }

    private BuildingInput CreateEmpty()
    {
        var input = DemoPresets.Building();
        input.ConfigurationName = "Новое здание";
        input.Components.Clear();
        input.LocationDescription = string.Empty;
        input.Purpose = string.Empty;
        input.GmComment = string.Empty;
        return input;
    }

    private void NotifyAll()
    {
        Notify(nameof(ConfigurationName));
        Notify(nameof(SelectedMode));
        Notify(nameof(BuildingType));
        Notify(nameof(FloorSize));
        Notify(nameof(FloorCount));
        Notify(nameof(FloorCountValue));
        Notify(nameof(ConstructionMethod));
        Notify(nameof(HullMaterial));
        Notify(nameof(ArmorMaterial));
        Notify(nameof(ShieldMaterial));
        Notify(nameof(Quality));
        Notify(nameof(ReactorType));
        Notify(nameof(ReactorLevel));
        Notify(nameof(LocationDescription));
        Notify(nameof(Purpose));
        Notify(nameof(GmComment));
    }
}

internal static class SpacecraftConfiguratorViewModelModeOptions
{
    public static IReadOnlyList<CatalogOption> Create() => new[]
    {
        new CatalogOption("mode.classic", "Классический", "mode", "Формулы исходного приложения."),
        new CatalogOption("mode.nri", "NRI System USSE", "mode", "Сопоставление с definitions, где оно доступно.")
    };
}

public abstract class NotifyBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        Notify(propertyName);
        return true;
    }

    protected void Notify([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

internal sealed class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;
    public void Execute(object? parameter) => _execute();
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

internal sealed class RelayCommand<T> : ICommand
{
    private readonly Action<T?> _execute;
    private readonly Func<T?, bool>? _canExecute;

    public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => _canExecute?.Invoke(Convert(parameter)) ?? true;
    public void Execute(object? parameter) => _execute(Convert(parameter));
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);

    private static T? Convert(object? parameter) => parameter is T value ? value : default;
}
