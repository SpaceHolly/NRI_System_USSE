using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Bson;
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
        var collection = _mongo.Database.GetCollection<BsonDocument>("sync_counters");
        var now = DateTime.UtcNow;
        var update = Builders<BsonDocument>.Update
            .SetOnInsert("CounterKey", CounterKey)
            .SetOnInsert("CreatedUtc", now)
            .Inc("Value", 1)
            .Set("UpdatedUtc", now);

        var options = new FindOneAndUpdateOptions<BsonDocument>
        {
            IsUpsert = true,
            ReturnDocument = ReturnDocument.After
        };

        var item = collection.FindOneAndUpdate(Builders<BsonDocument>.Filter.Eq("CounterKey", CounterKey), update, options);
        return item == null ? 1 : BsonLong(item, "Value", 1);
    }

    public long CurrentRevision()
    {
        var collection = _mongo.Database.GetCollection<BsonDocument>("sync_counters");
        var item = collection.Find(Builders<BsonDocument>.Filter.Eq("CounterKey", CounterKey)).FirstOrDefault();
        return item == null ? 0 : BsonLong(item, "Value");
    }

    private static long BsonLong(BsonDocument doc, string key, long fallback = 0)
    {
        if (!doc.TryGetValue(key, out var value) || value.IsBsonNull) return fallback;
        if (value.IsInt64) return value.AsInt64;
        if (value.IsInt32) return value.AsInt32;
        return long.TryParse(value.ToString(), out var parsed) ? parsed : fallback;
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
        var collection = _mongo.Database.GetCollection<BsonDocument>("sync_events");
        var scopeFilter = scopes.Count == 0
            ? FilterDefinition<BsonDocument>.Empty
            : Builders<BsonDocument>.Filter.In("Scope", scopes);
        var filter = Builders<BsonDocument>.Filter.Gt("Revision", afterRevision) & scopeFilter;
        return collection.Find(filter)
            .Sort(Builders<BsonDocument>.Sort.Ascending("Revision"))
            .Limit(normalizedLimit)
            .ToList()
            .Select(ToSyncEvent)
            .ToList();
    }

    public long GetLatestRevision()
    {
        var collection = _mongo.Database.GetCollection<BsonDocument>("sync_events");
        var item = collection.Find(FilterDefinition<BsonDocument>.Empty)
            .Sort(Builders<BsonDocument>.Sort.Descending("Revision"))
            .Limit(1)
            .FirstOrDefault();
        return item == null ? 0 : BsonLong(item, "Revision");
    }

    private static SyncEvent ToSyncEvent(BsonDocument doc)
    {
        return new SyncEvent
        {
            Id = BsonString(doc, "Id", BsonString(doc, "_id")),
            Revision = BsonLong(doc, "Revision"),
            Type = BsonString(doc, "Type"),
            Scope = BsonString(doc, "Scope", SyncScopes.Global),
            EntityType = BsonString(doc, "EntityType"),
            EntityId = BsonString(doc, "EntityId"),
            Operation = BsonString(doc, "Operation"),
            ActorUserId = BsonString(doc, "ActorUserId"),
            CampaignId = BsonString(doc, "CampaignId"),
            SessionId = BsonString(doc, "SessionId"),
            Payload = BsonDictionary(doc, "Payload"),
            SchemaVersion = (int)BsonLong(doc, "SchemaVersion", 1),
            Deleted = BsonBool(doc, "Deleted"),
            Archived = BsonBool(doc, "Archived"),
            CreatedUtc = BsonDate(doc, "CreatedUtc"),
            UpdatedUtc = BsonDate(doc, "UpdatedUtc")
        };
    }

    private static Dictionary<string, object> BsonDictionary(BsonDocument doc, string key)
    {
        if (!doc.TryGetValue(key, out var value) || value.IsBsonNull || !value.IsBsonDocument)
            return new Dictionary<string, object>();
        return value.AsBsonDocument.ToDictionary(x => x.Name, x => BsonValueToObject(x.Value), StringComparer.OrdinalIgnoreCase);
    }

    private static object BsonValueToObject(BsonValue value)
    {
        if (value == null || value.IsBsonNull) return string.Empty;
        if (value.IsString) return value.AsString;
        if (value.IsBoolean) return value.AsBoolean;
        if (value.IsInt32) return value.AsInt32;
        if (value.IsInt64) return value.AsInt64;
        if (value.IsDouble) return value.AsDouble;
        if (value.IsDecimal128) return value.AsDecimal128.ToString();
        if (value.IsValidDateTime) return value.ToUniversalTime();
        if (value.IsObjectId) return value.AsObjectId.ToString();
        if (value.IsBsonDocument)
            return value.AsBsonDocument.ToDictionary(x => x.Name, x => BsonValueToObject(x.Value), StringComparer.OrdinalIgnoreCase);
        if (value.IsBsonArray)
            return value.AsBsonArray.Select(BsonValueToObject).Cast<object>().ToArray();
        return value.ToString();
    }

    private static string BsonString(BsonDocument doc, string key, string fallback = "")
    {
        if (!doc.TryGetValue(key, out var value) || value.IsBsonNull) return fallback;
        return value.IsObjectId ? value.AsObjectId.ToString() : value.ToString();
    }

    private static long BsonLong(BsonDocument doc, string key, long fallback = 0)
    {
        if (!doc.TryGetValue(key, out var value) || value.IsBsonNull) return fallback;
        if (value.IsInt64) return value.AsInt64;
        if (value.IsInt32) return value.AsInt32;
        return long.TryParse(value.ToString(), out var parsed) ? parsed : fallback;
    }

    private static bool BsonBool(BsonDocument doc, string key)
    {
        if (!doc.TryGetValue(key, out var value) || value.IsBsonNull) return false;
        return value.IsBoolean ? value.AsBoolean : bool.TryParse(value.ToString(), out var parsed) && parsed;
    }

    private static DateTime BsonDate(BsonDocument doc, string key)
    {
        if (!doc.TryGetValue(key, out var value) || value.IsBsonNull || !value.IsValidDateTime) return DateTime.UtcNow;
        return value.ToUniversalTime();
    }
}

public sealed class SyncEventService
{
    private readonly SyncEventRepository _repository;
    private readonly RevisionService _revisionService;
    private readonly IServerLogger _logger;
    private readonly IMongoDatabase _database;
    private readonly AuthoritativeMongoScopeLookup02110 _scopeLookup;

    public SyncEventService(SyncEventRepository repository, RevisionService revisionService, IServerLogger logger, MongoContext mongo)
    {
        _repository = repository;
        _revisionService = revisionService;
        _logger = logger;
        _database = mongo.Database;
        _scopeLookup = new AuthoritativeMongoScopeLookup02110(mongo.Database);
    }

    public SyncEvent PublishGlobal(string type, string entityType, string entityId, string operation, string actorUserId, Dictionary<string, object>? payload, string requestId)
        => Write(type, SyncScopes.Global, string.Empty, string.Empty, entityType, entityId, operation, actorUserId, payload, requestId, "global");

    public SyncEvent PublishCampaign(string campaignId, string type, string entityType, string entityId, string operation, string actorUserId, Dictionary<string, object>? payload, string requestId)
    {
        if (string.IsNullOrWhiteSpace(campaignId)) throw new InvalidOperationException("Campaign sync publication requires CampaignId.");
        return Write(type, SyncScopes.Campaign(campaignId), campaignId, string.Empty, entityType, entityId, operation, actorUserId, payload, requestId, "campaign");
    }

    public SyncEvent PublishSession(string campaignId, string sessionId, string type, string entityType, string entityId, string operation, string actorUserId, Dictionary<string, object>? payload, string requestId)
    {
        if (string.IsNullOrWhiteSpace(campaignId) || string.IsNullOrWhiteSpace(sessionId)) throw new InvalidOperationException("Session sync publication requires CampaignId and SessionId.");
        return Write(type, SyncScopes.Session(sessionId), campaignId, sessionId, entityType, entityId, operation, actorUserId, payload, requestId, "session");
    }

    public SyncEvent PublishPrivate(string userId, string campaignId, string sessionId, string type, string entityType, string entityId, string operation, Dictionary<string, object>? payload, string requestId)
    {
        if (string.IsNullOrWhiteSpace(userId)) throw new InvalidOperationException("Private sync publication requires UserId.");
        return Write(type, $"private:{userId}", campaignId, sessionId, entityType, entityId, operation, userId, payload, requestId, "private");
    }

    public SyncEvent PublishSessionById(string sessionId, string type, string entityType, string entityId, string operation, string actorUserId, Dictionary<string, object>? payload, string requestId)
    {
        var campaignId = ResolveSessionCampaign(sessionId);
        if (string.IsNullOrWhiteSpace(campaignId)) throw new InvalidOperationException("Session sync publication could not resolve its Campaign.");
        return PublishSession(campaignId, sessionId, type, entityType, entityId, operation, actorUserId, payload, requestId);
    }

    public SyncEvent PublishCharacter(string characterId, string type, string entityType, string entityId, string operation, string actorUserId, Dictionary<string, object>? payload, string requestId)
    {
        var campaignId = ResolveCharacterCampaign(characterId);
        if (string.IsNullOrWhiteSpace(campaignId)) throw new InvalidOperationException("Character sync publication could not resolve its Campaign.");
        return PublishCampaign(campaignId, type, entityType, entityId, operation, actorUserId, payload, requestId);
    }

    public SyncEvent PublishEntity(string type, string entityType, string entityId, string operation, string actorUserId, Dictionary<string, object>? payload, string requestId)
    {
        var document = _scopeLookup.TryFindAny(entityId) ?? throw new InvalidOperationException("Entity sync publication could not resolve its authoritative scope.");
        var campaignId = BsonString(document, "CampaignId");
        var sessionId = BsonString(document, "SessionId");
        if (string.IsNullOrWhiteSpace(campaignId) && !string.IsNullOrWhiteSpace(sessionId)) campaignId = ResolveSessionCampaign(sessionId);
        if (string.IsNullOrWhiteSpace(campaignId))
        {
            var characterId = FirstNonEmpty(BsonString(document, "CharacterId"), BsonString(document, "SubjectId"), BsonString(document, "OwnerCharacterId"));
            campaignId = ResolveCharacterCampaign(characterId);
        }
        if (string.IsNullOrWhiteSpace(campaignId)) throw new InvalidOperationException("Entity sync publication has no authoritative CampaignId.");
        return string.IsNullOrWhiteSpace(sessionId)
            ? PublishCampaign(campaignId, type, entityType, entityId, operation, actorUserId, payload, requestId)
            : PublishSession(campaignId, sessionId, type, entityType, entityId, operation, actorUserId, payload, requestId);
    }

    [Obsolete("Use a typed PublishGlobal/PublishCampaign/PublishSession/PublishPrivate/PublishCharacter/PublishEntity API.")]
    public SyncEvent Publish(string type, string scope, string entityType, string entityId, string operation, string actorUserId, Dictionary<string, object>? payload, string requestId)
    {
        if (!IsExplicitGlobal(scope, type))
            throw new InvalidOperationException($"Legacy sync publication is allowed only for explicit global events. Event '{type}' must use a typed scope API.");
        return PublishGlobal(type, entityType, entityId, operation, actorUserId, payload, requestId);
    }

    private SyncEvent Write(string type, string scope, string campaignId, string sessionId, string entityType, string entityId, string operation, string actorUserId, Dictionary<string, object>? payload, string requestId, string publicationKind)
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
            CampaignId = campaignId ?? string.Empty,
            SessionId = sessionId ?? string.Empty,
            Payload = payload ?? new Dictionary<string, object>()
        };

        _repository.Add(evt);
        _logger.Debug($"sync.event.published requestId={requestId} revision={evt.Revision} type={evt.Type} publicationKind={publicationKind} scope={evt.Scope} campaignId={evt.CampaignId} sessionId={evt.SessionId} entityType={evt.EntityType} entityId={evt.EntityId}");
        return evt;
    }

    private static string ReadScopeValue(Dictionary<string, object>? payload, string key)
        => payload != null && payload.TryGetValue(key, out var value) ? Convert.ToString(value) ?? string.Empty : string.Empty;

    private string ResolveSessionCampaign(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId)) return string.Empty;
        var collection = _database.GetCollection<BsonDocument>("current_sessions");
        var document = collection.Find(Builders<BsonDocument>.Filter.Eq("SessionId", sessionId) | Builders<BsonDocument>.Filter.Eq("_id", sessionId)).Limit(1).FirstOrDefault();
        return document == null ? string.Empty : BsonString(document, "CampaignId");
    }

    private string ResolveCampaignIdentity(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var campaign = _database.GetCollection<BsonDocument>("campaigns").Find(Builders<BsonDocument>.Filter.Eq("_id", value) | Builders<BsonDocument>.Filter.Eq("Id", value)).Limit(1).FirstOrDefault();
        if (campaign != null) return value;
        return ResolveSessionCampaign(value);
    }

    private string ResolveCharacterCampaign(string characterId)
    {
        if (string.IsNullOrWhiteSpace(characterId)) return string.Empty;
        var character = _database.GetCollection<BsonDocument>("characters").Find(Builders<BsonDocument>.Filter.Eq("_id", characterId) | Builders<BsonDocument>.Filter.Eq("Id", characterId)).Limit(1).FirstOrDefault();
        if (character == null) return string.Empty;
        var sessionOrCampaign = BsonString(character, "SessionId");
        return FirstNonEmpty(ResolveSessionCampaign(sessionOrCampaign), sessionOrCampaign);
    }

    private static string ParseScopeId(string scope, params string[] prefixes)
    {
        foreach (var prefix in prefixes)
            if ((scope ?? string.Empty).StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return scope.Substring(prefix.Length);
        return string.Empty;
    }

    private static bool IsExplicitGlobal(string scope, string type)
        => string.Equals(scope, SyncScopes.Definitions, StringComparison.OrdinalIgnoreCase)
           || string.Equals(scope, SyncScopes.Fate, StringComparison.OrdinalIgnoreCase) && (type ?? string.Empty).StartsWith("fate.settings", StringComparison.OrdinalIgnoreCase)
           || string.Equals(scope, SyncScopes.Admin, StringComparison.OrdinalIgnoreCase)
           || string.Equals(scope, SyncScopes.Global, StringComparison.OrdinalIgnoreCase) && (type ?? string.Empty).StartsWith("system.", StringComparison.OrdinalIgnoreCase);

    private static string BsonString(BsonDocument document, string name)
        => document.TryGetValue(name, out var value) && value.IsString ? value.AsString : string.Empty;
    private static string FirstNonEmpty(params string[] values) => values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;

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
        var collection = _mongo.Database.GetCollection<BsonDocument>("sync_counters");
        var item = collection.Find(Builders<BsonDocument>.Filter.Eq("CounterKey", key)).FirstOrDefault();
        return ReadRevisionValue(item);
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
        var collection = _mongo.Database.GetCollection<BsonDocument>("sync_counters");
        var update = Builders<BsonDocument>.Update
            .SetOnInsert("CounterKey", key)
            .SetOnInsert("CreatedUtc", now)
            .Inc("Value", 1)
            .Set("UpdatedUtc", now);
        var options = new FindOneAndUpdateOptions<BsonDocument>
        {
            IsUpsert = true,
            ReturnDocument = ReturnDocument.After
        };
        var item = collection.FindOneAndUpdate(Builders<BsonDocument>.Filter.Eq("CounterKey", key), update, options);
        var next = ReadRevisionValue(item, 1);
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

    private static long ReadRevisionValue(BsonDocument? document, long fallback = 0)
    {
        if (document == null || !document.TryGetValue("Value", out var value) || value.IsBsonNull) return fallback;
        if (value.IsInt64) return value.AsInt64;
        if (value.IsInt32) return value.AsInt32;
        return long.TryParse(value.ToString(), out var parsed) ? parsed : fallback;
    }
}
