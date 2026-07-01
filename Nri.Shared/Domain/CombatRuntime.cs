using System;
using System.Collections.Generic;
using System.Linq;

namespace Nri.Shared.Domain;

public static class CombatFeatureFlags
{
    public const bool UseCombatSystemV1 = false;
    public const bool UseCombatEncounterRuntime = false;
    public const bool UseCombatInitiativeOrder = false;
    public const bool UseCombatTurnEngine = false;
    public const bool UseCombatActionLog = false;
    public const bool UseCombatReplayLog = false;
    public const bool UseCombatReadEndpoints = false;
    public const bool UseCombatWriteEndpoints = false;
    public const bool UseCombatLogReadEndpoints = false;
    public const bool UseCombatReplayReadEndpoints = false;
    public const bool UseCombatSafePayloadSummary = false;
    public const bool UseCombatSnapshotReadEndpoints = false;
    public const bool UseCombatDiagnosticsEndpoints = false;
    public const bool UseCombatActionEconomySkeleton = false;
    public const bool UseCombatActionDeclareEndpoints = false;
    public const bool UseCombatActionPointSpending = false;
    public const bool UseCombatActionValidation = false;
    public const bool UseCombatAttackRollMvp = false;
    public const bool UseCombatHitCalculationMvp = false;
    public const bool UseCombatCriticalRulesMvp = false;
    public const bool UseCombatAttackActionEndpoint = false;
    public const bool UseCombatFateRollHook = false;
    public const bool UseCombatDefenseMvp = false;
    public const bool UseCombatArmorDefenseMvp = false;
    public const bool UseCombatShieldDefenseMvp = false;
    public const bool UseCombatCoverModifierMvp = false;
    public const bool UseCombatDistanceModifierMvp = false;
    public const bool UseCombatDefensePreviewEndpoint = false;
    public const bool UseCombatAttackDefenseIntegration = false;
    public const bool UseCombatDamageMvp = false;
    public const bool UseCombatDamageApplicationEndpoint = false;
    public const bool UseCombatParticipantVitals = false;
    public const bool UseCombatTemporaryHealth = false;
    public const bool UseCombatAutoDefeatOnZeroHealth = false;
    public const bool UseCombatDamageActionLogging = false;
    public const bool UseCombatConditionsMvp = false;
    public const bool UseCombatConditionApplyEndpoint = false;
    public const bool UseCombatConditionRemoveEndpoint = false;
    public const bool UseCombatConditionReadEndpoint = false;
    public const bool UseCombatConditionDefinitionLookup = false;
    public const bool UseCombatConditionAutoEffects = false;
    public const bool UseCombatWeaponIntegrationMvp = false;
    public const bool UseCombatEquippedWeaponLookup = false;
    public const bool UseCombatAmmoCompatibilityMvp = false;
    public const bool UseCombatWeaponDamageDraft = false;
    public const bool UseCombatAttackDamageAutoApply = false;
    public const bool UseCombatAmmoReadOnlyCheck = false;
    public const bool UseCombatAmmoConsumptionMvp = false;
    public const bool UseCombatWeaponDurabilityMvp = false;
    public const bool UseCombatArmorDamageReduction = false;
    public const bool UseCombatArmorPenetration = false;
    public const bool UseCombatFateHookMvp = false;
    public const bool UseCombatFateAttackModifier = false;
    public const bool UseCombatFateDamageModifier = false;
    public const bool UseCombatFateLogging = false;
    public const bool UseCombatFateBreakdownInResponse = false;
    public const bool UseCombatMvpSmokeEndpoint = false;
    public const bool UseCombatPlayerReadEndpoints = false;
    public const bool UseCombatPlayerFeedEndpoint = false;
    public const bool UseCombatPlayerSnapshotEndpoint = false;
    public const bool UseCombatPlayerKnownConditions = false;
}

public static class CombatRuntimeStatuses
{
    public const string Draft = "draft";
    public const string Active = "active";
    public const string Paused = "paused";
    public const string Ended = "ended";
    public const string Cancelled = "cancelled";
}

public static class CombatTurnStatuses
{
    public const string Pending = "pending";
    public const string Active = "active";
    public const string Completed = "completed";
    public const string Skipped = "skipped";
    public const string Cancelled = "cancelled";
}

public static class CombatParticipantTypes
{
    public const string PlayerCharacter = "player_character";
    public const string Npc = "npc";
    public const string Creature = "creature";
    public const string Vehicle = "vehicle";
    public const string Squad = "squad";
    public const string Environmental = "environmental";
}

public static class CombatActionTypes
{
    public const string Move = "move";
    public const string Interact = "interact";
    public const string Prepare = "prepare";
    public const string Wait = "wait";
    public const string Skip = "skip";
    public const string Reaction = "reaction";
    public const string Attack = "attack";
    public const string Damage = "damage";
    public const string GmNote = "gm_note";
    public const string Custom = "custom";
}

public static class CombatActionStatuses
{
    public const string Declared = "declared";
    public const string Active = "active";
    public const string Completed = "completed";
    public const string Resolved = "resolved";
    public const string Failed = "failed";
    public const string Cancelled = "cancelled";
}

public static class CombatEventTypes
{
    public const string EncounterStarted = "encounter_started";
    public const string EncounterEnded = "encounter_ended";
    public const string EncounterCancelled = "encounter_cancelled";
    public const string InitiativeSorted = "initiative_sorted";
    public const string RoundStarted = "round_started";
    public const string RoundEnded = "round_ended";
    public const string TurnStarted = "turn_started";
    public const string TurnEnded = "turn_ended";
    public const string TurnSkipped = "turn_skipped";
    public const string TurnDelayed = "turn_delayed";
    public const string ParticipantAdded = "participant_added";
    public const string ParticipantRemoved = "participant_removed";
    public const string ActionDeclared = "action_declared";
    public const string ActionResolved = "action_resolved";
    public const string ActionCancelled = "action_cancelled";
    public const string ActionPointsSpent = "action_points_spent";
    public const string AttackResolved = "attack_resolved";
    public const string ParticipantVitalsSet = "participant_vitals_set";
    public const string DamageApplied = "damage_applied";
    public const string ConditionApplied = "condition_applied";
    public const string ConditionRemoved = "condition_removed";
    public const string WeaponAttackResolved = "weapon_attack_resolved";
    public const string GmNote = "gm_note";
}

public static class CombatConditionStatuses
{
    public const string Active = "active";
    public const string Removed = "removed";
    public const string Expired = "expired";
    public const string Suppressed = "suppressed";
}

public static class CombatHitResultIds
{
    public const string Miss = "miss";
    public const string Hit = "hit";
    public const string CriticalHit = "critical_hit";
    public const string Fumble = "fumble";
    public const string BlockedByInvalidState = "blocked_by_invalid_state";
}

public static class CombatVisibilityIds
{
    public const string Public = "public";
    public const string GmOnly = "gm_only";
    public const string ParticipantOnly = "participant_only";
    public const string HiddenUntilRevealed = "hidden_until_revealed";
}

public sealed class CombatEncounterState : EntityBase
{
    public string CampaignId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = CombatRuntimeStatuses.Draft;
    public string RuleSetId { get; set; } = string.Empty;
    public int RoundNumber { get; set; }
    public int ActiveTurnIndex { get; set; }
    public string ActiveParticipantId { get; set; } = string.Empty;
    public List<string> ParticipantIds { get; set; } = new List<string>();
    public List<CombatInitiativeEntry> InitiativeOrder { get; set; } = new List<CombatInitiativeEntry>();
    public List<string> TeamIds { get; set; } = new List<string>();
    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAtUtc { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTime LastUpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public List<string> Tags { get; set; } = new List<string>();
    public string Notes { get; set; } = string.Empty;
}

public sealed class CombatParticipantState : EntityBase
{
    public string EncounterId { get; set; } = string.Empty;
    public string CharacterId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ParticipantType { get; set; } = string.Empty;
    public string TeamId { get; set; } = string.Empty;
    public string ControllerUserId { get; set; } = string.Empty;
    public bool IsNpc { get; set; }
    public bool IsPlayerControlled { get; set; }
    public int Initiative { get; set; }
    public int InitiativeTieBreaker { get; set; }
    public string InitiativeGroup { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsDefeated { get; set; }
    public bool IsHidden { get; set; }
    public bool HasActedThisRound { get; set; }
    public int ActionPoints { get; set; }
    public int MinorActionPoints { get; set; }
    public int ReactionCount { get; set; }
    public int ReactionLimit { get; set; }
    public int MaxHealth { get; set; }
    public int CurrentHealth { get; set; }
    public int TemporaryHealth { get; set; }
    public int MaxMorale { get; set; }
    public int CurrentMorale { get; set; }
    public int LastDamageTaken { get; set; }
    public string LastDamageType { get; set; } = string.Empty;
    public DateTime? DefeatedAtUtc { get; set; }
    public string DefeatedReason { get; set; } = string.Empty;
    public List<CombatConditionState> Conditions { get; set; } = new List<CombatConditionState>();
    public string PositionSummary { get; set; } = string.Empty;
    public decimal DistanceMeters { get; set; }
    public string CoverState { get; set; } = string.Empty;
    public string VisibilityState { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new List<string>();
    public string Notes { get; set; } = string.Empty;
}

public sealed class CombatConditionState
{
    public string ConditionInstanceId { get; set; } = Guid.NewGuid().ToString("N");
    public string ConditionDefinitionId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string SourceActionId { get; set; } = string.Empty;
    public string SourceParticipantId { get; set; } = string.Empty;
    public string TargetParticipantId { get; set; } = string.Empty;
    public string ConditionGroup { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string StackMode { get; set; } = "unique";
    public int StackCount { get; set; } = 1;
    public int MaxStacks { get; set; } = 1;
    public string DurationMode { get; set; } = "until_removed";
    public int RemainingRounds { get; set; }
    public int AppliedRoundNumber { get; set; }
    public int AppliedTurnIndex { get; set; }
    public bool IsHiddenFromPlayer { get; set; }
    public bool IsPositive { get; set; }
    public bool IsNegative { get; set; }
    public string Status { get; set; } = CombatConditionStatuses.Active;
    public string Notes { get; set; } = string.Empty;
    public DateTime AppliedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAtUtc { get; set; }
}

public sealed class CombatInitiativeEntry
{
    public string ParticipantId { get; set; } = string.Empty;
    public int Initiative { get; set; }
    public int TieBreaker { get; set; }
    public int OrderIndex { get; set; }
    public bool IsDelayed { get; set; }
    public bool IsSkipped { get; set; }
    public string Notes { get; set; } = string.Empty;
}

public sealed class CombatTurnState : EntityBase
{
    public string EncounterId { get; set; } = string.Empty;
    public int RoundNumber { get; set; }
    public int TurnIndex { get; set; }
    public string ParticipantId { get; set; } = string.Empty;
    public string Status { get; set; } = CombatTurnStatuses.Pending;
    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAtUtc { get; set; }
    public bool Skipped { get; set; }
    public string SkipReason { get; set; } = string.Empty;
    public int ActionPointsStarted { get; set; }
    public int ActionPointsSpent { get; set; }
    public int MinorActionPointsStarted { get; set; }
    public int MinorActionPointsSpent { get; set; }
    public int ReactionsUsed { get; set; }
    public string Notes { get; set; } = string.Empty;
}

public sealed class CombatRoundRuntimeState : EntityBase
{
    public string EncounterId { get; set; } = string.Empty;
    public int RoundNumber { get; set; }
    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAtUtc { get; set; }
    public List<string> TurnIds { get; set; } = new List<string>();
    public List<string> CompletedParticipantIds { get; set; } = new List<string>();
    public string Notes { get; set; } = string.Empty;
}

public sealed class CombatActionState
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string EncounterId { get; set; } = string.Empty;
    public int RoundNumber { get; set; }
    public int TurnIndex { get; set; }
    public string ActorParticipantId { get; set; } = string.Empty;
    public string ActionType { get; set; } = string.Empty;
    public string ActionName { get; set; } = string.Empty;
    public List<string> TargetParticipantIds { get; set; } = new List<string>();
    public string TargetLocationSummary { get; set; } = string.Empty;
    public int ActionPointCost { get; set; }
    public int MinorActionPointCost { get; set; }
    public int ReactionCost { get; set; }
    public string Status { get; set; } = CombatTurnStatuses.Pending;
    public string RequestId { get; set; } = string.Empty;
    public string ActorUserId { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object> PayloadSummary { get; set; } = new Dictionary<string, object>();
    public string Notes { get; set; } = string.Empty;
}

public sealed class CombatRuntimeLogEntry : EntityBase
{
    public string EncounterId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public int RoundNumber { get; set; }
    public int TurnIndex { get; set; }
    public string ActorParticipantId { get; set; } = string.Empty;
    public string ActorUserId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Dictionary<string, object> PayloadSummary { get; set; } = new Dictionary<string, object>();
    public string Visibility { get; set; } = CombatVisibilityIds.Public;
    public string RequestId { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class CombatReplayEvent
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string EncounterId { get; set; } = string.Empty;
    public long SequenceNumber { get; set; }
    public string EventType { get; set; } = string.Empty;
    public int RoundNumber { get; set; }
    public int TurnIndex { get; set; }
    public string ActorParticipantId { get; set; } = string.Empty;
    public Dictionary<string, object> Data { get; set; } = new Dictionary<string, object>();
    public string Visibility { get; set; } = CombatVisibilityIds.Public;
    public string RequestId { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class CombatValidationIssue
{
    public string Code { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
}

public sealed class CombatRuntimeValidationResult
{
    public bool IsValid { get; set; } = true;
    public List<CombatValidationIssue> Errors { get; set; } = new List<CombatValidationIssue>();
    public List<CombatValidationIssue> Warnings { get; set; } = new List<CombatValidationIssue>();
    public DateTime CheckedAtUtc { get; set; } = DateTime.UtcNow;
}

public static class CombatRuntimeValidator
{
    public static CombatRuntimeValidationResult ValidateEncounter(CombatEncounterState encounter)
    {
        var result = new CombatRuntimeValidationResult();
        if (encounter == null)
        {
            AddError(result, "encounter_null", "Combat encounter is null.", string.Empty, "encounter");
            return result;
        }

        if (string.IsNullOrWhiteSpace(encounter.Id)) AddError(result, "encounter_id_missing", "Encounter Id is required.", encounter.Id, "encounter");
        if (string.IsNullOrWhiteSpace(encounter.CampaignId)) AddWarning(result, "encounter_campaign_id_missing", "CampaignId should be set for combat runtime state.", encounter.Id, "encounter");
        if (encounter.RoundNumber < 0) AddError(result, "encounter_round_number_invalid", "RoundNumber must be greater than or equal to zero.", encounter.Id, "encounter");
        if (encounter.ActiveTurnIndex < 0) AddError(result, "encounter_active_turn_index_invalid", "ActiveTurnIndex must be greater than or equal to zero.", encounter.Id, "encounter");
        if (encounter.ParticipantIds == null) AddError(result, "encounter_participant_ids_null", "ParticipantIds must not be null.", encounter.Id, "encounter");
        if (encounter.InitiativeOrder == null) AddError(result, "encounter_initiative_order_null", "InitiativeOrder must not be null.", encounter.Id, "encounter");
        ValidateInitiativeDuplicates(result, encounter.InitiativeOrder ?? new List<CombatInitiativeEntry>(), encounter.Id);
        Finalize(result);
        return result;
    }

    public static CombatRuntimeValidationResult ValidateParticipant(CombatParticipantState participant)
    {
        var result = new CombatRuntimeValidationResult();
        if (participant == null)
        {
            AddError(result, "participant_null", "Combat participant is null.", string.Empty, "participant");
            return result;
        }

        if (string.IsNullOrWhiteSpace(participant.Id)) AddError(result, "participant_id_missing", "Participant Id is required.", participant.Id, "participant");
        if (string.IsNullOrWhiteSpace(participant.EncounterId)) AddError(result, "participant_encounter_id_missing", "EncounterId is required.", participant.Id, "participant");
        if (string.IsNullOrWhiteSpace(participant.DisplayName)) AddWarning(result, "participant_display_name_missing", "DisplayName should be set.", participant.Id, "participant");
        if (participant.ActionPoints < 0) AddError(result, "participant_action_points_invalid", "ActionPoints must be greater than or equal to zero.", participant.Id, "participant");
        if (participant.MinorActionPoints < 0) AddError(result, "participant_minor_action_points_invalid", "MinorActionPoints must be greater than or equal to zero.", participant.Id, "participant");
        if (participant.ReactionCount < 0) AddError(result, "participant_reaction_count_invalid", "ReactionCount must be greater than or equal to zero.", participant.Id, "participant");
        if (participant.ReactionLimit < 0) AddError(result, "participant_reaction_limit_invalid", "ReactionLimit must be greater than or equal to zero.", participant.Id, "participant");
        if (participant.Tags == null) AddError(result, "participant_tags_null", "Tags must not be null.", participant.Id, "participant");
        Finalize(result);
        return result;
    }

    public static CombatRuntimeValidationResult ValidateInitiativeOrder(CombatEncounterState encounter, IEnumerable<CombatParticipantState> participants)
    {
        var result = ValidateEncounter(encounter);
        if (encounter == null) return result;

        var knownParticipantIds = new HashSet<string>((participants ?? Enumerable.Empty<CombatParticipantState>()).Where(x => x != null).Select(x => x.Id), StringComparer.OrdinalIgnoreCase);
        foreach (var entry in encounter.InitiativeOrder ?? new List<CombatInitiativeEntry>())
        {
            if (string.IsNullOrWhiteSpace(entry.ParticipantId))
            {
                AddError(result, "initiative_participant_id_missing", "Initiative entry ParticipantId is required.", encounter.Id, "initiative");
                continue;
            }

            if (knownParticipantIds.Count > 0 && !knownParticipantIds.Contains(entry.ParticipantId))
                AddError(result, "initiative_participant_missing", "Initiative entry references missing participant.", entry.ParticipantId, "initiative");
        }

        if (string.Equals(encounter.Status, CombatRuntimeStatuses.Active, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(encounter.ActiveParticipantId)
            && knownParticipantIds.Count > 0
            && !knownParticipantIds.Contains(encounter.ActiveParticipantId))
        {
            AddError(result, "encounter_active_participant_missing", "ActiveParticipantId must reference an existing participant when encounter is active.", encounter.ActiveParticipantId, "encounter");
        }

        Finalize(result);
        return result;
    }

    public static CombatRuntimeValidationResult ValidateTurn(CombatTurnState turn)
    {
        var result = new CombatRuntimeValidationResult();
        if (turn == null)
        {
            AddError(result, "turn_null", "Combat turn is null.", string.Empty, "turn");
            return result;
        }

        if (string.IsNullOrWhiteSpace(turn.EncounterId)) AddError(result, "turn_encounter_id_missing", "EncounterId is required.", turn.Id, "turn");
        if (turn.RoundNumber < 0) AddError(result, "turn_round_number_invalid", "RoundNumber must be greater than or equal to zero.", turn.Id, "turn");
        if (turn.TurnIndex < 0) AddError(result, "turn_index_invalid", "TurnIndex must be greater than or equal to zero.", turn.Id, "turn");
        if (string.IsNullOrWhiteSpace(turn.ParticipantId)) AddError(result, "turn_participant_id_missing", "ParticipantId is required.", turn.Id, "turn");
        if (turn.ActionPointsStarted < 0 || turn.ActionPointsSpent < 0) AddError(result, "turn_action_points_invalid", "Action point values must be greater than or equal to zero.", turn.Id, "turn");
        if (turn.MinorActionPointsStarted < 0 || turn.MinorActionPointsSpent < 0) AddError(result, "turn_minor_action_points_invalid", "Minor action point values must be greater than or equal to zero.", turn.Id, "turn");
        if (turn.ReactionsUsed < 0) AddError(result, "turn_reactions_used_invalid", "ReactionsUsed must be greater than or equal to zero.", turn.Id, "turn");
        Finalize(result);
        return result;
    }

    public static CombatRuntimeValidationResult ValidateAction(CombatActionState action)
    {
        var result = new CombatRuntimeValidationResult();
        if (action == null)
        {
            AddError(result, "action_null", "Combat action is null.", string.Empty, "action");
            return result;
        }

        if (string.IsNullOrWhiteSpace(action.Id)) AddError(result, "action_id_missing", "Action Id is required.", action.Id, "action");
        if (string.IsNullOrWhiteSpace(action.EncounterId)) AddError(result, "action_encounter_id_missing", "EncounterId is required.", action.Id, "action");
        if (action.RoundNumber < 0) AddError(result, "action_round_number_invalid", "RoundNumber must be greater than or equal to zero.", action.Id, "action");
        if (action.TurnIndex < 0) AddError(result, "action_turn_index_invalid", "TurnIndex must be greater than or equal to zero.", action.Id, "action");
        if (string.IsNullOrWhiteSpace(action.ActorParticipantId)) AddWarning(result, "action_actor_participant_id_missing", "ActorParticipantId should be set.", action.Id, "action");
        if (action.ActionPointCost < 0 || action.MinorActionPointCost < 0 || action.ReactionCost < 0) AddError(result, "action_cost_invalid", "Action costs must be greater than or equal to zero.", action.Id, "action");
        if (action.TargetParticipantIds == null) AddError(result, "action_target_participant_ids_null", "TargetParticipantIds must not be null.", action.Id, "action");
        if (action.PayloadSummary == null) AddError(result, "action_payload_summary_null", "PayloadSummary must not be null.", action.Id, "action");
        Finalize(result);
        return result;
    }

    public static CombatRuntimeValidationResult ValidateLogEntry(CombatRuntimeLogEntry entry)
    {
        var result = new CombatRuntimeValidationResult();
        if (entry == null)
        {
            AddError(result, "log_entry_null", "Combat log entry is null.", string.Empty, "log");
            return result;
        }

        if (string.IsNullOrWhiteSpace(entry.Id)) AddError(result, "log_entry_id_missing", "Log entry Id is required.", entry.Id, "log");
        if (string.IsNullOrWhiteSpace(entry.EncounterId)) AddError(result, "log_entry_encounter_id_missing", "EncounterId is required.", entry.Id, "log");
        if (entry.RoundNumber < 0) AddError(result, "log_entry_round_number_invalid", "RoundNumber must be greater than or equal to zero.", entry.Id, "log");
        if (entry.TurnIndex < 0) AddError(result, "log_entry_turn_index_invalid", "TurnIndex must be greater than or equal to zero.", entry.Id, "log");
        if (string.IsNullOrWhiteSpace(entry.EventType)) AddError(result, "log_entry_event_type_missing", "EventType is required.", entry.Id, "log");
        if (entry.PayloadSummary == null) AddError(result, "log_entry_payload_summary_null", "PayloadSummary must not be null.", entry.Id, "log");
        Finalize(result);
        return result;
    }

    private static void ValidateInitiativeDuplicates(CombatRuntimeValidationResult result, IEnumerable<CombatInitiativeEntry> entries, string encounterId)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries ?? Enumerable.Empty<CombatInitiativeEntry>())
        {
            var participantId = entry?.ParticipantId ?? string.Empty;
            if (string.IsNullOrWhiteSpace(participantId)) continue;
            if (!seen.Add(participantId))
                AddError(result, "initiative_duplicate_participant", "InitiativeOrder must not contain duplicate ParticipantId values.", encounterId, "initiative");
        }
    }

    private static void AddError(CombatRuntimeValidationResult result, string code, string message, string entityId, string entityType)
    {
        result.Errors.Add(Issue(code, "error", message, entityId, entityType));
    }

    private static void AddWarning(CombatRuntimeValidationResult result, string code, string message, string entityId, string entityType)
    {
        result.Warnings.Add(Issue(code, "warning", message, entityId, entityType));
    }

    private static CombatValidationIssue Issue(string code, string severity, string message, string entityId, string entityType)
    {
        return new CombatValidationIssue
        {
            Code = code ?? string.Empty,
            Severity = severity ?? string.Empty,
            Message = message ?? string.Empty,
            EntityId = entityId ?? string.Empty,
            EntityType = entityType ?? string.Empty
        };
    }

    private static void Finalize(CombatRuntimeValidationResult result)
    {
        result.IsValid = result.Errors.Count == 0;
    }
}
