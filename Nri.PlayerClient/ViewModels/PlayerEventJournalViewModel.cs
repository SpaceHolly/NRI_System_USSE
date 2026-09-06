using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows.Input;
using Nri.PlayerClient.Diagnostics;
using Nri.PlayerClient.Networking;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;

namespace Nri.PlayerClient.ViewModels;

public sealed class PlayerEventJournalViewModel : ViewModelBase
{
    private readonly CommandApi _api;
    private readonly Func<string> _activeCharacterIdAccessor;
    private string _campaignId = "default";
    private string _sessionId = string.Empty;
    private string _category = string.Empty;
    private string _searchText = string.Empty;
    private string _statusMessage = "Журнал событий пока недоступен.";
    private string _errorMessage = string.Empty;
    private bool _isEnabled;
    private EventJournalPlayerRow? _selectedEntry;
    private EventJournalCategoryOption? _selectedCategoryOption;

    public PlayerEventJournalViewModel(CommandApi api, Func<string> activeCharacterIdAccessor)
    {
        _api = api;
        _activeCharacterIdAccessor = activeCharacterIdAccessor;
        RefreshFlagsCommand = new RelayCommand(RefreshFlags);
        RefreshCommand = new RelayCommand(Refresh);
        SearchCommand = new RelayCommand(Search);
        ClearErrorCommand = new RelayCommand(() => ErrorMessage = string.Empty);
        SelectedCategoryOption = CategoryOptions[0];
    }

    public ObservableCollection<EventJournalPlayerRow> Entries { get; } = new();
    public ObservableCollection<EventJournalPlayerLinkRow> Links { get; } = new();
    public ObservableCollection<EventJournalPlayerAnnotationRow> Annotations { get; } = new();
    public ObservableCollection<EventJournalCategoryOption> CategoryOptions { get; } = new()
    {
        new EventJournalCategoryOption(string.Empty, "Все события"),
        new EventJournalCategoryOption(EventJournalCategoryIds.Session, "Сессия"),
        new EventJournalCategoryOption(EventJournalCategoryIds.Character, "Персонаж"),
        new EventJournalCategoryOption(EventJournalCategoryIds.Group, "Группа"),
        new EventJournalCategoryOption(EventJournalCategoryIds.Request, "Заявки"),
        new EventJournalCategoryOption(EventJournalCategoryIds.Combat, "Бой"),
        new EventJournalCategoryOption(EventJournalCategoryIds.Map, "Карты"),
        new EventJournalCategoryOption(EventJournalCategoryIds.WorldCalendar, "Календарь мира"),
        new EventJournalCategoryOption(EventJournalCategoryIds.RealSchedule, "Расписание"),
        new EventJournalCategoryOption(EventJournalCategoryIds.Inventory, "Инвентарь"),
        new EventJournalCategoryOption(EventJournalCategoryIds.System, "Система"),
        new EventJournalCategoryOption(EventJournalCategoryIds.Custom, "Другое")
    };

    public ICommand RefreshFlagsCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand SearchCommand { get; }
    public ICommand ClearErrorCommand { get; }

    public string CampaignId { get => _campaignId; set { if (_campaignId != value) { _campaignId = value ?? string.Empty; Notify(); } } }
    public string SessionId { get => _sessionId; set { if (_sessionId != value) { _sessionId = value ?? string.Empty; Notify(); } } }
    public string Category { get => _category; set { if (_category != value) { _category = value ?? string.Empty; Notify(); } } }
    public EventJournalCategoryOption? SelectedCategoryOption
    {
        get => _selectedCategoryOption;
        set
        {
            if (_selectedCategoryOption == value) return;
            _selectedCategoryOption = value;
            Category = value?.Value ?? string.Empty;
            Notify();
        }
    }
    public string SearchText { get => _searchText; set { if (_searchText != value) { _searchText = value ?? string.Empty; Notify(); } } }
    public string StatusMessage { get => _statusMessage; set { if (_statusMessage != value) { _statusMessage = value ?? string.Empty; Notify(); } } }
    public string ErrorMessage { get => _errorMessage; set { if (_errorMessage != value) { _errorMessage = value ?? string.Empty; Notify(); Notify(nameof(HasError)); } } }
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool IsEnabled { get => _isEnabled; set { if (_isEnabled != value) { _isEnabled = value; Notify(); Notify(nameof(IsDisabled)); } } }
    public bool IsDisabled => !IsEnabled;

    public EventJournalPlayerRow? SelectedEntry
    {
        get => _selectedEntry;
        set
        {
            if (_selectedEntry == value) return;
            _selectedEntry = value;
            Notify();
            Links.Clear();
            Annotations.Clear();
            if (value != null) LoadDetails(value.EntryId);
        }
    }

    public void RefreshFlags()
    {
        Safe("player.journal.flags", () =>
        {
            var response = _api.JournalEventPlayerList(BasePayload());
            if (response.Status == ResponseStatus.Ok)
            {
                IsEnabled = true;
                ReplaceEntries(ReadList(response.Payload, "items"));
                StatusMessage = Entries.Count == 0 ? "Журнал событий включён. Видимых событий пока нет." : $"Журнал событий включён. Видимых событий: {Entries.Count}.";
                return;
            }

            IsEnabled = false;
            StatusMessage = PlayerFacingMessage(response.Message, "Журнал событий пока недоступен.");
        });
    }

    public void Refresh()
    {
        if (IsEnabled) Load();
        else RefreshFlags();
    }

    public void Load()
    {
        if (!IsEnabled)
        {
            StatusMessage = "Журнал событий пока недоступен.";
            return;
        }

        Safe("player.journal.load", () =>
        {
            var response = _api.JournalEventPlayerList(BasePayload());
            RequireOk(response);
            ReplaceEntries(ReadList(response.Payload, "items"));
            StatusMessage = Entries.Count == 0 ? "Видимых событий пока нет." : $"Видимых событий: {Entries.Count}.";
        });
    }

    private void Search()
    {
        if (!IsEnabled) return;
        Safe("player.journal.search", () =>
        {
            var payload = BasePayload();
            payload["query"] = SearchText;
            var response = _api.JournalEventPlayerList(payload);
            RequireOk(response);
            ReplaceEntries(ReadList(response.Payload, "items"));
            StatusMessage = Entries.Count == 0 ? "По запросу ничего не найдено." : $"Найдено событий: {Entries.Count}.";
        });
    }

    private void LoadDetails(string entryId)
    {
        Safe("player.journal.entry.selected", () =>
        {
            var response = _api.JournalEventPlayerGet(new Dictionary<string, object> { { "entryId", entryId } });
            RequireOk(response);
            Links.Clear();
            foreach (var map in ReadList(response.Payload, "links").Select(ToMap).Where(x => x != null))
                Links.Add(EventJournalPlayerLinkRow.FromMap(map!));
            Annotations.Clear();
            foreach (var map in ReadList(response.Payload, "annotations").Select(ToMap).Where(x => x != null))
                Annotations.Add(EventJournalPlayerAnnotationRow.FromMap(map!));
        });
    }

    private Dictionary<string, object> BasePayload()
    {
        return new Dictionary<string, object>
        {
            { "campaignId", CampaignId },
            { "sessionId", SessionId },
            { "category", Category },
            { "characterId", _activeCharacterIdAccessor() ?? string.Empty }
        };
    }

    private void ReplaceEntries(IList<object> items)
    {
        Entries.Clear();
        foreach (var map in items.Select(ToMap).Where(x => x != null))
            Entries.Add(EventJournalPlayerRow.FromMap(map!));
    }

    private void Safe(string area, Action action)
    {
        try
        {
            ErrorMessage = string.Empty;
            action();
            ClientLogService.Instance.Info(area);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            ClientLogService.Instance.Warn($"{area}.error message={ex.Message}");
        }
    }

    private static void RequireOk(ResponseEnvelope response)
    {
        if (response.Status != ResponseStatus.Ok)
            throw new InvalidOperationException(PlayerFacingMessage(response.Message, "Не удалось выполнить действие."));
    }

    private static string PlayerFacingMessage(string? message, string fallback)
    {
        if (string.IsNullOrWhiteSpace(message)) return fallback;
        if (message.IndexOf("feature flag", StringComparison.OrdinalIgnoreCase) >= 0 ||
            message.IndexOf("flags", StringComparison.OrdinalIgnoreCase) >= 0 ||
            message.IndexOf("player-safe", StringComparison.OrdinalIgnoreCase) >= 0)
            return fallback;
        return message;
    }

    private static bool FindFlag(IList<object> flags, string key)
    {
        var shortKey = key.Contains(".") ? key.Substring(key.LastIndexOf('.') + 1) : key;
        foreach (var item in flags)
        {
            var map = ToMap(item);
            if (map == null) continue;
            var itemKey = Str(map, "key");
            var itemName = Str(map, "name");
            var areaKey = $"{Str(map, "area")}.{itemName}";
            if (string.Equals(Str(map, "key"), key, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(itemKey, shortKey, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(itemName, key, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(itemName, shortKey, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(areaKey, key, StringComparison.OrdinalIgnoreCase))
                return Bool(map, "effectiveValue") || Bool(map, "value") || Bool(map, "defaultValue");
        }
        return false;
    }

    private static Dictionary<string, object>? ReadMap(IDictionary<string, object> payload, string key)
        => payload.TryGetValue(key, out var value) ? ToMap(value) : null;

    private static IList<object> ReadList(IDictionary<string, object> payload, string key)
    {
        if (!payload.TryGetValue(key, out var value) || value == null) return new List<object>();
        if (value is IList<object> typed) return typed;
        if (value is ArrayList array) return array.Cast<object>().ToList();
        if (value is IEnumerable enumerable && value is not string) return enumerable.Cast<object>().ToList();
        return new List<object>();
    }

    private static Dictionary<string, object>? ToMap(object? value)
    {
        if (value is Dictionary<string, object> typed) return typed;
        if (value is IDictionary dictionary)
        {
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry entry in dictionary)
            {
                var key = Convert.ToString(entry.Key);
                if (!string.IsNullOrWhiteSpace(key)) result[key] = entry.Value ?? string.Empty;
            }
            return result;
        }
        if (value is IEnumerable enumerable && value is not string)
        {
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            var sequentialItems = new List<object?>();
            foreach (var item in enumerable)
            {
                if (item == null) continue;
                sequentialItems.Add(item);

                if (item is DictionaryEntry entry)
                {
                    var key = Convert.ToString(entry.Key);
                    if (!string.IsNullOrWhiteSpace(key)) result[key] = entry.Value ?? string.Empty;
                    continue;
                }

                if (item is object[] arrayPair && arrayPair.Length == 2)
                {
                    var key = Convert.ToString(arrayPair[0]);
                    if (!string.IsNullOrWhiteSpace(key)) result[key] = arrayPair[1] ?? string.Empty;
                    continue;
                }

                if (item is IList listPair && listPair.Count == 2)
                {
                    var key = Convert.ToString(listPair[0]);
                    if (!string.IsNullOrWhiteSpace(key)) result[key] = listPair[1] ?? string.Empty;
                    continue;
                }

                var nestedMap = ToMap(item);
                if (nestedMap != null &&
                    nestedMap.TryGetValue("Key", out var nestedKeyValue) &&
                    nestedMap.TryGetValue("Value", out var nestedValue))
                {
                    var nestedKey = Convert.ToString(nestedKeyValue);
                    if (!string.IsNullOrWhiteSpace(nestedKey)) result[nestedKey] = nestedValue ?? string.Empty;
                    continue;
                }

                var type = item.GetType();
                var keyProperty = type.GetProperty("Key") ?? type.GetProperty("Name");
                var valueProperty = type.GetProperty("Value");
                if (keyProperty == null || valueProperty == null) continue;
                var reflectedKey = Convert.ToString(keyProperty.GetValue(item));
                if (!string.IsNullOrWhiteSpace(reflectedKey)) result[reflectedKey] = valueProperty.GetValue(item) ?? string.Empty;
            }

            if (result.Count == 0 && sequentialItems.Count % 2 == 0)
            {
                for (var i = 0; i < sequentialItems.Count; i += 2)
                {
                    var key = Convert.ToString(sequentialItems[i]);
                    if (!string.IsNullOrWhiteSpace(key)) result[key] = sequentialItems[i + 1] ?? string.Empty;
                }
            }

            if (result.Count > 0) return result;
        }
        return null;
    }

    private static string Str(IDictionary<string, object> map, string key)
        => map.TryGetValue(key, out var value) && value != null ? Convert.ToString(value) ?? string.Empty : string.Empty;

    private static bool Bool(IDictionary<string, object> map, string key)
        => map.TryGetValue(key, out var value) && bool.TryParse(Convert.ToString(value), out var parsed) && parsed;
}

public sealed class EventJournalCategoryOption
{
    public EventJournalCategoryOption(string value, string label)
    {
        Value = value;
        Label = label;
    }

    public string Value { get; }
    public string Label { get; }
    public override string ToString() => Label;
}

public sealed class EventJournalPlayerRow
{
    public string EntryId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string ActorDisplayName { get; set; } = string.Empty;
    public string SubjectDisplayName { get; set; } = string.Empty;
    public string WorldDateTimeSnapshot { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; }
    public string CategoryLabel => ReadableCategory(Category);
    public string SeverityLabel => ReadableSeverity(Severity);
    public string OccurredText => OccurredAtUtc == default ? "—" : OccurredAtUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
    public string ContextText => string.Join(" • ", new[] { ActorDisplayName, SubjectDisplayName, WorldDateTimeSnapshot }.Where(x => !string.IsNullOrWhiteSpace(x)));

    public static EventJournalPlayerRow FromMap(IDictionary<string, object> map) => new()
    {
        EntryId = Str(map, "entryId"),
        Title = Str(map, "title"),
        Summary = Str(map, "summary"),
        Category = Str(map, "category"),
        Severity = Str(map, "severity"),
        ActorDisplayName = Str(map, "actorDisplayName"),
        SubjectDisplayName = Str(map, "subjectDisplayName"),
        WorldDateTimeSnapshot = Str(map, "worldDateTimeSnapshot"),
        OccurredAtUtc = Date(map, "occurredAtUtc")
    };

    private static string Str(IDictionary<string, object> map, string key) => map.TryGetValue(key, out var value) && value != null ? Convert.ToString(value) ?? string.Empty : string.Empty;
    private static DateTime Date(IDictionary<string, object> map, string key) => DateTime.TryParse(Str(map, key), out var parsed) ? parsed : default;

    private static string ReadableCategory(string value) => (value ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "session" => "Сессия",
        "character" => "Персонаж",
        "group" => "Группа",
        "request" => "Заявка",
        "combat" => "Бой",
        "map" => "Карта",
        "world_calendar" => "Календарь мира",
        "real_schedule" => "Расписание",
        "inventory" => "Инвентарь",
        "system" => "Система",
        "custom" => "Другое",
        _ => string.IsNullOrWhiteSpace(value) ? "Без категории" : PlayerDevelopmentGraphDisplay.ToReadableText(value)
    };

    private static string ReadableSeverity(string value) => (value ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "information" => "Информация",
        "notice" => "Уведомление",
        "important" => "Важно",
        "warning" => "Предупреждение",
        "critical" => "Критично",
        _ => string.IsNullOrWhiteSpace(value) ? "Обычная" : PlayerDevelopmentGraphDisplay.ToReadableText(value)
    };
}

public sealed class EventJournalPlayerLinkRow
{
    public string DisplayName { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string LinkRole { get; set; } = string.Empty;
    public string EntityTypeLabel => PlayerDevelopmentGraphDisplay.ToReadableText(EntityType);
    public string LinkRoleLabel => PlayerDevelopmentGraphDisplay.ToReadableText(LinkRole);
    public string Display => string.IsNullOrWhiteSpace(DisplayName) ? EntityTypeLabel : $"{DisplayName} ({EntityTypeLabel})";

    public static EventJournalPlayerLinkRow FromMap(IDictionary<string, object> map) => new()
    {
        DisplayName = Str(map, "displayName"),
        EntityType = Str(map, "entityType"),
        LinkRole = Str(map, "linkRole")
    };

    private static string Str(IDictionary<string, object> map, string key) => map.TryGetValue(key, out var value) && value != null ? Convert.ToString(value) ?? string.Empty : string.Empty;
}

public sealed class EventJournalPlayerAnnotationRow
{
    public string AuthorDisplayName { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;

    public static EventJournalPlayerAnnotationRow FromMap(IDictionary<string, object> map) => new()
    {
        AuthorDisplayName = Str(map, "authorDisplayName"),
        Text = Str(map, "text")
    };

    private static string Str(IDictionary<string, object> map, string key) => map.TryGetValue(key, out var value) && value != null ? Convert.ToString(value) ?? string.Empty : string.Empty;
}
