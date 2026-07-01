using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Nri.AdminClient.Networking;
using Nri.Shared.Contracts;

namespace Nri.AdminClient.ViewModels;

public sealed class AdminFateControlViewModel : ViewModelBase
{
    private readonly CommandApi _api;
    private bool _isBusy;
    private bool _engineEnabled;
    private string _activeProfileId = "fate_acceptance_profile_01457";
    private string _terrainProfile = "calm";
    private string _statusText = "Пульт Fate готов к подключению.";
    private string _simulationBaseRoll = "10";
    private string _simulationSeed = "1457";
    private string _simulationCharacterId = string.Empty;
    private string _simulationSkillId = "dev_acceptance_skill_01451";
    private FatePanelUiItem? _selectedPanel;
    private FateRollLogUiItem? _selectedRoll;

    public AdminFateControlViewModel(CommandApi api)
    {
        _api = api;
        RefreshCommand = new RelayCommand(Refresh);
        SeedAcceptanceCommand = new RelayCommand(SeedAcceptanceData);
        SaveStateCommand = new RelayCommand(SaveState);
        RunSimulationCommand = new RelayCommand(RunSimulation);
        MovePanelUpCommand = new RelayCommand(MoveSelectedPanelUp);
        MovePanelDownCommand = new RelayCommand(MoveSelectedPanelDown);
        TogglePanelVisibilityCommand = new RelayCommand(ToggleSelectedPanelVisibility);
        SaveLayoutCommand = new RelayCommand(SaveLayout);
        ResetLayoutCommand = new RelayCommand(ResetLayout);
    }

    public ObservableCollection<FateSimpleRow> Profiles { get; } = new();
    public ObservableCollection<FateSimpleRow> LayerRules { get; } = new();
    public ObservableCollection<FateSimpleRow> ModifierRules { get; } = new();
    public ObservableCollection<FateRollLogUiItem> RecentRolls { get; } = new();
    public ObservableCollection<FatePanelUiItem> Panels { get; } = new();
    public ObservableCollection<string> TerrainOptions { get; } = new()
    {
        "calm", "battle", "cursed_land", "blessed_land", "hell", "chaos", "drama", "key_moment", "anomalous_space"
    };
    public ObservableCollection<string> DockAreaOptions { get; } = new() { "left", "right", "center", "bottom", "floating" };

    public ICommand RefreshCommand { get; }
    public ICommand SeedAcceptanceCommand { get; }
    public ICommand SaveStateCommand { get; }
    public ICommand RunSimulationCommand { get; }
    public ICommand MovePanelUpCommand { get; }
    public ICommand MovePanelDownCommand { get; }
    public ICommand TogglePanelVisibilityCommand { get; }
    public ICommand SaveLayoutCommand { get; }
    public ICommand ResetLayoutCommand { get; }

    public bool IsBusy { get => _isBusy; private set { if (_isBusy != value) { _isBusy = value; Notify(); } } }
    public bool EngineEnabled { get => _engineEnabled; set { if (_engineEnabled != value) { _engineEnabled = value; Notify(); } } }
    public string ActiveProfileId { get => _activeProfileId; set { if (_activeProfileId != value) { _activeProfileId = value ?? string.Empty; Notify(); } } }
    public string TerrainProfile { get => _terrainProfile; set { if (_terrainProfile != value) { _terrainProfile = value ?? string.Empty; Notify(); } } }
    public string StatusText { get => _statusText; private set { if (_statusText != value) { _statusText = value; Notify(); } } }
    public string SimulationBaseRoll { get => _simulationBaseRoll; set { if (_simulationBaseRoll != value) { _simulationBaseRoll = value ?? string.Empty; Notify(); } } }
    public string SimulationSeed { get => _simulationSeed; set { if (_simulationSeed != value) { _simulationSeed = value ?? string.Empty; Notify(); } } }
    public string SimulationCharacterId { get => _simulationCharacterId; set { if (_simulationCharacterId != value) { _simulationCharacterId = value ?? string.Empty; Notify(); } } }
    public string SimulationSkillId { get => _simulationSkillId; set { if (_simulationSkillId != value) { _simulationSkillId = value ?? string.Empty; Notify(); } } }
    public string ConfidenceSummary { get; private set; } = "-";
    public string SelectedRollBaseResult => SelectedRoll?.BaseResult ?? "-";
    public string SelectedRollFinalResult => SelectedRoll?.FinalResult ?? "-";
    public string SelectedRollModifiers => SelectedRoll?.Modifiers ?? "-";
    public string SelectedRollLayers => SelectedRoll?.Layers ?? "-";
    public string PanelSummaryText => Panels.Count == 0
        ? "Панели не загружены"
        : string.Join(" / ", Panels.OrderBy(x => x.Order).Select(x => x.DisplayName));
    public FatePanelUiItem? SelectedPanel
    {
        get => _selectedPanel;
        set
        {
            if (_selectedPanel == value) return;
            _selectedPanel = value;
            Notify();
            Notify(nameof(SelectedPanelId));
        }
    }
    public string SelectedPanelId
    {
        get => SelectedPanel?.PanelId ?? string.Empty;
        set
        {
            var requested = value ?? string.Empty;
            var match = Panels.FirstOrDefault(x =>
                string.Equals(x.PanelId, requested, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.DisplayName, requested, StringComparison.OrdinalIgnoreCase));
            if (match != null) SelectedPanel = match;
            else Notify();
        }
    }
    public FateRollLogUiItem? SelectedRoll
    {
        get => _selectedRoll;
        set
        {
            if (_selectedRoll == value) return;
            _selectedRoll = value;
            Notify();
            Notify(nameof(SelectedRollBaseResult));
            Notify(nameof(SelectedRollFinalResult));
            Notify(nameof(SelectedRollModifiers));
            Notify(nameof(SelectedRollLayers));
        }
    }

    public void Refresh()
    {
        Run("Обновление Fate Control", () =>
        {
            var state = _api.FateAdminStateGet();
            if (state.Status != ResponseStatus.Ok) { StatusText = state.Message; return; }
            var stateMap = Map(state.Payload, "state");
            EngineEnabled = B(stateMap, "IsEnabled");
            ActiveProfileId = S(stateMap, "ActiveProfileId");
            TerrainProfile = S(stateMap, "TerrainProfile", "calm");

            Profiles.ReplaceWith(ReadRows(_api.FateAdminProfileList(), "items", "ProfileId", "DisplayName", "Description"));
            LayerRules.ReplaceWith(ReadRows(_api.FateAdminLayerRulesList(), "items", "LayerId", "LayerName", "DistributionMode"));
            ModifierRules.ReplaceWith(ReadRows(_api.FateAdminModifierRulesList(), "items", "RuleId", "SourceType", "ReasonToken"));
            RecentRolls.ReplaceWith(ReadRolls(_api.FateAdminRollLogsList(50)));
            LoadLayout();
            var confidence = _api.FateAdminConfidenceGet();
            if (confidence.Status == ResponseStatus.Ok)
                ConfidenceSummary = $"{S(confidence.Payload, "summary")} recent={S(confidence.Payload, "recentRollCount")}";
            Notify(nameof(ConfidenceSummary));
            StatusText = "Fate Control обновлён.";
        });
    }

    private void SeedAcceptanceData()
    {
        Run("Seed Fate acceptance", () =>
        {
            var response = _api.FateAdminSeedAcceptanceData(SimulationCharacterId);
            StatusText = response.Message;
            Refresh();
        });
    }

    private void SaveState()
    {
        Run("Сохранение состояния Fate", () =>
        {
            var response = _api.FateAdminStateUpdate(new Dictionary<string, object>
            {
                ["isEnabled"] = EngineEnabled,
                ["activeProfileId"] = string.IsNullOrWhiteSpace(ActiveProfileId) ? "fate_acceptance_profile_01457" : ActiveProfileId,
                ["terrainProfile"] = string.IsNullOrWhiteSpace(TerrainProfile) ? "calm" : TerrainProfile,
                ["confidenceMode"] = "enabled"
            });
            StatusText = response.Message;
            Refresh();
        });
    }

    private void RunSimulation()
    {
        Run("Fate simulation", () =>
        {
            int.TryParse(SimulationBaseRoll, out var baseRoll);
            int.TryParse(SimulationSeed, out var seed);
            var response = _api.FateAdminSimulateRoll(new Dictionary<string, object>
            {
                ["baseRoll"] = baseRoll <= 0 ? 10 : baseRoll,
                ["dieSides"] = 20,
                ["seed"] = seed,
                ["characterId"] = SimulationCharacterId ?? string.Empty,
                ["skillId"] = string.IsNullOrWhiteSpace(SimulationSkillId) ? "dev_acceptance_skill_01451" : SimulationSkillId,
                ["subAttributeId"] = "dev_acceptance_subattribute_01451"
            });
            StatusText = response.Message;
            RecentRolls.ReplaceWith(ReadRolls(_api.FateAdminRollLogsList(50)));
        });
    }

    private void LoadLayout()
    {
        var response = _api.FateControlLayoutGet(new Dictionary<string, object> { ["client"] = "AdminClient", ["layoutId"] = "default" });
        if (response.Status != ResponseStatus.Ok) return;
        var layout = Map(response.Payload, "layout");
        Panels.ReplaceWith(ReadPanels(layout));
        SelectedPanel ??= Panels.FirstOrDefault();
        Notify(nameof(PanelSummaryText));
    }

    private void SaveLayout()
    {
        Run("Сохранение раскладки Fate", () =>
        {
            var panels = Panels.OrderBy(x => x.Order).Select(x => new Dictionary<string, object>
            {
                ["PanelId"] = x.PanelId,
                ["DisplayName"] = x.DisplayName,
                ["IsVisible"] = x.IsVisible,
                ["DockArea"] = x.DockArea,
                ["Order"] = x.Order,
                ["Width"] = x.Width,
                ["Height"] = x.Height,
                ["IsCollapsed"] = x.IsCollapsed,
                ["Column"] = x.Column,
                ["Row"] = x.Row
            }).Cast<object>().ToArray();
            var response = _api.FateControlLayoutSave(new Dictionary<string, object> { ["client"] = "AdminClient", ["layoutId"] = "default", ["panels"] = panels });
            StatusText = response.Message;
            LoadLayout();
        });
    }

    private void ResetLayout()
    {
        Run("Сброс раскладки Fate", () =>
        {
            var response = _api.FateControlLayoutReset(new Dictionary<string, object> { ["client"] = "AdminClient", ["layoutId"] = "default" });
            StatusText = response.Message;
            LoadLayout();
        });
    }

    private void MoveSelectedPanelUp()
    {
        if (SelectedPanel == null) return;
        var sorted = Panels.OrderBy(x => x.Order).ToList();
        var index = sorted.IndexOf(SelectedPanel);
        if (index <= 0) return;
        (sorted[index - 1].Order, sorted[index].Order) = (sorted[index].Order, sorted[index - 1].Order);
        Panels.ReplaceWith(sorted.OrderBy(x => x.Order));
        SelectedPanel = Panels.FirstOrDefault(x => x.PanelId == sorted[index].PanelId);
    }

    private void MoveSelectedPanelDown()
    {
        if (SelectedPanel == null) return;
        var sorted = Panels.OrderBy(x => x.Order).ToList();
        var index = sorted.IndexOf(SelectedPanel);
        if (index < 0 || index >= sorted.Count - 1) return;
        (sorted[index + 1].Order, sorted[index].Order) = (sorted[index].Order, sorted[index + 1].Order);
        Panels.ReplaceWith(sorted.OrderBy(x => x.Order));
        SelectedPanel = Panels.FirstOrDefault(x => x.PanelId == sorted[index].PanelId);
    }

    private void ToggleSelectedPanelVisibility()
    {
        if (SelectedPanel == null) return;
        SelectedPanel.IsVisible = !SelectedPanel.IsVisible;
    }

    private void Run(string action, Action body)
    {
        try
        {
            IsBusy = true;
            StatusText = $"{action}...";
            body();
        }
        catch (Exception ex)
        {
            StatusText = $"{action}: ошибка: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static IEnumerable<FateSimpleRow> ReadRows(ResponseEnvelope response, string key, string idKey, string nameKey, string extraKey)
    {
        if (response.Status != ResponseStatus.Ok) return Array.Empty<FateSimpleRow>();
        return List(response.Payload, key).Select(x =>
        {
            var map = AsMap(x);
            return new FateSimpleRow { Id = S(map, idKey, S(map, "Id")), Name = S(map, nameKey, S(map, "DisplayName", S(map, "LayerName"))), Extra = S(map, extraKey) };
        });
    }

    private static IEnumerable<FateRollLogUiItem> ReadRolls(ResponseEnvelope response)
    {
        if (response.Status != ResponseStatus.Ok) return Array.Empty<FateRollLogUiItem>();
        return List(response.Payload, "items").Select(x =>
        {
            var map = AsMap(x);
            return new FateRollLogUiItem
            {
                RollId = S(map, "RollId", S(map, "Id")),
                RollType = S(map, "RollType"),
                Actor = S(map, "ActorDisplayName"),
                BaseResult = S(map, "VisibleBaseTotal", S(map, "BaseRandomResult")),
                FinalResult = S(map, "FinalVisibleResult"),
                Modifiers = string.Join("; ", List(map, "AppliedModifiers").Select(m => $"{S(AsMap(m), "SourceType")} {S(AsMap(m), "Value")} {S(AsMap(m), "Reason")}")),
                Layers = string.Join("; ", List(map, "AppliedLayers").Select(m => $"{S(AsMap(m), "LayerName")} {S(AsMap(m), "Modifier")}")),
                Created = S(map, "CreatedAtUtc")
            };
        });
    }

    private static IEnumerable<FatePanelUiItem> ReadPanels(Dictionary<string, object> layout)
    {
        return List(layout, "Panels").Select(x =>
        {
            var map = AsMap(x);
            return new FatePanelUiItem
            {
                PanelId = S(map, "PanelId"),
                DisplayName = S(map, "DisplayName"),
                IsVisible = B(map, "IsVisible", true),
                DockArea = S(map, "DockArea", "center"),
                Order = I(map, "Order", 0),
                Width = I(map, "Width", 320),
                Height = I(map, "Height", 220),
                IsCollapsed = B(map, "IsCollapsed"),
                Column = I(map, "Column", 0),
                Row = I(map, "Row", 0)
            };
        }).OrderBy(x => x.Order).ToArray();
    }

    private static Dictionary<string, object> Map(Dictionary<string, object> payload, string key) => AsMap(payload.TryGetValue(key, out var value) ? value : new Dictionary<string, object>());

    private static Dictionary<string, object> AsMap(object? value)
    {
        if (value is Dictionary<string, object> map) return map;
        if (value is IDictionary<string, object> dict) return dict.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
        if (value is IDictionary generic)
        {
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry entry in generic) result[Convert.ToString(entry.Key) ?? string.Empty] = entry.Value ?? string.Empty;
            return result;
        }
        return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<object> List(Dictionary<string, object> map, string key)
    {
        if (!map.TryGetValue(key, out var value) || value == null) return Array.Empty<object>();
        if (value is object[] array) return array;
        if (value is IEnumerable enumerable && value is not string) return enumerable.Cast<object>();
        return Array.Empty<object>();
    }

    private static string S(Dictionary<string, object> map, string key, string fallback = "")
    {
        if (!map.TryGetValue(key, out var value) || value == null) return fallback;
        return Convert.ToString(value) ?? fallback;
    }

    private static bool B(Dictionary<string, object> map, string key, bool fallback = false)
    {
        if (!map.TryGetValue(key, out var value) || value == null) return fallback;
        if (value is bool b) return b;
        return bool.TryParse(Convert.ToString(value), out var parsed) ? parsed : fallback;
    }

    private static int I(Dictionary<string, object> map, string key, int fallback)
    {
        if (!map.TryGetValue(key, out var value) || value == null) return fallback;
        return int.TryParse(Convert.ToString(value), out var parsed) ? parsed : fallback;
    }
}

public sealed class FateSimpleRow
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Extra { get; set; } = string.Empty;
}

public sealed class FateRollLogUiItem
{
    public string RollId { get; set; } = string.Empty;
    public string RollType { get; set; } = string.Empty;
    public string Actor { get; set; } = string.Empty;
    public string BaseResult { get; set; } = string.Empty;
    public string FinalResult { get; set; } = string.Empty;
    public string Modifiers { get; set; } = string.Empty;
    public string Layers { get; set; } = string.Empty;
    public string Created { get; set; } = string.Empty;
}

public sealed class FatePanelUiItem : ViewModelBase
{
    private bool _isVisible;
    private string _dockArea = "center";
    public string PanelId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsVisible { get => _isVisible; set { if (_isVisible != value) { _isVisible = value; Notify(); } } }
    public string DockArea { get => _dockArea; set { if (_dockArea != value) { _dockArea = value ?? "center"; Notify(); } } }
    public int Order { get; set; }
    public int Width { get; set; } = 320;
    public int Height { get; set; } = 220;
    public bool IsCollapsed { get; set; }
    public int Column { get; set; }
    public int Row { get; set; }
}

internal static class ObservableCollectionExtensions
{
    public static void ReplaceWith<T>(this ObservableCollection<T> target, IEnumerable<T> source)
    {
        target.Clear();
        foreach (var item in source) target.Add(item);
    }
}


