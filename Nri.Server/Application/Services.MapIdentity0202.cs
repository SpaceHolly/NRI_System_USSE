using System;
using System.Collections.Generic;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;
using Nri.Shared.Utilities;

namespace Nri.Server.Application;

public partial class ServiceHub
{
    public ResponseEnvelope MapIdentityResolve0202(CommandContext context)
    {
        RequireAdmin(context);
        var mapId = RequireLength(PayloadReader.GetString(context.Request.Payload, "mapId"), 1, 128, "mapId");
        var resolution = _mapIdentityResolver.ResolveSceneMap(mapId, PayloadReader.GetBool(context.Request.Payload, "includeArchived"));
        if (!resolution.IsResolved) return MapIdentityError0202(resolution);
        return Ok("Map identity resolved.", new Dictionary<string, object>
        {
            ["suppliedMapId"] = resolution.SuppliedMapId,
            ["canonicalMapId"] = resolution.CanonicalMapId,
            ["legacyMapId"] = resolution.LegacyMapId,
            ["mapType"] = resolution.CanonicalMap?.MapType ?? string.Empty,
            ["hasCompatibilityProjection"] = resolution.CompatibilityProjection != null,
            ["authoritativeCollection"] = "map_states",
            ["compatibilityCollection"] = MapIdentityAdapter0202.SceneProjectionCollectionName,
            ["mappingCollection"] = MapIdentityAdapter0202.MappingCollectionName
        });
    }

    private ResponseEnvelope MapIdentityError0202(MapIdentityResolution0202 resolution)
    {
        _logger.Admin($"map.identity.0202.rejected suppliedMapId={resolution.SuppliedMapId} status={resolution.Status}");
        if (resolution.Status == MapIdentityResolutionStatus0202.NotFound)
            return Error(resolution.Message, ResponseStatus.NotFound, ErrorCode.NotFound);
        if (resolution.Status == MapIdentityResolutionStatus0202.Archived)
            return Error(resolution.Message, ResponseStatus.Conflict, ErrorCode.Conflict);
        return Error(resolution.Message, ResponseStatus.Conflict, ErrorCode.Conflict);
    }

    private MapCanvasState CanonicalSceneFromProjection0202(MongoDB.Bson.BsonDocument projection, string canonicalMapId)
    {
        var existing = _repositories.MapCanvases.GetByIdAsync(canonicalMapId).GetAwaiter().GetResult();
        var map = existing ?? new MapCanvasState
        {
            Id = canonicalMapId,
            MapType = MapTypeIds.Scene,
            CreatedAtUtc = DateTime.UtcNow
        };
        map.CampaignId = GetDocString(projection, "CampaignId", "dev-campaign-core");
        map.RuleSetId = GetDocString(projection, "RuleSetId", "fantasy_nri_default");
        map.Name = GetDocString(projection, "DisplayName");
        map.Description = GetDocString(projection, "Description");
        map.WidthMeters = GetDocInt(projection, "WidthMeters", MapRuntimeValidation.SceneDefaultSizeMeters);
        map.HeightMeters = GetDocInt(projection, "HeightMeters", MapRuntimeValidation.SceneDefaultSizeMeters);
        map.GridCellSizeMeters = GetDocInt(projection, "GridSizeMeters", 25);
        map.CoordinateMode = MapCoordinateModes.MetersFromOrigin;
        map.BackgroundMode = MapBackgroundModes.None;
        map.IsArchived = GetDocBool(projection, "IsArchived");
        map.Archived = map.IsArchived;
        map.UpdatedAtUtc = DateTime.UtcNow;
        map.ExtraData ??= new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        map.ExtraData["showGrid"] = GetDocBool(projection, "ShowGrid");
        map.ExtraData["showCoordinates"] = GetDocBool(projection, "ShowCoordinates");
        return map;
    }

    private MapCanvasState CanonicalWorldFromProjection0202(MongoDB.Bson.BsonDocument projection, string canonicalMapId)
    {
        var existing = _repositories.MapCanvases.GetByIdAsync(canonicalMapId).GetAwaiter().GetResult();
        var map = existing ?? new MapCanvasState { Id = canonicalMapId, MapType = MapTypeIds.WorldMap, CreatedAtUtc = DateTime.UtcNow };
        map.CampaignId = GetDocString(projection, "CampaignId", "dev-campaign-core");
        map.RuleSetId = GetDocString(projection, "RuleSetId", "fantasy_nri_default");
        map.SpaceNodeId = GetDocString(projection, "SpaceNodeId", GetDocString(projection, "WorldId"));
        map.Name = GetDocString(projection, "DisplayName");
        map.Description = GetDocString(projection, "Description");
        map.WidthMeters = GetDocInt(projection, "WidthUnits", 5000);
        map.HeightMeters = GetDocInt(projection, "HeightUnits", 3000);
        map.GridCellSizeMeters = GetDocInt(projection, "GridSizeUnits", 250);
        map.CoordinateMode = WorldMapCoordinateModeIds.WorldUnits;
        map.BackgroundMode = MapBackgroundModes.None;
        map.IsArchived = GetDocBool(projection, "IsArchived");
        map.Archived = map.IsArchived;
        map.UpdatedAtUtc = DateTime.UtcNow;
        map.ExtraData ??= new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        map.ExtraData["widthUnits"] = map.WidthMeters;
        map.ExtraData["heightUnits"] = map.HeightMeters;
        map.ExtraData["gridSizeUnits"] = map.GridCellSizeMeters;
        return map;
    }
}
