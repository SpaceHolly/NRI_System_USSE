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

        _poller = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        _poller.Tick += (_, _) => RefreshAll();
    }

    public string LoginText { get; set; } = string.Empty;
    public string PasswordText { get; set; } = string.Empty;
    public string ConnectionState { get => _connectionState; set { _connectionState = value; Notify(); } }

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

    private void LoadPendingRequests()
    {
        PendingRequests.Clear();
        var r = _api.ListPendingRequests();
        if (r.Status != ResponseStatus.Ok || !r.Payload.ContainsKey("items")) return;
        foreach (var obj in ToList(r.Payload["items"]))
        {
            if (obj is not Dictionary<string, object> m) continue;
            PendingRequests.Add(new RowVm
            {
                Id = S(m, "requestId"),
                Name = S(m, "requestType"),
                State = S(m, "status"),
                Extra = S(m, "formula")
            });
        }
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
    }

    private void CombatStart()
    {
        var participants = new[]
        {
            new Dictionary<string, object>
            {
                {"kind","Npc"}, {"entityId","npc-1"}, {"displayName","NPC-1"}, {"ownerUserId",""}
            }
        };
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
        var participants = new[]
        {
            new Dictionary<string, object>
            {
                {"kind",NewParticipantKind}, {"entityId",Guid.NewGuid().ToString("N")}, {"displayName",NewParticipantName}, {"ownerUserId",""}
            }
        };
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
    }

    private void ChatMuteUser()
    {
        if (string.IsNullOrWhiteSpace(ChatModerationUserId)) return;
        _api.ChatRestrictionsMuteUser(ChatSessionId, ChatModerationUserId, ChatModerationReason);
        ChatRefresh();
    }

    private void ChatUnmuteUser()
    {
        if (string.IsNullOrWhiteSpace(ChatModerationUserId)) return;
        _api.ChatRestrictionsUnmuteUser(ChatSessionId, ChatModerationUserId);
        ChatRefresh();
    }

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
    }

    private void AudioSetMode()
    {
        _api.AudioModeSet(AudioSessionId, AudioModeInput, AudioCategoryInput);
        AudioRefresh();
    }

    private void AudioClearOverride() { _api.AudioOverrideClear(AudioSessionId); AudioRefresh(); }
    private void AudioNextTrack() { _api.AudioTrackNext(AudioSessionId); AudioRefresh(); }
    private void AudioSelectTrack() { if (!string.IsNullOrWhiteSpace(AudioSelectedTrackId)) { _api.AudioTrackSelect(AudioSessionId, AudioSelectedTrackId); AudioRefresh(); } }
    private void AudioReloadLibrary() { _api.AudioTrackReload(); AudioRefresh(); }

    private void ApproveRequest()
    {
        if (string.IsNullOrWhiteSpace(SelectedPendingRequestId)) return;
        _api.ApproveRequest(SelectedPendingRequestId, RequestComment);
        RefreshAll();
    }

    private void RejectRequest()
    {
        if (string.IsNullOrWhiteSpace(SelectedPendingRequestId)) return;
        _api.RejectRequest(SelectedPendingRequestId, RequestComment);
        RefreshAll();
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


public class CombatTrackerViewModel : AdminMainViewModel { }
