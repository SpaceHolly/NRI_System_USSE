using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nri.Server.Infrastructure.Mongo.Repositories;
using Nri.Server.Logging;
using Nri.Shared.Domain;

namespace Nri.Server.Application.Services;

public interface ICombatDamageApplicationService
{
    Task<CombatVitalsSetResponse> SetParticipantVitalsAsync(CombatParticipantVitalsSetRequest request, UserAccount actor);
    Task<CombatDamageResultResponse> ApplyDamageAsync(CombatDamageApplyRequest request, UserAccount actor);
    CombatDamageResultResponse CalculateDamageApplication(CombatDamageApplyRequest request, CombatParticipantState target);
    void MarkDefeatedIfNeeded(CombatDamageApplyRequest request, CombatParticipantState target, CombatDamageResultResponse result);
    CombatActionState BuildDamageActionState(CombatDamageApplyRequest request, CombatEncounterState encounter, CombatDamageResultResponse result, UserAccount actor);
    string BuildDamageLogMessage(CombatParticipantState target, CombatDamageResultResponse result);
}

public sealed class CombatDamageApplicationService : ICombatDamageApplicationService
{
    private readonly ICombatEncounterRepository _encounters;
    private readonly ICombatParticipantRepository _participants;
    private readonly ICombatActionRepository _actions;
    private readonly ICombatLogWriter _logWriter;
    private readonly ICombatSnapshotService _snapshotService;
    private readonly ICombatPayloadSummaryBuilder _payloadSummaryBuilder;
    private readonly IServerLogger _logger;

    public CombatDamageApplicationService(
        ICombatEncounterRepository encounters,
        ICombatParticipantRepository participants,
        ICombatActionRepository actions,
        ICombatLogWriter logWriter,
        ICombatSnapshotService snapshotService,
        ICombatPayloadSummaryBuilder payloadSummaryBuilder,
        IServerLogger logger)
    {
        _encounters = encounters;
        _participants = participants;
        _actions = actions;
        _logWriter = logWriter;
        _snapshotService = snapshotService;
        _payloadSummaryBuilder = payloadSummaryBuilder;
        _logger = logger;
    }

    private static bool TemporaryHealthEnabled => CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatTemporaryHealth));
    private static bool AutoDefeatEnabled => CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatAutoDefeatOnZeroHealth));
    private static bool DamageActionLoggingEnabled => CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatDamageActionLogging));

    public async Task<CombatVitalsSetResponse> SetParticipantVitalsAsync(CombatParticipantVitalsSetRequest request, UserAccount actor)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        var encounter = await RequireEncounterAsync(request.EncounterId);
        var participant = await RequireParticipantAsync(request.ParticipantId, encounter.Id, "participant_missing");

        ValidateVitals(request);
        participant.MaxHealth = request.MaxHealth;
        participant.CurrentHealth = request.CurrentHealth;
        participant.TemporaryHealth = request.TemporaryHealth;
        participant.MaxMorale = request.MaxMorale;
        participant.CurrentMorale = request.CurrentMorale;
        participant.UpdatedUtc = DateTime.UtcNow;
        await _participants.UpsertAsync(participant);

        await _logWriter.AppendLogAndReplayAsync(
            new CombatLogWriteRequest
            {
                EncounterId = encounter.Id,
                CampaignId = encounter.CampaignId,
                SessionId = encounter.SessionId,
                RoundNumber = encounter.RoundNumber,
                TurnIndex = encounter.ActiveTurnIndex,
                ActorParticipantId = participant.Id,
                ActorUserId = actor?.Id ?? string.Empty,
                EventType = CombatEventTypes.ParticipantVitalsSet,
                Message = string.IsNullOrWhiteSpace(request.Reason) ? "Participant vitals set." : request.Reason,
                SourcePayload = BuildVitalsPayload(participant),
                Visibility = CombatVisibilityIds.GmOnly,
                RequestId = request.RequestId ?? string.Empty
            },
            new CombatReplayWriteRequest
            {
                EncounterId = encounter.Id,
                EventType = CombatEventTypes.ParticipantVitalsSet,
                RoundNumber = encounter.RoundNumber,
                TurnIndex = encounter.ActiveTurnIndex,
                ActorParticipantId = participant.Id,
                SourcePayload = BuildVitalsPayload(participant),
                Visibility = CombatVisibilityIds.GmOnly,
                RequestId = request.RequestId ?? string.Empty
            });

        _logger.Admin($"combat.vitals.set.done participantId={participant.Id}");
        return new CombatVitalsSetResponse
        {
            EncounterId = encounter.Id,
            ParticipantId = participant.Id,
            MaxHealth = participant.MaxHealth,
            CurrentHealth = participant.CurrentHealth,
            TemporaryHealth = participant.TemporaryHealth,
            MaxMorale = participant.MaxMorale,
            CurrentMorale = participant.CurrentMorale,
            Message = "Participant vitals set.",
            Snapshot = await SnapshotAsync(encounter.Id, actor)
        };
    }

    public async Task<CombatDamageResultResponse> ApplyDamageAsync(CombatDamageApplyRequest request, UserAccount actor)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (string.IsNullOrWhiteSpace(request.RequestId)) throw new ArgumentException("operation_id_required", nameof(request));
        _logger.Debug($"combat.damage.apply.start encounterId={request.EncounterId} target={request.TargetParticipantId}");

        var encounter = await RequireEncounterAsync(request.EncounterId);
        var replayAction = await _actions.GetByRequestIdAsync(encounter.Id, request.RequestId, request.AttackerParticipantId);
        if (replayAction != null && string.Equals(replayAction.ActionType, CombatActionTypes.Damage, StringComparison.OrdinalIgnoreCase))
            return await BuildReplayResponseAsync(encounter, replayAction, actor);

        var target = await RequireParticipantAsync(request.TargetParticipantId, encounter.Id, "target_missing");
        if (!string.IsNullOrWhiteSpace(request.AttackerParticipantId))
            await RequireParticipantAsync(request.AttackerParticipantId, encounter.Id, "attacker_missing");
        if (request.DamageAmount < 0) throw new ArgumentOutOfRangeException(nameof(request.DamageAmount), "damage_amount_negative");

        var result = CalculateDamageApplication(request, target);
        ApplyCalculatedDamage(request, target, result);
        MarkDefeatedIfNeeded(request, target, result);
        await _participants.UpsertAsync(target);

        if (DamageActionLoggingEnabled)
        {
            var action = BuildDamageActionState(request, encounter, result, actor);
            action.PayloadSummary = _payloadSummaryBuilder.BuildLogPayloadSummary(CombatEventTypes.DamageApplied, action.PayloadSummary);
            await _actions.AppendAsync(action);
            result.ActionId = action.Id;
            var message = BuildDamageLogMessage(target, result);
            await WriteDamageLogAsync(encounter, request, result, message, actor);
            result.Message = message;
        }
        else
        {
            result.Warnings.Add("damage_action_logging_disabled");
            result.Message = BuildDamageLogMessage(target, result);
        }

        result.Snapshot = await SnapshotAsync(encounter.Id, actor);
        _logger.Debug($"combat.damage.apply.done target={target.Id} damageApplied={result.DamageApplied} currentHealth={target.CurrentHealth}");
        return result;
    }

    private async Task<CombatDamageResultResponse> BuildReplayResponseAsync(CombatEncounterState encounter, CombatActionState action, UserAccount actor)
    {
        var payload = action.PayloadSummary ?? new Dictionary<string, object>();
        var response = new CombatDamageResultResponse
        {
            EncounterId = encounter.Id,
            SourceActionId = Text(payload, "sourceActionId"),
            AttackerParticipantId = Text(payload, "attackerParticipantId"),
            TargetParticipantId = Text(payload, "targetParticipantId"),
            DamageAmount = Number(payload, "damageAmount"),
            DamageApplied = Number(payload, "damageApplied"),
            DamagePrevented = Number(payload, "damagePrevented"),
            DamageType = Text(payload, "damageType"),
            PreviousHealth = Number(payload, "previousHealth"),
            CurrentHealth = Number(payload, "currentHealth"),
            ResourceType = Text(payload, "resourceType"),
            PreviousResource = Number(payload, "previousResource"),
            CurrentResource = Number(payload, "currentResource"),
            PreviousTemporaryHealth = Number(payload, "previousTemporaryHealth"),
            CurrentTemporaryHealth = Number(payload, "currentTemporaryHealth"),
            TargetDefeated = Flag(payload, "targetDefeated"),
            DefeatedReason = Text(payload, "defeatedReason"),
            ActionId = action.Id,
            AlreadyApplied = true,
            Message = "Урон уже был применён; возвращён сохранённый результат без повторного списания."
        };
        response.Warnings.Add("damage_idempotent_replay_no_reapply");
        response.Snapshot = await SnapshotAsync(encounter.Id, actor);
        _logger.Debug($"combat.damage.apply.replay actionId={action.Id} requestId={action.RequestId}");
        return response;
    }

    public CombatDamageResultResponse CalculateDamageApplication(CombatDamageApplyRequest request, CombatParticipantState target)
    {
        var damageType = NormalizeDamageType(request.DamageType);
        var warnings = new List<string>();
        if (string.IsNullOrWhiteSpace(request.DamageType)) warnings.Add("damage_type_defaulted");
        if (target.IsDefeated) warnings.Add("target_already_defeated");
        if (request.DamageAmount == 0) warnings.Add("damage_amount_zero");
        if (!TemporaryHealthEnabled && target.TemporaryHealth > 0 && !request.IgnoreTemporaryHealth)
            warnings.Add("temporary_health_disabled");

        var vehicle = string.Equals(target.ParticipantType, CombatParticipantTypes.Vehicle, StringComparison.OrdinalIgnoreCase);
        var previousHealth = Math.Max(0, target.CurrentHealth);
        var previousResource = vehicle ? Math.Max(0, target.CurrentStructure) : previousHealth;
        var previousTemporaryHealth = Math.Max(0, target.TemporaryHealth);
        var remaining = Math.Max(0, request.DamageAmount);
        var prevented = 0;
        var currentTemporaryHealth = previousTemporaryHealth;
        if (TemporaryHealthEnabled && !request.IgnoreTemporaryHealth && currentTemporaryHealth > 0 && remaining > 0)
        {
            prevented = Math.Min(currentTemporaryHealth, remaining);
            currentTemporaryHealth -= prevented;
            remaining -= prevented;
        }

        var healthDamage = Math.Min(previousResource, remaining);
        var currentResource = Math.Max(0, previousResource - remaining);
        var currentHealth = vehicle ? previousHealth : currentResource;
        var applied = prevented + healthDamage;
        return new CombatDamageResultResponse
        {
            EncounterId = request.EncounterId ?? string.Empty,
            SourceActionId = request.SourceActionId ?? string.Empty,
            AttackerParticipantId = request.AttackerParticipantId ?? string.Empty,
            TargetParticipantId = request.TargetParticipantId ?? string.Empty,
            DamageAmount = Math.Max(0, request.DamageAmount),
            DamageApplied = applied,
            DamagePrevented = prevented,
            DamageType = damageType,
            PreviousHealth = previousHealth,
            CurrentHealth = currentHealth,
            ResourceType = vehicle ? "structure" : "health",
            PreviousResource = previousResource,
            CurrentResource = currentResource,
            PreviousTemporaryHealth = previousTemporaryHealth,
            CurrentTemporaryHealth = currentTemporaryHealth,
            Warnings = warnings
        };
    }

    public void MarkDefeatedIfNeeded(CombatDamageApplyRequest request, CombatParticipantState target, CombatDamageResultResponse result)
    {
        if (result.CurrentResource > 0) return;
        if (AutoDefeatEnabled && request.AllowAutoDefeat)
        {
            target.IsDefeated = true;
            if (!target.DefeatedAtUtc.HasValue) target.DefeatedAtUtc = DateTime.UtcNow;
            target.DefeatedReason = string.IsNullOrWhiteSpace(request.Reason)
                ? (string.Equals(result.ResourceType, "structure", StringComparison.OrdinalIgnoreCase) ? "structure_zero" : "health_zero")
                : request.Reason.Trim();
            result.TargetDefeated = true;
            result.DefeatedReason = target.DefeatedReason;
        }
        else
        {
            result.Warnings.Add("auto_defeat_disabled");
            result.TargetDefeated = target.IsDefeated;
            result.DefeatedReason = target.DefeatedReason ?? string.Empty;
        }
    }

    public CombatActionState BuildDamageActionState(CombatDamageApplyRequest request, CombatEncounterState encounter, CombatDamageResultResponse result, UserAccount actor)
    {
        return new CombatActionState
        {
            Id = Guid.NewGuid().ToString("N"),
            EncounterId = encounter.Id,
            RoundNumber = Math.Max(0, encounter.RoundNumber),
            TurnIndex = Math.Max(0, encounter.ActiveTurnIndex),
            ActorParticipantId = request.AttackerParticipantId ?? string.Empty,
            ActionType = CombatActionTypes.Damage,
            ActionName = "Применение урона",
            TargetParticipantIds = new List<string> { request.TargetParticipantId ?? string.Empty }.Where(x => !string.IsNullOrWhiteSpace(x)).ToList(),
            Status = CombatActionStatuses.Resolved,
            RequestId = request.RequestId ?? string.Empty,
            ActorUserId = actor?.Id ?? string.Empty,
            CreatedAtUtc = DateTime.UtcNow,
            PayloadSummary = BuildDamagePayload(request, result)
        };
    }

    public string BuildDamageLogMessage(CombatParticipantState target, CombatDamageResultResponse result)
    {
        var targetName = string.IsNullOrWhiteSpace(target.DisplayName) ? target.Id : target.DisplayName;
        var resourceName = string.Equals(result.ResourceType, "structure", StringComparison.OrdinalIgnoreCase) ? "Структура" : "Здоровье";
        var damageType = (result.DamageType ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "physical" => "физического",
            "magical" => "магического",
            "energy" => "энергетического",
            _ => ""
        };
        var message = $"{targetName} получает {result.DamageApplied} {damageType} урона. {resourceName}: {result.PreviousResource} → {result.CurrentResource}.";
        if (result.TargetDefeated) message += " Цель выведена из боя.";
        return message;
    }

    private void ApplyCalculatedDamage(CombatDamageApplyRequest request, CombatParticipantState target, CombatDamageResultResponse result)
    {
        if (string.Equals(result.ResourceType, "structure", StringComparison.OrdinalIgnoreCase))
            target.CurrentStructure = result.CurrentResource;
        else
            target.CurrentHealth = result.CurrentHealth;
        target.TemporaryHealth = result.CurrentTemporaryHealth;
        target.LastDamageTaken = result.DamageApplied;
        target.LastDamageType = result.DamageType;
        target.UpdatedUtc = DateTime.UtcNow;
    }

    private async Task WriteDamageLogAsync(CombatEncounterState encounter, CombatDamageApplyRequest request, CombatDamageResultResponse result, string message, UserAccount actor)
    {
        var payload = BuildDamagePayload(request, result);
        await _logWriter.AppendLogAndReplayAsync(
            new CombatLogWriteRequest
            {
                EncounterId = encounter.Id,
                CampaignId = encounter.CampaignId,
                SessionId = encounter.SessionId,
                RoundNumber = encounter.RoundNumber,
                TurnIndex = encounter.ActiveTurnIndex,
                ActorParticipantId = request.AttackerParticipantId ?? string.Empty,
                ActorUserId = actor?.Id ?? string.Empty,
                EventType = CombatEventTypes.DamageApplied,
                Message = message ?? string.Empty,
                SourcePayload = payload,
                Visibility = CombatVisibilityIds.Public,
                RequestId = request.RequestId ?? string.Empty
            },
            new CombatReplayWriteRequest
            {
                EncounterId = encounter.Id,
                EventType = CombatEventTypes.DamageApplied,
                RoundNumber = encounter.RoundNumber,
                TurnIndex = encounter.ActiveTurnIndex,
                ActorParticipantId = request.AttackerParticipantId ?? string.Empty,
                SourcePayload = payload,
                Visibility = CombatVisibilityIds.Public,
                RequestId = request.RequestId ?? string.Empty
            });
    }

    private async Task<CombatFullSnapshotResponse> SnapshotAsync(string encounterId, UserAccount actor)
    {
        return await _snapshotService.BuildFullSnapshotAsync(new CombatFullSnapshotRequest
        {
            EncounterId = encounterId,
            IncludeParticipants = true,
            IncludeTurns = true,
            IncludeRounds = true,
            IncludeActions = true,
            IncludeLogs = true,
            IncludeReplayEvents = false,
            LimitActions = 100,
            LimitLogs = 100
        }, actor);
    }

    private async Task<CombatEncounterState> RequireEncounterAsync(string encounterId)
    {
        if (string.IsNullOrWhiteSpace(encounterId)) throw new ArgumentException("encounter_missing");
        var encounter = await _encounters.GetByIdAsync(encounterId);
        if (encounter == null) throw new KeyNotFoundException("encounter_missing");
        return encounter;
    }

    private async Task<CombatParticipantState> RequireParticipantAsync(string participantId, string encounterId, string errorCode)
    {
        if (string.IsNullOrWhiteSpace(participantId)) throw new ArgumentException(errorCode);
        var participant = await _participants.GetByIdAsync(participantId);
        if (participant == null || !string.Equals(participant.EncounterId, encounterId, StringComparison.OrdinalIgnoreCase))
            throw new KeyNotFoundException(errorCode);
        return participant;
    }

    private static void ValidateVitals(CombatParticipantVitalsSetRequest request)
    {
        if (request.MaxHealth < 0) throw new ArgumentOutOfRangeException(nameof(request.MaxHealth), "max_health_negative");
        if (request.CurrentHealth < 0) throw new ArgumentOutOfRangeException(nameof(request.CurrentHealth), "current_health_negative");
        if (request.MaxHealth > 0 && request.CurrentHealth > request.MaxHealth) throw new InvalidOperationException("current_health_exceeds_max");
        if (request.TemporaryHealth < 0) throw new ArgumentOutOfRangeException(nameof(request.TemporaryHealth), "temporary_health_negative");
        if (request.MaxMorale < 0) throw new ArgumentOutOfRangeException(nameof(request.MaxMorale), "max_morale_negative");
        if (request.CurrentMorale < 0) throw new ArgumentOutOfRangeException(nameof(request.CurrentMorale), "current_morale_negative");
        if (request.MaxMorale > 0 && request.CurrentMorale > request.MaxMorale) throw new InvalidOperationException("current_morale_exceeds_max");
    }

    private static string NormalizeDamageType(string damageType)
    {
        var value = (damageType ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(value)) return "physical";
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "physical", "magical", "fire", "cold", "shock", "poison", "psychic", "true", "custom"
        };
        return allowed.Contains(value) ? value : "custom";
    }

    private static Dictionary<string, object> BuildVitalsPayload(CombatParticipantState participant)
    {
        return new Dictionary<string, object>
        {
            { "participantId", participant.Id },
            { "maxHealth", participant.MaxHealth },
            { "currentHealth", participant.CurrentHealth },
            { "temporaryHealth", participant.TemporaryHealth },
            { "maxMorale", participant.MaxMorale },
            { "currentMorale", participant.CurrentMorale }
        };
    }

    private static Dictionary<string, object> BuildDamagePayload(CombatDamageApplyRequest request, CombatDamageResultResponse result)
    {
        return new Dictionary<string, object>
        {
            { "sourceActionId", request.SourceActionId ?? string.Empty },
            { "attackerParticipantId", request.AttackerParticipantId ?? string.Empty },
            { "targetParticipantId", request.TargetParticipantId ?? string.Empty },
            { "damageAmount", result.DamageAmount },
            { "damageApplied", result.DamageApplied },
            { "damagePrevented", result.DamagePrevented },
            { "damageType", result.DamageType },
            { "previousHealth", result.PreviousHealth },
            { "currentHealth", result.CurrentHealth },
            { "resourceType", result.ResourceType },
            { "previousResource", result.PreviousResource },
            { "currentResource", result.CurrentResource },
            { "previousTemporaryHealth", result.PreviousTemporaryHealth },
            { "currentTemporaryHealth", result.CurrentTemporaryHealth },
            { "targetDefeated", result.TargetDefeated },
            { "defeatedReason", result.DefeatedReason ?? string.Empty }
        };
    }

    private static string Text(IDictionary<string, object> payload, string key)
    {
        return payload.TryGetValue(key, out var value) ? Convert.ToString(value) ?? string.Empty : string.Empty;
    }

    private static int Number(IDictionary<string, object> payload, string key)
    {
        if (!payload.TryGetValue(key, out var value) || value == null) return 0;
        try { return Convert.ToInt32(value); }
        catch { return 0; }
    }

    private static bool Flag(IDictionary<string, object> payload, string key)
    {
        if (!payload.TryGetValue(key, out var value) || value == null) return false;
        try { return Convert.ToBoolean(value); }
        catch { return false; }
    }
}
