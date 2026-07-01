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
    public ResponseEnvelope SessionCurrentGet(CommandContext context)
    {
        RequireAdmin(context);
        if (!CurrentSessionReadEnabled())
            return CurrentSessionDisabled(context.Request.Command);

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var campaignId = RequireLength(PayloadReader.GetString(payload, "campaignId"), 1, 128, "campaignId");
        var sessionId = RequireLength(PayloadReader.GetString(payload, "sessionId"), 0, 128, "sessionId");
        var session = LoadSession(campaignId, sessionId);
        _logger.Admin($"session.current.get campaignId={campaignId} sessionId={sessionId}");

        return Ok("Current session loaded.", new Dictionary<string, object>
        {
            { "hasSession", session != null },
            { "session", session == null ? new Dictionary<string, object>() : AdminSessionPayload(session) },
            { "playerPreview", session == null ? new Dictionary<string, object>() : PlayerSessionPayload(session) },
            { "warnings", BuildAdminWarnings(session).Cast<object>().ToArray() }
        });
    }

    public ResponseEnvelope SessionCurrentCreate(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!CurrentSessionWriteEnabled())
            return CurrentSessionDisabled(context.Request.Command);

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var campaignId = RequireLength(PayloadReader.GetString(payload, "campaignId"), 1, 128, "campaignId");
        var name = SessionFirstNonEmpty(RequireLength(PayloadReader.GetString(payload, "name"), 0, 160, "name"), "Новая сессия");
        var now = DateTime.UtcNow;
        _logger.Admin($"session.current.create.start campaignId={campaignId} actor={actor.Login}");

        var session = new CurrentSessionState
        {
            CampaignId = campaignId,
            SessionId = $"session_{Guid.NewGuid():N}",
            Name = name,
            Description = RequireLength(PayloadReader.GetString(payload, "description"), 0, 4096, "description"),
            Status = CurrentSessionStatusIds.Planned,
            Mode = CurrentSessionModeIds.Preparation,
            GMUserId = SessionFirstNonEmpty(RequireLength(PayloadReader.GetString(payload, "gmUserId"), 0, 128, "gmUserId"), actor.Id),
            GMDisplayName = SessionFirstNonEmpty(RequireLength(PayloadReader.GetString(payload, "gmDisplayName"), 0, 160, "gmDisplayName"), actor.Login),
            VisibilityMode = NormalizeSessionVisibility(RequireLength(PayloadReader.GetString(payload, "visibilityMode"), 0, 32, "visibilityMode")),
            IsPlayerVisible = !payload.ContainsKey("isPlayerVisible") || PayloadReader.GetBool(payload, "isPlayerVisible"),
            CurrentRealStartUtc = ParseDateTime(payload, "currentRealStartUtc"),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            CreatedByUserId = actor.Id,
            UpdatedByUserId = actor.Id
        };

        _repositories.CurrentSessions.Insert(session);
        _logger.Admin($"session.current.create.done campaignId={campaignId} sessionId={session.SessionId}");
        return Ok("Current session created.", new Dictionary<string, object>
        {
            { "session", AdminSessionPayload(session) },
            { "playerPreview", PlayerSessionPayload(session) }
        });
    }

    public ResponseEnvelope SessionCurrentUpdate(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!CurrentSessionWriteEnabled())
            return CurrentSessionDisabled(context.Request.Command);

        var session = RequireSessionById(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        if (payload.ContainsKey("name"))
            session.Name = SessionFirstNonEmpty(RequireLength(PayloadReader.GetString(payload, "name"), 0, 160, "name"), session.Name, "Новая сессия");
        if (payload.ContainsKey("description"))
            session.Description = RequireLength(PayloadReader.GetString(payload, "description"), 0, 4096, "description");
        if (payload.ContainsKey("visibilityMode"))
            session.VisibilityMode = NormalizeSessionVisibility(RequireLength(PayloadReader.GetString(payload, "visibilityMode"), 0, 32, "visibilityMode"));
        if (payload.ContainsKey("isPlayerVisible"))
            session.IsPlayerVisible = PayloadReader.GetBool(payload, "isPlayerVisible");
        if (payload.ContainsKey("publicNotes"))
            session.PublicNotes = RequireLength(PayloadReader.GetString(payload, "publicNotes"), 0, 4096, "publicNotes");
        if (payload.ContainsKey("gmNotes"))
            session.GMNotes = RequireLength(PayloadReader.GetString(payload, "gmNotes"), 0, 4096, "gmNotes");

        TouchSession(session, actor.Id);
        SaveSession(session);
        _logger.Admin($"session.current.update sessionId={session.SessionId}");
        return SessionResponse(session, "Current session updated.");
    }

    public ResponseEnvelope SessionCurrentStart(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!CurrentSessionWriteEnabled())
            return CurrentSessionDisabled(context.Request.Command);

        var session = RequireSessionById(context);
        if (IsArchivedSession(session))
            return Error("archived session cannot be started", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);
        var active = FindActiveSession(session.CampaignId);
        if (active != null && !string.Equals(active.SessionId, session.SessionId, StringComparison.OrdinalIgnoreCase))
            return Error("В кампании уже есть активная сессия.", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);

        session.Status = CurrentSessionStatusIds.Active;
        session.StartedAtUtc ??= DateTime.UtcNow;
        session.PausedAtUtc = null;
        TouchSession(session, actor.Id);
        SaveSession(session);
        _logger.Admin($"session.current.start sessionId={session.SessionId}");
        return SessionResponse(session, "Current session started.");
    }

    public ResponseEnvelope SessionCurrentPause(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!CurrentSessionWriteEnabled())
            return CurrentSessionDisabled(context.Request.Command);
        var session = RequireSessionById(context);
        if (IsTerminalSession(session))
            return Error("completed/cancelled session cannot be paused", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);
        session.Status = CurrentSessionStatusIds.Paused;
        session.PausedAtUtc = DateTime.UtcNow;
        TouchSession(session, actor.Id);
        SaveSession(session);
        _logger.Admin($"session.current.pause sessionId={session.SessionId}");
        return SessionResponse(session, "Current session paused.");
    }

    public ResponseEnvelope SessionCurrentResume(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!CurrentSessionWriteEnabled())
            return CurrentSessionDisabled(context.Request.Command);
        var session = RequireSessionById(context);
        if (IsTerminalSession(session))
            return Error("completed/cancelled session cannot be resumed", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);
        var active = FindActiveSession(session.CampaignId);
        if (active != null && !string.Equals(active.SessionId, session.SessionId, StringComparison.OrdinalIgnoreCase))
            return Error("В кампании уже есть активная сессия.", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);
        session.Status = CurrentSessionStatusIds.Active;
        session.PausedAtUtc = null;
        TouchSession(session, actor.Id);
        SaveSession(session);
        _logger.Admin($"session.current.resume sessionId={session.SessionId}");
        return SessionResponse(session, "Current session resumed.");
    }

    public ResponseEnvelope SessionCurrentComplete(CommandContext context)
    {
        return SetTerminalSessionStatus(context, CurrentSessionStatusIds.Completed, "session.current.complete", "Current session completed.");
    }

    public ResponseEnvelope SessionCurrentCancel(CommandContext context)
    {
        return SetTerminalSessionStatus(context, CurrentSessionStatusIds.Cancelled, "session.current.cancel", "Current session cancelled.");
    }

    public ResponseEnvelope SessionCurrentSetScene(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!CurrentSessionSceneLinkEnabled())
            return CurrentSessionDisabled(context.Request.Command);
        var session = RequireSessionById(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        session.CurrentSceneId = RequireLength(PayloadReader.GetString(payload, "currentSceneId"), 0, 128, "currentSceneId");
        session.CurrentSceneName = RequireLength(PayloadReader.GetString(payload, "currentSceneName"), 0, 240, "currentSceneName");
        if (payload.ContainsKey("activeRoomId"))
        {
            session.ActiveRoomId = RequireLength(PayloadReader.GetString(payload, "activeRoomId"), 0, 128, "activeRoomId");
            session.ActiveRoomName = ResolveRoomName(session.ActiveRoomId);
        }
        TouchSession(session, actor.Id);
        SaveSession(session);
        _logger.Admin($"session.current.setScene sessionId={session.SessionId}");
        return SessionResponse(session, "Current scene updated.");
    }

    public ResponseEnvelope SessionCurrentSetMode(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!CurrentSessionWriteEnabled())
            return CurrentSessionDisabled(context.Request.Command);
        var session = RequireSessionById(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        session.Mode = NormalizeSessionMode(PayloadReader.GetString(payload, "mode"));
        TouchSession(session, actor.Id);
        SaveSession(session);
        _logger.Admin($"session.current.setMode sessionId={session.SessionId} mode={session.Mode}");
        return SessionResponse(session, "Current session mode updated.");
    }

    public ResponseEnvelope SessionCurrentSetActiveSceneMap(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!CurrentSessionMapLinkEnabled())
            return CurrentSessionDisabled(context.Request.Command);
        var session = RequireSessionById(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var mapId = RequireLength(PayloadReader.GetString(payload, "mapId"), 0, 128, "mapId");
        MapCanvasState? map = null;
        if (!string.IsNullOrWhiteSpace(mapId))
        {
            map = _repositories.MapCanvases.GetByIdAsync(mapId).GetAwaiter().GetResult();
            if (map == null || map.Deleted || map.Archived || map.IsArchived || !string.Equals(map.MapType, MapTypeIds.Scene, StringComparison.OrdinalIgnoreCase))
                return Error("scene map not found", ResponseStatus.NotFound, ErrorCode.NotFound);
            if (!string.Equals(map.CampaignId, session.CampaignId, StringComparison.OrdinalIgnoreCase))
                return Error("scene map belongs to another campaign", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);
        }

        session.ActiveSceneMapId = map?.Id ?? string.Empty;
        session.ActiveSceneMapName = map?.Name ?? string.Empty;
        TouchSession(session, actor.Id);
        SaveSession(session);
        SyncSceneActiveLink(session, map, actor.Id);
        _logger.Admin($"session.current.setActiveSceneMap sessionId={session.SessionId} mapId={mapId}");
        return SessionResponse(session, "Active scene map updated.");
    }

    public ResponseEnvelope SessionCurrentSetActiveCombat(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!CurrentSessionWriteEnabled())
            return CurrentSessionDisabled(context.Request.Command);
        var session = RequireSessionById(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var encounterId = RequireLength(PayloadReader.GetString(payload, "combatEncounterId"), 1, 128, "combatEncounterId");
        var encounter = _repositories.CombatEncounters.GetByIdAsync(encounterId).GetAwaiter().GetResult();
        if (encounter == null || encounter.Deleted || encounter.Archived)
            return Error("combat encounter not found", ResponseStatus.NotFound, ErrorCode.NotFound);
        session.ActiveCombatEncounterId = encounter.Id;
        session.ActiveCombatName = SessionFirstNonEmpty(encounter.Name, encounter.Status, encounter.Id);
        session.Mode = CurrentSessionModeIds.Combat;
        TouchSession(session, actor.Id);
        SaveSession(session);
        _logger.Admin($"session.current.setActiveCombat sessionId={session.SessionId} encounterId={encounterId}");
        return SessionResponse(session, "Active combat updated.");
    }

    public ResponseEnvelope SessionCurrentClearActiveCombat(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!CurrentSessionWriteEnabled())
            return CurrentSessionDisabled(context.Request.Command);
        var session = RequireSessionById(context);
        session.ActiveCombatEncounterId = string.Empty;
        session.ActiveCombatName = string.Empty;
        if (string.Equals(session.Mode, CurrentSessionModeIds.Combat, StringComparison.OrdinalIgnoreCase))
            session.Mode = CurrentSessionModeIds.NormalScene;
        TouchSession(session, actor.Id);
        SaveSession(session);
        _logger.Admin($"session.current.clearActiveCombat sessionId={session.SessionId}");
        return SessionResponse(session, "Active combat cleared.");
    }

    public ResponseEnvelope SessionCurrentSetNotes(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!CurrentSessionWriteEnabled())
            return CurrentSessionDisabled(context.Request.Command);
        var session = RequireSessionById(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        session.PublicNotes = RequireLength(PayloadReader.GetString(payload, "publicNotes"), 0, 4096, "publicNotes");
        session.GMNotes = RequireLength(PayloadReader.GetString(payload, "gmNotes"), 0, 4096, "gmNotes");
        TouchSession(session, actor.Id);
        SaveSession(session);
        _logger.Admin($"session.current.setNotes sessionId={session.SessionId}");
        return SessionResponse(session, "Session notes updated.");
    }

    public ResponseEnvelope SessionPlayerCurrentGet(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!CurrentSessionPlayerViewEnabled())
        {
            _logger.Debug($"session.player.current.get.disabled user={actor.Login}");
            return Error("current session player view is disabled by feature flags", ResponseStatus.Forbidden, ErrorCode.Forbidden);
        }

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var campaignId = RequireLength(PayloadReader.GetString(payload, "campaignId"), 0, 128, "campaignId");
        var sessionId = RequireLength(PayloadReader.GetString(payload, "sessionId"), 0, 128, "sessionId");
        var session = LoadSession(campaignId, sessionId);
        if (session == null)
            return Ok("No current session.", new Dictionary<string, object> { { "hasSession", false }, { "session", new Dictionary<string, object>() } });
        if (!IsSessionVisibleForPlayer(session))
            return Error("current session is not visible for player", ResponseStatus.Forbidden, ErrorCode.Forbidden);

        _logger.Debug($"session.player.current.get user={actor.Login} sessionId={session.SessionId}");
        return Ok("Current session loaded.", new Dictionary<string, object>
        {
            { "hasSession", true },
            { "session", PlayerSessionPayload(session) }
        });
    }

    private ResponseEnvelope SetTerminalSessionStatus(CommandContext context, string status, string logEvent, string message)
    {
        var actor = RequireAdmin(context);
        if (!CurrentSessionWriteEnabled())
            return CurrentSessionDisabled(context.Request.Command);
        var session = RequireSessionById(context);
        session.Status = status;
        session.EndedAtUtc = DateTime.UtcNow;
        TouchSession(session, actor.Id);
        SaveSession(session);
        _logger.Admin($"{logEvent} sessionId={session.SessionId}");
        return SessionResponse(session, message);
    }

    private ResponseEnvelope SessionResponse(CurrentSessionState session, string message)
    {
        return Ok(message, new Dictionary<string, object>
        {
            { "hasSession", true },
            { "session", AdminSessionPayload(session) },
            { "playerPreview", PlayerSessionPayload(session) },
            { "warnings", BuildAdminWarnings(session).Cast<object>().ToArray() }
        });
    }

    private CurrentSessionState RequireSessionById(CommandContext context)
    {
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var sessionId = RequireLength(PayloadReader.GetString(payload, "sessionId"), 1, 128, "sessionId");
        var session = _repositories.CurrentSessions.Find(Builders<CurrentSessionState>.Filter.Eq(x => x.SessionId, sessionId)).FirstOrDefault()
            ?? _repositories.CurrentSessions.GetById(sessionId);
        if (session == null || session.Deleted || IsArchivedSession(session))
            throw new InvalidOperationException("current session not found");
        return session;
    }

    private CurrentSessionState? LoadSession(string campaignId, string sessionId)
    {
        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            return _repositories.CurrentSessions.Find(Builders<CurrentSessionState>.Filter.Eq(x => x.SessionId, sessionId)).FirstOrDefault()
                ?? _repositories.CurrentSessions.GetById(sessionId);
        }

        if (string.IsNullOrWhiteSpace(campaignId)) return null;
        return FindActiveSession(campaignId)
            ?? _repositories.CurrentSessions.Find(Builders<CurrentSessionState>.Filter.Eq(x => x.CampaignId, campaignId)
                & Builders<CurrentSessionState>.Filter.Eq(x => x.IsArchived, false)
                & Builders<CurrentSessionState>.Filter.Eq(x => x.Archived, false))
                .OrderByDescending(x => x.UpdatedAtUtc == default ? x.UpdatedUtc : x.UpdatedAtUtc)
                .FirstOrDefault();
    }

    private CurrentSessionState? FindActiveSession(string campaignId)
    {
        if (string.IsNullOrWhiteSpace(campaignId)) return null;
        return _repositories.CurrentSessions.Find(Builders<CurrentSessionState>.Filter.Eq(x => x.CampaignId, campaignId)
            & Builders<CurrentSessionState>.Filter.Eq(x => x.Status, CurrentSessionStatusIds.Active)
            & Builders<CurrentSessionState>.Filter.Eq(x => x.IsArchived, false)
            & Builders<CurrentSessionState>.Filter.Eq(x => x.Archived, false))
            .OrderByDescending(x => x.UpdatedAtUtc == default ? x.UpdatedUtc : x.UpdatedAtUtc)
            .FirstOrDefault();
    }

    private void SaveSession(CurrentSessionState session)
    {
        if (string.IsNullOrWhiteSpace(session.Id))
            session.Id = Guid.NewGuid().ToString("N");
        _repositories.CurrentSessions.Replace(session);
    }

    private void TouchSession(CurrentSessionState session, string userId)
    {
        session.UpdatedAtUtc = DateTime.UtcNow;
        session.UpdatedByUserId = userId ?? string.Empty;
    }

    private void SyncSceneActiveLink(CurrentSessionState session, MapCanvasState? map, string userId)
    {
        _repositories.SceneMapActiveLinks.DeactivateScopeAsync(session.CampaignId, session.SessionId, session.ActiveGroupId ?? string.Empty, session.CurrentSceneId ?? string.Empty).GetAwaiter().GetResult();
        if (map == null) return;
        _repositories.SceneMapActiveLinks.UpsertAsync(new SceneMapActiveLinkState
        {
            CampaignId = session.CampaignId,
            SessionId = session.SessionId,
            ActiveGroupId = session.ActiveGroupId ?? string.Empty,
            SceneId = session.CurrentSceneId ?? string.Empty,
            MapId = map.Id,
            MapName = map.Name ?? string.Empty,
            IsActive = true,
            VisibilityMode = map.VisibilityMode ?? MapVisibilityModes.Party,
            AssignedByUserId = userId,
            AssignedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        }).GetAwaiter().GetResult();
    }

    private Dictionary<string, object> AdminSessionPayload(CurrentSessionState session)
    {
        return new Dictionary<string, object>
        {
            { "id", session.Id },
            { "campaignId", session.CampaignId ?? string.Empty },
            { "sessionId", session.SessionId ?? string.Empty },
            { "name", session.Name ?? string.Empty },
            { "description", session.Description ?? string.Empty },
            { "status", session.Status ?? CurrentSessionStatusIds.Planned },
            { "mode", session.Mode ?? CurrentSessionModeIds.Preparation },
            { "currentSceneId", session.CurrentSceneId ?? string.Empty },
            { "currentSceneName", session.CurrentSceneName ?? string.Empty },
            { "activeSceneMapId", session.ActiveSceneMapId ?? string.Empty },
            { "activeSceneMapName", session.ActiveSceneMapName ?? string.Empty },
            { "activeWorldMapId", session.ActiveWorldMapId ?? string.Empty },
            { "activeWorldMapName", session.ActiveWorldMapName ?? string.Empty },
            { "activeRoomId", session.ActiveRoomId ?? string.Empty },
            { "activeRoomName", session.ActiveRoomName ?? string.Empty },
            { "activeCombatEncounterId", session.ActiveCombatEncounterId ?? string.Empty },
            { "activeCombatName", session.ActiveCombatName ?? string.Empty },
            { "activeGroupId", session.ActiveGroupId ?? string.Empty },
            { "activeGroupName", ResolveActiveGroupName(session.ActiveGroupId ?? string.Empty) },
            { "activeGroupMemberCount", CountVisibleGroupMembersForSession(session.ActiveGroupId ?? string.Empty, playerSafe: false) },
            { "currentWorldDate", session.CurrentWorldDate ?? string.Empty },
            { "gmUserId", session.GMUserId ?? string.Empty },
            { "gmDisplayName", session.GMDisplayName ?? string.Empty },
            { "visibilityMode", session.VisibilityMode ?? MapVisibilityModes.Party },
            { "isPlayerVisible", session.IsPlayerVisible },
            { "publicNotes", session.PublicNotes ?? string.Empty },
            { "gmNotes", session.GMNotes ?? string.Empty },
            { "startedAtUtc", session.StartedAtUtc ?? DateTime.MinValue },
            { "pausedAtUtc", session.PausedAtUtc ?? DateTime.MinValue },
            { "endedAtUtc", session.EndedAtUtc ?? DateTime.MinValue },
            { "updatedAtUtc", session.UpdatedAtUtc == default ? session.UpdatedUtc : session.UpdatedAtUtc },
            { "quickLinks", BuildQuickLinks(session).Cast<object>().ToArray() },
            { "diagnosticsSummary", BuildSessionDiagnostics(session) }
        };
    }

    private Dictionary<string, object> PlayerSessionPayload(CurrentSessionState session)
    {
        var result = new Dictionary<string, object>
        {
            { "sessionId", session.SessionId ?? string.Empty },
            { "name", session.Name ?? string.Empty },
            { "status", session.Status ?? CurrentSessionStatusIds.Planned },
            { "mode", session.Mode ?? CurrentSessionModeIds.Preparation },
            { "currentSceneName", session.CurrentSceneName ?? string.Empty },
            { "gmDisplayName", session.GMDisplayName ?? string.Empty },
            { "publicNotes", session.PublicNotes ?? string.Empty },
            { "hasActiveSceneMap", !string.IsNullOrWhiteSpace(session.ActiveSceneMapId) },
            { "activeSceneMapName", string.IsNullOrWhiteSpace(session.ActiveSceneMapId) ? string.Empty : session.ActiveSceneMapName ?? string.Empty },
            { "hasActiveWorldMap", !string.IsNullOrWhiteSpace(session.ActiveWorldMapId) },
            { "activeWorldMapName", string.IsNullOrWhiteSpace(session.ActiveWorldMapId) ? string.Empty : session.ActiveWorldMapName ?? string.Empty },
            { "hasActiveRoom", !string.IsNullOrWhiteSpace(session.ActiveRoomId) },
            { "activeRoomName", string.IsNullOrWhiteSpace(session.ActiveRoomId) ? string.Empty : session.ActiveRoomName ?? string.Empty },
            { "hasActiveCombat", !string.IsNullOrWhiteSpace(session.ActiveCombatEncounterId) },
            { "activeCombatSummary", string.IsNullOrWhiteSpace(session.ActiveCombatEncounterId) ? string.Empty : SessionFirstNonEmpty(session.ActiveCombatName, "Активный бой") },
            { "hasActiveGroup", !string.IsNullOrWhiteSpace(session.ActiveGroupId) },
            { "activeGroupName", ResolveActiveGroupName(session.ActiveGroupId ?? string.Empty) },
            { "activeGroupMemberCount", CountVisibleGroupMembersForSession(session.ActiveGroupId ?? string.Empty, playerSafe: true) },
            { "quickLinks", BuildPlayerQuickLinks(session).Cast<object>().ToArray() }
        };
        return result;
    }

    private IReadOnlyCollection<Dictionary<string, object>> BuildQuickLinks(CurrentSessionState session)
    {
        return new[]
        {
            Link("scene_map", "Открыть карту сцены", !string.IsNullOrWhiteSpace(session.ActiveSceneMapId), session.ActiveSceneMapId),
            Link("world_map", "Открыть карту мира", !string.IsNullOrWhiteSpace(session.ActiveWorldMapId), session.ActiveWorldMapId),
            Link("room", "Открыть помещение", !string.IsNullOrWhiteSpace(session.ActiveRoomId), session.ActiveRoomId),
            Link("combat", "Открыть бой", !string.IsNullOrWhiteSpace(session.ActiveCombatEncounterId), session.ActiveCombatEncounterId),
            Link("chat", "Открыть чат / кубики", true, session.SessionId),
            Link("requests", "Заявки игроков будут подключены в 0.14.4", false, string.Empty),
            Link("event_journal", "Журнал событий будет подключён в 0.14.8", false, string.Empty),
            Link("active_group", "Открыть активную группу", !string.IsNullOrWhiteSpace(session.ActiveGroupId), session.ActiveGroupId ?? string.Empty),
            Link("calendar", "Календарь будет добавлен в 0.14.5-0.14.6", false, string.Empty)
        };
    }

    private IReadOnlyCollection<Dictionary<string, object>> BuildPlayerQuickLinks(CurrentSessionState session)
    {
        return new[]
        {
            Link("scene_map", "Карта сцены", !string.IsNullOrWhiteSpace(session.ActiveSceneMapId), string.Empty),
            Link("world_map", "Карта мира", !string.IsNullOrWhiteSpace(session.ActiveWorldMapId), string.Empty),
            Link("combat", "Бой", !string.IsNullOrWhiteSpace(session.ActiveCombatEncounterId), string.Empty),
            Link("active_group", "Активная группа", !string.IsNullOrWhiteSpace(session.ActiveGroupId), string.Empty),
            Link("chat", "Чат", true, string.Empty),
            Link("requests", "Заявки / действия", true, string.Empty)
        };
    }

    private static Dictionary<string, object> Link(string key, string title, bool enabled, string targetId)
        => new Dictionary<string, object> { { "key", key }, { "title", title }, { "enabled", enabled }, { "targetId", targetId ?? string.Empty } };

    private IReadOnlyCollection<string> BuildAdminWarnings(CurrentSessionState? session)
    {
        var warnings = new List<string>();
        if (session == null)
        {
            warnings.Add("Сессия не создана.");
            return warnings;
        }
        if (string.IsNullOrWhiteSpace(session.ActiveSceneMapId)) warnings.Add("Активная карта сцены не выбрана.");
        if (string.IsNullOrWhiteSpace(session.ActiveGroupId)) warnings.Add("Активная группа не выбрана.");
        if (string.IsNullOrWhiteSpace(session.ActiveCombatEncounterId)) warnings.Add("Активного боя нет.");
        if (string.IsNullOrWhiteSpace(session.CurrentWorldDate)) warnings.Add("Календарь будет добавлен в 0.14.5-0.14.6.");
        return warnings;
    }

    private string BuildSessionDiagnostics(CurrentSessionState session)
    {
        return $"links: sceneMap={HasValue(session.ActiveSceneMapId)}, worldMap={HasValue(session.ActiveWorldMapId)}, room={HasValue(session.ActiveRoomId)}, group={HasValue(session.ActiveGroupId)}, combat={HasValue(session.ActiveCombatEncounterId)}";
    }

    private static string HasValue(string value) => string.IsNullOrWhiteSpace(value) ? "no" : "yes";

    private string ResolveRoomName(string roomId)
    {
        if (string.IsNullOrWhiteSpace(roomId)) return string.Empty;
        var room = _repositories.RoomInteriors.GetByIdAsync(roomId).GetAwaiter().GetResult();
        return room?.Name ?? string.Empty;
    }

    private ResponseEnvelope CurrentSessionDisabled(string commandName)
    {
        _logger.Admin($"session.current.disabled command={commandName}");
        return Error("current session endpoints disabled by feature flags", ResponseStatus.Forbidden, ErrorCode.Forbidden);
    }

    private bool CurrentSessionReadEnabled()
        => _featureFlags.IsEnabled(nameof(SessionFeatureFlags.UseCurrentSessionMvp))
            && _featureFlags.IsEnabled(nameof(SessionFeatureFlags.UseSessionStateV1));

    private bool CurrentSessionWriteEnabled() => CurrentSessionReadEnabled();

    private bool CurrentSessionSceneLinkEnabled()
        => CurrentSessionReadEnabled() && _featureFlags.IsEnabled(nameof(SessionFeatureFlags.UseSessionSceneLink));

    private bool CurrentSessionMapLinkEnabled()
        => CurrentSessionReadEnabled() && _featureFlags.IsEnabled(nameof(SessionFeatureFlags.UseSessionMapLink));

    private bool CurrentSessionPlayerViewEnabled()
        => CurrentSessionReadEnabled() && _featureFlags.IsEnabled(nameof(SessionFeatureFlags.UseSessionPlayerView));

    private static bool IsTerminalSession(CurrentSessionState session)
        => string.Equals(session.Status, CurrentSessionStatusIds.Completed, StringComparison.OrdinalIgnoreCase)
            || string.Equals(session.Status, CurrentSessionStatusIds.Cancelled, StringComparison.OrdinalIgnoreCase)
            || string.Equals(session.Status, CurrentSessionStatusIds.Archived, StringComparison.OrdinalIgnoreCase);

    private static bool IsArchivedSession(CurrentSessionState session)
        => session.Archived || session.IsArchived || string.Equals(session.Status, CurrentSessionStatusIds.Archived, StringComparison.OrdinalIgnoreCase);

    private static bool IsSessionVisibleForPlayer(CurrentSessionState session)
    {
        if (!session.IsPlayerVisible) return false;
        return !string.Equals(session.VisibilityMode, MapVisibilityModes.GmOnly, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(session.VisibilityMode, MapVisibilityModes.Hidden, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeSessionStatus(string? value)
    {
        var input = (value ?? string.Empty).Trim().ToLowerInvariant();
        return input switch
        {
            CurrentSessionStatusIds.Planned => CurrentSessionStatusIds.Planned,
            CurrentSessionStatusIds.Active => CurrentSessionStatusIds.Active,
            CurrentSessionStatusIds.Paused => CurrentSessionStatusIds.Paused,
            CurrentSessionStatusIds.Completed => CurrentSessionStatusIds.Completed,
            CurrentSessionStatusIds.Cancelled => CurrentSessionStatusIds.Cancelled,
            CurrentSessionStatusIds.Archived => CurrentSessionStatusIds.Archived,
            _ => CurrentSessionStatusIds.Planned
        };
    }

    private static string NormalizeSessionMode(string? value)
    {
        var input = (value ?? string.Empty).Trim().ToLowerInvariant();
        return input switch
        {
            CurrentSessionModeIds.Preparation => CurrentSessionModeIds.Preparation,
            CurrentSessionModeIds.NormalScene => CurrentSessionModeIds.NormalScene,
            CurrentSessionModeIds.Combat => CurrentSessionModeIds.Combat,
            CurrentSessionModeIds.Travel => CurrentSessionModeIds.Travel,
            CurrentSessionModeIds.ShortRest => CurrentSessionModeIds.ShortRest,
            CurrentSessionModeIds.LongRest => CurrentSessionModeIds.LongRest,
            CurrentSessionModeIds.Downtime => CurrentSessionModeIds.Downtime,
            CurrentSessionModeIds.Maintenance => CurrentSessionModeIds.Maintenance,
            CurrentSessionModeIds.Custom => CurrentSessionModeIds.Custom,
            _ => CurrentSessionModeIds.NormalScene
        };
    }

    private static string NormalizeSessionVisibility(string? value)
    {
        var input = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (string.Equals(input, MapVisibilityModes.Public, StringComparison.OrdinalIgnoreCase)) return MapVisibilityModes.Public;
        if (string.Equals(input, MapVisibilityModes.Party, StringComparison.OrdinalIgnoreCase)) return MapVisibilityModes.Party;
        if (string.Equals(input, MapVisibilityModes.Hidden, StringComparison.OrdinalIgnoreCase)) return MapVisibilityModes.Hidden;
        if (string.Equals(input, MapVisibilityModes.GmOnly, StringComparison.OrdinalIgnoreCase)) return MapVisibilityModes.GmOnly;
        return MapVisibilityModes.Party;
    }

    private static DateTime? ParseDateTime(IDictionary<string, object> payload, string key)
    {
        var text = PayloadReader.GetString(payload, key);
        if (string.IsNullOrWhiteSpace(text)) return null;
        return DateTime.TryParse(text, out var value) ? value.ToUniversalTime() : null;
    }

    private static string SessionFirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
        }
        return string.Empty;
    }
}
