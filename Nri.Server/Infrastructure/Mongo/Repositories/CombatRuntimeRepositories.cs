using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MongoDB.Driver;
using Nri.Server.Logging;
using Nri.Shared.Domain;

namespace Nri.Server.Infrastructure.Mongo.Repositories;

public interface ICombatEncounterRepository
{
    Task<CombatEncounterState?> GetByIdAsync(string id);
    Task<IReadOnlyCollection<CombatEncounterState>> ListByCampaignAsync(string campaignId, int limit = 100);
    Task<IReadOnlyCollection<CombatEncounterState>> ListBySessionAsync(string sessionId, int limit = 100);
    Task<CombatEncounterState> UpsertAsync(CombatEncounterState encounter);
    Task<bool> ArchiveAsync(string id, string actorUserId, string requestId);
}

public interface ICombatParticipantRepository
{
    Task<CombatParticipantState?> GetByIdAsync(string id);
    Task<IReadOnlyCollection<CombatParticipantState>> ListByEncounterAsync(string encounterId, int limit = 200);
    Task<CombatParticipantState> UpsertAsync(CombatParticipantState participant);
    Task<bool> ArchiveAsync(string id, string actorUserId, string requestId);
}

public interface ICombatTurnRepository
{
    Task<CombatTurnState?> GetByIdAsync(string id);
    Task<IReadOnlyCollection<CombatTurnState>> ListByEncounterAsync(string encounterId, int limit = 200);
    Task<CombatTurnState> UpsertAsync(CombatTurnState turn);
}

public interface ICombatRoundRepository
{
    Task<CombatRoundRuntimeState?> GetByEncounterRoundAsync(string encounterId, int roundNumber);
    Task<IReadOnlyCollection<CombatRoundRuntimeState>> ListByEncounterAsync(string encounterId, int limit = 200);
    Task<CombatRoundRuntimeState> UpsertAsync(CombatRoundRuntimeState round);
}

public interface ICombatActionRepository
{
    Task<CombatActionState?> GetByIdAsync(string id);
    Task<CombatActionState?> GetByRequestIdAsync(string encounterId, string requestId, string actorParticipantId);
    Task<IReadOnlyCollection<CombatActionState>> ListByEncounterAsync(string encounterId, int limit = 200);
    Task<CombatActionState> AppendAsync(CombatActionState action);
    Task<CombatActionState> UpsertAsync(CombatActionState action);
}

public interface ICombatLogRepository
{
    Task<CombatRuntimeLogEntry?> GetByIdAsync(string id);
    Task<IReadOnlyCollection<CombatRuntimeLogEntry>> ListByEncounterAsync(string encounterId, int limit = 200);
    Task<CombatRuntimeLogEntry> AppendAsync(CombatRuntimeLogEntry entry);
}

public interface ICombatReplayEventRepository
{
    Task<CombatReplayEvent?> GetByIdAsync(string id);
    Task<CombatReplayEvent?> GetByRequestIdAsync(string encounterId, string requestId);
    Task<IReadOnlyCollection<CombatReplayEvent>> ListByEncounterAsync(string encounterId, int limit = 200);
    Task<CombatReplayEvent> AppendAsync(CombatReplayEvent replayEvent);
}

internal static class CombatRuntimeRepositoryLimits
{
    public static int Clamp(int limit)
    {
        return Math.Max(1, Math.Min(limit, 500));
    }
}

public abstract class CombatEntityRepository<T> where T : EntityBase
{
    private readonly IMongoCollection<T> _collection;
    private readonly IServerLogger? _logger;
    private readonly string _type;

    protected CombatEntityRepository(IMongoCollection<T> collection, IServerLogger? logger, string type)
    {
        _collection = collection;
        _logger = logger;
        _type = type;
    }

    protected IMongoCollection<T> Collection => _collection;
    protected string Type => _type;

    public async Task<T?> GetByIdAsync(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        return await _collection.Find(x => x.Id == id && !x.Deleted).FirstOrDefaultAsync();
    }

    protected async Task<T> UpsertEntityAsync(T entity, string encounterId)
    {
        if (entity == null) throw new ArgumentNullException(nameof(entity));

        var now = DateTime.UtcNow;
        if (entity.CreatedUtc == default) entity.CreatedUtc = now;
        entity.UpdatedUtc = now;
        if (entity.SchemaVersion < 1) entity.SchemaVersion = 1;

        await _collection.ReplaceOneAsync(x => x.Id == entity.Id, entity, new ReplaceOptions { IsUpsert = true });
        _logger?.Debug($"combat.repository.upsert type={_type} id={entity.Id} encounterId={encounterId}");
        return entity;
    }

    protected async Task<T> AppendEntityAsync(T entity, string encounterId)
    {
        if (entity == null) throw new ArgumentNullException(nameof(entity));

        var now = DateTime.UtcNow;
        if (entity.CreatedUtc == default) entity.CreatedUtc = now;
        entity.UpdatedUtc = now;
        if (entity.SchemaVersion < 1) entity.SchemaVersion = 1;

        await _collection.InsertOneAsync(entity);
        _logger?.Debug($"combat.repository.append type={_type} encounterId={encounterId}");
        return entity;
    }

    public async Task<bool> ArchiveAsync(string id, string actorUserId, string requestId)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;

        var update = Builders<T>.Update
            .Set(x => x.Archived, true)
            .Set(x => x.UpdatedUtc, DateTime.UtcNow);

        var result = await _collection.UpdateOneAsync(x => x.Id == id, update);
        _logger?.Debug($"combat.repository.archive type={_type} id={id}");
        return result.ModifiedCount > 0;
    }

    protected void LogList(string encounterId, int limit)
    {
        _logger?.Debug($"combat.repository.list type={_type} encounterId={encounterId} limit={limit}");
    }
}

public sealed class CombatEncounterRepository : CombatEntityRepository<CombatEncounterState>, ICombatEncounterRepository
{
    public CombatEncounterRepository(IMongoCollection<CombatEncounterState> collection, IServerLogger? logger = null)
        : base(collection, logger, "encounter")
    {
    }

    public async Task<IReadOnlyCollection<CombatEncounterState>> ListByCampaignAsync(string campaignId, int limit = 100)
    {
        var safeLimit = CombatRuntimeRepositoryLimits.Clamp(limit);
        LogList(campaignId ?? string.Empty, safeLimit);
        return await Collection.Find(x => x.CampaignId == (campaignId ?? string.Empty) && !x.Deleted && !x.Archived)
            .SortByDescending(x => x.UpdatedUtc)
            .Limit(safeLimit)
            .ToListAsync();
    }

    public async Task<IReadOnlyCollection<CombatEncounterState>> ListBySessionAsync(string sessionId, int limit = 100)
    {
        var safeLimit = CombatRuntimeRepositoryLimits.Clamp(limit);
        LogList(sessionId ?? string.Empty, safeLimit);
        return await Collection.Find(x => x.SessionId == (sessionId ?? string.Empty) && !x.Deleted && !x.Archived)
            .SortByDescending(x => x.UpdatedUtc)
            .Limit(safeLimit)
            .ToListAsync();
    }

    public Task<CombatEncounterState> UpsertAsync(CombatEncounterState encounter)
    {
        return UpsertEntityAsync(encounter, encounter?.Id ?? string.Empty);
    }
}

public sealed class CombatParticipantRepository : CombatEntityRepository<CombatParticipantState>, ICombatParticipantRepository
{
    public CombatParticipantRepository(IMongoCollection<CombatParticipantState> collection, IServerLogger? logger = null)
        : base(collection, logger, "participant")
    {
    }

    public async Task<IReadOnlyCollection<CombatParticipantState>> ListByEncounterAsync(string encounterId, int limit = 200)
    {
        var safeLimit = CombatRuntimeRepositoryLimits.Clamp(limit);
        LogList(encounterId ?? string.Empty, safeLimit);
        return await Collection.Find(x => x.EncounterId == (encounterId ?? string.Empty) && !x.Deleted && !x.Archived)
            .SortByDescending(x => x.UpdatedUtc)
            .Limit(safeLimit)
            .ToListAsync();
    }

    public Task<CombatParticipantState> UpsertAsync(CombatParticipantState participant)
    {
        return UpsertEntityAsync(participant, participant?.EncounterId ?? string.Empty);
    }
}

public sealed class CombatTurnRepository : CombatEntityRepository<CombatTurnState>, ICombatTurnRepository
{
    public CombatTurnRepository(IMongoCollection<CombatTurnState> collection, IServerLogger? logger = null)
        : base(collection, logger, "turn")
    {
    }

    public async Task<IReadOnlyCollection<CombatTurnState>> ListByEncounterAsync(string encounterId, int limit = 200)
    {
        var safeLimit = CombatRuntimeRepositoryLimits.Clamp(limit);
        LogList(encounterId ?? string.Empty, safeLimit);
        return await Collection.Find(x => x.EncounterId == (encounterId ?? string.Empty) && !x.Deleted && !x.Archived)
            .SortBy(x => x.RoundNumber)
            .ThenBy(x => x.TurnIndex)
            .Limit(safeLimit)
            .ToListAsync();
    }

    public Task<CombatTurnState> UpsertAsync(CombatTurnState turn)
    {
        return UpsertEntityAsync(turn, turn?.EncounterId ?? string.Empty);
    }
}

public sealed class CombatLogRepository : CombatEntityRepository<CombatRuntimeLogEntry>, ICombatLogRepository
{
    public CombatLogRepository(IMongoCollection<CombatRuntimeLogEntry> collection, IServerLogger? logger = null)
        : base(collection, logger, "log")
    {
    }

    public async Task<IReadOnlyCollection<CombatRuntimeLogEntry>> ListByEncounterAsync(string encounterId, int limit = 200)
    {
        var safeLimit = CombatRuntimeRepositoryLimits.Clamp(limit);
        LogList(encounterId ?? string.Empty, safeLimit);
        return await Collection.Find(x => x.EncounterId == (encounterId ?? string.Empty) && !x.Deleted && !x.Archived)
            .SortByDescending(x => x.CreatedAtUtc)
            .Limit(safeLimit)
            .ToListAsync();
    }

    public Task<CombatRuntimeLogEntry> AppendAsync(CombatRuntimeLogEntry entry)
    {
        return AppendEntityAsync(entry, entry?.EncounterId ?? string.Empty);
    }
}

public sealed class CombatRoundRepository : ICombatRoundRepository
{
    private readonly IMongoCollection<CombatRoundRuntimeState> _collection;
    private readonly IServerLogger? _logger;

    public CombatRoundRepository(IMongoCollection<CombatRoundRuntimeState> collection, IServerLogger? logger = null)
    {
        _collection = collection;
        _logger = logger;
    }

    public async Task<CombatRoundRuntimeState?> GetByEncounterRoundAsync(string encounterId, int roundNumber)
    {
        if (string.IsNullOrWhiteSpace(encounterId)) return null;
        return await _collection.Find(x => x.EncounterId == encounterId && x.RoundNumber == roundNumber).FirstOrDefaultAsync();
    }

    public async Task<IReadOnlyCollection<CombatRoundRuntimeState>> ListByEncounterAsync(string encounterId, int limit = 200)
    {
        var safeLimit = CombatRuntimeRepositoryLimits.Clamp(limit);
        _logger?.Debug($"combat.repository.list type=round encounterId={encounterId} limit={safeLimit}");
        return await _collection.Find(x => x.EncounterId == (encounterId ?? string.Empty))
            .SortBy(x => x.RoundNumber)
            .Limit(safeLimit)
            .ToListAsync();
    }

    public async Task<CombatRoundRuntimeState> UpsertAsync(CombatRoundRuntimeState round)
    {
        if (round == null) throw new ArgumentNullException(nameof(round));
        if (string.IsNullOrWhiteSpace(round.Id)) round.Id = $"{round.EncounterId}:r{round.RoundNumber}";
        if (round.CreatedUtc == default) round.CreatedUtc = DateTime.UtcNow;
        round.UpdatedUtc = DateTime.UtcNow;
        if (round.SchemaVersion < 1) round.SchemaVersion = 1;
        await _collection.ReplaceOneAsync(
            x => x.EncounterId == round.EncounterId && x.RoundNumber == round.RoundNumber,
            round,
            new ReplaceOptions { IsUpsert = true });
        _logger?.Debug($"combat.repository.upsert type=round id={round.EncounterId}:{round.RoundNumber} encounterId={round.EncounterId}");
        return round;
    }
}

public sealed class CombatActionRepository : ICombatActionRepository
{
    private readonly IMongoCollection<CombatActionState> _collection;
    private readonly IServerLogger? _logger;

    public CombatActionRepository(IMongoCollection<CombatActionState> collection, IServerLogger? logger = null)
    {
        _collection = collection;
        _logger = logger;
    }

    public async Task<CombatActionState?> GetByIdAsync(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        return await _collection.Find(x => x.Id == id).FirstOrDefaultAsync();
    }

    public async Task<CombatActionState?> GetByRequestIdAsync(string encounterId, string requestId, string actorParticipantId)
    {
        if (string.IsNullOrWhiteSpace(encounterId) || string.IsNullOrWhiteSpace(requestId)) return null;
        return await _collection.Find(x => x.EncounterId == encounterId && x.RequestId == requestId && x.ActorParticipantId == (actorParticipantId ?? string.Empty)).FirstOrDefaultAsync();
    }

    public async Task<IReadOnlyCollection<CombatActionState>> ListByEncounterAsync(string encounterId, int limit = 200)
    {
        var safeLimit = CombatRuntimeRepositoryLimits.Clamp(limit);
        _logger?.Debug($"combat.repository.list type=action encounterId={encounterId} limit={safeLimit}");
        return await _collection.Find(x => x.EncounterId == (encounterId ?? string.Empty))
            .SortBy(x => x.CreatedAtUtc)
            .Limit(safeLimit)
            .ToListAsync();
    }

    public async Task<CombatActionState> AppendAsync(CombatActionState action)
    {
        if (action == null) throw new ArgumentNullException(nameof(action));
        if (action.CreatedAtUtc == default) action.CreatedAtUtc = DateTime.UtcNow;
        await _collection.InsertOneAsync(action);
        _logger?.Debug($"combat.repository.append type=action encounterId={action.EncounterId}");
        return action;
    }

    public async Task<CombatActionState> UpsertAsync(CombatActionState action)
    {
        if (action == null) throw new ArgumentNullException(nameof(action));
        if (string.IsNullOrWhiteSpace(action.Id)) action.Id = Guid.NewGuid().ToString("N");
        if (action.CreatedAtUtc == default) action.CreatedAtUtc = DateTime.UtcNow;
        await _collection.ReplaceOneAsync(x => x.Id == action.Id, action, new ReplaceOptions { IsUpsert = true });
        _logger?.Debug($"combat.repository.upsert type=action id={action.Id} encounterId={action.EncounterId}");
        return action;
    }
}

public sealed class CombatReplayEventRepository : ICombatReplayEventRepository
{
    private readonly IMongoCollection<CombatReplayEvent> _collection;
    private readonly IServerLogger? _logger;

    public CombatReplayEventRepository(IMongoCollection<CombatReplayEvent> collection, IServerLogger? logger = null)
    {
        _collection = collection;
        _logger = logger;
    }

    public async Task<CombatReplayEvent?> GetByIdAsync(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        return await _collection.Find(x => x.Id == id).FirstOrDefaultAsync();
    }

    public async Task<CombatReplayEvent?> GetByRequestIdAsync(string encounterId, string requestId)
    {
        if (string.IsNullOrWhiteSpace(encounterId) || string.IsNullOrWhiteSpace(requestId)) return null;
        return await _collection.Find(x => x.EncounterId == encounterId && x.RequestId == requestId).FirstOrDefaultAsync();
    }

    public async Task<IReadOnlyCollection<CombatReplayEvent>> ListByEncounterAsync(string encounterId, int limit = 200)
    {
        var safeLimit = CombatRuntimeRepositoryLimits.Clamp(limit);
        _logger?.Debug($"combat.repository.list type=replay_event encounterId={encounterId} limit={safeLimit}");
        return await _collection.Find(x => x.EncounterId == (encounterId ?? string.Empty))
            .SortBy(x => x.SequenceNumber)
            .Limit(safeLimit)
            .ToListAsync();
    }

    public async Task<CombatReplayEvent> AppendAsync(CombatReplayEvent replayEvent)
    {
        if (replayEvent == null) throw new ArgumentNullException(nameof(replayEvent));
        if (replayEvent.CreatedAtUtc == default) replayEvent.CreatedAtUtc = DateTime.UtcNow;
        await _collection.InsertOneAsync(replayEvent);
        _logger?.Debug($"combat.repository.append type=replay_event encounterId={replayEvent.EncounterId}");
        return replayEvent;
    }
}
