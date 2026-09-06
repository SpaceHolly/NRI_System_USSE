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
    private string _campaignId = string.Empty;
    private string _sessionId = string.Empty;
    private long _contextRevision;
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
    private string _statusMessage = "Выберите кампанию и сессию в верхней панели.";
    private string _warningMessage = string.Empty;
    private string _errorMessage = string.Empty;
    private string _lastRefreshText = "Не обновлялось";
    private SessionAutomationPolicyVm? _selectedAutomationPolicy;
    private string _automationPreview = "Выберите политику для безопасной проверки без записи данных.";

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
        CompleteCommand = new RelayCommand(() => { if (ConfirmTerminal("Завершить текущую сессию?")) SendStatusCommand(_api.SessionCurrentComplete, "Сессия завершена."); });
        CancelCommand = new RelayCommand(() => { if (ConfirmTerminal("Отменить текущую сессию?")) SendStatusCommand(_api.SessionCurrentCancel, "Сессия отменена."); });
        SetSceneCommand = new RelayCommand(SetScene);
        SetModeCommand = new RelayCommand(SetMode);
        SetActiveSceneMapCommand = new RelayCommand(SetActiveSceneMap);
        SetActiveCombatCommand = new RelayCommand(SetActiveCombat);
        ClearActiveCombatCommand = new RelayCommand(ClearActiveCombat);
        SaveNotesCommand = new RelayCommand(SaveNotes);
        PreviewAutomationCommand = new RelayCommand(PreviewAutomation);
    }

    public ObservableCollection<SessionQuickLinkVm> QuickLinks { get; } = new();
    public ObservableCollection<SessionAttentionVm> AttentionItems { get; } = new();
    public ObservableCollection<SessionAutomationPolicyVm> AutomationPolicies { get; } = new();
    public ObservableCollection<CampaignMemberVm> CampaignMembers { get; } = new();
    public ObservableCollection<SessionOptionVm> ModeOptions { get; } = new()
    {
        new(CurrentSessionModeIds.Preparation, "Подготовка"), new(CurrentSessionModeIds.NormalScene, "Сцена"),
        new(CurrentSessionModeIds.Combat, "Бой"), new(CurrentSessionModeIds.Travel, "Путешествие"),
        new(CurrentSessionModeIds.ShortRest, "Короткий отдых"), new(CurrentSessionModeIds.LongRest, "Долгий отдых"),
        new(CurrentSessionModeIds.Downtime, "Свободное время"), new(CurrentSessionModeIds.Maintenance, "Обслуживание"),
        new(CurrentSessionModeIds.Custom, "Другое")
    };
    public ObservableCollection<SessionOptionVm> VisibilityOptions { get; } = new()
    {
        new(MapVisibilityModes.Public, "Публично"), new(MapVisibilityModes.Party, "Участникам"),
        new(MapVisibilityModes.Hidden, "Скрыто"), new(MapVisibilityModes.GmOnly, "Только GM")
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
    public ICommand PreviewAutomationCommand { get; }

    public string CampaignId { get => _campaignId; private set { if (_campaignId == value) return; _campaignId = value; Notify(); NotifyState(); } }
    public string SessionId { get => _sessionId; private set { if (_sessionId == value) return; _sessionId = value; Notify(); NotifyState(); } }
    public string SessionName { get => _sessionName; set { if (_sessionName == value) return; _sessionName = value; Notify(); } }
    public string Description { get => _description; set { if (_description == value) return; _description = value; Notify(); } }
    public string Status { get => _status; private set { if (_status == value) return; _status = value; Notify(); Notify(nameof(StatusDisplay)); } }
    public string Mode { get => _mode; set { if (_mode == value) return; _mode = value; Notify(); Notify(nameof(ModeDisplay)); } }
    public string CurrentSceneId { get => _currentSceneId; set { if (_currentSceneId == value) return; _currentSceneId = value; Notify(); } }
    public string CurrentSceneName { get => _currentSceneName; set { if (_currentSceneName == value) return; _currentSceneName = value; Notify(); Notify(nameof(SceneSummary)); } }
    public string ActiveSceneMapId { get => _activeSceneMapId; set { if (_activeSceneMapId == value) return; _activeSceneMapId = value; Notify(); } }
    public string ActiveSceneMapName { get => _activeSceneMapName; private set { if (_activeSceneMapName == value) return; _activeSceneMapName = value; Notify(); Notify(nameof(SceneMapSummary)); } }
    public string ActiveCombatEncounterId { get => _activeCombatEncounterId; set { if (_activeCombatEncounterId == value) return; _activeCombatEncounterId = value; Notify(); } }
    public string ActiveCombatName { get => _activeCombatName; private set { if (_activeCombatName == value) return; _activeCombatName = value; Notify(); Notify(nameof(CombatSummary)); } }
    public string PublicNotes { get => _publicNotes; set { if (_publicNotes == value) return; _publicNotes = value; Notify(); } }
    public string GMNotes { get => _gmNotes; set { if (_gmNotes == value) return; _gmNotes = value; Notify(); } }
    public string VisibilityMode { get => _visibilityMode; set { if (_visibilityMode == value) return; _visibilityMode = value; Notify(); } }
    public bool IsPlayerVisible { get => _isPlayerVisible; set { if (_isPlayerVisible == value) return; _isPlayerVisible = value; Notify(); } }
    public bool HasSession { get => _hasSession; private set { if (_hasSession == value) return; _hasSession = value; Notify(); NotifyState(); } }
    public bool IsLoading { get => _isLoading; private set { if (_isLoading == value) return; _isLoading = value; Notify(); NotifyState(); } }
    public bool IsEnabled { get => _isEnabled; private set { if (_isEnabled == value) return; _isEnabled = value; Notify(); NotifyState(); } }
    public string StatusMessage { get => _statusMessage; private set { if (_statusMessage == value) return; _statusMessage = value; Notify(); } }
    public string WarningMessage { get => _warningMessage; private set { if (_warningMessage == value) return; _warningMessage = value; Notify(); } }
    public string ErrorMessage { get => _errorMessage; private set { if (_errorMessage == value) return; _errorMessage = value; Notify(); } }
    public string LastRefreshText { get => _lastRefreshText; private set { if (_lastRefreshText == value) return; _lastRefreshText = value; Notify(); } }
    public SessionAutomationPolicyVm? SelectedAutomationPolicy { get => _selectedAutomationPolicy; set { if (ReferenceEquals(_selectedAutomationPolicy, value)) return; _selectedAutomationPolicy = value; Notify(); Notify(nameof(CanPreviewAutomation)); } }
    public string AutomationPreview { get => _automationPreview; private set { if (_automationPreview == value) return; _automationPreview = value; Notify(); } }
    public bool CanUseSession => IsEnabled && !IsLoading && !string.IsNullOrWhiteSpace(CampaignId);
    public bool CanWriteSession => CanUseSession && HasSession;
    public bool CanPreviewAutomation => CanWriteSession && SelectedAutomationPolicy != null;
    public string StatusDisplay => DisplayStatus(Status);
    public string ModeDisplay => DisplayMode(Mode);
    public string SceneSummary => string.IsNullOrWhiteSpace(CurrentSceneName) ? "Сцена не выбрана" : CurrentSceneName;
    public string SceneMapSummary => string.IsNullOrWhiteSpace(ActiveSceneMapName) ? "Карта не назначена" : ActiveSceneMapName;
    public string CombatSummary => string.IsNullOrWhiteSpace(ActiveCombatName) ? "Бой не активен" : ActiveCombatName;

    public void SetContext(string campaignId, string sessionId, long contextRevision)
    {
        CampaignId = campaignId ?? string.Empty;
        SessionId = sessionId ?? string.Empty;
        _contextRevision = contextRevision;
        HasSession = !string.IsNullOrWhiteSpace(SessionId);
        ClearScopedProjection();
        if (IsEnabled && !string.IsNullOrWhiteSpace(CampaignId)) Load();
    }

    public void RefreshFlags()
    {
        try
        {
            var response = _api.SystemFeatureFlagsSnapshot();
            IsEnabled = IsOk(response) && Flag(Dictionaries(Get(response.Payload, "flags")), nameof(SessionFeatureFlags.UseCurrentSessionMvp))
                && Flag(Dictionaries(Get(response.Payload, "flags")), nameof(SessionFeatureFlags.UseSessionStateV1));
            StatusMessage = IsEnabled ? "Рабочее место GM готово." : "Проведение сессии выключено настройками сервера.";
            if (IsEnabled && !string.IsNullOrWhiteSpace(CampaignId)) Load();
        }
        catch (Exception ex) { Fail("Не удалось проверить доступность проведения сессии.", ex); }
    }

    private void Load()
    {
        if (!CanUseSession) return;
        Run("admin.session.current.load", () =>
        {
            var response = _api.SessionCurrentGet(new Dictionary<string, object> { ["campaignId"] = CampaignId, ["sessionId"] = SessionId });
            if (!IsOk(response)) { ErrorMessage = Friendly(response, "Не удалось загрузить сессию."); return; }
            ApplyPayload(response.Payload);
            LoadOperationalProjection();
            StatusMessage = HasSession ? "Сессия загружена." : "В кампании ещё нет выбранной сессии.";
        });
    }

    private void CreateSession() => Run("admin.session.current.create", () => HandleWrite(_api.SessionCurrentCreate(new Dictionary<string, object>
    {
        ["campaignId"] = CampaignId, ["name"] = SessionName, ["description"] = Description,
        ["visibilityMode"] = VisibilityMode, ["isPlayerVisible"] = IsPlayerVisible, ["expectedContextRevision"] = _contextRevision
    }), "Сессия создана."));

    private void UpdateSession() => Run("admin.session.current.update", () => HandleWrite(_api.SessionCurrentUpdate(SessionPayload(new Dictionary<string, object>
    {
        ["name"] = SessionName, ["description"] = Description, ["visibilityMode"] = VisibilityMode,
        ["isPlayerVisible"] = IsPlayerVisible, ["publicNotes"] = PublicNotes, ["gmNotes"] = GMNotes
    })), "Сессия сохранена."));

    private void SetScene() => Run("admin.session.current.setScene", () => HandleWrite(_api.SessionCurrentSetScene(SessionPayload(new Dictionary<string, object>
    { ["currentSceneId"] = CurrentSceneId, ["currentSceneName"] = CurrentSceneName })), "Сцена обновлена."));
    private void SetMode() => Run("admin.session.current.setMode", () => HandleWrite(_api.SessionCurrentSetMode(SessionPayload(new Dictionary<string, object> { ["mode"] = Mode })), "Режим обновлён."));
    private void SetActiveSceneMap() => Run("admin.session.current.setActiveSceneMap", () => HandleWrite(_api.SessionCurrentSetActiveSceneMap(SessionPayload(new Dictionary<string, object> { ["mapId"] = ActiveSceneMapId })), "Карта сцены назначена."));
    private void SetActiveCombat() => Run("admin.session.current.setActiveCombat", () => HandleWrite(_api.SessionCurrentSetActiveCombat(SessionPayload(new Dictionary<string, object> { ["combatEncounterId"] = ActiveCombatEncounterId })), "Активный бой назначен."));
    private void ClearActiveCombat() => Run("admin.session.current.clearActiveCombat", () => HandleWrite(_api.SessionCurrentClearActiveCombat(SessionPayload(new Dictionary<string, object>())), "Активный бой снят."));
    private void SaveNotes() => Run("admin.session.current.setNotes", () => HandleWrite(_api.SessionCurrentSetNotes(SessionPayload(new Dictionary<string, object> { ["publicNotes"] = PublicNotes, ["gmNotes"] = GMNotes })), "Заметки сохранены."));
    private bool SendStatusCommand(Func<Dictionary<string, object>, ResponseEnvelope> command, string message) { Run("admin.session.current.status", () => HandleWrite(command(SessionPayload(new Dictionary<string, object>())), message)); return true; }

    private void LoadOperationalProjection()
    {
        AttentionItems.Clear();
        AutomationPolicies.Clear();
        CampaignMembers.Clear();
        if (string.IsNullOrWhiteSpace(CampaignId)) return;
        var attention = _api.SessionAttentionGet(CampaignId, HasSession ? SessionId : string.Empty);
        if (IsOk(attention)) foreach (var item in Dictionaries(Get(attention.Payload, "items"))) AttentionItems.Add(SessionAttentionVm.From(item));
        if (!HasSession) return;
        var policies = _api.AutomationPolicyList(CampaignId);
        if (IsOk(policies)) foreach (var item in Dictionaries(Get(policies.Payload, "policies"))) AutomationPolicies.Add(SessionAutomationPolicyVm.From(item));
        var members = _api.CampaignMembershipList(CampaignId);
        if (IsOk(members)) foreach (var item in Dictionaries(Get(members.Payload, "members"))) CampaignMembers.Add(CampaignMemberVm.From(item));
    }

    private void PreviewAutomation()
    {
        if (!CanPreviewAutomation || SelectedAutomationPolicy == null) return;
        Run("admin.session.automation.dryRun", () =>
        {
            var response = _api.AutomationPolicyDryRun(CampaignId, SelectedAutomationPolicy.PolicyId);
            AutomationPreview = IsOk(response)
                ? FirstNonEmpty(Str(Get(response.Payload, "result")), "Проверка завершена без изменений.")
                : Friendly(response, "Не удалось проверить политику.");
        });
    }

    private void ApplyPayload(Dictionary<string, object> payload)
    {
        HasSession = Bool(Get(payload, "hasSession"));
        WarningMessage = Strings(Get(payload, "warnings")).FirstOrDefault() ?? string.Empty;
        var session = AsMap(Get(payload, "session"));
        if (!HasSession || session.Count == 0) { ClearScopedProjection(); LastRefreshText = DateTime.Now.ToString("HH:mm:ss"); return; }
        SessionId = Str(Get(session, "sessionId"));
        CampaignId = FirstNonEmpty(Str(Get(session, "campaignId")), CampaignId);
        SessionName = FirstNonEmpty(Str(Get(session, "name")), "Без названия");
        Description = Str(Get(session, "description"));
        Status = FirstNonEmpty(Str(Get(session, "status")), CurrentSessionStatusIds.Planned);
        Mode = FirstNonEmpty(Str(Get(session, "mode")), CurrentSessionModeIds.Preparation);
        CurrentSceneId = Str(Get(session, "currentSceneId")); CurrentSceneName = Str(Get(session, "currentSceneName"));
        ActiveSceneMapId = Str(Get(session, "activeSceneMapId")); ActiveSceneMapName = Str(Get(session, "activeSceneMapName"));
        ActiveCombatEncounterId = Str(Get(session, "activeCombatEncounterId")); ActiveCombatName = Str(Get(session, "activeCombatName"));
        PublicNotes = Str(Get(session, "publicNotes")); GMNotes = Str(Get(session, "gmNotes"));
        VisibilityMode = FirstNonEmpty(Str(Get(session, "visibilityMode")), MapVisibilityModes.Party);
        IsPlayerVisible = Bool(Get(session, "isPlayerVisible"), true);
        QuickLinks.Clear(); foreach (var link in Dictionaries(Get(session, "quickLinks"))) QuickLinks.Add(SessionQuickLinkVm.From(link));
        LastRefreshText = DateTime.Now.ToString("HH:mm:ss");
    }

    private void ClearScopedProjection() { AttentionItems.Clear(); AutomationPolicies.Clear(); CampaignMembers.Clear(); QuickLinks.Clear(); SelectedAutomationPolicy = null; AutomationPreview = "Выберите политику для безопасной проверки без записи данных."; }
    private void HandleWrite(ResponseEnvelope response, string message) { if (!IsOk(response)) { ErrorMessage = Friendly(response, "Операция не выполнена."); return; } ApplyPayload(response.Payload); LoadOperationalProjection(); StatusMessage = message; }
    private Dictionary<string, object> SessionPayload(Dictionary<string, object> payload) { payload["sessionId"] = SessionId; payload["expectedContextRevision"] = _contextRevision; return payload; }
    private void Run(string eventName, Action action) { if (IsLoading) return; IsLoading = true; ErrorMessage = string.Empty; try { ClientLogService.Instance.Info(eventName + ".start"); action(); ClientLogService.Instance.Info(eventName + ".done"); } catch (Exception ex) { Fail("Сервер недоступен или операция отклонена.", ex); } finally { IsLoading = false; } }
    private void Fail(string message, Exception ex) { ErrorMessage = message; ClientLogService.Instance.Error("admin.session.error", ex); }
    private void NotifyState() { Notify(nameof(CanUseSession)); Notify(nameof(CanWriteSession)); }
    private static bool ConfirmTerminal(string text) => MessageBox.Show(text, "Проведение сессии", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
    private static bool IsOk(ResponseEnvelope response) => response.Status == ResponseStatus.Ok;
    private static string Friendly(ResponseEnvelope response, string fallback) => string.IsNullOrWhiteSpace(response.Message) ? fallback : response.Message;
    private static object? Get(IDictionary<string, object> map, string key) => map.TryGetValue(key, out var value) ? value : null;
    private static Dictionary<string, object> AsMap(object? value) => value as Dictionary<string, object> ?? new Dictionary<string, object>();
    private static IEnumerable<Dictionary<string, object>> Dictionaries(object? value) => value is IEnumerable e && value is not string ? e.Cast<object>().Select(AsMap).Where(x => x.Count > 0) : Enumerable.Empty<Dictionary<string, object>>();
    private static IEnumerable<string> Strings(object? value) => value is IEnumerable e && value is not string ? e.Cast<object>().Select(Convert.ToString).Where(x => !string.IsNullOrWhiteSpace(x)).Cast<string>() : Enumerable.Empty<string>();
    private static string Str(object? value) => Convert.ToString(value) ?? string.Empty;
    private static bool Bool(object? value, bool fallback = false) { try { return value == null ? fallback : Convert.ToBoolean(value); } catch { return fallback; } }
    private static string FirstNonEmpty(params string[] values) => values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;
    private static bool Flag(IEnumerable<Dictionary<string, object>> flags, string key) => flags.Any(x =>
        (MatchesFlagName(Str(Get(x, "name")), key)
         || MatchesFlagName(Str(Get(x, "canonicalKey")), key)
         || string.Equals(Str(Get(x, "key")), key, StringComparison.OrdinalIgnoreCase))
        && Bool(Get(x, "effectiveValue"), Bool(Get(x, "effective"))));
    private static bool MatchesFlagName(string value, string key) =>
        string.Equals(value, key, StringComparison.OrdinalIgnoreCase)
        || value.EndsWith("." + key, StringComparison.OrdinalIgnoreCase);
    private static string DisplayStatus(string value) => value switch { CurrentSessionStatusIds.Planned => "Планируется", CurrentSessionStatusIds.Active => "Активна", CurrentSessionStatusIds.Paused => "Пауза", CurrentSessionStatusIds.Completed => "Завершена", CurrentSessionStatusIds.Cancelled => "Отменена", CurrentSessionStatusIds.Archived => "Архив", _ => value };
    private static string DisplayMode(string value) => value switch { CurrentSessionModeIds.Preparation => "Подготовка", CurrentSessionModeIds.NormalScene => "Сцена", CurrentSessionModeIds.Combat => "Бой", CurrentSessionModeIds.Travel => "Путешествие", CurrentSessionModeIds.ShortRest => "Короткий отдых", CurrentSessionModeIds.LongRest => "Долгий отдых", CurrentSessionModeIds.Downtime => "Свободное время", CurrentSessionModeIds.Maintenance => "Обслуживание", _ => "Другое" };
}

public sealed class SessionOptionVm
{
    public SessionOptionVm(string id, string title) { Id = id; Title = title; }
    public string Id { get; }
    public string Title { get; }
    public override string ToString() => Title;
}
public sealed class SessionQuickLinkVm
{
    public string Title { get; set; } = string.Empty; public bool Enabled { get; set; } public string StateText => Enabled ? "Доступно" : "Не назначено";
    public static SessionQuickLinkVm From(Dictionary<string, object> p) => new() { Title = Convert.ToString(p.TryGetValue("title", out var v) ? v : null) ?? string.Empty, Enabled = p.TryGetValue("enabled", out var e) && e is bool b && b };
}
public sealed class SessionAttentionVm
{
    public string Title { get; set; } = string.Empty; public string Summary { get; set; } = string.Empty; public string State { get; set; } = string.Empty;
    public string ActionRoute { get; set; } = string.Empty; public string SourceId { get; set; } = string.Empty;
    public static SessionAttentionVm From(Dictionary<string, object> p) => new() { Title = Text(p, "title"), Summary = FirstNonEmpty(Text(p, "actor"), "Системное событие"), State = Severity(Text(p, "severity")), ActionRoute = Text(p, "route"), SourceId = Text(p, "sourceId") };
    private static string Text(Dictionary<string, object> p, string k) => Convert.ToString(p.TryGetValue(k, out var v) ? v : null) ?? string.Empty;
    private static string FirstNonEmpty(params string[] values) => values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;
    private static string Severity(string value) => value switch { "high" => "Важно", "medium" => "Ожидает решения", _ => "К сведению" };
}
public sealed class SessionAutomationPolicyVm
{
    public string PolicyId { get; set; } = string.Empty; public string Name { get; set; } = string.Empty; public string Trigger { get; set; } = string.Empty; public string Decision { get; set; } = string.Empty; public string Description { get; set; } = string.Empty;
    public static SessionAutomationPolicyVm From(Dictionary<string, object> p) => new() { PolicyId = Text(p, "policyId"), Name = Text(p, "name"), Trigger = Text(p, "trigger"), Decision = Text(p, "decisionMode"), Description = Text(p, "description") };
    private static string Text(Dictionary<string, object> p, string k) => Convert.ToString(p.TryGetValue(k, out var v) ? v : null) ?? string.Empty;
}
public sealed class CampaignMemberVm
{
    public string Name { get; set; } = string.Empty; public string Login { get; set; } = string.Empty; public string Role { get; set; } = string.Empty; public string Status { get; set; } = string.Empty; public string CapabilitySummary { get; set; } = string.Empty;
    public string Identity => string.IsNullOrWhiteSpace(Login) ? Name : $"{Name} ({Login})";
    public string RightsSummary => string.IsNullOrWhiteSpace(CapabilitySummary) ? "Права определяются ролью кампании." : CapabilitySummary;
    public static CampaignMemberVm From(Dictionary<string, object> p) => new() { Name = Text(p, "accountName"), Login = Text(p, "login"), Role = Text(p, "role"), Status = Text(p, "status"), CapabilitySummary = Text(p, "capabilitySummary") };
    private static string Text(Dictionary<string, object> p, string k) => Convert.ToString(p.TryGetValue(k, out var v) ? v : null) ?? string.Empty;
}
