using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MongoDB.Driver;
using Nri.Shared.Contracts;
using Nri.Shared.Diagnostics;
using Nri.Shared.Domain;
using Nri.Shared.Utilities;

namespace Nri.Server.Application;

public partial class ServiceHub
{
    public ResponseEnvelope VisibilityGet(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var character = ResolveVisibilityCharacter(context, actor);
        return Ok("Visibility settings loaded.", VisibilityPayload(character.Visibility));
    }

    public ResponseEnvelope VisibilityUpdate(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var character = ResolveVisibilityCharacter(context, actor);
        if (character.OwnerUserId != actor.Id && !actor.Roles.Contains(UserRole.Admin) && !actor.Roles.Contains(UserRole.SuperAdmin))
            throw new UnauthorizedAccessException("Cannot update visibility for this character.");

        var v = character.Visibility;
        v.HideDescriptionForOthers = PayloadReader.GetBool(context.Request.Payload, "hideDescriptionForOthers");
        v.HideBackstoryForOthers = PayloadReader.GetBool(context.Request.Payload, "hideBackstoryForOthers");
        v.HideStatsForOthers = PayloadReader.GetBool(context.Request.Payload, "hideStatsForOthers");
        v.HideReputationForOthers = PayloadReader.GetBool(context.Request.Payload, "hideReputationForOthers");
        v.HideRaceForOthers = v.AllowAdvancedVisibilityOverrides && PayloadReader.GetBool(context.Request.Payload, "hideRaceForOthers");
        v.HideHeightForOthers = v.AllowAdvancedVisibilityOverrides && PayloadReader.GetBool(context.Request.Payload, "hideHeightForOthers");
        v.HideInventoryForOthers = v.AllowAdvancedVisibilityOverrides && PayloadReader.GetBool(context.Request.Payload, "hideInventoryForOthers");
        _repositories.Characters.Replace(character);

        WriteAudit("visibility", actor.Id, "update", character.Id);
        return Ok("Visibility settings updated.", VisibilityPayload(v));
    }

    public ResponseEnvelope CharacterPublicViewGet(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var character = GetCharacter(RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 8, 128, "characterId"));
        var owner = GetAccount(character.OwnerUserId);
        return Ok("Public character view loaded.", CharacterPublicPayload(character, owner, actor));
    }

    public ResponseEnvelope CharacterVisibleToMeGet(CommandContext context) => CharacterPublicViewGet(context);

    public ResponseEnvelope NotesCreate(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var text = RequireLength(PayloadReader.GetString(context.Request.Payload, "text"), 1, 8000, "text");
        var title = RequireLength(PayloadReader.GetString(context.Request.Payload, "title"), 0, 256, "title");
        var sessionId = PayloadReader.GetString(context.Request.Payload, "sessionId") ?? string.Empty;
        var targetType = PayloadReader.GetString(context.Request.Payload, "targetType") ?? string.Empty;
        var targetId = PayloadReader.GetString(context.Request.Payload, "targetId") ?? string.Empty;
        var typeRaw = PayloadReader.GetString(context.Request.Payload, "noteType") ?? NoteType.Personal.ToString();
        var visRaw = PayloadReader.GetString(context.Request.Payload, "visibility") ?? NoteVisibility.Personal.ToString();

        if (!Enum.TryParse<NoteType>(typeRaw, true, out var noteType)) noteType = NoteType.Personal;
        if (!Enum.TryParse<NoteVisibility>(visRaw, true, out var visibility)) visibility = NoteVisibility.Personal;

        if (visibility == NoteVisibility.AdminOnly && !(actor.Roles.Contains(UserRole.Admin) || actor.Roles.Contains(UserRole.SuperAdmin)))
            throw new UnauthorizedAccessException("Only admin can create AdminOnly notes.");

        var note = new Note
        {
            AuthorUserId = actor.Id,
            SessionId = sessionId,
            TargetType = targetType,
            TargetId = targetId,
            NoteType = noteType,
            Visibility = visibility,
            Title = title,
            Text = text
        };

        _repositories.Notes.Insert(note);
        WriteAudit("notes", actor.Id, "create", note.Id);
        return Ok("Note created.", NotePayload(note));
    }

    public ResponseEnvelope NotesList(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var sessionId = PayloadReader.GetString(context.Request.Payload, "sessionId") ?? string.Empty;
        var targetType = PayloadReader.GetString(context.Request.Payload, "targetType") ?? string.Empty;
        var targetId = PayloadReader.GetString(context.Request.Payload, "targetId") ?? string.Empty;

        var notes = _repositories.Notes.Find(FilterDefinition<Note>.Empty)
            .Where(x => !x.Archived)
            .Where(x => string.IsNullOrWhiteSpace(sessionId) || x.SessionId == sessionId)
            .Where(x => string.IsNullOrWhiteSpace(targetType) || x.TargetType == targetType)
            .Where(x => string.IsNullOrWhiteSpace(targetId) || x.TargetId == targetId)
            .Where(x => CanViewNote(actor, x))
            .OrderByDescending(x => x.UpdatedUtc)
            .Take(300)
            .Select(NotePayload)
            .Cast<object>()
            .ToArray();

        return Ok("Notes loaded.", new Dictionary<string, object> { { "items", notes } });
    }

    public ResponseEnvelope NotesGet(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var note = _repositories.Notes.GetById(RequireLength(PayloadReader.GetString(context.Request.Payload, "noteId"), 8, 128, "noteId")) ?? throw new KeyNotFoundException("Note not found.");
        if (!CanViewNote(actor, note)) throw new UnauthorizedAccessException("Note unavailable.");
        return Ok("Note loaded.", NotePayload(note));
    }

    public ResponseEnvelope NotesUpdate(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var note = _repositories.Notes.GetById(RequireLength(PayloadReader.GetString(context.Request.Payload, "noteId"), 8, 128, "noteId")) ?? throw new KeyNotFoundException("Note not found.");
        if (!CanEditNote(actor, note)) throw new UnauthorizedAccessException("Cannot edit note.");

        note.Title = RequireLength(PayloadReader.GetString(context.Request.Payload, "title"), 0, 256, "title");
        note.Text = RequireLength(PayloadReader.GetString(context.Request.Payload, "text"), 1, 8000, "text");
        var visRaw = PayloadReader.GetString(context.Request.Payload, "visibility") ?? note.Visibility.ToString();
        if (Enum.TryParse<NoteVisibility>(visRaw, true, out var vis)) note.Visibility = vis;
        _repositories.Notes.Replace(note);
        WriteAudit("notes", actor.Id, "update", note.Id);
        return Ok("Note updated.", NotePayload(note));
    }

    public ResponseEnvelope NotesArchive(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var note = _repositories.Notes.GetById(RequireLength(PayloadReader.GetString(context.Request.Payload, "noteId"), 8, 128, "noteId")) ?? throw new KeyNotFoundException("Note not found.");
        if (!CanEditNote(actor, note)) throw new UnauthorizedAccessException("Cannot archive note.");
        note.Archived = true;
        _repositories.Notes.Replace(note);
        WriteAudit("notes", actor.Id, "archive", note.Id);
        return Ok("Note archived.");
    }

    public ResponseEnvelope ReferenceList(CommandContext context)
    {
        GetCurrentAccount(context);
        var worldId = PayloadReader.GetString(context.Request.Payload, "worldId") ?? string.Empty;
        var type = PayloadReader.GetString(context.Request.Payload, "referenceType") ?? string.Empty;
        var items = _repositories.References.Find(FilterDefinition<ReferenceEntry>.Empty)
            .Where(x => !x.Archived)
            .Where(x => string.IsNullOrWhiteSpace(worldId) || x.WorldId == worldId)
            .Where(x => string.IsNullOrWhiteSpace(type) || string.Equals(x.ReferenceType, type, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.ReferenceType).ThenBy(x => x.DisplayName)
            .Select(ReferencePayload)
            .Cast<object>()
            .ToArray();
        return Ok("Reference data loaded.", new Dictionary<string, object> { { "items", items } });
    }

    public ResponseEnvelope ReferenceGet(CommandContext context)
    {
        GetCurrentAccount(context);
        var id = RequireLength(PayloadReader.GetString(context.Request.Payload, "referenceId"), 8, 128, "referenceId");
        var item = _repositories.References.GetById(id) ?? throw new KeyNotFoundException("Reference not found.");
        return Ok("Reference item loaded.", ReferencePayload(item));
    }

    public ResponseEnvelope ReferenceCreate(CommandContext context)
    {
        var actor = RequireSuperAdmin(context);
        var item = new ReferenceEntry
        {
            WorldId = RequireLength(PayloadReader.GetString(context.Request.Payload, "worldId"), 1, 128, "worldId"),
            ReferenceType = RequireLength(PayloadReader.GetString(context.Request.Payload, "referenceType"), 2, 64, "referenceType"),
            Key = RequireLength(PayloadReader.GetString(context.Request.Payload, "key"), 1, 128, "key"),
            DisplayName = RequireLength(PayloadReader.GetString(context.Request.Payload, "displayName"), 1, 256, "displayName"),
            DataJson = RequireLength(PayloadReader.GetString(context.Request.Payload, "dataJson"), 0, 64000, "dataJson")
        };
        _repositories.References.Insert(item);
        WriteAudit("reference", actor.Id, "create", item.Id);
        return Ok("Reference item created.", ReferencePayload(item));
    }

    public ResponseEnvelope ReferenceUpdate(CommandContext context)
    {
        var actor = RequireSuperAdmin(context);
        var item = _repositories.References.GetById(RequireLength(PayloadReader.GetString(context.Request.Payload, "referenceId"), 8, 128, "referenceId")) ?? throw new KeyNotFoundException("Reference not found.");
        item.DisplayName = RequireLength(PayloadReader.GetString(context.Request.Payload, "displayName"), 1, 256, "displayName");
        item.DataJson = RequireLength(PayloadReader.GetString(context.Request.Payload, "dataJson"), 0, 64000, "dataJson");
        item.Revision += 1;
        _repositories.References.Replace(item);
        WriteAudit("reference", actor.Id, "update", item.Id);
        return Ok("Reference item updated.", ReferencePayload(item));
    }

    public ResponseEnvelope ReferenceArchive(CommandContext context)
    {
        var actor = RequireSuperAdmin(context);
        var item = _repositories.References.GetById(RequireLength(PayloadReader.GetString(context.Request.Payload, "referenceId"), 8, 128, "referenceId")) ?? throw new KeyNotFoundException("Reference not found.");
        item.Archived = true;
        _repositories.References.Replace(item);
        WriteAudit("reference", actor.Id, "archive", item.Id);
        return Ok("Reference item archived.");
    }

    public ResponseEnvelope ReferenceReload(CommandContext context)
    {
        var actor = RequireSuperAdmin(context);
        EnsureDefinitionsLoaded(true);
        EnsureAudioLibraryLoaded(true);
        WriteAudit("reference", actor.Id, "reload", "all");
        return Ok("Reference data reloaded.");
    }

    public ResponseEnvelope UpdateVersionGet(CommandContext context)
    {
        GetCurrentAccount(context);
        var channel = PayloadReader.GetString(context.Request.Payload, "channel") ?? "stable";
        var version = GetOrCreateUpdateVersion(channel);
        return Ok("Update version loaded.", new Dictionary<string, object>
        {
            { "channel", version.ClientChannel },
            { "latestVersion", version.LatestVersion }
        });
    }

    public ResponseEnvelope UpdateManifestGet(CommandContext context)
    {
        GetCurrentAccount(context);
        var channel = PayloadReader.GetString(context.Request.Payload, "channel") ?? "stable";
        var version = GetOrCreateUpdateVersion(channel);
        return Ok("Update manifest loaded.", new Dictionary<string, object>
        {
            { "channel", version.ClientChannel },
            { "latestVersion", version.LatestVersion },
            { "manifestJson", version.ManifestJson }
        });
    }

    public ResponseEnvelope UpdateClientDownloadInfo(CommandContext context)
    {
        GetCurrentAccount(context);
        var channel = PayloadReader.GetString(context.Request.Payload, "channel") ?? "stable";
        var version = GetOrCreateUpdateVersion(channel);
        return Ok("Update download info loaded.", new Dictionary<string, object>
        {
            { "channel", version.ClientChannel },
            { "latestVersion", version.LatestVersion },
            { "downloadBaseUrl", version.DownloadBaseUrl }
        });
    }

    public ResponseEnvelope BackupCreate(CommandContext context)
    {
        var actor = RequireSuperAdmin(context);
        if (!BackupBaseEnabled()) return BackupDisabled();
        if (!BackupManualCreationEnabled()) return BackupFeatureDisabled("Manual backup creation is disabled by feature flags.");
        var label = PayloadReader.GetString(context.Request.Payload, "label") ?? ("backup-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss"));
        var description = PayloadReader.GetString(context.Request.Payload, "description") ?? string.Empty;
        var record = CreateFullServerBackup(actor, label, description, isSafety: false, sourceRestoreOperationId: string.Empty);
        _logger.Admin($"backup.create id={record.BackupId} label={record.DisplayName} actor={actor.Id}");
        return Ok("Backup created.", new Dictionary<string, object> { { "item", BackupRecordPayload(record, includeDetails: true) }, { "backupId", record.BackupId }, { "label", record.DisplayName } });
    }

    public ResponseEnvelope BackupList(CommandContext context)
    {
        RequireAdmin(context);
        if (!BackupBaseEnabled()) return BackupDisabled();
        var includeArchived = PayloadReader.GetBool(context.Request.Payload, "includeArchived");
        var records = _repositories.BackupRecords.Find(FilterDefinition<BackupRecordState>.Empty)
            .Where(x => includeArchived || !x.IsArchived)
            .OrderByDescending(x => x.CreatedUtc)
            .Take(200)
            .Select(x => (object)BackupRecordPayload(x, includeDetails: false))
            .ToArray();
        var legacy = _repositories.Backups.Find(FilterDefinition<BackupSnapshot>.Empty)
            .OrderByDescending(x => x.CreatedUtc)
            .Take(50)
            .Select(x => (object)new Dictionary<string, object>
            {
                { "backupId", x.Id },
                { "displayName", x.Label },
                { "status", "legacy_snapshot" },
                { "scope", "legacy" },
                { "createdAtUtc", x.CreatedUtc },
                { "createdByUserId", x.CreatedByUserId },
                { "verificationStatus", "not_supported" },
                { "isVerified", false },
                { "documentCount", 0 },
                { "collectionCount", 0 },
                { "sizeBytes", 0 }
            })
            .ToArray();
        var items = records.Concat(legacy).ToArray();
        return Ok("Backups loaded.", new Dictionary<string, object> { { "items", items } });
    }

    public ResponseEnvelope BackupRestore(CommandContext context)
    {
        return BackupRestoreExecute(context);
    }

    public ResponseEnvelope BackupExport(CommandContext context)
    {
        RequireSuperAdmin(context);
        return Error("Backup export is disabled in Backup / Restore MVP. Use registered server-side backups only.", ResponseStatus.Forbidden, ErrorCode.Forbidden);
    }

    public ResponseEnvelope AdminLocksList(CommandContext context)
    {
        RequireAdmin(context);
        var items = _repositories.Locks.Find(FilterDefinition<EntityLock>.Empty)
            .OrderBy(x => x.EntityType).ThenBy(x => x.EntityId)
            .Select(LockPayload)
            .Cast<object>()
            .ToArray();
        return Ok("Locks loaded.", new Dictionary<string, object> { { "items", items } });
    }

    public ResponseEnvelope AdminLocksForceRelease(CommandContext context)
    {
        return LockForceRelease(context);
    }

    public ResponseEnvelope AdminServerStatus(CommandContext context)
    {
        RequireAdmin(context);
        var status = new Dictionary<string, object>
        {
            { "utcNow", DateTime.UtcNow },
            { "onlineUsers", _repositories.Presence.Find(Builders<SessionUserState>.Filter.Eq(x => x.IsOnline, true)).Count },
            { "activeCombats", _repositories.Combats.Find(Builders<CombatState>.Filter.Eq(x => x.Status, CombatStatus.Active)).Count },
            { "pendingRequests", _repositories.ActionRequests.Find(Builders<ActionRequest>.Filter.Eq(x => x.Status, RequestStatus.Pending)).Count + _repositories.DiceRequests.Find(Builders<DiceRollRequest>.Filter.Eq(x => x.Status, RequestStatus.Pending)).Count },
            { "chatMessages", _repositories.ChatMessages.Find(FilterDefinition<ChatMessage>.Empty).Count }
        };
        return Ok("Server status loaded.", status);
    }

    public ResponseEnvelope AdminSessionsList(CommandContext context)
    {
        RequireAdmin(context);
        var sessions = _repositories.Presence.Find(Builders<SessionUserState>.Filter.Eq(x => x.IsOnline, true))
            .GroupBy(x => x.CurrentGameSessionId ?? string.Empty)
            .Select(g => new Dictionary<string, object> { { "sessionId", g.Key }, { "onlineCount", g.Count() }, { "users", g.Select(x => x.UserId).Cast<object>().ToArray() } })
            .Cast<object>()
            .ToArray();
        return Ok("Sessions loaded.", new Dictionary<string, object> { { "items", sessions } });
    }

    public ResponseEnvelope AdminDiagnosticsGet(CommandContext context)
    {
        RequireAdmin(context);
        var snapshot = PerformanceTelemetry0214.Current.Snapshot();
        var commands = snapshot.Commands
            .Take(40)
            .Select(item => new Dictionary<string, object>
            {
                { "command", item.Command },
                { "category", item.Category },
                { "count", item.Count },
                { "errors", item.ErrorCount },
                { "p50Ms", item.P50Milliseconds },
                { "p95Ms", item.P95Milliseconds },
                { "p99Ms", item.P99Milliseconds },
                { "maxMs", item.MaximumMilliseconds },
                { "maxRequestBytes", item.MaximumRequestBytes },
                { "maxResponseBytes", item.MaximumResponseBytes }
            })
            .Cast<object>()
            .ToArray();
        var clientsByType = snapshot.ConnectedClients.Values
            .GroupBy(item => string.IsNullOrWhiteSpace(item.ClientType) ? "UnknownClient" : item.ClientType)
            .Select(group => new Dictionary<string, object>
            {
                { "clientType", group.Key },
                { "connected", group.Count() },
                { "privateBytes", group.Sum(item => item.PrivateBytes) },
                { "peakPrivateBytes", group.Sum(item => item.PeakPrivateBytes) },
                { "workingSetBytes", group.Sum(item => item.WorkingSetBytes) },
                { "peakWorkingSetBytes", group.Sum(item => item.PeakWorkingSetBytes) },
                { "managedHeapBytes", group.Sum(item => item.ManagedHeapBytes) },
                { "peakManagedHeapBytes", group.Sum(item => item.PeakManagedHeapBytes) },
                { "threads", group.Sum(item => item.ThreadCount) },
                { "peakThreads", group.Sum(item => item.PeakThreadCount) },
                { "handles", group.Sum(item => item.HandleCount) },
                { "peakHandles", group.Sum(item => item.PeakHandleCount) },
                { "cpuPercent", Math.Round(group.Sum(item => item.CpuPercent), 2) },
                { "uiLagP95Ms", Math.Round(group.Max(item => item.UiLagP95Ms), 2) },
                { "uiLagMaxMs", Math.Round(group.Max(item => item.UiLagMaxMs), 2) },
                { "activePollers", group.Sum(item => item.ActivePollers) },
                { "activeReconnectLoops", group.Sum(item => item.ActiveReconnectLoops) },
                { "activeTimers", group.Sum(item => item.ActiveTimers) },
                { "inFlightRefreshes", group.Sum(item => item.InFlightRefreshes) }
            })
            .Cast<object>()
            .ToArray();
        var process = snapshot.Process;
        var payloadWarnings = snapshot.Commands
            .Where(item => item.MaximumResponseBytes > 1024 * 1024)
            .Select(item => $"Large response: {item.Command} ({item.MaximumResponseBytes} bytes)")
            .Cast<object>()
            .ToArray();
        var payload = new Dictionary<string, object>
        {
            { "lastAuditEntries", _repositories.AuditLogs.Find(FilterDefinition<AuditLogEntry>.Empty).OrderByDescending(x => x.CreatedUtc).Take(20).Select(x => new Dictionary<string, object>{{"at",x.CreatedUtc},{"category",x.Category},{"action",x.Action},{"target",x.Target}}).Cast<object>().ToArray() },
            { "locksCount", _repositories.Locks.Find(FilterDefinition<EntityLock>.Empty).Count },
            { "errorsHint", "Check debug/session/admin logs for stack traces." },
            { "performance", new Dictionary<string, object>
                {
                    { "startedAtUtc", snapshot.StartedAtUtc },
                    { "builtAtUtc", snapshot.BuiltAtUtc },
                    { "elapsedSeconds", snapshot.ElapsedSeconds },
                    { "capacity", snapshot.Capacity },
                    { "retainedSamples", snapshot.RetainedSampleCount },
                    { "totalRecorded", snapshot.TotalRecordedCount },
                    { "droppedSamples", snapshot.DroppedSampleCount },
                    { "server", new Dictionary<string, object>
                        {
                            { "privateBytes", process.PrivateBytes },
                            { "peakPrivateBytes", process.PeakPrivateBytes },
                            { "workingSetBytes", process.WorkingSetBytes },
                            { "peakWorkingSetBytes", process.PeakWorkingSetBytes },
                            { "managedHeapBytes", process.ManagedHeapBytes },
                            { "peakManagedHeapBytes", process.PeakManagedHeapBytes },
                            { "cpuPercent", process.CpuPercent },
                            { "threads", process.ThreadCount },
                            { "peakThreads", process.PeakThreadCount },
                            { "handles", process.HandleCount },
                            { "peakHandles", process.PeakHandleCount }
                        }
                    },
                    { "clientsByType", clientsByType },
                    { "commands", commands },
                    { "counters", snapshot.Counters.ToDictionary(pair => pair.Key, pair => (object)pair.Value) },
                    { "warnings", payloadWarnings }
                }
            }
        };
        return Ok("Diagnostics loaded.", payload);
    }

    private UserAccount RequireSuperAdmin(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        RoleGuard.EnsureRole(actor, UserRole.SuperAdmin);
        return actor;
    }

    private Character ResolveVisibilityCharacter(CommandContext context, UserAccount actor)
    {
        var id = PayloadReader.GetString(context.Request.Payload, "characterId");
        if (!string.IsNullOrWhiteSpace(id))
        {
            var c = GetCharacter(RequireLength(id, 8, 128, "characterId"));
            return c;
        }

        return _repositories.Characters.Find(Builders<Character>.Filter.Eq(x => x.OwnerUserId, actor.Id)).FirstOrDefault() ?? throw new KeyNotFoundException("Character not found.");
    }

    private Dictionary<string, object> VisibilityPayload(CharacterVisibilitySettings v)
    {
        return new Dictionary<string, object>
        {
            { "hideDescriptionForOthers", v.HideDescriptionForOthers },
            { "hideBackstoryForOthers", v.HideBackstoryForOthers },
            { "hideStatsForOthers", v.HideStatsForOthers },
            { "hideReputationForOthers", v.HideReputationForOthers },
            { "hideRaceForOthers", v.HideRaceForOthers },
            { "hideHeightForOthers", v.HideHeightForOthers },
            { "hideInventoryForOthers", v.HideInventoryForOthers },
            { "allowAdvancedVisibilityOverrides", v.AllowAdvancedVisibilityOverrides }
        };
    }

    private Dictionary<string, object> CharacterPublicPayload(Character c, UserAccount owner, UserAccount viewer)
    {
        var payload = CharacterDetailsPayloadWithProfileFirst(c, owner, viewer, string.Empty);
        var isPrivileged = viewer.Id == owner.Id || viewer.Roles.Contains(UserRole.Admin) || viewer.Roles.Contains(UserRole.SuperAdmin);
        if (!isPrivileged)
        {
            if (c.Visibility.HideRaceForOthers && c.Visibility.AllowAdvancedVisibilityOverrides) payload["race"] = "[hidden]";
            if (c.Visibility.HideHeightForOthers && c.Visibility.AllowAdvancedVisibilityOverrides) payload["height"] = "[hidden]";
            if (c.Visibility.HideInventoryForOthers && c.Visibility.AllowAdvancedVisibilityOverrides) payload["inventory"] = "[hidden]";
        }
        payload["visibility"] = VisibilityPayload(c.Visibility);
        return payload;
    }

    private bool CanViewNote(UserAccount actor, Note note)
    {
        var isAdmin = actor.Roles.Contains(UserRole.Admin) || actor.Roles.Contains(UserRole.SuperAdmin);
        if (isAdmin) return true;
        if (note.Visibility == NoteVisibility.AdminOnly) return false;
        if (note.Visibility == NoteVisibility.Personal) return note.AuthorUserId == actor.Id;
        if (note.Visibility == NoteVisibility.SharedWithOwner)
        {
            if (note.AuthorUserId == actor.Id) return true;
            if (note.TargetType == "character" && !string.IsNullOrWhiteSpace(note.TargetId))
            {
                var c = _repositories.Characters.GetById(note.TargetId);
                return c != null && c.OwnerUserId == actor.Id;
            }
            return false;
        }
        return true;
    }

    private bool CanEditNote(UserAccount actor, Note note)
    {
        return note.AuthorUserId == actor.Id || actor.Roles.Contains(UserRole.Admin) || actor.Roles.Contains(UserRole.SuperAdmin);
    }

    private Dictionary<string, object> NotePayload(Note n)
    {
        return new Dictionary<string, object>
        {
            { "noteId", n.Id }, { "authorUserId", n.AuthorUserId }, { "sessionId", n.SessionId },
            { "targetType", n.TargetType }, { "targetId", n.TargetId }, { "noteType", n.NoteType.ToString() },
            { "visibility", n.Visibility.ToString() }, { "title", n.Title }, { "text", n.Text },
            { "archived", n.Archived }, { "updatedUtc", n.UpdatedUtc }
        };
    }

    private Dictionary<string, object> ReferencePayload(ReferenceEntry x)
    {
        return new Dictionary<string, object>
        {
            { "referenceId", x.Id }, { "worldId", x.WorldId }, { "referenceType", x.ReferenceType },
            { "key", x.Key }, { "displayName", x.DisplayName }, { "dataJson", x.DataJson },
            { "revision", x.Revision }, { "archived", x.Archived }
        };
    }

    private UpdateVersionInfo GetOrCreateUpdateVersion(string channel)
    {
        var item = _repositories.UpdateVersions.Find(Builders<UpdateVersionInfo>.Filter.Eq(x => x.ClientChannel, channel)).FirstOrDefault();
        if (item != null) return item;
        item = new UpdateVersionInfo
        {
            ClientChannel = channel,
            LatestVersion = "1.0.0",
            ManifestJson = "{\"files\":[]}",
            DownloadBaseUrl = "./updates/"
        };
        _repositories.UpdateVersions.Insert(item);
        return item;
    }

    private void RestoreCharacters(Dictionary<string, object> map)
    {
        foreach (var item in ToObjectListOrEmpty(map, "characters"))
        {
            if (item is not Dictionary<string, object> d) continue;
            var obj = JsonProtocolSerializer.Deserialize<Character>(JsonProtocolSerializer.Serialize(d));
            if (obj == null) continue;
            var existing = _repositories.Characters.GetById(obj.Id);
            if (existing == null) _repositories.Characters.Insert(obj); else _repositories.Characters.Replace(obj);
        }
    }

    private void RestoreNotes(Dictionary<string, object> map)
    {
        foreach (var item in ToObjectListOrEmpty(map, "notes"))
        {
            if (item is not Dictionary<string, object> d) continue;
            var obj = JsonProtocolSerializer.Deserialize<Note>(JsonProtocolSerializer.Serialize(d));
            if (obj == null) continue;
            var existing = _repositories.Notes.GetById(obj.Id);
            if (existing == null) _repositories.Notes.Insert(obj); else _repositories.Notes.Replace(obj);
        }
    }

    private void RestoreReferences(Dictionary<string, object> map)
    {
        foreach (var item in ToObjectListOrEmpty(map, "references"))
        {
            if (item is not Dictionary<string, object> d) continue;
            var obj = JsonProtocolSerializer.Deserialize<ReferenceEntry>(JsonProtocolSerializer.Serialize(d));
            if (obj == null) continue;
            var existing = _repositories.References.GetById(obj.Id);
            if (existing == null) _repositories.References.Insert(obj); else _repositories.References.Replace(obj);
        }
    }

    private void RestoreAudioStates(Dictionary<string, object> map)
    {
        foreach (var item in ToObjectListOrEmpty(map, "audioStates"))
        {
            if (item is not Dictionary<string, object> d) continue;
            var obj = JsonProtocolSerializer.Deserialize<SessionAudioState>(JsonProtocolSerializer.Serialize(d));
            if (obj == null) continue;
            var existing = _repositories.AudioStates.GetById(obj.Id);
            if (existing == null) _repositories.AudioStates.Insert(obj); else _repositories.AudioStates.Replace(obj);
        }
    }

    private void RestoreChatSettings(Dictionary<string, object> map)
    {
        foreach (var item in ToObjectListOrEmpty(map, "chatSettings"))
        {
            if (item is not Dictionary<string, object> d) continue;
            var obj = JsonProtocolSerializer.Deserialize<SessionChatSettings>(JsonProtocolSerializer.Serialize(d));
            if (obj == null) continue;
            var existing = _repositories.SessionChatSettings.GetById(obj.Id);
            if (existing == null) _repositories.SessionChatSettings.Insert(obj); else _repositories.SessionChatSettings.Replace(obj);
        }
    }

    private static IList ToObjectListOrEmpty(Dictionary<string, object> map, string key)
    {
        if (!map.ContainsKey(key) || map[key] == null) return new ArrayList();
        return map[key] as IList ?? new ArrayList();
    }
}
