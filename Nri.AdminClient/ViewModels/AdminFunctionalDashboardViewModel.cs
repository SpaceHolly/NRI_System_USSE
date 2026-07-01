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

public sealed class AdminFunctionalDashboardViewModel : ViewModelBase
{
    private readonly CommandApi _api;
    private bool _isEnabled;
    private string _statusText = "Функциональный GM-пульт не загружен.";
    private string _lastRefreshText = "—";

    public AdminFunctionalDashboardViewModel(CommandApi api)
    {
        _api = api;
        RefreshCommand = new RelayCommand(Refresh);
    }

    public ObservableCollection<DashboardMetricVm> Metrics { get; } = new();
    public ObservableCollection<DashboardProcessVm> ActiveProcesses { get; } = new();
    public ObservableCollection<DashboardActionVm> NextActions { get; } = new();
    public ObservableCollection<string> Warnings { get; } = new();
    public ICommand RefreshCommand { get; }

    public bool IsEnabled { get => _isEnabled; private set { if (_isEnabled != value) { _isEnabled = value; Notify(); } } }
    public string StatusText { get => _statusText; private set { if (_statusText != value) { _statusText = value; Notify(); } } }
    public string LastRefreshText { get => _lastRefreshText; private set { if (_lastRefreshText != value) { _lastRefreshText = value; Notify(); } } }

    public void Refresh()
    {
        try
        {
            ClientLogService.Instance.Info("admin.functional.dashboard.load.start");
            var response = _api.AdminDashboardGet();
            ApplyPayload(response);
            ClientLogService.Instance.Info($"admin.functional.dashboard.load.done enabled={IsEnabled} metrics={Metrics.Count} processes={ActiveProcesses.Count} actions={NextActions.Count}");
        }
        catch (Exception ex)
        {
            StatusText = "Не удалось загрузить функциональный GM-пульт.";
            Warnings.Clear();
            Warnings.Add(ex.Message);
            ClientLogService.Instance.Error("admin.functional.dashboard.load.error " + ex.Message);
        }
    }

    private void ApplyPayload(ResponseEnvelope response)
    {
        Metrics.Clear();
        ActiveProcesses.Clear();
        NextActions.Clear();
        Warnings.Clear();

        if (response.Status != ResponseStatus.Ok)
        {
            IsEnabled = false;
            StatusText = string.IsNullOrWhiteSpace(response.Message) ? "Пульт временно недоступен." : response.Message;
            return;
        }

        var payload = response.Payload ?? new Dictionary<string, object>();
        IsEnabled = Bool(payload, "isEnabled");
        StatusText = String(payload, "message", IsEnabled ? "Функциональный GM-пульт активен." : response.Message);
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
                Summary = String(map, "summary", string.Empty),
                Target = String(map, "target", string.Empty)
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
                ActionLabel = String(map, "actionLabel", "Открыть"),
                Target = String(map, "target", string.Empty)
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
    public string Target { get; set; } = string.Empty;
}

public sealed class DashboardActionVm
{
    public string Title { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string ActionLabel { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
}
