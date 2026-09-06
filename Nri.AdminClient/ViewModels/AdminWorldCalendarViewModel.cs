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

public sealed class AdminWorldCalendarViewModel : ViewModelBase
{
    private readonly CommandApi _api;
    private string _campaignId = "default";
    private string _ruleSetId = string.Empty;
    private string _calendarId = string.Empty;
    private string _calendarName = "Календарь мира";
    private string _currentDisplay = "Дата не загружена";
    private string _currentSeason = string.Empty;
    private string _currentWeekDay = string.Empty;
    private int _year = 1611;
    private int _monthOrder = 1;
    private int _dayOfMonth = 1;
    private int _hour;
    private int _minute;
    private int _eventDurationMinutes = 60;
    private string _selectedEra = WorldCalendarEraLabels.CommonEra;
    private int _advanceDays = 1;
    private string _advanceReason = string.Empty;
    private string _statusMessage = "Календарь мира готов к подключению. Все World Calendar flags выключены по умолчанию.";
    private string _errorMessage = string.Empty;
    private bool _isLoading;
    private bool _calendarEnabled;
    private bool _currentDateEnabled;
    private bool _eventsEnabled;
    private bool _chronicleEnabled;
    private bool _holidaysEnabled;
    private bool _remindersEnabled;
    private bool _playerViewEnabled;
    private bool _futureVisibilityEnabled;
    private bool _hasUnsavedChanges;
    private bool _hasRoutePermission;
    private bool _isLoadingEventDraft;
    private string _eventSearchText = string.Empty;
    private string _selectedParticipantId = string.Empty;
    private string _selectedLocationId = string.Empty;
    private CalendarEventRow? _selectedEvent;
    private HolidayRow? _selectedHoliday;
    private ReminderRow? _selectedReminder;
    private string _eventTitle = "Новое событие";
    private string _eventSummary = string.Empty;
    private string _eventGmSummary = string.Empty;
    private string _eventType = WorldCalendarEventTypeIds.Fixed;
    private string _eventStatus = WorldCalendarEventStatusIds.Planned;
    private string _eventRevealPolicy = WorldCalendarRevealPolicyIds.Manual;
    private bool _eventPlayerVisible;
    private string _versionSummary = string.Empty;
    private string _versionType = WorldCalendarVersionTypeIds.OfficialHistory;
    private bool _versionPlayerVisible;
    private string _holidayName = "Праздник";
    private string _holidayDescription = string.Empty;
    private int _holidayMonth = 1;
    private int _holidayDay = 1;
    private bool _holidayPlayerVisible = true;
    private string _reminderTitle = "Напоминание";
    private string _reminderNotes = string.Empty;
    private string _realSchedulePlaceholder = "Расписание игр подключено во вкладке «Расписание игр» рядом с календарём мира.";

    public AdminWorldCalendarViewModel(CommandApi api)
    {
        _api = api;
        RefreshFlagsCommand = new RelayCommand(RefreshFlags);
        EnsureCalendarCommand = new RelayCommand(EnsureCalendar);
        RefreshCommand = new RelayCommand(LoadCalendar);
        SetDateCommand = new RelayCommand(SetDate);
        AdvanceTimeCommand = new RelayCommand(AdvanceTime);
        CreateEventCommand = new RelayCommand(CreateEvent, () => CanSaveEventDraft);
        UpdateEventCommand = new RelayCommand(UpdateEvent, () => CanSaveEventDraft && SelectedEvent != null);
        CancelEventCommand = new RelayCommand(CancelEvent);
        ArchiveEventCommand = new RelayCommand(ArchiveEvent);
        AddVersionCommand = new RelayCommand(AddVersion);
        CreateHolidayCommand = new RelayCommand(CreateHoliday);
        ArchiveHolidayCommand = new RelayCommand(ArchiveHoliday);
        CreateReminderCommand = new RelayCommand(CreateReminder);
        DismissReminderCommand = new RelayCommand(DismissReminder);
        ClearErrorCommand = new RelayCommand(() => ErrorMessage = string.Empty);
    }

    public ObservableCollection<CalendarMonthRow> Months { get; } = new();
    public ObservableCollection<CalendarEventRow> Events { get; } = new();
    public ObservableCollection<HolidayRow> Holidays { get; } = new();
    public ObservableCollection<ReminderRow> Reminders { get; } = new();
    public ObservableCollection<object> ChronicleParticipantOptions { get; } = new();
    public ObservableCollection<object> SelectedParticipantReferences { get; } = new();
    public ObservableCollection<string> EraOptions { get; } = new()
    {
        WorldCalendarEraLabels.CommonEra,
        WorldCalendarEraLabels.BeforeCommonEra
    };
    public ObservableCollection<object> ChronicleLocationOptions { get; } = new();
    public ObservableCollection<string> EventTypeOptions { get; } = new() { WorldCalendarEventTypeIds.Fixed, WorldCalendarEventTypeIds.Flexible, WorldCalendarEventTypeIds.Conditional, WorldCalendarEventTypeIds.Optional, WorldCalendarEventTypeIds.Cancelled };
    public ObservableCollection<string> EventStatusOptions { get; } = new() { WorldCalendarEventStatusIds.Planned, WorldCalendarEventStatusIds.Upcoming, WorldCalendarEventStatusIds.Occurred, WorldCalendarEventStatusIds.Resolved, WorldCalendarEventStatusIds.Cancelled };
    public ObservableCollection<string> RevealPolicyOptions { get; } = new() { WorldCalendarRevealPolicyIds.Manual, WorldCalendarRevealPolicyIds.Immediate, WorldCalendarRevealPolicyIds.AtDate, WorldCalendarRevealPolicyIds.Hidden };
    public ObservableCollection<string> VersionTypeOptions { get; } = new() { WorldCalendarVersionTypeIds.OfficialHistory, WorldCalendarVersionTypeIds.PlayerVisible, WorldCalendarVersionTypeIds.GmOnly, WorldCalendarVersionTypeIds.Alternative, WorldCalendarVersionTypeIds.Custom };

    public ICommand RefreshFlagsCommand { get; }
    public ICommand EnsureCalendarCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand SetDateCommand { get; }
    public ICommand AdvanceTimeCommand { get; }
    public ICommand CreateEventCommand { get; }
    public ICommand UpdateEventCommand { get; }
    public ICommand CancelEventCommand { get; }
    public ICommand ArchiveEventCommand { get; }
    public ICommand AddVersionCommand { get; }
    public ICommand CreateHolidayCommand { get; }
    public ICommand ArchiveHolidayCommand { get; }
    public ICommand CreateReminderCommand { get; }
    public ICommand DismissReminderCommand { get; }
    public ICommand ClearErrorCommand { get; }

    public string CampaignId { get => _campaignId; set { if (_campaignId != value) { _campaignId = value; Notify(); } } }
    public string RuleSetId { get => _ruleSetId; set { if (_ruleSetId != value) { _ruleSetId = value; Notify(); } } }
    public string CalendarName { get => _calendarName; private set { if (_calendarName != value) { _calendarName = value; Notify(); } } }
    public string CurrentDisplay { get => _currentDisplay; private set { if (_currentDisplay != value) { _currentDisplay = value; Notify(); } } }
    public string CurrentSeason { get => _currentSeason; private set { if (_currentSeason != value) { _currentSeason = value; Notify(); } } }
    public string CurrentWeekDay { get => _currentWeekDay; private set { if (_currentWeekDay != value) { _currentWeekDay = value; Notify(); } } }
    public int Year { get => _year; set { if (_year != value) { _year = value; Notify(); } } }
    public string SelectedEra
    {
        get => _selectedEra;
        set
        {
            if (_selectedEra == value) return;
            _selectedEra = value;
            Notify();
            Notify(nameof(IsBeforeCommonEra));
        }
    }
    public bool IsBeforeCommonEra
    {
        get => string.Equals(SelectedEra, WorldCalendarEraLabels.BeforeCommonEra, StringComparison.OrdinalIgnoreCase);
        set => SelectedEra = value ? WorldCalendarEraLabels.BeforeCommonEra : WorldCalendarEraLabels.CommonEra;
    }
    public int MonthOrder { get => _monthOrder; set { if (_monthOrder != value) { _monthOrder = value; Notify(); } } }
    public int DayOfMonth { get => _dayOfMonth; set { if (_dayOfMonth != value) { _dayOfMonth = value; Notify(); } } }
    public int Hour { get => _hour; set { if (_hour != value) { _hour = value; Notify(); } } }
    public int Minute { get => _minute; set { if (_minute != value) { _minute = value; Notify(); } } }
    public int EventDurationMinutes { get => _eventDurationMinutes; set { if (_eventDurationMinutes != value) { _eventDurationMinutes = value; Notify(); MarkDirty(); } } }
    public int AdvanceDays { get => _advanceDays; set { if (_advanceDays != value) { _advanceDays = value; Notify(); } } }
    public string AdvanceReason { get => _advanceReason; set { if (_advanceReason != value) { _advanceReason = value; Notify(); } } }
    public string StatusMessage { get => _statusMessage; private set { if (_statusMessage != value) { _statusMessage = value; Notify(); Notify(nameof(HasStatusMessage)); } } }
    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);
    public string ErrorMessage { get => _errorMessage; private set { if (_errorMessage != value) { _errorMessage = value; Notify(); Notify(nameof(HasError)); Notify(nameof(RouteState)); } } }
    public bool IsLoading { get => _isLoading; private set { if (_isLoading != value) { _isLoading = value; Notify(); Notify(nameof(RouteState)); Notify(nameof(CanSaveEventDraft)); RaiseEventCommandStates(); } } }
    public bool CalendarEnabled { get => _calendarEnabled; private set { if (_calendarEnabled != value) { _calendarEnabled = value; Notify(); Notify(nameof(DisabledText)); } } }
    public bool CurrentDateEnabled { get => _currentDateEnabled; private set { if (_currentDateEnabled != value) { _currentDateEnabled = value; Notify(); } } }
    public bool EventsEnabled { get => _eventsEnabled; private set { if (_eventsEnabled != value) { _eventsEnabled = value; Notify(); } } }
    public bool ChronicleEnabled { get => _chronicleEnabled; private set { if (_chronicleEnabled != value) { _chronicleEnabled = value; Notify(); } } }
    public bool HolidaysEnabled { get => _holidaysEnabled; private set { if (_holidaysEnabled != value) { _holidaysEnabled = value; Notify(); } } }
    public bool RemindersEnabled { get => _remindersEnabled; private set { if (_remindersEnabled != value) { _remindersEnabled = value; Notify(); } } }
    public bool PlayerViewEnabled { get => _playerViewEnabled; private set { if (_playerViewEnabled != value) { _playerViewEnabled = value; Notify(); } } }
    public bool FutureVisibilityEnabled { get => _futureVisibilityEnabled; private set { if (_futureVisibilityEnabled != value) { _futureVisibilityEnabled = value; Notify(); } } }
    public string DisabledText => CalendarEnabled ? string.Empty : "Календарь мира выключен флагами функций.";
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool HasRoutePermission { get => _hasRoutePermission; set { if (_hasRoutePermission == value) return; _hasRoutePermission = value; Notify(); Notify(nameof(PermissionState)); Notify(nameof(RouteState)); Notify(nameof(CanSaveEventDraft)); RaiseEventCommandStates(); } }
    public string PermissionState => HasRoutePermission ? "Раздел доступен." : "Войдите администратором, чтобы открыть хронику.";
    public string EventSearchText { get => _eventSearchText; set { if (_eventSearchText == value) return; _eventSearchText = value ?? string.Empty; Notify(); Notify(nameof(FilteredEvents)); Notify(nameof(RouteState)); } }
    public IEnumerable<CalendarEventRow> FilteredEvents => Events.Where(item =>
        string.IsNullOrWhiteSpace(EventSearchText)
        || item.Title.IndexOf(EventSearchText, StringComparison.OrdinalIgnoreCase) >= 0
        || item.StatusDisplay.IndexOf(EventSearchText, StringComparison.OrdinalIgnoreCase) >= 0
        || item.EventTypeDisplay.IndexOf(EventSearchText, StringComparison.OrdinalIgnoreCase) >= 0);
    public bool HasUnsavedChanges { get => _hasUnsavedChanges; private set { if (_hasUnsavedChanges == value) return; _hasUnsavedChanges = value; Notify(); Notify(nameof(UnsavedChangesSummary)); Notify(nameof(HasValidationIssues)); Notify(nameof(ValidationSummary)); Notify(nameof(CanSaveEventDraft)); RaiseEventCommandStates(); } }
    public bool HasValidationIssues => HasUnsavedChanges && string.IsNullOrWhiteSpace(EventTitle);
    public string ValidationSummary => HasValidationIssues ? "Укажите название события." : string.Empty;
    public bool CanSaveEventDraft => HasRoutePermission && !IsLoading && !HasValidationIssues;
    public string RouteState => AdminRouteStateResolver.ResolveCollection(
        HasRoutePermission,
        IsLoading,
        HasError,
        !string.IsNullOrWhiteSpace(EventSearchText),
        FilteredEvents.Any(),
        true,
        SelectedEvent != null || HasUnsavedChanges);
    public string UnsavedChangesSummary => HasUnsavedChanges ? "Есть несохранённые изменения хроники." : "Изменений хроники нет.";
    public string SelectedParticipantId { get => _selectedParticipantId; set { if (_selectedParticipantId != value) { _selectedParticipantId = value ?? string.Empty; Notify(); MarkDirty(); } } }
    public string SelectedLocationId { get => _selectedLocationId; set { if (_selectedLocationId != value) { _selectedLocationId = value ?? string.Empty; Notify(); MarkDirty(); } } }

    public CalendarEventRow? SelectedEvent
    {
        get => _selectedEvent;
        set
        {
            if (_selectedEvent == value) return;
            _selectedEvent = value;
            Notify();
            Notify(nameof(RouteState));
            Notify(nameof(CanSaveEventDraft));
            RaiseEventCommandStates();
            if (value != null)
            {
                _isLoadingEventDraft = true;
                EventTitle = value.Title;
                EventSummary = value.PublicSummary;
                EventGmSummary = value.GmSummary;
                EventType = value.EventType;
                EventStatus = value.Status;
                EventRevealPolicy = value.RevealPolicy;
                EventPlayerVisible = value.IsPlayerVisible;
                ApplySignedYear(value.Year);
                MonthOrder = value.MonthOrder;
                DayOfMonth = value.DayOfMonth;
                _isLoadingEventDraft = false;
                HasUnsavedChanges = false;
            }
        }
    }

    public HolidayRow? SelectedHoliday { get => _selectedHoliday; set { if (_selectedHoliday != value) { _selectedHoliday = value; Notify(); } } }
    public ReminderRow? SelectedReminder { get => _selectedReminder; set { if (_selectedReminder != value) { _selectedReminder = value; Notify(); } } }
    public string EventTitle { get => _eventTitle; set { if (_eventTitle != value) { _eventTitle = value; Notify(); MarkDirty(); Notify(nameof(HasValidationIssues)); Notify(nameof(ValidationSummary)); Notify(nameof(CanSaveEventDraft)); RaiseEventCommandStates(); } } }
    public string EventSummary { get => _eventSummary; set { if (_eventSummary != value) { _eventSummary = value; Notify(); MarkDirty(); } } }
    public string EventGmSummary { get => _eventGmSummary; set { if (_eventGmSummary != value) { _eventGmSummary = value; Notify(); MarkDirty(); } } }
    public string EventType { get => _eventType; set { if (_eventType != value) { _eventType = value; Notify(); } } }
    public string EventStatus { get => _eventStatus; set { if (_eventStatus != value) { _eventStatus = value; Notify(); } } }
    public string EventRevealPolicy { get => _eventRevealPolicy; set { if (_eventRevealPolicy != value) { _eventRevealPolicy = value; Notify(); } } }
    public bool EventPlayerVisible { get => _eventPlayerVisible; set { if (_eventPlayerVisible != value) { _eventPlayerVisible = value; Notify(); } } }
    public string VersionSummary { get => _versionSummary; set { if (_versionSummary != value) { _versionSummary = value; Notify(); } } }
    public string VersionType { get => _versionType; set { if (_versionType != value) { _versionType = value; Notify(); } } }
    public bool VersionPlayerVisible { get => _versionPlayerVisible; set { if (_versionPlayerVisible != value) { _versionPlayerVisible = value; Notify(); } } }
    public string HolidayName { get => _holidayName; set { if (_holidayName != value) { _holidayName = value; Notify(); } } }
    public string HolidayDescription { get => _holidayDescription; set { if (_holidayDescription != value) { _holidayDescription = value; Notify(); } } }
    public int HolidayMonth { get => _holidayMonth; set { if (_holidayMonth != value) { _holidayMonth = value; Notify(); } } }
    public int HolidayDay { get => _holidayDay; set { if (_holidayDay != value) { _holidayDay = value; Notify(); } } }
    public bool HolidayPlayerVisible { get => _holidayPlayerVisible; set { if (_holidayPlayerVisible != value) { _holidayPlayerVisible = value; Notify(); } } }
    public string ReminderTitle { get => _reminderTitle; set { if (_reminderTitle != value) { _reminderTitle = value; Notify(); } } }
    public string ReminderNotes { get => _reminderNotes; set { if (_reminderNotes != value) { _reminderNotes = value; Notify(); } } }
    public string RealSchedulePlaceholder { get => _realSchedulePlaceholder; private set { if (_realSchedulePlaceholder != value) { _realSchedulePlaceholder = value; Notify(); } } }

    public void RefreshFlags()
    {
        try
        {
            var response = _api.SystemFeatureFlagsSnapshot();
            if (!Ok(response))
            {
                ErrorMessage = Friendly(response, "Не удалось загрузить флаги функций календаря.");
                return;
            }
            var flags = Dicts(Get(response.Payload, "flags")).ToList();
            if (flags.Count == 0 && Get(response.Payload, "snapshot") is Dictionary<string, object> snapshot)
                flags = Dicts(Get(snapshot, "flags")).ToList();
            CalendarEnabled = Flag(flags, nameof(WorldCalendarFeatureFlags.UseWorldCalendarMvp));
            CurrentDateEnabled = Flag(flags, nameof(WorldCalendarFeatureFlags.UseWorldCalendarCurrentDate));
            EventsEnabled = Flag(flags, nameof(WorldCalendarFeatureFlags.UseWorldCalendarEvents));
            ChronicleEnabled = Flag(flags, nameof(WorldCalendarFeatureFlags.UseWorldCalendarChronicle));
            HolidaysEnabled = Flag(flags, nameof(WorldCalendarFeatureFlags.UseWorldCalendarHolidays));
            RemindersEnabled = Flag(flags, nameof(WorldCalendarFeatureFlags.UseWorldCalendarReminders));
            PlayerViewEnabled = Flag(flags, nameof(WorldCalendarFeatureFlags.UseWorldCalendarPlayerView));
            FutureVisibilityEnabled = Flag(flags, nameof(WorldCalendarFeatureFlags.UseWorldCalendarFutureVisibility));
            StatusMessage = CalendarEnabled ? "World Calendar flags доступны. Можно создать календарь кампании." : DisabledText;
            if (CalendarEnabled) LoadCalendar();
        }
        catch (Exception ex)
        {
            ErrorMessage = "Не удалось проверить World Calendar flags.";
            ClientLogService.Instance.Error("admin.world_calendar.flags.error", ex);
        }
    }

    private void EnsureCalendar() => SendAndRefresh(() => _api.WorldCalendarDefaultEnsure(BasePayload()), "admin.world_calendar.ensure");
    private void LoadCalendar() => SendAndApply(() => _api.WorldCalendarDefinitionGet(BasePayload()), "admin.world_calendar.load");
    private void SetDate()
        => SendAndRefresh(() => _api.WorldCalendarCurrentSet(DatePayload(new() { { "reason", AdvanceReason } })), "admin.world_calendar.current.set");
    private void AdvanceTime()
        => SendAndRefresh(() => _api.WorldCalendarCurrentAdvance(BasePayload(new() { { "days", AdvanceDays }, { "reason", AdvanceReason } })), "admin.world_calendar.current.advance");

    private void CreateEvent()
        => SendAndRefresh(() => _api.WorldCalendarEventCreate(DatePayload(new()
        {
            { "title", EventTitle },
            { "description", EventSummary },
            { "publicSummary", EventSummary },
            { "gmSummary", EventGmSummary },
            { "eventType", EventType },
            { "status", EventStatus },
            { "revealPolicy", EventRevealPolicy },
            { "durationMinutes", EventDurationMinutes },
            { "isPlayerVisible", EventPlayerVisible },
            { "visibilityMode", EventPlayerVisible ? WorldCalendarVisibilityModeIds.PlayerVisible : WorldCalendarVisibilityModeIds.GmOnly }
        })), "admin.world_calendar.event.create");

    private void UpdateEvent()
    {
        if (SelectedEvent == null) return;
        SendAndRefresh(() => _api.WorldCalendarEventUpdate(DatePayload(new()
        {
            { "eventId", SelectedEvent.EventId },
            { "title", EventTitle },
            { "description", EventSummary },
            { "publicSummary", EventSummary },
            { "gmSummary", EventGmSummary },
            { "eventType", EventType },
            { "status", EventStatus },
            { "revealPolicy", EventRevealPolicy },
            { "durationMinutes", EventDurationMinutes },
            { "isPlayerVisible", EventPlayerVisible },
            { "visibilityMode", EventPlayerVisible ? WorldCalendarVisibilityModeIds.PlayerVisible : WorldCalendarVisibilityModeIds.GmOnly }
        })), "admin.world_calendar.event.update");
    }

    private void CancelEvent()
    {
        if (SelectedEvent == null) return;
        SendAndRefresh(() => _api.WorldCalendarEventCancel(BasePayload(new() { { "eventId", SelectedEvent.EventId } })), "admin.world_calendar.event.cancel");
    }

    private void ArchiveEvent()
    {
        if (SelectedEvent == null) return;
        SendAndRefresh(() => _api.WorldCalendarEventArchive(BasePayload(new() { { "eventId", SelectedEvent.EventId } })), "admin.world_calendar.event.archive");
    }

    private void AddVersion()
    {
        if (SelectedEvent == null) return;
        SendAndRefresh(() => _api.WorldCalendarEventVersionAdd(BasePayload(new()
        {
            { "eventId", SelectedEvent.EventId },
            { "title", EventTitle },
            { "summary", VersionSummary },
            { "body", VersionSummary },
            { "versionType", VersionType },
            { "isPlayerVisible", VersionPlayerVisible },
            { "visibilityMode", VersionPlayerVisible ? WorldCalendarVisibilityModeIds.PlayerVisible : WorldCalendarVisibilityModeIds.GmOnly }
        })), "admin.world_calendar.version.add");
    }

    private void CreateHoliday()
        => SendAndRefresh(() => _api.WorldCalendarHolidayCreate(BasePayload(new()
        {
            { "name", HolidayName },
            { "description", HolidayDescription },
            { "monthOrder", HolidayMonth },
            { "dayOfMonth", HolidayDay },
            { "isPlayerVisible", HolidayPlayerVisible }
        })), "admin.world_calendar.holiday.create");

    private void ArchiveHoliday()
    {
        if (SelectedHoliday == null) return;
        SendAndRefresh(() => _api.WorldCalendarHolidayArchive(BasePayload(new() { { "holidayId", SelectedHoliday.HolidayId } })), "admin.world_calendar.holiday.archive");
    }

    private void CreateReminder()
        => SendAndRefresh(() => _api.WorldCalendarReminderCreate(DatePayload(new()
        {
            { "eventId", SelectedEvent?.EventId ?? string.Empty },
            { "title", ReminderTitle },
            { "notes", ReminderNotes }
        })), "admin.world_calendar.reminder.create");

    private void DismissReminder()
    {
        if (SelectedReminder == null) return;
        SendAndRefresh(() => _api.WorldCalendarReminderDismiss(BasePayload(new() { { "reminderId", SelectedReminder.ReminderId } })), "admin.world_calendar.reminder.dismiss");
    }

    private void SendAndApply(Func<ResponseEnvelope> sender, string logKey)
    {
        if (IsLoading) return;
        IsLoading = true;
        ErrorMessage = string.Empty;
        try
        {
            ClientLogService.Instance.Info($"{logKey}.start");
            var response = sender();
            if (!Ok(response))
            {
                ErrorMessage = Friendly(response, "Команда календаря не выполнена.");
                return;
            }
            ApplyCalendarPayload(response.Payload);
            HasUnsavedChanges = false;
            StatusMessage = Friendly(response, "Календарь обновлён.");
            ClientLogService.Instance.Info($"{logKey}.done");
        }
        catch (Exception ex)
        {
            ErrorMessage = "Не удалось выполнить команду календаря. Проверьте подключение к серверу.";
            ClientLogService.Instance.Error($"{logKey}.error", ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void MarkDirty()
    {
        if (!_isLoadingEventDraft) HasUnsavedChanges = true;
    }

    private void RaiseEventCommandStates()
    {
        ((RelayCommand)CreateEventCommand).RaiseCanExecuteChanged();
        ((RelayCommand)UpdateEventCommand).RaiseCanExecuteChanged();
    }

    private void SendAndRefresh(Func<ResponseEnvelope> sender, string logKey)
    {
        SendAndApply(sender, logKey);
        if (CalendarEnabled) LoadCalendar();
    }

    private void ApplyCalendarPayload(Dictionary<string, object> payload)
    {
        var calendar = Map(Get(payload, "calendar"));
        var current = Map(Get(payload, "current"));
        _calendarId = S(Get(calendar, "calendarId"));
        CalendarName = First(S(Get(calendar, "name")), "Календарь мира");
        CurrentDisplay = First(S(Get(current, "display")), "Дата не загружена");
        CurrentSeason = S(Get(current, "season"));
        CurrentWeekDay = S(Get(current, "weekDay"));
        var date = Map(Get(current, "dateTime"));
        if (date.Count > 0)
        {
            ApplySignedYear(I(Get(date, "year"), SignedYearForPayload()));
            MonthOrder = I(Get(date, "monthOrder"), MonthOrder);
            DayOfMonth = I(Get(date, "dayOfMonth"), DayOfMonth);
            Hour = I(Get(date, "hour"), Hour);
            Minute = I(Get(date, "minute"), Minute);
        }
        RealSchedulePlaceholder = First(S(Get(payload, "realSchedulePlaceholder")), RealSchedulePlaceholder);

        Months.Clear();
        foreach (var month in Dicts(Get(payload, "months"))) Months.Add(CalendarMonthRow.From(month));
        Events.Clear();
        foreach (var item in Dicts(Get(payload, "events"))) Events.Add(CalendarEventRow.From(item));
        Notify(nameof(FilteredEvents));
        Notify(nameof(RouteState));
        Holidays.Clear();
        foreach (var item in Dicts(Get(payload, "holidays"))) Holidays.Add(HolidayRow.From(item));
        Reminders.Clear();
        foreach (var item in Dicts(Get(payload, "reminders"))) Reminders.Add(ReminderRow.From(item));
    }

    private Dictionary<string, object> BasePayload(Dictionary<string, object>? extra = null)
    {
        var payload = extra ?? new Dictionary<string, object>();
        payload["campaignId"] = CampaignId;
        payload["ruleSetId"] = RuleSetId;
        if (!string.IsNullOrWhiteSpace(_calendarId)) payload["calendarId"] = _calendarId;
        return payload;
    }

    private Dictionary<string, object> DatePayload(Dictionary<string, object>? extra = null)
    {
        var payload = BasePayload(extra);
        payload["year"] = Year;
        payload["era"] = EraIdForPayload();
        payload["monthOrder"] = MonthOrder;
        payload["dayOfMonth"] = DayOfMonth;
        payload["hour"] = Hour;
        payload["minute"] = Minute;
        return payload;
    }

    private int SignedYearForPayload()
        => WorldCalendarMath.ToSignedYear(Year, EraIdForPayload());

    private string EraIdForPayload()
        => string.Equals(SelectedEra, WorldCalendarEraLabels.BeforeCommonEra, StringComparison.OrdinalIgnoreCase)
            ? WorldCalendarEraIds.BeforeCommonEra
            : WorldCalendarEraIds.CommonEra;

    private void ApplySignedYear(int signedYear)
    {
        Year = signedYear >= 1 ? signedYear : 1 - signedYear;
        SelectedEra = signedYear >= 1 ? WorldCalendarEraLabels.CommonEra : WorldCalendarEraLabels.BeforeCommonEra;
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
    private static int I(object? value, int fallback = 0) => int.TryParse(S(value), out var parsed) ? parsed : fallback;
    private static bool B(object? value) => value is bool b ? b : bool.TryParse(S(value), out var parsed) && parsed;
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
        public string EventId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string EventType { get; set; } = string.Empty;
        public string EventTypeDisplay { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string StatusDisplay { get; set; } = string.Empty;
        public string StartDisplay { get; set; } = string.Empty;
        public string PublicSummary { get; set; } = string.Empty;
        public string GmSummary { get; set; } = string.Empty;
        public string RevealPolicy { get; set; } = string.Empty;
        public bool IsPlayerVisible { get; set; }
        public int Year { get; set; }
        public int MonthOrder { get; set; }
        public int DayOfMonth { get; set; }
        public string Display => $"{StartDisplay}: {Title} ({StatusDisplay})";
        public static CalendarEventRow From(Dictionary<string, object> map)
        {
            var start = Map(Get(map, "start"));
            return new CalendarEventRow
            {
                EventId = S(Get(map, "eventId")),
                Title = S(Get(map, "title")),
                EventType = S(Get(map, "eventType")),
                EventTypeDisplay = S(Get(map, "eventTypeDisplay")),
                Status = S(Get(map, "status")),
                StatusDisplay = S(Get(map, "statusDisplay")),
                StartDisplay = S(Get(map, "startDisplay")),
                PublicSummary = S(Get(map, "publicSummary")),
                GmSummary = S(Get(map, "gmSummary")),
                RevealPolicy = S(Get(map, "revealPolicy")),
                IsPlayerVisible = B(Get(map, "isPlayerVisible")),
                Year = I(Get(start, "year")),
                MonthOrder = I(Get(start, "monthOrder"), 1),
                DayOfMonth = I(Get(start, "dayOfMonth"), 1)
            };
        }
    }

    public sealed class HolidayRow
    {
        public string HolidayId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string DateText { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsPlayerVisible { get; set; }
        public string Display => $"{DateText}: {Name}";
        public static HolidayRow From(Dictionary<string, object> map) => new() { HolidayId = S(Get(map, "holidayId")), Name = S(Get(map, "name")), DateText = $"{I(Get(map, "dayOfMonth"), 1):00}.{I(Get(map, "monthOrder"), 1):00}", Description = S(Get(map, "description")), IsPlayerVisible = B(Get(map, "isPlayerVisible")) };
    }

    public sealed class ReminderRow
    {
        public string ReminderId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string DateDisplay { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public string Display => $"{DateDisplay}: {Title}";
        public static ReminderRow From(Dictionary<string, object> map) => new() { ReminderId = S(Get(map, "reminderId")), Title = S(Get(map, "title")), DateDisplay = S(Get(map, "dateDisplay")), Notes = S(Get(map, "notes")) };
    }
}
