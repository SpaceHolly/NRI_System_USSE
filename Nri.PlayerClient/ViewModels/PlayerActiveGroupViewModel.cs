using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Nri.PlayerClient.Diagnostics;
using Nri.PlayerClient.Networking;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;

namespace Nri.PlayerClient.ViewModels;

public sealed class PlayerActiveGroupViewModel : ViewModelBase
{
    private readonly CommandApi _api;
    private readonly Func<string> _activeCharacterIdAccessor;
    private string _campaignId = "default";
    private string _sessionId = string.Empty;
    private bool _isEnabled;
    private bool _isLoading;
    private bool _hasActiveGroup;
    private string _groupName = string.Empty;
    private string _description = string.Empty;
    private string _publicNotes = string.Empty;
    private string _statusMessage = "Активная группа пока недоступна.";
    private string _errorMessage = string.Empty;
    private DateTime _lastRefreshAtUtc;

    public PlayerActiveGroupViewModel(CommandApi api, Func<string> activeCharacterIdAccessor)
    {
        _api = api;
        _activeCharacterIdAccessor = activeCharacterIdAccessor;
        RefreshFlagsCommand = new RelayCommand(RefreshFlags);
        LoadActiveGroupCommand = new RelayCommand(LoadActiveGroup);
        ClearErrorCommand = new RelayCommand(() => ErrorMessage = string.Empty);
    }

    public ObservableCollection<PlayerGroupMemberUiItem> Members { get; } = new();

    public ICommand RefreshFlagsCommand { get; }
    public ICommand LoadActiveGroupCommand { get; }
    public ICommand ClearErrorCommand { get; }

    public string CampaignId { get => _campaignId; set { if (_campaignId != value) { _campaignId = value; Notify(); } } }
    public string SessionId { get => _sessionId; set { if (_sessionId != value) { _sessionId = value; Notify(); } } }
    public bool IsEnabled { get => _isEnabled; private set { if (_isEnabled != value) { _isEnabled = value; Notify(); NotifyState(); } } }
    public bool IsLoading { get => _isLoading; private set { if (_isLoading != value) { _isLoading = value; Notify(); NotifyState(); } } }
    public bool HasActiveGroup { get => _hasActiveGroup; private set { if (_hasActiveGroup != value) { _hasActiveGroup = value; Notify(); NotifyState(); } } }
    public string GroupName { get => _groupName; private set { if (_groupName != value) { _groupName = value; Notify(); Notify(nameof(HeaderText)); } } }
    public string Description { get => _description; private set { if (_description != value) { _description = value; Notify(); } } }
    public string PublicNotes { get => _publicNotes; private set { if (_publicNotes != value) { _publicNotes = value; Notify(); } } }
    public string StatusMessage { get => _statusMessage; private set { if (_statusMessage != value) { _statusMessage = value; Notify(); } } }
    public string ErrorMessage { get => _errorMessage; private set { if (_errorMessage != value) { _errorMessage = value; Notify(); Notify(nameof(HasError)); } } }

    public bool CanLoad => IsEnabled && !IsLoading;
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public string HeaderText => HasActiveGroup ? GroupName : "Активная группа не назначена";
    public string EmptyText => IsEnabled ? "GM ещё не назначил активную группу." : "Группы персонажей пока недоступны.";
    public string LastRefreshText => _lastRefreshAtUtc == default ? "не обновлялось" : _lastRefreshAtUtc.ToLocalTime().ToString("HH:mm:ss");

    public void RefreshFlags()
    {
        try
        {
            var response = _api.SendSystemFeatureFlagsSnapshotForPlayer();
            if (!IsOk(response))
            {
                ErrorMessage = PlayerFacingMessage(response.Message, "Не удалось проверить доступность групп.");
                IsEnabled = false;
                return;
            }

            var flags = Dictionaries(Get(response.Payload, "flags")).ToList();
            IsEnabled = Flag(flags, nameof(CharacterGroupFeatureFlags.UseCharacterGroupsMvp))
                && Flag(flags, nameof(CharacterGroupFeatureFlags.UseGroupPlayerView));
            StatusMessage = IsEnabled
                ? "Активная группа доступна в режиме просмотра."
                : "Активная группа пока недоступна.";
            if (IsEnabled) LoadActiveGroup();
        }
        catch (Exception ex)
        {
            ErrorMessage = "Не удалось проверить доступность групп.";
            ClientLogService.Instance.Error("player.activeGroup.flags.error", ex);
        }
    }

    private void LoadActiveGroup()
    {
        if (!IsEnabled || IsLoading) return;
        IsLoading = true;
        ErrorMessage = string.Empty;
        try
        {
            var response = _api.GroupPlayerActiveGet(new Dictionary<string, object>
            {
                { "campaignId", CampaignId },
                { "sessionId", SessionId },
                { "characterId", _activeCharacterIdAccessor() ?? string.Empty }
            });
            if (!IsOk(response))
            {
                ErrorMessage = Friendly(response, "Не удалось загрузить активную группу.");
                return;
            }

            HasActiveGroup = Bool(response.Payload, "hasActiveGroup");
            Members.Clear();
            if (!HasActiveGroup)
            {
                GroupName = string.Empty;
                Description = string.Empty;
                PublicNotes = string.Empty;
                StatusMessage = "GM ещё не назначил активную группу.";
                return;
            }

            var group = Dict(Get(response.Payload, "group"));
            GroupName = First(Str(group, "name"), "Активная группа");
            Description = Str(group, "description");
            PublicNotes = Str(group, "publicNotes");
            foreach (var memberMap in Dictionaries(Get(group, "members")))
                Members.Add(PlayerGroupMemberUiItem.From(memberMap));
            _lastRefreshAtUtc = DateTime.UtcNow;
            Notify(nameof(LastRefreshText));
            StatusMessage = Members.Count == 0 ? "Активная группа загружена, участники пока не раскрыты." : $"Участников видно: {Members.Count}.";
        }
        catch (Exception ex)
        {
            ErrorMessage = "Активная группа не загрузилась.";
            ClientLogService.Instance.Error("player.activeGroup.load.error", ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void NotifyState()
    {
        Notify(nameof(CanLoad));
        Notify(nameof(HeaderText));
        Notify(nameof(EmptyText));
    }

    private static bool IsOk(ResponseEnvelope response) => response.Status == ResponseStatus.Ok;

    private static string Friendly(ResponseEnvelope response, string fallback)
        => string.IsNullOrWhiteSpace(response.Message) ? fallback : response.Message;

    private static string PlayerFacingMessage(string? message, string fallback)
    {
        if (string.IsNullOrWhiteSpace(message)) return fallback;
        if (message.IndexOf("feature flag", StringComparison.OrdinalIgnoreCase) >= 0 ||
            message.IndexOf("flags", StringComparison.OrdinalIgnoreCase) >= 0)
            return fallback;
        return message;
    }

    private static bool Flag(IEnumerable<Dictionary<string, object>> flags, string name)
        => flags.Any(flag => string.Equals(Str(flag, "name"), name, StringComparison.OrdinalIgnoreCase) && Bool(flag, "effectiveValue"));

    private static object? Get(IDictionary<string, object>? map, string key)
        => map != null && map.TryGetValue(key, out var value) ? value : null;

    private static string Str(IDictionary<string, object>? map, string key)
        => Convert.ToString(Get(map, key)) ?? string.Empty;

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

    private static string First(params string[] values) => values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;
}

public sealed class PlayerGroupMemberUiItem
{
    public string DisplayName { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string RoleInGroup { get; set; } = string.Empty;
    public string CharacterRole { get; set; } = string.Empty;
    public bool IsLeader { get; set; }
    public string PublicNotes { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public string RoleDisplay => IsLeader ? "Лидер" : RoleInGroup switch
    {
        CharacterGroupRoleInGroupIds.Companion => "Компаньон",
        CharacterGroupRoleInGroupIds.Guide => "Проводник",
        CharacterGroupRoleInGroupIds.Guard => "Охрана",
        CharacterGroupRoleInGroupIds.Prisoner => "Пленник",
        CharacterGroupRoleInGroupIds.Escort => "Эскорт",
        CharacterGroupRoleInGroupIds.Enemy => "Враг",
        CharacterGroupRoleInGroupIds.Observer => "Наблюдатель",
        _ => "Участник"
    };

    public static PlayerGroupMemberUiItem From(Dictionary<string, object> map)
        => new()
        {
            DisplayName = First(Str(map, "displayName"), "Без имени"),
            EntityType = Str(map, "entityType"),
            RoleInGroup = Str(map, "roleInGroup"),
            CharacterRole = Str(map, "characterRole"),
            IsLeader = Bool(map, "isLeader"),
            PublicNotes = Str(map, "publicNotes"),
            SortOrder = Int(map, "sortOrder")
        };

    private static string Str(IDictionary<string, object> map, string key) => map.TryGetValue(key, out var value) ? Convert.ToString(value) ?? string.Empty : string.Empty;
    private static int Int(IDictionary<string, object> map, string key) => int.TryParse(Str(map, key), out var value) ? value : 0;
    private static bool Bool(IDictionary<string, object> map, string key) => bool.TryParse(Str(map, key), out var value) && value;
    private static string First(params string[] values) => values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;
}

