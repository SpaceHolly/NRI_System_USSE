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
    private const string SceneMap0162DefaultMapId = "scene_map_default_0162";
    private const string SceneMap0162DefaultWorldId = "dev_world_0162";
    private const string SceneMap0162DefaultSessionId = "dev_session_0162";
    private const string SceneMap0162DefinitionsCollection = "scene_map_definitions";
    private const string SceneMap0162MarkersCollection = "scene_map_markers";
    private const string SceneMap0162SessionStatesCollection = "session_scene_map_states";

    public ResponseEnvelope SceneMapAdminList0162(CommandContext context)
    {
        RequireAdmin(context);
        if (!SceneMap0162AdminEnabled())
            return SceneMap0162Disabled(context.Request.Command);

        EnsureSceneMap0162Indexes();
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var includeArchived = PayloadReader.GetBool(payload, "includeArchived");
        var sessionId = FirstNonEmptyWorld(PayloadReader.GetString(payload, "sessionId"), SceneMap0162DefaultSessionId);
        var activeState = SceneMap0162SessionStates().Find(Builders<BsonDocument>.Filter.Eq("SessionId", sessionId)).FirstOrDefault();
        var activeMapId = GetDocString(activeState ?? new BsonDocument(), "ActiveSceneMapId");
        var filter = includeArchived
            ? Builders<BsonDocument>.Filter.Empty
            : Builders<BsonDocument>.Filter.Ne("IsArchived", true);
        var maps = SceneMap0162Definitions()
            .Find(filter)
            .Sort(Builders<BsonDocument>.Sort.Ascending("DisplayName"))
            .Limit(300)
            .ToList()
            .Select(doc => SceneMap0162ListPayload(doc, activeMapId))
            .Cast<object>()
            .ToArray();

        return Ok("Scene maps loaded.", new Dictionary<string, object>
        {
            ["items"] = maps,
            ["count"] = maps.Length,
            ["activeMapId"] = activeMapId,
            ["hasActiveMap"] = !string.IsNullOrWhiteSpace(activeMapId)
        });
    }

    public ResponseEnvelope SceneMapAdminCreate0162(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!SceneMap0162AdminEnabled())
            return SceneMap0162Disabled(context.Request.Command);

        EnsureSceneMap0162Indexes();
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var now = DateTime.UtcNow;
        var mapId = FirstNonEmptyWorld(PayloadReader.GetString(payload, "mapId"), PayloadReader.GetString(payload, "id"));
        if (string.IsNullOrWhiteSpace(mapId))
            mapId = "scene_map_" + Guid.NewGuid().ToString("N");

        var displayName = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "displayName"), PayloadReader.GetString(payload, "name")), 1, 160, "displayName");
        var widthMeters = PayloadReader.GetInt(payload, "widthMeters") ?? 2000;
        var heightMeters = PayloadReader.GetInt(payload, "heightMeters") ?? 2000;
        var gridSizeMeters = PayloadReader.GetInt(payload, "gridSizeMeters") ?? PayloadReader.GetInt(payload, "gridCellSizeMeters") ?? 50;
        var mapScale = NormalizeSceneMap0162Scale(PayloadReader.GetString(payload, "mapScale"));
        var defaultTileSizeMeters = PayloadReader.GetDouble(payload, "defaultTileSizeMeters") ?? Math.Max(1, gridSizeMeters);
        var recommendedGridSizeMeters = PayloadReader.GetDouble(payload, "recommendedGridSizeMeters") ?? Math.Max(1, gridSizeMeters);
        var validation = ValidateSceneMap0162Settings(widthMeters, heightMeters, gridSizeMeters, mapScale);
        if (validation != null) return validation;

        var existing = SceneMap0162Definitions().Find(IdFilter(mapId)).FirstOrDefault();
        var createdAt = existing != null && existing.TryGetValue("CreatedAtUtc", out var createdValue) && createdValue.IsValidDateTime
            ? createdValue.ToUniversalTime()
            : now;

        var doc = new BsonDocument
        {
            ["_id"] = mapId,
            ["Id"] = mapId,
            ["WorldId"] = FirstNonEmptyWorld(PayloadReader.GetString(payload, "worldId"), SceneMap0162DefaultWorldId),
            ["CampaignId"] = FirstNonEmptyWorld(PayloadReader.GetString(payload, "campaignId"), "dev-campaign-core"),
            ["RuleSetId"] = FirstNonEmptyWorld(PayloadReader.GetString(payload, "ruleSetId"), "fantasy_nri_default"),
            ["DisplayName"] = displayName,
            ["Description"] = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "description"), PayloadReader.GetString(payload, "publicDescription")), 0, 4096, "description"),
            ["WidthMeters"] = widthMeters,
            ["HeightMeters"] = heightMeters,
            ["GridSizeMeters"] = gridSizeMeters,
            ["MapScale"] = mapScale,
            ["DefaultTileSizeMeters"] = defaultTileSizeMeters,
            ["RecommendedGridSizeMeters"] = recommendedGridSizeMeters,
            ["BackgroundMode"] = FirstNonEmptyWorld(PayloadReader.GetString(payload, "backgroundMode"), "SolidColor"),
            ["BackgroundColor"] = FirstNonEmptyWorld(PayloadReader.GetString(payload, "backgroundColor"), "#111827"),
            ["LinkedWorldMapId"] = RequireLength(PayloadReader.GetString(payload, "linkedWorldMapId"), 0, 128, "linkedWorldMapId"),
            ["LinkedWorldMarkerId"] = RequireLength(PayloadReader.GetString(payload, "linkedWorldMarkerId"), 0, 128, "linkedWorldMarkerId"),
            ["ShowGrid"] = !payload.ContainsKey("showGrid") || PayloadReader.GetBool(payload, "showGrid"),
            ["ShowCoordinates"] = !payload.ContainsKey("showCoordinates") || PayloadReader.GetBool(payload, "showCoordinates"),
            ["SchemaVersion"] = 1,
            ["IsArchived"] = false,
            ["CreatedAtUtc"] = createdAt,
            ["UpdatedAtUtc"] = now,
            ["CreatedByUserId"] = existing != null ? GetDocString(existing, "CreatedByUserId") : actor.Id,
            ["UpdatedByUserId"] = actor.Id
        };

        var requestedCanonicalId = existing == null ? mapId : GetDocString(existing, "CanonicalMapId", mapId);
        var canonical = CanonicalSceneFromProjection0202(doc, requestedCanonicalId);
        var savedCanonical = _repositories.MapCanvases.UpsertAsync(canonical).GetAwaiter().GetResult();
        doc = _mapIdentityResolver.SynchronizeSceneProjection(savedCanonical, mapId, actor.Id, doc);
        _logger.Admin($"scene.map.0162.create mapId={savedCanonical.Id} legacyMapId={mapId} actor={actor.Login}");
        return Ok("Scene map saved.", new Dictionary<string, object>
        {
            ["mapId"] = savedCanonical.Id,
            ["map"] = SceneMap0162MapPayload(doc, admin: true)
        });
    }

    public ResponseEnvelope SceneMapAdminGet0162(CommandContext context)
    {
        RequireAdmin(context);
        if (!SceneMap0162AdminEnabled())
            return SceneMap0162Disabled(context.Request.Command);

        EnsureSceneMap0162Indexes();
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var mapId = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "mapId"), PayloadReader.GetString(payload, "id")), 1, 128, "mapId");
        var identity = _mapIdentityResolver.ResolveSceneMap(mapId);
        if (!identity.IsResolved) return MapIdentityError0202(identity);
        var map = identity.CompatibilityProjection;
        if (map == null) return Error("scene map compatibility projection not found", ResponseStatus.Conflict, ErrorCode.Conflict);

        return Ok("Scene map loaded.", SceneMap0162AdminPayload(map, includeMarkers: true));
    }

    public ResponseEnvelope SceneMapAdminUpdate0162(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!SceneMap0162AdminEnabled())
            return SceneMap0162Disabled(context.Request.Command);

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var mapId = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "mapId"), PayloadReader.GetString(payload, "id")), 1, 128, "mapId");
        var identity = _mapIdentityResolver.ResolveSceneMap(mapId);
        if (!identity.IsResolved) return MapIdentityError0202(identity);
        var doc = identity.CompatibilityProjection;
        if (doc == null) return Error("scene map compatibility projection not found", ResponseStatus.Conflict, ErrorCode.Conflict);

        var nextWidth = payload.ContainsKey("widthMeters") ? PayloadReader.GetInt(payload, "widthMeters") ?? GetDocInt(doc, "WidthMeters", 2000) : GetDocInt(doc, "WidthMeters", 2000);
        var nextHeight = payload.ContainsKey("heightMeters") ? PayloadReader.GetInt(payload, "heightMeters") ?? GetDocInt(doc, "HeightMeters", 2000) : GetDocInt(doc, "HeightMeters", 2000);
        var nextGrid = payload.ContainsKey("gridSizeMeters") || payload.ContainsKey("gridCellSizeMeters")
            ? PayloadReader.GetInt(payload, "gridSizeMeters") ?? PayloadReader.GetInt(payload, "gridCellSizeMeters") ?? GetDocInt(doc, "GridSizeMeters", 50)
            : GetDocInt(doc, "GridSizeMeters", 50);
        var nextScale = NormalizeSceneMap0162Scale(FirstNonEmptyWorld(PayloadReader.GetString(payload, "mapScale"), GetDocString(doc, "MapScale", "Area")));
        var validation = ValidateSceneMap0162Settings(nextWidth, nextHeight, nextGrid, nextScale);
        if (validation != null) return validation;

        if (payload.ContainsKey("displayName") || payload.ContainsKey("name"))
            doc["DisplayName"] = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "displayName"), PayloadReader.GetString(payload, "name")), 1, 160, "displayName");
        if (payload.ContainsKey("description"))
            doc["Description"] = RequireLength(PayloadReader.GetString(payload, "description"), 0, 4096, "description");
        doc["WidthMeters"] = nextWidth;
        doc["HeightMeters"] = nextHeight;
        doc["GridSizeMeters"] = nextGrid;
        doc["MapScale"] = nextScale;
        if (payload.ContainsKey("defaultTileSizeMeters"))
            doc["DefaultTileSizeMeters"] = PayloadReader.GetDouble(payload, "defaultTileSizeMeters") ?? GetDocDouble(doc, "DefaultTileSizeMeters", nextGrid);
        if (payload.ContainsKey("recommendedGridSizeMeters"))
            doc["RecommendedGridSizeMeters"] = PayloadReader.GetDouble(payload, "recommendedGridSizeMeters") ?? GetDocDouble(doc, "RecommendedGridSizeMeters", nextGrid);
        if (payload.ContainsKey("showGrid"))
            doc["ShowGrid"] = PayloadReader.GetBool(payload, "showGrid");
        if (payload.ContainsKey("showCoordinates"))
            doc["ShowCoordinates"] = PayloadReader.GetBool(payload, "showCoordinates");
        if (payload.ContainsKey("backgroundColor"))
            doc["BackgroundColor"] = RequireLength(PayloadReader.GetString(payload, "backgroundColor"), 0, 32, "backgroundColor");
        doc["UpdatedAtUtc"] = DateTime.UtcNow;
        doc["UpdatedByUserId"] = actor.Id;

        var canonical = CanonicalSceneFromProjection0202(doc, identity.CanonicalMapId);
        var savedCanonical = _repositories.MapCanvases.UpsertAsync(canonical).GetAwaiter().GetResult();
        doc = _mapIdentityResolver.SynchronizeSceneProjection(savedCanonical, identity.LegacyMapId, actor.Id, doc);
        return Ok("Scene map updated.", new Dictionary<string, object>
        {
            ["mapId"] = savedCanonical.Id,
            ["map"] = SceneMap0162MapPayload(doc, admin: true)
        });
    }

    public ResponseEnvelope SceneMapAdminArchive0162(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!SceneMap0162AdminEnabled())
            return SceneMap0162Disabled(context.Request.Command);

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var mapId = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "mapId"), PayloadReader.GetString(payload, "id")), 1, 128, "mapId");
        var identity = _mapIdentityResolver.ResolveSceneMap(mapId);
        if (!identity.IsResolved) return MapIdentityError0202(identity);
        var canonical = identity.CanonicalMap!;
        canonical.IsArchived = true;
        canonical.Archived = true;
        canonical.UpdatedAtUtc = DateTime.UtcNow;
        var saved = _repositories.MapCanvases.UpsertAsync(canonical).GetAwaiter().GetResult();
        _mapIdentityResolver.SynchronizeSceneProjection(saved, identity.LegacyMapId, actor.Id, identity.CompatibilityProjection);
        return Ok("Scene map archived.", new Dictionary<string, object> { ["mapId"] = identity.CanonicalMapId });
    }

    public ResponseEnvelope SceneMapAdminSetSessionActive0162(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!SceneMap0162SessionLinkEnabled())
            return SceneMap0162Disabled(context.Request.Command);

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var sessionId = FirstNonEmptyWorld(PayloadReader.GetString(payload, "sessionId"), SceneMap0162DefaultSessionId);
        var mapId = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "mapId"), PayloadReader.GetString(payload, "activeSceneMapId")), 1, 128, "mapId");
        var identity = _mapIdentityResolver.ResolveSceneMap(mapId);
        if (!identity.IsResolved) return MapIdentityError0202(identity);
        var map = identity.CompatibilityProjection;
        if (map == null) return Error("scene map compatibility projection not found", ResponseStatus.Conflict, ErrorCode.Conflict);
        mapId = identity.CanonicalMapId;

        var now = DateTime.UtcNow;
        var doc = new BsonDocument
        {
            ["_id"] = sessionId,
            ["SessionId"] = sessionId,
            ["CampaignId"] = FirstNonEmptyWorld(PayloadReader.GetString(payload, "campaignId"), GetDocString(map, "CampaignId", "dev-campaign-core")),
            ["ActiveGroupId"] = RequireLength(PayloadReader.GetString(payload, "activeGroupId"), 0, 128, "activeGroupId"),
            ["ActiveSceneMapId"] = mapId,
            ["ActiveSceneMapName"] = GetDocString(map, "DisplayName"),
            ["UpdatedAtUtc"] = now,
            ["UpdatedByUserId"] = actor.Id
        };

        SceneMap0162SessionStates().ReplaceOne(Builders<BsonDocument>.Filter.Eq("SessionId", sessionId), doc, new ReplaceOptions { IsUpsert = true });
        return Ok("Active scene map selected.", new Dictionary<string, object>
        {
            ["sessionId"] = sessionId,
            ["mapId"] = mapId,
            ["activeSceneMapId"] = mapId,
            ["mapName"] = GetDocString(map, "DisplayName"),
            ["activeSceneMapName"] = GetDocString(map, "DisplayName"),
            ["hasActiveMap"] = true,
            ["updatedAtUtc"] = now
        });
    }

    public ResponseEnvelope SceneMapAdminGetSessionActive0162(CommandContext context)
    {
        RequireAdmin(context);
        if (!SceneMap0162SessionLinkEnabled())
            return SceneMap0162Disabled(context.Request.Command);

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var sessionId = FirstNonEmptyWorld(PayloadReader.GetString(payload, "sessionId"), SceneMap0162DefaultSessionId);
        var state = ResolveSceneMap0162SessionState(sessionId, PayloadReader.GetString(payload, "campaignId"));
        if (state == null)
            return Ok("No active scene map.", new Dictionary<string, object> { ["hasActiveMap"] = false });

        var mapId = GetDocString(state, "ActiveSceneMapId");
        var identity = _mapIdentityResolver.ResolveSceneMap(mapId);
        var map = identity.IsResolved ? identity.CompatibilityProjection : null;
        return Ok("Active scene map loaded.", new Dictionary<string, object>
        {
            ["hasActiveMap"] = map != null,
            ["sessionId"] = sessionId,
            ["mapId"] = mapId,
            ["mapName"] = map == null ? string.Empty : GetDocString(map, "DisplayName")
        });
    }

    public ResponseEnvelope SceneMapAdminClearSessionActive0162(CommandContext context)
    {
        RequireAdmin(context);
        if (!SceneMap0162SessionLinkEnabled())
            return SceneMap0162Disabled(context.Request.Command);

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var sessionId = FirstNonEmptyWorld(PayloadReader.GetString(payload, "sessionId"), SceneMap0162DefaultSessionId);
        SceneMap0162SessionStates().DeleteOne(Builders<BsonDocument>.Filter.Eq("SessionId", sessionId));
        return Ok("Active scene map cleared.", new Dictionary<string, object> { ["sessionId"] = sessionId, ["hasActiveMap"] = false });
    }

    public ResponseEnvelope SceneMapAdminAddMarker0162(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!SceneMap0162MarkersEnabled())
            return SceneMap0162Disabled(context.Request.Command);

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var mapId = RequireLength(PayloadReader.GetString(payload, "mapId"), 1, 128, "mapId");
        var identity = _mapIdentityResolver.ResolveSceneMap(mapId);
        if (!identity.IsResolved) return MapIdentityError0202(identity);
        var map = identity.CompatibilityProjection!;

        var markerId = FirstNonEmptyWorld(PayloadReader.GetString(payload, "markerId"), PayloadReader.GetString(payload, "id"));
        if (string.IsNullOrWhiteSpace(markerId))
            markerId = "scene_map_marker_" + Guid.NewGuid().ToString("N");
        var doc = BuildSceneMap0162MarkerDoc(map, payload, markerId, actor.Id, DateTime.UtcNow, existing: null);
        var validation = ValidateSceneMap0162Marker(map, doc);
        if (validation != null) return validation;

        SceneMap0162Markers().ReplaceOne(IdFilter(markerId), doc, new ReplaceOptions { IsUpsert = true });
        return Ok("Scene map marker added.", new Dictionary<string, object>
        {
            ["markerId"] = markerId,
            ["marker"] = SceneMap0162MarkerPayload(doc, admin: true)
        });
    }

    public ResponseEnvelope SceneMapAdminUpdateMarker0162(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!SceneMap0162MarkersEnabled())
            return SceneMap0162Disabled(context.Request.Command);

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var markerId = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "markerId"), PayloadReader.GetString(payload, "id")), 1, 128, "markerId");
        var existing = SceneMap0162Markers().Find(ActiveIdFilter(markerId)).FirstOrDefault();
        if (existing == null)
            return Error("scene map marker not found", ResponseStatus.NotFound, ErrorCode.NotFound);
        var identity = _mapIdentityResolver.ResolveSceneMap(GetDocString(existing, "SceneMapId"));
        if (!identity.IsResolved) return MapIdentityError0202(identity);
        var map = identity.CompatibilityProjection!;

        var doc = BuildSceneMap0162MarkerDoc(map, payload, markerId, actor.Id, DateTime.UtcNow, existing);
        var validation = ValidateSceneMap0162Marker(map, doc);
        if (validation != null) return validation;

        SceneMap0162Markers().ReplaceOne(IdFilter(markerId), doc, new ReplaceOptions { IsUpsert = true });
        return Ok("Scene map marker updated.", new Dictionary<string, object>
        {
            ["markerId"] = markerId,
            ["marker"] = SceneMap0162MarkerPayload(doc, admin: true)
        });
    }

    public ResponseEnvelope SceneMapAdminArchiveMarker0162(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!SceneMap0162MarkersEnabled())
            return SceneMap0162Disabled(context.Request.Command);

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var markerId = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "markerId"), PayloadReader.GetString(payload, "id")), 1, 128, "markerId");
        var result = SceneMap0162Markers().UpdateOne(ActiveIdFilter(markerId), Builders<BsonDocument>.Update
            .Set("IsArchived", true)
            .Set("UpdatedAtUtc", DateTime.UtcNow)
            .Set("UpdatedByUserId", actor.Id));
        if (result.MatchedCount == 0)
            return Error("scene map marker not found", ResponseStatus.NotFound, ErrorCode.NotFound);
        return Ok("Scene map marker archived.", new Dictionary<string, object> { ["markerId"] = markerId });
    }

    public ResponseEnvelope SceneMapPlayerGetSessionActive0162(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!SceneMap0162PlayerEnabled())
        {
            _logger.Debug($"scene.map.0162.player.disabled user={actor.Login}");
            return Error("Scene Map viewer is disabled by feature flags.", ResponseStatus.Forbidden, ErrorCode.Forbidden);
        }

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var directMapId = PayloadReader.GetString(payload, "mapId");
        BsonDocument? state = null;
        var sessionId = FirstNonEmptyWorld(PayloadReader.GetString(payload, "sessionId"), SceneMap0162DefaultSessionId);
        if (string.IsNullOrWhiteSpace(directMapId))
            state = ResolveSceneMap0162SessionState(sessionId, PayloadReader.GetString(payload, "campaignId"));

        var mapId = string.IsNullOrWhiteSpace(directMapId) ? GetDocString(state ?? new BsonDocument(), "ActiveSceneMapId") : directMapId;
        if (string.IsNullOrWhiteSpace(mapId))
            return Ok("GM has not selected an active scene map.", new Dictionary<string, object> { ["hasActiveMap"] = false, ["warnings"] = new object[] { "GM ещё не назначил активную карту сцены." } });

        var projection = _playerMapProjectionService.BuildSceneMap(mapId, new PlayerMapProjectionContext0204
        {
            ActorUserId = actor.Id,
            CharacterId = PayloadReader.GetString(payload, "characterId") ?? string.Empty,
            CampaignId = PayloadReader.GetString(payload, "campaignId") ?? string.Empty,
            SessionId = sessionId,
            ActiveGroupId = PayloadReader.GetString(payload, "activeGroupId") ?? string.Empty,
            IncludeMarkers = !payload.ContainsKey("includeMarkers") || PayloadReader.GetBool(payload, "includeMarkers")
        });
        if (!projection.Success)
        {
            var status = projection.ErrorKind == "not_found" ? ResponseStatus.NotFound
                : projection.ErrorKind == "forbidden" ? ResponseStatus.Forbidden
                : ResponseStatus.Conflict;
            return Error(projection.Message, status, status == ResponseStatus.NotFound ? ErrorCode.NotFound : status == ResponseStatus.Forbidden ? ErrorCode.Forbidden : ErrorCode.Conflict);
        }
        var result = new Dictionary<string, object>(projection.Payload, StringComparer.OrdinalIgnoreCase)
        {
            ["hasActiveMap"] = true,
            ["sessionId"] = sessionId,
            ["mapId"] = Convert.ToString(PayloadReader.GetDictionary(projection.Payload, "map")?["mapId"]) ?? mapId
        };
        return Ok("Active scene map loaded.", result);
    }

    private Dictionary<string, object> SceneMap0162AdminPayload(BsonDocument map, bool includeMarkers)
    {
        var markers = includeMarkers
            ? SceneMap0162MarkerDocs(GetDocString(map, "Id"), includeHidden: true)
                .Select(marker => SceneMap0162MarkerPayload(marker, admin: true))
                .Cast<object>()
                .ToArray()
            : Array.Empty<object>();
        return new Dictionary<string, object>
        {
            ["map"] = SceneMap0162MapPayload(map, admin: true),
            ["markers"] = markers,
            ["tokens"] = MapToken0163PayloadsForMap(MapToken0163KindScene, SceneMap0162CanonicalMapId(map), admin: true).Cast<object>().ToArray(),
            ["layers"] = SceneMap0164LayerPayloadsForMap(GetDocString(map, "Id"), admin: true).Cast<object>().ToArray(),
            ["shapes"] = SceneMap0164ShapePayloadsForMap(GetDocString(map, "Id"), admin: true).Cast<object>().ToArray(),
            ["tileLayers"] = SceneMap0164TileLayerPayloadsForMap(GetDocString(map, "Id"), admin: true).Cast<object>().ToArray(),
            ["tilePatches"] = SceneMap0164TilePatchPayloadsForMap(GetDocString(map, "Id"), admin: true).Cast<object>().ToArray(),
            ["assetInstances"] = SceneMap0164AssetInstancePayloadsForMap(GetDocString(map, "Id"), admin: true).Cast<object>().ToArray(),
            ["markerBindings"] = Array.Empty<object>(),
            ["fog"] = new Dictionary<string, object> { ["hasFog"] = false, ["mode"] = "disabled", ["hiddenCells"] = Array.Empty<object>(), ["revealedCells"] = Array.Empty<object>() },
            ["markerCount"] = markers.Length,
            ["tokenCount"] = MapToken0163PayloadsForMap(MapToken0163KindScene, SceneMap0162CanonicalMapId(map), admin: true).Length,
            ["layerCount"] = SceneMap0164LayerPayloadsForMap(GetDocString(map, "Id"), admin: true).Length,
            ["shapeCount"] = SceneMap0164ShapePayloadsForMap(GetDocString(map, "Id"), admin: true).Length,
            ["tilePatchCount"] = SceneMap0164TilePatchPayloadsForMap(GetDocString(map, "Id"), admin: true).Length,
            ["assetInstanceCount"] = SceneMap0164AssetInstancePayloadsForMap(GetDocString(map, "Id"), admin: true).Length,
            ["sourceCollections"] = new object[] { SceneMap0162DefinitionsCollection, SceneMap0162MarkersCollection, SceneMap0162SessionStatesCollection, MapToken0163Collection, SceneMap0164LayersCollection, SceneMap0164ShapesCollection, SceneMap0164TileLayersCollection, SceneMap0164TilePatchesCollection, SceneMap0164AssetInstancesCollection }
        };
    }

    private Dictionary<string, object> SceneMap0162PlayerPayload(BsonDocument map)
    {
        var markers = SceneMap0162MarkerDocs(GetDocString(map, "Id"), includeHidden: false)
            .Select(marker => SceneMap0162MarkerPayload(marker, admin: false))
            .Cast<object>()
            .ToArray();
        var mapPayload = SceneMap0162MapPayload(map, admin: false);
        mapPayload["markers"] = markers;
        mapPayload["tokens"] = MapToken0163PayloadsForMap(MapToken0163KindScene, SceneMap0162CanonicalMapId(map), admin: false).Cast<object>().ToArray();
        mapPayload["layers"] = SceneMap0164LayerPayloadsForMap(GetDocString(map, "Id"), admin: false).Cast<object>().ToArray();
        mapPayload["shapes"] = SceneMap0164ShapePayloadsForMap(GetDocString(map, "Id"), admin: false).Cast<object>().ToArray();
        mapPayload["tileLayers"] = SceneMap0164TileLayerPayloadsForMap(GetDocString(map, "Id"), admin: false).Cast<object>().ToArray();
        mapPayload["tilePatches"] = SceneMap0164TilePatchPayloadsForMap(GetDocString(map, "Id"), admin: false).Cast<object>().ToArray();
        mapPayload["assetInstances"] = SceneMap0164AssetInstancePayloadsForMap(GetDocString(map, "Id"), admin: false).Cast<object>().ToArray();
        mapPayload["fogEnabled"] = false;
        mapPayload["fogOfWarVisibleState"] = new Dictionary<string, object> { ["mode"] = "disabled", ["hiddenCells"] = Array.Empty<object>() };
        return mapPayload;
    }

    private Dictionary<string, object> SceneMap0162ListPayload(BsonDocument map, string activeMapId)
    {
        var payload = SceneMap0162MapPayload(map, admin: true);
        payload["markerCount"] = SceneMap0162MarkerDocs(GetDocString(map, "Id"), includeHidden: true).Count;
        payload["fogEnabled"] = false;
        payload["isActive"] = !string.IsNullOrWhiteSpace(activeMapId) && string.Equals(GetDocString(map, "Id"), activeMapId, StringComparison.OrdinalIgnoreCase);
        return payload;
    }

    private Dictionary<string, object> SceneMap0162MapPayload(BsonDocument map, bool admin)
    {
        return new Dictionary<string, object>
        {
            ["mapId"] = GetDocString(map, "CanonicalMapId", GetDocString(map, "Id")),
            ["id"] = GetDocString(map, "CanonicalMapId", GetDocString(map, "Id")),
            ["worldId"] = GetDocString(map, "WorldId"),
            ["campaignId"] = GetDocString(map, "CampaignId"),
            ["ruleSetId"] = GetDocString(map, "RuleSetId"),
            ["name"] = GetDocString(map, "DisplayName"),
            ["displayName"] = GetDocString(map, "DisplayName"),
            ["description"] = GetDocString(map, "Description"),
            ["widthMeters"] = GetDocInt(map, "WidthMeters", 2000),
            ["heightMeters"] = GetDocInt(map, "HeightMeters", 2000),
            ["gridSizeMeters"] = GetDocInt(map, "GridSizeMeters", 50),
            ["gridCellSizeMeters"] = GetDocInt(map, "GridSizeMeters", 50),
            ["mapScale"] = GetDocString(map, "MapScale", "Area"),
            ["defaultTileSizeMeters"] = GetDocDouble(map, "DefaultTileSizeMeters", GetDocInt(map, "GridSizeMeters", 50)),
            ["recommendedGridSizeMeters"] = GetDocDouble(map, "RecommendedGridSizeMeters", GetDocInt(map, "GridSizeMeters", 50)),
            ["backgroundMode"] = GetDocString(map, "BackgroundMode", "SolidColor"),
            ["backgroundColor"] = GetDocString(map, "BackgroundColor", "#111827"),
            ["linkedWorldMapId"] = GetDocString(map, "LinkedWorldMapId"),
            ["linkedWorldMarkerId"] = GetDocString(map, "LinkedWorldMarkerId"),
            ["showGrid"] = GetDocBool(map, "ShowGrid"),
            ["showCoordinates"] = GetDocBool(map, "ShowCoordinates"),
            ["isArchived"] = GetDocBool(map, "IsArchived"),
            ["archived"] = GetDocBool(map, "IsArchived"),
            ["updatedAtUtc"] = GetDocDate(map, "UpdatedAtUtc"),
            ["adminDiagnostics"] = admin ? $"source={SceneMap0162DefinitionsCollection}" : string.Empty
        };
    }

    private static string SceneMap0162CanonicalMapId(BsonDocument map)
        => GetDocString(map, "CanonicalMapId", GetDocString(map, "Id"));

    private Dictionary<string, object> SceneMap0162MarkerPayload(BsonDocument marker, bool admin)
    {
        var visibility = GetDocString(marker, "Visibility", "Hidden");
        var payload = new Dictionary<string, object>
        {
            ["markerId"] = GetDocString(marker, "Id"),
            ["id"] = GetDocString(marker, "Id"),
            ["mapId"] = GetDocString(marker, "SceneMapId"),
            ["sceneMapId"] = GetDocString(marker, "SceneMapId"),
            ["campaignId"] = GetDocString(marker, "CampaignId"),
            ["name"] = GetDocString(marker, "DisplayName"),
            ["displayName"] = GetDocString(marker, "DisplayName"),
            ["markerType"] = GetDocString(marker, "MarkerType", "PointOfInterest"),
            ["x"] = GetDocDouble(marker, "X", 0d),
            ["y"] = GetDocDouble(marker, "Y", 0d),
            ["radiusMeters"] = GetDocDouble(marker, "RadiusMeters", 0d),
            ["visibility"] = visibility,
            ["visibilityMode"] = visibility,
            ["isPlayerVisible"] = string.Equals(visibility, "PlayerVisible", StringComparison.OrdinalIgnoreCase),
            ["publicNotes"] = GetDocString(marker, "DescriptionPlayer"),
            ["cardTitle"] = GetDocString(marker, "DisplayName"),
            ["cardDescription"] = GetDocString(marker, "DescriptionPlayer"),
            ["description"] = GetDocString(marker, "DescriptionPlayer"),
            ["isArchived"] = GetDocBool(marker, "IsArchived"),
            ["updatedAtUtc"] = GetDocDate(marker, "UpdatedAtUtc")
        };
        if (admin)
        {
            payload["gmNotes"] = GetDocString(marker, "DescriptionGm");
            payload["descriptionGm"] = GetDocString(marker, "DescriptionGm");
        }
        return payload;
    }

    private BsonDocument BuildSceneMap0162MarkerDoc(BsonDocument map, IDictionary<string, object> payload, string markerId, string actorUserId, DateTime now, BsonDocument? existing)
    {
        var visibility = NormalizeSceneMap0162Visibility(FirstNonEmptyWorld(
            PayloadReader.GetString(payload, "visibility"),
            PayloadReader.GetString(payload, "visibilityMode"),
            PayloadReader.GetBool(payload, "isPlayerVisible") ? "PlayerVisible" : existing == null ? "Hidden" : GetDocString(existing, "Visibility", "Hidden")));
        var doc = existing != null ? new BsonDocument(existing) : new BsonDocument
        {
            ["_id"] = markerId,
            ["Id"] = markerId,
            ["CreatedAtUtc"] = now,
            ["CreatedByUserId"] = actorUserId
        };

        doc["Id"] = markerId;
        doc["SceneMapId"] = SceneMap0162CanonicalMapId(map);
        doc["CampaignId"] = GetDocString(map, "CampaignId");
        doc["DisplayName"] = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "displayName"), PayloadReader.GetString(payload, "name"), existing == null ? "Маркер" : GetDocString(existing, "DisplayName")), 1, 160, "displayName");
        doc["DescriptionPlayer"] = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "descriptionPlayer"), PayloadReader.GetString(payload, "publicNotes"), PayloadReader.GetString(payload, "cardDescription"), existing == null ? string.Empty : GetDocString(existing, "DescriptionPlayer")), 0, 4096, "descriptionPlayer");
        doc["DescriptionGm"] = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "descriptionGm"), PayloadReader.GetString(payload, "gmNotes"), existing == null ? string.Empty : GetDocString(existing, "DescriptionGm")), 0, 4096, "descriptionGm");
        doc["MarkerType"] = NormalizeSceneMap0162MarkerType(FirstNonEmptyWorld(PayloadReader.GetString(payload, "markerType"), existing == null ? "PointOfInterest" : GetDocString(existing, "MarkerType", "PointOfInterest")));
        doc["X"] = PayloadReader.GetDouble(payload, "x") ?? PayloadReader.GetDouble(payload, "X") ?? (existing == null ? 0d : GetDocDouble(existing, "X", 0d));
        doc["Y"] = PayloadReader.GetDouble(payload, "y") ?? PayloadReader.GetDouble(payload, "Y") ?? (existing == null ? 0d : GetDocDouble(existing, "Y", 0d));
        doc["RadiusMeters"] = PayloadReader.GetDouble(payload, "radiusMeters") ?? (existing == null ? 0d : GetDocDouble(existing, "RadiusMeters", 0d));
        doc["Visibility"] = visibility;
        doc["IsArchived"] = false;
        doc["UpdatedAtUtc"] = now;
        doc["UpdatedByUserId"] = actorUserId;
        return doc;
    }

    private ResponseEnvelope? ValidateSceneMap0162Settings(int widthMeters, int heightMeters, int gridSizeMeters, string mapScale = "Area")
    {
        var scale = NormalizeSceneMap0162Scale(mapScale);
        var min = scale switch
        {
            "Interior" => 20,
            "Street" => 50,
            "Location" => 100,
            _ => 250
        };
        var max = scale switch
        {
            "Interior" => 100,
            "Street" => 500,
            "Location" => 1000,
            _ => 4000
        };
        if (widthMeters < min || heightMeters < min)
            return Error($"scene map dimensions for {scale} must be at least {min}x{min} meters", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);
        if (widthMeters > max || heightMeters > max)
            return Error($"scene map dimensions for {scale} must be no larger than {max}x{max} meters", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);
        if (gridSizeMeters < 1 || gridSizeMeters > 500)
            return Error("scene map grid size must be between 1 and 500 meters", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);
        return null;
    }

    private static string NormalizeSceneMap0162Scale(string? value)
    {
        var input = (value ?? string.Empty).Trim().ToLowerInvariant();
        return input switch
        {
            "interior" or "room" => "Interior",
            "street" or "alley" => "Street",
            "location" or "local" => "Location",
            "battlefield" or "battle" => "Battlefield",
            "area" or "" => "Area",
            _ => "Area"
        };
    }

    private ResponseEnvelope? ValidateSceneMap0162Marker(BsonDocument map, BsonDocument marker)
    {
        var x = GetDocDouble(marker, "X", 0d);
        var y = GetDocDouble(marker, "Y", 0d);
        var width = GetDocInt(map, "WidthMeters", 2000);
        var height = GetDocInt(map, "HeightMeters", 2000);
        if (x < 0 || y < 0 || x > width || y > height)
            return Error("marker coordinates are outside scene map bounds", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);
        return null;
    }

    private BsonDocument? ResolveSceneMap0162SessionState(string sessionId, string campaignId)
    {
        var state = SceneMap0162SessionStates().Find(Builders<BsonDocument>.Filter.Eq("SessionId", sessionId)).FirstOrDefault();
        if (state == null && !string.IsNullOrWhiteSpace(campaignId))
        {
            state = SceneMap0162SessionStates()
                .Find(Builders<BsonDocument>.Filter.Eq("CampaignId", campaignId))
                .Sort(Builders<BsonDocument>.Sort.Descending("UpdatedAtUtc"))
                .FirstOrDefault();
        }
        return state;
    }

    private List<BsonDocument> SceneMap0162MarkerDocs(string mapId, bool includeHidden)
    {
        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("SceneMapId", mapId),
            Builders<BsonDocument>.Filter.Ne("IsArchived", true));
        if (!includeHidden)
        {
            filter = Builders<BsonDocument>.Filter.And(
                filter,
                Builders<BsonDocument>.Filter.Eq("Visibility", "PlayerVisible"));
        }

        return SceneMap0162Markers()
            .Find(filter)
            .Sort(Builders<BsonDocument>.Sort.Ascending("DisplayName"))
            .ToList();
    }

    private void EnsureSceneMap0162Indexes()
    {
        SceneMap0162Definitions().Indexes.CreateMany(new[]
        {
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("Id")),
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("WorldId")),
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("CampaignId")),
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("IsArchived"))
        });
        SceneMap0162Markers().Indexes.CreateMany(new[]
        {
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("SceneMapId")),
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("Visibility")),
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("IsArchived"))
        });
        SceneMap0162SessionStates().Indexes.CreateMany(new[]
        {
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("SessionId")),
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("CampaignId")),
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("ActiveSceneMapId"))
        });
    }

    private bool SceneMap0162AdminEnabled()
        => _featureFlags.IsEnabled(nameof(MapFeatureFlags.UseMapSystemV1))
           && _featureFlags.IsEnabled(nameof(MapFeatureFlags.UseSpaceHierarchyV1))
           && _featureFlags.IsEnabled(nameof(MapFeatureFlags.UseSceneMapV1));

    private bool SceneMap0162MarkersEnabled()
        => SceneMap0162AdminEnabled()
           && _featureFlags.IsEnabled(nameof(MapFeatureFlags.UseSceneMapMarkers));

    private bool SceneMap0162SessionLinkEnabled()
        => SceneMap0162AdminEnabled()
           && _featureFlags.IsEnabled(nameof(MapFeatureFlags.UseSceneMapSessionLink));

    private bool SceneMap0162PlayerEnabled()
        => SceneMap0162SessionLinkEnabled()
           && _featureFlags.IsEnabled(nameof(MapFeatureFlags.UseSceneMapPlayerView));

    private ResponseEnvelope SceneMap0162Disabled(string commandName)
    {
        _logger.Admin($"scene.map.0162.disabled command={commandName}");
        return Error("Scene Map viewer is disabled by feature flags.", ResponseStatus.Forbidden, ErrorCode.Forbidden);
    }

    private IMongoCollection<BsonDocument> SceneMap0162Definitions() => _mongo.Database.GetCollection<BsonDocument>(SceneMap0162DefinitionsCollection);
    private IMongoCollection<BsonDocument> SceneMap0162Markers() => _mongo.Database.GetCollection<BsonDocument>(SceneMap0162MarkersCollection);
    private IMongoCollection<BsonDocument> SceneMap0162SessionStates() => _mongo.Database.GetCollection<BsonDocument>(SceneMap0162SessionStatesCollection);

    private static string NormalizeSceneMap0162Visibility(string? value)
    {
        var input = (value ?? string.Empty).Trim().ToLowerInvariant();
        return input switch
        {
            "playervisible" or "player_visible" or "public" or "party" or "visible" => "PlayerVisible",
            "gmonly" or "gm_only" or "gm" or "admin" => "GmOnly",
            "hidden" or "server_only" => "Hidden",
            _ => "Hidden"
        };
    }

    private static string NormalizeSceneMap0162MarkerType(string? value)
    {
        var input = (value ?? string.Empty).Trim().ToLowerInvariant();
        return input switch
        {
            "partystart" or "party_start" => "PartyStart",
            "pointofinterest" or "point_of_interest" or "poi" => "PointOfInterest",
            "entrance" => "Entrance",
            "exit" => "Exit",
            "hazard" => "Hazard",
            "objective" => "Objective",
            "gmnote" or "gm_note" => "GmNote",
            _ => "PointOfInterest"
        };
    }
}
