using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Driver;
using Nri.Server.Infrastructure;
using Nri.Server.Infrastructure.Mongo.Repositories;
using Nri.Server.Logging;
using Nri.Shared.Domain;

namespace Nri.Server.Application.Services;

public interface ICombatPayloadSummaryBuilder
{
    Dictionary<string, object> BuildLogPayloadSummary(string eventType, IDictionary<string, object> sourcePayload);
    Dictionary<string, object> BuildReplayDataSummary(string eventType, IDictionary<string, object> sourcePayload);
    object SanitizeValue(string key, object value);
    bool IsSensitiveKey(string key);
}

public sealed class CombatPayloadSummaryBuilder : ICombatPayloadSummaryBuilder
{
    private const int MaxStringLength = 500;
    private const int MaxCollectionItems = 20;
    private readonly IServerLogger _logger;

    public CombatPayloadSummaryBuilder(IServerLogger logger)
    {
        _logger = logger;
    }

    public Dictionary<string, object> BuildLogPayloadSummary(string eventType, IDictionary<string, object> sourcePayload)
    {
        return BuildSummary(eventType, sourcePayload);
    }

    public Dictionary<string, object> BuildReplayDataSummary(string eventType, IDictionary<string, object> sourcePayload)
    {
        return BuildSummary(eventType, sourcePayload);
    }

    public object SanitizeValue(string key, object value)
    {
        if (value == null) return string.Empty;
        if (IsSensitiveKey(key))
        {
            _logger.Debug($"combat.payload.sanitized key={key}");
            return "[redacted]";
        }

        if (value is string text)
        {
            return text.Length <= MaxStringLength ? text : text.Substring(0, MaxStringLength);
        }

        if (value is bool || value is int || value is long || value is decimal || value is double || value is float || value is DateTime)
        {
            return value;
        }

        if (value is IDictionary dictionary)
        {
            var safe = new Dictionary<string, object>();
            var count = 0;
            foreach (DictionaryEntry entry in dictionary)
            {
                if (count++ >= MaxCollectionItems) break;
                var childKey = Convert.ToString(entry.Key) ?? string.Empty;
                if (string.IsNullOrWhiteSpace(childKey) || IsSensitiveKey(childKey))
                {
                    _logger.Debug($"combat.payload.sanitized key={childKey}");
                    continue;
                }

                safe[childKey] = SanitizeValue(childKey, entry.Value ?? string.Empty);
            }

            return safe;
        }

        if (value is IEnumerable enumerable && !(value is string))
        {
            var items = new List<object>();
            var count = 0;
            foreach (var item in enumerable)
            {
                if (count++ >= MaxCollectionItems) break;
                items.Add(SanitizeValue(key, item ?? string.Empty));
            }

            return items;
        }

        var fallback = Convert.ToString(value) ?? string.Empty;
        return fallback.Length <= MaxStringLength ? fallback : fallback.Substring(0, MaxStringLength);
    }

    public bool IsSensitiveKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return false;
        var normalized = key.Trim();
        return Contains(normalized, "password")
            || Contains(normalized, "token")
            || Contains(normalized, "serverOnlyData")
            || Contains(normalized, "gmDescription")
            || Contains(normalized, "privateNotes")
            || Contains(normalized, "fullInventory")
            || Contains(normalized, "characterDetails")
            || Contains(normalized, "rawPayload")
            || Contains(normalized, "secret")
            || Contains(normalized, "hiddenData");
    }

    private Dictionary<string, object> BuildSummary(string eventType, IDictionary<string, object> sourcePayload)
    {
        var summary = new Dictionary<string, object>
        {
            { "eventType", eventType ?? string.Empty }
        };

        foreach (var pair in sourcePayload ?? new Dictionary<string, object>())
        {
            if (string.IsNullOrWhiteSpace(pair.Key)) continue;
            if (IsSensitiveKey(pair.Key))
            {
                _logger.Debug($"combat.payload.sanitized key={pair.Key}");
                continue;
            }

            summary[pair.Key] = SanitizeValue(pair.Key, pair.Value);
        }

        return summary;
    }

    private static bool Contains(string value, string search)
    {
        return value.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}

public interface ICombatReplaySequenceService
{
    Task<long> GetNextSequenceNumberAsync(string encounterId);
    Task<long> ReserveNextSequenceNumberAsync(string encounterId, string requestId);
}

public sealed class CombatReplaySequenceService : ICombatReplaySequenceService
{
    private const string Prefix = "combat-replay-seq:";
    private readonly MongoContext _mongo;
    private readonly IServerLogger _logger;

    public CombatReplaySequenceService(MongoContext mongo, IServerLogger logger)
    {
        _mongo = mongo;
        _logger = logger;
    }

    public async Task<long> GetNextSequenceNumberAsync(string encounterId)
    {
        if (string.IsNullOrWhiteSpace(encounterId)) throw new ArgumentException("encounterId is required");
        var current = await _mongo.SyncCounters.Find(Builders<SyncCounter>.Filter.Eq(x => x.CounterKey, Prefix + encounterId)).FirstOrDefaultAsync();
        return (current?.Value ?? 0) + 1;
    }

    public async Task<long> ReserveNextSequenceNumberAsync(string encounterId, string requestId)
    {
        if (string.IsNullOrWhiteSpace(encounterId)) throw new ArgumentException("encounterId is required");
        var now = DateTime.UtcNow;
        var key = Prefix + encounterId;
        var update = Builders<SyncCounter>.Update
            .SetOnInsert(x => x.Id, Guid.NewGuid().ToString("N"))
            .SetOnInsert(x => x.CounterKey, key)
            .SetOnInsert(x => x.CreatedUtc, now)
            .Inc(x => x.Value, 1)
            .Set(x => x.UpdatedUtc, now);
        var options = new FindOneAndUpdateOptions<SyncCounter>
        {
            IsUpsert = true,
            ReturnDocument = ReturnDocument.After
        };
        var item = await _mongo.SyncCounters.FindOneAndUpdateAsync(Builders<SyncCounter>.Filter.Eq(x => x.CounterKey, key), update, options);
        var next = item?.Value ?? 1;
        _logger.Debug($"combat.replay.sequence.reserve encounterId={encounterId} sequence={next} requestId={requestId}");
        return next;
    }
}

public interface ICombatLogWriter
{
    Task<CombatRuntimeLogEntry?> AppendLogAsync(CombatLogWriteRequest request);
    Task<CombatReplayEvent?> AppendReplayEventAsync(CombatReplayWriteRequest request);
    Task<CombatRuntimeLogEntry?> AppendLogAndReplayAsync(CombatLogWriteRequest logRequest, CombatReplayWriteRequest replayRequest);
}

public sealed class CombatLogWriter : ICombatLogWriter
{
    private readonly ICombatLogRepository _logs;
    private readonly ICombatReplayEventRepository _replayEvents;
    private readonly ICombatPayloadSummaryBuilder _summaryBuilder;
    private readonly ICombatReplaySequenceService _sequenceService;
    private readonly IServerLogger _logger;

    public CombatLogWriter(
        ICombatLogRepository logs,
        ICombatReplayEventRepository replayEvents,
        ICombatPayloadSummaryBuilder summaryBuilder,
        ICombatReplaySequenceService sequenceService,
        IServerLogger logger)
    {
        _logs = logs;
        _replayEvents = replayEvents;
        _summaryBuilder = summaryBuilder;
        _sequenceService = sequenceService;
        _logger = logger;
    }

    public async Task<CombatRuntimeLogEntry?> AppendLogAsync(CombatLogWriteRequest request)
    {
        try
        {
            _logger.Debug($"combat.log.append.start encounterId={request.EncounterId} eventType={request.EventType}");
            var entry = new CombatRuntimeLogEntry
            {
                EncounterId = request.EncounterId ?? string.Empty,
                CampaignId = request.CampaignId ?? string.Empty,
                SessionId = request.SessionId ?? string.Empty,
                RoundNumber = Math.Max(0, request.RoundNumber),
                TurnIndex = Math.Max(0, request.TurnIndex),
                ActorParticipantId = request.ActorParticipantId ?? string.Empty,
                ActorUserId = request.ActorUserId ?? string.Empty,
                EventType = NormalizeEventType(request.EventType),
                Message = Truncate(request.Message ?? string.Empty),
                PayloadSummary = _summaryBuilder.BuildLogPayloadSummary(request.EventType, request.SourcePayload),
                Visibility = NormalizeVisibility(request.Visibility),
                RequestId = request.RequestId ?? string.Empty,
                CreatedAtUtc = DateTime.UtcNow,
                SchemaVersion = 1
            };

            ValidateOrThrow(CombatRuntimeValidator.ValidateLogEntry(entry));
            var saved = await _logs.AppendAsync(entry);
            _logger.Debug($"combat.log.append.done logId={saved.Id}");
            return saved;
        }
        catch (Exception ex)
        {
            _logger.Admin($"combat.log.append.error encounterId={request?.EncounterId} eventType={request?.EventType} message={ex.Message}");
            return null;
        }
    }

    public async Task<CombatReplayEvent?> AppendReplayEventAsync(CombatReplayWriteRequest request)
    {
        if (!CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatReplayLog))) return null;
        try
        {
            var sequence = await _sequenceService.ReserveNextSequenceNumberAsync(request.EncounterId, request.RequestId);
            var replayEvent = new CombatReplayEvent
            {
                EncounterId = request.EncounterId ?? string.Empty,
                SequenceNumber = sequence,
                EventType = NormalizeEventType(request.EventType),
                RoundNumber = Math.Max(0, request.RoundNumber),
                TurnIndex = Math.Max(0, request.TurnIndex),
                ActorParticipantId = request.ActorParticipantId ?? string.Empty,
                Data = _summaryBuilder.BuildReplayDataSummary(request.EventType, request.SourcePayload),
                Visibility = NormalizeVisibility(request.Visibility),
                RequestId = request.RequestId ?? string.Empty,
                CreatedAtUtc = DateTime.UtcNow
            };

            var saved = await _replayEvents.AppendAsync(replayEvent);
            _logger.Debug($"combat.replay.append.done replayId={saved.Id} sequence={saved.SequenceNumber}");
            return saved;
        }
        catch (Exception ex)
        {
            _logger.Admin($"combat.replay.append.error encounterId={request?.EncounterId} eventType={request?.EventType} message={ex.Message}");
            return null;
        }
    }

    public async Task<CombatRuntimeLogEntry?> AppendLogAndReplayAsync(CombatLogWriteRequest logRequest, CombatReplayWriteRequest replayRequest)
    {
        var log = await AppendLogAsync(logRequest);
        await AppendReplayEventAsync(replayRequest);
        return log;
    }

    private static string NormalizeEventType(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? CombatEventTypes.GmNote : value.Trim();
    }

    private static string NormalizeVisibility(string value)
    {
        if (string.Equals(value, CombatVisibilityIds.GmOnly, StringComparison.OrdinalIgnoreCase)) return CombatVisibilityIds.GmOnly;
        if (string.Equals(value, CombatVisibilityIds.ParticipantOnly, StringComparison.OrdinalIgnoreCase)) return CombatVisibilityIds.ParticipantOnly;
        if (string.Equals(value, CombatVisibilityIds.HiddenUntilRevealed, StringComparison.OrdinalIgnoreCase)) return CombatVisibilityIds.HiddenUntilRevealed;
        return CombatVisibilityIds.Public;
    }

    private static string Truncate(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= 500) return value ?? string.Empty;
        return value.Substring(0, 500);
    }

    private static void ValidateOrThrow(CombatRuntimeValidationResult result)
    {
        if (result == null || result.IsValid) return;
        throw new ArgumentException(string.Join("; ", result.Errors.Select(x => $"{x.Code}: {x.Message}")));
    }
}

public interface ICombatLogReadService
{
    Task<CombatLogListResponse> ListLogsAsync(CombatLogListRequest request, UserAccount actor);
    Task<CombatReplayListResponse> ListReplayEventsAsync(CombatReplayListRequest request, UserAccount actor);
}

public sealed class CombatLogReadService : ICombatLogReadService
{
    private readonly ICombatLogRepository _logs;
    private readonly ICombatReplayEventRepository _replayEvents;
    private readonly IServerLogger _logger;

    public CombatLogReadService(ICombatLogRepository logs, ICombatReplayEventRepository replayEvents, IServerLogger logger)
    {
        _logs = logs;
        _replayEvents = replayEvents;
        _logger = logger;
    }

    public async Task<CombatLogListResponse> ListLogsAsync(CombatLogListRequest request, UserAccount actor)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (string.IsNullOrWhiteSpace(request.EncounterId)) throw new ArgumentException("encounterId is required");
        var limit = Math.Max(1, Math.Min(request.Limit <= 0 ? 100 : request.Limit, 500));
        var offset = Math.Max(0, request.Offset);
        var all = await _logs.ListByEncounterAsync(request.EncounterId, 500);
        var filtered = all
            .Where(x => Matches(x, request))
            .OrderBy(x => x.CreatedAtUtc)
            .ThenBy(x => x.RoundNumber)
            .ThenBy(x => x.TurnIndex)
            .ToList();
        var items = filtered.Skip(offset).Take(limit).Select(ToLogSummary).ToList();
        _logger.Debug($"combat.log.read.done encounterId={request.EncounterId} count={items.Count}");
        return new CombatLogListResponse
        {
            EncounterId = request.EncounterId,
            Items = items,
            Total = filtered.Count,
            Limit = limit,
            Offset = offset,
            HasMore = offset + items.Count < filtered.Count
        };
    }

    public async Task<CombatReplayListResponse> ListReplayEventsAsync(CombatReplayListRequest request, UserAccount actor)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (string.IsNullOrWhiteSpace(request.EncounterId)) throw new ArgumentException("encounterId is required");
        var limit = Math.Max(1, Math.Min(request.Limit <= 0 ? 200 : request.Limit, 1000));
        var all = await _replayEvents.ListByEncounterAsync(request.EncounterId, 1000);
        var filtered = all
            .Where(x => request.FromSequence <= 0 || x.SequenceNumber >= request.FromSequence)
            .Where(x => request.ToSequence <= 0 || x.SequenceNumber <= request.ToSequence)
            .OrderBy(x => x.SequenceNumber)
            .ToList();
        var items = filtered.Take(limit).Select(ToReplaySummary).ToList();
        _logger.Debug($"combat.replay.read.done encounterId={request.EncounterId} count={items.Count}");
        return new CombatReplayListResponse
        {
            EncounterId = request.EncounterId,
            Items = items,
            FromSequence = items.FirstOrDefault()?.SequenceNumber ?? 0,
            ToSequence = items.LastOrDefault()?.SequenceNumber ?? 0,
            HasMore = filtered.Count > items.Count
        };
    }

    private static bool Matches(CombatRuntimeLogEntry entry, CombatLogListRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Visibility)
            && !string.Equals(entry.Visibility, request.Visibility, StringComparison.OrdinalIgnoreCase)) return false;
        if (!string.IsNullOrWhiteSpace(request.EventType)
            && !string.Equals(entry.EventType, request.EventType, StringComparison.OrdinalIgnoreCase)) return false;
        if (request.FromRound > 0 && entry.RoundNumber < request.FromRound) return false;
        if (request.ToRound > 0 && entry.RoundNumber > request.ToRound) return false;
        return true;
    }

    private static CombatLogSummary ToLogSummary(CombatRuntimeLogEntry entry)
    {
        var summary = CombatEncounterManagementService.ToLogSummary(entry);
        summary.PayloadSummary = SafeDictionary(entry.PayloadSummary);
        return summary;
    }

    private static CombatReplayEventSummary ToReplaySummary(CombatReplayEvent replayEvent)
    {
        var summary = CombatEncounterManagementService.ToReplayEventSummary(replayEvent);
        summary.DataSummary = SafeDictionary(replayEvent.Data);
        return summary;
    }

    private static Dictionary<string, object> SafeDictionary(Dictionary<string, object> source)
    {
        return (source ?? new Dictionary<string, object>())
            .Where(x => !string.IsNullOrWhiteSpace(x.Key))
            .Take(50)
            .ToDictionary(x => x.Key, x => x.Value ?? string.Empty, StringComparer.OrdinalIgnoreCase);
    }
}
