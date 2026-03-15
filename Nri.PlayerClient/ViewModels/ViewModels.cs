using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
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
    public bool Archived { get; set; }
    public bool IsActive { get; set; }
}

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

        _poller = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        _poller.Tick += (_, _) => RefreshAll();
    }

    public string LoginText { get; set; } = string.Empty;
    public string PasswordText { get; set; } = string.Empty;
    public string ConnectionState { get => _connectionState; set { _connectionState = value; Notify(); } }
    public string SelectedSection { get => _selectedSection; set { _selectedSection = value; Notify(); } }
    public string DiceFormulaInput { get; set; } = "1d20";
    public string DiceVisibilityInput { get; set; } = "Public";
    public string DiceDescriptionInput { get; set; } = string.Empty;
    public string SelectedRequestId { get; set; } = string.Empty;

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

    public ICommand LoginCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand ShowActiveCharacterCommand { get; }
    public ICommand ShowMyCharactersCommand { get; }
    public ICommand ShowClassesCommand { get; }
    public ICommand ShowSkillsCommand { get; }
    public ICommand CreateDiceRequestCommand { get; }
    public ICommand CancelRequestCommand { get; }

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

        ClassProgressRows.Clear();
        if (payload.ContainsKey("classProgress"))
            foreach (var item in ToObjectList(payload["classProgress"]))
                if (item is Dictionary<string, object> row)
                    ClassProgressRows.Add($"{GetString(row, "classCode")} L{GetString(row, "level")} XP:{GetString(row, "experience")}");

        SkillRows.Clear();
        if (payload.ContainsKey("skills"))
            foreach (var item in ToObjectList(payload["skills"]))
                if (item is Dictionary<string, object> row)
                    SkillRows.Add($"{GetString(row, "name")} [{GetString(row, "type")}] {(GetBool(row, "available") ? "Доступен" : "Недоступен: " + GetString(row, "reason"))}");
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

    private void NotifyAll()
    {
        Notify(nameof(CharacterName));
        Notify(nameof(CharacterRace));
        Notify(nameof(CharacterAge));
        Notify(nameof(CharacterHeight));
        Notify(nameof(CharacterDescription));
        Notify(nameof(CharacterBackstory));
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
