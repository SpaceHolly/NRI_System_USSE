using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using MongoDB.Driver;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;
using Nri.Shared.Utilities;

namespace Nri.Server.Application;

public partial class ServiceHub
{
    private const string Campaign0218 = "dev-campaign-core";
    private const string RuleSet0218 = "fantasy_nri_default";
    private const int MaxActivePrimitives0218 = 1000;

    public ResponseEnvelope WorldAdminMapsList0218(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var campaignId = First0218(PayloadReader.GetString(payload, "campaignId"), Campaign0218);
        if (PayloadReader.GetBool(payload, "ensureFixture")) EnsureFixture0218(actor.Id);
        var maps = _mongo.MapCanvases.Find(x => x.CampaignId == campaignId && !x.IsArchived
                && x.CoordinateProfileId != null && x.CoordinateProfileId != string.Empty
                && x.ScaleProfileId != null && x.ScaleProfileId != string.Empty
                && x.PrimaryBoundWorldEntityId != null && x.PrimaryBoundWorldEntityId != string.Empty)
            .SortBy(x => x.Name).Limit(500).ToList();
        return Ok("Карты загружены.", new Dictionary<string, object>
        {
            { "maps", maps.Select(AdminMapSummary0218).Cast<object>().ToArray() },
            { "count", maps.Count }
        });
    }

    public ResponseEnvelope WorldAdminMapHierarchyGet0218(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var campaignId = First0218(PayloadReader.GetString(payload, "campaignId"), Campaign0218);
        if (PayloadReader.GetBool(payload, "ensureFixture")) EnsureFixture0218(actor.Id);
        var allNodes = _mongo.MapSpaceNodes.Find(x => x.CampaignId == campaignId && !x.IsArchived)
            .SortBy(x => x.SortOrder).ThenBy(x => x.Name).Limit(2000).ToList();
        var maps = _mongo.MapCanvases.Find(x => x.CampaignId == campaignId && !x.IsArchived
                && x.CoordinateProfileId != null && x.CoordinateProfileId != string.Empty
                && x.ScaleProfileId != null && x.ScaleProfileId != string.Empty
                && x.PrimaryBoundWorldEntityId != null && x.PrimaryBoundWorldEntityId != string.Empty).Limit(1000).ToList();
        var nodesById = allNodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
        var relevantNodeIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var map in maps)
        {
            if (!string.IsNullOrWhiteSpace(map.SpaceNodeId)) relevantNodeIds.Add(map.SpaceNodeId);
            if (!string.IsNullOrWhiteSpace(map.PrimaryBoundWorldEntityId)) relevantNodeIds.Add(map.PrimaryBoundWorldEntityId);
            foreach (var id in map.BoundWorldEntityIds ?? new List<string>())
                if (!string.IsNullOrWhiteSpace(id)) relevantNodeIds.Add(id);
        }
        foreach (var nodeId in relevantNodeIds.ToArray())
        {
            var cursor = nodeId;
            var guard = 0;
            while (nodesById.TryGetValue(cursor, out var node) && !string.IsNullOrWhiteSpace(node.ParentId) && guard++ < 64)
            {
                relevantNodeIds.Add(node.ParentId);
                cursor = node.ParentId;
            }
        }
        var nodes = allNodes.Where(node => relevantNodeIds.Contains(node.Id)).ToList();
        return Ok("Иерархия мира загружена.", new Dictionary<string, object>
        {
            { "nodes", nodes.Select(NodePayload0218).Cast<object>().ToArray() },
            { "maps", maps.Select(AdminMapSummary0218).Cast<object>().ToArray() },
            { "rootCount", nodes.Count(node => string.IsNullOrWhiteSpace(node.ParentId)) }
        });
    }

    public ResponseEnvelope WorldAdminMapGet0218(CommandContext context)
    {
        RequireAdmin(context);
        var map = RequireMap0218(PayloadReader.GetString(context.Request.Payload, "mapId"));
        return Ok("Карта загружена.", AdminMapProjection0218(map));
    }

    public ResponseEnvelope WorldAdminMapCreate0218(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var name = RequireLength(PayloadReader.GetString(payload, "name"), 1, 160, "name");
        var mapType = NormalizeMapType0218(PayloadReader.GetString(payload, "mapType"));
        var primaryNodeId = RequireLength(PayloadReader.GetString(payload, "primaryBoundWorldEntityId"), 1, 128, "primaryBoundWorldEntityId");
        if (!_mongo.MapSpaceNodes.Find(x => x.Id == primaryNodeId && !x.IsArchived).Any())
            throw new ArgumentException("Выбранный узел мира не существует.");
        var map = new MapCanvasState
        {
            CampaignId = First0218(PayloadReader.GetString(payload, "campaignId"), Campaign0218),
            RuleSetId = First0218(PayloadReader.GetString(payload, "ruleSetId"), RuleSet0218),
            WorldId = PayloadReader.GetString(payload, "worldId"),
            SpaceNodeId = primaryNodeId,
            PrimaryBoundWorldEntityId = primaryNodeId,
            BoundWorldEntityIds = new List<string> { primaryNodeId },
            ParentMapId = PayloadReader.GetString(payload, "parentMapId"),
            MapType = mapType,
            Name = name,
            Description = RequireLength(PayloadReader.GetString(payload, "description"), 0, 4096, "description"),
            CoordinateProfileId = PayloadReader.GetString(payload, "coordinateProfileId"),
            ScaleProfileId = PayloadReader.GetString(payload, "scaleProfileId"),
            WidthMeters = Math.Max(1, PayloadReader.GetInt(payload, "widthMeters") ?? 100000),
            HeightMeters = Math.Max(1, PayloadReader.GetInt(payload, "heightMeters") ?? 100000),
            VisibilityMode = First0218(PayloadReader.GetString(payload, "visibilityMode"), MapVisibilityModes.Party),
            SchemaVersion = 1,
            EntityRevision = 1,
            EditorRevision = 1,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        _mongo.MapCanvases.InsertOne(map);
        WriteAudit("world_map", map.Id, "create_0218", actor.Id);
        return Ok("Карта создана.", new Dictionary<string, object> { { "map", AdminMapSummary0218(map) } });
    }

    public ResponseEnvelope WorldAdminMapUpdate0218(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var map = RequireMap0218(PayloadReader.GetString(payload, "mapId"));
        var expected = PayloadReader.GetLong(payload, "expectedRevision") ?? map.EntityRevision;
        if (expected != map.EntityRevision) throw new InvalidOperationException("Карта уже изменена. Обновите данные и повторите операцию.");
        var name = PayloadReader.GetString(payload, "name");
        if (!string.IsNullOrWhiteSpace(name)) map.Name = RequireLength(name, 1, 160, "name");
        if (payload.ContainsKey("description")) map.Description = RequireLength(PayloadReader.GetString(payload, "description"), 0, 4096, "description");
        if (payload.ContainsKey("visibilityMode")) map.VisibilityMode = First0218(PayloadReader.GetString(payload, "visibilityMode"), map.VisibilityMode);
        map.EntityRevision++;
        map.EditorRevision++;
        map.UpdatedAtUtc = DateTime.UtcNow;
        _mongo.MapCanvases.ReplaceOne(x => x.Id == map.Id, map);
        WriteAudit("world_map", map.Id, "update_0218", actor.Id);
        return Ok("Карта обновлена.", new Dictionary<string, object> { { "map", AdminMapSummary0218(map) } });
    }

    public ResponseEnvelope WorldAdminMapBindingUpdate0218(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var map = RequireMap0218(PayloadReader.GetString(payload, "mapId"));
        var ids = ReadStringList0218(payload, "boundWorldEntityIds");
        if (ids.Count == 0) throw new ArgumentException("Укажите хотя бы один узел мира.");
        var existing = _mongo.MapSpaceNodes.Find(x => ids.Contains(x.Id) && !x.IsArchived).ToList();
        if (existing.Count != ids.Count) throw new ArgumentException("Один или несколько узлов мира не найдены.");
        map.BoundWorldEntityIds = ids;
        map.PrimaryBoundWorldEntityId = First0218(PayloadReader.GetString(payload, "primaryBoundWorldEntityId"), ids[0]);
        if (!ids.Contains(map.PrimaryBoundWorldEntityId, StringComparer.Ordinal)) throw new ArgumentException("Основной узел должен входить в список привязок.");
        map.SpaceNodeId = map.PrimaryBoundWorldEntityId;
        map.EntityRevision++;
        map.UpdatedAtUtc = DateTime.UtcNow;
        _mongo.MapCanvases.ReplaceOne(x => x.Id == map.Id, map);
        WriteAudit("world_map", map.Id, "binding_update_0218", actor.Id);
        return Ok("Привязки карты обновлены.", new Dictionary<string, object> { { "map", AdminMapSummary0218(map) } });
    }

    public ResponseEnvelope WorldAdminMapValidate0218(CommandContext context)
    {
        RequireAdmin(context);
        var map = RequireMap0218(PayloadReader.GetString(context.Request.Payload, "mapId"));
        var findings = ValidateMap0218(map);
        return Ok(findings.Count == 0 ? "Карта прошла проверку." : "Карта содержит предупреждения.", new Dictionary<string, object>
        {
            { "valid", findings.Count == 0 }, { "findings", findings.Cast<object>().ToArray() }
        });
    }

    public ResponseEnvelope WorldAdminMapFeatureCreate0218(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var feature = ReadFeature0218(context.Request.Payload ?? new Dictionary<string, object>(), null);
        RequireMap0218(feature.MapId);
        ValidateFeatureLayer0218(feature);
        feature.IsManual = true;
        feature.Revision = 1;
        feature.UpdatedAtUtc = DateTime.UtcNow;
        _mongo.MapSemanticFeatures0218.InsertOne(feature);
        WriteAudit("map_feature", feature.Id, "create", actor.Id);
        return Ok("Объект карты создан.", new Dictionary<string, object> { { "feature", AdminFeaturePayload0218(feature) } });
    }

    public ResponseEnvelope WorldAdminMapFeatureUpdate0218(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var id = RequireLength(PayloadReader.GetString(payload, "featureId"), 1, 128, "featureId");
        var existing = _mongo.MapSemanticFeatures0218.Find(x => x.Id == id && !x.IsArchived).FirstOrDefault()
            ?? throw new InvalidOperationException("Объект карты не найден.");
        var expected = PayloadReader.GetLong(payload, "expectedRevision") ?? existing.Revision;
        if (expected != existing.Revision) throw new InvalidOperationException("Объект карты уже изменён. Обновите данные.");
        var updated = ReadFeature0218(payload, existing);
        ValidateFeatureLayer0218(updated);
        updated.IsManual = true;
        updated.Revision++;
        updated.UpdatedAtUtc = DateTime.UtcNow;
        _mongo.MapSemanticFeatures0218.ReplaceOne(x => x.Id == id, updated);
        WriteAudit("map_feature", id, "update", actor.Id);
        return Ok("Объект карты обновлён.", new Dictionary<string, object> { { "feature", AdminFeaturePayload0218(updated) } });
    }

    public ResponseEnvelope WorldAdminMapFeatureArchive0218(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var id = RequireLength(PayloadReader.GetString(context.Request.Payload, "featureId"), 1, 128, "featureId");
        var update = Builders<MapSemanticFeatureState0218>.Update.Set(x => x.IsArchived, true).Set(x => x.UpdatedAtUtc, DateTime.UtcNow).Inc(x => x.Revision, 1);
        var result = _mongo.MapSemanticFeatures0218.UpdateOne(x => x.Id == id && !x.IsArchived, update);
        if (result.ModifiedCount != 1) throw new InvalidOperationException("Объект карты не найден.");
        WriteAudit("map_feature", id, "archive", actor.Id);
        return Ok("Объект карты архивирован.");
    }

    public ResponseEnvelope WorldAdminMapLayerUpdate0218(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var id = RequireLength(PayloadReader.GetString(payload, "layerId"), 1, 128, "layerId");
        var layer = _mongo.MapSemanticLayers0218.Find(x => x.Id == id && !x.IsArchived).FirstOrDefault()
            ?? throw new InvalidOperationException("Слой карты не найден.");
        var expected = PayloadReader.GetLong(payload, "expectedRevision") ?? layer.Revision;
        if (expected != layer.Revision) throw new InvalidOperationException("Слой уже изменён. Обновите карту.");
        if (payload.ContainsKey("visibleToPlayers")) layer.IsVisibleToPlayers = PayloadReader.GetBool(payload, "visibleToPlayers");
        if (payload.ContainsKey("isLocked")) layer.IsLocked = PayloadReader.GetBool(payload, "isLocked");
        layer.Revision++;
        layer.UpdatedAtUtc = DateTime.UtcNow;
        _mongo.MapSemanticLayers0218.ReplaceOne(x => x.Id == id, layer);
        WriteAudit("map_layer", id, "update_0218", actor.Id);
        return Ok("Слой карты обновлён.", new Dictionary<string, object> { { "layer", LayerPayload0218(layer) } });
    }

    public ResponseEnvelope WorldAdminMapPortalCreate0218(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var portal = ReadPortal0218(context.Request.Payload ?? new Dictionary<string, object>(), null);
        ValidatePortal0218(portal);
        portal.Revision = 1;
        portal.UpdatedAtUtc = DateTime.UtcNow;
        _mongo.MapPortals0218.InsertOne(portal);
        WriteAudit("map_portal", portal.Id, "create", actor.Id);
        return Ok("Переход создан.", new Dictionary<string, object> { { "portal", AdminPortalPayload0218(portal) } });
    }

    public ResponseEnvelope WorldAdminMapPortalUpdate0218(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var id = RequireLength(PayloadReader.GetString(payload, "portalId"), 1, 128, "portalId");
        var existing = _mongo.MapPortals0218.Find(x => x.Id == id && !x.IsArchived).FirstOrDefault()
            ?? throw new InvalidOperationException("Переход не найден.");
        var expected = PayloadReader.GetLong(payload, "expectedRevision") ?? existing.Revision;
        if (expected != existing.Revision) throw new InvalidOperationException("Переход уже изменён. Обновите карту.");
        var portal = ReadPortal0218(payload, existing);
        ValidatePortal0218(portal);
        portal.Revision++;
        portal.UpdatedAtUtc = DateTime.UtcNow;
        _mongo.MapPortals0218.ReplaceOne(x => x.Id == id, portal);
        WriteAudit("map_portal", id, "update", actor.Id);
        return Ok("Переход обновлён.", new Dictionary<string, object> { { "portal", AdminPortalPayload0218(portal) } });
    }

    public ResponseEnvelope WorldAdminMapDiscoveryGrant0218(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var ownerUserId = RequireLength(PayloadReader.GetString(payload, "ownerUserId"), 1, 128, "ownerUserId");
        var entityId = RequireLength(PayloadReader.GetString(payload, "entityId"), 1, 128, "entityId");
        var characterId = PayloadReader.GetString(payload, "characterId");
        var precision = NormalizePrecision0218(PayloadReader.GetString(payload, "precision"));
        var id = "map_knowledge_0218_" + ownerUserId + "_" + (string.IsNullOrWhiteSpace(characterId) ? "all" : characterId) + "_" + entityId;
        var state = _mongo.EntityKnowledgeStates.Find(x => x.Id == id).FirstOrDefault() ?? new EntityKnowledgeState { Id = id };
        state.CampaignId = First0218(PayloadReader.GetString(payload, "campaignId"), Campaign0218);
        state.KnowledgeId = id;
        state.EntityType = "map_geography";
        state.EntityId = entityId;
        state.EntityDisplayName = PayloadReader.GetString(payload, "displayName");
        state.OwnerUserId = ownerUserId;
        state.Level = precision == MapDiscoveryPrecisionIds0218.Exact ? KnowledgeLevelIds.Truth : KnowledgeLevelIds.Partial;
        state.TruthRelation = KnowledgeTruthRelationIds.Accurate;
        state.PlayerSummary = PayloadReader.GetString(payload, "playerSummary");
        state.IsApplied = true;
        state.IsPlayerVisible = true;
        state.VisibilityMode = ProjectVisibilityModeIds.PlayerVisible;
        state.GrantedByUserId = actor.Id;
        state.GrantedAtUtc = state.GrantedAtUtc == default ? DateTime.UtcNow : state.GrantedAtUtc;
        state.UpdatedAtUtc = DateTime.UtcNow;
        state.UpdatedByUserId = actor.Id;
        state.IsArchived = false;
        state.ExtraData["precision"] = precision;
        state.ExtraData["characterId"] = characterId;
        state.ExtraData["approximateX"] = PayloadReader.GetDouble(payload, "approximateX") ?? 0d;
        state.ExtraData["approximateY"] = PayloadReader.GetDouble(payload, "approximateY") ?? 0d;
        _mongo.EntityKnowledgeStates.ReplaceOne(x => x.Id == id, state, new ReplaceOptions { IsUpsert = true });
        WriteAudit("map_discovery", entityId, "grant_" + precision, actor.Id);
        return Ok("Знание о местности обновлено.", new Dictionary<string, object> { { "precision", precision } });
    }

    public ResponseEnvelope WorldAdminMapPlayerPreview0218(CommandContext context)
    {
        RequireAdmin(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var ownerId = PayloadReader.GetString(payload, "ownerUserId");
        if (string.IsNullOrWhiteSpace(ownerId))
            ownerId = _mongo.Accounts.Find(x => x.Login == "dev_player").FirstOrDefault()?.Id ?? string.Empty;
        return Ok("Предпросмотр игрока построен.", PlayerMapProjection0218(RequireMap0218(PayloadReader.GetString(payload, "mapId")), ownerId, PayloadReader.GetString(payload, "characterId")));
    }

    public ResponseEnvelope WorldPlayerMapsList0218(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var campaignId = First0218(PayloadReader.GetString(context.Request.Payload, "campaignId"), Campaign0218);
        var maps = _mongo.MapCanvases.Find(x => x.CampaignId == campaignId && !x.IsArchived
                && x.CoordinateProfileId != null && x.CoordinateProfileId != string.Empty
                && x.ScaleProfileId != null && x.ScaleProfileId != string.Empty
                && x.PrimaryBoundWorldEntityId != null && x.PrimaryBoundWorldEntityId != string.Empty
                && x.VisibilityMode != MapVisibilityModes.GmOnly && x.VisibilityMode != MapVisibilityModes.Hidden)
            .SortBy(x => x.Name).Limit(500).ToList();
        var items = maps.Where(map => CanPlayerKnowMap0218(map, actor.Id)).Select(PlayerMapSummary0218).Cast<object>().ToArray();
        return Ok("Доступные карты загружены.", new Dictionary<string, object> { { "maps", items }, { "count", items.Length } });
    }

    public ResponseEnvelope WorldPlayerMapGet0218(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var map = RequireMap0218(PayloadReader.GetString(payload, "mapId"));
        if (!CanPlayerKnowMap0218(map, actor.Id)) throw new UnauthorizedAccessException("Карта ещё не открыта персонажу.");
        return Ok("Карта загружена.", PlayerMapProjection0218(map, actor.Id, PayloadReader.GetString(payload, "characterId")));
    }

    public ResponseEnvelope WorldPlayerMapChildren0218(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var map = RequireMap0218(PayloadReader.GetString(context.Request.Payload, "mapId"));
        if (!CanPlayerKnowMap0218(map, actor.Id)) throw new UnauthorizedAccessException("Карта ещё не открыта персонажу.");
        var portals = VisiblePortals0218(map.Id, actor.Id);
        var targetIds = portals.Select(portal => portal.TargetMapId).Distinct().ToList();
        var maps = _mongo.MapCanvases.Find(x => targetIds.Contains(x.Id) && !x.IsArchived).ToList()
            .Where(child => CanPlayerKnowMap0218(child, actor.Id)).ToList();
        return Ok("Доступные переходы загружены.", new Dictionary<string, object>
        {
            { "children", maps.Select(PlayerMapSummary0218).Cast<object>().ToArray() },
            { "portals", portals.Where(portal => maps.Any(mapItem => mapItem.Id == portal.TargetMapId)).Select(PlayerPortalPayload0218).Cast<object>().ToArray() }
        });
    }

    public ResponseEnvelope WorldPlayerMapPortalOpen0218(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var portalId = RequireLength(PayloadReader.GetString(context.Request.Payload, "portalId"), 1, 128, "portalId");
        var portal = VisiblePortals0218(string.Empty, actor.Id).FirstOrDefault(item => item.Id == portalId)
            ?? throw new UnauthorizedAccessException("Переход недоступен.");
        var target = RequireMap0218(portal.TargetMapId);
        if (!CanPlayerKnowMap0218(target, actor.Id)) throw new UnauthorizedAccessException("Целевая карта ещё не открыта персонажу.");
        return Ok("Переход открыт.", PlayerMapProjection0218(target, actor.Id, PayloadReader.GetString(context.Request.Payload, "characterId")));
    }

    public ResponseEnvelope WorldPlayerMapFeatureGet0218(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var id = RequireLength(PayloadReader.GetString(context.Request.Payload, "featureId"), 1, 128, "featureId");
        var feature = _mongo.MapSemanticFeatures0218.Find(x => x.Id == id && !x.IsArchived).FirstOrDefault()
            ?? throw new InvalidOperationException("Объект карты не найден.");
        var knowledge = KnowledgeFor0218(actor.Id, id);
        if (!FeatureVisible0218(feature, knowledge)) throw new UnauthorizedAccessException("Объект карты ещё не известен персонажу.");
        return Ok("Объект карты загружен.", new Dictionary<string, object> { { "feature", PlayerFeaturePayload0218(feature, knowledge) } });
    }

    public ResponseEnvelope WorldPlayerMapDiscoveryGet0218(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var mapId = RequireLength(PayloadReader.GetString(context.Request.Payload, "mapId"), 1, 128, "mapId");
        var features = _mongo.MapSemanticFeatures0218.Find(x => x.MapId == mapId && !x.IsArchived).Limit(MaxActivePrimitives0218).ToList();
        var known = features.Select(feature => new { feature, knowledge = KnowledgeFor0218(actor.Id, feature.Id) })
            .Where(item => FeatureVisible0218(item.feature, item.knowledge))
            .Select(item => PlayerFeaturePayload0218(item.feature, item.knowledge)).Cast<object>().ToArray();
        return Ok("Известные сведения загружены.", new Dictionary<string, object> { { "features", known } });
    }

    public ResponseEnvelope WorldPlayerMapDistancePreview0218(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var map = RequireMap0218(PayloadReader.GetString(payload, "mapId"));
        if (!CanPlayerKnowMap0218(map, actor.Id)) throw new UnauthorizedAccessException("Карта недоступна.");
        var first = VisibleFeatureForDistance0218(actor.Id, PayloadReader.GetString(payload, "fromFeatureId"));
        var second = VisibleFeatureForDistance0218(actor.Id, PayloadReader.GetString(payload, "toFeatureId"));
        var scale = _mongo.MapScaleProfiles0218.Find(x => x.Id == map.ScaleProfileId).FirstOrDefault();
        if (scale == null || !scale.SupportsExactDistance || scale.Kind == MapScaleKindIds0218.Schematic || scale.Kind == MapScaleKindIds0218.Abstract)
            return Ok("Физическое расстояние для этой карты не определено.", new Dictionary<string, object> { { "precision", "unknown" }, { "display", "Расстояние неизвестно" } });
        var firstPoint = VisiblePoint0218(first.feature, first.knowledge);
        var secondPoint = VisiblePoint0218(second.feature, second.knowledge);
        var metres = MapDistance0218.EuclideanMetres(firstPoint, secondPoint, scale);
        var approximate = Precision0218(first.knowledge) != MapDiscoveryPrecisionIds0218.Exact || Precision0218(second.knowledge) != MapDiscoveryPrecisionIds0218.Exact;
        return Ok("Расстояние рассчитано.", new Dictionary<string, object>
        {
            { "precision", approximate ? "estimate" : "exact" },
            { "metres", metres },
            { "display", approximate ? $"примерно {metres / 1000d:0.#} км" : $"{metres / 1000d:0.##} км" }
        });
    }

    private Dictionary<string, object> AdminMapProjection0218(MapCanvasState map)
    {
        var features = _mongo.MapSemanticFeatures0218.Find(x => x.MapId == map.Id && !x.IsArchived).Limit(MaxActivePrimitives0218).ToList();
        var layers = _mongo.MapSemanticLayers0218.Find(x => x.MapId == map.Id && !x.IsArchived).SortBy(x => x.SortOrder).ToList();
        var portals = _mongo.MapPortals0218.Find(x => x.SourceMapId == map.Id && !x.IsArchived).ToList();
        var result = AdminMapSummary0218(map);
        result["features"] = features.Select(AdminFeaturePayload0218).Cast<object>().ToArray();
        result["layers"] = layers.Select(LayerPayload0218).Cast<object>().ToArray();
        result["portals"] = portals.Select(AdminPortalPayload0218).Cast<object>().ToArray();
        result["primitiveLimit"] = MaxActivePrimitives0218;
        result["culled"] = _mongo.MapSemanticFeatures0218.CountDocuments(x => x.MapId == map.Id && !x.IsArchived) > MaxActivePrimitives0218;
        return result;
    }

    private Dictionary<string, object> PlayerMapProjection0218(MapCanvasState map, string ownerUserId, string characterId)
    {
        var features = _mongo.MapSemanticFeatures0218.Find(x => x.MapId == map.Id && !x.IsArchived).Limit(MaxActivePrimitives0218).ToList();
        var visible = features.Select(feature => new { feature, knowledge = KnowledgeFor0218(ownerUserId, feature.Id, characterId) })
            .Where(item => FeatureVisible0218(item.feature, item.knowledge)).ToList();
        var layers = _mongo.MapSemanticLayers0218.Find(x => x.MapId == map.Id && !x.IsArchived && x.IsVisibleToPlayers).SortBy(x => x.SortOrder).ToList();
        var portals = VisiblePortals0218(map.Id, ownerUserId).Where(portal => CanPlayerKnowMap0218(RequireMap0218(portal.TargetMapId), ownerUserId)).ToList();
        return new Dictionary<string, object>
        {
            { "map", PlayerMapSummary0218(map) },
            { "features", visible.Select(item => PlayerFeaturePayload0218(item.feature, item.knowledge)).Cast<object>().ToArray() },
            { "layers", layers.Select(PlayerLayerPayload0218).Cast<object>().ToArray() },
            { "portals", portals.Select(PlayerPortalPayload0218).Cast<object>().ToArray() },
            { "weatherBadge", new Dictionary<string, object> { { "label", "Погода по текущему наблюдению" }, { "source", "environment_projection" }, { "persistedOnMap", false } } },
            { "travelAuthority", "TravelSession" },
            { "tokenCoordinateAuthority", "map_tokens" },
            { "builtAtUtc", DateTime.UtcNow }
        };
    }

    private MapCanvasState RequireMap0218(string mapId)
    {
        mapId = RequireLength(mapId, 1, 128, "mapId");
        return _mongo.MapCanvases.Find(x => x.Id == mapId && !x.IsArchived).FirstOrDefault()
            ?? throw new InvalidOperationException("Карта не найдена.");
    }

    private bool CanPlayerKnowMap0218(MapCanvasState map, string ownerUserId)
    {
        if (map.VisibilityMode == MapVisibilityModes.Public || map.VisibilityMode == MapVisibilityModes.Party) return true;
        return KnowledgeFor0218(ownerUserId, map.Id) != null;
    }

    private EntityKnowledgeState? KnowledgeFor0218(string ownerUserId, string entityId, string characterId = "")
    {
        var candidates = _mongo.EntityKnowledgeStates.Find(x => x.OwnerUserId == ownerUserId && x.EntityType == "map_geography" && x.EntityId == entityId && x.IsApplied && x.IsPlayerVisible && !x.IsArchived).ToList();
        if (string.IsNullOrWhiteSpace(characterId))
            return candidates.Where(item => !item.ExtraData.TryGetValue("characterId", out var raw) || string.IsNullOrWhiteSpace(Convert.ToString(raw)))
                .OrderByDescending(item => item.UpdatedAtUtc).FirstOrDefault();
        return candidates.FirstOrDefault(item => !item.ExtraData.TryGetValue("characterId", out var raw) || string.IsNullOrWhiteSpace(Convert.ToString(raw)) || string.Equals(Convert.ToString(raw), characterId, StringComparison.Ordinal));
    }

    private static bool FeatureVisible0218(MapSemanticFeatureState0218 feature, EntityKnowledgeState? knowledge)
        => !feature.IsArchived && ((!feature.IsSecret && feature.IsPlayerVisible)
            || (knowledge != null && Precision0218(knowledge) != MapDiscoveryPrecisionIds0218.Hidden));

    private List<MapPortalState0218> VisiblePortals0218(string mapId, string ownerUserId)
    {
        var filter = Builders<MapPortalState0218>.Filter.Eq(x => x.IsArchived, false);
        if (!string.IsNullOrWhiteSpace(mapId)) filter &= Builders<MapPortalState0218>.Filter.Eq(x => x.SourceMapId, mapId);
        return _mongo.MapPortals0218.Find(filter).Limit(500).ToList()
            .Where(portal => (!portal.IsSecret && portal.IsPlayerVisible) || KnowledgeFor0218(ownerUserId, portal.Id) != null).ToList();
    }

    private (MapSemanticFeatureState0218 feature, EntityKnowledgeState? knowledge) VisibleFeatureForDistance0218(string ownerUserId, string featureId)
    {
        var feature = _mongo.MapSemanticFeatures0218.Find(x => x.Id == featureId && !x.IsArchived).FirstOrDefault()
            ?? throw new InvalidOperationException("Точка измерения не найдена.");
        var knowledge = KnowledgeFor0218(ownerUserId, featureId);
        if (!FeatureVisible0218(feature, knowledge)) throw new UnauthorizedAccessException("Точка измерения не известна персонажу.");
        if (feature.Points.Count == 0) throw new ArgumentException("У точки измерения нет координат.");
        return (feature, knowledge);
    }

    private static MapPoint0218 VisiblePoint0218(MapSemanticFeatureState0218 feature, EntityKnowledgeState? knowledge)
    {
        if (Precision0218(knowledge) != MapDiscoveryPrecisionIds0218.Approximate) return feature.Points[0];
        return new MapPoint0218 { X = ExtraDouble0218(knowledge!, "approximateX"), Y = ExtraDouble0218(knowledge!, "approximateY") };
    }

    private static string Precision0218(EntityKnowledgeState? knowledge)
        => knowledge != null && knowledge.ExtraData.TryGetValue("precision", out var value) ? Convert.ToString(value) ?? MapDiscoveryPrecisionIds0218.Exact : MapDiscoveryPrecisionIds0218.Exact;

    private static double ExtraDouble0218(EntityKnowledgeState knowledge, string key)
        => knowledge.ExtraData.TryGetValue(key, out var value) && double.TryParse(Convert.ToString(value), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0d;

    private static Dictionary<string, object> AdminMapSummary0218(MapCanvasState map) => new Dictionary<string, object>
    {
        { "mapId", map.Id }, { "name", map.Name }, { "description", map.Description }, { "mapType", map.MapType },
        { "worldId", map.WorldId }, { "primaryWorldEntityId", map.PrimaryBoundWorldEntityId },
        { "boundWorldEntityIds", map.BoundWorldEntityIds.Cast<object>().ToArray() }, { "parentMapId", map.ParentMapId },
        { "coordinateProfileId", map.CoordinateProfileId }, { "scaleProfileId", map.ScaleProfileId },
        { "coordinateProfileKind", SafeCoordinateLabel0218(map.CoordinateMode) },
        { "widthMeters", map.WidthMeters }, { "heightMeters", map.HeightMeters }, { "visibilityMode", map.VisibilityMode },
        { "revision", map.EntityRevision }, { "updatedAtUtc", map.UpdatedAtUtc }
    };

    private static Dictionary<string, object> PlayerMapSummary0218(MapCanvasState map) => new Dictionary<string, object>
    {
        { "mapId", map.Id }, { "name", map.Name }, { "description", map.Description }, { "mapType", map.MapType },
        { "parentMapId", map.ParentMapId }, { "coordinateProfileKind", SafeCoordinateLabel0218(map.CoordinateMode) },
        { "widthMeters", map.WidthMeters }, { "heightMeters", map.HeightMeters }, { "updatedAtUtc", map.UpdatedAtUtc }
    };

    private static Dictionary<string, object> NodePayload0218(MapSpaceNodeState node) => new Dictionary<string, object>
    {
        { "nodeId", node.Id }, { "parentId", node.ParentId }, { "name", node.Name }, { "nodeType", node.NodeType }, { "sortOrder", node.SortOrder }
    };

    private static Dictionary<string, object> LayerPayload0218(MapSemanticLayerState0218 layer) => new Dictionary<string, object>
    {
        { "layerId", layer.Id }, { "name", layer.Name }, { "layerKind", layer.LayerKind }, { "sortOrder", layer.SortOrder },
        { "visibleToPlayers", layer.IsVisibleToPlayers }, { "isLocked", layer.IsLocked }, { "revision", layer.Revision }
    };

    private static Dictionary<string, object> PlayerLayerPayload0218(MapSemanticLayerState0218 layer) => new Dictionary<string, object>
    {
        { "layerId", layer.Id }, { "name", layer.Name }, { "layerKind", layer.LayerKind }, { "sortOrder", layer.SortOrder }
    };

    private static Dictionary<string, object> AdminFeaturePayload0218(MapSemanticFeatureState0218 feature) => new Dictionary<string, object>
    {
        { "featureId", feature.Id }, { "mapId", feature.MapId }, { "layerId", feature.LayerId }, { "name", feature.Name },
        { "semanticKind", feature.SemanticKind }, { "geometryKind", feature.GeometryKind }, { "points", PointPayloads0218(feature.Points) },
        { "boundWorldEntityId", feature.BoundWorldEntityId }, { "isPlayerVisible", feature.IsPlayerVisible }, { "isSecret", feature.IsSecret },
        { "isManual", feature.IsManual }, { "generationIdentity", feature.GenerationIdentity }, { "publicDescription", feature.PublicDescription },
        { "gmNotes", feature.GMNotes }, { "styleKey", feature.StyleKey }, { "revision", feature.Revision }
    };

    private static Dictionary<string, object> PlayerFeaturePayload0218(MapSemanticFeatureState0218 feature, EntityKnowledgeState? knowledge)
    {
        var precision = Precision0218(knowledge);
        var points = precision == MapDiscoveryPrecisionIds0218.Approximate && knowledge != null
            ? new List<MapPoint0218> { VisiblePoint0218(feature, knowledge) }
            : feature.Points;
        return new Dictionary<string, object>
        {
            { "featureId", feature.Id }, { "name", feature.Name }, { "semanticKind", feature.SemanticKind },
            { "geometryKind", feature.GeometryKind }, { "points", PointPayloads0218(points) },
            { "publicDescription", feature.PublicDescription }, { "styleKey", feature.StyleKey }, { "precision", precision }
        };
    }

    private static object[] PointPayloads0218(IEnumerable<MapPoint0218> points)
        => points.Select(point => (object)new Dictionary<string, object> { { "x", point.X }, { "y", point.Y } }).ToArray();

    private static Dictionary<string, object> AdminPortalPayload0218(MapPortalState0218 portal) => new Dictionary<string, object>
    {
        { "portalId", portal.Id }, { "name", portal.Name }, { "sourceMapId", portal.SourceMapId }, { "targetMapId", portal.TargetMapId },
        { "sourceFeatureId", portal.SourceFeatureId }, { "targetFeatureId", portal.TargetFeatureId },
        { "isPlayerVisible", portal.IsPlayerVisible }, { "isSecret", portal.IsSecret }, { "revision", portal.Revision }
    };

    private static Dictionary<string, object> PlayerPortalPayload0218(MapPortalState0218 portal) => new Dictionary<string, object>
    {
        { "portalId", portal.Id }, { "name", portal.Name }, { "targetMapId", portal.TargetMapId }, { "sourceFeatureId", portal.SourceFeatureId }
    };

    private MapSemanticFeatureState0218 ReadFeature0218(IDictionary<string, object> payload, MapSemanticFeatureState0218? existing)
    {
        var feature = existing ?? new MapSemanticFeatureState0218();
        feature.CampaignId = First0218(PayloadReader.GetString(payload, "campaignId"), string.IsNullOrWhiteSpace(feature.CampaignId) ? Campaign0218 : feature.CampaignId);
        feature.MapId = First0218(PayloadReader.GetString(payload, "mapId"), feature.MapId);
        feature.LayerId = First0218(PayloadReader.GetString(payload, "layerId"), feature.LayerId);
        feature.Name = First0218(PayloadReader.GetString(payload, "name"), feature.Name);
        if (string.IsNullOrWhiteSpace(feature.Name)) throw new ArgumentException("Название объекта обязательно.");
        feature.SemanticKind = First0218(PayloadReader.GetString(payload, "semanticKind"), feature.SemanticKind);
        feature.GeometryKind = First0218(PayloadReader.GetString(payload, "geometryKind"), feature.GeometryKind);
        var points = ReadPoints0218(payload, "points");
        if (points.Count == 0 && payload.ContainsKey("x") && payload.ContainsKey("y"))
            points.Add(new MapPoint0218 { X = PayloadReader.GetDouble(payload, "x") ?? 0d, Y = PayloadReader.GetDouble(payload, "y") ?? 0d });
        if (points.Count > 0) feature.Points = points;
        if (feature.Points.Count == 0) throw new ArgumentException("Укажите координаты объекта.");
        feature.BoundWorldEntityId = First0218(PayloadReader.GetString(payload, "boundWorldEntityId"), feature.BoundWorldEntityId);
        if (payload.ContainsKey("isPlayerVisible")) feature.IsPlayerVisible = PayloadReader.GetBool(payload, "isPlayerVisible");
        if (payload.ContainsKey("isSecret")) feature.IsSecret = PayloadReader.GetBool(payload, "isSecret");
        if (payload.ContainsKey("publicDescription")) feature.PublicDescription = RequireLength(PayloadReader.GetString(payload, "publicDescription"), 0, 2048, "publicDescription");
        if (payload.ContainsKey("gmNotes")) feature.GMNotes = RequireLength(PayloadReader.GetString(payload, "gmNotes"), 0, 4096, "gmNotes");
        feature.StyleKey = First0218(PayloadReader.GetString(payload, "styleKey"), feature.StyleKey);
        return feature;
    }

    private MapPortalState0218 ReadPortal0218(IDictionary<string, object> payload, MapPortalState0218? existing)
    {
        var portal = existing ?? new MapPortalState0218();
        portal.CampaignId = First0218(PayloadReader.GetString(payload, "campaignId"), string.IsNullOrWhiteSpace(portal.CampaignId) ? Campaign0218 : portal.CampaignId);
        portal.SourceMapId = First0218(PayloadReader.GetString(payload, "sourceMapId"), portal.SourceMapId);
        portal.TargetMapId = First0218(PayloadReader.GetString(payload, "targetMapId"), portal.TargetMapId);
        portal.SourceFeatureId = First0218(PayloadReader.GetString(payload, "sourceFeatureId"), portal.SourceFeatureId);
        portal.TargetFeatureId = First0218(PayloadReader.GetString(payload, "targetFeatureId"), portal.TargetFeatureId);
        portal.Name = First0218(PayloadReader.GetString(payload, "name"), portal.Name);
        if (payload.ContainsKey("isPlayerVisible")) portal.IsPlayerVisible = PayloadReader.GetBool(payload, "isPlayerVisible");
        if (payload.ContainsKey("isSecret")) portal.IsSecret = PayloadReader.GetBool(payload, "isSecret");
        return portal;
    }

    private void ValidatePortal0218(MapPortalState0218 portal)
    {
        if (string.IsNullOrWhiteSpace(portal.Name)) throw new ArgumentException("Название перехода обязательно.");
        var source = RequireMap0218(portal.SourceMapId);
        var target = RequireMap0218(portal.TargetMapId);
        if (source.CampaignId != target.CampaignId) throw new ArgumentException("Карты перехода должны принадлежать одной кампании.");
        if (source.Id == target.Id) throw new ArgumentException("Переход должен вести на другую карту.");
    }

    private void ValidateFeatureLayer0218(MapSemanticFeatureState0218 feature)
    {
        var layer = _mongo.MapSemanticLayers0218.Find(x => x.Id == feature.LayerId && x.MapId == feature.MapId && !x.IsArchived).FirstOrDefault()
            ?? throw new ArgumentException("Выбранный слой не принадлежит этой карте.");
        if (layer.IsLocked) throw new InvalidOperationException("Слой заблокирован для редактирования.");
    }

    private static List<MapPoint0218> ReadPoints0218(IDictionary<string, object> payload, string key)
    {
        var result = new List<MapPoint0218>();
        foreach (var raw in ReadObjectList0218(payload, key))
        {
            var wrapped = new Dictionary<string, object> { { "point", raw } };
            var point = PayloadReader.GetDictionary(wrapped, "point");
            if (point != null)
                result.Add(new MapPoint0218 { X = PayloadReader.GetDouble(point, "x") ?? 0d, Y = PayloadReader.GetDouble(point, "y") ?? 0d });
        }
        return result;
    }

    private static List<string> ReadStringList0218(IDictionary<string, object> payload, string key)
        => ReadObjectList0218(payload, key).Select(Convert.ToString).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!).Distinct(StringComparer.Ordinal).ToList();

    private static IList<object> ReadObjectList0218(IDictionary<string, object> payload, string key)
    {
        var known = PayloadReader.GetList(payload, key);
        if (known != null) return known;
        if (!payload.TryGetValue(key, out var raw) || raw == null || raw is string) return Array.Empty<object>();
        if (raw is IEnumerable enumerable) return enumerable.Cast<object>().ToList();
        return Array.Empty<object>();
    }

    private static List<string> ValidateMap0218(MapCanvasState map)
    {
        var findings = new List<string>();
        if (string.IsNullOrWhiteSpace(map.PrimaryBoundWorldEntityId)) findings.Add("Не выбран основной узел мира.");
        if (string.IsNullOrWhiteSpace(map.CoordinateProfileId)) findings.Add("Не выбран профиль координат.");
        if (string.IsNullOrWhiteSpace(map.ScaleProfileId)) findings.Add("Не выбран профиль масштаба.");
        if (map.WidthMeters <= 0 || map.HeightMeters <= 0) findings.Add("Размеры карты должны быть положительными.");
        return findings;
    }

    private static string NormalizeMapType0218(string value)
    {
        var allowed = new[] { MapTypeIds.World, MapTypeIds.Continent, MapTypeIds.Region, MapTypeIds.State, MapTypeIds.Settlement, MapTypeIds.District, MapTypeIds.Location, MapTypeIds.Interior, MapTypeIds.Dungeon, MapTypeIds.BattleScene, MapTypeIds.Galaxy, MapTypeIds.Sector, MapTypeIds.Subsector, MapTypeIds.StarSystem, MapTypeIds.Planet, MapTypeIds.Moon, MapTypeIds.Orbital, MapTypeIds.Custom };
        var normalized = string.IsNullOrWhiteSpace(value) ? MapTypeIds.Custom : value.Trim().ToLowerInvariant();
        if (!allowed.Contains(normalized, StringComparer.Ordinal)) throw new ArgumentException("Неподдерживаемый тип карты.");
        return normalized;
    }

    private static string NormalizePrecision0218(string value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? MapDiscoveryPrecisionIds0218.Exact : value.Trim().ToLowerInvariant();
        if (normalized != MapDiscoveryPrecisionIds0218.Exact && normalized != MapDiscoveryPrecisionIds0218.Approximate && normalized != MapDiscoveryPrecisionIds0218.Hidden)
            throw new ArgumentException("Неподдерживаемый профиль координат.");
        return normalized;
    }

    private static string SafeCoordinateLabel0218(string mode) => mode switch
    {
        MapCoordinateProfileKindIds0218.SchematicNodeSpace => "Схематическая карта",
        MapCoordinateProfileKindIds0218.HexGrid => "Гексагональная сетка",
        MapCoordinateProfileKindIds0218.SquareGrid => "Квадратная сетка",
        MapCoordinateProfileKindIds0218.LocalCartesian2D => "Локальные координаты",
        _ => "Географические координаты"
    };

    private static string First0218(string value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
}
