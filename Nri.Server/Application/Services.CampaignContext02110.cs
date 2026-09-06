using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Driver;
using Nri.Server.Infrastructure;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;
using Nri.Shared.Utilities;

namespace Nri.Server.Application;

public partial class ServiceHub
{
    public ResponseEnvelope GameContextGet02110(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        RevalidateContext02110(context.Session!, actor);
        return Ok("Игровой контекст загружен.", GameContextPayload02110(context.Session!, actor));
    }

    public ResponseEnvelope GameContextCampaignsList02110(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var memberships = _repositories.CampaignMemberships.Find(
            Builders<CampaignMembership>.Filter.Eq(x => x.UserId, actor.Id)
            & Builders<CampaignMembership>.Filter.Eq(x => x.Status, CampaignMembershipStatusIds.Active)
            & Builders<CampaignMembership>.Filter.Eq(x => x.IsArchived, false)
            & Builders<CampaignMembership>.Filter.Eq(x => x.Archived, false));
        var items = memberships.Select(m =>
        {
            var campaign = _repositories.Campaigns.GetById(m.CampaignId);
            if (campaign == null || campaign.Archived || campaign.Deleted) return null;
            var activeSession = _repositories.CurrentSessions.Find(
                Builders<CurrentSessionState>.Filter.Eq(x => x.CampaignId, campaign.Id)
                & Builders<CurrentSessionState>.Filter.Eq(x => x.Status, CurrentSessionStatusIds.Active)
                & Builders<CurrentSessionState>.Filter.Eq(x => x.IsArchived, false)).FirstOrDefault();
            return (object)new Dictionary<string, object>
            {
                ["campaignId"] = campaign.Id,
                ["name"] = campaign.Name,
                ["role"] = CampaignRoleDisplay02110(m.PrimaryRoleId),
                ["activeSessionName"] = activeSession?.Name ?? string.Empty,
                ["hasActiveSession"] = activeSession != null,
                ["isCurrent"] = string.Equals(context.Session!.GameContext.CampaignId, campaign.Id, StringComparison.Ordinal)
            };
        }).Where(x => x != null).Cast<object>().ToArray();
        return Ok("Доступные кампании загружены.", new Dictionary<string, object> { ["campaigns"] = items, ["count"] = items.Length });
    }

    public ResponseEnvelope GameContextSelectCampaign02110(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var campaignId = RequireLength(PayloadReader.GetString(context.Request.Payload, "campaignId"), 1, 128, "campaignId");
        RequireExpectedContextRevision02110(context);
        _campaignAuthorization.RequireCampaignCapability(context.Session!, campaignId, CampaignCapabilityIds.CampaignView);
        var campaign = _repositories.Campaigns.GetById(campaignId);
        if (campaign == null || campaign.Deleted || campaign.Archived) throw new KeyNotFoundException("Campaign not found.");

        var game = context.Session!.GameContext;
        game.CampaignId = campaignId;
        game.SessionId = string.Empty;
        game.ActiveCharacterId = string.Empty;
        game.SuperAdminOverrideActive = false;
        game.SuperAdminOverrideReason = string.Empty;
        TouchContext02110(game);
        SaveContextPreference02110(game);
        WriteAudit("game_context", actor.Id, "campaign.select", campaignId);
        return Ok("Кампания выбрана.", GameContextPayload02110(context.Session, actor));
    }

    public ResponseEnvelope GameContextSessionsList02110(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var campaignId = ResolveRequestedCampaign02110(context);
        _campaignAuthorization.RequireCampaignCapability(context.Session!, campaignId, CampaignCapabilityIds.SessionView);
        var canSeeGm = _campaignAuthorization.CanViewGMData(context.Session!, campaignId);
        var participationIds = _repositories.SessionParticipations.Find(
            Builders<SessionParticipation>.Filter.Eq(x => x.CampaignId, campaignId)
            & Builders<SessionParticipation>.Filter.Eq(x => x.UserId, actor.Id)
            & Builders<SessionParticipation>.Filter.Eq(x => x.Status, CampaignMembershipStatusIds.Active))
            .Select(x => x.SessionId).ToHashSet(StringComparer.Ordinal);
        var sessions = _repositories.CurrentSessions.Find(
            Builders<CurrentSessionState>.Filter.Eq(x => x.CampaignId, campaignId)
            & Builders<CurrentSessionState>.Filter.Eq(x => x.IsArchived, false)
            & Builders<CurrentSessionState>.Filter.Eq(x => x.Archived, false))
            .Where(x => canSeeGm || (x.IsPlayerVisible && participationIds.Contains(x.SessionId)))
            .OrderByDescending(x => x.UpdatedAtUtc)
            .Select(x => (object)new Dictionary<string, object>
            {
                ["sessionId"] = x.SessionId,
                ["name"] = x.Name,
                ["status"] = SessionStatusDisplay02110(x.Status),
                ["mode"] = SessionModeDisplay02110(x.Mode),
                ["leadGM"] = FirstNonEmpty(x.LeadGMDisplayName, x.GMDisplayName),
                ["isCurrent"] = string.Equals(context.Session!.GameContext.SessionId, x.SessionId, StringComparison.Ordinal)
            }).ToArray();
        return Ok("Сессии загружены.", new Dictionary<string, object> { ["sessions"] = sessions, ["count"] = sessions.Length });
    }

    public ResponseEnvelope GameContextSelectSession02110(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        RequireExpectedContextRevision02110(context);
        var sessionId = RequireLength(PayloadReader.GetString(context.Request.Payload, "sessionId"), 1, 128, "sessionId");
        var session = _repositories.CurrentSessions.Find(Builders<CurrentSessionState>.Filter.Eq(x => x.SessionId, sessionId)).FirstOrDefault();
        if (session == null || session.Archived || session.IsArchived) throw new KeyNotFoundException("Session not found.");
        if (!string.Equals(context.Session!.GameContext.CampaignId, session.CampaignId, StringComparison.Ordinal))
            throw new KeyNotFoundException("Session not found.");
        _campaignAuthorization.RequireSessionCapability(context.Session, session, CampaignCapabilityIds.SessionView);
        context.Session.GameContext.SessionId = session.SessionId;
        context.Session.GameContext.ActiveCharacterId = string.Empty;
        TouchContext02110(context.Session.GameContext);
        SaveContextPreference02110(context.Session.GameContext);
        WriteAudit("game_context", actor.Id, "session.select", session.SessionId);
        return Ok("Сессия выбрана.", GameContextPayload02110(context.Session, actor));
    }

    public ResponseEnvelope GameContextCharactersListEligible02110(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var game = context.Session!.GameContext;
        if (string.IsNullOrWhiteSpace(game.CampaignId)) return Ok("Сначала выберите кампанию.", new Dictionary<string, object> { ["characters"] = Array.Empty<object>(), ["count"] = 0 });
        _campaignAuthorization.RequireCampaignCapability(context.Session, game.CampaignId, CampaignCapabilityIds.CharacterViewPlayerSafe);
        var canManageAny = _campaignAuthorization.GetEffectiveCapabilities(actor.Id, game.CampaignId).Contains(CampaignCapabilityIds.CharacterManageAnyInCampaign);
        var allowed = string.IsNullOrWhiteSpace(game.SessionId) ? null : _repositories.SessionParticipations.Find(
            Builders<SessionParticipation>.Filter.Eq(x => x.SessionId, game.SessionId)
            & Builders<SessionParticipation>.Filter.Eq(x => x.UserId, actor.Id)
            & Builders<SessionParticipation>.Filter.Eq(x => x.Status, CampaignMembershipStatusIds.Active)).FirstOrDefault();
        var items = _repositories.CharacterOwnerships.Find(
            Builders<CharacterOwnershipState>.Filter.Eq(x => x.CampaignId, game.CampaignId)
            & Builders<CharacterOwnershipState>.Filter.Eq(x => x.IsArchived, false))
            .Where(x => canManageAny || string.Equals(x.OwnerUserId, actor.Id, StringComparison.Ordinal) || string.Equals(x.ControlledByUserId, actor.Id, StringComparison.Ordinal))
            .Where(x => allowed == null || allowed.AllowedCharacterIds.Count == 0 || allowed.AllowedCharacterIds.Contains(x.CharacterId))
            .Select(x =>
            {
                var eligible = IsInitialDevelopmentEligibleForSession02112(x.CharacterId);
                return (object)new Dictionary<string, object>
                {
                    ["characterId"] = x.CharacterId,
                    ["name"] = x.CharacterDisplayName,
                    ["isCurrent"] = string.Equals(game.ActiveCharacterId, x.CharacterId, StringComparison.Ordinal),
                    ["isEligible"] = eligible,
                    ["eligibilityReason"] = eligible ? string.Empty : "Завершите начальное развитие персонажа перед участием в сессии."
                };
            }).ToArray();
        return Ok("Доступные персонажи загружены.", new Dictionary<string, object> { ["characters"] = items, ["count"] = items.Length });
    }

    public ResponseEnvelope GameContextSelectCharacter02110(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        RequireExpectedContextRevision02110(context);
        var characterId = RequireLength(PayloadReader.GetString(context.Request.Payload, "characterId"), 1, 128, "characterId");
        var game = context.Session!.GameContext;
        var ownership = _repositories.CharacterOwnerships.Find(Builders<CharacterOwnershipState>.Filter.Eq(x => x.CharacterId, characterId)).FirstOrDefault();
        if (ownership == null || ownership.IsArchived || !string.Equals(ownership.CampaignId, game.CampaignId, StringComparison.Ordinal))
            throw new KeyNotFoundException("Character not found.");
        var capabilities = _campaignAuthorization.GetEffectiveCapabilities(actor.Id, game.CampaignId);
        if (!capabilities.Contains(CampaignCapabilityIds.CharacterManageAnyInCampaign)
            && !string.Equals(ownership.OwnerUserId, actor.Id, StringComparison.Ordinal)
            && !string.Equals(ownership.ControlledByUserId, actor.Id, StringComparison.Ordinal))
            throw new KeyNotFoundException("Character not found.");
        if (!IsInitialDevelopmentEligibleForSession02112(characterId))
            return Error("Завершите начальное развитие персонажа перед участием в сессии.", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);
        if (!string.IsNullOrWhiteSpace(game.SessionId))
        {
            var participation = _repositories.SessionParticipations.Find(
                Builders<SessionParticipation>.Filter.Eq(x => x.SessionId, game.SessionId)
                & Builders<SessionParticipation>.Filter.Eq(x => x.UserId, actor.Id)
                & Builders<SessionParticipation>.Filter.Eq(x => x.Status, CampaignMembershipStatusIds.Active)).FirstOrDefault();
            if (participation != null && participation.AllowedCharacterIds.Count > 0 && !participation.AllowedCharacterIds.Contains(characterId))
                throw new KeyNotFoundException("Character not found.");
        }
        game.ActiveCharacterId = characterId;
        TouchContext02110(game);
        SaveContextPreference02110(game);
        WriteAudit("game_context", actor.Id, "character.select", characterId);
        return Ok("Персонаж выбран.", GameContextPayload02110(context.Session, actor));
    }

    public ResponseEnvelope GameContextClearSession02110(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        RequireExpectedContextRevision02110(context);
        context.Session!.GameContext.SessionId = string.Empty;
        context.Session.GameContext.ActiveCharacterId = string.Empty;
        TouchContext02110(context.Session.GameContext);
        SaveContextPreference02110(context.Session.GameContext);
        return Ok("Сессия очищена.", GameContextPayload02110(context.Session, actor));
    }

    public ResponseEnvelope GameContextClearCharacter02110(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        RequireExpectedContextRevision02110(context);
        context.Session!.GameContext.ActiveCharacterId = string.Empty;
        TouchContext02110(context.Session.GameContext);
        SaveContextPreference02110(context.Session.GameContext);
        return Ok("Персонаж очищен.", GameContextPayload02110(context.Session, actor));
    }

    public ResponseEnvelope GameContextRestoreLast02110(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var preference = _repositories.ActiveGameContextPreferences.Find(Builders<ActiveGameContextPreference>.Filter.Eq(x => x.UserId, actor.Id)).FirstOrDefault();
        if (preference == null || !_campaignAuthorization.CanAccessCampaign(context.Session!, preference.CampaignId))
            return Ok("Сохранённый контекст недоступен.", GameContextPayload02110(context.Session!, actor));
        var game = context.Session!.GameContext;
        game.CampaignId = preference.CampaignId;
        game.SessionId = string.Empty;
        game.ActiveCharacterId = string.Empty;
        var session = _repositories.CurrentSessions.Find(
            Builders<CurrentSessionState>.Filter.Eq(x => x.SessionId, preference.SessionId)
            & Builders<CurrentSessionState>.Filter.Eq(x => x.CampaignId, preference.CampaignId)
            & Builders<CurrentSessionState>.Filter.Eq(x => x.IsArchived, false)).FirstOrDefault();
        if (session != null)
        {
            try { _campaignAuthorization.RequireSessionCapability(context.Session, session, CampaignCapabilityIds.SessionView); game.SessionId = session.SessionId; }
            catch (UnauthorizedAccessException) { }
        }
        if (!string.IsNullOrWhiteSpace(preference.CharacterId))
        {
            var ownership = _repositories.CharacterOwnerships.Find(
                Builders<CharacterOwnershipState>.Filter.Eq(x => x.CharacterId, preference.CharacterId)
                & Builders<CharacterOwnershipState>.Filter.Eq(x => x.CampaignId, preference.CampaignId)
                & Builders<CharacterOwnershipState>.Filter.Eq(x => x.IsArchived, false)).FirstOrDefault();
            if (ownership != null)
            {
                var capabilities = _campaignAuthorization.GetEffectiveCapabilities(actor.Id, preference.CampaignId);
                var ownsCharacter = capabilities.Contains(CampaignCapabilityIds.CharacterManageAnyInCampaign)
                    || string.Equals(ownership.OwnerUserId, actor.Id, StringComparison.Ordinal)
                    || string.Equals(ownership.ControlledByUserId, actor.Id, StringComparison.Ordinal);
                var sessionAllowsCharacter = true;
                if (ownsCharacter && !string.IsNullOrWhiteSpace(game.SessionId))
                {
                    var participation = _repositories.SessionParticipations.Find(
                        Builders<SessionParticipation>.Filter.Eq(x => x.SessionId, game.SessionId)
                        & Builders<SessionParticipation>.Filter.Eq(x => x.UserId, actor.Id)
                        & Builders<SessionParticipation>.Filter.Eq(x => x.Status, CampaignMembershipStatusIds.Active)).FirstOrDefault();
                    sessionAllowsCharacter = participation == null
                        || participation.AllowedCharacterIds.Count == 0
                        || participation.AllowedCharacterIds.Contains(ownership.CharacterId);
                }
                if (ownsCharacter && sessionAllowsCharacter && IsInitialDevelopmentEligibleForSession02112(ownership.CharacterId))
                    game.ActiveCharacterId = ownership.CharacterId;
            }
        }
        TouchContext02110(game);
        return Ok("Игровой контекст восстановлен.", GameContextPayload02110(context.Session, actor));
    }

    public ResponseEnvelope GameContextSuperAdminOverrideStart02110(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        RoleGuard.EnsureRole(actor, UserRole.SuperAdmin);
        RequireExpectedContextRevision02110(context);
        var campaignId = RequireLength(PayloadReader.GetString(context.Request.Payload, "campaignId"), 1, 128, "campaignId");
        var reason = RequireLength(PayloadReader.GetString(context.Request.Payload, "reason"), 5, 500, "reason");
        if (_repositories.Campaigns.GetById(campaignId) == null) throw new KeyNotFoundException("Campaign not found.");
        var game = context.Session!.GameContext;
        game.CampaignId = campaignId;
        game.SessionId = string.Empty;
        game.ActiveCharacterId = string.Empty;
        game.SuperAdminOverrideActive = true;
        game.SuperAdminOverrideReason = reason;
        TouchContext02110(game);
        WriteAudit("campaign_override", actor.Id, "start", $"{campaignId}:{reason}");
        return Ok("Режим SuperAdmin включён.", GameContextPayload02110(context.Session, actor));
    }

    public ResponseEnvelope GameContextSuperAdminCampaignsList02110(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        RoleGuard.EnsureRole(actor, UserRole.SuperAdmin);
        var items = _repositories.Campaigns.Find(Builders<Campaign>.Filter.Eq(x => x.Archived, false))
            .Where(x => !x.Deleted)
            .OrderBy(x => x.Name)
            .Select(x => (object)new Dictionary<string, object>
            {
                ["campaignId"] = x.Id,
                ["name"] = FirstNonEmpty(x.Name, "Кампания без названия")
            }).ToArray();
        return Ok("Кампании для явного доступа загружены.", new Dictionary<string, object> { ["campaigns"] = items, ["count"] = items.Length });
    }

    public ResponseEnvelope GameContextSuperAdminOverrideEnd02110(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        RoleGuard.EnsureRole(actor, UserRole.SuperAdmin);
        var campaignId = context.Session!.GameContext.CampaignId;
        context.Session.GameContext = new ActiveGameContext
        {
            AuthSessionId = context.Session.Token,
            ConnectionId = context.Session.ConnectionId,
            UserId = actor.Id,
            ContextRevision = context.Session.GameContext.ContextRevision + 1
        };
        WriteAudit("campaign_override", actor.Id, "end", campaignId);
        return Ok("Режим SuperAdmin выключен.", GameContextPayload02110(context.Session, actor));
    }

    private void RequireExpectedContextRevision02110(CommandContext context)
    {
        var expected = ReadLong0212(context.Request.Payload ?? new Dictionary<string, object>(), "expectedContextRevision", context.Session!.GameContext.ContextRevision);
        if (expected != context.Session!.GameContext.ContextRevision)
            throw new InvalidOperationException($"Игровой контекст изменился. Текущая ревизия: {context.Session.GameContext.ContextRevision}.");
    }

    private void RevalidateContext02110(AuthSession session, UserAccount actor)
    {
        var game = session.GameContext;
        if (string.IsNullOrWhiteSpace(game.CampaignId)) return;
        if (!_campaignAuthorization.CanAccessCampaign(session, game.CampaignId))
        {
            _sessionManager.InvalidateCampaignContexts(game.CampaignId, actor.Id);
            return;
        }
        if (!string.IsNullOrWhiteSpace(game.SessionId))
        {
            var current = _repositories.CurrentSessions.Find(
                Builders<CurrentSessionState>.Filter.Eq(x => x.SessionId, game.SessionId)
                & Builders<CurrentSessionState>.Filter.Eq(x => x.CampaignId, game.CampaignId)
                & Builders<CurrentSessionState>.Filter.Eq(x => x.IsArchived, false)).FirstOrDefault();
            if (current == null) { game.SessionId = string.Empty; game.ActiveCharacterId = string.Empty; game.ContextRevision++; }
        }
        game.LastValidatedAtUtc = DateTime.UtcNow;
    }

    private string ResolveRequestedCampaign02110(CommandContext context)
    {
        var requested = PayloadReader.GetString(context.Request.Payload, "campaignId");
        var campaignId = FirstNonEmpty(requested, context.Session!.GameContext.CampaignId);
        return RequireLength(campaignId, 1, 128, "campaignId");
    }

    private void TouchContext02110(ActiveGameContext game)
    {
        game.ContextRevision = Math.Max(1, game.ContextRevision + 1);
        game.SelectedAtUtc = DateTime.UtcNow;
        game.LastValidatedAtUtc = game.SelectedAtUtc;
    }

    private void SaveContextPreference02110(ActiveGameContext game)
    {
        if (string.IsNullOrWhiteSpace(game.CampaignId) || game.SuperAdminOverrideActive) return;
        var existing = _repositories.ActiveGameContextPreferences.Find(Builders<ActiveGameContextPreference>.Filter.Eq(x => x.UserId, game.UserId)).FirstOrDefault();
        if (existing == null)
        {
            _repositories.ActiveGameContextPreferences.Insert(new ActiveGameContextPreference { UserId = game.UserId, CampaignId = game.CampaignId, SessionId = game.SessionId, CharacterId = game.ActiveCharacterId });
            return;
        }
        existing.CampaignId = game.CampaignId;
        existing.SessionId = game.SessionId;
        existing.CharacterId = game.ActiveCharacterId;
        existing.LastUsedAtUtc = DateTime.UtcNow;
        _repositories.ActiveGameContextPreferences.Replace(existing);
    }

    private Dictionary<string, object> GameContextPayload02110(AuthSession session, UserAccount actor)
    {
        var game = session.GameContext;
        var campaign = string.IsNullOrWhiteSpace(game.CampaignId) ? null : _repositories.Campaigns.GetById(game.CampaignId);
        var current = string.IsNullOrWhiteSpace(game.SessionId) ? null : _repositories.CurrentSessions.Find(Builders<CurrentSessionState>.Filter.Eq(x => x.SessionId, game.SessionId)).FirstOrDefault();
        var ownership = string.IsNullOrWhiteSpace(game.ActiveCharacterId) ? null : _repositories.CharacterOwnerships.Find(Builders<CharacterOwnershipState>.Filter.Eq(x => x.CharacterId, game.ActiveCharacterId)).FirstOrDefault();
        var membership = string.IsNullOrWhiteSpace(game.CampaignId) ? null : _campaignAuthorization.GetMembership(actor.Id, game.CampaignId);
        var capabilities = string.IsNullOrWhiteSpace(game.CampaignId) ? Array.Empty<string>() : _campaignAuthorization.GetEffectiveCapabilities(actor.Id, game.CampaignId).OrderBy(x => x).ToArray();
        return new Dictionary<string, object>
        {
            ["contextRevision"] = game.ContextRevision,
            ["account"] = new Dictionary<string, object> { ["id"] = actor.Id, ["displayName"] = actor.Login },
            ["campaign"] = new Dictionary<string, object> { ["id"] = campaign?.Id ?? string.Empty, ["displayName"] = campaign?.Name ?? string.Empty },
            ["session"] = new Dictionary<string, object> { ["id"] = current?.SessionId ?? string.Empty, ["displayName"] = current?.Name ?? string.Empty },
            ["world"] = new Dictionary<string, object> { ["id"] = campaign?.WorldId ?? string.Empty, ["displayName"] = string.Empty },
            ["activeCharacter"] = new Dictionary<string, object> { ["id"] = ownership?.CharacterId ?? string.Empty, ["displayName"] = ownership?.CharacterDisplayName ?? string.Empty },
            ["activeScene"] = new Dictionary<string, object> { ["id"] = current?.CurrentSceneId ?? string.Empty, ["displayName"] = current?.CurrentSceneName ?? string.Empty },
            ["activeMap"] = new Dictionary<string, object> { ["id"] = FirstNonEmpty(current?.ActiveRoomId, current?.ActiveSceneMapId, current?.ActiveWorldMapId), ["displayName"] = FirstNonEmpty(current?.ActiveRoomName, current?.ActiveSceneMapName, current?.ActiveWorldMapName) },
            ["activeCombat"] = new Dictionary<string, object> { ["id"] = current?.ActiveCombatEncounterId ?? string.Empty, ["displayName"] = current?.ActiveCombatName ?? string.Empty },
            ["role"] = game.SuperAdminOverrideActive ? "SuperAdmin (явный режим)" : CampaignRoleDisplay02110(membership?.PrimaryRoleId ?? string.Empty),
            ["capabilities"] = capabilities.Cast<object>().ToArray(),
            ["sessionStatus"] = current == null ? string.Empty : SessionStatusDisplay02110(current.Status),
            ["sessionMode"] = current == null ? string.Empty : SessionModeDisplay02110(current.Mode),
            ["superAdminOverrideActive"] = game.SuperAdminOverrideActive,
            ["superAdminOverrideWarning"] = game.SuperAdminOverrideActive ? "Режим SuperAdmin — просмотр чужой кампании" : string.Empty,
            ["state"] = campaign == null ? "no_campaign" : current == null ? "campaign_selected" : "ready",
            ["stateMessage"] = campaign == null ? "Выберите кампанию." : string.Empty,
            ["missingProfileSections"] = Array.Empty<object>(),
            ["modules"] = BuildContextModuleAvailability0212().Select(x => (object)new Dictionary<string, object> { ["moduleKey"] = x.ModuleKey, ["isAvailable"] = x.IsAvailable, ["reason"] = x.Reason }).ToArray(),
            ["serverUtc"] = DateTime.UtcNow
        };
    }

    private static string CampaignRoleDisplay02110(string roleId) => roleId switch
    {
        CampaignRoleIds.OwnerGM => "Ведущий GM",
        CampaignRoleIds.CoGM => "Помощник GM",
        CampaignRoleIds.Editor => "Редактор",
        CampaignRoleIds.Player => "Игрок",
        CampaignRoleIds.Observer => "Наблюдатель",
        _ => "Нет роли"
    };

    private static string SessionStatusDisplay02110(string status) => status switch
    {
        CurrentSessionStatusIds.Planned => "Запланирована",
        CurrentSessionStatusIds.Active => "Активна",
        CurrentSessionStatusIds.Paused => "Приостановлена",
        CurrentSessionStatusIds.Completed => "Завершена",
        CurrentSessionStatusIds.Cancelled => "Отменена",
        _ => status
    };

    private static string SessionModeDisplay02110(string mode) => mode switch
    {
        CurrentSessionModeIds.Preparation => "Подготовка",
        CurrentSessionModeIds.NormalScene => "Сцена",
        CurrentSessionModeIds.Combat => "Бой",
        CurrentSessionModeIds.Travel => "Путешествие",
        CurrentSessionModeIds.Downtime => "Межсессионное время",
        _ => mode
    };
}
