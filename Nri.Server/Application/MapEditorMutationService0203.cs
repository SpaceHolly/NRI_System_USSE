using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MongoDB.Bson;
using MongoDB.Driver;
using Nri.Server.Infrastructure;
using Nri.Shared.Domain;

namespace Nri.Server.Application;

public interface IMapEditorMutationService
{
    MapEditorStateResult0203 GetState(string suppliedMapId);
    MapEditorMutationResult0203 Mutate(MapEditorMutationRequest0203 request);
}

public sealed class MapEditorMutationRequest0203
{
    public string OperationId { get; set; } = string.Empty;
    public string MapId { get; set; } = string.Empty;
    public string Mutation { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
    public string LayerId { get; set; } = string.Empty;
    public long ExpectedMapRevision { get; set; }
    public long? ExpectedLayerRevision { get; set; }
    public long? ExpectedObjectRevision { get; set; }
    public string ActorUserId { get; set; } = string.Empty;
    public Dictionary<string, object> Values { get; set; } = new Dictionary<string, object>();
}

public sealed class MapEditorStateResult0203
{
    public string CanonicalMapId { get; set; } = string.Empty;
    public long MapRevision { get; set; }
    public int WidthMeters { get; set; }
    public int HeightMeters { get; set; }
    public int GridCellSizeMeters { get; set; }
    public Dictionary<string, object>[] Layers { get; set; } = Array.Empty<Dictionary<string, object>>();
    public Dictionary<string, object>[] Objects { get; set; } = Array.Empty<Dictionary<string, object>>();
}

public sealed class MapEditorMutationResult0203
{
    public bool IsReplay { get; set; }
    public string CanonicalMapId { get; set; } = string.Empty;
    public string Mutation { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
    public long MapRevision { get; set; }
    public long LayerRevision { get; set; }
    public long ObjectRevision { get; set; }
    public Dictionary<string, object> Target { get; set; } = new Dictionary<string, object>();
}

public sealed class MapEditorMutationException0203 : InvalidOperationException
{
    public MapEditorMutationException0203(string kind, string message) : base(message) => Kind = kind;
    public string Kind { get; }
}

public sealed class MapEditorMutationService0203 : IMapEditorMutationService
{
    private const string LayersCollection = "scene_map_layers";
    private const string TileLayersCollection = "scene_map_tile_layers";
    private const string ShapesCollection = "scene_map_shapes";
    private const string TilePatchesCollection = "scene_map_tile_patches";
    private const string AssetsCollection = "scene_map_asset_instances";
    private const string OperationsCollection = "map_editor_operations";
    private readonly MongoContext _mongo;
    private readonly IMapIdentityResolver _identity;

    public MapEditorMutationService0203(MongoContext mongo, IMapIdentityResolver identity)
    {
        _mongo = mongo ?? throw new ArgumentNullException(nameof(mongo));
        _identity = identity ?? throw new ArgumentNullException(nameof(identity));
        EnsureIndexes();
    }

    public MapEditorStateResult0203 GetState(string suppliedMapId)
    {
        var resolution = Resolve(suppliedMapId);
        var mapIds = MapIds(resolution);
        var layers = Documents(LayersCollection, mapIds, includeArchived: false)
            .Concat(Documents(TileLayersCollection, mapIds, includeArchived: false))
            .OrderBy(document => Int(document, "SortOrder"))
            .ThenBy(document => Text(document, "Id"), StringComparer.Ordinal)
            .Select(LayerPayload)
            .ToArray();
        var layerOrder = layers.ToDictionary(row => Convert.ToString(row["id"], CultureInfo.InvariantCulture) ?? string.Empty,
            row => Convert.ToInt32(row["order"], CultureInfo.InvariantCulture), StringComparer.OrdinalIgnoreCase);
        var objects = Documents(TilePatchesCollection, mapIds, false).Select(document => ObjectPayload(document, "tilePatch", layerOrder))
            .Concat(Documents(ShapesCollection, mapIds, false).Select(document => ObjectPayload(document, "shape", layerOrder)))
            .Concat(Documents(AssetsCollection, mapIds, false).Select(document => ObjectPayload(document, "assetInstance", layerOrder)))
            .OrderBy(row => Convert.ToInt32(row["layerOrder"], CultureInfo.InvariantCulture))
            .ThenBy(row => Convert.ToInt32(row["zIndex"], CultureInfo.InvariantCulture))
            .ThenBy(row => Convert.ToString(row["id"], CultureInfo.InvariantCulture), StringComparer.Ordinal)
            .ToArray();
        var map = resolution.CanonicalMap!;
        return new MapEditorStateResult0203
        {
            CanonicalMapId = map.Id,
            MapRevision = map.EditorRevision,
            WidthMeters = map.WidthMeters,
            HeightMeters = map.HeightMeters,
            GridCellSizeMeters = map.GridCellSizeMeters,
            Layers = layers,
            Objects = objects
        };
    }

    public MapEditorMutationResult0203 Mutate(MapEditorMutationRequest0203 request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        request.OperationId = Required(request.OperationId, "OperationId");
        request.Mutation = Required(request.Mutation, "mutation").Trim().ToLowerInvariant();
        request.ActorUserId = Required(request.ActorUserId, "actorUserId");
        var resolution = Resolve(request.MapId);
        var replay = Operations().Find(Builders<BsonDocument>.Filter.Eq("OperationId", request.OperationId)).FirstOrDefault();
        if (replay != null) return ResultFromOperation(replay, true);

        var map = resolution.CanonicalMap!;
        if (map.EditorRevision != request.ExpectedMapRevision)
            throw Failure("conflict", $"Карта была изменена. Текущая редакция: {map.EditorRevision}; ожидалась: {request.ExpectedMapRevision}.");

        var now = DateTime.UtcNow;
        var mapFilter = Builders<MapCanvasState>.Filter.And(
            Builders<MapCanvasState>.Filter.Eq(item => item.Id, map.Id),
            Builders<MapCanvasState>.Filter.Eq(item => item.EditorRevision, request.ExpectedMapRevision));
        var nextMapRevision = request.ExpectedMapRevision + 1;
        var mapUpdate = Builders<MapCanvasState>.Update
            .Set(item => item.EditorRevision, nextMapRevision)
            .Set(item => item.UpdatedAtUtc, now);
        if (_mongo.MapCanvases.UpdateOne(mapFilter, mapUpdate).ModifiedCount != 1)
            throw Failure("conflict", "Карта изменилась во время сохранения. Перезагрузите редактор.");

        try
        {
            MapEditorMutationResult0203 result;
            if (request.Mutation.StartsWith("layer.", StringComparison.Ordinal))
                result = MutateLayer(request, resolution, now);
            else
                result = MutateObject(request, resolution, now);

            map.EditorRevision = nextMapRevision;
            map.UpdatedAtUtc = now;
            _identity.SynchronizeSceneProjection(map, resolution.LegacyMapId, request.ActorUserId, resolution.CompatibilityProjection);
            result.MapRevision = nextMapRevision;
            SaveOperation(request, result, now);
            return result;
        }
        catch
        {
            var rollbackFilter = Builders<MapCanvasState>.Filter.And(
                Builders<MapCanvasState>.Filter.Eq(item => item.Id, map.Id),
                Builders<MapCanvasState>.Filter.Eq(item => item.EditorRevision, nextMapRevision));
            _mongo.MapCanvases.UpdateOne(rollbackFilter,
                Builders<MapCanvasState>.Update
                    .Set(item => item.EditorRevision, request.ExpectedMapRevision)
                    .Set(item => item.UpdatedAtUtc, map.UpdatedAtUtc));
            throw;
        }
    }

    private MapEditorMutationResult0203 MutateLayer(MapEditorMutationRequest0203 request, MapIdentityResolution0202 resolution, DateTime now)
    {
        var isCreate = request.Mutation == "layer.create";
        var layerType = ValueText(request.Values, "layerType", "object").ToLowerInvariant();
        var layerId = isCreate ? (request.TargetId.Trim().Length > 0 ? request.TargetId.Trim() : "scene_layer_" + Guid.NewGuid().ToString("N")) : Required(request.TargetId, "targetId");
        var existing = isCreate ? null : FindLayer(layerId);
        var collectionName = isCreate
            ? (layerType == "tile" ? TileLayersCollection : LayersCollection)
            : Collection(TileLayersCollection).Find(IdFilter(layerId, true)).Any() ? TileLayersCollection : LayersCollection;
        var collection = Collection(collectionName);
        if (!isCreate && existing == null) throw Failure("not_found", "Слой не найден.");
        if (isCreate && existing != null) throw Failure("conflict", "Слой с таким идентификатором уже существует.");
        var currentRevision = existing == null ? 0L : Long(existing, "Revision", 1L);
        RequireExpected(request.ExpectedLayerRevision, currentRevision, "слой");

        if (request.Mutation == "layer.reorder")
        {
            var swapLayerId = ValueText(request.Values, "swapLayerId");
            if (!string.IsNullOrWhiteSpace(swapLayerId))
                return SwapLayerOrder(request, resolution, now, collection, existing!, layerId, currentRevision, swapLayerId);
        }

        var nextRevision = currentRevision + 1;
        var document = existing == null ? new BsonDocument() : new BsonDocument(existing);
        document["_id"] = layerId;
        document["Id"] = layerId;
        document["SceneMapId"] = resolution.CanonicalMapId;
        document["DisplayName"] = Limit(ValueText(request.Values, "displayName", existing == null ? "Новый слой" : Text(existing, "DisplayName")), 1, 160, "Название слоя");
        document["LayerKind"] = Limit(ValueText(request.Values, "layerKind", existing == null ? "Objects" : Text(existing, "LayerKind", "Objects")), 1, 64, "Тип слоя");
        document["SortOrder"] = ValueInt(request.Values, "sortOrder", existing == null ? 0 : Int(existing, "SortOrder"));
        document["IsLocked"] = ValueBool(request.Values, "isLocked", existing != null && Bool(existing, "IsLocked"));
        document["IsVisibleByDefault"] = ValueBool(request.Values, "isVisible", existing == null || Bool(existing, "IsVisibleByDefault"));
        document["Visibility"] = ValueText(request.Values, "visibility", existing == null ? "PlayerVisible" : Text(existing, "Visibility", "PlayerVisible"));
        document["Opacity"] = Math.Max(0d, Math.Min(1d, ValueDouble(request.Values, "opacity", existing == null ? 1d : Double(existing, "Opacity", 1d))));
        document["EditableKinds"] = ValueText(request.Values, "editableKinds", existing == null ? (layerType == "tile" ? "tilePatch" : "shape,assetInstance") : Text(existing, "EditableKinds"));
        document["Revision"] = nextRevision;
        document["IsArchived"] = request.Mutation == "layer.archive";
        document["UpdatedAtUtc"] = now;
        document["UpdatedByUserId"] = request.ActorUserId;
        if (!document.Contains("CreatedAtUtc")) document["CreatedAtUtc"] = now;
        if (!document.Contains("CreatedByUserId")) document["CreatedByUserId"] = request.ActorUserId;
        collection.ReplaceOne(IdFilter(layerId, true), document, new ReplaceOptions { IsUpsert = isCreate });
        return new MapEditorMutationResult0203
        {
            CanonicalMapId = resolution.CanonicalMapId,
            Mutation = request.Mutation,
            TargetId = layerId,
            LayerRevision = nextRevision,
            Target = LayerPayload(document)
        };
    }

    private MapEditorMutationResult0203 SwapLayerOrder(MapEditorMutationRequest0203 request, MapIdentityResolution0202 resolution,
        DateTime now, IMongoCollection<BsonDocument> sourceCollection, BsonDocument source, string sourceId, long sourceRevision, string swapLayerId)
    {
        var swap = FindLayer(swapLayerId) ?? throw Failure("not_found", "Слой для обмена порядком не найден.");
        if (Bool(source, "IsArchived") || Bool(swap, "IsArchived")) throw Failure("validation", "Архивный слой нельзя переупорядочить.");
        var swapCollection = Collection(Collection(TileLayersCollection).Find(IdFilter(swapLayerId, true)).Any() ? TileLayersCollection : LayersCollection);
        var sourceOrder = Int(source, "SortOrder");
        var swapOrder = Int(swap, "SortOrder");
        var swapRevision = Long(swap, "Revision", 1L);
        var sourceUpdate = Builders<BsonDocument>.Update.Set("SortOrder", swapOrder).Set("Revision", sourceRevision + 1)
            .Set("UpdatedAtUtc", now).Set("UpdatedByUserId", request.ActorUserId);
        var swapUpdate = Builders<BsonDocument>.Update.Set("SortOrder", sourceOrder).Set("Revision", swapRevision + 1)
            .Set("UpdatedAtUtc", now).Set("UpdatedByUserId", request.ActorUserId);
        var sourceFilter = Builders<BsonDocument>.Filter.And(IdFilter(sourceId, true), Builders<BsonDocument>.Filter.Eq("Revision", sourceRevision));
        var swapFilter = Builders<BsonDocument>.Filter.And(IdFilter(swapLayerId, true), Builders<BsonDocument>.Filter.Eq("Revision", swapRevision));
        if (sourceCollection.UpdateOne(sourceFilter, sourceUpdate).ModifiedCount != 1)
            throw Failure("conflict", "Порядок исходного слоя изменился. Перезагрузите редактор.");
        if (swapCollection.UpdateOne(swapFilter, swapUpdate).ModifiedCount != 1)
        {
            sourceCollection.UpdateOne(Builders<BsonDocument>.Filter.And(IdFilter(sourceId, true), Builders<BsonDocument>.Filter.Eq("Revision", sourceRevision + 1)),
                Builders<BsonDocument>.Update.Set("SortOrder", sourceOrder).Set("Revision", sourceRevision));
            throw Failure("conflict", "Порядок соседнего слоя изменился. Перезагрузите редактор.");
        }
        source["SortOrder"] = swapOrder;
        source["Revision"] = sourceRevision + 1;
        return new MapEditorMutationResult0203
        {
            CanonicalMapId = resolution.CanonicalMapId,
            Mutation = request.Mutation,
            TargetId = sourceId,
            LayerRevision = sourceRevision + 1,
            Target = LayerPayload(source)
        };
    }

    private MapEditorMutationResult0203 MutateObject(MapEditorMutationRequest0203 request, MapIdentityResolution0202 resolution, DateTime now)
    {
        var parts = request.Mutation.Split('.');
        if (parts.Length != 2) throw Failure("validation", "Неизвестная операция редактора.");
        var kind = parts[0];
        var action = parts[1];
        var collectionName = kind == "shape" ? ShapesCollection : kind == "tilepatch" ? TilePatchesCollection : kind == "asset" || kind == "assetinstance" ? AssetsCollection : string.Empty;
        if (collectionName.Length == 0) throw Failure("validation", "Этот тип объекта не поддерживается редактором.");
        if (action != "create" && action != "update" && action != "move" && action != "archive" && action != "restore")
            throw Failure("validation", "Эта операция объекта не поддерживается редактором.");

        var isCreate = action == "create";
        var targetId = isCreate ? (request.TargetId.Trim().Length > 0 ? request.TargetId.Trim() : Prefix(kind) + Guid.NewGuid().ToString("N")) : Required(request.TargetId, "targetId");
        var collection = Collection(collectionName);
        var existing = collection.Find(IdFilter(targetId, true)).FirstOrDefault();
        if (!isCreate && existing == null) throw Failure("not_found", "Объект карты не найден.");
        if (isCreate && existing != null) throw Failure("conflict", "Объект с таким идентификатором уже существует.");
        var currentObjectRevision = existing == null ? 0L : Long(existing, "Revision", 1L);
        RequireExpected(request.ExpectedObjectRevision, currentObjectRevision, "объект");

        var layerId = First(request.LayerId, ValueText(request.Values, "layerId"), existing == null ? string.Empty : First(Text(existing, "LayerId"), Text(existing, "TileLayerId")));
        layerId = Required(layerId, "layerId");
        var layer = FindLayer(layerId);
        if (layer == null) throw Failure("validation", "Выбранный слой не найден.");
        if (Bool(layer, "IsArchived")) throw Failure("validation", "Выбранный слой находится в архиве.");
        if (Bool(layer, "IsLocked") && action != "restore") throw Failure("locked", "Слой заблокирован. Изменения запрещены.");
        var requiredEditableKind = kind == "tilepatch" ? "tilePatch" : kind == "shape" ? "shape" : "assetInstance";
        var editableKinds = Text(layer, "EditableKinds");
        if (!string.IsNullOrWhiteSpace(editableKinds)
            && !editableKinds.Split(',').Any(value => string.Equals(value.Trim(), requiredEditableKind, StringComparison.OrdinalIgnoreCase)))
            throw Failure("validation", $"Слой не поддерживает объекты типа «{requiredEditableKind}».");
        var currentLayerRevision = Long(layer, "Revision", 1L);
        RequireExpected(request.ExpectedLayerRevision, currentLayerRevision, "слой");

        var document = existing == null ? new BsonDocument() : new BsonDocument(existing);
        document["_id"] = targetId;
        document["Id"] = targetId;
        document["SceneMapId"] = resolution.CanonicalMapId;
        document[kind == "tilepatch" ? "TileLayerId" : "LayerId"] = layerId;
        ApplyWhitelistedValues(document, request.Values);
        if (!document.Contains("DisplayName") && kind != "tilepatch") document["DisplayName"] = kind == "shape" ? "Новая фигура" : "Новый объект";
        if (kind == "tilepatch" && !document.Contains("MaterialKey")) document["MaterialKey"] = "grass";
        if (!document.Contains("Width")) document["Width"] = kind == "tilepatch" ? 5d : 20d;
        if (!document.Contains("Height")) document["Height"] = kind == "tilepatch" ? 5d : 20d;
        if (!document.Contains("X")) document["X"] = 0d;
        if (!document.Contains("Y")) document["Y"] = 0d;
        if (!document.Contains("Visibility")) document["Visibility"] = "PlayerVisible";
        document["IsArchived"] = action == "archive" ? true : action == "restore" ? false : existing != null && Bool(existing, "IsArchived");
        document["Revision"] = currentObjectRevision + 1;
        document["UpdatedAtUtc"] = now;
        document["UpdatedByUserId"] = request.ActorUserId;
        if (!document.Contains("CreatedAtUtc")) document["CreatedAtUtc"] = now;
        if (!document.Contains("CreatedByUserId")) document["CreatedByUserId"] = request.ActorUserId;
        ValidateBounds(document, resolution.CanonicalMap!);
        collection.ReplaceOne(IdFilter(targetId, true), document, new ReplaceOptions { IsUpsert = isCreate });

        var nextLayerRevision = currentLayerRevision + 1;
        var layerCollection = LayerCollection(layerId);
        var layerUpdate = Builders<BsonDocument>.Update.Set("Revision", nextLayerRevision).Set("UpdatedAtUtc", now).Set("UpdatedByUserId", request.ActorUserId);
        var layerFilter = Builders<BsonDocument>.Filter.And(IdFilter(layerId, true), Builders<BsonDocument>.Filter.Or(
            Builders<BsonDocument>.Filter.Eq("Revision", currentLayerRevision),
            currentLayerRevision == 1L ? Builders<BsonDocument>.Filter.Exists("Revision", false) : Builders<BsonDocument>.Filter.Eq("Revision", currentLayerRevision)));
        if (layerCollection.UpdateOne(layerFilter, layerUpdate).MatchedCount != 1)
            throw Failure("conflict", "Слой изменился во время сохранения. Перезагрузите редактор.");

        var order = Int(layer, "SortOrder");
        var objectKind = kind == "tilepatch" ? "tilePatch" : kind == "shape" ? "shape" : "assetInstance";
        return new MapEditorMutationResult0203
        {
            CanonicalMapId = resolution.CanonicalMapId,
            Mutation = request.Mutation,
            TargetId = targetId,
            LayerRevision = nextLayerRevision,
            ObjectRevision = currentObjectRevision + 1,
            Target = ObjectPayload(document, objectKind, new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { [layerId] = order })
        };
    }

    private void ApplyWhitelistedValues(BsonDocument document, IDictionary<string, object> values)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["displayName"] = "DisplayName", ["name"] = "DisplayName", ["x"] = "X", ["y"] = "Y",
            ["width"] = "Width", ["height"] = "Height", ["radius"] = "Radius", ["rotationDegrees"] = "RotationDegrees",
            ["zIndex"] = "ZIndex", ["sortOrder"] = "SortOrder", ["visibility"] = "Visibility", ["shapeKind"] = "ShapeKind",
            ["objectKind"] = "ObjectKind", ["materialKey"] = "MaterialKey", ["textureKey"] = "TextureKey",
            ["assetKey"] = "AssetKey", ["assetKind"] = "AssetKind", ["renderMode"] = "RenderMode",
            ["visualStyleKey"] = "VisualStyleKey", ["opacity"] = "Opacity", ["visualOpacity"] = "VisualOpacity",
            ["strokeThickness"] = "StrokeThickness", ["descriptionPlayer"] = "DescriptionPlayer", ["descriptionGm"] = "DescriptionGm",
            ["blocksMovement"] = "BlocksMovement", ["blocksVision"] = "BlocksVision", ["providesCover"] = "ProvidesCover",
            ["isInteractable"] = "IsInteractable", ["gridSnapEnabled"] = "GridSnapEnabled", ["points"] = "Points", ["text"] = "Text"
        };
        foreach (var pair in values)
        {
            if (!map.TryGetValue(pair.Key, out var field) || pair.Value == null) continue;
            document[field] = BsonValue.Create(pair.Value);
        }
    }

    private static void ValidateBounds(BsonDocument document, MapCanvasState map)
    {
        var x = Double(document, "X");
        var y = Double(document, "Y");
        var width = Double(document, "Width");
        var height = Double(document, "Height");
        if (width <= 0d || height <= 0d) throw Failure("validation", "Размер объекта должен быть положительным.");
        if (x < 0d || y < 0d || x + width > map.WidthMeters || y + height > map.HeightMeters)
            throw Failure("validation", "Объект выходит за границы карты.");
    }

    private MapIdentityResolution0202 Resolve(string suppliedMapId)
    {
        var resolution = _identity.ResolveSceneMap(Required(suppliedMapId, "mapId"));
        if (resolution.IsResolved) return resolution;
        var kind = resolution.Status == MapIdentityResolutionStatus0202.NotFound ? "not_found" : "conflict";
        throw Failure(kind, resolution.Message);
    }

    private IEnumerable<BsonDocument> Documents(string collectionName, string[] mapIds, bool includeArchived)
    {
        var filter = Builders<BsonDocument>.Filter.In("SceneMapId", mapIds);
        if (!includeArchived) filter &= Builders<BsonDocument>.Filter.Ne("IsArchived", true);
        return Collection(collectionName).Find(filter).ToList();
    }

    private BsonDocument? FindLayer(string layerId)
        => Collection(LayersCollection).Find(IdFilter(layerId, true)).FirstOrDefault()
           ?? Collection(TileLayersCollection).Find(IdFilter(layerId, true)).FirstOrDefault();

    private IMongoCollection<BsonDocument> LayerCollection(string layerId)
        => Collection(LayersCollection).Find(IdFilter(layerId, true)).Limit(1).Any() ? Collection(LayersCollection) : Collection(TileLayersCollection);

    private static Dictionary<string, object> LayerPayload(BsonDocument layer)
        => new Dictionary<string, object>
        {
            ["id"] = Text(layer, "Id"), ["name"] = Text(layer, "DisplayName"), ["layerKind"] = Text(layer, "LayerKind", layer.Contains("TileSizeMeters") ? "Terrain" : "Objects"),
            ["order"] = Int(layer, "SortOrder"), ["isLocked"] = Bool(layer, "IsLocked"), ["isVisible"] = Bool(layer, "IsVisibleByDefault"),
            ["isGmOnly"] = !string.Equals(Text(layer, "Visibility"), "PlayerVisible", StringComparison.OrdinalIgnoreCase),
            ["opacity"] = Double(layer, "Opacity", 1d), ["editableKinds"] = Text(layer, "EditableKinds"), ["revision"] = Long(layer, "Revision", 1L)
        };

    private static Dictionary<string, object> ObjectPayload(BsonDocument document, string kind, IDictionary<string, int> layerOrder)
    {
        var layerId = First(Text(document, "LayerId"), Text(document, "TileLayerId"));
        return new Dictionary<string, object>
        {
            ["id"] = Text(document, "Id"), ["kind"] = kind, ["name"] = Text(document, "DisplayName", kind == "tilePatch" ? Text(document, "MaterialKey", "Материал") : "Объект"),
            ["layerId"] = layerId, ["layerOrder"] = layerOrder.TryGetValue(layerId, out var order) ? order : 0,
            ["x"] = Double(document, "X"), ["y"] = Double(document, "Y"), ["width"] = Double(document, "Width"), ["height"] = Double(document, "Height"),
            ["rotationDegrees"] = Double(document, "RotationDegrees"), ["zIndex"] = Int(document, "ZIndex", Int(document, "SortOrder")),
            ["visibility"] = Text(document, "Visibility", "Hidden"), ["revision"] = Long(document, "Revision", 1L), ["isArchived"] = Bool(document, "IsArchived")
        };
    }

    private void SaveOperation(MapEditorMutationRequest0203 request, MapEditorMutationResult0203 result, DateTime now)
    {
        var document = new BsonDocument
        {
            ["_id"] = request.OperationId, ["OperationId"] = request.OperationId, ["CanonicalMapId"] = result.CanonicalMapId,
            ["Mutation"] = result.Mutation, ["TargetId"] = result.TargetId, ["MapRevision"] = result.MapRevision,
            ["LayerRevision"] = result.LayerRevision, ["ObjectRevision"] = result.ObjectRevision, ["ActorUserId"] = request.ActorUserId,
            ["CreatedAtUtc"] = now
        };
        Operations().InsertOne(document);
    }

    private static MapEditorMutationResult0203 ResultFromOperation(BsonDocument operation, bool replay)
        => new MapEditorMutationResult0203
        {
            IsReplay = replay, CanonicalMapId = Text(operation, "CanonicalMapId"), Mutation = Text(operation, "Mutation"), TargetId = Text(operation, "TargetId"),
            MapRevision = Long(operation, "MapRevision"), LayerRevision = Long(operation, "LayerRevision"), ObjectRevision = Long(operation, "ObjectRevision")
        };

    private void EnsureIndexes()
    {
        Operations().Indexes.CreateOne(new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("OperationId"), new CreateIndexOptions { Unique = true }));
        Operations().Indexes.CreateOne(new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("CanonicalMapId").Descending("CreatedAtUtc")));
    }

    private IMongoCollection<BsonDocument> Collection(string name) => _mongo.Database.GetCollection<BsonDocument>(name);
    private IMongoCollection<BsonDocument> Operations() => Collection(OperationsCollection);
    private static FilterDefinition<BsonDocument> IdFilter(string id, bool includeArchived)
    {
        var filter = Builders<BsonDocument>.Filter.Or(Builders<BsonDocument>.Filter.Eq("Id", id), Builders<BsonDocument>.Filter.Eq("_id", id));
        return includeArchived ? filter : filter & Builders<BsonDocument>.Filter.Ne("IsArchived", true);
    }
    private static string[] MapIds(MapIdentityResolution0202 resolution)
        => new[] { resolution.CanonicalMapId, resolution.LegacyMapId }.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    private static string Prefix(string kind) => kind == "shape" ? "scene_shape_" : kind == "tilepatch" ? "scene_tile_patch_" : "scene_asset_";
    private static string Required(string? value, string field) => string.IsNullOrWhiteSpace(value) ? throw Failure("validation", field + " обязателен.") : value.Trim();
    private static string Limit(string value, int minimum, int maximum, string field) => value.Length < minimum || value.Length > maximum ? throw Failure("validation", $"{field}: допустимая длина {minimum}-{maximum}.") : value;
    private static void RequireExpected(long? expected, long current, string name)
    {
        if (!expected.HasValue) throw Failure("validation", $"Для изменения требуется редакция: {name}.");
        if (expected.Value != current) throw Failure("conflict", $"{name} изменён другим редактором. Текущая редакция: {current}; ожидалась: {expected.Value}.");
    }
    private static MapEditorMutationException0203 Failure(string kind, string message) => new MapEditorMutationException0203(kind, message);
    private static string First(params string[] values) => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    private static string Text(BsonDocument? doc, string key, string fallback = "") => doc != null && doc.TryGetValue(key, out var value) && !value.IsBsonNull ? value.ToString() : fallback;
    private static bool Bool(BsonDocument doc, string key) => doc.TryGetValue(key, out var value) && (value.IsBoolean ? value.AsBoolean : bool.TryParse(value.ToString(), out var parsed) && parsed);
    private static long Long(BsonDocument doc, string key, long fallback = 0L) => doc.TryGetValue(key, out var value) && value.IsNumeric ? value.ToInt64() : fallback;
    private static int Int(BsonDocument doc, string key, int fallback = 0) => doc.TryGetValue(key, out var value) && value.IsNumeric ? value.ToInt32() : fallback;
    private static double Double(BsonDocument doc, string key, double fallback = 0d) => doc.TryGetValue(key, out var value) && value.IsNumeric ? value.ToDouble() : fallback;
    private static string ValueText(IDictionary<string, object> values, string key, string fallback = "") => values.TryGetValue(key, out var value) && value != null ? Convert.ToString(value, CultureInfo.InvariantCulture) ?? fallback : fallback;
    private static bool ValueBool(IDictionary<string, object> values, string key, bool fallback) => values.TryGetValue(key, out var value) && value != null ? Convert.ToBoolean(value, CultureInfo.InvariantCulture) : fallback;
    private static int ValueInt(IDictionary<string, object> values, string key, int fallback) => values.TryGetValue(key, out var value) && value != null ? Convert.ToInt32(value, CultureInfo.InvariantCulture) : fallback;
    private static double ValueDouble(IDictionary<string, object> values, string key, double fallback) => values.TryGetValue(key, out var value) && value != null ? Convert.ToDouble(value, CultureInfo.InvariantCulture) : fallback;
}
