using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;
using Nri.Shared.Utilities;

namespace Nri.Server.Application;

public partial class ServiceHub
{
    public ResponseEnvelope MapSceneList(CommandContext context)
    {
        RequireAdmin(context);
        if (!MapSceneReadEnabled())
        {
            _logger.Admin($"map.command.disabled command={context.Request.Command}");
            return Error("scene map endpoints disabled", ResponseStatus.Forbidden, ErrorCode.Forbidden);
        }

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var campaignId = RequireLength(PayloadReader.GetString(payload, "campaignId"), 1, 128, "campaignId");
        var sessionId = RequireLength(PayloadReader.GetString(payload, "sessionId"), 0, 128, "sessionId");
        var activeGroupId = RequireLength(PayloadReader.GetString(payload, "activeGroupId"), 0, 128, "activeGroupId");
        var sceneId = RequireLength(PayloadReader.GetString(payload, "sceneId"), 0, 128, "sceneId");
        var includeArchived = PayloadReader.GetBool(payload, "includeArchived");
        var maps = _repositories.MapCanvases
            .ListByCampaignAsync(campaignId, includeArchived, 500)
            .GetAwaiter()
            .GetResult()
            .Where(map => string.Equals(map.MapType, MapTypeIds.Scene, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var activeLink = MapSceneSessionLinkEnabled()
            ? ResolveActiveSceneLink(campaignId, sessionId, activeGroupId, sceneId)
            : null;
        var activeMapId = activeLink?.MapId ?? string.Empty;
        var items = maps
            .Select(map =>
            {
                var markerCount = _repositories.MapMarkers.ListByMapAsync(map.Id, includeArchived: false, limit: 5000).GetAwaiter().GetResult().Count;
                var fog = _repositories.MapFogLayers.GetByMapIdAsync(map.Id).GetAwaiter().GetResult();
                return MapSceneListItemPayload(map, markerCount, fog != null, string.Equals(map.Id, activeMapId, StringComparison.OrdinalIgnoreCase));
            })
            .Cast<object>()
            .ToArray();

        return Ok("Scene maps loaded.", new Dictionary<string, object>
        {
            { "items", items },
            { "count", items.Length },
            { "activeMapId", activeMapId },
            { "hasActiveMap", !string.IsNullOrWhiteSpace(activeMapId) }
        });
    }

    public ResponseEnvelope MapSceneCreate(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!MapSceneWriteEnabled())
        {
            _logger.Admin($"map.command.disabled command={context.Request.Command}");
            return Error("scene map endpoints disabled", ResponseStatus.Forbidden, ErrorCode.Forbidden);
        }

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var campaignId = RequireLength(PayloadReader.GetString(payload, "campaignId"), 1, 128, "campaignId");
        var ruleSetId = RequireLength(PayloadReader.GetString(payload, "ruleSetId"), 1, 128, "ruleSetId");
        var name = RequireLength(PayloadReader.GetString(payload, "name"), 1, 160, "name");
        var description = RequireLength(PayloadReader.GetString(payload, "description"), 0, 4096, "description");
        var spaceNodeId = RequireLength(PayloadReader.GetString(payload, "spaceNodeId"), 0, 128, "spaceNodeId");
        var widthMeters = PayloadReader.GetInt(payload, "widthMeters") ?? MapRuntimeValidation.SceneDefaultSizeMeters;
        var heightMeters = PayloadReader.GetInt(payload, "heightMeters") ?? MapRuntimeValidation.SceneDefaultSizeMeters;
        var gridCellSizeMeters = PayloadReader.GetInt(payload, "gridCellSizeMeters") ?? 25;
        var showGrid = !payload.ContainsKey("showGrid") || PayloadReader.GetBool(payload, "showGrid");
        var showCoordinates = !payload.ContainsKey("showCoordinates") || PayloadReader.GetBool(payload, "showCoordinates");

        var validation = ValidateSceneSettings(widthMeters, heightMeters, gridCellSizeMeters);
        if (validation != null) return validation;

        var map = new MapCanvasState
        {
            CampaignId = campaignId,
            RuleSetId = ruleSetId,
            SpaceNodeId = spaceNodeId,
            MapType = MapTypeIds.Scene,
            Name = name,
            Description = description,
            WidthMeters = widthMeters,
            HeightMeters = heightMeters,
            GridCellSizeMeters = gridCellSizeMeters,
            CoordinateMode = MapCoordinateModes.MetersFromOrigin,
            BackgroundMode = MapBackgroundModes.None,
            VisibilityMode = MapVisibilityModes.Party,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            ExtraData = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                { "showGrid", showGrid },
                { "showCoordinates", showCoordinates }
            }
        };

        _logger.Admin($"map.scene.create.start campaignId={campaignId} ruleSetId={ruleSetId}");
        var saved = _repositories.MapCanvases.UpsertAsync(map).GetAwaiter().GetResult();
        _mapIdentityResolver.SynchronizeSceneProjection(saved, saved.Id, actor.Id);
        _logger.Admin($"map.scene.create.done mapId={saved.Id}");

        return Ok("Scene map created.", new Dictionary<string, object>
        {
            { "mapId", saved.Id },
            { "map", MapScenePayload(saved) }
        });
    }

    public ResponseEnvelope MapSceneGet(CommandContext context)
    {
        RequireAdmin(context);
        if (!MapSceneReadEnabled())
        {
            _logger.Admin($"map.command.disabled command={context.Request.Command}");
            return Error("scene map endpoints disabled", ResponseStatus.Forbidden, ErrorCode.Forbidden);
        }

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var mapId = RequireLength(PayloadReader.GetString(payload, "mapId"), 1, 128, "mapId");
        var identity = _mapIdentityResolver.ResolveSceneMap(mapId);
        if (!identity.IsResolved) return MapIdentityError0202(identity);
        var map = identity.CanonicalMap!;
        mapId = identity.CanonicalMapId;

        var markers = _repositories.MapMarkers.ListByMapAsync(mapId, includeArchived: false, limit: 2000).GetAwaiter().GetResult();
        var bindings = _repositories.MapMarkerBindings.ListByMapAsync(mapId, 2000).GetAwaiter().GetResult();
        var fog = _repositories.MapFogLayers.GetByMapIdAsync(mapId).GetAwaiter().GetResult();
        _logger.Admin($"map.scene.get mapId={mapId} markers={markers.Count}");
        return Ok("Scene map loaded.", AdminSceneViewPayload(map, markers, bindings, fog));
    }

    public ResponseEnvelope MapSceneUpdateSettings(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!MapSceneWriteEnabled())
        {
            _logger.Admin($"map.command.disabled command={context.Request.Command}");
            return Error("scene map endpoints disabled", ResponseStatus.Forbidden, ErrorCode.Forbidden);
        }

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var mapId = RequireLength(PayloadReader.GetString(payload, "mapId"), 1, 128, "mapId");
        var identity = _mapIdentityResolver.ResolveSceneMap(mapId);
        if (!identity.IsResolved) return MapIdentityError0202(identity);
        var map = identity.CanonicalMap!;
        mapId = identity.CanonicalMapId;

        if (payload.ContainsKey("name"))
            map.Name = RequireLength(PayloadReader.GetString(payload, "name"), 1, 160, "name");
        if (payload.ContainsKey("description"))
            map.Description = RequireLength(PayloadReader.GetString(payload, "description"), 0, 4096, "description");

        var nextWidth = payload.ContainsKey("widthMeters") ? (PayloadReader.GetInt(payload, "widthMeters") ?? map.WidthMeters) : map.WidthMeters;
        var nextHeight = payload.ContainsKey("heightMeters") ? (PayloadReader.GetInt(payload, "heightMeters") ?? map.HeightMeters) : map.HeightMeters;
        var nextGrid = payload.ContainsKey("gridCellSizeMeters") ? (PayloadReader.GetInt(payload, "gridCellSizeMeters") ?? map.GridCellSizeMeters) : map.GridCellSizeMeters;
        var validation = ValidateSceneSettings(nextWidth, nextHeight, nextGrid);
        if (validation != null) return validation;

        map.WidthMeters = nextWidth;
        map.HeightMeters = nextHeight;
        map.GridCellSizeMeters = nextGrid;
        map.UpdatedAtUtc = DateTime.UtcNow;

        if (map.ExtraData == null)
            map.ExtraData = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        if (payload.ContainsKey("showGrid"))
            map.ExtraData["showGrid"] = PayloadReader.GetBool(payload, "showGrid");
        if (payload.ContainsKey("showCoordinates"))
            map.ExtraData["showCoordinates"] = PayloadReader.GetBool(payload, "showCoordinates");

        var saved = _repositories.MapCanvases.UpsertAsync(map).GetAwaiter().GetResult();
        _mapIdentityResolver.SynchronizeSceneProjection(saved, identity.LegacyMapId, actor.Id, identity.CompatibilityProjection);
        _logger.Admin($"map.scene.updateSettings actor={actor.Login} mapId={mapId}");
        return Ok("Scene map settings updated.", new Dictionary<string, object>
        {
            { "mapId", saved.Id },
            { "map", MapScenePayload(saved) }
        });
    }

    public ResponseEnvelope MapSceneArchive(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!MapSceneWriteEnabled())
        {
            _logger.Admin($"map.command.disabled command={context.Request.Command}");
            return Error("scene map endpoints disabled", ResponseStatus.Forbidden, ErrorCode.Forbidden);
        }

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var mapId = RequireLength(PayloadReader.GetString(payload, "mapId"), 1, 128, "mapId");
        var identity = _mapIdentityResolver.ResolveSceneMap(mapId);
        if (!identity.IsResolved) return MapIdentityError0202(identity);
        var canonical = identity.CanonicalMap!;
        canonical.IsArchived = true;
        canonical.Archived = true;
        canonical.UpdatedAtUtc = DateTime.UtcNow;
        var saved = _repositories.MapCanvases.UpsertAsync(canonical).GetAwaiter().GetResult();
        _mapIdentityResolver.SynchronizeSceneProjection(saved, identity.LegacyMapId, actor.Id, identity.CompatibilityProjection);
        _logger.Admin($"map.scene.archive actor={actor.Login} mapId={identity.CanonicalMapId}");
        return Ok("Scene map archived.", new Dictionary<string, object> { { "mapId", identity.CanonicalMapId } });
    }

    public ResponseEnvelope MapSceneMarkerList(CommandContext context)
    {
        RequireAdmin(context);
        if (!MapSceneReadEnabled() || !MapMarkersEnabled())
        {
            _logger.Admin($"map.command.disabled command={context.Request.Command}");
            return Error("scene map marker endpoints disabled", ResponseStatus.Forbidden, ErrorCode.Forbidden);
        }

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var mapId = RequireLength(PayloadReader.GetString(payload, "mapId"), 1, 128, "mapId");
        var map = _repositories.MapCanvases.GetByIdAsync(mapId).GetAwaiter().GetResult();
        if (map == null || map.Deleted) return Error("scene map not found", ResponseStatus.NotFound, ErrorCode.NotFound);

        var items = _repositories.MapMarkers
            .ListByMapAsync(mapId, includeArchived: false, limit: 5000)
            .GetAwaiter()
            .GetResult()
            .Select(MarkerPayload)
            .Cast<object>()
            .ToArray();

        return Ok("Scene map markers loaded.", new Dictionary<string, object>
        {
            { "mapId", mapId },
            { "items", items },
            { "count", items.Length }
        });
    }

    public ResponseEnvelope MapSceneMarkerAdd(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!MapSceneWriteEnabled() || !MapMarkersEnabled())
        {
            _logger.Admin($"map.command.disabled command={context.Request.Command}");
            return Error("scene map marker endpoints disabled", ResponseStatus.Forbidden, ErrorCode.Forbidden);
        }

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var mapId = RequireLength(PayloadReader.GetString(payload, "mapId"), 1, 128, "mapId");
        var map = _repositories.MapCanvases.GetByIdAsync(mapId).GetAwaiter().GetResult();
        if (map == null || map.Deleted) return Error("scene map not found", ResponseStatus.NotFound, ErrorCode.NotFound);

        var marker = new MapMarkerState
        {
            MapId = mapId,
            CampaignId = map.CampaignId,
            Name = RequireLength(PayloadReader.GetString(payload, "name"), 0, 160, "name"),
            MarkerType = NormalizeMarkerType(PayloadReader.GetString(payload, "markerType")),
            X = PayloadReader.GetDouble(payload, "x") ?? 0d,
            Y = PayloadReader.GetDouble(payload, "y") ?? 0d,
            IconKey = RequireLength(PayloadReader.GetString(payload, "iconKey"), 0, 128, "iconKey"),
            ColorKey = RequireLength(PayloadReader.GetString(payload, "colorKey"), 0, 64, "colorKey"),
            IsPlayerVisible = !payload.ContainsKey("isPlayerVisible") || PayloadReader.GetBool(payload, "isPlayerVisible"),
            LinkedEntityType = RequireLength(PayloadReader.GetString(payload, "linkedEntityType"), 0, 64, "linkedEntityType"),
            LinkedEntityId = RequireLength(PayloadReader.GetString(payload, "linkedEntityId"), 0, 128, "linkedEntityId"),
            CardTitle = RequireLength(PayloadReader.GetString(payload, "cardTitle"), 0, 160, "cardTitle"),
            CardDescription = RequireLength(PayloadReader.GetString(payload, "cardDescription"), 0, 4096, "cardDescription"),
            PublicNotes = RequireLength(PayloadReader.GetString(payload, "publicNotes"), 0, 4096, "publicNotes"),
            GMNotes = RequireLength(PayloadReader.GetString(payload, "gmNotes"), 0, 4096, "gmNotes"),
            VisibilityMode = MapVisibilityModes.Party,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = actor.Id,
            UpdatedByUserId = actor.Id
        };

        if (string.IsNullOrWhiteSpace(marker.Name)) marker.Name = "Маркер";
        if (!MapRuntimeValidation.IsMarkerInsideBounds(marker, map))
            return Error("marker coordinates are outside map bounds", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);

        var saved = _repositories.MapMarkers.UpsertAsync(marker).GetAwaiter().GetResult();
        _logger.Admin($"map.marker.add mapId={mapId} markerId={saved.Id}");
        return Ok("Scene marker added.", new Dictionary<string, object>
        {
            { "mapId", mapId },
            { "markerId", saved.Id },
            { "marker", MarkerPayload(saved) }
        });
    }

    public ResponseEnvelope MapSceneMarkerMove(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!MapSceneWriteEnabled() || !MapMarkersEnabled())
        {
            _logger.Admin($"map.command.disabled command={context.Request.Command}");
            return Error("scene map marker endpoints disabled", ResponseStatus.Forbidden, ErrorCode.Forbidden);
        }

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var markerId = RequireLength(PayloadReader.GetString(payload, "markerId"), 1, 128, "markerId");
        var marker = _repositories.MapMarkers.GetByIdAsync(markerId).GetAwaiter().GetResult();
        if (marker == null || marker.Deleted || marker.Archived)
            return Error("marker not found", ResponseStatus.NotFound, ErrorCode.NotFound);

        var map = _repositories.MapCanvases.GetByIdAsync(marker.MapId).GetAwaiter().GetResult();
        if (map == null || map.Deleted) return Error("scene map not found", ResponseStatus.NotFound, ErrorCode.NotFound);

        marker.X = PayloadReader.GetDouble(payload, "x") ?? marker.X;
        marker.Y = PayloadReader.GetDouble(payload, "y") ?? marker.Y;
        marker.UpdatedAtUtc = DateTime.UtcNow;
        marker.UpdatedByUserId = actor.Id;

        if (!MapRuntimeValidation.IsMarkerInsideBounds(marker, map))
            return Error("marker coordinates are outside map bounds", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);

        var saved = _repositories.MapMarkers.UpsertAsync(marker).GetAwaiter().GetResult();
        _logger.Admin($"map.marker.move markerId={saved.Id} x={saved.X} y={saved.Y}");
        return Ok("Scene marker moved.", new Dictionary<string, object>
        {
            { "markerId", saved.Id },
            { "marker", MarkerPayload(saved) }
        });
    }

    public ResponseEnvelope MapSceneMarkerUpdate(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!MapSceneWriteEnabled() || !MapMarkersEnabled())
        {
            _logger.Admin($"map.command.disabled command={context.Request.Command}");
            return Error("scene map marker endpoints disabled", ResponseStatus.Forbidden, ErrorCode.Forbidden);
        }

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var markerId = RequireLength(PayloadReader.GetString(payload, "markerId"), 1, 128, "markerId");
        var marker = _repositories.MapMarkers.GetByIdAsync(markerId).GetAwaiter().GetResult();
        if (marker == null || marker.Deleted || marker.Archived)
            return Error("marker not found", ResponseStatus.NotFound, ErrorCode.NotFound);

        var map = _repositories.MapCanvases.GetByIdAsync(marker.MapId).GetAwaiter().GetResult();
        if (map == null || map.Deleted) return Error("scene map not found", ResponseStatus.NotFound, ErrorCode.NotFound);

        if (payload.ContainsKey("name")) marker.Name = RequireLength(PayloadReader.GetString(payload, "name"), 0, 160, "name");
        if (payload.ContainsKey("markerType")) marker.MarkerType = NormalizeMarkerType(PayloadReader.GetString(payload, "markerType"));
        if (payload.ContainsKey("x")) marker.X = PayloadReader.GetDouble(payload, "x") ?? marker.X;
        if (payload.ContainsKey("y")) marker.Y = PayloadReader.GetDouble(payload, "y") ?? marker.Y;
        if (payload.ContainsKey("iconKey")) marker.IconKey = RequireLength(PayloadReader.GetString(payload, "iconKey"), 0, 128, "iconKey");
        if (payload.ContainsKey("colorKey")) marker.ColorKey = RequireLength(PayloadReader.GetString(payload, "colorKey"), 0, 64, "colorKey");
        if (payload.ContainsKey("isPlayerVisible")) marker.IsPlayerVisible = PayloadReader.GetBool(payload, "isPlayerVisible");
        if (payload.ContainsKey("linkedEntityType")) marker.LinkedEntityType = RequireLength(PayloadReader.GetString(payload, "linkedEntityType"), 0, 64, "linkedEntityType");
        if (payload.ContainsKey("linkedEntityId")) marker.LinkedEntityId = RequireLength(PayloadReader.GetString(payload, "linkedEntityId"), 0, 128, "linkedEntityId");
        if (payload.ContainsKey("cardTitle")) marker.CardTitle = RequireLength(PayloadReader.GetString(payload, "cardTitle"), 0, 160, "cardTitle");
        if (payload.ContainsKey("cardDescription")) marker.CardDescription = RequireLength(PayloadReader.GetString(payload, "cardDescription"), 0, 4096, "cardDescription");
        if (payload.ContainsKey("publicNotes")) marker.PublicNotes = RequireLength(PayloadReader.GetString(payload, "publicNotes"), 0, 4096, "publicNotes");
        if (payload.ContainsKey("gmNotes")) marker.GMNotes = RequireLength(PayloadReader.GetString(payload, "gmNotes"), 0, 4096, "gmNotes");

        marker.UpdatedAtUtc = DateTime.UtcNow;
        marker.UpdatedByUserId = actor.Id;
        if (string.IsNullOrWhiteSpace(marker.Name)) marker.Name = "Маркер";

        if (!MapRuntimeValidation.IsMarkerInsideBounds(marker, map))
            return Error("marker coordinates are outside map bounds", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);

        var saved = _repositories.MapMarkers.UpsertAsync(marker).GetAwaiter().GetResult();
        _logger.Admin($"map.marker.update markerId={saved.Id}");
        return Ok("Scene marker updated.", new Dictionary<string, object>
        {
            { "markerId", saved.Id },
            { "marker", MarkerPayload(saved) }
        });
    }

    public ResponseEnvelope MapSceneMarkerRemove(CommandContext context)
    {
        RequireAdmin(context);
        if (!MapSceneWriteEnabled() || !MapMarkersEnabled())
        {
            _logger.Admin($"map.command.disabled command={context.Request.Command}");
            return Error("scene map marker endpoints disabled", ResponseStatus.Forbidden, ErrorCode.Forbidden);
        }

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var markerId = RequireLength(PayloadReader.GetString(payload, "markerId"), 1, 128, "markerId");
        var removed = _repositories.MapMarkers.ArchiveAsync(markerId).GetAwaiter().GetResult();
        if (!removed) return Error("marker not found", ResponseStatus.NotFound, ErrorCode.NotFound);
        _logger.Admin($"map.marker.remove markerId={markerId}");
        return Ok("Scene marker removed.", new Dictionary<string, object> { { "markerId", markerId } });
    }

    public ResponseEnvelope MapSceneFogGet(CommandContext context)
    {
        RequireAdmin(context);
        if (!MapSceneFogEnabled())
        {
            _logger.Admin($"map.fog.disabled command={context.Request.Command}");
            return Error("scene map fog endpoints disabled", ResponseStatus.Forbidden, ErrorCode.Forbidden);
        }

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var mapId = RequireLength(PayloadReader.GetString(payload, "mapId"), 1, 128, "mapId");
        var map = _repositories.MapCanvases.GetByIdAsync(mapId).GetAwaiter().GetResult();
        if (map == null || map.Deleted || !string.Equals(map.MapType, MapTypeIds.Scene, StringComparison.OrdinalIgnoreCase))
            return Error("scene map not found", ResponseStatus.NotFound, ErrorCode.NotFound);

        var fog = _repositories.MapFogLayers.GetByMapIdAsync(mapId).GetAwaiter().GetResult();
        _logger.Admin($"map.fog.get mapId={mapId}");
        return Ok("Scene map fog loaded.", new Dictionary<string, object>
        {
            { "mapId", mapId },
            { "fog", FogPayload(fog, map) },
            { "updatedAtUtc", fog?.UpdatedAtUtc ?? DateTime.UtcNow },
            { "revision", fog?.Revision ?? 0L }
        });
    }

    public ResponseEnvelope MapSceneFogSetMode(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!MapSceneFogEnabled())
        {
            _logger.Admin($"map.fog.disabled command={context.Request.Command}");
            return Error("scene map fog endpoints disabled", ResponseStatus.Forbidden, ErrorCode.Forbidden);
        }

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var mapId = RequireLength(PayloadReader.GetString(payload, "mapId"), 1, 128, "mapId");
        var map = _repositories.MapCanvases.GetByIdAsync(mapId).GetAwaiter().GetResult();
        if (map == null || map.Deleted || !string.Equals(map.MapType, MapTypeIds.Scene, StringComparison.OrdinalIgnoreCase))
            return Error("scene map not found", ResponseStatus.NotFound, ErrorCode.NotFound);

        var mode = NormalizeFogMode(PayloadReader.GetString(payload, "mode"));
        var fog = LoadOrCreateFogState(map, actor.Id);
        var cellSize = payload.ContainsKey("cellSizeMeters")
            ? (PayloadReader.GetInt(payload, "cellSizeMeters") ?? fog.CellSizeMeters)
            : fog.CellSizeMeters;
        var cellValidation = ValidateFogCellSize(cellSize);
        if (cellValidation != null) return cellValidation;

        fog.Mode = mode;
        fog.CellSizeMeters = cellSize;
        if (payload.ContainsKey("defaultState"))
            fog.DefaultState = NormalizeFogDefaultState(PayloadReader.GetString(payload, "defaultState"));
        TouchFog(fog, actor.Id);
        var saved = _repositories.MapFogLayers.UpsertAsync(fog).GetAwaiter().GetResult();
        _logger.Admin($"map.fog.setMode mapId={mapId} mode={saved.Mode} defaultState={saved.DefaultState}");
        return Ok("Scene map fog mode updated.", new Dictionary<string, object>
        {
            { "mapId", mapId },
            { "fog", FogPayload(saved, map) },
            { "updatedAtUtc", saved.UpdatedAtUtc },
            { "revision", saved.Revision }
        });
    }

    public ResponseEnvelope MapSceneFogPaint(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!MapSceneFogEnabled())
        {
            _logger.Admin($"map.fog.disabled command={context.Request.Command}");
            return Error("scene map fog endpoints disabled", ResponseStatus.Forbidden, ErrorCode.Forbidden);
        }

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var mapId = RequireLength(PayloadReader.GetString(payload, "mapId"), 1, 128, "mapId");
        var map = _repositories.MapCanvases.GetByIdAsync(mapId).GetAwaiter().GetResult();
        if (map == null || map.Deleted || !string.Equals(map.MapType, MapTypeIds.Scene, StringComparison.OrdinalIgnoreCase))
            return Error("scene map not found", ResponseStatus.NotFound, ErrorCode.NotFound);

        var fog = LoadOrCreateFogState(map, actor.Id);
        if (string.Equals(fog.Mode, FogOfWarModeIds.Disabled, StringComparison.OrdinalIgnoreCase))
            return Error("fog mode is disabled", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);

        var brushMode = NormalizeFogBrushMode(PayloadReader.GetString(payload, "brushMode"));
        var shape = NormalizeFogShape(PayloadReader.GetString(payload, "shape"));
        var centerX = PayloadReader.GetDouble(payload, "centerX") ?? 0d;
        var centerY = PayloadReader.GetDouble(payload, "centerY") ?? 0d;
        var widthMeters = PayloadReader.GetDouble(payload, "widthMeters") ?? Math.Max(fog.CellSizeMeters, 1);
        var heightMeters = PayloadReader.GetDouble(payload, "heightMeters") ?? Math.Max(fog.CellSizeMeters, 1);
        var radiusMeters = PayloadReader.GetDouble(payload, "radiusMeters") ?? Math.Max(fog.CellSizeMeters, 1);

        if (payload.ContainsKey("cellSizeMeters"))
        {
            var forcedCell = PayloadReader.GetInt(payload, "cellSizeMeters") ?? fog.CellSizeMeters;
            var forcedValidation = ValidateFogCellSize(forcedCell);
            if (forcedValidation != null) return forcedValidation;
            fog.CellSizeMeters = forcedCell;
        }

        var range = BuildFogPaintRange(map, fog, shape, centerX, centerY, widthMeters, heightMeters, radiusMeters);
        if (range == null)
            return Error("fog paint area is outside map bounds", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);

        var applyResult = ApplyFogBrush(fog, range, brushMode);
        if (!applyResult.Success)
        {
            _logger.Admin($"map.fog.validation_failed mapId={mapId} reason={applyResult.ErrorMessage}");
            return Error(applyResult.ErrorMessage ?? "fog paint failed", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);
        }

        TouchFog(fog, actor.Id);
        var saved = _repositories.MapFogLayers.UpsertAsync(fog).GetAwaiter().GetResult();
        _logger.Admin($"map.fog.paint.done mapId={mapId} mode={brushMode} shape={shape} revision={saved.Revision}");
        return Ok("Scene map fog updated.", new Dictionary<string, object>
        {
            { "mapId", mapId },
            { "fog", FogPayload(saved, map) },
            { "updatedAtUtc", saved.UpdatedAtUtc },
            { "revision", saved.Revision }
        });
    }

    public ResponseEnvelope MapSceneFogReveal(CommandContext context)
    {
        return MapSceneFogByRectangle(context, FogBrushModeIds.Reveal);
    }

    public ResponseEnvelope MapSceneFogHide(CommandContext context)
    {
        return MapSceneFogByRectangle(context, FogBrushModeIds.Hide);
    }

    public ResponseEnvelope MapSceneFogClear(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!MapSceneFogEnabled())
        {
            _logger.Admin($"map.fog.disabled command={context.Request.Command}");
            return Error("scene map fog endpoints disabled", ResponseStatus.Forbidden, ErrorCode.Forbidden);
        }

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var mapId = RequireLength(PayloadReader.GetString(payload, "mapId"), 1, 128, "mapId");
        var map = _repositories.MapCanvases.GetByIdAsync(mapId).GetAwaiter().GetResult();
        if (map == null || map.Deleted || !string.Equals(map.MapType, MapTypeIds.Scene, StringComparison.OrdinalIgnoreCase))
            return Error("scene map not found", ResponseStatus.NotFound, ErrorCode.NotFound);

        var clearMode = NormalizeFogClearMode(PayloadReader.GetString(payload, "clearMode"));
        var fog = LoadOrCreateFogState(map, actor.Id);
        switch (clearMode)
        {
            case FogClearModeIds.RevealAll:
                fog.DefaultState = FogDefaultStateIds.Revealed;
                fog.HiddenCells.Clear();
                fog.RevealedCells.Clear();
                fog.GMOnlyCells.Clear();
                break;
            case FogClearModeIds.HideAll:
                fog.DefaultState = FogDefaultStateIds.Hidden;
                fog.HiddenCells.Clear();
                fog.RevealedCells.Clear();
                fog.GMOnlyCells.Clear();
                break;
            default:
                fog.HiddenCells.Clear();
                fog.RevealedCells.Clear();
                fog.GMOnlyCells.Clear();
                break;
        }

        TouchFog(fog, actor.Id);
        var saved = _repositories.MapFogLayers.UpsertAsync(fog).GetAwaiter().GetResult();
        _logger.Admin($"map.fog.clear.done mapId={mapId} clearMode={clearMode} revision={saved.Revision}");
        return Ok("Scene map fog cleared.", new Dictionary<string, object>
        {
            { "mapId", mapId },
            { "fog", FogPayload(saved, map) },
            { "updatedAtUtc", saved.UpdatedAtUtc },
            { "revision", saved.Revision }
        });
    }

    public ResponseEnvelope MapSceneFogFill(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!MapSceneFogEnabled())
        {
            _logger.Admin($"map.fog.disabled command={context.Request.Command}");
            return Error("scene map fog endpoints disabled", ResponseStatus.Forbidden, ErrorCode.Forbidden);
        }

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var mapId = RequireLength(PayloadReader.GetString(payload, "mapId"), 1, 128, "mapId");
        var map = _repositories.MapCanvases.GetByIdAsync(mapId).GetAwaiter().GetResult();
        if (map == null || map.Deleted || !string.Equals(map.MapType, MapTypeIds.Scene, StringComparison.OrdinalIgnoreCase))
            return Error("scene map not found", ResponseStatus.NotFound, ErrorCode.NotFound);

        var state = NormalizeFogFillState(PayloadReader.GetString(payload, "state"));
        var fog = LoadOrCreateFogState(map, actor.Id);
        fog.Mode = FogOfWarModeIds.Manual;
        fog.DefaultState = state;
        fog.HiddenCells.Clear();
        fog.RevealedCells.Clear();
        fog.GMOnlyCells.Clear();
        TouchFog(fog, actor.Id);
        var saved = _repositories.MapFogLayers.UpsertAsync(fog).GetAwaiter().GetResult();
        _logger.Admin($"map.fog.fill.done mapId={mapId} state={state} revision={saved.Revision}");
        return Ok("Scene map fog fill applied.", new Dictionary<string, object>
        {
            { "mapId", mapId },
            { "fog", FogPayload(saved, map) },
            { "updatedAtUtc", saved.UpdatedAtUtc },
            { "revision", saved.Revision }
        });
    }

    public ResponseEnvelope MapSceneFogReset(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!MapSceneFogEnabled())
        {
            _logger.Admin($"map.fog.disabled command={context.Request.Command}");
            return Error("scene map fog endpoints disabled", ResponseStatus.Forbidden, ErrorCode.Forbidden);
        }

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var mapId = RequireLength(PayloadReader.GetString(payload, "mapId"), 1, 128, "mapId");
        var map = _repositories.MapCanvases.GetByIdAsync(mapId).GetAwaiter().GetResult();
        if (map == null || map.Deleted || !string.Equals(map.MapType, MapTypeIds.Scene, StringComparison.OrdinalIgnoreCase))
            return Error("scene map not found", ResponseStatus.NotFound, ErrorCode.NotFound);

        var fog = LoadOrCreateFogState(map, actor.Id);
        fog.Mode = FogOfWarModeIds.Manual;
        fog.DefaultState = FogDefaultStateIds.Revealed;
        fog.CellSizeMeters = NormalizeFogCellSize(map.GridCellSizeMeters);
        fog.HiddenCells.Clear();
        fog.RevealedCells.Clear();
        fog.GMOnlyCells.Clear();
        TouchFog(fog, actor.Id);
        var saved = _repositories.MapFogLayers.UpsertAsync(fog).GetAwaiter().GetResult();
        _logger.Admin($"map.fog.reset.done mapId={mapId} revision={saved.Revision}");
        return Ok("Scene map fog reset.", new Dictionary<string, object>
        {
            { "mapId", mapId },
            { "fog", FogPayload(saved, map) },
            { "updatedAtUtc", saved.UpdatedAtUtc },
            { "revision", saved.Revision }
        });
    }

    public ResponseEnvelope MapSceneActiveSet(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!MapSceneSessionLinkEnabled())
        {
            _logger.Admin($"map.scene.active.disabled command={context.Request.Command}");
            return Error("scene map active link endpoints disabled", ResponseStatus.Forbidden, ErrorCode.Forbidden);
        }

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var campaignId = RequireLength(PayloadReader.GetString(payload, "campaignId"), 1, 128, "campaignId");
        var sessionId = RequireLength(PayloadReader.GetString(payload, "sessionId"), 0, 128, "sessionId");
        var activeGroupId = RequireLength(PayloadReader.GetString(payload, "activeGroupId"), 0, 128, "activeGroupId");
        var sceneId = RequireLength(PayloadReader.GetString(payload, "sceneId"), 0, 128, "sceneId");
        var mapId = RequireLength(PayloadReader.GetString(payload, "mapId"), 1, 128, "mapId");
        var visibilityMode = NormalizeVisibilityMode(RequireLength(PayloadReader.GetString(payload, "visibilityMode"), 0, 32, "visibilityMode"));
        var notes = RequireLength(PayloadReader.GetString(payload, "notes"), 0, 2048, "notes");

        _logger.Admin($"map.scene.active.set.start campaignId={campaignId} sessionId={sessionId} mapId={mapId}");
        var identity = _mapIdentityResolver.ResolveSceneMap(mapId);
        if (!identity.IsResolved) return MapIdentityError0202(identity);
        var map = identity.CanonicalMap!;
        mapId = identity.CanonicalMapId;
        if (!string.Equals(map.CampaignId, campaignId, StringComparison.OrdinalIgnoreCase))
            return Error("scene map campaign mismatch", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);

        _repositories.SceneMapActiveLinks.DeactivateScopeAsync(campaignId, sessionId, activeGroupId, sceneId).GetAwaiter().GetResult();
        var link = new SceneMapActiveLinkState
        {
            CampaignId = campaignId,
            SessionId = sessionId,
            ActiveGroupId = activeGroupId,
            SceneId = sceneId,
            MapId = mapId,
            MapName = map.Name ?? string.Empty,
            IsActive = true,
            VisibilityMode = string.IsNullOrWhiteSpace(visibilityMode) ? MapVisibilityModes.Party : visibilityMode,
            AssignedByUserId = actor.Id,
            AssignedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            Notes = notes
        };
        var saved = _repositories.SceneMapActiveLinks.UpsertAsync(link).GetAwaiter().GetResult();
        _logger.Admin($"map.scene.active.set.done campaignId={campaignId} sessionId={sessionId} mapId={mapId} linkId={saved.Id}");
        return Ok("Active scene map set.", ActiveLinkResponsePayload(saved, map, hasActiveMap: true));
    }

    public ResponseEnvelope MapSceneActiveGet(CommandContext context)
    {
        RequireAdmin(context);
        if (!MapSceneSessionLinkEnabled())
        {
            _logger.Admin($"map.scene.active.disabled command={context.Request.Command}");
            return Error("scene map active link endpoints disabled", ResponseStatus.Forbidden, ErrorCode.Forbidden);
        }

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var campaignId = RequireLength(PayloadReader.GetString(payload, "campaignId"), 1, 128, "campaignId");
        var sessionId = RequireLength(PayloadReader.GetString(payload, "sessionId"), 0, 128, "sessionId");
        var activeGroupId = RequireLength(PayloadReader.GetString(payload, "activeGroupId"), 0, 128, "activeGroupId");
        var sceneId = RequireLength(PayloadReader.GetString(payload, "sceneId"), 0, 128, "sceneId");

        var link = ResolveActiveSceneLink(campaignId, sessionId, activeGroupId, sceneId);
        if (link == null)
        {
            return Ok("No active scene map for scope.", new Dictionary<string, object>
            {
                { "hasActiveMap", false },
                { "campaignId", campaignId },
                { "sessionId", sessionId },
                { "activeGroupId", activeGroupId },
                { "sceneId", sceneId },
                { "warnings", new object[] { "active scene map is not assigned" } }
            });
        }

        var map = _repositories.MapCanvases.GetByIdAsync(link.MapId).GetAwaiter().GetResult();
        if (map == null || map.Deleted || map.Archived || map.IsArchived)
            return Ok("Active scene map link exists but map is unavailable.", new Dictionary<string, object>
            {
                { "hasActiveMap", false },
                { "campaignId", campaignId },
                { "sessionId", sessionId },
                { "activeGroupId", activeGroupId },
                { "sceneId", sceneId },
                { "warnings", new object[] { "active scene map link points to unavailable map" } }
            });

        var markers = _repositories.MapMarkers.ListByMapAsync(map.Id, includeArchived: false, limit: 2000).GetAwaiter().GetResult();
        var bindings = _repositories.MapMarkerBindings.ListByMapAsync(map.Id, 2000).GetAwaiter().GetResult();
        var fog = _repositories.MapFogLayers.GetByMapIdAsync(map.Id).GetAwaiter().GetResult();
        var payloadOut = ActiveLinkResponsePayload(link, map, hasActiveMap: true);
        payloadOut["adminMap"] = AdminSceneViewPayload(map, markers, bindings, fog);
        _logger.Admin($"map.scene.active.get campaignId={campaignId} sessionId={sessionId} mapId={map.Id}");
        return Ok("Active scene map loaded.", payloadOut);
    }

    public ResponseEnvelope MapSceneActiveClear(CommandContext context)
    {
        RequireAdmin(context);
        if (!MapSceneSessionLinkEnabled())
        {
            _logger.Admin($"map.scene.active.disabled command={context.Request.Command}");
            return Error("scene map active link endpoints disabled", ResponseStatus.Forbidden, ErrorCode.Forbidden);
        }

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var campaignId = RequireLength(PayloadReader.GetString(payload, "campaignId"), 1, 128, "campaignId");
        var sessionId = RequireLength(PayloadReader.GetString(payload, "sessionId"), 0, 128, "sessionId");
        var activeGroupId = RequireLength(PayloadReader.GetString(payload, "activeGroupId"), 0, 128, "activeGroupId");
        var sceneId = RequireLength(PayloadReader.GetString(payload, "sceneId"), 0, 128, "sceneId");

        var modified = _repositories.SceneMapActiveLinks.DeactivateScopeAsync(campaignId, sessionId, activeGroupId, sceneId).GetAwaiter().GetResult();
        _logger.Admin($"map.scene.active.clear campaignId={campaignId} sessionId={sessionId} modified={modified}");
        return Ok("Active scene map cleared.", new Dictionary<string, object>
        {
            { "hasActiveMap", false },
            { "campaignId", campaignId },
            { "sessionId", sessionId },
            { "activeGroupId", activeGroupId },
            { "sceneId", sceneId },
            { "clearedCount", modified }
        });
    }

    public ResponseEnvelope MapPlayerSceneActiveGet(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!MapPlayerSceneActiveEnabled())
        {
            _logger.Debug($"map.scene.active.disabled command={context.Request.Command} user={actor.Login}");
            return Error("player active scene map endpoint disabled", ResponseStatus.Forbidden, ErrorCode.Forbidden);
        }

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var campaignId = RequireLength(PayloadReader.GetString(payload, "campaignId"), 1, 128, "campaignId");
        var sessionId = RequireLength(PayloadReader.GetString(payload, "sessionId"), 0, 128, "sessionId");
        var activeGroupId = RequireLength(PayloadReader.GetString(payload, "activeGroupId"), 0, 128, "activeGroupId");
        var sceneId = RequireLength(PayloadReader.GetString(payload, "sceneId"), 0, 128, "sceneId");
        var characterId = RequireLength(PayloadReader.GetString(payload, "characterId"), 0, 128, "characterId");
        var includeMarkers = !payload.ContainsKey("includeMarkers") || PayloadReader.GetBool(payload, "includeMarkers");

        _logger.Debug($"map.player.scene.active.get.start user={actor.Login} campaignId={campaignId} sessionId={sessionId}");
        var link = ResolveActiveSceneLink(campaignId, sessionId, activeGroupId, sceneId);
        if (link == null || !link.IsActive || !IsActiveLinkVisibleForPlayer(link))
        {
            _logger.Debug($"map.player.scene.active.none user={actor.Login} campaignId={campaignId} sessionId={sessionId}");
            return Ok("No active scene map.", new Dictionary<string, object>
            {
                { "hasActiveMap", false },
                { "warnings", new object[] { "active scene map is not assigned" } },
                { "builtAtUtc", DateTime.UtcNow }
            });
        }

        var forward = new CommandContext
        {
            ConnectionId = context.ConnectionId,
            Session = context.Session,
            Request = new RequestEnvelope
            {
                Command = CommandNames.MapPlayerSceneGet,
                Payload = new Dictionary<string, object>
                {
                    { "mapId", link.MapId },
                    { "characterId", characterId },
                    { "activeGroupId", activeGroupId },
                    { "includeMarkers", includeMarkers }
                }
            }
        };
        var sceneResponse = MapPlayerSceneGet(forward);
        if (sceneResponse.Status != ResponseStatus.Ok)
        {
            if (sceneResponse.Status == ResponseStatus.Forbidden || sceneResponse.Status == ResponseStatus.NotFound)
            {
                _logger.Debug($"map.player.scene.active.forbidden user={actor.Login} mapId={link.MapId}");
                return Ok("No active scene map.", new Dictionary<string, object>
                {
                    { "hasActiveMap", false },
                    { "warnings", new object[] { "active scene map is not available for player" } },
                    { "builtAtUtc", DateTime.UtcNow }
                });
            }

            return sceneResponse;
        }

        var warnings = new List<object>();
        var warningItems = PayloadReader.GetList(sceneResponse.Payload, "warnings");
        if (warningItems != null)
        {
            foreach (var warning in warningItems)
            {
                var text = Convert.ToString(warning);
                if (!string.IsNullOrWhiteSpace(text))
                    warnings.Add(text);
            }
        }

        var playerMap = PayloadReader.GetDictionary(sceneResponse.Payload, "map")
            ?? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        var canonicalMapId = PayloadReader.GetString(playerMap, "mapId") ?? link.MapId;
        var projectionRevision = PayloadReader.GetLong(sceneResponse.Payload, "projectionRevision")
            ?? PayloadReader.GetLong(playerMap, "projectionRevision")
            ?? 0L;
        var canonicalMapRevision = PayloadReader.GetLong(sceneResponse.Payload, "canonicalMapRevision")
            ?? PayloadReader.GetLong(playerMap, "canonicalMapRevision")
            ?? 0L;

        _logger.Debug($"map.player.scene.active.get.done user={actor.Login} mapId={link.MapId}");
        return Ok("Active scene map loaded.", new Dictionary<string, object>
        {
            { "hasActiveMap", true },
            { "mapId", canonicalMapId },
            { "map", playerMap },
            { "projectionRevision", projectionRevision },
            { "canonicalMapRevision", canonicalMapRevision },
            { "fullSnapshotVersion", 1 },
            { "snapshotKind", "full" },
            { "warnings", warnings.ToArray() },
            { "builtAtUtc", DateTime.UtcNow }
        });
    }

    public ResponseEnvelope MapPlayerSceneGet(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!MapPlayerSceneEnabled())
        {
            _logger.Debug($"map.player.scene.get.disabled user={actor.Login}");
            return Error("player scene map endpoints disabled", ResponseStatus.Forbidden, ErrorCode.Forbidden);
        }

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var mapId = RequireLength(PayloadReader.GetString(payload, "mapId"), 1, 128, "mapId");
        _logger.Debug($"map.player.scene.get.start user={actor.Login} mapId={mapId}");
        var projection = _playerMapProjectionService.BuildSceneMap(mapId, new PlayerMapProjectionContext0204
        {
            ActorUserId = actor.Id,
            CharacterId = PayloadReader.GetString(payload, "characterId") ?? string.Empty,
            CampaignId = PayloadReader.GetString(payload, "campaignId") ?? string.Empty,
            SessionId = PayloadReader.GetString(payload, "sessionId") ?? string.Empty,
            ActiveGroupId = PayloadReader.GetString(payload, "activeGroupId") ?? string.Empty,
            IncludeMarkers = !payload.ContainsKey("includeMarkers") || PayloadReader.GetBool(payload, "includeMarkers")
        });
        if (!projection.Success)
        {
            var status = projection.ErrorKind == "not_found" ? ResponseStatus.NotFound
                : projection.ErrorKind == "forbidden" ? ResponseStatus.Forbidden
                : ResponseStatus.Conflict;
            var code = status == ResponseStatus.NotFound ? ErrorCode.NotFound
                : status == ResponseStatus.Forbidden ? ErrorCode.Forbidden
                : ErrorCode.Conflict;
            return Error(projection.Message, status, code);
        }
        _logger.Debug($"map.player.scene.get.done user={actor.Login} mapId={mapId} projectionRevision={PayloadReader.GetString(projection.Payload, "projectionRevision")}");
        return Ok(projection.Message, projection.Payload);
    }

    private bool MapSceneReadEnabled()
    {
        return _featureFlags.IsEnabled(nameof(MapFeatureFlags.UseMapSystemV1))
            && _featureFlags.IsEnabled(nameof(MapFeatureFlags.UseSpaceHierarchyV1))
            && _featureFlags.IsEnabled(nameof(MapFeatureFlags.UseSceneMapV1));
    }

    private bool MapSceneWriteEnabled() => MapSceneReadEnabled();

    private bool MapMarkersEnabled() => _featureFlags.IsEnabled(nameof(MapFeatureFlags.UseSceneMapMarkers));

    private bool MapFogEnabled() => _featureFlags.IsEnabled(nameof(MapFeatureFlags.UseSceneMapFogOfWar));

    private bool MapSceneFogEnabled() => MapSceneReadEnabled() && MapFogEnabled();

    private bool MapSceneSessionLinkEnabled()
    {
        return MapSceneReadEnabled()
            && _featureFlags.IsEnabled(nameof(MapFeatureFlags.UseSceneMapSessionLink));
    }

    private bool MapPlayerSceneEnabled()
    {
        return MapSceneReadEnabled()
            && _featureFlags.IsEnabled(nameof(MapFeatureFlags.UseSceneMapPlayerView));
    }

    private bool MapPlayerSceneActiveEnabled()
    {
        return MapPlayerSceneEnabled()
            && MapSceneSessionLinkEnabled();
    }

    private ResponseEnvelope? ValidateSceneSettings(int widthMeters, int heightMeters, int gridCellSizeMeters)
    {
        var sizeErrors = MapRuntimeValidation.ValidateSceneDimensions(widthMeters, heightMeters);
        if (sizeErrors.Count > 0)
            return Error(string.Join("; ", sizeErrors), ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);
        if (gridCellSizeMeters < 1 || gridCellSizeMeters > 500 || !MapRuntimeValidation.IsValidGridCellSize(gridCellSizeMeters))
            return Error("gridCellSizeMeters must be between 1 and 500", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);
        return null;
    }

    private static Dictionary<string, object> MapSceneListItemPayload(MapCanvasState map, int markerCount, bool hasFog, bool isActive)
    {
        return new Dictionary<string, object>
        {
            { "mapId", map.Id },
            { "campaignId", map.CampaignId ?? string.Empty },
            { "ruleSetId", map.RuleSetId ?? string.Empty },
            { "spaceNodeId", map.SpaceNodeId ?? string.Empty },
            { "name", map.Name ?? string.Empty },
            { "description", map.Description ?? string.Empty },
            { "mapType", map.MapType ?? string.Empty },
            { "widthMeters", map.WidthMeters },
            { "heightMeters", map.HeightMeters },
            { "gridCellSizeMeters", map.GridCellSizeMeters },
            { "showGrid", Bool(map.ExtraData, "showGrid", true) },
            { "showCoordinates", Bool(map.ExtraData, "showCoordinates", true) },
            { "archived", map.Archived || map.IsArchived },
            { "updatedAtUtc", map.UpdatedAtUtc == default ? map.UpdatedUtc : map.UpdatedAtUtc },
            { "markerCount", markerCount },
            { "fogEnabled", hasFog },
            { "isActive", isActive }
        };
    }

    private static Dictionary<string, object> MapScenePayload(MapCanvasState map)
    {
        return new Dictionary<string, object>
        {
            { "mapId", map.Id },
            { "campaignId", map.CampaignId ?? string.Empty },
            { "ruleSetId", map.RuleSetId ?? string.Empty },
            { "spaceNodeId", map.SpaceNodeId ?? string.Empty },
            { "name", map.Name ?? string.Empty },
            { "description", map.Description ?? string.Empty },
            { "mapType", map.MapType ?? string.Empty },
            { "widthMeters", map.WidthMeters },
            { "heightMeters", map.HeightMeters },
            { "gridCellSizeMeters", map.GridCellSizeMeters },
            { "showGrid", Bool(map.ExtraData, "showGrid", true) },
            { "showCoordinates", Bool(map.ExtraData, "showCoordinates", true) },
            { "originX", map.OriginX },
            { "originY", map.OriginY },
            { "coordinateMode", map.CoordinateMode ?? string.Empty },
            { "visibilityMode", map.VisibilityMode ?? string.Empty },
            { "updatedAtUtc", map.UpdatedAtUtc == default ? map.UpdatedUtc : map.UpdatedAtUtc },
            { "serverOnlyDataPresent", map.ServerOnlyData != null && map.ServerOnlyData.Count > 0 }
        };
    }

    private static Dictionary<string, object> MarkerPayload(MapMarkerState marker)
    {
        return new Dictionary<string, object>
        {
            { "markerId", marker.Id },
            { "mapId", marker.MapId ?? string.Empty },
            { "campaignId", marker.CampaignId ?? string.Empty },
            { "name", marker.Name ?? string.Empty },
            { "markerType", marker.MarkerType ?? string.Empty },
            { "x", marker.X },
            { "y", marker.Y },
            { "iconKey", marker.IconKey ?? string.Empty },
            { "colorKey", marker.ColorKey ?? string.Empty },
            { "isPlayerVisible", marker.IsPlayerVisible },
            { "linkedEntityType", marker.LinkedEntityType ?? string.Empty },
            { "linkedEntityId", marker.LinkedEntityId ?? string.Empty },
            { "cardTitle", marker.CardTitle ?? string.Empty },
            { "cardDescription", marker.CardDescription ?? string.Empty },
            { "publicNotes", marker.PublicNotes ?? string.Empty },
            { "gmNotes", marker.GMNotes ?? string.Empty },
            { "updatedAtUtc", marker.UpdatedAtUtc == default ? marker.UpdatedUtc : marker.UpdatedAtUtc }
        };
    }

    private static Dictionary<string, object> MarkerBindingPayload(MapMarkerBindingState binding)
    {
        return new Dictionary<string, object>
        {
            { "bindingId", binding.Id },
            { "mapId", binding.MapId ?? string.Empty },
            { "markerId", binding.MarkerId ?? string.Empty },
            { "bindingType", binding.BindingType ?? string.Empty },
            { "entityId", binding.EntityId ?? string.Empty },
            { "displayName", binding.DisplayName ?? string.Empty },
            { "isPrimary", binding.IsPrimary },
            { "visibility", binding.Visibility ?? string.Empty }
        };
    }

    private static Dictionary<string, object> FogPayload(FogOfWarState? fog, MapCanvasState? map = null)
    {
        var fallbackCellSize = NormalizeFogCellSize(map?.GridCellSizeMeters ?? 25);
        if (fog == null)
        {
            return new Dictionary<string, object>
            {
                { "hasFog", false },
                { "mode", FogOfWarModeIds.Manual },
                { "cellSizeMeters", fallbackCellSize },
                { "defaultState", FogDefaultStateIds.Revealed },
                { "hiddenCells", Array.Empty<object>() },
                { "revealedCells", Array.Empty<object>() },
                { "revision", 0L }
            };
        }

        return new Dictionary<string, object>
        {
            { "hasFog", true },
            { "mode", fog.Mode ?? FogOfWarModeIds.Manual },
            { "cellSizeMeters", NormalizeFogCellSize(fog.CellSizeMeters) },
            { "defaultState", NormalizeFogDefaultState(fog.DefaultState) },
            { "hiddenCells", fog.HiddenCells.Select(FogCellPayload).Cast<object>().ToArray() },
            { "revealedCells", fog.RevealedCells.Select(FogCellPayload).Cast<object>().ToArray() },
            { "updatedAtUtc", fog.UpdatedAtUtc },
            { "revision", fog.Revision }
        };
    }

    private static Dictionary<string, object> FogCellPayload(MapFogCellRange range)
    {
        return new Dictionary<string, object>
        {
            { "fromX", range.FromX },
            { "fromY", range.FromY },
            { "toX", range.ToX },
            { "toY", range.ToY }
        };
    }

    private Dictionary<string, object> AdminSceneViewPayload(
        MapCanvasState map,
        IReadOnlyCollection<MapMarkerState> markers,
        IReadOnlyCollection<MapMarkerBindingState> bindings,
        FogOfWarState? fog)
    {
        return new Dictionary<string, object>
        {
            { "map", MapScenePayload(map) },
            { "markers", markers.Select(MarkerPayload).Cast<object>().ToArray() },
            { "markerBindings", bindings.Select(MarkerBindingPayload).Cast<object>().ToArray() },
            { "fog", FogPayload(fog) },
            { "builtAtUtc", DateTime.UtcNow }
        };
    }

    private static Dictionary<string, object> PlayerFogPayload(FogOfWarState? fog, MapCanvasState map, IReadOnlyCollection<MapFogCellRange> hiddenRanges)
    {
        if (fog == null)
        {
            return new Dictionary<string, object>
            {
                { "hasFog", false },
                { "mode", FogOfWarModeIds.Disabled },
                { "cellSizeMeters", NormalizeFogCellSize(map.GridCellSizeMeters) },
                { "defaultState", FogDefaultStateIds.Revealed },
                { "hiddenCells", Array.Empty<object>() },
                { "revealedCells", Array.Empty<object>() },
                { "revision", 0L }
            };
        }

        return new Dictionary<string, object>
        {
            { "hasFog", true },
            { "mode", NormalizeFogMode(fog.Mode) },
            { "cellSizeMeters", NormalizeFogCellSize(fog.CellSizeMeters) },
            { "defaultState", NormalizeFogDefaultState(fog.DefaultState) },
            { "hiddenCells", hiddenRanges.Select(FogCellPayload).Cast<object>().ToArray() },
            { "revealedCells", fog.RevealedCells.Select(FogCellPayload).Cast<object>().ToArray() },
            { "updatedAtUtc", fog.UpdatedAtUtc },
            { "revision", fog.Revision }
        };
    }

    private static bool IsMapVisibleForPlayer(MapCanvasState map)
    {
        var visibility = (map.VisibilityMode ?? string.Empty).Trim();
        if (visibility.Length == 0) return true;
        return !string.Equals(visibility, MapVisibilityModes.GmOnly, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(visibility, MapVisibilityModes.Hidden, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMarkerVisibleForPlayer(MapMarkerState marker)
    {
        if (!marker.IsPlayerVisible) return false;
        var visibility = (marker.VisibilityMode ?? string.Empty).Trim();
        if (visibility.Length == 0) return true;
        return !string.Equals(visibility, MapVisibilityModes.GmOnly, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(visibility, MapVisibilityModes.Hidden, StringComparison.OrdinalIgnoreCase);
    }

    private static Dictionary<string, object> PlayerMarkerPayload(MapMarkerState marker, IReadOnlyCollection<MapMarkerBindingState> bindings)
    {
        var binding = bindings.FirstOrDefault(IsBindingVisibleForPlayer);
        return new Dictionary<string, object>
        {
            { "markerId", marker.Id },
            { "name", marker.Name ?? string.Empty },
            { "markerType", marker.MarkerType ?? string.Empty },
            { "x", marker.X },
            { "y", marker.Y },
            { "iconKey", marker.IconKey ?? string.Empty },
            { "colorKey", marker.ColorKey ?? string.Empty },
            { "cardTitle", marker.CardTitle ?? string.Empty },
            { "cardDescription", marker.CardDescription ?? string.Empty },
            { "linkedEntityType", binding?.BindingType ?? string.Empty },
            { "linkedEntityDisplayName", binding?.DisplayName ?? string.Empty },
            { "isVisible", true }
        };
    }

    private static bool IsBindingVisibleForPlayer(MapMarkerBindingState binding)
    {
        var visibility = (binding.Visibility ?? string.Empty).Trim();
        if (visibility.Length == 0) return true;
        return !string.Equals(visibility, MapVisibilityModes.GmOnly, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(visibility, MapVisibilityModes.Hidden, StringComparison.OrdinalIgnoreCase);
    }

    private SceneMapActiveLinkState? ResolveActiveSceneLink(string campaignId, string sessionId, string activeGroupId, string sceneId)
    {
        var direct = _repositories.SceneMapActiveLinks
            .GetActiveByScopeAsync(campaignId, sessionId, activeGroupId, sceneId)
            .GetAwaiter()
            .GetResult();
        if (direct != null) return direct;
        if (string.IsNullOrWhiteSpace(sessionId) && string.IsNullOrWhiteSpace(activeGroupId) && string.IsNullOrWhiteSpace(sceneId))
            return null;
        return _repositories.SceneMapActiveLinks
            .GetActiveByScopeAsync(campaignId, string.Empty, string.Empty, string.Empty)
            .GetAwaiter()
            .GetResult();
    }

    private static string NormalizeVisibilityMode(string? value)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (trimmed.Length == 0) return MapVisibilityModes.Party;
        if (string.Equals(trimmed, MapVisibilityModes.Public, StringComparison.OrdinalIgnoreCase)) return MapVisibilityModes.Public;
        if (string.Equals(trimmed, MapVisibilityModes.Party, StringComparison.OrdinalIgnoreCase)) return MapVisibilityModes.Party;
        if (string.Equals(trimmed, MapVisibilityModes.GmOnly, StringComparison.OrdinalIgnoreCase)) return MapVisibilityModes.GmOnly;
        if (string.Equals(trimmed, MapVisibilityModes.Hidden, StringComparison.OrdinalIgnoreCase)) return MapVisibilityModes.Hidden;
        return MapVisibilityModes.Party;
    }

    private static bool IsActiveLinkVisibleForPlayer(SceneMapActiveLinkState link)
    {
        var visibility = (link.VisibilityMode ?? string.Empty).Trim();
        if (visibility.Length == 0) return true;
        return !string.Equals(visibility, MapVisibilityModes.GmOnly, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(visibility, MapVisibilityModes.Hidden, StringComparison.OrdinalIgnoreCase);
    }

    private Dictionary<string, object> ActiveLinkResponsePayload(SceneMapActiveLinkState link, MapCanvasState? map, bool hasActiveMap)
    {
        return new Dictionary<string, object>
        {
            { "hasActiveMap", hasActiveMap },
            { "linkId", link?.Id ?? string.Empty },
            { "campaignId", link?.CampaignId ?? string.Empty },
            { "sessionId", link?.SessionId ?? string.Empty },
            { "activeGroupId", link?.ActiveGroupId ?? string.Empty },
            { "sceneId", link?.SceneId ?? string.Empty },
            { "mapId", link?.MapId ?? string.Empty },
            { "mapName", FirstNonEmpty(link?.MapName ?? string.Empty, map?.Name ?? string.Empty) },
            { "assignedByUserId", link?.AssignedByUserId ?? string.Empty },
            { "assignedAtUtc", link?.AssignedAtUtc ?? DateTime.UtcNow },
            { "updatedAtUtc", link?.UpdatedAtUtc ?? DateTime.UtcNow },
            { "warnings", Array.Empty<object>() }
        };
    }

    private ResponseEnvelope MapSceneFogByRectangle(CommandContext context, string brushMode)
    {
        var actor = RequireAdmin(context);
        if (!MapSceneFogEnabled())
        {
            _logger.Admin($"map.fog.disabled command={context.Request.Command}");
            return Error("scene map fog endpoints disabled", ResponseStatus.Forbidden, ErrorCode.Forbidden);
        }

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var mapId = RequireLength(PayloadReader.GetString(payload, "mapId"), 1, 128, "mapId");
        var map = _repositories.MapCanvases.GetByIdAsync(mapId).GetAwaiter().GetResult();
        if (map == null || map.Deleted || !string.Equals(map.MapType, MapTypeIds.Scene, StringComparison.OrdinalIgnoreCase))
            return Error("scene map not found", ResponseStatus.NotFound, ErrorCode.NotFound);

        var fog = LoadOrCreateFogState(map, actor.Id);
        if (string.Equals(fog.Mode, FogOfWarModeIds.Disabled, StringComparison.OrdinalIgnoreCase))
            return Error("fog mode is disabled", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);

        var x = PayloadReader.GetDouble(payload, "x") ?? 0d;
        var y = PayloadReader.GetDouble(payload, "y") ?? 0d;
        var width = PayloadReader.GetDouble(payload, "widthMeters") ?? Math.Max(fog.CellSizeMeters, 1);
        var height = PayloadReader.GetDouble(payload, "heightMeters") ?? Math.Max(fog.CellSizeMeters, 1);
        var range = BuildFogRangeFromRect(map, fog, x, y, width, height);
        if (range == null)
            return Error("fog rectangle is outside map bounds", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);

        var applyResult = ApplyFogBrush(fog, range, brushMode);
        if (!applyResult.Success)
            return Error(applyResult.ErrorMessage ?? "fog update failed", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);

        TouchFog(fog, actor.Id);
        var saved = _repositories.MapFogLayers.UpsertAsync(fog).GetAwaiter().GetResult();
        _logger.Admin($"map.fog.{brushMode}.done mapId={mapId} revision={saved.Revision}");
        return Ok("Scene map fog updated.", new Dictionary<string, object>
        {
            { "mapId", mapId },
            { "fog", FogPayload(saved, map) },
            { "updatedAtUtc", saved.UpdatedAtUtc },
            { "revision", saved.Revision }
        });
    }

    private FogOfWarState LoadOrCreateFogState(MapCanvasState map, string actorUserId)
    {
        var fog = _repositories.MapFogLayers.GetByMapIdAsync(map.Id).GetAwaiter().GetResult();
        if (fog != null)
        {
            fog.CellSizeMeters = NormalizeFogCellSize(fog.CellSizeMeters);
            fog.Mode = NormalizeFogMode(fog.Mode);
            fog.DefaultState = NormalizeFogDefaultState(fog.DefaultState);
            fog.CampaignId = string.IsNullOrWhiteSpace(fog.CampaignId) ? map.CampaignId : fog.CampaignId;
            return fog;
        }

        return new FogOfWarState
        {
            MapId = map.Id,
            CampaignId = map.CampaignId,
            CellSizeMeters = NormalizeFogCellSize(map.GridCellSizeMeters),
            Mode = FogOfWarModeIds.Manual,
            DefaultState = FogDefaultStateIds.Revealed,
            UpdatedAtUtc = DateTime.UtcNow,
            UpdatedByUserId = actorUserId,
            Revision = 0
        };
    }

    private static void TouchFog(FogOfWarState fog, string actorUserId)
    {
        fog.UpdatedAtUtc = DateTime.UtcNow;
        fog.UpdatedByUserId = actorUserId ?? string.Empty;
        fog.Revision = Math.Max(0L, fog.Revision) + 1L;
        if (fog.HiddenCells == null) fog.HiddenCells = new List<MapFogCellRange>();
        if (fog.RevealedCells == null) fog.RevealedCells = new List<MapFogCellRange>();
        if (fog.GMOnlyCells == null) fog.GMOnlyCells = new List<MapFogCellRange>();
    }

    private ResponseEnvelope? ValidateFogCellSize(int cellSizeMeters)
    {
        if (cellSizeMeters < FogMinCellSizeMeters || cellSizeMeters > FogMaxCellSizeMeters)
            return Error($"fog cell size must be between {FogMinCellSizeMeters} and {FogMaxCellSizeMeters} meters", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);
        return null;
    }

    private static MapFogCellRange? BuildFogPaintRange(
        MapCanvasState map,
        FogOfWarState fog,
        string shape,
        double centerX,
        double centerY,
        double widthMeters,
        double heightMeters,
        double radiusMeters)
    {
        var cellSize = NormalizeFogCellSize(fog.CellSizeMeters);
        if (string.Equals(shape, FogShapeIds.Cell, StringComparison.OrdinalIgnoreCase))
        {
            return BuildFogRangeFromRect(map, fog, centerX, centerY, cellSize, cellSize);
        }

        if (string.Equals(shape, FogShapeIds.Circle, StringComparison.OrdinalIgnoreCase))
        {
            var radius = Math.Max(1d, radiusMeters);
            return BuildFogRangeFromRect(map, fog, centerX - radius, centerY - radius, radius * 2d, radius * 2d);
        }

        var width = Math.Max(1d, widthMeters);
        var height = Math.Max(1d, heightMeters);
        return BuildFogRangeFromRect(map, fog, centerX - (width / 2d), centerY - (height / 2d), width, height);
    }

    private static MapFogCellRange? BuildFogRangeFromRect(MapCanvasState map, FogOfWarState fog, double x, double y, double widthMeters, double heightMeters)
    {
        var clampedX = Math.Max(0d, x);
        var clampedY = Math.Max(0d, y);
        var right = Math.Min(map.WidthMeters, clampedX + Math.Max(1d, widthMeters));
        var bottom = Math.Min(map.HeightMeters, clampedY + Math.Max(1d, heightMeters));
        if (right <= 0d || bottom <= 0d || clampedX >= map.WidthMeters || clampedY >= map.HeightMeters)
            return null;

        var cellSize = NormalizeFogCellSize(fog.CellSizeMeters);
        var fromX = MapCanvasProjectionHelper.ToCellIndex(clampedX, cellSize);
        var fromY = MapCanvasProjectionHelper.ToCellIndex(clampedY, cellSize);
        var toX = MapCanvasProjectionHelper.ToCellIndex(Math.Max(0d, right - 0.0001d), cellSize);
        var toY = MapCanvasProjectionHelper.ToCellIndex(Math.Max(0d, bottom - 0.0001d), cellSize);

        var maxCellX = MapCanvasProjectionHelper.ToCellIndex(Math.Max(0d, map.WidthMeters - 0.0001d), cellSize);
        var maxCellY = MapCanvasProjectionHelper.ToCellIndex(Math.Max(0d, map.HeightMeters - 0.0001d), cellSize);
        fromX = Math.Max(0, Math.Min(fromX, maxCellX));
        fromY = Math.Max(0, Math.Min(fromY, maxCellY));
        toX = Math.Max(fromX, Math.Min(toX, maxCellX));
        toY = Math.Max(fromY, Math.Min(toY, maxCellY));
        return new MapFogCellRange { FromX = fromX, FromY = fromY, ToX = toX, ToY = toY };
    }

    private static FogApplyResult ApplyFogBrush(FogOfWarState fog, MapFogCellRange range, string brushMode)
    {
        if (range == null) return FogApplyResult.Fail("fog range is invalid");
        NormalizeRange(range);

        var hidden = fog.HiddenCells ?? new List<MapFogCellRange>();
        var revealed = fog.RevealedCells ?? new List<MapFogCellRange>();
        if (string.Equals(brushMode, FogBrushModeIds.Hide, StringComparison.OrdinalIgnoreCase))
        {
            revealed = SubtractRectangle(revealed, range);
            hidden.Add(CloneRange(range));
            hidden = ClampRanges(hidden);
            if (hidden == null) return FogApplyResult.Fail("fog area is too large for one action");
            fog.RevealedCells = revealed;
            fog.HiddenCells = hidden;
            return FogApplyResult.Ok();
        }

        hidden = SubtractRectangle(hidden, range);
        revealed.Add(CloneRange(range));
        revealed = ClampRanges(revealed);
        if (revealed == null) return FogApplyResult.Fail("fog area is too large for one action");
        fog.HiddenCells = hidden;
        fog.RevealedCells = revealed;
        return FogApplyResult.Ok();
    }

    private static List<MapFogCellRange> SubtractRectangle(IEnumerable<MapFogCellRange> source, MapFogCellRange cut)
    {
        var result = new List<MapFogCellRange>();
        foreach (var current in source)
        {
            if (!RangesIntersect(current, cut))
            {
                result.Add(CloneRange(current));
                continue;
            }

            var overlapFromX = Math.Max(current.FromX, cut.FromX);
            var overlapToX = Math.Min(current.ToX, cut.ToX);
            var overlapFromY = Math.Max(current.FromY, cut.FromY);
            var overlapToY = Math.Min(current.ToY, cut.ToY);

            if (current.FromY <= overlapFromY - 1)
                result.Add(new MapFogCellRange { FromX = current.FromX, ToX = current.ToX, FromY = current.FromY, ToY = overlapFromY - 1 });
            if (overlapToY + 1 <= current.ToY)
                result.Add(new MapFogCellRange { FromX = current.FromX, ToX = current.ToX, FromY = overlapToY + 1, ToY = current.ToY });
            if (current.FromX <= overlapFromX - 1)
                result.Add(new MapFogCellRange { FromX = current.FromX, ToX = overlapFromX - 1, FromY = overlapFromY, ToY = overlapToY });
            if (overlapToX + 1 <= current.ToX)
                result.Add(new MapFogCellRange { FromX = overlapToX + 1, ToX = current.ToX, FromY = overlapFromY, ToY = overlapToY });
        }

        return result.Where(IsValidRange).Select(CloneRange).ToList();
    }

    private static bool RangesIntersect(MapFogCellRange left, MapFogCellRange right)
    {
        return left.FromX <= right.ToX
            && left.ToX >= right.FromX
            && left.FromY <= right.ToY
            && left.ToY >= right.FromY;
    }

    private static void NormalizeRange(MapFogCellRange range)
    {
        if (range.FromX > range.ToX) (range.FromX, range.ToX) = (range.ToX, range.FromX);
        if (range.FromY > range.ToY) (range.FromY, range.ToY) = (range.ToY, range.FromY);
    }

    private static bool IsValidRange(MapFogCellRange range)
    {
        return range != null && range.FromX <= range.ToX && range.FromY <= range.ToY;
    }

    private static MapFogCellRange CloneRange(MapFogCellRange range)
    {
        return new MapFogCellRange
        {
            FromX = range.FromX,
            FromY = range.FromY,
            ToX = range.ToX,
            ToY = range.ToY
        };
    }

    private static List<MapFogCellRange>? ClampRanges(List<MapFogCellRange> ranges)
    {
        var normalized = ranges.Where(IsValidRange).Select(CloneRange).ToList();
        if (normalized.Count > FogMaxRangesPerLayer) return null;
        return normalized;
    }

    private static string NormalizeFogMode(string? mode)
    {
        if (string.Equals(mode, FogOfWarModeIds.Disabled, StringComparison.OrdinalIgnoreCase))
            return FogOfWarModeIds.Disabled;
        return FogOfWarModeIds.Manual;
    }

    private static string NormalizeFogDefaultState(string? state)
    {
        return string.Equals(state, FogDefaultStateIds.Hidden, StringComparison.OrdinalIgnoreCase)
            ? FogDefaultStateIds.Hidden
            : FogDefaultStateIds.Revealed;
    }

    private static string NormalizeFogBrushMode(string? mode)
    {
        return string.Equals(mode, FogBrushModeIds.Hide, StringComparison.OrdinalIgnoreCase)
            ? FogBrushModeIds.Hide
            : FogBrushModeIds.Reveal;
    }

    private static string NormalizeFogShape(string? shape)
    {
        if (string.Equals(shape, FogShapeIds.Cell, StringComparison.OrdinalIgnoreCase))
            return FogShapeIds.Cell;
        if (string.Equals(shape, FogShapeIds.Circle, StringComparison.OrdinalIgnoreCase))
            return FogShapeIds.Circle;
        return FogShapeIds.Rectangle;
    }

    private static string NormalizeFogFillState(string? state) => NormalizeFogDefaultState(state);

    private static string NormalizeFogClearMode(string? mode)
    {
        if (string.Equals(mode, FogClearModeIds.HideAll, StringComparison.OrdinalIgnoreCase))
            return FogClearModeIds.HideAll;
        if (string.Equals(mode, FogClearModeIds.RevealAll, StringComparison.OrdinalIgnoreCase))
            return FogClearModeIds.RevealAll;
        return FogClearModeIds.ClearCustom;
    }

    private static int NormalizeFogCellSize(int value)
    {
        if (value < FogMinCellSizeMeters) return FogMinCellSizeMeters;
        if (value > FogMaxCellSizeMeters) return FogMaxCellSizeMeters;
        return value;
    }

    private static IReadOnlyCollection<MapFogCellRange> BuildPlayerHiddenRanges(FogOfWarState? fog, MapCanvasState map)
    {
        if (fog == null) return Array.Empty<MapFogCellRange>();
        if (string.Equals(NormalizeFogMode(fog.Mode), FogOfWarModeIds.Disabled, StringComparison.OrdinalIgnoreCase))
            return Array.Empty<MapFogCellRange>();

        var defaultState = NormalizeFogDefaultState(fog.DefaultState);
        var hidden = (fog.HiddenCells ?? new List<MapFogCellRange>()).Where(IsValidRange).Select(CloneRange).ToList();
        var revealed = (fog.RevealedCells ?? new List<MapFogCellRange>()).Where(IsValidRange).Select(CloneRange).ToList();
        if (string.Equals(defaultState, FogDefaultStateIds.Hidden, StringComparison.OrdinalIgnoreCase))
        {
            var full = BuildFogRangeFromRect(map, fog, 0d, 0d, map.WidthMeters, map.HeightMeters);
            if (full == null) return Array.Empty<MapFogCellRange>();
            var hiddenFromDefault = new List<MapFogCellRange> { full };
            foreach (var revealRange in revealed)
                hiddenFromDefault = SubtractRectangle(hiddenFromDefault, revealRange);
            hiddenFromDefault.AddRange(hidden);
            return hiddenFromDefault.Where(IsValidRange).Take(FogMaxRangesPerProjection).Select(CloneRange).ToArray();
        }

        return hidden.Where(IsValidRange).Take(FogMaxRangesPerProjection).Select(CloneRange).ToArray();
    }

    private static bool IsMarkerVisibleForPlayerByFog(
        MapMarkerState marker,
        MapCanvasState map,
        FogOfWarState? fog,
        IReadOnlyCollection<MapFogCellRange> hiddenRanges)
    {
        if (marker == null || map == null) return false;
        if (fog == null) return true;
        if (string.Equals(NormalizeFogMode(fog.Mode), FogOfWarModeIds.Disabled, StringComparison.OrdinalIgnoreCase))
            return true;

        var cellSize = NormalizeFogCellSize(fog.CellSizeMeters);
        var markerCellX = MapCanvasProjectionHelper.ToCellIndex(marker.X, cellSize);
        var markerCellY = MapCanvasProjectionHelper.ToCellIndex(marker.Y, cellSize);
        foreach (var range in hiddenRanges)
        {
            if (markerCellX >= range.FromX && markerCellX <= range.ToX && markerCellY >= range.FromY && markerCellY <= range.ToY)
                return false;
        }

        return true;
    }

    private static string NormalizeMarkerType(string? markerType)
    {
        var value = (markerType ?? string.Empty).Trim();
        if (value.Length == 0) return MapMarkerTypeIds.Custom;
        return AllowedMarkerTypes.Contains(value, StringComparer.OrdinalIgnoreCase) ? value : MapMarkerTypeIds.Custom;
    }

    private static bool Bool(IDictionary<string, object>? map, string key, bool fallback)
    {
        if (map == null || string.IsNullOrWhiteSpace(key)) return fallback;
        if (!map.TryGetValue(key, out var value) || value == null) return fallback;
        if (value is bool boolValue) return boolValue;
        return bool.TryParse(Convert.ToString(value), out var parsed) ? parsed : fallback;
    }

    private static readonly string[] AllowedMarkerTypes =
    {
        MapMarkerTypeIds.PlayerCharacter,
        MapMarkerTypeIds.Npc,
        MapMarkerTypeIds.Companion,
        MapMarkerTypeIds.Enemy,
        MapMarkerTypeIds.Neutral,
        MapMarkerTypeIds.PointOfInterest,
        MapMarkerTypeIds.Entrance,
        MapMarkerTypeIds.Exit,
        MapMarkerTypeIds.Cover,
        MapMarkerTypeIds.Objective,
        MapMarkerTypeIds.Hazard,
        MapMarkerTypeIds.Item,
        MapMarkerTypeIds.Vehicle,
        MapMarkerTypeIds.Custom
    };

    private static class FogBrushModeIds
    {
        public const string Reveal = "reveal";
        public const string Hide = "hide";
    }

    private static class FogShapeIds
    {
        public const string Rectangle = "rectangle";
        public const string Circle = "circle";
        public const string Cell = "cell";
    }

    private static class FogClearModeIds
    {
        public const string RevealAll = "reveal_all";
        public const string HideAll = "hide_all";
        public const string ClearCustom = "clear_custom";
    }

    private sealed class FogApplyResult
    {
        public bool Success { get; private set; }
        public string? ErrorMessage { get; private set; }

        public static FogApplyResult Ok() => new FogApplyResult { Success = true };

        public static FogApplyResult Fail(string message) => new FogApplyResult
        {
            Success = false,
            ErrorMessage = message
        };
    }

    private const int FogMinCellSizeMeters = 5;
    private const int FogMaxCellSizeMeters = 500;
    private const int FogMaxRangesPerLayer = 10000;
    private const int FogMaxRangesPerProjection = 12000;
}
