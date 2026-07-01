using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MongoDB.Driver;
using Nri.Server.Logging;
using Nri.Shared.Domain;

namespace Nri.Server.Infrastructure.Mongo.Repositories;

public interface IEconomyRuntimeStateRepository<T> where T : EntityBase
{
    Task<T?> GetByIdAsync(string id);
    Task<IReadOnlyCollection<T>> ListByCampaignAsync(string campaignId, int limit = 100, bool includeArchived = false);
    Task<T> UpsertAsync(T state);
    Task<bool> ArchiveAsync(string id, string actorUserId, string requestId);
}

public interface IFactionStateRepository : IEconomyRuntimeStateRepository<FactionState>
{
}

public interface IOrganizationStateRepository : IEconomyRuntimeStateRepository<OrganizationState>
{
}

public interface IMarketStateRepository : IEconomyRuntimeStateRepository<MarketState>
{
}

public interface ILawStateRepository : IEconomyRuntimeStateRepository<LawState>
{
}

public interface IRestrictionStateRepository : IEconomyRuntimeStateRepository<RestrictionState>
{
}

public interface IAssetStateRepository : IEconomyRuntimeStateRepository<AssetState>
{
}

public interface IEconomyScopeStateRepository : IEconomyRuntimeStateRepository<EconomyScopeState>
{
}

public abstract class EconomyRuntimeStateRepository<T> : IEconomyRuntimeStateRepository<T> where T : EntityBase
{
    private readonly IMongoCollection<T> _collection;
    private readonly IServerLogger? _logger;
    private readonly string _type;

    protected EconomyRuntimeStateRepository(IMongoCollection<T> collection, IServerLogger? logger, string type)
    {
        _collection = collection;
        _logger = logger;
        _type = type;
    }

    public async Task<T?> GetByIdAsync(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        return await _collection.Find(x => x.Id == id).FirstOrDefaultAsync();
    }

    public async Task<IReadOnlyCollection<T>> ListByCampaignAsync(string campaignId, int limit = 100, bool includeArchived = false)
    {
        var safeLimit = Math.Max(1, Math.Min(limit, 500));
        _logger?.Debug($"economy.repository.list type={_type} campaignId={campaignId} limit={safeLimit}");

        var filters = new List<FilterDefinition<T>>
        {
            Builders<T>.Filter.Eq("CampaignId", campaignId ?? string.Empty),
            Builders<T>.Filter.Eq(x => x.Deleted, false)
        };
        if (!includeArchived)
        {
            filters.Add(Builders<T>.Filter.Eq(x => x.Archived, false));
        }

        return await _collection
            .Find(Builders<T>.Filter.And(filters))
            .SortByDescending(x => x.UpdatedUtc)
            .Limit(safeLimit)
            .ToListAsync();
    }

    public async Task<T> UpsertAsync(T state)
    {
        if (state == null) throw new ArgumentNullException(nameof(state));

        var now = DateTime.UtcNow;
        if (state.CreatedUtc == default)
        {
            state.CreatedUtc = now;
        }

        state.UpdatedUtc = now;
        if (state.SchemaVersion < 1)
        {
            state.SchemaVersion = 1;
        }

        await _collection.ReplaceOneAsync(x => x.Id == state.Id, state, new ReplaceOptions { IsUpsert = true });
        _logger?.Debug($"economy.repository.upsert type={_type} id={state.Id} campaignId={GetStringProperty(state, "CampaignId")}");
        return state;
    }

    public async Task<bool> ArchiveAsync(string id, string actorUserId, string requestId)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        var update = Builders<T>.Update
            .Set(x => x.Archived, true)
            .Set(x => x.UpdatedUtc, DateTime.UtcNow);

        var result = await _collection.UpdateOneAsync(x => x.Id == id, update);
        _logger?.Debug($"economy.repository.archive type={_type} id={id}");
        return result.ModifiedCount > 0;
    }

    private static string GetStringProperty(T state, string propertyName)
    {
        var property = typeof(T).GetProperty(propertyName);
        return property?.GetValue(state) as string ?? string.Empty;
    }
}

public sealed class FactionStateRepository : EconomyRuntimeStateRepository<FactionState>, IFactionStateRepository
{
    public FactionStateRepository(IMongoCollection<FactionState> collection, IServerLogger? logger = null)
        : base(collection, logger, EconomyRuntimeKinds.Faction)
    {
    }
}

public sealed class OrganizationStateRepository : EconomyRuntimeStateRepository<OrganizationState>, IOrganizationStateRepository
{
    public OrganizationStateRepository(IMongoCollection<OrganizationState> collection, IServerLogger? logger = null)
        : base(collection, logger, EconomyRuntimeKinds.Organization)
    {
    }
}

public sealed class MarketStateRepository : EconomyRuntimeStateRepository<MarketState>, IMarketStateRepository
{
    public MarketStateRepository(IMongoCollection<MarketState> collection, IServerLogger? logger = null)
        : base(collection, logger, EconomyRuntimeKinds.Market)
    {
    }
}

public sealed class LawStateRepository : EconomyRuntimeStateRepository<LawState>, ILawStateRepository
{
    public LawStateRepository(IMongoCollection<LawState> collection, IServerLogger? logger = null)
        : base(collection, logger, EconomyRuntimeKinds.Law)
    {
    }
}

public sealed class RestrictionStateRepository : EconomyRuntimeStateRepository<RestrictionState>, IRestrictionStateRepository
{
    public RestrictionStateRepository(IMongoCollection<RestrictionState> collection, IServerLogger? logger = null)
        : base(collection, logger, EconomyRuntimeKinds.Restriction)
    {
    }
}

public sealed class AssetStateRepository : EconomyRuntimeStateRepository<AssetState>, IAssetStateRepository
{
    public AssetStateRepository(IMongoCollection<AssetState> collection, IServerLogger? logger = null)
        : base(collection, logger, EconomyRuntimeKinds.Asset)
    {
    }
}

public sealed class EconomyScopeStateRepository : EconomyRuntimeStateRepository<EconomyScopeState>, IEconomyScopeStateRepository
{
    public EconomyScopeStateRepository(IMongoCollection<EconomyScopeState> collection, IServerLogger? logger = null)
        : base(collection, logger, EconomyRuntimeKinds.EconomyScope)
    {
    }
}
