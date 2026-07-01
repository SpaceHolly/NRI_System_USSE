using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows.Input;
using Nri.AdminClient.Diagnostics;
using Nri.AdminClient.Networking;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;

namespace Nri.AdminClient.ViewModels;

public sealed class AdminEventJournalViewModel : ViewModelBase
{
    private readonly CommandApi _api;
    private string _campaignId = "default";
    private string _statusMessage = "Журнал событий пока недоступен. Все Event Journal flags выключены по умолчанию.";
    private string _errorMessage = string.Empty;
    private bool _isEnabled;
    private bool _manualEnabled;
    private bool _filtersEnabled;
    private bool _correctionsEnabled;
    private bool _includeArchived;
    private string _sessionId = string.Empty;
    private string _groupId = string.Empty;
    private string _category = string.Empty;
    private string _sourceModule = string.Empty;
    private string _searchText = string.Empty;
    private EventJournalRow? _selectedEntry;
    private EventJournalLinkRow? _selectedLink;
    private string _lastEditedEntryId = string.Empty;
    private string _title = "Новая запись журнала";
    private string _summary = string.Empty;
    private string _playerSummary = string.Empty;
    private string _gmDetails = string.Empty;
    private string _entryCategory = EventJournalCategoryIds.Session;
    private string _severity = EventJournalSeverityIds.Information;
    private string _visibilityMode = EventJournalVisibilityModeIds.GMOnly;
    private string _tagsText = string.Empty;
    private string _subjectEntityType = string.Empty;
    private string _subjectEntityId = string.Empty;
    private string _subjectDisplayName = string.Empty;
    private string _correctionSummary = string.Empty;
    private string _annotationText = string.Empty;
    private bool _annotationPlayerVisible;
    private string _linkEntityType = EventJournalEntityTypeIds.CurrentSession;
    private string _linkEntityId = string.Empty;
    private string _linkDisplayName = string.Empty;
    private string _linkRole = EventJournalLinkRoleIds.Related;
    private bool _linkPlayerVisible;

    public AdminEventJournalViewModel(CommandApi api)
    {
        _api = api;
        RefreshFlagsCommand = new RelayCommand(RefreshFlags);
        RefreshCommand = new RelayCommand(Load);
        SearchCommand = new RelayCommand(Search);
        CreateManualCommand = new RelayCommand(CreateManual);
        SaveManualCommand = new RelayCommand(SaveManual);
        CreateCorrectionCommand = new RelayCommand(CreateCorrection);
        AddAnnotationCommand = new RelayCommand(AddAnnotation);
        SetVisibilityCommand = new RelayCommand(SetVisibility);
        ArchiveCommand = new RelayCommand(Archive);
        RestoreCommand = new RelayCommand(Restore);
        AddLinkCommand = new RelayCommand(AddLink);
        RemoveLinkCommand = new RelayCommand(RemoveLink);
        ClearErrorCommand = new RelayCommand(() => ErrorMessage = string.Empty);
    }

    public ObservableCollection<EventJournalRow> Entries { get; } = new();
    public ObservableCollection<EventJournalLinkRow> Links { get; } = new();
    public ObservableCollection<EventJournalAnnotationRow> Annotations { get; } = new();
    public ObservableCollection<string> CategoryOptions { get; } = new()
    {
        string.Empty,
        EventJournalCategoryIds.Session,
        EventJournalCategoryIds.Character,
        EventJournalCategoryIds.Ownership,
        EventJournalCategoryIds.Group,
        EventJournalCategoryIds.Request,
        EventJournalCategoryIds.Combat,
        EventJournalCategoryIds.Map,
        EventJournalCategoryIds.WorldCalendar,
        EventJournalCategoryIds.RealSchedule,
        EventJournalCategoryIds.GMNote,
        EventJournalCategoryIds.Inventory,
        EventJournalCategoryIds.System,
        EventJournalCategoryIds.Custom
    };
    public ObservableCollection<string> EntryCategoryOptions { get; } = new()
    {
        EventJournalCategoryIds.Session,
        EventJournalCategoryIds.Character,
        EventJournalCategoryIds.Ownership,
        EventJournalCategoryIds.Group,
        EventJournalCategoryIds.Request,
        EventJournalCategoryIds.Combat,
        EventJournalCategoryIds.Map,
        EventJournalCategoryIds.WorldCalendar,
        EventJournalCategoryIds.RealSchedule,
        EventJournalCategoryIds.GMNote,
        EventJournalCategoryIds.Inventory,
        EventJournalCategoryIds.System,
        EventJournalCategoryIds.Custom
    };
    public ObservableCollection<string> SeverityOptions { get; } = new()
    {
        EventJournalSeverityIds.Information,
        EventJournalSeverityIds.Notice,
        EventJournalSeverityIds.Important,
        EventJournalSeverityIds.Warning,
        EventJournalSeverityIds.Critical
    };
    public ObservableCollection<string> VisibilityOptions { get; } = new()
    {
        EventJournalVisibilityModeIds.GMOnly,
        EventJournalVisibilityModeIds.GMTeam,
        EventJournalVisibilityModeIds.PlayerVisible,
        EventJournalVisibilityModeIds.SuperAdminOnly
    };
    public ObservableCollection<string> EntityTypeOptions { get; } = new()
    {
        EventJournalEntityTypeIds.CurrentSession,
        EventJournalEntityTypeIds.Session,
        EventJournalEntityTypeIds.Character,
        EventJournalEntityTypeIds.Npc,
        EventJournalEntityTypeIds.Companion,
        EventJournalEntityTypeIds.CharacterGroup,
        EventJournalEntityTypeIds.PlayerRequest,
        EventJournalEntityTypeIds.WorldCalendarEvent,
        EventJournalEntityTypeIds.RealScheduleEvent,
        EventJournalEntityTypeIds.SceneMap,
        EventJournalEntityTypeIds.WorldMap,
        EventJournalEntityTypeIds.Room,
        EventJournalEntityTypeIds.MapMarker,
        EventJournalEntityTypeIds.CombatEncounter,
        EventJournalEntityTypeIds.Location,
        EventJournalEntityTypeIds.Country,
        EventJournalEntityTypeIds.Region,
        EventJournalEntityTypeIds.Faction,
        EventJournalEntityTypeIds.Organization,
        EventJournalEntityTypeIds.GMNote,
        EventJournalEntityTypeIds.Custom
    };
    public ObservableCollection<string> LinkRoleOptions { get; } = new()
    {
        EventJournalLinkRoleIds.Actor,
        EventJournalLinkRoleIds.Subject,
        EventJournalLinkRoleIds.Source,
        EventJournalLinkRoleIds.Target,
        EventJournalLinkRoleIds.Related,
        EventJournalLinkRoleIds.Location,
        EventJournalLinkRoleIds.Result,
        EventJournalLinkRoleIds.CorrectionOf,
        EventJournalLinkRoleIds.Custom
    };

    public ICommand RefreshFlagsCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand SearchCommand { get; }
    public ICommand CreateManualCommand { get; }
    public ICommand SaveManualCommand { get; }
    public ICommand CreateCorrectionCommand { get; }
    public ICommand AddAnnotationCommand { get; }
    public ICommand SetVisibilityCommand { get; }
    public ICommand ArchiveCommand { get; }
    public ICommand RestoreCommand { get; }
    public ICommand AddLinkCommand { get; }
    public ICommand RemoveLinkCommand { get; }
    public ICommand ClearErrorCommand { get; }

    public string CampaignId { get => _campaignId; set { if (_campaignId != value) { _campaignId = value ?? string.Empty; Notify(); } } }
    public string StatusMessage { get => _statusMessage; set { if (_statusMessage != value) { _statusMessage = value ?? string.Empty; Notify(); } } }
    public string ErrorMessage { get => _errorMessage; set { if (_errorMessage != value) { _errorMessage = value ?? string.Empty; Notify(); Notify(nameof(HasError)); } } }
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool IsEnabled { get => _isEnabled; set { if (_isEnabled != value) { _isEnabled = value; Notify(); Notify(nameof(IsDisabled)); } } }
    public bool IsDisabled => !IsEnabled;
    public bool ManualEnabled { get => _manualEnabled; set { if (_manualEnabled != value) { _manualEnabled = value; Notify(); } } }
    public bool FiltersEnabled { get => _filtersEnabled; set { if (_filtersEnabled != value) { _filtersEnabled = value; Notify(); } } }
    public bool CorrectionsEnabled { get => _correctionsEnabled; set { if (_correctionsEnabled != value) { _correctionsEnabled = value; Notify(); } } }
    public bool IncludeArchived { get => _includeArchived; set { if (_includeArchived != value) { _includeArchived = value; Notify(); } } }
    public string SessionId { get => _sessionId; set { if (_sessionId != value) { _sessionId = value ?? string.Empty; Notify(); } } }
    public string GroupId { get => _groupId; set { if (_groupId != value) { _groupId = value ?? string.Empty; Notify(); } } }
    public string Category { get => _category; set { if (_category != value) { _category = value ?? string.Empty; Notify(); } } }
    public string SourceModule { get => _sourceModule; set { if (_sourceModule != value) { _sourceModule = value ?? string.Empty; Notify(); } } }
    public string SearchText { get => _searchText; set { if (_searchText != value) { _searchText = value ?? string.Empty; Notify(); } } }
    public string Title { get => _title; set { if (_title != value) { _title = value ?? string.Empty; Notify(); } } }
    public string Summary { get => _summary; set { if (_summary != value) { _summary = value ?? string.Empty; Notify(); } } }
    public string PlayerSummary { get => _playerSummary; set { if (_playerSummary != value) { _playerSummary = value ?? string.Empty; Notify(); } } }
    public string GMDetails { get => _gmDetails; set { if (_gmDetails != value) { _gmDetails = value ?? string.Empty; Notify(); } } }
    public string EntryCategory { get => _entryCategory; set { if (_entryCategory != value) { _entryCategory = value ?? EventJournalCategoryIds.Custom; Notify(); } } }
    public string Severity { get => _severity; set { if (_severity != value) { _severity = value ?? EventJournalSeverityIds.Information; Notify(); } } }
    public string VisibilityMode { get => _visibilityMode; set { if (_visibilityMode != value) { _visibilityMode = value ?? EventJournalVisibilityModeIds.GMOnly; Notify(); } } }
    public string TagsText { get => _tagsText; set { if (_tagsText != value) { _tagsText = value ?? string.Empty; Notify(); } } }
    public string SubjectEntityType { get => _subjectEntityType; set { if (_subjectEntityType != value) { _subjectEntityType = value ?? string.Empty; Notify(); } } }
    public string SubjectEntityId { get => _subjectEntityId; set { if (_subjectEntityId != value) { _subjectEntityId = value ?? string.Empty; Notify(); } } }
    public string SubjectDisplayName { get => _subjectDisplayName; set { if (_subjectDisplayName != value) { _subjectDisplayName = value ?? string.Empty; Notify(); } } }
    public string CorrectionSummary { get => _correctionSummary; set { if (_correctionSummary != value) { _correctionSummary = value ?? string.Empty; Notify(); } } }
    public string AnnotationText { get => _annotationText; set { if (_annotationText != value) { _annotationText = value ?? string.Empty; Notify(); } } }
    public bool AnnotationPlayerVisible { get => _annotationPlayerVisible; set { if (_annotationPlayerVisible != value) { _annotationPlayerVisible = value; Notify(); } } }
    public string LinkEntityType { get => _linkEntityType; set { if (_linkEntityType != value) { _linkEntityType = value ?? EventJournalEntityTypeIds.Custom; Notify(); } } }
    public string LinkEntityId { get => _linkEntityId; set { if (_linkEntityId != value) { _linkEntityId = value ?? string.Empty; Notify(); } } }
    public string LinkDisplayName { get => _linkDisplayName; set { if (_linkDisplayName != value) { _linkDisplayName = value ?? string.Empty; Notify(); } } }
    public string LinkRole { get => _linkRole; set { if (_linkRole != value) { _linkRole = value ?? EventJournalLinkRoleIds.Related; Notify(); } } }
    public bool LinkPlayerVisible { get => _linkPlayerVisible; set { if (_linkPlayerVisible != value) { _linkPlayerVisible = value; Notify(); } } }

    public EventJournalRow? SelectedEntry
    {
        get => _selectedEntry;
        set
        {
            if (_selectedEntry == value) return;
            _selectedEntry = value;
            if (value != null && !string.IsNullOrWhiteSpace(value.EntryId))
                _lastEditedEntryId = value.EntryId;
            Notify();
            BindSelectedEntry(value);
            if (value != null) LoadDetails(value.EntryId);
        }
    }

    public EventJournalLinkRow? SelectedLink { get => _selectedLink; set { if (_selectedLink != value) { _selectedLink = value; Notify(); } } }

    public void RefreshFlags()
    {
        Safe("admin.journal.flags", () =>
        {
            var response = _api.SystemFeatureFlagsSnapshot();
            var flags = ReadList(ReadMap(response.Payload, "snapshot") ?? response.Payload, "flags");
            IsEnabled = FindFlag(flags, "EventJournal.UseEventJournalMvp");
            ManualEnabled = FindFlag(flags, "EventJournal.UseEventJournalManualEntries");
            FiltersEnabled = FindFlag(flags, "EventJournal.UseEventJournalFilters");
            CorrectionsEnabled = FindFlag(flags, "EventJournal.UseEventJournalCorrections");
            StatusMessage = IsEnabled ? "Журнал событий включён." : "Журнал событий выключен флагами функций.";
            if (IsEnabled) Load();
        });
    }

    public void Load()
    {
        if (!IsEnabled)
        {
            StatusMessage = "Журнал событий пока недоступен.";
            return;
        }

        Safe("admin.journal.load", () =>
        {
            var response = _api.JournalEventList(BasePayload());
            RequireOk(response);
            ReplaceEntries(ReadList(response.Payload, "items"));
            StatusMessage = $"Записей журнала: {Entries.Count}.";
        });
    }

    private void Search()
    {
        Safe("admin.journal.search", () =>
        {
            var payload = BasePayload();
            payload["query"] = SearchText;
            var response = _api.JournalEventSearch(payload);
            RequireOk(response);
            ReplaceEntries(ReadList(response.Payload, "items"));
            SelectedEntry = Entries.FirstOrDefault();
            StatusMessage = $"Найдено записей: {Entries.Count}.";
        });
    }

    private void CreateManual()
    {
        if (!ManualEnabled)
            RefreshFlags();

        Safe("admin.journal.manual.create", () =>
        {
            var response = _api.JournalEventManualCreate(EditPayload());
            RequireOk(response);
            var item = ReadMap(response.Payload, "item");
            var created = item != null ? EventJournalRow.FromMap(item) : null;
            if (created != null)
            {
                _lastEditedEntryId = created.EntryId;
                SelectedEntry = created;
            }
            Load();
            if (created != null)
                SelectedEntry = Entries.FirstOrDefault(x => x.EntryId == created.EntryId) ?? created;
            StatusMessage = "Ручная запись создана.";
        });
    }

    private void SaveManual()
    {
        if (SelectedEntry == null && string.IsNullOrWhiteSpace(_lastEditedEntryId))
        {
            StatusMessage = "Выберите ручную запись.";
            return;
        }
        Safe("admin.journal.manual.update", () =>
        {
            var entryId = !string.IsNullOrWhiteSpace(SelectedEntry?.EntryId) ? SelectedEntry.EntryId : _lastEditedEntryId;
            var payload = EditPayload();
            payload["entryId"] = entryId;
            var response = _api.JournalEventManualUpdate(payload);
            RequireOk(response);
            Load();
            _lastEditedEntryId = entryId;
            SelectedEntry = Entries.FirstOrDefault(x => x.EntryId == entryId) ?? SelectedEntry;
            StatusMessage = "Ручная запись обновлена.";
        });
    }

    private void CreateCorrection()
    {
        if (SelectedEntry == null)
        {
            StatusMessage = "Выберите исходную запись.";
            return;
        }
        if (!CorrectionsEnabled)
        {
            StatusMessage = "Коррекции журнала выключены флагами функций.";
            return;
        }

        Safe("admin.journal.correction.create", () =>
        {
            var payload = EditPayload();
            payload["correctsEntryId"] = SelectedEntry.EntryId;
            payload["title"] = string.IsNullOrWhiteSpace(Title) ? $"Коррекция: {SelectedEntry.Title}" : Title;
            payload["summary"] = string.IsNullOrWhiteSpace(CorrectionSummary) ? Summary : CorrectionSummary;
            var response = _api.JournalEventCorrectionCreate(payload);
            RequireOk(response);
            StatusMessage = "Коррекция создана отдельной записью.";
            Load();
        });
    }

    private void AddAnnotation()
    {
        if (SelectedEntry == null)
        {
            StatusMessage = "Выберите запись.";
            return;
        }

        Safe("admin.journal.annotation.add", () =>
        {
            var response = _api.JournalEventAnnotationAdd(new Dictionary<string, object>
            {
                { "entryId", SelectedEntry.EntryId },
                { "text", AnnotationText },
                { "isPlayerVisible", AnnotationPlayerVisible }
            });
            RequireOk(response);
            AnnotationText = string.Empty;
            LoadDetails(SelectedEntry.EntryId);
            StatusMessage = "Аннотация добавлена.";
        });
    }

    private void SetVisibility()
    {
        if (SelectedEntry == null) return;
        Safe("admin.journal.visibility.changed", () =>
        {
            var response = _api.JournalEventVisibilitySet(new Dictionary<string, object> { { "entryId", SelectedEntry.EntryId }, { "visibilityMode", VisibilityMode } });
            RequireOk(response);
            Load();
        });
    }

    private void Archive() => SetArchive(true);
    private void Restore() => SetArchive(false);

    private void SetArchive(bool archived)
    {
        if (SelectedEntry == null) return;
        Safe(archived ? "admin.journal.archive" : "admin.journal.restore", () =>
        {
            var payload = new Dictionary<string, object> { { "entryId", SelectedEntry.EntryId } };
            var response = archived ? _api.JournalEventArchive(payload) : _api.JournalEventRestore(payload);
            RequireOk(response);
            Load();
        });
    }

    private void AddLink()
    {
        if (SelectedEntry == null)
        {
            StatusMessage = "Выберите запись.";
            return;
        }

        Safe("admin.journal.link.add", () =>
        {
            var response = _api.JournalEventLinkAdd(new Dictionary<string, object>
            {
                { "entryId", SelectedEntry.EntryId },
                { "entityType", LinkEntityType },
                { "entityId", LinkEntityId },
                { "displayName", LinkDisplayName },
                { "linkRole", LinkRole },
                { "isPlayerVisible", LinkPlayerVisible }
            });
            RequireOk(response);
            LinkEntityId = string.Empty;
            LinkDisplayName = string.Empty;
            LoadDetails(SelectedEntry.EntryId);
        });
    }

    private void RemoveLink()
    {
        if (SelectedLink == null) return;
        Safe("admin.journal.link.remove", () =>
        {
            var response = _api.JournalEventLinkRemove(new Dictionary<string, object> { { "linkId", SelectedLink.LinkId } });
            RequireOk(response);
            if (SelectedEntry != null) LoadDetails(SelectedEntry.EntryId);
        });
    }

    private void LoadDetails(string entryId)
    {
        Safe("admin.journal.entry.get", () =>
        {
            var response = _api.JournalEventGet(new Dictionary<string, object> { { "entryId", entryId } });
            RequireOk(response);
            Links.Clear();
            foreach (var map in ReadList(response.Payload, "links").Select(ToMap).Where(x => x != null))
                Links.Add(EventJournalLinkRow.FromMap(map!));
            Annotations.Clear();
            foreach (var map in ReadList(response.Payload, "annotations").Select(ToMap).Where(x => x != null))
                Annotations.Add(EventJournalAnnotationRow.FromMap(map!));
            var item = ReadMap(response.Payload, "item");
            if (item != null)
            {
                GMDetails = Str(item, "gmDetails");
                PlayerSummary = Str(item, "playerSummary");
            }
        });
    }

    private Dictionary<string, object> BasePayload()
    {
        return new Dictionary<string, object>
        {
            { "campaignId", CampaignId },
            { "includeArchived", IncludeArchived },
            { "sessionId", SessionId },
            { "groupId", GroupId },
            { "category", Category },
            { "sourceModule", SourceModule }
        };
    }

    private Dictionary<string, object> EditPayload()
    {
        return new Dictionary<string, object>
        {
            { "campaignId", CampaignId },
            { "sessionId", SessionId },
            { "groupId", GroupId },
            { "title", Title },
            { "summary", Summary },
            { "playerSummary", PlayerSummary },
            { "gmDetails", GMDetails },
            { "category", EntryCategory },
            { "severity", Severity },
            { "visibilityMode", VisibilityMode },
            { "tagsText", TagsText },
            { "subjectEntityType", SubjectEntityType },
            { "subjectEntityId", SubjectEntityId },
            { "subjectDisplayName", SubjectDisplayName }
        };
    }

    private void BindSelectedEntry(EventJournalRow? row)
    {
        Links.Clear();
        Annotations.Clear();
        if (row == null) return;
        Title = row.Title;
        Summary = row.Summary;
        EntryCategory = row.Category;
        Severity = row.Severity;
        VisibilityMode = row.VisibilityMode;
        TagsText = row.TagsText;
        SubjectEntityType = row.SubjectEntityType;
        SubjectEntityId = row.SubjectEntityId;
        SubjectDisplayName = row.SubjectDisplayName;
    }

    private void ReplaceEntries(IList<object> items)
    {
        Entries.Clear();
        foreach (var map in items.Select(ToMap).Where(x => x != null))
            Entries.Add(EventJournalRow.FromMap(map!));
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
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(response.Message) ? response.Status.ToString() : response.Message);
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
            if (string.Equals(itemKey, key, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(itemKey, shortKey, StringComparison.OrdinalIgnoreCase) ||
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

public sealed class EventJournalRow
{
    public string EntryId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string EntryType { get; set; } = string.Empty;
    public string SourceModule { get; set; } = string.Empty;
    public string VisibilityMode { get; set; } = string.Empty;
    public bool IsAutomatic { get; set; }
    public bool IsArchived { get; set; }
    public bool IsPlayerVisible { get; set; }
    public string ActorDisplayName { get; set; } = string.Empty;
    public string SubjectEntityType { get; set; } = string.Empty;
    public string SubjectEntityId { get; set; } = string.Empty;
    public string SubjectDisplayName { get; set; } = string.Empty;
    public string TagsText { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; }
    public string OccurredText => OccurredAtUtc == default ? "—" : OccurredAtUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
    public string KindText => IsAutomatic ? "Автоматическая" : EntryType;
    public string VisibilityText => IsPlayerVisible ? "Видно игрокам" : VisibilityMode;

    public static EventJournalRow FromMap(IDictionary<string, object> map)
    {
        return new EventJournalRow
        {
            EntryId = Str(map, "entryId"),
            Title = Str(map, "title"),
            Summary = Str(map, "summary"),
            Category = Str(map, "category"),
            Severity = Str(map, "severity"),
            EntryType = Str(map, "entryType"),
            SourceModule = Str(map, "sourceModule"),
            VisibilityMode = Str(map, "visibilityMode"),
            IsAutomatic = Bool(map, "isAutomatic"),
            IsArchived = Bool(map, "isArchived"),
            IsPlayerVisible = Bool(map, "isPlayerVisible"),
            ActorDisplayName = Str(map, "actorDisplayName"),
            SubjectEntityType = Str(map, "subjectEntityType"),
            SubjectEntityId = Str(map, "subjectEntityId"),
            SubjectDisplayName = Str(map, "subjectDisplayName"),
            TagsText = string.Join(", ", List(map, "tags").Select(Convert.ToString).Where(x => !string.IsNullOrWhiteSpace(x))),
            OccurredAtUtc = Date(map, "occurredAtUtc")
        };
    }

    private static string Str(IDictionary<string, object> map, string key) => map.TryGetValue(key, out var value) && value != null ? Convert.ToString(value) ?? string.Empty : string.Empty;
    private static bool Bool(IDictionary<string, object> map, string key) => map.TryGetValue(key, out var value) && bool.TryParse(Convert.ToString(value), out var parsed) && parsed;
    private static DateTime Date(IDictionary<string, object> map, string key) => DateTime.TryParse(Str(map, key), out var parsed) ? parsed : default;
    private static IList<object> List(IDictionary<string, object> map, string key)
    {
        if (!map.TryGetValue(key, out var value) || value == null) return Array.Empty<object>();
        if (value is IList<object> typed) return typed;
        if (value is ArrayList array) return array.Cast<object>().ToList();
        return Array.Empty<object>();
    }
}

public sealed class EventJournalLinkRow
{
    public string LinkId { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string LinkRole { get; set; } = string.Empty;
    public bool IsPlayerVisible { get; set; }
    public string VisibilityText => IsPlayerVisible ? "видно игрокам" : "GM-only";

    public static EventJournalLinkRow FromMap(IDictionary<string, object> map) => new()
    {
        LinkId = Str(map, "linkId"),
        EntityType = Str(map, "entityType"),
        EntityId = Str(map, "entityId"),
        DisplayName = Str(map, "displayName"),
        LinkRole = Str(map, "linkRole"),
        IsPlayerVisible = Bool(map, "isPlayerVisible")
    };

    private static string Str(IDictionary<string, object> map, string key) => map.TryGetValue(key, out var value) && value != null ? Convert.ToString(value) ?? string.Empty : string.Empty;
    private static bool Bool(IDictionary<string, object> map, string key) => map.TryGetValue(key, out var value) && bool.TryParse(Convert.ToString(value), out var parsed) && parsed;
}

public sealed class EventJournalAnnotationRow
{
    public string AnnotationId { get; set; } = string.Empty;
    public string AuthorDisplayName { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public bool IsPlayerVisible { get; set; }
    public string VisibilityText => IsPlayerVisible ? "видно игрокам" : "GM-only";

    public static EventJournalAnnotationRow FromMap(IDictionary<string, object> map) => new()
    {
        AnnotationId = Str(map, "annotationId"),
        AuthorDisplayName = Str(map, "authorDisplayName"),
        Text = Str(map, "text"),
        IsPlayerVisible = Bool(map, "isPlayerVisible")
    };

    private static string Str(IDictionary<string, object> map, string key) => map.TryGetValue(key, out var value) && value != null ? Convert.ToString(value) ?? string.Empty : string.Empty;
    private static bool Bool(IDictionary<string, object> map, string key) => map.TryGetValue(key, out var value) && bool.TryParse(Convert.ToString(value), out var parsed) && parsed;
}
