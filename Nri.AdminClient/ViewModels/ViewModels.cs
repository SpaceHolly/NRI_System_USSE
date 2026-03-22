using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Windows.Input;
using System.Windows.Threading;
using Nri.AdminClient.Networking;
using Nri.Shared.Configuration;
using Nri.Shared.Contracts;

namespace Nri.AdminClient.ViewModels;

public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void Notify([CallerMemberName] string? p = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
}

public sealed class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;
    public void Execute(object? parameter) => _execute();
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

public sealed class RelayCommand<T> : ICommand
{
    private readonly Action<T?> _execute;
    private readonly Func<T?, bool>? _canExecute;

    public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => _canExecute?.Invoke((T?)parameter) ?? true;
    public void Execute(object? parameter) => _execute((T?)parameter);
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

public class RowVm : ViewModelBase
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Extra { get; set; } = string.Empty;
}

public sealed class WorkspacePanelDescriptor : ViewModelBase
{
    private bool _isDetached;
    private bool _isVisible = true;
    private double _windowLeft = 120;
    private double _windowTop = 120;
    private double _windowWidth = 920;
    private double _windowHeight = 720;

    public WorkspacePanelDescriptor(string panelId, string title, bool canDetach)
    {
        PanelId = panelId;
        Title = title;
        CanDetach = canDetach;
    }

    public string PanelId { get; }
    public string Title { get; }
    public bool CanDetach { get; }

    public bool IsDetached
    {
        get => _isDetached;
        set
        {
            if (_isDetached != value)
            {
                _isDetached = value;
                Notify();
                Notify(nameof(PanelState));
                Notify(nameof(StateBadge));
            }
        }
    }

    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (_isVisible != value)
            {
                _isVisible = value;
                Notify();
                Notify(nameof(PanelState));
                Notify(nameof(StateBadge));
            }
        }
    }

    public double WindowLeft
    {
        get => _windowLeft;
        set { if (Math.Abs(_windowLeft - value) > 0.1) { _windowLeft = value; Notify(); } }
    }

    public double WindowTop
    {
        get => _windowTop;
        set { if (Math.Abs(_windowTop - value) > 0.1) { _windowTop = value; Notify(); } }
    }

    public double WindowWidth
    {
        get => _windowWidth;
        set { if (Math.Abs(_windowWidth - value) > 0.1) { _windowWidth = value; Notify(); } }
    }

    public double WindowHeight
    {
        get => _windowHeight;
        set { if (Math.Abs(_windowHeight - value) > 0.1) { _windowHeight = value; Notify(); } }
    }

    public string PanelState => !IsVisible ? "Скрыта" : IsDetached ? "Вынесена" : "Встроена";
    public string StateBadge => !IsVisible ? "◌" : IsDetached ? "⇱" : "●";
}

[DataContract]
public sealed class ConnectionSettingsModel
{
    [DataMember(Order = 1)] public string ServerHost { get; set; } = "127.0.0.1";
    [DataMember(Order = 2)] public int ServerPort { get; set; } = 5000;
    [DataMember(Order = 3)] public string LastServerHost { get; set; } = "127.0.0.1";
    [DataMember(Order = 4)] public int LastServerPort { get; set; } = 5000;
}

[DataContract]
public sealed class WorkspaceLayoutModel
{
    [DataMember(Order = 1)] public List<WorkspacePanelLayoutItem> Panels { get; set; } = new List<WorkspacePanelLayoutItem>();
}

[DataContract]
public sealed class WorkspacePanelLayoutItem
{
    [DataMember(Order = 1)] public string PanelId { get; set; } = string.Empty;
    [DataMember(Order = 2)] public bool IsDetached { get; set; }
    [DataMember(Order = 3)] public bool IsVisible { get; set; } = true;
    [DataMember(Order = 4)] public double Left { get; set; }
    [DataMember(Order = 5)] public double Top { get; set; }
    [DataMember(Order = 6)] public double Width { get; set; } = 920;
    [DataMember(Order = 7)] public double Height { get; set; } = 720;
}

public class AdminMainViewModel : ViewModelBase
{
    private readonly ClientSessionState _session = new ClientSessionState();
    private readonly JsonTcpClient _client;
    private readonly CommandApi _api;
    private readonly DispatcherTimer _poller;
    private readonly string _appDataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Nri.AdminClient");
    private string _connectionState = "Оффлайн";
    private string _connectionStatusDetail = "Соединение не установлено.";
    private string _sessionSummary = "Сессия не активна";
    private string _serverHostInput = "127.0.0.1";
    private string _serverPortInput = "5000";
    private string _lastServerHost = "127.0.0.1";
    private int _lastServerPort = 5000;
    private bool _isConnectionPopupOpen;
    private bool _isOnline;
    private bool _isConnectedToServer;
    private bool _isAuthenticated;
    private string _lastErrorMessage = string.Empty;
    private string _lastStatusMessage = "Ожидание подключения";
    private int _locksCount;
    private string _selectedSection = "Обзор";

    public AdminMainViewModel()
    {
        Directory.CreateDirectory(_appDataDirectory);

        _client = new JsonTcpClient(new ClientConfig(), _session);
        _api = new CommandApi(_client);

        LoginCommand = new RelayCommand(Login);
        RefreshCommand = new RelayCommand(RefreshAll);
        OpenConnectionPopupCommand = new RelayCommand(() => IsConnectionPopupOpen = !IsConnectionPopupOpen);
        ConnectToServerCommand = new RelayCommand(ConnectToServer);
        ApplyConnectionSettingsCommand = new RelayCommand(ApplyConnectionSettings);
        ResetConnectionDefaultsCommand = new RelayCommand(ResetConnectionDefaults);
        UseSavedConnectionSettingsCommand = new RelayCommand(UseSavedConnectionSettings);
        ApproveCommand = new RelayCommand(ApproveSelected);
        ArchiveCommand = new RelayCommand(ArchiveSelected);
        LoadOwnerCharactersCommand = new RelayCommand(LoadOwnerCharacters);
        OpenCharacterCommand = new RelayCommand(OpenCharacter);
        AcquireLockCommand = new RelayCommand(AcquireLock);
        ReleaseLockCommand = new RelayCommand(ReleaseLock);
        ForceUnlockCommand = new RelayCommand(ForceUnlock);
        SaveBasicInfoCommand = new RelayCommand(SaveBasicInfo);
        SaveStatsCommand = new RelayCommand(SaveStats);
        SaveMoneyCommand = new RelayCommand(SaveMoney);
        ApproveRequestCommand = new RelayCommand(ApproveRequest);
        RejectRequestCommand = new RelayCommand(RejectRequest);
        CombatStartCommand = new RelayCommand(CombatStart);
        CombatEndCommand = new RelayCommand(CombatEnd);
        CombatRefreshCommand = new RelayCommand(CombatRefresh);
        CombatNextTurnCommand = new RelayCommand(CombatNextTurn);
        CombatPrevTurnCommand = new RelayCommand(CombatPrevTurn);
        CombatNextRoundCommand = new RelayCommand(CombatNextRound);
        CombatSkipTurnCommand = new RelayCommand(CombatSkipTurn);
        CombatAddParticipantCommand = new RelayCommand(CombatAddParticipant);
        CombatRemoveParticipantCommand = new RelayCommand(CombatRemoveParticipant);
        CombatDetachCompanionCommand = new RelayCommand(CombatDetachCompanion);
        DefinitionsReloadCommand = new RelayCommand(DefinitionsReload);
        LoadClassTreeCommand = new RelayCommand(LoadClassTree);
        AcquireClassNodeCommand = new RelayCommand(AcquireClassNode);
        LoadSkillsCommand = new RelayCommand(LoadSkills);
        AcquireSkillCommand = new RelayCommand(AcquireSkill);
        ChatSendCommand = new RelayCommand(ChatSend);
        ChatRefreshCommand = new RelayCommand(ChatRefresh);
        ChatMuteUserCommand = new RelayCommand(ChatMuteUser);
        ChatUnmuteUserCommand = new RelayCommand(ChatUnmuteUser);
        ChatLockPlayersCommand = new RelayCommand(ChatLockPlayers);
        ChatUnlockPlayersCommand = new RelayCommand(ChatUnlockPlayers);
        ChatSetSlowModeCommand = new RelayCommand(ChatSetSlowMode);
        AudioRefreshCommand = new RelayCommand(AudioRefresh);
        AudioSetModeCommand = new RelayCommand(AudioSetMode);
        AudioClearOverrideCommand = new RelayCommand(AudioClearOverride);
        AudioNextTrackCommand = new RelayCommand(AudioNextTrack);
        AudioSelectTrackCommand = new RelayCommand(AudioSelectTrack);
        AudioReloadLibraryCommand = new RelayCommand(AudioReloadLibrary);
        VisibilityLoadCommand = new RelayCommand(VisibilityLoad);
        VisibilitySaveCommand = new RelayCommand(VisibilitySave);
        NotesRefreshCommand = new RelayCommand(NotesRefresh);
        NotesCreateCommand = new RelayCommand(NotesCreate);
        NotesArchiveCommand = new RelayCommand(NotesArchive);
        ReferenceRefreshCommand = new RelayCommand(ReferenceRefresh);
        ReferenceCreateCommand = new RelayCommand(ReferenceCreate);
        ReferenceUpdateCommand = new RelayCommand(ReferenceUpdate);
        ReferenceArchiveCommand = new RelayCommand(ReferenceArchive);
        BackupRefreshCommand = new RelayCommand(BackupRefresh);
        BackupCreateCommand = new RelayCommand(BackupCreate);
        BackupRestoreCommand = new RelayCommand(BackupRestore);
        BackupExportCommand = new RelayCommand(BackupExport);
        DiagnosticsRefreshCommand = new RelayCommand(DiagnosticsRefresh);
        SelectSectionCommand = new RelayCommand<string>(SelectSection);
        DetachWorkspacePanelCommand = new RelayCommand<string>(DetachWorkspacePanel);
        AttachWorkspacePanelCommand = new RelayCommand<string>(AttachWorkspacePanel);
        ToggleWorkspacePanelVisibilityCommand = new RelayCommand<string>(ToggleWorkspacePanelVisibility);
        ShowWorkspacePanelCommand = new RelayCommand<string>(ShowWorkspacePanel);
        HideWorkspacePanelCommand = new RelayCommand<string>(HideWorkspacePanel);

        InitializeWorkspacePanels();
        LoadConnectionSettings();
        LoadWorkspaceLayout();
        RefreshConnectionSummary();

        _poller = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        _poller.Tick += (_, _) => RefreshAll();
    }

    public string LoginText { get; set; } = string.Empty;
    public string PasswordText { get; set; } = string.Empty;
    public string ConnectionState { get => _connectionState; set { _connectionState = value; Notify(); } }
    public string ConnectionStatusDetail { get => _connectionStatusDetail; set { _connectionStatusDetail = value; Notify(); } }
    public string SessionSummary { get => _sessionSummary; set { _sessionSummary = value; Notify(); } }
    public bool IsOnline { get => _isOnline; set { _isOnline = value; Notify(); } }
    public bool IsConnectedToServer { get => _isConnectedToServer; set { _isConnectedToServer = value; Notify(); Notify(nameof(ConnectionStage)); Notify(nameof(LoginState)); Notify(nameof(ArePrivilegedSectionsEnabled)); Notify(nameof(SectionAccessHint)); } }
    public bool IsAuthenticated { get => _isAuthenticated; set { _isAuthenticated = value; Notify(); Notify(nameof(ConnectionStage)); Notify(nameof(LoginState)); Notify(nameof(ArePrivilegedSectionsEnabled)); Notify(nameof(SectionAccessHint)); } }
    public string LastErrorMessage { get => _lastErrorMessage; set { _lastErrorMessage = value; Notify(); Notify(nameof(HasConnectionError)); Notify(nameof(ConnectionStage)); } }
    public string LastStatusMessage { get => _lastStatusMessage; set { _lastStatusMessage = value; Notify(); } }
    public int LocksCount { get => _locksCount; set { _locksCount = value; Notify(); } }
    public bool HasConnectionError => !string.IsNullOrWhiteSpace(LastErrorMessage);
    public bool ArePrivilegedSectionsEnabled => IsConnectedToServer && IsAuthenticated;
    public string ConnectionStage => HasConnectionError ? "Ошибка подключения" : IsAuthenticated ? "Вошли как админ" : IsConnectedToServer ? "Подключено, вход не выполнен" : "Нет подключения";
    public string LoginState => IsAuthenticated ? $"Администратор: {LoginSummary}" : IsConnectedToServer ? "Сервер доступен, войдите как админ" : "Не авторизован";
    public string SectionAccessHint => ArePrivilegedSectionsEnabled ? "Рабочие разделы активны" : IsConnectedToServer ? "Для рабочих разделов выполните вход" : "Подключитесь к серверу, чтобы активировать рабочие разделы";
    public bool IsConnectionPopupOpen { get => _isConnectionPopupOpen; set { _isConnectionPopupOpen = value; Notify(); } }
    public string ServerHostInput { get => _serverHostInput; set { _serverHostInput = value; Notify(); } }
    public string ServerPortInput { get => _serverPortInput; set { _serverPortInput = value; Notify(); } }
    public string LastServerHost { get => _lastServerHost; set { _lastServerHost = value; Notify(); } }
    public int LastServerPort { get => _lastServerPort; set { _lastServerPort = value; Notify(); } }
    public string SelectedSection { get => _selectedSection; set { _selectedSection = value; Notify(); } }
    public string CurrentEndpoint => $"{_client.ServerHost}:{_client.ServerPort}";
    public string LoginSummary => string.IsNullOrWhiteSpace(LoginText) ? "Не авторизован" : LoginText;
    public int PendingAccountsCount => PendingAccounts.Count;
    public int PlayersCount => Players.Count;
    public int CharactersCount => Characters.Count;
    public int PendingRequestsCount => PendingRequests.Count;
    public int ActivePlayersCount => Players.Count(player => player.Extra.IndexOf("online=True", StringComparison.OrdinalIgnoreCase) >= 0);
    public bool HasActiveCombat => CombatRows.Any(row => row.IndexOf("Status:", StringComparison.OrdinalIgnoreCase) >= 0 && row.IndexOf("Ended", StringComparison.OrdinalIgnoreCase) < 0);
    public string ChatSummary => ChatRows.Count == 0 ? "Чат: нет данных" : $"Чат: {ChatRows.Count} сообщений";
    public string AudioSummary => string.IsNullOrWhiteSpace(AudioStateText) ? "Музыка: нет данных" : $"Музыка: {AudioStateText}";
    public string DiagnosticsSummary => DiagnosticsRows.Count == 0 ? "Диагностика: не загружена" : DiagnosticsRows.First();
    public string SessionStateSummary => CombatRows.FirstOrDefault(row => row.StartsWith("Status:", StringComparison.OrdinalIgnoreCase))?.Split(':').Skip(1).FirstOrDefault()?.Trim() ?? (ArePrivilegedSectionsEnabled ? "Спокойное состояние" : "Недоступно до входа");
    public int ActiveCombatParticipantsCount => CombatRows.Count(row => row.StartsWith("P:", StringComparison.OrdinalIgnoreCase));
    public string CombatTrackerSummary => HasActiveCombat ? $"Бой активен • участников: {ActiveCombatParticipantsCount}" : ActiveCombatParticipantsCount > 0 ? $"Трекер загружен • участников: {ActiveCombatParticipantsCount}" : "Трекер боя ждёт данных";
    public string ContentSummary => $"Classes: {ClassTreeRows.Count} • Skills: {SkillStateRows.Count}";
    public string ReferenceSummary => ReferenceRows.Count == 0 ? "Reference data: нет загруженных записей" : $"Reference data: {ReferenceRows.Count} записей типа {ReferenceType}";
    public string BackupSummary => BackupRows.Count == 0 ? "Backups: ещё не загружены" : $"Backups: {BackupRows.Count}, последний: {BackupRows[0]}";
    public string WorkspaceSummary => $"Панели: встроено {WorkspacePanels.Count(panel => panel.IsVisible && !panel.IsDetached)}, вынесено {WorkspacePanels.Count(panel => panel.IsVisible && panel.IsDetached)}, скрыто {WorkspacePanels.Count(panel => !panel.IsVisible)}";
    public string WorkspaceLayoutPath => Path.Combine(_appDataDirectory, "workspace.layout.json");
    public string ConnectionSettingsPath => Path.Combine(_appDataDirectory, "connection.settings.json");

    public string SelectedPendingAccountId { get; set; } = string.Empty;
    public string SelectedOwnerUserId { get; set; } = string.Empty;
    public string SelectedCharacterId { get; set; } = string.Empty;
    public string SelectedPendingRequestId { get; set; } = string.Empty;
    public string RequestComment { get; set; } = string.Empty;
    public string CombatSessionId { get; set; } = "default";
    public string NewParticipantName { get; set; } = "New NPC";
    public string NewParticipantKind { get; set; } = "Npc";
    public string SelectedCombatParticipantId { get; set; } = string.Empty;
    public string LockStateText { get; set; } = string.Empty;
    public string SelectedClassNodeId { get; set; } = string.Empty;
    public string SelectedSkillId { get; set; } = string.Empty;
    public string DefinitionVersionText { get; set; } = string.Empty;
    public string ChatSessionId { get; set; } = "default";
    public string ChatMessageText { get; set; } = string.Empty;
    public string ChatMessageType { get; set; } = "Public";
    public string ChatModerationUserId { get; set; } = string.Empty;
    public string ChatModerationReason { get; set; } = string.Empty;
    public int ChatSlowPublicSeconds { get; set; }
    public int ChatSlowHiddenSeconds { get; set; }
    public int ChatSlowAdminOnlySeconds { get; set; }
    public string ChatUnreadText { get; set; } = string.Empty;
    public string AudioSessionId { get; set; } = "default";
    public string AudioModeInput { get; set; } = "Auto";
    public string AudioCategoryInput { get; set; } = "Normal";
    public string AudioSelectedTrackId { get; set; } = string.Empty;
    public string AudioStateText { get; set; } = string.Empty;
    public bool VisHideDescription { get; set; }
    public bool VisHideBackstory { get; set; }
    public bool VisHideStats { get; set; }
    public bool VisHideReputation { get; set; }
    public string NoteSessionId { get; set; } = "default";
    public string NoteTargetType { get; set; } = "character";
    public string NoteTargetId { get; set; } = string.Empty;
    public string NoteTitle { get; set; } = string.Empty;
    public string NoteText { get; set; } = string.Empty;
    public string NoteVisibility { get; set; } = "AdminOnly";
    public string SelectedNoteId { get; set; } = string.Empty;
    public string ReferenceWorldId { get; set; } = "default-world";
    public string ReferenceType { get; set; } = "race";
    public string ReferenceId { get; set; } = string.Empty;
    public string ReferenceKey { get; set; } = string.Empty;
    public string ReferenceDisplayName { get; set; } = string.Empty;
    public string ReferenceDataJson { get; set; } = "{}";
    public string BackupLabel { get; set; } = string.Empty;
    public string SelectedBackupId { get; set; } = string.Empty;
    public string EditName { get; set; } = string.Empty;
    public string EditRace { get; set; } = string.Empty;
    public string EditHeight { get; set; } = string.Empty;
    public string EditDescription { get; set; } = string.Empty;
    public string EditBackstory { get; set; } = string.Empty;
    public int EditAge { get; set; }
    public int Health { get; set; }
    public int PhysicalArmor { get; set; }
    public int MagicalArmor { get; set; }
    public int Morale { get; set; }
    public int Strength { get; set; }
    public int Dexterity { get; set; }
    public int Endurance { get; set; }
    public int Wisdom { get; set; }
    public int Intellect { get; set; }
    public int Charisma { get; set; }
    public long Iron { get; set; }
    public long Bronze { get; set; }
    public long Silver { get; set; }
    public long Gold { get; set; }

    public ObservableCollection<RowVm> PendingAccounts { get; } = new ObservableCollection<RowVm>();
    public ObservableCollection<RowVm> Players { get; } = new ObservableCollection<RowVm>();
    public ObservableCollection<RowVm> Characters { get; } = new ObservableCollection<RowVm>();
    public ObservableCollection<string> InventoryRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<string> HoldingsRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<string> ReputationRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<string> CompanionRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<RowVm> PendingRequests { get; } = new ObservableCollection<RowVm>();
    public ObservableCollection<string> RequestHistoryRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<string> DiceFeedRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<string> CombatRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<string> CombatHistoryRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<string> ClassTreeRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<string> SkillStateRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<string> ChatRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<string> ChatRestrictionRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<string> AudioLibraryRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<string> NotesRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<string> ReferenceRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<string> BackupRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<string> DiagnosticsRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<string> OverviewActivityRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<WorkspacePanelDescriptor> WorkspacePanels { get; } = new ObservableCollection<WorkspacePanelDescriptor>();

    public WorkspacePanelDescriptor CharacterEditorPanel => GetPanelById("CharacterEditor");
    public WorkspacePanelDescriptor NotesPanel => GetPanelById("NotesManagement");
    public WorkspacePanelDescriptor RequestsPanel => GetPanelById("Requests");
    public WorkspacePanelDescriptor DiceFeedPanel => GetPanelById("DiceFeed");
    public WorkspacePanelDescriptor CombatTrackerPanel => GetPanelById("CombatTracker");
    public WorkspacePanelDescriptor SessionChatPanel => GetPanelById("SessionChat");
    public WorkspacePanelDescriptor SessionAudioPanel => GetPanelById("SessionAudio");

    public ICommand LoginCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand OpenConnectionPopupCommand { get; }
    public ICommand ConnectToServerCommand { get; }
    public ICommand ApplyConnectionSettingsCommand { get; }
    public ICommand ResetConnectionDefaultsCommand { get; }
    public ICommand UseSavedConnectionSettingsCommand { get; }
    public ICommand ApproveCommand { get; }
    public ICommand ArchiveCommand { get; }
    public ICommand LoadOwnerCharactersCommand { get; }
    public ICommand OpenCharacterCommand { get; }
    public ICommand AcquireLockCommand { get; }
    public ICommand ReleaseLockCommand { get; }
    public ICommand ForceUnlockCommand { get; }
    public ICommand SaveBasicInfoCommand { get; }
    public ICommand SaveStatsCommand { get; }
    public ICommand SaveMoneyCommand { get; }
    public ICommand ApproveRequestCommand { get; }
    public ICommand RejectRequestCommand { get; }
    public ICommand CombatStartCommand { get; }
    public ICommand CombatEndCommand { get; }
    public ICommand CombatRefreshCommand { get; }
    public ICommand CombatNextTurnCommand { get; }
    public ICommand CombatPrevTurnCommand { get; }
    public ICommand CombatNextRoundCommand { get; }
    public ICommand CombatSkipTurnCommand { get; }
    public ICommand CombatAddParticipantCommand { get; }
    public ICommand CombatRemoveParticipantCommand { get; }
    public ICommand CombatDetachCompanionCommand { get; }
    public ICommand DefinitionsReloadCommand { get; }
    public ICommand LoadClassTreeCommand { get; }
    public ICommand AcquireClassNodeCommand { get; }
    public ICommand LoadSkillsCommand { get; }
    public ICommand AcquireSkillCommand { get; }
    public ICommand ChatSendCommand { get; }
    public ICommand ChatRefreshCommand { get; }
    public ICommand ChatMuteUserCommand { get; }
    public ICommand ChatUnmuteUserCommand { get; }
    public ICommand ChatLockPlayersCommand { get; }
    public ICommand ChatUnlockPlayersCommand { get; }
    public ICommand ChatSetSlowModeCommand { get; }
    public ICommand AudioRefreshCommand { get; }
    public ICommand AudioSetModeCommand { get; }
    public ICommand AudioClearOverrideCommand { get; }
    public ICommand AudioNextTrackCommand { get; }
    public ICommand AudioSelectTrackCommand { get; }
    public ICommand AudioReloadLibraryCommand { get; }
    public ICommand VisibilityLoadCommand { get; }
    public ICommand VisibilitySaveCommand { get; }
    public ICommand NotesRefreshCommand { get; }
    public ICommand NotesCreateCommand { get; }
    public ICommand NotesArchiveCommand { get; }
    public ICommand ReferenceRefreshCommand { get; }
    public ICommand ReferenceCreateCommand { get; }
    public ICommand ReferenceUpdateCommand { get; }
    public ICommand ReferenceArchiveCommand { get; }
    public ICommand BackupRefreshCommand { get; }
    public ICommand BackupCreateCommand { get; }
    public ICommand BackupRestoreCommand { get; }
    public ICommand BackupExportCommand { get; }
    public ICommand DiagnosticsRefreshCommand { get; }
    public ICommand SelectSectionCommand { get; }
    public ICommand DetachWorkspacePanelCommand { get; }
    public ICommand AttachWorkspacePanelCommand { get; }
    public ICommand ToggleWorkspacePanelVisibilityCommand { get; }
    public ICommand ShowWorkspacePanelCommand { get; }
    public ICommand HideWorkspacePanelCommand { get; }

    public void LoadConnectionSettings()
    {
        var settings = ReadJson(ConnectionSettingsPath, new ConnectionSettingsModel());
        LastServerHost = settings.LastServerHost;
        LastServerPort = settings.LastServerPort;
        ServerHostInput = settings.ServerHost;
        ServerPortInput = settings.ServerPort.ToString();

        if (TryValidateConnectionSettings(ServerHostInput, ServerPortInput, out var host, out var port, out _))
        {
            _client.UpdateEndpoint(host, port);
        }

        RefreshConnectionSummary();
    }

    public void SaveConnectionSettings()
    {
        int.TryParse(ServerPortInput, out var currentPort);
        WriteJson(ConnectionSettingsPath, new ConnectionSettingsModel
        {
            ServerHost = ServerHostInput,
            ServerPort = currentPort <= 0 ? _client.ServerPort : currentPort,
            LastServerHost = LastServerHost,
            LastServerPort = LastServerPort
        });
    }

    public bool TryValidateConnectionSettings(string hostInput, string portInput, out string normalizedHost, out int port, out string error)
    {
        normalizedHost = hostInput.Trim();
        error = string.Empty;
        port = 0;

        if (string.IsNullOrWhiteSpace(normalizedHost))
        {
            error = "Укажите host.";
            return false;
        }

        if (!string.Equals(normalizedHost, "localhost", StringComparison.OrdinalIgnoreCase)
            && !(IPAddress.TryParse(normalizedHost, out var address) && address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork))
        {
            error = "Разрешены только localhost или IPv4-адрес.";
            return false;
        }

        if (!int.TryParse(portInput, out port) || port < 1 || port > 65535)
        {
            error = "Порт должен быть в диапазоне 1..65535.";
            return false;
        }

        return true;
    }

    public void ApplyConnectionSettings()
    {
        if (!TryValidateConnectionSettings(ServerHostInput, ServerPortInput, out var host, out var port, out var error))
        {
            SetConnectionError(error);
            return;
        }

        _client.UpdateEndpoint(host, port);
        LastServerHost = host;
        LastServerPort = port;
        SaveConnectionSettings();
        SetDisconnectedState($"Параметры сохранены: {host}:{port}");
        RefreshConnectionSummary();
    }

    public void ConnectToServer()
    {
        if (!TryValidateConnectionSettings(ServerHostInput, ServerPortInput, out var host, out var port, out var error))
        {
            SetConnectionError(error);
            return;
        }

        try
        {
            _client.UpdateEndpoint(host, port);
            _client.Connect();
            LastServerHost = host;
            LastServerPort = port;
            SaveConnectionSettings();
            IsAuthenticated = false;
            SetConnectedState($"Соединение установлено: {host}:{port}");
            IsConnectionPopupOpen = false;
        }
        catch (Exception ex)
        {
            SetConnectionError($"Не удалось подключиться к {host}:{port}. {ex.Message}");
        }
    }

    public void ResetConnectionDefaults()
    {
        ServerHostInput = "127.0.0.1";
        ServerPortInput = "5000";
        SetDisconnectedState("Используются значения по умолчанию.");
    }

    public void UseSavedConnectionSettings()
    {
        ServerHostInput = LastServerHost;
        ServerPortInput = LastServerPort.ToString();
        SetDisconnectedState($"Подставлены последние сохранённые настройки: {LastServerHost}:{LastServerPort}");
    }

    public void SetConnectedState(string detail)
    {
        IsConnectedToServer = true;
        IsOnline = true;
        LastErrorMessage = string.Empty;
        LastStatusMessage = detail;
        ConnectionState = IsAuthenticated ? "Онлайн / Admin" : "Подключено";
        ConnectionStatusDetail = detail;
        RefreshConnectionSummary();
    }

    public void SetDisconnectedState(string detail)
    {
        IsConnectedToServer = false;
        IsAuthenticated = false;
        IsOnline = false;
        LastStatusMessage = detail;
        ConnectionState = "Оффлайн";
        ConnectionStatusDetail = detail;
        RefreshConnectionSummary();
    }

    public void SetConnectionError(string detail)
    {
        _client.Disconnect();
        _poller.Stop();
        LastErrorMessage = detail;
        SetDisconnectedState(detail);
    }

    public void RefreshConnectionSummary()
    {
        SessionSummary = $"Endpoint: {CurrentEndpoint} • Stage: {ConnectionStage} • {LoginState} • Pending: {PendingAccountsCount} • Players: {PlayersCount} • Characters: {CharactersCount} • Requests: {PendingRequestsCount} • Locks: {LocksCount}";
        RefreshOverviewActivity();
        Notify(nameof(CurrentEndpoint));
        Notify(nameof(LoginSummary));
        Notify(nameof(PendingAccountsCount));
        Notify(nameof(PlayersCount));
        Notify(nameof(CharactersCount));
        Notify(nameof(PendingRequestsCount));
        Notify(nameof(ActivePlayersCount));
        Notify(nameof(HasActiveCombat));
        Notify(nameof(ChatSummary));
        Notify(nameof(AudioSummary));
        Notify(nameof(DiagnosticsSummary));
        Notify(nameof(SessionStateSummary));
        Notify(nameof(ActiveCombatParticipantsCount));
        Notify(nameof(CombatTrackerSummary));
        Notify(nameof(ContentSummary));
        Notify(nameof(ReferenceSummary));
        Notify(nameof(BackupSummary));
        Notify(nameof(WorkspaceSummary));
    }

    public void LoadWorkspaceLayout()
    {
        var layout = ReadJson(WorkspaceLayoutPath, new WorkspaceLayoutModel());
        foreach (var item in layout.Panels)
        {
            var panel = WorkspacePanels.FirstOrDefault(p => p.PanelId == item.PanelId);
            if (panel == null)
            {
                continue;
            }

            panel.IsDetached = item.IsDetached && panel.CanDetach;
            panel.IsVisible = item.IsVisible;
            panel.WindowLeft = item.Left;
            panel.WindowTop = item.Top;
            panel.WindowWidth = item.Width > 200 ? item.Width : 920;
            panel.WindowHeight = item.Height > 200 ? item.Height : 720;
        }
    }

    public void SaveWorkspaceLayout()
    {
        var layout = new WorkspaceLayoutModel
        {
            Panels = WorkspacePanels.Select(panel => new WorkspacePanelLayoutItem
            {
                PanelId = panel.PanelId,
                IsDetached = panel.IsDetached,
                IsVisible = panel.IsVisible,
                Left = panel.WindowLeft,
                Top = panel.WindowTop,
                Width = panel.WindowWidth,
                Height = panel.WindowHeight
            }).ToList()
        };

        WriteJson(WorkspaceLayoutPath, layout);
    }

    public WorkspacePanelDescriptor GetPanelById(string panelId) => WorkspacePanels.First(panel => panel.PanelId == panelId);

    public void UpdatePanelWindowBounds(string panelId, double left, double top, double width, double height)
    {
        var panel = GetPanelById(panelId);
        panel.WindowLeft = left;
        panel.WindowTop = top;
        panel.WindowWidth = width;
        panel.WindowHeight = height;
        SaveWorkspaceLayout();
    }

    private void InitializeWorkspacePanels()
    {
        WorkspacePanels.Add(new WorkspacePanelDescriptor("CharacterEditor", "Редактор персонажа", canDetach: true));
        WorkspacePanels.Add(new WorkspacePanelDescriptor("NotesManagement", "Заметки мастера", canDetach: true));
        WorkspacePanels.Add(new WorkspacePanelDescriptor("Requests", "Заявки", canDetach: true));
        WorkspacePanels.Add(new WorkspacePanelDescriptor("DiceFeed", "Лента бросков", canDetach: true));
        WorkspacePanels.Add(new WorkspacePanelDescriptor("CombatTracker", "Combat tracker", canDetach: true));
        WorkspacePanels.Add(new WorkspacePanelDescriptor("SessionChat", "Чат сессии", canDetach: true));
        WorkspacePanels.Add(new WorkspacePanelDescriptor("SessionAudio", "Музыка сессии", canDetach: true));
    }

    private void SelectSection(string? section)
    {
        if (!string.IsNullOrWhiteSpace(section))
        {
            SelectedSection = section;
        }
    }

    private void ToggleWorkspacePanelVisibility(string? panelId)
    {
        if (string.IsNullOrWhiteSpace(panelId)) return;
        var panel = GetPanelById(panelId);
        if (panel.IsVisible)
        {
            HideWorkspacePanel(panelId);
        }
        else
        {
            ShowWorkspacePanel(panelId);
        }
    }

    private void ShowWorkspacePanel(string? panelId)
    {
        if (string.IsNullOrWhiteSpace(panelId)) return;
        var panel = GetPanelById(panelId);
        panel.IsVisible = true;
        SaveWorkspaceLayout();
        RefreshConnectionSummary();
    }

    private void HideWorkspacePanel(string? panelId)
    {
        if (string.IsNullOrWhiteSpace(panelId)) return;
        var panel = GetPanelById(panelId);
        panel.IsDetached = false;
        panel.IsVisible = false;
        SaveWorkspaceLayout();
        RefreshConnectionSummary();
    }

    private void DetachWorkspacePanel(string? panelId)
    {
        if (string.IsNullOrWhiteSpace(panelId)) return;
        var panel = GetPanelById(panelId);
        if (!panel.CanDetach) return;
        panel.IsVisible = true;
        panel.IsDetached = true;
        SaveWorkspaceLayout();
        RefreshConnectionSummary();
    }

    private void AttachWorkspacePanel(string? panelId)
    {
        if (string.IsNullOrWhiteSpace(panelId)) return;
        var panel = GetPanelById(panelId);
        panel.IsDetached = false;
        panel.IsVisible = true;
        SaveWorkspaceLayout();
        RefreshConnectionSummary();
    }

    private void Login()
    {
        try
        {
            ConnectToServer();
            var r = _api.Login(LoginText, PasswordText);
            if (r.Status == ResponseStatus.Ok)
            {
                IsAuthenticated = true;
                SetConnectedState($"Авторизация успешна: {CurrentEndpoint}");
                _poller.Start();
                RefreshAll();
                Notify(nameof(LoginSummary));
            }
            else
            {
                IsAuthenticated = false;
                LastErrorMessage = r.Message;
                IsConnectedToServer = true;
                IsOnline = true;
                ConnectionState = "Подключено";
                ConnectionStatusDetail = string.IsNullOrWhiteSpace(r.Message) ? "Логин не выполнен." : r.Message;
                LastStatusMessage = "Логин не выполнен.";
                RefreshConnectionSummary();
            }
        }
        catch (Exception ex)
        {
            SetConnectionError($"Ошибка входа: {ex.Message}");
        }
    }

    private void RefreshAll()
    {
        if (!IsConnectedToServer)
        {
            SetDisconnectedState("Нет активного подключения. Сначала подключитесь к серверу.");
            return;
        }

        try
        {
            LoadPending();
            LoadPlayers();
            LoadPendingRequests();
            LoadRequestHistory();
            CombatRefresh();
            if (!string.IsNullOrWhiteSpace(SelectedCharacterId))
            {
                LoadClassTree();
                LoadSkills();
            }
            ChatRefresh();
            AudioRefresh();
            NotesRefresh();
            ReferenceRefresh();
            BackupRefresh();
            DiagnosticsRefresh();
            LoadLocksSummary();
            SetConnectedState($"Данные обновлены: {CurrentEndpoint}");
        }
        catch (Exception ex)
        {
            SetConnectionError($"Ошибка обновления: {ex.Message}");
        }
    }

    private void LoadPending()
    {
        PendingAccounts.Clear();
        var r = _api.GetPendingAccounts();
        if (r.Status != ResponseStatus.Ok || !r.Payload.ContainsKey("items")) return;
        foreach (var obj in ToList(r.Payload["items"]))
        {
            if (obj is not Dictionary<string, object> m) continue;
            PendingAccounts.Add(new RowVm { Id = S(m, "accountId"), Name = S(m, "login"), State = S(m, "status"), Extra = S(m, "createdUtc") });
        }
        RefreshConnectionSummary();
    }

    private void LoadPlayers()
    {
        Players.Clear();
        var r = _api.GetPlayers();
        if (r.Status != ResponseStatus.Ok || !r.Payload.ContainsKey("items")) return;
        foreach (var obj in ToList(r.Payload["items"]))
        {
            if (obj is not Dictionary<string, object> m) continue;
            Players.Add(new RowVm { Id = S(m, "accountId"), Name = S(m, "login"), State = S(m, "status"), Extra = $"online={S(m, "isOnline")}; last={S(m, "lastSeenUtc")}" });
        }
        RefreshConnectionSummary();
    }

    private void LoadOwnerCharacters()
    {
        if (string.IsNullOrWhiteSpace(SelectedOwnerUserId)) return;
        Characters.Clear();
        var r = _api.GetCharactersByOwner(SelectedOwnerUserId);
        if (r.Status != ResponseStatus.Ok || !r.Payload.ContainsKey("items")) return;
        foreach (var obj in ToList(r.Payload["items"]))
        {
            if (obj is not Dictionary<string, object> m) continue;
            Characters.Add(new RowVm { Id = S(m, "characterId"), Name = S(m, "name"), State = S(m, "archived"), Extra = S(m, "race") });
        }
        RefreshConnectionSummary();
    }

    private void OpenCharacter()
    {
        if (string.IsNullOrWhiteSpace(SelectedCharacterId)) return;
        var r = _api.GetCharacterDetails(SelectedCharacterId);
        if (r.Status != ResponseStatus.Ok) return;

        EditName = S(r.Payload, "name");
        EditRace = S(r.Payload, "race");
        EditHeight = S(r.Payload, "height");
        EditDescription = S(r.Payload, "description");
        EditBackstory = S(r.Payload, "backstory");
        int.TryParse(S(r.Payload, "age"), out var age); EditAge = age;

        if (r.Payload.ContainsKey("stats") && r.Payload["stats"] is Dictionary<string, object> stats)
        {
            int.TryParse(S(stats, "health"), out var v); Health = v;
            int.TryParse(S(stats, "physicalArmor"), out v); PhysicalArmor = v;
            int.TryParse(S(stats, "magicalArmor"), out v); MagicalArmor = v;
            int.TryParse(S(stats, "morale"), out v); Morale = v;
            int.TryParse(S(stats, "strength"), out v); Strength = v;
            int.TryParse(S(stats, "dexterity"), out v); Dexterity = v;
            int.TryParse(S(stats, "endurance"), out v); Endurance = v;
            int.TryParse(S(stats, "wisdom"), out v); Wisdom = v;
            int.TryParse(S(stats, "intellect"), out v); Intellect = v;
            int.TryParse(S(stats, "charisma"), out v); Charisma = v;
        }

        if (r.Payload.ContainsKey("money") && r.Payload["money"] is Dictionary<string, object> money)
        {
            long.TryParse(S(money, "Iron"), out var l); Iron = l;
            long.TryParse(S(money, "Bronze"), out l); Bronze = l;
            long.TryParse(S(money, "Silver"), out l); Silver = l;
            long.TryParse(S(money, "Gold"), out l); Gold = l;
        }

        InventoryRows.Clear();
        foreach (var item in ToList(r.Payload.ContainsKey("inventory") ? r.Payload["inventory"] : new ArrayList()))
            if (item is Dictionary<string, object> m)
                InventoryRows.Add($"{S(m, "label")} x{S(m, "quantity")}");

        HoldingsRows.Clear();
        foreach (var item in ToList(r.Payload.ContainsKey("holdings") ? r.Payload["holdings"] : new ArrayList()))
            if (item is Dictionary<string, object> m)
                HoldingsRows.Add($"{S(m, "name")} - {S(m, "description")}");

        ReputationRows.Clear();
        foreach (var item in ToList(r.Payload.ContainsKey("reputation") ? r.Payload["reputation"] : new ArrayList()))
            if (item is Dictionary<string, object> m)
                ReputationRows.Add($"{S(m, "scope")}:{S(m, "groupKey") }={S(m, "value")}");

        CompanionRows.Clear();
        foreach (var item in ToList(r.Payload.ContainsKey("companions") ? r.Payload["companions"] : new ArrayList()))
            if (item is Dictionary<string, object> m)
                CompanionRows.Add($"{S(m, "name")} ({S(m, "species")})");

        NotifyAllEditor();
    }

    private void LoadPendingRequests()
    {
        PendingRequests.Clear();
        var r = _api.ListPendingRequests();
        if (r.Status != ResponseStatus.Ok || !r.Payload.ContainsKey("items")) return;
        foreach (var obj in ToList(r.Payload["items"]))
        {
            if (obj is not Dictionary<string, object> m) continue;
            PendingRequests.Add(new RowVm { Id = S(m, "requestId"), Name = S(m, "requestType"), State = S(m, "status"), Extra = S(m, "formula") });
        }
        RefreshConnectionSummary();
    }

    private void LoadRequestHistory()
    {
        RequestHistoryRows.Clear();
        var r = _api.RequestHistory();
        if (r.Status == ResponseStatus.Ok && r.Payload.ContainsKey("items"))
        {
            foreach (var obj in ToList(r.Payload["items"]))
                if (obj is Dictionary<string, object> m)
                    RequestHistoryRows.Add($"{S(m, "requestId")} | {S(m, "status")} | {S(m, "requestType")} | {S(m, "formula")}");
        }

        DiceFeedRows.Clear();
        var feed = _api.DiceVisibleFeed();
        if (feed.Status == ResponseStatus.Ok && feed.Payload.ContainsKey("items"))
        {
            foreach (var obj in ToList(feed.Payload["items"]))
            {
                if (obj is not Dictionary<string, object> m) continue;
                var total = string.Empty;
                if (m.ContainsKey("result") && m["result"] is Dictionary<string, object> result) total = S(result, "total");
                DiceFeedRows.Add($"{S(m, "creatorUserId")} | {S(m, "formula")} | {total} | {S(m, "visibility")}");
            }
        }
        RefreshConnectionSummary();
    }

    private void CombatStart()
    {
        var participants = new[] { new Dictionary<string, object> { { "kind", "Npc" }, { "entityId", "npc-1" }, { "displayName", "NPC-1" }, { "ownerUserId", "" } } };
        _api.CombatStart(CombatSessionId, participants);
        CombatRefresh();
    }

    private void CombatEnd() { _api.CombatEnd(CombatSessionId); CombatRefresh(); }
    private void CombatNextTurn() { _api.CombatNextTurn(CombatSessionId); CombatRefresh(); }
    private void CombatPrevTurn() { _api.CombatPreviousTurn(CombatSessionId); CombatRefresh(); }
    private void CombatNextRound() { _api.CombatNextRound(CombatSessionId); CombatRefresh(); }
    private void CombatSkipTurn() { _api.CombatSkipTurn(CombatSessionId); CombatRefresh(); }

    private void CombatAddParticipant()
    {
        var participants = new[] { new Dictionary<string, object> { { "kind", NewParticipantKind }, { "entityId", Guid.NewGuid().ToString("N") }, { "displayName", NewParticipantName }, { "ownerUserId", "" } } };
        _api.CombatAddParticipant(CombatSessionId, participants);
        CombatRefresh();
    }

    private void CombatRemoveParticipant()
    {
        if (string.IsNullOrWhiteSpace(SelectedCombatParticipantId)) return;
        _api.CombatRemoveParticipant(CombatSessionId, SelectedCombatParticipantId);
        CombatRefresh();
    }

    private void CombatDetachCompanion()
    {
        if (string.IsNullOrWhiteSpace(SelectedCombatParticipantId)) return;
        _api.CombatDetachCompanion(CombatSessionId, SelectedCombatParticipantId);
        CombatRefresh();
    }

    private void CombatRefresh()
    {
        CombatRows.Clear();
        var state = _api.CombatGetState(CombatSessionId);
        if (state.Status == ResponseStatus.Ok)
        {
            CombatRows.Add($"Status: {S(state.Payload, "status")}");
            CombatRows.Add($"Round: {S(state.Payload, "round")}");
            CombatRows.Add($"TurnIndex: {S(state.Payload, "turnIndex")}");
            CombatRows.Add($"ActiveSlot: {S(state.Payload, "activeSlotId")}");
            foreach (var item in ToList(state.Payload.ContainsKey("participants") ? state.Payload["participants"] : new ArrayList()))
            {
                if (item is Dictionary<string, object> m)
                    CombatRows.Add($"P:{S(m, "participantId")} {S(m, "displayName")} {S(m, "kind")} roll={S(m, "baseRoll")} st={S(m, "status")}");
            }
        }

        CombatHistoryRows.Clear();
        var history = _api.CombatGetHistory(CombatSessionId);
        if (history.Status == ResponseStatus.Ok && history.Payload.ContainsKey("items"))
        {
            foreach (var item in ToList(history.Payload["items"]))
            {
                if (item is Dictionary<string, object> m)
                    CombatHistoryRows.Add($"{S(m, "at")} | {S(m, "eventType")} | {S(m, "message")}");
            }
        }
        RefreshConnectionSummary();
    }

    private void DefinitionsReload()
    {
        var r = _api.DefinitionsReload();
        DefinitionVersionText = S(r.Payload, "version");
        Notify(nameof(DefinitionVersionText));
    }

    private void LoadClassTree()
    {
        if (string.IsNullOrWhiteSpace(SelectedCharacterId)) return;
        ClassTreeRows.Clear();
        var tree = _api.ClassTreeGet(SelectedCharacterId);
        if (tree.Status == ResponseStatus.Ok)
        {
            DefinitionVersionText = S(tree.Payload, "definitionVersion");
            foreach (var d in ToList(tree.Payload.ContainsKey("directions") ? tree.Payload["directions"] : new ArrayList()))
            {
                if (d is not Dictionary<string, object> dm) continue;
                ClassTreeRows.Add($"[{S(dm, "directionId")}] branch={S(dm, "selectedBranchId")}");
                foreach (var n in ToList(dm.ContainsKey("acquiredNodes") ? dm["acquiredNodes"] : new ArrayList()))
                    if (n is Dictionary<string, object> nm)
                        ClassTreeRows.Add($"  + {S(nm, "nodeId")} at {S(nm, "acquiredAt")}");
            }
        }

        var available = _api.ClassTreeAvailable(SelectedCharacterId);
        if (available.Status == ResponseStatus.Ok && available.Payload.ContainsKey("items"))
        {
            foreach (var d in ToList(available.Payload["items"]))
            {
                if (d is not Dictionary<string, object> dm) continue;
                if (S(dm, "available") == "True")
                    ClassTreeRows.Add($"AVAILABLE {S(dm, "nodeId")} ({S(dm, "name")})");
            }
        }
        Notify(nameof(DefinitionVersionText));
    }

    private void AcquireClassNode()
    {
        if (string.IsNullOrWhiteSpace(SelectedCharacterId) || string.IsNullOrWhiteSpace(SelectedClassNodeId)) return;
        _api.ClassTreeAcquireNode(SelectedCharacterId, SelectedClassNodeId);
        LoadClassTree();
        LoadSkills();
    }

    private void LoadSkills()
    {
        if (string.IsNullOrWhiteSpace(SelectedCharacterId)) return;
        SkillStateRows.Clear();
        var r = _api.SkillsList(SelectedCharacterId);
        if (r.Status != ResponseStatus.Ok || !r.Payload.ContainsKey("items")) return;
        foreach (var item in ToList(r.Payload["items"]))
        {
            if (item is not Dictionary<string, object> m) continue;
            SkillStateRows.Add($"{S(m, "skillId")} | {S(m, "name")} | type={S(m, "type")} | acquired={S(m, "acquired")} | available={S(m, "available")} | reason={S(m, "reason")}");
        }
    }

    private void AcquireSkill()
    {
        if (string.IsNullOrWhiteSpace(SelectedCharacterId) || string.IsNullOrWhiteSpace(SelectedSkillId)) return;
        _api.SkillsAcquire(SelectedCharacterId, SelectedSkillId);
        LoadSkills();
    }

    private void ChatSend()
    {
        if (string.IsNullOrWhiteSpace(ChatMessageText)) return;
        _api.ChatSend(ChatSessionId, ChatMessageType, ChatMessageText);
        ChatMessageText = string.Empty;
        Notify(nameof(ChatMessageText));
        ChatRefresh();
    }

    private void ChatRefresh()
    {
        ChatRows.Clear();
        var history = _api.ChatHistoryGet(ChatSessionId, 80);
        if (history.Status == ResponseStatus.Ok && history.Payload.ContainsKey("items"))
        {
            foreach (var item in ToList(history.Payload["items"]))
            {
                if (item is not Dictionary<string, object> m) continue;
                ChatRows.Add($"{S(m, "createdUtc")} | {S(m, "type")} | {S(m, "senderDisplayName")}: {S(m, "text")}");
            }
        }

        var unread = _api.ChatUnreadGet(ChatSessionId);
        ChatUnreadText = "Unread: " + S(unread.Payload, "count");
        Notify(nameof(ChatUnreadText));

        var slow = _api.ChatSlowModeGet(ChatSessionId);
        ChatSlowPublicSeconds = int.TryParse(S(slow.Payload, "publicSeconds"), out var ps) ? ps : 0;
        ChatSlowHiddenSeconds = int.TryParse(S(slow.Payload, "hiddenToAdminsSeconds"), out var hs) ? hs : 0;
        ChatSlowAdminOnlySeconds = int.TryParse(S(slow.Payload, "adminOnlySeconds"), out var a) ? a : 0;
        Notify(nameof(ChatSlowPublicSeconds)); Notify(nameof(ChatSlowHiddenSeconds)); Notify(nameof(ChatSlowAdminOnlySeconds));

        ChatRestrictionRows.Clear();
        var restrictions = _api.ChatRestrictionsGet(ChatSessionId);
        ChatRestrictionRows.Add("LockPlayers=" + S(restrictions.Payload, "lockPlayers"));
        foreach (var item in ToList(restrictions.Payload.ContainsKey("restrictions") ? restrictions.Payload["restrictions"] : new ArrayList()))
            if (item is Dictionary<string, object> m)
                ChatRestrictionRows.Add($"{S(m, "userId")} muted={S(m, "muted")} reason={S(m, "reason")}");
        RefreshConnectionSummary();
    }

    private void ChatMuteUser() { if (!string.IsNullOrWhiteSpace(ChatModerationUserId)) { _api.ChatRestrictionsMuteUser(ChatSessionId, ChatModerationUserId, ChatModerationReason); ChatRefresh(); } }
    private void ChatUnmuteUser() { if (!string.IsNullOrWhiteSpace(ChatModerationUserId)) { _api.ChatRestrictionsUnmuteUser(ChatSessionId, ChatModerationUserId); ChatRefresh(); } }
    private void ChatLockPlayers() { _api.ChatRestrictionsLockPlayers(ChatSessionId); ChatRefresh(); }
    private void ChatUnlockPlayers() { _api.ChatRestrictionsUnlockPlayers(ChatSessionId); ChatRefresh(); }

    private void ChatSetSlowMode()
    {
        _api.ChatSlowModeSet(ChatSessionId, ChatSlowPublicSeconds, ChatSlowHiddenSeconds, ChatSlowAdminOnlySeconds);
        ChatRefresh();
    }

    private void AudioRefresh()
    {
        var state = _api.AudioStateGet(AudioSessionId);
        AudioStateText = $"mode={S(state.Payload, "mode")}; category={S(state.Payload, "category")}; track={S(state.Payload, "trackName")}; pos={S(state.Payload, "positionSeconds")}; override={S(state.Payload, "overrideEnabled")}; playback={S(state.Payload, "playbackState")}";
        Notify(nameof(AudioStateText));

        AudioLibraryRows.Clear();
        var lib = _api.AudioLibraryGet();
        if (lib.Status == ResponseStatus.Ok && lib.Payload.ContainsKey("items"))
        {
            foreach (var item in ToList(lib.Payload["items"]))
                if (item is Dictionary<string, object> m)
                    AudioLibraryRows.Add($"{S(m, "trackId")} | {S(m, "category")} | {S(m, "displayName")} | {S(m, "filePath")}");
        }
        RefreshConnectionSummary();
    }

    private void AudioSetMode() { _api.AudioModeSet(AudioSessionId, AudioModeInput, AudioCategoryInput); AudioRefresh(); }
    private void AudioClearOverride() { _api.AudioOverrideClear(AudioSessionId); AudioRefresh(); }
    private void AudioNextTrack() { _api.AudioTrackNext(AudioSessionId); AudioRefresh(); }
    private void AudioSelectTrack() { if (!string.IsNullOrWhiteSpace(AudioSelectedTrackId)) { _api.AudioTrackSelect(AudioSessionId, AudioSelectedTrackId); AudioRefresh(); } }
    private void AudioReloadLibrary() { _api.AudioTrackReload(); AudioRefresh(); }

    private void VisibilityLoad()
    {
        if (string.IsNullOrWhiteSpace(SelectedCharacterId)) return;
        var r = _api.VisibilityGet(SelectedCharacterId);
        VisHideDescription = S(r.Payload, "hideDescriptionForOthers") == "True";
        VisHideBackstory = S(r.Payload, "hideBackstoryForOthers") == "True";
        VisHideStats = S(r.Payload, "hideStatsForOthers") == "True";
        VisHideReputation = S(r.Payload, "hideReputationForOthers") == "True";
        Notify(nameof(VisHideDescription)); Notify(nameof(VisHideBackstory)); Notify(nameof(VisHideStats)); Notify(nameof(VisHideReputation));
    }

    private void VisibilitySave()
    {
        if (string.IsNullOrWhiteSpace(SelectedCharacterId)) return;
        _api.VisibilityUpdate(new Dictionary<string, object> { { "characterId", SelectedCharacterId }, { "hideDescriptionForOthers", VisHideDescription }, { "hideBackstoryForOthers", VisHideBackstory }, { "hideStatsForOthers", VisHideStats }, { "hideReputationForOthers", VisHideReputation } });
        VisibilityLoad();
    }

    private void NotesRefresh()
    {
        NotesRows.Clear();
        var r = _api.NotesList(new Dictionary<string, object> { { "sessionId", NoteSessionId }, { "targetType", NoteTargetType }, { "targetId", NoteTargetId } });
        foreach (var item in ToList(r.Payload.ContainsKey("items") ? r.Payload["items"] : new ArrayList()))
            if (item is Dictionary<string, object> m)
                NotesRows.Add($"{S(m, "noteId")} | {S(m, "visibility")} | {S(m, "title")} | {S(m, "text")}");
    }

    private void NotesCreate()
    {
        _api.NotesCreate(new Dictionary<string, object> { { "sessionId", NoteSessionId }, { "targetType", NoteTargetType }, { "targetId", NoteTargetId }, { "title", NoteTitle }, { "text", NoteText }, { "visibility", NoteVisibility }, { "noteType", "Session" } });
        NotesRefresh();
    }

    private void NotesArchive() { if (!string.IsNullOrWhiteSpace(SelectedNoteId)) { _api.NotesArchive(SelectedNoteId); NotesRefresh(); } }

    private void ReferenceRefresh()
    {
        ReferenceRows.Clear();
        var r = _api.ReferenceList(ReferenceWorldId, ReferenceType);
        foreach (var item in ToList(r.Payload.ContainsKey("items") ? r.Payload["items"] : new ArrayList()))
            if (item is Dictionary<string, object> m)
                ReferenceRows.Add($"{S(m, "referenceId")} | {S(m, "referenceType")} | {S(m, "key")} | {S(m, "displayName")}");
    }

    private void ReferenceCreate() { _api.ReferenceCreate(new Dictionary<string, object> { { "worldId", ReferenceWorldId }, { "referenceType", ReferenceType }, { "key", ReferenceKey }, { "displayName", ReferenceDisplayName }, { "dataJson", ReferenceDataJson } }); ReferenceRefresh(); }
    private void ReferenceUpdate() { if (!string.IsNullOrWhiteSpace(ReferenceId)) { _api.ReferenceUpdate(new Dictionary<string, object> { { "referenceId", ReferenceId }, { "displayName", ReferenceDisplayName }, { "dataJson", ReferenceDataJson } }); ReferenceRefresh(); } }
    private void ReferenceArchive() { if (!string.IsNullOrWhiteSpace(ReferenceId)) { _api.ReferenceArchive(ReferenceId); ReferenceRefresh(); } }

    private void BackupRefresh()
    {
        BackupRows.Clear();
        var r = _api.BackupList();
        foreach (var item in ToList(r.Payload.ContainsKey("items") ? r.Payload["items"] : new ArrayList()))
            if (item is Dictionary<string, object> m)
                BackupRows.Add($"{S(m, "backupId")} | {S(m, "label")} | {S(m, "createdUtc")}");
    }

    private void BackupCreate() { _api.BackupCreate(string.IsNullOrWhiteSpace(BackupLabel) ? "manual-backup" : BackupLabel); BackupRefresh(); }
    private void BackupRestore() { if (!string.IsNullOrWhiteSpace(SelectedBackupId)) { _api.BackupRestore(SelectedBackupId); BackupRefresh(); } }
    private void BackupExport() { if (!string.IsNullOrWhiteSpace(SelectedBackupId)) { _api.BackupExport(SelectedBackupId); } }

    private void DiagnosticsRefresh()
    {
        DiagnosticsRows.Clear();
        var s1 = _api.AdminServerStatus();
        DiagnosticsRows.Add("server utc=" + S(s1.Payload, "utcNow") + "; online=" + S(s1.Payload, "onlineUsers"));
        var s2 = _api.AdminSessionsList();
        DiagnosticsRows.Add("sessions payload size=" + ToList(s2.Payload.ContainsKey("items") ? s2.Payload["items"] : new ArrayList()).Count);
        var s3 = _api.AdminLocksList();
        LocksCount = ToList(s3.Payload.ContainsKey("items") ? s3.Payload["items"] : new ArrayList()).Count;
        DiagnosticsRows.Add("locks=" + LocksCount);
        RefreshConnectionSummary();
    }

    private void LoadLocksSummary()
    {
        if (DiagnosticsRows.Count == 0)
        {
            DiagnosticsRefresh();
        }
    }

    private void ApproveRequest() { if (!string.IsNullOrWhiteSpace(SelectedPendingRequestId)) { _api.ApproveRequest(SelectedPendingRequestId, RequestComment); RefreshAll(); } }
    private void RejectRequest() { if (!string.IsNullOrWhiteSpace(SelectedPendingRequestId)) { _api.RejectRequest(SelectedPendingRequestId, RequestComment); RefreshAll(); } }
    private void AcquireLock() { var r = _api.AcquireCharacterLock(SelectedCharacterId); LockStateText = r.Message; Notify(nameof(LockStateText)); }
    private void ReleaseLock() { var r = _api.ReleaseCharacterLock(SelectedCharacterId); LockStateText = r.Message; Notify(nameof(LockStateText)); }
    private void ForceUnlock() { var r = _api.ForceReleaseCharacterLock(SelectedCharacterId); LockStateText = r.Message; Notify(nameof(LockStateText)); }
    private void SaveBasicInfo() { _api.UpdateCharacterBasicInfo(new Dictionary<string, object> { { "characterId", SelectedCharacterId }, { "name", EditName }, { "race", EditRace }, { "height", EditHeight }, { "age", EditAge }, { "description", EditDescription }, { "backstory", EditBackstory } }); }
    private void SaveStats() { _api.UpdateCharacterStats(new Dictionary<string, object> { { "characterId", SelectedCharacterId }, { "health", Health }, { "physicalArmor", PhysicalArmor }, { "magicalArmor", MagicalArmor }, { "morale", Morale }, { "strength", Strength }, { "dexterity", Dexterity }, { "endurance", Endurance }, { "wisdom", Wisdom }, { "intellect", Intellect }, { "charisma", Charisma } }); }
    private void SaveMoney() { _api.UpdateCharacterMoney(new Dictionary<string, object> { { "characterId", SelectedCharacterId }, { "money", new Dictionary<string, object> { { "Iron", Iron }, { "Bronze", Bronze }, { "Silver", Silver }, { "Gold", Gold } } } }); }
    private void ApproveSelected() { if (!string.IsNullOrWhiteSpace(SelectedPendingAccountId)) _api.ApproveAccount(SelectedPendingAccountId); RefreshAll(); }
    private void ArchiveSelected() { if (!string.IsNullOrWhiteSpace(SelectedPendingAccountId)) _api.ArchiveAccount(SelectedPendingAccountId); RefreshAll(); }

    private void RefreshOverviewActivity()
    {
        OverviewActivityRows.Clear();
        OverviewActivityRows.Add(HasConnectionError ? $"Ошибка: {LastErrorMessage}" : LastStatusMessage);
        if (PendingRequests.Count > 0) OverviewActivityRows.Add($"Требуют решения: {PendingRequests[0].Name} / {PendingRequests[0].State}");
        if (PendingAccounts.Count > 0) OverviewActivityRows.Add($"Новый аккаунт: {PendingAccounts[0].Name}");
        if (DiceFeedRows.Count > 0) OverviewActivityRows.Add($"Последний бросок: {DiceFeedRows[0]}");
        if (ChatRows.Count > 0) OverviewActivityRows.Add($"Последнее сообщение: {ChatRows[0]}");
        if (DiagnosticsRows.Count > 0) OverviewActivityRows.Add($"Диагностика: {DiagnosticsRows[0]}");
        if (OverviewActivityRows.Count == 1 && string.IsNullOrWhiteSpace(OverviewActivityRows[0]))
        {
            OverviewActivityRows[0] = "Нет последних событий.";
        }
    }

    public void Shutdown()
    {
        SaveConnectionSettings();
        SaveWorkspaceLayout();
        _client.Disconnect();
    }

    private void NotifyAllEditor()
    {
        Notify(nameof(EditName)); Notify(nameof(EditRace)); Notify(nameof(EditHeight)); Notify(nameof(EditAge)); Notify(nameof(EditDescription)); Notify(nameof(EditBackstory));
        Notify(nameof(Health)); Notify(nameof(PhysicalArmor)); Notify(nameof(MagicalArmor)); Notify(nameof(Morale)); Notify(nameof(Strength)); Notify(nameof(Dexterity)); Notify(nameof(Endurance)); Notify(nameof(Wisdom)); Notify(nameof(Intellect)); Notify(nameof(Charisma));
        Notify(nameof(Iron)); Notify(nameof(Bronze)); Notify(nameof(Silver)); Notify(nameof(Gold));
    }

    private static T ReadJson<T>(string path, T fallback) where T : class
    {
        try
        {
            if (!File.Exists(path)) return fallback;
            using var stream = File.OpenRead(path);
            var serializer = new DataContractJsonSerializer(typeof(T));
            return serializer.ReadObject(stream) as T ?? fallback;
        }
        catch
        {
            return fallback;
        }
    }

    private static void WriteJson<T>(string path, T value) where T : class
    {
        using var stream = File.Create(path);
        var serializer = new DataContractJsonSerializer(typeof(T));
        serializer.WriteObject(stream, value);
    }

    private static IList ToList(object value) => value as IList ?? new ArrayList();
    private static string S(Dictionary<string, object> map, string key) => map.ContainsKey(key) && map[key] != null ? Convert.ToString(map[key]) ?? string.Empty : string.Empty;
}

public class CombatTrackerViewModel : AdminMainViewModel { }
