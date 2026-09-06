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

public sealed class AdminGMNotesViewModel : ViewModelBase
{
    private readonly CommandApi _api;
    private string _campaignId = "default";
    private string _statusMessage = "Заметки GM готовы к подключению. Все GM Notes flags выключены по умолчанию.";
    private string _errorMessage = string.Empty;
    private bool _isEnabled;
    private bool _quickEnabled;
    private bool _foldersEnabled;
    private bool _linksEnabled;
    private bool _searchEnabled;
    private bool _sharedVisibilityEnabled;
    private bool _auditEnabled;
    private bool _includeArchived;
    private bool _quickOnly = true;
    private string _searchText = string.Empty;
    private GMNoteRow? _selectedNote;
    private GMNoteFolderRow? _selectedFolder;
    private GMNoteLinkRow? _selectedLink;
    private string _lastEditedNoteId = string.Empty;
    private string _title = "Новая заметка GM";
    private string _content = string.Empty;
    private string _noteType = GMNoteTypeIds.Quick;
    private string _priority = "normal";
    private string _scopeType = GMNoteEntityTypeIds.Character;
    private string _scopeEntityId = string.Empty;
    private string _scopeDisplayName = string.Empty;
    private string _visibilityMode = GMNoteVisibilityModeIds.AuthorOnly;
    private string _folderId = string.Empty;
    private string _sessionId = string.Empty;
    private string _tagsText = string.Empty;
    private string _publicSummary = string.Empty;
    private string _folderName = "Новая папка";
    private string _folderDescription = string.Empty;
    private string _folderParentId = string.Empty;
    private string _linkEntityType = GMNoteEntityTypeIds.CurrentSession;
    private string _linkEntityId = string.Empty;
    private string _linkDisplayName = string.Empty;
    private string _linkRole = GMNoteLinkRoleIds.Related;

    public AdminGMNotesViewModel(CommandApi api)
    {
        _api = api;
        RefreshFlagsCommand = new RelayCommand(RefreshFlags);
        RefreshCommand = new RelayCommand(Load);
        SearchCommand = new RelayCommand(Search);
        CreateQuickCommand = new RelayCommand(CreateQuick);
        CreateCommand = new RelayCommand(Create);
        SaveCommand = new RelayCommand(Save);
        ArchiveCommand = new RelayCommand(Archive);
        RestoreCommand = new RelayCommand(Restore);
        PinCommand = new RelayCommand(Pin);
        UnpinCommand = new RelayCommand(Unpin);
        MoveCommand = new RelayCommand(Move);
        FolderCreateCommand = new RelayCommand(CreateFolder);
        FolderUpdateCommand = new RelayCommand(UpdateFolder);
        FolderArchiveCommand = new RelayCommand(ArchiveFolder);
        LinkAddCommand = new RelayCommand(AddLink);
        LinkRemoveCommand = new RelayCommand(RemoveLink);
        AuditRefreshCommand = new RelayCommand(LoadAudit);
        ClearErrorCommand = new RelayCommand(() => ErrorMessage = string.Empty);
    }

    public ObservableCollection<GMNoteRow> Notes { get; } = new();
    public ObservableCollection<GMNoteFolderRow> Folders { get; } = new();
    public ObservableCollection<GMNoteLinkRow> Links { get; } = new();
    public ObservableCollection<GMNoteAuditRow> AuditRows { get; } = new();
    public ObservableCollection<string> NoteTypeOptions { get; } = new()
    {
        GMNoteTypeIds.Quick, GMNoteTypeIds.Preparation, GMNoteTypeIds.Session, GMNoteTypeIds.Character,
        GMNoteTypeIds.Npc, GMNoteTypeIds.Companion, GMNoteTypeIds.Group, GMNoteTypeIds.Location,
        GMNoteTypeIds.Map, GMNoteTypeIds.Combat, GMNoteTypeIds.Request, GMNoteTypeIds.Calendar,
        GMNoteTypeIds.Schedule, GMNoteTypeIds.Secret, GMNoteTypeIds.Idea, GMNoteTypeIds.Todo, GMNoteTypeIds.Custom
    };
    public ObservableCollection<string> VisibilityOptions { get; } = new()
    {
        GMNoteVisibilityModeIds.AuthorOnly,
        GMNoteVisibilityModeIds.GMTeam,
        GMNoteVisibilityModeIds.SuperAdminOnly
    };
    public ObservableCollection<string> PriorityOptions { get; } = new() { "low", "normal", "high", "urgent" };
    public ObservableCollection<string> EntityTypeOptions { get; } = new()
    {
        GMNoteEntityTypeIds.CurrentSession, GMNoteEntityTypeIds.Session, GMNoteEntityTypeIds.Character,
        GMNoteEntityTypeIds.Npc, GMNoteEntityTypeIds.Companion, GMNoteEntityTypeIds.CharacterGroup,
        GMNoteEntityTypeIds.PlayerRequest, GMNoteEntityTypeIds.WorldCalendarEvent, GMNoteEntityTypeIds.RealScheduleEvent,
        GMNoteEntityTypeIds.SceneMap, GMNoteEntityTypeIds.WorldMap, GMNoteEntityTypeIds.Room,
        GMNoteEntityTypeIds.MapMarker, GMNoteEntityTypeIds.CombatEncounter, GMNoteEntityTypeIds.Location,
        GMNoteEntityTypeIds.Country, GMNoteEntityTypeIds.Region, GMNoteEntityTypeIds.Faction,
        GMNoteEntityTypeIds.Organization, GMNoteEntityTypeIds.Custom
    };
    public ObservableCollection<string> LinkRoleOptions { get; } = new()
    {
        GMNoteLinkRoleIds.Related, GMNoteLinkRoleIds.Subject, GMNoteLinkRoleIds.Source,
        GMNoteLinkRoleIds.Target, GMNoteLinkRoleIds.PreparationFor, GMNoteLinkRoleIds.FollowUp,
        GMNoteLinkRoleIds.Custom
    };

    public ICommand RefreshFlagsCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand SearchCommand { get; }
    public ICommand CreateQuickCommand { get; }
    public ICommand CreateCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand ArchiveCommand { get; }
    public ICommand RestoreCommand { get; }
    public ICommand PinCommand { get; }
    public ICommand UnpinCommand { get; }
    public ICommand MoveCommand { get; }
    public ICommand FolderCreateCommand { get; }
    public ICommand FolderUpdateCommand { get; }
    public ICommand FolderArchiveCommand { get; }
    public ICommand LinkAddCommand { get; }
    public ICommand LinkRemoveCommand { get; }
    public ICommand AuditRefreshCommand { get; }
    public ICommand ClearErrorCommand { get; }

    public string CampaignId { get => _campaignId; set { if (_campaignId != value) { _campaignId = value ?? string.Empty; Notify(); } } }
    public string StatusMessage { get => _statusMessage; set { if (_statusMessage != value) { _statusMessage = value ?? string.Empty; Notify(); } } }
    public string ErrorMessage { get => _errorMessage; set { if (_errorMessage != value) { _errorMessage = value ?? string.Empty; Notify(); Notify(nameof(HasError)); } } }
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool IsEnabled { get => _isEnabled; set { if (_isEnabled != value) { _isEnabled = value; Notify(); Notify(nameof(IsDisabled)); } } }
    public bool IsDisabled => !IsEnabled;
    public bool QuickEnabled { get => _quickEnabled; set { if (_quickEnabled != value) { _quickEnabled = value; Notify(); } } }
    public bool FoldersEnabled { get => _foldersEnabled; set { if (_foldersEnabled != value) { _foldersEnabled = value; Notify(); } } }
    public bool LinksEnabled { get => _linksEnabled; set { if (_linksEnabled != value) { _linksEnabled = value; Notify(); } } }
    public bool SearchEnabled { get => _searchEnabled; set { if (_searchEnabled != value) { _searchEnabled = value; Notify(); } } }
    public bool SharedVisibilityEnabled { get => _sharedVisibilityEnabled; set { if (_sharedVisibilityEnabled != value) { _sharedVisibilityEnabled = value; Notify(); } } }
    public bool AuditEnabled { get => _auditEnabled; set { if (_auditEnabled != value) { _auditEnabled = value; Notify(); } } }
    public bool IncludeArchived { get => _includeArchived; set { if (_includeArchived != value) { _includeArchived = value; Notify(); } } }
    public bool QuickOnly { get => _quickOnly; set { if (_quickOnly != value) { _quickOnly = value; Notify(); } } }
    public string SearchText { get => _searchText; set { if (_searchText != value) { _searchText = value ?? string.Empty; Notify(); } } }
    public string Title { get => _title; set { if (_title != value) { _title = value ?? string.Empty; Notify(); } } }
    public string Content { get => _content; set { if (_content != value) { _content = value ?? string.Empty; Notify(); } } }
    public string NoteType { get => _noteType; set { if (_noteType != value) { _noteType = value ?? GMNoteTypeIds.Custom; Notify(); } } }
    public string Priority { get => _priority; set { if (_priority != value) { _priority = value ?? "normal"; Notify(); } } }
    public string ScopeType { get => _scopeType; set { if (_scopeType != value) { _scopeType = value ?? GMNoteEntityTypeIds.Custom; Notify(); } } }
    public string ScopeEntityId { get => _scopeEntityId; set { if (_scopeEntityId != value) { _scopeEntityId = value ?? string.Empty; Notify(); } } }
    public string ScopeDisplayName { get => _scopeDisplayName; set { if (_scopeDisplayName != value) { _scopeDisplayName = value ?? string.Empty; Notify(); } } }
    public string VisibilityMode { get => _visibilityMode; set { if (_visibilityMode != value) { _visibilityMode = value ?? GMNoteVisibilityModeIds.AuthorOnly; Notify(); } } }
    public string FolderId { get => _folderId; set { if (_folderId != value) { _folderId = value ?? string.Empty; Notify(); } } }
    public string SessionId { get => _sessionId; set { if (_sessionId != value) { _sessionId = value ?? string.Empty; Notify(); } } }
    public string TagsText { get => _tagsText; set { if (_tagsText != value) { _tagsText = value ?? string.Empty; Notify(); } } }
    public string PublicSummary { get => _publicSummary; set { if (_publicSummary != value) { _publicSummary = value ?? string.Empty; Notify(); } } }
    public string FolderName { get => _folderName; set { if (_folderName != value) { _folderName = value ?? string.Empty; Notify(); } } }
    public string FolderDescription { get => _folderDescription; set { if (_folderDescription != value) { _folderDescription = value ?? string.Empty; Notify(); } } }
    public string FolderParentId { get => _folderParentId; set { if (_folderParentId != value) { _folderParentId = value ?? string.Empty; Notify(); } } }
    public string LinkEntityType { get => _linkEntityType; set { if (_linkEntityType != value) { _linkEntityType = value ?? GMNoteEntityTypeIds.Custom; Notify(); } } }
    public string LinkEntityId { get => _linkEntityId; set { if (_linkEntityId != value) { _linkEntityId = value ?? string.Empty; Notify(); } } }
    public string LinkDisplayName { get => _linkDisplayName; set { if (_linkDisplayName != value) { _linkDisplayName = value ?? string.Empty; Notify(); } } }
    public string LinkRole { get => _linkRole; set { if (_linkRole != value) { _linkRole = value ?? GMNoteLinkRoleIds.Related; Notify(); } } }

    public GMNoteRow? SelectedNote
    {
        get => _selectedNote;
        set
        {
            _selectedNote = value;
            if (value != null && !string.IsNullOrWhiteSpace(value.NoteId))
                _lastEditedNoteId = value.NoteId;
            Notify();
            BindSelectedNote(value);
        }
    }

    public GMNoteFolderRow? SelectedFolder
    {
        get => _selectedFolder;
        set
        {
            _selectedFolder = value;
            Notify();
            if (value != null)
            {
                FolderId = value.FolderId;
                FolderName = value.Name;
                FolderDescription = value.Description;
                FolderParentId = value.ParentFolderId;
            }
        }
    }

    public GMNoteLinkRow? SelectedLink { get => _selectedLink; set { if (_selectedLink != value) { _selectedLink = value; Notify(); } } }

    public void RefreshFlags()
    {
        Safe("admin.gmnotes.flags", () =>
        {
            var response = _api.SystemFeatureFlagsSnapshot();
            var flags = ReadList(ReadMap(response.Payload, "snapshot") ?? response.Payload, "flags");
            IsEnabled = FindFlag(flags, "GMNotes.UseGMNotesMvp");
            QuickEnabled = FindFlag(flags, "GMNotes.UseGMQuickNotes");
            FoldersEnabled = FindFlag(flags, "GMNotes.UseGMNoteFolders");
            LinksEnabled = FindFlag(flags, "GMNotes.UseGMNoteEntityLinks");
            SearchEnabled = FindFlag(flags, "GMNotes.UseGMNoteSearch");
            SharedVisibilityEnabled = FindFlag(flags, "GMNotes.UseGMNoteSharedVisibility");
            AuditEnabled = FindFlag(flags, "GMNotes.UseGMNoteAudit");
            StatusMessage = IsEnabled ? "Заметки GM включены." : "Заметки GM выключены флагами функций.";
            if (IsEnabled) Load();
        });
    }

    public void Load()
    {
        if (!IsEnabled)
        {
            StatusMessage = "Заметки GM выключены флагами функций.";
            return;
        }

        Safe("admin.gmnotes.load", () =>
        {
            LoadFolders();
            var response = _api.GMNoteList(BasePayload(new Dictionary<string, object>
            {
                { "includeArchived", IncludeArchived },
                { "quickOnly", QuickOnly },
                { "folderId", FolderId },
                { "sessionId", SessionId }
            }));
            RequireOk(response);
            ReplaceNotes(ReadList(response.Payload, "items"));
            StatusMessage = $"Заметки загружены: {Notes.Count}.";
        });
    }

    private void Search()
    {
        if (!SearchEnabled)
        {
            StatusMessage = "Поиск по заметкам GM выключен флагами функций.";
            return;
        }

        Safe("admin.gmnotes.search", () =>
        {
            var response = _api.GMNoteSearch(BasePayload(new Dictionary<string, object>
            {
                { "query", SearchText },
                { "includeArchived", IncludeArchived }
            }));
            RequireOk(response);
            ReplaceNotes(ReadList(response.Payload, "items"));
            StatusMessage = $"Найдено заметок: {Notes.Count}.";
        });
    }

    private void CreateQuick()
    {
        NoteType = GMNoteTypeIds.Quick;
        Create(true);
    }

    private void Create() => Create(false);

    private void Create(bool quick)
    {
        Safe("admin.gmnotes.create", () =>
        {
            var response = _api.GMNoteCreate(NotePayload(quick));
            RequireOk(response);
            var item = ReadMap(response.Payload, "item");
            var created = item != null ? GMNoteRow.From(item) : null;
            if (created != null)
            {
                _lastEditedNoteId = created.NoteId;
                SelectedNote = created;
            }
            Load();
            if (created != null)
                SelectedNote = Notes.FirstOrDefault(x => x.NoteId == created.NoteId) ?? created;
            StatusMessage = quick ? "Быстрая заметка создана." : "Заметка создана.";
        });
    }

    private void Save()
    {
        if (SelectedNote == null && string.IsNullOrWhiteSpace(_lastEditedNoteId))
        {
            Create(false);
            return;
        }

        Safe("admin.gmnotes.save", () =>
        {
            var noteId = !string.IsNullOrWhiteSpace(SelectedNote?.NoteId) ? SelectedNote.NoteId : _lastEditedNoteId;
            var payload = NotePayload(SelectedNote?.IsQuickNote ?? false);
            payload["noteId"] = noteId;
            var response = _api.GMNoteUpdate(payload);
            RequireOk(response);
            Load();
            _lastEditedNoteId = noteId;
            SelectedNote = Notes.FirstOrDefault(x => x.NoteId == noteId) ?? SelectedNote;
            StatusMessage = "Заметка сохранена.";
        });
    }

    private void Archive()
    {
        if (!Confirm("Архивировать заметку", "Заметка будет убрана из рабочего списка и сохранена в архиве. Продолжить?")) return;
        NoteStateChange(_api.GMNoteArchive, "Заметка отправлена в архив.");
    }
    private void Restore() => NoteStateChange(_api.GMNoteRestore, "Заметка восстановлена.");
    private void Pin() => NoteStateChange(_api.GMNotePin, "Заметка закреплена.");
    private void Unpin() => NoteStateChange(_api.GMNoteUnpin, "Закрепление снято.");

    private void Move()
    {
        if (SelectedNote == null) return;
        Safe("admin.gmnotes.move", () =>
        {
            var response = _api.GMNoteMove(new Dictionary<string, object>
            {
                { "noteId", SelectedNote.NoteId },
                { "folderId", FolderId },
                { "sortOrder", SelectedNote.SortOrder }
            });
            RequireOk(response);
            Load();
            StatusMessage = "Заметка перемещена.";
        });
    }

    private void CreateFolder()
    {
        if (!FoldersEnabled)
        {
            StatusMessage = "Папки заметок GM выключены флагами функций.";
            return;
        }

        Safe("admin.gmnotes.folder.create", () =>
        {
            var response = _api.GMNoteFolderCreate(BasePayload(new Dictionary<string, object>
            {
                { "parentFolderId", FolderParentId },
                { "name", FolderName },
                { "description", FolderDescription },
                { "visibilityMode", VisibilityMode },
                { "tagsText", TagsText }
            }));
            RequireOk(response);
            LoadFolders();
            StatusMessage = "Папка создана.";
        });
    }

    private void UpdateFolder()
    {
        if (SelectedFolder == null) return;
        Safe("admin.gmnotes.folder.update", () =>
        {
            var response = _api.GMNoteFolderUpdate(BasePayload(new Dictionary<string, object>
            {
                { "folderId", SelectedFolder.FolderId },
                { "parentFolderId", FolderParentId },
                { "name", FolderName },
                { "description", FolderDescription },
                { "visibilityMode", VisibilityMode },
                { "tagsText", TagsText }
            }));
            RequireOk(response);
            LoadFolders();
            StatusMessage = "Папка сохранена.";
        });
    }

    private void ArchiveFolder()
    {
        if (SelectedFolder == null) return;
        if (!Confirm("Архивировать папку", "Папка будет убрана из рабочего списка. Продолжить?")) return;
        Safe("admin.gmnotes.folder.archive", () =>
        {
            var response = _api.GMNoteFolderArchive(new Dictionary<string, object> { { "folderId", SelectedFolder.FolderId } });
            RequireOk(response);
            LoadFolders();
            StatusMessage = "Папка архивирована.";
        });
    }

    private void AddLink()
    {
        if (SelectedNote == null || !LinksEnabled) return;
        Safe("admin.gmnotes.link.add", () =>
        {
            var response = _api.GMNoteLinkAdd(BasePayload(new Dictionary<string, object>
            {
                { "noteId", SelectedNote.NoteId },
                { "entityType", LinkEntityType },
                { "entityId", LinkEntityId },
                { "displayName", LinkDisplayName },
                { "linkRole", LinkRole }
            }));
            RequireOk(response);
            LoadLinks();
            StatusMessage = "Привязка добавлена. Существование справочника пока не проверяется.";
        });
    }

    private void RemoveLink()
    {
        if (SelectedLink == null || !LinksEnabled) return;
        if (!Confirm("Удалить привязку", "Привязка заметки к выбранному объекту будет удалена. Продолжить?")) return;
        Safe("admin.gmnotes.link.remove", () =>
        {
            var response = _api.GMNoteLinkRemove(new Dictionary<string, object> { { "linkId", SelectedLink.LinkId } });
            RequireOk(response);
            LoadLinks();
            StatusMessage = "Привязка удалена.";
        });
    }

    private static bool Confirm(string title, string message)
        => System.Windows.MessageBox.Show(message, title, System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning)
           == System.Windows.MessageBoxResult.Yes;

    private void LoadFolders()
    {
        if (!FoldersEnabled)
        {
            Folders.Clear();
            return;
        }

        var response = _api.GMNoteFolderList(BasePayload(new Dictionary<string, object> { { "includeArchived", IncludeArchived } }));
        RequireOk(response);
        Folders.Clear();
        foreach (var map in ReadList(response.Payload, "items").Select(ToMap).Where(x => x != null))
            Folders.Add(GMNoteFolderRow.From(map!));
    }

    private void LoadLinks()
    {
        Links.Clear();
        if (SelectedNote == null || !LinksEnabled) return;
        var response = _api.GMNoteLinkList(new Dictionary<string, object> { { "noteId", SelectedNote.NoteId } });
        RequireOk(response);
        foreach (var map in ReadList(response.Payload, "items").Select(ToMap).Where(x => x != null))
            Links.Add(GMNoteLinkRow.From(map!));
    }

    private void LoadAudit()
    {
        AuditRows.Clear();
        if (SelectedNote == null || !AuditEnabled) return;
        Safe("admin.gmnotes.audit.load", () =>
        {
            var response = _api.GMNoteAuditList(new Dictionary<string, object> { { "noteId", SelectedNote.NoteId } });
            RequireOk(response);
            foreach (var map in ReadList(response.Payload, "items").Select(ToMap).Where(x => x != null))
                AuditRows.Add(GMNoteAuditRow.From(map!));
        });
    }

    private void NoteStateChange(Func<Dictionary<string, object>, ResponseEnvelope> action, string message)
    {
        var noteId = !string.IsNullOrWhiteSpace(SelectedNote?.NoteId) ? SelectedNote.NoteId : _lastEditedNoteId;
        if (string.IsNullOrWhiteSpace(noteId)) return;
        Safe("admin.gmnotes.state", () =>
        {
            var response = action(new Dictionary<string, object> { { "noteId", noteId } });
            RequireOk(response);
            Load();
            _lastEditedNoteId = noteId;
            SelectedNote = Notes.FirstOrDefault(x => x.NoteId == noteId) ?? SelectedNote;
            StatusMessage = message;
        });
    }

    private Dictionary<string, object> NotePayload(bool quick)
        => BasePayload(new Dictionary<string, object>
        {
            { "sessionId", SessionId },
            { "folderId", FolderId },
            { "title", Title },
            { "content", Content },
            { "noteType", quick ? GMNoteTypeIds.Quick : NoteType },
            { "priority", Priority },
            { "scopeType", ScopeType },
            { "scopeEntityId", ScopeEntityId },
            { "scopeDisplayName", ScopeDisplayName },
            { "visibilityMode", VisibilityMode },
            { "isPinned", SelectedNote?.IsPinned ?? false },
            { "isQuickNote", quick },
            { "publicSummary", PublicSummary },
            { "tagsText", TagsText }
        });

    private Dictionary<string, object> BasePayload(Dictionary<string, object> payload)
    {
        payload["campaignId"] = string.IsNullOrWhiteSpace(CampaignId) ? "default" : CampaignId.Trim();
        return payload;
    }

    private void BindSelectedNote(GMNoteRow? value)
    {
        Links.Clear();
        AuditRows.Clear();
        if (value == null) return;
        Title = value.Title;
        Content = value.Content;
        NoteType = value.NoteType;
        Priority = value.Priority;
        ScopeType = value.ScopeType;
        ScopeEntityId = value.ScopeEntityId;
        ScopeDisplayName = value.ScopeDisplayName;
        VisibilityMode = value.VisibilityMode;
        FolderId = value.FolderId;
        SessionId = value.SessionId;
        TagsText = value.TagsText;
        PublicSummary = value.PublicSummary;
        if (LinksEnabled) LoadLinks();
        if (AuditEnabled) LoadAudit();
    }

    private void ReplaceNotes(IEnumerable<object> rows)
    {
        Notes.Clear();
        foreach (var map in rows.Select(ToMap).Where(x => x != null))
            Notes.Add(GMNoteRow.From(map!));
        if (SelectedNote != null)
            SelectedNote = Notes.FirstOrDefault(x => x.NoteId == SelectedNote.NoteId);
    }

    private void Safe(string area, Action action)
    {
        try
        {
            ErrorMessage = string.Empty;
            action();
            ClientLogService.Instance.Info(area + ".done");
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            ClientLogService.Instance.Error(area + ".error " + ex);
        }
    }

    private static void RequireOk(ResponseEnvelope response)
    {
        if (response.Status != ResponseStatus.Ok)
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(response.Message) ? response.Status.ToString() : response.Message);
    }

    private static bool FindFlag(IEnumerable<object> flags, string name)
    {
        foreach (var map in flags.Select(ToMap).Where(x => x != null))
        {
            if (!string.Equals(S(map!, "name"), name, StringComparison.OrdinalIgnoreCase)) continue;
            return B(map!, "effectiveValue");
        }
        return false;
    }

    private static Dictionary<string, object>? ReadMap(IDictionary<string, object> payload, string key)
        => payload.TryGetValue(key, out var value) ? ToMap(value) : null;

    private static IList<object> ReadList(IDictionary<string, object> payload, string key)
    {
        if (!payload.TryGetValue(key, out var value) || value == null) return Array.Empty<object>();
        if (value is IList<object> typed) return typed;
        if (value is IEnumerable enumerable && value is not string)
        {
            var result = new List<object>();
            foreach (var item in enumerable) result.Add(item!);
            return result;
        }
        return Array.Empty<object>();
    }

    private static Dictionary<string, object>? ToMap(object value)
    {
        if (value is Dictionary<string, object> typed) return typed;
        if (value is IDictionary dictionary)
        {
            var map = new Dictionary<string, object>();
            foreach (DictionaryEntry entry in dictionary)
            {
                var key = Convert.ToString(entry.Key);
                if (!string.IsNullOrWhiteSpace(key)) map[key] = entry.Value!;
            }
            return map;
        }
        if (value is IEnumerable enumerable && value is not string)
        {
            var map = new Dictionary<string, object>();
            var sequentialItems = new List<object?>();
            foreach (var item in enumerable)
            {
                if (item == null) continue;
                sequentialItems.Add(item);

                if (item is DictionaryEntry entry)
                {
                    var key = Convert.ToString(entry.Key);
                    if (!string.IsNullOrWhiteSpace(key)) map[key] = entry.Value!;
                    continue;
                }

                if (item is object[] arrayPair && arrayPair.Length == 2)
                {
                    var key = Convert.ToString(arrayPair[0]);
                    if (!string.IsNullOrWhiteSpace(key)) map[key] = arrayPair[1]!;
                    continue;
                }

                if (item is IList listPair && listPair.Count == 2)
                {
                    var key = Convert.ToString(listPair[0]);
                    if (!string.IsNullOrWhiteSpace(key)) map[key] = listPair[1]!;
                    continue;
                }

                var type = item.GetType();
                var keyProperty = type.GetProperty("Key") ?? type.GetProperty("Name");
                var valueProperty = type.GetProperty("Value");
                if (keyProperty == null || valueProperty == null) continue;
                var reflectedKey = Convert.ToString(keyProperty.GetValue(item));
                if (!string.IsNullOrWhiteSpace(reflectedKey)) map[reflectedKey] = valueProperty.GetValue(item)!;
            }

            if (map.Count == 0 && sequentialItems.Count % 2 == 0)
            {
                for (var i = 0; i < sequentialItems.Count; i += 2)
                {
                    var key = Convert.ToString(sequentialItems[i]);
                    if (!string.IsNullOrWhiteSpace(key)) map[key] = sequentialItems[i + 1]!;
                }
            }

            if (map.Count > 0) return map;
        }
        return null;
    }

    private static string S(IDictionary<string, object> map, string key)
        => map.TryGetValue(key, out var value) && value != null ? Convert.ToString(value) ?? string.Empty : string.Empty;

    private static bool B(IDictionary<string, object> map, string key)
        => map.TryGetValue(key, out var value) && value != null && bool.TryParse(Convert.ToString(value), out var result) && result;

    private static int I(IDictionary<string, object> map, string key)
        => map.TryGetValue(key, out var value) && value != null && int.TryParse(Convert.ToString(value), out var result) ? result : 0;
}

public sealed class GMNoteRow
{
    public string NoteId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string FolderId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string NoteType { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string ScopeType { get; set; } = string.Empty;
    public string ScopeEntityId { get; set; } = string.Empty;
    public string ScopeDisplayName { get; set; } = string.Empty;
    public string VisibilityMode { get; set; } = string.Empty;
    public bool IsPinned { get; set; }
    public bool IsQuickNote { get; set; }
    public bool IsArchived { get; set; }
    public int SortOrder { get; set; }
    public string PublicSummary { get; set; } = string.Empty;
    public string TagsText { get; set; } = string.Empty;
    public string Preview => $"{(IsPinned ? "★ " : string.Empty)}{Title} | {NoteTypeLabel} | {VisibilityLabel} | {TagsText}";
    public string NoteTypeLabel => Label(NoteType);
    public string VisibilityLabel => Label(VisibilityMode);
    public string StateLabel => IsArchived ? "Архив" : IsPinned ? "Закреплена" : "Активна";

    public static GMNoteRow From(IDictionary<string, object> map) => new()
    {
        NoteId = S(map, "noteId"),
        CampaignId = S(map, "campaignId"),
        SessionId = S(map, "sessionId"),
        FolderId = S(map, "folderId"),
        Title = S(map, "title"),
        Content = S(map, "content"),
        NoteType = S(map, "noteType"),
        Priority = string.IsNullOrWhiteSpace(S(map, "priority")) ? "normal" : S(map, "priority"),
        ScopeType = S(map, "scopeType"),
        ScopeEntityId = S(map, "scopeEntityId"),
        ScopeDisplayName = S(map, "scopeDisplayName"),
        VisibilityMode = S(map, "visibilityMode"),
        IsPinned = B(map, "isPinned"),
        IsQuickNote = B(map, "isQuickNote"),
        IsArchived = B(map, "isArchived"),
        SortOrder = I(map, "sortOrder"),
        PublicSummary = S(map, "publicSummary"),
        TagsText = string.Join(", ", ReadArray(map, "tags"))
    };

    private static string S(IDictionary<string, object> map, string key) => map.TryGetValue(key, out var value) && value != null ? Convert.ToString(value) ?? string.Empty : string.Empty;
    private static bool B(IDictionary<string, object> map, string key) => map.TryGetValue(key, out var value) && value != null && bool.TryParse(Convert.ToString(value), out var result) && result;
    private static int I(IDictionary<string, object> map, string key) => map.TryGetValue(key, out var value) && value != null && int.TryParse(Convert.ToString(value), out var result) ? result : 0;
    private static IEnumerable<string> ReadArray(IDictionary<string, object> map, string key)
    {
        if (!map.TryGetValue(key, out var value) || value == null || value is string) return Array.Empty<string>();
        if (value is IEnumerable enumerable) return enumerable.Cast<object>().Select(x => Convert.ToString(x) ?? string.Empty).Where(x => !string.IsNullOrWhiteSpace(x));
        return Array.Empty<string>();
    }
    private static string Label(string value) => value switch
    {
        "quick" => "Быстрая",
        "preparation" => "Подготовка",
        "session" => "Сессия",
        "character" => "Персонаж",
        "npc" => "NPC",
        "companion" => "Компаньон",
        "group" => "Группа",
        "location" => "Локация",
        "map" => "Карта",
        "combat" => "Бой",
        "request" => "Заявка",
        "calendar" => "Календарь",
        "schedule" => "Расписание",
        "secret" => "Секрет",
        "idea" => "Идея",
        "todo" => "TODO",
        "author_only" => "Только автор",
        "gm_team" => "GM-команда",
        "superadmin_only" => "SuperAdmin",
        _ => string.IsNullOrWhiteSpace(value) ? "—" : value
    };
}

public sealed class GMNoteFolderRow
{
    public string FolderId { get; set; } = string.Empty;
    public string ParentFolderId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string VisibilityMode { get; set; } = string.Empty;
    public bool IsArchived { get; set; }
    public string Preview => $"{Name} | {(IsArchived ? "Архив" : "Активна")} | {VisibilityMode}";

    public static GMNoteFolderRow From(IDictionary<string, object> map) => new()
    {
        FolderId = S(map, "folderId"),
        ParentFolderId = S(map, "parentFolderId"),
        Name = S(map, "name"),
        Description = S(map, "description"),
        VisibilityMode = S(map, "visibilityMode"),
        IsArchived = B(map, "isArchived")
    };

    private static string S(IDictionary<string, object> map, string key) => map.TryGetValue(key, out var value) && value != null ? Convert.ToString(value) ?? string.Empty : string.Empty;
    private static bool B(IDictionary<string, object> map, string key) => map.TryGetValue(key, out var value) && value != null && bool.TryParse(Convert.ToString(value), out var result) && result;
}

public sealed class GMNoteLinkRow
{
    public string LinkId { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string LinkRole { get; set; } = string.Empty;
    public string Preview => $"{DisplayName} | {EntityType}:{EntityId} | {LinkRole}";

    public static GMNoteLinkRow From(IDictionary<string, object> map) => new()
    {
        LinkId = S(map, "linkId"),
        EntityType = S(map, "entityType"),
        EntityId = S(map, "entityId"),
        DisplayName = S(map, "displayName"),
        LinkRole = S(map, "linkRole")
    };

    private static string S(IDictionary<string, object> map, string key) => map.TryGetValue(key, out var value) && value != null ? Convert.ToString(value) ?? string.Empty : string.Empty;
}

public sealed class GMNoteAuditRow
{
    public string ActionType { get; set; } = string.Empty;
    public string PerformedByUserId { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string PerformedAtUtc { get; set; } = string.Empty;
    public string Preview => $"{PerformedAtUtc} | {ActionType} | {PerformedByUserId} | {Summary}";

    public static GMNoteAuditRow From(IDictionary<string, object> map) => new()
    {
        ActionType = S(map, "actionType"),
        PerformedByUserId = S(map, "performedByUserId"),
        Summary = S(map, "summary"),
        PerformedAtUtc = S(map, "performedAtUtc")
    };

    private static string S(IDictionary<string, object> map, string key) => map.TryGetValue(key, out var value) && value != null ? Convert.ToString(value) ?? string.Empty : string.Empty;
}
