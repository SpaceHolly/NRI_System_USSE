using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Input;
using System.Windows.Threading;
using Nri.AdminClient.Diagnostics;
using Nri.AdminClient.Networking;
using Nri.Shared.Configuration;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;

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

public sealed class ChatMessageRowVm : ViewModelBase
{
    public string Sender { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string Timestamp { get; set; } = string.Empty;
    public bool IsSystem { get; set; }
}

public sealed class SkillLevelEditorRowVm : ViewModelBase
{
    private int _level;
    private string _description = string.Empty;

    public int Level
    {
        get => _level;
        set { if (_level != value) { _level = value; Notify(); } }
    }

    public string Description
    {
        get => _description;
        set { if (_description != value) { _description = value; Notify(); } }
    }
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
    [DataMember(Order = 2)] public int ServerPort { get; set; } = 4600;
    [DataMember(Order = 3)] public string LastServerHost { get; set; } = "127.0.0.1";
    [DataMember(Order = 4)] public int LastServerPort { get; set; } = 4600;
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
    private string _serverPortInput = "4600";
    private string _lastServerHost = "127.0.0.1";
    private int _lastServerPort = 4600;
    private bool _isConnectionPopupOpen;
    private bool _isAuthPopupOpen;
    private bool _isOnline;
    private bool _isConnectedToServer;
    private bool _isAuthenticated;
    private string _lastErrorMessage = string.Empty;
    private string _lastStatusMessage = "Ожидание подключения";
    private int _locksCount;
    private bool _isBusy;
    private string _busyMessage = string.Empty;
    private string _selectedSection = "Обзор";
    private string _selectedCharacterWorkspaceTab = "Editor";
    private string _selectedLockId = string.Empty;
    private string _selectedPendingAccountId = string.Empty;
    private string _selectedOwnerUserId = string.Empty;
    private string _selectedCharacterId = string.Empty;
    private string _selectedPendingRequestId = string.Empty;
    private string _selectedCombatParticipantId = string.Empty;
    private string _selectedClassNodeId = string.Empty;
    private string _selectedSkillId = string.Empty;
    private string _selectedReferenceId = string.Empty;
    private string _selectedClassDefinitionCode = string.Empty;
    private string _selectedSkillDefinitionCode = string.Empty;
    private string _selectedBackupId = string.Empty;
    private string _selectedDiagnosticsId = string.Empty;
    private int _selectedContentTabIndex;
    private int _selectedSystemTabIndex;
    private string _charactersSearchText = string.Empty;
    private string _locksSearchText = string.Empty;
    private string _classSearchText = string.Empty;
    private string _skillSearchText = string.Empty;
    private int _diceCount = 1;
    private int _diceFaces = 20;
    private int _diceModifier;
    private string _diceModeInput = "Обычный";
    private string _diceVisibilityInput = "Public";
    private string _diceDescriptionInput = "Admin quick roll";
    private string _lastDiceAvailabilityReason = string.Empty;

    public AdminMainViewModel()
    {
        Directory.CreateDirectory(_appDataDirectory);

        _client = new JsonTcpClient(App.ClientConfig, _session);
        ClientLogService.Instance.Info("AdminMainViewModel initialized");
        _api = new CommandApi(_client);

        LoginCommand = new RelayCommand(() => RunUiAction("Авторизация", Login));
        RefreshCommand = new RelayCommand(() => RunUiAction("Полное обновление данных", RefreshAll));
        OpenConnectionPopupCommand = new RelayCommand(() =>
        {
            IsAuthPopupOpen = false;
            IsConnectionPopupOpen = !IsConnectionPopupOpen;
        });
        ToggleAuthPopupCommand = new RelayCommand(() =>
        {
            IsConnectionPopupOpen = false;
            IsAuthPopupOpen = !IsAuthPopupOpen;
        });
        ConnectToServerCommand = new RelayCommand(() => RunUiAction("Подключение к серверу", ConnectToServer));
        ApplyConnectionSettingsCommand = new RelayCommand(ApplyConnectionSettings);
        ResetConnectionDefaultsCommand = new RelayCommand(ResetConnectionDefaults);
        UseSavedConnectionSettingsCommand = new RelayCommand(UseSavedConnectionSettings);
        ApproveCommand = new RelayCommand(ApproveSelected);
        ArchiveCommand = new RelayCommand(ArchiveSelected);
        RejectAccountCommand = new RelayCommand(RejectSelectedAccount);
        BlockAccountCommand = new RelayCommand(BlockSelectedAccount);
        UnblockAccountCommand = new RelayCommand(UnblockSelectedAccount);
        ChangePasswordCommand = new RelayCommand(ChangePassword);
        ResetPasswordCommand = new RelayCommand(ResetSelectedPassword);
        CreateCharacterCommand = new RelayCommand(CreateCharacterForOwner);
        RollDiceCommand = new RelayCommand(RollCharacterDice);
        LoadOwnerCharactersCommand = new RelayCommand(LoadOwnerCharacters);
        OpenCharacterCommand = new RelayCommand(OpenCharacter);
        OpenPlayerCharactersCommand = new RelayCommand(OpenPlayerCharacters);
        FocusSelectedCharacterCommand = new RelayCommand(FocusSelectedCharacter);
        FocusSelectedRequestCommand = new RelayCommand(FocusSelectedRequest);
        FocusCharacterEditorCommand = new RelayCommand(FocusCharacterEditor);
        FocusCharacterNotesCommand = new RelayCommand(FocusCharacterNotes);
        FocusCharacterVisibilityCommand = new RelayCommand(FocusCharacterVisibility);
        RefreshSelectedCharacterCommand = new RelayCommand(RefreshSelectedCharacter);
        RefreshPeopleSectionCommand = new RelayCommand(RefreshPeopleSection);
        RefreshModerationSectionCommand = new RelayCommand(RefreshModerationSection);
        RefreshSessionSectionCommand = new RelayCommand(RefreshSessionSection);
        RefreshContentSectionCommand = new RelayCommand(RefreshContentSection);
        RefreshSystemSectionCommand = new RelayCommand(RefreshSystemSection);
        AcquireLockCommand = new RelayCommand(AcquireLock);
        ReleaseLockCommand = new RelayCommand(ReleaseLock);
        ForceUnlockCommand = new RelayCommand(ForceUnlock);
        SaveBasicInfoCommand = new RelayCommand(SaveBasicInfo);
        SaveStatsCommand = new RelayCommand(SaveStats);
        SaveMoneyCommand = new RelayCommand(SaveMoney);
        SaveXpCoinsCommand = new RelayCommand(SaveXpCoins);
        ApproveRequestCommand = new RelayCommand(ApproveRequest);
        RejectRequestCommand = new RelayCommand(RejectRequest);
        CombatStartCommand = new RelayCommand(() => RunUiAction("Запуск боя", CombatStart));
        CombatEndCommand = new RelayCommand(() => RunUiAction("Завершение боя", CombatEnd));
        CombatRefreshCommand = new RelayCommand(() => RunUiAction("Обновление боя", CombatRefresh));
        CombatNextTurnCommand = new RelayCommand(() => RunUiAction("Переход к следующему ходу", CombatNextTurn));
        CombatPrevTurnCommand = new RelayCommand(() => RunUiAction("Возврат к предыдущему ходу", CombatPrevTurn));
        CombatNextRoundCommand = new RelayCommand(() => RunUiAction("Переход к следующему раунду", CombatNextRound));
        CombatSkipTurnCommand = new RelayCommand(() => RunUiAction("Пропуск хода", CombatSkipTurn));
        CombatAddParticipantCommand = new RelayCommand(() => RunUiAction("Добавление участника боя", CombatAddParticipant));
        CombatRemoveParticipantCommand = new RelayCommand(() => RunUiAction("Удаление участника боя", CombatRemoveParticipant));
        CombatDetachCompanionCommand = new RelayCommand(() => RunUiAction("Отвязка спутника", CombatDetachCompanion));
        DefinitionsReloadCommand = new RelayCommand(() => RunUiAction("Перезагрузка definitions", DefinitionsReload));
        RefreshDefinitionClassesCommand = new RelayCommand(() => RunUiAction("Загрузка definitions классов", RefreshDefinitionClasses));
        NewClassDefinitionCommand = new RelayCommand(NewClassDefinition);
        OpenSelectedClassDefinitionCommand = new RelayCommand(() => RunUiAction("Открытие definitions класса", OpenSelectedClassDefinition));
        SaveClassDefinitionCommand = new RelayCommand(() => RunUiAction("Сохранение definitions класса", SaveClassDefinition));
        ArchiveClassDefinitionCommand = new RelayCommand(() => RunUiAction("Архивация definitions класса", ArchiveClassDefinition));
        RefreshDefinitionSkillsCommand = new RelayCommand(() => RunUiAction("Загрузка definitions навыков", RefreshDefinitionSkills));
        NewSkillDefinitionCommand = new RelayCommand(NewSkillDefinition);
        OpenSelectedSkillDefinitionCommand = new RelayCommand(() => RunUiAction("Открытие definitions навыка", OpenSelectedSkillDefinition));
        SaveSkillDefinitionCommand = new RelayCommand(() => RunUiAction("Сохранение definitions навыка", SaveSkillDefinition));
        ArchiveSkillDefinitionCommand = new RelayCommand(() => RunUiAction("Архивация definitions навыка", ArchiveSkillDefinition));
        AddSkillLevelCommand = new RelayCommand(AddSkillLevel);
        RemoveSkillLevelCommand = new RelayCommand(RemoveSkillLevel);
        LoadClassTreeCommand = new RelayCommand(() => RunUiAction("Загрузка class tree", LoadClassTree));
        AcquireClassNodeCommand = new RelayCommand(() => RunUiAction("Выдача class node", AcquireClassNode));
        LoadSkillsCommand = new RelayCommand(() => RunUiAction("Загрузка навыков", LoadSkills));
        AcquireSkillCommand = new RelayCommand(() => RunUiAction("Выдача навыка", AcquireSkill));
        ChatSendCommand = new RelayCommand(() => RunUiAction("Отправка сообщения", ChatSend));
        ChatRefreshCommand = new RelayCommand(() => RunUiAction("Обновление чата", ChatRefresh));
        ChatMuteUserCommand = new RelayCommand(ChatMuteUser);
        ChatUnmuteUserCommand = new RelayCommand(ChatUnmuteUser);
        ChatLockPlayersCommand = new RelayCommand(ChatLockPlayers);
        ChatUnlockPlayersCommand = new RelayCommand(ChatUnlockPlayers);
        ChatSetSlowModeCommand = new RelayCommand(ChatSetSlowMode);
        AudioRefreshCommand = new RelayCommand(() => RunUiAction("Обновление аудио", AudioRefresh));
        AudioSetModeCommand = new RelayCommand(() => RunUiAction("Смена режима аудио", AudioSetMode));
        AudioClearOverrideCommand = new RelayCommand(() => RunUiAction("Сброс override аудио", AudioClearOverride));
        AudioNextTrackCommand = new RelayCommand(() => RunUiAction("Следующий трек", AudioNextTrack));
        AudioSelectTrackCommand = new RelayCommand(() => RunUiAction("Выбор трека", AudioSelectTrack));
        AudioReloadLibraryCommand = new RelayCommand(() => RunUiAction("Перезагрузка аудиотеки", AudioReloadLibrary));
        VisibilityLoadCommand = new RelayCommand(VisibilityLoad);
        VisibilitySaveCommand = new RelayCommand(VisibilitySave);
        NotesRefreshCommand = new RelayCommand(NotesRefresh);
        NotesCreateCommand = new RelayCommand(NotesCreate);
        NotesArchiveCommand = new RelayCommand(NotesArchive);
        ReferenceRefreshCommand = new RelayCommand(() => RunUiAction("Обновление reference data", ReferenceRefresh));
        ReferenceCreateCommand = new RelayCommand(() => RunUiAction("Создание reference data", ReferenceCreate));
        ReferenceUpdateCommand = new RelayCommand(() => RunUiAction("Обновление reference data", ReferenceUpdate));
        ReferenceArchiveCommand = new RelayCommand(() => RunUiAction("Архивация reference data", ReferenceArchive));
        BackupRefreshCommand = new RelayCommand(() => RunUiAction("Обновление backup list", BackupRefresh));
        BackupCreateCommand = new RelayCommand(() => RunUiAction("Создание backup", BackupCreate));
        BackupRestoreCommand = new RelayCommand(() => RunUiAction("Восстановление backup", BackupRestore));
        BackupExportCommand = new RelayCommand(() => RunUiAction("Экспорт backup", BackupExport));
        DiagnosticsRefreshCommand = new RelayCommand(() => RunUiAction("Обновление diagnostics", DiagnosticsRefresh));
        FocusContentClassesCommand = new RelayCommand(FocusContentClasses);
        FocusContentReferenceCommand = new RelayCommand(FocusContentReference);
        FocusSystemReferenceCommand = new RelayCommand(FocusSystemReference);
        FocusSystemBackupsCommand = new RelayCommand(FocusSystemBackups);
        FocusSystemDiagnosticsCommand = new RelayCommand(FocusSystemDiagnostics);
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
        ClientLogService.Instance.Info("ui.admin.dice.panel.loaded");
        ClientLogService.Instance.Info("people.grid.template fixed=true");
        ClientLogService.Instance.Info("dice.actor.mode=account");
        TraceDiceAvailability();

        _poller = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        _poller.Tick += (_, _) => RefreshAll();
    }

    public string LoginText { get; set; } = string.Empty;
    public string PasswordText { get; set; } = string.Empty;
    public string OldPasswordText { get; set; } = string.Empty;
    public string NewPasswordText { get; set; } = string.Empty;
    public string ResetPasswordText { get; set; } = "TempPass123";
    public string CreateCharacterName { get; set; } = string.Empty;
    public string CreateCharacterRace { get; set; } = string.Empty;
    public string CreateCharacterBackstory { get; set; } = string.Empty;
    public int DiceCount { get => _diceCount; set { if (_diceCount != value) { _diceCount = value; Notify(); Notify(nameof(CanRollCharacterDice)); Notify(nameof(DiceRollAvailabilityHint)); TraceDiceAvailability(); } } }
    public int DiceFaces { get => _diceFaces; set { if (_diceFaces != value) { _diceFaces = value; Notify(); Notify(nameof(CanRollCharacterDice)); Notify(nameof(DiceRollAvailabilityHint)); TraceDiceAvailability(); } } }
    public int DiceModifier { get => _diceModifier; set { if (_diceModifier != value) { _diceModifier = value; Notify(); } } }
    public string DiceModeInput { get => _diceModeInput; set { if (_diceModeInput != value) { _diceModeInput = value; Notify(); Notify(nameof(DiceRollAvailabilityHint)); TraceDiceAvailability(); } } }
    public string DiceVisibilityInput { get => _diceVisibilityInput; set { if (_diceVisibilityInput != value) { _diceVisibilityInput = value; Notify(); } } }
    public string DiceDescriptionInput { get => _diceDescriptionInput; set { if (_diceDescriptionInput != value) { _diceDescriptionInput = value; Notify(); } } }
    public ObservableCollection<string> DiceModeOptions { get; } = new ObservableCollection<string> { "Обычный", "Тестовый" };
    public ObservableCollection<string> DiceVisibilityOptions { get; } = new ObservableCollection<string> { "Public", "HiddenToAdmins", "AdminOnly" };
    public string ConnectionState { get => _connectionState; set { _connectionState = value; Notify(); } }
    public string ConnectionStatusDetail { get => _connectionStatusDetail; set { _connectionStatusDetail = value; Notify(); } }
    public string SessionSummary { get => _sessionSummary; set { _sessionSummary = value; Notify(); } }
    public bool IsOnline { get => _isOnline; set { _isOnline = value; Notify(); } }
    public bool IsConnectedToServer { get => _isConnectedToServer; set { _isConnectedToServer = value; Notify(); Notify(nameof(ConnectionStage)); Notify(nameof(LoginState)); Notify(nameof(ArePrivilegedSectionsEnabled)); Notify(nameof(SectionAccessHint)); Notify(nameof(CanRollCharacterDice)); Notify(nameof(DiceRollAvailabilityHint)); TraceDiceAvailability(); } }
    public bool IsAuthenticated { get => _isAuthenticated; set { _isAuthenticated = value; Notify(); Notify(nameof(ConnectionStage)); Notify(nameof(LoginState)); Notify(nameof(ArePrivilegedSectionsEnabled)); Notify(nameof(SectionAccessHint)); Notify(nameof(CanRollCharacterDice)); Notify(nameof(DiceRollAvailabilityHint)); TraceDiceAvailability(); } }
    public string LastErrorMessage { get => _lastErrorMessage; set { _lastErrorMessage = value; Notify(); Notify(nameof(HasConnectionError)); Notify(nameof(ConnectionStage)); } }
    public string LastStatusMessage { get => _lastStatusMessage; set { _lastStatusMessage = value; Notify(); } }
    public int LocksCount { get => _locksCount; set { _locksCount = value; Notify(); } }
    public bool HasConnectionError => !string.IsNullOrWhiteSpace(LastErrorMessage);
    public bool ArePrivilegedSectionsEnabled => IsConnectedToServer && IsAuthenticated;
    public string ConnectionStage => HasConnectionError ? "Ошибка подключения" : IsAuthenticated ? "Вошли как админ" : IsConnectedToServer ? "Подключено, вход не выполнен" : "Нет подключения";
    public string LoginState => IsAuthenticated ? $"Администратор: {LoginSummary}" : IsConnectedToServer ? "Сервер доступен, войдите как админ" : "Не авторизован";
    public string SectionAccessHint => ArePrivilegedSectionsEnabled ? "Рабочие разделы активны" : IsConnectedToServer ? "Для рабочих разделов выполните вход" : "Подключитесь к серверу, чтобы активировать рабочие разделы";
    public bool IsConnectionPopupOpen { get => _isConnectionPopupOpen; set { _isConnectionPopupOpen = value; Notify(); } }
    public bool IsAuthPopupOpen { get => _isAuthPopupOpen; set { _isAuthPopupOpen = value; Notify(); } }
    public bool IsBusy { get => _isBusy; set { _isBusy = value; Notify(); Notify(nameof(IsIdle)); Notify(nameof(CanRollCharacterDice)); Notify(nameof(DiceRollAvailabilityHint)); TraceDiceAvailability(); } }
    public bool IsIdle => !IsBusy;
    public string BusyMessage { get => _busyMessage; set { _busyMessage = value; Notify(); } }
    public string ServerHostInput { get => _serverHostInput; set { _serverHostInput = value; Notify(); } }
    public string ServerPortInput { get => _serverPortInput; set { _serverPortInput = value; Notify(); } }
    public string LastServerHost { get => _lastServerHost; set { _lastServerHost = value; Notify(); } }
    public int LastServerPort { get => _lastServerPort; set { _lastServerPort = value; Notify(); } }
    public string SelectedSection { get => _selectedSection; set { _selectedSection = value; Notify(); } }
    public string CharactersSearchText { get => _charactersSearchText; set { _charactersSearchText = value; Notify(); Notify(nameof(FilteredCharacters)); var filtered = FilteredCharacters.Count(); ClientLogService.Instance.Info($"ui-filter section=Люди block=Персонажи query={_charactersSearchText} loaded={Characters.Count} filtered={filtered} visible={filtered}"); } }
    public string LocksSearchText { get => _locksSearchText; set { _locksSearchText = value; Notify(); Notify(nameof(FilteredLockRows)); var filtered = FilteredLockRows.Count(); ClientLogService.Instance.Info($"ui-filter section=Люди block=Блокировки query={_locksSearchText} loaded={LockRows.Count} filtered={filtered} visible={filtered}"); } }
    public string SelectedCharacterWorkspaceTab { get => _selectedCharacterWorkspaceTab; set { _selectedCharacterWorkspaceTab = value; Notify(); } }
    public string ClassSearchText { get => _classSearchText; set { _classSearchText = value; Notify(); Notify(nameof(FilteredClassDefinitionRows)); ClientLogService.Instance.Info($"ui-filter section=Контент block=Классы query={_classSearchText} loaded={ClassDefinitionRows.Count} visible={FilteredClassDefinitionRows.Count()}"); } }
    public string SkillSearchText { get => _skillSearchText; set { _skillSearchText = value; Notify(); Notify(nameof(FilteredSkillDefinitionRows)); ClientLogService.Instance.Info($"ui-filter section=Контент block=Навыки query={_skillSearchText} loaded={SkillDefinitionRows.Count} visible={FilteredSkillDefinitionRows.Count()}"); } }
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
    public string DiagnosticsSummary => DiagnosticsItems.Count == 0 ? "Диагностика: не загружена" : $"{DiagnosticsItems[0].Name} • {DiagnosticsItems[0].State} • {DiagnosticsItems[0].Extra}";
    public string SessionStateSummary => CombatRows.FirstOrDefault(row => row.StartsWith("Status:", StringComparison.OrdinalIgnoreCase))?.Split(':').Skip(1).FirstOrDefault()?.Trim() ?? (ArePrivilegedSectionsEnabled ? "Спокойное состояние" : "Недоступно до входа");
    public int ActiveCombatParticipantsCount => CombatRows.Count(row => row.StartsWith("P:", StringComparison.OrdinalIgnoreCase));
    public string CombatTrackerSummary => HasActiveCombat ? $"Бой активен • участников: {ActiveCombatParticipantsCount}" : ActiveCombatParticipantsCount > 0 ? $"Трекер загружен • участников: {ActiveCombatParticipantsCount}" : "Трекер боя ждёт данных";
    public bool IsSessionActive => ArePrivilegedSectionsEnabled && (HasActiveCombat || ChatRows.Count > 0 || AudioLibraryRows.Count > 0 || !string.IsNullOrWhiteSpace(AudioStateText));
    public int CombatOpponentsCount => CombatParticipantRows.Count(row => row.State.IndexOf("Npc", StringComparison.OrdinalIgnoreCase) >= 0 || row.State.IndexOf("Enemy", StringComparison.OrdinalIgnoreCase) >= 0);
    public RowVm? SelectedCombatParticipant => CombatParticipantRows.FirstOrDefault(row => row.Id == SelectedCombatParticipantId);
    public string SelectedCombatParticipantSummary => SelectedCombatParticipant == null ? "Активный участник боя не выбран." : $"{SelectedCombatParticipant.Name} • {SelectedCombatParticipant.State} • {SelectedCombatParticipant.Extra}";
    public string SessionAttentionSummary => HasActiveCombat ? "Бой активен — проверьте трекер и порядок ходов." : ChatRows.Count > 0 ? "Есть сообщения и активность чата." : !string.IsNullOrWhiteSpace(AudioStateText) ? "Проверьте текущее аудио сессии." : "Сессия ожидает активности.";
    public string ChatActivitySummary => ChatRows.Count == 0 ? "Нет доступных сообщений" : $"Последнее: {ChatRows[0]}";
    public string AudioTrackSummary => string.IsNullOrWhiteSpace(AudioStateText) ? "Нет активного аудио" : AudioStateText;
    public bool CanManageCombatSelection => ArePrivilegedSectionsEnabled && SelectedCombatParticipant != null && !IsBusy;
    public bool CanControlCombat => ArePrivilegedSectionsEnabled && !IsBusy;
    public bool CanSendChat => ArePrivilegedSectionsEnabled && !IsBusy && !string.IsNullOrWhiteSpace(ChatMessageText);
    public bool CanControlAudio => ArePrivilegedSectionsEnabled && !IsBusy;
    public string ContentSummary => $"Классов: {ClassDefinitionRows.Count} • Навыков: {SkillDefinitionRows.Count}";
    public string ContentReadinessSummary => !ArePrivilegedSectionsEnabled ? "Подключитесь и войдите, чтобы работать с контентом." : "Определения классов и навыков готовы к обновлению и редактированию.";
    public string SelectedClassSummary => SelectedClassDefinition == null ? "Класс не выбран." : $"{SelectedClassDefinition.Name} • {SelectedClassDefinition.State} • {SelectedClassDefinition.Extra}";
    public string SelectedSkillSummary => SelectedSkillDefinition == null ? "Навык не выбран." : $"{SelectedSkillDefinition.Name} • {SelectedSkillDefinition.State} • {SelectedSkillDefinition.Extra}";
    public string SelectedReferenceSummary => SelectedReference == null ? "Reference-запись не выбрана." : $"{SelectedReference.Name} • {SelectedReference.State} • {SelectedReference.Extra}";
    public string SelectedContentSummary => SelectedClassDefinition != null ? SelectedClassSummary : SelectedSkillDefinition != null ? SelectedSkillSummary : SelectedReferenceSummary;
    public string ReferenceSummary => ReferenceItems.Count == 0 ? "Справочные данные: нет загруженных записей" : $"Справочные данные: {ReferenceItems.Count} записей типа {ReferenceType}";
    public string BackupSummary => BackupItems.Count == 0 ? "Резервные копии: ещё не загружены" : $"Резервные копии: {BackupItems.Count}, последняя: {BackupItems[0].Name}";
    public string DiagnosticsStatusSummary => DiagnosticsItems.Count == 0 ? "Диагностика ещё не загружена" : DiagnosticsItems[0].Name;
    public string SelectedBackupSummary => SelectedBackup == null ? "Backup не выбран." : $"{SelectedBackup.Name} • {SelectedBackup.State} • {SelectedBackup.Extra}";
    public string SelectedDiagnosticsSummary => SelectedDiagnostics == null ? "Строка диагностики не выбрана." : $"{SelectedDiagnostics.Name} • {SelectedDiagnostics.State} • {SelectedDiagnostics.Extra}";
    public string SystemHealthSummary => DiagnosticsItems.Count == 0 ? "Служебные данные ещё не загружены." : $"Диагностика: {DiagnosticsItems.Count} • Резервные копии: {BackupItems.Count} • Справочные данные: {ReferenceItems.Count}";
    public bool CanControlContent => ArePrivilegedSectionsEnabled && !IsBusy;
    public bool CanRefreshContent => ArePrivilegedSectionsEnabled && !IsBusy;
    public bool CanManageClassDefinition => ArePrivilegedSectionsEnabled && !IsBusy;
    public bool CanArchiveClassDefinition => ArePrivilegedSectionsEnabled && !IsBusy && SelectedClassDefinition != null;
    public bool CanManageSkillDefinition => ArePrivilegedSectionsEnabled && !IsBusy;
    public bool CanArchiveSkillDefinition => ArePrivilegedSectionsEnabled && !IsBusy && SelectedSkillDefinition != null;
    public bool CanAcquireClassNode => ArePrivilegedSectionsEnabled && !IsBusy && !string.IsNullOrWhiteSpace(SelectedCharacterId) && SelectedClassNode != null;
    public bool CanAcquireSkill => ArePrivilegedSectionsEnabled && !IsBusy && !string.IsNullOrWhiteSpace(SelectedCharacterId) && SelectedSkill != null;
    public bool CanManageReferenceRecord => ArePrivilegedSectionsEnabled && !IsBusy && SelectedReference != null;
    public bool CanManageSelectedBackup => ArePrivilegedSectionsEnabled && !IsBusy && SelectedBackup != null;
    public bool CanRefreshSystem => ArePrivilegedSectionsEnabled && !IsBusy;
    public string WorkspaceSummary => $"Панели: встроено {WorkspacePanels.Count(panel => panel.IsVisible && !panel.IsDetached)}, вынесено {WorkspacePanels.Count(panel => panel.IsVisible && panel.IsDetached)}, скрыто {WorkspacePanels.Count(panel => !panel.IsVisible)}";
    public RowVm? SelectedLock => LockRows.FirstOrDefault(row => row.Id == SelectedLockId);
    public RowVm? SelectedPendingAccount => PendingAccounts.FirstOrDefault(row => row.Id == SelectedPendingAccountId);
    public RowVm? SelectedPlayer => Players.FirstOrDefault(row => row.Id == SelectedOwnerUserId);
    public RowVm? SelectedCharacter => Characters.FirstOrDefault(row => row.Id == SelectedCharacterId);
    public RowVm? SelectedRequest => PendingRequests.FirstOrDefault(row => row.Id == SelectedPendingRequestId);
    public RowVm? SelectedClassDefinition => ClassDefinitionRows.FirstOrDefault(row => row.Id == SelectedClassDefinitionCode);
    public RowVm? SelectedSkillDefinition => SkillDefinitionRows.FirstOrDefault(row => row.Id == SelectedSkillDefinitionCode);
    public RowVm? SelectedClassNode => ClassTreeItems.FirstOrDefault(row => row.Id == SelectedClassNodeId);
    public RowVm? SelectedSkill => SkillRows.FirstOrDefault(row => row.Id == SelectedSkillId);
    public RowVm? SelectedReference => ReferenceItems.FirstOrDefault(row => row.Id == ReferenceId);
    public RowVm? SelectedBackup => BackupItems.FirstOrDefault(row => row.Id == SelectedBackupId);
    public RowVm? SelectedDiagnostics => DiagnosticsItems.FirstOrDefault(row => row.Id == SelectedDiagnosticsId);
    public string SelectedPendingAccountSummary => SelectedPendingAccount == null ? "Выберите ожидающий аккаунт, чтобы увидеть детали и действия." : $"{SelectedPendingAccount.Name} • {SelectedPendingAccount.State} • {SelectedPendingAccount.Extra}";
    public string SelectedPlayerSummary => SelectedPlayer == null ? "Выберите игрока, чтобы загрузить связанных персонажей." : $"{SelectedPlayer.Name} • {SelectedPlayer.State} • {SelectedPlayer.Extra}";
    public string SelectedCharacterSummary => SelectedCharacter == null ? "Персонаж не выбран." : $"{SelectedCharacter.Name} • race: {SelectedCharacter.Extra} • archived: {SelectedCharacter.State}";
    public string SelectedRequestSummary => SelectedRequest == null ? "Активная заявка не выбрана." : $"{SelectedRequest.Name} • {SelectedRequest.State} • {SelectedRequest.Extra}";
    public string SelectedLockSummary => SelectedLock == null ? "Активный lock не выбран." : $"{SelectedLock.Name} • {SelectedLock.State} • {SelectedLock.Extra}";
    public string CharacterActionSummary => !ArePrivilegedSectionsEnabled ? "Подключитесь и выполните вход, чтобы работать с персонажами." : SelectedCharacter == null ? "Выберите персонажа для editor / visibility / content actions." : IsBusy ? $"Выполняется: {BusyMessage}" : "Действия с персонажем доступны.";
    public string ChatModerationSummary => !ArePrivilegedSectionsEnabled ? "Чат-модерация станет доступна после авторизации." : IsBusy ? $"Выполняется: {BusyMessage}" : string.IsNullOrWhiteSpace(ChatModerationUserId) ? "Для mute/unmute укажите userId; lock/slow mode уже доступны." : $"Готово к модерации пользователя {ChatModerationUserId}.";
    public string SystemActionSummary => !ArePrivilegedSectionsEnabled ? "Системные инструменты требуют подключения и авторизации." : IsBusy ? $"Выполняется: {BusyMessage}" : "Системные действия готовы к запуску.";
    public string HeaderStatusSummary => HasConnectionError ? LastErrorMessage : $"{ConnectionStage} • {LoginState}";
    public bool CanManagePendingAccount => ArePrivilegedSectionsEnabled && SelectedPendingAccount != null && !IsBusy;
    public bool CanResetSelectedAccountPassword => ArePrivilegedSectionsEnabled && !IsBusy && (!string.IsNullOrWhiteSpace(SelectedPendingAccountId) || !string.IsNullOrWhiteSpace(SelectedOwnerUserId));
    public bool CanLoadPlayerCharacters => ArePrivilegedSectionsEnabled && SelectedPlayer != null && !IsBusy;
    public bool CanOpenSelectedCharacter => ArePrivilegedSectionsEnabled && SelectedCharacter != null && !IsBusy;
    public bool CanModerateSelectedRequest => ArePrivilegedSectionsEnabled && SelectedRequest != null && !IsBusy;
    public bool CanManageSelectedLock => ArePrivilegedSectionsEnabled && SelectedLock != null && !IsBusy;
    public bool CanManageSelectedCharacter => ArePrivilegedSectionsEnabled && SelectedCharacter != null && !IsBusy;
    public bool CanCreateCharacterForOwner => ArePrivilegedSectionsEnabled && !IsBusy && !string.IsNullOrWhiteSpace(SelectedOwnerUserId);
    public bool CanRollCharacterDice => string.IsNullOrWhiteSpace(DiceRollAvailabilityHint);
    public string DiceRollAvailabilityHint => GetDiceRollAvailabilityReason();
    public bool CanManageCharacterVisibility => ArePrivilegedSectionsEnabled && SelectedCharacter != null && !IsBusy;
    public bool CanRefreshNotes => ArePrivilegedSectionsEnabled && !IsBusy;
    public bool CanCreateNote => ArePrivilegedSectionsEnabled && !IsBusy;
    public bool CanArchiveNote => ArePrivilegedSectionsEnabled && !IsBusy && !string.IsNullOrWhiteSpace(SelectedNoteId);
    public bool CanModerateChatUser => ArePrivilegedSectionsEnabled && !IsBusy && !string.IsNullOrWhiteSpace(ChatModerationUserId);
    public bool CanManageChatControls => ArePrivilegedSectionsEnabled && !IsBusy;
    public bool CanManageWorkspace => !IsBusy;
    public bool CanInitiateConnection => !IsBusy;
    public int SelectedContentTabIndex { get => _selectedContentTabIndex; set { if (_selectedContentTabIndex != value) { _selectedContentTabIndex = value; Notify(); } } }
    public int SelectedSystemTabIndex { get => _selectedSystemTabIndex; set { if (_selectedSystemTabIndex != value) { _selectedSystemTabIndex = value; Notify(); } } }
    public string WorkspaceLayoutPath => Path.Combine(_appDataDirectory, "workspace.layout.json");
    public string ConnectionSettingsPath => Path.Combine(_appDataDirectory, "connection.settings.json");

    public string SelectedPendingAccountId
    {
        get => _selectedPendingAccountId;
        set
        {
            if (_selectedPendingAccountId != value)
            {
                _selectedPendingAccountId = value;
                Notify();
                Notify(nameof(SelectedPendingAccount));
                Notify(nameof(SelectedPendingAccountSummary));
                Notify(nameof(CanManagePendingAccount));
                Notify(nameof(CanResetSelectedAccountPassword));
            }
        }
    }

    public string SelectedOwnerUserId
    {
        get => _selectedOwnerUserId;
        set
        {
            if (_selectedOwnerUserId != value)
            {
                _selectedOwnerUserId = value;
                Notify();
                Notify(nameof(SelectedPlayer));
                Notify(nameof(SelectedPlayerSummary));
                Notify(nameof(CanLoadPlayerCharacters));
                Notify(nameof(CanCreateCharacterForOwner));
                Notify(nameof(CanResetSelectedAccountPassword));
                ClientLogService.Instance.Info($"ui.people.owner.selected ownerUserId={_selectedOwnerUserId}");
                if (!string.IsNullOrWhiteSpace(_selectedOwnerUserId) && ArePrivilegedSectionsEnabled)
                {
                    LoadOwnerCharacters();
                }
            }
        }
    }

    public string SelectedCharacterId
    {
        get => _selectedCharacterId;
        set
        {
            if (_selectedCharacterId != value)
            {
                _selectedCharacterId = value;
                if (string.Equals(NoteTargetType, "character", StringComparison.OrdinalIgnoreCase))
                {
                    NoteTargetId = value;
                    Notify(nameof(NoteTargetId));
                }
                Notify();
                Notify(nameof(SelectedCharacter));
                Notify(nameof(SelectedCharacterSummary));
                Notify(nameof(CanOpenSelectedCharacter));
                Notify(nameof(CanRollCharacterDice));
                Notify(nameof(DiceRollAvailabilityHint));
                TraceDiceAvailability();
                ClientLogService.Instance.Info($"ui.people.character.selected characterId={_selectedCharacterId}");
            }
        }
    }

    public string SelectedPendingRequestId
    {
        get => _selectedPendingRequestId;
        set
        {
            if (_selectedPendingRequestId != value)
            {
                _selectedPendingRequestId = value;
                Notify();
                Notify(nameof(SelectedRequest));
                Notify(nameof(SelectedRequestSummary));
                Notify(nameof(CanModerateSelectedRequest));
            }
        }
    }

    public string SelectedLockId
    {
        get => _selectedLockId;
        set
        {
            if (_selectedLockId != value)
            {
                _selectedLockId = value;
                Notify();
                Notify(nameof(SelectedLock));
                Notify(nameof(SelectedLockSummary));
                Notify(nameof(CanManageSelectedLock));
            }
        }
    }
    public string RequestComment { get; set; } = string.Empty;
    public string CombatSessionId { get; set; } = "default";
    public string NewParticipantName { get; set; } = "New NPC";
    public string NewParticipantKind { get; set; } = "Npc";
    public string SelectedCombatParticipantId
    {
        get => _selectedCombatParticipantId;
        set
        {
            if (_selectedCombatParticipantId != value)
            {
                _selectedCombatParticipantId = value;
                Notify();
                Notify(nameof(SelectedCombatParticipant));
                Notify(nameof(SelectedCombatParticipantSummary));
                Notify(nameof(CanManageCombatSelection));
            }
        }
    }
    public string LockStateText { get; set; } = string.Empty;
    public string SelectedClassNodeId
    {
        get => _selectedClassNodeId;
        set
        {
            if (_selectedClassNodeId != value)
            {
                _selectedClassNodeId = value;
                Notify();
                Notify(nameof(SelectedClassNode));
                Notify(nameof(SelectedClassSummary));
                Notify(nameof(SelectedContentSummary));
                Notify(nameof(CanAcquireClassNode));
            }
        }
    }
    public string SelectedClassDefinitionCode
    {
        get => _selectedClassDefinitionCode;
        set
        {
            if (_selectedClassDefinitionCode != value)
            {
                _selectedClassDefinitionCode = value;
                Notify();
                Notify(nameof(SelectedClassDefinition));
                Notify(nameof(SelectedClassSummary));
                Notify(nameof(SelectedContentSummary));
                Notify(nameof(CanArchiveClassDefinition));
            }
        }
    }

    public string SelectedSkillDefinitionCode
    {
        get => _selectedSkillDefinitionCode;
        set
        {
            if (_selectedSkillDefinitionCode != value)
            {
                _selectedSkillDefinitionCode = value;
                Notify();
                Notify(nameof(SelectedSkillDefinition));
                Notify(nameof(SelectedSkillSummary));
                Notify(nameof(SelectedContentSummary));
                Notify(nameof(CanArchiveSkillDefinition));
            }
        }
    }

    public string SelectedSkillId
    {
        get => _selectedSkillId;
        set
        {
            if (_selectedSkillId != value)
            {
                _selectedSkillId = value;
                Notify();
                Notify(nameof(SelectedSkill));
                Notify(nameof(SelectedSkillSummary));
                Notify(nameof(SelectedContentSummary));
                Notify(nameof(CanAcquireSkill));
            }
        }
    }
    public string DefinitionVersionText { get; set; } = string.Empty;
    public string EditClassCode { get; set; } = string.Empty;
    public string EditClassName { get; set; } = string.Empty;
    public string EditClassDescription { get; set; } = string.Empty;
    public string EditClassDirectionCode { get; set; } = string.Empty;
    public string EditClassBranchCode { get; set; } = string.Empty;
    public string EditClassRootClassCode { get; set; } = string.Empty;
    public string EditClassParentClassCode { get; set; } = string.Empty;
    public int EditClassLevel { get; set; } = 1;
    public string EditClassGrantedSkillCodes { get; set; } = string.Empty;
    public string EditClassRequiredClassCodes { get; set; } = string.Empty;
    public bool EditClassIsActive { get; set; } = true;
    public string EditClassStatus { get; set; } = DefinitionStatus.Draft.ToString();
    public string EditSkillCode { get; set; } = string.Empty;
    public string EditSkillName { get; set; } = string.Empty;
    public string EditSkillDescription { get; set; } = string.Empty;
    public int EditSkillTier { get; set; } = 1;
    public int EditSkillMaxLevel { get; set; } = 1;
    public string EditSkillCategory { get; set; } = SkillCategory.Undefined.ToString();
    public bool EditSkillIsClassSkill { get; set; }
    public string EditSkillRequiredClassCodes { get; set; } = string.Empty;
    public string EditSkillRequiredSkillCodes { get; set; } = string.Empty;
    public bool EditSkillIsActive { get; set; } = true;
    public string EditSkillStatus { get; set; } = DefinitionStatus.Draft.ToString();
    public string DefinitionHintText => string.IsNullOrWhiteSpace(EditClassParentClassCode) ? "Для корневого класса ParentClassCode можно оставить пустым." : "Если ParentClassCode задан, сервер ожидает Level = parent.Level + 1.";
    public string SkillEditorHintText => SkillLevelEditorRows.Count == 0 ? "Добавьте хотя бы один уровень навыка перед сохранением." : $"Уровней навыка: {SkillLevelEditorRows.Count}. MaxLevel сейчас {EditSkillMaxLevel}.";
    public string ChatSessionId { get; set; } = "default";
    public string ChatMessageText { get; set; } = string.Empty;
    public string ChatMessageType { get; set; } = "Обычный";
    public ObservableCollection<string> ChatMessageTypeOptions { get; } = new ObservableCollection<string> { "Обычный", "Скрытый", "Только для админов" };
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
    public string ReferenceId
    {
        get => _selectedReferenceId;
        set
        {
            if (_selectedReferenceId != value)
            {
                _selectedReferenceId = value;
                var selected = ReferenceItems.FirstOrDefault(row => row.Id == value);
                if (selected != null)
                {
                    ReferenceDisplayName = selected.Name;
                    ReferenceKey = selected.Extra.StartsWith("key=", StringComparison.OrdinalIgnoreCase) ? selected.Extra.Substring(4) : ReferenceKey;
                    Notify(nameof(ReferenceDisplayName));
                    Notify(nameof(ReferenceKey));
                }
                Notify();
                Notify(nameof(SelectedReference));
                Notify(nameof(SelectedReferenceSummary));
                Notify(nameof(SelectedContentSummary));
                Notify(nameof(CanManageReferenceRecord));
            }
        }
    }
    public string ReferenceKey { get; set; } = string.Empty;
    public string ReferenceDisplayName { get; set; } = string.Empty;
    public string ReferenceDataJson { get; set; } = "{}";
    public string BackupLabel { get; set; } = string.Empty;
    public string SelectedBackupId
    {
        get => _selectedBackupId;
        set
        {
            if (_selectedBackupId != value)
            {
                _selectedBackupId = value;
                Notify();
                Notify(nameof(SelectedBackup));
                Notify(nameof(SelectedBackupSummary));
                Notify(nameof(CanManageSelectedBackup));
            }
        }
    }
    public string SelectedDiagnosticsId
    {
        get => _selectedDiagnosticsId;
        set
        {
            if (_selectedDiagnosticsId != value)
            {
                _selectedDiagnosticsId = value;
                Notify();
                Notify(nameof(SelectedDiagnostics));
                Notify(nameof(SelectedDiagnosticsSummary));
            }
        }
    }
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
    public long Platinum { get; set; }
    public long Orichalcum { get; set; }
    public long Adamant { get; set; }
    public long Sovereign { get; set; }
    public long ExperienceCoins { get; set; }

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
    public ObservableCollection<RowVm> CombatParticipantRows { get; } = new ObservableCollection<RowVm>();
    public ObservableCollection<string> CombatHistoryRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<RowVm> ClassDefinitionRows { get; } = new ObservableCollection<RowVm>();
    public ObservableCollection<RowVm> SkillDefinitionRows { get; } = new ObservableCollection<RowVm>();
    public ObservableCollection<SkillLevelEditorRowVm> SkillLevelEditorRows { get; } = new ObservableCollection<SkillLevelEditorRowVm>();
    public ObservableCollection<RowVm> ClassTreeItems { get; } = new ObservableCollection<RowVm>();
    public ObservableCollection<RowVm> SkillRows { get; } = new ObservableCollection<RowVm>();
    public ObservableCollection<string> ChatRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<ChatMessageRowVm> ChatMessageRows { get; } = new ObservableCollection<ChatMessageRowVm>();
    public ObservableCollection<string> ChatRestrictionRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<string> AudioLibraryRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<string> NotesRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<RowVm> ReferenceItems { get; } = new ObservableCollection<RowVm>();
    public ObservableCollection<RowVm> BackupItems { get; } = new ObservableCollection<RowVm>();
    public ObservableCollection<RowVm> DiagnosticsItems { get; } = new ObservableCollection<RowVm>();
    public ObservableCollection<RowVm> LockRows { get; } = new ObservableCollection<RowVm>();
    public IEnumerable<RowVm> FilteredCharacters => string.IsNullOrWhiteSpace(CharactersSearchText)
        ? Characters
        : Characters.Where(row => row.Name.IndexOf(CharactersSearchText, StringComparison.OrdinalIgnoreCase) >= 0
                                  || row.Id.IndexOf(CharactersSearchText, StringComparison.OrdinalIgnoreCase) >= 0
                                  || row.Extra.IndexOf(CharactersSearchText, StringComparison.OrdinalIgnoreCase) >= 0);
    public IEnumerable<RowVm> FilteredLockRows => string.IsNullOrWhiteSpace(LocksSearchText)
        ? LockRows
        : LockRows.Where(row => row.Name.IndexOf(LocksSearchText, StringComparison.OrdinalIgnoreCase) >= 0
                                || row.Id.IndexOf(LocksSearchText, StringComparison.OrdinalIgnoreCase) >= 0
                                || row.Extra.IndexOf(LocksSearchText, StringComparison.OrdinalIgnoreCase) >= 0);
    public IEnumerable<RowVm> FilteredClassDefinitionRows => string.IsNullOrWhiteSpace(ClassSearchText)
        ? ClassDefinitionRows
        : ClassDefinitionRows.Where(row => row.Name.IndexOf(ClassSearchText, StringComparison.OrdinalIgnoreCase) >= 0
                                           || row.Id.IndexOf(ClassSearchText, StringComparison.OrdinalIgnoreCase) >= 0
                                           || row.Extra.IndexOf(ClassSearchText, StringComparison.OrdinalIgnoreCase) >= 0);
    public IEnumerable<RowVm> FilteredSkillDefinitionRows => string.IsNullOrWhiteSpace(SkillSearchText)
        ? SkillDefinitionRows
        : SkillDefinitionRows.Where(row => row.Name.IndexOf(SkillSearchText, StringComparison.OrdinalIgnoreCase) >= 0
                                           || row.Id.IndexOf(SkillSearchText, StringComparison.OrdinalIgnoreCase) >= 0
                                           || row.Extra.IndexOf(SkillSearchText, StringComparison.OrdinalIgnoreCase) >= 0);
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
    public ICommand ToggleAuthPopupCommand { get; }
    public ICommand ConnectToServerCommand { get; }
    public ICommand ApplyConnectionSettingsCommand { get; }
    public ICommand ResetConnectionDefaultsCommand { get; }
    public ICommand UseSavedConnectionSettingsCommand { get; }
    public ICommand ApproveCommand { get; }
    public ICommand ArchiveCommand { get; }
    public ICommand RejectAccountCommand { get; }
    public ICommand BlockAccountCommand { get; }
    public ICommand UnblockAccountCommand { get; }
    public ICommand ChangePasswordCommand { get; }
    public ICommand ResetPasswordCommand { get; }
    public ICommand CreateCharacterCommand { get; }
    public ICommand RollDiceCommand { get; }
    public ICommand LoadOwnerCharactersCommand { get; }
    public ICommand OpenCharacterCommand { get; }
    public ICommand OpenPlayerCharactersCommand { get; }
    public ICommand FocusSelectedCharacterCommand { get; }
    public ICommand FocusSelectedRequestCommand { get; }
    public ICommand FocusCharacterEditorCommand { get; }
    public ICommand FocusCharacterNotesCommand { get; }
    public ICommand FocusCharacterVisibilityCommand { get; }
    public ICommand RefreshSelectedCharacterCommand { get; }
    public ICommand RefreshPeopleSectionCommand { get; }
    public ICommand RefreshModerationSectionCommand { get; }
    public ICommand RefreshSessionSectionCommand { get; }
    public ICommand RefreshContentSectionCommand { get; }
    public ICommand RefreshSystemSectionCommand { get; }
    public ICommand AcquireLockCommand { get; }
    public ICommand ReleaseLockCommand { get; }
    public ICommand ForceUnlockCommand { get; }
    public ICommand SaveBasicInfoCommand { get; }
    public ICommand SaveStatsCommand { get; }
    public ICommand SaveMoneyCommand { get; }
    public ICommand SaveXpCoinsCommand { get; }
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
    public ICommand RefreshDefinitionClassesCommand { get; }
    public ICommand NewClassDefinitionCommand { get; }
    public ICommand OpenSelectedClassDefinitionCommand { get; }
    public ICommand SaveClassDefinitionCommand { get; }
    public ICommand ArchiveClassDefinitionCommand { get; }
    public ICommand RefreshDefinitionSkillsCommand { get; }
    public ICommand NewSkillDefinitionCommand { get; }
    public ICommand OpenSelectedSkillDefinitionCommand { get; }
    public ICommand SaveSkillDefinitionCommand { get; }
    public ICommand ArchiveSkillDefinitionCommand { get; }
    public ICommand AddSkillLevelCommand { get; }
    public ICommand RemoveSkillLevelCommand { get; }
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
    public ICommand FocusContentClassesCommand { get; }
    public ICommand FocusContentReferenceCommand { get; }
    public ICommand FocusSystemReferenceCommand { get; }
    public ICommand FocusSystemBackupsCommand { get; }
    public ICommand FocusSystemDiagnosticsCommand { get; }
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
            ClientLogService.Instance.Info($"Server connection established: {host}:{port}");
            LastServerHost = host;
            LastServerPort = port;
            SaveConnectionSettings();
            IsAuthenticated = false;
            SetConnectedState($"Соединение установлено: {host}:{port}");
            IsConnectionPopupOpen = false;
            IsAuthPopupOpen = false;
        }
        catch (Exception ex)
        {
            ClientLogService.Instance.Error($"Server connection failed: {host}:{port}", ex);
            SetConnectionError($"Не удалось подключиться к {host}:{port}. {ex.Message}");
        }
    }

    public void ResetConnectionDefaults()
    {
        ServerHostInput = "127.0.0.1";
        ServerPortInput = "4600";
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
        SessionSummary = $"Стадия: {ConnectionStage} • {LoginState} • Ожидают: {PendingAccountsCount} • Игроков: {PlayersCount} • Персонажей: {CharactersCount} • Заявок: {PendingRequestsCount} • Блокировок: {LocksCount}";
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
        Notify(nameof(IsSessionActive));
        Notify(nameof(CombatOpponentsCount));
        Notify(nameof(SelectedCombatParticipant));
        Notify(nameof(SelectedCombatParticipantSummary));
        Notify(nameof(SessionAttentionSummary));
        Notify(nameof(ChatActivitySummary));
        Notify(nameof(AudioTrackSummary));
        Notify(nameof(CanManageCombatSelection));
        Notify(nameof(CanControlCombat));
        Notify(nameof(CanSendChat));
        Notify(nameof(CanControlAudio));
        Notify(nameof(ContentSummary));
        Notify(nameof(ContentReadinessSummary));
        Notify(nameof(SelectedClassNode));
        Notify(nameof(SelectedSkill));
        Notify(nameof(SelectedReference));
        Notify(nameof(SelectedBackup));
        Notify(nameof(SelectedDiagnostics));
        Notify(nameof(SelectedClassSummary));
        Notify(nameof(SelectedSkillSummary));
        Notify(nameof(SelectedReferenceSummary));
        Notify(nameof(SelectedContentSummary));
        Notify(nameof(ReferenceSummary));
        Notify(nameof(BackupSummary));
        Notify(nameof(DiagnosticsStatusSummary));
        Notify(nameof(SelectedBackupSummary));
        Notify(nameof(SelectedDiagnosticsSummary));
        Notify(nameof(SystemHealthSummary));
        Notify(nameof(WorkspaceSummary));
        Notify(nameof(SelectedPendingAccount));
        Notify(nameof(SelectedPlayer));
        Notify(nameof(SelectedCharacter));
        Notify(nameof(SelectedRequest));
        Notify(nameof(SelectedPendingAccountSummary));
        Notify(nameof(SelectedPlayerSummary));
        Notify(nameof(SelectedCharacterSummary));
        Notify(nameof(SelectedRequestSummary));
        Notify(nameof(CharacterActionSummary));
        Notify(nameof(ChatModerationSummary));
        Notify(nameof(SystemActionSummary));
        Notify(nameof(SelectedLock));
        Notify(nameof(SelectedLockSummary));
        Notify(nameof(HeaderStatusSummary));
        Notify(nameof(CanManagePendingAccount));
        Notify(nameof(CanLoadPlayerCharacters));
        Notify(nameof(CanOpenSelectedCharacter));
        Notify(nameof(CanModerateSelectedRequest));
        Notify(nameof(CanManageSelectedLock));
        Notify(nameof(CanManageSelectedCharacter));
        Notify(nameof(CanManageCharacterVisibility));
        Notify(nameof(CanRefreshNotes));
        Notify(nameof(CanCreateNote));
        Notify(nameof(CanArchiveNote));
        Notify(nameof(CanModerateChatUser));
        Notify(nameof(CanManageChatControls));
        Notify(nameof(CanManageWorkspace));
        Notify(nameof(CanInitiateConnection));
        Notify(nameof(CanControlContent));
        Notify(nameof(CanRefreshContent));
        Notify(nameof(CanAcquireClassNode));
        Notify(nameof(CanAcquireSkill));
        Notify(nameof(CanManageReferenceRecord));
        Notify(nameof(CanManageSelectedBackup));
        Notify(nameof(CanRefreshSystem));
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
        WorkspacePanels.Add(new WorkspacePanelDescriptor("CombatTracker", "Трекер боя", canDetach: true));
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
        ClientLogService.Instance.Info($"ui-panel action=detach panel={panel.PanelId}");
        SaveWorkspaceLayout();
        RefreshConnectionSummary();
    }

    private void AttachWorkspacePanel(string? panelId)
    {
        if (string.IsNullOrWhiteSpace(panelId)) return;
        var panel = GetPanelById(panelId);
        panel.IsDetached = false;
        panel.IsVisible = true;
        ClientLogService.Instance.Info($"ui-panel action=attach panel={panel.PanelId}");
        SaveWorkspaceLayout();
        RefreshConnectionSummary();
    }


    private void OpenPlayerCharacters()
    {
        if (string.IsNullOrWhiteSpace(SelectedOwnerUserId)) return;
        RunUiAction("Загрузка персонажей игрока", () =>
        {
            LoadOwnerCharacters();
            SelectedSection = "Люди";
        });
    }

    private void FocusSelectedCharacter()
    {
        if (string.IsNullOrWhiteSpace(SelectedCharacterId)) return;
        SelectedSection = "Персонажи";
        FocusCharacterEditor();
        OpenCharacter();
    }

    private void FocusSelectedRequest()
    {
        if (string.IsNullOrWhiteSpace(SelectedPendingRequestId)) return;
        SelectedSection = "Модерация";
    }

    private void FocusCharacterEditor()
    {
        SelectedSection = "Персонажи";
        SelectedCharacterWorkspaceTab = "Editor";
        ShowWorkspacePanel("CharacterEditor");
    }

    private void FocusCharacterNotes()
    {
        SelectedSection = "Персонажи";
        SelectedCharacterWorkspaceTab = "Notes";
        ShowWorkspacePanel("NotesManagement");
    }

    private void FocusCharacterVisibility()
    {
        SelectedSection = "Персонажи";
        SelectedCharacterWorkspaceTab = "Visibility";
    }

    private void RefreshSelectedCharacter()
    {
        if (string.IsNullOrWhiteSpace(SelectedCharacterId)) return;
        RunUiAction("Обновление персонажа", OpenCharacter);
    }

    private void RefreshPeopleSection()
    {
        RunUiAction("Обновление раздела Люди", () =>
        {
            LoadPending();
            LoadPlayers();
            if (!string.IsNullOrWhiteSpace(SelectedOwnerUserId))
            {
                LoadOwnerCharacters();
            }
            LoadLocksSummary();
            ClientLogService.Instance.Debug($"ui-refresh section=Люди final pending={PendingAccounts.Count} players={Players.Count} characters={Characters.Count} locks={LockRows.Count}");
        });
    }

    private void RefreshModerationSection()
    {
        RunUiAction("Обновление модерации", () =>
        {
            LoadPendingRequests();
            LoadRequestHistory();
            ClientLogService.Instance.Debug($"ui-refresh section=Модерация final requests={PendingRequests.Count} history={RequestHistoryRows.Count} dice={DiceFeedRows.Count}");
        });
    }

    private void RefreshSessionSection()
    {
        RunUiAction("Обновление сессии", () =>
        {
            CombatRefresh();
            ChatRefresh();
            AudioRefresh();
            ClientLogService.Instance.Debug($"ui-refresh section=Сессия final combatRows={CombatRows.Count} chatRows={ChatRows.Count} audioRows={AudioLibraryRows.Count}");
        });
    }

    private void RefreshContentSection()
    {
        RunUiAction("Обновление контента", () =>
        {
            DefinitionsReload();
            RefreshDefinitionClasses();
            RefreshDefinitionSkills();
            ClientLogService.Instance.Debug($"ui-refresh section=Контент final classes={ClassDefinitionRows.Count} skills={SkillDefinitionRows.Count}");
        });
    }

    private void RefreshSystemSection()
    {
        RunUiAction("Обновление системных инструментов", () =>
        {
            BackupRefresh();
            DiagnosticsRefresh();
            ClientLogService.Instance.Debug($"ui-refresh section=Система final backups={BackupItems.Count} diagnostics={DiagnosticsItems.Count}");
        });
    }

    private void FocusContentClasses()
    {
        SelectedSection = "Контент";
        SelectedContentTabIndex = 0;
    }

    private void FocusContentReference()
    {
        SelectedSection = "Контент";
        SelectedContentTabIndex = 1;
    }

    private void FocusSystemReference()
    {
        SelectedSection = "Система";
        SelectedSystemTabIndex = 0;
    }

    private void FocusSystemBackups()
    {
        SelectedSection = "Система";
        SelectedSystemTabIndex = 1;
    }

    private void FocusSystemDiagnostics()
    {
        SelectedSection = "Система";
        SelectedSystemTabIndex = 2;
    }

    private void RestoreSelection(ObservableCollection<RowVm> source, string selectedId, Action<string> setter)
    {
        if (string.IsNullOrWhiteSpace(selectedId))
        {
            return;
        }

        if (source.Any(row => row.Id == selectedId))
        {
            setter(selectedId);
            return;
        }

        setter(string.Empty);
    }

    private void RunUiAction(string message, Action action)
    {
        try
        {
            IsBusy = true;
            BusyMessage = message;
            LastStatusMessage = message;
            action();
            if (string.IsNullOrWhiteSpace(LastErrorMessage))
            {
                LastStatusMessage = message + " — готово";
            }
        }
        catch (Exception ex)
        {
            LastErrorMessage = ex.Message;
            LastStatusMessage = message + " — ошибка";
            RefreshConnectionSummary();
        }
        finally
        {
            IsBusy = false;
            BusyMessage = string.Empty;
            RefreshConnectionSummary();
        }
    }

    private void Login()
    {
        try
        {
            ConnectToServer();
            ClientLogService.Instance.Info($"Login attempt: user={LoginText}");
            var r = _api.Login(LoginText, PasswordText);
            if (r.Status == ResponseStatus.Ok)
            {
                var roleItems = ToList(r.Payload.ContainsKey("roles") ? r.Payload["roles"] : new ArrayList());
                var resolvedRoles = new List<string>();
                var isAdmin = false;
                foreach (var roleItem in roleItems)
                {
                    var roleValue = Convert.ToString(roleItem) ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(roleValue))
                    {
                        continue;
                    }

                    resolvedRoles.Add(roleValue);
                    if (string.Equals(roleValue, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase)
                        || string.Equals(roleValue, UserRole.SuperAdmin.ToString(), StringComparison.OrdinalIgnoreCase))
                    {
                        isAdmin = true;
                    }
                }
                ClientLogService.Instance.Info($"admin.roleGate rolesResolved={string.Join(", ", resolvedRoles)}");
                ClientLogService.Instance.Info($"admin.roleGate isAdmin={isAdmin}");
                if (!isAdmin)
                {
                    _poller.Stop();
                    IsAuthenticated = false;
                    IsConnectedToServer = false;
                    IsOnline = false;
                    ConnectionState = "Оффлайн";
                    ConnectionStatusDetail = "Этот аккаунт не имеет прав администратора";
                    LastStatusMessage = ConnectionStatusDetail;
                    LastErrorMessage = string.Empty;
                    _client.Disconnect();
                    RefreshConnectionSummary();
                    ClientLogService.Instance.Warn("auth.login.denied client-gate reason=insufficient-admin-role");
                    return;
                }

                IsAuthenticated = true;
                SetConnectedState($"Авторизация успешна: {CurrentEndpoint}");
                _poller.Start();
                RefreshAll();
                IsAuthPopupOpen = false;
                Notify(nameof(LoginSummary));
                ClientLogService.Instance.Info($"Login success: user={LoginText}");
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
                ClientLogService.Instance.Warn($"Login failed: user={LoginText}; message={r.Message}");
            }
        }
        catch (Exception ex)
        {
            SetConnectionError($"Ошибка входа: {ex.Message}");
        }
    }

    private void ChangePassword()
    {
        RunUiAction("Смена пароля администратора", () =>
        {
            ClientLogService.Instance.Info("ui.password.change.opened");
            var response = _api.ChangePassword(OldPasswordText, NewPasswordText);
            EnsureSuccess(response);
            OldPasswordText = string.Empty;
            NewPasswordText = string.Empty;
            Notify(nameof(OldPasswordText));
            Notify(nameof(NewPasswordText));
            ClientLogService.Instance.Info("auth.changePassword result=ok");
        });
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
            ClientLogService.Instance.Debug("ui-refresh section=Люди step=LoadPending");
            LoadPending();
            ClientLogService.Instance.Debug("ui-refresh section=Люди step=LoadPlayers");
            LoadPlayers();
            ClientLogService.Instance.Debug("ui-refresh section=Модерация step=LoadPendingRequests");
            LoadPendingRequests();
            LoadRequestHistory();
            ClientLogService.Instance.Debug("ui-refresh section=Сессия step=CombatRefresh");
            CombatRefresh();
            ClientLogService.Instance.Debug("ui-refresh section=Контент step=RefreshDefinitionClasses");
            RefreshDefinitionClasses();
            RefreshDefinitionSkills();
            if (!string.IsNullOrWhiteSpace(SelectedCharacterId))
            {
                ClientLogService.Instance.Debug("ui-refresh section=Персонажи step=LoadClassTree+LoadSkills");
                LoadClassTree();
                LoadSkills();
            }
            ClientLogService.Instance.Debug("ui-refresh section=Сессия step=ChatRefresh");
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
            var m = AsMap(obj);
            if (m == null) continue;
            PendingAccounts.Add(new RowVm { Id = S(m, "accountId"), Name = S(m, "login"), State = S(m, "status"), Extra = S(m, "createdUtc") });
        }
        ClientLogService.Instance.Debug($"ui-refresh section=Люди block=Ожидающие raw={ToList(r.Payload["items"]).Count} shown={PendingAccounts.Count}");
        ClientLogService.Instance.Info($"people.grid.rows count={PendingAccounts.Count}");
        RestoreSelection(PendingAccounts, SelectedPendingAccountId, value => SelectedPendingAccountId = value);
        RefreshConnectionSummary();
    }

    private void LoadPlayers()
    {
        Players.Clear();
        var r = _api.GetPlayers();
        if (r.Status != ResponseStatus.Ok || !r.Payload.ContainsKey("items")) return;
        foreach (var obj in ToList(r.Payload["items"]))
        {
            var m = AsMap(obj);
            if (m == null) continue;
            Players.Add(new RowVm { Id = S(m, "accountId"), Name = S(m, "login"), State = S(m, "status"), Extra = $"online={S(m, "isOnline")}; last={S(m, "lastSeenUtc")}" });
        }
        ClientLogService.Instance.Debug($"ui-refresh section=Люди block=Игроки raw={ToList(r.Payload["items"]).Count} shown={Players.Count}");
        ClientLogService.Instance.Info($"people.grid.rows count={Players.Count}");
        RestoreSelection(Players, SelectedOwnerUserId, value => SelectedOwnerUserId = value);
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
            var m = AsMap(obj);
            if (m == null) continue;
            Characters.Add(new RowVm { Id = S(m, "characterId"), Name = S(m, "name"), State = S(m, "archived"), Extra = S(m, "race") });
        }
        Notify(nameof(FilteredCharacters));
        var visibleCharacters = FilteredCharacters.Count();
        ClientLogService.Instance.Debug($"ui-refresh section=Люди block=Персонажи loaded={Characters.Count} filtered={visibleCharacters} visible={visibleCharacters}");
        ClientLogService.Instance.Info($"people.grid.rows count={visibleCharacters}");
        RestoreSelection(Characters, SelectedCharacterId, value => SelectedCharacterId = value);
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
            long.TryParse(S(money, "Platinum"), out l); Platinum = l;
            long.TryParse(S(money, "Orichalcum"), out l); Orichalcum = l;
            long.TryParse(S(money, "Adamant"), out l); Adamant = l;
            long.TryParse(S(money, "Sovereign"), out l); Sovereign = l;
            long.TryParse(S(money, "ExperienceCoins"), out l); ExperienceCoins = l;
            ClientLogService.Instance.Debug($"ui-refresh section=Персонажи block=Финансы loadedCurrencies={money.Count}");
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
                ReputationRows.Add($"{S(m, "scope")}:{S(m, "groupKey")}={S(m, "value")}");

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
        ClientLogService.Instance.Debug($"ui-refresh section=Модерация block=Заявки raw={ToList(r.Payload["items"]).Count} shown={PendingRequests.Count}");
        RestoreSelection(PendingRequests, SelectedPendingRequestId, value => SelectedPendingRequestId = value);
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
        ClientLogService.Instance.Debug("dice.feed.refresh requested");
        var feed = _api.DiceVisibleFeed();
        if (feed.Status == ResponseStatus.Ok && feed.Payload.ContainsKey("items"))
        {
            var rawItems = ToList(feed.Payload["items"]);
            ClientLogService.Instance.Debug($"dice.feed.refresh itemsRaw={rawItems.Count}");
            var mappedItems = 0;
            foreach (var obj in rawItems)
            {
                var m = AsMap(obj);
                if (m == null) continue;
                mappedItems++;
                var total = "?";
                if (m.ContainsKey("result"))
                {
                    var result = AsMap(m["result"]);
                    if (result != null) total = FirstNonEmpty(S(result, "total"), "?");
                }
                var creator = FirstNonEmpty(S(m, "creatorLogin"), S(m, "creatorUserId"));
                var isTest = string.Equals(S(m, "isTestRoll"), "True", StringComparison.OrdinalIgnoreCase);
                var label = isTest ? "[ТЕСТ] " : string.Empty;
                var rolls = BuildDiceRollDetails(m, CommandNames.DiceVisibleFeed);
                DiceFeedRows.Add($"{creator} | {label}{S(m, "formula")} = {total}{rolls} | {S(m, "visibility")}");
            }
            ClientLogService.Instance.Debug($"dice.feed.refresh itemsMapped={mappedItems}");
        }
        ClientLogService.Instance.Debug($"dice.feed.render visibleRows={DiceFeedRows.Count}");
        MergeDiceIntoChatFeed();
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
        CombatParticipantRows.Clear();
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
                {
                    var participantId = S(m, "participantId");
                    var displayName = S(m, "displayName");
                    var kind = S(m, "kind");
                    var extra = $"roll={S(m, "baseRoll")} • st={S(m, "status")}";
                    CombatRows.Add($"P:{participantId} {displayName} {kind} {extra}");
                    CombatParticipantRows.Add(new RowVm { Id = participantId, Name = displayName, State = kind, Extra = extra });
                }
            }
        }
        RestoreSelection(CombatParticipantRows, SelectedCombatParticipantId, value => SelectedCombatParticipantId = value);

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
        var r = EnsureSuccess(_api.DefinitionsReload());
        DefinitionVersionText = FirstNonEmpty(S(r.Payload, "version"), DefinitionVersionText);
        Notify(nameof(DefinitionVersionText));
    }

    private void RefreshDefinitionClasses()
    {
        ClassDefinitionRows.Clear();
        var response = EnsureSuccess(_api.DefinitionsClassesGet(true));
        foreach (var item in ToList(response.Payload.ContainsKey("items") ? response.Payload["items"] : new ArrayList()))
        {
            if (item is not Dictionary<string, object> map) continue;
            ClassDefinitionRows.Add(new RowVm
            {
                Id = S(map, "code"),
                Name = FirstNonEmpty(S(map, "name"), S(map, "code")),
                State = $"lvl={S(map, "level")} • {S(map, "status")}",
                Extra = $"branch={S(map, "branchCode")} • direction={S(map, "directionCode")} • active={S(map, "isActive")}"
            });
        }
        ClientLogService.Instance.Debug($"ui-refresh section=Контент block=Классы loaded={ClassDefinitionRows.Count} visible={FilteredClassDefinitionRows.Count()}");
        RestoreSelection(ClassDefinitionRows, SelectedClassDefinitionCode, value => SelectedClassDefinitionCode = value);
        Notify(nameof(ContentSummary));
        Notify(nameof(SelectedClassDefinition));
        Notify(nameof(SelectedClassSummary));
        Notify(nameof(SelectedContentSummary));
    }

    private void OpenSelectedClassDefinition()
    {
        if (string.IsNullOrWhiteSpace(SelectedClassDefinitionCode)) return;
        var response = EnsureSuccess(_api.DefinitionClassGet(SelectedClassDefinitionCode));
        if (!response.Payload.TryGetValue("item", out var item) || item is not Dictionary<string, object> map) return;
        ApplyClassDefinitionEditor(map);
    }

    private void NewClassDefinition()
    {
        SelectedClassDefinitionCode = string.Empty;
        EditClassCode = string.Empty;
        EditClassName = string.Empty;
        EditClassDescription = string.Empty;
        EditClassDirectionCode = string.Empty;
        EditClassBranchCode = string.Empty;
        EditClassRootClassCode = string.Empty;
        EditClassParentClassCode = string.Empty;
        EditClassLevel = 1;
        EditClassGrantedSkillCodes = string.Empty;
        EditClassRequiredClassCodes = string.Empty;
        EditClassIsActive = true;
        EditClassStatus = DefinitionStatus.Draft.ToString();
        NotifyClassDefinitionEditor();
    }

    private void SaveClassDefinition()
    {
        var response = EnsureSuccess(_api.DefinitionClassSave(BuildClassDefinitionPayload()));
        if (response.Payload.TryGetValue("item", out var item) && item is Dictionary<string, object> map)
        {
            ApplyClassDefinitionEditor(map);
        }
        RefreshDefinitionClasses();
    }

    private void ArchiveClassDefinition()
    {
        var code = FirstNonEmpty(SelectedClassDefinitionCode, EditClassCode);
        if (string.IsNullOrWhiteSpace(code)) return;
        EnsureSuccess(_api.DefinitionClassArchive(code));
        RefreshDefinitionClasses();
        if (string.Equals(EditClassCode, code, StringComparison.OrdinalIgnoreCase))
        {
            OpenSelectedClassDefinition();
        }
    }

    private void RefreshDefinitionSkills()
    {
        SkillDefinitionRows.Clear();
        var response = EnsureSuccess(_api.DefinitionsSkillsGet(true));
        foreach (var item in ToList(response.Payload.ContainsKey("items") ? response.Payload["items"] : new ArrayList()))
        {
            if (item is not Dictionary<string, object> map) continue;
            SkillDefinitionRows.Add(new RowVm
            {
                Id = S(map, "code"),
                Name = FirstNonEmpty(S(map, "name"), S(map, "code")),
                State = $"tier={S(map, "tier")} • {S(map, "status")}",
                Extra = $"maxLevel={S(map, "maxLevel")} • category={S(map, "skillCategory")} • active={S(map, "isActive")}"
            });
        }
        ClientLogService.Instance.Debug($"ui-refresh section=Контент block=Навыки loaded={SkillDefinitionRows.Count} visible={FilteredSkillDefinitionRows.Count()}");
        RestoreSelection(SkillDefinitionRows, SelectedSkillDefinitionCode, value => SelectedSkillDefinitionCode = value);
        Notify(nameof(ContentSummary));
        Notify(nameof(SelectedSkillDefinition));
        Notify(nameof(SelectedSkillSummary));
        Notify(nameof(SelectedContentSummary));
    }

    private void OpenSelectedSkillDefinition()
    {
        if (string.IsNullOrWhiteSpace(SelectedSkillDefinitionCode)) return;
        var response = EnsureSuccess(_api.DefinitionSkillGet(SelectedSkillDefinitionCode));
        if (!response.Payload.TryGetValue("item", out var item) || item is not Dictionary<string, object> map) return;
        ApplySkillDefinitionEditor(map);
    }

    private void NewSkillDefinition()
    {
        SelectedSkillDefinitionCode = string.Empty;
        EditSkillCode = string.Empty;
        EditSkillName = string.Empty;
        EditSkillDescription = string.Empty;
        EditSkillTier = 1;
        EditSkillMaxLevel = 1;
        EditSkillCategory = SkillCategory.Undefined.ToString();
        EditSkillIsClassSkill = false;
        EditSkillRequiredClassCodes = string.Empty;
        EditSkillRequiredSkillCodes = string.Empty;
        EditSkillIsActive = true;
        EditSkillStatus = DefinitionStatus.Draft.ToString();
        SkillLevelEditorRows.Clear();
        SkillLevelEditorRows.Add(new SkillLevelEditorRowVm { Level = 1, Description = string.Empty });
        NotifySkillDefinitionEditor();
    }

    private void SaveSkillDefinition()
    {
        var response = EnsureSuccess(_api.DefinitionSkillSave(BuildSkillDefinitionPayload()));
        if (response.Payload.TryGetValue("item", out var item) && item is Dictionary<string, object> map)
        {
            ApplySkillDefinitionEditor(map);
        }
        RefreshDefinitionSkills();
    }

    private void ArchiveSkillDefinition()
    {
        var code = FirstNonEmpty(SelectedSkillDefinitionCode, EditSkillCode);
        if (string.IsNullOrWhiteSpace(code)) return;
        EnsureSuccess(_api.DefinitionSkillArchive(code));
        RefreshDefinitionSkills();
        if (string.Equals(EditSkillCode, code, StringComparison.OrdinalIgnoreCase))
        {
            OpenSelectedSkillDefinition();
        }
    }

    private void AddSkillLevel()
    {
        SkillLevelEditorRows.Add(new SkillLevelEditorRowVm { Level = SkillLevelEditorRows.Count + 1, Description = string.Empty });
        EditSkillMaxLevel = Math.Max(EditSkillMaxLevel, SkillLevelEditorRows.Count);
        Notify(nameof(EditSkillMaxLevel));
        Notify(nameof(SkillEditorHintText));
    }

    private void RemoveSkillLevel()
    {
        if (SkillLevelEditorRows.Count == 0) return;
        SkillLevelEditorRows.RemoveAt(SkillLevelEditorRows.Count - 1);
        for (var index = 0; index < SkillLevelEditorRows.Count; index++) SkillLevelEditorRows[index].Level = index + 1;
        EditSkillMaxLevel = Math.Max(1, SkillLevelEditorRows.Count);
        Notify(nameof(EditSkillMaxLevel));
        Notify(nameof(SkillEditorHintText));
    }

    private void LoadClassTree()
    {
        if (string.IsNullOrWhiteSpace(SelectedCharacterId)) return;
        ClassTreeItems.Clear();
        var tree = _api.ClassTreeGet(SelectedCharacterId);
        if (tree.Status == ResponseStatus.Ok)
        {
            DefinitionVersionText = S(tree.Payload, "definitionVersion");
            foreach (var d in ToList(tree.Payload.ContainsKey("directions") ? tree.Payload["directions"] : new ArrayList()))
            {
                if (d is not Dictionary<string, object> dm) continue;
                var directionId = S(dm, "directionId");
                var branchId = S(dm, "selectedBranchId");
                ClassTreeItems.Add(new RowVm { Id = directionId, Name = $"Direction {directionId}", State = "Branch", Extra = $"selectedBranch={branchId}" });
                foreach (var n in ToList(dm.ContainsKey("acquiredNodes") ? dm["acquiredNodes"] : new ArrayList()))
                    if (n is Dictionary<string, object> nm)
                        ClassTreeItems.Add(new RowVm { Id = S(nm, "nodeId"), Name = S(nm, "nodeId"), State = "Acquired", Extra = $"acquiredAt={S(nm, "acquiredAt")}" });
            }
        }

        var available = _api.ClassTreeAvailable(SelectedCharacterId);
        if (available.Status == ResponseStatus.Ok && available.Payload.ContainsKey("items"))
        {
            foreach (var d in ToList(available.Payload["items"]))
            {
                if (d is not Dictionary<string, object> dm) continue;
                if (S(dm, "available") == "True")
                    ClassTreeItems.Add(new RowVm { Id = S(dm, "nodeId"), Name = S(dm, "name"), State = "Available", Extra = $"nodeId={S(dm, "nodeId")}" });
            }
        }
        RestoreSelection(ClassTreeItems, SelectedClassNodeId, value => SelectedClassNodeId = value);
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
        SkillRows.Clear();
        var r = _api.SkillsList(SelectedCharacterId);
        if (r.Status != ResponseStatus.Ok || !r.Payload.ContainsKey("items")) return;
        foreach (var item in ToList(r.Payload["items"]))
        {
            if (item is not Dictionary<string, object> m) continue;
            SkillRows.Add(new RowVm
            {
                Id = S(m, "skillId"),
                Name = S(m, "name"),
                State = $"type={S(m, "type")}",
                Extra = $"acquired={S(m, "acquired")} • available={S(m, "available")} • reason={S(m, "reason")}"
            });
        }
        RestoreSelection(SkillRows, SelectedSkillId, value => SelectedSkillId = value);
    }

    private void AcquireSkill()
    {
        if (string.IsNullOrWhiteSpace(SelectedCharacterId) || string.IsNullOrWhiteSpace(SelectedSkillId)) return;
        _api.SkillsAcquire(SelectedCharacterId, SelectedSkillId);
        LoadSkills();
    }

    private void ApplyClassDefinitionEditor(Dictionary<string, object> map)
    {
        EditClassCode = S(map, "code");
        EditClassName = S(map, "name");
        EditClassDescription = S(map, "description");
        EditClassDirectionCode = S(map, "directionCode");
        EditClassBranchCode = S(map, "branchCode");
        EditClassRootClassCode = S(map, "rootClassCode");
        EditClassParentClassCode = S(map, "parentClassCode");
        EditClassLevel = ParseInt(S(map, "level"), 1);
        EditClassGrantedSkillCodes = string.Join(", ", ReadStringList(map, "grantedSkillCodes"));
        EditClassRequiredClassCodes = string.Join(", ", ReadStringList(map, "requiredClassCodes"));
        EditClassIsActive = ParseBool(S(map, "isActive"), true);
        EditClassStatus = FirstNonEmpty(S(map, "status"), DefinitionStatus.Draft.ToString());
        SelectedClassDefinitionCode = EditClassCode;
        NotifyClassDefinitionEditor();
    }

    private void ApplySkillDefinitionEditor(Dictionary<string, object> map)
    {
        EditSkillCode = S(map, "code");
        EditSkillName = S(map, "name");
        EditSkillDescription = S(map, "description");
        EditSkillTier = ParseInt(S(map, "tier"), 1);
        EditSkillMaxLevel = ParseInt(S(map, "maxLevel"), 1);
        EditSkillCategory = FirstNonEmpty(S(map, "skillCategory"), SkillCategory.Undefined.ToString());
        EditSkillIsClassSkill = ParseBool(S(map, "isClassSkill"), false);
        EditSkillRequiredClassCodes = string.Join(", ", ReadStringList(map, "requiredClassCodes"));
        EditSkillRequiredSkillCodes = string.Join(", ", ReadStringList(map, "requiredSkillCodes"));
        EditSkillIsActive = ParseBool(S(map, "isActive"), true);
        EditSkillStatus = FirstNonEmpty(S(map, "status"), DefinitionStatus.Draft.ToString());
        SkillLevelEditorRows.Clear();
        foreach (var level in ReadMapList(map, "levels"))
        {
            SkillLevelEditorRows.Add(new SkillLevelEditorRowVm
            {
                Level = ParseInt(S(level, "level"), SkillLevelEditorRows.Count + 1),
                Description = S(level, "description")
            });
        }
        if (SkillLevelEditorRows.Count == 0) SkillLevelEditorRows.Add(new SkillLevelEditorRowVm { Level = 1, Description = string.Empty });
        SelectedSkillDefinitionCode = EditSkillCode;
        NotifySkillDefinitionEditor();
    }

    private Dictionary<string, object> BuildClassDefinitionPayload()
    {
        return new Dictionary<string, object>
        {
            { "code", EditClassCode },
            { "name", EditClassName },
            { "description", EditClassDescription },
            { "directionCode", EditClassDirectionCode },
            { "branchCode", EditClassBranchCode },
            { "rootClassCode", EditClassRootClassCode },
            { "parentClassCode", EditClassParentClassCode },
            { "level", EditClassLevel },
            { "grantedSkillCodes", SplitCsv(EditClassGrantedSkillCodes).Cast<object>().ToArray() },
            { "requiredClassCodes", SplitCsv(EditClassRequiredClassCodes).Cast<object>().ToArray() },
            { "isActive", EditClassIsActive },
            { "status", EditClassStatus }
        };
    }

    private Dictionary<string, object> BuildSkillDefinitionPayload()
    {
        return new Dictionary<string, object>
        {
            { "code", EditSkillCode },
            { "name", EditSkillName },
            { "description", EditSkillDescription },
            { "tier", EditSkillTier },
            { "maxLevel", EditSkillMaxLevel },
            { "skillCategory", EditSkillCategory },
            { "isClassSkill", EditSkillIsClassSkill },
            { "requiredClassCodes", SplitCsv(EditSkillRequiredClassCodes).Cast<object>().ToArray() },
            { "requiredSkillCodes", SplitCsv(EditSkillRequiredSkillCodes).Cast<object>().ToArray() },
            { "levels", SkillLevelEditorRows.Select(level => new Dictionary<string, object>
                {
                    { "level", level.Level },
                    { "description", level.Description },
                    { "requirements", new object[0] },
                    { "effects", new object[0] }
                }).Cast<object>().ToArray() },
            { "isActive", EditSkillIsActive },
            { "status", EditSkillStatus }
        };
    }

    private void NotifyClassDefinitionEditor()
    {
        Notify(nameof(EditClassCode)); Notify(nameof(EditClassName)); Notify(nameof(EditClassDescription)); Notify(nameof(EditClassDirectionCode)); Notify(nameof(EditClassBranchCode)); Notify(nameof(EditClassRootClassCode)); Notify(nameof(EditClassParentClassCode)); Notify(nameof(EditClassLevel)); Notify(nameof(EditClassGrantedSkillCodes)); Notify(nameof(EditClassRequiredClassCodes)); Notify(nameof(EditClassIsActive)); Notify(nameof(EditClassStatus)); Notify(nameof(DefinitionHintText));
    }

    private void NotifySkillDefinitionEditor()
    {
        Notify(nameof(EditSkillCode)); Notify(nameof(EditSkillName)); Notify(nameof(EditSkillDescription)); Notify(nameof(EditSkillTier)); Notify(nameof(EditSkillMaxLevel)); Notify(nameof(EditSkillCategory)); Notify(nameof(EditSkillIsClassSkill)); Notify(nameof(EditSkillRequiredClassCodes)); Notify(nameof(EditSkillRequiredSkillCodes)); Notify(nameof(EditSkillIsActive)); Notify(nameof(EditSkillStatus)); Notify(nameof(SkillEditorHintText));
    }

    private static int ParseInt(string value, int fallback) => int.TryParse(value, out var parsed) ? parsed : fallback;
    private static bool ParseBool(string value, bool fallback) => bool.TryParse(value, out var parsed) ? parsed : fallback;
    private static List<string> SplitCsv(string value) => value.Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Select(item => item.Trim()).Where(item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    private static List<string> ReadStringList(Dictionary<string, object> map, string key) => ToList(map.ContainsKey(key) ? map[key] : new ArrayList()).Cast<object>().Select(item => Convert.ToString(item) ?? string.Empty).Where(item => !string.IsNullOrWhiteSpace(item)).ToList();
    private static List<Dictionary<string, object>> ReadMapList(Dictionary<string, object> map, string key) => ToList(map.ContainsKey(key) ? map[key] : new ArrayList()).OfType<Dictionary<string, object>>().ToList();
    private ResponseEnvelope EnsureSuccess(ResponseEnvelope response)
    {
        if (response.Status != ResponseStatus.Ok)
        {
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(response.Message) ? response.Status.ToString() : response.Message);
        }

        LastErrorMessage = string.Empty;
        return response;
    }

    private void ChatSend()
    {
        if (string.IsNullOrWhiteSpace(ChatMessageText)) return;
        var sessionId = ResolveChatSessionId();
        var serverChatType = MapChatTypeToServer(ChatMessageType);
        ClientLogService.Instance.Info($"Chat send requested: sessionId={sessionId}; command={CommandNames.ChatSend}; uiType={ChatMessageType}; serverType={serverChatType}");
        _api.ChatSend(sessionId, serverChatType, ChatMessageText);
        ChatMessageText = string.Empty;
        Notify(nameof(ChatMessageText));
        ChatRefresh();
    }

    private void ChatRefresh()
    {
        var sessionId = ResolveChatSessionId();
        TraceChatDiagnostic($"request command={CommandNames.ChatVisibleFeed} session={sessionId}");
        ChatRows.Clear();
        ChatMessageRows.Clear();
        var feed = _api.ChatVisibleFeed(sessionId, 80);
        var feedItems = ExtractChatItems(feed.Payload, out var sourceKey, out var payloadKeys, out var rawItemsType);
        TraceChatDiagnostic($"response command={CommandNames.ChatVisibleFeed} status={feed.Status} success={(feed.Status == ResponseStatus.Ok)} payloadKeys=[{payloadKeys}] sourceKey={sourceKey} rawItems={feedItems.Count} rawType={rawItemsType}");
        LogFirstChatItemShape(feedItems, CommandNames.ChatVisibleFeed);
        if (feed.Status == ResponseStatus.Ok)
        {
            var mappedCount = 0;
            var filteredCount = 0;
            foreach (var item in feedItems)
            {
                var m = AsMap(item, CommandNames.ChatVisibleFeed);
                if (m == null) continue;
                mappedCount++;
                var row = BuildChatMessageRow(m);
                if (row == null)
                {
                    filteredCount++;
                    continue;
                }

                ChatRows.Add($"{row.Sender}: {row.Text}");
                ChatMessageRows.Add(row);
            }
            TraceChatDiagnostic($"mapped command={CommandNames.ChatVisibleFeed} mappedItems={mappedCount} filteredOut={filteredCount} displayItems={ChatMessageRows.Count}");
        }
        else
        {
            TraceChatDiagnostic($"response-error command={CommandNames.ChatVisibleFeed} message={feed.Message}");
        }
        TraceChatDiagnostic($"collection command={CommandNames.ChatVisibleFeed} chatRows={ChatRows.Count} uiCollection=ChatMessageRows uiCount={ChatMessageRows.Count}");
        ClientLogService.Instance.Debug($"ui-refresh section=Сессия block=Чат loaded={ChatRows.Count} visible={ChatMessageRows.Count}");
        MergeDiceIntoChatFeed();

        var unread = _api.ChatUnreadGet(sessionId);
        ChatUnreadText = "Unread: " + S(unread.Payload, "count");
        Notify(nameof(ChatUnreadText));

        var slow = _api.ChatSlowModeGet(sessionId);
        ChatSlowPublicSeconds = int.TryParse(S(slow.Payload, "publicSeconds"), out var ps) ? ps : 0;
        ChatSlowHiddenSeconds = int.TryParse(S(slow.Payload, "hiddenToAdminsSeconds"), out var hs) ? hs : 0;
        ChatSlowAdminOnlySeconds = int.TryParse(S(slow.Payload, "adminOnlySeconds"), out var a) ? a : 0;
        Notify(nameof(ChatSlowPublicSeconds)); Notify(nameof(ChatSlowHiddenSeconds)); Notify(nameof(ChatSlowAdminOnlySeconds));

        ChatRestrictionRows.Clear();
        var restrictions = _api.ChatRestrictionsGet(sessionId);
        ChatRestrictionRows.Add("LockPlayers=" + S(restrictions.Payload, "lockPlayers"));
        foreach (var item in ToList(restrictions.Payload.ContainsKey("restrictions") ? restrictions.Payload["restrictions"] : new ArrayList()))
            if (AsMap(item) is { } m)
                ChatRestrictionRows.Add($"{S(m, "userId")} muted={S(m, "muted")} reason={S(m, "reason")}");
        TraceChatDiagnostic($"ui chatRows={ChatRows.Count} chatMessageRows={ChatMessageRows.Count} restrictionsRows={ChatRestrictionRows.Count}");
        RefreshConnectionSummary();
    }

    private void ChatMuteUser() { if (!string.IsNullOrWhiteSpace(ChatModerationUserId)) { _api.ChatRestrictionsMuteUser(ResolveChatSessionId(), ChatModerationUserId, ChatModerationReason); ChatRefresh(); } }
    private void ChatUnmuteUser() { if (!string.IsNullOrWhiteSpace(ChatModerationUserId)) { _api.ChatRestrictionsUnmuteUser(ResolveChatSessionId(), ChatModerationUserId); ChatRefresh(); } }
    private void ChatLockPlayers() { _api.ChatRestrictionsLockPlayers(ResolveChatSessionId()); ChatRefresh(); }
    private void ChatUnlockPlayers() { _api.ChatRestrictionsUnlockPlayers(ResolveChatSessionId()); ChatRefresh(); }

    private void ChatSetSlowMode()
    {
        _api.ChatSlowModeSet(ResolveChatSessionId(), ChatSlowPublicSeconds, ChatSlowHiddenSeconds, ChatSlowAdminOnlySeconds);
        ChatRefresh();
    }

    private string ResolveChatSessionId()
    {
        var sessionId = string.IsNullOrWhiteSpace(ChatSessionId) ? "default" : ChatSessionId.Trim();
        if (!string.Equals(ChatSessionId, sessionId, StringComparison.Ordinal))
        {
            ChatSessionId = sessionId;
            Notify(nameof(ChatSessionId));
        }

        return sessionId;
    }

    private void AudioRefresh()
    {
        var state = _api.AudioStateGet(AudioSessionId);
        var mode = S(state.Payload, "mode");
        var category = S(state.Payload, "category");
        var track = FirstNonEmpty(S(state.Payload, "trackName"), "не выбрано");
        var position = FirstNonEmpty(S(state.Payload, "positionSeconds"), "0");
        var playback = FirstNonEmpty(S(state.Payload, "playbackState"), "нет данных");
        AudioStateText = $"Режим: {mode}; Категория: {category}; Трек: {track}; Позиция: {position} сек.; Состояние: {playback}";
        ClientLogService.Instance.Info($"ui-audio-refresh section=Сессия stateLoaded=true tracksRaw={state.Payload.Count}");
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
        ReferenceItems.Clear();
        var r = _api.ReferenceList(ReferenceWorldId, ReferenceType);
        foreach (var item in ToList(r.Payload.ContainsKey("items") ? r.Payload["items"] : new ArrayList()))
            if (item is Dictionary<string, object> m)
                ReferenceItems.Add(new RowVm
                {
                    Id = S(m, "referenceId"),
                    Name = S(m, "displayName"),
                    State = S(m, "referenceType"),
                    Extra = $"key={S(m, "key")}"
                });
        RestoreSelection(ReferenceItems, ReferenceId, value => ReferenceId = value);
    }

    private void ReferenceCreate() { _api.ReferenceCreate(new Dictionary<string, object> { { "worldId", ReferenceWorldId }, { "referenceType", ReferenceType }, { "key", ReferenceKey }, { "displayName", ReferenceDisplayName }, { "dataJson", ReferenceDataJson } }); ReferenceRefresh(); }
    private void ReferenceUpdate() { if (!string.IsNullOrWhiteSpace(ReferenceId)) { _api.ReferenceUpdate(new Dictionary<string, object> { { "referenceId", ReferenceId }, { "displayName", ReferenceDisplayName }, { "dataJson", ReferenceDataJson } }); ReferenceRefresh(); } }
    private void ReferenceArchive() { if (!string.IsNullOrWhiteSpace(ReferenceId)) { _api.ReferenceArchive(ReferenceId); ReferenceRefresh(); } }

    private void BackupRefresh()
    {
        BackupItems.Clear();
        var r = _api.BackupList();
        foreach (var item in ToList(r.Payload.ContainsKey("items") ? r.Payload["items"] : new ArrayList()))
            if (item is Dictionary<string, object> m)
                BackupItems.Add(new RowVm { Id = S(m, "backupId"), Name = S(m, "label"), State = "Backup", Extra = S(m, "createdUtc") });
        RestoreSelection(BackupItems, SelectedBackupId, value => SelectedBackupId = value);
    }

    private void BackupCreate() { _api.BackupCreate(string.IsNullOrWhiteSpace(BackupLabel) ? "manual-backup" : BackupLabel); BackupRefresh(); }
    private void BackupRestore() { if (!string.IsNullOrWhiteSpace(SelectedBackupId)) { _api.BackupRestore(SelectedBackupId); BackupRefresh(); } }
    private void BackupExport() { if (!string.IsNullOrWhiteSpace(SelectedBackupId)) { _api.BackupExport(SelectedBackupId); } }

    private void DiagnosticsRefresh()
    {
        DiagnosticsItems.Clear();
        var s1 = _api.AdminServerStatus();
        DiagnosticsItems.Add(new RowVm { Id = "server-status", Name = "Server status", State = $"online={S(s1.Payload, "onlineUsers")}", Extra = $"utc={S(s1.Payload, "utcNow")}" });
        var s2 = _api.AdminSessionsList();
        DiagnosticsItems.Add(new RowVm { Id = "sessions", Name = "Sessions payload", State = "Count", Extra = ToList(s2.Payload.ContainsKey("items") ? s2.Payload["items"] : new ArrayList()).Count.ToString() });
        LoadLocksSummary();
        DiagnosticsItems.Add(new RowVm { Id = "locks", Name = "Locks", State = "Count", Extra = LocksCount.ToString() });
        RestoreSelection(DiagnosticsItems, SelectedDiagnosticsId, value => SelectedDiagnosticsId = value);
        RefreshConnectionSummary();
    }

    private void LoadLocksSummary()
    {
        LockRows.Clear();
        var locks = _api.AdminLocksList();
        var items = ToList(locks.Payload.ContainsKey("items") ? locks.Payload["items"] : new ArrayList());
        LocksCount = items.Count;
        foreach (var item in items)
        {
            if (item is not Dictionary<string, object> map) continue;
            var resourceId = FirstNonEmpty(S(map, "characterId"), S(map, "entityId"), S(map, "resourceId"), S(map, "lockId"));
            var owner = FirstNonEmpty(S(map, "ownerUserId"), S(map, "ownerId"), S(map, "login"), "unknown owner");
            var state = FirstNonEmpty(S(map, "lockType"), S(map, "scope"), S(map, "resourceType"), "lock");
            var extra = string.Join(" • ", new[]
            {
                FirstNonEmpty(S(map, "resourceName"), S(map, "displayName"), resourceId),
                FirstNonEmpty(S(map, "acquiredUtc"), S(map, "createdUtc"), S(map, "expiresUtc"))
            }.Where(value => !string.IsNullOrWhiteSpace(value)));
            LockRows.Add(new RowVm { Id = resourceId, Name = owner, State = state, Extra = extra });
        }
        Notify(nameof(FilteredLockRows));
        ClientLogService.Instance.Debug($"ui-refresh section=Люди block=Блокировки raw={items.Count} shown={LockRows.Count}");
        ClientLogService.Instance.Info($"people.grid.rows count={LockRows.Count}");
        ClientLogService.Instance.Debug("people.grid.render ok");
        RestoreSelection(LockRows, SelectedLockId, value => SelectedLockId = value);
    }

    private void ApproveRequest() { if (!string.IsNullOrWhiteSpace(SelectedPendingRequestId)) { RunUiAction("Одобрение заявки", () => { _api.ApproveRequest(SelectedPendingRequestId, RequestComment); RefreshModerationSection(); }); } }
    private void RejectRequest() { if (!string.IsNullOrWhiteSpace(SelectedPendingRequestId)) { RunUiAction("Отклонение заявки", () => { _api.RejectRequest(SelectedPendingRequestId, RequestComment); RefreshModerationSection(); }); } }
    private void AcquireLock() { if (string.IsNullOrWhiteSpace(SelectedCharacterId)) return; RunUiAction("Получение lock", () => { var r = _api.AcquireCharacterLock(SelectedCharacterId); LockStateText = r.Message; Notify(nameof(LockStateText)); LoadLocksSummary(); }); }
    private void ReleaseLock() { if (string.IsNullOrWhiteSpace(SelectedCharacterId)) return; RunUiAction("Снятие lock", () => { var r = _api.ReleaseCharacterLock(SelectedCharacterId); LockStateText = r.Message; Notify(nameof(LockStateText)); LoadLocksSummary(); }); }
    private void ForceUnlock() { if (string.IsNullOrWhiteSpace(SelectedCharacterId) && SelectedLock != null) { SelectedCharacterId = SelectedLock.Id; } if (string.IsNullOrWhiteSpace(SelectedCharacterId)) return; RunUiAction("Принудительное снятие lock", () => { var r = _api.ForceReleaseCharacterLock(SelectedCharacterId); LockStateText = r.Message; Notify(nameof(LockStateText)); LoadLocksSummary(); }); }
    private void SaveBasicInfo()
    {
        if (string.IsNullOrWhiteSpace(SelectedCharacterId)) return;
        RunUiAction("Сохранение основных данных персонажа", () =>
        {
            ClientLogService.Instance.Info("ui-action section=Персонажи action=SaveBasic");
            var response = _api.UpdateCharacterBasicInfo(new Dictionary<string, object>
            {
                { "characterId", SelectedCharacterId },
                { "name", EditName },
                { "race", EditRace },
                { "height", EditHeight },
                { "age", EditAge },
                { "backstory", EditBackstory }
            });
            ClientLogService.Instance.Info($"character.update.basic response={response.Status}:{response.Message}");
            EnsureSuccess(response);
            OpenCharacter();
        });
    }

    private void SaveStats()
    {
        if (string.IsNullOrWhiteSpace(SelectedCharacterId)) return;
        RunUiAction("Сохранение характеристик персонажа", () =>
        {
            ClientLogService.Instance.Info("ui-action section=Персонажи action=SaveStats");
            var response = _api.UpdateCharacterStats(new Dictionary<string, object>
            {
                { "characterId", SelectedCharacterId },
                { "health", Health },
                { "physicalArmor", PhysicalArmor },
                { "magicalArmor", MagicalArmor },
                { "morale", Morale },
                { "strength", Strength },
                { "dexterity", Dexterity },
                { "endurance", Endurance },
                { "wisdom", Wisdom },
                { "intellect", Intellect },
                { "charisma", Charisma }
            });
            ClientLogService.Instance.Info($"character.update.stats response={response.Status}:{response.Message}");
            EnsureSuccess(response);
            OpenCharacter();
        });
    }

    private void SaveMoney()
    {
        if (string.IsNullOrWhiteSpace(SelectedCharacterId)) return;
        RunUiAction("Сохранение денег персонажа", () =>
        {
            ClientLogService.Instance.Info("ui-action section=Персонажи action=SaveMoney");
            var response = _api.UpdateCharacterMoney(new Dictionary<string, object>
            {
                { "characterId", SelectedCharacterId },
                { "money", new Dictionary<string, object>
                    {
                        { "Iron", Iron }, { "Bronze", Bronze }, { "Silver", Silver }, { "Gold", Gold },
                        { "Platinum", Platinum }, { "Orichalcum", Orichalcum }, { "Adamant", Adamant },
                        { "Sovereign", Sovereign }
                    }
                }
            });
            ClientLogService.Instance.Info($"character.update.money response={response.Status}:{response.Message}");
            EnsureSuccess(response);
            OpenCharacter();
        });
    }

    private void SaveXpCoins()
    {
        if (string.IsNullOrWhiteSpace(SelectedCharacterId)) return;
        RunUiAction("Сохранение монет опыта", () =>
        {
            ClientLogService.Instance.Info("ui-action section=Персонажи action=SaveXpCoins");
            var response = _api.UpdateCharacterXpCoins(new Dictionary<string, object>
            {
                { "characterId", SelectedCharacterId },
                { "xpCoins", ExperienceCoins }
            });
            ClientLogService.Instance.Info($"character.update.xp response={response.Status}:{response.Message}");
            EnsureSuccess(response);
            OpenCharacter();
        });
    }
    private void ApproveSelected() { if (!string.IsNullOrWhiteSpace(SelectedPendingAccountId)) RunUiAction("Подтверждение аккаунта", () => { ClientLogService.Instance.Info($"admin.account.approve.requested accountId={SelectedPendingAccountId}"); _api.ApproveAccount(SelectedPendingAccountId); RefreshPeopleSection(); }); }
    private void ArchiveSelected() { if (!string.IsNullOrWhiteSpace(SelectedPendingAccountId)) RunUiAction("Архивация аккаунта", () => { _api.ArchiveAccount(SelectedPendingAccountId); RefreshPeopleSection(); }); }
    private void RejectSelectedAccount() { if (!string.IsNullOrWhiteSpace(SelectedPendingAccountId)) RunUiAction("Отклонение аккаунта", () => { ClientLogService.Instance.Info($"admin.account.reject.requested accountId={SelectedPendingAccountId}"); _api.RejectAccount(SelectedPendingAccountId); RefreshPeopleSection(); }); }
    private void BlockSelectedAccount() { if (!string.IsNullOrWhiteSpace(SelectedPendingAccountId)) RunUiAction("Блокировка аккаунта", () => { ClientLogService.Instance.Info($"admin.account.block.requested accountId={SelectedPendingAccountId}"); _api.BlockAccount(SelectedPendingAccountId); RefreshPeopleSection(); }); }
    private void UnblockSelectedAccount() { if (!string.IsNullOrWhiteSpace(SelectedPendingAccountId)) RunUiAction("Разблокировка аккаунта", () => { ClientLogService.Instance.Info($"admin.account.unblock.requested accountId={SelectedPendingAccountId}"); _api.UnblockAccount(SelectedPendingAccountId); RefreshPeopleSection(); }); }
    private void ResetSelectedPassword()
    {
        var accountId = !string.IsNullOrWhiteSpace(SelectedPendingAccountId) ? SelectedPendingAccountId : SelectedOwnerUserId;
        if (string.IsNullOrWhiteSpace(accountId)) return;
        RunUiAction("Сброс пароля аккаунта", () =>
        {
            ClientLogService.Instance.Info($"admin.account.resetPassword.requested accountId={accountId}");
            _api.ResetPassword(accountId, ResetPasswordText);
            RefreshPeopleSection();
        });
    }

    private void CreateCharacterForOwner()
    {
        if (string.IsNullOrWhiteSpace(SelectedOwnerUserId)) return;
        RunUiAction("Создание персонажа (админ)", () =>
        {
            var payload = new Dictionary<string, object>
            {
                { "ownerUserId", SelectedOwnerUserId },
                { "name", CreateCharacterName },
                { "race", CreateCharacterRace },
                { "backstory", CreateCharacterBackstory }
            };
            ClientLogService.Instance.Info($"character.admin.create.send ownerUserId={SelectedOwnerUserId} name={CreateCharacterName}");
            var response = _api.CreateCharacter(payload);
            ClientLogService.Instance.Info($"character.admin.create.response status={response.Status} message={response.Message}");
            EnsureSuccess(response);
            LoadOwnerCharacters();
            ClientLogService.Instance.Info($"character.admin.create.success ownerUserId={SelectedOwnerUserId} listCount={Characters.Count}");
        });
    }

    private void RollCharacterDice()
    {
        var availabilityReason = GetDiceRollAvailabilityReason();
        if (!string.IsNullOrWhiteSpace(availabilityReason))
        {
            ClientLogService.Instance.Warn($"ui.admin.dice.click.blocked reason={availabilityReason}");
            return;
        }
        RunUiAction("Бросок кубов (админ)", () =>
        {
            var formula = DiceCount + "d" + DiceFaces + (DiceModifier == 0 ? string.Empty : DiceModifier > 0 ? "+" + DiceModifier : DiceModifier.ToString());
            var actorLogin = FirstNonEmpty(LoginText, "unknown");
            ClientLogService.Instance.Info($"dice.roll.actor login={actorLogin} userId=unknown");
            if (string.Equals(DiceModeInput, "Тестовый", StringComparison.OrdinalIgnoreCase))
            {
                ClientLogService.Instance.Info($"dice.roll.test.send actor={actorLogin} formula={formula}");
                var response = _api.DiceRollTest(formula, DiceVisibilityInput, DiceDescriptionInput);
                ClientLogService.Instance.Info($"dice.roll.test.response status={response.Status} message={response.Message}");
                EnsureSuccess(response);
            }
            else
            {
                ClientLogService.Instance.Info($"dice.roll.standard.send actor={actorLogin} formula={formula}");
                var response = _api.DiceRollStandard(formula, DiceVisibilityInput, DiceDescriptionInput);
                ClientLogService.Instance.Info($"dice.roll.standard.response status={response.Status} message={response.Message}");
                EnsureSuccess(response);
            }

            var testState = _api.DiceTestGetCurrent();
            ClientLogService.Instance.Info($"dice.test.getCurrent.status={testState.Status}");
            LoadPendingRequests();
            LoadRequestHistory();
        });
    }

    private string GetDiceRollAvailabilityReason()
    {
        if (!ArePrivilegedSectionsEnabled) return "Требуется подключение и вход администратора.";
        if (IsBusy) return "Дождитесь завершения текущей операции.";
        if (DiceCount < 1) return "Количество кубиков должно быть не меньше 1.";
        if (DiceFaces < 2) return "Количество граней должно быть не меньше 2.";
        return string.Empty;
    }

    private void TraceDiceAvailability()
    {
        var reason = GetDiceRollAvailabilityReason();
        if (string.Equals(reason, _lastDiceAvailabilityReason, StringComparison.Ordinal))
        {
            return;
        }

        _lastDiceAvailabilityReason = reason;
        var state = string.IsNullOrWhiteSpace(reason) ? "enabled" : "disabled";
        ClientLogService.Instance.Info("dice.actor.mode=account");
        ClientLogService.Instance.Info($"ui.admin.dice.button state={state} reason={FirstNonEmpty(reason, "ready")}");
    }

    private void RefreshOverviewActivity()
    {
        OverviewActivityRows.Clear();
        OverviewActivityRows.Add(HasConnectionError ? $"Ошибка: {LastErrorMessage}" : LastStatusMessage);
        if (PendingRequests.Count > 0) OverviewActivityRows.Add($"Требуют решения: {PendingRequests[0].Name} / {PendingRequests[0].State}");
        if (PendingAccounts.Count > 0) OverviewActivityRows.Add($"Новый аккаунт: {PendingAccounts[0].Name}");
        if (DiceFeedRows.Count > 0) OverviewActivityRows.Add($"Последний бросок: {DiceFeedRows[0]}");
        if (ChatRows.Count > 0) OverviewActivityRows.Add($"Последнее сообщение: {ChatRows[0]}");
        if (DiagnosticsItems.Count > 0) OverviewActivityRows.Add($"Диагностика: {DiagnosticsItems[0].Name} / {DiagnosticsItems[0].Extra}");
        if (OverviewActivityRows.Count == 1 && string.IsNullOrWhiteSpace(OverviewActivityRows[0]))
        {
            OverviewActivityRows[0] = "Нет последних событий.";
        }
    }

    public void Shutdown()
    {
        SaveConnectionSettings();
        SaveWorkspaceLayout();
        ClientLogService.Instance.Info("Logout / shutdown requested from Admin client");
        _client.Disconnect();
    }

    private void NotifyAllEditor()
    {
        Notify(nameof(EditName)); Notify(nameof(EditRace)); Notify(nameof(EditHeight)); Notify(nameof(EditAge)); Notify(nameof(EditDescription)); Notify(nameof(EditBackstory));
        Notify(nameof(Health)); Notify(nameof(PhysicalArmor)); Notify(nameof(MagicalArmor)); Notify(nameof(Morale)); Notify(nameof(Strength)); Notify(nameof(Dexterity)); Notify(nameof(Endurance)); Notify(nameof(Wisdom)); Notify(nameof(Intellect)); Notify(nameof(Charisma));
        Notify(nameof(Iron)); Notify(nameof(Bronze)); Notify(nameof(Silver)); Notify(nameof(Gold)); Notify(nameof(Platinum)); Notify(nameof(Orichalcum)); Notify(nameof(Adamant)); Notify(nameof(Sovereign)); Notify(nameof(ExperienceCoins));
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

    private static string FirstNonEmpty(params string[] values) => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    private static IList ToList(object value) => value as IList ?? new ArrayList();
    private static IList ExtractChatItems(Dictionary<string, object> payload, out string sourceKey, out string payloadKeys, out string rawItemsType)
    {
        payloadKeys = string.Join(",", payload.Keys.OrderBy(x => x, StringComparer.Ordinal));
        foreach (var key in new[] { "items", "messages", "feed", "history" })
        {
            if (!payload.ContainsKey(key))
            {
                continue;
            }

            var normalized = NormalizePayloadList(payload[key], out rawItemsType);
            sourceKey = key;
            return normalized;
        }

        foreach (var entry in payload)
        {
            if (entry.Value is string)
            {
                continue;
            }

            if (entry.Value is IEnumerable)
            {
                var normalized = NormalizePayloadList(entry.Value, out rawItemsType);
                sourceKey = entry.Key;
                return normalized;
            }
        }

        sourceKey = "<none>";
        rawItemsType = "<none>";
        return new ArrayList();
    }

    private static IList NormalizePayloadList(object? payloadValue, out string rawItemsType)
    {
        rawItemsType = payloadValue?.GetType().Name ?? "null";
        if (payloadValue is IList list) return list;
        if (payloadValue is IEnumerable enumerable && payloadValue is not string) return enumerable.Cast<object>().ToArray();
        return new ArrayList();
    }

    private void TraceChatDiagnostic(string message)
    {
        var line = "[CHAT-DIAG][Admin] " + message;
        ClientLogService.Instance.Debug(line);
    }

    private Dictionary<string, object>? AsMap(object? value, string context)
    {
        if (value is Dictionary<string, object> typedMap)
        {
            TraceChatDiagnostic($"map-shape command={context} branch=Dictionary<string,object> count={typedMap.Count}");
            return typedMap;
        }

        if (value is IDictionary dictionary)
        {
            var map = new Dictionary<string, object>();
            foreach (DictionaryEntry entry in dictionary)
            {
                var key = Convert.ToString(entry.Key);
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                map[key] = entry.Value;
            }

            TraceChatDiagnostic($"map-shape command={context} branch=IDictionary count={map.Count}");
            return map.Count > 0 ? map : null;
        }

        if (value is object[] objectArray)
        {
            if (TryConvertObjectArrayToMap(objectArray, out var objectArrayMap))
            {
                TraceChatDiagnostic($"map-shape command={context} branch=object[] count={objectArrayMap.Count}");
                return objectArrayMap;
            }

            TraceChatDiagnostic($"map-shape command={context} branch=object[] fallback=failed length={objectArray.Length}");
            return null;
        }

        if (value is IEnumerable enumerable && value is not string)
        {
            if (TryConvertEnumerableToMap(enumerable, out var enumerableMap))
            {
                TraceChatDiagnostic($"map-shape command={context} branch=IEnumerable count={enumerableMap.Count}");
                return enumerableMap;
            }

            TraceChatDiagnostic($"map-shape command={context} branch=IEnumerable fallback=failed type={value.GetType().FullName}");
            return null;
        }

        TraceChatDiagnostic($"map-shape command={context} branch=unsupported type={value?.GetType().FullName ?? "null"}");
        return null;
    }

    private Dictionary<string, object>? AsMap(object? value)
    {
        return AsMap(value, "generic");
    }

    private void LogFirstChatItemShape(IList items, string command)
    {
        if (items.Count == 0)
        {
            TraceChatDiagnostic($"first-item command={command} type=<none>");
            return;
        }

        var firstItem = items[0];
        var firstType = firstItem?.GetType().FullName ?? "null";
        TraceChatDiagnostic($"first-item command={command} type={firstType}");

        if (firstItem is IEnumerable enumerable && firstItem is not string)
        {
            var innerTypes = enumerable
                .Cast<object?>()
                .Take(6)
                .Select(item => item?.GetType().FullName ?? "null")
                .ToArray();
            TraceChatDiagnostic($"first-item-inner command={command} sampleTypes=[{string.Join(",", innerTypes)}]");
        }

        if (TryConvertPairLike(firstItem, out var key, out _, out var pairShape))
        {
            TraceChatDiagnostic($"first-item-pair command={command} shape={pairShape} key={key}");
        }
    }

    private static bool TryConvertObjectArrayToMap(object[] source, out Dictionary<string, object> map)
    {
        map = new Dictionary<string, object>(StringComparer.Ordinal);
        if (source.Length == 0)
        {
            return false;
        }

        var asPairs = true;
        foreach (var item in source)
        {
            if (!TryConvertPairLike(item, out var key, out var value, out _))
            {
                asPairs = false;
                break;
            }

            if (!string.IsNullOrWhiteSpace(key))
            {
                map[key] = value;
            }
        }

        if (asPairs && map.Count > 0)
        {
            return true;
        }

        if (source.Length % 2 != 0)
        {
            return false;
        }

        map.Clear();
        for (var i = 0; i < source.Length; i += 2)
        {
            var key = Convert.ToString(source[i]);
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            map[key] = source[i + 1];
        }

        return map.Count > 0;
    }

    private static bool TryConvertEnumerableToMap(IEnumerable source, out Dictionary<string, object> map)
    {
        map = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var item in source)
        {
            if (!TryConvertPairLike(item, out var key, out var value, out _))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(key))
            {
                map[key] = value;
            }
        }

        return map.Count > 0;
    }

    private static bool TryConvertPairLike(object? value, out string key, out object? mappedValue, out string shape)
    {
        key = string.Empty;
        mappedValue = null;
        shape = "unknown";

        if (value is DictionaryEntry dictionaryEntry)
        {
            key = Convert.ToString(dictionaryEntry.Key) ?? string.Empty;
            mappedValue = dictionaryEntry.Value;
            shape = "DictionaryEntry";
            return !string.IsNullOrWhiteSpace(key);
        }

        if (value is object[] objectPair && objectPair.Length == 2)
        {
            key = Convert.ToString(objectPair[0]) ?? string.Empty;
            mappedValue = objectPair[1];
            shape = "object[2]";
            return !string.IsNullOrWhiteSpace(key);
        }

        if (value is IList listPair && listPair.Count == 2)
        {
            key = Convert.ToString(listPair[0]) ?? string.Empty;
            mappedValue = listPair[1];
            shape = "IList[2]";
            return !string.IsNullOrWhiteSpace(key);
        }

        if (value != null)
        {
            var valueType = value.GetType();
            var keyProperty = valueType.GetProperty("Key");
            var valueProperty = valueType.GetProperty("Value");
            if (keyProperty != null && valueProperty != null)
            {
                key = Convert.ToString(keyProperty.GetValue(value)) ?? string.Empty;
                mappedValue = valueProperty.GetValue(value);
                shape = valueType.FullName ?? valueType.Name;
                return !string.IsNullOrWhiteSpace(key);
            }
        }

        return false;
    }

    private ChatMessageRowVm? BuildChatMessageRow(Dictionary<string, object> map)
    {
        var sender = FirstNonEmpty(S(map, "senderDisplayName"), S(map, "senderUserId"), "Система");
        var text = FirstNonEmpty(S(map, "text"), S(map, "message"), S(map, "body"));
        var type = FirstNonEmpty(S(map, "type"), "Public");
        var createdRaw = FirstNonEmpty(S(map, "createdUtc"), S(map, "createdAt"), S(map, "at"));
        var timestamp = FormatChatTimestamp(createdRaw);

        if (string.IsNullOrWhiteSpace(text))
        {
            TraceChatDiagnostic("chat-filter reason=empty-text");
            return null;
        }

        if (IsPlaceholderText(text))
        {
            TraceChatDiagnostic($"chat-filter reason=placeholder-text value={text}");
            return null;
        }

        return new ChatMessageRowVm
        {
            Sender = sender,
            Text = text,
            Timestamp = timestamp,
            IsSystem = string.Equals(type, "System", StringComparison.OrdinalIgnoreCase)
        };
    }

    private void MergeDiceIntoChatFeed()
    {
        var merged = 0;
        foreach (var row in DiceFeedRows)
        {
            if (IsPlaceholderText(row) || ChatRows.Contains(row))
            {
                continue;
            }

            ChatRows.Add(row);
            ChatMessageRows.Add(new ChatMessageRowVm
            {
                Sender = "Dice",
                Text = row,
                Timestamp = string.Empty,
                IsSystem = true
            });
            merged++;
        }

        ClientLogService.Instance.Info($"gameFeed diceMerged={merged}");
    }

    private string BuildDiceRollDetails(Dictionary<string, object> map, string context)
    {
        if (!map.TryGetValue("result", out var rawResult)) return string.Empty;
        var result = AsMap(rawResult, context);
        if (result == null || !result.TryGetValue("rolls", out var rawRolls)) return string.Empty;
        var values = ToList(rawRolls)
            .Cast<object>()
            .Select(value => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();
        if (values.Length == 0) return string.Empty;

        var rolled = string.Join(",", values);
        var modifier = 0;
        if (result.TryGetValue("modifier", out var rawModifier))
            int.TryParse(Convert.ToString(rawModifier, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out modifier);
        if (modifier == 0) return $" ({rolled})";
        return modifier > 0 ? $" ({rolled}+{modifier})" : $" ({rolled}{modifier})";
    }

    private static bool IsPlaceholderText(string text)
    {
        return string.Equals(text, "Нет сообщений", StringComparison.OrdinalIgnoreCase)
               || string.Equals(text, "Нет системных событий", StringComparison.OrdinalIgnoreCase)
               || string.Equals(text, "Нет видимых бросков", StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatChatTimestamp(string rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return string.Empty;
        }

        if (TryParseServerTimestamp(rawValue, out var parsed))
        {
            var local = parsed.ToLocalTime();
            return local.Date == DateTime.Now.Date
                ? local.ToString("HH:mm", CultureInfo.InvariantCulture)
                : local.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);
        }

        return rawValue;
    }

    private static bool TryParseServerTimestamp(string rawValue, out DateTime utcValue)
    {
        utcValue = default;
        var dateMatch = Regex.Match(rawValue, @"^/Date\(([-+]?\d+)");
        if (dateMatch.Success && long.TryParse(dateMatch.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var milliseconds))
        {
            utcValue = DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).UtcDateTime;
            return true;
        }

        if (DateTime.TryParse(rawValue, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
        {
            utcValue = DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
            return true;
        }

        return false;
    }

    private static string MapChatTypeToServer(string uiType)
    {
        return uiType switch
        {
            "Скрытый" => "HiddenToAdmins",
            "Только для админов" => "AdminOnly",
            _ => "Public"
        };
    }
    private static string S(Dictionary<string, object> map, string key) => map.ContainsKey(key) && map[key] != null ? Convert.ToString(map[key]) ?? string.Empty : string.Empty;
}

public class CombatTrackerViewModel : AdminMainViewModel { }
