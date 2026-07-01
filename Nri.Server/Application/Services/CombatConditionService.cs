using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nri.Server.Infrastructure.Mongo.Repositories;
using Nri.Server.Logging;
using Nri.Shared.Domain;

namespace Nri.Server.Application.Services;

public interface ICombatConditionService
{
    Task<CombatConditionResultResponse> ApplyConditionAsync(CombatConditionApplyRequest request, UserAccount actor);
    Task<CombatConditionResultResponse> RemoveConditionAsync(CombatConditionRemoveRequest request, UserAccount actor);
    Task<CombatConditionListResponse> ListConditionsAsync(CombatConditionListRequest request, UserAccount actor);
    Task<CombatConditionState> BuildConditionStateAsync(CombatConditionApplyRequest request, CombatEncounterState encounter, CombatParticipantState target, List<string> warnings);
    Task<CombatConditionDefinitionInfo> ResolveConditionDefinitionAsync(string conditionDefinitionId, string ruleSetId, List<string> warnings);
    CombatRuntimeValidationResult ValidateConditionApplication(CombatConditionApplyRequest request, CombatParticipantState target);
}

public sealed class CombatConditionDefinitionInfo
{
    public string DefinitionId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ConditionGroup { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string StackMode { get; set; } = "unique";
    public int MaxStacks { get; set; } = 1;
    public string DefaultDurationMode { get; set; } = "until_removed";
    public int DefaultDurationRounds { get; set; }
    public bool CanBeHiddenFromPlayer { get; set; } = true;
    public bool IsPositive { get; set; }
    public bool IsNegative { get; set; }
}

public sealed class CombatConditionService : ICombatConditionService
{
    private readonly ICombatEncounterRepository _encounters;
    private readonly ICombatParticipantRepository _participants;
    private readonly ICombatLogWriter _logWriter;
    private readonly ICombatSnapshotService _snapshotService;
    private readonly IDefinitionRepositoryV2? _definitions;
    private readonly IServerLogger _logger;

    public CombatConditionService(
        ICombatEncounterRepository encounters,
        ICombatParticipantRepository participants,
        ICombatLogWriter logWriter,
        ICombatSnapshotService snapshotService,
        IServerLogger logger,
        IDefinitionRepositoryV2? definitions = null)
    {
        _encounters = encounters;
        _participants = participants;
        _logWriter = logWriter;
        _snapshotService = snapshotService;
        _definitions = definitions;
        _logger = logger;
    }

    public async Task<CombatConditionResultResponse> ApplyConditionAsync(CombatConditionApplyRequest request, UserAccount actor)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        _logger.Admin($"combat.condition.apply.start encounterId={request.EncounterId} target={request.TargetParticipantId}");

        var encounter = await RequireEncounterAsync(request.EncounterId);
        var target = await RequireParticipantAsync(request.TargetParticipantId, encounter.Id);
        var validation = ValidateConditionApplication(request, target);
        ValidateOrThrow(validation);

        target.Conditions ??= new List<CombatConditionState>();
        var warnings = new List<string>();
        var incoming = await BuildConditionStateAsync(request, encounter, target, warnings);
        var activeExisting = target.Conditions.FirstOrDefault(x =>
            string.Equals(x.Status, CombatConditionStatuses.Active, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(x.ConditionDefinitionId)
            && string.Equals(x.ConditionDefinitionId, incoming.ConditionDefinitionId, StringComparison.OrdinalIgnoreCase));

        var applied = activeExisting == null
            ? AddNewCondition(target, incoming)
            : MergeExistingCondition(activeExisting, incoming, warnings);

        await _participants.UpsertAsync(target);
        await WriteConditionLogAsync(encounter, actor, request.RequestId, applied, CombatEventTypes.ConditionApplied, $"{target.DisplayName} gains condition {applied.DisplayName}.", BuildPayload(applied), applied.IsHiddenFromPlayer);

        _logger.Admin($"combat.condition.apply.done conditionId={applied.ConditionInstanceId}");
        return new CombatConditionResultResponse
        {
            EncounterId = encounter.Id,
            ParticipantId = target.Id,
            Condition = ToSummary(applied),
            Message = "condition applied",
            Warnings = warnings,
            Snapshot = await SnapshotAsync(encounter.Id, actor)
        };
    }

    public async Task<CombatConditionResultResponse> RemoveConditionAsync(CombatConditionRemoveRequest request, UserAccount actor)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        var encounter = await RequireEncounterAsync(request.EncounterId);
        var target = await RequireParticipantAsync(request.TargetParticipantId, encounter.Id);
        target.Conditions ??= new List<CombatConditionState>();

        var condition = target.Conditions.FirstOrDefault(x =>
            string.Equals(x.Status, CombatConditionStatuses.Active, StringComparison.OrdinalIgnoreCase)
            && ((!string.IsNullOrWhiteSpace(request.ConditionInstanceId) && string.Equals(x.ConditionInstanceId, request.ConditionInstanceId, StringComparison.OrdinalIgnoreCase))
                || (!string.IsNullOrWhiteSpace(request.ConditionDefinitionId) && string.Equals(x.ConditionDefinitionId, request.ConditionDefinitionId, StringComparison.OrdinalIgnoreCase))));

        if (condition == null) throw new KeyNotFoundException("combat condition not found");

        condition.Status = CombatConditionStatuses.Removed;
        condition.Notes = AppendNote(condition.Notes, request.Reason);
        await _participants.UpsertAsync(target);
        await WriteConditionLogAsync(encounter, actor, request.RequestId, condition, CombatEventTypes.ConditionRemoved, $"{target.DisplayName} loses condition {condition.DisplayName}.", BuildPayload(condition), condition.IsHiddenFromPlayer);

        _logger.Admin($"combat.condition.remove.done conditionId={condition.ConditionInstanceId}");
        return new CombatConditionResultResponse
        {
            EncounterId = encounter.Id,
            ParticipantId = target.Id,
            Condition = ToSummary(condition),
            Message = "condition removed",
            Warnings = new List<string>(),
            Snapshot = await SnapshotAsync(encounter.Id, actor)
        };
    }

    public async Task<CombatConditionListResponse> ListConditionsAsync(CombatConditionListRequest request, UserAccount actor)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        var encounter = await RequireEncounterAsync(request.EncounterId);
        var participant = await RequireParticipantAsync(request.ParticipantId, encounter.Id);
        var items = (participant.Conditions ?? new List<CombatConditionState>())
            .Where(x => request.IncludeRemoved || string.Equals(x.Status, CombatConditionStatuses.Active, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.AppliedAtUtc)
            .Select(ToSummary)
            .ToList();

        _logger.Debug($"combat.condition.list.done encounterId={encounter.Id} participantId={participant.Id} count={items.Count}");
        return new CombatConditionListResponse
        {
            EncounterId = encounter.Id,
            ParticipantId = participant.Id,
            Conditions = items
        };
    }

    public async Task<CombatConditionState> BuildConditionStateAsync(CombatConditionApplyRequest request, CombatEncounterState encounter, CombatParticipantState target, List<string> warnings)
    {
        var definition = await ResolveConditionDefinitionAsync(request.ConditionDefinitionId, encounter.RuleSetId, warnings);
        var durationMode = FirstNonEmpty(request.DurationMode, definition.DefaultDurationMode, "until_removed");
        var remainingRounds = string.Equals(durationMode, "rounds", StringComparison.OrdinalIgnoreCase)
            ? Math.Max(0, request.DurationRounds > 0 ? request.DurationRounds : definition.DefaultDurationRounds)
            : 0;

        return new CombatConditionState
        {
            ConditionInstanceId = Guid.NewGuid().ToString("N"),
            ConditionDefinitionId = request.ConditionDefinitionId?.Trim() ?? string.Empty,
            DisplayName = FirstNonEmpty(definition.DisplayName, request.ConditionDefinitionId, "condition"),
            SourceActionId = request.SourceActionId ?? string.Empty,
            SourceParticipantId = request.SourceParticipantId ?? string.Empty,
            TargetParticipantId = target.Id,
            ConditionGroup = definition.ConditionGroup,
            Severity = FirstNonEmpty(request.SeverityOverride, definition.Severity),
            StackMode = FirstNonEmpty(definition.StackMode, "unique"),
            StackCount = Math.Max(1, request.StackCount <= 0 ? 1 : request.StackCount),
            MaxStacks = Math.Max(1, definition.MaxStacks),
            DurationMode = durationMode,
            RemainingRounds = remainingRounds,
            AppliedRoundNumber = Math.Max(0, encounter.RoundNumber),
            AppliedTurnIndex = Math.Max(0, encounter.ActiveTurnIndex),
            IsHiddenFromPlayer = request.IsHiddenFromPlayer && definition.CanBeHiddenFromPlayer,
            IsPositive = definition.IsPositive,
            IsNegative = definition.IsNegative,
            Status = CombatConditionStatuses.Active,
            Notes = request.Notes ?? string.Empty,
            AppliedAtUtc = DateTime.UtcNow
        };
    }

    public Task<CombatConditionDefinitionInfo> ResolveConditionDefinitionAsync(string conditionDefinitionId, string ruleSetId, List<string> warnings)
    {
        var info = new CombatConditionDefinitionInfo
        {
            DefinitionId = conditionDefinitionId ?? string.Empty,
            DisplayName = conditionDefinitionId ?? string.Empty
        };

        if (!CombatFeatureGate.IsEnabled(nameof(CombatFeatureFlags.UseCombatConditionDefinitionLookup)))
        {
            AddWarning(warnings, "condition_definition_lookup_disabled");
            return Task.FromResult(info);
        }

        if (_definitions == null)
        {
            AddWarning(warnings, "condition_definition_repository_unavailable");
            return Task.FromResult(info);
        }

        var doc = _definitions.GetByIdAsync(DefinitionCategoryIds.Condition, conditionDefinitionId ?? string.Empty);
        if (doc == null)
        {
            AddWarning(warnings, "condition_definition_missing");
            return Task.FromResult(info);
        }

        if (doc.IsArchived) AddWarning(warnings, "condition_definition_archived");
        if (!string.IsNullOrWhiteSpace(ruleSetId) && doc.RuleSetIds != null && doc.RuleSetIds.Count > 0 && !doc.RuleSetIds.Any(x => string.Equals(x, ruleSetId, StringComparison.OrdinalIgnoreCase)))
            AddWarning(warnings, "condition_definition_ruleset_mismatch");

        var reader = new DefinitionExtraDataReader(doc.ExtraData);
        info.DisplayName = FirstNonEmpty(reader.GetString("displayNameRu", string.Empty), doc.Name, doc.Id);
        info.ConditionGroup = reader.GetString("conditionGroup", string.Empty);
        info.Severity = reader.GetString("severity", string.Empty);
        info.StackMode = FirstNonEmpty(reader.GetString("stackMode", string.Empty), "unique");
        info.MaxStacks = Math.Max(1, reader.GetInt("maxStacks", 1));
        info.DefaultDurationMode = FirstNonEmpty(reader.GetString("defaultDurationMode", string.Empty), "until_removed");
        info.DefaultDurationRounds = Math.Max(0, reader.GetInt("defaultDurationRounds", 0));
        info.CanBeHiddenFromPlayer = reader.GetBool("canBeHiddenFromPlayer", false);
        info.IsPositive = reader.GetBool("isPositive", false);
        info.IsNegative = reader.GetBool("isNegative", false);
        foreach (var warning in reader.Warnings) AddWarning(warnings, warning);
        foreach (var error in reader.Errors) AddWarning(warnings, error);
        return Task.FromResult(info);
    }

    public CombatRuntimeValidationResult ValidateConditionApplication(CombatConditionApplyRequest request, CombatParticipantState target)
    {
        var result = new CombatRuntimeValidationResult();
        if (string.IsNullOrWhiteSpace(request.EncounterId)) result.Errors.Add(Issue("encounter_id_missing", "error", "EncounterId is required.", string.Empty, "condition"));
        if (string.IsNullOrWhiteSpace(request.TargetParticipantId)) result.Errors.Add(Issue("target_participant_missing", "error", "TargetParticipantId is required.", string.Empty, "condition"));
        if (string.IsNullOrWhiteSpace(request.ConditionDefinitionId)) result.Errors.Add(Issue("condition_definition_missing", "error", "ConditionDefinitionId is required.", target?.Id ?? string.Empty, "condition"));
        if (request.StackCount < 0) result.Warnings.Add(Issue("condition_stack_defaulted", "warning", "StackCount below zero will be defaulted to one.", target?.Id ?? string.Empty, "condition"));
        if (request.DurationRounds < 0) result.Errors.Add(Issue("condition_duration_negative", "error", "DurationRounds must be greater than or equal to zero.", target?.Id ?? string.Empty, "condition"));
        result.IsValid = result.Errors.Count == 0;
        return result;
    }

    private CombatConditionState AddNewCondition(CombatParticipantState target, CombatConditionState incoming)
    {
        target.Conditions.Add(incoming);
        return incoming;
    }

    private CombatConditionState MergeExistingCondition(CombatConditionState existing, CombatConditionState incoming, List<string> warnings)
    {
        var stackMode = NormalizeStackMode(incoming);
        if (string.Equals(stackMode, "stacking", StringComparison.OrdinalIgnoreCase))
        {
            existing.StackCount = Math.Min(Math.Max(1, incoming.MaxStacks), Math.Max(1, existing.StackCount) + Math.Max(1, incoming.StackCount));
            RefreshDuration(existing, incoming);
            return existing;
        }

        if (string.Equals(stackMode, "refresh_duration", StringComparison.OrdinalIgnoreCase))
        {
            RefreshDuration(existing, incoming);
            return existing;
        }

        if (string.Equals(stackMode, "strongest_only", StringComparison.OrdinalIgnoreCase))
        {
            if (SeverityRank(incoming.Severity) > SeverityRank(existing.Severity))
            {
                existing.Severity = incoming.Severity;
                existing.DisplayName = incoming.DisplayName;
                existing.ConditionGroup = incoming.ConditionGroup;
                existing.MaxStacks = incoming.MaxStacks;
            }
            RefreshDuration(existing, incoming);
            return existing;
        }

        AddWarning(warnings, "condition_already_active");
        return existing;
    }

    private static void RefreshDuration(CombatConditionState existing, CombatConditionState incoming)
    {
        existing.DurationMode = incoming.DurationMode;
        existing.RemainingRounds = incoming.RemainingRounds;
        existing.AppliedAtUtc = DateTime.UtcNow;
        existing.AppliedRoundNumber = incoming.AppliedRoundNumber;
        existing.AppliedTurnIndex = incoming.AppliedTurnIndex;
        existing.Notes = AppendNote(existing.Notes, incoming.Notes);
    }

    private static string NormalizeStackMode(CombatConditionState incoming)
    {
        if (string.Equals(incoming.StackMode, "stacking", StringComparison.OrdinalIgnoreCase)) return "stacking";
        if (string.Equals(incoming.StackMode, "refresh_duration", StringComparison.OrdinalIgnoreCase)) return "refresh_duration";
        if (string.Equals(incoming.StackMode, "strongest_only", StringComparison.OrdinalIgnoreCase)) return "strongest_only";
        if (string.Equals(incoming.StackMode, "unique", StringComparison.OrdinalIgnoreCase)) return "unique";
        if (string.Equals(incoming.StackMode, "none", StringComparison.OrdinalIgnoreCase)) return "unique";
        if (incoming.MaxStacks > 1) return "stacking";
        return "unique";
    }

    private async Task WriteConditionLogAsync(CombatEncounterState encounter, UserAccount actor, string requestId, CombatConditionState condition, string eventType, string message, Dictionary<string, object> payload, bool hidden)
    {
        var visibility = hidden ? CombatVisibilityIds.GmOnly : CombatVisibilityIds.Public;
        await _logWriter.AppendLogAndReplayAsync(
            new CombatLogWriteRequest
            {
                EncounterId = encounter.Id,
                CampaignId = encounter.CampaignId,
                SessionId = encounter.SessionId,
                RoundNumber = encounter.RoundNumber,
                TurnIndex = encounter.ActiveTurnIndex,
                ActorParticipantId = condition.SourceParticipantId ?? string.Empty,
                ActorUserId = actor?.Id ?? string.Empty,
                EventType = eventType,
                Message = message,
                SourcePayload = payload,
                Visibility = visibility,
                RequestId = requestId ?? string.Empty
            },
            new CombatReplayWriteRequest
            {
                EncounterId = encounter.Id,
                EventType = eventType,
                RoundNumber = encounter.RoundNumber,
                TurnIndex = encounter.ActiveTurnIndex,
                ActorParticipantId = condition.SourceParticipantId ?? string.Empty,
                SourcePayload = payload,
                Visibility = visibility,
                RequestId = requestId ?? string.Empty
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
            LimitActions = 100,
            LimitLogs = 100
        }, actor);
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

    public static CombatConditionSummary ToSummary(CombatConditionState condition)
    {
        return new CombatConditionSummary
        {
            ConditionInstanceId = condition.ConditionInstanceId,
            ConditionDefinitionId = condition.ConditionDefinitionId,
            DisplayName = condition.DisplayName,
            ConditionGroup = condition.ConditionGroup,
            Severity = condition.Severity,
            StackCount = condition.StackCount,
            MaxStacks = condition.MaxStacks,
            DurationMode = condition.DurationMode,
            RemainingRounds = condition.RemainingRounds,
            IsHiddenFromPlayer = condition.IsHiddenFromPlayer,
            IsPositive = condition.IsPositive,
            IsNegative = condition.IsNegative,
            Status = condition.Status,
            AppliedRoundNumber = condition.AppliedRoundNumber,
            AppliedTurnIndex = condition.AppliedTurnIndex,
            AppliedAtUtc = condition.AppliedAtUtc
        };
    }

    private static Dictionary<string, object> BuildPayload(CombatConditionState condition)
    {
        return new Dictionary<string, object>
        {
            { "conditionInstanceId", condition.ConditionInstanceId },
            { "conditionDefinitionId", condition.ConditionDefinitionId },
            { "targetParticipantId", condition.TargetParticipantId },
            { "sourceParticipantId", condition.SourceParticipantId },
            { "conditionGroup", condition.ConditionGroup },
            { "severity", condition.Severity },
            { "stackCount", condition.StackCount },
            { "durationMode", condition.DurationMode },
            { "remainingRounds", condition.RemainingRounds },
            { "status", condition.Status }
        };
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

    private static string FirstNonEmpty(params string[] values)
    {
        return (values ?? Array.Empty<string>()).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim() ?? string.Empty;
    }

    private static void AddWarning(List<string> warnings, string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return;
        warnings ??= new List<string>();
        if (!warnings.Contains(code, StringComparer.OrdinalIgnoreCase)) warnings.Add(code);
    }

    private static int SeverityRank(string severity)
    {
        if (string.Equals(severity, "critical", StringComparison.OrdinalIgnoreCase)) return 5;
        if (string.Equals(severity, "severe", StringComparison.OrdinalIgnoreCase)) return 4;
        if (string.Equals(severity, "major", StringComparison.OrdinalIgnoreCase)) return 3;
        if (string.Equals(severity, "medium", StringComparison.OrdinalIgnoreCase) || string.Equals(severity, "moderate", StringComparison.OrdinalIgnoreCase)) return 2;
        if (string.Equals(severity, "minor", StringComparison.OrdinalIgnoreCase)) return 1;
        return 0;
    }

    private static string AppendNote(string current, string next)
    {
        if (string.IsNullOrWhiteSpace(next)) return current ?? string.Empty;
        if (string.IsNullOrWhiteSpace(current)) return next.Trim();
        return $"{current.Trim()} {next.Trim()}";
    }
}
