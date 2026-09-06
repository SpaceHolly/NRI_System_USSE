using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MongoDB.Bson;
using MongoDB.Driver;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;
using Nri.Shared.Utilities;

namespace Nri.Server.Application;

public partial class ServiceHub
{
    private const string QuestDefinitions0171Collection = "quest_definitions";
    private const string QuestInstances0171Collection = "quest_instances";
    private const string QuestObjectives0171Collection = "quest_objectives";
    private const string QuestRewardBundles0171Collection = "quest_reward_bundles";
    private const string QuestRewardGrants0171Collection = "quest_reward_grants";
    private const string QuestAudit0171Collection = "quest_audit_events";
    private bool _quest0171IndexesEnsured;

    public ResponseEnvelope QuestAdminListDefinitions0171(CommandContext context)
    {
        RequireAdmin(context);
        EnsureQuest0171Indexes();
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var campaignId = Quest0171CampaignId(payload);
        var includeArchived = PayloadReader.GetBool(payload, "includeArchived");
        var filter = Builders<BsonDocument>.Filter.Eq("CampaignId", campaignId);
        if (!includeArchived) filter &= Builders<BsonDocument>.Filter.Ne("IsArchived", true);
        var items = QuestDefinitions0171().Find(filter).Sort(Builders<BsonDocument>.Sort.Ascending("Name")).Limit(500).ToList()
            .Select(x => (object)QuestDefinition0171Payload(x, admin: true)).ToArray();
        return Ok("Quest definitions loaded.", new Dictionary<string, object> { ["items"] = items });
    }

    public ResponseEnvelope QuestAdminGetDefinition0171(CommandContext context)
    {
        RequireAdmin(context);
        EnsureQuest0171Indexes();
        var id = Quest0171RequiredId(context.Request.Payload, "definitionId");
        var definition = QuestDefinitions0171().Find(Quest0171IdFilter(id)).FirstOrDefault();
        if (definition == null || Quest0171Bool(definition, "IsArchived")) return Error("Quest definition not found.", ResponseStatus.NotFound, ErrorCode.NotFound);
        return Ok("Quest definition loaded.", new Dictionary<string, object> { ["item"] = QuestDefinition0171Payload(definition, admin: true) });
    }

    public ResponseEnvelope QuestAdminCreateDefinition0171(CommandContext context)
    {
        var actor = RequireAdmin(context);
        EnsureQuest0171Indexes();
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var now = DateTime.UtcNow;
        var definition = new BsonDocument
        {
            ["Id"] = Quest0171NewId("quest_def"),
            ["WorldId"] = Quest0171Text(payload, "worldId", string.Empty, 128),
            ["CampaignId"] = Quest0171CampaignId(payload),
            ["Name"] = Quest0171Text(payload, "name", "Новая задача", 240, required: true),
            ["ShortCode"] = Quest0171Text(payload, "shortCode", string.Empty, 64),
            ["Category"] = Quest0171Normalize(Quest0171Text(payload, "category", "Side", 64), Quest0171Categories(), "Side"),
            ["PublicDescription"] = Quest0171Text(payload, "publicDescription", Quest0171Text(payload, "description", string.Empty, 4096), 4096),
            ["GmDescription"] = Quest0171Text(payload, "gmDescription", string.Empty, 8192),
            ["ServerOnlyData"] = Quest0171Text(payload, "serverOnlyData", string.Empty, 8192),
            ["DefaultVisibility"] = Quest0171Normalize(Quest0171Text(payload, "defaultVisibility", "PlayerVisible", 64), Quest0171VisibilityModes(), "PlayerVisible"),
            ["Tags"] = Quest0171Array(payload, "tags"),
            ["SchemaVersion"] = 1,
            ["IsArchived"] = false,
            ["CreatedAtUtc"] = now,
            ["UpdatedAtUtc"] = now,
            ["CreatedByUserId"] = actor.Id,
            ["UpdatedByUserId"] = actor.Id
        };
        QuestDefinitions0171().InsertOne(definition);
        Quest0171Audit(actor, CommandNames.QuestAdminCreateDefinition, Quest0171String(definition, "Id"), "quest.definition.created", null, definition, "Quest definition created.");
        Quest0171Sync("quest.created", "quest_definition", Quest0171String(definition, "Id"), "created", actor.Id, context.Request.RequestId);
        return Ok("Quest definition created.", new Dictionary<string, object> { ["definitionId"] = Quest0171String(definition, "Id"), ["item"] = QuestDefinition0171Payload(definition, admin: true) });
    }

    public ResponseEnvelope QuestAdminUpdateDefinition0171(CommandContext context)
    {
        var actor = RequireAdmin(context);
        EnsureQuest0171Indexes();
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var definition = Quest0171RequireDefinition(payload);
        var before = new BsonDocument(definition);
        Quest0171SetIfPresent(definition, payload, "name", "Name", 1, 240);
        Quest0171SetIfPresent(definition, payload, "shortCode", "ShortCode", 0, 64);
        Quest0171SetIfPresent(definition, payload, "publicDescription", "PublicDescription", 0, 4096);
        Quest0171SetIfPresent(definition, payload, "gmDescription", "GmDescription", 0, 8192);
        if (payload.ContainsKey("category")) definition["Category"] = Quest0171Normalize(Quest0171Text(payload, "category", "Side", 64), Quest0171Categories(), "Side");
        if (payload.ContainsKey("defaultVisibility")) definition["DefaultVisibility"] = Quest0171Normalize(Quest0171Text(payload, "defaultVisibility", "PlayerVisible", 64), Quest0171VisibilityModes(), "PlayerVisible");
        if (payload.ContainsKey("tags")) definition["Tags"] = Quest0171Array(payload, "tags");
        Quest0171Touch(definition, actor.Id);
        QuestDefinitions0171().ReplaceOne(Quest0171IdFilter(Quest0171String(definition, "Id")), definition);
        Quest0171Audit(actor, CommandNames.QuestAdminUpdateDefinition, Quest0171String(definition, "Id"), "quest.definition.updated", before, definition, "Quest definition updated.");
        Quest0171Sync("quest.updated", "quest_definition", Quest0171String(definition, "Id"), "updated", actor.Id, context.Request.RequestId);
        return Ok("Quest definition updated.", new Dictionary<string, object> { ["item"] = QuestDefinition0171Payload(definition, admin: true) });
    }

    public ResponseEnvelope QuestAdminArchiveDefinition0171(CommandContext context)
    {
        var actor = RequireAdmin(context);
        EnsureQuest0171Indexes();
        var definition = Quest0171RequireDefinition(context.Request.Payload ?? new Dictionary<string, object>());
        var before = new BsonDocument(definition);
        definition["IsArchived"] = true;
        Quest0171Touch(definition, actor.Id);
        QuestDefinitions0171().ReplaceOne(Quest0171IdFilter(Quest0171String(definition, "Id")), definition);
        Quest0171Audit(actor, CommandNames.QuestAdminArchiveDefinition, Quest0171String(definition, "Id"), "quest.definition.archived", before, definition, "Quest definition archived.");
        Quest0171Sync("quest.updated", "quest_definition", Quest0171String(definition, "Id"), "archived", actor.Id, context.Request.RequestId);
        return Ok("Quest definition archived.", new Dictionary<string, object> { ["definitionId"] = Quest0171String(definition, "Id") });
    }

    public ResponseEnvelope QuestAdminListForCampaign0171(CommandContext context) => QuestAdminListInstances0171(context, sessionOnly: false);
    public ResponseEnvelope QuestAdminListForSession0171(CommandContext context) => QuestAdminListInstances0171(context, sessionOnly: true);

    public ResponseEnvelope QuestAdminGet0171(CommandContext context)
    {
        RequireAdmin(context);
        EnsureQuest0171Indexes();
        var quest = Quest0171RequireInstance(context.Request.Payload ?? new Dictionary<string, object>());
        return Ok("Quest loaded.", Quest0171AdminEnvelope(quest));
    }

    public ResponseEnvelope QuestAdminCreate0171(CommandContext context)
    {
        var actor = RequireAdmin(context);
        EnsureQuest0171Indexes();
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var now = DateTime.UtcNow;
        var definitionId = Quest0171Text(payload, "definitionId", string.Empty, 128);
        var definition = string.IsNullOrWhiteSpace(definitionId) ? null : QuestDefinitions0171().Find(Quest0171IdFilter(definitionId)).FirstOrDefault();
        var status = Quest0171Normalize(Quest0171Text(payload, "status", "Draft", 64), Quest0171Statuses(), "Draft");
        var visibilityDefault = definition == null ? "PlayerVisible" : Quest0171String(definition, "DefaultVisibility", "PlayerVisible");
        var quest = new BsonDocument
        {
            ["Id"] = Quest0171NewId("quest"),
            ["DefinitionId"] = definitionId,
            ["WorldId"] = Quest0171Text(payload, "worldId", definition == null ? string.Empty : Quest0171String(definition, "WorldId"), 128),
            ["CampaignId"] = Quest0171CampaignId(payload, definition),
            ["SessionId"] = Quest0171Text(payload, "sessionId", string.Empty, 128),
            ["SceneId"] = Quest0171Text(payload, "sceneId", string.Empty, 128),
            ["LocationId"] = Quest0171Text(payload, "locationId", string.Empty, 128),
            ["AssignedPartyId"] = Quest0171Text(payload, "assignedPartyId", string.Empty, 128),
            ["AssignedCharacterIds"] = Quest0171Array(payload, "assignedCharacterIds"),
            ["AssignedPlayerUserIds"] = Quest0171Array(payload, "assignedPlayerUserIds"),
            ["OwnerGmUserId"] = actor.Id,
            ["Status"] = status,
            ["Visibility"] = Quest0171Normalize(Quest0171Text(payload, "visibility", visibilityDefault, 64), Quest0171VisibilityModes(), visibilityDefault),
            ["PlayerTitle"] = Quest0171Text(payload, "playerTitle", Quest0171Text(payload, "name", definition == null ? "Новая задача" : Quest0171String(definition, "Name"), 240), 240, required: true),
            ["GmTitle"] = Quest0171Text(payload, "gmTitle", string.Empty, 240),
            ["PlayerSummary"] = Quest0171Text(payload, "playerSummary", definition == null ? string.Empty : Quest0171String(definition, "PublicDescription"), 4096),
            ["GmSummary"] = Quest0171Text(payload, "gmSummary", string.Empty, 8192),
            ["PlayerKnownDetails"] = Quest0171Text(payload, "playerKnownDetails", string.Empty, 4096),
            ["GmNotes"] = Quest0171Text(payload, "gmNotes", string.Empty, 8192),
            ["ServerOnlyData"] = Quest0171Text(payload, "serverOnlyData", string.Empty, 8192),
            ["StartedAtWorldDate"] = Quest0171Text(payload, "startedAtWorldDate", string.Empty, 128),
            ["CompletedAtWorldDate"] = string.Empty,
            ["StartedAtUtc"] = status == "Active" ? now : BsonNull.Value,
            ["CompletedAtUtc"] = BsonNull.Value,
            ["RewardBundleId"] = string.Empty,
            ["AutoCompleteWhenObjectivesDone"] = PayloadReader.GetBool(payload, "autoCompleteWhenObjectivesDone"),
            ["SchemaVersion"] = 1,
            ["IsArchived"] = false,
            ["CreatedAtUtc"] = now,
            ["UpdatedAtUtc"] = now,
            ["CreatedByUserId"] = actor.Id,
            ["UpdatedByUserId"] = actor.Id
        };
        QuestInstances0171().InsertOne(quest);
        Quest0171Audit(actor, CommandNames.QuestAdminCreate, Quest0171String(quest, "Id"), "quest.created", null, quest, "Quest instance created.");
        Quest0171Sync("quest.created", "quest_instance", Quest0171String(quest, "Id"), "created", actor.Id, context.Request.RequestId);
        return Ok("Quest created.", Quest0171AdminEnvelope(quest));
    }

    public ResponseEnvelope QuestAdminUpdate0171(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var quest = Quest0171RequireInstance(payload);
        var before = new BsonDocument(quest);
        Quest0171SetIfPresent(quest, payload, "playerTitle", "PlayerTitle", 1, 240);
        Quest0171SetIfPresent(quest, payload, "gmTitle", "GmTitle", 0, 240);
        Quest0171SetIfPresent(quest, payload, "playerSummary", "PlayerSummary", 0, 4096);
        Quest0171SetIfPresent(quest, payload, "gmSummary", "GmSummary", 0, 8192);
        Quest0171SetIfPresent(quest, payload, "playerKnownDetails", "PlayerKnownDetails", 0, 4096);
        Quest0171SetIfPresent(quest, payload, "gmNotes", "GmNotes", 0, 8192);
        Quest0171SetIfPresent(quest, payload, "sessionId", "SessionId", 0, 128);
        Quest0171SetIfPresent(quest, payload, "sceneId", "SceneId", 0, 128);
        Quest0171SetIfPresent(quest, payload, "locationId", "LocationId", 0, 128);
        Quest0171Touch(quest, actor.Id);
        QuestInstances0171().ReplaceOne(Quest0171IdFilter(Quest0171String(quest, "Id")), quest);
        Quest0171Audit(actor, CommandNames.QuestAdminUpdate, Quest0171String(quest, "Id"), "quest.updated", before, quest, "Quest updated.");
        Quest0171Sync("quest.updated", "quest_instance", Quest0171String(quest, "Id"), "updated", actor.Id, context.Request.RequestId);
        return Ok("Quest updated.", Quest0171AdminEnvelope(quest));
    }

    public ResponseEnvelope QuestAdminArchive0171(CommandContext context) => Quest0171SetArchived(context, true);
    public ResponseEnvelope QuestAdminAssign0171(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var quest = Quest0171RequireInstance(payload);
        var before = new BsonDocument(quest);
        quest["AssignedPartyId"] = Quest0171Text(payload, "assignedPartyId", Quest0171String(quest, "AssignedPartyId"), 128);
        if (payload.ContainsKey("assignedCharacterIds")) quest["AssignedCharacterIds"] = Quest0171Array(payload, "assignedCharacterIds");
        if (payload.ContainsKey("assignedPlayerUserIds")) quest["AssignedPlayerUserIds"] = Quest0171Array(payload, "assignedPlayerUserIds");
        Quest0171Touch(quest, actor.Id);
        QuestInstances0171().ReplaceOne(Quest0171IdFilter(Quest0171String(quest, "Id")), quest);
        Quest0171Audit(actor, CommandNames.QuestAdminAssign, Quest0171String(quest, "Id"), "quest.assigned", before, quest, "Quest assignment changed.");
        Quest0171Sync("quest.assigned", "quest_instance", Quest0171String(quest, "Id"), "assigned", actor.Id, context.Request.RequestId);
        return Ok("Quest assigned.", Quest0171AdminEnvelope(quest));
    }

    public ResponseEnvelope QuestAdminSetVisibility0171(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var quest = Quest0171RequireInstance(payload);
        var before = new BsonDocument(quest);
        quest["Visibility"] = Quest0171Normalize(Quest0171Text(payload, "visibility", Quest0171String(quest, "Visibility"), 64), Quest0171VisibilityModes(), "PlayerVisible");
        Quest0171Touch(quest, actor.Id);
        QuestInstances0171().ReplaceOne(Quest0171IdFilter(Quest0171String(quest, "Id")), quest);
        Quest0171Audit(actor, CommandNames.QuestAdminSetVisibility, Quest0171String(quest, "Id"), "quest.visibility.changed", before, quest, "Quest visibility changed.");
        Quest0171Sync("quest.visibility.changed", "quest_instance", Quest0171String(quest, "Id"), "visibility", actor.Id, context.Request.RequestId);
        return Ok("Quest visibility changed.", Quest0171AdminEnvelope(quest));
    }

    public ResponseEnvelope QuestAdminAddObjective0171(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var quest = Quest0171RequireInstance(payload);
        var now = DateTime.UtcNow;
        var objective = new BsonDocument
        {
            ["Id"] = Quest0171NewId("quest_obj"),
            ["QuestInstanceId"] = Quest0171String(quest, "Id"),
            ["Order"] = PayloadReader.GetInt(payload, "order") ?? QuestObjectives0171().Find(Builders<BsonDocument>.Filter.Eq("QuestInstanceId", Quest0171String(quest, "Id"))).ToList().Count + 1,
            ["Title"] = Quest0171Text(payload, "title", "Цель задачи", 240, required: true),
            ["PlayerText"] = Quest0171Text(payload, "playerText", Quest0171Text(payload, "description", string.Empty, 4096), 4096),
            ["GmText"] = Quest0171Text(payload, "gmText", string.Empty, 8192),
            ["ServerOnlyData"] = Quest0171Text(payload, "serverOnlyData", string.Empty, 8192),
            ["ObjectiveType"] = Quest0171Normalize(Quest0171Text(payload, "objectiveType", "Manual", 64), Quest0171ObjectiveTypes(), "Manual"),
            ["Status"] = Quest0171Normalize(Quest0171Text(payload, "status", "Visible", 64), Quest0171ObjectiveStatuses(), "Visible"),
            ["ProgressCurrent"] = PayloadReader.GetInt(payload, "progressCurrent") ?? 0,
            ["ProgressTarget"] = Math.Max(1, PayloadReader.GetInt(payload, "progressTarget") ?? 1),
            ["RelatedCharacterIds"] = Quest0171Array(payload, "relatedCharacterIds"),
            ["RelatedNpcIds"] = Quest0171Array(payload, "relatedNpcIds"),
            ["RelatedFactionIds"] = Quest0171Array(payload, "relatedFactionIds"),
            ["RelatedLocationIds"] = Quest0171Array(payload, "relatedLocationIds"),
            ["RelatedSceneMapIds"] = Quest0171Array(payload, "relatedSceneMapIds"),
            ["RelatedWorldMapMarkerIds"] = Quest0171Array(payload, "relatedWorldMapMarkerIds"),
            ["RelatedCombatSessionIds"] = Quest0171Array(payload, "relatedCombatSessionIds"),
            ["Visibility"] = Quest0171Normalize(Quest0171Text(payload, "visibility", "PlayerVisible", 64), Quest0171VisibilityModes(), "PlayerVisible"),
            ["SchemaVersion"] = 1,
            ["IsArchived"] = false,
            ["CreatedAtUtc"] = now,
            ["UpdatedAtUtc"] = now,
            ["CreatedByUserId"] = actor.Id,
            ["UpdatedByUserId"] = actor.Id
        };
        QuestObjectives0171().InsertOne(objective);
        Quest0171TouchAndSaveQuest(quest, actor.Id);
        Quest0171Audit(actor, CommandNames.QuestAdminAddObjective, Quest0171String(quest, "Id"), "quest.objective.added", null, objective, "Quest objective added.");
        Quest0171Sync("quest.objective.updated", "quest_instance", Quest0171String(quest, "Id"), "objective.added", actor.Id, context.Request.RequestId);
        var response = Quest0171AdminEnvelope(quest);
        response["objectiveId"] = Quest0171String(objective, "Id");
        response["objective"] = QuestObjective0171Payload(objective, admin: true);
        return Ok("Quest objective added.", response);
    }

    public ResponseEnvelope QuestAdminUpdateObjective0171(CommandContext context) => Quest0171UpdateObjective(context, "quest.objective.updated");
    public ResponseEnvelope QuestAdminSetObjectiveStatus0171(CommandContext context) => Quest0171UpdateObjective(context, "quest.objective.status.changed");
    public ResponseEnvelope QuestAdminSetObjectiveProgress0171(CommandContext context) => Quest0171UpdateObjective(context, "quest.objective.progress.changed");

    public ResponseEnvelope QuestAdminReorderObjectives0171(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var quest = Quest0171RequireInstance(payload);
        var list = PayloadReader.GetList(payload, "objectiveOrders") ?? new List<object>();
        foreach (var item in list)
        {
            var map = Quest0171ObjectMap(item);
            var objectiveId = Quest0171Text(map, "objectiveId", Quest0171Text(map, "id", string.Empty, 128), 128);
            var order = PayloadReader.GetInt(map, "order");
            if (string.IsNullOrWhiteSpace(objectiveId) || !order.HasValue) continue;
            QuestObjectives0171().UpdateOne(
                Quest0171IdFilter(objectiveId) & Builders<BsonDocument>.Filter.Eq("QuestInstanceId", Quest0171String(quest, "Id")),
                Builders<BsonDocument>.Update.Set("Order", order.Value).Set("UpdatedAtUtc", DateTime.UtcNow).Set("UpdatedByUserId", actor.Id));
        }
        Quest0171TouchAndSaveQuest(quest, actor.Id);
        Quest0171Audit(actor, CommandNames.QuestAdminReorderObjectives, Quest0171String(quest, "Id"), "quest.objectives.reordered", null, quest, "Quest objectives reordered.");
        Quest0171Sync("quest.objective.updated", "quest_instance", Quest0171String(quest, "Id"), "objective.reordered", actor.Id, context.Request.RequestId);
        return Ok("Quest objectives reordered.", Quest0171AdminEnvelope(quest));
    }

    public ResponseEnvelope QuestAdminSetStatus0171(CommandContext context)
    {
        var status = Quest0171Normalize(Quest0171Text(context.Request.Payload ?? new Dictionary<string, object>(), "status", "Active", 64), Quest0171Statuses(), "Active");
        return Quest0171SetStatus(context, status, "quest.status.changed");
    }

    public ResponseEnvelope QuestAdminComplete0171(CommandContext context) => Quest0171SetStatus(context, "Completed", "quest.completed");
    public ResponseEnvelope QuestAdminFail0171(CommandContext context) => Quest0171SetStatus(context, "Failed", "quest.failed");
    public ResponseEnvelope QuestAdminCancel0171(CommandContext context) => Quest0171SetStatus(context, "Cancelled", "quest.status.changed");

    public ResponseEnvelope QuestAdminCreateRewardBundle0171(CommandContext context) => Quest0171UpsertRewardBundle(context, create: true);
    public ResponseEnvelope QuestAdminUpdateRewardBundle0171(CommandContext context) => Quest0171UpsertRewardBundle(context, create: false);

    public ResponseEnvelope QuestAdminPreviewRewards0171(CommandContext context)
    {
        RequireAdmin(context);
        var quest = Quest0171RequireInstance(context.Request.Payload ?? new Dictionary<string, object>());
        var bundle = Quest0171RewardBundleForQuest(Quest0171String(quest, "Id"));
        return Ok("Quest rewards preview loaded.", new Dictionary<string, object>
        {
            ["questId"] = Quest0171String(quest, "Id"),
            ["rewardBundle"] = bundle == null ? new Dictionary<string, object>() : QuestRewardBundle0171Payload(bundle, admin: true),
            ["summary"] = bundle == null ? "Награды ещё не заданы." : Quest0171RewardSummary(bundle, admin: true)
        });
    }

    public ResponseEnvelope QuestAdminCreateRewardGrant0171(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var quest = Quest0171RequireInstance(payload);
        var bundle = Quest0171RewardBundleForQuest(Quest0171String(quest, "Id"));
        if (bundle == null) return Error("Reward bundle not found.", ResponseStatus.NotFound, ErrorCode.NotFound);
        var now = DateTime.UtcNow;
        var grant = new BsonDocument
        {
            ["Id"] = Quest0171NewId("quest_grant"),
            ["QuestInstanceId"] = Quest0171String(quest, "Id"),
            ["RewardBundleId"] = Quest0171String(bundle, "Id"),
            ["TargetCharacterIds"] = Quest0171Array(payload, "targetCharacterIds", Quest0171ArrayValues(quest, "AssignedCharacterIds")),
            ["TargetPlayerUserIds"] = Quest0171Array(payload, "targetPlayerUserIds", Quest0171ArrayValues(quest, "AssignedPlayerUserIds")),
            ["Status"] = "PendingGmApply",
            ["AppliedAtUtc"] = BsonNull.Value,
            ["AppliedByUserId"] = string.Empty,
            ["PlayerVisibleSummary"] = Quest0171Text(payload, "playerVisibleSummary", Quest0171RewardSummary(bundle, admin: false), 4096),
            ["GmSummary"] = Quest0171Text(payload, "gmSummary", Quest0171RewardSummary(bundle, admin: true), 8192),
            ["GrantResults"] = new BsonArray(),
            ["SchemaVersion"] = 1,
            ["IsArchived"] = false,
            ["CreatedAtUtc"] = now,
            ["UpdatedAtUtc"] = now,
            ["CreatedByUserId"] = actor.Id,
            ["UpdatedByUserId"] = actor.Id
        };
        QuestRewardGrants0171().InsertOne(grant);
        Quest0171Audit(actor, CommandNames.QuestAdminCreateRewardGrant, Quest0171String(quest, "Id"), "quest.reward.grant.created", null, grant, "Quest reward grant created.");
        Quest0171Sync("quest.reward.grant.created", "quest_instance", Quest0171String(quest, "Id"), "reward.grant.created", actor.Id, context.Request.RequestId);
        return Ok("Quest reward grant created.", new Dictionary<string, object> { ["grant"] = QuestRewardGrant0171Payload(grant, admin: true) });
    }

    public ResponseEnvelope QuestAdminApplyRewardGrant0171(CommandContext context)
    {
        var actor = RequireAdmin(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var grantId = Quest0171RequiredId(payload, "grantId");
        var grant = QuestRewardGrants0171().Find(Quest0171IdFilter(grantId)).FirstOrDefault();
        if (grant == null || Quest0171Bool(grant, "IsArchived")) return Error("Reward grant not found.", ResponseStatus.NotFound, ErrorCode.NotFound);
        var updated = Gameplay0174ApplyQuestRewardGrant(actor, grant, context.Request.RequestId, manual: true);
        return Ok("Quest reward grant applied.", new Dictionary<string, object> { ["grant"] = QuestRewardGrant0171Payload(updated, admin: true) });
    }

    public ResponseEnvelope QuestAdminGetAudit0171(CommandContext context)
    {
        RequireAdmin(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var questId = Quest0171Text(payload, "questId", string.Empty, 128);
        var filter = string.IsNullOrWhiteSpace(questId)
            ? FilterDefinition<BsonDocument>.Empty
            : Builders<BsonDocument>.Filter.Eq("QuestInstanceId", questId);
        var items = QuestAudit0171().Find(filter).Sort(Builders<BsonDocument>.Sort.Descending("CreatedAtUtc")).Limit(200).ToList()
            .Select(x => (object)Quest0171DocPayload(x, admin: true)).ToArray();
        return Ok("Quest audit loaded.", new Dictionary<string, object> { ["items"] = items });
    }

    public ResponseEnvelope QuestPlayerListActive0171(CommandContext context) => Quest0171PlayerList(context, new[] { "Active" });
    public ResponseEnvelope QuestPlayerListAvailable0171(CommandContext context) => Quest0171PlayerList(context, new[] { "Available" });

    public ResponseEnvelope QuestPlayerGet0171(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var quest = Quest0171RequireInstance(payload);
        if (!Quest0171CanPlayerSeeQuest(actor, quest)) return Error("Quest not found.", ResponseStatus.NotFound, ErrorCode.NotFound);
        return Ok("Quest loaded.", new Dictionary<string, object> { ["item"] = QuestInstance0171Payload(quest, admin: false) });
    }

    public ResponseEnvelope QuestPlayerGetJournal0171(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        EnsureQuest0171Indexes();
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var campaignId = Quest0171CampaignId(payload);
        var filter = Builders<BsonDocument>.Filter.Eq("CampaignId", campaignId)
            & Builders<BsonDocument>.Filter.Ne("IsArchived", true);
        var quests = QuestInstances0171().Find(filter).Sort(Builders<BsonDocument>.Sort.Descending("UpdatedAtUtc")).Limit(500).ToList()
            .Where(q => Quest0171CanPlayerSeeQuest(actor, q))
            .ToList();
        object[] ItemsFor(params string[] statuses) => quests.Where(q => statuses.Contains(Quest0171String(q, "Status"), StringComparer.OrdinalIgnoreCase))
            .Select(q => (object)QuestInstance0171Payload(q, admin: false)).ToArray();
        return Ok("Quest journal loaded.", new Dictionary<string, object>
        {
            ["active"] = ItemsFor("Active"),
            ["available"] = ItemsFor("Available"),
            ["completed"] = ItemsFor("Completed", "Failed"),
            ["items"] = quests.Select(q => (object)QuestInstance0171Payload(q, admin: false)).ToArray()
        });
    }

    public ResponseEnvelope QuestPlayerGetRewardGrants0171(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var campaignId = Quest0171CampaignId(payload);
        var visibleQuestIdList = QuestInstances0171().Find(Builders<BsonDocument>.Filter.Eq("CampaignId", campaignId) & Builders<BsonDocument>.Filter.Ne("IsArchived", true))
            .ToList()
            .Where(q => Quest0171CanPlayerSeeQuest(actor, q))
            .Select(q => Quest0171String(q, "Id"))
            .ToList();
        var visibleQuestIds = new HashSet<string>(visibleQuestIdList, StringComparer.OrdinalIgnoreCase);
        var grants = QuestRewardGrants0171().Find(Builders<BsonDocument>.Filter.Ne("IsArchived", true)).Limit(500).ToList()
            .Where(g => visibleQuestIds.Contains(Quest0171String(g, "QuestInstanceId")))
            .Select(g => (object)QuestRewardGrant0171Payload(g, admin: false)).ToArray();
        return Ok("Quest reward grants loaded.", new Dictionary<string, object> { ["items"] = grants });
    }

    private ResponseEnvelope QuestAdminListInstances0171(CommandContext context, bool sessionOnly)
    {
        RequireAdmin(context);
        EnsureQuest0171Indexes();
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var campaignId = Quest0171CampaignId(payload);
        var filter = Builders<BsonDocument>.Filter.Eq("CampaignId", campaignId);
        if (sessionOnly)
        {
            var sessionId = Quest0171Text(payload, "sessionId", string.Empty, 128);
            filter &= Builders<BsonDocument>.Filter.Eq("SessionId", sessionId);
        }
        if (!PayloadReader.GetBool(payload, "includeArchived")) filter &= Builders<BsonDocument>.Filter.Ne("IsArchived", true);
        var status = Quest0171Text(payload, "status", string.Empty, 64);
        if (!string.IsNullOrWhiteSpace(status)) filter &= Builders<BsonDocument>.Filter.Eq("Status", status);
        var items = QuestInstances0171().Find(filter).Sort(Builders<BsonDocument>.Sort.Descending("UpdatedAtUtc")).Limit(500).ToList()
            .Select(x => (object)QuestInstance0171Payload(x, admin: true, compact: true)).ToArray();
        return Ok("Quests loaded.", new Dictionary<string, object> { ["items"] = items });
    }

    private ResponseEnvelope Quest0171SetArchived(CommandContext context, bool archived)
    {
        var actor = RequireAdmin(context);
        var quest = Quest0171RequireInstance(context.Request.Payload ?? new Dictionary<string, object>());
        var before = new BsonDocument(quest);
        quest["IsArchived"] = archived;
        if (archived) quest["Status"] = "Archived";
        Quest0171Touch(quest, actor.Id);
        QuestInstances0171().ReplaceOne(Quest0171IdFilter(Quest0171String(quest, "Id")), quest);
        Quest0171Audit(actor, CommandNames.QuestAdminArchive, Quest0171String(quest, "Id"), "quest.archived", before, quest, archived ? "Quest archived." : "Quest restored.");
        Quest0171Sync("quest.updated", "quest_instance", Quest0171String(quest, "Id"), archived ? "archived" : "restored", actor.Id, context.Request.RequestId);
        return Ok(archived ? "Quest archived." : "Quest restored.", Quest0171AdminEnvelope(quest));
    }

    private ResponseEnvelope Quest0171SetStatus(CommandContext context, string status, string eventType)
    {
        var actor = RequireAdmin(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var quest = Quest0171RequireInstance(payload);
        var before = new BsonDocument(quest);
        var now = DateTime.UtcNow;
        quest["Status"] = status;
        if (status == "Active" && quest.GetValue("StartedAtUtc", BsonNull.Value).IsBsonNull) quest["StartedAtUtc"] = now;
        if (status == "Completed" || status == "Failed")
        {
            quest["CompletedAtUtc"] = now;
            quest["CompletedAtWorldDate"] = Quest0171Text(payload, "completedAtWorldDate", Quest0171String(quest, "CompletedAtWorldDate"), 128);
        }
        Quest0171Touch(quest, actor.Id);
        QuestInstances0171().ReplaceOne(Quest0171IdFilter(Quest0171String(quest, "Id")), quest);
        Quest0171Audit(actor, CommandNames.QuestAdminSetStatus, Quest0171String(quest, "Id"), eventType, before, quest, "Quest status changed.");
        Quest0171Sync(eventType, "quest_instance", Quest0171String(quest, "Id"), "status", actor.Id, context.Request.RequestId);
        Dictionary<string, object>? rewardProcessing = null;
        if (status == "Completed")
        {
            rewardProcessing = Gameplay0174ProcessQuestCompletion(actor, quest, context.Request.RequestId);
        }
        var response = Quest0171AdminEnvelope(quest);
        if (rewardProcessing != null) response["rewardProcessing"] = rewardProcessing;
        return Ok("Quest status changed.", response);
    }

    private ResponseEnvelope Quest0171UpdateObjective(CommandContext context, string action)
    {
        var actor = RequireAdmin(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var objectiveId = Quest0171RequiredId(payload, "objectiveId");
        var objective = QuestObjectives0171().Find(Quest0171IdFilter(objectiveId)).FirstOrDefault();
        if (objective == null || Quest0171Bool(objective, "IsArchived")) return Error("Quest objective not found.", ResponseStatus.NotFound, ErrorCode.NotFound);
        var quest = QuestInstances0171().Find(Quest0171IdFilter(Quest0171String(objective, "QuestInstanceId"))).FirstOrDefault();
        if (quest == null) return Error("Quest not found.", ResponseStatus.NotFound, ErrorCode.NotFound);
        var before = new BsonDocument(objective);
        Quest0171SetIfPresent(objective, payload, "title", "Title", 1, 240);
        Quest0171SetIfPresent(objective, payload, "playerText", "PlayerText", 0, 4096);
        Quest0171SetIfPresent(objective, payload, "gmText", "GmText", 0, 8192);
        if (payload.ContainsKey("objectiveType")) objective["ObjectiveType"] = Quest0171Normalize(Quest0171Text(payload, "objectiveType", "Manual", 64), Quest0171ObjectiveTypes(), "Manual");
        if (payload.ContainsKey("status")) objective["Status"] = Quest0171Normalize(Quest0171Text(payload, "status", "Visible", 64), Quest0171ObjectiveStatuses(), "Visible");
        if (payload.ContainsKey("progressCurrent")) objective["ProgressCurrent"] = Math.Max(0, PayloadReader.GetInt(payload, "progressCurrent") ?? Quest0171Int(objective, "ProgressCurrent"));
        if (payload.ContainsKey("progressTarget")) objective["ProgressTarget"] = Math.Max(1, PayloadReader.GetInt(payload, "progressTarget") ?? Quest0171Int(objective, "ProgressTarget", 1));
        if (payload.ContainsKey("visibility")) objective["Visibility"] = Quest0171Normalize(Quest0171Text(payload, "visibility", "PlayerVisible", 64), Quest0171VisibilityModes(), "PlayerVisible");
        if (payload.ContainsKey("order")) objective["Order"] = PayloadReader.GetInt(payload, "order") ?? Quest0171Int(objective, "Order");
        Quest0171Touch(objective, actor.Id);
        QuestObjectives0171().ReplaceOne(Quest0171IdFilter(objectiveId), objective);
        Quest0171TouchAndSaveQuest(quest, actor.Id);
        Quest0171Audit(actor, context.Request.Command, Quest0171String(quest, "Id"), action, before, objective, "Quest objective changed.");
        Quest0171Sync("quest.objective.updated", "quest_instance", Quest0171String(quest, "Id"), action, actor.Id, context.Request.RequestId);
        return Ok("Quest objective updated.", Quest0171AdminEnvelope(quest));
    }

    private ResponseEnvelope Quest0171UpsertRewardBundle(CommandContext context, bool create)
    {
        var actor = RequireAdmin(context);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var quest = Quest0171RequireInstance(payload);
        var bundleId = Quest0171Text(payload, "rewardBundleId", Quest0171String(quest, "RewardBundleId"), 128);
        var bundle = string.IsNullOrWhiteSpace(bundleId) ? null : QuestRewardBundles0171().Find(Quest0171IdFilter(bundleId)).FirstOrDefault();
        if (bundle == null)
        {
            if (!create && !string.IsNullOrWhiteSpace(bundleId)) return Error("Reward bundle not found.", ResponseStatus.NotFound, ErrorCode.NotFound);
            bundle = new BsonDocument
            {
                ["Id"] = Quest0171NewId("quest_reward"),
                ["QuestInstanceId"] = Quest0171String(quest, "Id"),
                ["SchemaVersion"] = 1,
                ["IsArchived"] = false,
                ["CreatedAtUtc"] = DateTime.UtcNow,
                ["CreatedByUserId"] = actor.Id
            };
        }
        var before = new BsonDocument(bundle);
        bundle["Name"] = Quest0171Text(payload, "name", Quest0171String(bundle, "Name", "Награды задачи"), 240, required: true);
        bundle["PublicDescription"] = Quest0171Text(payload, "publicDescription", Quest0171String(bundle, "PublicDescription"), 4096);
        bundle["GmDescription"] = Quest0171Text(payload, "gmDescription", Quest0171String(bundle, "GmDescription"), 8192);
        bundle["MoneyRewards"] = Quest0171Text(payload, "moneyRewards", Quest0171String(bundle, "MoneyRewards"), 2048);
        bundle["ExperienceCoinRewards"] = Quest0171Text(payload, "experienceCoinRewards", Quest0171String(bundle, "ExperienceCoinRewards"), 2048);
        bundle["ItemRewardRefs"] = Quest0171Text(payload, "itemRewardRefs", Quest0171String(bundle, "ItemRewardRefs"), 4096);
        bundle["KnowledgeRewardRefs"] = Quest0171Text(payload, "knowledgeRewardRefs", Quest0171String(bundle, "KnowledgeRewardRefs"), 4096);
        bundle["ReputationRewardRefs"] = Quest0171Text(payload, "reputationRewardRefs", Quest0171String(bundle, "ReputationRewardRefs"), 4096);
        bundle["UnlockRewardRefs"] = Quest0171Text(payload, "unlockRewardRefs", Quest0171String(bundle, "UnlockRewardRefs"), 4096);
        bundle["CustomRewardText"] = Quest0171Text(payload, "customRewardText", Quest0171String(bundle, "CustomRewardText"), 4096);
        bundle["RequiresGmApply"] = payload.ContainsKey("requiresGmApply") ? PayloadReader.GetBool(payload, "requiresGmApply") : Quest0171Bool(bundle, "RequiresGmApply", true);
        Quest0171Touch(bundle, actor.Id);
        QuestRewardBundles0171().ReplaceOne(Quest0171IdFilter(Quest0171String(bundle, "Id")), bundle, new ReplaceOptions { IsUpsert = true });
        quest["RewardBundleId"] = Quest0171String(bundle, "Id");
        Quest0171TouchAndSaveQuest(quest, actor.Id);
        Quest0171Audit(actor, create ? CommandNames.QuestAdminCreateRewardBundle : CommandNames.QuestAdminUpdateRewardBundle, Quest0171String(quest, "Id"), "quest.reward.updated", before, bundle, "Quest reward bundle saved.");
        Quest0171Sync("quest.reward.updated", "quest_instance", Quest0171String(quest, "Id"), "reward.updated", actor.Id, context.Request.RequestId);
        return Ok("Quest reward bundle saved.", Quest0171AdminEnvelope(quest));
    }

    private ResponseEnvelope Quest0171PlayerList(CommandContext context, string[] statuses)
    {
        var actor = GetCurrentAccount(context);
        EnsureQuest0171Indexes();
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var campaignId = Quest0171CampaignId(payload);
        var filter = Builders<BsonDocument>.Filter.Eq("CampaignId", campaignId)
            & Builders<BsonDocument>.Filter.In("Status", statuses)
            & Builders<BsonDocument>.Filter.Ne("IsArchived", true);
        var items = QuestInstances0171().Find(filter).Sort(Builders<BsonDocument>.Sort.Descending("UpdatedAtUtc")).Limit(500).ToList()
            .Where(q => Quest0171CanPlayerSeeQuest(actor, q))
            .Select(q => (object)QuestInstance0171Payload(q, admin: false, compact: true)).ToArray();
        return Ok("Player quests loaded.", new Dictionary<string, object> { ["items"] = items });
    }

    private Dictionary<string, object> Quest0171AdminEnvelope(BsonDocument quest)
    {
        return new Dictionary<string, object>
        {
            ["item"] = QuestInstance0171Payload(quest, admin: true),
            ["objectives"] = QuestObjectives0171ForQuest(Quest0171String(quest, "Id")).Select(x => (object)QuestObjective0171Payload(x, admin: true)).ToArray(),
            ["rewardBundle"] = Quest0171RewardBundleForQuest(Quest0171String(quest, "Id")) is BsonDocument bundle ? QuestRewardBundle0171Payload(bundle, admin: true) : new Dictionary<string, object>(),
            ["rewardGrants"] = QuestRewardGrants0171ForQuest(Quest0171String(quest, "Id")).Select(x => (object)QuestRewardGrant0171Payload(x, admin: true)).ToArray(),
            ["audit"] = QuestAudit0171().Find(Builders<BsonDocument>.Filter.Eq("QuestInstanceId", Quest0171String(quest, "Id"))).Sort(Builders<BsonDocument>.Sort.Descending("CreatedAtUtc")).Limit(50).ToList().Select(x => (object)Quest0171DocPayload(x, admin: true)).ToArray()
        };
    }

    private Dictionary<string, object> QuestInstance0171Payload(BsonDocument quest, bool admin, bool compact = false)
    {
        var id = Quest0171String(quest, "Id");
        var map = new Dictionary<string, object>
        {
            ["questId"] = id,
            ["status"] = Quest0171String(quest, "Status"),
            ["visibility"] = admin ? Quest0171String(quest, "Visibility") : "PlayerVisible",
            ["playerTitle"] = Quest0171String(quest, "PlayerTitle"),
            ["title"] = Quest0171String(quest, "PlayerTitle"),
            ["playerSummary"] = Quest0171String(quest, "PlayerSummary"),
            ["summary"] = Quest0171String(quest, "PlayerSummary"),
            ["playerKnownDetails"] = Quest0171String(quest, "PlayerKnownDetails"),
            ["startedAtWorldDate"] = Quest0171String(quest, "StartedAtWorldDate"),
            ["completedAtWorldDate"] = Quest0171String(quest, "CompletedAtWorldDate"),
            ["startedAtUtc"] = Quest0171DateString(quest, "StartedAtUtc"),
            ["completedAtUtc"] = Quest0171DateString(quest, "CompletedAtUtc"),
            ["updatedAtUtc"] = Quest0171DateString(quest, "UpdatedAtUtc")
        };
        if (admin)
        {
            map["id"] = id;
            map["definitionId"] = Quest0171String(quest, "DefinitionId");
            map["campaignId"] = Quest0171String(quest, "CampaignId");
            map["sessionId"] = Quest0171String(quest, "SessionId");
            map["sceneId"] = Quest0171String(quest, "SceneId");
            map["locationId"] = Quest0171String(quest, "LocationId");
            map["assignedPartyId"] = Quest0171String(quest, "AssignedPartyId");
            map["assignedCharacterIds"] = Quest0171ArrayValues(quest, "AssignedCharacterIds").Cast<object>().ToArray();
            map["assignedPlayerUserIds"] = Quest0171ArrayValues(quest, "AssignedPlayerUserIds").Cast<object>().ToArray();
            map["rewardBundleId"] = Quest0171String(quest, "RewardBundleId");
            map["gmTitle"] = Quest0171String(quest, "GmTitle");
            map["gmSummary"] = Quest0171String(quest, "GmSummary");
            map["gmNotes"] = Quest0171String(quest, "GmNotes");
            map["serverOnlyDataPresent"] = !string.IsNullOrWhiteSpace(Quest0171String(quest, "ServerOnlyData"));
            map["ownerGmUserId"] = Quest0171String(quest, "OwnerGmUserId");
        }
        if (!compact)
        {
            map["objectives"] = QuestObjectives0171ForQuest(id)
                .Where(o => admin || Quest0171ObjectiveVisibleToPlayer(o))
                .Select(o => (object)QuestObjective0171Payload(o, admin)).ToArray();
            var bundle = Quest0171RewardBundleForQuest(id);
            map["rewardBundle"] = bundle == null ? new Dictionary<string, object>() : QuestRewardBundle0171Payload(bundle, admin);
            map["rewardGrants"] = QuestRewardGrants0171ForQuest(id).Select(g => (object)QuestRewardGrant0171Payload(g, admin)).ToArray();
        }
        return map;
    }

    private Dictionary<string, object> QuestDefinition0171Payload(BsonDocument doc, bool admin)
    {
        var map = new Dictionary<string, object>
        {
            ["definitionId"] = Quest0171String(doc, "Id"),
            ["id"] = Quest0171String(doc, "Id"),
            ["campaignId"] = Quest0171String(doc, "CampaignId"),
            ["worldId"] = Quest0171String(doc, "WorldId"),
            ["name"] = Quest0171String(doc, "Name"),
            ["shortCode"] = Quest0171String(doc, "ShortCode"),
            ["category"] = Quest0171String(doc, "Category"),
            ["publicDescription"] = Quest0171String(doc, "PublicDescription"),
            ["defaultVisibility"] = Quest0171String(doc, "DefaultVisibility"),
            ["isArchived"] = Quest0171Bool(doc, "IsArchived")
        };
        if (admin)
        {
            map["gmDescription"] = Quest0171String(doc, "GmDescription");
            map["serverOnlyDataPresent"] = !string.IsNullOrWhiteSpace(Quest0171String(doc, "ServerOnlyData"));
        }
        return map;
    }

    private Dictionary<string, object> QuestObjective0171Payload(BsonDocument doc, bool admin)
    {
        var map = new Dictionary<string, object>
        {
            ["order"] = Quest0171Int(doc, "Order"),
            ["title"] = Quest0171String(doc, "Title"),
            ["playerText"] = Quest0171String(doc, "PlayerText"),
            ["objectiveType"] = Quest0171String(doc, "ObjectiveType"),
            ["status"] = Quest0171String(doc, "Status"),
            ["progressCurrent"] = Quest0171Int(doc, "ProgressCurrent"),
            ["progressTarget"] = Quest0171Int(doc, "ProgressTarget", 1)
        };
        if (admin)
        {
            map["objectiveId"] = Quest0171String(doc, "Id");
            map["id"] = Quest0171String(doc, "Id");
            map["questInstanceId"] = Quest0171String(doc, "QuestInstanceId");
            map["gmText"] = Quest0171String(doc, "GmText");
            map["visibility"] = Quest0171String(doc, "Visibility");
            map["serverOnlyDataPresent"] = !string.IsNullOrWhiteSpace(Quest0171String(doc, "ServerOnlyData"));
            map["relatedCharacterIds"] = Quest0171ArrayValues(doc, "RelatedCharacterIds").Cast<object>().ToArray();
            map["relatedNpcIds"] = Quest0171ArrayValues(doc, "RelatedNpcIds").Cast<object>().ToArray();
            map["relatedFactionIds"] = Quest0171ArrayValues(doc, "RelatedFactionIds").Cast<object>().ToArray();
            map["relatedLocationIds"] = Quest0171ArrayValues(doc, "RelatedLocationIds").Cast<object>().ToArray();
            map["relatedSceneMapIds"] = Quest0171ArrayValues(doc, "RelatedSceneMapIds").Cast<object>().ToArray();
            map["relatedWorldMapMarkerIds"] = Quest0171ArrayValues(doc, "RelatedWorldMapMarkerIds").Cast<object>().ToArray();
            map["relatedCombatSessionIds"] = Quest0171ArrayValues(doc, "RelatedCombatSessionIds").Cast<object>().ToArray();
        }
        return map;
    }

    private Dictionary<string, object> QuestRewardBundle0171Payload(BsonDocument doc, bool admin)
    {
        var map = new Dictionary<string, object>
        {
            ["name"] = Quest0171String(doc, "Name"),
            ["publicDescription"] = Quest0171String(doc, "PublicDescription"),
            ["moneyRewards"] = Quest0171String(doc, "MoneyRewards"),
            ["experienceCoinRewards"] = Quest0171String(doc, "ExperienceCoinRewards"),
            ["itemRewardRefs"] = Quest0171String(doc, "ItemRewardRefs"),
            ["knowledgeRewardRefs"] = Quest0171String(doc, "KnowledgeRewardRefs"),
            ["reputationRewardRefs"] = Quest0171String(doc, "ReputationRewardRefs"),
            ["unlockRewardRefs"] = Quest0171String(doc, "UnlockRewardRefs"),
            ["customRewardText"] = Quest0171String(doc, "CustomRewardText"),
            ["requiresGmApply"] = Quest0171Bool(doc, "RequiresGmApply", true),
            ["summary"] = Quest0171RewardSummary(doc, admin)
        };
        if (admin)
        {
            map["rewardBundleId"] = Quest0171String(doc, "Id");
            map["questInstanceId"] = Quest0171String(doc, "QuestInstanceId");
            map["gmDescription"] = Quest0171String(doc, "GmDescription");
        }
        return map;
    }

    private Dictionary<string, object> QuestRewardGrant0171Payload(BsonDocument doc, bool admin)
    {
        var map = new Dictionary<string, object>
        {
            ["status"] = Quest0171String(doc, "Status"),
            ["playerVisibleSummary"] = Quest0171String(doc, "PlayerVisibleSummary"),
            ["appliedAtUtc"] = Quest0171DateString(doc, "AppliedAtUtc")
        };
        if (admin)
        {
            map["grantId"] = Quest0171String(doc, "Id");
            map["questInstanceId"] = Quest0171String(doc, "QuestInstanceId");
            map["rewardBundleId"] = Quest0171String(doc, "RewardBundleId");
            map["targetCharacterIds"] = Quest0171ArrayValues(doc, "TargetCharacterIds").Cast<object>().ToArray();
            map["targetPlayerUserIds"] = Quest0171ArrayValues(doc, "TargetPlayerUserIds").Cast<object>().ToArray();
            map["appliedByUserId"] = Quest0171String(doc, "AppliedByUserId");
            map["gmSummary"] = Quest0171String(doc, "GmSummary");
            map["grantResults"] = Quest0171ArrayValues(doc, "GrantResults").Cast<object>().ToArray();
        }
        return map;
    }

    private Dictionary<string, object> Quest0171DocPayload(BsonDocument doc, bool admin)
    {
        var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var element in doc)
        {
            if (string.Equals(element.Name, "_id", StringComparison.OrdinalIgnoreCase)) continue;
            if (!admin && Quest0171IsSensitiveField(element.Name)) continue;
            result[Quest0171Camel(element.Name)] = Quest0171BsonValue(element.Value, admin);
        }
        return result;
    }

    private bool Quest0171CanPlayerSeeQuest(UserAccount actor, BsonDocument quest)
    {
        if (Quest0171Bool(quest, "IsArchived")) return false;
        var status = Quest0171String(quest, "Status");
        if (status == "Draft" || status == "Cancelled" || status == "Archived") return false;
        var visibility = Quest0171String(quest, "Visibility");
        if (visibility == "Hidden" || visibility == "GmOnly") return false;
        if (visibility == "PlayerVisible" || visibility == "PartyVisible") return true;
        if (Quest0171ArrayValues(quest, "AssignedPlayerUserIds").Contains(actor.Id, StringComparer.OrdinalIgnoreCase)) return true;
        var characterIds = Quest0171ArrayValues(quest, "AssignedCharacterIds");
        if (characterIds.Count == 0) return false;
        return _repositories.Characters.Find(Builders<Character>.Filter.In(x => x.Id, characterIds) & Builders<Character>.Filter.Eq(x => x.OwnerUserId, actor.Id)).Any();
    }

    private static bool Quest0171ObjectiveVisibleToPlayer(BsonDocument objective)
    {
        if (Quest0171Bool(objective, "IsArchived")) return false;
        var status = Quest0171String(objective, "Status");
        if (status == "Hidden") return false;
        var visibility = Quest0171String(objective, "Visibility");
        return visibility != "Hidden" && visibility != "GmOnly";
    }

    private BsonDocument Quest0171RequireDefinition(IDictionary<string, object> payload)
    {
        var id = Quest0171RequiredId(payload, "definitionId");
        var doc = QuestDefinitions0171().Find(Quest0171IdFilter(id)).FirstOrDefault();
        if (doc == null || Quest0171Bool(doc, "IsArchived")) throw new KeyNotFoundException("Quest definition not found.");
        return doc;
    }

    private BsonDocument Quest0171RequireInstance(IDictionary<string, object> payload)
    {
        EnsureQuest0171Indexes();
        var id = Quest0171RequiredId(payload, "questId");
        var doc = QuestInstances0171().Find(Quest0171IdFilter(id)).FirstOrDefault();
        if (doc == null || Quest0171Bool(doc, "IsArchived")) throw new KeyNotFoundException("Quest not found.");
        return doc;
    }

    private void Quest0171Audit(UserAccount actor, string command, string questId, string action, BsonDocument? before, BsonDocument? after, string summary)
    {
        var audit = new BsonDocument
        {
            ["Id"] = Quest0171NewId("quest_audit"),
            ["QuestInstanceId"] = questId,
            ["ActorUserId"] = actor.Id,
            ["ActorLogin"] = actor.Login,
            ["ActorRoles"] = new BsonArray(actor.Roles.Select(x => x.ToString())),
            ["Command"] = command,
            ["Action"] = action,
            ["Summary"] = summary,
            ["Before"] = before == null ? BsonNull.Value : Quest0171SafeAuditDoc(before),
            ["After"] = after == null ? BsonNull.Value : Quest0171SafeAuditDoc(after),
            ["CreatedAtUtc"] = DateTime.UtcNow
        };
        QuestAudit0171().InsertOne(audit);
        WriteAudit("quest", actor.Id, action, questId);
    }

    private void Quest0171Sync(string type, string entityType, string entityId, string operation, string actorUserId, string? requestId)
    {
        TryPublishSyncEvent(type, "quests", entityType, entityId, operation, actorUserId, new Dictionary<string, object>
        {
            ["entityType"] = entityType,
            ["entityId"] = entityId,
            ["operation"] = operation
        }, requestId ?? string.Empty);
    }

    private void EnsureQuest0171Indexes()
    {
        if (_quest0171IndexesEnsured) return;
        QuestDefinitions0171().Indexes.CreateOne(new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("CampaignId").Ascending("IsArchived")));
        QuestInstances0171().Indexes.CreateOne(new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("CampaignId").Ascending("Status").Ascending("IsArchived")));
        QuestInstances0171().Indexes.CreateOne(new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("SessionId")));
        QuestInstances0171().Indexes.CreateOne(new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("AssignedCharacterIds")));
        QuestObjectives0171().Indexes.CreateOne(new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("QuestInstanceId").Ascending("Order")));
        QuestRewardBundles0171().Indexes.CreateOne(new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("QuestInstanceId")));
        QuestRewardGrants0171().Indexes.CreateOne(new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("QuestInstanceId").Ascending("Status")));
        QuestAudit0171().Indexes.CreateOne(new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("QuestInstanceId").Descending("CreatedAtUtc")));
        _quest0171IndexesEnsured = true;
    }

    private IMongoCollection<BsonDocument> QuestDefinitions0171() => _mongo.Database.GetCollection<BsonDocument>(QuestDefinitions0171Collection);
    private IMongoCollection<BsonDocument> QuestInstances0171() => _mongo.Database.GetCollection<BsonDocument>(QuestInstances0171Collection);
    private IMongoCollection<BsonDocument> QuestObjectives0171() => _mongo.Database.GetCollection<BsonDocument>(QuestObjectives0171Collection);
    private IMongoCollection<BsonDocument> QuestRewardBundles0171() => _mongo.Database.GetCollection<BsonDocument>(QuestRewardBundles0171Collection);
    private IMongoCollection<BsonDocument> QuestRewardGrants0171() => _mongo.Database.GetCollection<BsonDocument>(QuestRewardGrants0171Collection);
    private IMongoCollection<BsonDocument> QuestAudit0171() => _mongo.Database.GetCollection<BsonDocument>(QuestAudit0171Collection);

    private List<BsonDocument> QuestObjectives0171ForQuest(string questId) => QuestObjectives0171().Find(Builders<BsonDocument>.Filter.Eq("QuestInstanceId", questId) & Builders<BsonDocument>.Filter.Ne("IsArchived", true)).Sort(Builders<BsonDocument>.Sort.Ascending("Order")).ToList();
    private BsonDocument? Quest0171RewardBundleForQuest(string questId) => QuestRewardBundles0171().Find(Builders<BsonDocument>.Filter.Eq("QuestInstanceId", questId) & Builders<BsonDocument>.Filter.Ne("IsArchived", true)).FirstOrDefault();
    private List<BsonDocument> QuestRewardGrants0171ForQuest(string questId) => QuestRewardGrants0171().Find(Builders<BsonDocument>.Filter.Eq("QuestInstanceId", questId) & Builders<BsonDocument>.Filter.Ne("IsArchived", true)).Sort(Builders<BsonDocument>.Sort.Descending("UpdatedAtUtc")).ToList();

    private static FilterDefinition<BsonDocument> Quest0171IdFilter(string id) => Builders<BsonDocument>.Filter.Eq("Id", id);
    private static string Quest0171NewId(string prefix) => prefix + "_" + Guid.NewGuid().ToString("N");
    private static string Quest0171RequiredId(IDictionary<string, object> payload, string key) => RequireLength(FirstNonEmpty(PayloadReader.GetString(payload, key), PayloadReader.GetString(payload, "id")), 1, 128, key);
    private static string Quest0171CampaignId(IDictionary<string, object> payload, BsonDocument? fallback = null) => RequireLength(FirstNonEmpty(PayloadReader.GetString(payload, "campaignId"), fallback == null ? string.Empty : Quest0171String(fallback, "CampaignId"), "dev-campaign-core"), 1, 128, "campaignId");
    private static string Quest0171Text(IDictionary<string, object> payload, string key, string fallback, int max, bool required = false) => RequireLength(FirstNonEmpty(PayloadReader.GetString(payload, key), fallback), required ? 1 : 0, max, key);

    private static string Quest0171String(BsonDocument doc, string key, string fallback = "")
    {
        if (!doc.Contains(key) || doc[key].IsBsonNull) return fallback;
        if (doc[key].IsString) return doc[key].AsString;
        return Convert.ToString(BsonTypeMapper.MapToDotNetValue(doc[key]), CultureInfo.InvariantCulture) ?? fallback;
    }

    private static int Quest0171Int(BsonDocument doc, string key, int fallback = 0)
    {
        if (!doc.Contains(key) || doc[key].IsBsonNull) return fallback;
        if (doc[key].IsInt32) return doc[key].AsInt32;
        int value;
        return int.TryParse(Quest0171String(doc, key), out value) ? value : fallback;
    }

    private static bool Quest0171Bool(BsonDocument doc, string key, bool fallback = false)
    {
        if (!doc.Contains(key) || doc[key].IsBsonNull) return fallback;
        if (doc[key].IsBoolean) return doc[key].AsBoolean;
        bool value;
        return bool.TryParse(Quest0171String(doc, key), out value) ? value : fallback;
    }

    private static string Quest0171DateString(BsonDocument doc, string key)
    {
        if (!doc.Contains(key) || doc[key].IsBsonNull) return string.Empty;
        return doc[key].ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
    }

    private static BsonArray Quest0171Array(IDictionary<string, object> payload, string key, IEnumerable<string>? fallback = null)
    {
        var list = new List<string>();
        var source = PayloadReader.GetList(payload, key);
        if (source != null)
        {
            foreach (var item in source)
            {
                var value = Convert.ToString(item, CultureInfo.InvariantCulture);
                if (!string.IsNullOrWhiteSpace(value)) list.Add(value.Trim());
            }
        }
        else
        {
            var single = PayloadReader.GetString(payload, key);
            if (!string.IsNullOrWhiteSpace(single))
            {
                list.AddRange(single.Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(x => x.Length > 0));
            }
            else if (fallback != null)
            {
                list.AddRange(fallback.Where(x => !string.IsNullOrWhiteSpace(x)));
            }
        }
        return new BsonArray(list.Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private static List<string> Quest0171ArrayValues(BsonDocument doc, string key)
    {
        if (!doc.Contains(key) || !doc[key].IsBsonArray) return new List<string>();
        return doc[key].AsBsonArray.Select(x => x.IsBsonNull ? string.Empty : Convert.ToString(BsonTypeMapper.MapToDotNetValue(x), CultureInfo.InvariantCulture) ?? string.Empty)
            .Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
    }

    private static string Quest0171Normalize(string value, HashSet<string> allowed, string fallback)
    {
        var match = allowed.FirstOrDefault(x => string.Equals(x, value, StringComparison.OrdinalIgnoreCase));
        return match ?? fallback;
    }

    private static HashSet<string> Quest0171Categories() => new HashSet<string>(new[] { "Main", "Side", "Personal", "Faction", "Exploration", "Combat", "Social", "Craft", "Custom" }, StringComparer.OrdinalIgnoreCase);
    private static HashSet<string> Quest0171Statuses() => new HashSet<string>(new[] { "Draft", "Available", "Active", "Completed", "Failed", "Cancelled", "Archived" }, StringComparer.OrdinalIgnoreCase);
    private static HashSet<string> Quest0171VisibilityModes() => new HashSet<string>(new[] { "GmOnly", "PlayerVisible", "PartyVisible", "AssignedCharactersOnly", "Hidden" }, StringComparer.OrdinalIgnoreCase);
    private static HashSet<string> Quest0171ObjectiveTypes() => new HashSet<string>(new[] { "Manual", "Talk", "Explore", "Collect", "Defeat", "Escort", "Deliver", "Investigate", "Custom" }, StringComparer.OrdinalIgnoreCase);
    private static HashSet<string> Quest0171ObjectiveStatuses() => new HashSet<string>(new[] { "Hidden", "Visible", "Active", "Completed", "Failed", "Skipped" }, StringComparer.OrdinalIgnoreCase);

    private static void Quest0171SetIfPresent(BsonDocument doc, IDictionary<string, object> payload, string payloadKey, string docKey, int min, int max)
    {
        if (!payload.ContainsKey(payloadKey)) return;
        doc[docKey] = RequireLength(PayloadReader.GetString(payload, payloadKey), min, max, payloadKey);
    }

    private void Quest0171TouchAndSaveQuest(BsonDocument quest, string actorId)
    {
        Quest0171Touch(quest, actorId);
        QuestInstances0171().ReplaceOne(Quest0171IdFilter(Quest0171String(quest, "Id")), quest);
    }

    private static void Quest0171Touch(BsonDocument doc, string actorId)
    {
        doc["UpdatedAtUtc"] = DateTime.UtcNow;
        doc["UpdatedByUserId"] = actorId;
        doc["Revision"] = Quest0171Int(doc, "Revision") + 1;
    }

    private static Dictionary<string, object> Quest0171ObjectMap(object item)
    {
        if (item is IDictionary<string, object> typed) return new Dictionary<string, object>(typed, StringComparer.OrdinalIgnoreCase);
        var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        if (item is IDictionary dictionary)
        {
            foreach (DictionaryEntry entry in dictionary)
            {
                var key = Convert.ToString(entry.Key);
                if (!string.IsNullOrWhiteSpace(key)) result[key] = entry.Value ?? string.Empty;
            }
            return result;
        }
        return result;
    }

    private static string Quest0171RewardSummary(BsonDocument bundle, bool admin)
    {
        var parts = new[]
        {
            Quest0171String(bundle, "MoneyRewards"),
            Quest0171String(bundle, "ExperienceCoinRewards"),
            Quest0171String(bundle, "ItemRewardRefs"),
            Quest0171String(bundle, "KnowledgeRewardRefs"),
            Quest0171String(bundle, "ReputationRewardRefs"),
            Quest0171String(bundle, "UnlockRewardRefs"),
            Quest0171String(bundle, "CustomRewardText"),
            admin ? Quest0171String(bundle, "GmDescription") : string.Empty
        }.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
        return parts.Length == 0 ? "Награды ещё не заданы." : string.Join("; ", parts);
    }

    private static BsonDocument Quest0171SafeAuditDoc(BsonDocument source)
    {
        var result = new BsonDocument();
        foreach (var element in source)
        {
            if (Quest0171IsServerOnlyField(element.Name)) continue;
            result[element.Name] = element.Value;
        }
        return result;
    }

    private static bool Quest0171IsServerOnlyField(string field)
        => string.Equals(field, "ServerOnlyData", StringComparison.OrdinalIgnoreCase);

    private static bool Quest0171IsSensitiveField(string field)
    {
        return Quest0171IsServerOnlyField(field)
            || field.IndexOf("Gm", StringComparison.OrdinalIgnoreCase) >= 0
            || field.IndexOf("Admin", StringComparison.OrdinalIgnoreCase) >= 0
            || string.Equals(field, "OwnerGmUserId", StringComparison.OrdinalIgnoreCase)
            || string.Equals(field, "AssignedPlayerUserIds", StringComparison.OrdinalIgnoreCase)
            || string.Equals(field, "AssignedCharacterIds", StringComparison.OrdinalIgnoreCase);
    }

    private static string Quest0171Camel(string value)
        => string.IsNullOrWhiteSpace(value) ? value : char.ToLowerInvariant(value[0]) + value.Substring(1);

    private static object Quest0171BsonValue(BsonValue value, bool admin)
    {
        if (value == null || value.IsBsonNull) return string.Empty;
        if (value.IsBsonArray) return value.AsBsonArray.Select(x => Quest0171BsonValue(x, admin)).ToArray();
        if (value.IsBsonDocument) return Quest0171DocPayloadStatic(value.AsBsonDocument, admin);
        if (value.BsonType == BsonType.ObjectId) return value.AsObjectId.ToString();
        if (value.BsonType == BsonType.DateTime) return value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
        return BsonTypeMapper.MapToDotNetValue(value);
    }

    private static Dictionary<string, object> Quest0171DocPayloadStatic(BsonDocument doc, bool admin)
    {
        var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var element in doc)
        {
            if (string.Equals(element.Name, "_id", StringComparison.OrdinalIgnoreCase)) continue;
            if (!admin && Quest0171IsSensitiveField(element.Name)) continue;
            result[Quest0171Camel(element.Name)] = Quest0171BsonValue(element.Value, admin);
        }
        return result;
    }
}
