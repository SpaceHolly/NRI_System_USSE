using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MongoDB.Driver;
using Nri.Server.Logging;
using Nri.Shared.Domain;

namespace Nri.Server.Infrastructure.Mongo.Repositories;

public interface IMapSpaceNodeRepository
{
    Task<MapSpaceNodeState?> GetByIdAsync(string id);
    Task<IReadOnlyCollection<MapSpaceNodeState>> ListByCampaignAsync(string campaignId, int limit = 200);
    Task<IReadOnlyCollection<MapSpaceNodeState>> ListByParentAsync(string campaignId, string parentId, int limit = 200);
    Task<MapSpaceNodeState> UpsertAsync(MapSpaceNodeState node);
    Task<bool> ArchiveAsync(string id);
}

public interface IMapCanvasRepository
{
    Task<MapCanvasState?> GetByIdAsync(string id);
    Task<IReadOnlyCollection<MapCanvasState>> ListByCampaignAsync(string campaignId, bool includeArchived = false, int limit = 200);
    Task<IReadOnlyCollection<MapCanvasState>> ListBySpaceNodeAsync(string campaignId, string spaceNodeId, bool includeArchived = false, int limit = 200);
    Task<MapCanvasState> UpsertAsync(MapCanvasState map);
    Task<bool> ArchiveAsync(string id);
}

public interface IRoomInteriorRepository
{
    Task<RoomInteriorState?> GetByIdAsync(string id);
    Task<IReadOnlyCollection<RoomInteriorState>> ListByCampaignAsync(string campaignId, bool includeArchived = false, int limit = 200);
    Task<IReadOnlyCollection<RoomInteriorState>> ListByParentAsync(string campaignId, string parentLocationId, string parentSceneMapId, bool includeArchived = false, int limit = 200);
    Task<RoomInteriorState> UpsertAsync(RoomInteriorState room);
    Task<bool> ArchiveAsync(string id);
}

public interface IWorldMapStateRepository
{
    Task<WorldMapState?> GetByIdAsync(string id);
    Task<IReadOnlyCollection<WorldMapState>> ListByCampaignAsync(string campaignId, bool includeArchived = false, int limit = 200);
    Task<WorldMapState> UpsertAsync(WorldMapState map);
    Task<bool> ArchiveAsync(string id);
}

public interface IMapMarkerRepository
{
    Task<MapMarkerState?> GetByIdAsync(string id);
    Task<IReadOnlyCollection<MapMarkerState>> ListByMapAsync(string mapId, bool includeArchived = false, int limit = 500);
    Task<MapMarkerState> UpsertAsync(MapMarkerState marker);
    Task<bool> ArchiveAsync(string id);
}

public interface IMapMarkerBindingRepository
{
    Task<MapMarkerBindingState?> GetByIdAsync(string id);
    Task<IReadOnlyCollection<MapMarkerBindingState>> ListByMapAsync(string mapId, int limit = 500);
    Task<IReadOnlyCollection<MapMarkerBindingState>> ListByMarkerAsync(string markerId, int limit = 200);
    Task<MapMarkerBindingState> UpsertAsync(MapMarkerBindingState binding);
    Task<bool> ArchiveAsync(string id);
}

public interface IWorldMapLayerRepository
{
    Task<WorldMapLayerState?> GetByIdAsync(string id);
    Task<IReadOnlyCollection<WorldMapLayerState>> ListByMapAsync(string worldMapId, bool includeArchived = false, int limit = 1000);
    Task<IReadOnlyCollection<WorldMapLayerState>> ListByMapAndTypeAsync(string worldMapId, string layerType, bool includeArchived = false, int limit = 1000);
    Task<WorldMapLayerState> UpsertAsync(WorldMapLayerState layer);
    Task<bool> ArchiveAsync(string id);
}

public interface IWorldMapLegendRepository
{
    Task<WorldMapLegendState?> GetByIdAsync(string id);
    Task<IReadOnlyCollection<WorldMapLegendState>> ListByMapAsync(string mapId, int limit = 200);
    Task<WorldMapLegendState> UpsertAsync(WorldMapLegendState legend);
    Task<bool> ArchiveAsync(string id);
}

public interface IMapFogLayerRepository
{
    Task<FogOfWarState?> GetByMapIdAsync(string mapId);
    Task<FogOfWarState> UpsertAsync(FogOfWarState fog);
}

public interface ISceneMapActiveLinkRepository
{
    Task<SceneMapActiveLinkState?> GetActiveByScopeAsync(string campaignId, string sessionId, string activeGroupId, string sceneId);
    Task<IReadOnlyCollection<SceneMapActiveLinkState>> ListByCampaignAsync(string campaignId, int limit = 100);
    Task<int> DeactivateScopeAsync(string campaignId, string sessionId, string activeGroupId, string sceneId);
    Task<SceneMapActiveLinkState> UpsertAsync(SceneMapActiveLinkState link);
}

internal static class MapRepositoryLimits
{
    public static int Clamp(int limit, int min = 1, int max = 1000)
    {
        return Math.Max(min, Math.Min(limit, max));
    }
}

public abstract class MapEntityRepository<T> where T : EntityBase
{
    private readonly IMongoCollection<T> _collection;
    private readonly IServerLogger? _logger;
    private readonly string _type;

    protected MapEntityRepository(IMongoCollection<T> collection, IServerLogger? logger, string type)
    {
        _collection = collection;
        _logger = logger;
        _type = type;
    }

    protected IMongoCollection<T> Collection => _collection;
    protected IServerLogger? Logger => _logger;
    protected string Type => _type;

    public async Task<T?> GetByIdAsync(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        return await _collection.Find(x => x.Id == id && !x.Deleted).FirstOrDefaultAsync();
    }

    protected async Task<T> UpsertEntityAsync(T entity, string contextId)
    {
        if (entity == null) throw new ArgumentNullException(nameof(entity));
        var now = DateTime.UtcNow;
        if (entity.CreatedUtc == default) entity.CreatedUtc = now;
        entity.UpdatedUtc = now;
        if (entity.SchemaVersion < 1) entity.SchemaVersion = 1;
        await _collection.ReplaceOneAsync(x => x.Id == entity.Id, entity, new ReplaceOptions { IsUpsert = true });
        _logger?.Debug($"map.repository.upsert type={_type} id={entity.Id} contextId={contextId}");
        return entity;
    }

    public async Task<bool> ArchiveAsync(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;
        var update = Builders<T>.Update
            .Set(x => x.Archived, true)
            .Set(x => x.UpdatedUtc, DateTime.UtcNow);
        var result = await _collection.UpdateOneAsync(x => x.Id == id, update);
        _logger?.Debug($"map.repository.archive type={_type} id={id}");
        return result.ModifiedCount > 0;
    }
}

public sealed class MapSpaceNodeRepository : MapEntityRepository<MapSpaceNodeState>, IMapSpaceNodeRepository
{
    public MapSpaceNodeRepository(IMongoCollection<MapSpaceNodeState> collection, IServerLogger? logger = null)
        : base(collection, logger, "space_node")
    {
    }

    public async Task<IReadOnlyCollection<MapSpaceNodeState>> ListByCampaignAsync(string campaignId, int limit = 200)
    {
        var safeLimit = MapRepositoryLimits.Clamp(limit, 1, 2000);
        return await Collection.Find(x => x.CampaignId == (campaignId ?? string.Empty) && !x.Deleted && !x.Archived)
            .SortBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .Limit(safeLimit)
            .ToListAsync();
    }

    public async Task<IReadOnlyCollection<MapSpaceNodeState>> ListByParentAsync(string campaignId, string parentId, int limit = 200)
    {
        var safeLimit = MapRepositoryLimits.Clamp(limit, 1, 2000);
        return await Collection.Find(x =>
                x.CampaignId == (campaignId ?? string.Empty)
                && x.ParentId == (parentId ?? string.Empty)
                && !x.Deleted
                && !x.Archived)
            .SortBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .Limit(safeLimit)
            .ToListAsync();
    }

    public Task<MapSpaceNodeState> UpsertAsync(MapSpaceNodeState node)
    {
        return UpsertEntityAsync(node, node?.CampaignId ?? string.Empty);
    }
}

public sealed class MapCanvasRepository : MapEntityRepository<MapCanvasState>, IMapCanvasRepository
{
    public MapCanvasRepository(IMongoCollection<MapCanvasState> collection, IServerLogger? logger = null)
        : base(collection, logger, "map_canvas")
    {
    }

    public async Task<IReadOnlyCollection<MapCanvasState>> ListByCampaignAsync(string campaignId, bool includeArchived = false, int limit = 200)
    {
        var safeLimit = MapRepositoryLimits.Clamp(limit, 1, 2000);
        return await Collection.Find(x => x.CampaignId == (campaignId ?? string.Empty) && !x.Deleted && (includeArchived || !x.Archived))
            .SortBy(x => x.Name)
            .Limit(safeLimit)
            .ToListAsync();
    }

    public async Task<IReadOnlyCollection<MapCanvasState>> ListBySpaceNodeAsync(string campaignId, string spaceNodeId, bool includeArchived = false, int limit = 200)
    {
        var safeLimit = MapRepositoryLimits.Clamp(limit, 1, 2000);
        return await Collection.Find(x =>
                x.CampaignId == (campaignId ?? string.Empty)
                && x.SpaceNodeId == (spaceNodeId ?? string.Empty)
                && !x.Deleted
                && (includeArchived || !x.Archived))
            .SortBy(x => x.Name)
            .Limit(safeLimit)
            .ToListAsync();
    }

    public Task<MapCanvasState> UpsertAsync(MapCanvasState map)
    {
        return UpsertEntityAsync(map, map?.SpaceNodeId ?? string.Empty);
    }
}

public sealed class RoomInteriorRepository : MapEntityRepository<RoomInteriorState>, IRoomInteriorRepository
{
    public RoomInteriorRepository(IMongoCollection<RoomInteriorState> collection, IServerLogger? logger = null)
        : base(collection, logger, "room_interior")
    {
    }

    public async Task<IReadOnlyCollection<RoomInteriorState>> ListByCampaignAsync(string campaignId, bool includeArchived = false, int limit = 200)
    {
        var safeLimit = MapRepositoryLimits.Clamp(limit, 1, 2000);
        return await Collection.Find(x =>
                x.CampaignId == (campaignId ?? string.Empty)
                && !x.Deleted
                && (includeArchived || !x.Archived))
            .SortBy(x => x.Name)
            .Limit(safeLimit)
            .ToListAsync();
    }

    public async Task<IReadOnlyCollection<RoomInteriorState>> ListByParentAsync(string campaignId, string parentLocationId, string parentSceneMapId, bool includeArchived = false, int limit = 200)
    {
        var safeLimit = MapRepositoryLimits.Clamp(limit, 1, 2000);
        var loc = parentLocationId ?? string.Empty;
        var scene = parentSceneMapId ?? string.Empty;
        return await Collection.Find(x =>
                x.CampaignId == (campaignId ?? string.Empty)
                && (string.IsNullOrWhiteSpace(loc) || x.ParentLocationId == loc)
                && (string.IsNullOrWhiteSpace(scene) || x.ParentSceneMapId == scene)
                && !x.Deleted
                && (includeArchived || !x.Archived))
            .SortBy(x => x.Name)
            .Limit(safeLimit)
            .ToListAsync();
    }

    public Task<RoomInteriorState> UpsertAsync(RoomInteriorState room)
    {
        return UpsertEntityAsync(room, room?.CampaignId ?? string.Empty);
    }
}

public sealed class WorldMapStateRepository : MapEntityRepository<WorldMapState>, IWorldMapStateRepository
{
    public WorldMapStateRepository(IMongoCollection<WorldMapState> collection, IServerLogger? logger = null)
        : base(collection, logger, "world_map")
    {
    }

    public async Task<IReadOnlyCollection<WorldMapState>> ListByCampaignAsync(string campaignId, bool includeArchived = false, int limit = 200)
    {
        var safeLimit = MapRepositoryLimits.Clamp(limit, 1, 2000);
        return await Collection.Find(x =>
                x.CampaignId == (campaignId ?? string.Empty)
                && !x.Deleted
                && (includeArchived || !x.Archived))
            .SortBy(x => x.Name)
            .Limit(safeLimit)
            .ToListAsync();
    }

    public Task<WorldMapState> UpsertAsync(WorldMapState map)
    {
        return UpsertEntityAsync(map, map?.CampaignId ?? string.Empty);
    }
}

public sealed class MapMarkerRepository : MapEntityRepository<MapMarkerState>, IMapMarkerRepository
{
    public MapMarkerRepository(IMongoCollection<MapMarkerState> collection, IServerLogger? logger = null)
        : base(collection, logger, "map_marker")
    {
    }

    public async Task<IReadOnlyCollection<MapMarkerState>> ListByMapAsync(string mapId, bool includeArchived = false, int limit = 500)
    {
        var safeLimit = MapRepositoryLimits.Clamp(limit, 1, 5000);
        return await Collection.Find(x => x.MapId == (mapId ?? string.Empty) && !x.Deleted && (includeArchived || !x.Archived))
            .SortBy(x => x.Layer)
            .ThenBy(x => x.Name)
            .Limit(safeLimit)
            .ToListAsync();
    }

    public Task<MapMarkerState> UpsertAsync(MapMarkerState marker)
    {
        return UpsertEntityAsync(marker, marker?.MapId ?? string.Empty);
    }
}

public sealed class MapFogLayerRepository : IMapFogLayerRepository
{
    private readonly IMongoCollection<FogOfWarState> _collection;
    private readonly IServerLogger? _logger;

    public MapFogLayerRepository(IMongoCollection<FogOfWarState> collection, IServerLogger? logger = null)
    {
        _collection = collection;
        _logger = logger;
    }

    public async Task<FogOfWarState?> GetByMapIdAsync(string mapId)
    {
        if (string.IsNullOrWhiteSpace(mapId)) return null;
        return await _collection.Find(x => x.MapId == mapId && !x.Deleted).FirstOrDefaultAsync();
    }

    public async Task<FogOfWarState> UpsertAsync(FogOfWarState fog)
    {
        if (fog == null) throw new ArgumentNullException(nameof(fog));
        if (string.IsNullOrWhiteSpace(fog.Id))
            fog.Id = $"fog:{fog.MapId}";
        var now = DateTime.UtcNow;
        if (fog.CreatedUtc == default) fog.CreatedUtc = now;
        fog.UpdatedUtc = now;
        fog.UpdatedAtUtc = now;
        if (fog.SchemaVersion < 1) fog.SchemaVersion = 1;
        await _collection.ReplaceOneAsync(x => x.MapId == fog.MapId, fog, new ReplaceOptions { IsUpsert = true });
        _logger?.Debug($"map.repository.upsert type=fog mapId={fog.MapId}");
        return fog;
    }
}

public sealed class SceneMapActiveLinkRepository : ISceneMapActiveLinkRepository
{
    private readonly IMongoCollection<SceneMapActiveLinkState> _collection;
    private readonly IServerLogger? _logger;

    public SceneMapActiveLinkRepository(IMongoCollection<SceneMapActiveLinkState> collection, IServerLogger? logger = null)
    {
        _collection = collection;
        _logger = logger;
    }

    public async Task<SceneMapActiveLinkState?> GetActiveByScopeAsync(string campaignId, string sessionId, string activeGroupId, string sceneId)
    {
        var campaign = campaignId ?? string.Empty;
        var session = sessionId ?? string.Empty;
        var group = activeGroupId ?? string.Empty;
        var scene = sceneId ?? string.Empty;
        return await _collection.Find(x =>
                x.CampaignId == campaign
                && x.SessionId == session
                && x.ActiveGroupId == group
                && x.SceneId == scene
                && x.IsActive
                && !x.Deleted
                && !x.Archived)
            .SortByDescending(x => x.UpdatedAtUtc)
            .FirstOrDefaultAsync();
    }

    public async Task<IReadOnlyCollection<SceneMapActiveLinkState>> ListByCampaignAsync(string campaignId, int limit = 100)
    {
        var safeLimit = MapRepositoryLimits.Clamp(limit, 1, 1000);
        return await _collection.Find(x =>
                x.CampaignId == (campaignId ?? string.Empty)
                && !x.Deleted
                && !x.Archived)
            .SortByDescending(x => x.UpdatedAtUtc)
            .Limit(safeLimit)
            .ToListAsync();
    }

    public async Task<int> DeactivateScopeAsync(string campaignId, string sessionId, string activeGroupId, string sceneId)
    {
        var campaign = campaignId ?? string.Empty;
        var session = sessionId ?? string.Empty;
        var group = activeGroupId ?? string.Empty;
        var scene = sceneId ?? string.Empty;
        var update = Builders<SceneMapActiveLinkState>.Update
            .Set(x => x.IsActive, false)
            .Set(x => x.Archived, true)
            .Set(x => x.UpdatedAtUtc, DateTime.UtcNow)
            .Set(x => x.UpdatedUtc, DateTime.UtcNow);
        var result = await _collection.UpdateManyAsync(x =>
            x.CampaignId == campaign
            && x.SessionId == session
            && x.ActiveGroupId == group
            && x.SceneId == scene
            && x.IsActive
            && !x.Deleted, update);
        _logger?.Debug($"map.repository.deactivate scope campaignId={campaign} sessionId={session} groupId={group} sceneId={scene} modified={result.ModifiedCount}");
        return (int)result.ModifiedCount;
    }

    public async Task<SceneMapActiveLinkState> UpsertAsync(SceneMapActiveLinkState link)
    {
        if (link == null) throw new ArgumentNullException(nameof(link));
        if (string.IsNullOrWhiteSpace(link.Id))
            link.Id = $"scene_active:{link.CampaignId}:{link.SessionId}:{link.ActiveGroupId}:{link.SceneId}:{Guid.NewGuid():N}";
        var now = DateTime.UtcNow;
        if (link.CreatedUtc == default) link.CreatedUtc = now;
        link.UpdatedUtc = now;
        link.UpdatedAtUtc = now;
        if (link.SchemaVersion < 1) link.SchemaVersion = 1;
        await _collection.ReplaceOneAsync(x => x.Id == link.Id, link, new ReplaceOptions { IsUpsert = true });
        _logger?.Debug($"map.repository.upsert type=scene_map_active id={link.Id} mapId={link.MapId}");
        return link;
    }
}

public sealed class MapMarkerBindingRepository : MapEntityRepository<MapMarkerBindingState>, IMapMarkerBindingRepository
{
    public MapMarkerBindingRepository(IMongoCollection<MapMarkerBindingState> collection, IServerLogger? logger = null)
        : base(collection, logger, "map_marker_binding")
    {
    }

    public async Task<IReadOnlyCollection<MapMarkerBindingState>> ListByMapAsync(string mapId, int limit = 500)
    {
        var safeLimit = MapRepositoryLimits.Clamp(limit, 1, 2000);
        return await Collection.Find(x => x.MapId == (mapId ?? string.Empty) && !x.Deleted && !x.Archived)
            .SortBy(x => x.MarkerId)
            .ThenBy(x => x.BindingType)
            .Limit(safeLimit)
            .ToListAsync();
    }

    public async Task<IReadOnlyCollection<MapMarkerBindingState>> ListByMarkerAsync(string markerId, int limit = 200)
    {
        var safeLimit = MapRepositoryLimits.Clamp(limit, 1, 1000);
        return await Collection.Find(x => x.MarkerId == (markerId ?? string.Empty) && !x.Deleted && !x.Archived)
            .SortBy(x => x.BindingType)
            .ThenBy(x => x.DisplayName)
            .Limit(safeLimit)
            .ToListAsync();
    }

    public Task<MapMarkerBindingState> UpsertAsync(MapMarkerBindingState binding)
    {
        return UpsertEntityAsync(binding, binding?.MarkerId ?? string.Empty);
    }
}

public sealed class WorldMapLayerRepository : MapEntityRepository<WorldMapLayerState>, IWorldMapLayerRepository
{
    public WorldMapLayerRepository(IMongoCollection<WorldMapLayerState> collection, IServerLogger? logger = null)
        : base(collection, logger, "world_map_layer")
    {
    }

    public async Task<IReadOnlyCollection<WorldMapLayerState>> ListByMapAsync(string worldMapId, bool includeArchived = false, int limit = 1000)
    {
        var safeLimit = MapRepositoryLimits.Clamp(limit, 1, 5000);
        return await Collection.Find(x =>
                x.WorldMapId == (worldMapId ?? string.Empty)
                && !x.Deleted
                && (includeArchived || !x.Archived))
            .SortBy(x => x.SortOrder)
            .ThenBy(x => x.LayerType)
            .Limit(safeLimit)
            .ToListAsync();
    }

    public async Task<IReadOnlyCollection<WorldMapLayerState>> ListByMapAndTypeAsync(string worldMapId, string layerType, bool includeArchived = false, int limit = 1000)
    {
        var safeLimit = MapRepositoryLimits.Clamp(limit, 1, 5000);
        return await Collection.Find(x =>
                x.WorldMapId == (worldMapId ?? string.Empty)
                && x.LayerType == (layerType ?? string.Empty)
                && !x.Deleted
                && (includeArchived || !x.Archived))
            .SortBy(x => x.SortOrder)
            .Limit(safeLimit)
            .ToListAsync();
    }

    public Task<WorldMapLayerState> UpsertAsync(WorldMapLayerState layer)
    {
        return UpsertEntityAsync(layer, layer?.WorldMapId ?? string.Empty);
    }
}

public sealed class WorldMapLegendRepository : MapEntityRepository<WorldMapLegendState>, IWorldMapLegendRepository
{
    public WorldMapLegendRepository(IMongoCollection<WorldMapLegendState> collection, IServerLogger? logger = null)
        : base(collection, logger, "world_map_legend")
    {
    }

    public async Task<IReadOnlyCollection<WorldMapLegendState>> ListByMapAsync(string mapId, int limit = 200)
    {
        var safeLimit = MapRepositoryLimits.Clamp(limit, 1, 2000);
        return await Collection.Find(x =>
                x.MapId == (mapId ?? string.Empty)
                && !x.Deleted
                && !x.Archived)
            .SortBy(x => x.LayerType)
            .Limit(safeLimit)
            .ToListAsync();
    }

    public Task<WorldMapLegendState> UpsertAsync(WorldMapLegendState legend)
    {
        return UpsertEntityAsync(legend, legend?.MapId ?? string.Empty);
    }
}
