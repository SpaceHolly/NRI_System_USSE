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
