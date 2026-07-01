using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;
using Nri.Shared.Utilities;

namespace Nri.Server.Application;

public partial class ServiceHub
{
    public ResponseEnvelope MapWorldList(CommandContext context)
    {
        RequireAdmin(context);
        if (!MapWorldReadEnabled())
            return MapWorldDisabled(context.Request.Command);

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var campaignId = RequireLength(PayloadReader.GetString(payload, "campaignId"), 1, 128, "campaignId");
        var includeArchived = PayloadReader.GetBool(payload, "includeArchived");
        var maps = _repositories.WorldMaps.ListByCampaignAsync(campaignId, includeArchived, 500).GetAwaiter().GetResult();

        var items = maps.Select(map =>
            {
                var markerCount = BuildAdminWorldMarkerPayloads(map, includeHidden: true).Length;
                return WorldMapListItemPayload(map, markerCount);
            })
            .Cast<object>()
            .ToArray();

        return Ok("World maps loaded.", new Dictionary<string, object>
        {
            { "items", items },
            { "count", items.Length }
        });
    }

    public ResponseEnvelope MapWorldCreate(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!MapWorldWriteEnabled())
            return MapWorldDisabled(context.Request.Command);

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var campaignId = RequireLength(PayloadReader.GetString(payload, "campaignId"), 1, 128, "campaignId");
        var ruleSetId = RequireLength(PayloadReader.GetString(payload, "ruleSetId"), 1, 128, "ruleSetId");
        var name = RequireLength(PayloadReader.GetString(payload, "name"), 1, 160, "name");
        var description = RequireLength(PayloadReader.GetString(payload, "description"), 0, 4096, "description");
        var spaceNodeId = RequireLength(PayloadReader.GetString(payload, "spaceNodeId"), 0, 128, "spaceNodeId");
        var widthCells = PayloadReader.GetInt(payload, "widthCells") ?? MapRuntimeValidation.WorldDefaultWidthCells;
        var heightCells = PayloadReader.GetInt(payload, "heightCells") ?? MapRuntimeValidation.WorldDefaultHeightCells;
        var cellSizeKm = PayloadReader.GetDouble(payload, "cellSizeKm");
        var projectionMode = NormalizeWorldProjectionMode(PayloadReader.GetString(payload, "projectionMode"));
        var coordinateMode = NormalizeWorldCoordinateMode(PayloadReader.GetString(payload, "coordinateMode"));
        var visibilityMode = NormalizeVisibilityMode(RequireLength(PayloadReader.GetString(payload, "visibilityMode"), 0, 32, "visibilityMode"));
        var isPlayerVisible = !payload.ContainsKey("isPlayerVisible") || PayloadReader.GetBool(payload, "isPlayerVisible");

        var validation = ValidateWorldMapSettings(widthCells, heightCells, cellSizeKm);
        if (validation != null) return validation;

        var map = new WorldMapState
        {
            CampaignId = campaignId,
            RuleSetId = ruleSetId,
            SpaceNodeId = spaceNodeId,
            Name = name,
            Description = description,
            WidthCells = widthCells,
            HeightCells = heightCells,
            CellSizeKm = cellSizeKm,
            ProjectionMode = projectionMode,
            CoordinateMode = coordinateMode,
            VisibilityMode = string.IsNullOrWhiteSpace(visibilityMode) ? MapVisibilityModes.Party : visibilityMode,
            IsPlayerVisible = isPlayerVisible,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = actor.Id,
            UpdatedByUserId = actor.Id
        };

        _logger.Admin($"map.world.create.start campaignId={campaignId} ruleSetId={ruleSetId} width={widthCells} height={heightCells}");
        var saved = _repositories.WorldMaps.UpsertAsync(map).GetAwaiter().GetResult();
        _logger.Admin($"map.world.create.done mapId={saved.Id}");

        return Ok("World map created.", new Dictionary<string, object>
        {
            { "mapId", saved.Id },
            { "map", WorldMapPayload(saved) }
        });
    }

    public ResponseEnvelope MapWorldGet(CommandContext context)
    {
        RequireAdmin(context);
        if (!MapWorldReadEnabled())
            return MapWorldDisabled(context.Request.Command);

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var mapId = RequireLength(PayloadReader.GetString(payload, "mapId"), 1, 128, "mapId");
        var includeLayers = !payload.ContainsKey("includeLayers") || PayloadReader.GetBool(payload, "includeLayers");
        var includeMarkers = !payload.ContainsKey("includeMarkers") || PayloadReader.GetBool(payload, "includeMarkers");
        var map = _repositories.WorldMaps.GetByIdAsync(mapId).GetAwaiter().GetResult();
        if (map == null || map.Deleted || map.Archived || map.IsArchived)
            return Error("world map not found", ResponseStatus.NotFound, ErrorCode.NotFound);

        var response = new Dictionary<string, object>
        {
            { "map", WorldMapPayload(map) }
        };

        if (includeLayers)
        {
            var layers = _repositories.WorldMapLayers.ListByMapAsync(mapId, includeArchived: false, limit: 5000).GetAwaiter().GetResult();
            response["layers"] = layers.Select(WorldLayerPayload).Cast<object>().ToArray();
            response["layerCount"] = layers.Count;
        }

        if (includeMarkers)
        {
            var markers = BuildAdminWorldMarkerPayloads(map, includeHidden: true);
            response["markers"] = markers;
            response["markerCount"] = markers.Length;
        }

        response["profile"] = ViewerFirstPayload("world_map_profiles", map.Id, admin: true, includeHidden: true);
        response["regions"] = ViewerDocumentsPayload("world_map_regions", map.Id, admin: true, includeHidden: true);
        response["locations"] = ViewerDocumentsPayload("world_map_locations", map.Id, admin: true, includeHidden: true);
        response["labels"] = ViewerDocumentsPayload("world_map_labels", map.Id, admin: true, includeHidden: true);
        response["legends"] = BuildWorldLegendsPayload();
        _logger.Admin($"map.world.get mapId={mapId}");
        return Ok("World map loaded.", response);
    }

    public ResponseEnvelope MapWorldUpdateSettings(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!MapWorldWriteEnabled())
            return MapWorldDisabled(context.Request.Command);

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var mapId = RequireLength(PayloadReader.GetString(payload, "mapId"), 1, 128, "mapId");
        var map = _repositories.WorldMaps.GetByIdAsync(mapId).GetAwaiter().GetResult();
        if (map == null || map.Deleted || map.Archived || map.IsArchived)
            return Error("world map not found", ResponseStatus.NotFound, ErrorCode.NotFound);

        if (payload.ContainsKey("name"))
            map.Name = RequireLength(PayloadReader.GetString(payload, "name"), 1, 160, "name");
        if (payload.ContainsKey("description"))
            map.Description = RequireLength(PayloadReader.GetString(payload, "description"), 0, 4096, "description");
        if (payload.ContainsKey("cellSizeKm"))
            map.CellSizeKm = PayloadReader.GetDouble(payload, "cellSizeKm");
        if (payload.ContainsKey("visibilityMode"))
            map.VisibilityMode = NormalizeVisibilityMode(RequireLength(PayloadReader.GetString(payload, "visibilityMode"), 0, 32, "visibilityMode"));
        if (payload.ContainsKey("isPlayerVisible"))
            map.IsPlayerVisible = PayloadReader.GetBool(payload, "isPlayerVisible");
        if (payload.ContainsKey("projectionMode"))
            map.ProjectionMode = NormalizeWorldProjectionMode(PayloadReader.GetString(payload, "projectionMode"));
        if (payload.ContainsKey("coordinateMode"))
            map.CoordinateMode = NormalizeWorldCoordinateMode(PayloadReader.GetString(payload, "coordinateMode"));

        map.UpdatedAtUtc = DateTime.UtcNow;
        map.UpdatedByUserId = actor.Id;

        var validation = ValidateWorldMapSettings(map.WidthCells, map.HeightCells, map.CellSizeKm);
        if (validation != null) return validation;

        var saved = _repositories.WorldMaps.UpsertAsync(map).GetAwaiter().GetResult();
        _logger.Admin($"map.world.updateSettings mapId={mapId}");
        return Ok("World map settings updated.", new Dictionary<string, object>
        {
            { "mapId", saved.Id },
            { "map", WorldMapPayload(saved) }
        });
    }

    public ResponseEnvelope MapWorldArchive(CommandContext context)
    {
        RequireAdmin(context);
        if (!MapWorldWriteEnabled())
            return MapWorldDisabled(context.Request.Command);

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var mapId = RequireLength(PayloadReader.GetString(payload, "mapId"), 1, 128, "mapId");
        var archived = _repositories.WorldMaps.ArchiveAsync(mapId).GetAwaiter().GetResult();
        if (!archived) return Error("world map not found", ResponseStatus.NotFound, ErrorCode.NotFound);
        _logger.Admin($"map.world.archive mapId={mapId}");
        return Ok("World map archived.", new Dictionary<string, object> { { "mapId", mapId } });
    }

    public ResponseEnvelope MapWorldLayerGet(CommandContext context)
    {
        RequireAdmin(context);
        if (!MapWorldLayerReadEnabled())
            return MapWorldDisabled(context.Request.Command);

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var mapId = RequireLength(PayloadReader.GetString(payload, "mapId"), 1, 128, "mapId");
        var layerType = NormalizeWorldLayerType(PayloadReader.GetString(payload, "layerType"));
        var layerFlagError = ValidateLayerFlag(layerType);
        if (layerFlagError != null) return layerFlagError;
        var map = _repositories.WorldMaps.GetByIdAsync(mapId).GetAwaiter().GetResult();
        if (map == null || map.Deleted || map.Archived || map.IsArchived)
            return Error("world map not found", ResponseStatus.NotFound, ErrorCode.NotFound);

        var layer = LoadOrCreateWorldLayer(map, layerType, actorUserId: "system");
        return Ok("World map layer loaded.", new Dictionary<string, object>
        {
            { "mapId", mapId },
            { "layer", WorldLayerPayload(layer) },
            { "legend", BuildWorldLegendForLayer(layerType) }
        });
    }

    public ResponseEnvelope MapWorldLayerPaint(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!MapWorldLayerWriteEnabled())
            return MapWorldDisabled(context.Request.Command);

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var mapId = RequireLength(PayloadReader.GetString(payload, "mapId"), 1, 128, "mapId");
        var layerType = NormalizeWorldLayerType(PayloadReader.GetString(payload, "layerType"));
        var layerFlagError = ValidateLayerFlag(layerType);
        if (layerFlagError != null) return layerFlagError;
        var map = _repositories.WorldMaps.GetByIdAsync(mapId).GetAwaiter().GetResult();
        if (map == null || map.Deleted || map.Archived || map.IsArchived)
            return Error("world map not found", ResponseStatus.NotFound, ErrorCode.NotFound);

        var brushShape = NormalizeWorldBrushShape(PayloadReader.GetString(payload, "brushShape"));
        var brushMode = NormalizeWorldBrushMode(PayloadReader.GetString(payload, "brushMode"));
        var x = PayloadReader.GetInt(payload, "x") ?? -1;
        var y = PayloadReader.GetInt(payload, "y") ?? -1;
        var width = PayloadReader.GetInt(payload, "widthCells") ?? 1;
        var height = PayloadReader.GetInt(payload, "heightCells") ?? 1;
        var radius = PayloadReader.GetInt(payload, "radiusCells") ?? 1;
        var label = RequireLength(PayloadReader.GetString(payload, "label"), 0, 200, "label");
        var value = PayloadReader.GetString(payload, "value");

        var cells = BuildBrushCells(map, brushShape, x, y, width, height, radius);
        if (cells.Count == 0)
            return Error("brush area is outside map bounds", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);
        if (cells.Count > 12000)
            return Error("brush area is too large for one operation", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);

        var layer = LoadOrCreateWorldLayer(map, layerType, actor.Id);
        var layerCells = GetWorldLayerCells(layer);

        _logger.Admin($"map.world.layer.paint.start mapId={mapId} layer={layerType} cells={cells.Count}");
        foreach (var (cellX, cellY) in cells)
        {
            var key = CellKey(cellX, cellY);
            if (string.Equals(brushMode, "clear", StringComparison.OrdinalIgnoreCase))
            {
                layerCells.Remove(key);
                continue;
            }

            var cellValue = BuildLayerCellValue(layerType, value, label);
            if (cellValue == null)
                return Error("invalid value for selected world map layer", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);
            layerCells[key] = cellValue;
        }

        layer.Data["cells"] = layerCells;
        layer.UpdatedByUserId = actor.Id;
        layer.UpdatedAtUtc = DateTime.UtcNow;
        var saved = _repositories.WorldMapLayers.UpsertAsync(layer).GetAwaiter().GetResult();
        _logger.Admin($"map.world.layer.paint.done mapId={mapId} layer={layerType} cells={cells.Count}");

        return Ok("World map layer updated.", new Dictionary<string, object>
        {
            { "mapId", mapId },
            { "layer", WorldLayerPayload(saved) },
            { "updatedCells", cells.Count }
        });
    }

    public ResponseEnvelope MapWorldLayerUpdateCell(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!MapWorldLayerWriteEnabled())
            return MapWorldDisabled(context.Request.Command);

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var mapId = RequireLength(PayloadReader.GetString(payload, "mapId"), 1, 128, "mapId");
        var layerType = NormalizeWorldLayerType(PayloadReader.GetString(payload, "layerType"));
        var layerFlagError = ValidateLayerFlag(layerType);
        if (layerFlagError != null) return layerFlagError;
        var cellX = PayloadReader.GetInt(payload, "cellX") ?? -1;
        var cellY = PayloadReader.GetInt(payload, "cellY") ?? -1;
        var label = RequireLength(PayloadReader.GetString(payload, "label"), 0, 200, "label");
        var value = PayloadReader.GetString(payload, "value");
        var map = _repositories.WorldMaps.GetByIdAsync(mapId).GetAwaiter().GetResult();
        if (map == null || map.Deleted || map.Archived || map.IsArchived)
            return Error("world map not found", ResponseStatus.NotFound, ErrorCode.NotFound);
        if (!MapRuntimeValidation.IsWorldCellInsideBounds(cellX, cellY, map.WidthCells, map.HeightCells))
            return Error("cell coordinates are outside map bounds", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);

        var cellValue = BuildLayerCellValue(layerType, value, label);
        if (cellValue == null)
            return Error("invalid value for selected world map layer", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);

        var layer = LoadOrCreateWorldLayer(map, layerType, actor.Id);
        var layerCells = GetWorldLayerCells(layer);
        layerCells[CellKey(cellX, cellY)] = cellValue;
        layer.Data["cells"] = layerCells;
        layer.UpdatedByUserId = actor.Id;
        layer.UpdatedAtUtc = DateTime.UtcNow;

        var saved = _repositories.WorldMapLayers.UpsertAsync(layer).GetAwaiter().GetResult();
        return Ok("World map layer cell updated.", new Dictionary<string, object>
        {
            { "mapId", mapId },
            { "layer", WorldLayerPayload(saved) }
        });
    }

    public ResponseEnvelope MapWorldLayerClear(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!MapWorldLayerWriteEnabled())
            return MapWorldDisabled(context.Request.Command);

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var mapId = RequireLength(PayloadReader.GetString(payload, "mapId"), 1, 128, "mapId");
        var layerType = NormalizeWorldLayerType(PayloadReader.GetString(payload, "layerType"));
        var layerFlagError = ValidateLayerFlag(layerType);
        if (layerFlagError != null) return layerFlagError;

        var map = _repositories.WorldMaps.GetByIdAsync(mapId).GetAwaiter().GetResult();
        if (map == null || map.Deleted || map.Archived || map.IsArchived)
            return Error("world map not found", ResponseStatus.NotFound, ErrorCode.NotFound);

        var layer = LoadOrCreateWorldLayer(map, layerType, actor.Id);
        layer.Data["cells"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        layer.UpdatedByUserId = actor.Id;
        layer.UpdatedAtUtc = DateTime.UtcNow;
        var saved = _repositories.WorldMapLayers.UpsertAsync(layer).GetAwaiter().GetResult();
        _logger.Admin($"map.world.layer.clear mapId={mapId} layer={layerType}");
        return Ok("World map layer cleared.", new Dictionary<string, object>
        {
            { "mapId", mapId },
            { "layer", WorldLayerPayload(saved) }
        });
    }

    public ResponseEnvelope MapWorldLayerSetVisibility(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!MapWorldLayerWriteEnabled())
            return MapWorldDisabled(context.Request.Command);

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var mapId = RequireLength(PayloadReader.GetString(payload, "mapId"), 1, 128, "mapId");
        var layerType = NormalizeWorldLayerType(PayloadReader.GetString(payload, "layerType"));
        var layerFlagError = ValidateLayerFlag(layerType);
        if (layerFlagError != null) return layerFlagError;

        var map = _repositories.WorldMaps.GetByIdAsync(mapId).GetAwaiter().GetResult();
        if (map == null || map.Deleted || map.Archived || map.IsArchived)
            return Error("world map not found", ResponseStatus.NotFound, ErrorCode.NotFound);

        var layer = LoadOrCreateWorldLayer(map, layerType, actor.Id);
        if (payload.ContainsKey("isVisibleToGM"))
            layer.IsVisibleToGM = PayloadReader.GetBool(payload, "isVisibleToGM");
        if (payload.ContainsKey("isVisibleToPlayers"))
            layer.IsVisibleToPlayers = PayloadReader.GetBool(payload, "isVisibleToPlayers");
        if (payload.ContainsKey("opacity"))
            layer.Opacity = Math.Max(0.05d, Math.Min(1d, PayloadReader.GetDouble(payload, "opacity") ?? layer.Opacity));
        layer.UpdatedByUserId = actor.Id;
        layer.UpdatedAtUtc = DateTime.UtcNow;
        var saved = _repositories.WorldMapLayers.UpsertAsync(layer).GetAwaiter().GetResult();
        return Ok("World map layer visibility updated.", new Dictionary<string, object>
        {
            { "mapId", mapId },
            { "layer", WorldLayerPayload(saved) }
        });
    }

    public ResponseEnvelope MapWorldMarkerList(CommandContext context)
    {
        RequireAdmin(context);
        if (!MapWorldMarkersReadEnabled())
            return MapWorldDisabled(context.Request.Command);

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var mapId = RequireLength(PayloadReader.GetString(payload, "mapId"), 1, 128, "mapId");
        var map = _repositories.WorldMaps.GetByIdAsync(mapId).GetAwaiter().GetResult();
        if (map == null || map.Deleted || map.Archived || map.IsArchived)
            return Error("world map not found", ResponseStatus.NotFound, ErrorCode.NotFound);

        var items = _repositories.MapMarkers
            .ListByMapAsync(mapId, includeArchived: false, limit: 5000)
            .GetAwaiter()
            .GetResult()
            .Select(WorldMarkerPayload)
            .Cast<object>()
            .ToArray();

        return Ok("World map markers loaded.", new Dictionary<string, object>
        {
            { "mapId", mapId },
            { "items", items },
            { "count", items.Length }
        });
    }

    public ResponseEnvelope MapWorldMarkerAdd(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!MapWorldMarkersWriteEnabled())
            return MapWorldDisabled(context.Request.Command);

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var mapId = RequireLength(PayloadReader.GetString(payload, "mapId"), 1, 128, "mapId");
        var map = _repositories.WorldMaps.GetByIdAsync(mapId).GetAwaiter().GetResult();
        if (map == null || map.Deleted || map.Archived || map.IsArchived)
            return Error("world map not found", ResponseStatus.NotFound, ErrorCode.NotFound);

        var marker = new MapMarkerState
        {
            MapId = mapId,
            CampaignId = map.CampaignId,
            Name = RequireLength(PayloadReader.GetString(payload, "name"), 0, 160, "name"),
            MarkerType = NormalizeWorldMarkerType(PayloadReader.GetString(payload, "markerType")),
            XNormalized = PayloadReader.GetDouble(payload, "xNormalized"),
            YNormalized = PayloadReader.GetDouble(payload, "yNormalized"),
            CellX = PayloadReader.GetInt(payload, "cellX"),
            CellY = PayloadReader.GetInt(payload, "cellY"),
            LinkedEntityType = NormalizeMarkerBindingType(PayloadReader.GetString(payload, "linkedEntityType")),
            LinkedEntityId = RequireLength(PayloadReader.GetString(payload, "linkedEntityId"), 0, 128, "linkedEntityId"),
            LinkedEntityDisplayName = RequireLength(PayloadReader.GetString(payload, "linkedEntityDisplayName"), 0, 160, "linkedEntityDisplayName"),
            LinkedEntityPublicLabel = RequireLength(PayloadReader.GetString(payload, "linkedEntityPublicLabel"), 0, 160, "linkedEntityPublicLabel"),
            LinkedSpaceNodeId = RequireLength(PayloadReader.GetString(payload, "linkedSpaceNodeId"), 0, 128, "linkedSpaceNodeId"),
            LinkedContinentId = RequireLength(PayloadReader.GetString(payload, "linkedContinentId"), 0, 128, "linkedContinentId"),
            LinkedCountryId = RequireLength(PayloadReader.GetString(payload, "linkedCountryId"), 0, 128, "linkedCountryId"),
            LinkedCityStateId = RequireLength(PayloadReader.GetString(payload, "linkedCityStateId"), 0, 128, "linkedCityStateId"),
            LinkedRegionId = RequireLength(PayloadReader.GetString(payload, "linkedRegionId"), 0, 128, "linkedRegionId"),
            LinkedLocationId = RequireLength(PayloadReader.GetString(payload, "linkedLocationId"), 0, 128, "linkedLocationId"),
            LinkedFactionId = RequireLength(PayloadReader.GetString(payload, "linkedFactionId"), 0, 128, "linkedFactionId"),
            LinkedOrganizationId = RequireLength(PayloadReader.GetString(payload, "linkedOrganizationId"), 0, 128, "linkedOrganizationId"),
            IsPlayerVisible = !payload.ContainsKey("isPlayerVisible") || PayloadReader.GetBool(payload, "isPlayerVisible"),
            VisibilityMode = NormalizeVisibilityMode(RequireLength(PayloadReader.GetString(payload, "visibilityMode"), 0, 32, "visibilityMode")),
            PublicNotes = RequireLength(PayloadReader.GetString(payload, "publicNotes"), 0, 4096, "publicNotes"),
            GMNotes = RequireLength(PayloadReader.GetString(payload, "gmNotes"), 0, 4096, "gmNotes"),
            IconKey = RequireLength(PayloadReader.GetString(payload, "iconKey"), 0, 128, "iconKey"),
            ColorKey = RequireLength(PayloadReader.GetString(payload, "colorKey"), 0, 64, "colorKey"),
            CardTitle = RequireLength(PayloadReader.GetString(payload, "cardTitle"), 0, 160, "cardTitle"),
            CardDescription = RequireLength(PayloadReader.GetString(payload, "cardDescription"), 0, 4096, "cardDescription"),
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = actor.Id,
            UpdatedByUserId = actor.Id
        };

        if (string.IsNullOrWhiteSpace(marker.Name)) marker.Name = "Маркер";
        if (!ValidateWorldMarkerPosition(marker, map, out var validationError))
            return Error(validationError, ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);

        var saved = _repositories.MapMarkers.UpsertAsync(marker).GetAwaiter().GetResult();
        UpsertWorldMapLocationFromMarker(map, saved, actor.Id);
        _logger.Admin($"map.world.marker.add mapId={mapId} markerId={saved.Id}");
        return Ok("World map marker added.", new Dictionary<string, object>
        {
            { "mapId", mapId },
            { "markerId", saved.Id },
            { "marker", WorldMarkerPayload(saved) }
        });
    }

    public ResponseEnvelope MapWorldMarkerMove(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!MapWorldMarkersWriteEnabled())
            return MapWorldDisabled(context.Request.Command);

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var markerId = RequireLength(PayloadReader.GetString(payload, "markerId"), 1, 128, "markerId");
        var marker = _repositories.MapMarkers.GetByIdAsync(markerId).GetAwaiter().GetResult();
        if (marker == null || marker.Deleted || marker.Archived)
            return Error("marker not found", ResponseStatus.NotFound, ErrorCode.NotFound);
        var map = _repositories.WorldMaps.GetByIdAsync(marker.MapId).GetAwaiter().GetResult();
        if (map == null || map.Deleted || map.Archived || map.IsArchived)
            return Error("world map not found", ResponseStatus.NotFound, ErrorCode.NotFound);

        if (payload.ContainsKey("xNormalized")) marker.XNormalized = PayloadReader.GetDouble(payload, "xNormalized");
        if (payload.ContainsKey("yNormalized")) marker.YNormalized = PayloadReader.GetDouble(payload, "yNormalized");
        if (payload.ContainsKey("cellX")) marker.CellX = PayloadReader.GetInt(payload, "cellX");
        if (payload.ContainsKey("cellY")) marker.CellY = PayloadReader.GetInt(payload, "cellY");
        marker.UpdatedAtUtc = DateTime.UtcNow;
        marker.UpdatedByUserId = actor.Id;

        if (!ValidateWorldMarkerPosition(marker, map, out var validationError))
            return Error(validationError, ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);

        var saved = _repositories.MapMarkers.UpsertAsync(marker).GetAwaiter().GetResult();
        UpsertWorldMapLocationFromMarker(map, saved, actor.Id);
        _logger.Admin($"map.world.marker.move markerId={saved.Id}");
        return Ok("World map marker moved.", new Dictionary<string, object>
        {
            { "markerId", saved.Id },
            { "marker", WorldMarkerPayload(saved) }
        });
    }

    public ResponseEnvelope MapWorldMarkerUpdate(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!MapWorldMarkersWriteEnabled())
            return MapWorldDisabled(context.Request.Command);

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var markerId = RequireLength(PayloadReader.GetString(payload, "markerId"), 1, 128, "markerId");
        var marker = _repositories.MapMarkers.GetByIdAsync(markerId).GetAwaiter().GetResult();
        if (marker == null || marker.Deleted || marker.Archived)
            return Error("marker not found", ResponseStatus.NotFound, ErrorCode.NotFound);
        var map = _repositories.WorldMaps.GetByIdAsync(marker.MapId).GetAwaiter().GetResult();
        if (map == null || map.Deleted || map.Archived || map.IsArchived)
            return Error("world map not found", ResponseStatus.NotFound, ErrorCode.NotFound);

        if (payload.ContainsKey("name")) marker.Name = RequireLength(PayloadReader.GetString(payload, "name"), 0, 160, "name");
        if (payload.ContainsKey("markerType")) marker.MarkerType = NormalizeWorldMarkerType(PayloadReader.GetString(payload, "markerType"));
        if (payload.ContainsKey("xNormalized")) marker.XNormalized = PayloadReader.GetDouble(payload, "xNormalized");
        if (payload.ContainsKey("yNormalized")) marker.YNormalized = PayloadReader.GetDouble(payload, "yNormalized");
        if (payload.ContainsKey("cellX")) marker.CellX = PayloadReader.GetInt(payload, "cellX");
        if (payload.ContainsKey("cellY")) marker.CellY = PayloadReader.GetInt(payload, "cellY");
        if (payload.ContainsKey("linkedEntityType")) marker.LinkedEntityType = NormalizeMarkerBindingType(PayloadReader.GetString(payload, "linkedEntityType"));
        if (payload.ContainsKey("linkedEntityId")) marker.LinkedEntityId = RequireLength(PayloadReader.GetString(payload, "linkedEntityId"), 0, 128, "linkedEntityId");
        if (payload.ContainsKey("linkedEntityDisplayName")) marker.LinkedEntityDisplayName = RequireLength(PayloadReader.GetString(payload, "linkedEntityDisplayName"), 0, 160, "linkedEntityDisplayName");
        if (payload.ContainsKey("linkedEntityPublicLabel")) marker.LinkedEntityPublicLabel = RequireLength(PayloadReader.GetString(payload, "linkedEntityPublicLabel"), 0, 160, "linkedEntityPublicLabel");
        if (payload.ContainsKey("linkedSpaceNodeId")) marker.LinkedSpaceNodeId = RequireLength(PayloadReader.GetString(payload, "linkedSpaceNodeId"), 0, 128, "linkedSpaceNodeId");
        if (payload.ContainsKey("linkedContinentId")) marker.LinkedContinentId = RequireLength(PayloadReader.GetString(payload, "linkedContinentId"), 0, 128, "linkedContinentId");
        if (payload.ContainsKey("linkedCountryId")) marker.LinkedCountryId = RequireLength(PayloadReader.GetString(payload, "linkedCountryId"), 0, 128, "linkedCountryId");
        if (payload.ContainsKey("linkedCityStateId")) marker.LinkedCityStateId = RequireLength(PayloadReader.GetString(payload, "linkedCityStateId"), 0, 128, "linkedCityStateId");
        if (payload.ContainsKey("linkedRegionId")) marker.LinkedRegionId = RequireLength(PayloadReader.GetString(payload, "linkedRegionId"), 0, 128, "linkedRegionId");
        if (payload.ContainsKey("linkedLocationId")) marker.LinkedLocationId = RequireLength(PayloadReader.GetString(payload, "linkedLocationId"), 0, 128, "linkedLocationId");
        if (payload.ContainsKey("linkedFactionId")) marker.LinkedFactionId = RequireLength(PayloadReader.GetString(payload, "linkedFactionId"), 0, 128, "linkedFactionId");
        if (payload.ContainsKey("linkedOrganizationId")) marker.LinkedOrganizationId = RequireLength(PayloadReader.GetString(payload, "linkedOrganizationId"), 0, 128, "linkedOrganizationId");
        if (payload.ContainsKey("isPlayerVisible")) marker.IsPlayerVisible = PayloadReader.GetBool(payload, "isPlayerVisible");
        if (payload.ContainsKey("visibilityMode")) marker.VisibilityMode = NormalizeVisibilityMode(RequireLength(PayloadReader.GetString(payload, "visibilityMode"), 0, 32, "visibilityMode"));
        if (payload.ContainsKey("publicNotes")) marker.PublicNotes = RequireLength(PayloadReader.GetString(payload, "publicNotes"), 0, 4096, "publicNotes");
        if (payload.ContainsKey("gmNotes")) marker.GMNotes = RequireLength(PayloadReader.GetString(payload, "gmNotes"), 0, 4096, "gmNotes");
        if (payload.ContainsKey("iconKey")) marker.IconKey = RequireLength(PayloadReader.GetString(payload, "iconKey"), 0, 128, "iconKey");
        if (payload.ContainsKey("colorKey")) marker.ColorKey = RequireLength(PayloadReader.GetString(payload, "colorKey"), 0, 64, "colorKey");
        if (payload.ContainsKey("cardTitle")) marker.CardTitle = RequireLength(PayloadReader.GetString(payload, "cardTitle"), 0, 160, "cardTitle");
        if (payload.ContainsKey("cardDescription")) marker.CardDescription = RequireLength(PayloadReader.GetString(payload, "cardDescription"), 0, 4096, "cardDescription");

        if (string.IsNullOrWhiteSpace(marker.Name)) marker.Name = "Маркер";
        marker.UpdatedAtUtc = DateTime.UtcNow;
        marker.UpdatedByUserId = actor.Id;

        if (!ValidateWorldMarkerPosition(marker, map, out var validationError))
            return Error(validationError, ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);

        var saved = _repositories.MapMarkers.UpsertAsync(marker).GetAwaiter().GetResult();
        UpsertWorldMapLocationFromMarker(map, saved, actor.Id);
        _logger.Admin($"map.world.marker.update markerId={saved.Id}");
        return Ok("World map marker updated.", new Dictionary<string, object>
        {
            { "markerId", saved.Id },
            { "marker", WorldMarkerPayload(saved) }
        });
    }

    public ResponseEnvelope MapWorldMarkerRemove(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!MapWorldMarkersWriteEnabled())
            return MapWorldDisabled(context.Request.Command);

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var markerId = RequireLength(PayloadReader.GetString(payload, "markerId"), 1, 128, "markerId");
        var marker = _repositories.MapMarkers.GetByIdAsync(markerId).GetAwaiter().GetResult();
        var archived = _repositories.MapMarkers.ArchiveAsync(markerId).GetAwaiter().GetResult();
        if (!archived) return Error("marker not found", ResponseStatus.NotFound, ErrorCode.NotFound);
        if (marker != null) ArchiveWorldMapLocationForMarker(marker, actor.Id);
        _logger.Admin($"map.world.marker.remove markerId={markerId}");
        return Ok("World map marker removed.", new Dictionary<string, object> { { "markerId", markerId } });
    }

    public ResponseEnvelope MapPlayerWorldList(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!MapWorldPlayerViewEnabled())
        {
            _logger.Debug($"map.player.world.list.disabled user={actor.Login}");
            return Error("world map player view is disabled by feature flags", ResponseStatus.Forbidden, ErrorCode.Forbidden);
        }

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var campaignId = RequireLength(PayloadReader.GetString(payload, "campaignId"), 1, 128, "campaignId");
        _logger.Debug($"map.player.world.list.start user={actor.Login} campaignId={campaignId}");

        var maps = _repositories.WorldMaps.ListByCampaignAsync(campaignId, includeArchived: false, limit: 300).GetAwaiter().GetResult();
        var items = maps
            .Where(IsWorldMapVisibleForPlayer)
            .Select(map => new Dictionary<string, object>
            {
                { "mapId", map.Id },
                { "name", map.Name ?? string.Empty },
                { "description", map.Description ?? string.Empty },
                { "spaceNodeId", map.SpaceNodeId ?? string.Empty },
                { "isPlayerVisible", map.IsPlayerVisible },
                { "updatedAtUtc", map.UpdatedAtUtc == default ? map.UpdatedUtc : map.UpdatedAtUtc }
            })
            .Cast<object>()
            .ToArray();

        _logger.Debug($"map.player.world.list.done user={actor.Login} count={items.Length}");
        return Ok("Player world maps loaded.", new Dictionary<string, object>
        {
            { "items", items },
            { "count", items.Length },
            { "builtAtUtc", DateTime.UtcNow }
        });
    }

    public ResponseEnvelope MapPlayerWorldGet(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!MapWorldPlayerViewEnabled())
        {
            _logger.Debug($"map.player.world.get.disabled user={actor.Login}");
            return Error("world map player view is disabled by feature flags", ResponseStatus.Forbidden, ErrorCode.Forbidden);
        }

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var mapId = RequireLength(PayloadReader.GetString(payload, "mapId"), 1, 128, "mapId");
        var includeLayers = !payload.ContainsKey("includeLayers") || PayloadReader.GetBool(payload, "includeLayers");
        var includeMarkers = !payload.ContainsKey("includeMarkers") || PayloadReader.GetBool(payload, "includeMarkers");
        _logger.Debug($"map.player.world.get.start user={actor.Login} mapId={mapId}");

        var map = _repositories.WorldMaps.GetByIdAsync(mapId).GetAwaiter().GetResult();
        if (map == null || map.Deleted || map.Archived || map.IsArchived)
            return Error("world map not found", ResponseStatus.NotFound, ErrorCode.NotFound);
        if (!IsWorldMapVisibleForPlayer(map))
        {
            _logger.Debug($"map.player.world.get.forbidden user={actor.Login} mapId={mapId}");
            return Error("world map is not visible for player", ResponseStatus.Forbidden, ErrorCode.Forbidden);
        }

        var warnings = new List<string>();
        var worldMap = new Dictionary<string, object>
        {
            { "mapId", map.Id },
            { "name", map.Name ?? string.Empty },
            { "description", map.Description ?? string.Empty },
            { "projectionMode", map.ProjectionMode ?? WorldMapProjectionModeIds.FlatGrid },
            { "coordinateMode", map.CoordinateMode ?? WorldMapCoordinateModeIds.Grid },
            { "widthCells", map.WidthCells },
            { "heightCells", map.HeightCells },
            { "cellSizeKm", map.CellSizeKm ?? 0d },
            { "builtAtUtc", DateTime.UtcNow }
        };

        if (includeLayers)
        {
            if (!MapWorldPlayerLayersEnabled())
            {
                warnings.Add("world map layers are disabled by feature flags");
                worldMap["layers"] = Array.Empty<object>();
                worldMap["legends"] = Array.Empty<object>();
            }
            else
            {
                var layers = _repositories.WorldMapLayers.ListByMapAsync(map.Id, includeArchived: false, limit: 5000).GetAwaiter().GetResult();
                var visibleLayers = layers.Where(IsWorldLayerVisibleForPlayer).OrderBy(x => x.SortOrder).ToArray();
                worldMap["layers"] = visibleLayers.Select(PlayerWorldLayerPayload).Cast<object>().ToArray();
                worldMap["legends"] = visibleLayers
                    .Select(layer => BuildWorldLegendForLayer(layer.LayerType))
                    .Cast<object>()
                    .ToArray();
                _logger.Debug($"map.player.world.projection.layers filtered={visibleLayers.Length} all={layers.Count}");
            }
        }

        if (includeMarkers)
        {
            if (!MapWorldPlayerMarkersEnabled())
            {
                warnings.Add("world map markers are disabled by feature flags");
                worldMap["markers"] = Array.Empty<object>();
            }
            else
            {
                var markers = BuildPlayerWorldMarkerPayloads(map);
                worldMap["markers"] = markers;
                _logger.Debug($"map.player.world.projection.markers filtered={markers.Length}");
            }
        }

        worldMap["regions"] = ViewerDocumentsPayload("world_map_regions", map.Id, admin: false, includeHidden: false);
        worldMap["locations"] = ViewerDocumentsPayload("world_map_locations", map.Id, admin: false, includeHidden: false);
        worldMap["labels"] = ViewerDocumentsPayload("world_map_labels", map.Id, admin: false, includeHidden: false);
        _logger.Debug($"map.player.world.get.done user={actor.Login} mapId={mapId}");
        return Ok("Player world map loaded.", new Dictionary<string, object>
        {
            { "map", worldMap },
            { "warnings", warnings.Cast<object>().ToArray() },
            { "builtAtUtc", DateTime.UtcNow }
        });
    }

    private ResponseEnvelope? ValidateLayerFlag(string layerType)
    {
        if (string.Equals(layerType, WorldMapLayerTypeIds.HeightDepth, StringComparison.OrdinalIgnoreCase) && !MapWorldHeightDepthEnabled())
            return Error("height/depth layer endpoints disabled", ResponseStatus.Forbidden, ErrorCode.Forbidden);
        if (string.Equals(layerType, WorldMapLayerTypeIds.Biome, StringComparison.OrdinalIgnoreCase) && !MapWorldBiomeEnabled())
            return Error("biome layer endpoints disabled", ResponseStatus.Forbidden, ErrorCode.Forbidden);
        if (string.Equals(layerType, WorldMapLayerTypeIds.Political, StringComparison.OrdinalIgnoreCase) && !MapWorldPoliticalEnabled())
            return Error("political layer endpoints disabled", ResponseStatus.Forbidden, ErrorCode.Forbidden);
        return null;
    }

    private ResponseEnvelope? ValidateWorldMapSettings(int widthCells, int heightCells, double? cellSizeKm)
    {
        var errors = MapRuntimeValidation.ValidateWorldDimensions(widthCells, heightCells);
        if (errors.Count > 0)
            return Error(string.Join("; ", errors), ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);
        if (cellSizeKm.HasValue && cellSizeKm.Value <= 0d)
            return Error("cellSizeKm must be > 0", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);
        return null;
    }

    private static string NormalizeWorldProjectionMode(string? value)
    {
        var input = (value ?? string.Empty).Trim().ToLowerInvariant();
        return input switch
        {
            WorldMapProjectionModeIds.FlatGrid => WorldMapProjectionModeIds.FlatGrid,
            WorldMapProjectionModeIds.EquirectangularPlaceholder => WorldMapProjectionModeIds.EquirectangularPlaceholder,
            _ => WorldMapProjectionModeIds.FlatGrid
        };
    }

    private static string NormalizeWorldCoordinateMode(string? value)
    {
        var input = (value ?? string.Empty).Trim().ToLowerInvariant();
        return input switch
        {
            WorldMapCoordinateModeIds.Grid => WorldMapCoordinateModeIds.Grid,
            WorldMapCoordinateModeIds.Normalized => WorldMapCoordinateModeIds.Normalized,
            WorldMapCoordinateModeIds.WorldUnits => WorldMapCoordinateModeIds.WorldUnits,
            _ => WorldMapCoordinateModeIds.Grid
        };
    }

    private static string NormalizeWorldLayerType(string? value)
    {
        var input = (value ?? string.Empty).Trim().ToLowerInvariant();
        return input switch
        {
            WorldMapLayerTypeIds.HeightDepth => WorldMapLayerTypeIds.HeightDepth,
            WorldMapLayerTypeIds.Biome => WorldMapLayerTypeIds.Biome,
            WorldMapLayerTypeIds.Political => WorldMapLayerTypeIds.Political,
            WorldMapLayerTypeIds.Marker => WorldMapLayerTypeIds.Marker,
            WorldMapLayerTypeIds.Annotation => WorldMapLayerTypeIds.Annotation,
            _ => WorldMapLayerTypeIds.Custom
        };
    }

    private static string NormalizeWorldBrushShape(string? value)
    {
        var input = (value ?? string.Empty).Trim().ToLowerInvariant();
        return input switch
        {
            "cell" => "cell",
            "rectangle" => "rectangle",
            "circle" => "circle",
            _ => "cell"
        };
    }

    private static string NormalizeWorldBrushMode(string? value)
    {
        return string.Equals((value ?? string.Empty).Trim(), "clear", StringComparison.OrdinalIgnoreCase)
            ? "clear"
            : "set";
    }

    private static string NormalizeWorldMarkerType(string? value)
    {
        var input = (value ?? string.Empty).Trim().ToLowerInvariant();
        return input switch
        {
            MapMarkerTypeIds.Continent => MapMarkerTypeIds.Continent,
            MapMarkerTypeIds.Country => MapMarkerTypeIds.Country,
            MapMarkerTypeIds.Capital => MapMarkerTypeIds.Capital,
            MapMarkerTypeIds.City => MapMarkerTypeIds.City,
            MapMarkerTypeIds.CityState => MapMarkerTypeIds.CityState,
            MapMarkerTypeIds.Region => MapMarkerTypeIds.Region,
            MapMarkerTypeIds.Location => MapMarkerTypeIds.Location,
            MapMarkerTypeIds.PointOfInterest => MapMarkerTypeIds.PointOfInterest,
            MapMarkerTypeIds.RoutePoint => MapMarkerTypeIds.RoutePoint,
            MapMarkerTypeIds.Port => MapMarkerTypeIds.Port,
            MapMarkerTypeIds.Ruin => MapMarkerTypeIds.Ruin,
            MapMarkerTypeIds.Dungeon => MapMarkerTypeIds.Dungeon,
            MapMarkerTypeIds.FactionBase => MapMarkerTypeIds.FactionBase,
            _ => MapMarkerTypeIds.Custom
        };
    }

    private static string NormalizeMarkerBindingType(string? value)
    {
        var input = (value ?? string.Empty).Trim().ToLowerInvariant();
        return input switch
        {
            "" => string.Empty,
            MapMarkerBindingTypeIds.SpaceNode => MapMarkerBindingTypeIds.SpaceNode,
            MapMarkerBindingTypeIds.Continent => MapMarkerBindingTypeIds.Continent,
            MapMarkerBindingTypeIds.Country => MapMarkerBindingTypeIds.Country,
            MapMarkerBindingTypeIds.CityState => MapMarkerBindingTypeIds.CityState,
            MapMarkerBindingTypeIds.Region => MapMarkerBindingTypeIds.Region,
            MapMarkerBindingTypeIds.Location => MapMarkerBindingTypeIds.Location,
            MapMarkerBindingTypeIds.Faction => MapMarkerBindingTypeIds.Faction,
            MapMarkerBindingTypeIds.Organization => MapMarkerBindingTypeIds.Organization,
            MapMarkerBindingTypeIds.Custom => MapMarkerBindingTypeIds.Custom,
            _ => MapMarkerBindingTypeIds.Custom
        };
    }

    private static string NormalizeHeightDepthValue(string? value)
    {
        var input = (value ?? string.Empty).Trim().ToLowerInvariant();
        return input switch
        {
            WorldMapHeightDepthCategoryIds.DeepOcean => WorldMapHeightDepthCategoryIds.DeepOcean,
            WorldMapHeightDepthCategoryIds.ShallowSea => WorldMapHeightDepthCategoryIds.ShallowSea,
            WorldMapHeightDepthCategoryIds.Coast => WorldMapHeightDepthCategoryIds.Coast,
            WorldMapHeightDepthCategoryIds.Lowland => WorldMapHeightDepthCategoryIds.Lowland,
            WorldMapHeightDepthCategoryIds.Highland => WorldMapHeightDepthCategoryIds.Highland,
            WorldMapHeightDepthCategoryIds.Mountain => WorldMapHeightDepthCategoryIds.Mountain,
            WorldMapHeightDepthCategoryIds.ExtremeMountain => WorldMapHeightDepthCategoryIds.ExtremeMountain,
            _ => WorldMapHeightDepthCategoryIds.Custom
        };
    }

    private static string NormalizeBiomeValue(string? value)
    {
        var input = (value ?? string.Empty).Trim().ToLowerInvariant();
        return input switch
        {
            WorldMapBiomeIds.Ocean => WorldMapBiomeIds.Ocean,
            WorldMapBiomeIds.Coast => WorldMapBiomeIds.Coast,
            WorldMapBiomeIds.TropicalForest => WorldMapBiomeIds.TropicalForest,
            WorldMapBiomeIds.Forest => WorldMapBiomeIds.Forest,
            WorldMapBiomeIds.Plains => WorldMapBiomeIds.Plains,
            WorldMapBiomeIds.Savanna => WorldMapBiomeIds.Savanna,
            WorldMapBiomeIds.Desert => WorldMapBiomeIds.Desert,
            WorldMapBiomeIds.Mountains => WorldMapBiomeIds.Mountains,
            WorldMapBiomeIds.Tundra => WorldMapBiomeIds.Tundra,
            WorldMapBiomeIds.Subarctic => WorldMapBiomeIds.Subarctic,
            WorldMapBiomeIds.Swamp => WorldMapBiomeIds.Swamp,
            WorldMapBiomeIds.Urban => WorldMapBiomeIds.Urban,
            _ => WorldMapBiomeIds.Custom
        };
    }

    private static Dictionary<string, object>? BuildLayerCellValue(string layerType, string? value, string label)
    {
        if (string.Equals(layerType, WorldMapLayerTypeIds.HeightDepth, StringComparison.OrdinalIgnoreCase))
        {
            var category = NormalizeHeightDepthValue(value);
            return new Dictionary<string, object>
            {
                { "category", category },
                { "label", label ?? string.Empty }
            };
        }

        if (string.Equals(layerType, WorldMapLayerTypeIds.Biome, StringComparison.OrdinalIgnoreCase))
        {
            var biome = NormalizeBiomeValue(value);
            return new Dictionary<string, object>
            {
                { "biomeId", biome },
                { "label", label ?? string.Empty }
            };
        }

        if (string.Equals(layerType, WorldMapLayerTypeIds.Political, StringComparison.OrdinalIgnoreCase))
        {
            var owner = (value ?? string.Empty).Trim();
            return new Dictionary<string, object>
            {
                { "owner", owner },
                { "label", label ?? string.Empty }
            };
        }

        return null;
    }

    private static Dictionary<string, object> GetWorldLayerCells(WorldMapLayerState layer)
    {
        if (layer.Data == null)
            layer.Data = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        if (!layer.Data.TryGetValue("cells", out var raw) || raw == null)
        {
            var created = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            layer.Data["cells"] = created;
            return created;
        }

        if (raw is Dictionary<string, object> typed)
            return typed;

        if (raw is IDictionary dictionary)
        {
            var mapped = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry entry in dictionary)
            {
                var key = Convert.ToString(entry.Key);
                if (string.IsNullOrWhiteSpace(key)) continue;
                mapped[key] = entry.Value!;
            }

            layer.Data["cells"] = mapped;
            return mapped;
        }

        var fallback = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        layer.Data["cells"] = fallback;
        return fallback;
    }

    private static string CellKey(int x, int y) => $"{x}:{y}";

    private static List<(int x, int y)> BuildBrushCells(WorldMapState map, string shape, int x, int y, int width, int height, int radius)
    {
        var cells = new List<(int x, int y)>();
        if (shape == "cell")
        {
            if (MapRuntimeValidation.IsWorldCellInsideBounds(x, y, map.WidthCells, map.HeightCells))
                cells.Add((x, y));
            return cells;
        }

        if (shape == "rectangle")
        {
            var w = Math.Max(1, width);
            var h = Math.Max(1, height);
            for (var yy = y; yy < y + h; yy++)
            {
                for (var xx = x; xx < x + w; xx++)
                {
                    if (MapRuntimeValidation.IsWorldCellInsideBounds(xx, yy, map.WidthCells, map.HeightCells))
                        cells.Add((xx, yy));
                }
            }

            return cells;
        }

        var safeRadius = Math.Max(1, radius);
        for (var yy = y - safeRadius; yy <= y + safeRadius; yy++)
        {
            for (var xx = x - safeRadius; xx <= x + safeRadius; xx++)
            {
                var dx = xx - x;
                var dy = yy - y;
                if ((dx * dx) + (dy * dy) > safeRadius * safeRadius) continue;
                if (MapRuntimeValidation.IsWorldCellInsideBounds(xx, yy, map.WidthCells, map.HeightCells))
                    cells.Add((xx, yy));
            }
        }

        return cells;
    }

    private WorldMapLayerState LoadOrCreateWorldLayer(WorldMapState map, string layerType, string actorUserId)
    {
        var existing = _repositories.WorldMapLayers.ListByMapAndTypeAsync(map.Id, layerType, includeArchived: false, limit: 1).GetAwaiter().GetResult().FirstOrDefault();
        if (existing != null)
            return existing;

        return new WorldMapLayerState
        {
            WorldMapId = map.Id,
            CampaignId = map.CampaignId,
            LayerType = layerType,
            Name = DefaultLayerName(layerType),
            IsVisibleToGM = true,
            IsVisibleToPlayers = string.Equals(layerType, WorldMapLayerTypeIds.Marker, StringComparison.OrdinalIgnoreCase),
            SortOrder = DefaultLayerSort(layerType),
            Opacity = 0.9d,
            CellResolution = 1,
            DataEncoding = WorldMapDataEncodingIds.SparseCells,
            Data = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                { "cells", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase) }
            },
            UpdatedAtUtc = DateTime.UtcNow,
            UpdatedByUserId = actorUserId ?? string.Empty
        };
    }

    private static bool ValidateWorldMarkerPosition(MapMarkerState marker, WorldMapState map, out string error)
    {
        if (marker.CellX.HasValue || marker.CellY.HasValue)
        {
            if (!marker.CellX.HasValue || !marker.CellY.HasValue)
            {
                error = "cellX and cellY must be provided together";
                return false;
            }

            if (!MapRuntimeValidation.IsWorldCellInsideBounds(marker.CellX.Value, marker.CellY.Value, map.WidthCells, map.HeightCells))
            {
                error = "marker cell coordinates are outside map bounds";
                return false;
            }
        }

        if (marker.XNormalized.HasValue || marker.YNormalized.HasValue)
        {
            if (!marker.XNormalized.HasValue || !marker.YNormalized.HasValue)
            {
                error = "xNormalized and yNormalized must be provided together";
                return false;
            }

            if (!MapRuntimeValidation.IsNormalizedCoordinate(marker.XNormalized.Value)
                || !MapRuntimeValidation.IsNormalizedCoordinate(marker.YNormalized.Value))
            {
                error = "normalized coordinates must be between 0 and 1";
                return false;
            }
        }

        if (!marker.CellX.HasValue && !marker.YNormalized.HasValue && !marker.XNormalized.HasValue && !marker.CellY.HasValue)
        {
            error = "marker must have cell or normalized coordinates";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static string DefaultLayerName(string layerType)
    {
        if (string.Equals(layerType, WorldMapLayerTypeIds.HeightDepth, StringComparison.OrdinalIgnoreCase)) return "Высота/глубина";
        if (string.Equals(layerType, WorldMapLayerTypeIds.Biome, StringComparison.OrdinalIgnoreCase)) return "Биомы";
        if (string.Equals(layerType, WorldMapLayerTypeIds.Political, StringComparison.OrdinalIgnoreCase)) return "Страны/области";
        return "Слой";
    }

    private static int DefaultLayerSort(string layerType)
    {
        if (string.Equals(layerType, WorldMapLayerTypeIds.HeightDepth, StringComparison.OrdinalIgnoreCase)) return 10;
        if (string.Equals(layerType, WorldMapLayerTypeIds.Biome, StringComparison.OrdinalIgnoreCase)) return 20;
        if (string.Equals(layerType, WorldMapLayerTypeIds.Political, StringComparison.OrdinalIgnoreCase)) return 30;
        if (string.Equals(layerType, WorldMapLayerTypeIds.Marker, StringComparison.OrdinalIgnoreCase)) return 40;
        return 100;
    }

    private static Dictionary<string, object> WorldMapPayload(WorldMapState map)
    {
        return new Dictionary<string, object>
        {
            { "mapId", map.Id },
            { "campaignId", map.CampaignId ?? string.Empty },
            { "ruleSetId", map.RuleSetId ?? string.Empty },
            { "spaceNodeId", map.SpaceNodeId ?? string.Empty },
            { "name", map.Name ?? string.Empty },
            { "description", map.Description ?? string.Empty },
            { "widthCells", map.WidthCells },
            { "heightCells", map.HeightCells },
            { "cellSizeKm", map.CellSizeKm.HasValue ? map.CellSizeKm.Value : 0d },
            { "projectionMode", map.ProjectionMode ?? WorldMapProjectionModeIds.FlatGrid },
            { "coordinateMode", map.CoordinateMode ?? WorldMapCoordinateModeIds.Grid },
            { "visibilityMode", map.VisibilityMode ?? MapVisibilityModes.Party },
            { "isPlayerVisible", map.IsPlayerVisible },
            { "isPlanetaryMap", map.IsPlanetaryMap },
            { "linkedWorldId", map.LinkedWorldId ?? string.Empty },
            { "linkedPlanetId", map.LinkedPlanetId ?? string.Empty },
            { "linkedContinentId", map.LinkedContinentId ?? string.Empty },
            { "updatedAtUtc", map.UpdatedAtUtc == default ? map.UpdatedUtc : map.UpdatedAtUtc }
        };
    }

    private static Dictionary<string, object> WorldMapListItemPayload(WorldMapState map, int markerCount)
    {
        var payload = WorldMapPayload(map);
        payload["markerCount"] = markerCount;
        payload["isArchived"] = map.Archived || map.IsArchived;
        return payload;
    }

    private static Dictionary<string, object> WorldLayerPayload(WorldMapLayerState layer)
    {
        var cells = GetCellsArray(layer);
        return new Dictionary<string, object>
        {
            { "layerId", layer.Id },
            { "worldMapId", layer.WorldMapId ?? string.Empty },
            { "layerType", layer.LayerType ?? string.Empty },
            { "name", layer.Name ?? string.Empty },
            { "isVisibleToGM", layer.IsVisibleToGM },
            { "isVisibleToPlayers", layer.IsVisibleToPlayers },
            { "sortOrder", layer.SortOrder },
            { "opacity", layer.Opacity },
            { "dataEncoding", layer.DataEncoding ?? WorldMapDataEncodingIds.SparseCells },
            { "cellResolution", layer.CellResolution },
            { "cells", cells },
            { "cellsCount", cells.Length },
            { "updatedAtUtc", layer.UpdatedAtUtc == default ? layer.UpdatedUtc : layer.UpdatedAtUtc }
        };
    }

    private static object[] GetCellsArray(WorldMapLayerState layer)
    {
        var cells = GetWorldLayerCells(layer);
        var result = new List<object>(cells.Count);
        foreach (var pair in cells)
        {
            var key = pair.Key ?? string.Empty;
            var split = key.Split(':');
            if (split.Length != 2) continue;
            if (!int.TryParse(split[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var x)) continue;
            if (!int.TryParse(split[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var y)) continue;
            var payload = new Dictionary<string, object>
            {
                { "cellX", x },
                { "cellY", y },
                { "value", pair.Value ?? new Dictionary<string, object>() }
            };
            result.Add(payload);
        }

        return result.ToArray();
    }

    private static Dictionary<string, object> WorldMarkerPayload(MapMarkerState marker)
    {
        return new Dictionary<string, object>
        {
            { "markerId", marker.Id },
            { "mapId", marker.MapId ?? string.Empty },
            { "campaignId", marker.CampaignId ?? string.Empty },
            { "name", marker.Name ?? string.Empty },
            { "markerType", marker.MarkerType ?? MapMarkerTypeIds.Custom },
            { "xNormalized", marker.XNormalized.HasValue ? marker.XNormalized.Value : -1d },
            { "yNormalized", marker.YNormalized.HasValue ? marker.YNormalized.Value : -1d },
            { "cellX", marker.CellX.HasValue ? marker.CellX.Value : -1 },
            { "cellY", marker.CellY.HasValue ? marker.CellY.Value : -1 },
            { "isPlayerVisible", marker.IsPlayerVisible },
            { "visibilityMode", marker.VisibilityMode ?? MapVisibilityModes.Party },
            { "linkedEntityType", marker.LinkedEntityType ?? string.Empty },
            { "linkedEntityId", marker.LinkedEntityId ?? string.Empty },
            { "linkedEntityDisplayName", marker.LinkedEntityDisplayName ?? string.Empty },
            { "linkedEntityPublicLabel", marker.LinkedEntityPublicLabel ?? string.Empty },
            { "linkedSpaceNodeId", marker.LinkedSpaceNodeId ?? string.Empty },
            { "linkedContinentId", marker.LinkedContinentId ?? string.Empty },
            { "linkedCountryId", marker.LinkedCountryId ?? string.Empty },
            { "linkedCityStateId", marker.LinkedCityStateId ?? string.Empty },
            { "linkedRegionId", marker.LinkedRegionId ?? string.Empty },
            { "linkedLocationId", marker.LinkedLocationId ?? string.Empty },
            { "linkedFactionId", marker.LinkedFactionId ?? string.Empty },
            { "linkedOrganizationId", marker.LinkedOrganizationId ?? string.Empty },
            { "publicNotes", marker.PublicNotes ?? string.Empty },
            { "gmNotes", marker.GMNotes ?? string.Empty },
            { "iconKey", marker.IconKey ?? string.Empty },
            { "colorKey", marker.ColorKey ?? string.Empty },
            { "cardTitle", marker.CardTitle ?? string.Empty },
            { "cardDescription", marker.CardDescription ?? string.Empty },
            { "updatedAtUtc", marker.UpdatedAtUtc == default ? marker.UpdatedUtc : marker.UpdatedAtUtc }
        };
    }

    private object[] BuildAdminWorldMarkerPayloads(WorldMapState map, bool includeHidden)
    {
        var result = new List<Dictionary<string, object>>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var markers = _repositories.MapMarkers.ListByMapAsync(map.Id, includeArchived: false, limit: 5000).GetAwaiter().GetResult();
        foreach (var marker in markers)
            AddWorldMarkerPayload(result, seen, WorldMarkerPayload(marker));

        AddWorldViewerDocumentMarkers(result, seen, "world_map_locations", map.Id, admin: true, includeHidden: includeHidden);
        AddWorldViewerDocumentMarkers(result, seen, "world_map_regions", map.Id, admin: true, includeHidden: includeHidden);
        return result.Cast<object>().ToArray();
    }

    private object[] BuildPlayerWorldMarkerPayloads(WorldMapState map)
    {
        var result = new List<Dictionary<string, object>>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var markers = _repositories.MapMarkers.ListByMapAsync(map.Id, includeArchived: false, limit: 5000).GetAwaiter().GetResult();
        var bindings = _repositories.MapMarkerBindings.ListByMapAsync(map.Id, 5000).GetAwaiter().GetResult();
        foreach (var marker in markers.Where(IsWorldMarkerVisibleForPlayer))
        {
            AddWorldMarkerPayload(
                result,
                seen,
                PlayerWorldMarkerPayload(marker, bindings.Where(x => string.Equals(x.MarkerId, marker.Id, StringComparison.OrdinalIgnoreCase)).ToArray()));
        }

        AddWorldViewerDocumentMarkers(result, seen, "world_map_locations", map.Id, admin: false, includeHidden: false);
        AddWorldViewerDocumentMarkers(result, seen, "world_map_regions", map.Id, admin: false, includeHidden: false);
        return result.Cast<object>().ToArray();
    }

    private void AddWorldViewerDocumentMarkers(
        List<Dictionary<string, object>> result,
        HashSet<string> seen,
        string collection,
        string mapId,
        bool admin,
        bool includeHidden)
    {
        foreach (var raw in ViewerDocumentsPayload(collection, mapId, admin, includeHidden))
        {
            if (raw is not IDictionary rawMap) continue;
            var doc = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry entry in rawMap)
            {
                var key = Convert.ToString(entry.Key);
                if (string.IsNullOrWhiteSpace(key)) continue;
                doc[key] = entry.Value!;
            }

            AddWorldMarkerPayload(result, seen, WorldViewerDocumentMarkerPayload(doc, collection, admin));
        }
    }

    private static void AddWorldMarkerPayload(
        List<Dictionary<string, object>> result,
        HashSet<string> seen,
        Dictionary<string, object> payload)
    {
        var key = FirstNonEmptyWorld(
            PayloadText(payload, "name") + "|" + PayloadText(payload, "markerType") + "|" + PayloadText(payload, "cellX") + "|" + PayloadText(payload, "cellY"),
            PayloadText(payload, "markerId"));
        if (seen.Add(key))
            result.Add(payload);
    }

    private static Dictionary<string, object> WorldViewerDocumentMarkerPayload(Dictionary<string, object> doc, string collection, bool admin)
    {
        var isRegion = string.Equals(collection, "world_map_regions", StringComparison.OrdinalIgnoreCase);
        var markerType = NormalizeWorldViewerMarkerType(FirstNonEmptyWorld(
            PayloadText(doc, "markerType"),
            PayloadText(doc, "locationType"),
            PayloadText(doc, "regionType"),
            isRegion ? MapMarkerTypeIds.Region : MapMarkerTypeIds.Location));
        var id = PayloadText(doc, "id");
        var name = FirstNonEmptyWorld(PayloadText(doc, "name"), PayloadText(doc, "displayName"), PayloadText(doc, "label"), PayloadText(doc, "text"), "Маркер");
        var publicDescription = FirstNonEmptyWorld(PayloadText(doc, "publicDescription"), PayloadText(doc, "publicNotes"), PayloadText(doc, "description"));
        var payload = new Dictionary<string, object>
        {
            { "markerId", id },
            { "mapId", PayloadText(doc, "mapId") },
            { "campaignId", PayloadText(doc, "campaignId") },
            { "name", name },
            { "markerType", markerType },
            { "xNormalized", PayloadDouble(doc, "xNormalized", -1d) },
            { "yNormalized", PayloadDouble(doc, "yNormalized", -1d) },
            { "cellX", PayloadInt(doc, "cellX", -1) },
            { "cellY", PayloadInt(doc, "cellY", -1) },
            { "isPlayerVisible", PayloadBool(doc, "isPlayerVisible", true) },
            { "visibilityMode", FirstNonEmptyWorld(PayloadText(doc, "visibilityMode"), MapVisibilityModes.Party) },
            { "linkedEntityType", FirstNonEmptyWorld(PayloadText(doc, "linkedEntityType"), isRegion ? MapMarkerBindingTypeIds.Region : MapMarkerBindingTypeIds.Location) },
            { "linkedEntityId", FirstNonEmptyWorld(PayloadText(doc, "linkedEntityId"), id) },
            { "linkedEntityDisplayName", FirstNonEmptyWorld(PayloadText(doc, "linkedEntityDisplayName"), PayloadText(doc, "displayName"), name) },
            { "linkedEntityPublicLabel", FirstNonEmptyWorld(PayloadText(doc, "linkedEntityPublicLabel"), PayloadText(doc, "publicLabel"), PayloadText(doc, "displayName"), name) },
            { "publicNotes", publicDescription },
            { "iconKey", PayloadText(doc, "iconKey") },
            { "colorKey", PayloadText(doc, "colorKey") },
            { "cardTitle", FirstNonEmptyWorld(PayloadText(doc, "cardTitle"), name) },
            { "cardDescription", publicDescription },
            { "updatedAtUtc", PayloadValue(doc, "updatedAtUtc") ?? DateTime.UtcNow }
        };

        if (admin)
            payload["gmNotes"] = PayloadText(doc, "gmNotes");

        return payload;
    }

    private static string NormalizeWorldViewerMarkerType(string value)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "continent" => MapMarkerTypeIds.Continent,
            "country" => MapMarkerTypeIds.Country,
            "capital" => MapMarkerTypeIds.Capital,
            "city" => MapMarkerTypeIds.City,
            "city_state" => MapMarkerTypeIds.CityState,
            "region" => MapMarkerTypeIds.Region,
            "location" => MapMarkerTypeIds.Location,
            "point_of_interest" => MapMarkerTypeIds.PointOfInterest,
            "port" => MapMarkerTypeIds.Port,
            "ruin" => MapMarkerTypeIds.Ruin,
            "dungeon" => MapMarkerTypeIds.Dungeon,
            "faction_base" => MapMarkerTypeIds.FactionBase,
            "sea" => MapMarkerTypeIds.Region,
            "border" => MapMarkerTypeIds.Region,
            _ => string.IsNullOrWhiteSpace(normalized) ? MapMarkerTypeIds.Custom : normalized
        };
    }

    private static object? PayloadValue(Dictionary<string, object> payload, string key)
    {
        if (payload.TryGetValue(key, out var value)) return value;
        foreach (var pair in payload)
            if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
                return pair.Value;
        return null;
    }

    private static string PayloadText(Dictionary<string, object> payload, string key)
        => Convert.ToString(PayloadValue(payload, key), CultureInfo.InvariantCulture) ?? string.Empty;

    private static int PayloadInt(Dictionary<string, object> payload, string key, int fallback)
    {
        var value = PayloadValue(payload, key);
        if (value is int i) return i;
        if (value is long l && l <= int.MaxValue && l >= int.MinValue) return (int)l;
        return int.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;
    }

    private static double PayloadDouble(Dictionary<string, object> payload, string key, double fallback)
    {
        var value = PayloadValue(payload, key);
        if (value is double d) return d;
        if (value is float f) return f;
        if (value is decimal m) return (double)m;
        return double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;
    }

    private static bool PayloadBool(Dictionary<string, object> payload, string key, bool fallback)
    {
        var value = PayloadValue(payload, key);
        if (value is bool b) return b;
        return bool.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out var parsed)
            ? parsed
            : fallback;
    }

    private static Dictionary<string, object> PlayerWorldLayerPayload(WorldMapLayerState layer)
    {
        var cells = GetWorldLayerCells(layer);
        var cellPayload = new List<object>(cells.Count);
        foreach (var pair in cells)
        {
            var key = pair.Key ?? string.Empty;
            var split = key.Split(':');
            if (split.Length != 2) continue;
            if (!int.TryParse(split[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var x)) continue;
            if (!int.TryParse(split[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var y)) continue;
            var valueMap = pair.Value as IDictionary ?? new Dictionary<string, object>();
            cellPayload.Add(PlayerWorldCellPayload(layer.LayerType, x, y, valueMap));
        }

        return new Dictionary<string, object>
        {
            { "layerType", layer.LayerType ?? WorldMapLayerTypeIds.Custom },
            { "name", layer.Name ?? string.Empty },
            { "isVisibleToPlayers", layer.IsVisibleToPlayers },
            { "opacity", layer.Opacity },
            { "dataEncoding", layer.DataEncoding ?? WorldMapDataEncodingIds.SparseCells },
            { "cells", cellPayload.ToArray() }
        };
    }

    private static Dictionary<string, object> PlayerWorldCellPayload(string layerType, int cellX, int cellY, IDictionary valueMap)
    {
        var result = new Dictionary<string, object>
        {
            { "cellX", cellX },
            { "cellY", cellY }
        };

        var label = Convert.ToString(valueMap["label"]) ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(label))
            result["label"] = label;

        if (string.Equals(layerType, WorldMapLayerTypeIds.HeightDepth, StringComparison.OrdinalIgnoreCase))
        {
            result["value"] = Convert.ToString(valueMap["category"]) ?? WorldMapHeightDepthCategoryIds.Custom;
            return result;
        }

        if (string.Equals(layerType, WorldMapLayerTypeIds.Biome, StringComparison.OrdinalIgnoreCase))
        {
            result["value"] = Convert.ToString(valueMap["biomeId"]) ?? WorldMapBiomeIds.Custom;
            return result;
        }

        if (string.Equals(layerType, WorldMapLayerTypeIds.Political, StringComparison.OrdinalIgnoreCase))
        {
            result["value"] = "political";
            if (string.IsNullOrWhiteSpace(label) && valueMap.Contains("owner"))
            {
                var owner = Convert.ToString(valueMap["owner"]) ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(owner))
                    result["label"] = owner;
            }

            return result;
        }

        result["value"] = "custom";
        return result;
    }

    private static Dictionary<string, object> PlayerWorldMarkerPayload(MapMarkerState marker, IReadOnlyCollection<MapMarkerBindingState> bindings)
    {
        var binding = bindings.FirstOrDefault(IsWorldBindingVisibleForPlayer);
        var bindingType = FirstNonEmptyWorld(
            binding?.BindingType,
            marker.LinkedEntityType);
        var bindingDisplay = FirstNonEmptyWorld(
            binding?.DisplayName,
            marker.LinkedEntityPublicLabel,
            marker.LinkedEntityDisplayName);

        return new Dictionary<string, object>
        {
            { "markerId", marker.Id },
            { "name", marker.Name ?? string.Empty },
            { "markerType", marker.MarkerType ?? MapMarkerTypeIds.Custom },
            { "xNormalized", marker.XNormalized ?? -1d },
            { "yNormalized", marker.YNormalized ?? -1d },
            { "cellX", marker.CellX ?? -1 },
            { "cellY", marker.CellY ?? -1 },
            { "iconKey", marker.IconKey ?? string.Empty },
            { "colorKey", marker.ColorKey ?? string.Empty },
            { "cardTitle", marker.CardTitle ?? string.Empty },
            { "cardDescription", marker.CardDescription ?? string.Empty },
            { "linkedEntityType", bindingType },
            { "linkedEntityDisplayName", bindingDisplay },
            { "isVisible", true }
        };
    }

    private static bool IsWorldMapVisibleForPlayer(WorldMapState map)
    {
        if (!map.IsPlayerVisible) return false;
        var visibility = (map.VisibilityMode ?? string.Empty).Trim();
        if (visibility.Length == 0) return true;
        return !string.Equals(visibility, MapVisibilityModes.GmOnly, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(visibility, MapVisibilityModes.Hidden, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(visibility, "server_only", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsWorldLayerVisibleForPlayer(WorldMapLayerState layer)
    {
        return layer != null
            && !layer.Archived
            && layer.IsVisibleToPlayers;
    }

    private bool IsWorldMarkerVisibleForPlayer(MapMarkerState marker)
    {
        if (marker == null || marker.Archived || marker.Deleted) return false;
        if (!marker.IsPlayerVisible) return false;
        var visibility = (marker.VisibilityMode ?? string.Empty).Trim();
        if (marker.ServerOnlyData != null && marker.ServerOnlyData.Count > 0) return false;
        var markerVisible = visibility.Length == 0
            || (!string.Equals(visibility, MapVisibilityModes.GmOnly, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(visibility, MapVisibilityModes.Hidden, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(visibility, "server_only", StringComparison.OrdinalIgnoreCase));
        return markerVisible && IsLinkedWorldViewerDocumentVisibleForPlayer(marker);
    }

    private bool IsLinkedWorldViewerDocumentVisibleForPlayer(MapMarkerState marker)
    {
        var linkedType = FirstNonEmptyWorld(marker.LinkedEntityType, marker.MarkerType).Trim().ToLowerInvariant();
        var collections = linkedType switch
        {
            "region" or "country" or "continent" or "border" => new[] { "world_map_regions", "world_map_locations" },
            "location" or "city" or "capital" or "ruin" or "anomaly" or "point_of_interest" => new[] { "world_map_locations", "world_map_regions" },
            _ => new[] { "world_map_locations", "world_map_regions" }
        };

        var candidateIds = new[]
            {
                marker.LinkedLocationId,
                marker.LinkedRegionId,
                marker.LinkedEntityId,
                marker.Id
            }
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (candidateIds.Length == 0) return true;

        foreach (var id in candidateIds)
        {
            foreach (var collection in collections)
            {
                var doc = ExistingViewerDocument(collection, id);
                if (doc != null && !IsViewerDocumentVisibleForPlayer(doc))
                    return false;
            }
        }

        return true;
    }

    private static bool IsWorldBindingVisibleForPlayer(MapMarkerBindingState binding)
    {
        if (binding == null) return false;
        var visibility = (binding.Visibility ?? string.Empty).Trim();
        if (visibility.Length == 0) return true;
        return !string.Equals(visibility, MapVisibilityModes.GmOnly, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(visibility, MapVisibilityModes.Hidden, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(visibility, "server_only", StringComparison.OrdinalIgnoreCase);
    }

    private static string FirstNonEmptyWorld(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return string.Empty;
    }

    private static object[] BuildWorldLegendsPayload()
    {
        return new object[]
        {
            BuildWorldLegendForLayer(WorldMapLayerTypeIds.HeightDepth),
            BuildWorldLegendForLayer(WorldMapLayerTypeIds.Biome),
            BuildWorldLegendForLayer(WorldMapLayerTypeIds.Political),
            BuildWorldLegendForLayer(WorldMapLayerTypeIds.Marker)
        };
    }

    private static Dictionary<string, object> BuildWorldLegendForLayer(string layerType)
    {
        var entries = new List<object>();
        if (string.Equals(layerType, WorldMapLayerTypeIds.HeightDepth, StringComparison.OrdinalIgnoreCase))
        {
            entries.Add(LegendEntry(WorldMapHeightDepthCategoryIds.DeepOcean, "Глубокий океан"));
            entries.Add(LegendEntry(WorldMapHeightDepthCategoryIds.ShallowSea, "Мелкое море"));
            entries.Add(LegendEntry(WorldMapHeightDepthCategoryIds.Coast, "Побережье"));
            entries.Add(LegendEntry(WorldMapHeightDepthCategoryIds.Lowland, "Равнина"));
            entries.Add(LegendEntry(WorldMapHeightDepthCategoryIds.Highland, "Возвышенность"));
            entries.Add(LegendEntry(WorldMapHeightDepthCategoryIds.Mountain, "Горы"));
            entries.Add(LegendEntry(WorldMapHeightDepthCategoryIds.ExtremeMountain, "Высокие горы"));
            entries.Add(LegendEntry(WorldMapHeightDepthCategoryIds.Custom, "Другое"));
        }
        else if (string.Equals(layerType, WorldMapLayerTypeIds.Biome, StringComparison.OrdinalIgnoreCase))
        {
            entries.Add(LegendEntry(WorldMapBiomeIds.Ocean, "Океан"));
            entries.Add(LegendEntry(WorldMapBiomeIds.Coast, "Побережье"));
            entries.Add(LegendEntry(WorldMapBiomeIds.TropicalForest, "Тропический лес"));
            entries.Add(LegendEntry(WorldMapBiomeIds.Forest, "Лес"));
            entries.Add(LegendEntry(WorldMapBiomeIds.Plains, "Равнины"));
            entries.Add(LegendEntry(WorldMapBiomeIds.Savanna, "Саванна"));
            entries.Add(LegendEntry(WorldMapBiomeIds.Desert, "Пустыня"));
            entries.Add(LegendEntry(WorldMapBiomeIds.Mountains, "Горы"));
            entries.Add(LegendEntry(WorldMapBiomeIds.Tundra, "Тундра"));
            entries.Add(LegendEntry(WorldMapBiomeIds.Subarctic, "Субарктика"));
            entries.Add(LegendEntry(WorldMapBiomeIds.Swamp, "Болото"));
            entries.Add(LegendEntry(WorldMapBiomeIds.Urban, "Город"));
            entries.Add(LegendEntry(WorldMapBiomeIds.Custom, "Другое"));
        }
        else if (string.Equals(layerType, WorldMapLayerTypeIds.Political, StringComparison.OrdinalIgnoreCase))
        {
            entries.Add(LegendEntry("country", "Страна"));
            entries.Add(LegendEntry("region", "Регион"));
            entries.Add(LegendEntry("faction", "Фракция"));
            entries.Add(LegendEntry("custom", "Своя метка"));
        }
        else if (string.Equals(layerType, WorldMapLayerTypeIds.Marker, StringComparison.OrdinalIgnoreCase))
        {
            entries.Add(LegendEntry(MapMarkerTypeIds.Continent, "Материк"));
            entries.Add(LegendEntry(MapMarkerTypeIds.Country, "Страна"));
            entries.Add(LegendEntry(MapMarkerTypeIds.Capital, "Столица"));
            entries.Add(LegendEntry(MapMarkerTypeIds.City, "Город"));
            entries.Add(LegendEntry(MapMarkerTypeIds.CityState, "Город-государство"));
            entries.Add(LegendEntry(MapMarkerTypeIds.Region, "Регион"));
            entries.Add(LegendEntry(MapMarkerTypeIds.Location, "Локация"));
            entries.Add(LegendEntry(MapMarkerTypeIds.PointOfInterest, "Точка интереса"));
            entries.Add(LegendEntry(MapMarkerTypeIds.Port, "Порт"));
            entries.Add(LegendEntry(MapMarkerTypeIds.Ruin, "Руины"));
            entries.Add(LegendEntry(MapMarkerTypeIds.Dungeon, "Подземелье"));
            entries.Add(LegendEntry(MapMarkerTypeIds.FactionBase, "База фракции"));
            entries.Add(LegendEntry(MapMarkerTypeIds.Custom, "Другое"));
        }

        return new Dictionary<string, object>
        {
            { "layerType", layerType },
            { "entries", entries.ToArray() }
        };
    }

    private static Dictionary<string, object> LegendEntry(string key, string label)
    {
        return new Dictionary<string, object>
        {
            { "key", key },
            { "label", label }
        };
    }

    private ResponseEnvelope MapWorldDisabled(string commandName)
    {
        _logger.Admin($"map.world.disabled command={commandName}");
        return Error("world map endpoints disabled by feature flags", ResponseStatus.Forbidden, ErrorCode.Forbidden);
    }

    private bool MapWorldReadEnabled()
    {
        return _featureFlags.IsEnabled(nameof(MapFeatureFlags.UseMapSystemV1))
            && _featureFlags.IsEnabled(nameof(MapFeatureFlags.UseSpaceHierarchyV1))
            && _featureFlags.IsEnabled(nameof(MapFeatureFlags.UseWorldMapV1));
    }

    private bool MapWorldWriteEnabled() => MapWorldReadEnabled();

    private bool MapWorldPainterEnabled()
    {
        return MapWorldReadEnabled()
            && _featureFlags.IsEnabled(nameof(MapFeatureFlags.UseWorldMapPainterMvp))
            && _featureFlags.IsEnabled(nameof(MapFeatureFlags.UseWorldMapLayers));
    }

    private bool MapWorldLayerReadEnabled() => MapWorldPainterEnabled();
    private bool MapWorldLayerWriteEnabled() => MapWorldPainterEnabled();

    private bool MapWorldHeightDepthEnabled()
        => _featureFlags.IsEnabled(nameof(MapFeatureFlags.UseWorldMapHeightDepthLayer));

    private bool MapWorldBiomeEnabled()
        => _featureFlags.IsEnabled(nameof(MapFeatureFlags.UseWorldMapBiomeLayer));

    private bool MapWorldPoliticalEnabled()
        => _featureFlags.IsEnabled(nameof(MapFeatureFlags.UseWorldMapPoliticalLayer));

    private bool MapWorldMarkersEnabled()
    {
        return MapWorldReadEnabled()
            && _featureFlags.IsEnabled(nameof(MapFeatureFlags.UseWorldMapMarkers));
    }

    private bool MapWorldMarkersReadEnabled() => MapWorldMarkersEnabled();
    private bool MapWorldMarkersWriteEnabled() => MapWorldMarkersEnabled();

    private bool MapWorldPlayerViewEnabled()
    {
        return MapWorldReadEnabled()
            && _featureFlags.IsEnabled(nameof(MapFeatureFlags.UseWorldMapPlayerView));
    }

    private bool MapWorldPlayerLayersEnabled()
    {
        return MapWorldPlayerViewEnabled()
            && _featureFlags.IsEnabled(nameof(MapFeatureFlags.UseWorldMapLayers));
    }

    private bool MapWorldPlayerMarkersEnabled()
    {
        return MapWorldPlayerViewEnabled()
            && _featureFlags.IsEnabled(nameof(MapFeatureFlags.UseWorldMapMarkers));
    }
}
