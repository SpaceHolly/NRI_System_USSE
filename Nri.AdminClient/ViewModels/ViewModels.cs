using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Threading;
using Nri.AdminClient.Networking;
using Nri.Shared.Configuration;
using Nri.Shared.Contracts;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Nri.AdminClient.ViewModels;

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

public class RowVm : ViewModelBase
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Extra { get; set; } = string.Empty;
}

public class AdminMainViewModel : ViewModelBase
{
    private readonly ClientSessionState _session = new ClientSessionState();
    private readonly CommandApi _api;
    private readonly DispatcherTimer _poller;
    private string _connectionState = "Вы в режиме оффлайн";

    public AdminMainViewModel()
    {
        var client = new JsonTcpClient(new ClientConfig(), _session);
        _api = new CommandApi(client);

        LoginCommand = new RelayCommand(Login);
        RefreshCommand = new RelayCommand(RefreshAll);
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

        _poller = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        _poller.Tick += (_, _) => RefreshAll();
    }

    public string LoginText { get; set; } = string.Empty;
    public string PasswordText { get; set; } = string.Empty;
    public string ConnectionState { get => _connectionState; set { _connectionState = value; Notify(); } }

    public string SelectedPendingAccountId { get; set; } = string.Empty;
    public string SelectedOwnerUserId { get; set; } = string.Empty;
    public string SelectedCharacterId { get; set; } = string.Empty;
    public string LockStateText { get; set; } = string.Empty;

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

    public ICommand LoginCommand { get; }
    public ICommand RefreshCommand { get; }
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

    private void Login()
    {
        try
        {
            var r = _api.Login(LoginText, PasswordText);
            if (r.Status == ResponseStatus.Ok)
            {
                ConnectionState = "Онлайн";
                _poller.Start();
                RefreshAll();
            }
            else ConnectionState = "Вы в режиме оффлайн";
        }
        catch { ConnectionState = "Вы в режиме оффлайн"; }
    }

    private void RefreshAll()
    {
        try
        {
            LoadPending();
            LoadPlayers();
            ConnectionState = "Онлайн";
            Notify(nameof(ConnectionState));
        }
        catch
        {
            ConnectionState = "Вы в режиме оффлайн";
            Notify(nameof(ConnectionState));
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
                ReputationRows.Add($"{S(m, "scope")}:{S(m, "groupKey")}={S(m, "value")}");

        CompanionRows.Clear();
        foreach (var item in ToList(r.Payload.ContainsKey("companions") ? r.Payload["companions"] : new ArrayList()))
            if (item is Dictionary<string, object> m)
                CompanionRows.Add($"{S(m, "name")} ({S(m, "species")})");

        NotifyAllEditor();
    }

    private void AcquireLock()
    {
        var r = _api.AcquireCharacterLock(SelectedCharacterId);
        LockStateText = r.Message;
        Notify(nameof(LockStateText));
    }

    private void ReleaseLock()
    {
        var r = _api.ReleaseCharacterLock(SelectedCharacterId);
        LockStateText = r.Message;
        Notify(nameof(LockStateText));
    }

    private void ForceUnlock()
    {
        var r = _api.ForceReleaseCharacterLock(SelectedCharacterId);
        LockStateText = r.Message;
        Notify(nameof(LockStateText));
    }

    private void SaveBasicInfo()
    {
        _api.UpdateCharacterBasicInfo(new Dictionary<string, object>
        {
            { "characterId", SelectedCharacterId }, { "name", EditName }, { "race", EditRace }, { "height", EditHeight }, { "age", EditAge }, { "description", EditDescription }, { "backstory", EditBackstory }
        });
    }

    private void SaveStats()
    {
        _api.UpdateCharacterStats(new Dictionary<string, object>
        {
            { "characterId", SelectedCharacterId }, { "health", Health }, { "physicalArmor", PhysicalArmor }, { "magicalArmor", MagicalArmor }, { "morale", Morale }, { "strength", Strength }, { "dexterity", Dexterity }, { "endurance", Endurance }, { "wisdom", Wisdom }, { "intellect", Intellect }, { "charisma", Charisma }
        });
    }

    private void SaveMoney()
    {
        _api.UpdateCharacterMoney(new Dictionary<string, object>
        {
            { "characterId", SelectedCharacterId },
            { "money", new Dictionary<string, object> { { "Iron", Iron }, { "Bronze", Bronze }, { "Silver", Silver }, { "Gold", Gold } } }
        });
    }

    private void ApproveSelected() { if (!string.IsNullOrWhiteSpace(SelectedPendingAccountId)) _api.ApproveAccount(SelectedPendingAccountId); RefreshAll(); }
    private void ArchiveSelected() { if (!string.IsNullOrWhiteSpace(SelectedPendingAccountId)) _api.ArchiveAccount(SelectedPendingAccountId); RefreshAll(); }

    private void NotifyAllEditor()
    {
        Notify(nameof(EditName)); Notify(nameof(EditRace)); Notify(nameof(EditHeight)); Notify(nameof(EditAge)); Notify(nameof(EditDescription)); Notify(nameof(EditBackstory));
        Notify(nameof(Health)); Notify(nameof(PhysicalArmor)); Notify(nameof(MagicalArmor)); Notify(nameof(Morale)); Notify(nameof(Strength)); Notify(nameof(Dexterity)); Notify(nameof(Endurance)); Notify(nameof(Wisdom)); Notify(nameof(Intellect)); Notify(nameof(Charisma));
        Notify(nameof(Iron)); Notify(nameof(Bronze)); Notify(nameof(Silver)); Notify(nameof(Gold));
    }

    private static IList ToList(object value) => value as IList ?? new ArrayList();
    private static string S(Dictionary<string, object> map, string key) => map.ContainsKey(key) && map[key] != null ? Convert.ToString(map[key]) ?? string.Empty : string.Empty;
}

    protected void Notify([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class LoginViewModel : ViewModelBase
{
    private string _login = string.Empty;
    public string Login
    {
        get => _login;
        set
        {
            _login = value;
            Notify();
        }
    }
}

public class AdminDashboardViewModel : ViewModelBase { }
public class CharacterViewModel : ViewModelBase { }
public class RequestsViewModel : ViewModelBase { }
public class CombatViewModel : ViewModelBase { }
public class ChatPanelViewModel : ViewModelBase { }
public class AudioPanelViewModel : ViewModelBase { }
