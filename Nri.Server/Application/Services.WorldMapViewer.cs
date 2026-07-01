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
    private const string WorldMapMvp01455MapId = "nri_world_map_mvp_01455";
    private const string WorldMapMvp01455CampaignId = "dev-campaign-core";
    private const string WorldMapMvp01455RuleSetId = "fantasy_nri_default";
    private const string WorldMapMvp01455Name = "Карта мира NRI";
    private const string WorldMapVisibleCity01455 = "PLAYER_VISIBLE_MAP_CITY_01455";
    private const string WorldMapVisibleRegion01455 = "PLAYER_VISIBLE_MAP_REGION_01455";
    private const string WorldMapVisibleGuiLocation01455 = "PLAYER_VISIBLE_MAP_GUI_LOCATION_01455";
    private const string WorldMapGmOnlyRuin01455 = "GM_ONLY_MAP_RUIN_01455_DO_NOT_LEAK";
    private const string WorldMapGmOnlyAnomaly01455 = "GM_ONLY_MAP_ANOMALY_01455_DO_NOT_LEAK";
    private const string WorldMapServerOnlyTrigger01455 = "SERVER_ONLY_MAP_TRIGGER_01455_DO_NOT_LEAK";

    public ResponseEnvelope WorldMapAdminSeedMvp(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!WorldMapViewerAdminEnabled())
            return WorldMapViewerDisabled(context.Request.Command);

        EnsureWorldMapViewerIndexes();
        ResetWorldMapMvp01455Documents();

        var now = DateTime.UtcNow;
        var map = _repositories.WorldMaps.GetByIdAsync(WorldMapMvp01455MapId).GetAwaiter().GetResult() ?? new WorldMapState
        {
            Id = WorldMapMvp01455MapId,
            CreatedAtUtc = now,
            CreatedByUserId = actor.Id
        };

        map.CampaignId = WorldMapMvp01455CampaignId;
        map.RuleSetId = WorldMapMvp01455RuleSetId;
        map.Name = WorldMapMvp01455Name;
        map.Description = "Структурная MVP-карта мира для Foundation 0.14.55.";
        map.WidthCells = 120;
        map.HeightCells = 80;
        map.CellSizeKm = 10d;
        map.ProjectionMode = WorldMapProjectionModeIds.FlatGrid;
        map.CoordinateMode = WorldMapCoordinateModeIds.Grid;
        map.VisibilityMode = MapVisibilityModes.Public;
        map.IsPlayerVisible = true;
        map.IsArchived = false;
        map.Archived = false;
        map.Deleted = false;
        map.UpdatedAtUtc = now;
        map.UpdatedByUserId = actor.Id;
        map.Tags = new List<string> { "0.14.55", "world-map-viewer-mvp" };
        _repositories.WorldMaps.UpsertAsync(map).GetAwaiter().GetResult();

        SeedWorldMapMvpLayers(map, actor.Id);
        SeedWorldMapMvpMarkers(map, actor.Id);
        SeedWorldMapMvpDocuments(map, actor.Id, now);
        WriteWorldMapViewerJournal(actor, map.Id, "world_map_seeded", "World Map MVP seeded.", "Карта мира NRI подготовлена для просмотра.");

        _logger.Admin($"world.map.viewer.seed.done mapId={map.Id} actor={actor.Login}");
        return Ok("World Map MVP seeded.", new Dictionary<string, object>
        {
            ["mapId"] = map.Id,
            ["mapName"] = map.Name,
            ["collections"] = new object[] { "world_map_profiles", "world_map_layers", "world_map_regions", "world_map_locations", "world_map_labels" }
        });
    }

    private void ResetWorldMapMvp01455Documents()
    {
        var mapIdFilter = Builders<BsonDocument>.Filter.Eq("MapId", WorldMapMvp01455MapId);
        var worldMapIdFilter = Builders<BsonDocument>.Filter.Eq("WorldMapId", WorldMapMvp01455MapId);
        var idFilter = Builders<BsonDocument>.Filter.Eq("_id", WorldMapMvp01455MapId);
        var entityIdFilter = Builders<BsonDocument>.Filter.Eq("Id", WorldMapMvp01455MapId);

        ViewerCollection("world_map_profiles").DeleteMany(mapIdFilter);
        ViewerCollection("world_map_regions").DeleteMany(mapIdFilter);
        ViewerCollection("world_map_locations").DeleteMany(mapIdFilter);
        ViewerCollection("world_map_labels").DeleteMany(mapIdFilter);
        _mongo.Database.GetCollection<BsonDocument>("world_map_layers").DeleteMany(worldMapIdFilter);
        _mongo.Database.GetCollection<BsonDocument>("world_map_legends").DeleteMany(mapIdFilter);
        _mongo.Database.GetCollection<BsonDocument>("map_markers").DeleteMany(mapIdFilter);
        _mongo.Database.GetCollection<BsonDocument>("map_marker_bindings").DeleteMany(mapIdFilter);
        _mongo.Database.GetCollection<BsonDocument>("world_map_states").DeleteMany(Builders<BsonDocument>.Filter.Or(idFilter, entityIdFilter));
    }

    public ResponseEnvelope WorldMapAdminGet(CommandContext context)
    {
        RequireAdmin(context);
        if (!WorldMapViewerAdminEnabled())
            return WorldMapViewerDisabled(context.Request.Command);

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var mapId = FirstNonEmptyWorld(PayloadReader.GetString(payload, "mapId"), WorldMapMvp01455MapId);
        var includeHidden = !payload.ContainsKey("includeHidden") || PayloadReader.GetBool(payload, "includeHidden");
        var map = _repositories.WorldMaps.GetByIdAsync(mapId).GetAwaiter().GetResult();
        if (map == null || map.Deleted || map.Archived || map.IsArchived)
            return Error("world map not found", ResponseStatus.NotFound, ErrorCode.NotFound);

        return Ok("World Map viewer loaded.", BuildWorldMapViewerPayload(map, admin: true, includeHidden: includeHidden));
    }

    public ResponseEnvelope WorldMapAdminCreateOrUpdateLocation(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!WorldMapViewerAdminEnabled())
            return WorldMapViewerDisabled(context.Request.Command);

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var mapId = RequireLength(PayloadReader.GetString(payload, "mapId"), 1, 128, "mapId");
        var map = _repositories.WorldMaps.GetByIdAsync(mapId).GetAwaiter().GetResult();
        if (map == null || map.Deleted || map.Archived || map.IsArchived)
            return Error("world map not found", ResponseStatus.NotFound, ErrorCode.NotFound);

        var visibility = NormalizeWorldMapViewerVisibility(PayloadReader.GetString(payload, "visibilityMode"), out var visibilityError);
        if (!string.IsNullOrWhiteSpace(visibilityError))
            return Error(visibilityError, ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);

        var locationId = FirstNonEmptyWorld(PayloadReader.GetString(payload, "locationId"), PayloadReader.GetString(payload, "id"));
        if (string.IsNullOrWhiteSpace(locationId))
            locationId = "world_map_location_" + Guid.NewGuid().ToString("N");

        var name = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "name"), PayloadReader.GetString(payload, "displayName")), 1, 160, "name");
        var cellX = PayloadReader.GetInt(payload, "cellX");
        var cellY = PayloadReader.GetInt(payload, "cellY");
        var xNormalized = PayloadReader.GetDouble(payload, "xNormalized");
        var yNormalized = PayloadReader.GetDouble(payload, "yNormalized");
        if (!ValidateWorldMapViewerCoordinates(map, cellX, cellY, xNormalized, yNormalized, out var coordinateError))
            return Error(coordinateError, ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);

        var now = DateTime.UtcNow;
        var doc = ExistingViewerDocument("world_map_locations", locationId) ?? new BsonDocument
        {
            ["Id"] = locationId,
            ["CreatedAtUtc"] = now,
            ["CreatedByUserId"] = actor.Id
        };

        doc["MapId"] = map.Id;
        doc["CampaignId"] = map.CampaignId ?? string.Empty;
        doc["Name"] = name;
        doc["DisplayName"] = name;
        doc["LocationType"] = FirstNonEmptyWorld(PayloadReader.GetString(payload, "locationType"), PayloadReader.GetString(payload, "markerType"), MapMarkerTypeIds.Location);
        doc["CellX"] = cellX ?? -1;
        doc["CellY"] = cellY ?? -1;
        doc["XNormalized"] = xNormalized ?? (cellX.HasValue ? (double)cellX.Value / Math.Max(1, map.WidthCells - 1) : 0.5d);
        doc["YNormalized"] = yNormalized ?? (cellY.HasValue ? (double)cellY.Value / Math.Max(1, map.HeightCells - 1) : 0.5d);
        doc["VisibilityMode"] = visibility;
        doc["IsPlayerVisible"] = !payload.ContainsKey("isPlayerVisible") || PayloadReader.GetBool(payload, "isPlayerVisible");
        doc["PublicDescription"] = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "publicDescription"), PayloadReader.GetString(payload, "publicNotes"), PayloadReader.GetString(payload, "description")), 0, 4096, "publicDescription");
        doc["GMNotes"] = RequireLength(PayloadReader.GetString(payload, "gmNotes"), 0, 4096, "gmNotes");
        doc["LinkedEntityType"] = NormalizeMarkerBindingType(PayloadReader.GetString(payload, "linkedEntityType"));
        doc["LinkedEntityId"] = RequireLength(PayloadReader.GetString(payload, "linkedEntityId"), 0, 128, "linkedEntityId");
        doc["LinkedEntityDisplayName"] = RequireLength(PayloadReader.GetString(payload, "linkedEntityDisplayName"), 0, 160, "linkedEntityDisplayName");
        doc["UpdatedAtUtc"] = now;
        doc["UpdatedByUserId"] = actor.Id;
        doc["IsArchived"] = false;

        UpsertViewerDocument("world_map_locations", doc);
        UpsertMarkerFromViewerLocation(map, doc, actor.Id);
        WriteWorldMapViewerJournal(actor, map.Id, "world_map_location_saved", $"World map location saved: {name}", $"На карту мира добавлена локация: {name}.");

        return Ok("World map location saved.", new Dictionary<string, object>
        {
            ["mapId"] = map.Id,
            ["locationId"] = locationId,
            ["location"] = ViewerDocumentPayload(doc, admin: true)
        });
    }

    public ResponseEnvelope WorldMapAdminCreateOrUpdateRegion(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!WorldMapViewerAdminEnabled())
            return WorldMapViewerDisabled(context.Request.Command);

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var mapId = RequireLength(PayloadReader.GetString(payload, "mapId"), 1, 128, "mapId");
        var map = _repositories.WorldMaps.GetByIdAsync(mapId).GetAwaiter().GetResult();
        if (map == null || map.Deleted || map.Archived || map.IsArchived)
            return Error("world map not found", ResponseStatus.NotFound, ErrorCode.NotFound);

        var geometry = BuildViewerRegionGeometry(map, payload, out var geometryError);
        if (geometry == null)
            return Error(geometryError, ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);

        var visibility = NormalizeWorldMapViewerVisibility(PayloadReader.GetString(payload, "visibilityMode"), out var visibilityError);
        if (!string.IsNullOrWhiteSpace(visibilityError))
            return Error(visibilityError, ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);

        var regionId = FirstNonEmptyWorld(PayloadReader.GetString(payload, "regionId"), PayloadReader.GetString(payload, "id"));
        if (string.IsNullOrWhiteSpace(regionId))
            regionId = "world_map_region_" + Guid.NewGuid().ToString("N");

        var name = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "name"), PayloadReader.GetString(payload, "displayName")), 1, 160, "name");
        var now = DateTime.UtcNow;
        var doc = ExistingViewerDocument("world_map_regions", regionId) ?? new BsonDocument
        {
            ["Id"] = regionId,
            ["CreatedAtUtc"] = now,
            ["CreatedByUserId"] = actor.Id
        };

        doc["MapId"] = map.Id;
        doc["CampaignId"] = map.CampaignId ?? string.Empty;
        doc["Name"] = name;
        doc["DisplayName"] = name;
        doc["RegionType"] = FirstNonEmptyWorld(PayloadReader.GetString(payload, "regionType"), "region");
        doc["LayerType"] = FirstNonEmptyWorld(PayloadReader.GetString(payload, "layerType"), WorldMapLayerTypeIds.Political);
        doc["Geometry"] = geometry;
        doc["VisibilityMode"] = visibility;
        doc["IsPlayerVisible"] = !payload.ContainsKey("isPlayerVisible") || PayloadReader.GetBool(payload, "isPlayerVisible");
        doc["PublicDescription"] = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "publicDescription"), PayloadReader.GetString(payload, "description")), 0, 4096, "publicDescription");
        doc["GMNotes"] = RequireLength(PayloadReader.GetString(payload, "gmNotes"), 0, 4096, "gmNotes");
        doc["UpdatedAtUtc"] = now;
        doc["UpdatedByUserId"] = actor.Id;
        doc["IsArchived"] = false;
        UpsertViewerDocument("world_map_regions", doc);

        WriteWorldMapViewerJournal(actor, map.Id, "world_map_region_saved", $"World map region saved: {name}", $"На карту мира добавлен регион: {name}.");
        return Ok("World map region saved.", new Dictionary<string, object>
        {
            ["mapId"] = map.Id,
            ["regionId"] = regionId,
            ["region"] = ViewerDocumentPayload(doc, admin: true)
        });
    }

    public ResponseEnvelope WorldMapAdminUpdateVisibility(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!WorldMapViewerAdminEnabled())
            return WorldMapViewerDisabled(context.Request.Command);

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var entityType = FirstNonEmptyWorld(PayloadReader.GetString(payload, "entityType"), PayloadReader.GetString(payload, "type")).Trim().ToLowerInvariant();
        var entityId = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "entityId"), PayloadReader.GetString(payload, "id")), 1, 128, "entityId");
        var visibility = NormalizeWorldMapViewerVisibility(PayloadReader.GetString(payload, "visibilityMode"), out var visibilityError);
        if (!string.IsNullOrWhiteSpace(visibilityError))
            return Error(visibilityError, ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);

        var isPlayerVisible = !payload.ContainsKey("isPlayerVisible") || PayloadReader.GetBool(payload, "isPlayerVisible");
        var now = DateTime.UtcNow;
        var collection = ViewerCollectionForEntityType(entityType);
        if (string.IsNullOrWhiteSpace(collection))
            return Error("unsupported world map viewer entity type", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);

        var doc = ExistingViewerDocument(collection, entityId);
        if (doc == null)
            return Error("world map viewer entity not found", ResponseStatus.NotFound, ErrorCode.NotFound);

        doc["VisibilityMode"] = visibility;
        doc["IsPlayerVisible"] = isPlayerVisible;
        doc["UpdatedAtUtc"] = now;
        doc["UpdatedByUserId"] = actor.Id;
        UpsertViewerDocument(collection, doc);

        return Ok("World map visibility updated.", new Dictionary<string, object>
        {
            ["entityType"] = entityType,
            ["entityId"] = entityId,
            ["visibilityMode"] = visibility,
            ["isPlayerVisible"] = isPlayerVisible
        });
    }

    public ResponseEnvelope WorldMapAdminValidate(CommandContext context)
    {
        RequireAdmin(context);
        if (!WorldMapViewerAdminEnabled())
            return WorldMapViewerDisabled(context.Request.Command);

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var mapId = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "mapId"), WorldMapMvp01455MapId), 1, 128, "mapId");
        var map = _repositories.WorldMaps.GetByIdAsync(mapId).GetAwaiter().GetResult();
        if (map == null || map.Deleted || map.Archived || map.IsArchived)
            return Error("world map not found", ResponseStatus.NotFound, ErrorCode.NotFound);

        if (payload.ContainsKey("x") || payload.ContainsKey("cellX") || payload.ContainsKey("xNormalized"))
        {
            var cellX = PayloadReader.GetInt(payload, "cellX") ?? PayloadReader.GetInt(payload, "x");
            var cellY = PayloadReader.GetInt(payload, "cellY") ?? PayloadReader.GetInt(payload, "y");
            var xNormalized = PayloadReader.GetDouble(payload, "xNormalized");
            var yNormalized = PayloadReader.GetDouble(payload, "yNormalized");
            if (!ValidateWorldMapViewerCoordinates(map, cellX, cellY, xNormalized, yNormalized, out var coordinateError))
                return Error(coordinateError, ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);
        }

        if (payload.ContainsKey("visibilityMode"))
        {
            var visibility = NormalizeWorldMapViewerVisibility(PayloadReader.GetString(payload, "visibilityMode"), out var visibilityError);
            if (!string.IsNullOrWhiteSpace(visibilityError))
                return Error(visibilityError, ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);
        }

        var profileCount = CountViewerDocuments("world_map_profiles", mapId);
        var regionCount = CountViewerDocuments("world_map_regions", mapId);
        var locationCount = CountViewerDocuments("world_map_locations", mapId);
        var labelCount = CountViewerDocuments("world_map_labels", mapId);
        return Ok("World Map viewer validation passed.", new Dictionary<string, object>
        {
            ["mapId"] = mapId,
            ["profileCount"] = profileCount,
            ["regionCount"] = regionCount,
            ["locationCount"] = locationCount,
            ["labelCount"] = labelCount
        });
    }

    public ResponseEnvelope WorldMapPlayerLocationGet(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!WorldMapViewerPlayerEnabled())
            return WorldMapViewerDisabled(context.Request.Command);

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var mapId = RequireLength(PayloadReader.GetString(payload, "mapId"), 1, 128, "mapId");
        var locationId = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "locationId"), PayloadReader.GetString(payload, "id")), 1, 128, "locationId");
        var map = _repositories.WorldMaps.GetByIdAsync(mapId).GetAwaiter().GetResult();
        if (map == null || map.Deleted || map.Archived || map.IsArchived || !IsWorldMapVisibleForPlayer(map))
            return Error("world map not found", ResponseStatus.NotFound, ErrorCode.NotFound);

        var doc = ExistingViewerDocument("world_map_locations", locationId);
        if (doc == null || !string.Equals(ViewerString(doc, "MapId"), mapId, StringComparison.OrdinalIgnoreCase) || !IsViewerDocumentVisibleForPlayer(doc))
        {
            _logger.Debug($"world.map.player.location.get.hidden user={actor.Login} mapId={mapId} locationId={locationId}");
            return Error("world map location not found", ResponseStatus.NotFound, ErrorCode.NotFound);
        }

        return Ok("Player world map location loaded.", new Dictionary<string, object>
        {
            ["location"] = ViewerDocumentPayload(doc, admin: false)
        });
    }

    public ResponseEnvelope WorldMapPlayerRegionGet(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!WorldMapViewerPlayerEnabled())
            return WorldMapViewerDisabled(context.Request.Command);

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var mapId = RequireLength(PayloadReader.GetString(payload, "mapId"), 1, 128, "mapId");
        var regionId = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "regionId"), PayloadReader.GetString(payload, "id")), 1, 128, "regionId");
        var map = _repositories.WorldMaps.GetByIdAsync(mapId).GetAwaiter().GetResult();
        if (map == null || map.Deleted || map.Archived || map.IsArchived || !IsWorldMapVisibleForPlayer(map))
            return Error("world map not found", ResponseStatus.NotFound, ErrorCode.NotFound);

        var doc = ExistingViewerDocument("world_map_regions", regionId);
        if (doc == null || !string.Equals(ViewerString(doc, "MapId"), mapId, StringComparison.OrdinalIgnoreCase) || !IsViewerDocumentVisibleForPlayer(doc))
        {
            _logger.Debug($"world.map.player.region.get.hidden user={actor.Login} mapId={mapId} regionId={regionId}");
            return Error("world map region not found", ResponseStatus.NotFound, ErrorCode.NotFound);
        }

        return Ok("Player world map region loaded.", new Dictionary<string, object>
        {
            ["region"] = ViewerDocumentPayload(doc, admin: false)
        });
    }

    private void SeedWorldMapMvpLayers(WorldMapState map, string actorUserId)
    {
        var height = LoadOrCreateWorldLayer(map, WorldMapLayerTypeIds.HeightDepth, actorUserId);
        var heightCells = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        PaintViewerRect(heightCells, 0, 0, map.WidthCells, map.HeightCells, BuildLayerCellValue(WorldMapLayerTypeIds.HeightDepth, WorldMapHeightDepthCategoryIds.DeepOcean, "Глубокий океан")!);
        PaintViewerRect(heightCells, 17, 12, 42, 34, BuildLayerCellValue(WorldMapLayerTypeIds.HeightDepth, WorldMapHeightDepthCategoryIds.Lowland, "Материк Ардена")!);
        PaintViewerRect(heightCells, 45, 18, 20, 16, BuildLayerCellValue(WorldMapLayerTypeIds.HeightDepth, WorldMapHeightDepthCategoryIds.Highland, "Высокие плато")!);
        PaintViewerRect(heightCells, 54, 23, 10, 12, BuildLayerCellValue(WorldMapLayerTypeIds.HeightDepth, WorldMapHeightDepthCategoryIds.Mountain, "Горный хребет")!);
        PaintViewerRect(heightCells, 72, 40, 30, 22, BuildLayerCellValue(WorldMapLayerTypeIds.HeightDepth, WorldMapHeightDepthCategoryIds.Lowland, "Южный материк")!);
        height.Data["cells"] = heightCells;
        height.IsVisibleToPlayers = true;
        height.IsVisibleToGM = true;
        height.UpdatedAtUtc = DateTime.UtcNow;
        height.UpdatedByUserId = actorUserId;
        _repositories.WorldMapLayers.UpsertAsync(height).GetAwaiter().GetResult();

        var biome = LoadOrCreateWorldLayer(map, WorldMapLayerTypeIds.Biome, actorUserId);
        var biomeCells = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        PaintViewerRect(biomeCells, 0, 0, map.WidthCells, map.HeightCells, BuildLayerCellValue(WorldMapLayerTypeIds.Biome, WorldMapBiomeIds.Ocean, "Океан")!);
        PaintViewerRect(biomeCells, 20, 14, 28, 20, BuildLayerCellValue(WorldMapLayerTypeIds.Biome, WorldMapBiomeIds.Forest, "Леса Ардены")!);
        PaintViewerRect(biomeCells, 44, 28, 18, 14, BuildLayerCellValue(WorldMapLayerTypeIds.Biome, WorldMapBiomeIds.Plains, "Равнины")!);
        PaintViewerRect(biomeCells, 54, 23, 10, 12, BuildLayerCellValue(WorldMapLayerTypeIds.Biome, WorldMapBiomeIds.Mountains, "Горы")!);
        PaintViewerRect(biomeCells, 76, 43, 18, 14, BuildLayerCellValue(WorldMapLayerTypeIds.Biome, WorldMapBiomeIds.Desert, "Сухие земли")!);
        biome.Data["cells"] = biomeCells;
        biome.IsVisibleToPlayers = true;
        biome.IsVisibleToGM = true;
        biome.UpdatedAtUtc = DateTime.UtcNow;
        biome.UpdatedByUserId = actorUserId;
        _repositories.WorldMapLayers.UpsertAsync(biome).GetAwaiter().GetResult();

        var political = LoadOrCreateWorldLayer(map, WorldMapLayerTypeIds.Political, actorUserId);
        var politicalCells = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        PaintViewerRect(politicalCells, 22, 16, 18, 16, BuildLayerCellValue(WorldMapLayerTypeIds.Political, "ardena", "Королевство Ардена")!);
        PaintViewerRect(politicalCells, 41, 18, 16, 18, BuildLayerCellValue(WorldMapLayerTypeIds.Political, "free_marches", "Свободные марки")!);
        PaintViewerRect(politicalCells, 74, 42, 20, 18, BuildLayerCellValue(WorldMapLayerTypeIds.Political, "south_league", "Южная лига")!);
        political.Data["cells"] = politicalCells;
        political.IsVisibleToPlayers = true;
        political.IsVisibleToGM = true;
        political.UpdatedAtUtc = DateTime.UtcNow;
        political.UpdatedByUserId = actorUserId;
        _repositories.WorldMapLayers.UpsertAsync(political).GetAwaiter().GetResult();
    }

    private void SeedWorldMapMvpMarkers(WorldMapState map, string actorUserId)
    {
        UpsertSeedWorldMarker(map, "world_map_marker_visible_city_01455", WorldMapVisibleCity01455, MapMarkerTypeIds.Capital, 31, 23, true, MapVisibilityModes.Public, "Открытая игрокам столица.", string.Empty, string.Empty, actorUserId);
        UpsertSeedWorldMarker(map, "world_map_marker_visible_region_01455", WorldMapVisibleRegion01455, MapMarkerTypeIds.Region, 44, 27, true, MapVisibilityModes.Public, "Открытый регион карты мира.", string.Empty, string.Empty, actorUserId);
        UpsertSeedWorldMarker(map, "world_map_marker_hidden_ruin_01455", WorldMapGmOnlyRuin01455, MapMarkerTypeIds.Ruin, 58, 33, false, MapVisibilityModes.GmOnly, string.Empty, WorldMapGmOnlyRuin01455, string.Empty, actorUserId);
        UpsertSeedWorldMarker(map, "world_map_marker_hidden_anomaly_01455", WorldMapGmOnlyAnomaly01455, MapMarkerTypeIds.PointOfInterest, 82, 49, false, MapVisibilityModes.GmOnly, string.Empty, WorldMapGmOnlyAnomaly01455, string.Empty, actorUserId);
        UpsertSeedWorldMarker(map, "world_map_marker_server_trigger_01455", "Серверный триггер карты", MapMarkerTypeIds.Custom, 12, 65, false, "server_only", string.Empty, string.Empty, WorldMapServerOnlyTrigger01455, actorUserId);
    }

    private void SeedWorldMapMvpDocuments(WorldMapState map, string actorUserId, DateTime now)
    {
        UpsertViewerDocument("world_map_profiles", new BsonDocument
        {
            ["Id"] = WorldMapMvp01455MapId,
            ["MapId"] = map.Id,
            ["WorldId"] = "nri_world_01455",
            ["CampaignId"] = map.CampaignId,
            ["Name"] = map.Name,
            ["DisplayName"] = map.Name,
            ["Description"] = map.Description,
            ["WidthCells"] = map.WidthCells,
            ["HeightCells"] = map.HeightCells,
            ["ProjectionMode"] = map.ProjectionMode,
            ["CoordinateMode"] = map.CoordinateMode,
            ["VisibilityMode"] = MapVisibilityModes.Public,
            ["IsPlayerVisible"] = true,
            ["IsArchived"] = false,
            ["CreatedAtUtc"] = now,
            ["UpdatedAtUtc"] = now,
            ["CreatedByUserId"] = actorUserId,
            ["UpdatedByUserId"] = actorUserId,
            ["Tags"] = new BsonArray { "0.14.55", "world-map-viewer-mvp" }
        });

        UpsertRegionDoc(map, "world_map_region_sea_01455", "Северное море NRI", "sea", WorldMapLayerTypeIds.HeightDepth, MapVisibilityModes.Public, true, RectGeometry(0, 0, map.WidthCells, map.HeightCells), now, actorUserId);
        UpsertRegionDoc(map, "world_map_region_continent_01455", "Материк Ардена", "continent", WorldMapLayerTypeIds.HeightDepth, MapVisibilityModes.Public, true, RectGeometry(17, 12, 48, 34), now, actorUserId);
        UpsertRegionDoc(map, "world_map_region_player_visible_01455", WorldMapVisibleRegion01455, "region", WorldMapLayerTypeIds.Political, MapVisibilityModes.Public, true, RectGeometry(22, 16, 35, 22), now, actorUserId);
        UpsertRegionDoc(map, "world_map_region_hidden_border_01455", "Скрытая спорная граница NRI", "border", WorldMapLayerTypeIds.Political, MapVisibilityModes.GmOnly, false, RectGeometry(54, 22, 8, 14), now, actorUserId);

        UpsertLocationDoc(map, "world_map_location_visible_city_01455", WorldMapVisibleCity01455, MapMarkerTypeIds.Capital, 31, 23, MapVisibilityModes.Public, true, "Открытая игрокам столица.", string.Empty, string.Empty, now, actorUserId);
        UpsertLocationDoc(map, "world_map_location_hidden_ruin_01455", WorldMapGmOnlyRuin01455, MapMarkerTypeIds.Ruin, 58, 33, MapVisibilityModes.GmOnly, false, string.Empty, WorldMapGmOnlyRuin01455, string.Empty, now, actorUserId);
        UpsertLocationDoc(map, "world_map_location_hidden_anomaly_01455", WorldMapGmOnlyAnomaly01455, MapMarkerTypeIds.PointOfInterest, 82, 49, MapVisibilityModes.GmOnly, false, string.Empty, WorldMapGmOnlyAnomaly01455, string.Empty, now, actorUserId);
        UpsertLocationDoc(map, "world_map_location_server_trigger_01455", "Серверный триггер карты", MapMarkerTypeIds.Custom, 12, 65, "server_only", false, string.Empty, string.Empty, WorldMapServerOnlyTrigger01455, now, actorUserId);

        UpsertLabelDoc(map, "world_map_label_city_01455", WorldMapVisibleCity01455, 31, 22, MapVisibilityModes.Public, true, now, actorUserId);
        UpsertLabelDoc(map, "world_map_label_region_01455", WorldMapVisibleRegion01455, 42, 25, MapVisibilityModes.Public, true, now, actorUserId);
    }

    private void UpsertSeedWorldMarker(WorldMapState map, string markerId, string name, string markerType, int cellX, int cellY, bool isPlayerVisible, string visibilityMode, string publicNotes, string gmNotes, string serverOnlyToken, string actorUserId)
    {
        var now = DateTime.UtcNow;
        var marker = _repositories.MapMarkers.GetByIdAsync(markerId).GetAwaiter().GetResult() ?? new MapMarkerState
        {
            Id = markerId,
            CreatedAtUtc = now,
            CreatedByUserId = actorUserId
        };

        marker.MapId = map.Id;
        marker.CampaignId = map.CampaignId;
        marker.Name = name;
        marker.MarkerType = markerType;
        marker.CellX = cellX;
        marker.CellY = cellY;
        marker.XNormalized = (double)cellX / Math.Max(1, map.WidthCells - 1);
        marker.YNormalized = (double)cellY / Math.Max(1, map.HeightCells - 1);
        marker.IsPlayerVisible = isPlayerVisible;
        marker.VisibilityMode = visibilityMode;
        marker.PublicNotes = publicNotes;
        marker.GMNotes = gmNotes;
        marker.CardTitle = name;
        marker.CardDescription = publicNotes;
        marker.LinkedEntityType = markerType == MapMarkerTypeIds.Region ? MapMarkerBindingTypeIds.Region : MapMarkerBindingTypeIds.Location;
        marker.LinkedEntityId = markerId.Replace("marker", markerType == MapMarkerTypeIds.Region ? "region" : "location");
        marker.LinkedEntityDisplayName = name;
        marker.LinkedEntityPublicLabel = isPlayerVisible ? name : string.Empty;
        marker.ServerOnlyData = string.IsNullOrWhiteSpace(serverOnlyToken)
            ? new Dictionary<string, object>()
            : new Dictionary<string, object> { ["trigger"] = serverOnlyToken };
        marker.Archived = false;
        marker.Deleted = false;
        marker.UpdatedAtUtc = now;
        marker.UpdatedByUserId = actorUserId;

        var saved = _repositories.MapMarkers.UpsertAsync(marker).GetAwaiter().GetResult();
        UpsertWorldMapLocationFromMarker(map, saved, actorUserId);
    }

    private void UpsertRegionDoc(WorldMapState map, string id, string name, string regionType, string layerType, string visibilityMode, bool isPlayerVisible, BsonDocument geometry, DateTime now, string actorUserId)
    {
        UpsertViewerDocument("world_map_regions", new BsonDocument
        {
            ["Id"] = id,
            ["MapId"] = map.Id,
            ["CampaignId"] = map.CampaignId,
            ["Name"] = name,
            ["DisplayName"] = name,
            ["RegionType"] = regionType,
            ["LayerType"] = layerType,
            ["Geometry"] = geometry,
            ["VisibilityMode"] = visibilityMode,
            ["IsPlayerVisible"] = isPlayerVisible,
            ["IsArchived"] = false,
            ["CreatedAtUtc"] = now,
            ["UpdatedAtUtc"] = now,
            ["CreatedByUserId"] = actorUserId,
            ["UpdatedByUserId"] = actorUserId
        });
    }

    private void UpsertLocationDoc(WorldMapState map, string id, string name, string locationType, int cellX, int cellY, string visibilityMode, bool isPlayerVisible, string publicDescription, string gmNotes, string serverOnlyToken, DateTime now, string actorUserId)
    {
        var doc = new BsonDocument
        {
            ["Id"] = id,
            ["MapId"] = map.Id,
            ["CampaignId"] = map.CampaignId,
            ["Name"] = name,
            ["DisplayName"] = name,
            ["LocationType"] = locationType,
            ["CellX"] = cellX,
            ["CellY"] = cellY,
            ["XNormalized"] = (double)cellX / Math.Max(1, map.WidthCells - 1),
            ["YNormalized"] = (double)cellY / Math.Max(1, map.HeightCells - 1),
            ["VisibilityMode"] = visibilityMode,
            ["IsPlayerVisible"] = isPlayerVisible,
            ["PublicDescription"] = publicDescription,
            ["GMNotes"] = gmNotes,
            ["IsArchived"] = false,
            ["CreatedAtUtc"] = now,
            ["UpdatedAtUtc"] = now,
            ["CreatedByUserId"] = actorUserId,
            ["UpdatedByUserId"] = actorUserId
        };
        if (!string.IsNullOrWhiteSpace(serverOnlyToken))
            doc["ServerOnlyData"] = new BsonDocument { ["trigger"] = serverOnlyToken };
        UpsertViewerDocument("world_map_locations", doc);
    }

    private void UpsertLabelDoc(WorldMapState map, string id, string label, int cellX, int cellY, string visibilityMode, bool isPlayerVisible, DateTime now, string actorUserId)
    {
        UpsertViewerDocument("world_map_labels", new BsonDocument
        {
            ["Id"] = id,
            ["MapId"] = map.Id,
            ["CampaignId"] = map.CampaignId,
            ["Text"] = label,
            ["Label"] = label,
            ["CellX"] = cellX,
            ["CellY"] = cellY,
            ["VisibilityMode"] = visibilityMode,
            ["IsPlayerVisible"] = isPlayerVisible,
            ["IsArchived"] = false,
            ["CreatedAtUtc"] = now,
            ["UpdatedAtUtc"] = now,
            ["CreatedByUserId"] = actorUserId,
            ["UpdatedByUserId"] = actorUserId
        });
    }

    private void UpsertWorldMapLocationFromMarker(WorldMapState map, MapMarkerState marker, string actorUserId)
    {
        if (!string.Equals(marker.MarkerType, MapMarkerTypeIds.Location, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(marker.MarkerType, MapMarkerTypeIds.Capital, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(marker.MarkerType, MapMarkerTypeIds.City, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(marker.MarkerType, MapMarkerTypeIds.CityState, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(marker.MarkerType, MapMarkerTypeIds.PointOfInterest, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(marker.MarkerType, MapMarkerTypeIds.Ruin, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(marker.MarkerType, MapMarkerTypeIds.Dungeon, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(marker.MarkerType, MapMarkerTypeIds.Port, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(marker.MarkerType, MapMarkerTypeIds.Region, StringComparison.OrdinalIgnoreCase))
            return;

        var collection = string.Equals(marker.MarkerType, MapMarkerTypeIds.Region, StringComparison.OrdinalIgnoreCase)
            ? "world_map_regions"
            : "world_map_locations";
        var id = FirstNonEmptyWorld(marker.LinkedLocationId, marker.LinkedRegionId, marker.LinkedEntityId, marker.Id);
        var now = DateTime.UtcNow;
        var doc = ExistingViewerDocument(collection, id) ?? new BsonDocument
        {
            ["Id"] = id,
            ["CreatedAtUtc"] = marker.CreatedAtUtc == default ? now : marker.CreatedAtUtc,
            ["CreatedByUserId"] = FirstNonEmptyWorld(marker.CreatedByUserId, actorUserId)
        };

        doc["MapId"] = map.Id;
        doc["CampaignId"] = map.CampaignId ?? string.Empty;
        doc["Name"] = marker.Name ?? string.Empty;
        doc["DisplayName"] = marker.Name ?? string.Empty;
        doc["VisibilityMode"] = marker.VisibilityMode ?? MapVisibilityModes.Party;
        doc["IsPlayerVisible"] = marker.IsPlayerVisible;
        doc["IsArchived"] = marker.Archived || marker.Deleted;
        doc["CellX"] = marker.CellX ?? -1;
        doc["CellY"] = marker.CellY ?? -1;
        doc["XNormalized"] = marker.XNormalized ?? -1d;
        doc["YNormalized"] = marker.YNormalized ?? -1d;
        doc["PublicDescription"] = FirstNonEmptyWorld(marker.CardDescription, marker.PublicNotes);
        doc["GMNotes"] = marker.GMNotes ?? string.Empty;
        doc["LinkedEntityType"] = marker.LinkedEntityType ?? string.Empty;
        doc["LinkedEntityId"] = marker.LinkedEntityId ?? string.Empty;
        doc["LinkedEntityDisplayName"] = marker.LinkedEntityDisplayName ?? string.Empty;
        doc["UpdatedAtUtc"] = now;
        doc["UpdatedByUserId"] = actorUserId;
        if (collection == "world_map_regions")
        {
            doc["RegionType"] = "region";
            doc["LayerType"] = WorldMapLayerTypeIds.Political;
            if (!doc.Contains("Geometry")) doc["Geometry"] = RectGeometry(marker.CellX ?? 0, marker.CellY ?? 0, 2, 2);
        }
        else
        {
            doc["LocationType"] = marker.MarkerType ?? MapMarkerTypeIds.Location;
        }

        if (marker.ServerOnlyData != null && marker.ServerOnlyData.Count > 0)
        {
            var serverOnly = new BsonDocument();
            foreach (var pair in marker.ServerOnlyData)
                serverOnly[pair.Key] = BsonValue.Create(pair.Value);
            doc["ServerOnlyData"] = serverOnly;
        }

        UpsertViewerDocument(collection, doc);
    }

    private void UpsertMarkerFromViewerLocation(WorldMapState map, BsonDocument doc, string actorUserId)
    {
        var id = ViewerString(doc, "Id");
        if (string.IsNullOrWhiteSpace(id)) return;
        var marker = _repositories.MapMarkers.GetByIdAsync(id).GetAwaiter().GetResult() ?? new MapMarkerState
        {
            Id = id,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = actorUserId
        };

        marker.MapId = map.Id;
        marker.CampaignId = map.CampaignId;
        marker.Name = FirstNonEmptyWorld(ViewerString(doc, "DisplayName"), ViewerString(doc, "Name"), "Маркер");
        marker.MarkerType = FirstNonEmptyWorld(ViewerString(doc, "LocationType"), MapMarkerTypeIds.Location);
        marker.CellX = doc.TryGetValue("CellX", out var cellX) && cellX.IsNumeric ? cellX.ToInt32() : marker.CellX;
        marker.CellY = doc.TryGetValue("CellY", out var cellY) && cellY.IsNumeric ? cellY.ToInt32() : marker.CellY;
        marker.XNormalized = doc.TryGetValue("XNormalized", out var xNormalized) && xNormalized.IsNumeric ? xNormalized.ToDouble() : marker.XNormalized;
        marker.YNormalized = doc.TryGetValue("YNormalized", out var yNormalized) && yNormalized.IsNumeric ? yNormalized.ToDouble() : marker.YNormalized;
        marker.VisibilityMode = FirstNonEmptyWorld(ViewerString(doc, "VisibilityMode"), MapVisibilityModes.Party);
        marker.IsPlayerVisible = ViewerBool(doc, "IsPlayerVisible", true);
        marker.PublicNotes = ViewerString(doc, "PublicDescription");
        marker.GMNotes = ViewerString(doc, "GMNotes");
        marker.CardTitle = marker.Name;
        marker.CardDescription = marker.PublicNotes;
        marker.LinkedEntityType = FirstNonEmptyWorld(ViewerString(doc, "LinkedEntityType"), MapMarkerBindingTypeIds.Location);
        marker.LinkedEntityId = FirstNonEmptyWorld(ViewerString(doc, "LinkedEntityId"), id);
        marker.LinkedEntityDisplayName = FirstNonEmptyWorld(ViewerString(doc, "LinkedEntityDisplayName"), marker.Name);
        marker.LinkedEntityPublicLabel = marker.IsPlayerVisible ? marker.Name : string.Empty;
        marker.LinkedLocationId = id;
        marker.Archived = ViewerBool(doc, "IsArchived", false);
        marker.Deleted = false;
        marker.UpdatedAtUtc = DateTime.UtcNow;
        marker.UpdatedByUserId = actorUserId;
        _repositories.MapMarkers.UpsertAsync(marker).GetAwaiter().GetResult();
    }

    private void ArchiveWorldMapLocationForMarker(MapMarkerState marker, string actorUserId)
    {
        foreach (var collection in new[] { "world_map_locations", "world_map_regions" })
        {
            var id = FirstNonEmptyWorld(marker.LinkedLocationId, marker.LinkedRegionId, marker.LinkedEntityId, marker.Id);
            var doc = ExistingViewerDocument(collection, id);
            if (doc == null) continue;
            doc["IsArchived"] = true;
            doc["UpdatedAtUtc"] = DateTime.UtcNow;
            doc["UpdatedByUserId"] = actorUserId;
            UpsertViewerDocument(collection, doc);
        }
    }

    private Dictionary<string, object> BuildWorldMapViewerPayload(WorldMapState map, bool admin, bool includeHidden)
    {
        var include = admin ? includeHidden : false;
        return new Dictionary<string, object>
        {
            ["map"] = WorldMapPayload(map),
            ["profile"] = ViewerFirstPayload("world_map_profiles", map.Id, admin, include),
            ["regions"] = ViewerDocumentsPayload("world_map_regions", map.Id, admin, include),
            ["locations"] = ViewerDocumentsPayload("world_map_locations", map.Id, admin, include),
            ["labels"] = ViewerDocumentsPayload("world_map_labels", map.Id, admin, include),
            ["layers"] = _repositories.WorldMapLayers.ListByMapAsync(map.Id, includeArchived: false, limit: 5000).GetAwaiter().GetResult().Select(WorldLayerPayload).Cast<object>().ToArray(),
            ["markers"] = _repositories.MapMarkers.ListByMapAsync(map.Id, includeArchived: false, limit: 5000).GetAwaiter().GetResult().Select(WorldMarkerPayload).Cast<object>().ToArray(),
            ["legends"] = BuildWorldLegendsPayload(),
            ["builtAtUtc"] = DateTime.UtcNow
        };
    }

    private object[] ViewerDocumentsPayload(string collection, string mapId, bool admin, bool includeHidden)
    {
        return ViewerCollection(collection)
            .Find(Builders<BsonDocument>.Filter.Eq("MapId", mapId))
            .Limit(5000)
            .ToList()
            .Where(doc => !ViewerBool(doc, "IsArchived", false))
            .Where(doc => admin ? includeHidden || IsViewerDocumentVisibleForPlayer(doc) : IsViewerDocumentVisibleForPlayer(doc))
            .Select(doc => ViewerDocumentPayload(doc, admin))
            .Cast<object>()
            .ToArray();
    }

    private Dictionary<string, object> ViewerFirstPayload(string collection, string mapId, bool admin, bool includeHidden)
    {
        var doc = ViewerCollection(collection).Find(Builders<BsonDocument>.Filter.Eq("MapId", mapId)).FirstOrDefault();
        if (doc == null) return new Dictionary<string, object>();
        if (!admin && !IsViewerDocumentVisibleForPlayer(doc)) return new Dictionary<string, object>();
        if (admin || includeHidden) return ViewerDocumentPayload(doc, admin);
        return IsViewerDocumentVisibleForPlayer(doc) ? ViewerDocumentPayload(doc, admin) : new Dictionary<string, object>();
    }

    private Dictionary<string, object> ViewerDocumentPayload(BsonDocument doc, bool admin)
    {
        var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var element in doc.Elements)
        {
            if (!admin && IsViewerGmOnlyField(element.Name)) continue;
            if (!admin && element.Name.Equals("ServerOnlyData", StringComparison.OrdinalIgnoreCase)) continue;
            result[element.Name] = ViewerBsonToObject(element.Value, admin);
        }

        return result;
    }

    private static bool IsViewerGmOnlyField(string name)
    {
        return name.Equals("GMNotes", StringComparison.OrdinalIgnoreCase)
            || name.Equals("GMDescription", StringComparison.OrdinalIgnoreCase)
            || name.Equals("AdminOnlyNotes", StringComparison.OrdinalIgnoreCase)
            || name.Equals("DecisionCommentGMOnly", StringComparison.OrdinalIgnoreCase);
    }

    private static object ViewerBsonToObject(BsonValue value, bool admin)
    {
        if (value == null || value == BsonNull.Value) return string.Empty;
        if (value.IsString) return value.AsString;
        if (value.IsBoolean) return value.AsBoolean;
        if (value.IsInt32) return value.AsInt32;
        if (value.IsInt64) return value.AsInt64;
        if (value.IsDouble) return value.AsDouble;
        if (value.IsValidDateTime) return value.ToUniversalTime();
        if (value.IsObjectId) return value.AsObjectId.ToString();
        if (value.IsBsonArray) return value.AsBsonArray.Select(x => ViewerBsonToObject(x, admin)).Cast<object>().ToArray();
        if (value.IsBsonDocument)
        {
            var doc = value.AsBsonDocument;
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (var element in doc.Elements)
            {
                if (!admin && IsViewerGmOnlyField(element.Name)) continue;
                if (!admin && element.Name.Equals("ServerOnlyData", StringComparison.OrdinalIgnoreCase)) continue;
                result[element.Name] = ViewerBsonToObject(element.Value, admin);
            }
            return result;
        }

        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private void PaintViewerRect(Dictionary<string, object> cells, int x, int y, int width, int height, Dictionary<string, object> value)
    {
        for (var yy = y; yy < y + height; yy++)
        {
            for (var xx = x; xx < x + width; xx++)
            {
                cells[CellKey(xx, yy)] = new Dictionary<string, object>(value, StringComparer.OrdinalIgnoreCase);
            }
        }
    }

    private static BsonDocument RectGeometry(int x, int y, int width, int height)
    {
        return new BsonDocument
        {
            ["type"] = "polygon",
            ["points"] = new BsonArray
            {
                PointDoc(x, y),
                PointDoc(x + width, y),
                PointDoc(x + width, y + height),
                PointDoc(x, y + height)
            }
        };
    }

    private static BsonDocument PointDoc(double x, double y) => new BsonDocument { ["x"] = x, ["y"] = y };

    private BsonDocument? BuildViewerRegionGeometry(WorldMapState map, IDictionary<string, object> payload, out string error)
    {
        error = string.Empty;
        var x = PayloadReader.GetInt(payload, "x") ?? PayloadReader.GetInt(payload, "cellX") ?? -1;
        var y = PayloadReader.GetInt(payload, "y") ?? PayloadReader.GetInt(payload, "cellY") ?? -1;
        var width = PayloadReader.GetInt(payload, "widthCells") ?? PayloadReader.GetInt(payload, "width") ?? 1;
        var height = PayloadReader.GetInt(payload, "heightCells") ?? PayloadReader.GetInt(payload, "height") ?? 1;
        if (x < 0 || y < 0 || width <= 0 || height <= 0)
        {
            error = "region geometry must have positive rectangle bounds inside map";
            return null;
        }
        if (x >= map.WidthCells || y >= map.HeightCells || x + width > map.WidthCells || y + height > map.HeightCells)
        {
            error = "region geometry is outside map bounds";
            return null;
        }
        if (width * height > 12000)
        {
            error = "region geometry is too large for one operation";
            return null;
        }

        return RectGeometry(x, y, width, height);
    }

    private bool ValidateWorldMapViewerCoordinates(WorldMapState map, int? cellX, int? cellY, double? xNormalized, double? yNormalized, out string error)
    {
        if (cellX.HasValue || cellY.HasValue)
        {
            if (!cellX.HasValue || !cellY.HasValue)
            {
                error = "cellX and cellY must be provided together";
                return false;
            }

            if (!MapRuntimeValidation.IsWorldCellInsideBounds(cellX.Value, cellY.Value, map.WidthCells, map.HeightCells))
            {
                error = "coordinates are outside world map bounds";
                return false;
            }
        }

        if (xNormalized.HasValue || yNormalized.HasValue)
        {
            if (!xNormalized.HasValue || !yNormalized.HasValue)
            {
                error = "xNormalized and yNormalized must be provided together";
                return false;
            }

            if (!MapRuntimeValidation.IsNormalizedCoordinate(xNormalized.Value) || !MapRuntimeValidation.IsNormalizedCoordinate(yNormalized.Value))
            {
                error = "normalized coordinates must be between 0 and 1";
                return false;
            }
        }

        if (!cellX.HasValue && !cellY.HasValue && !xNormalized.HasValue && !yNormalized.HasValue)
        {
            error = "coordinates are required";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static string NormalizeWorldMapViewerVisibility(string? value, out string error)
    {
        error = string.Empty;
        var raw = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(raw)) return MapVisibilityModes.Public;
        if (raw == MapVisibilityModes.Public || raw == MapVisibilityModes.Party || raw == MapVisibilityModes.GmOnly || raw == MapVisibilityModes.Hidden || raw == "server_only")
            return raw;
        error = "invalid world map visibility mode";
        return string.Empty;
    }

    private static string ViewerCollectionForEntityType(string entityType)
    {
        return entityType switch
        {
            "profile" or "map" or "world_map" => "world_map_profiles",
            "region" or "country" or "continent" or "border" => "world_map_regions",
            "location" or "city" or "capital" or "ruin" or "anomaly" => "world_map_locations",
            "label" => "world_map_labels",
            _ => string.Empty
        };
    }

    private bool IsViewerDocumentVisibleForPlayer(BsonDocument doc)
    {
        if (doc == null) return false;
        if (ViewerBool(doc, "IsArchived", false) || ViewerBool(doc, "Archived", false) || ViewerBool(doc, "Deleted", false)) return false;
        if (!ViewerBool(doc, "IsPlayerVisible", true)) return false;
        var visibility = FirstNonEmptyWorld(ViewerString(doc, "VisibilityMode"), ViewerString(doc, "Visibility")).ToLowerInvariant();
        if (visibility.Contains("gm_only") || visibility.Contains("hidden") || visibility.Contains("server_only") || visibility.Contains("superadmin")) return false;
        if (doc.TryGetValue("ServerOnlyData", out var serverOnly) && serverOnly != BsonNull.Value)
        {
            if (serverOnly.IsBsonDocument && serverOnly.AsBsonDocument.ElementCount > 0) return false;
            if (serverOnly.IsBsonArray && serverOnly.AsBsonArray.Count > 0) return false;
            if (serverOnly.IsString && !string.IsNullOrWhiteSpace(serverOnly.AsString)) return false;
        }
        return true;
    }

    private IMongoCollection<BsonDocument> ViewerCollection(string name)
        => _mongo.Database.GetCollection<BsonDocument>(name);

    private BsonDocument? ExistingViewerDocument(string collection, string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        return ViewerCollection(collection)
            .Find(Builders<BsonDocument>.Filter.Eq("Id", id))
            .FirstOrDefault();
    }

    private void UpsertViewerDocument(string collection, BsonDocument doc)
    {
        var id = ViewerString(doc, "Id");
        if (string.IsNullOrWhiteSpace(id)) throw new InvalidOperationException("world map viewer document id is required");
        ViewerCollection(collection).ReplaceOne(
            Builders<BsonDocument>.Filter.Eq("Id", id),
            doc,
            new ReplaceOptions { IsUpsert = true });
    }

    private long CountViewerDocuments(string collection, string mapId)
        => ViewerCollection(collection).CountDocuments(Builders<BsonDocument>.Filter.Eq("MapId", mapId));

    private static string ViewerString(BsonDocument doc, string field)
    {
        if (doc == null || !doc.TryGetValue(field, out var value) || value == BsonNull.Value) return string.Empty;
        if (value.IsString) return value.AsString;
        if (value.IsObjectId) return value.AsObjectId.ToString();
        if (value.IsValidDateTime) return value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static bool ViewerBool(BsonDocument doc, string field, bool fallback)
    {
        if (doc == null || !doc.TryGetValue(field, out var value) || value == BsonNull.Value) return fallback;
        if (value.IsBoolean) return value.AsBoolean;
        return bool.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out var parsed) ? parsed : fallback;
    }

    private void EnsureWorldMapViewerIndexes()
    {
        foreach (var name in new[] { "world_map_profiles", "world_map_regions", "world_map_locations", "world_map_labels" })
        {
            var collection = ViewerCollection(name);
            collection.Indexes.CreateMany(new[]
            {
                new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("Id"), new CreateIndexOptions { Name = "ix_id" }),
                new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("MapId"), new CreateIndexOptions { Name = "ix_map_id" }),
                new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("CampaignId"), new CreateIndexOptions { Name = "ix_campaign_id" }),
                new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("IsPlayerVisible"), new CreateIndexOptions { Name = "ix_player_visible" }),
                new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("VisibilityMode"), new CreateIndexOptions { Name = "ix_visibility" })
            });
        }
    }

    private void WriteWorldMapViewerJournal(UserAccount actor, string mapId, string eventType, string summary, string playerSummary)
    {
        try
        {
            var entry = new EventJournalEntryState
            {
                CampaignId = WorldMapMvp01455CampaignId,
                SourceModule = "world_map_viewer",
                SourceEventType = eventType,
                SourceEventId = mapId,
                CorrelationId = $"world_map_viewer:{eventType}:{mapId}",
                EntryType = EventJournalEntryTypeIds.Automatic,
                Category = EventJournalCategoryIds.Map,
                Severity = EventJournalSeverityIds.Information,
                Title = "Карта мира",
                Summary = summary,
                PlayerSummary = playerSummary,
                VisibilityMode = EventJournalVisibilityModeIds.PlayerVisible,
                IsPlayerVisible = true,
                IsAutomatic = true,
                ActorUserId = actor.Id,
                ActorDisplayName = actor.Login,
                SubjectEntityType = EventJournalEntityTypeIds.WorldMap,
                SubjectEntityId = mapId,
                SubjectDisplayName = WorldMapMvp01455Name,
                OccurredAtUtc = DateTime.UtcNow,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedByUserId = actor.Id,
                UpdatedAtUtc = DateTime.UtcNow,
                Tags = new List<string> { "0.14.55", "world-map-viewer" }
            };
            entry.SequenceNumber = _repositories.EventJournalEntries.Find(Builders<EventJournalEntryState>.Filter.Eq(x => x.CampaignId, entry.CampaignId)).OrderByDescending(x => x.SequenceNumber).FirstOrDefault()?.SequenceNumber + 1 ?? 1;
            _repositories.EventJournalEntries.Insert(entry);
        }
        catch (Exception ex)
        {
            _logger.Debug($"world.map.viewer.journal.skip reason={ex.GetType().Name}");
        }
    }

    private bool WorldMapViewerAdminEnabled()
        => _featureFlags.IsEnabled(nameof(MapFeatureFlags.UseMapSystemV1))
           && _featureFlags.IsEnabled(nameof(MapFeatureFlags.UseSpaceHierarchyV1))
           && _featureFlags.IsEnabled(nameof(MapFeatureFlags.UseWorldMapV1));

    private bool WorldMapViewerPlayerEnabled()
        => WorldMapViewerAdminEnabled()
           && _featureFlags.IsEnabled(nameof(MapFeatureFlags.UseWorldMapPlayerView));

    private ResponseEnvelope WorldMapViewerDisabled(string commandName)
    {
        _logger.Admin($"world.map.viewer.disabled command={commandName}");
        return Error("World Map viewer is disabled by feature flags.", ResponseStatus.Forbidden, ErrorCode.Forbidden);
    }
}
