using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Nri.AdminClient.Diagnostics;
using Nri.AdminClient.Networking;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;

namespace Nri.AdminClient.ViewModels;

public sealed class AdminCurrentSessionViewModel : ViewModelBase
{
    private readonly CommandApi _api;
    private string _campaignId = "default";
    private string _sessionId = string.Empty;
    private string _sessionName = "Новая сессия";
    private string _description = string.Empty;
    private string _status = CurrentSessionStatusIds.Planned;
    private string _mode = CurrentSessionModeIds.Preparation;
    private string _currentSceneId = string.Empty;
    private string _currentSceneName = string.Empty;
    private string _activeSceneMapId = string.Empty;
    private string _activeSceneMapName = string.Empty;
    private string _activeCombatEncounterId = string.Empty;
    private string _activeCombatName = string.Empty;
    private string _publicNotes = string.Empty;
    private string _gmNotes = string.Empty;
    private string _visibilityMode = MapVisibilityModes.Party;
    private bool _isPlayerVisible = true;
    private bool _hasSession;
    private bool _isLoading;
    private bool _isEnabled;
    private string _statusMessage = "Текущая сессия готова к подключению. Включите флаги функций Current Session MVP для работы.";
    private string _warningMessage = string.Empty;
    private string _errorMessage = string.Empty;
    private string _lastRefreshText = "не обновлялось";

    public AdminCurrentSessionViewModel(CommandApi api)
    {
        _api = api;
        RefreshFlagsCommand = new RelayCommand(RefreshFlags);
        LoadCommand = new RelayCommand(Load);
        CreateCommand = new RelayCommand(CreateSession);
        UpdateCommand = new RelayCommand(UpdateSession);
        StartCommand = new RelayCommand(() => SendStatusCommand(_api.SessionCurrentStart, "Сессия запущена."));
        PauseCommand = new RelayCommand(() => SendStatusCommand(_api.SessionCurrentPause, "Сессия поставлена на паузу."));
        ResumeCommand = new RelayCommand(() => SendStatusCommand(_api.SessionCurrentResume, "Сессия продолжена."));
        CompleteCommand = new RelayCommand(() =>
        {
            if (!ConfirmTerminal("Завершить текущую сессию?")) return;
            SendStatusCommand(_api.SessionCurrentComplete, "Сессия завершена.");
        });
        CancelCommand = new RelayCommand(() =>
        {
            if (!ConfirmTerminal("Отменить текущую сессию?")) return;
            SendStatusCommand(_api.SessionCurrentCancel, "Сессия отменена.");
        });
        SetSceneCommand = new RelayCommand(SetScene);
        SetModeCommand = new RelayCommand(SetMode);
        SetActiveSceneMapCommand = new RelayCommand(SetActiveSceneMap);
        SetActiveCombatCommand = new RelayCommand(SetActiveCombat);
        ClearActiveCombatCommand = new RelayCommand(ClearActiveCombat);
        SaveNotesCommand = new RelayCommand(SaveNotes);
        ClearErrorCommand = new RelayCommand(() => { ErrorMessage = string.Empty; WarningMessage = string.Empty; });
    }

    public ObservableCollection<SessionQuickLinkVm> QuickLinks { get; } = new();
    public ObservableCollection<string> Warnings { get; } = new();
    public ObservableCollection<string> ModeOptions { get; } = new()
    {
        CurrentSessionModeIds.Preparation,
        CurrentSessionModeIds.NormalScene,
        CurrentSessionModeIds.Combat,
        CurrentSessionModeIds.Travel,
        CurrentSessionModeIds.ShortRest,
        CurrentSessionModeIds.LongRest,
        CurrentSessionModeIds.Downtime,
        CurrentSessionModeIds.Maintenance,
        CurrentSessionModeIds.Custom
    };

    public ICommand RefreshFlagsCommand { get; }
    public ICommand LoadCommand { get; }
    public ICommand CreateCommand { get; }
    public ICommand UpdateCommand { get; }
    public ICommand StartCommand { get; }
    public ICommand PauseCommand { get; }
    public ICommand ResumeCommand { get; }
    public ICommand CompleteCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand SetSceneCommand { get; }
    public ICommand SetModeCommand { get; }
    public ICommand SetActiveSceneMapCommand { get; }
    public ICommand SetActiveCombatCommand { get; }
    public ICommand ClearActiveCombatCommand { get; }
    public ICommand SaveNotesCommand { get; }
    public ICommand ClearErrorCommand { get; }

    public string CampaignId { get => _campaignId; set { if (_campaignId != value) { _campaignId = value; Notify(); } } }
    public string SessionId { get => _sessionId; set { if (_sessionId != value) { _sessionId = value; Notify(); NotifyState(); } } }
    public string SessionName { get => _sessionName; set { if (_sessionName != value) { _sessionName = value; Notify(); } } }
    public string Description { get => _description; set { if (_description != value) { _description = value; Notify(); } } }
    public string Status { get => _status; private set { if (_status != value) { _status = value; Notify(); Notify(nameof(StatusDisplay)); } } }
    public string Mode { get => _mode; set { if (_mode != value) { _mode = value; Notify(); Notify(nameof(ModeDisplay)); } } }
    public string CurrentSceneId { get => _currentSceneId; set { if (_currentSceneId != value) { _currentSceneId = value; Notify(); } } }
    public string CurrentSceneName { get => _currentSceneName; set { if (_currentSceneName != value) { _currentSceneName = value; Notify(); Notify(nameof(SceneSummary)); } } }
    public string ActiveSceneMapId { get => _activeSceneMapId; set { if (_activeSceneMapId != value) { _activeSceneMapId = value; Notify(); } } }
    public string ActiveSceneMapName { get => _activeSceneMapName; private set { if (_activeSceneMapName != value) { _activeSceneMapName = value; Notify(); Notify(nameof(SceneMapSummary)); } } }
    public string ActiveCombatEncounterId { get => _activeCombatEncounterId; set { if (_activeCombatEncounterId != value) { _activeCombatEncounterId = value; Notify(); } } }
    public string ActiveCombatName { get => _activeCombatName; private set { if (_activeCombatName != value) { _activeCombatName = value; Notify(); Notify(nameof(CombatSummary)); } } }
    public string PublicNotes { get => _publicNotes; set { if (_publicNotes != value) { _publicNotes = value; Notify(); } } }
    public string GMNotes { get => _gmNotes; set { if (_gmNotes != value) { _gmNotes = value; Notify(); } } }
    public string VisibilityMode { get => _visibilityMode; set { if (_visibilityMode != value) { _visibilityMode = value; Notify(); } } }
    public bool IsPlayerVisible { get => _isPlayerVisible; set { if (_isPlayerVisible != value) { _isPlayerVisible = value; Notify(); } } }
    public bool HasSession { get => _hasSession; private set { if (_hasSession != value) { _hasSession = value; Notify(); NotifyState(); } } }
    public bool IsLoading { get => _isLoading; private set { if (_isLoading != value) { _isLoading = value; Notify(); NotifyState(); } } }
    public bool IsEnabled { get => _isEnabled; private set { if (_isEnabled != value) { _isEnabled = value; Notify(); NotifyState(); } } }
    public string StatusMessage { get => _statusMessage; private set { if (_statusMessage != value) { _statusMessage = value; Notify(); } } }
    public string WarningMessage { get => _warningMessage; private set { if (_warningMessage != value) { _warningMessage = value; Notify(); Notify(nameof(HasWarning)); } } }
    public string ErrorMessage { get => _errorMessage; private set { if (_errorMessage != value) { _errorMessage = value; Notify(); Notify(nameof(HasError)); } } }
    public string LastRefreshText { get => _lastRefreshText; private set { if (_lastRefreshText != value) { _lastRefreshText = value; Notify(); } } }

    public bool HasWarning => !string.IsNullOrWhiteSpace(WarningMessage);
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool CanUseSession => IsEnabled && !IsLoading;
    public bool CanWriteSession => CanUseSession && HasSession;
    public string StatusDisplay => DisplayStatus(Status);
    public string ModeDisplay => DisplayMode(Mode);
    public string SceneSummary => string.IsNullOrWhiteSpace(CurrentSceneName) ? "Сцена не выбрана" : CurrentSceneName;
    public string SceneMapSummary => string.IsNullOrWhiteSpace(ActiveSceneMapName) ? "Активная карта сцены не выбрана" : ActiveSceneMapName;
    public string CombatSummary => string.IsNullOrWhiteSpace(ActiveCombatName) ? "Активный бой не выбран" : ActiveCombatName;

    public void RefreshFlags()
    {
        try
        {
            var response = _api.SystemFeatureFlagsSnapshot();
            if (!IsOk(response))
            {
                ErrorMessage = Friendly(response, "Не удалось загрузить флаги функций текущей сессии.");
                IsEnabled = false;
                return;
            }

            var flags = Dictionaries(Get(response.Payload, "flags")).ToList();
            IsEnabled = Flag(flags, nameof(SessionFeatureFlags.UseCurrentSessionMvp))
                && Flag(flags, nameof(SessionFeatureFlags.UseSessionStateV1));
            StatusMessage = IsEnabled
                ? "Current Session MVP включён. Можно создавать и вести текущую сессию."
                : "Текущая сессия выключена флагами функций.";
            if (IsEnabled) Load();
        }
        catch (Exception ex)
        {
            ErrorMessage = "Не удалось проверить Current Session flags.";
            ClientLogService.Instance.Error("admin.session.flags.error", ex);
        }
    }

    private void Load()
    {
        if (!CanUseSession && !IsEnabled) return;
        Run("admin.session.current.load", () =>
        {
            var response = _api.SessionCurrentGet(new Dictionary<string, object>
            {
                { "campaignId", CampaignId },
                { "sessionId", SessionId }
            });
            if (!IsOk(response))
            {
                ErrorMessage = Friendly(response, "Не удалось загрузить текущую сессию.");
                return;
            }
            ApplyPayload(response.Payload);
            StatusMessage = HasSession ? "Текущая сессия загружена." : "Сессия ещё не создана.";
        });
    }

    private void CreateSession()
    {
        Run("admin.session.current.create", () =>
        {
            var response = _api.SessionCurrentCreate(new Dictionary<string, object>
            {
                { "campaignId", CampaignId },
                { "name", SessionName },
                { "description", Description },
                { "visibilityMode", VisibilityMode },
                { "isPlayerVisible", IsPlayerVisible }
            });
            HandleWrite(response, "Сессия создана.");
        });
    }

    private void UpdateSession()
    {
        Run("admin.session.current.update", () =>
        {
            var response = _api.SessionCurrentUpdate(SessionPayload(new Dictionary<string, object>
            {
                { "name", SessionName },
                { "description", Description },
                { "visibilityMode", VisibilityMode },
                { "isPlayerVisible", IsPlayerVisible },
                { "publicNotes", PublicNotes },
                { "gmNotes", GMNotes }
            }));
            HandleWrite(response, "Сессия обновлена.");
        });
    }

    private void SetScene()
    {
        Run("admin.session.current.setScene", () =>
        {
            var response = _api.SessionCurrentSetScene(SessionPayload(new Dictionary<string, object>
            {
                { "currentSceneId", CurrentSceneId },
                { "currentSceneName", CurrentSceneName }
            }));
            HandleWrite(response, "Сцена обновлена.");
        });
    }

    private void SetMode()
    {
        Run("admin.session.current.setMode", () =>
        {
            var response = _api.SessionCurrentSetMode(SessionPayload(new Dictionary<string, object> { { "mode", Mode } }));
            HandleWrite(response, "Режим обновлён.");
        });
    }

    private void SetActiveSceneMap()
    {
        Run("admin.session.current.setActiveSceneMap", () =>
        {
            var response = _api.SessionCurrentSetActiveSceneMap(SessionPayload(new Dictionary<string, object> { { "mapId", ActiveSceneMapId } }));
            HandleWrite(response, "Активная карта сцены обновлена.");
        });
    }

    private void SetActiveCombat()
    {
        Run("admin.session.current.setActiveCombat", () =>
        {
            var response = _api.SessionCurrentSetActiveCombat(SessionPayload(new Dictionary<string, object> { { "combatEncounterId", ActiveCombatEncounterId } }));
            HandleWrite(response, "Активный бой обновлён.");
        });
    }

    private void ClearActiveCombat()
    {
        Run("admin.session.current.clearActiveCombat", () =>
        {
            var response = _api.SessionCurrentClearActiveCombat(SessionPayload(new Dictionary<string, object>()));
            HandleWrite(response, "Активный бой снят.");
        });
    }

    private void SaveNotes()
    {
        Run("admin.session.current.setNotes", () =>
        {
            var response = _api.SessionCurrentSetNotes(SessionPayload(new Dictionary<string, object>
            {
                { "publicNotes", PublicNotes },
                { "gmNotes", GMNotes }
            }));
            HandleWrite(response, "Заметки сессии сохранены.");
        });
    }

    private void SendStatusCommand(Func<Dictionary<string, object>, ResponseEnvelope> command, string okMessage)
    {
        Run("admin.session.current.status", () => HandleWrite(command(SessionPayload(new Dictionary<string, object>())), okMessage));
    }

    private void HandleWrite(ResponseEnvelope response, string okMessage)
    {
        if (!IsOk(response))
        {
            ErrorMessage = Friendly(response, okMessage);
            return;
        }
        ApplyPayload(response.Payload);
        StatusMessage = okMessage;
    }

    private Dictionary<string, object> SessionPayload(Dictionary<string, object> payload)
    {
        payload["sessionId"] = SessionId;
        return payload;
    }

    private void ApplyPayload(Dictionary<string, object> payload)
    {
        HasSession = Bool(Get(payload, "hasSession"));
        Warnings.Clear();
        foreach (var warning in Strings(Get(payload, "warnings")))
        {
            if (!string.IsNullOrWhiteSpace(warning)) Warnings.Add(warning);
        }
        WarningMessage = Warnings.Count == 0 ? string.Empty : Warnings[0];

        var session = AsMap(Get(payload, "session"));
        if (!HasSession || session.Count == 0)
        {
            QuickLinks.Clear();
            LastRefreshText = DateTime.Now.ToString("HH:mm:ss");
            return;
        }

        SessionId = Str(Get(session, "sessionId"));
        CampaignId = FirstNonEmpty(Str(Get(session, "campaignId")), CampaignId);
        SessionName = FirstNonEmpty(Str(Get(session, "name")), "Без названия");
        Description = Str(Get(session, "description"));
        Status = FirstNonEmpty(Str(Get(session, "status")), CurrentSessionStatusIds.Planned);
        Mode = FirstNonEmpty(Str(Get(session, "mode")), CurrentSessionModeIds.Preparation);
        CurrentSceneId = Str(Get(session, "currentSceneId"));
        CurrentSceneName = Str(Get(session, "currentSceneName"));
        ActiveSceneMapId = Str(Get(session, "activeSceneMapId"));
        ActiveSceneMapName = Str(Get(session, "activeSceneMapName"));
        ActiveCombatEncounterId = Str(Get(session, "activeCombatEncounterId"));
        ActiveCombatName = Str(Get(session, "activeCombatName"));
        PublicNotes = Str(Get(session, "publicNotes"));
        GMNotes = Str(Get(session, "gmNotes"));
        VisibilityMode = FirstNonEmpty(Str(Get(session, "visibilityMode")), MapVisibilityModes.Party);
        IsPlayerVisible = Bool(Get(session, "isPlayerVisible"), true);
        QuickLinks.Clear();
        foreach (var link in Dictionaries(Get(session, "quickLinks")))
            QuickLinks.Add(SessionQuickLinkVm.From(link));
        LastRefreshText = DateTime.Now.ToString("HH:mm:ss");
    }

    private void Run(string logEvent, Action action)
    {
        if (IsLoading) return;
        IsLoading = true;
        ErrorMessage = string.Empty;
        try
        {
            ClientLogService.Instance.Info($"{logEvent}.start");
            action();
            ClientLogService.Instance.Info($"{logEvent}.done");
        }
        catch (Exception ex)
        {
            ErrorMessage = "Не удалось выполнить операцию Current Session.";
            ClientLogService.Instance.Error($"{logEvent}.error", ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void NotifyState()
    {
        Notify(nameof(CanUseSession));
        Notify(nameof(CanWriteSession));
    }

    private static bool ConfirmTerminal(string text)
        => MessageBox.Show(text, "Текущая сессия", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;

    private static bool IsOk(ResponseEnvelope response) => response.Status == ResponseStatus.Ok;
    private static string Friendly(ResponseEnvelope response, string fallback) => string.IsNullOrWhiteSpace(response.Message) ? fallback : response.Message;
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
    private static IEnumerable<string> Strings(object? value)
    {
        if (value is IEnumerable enumerable && value is not string)
        {
            foreach (var item in enumerable) yield return Str(item);
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
        _ => status
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
        _ => mode
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
