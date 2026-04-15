using Nri.PlayerClient.Diagnostics;
using Nri.PlayerClient.Networking;
using Nri.Shared.Configuration;
using Nri.Shared.Contracts;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Input;
using System.Windows.Threading;

namespace Nri.PlayerClient.ViewModels;

public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void Notify([CallerMemberName] string? p = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
}

public sealed class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    public RelayCommand(Action execute) : this(_ => execute()) { }
    public RelayCommand(Action<object?> execute) { _execute = execute; }
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => _execute(parameter);
}

public class CharacterListItemVm : ViewModelBase
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Race { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Age { get; set; } = string.Empty;
    public bool Archived { get; set; }
    public bool IsActive { get; set; }
}

public class CurrencyRowVm
{
    public string Name { get; set; } = string.Empty;
    public string Abbrev { get; set; } = string.Empty;
    public string Color { get; set; } = "#FFFFFF";
    public long Amount { get; set; }
}

public class StatRowVm
{
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public class ReputationRowVm
{
    public string Label { get; set; } = string.Empty;
    public int Value { get; set; }
    public double Percent => (Value + 100) / 200.0 * 100.0;
}

public class CompanionVm : ViewModelBase
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Species { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string NameDisplay => string.IsNullOrWhiteSpace(Name) ? "Безымянный компаньон" : Name;
    public string SpeciesDisplay => string.IsNullOrWhiteSpace(Species) ? "Не указано" : Species;
    public string NotesDisplay => string.IsNullOrWhiteSpace(Notes) ? "Описание компаньона не загружено" : Notes;
    public ObservableCollection<StatRowVm> StatsRows { get; } = new ObservableCollection<StatRowVm>();
    public ObservableCollection<StatRowVm> CoreStatRows { get; } = new ObservableCollection<StatRowVm>();
    public ObservableCollection<StatRowVm> AttributeStatRows { get; } = new ObservableCollection<StatRowVm>();
    public ObservableCollection<string> InventoryRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<string> HoldingsRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<string> SkillsRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<string> ClassRows { get; } = new ObservableCollection<string>();
}


public class ClassDirectionVm
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
}

public class ClassBranchVm
{
    public string Key { get; set; } = string.Empty;
    public string DirectionKey { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public class ClassEntryVm
{
    public string NodeId { get; set; } = string.Empty;
    public string BranchKey { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public class PublicProfileFieldVm
{
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public class GameFeedItemVm
{
    public string Kind { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public bool IsMuted { get; set; }
}

public class ChatMessageRowVm
{
    public string Sender { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string Timestamp { get; set; } = string.Empty;
    public bool IsSystem { get; set; }
}

public class ClassNodeVisualVm
{
    public string NodeId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string State { get; set; } = "Locked";
    public double X { get; set; }
    public double Y { get; set; }
}

public class PlayerMainViewModel : ViewModelBase
{
    private readonly ClientSessionState _session = new ClientSessionState();
    private readonly ClientConfig _clientConfig;
    private readonly JsonTcpClient _client;
    private readonly CommandApi _api;
    private readonly DispatcherTimer _poller;

    private string _connectionState = "Оффлайн";
    private bool _isAuthPopupOpen;
    private bool _isConnectionPopupOpen;
    private string _selectedMainTab = "MyCharacters";
    private CompanionVm? _selectedCompanion;
    private string _selectedClassDirectionKey = "defender";
    private ClassBranchVm? _selectedClassBranch;
    private ClassEntryVm? _selectedClassEntry;

    public PlayerMainViewModel()
    {
        _clientConfig = App.ClientConfig;
        _client = new JsonTcpClient(_clientConfig, _session);
        _api = new CommandApi(_client);
        ClientLogService.Instance.Info("PlayerMainViewModel initialized");

        ToggleAuthPopupCommand = new RelayCommand(() => IsAuthPopupOpen = !IsAuthPopupOpen);
        ToggleConnectionPopupCommand = new RelayCommand(() => IsConnectionPopupOpen = !IsConnectionPopupOpen);
        LoginCommand = new RelayCommand(Login);
        RegisterCommand = new RelayCommand(Register);
        RefreshCommand = new RelayCommand(RefreshAll);

        LoadCharacterDetailsCommand = new RelayCommand(LoadSelectedCharacterDetails);
        CreateDiceRequestCommand = new RelayCommand(CreateDiceRequest);
        CancelRequestCommand = new RelayCommand(CancelRequest);

        ChatSendCommand = new RelayCommand(SendChat);
        BottomRefreshCommand = new RelayCommand(RefreshBottomPanel);

        AudioApplyLocalSettingsCommand = new RelayCommand(ApplyAudioLocalSettings);

        VisibilityLoadCommand = new RelayCommand(LoadVisibility);
        VisibilitySaveCommand = new RelayCommand(SaveVisibility);
        PublicCharacterLoadCommand = new RelayCommand(LoadPublicCharacter);

        NotesRefreshCommand = new RelayCommand(RefreshNotes);
        NotesCreateCommand = new RelayCommand(CreateNote);
        NotesArchiveCommand = new RelayCommand(ArchiveNote);

        AcquireClassNodeCommand = new RelayCommand(AcquireClassNode);
        AcquireSkillCommand = new RelayCommand(AcquireSkill);
        ConnectToServerCommand = new RelayCommand(ConnectToServer);
        ApplyConnectionSettingsCommand = new RelayCommand(ApplyConnectionSettings);
        ResetConnectionDefaultsCommand = new RelayCommand(ResetConnectionDefaults);
        UseLastConnectionCommand = new RelayCommand(UseSavedConnectionSettings);

        _poller = new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
        _poller.Tick += (_, _) => PollRefresh();

        LoadConnectionSettings();
        InitializeClassVisualLayout();
        InitializeDefaultPublicProfile();
        InitializeDefaultCharacterScaffolding();
        LoadLocalAudioSettings();
        RefreshConnectionSummary();
    }

    public string LoginText { get; set; } = string.Empty;
    public string PasswordText { get; set; } = string.Empty;
    public string PlayerDisplayName { get; set; } = "Гость";
    public string SessionSummary { get; set; } = "Сессия: default";

    public bool IsAuthPopupOpen { get => _isAuthPopupOpen; set { _isAuthPopupOpen = value; Notify(); } }
    public bool IsConnectionPopupOpen { get => _isConnectionPopupOpen; set { _isConnectionPopupOpen = value; Notify(); } }
    public string ConnectionState { get => _connectionState; set { _connectionState = value; Notify(); Notify(nameof(IsOnline)); Notify(nameof(IsAuthenticated)); } }
    public bool IsOnline => string.Equals(ConnectionState, "Онлайн", StringComparison.OrdinalIgnoreCase);
    public bool IsAuthenticated => IsOnline && !string.Equals(PlayerDisplayName, "Гость", StringComparison.OrdinalIgnoreCase);

    public string SelectedMainTab { get => _selectedMainTab; set { _selectedMainTab = value; Notify(); } }
    public string SelectedCharacterId { get; set; } = string.Empty;
    public string PublicViewCharacterId { get; set; } = string.Empty;
    public string ServerHostInput { get; set; } = "127.0.0.1";
    public string ServerPortInput { get; set; } = "4600";
    public string LastServerHost { get; set; } = "127.0.0.1";
    public int LastServerPort { get; set; } = 4600;
    public string ConnectionStatusDetail { get; set; } = "Не подключено";
    public string ConnectedEndpointDisplay => $"{ServerHostInput}:{ServerPortInput}";
    public string SelectedClassDirectionKey
    {
        get => _selectedClassDirectionKey;
        set
        {
            if (_selectedClassDirectionKey == value) return;
            _selectedClassDirectionKey = value;
            Notify();
            RebuildClassNavigation();
        }
    }

    public string CharacterName { get; set; } = string.Empty;
    public string CharacterRace { get; set; } = string.Empty;
    public string CharacterAge { get; set; } = string.Empty;
    public string CharacterHeight { get; set; } = string.Empty;
    public string CharacterDescription { get; set; } = string.Empty;
    public string CharacterBackstory { get; set; } = string.Empty;

    public string CharacterNameDisplay => string.IsNullOrWhiteSpace(CharacterName) ? "Без имени" : CharacterName;
    public string CharacterRaceDisplay => string.IsNullOrWhiteSpace(CharacterRace) ? "Не указано" : CharacterRace;
    public string CharacterAgeDisplay => string.IsNullOrWhiteSpace(CharacterAge) ? "0" : CharacterAge;
    public string CharacterHeightDisplay => string.IsNullOrWhiteSpace(CharacterHeight) ? "0" : CharacterHeight;
    public string CharacterDescriptionDisplay => string.IsNullOrWhiteSpace(CharacterDescription) ? "Описание не загружено" : CharacterDescription;
    public string CharacterBackstoryDisplay => string.IsNullOrWhiteSpace(CharacterBackstory) ? "Предыстория не загружена" : CharacterBackstory;

    public bool VisHideDescription { get; set; }
    public bool VisHideBackstory { get; set; }
    public bool VisHideStats { get; set; }
    public bool VisHideReputation { get; set; }

    public int DiceCount { get; set; } = 1;
    public int DiceFaces { get; set; } = 20;
    public int DiceModifier { get; set; }
    public string DiceVisibilityInput { get; set; } = "Общее";
    public string DiceDescriptionInput { get; set; } = string.Empty;
    public string SelectedRequestId { get; set; } = string.Empty;

    public string ChatSessionId { get; set; } = "default";
    public string ChatTypeInput { get; set; } = "Общее";
    public string ChatTextInput { get; set; } = string.Empty;

    public string AudioSessionId { get; set; } = "default";
    public string AudioStateText { get; set; } = string.Empty;
    public double LocalVolume { get; set; } = 0.7;
    public bool LocalMuted { get; set; }

    public string NoteSessionId { get; set; } = "default";
    public string NoteTargetType { get; set; } = "character";
    public string NoteTargetId { get; set; } = string.Empty;
    public string NoteTitle { get; set; } = string.Empty;
    public string NoteText { get; set; } = string.Empty;
    public string NoteVisibility { get; set; } = "Personal";
    public string SelectedNoteId { get; set; } = string.Empty;

    public string SelectedClassNodeId { get; set; } = string.Empty;
    public string SelectedSkillId { get; set; } = string.Empty;

    public ObservableCollection<CharacterListItemVm> MyCharacters { get; } = new ObservableCollection<CharacterListItemVm>();
    public ObservableCollection<StatRowVm> StatsRows { get; } = new ObservableCollection<StatRowVm>();
    public ObservableCollection<StatRowVm> CoreStatRows { get; } = new ObservableCollection<StatRowVm>();
    public ObservableCollection<StatRowVm> AttributeStatRows { get; } = new ObservableCollection<StatRowVm>();
    public ObservableCollection<CurrencyRowVm> MoneyRows { get; } = new ObservableCollection<CurrencyRowVm>();
    public ObservableCollection<string> InventoryRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<string> HoldingsRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<ReputationRowVm> ReputationRows { get; } = new ObservableCollection<ReputationRowVm>();
    public ObservableCollection<CompanionVm> Companions { get; } = new ObservableCollection<CompanionVm>();
    public CompanionVm? SelectedCompanion
    {
        get => _selectedCompanion;
        set { _selectedCompanion = value; Notify(); }
    }

    public ObservableCollection<string> SkillRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<string> SkillCatalogRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<ClassNodeVisualVm> ClassNodes { get; } = new ObservableCollection<ClassNodeVisualVm>();
    public ObservableCollection<ClassDirectionVm> ClassDirections { get; } = new ObservableCollection<ClassDirectionVm>();
    public ObservableCollection<ClassBranchVm> ClassBranches { get; } = new ObservableCollection<ClassBranchVm>();
    public ObservableCollection<ClassEntryVm> ClassEntries { get; } = new ObservableCollection<ClassEntryVm>();
    public ClassBranchVm? SelectedClassBranch
    {
        get => _selectedClassBranch;
        set
        {
            _selectedClassBranch = value;
            Notify();
            RebuildClassEntries();
        }
    }
    public ClassEntryVm? SelectedClassEntry
    {
        get => _selectedClassEntry;
        set { _selectedClassEntry = value; Notify(); NotifyClassDetail(); }
    }

    public ObservableCollection<string> ChatRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<ChatMessageRowVm> ChatMessageRows { get; } = new ObservableCollection<ChatMessageRowVm>();
    public ObservableCollection<string> EventRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<string> DiceFeedRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<string> RequestRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<string> SessionStateRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<GameFeedItemVm> GameFeedRows { get; } = new ObservableCollection<GameFeedItemVm>();

    public ObservableCollection<string> PublicCharacterRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<PublicProfileFieldVm> PublicProfileIdentityRows { get; } = new ObservableCollection<PublicProfileFieldVm>();
    public ObservableCollection<PublicProfileFieldVm> PublicProfileSummaryRows { get; } = new ObservableCollection<PublicProfileFieldVm>();
    public ObservableCollection<string> PublicProfileHiddenRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<string> NoteRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<string> AdminNoteRows { get; } = new ObservableCollection<string>();

    public ObservableCollection<string> DiceVisibilityOptions { get; } = new ObservableCollection<string> { "Общее", "Только мастеру", "Теневой" };
    public ObservableCollection<string> ChatTypeOptions { get; } = new ObservableCollection<string> { "Общее", "Скрытое админам", "Только админам" };
    public ObservableCollection<string> NoteTargetTypeOptions { get; } = new ObservableCollection<string> { "character", "session", "campaign" };
    public ObservableCollection<string> NoteVisibilityOptions { get; } = new ObservableCollection<string> { "Personal", "SharedWithOwner", "SessionShared" };

    public string SelectedClassDirectionDisplay => GetDirectionLabel(SelectedClassDirectionKey);
    public string SelectedClassBranchTitle => SelectedClassBranch?.Title ?? "Ветвь не выбрана";
    public string SelectedClassBranchSummary => SelectedClassBranch?.Summary ?? "Выберите направление, чтобы увидеть доступные ветви развития.";
    public string SelectedClassEntryTitle => SelectedClassEntry?.Title ?? "Класс не выбран";
    public string SelectedClassEntrySummary => SelectedClassEntry?.Summary ?? "Выберите ветвь и класс, чтобы открыть карточку развития.";
    public string SelectedClassEntryState => SelectedClassEntry?.Status ?? "Placeholder";
    public string SelectedClassEntryRequirements => SelectedClassEntry == null ? "Данные о требованиях ещё не загружены." : $"Статус узла: {SelectedClassEntry.Status}. Полные требования и бонусы будут подключены позже.";
    public bool HasClassBranches => ClassBranches.Count > 0;
    public bool HasClassEntries => ClassEntries.Count > 0;
    public bool HasSelectedClassEntry => SelectedClassEntry != null;

    public string PublicProfileName { get; set; } = "Публичный профиль";
    public string PublicProfileSubtitle { get; set; } = "Нет данных";
    public string PublicProfileStatusText { get; set; } = "Данные ещё не загружены";
    public string PublicProfileHintText { get; set; } = "Подключитесь к серверу, чтобы увидеть содержимое";
    public string PublicProfileDescription { get; set; } = "После загрузки здесь появится игровая карточка другого персонажа.";
    public bool HasPublicProfileData => PublicProfileIdentityRows.Count > 0 || PublicProfileSummaryRows.Count > 0 || PublicProfileHiddenRows.Count > 0;

    public ICommand ToggleAuthPopupCommand { get; }
    public ICommand ToggleConnectionPopupCommand { get; }
    public ICommand LoginCommand { get; }
    public ICommand RegisterCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand LoadCharacterDetailsCommand { get; }
    public ICommand CreateDiceRequestCommand { get; }
    public ICommand CancelRequestCommand { get; }
    public ICommand ChatSendCommand { get; }
    public ICommand BottomRefreshCommand { get; }
    public ICommand AudioApplyLocalSettingsCommand { get; }
    public ICommand VisibilityLoadCommand { get; }
    public ICommand VisibilitySaveCommand { get; }
    public ICommand PublicCharacterLoadCommand { get; }
    public ICommand NotesRefreshCommand { get; }
    public ICommand NotesCreateCommand { get; }
    public ICommand NotesArchiveCommand { get; }
    public ICommand AcquireClassNodeCommand { get; }
    public ICommand AcquireSkillCommand { get; }
    public ICommand ConnectToServerCommand { get; }
    public ICommand ApplyConnectionSettingsCommand { get; }
    public ICommand ResetConnectionDefaultsCommand { get; }
    public ICommand UseLastConnectionCommand { get; }

    private void Login()
    {
        try
        {
            EnsureConnected();
            ClientLogService.Instance.Info($"Login attempt: user={LoginText}");
            var result = _api.Login(LoginText, PasswordText);
            if (result.Status != ResponseStatus.Ok)
            {
                ConnectionState = "Оффлайн";
                ClientLogService.Instance.Warn($"Login failed: user={LoginText}; message={result.Message}");
                return;
            }

            SetConnectedState();
            IsAuthPopupOpen = false;
            PlayerDisplayName = LoginText;
            SessionSummary = "Сессия: default";
            RefreshAll();
            ClientLogService.Instance.Info($"Login success: user={LoginText}");
            _poller.Start();
        }
        catch (Exception ex)
        {
            SetConnectionError(ex);
        }
    }

    private void Register()
    {
        try
        {
            _api.Register(LoginText, PasswordText);
        }
        catch (Exception ex)
        {
            SetConnectionError(ex);
        }
    }

    private void RefreshAll()
    {
        try
        {
            LoadCharacters();
            LoadActiveCharacter();
            LoadClassAndSkills();
            RefreshBottomPanel();
            RefreshNotes();
            NotifyHeader();
            SetConnectedState();
        }
        catch (Exception ex)
        {
            SetConnectionError(ex);
        }
    }

    private void PollRefresh()
    {
        try
        {
            RefreshBottomPanel();
        }
        catch (Exception ex)
        {
            SetConnectionError(ex);
        }
    }

    private void LoadCharacters()
    {
        MyCharacters.Clear();
        var mine = _api.GetMyCharacters();
        foreach (var item in ToObjectList(mine.Payload.ContainsKey("items") ? mine.Payload["items"] : new ArrayList()))
        {
            if (item is not Dictionary<string, object> map) continue;
            MyCharacters.Add(new CharacterListItemVm
            {
                Id = GetString(map, "characterId"),
                Name = GetString(map, "name"),
                Race = GetString(map, "race"),
                Description = GetString(map, "description"),
                Age = GetString(map, "age"),
                Archived = GetString(map, "archived") == "True"
            });
        }

        if (string.IsNullOrWhiteSpace(SelectedCharacterId) && MyCharacters.Count > 0)
            SelectedCharacterId = MyCharacters[0].Id;
    }

    private void LoadSelectedCharacterDetails()
    {
        if (string.IsNullOrWhiteSpace(SelectedCharacterId)) return;
        var details = _api.GetCharacterDetails(SelectedCharacterId);
        if (details.Status == ResponseStatus.Ok)
            ApplyCharacterPayload(details.Payload);
    }

    private void LoadActiveCharacter()
    {
        var active = _api.GetActiveCharacter();
        if (active.Status == ResponseStatus.Ok && active.Payload.Count > 0)
            ApplyCharacterPayload(active.Payload);
    }

    private void ApplyCharacterPayload(Dictionary<string, object> payload)
    {
        CharacterName = GetString(payload, "name");
        CharacterRace = GetString(payload, "race");
        CharacterAge = GetString(payload, "age");
        CharacterHeight = GetString(payload, "height");
        CharacterDescription = GetString(payload, "description");
        CharacterBackstory = GetString(payload, "backstory");
        SelectedCharacterId = GetString(payload, "characterId");

        StatsRows.Clear();
        var stats = payload.ContainsKey("stats") && payload["stats"] is Dictionary<string, object> loadedStats
            ? loadedStats
            : new Dictionary<string, object>();
        AddStat("Здоровье", stats, "health");
        AddStat("Броня физ.", stats, "physicalArmor");
        AddStat("Броня маг.", stats, "magicalArmor");
        AddStat("Мораль", stats, "morale");
        AddStat("Сила", stats, "strength");
        AddStat("Ловкость", stats, "dexterity");
        AddStat("Выносливость", stats, "endurance");
        AddStat("Мудрость", stats, "wisdom");
        AddStat("Интеллект", stats, "intellect");
        AddStat("Харизма", stats, "charisma");
        RebuildStatGroups();

        MoneyRows.Clear();
        var money = payload.ContainsKey("money") && payload["money"] is Dictionary<string, object> loadedMoney
            ? loadedMoney
            : new Dictionary<string, object>();
        AddCurrency("Железная", "Fe", "#B0BEC5", money, "Iron");
        AddCurrency("Бронзовая", "Br", "#B87333", money, "Bronze");
        AddCurrency("Серебряная", "Ag", "#C0C0C0", money, "Silver");
        AddCurrency("Золотая", "Au", "#FFD700", money, "Gold");
        AddCurrency("Платиновая", "Pt", "#E5E4E2", money, "Platinum");
        AddCurrency("Орихалк", "Or", "#39FF14", money, "Orichalcum");
        AddCurrency("Адамант", "Ad", "#5F9EA0", money, "Adamant");
        AddCurrency("Государева", "Sov", "#B05CFF", money, "Sovereign");

        InventoryRows.Clear();
        foreach (var item in ToObjectList(payload.ContainsKey("inventory") ? payload["inventory"] : new ArrayList()))
        {
            if (item is not Dictionary<string, object> map) continue;
            InventoryRows.Add($"{GetString(map, "label")} x{GetString(map, "quantity")} | dur={GetString(map, "durability")} | equip={GetString(map, "equipped")} | use={GetString(map, "consumptionPerUse")}");
        }

        HoldingsRows.Clear();
        foreach (var item in ToObjectList(payload.ContainsKey("holdings") ? payload["holdings"] : new ArrayList()))
            if (item is Dictionary<string, object> map)
                HoldingsRows.Add($"{GetString(map, "name")} — {GetString(map, "description")}");

        ReputationRows.Clear();
        foreach (var item in ToObjectList(payload.ContainsKey("reputation") ? payload["reputation"] : new ArrayList()))
        {
            if (item is not Dictionary<string, object> map) continue;
            int.TryParse(GetString(map, "value"), out var value);
            ReputationRows.Add(new ReputationRowVm { Label = GetString(map, "groupKey"), Value = value });
        }

        Companions.Clear();
        foreach (var item in ToObjectList(payload.ContainsKey("companions") ? payload["companions"] : new ArrayList()))
        {
            if (item is not Dictionary<string, object> map) continue;
            var vm = new CompanionVm
            {
                Id = GetString(map, "id"),
                Name = string.IsNullOrWhiteSpace(GetString(map, "name")) ? "Безымянный компаньон" : GetString(map, "name"),
                Species = GetString(map, "species"),
                Notes = GetString(map, "notes")
            };
            AddCompanionStatScaffold(vm);
            if (map.ContainsKey("stats") && map["stats"] is Dictionary<string, object> companionStats)
                ApplyCompanionStats(vm, companionStats);

            foreach (var inv in ToObjectList(map.ContainsKey("inventory") ? map["inventory"] : new ArrayList()))
                if (inv is Dictionary<string, object> im)
                    vm.InventoryRows.Add($"{GetString(im, "label")} x{GetString(im, "quantity")}");

            foreach (var hold in ToObjectList(map.ContainsKey("holdings") ? map["holdings"] : new ArrayList()))
                if (hold is Dictionary<string, object> hm)
                    vm.HoldingsRows.Add($"{GetString(hm, "name")} — {GetString(hm, "description")}");

            foreach (var sk in ToObjectList(map.ContainsKey("skills") ? map["skills"] : new ArrayList()))
                if (sk is Dictionary<string, object> sm)
                    vm.SkillsRows.Add($"{GetString(sm, "name")} [{GetString(sm, "type")}] ");

            EnsureCollectionPlaceholder(vm.InventoryRows, "Нет данных по инвентарю");
            EnsureCollectionPlaceholder(vm.HoldingsRows, "Нет данных по владениям");
            EnsureCollectionPlaceholder(vm.SkillsRows, "Нет данных по навыкам");
            EnsureCollectionPlaceholder(vm.ClassRows, "Классы компаньона не загружены");

            Companions.Add(vm);
        }

        EnsureCollectionPlaceholder(InventoryRows, "Нет данных по инвентарю");
        EnsureCollectionPlaceholder(HoldingsRows, "Нет данных по владениям");
        EnsureReputationPlaceholder();
        EnsureCompanionsPlaceholder();
        if (SelectedCompanion == null || !Companions.Contains(SelectedCompanion))
            SelectedCompanion = Companions.FirstOrDefault();

        NotifyCharacter();
    }

    private void CreateDiceRequest()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(SelectedCharacterId) && MyCharacters.Count > 0) SelectedCharacterId = MyCharacters[0].Id;
            var formula = DiceCount + "d" + DiceFaces + (DiceModifier == 0 ? string.Empty : DiceModifier > 0 ? "+" + DiceModifier : DiceModifier.ToString());
            _api.CreateDiceRequest(SelectedCharacterId, formula, ToServerDiceVisibility(DiceVisibilityInput), DiceDescriptionInput);
            RefreshBottomPanel();
        }
        catch (Exception ex) { SetConnectionError(ex); }
    }

    private void CancelRequest()
    {
        if (string.IsNullOrWhiteSpace(SelectedRequestId)) return;
        try
        {
            _api.CancelRequest(SelectedRequestId);
            RefreshBottomPanel();
        }
        catch (Exception ex) { SetConnectionError(ex); }
    }

    private void RefreshBottomPanel()
    {
        RefreshChat();
        RefreshDiceAndRequests();
        RefreshCombatEvents();
        RefreshAudioState();
        BuildGameFeed();
    }

    private void SendChat()
    {
        if (string.IsNullOrWhiteSpace(ChatTextInput)) return;
        var sessionId = ResolveChatSessionId();
        var serverType = ToServerChatType(ChatTypeInput);
        if (string.Equals(serverType, "System", StringComparison.OrdinalIgnoreCase))
        {
            TraceChatDiagnostic("blocked client-side system message send");
            return;
        }
        ClientLogService.Instance.Info($"Chat send requested: sessionId={sessionId}; command={CommandNames.ChatSend}");
        _api.ChatSend(sessionId, serverType, ChatTextInput);
        ChatTextInput = string.Empty;
        Notify(nameof(ChatTextInput));
        RefreshChat();
        BuildGameFeed();
    }

    private void RefreshChat()
    {
        var sessionId = ResolveChatSessionId();
        TraceChatDiagnostic($"request command={CommandNames.ChatVisibleFeed} session={sessionId}");
        ChatRows.Clear();
        ChatMessageRows.Clear();
        var chat = _api.ChatVisibleFeed(sessionId, 80);
        var chatItems = ExtractChatItems(chat.Payload, out var sourceKey, out var payloadKeys, out var rawItemsType);
        TraceChatDiagnostic($"response command={CommandNames.ChatVisibleFeed} status={chat.Status} success={(chat.Status == ResponseStatus.Ok)} payloadKeys=[{payloadKeys}] sourceKey={sourceKey} rawItems={chatItems.Count} rawType={rawItemsType}");
        LogFirstChatItemShape(chatItems, CommandNames.ChatVisibleFeed);
        var mappedCount = 0;
        var filteredCount = 0;
        foreach (var item in chatItems)
        {
            var map = AsMap(item, CommandNames.ChatVisibleFeed);
            if (map == null) continue;
            mappedCount++;
            var row = BuildChatMessageRow(map);
            if (row == null)
            {
                filteredCount++;
                continue;
            }

            ChatRows.Add($"{row.Sender}: {row.Text}");
            ChatMessageRows.Add(row);
        }
        TraceChatDiagnostic($"mapped command={CommandNames.ChatVisibleFeed} mappedItems={mappedCount} filteredOut={filteredCount} displayItems={ChatMessageRows.Count}");

        BuildGameFeed();
        TraceChatDiagnostic($"collection command={CommandNames.ChatVisibleFeed} chatRows={ChatRows.Count} chatMessageRows={ChatMessageRows.Count} uiCollection=GameFeedRows uiCount={GameFeedRows.Count}");
    }

    private void RefreshDiceAndRequests()
    {
        DiceFeedRows.Clear();
        var feed = _api.DiceVisibleFeed();
        foreach (var item in ToObjectList(feed.Payload.ContainsKey("items") ? feed.Payload["items"] : new ArrayList()))
        {
            if (item is not Dictionary<string, object> map) continue;
            var total = string.Empty;
            if (map.ContainsKey("result") && map["result"] is Dictionary<string, object> result) total = GetString(result, "total");
            DiceFeedRows.Add($"{GetString(map, "creatorUserId")} | {GetString(map, "formula")} => {total}");
        }

        RequestRows.Clear();
        var req = _api.ListMyRequests();
        foreach (var item in ToObjectList(req.Payload.ContainsKey("items") ? req.Payload["items"] : new ArrayList()))
        {
            if (item is not Dictionary<string, object> map) continue;
            RequestRows.Add($"{GetString(map, "requestId")} | {GetString(map, "status")} | {GetString(map, "formula")}");
        }

        EnsureCollectionPlaceholder(DiceFeedRows, "Нет видимых бросков");
        EnsureCollectionPlaceholder(RequestRows, "Нет активных заявок");
    }

    private void RefreshCombatEvents()
    {
        EventRows.Clear();
        var timeline = _api.CombatTimeline(ChatSessionId);
        foreach (var item in ToObjectList(timeline.Payload.ContainsKey("items") ? timeline.Payload["items"] : new ArrayList()))
        {
            if (item is not Dictionary<string, object> map) continue;
            EventRows.Add($"{GetString(map, "at")} | {GetString(map, "eventType")} | {GetString(map, "message")}");
        }

        SessionStateRows.Clear();
        var state = _api.CombatVisibleState(ChatSessionId);
        SessionStateRows.Add("Combat status: " + GetString(state.Payload, "status"));
        SessionStateRows.Add("Round: " + GetString(state.Payload, "round"));
        SessionStateRows.Add("Active slot: " + GetString(state.Payload, "activeSlotId"));

        EnsureCollectionPlaceholder(EventRows, "Нет системных событий");
    }

    private string ConnectionSettingsPath
    {
        get
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Nri.PlayerClient");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return Path.Combine(dir, "connection.settings.json");
        }
    }

    private string AudioSettingsPath
    {
        get
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Nri.PlayerClient");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return Path.Combine(dir, "audio.settings.json");
        }
    }


    private void LoadConnectionSettings()
    {
        try
        {
            if (File.Exists(ConnectionSettingsPath))
            {
                var map = JsonProtocolSerializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(ConnectionSettingsPath));
                if (map != null)
                {
                    ServerHostInput = GetStringOrFallback(map, "serverHost", "127.0.0.1");
                    ServerPortInput = GetStringOrFallback(map, "serverPort", "4600");
                    LastServerHost = ServerHostInput;
                    int.TryParse(ServerPortInput, NumberStyles.Integer, CultureInfo.InvariantCulture, out var loadedPort);
                    LastServerPort = loadedPort <= 0 ? 4600 : loadedPort;
                }
            }
            else
            {
                ServerHostInput = _clientConfig.ServerHost;
                ServerPortInput = _clientConfig.ServerPort.ToString(CultureInfo.InvariantCulture);
                LastServerHost = ServerHostInput;
                LastServerPort = _clientConfig.ServerPort;
                Notify(nameof(LastServerHost));
                Notify(nameof(LastServerPort));
            }

            _client.UpdateEndpoint(ServerHostInput, LastServerPort);
            Notify(nameof(ServerHostInput));
            Notify(nameof(ServerPortInput));
            Notify(nameof(ConnectedEndpointDisplay));
        }
        catch
        {
            ServerHostInput = "127.0.0.1";
            ServerPortInput = "4600";
            LastServerHost = ServerHostInput;
            LastServerPort = 4600;
        }
    }

    private void SaveConnectionSettings()
    {
        File.WriteAllText(ConnectionSettingsPath, JsonProtocolSerializer.Serialize(new Dictionary<string, object>
        {
            { "serverHost", ServerHostInput },
            { "serverPort", ServerPortInput }
        }));
    }

    private void ConnectToServer()
    {
        ApplyConnectionSettings();
    }

    private void ApplyConnectionSettings()
    {
        if (!TryValidateConnectionSettings(out var host, out var port, out var message))
        {
            SetDisconnectedState(message);
            return;
        }

        try
        {
            _client.UpdateEndpoint(host, port);
            _client.Connect();
            ServerHostInput = host;
            ServerPortInput = port.ToString(CultureInfo.InvariantCulture);
            LastServerHost = host;
            LastServerPort = port;
            Notify(nameof(LastServerHost));
            Notify(nameof(LastServerPort));
            SaveConnectionSettings();
            SetConnectedState();
            IsConnectionPopupOpen = false;
            Notify(nameof(ServerHostInput));
            Notify(nameof(ServerPortInput));
            RefreshConnectionSummary();
        }
        catch (Exception ex)
        {
            SetConnectionError(ex);
        }
    }

    private void ResetConnectionDefaults()
    {
        ServerHostInput = "127.0.0.1";
        ServerPortInput = "4600";
        Notify(nameof(ServerHostInput));
        Notify(nameof(ServerPortInput));
        Notify(nameof(ConnectedEndpointDisplay));
    }

    private void UseSavedConnectionSettings()
    {
        ServerHostInput = LastServerHost;
        ServerPortInput = LastServerPort.ToString(CultureInfo.InvariantCulture);
        Notify(nameof(ServerHostInput));
        Notify(nameof(ServerPortInput));
        Notify(nameof(ConnectedEndpointDisplay));
    }

    private bool TryValidateConnectionSettings(out string host, out int port, out string error)
    {
        host = (ServerHostInput ?? string.Empty).Trim();
        error = string.Empty;
        port = 0;

        if (string.IsNullOrWhiteSpace(host))
        {
            error = "Неверный адрес";
            return false;
        }

        if (!string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) && !IPAddress.TryParse(host, out _))
        {
            error = "Неверный адрес";
            return false;
        }

        if (!int.TryParse(ServerPortInput, NumberStyles.Integer, CultureInfo.InvariantCulture, out port) || port < 1 || port > 65535)
        {
            error = "Неверный порт";
            return false;
        }

        return true;
    }

    private void EnsureConnected()
    {
        if (_client.ServerHost != ServerHostInput || _client.ServerPort.ToString(CultureInfo.InvariantCulture) != ServerPortInput)
            ApplyConnectionSettings();
        else
            _client.Connect();
    }

    private void SetConnectedState()
    {
        ConnectionState = "Онлайн";
        ConnectionStatusDetail = $"Подключено к {ServerHostInput}:{ServerPortInput}";
        RefreshConnectionSummary();
        Notify(nameof(ConnectionStatusDetail));
    }

    private void SetDisconnectedState(string message)
    {
        _client.Disconnect();
        ConnectionState = "Оффлайн";
        ConnectionStatusDetail = message;
        RefreshConnectionSummary();
        Notify(nameof(ConnectionStatusDetail));
    }

    private void SetConnectionError(Exception ex)
    {
        var message = ex switch
        {
            SocketException => "Сервер недоступен",
            TimeoutException => "Не удалось подключиться к серверу: timeout",
            InvalidOperationException => "Не удалось подключиться к серверу",
            _ => string.IsNullOrWhiteSpace(ex.Message) ? "Не удалось подключиться к серверу" : ex.Message
        };
        ClientLogService.Instance.Error("Connection error", ex);
        SetDisconnectedState(message);
    }

    private void RefreshConnectionSummary()
    {
        SessionSummary = $"Сервер: {ServerHostInput}:{ServerPortInput}";
        Notify(nameof(SessionSummary));
        Notify(nameof(ConnectedEndpointDisplay));
    }

    private void RefreshAudioState()
    {
        var state = _api.AudioStateSync(AudioSessionId);
        AudioStateText = $"{GetString(state.Payload, "mode")} / {GetString(state.Payload, "category")} / {GetString(state.Payload, "trackName")} @ {GetString(state.Payload, "positionSeconds")}s";
        Notify(nameof(AudioStateText));
    }

    private void LoadLocalAudioSettings()
    {
        try
        {
            if (!File.Exists(AudioSettingsPath))
                return;

            var map = JsonProtocolSerializer.Deserialize<Dictionary<string, object>>(
             File.ReadAllText(AudioSettingsPath));

            if (map == null)
                return;

            double volume = 0.7;
            bool muted = false;

            if (map.ContainsKey("volume"))
                double.TryParse(Convert.ToString(map["volume"]), out volume);

            if (map.ContainsKey("muted"))
                bool.TryParse(Convert.ToString(map["muted"]), out muted);

            LocalVolume = Math.Max(0, Math.Min(1, volume));
            LocalMuted = muted;
        }
        catch
        {

        }
    }

    private void ApplyAudioLocalSettings()
    {
        _api.AudioClientSettingsSet(LocalVolume, LocalMuted);
        try
        {
            File.WriteAllText(AudioSettingsPath, JsonProtocolSerializer.Serialize(new Dictionary<string, object>
            {
                { "volume", LocalVolume },
                { "muted", LocalMuted }
            }));
        }
        catch { }

        RefreshAudioState();
    }

    private void LoadVisibility()
    {
        if (string.IsNullOrWhiteSpace(SelectedCharacterId)) return;
        var r = _api.VisibilityGet(SelectedCharacterId);
        VisHideDescription = GetString(r.Payload, "hideDescriptionForOthers") == "True";
        VisHideBackstory = GetString(r.Payload, "hideBackstoryForOthers") == "True";
        VisHideStats = GetString(r.Payload, "hideStatsForOthers") == "True";
        VisHideReputation = GetString(r.Payload, "hideReputationForOthers") == "True";
        Notify(nameof(VisHideDescription));
        Notify(nameof(VisHideBackstory));
        Notify(nameof(VisHideStats));
        Notify(nameof(VisHideReputation));
    }

    private void SaveVisibility()
    {
        if (string.IsNullOrWhiteSpace(SelectedCharacterId)) return;
        _api.VisibilityUpdate(new Dictionary<string, object>
        {
            { "characterId", SelectedCharacterId },
            { "hideDescriptionForOthers", VisHideDescription },
            { "hideBackstoryForOthers", VisHideBackstory },
            { "hideStatsForOthers", VisHideStats },
            { "hideReputationForOthers", VisHideReputation }
        });
    }

    private void LoadPublicCharacter()
    {
        PublicCharacterRows.Clear();
        InitializeDefaultPublicProfile();
        if (string.IsNullOrWhiteSpace(PublicViewCharacterId)) return;
        var r = _api.CharacterPublicViewGet(PublicViewCharacterId);
        foreach (var kv in r.Payload)
            PublicCharacterRows.Add(kv.Key + " = " + Convert.ToString(kv.Value));

        PublicProfileName = string.IsNullOrWhiteSpace(GetString(r.Payload, "name")) ? "Публичный профиль" : GetString(r.Payload, "name");
        PublicProfileSubtitle = string.IsNullOrWhiteSpace(GetString(r.Payload, "race")) ? "Публичный профиль загружен" : $"Раса: {GetString(r.Payload, "race")}";
        PublicProfileStatusText = "Данные профиля получены";
        PublicProfileHintText = string.IsNullOrWhiteSpace(PublicViewCharacterId) ? "Подключитесь к серверу, чтобы увидеть содержимое" : $"Идентификатор просмотра: {PublicViewCharacterId}";
        PublicProfileDescription = string.IsNullOrWhiteSpace(GetString(r.Payload, "description")) ? "Описание скрыто или ещё не загружено." : GetString(r.Payload, "description");

        AddPublicProfileField(PublicProfileIdentityRows, "Имя", PublicProfileName);
        AddPublicProfileField(PublicProfileIdentityRows, "Раса", GetStringOrFallback(r.Payload, "race", "Не указано"));
        AddPublicProfileField(PublicProfileIdentityRows, "Возраст", GetStringOrFallback(r.Payload, "age", "Не указано"));
        AddPublicProfileField(PublicProfileIdentityRows, "Рост", GetStringOrFallback(r.Payload, "height", "Не указано"));
        AddPublicProfileField(PublicProfileSummaryRows, "Описание", GetStringOrFallback(r.Payload, "description", "Скрыто или недоступно"));
        AddPublicProfileField(PublicProfileSummaryRows, "Предыстория", GetStringOrFallback(r.Payload, "backstory", "Скрыто или недоступно"));
        AddPublicProfileField(PublicProfileSummaryRows, "Характеристики", GetStringOrFallback(r.Payload, "statsSummary", "Краткая сводка пока не предоставлена сервером"));

        foreach (var hiddenKey in new[] { "hiddenFields", "hidden", "blockedFields" })
        {
            if (!r.Payload.ContainsKey(hiddenKey)) continue;
            foreach (var item in ToObjectList(r.Payload[hiddenKey]))
                PublicProfileHiddenRows.Add(Convert.ToString(item) ?? string.Empty);
        }

        if (PublicProfileHiddenRows.Count == 0)
            PublicProfileHiddenRows.Add("Скрытые поля сервером не перечислены");

        NotifyPublicProfile();
    }

    private void RefreshNotes()
    {
        NoteRows.Clear();
        AdminNoteRows.Clear();
        var r = _api.NotesList(new Dictionary<string, object>
        {
            { "sessionId", NoteSessionId },
            { "targetType", NoteTargetType },
            { "targetId", NoteTargetId }
        });

        foreach (var item in ToObjectList(r.Payload.ContainsKey("items") ? r.Payload["items"] : new ArrayList()))
        {
            if (item is not Dictionary<string, object> map) continue;
            var row = $"{GetString(map, "noteId")} | {GetString(map, "visibility")} | {GetString(map, "title")} | {GetString(map, "text")}";
            if (GetString(map, "visibility") == "AdminOnly" || GetString(map, "noteType") == "Master")
                AdminNoteRows.Add(row);
            else
                NoteRows.Add(row);
        }

        EnsureCollectionPlaceholder(NoteRows, "Нет личных заметок");
        EnsureCollectionPlaceholder(AdminNoteRows, "Нет заметок/советов от админов");
    }

    private void CreateNote()
    {
        _api.NotesCreate(new Dictionary<string, object>
        {
            { "sessionId", NoteSessionId },
            { "targetType", NoteTargetType },
            { "targetId", NoteTargetId },
            { "title", NoteTitle },
            { "text", NoteText },
            { "visibility", NoteVisibility },
            { "noteType", "Personal" }
        });
        RefreshNotes();
    }

    private void ArchiveNote()
    {
        if (string.IsNullOrWhiteSpace(SelectedNoteId)) return;
        _api.NotesArchive(SelectedNoteId);
        RefreshNotes();
    }

    private void LoadClassAndSkills()
    {
        if (string.IsNullOrWhiteSpace(SelectedCharacterId)) return;

        var tree = _api.ClassTreeAvailable(SelectedCharacterId);
        var available = new HashSet<string>();
        var acquired = new HashSet<string>();

        foreach (var item in ToObjectList(tree.Payload.ContainsKey("items") ? tree.Payload["items"] : new ArrayList()))
        {
            if (item is not Dictionary<string, object> map) continue;
            var nodeId = GetString(map, "nodeId");
            if (GetString(map, "acquired") == "True") acquired.Add(nodeId);
            else if (GetString(map, "available") == "True") available.Add(nodeId);
        }

        foreach (var node in ClassNodes)
        {
            if (node.NodeId == "novice") node.State = "Start";
            else if (acquired.Contains(node.NodeId)) node.State = "Taken";
            else if (available.Contains(node.NodeId)) node.State = "Available";
            else node.State = "Locked";
        }
        Notify(nameof(ClassNodes));
        RebuildClassNavigation();

        SkillRows.Clear();
        SkillCatalogRows.Clear();
        var skills = _api.SkillsList(SelectedCharacterId);
        foreach (var item in ToObjectList(skills.Payload.ContainsKey("items") ? skills.Payload["items"] : new ArrayList()))
        {
            if (item is not Dictionary<string, object> map) continue;
            var row = $"{GetString(map, "name")} [{GetString(map, "type")}] | acquired={GetString(map, "acquired")} | available={GetString(map, "available")} | {GetString(map, "reason")}";
            SkillCatalogRows.Add(row);
            SkillRows.Add(row);
        }
    }

    private void AcquireClassNode(object? parameter)
    {
        if (parameter is string key && !string.IsNullOrWhiteSpace(key))
        {
            if (ClassDirections.Any(d => d.Key == key))
            {
                SelectedClassDirectionKey = key;
                return;
            }

            if (ClassBranches.Any(b => b.Key == key))
            {
                SelectedClassBranch = ClassBranches.First(b => b.Key == key);
                return;
            }

            SelectedClassNodeId = key;
            var entry = ClassEntries.FirstOrDefault(e => e.NodeId == key);
            if (entry != null)
                SelectedClassEntry = entry;
        }
        if (string.IsNullOrWhiteSpace(SelectedCharacterId) || string.IsNullOrWhiteSpace(SelectedClassNodeId)) return;
        _api.ClassTreeAcquireNode(SelectedCharacterId, SelectedClassNodeId);
        LoadClassAndSkills();
    }

    private void AcquireSkill()
    {
        if (string.IsNullOrWhiteSpace(SelectedCharacterId) || string.IsNullOrWhiteSpace(SelectedSkillId)) return;
        _api.SkillsAcquire(SelectedCharacterId, SelectedSkillId);
        LoadClassAndSkills();
    }

    private void InitializeClassVisualLayout()
    {
        ClassDirections.Clear();
        ClassDirections.Add(new ClassDirectionVm { Key = "defender", Label = "Защитник", Summary = "Оборона, стойкость и контроль линии фронта." });
        ClassDirections.Add(new ClassDirectionVm { Key = "vanguard", Label = "Передовой", Summary = "Агрессивное продвижение и давление на противника." });
        ClassDirections.Add(new ClassDirectionVm { Key = "ranger", Label = "Рейнджер", Summary = "Дистанция, мобильность и точечные удары." });
        ClassDirections.Add(new ClassDirectionVm { Key = "samurai", Label = "Самурай", Summary = "Дисциплина, темп боя и контрудары." });
        ClassDirections.Add(new ClassDirectionVm { Key = "mage", Label = "Маг", Summary = "Арканные эффекты, контроль и поддержка." });
        ClassDirections.Add(new ClassDirectionVm { Key = "inventor", Label = "Изобретатель", Summary = "Тактические устройства и нестандартные решения." });

        ClassNodes.Clear();
        ClassNodes.Add(new ClassNodeVisualVm { NodeId = "novice", Title = "Новичок", State = "Start", X = 190, Y = 120 });
        ClassNodes.Add(new ClassNodeVisualVm { NodeId = "defender_branch_1", Title = "Опорная стойка", State = "Locked", X = 190, Y = 12 });
        ClassNodes.Add(new ClassNodeVisualVm { NodeId = "vanguard_branch_1", Title = "Прорывной строй", State = "Locked", X = 322, Y = 68 });
        ClassNodes.Add(new ClassNodeVisualVm { NodeId = "ranger_branch_1", Title = "Дальняя тропа", State = "Locked", X = 322, Y = 186 });
        ClassNodes.Add(new ClassNodeVisualVm { NodeId = "samurai_branch_1", Title = "Путь клинка", State = "Locked", X = 190, Y = 242 });
        ClassNodes.Add(new ClassNodeVisualVm { NodeId = "mage_branch_1", Title = "Круг фокуса", State = "Locked", X = 58, Y = 186 });
        ClassNodes.Add(new ClassNodeVisualVm { NodeId = "inventor_branch_1", Title = "Механический контур", State = "Locked", X = 58, Y = 68 });
        RebuildClassNavigation();
    }

    private void AddStat(string label, Dictionary<string, object> map, string key) => StatsRows.Add(new StatRowVm { Label = label, Value = string.IsNullOrWhiteSpace(GetString(map, key)) ? "0" : GetString(map, key) });

    private void RebuildStatGroups()
    {
        CoreStatRows.Clear();
        AttributeStatRows.Clear();

        foreach (var row in StatsRows)
        {
            if (row.Label == "Здоровье" || row.Label == "Броня физ." || row.Label == "Броня маг." || row.Label == "Мораль")
                CoreStatRows.Add(row);
            else
                AttributeStatRows.Add(row);
        }
    }

    private void InitializeDefaultCharacterScaffolding()
    {
        if (StatsRows.Count == 0)
        {
            var empty = new Dictionary<string, object>();
            AddStat("Здоровье", empty, "health");
            AddStat("Броня физ.", empty, "physicalArmor");
            AddStat("Броня маг.", empty, "magicalArmor");
            AddStat("Мораль", empty, "morale");
            AddStat("Сила", empty, "strength");
            AddStat("Ловкость", empty, "dexterity");
            AddStat("Выносливость", empty, "endurance");
            AddStat("Мудрость", empty, "wisdom");
            AddStat("Интеллект", empty, "intellect");
            AddStat("Харизма", empty, "charisma");
        }

        if (MoneyRows.Count == 0)
        {
            var empty = new Dictionary<string, object>();
            AddCurrency("Железная", "Fe", "#B0BEC5", empty, "Iron");
            AddCurrency("Бронзовая", "Br", "#B87333", empty, "Bronze");
            AddCurrency("Серебряная", "Ag", "#C0C0C0", empty, "Silver");
            AddCurrency("Золотая", "Au", "#FFD700", empty, "Gold");
            AddCurrency("Платиновая", "Pt", "#E5E4E2", empty, "Platinum");
            AddCurrency("Орихалк", "Or", "#39FF14", empty, "Orichalcum");
            AddCurrency("Адамант", "Ad", "#5F9EA0", empty, "Adamant");
            AddCurrency("Государева", "Sov", "#B05CFF", empty, "Sovereign");
        }

        EnsureCollectionPlaceholder(InventoryRows, "Данные инвентаря не загружены");
        EnsureCollectionPlaceholder(HoldingsRows, "Данные владений не загружены");
        EnsureReputationPlaceholder();
        EnsureCompanionsPlaceholder();
        SelectedCompanion = Companions.FirstOrDefault();
        EnsureCollectionPlaceholder(NoteRows, "Нет заметок");
        RebuildStatGroups();
        EnsureCollectionPlaceholder(AdminNoteRows, "Нет советов от админов");
        RebuildClassNavigation();
        BuildGameFeed();
    }


    private void InitializeDefaultPublicProfile()
    {
        PublicProfileIdentityRows.Clear();
        PublicProfileSummaryRows.Clear();
        PublicProfileHiddenRows.Clear();
        PublicProfileName = "Публичный профиль";
        PublicProfileSubtitle = "Нет данных";
        PublicProfileStatusText = "Данные ещё не загружены";
        PublicProfileHintText = "Подключитесь к серверу, чтобы увидеть содержимое";
        PublicProfileDescription = "После загрузки здесь появятся имя, раса, описание и доступная сводка характеристик другого персонажа.";
        NotifyPublicProfile();
    }

    private void RebuildClassNavigation()
    {
        ClassBranches.Clear();
        var direction = ClassDirections.FirstOrDefault(d => d.Key == SelectedClassDirectionKey) ?? ClassDirections.FirstOrDefault();
        if (direction == null)
            return;

        foreach (var node in ClassNodes.Where(n => n.NodeId != "novice" && n.NodeId.StartsWith(direction.Key, StringComparison.OrdinalIgnoreCase)))
        {
            ClassBranches.Add(new ClassBranchVm
            {
                Key = node.NodeId,
                DirectionKey = direction.Key,
                Title = node.Title,
                Summary = $"Ветвь направления «{direction.Label}». Здесь позже появятся десятки специализированных классов.",
                Status = node.State
            });
        }

        if (ClassBranches.Count == 0)
        {
            ClassBranches.Add(new ClassBranchVm
            {
                Key = direction.Key + "_placeholder_branch",
                DirectionKey = direction.Key,
                Title = "Ветвь ещё не загружена",
                Summary = "Сервер пока не прислал реальные ветви для этого направления.",
                Status = "Placeholder"
            });
        }

        SelectedClassBranch = ClassBranches.FirstOrDefault();
        Notify(nameof(HasClassBranches));
        Notify(nameof(SelectedClassDirectionDisplay));
    }

    private void RebuildClassEntries()
    {
        ClassEntries.Clear();
        if (SelectedClassBranch == null)
        {
            SelectedClassEntry = null;
            NotifyClassDetail();
            return;
        }

        if (SelectedClassBranch.Key.EndsWith("_placeholder_branch", StringComparison.OrdinalIgnoreCase))
        {
            ClassEntries.Add(new ClassEntryVm
            {
                NodeId = SelectedClassBranch.Key + "_class",
                BranchKey = SelectedClassBranch.Key,
                Title = "Классы ещё не загружены",
                Summary = "Когда сервер начнёт отдавать дерево развития, здесь появится список классов выбранной ветви.",
                Status = "Placeholder"
            });
        }
        else
        {
            var branchTitle = SelectedClassBranch.Title;
            ClassEntries.Add(new ClassEntryVm
            {
                NodeId = SelectedClassBranch.Key,
                BranchKey = SelectedClassBranch.Key,
                Title = branchTitle,
                Summary = "Стартовый узел ветви. Позже здесь будут уровни развития, развилки и специализации.",
                Status = SelectedClassBranch.Status
            });
            ClassEntries.Add(new ClassEntryVm
            {
                NodeId = SelectedClassBranch.Key + "_advanced",
                BranchKey = SelectedClassBranch.Key,
                Title = branchTitle + " — развитие",
                Summary = "Placeholder для следующего класса внутри выбранной ветви.",
                Status = "Placeholder"
            });
        }

        SelectedClassEntry = ClassEntries.FirstOrDefault();
        Notify(nameof(HasClassEntries));
        Notify(nameof(SelectedClassBranchTitle));
        Notify(nameof(SelectedClassBranchSummary));
    }

    private void NotifyClassDetail()
    {
        Notify(nameof(SelectedClassDirectionDisplay));
        Notify(nameof(SelectedClassBranchTitle));
        Notify(nameof(SelectedClassBranchSummary));
        Notify(nameof(SelectedClassEntryTitle));
        Notify(nameof(SelectedClassEntrySummary));
        Notify(nameof(SelectedClassEntryState));
        Notify(nameof(SelectedClassEntryRequirements));
        Notify(nameof(HasSelectedClassEntry));
    }

    private void AddPublicProfileField(ObservableCollection<PublicProfileFieldVm> target, string label, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            target.Add(new PublicProfileFieldVm { Label = label, Value = value });
    }

    private void NotifyPublicProfile()
    {
        Notify(nameof(PublicProfileName));
        Notify(nameof(PublicProfileSubtitle));
        Notify(nameof(PublicProfileStatusText));
        Notify(nameof(PublicProfileHintText));
        Notify(nameof(PublicProfileDescription));
        Notify(nameof(HasPublicProfileData));
    }

    private static string GetStringOrFallback(Dictionary<string, object> map, string key, string fallback)
    {
        var value = GetString(map, key);
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static string GetDirectionLabel(string key)
    {
        return key switch
        {
            "defender" => "Защитник",
            "vanguard" => "Передовой",
            "ranger" => "Рейнджер",
            "samurai" => "Самурай",
            "mage" => "Маг",
            "inventor" => "Изобретатель",
            _ => "Направление не выбрано"
        };
    }

    private void EnsureCollectionPlaceholder(ObservableCollection<string> collection, string placeholder)
    {
        if (collection.Count == 0)
            collection.Add(placeholder);
    }

    private void EnsureReputationPlaceholder()
    {
        if (ReputationRows.Count == 0)
            ReputationRows.Add(new ReputationRowVm { Label = "Нет данных", Value = 0 });
    }

    private void EnsureCompanionsPlaceholder()
    {
        if (Companions.Count == 0)
        {
            var vm = new CompanionVm { Name = "Нет данных", Species = "—", Notes = "Сервер не вернул компаньонов" };
            AddCompanionStatScaffold(vm);
            vm.InventoryRows.Add("Нет данных");
            vm.HoldingsRows.Add("Нет данных");
            vm.SkillsRows.Add("Нет данных");
            vm.ClassRows.Add("Классы компаньона не загружены");
            Companions.Add(vm);
        }
    }

    private void AddCompanionStatScaffold(CompanionVm vm)
    {
        if (vm.StatsRows.Count > 0) return;
        AddCompanionStat(vm, "Здоровье", "0", true);
        AddCompanionStat(vm, "Физ. защита", "0", true);
        AddCompanionStat(vm, "Маг. защита", "0", true);
        AddCompanionStat(vm, "Мораль", "0", true);
        AddCompanionStat(vm, "Сила", "0", false);
        AddCompanionStat(vm, "Ловкость", "0", false);
        AddCompanionStat(vm, "Выносливость", "0", false);
        AddCompanionStat(vm, "Мудрость", "0", false);
        AddCompanionStat(vm, "Интеллект", "0", false);
        AddCompanionStat(vm, "Харизма", "0", false);
    }

    private void ApplyCompanionStats(CompanionVm vm, Dictionary<string, object> stats)
    {
        vm.StatsRows.Clear();
        vm.CoreStatRows.Clear();
        vm.AttributeStatRows.Clear();
        AddCompanionStat(vm, "Здоровье", GetMapValueOrDefault(stats, "health"), true);
        AddCompanionStat(vm, "Физ. защита", GetMapValueOrDefault(stats, "physicalArmor", "physicalDefense"), true);
        AddCompanionStat(vm, "Маг. защита", GetMapValueOrDefault(stats, "magicalArmor", "magicalDefense"), true);
        AddCompanionStat(vm, "Мораль", GetMapValueOrDefault(stats, "morale"), true);
        AddCompanionStat(vm, "Сила", GetMapValueOrDefault(stats, "strength"), false);
        AddCompanionStat(vm, "Ловкость", GetMapValueOrDefault(stats, "dexterity"), false);
        AddCompanionStat(vm, "Выносливость", GetMapValueOrDefault(stats, "endurance"), false);
        AddCompanionStat(vm, "Мудрость", GetMapValueOrDefault(stats, "wisdom"), false);
        AddCompanionStat(vm, "Интеллект", GetMapValueOrDefault(stats, "intellect"), false);
        AddCompanionStat(vm, "Харизма", GetMapValueOrDefault(stats, "charisma"), false);
    }

    private void AddCompanionStat(CompanionVm vm, string label, string value, bool isCore)
    {
        var row = new StatRowVm { Label = label, Value = string.IsNullOrWhiteSpace(value) ? "0" : value };
        vm.StatsRows.Add(row);
        if (isCore)
            vm.CoreStatRows.Add(row);
        else
            vm.AttributeStatRows.Add(row);
    }


    public void Shutdown()
    {
        ClientLogService.Instance.Info("Logout / shutdown requested from Player client");
        _poller.Stop();
        _client.Disconnect();
    }

    private string ToServerChatType(string uiType)
    {
        return uiType switch
        {
            "Общее" => "Public",
            "Скрытое админам" => "HiddenToAdmins",
            "Только админам" => "AdminOnly",
            _ => "Public"
        };
    }

    private string ToServerDiceVisibility(string uiValue)
    {
        return uiValue switch
        {
            "Общее" => "Public",
            "Только мастеру" => "MasterOnly",
            "Теневой" => "Shadow",
            _ => "Public"
        };
    }

    private void BuildGameFeed()
    {
        GameFeedRows.Clear();
        var filteredPlaceholders = 0;

        foreach (var item in ChatMessageRows)
        {
            GameFeedRows.Add(new GameFeedItemVm
            {
                Kind = item.IsSystem ? "System" : "Chat",
                Text = $"{item.Sender}: {item.Text}",
                IsMuted = item.IsSystem
            });
        }

        foreach (var item in EventRows)
        {
            if (IsPlaceholderText(item))
            {
                filteredPlaceholders++;
                continue;
            }

            GameFeedRows.Add(new GameFeedItemVm { Kind = "System", Text = item, IsMuted = true });
        }

        foreach (var item in DiceFeedRows)
        {
            if (IsPlaceholderText(item))
            {
                filteredPlaceholders++;
                continue;
            }

            GameFeedRows.Add(new GameFeedItemVm { Kind = "Dice", Text = item, IsMuted = true });
        }

        foreach (var item in RequestRows)
        {
            if (IsPlaceholderText(item))
            {
                filteredPlaceholders++;
                continue;
            }

            GameFeedRows.Add(new GameFeedItemVm { Kind = "Request", Text = item, IsMuted = true });
        }

        if (GameFeedRows.Count == 0)
            GameFeedRows.Add(new GameFeedItemVm { Kind = "Hint", Text = "Лента пуста", IsMuted = true });

        TraceChatDiagnostic($"game-feed build chat={ChatMessageRows.Count} event={EventRows.Count} dice={DiceFeedRows.Count} request={RequestRows.Count} filteredPlaceholders={filteredPlaceholders} final={GameFeedRows.Count}");
    }
    private void AddCurrency(string name, string abbr, string color, Dictionary<string, object> money, string key)
    {
        long.TryParse(GetString(money, key), out var amount);
        MoneyRows.Add(new CurrencyRowVm { Name = name, Abbrev = abbr, Color = color, Amount = amount });
    }

    private void NotifyHeader()
    {
        Notify(nameof(PlayerDisplayName));
        Notify(nameof(SessionSummary));
        Notify(nameof(IsAuthenticated));
    }

    private void NotifyCharacter()
    {
        Notify(nameof(CharacterName));
        Notify(nameof(CharacterRace));
        Notify(nameof(CharacterAge));
        Notify(nameof(CharacterHeight));
        Notify(nameof(CharacterDescription));
        Notify(nameof(CharacterBackstory));
        Notify(nameof(CharacterNameDisplay));
        Notify(nameof(CharacterRaceDisplay));
        Notify(nameof(CharacterAgeDisplay));
        Notify(nameof(CharacterHeightDisplay));
        Notify(nameof(CharacterDescriptionDisplay));
        Notify(nameof(CharacterBackstoryDisplay));
    }

    private ChatMessageRowVm? BuildChatMessageRow(Dictionary<string, object> map)
    {
        var sender = FirstNonEmpty(GetString(map, "senderDisplayName"), GetString(map, "senderUserId"), "Система");
        var text = FirstNonEmpty(GetString(map, "text"), GetString(map, "message"), GetString(map, "body"));
        var type = FirstNonEmpty(GetString(map, "type"), "Public");
        var createdRaw = FirstNonEmpty(GetString(map, "createdUtc"), GetString(map, "createdAt"), GetString(map, "at"));
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

    private static bool IsPlaceholderText(string text)
    {
        return string.Equals(text, "Нет сообщений", StringComparison.OrdinalIgnoreCase)
               || string.Equals(text, "Нет системных событий", StringComparison.OrdinalIgnoreCase)
               || string.Equals(text, "Нет видимых бросков", StringComparison.OrdinalIgnoreCase)
               || string.Equals(text, "Нет активных заявок", StringComparison.OrdinalIgnoreCase);
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

    private static string FirstNonEmpty(params string[] values) => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private static string GetString(Dictionary<string, object> map, string key) => map.ContainsKey(key) && map[key] != null ? Convert.ToString(map[key]) ?? string.Empty : string.Empty;

    private static string GetMapValueOrDefault(Dictionary<string, object> map, params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = GetString(map, key);
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return "0";
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
        var line = "[CHAT-DIAG][Player] " + message;
        ClientLogService.Instance.Info(line);
        ConnectionStatusDetail = line;
        Notify(nameof(ConnectionStatusDetail));
    }

    private static IList ToObjectList(object payload) => payload as IList ?? new ArrayList();
}

public class CombatViewModel : PlayerMainViewModel { }
