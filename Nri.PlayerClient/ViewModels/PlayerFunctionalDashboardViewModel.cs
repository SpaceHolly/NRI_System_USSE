using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Nri.PlayerClient.Diagnostics;
using Nri.PlayerClient.Networking;
using Nri.Shared.Contracts;

namespace Nri.PlayerClient.ViewModels;

public sealed class PlayerFunctionalDashboardViewModel : ViewModelBase
{
    private readonly CommandApi _api;
    private bool _isEnabled;
    private string _statusText = "Игровой центр не загружен.";
    private string _lastRefreshText = "—";

    public PlayerFunctionalDashboardViewModel(CommandApi api)
    {
        _api = api;
        RefreshCommand = new RelayCommand(Refresh);
    }

    public ObservableCollection<DashboardMetricVm> Metrics { get; } = new();
    public ObservableCollection<DashboardProcessVm> ActiveProcesses { get; } = new();
    public ObservableCollection<DashboardActionVm> NextActions { get; } = new();
    public ObservableCollection<DashboardCharacterVm> CharacterCards { get; } = new();
    public ObservableCollection<string> Warnings { get; } = new();
    public ICommand RefreshCommand { get; }

    public bool IsEnabled { get => _isEnabled; private set { if (_isEnabled != value) { _isEnabled = value; Notify(); } } }
    public string StatusText { get => _statusText; private set { if (_statusText != value) { _statusText = value; Notify(); } } }
    public string LastRefreshText { get => _lastRefreshText; private set { if (_lastRefreshText != value) { _lastRefreshText = value; Notify(); } } }

    public void Refresh()
    {
        try
        {
            ClientLogService.Instance.Info("player.functional.dashboard.load.start");
            var response = _api.PlayerDashboardGet();
            ApplyPayload(response);
            ClientLogService.Instance.Info($"player.functional.dashboard.load.done enabled={IsEnabled} metrics={Metrics.Count} processes={ActiveProcesses.Count} actions={NextActions.Count}");
        }
        catch (Exception ex)
        {
            StatusText = "Не удалось загрузить пульт игрока.";
            Warnings.Clear();
            Warnings.Add(ex.Message);
            ClientLogService.Instance.Error("player.functional.dashboard.load.error " + ex.Message);
        }
    }

    private void ApplyPayload(ResponseEnvelope response)
    {
        Metrics.Clear();
        ActiveProcesses.Clear();
        NextActions.Clear();
        CharacterCards.Clear();
        Warnings.Clear();

        if (response.Status != ResponseStatus.Ok)
        {
            IsEnabled = false;
            StatusText = PlayerFacingMessage(response.Message, "Игровой центр временно недоступен.");
            return;
        }

        var payload = response.Payload ?? new Dictionary<string, object>();
        IsEnabled = Bool(payload, "isEnabled");
        StatusText = PlayerFacingMessage(String(payload, "message", IsEnabled ? "Игровой центр активен." : response.Message), IsEnabled ? "Игровой центр активен." : "Игровой центр пока недоступен.");
        LastRefreshText = DateTime.Now.ToString("HH:mm:ss");

        foreach (var item in Items(payload, "metrics"))
        {
            var map = Map(item);
            Metrics.Add(new DashboardMetricVm
            {
                Label = String(map, "label", "Показатель"),
                Value = String(map, "value", "0"),
                Hint = String(map, "hint", string.Empty)
            });
        }

        foreach (var item in Items(payload, "activeProcesses"))
        {
            var map = Map(item);
            ActiveProcesses.Add(new DashboardProcessVm
            {
                Type = String(map, "type", "Процесс"),
                Title = String(map, "title", "Без названия"),
                Status = String(map, "status", "—"),
                Progress = String(map, "progress", "—"),
                Summary = String(map, "summary", string.Empty)
            });
        }

        foreach (var item in Items(payload, "nextActions"))
        {
            var map = Map(item);
            NextActions.Add(new DashboardActionVm
            {
                Title = String(map, "title", "Следующее действие"),
                Subject = String(map, "subject", "Без названия"),
                Priority = String(map, "priority", "обычно"),
                ActionLabel = String(map, "actionLabel", "Открыть")
            });
        }

        foreach (var item in Items(payload, "characterCards"))
        {
            var map = Map(item);
            CharacterCards.Add(new DashboardCharacterVm
            {
                Name = String(map, "name", "Без имени"),
                Race = String(map, "race", "—"),
                Health = String(map, "health", "—"),
                Armor = String(map, "armor", "—"),
                XpCoins = String(map, "xpCoins", "0"),
                Summary = String(map, "summary", "Данные персонажа не загружены.")
            });
        }

        foreach (var item in Items(payload, "warnings"))
        {
            var text = item?.ToString();
            if (!string.IsNullOrWhiteSpace(text)) Warnings.Add(text);
        }
    }

    private static IEnumerable<object> Items(Dictionary<string, object> map, string key)
        => map.TryGetValue(key, out var value) && value is IEnumerable enumerable && value is not string
            ? enumerable.Cast<object>()
            : Enumerable.Empty<object>();

    private static Dictionary<string, object> Map(object? value)
        => value as Dictionary<string, object> ?? new Dictionary<string, object>();

    private static string String(Dictionary<string, object> map, string key, string fallback)
        => map.TryGetValue(key, out var value) && value != null && !string.IsNullOrWhiteSpace(value.ToString())
            ? value.ToString() ?? fallback
            : fallback;

    private static bool Bool(Dictionary<string, object> map, string key)
        => map.TryGetValue(key, out var value) && value is bool b && b;

    private static string PlayerFacingMessage(string? message, string fallback)
    {
        if (string.IsNullOrWhiteSpace(message)) return fallback;
        if (message.IndexOf("feature flag", StringComparison.OrdinalIgnoreCase) >= 0 ||
            message.IndexOf("ClientFunctionalization", StringComparison.OrdinalIgnoreCase) >= 0 ||
            message.IndexOf("flags", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Игровой центр пока недоступен в этой кампании.";
        return message;
    }
}

public sealed class DashboardMetricVm
{
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Hint { get; set; } = string.Empty;
}

public sealed class DashboardProcessVm
{
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Progress { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
}

public sealed class DashboardActionVm
{
    public string Title { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string ActionLabel { get; set; } = string.Empty;
}

public sealed class DashboardCharacterVm
{
    public string Name { get; set; } = string.Empty;
    public string Race { get; set; } = string.Empty;
    public string Health { get; set; } = string.Empty;
    public string Armor { get; set; } = string.Empty;
    public string XpCoins { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
}
