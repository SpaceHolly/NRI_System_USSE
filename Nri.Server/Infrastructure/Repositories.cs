using System.Collections.Generic;
using Nri.Shared.Domain;

namespace Nri.Server.Infrastructure;

public interface IRepository<T> where T : EntityBase
{
    T? GetById(string id);
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
