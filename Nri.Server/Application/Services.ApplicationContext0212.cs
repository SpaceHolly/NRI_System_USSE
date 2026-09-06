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
    public ResponseEnvelope ContextCurrentGet(CommandContext context)
        => GameContextGet02110(context);

    public ResponseEnvelope ContextCharacterSwitch(CommandContext context)
        => GameContextSelectCharacter02110(context);

    public ResponseEnvelope CharacterProfileMigrateMissing(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var characterId = RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 8, 128, "characterId");
        var character = GetCharacter(characterId);
        var result = _characterProfileCreationService.MigrateMissingProfilesAsync(character, actor.Id, context.Request.RequestId ?? string.Empty).GetAwaiter().GetResult();
        WriteAudit("profile_migration", actor.Id, "migrateMissing", $"{characterId}:{result.Success}:{string.Join(",", result.CreatedProfiles)}");
        return result.Success
            ? Ok("Отсутствующие профили персонажа восстановлены.", new Dictionary<string, object>
            {
                ["characterId"] = characterId,
                ["createdProfiles"] = result.CreatedProfiles.Cast<object>().ToArray(),
                ["idempotent"] = result.CreatedProfiles.Count == 0
            })
            : Error("Не удалось восстановить все обязательные профили персонажа.", ResponseStatus.Conflict, ErrorCode.Conflict);
    }

    private SessionUserState GetOrCreateContextPresence(string userId)
    {
        var presence = _repositories.Presence.Find(
            Builders<SessionUserState>.Filter.Eq(x => x.UserId, userId)).FirstOrDefault();
        if (presence != null) return presence;
        presence = new SessionUserState { UserId = userId, ContextRevision = 0 };
        _repositories.Presence.Insert(presence);
        return presence;
    }

    private ApplicationContextSnapshot BuildApplicationContextSnapshot(UserAccount actor, SessionUserState presence)
    {
        var ownership = string.IsNullOrWhiteSpace(presence.ActiveCharacterId)
            ? null
            : _repositories.CharacterOwnerships.Find(
                Builders<CharacterOwnershipState>.Filter.Eq(x => x.CharacterId, presence.ActiveCharacterId)).FirstOrDefault();
        var character = string.IsNullOrWhiteSpace(presence.ActiveCharacterId)
            ? null
            : _repositories.Characters.GetById(presence.ActiveCharacterId);
        var campaignId = ownership?.CampaignId ?? string.Empty;
        var session = ResolveContextSession(presence, campaignId);
        if (string.IsNullOrWhiteSpace(campaignId)) campaignId = session?.CampaignId ?? string.Empty;

        var snapshot = new ApplicationContextSnapshot
        {
            Account = Ref0212(actor.Id, actor.Login),
            Role = HighestRole0212(actor),
            Campaign = Ref0212(campaignId, string.IsNullOrWhiteSpace(campaignId) ? string.Empty : "Текущая кампания"),
            Session = Ref0212(session?.SessionId, session?.Name),
            ActiveCharacter = Ref0212(character?.Id, FirstNonEmpty(ownership?.CharacterDisplayName, character?.Name)),
            ActiveScene = Ref0212(session?.CurrentSceneId, session?.CurrentSceneName),
            ActiveMap = Ref0212(
                FirstNonEmpty(session?.ActiveRoomId, session?.ActiveSceneMapId, session?.ActiveWorldMapId),
                FirstNonEmpty(session?.ActiveRoomName, session?.ActiveSceneMapName, session?.ActiveWorldMapName)),
            ActiveCombat = Ref0212(session?.ActiveCombatEncounterId, session?.ActiveCombatName),
            ContextRevision = Math.Max(0, presence.ContextRevision),
            ServerUtc = DateTime.UtcNow,
            State = string.IsNullOrWhiteSpace(presence.ActiveCharacterId) ? ApplicationContextStates.NoCharacter : ApplicationContextStates.Ready,
            StateMessage = string.IsNullOrWhiteSpace(presence.ActiveCharacterId) ? "Активный персонаж не выбран." : string.Empty,
            Modules = BuildContextModuleAvailability0212()
        };

        if (character != null && !_characterDetailsProfileBuilder.CanBuildFromProfilesAsync(character.Id).GetAwaiter().GetResult())
        {
            snapshot.State = ApplicationContextStates.ProfileMigrationRequired;
            snapshot.StateMessage = actor.Roles.Contains(UserRole.Admin) || actor.Roles.Contains(UserRole.SuperAdmin)
                ? "Профили персонажа требуют явной миграции или восстановления."
                : "Данные персонажа временно недоступны. Обратитесь к мастеру.";
        }

        return snapshot;
    }

    private CurrentSessionState? ResolveContextSession(SessionUserState presence, string campaignId)
    {
        if (!string.IsNullOrWhiteSpace(presence.CurrentGameSessionId))
        {
            var byId = _repositories.CurrentSessions.Find(
                Builders<CurrentSessionState>.Filter.Eq(x => x.SessionId, presence.CurrentGameSessionId)
                & Builders<CurrentSessionState>.Filter.Eq(x => x.IsArchived, false)
                & Builders<CurrentSessionState>.Filter.Eq(x => x.Archived, false)).FirstOrDefault();
            if (byId != null) return byId;
        }

        if (string.IsNullOrWhiteSpace(campaignId)) return null;
        return _repositories.CurrentSessions.Find(
                Builders<CurrentSessionState>.Filter.Eq(x => x.CampaignId, campaignId)
                & Builders<CurrentSessionState>.Filter.Eq(x => x.Status, CurrentSessionStatusIds.Active)
                & Builders<CurrentSessionState>.Filter.Eq(x => x.IsArchived, false)
                & Builders<CurrentSessionState>.Filter.Eq(x => x.Archived, false))
            .OrderByDescending(x => x.UpdatedAtUtc)
            .FirstOrDefault();
    }

    private List<ApplicationModuleAvailability> BuildContextModuleAvailability0212()
    {
        return new List<ApplicationModuleAvailability>
        {
            Module0212("character", true, string.Empty),
            Module0212("scene_map", _featureFlags.IsEnabled(nameof(MapFeatureFlags.UseSceneMapV1)), "Карта сцены выключена в текущем профиле функций."),
            Module0212("world_map", _featureFlags.IsEnabled(nameof(MapFeatureFlags.UseWorldMapV1)), "Карта мира выключена в текущем профиле функций."),
            Module0212("combat", _featureFlags.IsEnabled(nameof(CombatFeatureFlags.UseCombatSystemV1)), "Боевой модуль выключен в текущем профиле функций."),
            Module0212("current_session", _featureFlags.IsEnabled(nameof(SessionFeatureFlags.UseCurrentSessionMvp)), "Текущая сессия выключена в текущем профиле функций.")
        };
    }

    private static ApplicationModuleAvailability Module0212(string key, bool available, string disabledReason)
        => new ApplicationModuleAvailability { ModuleKey = key, IsAvailable = available, Reason = available ? string.Empty : disabledReason };

    private static ApplicationContextReference Ref0212(string? id, string? name)
        => new ApplicationContextReference { Id = id ?? string.Empty, DisplayName = name ?? string.Empty };

    private static string HighestRole0212(UserAccount actor)
    {
        if (actor.Roles.Contains(UserRole.SuperAdmin)) return UserRole.SuperAdmin.ToString();
        if (actor.Roles.Contains(UserRole.Admin)) return UserRole.Admin.ToString();
        if (actor.Roles.Contains(UserRole.Observer)) return UserRole.Observer.ToString();
        return UserRole.Player.ToString();
    }

    private static Dictionary<string, object> ApplicationContextPayload(ApplicationContextSnapshot snapshot)
    {
        return new Dictionary<string, object>
        {
            ["contextRevision"] = snapshot.ContextRevision,
            ["serverUtc"] = snapshot.ServerUtc,
            ["state"] = snapshot.State,
            ["stateMessage"] = snapshot.StateMessage,
            ["account"] = ReferencePayload0212(snapshot.Account),
            ["role"] = snapshot.Role,
            ["campaign"] = ReferencePayload0212(snapshot.Campaign),
            ["session"] = ReferencePayload0212(snapshot.Session),
            ["world"] = ReferencePayload0212(snapshot.World),
            ["activeCharacter"] = ReferencePayload0212(snapshot.ActiveCharacter),
            ["activeScene"] = ReferencePayload0212(snapshot.ActiveScene),
            ["activeMap"] = ReferencePayload0212(snapshot.ActiveMap),
            ["activeCombat"] = ReferencePayload0212(snapshot.ActiveCombat),
            ["missingProfileSections"] = snapshot.MissingProfileSections.Cast<object>().ToArray(),
            ["modules"] = snapshot.Modules.Select(x => (object)new Dictionary<string, object>
            {
                ["moduleKey"] = x.ModuleKey,
                ["isAvailable"] = x.IsAvailable,
                ["reason"] = x.Reason
            }).ToArray()
        };
    }

    private static Dictionary<string, object> ReferencePayload0212(ApplicationContextReference reference)
        => new Dictionary<string, object> { ["id"] = reference.Id, ["displayName"] = reference.DisplayName };

    private static ResponseEnvelope ContextConflict(string message, long currentRevision)
        => new ResponseEnvelope
        {
            Status = ResponseStatus.Conflict,
            ErrorCode = ErrorCode.Conflict,
            Message = message,
            Payload = new Dictionary<string, object> { ["currentContextRevision"] = currentRevision }
        };

    private static long ReadLong0212(Dictionary<string, object> payload, string key, long fallback)
    {
        if (payload == null || !payload.TryGetValue(key, out var value) || value == null) return fallback;
        try { return Convert.ToInt64(value); }
        catch { return fallback; }
    }
}
