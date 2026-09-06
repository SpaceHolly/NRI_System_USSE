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
    private const string SceneMap0164LayersCollection = "scene_map_layers";
    private const string SceneMap0164ShapesCollection = "scene_map_shapes";
    private const string SceneMap0164TileLayersCollection = "scene_map_tile_layers";
    private const string SceneMap0164TilePatchesCollection = "scene_map_tile_patches";
    private const string SceneMap0164AssetInstancesCollection = "scene_map_asset_instances";

    public ResponseEnvelope SceneMapLayerAdminList0164(CommandContext context)
    {
        RequireAdmin(context);
        if (!SceneMap0164AdminEnabled())
            return SceneMap0164Disabled(context.Request.Command);

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var mapId = RequireLength(PayloadReader.GetString(payload, "sceneMapId") ?? PayloadReader.GetString(payload, "mapId"), 1, 128, "mapId");
        var map = SceneMap0162Definitions().Find(ActiveIdFilter(mapId)).FirstOrDefault();
        if (map == null)
            return Error("scene map not found", ResponseStatus.NotFound, ErrorCode.NotFound);

        var items = SceneMap0164LayerDocsForMap(mapId, includeHidden: true)
            .Select(x => SceneMap0164LayerPayload(x, admin: true))
            .Cast<object>()
            .ToArray();
        return Ok("Scene map layers loaded.", new Dictionary<string, object>
        {
            ["mapId"] = mapId,
            ["items"] = items,
            ["layers"] = items,
            ["count"] = items.Length,
            ["sourceCollection"] = SceneMap0164LayersCollection
        });
    }

    public ResponseEnvelope SceneMapLayerAdminCreate0164(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!SceneMap0164AdminEnabled())
            return SceneMap0164Disabled(context.Request.Command);

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var mapId = RequireLength(PayloadReader.GetString(payload, "sceneMapId") ?? PayloadReader.GetString(payload, "mapId"), 1, 128, "mapId");
        var map = SceneMap0162Definitions().Find(ActiveIdFilter(mapId)).FirstOrDefault();
        if (map == null)
            return Error("scene map not found", ResponseStatus.NotFound, ErrorCode.NotFound);

        EnsureSceneMap0164Indexes();
        var layerId = FirstNonEmptyWorld(PayloadReader.GetString(payload, "layerId"), PayloadReader.GetString(payload, "id"));
        if (string.IsNullOrWhiteSpace(layerId))
            layerId = "scene_layer_" + Guid.NewGuid().ToString("N");

        var doc = BuildSceneMap0164LayerDoc(map, payload, layerId, actor.Id, DateTime.UtcNow, existing: null);
        SceneMap0164Layers().ReplaceOne(IdFilter(layerId), doc, new ReplaceOptions { IsUpsert = true });
        return Ok("Scene map layer created.", new Dictionary<string, object>
        {
            ["layerId"] = layerId,
            ["layer"] = SceneMap0164LayerPayload(doc, admin: true)
        });
    }

    public ResponseEnvelope SceneMapLayerAdminUpdate0164(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!SceneMap0164AdminEnabled())
            return SceneMap0164Disabled(context.Request.Command);

        EnsureSceneMap0164Indexes();
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var layerId = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "layerId"), PayloadReader.GetString(payload, "id")), 1, 128, "layerId");
        var existing = SceneMap0164Layers().Find(ActiveIdFilter(layerId)).FirstOrDefault();
        if (existing == null)
            return Error("scene map layer not found", ResponseStatus.NotFound, ErrorCode.NotFound);
        var map = SceneMap0162Definitions().Find(ActiveIdFilter(GetDocString(existing, "SceneMapId"))).FirstOrDefault();
        if (map == null)
            return Error("scene map not found", ResponseStatus.NotFound, ErrorCode.NotFound);

        var doc = BuildSceneMap0164LayerDoc(map, payload, layerId, actor.Id, DateTime.UtcNow, existing);
        SceneMap0164Layers().ReplaceOne(IdFilter(layerId), doc, new ReplaceOptions { IsUpsert = true });
        return Ok("Scene map layer updated.", new Dictionary<string, object>
        {
            ["layerId"] = layerId,
            ["layer"] = SceneMap0164LayerPayload(doc, admin: true)
        });
    }

    public ResponseEnvelope SceneMapLayerAdminArchive0164(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!SceneMap0164AdminEnabled())
            return SceneMap0164Disabled(context.Request.Command);
        var layerId = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(context.Request.Payload, "layerId"), PayloadReader.GetString(context.Request.Payload, "id")), 1, 128, "layerId");
        var result = SceneMap0164Layers().UpdateOne(ActiveIdFilter(layerId), Builders<BsonDocument>.Update
            .Set("IsArchived", true)
            .Set("UpdatedAtUtc", DateTime.UtcNow)
            .Set("UpdatedByUserId", actor.Id));
        if (result.MatchedCount == 0)
            return Error("scene map layer not found", ResponseStatus.NotFound, ErrorCode.NotFound);
        return Ok("Scene map layer archived.", new Dictionary<string, object> { ["layerId"] = layerId });
    }

    public ResponseEnvelope SceneMapLayerAdminReorder0164(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!SceneMap0164AdminEnabled())
            return SceneMap0164Disabled(context.Request.Command);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var layerId = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "layerId"), PayloadReader.GetString(payload, "id")), 1, 128, "layerId");
        var sortOrder = PayloadReader.GetInt(payload, "sortOrder") ?? 0;
        var result = SceneMap0164Layers().UpdateOne(ActiveIdFilter(layerId), Builders<BsonDocument>.Update
            .Set("SortOrder", sortOrder)
            .Set("UpdatedAtUtc", DateTime.UtcNow)
            .Set("UpdatedByUserId", actor.Id));
        if (result.MatchedCount == 0)
            return Error("scene map layer not found", ResponseStatus.NotFound, ErrorCode.NotFound);
        return Ok("Scene map layer reordered.", new Dictionary<string, object> { ["layerId"] = layerId, ["sortOrder"] = sortOrder });
    }

    public ResponseEnvelope SceneMapLayerAdminSetVisibility0164(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!SceneMap0164AdminEnabled())
            return SceneMap0164Disabled(context.Request.Command);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var layerId = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "layerId"), PayloadReader.GetString(payload, "id")), 1, 128, "layerId");
        var visibility = NormalizeSceneMap0164Visibility(PayloadReader.GetString(payload, "visibility"));
        var result = SceneMap0164Layers().UpdateOne(ActiveIdFilter(layerId), Builders<BsonDocument>.Update
            .Set("Visibility", visibility)
            .Set("UpdatedAtUtc", DateTime.UtcNow)
            .Set("UpdatedByUserId", actor.Id));
        if (result.MatchedCount == 0)
            return Error("scene map layer not found", ResponseStatus.NotFound, ErrorCode.NotFound);
        return Ok("Scene map layer visibility updated.", new Dictionary<string, object> { ["layerId"] = layerId, ["visibility"] = visibility });
    }

    public ResponseEnvelope SceneMapLayerPlayerListForActiveSceneMap0164(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!SceneMap0164PlayerEnabled())
        {
            _logger.Debug($"scene.map.0164.layer.player.disabled user={actor.Login}");
            return SceneMap0164Disabled(context.Request.Command);
        }

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var state = ResolveSceneMap0162SessionState(FirstNonEmptyWorld(PayloadReader.GetString(payload, "sessionId"), SceneMap0162DefaultSessionId), PayloadReader.GetString(payload, "campaignId"));
        if (state == null)
            return Ok("No active scene map.", new Dictionary<string, object> { ["hasActiveMap"] = false, ["items"] = Array.Empty<object>(), ["layers"] = Array.Empty<object>() });
        var mapId = GetDocString(state, "ActiveSceneMapId");
        var layers = SceneMap0164LayerPayloadsForMap(mapId, admin: false).Cast<object>().ToArray();
        return Ok("Player scene map layers loaded.", new Dictionary<string, object>
        {
            ["hasActiveMap"] = true,
            ["mapId"] = mapId,
            ["items"] = layers,
            ["layers"] = layers,
            ["count"] = layers.Length
        });
    }

    public ResponseEnvelope SceneMapShapeAdminList0164(CommandContext context)
    {
        RequireAdmin(context);
        if (!SceneMap0164AdminEnabled())
            return SceneMap0164Disabled(context.Request.Command);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var mapId = RequireLength(PayloadReader.GetString(payload, "sceneMapId") ?? PayloadReader.GetString(payload, "mapId"), 1, 128, "mapId");
        var items = SceneMap0164ShapePayloadsForMap(mapId, admin: true).Cast<object>().ToArray();
        return Ok("Scene map shapes loaded.", new Dictionary<string, object>
        {
            ["mapId"] = mapId,
            ["items"] = items,
            ["shapes"] = items,
            ["count"] = items.Length,
            ["sourceCollection"] = SceneMap0164ShapesCollection
        });
    }

    public ResponseEnvelope SceneMapShapeAdminGet0164(CommandContext context)
    {
        RequireAdmin(context);
        if (!SceneMap0164AdminEnabled())
            return SceneMap0164Disabled(context.Request.Command);
        var shapeId = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(context.Request.Payload, "shapeId"), PayloadReader.GetString(context.Request.Payload, "id")), 1, 128, "shapeId");
        var shape = SceneMap0164Shapes().Find(ActiveIdFilter(shapeId)).FirstOrDefault();
        if (shape == null)
            return Error("scene map shape not found", ResponseStatus.NotFound, ErrorCode.NotFound);
        return Ok("Scene map shape loaded.", new Dictionary<string, object> { ["shapeId"] = shapeId, ["shape"] = SceneMap0164ShapePayload(shape, admin: true) });
    }

    public ResponseEnvelope SceneMapShapeAdminCreate0164(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!SceneMap0164AdminEnabled())
            return SceneMap0164Disabled(context.Request.Command);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var mapId = RequireLength(PayloadReader.GetString(payload, "sceneMapId") ?? PayloadReader.GetString(payload, "mapId"), 1, 128, "mapId");
        var map = SceneMap0162Definitions().Find(ActiveIdFilter(mapId)).FirstOrDefault();
        if (map == null)
            return Error("scene map not found", ResponseStatus.NotFound, ErrorCode.NotFound);

        EnsureSceneMap0164Indexes();
        var shapeId = FirstNonEmptyWorld(PayloadReader.GetString(payload, "shapeId"), PayloadReader.GetString(payload, "id"));
        if (string.IsNullOrWhiteSpace(shapeId))
            shapeId = "scene_shape_" + Guid.NewGuid().ToString("N");
        var doc = BuildSceneMap0164ShapeDoc(map, payload, shapeId, actor.Id, DateTime.UtcNow, existing: null);
        var validation = ValidateSceneMap0164Shape(map, doc);
        if (validation != null) return validation;
        SceneMap0164Shapes().ReplaceOne(IdFilter(shapeId), doc, new ReplaceOptions { IsUpsert = true });
        return Ok("Scene map shape created.", new Dictionary<string, object> { ["shapeId"] = shapeId, ["shape"] = SceneMap0164ShapePayload(doc, admin: true) });
    }

    public ResponseEnvelope SceneMapShapeAdminUpdate0164(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!SceneMap0164AdminEnabled())
            return SceneMap0164Disabled(context.Request.Command);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var shapeId = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "shapeId"), PayloadReader.GetString(payload, "id")), 1, 128, "shapeId");
        var existing = SceneMap0164Shapes().Find(ActiveIdFilter(shapeId)).FirstOrDefault();
        if (existing == null)
            return Error("scene map shape not found", ResponseStatus.NotFound, ErrorCode.NotFound);
        var map = SceneMap0162Definitions().Find(ActiveIdFilter(GetDocString(existing, "SceneMapId"))).FirstOrDefault();
        if (map == null)
            return Error("scene map not found", ResponseStatus.NotFound, ErrorCode.NotFound);
        var doc = BuildSceneMap0164ShapeDoc(map, payload, shapeId, actor.Id, DateTime.UtcNow, existing);
        var validation = ValidateSceneMap0164Shape(map, doc);
        if (validation != null) return validation;
        SceneMap0164Shapes().ReplaceOne(IdFilter(shapeId), doc, new ReplaceOptions { IsUpsert = true });
        return Ok("Scene map shape updated.", new Dictionary<string, object> { ["shapeId"] = shapeId, ["shape"] = SceneMap0164ShapePayload(doc, admin: true) });
    }

    public ResponseEnvelope SceneMapShapeAdminMove0164(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!SceneMap0164AdminEnabled())
            return SceneMap0164Disabled(context.Request.Command);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var shapeId = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "shapeId"), PayloadReader.GetString(payload, "id")), 1, 128, "shapeId");
        var existing = SceneMap0164Shapes().Find(ActiveIdFilter(shapeId)).FirstOrDefault();
        if (existing == null)
            return Error("scene map shape not found", ResponseStatus.NotFound, ErrorCode.NotFound);
        var map = SceneMap0162Definitions().Find(ActiveIdFilter(GetDocString(existing, "SceneMapId"))).FirstOrDefault();
        if (map == null)
            return Error("scene map not found", ResponseStatus.NotFound, ErrorCode.NotFound);
        var doc = new BsonDocument(existing)
        {
            ["X"] = PayloadReader.GetDouble(payload, "x") ?? GetDocDouble(existing, "X", 0d),
            ["Y"] = PayloadReader.GetDouble(payload, "y") ?? GetDocDouble(existing, "Y", 0d),
            ["UpdatedAtUtc"] = DateTime.UtcNow,
            ["UpdatedByUserId"] = actor.Id
        };
        var validation = ValidateSceneMap0164Shape(map, doc);
        if (validation != null) return validation;
        SceneMap0164Shapes().ReplaceOne(IdFilter(shapeId), doc, new ReplaceOptions { IsUpsert = true });
        return Ok("Scene map shape moved.", new Dictionary<string, object> { ["shapeId"] = shapeId, ["shape"] = SceneMap0164ShapePayload(doc, admin: true) });
    }

    public ResponseEnvelope SceneMapShapeAdminResize0164(CommandContext context)
    {
        return SceneMapShapeAdminUpdate0164(context);
    }

    public ResponseEnvelope SceneMapShapeAdminDuplicate0164(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!SceneMap0164AdminEnabled())
            return SceneMap0164Disabled(context.Request.Command);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var sourceId = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "shapeId"), PayloadReader.GetString(payload, "id")), 1, 128, "shapeId");
        var existing = SceneMap0164Shapes().Find(ActiveIdFilter(sourceId)).FirstOrDefault();
        if (existing == null)
            return Error("scene map shape not found", ResponseStatus.NotFound, ErrorCode.NotFound);
        var duplicateId = FirstNonEmptyWorld(PayloadReader.GetString(payload, "newShapeId"), "scene_shape_" + Guid.NewGuid().ToString("N"));
        var now = DateTime.UtcNow;
        var doc = new BsonDocument(existing)
        {
            ["_id"] = duplicateId,
            ["Id"] = duplicateId,
            ["DisplayName"] = FirstNonEmptyWorld(PayloadReader.GetString(payload, "displayName"), GetDocString(existing, "DisplayName") + " (копия)"),
            ["X"] = PayloadReader.GetDouble(payload, "x") ?? GetDocDouble(existing, "X", 0d) + 10d,
            ["Y"] = PayloadReader.GetDouble(payload, "y") ?? GetDocDouble(existing, "Y", 0d) + 10d,
            ["CreatedAtUtc"] = now,
            ["UpdatedAtUtc"] = now,
            ["CreatedByUserId"] = actor.Id,
            ["UpdatedByUserId"] = actor.Id,
            ["IsArchived"] = false
        };
        var map = SceneMap0162Definitions().Find(ActiveIdFilter(GetDocString(existing, "SceneMapId"))).FirstOrDefault();
        if (map == null)
            return Error("scene map not found", ResponseStatus.NotFound, ErrorCode.NotFound);
        var validation = ValidateSceneMap0164Shape(map, doc);
        if (validation != null) return validation;
        SceneMap0164Shapes().ReplaceOne(IdFilter(duplicateId), doc, new ReplaceOptions { IsUpsert = true });
        return Ok("Scene map shape duplicated.", new Dictionary<string, object> { ["shapeId"] = duplicateId, ["shape"] = SceneMap0164ShapePayload(doc, admin: true) });
    }

    public ResponseEnvelope SceneMapShapeAdminArchive0164(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!SceneMap0164AdminEnabled())
            return SceneMap0164Disabled(context.Request.Command);
        var shapeId = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(context.Request.Payload, "shapeId"), PayloadReader.GetString(context.Request.Payload, "id")), 1, 128, "shapeId");
        var result = SceneMap0164Shapes().UpdateOne(ActiveIdFilter(shapeId), Builders<BsonDocument>.Update
            .Set("IsArchived", true)
            .Set("UpdatedAtUtc", DateTime.UtcNow)
            .Set("UpdatedByUserId", actor.Id));
        if (result.MatchedCount == 0)
            return Error("scene map shape not found", ResponseStatus.NotFound, ErrorCode.NotFound);
        return Ok("Scene map shape archived.", new Dictionary<string, object> { ["shapeId"] = shapeId });
    }

    public ResponseEnvelope SceneMapShapeAdminSetVisibility0164(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!SceneMap0164AdminEnabled())
            return SceneMap0164Disabled(context.Request.Command);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var shapeId = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "shapeId"), PayloadReader.GetString(payload, "id")), 1, 128, "shapeId");
        var visibility = NormalizeSceneMap0164Visibility(PayloadReader.GetString(payload, "visibility"));
        var result = SceneMap0164Shapes().UpdateOne(ActiveIdFilter(shapeId), Builders<BsonDocument>.Update
            .Set("Visibility", visibility)
            .Set("UpdatedAtUtc", DateTime.UtcNow)
            .Set("UpdatedByUserId", actor.Id));
        if (result.MatchedCount == 0)
            return Error("scene map shape not found", ResponseStatus.NotFound, ErrorCode.NotFound);
        return Ok("Scene map shape visibility updated.", new Dictionary<string, object> { ["shapeId"] = shapeId, ["visibility"] = visibility });
    }

    public ResponseEnvelope SceneMapShapeAdminReorder0164(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!SceneMap0164AdminEnabled())
            return SceneMap0164Disabled(context.Request.Command);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var shapeId = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "shapeId"), PayloadReader.GetString(payload, "id")), 1, 128, "shapeId");
        var sortOrder = PayloadReader.GetInt(payload, "sortOrder") ?? 0;
        var result = SceneMap0164Shapes().UpdateOne(ActiveIdFilter(shapeId), Builders<BsonDocument>.Update
            .Set("SortOrder", sortOrder)
            .Set("UpdatedAtUtc", DateTime.UtcNow)
            .Set("UpdatedByUserId", actor.Id));
        if (result.MatchedCount == 0)
            return Error("scene map shape not found", ResponseStatus.NotFound, ErrorCode.NotFound);
        return Ok("Scene map shape reordered.", new Dictionary<string, object> { ["shapeId"] = shapeId, ["sortOrder"] = sortOrder });
    }

    public ResponseEnvelope SceneMapShapePlayerListForActiveSceneMap0164(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!SceneMap0164PlayerEnabled())
        {
            _logger.Debug($"scene.map.0164.shape.player.disabled user={actor.Login}");
            return SceneMap0164Disabled(context.Request.Command);
        }
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var state = ResolveSceneMap0162SessionState(FirstNonEmptyWorld(PayloadReader.GetString(payload, "sessionId"), SceneMap0162DefaultSessionId), PayloadReader.GetString(payload, "campaignId"));
        if (state == null)
            return Ok("No active scene map.", new Dictionary<string, object> { ["hasActiveMap"] = false, ["items"] = Array.Empty<object>(), ["shapes"] = Array.Empty<object>() });
        var mapId = GetDocString(state, "ActiveSceneMapId");
        var shapes = SceneMap0164ShapePayloadsForMap(mapId, admin: false).Cast<object>().ToArray();
        return Ok("Player scene map shapes loaded.", new Dictionary<string, object>
        {
            ["hasActiveMap"] = true,
            ["mapId"] = mapId,
            ["items"] = shapes,
            ["shapes"] = shapes,
            ["count"] = shapes.Length
        });
    }

    public ResponseEnvelope SceneMapTileLayerAdminList0164(CommandContext context)
    {
        RequireAdmin(context);
        if (!SceneMap0164AdminEnabled())
            return SceneMap0164Disabled(context.Request.Command);

        var mapId = RequireLength(PayloadReader.GetString(context.Request.Payload, "sceneMapId") ?? PayloadReader.GetString(context.Request.Payload, "mapId"), 1, 128, "mapId");
        var items = SceneMap0164TileLayerPayloadsForMap(mapId, admin: true).Cast<object>().ToArray();
        return Ok("Scene map tile layers loaded.", new Dictionary<string, object>
        {
            ["mapId"] = mapId,
            ["items"] = items,
            ["tileLayers"] = items,
            ["count"] = items.Length,
            ["sourceCollection"] = SceneMap0164TileLayersCollection
        });
    }

    public ResponseEnvelope SceneMapTileLayerAdminCreate0164(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!SceneMap0164AdminEnabled())
            return SceneMap0164Disabled(context.Request.Command);

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var mapId = RequireLength(PayloadReader.GetString(payload, "sceneMapId") ?? PayloadReader.GetString(payload, "mapId"), 1, 128, "mapId");
        var map = SceneMap0162Definitions().Find(ActiveIdFilter(mapId)).FirstOrDefault();
        if (map == null)
            return Error("scene map not found", ResponseStatus.NotFound, ErrorCode.NotFound);

        EnsureSceneMap0164Indexes();
        var layerId = FirstNonEmptyWorld(PayloadReader.GetString(payload, "tileLayerId"), PayloadReader.GetString(payload, "layerId"), PayloadReader.GetString(payload, "id"));
        if (string.IsNullOrWhiteSpace(layerId))
            layerId = "scene_tile_layer_" + Guid.NewGuid().ToString("N");

        var doc = BuildSceneMap0164TileLayerDoc(map, payload, layerId, actor.Id, DateTime.UtcNow, existing: null);
        SceneMap0164TileLayers().ReplaceOne(IdFilter(layerId), doc, new ReplaceOptions { IsUpsert = true });
        return Ok("Scene map tile layer created.", new Dictionary<string, object>
        {
            ["tileLayerId"] = layerId,
            ["layerId"] = layerId,
            ["tileLayer"] = SceneMap0164TileLayerPayload(doc, admin: true)
        });
    }

    public ResponseEnvelope SceneMapTileLayerAdminUpdate0164(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!SceneMap0164AdminEnabled())
            return SceneMap0164Disabled(context.Request.Command);

        EnsureSceneMap0164Indexes();
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var layerId = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "tileLayerId"), PayloadReader.GetString(payload, "layerId"), PayloadReader.GetString(payload, "id")), 1, 128, "tileLayerId");
        var existing = SceneMap0164TileLayers().Find(ActiveIdFilter(layerId)).FirstOrDefault();
        if (existing == null)
            return Error("scene map tile layer not found", ResponseStatus.NotFound, ErrorCode.NotFound);
        var map = SceneMap0162Definitions().Find(ActiveIdFilter(GetDocString(existing, "SceneMapId"))).FirstOrDefault();
        if (map == null)
            return Error("scene map not found", ResponseStatus.NotFound, ErrorCode.NotFound);

        var doc = BuildSceneMap0164TileLayerDoc(map, payload, layerId, actor.Id, DateTime.UtcNow, existing);
        SceneMap0164TileLayers().ReplaceOne(IdFilter(layerId), doc, new ReplaceOptions { IsUpsert = true });
        return Ok("Scene map tile layer updated.", new Dictionary<string, object>
        {
            ["tileLayerId"] = layerId,
            ["layerId"] = layerId,
            ["tileLayer"] = SceneMap0164TileLayerPayload(doc, admin: true)
        });
    }

    public ResponseEnvelope SceneMapTileLayerAdminArchive0164(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!SceneMap0164AdminEnabled())
            return SceneMap0164Disabled(context.Request.Command);
        var layerId = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(context.Request.Payload, "tileLayerId"), PayloadReader.GetString(context.Request.Payload, "layerId"), PayloadReader.GetString(context.Request.Payload, "id")), 1, 128, "tileLayerId");
        var result = SceneMap0164TileLayers().UpdateOne(ActiveIdFilter(layerId), Builders<BsonDocument>.Update
            .Set("IsArchived", true)
            .Set("UpdatedAtUtc", DateTime.UtcNow)
            .Set("UpdatedByUserId", actor.Id));
        if (result.MatchedCount == 0)
            return Error("scene map tile layer not found", ResponseStatus.NotFound, ErrorCode.NotFound);
        return Ok("Scene map tile layer archived.", new Dictionary<string, object> { ["tileLayerId"] = layerId, ["layerId"] = layerId });
    }

    public ResponseEnvelope SceneMapTilePatchAdminList0164(CommandContext context)
    {
        RequireAdmin(context);
        if (!SceneMap0164AdminEnabled())
            return SceneMap0164Disabled(context.Request.Command);

        var mapId = RequireLength(PayloadReader.GetString(context.Request.Payload, "sceneMapId") ?? PayloadReader.GetString(context.Request.Payload, "mapId"), 1, 128, "mapId");
        var items = SceneMap0164TilePatchPayloadsForMap(mapId, admin: true).Cast<object>().ToArray();
        return Ok("Scene map tile patches loaded.", new Dictionary<string, object>
        {
            ["mapId"] = mapId,
            ["items"] = items,
            ["tilePatches"] = items,
            ["count"] = items.Length,
            ["sourceCollection"] = SceneMap0164TilePatchesCollection
        });
    }

    public ResponseEnvelope SceneMapTilePatchAdminPaint0164(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!SceneMap0164AdminEnabled())
            return SceneMap0164Disabled(context.Request.Command);

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var mapId = RequireLength(PayloadReader.GetString(payload, "sceneMapId") ?? PayloadReader.GetString(payload, "mapId"), 1, 128, "mapId");
        var map = SceneMap0162Definitions().Find(ActiveIdFilter(mapId)).FirstOrDefault();
        if (map == null)
            return Error("scene map not found", ResponseStatus.NotFound, ErrorCode.NotFound);

        EnsureSceneMap0164Indexes();
        var patchId = FirstNonEmptyWorld(PayloadReader.GetString(payload, "tilePatchId"), PayloadReader.GetString(payload, "patchId"), PayloadReader.GetString(payload, "id"));
        BsonDocument? existing = null;
        if (!string.IsNullOrWhiteSpace(patchId))
            existing = SceneMap0164TilePatches().Find(ActiveIdFilter(patchId)).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(patchId))
            patchId = "scene_tile_patch_" + Guid.NewGuid().ToString("N");

        var doc = BuildSceneMap0164TilePatchDoc(map, payload, patchId, actor.Id, DateTime.UtcNow, existing);
        var validation = ValidateSceneMap0164TilePatch(map, doc);
        if (validation != null) return validation;
        SceneMap0164TilePatches().ReplaceOne(IdFilter(patchId), doc, new ReplaceOptions { IsUpsert = true });
        return Ok("Scene map tile patch painted.", new Dictionary<string, object>
        {
            ["tilePatchId"] = patchId,
            ["patchId"] = patchId,
            ["tilePatch"] = SceneMap0164TilePatchPayload(doc, admin: true)
        });
    }

    public ResponseEnvelope SceneMapTilePatchAdminArchive0164(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!SceneMap0164AdminEnabled())
            return SceneMap0164Disabled(context.Request.Command);
        var patchId = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(context.Request.Payload, "tilePatchId"), PayloadReader.GetString(context.Request.Payload, "patchId"), PayloadReader.GetString(context.Request.Payload, "id")), 1, 128, "tilePatchId");
        var result = SceneMap0164TilePatches().UpdateOne(ActiveIdFilter(patchId), Builders<BsonDocument>.Update
            .Set("IsArchived", true)
            .Set("UpdatedAtUtc", DateTime.UtcNow)
            .Set("UpdatedByUserId", actor.Id));
        if (result.MatchedCount == 0)
            return Error("scene map tile patch not found", ResponseStatus.NotFound, ErrorCode.NotFound);
        return Ok("Scene map tile patch archived.", new Dictionary<string, object> { ["tilePatchId"] = patchId, ["patchId"] = patchId });
    }

    public ResponseEnvelope SceneMapAssetInstanceAdminList0164(CommandContext context)
    {
        RequireAdmin(context);
        if (!SceneMap0164AdminEnabled())
            return SceneMap0164Disabled(context.Request.Command);

        var mapId = RequireLength(PayloadReader.GetString(context.Request.Payload, "sceneMapId") ?? PayloadReader.GetString(context.Request.Payload, "mapId"), 1, 128, "mapId");
        var items = SceneMap0164AssetInstancePayloadsForMap(mapId, admin: true).Cast<object>().ToArray();
        return Ok("Scene map asset instances loaded.", new Dictionary<string, object>
        {
            ["mapId"] = mapId,
            ["items"] = items,
            ["assetInstances"] = items,
            ["count"] = items.Length,
            ["sourceCollection"] = SceneMap0164AssetInstancesCollection
        });
    }

    public ResponseEnvelope SceneMapAssetInstanceAdminCreate0164(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!SceneMap0164AdminEnabled())
            return SceneMap0164Disabled(context.Request.Command);

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var mapId = RequireLength(PayloadReader.GetString(payload, "sceneMapId") ?? PayloadReader.GetString(payload, "mapId"), 1, 128, "mapId");
        var map = SceneMap0162Definitions().Find(ActiveIdFilter(mapId)).FirstOrDefault();
        if (map == null)
            return Error("scene map not found", ResponseStatus.NotFound, ErrorCode.NotFound);

        EnsureSceneMap0164Indexes();
        var assetId = FirstNonEmptyWorld(PayloadReader.GetString(payload, "assetInstanceId"), PayloadReader.GetString(payload, "assetId"), PayloadReader.GetString(payload, "id"));
        if (string.IsNullOrWhiteSpace(assetId))
            assetId = "scene_asset_" + Guid.NewGuid().ToString("N");

        var doc = BuildSceneMap0164AssetInstanceDoc(map, payload, assetId, actor.Id, DateTime.UtcNow, existing: null);
        var validation = ValidateSceneMap0164AssetInstance(map, doc);
        if (validation != null) return validation;
        SceneMap0164AssetInstances().ReplaceOne(IdFilter(assetId), doc, new ReplaceOptions { IsUpsert = true });
        return Ok("Scene map asset instance created.", new Dictionary<string, object>
        {
            ["assetInstanceId"] = assetId,
            ["assetInstance"] = SceneMap0164AssetInstancePayload(doc, admin: true)
        });
    }

    public ResponseEnvelope SceneMapAssetInstanceAdminUpdate0164(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!SceneMap0164AdminEnabled())
            return SceneMap0164Disabled(context.Request.Command);

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var assetId = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "assetInstanceId"), PayloadReader.GetString(payload, "assetId"), PayloadReader.GetString(payload, "id")), 1, 128, "assetInstanceId");
        var existing = SceneMap0164AssetInstances().Find(ActiveIdFilter(assetId)).FirstOrDefault();
        if (existing == null)
            return Error("scene map asset instance not found", ResponseStatus.NotFound, ErrorCode.NotFound);
        var map = SceneMap0162Definitions().Find(ActiveIdFilter(GetDocString(existing, "SceneMapId"))).FirstOrDefault();
        if (map == null)
            return Error("scene map not found", ResponseStatus.NotFound, ErrorCode.NotFound);

        var doc = BuildSceneMap0164AssetInstanceDoc(map, payload, assetId, actor.Id, DateTime.UtcNow, existing);
        var validation = ValidateSceneMap0164AssetInstance(map, doc);
        if (validation != null) return validation;
        SceneMap0164AssetInstances().ReplaceOne(IdFilter(assetId), doc, new ReplaceOptions { IsUpsert = true });
        return Ok("Scene map asset instance updated.", new Dictionary<string, object>
        {
            ["assetInstanceId"] = assetId,
            ["assetInstance"] = SceneMap0164AssetInstancePayload(doc, admin: true)
        });
    }

    public ResponseEnvelope SceneMapAssetInstanceAdminArchive0164(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!SceneMap0164AdminEnabled())
            return SceneMap0164Disabled(context.Request.Command);
        var assetId = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(context.Request.Payload, "assetInstanceId"), PayloadReader.GetString(context.Request.Payload, "assetId"), PayloadReader.GetString(context.Request.Payload, "id")), 1, 128, "assetInstanceId");
        var result = SceneMap0164AssetInstances().UpdateOne(ActiveIdFilter(assetId), Builders<BsonDocument>.Update
            .Set("IsArchived", true)
            .Set("UpdatedAtUtc", DateTime.UtcNow)
            .Set("UpdatedByUserId", actor.Id));
        if (result.MatchedCount == 0)
            return Error("scene map asset instance not found", ResponseStatus.NotFound, ErrorCode.NotFound);
        return Ok("Scene map asset instance archived.", new Dictionary<string, object> { ["assetInstanceId"] = assetId });
    }

    public ResponseEnvelope SceneMapTilePatchPlayerListForActiveSceneMap0164(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!SceneMap0164PlayerEnabled())
        {
            _logger.Debug($"scene.map.0164.tilePatch.player.disabled user={actor.Login}");
            return SceneMap0164Disabled(context.Request.Command);
        }
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var state = ResolveSceneMap0162SessionState(FirstNonEmptyWorld(PayloadReader.GetString(payload, "sessionId"), SceneMap0162DefaultSessionId), PayloadReader.GetString(payload, "campaignId"));
        if (state == null)
            return Ok("No active scene map.", new Dictionary<string, object> { ["hasActiveMap"] = false, ["items"] = Array.Empty<object>(), ["tilePatches"] = Array.Empty<object>() });
        var mapId = GetDocString(state, "ActiveSceneMapId");
        var patches = SceneMap0164TilePatchPayloadsForMap(mapId, admin: false).Cast<object>().ToArray();
        return Ok("Player scene map tile patches loaded.", new Dictionary<string, object>
        {
            ["hasActiveMap"] = true,
            ["mapId"] = mapId,
            ["items"] = patches,
            ["tilePatches"] = patches,
            ["count"] = patches.Length
        });
    }

    public ResponseEnvelope SceneMapAssetInstancePlayerListForActiveSceneMap0164(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!SceneMap0164PlayerEnabled())
        {
            _logger.Debug($"scene.map.0164.asset.player.disabled user={actor.Login}");
            return SceneMap0164Disabled(context.Request.Command);
        }
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var state = ResolveSceneMap0162SessionState(FirstNonEmptyWorld(PayloadReader.GetString(payload, "sessionId"), SceneMap0162DefaultSessionId), PayloadReader.GetString(payload, "campaignId"));
        if (state == null)
            return Ok("No active scene map.", new Dictionary<string, object> { ["hasActiveMap"] = false, ["items"] = Array.Empty<object>(), ["assetInstances"] = Array.Empty<object>() });
        var mapId = GetDocString(state, "ActiveSceneMapId");
        var assets = SceneMap0164AssetInstancePayloadsForMap(mapId, admin: false).Cast<object>().ToArray();
        return Ok("Player scene map asset instances loaded.", new Dictionary<string, object>
        {
            ["hasActiveMap"] = true,
            ["mapId"] = mapId,
            ["items"] = assets,
            ["assetInstances"] = assets,
            ["count"] = assets.Length
        });
    }

    private BsonDocument BuildSceneMap0164LayerDoc(BsonDocument map, IDictionary<string, object> payload, string layerId, string actorUserId, DateTime now, BsonDocument? existing)
    {
        return new BsonDocument
        {
            ["_id"] = layerId,
            ["Id"] = layerId,
            ["SceneMapId"] = GetDocString(map, "Id"),
            ["DisplayName"] = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "displayName"), PayloadReader.GetString(payload, "name"), existing == null ? "Слой" : GetDocString(existing, "DisplayName")), 1, 160, "displayName"),
            ["LayerKind"] = NormalizeSceneMap0164LayerKind(FirstNonEmptyWorld(PayloadReader.GetString(payload, "layerKind"), existing == null ? "Objects" : GetDocString(existing, "LayerKind", "Objects"))),
            ["SortOrder"] = PayloadReader.GetInt(payload, "sortOrder") ?? (existing == null ? 0 : GetDocInt(existing, "SortOrder", 0)),
            ["IsVisibleByDefault"] = !payload.ContainsKey("isVisibleByDefault") ? (existing == null || GetDocBool(existing, "IsVisibleByDefault")) : PayloadReader.GetBool(payload, "isVisibleByDefault"),
            ["Visibility"] = NormalizeSceneMap0164Visibility(FirstNonEmptyWorld(PayloadReader.GetString(payload, "visibility"), existing == null ? "PlayerVisible" : GetDocString(existing, "Visibility", "PlayerVisible"))),
            ["IsArchived"] = false,
            ["CreatedAtUtc"] = existing != null ? GetDocDate(existing, "CreatedAtUtc") : now,
            ["UpdatedAtUtc"] = now,
            ["CreatedByUserId"] = existing != null ? GetDocString(existing, "CreatedByUserId") : actorUserId,
            ["UpdatedByUserId"] = actorUserId
        };
    }

    private BsonDocument BuildSceneMap0164ShapeDoc(BsonDocument map, IDictionary<string, object> payload, string shapeId, string actorUserId, DateTime now, BsonDocument? existing)
    {
        var layerId = FirstNonEmptyWorld(PayloadReader.GetString(payload, "layerId"), existing == null ? string.Empty : GetDocString(existing, "LayerId"));
        if (string.IsNullOrWhiteSpace(layerId))
            layerId = EnsureSceneMap0164DefaultLayer(GetDocString(map, "Id"), actorUserId);

        return new BsonDocument
        {
            ["_id"] = shapeId,
            ["Id"] = shapeId,
            ["SceneMapId"] = GetDocString(map, "Id"),
            ["LayerId"] = layerId,
            ["DisplayName"] = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "displayName"), PayloadReader.GetString(payload, "name"), existing == null ? "Объект локации" : GetDocString(existing, "DisplayName")), 1, 160, "displayName"),
            ["DescriptionPlayer"] = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "descriptionPlayer"), PayloadReader.GetString(payload, "publicNotes"), existing == null ? string.Empty : GetDocString(existing, "DescriptionPlayer")), 0, 4096, "descriptionPlayer"),
            ["DescriptionGm"] = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "descriptionGm"), PayloadReader.GetString(payload, "gmNotes"), existing == null ? string.Empty : GetDocString(existing, "DescriptionGm")), 0, 4096, "descriptionGm"),
            ["ShapeKind"] = NormalizeSceneMap0164ShapeKind(FirstNonEmptyWorld(PayloadReader.GetString(payload, "shapeKind"), existing == null ? "Rectangle" : GetDocString(existing, "ShapeKind", "Rectangle"))),
            ["ObjectKind"] = NormalizeSceneMap0164ObjectKind(FirstNonEmptyWorld(PayloadReader.GetString(payload, "objectKind"), existing == null ? "Decoration" : GetDocString(existing, "ObjectKind", "Decoration"))),
            ["X"] = PayloadReader.GetDouble(payload, "x") ?? (existing == null ? 0d : GetDocDouble(existing, "X", 0d)),
            ["Y"] = PayloadReader.GetDouble(payload, "y") ?? (existing == null ? 0d : GetDocDouble(existing, "Y", 0d)),
            ["Width"] = PayloadReader.GetDouble(payload, "width") ?? (existing == null ? 50d : GetDocDouble(existing, "Width", 50d)),
            ["Height"] = PayloadReader.GetDouble(payload, "height") ?? (existing == null ? 50d : GetDocDouble(existing, "Height", 50d)),
            ["Radius"] = PayloadReader.GetDouble(payload, "radius") ?? (existing == null ? 0d : GetDocDouble(existing, "Radius", 0d)),
            ["RotationDegrees"] = PayloadReader.GetDouble(payload, "rotationDegrees") ?? (existing == null ? 0d : GetDocDouble(existing, "RotationDegrees", 0d)),
            ["Points"] = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "points"), existing == null ? string.Empty : GetDocString(existing, "Points")), 0, 4096, "points"),
            ["Text"] = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "text"), existing == null ? string.Empty : GetDocString(existing, "Text")), 0, 2048, "text"),
            ["FillKey"] = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "fillKey"), existing == null ? string.Empty : GetDocString(existing, "FillKey")), 0, 64, "fillKey"),
            ["StrokeKey"] = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "strokeKey"), existing == null ? string.Empty : GetDocString(existing, "StrokeKey")), 0, 64, "strokeKey"),
            ["Opacity"] = PayloadReader.GetDouble(payload, "opacity") ?? (existing == null ? 0.65d : GetDocDouble(existing, "Opacity", 0.65d)),
            ["MaterialKey"] = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "materialKey"), existing == null ? DefaultSceneMap0164MaterialKey(FirstNonEmptyWorld(PayloadReader.GetString(payload, "objectKind"), "Decoration"), PayloadReader.GetString(payload, "fillKey")) : GetDocString(existing, "MaterialKey")), 0, 96, "materialKey"),
            ["TextureKey"] = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "textureKey"), existing == null ? DefaultSceneMap0164TextureKey(FirstNonEmptyWorld(PayloadReader.GetString(payload, "objectKind"), "Decoration"), PayloadReader.GetString(payload, "fillKey")) : GetDocString(existing, "TextureKey")), 0, 96, "textureKey"),
            ["PatternKey"] = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "patternKey"), existing == null ? string.Empty : GetDocString(existing, "PatternKey")), 0, 96, "patternKey"),
            ["AssetKey"] = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "assetKey"), existing == null ? DefaultSceneMap0164AssetKey(FirstNonEmptyWorld(PayloadReader.GetString(payload, "objectKind"), "Decoration")) : GetDocString(existing, "AssetKey")), 0, 96, "assetKey"),
            ["VisualStyleKey"] = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "visualStyleKey"), existing == null ? DefaultSceneMap0164VisualStyleKey(FirstNonEmptyWorld(PayloadReader.GetString(payload, "objectKind"), "Decoration")) : GetDocString(existing, "VisualStyleKey")), 0, 96, "visualStyleKey"),
            ["RenderMode"] = NormalizeSceneMap0164RenderMode(FirstNonEmptyWorld(PayloadReader.GetString(payload, "renderMode"), existing == null ? DefaultSceneMap0164RenderMode(FirstNonEmptyWorld(PayloadReader.GetString(payload, "shapeKind"), "Rectangle"), FirstNonEmptyWorld(PayloadReader.GetString(payload, "objectKind"), "Decoration")) : GetDocString(existing, "RenderMode", "TexturedShape"))),
            ["GridSnapEnabled"] = !payload.ContainsKey("gridSnapEnabled") ? (existing == null || GetDocBool(existing, "GridSnapEnabled")) : PayloadReader.GetBool(payload, "gridSnapEnabled"),
            ["VisualOpacity"] = PayloadReader.GetDouble(payload, "visualOpacity") ?? (existing == null ? (PayloadReader.GetDouble(payload, "opacity") ?? 0.88d) : GetDocDouble(existing, "VisualOpacity", GetDocDouble(existing, "Opacity", 0.88d))),
            ["StrokeThickness"] = PayloadReader.GetDouble(payload, "strokeThickness") ?? (existing == null ? DefaultSceneMap0164StrokeThickness(FirstNonEmptyWorld(PayloadReader.GetString(payload, "objectKind"), "Decoration")) : GetDocDouble(existing, "StrokeThickness", 1.4d)),
            ["ZIndex"] = PayloadReader.GetInt(payload, "zIndex") ?? (existing == null ? PayloadReader.GetInt(payload, "sortOrder") ?? 0 : GetDocInt(existing, "ZIndex", GetDocInt(existing, "SortOrder", 0))),
            ["SortOrder"] = PayloadReader.GetInt(payload, "sortOrder") ?? (existing == null ? 0 : GetDocInt(existing, "SortOrder", 0)),
            ["Visibility"] = NormalizeSceneMap0164Visibility(FirstNonEmptyWorld(PayloadReader.GetString(payload, "visibility"), existing == null ? "PlayerVisible" : GetDocString(existing, "Visibility", "PlayerVisible"))),
            ["BlocksMovement"] = payload.ContainsKey("blocksMovement") ? PayloadReader.GetBool(payload, "blocksMovement") : existing != null && GetDocBool(existing, "BlocksMovement"),
            ["BlocksVision"] = payload.ContainsKey("blocksVision") ? PayloadReader.GetBool(payload, "blocksVision") : existing != null && GetDocBool(existing, "BlocksVision"),
            ["ProvidesCover"] = payload.ContainsKey("providesCover") ? PayloadReader.GetBool(payload, "providesCover") : existing != null && GetDocBool(existing, "ProvidesCover"),
            ["IsInteractable"] = payload.ContainsKey("isInteractable") ? PayloadReader.GetBool(payload, "isInteractable") : existing != null && GetDocBool(existing, "IsInteractable"),
            ["LinkedEntityType"] = NormalizeSceneMap0164LinkedEntityType(FirstNonEmptyWorld(PayloadReader.GetString(payload, "linkedEntityType"), existing == null ? "None" : GetDocString(existing, "LinkedEntityType", "None"))),
            ["LinkedEntityId"] = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "linkedEntityId"), existing == null ? string.Empty : GetDocString(existing, "LinkedEntityId")), 0, 256, "linkedEntityId"),
            ["IsArchived"] = false,
            ["CreatedAtUtc"] = existing != null ? GetDocDate(existing, "CreatedAtUtc") : now,
            ["UpdatedAtUtc"] = now,
            ["CreatedByUserId"] = existing != null ? GetDocString(existing, "CreatedByUserId") : actorUserId,
            ["UpdatedByUserId"] = actorUserId
        };
    }

    private BsonDocument BuildSceneMap0164TileLayerDoc(BsonDocument map, IDictionary<string, object> payload, string layerId, string actorUserId, DateTime now, BsonDocument? existing)
    {
        return new BsonDocument
        {
            ["_id"] = layerId,
            ["Id"] = layerId,
            ["SceneMapId"] = GetDocString(map, "Id"),
            ["DisplayName"] = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "displayName"), PayloadReader.GetString(payload, "name"), existing == null ? "Материалы карты" : GetDocString(existing, "DisplayName")), 1, 160, "displayName"),
            ["TileSizeMeters"] = PayloadReader.GetDouble(payload, "tileSizeMeters") ?? (existing == null ? GetDocInt(map, "DefaultTileSizeMeters", GetDocInt(map, "GridSizeMeters", 5)) : GetDocDouble(existing, "TileSizeMeters", GetDocInt(map, "GridSizeMeters", 5))),
            ["SortOrder"] = PayloadReader.GetInt(payload, "sortOrder") ?? (existing == null ? 10 : GetDocInt(existing, "SortOrder", 10)),
            ["IsVisibleByDefault"] = !payload.ContainsKey("isVisibleByDefault") ? (existing == null || GetDocBool(existing, "IsVisibleByDefault")) : PayloadReader.GetBool(payload, "isVisibleByDefault"),
            ["Visibility"] = NormalizeSceneMap0164Visibility(FirstNonEmptyWorld(PayloadReader.GetString(payload, "visibility"), existing == null ? "PlayerVisible" : GetDocString(existing, "Visibility", "PlayerVisible"))),
            ["IsArchived"] = false,
            ["CreatedAtUtc"] = existing != null ? GetDocDate(existing, "CreatedAtUtc") : now,
            ["UpdatedAtUtc"] = now,
            ["CreatedByUserId"] = existing != null ? GetDocString(existing, "CreatedByUserId") : actorUserId,
            ["UpdatedByUserId"] = actorUserId
        };
    }

    private BsonDocument BuildSceneMap0164TilePatchDoc(BsonDocument map, IDictionary<string, object> payload, string patchId, string actorUserId, DateTime now, BsonDocument? existing)
    {
        var layerId = FirstNonEmptyWorld(PayloadReader.GetString(payload, "tileLayerId"), PayloadReader.GetString(payload, "layerId"), existing == null ? string.Empty : GetDocString(existing, "TileLayerId"));
        if (string.IsNullOrWhiteSpace(layerId))
            layerId = EnsureSceneMap0164DefaultTileLayer(GetDocString(map, "Id"), actorUserId);

        return new BsonDocument
        {
            ["_id"] = patchId,
            ["Id"] = patchId,
            ["SceneMapId"] = GetDocString(map, "Id"),
            ["TileLayerId"] = layerId,
            ["MaterialKey"] = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "materialKey"), existing == null ? "grass" : GetDocString(existing, "MaterialKey", "grass")), 1, 96, "materialKey"),
            ["TextureKey"] = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "textureKey"), existing == null ? DefaultSceneMap0164TextureForMaterial(PayloadReader.GetString(payload, "materialKey")) : GetDocString(existing, "TextureKey")), 0, 96, "textureKey"),
            ["X"] = PayloadReader.GetDouble(payload, "x") ?? (existing == null ? 0d : GetDocDouble(existing, "X", 0d)),
            ["Y"] = PayloadReader.GetDouble(payload, "y") ?? (existing == null ? 0d : GetDocDouble(existing, "Y", 0d)),
            ["Width"] = PayloadReader.GetDouble(payload, "width") ?? (existing == null ? 20d : GetDocDouble(existing, "Width", 20d)),
            ["Height"] = PayloadReader.GetDouble(payload, "height") ?? (existing == null ? 20d : GetDocDouble(existing, "Height", 20d)),
            ["RotationDegrees"] = PayloadReader.GetDouble(payload, "rotationDegrees") ?? (existing == null ? 0d : GetDocDouble(existing, "RotationDegrees", 0d)),
            ["Opacity"] = PayloadReader.GetDouble(payload, "opacity") ?? (existing == null ? 1d : GetDocDouble(existing, "Opacity", 1d)),
            ["SortOrder"] = PayloadReader.GetInt(payload, "sortOrder") ?? (existing == null ? 10 : GetDocInt(existing, "SortOrder", 10)),
            ["Visibility"] = NormalizeSceneMap0164Visibility(FirstNonEmptyWorld(PayloadReader.GetString(payload, "visibility"), existing == null ? "PlayerVisible" : GetDocString(existing, "Visibility", "PlayerVisible"))),
            ["IsArchived"] = false,
            ["CreatedAtUtc"] = existing != null ? GetDocDate(existing, "CreatedAtUtc") : now,
            ["UpdatedAtUtc"] = now,
            ["CreatedByUserId"] = existing != null ? GetDocString(existing, "CreatedByUserId") : actorUserId,
            ["UpdatedByUserId"] = actorUserId
        };
    }

    private BsonDocument BuildSceneMap0164AssetInstanceDoc(BsonDocument map, IDictionary<string, object> payload, string assetId, string actorUserId, DateTime now, BsonDocument? existing)
    {
        var assetKey = FirstNonEmptyWorld(PayloadReader.GetString(payload, "assetKey"), existing == null ? "crate" : GetDocString(existing, "AssetKey", "crate"));
        return new BsonDocument
        {
            ["_id"] = assetId,
            ["Id"] = assetId,
            ["SceneMapId"] = GetDocString(map, "Id"),
            ["AssetKey"] = RequireLength(assetKey, 1, 96, "assetKey"),
            ["DisplayName"] = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "displayName"), PayloadReader.GetString(payload, "name"), existing == null ? DefaultSceneMap0164AssetDisplayName(assetKey) : GetDocString(existing, "DisplayName")), 1, 160, "displayName"),
            ["AssetKind"] = NormalizeSceneMap0164AssetKind(FirstNonEmptyWorld(PayloadReader.GetString(payload, "assetKind"), existing == null ? DefaultSceneMap0164AssetKind(assetKey) : GetDocString(existing, "AssetKind", "Prop"))),
            ["ObjectKind"] = NormalizeSceneMap0164ObjectKind(FirstNonEmptyWorld(PayloadReader.GetString(payload, "objectKind"), existing == null ? DefaultSceneMap0164ObjectKindForAsset(assetKey) : GetDocString(existing, "ObjectKind", "Decoration"))),
            ["X"] = PayloadReader.GetDouble(payload, "x") ?? (existing == null ? 0d : GetDocDouble(existing, "X", 0d)),
            ["Y"] = PayloadReader.GetDouble(payload, "y") ?? (existing == null ? 0d : GetDocDouble(existing, "Y", 0d)),
            ["Width"] = PayloadReader.GetDouble(payload, "width") ?? (existing == null ? DefaultSceneMap0164AssetWidth(assetKey) : GetDocDouble(existing, "Width", DefaultSceneMap0164AssetWidth(assetKey))),
            ["Height"] = PayloadReader.GetDouble(payload, "height") ?? (existing == null ? DefaultSceneMap0164AssetHeight(assetKey) : GetDocDouble(existing, "Height", DefaultSceneMap0164AssetHeight(assetKey))),
            ["RotationDegrees"] = PayloadReader.GetDouble(payload, "rotationDegrees") ?? (existing == null ? 0d : GetDocDouble(existing, "RotationDegrees", 0d)),
            ["ZIndex"] = PayloadReader.GetInt(payload, "zIndex") ?? (existing == null ? 100 : GetDocInt(existing, "ZIndex", 100)),
            ["Visibility"] = NormalizeSceneMap0164Visibility(FirstNonEmptyWorld(PayloadReader.GetString(payload, "visibility"), existing == null ? "PlayerVisible" : GetDocString(existing, "Visibility", "PlayerVisible"))),
            ["DescriptionPlayer"] = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "descriptionPlayer"), PayloadReader.GetString(payload, "publicNotes"), existing == null ? string.Empty : GetDocString(existing, "DescriptionPlayer")), 0, 4096, "descriptionPlayer"),
            ["DescriptionGm"] = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "descriptionGm"), PayloadReader.GetString(payload, "gmNotes"), existing == null ? string.Empty : GetDocString(existing, "DescriptionGm")), 0, 4096, "descriptionGm"),
            ["BlocksMovement"] = payload.ContainsKey("blocksMovement") ? PayloadReader.GetBool(payload, "blocksMovement") : existing != null && GetDocBool(existing, "BlocksMovement"),
            ["BlocksVision"] = payload.ContainsKey("blocksVision") ? PayloadReader.GetBool(payload, "blocksVision") : existing != null && GetDocBool(existing, "BlocksVision"),
            ["ProvidesCover"] = payload.ContainsKey("providesCover") ? PayloadReader.GetBool(payload, "providesCover") : existing != null && GetDocBool(existing, "ProvidesCover"),
            ["IsInteractable"] = payload.ContainsKey("isInteractable") ? PayloadReader.GetBool(payload, "isInteractable") : existing == null || GetDocBool(existing, "IsInteractable"),
            ["LinkedEntityType"] = NormalizeSceneMap0164LinkedEntityType(FirstNonEmptyWorld(PayloadReader.GetString(payload, "linkedEntityType"), existing == null ? "None" : GetDocString(existing, "LinkedEntityType", "None"))),
            ["LinkedEntityId"] = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "linkedEntityId"), existing == null ? string.Empty : GetDocString(existing, "LinkedEntityId")), 0, 256, "linkedEntityId"),
            ["IsArchived"] = false,
            ["CreatedAtUtc"] = existing != null ? GetDocDate(existing, "CreatedAtUtc") : now,
            ["UpdatedAtUtc"] = now,
            ["CreatedByUserId"] = existing != null ? GetDocString(existing, "CreatedByUserId") : actorUserId,
            ["UpdatedByUserId"] = actorUserId
        };
    }

    private ResponseEnvelope? ValidateSceneMap0164Shape(BsonDocument map, BsonDocument shape)
    {
        var width = GetDocInt(map, "WidthMeters", 2000);
        var height = GetDocInt(map, "HeightMeters", 2000);
        var x = GetDocDouble(shape, "X", 0d);
        var y = GetDocDouble(shape, "Y", 0d);
        var w = GetDocDouble(shape, "Width", 0d);
        var h = GetDocDouble(shape, "Height", 0d);
        var radius = GetDocDouble(shape, "Radius", 0d);
        if (x < 0 || y < 0 || x > width || y > height)
            return Error("shape coordinates are outside scene map bounds", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);
        if (w < 0 || h < 0 || radius < 0)
            return Error("shape geometry values must be non-negative", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);
        if (w > 0 && x + w > width)
            return Error("shape width extends outside scene map bounds", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);
        if (h > 0 && y + h > height)
            return Error("shape height extends outside scene map bounds", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);
        foreach (var point in ParseSceneMap0164Points(GetDocString(shape, "Points")))
        {
            if (point.x < 0 || point.y < 0 || point.x > width || point.y > height)
                return Error("shape point is outside scene map bounds", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);
        }
        return null;
    }

    private ResponseEnvelope? ValidateSceneMap0164TilePatch(BsonDocument map, BsonDocument patch)
    {
        var width = GetDocInt(map, "WidthMeters", 2000);
        var height = GetDocInt(map, "HeightMeters", 2000);
        var x = GetDocDouble(patch, "X", 0d);
        var y = GetDocDouble(patch, "Y", 0d);
        var w = GetDocDouble(patch, "Width", 0d);
        var h = GetDocDouble(patch, "Height", 0d);
        if (x < 0 || y < 0 || x > width || y > height)
            return Error("tile patch coordinates are outside scene map bounds", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);
        if (w <= 0 || h <= 0)
            return Error("tile patch size must be positive", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);
        if (x + w > width || y + h > height)
            return Error("tile patch extends outside scene map bounds", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);
        return null;
    }

    private ResponseEnvelope? ValidateSceneMap0164AssetInstance(BsonDocument map, BsonDocument asset)
    {
        var width = GetDocInt(map, "WidthMeters", 2000);
        var height = GetDocInt(map, "HeightMeters", 2000);
        var x = GetDocDouble(asset, "X", 0d);
        var y = GetDocDouble(asset, "Y", 0d);
        var w = GetDocDouble(asset, "Width", 0d);
        var h = GetDocDouble(asset, "Height", 0d);
        if (x < 0 || y < 0 || x > width || y > height)
            return Error("asset coordinates are outside scene map bounds", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);
        if (w <= 0 || h <= 0)
            return Error("asset size must be positive", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);
        if (x + w > width || y + h > height)
            return Error("asset bounds extend outside scene map bounds", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);
        return null;
    }

    private Dictionary<string, object>[] SceneMap0164LayerPayloadsForMap(string mapId, bool admin)
    {
        return SceneMap0164LayerDocsForMap(mapId, includeHidden: admin)
            .Select(x => SceneMap0164LayerPayload(x, admin))
            .ToArray();
    }

    private Dictionary<string, object>[] SceneMap0164ShapePayloadsForMap(string mapId, bool admin)
    {
        var playerVisibleLayerIds = admin
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : SceneMap0164LayerDocsForMap(mapId, includeHidden: false).Select(x => GetDocString(x, "Id")).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return SceneMap0164ShapeDocsForMap(mapId, includeHidden: admin)
            .Where(x => admin || playerVisibleLayerIds.Contains(GetDocString(x, "LayerId")))
            .Select(x => SceneMap0164ShapePayload(x, admin))
            .ToArray();
    }

    private Dictionary<string, object>[] SceneMap0164TileLayerPayloadsForMap(string mapId, bool admin)
    {
        return SceneMap0164TileLayerDocsForMap(mapId, includeHidden: admin)
            .Select(x => SceneMap0164TileLayerPayload(x, admin))
            .ToArray();
    }

    private Dictionary<string, object>[] SceneMap0164TilePatchPayloadsForMap(string mapId, bool admin)
    {
        var playerVisibleLayerIds = admin
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : SceneMap0164TileLayerDocsForMap(mapId, includeHidden: false).Select(x => GetDocString(x, "Id")).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return SceneMap0164TilePatchDocsForMap(mapId, includeHidden: admin)
            .Where(x => admin || playerVisibleLayerIds.Contains(GetDocString(x, "TileLayerId")))
            .Select(x => SceneMap0164TilePatchPayload(x, admin))
            .ToArray();
    }

    private Dictionary<string, object>[] SceneMap0164AssetInstancePayloadsForMap(string mapId, bool admin)
    {
        return SceneMap0164AssetInstanceDocsForMap(mapId, includeHidden: admin)
            .Select(x => SceneMap0164AssetInstancePayload(x, admin))
            .ToArray();
    }

    private List<BsonDocument> SceneMap0164LayerDocsForMap(string mapId, bool includeHidden)
    {
        EnsureSceneMap0164Indexes();
        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("SceneMapId", mapId),
            Builders<BsonDocument>.Filter.Ne("IsArchived", true));
        if (!includeHidden)
            filter = Builders<BsonDocument>.Filter.And(filter, Builders<BsonDocument>.Filter.Eq("Visibility", "PlayerVisible"));
        return SceneMap0164Layers().Find(filter).Sort(Builders<BsonDocument>.Sort.Ascending("SortOrder").Ascending("DisplayName")).ToList();
    }

    private List<BsonDocument> SceneMap0164ShapeDocsForMap(string mapId, bool includeHidden)
    {
        EnsureSceneMap0164Indexes();
        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("SceneMapId", mapId),
            Builders<BsonDocument>.Filter.Ne("IsArchived", true));
        if (!includeHidden)
            filter = Builders<BsonDocument>.Filter.And(filter, Builders<BsonDocument>.Filter.Eq("Visibility", "PlayerVisible"));
        return SceneMap0164Shapes().Find(filter).Sort(Builders<BsonDocument>.Sort.Ascending("SortOrder").Ascending("DisplayName")).ToList();
    }

    private List<BsonDocument> SceneMap0164TileLayerDocsForMap(string mapId, bool includeHidden)
    {
        EnsureSceneMap0164Indexes();
        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("SceneMapId", mapId),
            Builders<BsonDocument>.Filter.Ne("IsArchived", true));
        if (!includeHidden)
            filter = Builders<BsonDocument>.Filter.And(filter, Builders<BsonDocument>.Filter.Eq("Visibility", "PlayerVisible"));
        return SceneMap0164TileLayers().Find(filter).Sort(Builders<BsonDocument>.Sort.Ascending("SortOrder").Ascending("DisplayName")).ToList();
    }

    private List<BsonDocument> SceneMap0164TilePatchDocsForMap(string mapId, bool includeHidden)
    {
        EnsureSceneMap0164Indexes();
        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("SceneMapId", mapId),
            Builders<BsonDocument>.Filter.Ne("IsArchived", true));
        if (!includeHidden)
            filter = Builders<BsonDocument>.Filter.And(filter, Builders<BsonDocument>.Filter.Eq("Visibility", "PlayerVisible"));
        return SceneMap0164TilePatches().Find(filter).Sort(Builders<BsonDocument>.Sort.Ascending("SortOrder").Ascending("MaterialKey")).ToList();
    }

    private List<BsonDocument> SceneMap0164AssetInstanceDocsForMap(string mapId, bool includeHidden)
    {
        EnsureSceneMap0164Indexes();
        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("SceneMapId", mapId),
            Builders<BsonDocument>.Filter.Ne("IsArchived", true));
        if (!includeHidden)
            filter = Builders<BsonDocument>.Filter.And(filter, Builders<BsonDocument>.Filter.Eq("Visibility", "PlayerVisible"));
        return SceneMap0164AssetInstances().Find(filter).Sort(Builders<BsonDocument>.Sort.Ascending("ZIndex").Ascending("DisplayName")).ToList();
    }

    private Dictionary<string, object> SceneMap0164LayerPayload(BsonDocument layer, bool admin)
    {
        return new Dictionary<string, object>
        {
            ["layerId"] = GetDocString(layer, "Id"),
            ["id"] = GetDocString(layer, "Id"),
            ["sceneMapId"] = GetDocString(layer, "SceneMapId"),
            ["mapId"] = GetDocString(layer, "SceneMapId"),
            ["displayName"] = GetDocString(layer, "DisplayName"),
            ["name"] = GetDocString(layer, "DisplayName"),
            ["layerKind"] = GetDocString(layer, "LayerKind", "Objects"),
            ["sortOrder"] = GetDocInt(layer, "SortOrder", 0),
            ["isVisibleByDefault"] = GetDocBool(layer, "IsVisibleByDefault"),
            ["visibility"] = GetDocString(layer, "Visibility", "Hidden"),
            ["visibilityMode"] = GetDocString(layer, "Visibility", "Hidden"),
            ["isPlayerVisible"] = string.Equals(GetDocString(layer, "Visibility"), "PlayerVisible", StringComparison.OrdinalIgnoreCase),
            ["isArchived"] = GetDocBool(layer, "IsArchived"),
            ["updatedAtUtc"] = GetDocDate(layer, "UpdatedAtUtc")
        };
    }

    private Dictionary<string, object> SceneMap0164ShapePayload(BsonDocument shape, bool admin)
    {
        var payload = new Dictionary<string, object>
        {
            ["shapeId"] = GetDocString(shape, "Id"),
            ["id"] = GetDocString(shape, "Id"),
            ["sceneMapId"] = GetDocString(shape, "SceneMapId"),
            ["mapId"] = GetDocString(shape, "SceneMapId"),
            ["layerId"] = GetDocString(shape, "LayerId"),
            ["displayName"] = GetDocString(shape, "DisplayName"),
            ["name"] = GetDocString(shape, "DisplayName"),
            ["descriptionPlayer"] = GetDocString(shape, "DescriptionPlayer"),
            ["cardDescription"] = GetDocString(shape, "DescriptionPlayer"),
            ["shapeKind"] = GetDocString(shape, "ShapeKind", "Rectangle"),
            ["objectKind"] = GetDocString(shape, "ObjectKind", "Decoration"),
            ["x"] = GetDocDouble(shape, "X", 0d),
            ["y"] = GetDocDouble(shape, "Y", 0d),
            ["width"] = GetDocDouble(shape, "Width", 0d),
            ["height"] = GetDocDouble(shape, "Height", 0d),
            ["radius"] = GetDocDouble(shape, "Radius", 0d),
            ["rotationDegrees"] = GetDocDouble(shape, "RotationDegrees", 0d),
            ["points"] = GetDocString(shape, "Points"),
            ["text"] = GetDocString(shape, "Text"),
            ["fillKey"] = GetDocString(shape, "FillKey"),
            ["strokeKey"] = GetDocString(shape, "StrokeKey"),
            ["opacity"] = GetDocDouble(shape, "Opacity", 0.65d),
            ["materialKey"] = GetDocString(shape, "MaterialKey", DefaultSceneMap0164MaterialKey(GetDocString(shape, "ObjectKind", "Decoration"), GetDocString(shape, "FillKey"))),
            ["textureKey"] = GetDocString(shape, "TextureKey", DefaultSceneMap0164TextureKey(GetDocString(shape, "ObjectKind", "Decoration"), GetDocString(shape, "FillKey"))),
            ["patternKey"] = GetDocString(shape, "PatternKey"),
            ["assetKey"] = GetDocString(shape, "AssetKey", DefaultSceneMap0164AssetKey(GetDocString(shape, "ObjectKind", "Decoration"))),
            ["visualStyleKey"] = GetDocString(shape, "VisualStyleKey", DefaultSceneMap0164VisualStyleKey(GetDocString(shape, "ObjectKind", "Decoration"))),
            ["renderMode"] = GetDocString(shape, "RenderMode", DefaultSceneMap0164RenderMode(GetDocString(shape, "ShapeKind", "Rectangle"), GetDocString(shape, "ObjectKind", "Decoration"))),
            ["gridSnapEnabled"] = GetDocBool(shape, "GridSnapEnabled"),
            ["visualOpacity"] = GetDocDouble(shape, "VisualOpacity", GetDocDouble(shape, "Opacity", 0.88d)),
            ["strokeThickness"] = GetDocDouble(shape, "StrokeThickness", DefaultSceneMap0164StrokeThickness(GetDocString(shape, "ObjectKind", "Decoration"))),
            ["zIndex"] = GetDocInt(shape, "ZIndex", GetDocInt(shape, "SortOrder", 0)),
            ["sortOrder"] = GetDocInt(shape, "SortOrder", 0),
            ["visibility"] = GetDocString(shape, "Visibility", "Hidden"),
            ["visibilityMode"] = GetDocString(shape, "Visibility", "Hidden"),
            ["isPlayerVisible"] = string.Equals(GetDocString(shape, "Visibility"), "PlayerVisible", StringComparison.OrdinalIgnoreCase),
            ["blocksMovement"] = GetDocBool(shape, "BlocksMovement"),
            ["blocksVision"] = GetDocBool(shape, "BlocksVision"),
            ["providesCover"] = GetDocBool(shape, "ProvidesCover"),
            ["isInteractable"] = GetDocBool(shape, "IsInteractable"),
            ["linkedEntityType"] = GetDocString(shape, "LinkedEntityType", "None"),
            ["linkedEntityId"] = admin ? GetDocString(shape, "LinkedEntityId") : string.Empty,
            ["isArchived"] = GetDocBool(shape, "IsArchived"),
            ["updatedAtUtc"] = GetDocDate(shape, "UpdatedAtUtc")
        };
        if (admin)
        {
            payload["descriptionGm"] = GetDocString(shape, "DescriptionGm");
            payload["gmNotes"] = GetDocString(shape, "DescriptionGm");
        }
        return payload;
    }

    private Dictionary<string, object> SceneMap0164TileLayerPayload(BsonDocument layer, bool admin)
    {
        return new Dictionary<string, object>
        {
            ["tileLayerId"] = GetDocString(layer, "Id"),
            ["layerId"] = GetDocString(layer, "Id"),
            ["id"] = GetDocString(layer, "Id"),
            ["sceneMapId"] = GetDocString(layer, "SceneMapId"),
            ["mapId"] = GetDocString(layer, "SceneMapId"),
            ["displayName"] = GetDocString(layer, "DisplayName"),
            ["name"] = GetDocString(layer, "DisplayName"),
            ["tileSizeMeters"] = GetDocDouble(layer, "TileSizeMeters", 5d),
            ["sortOrder"] = GetDocInt(layer, "SortOrder", 10),
            ["isVisibleByDefault"] = GetDocBool(layer, "IsVisibleByDefault"),
            ["visibility"] = GetDocString(layer, "Visibility", "Hidden"),
            ["visibilityMode"] = GetDocString(layer, "Visibility", "Hidden"),
            ["isPlayerVisible"] = string.Equals(GetDocString(layer, "Visibility"), "PlayerVisible", StringComparison.OrdinalIgnoreCase),
            ["isArchived"] = GetDocBool(layer, "IsArchived"),
            ["updatedAtUtc"] = GetDocDate(layer, "UpdatedAtUtc")
        };
    }

    private Dictionary<string, object> SceneMap0164TilePatchPayload(BsonDocument patch, bool admin)
    {
        return new Dictionary<string, object>
        {
            ["tilePatchId"] = GetDocString(patch, "Id"),
            ["patchId"] = GetDocString(patch, "Id"),
            ["id"] = GetDocString(patch, "Id"),
            ["sceneMapId"] = GetDocString(patch, "SceneMapId"),
            ["mapId"] = GetDocString(patch, "SceneMapId"),
            ["tileLayerId"] = GetDocString(patch, "TileLayerId"),
            ["layerId"] = GetDocString(patch, "TileLayerId"),
            ["materialKey"] = GetDocString(patch, "MaterialKey", "grass"),
            ["textureKey"] = GetDocString(patch, "TextureKey", DefaultSceneMap0164TextureForMaterial(GetDocString(patch, "MaterialKey", "grass"))),
            ["x"] = GetDocDouble(patch, "X", 0d),
            ["y"] = GetDocDouble(patch, "Y", 0d),
            ["width"] = GetDocDouble(patch, "Width", 1d),
            ["height"] = GetDocDouble(patch, "Height", 1d),
            ["rotationDegrees"] = GetDocDouble(patch, "RotationDegrees", 0d),
            ["opacity"] = GetDocDouble(patch, "Opacity", 1d),
            ["sortOrder"] = GetDocInt(patch, "SortOrder", 10),
            ["visibility"] = GetDocString(patch, "Visibility", "Hidden"),
            ["visibilityMode"] = GetDocString(patch, "Visibility", "Hidden"),
            ["isPlayerVisible"] = string.Equals(GetDocString(patch, "Visibility"), "PlayerVisible", StringComparison.OrdinalIgnoreCase),
            ["isArchived"] = GetDocBool(patch, "IsArchived"),
            ["updatedAtUtc"] = GetDocDate(patch, "UpdatedAtUtc")
        };
    }

    private Dictionary<string, object> SceneMap0164AssetInstancePayload(BsonDocument asset, bool admin)
    {
        var payload = new Dictionary<string, object>
        {
            ["assetInstanceId"] = GetDocString(asset, "Id"),
            ["assetId"] = GetDocString(asset, "Id"),
            ["id"] = GetDocString(asset, "Id"),
            ["sceneMapId"] = GetDocString(asset, "SceneMapId"),
            ["mapId"] = GetDocString(asset, "SceneMapId"),
            ["assetKey"] = GetDocString(asset, "AssetKey", "crate"),
            ["displayName"] = GetDocString(asset, "DisplayName"),
            ["name"] = GetDocString(asset, "DisplayName"),
            ["assetKind"] = GetDocString(asset, "AssetKind", "Prop"),
            ["objectKind"] = GetDocString(asset, "ObjectKind", "Decoration"),
            ["x"] = GetDocDouble(asset, "X", 0d),
            ["y"] = GetDocDouble(asset, "Y", 0d),
            ["width"] = GetDocDouble(asset, "Width", 1d),
            ["height"] = GetDocDouble(asset, "Height", 1d),
            ["rotationDegrees"] = GetDocDouble(asset, "RotationDegrees", 0d),
            ["zIndex"] = GetDocInt(asset, "ZIndex", 100),
            ["visibility"] = GetDocString(asset, "Visibility", "Hidden"),
            ["visibilityMode"] = GetDocString(asset, "Visibility", "Hidden"),
            ["isPlayerVisible"] = string.Equals(GetDocString(asset, "Visibility"), "PlayerVisible", StringComparison.OrdinalIgnoreCase),
            ["descriptionPlayer"] = GetDocString(asset, "DescriptionPlayer"),
            ["cardDescription"] = GetDocString(asset, "DescriptionPlayer"),
            ["blocksMovement"] = GetDocBool(asset, "BlocksMovement"),
            ["blocksVision"] = GetDocBool(asset, "BlocksVision"),
            ["providesCover"] = GetDocBool(asset, "ProvidesCover"),
            ["isInteractable"] = GetDocBool(asset, "IsInteractable"),
            ["linkedEntityType"] = GetDocString(asset, "LinkedEntityType", "None"),
            ["linkedEntityId"] = admin ? GetDocString(asset, "LinkedEntityId") : string.Empty,
            ["isArchived"] = GetDocBool(asset, "IsArchived"),
            ["updatedAtUtc"] = GetDocDate(asset, "UpdatedAtUtc")
        };
        if (admin)
        {
            payload["descriptionGm"] = GetDocString(asset, "DescriptionGm");
            payload["gmNotes"] = GetDocString(asset, "DescriptionGm");
        }
        return payload;
    }

    private string EnsureSceneMap0164DefaultLayer(string mapId, string actorUserId)
    {
        var existing = SceneMap0164Layers().Find(Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("SceneMapId", mapId),
            Builders<BsonDocument>.Filter.Eq("LayerKind", "Objects"),
            Builders<BsonDocument>.Filter.Ne("IsArchived", true))).FirstOrDefault();
        if (existing != null)
            return GetDocString(existing, "Id");

        var now = DateTime.UtcNow;
        var id = "scene_layer_default_" + Guid.NewGuid().ToString("N");
        var doc = new BsonDocument
        {
            ["_id"] = id,
            ["Id"] = id,
            ["SceneMapId"] = mapId,
            ["DisplayName"] = "Объекты сцены",
            ["LayerKind"] = "Objects",
            ["SortOrder"] = 40,
            ["IsVisibleByDefault"] = true,
            ["Visibility"] = "PlayerVisible",
            ["IsArchived"] = false,
            ["CreatedAtUtc"] = now,
            ["UpdatedAtUtc"] = now,
            ["CreatedByUserId"] = actorUserId,
            ["UpdatedByUserId"] = actorUserId
        };
        SceneMap0164Layers().ReplaceOne(IdFilter(id), doc, new ReplaceOptions { IsUpsert = true });
        return id;
    }

    private string EnsureSceneMap0164DefaultTileLayer(string mapId, string actorUserId)
    {
        var existing = SceneMap0164TileLayers().Find(Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("SceneMapId", mapId),
            Builders<BsonDocument>.Filter.Ne("IsArchived", true))).FirstOrDefault();
        if (existing != null)
            return GetDocString(existing, "Id");

        var map = SceneMap0162Definitions().Find(ActiveIdFilter(mapId)).FirstOrDefault() ?? new BsonDocument { ["GridSizeMeters"] = 5 };
        var now = DateTime.UtcNow;
        var id = "scene_tile_layer_default_" + Guid.NewGuid().ToString("N");
        var doc = new BsonDocument
        {
            ["_id"] = id,
            ["Id"] = id,
            ["SceneMapId"] = mapId,
            ["DisplayName"] = "Материалы локации",
            ["TileSizeMeters"] = GetDocInt(map, "DefaultTileSizeMeters", GetDocInt(map, "GridSizeMeters", 5)),
            ["SortOrder"] = 10,
            ["IsVisibleByDefault"] = true,
            ["Visibility"] = "PlayerVisible",
            ["IsArchived"] = false,
            ["CreatedAtUtc"] = now,
            ["UpdatedAtUtc"] = now,
            ["CreatedByUserId"] = actorUserId,
            ["UpdatedByUserId"] = actorUserId
        };
        SceneMap0164TileLayers().ReplaceOne(IdFilter(id), doc, new ReplaceOptions { IsUpsert = true });
        return id;
    }

    private void EnsureSceneMap0164Indexes()
    {
        SceneMap0164Layers().Indexes.CreateMany(new[]
        {
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("Id")),
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("SceneMapId")),
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("LayerKind")),
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("Visibility")),
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("IsArchived"))
        });
        SceneMap0164Shapes().Indexes.CreateMany(new[]
        {
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("Id")),
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("SceneMapId")),
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("LayerId")),
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("ObjectKind")),
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("Visibility")),
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("IsArchived"))
        });
        SceneMap0164TileLayers().Indexes.CreateMany(new[]
        {
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("Id")),
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("SceneMapId")),
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("Visibility")),
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("IsArchived"))
        });
        SceneMap0164TilePatches().Indexes.CreateMany(new[]
        {
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("Id")),
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("SceneMapId")),
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("TileLayerId")),
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("MaterialKey")),
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("Visibility")),
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("IsArchived"))
        });
        SceneMap0164AssetInstances().Indexes.CreateMany(new[]
        {
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("Id")),
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("SceneMapId")),
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("AssetKey")),
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("AssetKind")),
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("Visibility")),
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("IsArchived"))
        });
    }

    private IMongoCollection<BsonDocument> SceneMap0164Layers() => _mongo.Database.GetCollection<BsonDocument>(SceneMap0164LayersCollection);
    private IMongoCollection<BsonDocument> SceneMap0164Shapes() => _mongo.Database.GetCollection<BsonDocument>(SceneMap0164ShapesCollection);
    private IMongoCollection<BsonDocument> SceneMap0164TileLayers() => _mongo.Database.GetCollection<BsonDocument>(SceneMap0164TileLayersCollection);
    private IMongoCollection<BsonDocument> SceneMap0164TilePatches() => _mongo.Database.GetCollection<BsonDocument>(SceneMap0164TilePatchesCollection);
    private IMongoCollection<BsonDocument> SceneMap0164AssetInstances() => _mongo.Database.GetCollection<BsonDocument>(SceneMap0164AssetInstancesCollection);

    private bool SceneMap0164AdminEnabled()
        => _featureFlags.IsEnabled(nameof(MapFeatureFlags.UseMapSystemV1))
           && _featureFlags.IsEnabled(nameof(MapFeatureFlags.UseSpaceHierarchyV1))
           && _featureFlags.IsEnabled(nameof(MapFeatureFlags.UseSceneMapV1));

    private bool SceneMap0164PlayerEnabled()
        => SceneMap0164AdminEnabled()
           && _featureFlags.IsEnabled(nameof(MapFeatureFlags.UseSceneMapSessionLink))
           && _featureFlags.IsEnabled(nameof(MapFeatureFlags.UseSceneMapPlayerView));

    private ResponseEnvelope SceneMap0164Disabled(string commandName)
    {
        _logger.Admin($"scene.map.0164.disabled command={commandName}");
        return Error("Location map editor is disabled by feature flags.", ResponseStatus.Forbidden, ErrorCode.Forbidden);
    }

    private static string NormalizeSceneMap0164Visibility(string? value)
    {
        var input = (value ?? string.Empty).Trim().ToLowerInvariant();
        return input switch
        {
            "playervisible" or "player_visible" or "public" or "visible" => "PlayerVisible",
            "gmonly" or "gm_only" or "gm" or "admin" => "GmOnly",
            "hidden" or "server_only" => "Hidden",
            _ => "Hidden"
        };
    }

    private static string NormalizeSceneMap0164LayerKind(string? value)
    {
        var input = (value ?? string.Empty).Trim().ToLowerInvariant();
        return input switch
        {
            "terrain" => "Terrain",
            "buildings" or "building" or "rooms" => "Buildings",
            "roads" or "road" => "Roads",
            "walls" or "wall" => "Walls",
            "objects" or "object" => "Objects",
            "labels" or "label" => "Labels",
            "gmnotes" or "gm_notes" or "gmnote" => "GmNotes",
            _ => "Objects"
        };
    }

    private static string NormalizeSceneMap0164ShapeKind(string? value)
    {
        var input = (value ?? string.Empty).Trim().ToLowerInvariant();
        return input switch
        {
            "rectangle" or "rect" => "Rectangle",
            "circle" => "Circle",
            "line" or "wall" => "Line",
            "polyline" or "road" => "Polyline",
            "polygon" or "zone" => "Polygon",
            "text" or "textlabel" or "label" => "Text",
            _ => "Rectangle"
        };
    }

    private static string NormalizeSceneMap0164ObjectKind(string? value)
    {
        var input = (value ?? string.Empty).Trim().ToLowerInvariant();
        return input switch
        {
            "terrainzone" or "terrain_zone" => "TerrainZone",
            "building" => "Building",
            "room" => "Room",
            "wall" => "Wall",
            "road" => "Road",
            "alley" => "Alley",
            "door" => "Door",
            "entrance" => "Entrance",
            "exit" => "Exit",
            "cover" => "Cover",
            "obstacle" => "Obstacle",
            "hazardzone" or "hazard_zone" => "HazardZone",
            "marketstall" or "market_stall" => "MarketStall",
            "shoparea" or "shop_area" => "ShopArea",
            "tavernarea" or "tavern_area" => "TavernArea",
            "storagearea" or "storage_area" => "StorageArea",
            "objectivezone" or "objective_zone" => "ObjectiveZone",
            "spawnzone" or "spawn_zone" => "SpawnZone",
            "decoration" => "Decoration",
            "textlabel" or "text_label" => "TextLabel",
            "gmnote" or "gm_note" => "GmNote",
            _ => "Decoration"
        };
    }

    private static string NormalizeSceneMap0164LinkedEntityType(string? value)
    {
        var input = (value ?? string.Empty).Trim().ToLowerInvariant();
        return input switch
        {
            "shop" => "Shop",
            "npc" => "Npc",
            "faction" => "Faction",
            "quest" => "Quest",
            "location" => "Location",
            "object" => "Object",
            _ => "None"
        };
    }

    private static string NormalizeSceneMap0164AssetKind(string? value)
    {
        var input = (value ?? string.Empty).Trim().ToLowerInvariant();
        return input switch
        {
            "market_stall" or "counter" or "shelf" or "crate" or "barrel" or "cart" or "signboard" => input,
            "table" or "chair_or_bench" or "bed" or "hearth" or "bar_counter" => input,
            "lantern" or "well" or "fence" or "door" or "window" or "stairs" => input,
            "tent" or "campfire" or "tree" or "bush" or "rock" or "log" => input,
            "cover_low" or "cover_high" or "obstacle" or "hazard_zone" or "objective_marker" or "spawn_zone" => input,
            "secret_passage" => input,
            "market" or "shop" => "Market",
            "tavern" or "interior" => "Interior",
            "street" or "city" => "Street",
            "camp" or "outdoor" => "Outdoor",
            "gameplay" or "cover" or "hazard" or "objective" => "Gameplay",
            "building" => "Building",
            "prop" or "props" or "object" => "Prop",
            _ => "Prop"
        };
    }

    private static string NormalizeSceneMap0164RenderMode(string? value)
    {
        var input = (value ?? string.Empty).Trim().ToLowerInvariant();
        return input switch
        {
            "shape" => "Shape",
            "texturedshape" or "textured_shape" or "material" => "TexturedShape",
            "assetstamp" or "asset_stamp" or "stamp" => "AssetStamp",
            "linewall" or "line_wall" or "wall" => "LineWall",
            "roadpath" or "road_path" or "road" => "RoadPath",
            "zoneoverlay" or "zone_overlay" or "zone" => "ZoneOverlay",
            "label" or "text" => "Label",
            _ => "TexturedShape"
        };
    }

    private static string DefaultSceneMap0164TextureForMaterial(string? materialKey)
    {
        var key = (materialKey ?? string.Empty).Trim().ToLowerInvariant();
        return key switch
        {
            "grass" => "grass_noise",
            "dirt" => "dirt_track",
            "mud" => "mud_mottle",
            "sand" => "sand_dots",
            "stone" => "stone_tiles",
            "cobblestone" or "market_square_cobble" => "cobble_small",
            "wood_planks" or "bridge_wood" => "wood_planks",
            "stone_tiles" => "stone_tiles",
            "tavern_floor" => "wood_planks",
            "shop_floor" => "stone_tiles",
            "warehouse_floor" => "wood_planks",
            "road_dirt" => "dirt_track",
            "alley_stone" => "narrow_stone",
            "shallow_water" => "water_ripple",
            "hazard_red_overlay" => "hazard_cross",
            "objective_gold_overlay" => "objective_hatch",
            "spawn_blue_overlay" => "spawn_grid",
            _ => "cobble_small"
        };
    }

    private static string DefaultSceneMap0164AssetKind(string assetKey)
    {
        var key = (assetKey ?? string.Empty).Trim().ToLowerInvariant();
        return key switch
        {
            "market_stall" or "counter" or "shelf" or "crate" or "barrel" or "cart" or "signboard" => "Market",
            "table" or "chair_or_bench" or "bed" or "hearth" or "bar_counter" => "Interior",
            "lantern" or "well" or "fence" or "door" or "window" or "stairs" => "Street",
            "tent" or "campfire" or "tree" or "bush" or "rock" or "log" => "Outdoor",
            "cover_low" or "cover_high" or "obstacle" or "hazard_zone" or "objective_marker" or "spawn_zone" => "Gameplay",
            _ => "Prop"
        };
    }

    private static string DefaultSceneMap0164ObjectKindForAsset(string assetKey)
    {
        var key = (assetKey ?? string.Empty).Trim().ToLowerInvariant();
        return key switch
        {
            "market_stall" => "MarketStall",
            "door" => "Door",
            "cover_low" or "cover_high" => "Cover",
            "obstacle" => "Obstacle",
            "hazard_zone" => "HazardZone",
            "objective_marker" => "ObjectiveZone",
            "spawn_zone" => "SpawnZone",
            _ => "Decoration"
        };
    }

    private static string DefaultSceneMap0164AssetDisplayName(string assetKey)
    {
        var key = (assetKey ?? string.Empty).Trim().ToLowerInvariant();
        return key switch
        {
            "market_stall" => "Рыночная лавка",
            "counter" => "Прилавок",
            "shelf" => "Полка",
            "crate" => "Ящик",
            "barrel" => "Бочка",
            "cart" => "Телега",
            "signboard" => "Вывеска",
            "table" => "Стол",
            "chair_or_bench" => "Скамья",
            "bed" => "Кровать",
            "hearth" => "Очаг",
            "bar_counter" => "Стойка",
            "lantern" => "Фонарь",
            "well" => "Колодец",
            "fence" => "Забор",
            "door" => "Дверь",
            "window" => "Окно",
            "stairs" => "Лестница",
            "tent" => "Палатка",
            "campfire" => "Костёр",
            "tree" => "Дерево",
            "bush" => "Куст",
            "rock" => "Камень",
            "log" => "Бревно",
            "cover_low" => "Низкое укрытие",
            "cover_high" => "Высокое укрытие",
            "obstacle" => "Препятствие",
            "hazard_zone" => "Опасная зона",
            "objective_marker" => "Цель",
            "spawn_zone" => "Зона старта",
            _ => "Объект карты"
        };
    }

    private static double DefaultSceneMap0164AssetWidth(string assetKey)
    {
        var key = (assetKey ?? string.Empty).Trim().ToLowerInvariant();
        return key switch
        {
            "market_stall" => 12d,
            "cart" => 10d,
            "table" => 6d,
            "bar_counter" => 12d,
            "fence" => 16d,
            "stairs" => 8d,
            "tent" => 12d,
            "tree" => 8d,
            "cover_high" => 8d,
            "hazard_zone" or "spawn_zone" => 14d,
            _ => 5d
        };
    }

    private static double DefaultSceneMap0164AssetHeight(string assetKey)
    {
        var key = (assetKey ?? string.Empty).Trim().ToLowerInvariant();
        return key switch
        {
            "market_stall" => 8d,
            "cart" => 6d,
            "table" => 5d,
            "bar_counter" => 4d,
            "fence" => 3d,
            "stairs" => 6d,
            "tent" => 10d,
            "tree" => 8d,
            "cover_high" => 8d,
            "hazard_zone" or "spawn_zone" => 14d,
            _ => 5d
        };
    }

    private static string DefaultSceneMap0164RenderMode(string shapeKind, string objectKind)
    {
        var kind = (objectKind ?? string.Empty).Trim().ToLowerInvariant();
        var shape = (shapeKind ?? string.Empty).Trim().ToLowerInvariant();
        if (kind is "road" or "alley") return "RoadPath";
        if (kind is "wall") return "LineWall";
        if (kind is "marketstall" or "door" or "entrance" or "exit" or "decoration") return "AssetStamp";
        if (kind is "hazardzone" or "objectivezone" or "spawnzone" or "terrainzone") return "ZoneOverlay";
        if (kind is "textlabel" or "gmnote" || shape is "text") return "Label";
        return "TexturedShape";
    }

    private static string DefaultSceneMap0164MaterialKey(string objectKind, string? fillKey)
    {
        var fill = (fillKey ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(fill)) return fill;
        return (objectKind ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "terrainzone" => "cobblestone",
            "road" => "packed_dirt",
            "alley" => "dark_stone",
            "building" or "room" or "shoparea" => "wood_floor",
            "tavernarea" => "warm_wood",
            "storagearea" => "stone_floor",
            "marketstall" => "canvas_red",
            "entrance" or "exit" or "door" => "iron_wood",
            "hazardzone" => "hazard",
            "gmnote" => "gm_overlay",
            _ => "stone_floor"
        };
    }

    private static string DefaultSceneMap0164TextureKey(string objectKind, string? fillKey)
    {
        var material = DefaultSceneMap0164MaterialKey(objectKind, fillKey);
        return material switch
        {
            "cobblestone" => "cobble_small",
            "packed_dirt" => "dirt_track",
            "dark_stone" => "narrow_stone",
            "wood_floor" or "warm_wood" => "wood_planks",
            "stone_floor" => "stone_tiles",
            "canvas_red" => "canvas_stripe",
            "iron_wood" => "gate_planks",
            _ => material
        };
    }

    private static string DefaultSceneMap0164AssetKey(string objectKind)
    {
        return (objectKind ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "marketstall" => "asset_market_stall",
            "shoparea" => "asset_shop_sign",
            "tavernarea" => "asset_tavern_sign",
            "storagearea" => "asset_crates",
            "entrance" => "asset_gate",
            "door" => "asset_door",
            "cover" => "asset_cover",
            "obstacle" => "asset_barrels",
            "hazardzone" => "asset_hazard",
            "decoration" => "asset_prop",
            _ => string.Empty
        };
    }

    private static string DefaultSceneMap0164VisualStyleKey(string objectKind)
    {
        return (objectKind ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "terrainzone" => "terrain.cobblestone",
            "road" => "road.main",
            "alley" => "road.alley",
            "shoparea" => "building.shop",
            "tavernarea" => "building.tavern",
            "storagearea" => "building.storage",
            "marketstall" => "prop.market_stall",
            "entrance" => "structure.gate",
            "hazardzone" => "overlay.hazard",
            "gmnote" => "overlay.gm",
            _ => "object.default"
        };
    }

    private static double DefaultSceneMap0164StrokeThickness(string objectKind)
    {
        return (objectKind ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "wall" => 5d,
            "road" => 4d,
            "alley" => 3d,
            "entrance" or "door" or "exit" => 2.5d,
            _ => 1.4d
        };
    }

    private static IEnumerable<(double x, double y)> ParseSceneMap0164Points(string points)
    {
        if (string.IsNullOrWhiteSpace(points))
            yield break;
        foreach (var part in points.Split(new[] { ';', '|' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var xy = part.Split(',');
            if (xy.Length != 2)
                continue;
            if (double.TryParse(xy[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
                && double.TryParse(xy[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
                yield return (x, y);
        }
    }
}
