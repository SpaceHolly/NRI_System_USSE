using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Nri.AdminClient.Diagnostics;
using Nri.AdminClient.Networking;
using Nri.Shared.Contracts;
using Nri.Ui.Wpf.Controls;

namespace Nri.AdminClient.ViewModels;

public sealed class AdminRestViewModel : ViewModelBase
{
    private readonly CommandApi _api;
    private RestRow? _selectedRest;
    private RestParticipantRow? _selectedParticipant;
    private DowntimeRow? _selectedDowntimeAction;
    private RecoveryGrantRow? _selectedRecoveryGrant;
    private string _statusMessage = "Отдых не загружен.";
    private string _campaignId = "dev-campaign-core";
    private string _sessionId = "default";
    private string _restType = "ShortRest";
    private int _durationMinutes = 60;
    private string _visibility = "PlayerVisible";
    private string _restQuality = "Normal";
    private string _locationSafety = "Normal";
    private string _playerSummary = "Отдых запланирован мастером.";
    private string _gmNotes = string.Empty;
    private string _participantName = "Участник";
    private string _participantCharacterId = string.Empty;
    private string _participantPlayerUserId = string.Empty;
    private string _participantKind = "PlayerCharacter";
    private bool _eligibleForRecovery = true;
    private string _downtimeType = "Watch";
    private string _downtimeText = "Дежурство во время отдыха.";
    private int _downtimeDurationMinutes = 60;
    private string _gmResult = "Действие завершено.";
    private string _interruptReason = "Отдых прерван мастером.";

    public AdminRestViewModel(CommandApi api)
    {
        _api = api;
        RefreshCommand = new RelayCommand(Refresh);
        CreateRestCommand = new RelayCommand(CreateRest);
        SaveRestCommand = new RelayCommand(SaveRest);
        AddParticipantCommand = new RelayCommand(AddParticipant);
        StartRestCommand = new RelayCommand(() => RestAction("Запуск отдыха", () => _api.RestAdminStart(SelectedRest!.RestId)));
        CompleteRestCommand = new RelayCommand(() =>
        {
            if (!Confirm("Завершить отдых", "Сервер рассчитает итог восстановления для участников. Продолжить?")) return;
            RestAction("Завершение отдыха", () => _api.RestAdminComplete(new Dictionary<string, object> { ["restId"] = SelectedRest!.RestId, ["actualDurationMinutes"] = DurationMinutes }));
        });
        InterruptRestCommand = new RelayCommand(() => RestAction("Прерывание отдыха", () => _api.RestAdminInterrupt(new Dictionary<string, object> { ["restId"] = SelectedRest!.RestId, ["actualDurationMinutes"] = Math.Max(0, DurationMinutes / 2), ["interruptedReason"] = InterruptReason })));
        CancelRestCommand = new RelayCommand(() =>
        {
            if (!Confirm("Отменить отдых", "Отдых будет отменён без обычного завершения восстановления. Продолжить?")) return;
            RestAction("Отмена отдыха", () => _api.RestAdminCancel(SelectedRest!.RestId));
        });
        SetParticipantActingCommand = new RelayCommand(SetParticipantActingSeparately);
        CreateDowntimeCommand = new RelayCommand(CreateDowntime);
        ApproveDowntimeCommand = new RelayCommand(() =>
        {
            if (!Confirm("Одобрить действие", "Действие во время отдыха будет одобрено GM. Продолжить?")) return;
            DowntimeAction("Одобрение downtime", () => _api.RestAdminApproveDowntimeAction(SelectedDowntimeAction!.ActionId));
        });
        CompleteDowntimeCommand = new RelayCommand(() => DowntimeAction("Завершение downtime", () => _api.RestAdminCompleteDowntimeAction(new Dictionary<string, object> { ["actionId"] = SelectedDowntimeAction!.ActionId, ["resultPlayerVisible"] = GmResult, ["resultGm"] = GmResult })));
        ApplyRecoveryGrantCommand = new RelayCommand(() =>
        {
            if (!Confirm("Применить восстановление", "Изменения будут применены к состоянию персонажа. Продолжить?")) return;
            GrantAction("Применение recovery grant", () => _api.RestAdminApplyRecoveryGrant(SelectedRecoveryGrant!.GrantId));
        });
    }

    public ObservableCollection<RestRow> RestSessions { get; } = new();
    public ObservableCollection<RestParticipantRow> Participants { get; } = new();
    public ObservableCollection<DowntimeRow> DowntimeActions { get; } = new();
    public ObservableCollection<RecoveryGrantRow> RecoveryGrants { get; } = new();
    public ObservableCollection<string> AuditRows { get; } = new();
    public ObservableCollection<NriReferenceOption> ParticipantCharacterOptions { get; } = new();

    public RestOption[] RestTypes { get; } = { new("ShortRest", "Короткий отдых"), new("LongRest", "Долгий отдых"), new("CustomRest", "Особый отдых") };
    public RestOption[] VisibilityModes { get; } = { new("PlayerVisible", "Всем игрокам"), new("PartyVisible", "Участникам группы"), new("AssignedParticipantsOnly", "Только участникам"), new("GmOnly", "Только GM"), new("Hidden", "Скрыто") };
    public RestOption[] Qualities { get; } = { new("Poor", "Плохие условия"), new("Normal", "Обычные условия"), new("Good", "Хорошие условия"), new("Excellent", "Отличные условия") };
    public RestOption[] SafetyModes { get; } = { new("Unsafe", "Опасно"), new("Risky", "Есть риск"), new("Normal", "Обычно"), new("Safe", "Безопасно") };
    public RestOption[] ParticipantKinds { get; } = { new("PlayerCharacter", "Персонаж игрока"), new("Companion", "Компаньон"), new("Npc", "NPC"), new("Custom", "Другое") };
    public RestOption[] DowntimeTypes { get; } = { new("Watch", "Дежурство"), new("Repair", "Ремонт"), new("TreatWounds", "Лечение"), new("Study", "Обучение"), new("CraftPrep", "Подготовка к ремеслу"), new("Shop", "Покупки"), new("Social", "Общение"), new("Scout", "Разведка"), new("Personal", "Личное действие"), new("Custom", "Другое") };

    public ICommand RefreshCommand { get; }
    public ICommand CreateRestCommand { get; }
    public ICommand SaveRestCommand { get; }
    public ICommand AddParticipantCommand { get; }
    public ICommand StartRestCommand { get; }
    public ICommand CompleteRestCommand { get; }
    public ICommand InterruptRestCommand { get; }
    public ICommand CancelRestCommand { get; }
    public ICommand SetParticipantActingCommand { get; }
    public ICommand CreateDowntimeCommand { get; }
    public ICommand ApproveDowntimeCommand { get; }
    public ICommand CompleteDowntimeCommand { get; }
    public ICommand ApplyRecoveryGrantCommand { get; }

    public string CampaignId { get => _campaignId; set { _campaignId = value; Notify(); } }
    public string SessionId { get => _sessionId; set { _sessionId = value; Notify(); } }
    public string RestType { get => _restType; set { _restType = value; DurationMinutes = value == "LongRest" ? 480 : 60; Notify(); } }
    public int DurationMinutes { get => _durationMinutes; set { _durationMinutes = Math.Max(1, value); Notify(); } }
    public string Visibility { get => _visibility; set { _visibility = value; Notify(); } }
    public string RestQuality { get => _restQuality; set { _restQuality = value; Notify(); } }
    public string LocationSafety { get => _locationSafety; set { _locationSafety = value; Notify(); } }
    public string PlayerSummary { get => _playerSummary; set { _playerSummary = value; Notify(); } }
    public string GmNotes { get => _gmNotes; set { _gmNotes = value; Notify(); } }
    public string ParticipantName { get => _participantName; set { _participantName = value; Notify(); } }
    public string ParticipantCharacterId { get => _participantCharacterId; set { _participantCharacterId = value; Notify(); } }
    public string ParticipantPlayerUserId { get => _participantPlayerUserId; set { _participantPlayerUserId = value; Notify(); } }
    public string ParticipantKind { get => _participantKind; set { _participantKind = value; Notify(); } }
    public bool EligibleForRecovery { get => _eligibleForRecovery; set { _eligibleForRecovery = value; Notify(); } }
    public string DowntimeType { get => _downtimeType; set { _downtimeType = value; Notify(); } }
    public string DowntimeText { get => _downtimeText; set { _downtimeText = value; Notify(); } }
    public int DowntimeDurationMinutes { get => _downtimeDurationMinutes; set { _downtimeDurationMinutes = Math.Max(0, value); Notify(); } }
    public string GmResult { get => _gmResult; set { _gmResult = value; Notify(); } }
    public string InterruptReason { get => _interruptReason; set { _interruptReason = value; Notify(); } }
    public string StatusMessage { get => _statusMessage; set { _statusMessage = value; Notify(); } }

    public RestRow? SelectedRest
    {
        get => _selectedRest;
        set
        {
            _selectedRest = value;
            Notify();
            if (value != null)
            {
                RestType = value.RestType;
                DurationMinutes = value.PlannedDurationMinutes;
                Visibility = value.Visibility;
                PlayerSummary = value.PlayerVisibleSummary;
                LoadRest(value.RestId);
            }
        }
    }

    public RestParticipantRow? SelectedParticipant { get => _selectedParticipant; set { _selectedParticipant = value; Notify(); } }
    public DowntimeRow? SelectedDowntimeAction { get => _selectedDowntimeAction; set { _selectedDowntimeAction = value; Notify(); } }
    public RecoveryGrantRow? SelectedRecoveryGrant { get => _selectedRecoveryGrant; set { _selectedRecoveryGrant = value; Notify(); } }

    public void Refresh()
    {
        RefreshParticipantCharacterOptions();
        Run("load", () =>
        {
            var response = _api.RestAdminListForSession(new Dictionary<string, object> { ["campaignId"] = CampaignId, ["sessionId"] = SessionId });
            if (response.Status != ResponseStatus.Ok) { StatusMessage = response.Message; return; }
            RestSessions.Clear();
            foreach (var row in List(response.Payload, "items").Select(x => RestRow.From(Map(x)))) RestSessions.Add(row);
            StatusMessage = $"Отдых: {RestSessions.Count}.";
        });
    }

    private void RefreshParticipantCharacterOptions()
    {
        ParticipantCharacterOptions.Clear();
        var response = _api.GetAllCharacters(includeArchived: false);
        if (response.Status != ResponseStatus.Ok) return;

        foreach (var item in List(response.Payload, "items").Select(Map))
        {
            var id = FirstNonEmpty(S(item, "characterId"), S(item, "id"));
            if (string.IsNullOrWhiteSpace(id)) continue;
            ParticipantCharacterOptions.Add(new NriReferenceOption
            {
                Id = id,
                DisplayName = FirstNonEmpty(S(item, "name"), "Персонаж без имени"),
                TypeLabel = "Персонаж",
                StatusLabel = "Доступен"
            });
        }
    }

    private void LoadRest(string restId)
    {
        Run("get", () =>
        {
            var response = _api.RestAdminGet(restId);
            if (response.Status != ResponseStatus.Ok) { StatusMessage = response.Message; return; }
            Participants.Clear();
            DowntimeActions.Clear();
            RecoveryGrants.Clear();
            foreach (var row in List(response.Payload, "participants").Select(x => RestParticipantRow.From(Map(x)))) Participants.Add(row);
            foreach (var row in List(response.Payload, "downtimeActions").Select(x => DowntimeRow.From(Map(x)))) DowntimeActions.Add(row);
            foreach (var row in List(response.Payload, "recoveryGrants").Select(x => RecoveryGrantRow.From(Map(x)))) RecoveryGrants.Add(row);
            LoadAudit(restId);
            StatusMessage = $"Открыт отдых: {restId}.";
        });
    }

    private void LoadAudit(string restId)
    {
        var response = _api.RestAdminGetAudit(restId);
        if (response.Status != ResponseStatus.Ok) return;
        AuditRows.Clear();
        foreach (var item in List(response.Payload, "items").Select(Map))
            AuditRows.Add($"{S(item, "createdAtUtc")} | {S(item, "actorLogin")} | {S(item, "action")} | {S(item, "summary")}");
    }

    private void CreateRest()
    {
        Run("create", () =>
        {
            var response = _api.RestAdminCreate(new Dictionary<string, object>
            {
                ["campaignId"] = CampaignId,
                ["sessionId"] = SessionId,
                ["restType"] = RestType,
                ["plannedDurationMinutes"] = DurationMinutes,
                ["visibility"] = Visibility,
                ["restQuality"] = RestQuality,
                ["restLocationSafety"] = LocationSafety,
                ["playerVisibleSummary"] = PlayerSummary,
                ["gmNotes"] = GmNotes
            });
            if (response.Status != ResponseStatus.Ok) { StatusMessage = response.Message; return; }
            Refresh();
            StatusMessage = "Отдых создан.";
        });
    }

    private void SaveRest()
    {
        if (SelectedRest == null) return;
        Run("save", () =>
        {
            var response = _api.RestAdminUpdate(new Dictionary<string, object>
            {
                ["restId"] = SelectedRest.RestId,
                ["restType"] = RestType,
                ["plannedDurationMinutes"] = DurationMinutes,
                ["visibility"] = Visibility,
                ["restQuality"] = RestQuality,
                ["restLocationSafety"] = LocationSafety,
                ["playerVisibleSummary"] = PlayerSummary,
                ["gmNotes"] = GmNotes
            });
            if (response.Status != ResponseStatus.Ok) { StatusMessage = response.Message; return; }
            LoadRest(SelectedRest.RestId);
            StatusMessage = "Отдых сохранён.";
        });
    }

    private void AddParticipant()
    {
        if (SelectedRest == null) return;
        Run("participant.add", () =>
        {
            var response = _api.RestAdminAddParticipant(new Dictionary<string, object>
            {
                ["restId"] = SelectedRest.RestId,
                ["displayName"] = ParticipantName,
                ["characterId"] = ParticipantCharacterId,
                ["playerUserId"] = ParticipantPlayerUserId,
                ["participantKind"] = ParticipantKind,
                ["eligibleForRecovery"] = EligibleForRecovery
            });
            if (response.Status != ResponseStatus.Ok) { StatusMessage = response.Message; return; }
            LoadRest(SelectedRest.RestId);
            StatusMessage = "Участник добавлен.";
        });
    }

    private void SetParticipantActingSeparately()
    {
        if (SelectedRest == null || SelectedParticipant == null) return;
        Run("participant.acting", () =>
        {
            var response = _api.RestAdminSetParticipantStatus(new Dictionary<string, object> { ["participantId"] = SelectedParticipant.ParticipantId, ["participationStatus"] = "ActingSeparately", ["eligibleForRecovery"] = false, ["playerVisibleStatus"] = "Действует отдельно и не получает восстановление от отдыха." });
            if (response.Status != ResponseStatus.Ok) { StatusMessage = response.Message; return; }
            LoadRest(SelectedRest.RestId);
            StatusMessage = "Участник действует отдельно.";
        });
    }

    private void CreateDowntime()
    {
        if (SelectedRest == null) return;
        Run("downtime.create", () =>
        {
            var response = _api.RestAdminCreateDowntimeAction(new Dictionary<string, object>
            {
                ["restId"] = SelectedRest.RestId,
                ["characterId"] = ParticipantCharacterId,
                ["playerUserId"] = ParticipantPlayerUserId,
                ["actionType"] = DowntimeType,
                ["playerText"] = DowntimeText,
                ["gmText"] = GmNotes,
                ["durationMinutes"] = DowntimeDurationMinutes
            });
            if (response.Status != ResponseStatus.Ok) { StatusMessage = response.Message; return; }
            LoadRest(SelectedRest.RestId);
            StatusMessage = "Downtime action создан.";
        });
    }

    private void RestAction(string operation, Func<ResponseEnvelope> action)
    {
        if (SelectedRest == null) return;
        Run(operation, () =>
        {
            var response = action();
            if (response.Status != ResponseStatus.Ok) { StatusMessage = response.Message; return; }
            LoadRest(SelectedRest.RestId);
            Refresh();
            StatusMessage = response.Message;
        });
    }

    private static bool Confirm(string title, string message)
        => System.Windows.MessageBox.Show(message, title, System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning)
           == System.Windows.MessageBoxResult.Yes;

    private void DowntimeAction(string operation, Func<ResponseEnvelope> action)
    {
        if (SelectedRest == null || SelectedDowntimeAction == null) return;
        Run(operation, () =>
        {
            var response = action();
            if (response.Status != ResponseStatus.Ok) { StatusMessage = response.Message; return; }
            LoadRest(SelectedRest.RestId);
            StatusMessage = response.Message;
        });
    }

    private void GrantAction(string operation, Func<ResponseEnvelope> action)
    {
        if (SelectedRest == null || SelectedRecoveryGrant == null) return;
        Run(operation, () =>
        {
            var response = action();
            if (response.Status != ResponseStatus.Ok) { StatusMessage = response.Message; return; }
            LoadRest(SelectedRest.RestId);
            StatusMessage = response.Message;
        });
    }

    private void Run(string operation, Action action)
    {
        try
        {
            ClientLogService.Instance.Info($"admin.rest.{operation}.start");
            action();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            ClientLogService.Instance.Error($"admin.rest.{operation}.error", ex);
        }
    }

    private static Dictionary<string, object> Map(object? raw) => raw as Dictionary<string, object> ?? new Dictionary<string, object>();
    private static IEnumerable<object> List(IDictionary<string, object> map, string key) => map.TryGetValue(key, out var raw) && raw is IEnumerable list ? list.Cast<object>() : Array.Empty<object>();
    private static string S(Dictionary<string, object> map, string key) => map.TryGetValue(key, out var raw) ? Convert.ToString(raw) ?? string.Empty : string.Empty;
    private static int I(Dictionary<string, object> map, string key) => int.TryParse(S(map, key), out var value) ? value : 0;
    private static bool B(Dictionary<string, object> map, string key) => S(map, key).Equals("True", StringComparison.OrdinalIgnoreCase);
    private static string FirstNonEmpty(params string[] values) => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    public sealed class RestOption
    {
        public RestOption(string id, string title)
        {
            Id = id;
            Title = title;
        }

        public string Id { get; }
        public string Title { get; }
    }

    public sealed class RestRow
    {
        public string RestId { get; set; } = string.Empty;
        public string RestType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Visibility { get; set; } = string.Empty;
        public int PlannedDurationMinutes { get; set; }
        public string PlayerVisibleSummary { get; set; } = string.Empty;
        public string Summary => $"{RestType} | {Status} | {PlannedDurationMinutes} мин | {Visibility}";
        public static RestRow From(Dictionary<string, object> map) => new() { RestId = S(map, "restId"), RestType = S(map, "restType"), Status = S(map, "status"), Visibility = S(map, "visibility"), PlannedDurationMinutes = I(map, "plannedDurationMinutes"), PlayerVisibleSummary = S(map, "playerVisibleSummary") };
    }

    public sealed class RestParticipantRow
    {
        public string ParticipantId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string ParticipationStatus { get; set; } = string.Empty;
        public bool EligibleForRecovery { get; set; }
        public string RecoveryResult { get; set; } = string.Empty;
        public string Summary => $"{DisplayName} | {ParticipationStatus} | recovery={RecoveryResult}";
        public static RestParticipantRow From(Dictionary<string, object> map) => new() { ParticipantId = S(map, "participantId"), DisplayName = S(map, "displayName"), ParticipationStatus = S(map, "participationStatus"), EligibleForRecovery = B(map, "eligibleForRecovery"), RecoveryResult = S(map, "recoveryResult") };
    }

    public sealed class DowntimeRow
    {
        public string ActionId { get; set; } = string.Empty;
        public string ActionType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string PlayerText { get; set; } = string.Empty;
        public string Summary => $"{ActionType} | {Status} | {PlayerText}";
        public static DowntimeRow From(Dictionary<string, object> map) => new() { ActionId = S(map, "actionId"), ActionType = S(map, "actionType"), Status = S(map, "status"), PlayerText = S(map, "playerText") };
    }

    public sealed class RecoveryGrantRow
    {
        public string GrantId { get; set; } = string.Empty;
        public string GrantType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string RecoverySummaryPlayer { get; set; } = string.Empty;
        public string Summary => $"{GrantType} | {Status} | {RecoverySummaryPlayer}";
        public static RecoveryGrantRow From(Dictionary<string, object> map) => new() { GrantId = S(map, "grantId"), GrantType = S(map, "grantType"), Status = S(map, "status"), RecoverySummaryPlayer = S(map, "recoverySummaryPlayer") };
    }
}
