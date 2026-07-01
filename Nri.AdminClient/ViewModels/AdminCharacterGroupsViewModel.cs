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

public sealed class AdminCharacterGroupsViewModel : ViewModelBase
{
    private readonly CommandApi _api;
    private string _campaignId = "default";
    private string _sessionId = string.Empty;
    private string _statusMessage = "Группы персонажей готовы к подключению. Включите флаги функций Character Groups MVP для работы.";
    private string _warningMessage = string.Empty;
    private string _errorMessage = string.Empty;
    private bool _isLoading;
    private bool _isEnabled;
    private bool _isMembershipEnabled;
    private bool _isActiveGroupEnabled;
    private bool _isPlayerViewEnabled;
    private CharacterGroupUiItem? _selectedGroup;
    private CharacterGroupMemberUiItem? _selectedMember;
    private string _groupName = "Новая группа";
    private string _groupDescription = string.Empty;
    private string _groupType = CharacterGroupTypeIds.Party;
    private string _groupStatus = CharacterGroupStatusIds.Draft;
    private string _groupVisibilityMode = MapVisibilityModes.Party;
    private bool _groupPlayerVisible;
    private string _groupPublicNotes = string.Empty;
    private string _groupGmNotes = string.Empty;
    private string _newMemberEntityType = CharacterGroupEntityTypeIds.PlayerCharacter;
    private string _newMemberEntityId = string.Empty;
    private string _newMemberDisplayName = string.Empty;
    private string _newMemberRoleInGroup = CharacterGroupRoleInGroupIds.Member;
    private string _newMemberCharacterRole = CharacterGroupCharacterRoleIds.PlayerCharacter;
    private bool _newMemberPlayerVisible = true;
    private string _newMemberVisibilityMode = MapVisibilityModes.Party;
    private bool _newMemberIsLeader;
    private string _memberPublicNotes = string.Empty;
    private string _memberGmNotes = string.Empty;
    private DateTime _lastRefreshAtUtc;

    public AdminCharacterGroupsViewModel(CommandApi api)
    {
        _api = api;
        RefreshFlagsCommand = new RelayCommand(RefreshFlags);
        RefreshGroupsCommand = new RelayCommand(RefreshGroups);
        CreateGroupCommand = new RelayCommand(CreateGroup);
        LoadSelectedGroupCommand = new RelayCommand(LoadSelectedGroup);
        SaveGroupCommand = new RelayCommand(SaveGroup);
        ArchiveGroupCommand = new RelayCommand(ArchiveGroup);
        SetActiveCommand = new RelayCommand(SetActiveGroup);
        ClearActiveCommand = new RelayCommand(ClearActiveGroup);
        AddMemberCommand = new RelayCommand(AddMember);
        SaveMemberCommand = new RelayCommand(SaveMember);
        RemoveMemberCommand = new RelayCommand(RemoveMember);
        MoveMemberUpCommand = new RelayCommand(() => MoveMember(-10));
        MoveMemberDownCommand = new RelayCommand(() => MoveMember(10));
        ClearErrorCommand = new RelayCommand(() => { ErrorMessage = string.Empty; WarningMessage = string.Empty; });
    }

    public ObservableCollection<CharacterGroupUiItem> Groups { get; } = new();
    public ObservableCollection<CharacterGroupMemberUiItem> Members { get; } = new();
    public ObservableCollection<GroupOptionUiItem> GroupTypeOptions { get; } = new()
    {
        Opt(CharacterGroupTypeIds.Party, "Партия"),
        Opt(CharacterGroupTypeIds.NpcGroup, "NPC-группа"),
        Opt(CharacterGroupTypeIds.CompanionGroup, "Компаньоны"),
        Opt(CharacterGroupTypeIds.EnemyGroup, "Враги"),
        Opt(CharacterGroupTypeIds.NeutralGroup, "Нейтральные"),
        Opt(CharacterGroupTypeIds.EscortGroup, "Сопровождение"),
        Opt(CharacterGroupTypeIds.TemporaryGroup, "Временная группа"),
        Opt(CharacterGroupTypeIds.Custom, "Другое")
    };
    public ObservableCollection<GroupOptionUiItem> GroupStatusOptions { get; } = new()
    {
        Opt(CharacterGroupStatusIds.Draft, "Черновик"),
        Opt(CharacterGroupStatusIds.Active, "Активна"),
        Opt(CharacterGroupStatusIds.Inactive, "Неактивна"),
        Opt(CharacterGroupStatusIds.Disbanded, "Расформирована")
    };
    public ObservableCollection<GroupOptionUiItem> EntityTypeOptions { get; } = new()
    {
        Opt(CharacterGroupEntityTypeIds.PlayerCharacter, "Персонаж игрока"),
        Opt(CharacterGroupEntityTypeIds.Npc, "NPC"),
        Opt(CharacterGroupEntityTypeIds.Companion, "Компаньон"),
        Opt(CharacterGroupEntityTypeIds.TemporaryAlly, "Временный союзник"),
        Opt(CharacterGroupEntityTypeIds.Enemy, "Враг"),
        Opt(CharacterGroupEntityTypeIds.Neutral, "Нейтральный"),
        Opt(CharacterGroupEntityTypeIds.Custom, "Другое")
    };
    public ObservableCollection<GroupOptionUiItem> RoleInGroupOptions { get; } = new()
    {
        Opt(CharacterGroupRoleInGroupIds.Leader, "Лидер"),
        Opt(CharacterGroupRoleInGroupIds.Member, "Участник"),
        Opt(CharacterGroupRoleInGroupIds.Companion, "Компаньон"),
        Opt(CharacterGroupRoleInGroupIds.Guide, "Проводник"),
        Opt(CharacterGroupRoleInGroupIds.Guard, "Охрана"),
        Opt(CharacterGroupRoleInGroupIds.Prisoner, "Пленник"),
        Opt(CharacterGroupRoleInGroupIds.Escort, "Эскорт"),
        Opt(CharacterGroupRoleInGroupIds.Enemy, "Враг"),
        Opt(CharacterGroupRoleInGroupIds.Observer, "Наблюдатель"),
        Opt(CharacterGroupRoleInGroupIds.Custom, "Другое")
    };
    public ObservableCollection<GroupOptionUiItem> CharacterRoleOptions { get; } = new()
    {
        Opt(CharacterGroupCharacterRoleIds.PlayerCharacter, "Персонаж игрока"),
        Opt(CharacterGroupCharacterRoleIds.NPC, "NPC"),
        Opt(CharacterGroupCharacterRoleIds.Companion, "Компаньон"),
        Opt(CharacterGroupCharacterRoleIds.TemporaryAlly, "Временный союзник"),
        Opt(CharacterGroupCharacterRoleIds.Enemy, "Враг"),
        Opt(CharacterGroupCharacterRoleIds.Inactive, "Неактивен"),
        Opt(CharacterGroupCharacterRoleIds.Custom, "Другое")
    };
    public ObservableCollection<GroupOptionUiItem> VisibilityOptions { get; } = new()
    {
        Opt(MapVisibilityModes.Public, "Публично"),
        Opt(MapVisibilityModes.Party, "Группа"),
        Opt(MapVisibilityModes.Hidden, "Скрыто"),
        Opt(MapVisibilityModes.GmOnly, "Только GM")
    };

    public ICommand RefreshFlagsCommand { get; }
    public ICommand RefreshGroupsCommand { get; }
    public ICommand CreateGroupCommand { get; }
    public ICommand LoadSelectedGroupCommand { get; }
    public ICommand SaveGroupCommand { get; }
    public ICommand ArchiveGroupCommand { get; }
    public ICommand SetActiveCommand { get; }
    public ICommand ClearActiveCommand { get; }
    public ICommand AddMemberCommand { get; }
    public ICommand SaveMemberCommand { get; }
    public ICommand RemoveMemberCommand { get; }
    public ICommand MoveMemberUpCommand { get; }
    public ICommand MoveMemberDownCommand { get; }
    public ICommand ClearErrorCommand { get; }

    public string CampaignId { get => _campaignId; set { if (_campaignId != value) { _campaignId = value; Notify(); } } }
    public string SessionId { get => _sessionId; set { if (_sessionId != value) { _sessionId = value; Notify(); } } }
    public string StatusMessage { get => _statusMessage; private set { if (_statusMessage != value) { _statusMessage = value; Notify(); } } }
    public string WarningMessage { get => _warningMessage; private set { if (_warningMessage != value) { _warningMessage = value; Notify(); Notify(nameof(HasWarning)); } } }
    public string ErrorMessage { get => _errorMessage; private set { if (_errorMessage != value) { _errorMessage = value; Notify(); Notify(nameof(HasError)); } } }
    public bool IsLoading { get => _isLoading; private set { if (_isLoading != value) { _isLoading = value; Notify(); NotifyState(); } } }
    public bool IsEnabled { get => _isEnabled; private set { if (_isEnabled != value) { _isEnabled = value; Notify(); NotifyState(); } } }
    public bool IsMembershipEnabled { get => _isMembershipEnabled; private set { if (_isMembershipEnabled != value) { _isMembershipEnabled = value; Notify(); NotifyState(); } } }
    public bool IsActiveGroupEnabled { get => _isActiveGroupEnabled; private set { if (_isActiveGroupEnabled != value) { _isActiveGroupEnabled = value; Notify(); NotifyState(); } } }
    public bool IsPlayerViewEnabled { get => _isPlayerViewEnabled; private set { if (_isPlayerViewEnabled != value) { _isPlayerViewEnabled = value; Notify(); } } }
    public CharacterGroupUiItem? SelectedGroup
    {
        get => _selectedGroup;
        set
        {
            if (_selectedGroup == value) return;
            _selectedGroup = value;
            Notify();
            NotifyState();
            if (value != null) ApplyGroup(value);
        }
    }
    public CharacterGroupMemberUiItem? SelectedMember
    {
        get => _selectedMember;
        set
        {
            if (_selectedMember == value) return;
            _selectedMember = value;
            Notify();
            NotifyState();
            if (value != null) ApplyMember(value);
        }
    }

    public string GroupName { get => _groupName; set { if (_groupName != value) { _groupName = value; Notify(); } } }
    public string GroupDescription { get => _groupDescription; set { if (_groupDescription != value) { _groupDescription = value; Notify(); } } }
    public string GroupType { get => _groupType; set { if (_groupType != value) { _groupType = value; Notify(); } } }
    public string GroupStatus { get => _groupStatus; set { if (_groupStatus != value) { _groupStatus = value; Notify(); } } }
    public string GroupVisibilityMode { get => _groupVisibilityMode; set { if (_groupVisibilityMode != value) { _groupVisibilityMode = value; Notify(); } } }
    public bool GroupPlayerVisible { get => _groupPlayerVisible; set { if (_groupPlayerVisible != value) { _groupPlayerVisible = value; Notify(); } } }
    public string GroupPublicNotes { get => _groupPublicNotes; set { if (_groupPublicNotes != value) { _groupPublicNotes = value; Notify(); } } }
    public string GroupGmNotes { get => _groupGmNotes; set { if (_groupGmNotes != value) { _groupGmNotes = value; Notify(); } } }
    public string NewMemberEntityType { get => _newMemberEntityType; set { if (_newMemberEntityType != value) { _newMemberEntityType = value; Notify(); } } }
    public string NewMemberEntityId { get => _newMemberEntityId; set { if (_newMemberEntityId != value) { _newMemberEntityId = value; Notify(); } } }
    public string NewMemberDisplayName { get => _newMemberDisplayName; set { if (_newMemberDisplayName != value) { _newMemberDisplayName = value; Notify(); } } }
    public string NewMemberRoleInGroup { get => _newMemberRoleInGroup; set { if (_newMemberRoleInGroup != value) { _newMemberRoleInGroup = value; Notify(); } } }
    public string NewMemberCharacterRole { get => _newMemberCharacterRole; set { if (_newMemberCharacterRole != value) { _newMemberCharacterRole = value; Notify(); } } }
    public bool NewMemberPlayerVisible { get => _newMemberPlayerVisible; set { if (_newMemberPlayerVisible != value) { _newMemberPlayerVisible = value; Notify(); } } }
    public string NewMemberVisibilityMode { get => _newMemberVisibilityMode; set { if (_newMemberVisibilityMode != value) { _newMemberVisibilityMode = value; Notify(); } } }
    public bool NewMemberIsLeader { get => _newMemberIsLeader; set { if (_newMemberIsLeader != value) { _newMemberIsLeader = value; Notify(); } } }
    public string MemberPublicNotes { get => _memberPublicNotes; set { if (_memberPublicNotes != value) { _memberPublicNotes = value; Notify(); } } }
    public string MemberGmNotes { get => _memberGmNotes; set { if (_memberGmNotes != value) { _memberGmNotes = value; Notify(); } } }
    public string LastRefreshText => _lastRefreshAtUtc == default ? "не обновлялось" : _lastRefreshAtUtc.ToLocalTime().ToString("HH:mm:ss");
    public bool HasWarning => !string.IsNullOrWhiteSpace(WarningMessage);
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool CanUseGroups => IsEnabled && !IsLoading;
    public bool CanEditGroups => CanUseGroups && SelectedGroup != null;
    public bool CanEditMembers => CanEditGroups && IsMembershipEnabled;
    public bool CanUseActiveGroup => CanEditGroups && IsActiveGroupEnabled;
    public string FeatureSummary => $"groups={IsEnabled}; members={IsMembershipEnabled}; active={IsActiveGroupEnabled}; playerView={IsPlayerViewEnabled}";
    public string SelectedGroupSummary => SelectedGroup == null ? "Группа не выбрана." : $"{SelectedGroup.Name} · {SelectedGroup.TypeDisplay} · участников: {SelectedGroup.MemberCount}";

    public void RefreshFlags()
    {
        try
        {
            var response = _api.SystemFeatureFlagsSnapshot();
            if (!IsOk(response))
            {
                ErrorMessage = Friendly(response, "Не удалось загрузить флаги функций групп.");
                IsEnabled = false;
                return;
            }

            var flags = Dictionaries(Get(response.Payload, "flags")).ToList();
            IsEnabled = Flag(flags, nameof(CharacterGroupFeatureFlags.UseCharacterGroupsMvp));
            IsMembershipEnabled = IsEnabled && Flag(flags, nameof(CharacterGroupFeatureFlags.UseGroupMembershipV1));
            IsActiveGroupEnabled = IsMembershipEnabled
                && Flag(flags, nameof(CharacterGroupFeatureFlags.UseActiveGroupMvp))
                && Flag(flags, nameof(CharacterGroupFeatureFlags.UseGroupSessionLink));
            IsPlayerViewEnabled = IsEnabled && Flag(flags, nameof(CharacterGroupFeatureFlags.UseGroupPlayerView));
            StatusMessage = IsEnabled
                ? "Character Groups MVP включён. Можно создавать группы, вести состав и назначать активную группу."
                : "Группы персонажей выключены флагами функций.";
            if (IsEnabled) RefreshGroups();
        }
        catch (Exception ex)
        {
            ErrorMessage = "Не удалось проверить флаги функций групп.";
            ClientLogService.Instance.Error("admin.characterGroups.flags.error", ex);
        }
    }

    private void RefreshGroups()
    {
        if (!IsEnabled) return;
        Run("admin.characterGroups.list", () =>
        {
            var response = _api.GroupCharacterList(new Dictionary<string, object>
            {
                { "campaignId", CampaignId },
                { "sessionId", SessionId },
                { "includeArchived", false }
            });
            if (!IsOk(response))
            {
                ErrorMessage = Friendly(response, "Не удалось загрузить группы персонажей.");
                return;
            }

            Groups.Clear();
            foreach (var item in Dictionaries(Get(response.Payload, "items")))
                Groups.Add(CharacterGroupUiItem.From(item));
            _lastRefreshAtUtc = DateTime.UtcNow;
            Notify(nameof(LastRefreshText));
            StatusMessage = Groups.Count == 0 ? "Группы персонажей пока не созданы." : $"Групп загружено: {Groups.Count}.";
            if (SelectedGroup == null && Groups.Count > 0) SelectedGroup = Groups[0];
        });
    }

    private void CreateGroup()
    {
        if (!IsEnabled) return;
        Run("admin.characterGroups.create", () =>
        {
            var response = _api.GroupCharacterCreate(GroupPayload(includeGroupId: false));
            if (!IsOk(response))
            {
                ErrorMessage = Friendly(response, "Не удалось создать группу.");
                return;
            }
            ApplyGroupResponse(response);
            RefreshGroups();
            StatusMessage = "Группа создана.";
        });
    }

    private void LoadSelectedGroup()
    {
        if (SelectedGroup == null) return;
        Run("admin.characterGroups.get", () =>
        {
            var response = _api.GroupCharacterGet(new Dictionary<string, object> { { "groupId", SelectedGroup.GroupId } });
            if (!IsOk(response))
            {
                ErrorMessage = Friendly(response, "Не удалось открыть группу.");
                return;
            }
            ApplyGroupResponse(response);
            StatusMessage = "Группа загружена.";
        });
    }

    private void SaveGroup()
    {
        if (SelectedGroup == null) return;
        Run("admin.characterGroups.update", () =>
        {
            var response = _api.GroupCharacterUpdate(GroupPayload(includeGroupId: true));
            if (!IsOk(response))
            {
                ErrorMessage = Friendly(response, "Не удалось сохранить группу.");
                return;
            }
            ApplyGroupResponse(response);
            RefreshGroups();
            StatusMessage = "Группа сохранена.";
        });
    }

    private void ArchiveGroup()
    {
        if (SelectedGroup == null) return;
        if (MessageBox.Show("Архивировать выбранную группу персонажей?", "Архив группы", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        Run("admin.characterGroups.archive", () =>
        {
            var response = _api.GroupCharacterArchive(new Dictionary<string, object> { { "groupId", SelectedGroup.GroupId } });
            if (!IsOk(response))
            {
                ErrorMessage = Friendly(response, "Не удалось архивировать группу.");
                return;
            }
            SelectedGroup = null;
            Members.Clear();
            RefreshGroups();
            StatusMessage = "Группа архивирована.";
        });
    }

    private void SetActiveGroup()
    {
        if (SelectedGroup == null || !IsActiveGroupEnabled) return;
        Run("admin.characterGroups.setActive", () =>
        {
            var response = _api.GroupCharacterSetActive(new Dictionary<string, object>
            {
                { "campaignId", CampaignId },
                { "sessionId", SessionId },
                { "groupId", SelectedGroup.GroupId }
            });
            if (!IsOk(response))
            {
                ErrorMessage = Friendly(response, "Не удалось назначить активную группу.");
                return;
            }
            RefreshGroups();
            StatusMessage = "Активная группа назначена.";
        });
    }

    private void ClearActiveGroup()
    {
        if (!IsActiveGroupEnabled) return;
        if (MessageBox.Show("Игроки больше не будут видеть активную группу через текущую сессию. Продолжить?", "Снять активную группу", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        Run("admin.characterGroups.clearActive", () =>
        {
            var response = _api.GroupCharacterClearActive(new Dictionary<string, object>
            {
                { "campaignId", CampaignId },
                { "sessionId", SessionId }
            });
            if (!IsOk(response))
            {
                ErrorMessage = Friendly(response, "Не удалось снять активную группу.");
                return;
            }
            RefreshGroups();
            StatusMessage = "Активная группа снята.";
        });
    }

    private void AddMember()
    {
        if (SelectedGroup == null || !IsMembershipEnabled) return;
        Run("admin.characterGroups.member.add", () =>
        {
            var response = _api.GroupCharacterMemberAdd(MemberPayload(includeMemberId: false));
            if (!IsOk(response))
            {
                ErrorMessage = Friendly(response, "Не удалось добавить участника.");
                return;
            }
            ApplyGroupResponse(response);
            NewMemberEntityId = string.Empty;
            NewMemberDisplayName = string.Empty;
            StatusMessage = "Участник добавлен.";
        });
    }

    private void SaveMember()
    {
        if (SelectedMember == null || !IsMembershipEnabled) return;
        Run("admin.characterGroups.member.update", () =>
        {
            var response = _api.GroupCharacterMemberUpdate(MemberPayload(includeMemberId: true));
            if (!IsOk(response))
            {
                ErrorMessage = Friendly(response, "Не удалось сохранить участника.");
                return;
            }
            ApplyGroupResponse(response);
            StatusMessage = "Участник сохранён.";
        });
    }

    private void RemoveMember()
    {
        if (SelectedMember == null || !IsMembershipEnabled) return;
        if (MessageBox.Show("Удалить участника из группы?", "Удаление участника", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        Run("admin.characterGroups.member.remove", () =>
        {
            var response = _api.GroupCharacterMemberRemove(new Dictionary<string, object> { { "memberId", SelectedMember.MemberId } });
            if (!IsOk(response))
            {
                ErrorMessage = Friendly(response, "Не удалось удалить участника.");
                return;
            }
            LoadSelectedGroup();
            StatusMessage = "Участник удалён из группы.";
        });
    }

    private void MoveMember(int delta)
    {
        if (SelectedMember == null || !IsMembershipEnabled) return;
        Run("admin.characterGroups.member.move", () =>
        {
            var response = _api.GroupCharacterMemberMove(new Dictionary<string, object>
            {
                { "memberId", SelectedMember.MemberId },
                { "sortOrder", SelectedMember.SortOrder + delta }
            });
            if (!IsOk(response))
            {
                ErrorMessage = Friendly(response, "Не удалось переместить участника.");
                return;
            }
            LoadSelectedGroup();
        });
    }

    private Dictionary<string, object> GroupPayload(bool includeGroupId)
    {
        var payload = new Dictionary<string, object>
        {
            { "campaignId", CampaignId },
            { "sessionId", SessionId },
            { "name", GroupName },
            { "description", GroupDescription },
            { "groupType", GroupType },
            { "status", GroupStatus },
            { "visibilityMode", GroupVisibilityMode },
            { "isPlayerVisible", GroupPlayerVisible },
            { "publicNotes", GroupPublicNotes },
            { "gmNotes", GroupGmNotes }
        };
        if (includeGroupId && SelectedGroup != null) payload["groupId"] = SelectedGroup.GroupId;
        return payload;
    }

    private Dictionary<string, object> MemberPayload(bool includeMemberId)
    {
        var payload = new Dictionary<string, object>
        {
            { "groupId", SelectedGroup?.GroupId ?? string.Empty },
            { "entityType", NewMemberEntityType },
            { "entityId", NewMemberEntityId },
            { "displayName", NewMemberDisplayName },
            { "roleInGroup", NewMemberRoleInGroup },
            { "characterRole", NewMemberCharacterRole },
            { "isLeader", NewMemberIsLeader },
            { "isPlayerVisible", NewMemberPlayerVisible },
            { "visibilityMode", NewMemberVisibilityMode },
            { "publicNotes", MemberPublicNotes },
            { "gmNotes", MemberGmNotes }
        };
        if (includeMemberId && SelectedMember != null) payload["memberId"] = SelectedMember.MemberId;
        return payload;
    }

    private void ApplyGroupResponse(ResponseEnvelope response)
    {
        var groupMap = Dict(Get(response.Payload, "group"));
        if (groupMap == null) return;
        var group = CharacterGroupUiItem.From(groupMap);
        ApplyGroup(group);
        Members.Clear();
        foreach (var memberMap in Dictionaries(Get(groupMap, "members")).Concat(Dictionaries(Get(response.Payload, "members"))).GroupBy(x => Str(x, "memberId")).Select(x => x.First()))
            Members.Add(CharacterGroupMemberUiItem.From(memberMap));
        SelectedMember = Members.FirstOrDefault();
        SelectedGroup = group;
    }

    private void ApplyGroup(CharacterGroupUiItem group)
    {
        GroupName = group.Name;
        GroupDescription = group.Description;
        GroupType = group.GroupType;
        GroupStatus = group.Status;
        GroupVisibilityMode = group.VisibilityMode;
        GroupPlayerVisible = group.IsPlayerVisible;
        GroupPublicNotes = group.PublicNotes;
        GroupGmNotes = group.GMNotes;
    }

    private void ApplyMember(CharacterGroupMemberUiItem member)
    {
        NewMemberEntityType = member.EntityType;
        NewMemberEntityId = member.EntityId;
        NewMemberDisplayName = member.DisplayName;
        NewMemberRoleInGroup = member.RoleInGroup;
        NewMemberCharacterRole = member.CharacterRole;
        NewMemberIsLeader = member.IsLeader;
        NewMemberPlayerVisible = member.IsPlayerVisible;
        NewMemberVisibilityMode = member.VisibilityMode;
        MemberPublicNotes = member.PublicNotes;
        MemberGmNotes = member.GMNotes;
    }

    private void Run(string eventName, Action action)
    {
        if (IsLoading) return;
        IsLoading = true;
        ErrorMessage = string.Empty;
        WarningMessage = string.Empty;
        try
        {
            action();
        }
        catch (Exception ex)
        {
            ErrorMessage = "Операция с группами персонажей завершилась ошибкой.";
            ClientLogService.Instance.Error(eventName, ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void NotifyState()
    {
        Notify(nameof(CanUseGroups));
        Notify(nameof(CanEditGroups));
        Notify(nameof(CanEditMembers));
        Notify(nameof(CanUseActiveGroup));
        Notify(nameof(FeatureSummary));
        Notify(nameof(SelectedGroupSummary));
    }

    private static bool IsOk(ResponseEnvelope response) => response.Status == ResponseStatus.Ok;

    private static string Friendly(ResponseEnvelope response, string fallback)
        => string.IsNullOrWhiteSpace(response.Message) ? fallback : response.Message;

    private static bool Flag(IEnumerable<Dictionary<string, object>> flags, string name)
        => flags.Any(flag => string.Equals(Str(flag, "name"), name, StringComparison.OrdinalIgnoreCase) && Bool(flag, "effectiveValue"));

    private static object? Get(IDictionary<string, object>? map, string key)
        => map != null && map.TryGetValue(key, out var value) ? value : null;

    private static string Str(IDictionary<string, object>? map, string key)
        => Convert.ToString(Get(map, key)) ?? string.Empty;

    private static int Int(IDictionary<string, object>? map, string key)
        => int.TryParse(Str(map, key), out var value) ? value : 0;

    private static bool Bool(IDictionary<string, object>? map, string key)
        => bool.TryParse(Str(map, key), out var value) && value;

    private static Dictionary<string, object>? Dict(object? value)
    {
        if (value is Dictionary<string, object> typed) return typed;
        if (value is IDictionary dictionary)
        {
            var result = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (DictionaryEntry entry in dictionary)
            {
                var key = Convert.ToString(entry.Key);
                if (!string.IsNullOrWhiteSpace(key)) result[key] = entry.Value!;
            }
            return result;
        }
        return null;
    }

    private static IEnumerable<Dictionary<string, object>> Dictionaries(object? value)
    {
        if (value is IEnumerable enumerable && value is not string)
        {
            foreach (var item in enumerable)
            {
                var map = Dict(item);
                if (map != null) yield return map;
            }
        }
    }

    private static GroupOptionUiItem Opt(string id, string title) => new(id, title);

    internal static string TypeTitle(string id) => id switch
    {
        CharacterGroupTypeIds.Party => "Партия",
        CharacterGroupTypeIds.NpcGroup => "NPC-группа",
        CharacterGroupTypeIds.CompanionGroup => "Компаньоны",
        CharacterGroupTypeIds.EnemyGroup => "Враги",
        CharacterGroupTypeIds.NeutralGroup => "Нейтральные",
        CharacterGroupTypeIds.EscortGroup => "Сопровождение",
        CharacterGroupTypeIds.TemporaryGroup => "Временная группа",
        _ => "Другое"
    };

    internal static string RoleTitle(string id) => id switch
    {
        CharacterGroupRoleInGroupIds.Leader => "Лидер",
        CharacterGroupRoleInGroupIds.Companion => "Компаньон",
        CharacterGroupRoleInGroupIds.Guide => "Проводник",
        CharacterGroupRoleInGroupIds.Guard => "Охрана",
        CharacterGroupRoleInGroupIds.Prisoner => "Пленник",
        CharacterGroupRoleInGroupIds.Escort => "Эскорт",
        CharacterGroupRoleInGroupIds.Enemy => "Враг",
        CharacterGroupRoleInGroupIds.Observer => "Наблюдатель",
        CharacterGroupRoleInGroupIds.Custom => "Другое",
        _ => "Участник"
    };
}

public sealed class GroupOptionUiItem
{
    public GroupOptionUiItem(string id, string title)
    {
        Id = id;
        Title = title;
    }

    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
}

public sealed class CharacterGroupUiItem
{
    public string GroupId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string GroupType { get; set; } = CharacterGroupTypeIds.Party;
    public string Status { get; set; } = CharacterGroupStatusIds.Draft;
    public bool IsActive { get; set; }
    public bool IsPlayerVisible { get; set; }
    public string VisibilityMode { get; set; } = MapVisibilityModes.Party;
    public int MemberCount { get; set; }
    public string PublicNotes { get; set; } = string.Empty;
    public string GMNotes { get; set; } = string.Empty;
    public string ActiveBadge => IsActive ? "Активная" : "—";
    public string TypeDisplay => AdminCharacterGroupsViewModel.TypeTitle(GroupType);
    public string VisibilityDisplay => IsPlayerVisible ? "Видна игрокам" : "Скрыта";

    public static CharacterGroupUiItem From(Dictionary<string, object> map)
        => new()
        {
            GroupId = Str(map, "groupId"),
            CampaignId = Str(map, "campaignId"),
            SessionId = Str(map, "sessionId"),
            Name = First(Str(map, "name"), "Без названия"),
            Description = Str(map, "description"),
            GroupType = First(Str(map, "groupType"), CharacterGroupTypeIds.Party),
            Status = First(Str(map, "status"), CharacterGroupStatusIds.Draft),
            IsActive = Bool(map, "isActive"),
            IsPlayerVisible = Bool(map, "isPlayerVisible"),
            VisibilityMode = First(Str(map, "visibilityMode"), MapVisibilityModes.Party),
            MemberCount = Int(map, "memberCount"),
            PublicNotes = Str(map, "publicNotes"),
            GMNotes = Str(map, "gmNotes")
        };

    private static string Str(IDictionary<string, object> map, string key) => map.TryGetValue(key, out var value) ? Convert.ToString(value) ?? string.Empty : string.Empty;
    private static int Int(IDictionary<string, object> map, string key) => int.TryParse(Str(map, key), out var value) ? value : 0;
    private static bool Bool(IDictionary<string, object> map, string key) => bool.TryParse(Str(map, key), out var value) && value;
    private static string First(params string[] values) => values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;
}

public sealed class CharacterGroupMemberUiItem
{
    public string MemberId { get; set; } = string.Empty;
    public string EntityType { get; set; } = CharacterGroupEntityTypeIds.PlayerCharacter;
    public string EntityId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string RoleInGroup { get; set; } = CharacterGroupRoleInGroupIds.Member;
    public string CharacterRole { get; set; } = CharacterGroupCharacterRoleIds.PlayerCharacter;
    public bool IsLeader { get; set; }
    public bool IsPlayerVisible { get; set; }
    public string VisibilityMode { get; set; } = MapVisibilityModes.Party;
    public int SortOrder { get; set; }
    public string PublicNotes { get; set; } = string.Empty;
    public string GMNotes { get; set; } = string.Empty;
    public string RoleDisplay => IsLeader ? "Лидер" : AdminCharacterGroupsViewModel.RoleTitle(RoleInGroup);
    public string PlayerVisibilityDisplay => IsPlayerVisible ? "Виден игрокам" : "Скрыт";

    public static CharacterGroupMemberUiItem From(Dictionary<string, object> map)
        => new()
        {
            MemberId = Str(map, "memberId"),
            EntityType = First(Str(map, "entityType"), CharacterGroupEntityTypeIds.PlayerCharacter),
            EntityId = Str(map, "entityId"),
            DisplayName = First(Str(map, "displayName"), Str(map, "entityId"), "Без имени"),
            RoleInGroup = First(Str(map, "roleInGroup"), CharacterGroupRoleInGroupIds.Member),
            CharacterRole = First(Str(map, "characterRole"), CharacterGroupCharacterRoleIds.PlayerCharacter),
            IsLeader = Bool(map, "isLeader"),
            IsPlayerVisible = Bool(map, "isPlayerVisible"),
            VisibilityMode = First(Str(map, "visibilityMode"), MapVisibilityModes.Party),
            SortOrder = Int(map, "sortOrder"),
            PublicNotes = Str(map, "publicNotes"),
            GMNotes = Str(map, "gmNotes")
        };

    private static string Str(IDictionary<string, object> map, string key) => map.TryGetValue(key, out var value) ? Convert.ToString(value) ?? string.Empty : string.Empty;
    private static int Int(IDictionary<string, object> map, string key) => int.TryParse(Str(map, key), out var value) ? value : 0;
    private static bool Bool(IDictionary<string, object> map, string key) => bool.TryParse(Str(map, key), out var value) && value;
    private static string First(params string[] values) => values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;
}

