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
    public ObservableCollection<StatRowVm> StatsRows { get; } = new ObservableCollection<StatRowVm>();
    public ObservableCollection<string> InventoryRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<string> HoldingsRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<string> SkillsRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<string> ClassRows { get; } = new ObservableCollection<string>();
}

public class GameFeedItemVm
{
    public string Kind { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
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
        InitializeDefaultCharacterScaffolding();
        LoadLocalAudioSettings();
    }

    public string LoginText { get; set; } = string.Empty;
    public string PasswordText { get; set; } = string.Empty;
    public string PlayerDisplayName { get; set; } = "Гость";
    public string SessionSummary { get; set; } = "Сессия: default";

    public bool IsAuthPopupOpen { get => _isAuthPopupOpen; set { _isAuthPopupOpen = value; Notify(); } }
    public string ConnectionState { get => _connectionState; set { _connectionState = value; Notify(); Notify(nameof(IsOnline)); Notify(nameof(IsAuthenticated)); } }
    public bool IsOnline => string.Equals(ConnectionState, "Онлайн", StringComparison.OrdinalIgnoreCase);
    public bool IsAuthenticated => IsOnline && !string.Equals(PlayerDisplayName, "Гость", StringComparison.OrdinalIgnoreCase);

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
    public ObservableCollection<GameFeedItemVm> GameFeedRows { get; } = new ObservableCollection<GameFeedItemVm>();

    public ObservableCollection<string> PublicCharacterRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<string> NoteRows { get; } = new ObservableCollection<string>();
    public ObservableCollection<string> AdminNoteRows { get; } = new ObservableCollection<string>();

    public ObservableCollection<string> DiceVisibilityOptions { get; } = new ObservableCollection<string> { "Public", "MasterOnly", "Shadow" };
    public ObservableCollection<string> ChatTypeOptions { get; } = new ObservableCollection<string> { "Общее", "Скрытое админам", "Только админам", "Системное" };
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
            if (result.Status != ResponseStatus.Ok)
            {
                ConnectionState = "Оффлайн";
                return;
            }

            ConnectionState = "Онлайн";
            IsAuthPopupOpen = false;
            PlayerDisplayName = LoginText;
            SessionSummary = "Сессия: default";
            RefreshAll();
            _poller.Start();
        }
        catch
        {
            ConnectionState = "Оффлайн";
        }
    }

    private void Register()
    {
        try
        {
            _api.Register(LoginText, PasswordText);
        }
        catch
        {
            ConnectionState = "Оффлайн";
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
            ConnectionState = "Онлайн";
        }
        catch
        {
            ConnectionState = "Оффлайн";
        }
    }

    private void PollRefresh()
    {
        try
        {
            RefreshBottomPanel();
        }
        catch
        {
            ConnectionState = "Оффлайн";
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

        MoneyRows.Clear();
        var money = payload.ContainsKey("money") && payload["money"] is Dictionary<string, object> loadedMoney
            ? loadedMoney
            : new Dictionary<string, object>();
        AddCurrency("Железная", "Fe", "#B0BEC5", money, "Iron");
        AddCurrency("Бронзовая", "Br", "#B87333", money, "Bronze");
        AddCurrency("Серебряная", "Ag", "#C0C0C0", money, "Silver");
        AddCurrency("Золотая", "Au", "#FFD700", money, "Gold");
        AddCurrency("Платиновая", "Pt", "#E5E4E2", money, "Platinum");
        AddCurrency("Орихалк", "Or", "#8A2BE2", money, "Orichalcum");
        AddCurrency("Адамант", "Ad", "#5F9EA0", money, "Adamant");
        AddCurrency("Государева", "Sov", "#1E90FF", money, "Sovereign");

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
            vm.StatsRows.Add(new StatRowVm { Label = "Здоровье", Value = "—" });
            vm.StatsRows.Add(new StatRowVm { Label = "Броня", Value = "—" });
            vm.StatsRows.Add(new StatRowVm { Label = "Мораль", Value = "—" });

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
            EnsureCollectionPlaceholder(vm.ClassRows, "Нет данных по классам");

            Companions.Add(vm);
        }

        EnsureCollectionPlaceholder(InventoryRows, "Нет данных по инвентарю");
        EnsureCollectionPlaceholder(HoldingsRows, "Нет данных по владениям");
        EnsureReputationPlaceholder();
        EnsureCompanionsPlaceholder();

        NotifyCharacter();
    }

    private void CreateDiceRequest()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(SelectedCharacterId) && MyCharacters.Count > 0) SelectedCharacterId = MyCharacters[0].Id;
            var formula = DiceCount + "d" + DiceFaces + (DiceModifier == 0 ? string.Empty : DiceModifier > 0 ? "+" + DiceModifier : DiceModifier.ToString());
            _api.CreateDiceRequest(SelectedCharacterId, formula, DiceVisibilityInput, DiceDescriptionInput);
            RefreshBottomPanel();
        }
        catch { ConnectionState = "Оффлайн"; }
    }

    private void CancelRequest()
    {
        if (string.IsNullOrWhiteSpace(SelectedRequestId)) return;
        try
        {
            _api.CancelRequest(SelectedRequestId);
            RefreshBottomPanel();
        }
        catch { ConnectionState = "Оффлайн"; }
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
        _api.ChatSend(ChatSessionId, ToServerChatType(ChatTypeInput), ChatTextInput);
        ChatTextInput = string.Empty;
        Notify(nameof(ChatTextInput));
        RefreshChat();
    }

    private void RefreshChat()
    {
        ChatRows.Clear();
        var chat = _api.ChatVisibleFeed(ChatSessionId, 80);
        foreach (var item in ToObjectList(chat.Payload.ContainsKey("items") ? chat.Payload["items"] : new ArrayList()))
        {
            if (item is not Dictionary<string, object> map) continue;
            ChatRows.Add($"{GetString(map, "createdUtc")} | {GetString(map, "type")} | {GetString(map, "senderDisplayName")}: {GetString(map, "text")}");
        }

        EnsureCollectionPlaceholder(ChatRows, "Нет сообщений");
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
        if (string.IsNullOrWhiteSpace(PublicViewCharacterId)) return;
        var r = _api.CharacterPublicViewGet(PublicViewCharacterId);
        foreach (var kv in r.Payload)
            PublicCharacterRows.Add(kv.Key + " = " + Convert.ToString(kv.Value));
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
        ClassNodes.Add(new ClassNodeVisualVm { NodeId = "novice", Title = "Новичок", State = "Start", X = 190, Y = 120 });
        ClassNodes.Add(new ClassNodeVisualVm { NodeId = "defender_branch_1", Title = "Ветвь защитника I", X = 190, Y = 12 });
        ClassNodes.Add(new ClassNodeVisualVm { NodeId = "vanguard_branch_1", Title = "Ветвь передового I", X = 322, Y = 68 });
        ClassNodes.Add(new ClassNodeVisualVm { NodeId = "ranger_branch_1", Title = "Ветвь рейнджера I", X = 322, Y = 186 });
        ClassNodes.Add(new ClassNodeVisualVm { NodeId = "samurai_branch_1", Title = "Ветвь самурая I", X = 190, Y = 242 });
        ClassNodes.Add(new ClassNodeVisualVm { NodeId = "mage_branch_1", Title = "Ветвь мага I", X = 58, Y = 186 });
        ClassNodes.Add(new ClassNodeVisualVm { NodeId = "inventor_branch_1", Title = "Ветвь изобретателя I", X = 58, Y = 68 });
    }

    private void AddStat(string label, Dictionary<string, object> map, string key) => StatsRows.Add(new StatRowVm { Label = label, Value = string.IsNullOrWhiteSpace(GetString(map, key)) ? "0" : GetString(map, key) });


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
            AddCurrency("Орихалк", "Or", "#8A2BE2", empty, "Orichalcum");
            AddCurrency("Адамант", "Ad", "#5F9EA0", empty, "Adamant");
            AddCurrency("Государева", "Sov", "#1E90FF", empty, "Sovereign");
        }

        EnsureCollectionPlaceholder(InventoryRows, "Данные инвентаря не загружены");
        EnsureCollectionPlaceholder(HoldingsRows, "Данные владений не загружены");
        EnsureReputationPlaceholder();
        EnsureCompanionsPlaceholder();
        EnsureCollectionPlaceholder(NoteRows, "Нет заметок");
        EnsureCollectionPlaceholder(AdminNoteRows, "Нет советов от админов");
        BuildGameFeed();
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
            vm.StatsRows.Add(new StatRowVm { Label = "Здоровье", Value = "—" });
            vm.InventoryRows.Add("Нет данных");
            vm.HoldingsRows.Add("Нет данных");
            vm.SkillsRows.Add("Нет данных");
            vm.ClassRows.Add("Нет данных");
            Companions.Add(vm);
        }
    }

    private string ToServerChatType(string uiType)
    {
        return uiType switch
        {
            "Общее" => "Public",
            "Скрытое админам" => "HiddenToAdmins",
            "Только админам" => "AdminOnly",
            "Системное" => "System",
            _ => "Public"
        };
    }

    private void BuildGameFeed()
    {
        GameFeedRows.Clear();
        foreach (var item in ChatRows) GameFeedRows.Add(new GameFeedItemVm { Kind = "Chat", Text = item });
        foreach (var item in EventRows) GameFeedRows.Add(new GameFeedItemVm { Kind = "System", Text = item });
        foreach (var item in DiceFeedRows) GameFeedRows.Add(new GameFeedItemVm { Kind = "Dice", Text = item });
        foreach (var item in RequestRows) GameFeedRows.Add(new GameFeedItemVm { Kind = "Request", Text = item });
        if (GameFeedRows.Count == 0)
            GameFeedRows.Add(new GameFeedItemVm { Kind = "System", Text = "Лента пуста" });
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
    }

    private static string GetString(Dictionary<string, object> map, string key) => map.ContainsKey(key) && map[key] != null ? Convert.ToString(map[key]) ?? string.Empty : string.Empty;

    private static IList ToObjectList(object payload) => payload as IList ?? new ArrayList();
}

public class CombatViewModel : PlayerMainViewModel { }
