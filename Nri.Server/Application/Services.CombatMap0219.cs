using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Bson;
using MongoDB.Driver;
using Nri.Server.Application.Services;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;
using Nri.Shared.Utilities;

namespace Nri.Server.Application;

public partial class ServiceHub
{
    private bool TryResolveCombatMap0219Encounter(IDictionary<string, object> payload, out CombatEncounterState encounter)
    {
        var encounterId = FirstNonEmptyWorld(
            PayloadReader.GetString(payload, "encounterId"),
            PayloadReader.GetString(payload, "combatId"));
        encounter = string.IsNullOrWhiteSpace(encounterId)
            ? null!
            : _repositories.CombatEncounters.GetByIdAsync(encounterId).GetAwaiter().GetResult()!;
        return encounter != null;
    }

    private CombatEncounterState? CombatMap0219ResolvePlayerEncounter(IDictionary<string, object> payload, UserAccount actor)
    {
        var request = ParseCombatPlayerSnapshotRequest(payload);
        var encounter = ResolvePlayerEncounter(request, actor);
        if (encounter == null) return null;
        var participants = _repositories.CombatParticipants.ListByEncounterAsync(encounter.Id, 500).GetAwaiter().GetResult().ToList();
        return ResolvePlayerParticipant(actor, request, participants) == null ? null : encounter;
    }

    private BsonDocument CombatMap0219ResolveSceneMap(CombatEncounterState encounter, IDictionary<string, object> payload)
    {
        var mapId = FirstNonEmptyWorld(
            PayloadReader.GetString(payload, "sceneMapId"),
            PayloadReader.GetString(payload, "mapId"));
        if (string.IsNullOrWhiteSpace(mapId))
        {
            var state = ResolveSceneMap0162SessionState(encounter.SessionId, encounter.CampaignId);
            mapId = GetDocString(state ?? new BsonDocument(), "ActiveSceneMapId");
        }
        if (string.IsNullOrWhiteSpace(mapId)) throw new KeyNotFoundException("Active scene map is not selected.");
        var identity = _mapIdentityResolver.ResolveSceneMap(mapId);
        if (!identity.IsResolved || identity.CompatibilityProjection == null)
            throw new KeyNotFoundException("Active scene map is unavailable.");
        return identity.CompatibilityProjection;
    }

    private Dictionary<string, object> CombatMap0219AdminOverlayPayload(CombatEncounterState encounter, IDictionary<string, object> payload)
    {
        var map = CombatMap0219ResolveSceneMap(encounter, payload);
        var mapId = SceneMap0162CanonicalMapId(map);
        var tokenDocs = MapToken0163DocsForMap(MapToken0163KindScene, mapId, includeHidden: true)
            .ToDictionary(x => GetDocString(x, "Id"), StringComparer.OrdinalIgnoreCase);
        var participants = _repositories.CombatParticipants.ListByEncounterAsync(encounter.Id, 500).GetAwaiter().GetResult().ToList();
        var participantPayloads = participants.Select(CombatMap0219AdminParticipantPayload).Cast<object>().ToArray();
        var overlays = participants.Select(participant =>
        {
            tokenDocs.TryGetValue(participant.MapTokenId ?? string.Empty, out var token);
            var overlay = CombatMap0219ParticipantOverlayPayload(participant, token, admin: true);
            overlay["isCurrentTurn"] = string.Equals(participant.Id, encounter.ActiveParticipantId, StringComparison.OrdinalIgnoreCase);
            return overlay;
        }).Cast<object>().ToArray();

        return new Dictionary<string, object>
        {
            ["combat"] = CombatEncounterSummaryPayload(CombatEncounterManagementService.ToEncounterSummary(encounter)),
            ["encounter"] = CombatEncounterSummaryPayload(CombatEncounterManagementService.ToEncounterSummary(encounter)),
            ["combatId"] = encounter.Id,
            ["sceneMap"] = CombatMap0167SceneMapPayload(map),
            ["sceneMapId"] = mapId,
            ["participants"] = participantPayloads,
            ["combatTokens"] = overlays,
            ["tokens"] = tokenDocs.Values.Select(x => CombatMap0167TokenPayload(x, admin: true, includeCoordinates: true)).Cast<object>().ToArray(),
            ["tileLayers"] = SceneMap0164TileLayerPayloadsForMap(mapId, admin: true).Cast<object>().ToArray(),
            ["tilePatches"] = SceneMap0164TilePatchPayloadsForMap(mapId, admin: true).Cast<object>().ToArray(),
            ["assetInstances"] = SceneMap0164AssetInstancePayloadsForMap(mapId, admin: true).Cast<object>().ToArray(),
            ["warnings"] = CombatMap0219Warnings(participants, tokenDocs).Cast<object>().ToArray(),
            ["sourceOfTruth"] = "combat_participants links; map_token_instances coordinates",
            ["builtAtUtc"] = DateTime.UtcNow
        };
    }

    private Dictionary<string, object> CombatMap0219JoinableTokensPayload(CombatEncounterState encounter, IDictionary<string, object> payload)
    {
        var map = CombatMap0219ResolveSceneMap(encounter, payload);
        var participants = _repositories.CombatParticipants.ListByEncounterAsync(encounter.Id, 500).GetAwaiter().GetResult();
        var linked = participants.Select(x => x.MapTokenId).Where(x => !string.IsNullOrWhiteSpace(x)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var includeLinked = PayloadReader.GetBool(payload, "includeAlreadyLinked");
        var tokens = MapToken0163DocsForMap(MapToken0163KindScene, SceneMap0162CanonicalMapId(map), includeHidden: true)
            .Where(CombatMap0167CanJoinCombat)
            .Where(x => includeLinked || !linked.Contains(GetDocString(x, "Id")))
            .Select(x => CombatMap0167TokenPayload(x, admin: true, includeCoordinates: true))
            .Cast<object>()
            .ToArray();
        return new Dictionary<string, object>
        {
            ["combat"] = new Dictionary<string, object>
            {
                ["combatId"] = encounter.Id,
                ["currentParticipantId"] = encounter.ActiveParticipantId,
                ["roundNumber"] = encounter.RoundNumber,
                ["status"] = encounter.Status
            },
            ["combatId"] = encounter.Id,
            ["sceneMapId"] = SceneMap0162CanonicalMapId(map),
            ["tokens"] = tokens,
            ["count"] = tokens.Length,
            ["alreadyLinkedCount"] = linked.Count
        };
    }

    private ResponseEnvelope CombatMap0219AddParticipantFromToken(CommandContext context, UserAccount actor, CombatEncounterState encounter, IDictionary<string, object> payload)
    {
        var map = CombatMap0219ResolveSceneMap(encounter, payload);
        var token = CombatMap0167RequireSceneToken(payload, map);
        if (!CombatMap0167CanJoinCombat(token))
            return Error("This map token cannot join combat.", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);
        var visibility = CombatMap0167CombatVisibilityFromToken(token);
        var linkedType = GetDocString(token, "LinkedEntityType");
        var summary = CombatV1Service().AddParticipantAsync(new CombatParticipantAddRequest
        {
            EncounterId = encounter.Id,
            CharacterId = string.Equals(linkedType, "Character", StringComparison.OrdinalIgnoreCase) ? GetDocString(token, "LinkedEntityId") : string.Empty,
            DisplayName = FirstNonEmptyWorld(PayloadReader.GetString(payload, "displayName"), GetDocString(token, "DisplayName"), "Участник"),
            ParticipantType = CombatMap0167ParticipantTypeFromToken(token),
            TeamId = FirstNonEmptyWorld(PayloadReader.GetString(payload, "teamId"), "neutral"),
            ControllerUserId = PayloadReader.GetString(payload, "controllerUserId") ?? string.Empty,
            IsNpc = !string.Equals(CombatMap0167ParticipantTypeFromToken(token), CombatParticipantTypes.PlayerCharacter, StringComparison.OrdinalIgnoreCase),
            IsPlayerControlled = string.Equals(CombatMap0167ParticipantTypeFromToken(token), CombatParticipantTypes.PlayerCharacter, StringComparison.OrdinalIgnoreCase),
            IsHidden = !string.Equals(visibility, "player_visible", StringComparison.OrdinalIgnoreCase),
            RequestId = FirstNonEmptyWorld(context.Request.RequestId, "combat-map-add-" + Guid.NewGuid().ToString("N"))
        }, actor).GetAwaiter().GetResult();
        var participant = _repositories.CombatParticipants.GetByIdAsync(summary.Id).GetAwaiter().GetResult()!;
        CombatMap0219ApplyTokenLink(participant, map, token);
        _repositories.CombatParticipants.UpsertAsync(participant).GetAwaiter().GetResult();
        _logger.Admin($"combat.v1.map.participant.added encounterId={encounter.Id} participantId={participant.Id}");
        return Ok("Participant added from map token.", CombatMap0219AdminOverlayPayload(encounter, payload));
    }

    private ResponseEnvelope CombatMap0219LinkParticipantToken(CommandContext context, UserAccount actor, CombatEncounterState encounter, IDictionary<string, object> payload)
    {
        var participant = CombatMap0219RequireParticipant(encounter, payload);
        var map = CombatMap0219ResolveSceneMap(encounter, payload);
        var token = CombatMap0167RequireSceneToken(payload, map);
        CombatMap0219ApplyTokenLink(participant, map, token);
        _repositories.CombatParticipants.UpsertAsync(participant).GetAwaiter().GetResult();
        _logger.Admin($"combat.v1.map.token.linked encounterId={encounter.Id} participantId={participant.Id} actor={actor.Id}");
        return Ok("Participant token linked.", CombatMap0219AdminOverlayPayload(encounter, payload));
    }

    private ResponseEnvelope CombatMap0219UnlinkParticipantToken(CommandContext context, UserAccount actor, CombatEncounterState encounter, IDictionary<string, object> payload)
    {
        var participant = CombatMap0219RequireParticipant(encounter, payload);
        participant.SceneMapId = string.Empty;
        participant.MapTokenId = string.Empty;
        participant.MapTokenDisplayName = string.Empty;
        participant.MapTokenVisibility = "hidden";
        participant.MapLinkStatus = "unlinked";
        participant.MapBadgeText = string.Empty;
        participant.MapBadgeColorKey = string.Empty;
        _repositories.CombatParticipants.UpsertAsync(participant).GetAwaiter().GetResult();
        _logger.Admin($"combat.v1.map.token.unlinked encounterId={encounter.Id} participantId={participant.Id} actor={actor.Id}");
        return Ok("Participant token unlinked.", CombatMap0219AdminOverlayPayload(encounter, payload));
    }

    private ResponseEnvelope CombatMap0219SyncVisibility(CommandContext context, UserAccount actor, CombatEncounterState encounter, IDictionary<string, object> payload)
    {
        var participant = CombatMap0219RequireParticipant(encounter, payload);
        if (string.IsNullOrWhiteSpace(participant.MapTokenId))
            return Error("Participant is not linked to a map token.", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);
        var token = MapToken0163CollectionRef().Find(Combat0166ActiveIdFilter(participant.MapTokenId)).FirstOrDefault();
        if (token == null) return Error("Linked map token not found.", ResponseStatus.NotFound, ErrorCode.NotFound);
        participant.MapTokenVisibility = CombatMap0167CombatVisibilityFromToken(token);
        participant.IsHidden = !string.Equals(participant.MapTokenVisibility, "player_visible", StringComparison.OrdinalIgnoreCase);
        participant.VisibilityState = participant.IsHidden ? CombatVisibilityIds.GmOnly : CombatVisibilityIds.Public;
        participant.MapLinkStatus = CombatMap0219LinkStatus(participant, token);
        _repositories.CombatParticipants.UpsertAsync(participant).GetAwaiter().GetResult();
        _logger.Admin($"combat.v1.map.visibility.synced encounterId={encounter.Id} participantId={participant.Id} actor={actor.Id}");
        return Ok("Participant visibility synchronized from token.", CombatMap0219AdminOverlayPayload(encounter, payload));
    }

    private Dictionary<string, object> CombatMap0219PlayerOverlayPayload(CombatEncounterState encounter, IDictionary<string, object> payload, UserAccount actor)
    {
        var map = CombatMap0219ResolveSceneMap(encounter, payload);
        var mapId = SceneMap0162CanonicalMapId(map);
        var projection = _playerMapProjectionService.BuildSceneMap(mapId, new PlayerMapProjectionContext0204
        {
            ActorUserId = actor.Id,
            CampaignId = encounter.CampaignId,
            SessionId = encounter.SessionId,
            IncludeMarkers = true
        });
        if (!projection.Success) throw new KeyNotFoundException(projection.Message);
        var projectedMap = PayloadReader.GetDictionary(projection.Payload, "map") ?? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        var tokens = MapToken0163DocsForMap(MapToken0163KindScene, mapId, includeHidden: false)
            .ToDictionary(x => GetDocString(x, "Id"), StringComparer.OrdinalIgnoreCase);
        var visibleParticipants = _repositories.CombatParticipants.ListByEncounterAsync(encounter.Id, 500).GetAwaiter().GetResult()
            .Where(x => !x.IsHidden && string.Equals(x.VisibilityState, CombatVisibilityIds.Public, StringComparison.OrdinalIgnoreCase));
        var overlays = visibleParticipants.Select(participant =>
        {
            if (string.IsNullOrWhiteSpace(participant.MapTokenId) || !tokens.TryGetValue(participant.MapTokenId, out var token)) return null;
            if (!string.Equals(participant.MapTokenVisibility, "player_visible", StringComparison.OrdinalIgnoreCase)) return null;
            var overlay = CombatMap0219ParticipantOverlayPayload(participant, token, admin: false);
            overlay["isCurrentTurn"] = string.Equals(participant.Id, encounter.ActiveParticipantId, StringComparison.OrdinalIgnoreCase);
            return overlay;
        }).Where(x => x != null).Cast<object>().ToArray();
        return new Dictionary<string, object>
        {
            ["combat"] = new Dictionary<string, object>
            {
                ["combatId"] = encounter.Id,
                ["currentParticipantId"] = encounter.ActiveParticipantId,
                ["roundNumber"] = encounter.RoundNumber,
                ["status"] = encounter.Status
            },
            ["combatId"] = encounter.Id,
            ["sceneMap"] = projectedMap,
            ["sceneMapId"] = mapId,
            ["combatTokens"] = overlays,
            ["tileLayers"] = projectedMap.TryGetValue("tileLayers", out var layers) ? layers : Array.Empty<object>(),
            ["tilePatches"] = projectedMap.TryGetValue("tilePatches", out var patches) ? patches : Array.Empty<object>(),
            ["assetInstances"] = projectedMap.TryGetValue("assetInstances", out var assets) ? assets : Array.Empty<object>(),
            ["visibleTokenCount"] = overlays.Length,
            ["warnings"] = Array.Empty<object>(),
            ["builtAtUtc"] = DateTime.UtcNow
        };
    }

    private Dictionary<string, object> CombatMap0219LinkAuditPayload(CombatEncounterState encounter, IDictionary<string, object> payload)
    {
        var map = CombatMap0219ResolveSceneMap(encounter, payload);
        var participants = _repositories.CombatParticipants.ListByEncounterAsync(encounter.Id, 500).GetAwaiter().GetResult();
        return new Dictionary<string, object>
        {
            ["combatId"] = encounter.Id,
            ["sceneMapId"] = SceneMap0162CanonicalMapId(map),
            ["participants"] = participants.Select(x => (object)new Dictionary<string, object>
            {
                ["participantId"] = x.Id,
                ["participantName"] = x.DisplayName,
                ["mapTokenId"] = x.MapTokenId,
                ["mapTokenDisplayName"] = x.MapTokenDisplayName,
                ["linkStatus"] = x.MapLinkStatus,
                ["sourceOfTruth"] = "map_token_instances.X/Y"
            }).ToArray(),
            ["sourceCollections"] = new object[] { "combat_participants", MapToken0163Collection, SceneMap0162DefinitionsCollection }
        };
    }

    private CombatParticipantState CombatMap0219RequireParticipant(CombatEncounterState encounter, IDictionary<string, object> payload)
    {
        var participantId = PayloadReader.GetString(payload, "participantId") ?? string.Empty;
        var participant = _repositories.CombatParticipants.GetByIdAsync(participantId).GetAwaiter().GetResult()
            ?? throw new KeyNotFoundException("Combat participant not found.");
        if (!string.Equals(participant.EncounterId, encounter.Id, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Combat participant belongs to another encounter.");
        return participant;
    }

    private static void CombatMap0219ApplyTokenLink(CombatParticipantState participant, BsonDocument map, BsonDocument token)
    {
        participant.SceneMapId = SceneMap0162CanonicalMapId(map);
        participant.MapTokenId = GetDocString(token, "Id");
        participant.MapTokenDisplayName = GetDocString(token, "DisplayName");
        participant.MapTokenVisibility = CombatMap0167CombatVisibilityFromToken(token);
        participant.MapLinkStatus = "linked";
        participant.MapBadgeText = participant.DisplayName;
        participant.MapBadgeColorKey = GetDocString(token, "ColorKey");
    }

    private static Dictionary<string, object> CombatMap0219AdminParticipantPayload(CombatParticipantState participant)
        => CombatParticipantSummaryPayload(CombatEncounterManagementService.ToParticipantSummary(participant));

    private static Dictionary<string, object> CombatMap0219ParticipantOverlayPayload(CombatParticipantState participant, BsonDocument? token, bool admin)
    {
        var result = new Dictionary<string, object>
        {
            ["participantId"] = participant.Id,
            ["participantName"] = participant.DisplayName,
            ["displayName"] = participant.MapTokenDisplayName,
            ["sceneMapId"] = participant.SceneMapId,
            ["linkStatus"] = CombatMap0219LinkStatus(participant, token),
            ["isCurrentTurn"] = false,
            ["mapBadgeText"] = participant.MapBadgeText,
            ["mapBadgeColorKey"] = participant.MapBadgeColorKey
        };
        if (token != null)
        {
            result["mapTokenId"] = GetDocString(token, "Id");
            result["mapTokenDisplayName"] = GetDocString(token, "DisplayName");
            result["tokenType"] = GetDocString(token, "TokenType");
            result["x"] = GetDocDouble(token, "X", 0d);
            result["y"] = GetDocDouble(token, "Y", 0d);
            result["radius"] = GetDocDouble(token, "Radius", 0d);
            result["size"] = GetDocDouble(token, "Size", 1d);
            result["iconKey"] = GetDocString(token, "IconKey");
            result["colorKey"] = GetDocString(token, "ColorKey");
            result["tokenVisibility"] = admin ? GetDocString(token, "Visibility", "Hidden") : "PlayerVisible";
            result["sourceOfTruth"] = "map_token_instances.X/Y";
        }
        return result;
    }

    private static string CombatMap0219LinkStatus(CombatParticipantState participant, BsonDocument? token)
    {
        if (string.IsNullOrWhiteSpace(participant.MapTokenId)) return "unlinked";
        if (token == null) return "broken_token_missing";
        if (GetDocBool(token, "IsArchived")) return "broken_token_archived";
        if (!string.Equals(participant.SceneMapId, GetDocString(token, "MapId"), StringComparison.OrdinalIgnoreCase)) return "broken_map_mismatch";
        return string.Equals(GetDocString(token, "Visibility"), "PlayerVisible", StringComparison.OrdinalIgnoreCase)
            ? "linked_player_visible"
            : "linked_hidden_from_players";
    }

    private static IEnumerable<string> CombatMap0219Warnings(IEnumerable<CombatParticipantState> participants, IDictionary<string, BsonDocument> tokens)
    {
        foreach (var participant in participants)
        {
            if (string.IsNullOrWhiteSpace(participant.MapTokenId)) continue;
            if (!tokens.TryGetValue(participant.MapTokenId, out var token))
                yield return $"Токен участника «{participant.DisplayName}» недоступен.";
            else if (GetDocBool(token, "IsArchived"))
                yield return $"Токен участника «{participant.DisplayName}» архивирован.";
        }
    }

    private ResponseEnvelope CombatMap0219MovePlayerToken(
        CommandContext context,
        UserAccount actor,
        IDictionary<string, object> payload)
    {
        var encounter = CombatMap0219ResolvePlayerEncounter(payload, actor);
        if (encounter == null)
            return Error("Активный бой для игрока не найден.", ResponseStatus.NotFound, ErrorCode.NotFound);

        var request = ParseCombatPlayerSnapshotRequest(payload);
        var participants = _repositories.CombatParticipants.ListByEncounterAsync(encounter.Id, 500).GetAwaiter().GetResult().ToList();
        var participant = ResolvePlayerParticipant(actor, request, participants);
        if (participant == null)
            return Error("Управляемый участник боя не найден.", ResponseStatus.NotFound, ErrorCode.NotFound);
        if (!string.Equals(encounter.ActiveParticipantId, participant.Id, StringComparison.OrdinalIgnoreCase))
            return Error("Перемещать токен можно только в свой ход.", ResponseStatus.Conflict, ErrorCode.Conflict);
        if (string.IsNullOrWhiteSpace(participant.MapTokenId))
            return Error("У участника нет связанного токена карты.", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);

        var operationId = FirstNonEmptyWorld(PayloadReader.GetString(payload, "operationId"), context.Request.RequestId);
        if (string.IsNullOrWhiteSpace(operationId))
            return Error("Для перемещения требуется идентификатор операции.", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);

        var token = MapToken0163CollectionRef().Find(Combat0166ActiveIdFilter(participant.MapTokenId)).FirstOrDefault();
        if (token == null)
            return Error("Связанный токен карты не найден.", ResponseStatus.NotFound, ErrorCode.NotFound);
        var map = CombatMap0219ResolveSceneMap(encounter, payload);
        var mapId = SceneMap0162CanonicalMapId(map);
        if (!string.Equals(GetDocString(token, "MapId"), mapId, StringComparison.OrdinalIgnoreCase))
            return Error("Токен связан с другой картой сцены.", ResponseStatus.Conflict, ErrorCode.Conflict);

        var targetX = PayloadReader.GetDouble(payload, "x");
        var targetY = PayloadReader.GetDouble(payload, "y");
        if (!targetX.HasValue || !targetY.HasValue || double.IsNaN(targetX.Value) || double.IsNaN(targetY.Value)
            || double.IsInfinity(targetX.Value) || double.IsInfinity(targetY.Value))
            return Error("Укажите корректные координаты перемещения.", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);

        var mapWidth = GetDocDouble(map, "WidthMeters", 0d);
        var mapHeight = GetDocDouble(map, "HeightMeters", 0d);
        if (targetX.Value < 0 || targetY.Value < 0 || targetX.Value > mapWidth || targetY.Value > mapHeight)
            return Error("Точка перемещения находится за границами карты.", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);

        var fromX = GetDocDouble(token, "X", 0d);
        var fromY = GetDocDouble(token, "Y", 0d);
        var distance = Math.Sqrt(Math.Pow(targetX.Value - fromX, 2) + Math.Pow(targetY.Value - fromY, 2));
        var gridMeters = Math.Max(1d, GetDocDouble(map, "GridSizeMeters", 5d));
        var maximumMoveMeters = Math.Max(30d, gridMeters * 3d);
        if (distance > maximumMoveMeters + 0.001d)
            return Error($"За одно действие можно переместиться не более чем на {maximumMoveMeters:0.##} м.", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);

        var action = CombatV1ActionEconomyService().DeclareActionAsync(new CombatActionDeclareRequest
        {
            EncounterId = encounter.Id,
            ActorParticipantId = participant.Id,
            ActionType = CombatActionTypes.Move,
            ActionName = "Перемещение по карте",
            TargetLocationSummary = $"{targetX.Value:0.##}; {targetY.Value:0.##} м",
            PayloadSummary = new Dictionary<string, object>
            {
                ["mapTokenId"] = participant.MapTokenId,
                ["fromX"] = fromX,
                ["fromY"] = fromY,
                ["toX"] = targetX.Value,
                ["toY"] = targetY.Value,
                ["distanceMeters"] = distance
            },
            RequestId = operationId
        }, actor).GetAwaiter().GetResult();

        if (action.AlreadyApplied)
        {
            return Ok("Перемещение уже было применено.", new Dictionary<string, object>
            {
                ["movementActionResolved"] = true,
                ["alreadyApplied"] = true,
                ["operationId"] = operationId,
                ["actionHalvesAfter"] = action.ActionPointsRemaining,
                ["mapTokenCoordinateChanged"] = false,
                ["combatParticipantCoordinateCopied"] = false,
                ["overlay"] = CombatMap0219PlayerOverlayPayload(encounter, payload, actor),
                ["sourceOfTruth"] = "map_token_instances.X/Y"
            });
        }

        var currentRevision = GetDocInt(token, "Revision", 1);
        var now = DateTime.UtcNow;
        var movedToken = new BsonDocument(token)
        {
            ["X"] = targetX.Value,
            ["Y"] = targetY.Value,
            ["Revision"] = currentRevision + 1,
            ["UpdatedAtUtc"] = now,
            ["LastMovedAtUtc"] = now,
            ["UpdatedByUserId"] = actor.Id
        };
        var validation = ValidateMapToken0163(map, movedToken);
        if (validation != null) return validation;
        var revisionFilter = currentRevision == 1
            ? Builders<BsonDocument>.Filter.Or(Builders<BsonDocument>.Filter.Eq("Revision", currentRevision), Builders<BsonDocument>.Filter.Exists("Revision", false))
            : Builders<BsonDocument>.Filter.Eq("Revision", currentRevision);
        var moved = MapToken0163CollectionRef().ReplaceOne(
            Builders<BsonDocument>.Filter.And(Combat0166ActiveIdFilter(participant.MapTokenId), revisionFilter),
            movedToken).ModifiedCount == 1;
        if (!moved)
            return Error("Токен изменился во время перемещения. Обновите карту.", ResponseStatus.Conflict, ErrorCode.Conflict);

        var completed = CombatV1ActionEconomyService().CompleteActionAsync(new CombatActionCompleteRequest
        {
            EncounterId = encounter.Id,
            ActionId = action.ActionId,
            ResultStatus = CombatActionStatuses.Completed,
            Message = $"{participant.DisplayName} перемещается на {distance:0.##} м.",
            RequestId = operationId + ":complete"
        }, actor).GetAwaiter().GetResult();
        _logger.Admin($"combat.map.player.move.done encounterId={encounter.Id} participantId={participant.Id} tokenId={participant.MapTokenId} distanceMeters={distance:0.##}");

        return Ok("Токен перемещён.", new Dictionary<string, object>
        {
            ["movementActionResolved"] = true,
            ["alreadyApplied"] = false,
            ["operationId"] = operationId,
            ["actionHalvesBefore"] = action.ActionPointsRemaining + 1,
            ["actionHalvesAfter"] = completed.ActionPointsRemaining,
            ["fromX"] = fromX,
            ["fromY"] = fromY,
            ["toX"] = targetX.Value,
            ["toY"] = targetY.Value,
            ["distanceMeters"] = distance,
            ["mapTokenCoordinateChanged"] = true,
            ["combatParticipantCoordinateCopied"] = false,
            ["overlay"] = CombatMap0219PlayerOverlayPayload(encounter, payload, actor),
            ["sourceOfTruth"] = "map_token_instances.X/Y"
        });
    }
}
