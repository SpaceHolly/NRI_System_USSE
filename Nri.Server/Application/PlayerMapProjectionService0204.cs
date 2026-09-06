using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Bson;
using MongoDB.Driver;
using Nri.Server.Infrastructure;
using Nri.Shared.Domain;
using Nri.Shared.Utilities;

namespace Nri.Server.Application;

public sealed class PlayerMapProjectionContext0204
{
    public string ActorUserId { get; set; } = string.Empty;
    public string CharacterId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string ActiveGroupId { get; set; } = string.Empty;
    public bool AdminPreview { get; set; }
    public bool IncludeMarkers { get; set; } = true;
}

public sealed class PlayerMapProjectionResult0204
{
    public bool Success { get; set; }
    public string ErrorKind { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Dictionary<string, object> Payload { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public interface IPlayerMapProjectionService
{
    PlayerMapProjectionResult0204 BuildSceneMap(string suppliedMapId, PlayerMapProjectionContext0204 context);
}

/// <summary>
/// The sole server boundary that turns canonical map state into a player-safe snapshot.
/// Hidden documents are filtered before any DTO dictionary is created.
/// </summary>
public sealed class PlayerMapProjectionService0204 : IPlayerMapProjectionService
{
    private readonly INriRepositoryFactory _repositories;
    private readonly MongoContext _mongo;
    private readonly IMapIdentityResolver _identity;

    public PlayerMapProjectionService0204(INriRepositoryFactory repositories, MongoContext mongo, IMapIdentityResolver identity)
    {
        _repositories = repositories ?? throw new ArgumentNullException(nameof(repositories));
        _mongo = mongo ?? throw new ArgumentNullException(nameof(mongo));
        _identity = identity ?? throw new ArgumentNullException(nameof(identity));
    }

    public PlayerMapProjectionResult0204 BuildSceneMap(string suppliedMapId, PlayerMapProjectionContext0204 context)
    {
        context ??= new PlayerMapProjectionContext0204();
        var identity = _identity.ResolveSceneMap(suppliedMapId);
        if (!identity.IsResolved || identity.CanonicalMap == null)
            return Fail(identity.Status == MapIdentityResolutionStatus0202.NotFound ? "not_found" : "conflict", identity.Message);

        var map = identity.CanonicalMap;
        if (!string.IsNullOrWhiteSpace(context.CampaignId)
            && !string.Equals(map.CampaignId, context.CampaignId, StringComparison.OrdinalIgnoreCase))
            return Fail("not_found", "Scene map is not available in this campaign.");
        if (!CanSee(map.VisibilityMode, map.IsArchived || map.Archived || map.Deleted))
            return Fail("forbidden", "Scene map is not available.");
        if (!CanUseCharacter(context))
            return Fail("forbidden", "Character is not available for this player projection.");

        var canonicalMapId = identity.CanonicalMapId;
        var compatibilityMapId = string.IsNullOrWhiteSpace(identity.LegacyMapId) ? canonicalMapId : identity.LegacyMapId;
        var mapIds = new[] { canonicalMapId, compatibilityMapId }.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var projectionRevision = Math.Max(map.EditorRevision, Math.Max(1, map.UpdatedAtUtc.ToUniversalTime().Ticks));

        var visibleLayers = ReadMapDocs("scene_map_layers", "SceneMapId", mapIds)
            .Where(IsVisibleDocument)
            .OrderBy(x => Int(x, "SortOrder"))
            .ThenBy(x => Text(x, "DisplayName"), StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        var visibleLayerIds = visibleLayers.Select(x => Text(x, "Id")).Where(x => x.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var visibleTileLayers = ReadMapDocs("scene_map_tile_layers", "SceneMapId", mapIds)
            .Where(IsVisibleDocument)
            .OrderBy(x => Int(x, "SortOrder"))
            .ToList();
        var visibleTileLayerIds = visibleTileLayers.Select(x => Text(x, "Id")).Where(x => x.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var fog = _repositories.MapFogLayers.GetByMapIdAsync(canonicalMapId).GetAwaiter().GetResult();
        var fogEnabled = fog != null && !string.Equals(fog.Mode, FogOfWarModeIds.Disabled, StringComparison.OrdinalIgnoreCase);
        bool PositionRevealed(double x, double y) => !fogEnabled || IsPositionRevealed(fog!, x, y);

        var objects = new List<Dictionary<string, object>>();
        var markerIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (context.IncludeMarkers)
        {
            foreach (var marker in _repositories.MapMarkers.ListByMapAsync(canonicalMapId, false, 5000).GetAwaiter().GetResult())
            {
                if (!marker.IsPlayerVisible || !CanSee(marker.VisibilityMode, marker.Archived || marker.Deleted) || !PositionRevealed(marker.X, marker.Y)) continue;
                var safe = SafeObject("marker", marker.Id, marker.Name, marker.MarkerType, marker.X, marker.Y,
                    marker.PublicNotes, marker.IconKey, marker.ColorKey, marker.LinkedEntityType, string.Empty, priority: 300);
                objects.Add(safe);
                markerIds.Add(marker.Id);
                projectionRevision = MaxRevision(projectionRevision, marker.UpdatedUtc);
            }

            foreach (var marker in ReadMapDocs("scene_map_markers", "SceneMapId", mapIds).Where(IsVisibleDocument))
            {
                var id = Text(marker, "Id");
                var x = Number(marker, "X");
                var y = Number(marker, "Y");
                if (id.Length == 0 || markerIds.Contains(id) || !PositionRevealed(x, y)) continue;
                objects.Add(SafeObject("marker", id, Text(marker, "DisplayName"), Text(marker, "MarkerType", "PointOfInterest"), x, y,
                    Text(marker, "DescriptionPlayer"), string.Empty, string.Empty, string.Empty, string.Empty, 300));
                markerIds.Add(id);
                projectionRevision = MaxRevision(projectionRevision, Date(marker, "UpdatedAtUtc"));
            }
        }

        foreach (var token in ReadMapDocs("map_token_instances", "MapId", mapIds).Where(IsVisibleDocument))
        {
            if (!string.Equals(Text(token, "MapKind", "scene"), "scene", StringComparison.OrdinalIgnoreCase)) continue;
            var x = Number(token, "X");
            var y = Number(token, "Y");
            if (!PositionRevealed(x, y)) continue;
            objects.Add(SafeObject("token", Text(token, "Id"), Text(token, "DisplayName"), Text(token, "TokenType", "Object"), x, y,
                Text(token, "DescriptionPlayer"), Text(token, "IconKey"), Text(token, "ColorKey"), Text(token, "LinkedEntityType"), Text(token, "LinkedEntityDisplayName"), 400,
                Number(token, "Size", 1d), Number(token, "Size", 1d)));
            projectionRevision = MaxRevision(projectionRevision, Date(token, "UpdatedAtUtc"));
        }

        foreach (var shape in ReadMapDocs("scene_map_shapes", "SceneMapId", mapIds).Where(IsVisibleDocument))
        {
            var layerId = Text(shape, "LayerId");
            if (layerId.Length > 0 && !visibleLayerIds.Contains(layerId)) continue;
            var x = Number(shape, "X");
            var y = Number(shape, "Y");
            if (!PositionRevealed(x, y)) continue;
            var safe = SafeObject("shape", Text(shape, "Id"), Text(shape, "DisplayName"), Text(shape, "ObjectKind", "Decoration"), x, y,
                Text(shape, "DescriptionPlayer"), Text(shape, "AssetKey"), Text(shape, "FillKey"), Text(shape, "LinkedEntityType"), string.Empty, 100,
                Number(shape, "Width", 1d), Number(shape, "Height", 1d));
            safe["shapeKind"] = Text(shape, "ShapeKind", "Rectangle");
            safe["radius"] = Number(shape, "Radius");
            safe["rotationDegrees"] = Number(shape, "RotationDegrees");
            safe["fillKey"] = Text(shape, "FillKey");
            safe["strokeKey"] = Text(shape, "StrokeKey");
            safe["opacity"] = Number(shape, "Opacity", 0.65d);
            safe["materialKey"] = Text(shape, "MaterialKey");
            safe["textureKey"] = Text(shape, "TextureKey");
            safe["assetKey"] = Text(shape, "AssetKey");
            safe["zIndex"] = Int(shape, "ZIndex", Int(shape, "SortOrder"));
            objects.Add(safe);
            projectionRevision = MaxRevision(projectionRevision, Date(shape, "UpdatedAtUtc"));
        }

        foreach (var asset in ReadMapDocs("scene_map_asset_instances", "SceneMapId", mapIds).Where(IsVisibleDocument))
        {
            var layerId = Text(asset, "LayerId");
            if (layerId.Length > 0 && !visibleLayerIds.Contains(layerId)) continue;
            var x = Number(asset, "X");
            var y = Number(asset, "Y");
            if (!PositionRevealed(x, y)) continue;
            var safe = SafeObject("asset", Text(asset, "Id"), Text(asset, "DisplayName"), Text(asset, "AssetKind", "Prop"), x, y,
                Text(asset, "DescriptionPlayer"), Text(asset, "AssetKey"), string.Empty, Text(asset, "LinkedEntityType"), string.Empty, 200,
                Number(asset, "Width", 1d), Number(asset, "Height", 1d));
            safe["assetKey"] = Text(asset, "AssetKey");
            safe["objectKind"] = Text(asset, "ObjectKind", "Decoration");
            safe["rotationDegrees"] = Number(asset, "RotationDegrees");
            safe["zIndex"] = Int(asset, "ZIndex", 100);
            objects.Add(safe);
            projectionRevision = MaxRevision(projectionRevision, Date(asset, "UpdatedAtUtc"));
        }

        var patches = ReadMapDocs("scene_map_tile_patches", "SceneMapId", mapIds)
            .Where(IsVisibleDocument)
            .Where(x =>
            {
                var layerId = Text(x, "TileLayerId");
                return layerId.Length == 0 || visibleTileLayerIds.Contains(layerId);
            })
            .Select(SafeTilePatch)
            .Cast<object>()
            .ToArray();

        var ordered = objects
            .Where(x => !string.IsNullOrWhiteSpace(Convert.ToString(x["id"])))
            .OrderByDescending(x => Convert.ToInt32(x["labelPriority"]))
            .ThenBy(x => Convert.ToString(x["name"]), StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(x => Convert.ToString(x["id"]), StringComparer.Ordinal)
            .ToArray();
        var labels = ordered.Where(x => !string.IsNullOrWhiteSpace(Convert.ToString(x["name"])))
            .Select(x => (object)new Dictionary<string, object>
            {
                ["objectId"] = x["id"], ["text"] = x["name"], ["objectKind"] = x["kind"],
                ["priority"] = x["labelPriority"], ["x"] = x["x"], ["y"] = x["y"]
            }).ToArray();
        var legend = ordered.GroupBy(x => Convert.ToString(x["kind"]) ?? "object", StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => LegendOrder(x.Key))
            .Select(x => (object)new Dictionary<string, object>
            {
                ["category"] = x.Key, ["displayName"] = LegendName(x.Key), ["visibleCount"] = x.Count(), ["isEnabled"] = true
            }).ToArray();

        var markers = ordered.Where(x => string.Equals(Convert.ToString(x["kind"]), "marker", StringComparison.OrdinalIgnoreCase)).Cast<object>().ToArray();
        var tokens = ordered.Where(x => string.Equals(Convert.ToString(x["kind"]), "token", StringComparison.OrdinalIgnoreCase)).Cast<object>().ToArray();
        var shapes = ordered.Where(x => string.Equals(Convert.ToString(x["kind"]), "shape", StringComparison.OrdinalIgnoreCase)).Cast<object>().ToArray();
        var assets = ordered.Where(x => string.Equals(Convert.ToString(x["kind"]), "asset", StringComparison.OrdinalIgnoreCase)).Cast<object>().ToArray();
        var layers = visibleLayers.Select(SafeLayer).Cast<object>().ToArray();
        var tileLayers = visibleTileLayers.Select(SafeLayer).Cast<object>().ToArray();

        var fogPayload = SafeFog(fog);
        var mapPayload = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["mapId"] = canonicalMapId,
            ["name"] = map.Name ?? string.Empty,
            ["description"] = map.Description ?? string.Empty,
            ["mapType"] = MapTypeIds.Scene,
            ["widthMeters"] = map.WidthMeters,
            ["heightMeters"] = map.HeightMeters,
            ["gridCellSizeMeters"] = map.GridCellSizeMeters,
            ["showGrid"] = ExtraBool(map.ExtraData, "showGrid", true),
            ["showCoordinates"] = ExtraBool(map.ExtraData, "showCoordinates", true),
            ["fogEnabled"] = fogEnabled,
            ["fogOfWarVisibleState"] = fogPayload,
            ["layers"] = layers,
            ["tileLayers"] = tileLayers,
            ["tilePatches"] = patches,
            ["markers"] = markers,
            ["tokens"] = tokens,
            ["shapes"] = shapes,
            ["assetInstances"] = assets,
            ["objects"] = ordered.Cast<object>().ToArray(),
            ["labels"] = labels,
            ["legend"] = legend,
            ["canonicalMapRevision"] = map.EditorRevision,
            ["projectionRevision"] = projectionRevision,
            ["fullSnapshotVersion"] = 1,
            ["snapshotKind"] = "full",
            ["builtAtUtc"] = DateTime.UtcNow
        };

        return new PlayerMapProjectionResult0204
        {
            Success = true,
            Message = "Player scene map loaded.",
            Payload = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["map"] = mapPayload,
                ["projectionRevision"] = projectionRevision,
                ["canonicalMapRevision"] = map.EditorRevision,
                ["fullSnapshotVersion"] = 1,
                ["snapshotKind"] = "full",
                ["warnings"] = Array.Empty<object>(),
                ["builtAtUtc"] = DateTime.UtcNow
            }
        };
    }

    private bool CanUseCharacter(PlayerMapProjectionContext0204 context)
    {
        if (string.IsNullOrWhiteSpace(context.CharacterId)) return true;
        var ownership = _mongo.CharacterOwnerships.Find(Builders<CharacterOwnershipState>.Filter.And(
            Builders<CharacterOwnershipState>.Filter.Eq(x => x.CharacterId, context.CharacterId),
            Builders<CharacterOwnershipState>.Filter.Eq(x => x.Deleted, false))).FirstOrDefault();
        if (ownership == null) return context.AdminPreview;
        return context.AdminPreview
            || string.Equals(ownership.OwnerUserId, context.ActorUserId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(ownership.ControlledByUserId, context.ActorUserId, StringComparison.OrdinalIgnoreCase);
    }

    private List<BsonDocument> ReadMapDocs(string collectionName, string mapField, IReadOnlyCollection<string> mapIds)
    {
        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.In(mapField, mapIds),
            Builders<BsonDocument>.Filter.Ne("IsArchived", true),
            Builders<BsonDocument>.Filter.Ne("Deleted", true),
            Builders<BsonDocument>.Filter.Ne("Archived", true));
        return _mongo.Database.GetCollection<BsonDocument>(collectionName).Find(filter).ToList();
    }

    private static bool IsVisibleDocument(BsonDocument doc)
    {
        if ((doc.Contains("IsPlayerVisible") && !Bool(doc, "IsPlayerVisible")) || Bool(doc, "IsGmOnly")) return false;
        return CanSee(Text(doc, "Visibility", Text(doc, "VisibilityMode", "Hidden")),
            Bool(doc, "IsArchived") || Bool(doc, "Deleted") || Bool(doc, "Archived"));
    }

    internal static bool CanSee(string? visibility, bool archived) => PlayerMapVisibilityPolicy0204.IsIncluded(visibility, archived);

    private static Dictionary<string, object> SafeObject(string kind, string id, string name, string type, double x, double y,
        string description, string iconKey, string colorKey, string linkedEntityType, string linkedEntityDisplayName, int priority,
        double width = 1d, double height = 1d)
    {
        var payload = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = id, ["objectId"] = id, [kind + "Id"] = id, ["kind"] = kind,
            ["name"] = name ?? string.Empty, ["displayName"] = name ?? string.Empty,
            ["type"] = type ?? string.Empty, [kind + "Type"] = type ?? string.Empty,
            ["x"] = x, ["y"] = y, ["width"] = width, ["height"] = height,
            ["cardTitle"] = name ?? string.Empty, ["cardDescription"] = description ?? string.Empty,
            ["descriptionPlayer"] = description ?? string.Empty, ["iconKey"] = iconKey ?? string.Empty,
            ["colorKey"] = colorKey ?? string.Empty, ["linkedEntityType"] = linkedEntityType ?? string.Empty,
            ["linkedEntityDisplayName"] = linkedEntityDisplayName ?? string.Empty,
            ["labelPriority"] = priority, ["isVisible"] = true
        };
        if (string.Equals(kind, "asset", StringComparison.OrdinalIgnoreCase)) payload["assetInstanceId"] = id;
        return payload;
    }

    private static Dictionary<string, object> SafeLayer(BsonDocument layer)
        => new(StringComparer.OrdinalIgnoreCase)
        {
            ["layerId"] = Text(layer, "Id"), ["id"] = Text(layer, "Id"),
            ["displayName"] = Text(layer, "DisplayName"), ["name"] = Text(layer, "DisplayName"),
            ["layerKind"] = Text(layer, "LayerKind", "Objects"), ["sortOrder"] = Int(layer, "SortOrder"),
            ["isVisibleByDefault"] = Bool(layer, "IsVisibleByDefault", true)
        };

    private static Dictionary<string, object> SafeTilePatch(BsonDocument patch)
        => new(StringComparer.OrdinalIgnoreCase)
        {
            ["tilePatchId"] = Text(patch, "Id"), ["id"] = Text(patch, "Id"),
            ["tileLayerId"] = Text(patch, "TileLayerId"), ["materialKey"] = Text(patch, "MaterialKey", "grass"),
            ["textureKey"] = Text(patch, "TextureKey"), ["x"] = Number(patch, "X"), ["y"] = Number(patch, "Y"),
            ["width"] = Number(patch, "Width", 1d), ["height"] = Number(patch, "Height", 1d),
            ["rotationDegrees"] = Number(patch, "RotationDegrees"), ["opacity"] = Number(patch, "Opacity", 1d),
            ["sortOrder"] = Int(patch, "SortOrder", 10)
        };

    private static Dictionary<string, object> SafeFog(FogOfWarState? fog)
        => new(StringComparer.OrdinalIgnoreCase)
        {
            ["mode"] = fog?.Mode ?? FogOfWarModeIds.Disabled,
            ["cellSizeMeters"] = fog?.CellSizeMeters ?? 25,
            ["hiddenCells"] = (fog?.HiddenCells ?? new List<MapFogCellRange>()).Select(x => (object)new Dictionary<string, object>
            {
                ["fromX"] = x.FromX, ["fromY"] = x.FromY, ["toX"] = x.ToX, ["toY"] = x.ToY
            }).ToArray()
        };

    private static bool IsPositionRevealed(FogOfWarState fog, double x, double y)
    {
        var cell = Math.Max(1, fog.CellSizeMeters);
        var cellX = (int)Math.Floor(Math.Max(0, x) / cell);
        var cellY = (int)Math.Floor(Math.Max(0, y) / cell);
        bool In(IEnumerable<MapFogCellRange> ranges) => ranges.Any(r => cellX >= r.FromX && cellX <= r.ToX && cellY >= r.FromY && cellY <= r.ToY);
        if (In(fog.GMOnlyCells ?? new List<MapFogCellRange>())) return false;
        if (In(fog.HiddenCells ?? new List<MapFogCellRange>())) return false;
        if (In(fog.RevealedCells ?? new List<MapFogCellRange>())) return true;
        return !string.Equals(fog.DefaultState, FogDefaultStateIds.Hidden, StringComparison.OrdinalIgnoreCase);
    }

    private static int LegendOrder(string kind) => kind.ToLowerInvariant() switch { "token" => 0, "marker" => 1, "asset" => 2, "shape" => 3, _ => 9 };
    private static string LegendName(string kind) => kind.ToLowerInvariant() switch { "token" => "Токены", "marker" => "Маркеры", "asset" => "Объекты", "shape" => "Области", _ => "Прочее" };
    private static long MaxRevision(long current, DateTime value) => value == default ? current : Math.Max(current, value.ToUniversalTime().Ticks);
    private static PlayerMapProjectionResult0204 Fail(string kind, string message) => new() { ErrorKind = kind, Message = message };
    private static bool ExtraBool(IDictionary<string, object>? data, string key, bool fallback) => data != null && data.TryGetValue(key, out var raw) ? Convert.ToBoolean(raw) : fallback;
    private static string Text(BsonDocument doc, string name, string fallback = "") => doc.TryGetValue(name, out var value) && !value.IsBsonNull ? value.ToString() : fallback;
    private static bool Bool(BsonDocument doc, string name, bool fallback = false) => doc.TryGetValue(name, out var value) && value.IsBoolean ? value.AsBoolean : fallback;
    private static int Int(BsonDocument doc, string name, int fallback = 0) => doc.TryGetValue(name, out var value) && value.IsNumeric ? value.ToInt32() : fallback;
    private static double Number(BsonDocument doc, string name, double fallback = 0d) => doc.TryGetValue(name, out var value) && value.IsNumeric ? value.ToDouble() : fallback;
    private static DateTime Date(BsonDocument doc, string name) => doc.TryGetValue(name, out var value) && value.IsValidDateTime ? value.ToUniversalTime() : default;
}
