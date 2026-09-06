using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Nri.PlayerClient.Diagnostics;
using Nri.PlayerClient.Networking;
using Nri.Shared.Contracts;

namespace Nri.PlayerClient.ViewModels;

public sealed class PlayerRestViewModel : ViewModelBase
{
    private readonly CommandApi _api;
    private readonly Func<string> _activeCharacterId;
    private PlayerRestRow? _selectedRest;
    private string _statusMessage = "Отдых не загружен.";
    private string _campaignId = "dev-campaign-core";
    private string _sessionId = "default";
    private string _downtimeType = "Watch";
    private string _downtimeText = "Дежурю во время отдыха.";
    private int _downtimeDurationMinutes = 60;
    private DowntimeTypeOption? _selectedDowntimeType;

    public PlayerRestViewModel(CommandApi api, Func<string> activeCharacterId)
    {
        _api = api;
        _activeCharacterId = activeCharacterId;
        RefreshCommand = new RelayCommand(Refresh);
        LoadSelectedCommand = new RelayCommand(LoadSelected);
        SubmitDowntimeCommand = new RelayCommand(SubmitDowntime);
        SelectedDowntimeType = DowntimeTypes[0];
    }

    public ObservableCollection<PlayerRestRow> RestSessions { get; } = new();
    public ObservableCollection<PlayerParticipantRow> Participants { get; } = new();
    public ObservableCollection<PlayerDowntimeRow> DowntimeActions { get; } = new();
    public ObservableCollection<PlayerRecoveryGrantRow> RecoveryGrants { get; } = new();
    public DowntimeTypeOption[] DowntimeTypes { get; } =
    {
        new DowntimeTypeOption("Watch", "Дежурство"),
        new DowntimeTypeOption("Repair", "Ремонт"),
        new DowntimeTypeOption("TreatWounds", "Лечение"),
        new DowntimeTypeOption("Study", "Обучение"),
        new DowntimeTypeOption("CraftPrep", "Подготовка производства"),
        new DowntimeTypeOption("Shop", "Покупки"),
        new DowntimeTypeOption("Social", "Общение"),
        new DowntimeTypeOption("Scout", "Разведка"),
        new DowntimeTypeOption("Personal", "Личное дело"),
        new DowntimeTypeOption("Custom", "Другое")
    };

    public ICommand RefreshCommand { get; }
    public ICommand LoadSelectedCommand { get; }
    public ICommand SubmitDowntimeCommand { get; }

    public string CampaignId { get => _campaignId; set { _campaignId = value; Notify(); } }
    public string SessionId { get => _sessionId; set { _sessionId = value; Notify(); } }
    public string DowntimeType { get => _downtimeType; set { _downtimeType = value; Notify(); } }
    public DowntimeTypeOption? SelectedDowntimeType
    {
        get => _selectedDowntimeType;
        set
        {
            if (_selectedDowntimeType == value) return;
            _selectedDowntimeType = value;
            DowntimeType = value?.Value ?? "Custom";
            Notify();
        }
    }
    public string DowntimeText { get => _downtimeText; set { _downtimeText = value; Notify(); } }
    public int DowntimeDurationMinutes { get => _downtimeDurationMinutes; set { _downtimeDurationMinutes = Math.Max(0, value); Notify(); } }
    public string StatusMessage { get => _statusMessage; set { _statusMessage = value; Notify(); } }

    public PlayerRestRow? SelectedRest
    {
        get => _selectedRest;
        set { _selectedRest = value; Notify(); if (value != null) LoadRest(value.RestId); }
    }

    public void Refresh()
    {
        Run("load", () =>
        {
            var response = _api.RestPlayerGetActiveForSession(new Dictionary<string, object> { ["campaignId"] = CampaignId, ["sessionId"] = SessionId });
            if (response.Status != ResponseStatus.Ok) { StatusMessage = response.Message; return; }
            RestSessions.Clear();
            foreach (var row in List(response.Payload, "items").Select(x => PlayerRestRow.From(Map(x)))) RestSessions.Add(row);
            RefreshGrants();
            StatusMessage = RestSessions.Count == 0 ? "GM ещё не открыл отдых игрокам." : $"Доступно состояний отдыха: {RestSessions.Count}.";
        });
    }

    private void LoadSelected()
    {
        if (SelectedRest != null) LoadRest(SelectedRest.RestId);
    }

    private void LoadRest(string restId)
    {
        Run("get", () =>
        {
            var response = _api.RestPlayerGetMyRestStatus(new Dictionary<string, object> { ["restId"] = restId, ["campaignId"] = CampaignId, ["sessionId"] = SessionId });
            if (response.Status != ResponseStatus.Ok) { StatusMessage = response.Message; return; }
            Participants.Clear();
            DowntimeActions.Clear();
            RecoveryGrants.Clear();
            foreach (var row in List(response.Payload, "participants").Select(x => PlayerParticipantRow.From(Map(x)))) Participants.Add(row);
            foreach (var row in List(response.Payload, "downtimeActions").Select(x => PlayerDowntimeRow.From(Map(x)))) DowntimeActions.Add(row);
            foreach (var row in List(response.Payload, "recoveryGrants").Select(x => PlayerRecoveryGrantRow.From(Map(x)))) RecoveryGrants.Add(row);
            StatusMessage = "Статус отдыха обновлён.";
        });
    }

    private void SubmitDowntime()
    {
        if (SelectedRest == null) return;
        var confirmation = MessageBox.Show(
            $"Отправить действие во время отдыха?\n\n{SelectedDowntimeType?.Label ?? "Другое"}\n{DowntimeDurationMinutes} мин\n{DowntimeText}",
            "Подтверждение действия",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirmation != MessageBoxResult.Yes)
        {
            StatusMessage = "Действие во время отдыха отменено.";
            return;
        }
        Run("downtime.submit", () =>
        {
            var response = _api.RestPlayerSubmitDowntimeAction(new Dictionary<string, object>
            {
                ["restId"] = SelectedRest.RestId,
                ["characterId"] = _activeCharacterId(),
                ["actionType"] = DowntimeType,
                ["playerText"] = DowntimeText,
                ["durationMinutes"] = DowntimeDurationMinutes
            });
            StatusMessage = response.Message;
            if (response.Status == ResponseStatus.Ok) LoadRest(SelectedRest.RestId);
        });
    }

    private void RefreshGrants()
    {
        var response = _api.RestPlayerGetRecoveryGrants(new Dictionary<string, object> { ["campaignId"] = CampaignId, ["characterId"] = _activeCharacterId() });
        if (response.Status != ResponseStatus.Ok) return;
        RecoveryGrants.Clear();
        foreach (var row in List(response.Payload, "items").Select(x => PlayerRecoveryGrantRow.From(Map(x)))) RecoveryGrants.Add(row);
    }

    private void Run(string operation, Action action)
    {
        try
        {
            ClientLogService.Instance.Info($"player.rest.{operation}.start");
            action();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            ClientLogService.Instance.Error($"player.rest.{operation}.error", ex);
        }
    }

    private static Dictionary<string, object> Map(object? raw) => raw as Dictionary<string, object> ?? new Dictionary<string, object>();
    private static IEnumerable<object> List(IDictionary<string, object> map, string key) => map.TryGetValue(key, out var raw) && raw is IEnumerable list ? list.Cast<object>() : Array.Empty<object>();
    private static string S(Dictionary<string, object> map, string key) => map.TryGetValue(key, out var raw) ? Convert.ToString(raw) ?? string.Empty : string.Empty;
    private static int I(Dictionary<string, object> map, string key) => int.TryParse(S(map, key), out var value) ? value : 0;
    private static bool B(Dictionary<string, object> map, string key) => S(map, key).Equals("True", StringComparison.OrdinalIgnoreCase);

    public sealed class PlayerRestRow
    {
        public string RestId { get; set; } = string.Empty;
        public string RestType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int PlannedDurationMinutes { get; set; }
        public string PlayerVisibleSummary { get; set; } = string.Empty;
        public string Summary => $"{PlayerDevelopmentGraphDisplay.ToReadableText(RestType)} | {PlayerDevelopmentGraphDisplay.ToReadableText(Status)} | {PlannedDurationMinutes} мин";
        public static PlayerRestRow From(Dictionary<string, object> map) => new() { RestId = S(map, "restId"), RestType = S(map, "restType"), Status = S(map, "status"), PlannedDurationMinutes = I(map, "plannedDurationMinutes"), PlayerVisibleSummary = S(map, "playerVisibleSummary") };
    }

    public sealed class PlayerParticipantRow
    {
        public string DisplayName { get; set; } = string.Empty;
        public string ParticipationStatus { get; set; } = string.Empty;
        public bool EligibleForRecovery { get; set; }
        public string PlayerVisibleStatus { get; set; } = string.Empty;
        public string Summary => $"{DisplayName} | {ParticipationStatus} | {(EligibleForRecovery ? "может восстановиться" : "без восстановления")}";
        public static PlayerParticipantRow From(Dictionary<string, object> map) => new() { DisplayName = S(map, "displayName"), ParticipationStatus = S(map, "participationStatus"), EligibleForRecovery = B(map, "eligibleForRecovery"), PlayerVisibleStatus = S(map, "playerVisibleStatus") };
    }

    public sealed class PlayerDowntimeRow
    {
        public string ActionType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string PlayerText { get; set; } = string.Empty;
        public string ResultPlayerVisible { get; set; } = string.Empty;
        public string Summary => $"{PlayerDevelopmentGraphDisplay.ToReadableText(ActionType)} | {PlayerDevelopmentGraphDisplay.ToReadableText(Status)} | {PlayerText}";
        public static PlayerDowntimeRow From(Dictionary<string, object> map) => new() { ActionType = S(map, "actionType"), Status = S(map, "status"), PlayerText = S(map, "playerText"), ResultPlayerVisible = S(map, "resultPlayerVisible") };
    }

    public sealed class PlayerRecoveryGrantRow
    {
        public string GrantType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string RecoverySummaryPlayer { get; set; } = string.Empty;
        public string Summary => $"{PlayerDevelopmentGraphDisplay.ToReadableText(GrantType)} | {PlayerDevelopmentGraphDisplay.ToReadableText(Status)} | {RecoverySummaryPlayer}";
        public static PlayerRecoveryGrantRow From(Dictionary<string, object> map) => new() { GrantType = S(map, "grantType"), Status = S(map, "status"), RecoverySummaryPlayer = S(map, "recoverySummaryPlayer") };
    }

    public sealed class DowntimeTypeOption
    {
        public DowntimeTypeOption(string value, string label)
        {
            Value = value;
            Label = label;
        }

        public string Value { get; }
        public string Label { get; }
        public override string ToString() => Label;
    }
}
