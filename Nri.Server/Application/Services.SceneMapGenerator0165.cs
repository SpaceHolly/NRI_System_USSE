using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MongoDB.Bson;
using MongoDB.Driver;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;
using Nri.Shared.Utilities;

namespace Nri.Server.Application;

public partial class ServiceHub
{
    private const string SceneMap0165PresetsCollection = "scene_map_generation_presets";
    private const string SceneMap0165TemplatesCollection = "scene_map_templates";
    private const string SceneMap0165RunsCollection = "scene_map_generation_runs";
    private const string SceneMap0165DefaultSessionId = "dev_session_0162";
    private const string SceneMap0165GmLeakToken = "GM_ONLY_GENERATOR_0165_DO_NOT_LEAK";

    public ResponseEnvelope SceneMapGeneratorAdminListPresets0165(CommandContext context)
    {
        RequireAdmin(context);
        if (!SceneMapGenerator0165Enabled()) return SceneMapGenerator0165Disabled(context.Request.Command);
        EnsureSceneMapGenerator0165BuiltIns();
        var items = SceneMapGenerator0165Presets()
            .Find(Builders<BsonDocument>.Filter.Ne("IsArchived", true))
            .Sort(Builders<BsonDocument>.Sort.Ascending("SortOrder").Ascending("DisplayName"))
            .ToList()
            .Select(x => SceneMapGenerator0165PresetPayload(x))
            .Cast<object>()
            .ToArray();
        return Ok("Location generator presets loaded.", new Dictionary<string, object>
        {
            ["items"] = items,
            ["presets"] = items,
            ["count"] = items.Length,
            ["sourceCollection"] = SceneMap0165PresetsCollection
        });
    }

    public ResponseEnvelope SceneMapGeneratorAdminGetPreset0165(CommandContext context)
    {
        RequireAdmin(context);
        if (!SceneMapGenerator0165Enabled()) return SceneMapGenerator0165Disabled(context.Request.Command);
        EnsureSceneMapGenerator0165BuiltIns();
        var presetId = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(context.Request.Payload, "presetId"), PayloadReader.GetString(context.Request.Payload, "id")), 1, 128, "presetId");
        var doc = SceneMapGenerator0165Presets().Find(ActiveIdFilter(presetId)).FirstOrDefault();
        if (doc == null) return Error("location generator preset not found", ResponseStatus.NotFound, ErrorCode.NotFound);
        return Ok("Location generator preset loaded.", new Dictionary<string, object> { ["preset"] = SceneMapGenerator0165PresetPayload(doc), ["presetId"] = presetId });
    }

    public ResponseEnvelope SceneMapGeneratorAdminCreatePreset0165(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!SceneMapGenerator0165Enabled()) return SceneMapGenerator0165Disabled(context.Request.Command);
        var doc = BuildSceneMapGenerator0165PresetDoc(context.Request.Payload ?? new Dictionary<string, object>(), actor.Id, DateTime.UtcNow, null);
        SceneMapGenerator0165Presets().ReplaceOne(IdFilter(GetDocString(doc, "Id")), doc, new ReplaceOptions { IsUpsert = true });
        return Ok("Location generator preset saved.", new Dictionary<string, object> { ["presetId"] = GetDocString(doc, "Id"), ["preset"] = SceneMapGenerator0165PresetPayload(doc) });
    }

    public ResponseEnvelope SceneMapGeneratorAdminUpdatePreset0165(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!SceneMapGenerator0165Enabled()) return SceneMapGenerator0165Disabled(context.Request.Command);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var presetId = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "presetId"), PayloadReader.GetString(payload, "id")), 1, 128, "presetId");
        var existing = SceneMapGenerator0165Presets().Find(ActiveIdFilter(presetId)).FirstOrDefault();
        if (existing == null) return Error("location generator preset not found", ResponseStatus.NotFound, ErrorCode.NotFound);
        var doc = BuildSceneMapGenerator0165PresetDoc(payload, actor.Id, DateTime.UtcNow, existing);
        doc["_id"] = presetId;
        doc["Id"] = presetId;
        SceneMapGenerator0165Presets().ReplaceOne(IdFilter(presetId), doc, new ReplaceOptions { IsUpsert = true });
        return Ok("Location generator preset updated.", new Dictionary<string, object> { ["presetId"] = presetId, ["preset"] = SceneMapGenerator0165PresetPayload(doc) });
    }

    public ResponseEnvelope SceneMapGeneratorAdminArchivePreset0165(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!SceneMapGenerator0165Enabled()) return SceneMapGenerator0165Disabled(context.Request.Command);
        var presetId = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(context.Request.Payload, "presetId"), PayloadReader.GetString(context.Request.Payload, "id")), 1, 128, "presetId");
        var result = SceneMapGenerator0165Presets().UpdateOne(ActiveIdFilter(presetId), Builders<BsonDocument>.Update
            .Set("IsArchived", true)
            .Set("UpdatedAtUtc", DateTime.UtcNow)
            .Set("UpdatedByUserId", actor.Id));
        if (result.MatchedCount == 0) return Error("location generator preset not found", ResponseStatus.NotFound, ErrorCode.NotFound);
        return Ok("Location generator preset archived.", new Dictionary<string, object> { ["presetId"] = presetId });
    }

    public ResponseEnvelope SceneMapGeneratorAdminListTemplates0165(CommandContext context)
    {
        RequireAdmin(context);
        if (!SceneMapGenerator0165Enabled()) return SceneMapGenerator0165Disabled(context.Request.Command);
        EnsureSceneMapGenerator0165BuiltIns();
        var items = SceneMapGenerator0165Templates()
            .Find(Builders<BsonDocument>.Filter.Ne("IsArchived", true))
            .Sort(Builders<BsonDocument>.Sort.Ascending("SortOrder").Ascending("DisplayName"))
            .ToList()
            .Select(x => SceneMapGenerator0165TemplatePayload(x, includeBlueprints: false))
            .Cast<object>()
            .ToArray();
        return Ok("Location map templates loaded.", new Dictionary<string, object>
        {
            ["items"] = items,
            ["templates"] = items,
            ["count"] = items.Length,
            ["sourceCollection"] = SceneMap0165TemplatesCollection
        });
    }

    public ResponseEnvelope SceneMapGeneratorAdminGetTemplate0165(CommandContext context)
    {
        RequireAdmin(context);
        if (!SceneMapGenerator0165Enabled()) return SceneMapGenerator0165Disabled(context.Request.Command);
        EnsureSceneMapGenerator0165BuiltIns();
        var templateId = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(context.Request.Payload, "templateId"), PayloadReader.GetString(context.Request.Payload, "id")), 1, 128, "templateId");
        var doc = SceneMapGenerator0165Templates().Find(ActiveIdFilter(templateId)).FirstOrDefault();
        if (doc == null) return Error("location map template not found", ResponseStatus.NotFound, ErrorCode.NotFound);
        return Ok("Location map template loaded.", new Dictionary<string, object> { ["templateId"] = templateId, ["template"] = SceneMapGenerator0165TemplatePayload(doc, includeBlueprints: true) });
    }

    public ResponseEnvelope SceneMapGeneratorAdminCreateTemplateFromMap0165(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!SceneMapGenerator0165Enabled()) return SceneMapGenerator0165Disabled(context.Request.Command);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var suppliedSourceMapId = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "sceneMapId"), PayloadReader.GetString(payload, "mapId"), PayloadReader.GetString(payload, "templateSourceMapId")), 1, 128, "mapId");
        var sourceIdentity = _mapIdentityResolver.ResolveSceneMap(suppliedSourceMapId);
        if (!sourceIdentity.IsResolved) return MapIdentityError0202(sourceIdentity);
        var sourceMapId = sourceIdentity.CanonicalMapId;
        var map = sourceIdentity.CompatibilityProjection!;
        var now = DateTime.UtcNow;
        var templateId = FirstNonEmptyWorld(PayloadReader.GetString(payload, "templateId"), "template_from_map_" + Guid.NewGuid().ToString("N"));
        var doc = new BsonDocument
        {
            ["_id"] = templateId,
            ["Id"] = templateId,
            ["DisplayName"] = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "displayName"), "Шаблон: " + GetDocString(map, "DisplayName")), 1, 160, "displayName"),
            ["Description"] = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "description"), "Создан из карты " + GetDocString(map, "DisplayName")), 0, 4096, "description"),
            ["LocationKind"] = NormalizeSceneMapGenerator0165LocationKind(PayloadReader.GetString(payload, "locationKind")),
            ["MapScale"] = GetDocString(map, "MapScale", "Location"),
            ["WidthMeters"] = GetDocInt(map, "WidthMeters", 100),
            ["HeightMeters"] = GetDocInt(map, "HeightMeters", 100),
            ["TileSizeMeters"] = GetDocDouble(map, "DefaultTileSizeMeters", GetDocDouble(map, "GridSizeMeters", 5)),
            ["GridSizeMeters"] = GetDocDouble(map, "GridSizeMeters", 5),
            ["TemplateSourceMapId"] = sourceMapId,
            ["TileLayerBlueprints"] = new BsonArray(SceneMap0164TileLayerDocsForMap(sourceMapId, includeHidden: true).Select(CloneTemplateTileLayer)),
            ["TilePatchBlueprints"] = new BsonArray(SceneMap0164TilePatchDocsForMap(sourceMapId, includeHidden: true).Select(CloneTemplateTilePatch)),
            ["AssetInstanceBlueprints"] = new BsonArray(SceneMap0164AssetInstanceDocsForMap(sourceMapId, includeHidden: true).Select(CloneTemplateAsset)),
            ["ShapeBlueprints"] = new BsonArray(SceneMap0164ShapeDocsForMap(sourceMapId, includeHidden: true).Select(CloneTemplateShape)),
            ["MarkerBlueprints"] = new BsonArray(SceneMap0162MarkerDocs(sourceMapId, includeHidden: true).Select(CloneTemplateMarker)),
            ["TokenBlueprints"] = new BsonArray(),
            ["IsBuiltIn"] = false,
            ["IsArchived"] = false,
            ["CreatedAtUtc"] = now,
            ["UpdatedAtUtc"] = now,
            ["CreatedByUserId"] = actor.Id,
            ["UpdatedByUserId"] = actor.Id
        };
        SceneMapGenerator0165Templates().ReplaceOne(IdFilter(templateId), doc, new ReplaceOptions { IsUpsert = true });
        return Ok("Location map template created.", new Dictionary<string, object> { ["templateId"] = templateId, ["template"] = SceneMapGenerator0165TemplatePayload(doc, includeBlueprints: true) });
    }

    public ResponseEnvelope SceneMapGeneratorAdminArchiveTemplate0165(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!SceneMapGenerator0165Enabled()) return SceneMapGenerator0165Disabled(context.Request.Command);
        var templateId = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(context.Request.Payload, "templateId"), PayloadReader.GetString(context.Request.Payload, "id")), 1, 128, "templateId");
        var result = SceneMapGenerator0165Templates().UpdateOne(ActiveIdFilter(templateId), Builders<BsonDocument>.Update
            .Set("IsArchived", true)
            .Set("UpdatedAtUtc", DateTime.UtcNow)
            .Set("UpdatedByUserId", actor.Id));
        if (result.MatchedCount == 0) return Error("location map template not found", ResponseStatus.NotFound, ErrorCode.NotFound);
        return Ok("Location map template archived.", new Dictionary<string, object> { ["templateId"] = templateId });
    }

    public ResponseEnvelope SceneMapGeneratorAdminPreview0165(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!SceneMapGenerator0165Enabled()) return SceneMapGenerator0165Disabled(context.Request.Command);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var suppliedMapId = RequireLength(PayloadReader.GetString(payload, "mapId"), 1, 128, "mapId");
        var identity = _mapIdentityResolver.ResolveSceneMap(suppliedMapId);
        if (!identity.IsResolved) return MapIdentityError0202(identity);
        payload["mapId"] = identity.CanonicalMapId;
        payload["widthMeters"] = identity.CanonicalMap!.WidthMeters;
        payload["heightMeters"] = identity.CanonicalMap.HeightMeters;
        payload["gridSizeMeters"] = identity.CanonicalMap.GridCellSizeMeters;
        payload["mapScale"] = SceneMapGenerator0205ScaleForBounds(identity.CanonicalMap.WidthMeters, identity.CanonicalMap.HeightMeters);
        var output = BuildSceneMapGenerator0165Output(payload, actor.Id, identity.CanonicalMapId);
        var validation = ValidateSceneMapGenerator0165Output(output);
        if (validation != null) return validation;
        try
        {
            var preview = _mapGenerationService.CreatePreview(new MapGenerationPreviewInput0205
            {
                MapId = identity.CanonicalMapId,
                ExpectedMapRevision = PayloadReader.GetLong(payload, "expectedMapRevision") ?? identity.CanonicalMap.EditorRevision,
                ActorUserId = actor.Id,
                TemplateId = output.TemplateId,
                TemplateRevision = SceneMapGenerator0165DefinitionRevision(SceneMapGenerator0165Templates(), output.TemplateId),
                PresetId = output.PresetId,
                PresetRevision = SceneMapGenerator0165DefinitionRevision(SceneMapGenerator0165Presets(), output.PresetId),
                Seed = output.Seed,
                RuleSetId = output.RuleSetId,
                Fingerprint = output.NormalizedHash,
                Blueprint = output.BlueprintDoc(),
                Summary = SceneMapGenerator0165SummaryPayload(output),
                Warnings = SceneMapGenerator0205Warnings(output)
            });
            var response = SceneMapGenerator0165OutputPayload(output, SceneMapGenerator0205PreviewDoc(preview), identity.CanonicalMapId);
            response["previewId"] = preview.Preview.PreviewId;
            response["previewFingerprint"] = preview.Preview.Fingerprint;
            response["mapRevision"] = preview.Preview.MapRevision;
            response["warnings"] = preview.Preview.Warnings.Cast<object>().ToArray();
            response["previewIsTransient"] = true;
            return Ok("Предпросмотр карты создан без изменения карты.", response);
        }
        catch (MapGenerationException0205 ex)
        {
            return SceneMapGenerator0205Error(ex);
        }
    }

    public ResponseEnvelope SceneMapGeneratorAdminCancelPreview0205(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!SceneMapGenerator0165Enabled()) return SceneMapGenerator0165Disabled(context.Request.Command);
        try
        {
            var previewId = RequireLength(PayloadReader.GetString(context.Request.Payload, "previewId"), 1, 128, "previewId");
            _mapGenerationService.CancelPreview(previewId, actor.Id);
            return Ok("Предпросмотр отменён.", new Dictionary<string, object> { ["previewId"] = previewId, ["cancelled"] = true });
        }
        catch (MapGenerationException0205 ex)
        {
            return SceneMapGenerator0205Error(ex);
        }
    }

    public ResponseEnvelope SceneMapGeneratorAdminGenerate0165(CommandContext context)
        => SceneMapGeneratorAdminSavePreviewAsSceneMap0165(context);

    public ResponseEnvelope SceneMapGeneratorAdminRegenerate0165(CommandContext context)
        => SceneMapGeneratorAdminPreview0165(context);

    public ResponseEnvelope SceneMapGeneratorAdminSavePreviewAsSceneMap0165(CommandContext context)
    {
        var actor = RequireAdmin(context);
        if (!SceneMapGenerator0165Enabled()) return SceneMapGenerator0165Disabled(context.Request.Command);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        try
        {
            var previewId = RequireLength(PayloadReader.GetString(payload, "previewId"), 1, 128, "previewId");
            var gate = _mapGenerationService.BeginApply(new MapGenerationApplyInput0205
            {
                PreviewId = previewId,
                OperationId = RequireLength(PayloadReader.GetString(payload, "operationId"), 1, 160, "operationId"),
                MapId = RequireLength(PayloadReader.GetString(payload, "mapId"), 1, 128, "mapId"),
                ExpectedMapRevision = PayloadReader.GetLong(payload, "expectedMapRevision") ?? -1,
                Fingerprint = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "previewFingerprint"), PayloadReader.GetString(payload, "fingerprint")), 1, 128, "previewFingerprint"),
                ActorUserId = actor.Id
            });
            if (gate.AlreadyApplied)
                return Ok("Этот результат уже применён к карте.", SceneMapGenerator0205ReplayPayload(gate.ExistingRun));

            var currentTemplateRevision = SceneMapGenerator0165DefinitionRevision(SceneMapGenerator0165Templates(), gate.PreviewHandle.Preview.TemplateId);
            var currentPresetRevision = SceneMapGenerator0165DefinitionRevision(SceneMapGenerator0165Presets(), gate.PreviewHandle.Preview.PresetId);
            if (currentTemplateRevision != gate.PreviewHandle.Preview.TemplateRevision || currentPresetRevision != gate.PreviewHandle.Preview.PresetRevision)
            {
                _mapGenerationService.FailApply(gate, "Template or preset revision changed before apply.");
                throw new MapGenerationException0205("conflict", "Шаблон или пресет изменился после предпросмотра. Создайте предпросмотр заново.");
            }

            var output = SceneMap0165OutputFromPreview0205(gate.PreviewHandle, actor.Id);
            try
            {
                var appliedRevision = ApplySceneMapGenerator0205Output(output, gate, actor.Id);
                var run = _mapGenerationService.CompleteApply(gate, appliedRevision, SceneMapGenerator0165SummaryPayload(output));
                var response = SceneMapGenerator0165OutputPayload(output, run, output.MapId);
                response["alreadyApplied"] = false;
                response["mapRevision"] = appliedRevision;
                response["previewFingerprint"] = output.NormalizedHash;
                return Ok("Предпросмотр применён к карте.", response);
            }
            catch (Exception ex)
            {
                _mapGenerationService.FailApply(gate, ex.Message);
                throw;
            }
        }
        catch (MapGenerationException0205 ex)
        {
            return SceneMapGenerator0205Error(ex);
        }
        catch (MapEditorMutationException0203 ex)
        {
            var status = ex.Kind == "not_found" ? ResponseStatus.NotFound : ex.Kind == "conflict" ? ResponseStatus.Conflict : ResponseStatus.ValidationFailed;
            var code = ex.Kind == "not_found" ? ErrorCode.NotFound : ex.Kind == "conflict" ? ErrorCode.Conflict : ErrorCode.ValidationFailed;
            return Error(ex.Message, status, code);
        }
    }

    public ResponseEnvelope SceneMapGeneratorAdminGenerateAndSetSessionActive0165(CommandContext context)
        => SceneMapGeneratorAdminSavePreviewAsSceneMap0165(context);

    public ResponseEnvelope SceneMapGeneratorAdminGetGenerationRun0165(CommandContext context)
    {
        RequireAdmin(context);
        if (!SceneMapGenerator0165Enabled()) return SceneMapGenerator0165Disabled(context.Request.Command);
        var runId = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(context.Request.Payload, "runId"), PayloadReader.GetString(context.Request.Payload, "generationRunId"), PayloadReader.GetString(context.Request.Payload, "id")), 1, 128, "runId");
        var doc = SceneMapGenerator0165Runs().Find(ActiveIdFilter(runId)).FirstOrDefault();
        if (doc == null) return Error("generation run not found", ResponseStatus.NotFound, ErrorCode.NotFound);
        return Ok("Location map generation run loaded.", new Dictionary<string, object> { ["runId"] = runId, ["run"] = SceneMapGenerator0165RunPayload(doc) });
    }

    private static int SceneMapGenerator0165DefinitionRevision(IMongoCollection<BsonDocument> collection, string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return 0;
        var document = collection.Find(ActiveIdFilter(id)).FirstOrDefault();
        return document == null ? 0 : GetDocInt(document, "Revision", 1);
    }

    private static List<string> SceneMapGenerator0205Warnings(SceneMapGenerator0165Output output)
    {
        var warnings = new List<string>();
        var hidden = output.AssetInstances.Concat(output.Shapes).Concat(output.Markers)
            .Count(item => !string.Equals(GetDocString(item, "Visibility"), "PlayerVisible", StringComparison.OrdinalIgnoreCase));
        if (hidden > 0) warnings.Add($"Скрытые от игроков объекты: {hidden}.");
        if (output.AssetInstances.Count + output.Shapes.Count + output.Markers.Count > 120)
            warnings.Add("На карте будет создано более 120 объектов; первое отображение может занять немного больше времени.");
        return warnings;
    }

    private static BsonDocument SceneMapGenerator0205PreviewDoc(MapGenerationPreviewHandle0205 handle)
        => new BsonDocument
        {
            ["Id"] = handle.Preview.PreviewId,
            ["Status"] = MapGenerationRuntime0205.PreviewStatus,
            ["NormalizedHash"] = handle.Preview.Fingerprint,
            ["GeneratedSceneMapId"] = handle.Preview.CanonicalMapId
        };

    private ResponseEnvelope SceneMapGenerator0205Error(MapGenerationException0205 exception)
    {
        var status = exception.Kind == "not_found" ? ResponseStatus.NotFound
            : exception.Kind == "forbidden" ? ResponseStatus.Unauthorized
            : exception.Kind == "conflict" ? ResponseStatus.Conflict
            : ResponseStatus.ValidationFailed;
        var code = exception.Kind == "not_found" ? ErrorCode.NotFound
            : exception.Kind == "forbidden" ? ErrorCode.Unauthorized
            : exception.Kind == "conflict" ? ErrorCode.Conflict
            : ErrorCode.ValidationFailed;
        return Error(exception.Message, status, code);
    }

    private static Dictionary<string, object> SceneMapGenerator0205ReplayPayload(BsonDocument run)
        => new Dictionary<string, object>
        {
            ["runId"] = GetDocString(run, "Id"),
            ["mapId"] = GetDocString(run, "GeneratedSceneMapId"),
            ["previewFingerprint"] = GetDocString(run, "NormalizedHash"),
            ["mapRevision"] = run.TryGetValue("AppliedMapRevision", out var revision) && revision.IsNumeric ? revision.ToInt64() : 0L,
            ["alreadyApplied"] = true,
            ["status"] = "AlreadyApplied"
        };

    private SceneMapGenerator0165Output SceneMap0165OutputFromPreview0205(MapGenerationPreviewHandle0205 handle, string actorUserId)
    {
        var blueprint = new BsonDocument(handle.Blueprint);
        var output = new SceneMapGenerator0165Output
        {
            PresetId = handle.Preview.PresetId,
            TemplateId = handle.Preview.TemplateId,
            DisplayName = GetDocString(blueprint, "DisplayName"),
            Description = GetDocString(blueprint, "Description"),
            LocationKind = GetDocString(blueprint, "LocationKind", "Market"),
            MapScale = GetDocString(blueprint, "MapScale", "Street"),
            WidthMeters = GetDocInt(blueprint, "WidthMeters", 200),
            HeightMeters = GetDocInt(blueprint, "HeightMeters", 200),
            TileSizeMeters = GetDocDouble(blueprint, "TileSizeMeters", 5),
            GridSizeMeters = GetDocDouble(blueprint, "GridSizeMeters", 5),
            Seed = handle.Preview.Seed,
            Density = GetDocString(blueprint, "Density", "Medium"),
            DetailLevel = GetDocString(blueprint, "DetailLevel", "Normal"),
            Symmetry = GetDocString(blueprint, "Symmetry", "None"),
            IncludeGmSecrets = GetDocBool(blueprint, "IncludeGmSecrets"),
            IncludeHazards = GetDocBool(blueprint, "IncludeHazards"),
            IncludeSpawnZones = GetDocBool(blueprint, "IncludeSpawnZones"),
            IncludeObjectiveZones = GetDocBool(blueprint, "IncludeObjectiveZones"),
            CampaignId = GetDocString(blueprint, "CampaignId", "dev-campaign-core"),
            RuleSetId = GetDocString(blueprint, "RuleSetId", "fantasy_nri_default"),
            MapId = handle.Preview.CanonicalMapId,
            CreatedByUserId = actorUserId,
            NormalizedHash = handle.Preview.Fingerprint
        };
        output.TileLayers.AddRange(blueprint.GetValue("TileLayerBlueprints", new BsonArray()).AsBsonArray.OfType<BsonDocument>().Select(item => new BsonDocument(item)));
        output.TilePatches.AddRange(blueprint.GetValue("TilePatchBlueprints", new BsonArray()).AsBsonArray.OfType<BsonDocument>().Select(item => new BsonDocument(item)));
        output.AssetInstances.AddRange(blueprint.GetValue("AssetInstanceBlueprints", new BsonArray()).AsBsonArray.OfType<BsonDocument>().Select(item => new BsonDocument(item)));
        output.Shapes.AddRange(blueprint.GetValue("ShapeBlueprints", new BsonArray()).AsBsonArray.OfType<BsonDocument>().Select(item => new BsonDocument(item)));
        output.Markers.AddRange(blueprint.GetValue("MarkerBlueprints", new BsonArray()).AsBsonArray.OfType<BsonDocument>().Select(item => new BsonDocument(item)));
        return output;
    }

    private long ApplySceneMapGenerator0205Output(SceneMapGenerator0165Output output, MapGenerationApplyGate0205 gate, string actorUserId)
    {
        ValidateSceneMapGenerator0205ApplyPlan(output, gate);
        var mapRevision = gate.PreviewHandle.Preview.MapRevision;
        var layerRevisions = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        var layerIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var fingerprintPart = output.NormalizedHash.Substring(0, Math.Min(12, output.NormalizedHash.Length));
        var operationPrefix = gate.OperationId.Length > 96 ? gate.OperationId.Substring(0, 96) : gate.OperationId;

        foreach (var layer in output.TileLayers)
        {
            var localId = FirstNonEmptyWorld(GetDocString(layer, "Id"), "ground");
            var targetId = SceneMapGenerator0205TargetId(output.MapId, fingerprintPart, localId);
            var result = _mapEditorMutationService.Mutate(new MapEditorMutationRequest0203
            {
                OperationId = operationPrefix + ":layer:" + SceneMapGenerator0205StableLocalId(localId, "ground"),
                MapId = output.MapId,
                Mutation = "layer.create",
                TargetId = targetId,
                ExpectedMapRevision = mapRevision,
                ExpectedLayerRevision = 0,
                ActorUserId = actorUserId,
                Values = new Dictionary<string, object>
                {
                    ["layerType"] = "tile",
                    ["displayName"] = GetDocString(layer, "DisplayName", "Покрытие"),
                    ["layerKind"] = "GeneratedTiles",
                    ["sortOrder"] = GetDocInt(layer, "SortOrder", 10),
                    ["visibility"] = GetDocString(layer, "Visibility", "PlayerVisible"),
                    ["editableKinds"] = "tilePatch"
                }
            });
            mapRevision = result.MapRevision;
            layerIds[localId] = targetId;
            layerRevisions[targetId] = result.LayerRevision;
        }

        var objectLayerId = SceneMapGenerator0205TargetId(output.MapId, fingerprintPart, "objects");
        if (output.AssetInstances.Count + output.Shapes.Count > 0)
        {
            var layerResult = _mapEditorMutationService.Mutate(new MapEditorMutationRequest0203
            {
                OperationId = operationPrefix + ":layer:objects",
                MapId = output.MapId,
                Mutation = "layer.create",
                TargetId = objectLayerId,
                ExpectedMapRevision = mapRevision,
                ExpectedLayerRevision = 0,
                ActorUserId = actorUserId,
                Values = new Dictionary<string, object>
                {
                    ["layerType"] = "object", ["displayName"] = "Объекты генератора", ["layerKind"] = "GeneratedObjects",
                    ["sortOrder"] = 40, ["visibility"] = "PlayerVisible", ["editableKinds"] = "shape,assetInstance"
                }
            });
            mapRevision = layerResult.MapRevision;
            layerRevisions[objectLayerId] = layerResult.LayerRevision;
        }

        foreach (var patch in output.TilePatches)
        {
            var localLayerId = FirstNonEmptyWorld(GetDocString(patch, "TileLayerId"), output.TileLayers.Select(item => GetDocString(item, "Id")).FirstOrDefault());
            var layerId = layerIds.TryGetValue(localLayerId, out var resolvedLayer) ? resolvedLayer : layerIds.Values.First();
            mapRevision = ApplySceneMapGenerator0205Object(output, gate, actorUserId, operationPrefix, fingerprintPart, "tilepatch", patch, layerId, mapRevision, layerRevisions);
        }
        foreach (var asset in output.AssetInstances)
            mapRevision = ApplySceneMapGenerator0205Object(output, gate, actorUserId, operationPrefix, fingerprintPart, "asset", asset, objectLayerId, mapRevision, layerRevisions);
        foreach (var shape in output.Shapes)
            mapRevision = ApplySceneMapGenerator0205Object(output, gate, actorUserId, operationPrefix, fingerprintPart, "shape", shape, objectLayerId, mapRevision, layerRevisions);

        foreach (var marker in output.Markers)
        {
            var localId = FirstNonEmptyWorld(GetDocString(marker, "Id"), Guid.NewGuid().ToString("N"));
            var visibility = GetDocString(marker, "Visibility", "PlayerVisible");
            _repositories.MapMarkers.UpsertAsync(new MapMarkerState
            {
                Id = SceneMapGenerator0205TargetId(output.MapId, fingerprintPart, "marker_" + localId),
                MapId = output.MapId,
                CampaignId = output.CampaignId,
                Name = GetDocString(marker, "DisplayName", "Метка"),
                MarkerType = GetDocString(marker, "MarkerType", MapMarkerTypeIds.Custom),
                X = GetDocDouble(marker, "X", 0), Y = GetDocDouble(marker, "Y", 0),
                IsPlayerVisible = string.Equals(visibility, "PlayerVisible", StringComparison.OrdinalIgnoreCase),
                VisibilityMode = string.Equals(visibility, "PlayerVisible", StringComparison.OrdinalIgnoreCase) ? MapVisibilityModes.Party : MapVisibilityModes.GmOnly,
                PublicNotes = GetDocString(marker, "DescriptionPlayer"), GMNotes = GetDocString(marker, "DescriptionGm"),
                CreatedByUserId = actorUserId, UpdatedByUserId = actorUserId, UpdatedAtUtc = DateTime.UtcNow
            }).GetAwaiter().GetResult();
        }

        return mapRevision;
    }

    private long ApplySceneMapGenerator0205Object(SceneMapGenerator0165Output output, MapGenerationApplyGate0205 gate, string actorUserId,
        string operationPrefix, string fingerprintPart, string kind, BsonDocument source, string layerId, long mapRevision,
        Dictionary<string, long> layerRevisions)
    {
        var localId = FirstNonEmptyWorld(GetDocString(source, "Id"), Guid.NewGuid().ToString("N"));
        var values = SceneMapGenerator0165BlueprintPayload(source);
        values["displayName"] = GetDocString(source, "DisplayName", kind == "tilepatch" ? "Покрытие" : "Объект");
        var result = _mapEditorMutationService.Mutate(new MapEditorMutationRequest0203
        {
            OperationId = operationPrefix + ":" + kind + ":" + SceneMapGenerator0205StableLocalId(localId, "item"),
            MapId = output.MapId,
            Mutation = kind + ".create",
            TargetId = SceneMapGenerator0205TargetId(output.MapId, fingerprintPart, kind + "_" + localId),
            LayerId = layerId,
            ExpectedMapRevision = mapRevision,
            ExpectedLayerRevision = layerRevisions[layerId],
            ExpectedObjectRevision = 0,
            ActorUserId = actorUserId,
            Values = values
        });
        layerRevisions[layerId] = result.LayerRevision;
        return result.MapRevision;
    }

    private static string SceneMapGenerator0205TargetId(string mapId, string fingerprintPart, string localId)
        => mapId + "_generated_" + fingerprintPart + "_" + SceneMapGenerator0205StableLocalId(localId, "item");

    private static void ValidateSceneMapGenerator0205ApplyPlan(SceneMapGenerator0165Output output, MapGenerationApplyGate0205 gate)
    {
        var fingerprintPart = output.NormalizedHash.Substring(0, Math.Min(12, output.NormalizedHash.Length));
        var identities = new List<string>();
        identities.AddRange(output.TileLayers.Select(layer => SceneMapGenerator0205TargetId(output.MapId, fingerprintPart, FirstNonEmptyWorld(GetDocString(layer, "Id"), "ground"))));
        if (output.AssetInstances.Count + output.Shapes.Count > 0)
            identities.Add(SceneMapGenerator0205TargetId(output.MapId, fingerprintPart, "objects"));
        identities.AddRange(output.TilePatches.Select(item => SceneMapGenerator0205TargetId(output.MapId, fingerprintPart, "tilepatch_" + FirstNonEmptyWorld(GetDocString(item, "Id"), "item"))));
        identities.AddRange(output.AssetInstances.Select(item => SceneMapGenerator0205TargetId(output.MapId, fingerprintPart, "asset_" + FirstNonEmptyWorld(GetDocString(item, "Id"), "item"))));
        identities.AddRange(output.Shapes.Select(item => SceneMapGenerator0205TargetId(output.MapId, fingerprintPart, "shape_" + FirstNonEmptyWorld(GetDocString(item, "Id"), "item"))));
        identities.AddRange(output.Markers.Select(item => SceneMapGenerator0205TargetId(output.MapId, fingerprintPart, "marker_" + FirstNonEmptyWorld(GetDocString(item, "Id"), "item"))));
        var duplicate = identities.GroupBy(id => id, StringComparer.OrdinalIgnoreCase).FirstOrDefault(group => group.Count() > 1);
        if (duplicate != null)
            throw new MapGenerationException0205("validation", "Шаблон создаёт повторяющиеся идентификаторы объектов. Исправьте шаблон и повторите предпросмотр.");
    }

    private static string SceneMapGenerator0205StableLocalId(string id, string fallback)
    {
        var source = string.IsNullOrWhiteSpace(id) ? fallback : id.Trim();
        var normalized = new string(source.Select(character => char.IsLetterOrDigit(character) || character is '_' or '-' ? character : '_').ToArray());
        if (normalized.Length <= 56) return normalized;
        using var sha = SHA256.Create();
        var suffix = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(source))).Replace("-", string.Empty).Substring(0, 12).ToLowerInvariant();
        return normalized.Substring(0, 43) + "_" + suffix;
    }

    private static string SceneMapGenerator0205ScaleForBounds(int widthMeters, int heightMeters)
    {
        var largestSide = Math.Max(widthMeters, heightMeters);
        if (largestSide <= 100) return "Interior";
        if (largestSide <= 500) return "Street";
        if (largestSide <= 1000) return "Location";
        return "Area";
    }

    private SceneMapGenerator0165Output BuildSceneMapGenerator0165Output(IDictionary<string, object> payload, string actorUserId, string persistableMapId)
    {
        EnsureSceneMapGenerator0165BuiltIns();
        var presetId = FirstNonEmptyWorld(PayloadReader.GetString(payload, "presetId"), "preset_market_square_0165");
        var preset = SceneMapGenerator0165Presets().Find(ActiveIdFilter(presetId)).FirstOrDefault()
            ?? throw new MapGenerationException0205("validation", "Выбранный пресет не найден или находится в архиве.");
        var templateId = PayloadReader.GetString(payload, "templateId");
        var template = string.IsNullOrWhiteSpace(templateId) ? null : SceneMapGenerator0165Templates().Find(ActiveIdFilter(templateId)).FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(templateId) && template == null)
            throw new MapGenerationException0205("validation", "Выбранный шаблон не найден или находится в архиве.");
        var seed = FirstNonEmptyWorld(PayloadReader.GetString(payload, "seed"), "seed-0165");
        var locationKind = NormalizeSceneMapGenerator0165LocationKind(FirstNonEmptyWorld(PayloadReader.GetString(payload, "locationKind"), GetDocString(preset, "LocationKind")));
        var mapScale = NormalizeSceneMap0162Scale(FirstNonEmptyWorld(PayloadReader.GetString(payload, "mapScale"), template == null ? GetDocString(preset, "MapScale", "Street") : GetDocString(template, "MapScale", "Street")));
        var width = PayloadReader.GetInt(payload, "widthMeters") ?? (template == null ? GetDocInt(preset, "DefaultWidthMeters", 200) : GetDocInt(template, "WidthMeters", 200));
        var height = PayloadReader.GetInt(payload, "heightMeters") ?? (template == null ? GetDocInt(preset, "DefaultHeightMeters", 200) : GetDocInt(template, "HeightMeters", 200));
        var tileSize = PayloadReader.GetDouble(payload, "tileSizeMeters") ?? (template == null ? GetDocDouble(preset, "DefaultTileSizeMeters", 5) : GetDocDouble(template, "TileSizeMeters", 5));
        var gridSize = PayloadReader.GetDouble(payload, "gridSizeMeters") ?? (template == null ? GetDocDouble(preset, "DefaultGridSizeMeters", 5) : GetDocDouble(template, "GridSizeMeters", 5));
        var displayName = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "displayName"), "Сгенерированная локация: " + GetDocString(preset, "DisplayName")), 1, 160, "displayName");
        var density = NormalizeSceneMapGenerator0165Option(PayloadReader.GetString(payload, "density"), "Medium", new[] { "Low", "Medium", "High" });
        var detail = NormalizeSceneMapGenerator0165Option(PayloadReader.GetString(payload, "detailLevel"), "Normal", new[] { "Basic", "Normal", "Rich" });
        var symmetry = NormalizeSceneMapGenerator0165Option(PayloadReader.GetString(payload, "symmetry"), "None", new[] { "None", "Loose", "Structured" });
        var includeGmSecrets = !payload.ContainsKey("includeGmSecrets") || PayloadReader.GetBool(payload, "includeGmSecrets");
        var includeHazards = !payload.ContainsKey("includeHazards") || PayloadReader.GetBool(payload, "includeHazards");
        var includeSpawnZones = !payload.ContainsKey("includeSpawnZones") || PayloadReader.GetBool(payload, "includeSpawnZones");
        var includeObjectiveZones = !payload.ContainsKey("includeObjectiveZones") || PayloadReader.GetBool(payload, "includeObjectiveZones");

        var output = new SceneMapGenerator0165Output
        {
            PresetId = presetId,
            TemplateId = templateId ?? string.Empty,
            DisplayName = displayName,
            Description = $"Сгенерировано из пресета {GetDocString(preset, "DisplayName")} с seed {seed}.",
            LocationKind = locationKind,
            MapScale = mapScale,
            WidthMeters = width,
            HeightMeters = height,
            TileSizeMeters = tileSize,
            GridSizeMeters = gridSize,
            Seed = seed,
            Density = density,
            DetailLevel = detail,
            Symmetry = symmetry,
            IncludeGmSecrets = includeGmSecrets,
            IncludeHazards = includeHazards,
            IncludeSpawnZones = includeSpawnZones,
            IncludeObjectiveZones = includeObjectiveZones,
            CampaignId = FirstNonEmptyWorld(PayloadReader.GetString(payload, "campaignId"), "dev-campaign-core"),
            RuleSetId = FirstNonEmptyWorld(PayloadReader.GetString(payload, "ruleSetId"), "fantasy_nri_default"),
            MapId = FirstNonEmptyWorld(persistableMapId, PayloadReader.GetString(payload, "mapId")),
            CreatedByUserId = actorUserId
        };
        if (string.IsNullOrWhiteSpace(output.MapId))
            output.MapId = "scene_map_generated_" + Guid.NewGuid().ToString("N");

        if (template != null && template.TryGetValue("TilePatchBlueprints", out var templatePatches) && templatePatches.IsBsonArray && templatePatches.AsBsonArray.Count > 0)
        {
            ApplyTemplateBlueprint(output, template);
        }
        else
        {
            GeneratePresetBlueprint(output, preset);
        }
        output.NormalizedHash = ComputeSceneMapGenerator0165Hash(output);
        return output;
    }

    private void GeneratePresetBlueprint(SceneMapGenerator0165Output output, BsonDocument preset)
    {
        var rng = new Random(StableSceneMapGenerator0165Seed(output.PresetId + "|" + output.Seed + "|" + output.WidthMeters + "x" + output.HeightMeters + "|" + output.Density + "|" + output.DetailLevel + "|" + output.Symmetry));
        output.TileLayers.Add(new BsonDocument
        {
            ["Id"] = "tile_layer_ground",
            ["DisplayName"] = "Покрытия",
            ["TileSizeMeters"] = output.TileSizeMeters,
            ["SortOrder"] = 10,
            ["Visibility"] = "PlayerVisible"
        });

        var kind = output.LocationKind;
        AddTile(output, "ground", MaterialForKind(kind), 0, 0, output.WidthMeters, output.HeightMeters, 1);
        if (kind == "Market")
        {
            AddTile(output, "square", "market_square_cobble", output.WidthMeters * 0.18, output.HeightMeters * 0.18, output.WidthMeters * 0.64, output.HeightMeters * 0.52, 5);
            AddTile(output, "road_h", "road_dirt", 0, output.HeightMeters * 0.48, output.WidthMeters, output.HeightMeters * 0.14, 8);
            AddTile(output, "road_v", "cobblestone", output.WidthMeters * 0.48, 0, output.WidthMeters * 0.14, output.HeightMeters, 9);
            AddTile(output, "north_lane", "stone_tiles", output.WidthMeters * 0.08, output.HeightMeters * 0.12, output.WidthMeters * 0.84, output.HeightMeters * 0.08, 10);
            AddTile(output, "south_lane", "dirt", output.WidthMeters * 0.08, output.HeightMeters * 0.72, output.WidthMeters * 0.84, output.HeightMeters * 0.08, 11);
            AddTile(output, "stall_row_w", "wood_planks", output.WidthMeters * 0.12, output.HeightMeters * 0.24, output.WidthMeters * 0.16, output.HeightMeters * 0.36, 12);
            AddTile(output, "stall_row_e", "wood_planks", output.WidthMeters * 0.72, output.HeightMeters * 0.24, output.WidthMeters * 0.16, output.HeightMeters * 0.36, 13);
            AddAssetsGrid(output, rng, "market_stall", "market_stall", "Рыночная лавка", 6, 22, 22);
            AddAssetsGrid(output, rng, "crate", "crate", "Ящик", 4, 8, 8);
            AddAsset(output, "tavern", "bar_counter", "Трактир", output.WidthMeters * 0.68, output.HeightMeters * 0.15, 26, 18, "PlayerVisible");
            AddAsset(output, "shop", "counter", "Лавка торговца", output.WidthMeters * 0.18, output.HeightMeters * 0.16, 22, 18, "PlayerVisible");
            AddMarker(output, "entrance_w", "Западный вход", "Entrance", 5, output.HeightMeters * 0.55, "PlayerVisible");
            AddMarker(output, "exit_e", "Восточный выход", "Exit", output.WidthMeters - 8, output.HeightMeters * 0.55, "PlayerVisible");
            AddMarker(output, "poi_market", "Центр рынка", "PointOfInterest", output.WidthMeters * 0.5, output.HeightMeters * 0.42, "PlayerVisible");
        }
        else if (kind == "Shop")
        {
            AddTile(output, "floor", "shop_floor", 2, 2, output.WidthMeters - 4, output.HeightMeters - 4, 5);
            AddShape(output, "walls", "Границы комнаты", "Rectangle", "Wall", 0, 0, output.WidthMeters, output.HeightMeters, "PlayerVisible", false);
            AddAsset(output, "door", "door", "Входная дверь", output.WidthMeters * 0.45, output.HeightMeters - 4, 6, 4, "PlayerVisible");
            AddAsset(output, "counter", "counter", "Прилавок", output.WidthMeters * 0.55, output.HeightMeters * 0.35, 12, 4, "PlayerVisible");
            AddAssetsLine(output, "shelf", "shelf", "Полка", 5, 4, 4, output.WidthMeters * 0.12, output.HeightMeters * 0.14);
            AddMarker(output, "entrance", "Вход", "Entrance", output.WidthMeters * 0.5, output.HeightMeters - 2, "PlayerVisible");
            AddMarker(output, "customer", "Позиция покупателя", "PointOfInterest", output.WidthMeters * 0.42, output.HeightMeters * 0.58, "PlayerVisible");
        }
        else if (kind == "Tavern")
        {
            AddTile(output, "floor", "tavern_floor", 2, 2, output.WidthMeters - 4, output.HeightMeters - 4, 5);
            AddAsset(output, "bar", "bar_counter", "Барная стойка", output.WidthMeters * 0.62, output.HeightMeters * 0.18, 18, 5, "PlayerVisible");
            AddAsset(output, "hearth", "hearth", "Очаг", output.WidthMeters * 0.12, output.HeightMeters * 0.18, 8, 8, "PlayerVisible");
            AddAssetsGrid(output, rng, "table", "table", "Стол", 5, 8, 8);
            AddAssetsGrid(output, rng, "chair_or_bench", "chair_or_bench", "Скамья", 7, 5, 4);
            AddShape(output, "storage", "Кладовая", "Rectangle", "StorageArea", output.WidthMeters * 0.72, output.HeightMeters * 0.68, output.WidthMeters * 0.2, output.HeightMeters * 0.2, "PlayerVisible", false);
            AddMarker(output, "entrance", "Вход", "Entrance", output.WidthMeters * 0.5, output.HeightMeters - 2, "PlayerVisible");
            AddMarker(output, "poi_bar", "Бар", "PointOfInterest", output.WidthMeters * 0.7, output.HeightMeters * 0.22, "PlayerVisible");
        }
        else if (kind == "Alley")
        {
            AddTile(output, "alley", "alley_stone", output.WidthMeters * 0.18, 0, output.WidthMeters * 0.42, output.HeightMeters, 5);
            AddTile(output, "side", "stone_tiles", 0, 0, output.WidthMeters * 0.18, output.HeightMeters, 4);
            AddTile(output, "side2", "stone_tiles", output.WidthMeters * 0.6, 0, output.WidthMeters * 0.4, output.HeightMeters, 4);
            AddAssetsGrid(output, rng, "crate", "crate", "Ящик", 5, 8, 8);
            AddAssetsGrid(output, rng, "barrel", "barrel", "Бочка", 4, 6, 6);
            AddMarker(output, "entrance", "Вход в переулок", "Entrance", output.WidthMeters * 0.38, 4, "PlayerVisible");
            AddMarker(output, "exit", "Выход из переулка", "Exit", output.WidthMeters * 0.38, output.HeightMeters - 5, "PlayerVisible");
            if (output.IncludeGmSecrets)
                AddShape(output, "secret", "Скрытый проход", "Line", "GmNote", output.WidthMeters * 0.62, output.HeightMeters * 0.55, 18, 3, "GmOnly", true);
            if (output.IncludeHazards)
                AddShape(output, "ambush", "Зона засады", "Rectangle", "HazardZone", output.WidthMeters * 0.2, output.HeightMeters * 0.42, output.WidthMeters * 0.25, output.HeightMeters * 0.16, "Hidden", true);
        }
        else if (kind == "Road" || kind == "Bridge")
        {
            AddTile(output, "road", kind == "Bridge" ? "bridge_wood" : "road_dirt", 0, output.HeightMeters * 0.42, output.WidthMeters, output.HeightMeters * 0.16, 7);
            if (kind == "Bridge")
                AddTile(output, "water", "shallow_water", 0, output.HeightMeters * 0.28, output.WidthMeters, output.HeightMeters * 0.44, 3);
            AddAssetsGrid(output, rng, "tree", "tree", "Дерево", 6, 10, 10);
            AddAssetsGrid(output, rng, "rock", "rock", "Камень", 4, 8, 6);
            AddAssetsGrid(output, rng, "log", "log", "Бревно", 3, 10, 5);
            AddMarker(output, "start", "Начало пути", "Entrance", 5, output.HeightMeters * 0.5, "PlayerVisible");
            AddMarker(output, "exit", "Выход", "Exit", output.WidthMeters - 8, output.HeightMeters * 0.5, "PlayerVisible");
        }
        else if (kind == "Camp")
        {
            AddTile(output, "paths", "dirt", output.WidthMeters * 0.25, output.HeightMeters * 0.45, output.WidthMeters * 0.5, output.HeightMeters * 0.12, 5);
            AddAsset(output, "fire", "campfire", "Костёр", output.WidthMeters * 0.47, output.HeightMeters * 0.48, 8, 8, "PlayerVisible");
            AddAssetsGrid(output, rng, "tent", "tent", "Палатка", 5, 14, 12);
            AddAssetsGrid(output, rng, "crate", "crate", "Припасы", 4, 8, 8);
            AddMarker(output, "watch", "Дозорная точка", "PointOfInterest", output.WidthMeters * 0.78, output.HeightMeters * 0.18, "PlayerVisible");
            AddMarker(output, "entrance", "Тропа к лагерю", "Entrance", output.WidthMeters * 0.1, output.HeightMeters * 0.55, "PlayerVisible");
            if (output.IncludeGmSecrets)
                AddAsset(output, "stash", "crate", "Скрытый тайник", output.WidthMeters * 0.72, output.HeightMeters * 0.7, 8, 8, "GmOnly", SceneMap0165GmLeakToken);
        }
        else
        {
            AddTile(output, "stone", "stone", output.WidthMeters * 0.1, output.HeightMeters * 0.1, output.WidthMeters * 0.8, output.HeightMeters * 0.65, 4);
            AddAssetsGrid(output, rng, "rock", "rock", "Обломок", 6, 8, 8);
            AddAssetsGrid(output, rng, "cover_low", "cover_low", "Низкое укрытие", 4, 10, 6);
            AddMarker(output, "entrance", "Вход", "Entrance", output.WidthMeters * 0.12, output.HeightMeters * 0.82, "PlayerVisible");
            AddMarker(output, "exit", "Выход", "Exit", output.WidthMeters * 0.86, output.HeightMeters * 0.18, "PlayerVisible");
            if (output.IncludeObjectiveZones)
                AddShape(output, "objective", "Цель", "Rectangle", "ObjectiveZone", output.WidthMeters * 0.45, output.HeightMeters * 0.38, output.WidthMeters * 0.18, output.HeightMeters * 0.14, "PlayerVisible", false);
            if (output.IncludeHazards)
                AddShape(output, "hazard", "Опасная зона", "Rectangle", "HazardZone", output.WidthMeters * 0.22, output.HeightMeters * 0.28, output.WidthMeters * 0.18, output.HeightMeters * 0.12, "Hidden", true);
        }

        if (output.IncludeSpawnZones)
            AddShape(output, "spawn", "Стартовая зона", "Rectangle", "SpawnZone", output.WidthMeters * 0.04, output.HeightMeters * 0.72, Math.Max(8, output.WidthMeters * 0.12), Math.Max(8, output.HeightMeters * 0.12), "PlayerVisible", false);
        if (output.IncludeObjectiveZones && output.Markers.All(x => GetDocString(x, "MarkerType") != "Objective"))
            AddMarker(output, "objective_marker", "Цель сцены", "Objective", output.WidthMeters * 0.55, output.HeightMeters * 0.45, "PlayerVisible");
        EnsurePresetVisualQuality0165(output, rng);
        EnsureMinimumSceneMapGenerator0165Density(output, rng);
    }

    private void EnsurePresetVisualQuality0165(SceneMapGenerator0165Output output, Random rng)
    {
        if (output.LocationKind == "Market")
        {
            AddAssetsGrid(output, rng, "quality_market_cart", "cart", "Торговая телега", 3, 14, 8);
            AddAssetsGrid(output, rng, "quality_market_sign", "signboard", "Вывеска", 3, 6, 6);
            AddAsset(output, "quality_market_well", "well", "Колодец", output.WidthMeters * 0.44, output.HeightMeters * 0.38, 10, 10, "PlayerVisible");
            AddAsset(output, "quality_market_lantern_n", "lantern", "Фонарь", output.WidthMeters * 0.35, output.HeightMeters * 0.28, 5, 5, "PlayerVisible");
            AddAsset(output, "quality_market_lantern_s", "lantern", "Фонарь", output.WidthMeters * 0.64, output.HeightMeters * 0.66, 5, 5, "PlayerVisible");
            AddAsset(output, "quality_market_table", "table", "Стол торговца", output.WidthMeters * 0.52, output.HeightMeters * 0.6, 10, 8, "PlayerVisible");
        }
        else if (output.LocationKind == "Shop")
        {
            AddTile(output, "quality_shop_storage_floor", "wood_planks", output.WidthMeters * 0.08, output.HeightMeters * 0.08, output.WidthMeters * 0.32, output.HeightMeters * 0.28, 6);
            AddTile(output, "quality_shop_entry_mat", "stone_tiles", output.WidthMeters * 0.38, output.HeightMeters * 0.78, output.WidthMeters * 0.24, output.HeightMeters * 0.14, 7);
            AddAsset(output, "quality_shop_storage_counter", "counter", "Стол выдачи", output.WidthMeters * 0.18, output.HeightMeters * 0.48, 9, 4, "PlayerVisible");
            AddAssetsGrid(output, rng, "quality_shop_crate", "crate", "Товарный ящик", 3, 5, 5);
            AddAssetsGrid(output, rng, "quality_shop_barrel", "barrel", "Бочонок", 2, 4, 4);
            AddAsset(output, "quality_shop_sign", "signboard", "Вывеска магазина", output.WidthMeters * 0.12, output.HeightMeters * 0.72, 6, 4, "PlayerVisible");
        }
        else if (output.LocationKind == "Tavern")
        {
            AddTile(output, "quality_tavern_bar_floor", "wood_planks", output.WidthMeters * 0.55, output.HeightMeters * 0.08, output.WidthMeters * 0.36, output.HeightMeters * 0.22, 6);
            AddTile(output, "quality_tavern_hearth_stone", "stone_tiles", output.WidthMeters * 0.06, output.HeightMeters * 0.1, output.WidthMeters * 0.2, output.HeightMeters * 0.22, 7);
            AddAsset(output, "quality_tavern_door", "door", "Входная дверь", output.WidthMeters * 0.48, output.HeightMeters - 4, 6, 4, "PlayerVisible");
            AddAssetsGrid(output, rng, "quality_tavern_barrel", "barrel", "Бочонок", 2, 4, 4);
            AddAsset(output, "quality_tavern_notice", "signboard", "Доска объявлений", output.WidthMeters * 0.2, output.HeightMeters * 0.74, 7, 5, "PlayerVisible");
        }
        else if (output.LocationKind == "Alley")
        {
            AddTile(output, "quality_alley_crossing", "cobblestone", output.WidthMeters * 0.18, output.HeightMeters * 0.42, output.WidthMeters * 0.42, output.HeightMeters * 0.12, 6);
            AddTile(output, "quality_alley_drain", "mud", output.WidthMeters * 0.38, 0, output.WidthMeters * 0.06, output.HeightMeters, 7);
            AddAsset(output, "quality_alley_door", "door", "Черный вход", output.WidthMeters * 0.62, output.HeightMeters * 0.18, 6, 8, "PlayerVisible");
            AddAsset(output, "quality_alley_sign", "signboard", "Старая вывеска", output.WidthMeters * 0.12, output.HeightMeters * 0.32, 6, 6, "PlayerVisible");
            AddAssetsGrid(output, rng, "quality_alley_window", "window", "Окно", 2, 5, 5);
        }
        else if (output.LocationKind == "Camp")
        {
            AddTile(output, "quality_camp_clearing", "grass", output.WidthMeters * 0.12, output.HeightMeters * 0.12, output.WidthMeters * 0.76, output.HeightMeters * 0.62, 4);
            AddTile(output, "quality_camp_mud_track", "mud", output.WidthMeters * 0.08, output.HeightMeters * 0.68, output.WidthMeters * 0.5, output.HeightMeters * 0.1, 6);
            AddTile(output, "quality_camp_north_woods", "forest_floor", 0, 0, output.WidthMeters, output.HeightMeters * 0.16, 7);
            AddTile(output, "quality_camp_rocky_edge", "stone", output.WidthMeters * 0.74, output.HeightMeters * 0.62, output.WidthMeters * 0.2, output.HeightMeters * 0.18, 8);
            AddAssetsGrid(output, rng, "quality_camp_log", "log", "Бревно", 3, 10, 5);
            AddAssetsGrid(output, rng, "quality_camp_barrel", "barrel", "Бочка", 2, 6, 6);
            AddAsset(output, "quality_camp_watch_sign", "signboard", "Дозорный знак", output.WidthMeters * 0.78, output.HeightMeters * 0.24, 6, 6, "PlayerVisible");
            if (output.Markers.All(x => GetDocString(x, "MarkerType") != "Exit"))
                AddMarker(output, "quality_camp_exit", "Выход к лесу", "Exit", output.WidthMeters * 0.86, output.HeightMeters * 0.18, "PlayerVisible");
        }
    }

    private void ApplyTemplateBlueprint(SceneMapGenerator0165Output output, BsonDocument template)
    {
        output.TileLayers.AddRange(template.GetValue("TileLayerBlueprints", new BsonArray()).AsBsonArray.OfType<BsonDocument>().Select(x => new BsonDocument(x)));
        output.TilePatches.AddRange(template.GetValue("TilePatchBlueprints", new BsonArray()).AsBsonArray.OfType<BsonDocument>().Select(x => new BsonDocument(x)));
        output.AssetInstances.AddRange(template.GetValue("AssetInstanceBlueprints", new BsonArray()).AsBsonArray.OfType<BsonDocument>().Select(x => new BsonDocument(x)));
        output.Shapes.AddRange(template.GetValue("ShapeBlueprints", new BsonArray()).AsBsonArray.OfType<BsonDocument>().Select(x => new BsonDocument(x)));
        output.Markers.AddRange(template.GetValue("MarkerBlueprints", new BsonArray()).AsBsonArray.OfType<BsonDocument>().Select(x => new BsonDocument(x)));
        if (output.TileLayers.Count == 0)
            output.TileLayers.Add(new BsonDocument { ["Id"] = "tile_layer_ground", ["DisplayName"] = "Покрытия", ["TileSizeMeters"] = output.TileSizeMeters, ["SortOrder"] = 10, ["Visibility"] = "PlayerVisible" });
    }

    private ResponseEnvelope? ValidateSceneMapGenerator0165Output(SceneMapGenerator0165Output output)
    {
        var validation = ValidateSceneMap0162Settings(output.WidthMeters, output.HeightMeters, (int)Math.Round(output.GridSizeMeters), output.MapScale);
        if (validation != null) return validation;
        if (output.TileSizeMeters <= 0 || output.GridSizeMeters <= 0)
            return Error("tile and grid size must be positive", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);
        if (output.TileLayers.Count == 0 || output.TilePatches.Count == 0)
            return Error("generated map must contain tile layers and tile patches", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);
        foreach (var patch in output.TilePatches)
        {
            var validationPatch = ValidateSceneMap0164TilePatch(SceneMapGenerator0165MapDoc(output), SceneMapGenerator0165PatchDoc(output, patch, DateTime.UtcNow, output.CreatedByUserId));
            if (validationPatch != null) return validationPatch;
        }
        foreach (var asset in output.AssetInstances)
        {
            var validationAsset = ValidateSceneMap0164AssetInstance(SceneMapGenerator0165MapDoc(output), SceneMapGenerator0165AssetDoc(output, asset, DateTime.UtcNow, output.CreatedByUserId));
            if (validationAsset != null) return validationAsset;
        }
        return null;
    }

    private Dictionary<string, object> SceneMapGenerator0165OutputPayload(SceneMapGenerator0165Output output, BsonDocument run, string generatedSceneMapId = "", bool active = false)
    {
        return new Dictionary<string, object>
        {
            ["runId"] = GetDocString(run, "Id"),
            ["generationRunId"] = GetDocString(run, "Id"),
            ["status"] = GetDocString(run, "Status"),
            ["mapId"] = FirstNonEmptyWorld(generatedSceneMapId, output.MapId),
            ["generatedSceneMapId"] = generatedSceneMapId,
            ["isActive"] = active,
            ["normalizedHash"] = output.NormalizedHash,
            ["presetId"] = output.PresetId,
            ["templateId"] = output.TemplateId,
            ["seed"] = output.Seed,
            ["map"] = SceneMapGenerator0165MapPayload(output),
            ["tileLayers"] = output.TileLayers.Select(SceneMapGenerator0165BlueprintPayload).Cast<object>().ToArray(),
            ["tilePatches"] = output.TilePatches.Select(SceneMapGenerator0165BlueprintPayload).Cast<object>().ToArray(),
            ["assetInstances"] = output.AssetInstances.Select(SceneMapGenerator0165BlueprintPayload).Cast<object>().ToArray(),
            ["shapes"] = output.Shapes.Select(SceneMapGenerator0165BlueprintPayload).Cast<object>().ToArray(),
            ["markers"] = output.Markers.Select(SceneMapGenerator0165BlueprintPayload).Cast<object>().ToArray(),
            ["summary"] = SceneMapGenerator0165SummaryPayload(output),
            ["sourceCollections"] = new object[] { SceneMap0162DefinitionsCollection, SceneMap0164TileLayersCollection, SceneMap0164TilePatchesCollection, SceneMap0164AssetInstancesCollection, SceneMap0164ShapesCollection, SceneMap0162MarkersCollection, SceneMap0165RunsCollection }
        };
    }

    private Dictionary<string, object> SceneMapGenerator0165MapPayload(SceneMapGenerator0165Output output)
        => new Dictionary<string, object>
        {
            ["mapId"] = output.MapId,
            ["id"] = output.MapId,
            ["displayName"] = output.DisplayName,
            ["name"] = output.DisplayName,
            ["description"] = output.Description,
            ["locationKind"] = output.LocationKind,
            ["mapScale"] = output.MapScale,
            ["widthMeters"] = output.WidthMeters,
            ["heightMeters"] = output.HeightMeters,
            ["tileSizeMeters"] = output.TileSizeMeters,
            ["defaultTileSizeMeters"] = output.TileSizeMeters,
            ["gridSizeMeters"] = output.GridSizeMeters,
            ["recommendedGridSizeMeters"] = output.GridSizeMeters,
            ["showGrid"] = true,
            ["showCoordinates"] = true
        };

    private Dictionary<string, object> SceneMapGenerator0165SummaryPayload(SceneMapGenerator0165Output output)
        => new Dictionary<string, object>
        {
            ["tileLayerCount"] = output.TileLayers.Count,
            ["tilePatchCount"] = output.TilePatches.Count,
            ["assetInstanceCount"] = output.AssetInstances.Count,
            ["shapeCount"] = output.Shapes.Count,
            ["markerCount"] = output.Markers.Count,
            ["gmOnlyCount"] = output.AssetInstances.Concat(output.Shapes).Concat(output.Markers).Count(x => string.Equals(GetDocString(x, "Visibility"), "GmOnly", StringComparison.OrdinalIgnoreCase)),
            ["hiddenCount"] = output.AssetInstances.Concat(output.Shapes).Concat(output.Markers).Count(x => string.Equals(GetDocString(x, "Visibility"), "Hidden", StringComparison.OrdinalIgnoreCase)),
            ["seed"] = output.Seed,
            ["mapSize"] = $"{output.WidthMeters}x{output.HeightMeters}",
            ["normalizedHash"] = output.NormalizedHash
        };

    private static Dictionary<string, object> SceneMapGenerator0165BlueprintPayload(BsonDocument doc)
    {
        return doc.Elements.ToDictionary(x => ToCamel(x.Name), x => SceneMapGenerator0165PayloadValue(x.Value));
    }

    private Dictionary<string, object> SceneMapGenerator0165PresetPayload(BsonDocument doc)
        => new Dictionary<string, object>
        {
            ["presetId"] = GetDocString(doc, "Id"),
            ["id"] = GetDocString(doc, "Id"),
            ["displayName"] = GetDocString(doc, "DisplayName"),
            ["name"] = GetDocString(doc, "DisplayName"),
            ["description"] = GetDocString(doc, "Description"),
            ["locationKind"] = GetDocString(doc, "LocationKind"),
            ["mapScale"] = GetDocString(doc, "MapScale"),
            ["defaultWidthMeters"] = GetDocInt(doc, "DefaultWidthMeters", 100),
            ["defaultHeightMeters"] = GetDocInt(doc, "DefaultHeightMeters", 100),
            ["defaultTileSizeMeters"] = GetDocDouble(doc, "DefaultTileSizeMeters", 5),
            ["defaultGridSizeMeters"] = GetDocDouble(doc, "DefaultGridSizeMeters", 5),
            ["allowedMaterials"] = BsonArrayToStrings0165(doc, "AllowedMaterials"),
            ["allowedAssets"] = BsonArrayToStrings0165(doc, "AllowedAssets"),
            ["requiredZones"] = BsonArrayToStrings0165(doc, "RequiredZones"),
            ["isBuiltIn"] = GetDocBool(doc, "IsBuiltIn"),
            ["isArchived"] = GetDocBool(doc, "IsArchived")
        };

    private Dictionary<string, object> SceneMapGenerator0165TemplatePayload(BsonDocument doc, bool includeBlueprints)
    {
        var payload = new Dictionary<string, object>
        {
            ["templateId"] = GetDocString(doc, "Id"),
            ["id"] = GetDocString(doc, "Id"),
            ["displayName"] = GetDocString(doc, "DisplayName"),
            ["name"] = GetDocString(doc, "DisplayName"),
            ["description"] = GetDocString(doc, "Description"),
            ["locationKind"] = GetDocString(doc, "LocationKind"),
            ["mapScale"] = GetDocString(doc, "MapScale"),
            ["widthMeters"] = GetDocInt(doc, "WidthMeters", 100),
            ["heightMeters"] = GetDocInt(doc, "HeightMeters", 100),
            ["tileSizeMeters"] = GetDocDouble(doc, "TileSizeMeters", 5),
            ["gridSizeMeters"] = GetDocDouble(doc, "GridSizeMeters", 5),
            ["templateSourceMapId"] = GetDocString(doc, "TemplateSourceMapId"),
            ["tilePatchCount"] = doc.GetValue("TilePatchBlueprints", new BsonArray()).AsBsonArray.Count,
            ["assetInstanceCount"] = doc.GetValue("AssetInstanceBlueprints", new BsonArray()).AsBsonArray.Count,
            ["shapeCount"] = doc.GetValue("ShapeBlueprints", new BsonArray()).AsBsonArray.Count,
            ["markerCount"] = doc.GetValue("MarkerBlueprints", new BsonArray()).AsBsonArray.Count,
            ["isBuiltIn"] = GetDocBool(doc, "IsBuiltIn"),
            ["isArchived"] = GetDocBool(doc, "IsArchived")
        };
        if (includeBlueprints)
        {
            payload["tileLayerBlueprints"] = doc.GetValue("TileLayerBlueprints", new BsonArray()).AsBsonArray.OfType<BsonDocument>().Select(SceneMapGenerator0165BlueprintPayload).Cast<object>().ToArray();
            payload["tilePatchBlueprints"] = doc.GetValue("TilePatchBlueprints", new BsonArray()).AsBsonArray.OfType<BsonDocument>().Select(SceneMapGenerator0165BlueprintPayload).Cast<object>().ToArray();
            payload["assetInstanceBlueprints"] = doc.GetValue("AssetInstanceBlueprints", new BsonArray()).AsBsonArray.OfType<BsonDocument>().Select(SceneMapGenerator0165BlueprintPayload).Cast<object>().ToArray();
        }
        return payload;
    }

    private Dictionary<string, object> SceneMapGenerator0165RunPayload(BsonDocument doc)
        => new Dictionary<string, object>
        {
            ["runId"] = GetDocString(doc, "Id"),
            ["presetId"] = GetDocString(doc, "PresetId"),
            ["templateId"] = GetDocString(doc, "TemplateId"),
            ["generatedSceneMapId"] = GetDocString(doc, "GeneratedSceneMapId"),
            ["displayName"] = GetDocString(doc, "DisplayName"),
            ["seed"] = GetDocString(doc, "Seed"),
            ["status"] = GetDocString(doc, "Status"),
            ["normalizedHash"] = GetDocString(doc, "NormalizedHash"),
            ["createdAtUtc"] = GetDocDate(doc, "CreatedAtUtc"),
            ["summary"] = doc.TryGetValue("ResultSummary", out var summary) && summary.IsBsonDocument ? SceneMapGenerator0165BlueprintPayload(summary.AsBsonDocument) : new Dictionary<string, object>()
        };

    private BsonDocument BuildSceneMapGenerator0165PresetDoc(IDictionary<string, object> payload, string actorUserId, DateTime now, BsonDocument? existing)
    {
        var presetId = FirstNonEmptyWorld(PayloadReader.GetString(payload, "presetId"), PayloadReader.GetString(payload, "id"), existing == null ? "preset_" + Guid.NewGuid().ToString("N") : GetDocString(existing, "Id"));
        var locationKind = NormalizeSceneMapGenerator0165LocationKind(FirstNonEmptyWorld(PayloadReader.GetString(payload, "locationKind"), existing == null ? "Custom" : GetDocString(existing, "LocationKind")));
        var scale = NormalizeSceneMap0162Scale(FirstNonEmptyWorld(PayloadReader.GetString(payload, "mapScale"), existing == null ? "Location" : GetDocString(existing, "MapScale")));
        return new BsonDocument
        {
            ["_id"] = presetId,
            ["Id"] = presetId,
            ["DisplayName"] = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "displayName"), PayloadReader.GetString(payload, "name"), existing == null ? "Новый пресет" : GetDocString(existing, "DisplayName")), 1, 160, "displayName"),
            ["Description"] = RequireLength(FirstNonEmptyWorld(PayloadReader.GetString(payload, "description"), existing == null ? string.Empty : GetDocString(existing, "Description")), 0, 4096, "description"),
            ["LocationKind"] = locationKind,
            ["MapScale"] = scale,
            ["DefaultWidthMeters"] = PayloadReader.GetInt(payload, "defaultWidthMeters") ?? (existing == null ? 100 : GetDocInt(existing, "DefaultWidthMeters", 100)),
            ["DefaultHeightMeters"] = PayloadReader.GetInt(payload, "defaultHeightMeters") ?? (existing == null ? 100 : GetDocInt(existing, "DefaultHeightMeters", 100)),
            ["DefaultTileSizeMeters"] = PayloadReader.GetDouble(payload, "defaultTileSizeMeters") ?? (existing == null ? 5d : GetDocDouble(existing, "DefaultTileSizeMeters", 5)),
            ["DefaultGridSizeMeters"] = PayloadReader.GetDouble(payload, "defaultGridSizeMeters") ?? (existing == null ? 5d : GetDocDouble(existing, "DefaultGridSizeMeters", 5)),
            ["AllowedMaterials"] = existing != null && existing.TryGetValue("AllowedMaterials", out var materials) && materials.IsBsonArray ? materials.AsBsonArray : new BsonArray(new[] { "grass", "stone", "cobblestone" }),
            ["AllowedAssets"] = existing != null && existing.TryGetValue("AllowedAssets", out var assets) && assets.IsBsonArray ? assets.AsBsonArray : new BsonArray(new[] { "crate", "barrel", "market_stall" }),
            ["RequiredZones"] = existing != null && existing.TryGetValue("RequiredZones", out var zones) && zones.IsBsonArray ? zones.AsBsonArray : new BsonArray(new[] { "entrance" }),
            ["GenerationRules"] = existing != null && existing.TryGetValue("GenerationRules", out var rules) && rules.IsBsonDocument ? rules.AsBsonDocument : new BsonDocument(),
            ["SortOrder"] = PayloadReader.GetInt(payload, "sortOrder") ?? (existing == null ? 100 : GetDocInt(existing, "SortOrder", 100)),
            ["IsBuiltIn"] = existing != null && GetDocBool(existing, "IsBuiltIn"),
            ["IsArchived"] = false,
            ["CreatedAtUtc"] = existing != null ? GetDocDate(existing, "CreatedAtUtc") : now,
            ["UpdatedAtUtc"] = now,
            ["CreatedByUserId"] = existing != null ? GetDocString(existing, "CreatedByUserId") : actorUserId,
            ["UpdatedByUserId"] = actorUserId
        };
    }

    private BsonDocument SceneMapGenerator0165MapDoc(SceneMapGenerator0165Output output)
        => new BsonDocument
        {
            ["_id"] = output.MapId,
            ["Id"] = output.MapId,
            ["WorldId"] = SceneMap0162DefaultWorldId,
            ["CampaignId"] = output.CampaignId,
            ["RuleSetId"] = output.RuleSetId,
            ["DisplayName"] = output.DisplayName,
            ["Description"] = output.Description,
            ["WidthMeters"] = output.WidthMeters,
            ["HeightMeters"] = output.HeightMeters,
            ["GridSizeMeters"] = (int)Math.Round(output.GridSizeMeters),
            ["MapScale"] = output.MapScale,
            ["DefaultTileSizeMeters"] = output.TileSizeMeters,
            ["RecommendedGridSizeMeters"] = output.GridSizeMeters,
            ["BackgroundMode"] = "TileAssetGenerated",
            ["BackgroundColor"] = "#111827",
            ["ShowGrid"] = true,
            ["ShowCoordinates"] = true,
            ["GeneratorPresetId"] = output.PresetId,
            ["GeneratorTemplateId"] = output.TemplateId,
            ["GeneratorSeed"] = output.Seed,
            ["GeneratorHash"] = output.NormalizedHash,
            ["SchemaVersion"] = 2,
            ["IsArchived"] = false
        };

    private BsonDocument SceneMapGenerator0165TileLayerDoc(SceneMapGenerator0165Output output, BsonDocument layer, DateTime now, string actorUserId)
    {
        var id = SceneMapGenerator0165ScopedId(output.MapId, GetDocString(layer, "Id"));
        return new BsonDocument
        {
            ["_id"] = id,
            ["Id"] = id,
            ["SceneMapId"] = output.MapId,
            ["DisplayName"] = GetDocString(layer, "DisplayName", "Покрытия"),
            ["TileSizeMeters"] = GetDocDouble(layer, "TileSizeMeters", output.TileSizeMeters),
            ["SortOrder"] = GetDocInt(layer, "SortOrder", 10),
            ["IsVisibleByDefault"] = true,
            ["Visibility"] = GetDocString(layer, "Visibility", "PlayerVisible"),
            ["IsArchived"] = false,
            ["CreatedAtUtc"] = now,
            ["UpdatedAtUtc"] = now,
            ["CreatedByUserId"] = actorUserId,
            ["UpdatedByUserId"] = actorUserId
        };
    }

    private BsonDocument SceneMapGenerator0165PatchDoc(SceneMapGenerator0165Output output, BsonDocument patch, DateTime now, string actorUserId)
    {
        var id = SceneMapGenerator0165ScopedId(output.MapId, GetDocString(patch, "Id"));
        var layerId = SceneMapGenerator0165ScopedId(output.MapId, FirstNonEmptyWorld(GetDocString(patch, "TileLayerId"), "tile_layer_ground"));
        var material = GetDocString(patch, "MaterialKey", "grass");
        return new BsonDocument
        {
            ["_id"] = id,
            ["Id"] = id,
            ["SceneMapId"] = output.MapId,
            ["TileLayerId"] = layerId,
            ["MaterialKey"] = material,
            ["TextureKey"] = GetDocString(patch, "TextureKey", DefaultSceneMap0164TextureForMaterial(material)),
            ["X"] = GetDocDouble(patch, "X", 0),
            ["Y"] = GetDocDouble(patch, "Y", 0),
            ["Width"] = GetDocDouble(patch, "Width", output.WidthMeters),
            ["Height"] = GetDocDouble(patch, "Height", output.HeightMeters),
            ["RotationDegrees"] = GetDocDouble(patch, "RotationDegrees", 0),
            ["Opacity"] = GetDocDouble(patch, "Opacity", 1),
            ["SortOrder"] = GetDocInt(patch, "SortOrder", 10),
            ["Visibility"] = GetDocString(patch, "Visibility", "PlayerVisible"),
            ["IsArchived"] = false,
            ["CreatedAtUtc"] = now,
            ["UpdatedAtUtc"] = now,
            ["CreatedByUserId"] = actorUserId,
            ["UpdatedByUserId"] = actorUserId
        };
    }

    private BsonDocument SceneMapGenerator0165AssetDoc(SceneMapGenerator0165Output output, BsonDocument asset, DateTime now, string actorUserId)
    {
        var id = SceneMapGenerator0165ScopedId(output.MapId, GetDocString(asset, "Id"));
        var assetKey = GetDocString(asset, "AssetKey", "crate");
        return new BsonDocument
        {
            ["_id"] = id,
            ["Id"] = id,
            ["SceneMapId"] = output.MapId,
            ["AssetKey"] = assetKey,
            ["DisplayName"] = GetDocString(asset, "DisplayName", DefaultSceneMap0164AssetDisplayName(assetKey)),
            ["AssetKind"] = GetDocString(asset, "AssetKind", DefaultSceneMap0164AssetKind(assetKey)),
            ["ObjectKind"] = GetDocString(asset, "ObjectKind", DefaultSceneMap0164ObjectKindForAsset(assetKey)),
            ["X"] = GetDocDouble(asset, "X", 0),
            ["Y"] = GetDocDouble(asset, "Y", 0),
            ["Width"] = GetDocDouble(asset, "Width", DefaultSceneMap0164AssetWidth(assetKey)),
            ["Height"] = GetDocDouble(asset, "Height", DefaultSceneMap0164AssetHeight(assetKey)),
            ["RotationDegrees"] = GetDocDouble(asset, "RotationDegrees", 0),
            ["ZIndex"] = GetDocInt(asset, "ZIndex", 100),
            ["Visibility"] = GetDocString(asset, "Visibility", "PlayerVisible"),
            ["DescriptionPlayer"] = GetDocString(asset, "DescriptionPlayer"),
            ["DescriptionGm"] = GetDocString(asset, "DescriptionGm"),
            ["BlocksMovement"] = GetDocBool(asset, "BlocksMovement"),
            ["BlocksVision"] = GetDocBool(asset, "BlocksVision"),
            ["ProvidesCover"] = GetDocBool(asset, "ProvidesCover"),
            ["IsInteractable"] = true,
            ["LinkedEntityType"] = GetDocString(asset, "LinkedEntityType", "None"),
            ["LinkedEntityId"] = GetDocString(asset, "LinkedEntityId"),
            ["IsArchived"] = false,
            ["CreatedAtUtc"] = now,
            ["UpdatedAtUtc"] = now,
            ["CreatedByUserId"] = actorUserId,
            ["UpdatedByUserId"] = actorUserId
        };
    }

    private BsonDocument SceneMapGenerator0165ShapeDoc(SceneMapGenerator0165Output output, BsonDocument shape, DateTime now, string actorUserId)
    {
        var id = SceneMapGenerator0165ScopedId(output.MapId, GetDocString(shape, "Id"));
        return new BsonDocument
        {
            ["_id"] = id,
            ["Id"] = id,
            ["SceneMapId"] = output.MapId,
            ["LayerId"] = string.Empty,
            ["DisplayName"] = GetDocString(shape, "DisplayName", "Область"),
            ["DescriptionPlayer"] = GetDocString(shape, "DescriptionPlayer"),
            ["DescriptionGm"] = GetDocString(shape, "DescriptionGm"),
            ["ShapeKind"] = GetDocString(shape, "ShapeKind", "Rectangle"),
            ["ObjectKind"] = GetDocString(shape, "ObjectKind", "Decoration"),
            ["X"] = GetDocDouble(shape, "X", 0),
            ["Y"] = GetDocDouble(shape, "Y", 0),
            ["Width"] = GetDocDouble(shape, "Width", 10),
            ["Height"] = GetDocDouble(shape, "Height", 10),
            ["Radius"] = GetDocDouble(shape, "Radius", 0),
            ["RotationDegrees"] = 0,
            ["Points"] = GetDocString(shape, "Points"),
            ["Text"] = GetDocString(shape, "Text"),
            ["FillKey"] = GetDocString(shape, "FillKey", "accent"),
            ["StrokeKey"] = GetDocString(shape, "StrokeKey", "default"),
            ["Opacity"] = GetDocDouble(shape, "Opacity", 0.35),
            ["MaterialKey"] = GetDocString(shape, "MaterialKey", "objective_gold_overlay"),
            ["TextureKey"] = GetDocString(shape, "TextureKey", "overlay_soft"),
            ["RenderMode"] = GetDocString(shape, "RenderMode", "TexturedShape"),
            ["GridSnapEnabled"] = true,
            ["VisualOpacity"] = GetDocDouble(shape, "VisualOpacity", 0.45),
            ["StrokeThickness"] = 1.4,
            ["ZIndex"] = GetDocInt(shape, "ZIndex", 220),
            ["SortOrder"] = GetDocInt(shape, "SortOrder", 100),
            ["Visibility"] = GetDocString(shape, "Visibility", "PlayerVisible"),
            ["BlocksMovement"] = false,
            ["BlocksVision"] = false,
            ["ProvidesCover"] = false,
            ["IsInteractable"] = true,
            ["LinkedEntityType"] = "None",
            ["LinkedEntityId"] = string.Empty,
            ["IsArchived"] = false,
            ["CreatedAtUtc"] = now,
            ["UpdatedAtUtc"] = now,
            ["CreatedByUserId"] = actorUserId,
            ["UpdatedByUserId"] = actorUserId
        };
    }

    private BsonDocument SceneMapGenerator0165MarkerDoc(SceneMapGenerator0165Output output, BsonDocument marker, DateTime now, string actorUserId)
    {
        var id = SceneMapGenerator0165ScopedId(output.MapId, GetDocString(marker, "Id"));
        return new BsonDocument
        {
            ["_id"] = id,
            ["Id"] = id,
            ["SceneMapId"] = output.MapId,
            ["CampaignId"] = output.CampaignId,
            ["DisplayName"] = GetDocString(marker, "DisplayName", "Маркер"),
            ["DescriptionPlayer"] = GetDocString(marker, "DescriptionPlayer"),
            ["DescriptionGm"] = GetDocString(marker, "DescriptionGm"),
            ["MarkerType"] = GetDocString(marker, "MarkerType", "PointOfInterest"),
            ["X"] = GetDocDouble(marker, "X", 0),
            ["Y"] = GetDocDouble(marker, "Y", 0),
            ["RadiusMeters"] = GetDocDouble(marker, "RadiusMeters", 0),
            ["Visibility"] = GetDocString(marker, "Visibility", "PlayerVisible"),
            ["IsArchived"] = false,
            ["CreatedAtUtc"] = now,
            ["UpdatedAtUtc"] = now,
            ["CreatedByUserId"] = actorUserId,
            ["UpdatedByUserId"] = actorUserId
        };
    }

    private static BsonDocument CloneTemplateTileLayer(BsonDocument x) => new BsonDocument
    {
        ["Id"] = ShortTemplateId(GetDocString(x, "Id"), "tile_layer"),
        ["DisplayName"] = GetDocString(x, "DisplayName"),
        ["TileSizeMeters"] = GetDocDouble(x, "TileSizeMeters", 5),
        ["SortOrder"] = GetDocInt(x, "SortOrder", 10),
        ["Visibility"] = GetDocString(x, "Visibility", "PlayerVisible")
    };

    private static BsonDocument CloneTemplateTilePatch(BsonDocument x) => new BsonDocument
    {
        ["Id"] = ShortTemplateId(GetDocString(x, "Id"), "tile_patch"),
        ["TileLayerId"] = ShortTemplateId(GetDocString(x, "TileLayerId"), "tile_layer_ground"),
        ["MaterialKey"] = GetDocString(x, "MaterialKey", "grass"),
        ["TextureKey"] = GetDocString(x, "TextureKey"),
        ["X"] = GetDocDouble(x, "X", 0),
        ["Y"] = GetDocDouble(x, "Y", 0),
        ["Width"] = GetDocDouble(x, "Width", 10),
        ["Height"] = GetDocDouble(x, "Height", 10),
        ["SortOrder"] = GetDocInt(x, "SortOrder", 10),
        ["Visibility"] = GetDocString(x, "Visibility", "PlayerVisible")
    };

    private static BsonDocument CloneTemplateAsset(BsonDocument x) => new BsonDocument
    {
        ["Id"] = ShortTemplateId(GetDocString(x, "Id"), "asset"),
        ["AssetKey"] = GetDocString(x, "AssetKey", "crate"),
        ["DisplayName"] = GetDocString(x, "DisplayName", "Объект"),
        ["AssetKind"] = GetDocString(x, "AssetKind", "Prop"),
        ["ObjectKind"] = GetDocString(x, "ObjectKind", "Decoration"),
        ["X"] = GetDocDouble(x, "X", 0),
        ["Y"] = GetDocDouble(x, "Y", 0),
        ["Width"] = GetDocDouble(x, "Width", 5),
        ["Height"] = GetDocDouble(x, "Height", 5),
        ["ZIndex"] = GetDocInt(x, "ZIndex", 100),
        ["Visibility"] = GetDocString(x, "Visibility", "PlayerVisible"),
        ["DescriptionPlayer"] = GetDocString(x, "DescriptionPlayer"),
        ["DescriptionGm"] = GetDocString(x, "DescriptionGm")
    };

    private static BsonDocument CloneTemplateShape(BsonDocument x) => new BsonDocument
    {
        ["Id"] = ShortTemplateId(GetDocString(x, "Id"), "shape"),
        ["DisplayName"] = GetDocString(x, "DisplayName", "Область"),
        ["ShapeKind"] = GetDocString(x, "ShapeKind", "Rectangle"),
        ["ObjectKind"] = GetDocString(x, "ObjectKind", "Decoration"),
        ["X"] = GetDocDouble(x, "X", 0),
        ["Y"] = GetDocDouble(x, "Y", 0),
        ["Width"] = GetDocDouble(x, "Width", 10),
        ["Height"] = GetDocDouble(x, "Height", 10),
        ["Visibility"] = GetDocString(x, "Visibility", "PlayerVisible"),
        ["DescriptionPlayer"] = GetDocString(x, "DescriptionPlayer"),
        ["DescriptionGm"] = GetDocString(x, "DescriptionGm")
    };

    private static BsonDocument CloneTemplateMarker(BsonDocument x) => new BsonDocument
    {
        ["Id"] = ShortTemplateId(GetDocString(x, "Id"), "marker"),
        ["DisplayName"] = GetDocString(x, "DisplayName", "Маркер"),
        ["MarkerType"] = GetDocString(x, "MarkerType", "PointOfInterest"),
        ["X"] = GetDocDouble(x, "X", 0),
        ["Y"] = GetDocDouble(x, "Y", 0),
        ["Visibility"] = GetDocString(x, "Visibility", "PlayerVisible"),
        ["DescriptionPlayer"] = GetDocString(x, "DescriptionPlayer"),
        ["DescriptionGm"] = GetDocString(x, "DescriptionGm")
    };

    private void EnsureSceneMapGenerator0165BuiltIns()
    {
        EnsureSceneMapGenerator0165Indexes();
        var now = DateTime.UtcNow;
        foreach (var preset in BuiltInSceneMapGenerator0165Presets(now))
            SceneMapGenerator0165Presets().ReplaceOne(IdFilter(GetDocString(preset, "Id")), preset, new ReplaceOptions { IsUpsert = true });
        foreach (var template in BuiltInSceneMapGenerator0165Templates(now))
            SceneMapGenerator0165Templates().ReplaceOne(IdFilter(GetDocString(template, "Id")), template, new ReplaceOptions { IsUpsert = true });
    }

    private IEnumerable<BsonDocument> BuiltInSceneMapGenerator0165Presets(DateTime now)
    {
        yield return Preset("preset_market_square_0165", "Рыночная площадь", "Market", "Street", 200, 200, 5, 5, 10, new[] { "market_square_cobble", "road_dirt", "cobblestone", "shop_floor" }, new[] { "market_stall", "cart", "crate", "barrel", "counter", "signboard" }, now);
        yield return Preset("preset_shop_interior_0165", "Магазин", "Shop", "Interior", 40, 30, 2, 2, 20, new[] { "shop_floor", "wood_planks", "stone_tiles" }, new[] { "counter", "shelf", "crate", "barrel", "door" }, now);
        yield return Preset("preset_tavern_0165", "Трактир", "Tavern", "Interior", 60, 50, 2, 2, 30, new[] { "tavern_floor", "wood_planks", "stone_tiles" }, new[] { "bar_counter", "table", "chair_or_bench", "hearth", "door" }, now);
        yield return Preset("preset_back_alley_0165", "Переулок", "Alley", "Street", 120, 80, 5, 5, 40, new[] { "alley_stone", "stone_tiles", "cobblestone" }, new[] { "crate", "barrel", "signboard", "door", "window" }, now);
        yield return Preset("preset_road_encounter_0165", "Дорога", "Road", "Area", 300, 200, 10, 10, 50, new[] { "grass", "dirt", "road_dirt", "stone" }, new[] { "tree", "rock", "log", "cover_low", "cover_high" }, now);
        yield return Preset("preset_camp_0165", "Лагерь", "Camp", "Location", 150, 150, 5, 5, 60, new[] { "grass", "dirt", "mud" }, new[] { "tent", "campfire", "crate", "barrel", "log" }, now);
        yield return Preset("preset_ruins_0165", "Руины", "Ruins", "Location", 200, 200, 5, 5, 70, new[] { "stone", "stone_tiles", "grass", "dirt" }, new[] { "rock", "cover_low", "cover_high", "obstacle", "objective_marker" }, now);
        yield return Preset("preset_warehouse_0165", "Склад", "Warehouse", "Interior", 80, 60, 2, 2, 80, new[] { "warehouse_floor", "wood_planks", "stone_tiles" }, new[] { "shelf", "crate", "barrel", "door", "cart" }, now);
        yield return Preset("preset_cave_or_mine_0165", "Пещера / шахта", "Cave", "Location", 200, 150, 5, 5, 90, new[] { "stone", "dirt", "mud", "shallow_water" }, new[] { "rock", "obstacle", "lantern", "cover_low" }, now);
        yield return Preset("preset_bridge_crossing_0165", "Мост / переправа", "Bridge", "Area", 200, 120, 5, 5, 100, new[] { "shallow_water", "bridge_wood", "road_dirt", "grass" }, new[] { "rock", "log", "cover_low", "signboard" }, now);
    }

    private IEnumerable<BsonDocument> BuiltInSceneMapGenerator0165Templates(DateTime now)
    {
        foreach (var spec in new[]
                 {
                     ("template_market_small_0165", "Малый рынок", "Market", "Street", 120, 100),
                     ("template_shop_small_0165", "Малый магазин", "Shop", "Interior", 40, 30),
                     ("template_tavern_small_0165", "Малый трактир", "Tavern", "Interior", 50, 40),
                     ("template_alley_small_0165", "Малый переулок", "Alley", "Street", 90, 70),
                     ("template_camp_small_0165", "Малый лагерь", "Camp", "Location", 120, 100)
                 })
        {
            var templateName = spec.Item1 switch
            {
                "template_market_small_0165" => "Малый рынок",
                "template_shop_small_0165" => "Малый магазин",
                "template_tavern_small_0165" => "Малый трактир",
                "template_alley_small_0165" => "Малый переулок",
                "template_camp_small_0165" => "Малый лагерь",
                _ => spec.Item2
            };
            var output = new SceneMapGenerator0165Output
            {
                PresetId = "preset_" + spec.Item3.ToLowerInvariant(),
                DisplayName = templateName,
                Description = "Встроенный шаблон генератора локаций.",
                LocationKind = spec.Item3,
                MapScale = spec.Item4,
                WidthMeters = spec.Item5,
                HeightMeters = spec.Item6,
                TileSizeMeters = spec.Item4 == "Interior" ? 2 : 5,
                GridSizeMeters = spec.Item4 == "Interior" ? 2 : 5,
                Seed = spec.Item1,
                Density = "Medium",
                DetailLevel = "Normal",
                Symmetry = "Loose",
                IncludeGmSecrets = true,
                IncludeHazards = true,
                IncludeSpawnZones = true,
                IncludeObjectiveZones = true,
                CampaignId = "dev-campaign-core",
                RuleSetId = "fantasy_nri_default",
                MapId = spec.Item1,
                CreatedByUserId = "system"
            };
            GeneratePresetBlueprint(output, new BsonDocument { ["DisplayName"] = templateName, ["LocationKind"] = spec.Item3 });
            yield return new BsonDocument
            {
                ["_id"] = spec.Item1,
                ["Id"] = spec.Item1,
                ["DisplayName"] = templateName,
                ["Description"] = "Встроенный шаблон библиотеки 0.16.5.",
                ["LocationKind"] = spec.Item3,
                ["MapScale"] = spec.Item4,
                ["WidthMeters"] = spec.Item5,
                ["HeightMeters"] = spec.Item6,
                ["TileSizeMeters"] = output.TileSizeMeters,
                ["GridSizeMeters"] = output.GridSizeMeters,
                ["TemplateSourceMapId"] = string.Empty,
                ["TileLayerBlueprints"] = new BsonArray(output.TileLayers),
                ["TilePatchBlueprints"] = new BsonArray(output.TilePatches),
                ["AssetInstanceBlueprints"] = new BsonArray(output.AssetInstances),
                ["ShapeBlueprints"] = new BsonArray(output.Shapes),
                ["MarkerBlueprints"] = new BsonArray(output.Markers),
                ["TokenBlueprints"] = new BsonArray(),
                ["SortOrder"] = 100,
                ["IsBuiltIn"] = true,
                ["IsArchived"] = false,
                ["CreatedAtUtc"] = now,
                ["UpdatedAtUtc"] = now
            };
        }
    }

    private static BsonDocument Preset(string id, string name, string kind, string scale, int width, int height, double tile, double grid, int sort, string[] materials, string[] assets, DateTime now)
    {
        name = id switch
        {
            "preset_market_square_0165" => "Рыночная площадь",
            "preset_shop_interior_0165" => "Магазин",
            "preset_tavern_0165" => "Трактир",
            "preset_back_alley_0165" => "Переулок",
            "preset_road_encounter_0165" => "Дорога",
            "preset_camp_0165" => "Лагерь",
            "preset_ruins_0165" => "Руины",
            "preset_warehouse_0165" => "Склад",
            "preset_cave_or_mine_0165" => "Пещера / шахта",
            "preset_bridge_crossing_0165" => "Мост / переправа",
            _ => name
        };

        return new BsonDocument
        {
            ["_id"] = id,
            ["Id"] = id,
            ["DisplayName"] = name,
            ["Description"] = $"Встроенный пресет генератора: {name}.",
            ["LocationKind"] = kind,
            ["MapScale"] = scale,
            ["DefaultWidthMeters"] = width,
            ["DefaultHeightMeters"] = height,
            ["DefaultTileSizeMeters"] = tile,
            ["DefaultGridSizeMeters"] = grid,
            ["AllowedMaterials"] = new BsonArray(materials),
            ["AllowedAssets"] = new BsonArray(assets),
            ["RequiredZones"] = new BsonArray(new[] { "entrance", "exit", "points_of_interest" }),
            ["GenerationRules"] = new BsonDocument { ["MinimumTileCoveragePercent"] = scale == "Interior" ? 80 : scale == "Street" ? 70 : 60 },
            ["SortOrder"] = sort,
            ["IsBuiltIn"] = true,
            ["IsArchived"] = false,
            ["CreatedAtUtc"] = now,
            ["UpdatedAtUtc"] = now
        };
    }

    private void AddTile(SceneMapGenerator0165Output output, string id, string material, double x, double y, double width, double height, int sort)
    {
        output.TilePatches.Add(new BsonDocument
        {
            ["Id"] = id,
            ["TileLayerId"] = "tile_layer_ground",
            ["MaterialKey"] = material,
            ["TextureKey"] = DefaultSceneMap0164TextureForMaterial(material),
            ["X"] = Clamp(x, 0, output.WidthMeters),
            ["Y"] = Clamp(y, 0, output.HeightMeters),
            ["Width"] = Clamp(width, 1, output.WidthMeters - Clamp(x, 0, output.WidthMeters - 1)),
            ["Height"] = Clamp(height, 1, output.HeightMeters - Clamp(y, 0, output.HeightMeters - 1)),
            ["SortOrder"] = sort,
            ["Visibility"] = "PlayerVisible"
        });
    }

    private void AddAsset(SceneMapGenerator0165Output output, string id, string assetKey, string name, double x, double y, double width, double height, string visibility, string gmNotes = "")
    {
        output.AssetInstances.Add(new BsonDocument
        {
            ["Id"] = id,
            ["AssetKey"] = assetKey,
            ["DisplayName"] = name,
            ["AssetKind"] = DefaultSceneMap0164AssetKind(assetKey),
            ["ObjectKind"] = DefaultSceneMap0164ObjectKindForAsset(assetKey),
            ["X"] = Clamp(x, 0, output.WidthMeters - Math.Max(1, width)),
            ["Y"] = Clamp(y, 0, output.HeightMeters - Math.Max(1, height)),
            ["Width"] = Math.Max(1, width),
            ["Height"] = Math.Max(1, height),
            ["ZIndex"] = 120,
            ["Visibility"] = visibility,
            ["DescriptionPlayer"] = visibility == "PlayerVisible" ? name : string.Empty,
            ["DescriptionGm"] = gmNotes
        });
    }

    private void AddShape(SceneMapGenerator0165Output output, string id, string name, string shapeKind, string objectKind, double x, double y, double width, double height, string visibility, bool gmOnlyNote)
    {
        output.Shapes.Add(new BsonDocument
        {
            ["Id"] = id,
            ["DisplayName"] = name,
            ["ShapeKind"] = shapeKind,
            ["ObjectKind"] = objectKind,
            ["X"] = Clamp(x, 0, output.WidthMeters - Math.Max(1, width)),
            ["Y"] = Clamp(y, 0, output.HeightMeters - Math.Max(1, height)),
            ["Width"] = Math.Max(1, width),
            ["Height"] = Math.Max(1, height),
            ["Visibility"] = visibility,
            ["DescriptionPlayer"] = visibility == "PlayerVisible" ? name : string.Empty,
            ["DescriptionGm"] = gmOnlyNote ? SceneMap0165GmLeakToken : string.Empty,
            ["MaterialKey"] = objectKind == "HazardZone" ? "hazard_red_overlay" : objectKind == "SpawnZone" ? "spawn_blue_overlay" : "objective_gold_overlay",
            ["Opacity"] = 0.34
        });
    }

    private void AddMarker(SceneMapGenerator0165Output output, string id, string name, string type, double x, double y, string visibility)
    {
        output.Markers.Add(new BsonDocument
        {
            ["Id"] = id,
            ["DisplayName"] = name,
            ["MarkerType"] = type,
            ["X"] = Clamp(x, 0, output.WidthMeters),
            ["Y"] = Clamp(y, 0, output.HeightMeters),
            ["Visibility"] = visibility,
            ["DescriptionPlayer"] = visibility == "PlayerVisible" ? name : string.Empty,
            ["DescriptionGm"] = visibility == "GmOnly" ? SceneMap0165GmLeakToken : string.Empty
        });
    }

    private void AddAssetsGrid(SceneMapGenerator0165Output output, Random rng, string idPrefix, string assetKey, string name, int count, double width, double height)
    {
        var densityBonus = output.Density == "High" ? 3 : output.Density == "Low" ? -2 : 0;
        var detailBonus = output.DetailLevel == "Rich" ? 2 : output.DetailLevel == "Basic" ? -1 : 0;
        var total = Math.Max(1, count + densityBonus + detailBonus);
        for (var i = 0; i < total; i++)
        {
            var x = output.WidthMeters * (0.12 + 0.74 * rng.NextDouble());
            var y = output.HeightMeters * (0.12 + 0.74 * rng.NextDouble());
            AddAsset(output, $"{idPrefix}_{i + 1}", assetKey, $"{name} {i + 1}", x, y, width, height, "PlayerVisible");
        }
    }

    private void AddAssetsLine(SceneMapGenerator0165Output output, string idPrefix, string assetKey, string name, int count, double width, double height, double startX, double startY)
    {
        for (var i = 0; i < count; i++)
            AddAsset(output, $"{idPrefix}_{i + 1}", assetKey, $"{name} {i + 1}", startX + i * (width + 1), startY, width, height, "PlayerVisible");
    }

    private void EnsureMinimumSceneMapGenerator0165Density(SceneMapGenerator0165Output output, Random rng)
    {
        var minAssets = output.MapScale == "Interior" ? 8 : output.MapScale == "Street" ? 15 : 10;
        while (output.AssetInstances.Count(x => GetDocString(x, "Visibility") == "PlayerVisible") < minAssets)
            AddAsset(output, "filler_" + output.AssetInstances.Count, output.MapScale == "Interior" ? "crate" : "rock", output.MapScale == "Interior" ? "Реквизит" : "Ориентир", output.WidthMeters * rng.NextDouble(), output.HeightMeters * rng.NextDouble(), 6, 6, "PlayerVisible");
        if (output.Markers.Count(x => GetDocString(x, "MarkerType") is "Entrance" or "Exit") < 1)
            AddMarker(output, "entrance_auto", "Вход", "Entrance", 2, output.HeightMeters * 0.5, "PlayerVisible");
        if (output.MapScale != "Interior" && output.Markers.Count(x => GetDocString(x, "MarkerType") is "Entrance" or "Exit") < 2)
            AddMarker(output, "exit_auto", "Выход", "Exit", output.WidthMeters - 3, output.HeightMeters * 0.5, "PlayerVisible");
    }

    private string ComputeSceneMapGenerator0165Hash(SceneMapGenerator0165Output output)
    {
        var text = string.Join("|", new[]
        {
            output.PresetId, output.TemplateId, output.Seed, output.WidthMeters.ToString(CultureInfo.InvariantCulture), output.HeightMeters.ToString(CultureInfo.InvariantCulture),
            string.Join(";", output.TilePatches.OrderBy(x => GetDocString(x, "Id")).Select(x => $"{GetDocString(x, "Id")}:{GetDocString(x, "MaterialKey")}:{GetDocDouble(x, "X", 0):0.###}:{GetDocDouble(x, "Y", 0):0.###}:{GetDocDouble(x, "Width", 0):0.###}:{GetDocDouble(x, "Height", 0):0.###}")),
            string.Join(";", output.AssetInstances.OrderBy(x => GetDocString(x, "Id")).Select(x => $"{GetDocString(x, "Id")}:{GetDocString(x, "AssetKey")}:{GetDocDouble(x, "X", 0):0.###}:{GetDocDouble(x, "Y", 0):0.###}:{GetDocString(x, "Visibility")}")),
            string.Join(";", output.Shapes.OrderBy(x => GetDocString(x, "Id")).Select(x => $"{GetDocString(x, "Id")}:{GetDocString(x, "ObjectKind")}:{GetDocDouble(x, "X", 0):0.###}:{GetDocDouble(x, "Y", 0):0.###}:{GetDocString(x, "Visibility")}")),
            string.Join(";", output.Markers.OrderBy(x => GetDocString(x, "Id")).Select(x => $"{GetDocString(x, "Id")}:{GetDocString(x, "MarkerType")}:{GetDocDouble(x, "X", 0):0.###}:{GetDocDouble(x, "Y", 0):0.###}:{GetDocString(x, "Visibility")}"))
        });
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", string.Empty).ToLowerInvariant();
    }

    private void EnsureSceneMapGenerator0165Indexes()
    {
        SceneMapGenerator0165Presets().Indexes.CreateMany(new[]
        {
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("Id")),
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("LocationKind")),
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("IsArchived"))
        });
        SceneMapGenerator0165Templates().Indexes.CreateMany(new[]
        {
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("Id")),
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("LocationKind")),
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("IsArchived"))
        });
        SceneMapGenerator0165Runs().Indexes.CreateMany(new[]
        {
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("Id")),
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("PresetId")),
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("GeneratedSceneMapId")),
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("Status"))
        });
    }

    private bool SceneMapGenerator0165Enabled() => SceneMap0164AdminEnabled();

    private ResponseEnvelope SceneMapGenerator0165Disabled(string command)
    {
        _logger.Admin($"scene.map.generator.0165.disabled command={command}");
        return Error("Scene / Location Map Generator is disabled by feature flags.", ResponseStatus.Forbidden, ErrorCode.Forbidden);
    }

    private IMongoCollection<BsonDocument> SceneMapGenerator0165Presets() => _mongo.Database.GetCollection<BsonDocument>(SceneMap0165PresetsCollection);
    private IMongoCollection<BsonDocument> SceneMapGenerator0165Templates() => _mongo.Database.GetCollection<BsonDocument>(SceneMap0165TemplatesCollection);
    private IMongoCollection<BsonDocument> SceneMapGenerator0165Runs() => _mongo.Database.GetCollection<BsonDocument>(SceneMap0165RunsCollection);

    private static string NormalizeSceneMapGenerator0165LocationKind(string? value)
    {
        var input = (value ?? string.Empty).Trim().ToLowerInvariant();
        return input switch
        {
            "market" => "Market",
            "shop" => "Shop",
            "tavern" => "Tavern",
            "alley" => "Alley",
            "street" => "Street",
            "road" => "Road",
            "camp" => "Camp",
            "ruins" => "Ruins",
            "warehouse" => "Warehouse",
            "cave" or "mine" => "Cave",
            "bridge" => "Bridge",
            "port" => "Port",
            "temple" => "Temple",
            "hideout" => "Hideout",
            "smallbattlefield" or "small_battlefield" => "SmallBattlefield",
            _ => "Market"
        };
    }

    private static string MaterialForKind(string kind)
        => kind switch
        {
            "Shop" => "shop_floor",
            "Tavern" => "tavern_floor",
            "Warehouse" => "warehouse_floor",
            "Alley" => "alley_stone",
            "Road" => "grass",
            "Camp" => "grass",
            "Cave" => "stone",
            "Bridge" => "grass",
            "Ruins" => "grass",
            _ => "grass"
        };

    private static string NormalizeSceneMapGenerator0165Option(string? value, string fallback, string[] allowed)
        => allowed.FirstOrDefault(x => string.Equals(x, value, StringComparison.OrdinalIgnoreCase)) ?? fallback;

    private static int StableSceneMapGenerator0165Seed(string text)
    {
        unchecked
        {
            var hash = 17;
            foreach (var ch in text)
                hash = hash * 31 + ch;
            return hash;
        }
    }

    private static double Clamp(double value, double min, double max)
        => Math.Max(min, Math.Min(Math.Max(min, max), value));

    private static string SceneMapGenerator0165ScopedId(string mapId, string localId)
        => mapId + "_" + ShortTemplateId(localId, "item");

    private static string ShortTemplateId(string id, string fallback)
    {
        if (string.IsNullOrWhiteSpace(id)) return fallback;
        var marker = id.LastIndexOf("_", StringComparison.Ordinal);
        return marker > 0 && id.Length - marker < 48 ? id.Substring(marker + 1) : id.Replace("scene_map_tile_visual_demo_0164_", string.Empty);
    }

    private static string ToCamel(string value)
        => string.IsNullOrWhiteSpace(value) ? value : char.ToLowerInvariant(value[0]) + value.Substring(1);

    private static object SceneMapGenerator0165PayloadValue(BsonValue value)
    {
        if (value == null || value.IsBsonNull) return string.Empty;
        if (value.IsString) return value.AsString;
        if (value.IsBoolean) return value.AsBoolean;
        if (value.IsInt32) return value.AsInt32;
        if (value.IsInt64) return value.AsInt64;
        if (value.IsDouble) return value.AsDouble;
        if (value.IsValidDateTime) return value.ToUniversalTime();
        if (value.IsBsonArray) return value.AsBsonArray.Select(SceneMapGenerator0165PayloadValue).ToArray();
        if (value.IsBsonDocument) return value.AsBsonDocument.Elements.ToDictionary(x => ToCamel(x.Name), x => SceneMapGenerator0165PayloadValue(x.Value));
        return value.ToString();
    }

    private static string[] BsonArrayToStrings0165(BsonDocument doc, string name)
    {
        if (!doc.TryGetValue(name, out var value) || !value.IsBsonArray) return Array.Empty<string>();
        return value.AsBsonArray.Select(x => x.ToString()).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
    }

    private sealed class SceneMapGenerator0165Output
    {
        public string PresetId { get; set; } = string.Empty;
        public string TemplateId { get; set; } = string.Empty;
        public string MapId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string LocationKind { get; set; } = "Market";
        public string MapScale { get; set; } = "Street";
        public int WidthMeters { get; set; }
        public int HeightMeters { get; set; }
        public double TileSizeMeters { get; set; }
        public double GridSizeMeters { get; set; }
        public string Seed { get; set; } = string.Empty;
        public string Density { get; set; } = "Medium";
        public string DetailLevel { get; set; } = "Normal";
        public string Symmetry { get; set; } = "None";
        public bool IncludeGmSecrets { get; set; }
        public bool IncludeHazards { get; set; }
        public bool IncludeSpawnZones { get; set; }
        public bool IncludeObjectiveZones { get; set; }
        public string CampaignId { get; set; } = "dev-campaign-core";
        public string RuleSetId { get; set; } = "fantasy_nri_default";
        public string CreatedByUserId { get; set; } = string.Empty;
        public string NormalizedHash { get; set; } = string.Empty;
        public List<BsonDocument> TileLayers { get; } = new();
        public List<BsonDocument> TilePatches { get; } = new();
        public List<BsonDocument> AssetInstances { get; } = new();
        public List<BsonDocument> Shapes { get; } = new();
        public List<BsonDocument> Markers { get; } = new();

        public BsonDocument ParametersDoc() => new BsonDocument
        {
            ["PresetId"] = PresetId,
            ["TemplateId"] = TemplateId,
            ["Seed"] = Seed,
            ["WidthMeters"] = WidthMeters,
            ["HeightMeters"] = HeightMeters,
            ["TileSizeMeters"] = TileSizeMeters,
            ["GridSizeMeters"] = GridSizeMeters,
            ["Density"] = Density,
            ["DetailLevel"] = DetailLevel,
            ["Symmetry"] = Symmetry,
            ["IncludeGmSecrets"] = IncludeGmSecrets,
            ["IncludeHazards"] = IncludeHazards,
            ["IncludeSpawnZones"] = IncludeSpawnZones,
            ["IncludeObjectiveZones"] = IncludeObjectiveZones
        };

        public BsonDocument SummaryDoc() => new BsonDocument
        {
            ["TileLayerCount"] = TileLayers.Count,
            ["TilePatchCount"] = TilePatches.Count,
            ["AssetInstanceCount"] = AssetInstances.Count,
            ["ShapeCount"] = Shapes.Count,
            ["MarkerCount"] = Markers.Count,
            ["NormalizedHash"] = NormalizedHash
        };

        public BsonDocument BlueprintDoc() => new BsonDocument
        {
            ["DisplayName"] = DisplayName,
            ["Description"] = Description,
            ["LocationKind"] = LocationKind,
            ["MapScale"] = MapScale,
            ["WidthMeters"] = WidthMeters,
            ["HeightMeters"] = HeightMeters,
            ["TileSizeMeters"] = TileSizeMeters,
            ["GridSizeMeters"] = GridSizeMeters,
            ["Density"] = Density,
            ["DetailLevel"] = DetailLevel,
            ["Symmetry"] = Symmetry,
            ["IncludeGmSecrets"] = IncludeGmSecrets,
            ["IncludeHazards"] = IncludeHazards,
            ["IncludeSpawnZones"] = IncludeSpawnZones,
            ["IncludeObjectiveZones"] = IncludeObjectiveZones,
            ["CampaignId"] = CampaignId,
            ["RuleSetId"] = RuleSetId,
            ["TileLayerBlueprints"] = new BsonArray(TileLayers),
            ["TilePatchBlueprints"] = new BsonArray(TilePatches),
            ["AssetInstanceBlueprints"] = new BsonArray(AssetInstances),
            ["ShapeBlueprints"] = new BsonArray(Shapes),
            ["MarkerBlueprints"] = new BsonArray(Markers)
        };
    }
}
