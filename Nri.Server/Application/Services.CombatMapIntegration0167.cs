using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MongoDB.Bson;
using MongoDB.Driver;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;
using Nri.Shared.Utilities;

namespace Nri.Server.Application;

public partial class ServiceHub
{
    public ResponseEnvelope CombatMapAdminGetActiveSceneMapOverlay0167(CommandContext context)
    {
        RequireAdmin(context);
        if (!CombatMap0167AdminReadEnabled()) return CombatMap0167Disabled(context.Request.Command);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        if (TryResolveCombatMap0219Encounter(payload, out var canonicalEncounter))
            return Ok("Combat map overlay loaded.", CombatMap0219AdminOverlayPayload(canonicalEncounter, payload));
        var combat = CombatMap0167ResolveAdminCombat(payload);
        var map = CombatMap0167ResolveSceneMap(payload, combat);
        return Ok("Combat map overlay loaded.", CombatMap0167AdminOverlayPayload(combat, map));
    }

    public ResponseEnvelope CombatMapAdminListJoinableTokens0167(CommandContext context)
    {
        RequireAdmin(context);
        if (!CombatMap0167AdminReadEnabled()) return CombatMap0167Disabled(context.Request.Command);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        if (TryResolveCombatMap0219Encounter(payload, out var canonicalEncounter))
            return Ok("Joinable map tokens loaded.", CombatMap0219JoinableTokensPayload(canonicalEncounter, payload));
        var combat = CombatMap0167ResolveAdminCombat(payload);
        var map = CombatMap0167ResolveSceneMap(payload, combat);
        var includeAlreadyLinked = PayloadReader.GetBool(payload, "includeAlreadyLinked");
        var linkedIds = Combat0166ParticipantDocs(Combat0166String(combat, "Id"), includeArchived: false)
            .Select(x => Combat0166String(x, "MapTokenId"))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var tokens = MapToken0163DocsForMap(MapToken0163KindScene, SceneMap0162CanonicalMapId(map), includeHidden: true)
            .Where(CombatMap0167CanJoinCombat)
            .Where(x => includeAlreadyLinked || !linkedIds.Contains(GetDocString(x, "Id")))
            .Select(x => CombatMap0167TokenPayload(x, admin: true, includeCoordinates: true))
            .Cast<object>()
            .ToArray();

        return Ok("Joinable map tokens loaded.", new Dictionary<string, object>
        {
            ["combatId"] = Combat0166String(combat, "Id"),
            ["sceneMapId"] = Combat0166String(map, "Id"),
            ["tokens"] = tokens,
            ["count"] = tokens.Length,
            ["alreadyLinkedCount"] = linkedIds.Count
        });
    }

    public ResponseEnvelope CombatMapAdminAddParticipantFromToken0167(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!CombatMap0167AdminWriteEnabled()) return CombatMap0167Disabled(context.Request.Command);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        if (TryResolveCombatMap0219Encounter(payload, out var canonicalEncounter))
            return CombatMap0219AddParticipantFromToken(context, actor, canonicalEncounter, payload);
        var combat = CombatMap0167ResolveAdminCombat(payload);
        var map = CombatMap0167ResolveSceneMap(payload, combat);
        var token = CombatMap0167RequireSceneToken(payload, map);
        if (!CombatMap0167CanJoinCombat(token))
            return Error("This map token cannot join combat.", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);

        var participant = CombatMap0167CreateParticipantFromToken(combat, map, token, payload, actor.Id);
        AppendCombat0166Event(combat, "combat.map.participant.from_token.added", "Участник добавлен из токена карты.", $"participantId={Combat0166String(participant, "Id")} tokenId={GetDocString(token, "Id")}", actor.Id, "gm_only", Combat0166String(participant, "Id"));
        PublishCombat0166Sync(Combat0166ReloadSession(combat), "combat.map.participant.added", actor.Id, context.Request.RequestId);
        return Ok("Participant added from map token.", CombatMap0167AdminOverlayPayload(Combat0166ReloadSession(combat), map));
    }

    public ResponseEnvelope CombatMapAdminAddParticipantsFromTokens0167(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!CombatMap0167AdminWriteEnabled()) return CombatMap0167Disabled(context.Request.Command);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var combat = CombatMap0167ResolveAdminCombat(payload);
        var map = CombatMap0167ResolveSceneMap(payload, combat);
        var tokenIds = CombatMap0167StringList(payload, "tokenIds").ToArray();
        if (tokenIds.Length == 0)
        {
            var tokenId = FirstNonEmptyWorld(PayloadReader.GetString(payload, "tokenId"), PayloadReader.GetString(payload, "mapTokenId"));
            if (!string.IsNullOrWhiteSpace(tokenId)) tokenIds = new[] { tokenId };
        }
        if (tokenIds.Length == 0) return Error("tokenIds is required.", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);

        var added = 0;
        foreach (var tokenId in tokenIds.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var tokenPayload = new Dictionary<string, object>(payload, StringComparer.OrdinalIgnoreCase) { ["tokenId"] = tokenId };
            var token = CombatMap0167RequireSceneToken(tokenPayload, map);
            if (!CombatMap0167CanJoinCombat(token)) continue;
            CombatMap0167CreateParticipantFromToken(combat, map, token, payload, actor.Id);
            added++;
        }

        AppendCombat0166Event(combat, "combat.map.participants.from_tokens.added", "Участники добавлены из токенов карты.", $"added={added}", actor.Id, "gm_only", string.Empty);
        PublishCombat0166Sync(Combat0166ReloadSession(combat), "combat.map.participants.added", actor.Id, context.Request.RequestId);
        return Ok("Participants added from map tokens.", CombatMap0167AdminOverlayPayload(Combat0166ReloadSession(combat), map));
    }

    public ResponseEnvelope CombatMapAdminLinkParticipantToken0167(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!CombatMap0167AdminWriteEnabled()) return CombatMap0167Disabled(context.Request.Command);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        if (TryResolveCombatMap0219Encounter(payload, out var canonicalEncounter))
            return CombatMap0219LinkParticipantToken(context, actor, canonicalEncounter, payload);
        var participant = Combat0166RequireParticipant(payload);
        var combat = Combat0166RequireSessionById(Combat0166String(participant, "CombatId"));
        var map = CombatMap0167ResolveSceneMap(payload, combat);
        var token = CombatMap0167RequireSceneToken(payload, map);
        CombatMap0167ApplyTokenLink(participant, map, token, actor.Id);
        AppendCombat0166Event(combat, "combat.map.token.linked", "Токен карты привязан к участнику боя.", $"participantId={Combat0166String(participant, "Id")} tokenId={GetDocString(token, "Id")}", actor.Id, "gm_only", Combat0166String(participant, "Id"));
        PublishCombat0166Sync(combat, "combat.map.token.linked", actor.Id, context.Request.RequestId);
        return Ok("Participant token linked.", CombatMap0167AdminOverlayPayload(Combat0166ReloadSession(combat), map));
    }

    public ResponseEnvelope CombatMapAdminUnlinkParticipantToken0167(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!CombatMap0167AdminWriteEnabled()) return CombatMap0167Disabled(context.Request.Command);
        var canonicalPayload = context.Request.Payload ?? new Dictionary<string, object>();
        if (TryResolveCombatMap0219Encounter(canonicalPayload, out var canonicalEncounter))
            return CombatMap0219UnlinkParticipantToken(context, actor, canonicalEncounter, canonicalPayload);
        var participant = Combat0166RequireParticipant(context.Request.Payload);
        var combat = Combat0166RequireSessionById(Combat0166String(participant, "CombatId"));
        participant["SceneMapId"] = string.Empty;
        participant["MapTokenId"] = string.Empty;
        participant["MapTokenDisplayName"] = string.Empty;
        participant["MapTokenVisibility"] = "hidden";
        participant["MapLinkStatus"] = "unlinked";
        participant["MapOverlayState"] = "none";
        Combat0166TouchParticipant(participant, actor.Id);
        Combat0166Participants().ReplaceOne(Combat0166AnyIdFilter(Combat0166String(participant, "Id")), participant);
        AppendCombat0166Event(combat, "combat.map.token.unlinked", "Токен карты отвязан от участника боя.", $"participantId={Combat0166String(participant, "Id")}", actor.Id, "gm_only", Combat0166String(participant, "Id"));
        PublishCombat0166Sync(combat, "combat.map.token.unlinked", actor.Id, context.Request.RequestId);
        var map = CombatMap0167ResolveSceneMap(context.Request.Payload ?? new Dictionary<string, object>(), combat, allowMissing: true);
        return Ok("Participant token unlinked.", map == null ? Combat0166AdminPayload(Combat0166ReloadSession(combat), includeLog: true) : CombatMap0167AdminOverlayPayload(Combat0166ReloadSession(combat), map));
    }

    public ResponseEnvelope CombatMapAdminSyncParticipantVisibilityFromToken0167(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!CombatMap0167AdminWriteEnabled()) return CombatMap0167Disabled(context.Request.Command);
        var canonicalPayload = context.Request.Payload ?? new Dictionary<string, object>();
        if (TryResolveCombatMap0219Encounter(canonicalPayload, out var canonicalEncounter))
            return CombatMap0219SyncVisibility(context, actor, canonicalEncounter, canonicalPayload);
        var participant = Combat0166RequireParticipant(context.Request.Payload);
        var combat = Combat0166RequireSessionById(Combat0166String(participant, "CombatId"));
        var tokenId = Combat0166String(participant, "MapTokenId");
        if (string.IsNullOrWhiteSpace(tokenId)) return Error("Participant is not linked to a map token.", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);
        var token = MapToken0163CollectionRef().Find(Combat0166ActiveIdFilter(tokenId)).FirstOrDefault();
        if (token == null) return Error("Linked map token not found.", ResponseStatus.NotFound, ErrorCode.NotFound);
        participant["MapTokenVisibility"] = CombatMap0167CombatVisibilityFromToken(token);
        participant["VisibilityMode"] = string.Equals(CombatMap0167CombatVisibilityFromToken(token), "player_visible", StringComparison.OrdinalIgnoreCase) ? "player_visible" : Combat0166String(participant, "VisibilityMode", "hidden");
        participant["IsPlayerVisible"] = string.Equals(Combat0166String(participant, "VisibilityMode"), "player_visible", StringComparison.OrdinalIgnoreCase);
        participant["MapLinkStatus"] = CombatMap0167LinkStatus(participant, token);
        Combat0166TouchParticipant(participant, actor.Id);
        Combat0166Participants().ReplaceOne(Combat0166AnyIdFilter(Combat0166String(participant, "Id")), participant);
        AppendCombat0166Event(combat, "combat.map.visibility.synced", "Видимость участника синхронизирована с токеном карты.", $"participantId={Combat0166String(participant, "Id")} tokenId={tokenId}", actor.Id, "gm_only", Combat0166String(participant, "Id"));
        PublishCombat0166Sync(combat, "combat.map.visibility.changed", actor.Id, context.Request.RequestId);
        return Ok("Participant visibility synchronized from token.", CombatMap0167AdminOverlayPayload(Combat0166ReloadSession(combat), CombatMap0167ResolveSceneMap(context.Request.Payload ?? new Dictionary<string, object>(), combat)));
    }

    public ResponseEnvelope CombatMapAdminSetParticipantMapBadge0167(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!CombatMap0167AdminWriteEnabled()) return CombatMap0167Disabled(context.Request.Command);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var participant = Combat0166RequireParticipant(payload);
        var combat = Combat0166RequireSessionById(Combat0166String(participant, "CombatId"));
        participant["MapBadgeText"] = FirstNonEmptyWorld(PayloadReader.GetString(payload, "badgeText"), PayloadReader.GetString(payload, "mapBadgeText"), string.Empty);
        participant["MapBadgeColorKey"] = FirstNonEmptyWorld(PayloadReader.GetString(payload, "badgeColorKey"), PayloadReader.GetString(payload, "colorKey"), string.Empty);
        participant["MapBadgeVisibility"] = NormalizeCombat0166Visibility(FirstNonEmptyWorld(PayloadReader.GetString(payload, "badgeVisibility"), PayloadReader.GetString(payload, "visibilityMode")), Combat0166String(participant, "VisibilityMode", "hidden"));
        Combat0166TouchParticipant(participant, actor.Id);
        Combat0166Participants().ReplaceOne(Combat0166AnyIdFilter(Combat0166String(participant, "Id")), participant);
        PublishCombat0166Sync(combat, "combat.map.badge.changed", actor.Id, context.Request.RequestId);
        return Ok("Participant map badge updated.", CombatMap0167AdminOverlayPayload(Combat0166ReloadSession(combat), CombatMap0167ResolveSceneMap(payload, combat)));
    }

    public ResponseEnvelope CombatMapAdminFocusParticipantToken0167(CommandContext context)
    {
        RequireAdmin(context);
        if (!CombatMap0167AdminReadEnabled()) return CombatMap0167Disabled(context.Request.Command);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        if (TryResolveCombatMap0219Encounter(payload, out var canonicalEncounter))
        {
            var canonicalOverlay = CombatMap0219AdminOverlayPayload(canonicalEncounter, payload);
            var participantId = PayloadReader.GetString(payload, "participantId") ?? string.Empty;
            var canonicalParticipant = _repositories.CombatParticipants.GetByIdAsync(participantId).GetAwaiter().GetResult();
            canonicalOverlay["focusedParticipantId"] = canonicalParticipant?.Id ?? string.Empty;
            canonicalOverlay["focusedMapTokenId"] = canonicalParticipant?.MapTokenId ?? string.Empty;
            return Ok("Participant map token focused.", canonicalOverlay);
        }
        var participant = Combat0166RequireParticipant(payload);
        var combat = Combat0166RequireSessionById(Combat0166String(participant, "CombatId"));
        var map = CombatMap0167ResolveSceneMap(payload, combat);
        var overlay = CombatMap0167AdminOverlayPayload(combat, map);
        overlay["focusedParticipantId"] = Combat0166String(participant, "Id");
        overlay["focusedMapTokenId"] = Combat0166String(participant, "MapTokenId");
        return Ok("Participant map token focused.", overlay);
    }

    public ResponseEnvelope CombatMapAdminGetLinkAudit0167(CommandContext context)
    {
        RequireAdmin(context);
        if (!CombatMap0167AdminReadEnabled()) return CombatMap0167Disabled(context.Request.Command);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        if (TryResolveCombatMap0219Encounter(payload, out var canonicalEncounter))
            return Ok("Combat map link audit loaded.", CombatMap0219LinkAuditPayload(canonicalEncounter, payload));
        var combat = CombatMap0167ResolveAdminCombat(payload);
        var map = CombatMap0167ResolveSceneMap(payload, combat, allowMissing: true);
        var participants = Combat0166ParticipantDocs(Combat0166String(combat, "Id"), includeArchived: false)
            .Select(p =>
            {
                var tokenId = Combat0166String(p, "MapTokenId");
                var token = string.IsNullOrWhiteSpace(tokenId) ? null : MapToken0163CollectionRef().Find(Combat0166AnyIdFilter(tokenId)).FirstOrDefault();
                return new Dictionary<string, object>
                {
                    ["participantId"] = Combat0166String(p, "Id"),
                    ["participantName"] = Combat0166String(p, "DisplayName"),
                    ["sceneMapId"] = Combat0166String(p, "SceneMapId"),
                    ["mapTokenId"] = tokenId,
                    ["tokenExists"] = token != null,
                    ["tokenArchived"] = token != null && GetDocBool(token, "IsArchived"),
                    ["tokenMapId"] = token == null ? string.Empty : GetDocString(token, "MapId"),
                    ["linkStatus"] = CombatMap0167LinkStatus(p, token),
                    ["sourceOfTruth"] = "map_token_instances.X/Y"
                };
            })
            .Cast<object>()
            .ToArray();
        return Ok("Combat map link audit loaded.", new Dictionary<string, object>
        {
            ["combatId"] = Combat0166String(combat, "Id"),
            ["sceneMapId"] = map == null ? string.Empty : Combat0166String(map, "Id"),
            ["participants"] = participants,
            ["sourceCollections"] = new object[] { Combat0166ParticipantsCollection, MapToken0163Collection, SceneMap0162DefinitionsCollection }
        });
    }

    public ResponseEnvelope CombatMapPlayerGetActiveSceneMapOverlay0167(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!CombatMap0167PlayerReadEnabled()) return CombatMap0167Disabled(context.Request.Command);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var canonical = CombatMap0219ResolvePlayerEncounter(payload, actor);
        if (canonical != null)
            return Ok("Combat map overlay loaded.", CombatMap0219PlayerOverlayPayload(canonical, payload, actor));
        var combat = Combat0166FindActiveForPlayerPayload(payload);
        if (!Combat0166CanPlayerSeeCombat(combat))
            return Error("Combat map overlay is not available.", ResponseStatus.NotFound, ErrorCode.NotFound);
        var map = CombatMap0167ResolveSceneMap(payload, combat);
        return Ok("Combat map overlay loaded.", CombatMap0167PlayerOverlayPayload(combat, map, actor.Id));
    }

    public ResponseEnvelope CombatMapPlayerGetMyVisibleCombatTokens0167(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!CombatMap0167PlayerReadEnabled()) return CombatMap0167Disabled(context.Request.Command);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var canonical = CombatMap0219ResolvePlayerEncounter(payload, actor);
        if (canonical != null)
        {
            var canonicalOverlay = CombatMap0219PlayerOverlayPayload(canonical, payload, actor);
            return Ok("Visible combat tokens loaded.", new Dictionary<string, object>
            {
                ["combatId"] = canonical.Id,
                ["sceneMapId"] = canonicalOverlay.TryGetValue("sceneMapId", out var mapId) ? mapId : string.Empty,
                ["tokens"] = canonicalOverlay.TryGetValue("combatTokens", out var tokens) ? tokens : Array.Empty<object>(),
                ["warnings"] = Array.Empty<object>()
            });
        }
        var combat = Combat0166FindActiveForPlayerPayload(payload);
        var map = CombatMap0167ResolveSceneMap(payload, combat);
        var overlay = CombatMap0167PlayerOverlayPayload(combat, map, actor.Id);
        return Ok("Visible combat tokens loaded.", new Dictionary<string, object>
        {
            ["combatId"] = Combat0166String(combat, "Id"),
            ["sceneMapId"] = Combat0166String(map, "Id"),
            ["tokens"] = overlay["combatTokens"],
            ["warnings"] = overlay["warnings"]
        });
    }

    public ResponseEnvelope CombatMapPlayerMoveMyToken0219(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!CombatMap0167PlayerReadEnabled() || !CombatV1ActionWriteEnabled())
            return CombatMap0167Disabled(context.Request.Command);
        return CombatMap0219MovePlayerToken(context, actor, context.Request.Payload ?? new Dictionary<string, object>());
    }

    private Dictionary<string, object> CombatMap0167AdminOverlayPayload(BsonDocument combat, BsonDocument map)
    {
        var mapId = SceneMap0162CanonicalMapId(map);
        var tokens = MapToken0163DocsForMap(MapToken0163KindScene, mapId, includeHidden: true).ToDictionary(x => GetDocString(x, "Id"), StringComparer.OrdinalIgnoreCase);
        var participants = Combat0166ParticipantDocs(Combat0166String(combat, "Id"), includeArchived: false)
            .Select(p => CombatMap0167ParticipantOverlayPayload(p, tokens.TryGetValue(Combat0166String(p, "MapTokenId"), out var token) ? token : null, admin: true, actorUserId: string.Empty))
            .Cast<object>()
            .ToArray();
        var joinable = tokens.Values.Where(CombatMap0167CanJoinCombat)
            .Select(x => CombatMap0167TokenPayload(x, admin: true, includeCoordinates: true))
            .Cast<object>()
            .ToArray();
        return new Dictionary<string, object>
        {
            ["combat"] = Combat0166CombatPayload(combat, admin: true),
            ["combatId"] = Combat0166String(combat, "Id"),
            ["sceneMap"] = CombatMap0167SceneMapPayload(map),
            ["sceneMapId"] = mapId,
            ["sourceOfTruth"] = "token coordinates come from map_token_instances only",
            ["participants"] = participants,
            ["combatTokens"] = participants,
            ["tokens"] = tokens.Values.Select(x => CombatMap0167TokenPayload(x, admin: true, includeCoordinates: true)).Cast<object>().ToArray(),
            ["joinableTokens"] = joinable,
            ["tileLayers"] = SceneMap0164TileLayerPayloadsForMap(mapId, admin: true).Cast<object>().ToArray(),
            ["tilePatches"] = SceneMap0164TilePatchPayloadsForMap(mapId, admin: true).Cast<object>().ToArray(),
            ["assetInstances"] = SceneMap0164AssetInstancePayloadsForMap(mapId, admin: true).Cast<object>().ToArray(),
            ["warnings"] = CombatMap0167OverlayWarnings(combat, tokens).Cast<object>().ToArray(),
            ["builtAtUtc"] = DateTime.UtcNow
        };
    }

    private Dictionary<string, object> CombatMap0167PlayerOverlayPayload(BsonDocument combat, BsonDocument map, string actorUserId)
    {
        var canonicalMapId = SceneMap0162CanonicalMapId(map);
        var projection = _playerMapProjectionService.BuildSceneMap(canonicalMapId, new PlayerMapProjectionContext0204
        {
            ActorUserId = actorUserId,
            CampaignId = Combat0166String(combat, "CampaignId"),
            SessionId = Combat0166String(combat, "SessionId"),
            IncludeMarkers = true
        });
        if (!projection.Success)
            throw new KeyNotFoundException(projection.Message);
        var projectedMap = PayloadReader.GetDictionary(projection.Payload, "map")
            ?? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        var tokenDocs = MapToken0163DocsForMap(MapToken0163KindScene, canonicalMapId, includeHidden: false)
            .ToDictionary(x => GetDocString(x, "Id"), StringComparer.OrdinalIgnoreCase);
        var visibleParticipants = Combat0166ParticipantDocs(Combat0166String(combat, "Id"), includeArchived: false)
            .Where(Combat0166CanPlayerSeeParticipant)
            .ToList();
        var overlayTokens = visibleParticipants
            .Select(p =>
            {
                var tokenId = Combat0166String(p, "MapTokenId");
                return !string.IsNullOrWhiteSpace(tokenId) && tokenDocs.TryGetValue(tokenId, out var token) && CombatMap0167CanPlayerSeeLinkedToken(p, token)
                    ? CombatMap0167ParticipantOverlayPayload(p, token, admin: false, actorUserId)
                    : null;
            })
            .Where(x => x != null)
            .Cast<object>()
            .ToArray();

        return new Dictionary<string, object>
        {
            ["combat"] = Combat0166CombatPayload(combat, admin: false),
            ["combatId"] = Combat0166String(combat, "Id"),
            ["sceneMap"] = projectedMap,
            ["sceneMapId"] = canonicalMapId,
            ["combatTokens"] = overlayTokens,
            ["tileLayers"] = projectedMap.TryGetValue("tileLayers", out var tileLayers) ? tileLayers : Array.Empty<object>(),
            ["tilePatches"] = projectedMap.TryGetValue("tilePatches", out var tilePatches) ? tilePatches : Array.Empty<object>(),
            ["assetInstances"] = projectedMap.TryGetValue("assetInstances", out var assets) ? assets : Array.Empty<object>(),
            ["visibleTokenCount"] = overlayTokens.Length,
            ["projectionRevision"] = projection.Payload.TryGetValue("projectionRevision", out var revision) ? revision : 0L,
            ["warnings"] = Array.Empty<object>(),
            ["builtAtUtc"] = DateTime.UtcNow
        };
    }

    private Dictionary<string, object> CombatMap0167ParticipantOverlayPayload(BsonDocument participant, BsonDocument? token, bool admin, string actorUserId)
    {
        var basePayload = Combat0166ParticipantPayload(participant, admin, actorUserId);
        var status = CombatMap0167LinkStatus(participant, token);
        basePayload["sceneMapId"] = Combat0166String(participant, "SceneMapId");
        basePayload["linkStatus"] = status;
        basePayload["mapOverlayState"] = Combat0166String(participant, "MapOverlayState", status);
        basePayload["mapBadgeText"] = Combat0166String(participant, "MapBadgeText");
        basePayload["mapBadgeColorKey"] = Combat0166String(participant, "MapBadgeColorKey");
        basePayload["mapBadgeVisibility"] = admin ? Combat0166String(participant, "MapBadgeVisibility") : "player_visible";
        basePayload["isCurrentTurn"] = string.Equals(Combat0166String(participant, "TurnStatus"), "active", StringComparison.OrdinalIgnoreCase);
        if (token != null && (admin || CombatMap0167CanPlayerSeeLinkedToken(participant, token)))
        {
            basePayload["mapTokenId"] = GetDocString(token, "Id");
            basePayload["mapTokenDisplayName"] = GetDocString(token, "DisplayName");
            basePayload["tokenType"] = GetDocString(token, "TokenType");
            basePayload["x"] = GetDocDouble(token, "X", 0d);
            basePayload["y"] = GetDocDouble(token, "Y", 0d);
            basePayload["radius"] = GetDocDouble(token, "Radius", 0d);
            basePayload["size"] = GetDocDouble(token, "Size", 1d);
            basePayload["iconKey"] = GetDocString(token, "IconKey");
            basePayload["colorKey"] = GetDocString(token, "ColorKey");
            basePayload["visibilityMode"] = admin ? Combat0166String(participant, "VisibilityMode") : "player_visible";
            basePayload["tokenVisibility"] = admin ? GetDocString(token, "Visibility", "Hidden") : "PlayerVisible";
            basePayload["sourceOfTruth"] = "map_token_instances.X/Y";
        }
        else if (!admin)
        {
            basePayload["mapTokenId"] = string.Empty;
            basePayload["mapTokenDisplayName"] = string.Empty;
        }
        return basePayload;
    }

    private Dictionary<string, object> CombatMap0167TokenPayload(BsonDocument token, bool admin, bool includeCoordinates)
    {
        var payload = new Dictionary<string, object>
        {
            ["tokenId"] = GetDocString(token, "Id"),
            ["id"] = GetDocString(token, "Id"),
            ["displayName"] = GetDocString(token, "DisplayName"),
            ["tokenType"] = GetDocString(token, "TokenType"),
            ["linkedEntityType"] = admin ? GetDocString(token, "LinkedEntityType") : string.Empty,
            ["linkedEntityDisplayName"] = GetDocString(token, "LinkedEntityDisplayName"),
            ["iconKey"] = GetDocString(token, "IconKey"),
            ["colorKey"] = GetDocString(token, "ColorKey"),
            ["visibility"] = admin ? GetDocString(token, "Visibility", "Hidden") : "PlayerVisible",
            ["isPlayerVisible"] = string.Equals(GetDocString(token, "Visibility"), "PlayerVisible", StringComparison.OrdinalIgnoreCase),
            ["canJoinCombat"] = CombatMap0167CanJoinCombat(token)
        };
        if (includeCoordinates)
        {
            payload["x"] = GetDocDouble(token, "X", 0d);
            payload["y"] = GetDocDouble(token, "Y", 0d);
            payload["radius"] = GetDocDouble(token, "Radius", 0d);
            payload["size"] = GetDocDouble(token, "Size", 1d);
        }
        return payload;
    }

    private Dictionary<string, object> CombatMap0167SceneMapPayload(BsonDocument map)
        => new()
        {
            ["mapId"] = Combat0166String(map, "Id"),
            ["id"] = Combat0166String(map, "Id"),
            ["name"] = GetDocString(map, "DisplayName"),
            ["widthMeters"] = GetDocDouble(map, "WidthMeters", 0d),
            ["heightMeters"] = GetDocDouble(map, "HeightMeters", 0d),
            ["gridSizeMeters"] = GetDocDouble(map, "GridSizeMeters", 5d),
            ["showGrid"] = GetDocBool(map, "ShowGrid"),
            ["showCoordinates"] = GetDocBool(map, "ShowCoordinates")
        };

    private Dictionary<string, object> CombatMap0167PlayerSceneMapPayload(BsonDocument map)
    {
        var payload = CombatMap0167SceneMapPayload(map);
        payload["description"] = string.Empty;
        return payload;
    }

    private BsonDocument CombatMap0167ResolveAdminCombat(IDictionary<string, object> payload)
    {
        var combatId = FirstNonEmptyWorld(PayloadReader.GetString(payload, "combatId"), PayloadReader.GetString(payload, "id"));
        if (!string.IsNullOrWhiteSpace(combatId)) return Combat0166RequireSessionById(combatId);
        EnsureCombat0166Indexes();
        var campaignId = FirstNonEmptyWorld(PayloadReader.GetString(payload, "campaignId"), Combat0166DefaultCampaignId);
        var sessionId = FirstNonEmptyWorld(PayloadReader.GetString(payload, "sessionId"), Combat0166DefaultSessionId);
        var filter = Builders<BsonDocument>.Filter.Eq("CampaignId", campaignId)
            & Builders<BsonDocument>.Filter.Eq("SessionId", sessionId)
            & Builders<BsonDocument>.Filter.Ne("IsArchived", true)
            & Builders<BsonDocument>.Filter.In("Status", new[] { "setup", "active", "paused" });
        var combat = Combat0166Sessions().Find(filter).Sort(Builders<BsonDocument>.Sort.Descending("UpdatedAtUtc")).FirstOrDefault();
        if (combat == null) throw new KeyNotFoundException("Active combat tracker not found.");
        return combat;
    }

    private BsonDocument CombatMap0167ResolveSceneMap(IDictionary<string, object> payload, BsonDocument combat, bool allowMissing = false)
    {
        var mapId = FirstNonEmptyWorld(
            PayloadReader.GetString(payload, "sceneMapId"),
            PayloadReader.GetString(payload, "mapId"),
            Combat0166String(combat, "SceneMapId"));
        if (string.IsNullOrWhiteSpace(mapId))
        {
            var state = ResolveSceneMap0162SessionState(Combat0166String(combat, "SessionId"), Combat0166String(combat, "CampaignId"));
            mapId = GetDocString(state ?? new BsonDocument(), "ActiveSceneMapId");
        }
        if (string.IsNullOrWhiteSpace(mapId))
        {
            if (allowMissing) return null!;
            throw new KeyNotFoundException("Active scene map is not selected.");
        }
        var identity = _mapIdentityResolver.ResolveSceneMap(mapId);
        if (!identity.IsResolved)
        {
            if (allowMissing) return null!;
            throw identity.Status == MapIdentityResolutionStatus0202.NotFound
                ? new KeyNotFoundException(identity.Message)
                : new InvalidOperationException(identity.Message);
        }
        return identity.CompatibilityProjection!;
    }

    private BsonDocument CombatMap0167RequireSceneToken(IDictionary<string, object> payload, BsonDocument map)
    {
        var tokenId = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "tokenId"), PayloadReader.GetString(payload, "mapTokenId"), PayloadReader.GetString(payload, "id")), 1, 128, "tokenId");
        var token = MapToken0163CollectionRef().Find(Combat0166ActiveIdFilter(tokenId)).FirstOrDefault();
        if (token == null) throw new KeyNotFoundException("Map token not found.");
        if (!string.Equals(GetDocString(token, "MapKind"), MapToken0163KindScene, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Map token must belong to a scene map.");
        if (!string.Equals(GetDocString(token, "MapId"), SceneMap0162CanonicalMapId(map), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Map token belongs to another scene map.");
        return token;
    }

    private BsonDocument CombatMap0167CreateParticipantFromToken(BsonDocument combat, BsonDocument map, BsonDocument token, IDictionary<string, object> payload, string actorUserId)
    {
        var now = DateTime.UtcNow;
        var tokenId = GetDocString(token, "Id");
        var participantId = FirstNonEmptyWorld(PayloadReader.GetString(payload, "participantId"), "combat_participant_" + Guid.NewGuid().ToString("N"));
        var participant = new BsonDocument
        {
            ["_id"] = participantId,
            ["Id"] = participantId,
            ["CombatId"] = Combat0166String(combat, "Id"),
            ["CampaignId"] = Combat0166String(combat, "CampaignId"),
            ["SessionId"] = Combat0166String(combat, "SessionId"),
            ["DisplayName"] = FirstNonEmptyWorld(PayloadReader.GetString(payload, "displayName"), GetDocString(token, "DisplayName"), "Участник"),
            ["ParticipantType"] = CombatMap0167ParticipantTypeFromToken(token),
            ["TeamId"] = FirstNonEmptyWorld(PayloadReader.GetString(payload, "teamId"), "neutral"),
            ["ControllerUserId"] = PayloadReader.GetString(payload, "controllerUserId") ?? string.Empty,
            ["CharacterId"] = FirstNonEmptyWorld(PayloadReader.GetString(payload, "characterId"), GetDocString(token, "LinkedEntityType").Equals("Character", StringComparison.OrdinalIgnoreCase) ? GetDocString(token, "LinkedEntityId") : string.Empty),
            ["VisibilityMode"] = CombatMap0167CombatVisibilityFromToken(token),
            ["IsPlayerVisible"] = string.Equals(CombatMap0167CombatVisibilityFromToken(token), "player_visible", StringComparison.OrdinalIgnoreCase),
            ["InitiativeRoll"] = 0,
            ["InitiativeOrderIndex"] = 9999,
            ["TurnStatus"] = "waiting",
            ["StandardActions"] = 1,
            ["MinorActions"] = 2,
            ["ReactionSlots"] = 1,
            ["ReactionUsedThisRound"] = false,
            ["PublicStateText"] = string.Empty,
            ["PublicNotes"] = GetDocString(token, "DescriptionPlayer"),
            ["GmStateText"] = string.Empty,
            ["GmNotes"] = GetDocString(token, "DescriptionGm"),
            ["SceneMapId"] = Combat0166String(map, "Id"),
            ["MapTokenId"] = tokenId,
            ["MapTokenDisplayName"] = GetDocString(token, "DisplayName"),
            ["MapTokenVisibility"] = CombatMap0167CombatVisibilityFromToken(token),
            ["MapLinkStatus"] = "linked",
            ["MapOverlayState"] = "linked",
            ["MapBadgeText"] = PayloadReader.GetString(payload, "badgeText") ?? string.Empty,
            ["MapBadgeColorKey"] = PayloadReader.GetString(payload, "badgeColorKey") ?? GetDocString(token, "ColorKey"),
            ["MapBadgeVisibility"] = CombatMap0167CombatVisibilityFromToken(token),
            ["LastMapLinkUpdatedUtc"] = now,
            ["CreatedAtUtc"] = now,
            ["CreatedByUserId"] = actorUserId,
            ["UpdatedAtUtc"] = now,
            ["UpdatedByUserId"] = actorUserId,
            ["Revision"] = 1,
            ["IsArchived"] = false
        };
        Combat0166Participants().ReplaceOne(Combat0166AnyIdFilter(participantId), participant, new ReplaceOptions { IsUpsert = true });
        RebuildCombat0166Order(combat);
        return participant;
    }

    private void CombatMap0167ApplyTokenLink(BsonDocument participant, BsonDocument map, BsonDocument token, string actorUserId)
    {
        participant["SceneMapId"] = Combat0166String(map, "Id");
        participant["MapTokenId"] = GetDocString(token, "Id");
        participant["MapTokenDisplayName"] = GetDocString(token, "DisplayName");
        participant["MapTokenVisibility"] = CombatMap0167CombatVisibilityFromToken(token);
        participant["MapLinkStatus"] = "linked";
        participant["MapOverlayState"] = "linked";
        participant["LastMapLinkUpdatedUtc"] = DateTime.UtcNow;
        if (string.IsNullOrWhiteSpace(Combat0166String(participant, "MapBadgeColorKey")))
            participant["MapBadgeColorKey"] = GetDocString(token, "ColorKey");
        Combat0166TouchParticipant(participant, actorUserId);
        Combat0166Participants().ReplaceOne(Combat0166AnyIdFilter(Combat0166String(participant, "Id")), participant);
    }

    private IEnumerable<string> CombatMap0167OverlayWarnings(BsonDocument combat, Dictionary<string, BsonDocument> tokens)
    {
        foreach (var participant in Combat0166ParticipantDocs(Combat0166String(combat, "Id"), includeArchived: false))
        {
            var tokenId = Combat0166String(participant, "MapTokenId");
            if (string.IsNullOrWhiteSpace(tokenId)) continue;
            if (!tokens.TryGetValue(tokenId, out var token))
            {
                token = MapToken0163CollectionRef().Find(Combat0166AnyIdFilter(tokenId)).FirstOrDefault();
                if (token == null)
                    yield return $"Broken link: {Combat0166String(participant, "DisplayName")} -> {tokenId}";
                else if (GetDocBool(token, "IsArchived"))
                    yield return $"Archived token: {Combat0166String(participant, "DisplayName")} -> {tokenId}";
                else
                    yield return $"Broken link: {Combat0166String(participant, "DisplayName")} -> {tokenId}";
            }
            else if (GetDocBool(token, "IsArchived"))
                yield return $"Archived token: {Combat0166String(participant, "DisplayName")} -> {tokenId}";
            else if (!string.Equals(GetDocString(token, "Visibility"), "PlayerVisible", StringComparison.OrdinalIgnoreCase))
                yield return $"Token hidden from players: {Combat0166String(participant, "DisplayName")} -> {GetDocString(token, "DisplayName")}";
        }
    }

    private bool CombatMap0167AdminReadEnabled()
        => Combat0166AdminReadEnabled()
           && _featureFlags.IsEnabled(nameof(CombatFeatureFlags.UseCombatMapTokenLinks))
           && _featureFlags.IsEnabled(nameof(CombatFeatureFlags.UseCombatMapIntegrationGate))
           && _featureFlags.IsEnabled(nameof(CombatFeatureFlags.UseCombatMapOverlay))
           && _featureFlags.IsEnabled(nameof(MapFeatureFlags.UseMapSystemV1))
           && _featureFlags.IsEnabled(nameof(MapFeatureFlags.UseSpaceHierarchyV1))
           && _featureFlags.IsEnabled(nameof(MapFeatureFlags.UseSceneMapV1))
           && _featureFlags.IsEnabled(nameof(MapFeatureFlags.UseSceneMapMarkers))
           && _featureFlags.IsEnabled(nameof(MapFeatureFlags.UseSceneMapSessionLink));

    private bool CombatMap0167AdminWriteEnabled()
        => CombatMap0167AdminReadEnabled()
           && _featureFlags.IsEnabled(nameof(CombatFeatureFlags.UseCombatWriteEndpoints));

    private bool CombatMap0167PlayerReadEnabled()
        => Combat0166PlayerReadEnabled()
           && _featureFlags.IsEnabled(nameof(CombatFeatureFlags.UseCombatMapTokenLinks))
           && _featureFlags.IsEnabled(nameof(CombatFeatureFlags.UseCombatMapIntegrationGate))
           && _featureFlags.IsEnabled(nameof(CombatFeatureFlags.UseCombatMapOverlay))
           && _featureFlags.IsEnabled(nameof(MapFeatureFlags.UseMapSystemV1))
           && _featureFlags.IsEnabled(nameof(MapFeatureFlags.UseSpaceHierarchyV1))
           && _featureFlags.IsEnabled(nameof(MapFeatureFlags.UseSceneMapV1))
           && _featureFlags.IsEnabled(nameof(MapFeatureFlags.UseSceneMapMarkers))
           && _featureFlags.IsEnabled(nameof(MapFeatureFlags.UseSceneMapSessionLink))
           && _featureFlags.IsEnabled(nameof(MapFeatureFlags.UseSceneMapPlayerView));

    private ResponseEnvelope CombatMap0167Disabled(string command)
    {
        _logger.Admin($"combat.map.0167.disabled command={command}");
        return Error("Combat + Map integration is disabled by feature flags.", ResponseStatus.Forbidden, ErrorCode.Forbidden);
    }

    private void CombatMap0167PublishTokenProjectionSync(BsonDocument token, string eventType, string actorUserId, string requestId)
    {
        if (!_featureFlags.IsEnabled(nameof(CombatFeatureFlags.UseCombatSyncEvents))) return;
        if (!string.Equals(GetDocString(token, "MapKind"), MapToken0163KindScene, StringComparison.OrdinalIgnoreCase)) return;
        var tokenId = GetDocString(token, "Id");
        if (string.IsNullOrWhiteSpace(tokenId)) return;
        var participants = Combat0166Participants()
            .Find(Builders<BsonDocument>.Filter.Eq("MapTokenId", tokenId) & Builders<BsonDocument>.Filter.Ne("IsArchived", true))
            .ToList();
        foreach (var participant in participants)
        {
            var combat = Combat0166Sessions().Find(Combat0166ActiveIdFilter(Combat0166String(participant, "CombatId"))).FirstOrDefault();
            if (combat == null) continue;
            TryPublishSyncEvent(eventType, Combat0166String(combat, "SessionId"), "combat_map_overlay", Combat0166String(combat, "Id"), "changed", actorUserId, new Dictionary<string, object>
            {
                ["combatId"] = Combat0166String(combat, "Id"),
                ["participantId"] = Combat0166String(participant, "Id"),
                ["sceneMapId"] = GetDocString(token, "MapId"),
                ["mapTokenId"] = tokenId,
                ["eventType"] = eventType,
                ["sourceOfTruth"] = "map_token_instances"
            }, requestId);
        }
    }

    private static bool CombatMap0167CanJoinCombat(BsonDocument token)
    {
        if (GetDocBool(token, "IsArchived")) return false;
        if (GetDocBool(token, "CanJoinCombat")) return true;
        var tokenType = GetDocString(token, "TokenType").Trim().ToLowerInvariant();
        var linkedType = GetDocString(token, "LinkedEntityType").Trim().ToLowerInvariant();
        return tokenType is "playercharacter" or "player_character" or "companion" or "npc" or "enemy" or "creature" or "vehicle"
            || linkedType is "character" or "npc" or "companion" or "enemy" or "combatparticipant";
    }

    private static bool CombatMap0167CanPlayerSeeLinkedToken(BsonDocument participant, BsonDocument token)
        => Combat0166CanPlayerSeeParticipant(participant)
           && string.Equals(Combat0166String(participant, "MapTokenVisibility"), "player_visible", StringComparison.OrdinalIgnoreCase)
           && !GetDocBool(token, "IsArchived")
           && string.Equals(GetDocString(token, "Visibility"), "PlayerVisible", StringComparison.OrdinalIgnoreCase);

    private static string CombatMap0167CombatVisibilityFromToken(BsonDocument token)
    {
        return GetDocString(token, "Visibility", "Hidden") switch
        {
            "PlayerVisible" => "player_visible",
            "GmOnly" => "gm_only",
            _ => "hidden"
        };
    }

    private static string CombatMap0167LinkStatus(BsonDocument participant, BsonDocument? token)
    {
        if (string.IsNullOrWhiteSpace(Combat0166String(participant, "MapTokenId"))) return "unlinked";
        if (token == null) return "broken_token_missing";
        if (GetDocBool(token, "IsArchived")) return "broken_token_archived";
        if (!string.Equals(Combat0166String(participant, "SceneMapId"), GetDocString(token, "MapId"), StringComparison.OrdinalIgnoreCase)) return "broken_map_mismatch";
        return string.Equals(GetDocString(token, "Visibility"), "PlayerVisible", StringComparison.OrdinalIgnoreCase) ? "linked_player_visible" : "linked_hidden_from_players";
    }

    private static string CombatMap0167ParticipantTypeFromToken(BsonDocument token)
    {
        var tokenType = GetDocString(token, "TokenType").Trim().ToLowerInvariant();
        return tokenType switch
        {
            "playercharacter" or "player_character" => "player_character",
            "companion" => "companion",
            "npc" => "npc",
            "enemy" => "enemy",
            "creature" => "creature",
            "vehicle" => "vehicle",
            _ => "custom"
        };
    }

    private static IEnumerable<string> CombatMap0167StringList(IDictionary<string, object> payload, string key)
    {
        if (!payload.TryGetValue(key, out var raw) || raw == null) yield break;
        if (raw is string s)
        {
            foreach (var part in s.Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries))
                yield return part.Trim();
            yield break;
        }
        if (raw is IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                var value = Convert.ToString(item, CultureInfo.InvariantCulture);
                if (!string.IsNullOrWhiteSpace(value)) yield return value.Trim();
            }
        }
    }
}
