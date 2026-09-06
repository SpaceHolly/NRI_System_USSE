using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Driver;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;
using Nri.Shared.Utilities;

namespace Nri.Server.Application;

public partial class ServiceHub
{
    private const int GMNoteMaxList = 500;

    public ResponseEnvelope GMNoteList(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!GMNotesEnabled()) return GMNotesDisabled();
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var campaignId = ReadCampaignId(payload);
        var includeArchived = PayloadReader.GetBool(payload, "includeArchived");
        var folderId = RequireLength(PayloadReader.GetString(payload, "folderId"), 0, 128, "folderId");
        var sessionId = RequireLength(PayloadReader.GetString(payload, "sessionId"), 0, 128, "sessionId");
        var quickOnly = PayloadReader.GetBool(payload, "quickOnly");
        var items = LoadVisibleGMNotes(actor, campaignId, includeArchived)
            .Where(x => string.IsNullOrWhiteSpace(folderId) || x.FolderId == folderId)
            .Where(x => string.IsNullOrWhiteSpace(sessionId) || x.SessionId == sessionId)
            .Where(x => !quickOnly || x.IsQuickNote)
            .OrderByDescending(x => x.IsPinned)
            .ThenByDescending(x => x.UpdatedAtUtc)
            .Take(GMNoteMaxList)
            .Select(x => (object)GMNotePayload(x))
            .ToArray();
        _logger.Admin($"gm.note.list actor={actor.Login} campaignId={campaignId} count={items.Length}");
        return Ok("GM notes loaded.", new Dictionary<string, object> { { "items", items } });
    }

    public ResponseEnvelope GMNoteSearch(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!GMNotesEnabled()) return GMNotesDisabled();
        if (!GMNoteSearchEnabled()) return GMNotesFeatureDisabled("Поиск по заметкам GM выключен feature flags.");
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var campaignId = ReadCampaignId(payload);
        var query = (PayloadReader.GetString(payload, "query") ?? string.Empty).Trim();
        var includeArchived = PayloadReader.GetBool(payload, "includeArchived");
        if (query.Length < 2) return Ok("GM note search requires at least 2 characters.", new Dictionary<string, object> { { "items", Array.Empty<object>() } });
        var links = LoadGMNoteLinks(campaignId, string.Empty, includeArchived: false);
        var items = LoadVisibleGMNotes(actor, campaignId, includeArchived)
            .Where(x => Contains(x.Title, query) || Contains(x.Content, query) || x.Tags.Any(tag => Contains(tag, query)) || links.Any(link => link.NoteId == x.Id && Contains(link.DisplayName, query)))
            .OrderByDescending(x => x.IsPinned)
            .ThenByDescending(x => x.UpdatedAtUtc)
            .Take(GMNoteMaxList)
            .Select(x => (object)GMNotePayload(x))
            .ToArray();
        return Ok("GM notes search completed.", new Dictionary<string, object> { { "items", items } });
    }

    public ResponseEnvelope GMNoteCreate(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!GMNotesEnabled()) return GMNotesDisabled();
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var isQuick = PayloadReader.GetBool(payload, "isQuickNote");
        if (isQuick && !GMQuickNotesEnabled()) return GMNotesFeatureDisabled("Быстрые заметки GM выключены feature flags.");
        var visibility = NormalizeGMNoteVisibility(PayloadReader.GetString(payload, "visibilityMode"));
        if (visibility == GMNoteVisibilityModeIds.GMTeam && !GMNoteSharedVisibilityEnabled())
            return GMNotesFeatureDisabled("Общие заметки GM-команды выключены feature flags.");
        var now = DateTime.UtcNow;
        var note = new GMNoteState
        {
            CampaignId = ReadCampaignId(payload),
            SessionId = RequireLength(PayloadReader.GetString(payload, "sessionId"), 0, 128, "sessionId"),
            FolderId = RequireLength(PayloadReader.GetString(payload, "folderId"), 0, 128, "folderId"),
            AuthorUserId = actor.Id,
            AuthorDisplayName = FirstNonEmpty(actor.Login, actor.Id),
            Title = RequireLength(FirstNonEmpty(PayloadReader.GetString(payload, "title"), "Новая заметка GM"), 1, 256, "title"),
            Content = RequireLength(PayloadReader.GetString(payload, "content"), 0, 16000, "content"),
            NoteType = NormalizeGMNoteType(PayloadReader.GetString(payload, "noteType"), isQuick ? GMNoteTypeIds.Quick : GMNoteTypeIds.Preparation),
            Priority = NormalizeGMNotePriority(PayloadReader.GetString(payload, "priority")),
            ScopeType = NormalizeGMNoteEntityType(FirstNonEmpty(PayloadReader.GetString(payload, "scopeType"), PayloadReader.GetString(payload, "linkedEntityType"))),
            ScopeEntityId = RequireLength(FirstNonEmpty(PayloadReader.GetString(payload, "scopeEntityId"), PayloadReader.GetString(payload, "characterId"), PayloadReader.GetString(payload, "linkedEntityId")), 0, 128, "scopeEntityId"),
            ScopeDisplayName = RequireLength(FirstNonEmpty(PayloadReader.GetString(payload, "scopeDisplayName"), PayloadReader.GetString(payload, "linkedEntityDisplayName")), 0, 256, "scopeDisplayName"),
            VisibilityMode = visibility,
            IsSharedWithGMs = visibility == GMNoteVisibilityModeIds.GMTeam,
            IsPinned = PayloadReader.GetBool(payload, "isPinned"),
            IsQuickNote = isQuick,
            SortOrder = PayloadReader.GetInt(payload, "sortOrder") ?? 0,
            PublicSummary = RequireLength(PayloadReader.GetString(payload, "publicSummary"), 0, 2048, "publicSummary"),
            Tags = ReadTags(payload),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        _repositories.GMNotes.Insert(note);
        WriteAudit("gm_notes", actor.Id, "gm.note.created", note.Id);
        WriteGMNoteAudit(actor, note, GMNoteAuditActionIds.Created, "GM note created.");
        _logger.Admin($"gm.note.create actor={actor.Login} campaignId={note.CampaignId} noteId={note.Id} quick={note.IsQuickNote}");
        return Ok("GM note created.", new Dictionary<string, object> { { "item", GMNotePayload(note) } });
    }

    public ResponseEnvelope GMNoteGet(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!GMNotesEnabled()) return GMNotesDisabled();
        var note = RequireGMNote(context, actor, canEdit: false);
        return Ok("GM note loaded.", new Dictionary<string, object> { { "item", GMNotePayload(note) } });
    }

    public ResponseEnvelope GMNoteUpdate(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!GMNotesEnabled()) return GMNotesDisabled();
        var note = RequireGMNote(context, actor, canEdit: true);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        if (payload.ContainsKey("title")) note.Title = RequireLength(FirstNonEmpty(PayloadReader.GetString(payload, "title"), note.Title), 1, 256, "title");
        if (payload.ContainsKey("content")) note.Content = RequireLength(PayloadReader.GetString(payload, "content"), 0, 16000, "content");
        if (payload.ContainsKey("noteType")) note.NoteType = NormalizeGMNoteType(PayloadReader.GetString(payload, "noteType"), note.NoteType);
        if (payload.ContainsKey("priority")) note.Priority = NormalizeGMNotePriority(PayloadReader.GetString(payload, "priority"));
        if (payload.ContainsKey("scopeType") || payload.ContainsKey("linkedEntityType")) note.ScopeType = NormalizeGMNoteEntityType(FirstNonEmpty(PayloadReader.GetString(payload, "scopeType"), PayloadReader.GetString(payload, "linkedEntityType")));
        if (payload.ContainsKey("scopeEntityId") || payload.ContainsKey("characterId") || payload.ContainsKey("linkedEntityId"))
            note.ScopeEntityId = RequireLength(FirstNonEmpty(PayloadReader.GetString(payload, "scopeEntityId"), PayloadReader.GetString(payload, "characterId"), PayloadReader.GetString(payload, "linkedEntityId")), 0, 128, "scopeEntityId");
        if (payload.ContainsKey("scopeDisplayName") || payload.ContainsKey("linkedEntityDisplayName"))
            note.ScopeDisplayName = RequireLength(FirstNonEmpty(PayloadReader.GetString(payload, "scopeDisplayName"), PayloadReader.GetString(payload, "linkedEntityDisplayName")), 0, 256, "scopeDisplayName");
        if (payload.ContainsKey("sessionId")) note.SessionId = RequireLength(PayloadReader.GetString(payload, "sessionId"), 0, 128, "sessionId");
        if (payload.ContainsKey("folderId")) note.FolderId = RequireLength(PayloadReader.GetString(payload, "folderId"), 0, 128, "folderId");
        if (payload.ContainsKey("publicSummary")) note.PublicSummary = RequireLength(PayloadReader.GetString(payload, "publicSummary"), 0, 2048, "publicSummary");
        if (payload.ContainsKey("tags")) note.Tags = ReadTags(payload);
        if (payload.ContainsKey("tagsText")) note.Tags = ReadTags(payload);
        if (payload.ContainsKey("visibilityMode"))
        {
            var visibility = NormalizeGMNoteVisibility(PayloadReader.GetString(payload, "visibilityMode"));
            if (visibility == GMNoteVisibilityModeIds.GMTeam && !GMNoteSharedVisibilityEnabled())
                return GMNotesFeatureDisabled("Общие заметки GM-команды выключены feature flags.");
            note.VisibilityMode = visibility;
            note.IsSharedWithGMs = visibility == GMNoteVisibilityModeIds.GMTeam;
        }
        note.UpdatedAtUtc = DateTime.UtcNow;
        note.Revision++;
        _repositories.GMNotes.Replace(note);
        WriteAudit("gm_notes", actor.Id, "gm.note.updated", note.Id);
        WriteGMNoteAudit(actor, note, GMNoteAuditActionIds.Updated, "GM note updated.");
        return Ok("GM note updated.", new Dictionary<string, object> { { "item", GMNotePayload(note) } });
    }

    public ResponseEnvelope GMNoteArchive(CommandContext context) => SetGMNoteArchived(context, true);
    public ResponseEnvelope GMNoteRestore(CommandContext context) => SetGMNoteArchived(context, false);
    public ResponseEnvelope GMNotePin(CommandContext context) => SetGMNotePinned(context, true);
    public ResponseEnvelope GMNoteUnpin(CommandContext context) => SetGMNotePinned(context, false);

    public ResponseEnvelope GMNoteMove(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!GMNotesEnabled()) return GMNotesDisabled();
        if (!GMNoteFoldersEnabled()) return GMNotesFeatureDisabled("Папки заметок GM выключены feature flags.");
        var note = RequireGMNote(context, actor, canEdit: true);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        note.FolderId = RequireLength(PayloadReader.GetString(payload, "folderId"), 0, 128, "folderId");
        note.SortOrder = PayloadReader.GetInt(payload, "sortOrder") ?? note.SortOrder;
        note.UpdatedAtUtc = DateTime.UtcNow;
        note.Revision++;
        _repositories.GMNotes.Replace(note);
        WriteAudit("gm_notes", actor.Id, "gm.note.moved", note.Id);
        WriteGMNoteAudit(actor, note, GMNoteAuditActionIds.Moved, "GM note moved.");
        return Ok("GM note moved.", new Dictionary<string, object> { { "item", GMNotePayload(note) } });
    }

    public ResponseEnvelope GMNoteFolderList(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!GMNotesEnabled()) return GMNotesDisabled();
        if (!GMNoteFoldersEnabled()) return GMNotesFeatureDisabled("Папки заметок GM выключены feature flags.");
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var campaignId = ReadCampaignId(payload);
        var includeArchived = PayloadReader.GetBool(payload, "includeArchived");
        var filter = Builders<GMNoteFolderState>.Filter.Eq(x => x.CampaignId, campaignId);
        if (!includeArchived) filter &= Builders<GMNoteFolderState>.Filter.Eq(x => x.IsArchived, false);
        var items = _repositories.GMNoteFolders.Find(filter)
            .Where(x => CanViewGMNoteFolder(actor, x))
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Select(x => (object)GMNoteFolderPayload(x))
            .ToArray();
        return Ok("GM note folders loaded.", new Dictionary<string, object> { { "items", items } });
    }

    public ResponseEnvelope GMNoteFolderCreate(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!GMNotesEnabled()) return GMNotesDisabled();
        if (!GMNoteFoldersEnabled()) return GMNotesFeatureDisabled("Папки заметок GM выключены feature flags.");
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var visibility = NormalizeGMNoteVisibility(PayloadReader.GetString(payload, "visibilityMode"));
        var now = DateTime.UtcNow;
        var folder = new GMNoteFolderState
        {
            CampaignId = ReadCampaignId(payload),
            ParentFolderId = RequireLength(PayloadReader.GetString(payload, "parentFolderId"), 0, 128, "parentFolderId"),
            Name = RequireLength(FirstNonEmpty(PayloadReader.GetString(payload, "name"), "Новая папка"), 1, 160, "name"),
            Description = RequireLength(PayloadReader.GetString(payload, "description"), 0, 2048, "description"),
            VisibilityMode = visibility,
            OwnerUserId = actor.Id,
            SortOrder = PayloadReader.GetInt(payload, "sortOrder") ?? 0,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            CreatedByUserId = actor.Id,
            UpdatedByUserId = actor.Id,
            Tags = ReadTags(payload)
        };
        _repositories.GMNoteFolders.Insert(folder);
        return Ok("GM note folder created.", new Dictionary<string, object> { { "item", GMNoteFolderPayload(folder) } });
    }

    public ResponseEnvelope GMNoteFolderUpdate(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!GMNotesEnabled()) return GMNotesDisabled();
        if (!GMNoteFoldersEnabled()) return GMNotesFeatureDisabled("Папки заметок GM выключены feature flags.");
        var folder = RequireGMNoteFolder(context, actor);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        if (payload.ContainsKey("name")) folder.Name = RequireLength(FirstNonEmpty(PayloadReader.GetString(payload, "name"), folder.Name), 1, 160, "name");
        if (payload.ContainsKey("description")) folder.Description = RequireLength(PayloadReader.GetString(payload, "description"), 0, 2048, "description");
        if (payload.ContainsKey("parentFolderId"))
        {
            var parentFolderId = RequireLength(PayloadReader.GetString(payload, "parentFolderId"), 0, 128, "parentFolderId");
            if (parentFolderId == folder.Id) throw new ArgumentException("parentFolderId cannot point to itself.");
            folder.ParentFolderId = parentFolderId;
        }
        if (payload.ContainsKey("visibilityMode")) folder.VisibilityMode = NormalizeGMNoteVisibility(PayloadReader.GetString(payload, "visibilityMode"));
        if (payload.ContainsKey("sortOrder")) folder.SortOrder = PayloadReader.GetInt(payload, "sortOrder") ?? folder.SortOrder;
        if (payload.ContainsKey("tags")) folder.Tags = ReadTags(payload);
        if (payload.ContainsKey("tagsText")) folder.Tags = ReadTags(payload);
        folder.UpdatedAtUtc = DateTime.UtcNow;
        folder.UpdatedByUserId = actor.Id;
        _repositories.GMNoteFolders.Replace(folder);
        return Ok("GM note folder updated.", new Dictionary<string, object> { { "item", GMNoteFolderPayload(folder) } });
    }

    public ResponseEnvelope GMNoteFolderArchive(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!GMNotesEnabled()) return GMNotesDisabled();
        if (!GMNoteFoldersEnabled()) return GMNotesFeatureDisabled("Папки заметок GM выключены feature flags.");
        var folder = RequireGMNoteFolder(context, actor);
        folder.IsArchived = true;
        folder.UpdatedAtUtc = DateTime.UtcNow;
        folder.UpdatedByUserId = actor.Id;
        _repositories.GMNoteFolders.Replace(folder);
        return Ok("GM note folder archived.", new Dictionary<string, object> { { "item", GMNoteFolderPayload(folder) } });
    }

    public ResponseEnvelope GMNoteLinkList(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!GMNotesEnabled()) return GMNotesDisabled();
        if (!GMNoteEntityLinksEnabled()) return GMNotesFeatureDisabled("Привязки заметок GM выключены feature flags.");
        var note = RequireGMNote(context, actor, canEdit: false);
        var items = LoadGMNoteLinks(note.CampaignId, note.Id, includeArchived: false)
            .Select(x => (object)GMNoteLinkPayload(x))
            .ToArray();
        return Ok("GM note links loaded.", new Dictionary<string, object> { { "items", items } });
    }

    public ResponseEnvelope GMNoteLinkAdd(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!GMNotesEnabled()) return GMNotesDisabled();
        if (!GMNoteEntityLinksEnabled()) return GMNotesFeatureDisabled("Привязки заметок GM выключены feature flags.");
        var note = RequireGMNote(context, actor, canEdit: true);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var entityId = RequireLength(PayloadReader.GetString(payload, "entityId"), 1, 128, "entityId");
        var link = new GMNoteEntityLinkState
        {
            NoteId = note.Id,
            CampaignId = note.CampaignId,
            EntityType = NormalizeGMNoteEntityType(PayloadReader.GetString(payload, "entityType")),
            EntityId = entityId,
            DisplayName = RequireLength(FirstNonEmpty(PayloadReader.GetString(payload, "displayName"), entityId), 1, 256, "displayName"),
            LinkRole = NormalizeGMNoteLinkRole(PayloadReader.GetString(payload, "linkRole")),
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = actor.Id
        };
        _repositories.GMNoteLinks.Insert(link);
        WriteAudit("gm_notes", actor.Id, "gm.note.link.added", $"{note.Id}:{link.Id}");
        WriteGMNoteAudit(actor, note, GMNoteAuditActionIds.LinkAdded, $"GM note link added: {link.EntityType}:{link.EntityId}");
        return Ok("GM note link added.", new Dictionary<string, object> { { "item", GMNoteLinkPayload(link) }, { "warning", "Привязка сохранена без проверки существования справочника." } });
    }

    public ResponseEnvelope GMNoteLinkRemove(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!GMNotesEnabled()) return GMNotesDisabled();
        if (!GMNoteEntityLinksEnabled()) return GMNotesFeatureDisabled("Привязки заметок GM выключены feature flags.");
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var linkId = RequireLength(PayloadReader.GetString(payload, "linkId"), 1, 128, "linkId");
        var link = _repositories.GMNoteLinks.GetById(linkId) ?? throw new KeyNotFoundException("GM note link not found.");
        var note = _repositories.GMNotes.GetById(link.NoteId) ?? throw new KeyNotFoundException("GM note not found.");
        if (!CanEditGMNote(actor, note)) throw new UnauthorizedAccessException("GM note edit is forbidden.");
        link.IsArchived = true;
        link.UpdatedAtUtc = DateTime.UtcNow;
        _repositories.GMNoteLinks.Replace(link);
        WriteAudit("gm_notes", actor.Id, "gm.note.link.removed", $"{note.Id}:{link.Id}");
        WriteGMNoteAudit(actor, note, GMNoteAuditActionIds.LinkRemoved, $"GM note link removed: {link.EntityType}:{link.EntityId}");
        return Ok("GM note link removed.", new Dictionary<string, object> { { "linkId", link.Id } });
    }

    public ResponseEnvelope GMNoteAuditList(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!GMNotesEnabled()) return GMNotesDisabled();
        if (!GMNoteAuditEnabled()) return GMNotesFeatureDisabled("Аудит заметок GM выключен feature flags.");
        var note = RequireGMNote(context, actor, canEdit: false);
        var filter = Builders<GMNoteAuditEntry>.Filter.Eq(x => x.NoteId, note.Id);
        var items = _repositories.GMNoteAudit.Find(filter)
            .OrderByDescending(x => x.PerformedAtUtc)
            .Take(200)
            .Select(x => (object)GMNoteAuditPayload(x))
            .ToArray();
        return Ok("GM note audit loaded.", new Dictionary<string, object> { { "items", items } });
    }

    private ResponseEnvelope SetGMNoteArchived(CommandContext context, bool archived)
    {
        var actor = GetCurrentAccount(context);
        if (!GMNotesEnabled()) return GMNotesDisabled();
        var note = RequireGMNote(context, actor, canEdit: true);
        note.IsArchived = archived;
        note.ArchivedAtUtc = archived ? DateTime.UtcNow : null;
        note.UpdatedAtUtc = DateTime.UtcNow;
        note.Revision++;
        _repositories.GMNotes.Replace(note);
        var action = archived ? GMNoteAuditActionIds.Archived : GMNoteAuditActionIds.Restored;
        WriteAudit("gm_notes", actor.Id, archived ? "gm.note.archived" : "gm.note.restored", note.Id);
        WriteGMNoteAudit(actor, note, action, archived ? "GM note archived." : "GM note restored.");
        return Ok(archived ? "GM note archived." : "GM note restored.", new Dictionary<string, object> { { "item", GMNotePayload(note) } });
    }

    private ResponseEnvelope SetGMNotePinned(CommandContext context, bool pinned)
    {
        var actor = GetCurrentAccount(context);
        if (!GMNotesEnabled()) return GMNotesDisabled();
        var note = RequireGMNote(context, actor, canEdit: true);
        note.IsPinned = pinned;
        note.UpdatedAtUtc = DateTime.UtcNow;
        note.Revision++;
        _repositories.GMNotes.Replace(note);
        var action = pinned ? GMNoteAuditActionIds.Pinned : GMNoteAuditActionIds.Unpinned;
        WriteAudit("gm_notes", actor.Id, pinned ? "gm.note.pinned" : "gm.note.unpinned", note.Id);
        WriteGMNoteAudit(actor, note, action, pinned ? "GM note pinned." : "GM note unpinned.");
        return Ok(pinned ? "GM note pinned." : "GM note unpinned.", new Dictionary<string, object> { { "item", GMNotePayload(note) } });
    }

    private GMNoteState RequireGMNote(CommandContext context, UserAccount actor, bool canEdit)
    {
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var noteId = RequireLength(FirstNonEmpty(PayloadReader.GetString(payload, "noteId"), PayloadReader.GetString(payload, "id")), 1, 128, "noteId");
        var note = _repositories.GMNotes.GetById(noteId) ?? throw new KeyNotFoundException("GM note not found.");
        if (canEdit)
        {
            if (!CanEditGMNote(actor, note)) throw new UnauthorizedAccessException("GM note edit is forbidden.");
        }
        else if (!CanViewGMNote(actor, note))
        {
            throw new UnauthorizedAccessException("GM note view is forbidden.");
        }
        return note;
    }

    private GMNoteFolderState RequireGMNoteFolder(CommandContext context, UserAccount actor)
    {
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var folderId = RequireLength(FirstNonEmpty(PayloadReader.GetString(payload, "folderId"), PayloadReader.GetString(payload, "id")), 1, 128, "folderId");
        var folder = _repositories.GMNoteFolders.GetById(folderId) ?? throw new KeyNotFoundException("GM note folder not found.");
        if (!CanEditGMNoteFolder(actor, folder)) throw new UnauthorizedAccessException("GM note folder edit is forbidden.");
        return folder;
    }

    private List<GMNoteState> LoadVisibleGMNotes(UserAccount actor, string campaignId, bool includeArchived)
    {
        var filter = Builders<GMNoteState>.Filter.Eq(x => x.CampaignId, campaignId);
        if (!includeArchived) filter &= Builders<GMNoteState>.Filter.Eq(x => x.IsArchived, false);
        return _repositories.GMNotes.Find(filter).Where(x => CanViewGMNote(actor, x)).ToList();
    }

    private List<GMNoteEntityLinkState> LoadGMNoteLinks(string campaignId, string noteId, bool includeArchived)
    {
        var filter = Builders<GMNoteEntityLinkState>.Filter.Eq(x => x.CampaignId, campaignId);
        if (!string.IsNullOrWhiteSpace(noteId)) filter &= Builders<GMNoteEntityLinkState>.Filter.Eq(x => x.NoteId, noteId);
        if (!includeArchived) filter &= Builders<GMNoteEntityLinkState>.Filter.Eq(x => x.IsArchived, false);
        return _repositories.GMNoteLinks.Find(filter).ToList();
    }

    private bool CanViewGMNote(UserAccount actor, GMNoteState note)
    {
        if (actor.Roles.Contains(UserRole.SuperAdmin)) return true;
        if (note.VisibilityMode == GMNoteVisibilityModeIds.AuthorOnly) return note.AuthorUserId == actor.Id;
        if (note.VisibilityMode == GMNoteVisibilityModeIds.GMTeam) return true;
        return false;
    }

    private bool CanEditGMNote(UserAccount actor, GMNoteState note)
    {
        if (actor.Roles.Contains(UserRole.SuperAdmin)) return true;
        if (note.AuthorUserId == actor.Id) return true;
        return note.VisibilityMode == GMNoteVisibilityModeIds.GMTeam;
    }

    private bool CanViewGMNoteFolder(UserAccount actor, GMNoteFolderState folder)
    {
        if (actor.Roles.Contains(UserRole.SuperAdmin)) return true;
        if (folder.VisibilityMode == GMNoteVisibilityModeIds.AuthorOnly) return folder.OwnerUserId == actor.Id || folder.CreatedByUserId == actor.Id;
        if (folder.VisibilityMode == GMNoteVisibilityModeIds.GMTeam) return true;
        return false;
    }

    private bool CanEditGMNoteFolder(UserAccount actor, GMNoteFolderState folder)
    {
        if (actor.Roles.Contains(UserRole.SuperAdmin)) return true;
        return folder.OwnerUserId == actor.Id || folder.CreatedByUserId == actor.Id || folder.VisibilityMode == GMNoteVisibilityModeIds.GMTeam;
    }

    private void WriteGMNoteAudit(UserAccount actor, GMNoteState note, string action, string summary)
    {
        if (!GMNoteAuditEnabled()) return;
        _repositories.GMNoteAudit.Insert(new GMNoteAuditEntry
        {
            CampaignId = note.CampaignId,
            NoteId = note.Id,
            ActionType = action,
            PerformedByUserId = actor.Id,
            Summary = RequireLength(summary, 0, 512, "summary"),
            PerformedAtUtc = DateTime.UtcNow
        });
    }

    private static Dictionary<string, object> GMNotePayload(GMNoteState note) => new Dictionary<string, object>
    {
        { "noteId", note.Id },
        { "campaignId", note.CampaignId },
        { "sessionId", note.SessionId },
        { "folderId", note.FolderId },
        { "authorUserId", note.AuthorUserId },
        { "authorDisplayName", note.AuthorDisplayName },
        { "title", note.Title },
        { "content", note.Content },
        { "noteType", note.NoteType },
        { "priority", note.Priority },
        { "scopeType", note.ScopeType },
        { "scopeEntityId", note.ScopeEntityId },
        { "scopeDisplayName", note.ScopeDisplayName },
        { "visibilityMode", note.VisibilityMode },
        { "isSharedWithGMs", note.IsSharedWithGMs },
        { "isPinned", note.IsPinned },
        { "isQuickNote", note.IsQuickNote },
        { "isArchived", note.IsArchived },
        { "revision", note.Revision },
        { "sortOrder", note.SortOrder },
        { "createdAtUtc", note.CreatedAtUtc },
        { "updatedAtUtc", note.UpdatedAtUtc },
        { "archivedAtUtc", note.ArchivedAtUtc.HasValue ? (object)note.ArchivedAtUtc.Value : string.Empty },
        { "publicSummary", note.PublicSummary },
        { "tags", note.Tags.Cast<object>().ToArray() }
    };

    private static Dictionary<string, object> GMNoteFolderPayload(GMNoteFolderState folder) => new Dictionary<string, object>
    {
        { "folderId", folder.Id },
        { "campaignId", folder.CampaignId },
        { "parentFolderId", folder.ParentFolderId },
        { "name", folder.Name },
        { "description", folder.Description },
        { "visibilityMode", folder.VisibilityMode },
        { "ownerUserId", folder.OwnerUserId },
        { "sortOrder", folder.SortOrder },
        { "isArchived", folder.IsArchived },
        { "createdAtUtc", folder.CreatedAtUtc },
        { "updatedAtUtc", folder.UpdatedAtUtc },
        { "tags", folder.Tags.Cast<object>().ToArray() }
    };

    private static Dictionary<string, object> GMNoteLinkPayload(GMNoteEntityLinkState link) => new Dictionary<string, object>
    {
        { "linkId", link.Id },
        { "noteId", link.NoteId },
        { "campaignId", link.CampaignId },
        { "entityType", link.EntityType },
        { "entityId", link.EntityId },
        { "displayName", link.DisplayName },
        { "linkRole", link.LinkRole },
        { "isArchived", link.IsArchived },
        { "createdAtUtc", link.CreatedAtUtc },
        { "updatedAtUtc", link.UpdatedAtUtc }
    };

    private static Dictionary<string, object> GMNoteAuditPayload(GMNoteAuditEntry entry) => new Dictionary<string, object>
    {
        { "auditId", entry.Id },
        { "noteId", entry.NoteId },
        { "campaignId", entry.CampaignId },
        { "actionType", entry.ActionType },
        { "performedByUserId", entry.PerformedByUserId },
        { "summary", entry.Summary },
        { "performedAtUtc", entry.PerformedAtUtc }
    };

    private static List<string> ReadTags(IDictionary<string, object> payload)
    {
        var result = new List<string>();
        var list = PayloadReader.GetList(payload, "tags");
        if (list != null)
        {
            foreach (var item in list)
            {
                var value = Convert.ToString(item)?.Trim();
                if (!string.IsNullOrWhiteSpace(value) && !result.Contains(value, StringComparer.OrdinalIgnoreCase))
                    result.Add(value);
            }
        }

        var text = PayloadReader.GetString(payload, "tagsText");
        if (!string.IsNullOrWhiteSpace(text))
        {
            foreach (var part in text.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var value = part.Trim();
                if (!string.IsNullOrWhiteSpace(value) && !result.Contains(value, StringComparer.OrdinalIgnoreCase))
                    result.Add(value);
            }
        }

        return result.Take(32).ToList();
    }

    private static string ReadCampaignId(IDictionary<string, object> payload)
        => RequireLength(PayloadReader.GetString(payload, "campaignId") ?? "default", 1, 128, "campaignId");

    private static string NormalizeGMNoteType(string? value, string fallback)
    {
        var text = (value ?? fallback ?? string.Empty).Trim().ToLowerInvariant();
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            GMNoteTypeIds.Quick, GMNoteTypeIds.Preparation, GMNoteTypeIds.Session, GMNoteTypeIds.Character,
            GMNoteTypeIds.Npc, GMNoteTypeIds.Companion, GMNoteTypeIds.Group, GMNoteTypeIds.Location,
            GMNoteTypeIds.Map, GMNoteTypeIds.Combat, GMNoteTypeIds.Request, GMNoteTypeIds.Calendar,
            GMNoteTypeIds.Schedule, GMNoteTypeIds.Secret, GMNoteTypeIds.Idea, GMNoteTypeIds.Todo, GMNoteTypeIds.Custom
        };
        return allowed.Contains(text) ? text : GMNoteTypeIds.Custom;
    }

    private static string NormalizeGMNoteVisibility(string? value)
    {
        var text = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (text == GMNoteVisibilityModeIds.GMTeam) return GMNoteVisibilityModeIds.GMTeam;
        if (text == GMNoteVisibilityModeIds.SuperAdminOnly) return GMNoteVisibilityModeIds.SuperAdminOnly;
        return GMNoteVisibilityModeIds.AuthorOnly;
    }

    private static string NormalizeGMNoteEntityType(string? value)
    {
        var text = (value ?? string.Empty).Trim().ToLowerInvariant();
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            GMNoteEntityTypeIds.CurrentSession, GMNoteEntityTypeIds.Session, GMNoteEntityTypeIds.Character,
            GMNoteEntityTypeIds.Npc, GMNoteEntityTypeIds.Companion, GMNoteEntityTypeIds.CharacterGroup,
            GMNoteEntityTypeIds.PlayerRequest, GMNoteEntityTypeIds.WorldCalendarEvent, GMNoteEntityTypeIds.RealScheduleEvent,
            GMNoteEntityTypeIds.SceneMap, GMNoteEntityTypeIds.WorldMap, GMNoteEntityTypeIds.Room,
            GMNoteEntityTypeIds.MapMarker, GMNoteEntityTypeIds.CombatEncounter, GMNoteEntityTypeIds.Location,
            GMNoteEntityTypeIds.Country, GMNoteEntityTypeIds.Region, GMNoteEntityTypeIds.Faction,
            GMNoteEntityTypeIds.Organization, GMNoteEntityTypeIds.Custom
        };
        return allowed.Contains(text) ? text : GMNoteEntityTypeIds.Custom;
    }

    private static string NormalizeGMNotePriority(string? value)
    {
        var text = (value ?? string.Empty).Trim().ToLowerInvariant();
        return text switch
        {
            "low" or "normal" or "high" or "urgent" => text,
            _ => "normal"
        };
    }

    private static string NormalizeGMNoteLinkRole(string? value)
    {
        var text = (value ?? string.Empty).Trim().ToLowerInvariant();
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            GMNoteLinkRoleIds.Related, GMNoteLinkRoleIds.Subject, GMNoteLinkRoleIds.Source,
            GMNoteLinkRoleIds.Target, GMNoteLinkRoleIds.PreparationFor, GMNoteLinkRoleIds.FollowUp,
            GMNoteLinkRoleIds.Custom
        };
        return allowed.Contains(text) ? text : GMNoteLinkRoleIds.Related;
    }

    private static bool Contains(string? source, string query)
        => !string.IsNullOrWhiteSpace(source) && source.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;

    private bool GMNotesEnabled() => _featureFlags.IsEnabled(nameof(GMNotesFeatureFlags.UseGMNotesMvp));
    private bool GMQuickNotesEnabled() => GMNotesEnabled() && _featureFlags.IsEnabled(nameof(GMNotesFeatureFlags.UseGMQuickNotes));
    private bool GMNoteFoldersEnabled() => GMNotesEnabled() && _featureFlags.IsEnabled(nameof(GMNotesFeatureFlags.UseGMNoteFolders));
    private bool GMNoteEntityLinksEnabled() => GMNotesEnabled() && _featureFlags.IsEnabled(nameof(GMNotesFeatureFlags.UseGMNoteEntityLinks));
    private bool GMNoteSearchEnabled() => GMNotesEnabled() && _featureFlags.IsEnabled(nameof(GMNotesFeatureFlags.UseGMNoteSearch));
    private bool GMNoteSharedVisibilityEnabled() => GMNotesEnabled() && _featureFlags.IsEnabled(nameof(GMNotesFeatureFlags.UseGMNoteSharedVisibility));
    private bool GMNoteAuditEnabled() => GMNotesEnabled() && _featureFlags.IsEnabled(nameof(GMNotesFeatureFlags.UseGMNoteAudit));
    private static ResponseEnvelope GMNotesDisabled() => GMNotesFeatureDisabled("Заметки GM выключены feature flags.");
    private static ResponseEnvelope GMNotesFeatureDisabled(string message) => Error(message, ResponseStatus.Forbidden, ErrorCode.Forbidden);
}
