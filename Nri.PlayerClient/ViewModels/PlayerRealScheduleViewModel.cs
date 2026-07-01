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

public sealed class PlayerRealScheduleViewModel : ViewModelBase
{
    private readonly CommandApi _api;
    private string _campaignId = "default";
    private string _statusMessage = "Расписание игр будет загружено после входа.";
    private string _errorMessage = string.Empty;
    private bool _isEnabled;
    private bool _isLoading;
    private ScheduleEventRow? _selectedEvent;
    private ScheduleEventRow? _nextEvent;

    public PlayerRealScheduleViewModel(CommandApi api)
    {
        _api = api;
        RefreshFlagsCommand = new RelayCommand(RefreshFlags);
        RefreshCommand = new RelayCommand(Load);
        ClearErrorCommand = new RelayCommand(() => ErrorMessage = string.Empty);
    }

    public ObservableCollection<ScheduleEventRow> Events { get; } = new();
    public ICommand RefreshFlagsCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand ClearErrorCommand { get; }

    public string CampaignId { get => _campaignId; set { if (_campaignId != value) { _campaignId = value; Notify(); } } }
    public string StatusMessage { get => _statusMessage; private set { if (_statusMessage != value) { _statusMessage = value; Notify(); } } }
    public string ErrorMessage { get => _errorMessage; private set { if (_errorMessage != value) { _errorMessage = value; Notify(); Notify(nameof(HasError)); } } }
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool IsEnabled { get => _isEnabled; private set { if (_isEnabled != value) { _isEnabled = value; Notify(); Notify(nameof(DisabledText)); } } }
    public bool IsLoading { get => _isLoading; private set { if (_isLoading != value) { _isLoading = value; Notify(); } } }
    public string DisabledText => IsEnabled ? string.Empty : "Расписание игр пока недоступно.";

    public ScheduleEventRow? SelectedEvent { get => _selectedEvent; set { if (_selectedEvent != value) { _selectedEvent = value; Notify(); } } }
    public ScheduleEventRow? NextEvent { get => _nextEvent; private set { if (_nextEvent != value) { _nextEvent = value; Notify(); Notify(nameof(HasNextEvent)); } } }
    public bool HasNextEvent => NextEvent != null;

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
                StatusMessage = "Пробую открыть расписание, доступное игроку.";
                await LoadAsync();
                return;
            }
            var flags = Dicts(Get(response.Payload, "flags")).ToList();
            if (flags.Count == 0 && Get(response.Payload, "snapshot") is Dictionary<string, object> snapshot)
                flags = Dicts(Get(snapshot, "flags")).ToList();
            IsEnabled = Flag(flags, nameof(RealScheduleFeatureFlags.UseRealScheduleCalendarMvp)) && Flag(flags, nameof(RealScheduleFeatureFlags.UseRealSchedulePlayerView));
            StatusMessage = IsEnabled ? "Расписание игр доступно. Показаны только события, раскрытые игрокам." : DisabledText;
            if (IsEnabled) await LoadAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = "Не удалось проверить доступность расписания.";
            ClientLogService.Instance.Error("player.schedule.flags.error", ex);
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
            ClientLogService.Instance.Info("player.schedule.load.start");
            var nextRequest = new Dictionary<string, object> { { "campaignId", CampaignId } };
            var next = await Task.Run(() => _api.RealSchedulePlayerNext(nextRequest));
            if (Ok(next) && B(Get(next.Payload, "hasNext")))
            {
                NextEvent = ScheduleEventRow.From(Map(Get(next.Payload, "item")));
            }
            else
            {
                NextEvent = null;
            }

            var listRequest = new Dictionary<string, object> { { "campaignId", CampaignId } };
            var list = await Task.Run(() => _api.RealSchedulePlayerList(listRequest));
            if (!Ok(list)) { ErrorMessage = PlayerFacingMessage(list.Message, "Расписание игр пока недоступно."); return; }
            Events.Clear();
            foreach (var item in Dicts(Get(list.Payload, "items"))) Events.Add(ScheduleEventRow.From(item));
            StatusMessage = Events.Count == 0 ? "Ближайших открытых событий пока нет." : $"Открытых событий: {Events.Count}.";
            ClientLogService.Instance.Info("player.schedule.load.done");
        }
        catch (Exception ex)
        {
            ErrorMessage = "Не удалось загрузить расписание. Проверьте подключение к серверу.";
            ClientLogService.Instance.Error("player.schedule.load.error", ex);
        }
        finally { IsLoading = false; }
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
    private static DateTime Dt(object? value, DateTime fallback) => DateTime.TryParse(S(value), out var parsed) ? parsed.ToUniversalTime() : fallback;
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

    public sealed class ScheduleEventRow
    {
        public string EventId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string EventTypeDisplay { get; set; } = string.Empty;
        public string StatusDisplay { get; set; } = string.Empty;
        public DateTime StartUtc { get; set; }
        public string TimeZoneId { get; set; } = string.Empty;
        public string LocalStartDisplay { get; set; } = string.Empty;
        public string CountdownText { get; set; } = string.Empty;
        public string GMDisplayName { get; set; } = string.Empty;
        public string LocationText { get; set; } = string.Empty;
        public string ConnectionInfoSummary { get; set; } = string.Empty;
        public string PublicNotes { get; set; } = string.Empty;
        public ObservableCollection<ParticipantRow> Participants { get; } = new();
        public string Display => $"{LocalStartDisplay}: {Title}";
        public string Meta => $"{StatusDisplay} | {EventTypeDisplay} | Проводит: {First(GMDisplayName, "GM не указан")}";
        public static ScheduleEventRow From(Dictionary<string, object> map)
        {
            var row = new ScheduleEventRow
            {
                EventId = S(Get(map, "eventId")),
                Title = S(Get(map, "title")),
                Description = S(Get(map, "description")),
                EventTypeDisplay = S(Get(map, "eventTypeDisplay")),
                StatusDisplay = S(Get(map, "statusDisplay")),
                StartUtc = Dt(Get(map, "startUtc"), DateTime.UtcNow),
                TimeZoneId = S(Get(map, "timeZoneId")),
                LocalStartDisplay = S(Get(map, "localStartDisplay")),
                CountdownText = S(Get(map, "countdownText")),
                GMDisplayName = S(Get(map, "gmDisplayName")),
                LocationText = S(Get(map, "locationText")),
                ConnectionInfoSummary = S(Get(map, "connectionInfoSummary")),
                PublicNotes = S(Get(map, "publicNotes"))
            };
            foreach (var participant in Dicts(Get(map, "participants"))) row.Participants.Add(ParticipantRow.From(participant));
            return row;
        }
    }

    public sealed class ParticipantRow
    {
        public string DisplayName { get; set; } = string.Empty;
        public string ParticipantRoleDisplay { get; set; } = string.Empty;
        public string ResponseStatusDisplay { get; set; } = string.Empty;
        public string Display => $"{DisplayName} | {ParticipantRoleDisplay} | {ResponseStatusDisplay}";
        public static ParticipantRow From(Dictionary<string, object> map) => new()
        {
            DisplayName = S(Get(map, "displayName")),
            ParticipantRoleDisplay = S(Get(map, "participantRoleDisplay")),
            ResponseStatusDisplay = S(Get(map, "responseStatusDisplay"))
        };
    }
}
