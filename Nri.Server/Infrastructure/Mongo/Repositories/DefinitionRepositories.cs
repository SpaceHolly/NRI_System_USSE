using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Driver;
using Nri.Shared.Domain;

namespace Nri.Server.Infrastructure.Mongo.Repositories;

public interface IClassDefinitionRepository
{
    IReadOnlyCollection<ClassDefinition> GetAll(bool includeArchived);
    ClassDefinition? GetByCode(string code);
    bool Exists(string code);
    bool Upsert(ClassDefinition definition);
    bool Archive(string code, string archivedByUserId);
}

public interface ISkillDefinitionRepository
{
    IReadOnlyCollection<SkillDefinition> GetAll(bool includeArchived);
    SkillDefinition? GetByCode(string code);
    bool Exists(string code);
    bool Upsert(SkillDefinition definition);
    bool Archive(string code, string archivedByUserId);
}

public sealed class ClassDefinitionRepository : IClassDefinitionRepository
{
    private readonly IMongoCollection<ClassDefinition> _collection;

    public ClassDefinitionRepository(IMongoCollection<ClassDefinition> collection)
    {
        _collection = collection;
    }

    public IReadOnlyCollection<ClassDefinition> GetAll(bool includeArchived)
    {
        var filter = includeArchived
            ? FilterDefinition<ClassDefinition>.Empty
            : Builders<ClassDefinition>.Filter.Eq(x => x.Archived, false);
        return _collection.Find(filter).SortBy(x => x.Level).ThenBy(x => x.Code).ToList();
    }

    public ClassDefinition? GetByCode(string code)
    {
        return _collection.Find(Builders<ClassDefinition>.Filter.Eq(x => x.Code, code)).FirstOrDefault();
    }

    public bool Exists(string code)
    {
        return _collection.Find(Builders<ClassDefinition>.Filter.Eq(x => x.Code, code)).Limit(1).Any();
    }

    public bool Upsert(ClassDefinition definition)
    {
        definition.UpdatedUtc = DateTime.UtcNow;
        var existed = Exists(definition.Code);
        if (!existed)
        {
            definition.CreatedUtc = definition.UpdatedUtc;
        }

        _collection.ReplaceOne(Builders<ClassDefinition>.Filter.Eq(x => x.Code, definition.Code), definition, new ReplaceOptions { IsUpsert = true });
        return !existed;
    }

    public bool Archive(string code, string archivedByUserId)
    {
        var update = Builders<ClassDefinition>.Update
            .Set(x => x.Archived, true)
            .Set(x => x.IsActive, false)
            .Set(x => x.Status, DefinitionStatus.Archived)
            .Set(x => x.ArchivedByUserId, archivedByUserId)
            .Set(x => x.ArchivedUtc, DateTime.UtcNow)
            .Set(x => x.UpdatedUtc, DateTime.UtcNow);
        var result = _collection.UpdateOne(Builders<ClassDefinition>.Filter.Eq(x => x.Code, code), update);
        return result.ModifiedCount > 0;
    }
}

public sealed class SkillDefinitionRepository : ISkillDefinitionRepository
{
    private readonly IMongoCollection<SkillDefinition> _collection;

    public SkillDefinitionRepository(IMongoCollection<SkillDefinition> collection)
    {
        _collection = collection;
    }

    public IReadOnlyCollection<SkillDefinition> GetAll(bool includeArchived)
    {
        var filter = includeArchived
            ? FilterDefinition<SkillDefinition>.Empty
            : Builders<SkillDefinition>.Filter.Eq(x => x.Archived, false);
        return _collection.Find(filter).SortBy(x => x.Tier).ThenBy(x => x.Code).ToList();
    }

    public SkillDefinition? GetByCode(string code)
    {
        return _collection.Find(Builders<SkillDefinition>.Filter.Eq(x => x.Code, code)).FirstOrDefault();
    }

    public bool Exists(string code)
    {
        return _collection.Find(Builders<SkillDefinition>.Filter.Eq(x => x.Code, code)).Limit(1).Any();
    }

    public bool Upsert(SkillDefinition definition)
    {
        definition.UpdatedUtc = DateTime.UtcNow;
        var existed = Exists(definition.Code);
        if (!existed)
        {
            definition.CreatedUtc = definition.UpdatedUtc;
        }

        _collection.ReplaceOne(Builders<SkillDefinition>.Filter.Eq(x => x.Code, definition.Code), definition, new ReplaceOptions { IsUpsert = true });
        return !existed;
    }

    public bool Archive(string code, string archivedByUserId)
    {
        var update = Builders<SkillDefinition>.Update
            .Set(x => x.Archived, true)
            .Set(x => x.IsActive, false)
            .Set(x => x.Status, DefinitionStatus.Archived)
            .Set(x => x.ArchivedByUserId, archivedByUserId)
            .Set(x => x.ArchivedUtc, DateTime.UtcNow)
            .Set(x => x.UpdatedUtc, DateTime.UtcNow);
        var result = _collection.UpdateOne(Builders<SkillDefinition>.Filter.Eq(x => x.Code, code), update);
        return result.ModifiedCount > 0;
    }
}
