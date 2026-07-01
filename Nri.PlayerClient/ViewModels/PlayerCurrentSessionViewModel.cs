using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Nri.PlayerClient.Diagnostics;
using Nri.PlayerClient.Networking;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;

namespace Nri.PlayerClient.ViewModels;

public sealed class PlayerCurrentSessionViewModel : ViewModelBase
{
    private readonly CommandApi _api;
    private readonly Func<string> _activeCharacterIdAccessor;
    private string _campaignId = "default";
    private string _sessionId = string.Empty;
    private string _sessionName = "Текущая сессия не загружена";
    private string _status = string.Empty;
    private string _mode = string.Empty;
    private string _currentSceneName = string.Empty;
    private string _gmDisplayName = string.Empty;
    private string _publicNotes = string.Empty;
    private string _activeSceneMapName = string.Empty;
    private string _activeWorldMapName = string.Empty;
    private string _activeRoomName = string.Empty;
    private string _activeCombatSummary = string.Empty;
    private bool _hasSession;
    private bool _isEnabled;
    private bool _isLoading;
    private string _statusMessage = "Состояние текущей сессии будет показано после подключения.";
    private string _warningMessage = string.Empty;
    private string _errorMessage = string.Empty;
    private string _lastRefreshText = "не обновлялось";

    public PlayerCurrentSessionViewModel(CommandApi api, Func<string> activeCharacterIdAccessor)
    {
        _api = api;
        _activeCharacterIdAccessor = activeCharacterIdAccessor;
        RefreshFlagsCommand = new RelayCommand(RefreshFlags);
        RefreshCommand = new RelayCommand(Load);
        ClearErrorCommand = new RelayCommand(() => { ErrorMessage = string.Empty; WarningMessage = string.Empty; });
    }

    public ObservableCollection<SessionQuickLinkVm> QuickLinks { get; } = new();
    public ICommand RefreshFlagsCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand ClearErrorCommand { get; }

    public string CampaignId { get => _campaignId; set { if (_campaignId != value) { _campaignId = value; Notify(); } } }
    public string SessionId { get => _sessionId; private set { if (_sessionId != value) { _sessionId = value; Notify(); } } }
    public string SessionName { get => _sessionName; private set { if (_sessionName != value) { _sessionName = value; Notify(); } } }
    public string Status { get => _status; private set { if (_status != value) { _status = value; Notify(); Notify(nameof(StatusDisplay)); } } }
    public string Mode { get => _mode; private set { if (_mode != value) { _mode = value; Notify(); Notify(nameof(ModeDisplay)); } } }
    public string CurrentSceneName { get => _currentSceneName; private set { if (_currentSceneName != value) { _currentSceneName = value; Notify(); Notify(nameof(SceneDisplay)); } } }
    public string GMDisplayName { get => _gmDisplayName; private set { if (_gmDisplayName != value) { _gmDisplayName = value; Notify(); Notify(nameof(GMDisplay)); } } }
    public string PublicNotes { get => _publicNotes; private set { if (_publicNotes != value) { _publicNotes = value; Notify(); Notify(nameof(NotesDisplay)); } } }
    public string ActiveSceneMapName { get => _activeSceneMapName; private set { if (_activeSceneMapName != value) { _activeSceneMapName = value; Notify(); Notify(nameof(SceneMapDisplay)); } } }
    public string ActiveWorldMapName { get => _activeWorldMapName; private set { if (_activeWorldMapName != value) { _activeWorldMapName = value; Notify(); Notify(nameof(WorldMapDisplay)); } } }
    public string ActiveRoomName { get => _activeRoomName; private set { if (_activeRoomName != value) { _activeRoomName = value; Notify(); Notify(nameof(RoomDisplay)); } } }
    public string ActiveCombatSummary { get => _activeCombatSummary; private set { if (_activeCombatSummary != value) { _activeCombatSummary = value; Notify(); Notify(nameof(CombatDisplay)); } } }
    public bool HasSession { get => _hasSession; private set { if (_hasSession != value) { _hasSession = value; Notify(); } } }
    public bool IsEnabled { get => _isEnabled; private set { if (_isEnabled != value) { _isEnabled = value; Notify(); } } }
    public bool IsLoading { get => _isLoading; private set { if (_isLoading != value) { _isLoading = value; Notify(); } } }
    public string StatusMessage { get => _statusMessage; private set { if (_statusMessage != value) { _statusMessage = value; Notify(); } } }
    public string WarningMessage { get => _warningMessage; private set { if (_warningMessage != value) { _warningMessage = value; Notify(); Notify(nameof(HasWarning)); } } }
    public string ErrorMessage { get => _errorMessage; private set { if (_errorMessage != value) { _errorMessage = value; Notify(); Notify(nameof(HasError)); } } }
    public string LastRefreshText { get => _lastRefreshText; private set { if (_lastRefreshText != value) { _lastRefreshText = value; Notify(); } } }

    public bool HasWarning => !string.IsNullOrWhiteSpace(WarningMessage);
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public string StatusDisplay => DisplayStatus(Status);
    public string ModeDisplay => DisplayMode(Mode);
    public string SceneDisplay => string.IsNullOrWhiteSpace(CurrentSceneName) ? "Сцена не выбрана" : CurrentSceneName;
    public string GMDisplay => string.IsNullOrWhiteSpace(GMDisplayName) ? "GM не указан" : GMDisplayName;
    public string NotesDisplay => string.IsNullOrWhiteSpace(PublicNotes) ? "Публичных заметок сессии пока нет." : PublicNotes;
    public string SceneMapDisplay => string.IsNullOrWhiteSpace(ActiveSceneMapName) ? "GM ещё не назначил активную карту сцены." : ActiveSceneMapName;
    public string WorldMapDisplay => string.IsNullOrWhiteSpace(ActiveWorldMapName) ? "Карта мира не назначена." : ActiveWorldMapName;
    public string RoomDisplay => string.IsNullOrWhiteSpace(ActiveRoomName) ? "Помещение не назначено." : ActiveRoomName;
    public string CombatDisplay => string.IsNullOrWhiteSpace(ActiveCombatSummary) ? "Активного боя нет." : ActiveCombatSummary;

    public void RefreshFlags()
    {
        try
        {
            var response = _api.SendSystemFeatureFlagsSnapshotForPlayer();
            if (!IsOk(response))
            {
                IsEnabled = false;
                ErrorMessage = PlayerFacingMessage(response.Message, "Не удалось проверить доступность текущей сессии.");
                return;
            }

            var flags = Dictionaries(Get(response.Payload, "flags")).ToList();
            IsEnabled = Flag(flags, nameof(SessionFeatureFlags.UseCurrentSessionMvp))
                && Flag(flags, nameof(SessionFeatureFlags.UseSessionStateV1))
                && Flag(flags, nameof(SessionFeatureFlags.UseSessionPlayerView));
            StatusMessage = IsEnabled
                ? "Статус текущей сессии доступен."
                : "Текущая сессия пока недоступна.";
            if (IsEnabled) Load();
        }
        catch (Exception ex)
        {
            ErrorMessage = "Не удалось проверить доступность текущей сессии.";
            ClientLogService.Instance.Error("player.session.flags.error", ex);
        }
    }

    private void Load()
    {
        if (!IsEnabled || IsLoading) return;
        IsLoading = true;
        ErrorMessage = string.Empty;
        try
        {
            ClientLogService.Instance.Info("player.session.current.load.start");
            var response = _api.SessionPlayerCurrentGet(new Dictionary<string, object>
            {
                { "campaignId", CampaignId },
                { "characterId", _activeCharacterIdAccessor() }
            });
            if (!IsOk(response))
            {
                ErrorMessage = Friendly(response, "Не удалось загрузить текущую сессию.");
                return;
            }
            ApplyPayload(response.Payload);
            ClientLogService.Instance.Info("player.session.current.load.done");
        }
        catch (Exception ex)
        {
            ErrorMessage = "Не удалось загрузить состояние текущей сессии.";
            ClientLogService.Instance.Error("player.session.current.load.error", ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyPayload(Dictionary<string, object> payload)
    {
        HasSession = Bool(Get(payload, "hasSession"));
        if (!HasSession)
        {
            SessionName = "GM ещё не создал текущую сессию.";
            StatusMessage = "Текущая сессия не назначена.";
            QuickLinks.Clear();
            LastRefreshText = DateTime.Now.ToString("HH:mm:ss");
            return;
        }

        var session = AsMap(Get(payload, "session"));
        SessionId = Str(Get(session, "sessionId"));
        SessionName = FirstNonEmpty(Str(Get(session, "name")), "Текущая сессия");
        Status = Str(Get(session, "status"));
        Mode = Str(Get(session, "mode"));
        CurrentSceneName = Str(Get(session, "currentSceneName"));
        GMDisplayName = Str(Get(session, "gmDisplayName"));
        PublicNotes = Str(Get(session, "publicNotes"));
        ActiveSceneMapName = Bool(Get(session, "hasActiveSceneMap")) ? Str(Get(session, "activeSceneMapName")) : string.Empty;
        ActiveWorldMapName = Bool(Get(session, "hasActiveWorldMap")) ? Str(Get(session, "activeWorldMapName")) : string.Empty;
        ActiveRoomName = Bool(Get(session, "hasActiveRoom")) ? Str(Get(session, "activeRoomName")) : string.Empty;
        ActiveCombatSummary = Bool(Get(session, "hasActiveCombat")) ? Str(Get(session, "activeCombatSummary")) : string.Empty;
        QuickLinks.Clear();
        foreach (var link in Dictionaries(Get(session, "quickLinks")))
            QuickLinks.Add(SessionQuickLinkVm.From(link));
        StatusMessage = "Текущая сессия загружена.";
        LastRefreshText = DateTime.Now.ToString("HH:mm:ss");
    }

    private static bool IsOk(ResponseEnvelope response) => response.Status == ResponseStatus.Ok;
    private static string Friendly(ResponseEnvelope response, string fallback) => string.IsNullOrWhiteSpace(response.Message) ? fallback : response.Message;
    private static string PlayerFacingMessage(string? message, string fallback)
    {
        if (string.IsNullOrWhiteSpace(message)) return fallback;
        if (message.IndexOf("feature flag", StringComparison.OrdinalIgnoreCase) >= 0 ||
            message.IndexOf("flags", StringComparison.OrdinalIgnoreCase) >= 0)
            return fallback;
        return message;
    }
    private static object? Get(IDictionary<string, object> map, string key) => map.TryGetValue(key, out var value) ? value : null;
    private static Dictionary<string, object> AsMap(object? value) => value as Dictionary<string, object> ?? new Dictionary<string, object>();
    private static string Str(object? value) => Convert.ToString(value) ?? string.Empty;
    private static bool Bool(object? value, bool fallback = false) => value is bool b ? b : bool.TryParse(Str(value), out var parsed) ? parsed : fallback;
    private static string FirstNonEmpty(params string[] values) => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
    private static IEnumerable<Dictionary<string, object>> Dictionaries(object? value)
    {
        if (value is IEnumerable enumerable && value is not string)
        {
            foreach (var item in enumerable)
            {
                if (item is Dictionary<string, object> map) yield return map;
            }
        }
    }
    private static bool Flag(IEnumerable<Dictionary<string, object>> flags, string name)
        => flags.Any(flag => string.Equals(Str(Get(flag, "name")), name, StringComparison.OrdinalIgnoreCase) && Bool(Get(flag, "effective")));
    private static string DisplayStatus(string status) => status switch
    {
        CurrentSessionStatusIds.Planned => "Планируется",
        CurrentSessionStatusIds.Active => "Активна",
        CurrentSessionStatusIds.Paused => "Пауза",
        CurrentSessionStatusIds.Completed => "Завершена",
        CurrentSessionStatusIds.Cancelled => "Отменена",
        CurrentSessionStatusIds.Archived => "Архив",
        _ => string.IsNullOrWhiteSpace(status) ? "—" : status
    };
    private static string DisplayMode(string mode) => mode switch
    {
        CurrentSessionModeIds.Preparation => "Подготовка",
        CurrentSessionModeIds.NormalScene => "Сцена",
        CurrentSessionModeIds.Combat => "Бой",
        CurrentSessionModeIds.Travel => "Путешествие",
        CurrentSessionModeIds.ShortRest => "Короткий отдых",
        CurrentSessionModeIds.LongRest => "Долгий отдых",
        CurrentSessionModeIds.Downtime => "Даунтайм",
        CurrentSessionModeIds.Maintenance => "Обслуживание",
        CurrentSessionModeIds.Custom => "Другое",
        _ => string.IsNullOrWhiteSpace(mode) ? "—" : mode
    };
}

public sealed class SessionQuickLinkVm
{
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public string TargetId { get; set; } = string.Empty;
    public string StateText => Enabled ? "Доступно" : "Позже";

    public static SessionQuickLinkVm From(Dictionary<string, object> payload)
        => new SessionQuickLinkVm
        {
            Key = Convert.ToString(payload.TryGetValue("key", out var key) ? key : string.Empty) ?? string.Empty,
            Title = Convert.ToString(payload.TryGetValue("title", out var title) ? title : string.Empty) ?? string.Empty,
            Enabled = payload.TryGetValue("enabled", out var enabled) && enabled is bool b && b,
            TargetId = Convert.ToString(payload.TryGetValue("targetId", out var targetId) ? targetId : string.Empty) ?? string.Empty
        };
}
