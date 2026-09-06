using System;
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
    public ResponseEnvelope GameplayAdminGetResolutionQueue0174(CommandContext context)
    {
        GetCurrentAccount(context);
        Gameplay0174EnsureIndexes();
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var campaignId = Gameplay0174CampaignId(payload);

        var purchaseRequests = Gameplay0174PurchaseRequests(campaignId).ToList();
        var saleRequests = purchaseRequests.Where(Gameplay0174IsSaleRequest).ToList();
        var buyRequests = purchaseRequests.Where(x => !Gameplay0174IsSaleRequest(x)).ToList();
        var rewardGrants = Gameplay0174QuestRewardGrants(campaignId).ToList();
        var recoveryGrants = Gameplay0174RecoveryGrants(campaignId).ToList();
        var rests = Gameplay0174RestSessions(campaignId).ToList();
        var downtime = Gameplay0174DowntimeActions(campaignId).ToList();

        var queueItems = new List<object>();
        var automationItems = _repositories.AutomationExecutions.Find(
            Builders<AutomationExecutionRecord>.Filter.Eq(x => x.CampaignId, campaignId)
            & Builders<AutomationExecutionRecord>.Filter.Eq(x => x.Status, AutomationExecutionStatusIds.Proposed));
        queueItems.AddRange(automationItems.Select(x => Gameplay0174QueueItem("AutomationProposal", new BsonDocument(), "Автоматизация", "Ожидает решения", "Система", x.Id, x.ReadableResult)));
        queueItems.AddRange(saleRequests.Select(x => Gameplay0174QueueItem("SaleRequest", x, "Продажа", Shop0172String(x, "Status"), Shop0172String(x, "SellerLogin", Shop0172String(x, "BuyerLogin")), Shop0172String(x, "Id"), Shop0172String(x, "PlayerComment"))));
        queueItems.AddRange(purchaseRequests.Select(x => Gameplay0174QueueItem("PurchaseRequest", x, "Покупка", Shop0172String(x, "Status"), Shop0172String(x, "BuyerLogin"), Shop0172String(x, "Id"), Shop0172String(x, "PlayerComment"))));
        queueItems.AddRange(rewardGrants.Select(x => Gameplay0174QueueItem("RewardGrant", x, "Награда", Quest0171String(x, "Status"), string.Join(", ", Quest0171ArrayValues(x, "TargetPlayerUserIds")), Quest0171String(x, "Id"), Quest0171String(x, "PlayerVisibleSummary"))));
        queueItems.AddRange(recoveryGrants.Select(x => Gameplay0174QueueItem("RestRecovery", x, "Восстановление", Rest0173String(x, "Status"), Rest0173String(x, "PlayerUserId"), Rest0173String(x, "Id"), Rest0173String(x, "RecoverySummaryPlayer"))));
        queueItems.AddRange(downtime.Where(x => Rest0173String(x, "Status") is "Submitted" or "Approved")
            .Select(x => Gameplay0174QueueItem("DowntimeAction", x, "Действие отдыха", Rest0173String(x, "Status"), Rest0173String(x, "PlayerUserId"), Rest0173String(x, "Id"), Rest0173String(x, "PlayerText"))));

        return Ok("Gameplay resolution queue loaded.", new Dictionary<string, object>
        {
            ["queueItems"] = queueItems.ToArray(),
            ["questSummary"] = Gameplay0174QuestSummary(campaignId),
            ["purchaseRequests"] = buyRequests.Select(x => (object)Shop0172PurchaseRequestPayload(x, admin: true)).ToArray(),
            ["saleRequests"] = saleRequests.Select(x => (object)Shop0172PurchaseRequestPayload(x, admin: true)).ToArray(),
            ["personnelRequests"] = buyRequests.Where(Gameplay0174IsPersonnelPurchase).Select(x => (object)Shop0172PurchaseRequestPayload(x, admin: true)).ToArray(),
            ["rewardGrants"] = rewardGrants.Select(x => (object)QuestRewardGrant0171Payload(x, admin: true)).ToArray(),
            ["restStatus"] = rests.Select(x => (object)Rest0173SessionPayload(x, admin: true)).ToArray(),
            ["downtimeActions"] = downtime.Select(x => (object)Rest0173DowntimePayload(x, admin: true, viewer: null)).ToArray(),
            ["audit"] = Gameplay0174AuditSummary(campaignId),
            ["integrationMode"] = "0.17.4 focused integration: safe grants are recorded idempotently; profile mutation is only used when an explicit safe adapter exists."
        });
    }

    public ResponseEnvelope GameplayAdminResolveQueueItem0174(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        Gameplay0174EnsureIndexes();
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var itemType = Gameplay0174Text(payload, "itemType", Gameplay0174Text(payload, "type", string.Empty, 64), 64);
        var entityId = Gameplay0174Text(payload, "entityId", Gameplay0174Text(payload, "id", string.Empty, 128), 128);
        if (string.IsNullOrWhiteSpace(itemType) || string.IsNullOrWhiteSpace(entityId))
            return Error("itemType and entityId are required.", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);

        if (itemType.Equals("RewardGrant", StringComparison.OrdinalIgnoreCase))
        {
            var grant = QuestRewardGrants0171().Find(Quest0171IdFilter(entityId)).FirstOrDefault();
            if (grant == null || Quest0171Bool(grant, "IsArchived")) return Error("Reward grant not found.", ResponseStatus.NotFound, ErrorCode.NotFound);
            var updated = Gameplay0174ApplyQuestRewardGrant(actor, grant, context.Request.RequestId, manual: true);
            return Ok("Reward grant resolved.", new Dictionary<string, object> { ["item"] = QuestRewardGrant0171Payload(updated, admin: true) });
        }

        if (itemType.Equals("AutomationProposal", StringComparison.OrdinalIgnoreCase))
        {
            var execution = _repositories.AutomationExecutions.GetById(entityId);
            if (execution == null || execution.Status != AutomationExecutionStatusIds.Proposed)
                return Error("Automation proposal not found.", ResponseStatus.NotFound, ErrorCode.NotFound);
            _campaignAuthorization.RequireCampaignCapability(context.Session!, execution.CampaignId, CampaignCapabilityIds.AutomationApprove);
            execution.Status = AutomationExecutionStatusIds.Approved;
            execution.DecidedAtUtc = DateTime.UtcNow;
            execution.DecidedByUserId = actor.Id;
            execution.EntityRevision++;
            if (execution.TargetAction == "session.attention.notify")
            {
                execution.Status = AutomationExecutionStatusIds.Applied;
                execution.AppliedAtUtc = DateTime.UtcNow;
                execution.ReadableResult = "GM подтвердил уведомление; доменные данные не изменялись.";
            }
            else if (execution.TargetAction == "resolution_queue.propose_exposure"
                     && execution.Trigger == "weather.exposure.harmful")
            {
                var exposureContext = new CommandContext
                {
                    ConnectionId = context.ConnectionId,
                    Session = context.Session,
                    Request = new RequestEnvelope
                    {
                        Command = CommandNames.WorldAdminExposureApprove,
                        RequestId = context.Request.RequestId,
                        AuthToken = context.Request.AuthToken,
                        Payload = new Dictionary<string, object>
                        {
                            ["suggestionId"] = execution.CorrelationId,
                            ["operationId"] = execution.OperationId
                        }
                    }
                };
                var exposureResult = WorldAdminExposureApprove0217(exposureContext);
                if (exposureResult.Status == ResponseStatus.Ok)
                {
                    execution.Status = AutomationExecutionStatusIds.Applied;
                    execution.AppliedAtUtc = DateTime.UtcNow;
                    execution.ReadableResult = "GM подтвердил воздействие среды; эффект применён каноническим сервисом один раз.";
                }
                else
                {
                    execution.Status = AutomationExecutionStatusIds.Failed;
                    execution.FailureCategory = "canonical_action_failed";
                    execution.ReadableResult = "Канонический сервис воздействия среды отклонил действие; прямое изменение данных не выполнялось.";
                }
            }
            else
            {
                execution.Status = AutomationExecutionStatusIds.Failed;
                execution.FailureCategory = "canonical_action_adapter_required";
                execution.ReadableResult = "Для действия нет разрешённого канонического адаптера; изменения не применены.";
            }
            _repositories.AutomationExecutions.Replace(execution);
            WriteAudit("automation", actor.Id, "execution.resolve", $"{execution.Id}:{execution.Status}");
            return Ok("Предложение автоматизации обработано.", new Dictionary<string, object> { ["status"] = execution.Status, ["result"] = execution.ReadableResult });
        }

        if (itemType.Equals("RestRecovery", StringComparison.OrdinalIgnoreCase))
        {
            var grant = RecoveryGrants0173().Find(Rest0173IdFilter(entityId)).FirstOrDefault();
            if (grant == null || Rest0173Bool(grant, "IsArchived")) return Error("Recovery grant not found.", ResponseStatus.NotFound, ErrorCode.NotFound);
            var updated = Gameplay0174ApplyRestRecoveryGrant(actor, grant, context.Request.RequestId);
            return Ok("Recovery grant resolved.", new Dictionary<string, object> { ["item"] = Rest0173GrantPayload(updated, admin: true, viewer: null) });
        }

        if (itemType.Equals("PurchaseRequest", StringComparison.OrdinalIgnoreCase))
        {
            var request = PurchaseRequests0172().Find(Shop0172IdFilter(entityId)).FirstOrDefault();
            if (request == null || Shop0172Bool(request, "IsArchived")) return Error("Purchase request not found.", ResponseStatus.NotFound, ErrorCode.NotFound);
            return Ok("Purchase request is queued for the shop workflow.", new Dictionary<string, object> { ["item"] = Shop0172PurchaseRequestPayload(request, admin: true) });
        }

        if (itemType.Equals("SaleRequest", StringComparison.OrdinalIgnoreCase))
        {
            var request = PurchaseRequests0172().Find(Shop0172IdFilter(entityId)).FirstOrDefault();
            if (request == null || Shop0172Bool(request, "IsArchived") || !Gameplay0174IsSaleRequest(request)) return Error("Sale request not found.", ResponseStatus.NotFound, ErrorCode.NotFound);
            return Ok("Sale request is queued for the shop workflow.", new Dictionary<string, object> { ["item"] = Shop0172PurchaseRequestPayload(request, admin: true) });
        }

        return Error("Unsupported queue item type.", ResponseStatus.ValidationFailed, ErrorCode.ValidationFailed);
    }

    public ResponseEnvelope GameplayPlayerGetMyGameplayStatus0174(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        Gameplay0174EnsureIndexes();
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var campaignId = Gameplay0174CampaignId(payload);

        var quests = QuestInstances0171()
            .Find(Builders<BsonDocument>.Filter.Eq("CampaignId", campaignId) & Builders<BsonDocument>.Filter.Ne("IsArchived", true))
            .Sort(Builders<BsonDocument>.Sort.Descending("UpdatedAtUtc"))
            .Limit(300)
            .ToList()
            .Where(q => Gameplay0174CanPlayerSeeQuest(actor, q))
            .ToList();
        var visibleQuestIds = new HashSet<string>(quests.Select(q => Quest0171String(q, "Id")), StringComparer.OrdinalIgnoreCase);
        var purchaseFilter = Builders<BsonDocument>.Filter.Eq("CampaignId", campaignId) & Builders<BsonDocument>.Filter.Eq("BuyerUserId", actor.Id) & Builders<BsonDocument>.Filter.Ne("IsArchived", true);
        var purchases = PurchaseRequests0172().Find(purchaseFilter).Sort(Builders<BsonDocument>.Sort.Descending("UpdatedAtUtc")).Limit(100).ToList();
        var receiptFilter = Builders<BsonDocument>.Filter.Eq("CampaignId", campaignId) & Builders<BsonDocument>.Filter.Eq("BuyerUserId", actor.Id);
        var receipts = PurchaseReceipts0172().Find(receiptFilter).Sort(Builders<BsonDocument>.Sort.Descending("CreatedAtUtc")).Limit(50).ToList();
        var restSessions = RestSessions0173()
            .Find(Builders<BsonDocument>.Filter.Eq("CampaignId", campaignId) & Builders<BsonDocument>.Filter.Ne("IsArchived", true))
            .Sort(Builders<BsonDocument>.Sort.Descending("UpdatedAtUtc"))
            .Limit(100)
            .ToList()
            .Where(rest => Rest0173CanPlayerSeeRest(rest, actor))
            .ToList();
        var downtime = DowntimeActions0173()
            .Find(Builders<BsonDocument>.Filter.Eq("PlayerUserId", actor.Id) & Builders<BsonDocument>.Filter.Ne("IsArchived", true))
            .Sort(Builders<BsonDocument>.Sort.Descending("UpdatedAtUtc"))
            .Limit(100)
            .ToList();
        var recovery = RecoveryGrants0173()
            .Find(Builders<BsonDocument>.Filter.Eq("PlayerUserId", actor.Id) & Builders<BsonDocument>.Filter.Ne("IsArchived", true))
            .Sort(Builders<BsonDocument>.Sort.Descending("UpdatedAtUtc"))
            .Limit(100)
            .ToList();
        var rewardGrants = QuestRewardGrants0171()
            .Find(Builders<BsonDocument>.Filter.Ne("IsArchived", true))
            .Sort(Builders<BsonDocument>.Sort.Descending("UpdatedAtUtc"))
            .Limit(300)
            .ToList()
            .Where(g => visibleQuestIds.Contains(Quest0171String(g, "QuestInstanceId")))
            .ToList();

        return Ok("Player gameplay status loaded.", new Dictionary<string, object>
        {
            ["activeQuests"] = quests.Where(q => Quest0171String(q, "Status") == "Active").Select(q => (object)QuestInstance0171Payload(q, admin: false, compact: true)).ToArray(),
            ["availableQuests"] = quests.Where(q => Quest0171String(q, "Status") == "Available").Select(q => (object)QuestInstance0171Payload(q, admin: false, compact: true)).ToArray(),
            ["completedQuests"] = quests.Where(q => Quest0171String(q, "Status") is "Completed" or "Failed").Select(q => (object)QuestInstance0171Payload(q, admin: false, compact: true)).ToArray(),
            ["pendingPurchases"] = purchases.Where(x => !Gameplay0174IsSaleRequest(x) && Shop0172String(x, "Status") is "PendingGmReview" or "RequiresProjectOrLicense" or "Approved").Select(x => (object)Shop0172PurchaseRequestPayload(x, admin: false)).ToArray(),
            ["pendingSales"] = purchases.Where(x => Gameplay0174IsSaleRequest(x) && Shop0172String(x, "Status") is "PendingGmReview" or "RequiresProjectOrLicense" or "Approved").Select(x => (object)Shop0172PurchaseRequestPayload(x, admin: false)).ToArray(),
            ["restStatus"] = restSessions.Select(x => (object)Rest0173SessionPayload(x, admin: false)).ToArray(),
            ["downtimeActions"] = downtime.Select(x => (object)Rest0173DowntimePayload(x, admin: false, viewer: actor)).ToArray(),
            ["rewardSummary"] = rewardGrants.Select(x => (object)QuestRewardGrant0171Payload(x, admin: false)).Concat(recovery.Select(x => (object)Rest0173GrantPayload(x, admin: false, viewer: actor))).ToArray(),
            ["recentReceipts"] = receipts.Select(x => (object)Shop0172ReceiptPayload(x, admin: false)).ToArray(),
            ["noGmData"] = true
        });
    }

    private Dictionary<string, object> Gameplay0174ProcessQuestCompletion(UserAccount actor, BsonDocument quest, string? requestId)
    {
        var bundle = Quest0171RewardBundleForQuest(Quest0171String(quest, "Id"));
        if (bundle == null)
        {
            return new Dictionary<string, object>
            {
                ["processed"] = false,
                ["reason"] = "No reward bundle is attached to this quest."
            };
        }

        var questId = Quest0171String(quest, "Id");
        var bundleId = Quest0171String(bundle, "Id");
        var existing = QuestRewardGrants0171()
            .Find(Builders<BsonDocument>.Filter.Eq("QuestInstanceId", questId)
                & Builders<BsonDocument>.Filter.Eq("RewardBundleId", bundleId)
                & Builders<BsonDocument>.Filter.Ne("IsArchived", true))
            .Sort(Builders<BsonDocument>.Sort.Descending("UpdatedAtUtc"))
            .FirstOrDefault();
        var grant = existing ?? Gameplay0174CreateQuestRewardGrant(actor, quest, bundle);
        var updated = Gameplay0174ApplyQuestRewardGrant(actor, grant, requestId, manual: false);
        return new Dictionary<string, object>
        {
            ["processed"] = true,
            ["idempotent"] = existing != null,
            ["grant"] = QuestRewardGrant0171Payload(updated, admin: true)
        };
    }

    private BsonDocument Gameplay0174CreateQuestRewardGrant(UserAccount actor, BsonDocument quest, BsonDocument bundle)
    {
        var now = DateTime.UtcNow;
        var grant = new BsonDocument
        {
            ["Id"] = Quest0171NewId("quest_grant"),
            ["QuestInstanceId"] = Quest0171String(quest, "Id"),
            ["RewardBundleId"] = Quest0171String(bundle, "Id"),
            ["TargetCharacterIds"] = new BsonArray(Quest0171ArrayValues(quest, "AssignedCharacterIds")),
            ["TargetPlayerUserIds"] = new BsonArray(Quest0171ArrayValues(quest, "AssignedPlayerUserIds")),
            ["Status"] = "PendingGmApply",
            ["PlayerVisibleSummary"] = Quest0171RewardSummary(bundle, admin: false),
            ["GmSummary"] = Quest0171RewardSummary(bundle, admin: true),
            ["GrantResults"] = new BsonArray(),
            ["IdempotencyKey"] = $"quest-complete:{Quest0171String(quest, "Id")}:{Quest0171String(bundle, "Id")}",
            ["CompletionProcessed"] = false,
            ["AppliedAtUtc"] = BsonNull.Value,
            ["AppliedByUserId"] = string.Empty,
            ["CreatedAtUtc"] = now,
            ["UpdatedAtUtc"] = now,
            ["CreatedByUserId"] = actor.Id,
            ["UpdatedByUserId"] = actor.Id,
            ["Revision"] = 1,
            ["IsArchived"] = false,
            ["SchemaVersion"] = "0.17.4"
        };
        QuestRewardGrants0171().InsertOne(grant);
        Quest0171Audit(actor, CommandNames.QuestAdminComplete, Quest0171String(quest, "Id"), "quest.reward.grant.created", null, grant, "Quest completion reward grant created.");
        Quest0171Sync("quest.reward.grant.created", "quest_reward_grant", Quest0171String(grant, "Id"), "created", actor.Id, string.Empty);
        return grant;
    }

    private BsonDocument Gameplay0174ApplyQuestRewardGrant(UserAccount actor, BsonDocument grant, string? requestId, bool manual)
    {
        var status = Quest0171String(grant, "Status");
        if (status == "Applied" && Quest0171Bool(grant, "CompletionProcessed", true))
            return grant;

        var bundle = QuestRewardBundles0171().Find(Quest0171IdFilter(Quest0171String(grant, "RewardBundleId"))).FirstOrDefault();
        var before = grant.DeepClone().AsBsonDocument;
        var findings = new BsonArray(Quest0171ArrayValues(grant, "GrantResults"));
        if (bundle == null)
        {
            grant["Status"] = "PendingGmApply";
            findings.Add("Reward bundle is missing; GM must resolve manually.");
        }
        else
        {
            var hasStructured = Gameplay0174HasStructuredReward(bundle);
            var hasUnsafe = Quest0171Bool(bundle, "RequiresGmApply", true)
                || !string.IsNullOrWhiteSpace(Quest0171String(bundle, "CustomRewardText"))
                || !string.IsNullOrWhiteSpace(Quest0171String(bundle, "GmDescription"));
            if (manual)
            {
                grant["Status"] = "Applied";
                grant["AppliedAtUtc"] = DateTime.UtcNow;
                grant["AppliedByUserId"] = actor.Id;
                findings.Add("GM explicitly applied the reward grant. No legacy Character document write was used.");
            }
            else if (hasUnsafe && hasStructured)
            {
                grant["Status"] = "PartiallyApplied";
                findings.Add("Safe structured reward was recorded idempotently; GM-only/custom reward remains pending for manual resolution.");
            }
            else if (hasUnsafe)
            {
                grant["Status"] = "PendingGmApply";
                findings.Add("Reward contains GM-only/custom effects and remains pending GM application.");
            }
            else
            {
                grant["Status"] = "Applied";
                grant["AppliedAtUtc"] = DateTime.UtcNow;
                grant["AppliedByUserId"] = actor.Id;
                findings.Add(hasStructured
                    ? "Structured reward recorded idempotently in quest_reward_grants. No unsafe profile mutation was attempted."
                    : "Quest completed with an empty reward bundle; grant marked applied for idempotency.");
            }
        }

        grant["CompletionProcessed"] = true;
        grant["GrantResults"] = new BsonArray(findings.Select(x => x.ToString()).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase));
        Quest0171Touch(grant, actor.Id);
        QuestRewardGrants0171().ReplaceOne(Quest0171IdFilter(Quest0171String(grant, "Id")), grant);
        Quest0171Audit(actor, manual ? CommandNames.QuestAdminApplyRewardGrant : CommandNames.QuestAdminComplete, Quest0171String(grant, "QuestInstanceId"), manual ? "quest.reward.grant.applied" : "quest.reward.grant.processed", before, grant, manual ? "Quest reward grant applied manually." : "Quest completion reward processed.");
        Quest0171Sync("quest.reward.grant.updated", "quest_reward_grant", Quest0171String(grant, "Id"), manual ? "applied" : "processed", actor.Id, requestId ?? string.Empty);
        Gameplay0174Sync(actor.Id, "quest_reward_grant", Quest0171String(grant, "Id"), "updated", requestId);
        return grant;
    }

    private BsonDocument Gameplay0174ApplyRestRecoveryGrant(UserAccount actor, BsonDocument grant, string? requestId)
    {
        if (Rest0173String(grant, "Status") == "Applied") return grant;
        var before = grant.DeepClone().AsBsonDocument;
        grant["Status"] = "Applied";
        grant["AppliedAtUtc"] = DateTime.UtcNow;
        grant["AppliedByUserId"] = actor.Id;
        grant["AppliedEffects"] = "GM подтвердил восстановление вручную. Автоматическая мутация HP/condition не выполнялась без безопасного профильного адаптера.";
        Rest0173Touch(grant, actor.Id);
        RecoveryGrants0173().ReplaceOne(Rest0173IdFilter(Rest0173String(grant, "Id")), grant);
        Rest0173Audit(actor, CommandNames.GameplayAdminResolveQueueItem, Rest0173String(grant, "RestSessionId"), string.Empty, Rest0173String(grant, "Id"), "rest.recovery.grant.applied", before, grant, "Recovery grant applied through gameplay queue.");
        Rest0173Sync("rest.recovery.grant.applied", "recovery_grant", Rest0173String(grant, "Id"), "applied", actor.Id, requestId ?? string.Empty);
        Gameplay0174Sync(actor.Id, "recovery_grant", Rest0173String(grant, "Id"), "updated", requestId);
        return grant;
    }

    private void Gameplay0174EnsureIndexes()
    {
        EnsureQuest0171Indexes();
        EnsureShop0172Indexes();
        EnsureRest0173Indexes();
    }

    private IEnumerable<BsonDocument> Gameplay0174PurchaseRequests(string campaignId)
        => PurchaseRequests0172().Find(Builders<BsonDocument>.Filter.Eq("CampaignId", campaignId)
            & Builders<BsonDocument>.Filter.In("Status", new[] { "PendingGmReview", "RequiresProjectOrLicense", "Approved" })
            & Builders<BsonDocument>.Filter.Ne("IsArchived", true))
            .Sort(Builders<BsonDocument>.Sort.Descending("UpdatedAtUtc"))
            .Limit(200)
            .ToList();

    private IEnumerable<BsonDocument> Gameplay0174QuestRewardGrants(string campaignId)
    {
        var questIds = QuestInstances0171().Find(Builders<BsonDocument>.Filter.Eq("CampaignId", campaignId) & Builders<BsonDocument>.Filter.Ne("IsArchived", true))
            .Limit(1000)
            .ToList()
            .Select(x => Quest0171String(x, "Id"))
            .ToArray();
        if (questIds.Length == 0) return Array.Empty<BsonDocument>();
        return QuestRewardGrants0171().Find(Builders<BsonDocument>.Filter.In("QuestInstanceId", questIds)
            & Builders<BsonDocument>.Filter.In("Status", new[] { "PendingGmApply", "PartiallyApplied", "RequiresProjectOrLicense" })
            & Builders<BsonDocument>.Filter.Ne("IsArchived", true))
            .Sort(Builders<BsonDocument>.Sort.Descending("UpdatedAtUtc"))
            .Limit(200)
            .ToList();
    }

    private IEnumerable<BsonDocument> Gameplay0174RecoveryGrants(string campaignId)
        => RecoveryGrants0173().Find(Builders<BsonDocument>.Filter.Eq("CampaignId", campaignId)
            & Builders<BsonDocument>.Filter.In("Status", new[] { "PendingGmApply", "RequiresProjectOrLicense" })
            & Builders<BsonDocument>.Filter.Ne("IsArchived", true))
            .Sort(Builders<BsonDocument>.Sort.Descending("UpdatedAtUtc"))
            .Limit(200)
            .ToList();

    private IEnumerable<BsonDocument> Gameplay0174RestSessions(string campaignId)
        => RestSessions0173().Find(Builders<BsonDocument>.Filter.Eq("CampaignId", campaignId)
            & Builders<BsonDocument>.Filter.In("Status", new[] { "Active", "Interrupted", "Completed" })
            & Builders<BsonDocument>.Filter.Ne("IsArchived", true))
            .Sort(Builders<BsonDocument>.Sort.Descending("UpdatedAtUtc"))
            .Limit(100)
            .ToList();

    private IEnumerable<BsonDocument> Gameplay0174DowntimeActions(string campaignId)
        => DowntimeActions0173().Find(Builders<BsonDocument>.Filter.Eq("CampaignId", campaignId)
            & Builders<BsonDocument>.Filter.Ne("IsArchived", true))
            .Sort(Builders<BsonDocument>.Sort.Descending("UpdatedAtUtc"))
            .Limit(200)
            .ToList();

    private static Dictionary<string, object> Gameplay0174QueueItem(string type, BsonDocument doc, string category, string status, string actor, string entityId, string summary)
        => new()
        {
            ["queueItemId"] = $"{type}:{entityId}",
            ["itemType"] = type,
            ["category"] = category,
            ["status"] = status,
            ["actor"] = actor,
            ["entityId"] = entityId,
            ["title"] = string.IsNullOrWhiteSpace(summary) ? category : summary,
            ["summary"] = summary,
            ["updatedAtUtc"] = Gameplay0174Date(doc, "UpdatedAtUtc")
        };

    private Dictionary<string, object> Gameplay0174QuestSummary(string campaignId)
    {
        var quests = QuestInstances0171().Find(Builders<BsonDocument>.Filter.Eq("CampaignId", campaignId) & Builders<BsonDocument>.Filter.Ne("IsArchived", true)).Limit(1000).ToList();
        return new Dictionary<string, object>
        {
            ["active"] = quests.Count(x => Quest0171String(x, "Status") == "Active"),
            ["available"] = quests.Count(x => Quest0171String(x, "Status") == "Available"),
            ["completed"] = quests.Count(x => Quest0171String(x, "Status") == "Completed"),
            ["failed"] = quests.Count(x => Quest0171String(x, "Status") == "Failed")
        };
    }

    private object[] Gameplay0174AuditSummary(string campaignId)
    {
        var questIds = QuestInstances0171().Find(Builders<BsonDocument>.Filter.Eq("CampaignId", campaignId)).Limit(500).ToList().Select(x => Quest0171String(x, "Id")).ToArray();
        var questAudit = questIds.Length == 0
            ? Array.Empty<object>()
            : QuestAudit0171().Find(Builders<BsonDocument>.Filter.In("QuestInstanceId", questIds)).Sort(Builders<BsonDocument>.Sort.Descending("CreatedAtUtc")).Limit(20).ToList().Select(x => (object)Quest0171DocPayload(x, admin: true)).ToArray();
        return questAudit;
    }

    private bool Gameplay0174CanPlayerSeeQuest(UserAccount actor, BsonDocument quest)
    {
        if (Quest0171Bool(quest, "IsArchived")) return false;
        var status = Quest0171String(quest, "Status");
        if (status is "Draft" or "Cancelled" or "Archived") return false;
        var visibility = Quest0171String(quest, "Visibility");
        if (visibility is "Hidden" or "GmOnly") return false;
        if (Quest0171ArrayValues(quest, "AssignedPlayerUserIds").Contains(actor.Id, StringComparer.OrdinalIgnoreCase)) return true;
        return visibility is "PlayerVisible" or "PartyVisible";
    }

    private bool Gameplay0174IsPersonnelPurchase(BsonDocument request)
    {
        var offer = Shop0172FindOffer(Shop0172String(request, "OfferId"));
        if (offer == null) return false;
        var type = Shop0172String(offer, "OfferType");
        return type is "Personnel" or "Companion" or "Slave" or "Contract";
    }

    private static bool Gameplay0174IsSaleRequest(BsonDocument request)
        => Shop0172String(request, "TransactionType", "Purchase").Equals("Sell", StringComparison.OrdinalIgnoreCase);

    private static bool Gameplay0174HasStructuredReward(BsonDocument bundle)
        => !string.IsNullOrWhiteSpace(Quest0171String(bundle, "MoneyRewards"))
            || !string.IsNullOrWhiteSpace(Quest0171String(bundle, "ExperienceCoinRewards"))
            || !string.IsNullOrWhiteSpace(Quest0171String(bundle, "ItemRewardRefs"))
            || !string.IsNullOrWhiteSpace(Quest0171String(bundle, "KnowledgeRewardRefs"))
            || !string.IsNullOrWhiteSpace(Quest0171String(bundle, "ReputationRewardRefs"))
            || !string.IsNullOrWhiteSpace(Quest0171String(bundle, "UnlockRewardRefs"));

    private void Gameplay0174Sync(string actorUserId, string entityType, string entityId, string operation, string? requestId)
    {
        TryPublishSyncEvent("gameplay.changed", "gameplay", entityType, entityId, operation, actorUserId, new Dictionary<string, object>
        {
            ["entityType"] = entityType,
            ["entityId"] = entityId,
            ["operation"] = operation
        }, requestId ?? string.Empty);
    }

    private static string Gameplay0174CampaignId(IDictionary<string, object> payload)
        => RequireLength(FirstNonEmpty(PayloadReader.GetString(payload, "campaignId"), "dev-campaign-core"), 1, 128, "campaignId");

    private static string Gameplay0174Text(IDictionary<string, object> payload, string key, string fallback, int max)
        => RequireLength(FirstNonEmpty(PayloadReader.GetString(payload, key), fallback), 0, max, key);

    private static string Gameplay0174Date(BsonDocument doc, string key)
    {
        if (!doc.Contains(key) || doc[key].IsBsonNull) return string.Empty;
        if (doc[key].IsValidDateTime) return doc[key].ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
        return Convert.ToString(BsonTypeMapper.MapToDotNetValue(doc[key]), CultureInfo.InvariantCulture) ?? string.Empty;
    }
}
