using System;
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
    private const string MapToken0163Collection = "map_token_instances";
    private const string MapToken0205OperationsCollection = "map_token_move_operations";
    private const string MapToken0163KindWorld = "World";
    private const string MapToken0163KindScene = "Scene";

    public ResponseEnvelope MapTokenAdminListForMap0163(CommandContext context)
    {
        RequireAdmin(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var mapKind = NormalizeMapToken0163MapKind(PayloadReader.GetString(payload, "mapKind"));
        if (!MapToken0163AdminEnabled(mapKind))
            return MapToken0163Disabled(context.Request.Command);

        var mapId = RequireLength(PayloadReader.GetString(payload, "mapId"), 1, 128, "mapId");
        var map = ResolveMapToken0163Map(mapKind, mapId);
        if (map == null)
            return Error("map not found", ResponseStatus.NotFound, ErrorCode.NotFound);
        if (mapKind == MapToken0163KindScene)
            mapId = GetDocString(map, "CanonicalMapId", GetDocString(map, "Id"));

        EnsureMapToken0163Indexes();
        var tokens = MapToken0163DocsForMap(mapKind, mapId, includeHidden: true)
            .Select(doc => MapToken0163Payload(doc, admin: true))
            .Cast<object>()
            .ToArray();

        return Ok("Map tokens loaded.", new Dictionary<string, object>
        {
            ["mapKind"] = mapKind,
            ["mapId"] = mapId,
            ["items"] = tokens,
            ["tokens"] = tokens,
            ["count"] = tokens.Length,
            ["sourceCollection"] = MapToken0163Collection
        });
    }

    public ResponseEnvelope MapTokenAdminGet0163(CommandContext context)
    {
        RequireAdmin(context);
        EnsureMapToken0163Indexes();
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var tokenId = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "tokenId"), PayloadReader.GetString(payload, "id")), 1, 128, "tokenId");
        var token = MapToken0163CollectionRef().Find(ActiveIdFilter(tokenId)).FirstOrDefault();
        if (token == null)
            return Error("map token not found", ResponseStatus.NotFound, ErrorCode.NotFound);
        if (!MapToken0163AdminEnabled(GetDocString(token, "MapKind")))
            return MapToken0163Disabled(context.Request.Command);

        return Ok("Map token loaded.", new Dictionary<string, object>
        {
            ["tokenId"] = tokenId,
            ["token"] = MapToken0163Payload(token, admin: true)
        });
    }

    public ResponseEnvelope MapTokenAdminCreate0163(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var mapKind = NormalizeMapToken0163MapKind(PayloadReader.GetString(payload, "mapKind"));
        if (!MapToken0163AdminEnabled(mapKind))
            return MapToken0163Disabled(context.Request.Command);

        var mapId = RequireLength(PayloadReader.GetString(payload, "mapId"), 1, 128, "mapId");
        var map = ResolveMapToken0163Map(mapKind, mapId);
        if (map == null)
            return Error("map not found", ResponseStatus.NotFound, ErrorCode.NotFound);

        EnsureMapToken0163Indexes();
        var tokenId = FirstNonEmptyWorld(PayloadReader.GetString(payload, "tokenId"), PayloadReader.GetString(payload, "id"));
        if (string.IsNullOrWhiteSpace(tokenId))
            tokenId = "map_token_" + Guid.NewGuid().ToString("N");

        var now = DateTime.UtcNow;
        var doc = BuildMapToken0163Doc(map, payload, tokenId, actor.Id, now, existing: null);
        var validation = ValidateMapToken0163(map, doc);
        if (validation != null) return validation;

        MapToken0163CollectionRef().ReplaceOne(IdFilter(tokenId), doc, new ReplaceOptions { IsUpsert = true });
        CombatMap0167PublishTokenProjectionSync(doc, "combat.map.token.moved", actor.Id, context.Request.RequestId);
        _logger.Admin($"map.token.0163.create mapKind={mapKind} mapId={mapId} tokenId={tokenId} actor={actor.Login}");
        return Ok("Map token created.", new Dictionary<string, object>
        {
            ["tokenId"] = tokenId,
            ["token"] = MapToken0163Payload(doc, admin: true)
        });
    }

    public ResponseEnvelope MapTokenAdminUpdate0163(CommandContext context)
    {
        var actor = RequireAdmin(context);
        EnsureMapToken0163Indexes();
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var tokenId = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "tokenId"), PayloadReader.GetString(payload, "id")), 1, 128, "tokenId");
        var existing = MapToken0163CollectionRef().Find(ActiveIdFilter(tokenId)).FirstOrDefault();
        if (existing == null)
            return Error("map token not found", ResponseStatus.NotFound, ErrorCode.NotFound);
        if (!MapToken0163AdminEnabled(GetDocString(existing, "MapKind")))
            return MapToken0163Disabled(context.Request.Command);

        var map = ResolveMapToken0163Map(GetDocString(existing, "MapKind"), GetDocString(existing, "MapId"));
        if (map == null)
            return Error("map not found", ResponseStatus.NotFound, ErrorCode.NotFound);

        var doc = BuildMapToken0163Doc(map, payload, tokenId, actor.Id, DateTime.UtcNow, existing);
        var validation = ValidateMapToken0163(map, doc);
        if (validation != null) return validation;

        MapToken0163CollectionRef().ReplaceOne(IdFilter(tokenId), doc, new ReplaceOptions { IsUpsert = true });
        return Ok("Map token updated.", new Dictionary<string, object>
        {
            ["tokenId"] = tokenId,
            ["token"] = MapToken0163Payload(doc, admin: true)
        });
    }

    public ResponseEnvelope MapTokenAdminMove0163(CommandContext context)
    {
        var actor = RequireAdmin(context);
        EnsureMapToken0163Indexes();
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var tokenId = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "tokenId"), PayloadReader.GetString(payload, "id")), 1, 128, "tokenId");
        var operationId = FirstNonEmptyWorld(PayloadReader.GetString(payload, "operationId"), context.Request.RequestId);
        if (!string.IsNullOrWhiteSpace(operationId))
        {
            var replay = MapToken0205Operations().Find(Builders<BsonDocument>.Filter.Eq("OperationId", operationId)).FirstOrDefault();
            if (replay != null)
            {
                var replayToken = MapToken0163CollectionRef().Find(IdFilter(GetDocString(replay, "TokenId"))).FirstOrDefault();
                if (replayToken == null) return Error("map token not found", ResponseStatus.NotFound, ErrorCode.NotFound);
                return Ok("Map token move already applied.", new Dictionary<string, object>
                {
                    ["tokenId"] = GetDocString(replayToken, "Id"), ["token"] = MapToken0163Payload(replayToken, admin: true),
                    ["alreadyApplied"] = true, ["operationId"] = operationId
                });
            }
        }
        var existing = MapToken0163CollectionRef().Find(ActiveIdFilter(tokenId)).FirstOrDefault();
        if (existing == null)
            return Error("map token not found", ResponseStatus.NotFound, ErrorCode.NotFound);
        if (!MapToken0163AdminEnabled(GetDocString(existing, "MapKind")))
            return MapToken0163Disabled(context.Request.Command);

        var map = ResolveMapToken0163Map(GetDocString(existing, "MapKind"), GetDocString(existing, "MapId"));
        if (map == null)
            return Error("map not found", ResponseStatus.NotFound, ErrorCode.NotFound);

        var now = DateTime.UtcNow;
        var currentRevision = GetDocInt(existing, "Revision", 1);
        var expectedRevision = PayloadReader.GetLong(payload, "expectedRevision");
        if (expectedRevision.HasValue && expectedRevision.Value != currentRevision)
            return Error($"Токен был изменён. Текущая редакция: {currentRevision}; ожидалась: {expectedRevision.Value}.", ResponseStatus.Conflict, ErrorCode.Conflict);
        var doc = new BsonDocument(existing);
        doc["X"] = PayloadReader.GetDouble(payload, "x") ?? PayloadReader.GetDouble(payload, "X") ?? GetDocDouble(existing, "X", 0d);
        doc["Y"] = PayloadReader.GetDouble(payload, "y") ?? PayloadReader.GetDouble(payload, "Y") ?? GetDocDouble(existing, "Y", 0d);
        doc["UpdatedAtUtc"] = now;
        doc["LastMovedAtUtc"] = now;
        doc["UpdatedByUserId"] = actor.Id;
        doc["Revision"] = currentRevision + 1;
        var validation = ValidateMapToken0163(map, doc);
        if (validation != null) return validation;

        var revisionFilter = currentRevision == 1
            ? Builders<BsonDocument>.Filter.Or(Builders<BsonDocument>.Filter.Eq("Revision", currentRevision), Builders<BsonDocument>.Filter.Exists("Revision", false))
            : Builders<BsonDocument>.Filter.Eq("Revision", currentRevision);
        var moveFilter = Builders<BsonDocument>.Filter.And(IdFilter(tokenId), revisionFilter);
        if (MapToken0163CollectionRef().ReplaceOne(moveFilter, doc).ModifiedCount != 1)
            return Error("Токен изменился во время перемещения. Обновите карту и повторите действие.", ResponseStatus.Conflict, ErrorCode.Conflict);
        if (!string.IsNullOrWhiteSpace(operationId))
        {
            try
            {
                MapToken0205Operations().InsertOne(new BsonDocument
                {
                    ["_id"] = operationId, ["OperationId"] = operationId, ["TokenId"] = tokenId,
                    ["FromRevision"] = currentRevision, ["ToRevision"] = currentRevision + 1,
                    ["X"] = GetDocDouble(doc, "X", 0), ["Y"] = GetDocDouble(doc, "Y", 0),
                    ["ActorUserId"] = actor.Id, ["AppliedAtUtc"] = now
                });
            }
            catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey) { }
        }
        CombatMap0167PublishTokenProjectionSync(doc, "combat.map.token.moved", actor.Id, context.Request.RequestId);
        return Ok("Map token moved.", new Dictionary<string, object>
        {
            ["tokenId"] = tokenId,
            ["token"] = MapToken0163Payload(doc, admin: true),
            ["alreadyApplied"] = false,
            ["operationId"] = operationId
        });
    }

    public ResponseEnvelope MapTokenAdminArchive0163(CommandContext context)
    {
        var actor = RequireAdmin(context);
        EnsureMapToken0163Indexes();
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var tokenId = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "tokenId"), PayloadReader.GetString(payload, "id")), 1, 128, "tokenId");
        var existing = MapToken0163CollectionRef().Find(ActiveIdFilter(tokenId)).FirstOrDefault();
        if (existing == null)
            return Error("map token not found", ResponseStatus.NotFound, ErrorCode.NotFound);
        if (!MapToken0163AdminEnabled(GetDocString(existing, "MapKind")))
            return MapToken0163Disabled(context.Request.Command);

        var result = MapToken0163CollectionRef().UpdateOne(ActiveIdFilter(tokenId), Builders<BsonDocument>.Update
            .Set("IsArchived", true)
            .Set("UpdatedAtUtc", DateTime.UtcNow)
            .Set("UpdatedByUserId", actor.Id));
        if (result.MatchedCount == 0)
            return Error("map token not found", ResponseStatus.NotFound, ErrorCode.NotFound);
        var archived = MapToken0163CollectionRef().Find(IdFilter(tokenId)).FirstOrDefault();
        if (archived != null)
            CombatMap0167PublishTokenProjectionSync(archived, "combat.map.visibility.changed", actor.Id, context.Request.RequestId);
        return Ok("Map token archived.", new Dictionary<string, object> { ["tokenId"] = tokenId });
    }

    public ResponseEnvelope MapTokenAdminSetVisibility0163(CommandContext context)
    {
        return MapToken0163SetVisibility(context, NormalizeMapToken0163Visibility(PayloadReader.GetString(context.Request.Payload ?? new Dictionary<string, object>(), "visibility")));
    }

    public ResponseEnvelope MapTokenAdminRevealToPlayers0163(CommandContext context)
    {
        return MapToken0163SetVisibility(context, "PlayerVisible");
    }

    public ResponseEnvelope MapTokenAdminHideFromPlayers0163(CommandContext context)
    {
        return MapToken0163SetVisibility(context, "Hidden");
    }

    public ResponseEnvelope MapTokenPlayerListForActiveWorldMap0163(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!MapToken0163PlayerWorldEnabled())
        {
            _logger.Debug($"map.token.0163.player.world.disabled user={actor.Login}");
            return MapToken0163Disabled(context.Request.Command);
        }

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var sessionId = FirstNonEmptyWorld(PayloadReader.GetString(payload, "sessionId"), WorldMap0161DefaultSessionId);
        var state = ResolveMapToken0163WorldSessionState(sessionId, PayloadReader.GetString(payload, "campaignId"));
        if (state == null)
            return Error("GM has not selected an active world map.", ResponseStatus.NotFound, ErrorCode.NotFound);

        var mapId = GetDocString(state, "ActiveWorldMapId");
        var map = ResolveMapToken0163Map(MapToken0163KindWorld, mapId);
        if (map == null)
            return Error("Active world map is unavailable.", ResponseStatus.NotFound, ErrorCode.NotFound);

        var tokens = MapToken0163DocsForMap(MapToken0163KindWorld, mapId, includeHidden: false)
            .Select(doc => MapToken0163Payload(doc, admin: false))
            .Cast<object>()
            .ToArray();
        return Ok("Active world map tokens loaded.", new Dictionary<string, object>
        {
            ["sessionId"] = sessionId,
            ["mapKind"] = MapToken0163KindWorld,
            ["mapId"] = mapId,
            ["tokens"] = tokens,
            ["items"] = tokens,
            ["count"] = tokens.Length,
            ["builtAtUtc"] = DateTime.UtcNow
        });
    }

    public ResponseEnvelope MapTokenPlayerListForActiveSceneMap0163(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!MapToken0163PlayerSceneEnabled())
        {
            _logger.Debug($"map.token.0163.player.scene.disabled user={actor.Login}");
            return MapToken0163Disabled(context.Request.Command);
        }

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var sessionId = FirstNonEmptyWorld(PayloadReader.GetString(payload, "sessionId"), SceneMap0162DefaultSessionId);
        var state = ResolveSceneMap0162SessionState(sessionId, PayloadReader.GetString(payload, "campaignId"));
        if (state == null)
            return Ok("GM has not selected an active scene map.", new Dictionary<string, object>
            {
                ["hasActiveMap"] = false,
                ["tokens"] = Array.Empty<object>(),
                ["items"] = Array.Empty<object>()
            });

        var mapId = GetDocString(state, "ActiveSceneMapId");
        var map = ResolveMapToken0163Map(MapToken0163KindScene, mapId);
        if (map == null)
            return Error("Active scene map is unavailable.", ResponseStatus.NotFound, ErrorCode.NotFound);

        var tokens = MapToken0163DocsForMap(MapToken0163KindScene, mapId, includeHidden: false)
            .Select(doc => MapToken0163Payload(doc, admin: false))
            .Cast<object>()
            .ToArray();
        return Ok("Active scene map tokens loaded.", new Dictionary<string, object>
        {
            ["hasActiveMap"] = true,
            ["sessionId"] = sessionId,
            ["mapKind"] = MapToken0163KindScene,
            ["mapId"] = mapId,
            ["tokens"] = tokens,
            ["items"] = tokens,
            ["count"] = tokens.Length,
            ["builtAtUtc"] = DateTime.UtcNow
        });
    }

    private ResponseEnvelope MapToken0163SetVisibility(CommandContext context, string visibility)
    {
        var actor = RequireAdmin(context);
        EnsureMapToken0163Indexes();
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var tokenId = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "tokenId"), PayloadReader.GetString(payload, "id")), 1, 128, "tokenId");
        var existing = MapToken0163CollectionRef().Find(ActiveIdFilter(tokenId)).FirstOrDefault();
        if (existing == null)
            return Error("map token not found", ResponseStatus.NotFound, ErrorCode.NotFound);
        if (!MapToken0163AdminEnabled(GetDocString(existing, "MapKind")))
            return MapToken0163Disabled(context.Request.Command);

        var update = Builders<BsonDocument>.Update
            .Set("Visibility", visibility)
            .Set("UpdatedAtUtc", DateTime.UtcNow)
            .Set("UpdatedByUserId", actor.Id);
        MapToken0163CollectionRef().UpdateOne(ActiveIdFilter(tokenId), update);
        var updated = MapToken0163CollectionRef().Find(ActiveIdFilter(tokenId)).FirstOrDefault() ?? existing;
        CombatMap0167PublishTokenProjectionSync(updated, "combat.map.visibility.changed", actor.Id, context.Request.RequestId);
        return Ok("Map token visibility updated.", new Dictionary<string, object>
        {
            ["tokenId"] = tokenId,
            ["visibility"] = visibility,
            ["token"] = MapToken0163Payload(updated, admin: true)
        });
    }

    private Dictionary<string, object>[] MapToken0163PayloadsForMap(string mapKind, string mapId, bool admin)
    {
        EnsureMapToken0163Indexes();
        return MapToken0163DocsForMap(mapKind, mapId, includeHidden: admin)
            .Select(doc => MapToken0163Payload(doc, admin))
            .ToArray();
    }

    private Dictionary<string, object> MapToken0163Payload(BsonDocument token, bool admin)
    {
        var payload = new Dictionary<string, object>
        {
            ["tokenId"] = GetDocString(token, "Id"),
            ["id"] = GetDocString(token, "Id"),
            ["worldId"] = GetDocString(token, "WorldId"),
            ["sessionId"] = GetDocString(token, "SessionId"),
            ["mapKind"] = GetDocString(token, "MapKind"),
            ["mapId"] = GetDocString(token, "MapId"),
            ["displayName"] = GetDocString(token, "DisplayName"),
            ["name"] = GetDocString(token, "DisplayName"),
            ["descriptionPlayer"] = GetDocString(token, "DescriptionPlayer"),
            ["cardDescription"] = GetDocString(token, "DescriptionPlayer"),
            ["tokenType"] = GetDocString(token, "TokenType", "Object"),
            ["linkedEntityType"] = GetDocString(token, "LinkedEntityType", "None"),
            ["linkedEntityId"] = admin ? GetDocString(token, "LinkedEntityId") : string.Empty,
            ["linkedEntityDisplayName"] = GetDocString(token, "LinkedEntityDisplayName"),
            ["x"] = GetDocDouble(token, "X", 0d),
            ["y"] = GetDocDouble(token, "Y", 0d),
            ["radius"] = GetDocDouble(token, "Radius", 0d),
            ["size"] = GetDocDouble(token, "Size", 1d),
            ["rotationDegrees"] = GetDocDouble(token, "RotationDegrees", 0d),
            ["layerKey"] = GetDocString(token, "LayerKey"),
            ["iconKey"] = GetDocString(token, "IconKey"),
            ["colorKey"] = GetDocString(token, "ColorKey"),
            ["visibility"] = GetDocString(token, "Visibility", "Hidden"),
            ["visibilityMode"] = GetDocString(token, "Visibility", "Hidden"),
            ["isPlayerVisible"] = string.Equals(GetDocString(token, "Visibility"), "PlayerVisible", StringComparison.OrdinalIgnoreCase),
            ["canJoinCombat"] = GetDocBool(token, "CanJoinCombat"),
            ["isArchived"] = GetDocBool(token, "IsArchived"),
            ["updatedAtUtc"] = GetDocDate(token, "UpdatedAtUtc"),
            ["lastMovedAtUtc"] = GetDocDate(token, "LastMovedAtUtc")
            ,["revision"] = GetDocInt(token, "Revision", 1)
        };
        if (admin)
        {
            payload["descriptionGm"] = GetDocString(token, "DescriptionGm");
            payload["gmNotes"] = GetDocString(token, "DescriptionGm");
            payload["visibleToAccountIds"] = BsonArrayToStrings(token, "VisibleToAccountIds");
            payload["visibleToCharacterIds"] = BsonArrayToStrings(token, "VisibleToCharacterIds");
        }

        return payload;
    }

    private BsonDocument BuildMapToken0163Doc(BsonDocument map, IDictionary<string, object> payload, string tokenId, string actorUserId, DateTime now, BsonDocument? existing)
    {
        var mapKind = existing == null
            ? NormalizeMapToken0163MapKind(PayloadReader.GetString(payload, "mapKind"))
            : NormalizeMapToken0163MapKind(GetDocString(existing, "MapKind"));
        var visibility = NormalizeMapToken0163Visibility(FirstNonEmptyWorld(
            PayloadReader.GetString(payload, "visibility"),
            PayloadReader.GetString(payload, "visibilityMode"),
            PayloadReader.GetBool(payload, "isPlayerVisible") ? "PlayerVisible" : existing == null ? "Hidden" : GetDocString(existing, "Visibility", "Hidden")));

        var doc = existing != null ? new BsonDocument(existing) : new BsonDocument
        {
            ["_id"] = tokenId,
            ["Id"] = tokenId,
            ["CreatedAtUtc"] = now,
            ["CreatedByUserId"] = actorUserId
        };

        doc["Id"] = tokenId;
        doc["WorldId"] = FirstNonEmptyWorld(PayloadReader.GetString(payload, "worldId"), existing == null ? GetMapToken0163WorldId(map, mapKind) : GetDocString(existing, "WorldId"));
        doc["SessionId"] = FirstNonEmptyWorld(PayloadReader.GetString(payload, "sessionId"), existing == null ? (mapKind == MapToken0163KindScene ? SceneMap0162DefaultSessionId : WorldMap0161DefaultSessionId) : GetDocString(existing, "SessionId"));
        doc["MapKind"] = mapKind;
        doc["MapId"] = mapKind == MapToken0163KindScene
            ? GetDocString(map, "CanonicalMapId", GetDocString(map, "Id"))
            : GetDocString(map, "Id");
        doc["DisplayName"] = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "displayName"), PayloadReader.GetString(payload, "name"), existing == null ? "Token" : GetDocString(existing, "DisplayName")), 1, 160, "displayName");
        doc["DescriptionPlayer"] = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "descriptionPlayer"), PayloadReader.GetString(payload, "publicNotes"), PayloadReader.GetString(payload, "cardDescription"), existing == null ? string.Empty : GetDocString(existing, "DescriptionPlayer")), 0, 4096, "descriptionPlayer");
        doc["DescriptionGm"] = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "descriptionGm"), PayloadReader.GetString(payload, "gmNotes"), existing == null ? string.Empty : GetDocString(existing, "DescriptionGm")), 0, 4096, "descriptionGm");
        doc["TokenType"] = NormalizeMapToken0163TokenType(FirstNonEmptyWorld(PayloadReader.GetString(payload, "tokenType"), existing == null ? "Object" : GetDocString(existing, "TokenType", "Object")));
        doc["LinkedEntityType"] = NormalizeMapToken0163LinkedEntityType(FirstNonEmptyWorld(PayloadReader.GetString(payload, "linkedEntityType"), existing == null ? "None" : GetDocString(existing, "LinkedEntityType", "None")));
        doc["LinkedEntityId"] = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "linkedEntityId"), existing == null ? string.Empty : GetDocString(existing, "LinkedEntityId")), 0, 256, "linkedEntityId");
        doc["LinkedEntityDisplayName"] = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "linkedEntityDisplayName"), existing == null ? string.Empty : GetDocString(existing, "LinkedEntityDisplayName")), 0, 256, "linkedEntityDisplayName");
        doc["X"] = PayloadReader.GetDouble(payload, "x") ?? PayloadReader.GetDouble(payload, "X") ?? (existing == null ? 0d : GetDocDouble(existing, "X", 0d));
        doc["Y"] = PayloadReader.GetDouble(payload, "y") ?? PayloadReader.GetDouble(payload, "Y") ?? (existing == null ? 0d : GetDocDouble(existing, "Y", 0d));
        doc["Radius"] = PayloadReader.GetDouble(payload, "radius") ?? (existing == null ? 0d : GetDocDouble(existing, "Radius", 0d));
        doc["Size"] = PayloadReader.GetDouble(payload, "size") ?? (existing == null ? 1d : GetDocDouble(existing, "Size", 1d));
        doc["RotationDegrees"] = PayloadReader.GetDouble(payload, "rotationDegrees") ?? (existing == null ? 0d : GetDocDouble(existing, "RotationDegrees", 0d));
        doc["LayerKey"] = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "layerKey"), existing == null ? "tokens" : GetDocString(existing, "LayerKey", "tokens")), 0, 128, "layerKey");
        doc["IconKey"] = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "iconKey"), existing == null ? string.Empty : GetDocString(existing, "IconKey")), 0, 128, "iconKey");
        doc["ColorKey"] = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "colorKey"), existing == null ? string.Empty : GetDocString(existing, "ColorKey")), 0, 128, "colorKey");
        doc["Visibility"] = visibility;
        doc["VisibleToAccountIds"] = existing != null && existing.TryGetValue("VisibleToAccountIds", out var accountIds) && accountIds.IsBsonArray ? accountIds.AsBsonArray : new BsonArray();
        doc["VisibleToCharacterIds"] = existing != null && existing.TryGetValue("VisibleToCharacterIds", out var characterIds) && characterIds.IsBsonArray ? characterIds.AsBsonArray : new BsonArray();
        doc["CanJoinCombat"] = PayloadReader.GetBool(payload, "canJoinCombat") || (existing != null && GetDocBool(existing, "CanJoinCombat") && !payload.ContainsKey("canJoinCombat"));
        doc["IsArchived"] = false;
        doc["UpdatedAtUtc"] = now;
        doc["UpdatedByUserId"] = actorUserId;
        doc["Revision"] = existing == null ? 1 : GetDocInt(existing, "Revision", 1) + 1;
        if (existing == null && !doc.Contains("LastMovedAtUtc"))
            doc["LastMovedAtUtc"] = now;
        return doc;
    }

    private ResponseEnvelope? ValidateMapToken0163(BsonDocument map, BsonDocument token)
    {
        var mapKind = NormalizeMapToken0163MapKind(GetDocString(token, "MapKind"));
        var x = GetDocDouble(token, "X", 0d);
        var y = GetDocDouble(token, "Y", 0d);
        var width = mapKind == MapToken0163KindWorld
            ? GetDocInt(map, "WidthUnits", 5000)
            : GetDocInt(map, "WidthMeters", 2000);
        var height = mapKind == MapToken0163KindWorld
            ? GetDocInt(map, "HeightUnits", 3000)
            : GetDocInt(map, "HeightMeters", 2000);
        if (x < 0 || y < 0 || x > width || y > height)
            return Error("token coordinates are outside map bounds", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);
        return null;
    }

    private List<BsonDocument> MapToken0163DocsForMap(string mapKind, string mapId, bool includeHidden)
    {
        EnsureMapToken0163Indexes();
        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("MapKind", NormalizeMapToken0163MapKind(mapKind)),
            Builders<BsonDocument>.Filter.Eq("MapId", mapId),
            Builders<BsonDocument>.Filter.Ne("IsArchived", true));
        if (!includeHidden)
        {
            filter = Builders<BsonDocument>.Filter.And(filter, Builders<BsonDocument>.Filter.Eq("Visibility", "PlayerVisible"));
        }

        return MapToken0163CollectionRef()
            .Find(filter)
            .Sort(Builders<BsonDocument>.Sort.Ascending("DisplayName"))
            .ToList();
    }

    private BsonDocument? ResolveMapToken0163Map(string mapKind, string mapId)
    {
        mapKind = NormalizeMapToken0163MapKind(mapKind);
        if (mapKind == MapToken0163KindWorld)
            return WorldMap0161Definitions().Find(ActiveIdFilter(mapId)).FirstOrDefault();
        var identity = _mapIdentityResolver.ResolveSceneMap(mapId);
        return identity.IsResolved ? identity.CompatibilityProjection : null;
    }

    private BsonDocument? ResolveMapToken0163WorldSessionState(string sessionId, string campaignId)
    {
        var state = WorldMap0161SessionStates().Find(Builders<BsonDocument>.Filter.Eq("SessionId", sessionId)).FirstOrDefault();
        if (state == null && !string.IsNullOrWhiteSpace(campaignId))
        {
            state = WorldMap0161SessionStates()
                .Find(Builders<BsonDocument>.Filter.Eq("CampaignId", campaignId))
                .Sort(Builders<BsonDocument>.Sort.Descending("UpdatedAtUtc"))
                .FirstOrDefault();
        }
        return state;
    }

    private string GetMapToken0163WorldId(BsonDocument map, string mapKind)
    {
        return mapKind == MapToken0163KindWorld
            ? GetDocString(map, "WorldId", "dev_world_0161")
            : GetDocString(map, "WorldId", "dev_world_0162");
    }

    private void EnsureMapToken0163Indexes()
    {
        MapToken0163CollectionRef().Indexes.CreateMany(new[]
        {
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("Id")),
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("WorldId")),
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("SessionId")),
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("MapKind").Ascending("MapId")),
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("Visibility")),
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("IsArchived"))
        });
        MapToken0205Operations().Indexes.CreateOne(new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys.Ascending("OperationId"), new CreateIndexOptions { Unique = true }));
    }

    private IMongoCollection<BsonDocument> MapToken0163CollectionRef() => _mongo.Database.GetCollection<BsonDocument>(MapToken0163Collection);
    private IMongoCollection<BsonDocument> MapToken0205Operations() => _mongo.Database.GetCollection<BsonDocument>(MapToken0205OperationsCollection);

    private bool MapToken0163AdminEnabled(string mapKind)
    {
        mapKind = NormalizeMapToken0163MapKind(mapKind);
        if (!_featureFlags.IsEnabled(nameof(MapFeatureFlags.UseMapSystemV1)) || !_featureFlags.IsEnabled(nameof(MapFeatureFlags.UseSpaceHierarchyV1)))
            return false;
        return mapKind == MapToken0163KindWorld
            ? _featureFlags.IsEnabled(nameof(MapFeatureFlags.UseWorldMapV1)) && _featureFlags.IsEnabled(nameof(MapFeatureFlags.UseWorldMapMarkers))
            : _featureFlags.IsEnabled(nameof(MapFeatureFlags.UseSceneMapV1)) && _featureFlags.IsEnabled(nameof(MapFeatureFlags.UseSceneMapMarkers));
    }

    private bool MapToken0163PlayerWorldEnabled()
        => _featureFlags.IsEnabled(nameof(MapFeatureFlags.UseMapSystemV1))
           && _featureFlags.IsEnabled(nameof(MapFeatureFlags.UseSpaceHierarchyV1))
           && _featureFlags.IsEnabled(nameof(MapFeatureFlags.UseWorldMapV1))
           && _featureFlags.IsEnabled(nameof(MapFeatureFlags.UseWorldMapMarkers))
           && _featureFlags.IsEnabled(nameof(MapFeatureFlags.UseWorldMapPlayerView));

    private bool MapToken0163PlayerSceneEnabled()
        => _featureFlags.IsEnabled(nameof(MapFeatureFlags.UseMapSystemV1))
           && _featureFlags.IsEnabled(nameof(MapFeatureFlags.UseSpaceHierarchyV1))
           && _featureFlags.IsEnabled(nameof(MapFeatureFlags.UseSceneMapV1))
           && _featureFlags.IsEnabled(nameof(MapFeatureFlags.UseSceneMapMarkers))
           && _featureFlags.IsEnabled(nameof(MapFeatureFlags.UseSceneMapSessionLink))
           && _featureFlags.IsEnabled(nameof(MapFeatureFlags.UseSceneMapPlayerView));

    private ResponseEnvelope MapToken0163Disabled(string commandName)
    {
        _logger.Admin($"map.token.0163.disabled command={commandName}");
        return Error("Map token layer is disabled by feature flags.", ResponseStatus.Forbidden, ErrorCode.Forbidden);
    }

    private static string NormalizeMapToken0163MapKind(string? value)
    {
        var input = (value ?? string.Empty).Trim().ToLowerInvariant();
        return input switch
        {
            "world" or "worldmap" or "world_map" => MapToken0163KindWorld,
            "scene" or "scenemap" or "scene_map" or "local" => MapToken0163KindScene,
            _ => MapToken0163KindScene
        };
    }

    private static string NormalizeMapToken0163Visibility(string? value)
    {
        var input = (value ?? string.Empty).Trim().ToLowerInvariant();
        return input switch
        {
            "playervisible" or "player_visible" or "public" or "visible" or "party" => "PlayerVisible",
            "gmonly" or "gm_only" or "gm" or "admin" => "GmOnly",
            "hidden" or "server_only" => "Hidden",
            _ => "Hidden"
        };
    }

    private static string NormalizeMapToken0163TokenType(string? value)
    {
        var input = (value ?? string.Empty).Trim().ToLowerInvariant();
        return input switch
        {
            "party" => "Party",
            "playercharacter" or "player_character" or "character" => "PlayerCharacter",
            "companion" => "Companion",
            "npc" => "Npc",
            "enemy" => "Enemy",
            "object" or "item" => "Object",
            "hazard" => "Hazard",
            "objective" => "Objective",
            "vehicle" => "Vehicle",
            "gmnote" or "gm_note" => "GmNote",
            _ => "Object"
        };
    }

    private static string NormalizeMapToken0163LinkedEntityType(string? value)
    {
        var input = (value ?? string.Empty).Trim().ToLowerInvariant();
        return input switch
        {
            "character" => "Character",
            "npc" => "Npc",
            "companion" => "Companion",
            "enemy" => "Enemy",
            "object" or "item" => "Object",
            _ => "None"
        };
    }

    private static string[] BsonArrayToStrings(BsonDocument doc, string name)
    {
        if (!doc.TryGetValue(name, out var value) || !value.IsBsonArray)
            return Array.Empty<string>();
        return value.AsBsonArray.Select(item => item.ToString()).ToArray();
    }
}
