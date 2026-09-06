using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Bson;
using MongoDB.Driver;
using Nri.Server.Infrastructure;
using Nri.Shared.Domain;

namespace Nri.Server.Application;

public interface IMapIdentityResolver
{
    MapIdentityResolution0202 ResolveSceneMap(string suppliedMapId, bool includeArchived = false);
    MapIdentityResolution0202 ResolveWorldMap(string suppliedMapId, bool includeArchived = false);
    BsonDocument SynchronizeSceneProjection(MapCanvasState canonicalMap, string legacyMapId, string actorUserId, BsonDocument? seed = null);
    BsonDocument SynchronizeWorldProjection(MapCanvasState canonicalMap, string legacyMapId, string actorUserId, BsonDocument? seed = null);
}

public enum MapIdentityResolutionStatus0202
{
    Resolved,
    NotFound,
    Archived,
    Conflict,
    StaleProjection
}

public sealed class MapIdentityResolution0202
{
    public MapIdentityResolutionStatus0202 Status { get; set; }
    public string SuppliedMapId { get; set; } = string.Empty;
    public string CanonicalMapId { get; set; } = string.Empty;
    public string LegacyMapId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public MapCanvasState? CanonicalMap { get; set; }
    public BsonDocument? CompatibilityProjection { get; set; }
    public bool IsResolved => Status == MapIdentityResolutionStatus0202.Resolved;
}

public sealed class MapIdentityAdapter0202 : IMapIdentityResolver
{
    public const string MappingCollectionName = "map_identity_mappings";
    public const string SceneProjectionCollectionName = "scene_map_definitions";
    public const string WorldProjectionCollectionName = "world_map_definitions";
    private readonly IMongoCollection<MapCanvasState> _canonicalMaps;
    private readonly IMongoCollection<BsonDocument> _projections;
    private readonly IMongoCollection<BsonDocument> _worldProjections;
    private readonly IMongoCollection<BsonDocument> _mappings;

    public MapIdentityAdapter0202(MongoContext mongo)
    {
        _canonicalMaps = mongo.MapCanvases;
        _projections = mongo.Database.GetCollection<BsonDocument>(SceneProjectionCollectionName);
        _worldProjections = mongo.Database.GetCollection<BsonDocument>(WorldProjectionCollectionName);
        _mappings = mongo.Database.GetCollection<BsonDocument>(MappingCollectionName);
        EnsureIndexes();
    }

    public MapIdentityResolution0202 ResolveSceneMap(string suppliedMapId, bool includeArchived = false)
    {
        suppliedMapId = (suppliedMapId ?? string.Empty).Trim();
        if (suppliedMapId.Length == 0)
            return Result(MapIdentityResolutionStatus0202.NotFound, suppliedMapId, "Map identity is required.");

        var directCanonical = _canonicalMaps.Find(Builders<MapCanvasState>.Filter.Eq(x => x.Id, suppliedMapId)).FirstOrDefault();
        var directProjection = _projections.Find(IdFilter(suppliedMapId)).FirstOrDefault();
        var mappingFilter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Ne("IsArchived", true),
            Builders<BsonDocument>.Filter.Or(
                Builders<BsonDocument>.Filter.Eq("CanonicalMapId", suppliedMapId),
                Builders<BsonDocument>.Filter.Eq("LegacyMapId", suppliedMapId)));
        var mappings = _mappings.Find(mappingFilter).Limit(10).ToList();

        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (directCanonical != null) candidates.Add(directCanonical.Id);
        foreach (var mapping in mappings)
        {
            var id = DocString(mapping, "CanonicalMapId");
            if (id.Length > 0) candidates.Add(id);
        }
        if (directProjection != null)
        {
            var projectedCanonical = DocString(directProjection, "CanonicalMapId");
            if (projectedCanonical.Length > 0) candidates.Add(projectedCanonical);
            else if (directCanonical != null) candidates.Add(directCanonical.Id);
        }

        var candidateStatus = MapIdentityDecision0202.Evaluate(candidates, canonicalExists: true, archived: false, conflictingProjection: false, staleProjection: false);
        if (candidateStatus == MapIdentityResolutionStatus0202.NotFound)
            return Result(candidateStatus, suppliedMapId, "Map identity was not found.");
        if (candidateStatus == MapIdentityResolutionStatus0202.Conflict)
            return Result(candidateStatus, suppliedMapId, "Map identity resolves to multiple canonical maps.");

        var canonicalId = candidates.Single();
        var canonical = directCanonical != null && string.Equals(directCanonical.Id, canonicalId, StringComparison.OrdinalIgnoreCase)
            ? directCanonical
            : _canonicalMaps.Find(Builders<MapCanvasState>.Filter.Eq(x => x.Id, canonicalId)).FirstOrDefault();
        if (canonical == null)
            return Result(MapIdentityResolutionStatus0202.Conflict, suppliedMapId, "Map identity mapping points to a missing canonical map.", canonicalId);
        if (!string.Equals(canonical.MapType, MapTypeIds.Scene, StringComparison.OrdinalIgnoreCase))
            return Result(MapIdentityResolutionStatus0202.Conflict, suppliedMapId, "Map identity points to a non-scene map.", canonicalId);
        if (!includeArchived && (canonical.Deleted || canonical.Archived || canonical.IsArchived))
            return Result(MapIdentityResolutionStatus0202.Archived, suppliedMapId, "Canonical map is archived.", canonicalId);

        var mappedLegacyIds = mappings
            .Where(mapping => string.Equals(DocString(mapping, "CanonicalMapId"), canonicalId, StringComparison.OrdinalIgnoreCase))
            .Select(mapping => DocString(mapping, "LegacyMapId"))
            .Where(id => id.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (mappedLegacyIds.Length > 1)
            return Result(MapIdentityResolutionStatus0202.Conflict, suppliedMapId, "Canonical map has multiple active compatibility projections.", canonicalId);

        var legacyId = directProjection != null
            ? DocString(directProjection, "Id", suppliedMapId)
            : mappedLegacyIds.FirstOrDefault() ?? canonicalId;
        var projection = directProjection;
        if (projection == null && legacyId.Length > 0)
            projection = _projections.Find(IdFilter(legacyId)).FirstOrDefault();
        if (projection != null)
        {
            var projectionCanonical = DocString(projection, "CanonicalMapId");
            if (projectionCanonical.Length > 0 && !string.Equals(projectionCanonical, canonicalId, StringComparison.OrdinalIgnoreCase))
                return Result(MapIdentityResolutionStatus0202.Conflict, suppliedMapId, "Compatibility projection points to another canonical map.", canonicalId, legacyId);
            if (projection.TryGetValue("CanonicalUpdatedAtUtc", out var snapshot) && snapshot.IsValidDateTime
                && canonical.UpdatedAtUtc.ToUniversalTime() > snapshot.ToUniversalTime().AddMilliseconds(1))
                return Result(MapIdentityResolutionStatus0202.StaleProjection, suppliedMapId, "Compatibility projection is stale.", canonicalId, legacyId);
            if (!includeArchived && DocBool(projection, "IsArchived"))
                return Result(MapIdentityResolutionStatus0202.Archived, suppliedMapId, "Compatibility projection is archived.", canonicalId, legacyId);
        }

        return new MapIdentityResolution0202
        {
            Status = MapIdentityResolutionStatus0202.Resolved,
            SuppliedMapId = suppliedMapId,
            CanonicalMapId = canonicalId,
            LegacyMapId = legacyId,
            CanonicalMap = canonical,
            CompatibilityProjection = projection,
            Message = "Map identity resolved."
        };
    }

    public BsonDocument SynchronizeSceneProjection(MapCanvasState canonicalMap, string legacyMapId, string actorUserId, BsonDocument? seed = null)
    {
        if (canonicalMap == null) throw new ArgumentNullException(nameof(canonicalMap));
        if (!string.Equals(canonicalMap.MapType, MapTypeIds.Scene, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only scene maps can use the scene compatibility projection.");
        legacyMapId = string.IsNullOrWhiteSpace(legacyMapId) ? canonicalMap.Id : legacyMapId.Trim();
        var conflicts = _mappings.Find(Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Ne("IsArchived", true),
            Builders<BsonDocument>.Filter.Or(
                Builders<BsonDocument>.Filter.And(Builders<BsonDocument>.Filter.Eq("LegacyMapId", legacyMapId), Builders<BsonDocument>.Filter.Ne("CanonicalMapId", canonicalMap.Id)),
                Builders<BsonDocument>.Filter.And(Builders<BsonDocument>.Filter.Eq("CanonicalMapId", canonicalMap.Id), Builders<BsonDocument>.Filter.Ne("LegacyMapId", legacyMapId))))).Limit(1).FirstOrDefault();
        if (conflicts != null)
            throw new InvalidOperationException("Map identity mapping conflicts with an existing active projection.");

        var now = DateTime.UtcNow;
        var projection = seed != null ? new BsonDocument(seed) : _projections.Find(IdFilter(legacyMapId)).FirstOrDefault() ?? new BsonDocument();
        projection["_id"] = legacyMapId;
        projection["Id"] = legacyMapId;
        projection["CanonicalMapId"] = canonicalMap.Id;
        projection["DisplayName"] = canonicalMap.Name;
        projection["Description"] = canonicalMap.Description ?? string.Empty;
        projection["CampaignId"] = canonicalMap.CampaignId ?? string.Empty;
        projection["RuleSetId"] = canonicalMap.RuleSetId ?? string.Empty;
        projection["WidthMeters"] = canonicalMap.WidthMeters;
        projection["HeightMeters"] = canonicalMap.HeightMeters;
        projection["GridSizeMeters"] = canonicalMap.GridCellSizeMeters;
        projection["ShowGrid"] = ExtraBool(canonicalMap.ExtraData, "showGrid", true);
        projection["ShowCoordinates"] = ExtraBool(canonicalMap.ExtraData, "showCoordinates", true);
        projection["IsArchived"] = canonicalMap.IsArchived || canonicalMap.Archived || canonicalMap.Deleted;
        projection["CanonicalUpdatedAtUtc"] = canonicalMap.UpdatedAtUtc.ToUniversalTime();
        projection["CompatibilityProjectionVersion"] = 1;
        projection["UpdatedAtUtc"] = now;
        projection["UpdatedByUserId"] = actorUserId ?? string.Empty;
        if (!projection.Contains("CreatedAtUtc")) projection["CreatedAtUtc"] = now;
        _projections.ReplaceOne(IdFilter(legacyMapId), projection, new ReplaceOptions { IsUpsert = true });

        var mappingId = "scene:" + legacyMapId;
        var mapping = new BsonDocument
        {
            ["_id"] = mappingId,
            ["Id"] = mappingId,
            ["MapKind"] = "scene",
            ["CanonicalMapId"] = canonicalMap.Id,
            ["LegacyMapId"] = legacyMapId,
            ["ProjectionCollection"] = SceneProjectionCollectionName,
            ["CampaignId"] = canonicalMap.CampaignId ?? string.Empty,
            ["RuleSetId"] = canonicalMap.RuleSetId ?? string.Empty,
            ["IsArchived"] = false,
            ["UpdatedAtUtc"] = now,
            ["UpdatedByUserId"] = actorUserId ?? string.Empty
        };
        _mappings.ReplaceOne(IdFilter(mappingId), mapping, new ReplaceOptions { IsUpsert = true });
        return projection;
    }

    public MapIdentityResolution0202 ResolveWorldMap(string suppliedMapId, bool includeArchived = false)
    {
        suppliedMapId = (suppliedMapId ?? string.Empty).Trim();
        if (suppliedMapId.Length == 0) return Result(MapIdentityResolutionStatus0202.NotFound, suppliedMapId, "Map identity is required.");
        var directCanonical = _canonicalMaps.Find(Builders<MapCanvasState>.Filter.Eq(x => x.Id, suppliedMapId)).FirstOrDefault();
        var directProjection = _worldProjections.Find(IdFilter(suppliedMapId)).FirstOrDefault();
        var mappings = _mappings.Find(Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Ne("IsArchived", true),
            Builders<BsonDocument>.Filter.Eq("MapKind", "world"),
            Builders<BsonDocument>.Filter.Or(Builders<BsonDocument>.Filter.Eq("CanonicalMapId", suppliedMapId), Builders<BsonDocument>.Filter.Eq("LegacyMapId", suppliedMapId)))).Limit(10).ToList();
        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (directCanonical != null) candidates.Add(directCanonical.Id);
        foreach (var mapping in mappings)
        {
            var id = DocString(mapping, "CanonicalMapId");
            if (id.Length > 0) candidates.Add(id);
        }
        if (directProjection != null)
        {
            var id = DocString(directProjection, "CanonicalMapId");
            if (id.Length > 0) candidates.Add(id);
            else if (directCanonical != null) candidates.Add(directCanonical.Id);
        }
        var status = MapIdentityDecision0202.Evaluate(candidates, true, false, false, false);
        if (status == MapIdentityResolutionStatus0202.NotFound) return Result(status, suppliedMapId, "Map identity was not found.");
        if (status == MapIdentityResolutionStatus0202.Conflict) return Result(status, suppliedMapId, "Map identity resolves to multiple canonical maps.");
        var canonicalId = candidates.Single();
        var canonical = directCanonical != null && string.Equals(directCanonical.Id, canonicalId, StringComparison.OrdinalIgnoreCase)
            ? directCanonical : _canonicalMaps.Find(Builders<MapCanvasState>.Filter.Eq(x => x.Id, canonicalId)).FirstOrDefault();
        if (canonical == null) return Result(MapIdentityResolutionStatus0202.Conflict, suppliedMapId, "Map identity mapping points to a missing canonical map.", canonicalId);
        if (!string.Equals(canonical.MapType, MapTypeIds.World, StringComparison.OrdinalIgnoreCase) && !string.Equals(canonical.MapType, MapTypeIds.WorldMap, StringComparison.OrdinalIgnoreCase))
            return Result(MapIdentityResolutionStatus0202.Conflict, suppliedMapId, "Map identity points to a non-world map.", canonicalId);
        if (!includeArchived && (canonical.Deleted || canonical.Archived || canonical.IsArchived))
            return Result(MapIdentityResolutionStatus0202.Archived, suppliedMapId, "Canonical map is archived.", canonicalId);
        var legacyIds = mappings.Where(x => string.Equals(DocString(x, "CanonicalMapId"), canonicalId, StringComparison.OrdinalIgnoreCase))
            .Select(x => DocString(x, "LegacyMapId")).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (legacyIds.Length > 1) return Result(MapIdentityResolutionStatus0202.Conflict, suppliedMapId, "Canonical map has multiple active compatibility projections.", canonicalId);
        var legacyId = directProjection != null ? DocString(directProjection, "Id", suppliedMapId) : legacyIds.FirstOrDefault() ?? canonicalId;
        var projection = directProjection ?? _worldProjections.Find(IdFilter(legacyId)).FirstOrDefault();
        if (projection != null)
        {
            var projectedCanonical = DocString(projection, "CanonicalMapId");
            if (projectedCanonical.Length > 0 && !string.Equals(projectedCanonical, canonicalId, StringComparison.OrdinalIgnoreCase))
                return Result(MapIdentityResolutionStatus0202.Conflict, suppliedMapId, "Compatibility projection points to another canonical map.", canonicalId, legacyId);
            if (projection.TryGetValue("CanonicalUpdatedAtUtc", out var snapshot) && snapshot.IsValidDateTime && canonical.UpdatedAtUtc.ToUniversalTime() > snapshot.ToUniversalTime().AddMilliseconds(1))
                return Result(MapIdentityResolutionStatus0202.StaleProjection, suppliedMapId, "Compatibility projection is stale.", canonicalId, legacyId);
            if (!includeArchived && DocBool(projection, "IsArchived")) return Result(MapIdentityResolutionStatus0202.Archived, suppliedMapId, "Compatibility projection is archived.", canonicalId, legacyId);
        }
        return new MapIdentityResolution0202 { Status = MapIdentityResolutionStatus0202.Resolved, SuppliedMapId = suppliedMapId, CanonicalMapId = canonicalId, LegacyMapId = legacyId, CanonicalMap = canonical, CompatibilityProjection = projection, Message = "Map identity resolved." };
    }

    public BsonDocument SynchronizeWorldProjection(MapCanvasState canonicalMap, string legacyMapId, string actorUserId, BsonDocument? seed = null)
    {
        if (canonicalMap == null) throw new ArgumentNullException(nameof(canonicalMap));
        if (!string.Equals(canonicalMap.MapType, MapTypeIds.World, StringComparison.OrdinalIgnoreCase) && !string.Equals(canonicalMap.MapType, MapTypeIds.WorldMap, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only world maps can use the world compatibility projection.");
        legacyMapId = string.IsNullOrWhiteSpace(legacyMapId) ? canonicalMap.Id : legacyMapId.Trim();
        var conflicts = _mappings.Find(Builders<BsonDocument>.Filter.And(Builders<BsonDocument>.Filter.Ne("IsArchived", true), Builders<BsonDocument>.Filter.Eq("MapKind", "world"),
            Builders<BsonDocument>.Filter.Or(
                Builders<BsonDocument>.Filter.And(Builders<BsonDocument>.Filter.Eq("LegacyMapId", legacyMapId), Builders<BsonDocument>.Filter.Ne("CanonicalMapId", canonicalMap.Id)),
                Builders<BsonDocument>.Filter.And(Builders<BsonDocument>.Filter.Eq("CanonicalMapId", canonicalMap.Id), Builders<BsonDocument>.Filter.Ne("LegacyMapId", legacyMapId))))).Limit(1).FirstOrDefault();
        if (conflicts != null) throw new InvalidOperationException("Map identity mapping conflicts with an existing active projection.");
        var now = DateTime.UtcNow;
        var projection = seed != null ? new BsonDocument(seed) : _worldProjections.Find(IdFilter(legacyMapId)).FirstOrDefault() ?? new BsonDocument();
        projection["_id"] = legacyMapId; projection["Id"] = legacyMapId; projection["CanonicalMapId"] = canonicalMap.Id;
        projection["DisplayName"] = canonicalMap.Name; projection["Description"] = canonicalMap.Description ?? string.Empty;
        projection["CampaignId"] = canonicalMap.CampaignId ?? string.Empty; projection["RuleSetId"] = canonicalMap.RuleSetId ?? string.Empty;
        projection["WidthUnits"] = ExtraInt(canonicalMap.ExtraData, "widthUnits", Math.Max(1, canonicalMap.WidthMeters));
        projection["HeightUnits"] = ExtraInt(canonicalMap.ExtraData, "heightUnits", Math.Max(1, canonicalMap.HeightMeters));
        projection["GridSizeUnits"] = ExtraInt(canonicalMap.ExtraData, "gridSizeUnits", Math.Max(1, canonicalMap.GridCellSizeMeters));
        projection["IsArchived"] = canonicalMap.IsArchived || canonicalMap.Archived || canonicalMap.Deleted;
        projection["CanonicalUpdatedAtUtc"] = canonicalMap.UpdatedAtUtc.ToUniversalTime(); projection["CompatibilityProjectionVersion"] = 1;
        projection["UpdatedAtUtc"] = now; projection["UpdatedByUserId"] = actorUserId ?? string.Empty;
        if (!projection.Contains("CreatedAtUtc")) projection["CreatedAtUtc"] = now;
        _worldProjections.ReplaceOne(IdFilter(legacyMapId), projection, new ReplaceOptions { IsUpsert = true });
        var mappingId = "world:" + legacyMapId;
        var mapping = new BsonDocument { ["_id"] = mappingId, ["Id"] = mappingId, ["MapKind"] = "world", ["CanonicalMapId"] = canonicalMap.Id, ["LegacyMapId"] = legacyMapId,
            ["ProjectionCollection"] = WorldProjectionCollectionName, ["CampaignId"] = canonicalMap.CampaignId ?? string.Empty, ["RuleSetId"] = canonicalMap.RuleSetId ?? string.Empty,
            ["IsArchived"] = false, ["UpdatedAtUtc"] = now, ["UpdatedByUserId"] = actorUserId ?? string.Empty };
        _mappings.ReplaceOne(IdFilter(mappingId), mapping, new ReplaceOptions { IsUpsert = true });
        return projection;
    }

    private void EnsureIndexes()
    {
        _mappings.Indexes.CreateMany(new[]
        {
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("CanonicalMapId")),
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("LegacyMapId")),
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("MapKind").Ascending("IsArchived"))
        });
    }

    private static MapIdentityResolution0202 Result(MapIdentityResolutionStatus0202 status, string supplied, string message, string canonical = "", string legacy = "")
        => new() { Status = status, SuppliedMapId = supplied, CanonicalMapId = canonical, LegacyMapId = legacy, Message = message };

    private static FilterDefinition<BsonDocument> IdFilter(string id)
        => Builders<BsonDocument>.Filter.Or(Builders<BsonDocument>.Filter.Eq("_id", id), Builders<BsonDocument>.Filter.Eq("Id", id));

    private static string DocString(BsonDocument document, string name, string fallback = "")
        => document.TryGetValue(name, out var value) && !value.IsBsonNull ? value.ToString() : fallback;

    private static bool DocBool(BsonDocument document, string name)
        => document.TryGetValue(name, out var value) && value.IsBoolean && value.AsBoolean;

    private static bool ExtraBool(IDictionary<string, object>? extra, string key, bool fallback)
    {
        if (extra == null || !extra.TryGetValue(key, out var value) || value == null) return fallback;
        return value is bool flag ? flag : bool.TryParse(value.ToString(), out var parsed) ? parsed : fallback;
    }

    private static int ExtraInt(IDictionary<string, object>? extra, string key, int fallback)
    {
        if (extra == null || !extra.TryGetValue(key, out var value) || value == null) return fallback;
        return value is int number ? number : int.TryParse(value.ToString(), out var parsed) ? parsed : fallback;
    }
}

public static class MapIdentityDecision0202
{
    public static MapIdentityResolutionStatus0202 Evaluate(
        IEnumerable<string> canonicalCandidates,
        bool canonicalExists,
        bool archived,
        bool conflictingProjection,
        bool staleProjection)
    {
        var count = (canonicalCandidates ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(2)
            .Count();
        if (count == 0) return MapIdentityResolutionStatus0202.NotFound;
        if (count > 1 || !canonicalExists || conflictingProjection) return MapIdentityResolutionStatus0202.Conflict;
        if (archived) return MapIdentityResolutionStatus0202.Archived;
        if (staleProjection) return MapIdentityResolutionStatus0202.StaleProjection;
        return MapIdentityResolutionStatus0202.Resolved;
    }
}
