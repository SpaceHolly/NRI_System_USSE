using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Driver;
using Nri.Server.Infrastructure;
using Nri.Server.Logging;
using Nri.Shared.Domain;

namespace Nri.Server.Application;

public sealed class RevisionService
{
    private const string CounterKey = "global";
    private readonly MongoContext _mongo;

    public RevisionService(MongoContext mongo)
    {
        _mongo = mongo;
    }

    public long NextRevision()
    {
        var update = Builders<SyncCounter>.Update
            .SetOnInsert(x => x.CounterKey, CounterKey)
            .SetOnInsert(x => x.CreatedUtc, DateTime.UtcNow)
            .Inc(x => x.Value, 1)
            .Set(x => x.UpdatedUtc, DateTime.UtcNow);

        var options = new FindOneAndUpdateOptions<SyncCounter>
        {
            IsUpsert = true,
            ReturnDocument = ReturnDocument.After
        };

        var item = _mongo.SyncCounters.FindOneAndUpdate(Builders<SyncCounter>.Filter.Eq(x => x.CounterKey, CounterKey), update, options);
        return item?.Value ?? 1;
    }

    public long CurrentRevision()
    {
        return _mongo.SyncCounters.Find(Builders<SyncCounter>.Filter.Eq(x => x.CounterKey, CounterKey)).FirstOrDefault()?.Value ?? 0;
    }
}

public sealed class SyncEventRepository
{
    private readonly MongoContext _mongo;

    public SyncEventRepository(MongoContext mongo)
    {
        _mongo = mongo;
    }

    public void Add(SyncEvent evt)
    {
        _mongo.SyncEvents.InsertOne(evt);
    }

    public IReadOnlyCollection<SyncEvent> GetAfterRevision(long afterRevision, IReadOnlyCollection<string> scopes, int limit)
    {
        var normalizedLimit = Math.Max(1, Math.Min(500, limit));
        var scopeFilter = scopes.Count == 0
            ? FilterDefinition<SyncEvent>.Empty
            : Builders<SyncEvent>.Filter.In(x => x.Scope, scopes);
        var filter = Builders<SyncEvent>.Filter.Gt(x => x.Revision, afterRevision) & scopeFilter;
        return _mongo.SyncEvents.Find(filter).SortBy(x => x.Revision).Limit(normalizedLimit).ToList();
    }

    public long GetLatestRevision()
    {
        var item = _mongo.SyncEvents.Find(FilterDefinition<SyncEvent>.Empty).SortByDescending(x => x.Revision).Limit(1).FirstOrDefault();
        return item?.Revision ?? 0;
    }
}

public sealed class SyncEventService
{
    private readonly SyncEventRepository _repository;
    private readonly RevisionService _revisionService;
    private readonly IServerLogger _logger;

    public SyncEventService(SyncEventRepository repository, RevisionService revisionService, IServerLogger logger)
    {
        _repository = repository;
        _revisionService = revisionService;
        _logger = logger;
    }

    public SyncEvent Publish(string type, string scope, string entityType, string entityId, string operation, string actorUserId, Dictionary<string, object>? payload, string requestId)
    {
        var evt = new SyncEvent
        {
            Revision = _revisionService.NextRevision(),
            Type = type ?? string.Empty,
            Scope = scope ?? SyncScopes.Global,
            EntityType = entityType ?? string.Empty,
            EntityId = entityId ?? string.Empty,
            Operation = operation ?? string.Empty,
            ActorUserId = actorUserId ?? string.Empty,
            Payload = payload ?? new Dictionary<string, object>()
        };

        _repository.Add(evt);
        _logger.Debug($"sync.event.published requestId={requestId} revision={evt.Revision} type={evt.Type} scope={evt.Scope} entityType={evt.EntityType} entityId={evt.EntityId}");
        return evt;
    }

    public (long LatestRevision, IReadOnlyCollection<SyncEvent> Events) GetChanges(long afterRevision, IReadOnlyCollection<string> scopes, int limit, string requestId)
    {
        var events = _repository.GetAfterRevision(afterRevision, scopes, limit);
        var latestRevision = _revisionService.CurrentRevision();
        _logger.Debug($"sync.changes.get requestId={requestId} afterRevision={afterRevision} returnedCount={events.Count} latestRevision={latestRevision}");
        return (latestRevision, events);
    }

    public long GetSnapshotInfo(string requestId)
    {
        var latestRevision = _revisionService.CurrentRevision();
        _logger.Debug($"sync.snapshot.get requestId={requestId} latestRevision={latestRevision}");
        return latestRevision;
    }
}

public interface IEntityRevisionService
{
    long GetCurrentRevisionAsync(string entityType, string entityId);
    void EnsureExpectedRevisionAsync(string entityType, string entityId, long? expectedRevision);
    long BumpRevisionAsync(string entityType, string entityId, string actorUserId, string requestId);
    long? TryGetExpectedRevision(IDictionary<string, object> payload);
}

public sealed class EntityRevisionConflictException : InvalidOperationException
{
    public EntityRevisionConflictException(string entityType, string entityId, long expectedRevision, long currentRevision)
        : base($"Revision conflict for {entityType}/{entityId}: expected={expectedRevision}, current={currentRevision}.")
    {
        EntityType = entityType;
        EntityId = entityId;
        ExpectedRevision = expectedRevision;
        CurrentRevision = currentRevision;
    }

    public string EntityType { get; }
    public string EntityId { get; }
    public long ExpectedRevision { get; }
    public long CurrentRevision { get; }
}

public sealed class EntityRevisionService : IEntityRevisionService
{
    private const string Prefix = "entity-revision:";
    private readonly MongoContext _mongo;
    private readonly IServerLogger _logger;

    public EntityRevisionService(MongoContext mongo, IServerLogger logger)
    {
        _mongo = mongo;
        _logger = logger;
    }

    public long GetCurrentRevisionAsync(string entityType, string entityId)
    {
        var key = BuildKey(entityType, entityId);
        return _mongo.SyncCounters.Find(Builders<SyncCounter>.Filter.Eq(x => x.CounterKey, key)).FirstOrDefault()?.Value ?? 0;
    }

    public void EnsureExpectedRevisionAsync(string entityType, string entityId, long? expectedRevision)
    {
        var current = GetCurrentRevisionAsync(entityType, entityId);
        if (!expectedRevision.HasValue)
        {
            _logger.Debug($"revision.legacy_write entityType={entityType} entityId={entityId} reason=no_expected_revision");
            return;
        }

        _logger.Debug($"revision.check entityType={entityType} entityId={entityId} expected={expectedRevision.Value} current={current}");
        if (expectedRevision.Value != current)
        {
            _logger.Debug($"revision.conflict entityType={entityType} entityId={entityId} expected={expectedRevision.Value} current={current}");
            throw new EntityRevisionConflictException(entityType, entityId, expectedRevision.Value, current);
        }
    }

    public long BumpRevisionAsync(string entityType, string entityId, string actorUserId, string requestId)
    {
        var key = BuildKey(entityType, entityId);
        var now = DateTime.UtcNow;
        var update = Builders<SyncCounter>.Update
            .SetOnInsert(x => x.CounterKey, key)
            .SetOnInsert(x => x.CreatedUtc, now)
            .Inc(x => x.Value, 1)
            .Set(x => x.UpdatedUtc, now);
        var options = new FindOneAndUpdateOptions<SyncCounter> { IsUpsert = true, ReturnDocument = ReturnDocument.After };
        var item = _mongo.SyncCounters.FindOneAndUpdate(Builders<SyncCounter>.Filter.Eq(x => x.CounterKey, key), update, options);
        var next = item?.Value ?? 1;
        _logger.Debug($"revision.bump entityType={entityType} entityId={entityId} newRevision={next} requestId={requestId} actorUserId={actorUserId}");
        return next;
    }

    public long? TryGetExpectedRevision(IDictionary<string, object> payload)
    {
        if (!payload.TryGetValue("expectedRevision", out var value) || value == null) return null;
        if (value is long l) return l;
        if (value is int i) return i;
        return long.TryParse(Convert.ToString(value), out var parsed) ? parsed : null;
    }

    private static string BuildKey(string entityType, string entityId)
    {
        return $"{Prefix}{entityType}:{entityId}";
    }
}
