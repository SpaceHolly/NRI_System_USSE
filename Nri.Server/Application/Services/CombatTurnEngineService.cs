using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nri.Server.Infrastructure.Mongo.Repositories;
using Nri.Server.Logging;
using Nri.Shared.Domain;

namespace Nri.Server.Application.Services;

public interface ICombatTurnEngineService
{
    Task<CombatTurnEngineResponse> SortInitiativeAsync(CombatInitiativeSortRequest request, UserAccount actor);
    Task<CombatTurnEngineResponse> StartRoundAsync(CombatRoundStartRequest request, UserAccount actor);
    Task<CombatTurnEngineResponse> StartTurnAsync(CombatTurnStartRequest request, UserAccount actor);
    Task<CombatTurnEngineResponse> EndTurnAsync(CombatTurnEndRequest request, UserAccount actor);
    Task<CombatTurnEngineResponse> MoveToNextTurnAsync(CombatNextTurnRequest request, UserAccount actor);
    Task<CombatTurnEngineResponse> MoveToNextRoundAsync(CombatNextRoundRequest request, UserAccount actor);
    Task<CombatTurnEngineResponse> SkipTurnAsync(CombatSkipTurnRequest request, UserAccount actor);
    Task<CombatTurnEngineResponse> DelayTurnAsync(CombatDelayTurnRequest request, UserAccount actor);
}

public sealed class CombatTurnEngineService : ICombatTurnEngineService
{
    private const string SortModeDescending = "descending_initiative_then_tiebreaker";
    private const string SortModeOrderIndexOnly = "order_index_only";
    private const string SortModeManualKeepCurrent = "manual_keep_current";

    private readonly ICombatEncounterRepository _encounters;
    private readonly ICombatParticipantRepository _participants;
    private readonly ICombatTurnRepository _turns;
    private readonly ICombatRoundRepository _rounds;
    private readonly ICombatLogWriter _logWriter;
    private readonly IServerLogger _logger;

    public CombatTurnEngineService(
        ICombatEncounterRepository encounters,
        ICombatParticipantRepository participants,
        ICombatTurnRepository turns,
        ICombatRoundRepository rounds,
        ICombatLogWriter logWriter,
        IServerLogger logger)
    {
        _encounters = encounters;
        _participants = participants;
        _turns = turns;
        _rounds = rounds;
        _logWriter = logWriter;
        _logger = logger;
    }

    public async Task<CombatTurnEngineResponse> SortInitiativeAsync(CombatInitiativeSortRequest request, UserAccount actor)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        var encounter = await RequireEncounterAsync(request.EncounterId);
        EnsureMutableEncounter(encounter);
        var previous = encounter.ActiveParticipantId;
        var participants = await LoadParticipantsAsync(encounter.Id);
        var activeIds = ActiveParticipants(participants.Values).Select(x => x.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var entries = EnsureInitiativeEntries(encounter, participants.Values)
            .Where(x => activeIds.Contains(x.ParticipantId))
            .ToList();

        var sortMode = string.IsNullOrWhiteSpace(request.SortMode) ? SortModeDescending : request.SortMode.Trim();
        if (string.Equals(sortMode, SortModeDescending, StringComparison.OrdinalIgnoreCase))
        {
            entries = entries
                .OrderByDescending(x => x.Initiative)
                .ThenByDescending(x => x.TieBreaker)
                .ThenBy(x => x.OrderIndex)
                .ToList();
        }
        else if (string.Equals(sortMode, SortModeOrderIndexOnly, StringComparison.OrdinalIgnoreCase)
            || string.Equals(sortMode, SortModeManualKeepCurrent, StringComparison.OrdinalIgnoreCase))
        {
            entries = entries.OrderBy(x => x.OrderIndex).ToList();
        }
        else
        {
            throw new ArgumentException("Unsupported initiative sort mode.");
        }

        Reindex(entries);
        encounter.InitiativeOrder = entries;
        if (string.IsNullOrWhiteSpace(encounter.ActiveParticipantId) || !activeIds.Contains(encounter.ActiveParticipantId))
        {
            var first = entries.FirstOrDefault();
            encounter.ActiveTurnIndex = first?.OrderIndex ?? 0;
            encounter.ActiveParticipantId = first?.ParticipantId ?? string.Empty;
        }
        else
        {
            var current = entries.FirstOrDefault(x => string.Equals(x.ParticipantId, encounter.ActiveParticipantId, StringComparison.OrdinalIgnoreCase));
            if (current != null) encounter.ActiveTurnIndex = current.OrderIndex;
        }

        encounter.LastUpdatedAtUtc = DateTime.UtcNow;
        ValidateOrThrow(CombatRuntimeValidator.ValidateInitiativeOrder(encounter, participants.Values));
        await _encounters.UpsertAsync(encounter);
        await WriteTransitionLogAsync(encounter, CombatEventTypes.InitiativeSorted, "Initiative order sorted.", actor, request.RequestId, encounter.ActiveParticipantId);
        _logger.Admin($"combat.v1.initiative.sort.done encounterId={encounter.Id} count={entries.Count}");
        return await BuildResponseAsync(encounter, previous, true, "Initiative order sorted.");
    }

    public async Task<CombatTurnEngineResponse> StartRoundAsync(CombatRoundStartRequest request, UserAccount actor)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        var encounter = await RequireEncounterAsync(request.EncounterId);
        EnsureMutableEncounter(encounter);
        var previous = encounter.ActiveParticipantId;
        var targetRound = request.RoundNumber > 0 ? request.RoundNumber : (encounter.RoundNumber > 0 ? encounter.RoundNumber : 1);
        var participants = await LoadParticipantsAsync(encounter.Id);
        await StartRoundInternalAsync(encounter, participants, targetRound, actor, request.RequestId, startFirstTurn: false, endPreviousRound: false);
        _logger.Admin($"combat.v1.round.start.done encounterId={encounter.Id} round={encounter.RoundNumber}");
        return await BuildResponseAsync(encounter, previous, true, "Round started.");
    }

    public async Task<CombatTurnEngineResponse> StartTurnAsync(CombatTurnStartRequest request, UserAccount actor)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        Require(request.ParticipantId, "participantId is required");
        var encounter = await RequireEncounterAsync(request.EncounterId);
        EnsureMutableEncounter(encounter);
        var previous = encounter.ActiveParticipantId;
        var participants = await LoadParticipantsAsync(encounter.Id);
        var participant = RequireParticipant(participants, request.ParticipantId);
        EnsureParticipantCanAct(participant);
        var index = FindOrAppendInitiativeIndex(encounter, participant);
        await StartTurnInternalAsync(encounter, participant, index, actor, request.RequestId, new List<string>());
        _logger.Admin($"combat.v1.turn.start.done encounterId={encounter.Id} participantId={participant.Id}");
        return await BuildResponseAsync(encounter, previous, true, "Turn started.");
    }

    public async Task<CombatTurnEngineResponse> EndTurnAsync(CombatTurnEndRequest request, UserAccount actor)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        Require(request.ParticipantId, "participantId is required");
        var encounter = await RequireEncounterAsync(request.EncounterId);
        EnsureMutableEncounter(encounter);
        var previous = encounter.ActiveParticipantId;
        var participants = await LoadParticipantsAsync(encounter.Id);
        var participant = RequireParticipant(participants, request.ParticipantId);
        var warnings = new List<string>();

        var turn = await FindCurrentTurnAsync(encounter, participant.Id);
        if (turn == null)
        {
            warnings.Add("active turn state missing; completed placeholder created");
            turn = CreateTurn(encounter, participant, encounter.ActiveTurnIndex, CombatTurnStatuses.Completed);
        }

        turn.Status = CombatTurnStatuses.Completed;
        turn.EndedAtUtc = DateTime.UtcNow;
        turn.ActionPointsSpent = Math.Max(0, turn.ActionPointsStarted - participant.ActionPoints);
        turn.MinorActionPointsSpent = Math.Max(0, turn.MinorActionPointsStarted - participant.MinorActionPoints);
        turn.ReactionsUsed = participant.ReactionCount;
        turn.Notes = SafeReason(turn.Notes, request.Reason);
        ValidateOrThrow(CombatRuntimeValidator.ValidateTurn(turn));
        await _turns.UpsertAsync(turn);
        await AddTurnToRoundAsync(encounter, turn, participant.Id, completed: true);

        participant.HasActedThisRound = true;
        participant.ActionPoints = 0;
        participant.MinorActionPoints = 0;
        await _participants.UpsertAsync(participant);
        encounter.LastUpdatedAtUtc = DateTime.UtcNow;
        await _encounters.UpsertAsync(encounter);
        await WriteTransitionLogAsync(encounter, CombatEventTypes.TurnEnded, SafeReason("Turn ended.", request.Reason), actor, request.RequestId, participant.Id);
        _logger.Admin($"combat.v1.turn.end.done encounterId={encounter.Id} participantId={participant.Id}");
        return await BuildResponseAsync(encounter, previous, true, "Turn ended.", warnings);
    }

    public async Task<CombatTurnEngineResponse> MoveToNextTurnAsync(CombatNextTurnRequest request, UserAccount actor)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        var encounter = await RequireEncounterAsync(request.EncounterId);
        EnsureMutableEncounter(encounter);
        var previous = encounter.ActiveParticipantId;
        var participants = await LoadParticipantsAsync(encounter.Id);
        var activeEntries = ActiveOrderedEntries(encounter, participants.Values).ToList();
        if (activeEntries.Count == 0)
        {
            encounter.ActiveParticipantId = string.Empty;
            encounter.ActiveTurnIndex = 0;
            encounter.LastUpdatedAtUtc = DateTime.UtcNow;
            await _encounters.UpsertAsync(encounter);
            return await BuildResponseAsync(encounter, previous, false, "No active participants available.", new[] { "no active participants available" });
        }

        var nextEntry = FindNextEntry(encounter, activeEntries, participants);
        if (nextEntry == null)
        {
            await StartNextRoundInternalAsync(encounter, participants, actor, request.RequestId, startFirstTurn: true);
            _logger.Admin($"combat.v1.turn.next.new_round encounterId={encounter.Id} round={encounter.RoundNumber}");
            return await BuildResponseAsync(encounter, previous, true, "Next round started.");
        }

        var participant = RequireParticipant(participants, nextEntry.ParticipantId);
        await StartTurnInternalAsync(encounter, participant, nextEntry.OrderIndex, actor, request.RequestId, new List<string>());
        _logger.Admin($"combat.v1.turn.next.done encounterId={encounter.Id} participantId={participant.Id}");
        return await BuildResponseAsync(encounter, previous, true, "Next turn started.");
    }

    public async Task<CombatTurnEngineResponse> MoveToNextRoundAsync(CombatNextRoundRequest request, UserAccount actor)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        var encounter = await RequireEncounterAsync(request.EncounterId);
        EnsureMutableEncounter(encounter);
        var previous = encounter.ActiveParticipantId;
        var participants = await LoadParticipantsAsync(encounter.Id);
        await StartNextRoundInternalAsync(encounter, participants, actor, request.RequestId, startFirstTurn: true);
        _logger.Admin($"combat.v1.round.next.done encounterId={encounter.Id} round={encounter.RoundNumber}");
        return await BuildResponseAsync(encounter, previous, true, "Next round started.");
    }

    public async Task<CombatTurnEngineResponse> SkipTurnAsync(CombatSkipTurnRequest request, UserAccount actor)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        Require(request.ParticipantId, "participantId is required");
        var encounter = await RequireEncounterAsync(request.EncounterId);
        EnsureMutableEncounter(encounter);
        var previous = encounter.ActiveParticipantId;
        var participants = await LoadParticipantsAsync(encounter.Id);
        var participant = RequireParticipant(participants, request.ParticipantId);
        var index = FindOrAppendInitiativeIndex(encounter, participant);
        var turn = await FindCurrentTurnAsync(encounter, participant.Id) ?? CreateTurn(encounter, participant, index, CombatTurnStatuses.Skipped);
        turn.Status = CombatTurnStatuses.Skipped;
        turn.Skipped = true;
        turn.SkipReason = request.Reason ?? string.Empty;
        turn.EndedAtUtc = DateTime.UtcNow;
        ValidateOrThrow(CombatRuntimeValidator.ValidateTurn(turn));
        await _turns.UpsertAsync(turn);
        await AddTurnToRoundAsync(encounter, turn, participant.Id, completed: true);

        participant.HasActedThisRound = true;
        participant.ActionPoints = 0;
        participant.MinorActionPoints = 0;
        await _participants.UpsertAsync(participant);
        encounter.ActiveTurnIndex = index;
        encounter.ActiveParticipantId = participant.Id;
        encounter.LastUpdatedAtUtc = DateTime.UtcNow;
        await _encounters.UpsertAsync(encounter);
        await WriteTransitionLogAsync(encounter, CombatEventTypes.TurnSkipped, SafeReason("Turn skipped.", request.Reason), actor, request.RequestId, participant.Id);
        _logger.Admin($"combat.v1.turn.skip.done encounterId={encounter.Id} participantId={participant.Id}");
        return await BuildResponseAsync(encounter, previous, true, "Turn skipped.");
    }

    public async Task<CombatTurnEngineResponse> DelayTurnAsync(CombatDelayTurnRequest request, UserAccount actor)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        Require(request.ParticipantId, "participantId is required");
        var encounter = await RequireEncounterAsync(request.EncounterId);
        EnsureMutableEncounter(encounter);
        var previous = encounter.ActiveParticipantId;
        var participants = await LoadParticipantsAsync(encounter.Id);
        var participant = RequireParticipant(participants, request.ParticipantId);
        EnsureParticipantCanAct(participant);

        var entries = EnsureInitiativeEntries(encounter, participants.Values).ToList();
        var entry = entries.FirstOrDefault(x => string.Equals(x.ParticipantId, participant.Id, StringComparison.OrdinalIgnoreCase));
        if (entry == null) throw new InvalidOperationException("initiative entry missing");
        entries.Remove(entry);
        entry.IsDelayed = true;
        entry.IsSkipped = false;
        entry.Notes = SafeReason(entry.Notes, request.Reason);
        entries.Add(entry);
        Reindex(entries);
        encounter.InitiativeOrder = entries;
        var delayedEntry = entries.First(x => string.Equals(x.ParticipantId, participant.Id, StringComparison.OrdinalIgnoreCase));
        if (string.Equals(previous, participant.Id, StringComparison.OrdinalIgnoreCase))
        {
            encounter.ActiveTurnIndex = 0;
            encounter.ActiveParticipantId = string.Empty;
        }
        else
        {
            encounter.ActiveTurnIndex = delayedEntry.OrderIndex;
        }
        encounter.LastUpdatedAtUtc = DateTime.UtcNow;
        participant.HasActedThisRound = false;
        await _participants.UpsertAsync(participant);
        ValidateOrThrow(CombatRuntimeValidator.ValidateInitiativeOrder(encounter, participants.Values));
        await _encounters.UpsertAsync(encounter);
        await WriteTransitionLogAsync(encounter, CombatEventTypes.TurnDelayed, SafeReason("Turn delayed.", request.Reason), actor, request.RequestId, participant.Id);
        _logger.Admin($"combat.v1.turn.delay.done encounterId={encounter.Id} participantId={participant.Id}");
        return await BuildResponseAsync(encounter, previous, true, "Turn delayed.");
    }

    private async Task StartNextRoundInternalAsync(CombatEncounterState encounter, Dictionary<string, CombatParticipantState> participants, UserAccount actor, string requestId, bool startFirstTurn)
    {
        var currentRound = await _rounds.GetByEncounterRoundAsync(encounter.Id, encounter.RoundNumber);
        if (currentRound != null && currentRound.EndedAtUtc == null)
        {
            currentRound.EndedAtUtc = DateTime.UtcNow;
            await _rounds.UpsertAsync(currentRound);
            await WriteTransitionLogAsync(encounter, CombatEventTypes.RoundEnded, "Round ended.", actor, requestId, encounter.ActiveParticipantId);
        }

        var nextRound = encounter.RoundNumber > 0 ? encounter.RoundNumber + 1 : 1;
        await StartRoundInternalAsync(encounter, participants, nextRound, actor, requestId, startFirstTurn, endPreviousRound: false);
    }

    private async Task StartRoundInternalAsync(CombatEncounterState encounter, Dictionary<string, CombatParticipantState> participants, int roundNumber, UserAccount actor, string requestId, bool startFirstTurn, bool endPreviousRound)
    {
        if (endPreviousRound)
        {
            var currentRound = await _rounds.GetByEncounterRoundAsync(encounter.Id, encounter.RoundNumber);
            if (currentRound != null && currentRound.EndedAtUtc == null)
            {
                currentRound.EndedAtUtc = DateTime.UtcNow;
                await _rounds.UpsertAsync(currentRound);
                await WriteTransitionLogAsync(encounter, CombatEventTypes.RoundEnded, "Round ended.", actor, requestId, encounter.ActiveParticipantId);
            }
        }

        encounter.InitiativeOrder = EnsureInitiativeEntries(encounter, participants.Values).OrderBy(x => x.OrderIndex).ToList();
        Reindex(encounter.InitiativeOrder);
        foreach (var roundParticipant in ActiveParticipants(participants.Values))
        {
            roundParticipant.HasActedThisRound = false;
            roundParticipant.ReactionCount = 0;
            if (roundParticipant.ActionPoints <= 0) roundParticipant.ActionPoints = 1;
            if (roundParticipant.MinorActionPoints <= 0) roundParticipant.MinorActionPoints = 1;
            await _participants.UpsertAsync(roundParticipant);
        }

        var firstEntry = ActiveOrderedEntries(encounter, participants.Values).FirstOrDefault();
        encounter.RoundNumber = roundNumber;
        encounter.ActiveTurnIndex = firstEntry?.OrderIndex ?? 0;
        encounter.ActiveParticipantId = firstEntry?.ParticipantId ?? string.Empty;
        encounter.Status = CombatRuntimeStatuses.Active;
        encounter.LastUpdatedAtUtc = DateTime.UtcNow;

        var round = await _rounds.GetByEncounterRoundAsync(encounter.Id, roundNumber) ?? new CombatRoundRuntimeState
        {
            EncounterId = encounter.Id,
            RoundNumber = roundNumber,
            StartedAtUtc = DateTime.UtcNow
        };
        round.EndedAtUtc = null;
        round.TurnIds ??= new List<string>();
        round.CompletedParticipantIds ??= new List<string>();
        await _rounds.UpsertAsync(round);
        ValidateOrThrow(CombatRuntimeValidator.ValidateInitiativeOrder(encounter, participants.Values));
        await _encounters.UpsertAsync(encounter);
        await WriteTransitionLogAsync(encounter, CombatEventTypes.RoundStarted, "Round started.", actor, requestId, encounter.ActiveParticipantId);

        if (startFirstTurn && firstEntry != null && participants.TryGetValue(firstEntry.ParticipantId, out var participant))
        {
            await StartTurnInternalAsync(encounter, participant, firstEntry.OrderIndex, actor, requestId, new List<string>());
        }
    }

    private async Task StartTurnInternalAsync(CombatEncounterState encounter, CombatParticipantState participant, int turnIndex, UserAccount actor, string requestId, List<string> warnings)
    {
        EnsureParticipantCanAct(participant);
        encounter.ActiveTurnIndex = Math.Max(0, turnIndex);
        encounter.ActiveParticipantId = participant.Id;
        encounter.Status = CombatRuntimeStatuses.Active;
        encounter.LastUpdatedAtUtc = DateTime.UtcNow;
        participant.HasActedThisRound = false;
        if (participant.ActionPoints <= 0) participant.ActionPoints = 1;
        if (participant.MinorActionPoints <= 0) participant.MinorActionPoints = 1;
        await _participants.UpsertAsync(participant);

        var turn = CreateTurn(encounter, participant, encounter.ActiveTurnIndex, CombatTurnStatuses.Active);
        ValidateOrThrow(CombatRuntimeValidator.ValidateTurn(turn));
        await _turns.UpsertAsync(turn);
        await AddTurnToRoundAsync(encounter, turn, participant.Id, completed: false);
        await _encounters.UpsertAsync(encounter);
        await WriteTransitionLogAsync(encounter, CombatEventTypes.TurnStarted, "Turn started.", actor, requestId, participant.Id);
    }

    private CombatTurnState CreateTurn(CombatEncounterState encounter, CombatParticipantState participant, int turnIndex, string status)
    {
        return new CombatTurnState
        {
            Id = TurnId(encounter.Id, encounter.RoundNumber, turnIndex, participant.Id),
            EncounterId = encounter.Id,
            RoundNumber = encounter.RoundNumber,
            TurnIndex = Math.Max(0, turnIndex),
            ParticipantId = participant.Id,
            Status = status,
            StartedAtUtc = DateTime.UtcNow,
            EndedAtUtc = string.Equals(status, CombatTurnStatuses.Active, StringComparison.OrdinalIgnoreCase) ? null : DateTime.UtcNow,
            Skipped = string.Equals(status, CombatTurnStatuses.Skipped, StringComparison.OrdinalIgnoreCase),
            ActionPointsStarted = participant.ActionPoints,
            MinorActionPointsStarted = participant.MinorActionPoints,
            ReactionsUsed = participant.ReactionCount,
            SchemaVersion = 1
        };
    }

    private async Task AddTurnToRoundAsync(CombatEncounterState encounter, CombatTurnState turn, string participantId, bool completed)
    {
        var round = await _rounds.GetByEncounterRoundAsync(encounter.Id, encounter.RoundNumber) ?? new CombatRoundRuntimeState
        {
            EncounterId = encounter.Id,
            RoundNumber = encounter.RoundNumber,
            StartedAtUtc = DateTime.UtcNow
        };
        round.TurnIds ??= new List<string>();
        round.CompletedParticipantIds ??= new List<string>();
        if (!round.TurnIds.Contains(turn.Id, StringComparer.OrdinalIgnoreCase)) round.TurnIds.Add(turn.Id);
        if (completed && !round.CompletedParticipantIds.Contains(participantId, StringComparer.OrdinalIgnoreCase)) round.CompletedParticipantIds.Add(participantId);
        await _rounds.UpsertAsync(round);
    }

    private async Task<CombatTurnState?> FindCurrentTurnAsync(CombatEncounterState encounter, string participantId)
    {
        var turnId = TurnId(encounter.Id, encounter.RoundNumber, encounter.ActiveTurnIndex, participantId);
        var turn = await _turns.GetByIdAsync(turnId);
        if (turn != null) return turn;

        var turns = await _turns.ListByEncounterAsync(encounter.Id, 500);
        return turns.LastOrDefault(x =>
            x.RoundNumber == encounter.RoundNumber
            && string.Equals(x.ParticipantId, participantId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.Status, CombatTurnStatuses.Active, StringComparison.OrdinalIgnoreCase));
    }

    private CombatInitiativeEntry? FindNextEntry(CombatEncounterState encounter, List<CombatInitiativeEntry> activeEntries, Dictionary<string, CombatParticipantState> participants)
    {
        if (string.IsNullOrWhiteSpace(encounter.ActiveParticipantId))
        {
            return activeEntries.OrderBy(x => x.OrderIndex).FirstOrDefault(x =>
                participants.TryGetValue(x.ParticipantId, out var participant) && !participant.HasActedThisRound);
        }

        var currentIndex = encounter.ActiveTurnIndex;
        foreach (var entry in activeEntries.Where(x => x.OrderIndex > currentIndex).OrderBy(x => x.OrderIndex))
        {
            if (participants.TryGetValue(entry.ParticipantId, out var participant) && !participant.HasActedThisRound) return entry;
        }

        return null;
    }

    private int FindOrAppendInitiativeIndex(CombatEncounterState encounter, CombatParticipantState participant)
    {
        encounter.InitiativeOrder ??= new List<CombatInitiativeEntry>();
        var entry = encounter.InitiativeOrder.FirstOrDefault(x => string.Equals(x.ParticipantId, participant.Id, StringComparison.OrdinalIgnoreCase));
        if (entry == null)
        {
            entry = new CombatInitiativeEntry
            {
                ParticipantId = participant.Id,
                Initiative = participant.Initiative,
                TieBreaker = participant.InitiativeTieBreaker,
                OrderIndex = encounter.InitiativeOrder.Count
            };
            encounter.InitiativeOrder.Add(entry);
        }

        Reindex(encounter.InitiativeOrder);
        return entry.OrderIndex;
    }

    private static IEnumerable<CombatInitiativeEntry> ActiveOrderedEntries(CombatEncounterState encounter, IEnumerable<CombatParticipantState> participants)
    {
        var activeIds = ActiveParticipants(participants).Select(x => x.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return (encounter.InitiativeOrder ?? new List<CombatInitiativeEntry>())
            .Where(x => activeIds.Contains(x.ParticipantId) && !x.IsSkipped)
            .OrderBy(x => x.OrderIndex);
    }

    private static IEnumerable<CombatParticipantState> ActiveParticipants(IEnumerable<CombatParticipantState> participants)
    {
        return (participants ?? Enumerable.Empty<CombatParticipantState>())
            .Where(x => x != null && x.IsActive && !x.IsDefeated);
    }

    private static List<CombatInitiativeEntry> EnsureInitiativeEntries(CombatEncounterState encounter, IEnumerable<CombatParticipantState> participants)
    {
        var entries = encounter.InitiativeOrder ?? new List<CombatInitiativeEntry>();
        var byId = entries
            .Where(x => !string.IsNullOrWhiteSpace(x.ParticipantId))
            .GroupBy(x => x.ParticipantId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var participant in participants ?? Enumerable.Empty<CombatParticipantState>())
        {
            if (participant == null || !participant.IsActive) continue;
            if (byId.ContainsKey(participant.Id)) continue;
            var entry = new CombatInitiativeEntry
            {
                ParticipantId = participant.Id,
                Initiative = participant.Initiative,
                TieBreaker = participant.InitiativeTieBreaker,
                OrderIndex = byId.Count
            };
            byId[participant.Id] = entry;
            entries.Add(entry);
        }

        Reindex(entries);
        return entries;
    }

    private static void Reindex(IList<CombatInitiativeEntry> entries)
    {
        for (var i = 0; i < entries.Count; i++)
        {
            entries[i].OrderIndex = i;
        }
    }

    private async Task<Dictionary<string, CombatParticipantState>> LoadParticipantsAsync(string encounterId)
    {
        var list = await _participants.ListByEncounterAsync(encounterId, 500);
        return list
            .Where(x => x != null)
            .GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
    }

    private async Task<CombatEncounterState> RequireEncounterAsync(string encounterId)
    {
        Require(encounterId, "encounterId is required");
        var encounter = await _encounters.GetByIdAsync(encounterId);
        if (encounter == null) throw new KeyNotFoundException("Combat encounter not found.");
        return encounter;
    }

    private static CombatParticipantState RequireParticipant(Dictionary<string, CombatParticipantState> participants, string participantId)
    {
        if (participants.TryGetValue(participantId ?? string.Empty, out var participant)) return participant;
        throw new KeyNotFoundException("Combat participant not found.");
    }

    private static void EnsureParticipantCanAct(CombatParticipantState participant)
    {
        if (!participant.IsActive) throw new InvalidOperationException("Combat participant is inactive.");
        if (participant.IsDefeated) throw new InvalidOperationException("Defeated participant cannot act.");
    }

    private static void EnsureMutableEncounter(CombatEncounterState encounter)
    {
        if (string.Equals(encounter.Status, CombatRuntimeStatuses.Ended, StringComparison.OrdinalIgnoreCase)
            || string.Equals(encounter.Status, CombatRuntimeStatuses.Cancelled, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Encounter is not mutable.");
        }
    }

    private async Task WriteTransitionLogAsync(CombatEncounterState encounter, string eventType, string message, UserAccount actor, string requestId, string actorParticipantId)
    {
        var source = new Dictionary<string, object>
        {
            { "encounterId", encounter.Id },
            { "roundNumber", encounter.RoundNumber },
            { "activeTurnIndex", encounter.ActiveTurnIndex },
            { "activeParticipantId", encounter.ActiveParticipantId }
        };

        await _logWriter.AppendLogAndReplayAsync(new CombatLogWriteRequest
        {
            EncounterId = encounter.Id,
            CampaignId = encounter.CampaignId,
            SessionId = encounter.SessionId,
            RoundNumber = encounter.RoundNumber,
            TurnIndex = encounter.ActiveTurnIndex,
            ActorParticipantId = actorParticipantId ?? string.Empty,
            ActorUserId = actor?.Id ?? string.Empty,
            EventType = eventType,
            Message = message ?? string.Empty,
            SourcePayload = source,
            Visibility = CombatVisibilityIds.Public,
            RequestId = requestId ?? string.Empty
        }, new CombatReplayWriteRequest
        {
            EncounterId = encounter.Id,
            EventType = eventType,
            RoundNumber = encounter.RoundNumber,
            TurnIndex = encounter.ActiveTurnIndex,
            ActorParticipantId = actorParticipantId ?? string.Empty,
            SourcePayload = source,
            Visibility = CombatVisibilityIds.Public,
            RequestId = requestId ?? string.Empty
        });
    }

    private async Task<CombatTurnEngineResponse> BuildResponseAsync(CombatEncounterState encounter, string previousParticipantId, bool changed, string message, IEnumerable<string>? warnings = null)
    {
        var participants = await _participants.ListByEncounterAsync(encounter.Id, 500);
        var response = new CombatTurnEngineResponse
        {
            EncounterId = encounter.Id,
            Status = encounter.Status,
            RoundNumber = encounter.RoundNumber,
            ActiveTurnIndex = encounter.ActiveTurnIndex,
            ActiveParticipantId = encounter.ActiveParticipantId,
            PreviousParticipantId = previousParticipantId ?? string.Empty,
            Changed = changed,
            Message = message ?? string.Empty,
            Snapshot = new CombatEncounterSnapshotResponse
            {
                Encounter = CombatEncounterManagementService.ToEncounterSummary(encounter),
                Participants = participants.Select(CombatEncounterManagementService.ToParticipantSummary).ToList()
            },
            Warnings = (warnings ?? Enumerable.Empty<string>()).Where(x => !string.IsNullOrWhiteSpace(x)).ToList()
        };

        response.Snapshot.Encounter.ParticipantCount = response.Snapshot.Participants.Count;
        response.Snapshot.Warnings.AddRange(response.Warnings);
        return response;
    }

    private static void ValidateOrThrow(CombatRuntimeValidationResult result)
    {
        if (result == null || result.IsValid) return;
        throw new ArgumentException(string.Join("; ", result.Errors.Select(x => $"{x.Code}: {x.Message}")));
    }

    private static string TurnId(string encounterId, int roundNumber, int turnIndex, string participantId)
    {
        return $"{encounterId}:r{roundNumber}:t{turnIndex}:{participantId}";
    }

    private static void Require(string value, string message)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException(message);
    }

    private static string SafeReason(string baseMessage, string reason)
    {
        return string.IsNullOrWhiteSpace(reason) ? baseMessage ?? string.Empty : $"{baseMessage} Reason: {reason.Trim()}";
    }
}
