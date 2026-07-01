using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Nri.PlayerClient.Diagnostics;
using Nri.PlayerClient.Networking;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;

namespace Nri.PlayerClient.ViewModels;

public sealed class PlayerWorldCalendarViewModel : ViewModelBase
{
    private readonly CommandApi _api;
    private readonly Func<string> _activeCharacterIdAccessor;
    private string _campaignId = "default";
    private string _calendarName = "Календарь мира";
    private string _currentDisplay = "Дата не загружена";
    private string _currentSeason = string.Empty;
    private string _currentWeekDay = string.Empty;
    private string _statusMessage = "Календарь мира будет загружен после входа.";
    private string _errorMessage = string.Empty;
    private bool _isEnabled;
    private bool _isLoading;
    private CalendarEventRow? _selectedEvent;
    private string _realSchedulePlaceholder = "Расписание игр доступно в отдельной вкладке расписания. Здесь показан внутриигровой календарь мира.";

    public PlayerWorldCalendarViewModel(CommandApi api, Func<string> activeCharacterIdAccessor)
    {
        _api = api;
        _activeCharacterIdAccessor = activeCharacterIdAccessor;
        RefreshFlagsCommand = new RelayCommand(RefreshFlags);
        RefreshCommand = new RelayCommand(Load);
        ClearErrorCommand = new RelayCommand(() => ErrorMessage = string.Empty);
    }

    public ObservableCollection<CalendarMonthRow> Months { get; } = new();
    public ObservableCollection<CalendarEventRow> Events { get; } = new();
    public ObservableCollection<HolidayRow> Holidays { get; } = new();
    public ICommand RefreshFlagsCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand ClearErrorCommand { get; }

    public string CampaignId { get => _campaignId; set { if (_campaignId != value) { _campaignId = value; Notify(); } } }
    public string CalendarName { get => _calendarName; private set { if (_calendarName != value) { _calendarName = value; Notify(); } } }
    public string CurrentDisplay { get => _currentDisplay; private set { if (_currentDisplay != value) { _currentDisplay = value; Notify(); } } }
    public string CurrentSeason { get => _currentSeason; private set { if (_currentSeason != value) { _currentSeason = value; Notify(); } } }
    public string CurrentWeekDay { get => _currentWeekDay; private set { if (_currentWeekDay != value) { _currentWeekDay = value; Notify(); } } }
    public string StatusMessage { get => _statusMessage; private set { if (_statusMessage != value) { _statusMessage = value; Notify(); } } }
    public string ErrorMessage { get => _errorMessage; private set { if (_errorMessage != value) { _errorMessage = value; Notify(); Notify(nameof(HasError)); } } }
    public bool IsEnabled { get => _isEnabled; private set { if (_isEnabled != value) { _isEnabled = value; Notify(); } } }
    public bool IsLoading { get => _isLoading; private set { if (_isLoading != value) { _isLoading = value; Notify(); } } }
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public CalendarEventRow? SelectedEvent { get => _selectedEvent; set { if (_selectedEvent != value) { _selectedEvent = value; Notify(); } } }
    public string RealSchedulePlaceholder { get => _realSchedulePlaceholder; private set { if (_realSchedulePlaceholder != value) { _realSchedulePlaceholder = value; Notify(); } } }

    public void RefreshFlags()
        => _ = RefreshFlagsAsync();

    private async Task RefreshFlagsAsync()
    {
        try
        {
            var response = await Task.Run(() => _api.SendSystemFeatureFlagsSnapshotForPlayer());
            if (!Ok(response))
            {
                IsEnabled = true;
                StatusMessage = "Пробую открыть календарь, доступный игроку.";
                await LoadAsync();
                return;
            }
            var flags = Dicts(Get(response.Payload, "flags")).ToList();
            if (flags.Count == 0 && Get(response.Payload, "snapshot") is Dictionary<string, object> snapshot)
                flags = Dicts(Get(snapshot, "flags")).ToList();
            IsEnabled = Flag(flags, nameof(WorldCalendarFeatureFlags.UseWorldCalendarMvp))
                && Flag(flags, nameof(WorldCalendarFeatureFlags.UseWorldCalendarPlayerView));
            StatusMessage = IsEnabled
                ? "Календарь мира доступен. Показаны только раскрытые игрокам события."
                : "Календарь мира пока недоступен.";
            if (IsEnabled) await LoadAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = "Не удалось проверить доступность календаря мира.";
            ClientLogService.Instance.Error("player.world_calendar.flags.error", ex);
        }
    }

    private void Load()
        => _ = LoadAsync();

    private async Task LoadAsync()
    {
        if (!IsEnabled || IsLoading) return;
        IsLoading = true;
        ErrorMessage = string.Empty;
        try
        {
            ClientLogService.Instance.Info("player.world_calendar.load.start");
            var request = new Dictionary<string, object>
            {
                { "campaignId", CampaignId },
                { "characterId", _activeCharacterIdAccessor() }
            };
            var response = await Task.Run(() => _api.WorldCalendarPlayerGet(request));
            if (!Ok(response))
            {
                ErrorMessage = PlayerFacingMessage(response.Message, "Календарь мира пока недоступен.");
                return;
            }
            Apply(response.Payload);
            StatusMessage = "Календарь мира загружен.";
            ClientLogService.Instance.Info("player.world_calendar.load.done");
        }
        catch (Exception ex)
        {
            ErrorMessage = "Не удалось загрузить календарь мира. Проверьте подключение к серверу.";
            ClientLogService.Instance.Error("player.world_calendar.load.error", ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void Apply(Dictionary<string, object> payload)
    {
        var calendar = Map(Get(payload, "calendar"));
        var current = Map(Get(payload, "current"));
        CalendarName = First(S(Get(calendar, "name")), "Календарь мира");
        CurrentDisplay = First(S(Get(current, "display")), "Дата не загружена");
        CurrentSeason = S(Get(current, "season"));
        CurrentWeekDay = S(Get(current, "weekDay"));
        RealSchedulePlaceholder = First(S(Get(payload, "realSchedulePlaceholder")), RealSchedulePlaceholder);

        Months.Clear();
        foreach (var item in Dicts(Get(payload, "months"))) Months.Add(CalendarMonthRow.From(item));
        Events.Clear();
        foreach (var item in Dicts(Get(payload, "events"))) Events.Add(CalendarEventRow.From(item));
        Holidays.Clear();
        foreach (var item in Dicts(Get(payload, "holidays"))) Holidays.Add(HolidayRow.From(item));
    }

    private static bool Ok(ResponseEnvelope response) => response.Status == ResponseStatus.Ok;
    private static string Friendly(ResponseEnvelope response, string fallback) => string.IsNullOrWhiteSpace(response.Message) ? fallback : response.Message;
    private static string PlayerFacingMessage(string? message, string fallback)
    {
        if (string.IsNullOrWhiteSpace(message)) return fallback;
        if (message.IndexOf("feature flag", StringComparison.OrdinalIgnoreCase) >= 0 ||
            message.IndexOf("flags", StringComparison.OrdinalIgnoreCase) >= 0 ||
            message.IndexOf("player-safe", StringComparison.OrdinalIgnoreCase) >= 0)
            return fallback;
        return message;
    }
    private static object? Get(IDictionary<string, object> map, string key)
    {
        if (map.TryGetValue(key, out var value)) return value;
        var pair = map.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase));
        return string.IsNullOrEmpty(pair.Key) ? null : pair.Value;
    }

    private static Dictionary<string, object> Map(object? value)
    {
        if (value is Dictionary<string, object> typed) return typed;
        var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        if (value is IDictionary dictionary)
        {
            foreach (DictionaryEntry entry in dictionary)
            {
                var key = Convert.ToString(entry.Key) ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(key)) result[key] = entry.Value ?? string.Empty;
            }
            return result;
        }

        if (value is IEnumerable enumerable && value is not string)
        {
            foreach (var item in enumerable)
            {
                if (TryReadKeyValue(item, out var key, out var entryValue))
                    result[key] = entryValue ?? string.Empty;
            }
        }

        return result;
    }
    private static string S(object? value) => Convert.ToString(value) ?? string.Empty;
    private static bool B(object? value) => value is bool b ? b : bool.TryParse(S(value), out var parsed) && parsed;
    private static int I(object? value, int fallback = 0) => int.TryParse(S(value), out var parsed) ? parsed : fallback;
    private static string First(params string[] values) => values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim() ?? string.Empty;
    private static IEnumerable<Dictionary<string, object>> Dicts(object? value)
    {
        if (value is IEnumerable enumerable && value is not string)
            foreach (var item in enumerable)
            {
                var map = Map(item);
                if (map.Count > 0) yield return map;
            }
    }

    private static bool TryReadKeyValue(object? item, out string key, out object? value)
    {
        key = string.Empty;
        value = null;
        if (item == null) return false;
        if (item is IDictionary dictionary)
        {
            object? rawKey = null;
            object? rawValue = null;
            foreach (DictionaryEntry entry in dictionary)
            {
                var entryKey = Convert.ToString(entry.Key) ?? string.Empty;
                if (string.Equals(entryKey, "Key", StringComparison.OrdinalIgnoreCase)) rawKey = entry.Value;
                if (string.Equals(entryKey, "Value", StringComparison.OrdinalIgnoreCase)) rawValue = entry.Value;
            }
            key = Convert.ToString(rawKey) ?? string.Empty;
            value = rawValue;
            return !string.IsNullOrWhiteSpace(key);
        }

        var type = item.GetType();
        var keyProperty = type.GetProperty("Key");
        var valueProperty = type.GetProperty("Value");
        key = Convert.ToString(keyProperty?.GetValue(item, null)) ?? string.Empty;
        value = valueProperty?.GetValue(item, null);
        return !string.IsNullOrWhiteSpace(key);
    }
    private static bool Flag(IEnumerable<Dictionary<string, object>> flags, string name)
        => flags.Any(flag => MatchesFlagName(S(Get(flag, "name")), name)
                             && (B(Get(flag, "effectiveValue")) || B(Get(flag, "effective"))));
    private static bool MatchesFlagName(string actual, string expected)
        => string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase)
           || actual.EndsWith("." + expected, StringComparison.OrdinalIgnoreCase);

    public sealed class CalendarMonthRow
    {
        public int Order { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Display => $"{Order}. {Name} — {Description}";
        public static CalendarMonthRow From(Dictionary<string, object> map) => new() { Order = I(Get(map, "order")), Name = S(Get(map, "name")), Description = S(Get(map, "description")) };
    }

    public sealed class CalendarEventRow
    {
        public string Title { get; set; } = string.Empty;
        public string StartDisplay { get; set; } = string.Empty;
        public string EventTypeDisplay { get; set; } = string.Empty;
        public string StatusDisplay { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string AuthorDisplayName { get; set; } = string.Empty;
        public string Display => $"{StartDisplay}: {Title}";
        public static CalendarEventRow From(Dictionary<string, object> map) => new()
        {
            Title = S(Get(map, "title")),
            StartDisplay = S(Get(map, "startDisplay")),
            EventTypeDisplay = S(Get(map, "eventTypeDisplay")),
            StatusDisplay = S(Get(map, "statusDisplay")),
            Summary = First(S(Get(map, "publicSummary")), S(Get(map, "description"))),
            AuthorDisplayName = S(Get(map, "authorDisplayName"))
        };
    }

    public sealed class HolidayRow
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string DateText { get; set; } = string.Empty;
        public string Display => $"{DateText}: {Name}";
        public static HolidayRow From(Dictionary<string, object> map) => new() { Name = S(Get(map, "name")), Description = S(Get(map, "description")), DateText = $"{I(Get(map, "dayOfMonth"), 1):00}.{I(Get(map, "monthOrder"), 1):00}" };
    }
}
