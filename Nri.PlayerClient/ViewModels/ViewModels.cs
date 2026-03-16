using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Web.Script.Serialization;
using System.Windows.Input;
using System.Windows.Threading;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Threading;
using System.Web.Script.Serialization;
using Nri.PlayerClient.Networking;
using Nri.Shared.Configuration;
using Nri.Shared.Contracts;

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
    private readonly Action _execute;
    public RelayCommand(Action execute) { _execute = execute; }
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => _execute();
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
    public ObservableCollection<string> InventoryRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<string> SkillsRows { get; } = new ObservableCollection<string>();
}

public class ClassNodeVisualVm
{
    public string NodeId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string State { get; set; } = "Locked";
    public double X { get; set; }
    public double Y { get; set; }
public class CompanionVm : ViewModelBase
{
    public string Name { get; set; } = string.Empty;
    public string Species { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public ObservableCollection<string> Inventory { get; } = new ObservableCollection<string>();
}

public class PlayerMainViewModel : ViewModelBase
{
    private readonly ClientSessionState _session = new ClientSessionState();
    private readonly CommandApi _api;
    private readonly DispatcherTimer _poller;

    private string _connectionState = "Оффлайн";
    private bool _isAuthPopupOpen;
    private string _selectedMainTab = "MyCharacters";

    public PlayerMainViewModel()
    {
        var client = new JsonTcpClient(new ClientConfig(), _session);
        _api = new CommandApi(client);

        ToggleAuthPopupCommand = new RelayCommand(() => IsAuthPopupOpen = !IsAuthPopupOpen);
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

        _poller = new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
        _poller.Tick += (_, _) => PollRefresh();

        InitializeClassVisualLayout();
        LoadLocalAudioSettings();
    private string _connectionState = "Вы в режиме оффлайн";
    private string _selectedSection = "ActiveCharacter";

    public PlayerMainViewModel()
    {
        var config = new ClientConfig();
        var client = new JsonTcpClient(config, _session);
        _api = new CommandApi(client);

        LoginCommand = new RelayCommand(Login);
        RefreshCommand = new RelayCommand(RefreshAll);
        ShowActiveCharacterCommand = new RelayCommand(() => SelectedSection = "ActiveCharacter");
        ShowMyCharactersCommand = new RelayCommand(() => SelectedSection = "MyCharacters");
        ShowClassesCommand = new RelayCommand(() => SelectedSection = "Classes");
        ShowSkillsCommand = new RelayCommand(() => SelectedSection = "Skills");
        CreateDiceRequestCommand = new RelayCommand(CreateDiceRequest);
        CancelRequestCommand = new RelayCommand(CancelRequest);
        AcquireClassNodeCommand = new RelayCommand(AcquireClassNode);
        AcquireSkillCommand = new RelayCommand(AcquireSkill);
        ChatSendCommand = new RelayCommand(ChatSend);
        ChatRefreshCommand = new RelayCommand(ChatRefresh);
        AudioRefreshCommand = new RelayCommand(AudioRefresh);
        AudioApplyLocalSettingsCommand = new RelayCommand(AudioApplyLocalSettings);
        VisibilityLoadCommand = new RelayCommand(VisibilityLoad);
        VisibilitySaveCommand = new RelayCommand(VisibilitySave);
        PublicCharacterLoadCommand = new RelayCommand(PublicCharacterLoad);
        NotesRefreshCommand = new RelayCommand(NotesRefresh);
        NotesCreateCommand = new RelayCommand(NotesCreate);
        NotesArchiveCommand = new RelayCommand(NotesArchive);

        LoadLocalAudioSettings();

        _poller = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        _poller.Tick += (_, _) => RefreshAll();
    }

    public string LoginText { get; set; } = string.Empty;
    public string PasswordText { get; set; } = string.Empty;
    public string PlayerDisplayName { get; set; } = "Гость";
    public string SessionSummary { get; set; } = "Сессия: default";

    public bool IsAuthPopupOpen { get => _isAuthPopupOpen; set { _isAuthPopupOpen = value; Notify(); } }
    public string ConnectionState { get => _connectionState; set { _connectionState = value; Notify(); Notify(nameof(IsOnline)); } }
    public bool IsOnline => string.Equals(ConnectionState, "Онлайн", StringComparison.OrdinalIgnoreCase);

    public string SelectedMainTab { get => _selectedMainTab; set { _selectedMainTab = value; Notify(); } }
    public string SelectedCharacterId { get; set; } = string.Empty;
    public string PublicViewCharacterId { get; set; } = string.Empty;

    public string CharacterName { get; set; } = string.Empty;
    public string CharacterRace { get; set; } = string.Empty;
    public string CharacterAge { get; set; } = string.Empty;
    public string CharacterHeight { get; set; } = string.Empty;
    public string CharacterDescription { get; set; } = string.Empty;
    public string CharacterBackstory { get; set; } = string.Empty;

    public bool VisHideDescription { get; set; }
    public bool VisHideBackstory { get; set; }
    public bool VisHideStats { get; set; }
    public bool VisHideReputation { get; set; }

    public int DiceCount { get; set; } = 1;
    public int DiceFaces { get; set; } = 20;
    public int DiceModifier { get; set; }
    public string DiceVisibilityInput { get; set; } = "Public";
    public string DiceDescriptionInput { get; set; } = string.Empty;
    public string SelectedRequestId { get; set; } = string.Empty;

    public string ChatSessionId { get; set; } = "default";
    public string ChatTypeInput { get; set; } = "Public";
    public string ChatTextInput { get; set; } = string.Empty;

    public string ConnectionState { get => _connectionState; set { _connectionState = value; Notify(); } }
    public string SelectedSection { get => _selectedSection; set { _selectedSection = value; Notify(); } }
    public string DiceFormulaInput { get; set; } = "1d20";
    public string DiceVisibilityInput { get; set; } = "Public";
    public string DiceDescriptionInput { get; set; } = string.Empty;
    public string SelectedRequestId { get; set; } = string.Empty;
    public string SessionIdInput { get; set; } = "default";
    public string SelectedCharacterId { get; set; } = string.Empty;
    public string SelectedClassNodeId { get; set; } = string.Empty;
    public string SelectedSkillId { get; set; } = string.Empty;
    public string ChatSessionId { get; set; } = "default";
    public string ChatTextInput { get; set; } = string.Empty;
    public string ChatTypeInput { get; set; } = "Public";
    public string ChatUnreadText { get; set; } = string.Empty;
    public string AudioSessionId { get; set; } = "default";
    public string AudioStateText { get; set; } = string.Empty;
    public double LocalVolume { get; set; } = 0.7;
    public bool LocalMuted { get; set; }

    public string PublicViewCharacterId { get; set; } = string.Empty;
    public bool VisHideDescription { get; set; }
    public bool VisHideBackstory { get; set; }
    public bool VisHideStats { get; set; }
    public bool VisHideReputation { get; set; }
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
    public ObservableCollection<CurrencyRowVm> MoneyRows { get; } = new ObservableCollection<CurrencyRowVm>();
    public ObservableCollection<string> InventoryRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<string> HoldingsRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<ReputationRowVm> ReputationRows { get; } = new ObservableCollection<ReputationRowVm>();
    public ObservableCollection<CompanionVm> Companions { get; } = new ObservableCollection<CompanionVm>();

    public ObservableCollection<string> SkillRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<string> SkillCatalogRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<ClassNodeVisualVm> ClassNodes { get; } = new ObservableCollection<ClassNodeVisualVm>();

    public ObservableCollection<string> ChatRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<string> EventRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<string> DiceFeedRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<string> RequestRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<string> SessionStateRows { get; } = new ObservableCollection<string>();

    public ObservableCollection<string> PublicCharacterRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<string> NoteRows { get; } = new ObservableCollection<string>();

    public ObservableCollection<int> DiceCountOptions { get; } = new ObservableCollection<int> { 1, 2, 3, 4, 5, 6 };
    public ObservableCollection<int> DiceFacesOptions { get; } = new ObservableCollection<int> { 4, 6, 8, 10, 12, 20 };
    public ObservableCollection<string> DiceVisibilityOptions { get; } = new ObservableCollection<string> { "Public", "MasterOnly", "Shadow" };
    public ObservableCollection<string> ChatTypeOptions { get; } = new ObservableCollection<string> { "Public", "System", "Whisper" };
    public ObservableCollection<string> NoteTargetTypeOptions { get; } = new ObservableCollection<string> { "character", "session", "campaign" };
    public ObservableCollection<string> NoteVisibilityOptions { get; } = new ObservableCollection<string> { "Personal", "SharedWithOwner", "SessionShared" };


    public ICommand ToggleAuthPopupCommand { get; }
    public ICommand LoginCommand { get; }
    public ICommand RegisterCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand LoadCharacterDetailsCommand { get; }
    public ICommand CreateDiceRequestCommand { get; }
    public ICommand CancelRequestCommand { get; }
    public ICommand ChatSendCommand { get; }
    public ICommand BottomRefreshCommand { get; }
    public string CharacterName { get; set; } = string.Empty;
    public string CharacterRace { get; set; } = string.Empty;
    public string CharacterAge { get; set; } = string.Empty;
    public string CharacterHeight { get; set; } = string.Empty;
    public string CharacterDescription { get; set; } = string.Empty;
    public string CharacterBackstory { get; set; } = string.Empty;

    public string StatsText { get; set; } = string.Empty;

    public ObservableCollection<string> MoneyRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<string> InventoryRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<CompanionVm> Companions { get; } = new ObservableCollection<CompanionVm>();
    public ObservableCollection<string> Holdings { get; } = new ObservableCollection<string>();
    public ObservableCollection<string> ReputationRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<string> ClassProgressRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<string> SkillRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<CharacterListItemVm> MyCharacters { get; } = new ObservableCollection<CharacterListItemVm>();
    public ObservableCollection<string> MyRequests { get; } = new ObservableCollection<string>();
    public ObservableCollection<string> DiceFeed { get; } = new ObservableCollection<string>();
    public ObservableCollection<string> CombatStateRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<string> CombatTimelineRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<string> ChatRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<string> PublicCharacterRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<string> NoteRows { get; } = new ObservableCollection<string>();

    public ICommand LoginCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand ShowActiveCharacterCommand { get; }
    public ICommand ShowMyCharactersCommand { get; }
    public ICommand ShowClassesCommand { get; }
    public ICommand ShowSkillsCommand { get; }
    public ICommand CreateDiceRequestCommand { get; }
    public ICommand CancelRequestCommand { get; }
    public ICommand AcquireClassNodeCommand { get; }
    public ICommand AcquireSkillCommand { get; }
    public ICommand ChatSendCommand { get; }
    public ICommand ChatRefreshCommand { get; }
    public ICommand AudioRefreshCommand { get; }
    public ICommand AudioApplyLocalSettingsCommand { get; }
    public ICommand VisibilityLoadCommand { get; }
    public ICommand VisibilitySaveCommand { get; }
    public ICommand PublicCharacterLoadCommand { get; }
    public ICommand NotesRefreshCommand { get; }
    public ICommand NotesCreateCommand { get; }
    public ICommand NotesArchiveCommand { get; }
    public ICommand AcquireClassNodeCommand { get; }
    public ICommand AcquireSkillCommand { get; }

    private void Login()
    {
        try
        {
            var result = _api.Login(LoginText, PasswordText);
            if (result.Status == ResponseStatus.Ok)
            {
                ConnectionState = "Онлайн";
                _poller.Start();
                RefreshAll();
                return;
            }

            ConnectionState = "Вы в режиме оффлайн";
        }
        catch
        {
            ConnectionState = "Вы в режиме оффлайн";
        }
    }

    private void RefreshAll()
    {
        try
        {
            var active = _api.GetActiveCharacter();
            if (active.Status == ResponseStatus.Ok && active.Payload.Count > 0)
            {
                ApplyCharacter(active.Payload, true);
            }

            var mine = _api.GetMyCharacters();
            MyCharacters.Clear();
            if (mine.Status == ResponseStatus.Ok && mine.Payload.ContainsKey("items"))
            {
                foreach (var item in ToObjectList(mine.Payload["items"]))
                {
                    var map = item as Dictionary<string, object>;
                    if (map == null) continue;
                    MyCharacters.Add(new CharacterListItemVm
                    {
                        Id = GetString(map, "characterId"),
                        Name = GetString(map, "name"),
                        Race = GetString(map, "race"),
                        Archived = GetBool(map, "archived")
                    });
                }
            }

            LoadRequestsAndFeed();
            LoadCombat();
            LoadClassAndSkillState();
            ChatRefresh();
            AudioRefresh();
            NotesRefresh();
            ConnectionState = "Онлайн";
            NotifyAll();
        }
        catch
        {
            ConnectionState = "Вы в режиме оффлайн";
            Notify(nameof(ConnectionState));
        }
    }

    private void ApplyCharacter(Dictionary<string, object> payload, bool markActive)
    {
        CharacterName = GetString(payload, "name");
        CharacterRace = GetString(payload, "race");
        CharacterAge = GetString(payload, "age");
        CharacterHeight = GetString(payload, "height");
        CharacterDescription = GetString(payload, "description");
        CharacterBackstory = GetString(payload, "backstory");

        StatsText = payload.ContainsKey("stats") && payload["stats"] is Dictionary<string, object> stats
            ? $"HP:{GetString(stats, "health")}, AP:{GetString(stats, "physicalArmor")}, AM:{GetString(stats, "magicalArmor")}, Morale:{GetString(stats, "morale")}, Str:{GetString(stats, "strength")}, Dex:{GetString(stats, "dexterity")}, End:{GetString(stats, "endurance")}, Wis:{GetString(stats, "wisdom")}, Int:{GetString(stats, "intellect")}, Cha:{GetString(stats, "charisma")}"
            : "[hidden]";

        MoneyRows.Clear();
        if (payload.ContainsKey("money") && payload["money"] is Dictionary<string, object> money)
            foreach (var entry in money) MoneyRows.Add($"{entry.Key}: {entry.Value}");

        InventoryRows.Clear();
        if (payload.ContainsKey("inventory"))
            foreach (var item in ToObjectList(payload["inventory"]))
                if (item is Dictionary<string, object> row)
                    InventoryRows.Add($"{GetString(row, "label")} x{GetString(row, "quantity")} ({GetString(row, "description")}) [dur={GetString(row, "durability")}, use={GetString(row, "consumptionPerUse")}, eq={GetString(row, "equipped")}] ");

        Companions.Clear();
        if (payload.ContainsKey("companions"))
        {
            foreach (var item in ToObjectList(payload["companions"]))
            {
                if (item is not Dictionary<string, object> row) continue;
                var vm = new CompanionVm { Name = GetString(row, "name"), Species = GetString(row, "species"), Notes = GetString(row, "notes") };
                if (row.ContainsKey("inventory"))
                    foreach (var inv in ToObjectList(row["inventory"]))
                        if (inv is Dictionary<string, object> invMap)
                            vm.Inventory.Add($"{GetString(invMap, "label")} x{GetString(invMap, "quantity")}");
                Companions.Add(vm);
            }
        }

        Holdings.Clear();
        if (payload.ContainsKey("holdings"))
            foreach (var item in ToObjectList(payload["holdings"]))
                if (item is Dictionary<string, object> row)
                    Holdings.Add($"{GetString(row, "name")}: {GetString(row, "description")}");

        ReputationRows.Clear();
        if (payload.ContainsKey("reputation"))
        {
            if (payload["reputation"] is string hidden)
            {
                ReputationRows.Add(hidden);
            }
            else
            {
                foreach (var item in ToObjectList(payload["reputation"]))
                    if (item is Dictionary<string, object> row)
                        ReputationRows.Add($"{GetString(row, "scope")}:{GetString(row, "groupKey")}={GetString(row, "value")}");
            }
        }

        if (markActive && payload.ContainsKey("characterId"))
        {
            SelectedCharacterId = GetString(payload, "characterId");
            Notify(nameof(SelectedCharacterId));
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
            if (!File.Exists(AudioSettingsPath)) return;
            var map = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(File.ReadAllText(AudioSettingsPath));
            if (map == null) return;
            if (map.ContainsKey("volume")) double.TryParse(Convert.ToString(map["volume"]), out var vol); else vol = 0.7;
            if (map.ContainsKey("muted")) bool.TryParse(Convert.ToString(map["muted"]), out var muted); else muted = false;
            LocalVolume = Math.Max(0, Math.Min(1, vol));
            LocalMuted = muted;
            var serializer = new JavaScriptSerializer();
            var map = serializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(AudioSettingsPath));
            if (map == null) return;
            var v = 0.7;
            if (map.ContainsKey("volume")) double.TryParse(Convert.ToString(map["volume"]), out v);
            var m = false;
            if (map.ContainsKey("muted")) bool.TryParse(Convert.ToString(map["muted"]), out m);
            LocalVolume = Math.Max(0, Math.Min(1, v));
            LocalMuted = m;
        }
        catch { }
    }

    private void ApplyAudioLocalSettings()
    {
        _api.AudioClientSettingsSet(LocalVolume, LocalMuted);
        try
        {
            File.WriteAllText(AudioSettingsPath, new JavaScriptSerializer().Serialize(new Dictionary<string, object>
            {
                { "volume", LocalVolume },
                { "muted", LocalMuted }
            }));
        }
        catch { }

        RefreshAudioState();
    }

    private void LoadVisibility()
    private void SaveLocalAudioSettings()
    {
        try
        {
            var serializer = new JavaScriptSerializer();
            File.WriteAllText(AudioSettingsPath, serializer.Serialize(new Dictionary<string, object> { { "volume", LocalVolume }, { "muted", LocalMuted } }));
        }
        catch { }
    }

    private void AudioRefresh()
    {
        var state = _api.AudioStateSync(AudioSessionId);
        AudioStateText = $"mode={GetString(state.Payload, "mode")}; category={GetString(state.Payload, "category")}; track={GetString(state.Payload, "trackName")}; pos={GetString(state.Payload, "positionSeconds")}; playback={GetString(state.Payload, "playbackState")}";
        Notify(nameof(AudioStateText));

        var local = _api.AudioClientSettingsGet();
        if (local.Status == ResponseStatus.Ok)
        {
            if (double.TryParse(GetString(local.Payload, "volume"), out var v)) LocalVolume = v;
            LocalMuted = GetString(local.Payload, "muted") == "True";
            Notify(nameof(LocalVolume));
            Notify(nameof(LocalMuted));
        }
    }

    private void AudioApplyLocalSettings()
    {
        _api.AudioClientSettingsSet(LocalVolume, LocalMuted);
        SaveLocalAudioSettings();
        AudioRefresh();
    }


    private void VisibilityLoad()
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
        Notify(nameof(VisHideDescription)); Notify(nameof(VisHideBackstory)); Notify(nameof(VisHideStats)); Notify(nameof(VisHideReputation));
    }

    private void VisibilitySave()
    {
        if (string.IsNullOrWhiteSpace(SelectedCharacterId)) return;
        _api.VisibilityUpdate(new Dictionary<string, object>{{"characterId",SelectedCharacterId},{"hideDescriptionForOthers",VisHideDescription},{"hideBackstoryForOthers",VisHideBackstory},{"hideStatsForOthers",VisHideStats},{"hideReputationForOthers",VisHideReputation}});
    }

    private void PublicCharacterLoad()
    {
        PublicCharacterRows.Clear();
        if (string.IsNullOrWhiteSpace(PublicViewCharacterId)) return;
        var r = _api.CharacterPublicViewGet(PublicViewCharacterId);
        foreach (var kv in r.Payload)
            PublicCharacterRows.Add(kv.Key + " = " + Convert.ToString(kv.Value));
    }

    private void RefreshNotes()
    {
        NoteRows.Clear();
        var r = _api.NotesList(new Dictionary<string, object>
        {
            { "sessionId", NoteSessionId },
            { "targetType", NoteTargetType },
            { "targetId", NoteTargetId }
        });

        foreach (var item in ToObjectList(r.Payload.ContainsKey("items") ? r.Payload["items"] : new ArrayList()))
            if (item is Dictionary<string, object> map)
                NoteRows.Add($"{GetString(map, "noteId")} | {GetString(map, "visibility")} | {GetString(map, "title")} | {GetString(map, "text")}");
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
        if (parameter is string nodeId && !string.IsNullOrWhiteSpace(nodeId))
            SelectedClassNodeId = nodeId;
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
        ClassNodes.Clear();
        ClassNodes.Add(new ClassNodeVisualVm { NodeId = "novice", Title = "Новичок", State = "Start", X = 220, Y = 120 });
        ClassNodes.Add(new ClassNodeVisualVm { NodeId = "defender_guard", Title = "Защитник", X = 220, Y = 20 });
        ClassNodes.Add(new ClassNodeVisualVm { NodeId = "vanguard_breach", Title = "Передовой", X = 315, Y = 70 });
        ClassNodes.Add(new ClassNodeVisualVm { NodeId = "ranger_hunt", Title = "Рейнджер", X = 315, Y = 170 });
        ClassNodes.Add(new ClassNodeVisualVm { NodeId = "samurai_focus", Title = "Самурай", X = 220, Y = 220 });
        ClassNodes.Add(new ClassNodeVisualVm { NodeId = "mage_channel", Title = "Маг", X = 125, Y = 170 });
        ClassNodes.Add(new ClassNodeVisualVm { NodeId = "inventor_gear", Title = "Изобретатель", X = 125, Y = 70 });
    }

    private void AddStat(string label, Dictionary<string, object> map, string key) => StatsRows.Add(new StatRowVm { Label = label, Value = GetString(map, key) });

    private void AddCurrency(string name, string abbr, string color, Dictionary<string, object> money, string key)
    {
        long.TryParse(GetString(money, key), out var amount);
        MoneyRows.Add(new CurrencyRowVm { Name = name, Abbrev = abbr, Color = color, Amount = amount });
    }

    private void NotifyHeader()
    {
        Notify(nameof(PlayerDisplayName));
        Notify(nameof(SessionSummary));
    }

    private void NotifyCharacter()
        foreach (var kv in r.Payload) PublicCharacterRows.Add(kv.Key + "=" + Convert.ToString(kv.Value));
    }

    private void NotesRefresh()
    {
        NoteRows.Clear();
        var r = _api.NotesList(new Dictionary<string, object>{{"sessionId",NoteSessionId},{"targetType",NoteTargetType},{"targetId",NoteTargetId}});
        foreach (var item in ToObjectList(r.Payload.ContainsKey("items") ? r.Payload["items"] : new ArrayList()))
            if (item is Dictionary<string, object> m)
                NoteRows.Add($"{GetString(m,"noteId")} | {GetString(m,"visibility")} | {GetString(m,"title")} | {GetString(m,"text")}");
    }

    private void NotesCreate()
    {
        _api.NotesCreate(new Dictionary<string, object>{{"sessionId",NoteSessionId},{"targetType",NoteTargetType},{"targetId",NoteTargetId},{"title",NoteTitle},{"text",NoteText},{"visibility",NoteVisibility},{"noteType","Personal"}});
        NotesRefresh();
    }

    private void NotesArchive() { if(!string.IsNullOrWhiteSpace(SelectedNoteId)){ _api.NotesArchive(SelectedNoteId); NotesRefresh(); } }

    private void ChatSend()
    {
        if (string.IsNullOrWhiteSpace(ChatTextInput)) return;
        _api.ChatSend(ChatSessionId, ChatTypeInput, ChatTextInput);
        ChatTextInput = string.Empty;
        Notify(nameof(ChatTextInput));
        ChatRefresh();
    }

    private void ChatRefresh()
    {
        ChatRows.Clear();
        var history = _api.ChatHistoryGet(ChatSessionId, 80);
        if (history.Status == ResponseStatus.Ok && history.Payload.ContainsKey("items"))
        {
            foreach (var item in ToObjectList(history.Payload["items"]))
                if (item is Dictionary<string, object> m)
                    ChatRows.Add($"{GetString(m, "createdUtc")} | {GetString(m, "type")} | {GetString(m, "senderDisplayName")}: {GetString(m, "text")}");
        }
        var unread = _api.ChatUnreadGet(ChatSessionId);
        ChatUnreadText = "Unread: " + GetString(unread.Payload, "count");
        Notify(nameof(ChatUnreadText));
    }

    private void LoadClassAndSkillState()
    {
        if (string.IsNullOrWhiteSpace(SelectedCharacterId) && MyCharacters.Count > 0) SelectedCharacterId = MyCharacters[0].Id;
        if (string.IsNullOrWhiteSpace(SelectedCharacterId)) return;

        ClassProgressRows.Clear();
        var tree = _api.ClassTreeGet(SelectedCharacterId);
        if (tree.Status == ResponseStatus.Ok)
        {
            foreach (var dir in ToObjectList(tree.Payload.ContainsKey("directions") ? tree.Payload["directions"] : new ArrayList()))
            {
                if (dir is not Dictionary<string, object> d) continue;
                ClassProgressRows.Add($"[{GetString(d, "directionId")}] branch={GetString(d, "selectedBranchId")}");
                foreach (var n in ToObjectList(d.ContainsKey("acquiredNodes") ? d["acquiredNodes"] : new ArrayList()))
                    if (n is Dictionary<string, object> nm)
                        ClassProgressRows.Add($"  + {GetString(nm, "nodeId")}");
            }
        }

        var available = _api.ClassTreeAvailable(SelectedCharacterId);
        if (available.Status == ResponseStatus.Ok && available.Payload.ContainsKey("items"))
        {
            foreach (var node in ToObjectList(available.Payload["items"]))
            {
                if (node is not Dictionary<string, object> n) continue;
                if (GetString(n, "available") == "True")
                    ClassProgressRows.Add($"AVAILABLE {GetString(n, "nodeId")} | {GetString(n, "name")}");
                else if (GetString(n, "acquired") != "True")
                    ClassProgressRows.Add($"LOCKED {GetString(n, "nodeId")} | reason(s): {GetString(n, "reasons")}");
            }
        }

        SkillRows.Clear();
        var skills = _api.SkillsList(SelectedCharacterId);
        if (skills.Status == ResponseStatus.Ok && skills.Payload.ContainsKey("items"))
        {
            foreach (var item in ToObjectList(skills.Payload["items"]))
            {
                if (item is not Dictionary<string, object> row) continue;
                SkillRows.Add($"{GetString(row, "name")} [{GetString(row, "type")}] acquired={GetString(row, "acquired")} available={GetString(row, "available")} reason={GetString(row, "reason")}");
            }
        }
    }

    private void LoadCombat()
    {
        CombatStateRows.Clear();
        var state = _api.CombatVisibleState(SessionIdInput);
        if (state.Status == ResponseStatus.Ok)
        {
            CombatStateRows.Add($"Status: {GetString(state.Payload, "status")}");
            CombatStateRows.Add($"Round: {GetString(state.Payload, "round")}");
            CombatStateRows.Add($"ActiveSlot: {GetString(state.Payload, "activeSlotId")}");
            if (state.Payload.ContainsKey("slots"))
            {
                foreach (var slot in ToObjectList(state.Payload["slots"]))
                    if (slot is Dictionary<string, object> map)
                        CombatStateRows.Add($"Slot {GetString(map, "order")} grp={GetString(map, "isGroup")} maxRoll={GetString(map, "maxRoll")}");
            }
        }

        CombatTimelineRows.Clear();
        var timeline = _api.CombatTimeline(SessionIdInput);
        if (timeline.Status == ResponseStatus.Ok && timeline.Payload.ContainsKey("items"))
        {
            foreach (var item in ToObjectList(timeline.Payload["items"]))
                if (item is Dictionary<string, object> map)
                    CombatTimelineRows.Add($"{GetString(map, "at")} | {GetString(map, "eventType")} | {GetString(map, "message")}");
        }
    }

    private void LoadRequestsAndFeed()
    {
        MyRequests.Clear();
        var req = _api.ListMyRequests();
        if (req.Status == ResponseStatus.Ok && req.Payload.ContainsKey("items"))
        {
            foreach (var item in ToObjectList(req.Payload["items"]))
            {
                if (item is Dictionary<string, object> map)
                {
                    var id = GetString(map, "requestId");
                    var status = GetString(map, "status");
                    var formula = GetString(map, "formula");
                    var resultText = string.Empty;
                    if (map.ContainsKey("result") && map["result"] is Dictionary<string, object> result)
                        resultText = $" => {GetString(result, "total")}";
                    MyRequests.Add($"{id} | {status} | {formula}{resultText}");
                }
            }
        }

        DiceFeed.Clear();
        var feed = _api.DiceVisibleFeed();
        if (feed.Status == ResponseStatus.Ok && feed.Payload.ContainsKey("items"))
        {
            foreach (var item in ToObjectList(feed.Payload["items"]))
            {
                if (item is Dictionary<string, object> map)
                {
                    var formula = GetString(map, "formula");
                    var creator = GetString(map, "creatorUserId");
                    var total = string.Empty;
                    if (map.ContainsKey("result") && map["result"] is Dictionary<string, object> result)
                        total = GetString(result, "total");
                    DiceFeed.Add($"{creator}: {formula} => {total}");
                }
            }
        }
    }

    private void CreateDiceRequest()
    {
        try
        {
            var charId = string.Empty;
            if (MyCharacters.Count > 0) charId = MyCharacters[0].Id;
            _api.CreateDiceRequest(charId, DiceFormulaInput, DiceVisibilityInput, DiceDescriptionInput);
            LoadRequestsAndFeed();
        }
        catch
        {
            ConnectionState = "Вы в режиме оффлайн";
        }
    }

    private void CancelRequest()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(SelectedRequestId))
            {
                _api.CancelRequest(SelectedRequestId);
                LoadRequestsAndFeed();
            }
        }
        catch
        {
            ConnectionState = "Вы в режиме оффлайн";
        }
    }


    private void AcquireClassNode()
    {
        if (string.IsNullOrWhiteSpace(SelectedCharacterId) || string.IsNullOrWhiteSpace(SelectedClassNodeId)) return;
        _api.ClassTreeAcquireNode(SelectedCharacterId, SelectedClassNodeId);
        LoadClassAndSkillState();
    }

    private void AcquireSkill()
    {
        if (string.IsNullOrWhiteSpace(SelectedCharacterId) || string.IsNullOrWhiteSpace(SelectedSkillId)) return;
        _api.SkillsAcquire(SelectedCharacterId, SelectedSkillId);
        LoadClassAndSkillState();
    }

    private void NotifyAll()
    {
        Notify(nameof(CharacterName));
        Notify(nameof(CharacterRace));
        Notify(nameof(CharacterAge));
        Notify(nameof(CharacterHeight));
        Notify(nameof(CharacterDescription));
        Notify(nameof(CharacterBackstory));
    }

    private static string GetString(Dictionary<string, object> map, string key) => map.ContainsKey(key) && map[key] != null ? Convert.ToString(map[key]) ?? string.Empty : string.Empty;

    private static IList ToObjectList(object payload) => payload as IList ?? new ArrayList();
}

        Notify(nameof(StatsText));
        Notify(nameof(ConnectionState));
    }

    private static string GetString(Dictionary<string, object> map, string key) => map.ContainsKey(key) && map[key] != null ? Convert.ToString(map[key]) ?? string.Empty : string.Empty;
    private static bool GetBool(Dictionary<string, object> map, string key) => map.ContainsKey(key) && bool.TryParse(Convert.ToString(map[key]), out var v) && v;

    private static IList ToObjectList(object payload)
    {
        if (payload is IList list) return list;
        return new ArrayList();
    }
}


public class CombatViewModel : PlayerMainViewModel { }
