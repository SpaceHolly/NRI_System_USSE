using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nri.Server.Infrastructure.Mongo.Repositories;
using Nri.Server.Logging;
using Nri.Shared.Domain;

namespace Nri.Server.Application.Services;

public interface ICombatEncounterManagementService
{
    Task<CombatEncounterCreateResponse> CreateEncounterAsync(CombatEncounterCreateRequest request, UserAccount actor);
    Task<CombatEncounterSummary> EndEncounterAsync(CombatEncounterEndRequest request, UserAccount actor);
    Task<CombatEncounterSummary> CancelEncounterAsync(CombatEncounterCancelRequest request, UserAccount actor);
    Task<CombatParticipantSummary> AddParticipantAsync(CombatParticipantAddRequest request, UserAccount actor);
    Task<CombatParticipantSummary> RemoveParticipantAsync(CombatParticipantRemoveRequest request, UserAccount actor);
    Task<CombatEncounterSnapshotResponse> GetSnapshotAsync(CombatEncounterSnapshotRequest request, UserAccount actor);
}

public sealed class CombatEncounterManagementService : ICombatEncounterManagementService
{
    private readonly ICombatEncounterRepository _encounters;
    private readonly ICombatParticipantRepository _participants;
    private readonly ICombatLogRepository _logs;
    private readonly ICombatReplayEventRepository _replayEvents;
    private readonly ICombatLogWriter _logWriter;
    private readonly IServerLogger _logger;

    public CombatEncounterManagementService(
        ICombatEncounterRepository encounters,
        ICombatParticipantRepository participants,
        ICombatLogRepository logs,
        ICombatReplayEventRepository replayEvents,
        ICombatLogWriter logWriter,
        IServerLogger logger)
    {
        _encounters = encounters;
        _participants = participants;
        _logs = logs;
        _replayEvents = replayEvents;
        _logWriter = logWriter;
        _logger = logger;
    }

    public async Task<CombatEncounterCreateResponse> CreateEncounterAsync(CombatEncounterCreateRequest request, UserAccount actor)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        Require(request.CampaignId, "campaignId is required");
        Require(request.SessionId, "sessionId is required");
        Require(request.RuleSetId, "ruleSetId is required");

        var now = DateTime.UtcNow;
        var encounter = new CombatEncounterState
        {
            Id = Guid.NewGuid().ToString("N"),
            CampaignId = request.CampaignId.Trim(),
            SessionId = request.SessionId.Trim(),
            RuleSetId = request.RuleSetId.Trim(),
            Name = string.IsNullOrWhiteSpace(request.Name) ? "Combat Encounter" : request.Name.Trim(),
            Status = CombatRuntimeStatuses.Active,
            RoundNumber = 0,
            ActiveTurnIndex = 0,
            ActiveParticipantId = string.Empty,
            ParticipantIds = new List<string>(),
            InitiativeOrder = new List<CombatInitiativeEntry>(),
            TeamIds = SafeList(request.TeamIds),
            StartedAtUtc = now,
            EndedAtUtc = null,
            CreatedByUserId = actor.Id,
            LastUpdatedAtUtc = now,
            Tags = SafeList(request.Tags),
            Notes = request.Notes ?? string.Empty,
            SchemaVersion = 1
        };

        ValidateOrThrow(CombatRuntimeValidator.ValidateEncounter(encounter));
        await _encounters.UpsertAsync(encounter);
        await WriteLogAsync(encounter, CombatEventTypes.EncounterStarted, "Бой начат.", actor, request.RequestId, string.Empty);
        await WriteReplayEventAsync(encounter, CombatEventTypes.EncounterStarted, actor, request.RequestId, string.Empty);

        _logger.Admin($"combat.v1.encounter.create.done encounterId={encounter.Id}");
        return new CombatEncounterCreateResponse
        {
            EncounterId = encounter.Id,
            Status = encounter.Status,
            CampaignId = encounter.CampaignId,
            SessionId = encounter.SessionId,
            RoundNumber = encounter.RoundNumber,
            ActiveTurnIndex = encounter.ActiveTurnIndex,
            CreatedAtUtc = now
        };
    }

    public async Task<CombatEncounterSummary> EndEncounterAsync(CombatEncounterEndRequest request, UserAccount actor)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        var encounter = await RequireEncounterAsync(request.EncounterId);
        if (string.Equals(encounter.Status, CombatRuntimeStatuses.Cancelled, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Cancelled encounter cannot be ended.");

        if (!string.Equals(encounter.Status, CombatRuntimeStatuses.Ended, StringComparison.OrdinalIgnoreCase))
        {
            var now = DateTime.UtcNow;
            encounter.Status = CombatRuntimeStatuses.Ended;
            encounter.EndedAtUtc = now;
            encounter.LastUpdatedAtUtc = now;
            await _encounters.UpsertAsync(encounter);
            await WriteLogAsync(encounter, CombatEventTypes.EncounterEnded, SafeReason("Бой завершён.", request.Reason), actor, request.RequestId, string.Empty);
            await WriteReplayEventAsync(encounter, CombatEventTypes.EncounterEnded, actor, request.RequestId, string.Empty);
        }

        _logger.Admin($"combat.v1.encounter.end.done encounterId={encounter.Id}");
        return ToEncounterSummary(encounter);
    }

    public async Task<CombatEncounterSummary> CancelEncounterAsync(CombatEncounterCancelRequest request, UserAccount actor)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        var encounter = await RequireEncounterAsync(request.EncounterId);
        if (string.Equals(encounter.Status, CombatRuntimeStatuses.Ended, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Ended encounter cannot be cancelled.");

        if (!string.Equals(encounter.Status, CombatRuntimeStatuses.Cancelled, StringComparison.OrdinalIgnoreCase))
        {
            var now = DateTime.UtcNow;
            encounter.Status = CombatRuntimeStatuses.Cancelled;
            encounter.EndedAtUtc = now;
            encounter.LastUpdatedAtUtc = now;
            await _encounters.UpsertAsync(encounter);
            await WriteLogAsync(encounter, CombatEventTypes.EncounterCancelled, SafeReason("Бой отменён.", request.Reason), actor, request.RequestId, string.Empty);
            await WriteReplayEventAsync(encounter, CombatEventTypes.EncounterCancelled, actor, request.RequestId, string.Empty);
        }

        _logger.Admin($"combat.v1.encounter.cancel.done encounterId={encounter.Id}");
        return ToEncounterSummary(encounter);
    }

    public async Task<CombatParticipantSummary> AddParticipantAsync(CombatParticipantAddRequest request, UserAccount actor)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        var encounter = await RequireEncounterAsync(request.EncounterId);
        EnsureMutableEncounter(encounter);
        Require(request.DisplayName, "displayName is required");

        var participantType = string.IsNullOrWhiteSpace(request.ParticipantType)
            ? (request.IsNpc ? CombatParticipantTypes.Npc : CombatParticipantTypes.PlayerCharacter)
            : request.ParticipantType.Trim();

        var existingParticipants = await _participants.ListByEncounterAsync(encounter.Id, 500);
        if (!request.IsNpc && !string.IsNullOrWhiteSpace(request.CharacterId)
            && existingParticipants.Any(x => x.IsActive && string.Equals(x.CharacterId, request.CharacterId, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("duplicate_character_participant");
        }

        var participant = new CombatParticipantState
        {
            Id = Guid.NewGuid().ToString("N"),
            EncounterId = encounter.Id,
            CharacterId = request.CharacterId ?? string.Empty,
            DisplayName = request.DisplayName.Trim(),
            ParticipantType = participantType,
            TeamId = request.TeamId ?? string.Empty,
            ControllerUserId = request.ControllerUserId ?? string.Empty,
            IsNpc = request.IsNpc,
            IsPlayerControlled = request.IsPlayerControlled,
            Initiative = request.Initiative,
            InitiativeTieBreaker = request.InitiativeTieBreaker,
            MaxStructure = Math.Max(0, request.MaxStructure),
            CurrentStructure = Math.Max(0, request.CurrentStructure),
            FrontProtection = Math.Max(0, request.FrontProtection),
            SideProtection = Math.Max(0, request.SideProtection),
            RearProtection = Math.Max(0, request.RearProtection),
            InitiativeGroup = string.Empty,
            Natural20BonusTurn = request.Initiative == 20,
            Natural20BonusTurnUsed = false,
            Natural1FirstTurnPenalty = request.Initiative == 1,
            Natural1PenaltyConsumed = false,
            IsActive = true,
            IsDefeated = false,
            IsHidden = request.IsHidden,
            HasActedThisRound = false,
            ActionPoints = CombatActionEconomyPolicy0219.HalfActionsPerTurn,
            MinorActionPoints = 0,
            ReactionCount = 0,
            ReactionLimit = CombatActionEconomyPolicy0219.ReactionsPerRound,
            PositionSummary = string.Empty,
            DistanceMeters = 0m,
            CoverState = string.Empty,
            VisibilityState = request.IsHidden ? CombatVisibilityIds.GmOnly : CombatVisibilityIds.Public,
            Tags = SafeList(request.Tags),
            Notes = request.Notes ?? string.Empty,
            SchemaVersion = 1
        };

        ValidateOrThrow(CombatRuntimeValidator.ValidateParticipant(participant));
        await _participants.UpsertAsync(participant);

        if (!encounter.ParticipantIds.Contains(participant.Id, StringComparer.OrdinalIgnoreCase))
            encounter.ParticipantIds.Add(participant.Id);
        encounter.InitiativeOrder.Add(new CombatInitiativeEntry
        {
            ParticipantId = participant.Id,
            Initiative = participant.Initiative,
            TieBreaker = participant.InitiativeTieBreaker,
            OrderIndex = encounter.InitiativeOrder.Count,
            IsDelayed = false,
            IsSkipped = false
        });
        encounter.LastUpdatedAtUtc = DateTime.UtcNow;
        ValidateOrThrow(CombatRuntimeValidator.ValidateInitiativeOrder(encounter, existingParticipants.Concat(new[] { participant })));
        await _encounters.UpsertAsync(encounter);
        await WriteLogAsync(encounter, CombatEventTypes.ParticipantAdded, $"Участник добавлен: {participant.DisplayName}", actor, request.RequestId, participant.Id);
        await WriteReplayEventAsync(encounter, CombatEventTypes.ParticipantAdded, actor, request.RequestId, participant.Id);

        _logger.Admin($"combat.v1.participant.add.done encounterId={encounter.Id} participantId={participant.Id}");
        return ToParticipantSummary(participant);
    }

    public async Task<CombatParticipantSummary> RemoveParticipantAsync(CombatParticipantRemoveRequest request, UserAccount actor)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        var encounter = await RequireEncounterAsync(request.EncounterId);
        EnsureMutableEncounter(encounter);
        var participant = await _participants.GetByIdAsync(request.ParticipantId);
        if (participant == null || !string.Equals(participant.EncounterId, encounter.Id, StringComparison.OrdinalIgnoreCase))
            throw new KeyNotFoundException("Combat participant not found.");

        participant.IsActive = false;
        if (!participant.Tags.Contains("removed", StringComparer.OrdinalIgnoreCase)) participant.Tags.Add("removed");
        participant.Notes = SafeReason(participant.Notes, request.Reason);
        await _participants.UpsertAsync(participant);

        encounter.ParticipantIds.RemoveAll(x => string.Equals(x, participant.Id, StringComparison.OrdinalIgnoreCase));
        foreach (var entry in encounter.InitiativeOrder.Where(x => string.Equals(x.ParticipantId, participant.Id, StringComparison.OrdinalIgnoreCase)))
        {
            entry.IsSkipped = true;
            entry.Notes = SafeReason(entry.Notes, request.Reason);
        }
        if (string.Equals(encounter.ActiveParticipantId, participant.Id, StringComparison.OrdinalIgnoreCase))
            encounter.ActiveParticipantId = string.Empty;
        encounter.LastUpdatedAtUtc = DateTime.UtcNow;
        await _encounters.UpsertAsync(encounter);
        await WriteLogAsync(encounter, CombatEventTypes.ParticipantRemoved, $"Участник удалён: {participant.DisplayName}", actor, request.RequestId, participant.Id);
        await WriteReplayEventAsync(encounter, CombatEventTypes.ParticipantRemoved, actor, request.RequestId, participant.Id);

        _logger.Admin($"combat.v1.participant.remove.done encounterId={encounter.Id} participantId={participant.Id}");
        return ToParticipantSummary(participant);
    }

    public async Task<CombatEncounterSnapshotResponse> GetSnapshotAsync(CombatEncounterSnapshotRequest request, UserAccount actor)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        var encounter = await RequireEncounterAsync(request.EncounterId);
        var response = new CombatEncounterSnapshotResponse
        {
            Encounter = ToEncounterSummary(encounter)
        };

        if (request.IncludeParticipants)
        {
            var participants = await _participants.ListByEncounterAsync(encounter.Id, 500);
            response.Participants.AddRange(participants.Select(ToParticipantSummary));
            response.Encounter.ParticipantCount = response.Participants.Count;
        }

        if (request.IncludeLogs)
        {
            var logs = await _logs.ListByEncounterAsync(encounter.Id, 100);
            response.Logs.AddRange(logs.Select(ToLogSummary));
        }

        if (request.IncludeReplayEvents)
        {
            var replayEvents = await _replayEvents.ListByEncounterAsync(encounter.Id, 100);
            response.ReplayEvents.AddRange(replayEvents.Select(ToReplayEventSummary));
        }

        _logger.Admin($"combat.v1.snapshot.done encounterId={encounter.Id}");
        return response;
    }

    private async Task<CombatEncounterState> RequireEncounterAsync(string encounterId)
    {
        Require(encounterId, "encounterId is required");
        var encounter = await _encounters.GetByIdAsync(encounterId);
        if (encounter == null) throw new KeyNotFoundException("Combat encounter not found.");
        return encounter;
    }

    private static void EnsureMutableEncounter(CombatEncounterState encounter)
    {
        if (string.Equals(encounter.Status, CombatRuntimeStatuses.Ended, StringComparison.OrdinalIgnoreCase)
            || string.Equals(encounter.Status, CombatRuntimeStatuses.Cancelled, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Encounter is not mutable.");
        }
    }

    private async Task WriteLogAsync(CombatEncounterState encounter, string eventType, string message, UserAccount actor, string requestId, string actorParticipantId)
    {
        await _logWriter.AppendLogAsync(new CombatLogWriteRequest
        {
            EncounterId = encounter.Id,
            CampaignId = encounter.CampaignId,
            SessionId = encounter.SessionId,
            RoundNumber = encounter.RoundNumber,
            TurnIndex = encounter.ActiveTurnIndex,
            ActorParticipantId = actorParticipantId ?? string.Empty,
            ActorUserId = actor?.Id ?? string.Empty,
            EventType = eventType,
            Message = message,
            SourcePayload = new Dictionary<string, object>
            {
                { "encounterId", encounter.Id },
                { "status", encounter.Status }
            },
            Visibility = CombatVisibilityIds.Public,
            RequestId = requestId ?? string.Empty
        });
    }

    private async Task WriteReplayEventAsync(CombatEncounterState encounter, string eventType, UserAccount actor, string requestId, string actorParticipantId)
    {
        await _logWriter.AppendReplayEventAsync(new CombatReplayWriteRequest
        {
            EncounterId = encounter.Id,
            EventType = eventType,
            RoundNumber = encounter.RoundNumber,
            TurnIndex = encounter.ActiveTurnIndex,
            ActorParticipantId = actorParticipantId ?? string.Empty,
            SourcePayload = new Dictionary<string, object>
            {
                { "encounterId", encounter.Id },
                { "actorUserId", actor?.Id ?? string.Empty }
            },
            Visibility = CombatVisibilityIds.Public,
            RequestId = requestId ?? string.Empty
        });
    }

    private static void ValidateOrThrow(CombatRuntimeValidationResult result)
    {
        if (result == null || result.IsValid) return;
        throw new ArgumentException(string.Join("; ", result.Errors.Select(x => $"{x.Code}: {x.Message}")));
    }

    private static void Require(string value, string message)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException(message);
    }

    private static List<string> SafeList(IEnumerable<string> values)
    {
        return (values ?? Enumerable.Empty<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string SafeReason(string baseMessage, string reason)
    {
        return string.IsNullOrWhiteSpace(reason) ? baseMessage ?? string.Empty : $"{baseMessage} Причина: {reason.Trim()}";
    }

    public static CombatEncounterSummary ToEncounterSummary(CombatEncounterState encounter)
    {
        return new CombatEncounterSummary
        {
            Id = encounter.Id,
            CampaignId = encounter.CampaignId,
            SessionId = encounter.SessionId,
            Name = encounter.Name,
            Status = encounter.Status,
            RuleSetId = encounter.RuleSetId,
            RoundNumber = encounter.RoundNumber,
            ActiveTurnIndex = encounter.ActiveTurnIndex,
            ActiveParticipantId = encounter.ActiveParticipantId,
            ParticipantCount = encounter.ParticipantIds?.Count ?? 0,
            StartedAtUtc = encounter.StartedAtUtc,
            EndedAtUtc = encounter.EndedAtUtc,
            Tags = SafeList(encounter.Tags)
        };
    }

    public static CombatParticipantSummary ToParticipantSummary(CombatParticipantState participant)
    {
        return new CombatParticipantSummary
        {
            Id = participant.Id,
            EncounterId = participant.EncounterId,
            CharacterId = participant.CharacterId,
            DisplayName = participant.DisplayName,
            ParticipantType = participant.ParticipantType,
            TeamId = participant.TeamId,
            ControllerUserId = participant.ControllerUserId,
            IsNpc = participant.IsNpc,
            IsPlayerControlled = participant.IsPlayerControlled,
            Initiative = participant.Initiative,
            Natural20BonusTurn = participant.Natural20BonusTurn || participant.Initiative == 20,
            Natural20BonusTurnUsed = participant.Natural20BonusTurnUsed,
            Natural1FirstTurnPenalty = participant.Natural1FirstTurnPenalty || participant.Initiative == 1,
            Natural1PenaltyConsumed = participant.Natural1PenaltyConsumed,
            Natural1PenaltyActive = (participant.Natural1FirstTurnPenalty || participant.Initiative == 1) && !participant.Natural1PenaltyConsumed,
            IsActive = participant.IsActive,
            IsDefeated = participant.IsDefeated,
            IsHidden = participant.IsHidden,
            HasActedThisRound = participant.HasActedThisRound,
            ActionPoints = participant.ActionPoints,
            MinorActionPoints = participant.MinorActionPoints,
            ReactionCount = participant.ReactionCount,
            ReactionLimit = participant.ReactionLimit,
            MaxHealth = participant.MaxHealth,
            CurrentHealth = participant.CurrentHealth,
            MaxStructure = participant.MaxStructure,
            CurrentStructure = participant.CurrentStructure,
            FrontProtection = participant.FrontProtection,
            SideProtection = participant.SideProtection,
            RearProtection = participant.RearProtection,
            DisabledModuleName = participant.DisabledModuleName,
            TemporaryHealth = participant.TemporaryHealth,
            MaxMorale = participant.MaxMorale,
            CurrentMorale = participant.CurrentMorale,
            LastDamageTaken = participant.LastDamageTaken,
            LastDamageType = participant.LastDamageType,
            DefeatedAtUtc = participant.DefeatedAtUtc,
            DefeatedReason = participant.DefeatedReason,
            ConditionCount = (participant.Conditions ?? new List<CombatConditionState>()).Count(x => string.Equals(x.Status, CombatConditionStatuses.Active, StringComparison.OrdinalIgnoreCase)),
            ActiveConditionIds = (participant.Conditions ?? new List<CombatConditionState>())
                .Where(x => string.Equals(x.Status, CombatConditionStatuses.Active, StringComparison.OrdinalIgnoreCase))
                .Select(x => x.ConditionDefinitionId ?? string.Empty)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            PositionSummary = participant.PositionSummary,
            SceneMapId = participant.SceneMapId,
            MapTokenId = participant.MapTokenId,
            MapTokenDisplayName = participant.MapTokenDisplayName,
            MapTokenVisibility = participant.MapTokenVisibility,
            MapLinkStatus = participant.MapLinkStatus,
            MapBadgeText = participant.MapBadgeText,
            MapBadgeColorKey = participant.MapBadgeColorKey,
            DistanceMeters = participant.DistanceMeters,
            CoverState = participant.CoverState,
            VisibilityState = participant.VisibilityState,
            Tags = SafeList(participant.Tags)
        };
    }

    public static CombatLogSummary ToLogSummary(CombatRuntimeLogEntry entry)
    {
        return new CombatLogSummary
        {
            Id = entry.Id,
            EncounterId = entry.EncounterId,
            RoundNumber = entry.RoundNumber,
            TurnIndex = entry.TurnIndex,
            ActorParticipantId = entry.ActorParticipantId,
            EventType = entry.EventType,
            Message = entry.Message,
            Visibility = entry.Visibility,
            CreatedAtUtc = entry.CreatedAtUtc,
            RequestId = entry.RequestId,
            PayloadSummary = entry.PayloadSummary ?? new Dictionary<string, object>()
        };
    }

    public static CombatReplayEventSummary ToReplayEventSummary(CombatReplayEvent replayEvent)
    {
        return new CombatReplayEventSummary
        {
            Id = replayEvent.Id,
            EncounterId = replayEvent.EncounterId,
            SequenceNumber = replayEvent.SequenceNumber,
            EventType = replayEvent.EventType,
            RoundNumber = replayEvent.RoundNumber,
            TurnIndex = replayEvent.TurnIndex,
            ActorParticipantId = replayEvent.ActorParticipantId,
            Visibility = replayEvent.Visibility,
            CreatedAtUtc = replayEvent.CreatedAtUtc,
            RequestId = replayEvent.RequestId,
            DataSummary = replayEvent.Data ?? new Dictionary<string, object>()
        };
    }
}
