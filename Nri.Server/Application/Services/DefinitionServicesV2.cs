using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Driver;
using Nri.Server.Infrastructure;
using Nri.Server.Logging;
using Nri.Shared.Domain;

namespace Nri.Server.Application.Services;

public interface IDefinitionRepositoryV2
{
    UnifiedDefinitionDocument GetByIdAsync(string category, string id);
    IReadOnlyCollection<UnifiedDefinitionDocument> QueryAsync(DefinitionQuery query);
    void UpsertAsync(UnifiedDefinitionDocument definition);
    void ArchiveAsync(string category, string id);
    void RestoreAsync(string category, string id);
}

public sealed class MongoDefinitionRepositoryV2 : IDefinitionRepositoryV2
{
    private readonly IMongoCollection<UnifiedDefinitionDocument> _collection;

    public MongoDefinitionRepositoryV2(MongoContext context)
    {
        _collection = context.UnifiedDefinitions;
    }

    public UnifiedDefinitionDocument GetByIdAsync(string category, string id)
    {
        return _collection.Find(Builders<UnifiedDefinitionDocument>.Filter.Eq(x => x.Category, category) & Builders<UnifiedDefinitionDocument>.Filter.Eq(x => x.Id, id)).FirstOrDefault();
    }

    public IReadOnlyCollection<UnifiedDefinitionDocument> QueryAsync(DefinitionQuery query)
    {
        var filter = Builders<UnifiedDefinitionDocument>.Filter.Eq(x => x.Category, query.Category);
        if (!query.IncludeArchived) filter &= Builders<UnifiedDefinitionDocument>.Filter.Eq(x => x.IsArchived, false);
        if (!string.IsNullOrWhiteSpace(query.RuleSetId)) filter &= Builders<UnifiedDefinitionDocument>.Filter.AnyEq(x => x.RuleSetIds, query.RuleSetId);
        if (query.Tags != null && query.Tags.Count > 0) filter &= Builders<UnifiedDefinitionDocument>.Filter.AnyIn(x => x.Tags, query.Tags);
        var result = _collection.Find(filter).Skip(Math.Max(0, query.Offset)).Limit(Math.Max(1, query.Limit)).ToList();
        if (!string.IsNullOrWhiteSpace(query.SearchText))
        {
            result = result.Where(x => x.Name.IndexOf(query.SearchText, StringComparison.OrdinalIgnoreCase) >= 0 || x.PublicDescription.IndexOf(query.SearchText, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
        }
        return result;
    }

    public void UpsertAsync(UnifiedDefinitionDocument definition)
    {
        definition.UpdatedAtUtc = DateTime.UtcNow;
        if (definition.CreatedAtUtc == default(DateTime)) definition.CreatedAtUtc = definition.UpdatedAtUtc;
        _collection.ReplaceOne(Builders<UnifiedDefinitionDocument>.Filter.Eq(x => x.Category, definition.Category) & Builders<UnifiedDefinitionDocument>.Filter.Eq(x => x.Id, definition.Id), definition, new ReplaceOptions { IsUpsert = true });
    }

    public void ArchiveAsync(string category, string id)
    {
        _collection.UpdateOne(Builders<UnifiedDefinitionDocument>.Filter.Eq(x => x.Category, category) & Builders<UnifiedDefinitionDocument>.Filter.Eq(x => x.Id, id), Builders<UnifiedDefinitionDocument>.Update.Set(x => x.IsArchived, true).Set(x => x.UpdatedAtUtc, DateTime.UtcNow));
    }

    public void RestoreAsync(string category, string id)
    {
        _collection.UpdateOne(Builders<UnifiedDefinitionDocument>.Filter.Eq(x => x.Category, category) & Builders<UnifiedDefinitionDocument>.Filter.Eq(x => x.Id, id), Builders<UnifiedDefinitionDocument>.Update.Set(x => x.IsArchived, false).Set(x => x.UpdatedAtUtc, DateTime.UtcNow));
    }
}

public interface IDefinitionServiceV2
{
    Dictionary<string, object> GetAsync(string category, string id, VisibilityContext visibilityContext);
    IReadOnlyCollection<Dictionary<string, object>> QueryAsync(DefinitionQuery query, VisibilityContext visibilityContext);
    UnifiedDefinitionDocument UpsertAsync(DefinitionUpsertRequest request, VisibilityContext visibilityContext);
    bool ArchiveAsync(string category, string id, long? expectedRevision, string actorUserId, string requestId);
    bool RestoreAsync(string category, string id, long? expectedRevision, string actorUserId, string requestId);
}

public sealed class DefinitionServiceV2 : IDefinitionServiceV2
{
    private readonly IDefinitionRepositoryV2 _repository;
    private readonly IVisibilityService _visibility;
    private readonly IEntityRevisionService _revisions;
    private readonly SyncEventService _sync;
    private readonly IServerLogger _logger;

    public DefinitionServiceV2(IDefinitionRepositoryV2 repository, IVisibilityService visibility, IEntityRevisionService revisions, SyncEventService sync, IServerLogger logger)
    {
        _repository = repository;
        _visibility = visibility;
        _revisions = revisions;
        _sync = sync;
        _logger = logger;
    }

    public Dictionary<string, object> GetAsync(string category, string id, VisibilityContext visibilityContext)
    {
        _logger.Debug($"definition.v2.get category={category} id={id}");
        var doc = _repository.GetByIdAsync(category, id);
        if (doc == null) return new Dictionary<string, object>();
        return ApplyVisibility(doc, visibilityContext);
    }

    public IReadOnlyCollection<Dictionary<string, object>> QueryAsync(DefinitionQuery query, VisibilityContext visibilityContext)
    {
        var items = _repository.QueryAsync(query);
        var mapped = items.Select(x => ApplyVisibility(x, visibilityContext)).Where(x => x != null).ToList();
        _logger.Debug($"definition.v2.query category={query.Category} count={mapped.Count}");
        return mapped;
    }

    public UnifiedDefinitionDocument UpsertAsync(DefinitionUpsertRequest request, VisibilityContext visibilityContext)
    {
        var doc = request.Definition;
        _revisions.EnsureExpectedRevisionAsync("definitionv2:" + doc.Category, doc.Id, request.ExpectedRevision);
        _repository.UpsertAsync(doc);
        _revisions.BumpRevisionAsync("definitionv2:" + doc.Category, doc.Id, request.ActorUserId, request.RequestId);
        _logger.Debug($"definition.v2.upsert category={doc.Category} id={doc.Id}");
        TryPublish(doc.Category, doc.Id, "updated", request.ActorUserId, request.RequestId);
        return doc;
    }

    public bool ArchiveAsync(string category, string id, long? expectedRevision, string actorUserId, string requestId)
    {
        _revisions.EnsureExpectedRevisionAsync("definitionv2:" + category, id, expectedRevision);
        _repository.ArchiveAsync(category, id);
        _revisions.BumpRevisionAsync("definitionv2:" + category, id, actorUserId, requestId);
        _logger.Debug($"definition.v2.archive category={category} id={id}");
        TryPublish(category, id, "archived", actorUserId, requestId);
        return true;
    }

    public bool RestoreAsync(string category, string id, long? expectedRevision, string actorUserId, string requestId)
    {
        _revisions.EnsureExpectedRevisionAsync("definitionv2:" + category, id, expectedRevision);
        _repository.RestoreAsync(category, id);
        _revisions.BumpRevisionAsync("definitionv2:" + category, id, actorUserId, requestId);
        TryPublish(category, id, "restored", actorUserId, requestId);
        return true;
    }

    private Dictionary<string, object> ApplyVisibility(UnifiedDefinitionDocument doc, VisibilityContext context)
    {
        var payload = new Dictionary<string, object>
        {
            { "id", doc.Id }, { "category", doc.Category }, { "name", doc.Name }, { "publicDescription", doc.PublicDescription },
            { "gmDescription", doc.GMDescription }, { "serverOnlyData", doc.ServerOnlyData }, { "extraData", doc.ExtraData },
            { "visibilityRule", doc.VisibilityRule }, { "tags", doc.Tags.Cast<object>().ToArray() }, { "schemaVersion", doc.SchemaVersion }, { "isArchived", doc.IsArchived }
        };
        var filtered = _visibility.FilterDefinitionPayload(payload, context, doc.Category, doc.Id);
        if (filtered != null && filtered.Count != payload.Count) _logger.Debug($"definition.v2.visibility.filtered category={doc.Category} id={doc.Id}");
        return filtered;
    }

    private void TryPublish(string category, string id, string operation, string actorUserId, string requestId)
    {
        try
        {
            _sync.Publish("definitions.updated", SyncScopes.Definitions, "definition", id, operation, actorUserId, new Dictionary<string, object> { { "category", category }, { "definitionId", id }, { "operation", operation }, { "updatedUtc", DateTime.UtcNow } }, requestId);
        }
        catch (Exception ex)
        {
            _logger.Error($"definition.v2.sync.publish.error category={category} id={id} message={ex.Message}", ex);
        }
    }
}

public static class LegacyDefinitionAdapter
{
    public static UnifiedDefinitionDocument SkillToUnified(SkillDefinition source)
    {
        return new UnifiedDefinitionDocument { Id = source.Code, Category = "skill", Name = source.Name, PublicDescription = source.Description, VisibilityRule = source.VisibilityRule, IsArchived = source.IsArchived, SourceDocument = "skill_definitions" };
    }

    public static UnifiedDefinitionDocument ClassToUnified(ClassDefinition source)
    {
        return new UnifiedDefinitionDocument { Id = source.Code, Category = "class", Name = source.Name, PublicDescription = source.Description, VisibilityRule = "public", IsArchived = source.Archived, SourceDocument = "class_definitions" };
    }

    public static UnifiedDefinitionDocument RaceToUnified(RaceDefinition source)
    {
        return new UnifiedDefinitionDocument { Id = source.Code, Category = "race", Name = source.Name, PublicDescription = source.Description, VisibilityRule = "public", IsArchived = source.Archived, SourceDocument = "race_definitions" };
    }

    public static SkillDefinition UnifiedToSkill(UnifiedDefinitionDocument source) { return new SkillDefinition { Code = source.Id, Name = source.Name, Description = source.PublicDescription, VisibilityRule = source.VisibilityRule, IsArchived = source.IsArchived }; }
    public static ClassDefinition UnifiedToClass(UnifiedDefinitionDocument source) { return new ClassDefinition { Code = source.Id, Name = source.Name, Description = source.PublicDescription, Archived = source.IsArchived }; }
    public static RaceDefinition UnifiedToRace(UnifiedDefinitionDocument source) { return new RaceDefinition { Code = source.Id, Name = source.Name, Description = source.PublicDescription, Archived = source.IsArchived }; }
}
