using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Nri.AdminClient.Diagnostics;
using Nri.AdminClient.Networking;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;

namespace Nri.AdminClient.ViewModels;

public sealed class AdminRealScheduleViewModel : ViewModelBase
{
    private readonly CommandApi _api;
    private string _campaignId = "default";
    private string _statusMessage = "Расписание игр готово к подключению. Все Real Schedule flags выключены по умолчанию.";
    private string _errorMessage = string.Empty;
    private bool _isLoading;
    private bool _isEnabled;
    private bool _playerViewEnabled;
    private bool _sessionLinkEnabled;
    private bool _groupLinkEnabled;
    private bool _worldCalendarLinkEnabled;
    private bool _remindersEnabled;
    private ScheduleEventRow? _selectedEvent;
    private ParticipantRow? _selectedParticipant;
    private string _title = "Новая игра";
    private string _description = string.Empty;
    private string _eventType = RealScheduleEventTypeIds.GameSession;
    private string _eventStatus = RealScheduleEventStatusIds.Planned;
    private DateTime _startLocal = DateTime.Now.AddDays(7).Date.AddHours(19);
    private DateTime _endLocal = DateTime.Now.AddDays(7).Date.AddHours(23);
    private string _timeZoneId = TimeZoneInfo.Local.Id;
    private string _gmUserId = string.Empty;
    private string _gmDisplayName = string.Empty;
    private string _organizerDisplayName = string.Empty;
    private string _locationText = string.Empty;
    private string _connectionInfoSummary = string.Empty;
    private string _sessionId = string.Empty;
    private string _groupId = string.Empty;
    private string _linkedWorldCalendarEventId = string.Empty;
    private int _worldYear = 1611;
    private int _worldMonthOrder = 1;
    private int _worldDayOfMonth = 1;
    private bool _isPlayerVisible = true;
    private bool _reminderEnabled;
    private int _reminderBeforeMinutes = 60;
    private string _publicNotes = string.Empty;
    private string _gmNotes = string.Empty;
    private string _participantUserId = string.Empty;
    private string _participantDisplayName = "Игрок";
    private string _participantRole = RealScheduleParticipantRoleIds.Player;
    private string _participantResponse = RealScheduleParticipantResponseIds.Unknown;
    private bool _participantVisible = true;

    public AdminRealScheduleViewModel(CommandApi api)
    {
        _api = api;
        RefreshFlagsCommand = new RelayCommand(RefreshFlags);
        RefreshCommand = new RelayCommand(Load);
        CreateCommand = new RelayCommand(Create);
        UpdateCommand = new RelayCommand(Update);
        RescheduleCommand = new RelayCommand(Reschedule);
        CancelCommand = new RelayCommand(() => StatusChange(_api.RealScheduleCancel, "admin.schedule.cancel"));
        StartCommand = new RelayCommand(() => StatusChange(_api.RealScheduleStart, "admin.schedule.start"));
        CompleteCommand = new RelayCommand(() => StatusChange(_api.RealScheduleComplete, "admin.schedule.complete"));
        ArchiveCommand = new RelayCommand(() => StatusChange(_api.RealScheduleArchive, "admin.schedule.archive"));
        AddParticipantCommand = new RelayCommand(AddParticipant);
        UpdateParticipantCommand = new RelayCommand(UpdateParticipant);
        RemoveParticipantCommand = new RelayCommand(RemoveParticipant);
        ClearErrorCommand = new RelayCommand(() => ErrorMessage = string.Empty);
    }

    public ObservableCollection<ScheduleEventRow> Events { get; } = new();
    public ObservableCollection<ParticipantRow> Participants { get; } = new();
    public ObservableCollection<string> EventTypeOptions { get; } = new()
    {
        RealScheduleEventTypeIds.GameSession,
        RealScheduleEventTypeIds.CampaignSession,
        RealScheduleEventTypeIds.OneShot,
        RealScheduleEventTypeIds.Preparation,
        RealScheduleEventTypeIds.Maintenance,
        RealScheduleEventTypeIds.TechnicalWork,
        RealScheduleEventTypeIds.Meeting,
        RealScheduleEventTypeIds.Announcement,
        RealScheduleEventTypeIds.Custom
    };
    public ObservableCollection<string> StatusOptions { get; } = new()
    {
        RealScheduleEventStatusIds.Planned,
        RealScheduleEventStatusIds.Confirmed,
        RealScheduleEventStatusIds.Rescheduled,
        RealScheduleEventStatusIds.InProgress,
        RealScheduleEventStatusIds.Completed,
        RealScheduleEventStatusIds.Cancelled
    };
    public ObservableCollection<string> ParticipantRoleOptions { get; } = new()
    {
        RealScheduleParticipantRoleIds.Gm,
        RealScheduleParticipantRoleIds.Player,
        RealScheduleParticipantRoleIds.Observer,
        RealScheduleParticipantRoleIds.Assistant,
        RealScheduleParticipantRoleIds.Organizer,
        RealScheduleParticipantRoleIds.Custom
    };
    public ObservableCollection<string> ParticipantResponseOptions { get; } = new()
    {
        RealScheduleParticipantResponseIds.Invited,
        RealScheduleParticipantResponseIds.Accepted,
        RealScheduleParticipantResponseIds.Tentative,
        RealScheduleParticipantResponseIds.Declined,
        RealScheduleParticipantResponseIds.Unknown
    };

    public ICommand RefreshFlagsCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand CreateCommand { get; }
    public ICommand UpdateCommand { get; }
    public ICommand RescheduleCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand StartCommand { get; }
    public ICommand CompleteCommand { get; }
    public ICommand ArchiveCommand { get; }
    public ICommand AddParticipantCommand { get; }
    public ICommand UpdateParticipantCommand { get; }
    public ICommand RemoveParticipantCommand { get; }
    public ICommand ClearErrorCommand { get; }

    public string CampaignId { get => _campaignId; set { if (_campaignId != value) { _campaignId = value; Notify(); } } }
    public string StatusMessage { get => _statusMessage; private set { if (_statusMessage != value) { _statusMessage = value; Notify(); } } }
    public string ErrorMessage { get => _errorMessage; private set { if (_errorMessage != value) { _errorMessage = value; Notify(); Notify(nameof(HasError)); } } }
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool IsLoading { get => _isLoading; private set { if (_isLoading != value) { _isLoading = value; Notify(); } } }
    public bool IsEnabled { get => _isEnabled; private set { if (_isEnabled != value) { _isEnabled = value; Notify(); Notify(nameof(DisabledText)); } } }
    public bool PlayerViewEnabled { get => _playerViewEnabled; private set { if (_playerViewEnabled != value) { _playerViewEnabled = value; Notify(); } } }
    public bool SessionLinkEnabled { get => _sessionLinkEnabled; private set { if (_sessionLinkEnabled != value) { _sessionLinkEnabled = value; Notify(); } } }
    public bool GroupLinkEnabled { get => _groupLinkEnabled; private set { if (_groupLinkEnabled != value) { _groupLinkEnabled = value; Notify(); } } }
    public bool WorldCalendarLinkEnabled { get => _worldCalendarLinkEnabled; private set { if (_worldCalendarLinkEnabled != value) { _worldCalendarLinkEnabled = value; Notify(); } } }
    public bool RemindersEnabled { get => _remindersEnabled; private set { if (_remindersEnabled != value) { _remindersEnabled = value; Notify(); } } }
    public string DisabledText => IsEnabled ? string.Empty : "Расписание игр пока недоступно. Включите флаги функций Real Schedule для dev-проверки.";

    public ScheduleEventRow? SelectedEvent
    {
        get => _selectedEvent;
        set
        {
            if (_selectedEvent == value) return;
            _selectedEvent = value;
            Notify();
            if (value == null) return;

            Title = value.Title;
            Description = value.Description;
            EventType = value.EventType;
            EventStatus = value.Status;
            StartLocal = value.StartUtc.ToLocalTime();
            EndLocal = value.EndUtc?.ToLocalTime() ?? value.StartUtc.ToLocalTime().AddHours(4);
            TimeZoneId = First(value.TimeZoneId, TimeZoneInfo.Local.Id);
            GMDisplayName = value.GMDisplayName;
            LocationText = value.LocationText;
            ConnectionInfoSummary = value.ConnectionInfoSummary;
            SessionId = value.SessionId;
            GroupId = value.GroupId;
            IsPlayerVisible = value.IsPlayerVisible;
            ReminderEnabled = value.ReminderEnabled;
            ReminderBeforeMinutes = value.ReminderBeforeMinutes;
            PublicNotes = value.PublicNotes;
            GMNotes = value.GMNotes;
            LoadParticipants();
        }
    }

    public ParticipantRow? SelectedParticipant
    {
        get => _selectedParticipant;
        set
        {
            if (_selectedParticipant == value) return;
            _selectedParticipant = value;
            Notify();
            if (value == null) return;

            ParticipantUserId = value.UserId;
            ParticipantDisplayName = value.DisplayName;
            ParticipantRole = value.ParticipantRole;
            ParticipantResponse = value.ResponseStatus;
            ParticipantVisible = value.IsPlayerVisible;
        }
    }

    public string Title { get => _title; set { if (_title != value) { _title = value; Notify(); } } }
    public string Description { get => _description; set { if (_description != value) { _description = value; Notify(); } } }
    public string EventType { get => _eventType; set { if (_eventType != value) { _eventType = value; Notify(); } } }
    public string EventStatus { get => _eventStatus; set { if (_eventStatus != value) { _eventStatus = value; Notify(); } } }
    public DateTime StartLocal { get => _startLocal; set { if (_startLocal != value) { _startLocal = value; Notify(); } } }
    public DateTime EndLocal { get => _endLocal; set { if (_endLocal != value) { _endLocal = value; Notify(); } } }
    public string TimeZoneId { get => _timeZoneId; set { if (_timeZoneId != value) { _timeZoneId = value; Notify(); } } }
    public string GMUserId { get => _gmUserId; set { if (_gmUserId != value) { _gmUserId = value; Notify(); } } }
    public string GMDisplayName { get => _gmDisplayName; set { if (_gmDisplayName != value) { _gmDisplayName = value; Notify(); } } }
    public string OrganizerDisplayName { get => _organizerDisplayName; set { if (_organizerDisplayName != value) { _organizerDisplayName = value; Notify(); } } }
    public string LocationText { get => _locationText; set { if (_locationText != value) { _locationText = value; Notify(); } } }
    public string ConnectionInfoSummary { get => _connectionInfoSummary; set { if (_connectionInfoSummary != value) { _connectionInfoSummary = value; Notify(); } } }
    public string SessionId { get => _sessionId; set { if (_sessionId != value) { _sessionId = value; Notify(); } } }
    public string GroupId { get => _groupId; set { if (_groupId != value) { _groupId = value; Notify(); } } }
    public string LinkedWorldCalendarEventId { get => _linkedWorldCalendarEventId; set { if (_linkedWorldCalendarEventId != value) { _linkedWorldCalendarEventId = value; Notify(); } } }
    public int WorldYear { get => _worldYear; set { if (_worldYear != value) { _worldYear = value; Notify(); } } }
    public int WorldMonthOrder { get => _worldMonthOrder; set { if (_worldMonthOrder != value) { _worldMonthOrder = value; Notify(); } } }
    public int WorldDayOfMonth { get => _worldDayOfMonth; set { if (_worldDayOfMonth != value) { _worldDayOfMonth = value; Notify(); } } }
    public bool IsPlayerVisible { get => _isPlayerVisible; set { if (_isPlayerVisible != value) { _isPlayerVisible = value; Notify(); } } }
    public bool ReminderEnabled { get => _reminderEnabled; set { if (_reminderEnabled != value) { _reminderEnabled = value; Notify(); } } }
    public int ReminderBeforeMinutes { get => _reminderBeforeMinutes; set { if (_reminderBeforeMinutes != value) { _reminderBeforeMinutes = value; Notify(); } } }
    public string PublicNotes { get => _publicNotes; set { if (_publicNotes != value) { _publicNotes = value; Notify(); } } }
    public string GMNotes { get => _gmNotes; set { if (_gmNotes != value) { _gmNotes = value; Notify(); } } }
    public string ParticipantUserId { get => _participantUserId; set { if (_participantUserId != value) { _participantUserId = value; Notify(); } } }
    public string ParticipantDisplayName { get => _participantDisplayName; set { if (_participantDisplayName != value) { _participantDisplayName = value; Notify(); } } }
    public string ParticipantRole { get => _participantRole; set { if (_participantRole != value) { _participantRole = value; Notify(); } } }
    public string ParticipantResponse { get => _participantResponse; set { if (_participantResponse != value) { _participantResponse = value; Notify(); } } }
    public bool ParticipantVisible { get => _participantVisible; set { if (_participantVisible != value) { _participantVisible = value; Notify(); } } }

    public void RefreshFlags()
    {
        try
        {
            var response = _api.SystemFeatureFlagsSnapshot();
            if (!Ok(response)) { ErrorMessage = Friendly(response, "Не удалось загрузить флаги функций расписания."); return; }
            var flags = Dicts(Get(response.Payload, "flags")).ToList();
            if (flags.Count == 0 && Get(response.Payload, "snapshot") is Dictionary<string, object> snapshot)
                flags = Dicts(Get(snapshot, "flags")).ToList();
            IsEnabled = Flag(flags, nameof(RealScheduleFeatureFlags.UseRealScheduleCalendarMvp)) && Flag(flags, nameof(RealScheduleFeatureFlags.UseRealScheduleEvents));
            PlayerViewEnabled = Flag(flags, nameof(RealScheduleFeatureFlags.UseRealSchedulePlayerView));
            SessionLinkEnabled = Flag(flags, nameof(RealScheduleFeatureFlags.UseRealScheduleSessionLink));
            GroupLinkEnabled = Flag(flags, nameof(RealScheduleFeatureFlags.UseRealScheduleGroupLink));
            WorldCalendarLinkEnabled = Flag(flags, nameof(RealScheduleFeatureFlags.UseRealScheduleWorldCalendarLink));
            RemindersEnabled = Flag(flags, nameof(RealScheduleFeatureFlags.UseRealScheduleReminders));
            StatusMessage = IsEnabled ? "Real Schedule доступен. Можно вести реальные даты ближайших игр и техработ." : DisabledText;
            if (IsEnabled) Load();
        }
        catch (Exception ex)
        {
            ErrorMessage = "Не удалось проверить Real Schedule flags.";
            ClientLogService.Instance.Error("admin.schedule.flags.error", ex);
        }
    }

    private void Load()
    {
        if (!IsEnabled || IsLoading) return;
        IsLoading = true;
        ErrorMessage = string.Empty;
        try
        {
            ClientLogService.Instance.Info("admin.schedule.load.start");
            var response = _api.RealScheduleList(new Dictionary<string, object> { { "campaignId", CampaignId } });
            if (!Ok(response)) { ErrorMessage = Friendly(response, "Расписание игр пока недоступно."); return; }
            Events.Clear();
            foreach (var item in Dicts(Get(response.Payload, "items"))) Events.Add(ScheduleEventRow.From(item));
            StatusMessage = Events.Count == 0 ? "В расписании пока нет событий." : $"Событий в расписании: {Events.Count}.";
            ClientLogService.Instance.Info("admin.schedule.load.done");
        }
        catch (Exception ex)
        {
            ErrorMessage = "Не удалось загрузить расписание. Проверьте подключение к серверу.";
            ClientLogService.Instance.Error("admin.schedule.load.error", ex);
        }
        finally { IsLoading = false; }
    }

    private void Create() => SendAndRefresh(() => _api.RealScheduleCreate(EventPayload()), "admin.schedule.create");

    private void Update()
    {
        if (SelectedEvent == null) return;
        var payload = EventPayload();
        payload["eventId"] = SelectedEvent.EventId;
        SendAndRefresh(() => _api.RealScheduleUpdate(payload), "admin.schedule.update");
    }

    private void Reschedule()
    {
        if (SelectedEvent == null) return;
        SendAndRefresh(() => _api.RealScheduleReschedule(new Dictionary<string, object>
        {
            { "eventId", SelectedEvent.EventId },
            { "startUtc", StartLocal.ToUniversalTime().ToString("O") },
            { "endUtc", EndLocal.ToUniversalTime().ToString("O") },
            { "timeZoneId", TimeZoneId }
        }), "admin.schedule.reschedule");
    }

    private void StatusChange(Func<Dictionary<string, object>, ResponseEnvelope> action, string logKey)
    {
        if (SelectedEvent == null) return;
        SendAndRefresh(() => action(new Dictionary<string, object> { { "eventId", SelectedEvent.EventId } }), logKey);
    }

    private void LoadParticipants()
    {
        Participants.Clear();
        if (SelectedEvent == null) return;
        try
        {
            var response = _api.RealScheduleParticipantList(new Dictionary<string, object> { { "eventId", SelectedEvent.EventId } });
            if (!Ok(response)) return;
            foreach (var item in Dicts(Get(response.Payload, "items"))) Participants.Add(ParticipantRow.From(item));
        }
        catch (Exception ex)
        {
            ClientLogService.Instance.Error("admin.schedule.participants.load.error", ex);
        }
    }

    private void AddParticipant()
    {
        if (SelectedEvent == null) return;
        SendParticipantAndRefresh(() => _api.RealScheduleParticipantAdd(ParticipantPayload(new Dictionary<string, object> { { "eventId", SelectedEvent.EventId } })), "admin.schedule.participant.add");
    }

    private void UpdateParticipant()
    {
        if (SelectedParticipant == null) return;
        SendParticipantAndRefresh(() => _api.RealScheduleParticipantUpdate(ParticipantPayload(new Dictionary<string, object> { { "participantId", SelectedParticipant.ParticipantId } })), "admin.schedule.participant.update");
    }

    private void RemoveParticipant()
    {
        if (SelectedParticipant == null) return;
        SendParticipantAndRefresh(() => _api.RealScheduleParticipantRemove(new Dictionary<string, object> { { "participantId", SelectedParticipant.ParticipantId } }), "admin.schedule.participant.remove");
    }

    private Dictionary<string, object> EventPayload() => new()
    {
        { "campaignId", CampaignId },
        { "title", Title },
        { "description", Description },
        { "eventType", EventType },
        { "status", EventStatus },
        { "startUtc", StartLocal.ToUniversalTime().ToString("O") },
        { "endUtc", EndLocal.ToUniversalTime().ToString("O") },
        { "timeZoneId", TimeZoneId },
        { "gmUserId", GMUserId },
        { "gmDisplayName", GMDisplayName },
        { "organizerDisplayName", OrganizerDisplayName },
        { "locationText", LocationText },
        { "connectionInfoSummary", ConnectionInfoSummary },
        { "sessionId", SessionId },
        { "groupId", GroupId },
        { "linkedWorldCalendarEventId", LinkedWorldCalendarEventId },
        { "worldYear", WorldYear },
        { "worldMonthOrder", WorldMonthOrder },
        { "worldDayOfMonth", WorldDayOfMonth },
        { "isPlayerVisible", IsPlayerVisible },
        { "visibilityMode", IsPlayerVisible ? RealScheduleVisibilityModeIds.PlayerVisible : RealScheduleVisibilityModeIds.GmOnly },
        { "reminderEnabled", ReminderEnabled },
        { "reminderBeforeMinutes", ReminderBeforeMinutes },
        { "publicNotes", PublicNotes },
        { "gmNotes", GMNotes }
    };

    private Dictionary<string, object> ParticipantPayload(Dictionary<string, object> payload)
    {
        payload["userId"] = ParticipantUserId;
        payload["displayName"] = ParticipantDisplayName;
        payload["participantRole"] = ParticipantRole;
        payload["responseStatus"] = ParticipantResponse;
        payload["isPlayerVisible"] = ParticipantVisible;
        return payload;
    }

    private void SendAndRefresh(Func<ResponseEnvelope> send, string logKey)
    {
        try
        {
            ClientLogService.Instance.Info($"{logKey}.start");
            var response = send();
            if (!Ok(response)) { ErrorMessage = Friendly(response, "Операция расписания не выполнена."); return; }
            ClientLogService.Instance.Info($"{logKey}.done");
            Load();
        }
        catch (Exception ex)
        {
            ErrorMessage = "Операция расписания не выполнена. Проверьте данные и подключение.";
            ClientLogService.Instance.Error($"{logKey}.error", ex);
        }
    }

    private void SendParticipantAndRefresh(Func<ResponseEnvelope> send, string logKey)
    {
        try
        {
            var response = send();
            if (!Ok(response)) { ErrorMessage = Friendly(response, "Операция с участником не выполнена."); return; }
            LoadParticipants();
        }
        catch (Exception ex)
        {
            ErrorMessage = "Операция с участником не выполнена.";
            ClientLogService.Instance.Error($"{logKey}.error", ex);
        }
    }

    private static bool Ok(ResponseEnvelope response) => response.Status == ResponseStatus.Ok;
    private static string Friendly(ResponseEnvelope response, string fallback) => string.IsNullOrWhiteSpace(response.Message) ? fallback : response.Message;
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
        public string CampaignId { get; set; } = string.Empty;
        public string SessionId { get; set; } = string.Empty;
        public string GroupId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string EventType { get; set; } = string.Empty;
        public string EventTypeDisplay { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string StatusDisplay { get; set; } = string.Empty;
        public DateTime StartUtc { get; set; }
        public DateTime? EndUtc { get; set; }
        public string TimeZoneId { get; set; } = string.Empty;
        public string LocalStartDisplay { get; set; } = string.Empty;
        public string CountdownText { get; set; } = string.Empty;
        public string GMDisplayName { get; set; } = string.Empty;
        public string LocationText { get; set; } = string.Empty;
        public string ConnectionInfoSummary { get; set; } = string.Empty;
        public bool IsPlayerVisible { get; set; }
        public bool ReminderEnabled { get; set; }
        public int ReminderBeforeMinutes { get; set; }
        public string PublicNotes { get; set; } = string.Empty;
        public string GMNotes { get; set; } = string.Empty;
        public string Display => $"{LocalStartDisplay}: {Title}";
        public string Meta => $"{StatusDisplay} | {EventTypeDisplay} | Проводит: {First(GMDisplayName, "GM не указан")}";
        public static ScheduleEventRow From(Dictionary<string, object> map) => new()
        {
            EventId = S(Get(map, "eventId")),
            CampaignId = S(Get(map, "campaignId")),
            SessionId = S(Get(map, "sessionId")),
            GroupId = S(Get(map, "groupId")),
            Title = S(Get(map, "title")),
            Description = S(Get(map, "description")),
            EventType = S(Get(map, "eventType")),
            EventTypeDisplay = S(Get(map, "eventTypeDisplay")),
            Status = S(Get(map, "status")),
            StatusDisplay = S(Get(map, "statusDisplay")),
            StartUtc = Dt(Get(map, "startUtc"), DateTime.UtcNow),
            EndUtc = string.IsNullOrWhiteSpace(S(Get(map, "endUtc"))) ? null : Dt(Get(map, "endUtc"), DateTime.UtcNow),
            TimeZoneId = S(Get(map, "timeZoneId")),
            LocalStartDisplay = S(Get(map, "localStartDisplay")),
            CountdownText = S(Get(map, "countdownText")),
            GMDisplayName = S(Get(map, "gmDisplayName")),
            LocationText = S(Get(map, "locationText")),
            ConnectionInfoSummary = S(Get(map, "connectionInfoSummary")),
            IsPlayerVisible = B(Get(map, "isPlayerVisible")),
            ReminderEnabled = B(Get(map, "reminderEnabled")),
            ReminderBeforeMinutes = I(Get(map, "reminderBeforeMinutes"), 0),
            PublicNotes = S(Get(map, "publicNotes")),
            GMNotes = S(Get(map, "gmNotes"))
        };
    }

    public sealed class ParticipantRow
    {
        public string ParticipantId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string ParticipantRole { get; set; } = string.Empty;
        public string ParticipantRoleDisplay { get; set; } = string.Empty;
        public string ResponseStatus { get; set; } = string.Empty;
        public string ResponseStatusDisplay { get; set; } = string.Empty;
        public bool IsPlayerVisible { get; set; }
        public string Display => $"{DisplayName} | {ParticipantRoleDisplay} | {ResponseStatusDisplay}";
        public static ParticipantRow From(Dictionary<string, object> map) => new()
        {
            ParticipantId = S(Get(map, "participantId")),
            UserId = S(Get(map, "userId")),
            DisplayName = S(Get(map, "displayName")),
            ParticipantRole = S(Get(map, "participantRole")),
            ParticipantRoleDisplay = S(Get(map, "participantRoleDisplay")),
            ResponseStatus = S(Get(map, "responseStatus")),
            ResponseStatusDisplay = S(Get(map, "responseStatusDisplay")),
            IsPlayerVisible = B(Get(map, "isPlayerVisible"))
        };
    }
}
