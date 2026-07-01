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
    public ResponseEnvelope MapRoomList(CommandContext context)
    {
        RequireAdmin(context);
        if (!MapRoomReadEnabled())
            return MapRoomDisabled(context.Request.Command);

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var campaignId = RequireLength(PayloadReader.GetString(payload, "campaignId"), 1, 128, "campaignId");
        var parentLocationId = RequireLength(PayloadReader.GetString(payload, "parentLocationId"), 0, 128, "parentLocationId");
        var parentSceneMapId = RequireLength(PayloadReader.GetString(payload, "parentSceneMapId"), 0, 128, "parentSceneMapId");
        var includeArchived = PayloadReader.GetBool(payload, "includeArchived");

        IReadOnlyCollection<RoomInteriorState> rooms;
        if (string.IsNullOrWhiteSpace(parentLocationId) && string.IsNullOrWhiteSpace(parentSceneMapId))
            rooms = _repositories.RoomInteriors.ListByCampaignAsync(campaignId, includeArchived, 500).GetAwaiter().GetResult();
        else
            rooms = _repositories.RoomInteriors.ListByParentAsync(campaignId, parentLocationId, parentSceneMapId, includeArchived, 500).GetAwaiter().GetResult();

        var items = rooms.Select(RoomListItemPayload).Cast<object>().ToArray();
        return Ok("Rooms loaded.", new Dictionary<string, object>
        {
            { "items", items },
            { "count", items.Length }
        });
    }

    public ResponseEnvelope MapRoomCreate(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!MapRoomWriteEnabled())
            return MapRoomDisabled(context.Request.Command);

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var campaignId = RequireLength(PayloadReader.GetString(payload, "campaignId"), 1, 128, "campaignId");
        var ruleSetId = RequireLength(PayloadReader.GetString(payload, "ruleSetId"), 1, 128, "ruleSetId");
        var name = RequireLength(PayloadReader.GetString(payload, "name"), 1, 160, "name");
        var description = RequireLength(PayloadReader.GetString(payload, "description"), 0, 4096, "description");
        var roomType = NormalizeRoomType(PayloadReader.GetString(payload, "roomType"));
        var interiorType = NormalizeInteriorType(PayloadReader.GetString(payload, "interiorType"));
        var visibilityMode = NormalizeVisibilityMode(RequireLength(PayloadReader.GetString(payload, "visibilityMode"), 0, 32, "visibilityMode"));
        var isPlayerVisible = !payload.ContainsKey("isPlayerVisible") || PayloadReader.GetBool(payload, "isPlayerVisible");
        var widthMeters = PayloadReader.GetDouble(payload, "widthMeters");
        var heightMeters = PayloadReader.GetDouble(payload, "heightMeters");
        var gridCellSizeMeters = PayloadReader.GetInt(payload, "gridCellSizeMeters");

        var sizeValidation = MapRuntimeValidation.ValidateRoomDimensions(widthMeters, heightMeters, gridCellSizeMeters);
        if (sizeValidation.Count > 0)
            return Error(string.Join("; ", sizeValidation), ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);

        var now = DateTime.UtcNow;
        var room = new RoomInteriorState
        {
            CampaignId = campaignId,
            RuleSetId = ruleSetId,
            SpaceNodeId = RequireLength(PayloadReader.GetString(payload, "spaceNodeId"), 0, 128, "spaceNodeId"),
            ParentSpaceNodeId = RequireLength(PayloadReader.GetString(payload, "parentSpaceNodeId"), 0, 128, "parentSpaceNodeId"),
            ParentLocationId = RequireLength(PayloadReader.GetString(payload, "parentLocationId"), 0, 128, "parentLocationId"),
            ParentSceneMapId = RequireLength(PayloadReader.GetString(payload, "parentSceneMapId"), 0, 128, "parentSceneMapId"),
            ParentWorldMapId = RequireLength(PayloadReader.GetString(payload, "parentWorldMapId"), 0, 128, "parentWorldMapId"),
            Name = name,
            Description = description,
            RoomType = roomType,
            InteriorType = interiorType,
            WidthMeters = widthMeters ?? MapRuntimeValidation.RoomDefaultSizeMeters,
            HeightMeters = heightMeters ?? MapRuntimeValidation.RoomDefaultSizeMeters,
            AreaSquareMeters = ComputeArea(widthMeters, heightMeters),
            GridCellSizeMeters = gridCellSizeMeters.GetValueOrDefault(2) <= 0 ? 2 : gridCellSizeMeters.GetValueOrDefault(2),
            LayoutMode = NormalizeRoomLayoutMode(PayloadReader.GetString(payload, "layoutMode")),
            VisibilityMode = string.IsNullOrWhiteSpace(visibilityMode) ? MapVisibilityModes.Party : visibilityMode,
            IsPlayerVisible = isPlayerVisible,
            PublicNotes = RequireLength(PayloadReader.GetString(payload, "publicNotes"), 0, 4096, "publicNotes"),
            GMNotes = RequireLength(PayloadReader.GetString(payload, "gmNotes"), 0, 4096, "gmNotes"),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            CreatedByUserId = actor.Id,
            UpdatedByUserId = actor.Id
        };

        _logger.Admin($"map.room.create.start campaignId={campaignId} parentLocationId={room.ParentLocationId} parentSceneMapId={room.ParentSceneMapId}");
        var saved = _repositories.RoomInteriors.UpsertAsync(room).GetAwaiter().GetResult();
        _logger.Admin($"map.room.create.done roomId={saved.Id}");

        return Ok("Room created.", new Dictionary<string, object>
        {
            { "roomId", saved.Id },
            { "room", RoomPayload(saved) }
        });
    }

    public ResponseEnvelope MapRoomGet(CommandContext context)
    {
        RequireAdmin(context);
        if (!MapRoomReadEnabled())
            return MapRoomDisabled(context.Request.Command);

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var roomId = RequireLength(PayloadReader.GetString(payload, "roomId"), 1, 128, "roomId");
        var room = _repositories.RoomInteriors.GetByIdAsync(roomId).GetAwaiter().GetResult();
        if (room == null || room.Deleted || room.Archived || room.IsArchived)
            return Error("room not found", ResponseStatus.NotFound, ErrorCode.NotFound);

        var markers = RoomMarkersEnabled()
            ? _repositories.MapMarkers.ListByMapAsync(room.Id, includeArchived: false, limit: 5000).GetAwaiter().GetResult()
            : Array.Empty<MapMarkerState>();

        _logger.Admin($"map.room.get roomId={roomId}");
        return Ok("Room loaded.", new Dictionary<string, object>
        {
            { "room", RoomPayload(room) },
            { "markers", markers.Select(RoomMarkerPayload).Cast<object>().ToArray() },
            { "markerCount", markers.Count }
        });
    }

    public ResponseEnvelope MapRoomUpdate(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!MapRoomWriteEnabled())
            return MapRoomDisabled(context.Request.Command);

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var roomId = RequireLength(PayloadReader.GetString(payload, "roomId"), 1, 128, "roomId");
        var room = _repositories.RoomInteriors.GetByIdAsync(roomId).GetAwaiter().GetResult();
        if (room == null || room.Deleted || room.Archived || room.IsArchived)
            return Error("room not found", ResponseStatus.NotFound, ErrorCode.NotFound);

        if (payload.ContainsKey("name")) room.Name = RequireLength(PayloadReader.GetString(payload, "name"), 1, 160, "name");
        if (payload.ContainsKey("description")) room.Description = RequireLength(PayloadReader.GetString(payload, "description"), 0, 4096, "description");
        if (payload.ContainsKey("roomType")) room.RoomType = NormalizeRoomType(PayloadReader.GetString(payload, "roomType"));
        if (payload.ContainsKey("interiorType")) room.InteriorType = NormalizeInteriorType(PayloadReader.GetString(payload, "interiorType"));
        if (payload.ContainsKey("widthMeters")) room.WidthMeters = PayloadReader.GetDouble(payload, "widthMeters");
        if (payload.ContainsKey("heightMeters")) room.HeightMeters = PayloadReader.GetDouble(payload, "heightMeters");
        if (payload.ContainsKey("gridCellSizeMeters")) room.GridCellSizeMeters = Math.Max(1, PayloadReader.GetInt(payload, "gridCellSizeMeters") ?? room.GridCellSizeMeters);
        if (payload.ContainsKey("isPlayerVisible")) room.IsPlayerVisible = PayloadReader.GetBool(payload, "isPlayerVisible");
        if (payload.ContainsKey("visibilityMode")) room.VisibilityMode = NormalizeVisibilityMode(RequireLength(PayloadReader.GetString(payload, "visibilityMode"), 0, 32, "visibilityMode"));
        if (payload.ContainsKey("publicNotes")) room.PublicNotes = RequireLength(PayloadReader.GetString(payload, "publicNotes"), 0, 4096, "publicNotes");
        if (payload.ContainsKey("gmNotes")) room.GMNotes = RequireLength(PayloadReader.GetString(payload, "gmNotes"), 0, 4096, "gmNotes");

        room.AreaSquareMeters = ComputeArea(room.WidthMeters, room.HeightMeters);
        room.UpdatedAtUtc = DateTime.UtcNow;
        room.UpdatedByUserId = actor.Id;

        var sizeValidation = MapRuntimeValidation.ValidateRoomDimensions(room.WidthMeters, room.HeightMeters, room.GridCellSizeMeters);
        if (sizeValidation.Count > 0)
            return Error(string.Join("; ", sizeValidation), ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);

        var saved = _repositories.RoomInteriors.UpsertAsync(room).GetAwaiter().GetResult();
        _logger.Admin($"map.room.update roomId={roomId}");
        return Ok("Room updated.", new Dictionary<string, object>
        {
            { "roomId", saved.Id },
            { "room", RoomPayload(saved) }
        });
    }

    public ResponseEnvelope MapRoomArchive(CommandContext context)
    {
        RequireAdmin(context);
        if (!MapRoomWriteEnabled())
            return MapRoomDisabled(context.Request.Command);

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var roomId = RequireLength(PayloadReader.GetString(payload, "roomId"), 1, 128, "roomId");
        var archived = _repositories.RoomInteriors.ArchiveAsync(roomId).GetAwaiter().GetResult();
        if (!archived) return Error("room not found", ResponseStatus.NotFound, ErrorCode.NotFound);
        _logger.Admin($"map.room.archive roomId={roomId}");
        return Ok("Room archived.", new Dictionary<string, object> { { "roomId", roomId } });
    }

    public ResponseEnvelope MapRoomMarkerList(CommandContext context)
    {
        RequireAdmin(context);
        if (!RoomMarkersEnabled())
            return MapRoomDisabled(context.Request.Command);

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var roomId = RequireLength(PayloadReader.GetString(payload, "roomId"), 1, 128, "roomId");
        var room = _repositories.RoomInteriors.GetByIdAsync(roomId).GetAwaiter().GetResult();
        if (room == null || room.Deleted || room.Archived || room.IsArchived)
            return Error("room not found", ResponseStatus.NotFound, ErrorCode.NotFound);

        var items = _repositories.MapMarkers.ListByMapAsync(roomId, includeArchived: false, limit: 5000).GetAwaiter().GetResult();
        return Ok("Room markers loaded.", new Dictionary<string, object>
        {
            { "items", items.Select(RoomMarkerPayload).Cast<object>().ToArray() },
            { "count", items.Count }
        });
    }

    public ResponseEnvelope MapRoomMarkerAdd(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!RoomMarkersEnabled())
            return MapRoomDisabled(context.Request.Command);

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var roomId = RequireLength(PayloadReader.GetString(payload, "roomId"), 1, 128, "roomId");
        var room = _repositories.RoomInteriors.GetByIdAsync(roomId).GetAwaiter().GetResult();
        if (room == null || room.Deleted || room.Archived || room.IsArchived)
            return Error("room not found", ResponseStatus.NotFound, ErrorCode.NotFound);

        var marker = new MapMarkerState
        {
            MapId = room.Id,
            CampaignId = room.CampaignId,
            Name = RequireLength(PayloadReader.GetString(payload, "name"), 0, 180, "name"),
            MarkerType = NormalizeRoomMarkerType(PayloadReader.GetString(payload, "markerType")),
            X = PayloadReader.GetDouble(payload, "x") ?? 0d,
            Y = PayloadReader.GetDouble(payload, "y") ?? 0d,
            IsPlayerVisible = !payload.ContainsKey("isPlayerVisible") || PayloadReader.GetBool(payload, "isPlayerVisible"),
            VisibilityMode = NormalizeVisibilityMode(RequireLength(PayloadReader.GetString(payload, "visibilityMode"), 0, 32, "visibilityMode")),
            LinkedEntityType = RequireLength(PayloadReader.GetString(payload, "linkedEntityType"), 0, 64, "linkedEntityType"),
            LinkedEntityId = RequireLength(PayloadReader.GetString(payload, "linkedEntityId"), 0, 128, "linkedEntityId"),
            LinkedEntityDisplayName = RequireLength(PayloadReader.GetString(payload, "linkedEntityDisplayName"), 0, 256, "linkedEntityDisplayName"),
            LinkedEntityPublicLabel = RequireLength(PayloadReader.GetString(payload, "linkedEntityPublicLabel"), 0, 256, "linkedEntityPublicLabel"),
            PublicNotes = RequireLength(PayloadReader.GetString(payload, "publicNotes"), 0, 4096, "publicNotes"),
            GMNotes = RequireLength(PayloadReader.GetString(payload, "gmNotes"), 0, 4096, "gmNotes"),
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = actor.Id,
            UpdatedByUserId = actor.Id
        };

        if (string.IsNullOrWhiteSpace(marker.Name))
            marker.Name = "Маркер";
        if (!MapRuntimeValidation.IsRoomMarkerInsideBounds(marker.X, marker.Y, room))
            return Error("marker coordinates are outside room bounds", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);

        var saved = _repositories.MapMarkers.UpsertAsync(marker).GetAwaiter().GetResult();
        _logger.Admin($"map.room.marker.add roomId={roomId} markerId={saved.Id}");
        return Ok("Room marker added.", new Dictionary<string, object>
        {
            { "markerId", saved.Id },
            { "marker", RoomMarkerPayload(saved) }
        });
    }

    public ResponseEnvelope MapRoomMarkerMove(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!RoomMarkersEnabled())
            return MapRoomDisabled(context.Request.Command);

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var markerId = RequireLength(PayloadReader.GetString(payload, "markerId"), 1, 128, "markerId");
        var marker = _repositories.MapMarkers.GetByIdAsync(markerId).GetAwaiter().GetResult();
        if (marker == null || marker.Deleted || marker.Archived)
            return Error("marker not found", ResponseStatus.NotFound, ErrorCode.NotFound);

        var room = _repositories.RoomInteriors.GetByIdAsync(marker.MapId).GetAwaiter().GetResult();
        if (room == null || room.Deleted || room.Archived || room.IsArchived)
            return Error("room not found", ResponseStatus.NotFound, ErrorCode.NotFound);

        marker.X = PayloadReader.GetDouble(payload, "x") ?? marker.X;
        marker.Y = PayloadReader.GetDouble(payload, "y") ?? marker.Y;
        if (!MapRuntimeValidation.IsRoomMarkerInsideBounds(marker.X, marker.Y, room))
            return Error("marker coordinates are outside room bounds", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);

        marker.UpdatedAtUtc = DateTime.UtcNow;
        marker.UpdatedByUserId = actor.Id;
        var saved = _repositories.MapMarkers.UpsertAsync(marker).GetAwaiter().GetResult();
        _logger.Admin($"map.room.marker.move roomId={room.Id} markerId={saved.Id}");
        return Ok("Room marker moved.", new Dictionary<string, object>
        {
            { "markerId", saved.Id },
            { "marker", RoomMarkerPayload(saved) }
        });
    }

    public ResponseEnvelope MapRoomMarkerUpdate(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!RoomMarkersEnabled())
            return MapRoomDisabled(context.Request.Command);

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var markerId = RequireLength(PayloadReader.GetString(payload, "markerId"), 1, 128, "markerId");
        var marker = _repositories.MapMarkers.GetByIdAsync(markerId).GetAwaiter().GetResult();
        if (marker == null || marker.Deleted || marker.Archived)
            return Error("marker not found", ResponseStatus.NotFound, ErrorCode.NotFound);

        var room = _repositories.RoomInteriors.GetByIdAsync(marker.MapId).GetAwaiter().GetResult();
        if (room == null || room.Deleted || room.Archived || room.IsArchived)
            return Error("room not found", ResponseStatus.NotFound, ErrorCode.NotFound);

        if (payload.ContainsKey("name")) marker.Name = RequireLength(PayloadReader.GetString(payload, "name"), 0, 180, "name");
        if (payload.ContainsKey("markerType")) marker.MarkerType = NormalizeRoomMarkerType(PayloadReader.GetString(payload, "markerType"));
        if (payload.ContainsKey("x")) marker.X = PayloadReader.GetDouble(payload, "x") ?? marker.X;
        if (payload.ContainsKey("y")) marker.Y = PayloadReader.GetDouble(payload, "y") ?? marker.Y;
        if (payload.ContainsKey("linkedEntityType")) marker.LinkedEntityType = RequireLength(PayloadReader.GetString(payload, "linkedEntityType"), 0, 64, "linkedEntityType");
        if (payload.ContainsKey("linkedEntityId")) marker.LinkedEntityId = RequireLength(PayloadReader.GetString(payload, "linkedEntityId"), 0, 128, "linkedEntityId");
        if (payload.ContainsKey("linkedEntityDisplayName")) marker.LinkedEntityDisplayName = RequireLength(PayloadReader.GetString(payload, "linkedEntityDisplayName"), 0, 256, "linkedEntityDisplayName");
        if (payload.ContainsKey("linkedEntityPublicLabel")) marker.LinkedEntityPublicLabel = RequireLength(PayloadReader.GetString(payload, "linkedEntityPublicLabel"), 0, 256, "linkedEntityPublicLabel");
        if (payload.ContainsKey("isPlayerVisible")) marker.IsPlayerVisible = PayloadReader.GetBool(payload, "isPlayerVisible");
        if (payload.ContainsKey("visibilityMode")) marker.VisibilityMode = NormalizeVisibilityMode(RequireLength(PayloadReader.GetString(payload, "visibilityMode"), 0, 32, "visibilityMode"));
        if (payload.ContainsKey("publicNotes")) marker.PublicNotes = RequireLength(PayloadReader.GetString(payload, "publicNotes"), 0, 4096, "publicNotes");
        if (payload.ContainsKey("gmNotes")) marker.GMNotes = RequireLength(PayloadReader.GetString(payload, "gmNotes"), 0, 4096, "gmNotes");

        if (string.IsNullOrWhiteSpace(marker.Name))
            marker.Name = "Маркер";
        if (!MapRuntimeValidation.IsRoomMarkerInsideBounds(marker.X, marker.Y, room))
            return Error("marker coordinates are outside room bounds", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);

        marker.UpdatedAtUtc = DateTime.UtcNow;
        marker.UpdatedByUserId = actor.Id;
        var saved = _repositories.MapMarkers.UpsertAsync(marker).GetAwaiter().GetResult();
        _logger.Admin($"map.room.marker.update roomId={room.Id} markerId={saved.Id}");
        return Ok("Room marker updated.", new Dictionary<string, object>
        {
            { "markerId", saved.Id },
            { "marker", RoomMarkerPayload(saved) }
        });
    }

    public ResponseEnvelope MapRoomMarkerRemove(CommandContext context)
    {
        RequireAdmin(context);
        if (!RoomMarkersEnabled())
            return MapRoomDisabled(context.Request.Command);

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var markerId = RequireLength(PayloadReader.GetString(payload, "markerId"), 1, 128, "markerId");
        var archived = _repositories.MapMarkers.ArchiveAsync(markerId).GetAwaiter().GetResult();
        if (!archived) return Error("marker not found", ResponseStatus.NotFound, ErrorCode.NotFound);
        _logger.Admin($"map.room.marker.remove markerId={markerId}");
        return Ok("Room marker removed.", new Dictionary<string, object> { { "markerId", markerId } });
    }

    public ResponseEnvelope MapPlayerRoomList(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!RoomPlayerViewEnabled())
            return Error("room player view is disabled by feature flags", ResponseStatus.Forbidden, ErrorCode.Forbidden);

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var campaignId = RequireLength(PayloadReader.GetString(payload, "campaignId"), 1, 128, "campaignId");
        var parentLocationId = RequireLength(PayloadReader.GetString(payload, "parentLocationId"), 0, 128, "parentLocationId");
        var parentSceneMapId = RequireLength(PayloadReader.GetString(payload, "parentSceneMapId"), 0, 128, "parentSceneMapId");

        IReadOnlyCollection<RoomInteriorState> rooms;
        if (string.IsNullOrWhiteSpace(parentLocationId) && string.IsNullOrWhiteSpace(parentSceneMapId))
            rooms = _repositories.RoomInteriors.ListByCampaignAsync(campaignId, includeArchived: false, limit: 500).GetAwaiter().GetResult();
        else
            rooms = _repositories.RoomInteriors.ListByParentAsync(campaignId, parentLocationId, parentSceneMapId, includeArchived: false, limit: 500).GetAwaiter().GetResult();

        var visible = rooms.Where(IsRoomVisibleForPlayer)
            .Select(room => new Dictionary<string, object>
            {
                { "roomId", room.Id },
                { "name", room.Name ?? string.Empty },
                { "description", room.Description ?? string.Empty },
                { "roomType", room.RoomType ?? RoomTypeIds.Room },
                { "interiorType", room.InteriorType ?? InteriorTypeIds.Building },
                { "updatedAtUtc", room.UpdatedAtUtc }
            })
            .Cast<object>()
            .ToArray();

        _logger.Debug($"map.player.room.list user={actor.Login} count={visible.Length}");
        return Ok("Player rooms loaded.", new Dictionary<string, object>
        {
            { "items", visible },
            { "count", visible.Length }
        });
    }

    public ResponseEnvelope MapPlayerRoomGet(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!RoomPlayerViewEnabled())
            return Error("room player view is disabled by feature flags", ResponseStatus.Forbidden, ErrorCode.Forbidden);

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var roomId = RequireLength(PayloadReader.GetString(payload, "roomId"), 1, 128, "roomId");
        _logger.Debug($"map.player.room.get user={actor.Login} roomId={roomId}");

        var room = _repositories.RoomInteriors.GetByIdAsync(roomId).GetAwaiter().GetResult();
        if (room == null || room.Deleted || room.Archived || room.IsArchived)
            return Error("room not found", ResponseStatus.NotFound, ErrorCode.NotFound);
        if (!IsRoomVisibleForPlayer(room))
            return Error("room is not visible for player", ResponseStatus.Forbidden, ErrorCode.Forbidden);

        var markers = RoomMarkersEnabled()
            ? _repositories.MapMarkers.ListByMapAsync(room.Id, includeArchived: false, limit: 5000).GetAwaiter().GetResult()
            : Array.Empty<MapMarkerState>();
        var visibleMarkers = markers.Where(IsRoomMarkerVisibleForPlayer).ToList();

        var map = new Dictionary<string, object>
        {
            { "roomId", room.Id },
            { "name", room.Name ?? string.Empty },
            { "description", room.Description ?? string.Empty },
            { "roomType", room.RoomType ?? RoomTypeIds.Room },
            { "interiorType", room.InteriorType ?? InteriorTypeIds.Building },
            { "widthMeters", room.WidthMeters ?? 0d },
            { "heightMeters", room.HeightMeters ?? 0d },
            { "gridCellSizeMeters", room.GridCellSizeMeters > 0 ? room.GridCellSizeMeters : 2 },
            { "publicNotes", room.PublicNotes ?? string.Empty },
            { "markers", visibleMarkers.Select(PlayerRoomMarkerPayload).Cast<object>().ToArray() },
            { "builtAtUtc", DateTime.UtcNow }
        };

        return Ok("Player room loaded.", new Dictionary<string, object>
        {
            { "map", map },
            { "warnings", Array.Empty<object>() },
            { "builtAtUtc", DateTime.UtcNow }
        });
    }

    private ResponseEnvelope MapRoomDisabled(string commandName)
    {
        _logger.Admin($"map.room.disabled command={commandName}");
        return Error("room/interior endpoints disabled by feature flags", ResponseStatus.Forbidden, ErrorCode.Forbidden);
    }

    private bool MapRoomReadEnabled()
    {
        return _featureFlags.IsEnabled(nameof(MapFeatureFlags.UseMapSystemV1))
            && _featureFlags.IsEnabled(nameof(MapFeatureFlags.UseSpaceHierarchyV1))
            && _featureFlags.IsEnabled(nameof(MapFeatureFlags.UseRoomInteriorV1))
            && _featureFlags.IsEnabled(nameof(MapFeatureFlags.UseRoomMapMvp));
    }

    private bool MapRoomWriteEnabled() => MapRoomReadEnabled();

    private bool RoomMarkersEnabled()
    {
        return MapRoomReadEnabled() && _featureFlags.IsEnabled(nameof(MapFeatureFlags.UseRoomMarkers));
    }

    private bool RoomPlayerViewEnabled()
    {
        return MapRoomReadEnabled() && _featureFlags.IsEnabled(nameof(MapFeatureFlags.UseRoomPlayerView));
    }

    private static string NormalizeRoomType(string? value)
    {
        return value switch
        {
            RoomTypeIds.Hall => RoomTypeIds.Hall,
            RoomTypeIds.Corridor => RoomTypeIds.Corridor,
            RoomTypeIds.Chamber => RoomTypeIds.Chamber,
            RoomTypeIds.Entrance => RoomTypeIds.Entrance,
            RoomTypeIds.Exit => RoomTypeIds.Exit,
            RoomTypeIds.Storage => RoomTypeIds.Storage,
            RoomTypeIds.LivingSpace => RoomTypeIds.LivingSpace,
            RoomTypeIds.Workshop => RoomTypeIds.Workshop,
            RoomTypeIds.Laboratory => RoomTypeIds.Laboratory,
            RoomTypeIds.Office => RoomTypeIds.Office,
            RoomTypeIds.Barracks => RoomTypeIds.Barracks,
            RoomTypeIds.Hangar => RoomTypeIds.Hangar,
            RoomTypeIds.EngineRoom => RoomTypeIds.EngineRoom,
            RoomTypeIds.Bridge => RoomTypeIds.Bridge,
            RoomTypeIds.DungeonRoom => RoomTypeIds.DungeonRoom,
            RoomTypeIds.Cave => RoomTypeIds.Cave,
            RoomTypeIds.Ruin => RoomTypeIds.Ruin,
            RoomTypeIds.Custom => RoomTypeIds.Custom,
            _ => RoomTypeIds.Room
        };
    }

    private static string NormalizeInteriorType(string? value)
    {
        return value switch
        {
            InteriorTypeIds.Dungeon => InteriorTypeIds.Dungeon,
            InteriorTypeIds.Ship => InteriorTypeIds.Ship,
            InteriorTypeIds.Airship => InteriorTypeIds.Airship,
            InteriorTypeIds.Vehicle => InteriorTypeIds.Vehicle,
            InteriorTypeIds.Station => InteriorTypeIds.Station,
            InteriorTypeIds.Cave => InteriorTypeIds.Cave,
            InteriorTypeIds.Camp => InteriorTypeIds.Camp,
            InteriorTypeIds.Fortification => InteriorTypeIds.Fortification,
            InteriorTypeIds.Underground => InteriorTypeIds.Underground,
            InteriorTypeIds.Custom => InteriorTypeIds.Custom,
            _ => InteriorTypeIds.Building
        };
    }

    private static string NormalizeRoomLayoutMode(string? value)
    {
        return value switch
        {
            RoomLayoutModeIds.None => RoomLayoutModeIds.None,
            RoomLayoutModeIds.SimpleRect => RoomLayoutModeIds.SimpleRect,
            RoomLayoutModeIds.ImportedLater => RoomLayoutModeIds.ImportedLater,
            RoomLayoutModeIds.GeneratedLater => RoomLayoutModeIds.GeneratedLater,
            _ => RoomLayoutModeIds.Grid
        };
    }

    private static string NormalizeRoomMarkerType(string? value)
    {
        return value switch
        {
            MapMarkerTypeIds.Character => MapMarkerTypeIds.Character,
            MapMarkerTypeIds.PlayerCharacter => MapMarkerTypeIds.PlayerCharacter,
            MapMarkerTypeIds.Npc => MapMarkerTypeIds.Npc,
            MapMarkerTypeIds.Companion => MapMarkerTypeIds.Companion,
            MapMarkerTypeIds.Enemy => MapMarkerTypeIds.Enemy,
            MapMarkerTypeIds.Item => MapMarkerTypeIds.Item,
            MapMarkerTypeIds.Door => MapMarkerTypeIds.Door,
            MapMarkerTypeIds.Window => MapMarkerTypeIds.Window,
            MapMarkerTypeIds.Container => MapMarkerTypeIds.Container,
            MapMarkerTypeIds.Furniture => MapMarkerTypeIds.Furniture,
            MapMarkerTypeIds.Trap => MapMarkerTypeIds.Trap,
            MapMarkerTypeIds.Hazard => MapMarkerTypeIds.Hazard,
            MapMarkerTypeIds.Objective => MapMarkerTypeIds.Objective,
            MapMarkerTypeIds.PointOfInterest => MapMarkerTypeIds.PointOfInterest,
            MapMarkerTypeIds.Cover => MapMarkerTypeIds.Cover,
            MapMarkerTypeIds.Exit => MapMarkerTypeIds.Exit,
            MapMarkerTypeIds.Entrance => MapMarkerTypeIds.Entrance,
            _ => MapMarkerTypeIds.Custom
        };
    }

    private static bool IsRoomVisibleForPlayer(RoomInteriorState room)
    {
        if (room == null || room.Deleted || room.Archived || room.IsArchived) return false;
        if (!room.IsPlayerVisible) return false;
        if (string.Equals(room.VisibilityMode, MapVisibilityModes.GmOnly, StringComparison.OrdinalIgnoreCase)) return false;
        if (string.Equals(room.VisibilityMode, MapVisibilityModes.Hidden, StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }

    private static bool IsRoomMarkerVisibleForPlayer(MapMarkerState marker)
    {
        if (marker == null || marker.Deleted || marker.Archived) return false;
        if (!marker.IsPlayerVisible) return false;
        if (string.Equals(marker.VisibilityMode, MapVisibilityModes.GmOnly, StringComparison.OrdinalIgnoreCase)) return false;
        if (string.Equals(marker.VisibilityMode, MapVisibilityModes.Hidden, StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }

    private static double? ComputeArea(double? widthMeters, double? heightMeters)
    {
        if (!widthMeters.HasValue || !heightMeters.HasValue) return null;
        if (widthMeters.Value <= 0 || heightMeters.Value <= 0) return null;
        return Math.Round(widthMeters.Value * heightMeters.Value, 2);
    }

    private static Dictionary<string, object> RoomPayload(RoomInteriorState room)
    {
        return new Dictionary<string, object>
        {
            { "roomId", room.Id },
            { "campaignId", room.CampaignId ?? string.Empty },
            { "ruleSetId", room.RuleSetId ?? string.Empty },
            { "spaceNodeId", room.SpaceNodeId ?? string.Empty },
            { "parentSpaceNodeId", room.ParentSpaceNodeId ?? string.Empty },
            { "parentLocationId", room.ParentLocationId ?? string.Empty },
            { "parentSceneMapId", room.ParentSceneMapId ?? string.Empty },
            { "parentWorldMapId", room.ParentWorldMapId ?? string.Empty },
            { "name", room.Name ?? string.Empty },
            { "description", room.Description ?? string.Empty },
            { "roomType", room.RoomType ?? RoomTypeIds.Room },
            { "interiorType", room.InteriorType ?? InteriorTypeIds.Building },
            { "widthMeters", room.WidthMeters ?? 0d },
            { "heightMeters", room.HeightMeters ?? 0d },
            { "areaSquareMeters", room.AreaSquareMeters ?? 0d },
            { "gridCellSizeMeters", room.GridCellSizeMeters },
            { "layoutMode", room.LayoutMode ?? RoomLayoutModeIds.Grid },
            { "visibilityMode", room.VisibilityMode ?? MapVisibilityModes.Party },
            { "isPlayerVisible", room.IsPlayerVisible },
            { "publicNotes", room.PublicNotes ?? string.Empty },
            { "gmNotes", room.GMNotes ?? string.Empty },
            { "isArchived", room.IsArchived || room.Archived },
            { "updatedAtUtc", room.UpdatedAtUtc }
        };
    }

    private static Dictionary<string, object> RoomListItemPayload(RoomInteriorState room)
    {
        return new Dictionary<string, object>
        {
            { "roomId", room.Id },
            { "name", room.Name ?? string.Empty },
            { "roomType", room.RoomType ?? RoomTypeIds.Room },
            { "interiorType", room.InteriorType ?? InteriorTypeIds.Building },
            { "widthMeters", room.WidthMeters ?? 0d },
            { "heightMeters", room.HeightMeters ?? 0d },
            { "parentLocationId", room.ParentLocationId ?? string.Empty },
            { "parentSceneMapId", room.ParentSceneMapId ?? string.Empty },
            { "isPlayerVisible", room.IsPlayerVisible },
            { "visibilityMode", room.VisibilityMode ?? MapVisibilityModes.Party },
            { "updatedAtUtc", room.UpdatedAtUtc }
        };
    }

    private static Dictionary<string, object> RoomMarkerPayload(MapMarkerState marker)
    {
        return new Dictionary<string, object>
        {
            { "markerId", marker.Id },
            { "mapId", marker.MapId ?? string.Empty },
            { "name", marker.Name ?? string.Empty },
            { "markerType", marker.MarkerType ?? MapMarkerTypeIds.Custom },
            { "x", marker.X },
            { "y", marker.Y },
            { "isPlayerVisible", marker.IsPlayerVisible },
            { "visibilityMode", marker.VisibilityMode ?? MapVisibilityModes.Party },
            { "linkedEntityType", marker.LinkedEntityType ?? string.Empty },
            { "linkedEntityId", marker.LinkedEntityId ?? string.Empty },
            { "linkedEntityDisplayName", marker.LinkedEntityDisplayName ?? string.Empty },
            { "linkedEntityPublicLabel", marker.LinkedEntityPublicLabel ?? string.Empty },
            { "publicNotes", marker.PublicNotes ?? string.Empty },
            { "gmNotes", marker.GMNotes ?? string.Empty }
        };
    }

    private static Dictionary<string, object> PlayerRoomMarkerPayload(MapMarkerState marker)
    {
        return new Dictionary<string, object>
        {
            { "markerId", marker.Id },
            { "name", marker.Name ?? string.Empty },
            { "markerType", marker.MarkerType ?? MapMarkerTypeIds.Custom },
            { "x", marker.X },
            { "y", marker.Y },
            { "cardTitle", marker.CardTitle ?? string.Empty },
            { "cardDescription", marker.CardDescription ?? string.Empty },
            { "isVisible", true }
        };
    }
}
