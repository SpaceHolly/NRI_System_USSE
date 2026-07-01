using System.Collections.Generic;
using System.Threading.Tasks;
using Nri.Server.Infrastructure.Mongo.Repositories;
using Nri.Shared.Domain;

namespace Nri.Server.Application.Services;

public interface ICombatRuntimePersistenceService
{
    Task<CombatEncounterState?> GetEncounterAsync(string id);
    Task<IReadOnlyCollection<CombatEncounterState>> ListEncountersByCampaignAsync(string campaignId, int limit = 100);
    Task<IReadOnlyCollection<CombatParticipantState>> ListParticipantsAsync(string encounterId, int limit = 200);
    Task<IReadOnlyCollection<CombatTurnState>> ListTurnsAsync(string encounterId, int limit = 200);
    Task<IReadOnlyCollection<CombatRoundRuntimeState>> ListRoundsAsync(string encounterId, int limit = 200);
    Task<IReadOnlyCollection<CombatActionState>> ListActionsAsync(string encounterId, int limit = 200);
    Task<IReadOnlyCollection<CombatRuntimeLogEntry>> ListLogsAsync(string encounterId, int limit = 200);
    Task<IReadOnlyCollection<CombatReplayEvent>> ListReplayEventsAsync(string encounterId, int limit = 200);
}

public sealed class CombatRuntimePersistenceService : ICombatRuntimePersistenceService
{
    private readonly ICombatEncounterRepository _encounters;
    private readonly ICombatParticipantRepository _participants;
    private readonly ICombatTurnRepository _turns;
    private readonly ICombatRoundRepository _rounds;
    private readonly ICombatActionRepository _actions;
    private readonly ICombatLogRepository _logs;
    private readonly ICombatReplayEventRepository _replayEvents;

    public CombatRuntimePersistenceService(
        ICombatEncounterRepository encounters,
        ICombatParticipantRepository participants,
        ICombatTurnRepository turns,
        ICombatRoundRepository rounds,
        ICombatActionRepository actions,
        ICombatLogRepository logs,
        ICombatReplayEventRepository replayEvents)
    {
        _encounters = encounters;
        _participants = participants;
        _turns = turns;
        _rounds = rounds;
        _actions = actions;
        _logs = logs;
        _replayEvents = replayEvents;
    }

    public Task<CombatEncounterState?> GetEncounterAsync(string id)
    {
        return _encounters.GetByIdAsync(id);
    }

    public Task<IReadOnlyCollection<CombatEncounterState>> ListEncountersByCampaignAsync(string campaignId, int limit = 100)
    {
        return _encounters.ListByCampaignAsync(campaignId, limit);
    }

    public Task<IReadOnlyCollection<CombatParticipantState>> ListParticipantsAsync(string encounterId, int limit = 200)
    {
        return _participants.ListByEncounterAsync(encounterId, limit);
    }

    public Task<IReadOnlyCollection<CombatTurnState>> ListTurnsAsync(string encounterId, int limit = 200)
    {
        return _turns.ListByEncounterAsync(encounterId, limit);
    }

    public Task<IReadOnlyCollection<CombatRoundRuntimeState>> ListRoundsAsync(string encounterId, int limit = 200)
    {
        return _rounds.ListByEncounterAsync(encounterId, limit);
    }

    public Task<IReadOnlyCollection<CombatActionState>> ListActionsAsync(string encounterId, int limit = 200)
    {
        return _actions.ListByEncounterAsync(encounterId, limit);
    }

    public Task<IReadOnlyCollection<CombatRuntimeLogEntry>> ListLogsAsync(string encounterId, int limit = 200)
    {
        return _logs.ListByEncounterAsync(encounterId, limit);
    }

    public Task<IReadOnlyCollection<CombatReplayEvent>> ListReplayEventsAsync(string encounterId, int limit = 200)
    {
        return _replayEvents.ListByEncounterAsync(encounterId, limit);
    }
}
