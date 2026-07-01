using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Driver;
using Nri.Server.Application.Services;
using Nri.Shared.Contracts;
using Nri.Shared.Domain;
using Nri.Shared.Utilities;

namespace Nri.Server.Application;

public partial class ServiceHub
{
    public ResponseEnvelope CombatV1PlayerSnapshot(CommandContext context)
    {
        if (!CombatV1PlayerSnapshotReadEnabled())
        {
            _logger.Admin($"combat.player.disabled command={context.Request.Command}");
            return Error("combat v1 player snapshot endpoint disabled", ResponseStatus.Forbidden, ErrorCode.Forbidden);
        }

        var actor = GetCurrentAccount(context);
        var request = ParseCombatPlayerSnapshotRequest(context.Request.Payload);
        request.RequestId = FirstNonEmpty(request.RequestId, context.Request.RequestId ?? string.Empty);
        _logger.Admin($"combat.player.snapshot.start encounterId={request.EncounterId}");

        var response = BuildCombatPlayerSnapshot(request, actor);
        _logger.Admin($"combat.player.snapshot.done encounterId={request.EncounterId} participant={response.MyParticipant.ParticipantId}");
        return Ok("Combat player snapshot loaded.", CombatPlayerSnapshotPayload(response));
    }

    public ResponseEnvelope CombatV1PlayerFeed(CommandContext context)
    {
        if (!CombatV1PlayerFeedReadEnabled())
        {
            _logger.Admin($"combat.player.disabled command={context.Request.Command}");
            return Error("combat v1 player feed endpoint disabled", ResponseStatus.Forbidden, ErrorCode.Forbidden);
        }

        var actor = GetCurrentAccount(context);
        var request = ParseCombatPlayerFeedRequest(context.Request.Payload);
        request.RequestId = FirstNonEmpty(request.RequestId, context.Request.RequestId ?? string.Empty);

        var encounter = _repositories.CombatEncounters.GetByIdAsync(request.EncounterId).GetAwaiter().GetResult()
            ?? throw new KeyNotFoundException("Combat encounter not found.");
        EnsurePlayerMaySeeEncounter(actor, encounter, request.CharacterId, request.ParticipantId);

        var logs = _repositories.CombatRuntimeLogs.ListByEncounterAsync(request.EncounterId, Math.Max(1, Math.Min(request.Limit, 200))).GetAwaiter().GetResult()
            .Where(IsPublicCombatLog)
            .Where(x => request.SinceUtc == null || x.CreatedAtUtc >= request.SinceUtc.Value)
            .OrderBy(x => x.CreatedAtUtc)
            .Take(Math.Max(1, Math.Min(request.Limit, 100)))
            .Select(CombatPlayerLogItemFromLog)
            .ToList();

        _logger.Admin($"combat.player.feed.done encounterId={request.EncounterId} count={logs.Count}");
        return Ok("Combat player feed loaded.", new Dictionary<string, object>
        {
            { "encounterId", request.EncounterId },
            { "items", logs.Select(CombatPlayerLogItemPayload).Cast<object>().ToArray() }
        });
    }

    private CombatPlayerSnapshotResponse BuildCombatPlayerSnapshot(CombatPlayerSnapshotRequest request, UserAccount actor)
    {
        var encounter = _repositories.CombatEncounters.GetByIdAsync(request.EncounterId).GetAwaiter().GetResult()
            ?? throw new KeyNotFoundException("Combat encounter not found.");
        var participants = _repositories.CombatParticipants.ListByEncounterAsync(request.EncounterId, 500).GetAwaiter().GetResult().ToList();
        var myParticipant = ResolvePlayerParticipant(actor, request, participants);
        if (myParticipant == null)
        {
            _logger.Admin($"combat.player.forbidden encounterId={request.EncounterId}");
            throw new UnauthorizedAccessException("Combat participant unavailable.");
        }

        var activeParticipant = participants.FirstOrDefault(x => string.Equals(x.Id, encounter.ActiveParticipantId, StringComparison.Ordinal));
        var visibleParticipants = request.IncludePublicParticipants
            ? participants.Where(x => !x.IsHidden || string.Equals(x.Id, myParticipant.Id, StringComparison.Ordinal))
                .OrderByDescending(x => string.Equals(x.Id, encounter.ActiveParticipantId, StringComparison.Ordinal))
                .ThenBy(x => x.TeamId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Select(x => CombatPlayerParticipantFromState(x, encounter, IsCombatPlayerKnownConditionsEnabled()))
                .ToList()
            : new List<CombatPlayerParticipantSummary>();

        var response = new CombatPlayerSnapshotResponse
        {
            Encounter = new CombatPlayerEncounterSummary
            {
                EncounterId = encounter.Id,
                Name = encounter.Name,
                Status = encounter.Status,
                RoundNumber = encounter.RoundNumber,
                ActiveTurnIndex = encounter.ActiveTurnIndex,
                IsActive = string.Equals(encounter.Status, CombatRuntimeStatuses.Active, StringComparison.OrdinalIgnoreCase)
            },
            MyParticipant = CombatPlayerParticipantFromState(myParticipant, encounter, IsCombatPlayerKnownConditionsEnabled()),
            Participants = visibleParticipants,
            CurrentTurn = new CombatPlayerTurnSummary
            {
                RoundNumber = encounter.RoundNumber,
                TurnIndex = encounter.ActiveTurnIndex,
                ActiveParticipantId = encounter.ActiveParticipantId,
                ActiveParticipantName = activeParticipant?.DisplayName ?? string.Empty,
                IsMyTurn = string.Equals(encounter.ActiveParticipantId, myParticipant.Id, StringComparison.Ordinal)
            },
            BuiltAtUtc = DateTime.UtcNow
        };

        if (request.IncludePublicLog)
        {
            response.PublicLog = _repositories.CombatRuntimeLogs.ListByEncounterAsync(request.EncounterId, Math.Max(1, Math.Min(request.LimitLog, 200))).GetAwaiter().GetResult()
                .Where(IsPublicCombatLog)
                .OrderByDescending(x => x.CreatedAtUtc)
                .Take(Math.Max(1, Math.Min(request.LimitLog, 100)))
                .OrderBy(x => x.CreatedAtUtc)
                .Select(CombatPlayerLogItemFromLog)
                .ToList();
        }

        return response;
    }

    private CombatParticipantState? ResolvePlayerParticipant(UserAccount actor, CombatPlayerSnapshotRequest request, List<CombatParticipantState> participants)
    {
        var isAdmin = IsCombatAdmin(actor);
        var characterId = FirstNonEmpty(request.CharacterId, GetActiveCharacterId(actor));

        CombatParticipantState? participant = null;
        if (!string.IsNullOrWhiteSpace(request.ParticipantId))
        {
            participant = participants.FirstOrDefault(x => string.Equals(x.Id, request.ParticipantId, StringComparison.Ordinal));
        }
        if (participant == null && !string.IsNullOrWhiteSpace(characterId))
        {
            participant = participants.FirstOrDefault(x => string.Equals(x.CharacterId, characterId, StringComparison.Ordinal));
        }
        if (participant == null) return null;
        if (isAdmin) return participant;

        var ownsCharacter = !string.IsNullOrWhiteSpace(participant.CharacterId)
            && CharacterBelongsToActor(participant.CharacterId, actor.Id);
        var controlsParticipant = string.Equals(participant.ControllerUserId, actor.Id, StringComparison.Ordinal);
        return ownsCharacter || controlsParticipant ? participant : null;
    }

    private void EnsurePlayerMaySeeEncounter(UserAccount actor, CombatEncounterState encounter, string characterId, string participantId)
    {
        if (IsCombatAdmin(actor)) return;

        var participants = _repositories.CombatParticipants.ListByEncounterAsync(encounter.Id, 500).GetAwaiter().GetResult().ToList();
        var request = new CombatPlayerSnapshotRequest
        {
            EncounterId = encounter.Id,
            CharacterId = characterId,
            ParticipantId = participantId
        };
        if (ResolvePlayerParticipant(actor, request, participants) == null)
        {
            _logger.Admin($"combat.player.forbidden encounterId={encounter.Id}");
            throw new UnauthorizedAccessException("Combat encounter unavailable.");
        }
    }

    private bool CharacterBelongsToActor(string characterId, string actorUserId)
    {
        var character = _repositories.Characters.GetById(characterId);
        return character != null
            && !character.Deleted
            && string.Equals(character.OwnerUserId, actorUserId, StringComparison.Ordinal);
    }

    private string GetActiveCharacterId(UserAccount actor)
    {
        var presence = _repositories.Presence.Find(Builders<SessionUserState>.Filter.Eq(x => x.UserId, actor.Id)).FirstOrDefault();
        return presence?.ActiveCharacterId ?? string.Empty;
    }

    private static bool IsCombatAdmin(UserAccount actor)
    {
        return actor.Roles.Contains(UserRole.Admin) || actor.Roles.Contains(UserRole.SuperAdmin);
    }

    private static bool IsPublicCombatLog(CombatRuntimeLogEntry entry)
    {
        return string.IsNullOrWhiteSpace(entry.Visibility)
            || string.Equals(entry.Visibility, CombatVisibilityIds.Public, StringComparison.OrdinalIgnoreCase);
    }

    private static CombatPlayerParticipantSummary CombatPlayerParticipantFromState(CombatParticipantState participant, CombatEncounterState encounter, bool includeConditions)
    {
        return new CombatPlayerParticipantSummary
        {
            ParticipantId = participant.Id,
            CharacterId = participant.CharacterId,
            DisplayName = participant.DisplayName,
            TeamId = participant.TeamId,
            ParticipantType = participant.ParticipantType,
            IsCurrentTurn = string.Equals(encounter.ActiveParticipantId, participant.Id, StringComparison.Ordinal),
            IsActive = participant.IsActive,
            IsDefeated = participant.IsDefeated,
            CurrentHealth = participant.CurrentHealth,
            MaxHealth = participant.MaxHealth,
            TemporaryHealth = participant.TemporaryHealth,
            CurrentMorale = participant.CurrentMorale,
            MaxMorale = participant.MaxMorale,
            VisibilityState = participant.VisibilityState,
            KnownConditions = includeConditions
                ? participant.Conditions
                    .Where(x => string.Equals(x.Status, CombatConditionStatuses.Active, StringComparison.OrdinalIgnoreCase) && !x.IsHiddenFromPlayer)
                    .Select(x => new CombatPlayerConditionSummary
                    {
                        ConditionDefinitionId = x.ConditionDefinitionId,
                        DisplayName = FirstNonEmpty(x.DisplayName, x.ConditionDefinitionId),
                        Severity = x.Severity,
                        StackCount = x.StackCount,
                        RemainingRounds = x.RemainingRounds,
                        IsPositive = x.IsPositive,
                        IsNegative = x.IsNegative
                    })
                    .ToList()
                : new List<CombatPlayerConditionSummary>()
        };
    }

    private static CombatPlayerLogItem CombatPlayerLogItemFromLog(CombatRuntimeLogEntry entry)
    {
        return new CombatPlayerLogItem
        {
            CreatedAtUtc = entry.CreatedAtUtc,
            RoundNumber = entry.RoundNumber,
            TurnIndex = entry.TurnIndex,
            EventType = entry.EventType,
            Message = entry.Message
        };
    }

    private static CombatPlayerSnapshotRequest ParseCombatPlayerSnapshotRequest(IDictionary<string, object> payload)
    {
        return new CombatPlayerSnapshotRequest
        {
            EncounterId = PayloadReader.GetString(payload, "encounterId") ?? string.Empty,
            CharacterId = PayloadReader.GetString(payload, "characterId") ?? string.Empty,
            ParticipantId = PayloadReader.GetString(payload, "participantId") ?? string.Empty,
            IncludePublicParticipants = !payload.ContainsKey("includePublicParticipants") || PayloadReader.GetBool(payload, "includePublicParticipants"),
            IncludePublicLog = !payload.ContainsKey("includePublicLog") || PayloadReader.GetBool(payload, "includePublicLog"),
            LimitLog = Math.Max(1, Math.Min(PayloadReader.GetInt(payload, "limitLog") ?? 100, 100)),
            RequestId = PayloadReader.GetString(payload, "requestId") ?? string.Empty
        };
    }

    private static CombatPlayerFeedRequest ParseCombatPlayerFeedRequest(IDictionary<string, object> payload)
    {
        var sinceRaw = PayloadReader.GetString(payload, "sinceUtc") ?? string.Empty;
        DateTime? sinceUtc = DateTime.TryParse(sinceRaw, out var parsed) ? parsed.ToUniversalTime() : null;
        return new CombatPlayerFeedRequest
        {
            EncounterId = PayloadReader.GetString(payload, "encounterId") ?? string.Empty,
            CharacterId = PayloadReader.GetString(payload, "characterId") ?? string.Empty,
            ParticipantId = PayloadReader.GetString(payload, "participantId") ?? string.Empty,
            SinceUtc = sinceUtc,
            Limit = Math.Max(1, Math.Min(PayloadReader.GetInt(payload, "limit") ?? 100, 100)),
            RequestId = PayloadReader.GetString(payload, "requestId") ?? string.Empty
        };
    }

    private static Dictionary<string, object> CombatPlayerSnapshotPayload(CombatPlayerSnapshotResponse response)
    {
        return new Dictionary<string, object>
        {
            { "encounter", CombatPlayerEncounterPayload(response.Encounter) },
            { "myParticipant", CombatPlayerParticipantPayload(response.MyParticipant) },
            { "participants", response.Participants.Select(CombatPlayerParticipantPayload).Cast<object>().ToArray() },
            { "currentTurn", CombatPlayerTurnPayload(response.CurrentTurn) },
            { "publicLog", response.PublicLog.Select(CombatPlayerLogItemPayload).Cast<object>().ToArray() },
            { "warnings", response.Warnings.Cast<object>().ToArray() },
            { "builtAtUtc", response.BuiltAtUtc }
        };
    }

    private static Dictionary<string, object> CombatPlayerEncounterPayload(CombatPlayerEncounterSummary encounter)
    {
        return new Dictionary<string, object>
        {
            { "encounterId", encounter.EncounterId },
            { "name", encounter.Name },
            { "status", encounter.Status },
            { "roundNumber", encounter.RoundNumber },
            { "activeTurnIndex", encounter.ActiveTurnIndex },
            { "isActive", encounter.IsActive }
        };
    }

    private static Dictionary<string, object> CombatPlayerParticipantPayload(CombatPlayerParticipantSummary participant)
    {
        return new Dictionary<string, object>
        {
            { "participantId", participant.ParticipantId },
            { "characterId", participant.CharacterId },
            { "displayName", participant.DisplayName },
            { "teamId", participant.TeamId },
            { "participantType", participant.ParticipantType },
            { "isCurrentTurn", participant.IsCurrentTurn },
            { "isActive", participant.IsActive },
            { "isDefeated", participant.IsDefeated },
            { "currentHealth", participant.CurrentHealth ?? 0 },
            { "maxHealth", participant.MaxHealth ?? 0 },
            { "temporaryHealth", participant.TemporaryHealth ?? 0 },
            { "currentMorale", participant.CurrentMorale ?? 0 },
            { "maxMorale", participant.MaxMorale ?? 0 },
            { "knownConditions", participant.KnownConditions.Select(CombatPlayerConditionPayload).Cast<object>().ToArray() },
            { "visibilityState", participant.VisibilityState }
        };
    }

    private static Dictionary<string, object> CombatPlayerConditionPayload(CombatPlayerConditionSummary condition)
    {
        return new Dictionary<string, object>
        {
            { "conditionDefinitionId", condition.ConditionDefinitionId },
            { "displayName", condition.DisplayName },
            { "severity", condition.Severity },
            { "stackCount", condition.StackCount },
            { "remainingRounds", condition.RemainingRounds },
            { "isPositive", condition.IsPositive },
            { "isNegative", condition.IsNegative }
        };
    }

    private static Dictionary<string, object> CombatPlayerTurnPayload(CombatPlayerTurnSummary turn)
    {
        return new Dictionary<string, object>
        {
            { "roundNumber", turn.RoundNumber },
            { "turnIndex", turn.TurnIndex },
            { "activeParticipantId", turn.ActiveParticipantId },
            { "activeParticipantName", turn.ActiveParticipantName },
            { "isMyTurn", turn.IsMyTurn }
        };
    }

    private static Dictionary<string, object> CombatPlayerLogItemPayload(CombatPlayerLogItem item)
    {
        return new Dictionary<string, object>
        {
            { "createdAtUtc", item.CreatedAtUtc },
            { "roundNumber", item.RoundNumber },
            { "turnIndex", item.TurnIndex },
            { "eventType", item.EventType },
            { "message", item.Message }
        };
    }

    private static bool CombatV1PlayerSnapshotReadEnabled()
    {
        return CombatV1PlayerReadEnabled()
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatPlayerSnapshotEndpoint));
    }

    private static bool CombatV1PlayerFeedReadEnabled()
    {
        return CombatV1PlayerReadEnabled()
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatPlayerFeedEndpoint));
    }

    private static bool CombatV1PlayerReadEnabled()
    {
        return CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatSystemV1))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatEncounterRuntime))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatReadEndpoints))
            && CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatPlayerReadEndpoints));
    }

    private static bool IsCombatPlayerKnownConditionsEnabled()
    {
        return CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatPlayerKnownConditions));
    }
}
