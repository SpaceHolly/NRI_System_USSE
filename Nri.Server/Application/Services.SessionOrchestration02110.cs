using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Driver;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;
using Nri.Shared.Utilities;

namespace Nri.Server.Application;

public partial class ServiceHub
{
    public ResponseEnvelope SessionAttentionGet02110(CommandContext context)
    {
        var campaignId = ResolveRequestedCampaign02110(context);
        _campaignAuthorization.RequireCampaignCapability(context.Session!, campaignId, CampaignCapabilityIds.SessionViewGMData);
        var sessionId = PayloadReader.GetString(context.Request.Payload, "sessionId");
        CurrentSessionState? session = null;
        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            session = _repositories.CurrentSessions.Find(Builders<CurrentSessionState>.Filter.Eq(x => x.SessionId, sessionId)).FirstOrDefault();
            if (session == null || !string.Equals(session.CampaignId, campaignId, StringComparison.Ordinal)) throw new KeyNotFoundException("Session not found.");
        }
        var attention = new List<SessionAttentionItem>();
        attention.AddRange(_repositories.PlayerRequests.Find(
                Builders<PlayerRequestState>.Filter.Eq(x => x.CampaignId, campaignId)
                & Builders<PlayerRequestState>.Filter.In(x => x.Status, new[] { PlayerRequestStatusIds.Submitted, PlayerRequestStatusIds.InReview, PlayerRequestStatusIds.ChangesRequested }))
            .Select(x => new SessionAttentionItem { SourceType = "player_request", SourceId = x.Id, Title = FirstNonEmpty(x.Title, "Заявка игрока"), Severity = "medium", CreatedAtUtc = x.CreatedAtUtc, ActorDisplayName = x.CreatedByDisplayName, ActionRoute = "requests" }));
        attention.AddRange(_mongo.CharacterCreationDrafts.Find(x => x.CampaignId == campaignId
                && x.Status == CharacterCreationDraftStatusIds.Submitted && !x.IsArchived).ToList()
            .Select(x => new SessionAttentionItem
            {
                SourceType = "character_creation_draft",
                SourceId = x.Id,
                Title = "Создание персонажа: " + FirstNonEmpty(x.DisplayName, "Без имени"),
                Severity = "medium",
                CreatedAtUtc = x.UpdatedUtc,
                ActorDisplayName = _repositories.Accounts.GetById(x.OwnerUserId)?.Login ?? "Игрок",
                ActionRoute = "admin.character_creation"
            }));
        if (session != null)
        {
            attention.AddRange(_repositories.AutomationExecutions.Find(
                    Builders<AutomationExecutionRecord>.Filter.Eq(x => x.CampaignId, campaignId)
                    & Builders<AutomationExecutionRecord>.Filter.Eq(x => x.SessionId, session.SessionId)
                    & Builders<AutomationExecutionRecord>.Filter.In(x => x.Status, new[] { AutomationExecutionStatusIds.Proposed, AutomationExecutionStatusIds.Failed }))
                .Select(x => new SessionAttentionItem { SourceType = "automation", SourceId = x.Id, Title = FirstNonEmpty(x.ReadableResult, "Решение автоматизации"), Severity = x.Status == AutomationExecutionStatusIds.Failed ? "high" : "medium", CreatedAtUtc = x.CreatedUtc, ActionRoute = "automation" }));
        }
        var payload = attention.OrderByDescending(x => x.Severity).ThenBy(x => x.CreatedAtUtc).Select(x => (object)new Dictionary<string, object>
        {
            ["title"] = x.Title,
            ["severity"] = x.Severity,
            ["createdAtUtc"] = x.CreatedAtUtc,
            ["actor"] = x.ActorDisplayName,
            ["route"] = x.ActionRoute,
            ["sourceType"] = x.SourceType,
            ["sourceId"] = x.SourceId
        }).ToArray();
        return Ok(session == null ? "Задачи кампании загружены." : "Задачи сессии загружены.", new Dictionary<string, object>
        {
            ["items"] = payload, ["count"] = payload.Length, ["scope"] = session == null ? "campaign" : "session", ["campaignId"] = campaignId
        });
    }

    public ResponseEnvelope AutomationPolicyList02110(CommandContext context)
    {
        var campaignId = ResolveRequestedCampaign02110(context);
        _campaignAuthorization.RequireCampaignCapability(context.Session!, campaignId, CampaignCapabilityIds.AutomationView);
        var items = _repositories.AutomationPolicies.Find(Builders<AutomationPolicyDefinition>.Filter.Eq(x => x.CampaignId, campaignId))
            .Where(x => !x.Archived && !x.Deleted)
            .OrderBy(x => x.Priority)
            .Select(AutomationPolicyPayload02110).Cast<object>().ToArray();
        return Ok("Политики автоматизации загружены.", new Dictionary<string, object> { ["policies"] = items, ["count"] = items.Length });
    }

    public ResponseEnvelope AutomationPolicyUpdate02110(CommandContext context)
    {
        var actor = GetCurrentAccount(context);
        var campaignId = ResolveRequestedCampaign02110(context);
        _campaignAuthorization.RequireCampaignCapability(context.Session!, campaignId, CampaignCapabilityIds.AutomationManage);
        var payload = context.Request.Payload ?? new Dictionary<string, object>();
        var policyId = RequireLength(PayloadReader.GetString(payload, "policyId"), 0, 128, "policyId");
        var policy = string.IsNullOrWhiteSpace(policyId) ? null : _repositories.AutomationPolicies.GetById(policyId);
        if (policy != null && !string.Equals(policy.CampaignId, campaignId, StringComparison.Ordinal)) throw new KeyNotFoundException("Automation policy not found.");
        var isNew = policy == null;
        policy ??= new AutomationPolicyDefinition
        {
            CampaignId = campaignId,
            DisplayName = RequireLength(PayloadReader.GetString(payload, "name"), 3, 160, "name"),
            TriggerKind = NormalizeAutomationTrigger02110(PayloadReader.GetString(payload, "trigger")),
            TargetDomainAction = NormalizeAutomationAction02110(PayloadReader.GetString(payload, "action")),
            DecisionMode = NormalizeAutomationDecisionMode02110(PayloadReader.GetString(payload, "decisionMode")),
            GMDescription = RequireLength(PayloadReader.GetString(payload, "description"), 0, 1000, "description"),
            Enabled = payload.ContainsKey("enabled") && PayloadReader.GetBool(payload, "enabled")
        };
        if (!isNew && payload.ContainsKey("name")) policy.DisplayName = RequireLength(PayloadReader.GetString(payload, "name"), 3, 160, "name");
        if (!isNew && payload.ContainsKey("trigger")) policy.TriggerKind = NormalizeAutomationTrigger02110(PayloadReader.GetString(payload, "trigger"));
        if (!isNew && payload.ContainsKey("action")) policy.TargetDomainAction = NormalizeAutomationAction02110(PayloadReader.GetString(payload, "action"));
        var expectedRevision = Convert.ToInt64(payload.TryGetValue("expectedRevision", out var raw) ? raw : policy.EntityRevision);
        if (expectedRevision != policy.EntityRevision) throw new InvalidOperationException("Политика уже изменена. Обновите данные.");
        if (payload.ContainsKey("enabled")) policy.Enabled = PayloadReader.GetBool(payload, "enabled");
        if (payload.ContainsKey("decisionMode")) policy.DecisionMode = NormalizeAutomationDecisionMode02110(PayloadReader.GetString(payload, "decisionMode"));
        if (isNew) _repositories.AutomationPolicies.Insert(policy);
        else { policy.EntityRevision++; _repositories.AutomationPolicies.Replace(policy); }
        WriteAudit("automation", actor.Id, isNew ? "policy.create" : "policy.update", $"{policy.Id}:{policy.EntityRevision}");
        return Ok(isNew ? "Политика автоматизации создана." : "Политика автоматизации обновлена.", new Dictionary<string, object> { ["policy"] = AutomationPolicyPayload02110(policy) });
    }

    public ResponseEnvelope AutomationPolicyDryRun02110(CommandContext context)
    {
        var campaignId = ResolveRequestedCampaign02110(context);
        _campaignAuthorization.RequireCampaignCapability(context.Session!, campaignId, CampaignCapabilityIds.AutomationManage);
        var policyId = RequireLength(PayloadReader.GetString(context.Request.Payload, "policyId"), 1, 128, "policyId");
        var policy = _repositories.AutomationPolicies.GetById(policyId);
        if (policy == null || !string.Equals(policy.CampaignId, campaignId, StringComparison.Ordinal)) throw new KeyNotFoundException("Automation policy not found.");
        var wouldPropose = policy.Enabled && policy.DecisionMode == AutomationDecisionModeIds.RequireGMApproval;
        var result = policy.Enabled ? policy.DecisionMode == AutomationDecisionModeIds.AutoApplySafe ? "Безопасное действие может быть выполнено через основной сервис." : wouldPropose ? "Будет создано предложение для решения GM в общей очереди." : "Будет создано уведомление без изменения данных." : "Политика выключена.";
        return Ok("Предварительная проверка выполнена без изменений.", new Dictionary<string, object>
        {
            ["dryRun"] = true, ["wouldMutate"] = false, ["wouldPropose"] = wouldPropose,
            ["result"] = result, ["targetAction"] = policy.TargetDomainAction, ["decisionMode"] = AutomationDecisionModeDisplay02110(policy.DecisionMode)
        });
    }

    public ResponseEnvelope AutomationExecutionList02110(CommandContext context)
    {
        var campaignId = ResolveRequestedCampaign02110(context);
        _campaignAuthorization.RequireCampaignCapability(context.Session!, campaignId, CampaignCapabilityIds.AutomationView);
        var sessionId = PayloadReader.GetString(context.Request.Payload, "sessionId");
        var filter = Builders<AutomationExecutionRecord>.Filter.Eq(x => x.CampaignId, campaignId);
        if (!string.IsNullOrWhiteSpace(sessionId)) filter &= Builders<AutomationExecutionRecord>.Filter.Eq(x => x.SessionId, sessionId);
        var items = _repositories.AutomationExecutions.Find(filter).OrderByDescending(x => x.CreatedUtc).Take(100).Select(x => (object)new Dictionary<string, object>
        {
            ["policyId"] = x.PolicyId, ["trigger"] = x.Trigger, ["action"] = x.TargetAction, ["status"] = x.Status,
            ["result"] = x.ReadableResult, ["createdAtUtc"] = x.CreatedUtc, ["decisionMode"] = AutomationDecisionModeDisplay02110(x.DecisionMode)
        }).ToArray();
        return Ok("История автоматизации загружена.", new Dictionary<string, object> { ["executions"] = items, ["count"] = items.Length });
    }

    private static Dictionary<string, object> AutomationPolicyPayload02110(AutomationPolicyDefinition x) => new()
    {
        ["policyId"] = x.Id, ["name"] = x.DisplayName, ["trigger"] = x.TriggerKind,
        ["action"] = x.TargetDomainAction, ["decisionMode"] = AutomationDecisionModeDisplay02110(x.DecisionMode),
        ["enabled"] = x.Enabled, ["description"] = x.GMDescription, ["revision"] = x.EntityRevision
    };

    private static string NormalizeAutomationDecisionMode02110(string mode) => mode?.Trim().ToLowerInvariant() switch
    {
        "auto_apply_safe" => AutomationDecisionModeIds.AutoApplySafe,
        "require_gm_approval" => AutomationDecisionModeIds.RequireGMApproval,
        "notify_only" => AutomationDecisionModeIds.NotifyOnly,
        "disabled" => AutomationDecisionModeIds.Disabled,
        _ => throw new ArgumentException("Неизвестный режим решения.")
    };

    private static string NormalizeAutomationTrigger02110(string trigger) => trigger?.Trim().ToLowerInvariant() switch
    {
        "combat.started" => "combat.started",
        "combat.ended" => "combat.ended",
        "weather.exposure.harmful" => "weather.exposure.harmful",
        _ => throw new ArgumentException("Неизвестный тип события автоматизации.")
    };

    private static string NormalizeAutomationAction02110(string action) => action?.Trim().ToLowerInvariant() switch
    {
        "session.link_active_combat" => "session.link_active_combat",
        "session.attention.notify" => "session.attention.notify",
        "resolution_queue.propose_exposure" => "resolution_queue.propose_exposure",
        _ => throw new ArgumentException("Неизвестное типизированное действие автоматизации.")
    };

    private void EvaluateAutomationEvent02110(CurrentSessionState session, string trigger, string sourceId, string actorId)
    {
        var policies = _repositories.AutomationPolicies.Find(
            Builders<AutomationPolicyDefinition>.Filter.Eq(x => x.CampaignId, session.CampaignId)
            & Builders<AutomationPolicyDefinition>.Filter.Eq(x => x.TriggerKind, trigger)
            & Builders<AutomationPolicyDefinition>.Filter.Eq(x => x.Enabled, true))
            .Where(x => !x.Archived && !x.Deleted)
            .OrderBy(x => x.Priority).ToArray();
        foreach (var policy in policies)
        {
            var operationId = $"automation:{policy.Id}:{trigger}:{sourceId}";
            if (_repositories.AutomationExecutions.Find(Builders<AutomationExecutionRecord>.Filter.Eq(x => x.OperationId, operationId)).Any()) continue;
            var record = new AutomationExecutionRecord
            {
                CampaignId = session.CampaignId,
                SessionId = session.SessionId,
                PolicyId = policy.Id,
                Trigger = trigger,
                DecisionMode = policy.DecisionMode,
                TargetAction = policy.TargetDomainAction,
                OperationId = operationId,
                CorrelationId = sourceId,
                CausationId = sourceId,
                AutomationDepth = 1
            };
            if (policy.DecisionMode == AutomationDecisionModeIds.Disabled) continue;
            if (policy.DecisionMode == AutomationDecisionModeIds.AutoApplySafe && policy.TargetDomainAction == "session.link_active_combat" && trigger == "combat.started")
            {
                record.Status = AutomationExecutionStatusIds.Applied;
                record.AppliedAtUtc = DateTime.UtcNow;
                record.ReadableResult = "Активный бой уже связан с сессией канонической командой.";
            }
            else if (policy.DecisionMode == AutomationDecisionModeIds.NotifyOnly)
            {
                record.Status = AutomationExecutionStatusIds.Proposed;
                record.ReadableResult = "Событие требует внимания GM; данные не изменены.";
            }
            else if (policy.DecisionMode == AutomationDecisionModeIds.RequireGMApproval)
            {
                record.Status = AutomationExecutionStatusIds.Proposed;
                record.ReadableResult = "Предложение ожидает решения GM в общей очереди.";
            }
            else
            {
                record.Status = AutomationExecutionStatusIds.Failed;
                record.FailureCategory = "unsupported_safe_action";
                record.ReadableResult = "Действие не входит в разрешённый безопасный набор.";
            }
            _repositories.AutomationExecutions.Insert(record);
            WriteAudit("automation", actorId, "execution.evaluate", $"{record.Id}:{record.Status}:{operationId}");
        }
    }

    private static string AutomationDecisionModeDisplay02110(string mode) => mode switch
    {
        AutomationDecisionModeIds.AutoApplySafe => "Применять безопасные действия",
        AutomationDecisionModeIds.RequireGMApproval => "Требовать решения GM",
        AutomationDecisionModeIds.NotifyOnly => "Только уведомлять",
        _ => "Выключено"
    };
}
