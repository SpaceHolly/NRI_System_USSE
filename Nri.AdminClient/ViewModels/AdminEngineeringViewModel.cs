using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Nri.AdminClient.Diagnostics;
using Nri.AdminClient.Networking;
using Nri.Shared.Contracts;

namespace Nri.AdminClient.ViewModels;

public sealed class AdminEngineeringViewModel : ViewModelBase
{
    private readonly CommandApi _api;
    private string _campaignId = "default";
    private string _ruleSetId = "default";
    private string _statusMessage = "Инженерный конструктор создаёт чертёж, а не готовую технику.";
    private string _errorMessage = string.Empty;
    private EngineeringPlatformUiItem? _selectedPlatform;
    private EngineeringModuleUiItem? _selectedModule;
    private EngineeringProjectUiItem? _selectedProject;

    public AdminEngineeringViewModel(CommandApi api)
    {
        _api = api;
        RefreshCommand = new RelayCommand(RefreshAll);
        CreatePlatformCommand = new RelayCommand(CreatePlatform);
        CreateModuleCommand = new RelayCommand(CreateModule);
        CreatePresetCommand = new RelayCommand(CreatePreset);
        ValidateDesignCommand = new RelayCommand(ValidateDesign);
        CreateProjectCommand = new RelayCommand(CreateProject);
        StartProjectCommand = new RelayCommand(() => ProjectAction(_api.EngineeringProjectStart, "Проект запущен."));
        CompleteProjectCommand = new RelayCommand(() => ProjectAction(_api.EngineeringProjectComplete, "Проект завершён."));
        CancelProjectCommand = new RelayCommand(() => ProjectAction(_api.EngineeringProjectCancel, "Проект отменён."));
        FailProjectCommand = new RelayCommand(() => ProjectAction(_api.EngineeringProjectFail, "Проект провален."));
        AddProgressCommand = new RelayCommand(AddProgress);
        PrepareBlueprintCommand = new RelayCommand(() => ProjectAction(_api.EngineeringBlueprintPrepare, "Чертёж подготовлен."));
        AcceptBlueprintCommand = new RelayCommand(() => ProjectAction(_api.EngineeringBlueprintAccept, "Чертёж принят. Экземпляр техники не создан."));
        ClearErrorCommand = new RelayCommand(() => ErrorMessage = string.Empty);
    }

    public ObservableCollection<EngineeringPlatformUiItem> Platforms { get; } = new();
    public ObservableCollection<EngineeringModuleUiItem> Modules { get; } = new();
    public ObservableCollection<EngineeringPresetUiItem> Presets { get; } = new();
    public ObservableCollection<EngineeringProjectUiItem> Projects { get; } = new();
    public ObservableCollection<string> ValidationRows { get; } = new();

    public ICommand RefreshCommand { get; }
    public ICommand CreatePlatformCommand { get; }
    public ICommand CreateModuleCommand { get; }
    public ICommand CreatePresetCommand { get; }
    public ICommand ValidateDesignCommand { get; }
    public ICommand CreateProjectCommand { get; }
    public ICommand StartProjectCommand { get; }
    public ICommand CompleteProjectCommand { get; }
    public ICommand CancelProjectCommand { get; }
    public ICommand FailProjectCommand { get; }
    public ICommand AddProgressCommand { get; }
    public ICommand PrepareBlueprintCommand { get; }
    public ICommand AcceptBlueprintCommand { get; }
    public ICommand ClearErrorCommand { get; }

    public string CampaignId { get => _campaignId; set { if (_campaignId != value) { _campaignId = value; Notify(); } } }
    public string RuleSetId { get => _ruleSetId; set { if (_ruleSetId != value) { _ruleSetId = value; Notify(); } } }
    public string StatusMessage { get => _statusMessage; private set { if (_statusMessage != value) { _statusMessage = value; Notify(); } } }
    public string ErrorMessage { get => _errorMessage; private set { if (_errorMessage != value) { _errorMessage = value; Notify(); } } }
    public EngineeringPlatformUiItem? SelectedPlatform { get => _selectedPlatform; set { if (_selectedPlatform != value) { _selectedPlatform = value; Notify(); if (value != null) ProjectPlatformId = value.PlatformId; } } }
    public EngineeringModuleUiItem? SelectedModule { get => _selectedModule; set { if (_selectedModule != value) { _selectedModule = value; Notify(); } } }
    public EngineeringProjectUiItem? SelectedProject { get => _selectedProject; set { if (_selectedProject != value) { _selectedProject = value; Notify(); } } }

    public string NewPlatformName { get; set; } = "Новая платформа";
    public string NewPlatformKind { get; set; } = "ground_vehicle";
    public string NewPlatformSizeClass { get; set; } = "medium";
    public string NewPlatformSlots { get; set; } = "8";
    public string NewPlatformHardpoints { get; set; } = "2";
    public string NewPlatformPowerOutput { get; set; } = "100";
    public string NewPlatformCost { get; set; } = "1000";
    public bool NewPlatformVisible { get; set; }

    public string NewModuleName { get; set; } = "Новый модуль";
    public string NewModuleCategory { get; set; } = "utility";
    public string NewModuleSlotType { get; set; } = "internal";
    public string NewModuleSlotCost { get; set; } = "1";
    public string NewModuleHardpointCost { get; set; } = "0";
    public string NewModulePowerLoad { get; set; } = "10";
    public string NewModulePowerOutput { get; set; } = "0";
    public string NewModuleCost { get; set; } = "100";
    public string NewModuleDiceExpression { get; set; } = string.Empty;
    public bool NewModuleVisible { get; set; }

    public string NewPresetName { get; set; } = "Пресет конструкции";
    public string NewPresetRole { get; set; } = "Назначение задаётся модулями.";

    public string ProjectName { get; set; } = "Инженерный проект";
    public string ProjectPlatformId { get; set; } = string.Empty;
    public string ProjectModuleIds { get; set; } = string.Empty;
    public string ProjectOwnerUserId { get; set; } = string.Empty;
    public string ProjectActorEntityId { get; set; } = string.Empty;
    public string ProjectIntendedRole { get; set; } = string.Empty;
    public string ProgressAmount { get; set; } = "10";

    public void RefreshAll()
    {
        Run("admin.engineering.refresh", () =>
        {
            LoadPlatforms();
            LoadModules();
            LoadPresets();
            LoadProjects();
            StatusMessage = "Инженерные данные обновлены.";
        });
    }

    private void LoadPlatforms()
    {
        Platforms.Clear();
        var response = _api.EngineeringPlatformList(BasePayload());
        EnsureOk(response);
        foreach (var map in Items(response)) Platforms.Add(EngineeringPlatformUiItem.From(map));
    }

    private void LoadModules()
    {
        Modules.Clear();
        var response = _api.EngineeringModuleList(BasePayload());
        EnsureOk(response);
        foreach (var map in Items(response)) Modules.Add(EngineeringModuleUiItem.From(map));
    }

    private void LoadPresets()
    {
        Presets.Clear();
        var response = _api.EngineeringPresetList(BasePayload());
        EnsureOk(response);
        foreach (var map in Items(response)) Presets.Add(EngineeringPresetUiItem.From(map));
    }

    private void LoadProjects()
    {
        Projects.Clear();
        var response = _api.EngineeringProjectList(BasePayload());
        EnsureOk(response);
        foreach (var map in Items(response)) Projects.Add(EngineeringProjectUiItem.From(map));
    }

    private void CreatePlatform()
    {
        Run("admin.engineering.platform.create", () =>
        {
            var response = _api.EngineeringPlatformCreate(new Dictionary<string, object>(BasePayload())
            {
                { "name", NewPlatformName },
                { "platformKind", NewPlatformKind },
                { "sizeClassId", NewPlatformSizeClass },
                { "baseSlots", Int(NewPlatformSlots, 8) },
                { "baseHardpoints", Int(NewPlatformHardpoints, 2) },
                { "basePowerOutput", Dec(NewPlatformPowerOutput, 100m) },
                { "baseCost", Dec(NewPlatformCost, 1000m) },
                { "isPlayerVisible", NewPlatformVisible },
                { "visibilityMode", NewPlatformVisible ? "player_visible" : "gm_only" }
            });
            EnsureOk(response);
            LoadPlatforms();
        });
    }

    private void CreateModule()
    {
        Run("admin.engineering.module.create", () =>
        {
            var response = _api.EngineeringModuleCreate(new Dictionary<string, object>(BasePayload())
            {
                { "name", NewModuleName },
                { "moduleCategory", NewModuleCategory },
                { "slotType", NewModuleSlotType },
                { "slotCost", Int(NewModuleSlotCost, 1) },
                { "hardpointCost", Int(NewModuleHardpointCost, 0) },
                { "powerLoad", Dec(NewModulePowerLoad, 10m) },
                { "powerOutput", Dec(NewModulePowerOutput, 0m) },
                { "cost", Dec(NewModuleCost, 100m) },
                { "diceExpression", NewModuleDiceExpression },
                { "isPlayerVisible", NewModuleVisible },
                { "visibilityMode", NewModuleVisible ? "player_visible" : "gm_only" }
            });
            EnsureOk(response);
            LoadModules();
        });
    }

    private void CreatePreset()
    {
        Run("admin.engineering.preset.create", () =>
        {
            var response = _api.EngineeringPresetCreate(new Dictionary<string, object>(BasePayload())
            {
                { "name", NewPresetName },
                { "platformId", ProjectPlatformId },
                { "moduleIds", ModuleIds() },
                { "roleSummary", NewPresetRole },
                { "isPlayerVisible", true },
                { "visibilityMode", "player_visible" }
            });
            EnsureOk(response);
            LoadPresets();
        });
    }

    private void ValidateDesign()
    {
        Run("admin.engineering.validate", () =>
        {
            ValidationRows.Clear();
            var response = _api.EngineeringDesignValidate(new Dictionary<string, object>(BasePayload())
            {
                { "platformId", ProjectPlatformId },
                { "moduleIds", ModuleIds() },
                { "allowPowerOverload", true }
            });
            EnsureOk(response);
            var validation = Dict(Get(response.Payload, "validation"));
            ValidationRows.Add(Str(Get(validation, "summary"), "Проверка выполнена."));
            foreach (var issue in List(Get(validation, "issues")).Select(Dict))
                ValidationRows.Add($"{Str(Get(issue, "severity"))}: {Str(Get(issue, "message"))}");
            var cost = Dict(Get(response.Payload, "costEstimate"));
            if (cost.Count > 0) ValidationRows.Add($"Оценка стоимости: {Str(Get(cost, "totalCost"), "0")}; дней: {Str(Get(cost, "estimatedWorkDays"), "—")}");
        });
    }

    private void CreateProject()
    {
        Run("admin.engineering.project.create", () =>
        {
            var response = _api.EngineeringProjectCreate(new Dictionary<string, object>(BasePayload())
            {
                { "name", ProjectName },
                { "platformId", ProjectPlatformId },
                { "moduleIds", ModuleIds() },
                { "ownerUserId", ProjectOwnerUserId },
                { "actorEntityId", ProjectActorEntityId },
                { "intendedRole", ProjectIntendedRole },
                { "publicNotes", "Проектирование чертежа. Производство будет отдельным этапом." }
            });
            EnsureOk(response);
            LoadProjects();
        });
    }

    private void AddProgress()
    {
        if (SelectedProject == null) return;
        Run("admin.engineering.progress", () =>
        {
            var response = _api.EngineeringProjectProgressAdd(new Dictionary<string, object>
            {
                { "engineeringProjectId", SelectedProject.EngineeringProjectId },
                { "progressDelta", Int(ProgressAmount, 10) }
            });
            EnsureOk(response);
            LoadProjects();
        });
    }

    private void ProjectAction(Func<Dictionary<string, object>, ResponseEnvelope> action, string success)
    {
        if (SelectedProject == null) return;
        Run("admin.engineering.project.action", () =>
        {
            var response = action(new Dictionary<string, object> { { "engineeringProjectId", SelectedProject.EngineeringProjectId } });
            EnsureOk(response);
            StatusMessage = success;
            LoadProjects();
        });
    }

    private Dictionary<string, object> BasePayload() => new() { { "campaignId", CampaignId }, { "ruleSetId", RuleSetId }, { "includeArchived", false } };
    private object[] ModuleIds() => ProjectModuleIds.Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(x => x.Length > 0).Cast<object>().ToArray();

    private void Run(string scope, Action action)
    {
        try
        {
            ErrorMessage = string.Empty;
            ClientLogService.Instance.Info(scope + ".start");
            action();
            ClientLogService.Instance.Info(scope + ".done");
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            StatusMessage = "Инженерный MVP недоступен или выключен флагами функций.";
            ClientLogService.Instance.Error(scope + ".error " + ex.Message, ex);
        }
    }

    private static void EnsureOk(ResponseEnvelope response)
    {
        if (response.Status != ResponseStatus.Ok) throw new InvalidOperationException(response.Message);
    }

    private static IEnumerable<Dictionary<string, object>> Items(ResponseEnvelope response) => List(Get(response.Payload, "items")).Select(Dict);
    internal static object? Get(Dictionary<string, object> map, string key) => map.TryGetValue(key, out var value) ? value : null;
    internal static Dictionary<string, object> Dict(object? raw)
    {
        if (raw is Dictionary<string, object> typed) return typed;
        if (raw is IDictionary dictionary)
        {
            var result = new Dictionary<string, object>();
            foreach (DictionaryEntry entry in dictionary)
            {
                var key = Convert.ToString(entry.Key);
                if (!string.IsNullOrWhiteSpace(key)) result[key] = entry.Value!;
            }
            return result;
        }
        return new Dictionary<string, object>();
    }

    internal static IEnumerable<object> List(object? raw)
    {
        if (raw is IEnumerable enumerable && raw is not string)
        {
            foreach (var item in enumerable) yield return item!;
        }
    }

    internal static string Str(object? raw, string fallback = "") => string.IsNullOrWhiteSpace(Convert.ToString(raw)) ? fallback : Convert.ToString(raw)!;
    internal static int Int(string raw, int fallback) => int.TryParse(raw, out var value) ? value : fallback;
    internal static decimal Dec(string raw, decimal fallback) => decimal.TryParse(raw, out var value) ? value : fallback;
}

public sealed class EngineeringPlatformUiItem
{
    public string PlatformId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string PlatformKind { get; set; } = string.Empty;
    public string SizeClassId { get; set; } = string.Empty;
    public string Summary => $"{Name} • {PlatformKind} • {SizeClassId}";
    public static EngineeringPlatformUiItem From(Dictionary<string, object> map) => new()
    {
        PlatformId = AdminEngineeringViewModel.Str(AdminEngineeringViewModel.Get(map, "platformId"), AdminEngineeringViewModel.Str(AdminEngineeringViewModel.Get(map, "id"))),
        Name = AdminEngineeringViewModel.Str(AdminEngineeringViewModel.Get(map, "name"), "Платформа"),
        PlatformKind = AdminEngineeringViewModel.Str(AdminEngineeringViewModel.Get(map, "platformKind"), "custom"),
        SizeClassId = AdminEngineeringViewModel.Str(AdminEngineeringViewModel.Get(map, "sizeClassId"), "medium")
    };
}

public sealed class EngineeringModuleUiItem
{
    public string ModuleId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string DiceExpression { get; set; } = string.Empty;
    public string Summary => string.IsNullOrWhiteSpace(DiceExpression) ? $"{Name} • {Category}" : $"{Name} • {Category} • {DiceExpression}";
    public static EngineeringModuleUiItem From(Dictionary<string, object> map) => new()
    {
        ModuleId = AdminEngineeringViewModel.Str(AdminEngineeringViewModel.Get(map, "moduleId"), AdminEngineeringViewModel.Str(AdminEngineeringViewModel.Get(map, "id"))),
        Name = AdminEngineeringViewModel.Str(AdminEngineeringViewModel.Get(map, "name"), "Модуль"),
        Category = AdminEngineeringViewModel.Str(AdminEngineeringViewModel.Get(map, "moduleCategory"), "custom"),
        DiceExpression = AdminEngineeringViewModel.Str(AdminEngineeringViewModel.Get(map, "diceExpression"))
    };
}

public sealed class EngineeringPresetUiItem
{
    public string Name { get; set; } = string.Empty;
    public string PlatformId { get; set; } = string.Empty;
    public string Summary => $"{Name} • {PlatformId}";
    public static EngineeringPresetUiItem From(Dictionary<string, object> map) => new()
    {
        Name = AdminEngineeringViewModel.Str(AdminEngineeringViewModel.Get(map, "name"), "Пресет"),
        PlatformId = AdminEngineeringViewModel.Str(AdminEngineeringViewModel.Get(map, "platformId"))
    };
}

public sealed class EngineeringProjectUiItem
{
    public string EngineeringProjectId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Progress { get; set; } = "0";
    public string BlueprintStatus { get; set; } = string.Empty;
    public string Summary => $"{Name} • {Status} • {Progress}% • blueprint: {BlueprintStatus}";
    public static EngineeringProjectUiItem From(Dictionary<string, object> map) => new()
    {
        EngineeringProjectId = AdminEngineeringViewModel.Str(AdminEngineeringViewModel.Get(map, "engineeringProjectId"), AdminEngineeringViewModel.Str(AdminEngineeringViewModel.Get(map, "id"))),
        Name = AdminEngineeringViewModel.Str(AdminEngineeringViewModel.Get(map, "name"), "Инженерный проект"),
        Status = AdminEngineeringViewModel.Str(AdminEngineeringViewModel.Get(map, "status"), "draft"),
        Progress = AdminEngineeringViewModel.Str(AdminEngineeringViewModel.Get(map, "progressPercent"), "0"),
        BlueprintStatus = AdminEngineeringViewModel.Str(AdminEngineeringViewModel.Get(map, "blueprintStatus"), "draft")
    };
}
