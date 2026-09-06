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
    private const string WorldMap0161DefaultMapId = "world_map_default_0161";
    private const string WorldMap0161DefaultWorldId = "dev_world_0161";
    private const string WorldMap0161DefaultSessionId = "dev_session_0161";
    private const string WorldMap0161DefinitionsCollection = "world_map_definitions";
    private const string WorldMap0161MarkersCollection = "world_map_markers";
    private const string WorldMap0161SessionStatesCollection = "session_world_map_states";

    public ResponseEnvelope WorldMapAdminList0161(CommandContext context)
    {
        RequireAdmin(context);
        if (!WorldMapViewerAdminEnabled())
            return WorldMapViewerDisabled(context.Request.Command);

        EnsureWorldMap0161Indexes();
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var includeArchived = PayloadReader.GetBool(payload, "includeArchived");
        var filter = includeArchived
            ? Builders<BsonDocument>.Filter.Empty
            : Builders<BsonDocument>.Filter.Ne("IsArchived", true);
        var maps = WorldMap0161Definitions()
            .Find(filter)
            .Sort(Builders<BsonDocument>.Sort.Ascending("DisplayName"))
            .Limit(300)
            .ToList()
            .Select(doc => WorldMap0161ListPayload(doc))
            .Cast<object>()
            .ToArray();

        return Ok("World maps loaded.", new Dictionary<string, object>
        {
            ["items"] = maps,
            ["count"] = maps.Length
        });
    }

    public ResponseEnvelope WorldMapAdminCreate0161(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!WorldMapViewerAdminEnabled())
            return WorldMapViewerDisabled(context.Request.Command);

        EnsureWorldMap0161Indexes();
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var now = DateTime.UtcNow;
        var mapId = FirstNonEmptyWorld(PayloadReader.GetString(payload, "mapId"), PayloadReader.GetString(payload, "id"));
        if (string.IsNullOrWhiteSpace(mapId))
            mapId = "world_map_" + Guid.NewGuid().ToString("N");

        var displayName = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "displayName"), PayloadReader.GetString(payload, "name")), 1, 160, "displayName");
        var widthUnits = Math.Max(1, PayloadReader.GetInt(payload, "widthUnits") ?? PayloadReader.GetInt(payload, "widthCells") ?? 5000);
        var heightUnits = Math.Max(1, PayloadReader.GetInt(payload, "heightUnits") ?? PayloadReader.GetInt(payload, "heightCells") ?? 3000);
        var gridSizeUnits = Math.Max(1, PayloadReader.GetInt(payload, "gridSizeUnits") ?? PayloadReader.GetInt(payload, "gridCellSize") ?? 250);
        if (widthUnits > 100000 || heightUnits > 100000)
            return Error("world map dimensions are too large for MVP viewer", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);

        var existing = WorldMap0161Definitions().Find(IdFilter(mapId)).FirstOrDefault();
        var createdAt = existing != null && existing.TryGetValue("CreatedAtUtc", out var createdValue) && createdValue.IsValidDateTime
            ? createdValue.ToUniversalTime()
            : now;

        var doc = new BsonDocument
        {
            ["_id"] = mapId,
            ["Id"] = mapId,
            ["WorldId"] = FirstNonEmptyWorld(PayloadReader.GetString(payload, "worldId"), WorldMap0161DefaultWorldId),
            ["DisplayName"] = displayName,
            ["Description"] = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "description"), PayloadReader.GetString(payload, "publicDescription")), 0, 4096, "description"),
            ["WidthUnits"] = widthUnits,
            ["HeightUnits"] = heightUnits,
            ["UnitLabel"] = FirstNonEmptyWorld(PayloadReader.GetString(payload, "unitLabel"), "км"),
            ["GridSizeUnits"] = gridSizeUnits,
            ["BackgroundMode"] = FirstNonEmptyWorld(PayloadReader.GetString(payload, "backgroundMode"), "solid"),
            ["BackgroundColor"] = FirstNonEmptyWorld(PayloadReader.GetString(payload, "backgroundColor"), "#172033"),
            ["SchemaVersion"] = 1,
            ["IsArchived"] = false,
            ["CreatedAtUtc"] = createdAt,
            ["UpdatedAtUtc"] = now,
            ["CreatedByUserId"] = existing != null ? GetDocString(existing, "CreatedByUserId") : actor.Id,
            ["UpdatedByUserId"] = actor.Id
        };

        var canonicalId = existing == null ? mapId : GetDocString(existing, "CanonicalMapId", mapId);
        var canonical = CanonicalWorldFromProjection0202(doc, canonicalId);
        var savedCanonical = _repositories.MapCanvases.UpsertAsync(canonical).GetAwaiter().GetResult();
        doc = _mapIdentityResolver.SynchronizeWorldProjection(savedCanonical, mapId, actor.Id, doc);
        _logger.Admin($"world.map.0161.create mapId={savedCanonical.Id} legacyMapId={mapId} actor={actor.Login}");
        return Ok("World map saved.", new Dictionary<string, object>
        {
            ["mapId"] = savedCanonical.Id,
            ["map"] = WorldMap0161AdminPayload(doc, includeMarkers: true)
        });
    }

    public ResponseEnvelope WorldMapAdminUpdate0161(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!WorldMapViewerAdminEnabled())
            return WorldMapViewerDisabled(context.Request.Command);

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var mapId = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "mapId"), PayloadReader.GetString(payload, "id")), 1, 128, "mapId");
        var identity = WorldMap0161ResolveIdentity0202(mapId, actor.Id);
        if (!identity.IsResolved) return MapIdentityError0202(identity);
        var doc = identity.CompatibilityProjection!;

        if (payload.ContainsKey("displayName") || payload.ContainsKey("name"))
            doc["DisplayName"] = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "displayName"), PayloadReader.GetString(payload, "name")), 1, 160, "displayName");
        if (payload.ContainsKey("description"))
            doc["Description"] = RequireLength(PayloadReader.GetString(payload, "description"), 0, 4096, "description");
        if (payload.ContainsKey("unitLabel"))
            doc["UnitLabel"] = RequireLength(PayloadReader.GetString(payload, "unitLabel"), 1, 16, "unitLabel");
        if (payload.ContainsKey("gridSizeUnits"))
            doc["GridSizeUnits"] = Math.Max(1, PayloadReader.GetInt(payload, "gridSizeUnits") ?? GetDocInt(doc, "GridSizeUnits", 250));
        if (payload.ContainsKey("backgroundColor"))
            doc["BackgroundColor"] = RequireLength(PayloadReader.GetString(payload, "backgroundColor"), 0, 32, "backgroundColor");

        doc["UpdatedAtUtc"] = DateTime.UtcNow;
        doc["UpdatedByUserId"] = actor.Id;
        var canonical = CanonicalWorldFromProjection0202(doc, identity.CanonicalMapId);
        var savedCanonical = _repositories.MapCanvases.UpsertAsync(canonical).GetAwaiter().GetResult();
        doc = _mapIdentityResolver.SynchronizeWorldProjection(savedCanonical, identity.LegacyMapId, actor.Id, doc);
        return Ok("World map updated.", new Dictionary<string, object>
        {
            ["mapId"] = identity.CanonicalMapId,
            ["map"] = WorldMap0161AdminPayload(doc, includeMarkers: true)
        });
    }

    public ResponseEnvelope WorldMapAdminArchive0161(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!WorldMapViewerAdminEnabled())
            return WorldMapViewerDisabled(context.Request.Command);

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var mapId = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "mapId"), PayloadReader.GetString(payload, "id")), 1, 128, "mapId");
        var identity = WorldMap0161ResolveIdentity0202(mapId, actor.Id);
        if (!identity.IsResolved) return MapIdentityError0202(identity);
        var canonical = identity.CanonicalMap!;
        canonical.IsArchived = true; canonical.Archived = true; canonical.UpdatedAtUtc = DateTime.UtcNow;
        var saved = _repositories.MapCanvases.UpsertAsync(canonical).GetAwaiter().GetResult();
        _mapIdentityResolver.SynchronizeWorldProjection(saved, identity.LegacyMapId, actor.Id, identity.CompatibilityProjection);
        return Ok("World map archived.", new Dictionary<string, object> { ["mapId"] = identity.CanonicalMapId });
    }

    public ResponseEnvelope WorldMapAdminSetSessionActive0161(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!WorldMapViewerAdminEnabled())
            return WorldMapViewerDisabled(context.Request.Command);

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var sessionId = FirstNonEmptyWorld(PayloadReader.GetString(payload, "sessionId"), WorldMap0161DefaultSessionId);
        var mapId = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "mapId"), PayloadReader.GetString(payload, "activeWorldMapId")), 1, 128, "mapId");
        var identity = WorldMap0161ResolveIdentity0202(mapId, actor.Id);
        if (!identity.IsResolved) return MapIdentityError0202(identity);
        mapId = identity.CanonicalMapId;
        var map = identity.CompatibilityProjection!;

        var now = DateTime.UtcNow;
        var doc = new BsonDocument
        {
            ["_id"] = sessionId,
            ["SessionId"] = sessionId,
            ["CampaignId"] = FirstNonEmptyWorld(PayloadReader.GetString(payload, "campaignId"), "dev-campaign-core"),
            ["ActiveWorldMapId"] = mapId,
            ["ActiveWorldMapName"] = GetDocString(map, "DisplayName"),
            ["UpdatedAtUtc"] = now,
            ["UpdatedByUserId"] = actor.Id
        };

        WorldMap0161SessionStates().ReplaceOne(Builders<BsonDocument>.Filter.Eq("SessionId", sessionId), doc, new ReplaceOptions { IsUpsert = true });
        return Ok("Active world map selected.", new Dictionary<string, object>
        {
            ["sessionId"] = sessionId,
            ["activeWorldMapId"] = mapId,
            ["activeWorldMapName"] = GetDocString(map, "DisplayName"),
            ["updatedAtUtc"] = now
        });
    }

    public ResponseEnvelope WorldMapAdminAddMarker0161(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!WorldMapViewerAdminEnabled())
            return WorldMapViewerDisabled(context.Request.Command);

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var mapId = RequireLength(PayloadReader.GetString(payload, "mapId"), 1, 128, "mapId");
        var identity = WorldMap0161ResolveIdentity0202(mapId, actor.Id);
        if (!identity.IsResolved) return MapIdentityError0202(identity);
        mapId = identity.CanonicalMapId;
        var map = identity.CompatibilityProjection!;

        var markerId = FirstNonEmptyWorld(PayloadReader.GetString(payload, "markerId"), PayloadReader.GetString(payload, "id"));
        if (string.IsNullOrWhiteSpace(markerId))
            markerId = "world_map_marker_" + Guid.NewGuid().ToString("N");

        var now = DateTime.UtcNow;
        var doc = BuildWorldMap0161MarkerDoc(map, payload, markerId, actor.Id, now, existing: null);
        var validation = ValidateWorldMap0161Marker(map, doc);
        if (validation != null) return validation;

        WorldMap0161Markers().ReplaceOne(IdFilter(markerId), doc, new ReplaceOptions { IsUpsert = true });
        return Ok("World map marker added.", new Dictionary<string, object>
        {
            ["markerId"] = markerId,
            ["marker"] = WorldMap0161MarkerPayload(map, doc, admin: true)
        });
    }

    public ResponseEnvelope WorldMapAdminUpdateMarker0161(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!WorldMapViewerAdminEnabled())
            return WorldMapViewerDisabled(context.Request.Command);

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var markerId = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "markerId"), PayloadReader.GetString(payload, "id")), 1, 128, "markerId");
        var existing = WorldMap0161Markers().Find(ActiveIdFilter(markerId)).FirstOrDefault();
        if (existing == null)
            return Error("world map marker not found", ResponseStatus.NotFound, ErrorCode.NotFound);
        var mapId = GetDocString(existing, "WorldMapId");
        var identity = WorldMap0161ResolveIdentity0202(mapId, actor.Id);
        if (!identity.IsResolved) return MapIdentityError0202(identity);
        var map = identity.CompatibilityProjection!;

        var doc = BuildWorldMap0161MarkerDoc(map, payload, markerId, actor.Id, DateTime.UtcNow, existing);
        var validation = ValidateWorldMap0161Marker(map, doc);
        if (validation != null) return validation;

        WorldMap0161Markers().ReplaceOne(IdFilter(markerId), doc, new ReplaceOptions { IsUpsert = true });
        return Ok("World map marker updated.", new Dictionary<string, object>
        {
            ["markerId"] = markerId,
            ["marker"] = WorldMap0161MarkerPayload(map, doc, admin: true)
        });
    }

    public ResponseEnvelope WorldMapAdminArchiveMarker0161(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!WorldMapViewerAdminEnabled())
            return WorldMapViewerDisabled(context.Request.Command);

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var markerId = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "markerId"), PayloadReader.GetString(payload, "id")), 1, 128, "markerId");
        var update = Builders<BsonDocument>.Update
            .Set("IsArchived", true)
            .Set("UpdatedAtUtc", DateTime.UtcNow)
            .Set("UpdatedByUserId", actor.Id);
        var result = WorldMap0161Markers().UpdateOne(ActiveIdFilter(markerId), update);
        if (result.MatchedCount == 0)
            return Error("world map marker not found", ResponseStatus.NotFound, ErrorCode.NotFound);
        return Ok("World map marker archived.", new Dictionary<string, object> { ["markerId"] = markerId });
    }

    public ResponseEnvelope WorldMapPlayerGetSessionActive0161(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        if (!WorldMapViewerPlayerEnabled())
        {
            _logger.Debug($"world.map.0161.player.disabled user={actor.Login}");
            return Error("World Map player view is disabled by feature flags.", ResponseStatus.Forbidden, ErrorCode.Forbidden);
        }

        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var sessionId = FirstNonEmptyWorld(PayloadReader.GetString(payload, "sessionId"), WorldMap0161DefaultSessionId);
        var campaignId = PayloadReader.GetString(payload, "campaignId");
        var state = WorldMap0161SessionStates().Find(Builders<BsonDocument>.Filter.Eq("SessionId", sessionId)).FirstOrDefault();
        if (state == null && !string.IsNullOrWhiteSpace(campaignId))
        {
            state = WorldMap0161SessionStates()
                .Find(Builders<BsonDocument>.Filter.Eq("CampaignId", campaignId))
                .Sort(Builders<BsonDocument>.Sort.Descending("UpdatedAtUtc"))
                .FirstOrDefault();
        }

        if (state == null)
            return Error("GM has not selected an active world map.", ResponseStatus.NotFound, ErrorCode.NotFound);

        var mapId = GetDocString(state, "ActiveWorldMapId");
        var identity = WorldMap0161ResolveIdentity0202(mapId, actor.Id);
        if (!identity.IsResolved) return Error("Active world map is unavailable.", ResponseStatus.NotFound, ErrorCode.NotFound);
        var map = identity.CompatibilityProjection!;

        return Ok("Active world map loaded.", new Dictionary<string, object>
        {
            ["hasActiveMap"] = true,
            ["sessionId"] = sessionId,
            ["map"] = WorldMap0161PlayerPayload(map),
            ["builtAtUtc"] = DateTime.UtcNow
        });
    }

    private bool TryWorldMapAdminGet0161(CommandContext context, out ResponseEnvelope response)
    {
        response = null!;
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var mapId = FirstNonEmptyWorld(PayloadReader.GetString(payload, "mapId"), PayloadReader.GetString(payload, "id"));
        if (string.IsNullOrWhiteSpace(mapId))
            return false;

        var identity = WorldMap0161ResolveIdentity0202(mapId, "system-map-read");
        var map = identity.CompatibilityProjection;
        if (!identity.IsResolved || map == null)
            return false;

        response = Ok("World map loaded.", WorldMap0161AdminPayload(map, includeMarkers: true));
        return true;
    }

    private Dictionary<string, object> WorldMap0161AdminPayload(BsonDocument map, bool includeMarkers)
    {
        var payload = new Dictionary<string, object>
        {
            ["map"] = WorldMap0161MapPayload(map, admin: true),
            ["markers"] = includeMarkers
                ? WorldMap0161MarkerDocs(WorldMap0161CanonicalMapId(map), includeHidden: true)
                    .Select(marker => WorldMap0161MarkerPayload(map, marker, admin: true))
                    .Cast<object>()
                    .ToArray()
                : Array.Empty<object>(),
            ["markerCount"] = includeMarkers ? WorldMap0161MarkerDocs(WorldMap0161CanonicalMapId(map), includeHidden: true).Count : 0,
            ["tokens"] = MapToken0163PayloadsForMap(MapToken0163KindWorld, WorldMap0161CanonicalMapId(map), admin: true).Cast<object>().ToArray(),
            ["tokenCount"] = MapToken0163PayloadsForMap(MapToken0163KindWorld, WorldMap0161CanonicalMapId(map), admin: true).Length,
            ["layers"] = Array.Empty<object>(),
            ["legends"] = BuildWorldMap0161LegendsPayload(),
            ["sourceCollections"] = new object[] { WorldMap0161DefinitionsCollection, WorldMap0161MarkersCollection, WorldMap0161SessionStatesCollection, MapToken0163Collection }
        };
        return payload;
    }

    private Dictionary<string, object> WorldMap0161PlayerPayload(BsonDocument map)
    {
        var markers = WorldMap0161MarkerDocs(WorldMap0161CanonicalMapId(map), includeHidden: false)
            .Where(marker => string.Equals(GetDocString(marker, "Visibility"), "PlayerVisible", StringComparison.OrdinalIgnoreCase))
            .Select(marker => WorldMap0161MarkerPayload(map, marker, admin: false))
            .Cast<object>()
            .ToArray();

        var mapPayload = WorldMap0161MapPayload(map, admin: false);
        mapPayload["markers"] = markers;
        mapPayload["tokens"] = MapToken0163PayloadsForMap(MapToken0163KindWorld, WorldMap0161CanonicalMapId(map), admin: false).Cast<object>().ToArray();
        mapPayload["layers"] = Array.Empty<object>();
        mapPayload["legends"] = BuildWorldMap0161LegendsPayload();
        return mapPayload;
    }

    private Dictionary<string, object> WorldMap0161ListPayload(BsonDocument map)
    {
        var markerCount = WorldMap0161MarkerDocs(WorldMap0161CanonicalMapId(map), includeHidden: true).Count;
        var payload = WorldMap0161MapPayload(map, admin: true);
        payload["markerCount"] = markerCount;
        return payload;
    }

    private Dictionary<string, object> WorldMap0161MapPayload(BsonDocument map, bool admin)
    {
        var widthUnits = GetDocInt(map, "WidthUnits", 5000);
        var heightUnits = GetDocInt(map, "HeightUnits", 3000);
        var gridSize = Math.Max(1, GetDocInt(map, "GridSizeUnits", 250));
        var widthCells = Math.Max(1, (int)Math.Ceiling(widthUnits / (double)gridSize));
        var heightCells = Math.Max(1, (int)Math.Ceiling(heightUnits / (double)gridSize));
        var payload = new Dictionary<string, object>
        {
            ["mapId"] = WorldMap0161CanonicalMapId(map),
            ["name"] = GetDocString(map, "DisplayName"),
            ["displayName"] = GetDocString(map, "DisplayName"),
            ["description"] = GetDocString(map, "Description"),
            ["widthUnits"] = widthUnits,
            ["heightUnits"] = heightUnits,
            ["unitLabel"] = GetDocString(map, "UnitLabel", "км"),
            ["gridSizeUnits"] = gridSize,
            ["widthCells"] = widthCells,
            ["heightCells"] = heightCells,
            ["cellSizeKm"] = gridSize,
            ["projectionMode"] = WorldMapProjectionModeIds.FlatGrid,
            ["coordinateMode"] = WorldMapCoordinateModeIds.WorldUnits,
            ["backgroundMode"] = GetDocString(map, "BackgroundMode", "solid"),
            ["backgroundColor"] = GetDocString(map, "BackgroundColor", "#172033"),
            ["isArchived"] = GetDocBool(map, "IsArchived"),
            ["updatedAtUtc"] = GetDocDate(map, "UpdatedAtUtc")
        };
        if (admin)
        {
            payload["id"] = WorldMap0161CanonicalMapId(map);
            payload["worldId"] = GetDocString(map, "WorldId");
            payload["adminDiagnostics"] = $"source={WorldMap0161DefinitionsCollection}";
        }
        return payload;
    }

    private Dictionary<string, object> WorldMap0161MarkerPayload(BsonDocument map, BsonDocument marker, bool admin)
    {
        var widthUnits = Math.Max(1, GetDocInt(map, "WidthUnits", 5000));
        var heightUnits = Math.Max(1, GetDocInt(map, "HeightUnits", 3000));
        var gridSize = Math.Max(1, GetDocInt(map, "GridSizeUnits", 250));
        var x = GetDocDouble(marker, "X", 0d);
        var y = GetDocDouble(marker, "Y", 0d);
        var payload = new Dictionary<string, object>
        {
            ["markerId"] = GetDocString(marker, "Id"),
            ["name"] = GetDocString(marker, "DisplayName"),
            ["displayName"] = GetDocString(marker, "DisplayName"),
            ["markerType"] = GetDocString(marker, "MarkerType", "custom"),
            ["x"] = x,
            ["y"] = y,
            ["cellX"] = (int)Math.Round(x / gridSize),
            ["cellY"] = (int)Math.Round(y / gridSize),
            ["xNormalized"] = Math.Max(0d, Math.Min(1d, x / widthUnits)),
            ["yNormalized"] = Math.Max(0d, Math.Min(1d, y / heightUnits)),
            ["visibility"] = GetDocString(marker, "Visibility", "Hidden"),
            ["visibilityMode"] = GetDocString(marker, "Visibility", "Hidden"),
            ["isPlayerVisible"] = string.Equals(GetDocString(marker, "Visibility"), "PlayerVisible", StringComparison.OrdinalIgnoreCase),
            ["publicNotes"] = GetDocString(marker, "DescriptionPlayer"),
            ["cardTitle"] = GetDocString(marker, "DisplayName"),
            ["cardDescription"] = GetDocString(marker, "DescriptionPlayer"),
            ["description"] = GetDocString(marker, "DescriptionPlayer"),
            ["isArchived"] = GetDocBool(marker, "IsArchived"),
            ["updatedAtUtc"] = GetDocDate(marker, "UpdatedAtUtc")
        };

        if (admin)
        {
            payload["id"] = GetDocString(marker, "Id");
            payload["mapId"] = GetDocString(marker, "WorldMapId");
            payload["worldMapId"] = GetDocString(marker, "WorldMapId");
            payload["gmNotes"] = GetDocString(marker, "DescriptionGm");
            payload["descriptionGm"] = GetDocString(marker, "DescriptionGm");
        }

        return payload;
    }

    private object[] BuildWorldMap0161LegendsPayload()
    {
        return new object[]
        {
            new Dictionary<string, object>
            {
                ["layerType"] = WorldMapLayerTypeIds.Marker,
                ["entries"] = new object[]
                {
                    new Dictionary<string, object> { ["key"] = "capital", ["label"] = "Столица", ["colorKey"] = "accent" },
                    new Dictionary<string, object> { ["key"] = "location", ["label"] = "Локация", ["colorKey"] = "success" },
                    new Dictionary<string, object> { ["key"] = "ruin", ["label"] = "Руины", ["colorKey"] = "warning" },
                    new Dictionary<string, object> { ["key"] = "camp", ["label"] = "Лагерь", ["colorKey"] = "danger" }
                }
            }
        };
    }

    private BsonDocument BuildWorldMap0161MarkerDoc(BsonDocument map, IDictionary<string, object> payload, string markerId, string actorUserId, DateTime now, BsonDocument? existing)
    {
        var gridSize = Math.Max(1, GetDocInt(map, "GridSizeUnits", 250));
        var x = PayloadReader.GetDouble(payload, "x")
            ?? PayloadReader.GetDouble(payload, "X")
            ?? ((PayloadReader.GetInt(payload, "cellX") ?? 0) * gridSize);
        var y = PayloadReader.GetDouble(payload, "y")
            ?? PayloadReader.GetDouble(payload, "Y")
            ?? ((PayloadReader.GetInt(payload, "cellY") ?? 0) * gridSize);
        var visibility = NormalizeWorldMap0161Visibility(FirstNonEmptyWorld(PayloadReader.GetString(payload, "visibility"), PayloadReader.GetString(payload, "visibilityMode"), PayloadReader.GetBool(payload, "isPlayerVisible") ? "PlayerVisible" : "Hidden"));

        var doc = existing != null ? new BsonDocument(existing) : new BsonDocument
        {
            ["_id"] = markerId,
            ["Id"] = markerId,
            ["CreatedAtUtc"] = now,
            ["CreatedByUserId"] = actorUserId
        };

        doc["Id"] = markerId;
        doc["WorldMapId"] = WorldMap0161CanonicalMapId(map);
        doc["DisplayName"] = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "displayName"), PayloadReader.GetString(payload, "name"), existing == null ? "Маркер" : GetDocString(existing, "DisplayName")), 1, 160, "displayName");
        doc["DescriptionPlayer"] = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "descriptionPlayer"), PayloadReader.GetString(payload, "publicNotes"), PayloadReader.GetString(payload, "cardDescription"), existing == null ? string.Empty : GetDocString(existing, "DescriptionPlayer")), 0, 4096, "descriptionPlayer");
        doc["DescriptionGm"] = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "descriptionGm"), PayloadReader.GetString(payload, "gmNotes"), existing == null ? string.Empty : GetDocString(existing, "DescriptionGm")), 0, 4096, "descriptionGm");
        doc["MarkerType"] = FirstNonEmptyWorld(PayloadReader.GetString(payload, "markerType"), existing == null ? "custom" : GetDocString(existing, "MarkerType", "custom"));
        doc["X"] = x;
        doc["Y"] = y;
        doc["Visibility"] = visibility;
        doc["IsArchived"] = false;
        doc["UpdatedAtUtc"] = now;
        doc["UpdatedByUserId"] = actorUserId;
        return doc;
    }

    private ResponseEnvelope? ValidateWorldMap0161Marker(BsonDocument map, BsonDocument marker)
    {
        var x = GetDocDouble(marker, "X", 0d);
        var y = GetDocDouble(marker, "Y", 0d);
        var width = GetDocInt(map, "WidthUnits", 5000);
        var height = GetDocInt(map, "HeightUnits", 3000);
        if (x < 0 || y < 0 || x > width || y > height)
            return Error("marker coordinates are outside world map bounds", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);
        return null;
    }

    private List<BsonDocument> WorldMap0161MarkerDocs(string mapId, bool includeHidden)
    {
        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("WorldMapId", mapId),
            Builders<BsonDocument>.Filter.Ne("IsArchived", true));
        if (!includeHidden)
        {
            filter = Builders<BsonDocument>.Filter.And(
                filter,
                Builders<BsonDocument>.Filter.Eq("Visibility", "PlayerVisible"));
        }

        return WorldMap0161Markers()
            .Find(filter)
            .Sort(Builders<BsonDocument>.Sort.Ascending("DisplayName"))
            .ToList();
    }

    private void EnsureWorldMap0161Indexes()
    {
        WorldMap0161Definitions().Indexes.CreateMany(new[]
        {
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("Id")),
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("WorldId")),
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("IsArchived"))
        });
        WorldMap0161Markers().Indexes.CreateMany(new[]
        {
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("WorldMapId")),
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("Visibility")),
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("IsArchived"))
        });
        WorldMap0161SessionStates().Indexes.CreateMany(new[]
        {
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("SessionId")),
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("CampaignId")),
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("ActiveWorldMapId"))
        });
    }

    private MapIdentityResolution0202 WorldMap0161ResolveIdentity0202(string mapId, string actorUserId, bool includeArchived = false)
    {
        var resolution = _mapIdentityResolver.ResolveWorldMap(mapId, includeArchived);
        if (resolution.Status != MapIdentityResolutionStatus0202.NotFound) return resolution;
        var legacy = WorldMap0161Definitions().Find(IdFilter(mapId)).FirstOrDefault();
        if (legacy == null) return resolution;
        var canonicalId = GetDocString(legacy, "CanonicalMapId", GetDocString(legacy, "Id", mapId));
        var canonical = CanonicalWorldFromProjection0202(legacy, canonicalId);
        var saved = _repositories.MapCanvases.UpsertAsync(canonical).GetAwaiter().GetResult();
        _mapIdentityResolver.SynchronizeWorldProjection(saved, GetDocString(legacy, "Id", mapId), actorUserId, legacy);
        return _mapIdentityResolver.ResolveWorldMap(mapId, includeArchived);
    }

    private static string WorldMap0161CanonicalMapId(BsonDocument map)
        => GetDocString(map, "CanonicalMapId", GetDocString(map, "Id"));

    private IMongoCollection<BsonDocument> WorldMap0161Definitions() => _mongo.Database.GetCollection<BsonDocument>(WorldMap0161DefinitionsCollection);
    private IMongoCollection<BsonDocument> WorldMap0161Markers() => _mongo.Database.GetCollection<BsonDocument>(WorldMap0161MarkersCollection);
    private IMongoCollection<BsonDocument> WorldMap0161SessionStates() => _mongo.Database.GetCollection<BsonDocument>(WorldMap0161SessionStatesCollection);

    private static FilterDefinition<BsonDocument> IdFilter(string id)
        => Builders<BsonDocument>.Filter.Or(Builders<BsonDocument>.Filter.Eq("_id", id), Builders<BsonDocument>.Filter.Eq("Id", id));

    private static FilterDefinition<BsonDocument> ActiveIdFilter(string id)
        => Builders<BsonDocument>.Filter.And(IdFilter(id), Builders<BsonDocument>.Filter.Ne("IsArchived", true));

    private static string NormalizeWorldMap0161Visibility(string? value)
    {
        var input = (value ?? string.Empty).Trim().ToLowerInvariant();
        return input switch
        {
            "playervisible" or "player_visible" or "public" or "party" => "PlayerVisible",
            "gmonly" or "gm_only" or "gm" => "GmOnly",
            "hidden" or "server_only" => "Hidden",
            _ => "Hidden"
        };
    }

    private static string GetDocString(BsonDocument doc, string name, string fallback = "")
    {
        return doc.TryGetValue(name, out var value) && !value.IsBsonNull ? value.ToString() : fallback;
    }

    private static int GetDocInt(BsonDocument doc, string name, int fallback)
    {
        if (!doc.TryGetValue(name, out var value) || value.IsBsonNull) return fallback;
        if (value.IsInt32) return value.AsInt32;
        if (value.IsInt64) return (int)value.AsInt64;
        return int.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
    }

    private static double GetDocDouble(BsonDocument doc, string name, double fallback)
    {
        if (!doc.TryGetValue(name, out var value) || value.IsBsonNull) return fallback;
        if (value.IsDouble) return value.AsDouble;
        if (value.IsInt32) return value.AsInt32;
        if (value.IsInt64) return value.AsInt64;
        return double.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
    }

    private static bool GetDocBool(BsonDocument doc, string name)
    {
        if (!doc.TryGetValue(name, out var value) || value.IsBsonNull) return false;
        if (value.IsBoolean) return value.AsBoolean;
        return bool.TryParse(value.ToString(), out var parsed) && parsed;
    }

    private static DateTime GetDocDate(BsonDocument doc, string name)
    {
        if (!doc.TryGetValue(name, out var value) || value.IsBsonNull) return DateTime.MinValue;
        return value.IsValidDateTime ? value.ToUniversalTime() : DateTime.MinValue;
    }
}
