using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nri.Server.Infrastructure.Mongo.Repositories;
using Nri.Server.Logging;
using Nri.Shared.Domain;

namespace Nri.Server.Application.Services;

public interface ICombatSnapshotService
{
    Task<CombatFullSnapshotResponse> BuildFullSnapshotAsync(CombatFullSnapshotRequest request, UserAccount actor);
    Task<CombatEncounterSummary> BuildEncounterSummaryAsync(string encounterId, UserAccount actor);
    Task<IReadOnlyCollection<CombatInitiativeEntrySummary>> BuildInitiativeSummaryAsync(string encounterId, UserAccount actor);
    Task<CombatTurnSummary> BuildCurrentTurnSummaryAsync(string encounterId, UserAccount actor);
}

public interface ICombatDiagnosticsService
{
    Task<CombatDiagnosticsResponse> RunDiagnosticsAsync(CombatDiagnosticsRequest request, UserAccount actor);
    CombatDiagnosticsSection ValidateEncounter(CombatEncounterState encounter, IReadOnlyCollection<CombatParticipantState> participants);
    CombatDiagnosticsSection ValidateParticipants(IReadOnlyCollection<CombatParticipantState> participants);
    CombatDiagnosticsSection ValidateInitiative(CombatEncounterState encounter, IReadOnlyCollection<CombatParticipantState> participants);
    CombatDiagnosticsSection ValidateTurnState(CombatEncounterState encounter, IReadOnlyCollection<CombatTurnState> turns, IReadOnlyCollection<CombatParticipantState> participants);
    CombatDiagnosticsSection ValidateActions(IReadOnlyCollection<CombatActionState> actions, IReadOnlyCollection<CombatParticipantState> participants);
}

public interface ICombatActionEconomyService
{
    Task<CombatActionEconomyResponse> DeclareActionAsync(CombatActionDeclareRequest request, UserAccount actor);
    Task<CombatActionEconomyResponse> CompleteActionAsync(CombatActionCompleteRequest request, UserAccount actor);
    Task<CombatActionEconomyResponse> CancelActionAsync(CombatActionCancelRequest request, UserAccount actor);
    Task<CombatActionEconomyResponse> SpendActionPointsAsync(CombatActionSpendRequest request, UserAccount actor);
    Task<CombatActionEconomyResponse> TriggerPreparedActionAsync(CombatPreparedActionTriggerRequest request, UserAccount actor);
    Task<CombatRuntimeValidationResult> ValidateActionCostAsync(CombatParticipantState participant, int actionPointCost, int minorActionPointCost, int reactionCost, bool strictMode);
}

public sealed class CombatSnapshotService : ICombatSnapshotService
{
    private readonly ICombatEncounterRepository _encounters;
    private readonly ICombatParticipantRepository _participants;
    private readonly ICombatTurnRepository _turns;
    private readonly ICombatRoundRepository _rounds;
    private readonly ICombatActionRepository _actions;
    private readonly ICombatLogRepository _logs;
    private readonly ICombatReplayEventRepository _replayEvents;
    private readonly ICombatDiagnosticsService _diagnostics;
    private readonly IServerLogger _logger;

    public CombatSnapshotService(
        ICombatEncounterRepository encounters,
        ICombatParticipantRepository participants,
        ICombatTurnRepository turns,
        ICombatRoundRepository rounds,
        ICombatActionRepository actions,
        ICombatLogRepository logs,
        ICombatReplayEventRepository replayEvents,
        ICombatDiagnosticsService diagnostics,
        IServerLogger logger)
    {
        _encounters = encounters;
        _participants = participants;
        _turns = turns;
        _rounds = rounds;
        _actions = actions;
        _logs = logs;
        _replayEvents = replayEvents;
        _diagnostics = diagnostics;
        _logger = logger;
    }

    public async Task<CombatFullSnapshotResponse> BuildFullSnapshotAsync(CombatFullSnapshotRequest request, UserAccount actor)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (string.IsNullOrWhiteSpace(request.EncounterId)) throw new ArgumentException("encounterId is required");

        var encounter = await RequireEncounterAsync(request.EncounterId);
        var response = new CombatFullSnapshotResponse
        {
            Encounter = CombatEncounterManagementService.ToEncounterSummary(encounter),
            BuiltAtUtc = DateTime.UtcNow
        };

        var participants = request.IncludeParticipants
            ? (await _participants.ListByEncounterAsync(encounter.Id, 500)).ToList()
            : new List<CombatParticipantState>();
        response.Participants.AddRange(participants.Select(CombatEncounterManagementService.ToParticipantSummary));
        response.InitiativeOrder.AddRange(ToInitiativeSummary(encounter, participants));

        IReadOnlyCollection<CombatTurnState> turns = new List<CombatTurnState>();
        if (request.IncludeTurns)
        {
            turns = await _turns.ListByEncounterAsync(encounter.Id, 500);
            response.CurrentTurn = ToCurrentTurnSummary(encounter, turns);
            if (string.IsNullOrWhiteSpace(response.CurrentTurn.ParticipantId))
                response.Warnings.Add("current_turn_missing");
        }

        if (request.IncludeRounds)
        {
            var round = await _rounds.GetByEncounterRoundAsync(encounter.Id, encounter.RoundNumber);
            if (round == null)
            {
                response.Warnings.Add("current_round_missing");
            }
            else
            {
                response.CurrentRound = ToRoundSummary(round, turns);
            }
        }

        if (request.IncludeActions)
        {
            var limit = Clamp(request.LimitActions, 100, 500);
            var actions = await _actions.ListByEncounterAsync(encounter.Id, limit);
            response.RecentActions.AddRange(actions.OrderByDescending(x => x.CreatedAtUtc).Take(limit).Select(ToActionSummary));
        }

        if (request.IncludeLogs)
        {
            var limit = Clamp(request.LimitLogs, 100, 500);
            var logs = await _logs.ListByEncounterAsync(encounter.Id, limit);
            response.RecentLogs.AddRange(logs.OrderByDescending(x => x.CreatedAtUtc).Take(limit).Select(CombatEncounterManagementService.ToLogSummary));
        }

        if (request.IncludeReplayEvents)
        {
            var replay = await _replayEvents.ListByEncounterAsync(encounter.Id, 1000);
            response.RecentReplayEvents.AddRange(replay.OrderByDescending(x => x.SequenceNumber).Take(1000).Select(CombatEncounterManagementService.ToReplayEventSummary));
        }

        if (request.IncludeDiagnostics)
        {
            var diagnostics = await _diagnostics.RunDiagnosticsAsync(new CombatDiagnosticsRequest
            {
                EncounterId = encounter.Id,
                RequestId = request.RequestId
            }, actor);
            response.Diagnostics = diagnostics.Summary;
        }

        _logger.Debug($"combat.snapshot.full.done encounterId={encounter.Id} participants={response.Participants.Count} actions={response.RecentActions.Count} logs={response.RecentLogs.Count}");
        return response;
    }

    public async Task<CombatEncounterSummary> BuildEncounterSummaryAsync(string encounterId, UserAccount actor)
    {
        return CombatEncounterManagementService.ToEncounterSummary(await RequireEncounterAsync(encounterId));
    }

    public async Task<IReadOnlyCollection<CombatInitiativeEntrySummary>> BuildInitiativeSummaryAsync(string encounterId, UserAccount actor)
    {
        var encounter = await RequireEncounterAsync(encounterId);
        var participants = await _participants.ListByEncounterAsync(encounter.Id, 500);
        return ToInitiativeSummary(encounter, participants.ToList());
    }

    public async Task<CombatTurnSummary> BuildCurrentTurnSummaryAsync(string encounterId, UserAccount actor)
    {
        var encounter = await RequireEncounterAsync(encounterId);
        var turns = await _turns.ListByEncounterAsync(encounter.Id, 500);
        return ToCurrentTurnSummary(encounter, turns);
    }

    private async Task<CombatEncounterState> RequireEncounterAsync(string encounterId)
    {
        var encounter = await _encounters.GetByIdAsync(encounterId);
        if (encounter == null) throw new KeyNotFoundException("combat encounter not found");
        return encounter;
    }

    private static List<CombatInitiativeEntrySummary> ToInitiativeSummary(CombatEncounterState encounter, IReadOnlyCollection<CombatParticipantState> participants)
    {
        var byId = participants.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
        return (encounter.InitiativeOrder ?? new List<CombatInitiativeEntry>())
            .OrderBy(x => x.OrderIndex)
            .Select(entry =>
            {
                byId.TryGetValue(entry.ParticipantId ?? string.Empty, out var participant);
                return new CombatInitiativeEntrySummary
                {
                    ParticipantId = entry.ParticipantId ?? string.Empty,
                    DisplayName = participant?.DisplayName ?? string.Empty,
                    Initiative = entry.Initiative,
                    TieBreaker = entry.TieBreaker,
                    OrderIndex = entry.OrderIndex,
                    IsDelayed = entry.IsDelayed,
                    IsSkipped = entry.IsSkipped,
                    IsActive = participant?.IsActive ?? false,
                    IsDefeated = participant?.IsDefeated ?? false
                };
            })
            .ToList();
    }

    private static CombatRoundSummary ToRoundSummary(CombatRoundRuntimeState round, IReadOnlyCollection<CombatTurnState> turns)
    {
        return new CombatRoundSummary
        {
            EncounterId = round.EncounterId,
            RoundNumber = round.RoundNumber,
            StartedAtUtc = round.StartedAtUtc,
            EndedAtUtc = round.EndedAtUtc,
            TurnCount = turns.Count(x => x.RoundNumber == round.RoundNumber),
            CompletedParticipantIds = (round.CompletedParticipantIds ?? new List<string>()).ToList()
        };
    }

    private static CombatTurnSummary ToCurrentTurnSummary(CombatEncounterState encounter, IReadOnlyCollection<CombatTurnState> turns)
    {
        var turn = turns
            .Where(x => x.RoundNumber == encounter.RoundNumber && x.TurnIndex == encounter.ActiveTurnIndex)
            .OrderByDescending(x => x.StartedAtUtc)
            .FirstOrDefault()
            ?? turns.OrderByDescending(x => x.RoundNumber).ThenByDescending(x => x.TurnIndex).FirstOrDefault();
        return turn == null ? new CombatTurnSummary() : ToTurnSummary(turn);
    }

    public static CombatTurnSummary ToTurnSummary(CombatTurnState turn)
    {
        return new CombatTurnSummary
        {
            EncounterId = turn.EncounterId,
            RoundNumber = turn.RoundNumber,
            TurnIndex = turn.TurnIndex,
            ParticipantId = turn.ParticipantId,
            Status = turn.Status,
            StartedAtUtc = turn.StartedAtUtc,
            EndedAtUtc = turn.EndedAtUtc,
            Skipped = turn.Skipped,
            SkipReason = turn.SkipReason,
            ActionPointsStarted = turn.ActionPointsStarted,
            ActionPointsSpent = turn.ActionPointsSpent,
            MinorActionPointsStarted = turn.MinorActionPointsStarted,
            MinorActionPointsSpent = turn.MinorActionPointsSpent,
            ReactionsUsed = turn.ReactionsUsed
        };
    }

    public static CombatActionSummary ToActionSummary(CombatActionState action)
    {
        return new CombatActionSummary
        {
            Id = action.Id,
            EncounterId = action.EncounterId,
            RoundNumber = action.RoundNumber,
            TurnIndex = action.TurnIndex,
            ActorParticipantId = action.ActorParticipantId,
            ActionType = action.ActionType,
            ActionName = action.ActionName,
            TargetParticipantIds = (action.TargetParticipantIds ?? new List<string>()).ToList(),
            TargetLocationSummary = action.TargetLocationSummary,
            ActionPointCost = action.ActionPointCost,
            MinorActionPointCost = action.MinorActionPointCost,
            ReactionCost = action.ReactionCost,
            Status = action.Status,
            CreatedAtUtc = action.CreatedAtUtc,
            RequestId = action.RequestId
        };
    }

    private static int Clamp(int value, int fallback, int max)
    {
        return Math.Max(1, Math.Min(value <= 0 ? fallback : value, max));
    }
}

public sealed class CombatDiagnosticsService : ICombatDiagnosticsService
{
    private readonly ICombatEncounterRepository _encounters;
    private readonly ICombatParticipantRepository _participants;
    private readonly ICombatTurnRepository _turns;
    private readonly ICombatActionRepository _actions;
    private readonly ICombatLogRepository _logs;
    private readonly IServerLogger _logger;

    public CombatDiagnosticsService(
        ICombatEncounterRepository encounters,
        ICombatParticipantRepository participants,
        ICombatTurnRepository turns,
        ICombatActionRepository actions,
        ICombatLogRepository logs,
        IServerLogger logger)
    {
        _encounters = encounters;
        _participants = participants;
        _turns = turns;
        _actions = actions;
        _logs = logs;
        _logger = logger;
    }

    public async Task<CombatDiagnosticsResponse> RunDiagnosticsAsync(CombatDiagnosticsRequest request, UserAccount actor)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (string.IsNullOrWhiteSpace(request.EncounterId)) throw new ArgumentException("encounterId is required");

        var encounter = await _encounters.GetByIdAsync(request.EncounterId);
        if (encounter == null) throw new KeyNotFoundException("combat encounter not found");

        var participants = (await _participants.ListByEncounterAsync(encounter.Id, 500)).ToList();
        var turns = (await _turns.ListByEncounterAsync(encounter.Id, 500)).ToList();
        var actions = (await _actions.ListByEncounterAsync(encounter.Id, 500)).ToList();
        var logs = (await _logs.ListByEncounterAsync(encounter.Id, 500)).ToList();
        var response = new CombatDiagnosticsResponse { EncounterId = encounter.Id, CheckedAtUtc = DateTime.UtcNow };

        if (request.IncludeEncounterValidation) response.Sections.Add(ValidateEncounter(encounter, participants));
        if (request.IncludeParticipantValidation) response.Sections.Add(ValidateParticipants(participants));
        if (request.IncludeInitiativeValidation) response.Sections.Add(ValidateInitiative(encounter, participants));
        if (request.IncludeTurnValidation) response.Sections.Add(ValidateTurnState(encounter, turns, participants));
        if (request.IncludeActionValidation) response.Sections.Add(ValidateActions(actions, participants));

        response.Errors.AddRange(response.Sections.SelectMany(x => x.Errors));
        response.Warnings.AddRange(response.Sections.SelectMany(x => x.Warnings));
        response.IsValid = response.Errors.Count == 0;
        response.Summary = new CombatDiagnosticsSummary
        {
            ParticipantCount = participants.Count,
            ActiveParticipantCount = participants.Count(x => x.IsActive),
            DefeatedParticipantCount = participants.Count(x => x.IsDefeated),
            InitiativeEntryCount = encounter.InitiativeOrder?.Count ?? 0,
            RoundNumber = encounter.RoundNumber,
            ActiveTurnIndex = encounter.ActiveTurnIndex,
            ActionCount = actions.Count,
            LogCount = logs.Count,
            ErrorCount = response.Errors.Count,
            WarningCount = response.Warnings.Count
        };

        _logger.Debug($"combat.diagnostics.done encounterId={encounter.Id} errors={response.Errors.Count} warnings={response.Warnings.Count}");
        return response;
    }

    public CombatDiagnosticsSection ValidateEncounter(CombatEncounterState encounter, IReadOnlyCollection<CombatParticipantState> participants)
    {
        var section = Section("encounter");
        Add(section, CombatRuntimeValidator.ValidateEncounter(encounter));
        if (!IsKnownStatus(encounter.Status)) section.Errors.Add(Issue("encounter_status_invalid", "error", "Encounter status is not recognized.", encounter.Id, "encounter"));
        if (encounter.ParticipantIds != null && encounter.ParticipantIds.Count != encounter.ParticipantIds.Distinct(StringComparer.OrdinalIgnoreCase).Count())
            section.Errors.Add(Issue("encounter_duplicate_participant_ids", "error", "ParticipantIds must not contain duplicates.", encounter.Id, "encounter"));
        if (string.Equals(encounter.Status, CombatRuntimeStatuses.Active, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(encounter.ActiveParticipantId)
            && !participants.Any(x => string.Equals(x.Id, encounter.ActiveParticipantId, StringComparison.OrdinalIgnoreCase) && x.IsActive))
        {
            section.Errors.Add(Issue("active_participant_missing", "error", "ActiveParticipantId does not reference an active participant.", encounter.ActiveParticipantId, "participant"));
        }

        Finalize(section);
        return section;
    }

    public CombatDiagnosticsSection ValidateParticipants(IReadOnlyCollection<CombatParticipantState> participants)
    {
        var section = Section("participants");
        foreach (var participant in participants ?? new List<CombatParticipantState>())
        {
            Add(section, CombatRuntimeValidator.ValidateParticipant(participant));
            if (participant.MaxHealth < 0) section.Errors.Add(Issue("max_health_negative", "error", "MaxHealth must be greater than or equal to zero.", participant.Id, "participant"));
            if (participant.CurrentHealth < 0) section.Errors.Add(Issue("current_health_negative", "error", "CurrentHealth must be greater than or equal to zero.", participant.Id, "participant"));
            if (participant.MaxHealth > 0 && participant.CurrentHealth > participant.MaxHealth) section.Errors.Add(Issue("current_health_exceeds_max", "error", "CurrentHealth must not exceed MaxHealth.", participant.Id, "participant"));
            if (participant.TemporaryHealth < 0) section.Errors.Add(Issue("temporary_health_negative", "error", "TemporaryHealth must be greater than or equal to zero.", participant.Id, "participant"));
            if (participant.IsDefeated && participant.CurrentHealth > 0) section.Warnings.Add(Issue("defeated_with_positive_health", "warning", "Participant is defeated while CurrentHealth is positive.", participant.Id, "participant"));
            if (!participant.IsDefeated && participant.MaxHealth > 0 && participant.CurrentHealth == 0) section.Warnings.Add(Issue("zero_health_not_defeated", "warning", "Participant has zero CurrentHealth but is not defeated.", participant.Id, "participant"));
            if (participant.Conditions == null)
            {
                section.Errors.Add(Issue("conditions_null", "error", "Conditions list must not be null.", participant.Id, "condition"));
                continue;
            }

            foreach (var condition in participant.Conditions)
            {
                if (condition == null)
                {
                    section.Errors.Add(Issue("condition_null", "error", "Condition entry must not be null.", participant.Id, "condition"));
                    continue;
                }

                if (condition.StackCount < 0) section.Errors.Add(Issue("condition_stack_negative", "error", "Condition StackCount must be greater than or equal to zero.", condition.ConditionInstanceId, "condition"));
                if (string.Equals(condition.DurationMode, "rounds", StringComparison.OrdinalIgnoreCase) && condition.RemainingRounds < 0)
                    section.Errors.Add(Issue("condition_remaining_rounds_negative", "error", "Round-based conditions must not have negative RemainingRounds.", condition.ConditionInstanceId, "condition"));
                if (string.IsNullOrWhiteSpace(condition.ConditionDefinitionId))
                    section.Warnings.Add(Issue("condition_definition_missing", "warning", "ConditionDefinitionId should be set for combat condition state.", condition.ConditionInstanceId, "condition"));
            }

            var duplicateActiveDefinitions = participant.Conditions
                .Where(x => x != null
                    && string.Equals(x.Status, CombatConditionStatuses.Active, StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(x.ConditionDefinitionId)
                    && x.MaxStacks <= 1)
                .GroupBy(x => x.ConditionDefinitionId, StringComparer.OrdinalIgnoreCase)
                .Where(x => x.Count() > 1);
            foreach (var duplicate in duplicateActiveDefinitions)
            {
                section.Warnings.Add(Issue("active_duplicate_unique_condition", "warning", "Participant has duplicate active unique condition entries.", duplicate.Key, "condition"));
            }
        }
        Finalize(section);
        return section;
    }

    public CombatDiagnosticsSection ValidateInitiative(CombatEncounterState encounter, IReadOnlyCollection<CombatParticipantState> participants)
    {
        var section = Section("initiative");
        Add(section, CombatRuntimeValidator.ValidateInitiativeOrder(encounter, participants));
        var entryIds = new HashSet<string>((encounter.InitiativeOrder ?? new List<CombatInitiativeEntry>()).Select(x => x.ParticipantId ?? string.Empty), StringComparer.OrdinalIgnoreCase);
        foreach (var participant in (participants ?? new List<CombatParticipantState>()).Where(x => x.IsActive && !x.IsDefeated))
        {
            if (!entryIds.Contains(participant.Id))
                section.Warnings.Add(Issue("active_participant_missing_initiative", "warning", "Active participant has no initiative entry.", participant.Id, "participant"));
        }

        Finalize(section);
        return section;
    }

    public CombatDiagnosticsSection ValidateTurnState(CombatEncounterState encounter, IReadOnlyCollection<CombatTurnState> turns, IReadOnlyCollection<CombatParticipantState> participants)
    {
        var section = Section("turns");
        var participantIds = new HashSet<string>((participants ?? new List<CombatParticipantState>()).Select(x => x.Id), StringComparer.OrdinalIgnoreCase);
        foreach (var turn in turns ?? new List<CombatTurnState>())
        {
            Add(section, CombatRuntimeValidator.ValidateTurn(turn));
            if (!string.IsNullOrWhiteSpace(turn.ParticipantId) && !participantIds.Contains(turn.ParticipantId))
                section.Errors.Add(Issue("turn_participant_missing", "error", "Turn references missing participant.", turn.ParticipantId, "turn"));
        }

        if (string.Equals(encounter.Status, CombatRuntimeStatuses.Active, StringComparison.OrdinalIgnoreCase)
            && turns != null
            && turns.Count > 0
            && !turns.Any(x => x.RoundNumber == encounter.RoundNumber && x.TurnIndex == encounter.ActiveTurnIndex))
        {
            section.Warnings.Add(Issue("current_turn_missing", "warning", "No turn state exists for the encounter current turn index.", encounter.Id, "turn"));
        }

        Finalize(section);
        return section;
    }

    public CombatDiagnosticsSection ValidateActions(IReadOnlyCollection<CombatActionState> actions, IReadOnlyCollection<CombatParticipantState> participants)
    {
        var section = Section("actions");
        var participantIds = new HashSet<string>((participants ?? new List<CombatParticipantState>()).Select(x => x.Id), StringComparer.OrdinalIgnoreCase);
        foreach (var action in actions ?? new List<CombatActionState>())
        {
            Add(section, CombatRuntimeValidator.ValidateAction(action));
            if (!string.IsNullOrWhiteSpace(action.ActorParticipantId) && !participantIds.Contains(action.ActorParticipantId))
                section.Errors.Add(Issue("action_actor_missing", "error", "Action references missing actor participant.", action.ActorParticipantId, "action"));
            foreach (var targetId in action.TargetParticipantIds ?? new List<string>())
            {
                if (!participantIds.Contains(targetId))
                    section.Warnings.Add(Issue("action_target_missing", "warning", "Action references missing target participant.", targetId, "action"));
            }
        }

        Finalize(section);
        return section;
    }

    private static CombatDiagnosticsSection Section(string name) => new CombatDiagnosticsSection { Section = name ?? string.Empty };

    private static void Add(CombatDiagnosticsSection section, CombatRuntimeValidationResult result)
    {
        if (result == null) return;
        section.Errors.AddRange(result.Errors);
        section.Warnings.AddRange(result.Warnings);
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

    private static void Finalize(CombatDiagnosticsSection section)
    {
        section.IsValid = section.Errors.Count == 0;
    }

    private static bool IsKnownStatus(string status)
    {
        return string.Equals(status, CombatRuntimeStatuses.Draft, StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, CombatRuntimeStatuses.Active, StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, CombatRuntimeStatuses.Paused, StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, CombatRuntimeStatuses.Ended, StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, CombatRuntimeStatuses.Cancelled, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class CombatActionEconomyService : ICombatActionEconomyService
{
    private static readonly HashSet<string> AllowedActionTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        CombatActionTypes.Move,
        CombatActionTypes.Interact,
        CombatActionTypes.Prepare,
        CombatActionTypes.Wait,
        CombatActionTypes.Skip,
        CombatActionTypes.Reaction,
        CombatActionTypes.GmNote,
        CombatActionTypes.Custom
    };

    private static readonly HashSet<string> UnsupportedMechanicActionTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "attack",
        "damage",
        "spell_damage",
        "apply_condition",
        "armor_penetration"
    };

    private readonly ICombatEncounterRepository _encounters;
    private readonly ICombatParticipantRepository _participants;
    private readonly ICombatActionRepository _actions;
    private readonly ICombatLogWriter _logWriter;
    private readonly ICombatSnapshotService _snapshotService;
    private readonly ICombatPayloadSummaryBuilder _payloadSummaryBuilder;
    private readonly IServerLogger _logger;

    public CombatActionEconomyService(
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

    public async Task<CombatActionEconomyResponse> DeclareActionAsync(CombatActionDeclareRequest request, UserAccount actor)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (string.IsNullOrWhiteSpace(request.RequestId)) throw new ArgumentException("operation_id_required", nameof(request));
        var encounter = await RequireEncounterAsync(request.EncounterId);
        EnsureEncounterCanAcceptActions(encounter);
        var participant = await RequireParticipantAsync(request.ActorParticipantId, encounter.Id);
        EnsureActorCanControl(participant, actor);
        var replay = await _actions.GetByRequestIdAsync(encounter.Id, request.RequestId, participant.Id);
        if (replay != null)
        {
            var replayResponse = await ResponseAsync(encounter.Id, replay.Id, participant.Id, replay.Status, participant,
                new List<string> { "action_idempotent_replay_no_respend" }, actor);
            replayResponse.AlreadyApplied = true;
            replayResponse.Message = "Действие уже было объявлено; очки действия повторно не списаны.";
            return replayResponse;
        }
        if (!participant.IsActive || participant.IsDefeated) throw new InvalidOperationException("actor participant is not able to act");
        ValidateSafeActionType(request.ActionType);
        var canonicalCost = CombatActionEconomyPolicy0219.CostFor(request.ActionType);
        if (string.Equals(request.ActionType, CombatActionTypes.Prepare, StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(PayloadText(request.PayloadSummary, "triggerDefinitionId")))
            throw new ArgumentException("prepared_trigger_definition_required", nameof(request));
        ValidateCosts(canonicalCost.HalfActions, 0, canonicalCost.Reactions);
        await ValidateTargetsAsync(request.TargetParticipantIds, encounter.Id);

        var action = new CombatActionState
        {
            Id = Guid.NewGuid().ToString("N"),
            EncounterId = encounter.Id,
            RoundNumber = Math.Max(0, encounter.RoundNumber),
            TurnIndex = Math.Max(0, encounter.ActiveTurnIndex),
            ActorParticipantId = participant.Id,
            ActionType = NormalizeActionType(request.ActionType),
            ActionName = request.ActionName ?? string.Empty,
            TargetParticipantIds = SafeList(request.TargetParticipantIds),
            TargetLocationSummary = request.TargetLocationSummary ?? string.Empty,
            ActionPointCost = canonicalCost.HalfActions,
            MinorActionPointCost = 0,
            ReactionCost = canonicalCost.Reactions,
            Status = CombatActionStatuses.Declared,
            RequestId = request.RequestId ?? string.Empty,
            ActorUserId = actor?.Id ?? string.Empty,
            CreatedAtUtc = DateTime.UtcNow,
            PayloadSummary = _payloadSummaryBuilder.BuildLogPayloadSummary(CombatEventTypes.ActionDeclared, request.PayloadSummary),
            Notes = request.Notes ?? string.Empty
        };

        ValidateOrThrow(CombatRuntimeValidator.ValidateAction(action));
        await _actions.AppendAsync(action);

        var warnings = new List<string>();
        if (CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatActionPointSpending)))
        {
            await SpendPointsInternalAsync(participant, action.ActionPointCost, action.MinorActionPointCost, action.ReactionCost);
        }
        else
        {
            warnings.Add("action point spending disabled");
        }

        await WriteLogAsync(encounter, action.ActorParticipantId, CombatEventTypes.ActionDeclared,
            string.Equals(action.ActionType, CombatActionTypes.Prepare, StringComparison.OrdinalIgnoreCase)
                ? $"Подготовлено действие «{action.ActionName}»."
                : $"Объявлено действие «{action.ActionName}».", request.RequestId, new Dictionary<string, object>
        {
            { "actionId", action.Id },
            { "actionType", action.ActionType },
            { "actionName", action.ActionName },
            { "status", action.Status }
        });

        _logger.Debug($"combat.action.declare.done encounterId={encounter.Id} actionId={action.Id}");
        return await ResponseAsync(encounter.Id, action.Id, action.ActorParticipantId, action.Status, participant, warnings, actor);
    }

    public async Task<CombatActionEconomyResponse> CompleteActionAsync(CombatActionCompleteRequest request, UserAccount actor)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        var encounter = await RequireEncounterAsync(request.EncounterId);
        var action = await RequireActionAsync(request.ActionId, encounter.Id);
        var participant = await _participants.GetByIdAsync(action.ActorParticipantId) ?? throw new KeyNotFoundException("actor participant missing");
        EnsureActorCanControl(participant, actor);
        if (IsTerminal(action.Status)) throw new InvalidOperationException("combat action is already terminal");
        var status = NormalizeResultStatus(request.ResultStatus);
        action.Status = status;
        await _actions.UpsertAsync(action);
        await WriteLogAsync(encounter, action.ActorParticipantId, CombatEventTypes.ActionResolved,
            string.IsNullOrWhiteSpace(request.Message) ? "Действие разрешено." : request.Message, request.RequestId, new Dictionary<string, object>
        {
            { "actionId", action.Id },
            { "status", action.Status }
        });

        _logger.Debug($"combat.action.complete.done encounterId={encounter.Id} actionId={action.Id} status={action.Status}");
        return await ResponseAsync(encounter.Id, action.Id, action.ActorParticipantId, action.Status, participant, new List<string>(), actor);
    }

    public async Task<CombatActionEconomyResponse> CancelActionAsync(CombatActionCancelRequest request, UserAccount actor)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        var encounter = await RequireEncounterAsync(request.EncounterId);
        var action = await RequireActionAsync(request.ActionId, encounter.Id);
        var participant = await _participants.GetByIdAsync(action.ActorParticipantId) ?? throw new KeyNotFoundException("actor participant missing");
        EnsureActorCanControl(participant, actor);
        if (string.Equals(action.Status, CombatActionStatuses.Completed, StringComparison.OrdinalIgnoreCase)
            || string.Equals(action.Status, CombatActionStatuses.Resolved, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("completed combat action cannot be cancelled");
        action.Status = CombatActionStatuses.Cancelled;
        await _actions.UpsertAsync(action);
        await WriteLogAsync(encounter, action.ActorParticipantId, CombatEventTypes.ActionCancelled, string.IsNullOrWhiteSpace(request.Reason) ? "Действие отменено." : request.Reason, request.RequestId, new Dictionary<string, object>
        {
            { "actionId", action.Id },
            { "status", action.Status }
        });

        _logger.Debug($"combat.action.cancel.done encounterId={encounter.Id} actionId={action.Id}");
        return await ResponseAsync(encounter.Id, action.Id, action.ActorParticipantId, action.Status, participant, new List<string> { "refund policy not implemented" }, actor);
    }

    public async Task<CombatActionEconomyResponse> SpendActionPointsAsync(CombatActionSpendRequest request, UserAccount actor)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        var encounter = await RequireEncounterAsync(request.EncounterId);
        var participant = await RequireParticipantAsync(request.ParticipantId, encounter.Id);
        EnsureActorCanControl(participant, actor);
        ValidateCosts(request.ActionPointCost, request.MinorActionPointCost, request.ReactionCost);
        var warnings = new List<string>();
        if (!CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatActionPointSpending)))
        {
            warnings.Add("action point spending disabled");
            return await ResponseAsync(encounter.Id, string.Empty, participant.Id, "spending_disabled", participant, warnings, actor);
        }

        await SpendPointsInternalAsync(participant, request.ActionPointCost, request.MinorActionPointCost, request.ReactionCost);
        await WriteLogAsync(encounter, participant.Id, CombatEventTypes.ActionPointsSpent, string.IsNullOrWhiteSpace(request.Reason) ? "Потрачены очки действия." : request.Reason, request.RequestId, new Dictionary<string, object>
        {
            { "participantId", participant.Id },
            { "actionPointCost", request.ActionPointCost },
            { "minorActionPointCost", request.MinorActionPointCost },
            { "reactionCost", request.ReactionCost }
        });

        _logger.Debug($"combat.action.spend.done encounterId={encounter.Id} participantId={participant.Id}");
        return await ResponseAsync(encounter.Id, string.Empty, participant.Id, "points_spent", participant, warnings, actor);
    }

    public async Task<CombatActionEconomyResponse> TriggerPreparedActionAsync(CombatPreparedActionTriggerRequest request, UserAccount actor)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (string.IsNullOrWhiteSpace(request.RequestId)) throw new ArgumentException("operation_id_required", nameof(request));
        var encounter = await RequireEncounterAsync(request.EncounterId);
        EnsureEncounterCanAcceptActions(encounter);
        var prepared = await RequireActionAsync(request.PreparedActionId, encounter.Id);
        if (!string.Equals(prepared.ActionType, CombatActionTypes.Prepare, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("prepared_action_required");
        var participant = await RequireParticipantAsync(prepared.ActorParticipantId, encounter.Id);
        EnsureActorCanControl(participant, actor);

        var replay = await _actions.GetByRequestIdAsync(encounter.Id, request.RequestId, participant.Id);
        if (replay != null)
        {
            var replayResponse = await ResponseAsync(encounter.Id, replay.Id, participant.Id, replay.Status, participant,
                new List<string> { "prepared_action_idempotent_replay_no_reaction_respend" }, actor);
            replayResponse.AlreadyApplied = true;
            replayResponse.Message = "Подготовленное действие уже сработало; реакция повторно не потрачена.";
            return replayResponse;
        }

        if (!string.Equals(prepared.Status, CombatActionStatuses.Declared, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("prepared_action_not_available");
        if (prepared.RoundNumber != encounter.RoundNumber)
            throw new InvalidOperationException("prepared_action_expired");
        var expectedTrigger = PayloadText(prepared.PayloadSummary, "triggerDefinitionId");
        if (string.IsNullOrWhiteSpace(expectedTrigger)
            || !string.Equals(expectedTrigger, request.TriggerDefinitionId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("prepared_trigger_context_mismatch");

        await ValidateTargetsAsync(request.TargetParticipantIds, encounter.Id);
        await SpendPointsInternalAsync(participant, 0, 0, 1);
        prepared.Status = CombatActionStatuses.Resolved;
        prepared.PayloadSummary["triggeredAtUtc"] = DateTime.UtcNow;
        prepared.PayloadSummary["triggerContext"] = request.TriggerContext ?? string.Empty;
        await _actions.UpsertAsync(prepared);

        var triggered = new CombatActionState
        {
            EncounterId = encounter.Id,
            RoundNumber = encounter.RoundNumber,
            TurnIndex = encounter.ActiveTurnIndex,
            ActorParticipantId = participant.Id,
            ActionType = CombatActionTypes.Reaction,
            ActionName = string.IsNullOrWhiteSpace(prepared.ActionName) ? "Подготовленное действие" : prepared.ActionName,
            TargetParticipantIds = SafeList(request.TargetParticipantIds),
            ReactionCost = 1,
            Status = CombatActionStatuses.Resolved,
            RequestId = request.RequestId,
            ActorUserId = actor?.Id ?? string.Empty,
            PayloadSummary = _payloadSummaryBuilder.BuildLogPayloadSummary(CombatEventTypes.PreparedActionTriggered,
                new Dictionary<string, object>
                {
                    { "preparedActionId", prepared.Id },
                    { "triggerDefinitionId", expectedTrigger },
                    { "triggerContext", request.TriggerContext ?? string.Empty }
                })
        };
        await _actions.AppendAsync(triggered);
        await WriteLogAsync(encounter, participant.Id, CombatEventTypes.PreparedActionTriggered,
            "Подготовленное действие сработало; реакция потрачена.", request.RequestId, triggered.PayloadSummary);
        return await ResponseAsync(encounter.Id, triggered.Id, participant.Id, triggered.Status, participant,
            new List<string>(), actor);
    }

    public Task<CombatRuntimeValidationResult> ValidateActionCostAsync(CombatParticipantState participant, int actionPointCost, int minorActionPointCost, int reactionCost, bool strictMode)
    {
        var result = new CombatRuntimeValidationResult();
        if (participant == null)
        {
            result.Errors.Add(Issue("participant_missing", "error", "Participant is required.", string.Empty, "participant"));
        }
        else
        {
            if (actionPointCost < 0 || minorActionPointCost < 0 || reactionCost < 0)
                result.Errors.Add(Issue("action_cost_negative", "error", "Action costs must not be negative.", participant.Id, "action"));
            if (actionPointCost > participant.ActionPoints)
                result.Errors.Add(Issue("action_points_insufficient", "error", "Participant does not have enough action points.", participant.Id, "participant"));
            if (minorActionPointCost > participant.MinorActionPoints)
                result.Errors.Add(Issue("minor_action_points_insufficient", "error", "Participant does not have enough minor action points.", participant.Id, "participant"));
            if (participant.ReactionCount + reactionCost > participant.ReactionLimit)
                result.Errors.Add(Issue("reaction_limit_exceeded", "error", "Reaction limit would be exceeded.", participant.Id, "participant"));
        }

        result.IsValid = result.Errors.Count == 0;
        return Task.FromResult(result);
    }

    private static void EnsureActorCanControl(CombatParticipantState participant, UserAccount actor)
    {
        var isAdmin = actor?.Roles?.Any(role => role == UserRole.Admin || role == UserRole.SuperAdmin) == true;
        if (isAdmin) return;
        if (actor == null || string.IsNullOrWhiteSpace(participant.ControllerUserId)
            || !string.Equals(participant.ControllerUserId, actor.Id, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("combat participant is not controlled by current user");
    }

    private async Task SpendPointsInternalAsync(CombatParticipantState participant, int actionPointCost, int minorActionPointCost, int reactionCost)
    {
        var validation = await ValidateActionCostAsync(participant, actionPointCost, minorActionPointCost, reactionCost, true);
        ValidateOrThrow(validation);
        participant.ActionPoints -= actionPointCost;
        participant.MinorActionPoints -= minorActionPointCost;
        participant.ReactionCount += reactionCost;
        await _participants.UpsertAsync(participant);
    }

    private async Task<CombatEncounterState> RequireEncounterAsync(string encounterId)
    {
        if (string.IsNullOrWhiteSpace(encounterId)) throw new ArgumentException("encounterId is required");
        var encounter = await _encounters.GetByIdAsync(encounterId);
        if (encounter == null) throw new KeyNotFoundException("combat encounter not found");
        return encounter;
    }

    private async Task<CombatParticipantState> RequireParticipantAsync(string participantId, string encounterId)
    {
        if (string.IsNullOrWhiteSpace(participantId)) throw new ArgumentException("participantId is required");
        var participant = await _participants.GetByIdAsync(participantId);
        if (participant == null || !string.Equals(participant.EncounterId, encounterId, StringComparison.OrdinalIgnoreCase))
            throw new KeyNotFoundException("combat participant not found");
        return participant;
    }

    private async Task<CombatActionState> RequireActionAsync(string actionId, string encounterId)
    {
        if (string.IsNullOrWhiteSpace(actionId)) throw new ArgumentException("actionId is required");
        var action = await _actions.GetByIdAsync(actionId);
        if (action == null || !string.Equals(action.EncounterId, encounterId, StringComparison.OrdinalIgnoreCase))
            throw new KeyNotFoundException("combat action not found");
        return action;
    }

    private async Task ValidateTargetsAsync(IEnumerable<string> targetParticipantIds, string encounterId)
    {
        foreach (var targetId in SafeList(targetParticipantIds))
        {
            var target = await _participants.GetByIdAsync(targetId);
            if (target == null || !string.Equals(target.EncounterId, encounterId, StringComparison.OrdinalIgnoreCase))
                throw new KeyNotFoundException("combat action target participant not found");
        }
    }

    private async Task WriteLogAsync(CombatEncounterState encounter, string actorParticipantId, string eventType, string message, string requestId, Dictionary<string, object> payload)
    {
        await _logWriter.AppendLogAndReplayAsync(
            new CombatLogWriteRequest
            {
                EncounterId = encounter.Id,
                CampaignId = encounter.CampaignId,
                SessionId = encounter.SessionId,
                RoundNumber = encounter.RoundNumber,
                TurnIndex = encounter.ActiveTurnIndex,
                ActorParticipantId = actorParticipantId ?? string.Empty,
                EventType = eventType,
                Message = message ?? string.Empty,
                SourcePayload = payload ?? new Dictionary<string, object>(),
                Visibility = CombatVisibilityIds.Public,
                RequestId = requestId ?? string.Empty
            },
            new CombatReplayWriteRequest
            {
                EncounterId = encounter.Id,
                EventType = eventType,
                RoundNumber = encounter.RoundNumber,
                TurnIndex = encounter.ActiveTurnIndex,
                ActorParticipantId = actorParticipantId ?? string.Empty,
                SourcePayload = payload ?? new Dictionary<string, object>(),
                Visibility = CombatVisibilityIds.Public,
                RequestId = requestId ?? string.Empty
            });
    }

    private async Task<CombatActionEconomyResponse> ResponseAsync(string encounterId, string actionId, string participantId, string status, CombatParticipantState participant, List<string> warnings, UserAccount actor)
    {
        var snapshot = await _snapshotService.BuildFullSnapshotAsync(new CombatFullSnapshotRequest
        {
            EncounterId = encounterId,
            IncludeParticipants = true,
            IncludeTurns = true,
            IncludeRounds = true,
            IncludeActions = true,
            IncludeLogs = true,
            LimitActions = 100,
            LimitLogs = 100
        }, actor);
        return new CombatActionEconomyResponse
        {
            EncounterId = encounterId ?? string.Empty,
            ActionId = actionId ?? string.Empty,
            ActorParticipantId = participantId ?? string.Empty,
            Status = status ?? string.Empty,
            ActionPointsRemaining = participant?.ActionPoints ?? 0,
            MinorActionPointsRemaining = participant?.MinorActionPoints ?? 0,
            ReactionsUsed = participant?.ReactionCount ?? 0,
            ReactionLimit = participant?.ReactionLimit ?? 0,
            Message = status ?? string.Empty,
            Warnings = warnings ?? new List<string>(),
            Snapshot = snapshot
        };
    }

    private static void EnsureEncounterCanAcceptActions(CombatEncounterState encounter)
    {
        if (string.Equals(encounter.Status, CombatRuntimeStatuses.Ended, StringComparison.OrdinalIgnoreCase)
            || string.Equals(encounter.Status, CombatRuntimeStatuses.Cancelled, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("combat encounter cannot accept actions in its current status");
    }

    private static void ValidateSafeActionType(string actionType)
    {
        var normalized = NormalizeActionType(actionType);
        if (UnsupportedMechanicActionTypes.Contains(normalized))
            throw new NotSupportedException("combat action type not supported in foundation 0.9");
        if (!AllowedActionTypes.Contains(normalized))
            throw new NotSupportedException("combat action type not supported in foundation 0.9");
    }

    private static string NormalizeActionType(string actionType)
    {
        return string.IsNullOrWhiteSpace(actionType) ? CombatActionTypes.Custom : actionType.Trim();
    }

    private static void ValidateCosts(int actionPointCost, int minorActionPointCost, int reactionCost)
    {
        if (actionPointCost < 0 || minorActionPointCost < 0 || reactionCost < 0)
            throw new ArgumentException("action costs must be non-negative");
    }

    private static string NormalizeResultStatus(string status)
    {
        if (string.Equals(status, CombatActionStatuses.Resolved, StringComparison.OrdinalIgnoreCase)) return CombatActionStatuses.Resolved;
        if (string.Equals(status, CombatActionStatuses.Failed, StringComparison.OrdinalIgnoreCase)) return CombatActionStatuses.Failed;
        if (string.Equals(status, CombatActionStatuses.Cancelled, StringComparison.OrdinalIgnoreCase)) return CombatActionStatuses.Cancelled;
        return CombatActionStatuses.Completed;
    }

    private static bool IsTerminal(string status)
    {
        return string.Equals(status, CombatActionStatuses.Completed, StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, CombatActionStatuses.Resolved, StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, CombatActionStatuses.Failed, StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, CombatActionStatuses.Cancelled, StringComparison.OrdinalIgnoreCase);
    }

    private static List<string> SafeList(IEnumerable<string> values)
    {
        return (values ?? Enumerable.Empty<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string PayloadText(IDictionary<string, object> payload, string key)
    {
        return payload != null && payload.TryGetValue(key, out var value)
            ? Convert.ToString(value) ?? string.Empty
            : string.Empty;
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

    private static void ValidateOrThrow(CombatRuntimeValidationResult result)
    {
        if (result == null || result.IsValid) return;
        throw new ArgumentException(string.Join("; ", result.Errors.Select(x => $"{x.Code}: {x.Message}")));
    }
}
