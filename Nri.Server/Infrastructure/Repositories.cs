using System;
using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Driver;
using Nri.Server.Logging;
using Nri.Shared.Configuration;
using System.Collections.Generic;
using Nri.Shared.Domain;

namespace Nri.Server.Infrastructure;

public interface IRepository<T> where T : EntityBase
{
    T? GetById(string id);
    IReadOnlyCollection<T> Find(FilterDefinition<T> filter);
    void Insert(T entity);
    void Replace(T entity);
}

public interface INriRepositoryFactory
{
    IRepository<UserAccount> Accounts { get; }
    IRepository<UserProfile> Profiles { get; }
    IRepository<Character> Characters { get; }
    IRepository<SessionUserState> Presence { get; }
    IRepository<EntityLock> Locks { get; }
    IRepository<AuditLogEntry> AuditLogs { get; }
    IRepository<RequestBaseDocument> Requests { get; }
    IRepository<ChatMessage> ChatMessages { get; }
    IRepository<SessionAudioState> AudioStates { get; }
}

public class RequestBaseDocument : EntityBase
{
    public string RequestType { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string CreatorUserId { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
}

public class MongoContext
{
    public IMongoCollection<UserAccount> Accounts { get; }
    public IMongoCollection<UserProfile> Profiles { get; }
    public IMongoCollection<Character> Characters { get; }
    public IMongoCollection<SessionUserState> Presence { get; }
    public IMongoCollection<EntityLock> Locks { get; }
    public IMongoCollection<AuditLogEntry> AuditLogs { get; }
    public IMongoCollection<RequestBaseDocument> Requests { get; }
    public IMongoCollection<ChatMessage> ChatMessages { get; }
    public IMongoCollection<SessionAudioState> AudioStates { get; }

    public MongoContext(ServerConfig config, IServerLogger logger)
    {
        var client = new MongoClient(config.Mongo.ConnectionString);
        var db = client.GetDatabase(config.Mongo.DatabaseName);

        Accounts = db.GetCollection<UserAccount>("accounts");
        Profiles = db.GetCollection<UserProfile>("profiles");
        Characters = db.GetCollection<Character>("characters");
        Presence = db.GetCollection<SessionUserState>("sessions");
        Locks = db.GetCollection<EntityLock>("locks");
        AuditLogs = db.GetCollection<AuditLogEntry>("audit_logs");
        Requests = db.GetCollection<RequestBaseDocument>("requests");
        ChatMessages = db.GetCollection<ChatMessage>("chat_messages");
        AudioStates = db.GetCollection<SessionAudioState>("audio_states");

        EnsureIndexes();
        logger.Debug("Mongo context initialized.");
    }

    private void EnsureIndexes()
    {
        Accounts.Indexes.CreateOne(new CreateIndexModel<UserAccount>(Builders<UserAccount>.IndexKeys.Ascending(x => x.Login), new CreateIndexOptions { Unique = true }));
        Presence.Indexes.CreateOne(new CreateIndexModel<SessionUserState>(Builders<SessionUserState>.IndexKeys.Ascending(x => x.AuthToken), new CreateIndexOptions { Unique = true }));
        Characters.Indexes.CreateOne(new CreateIndexModel<Character>(Builders<Character>.IndexKeys.Ascending(x => x.OwnerUserId)));
        Locks.Indexes.CreateOne(new CreateIndexModel<EntityLock>(Builders<EntityLock>.IndexKeys.Ascending(x => x.EntityType).Ascending(x => x.EntityId), new CreateIndexOptions { Unique = true }));
    }
}

public class MongoRepository<T> : IRepository<T> where T : EntityBase
{
    private readonly IMongoCollection<T> _collection;

    public MongoRepository(IMongoCollection<T> collection)
    {
        _collection = collection;
    }

    public T? GetById(string id)
    {
        return _collection.Find(x => x.Id == id).FirstOrDefault();
    }

    public IReadOnlyCollection<T> Find(FilterDefinition<T> filter)
    {
        return _collection.Find(filter).ToList();
    }

    public void Insert(T entity)
    {
        entity.CreatedUtc = DateTime.UtcNow;
        entity.UpdatedUtc = DateTime.UtcNow;
        _collection.InsertOne(entity);
    }

    public void Replace(T entity)
    {
        entity.UpdatedUtc = DateTime.UtcNow;
        _collection.ReplaceOne(x => x.Id == entity.Id, entity, new ReplaceOptions { IsUpsert = false });
    }
}

public class MongoRepositoryFactory : INriRepositoryFactory
{
    public MongoRepositoryFactory(MongoContext context)
    {
        Accounts = new MongoRepository<UserAccount>(context.Accounts);
        Profiles = new MongoRepository<UserProfile>(context.Profiles);
        Characters = new MongoRepository<Character>(context.Characters);
        Presence = new MongoRepository<SessionUserState>(context.Presence);
        Locks = new MongoRepository<EntityLock>(context.Locks);
        AuditLogs = new MongoRepository<AuditLogEntry>(context.AuditLogs);
        Requests = new MongoRepository<RequestBaseDocument>(context.Requests);
        ChatMessages = new MongoRepository<ChatMessage>(context.ChatMessages);
        AudioStates = new MongoRepository<SessionAudioState>(context.AudioStates);
    }

    public IRepository<UserAccount> Accounts { get; }
    public IRepository<UserProfile> Profiles { get; }
    public IRepository<Character> Characters { get; }
    public IRepository<SessionUserState> Presence { get; }
    public IRepository<EntityLock> Locks { get; }
    public IRepository<AuditLogEntry> AuditLogs { get; }
    public IRepository<RequestBaseDocument> Requests { get; }
    public IRepository<ChatMessage> ChatMessages { get; }
    public IRepository<SessionAudioState> AudioStates { get; }
    IReadOnlyCollection<T> List();
    void Save(T entity);
}

public interface IMongoRepositoryFactory
{
    IRepository<UserAccount> UserAccounts { get; }
    IRepository<Character> Characters { get; }
    IRepository<GameSession> Sessions { get; }
    IRepository<AuditLogEntry> AuditLogs { get; }
}

public class InMemoryRepository<T> : IRepository<T> where T : EntityBase
{
    private readonly Dictionary<string, T> _storage = new Dictionary<string, T>();

    public T? GetById(string id) => _storage.ContainsKey(id) ? _storage[id] : null;

    public IReadOnlyCollection<T> List() => _storage.Values;

    public void Save(T entity)
    {
        _storage[entity.Id] = entity;
    }
}

public class MongoRepositoryFactoryStub : IMongoRepositoryFactory
{
    public IRepository<UserAccount> UserAccounts { get; } = new InMemoryRepository<UserAccount>();
    public IRepository<Character> Characters { get; } = new InMemoryRepository<Character>();
    public IRepository<GameSession> Sessions { get; } = new InMemoryRepository<GameSession>();
    public IRepository<AuditLogEntry> AuditLogs { get; } = new InMemoryRepository<AuditLogEntry>();
}
