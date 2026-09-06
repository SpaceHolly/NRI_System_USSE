using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Bson;
using MongoDB.Driver;
using Nri.Server.Infrastructure;
using Nri.Shared.Domain;

namespace Nri.Server.Application;

public interface IMapGenerationService
{
    MapGenerationPreviewHandle0205 CreatePreview(MapGenerationPreviewInput0205 input);
    MapGenerationPreviewHandle0205 GetPreview(string previewId, string actorUserId);
    bool CancelPreview(string previewId, string actorUserId);
    MapGenerationApplyGate0205 BeginApply(MapGenerationApplyInput0205 input);
    BsonDocument CompleteApply(MapGenerationApplyGate0205 gate, long appliedMapRevision, Dictionary<string, object> resultSummary);
    void FailApply(MapGenerationApplyGate0205 gate, string message);
}

public sealed class MapGenerationPreviewInput0205
{
    public string MapId { get; set; } = string.Empty;
    public long ExpectedMapRevision { get; set; }
    public string ActorUserId { get; set; } = string.Empty;
    public string TemplateId { get; set; } = string.Empty;
    public int TemplateRevision { get; set; }
    public string PresetId { get; set; } = string.Empty;
    public int PresetRevision { get; set; }
    public string Seed { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = string.Empty;
    public string Fingerprint { get; set; } = string.Empty;
    public BsonDocument Blueprint { get; set; } = new BsonDocument();
    public Dictionary<string, object> Summary { get; set; } = new Dictionary<string, object>();
    public List<string> Warnings { get; set; } = new List<string>();
}

public sealed class MapGenerationApplyInput0205
{
    public string PreviewId { get; set; } = string.Empty;
    public string OperationId { get; set; } = string.Empty;
    public string MapId { get; set; } = string.Empty;
    public long ExpectedMapRevision { get; set; }
    public string Fingerprint { get; set; } = string.Empty;
    public string ActorUserId { get; set; } = string.Empty;
}

public sealed class MapGenerationPreviewHandle0205
{
    public MapGenerationPreview0205 Preview { get; set; } = new MapGenerationPreview0205();
    public BsonDocument Blueprint { get; set; } = new BsonDocument();
    public string ActorUserId { get; set; } = string.Empty;
}

public sealed class MapGenerationApplyGate0205
{
    public bool AlreadyApplied { get; set; }
    public MapGenerationPreviewHandle0205 PreviewHandle { get; set; } = new MapGenerationPreviewHandle0205();
    public string RunId { get; set; } = string.Empty;
    public string OperationId { get; set; } = string.Empty;
    public BsonDocument ExistingRun { get; set; } = new BsonDocument();
}

public sealed class MapGenerationException0205 : InvalidOperationException
{
    public MapGenerationException0205(string kind, string message) : base(message) => Kind = kind;
    public string Kind { get; }
}

public sealed class MapGenerationService0205 : IMapGenerationService
{
    public const string RunsCollectionName = "scene_map_generation_runs";
    private readonly ConcurrentDictionary<string, MapGenerationPreviewHandle0205> _previews = new(StringComparer.OrdinalIgnoreCase);
    private readonly IMapIdentityResolver _identity;
    private readonly IMongoCollection<BsonDocument> _runs;

    public MapGenerationService0205(MongoContext mongo, IMapIdentityResolver identity)
    {
        _identity = identity ?? throw new ArgumentNullException(nameof(identity));
        _runs = (mongo ?? throw new ArgumentNullException(nameof(mongo))).Database.GetCollection<BsonDocument>(RunsCollectionName);
        _runs.Indexes.CreateMany(new[]
        {
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("OperationId"), new CreateIndexOptions { Unique = true, Sparse = true }),
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("GeneratedSceneMapId")),
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("Status"))
        });
    }

    public MapGenerationPreviewHandle0205 CreatePreview(MapGenerationPreviewInput0205 input)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));
        var actor = Required(input.ActorUserId, "actorUserId");
        var resolution = Resolve(input.MapId);
        var map = resolution.CanonicalMap!;
        if (map.EditorRevision != input.ExpectedMapRevision)
            throw Failure("conflict", $"Карта была изменена. Текущая редакция: {map.EditorRevision}; ожидалась: {input.ExpectedMapRevision}.");
        if (string.IsNullOrWhiteSpace(input.Fingerprint)) throw Failure("validation", "Не удалось вычислить отпечаток предпросмотра.");
        var fingerprint = MapGenerationDeterminism0205.ComputeFingerprint(
            input.TemplateId,
            input.TemplateRevision,
            input.PresetId,
            input.PresetRevision,
            input.Seed,
            map.WidthMeters,
            map.HeightMeters,
            input.RuleSetId,
            input.Fingerprint);

        RemoveExpired();
        var previewId = "map_generation_preview_" + Guid.NewGuid().ToString("N");
        var handle = new MapGenerationPreviewHandle0205
        {
            ActorUserId = actor,
            Blueprint = new BsonDocument(input.Blueprint),
            Preview = new MapGenerationPreview0205
            {
                PreviewId = previewId,
                CanonicalMapId = resolution.CanonicalMapId,
                MapRevision = map.EditorRevision,
                TemplateId = input.TemplateId,
                TemplateRevision = input.TemplateRevision,
                PresetId = input.PresetId,
                PresetRevision = input.PresetRevision,
                Seed = input.Seed,
                RuleSetId = input.RuleSetId,
                Fingerprint = fingerprint,
                BuiltAtUtc = DateTime.UtcNow,
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(30),
                Summary = new Dictionary<string, object>(input.Summary, StringComparer.OrdinalIgnoreCase),
                Warnings = input.Warnings.ToList()
            }
        };
        _previews[previewId] = handle;
        return handle;
    }

    public MapGenerationPreviewHandle0205 GetPreview(string previewId, string actorUserId)
    {
        RemoveExpired();
        if (!_previews.TryGetValue(Required(previewId, "previewId"), out var handle))
            throw Failure("not_found", "Предпросмотр не найден или срок его действия истёк.");
        if (!string.Equals(handle.ActorUserId, Required(actorUserId, "actorUserId"), StringComparison.OrdinalIgnoreCase))
            throw Failure("forbidden", "Предпросмотр создан другим пользователем.");
        return handle;
    }

    public bool CancelPreview(string previewId, string actorUserId)
    {
        var handle = GetPreview(previewId, actorUserId);
        return _previews.TryRemove(handle.Preview.PreviewId, out _);
    }

    public MapGenerationApplyGate0205 BeginApply(MapGenerationApplyInput0205 input)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));
        var operationId = Required(input.OperationId, "operationId");
        var existing = _runs.Find(Builders<BsonDocument>.Filter.Eq("OperationId", operationId)).FirstOrDefault();
        if (existing != null)
        {
            if (Text(existing, "Status") == MapGenerationRuntime0205.AppliedStatus)
                return new MapGenerationApplyGate0205 { AlreadyApplied = true, OperationId = operationId, RunId = Text(existing, "Id"), ExistingRun = existing };
            throw Failure("conflict", "Операция применения уже выполнялась и не может быть повторена автоматически.");
        }

        var handle = GetPreview(input.PreviewId, input.ActorUserId);
        if (!string.Equals(handle.Preview.CanonicalMapId, Resolve(input.MapId).CanonicalMapId, StringComparison.OrdinalIgnoreCase))
            throw Failure("validation", "Предпросмотр относится к другой карте.");
        if (!string.Equals(handle.Preview.Fingerprint, Required(input.Fingerprint, "fingerprint"), StringComparison.OrdinalIgnoreCase))
            throw Failure("conflict", "Предпросмотр изменился. Создайте его заново.");
        if (handle.Preview.MapRevision != input.ExpectedMapRevision)
            throw Failure("conflict", "Редакция карты не совпадает с редакцией предпросмотра.");
        var current = Resolve(input.MapId).CanonicalMap!;
        if (current.EditorRevision != input.ExpectedMapRevision)
            throw Failure("conflict", $"Карта была изменена. Текущая редакция: {current.EditorRevision}; ожидалась: {input.ExpectedMapRevision}.");

        var sameResult = _runs.Find(Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("GeneratedSceneMapId", handle.Preview.CanonicalMapId),
            Builders<BsonDocument>.Filter.Eq("NormalizedHash", handle.Preview.Fingerprint),
            Builders<BsonDocument>.Filter.Eq("Status", MapGenerationRuntime0205.AppliedStatus))).FirstOrDefault();
        if (sameResult != null)
            return new MapGenerationApplyGate0205 { AlreadyApplied = true, OperationId = operationId, RunId = Text(sameResult, "Id"), ExistingRun = sameResult };

        var runId = "map_generation_run_" + Guid.NewGuid().ToString("N");
        var now = DateTime.UtcNow;
        var reservation = new BsonDocument
        {
            ["_id"] = runId, ["Id"] = runId, ["OperationId"] = operationId,
            ["PreviewId"] = handle.Preview.PreviewId, ["GeneratedSceneMapId"] = handle.Preview.CanonicalMapId,
            ["TemplateId"] = handle.Preview.TemplateId, ["TemplateRevision"] = handle.Preview.TemplateRevision,
            ["PresetId"] = handle.Preview.PresetId, ["PresetRevision"] = handle.Preview.PresetRevision,
            ["Seed"] = handle.Preview.Seed, ["NormalizedHash"] = handle.Preview.Fingerprint,
            ["ExpectedMapRevision"] = input.ExpectedMapRevision, ["Status"] = MapGenerationRuntime0205.ApplyingStatus,
            ["CreatedByAccountId"] = input.ActorUserId, ["CreatedAtUtc"] = now, ["UpdatedAtUtc"] = now,
            ["ServiceVersion"] = MapGenerationRuntime0205.ServiceVersion, ["IsArchived"] = false
        };
        try { _runs.InsertOne(reservation); }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            var replay = _runs.Find(Builders<BsonDocument>.Filter.Eq("OperationId", operationId)).FirstOrDefault();
            if (replay != null && Text(replay, "Status") == MapGenerationRuntime0205.AppliedStatus)
                return new MapGenerationApplyGate0205 { AlreadyApplied = true, OperationId = operationId, RunId = Text(replay, "Id"), ExistingRun = replay };
            throw Failure("conflict", "Операция применения уже зарезервирована.");
        }
        return new MapGenerationApplyGate0205 { PreviewHandle = handle, OperationId = operationId, RunId = runId };
    }

    public BsonDocument CompleteApply(MapGenerationApplyGate0205 gate, long appliedMapRevision, Dictionary<string, object> resultSummary)
    {
        var now = DateTime.UtcNow;
        var update = Builders<BsonDocument>.Update
            .Set("Status", MapGenerationRuntime0205.AppliedStatus)
            .Set("AppliedMapRevision", appliedMapRevision)
            .Set("ResultSummary", new BsonDocument(resultSummary.Select(pair => new BsonElement(pair.Key, BsonValue.Create(pair.Value)))))
            .Set("AppliedAtUtc", now).Set("UpdatedAtUtc", now);
        _runs.UpdateOne(Builders<BsonDocument>.Filter.Eq("Id", gate.RunId), update);
        _previews.TryRemove(gate.PreviewHandle.Preview.PreviewId, out _);
        return _runs.Find(Builders<BsonDocument>.Filter.Eq("Id", gate.RunId)).First();
    }

    public void FailApply(MapGenerationApplyGate0205 gate, string message)
    {
        if (gate == null || string.IsNullOrWhiteSpace(gate.RunId)) return;
        _runs.UpdateOne(Builders<BsonDocument>.Filter.Eq("Id", gate.RunId), Builders<BsonDocument>.Update
            .Set("Status", MapGenerationRuntime0205.FailedStatus)
            .Set("FailureSummary", (message ?? string.Empty).Length > 500 ? message.Substring(0, 500) : message ?? string.Empty)
            .Set("UpdatedAtUtc", DateTime.UtcNow));
    }

    private MapIdentityResolution0202 Resolve(string mapId)
    {
        var resolution = _identity.ResolveSceneMap(Required(mapId, "mapId"));
        if (resolution.IsResolved) return resolution;
        throw Failure(resolution.Status == MapIdentityResolutionStatus0202.NotFound ? "not_found" : "conflict", resolution.Message);
    }

    private void RemoveExpired()
    {
        var now = DateTime.UtcNow;
        foreach (var pair in _previews.Where(pair => pair.Value.Preview.ExpiresAtUtc <= now).ToArray())
            _previews.TryRemove(pair.Key, out _);
    }

    private static string Required(string value, string field)
        => string.IsNullOrWhiteSpace(value) ? throw Failure("validation", $"Поле {field} обязательно.") : value.Trim();
    private static string Text(BsonDocument document, string field)
        => document.TryGetValue(field, out var value) && value.IsString ? value.AsString : string.Empty;
    private static MapGenerationException0205 Failure(string kind, string message) => new MapGenerationException0205(kind, message);
}
