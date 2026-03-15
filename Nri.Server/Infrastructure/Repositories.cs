using System;
using System.Collections.Generic;
using MongoDB.Driver;
using Nri.Server.Logging;
using Nri.Shared.Configuration;
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
    IRepository<ActionRequest> ActionRequests { get; }
    IRepository<DiceRollRequest> DiceRequests { get; }
    IRepository<ChatMessage> ChatMessages { get; }
    IRepository<ChatReadState> ChatReadStates { get; }
    IRepository<SessionChatSettings> SessionChatSettings { get; }
    IRepository<ChatUserThrottleState> ChatThrottleStates { get; }
    IRepository<SessionAudioState> AudioStates { get; }
    IRepository<CombatState> Combats { get; }
    IRepository<CombatLogEntry> CombatLogs { get; }
    IRepository<ClassTreeDefinition> ClassTrees { get; }
    IRepository<SkillDefinitionRecord> SkillDefinitions { get; }
    IRepository<DefinitionVersion> DefinitionVersions { get; }
}

public class MongoContext
{
    public IMongoCollection<UserAccount> Accounts { get; }
    public IMongoCollection<UserProfile> Profiles { get; }
    public IMongoCollection<Character> Characters { get; }
    public IMongoCollection<SessionUserState> Presence { get; }
    public IMongoCollection<EntityLock> Locks { get; }
    public IMongoCollection<AuditLogEntry> AuditLogs { get; }
    public IMongoCollection<ActionRequest> ActionRequests { get; }
    public IMongoCollection<DiceRollRequest> DiceRequests { get; }
    public IMongoCollection<ChatMessage> ChatMessages { get; }
    public IMongoCollection<ChatReadState> ChatReadStates { get; }
    public IMongoCollection<SessionChatSettings> SessionChatSettings { get; }
    public IMongoCollection<ChatUserThrottleState> ChatThrottleStates { get; }
    public IMongoCollection<SessionAudioState> AudioStates { get; }
    public IMongoCollection<CombatState> Combats { get; }
    public IMongoCollection<CombatLogEntry> CombatLogs { get; }
    public IMongoCollection<ClassTreeDefinition> ClassTrees { get; }
    public IMongoCollection<SkillDefinitionRecord> SkillDefinitions { get; }
    public IMongoCollection<DefinitionVersion> DefinitionVersions { get; }

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
        ActionRequests = db.GetCollection<ActionRequest>("action_requests");
        DiceRequests = db.GetCollection<DiceRollRequest>("dice_requests");
        ChatMessages = db.GetCollection<ChatMessage>("chat_messages");
        ChatReadStates = db.GetCollection<ChatReadState>("chat_read_states");
        SessionChatSettings = db.GetCollection<SessionChatSettings>("session_chat_settings");
        ChatThrottleStates = db.GetCollection<ChatUserThrottleState>("chat_throttle_states");
        AudioStates = db.GetCollection<SessionAudioState>("audio_states");
        Combats = db.GetCollection<CombatState>("combat_states");
        CombatLogs = db.GetCollection<CombatLogEntry>("combat_logs");
        ClassTrees = db.GetCollection<ClassTreeDefinition>("class_tree_definitions");
        SkillDefinitions = db.GetCollection<SkillDefinitionRecord>("skill_definitions");
        DefinitionVersions = db.GetCollection<DefinitionVersion>("definition_versions");

        EnsureIndexes();
        logger.Debug("Mongo context initialized.");
    }

    private void EnsureIndexes()
    {
        Accounts.Indexes.CreateOne(new CreateIndexModel<UserAccount>(Builders<UserAccount>.IndexKeys.Ascending(x => x.Login), new CreateIndexOptions { Unique = true }));
        Presence.Indexes.CreateOne(new CreateIndexModel<SessionUserState>(Builders<SessionUserState>.IndexKeys.Ascending(x => x.AuthToken), new CreateIndexOptions { Unique = true }));
        Characters.Indexes.CreateOne(new CreateIndexModel<Character>(Builders<Character>.IndexKeys.Ascending(x => x.OwnerUserId)));
        Locks.Indexes.CreateOne(new CreateIndexModel<EntityLock>(Builders<EntityLock>.IndexKeys.Ascending(x => x.EntityType).Ascending(x => x.EntityId), new CreateIndexOptions { Unique = true }));
        ActionRequests.Indexes.CreateOne(new CreateIndexModel<ActionRequest>(Builders<ActionRequest>.IndexKeys.Ascending(x => x.CreatorUserId).Ascending(x => x.Fingerprint).Ascending(x => x.Status)));
        DiceRequests.Indexes.CreateOne(new CreateIndexModel<DiceRollRequest>(Builders<DiceRollRequest>.IndexKeys.Ascending(x => x.CreatorUserId).Ascending(x => x.Fingerprint).Ascending(x => x.Status)));
        Combats.Indexes.CreateOne(new CreateIndexModel<CombatState>(Builders<CombatState>.IndexKeys.Ascending(x => x.SessionId), new CreateIndexOptions { Unique = true }));
        CombatLogs.Indexes.CreateOne(new CreateIndexModel<CombatLogEntry>(Builders<CombatLogEntry>.IndexKeys.Ascending(x => x.CombatId).Descending(x => x.CreatedUtc)));
        ChatMessages.Indexes.CreateOne(new CreateIndexModel<ChatMessage>(Builders<ChatMessage>.IndexKeys.Ascending(x => x.SessionId).Descending(x => x.CreatedUtc)));
        ChatReadStates.Indexes.CreateOne(new CreateIndexModel<ChatReadState>(Builders<ChatReadState>.IndexKeys.Ascending(x => x.SessionId).Ascending(x => x.UserId), new CreateIndexOptions { Unique = true }));
        SessionChatSettings.Indexes.CreateOne(new CreateIndexModel<SessionChatSettings>(Builders<SessionChatSettings>.IndexKeys.Ascending(x => x.SessionId), new CreateIndexOptions { Unique = true }));
        ChatThrottleStates.Indexes.CreateOne(new CreateIndexModel<ChatUserThrottleState>(Builders<ChatUserThrottleState>.IndexKeys.Ascending(x => x.SessionId).Ascending(x => x.UserId).Ascending(x => x.MessageType), new CreateIndexOptions { Unique = true }));
        ClassTrees.Indexes.CreateOne(new CreateIndexModel<ClassTreeDefinition>(Builders<ClassTreeDefinition>.IndexKeys.Ascending(x => x.DirectionId), new CreateIndexOptions { Unique = true }));
        SkillDefinitions.Indexes.CreateOne(new CreateIndexModel<SkillDefinitionRecord>(Builders<SkillDefinitionRecord>.IndexKeys.Ascending(x => x.SkillId), new CreateIndexOptions { Unique = true }));
        DefinitionVersions.Indexes.CreateOne(new CreateIndexModel<DefinitionVersion>(Builders<DefinitionVersion>.IndexKeys.Ascending(x => x.ContentName), new CreateIndexOptions { Unique = true }));
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
        ActionRequests = new MongoRepository<ActionRequest>(context.ActionRequests);
        DiceRequests = new MongoRepository<DiceRollRequest>(context.DiceRequests);
        ChatMessages = new MongoRepository<ChatMessage>(context.ChatMessages);
        ChatReadStates = new MongoRepository<ChatReadState>(context.ChatReadStates);
        SessionChatSettings = new MongoRepository<SessionChatSettings>(context.SessionChatSettings);
        ChatThrottleStates = new MongoRepository<ChatUserThrottleState>(context.ChatThrottleStates);
        AudioStates = new MongoRepository<SessionAudioState>(context.AudioStates);
        Combats = new MongoRepository<CombatState>(context.Combats);
        CombatLogs = new MongoRepository<CombatLogEntry>(context.CombatLogs);
        ClassTrees = new MongoRepository<ClassTreeDefinition>(context.ClassTrees);
        SkillDefinitions = new MongoRepository<SkillDefinitionRecord>(context.SkillDefinitions);
        DefinitionVersions = new MongoRepository<DefinitionVersion>(context.DefinitionVersions);
    }

    public IRepository<UserAccount> Accounts { get; }
    public IRepository<UserProfile> Profiles { get; }
    public IRepository<Character> Characters { get; }
    public IRepository<SessionUserState> Presence { get; }
    public IRepository<EntityLock> Locks { get; }
    public IRepository<AuditLogEntry> AuditLogs { get; }
    public IRepository<ActionRequest> ActionRequests { get; }
    public IRepository<DiceRollRequest> DiceRequests { get; }
    public IRepository<ChatMessage> ChatMessages { get; }
    public IRepository<ChatReadState> ChatReadStates { get; }
    public IRepository<SessionChatSettings> SessionChatSettings { get; }
    public IRepository<ChatUserThrottleState> ChatThrottleStates { get; }
    public IRepository<SessionAudioState> AudioStates { get; }
    public IRepository<CombatState> Combats { get; }
    public IRepository<CombatLogEntry> CombatLogs { get; }
    public IRepository<ClassTreeDefinition> ClassTrees { get; }
    public IRepository<SkillDefinitionRecord> SkillDefinitions { get; }
    public IRepository<DefinitionVersion> DefinitionVersions { get; }
}
