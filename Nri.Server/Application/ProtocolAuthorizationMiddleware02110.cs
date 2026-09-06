using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Bson;
using MongoDB.Driver;
using Nri.Server.Infrastructure;
using Nri.Server.Logging;
using Nri.Shared.Domain;
using Nri.Shared.Utilities;

namespace Nri.Server.Application;

public sealed class ResolvedAuthorizationScope02110
{
    public string CampaignId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string CharacterId { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public bool IsGlobal { get; set; }
    public string Source { get; set; } = string.Empty;
}

public sealed class ProtocolAuthorizationMiddleware02110
{
    private readonly INriRepositoryFactory _repositories;
    private readonly ICampaignAuthorizationService _campaignAuthorization;
    private readonly IServerLogger _logger;
    private readonly AuthoritativeMongoScopeLookup02110 _scopeLookup;

    public ProtocolAuthorizationMiddleware02110(
        INriRepositoryFactory repositories,
        ICampaignAuthorizationService campaignAuthorization,
        IServerLogger logger,
        MongoContext mongo)
    {
        _repositories = repositories;
        _campaignAuthorization = campaignAuthorization;
        _logger = logger;
        _scopeLookup = new AuthoritativeMongoScopeLookup02110(mongo.Database);
    }

    public ResolvedAuthorizationScope02110 Authorize(CommandContext context, ProtocolAuthorizationDescriptor02110 descriptor)
    {
        if (descriptor.AuthorizationClass == ProtocolAuthorizationClass02110.PublicUnauthenticated)
            return Global("public");
        if (context.Session == null) throw new UnauthorizedAccessException("Access is unavailable.");
        if (descriptor.AuthorizationClass == ProtocolAuthorizationClass02110.AuthenticatedGlobal)
            return Global("authenticated");

        var actor = _repositories.Accounts.GetById(context.Session.UserId)
                    ?? throw new UnauthorizedAccessException("Access is unavailable.");
        if (descriptor.AuthorizationClass == ProtocolAuthorizationClass02110.SystemAdmin)
        {
            if (!actor.Roles.Contains(UserRole.Admin) && !actor.Roles.Contains(UserRole.SuperAdmin))
                throw new UnauthorizedAccessException("Access is unavailable.");
            return Global("system_admin");
        }
        if (descriptor.AuthorizationClass == ProtocolAuthorizationClass02110.SuperAdminOnly)
        {
            if (!actor.Roles.Contains(UserRole.SuperAdmin)) throw new UnauthorizedAccessException("Access is unavailable.");
            return Global("superadmin");
        }

        var scope = Resolve(context, descriptor);
        if (string.IsNullOrWhiteSpace(scope.CampaignId))
            throw new UnauthorizedAccessException("Campaign access is unavailable.");

        var requestedCampaign = Get(context, "campaignId");
        if (!string.IsNullOrWhiteSpace(requestedCampaign)
            && !string.Equals(requestedCampaign, scope.CampaignId, StringComparison.Ordinal))
            Deny(context, descriptor, "payload_scope_mismatch");

        var selectedCampaign = context.Session.GameContext.CampaignId;
        var selectingCampaign = string.Equals(descriptor.CommandName, "gameContext.selectCampaign", StringComparison.Ordinal);
        if (!selectingCampaign && !string.IsNullOrWhiteSpace(selectedCampaign)
            && !string.Equals(selectedCampaign, scope.CampaignId, StringComparison.Ordinal))
            Deny(context, descriptor, "active_context_mismatch");

        ValidateContextRevision(context);
        if (descriptor.AuthorizationClass is ProtocolAuthorizationClass02110.SessionScopedRead or ProtocolAuthorizationClass02110.SessionScopedMutation
            && !string.IsNullOrWhiteSpace(scope.SessionId))
        {
            var session = RequireSession(scope.SessionId, scope.CampaignId);
            _campaignAuthorization.RequireSessionCapability(context.Session, session, descriptor.RequiredCapability);
        }
        else
        {
            _campaignAuthorization.RequireCampaignCapability(context.Session, scope.CampaignId, descriptor.RequiredCapability);
        }

        if (descriptor.AuthorizationClass == ProtocolAuthorizationClass02110.PlayerOwnedCharacter)
            RequirePlayerCharacter(context, scope);
        return scope;
    }

    private ResolvedAuthorizationScope02110 Resolve(CommandContext context, ProtocolAuthorizationDescriptor02110 descriptor)
    {
        return descriptor.ScopeKind switch
        {
            ProtocolScopeKind02110.Campaign => ResolveCampaign(context),
            ProtocolScopeKind02110.Session => ResolveSession(context),
            ProtocolScopeKind02110.Character => ResolveCharacter(context),
            ProtocolScopeKind02110.Map => ResolveMap(context),
            ProtocolScopeKind02110.Combat => ResolveCombat(context),
            ProtocolScopeKind02110.Request => ResolveRequest(context),
            ProtocolScopeKind02110.Project => ResolveProject(context),
            ProtocolScopeKind02110.ActiveCampaign => ResolveGenericOrActive(context, descriptor),
            _ => ResolveGenericOrActive(context, descriptor)
        };
    }

    private ResolvedAuthorizationScope02110 ResolveCampaign(CommandContext context)
    {
        var campaignId = First(context, "campaignId", "targetCampaignId");
        if (string.IsNullOrWhiteSpace(campaignId)) campaignId = context.Session!.GameContext.CampaignId;
        return Scope(campaignId, string.Empty, string.Empty, "campaign", campaignId, "campaign_payload_or_context");
    }

    private ResolvedAuthorizationScope02110 ResolveActiveCampaign(CommandContext context)
    {
        var payloadCampaign = First(context, "campaignId", "targetCampaignId");
        var campaignId = FirstNonEmpty(payloadCampaign, context.Session!.GameContext.CampaignId);
        return Scope(campaignId, context.Session.GameContext.SessionId, context.Session.GameContext.ActiveCharacterId,
            "campaign", campaignId, string.IsNullOrWhiteSpace(payloadCampaign) ? "active_context" : "campaign_payload");
    }

    private ResolvedAuthorizationScope02110 ResolveGenericOrActive(CommandContext context, ProtocolAuthorizationDescriptor02110 descriptor)
    {
        foreach (var candidate in EntityIdCandidates(context))
        {
            var document = _scopeLookup.TryFind(descriptor.SecurityTestGroup, candidate.Value)
                           ?? _scopeLookup.TryFindAny(candidate.Value);
            if (document == null) continue;
            var campaignId = BsonString(document, "CampaignId");
            var sessionId = BsonString(document, "SessionId");
            var characterId = FirstNonEmpty(BsonString(document, "CharacterId"), BsonString(document, "OwnerCharacterId"));
            if (string.IsNullOrWhiteSpace(campaignId) && !string.IsNullOrWhiteSpace(sessionId)) campaignId = ResolveSessionOrCampaign(sessionId);
            if (string.IsNullOrWhiteSpace(campaignId) && !string.IsNullOrWhiteSpace(characterId))
            {
                var character = _repositories.Characters.GetById(characterId);
                if (character != null) campaignId = ResolveSessionOrCampaign(character.SessionId);
            }
            if (string.IsNullOrWhiteSpace(campaignId))
            {
                var mapId = BsonString(document, "MapId");
                if (!string.IsNullOrWhiteSpace(mapId))
                {
                    var map = _repositories.MapCanvases.GetByIdAsync(mapId).GetAwaiter().GetResult();
                    campaignId = map?.CampaignId ?? string.Empty;
                }
            }
            if (string.IsNullOrWhiteSpace(campaignId))
            {
                var encounterId = BsonString(document, "EncounterId");
                if (!string.IsNullOrWhiteSpace(encounterId))
                {
                    var encounter = _repositories.CombatEncounters.GetByIdAsync(encounterId).GetAwaiter().GetResult();
                    campaignId = encounter?.CampaignId ?? string.Empty;
                    sessionId = FirstNonEmpty(sessionId, encounter?.SessionId ?? string.Empty);
                }
            }
            if (string.IsNullOrWhiteSpace(campaignId))
            {
                var projectId = BsonString(document, "ProjectId");
                var project = string.IsNullOrWhiteSpace(projectId) ? null : _repositories.Projects.GetById(projectId);
                campaignId = project?.CampaignId ?? string.Empty;
                sessionId = FirstNonEmpty(sessionId, project?.SessionId ?? string.Empty);
                characterId = FirstNonEmpty(characterId, project?.OwnerCharacterId ?? string.Empty);
            }
            if (!string.IsNullOrWhiteSpace(campaignId))
                return Scope(campaignId, sessionId, characterId, descriptor.SecurityTestGroup, candidate.Value, $"mongo_entity:{candidate.Key}");
        }
        return ResolveActiveCampaign(context);
    }

    private ResolvedAuthorizationScope02110 ResolveSession(CommandContext context)
    {
        var sessionId = First(context, "sessionId", "currentSessionId", "targetSessionId");
        if (string.IsNullOrWhiteSpace(sessionId)) sessionId = context.Session!.GameContext.SessionId;
        if (string.IsNullOrWhiteSpace(sessionId)) return ResolveActiveCampaign(context);
        var session = FindSession(sessionId);
        if (session == null) throw new KeyNotFoundException("Session not found.");
        return Scope(session.CampaignId, session.SessionId, string.Empty, "session", session.Id, "current_session_entity");
    }

    private ResolvedAuthorizationScope02110 ResolveCharacter(CommandContext context)
    {
        var characterId = First(context, "characterId", "ownerCharacterId", "targetCharacterId", "actorCharacterId");
        if (string.IsNullOrWhiteSpace(characterId)) characterId = context.Session!.GameContext.ActiveCharacterId;
        if (string.IsNullOrWhiteSpace(characterId)) return ResolveActiveCampaign(context);
        var ownership = _repositories.CharacterOwnerships.Find(
            Builders<CharacterOwnershipState>.Filter.Eq(x => x.CharacterId, characterId)).FirstOrDefault();
        if (ownership != null)
            return Scope(ownership.CampaignId, string.Empty, ownership.CharacterId, "character", ownership.CharacterId, "character_v2_ownership");
        var character = _repositories.Characters.GetById(characterId);
        if (character == null) throw new KeyNotFoundException("Character not found.");
        var campaignId = ResolveSessionOrCampaign(character.SessionId);
        return Scope(campaignId, character.SessionId, character.Id, "character", character.Id, "character_v2_identity");
    }

    private ResolvedAuthorizationScope02110 ResolveMap(CommandContext context)
    {
        var mapId = First(context, "mapId", "sceneMapId", "worldMapId", "activeSceneMapId");
        MapCanvasState? map = null;
        if (!string.IsNullOrWhiteSpace(mapId))
        {
            map = _repositories.MapCanvases.GetByIdAsync(mapId).GetAwaiter().GetResult();
            if (map != null) return Scope(map.CampaignId, string.Empty, string.Empty, "map", map.Id, "map_canvas_entity");
            var world = _repositories.WorldMaps.GetByIdAsync(mapId).GetAwaiter().GetResult();
            if (world != null) return Scope(world.CampaignId, string.Empty, string.Empty, "world_map", world.Id, "world_map_entity");
        }

        var markerId = First(context, "markerId", "tokenId");
        if (!string.IsNullOrWhiteSpace(markerId))
        {
            var marker = _repositories.MapMarkers.GetByIdAsync(markerId).GetAwaiter().GetResult();
            if (marker != null)
            {
                map = _repositories.MapCanvases.GetByIdAsync(marker.MapId).GetAwaiter().GetResult();
                if (map != null) return Scope(map.CampaignId, string.Empty, string.Empty, "map_marker", marker.Id, "marker_parent_map");
            }
            var token = _scopeLookup.TryFind("Map tokens", markerId) ?? _scopeLookup.TryFindAny(markerId);
            var tokenMapId = token == null ? string.Empty : BsonString(token, "MapId");
            if (!string.IsNullOrWhiteSpace(tokenMapId))
            {
                map = _repositories.MapCanvases.GetByIdAsync(tokenMapId).GetAwaiter().GetResult();
                if (map != null) return Scope(map.CampaignId, string.Empty, string.Empty, "map_token", markerId, "token_parent_map");
                var tokenWorldMap = _repositories.WorldMaps.GetByIdAsync(tokenMapId).GetAwaiter().GetResult();
                if (tokenWorldMap != null) return Scope(tokenWorldMap.CampaignId, string.Empty, string.Empty, "map_token", markerId, "token_parent_world_map");
            }
        }
        if (string.IsNullOrWhiteSpace(mapId) && string.IsNullOrWhiteSpace(markerId)) return ResolveActiveCampaign(context);
        throw new KeyNotFoundException("Map not found.");
    }

    private ResolvedAuthorizationScope02110 ResolveCombat(CommandContext context)
    {
        var encounterId = First(context, "encounterId", "combatId", "activeCombatId");
        if (string.IsNullOrWhiteSpace(encounterId))
        {
            var participantId = First(context, "participantId", "targetParticipantId");
            if (!string.IsNullOrWhiteSpace(participantId))
            {
                var participant = _repositories.CombatParticipants.GetByIdAsync(participantId).GetAwaiter().GetResult();
                encounterId = participant?.EncounterId ?? string.Empty;
            }
        }
        if (string.IsNullOrWhiteSpace(encounterId)) return ResolveActiveCampaign(context);
        var encounter = _repositories.CombatEncounters.GetByIdAsync(encounterId).GetAwaiter().GetResult();
        if (encounter != null) return Scope(encounter.CampaignId, encounter.SessionId, string.Empty, "combat", encounter.Id, "combat_entity");
        var legacy = _repositories.Combats.GetById(encounterId);
        if (legacy != null) return Scope(ResolveSessionOrCampaign(legacy.SessionId), legacy.SessionId, string.Empty, "combat", legacy.Id, "legacy_combat_parent_session");
        throw new KeyNotFoundException("Combat not found.");
    }

    private ResolvedAuthorizationScope02110 ResolveRequest(CommandContext context)
    {
        var requestId = First(context, "requestId", "playerRequestId", "diceRequestId", "actionRequestId");
        if (string.IsNullOrWhiteSpace(requestId))
        {
            var characterId = First(context, "characterId", "ownerCharacterId", "targetCharacterId");
            return string.IsNullOrWhiteSpace(characterId) ? ResolveActiveCampaign(context) : ResolveCharacter(context);
        }
        var playerRequest = _repositories.PlayerRequests.GetById(requestId);
        if (playerRequest != null) return Scope(playerRequest.CampaignId, playerRequest.SessionId, playerRequest.CharacterId, "request", playerRequest.Id, "player_request_entity");
        var request = _repositories.ActionRequests.GetById(requestId) as RequestBase ?? _repositories.DiceRequests.GetById(requestId);
        if (request != null && !string.IsNullOrWhiteSpace(request.CharacterId))
        {
            var character = _repositories.Characters.GetById(request.CharacterId);
            if (character != null) return Scope(ResolveSessionOrCampaign(character.SessionId), character.SessionId, character.Id, "request", request.Id, "request_character_parent");
        }
        throw new KeyNotFoundException("Request not found.");
    }

    private ResolvedAuthorizationScope02110 ResolveProject(CommandContext context)
    {
        var projectId = First(context, "projectId", "runtimeProjectId", "manufacturingProjectId", "craftingProjectId");
        if (string.IsNullOrWhiteSpace(projectId)) return ResolveActiveCampaign(context);
        var project = _repositories.Projects.GetById(projectId);
        if (project != null) return Scope(project.CampaignId, project.SessionId, project.OwnerCharacterId, "project", project.Id, "project_entity");
        return ResolveGenericOrActive(context, new ProtocolAuthorizationDescriptor02110 { SecurityTestGroup = "Projects" });
    }

    private void RequirePlayerCharacter(CommandContext context, ResolvedAuthorizationScope02110 scope)
    {
        if (string.IsNullOrWhiteSpace(scope.CharacterId)) return;
        if (_campaignAuthorization.GetEffectiveCapabilities(context.Session!.UserId, scope.CampaignId)
            .Contains(CampaignCapabilityIds.CharacterManageAnyInCampaign)) return;
        var character = _repositories.Characters.GetById(scope.CharacterId)
                        ?? throw new KeyNotFoundException("Character not found.");
        if (string.Equals(character.OwnerUserId, context.Session!.UserId, StringComparison.Ordinal)) return;
        var participation = _repositories.SessionParticipations.Find(
            Builders<SessionParticipation>.Filter.Eq(x => x.CampaignId, scope.CampaignId)
            & Builders<SessionParticipation>.Filter.Eq(x => x.UserId, context.Session.UserId)
            & Builders<SessionParticipation>.Filter.Eq(x => x.Status, CampaignMembershipStatusIds.Active)).FirstOrDefault();
        if (participation != null && (string.Equals(participation.ActiveCharacterId, character.Id, StringComparison.Ordinal)
                                      || participation.AllowedCharacterIds.Contains(character.Id))) return;
        Deny(context, null, "foreign_character");
    }

    private CurrentSessionState RequireSession(string sessionId, string campaignId)
    {
        var session = FindSession(sessionId);
        if (session == null || !string.Equals(session.CampaignId, campaignId, StringComparison.Ordinal))
            throw new KeyNotFoundException("Session not found.");
        return session;
    }

    private CurrentSessionState? FindSession(string sessionId)
        => _repositories.CurrentSessions.Find(
            Builders<CurrentSessionState>.Filter.Eq(x => x.SessionId, sessionId)
            | Builders<CurrentSessionState>.Filter.Eq(x => x.Id, sessionId)).FirstOrDefault();

    private string ResolveSessionOrCampaign(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var session = FindSession(value);
        return session?.CampaignId ?? value;
    }

    private static void ValidateContextRevision(CommandContext context)
    {
        var expected = PayloadReader.GetLong(context.Request.Payload, "contextRevision")
                       ?? PayloadReader.GetLong(context.Request.Payload, "expectedContextRevision");
        if (expected.HasValue && expected.Value != context.Session!.GameContext.ContextRevision)
            throw new InvalidOperationException("Active game context changed. Refresh the workspace and retry.");
    }

    private void Deny(CommandContext context, ProtocolAuthorizationDescriptor02110? descriptor, string reason)
    {
        _logger.Admin($"campaign.authorization.denied requestId={context.Request.RequestId} command={descriptor?.CommandName ?? context.Request.Command} userId={context.Session?.UserId} reason={reason}");
        throw new UnauthorizedAccessException("Campaign access is unavailable.");
    }

    private static ResolvedAuthorizationScope02110 Global(string source)
        => new ResolvedAuthorizationScope02110 { IsGlobal = true, EntityType = "global", Source = source };

    private static ResolvedAuthorizationScope02110 Scope(string campaignId, string sessionId, string characterId, string entityType, string entityId, string source)
        => new ResolvedAuthorizationScope02110
        {
            CampaignId = campaignId ?? string.Empty,
            SessionId = sessionId ?? string.Empty,
            CharacterId = characterId ?? string.Empty,
            EntityType = entityType,
            EntityId = entityId ?? string.Empty,
            Source = source
        };

    private static string First(CommandContext context, params string[] keys)
        => keys.Select(key => Get(context, key)).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    private static string Get(CommandContext context, string key) => PayloadReader.GetString(context.Request.Payload, key) ?? string.Empty;
    private static string FirstNonEmpty(params string[] values) => values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;

    private static IEnumerable<KeyValuePair<string, string>> EntityIdCandidates(CommandContext context)
    {
        foreach (var pair in context.Request.Payload)
        {
            if (!pair.Key.EndsWith("Id", StringComparison.OrdinalIgnoreCase)) continue;
            if (pair.Key.Equals("campaignId", StringComparison.OrdinalIgnoreCase)
                || pair.Key.Equals("sessionId", StringComparison.OrdinalIgnoreCase)
                || pair.Key.Equals("contextRevision", StringComparison.OrdinalIgnoreCase)
                || pair.Key.IndexOf("operation", StringComparison.OrdinalIgnoreCase) >= 0) continue;
            var value = Convert.ToString(pair.Value)?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(value)) yield return new KeyValuePair<string, string>(pair.Key, value);
        }
    }

    private static string BsonString(BsonDocument document, string name)
        => document.TryGetValue(name, out var value) && value.IsString ? value.AsString : string.Empty;
}

internal sealed class AuthoritativeMongoScopeLookup02110
{
    private readonly IMongoDatabase _database;
    private readonly string[] _collectionNames;
    private readonly Dictionary<string, BsonDocument?> _cache = new Dictionary<string, BsonDocument?>(StringComparer.Ordinal);
    private readonly object _sync = new object();

    public AuthoritativeMongoScopeLookup02110(IMongoDatabase database)
    {
        _database = database;
        _collectionNames = database.ListCollectionNames().ToList().OrderBy(x => x).ToArray();
    }

    public BsonDocument? TryFind(string securityGroup, string id)
    {
        var key = $"{securityGroup}|{id}";
        lock (_sync) if (_cache.TryGetValue(key, out var cached)) return cached;
        var keywords = Keywords(securityGroup);
        foreach (var name in _collectionNames.Where(name => keywords.Any(keyword => name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)))
        {
            var collection = _database.GetCollection<BsonDocument>(name);
            var filter = Builders<BsonDocument>.Filter.Eq("_id", id) | Builders<BsonDocument>.Filter.Eq("Id", id);
            var document = collection.Find(filter).Limit(1).FirstOrDefault();
            if (document == null) continue;
            lock (_sync) _cache[key] = document;
            return document;
        }
        lock (_sync) _cache[key] = null;
        return null;
    }

    public BsonDocument? TryFindAny(string id)
    {
        var key = $"*|{id}";
        lock (_sync) if (_cache.TryGetValue(key, out var cached)) return cached;
        foreach (var name in _collectionNames)
        {
            var collection = _database.GetCollection<BsonDocument>(name);
            var filter = Builders<BsonDocument>.Filter.Eq("_id", id) | Builders<BsonDocument>.Filter.Eq("Id", id);
            var document = collection.Find(filter).Limit(1).FirstOrDefault();
            if (document == null) continue;
            lock (_sync) _cache[key] = document;
            return document;
        }
        lock (_sync) _cache[key] = null;
        return null;
    }

    private static string[] Keywords(string group)
        => group switch
        {
            "Character Player" or "Character Admin" or "Character v2 Profiles" => new[] { "character", "actor", "inventory", "skill", "development", "runtime" },
            "CurrentSession" => new[] { "session", "calendar", "fate", "gameplay" },
            "ActiveGroup" => new[] { "group" },
            "Maps" or "Map tokens" => new[] { "map", "room", "marker", "token" },
            "Combat" => new[] { "combat" },
            "Weather" => new[] { "weather", "environment", "observation", "exposure" },
            "Travel" => new[] { "travel" },
            "Knowledge" => new[] { "knowledge" },
            "Quest" => new[] { "quest" },
            "Economy/Shop/Market" => new[] { "economy", "shop", "market", "legal", "restriction" },
            "Organizations" => new[] { "organization", "faction" },
            "Projects" => new[] { "project", "research", "engineering", "production", "manufacturing", "crafting", "factory", "proposal", "asset" },
            "Requests" or "Dice" => new[] { "request", "dice", "proposal" },
            "Chat" => new[] { "chat" },
            "Audio" => new[] { "audio" },
            "Notes" => new[] { "note", "journal" },
            "Automation" => new[] { "automation" },
            "Portability" => new[] { "campaign", "portability" },
            _ => new[] { "campaign", "session" }
        };
}
