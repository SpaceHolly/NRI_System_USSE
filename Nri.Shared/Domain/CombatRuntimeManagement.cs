using System;
using System.Collections.Generic;

namespace Nri.Shared.Domain;

public sealed class CombatEncounterCreateRequest
{
    public string CampaignId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<string> TeamIds { get; set; } = new List<string>();
    public List<string> Tags { get; set; } = new List<string>();
    public string Notes { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
}

public sealed class CombatEncounterCreateResponse
{
    public string EncounterId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public int RoundNumber { get; set; }
    public int ActiveTurnIndex { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class CombatEncounterEndRequest
{
    public string EncounterId { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
}

public sealed class CombatEncounterCancelRequest
{
    public string EncounterId { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
}

public sealed class CombatParticipantAddRequest
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
    public List<string> Tags { get; set; } = new List<string>();
    public string Notes { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
}

public sealed class CombatParticipantRemoveRequest
{
    public string EncounterId { get; set; } = string.Empty;
    public string ParticipantId { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
}

public sealed class CombatEncounterSnapshotRequest
{
    public string EncounterId { get; set; } = string.Empty;
    public bool IncludeParticipants { get; set; } = true;
    public bool IncludeLogs { get; set; }
    public bool IncludeReplayEvents { get; set; }
    public string RequestId { get; set; } = string.Empty;
}

public sealed class CombatEncounterSnapshotResponse
{
    public CombatEncounterSummary Encounter { get; set; } = new CombatEncounterSummary();
    public List<CombatParticipantSummary> Participants { get; set; } = new List<CombatParticipantSummary>();
    public List<CombatLogSummary> Logs { get; set; } = new List<CombatLogSummary>();
    public List<CombatReplayEventSummary> ReplayEvents { get; set; } = new List<CombatReplayEventSummary>();
    public List<string> Warnings { get; set; } = new List<string>();
}

public sealed class CombatEncounterSummary
{
    public string Id { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = string.Empty;
    public int RoundNumber { get; set; }
    public int ActiveTurnIndex { get; set; }
    public string ActiveParticipantId { get; set; } = string.Empty;
    public int ParticipantCount { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime? EndedAtUtc { get; set; }
    public List<string> Tags { get; set; } = new List<string>();
}

public sealed class CombatParticipantSummary
{
    public string Id { get; set; } = string.Empty;
    public string EncounterId { get; set; } = string.Empty;
    public string CharacterId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ParticipantType { get; set; } = string.Empty;
    public string TeamId { get; set; } = string.Empty;
    public string ControllerUserId { get; set; } = string.Empty;
    public bool IsNpc { get; set; }
    public bool IsPlayerControlled { get; set; }
    public int Initiative { get; set; }
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
    public int ConditionCount { get; set; }
    public List<string> ActiveConditionIds { get; set; } = new List<string>();
    public string PositionSummary { get; set; } = string.Empty;
    public decimal DistanceMeters { get; set; }
    public string CoverState { get; set; } = string.Empty;
    public string VisibilityState { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new List<string>();
}

public sealed class CombatLogSummary
{
    public string Id { get; set; } = string.Empty;
    public string EncounterId { get; set; } = string.Empty;
    public int RoundNumber { get; set; }
    public int TurnIndex { get; set; }
    public string ActorParticipantId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Visibility { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public string RequestId { get; set; } = string.Empty;
    public Dictionary<string, object> PayloadSummary { get; set; } = new Dictionary<string, object>();
}

public sealed class CombatReplayEventSummary
{
    public string Id { get; set; } = string.Empty;
    public string EncounterId { get; set; } = string.Empty;
    public long SequenceNumber { get; set; }
    public string EventType { get; set; } = string.Empty;
    public int RoundNumber { get; set; }
    public int TurnIndex { get; set; }
    public string ActorParticipantId { get; set; } = string.Empty;
    public string Visibility { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public string RequestId { get; set; } = string.Empty;
    public Dictionary<string, object> DataSummary { get; set; } = new Dictionary<string, object>();
}

public sealed class CombatLogWriteRequest
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
    public Dictionary<string, object> SourcePayload { get; set; } = new Dictionary<string, object>();
    public string Visibility { get; set; } = CombatVisibilityIds.Public;
    public string RequestId { get; set; } = string.Empty;
}

public sealed class CombatReplayWriteRequest
{
    public string EncounterId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public int RoundNumber { get; set; }
    public int TurnIndex { get; set; }
    public string ActorParticipantId { get; set; } = string.Empty;
    public Dictionary<string, object> SourcePayload { get; set; } = new Dictionary<string, object>();
    public string Visibility { get; set; } = CombatVisibilityIds.Public;
    public string RequestId { get; set; } = string.Empty;
}

public sealed class CombatLogListRequest
{
    public string EncounterId { get; set; } = string.Empty;
    public string Visibility { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public int FromRound { get; set; }
    public int ToRound { get; set; }
    public int Limit { get; set; } = 100;
    public int Offset { get; set; }
    public string RequestId { get; set; } = string.Empty;
}

public sealed class CombatReplayListRequest
{
    public string EncounterId { get; set; } = string.Empty;
    public long FromSequence { get; set; }
    public long ToSequence { get; set; }
    public int Limit { get; set; } = 200;
    public string RequestId { get; set; } = string.Empty;
}

public sealed class CombatLogListResponse
{
    public string EncounterId { get; set; } = string.Empty;
    public List<CombatLogSummary> Items { get; set; } = new List<CombatLogSummary>();
    public int Total { get; set; }
    public int Limit { get; set; }
    public int Offset { get; set; }
    public bool HasMore { get; set; }
}

public sealed class CombatReplayListResponse
{
    public string EncounterId { get; set; } = string.Empty;
    public List<CombatReplayEventSummary> Items { get; set; } = new List<CombatReplayEventSummary>();
    public long FromSequence { get; set; }
    public long ToSequence { get; set; }
    public bool HasMore { get; set; }
}

public sealed class CombatPlayerSnapshotRequest
{
    public string EncounterId { get; set; } = string.Empty;
    public string CharacterId { get; set; } = string.Empty;
    public string ParticipantId { get; set; } = string.Empty;
    public bool IncludePublicParticipants { get; set; } = true;
    public bool IncludePublicLog { get; set; } = true;
    public int LimitLog { get; set; } = 100;
    public string RequestId { get; set; } = string.Empty;
}

public sealed class CombatPlayerFeedRequest
{
    public string EncounterId { get; set; } = string.Empty;
    public string CharacterId { get; set; } = string.Empty;
    public string ParticipantId { get; set; } = string.Empty;
    public DateTime? SinceUtc { get; set; }
    public int Limit { get; set; } = 100;
    public string RequestId { get; set; } = string.Empty;
}

public sealed class CombatPlayerSnapshotResponse
{
    public CombatPlayerEncounterSummary Encounter { get; set; } = new CombatPlayerEncounterSummary();
    public CombatPlayerParticipantSummary MyParticipant { get; set; } = new CombatPlayerParticipantSummary();
    public List<CombatPlayerParticipantSummary> Participants { get; set; } = new List<CombatPlayerParticipantSummary>();
    public CombatPlayerTurnSummary CurrentTurn { get; set; } = new CombatPlayerTurnSummary();
    public List<CombatPlayerLogItem> PublicLog { get; set; } = new List<CombatPlayerLogItem>();
    public List<string> Warnings { get; set; } = new List<string>();
    public DateTime BuiltAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class CombatPlayerEncounterSummary
{
    public string EncounterId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int RoundNumber { get; set; }
    public int ActiveTurnIndex { get; set; }
    public bool IsActive { get; set; }
}

public sealed class CombatPlayerParticipantSummary
{
    public string ParticipantId { get; set; } = string.Empty;
    public string CharacterId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string TeamId { get; set; } = string.Empty;
    public string ParticipantType { get; set; } = string.Empty;
    public bool IsCurrentTurn { get; set; }
    public bool IsActive { get; set; }
    public bool IsDefeated { get; set; }
    public int? CurrentHealth { get; set; }
    public int? MaxHealth { get; set; }
    public int? TemporaryHealth { get; set; }
    public int? CurrentMorale { get; set; }
    public int? MaxMorale { get; set; }
    public List<CombatPlayerConditionSummary> KnownConditions { get; set; } = new List<CombatPlayerConditionSummary>();
    public string VisibilityState { get; set; } = string.Empty;
}

public sealed class CombatPlayerConditionSummary
{
    public string ConditionDefinitionId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public int StackCount { get; set; }
    public int RemainingRounds { get; set; }
    public bool IsPositive { get; set; }
    public bool IsNegative { get; set; }
}

public sealed class CombatPlayerTurnSummary
{
    public int RoundNumber { get; set; }
    public int TurnIndex { get; set; }
    public string ActiveParticipantId { get; set; } = string.Empty;
    public string ActiveParticipantName { get; set; } = string.Empty;
    public bool IsMyTurn { get; set; }
}

public sealed class CombatPlayerLogItem
{
    public DateTime CreatedAtUtc { get; set; }
    public int RoundNumber { get; set; }
    public int TurnIndex { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public sealed class CombatInitiativeSortRequest
{
    public string EncounterId { get; set; } = string.Empty;
    public string SortMode { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
}

public sealed class CombatRoundStartRequest
{
    public string EncounterId { get; set; } = string.Empty;
    public int RoundNumber { get; set; }
    public string RequestId { get; set; } = string.Empty;
}

public sealed class CombatTurnStartRequest
{
    public string EncounterId { get; set; } = string.Empty;
    public string ParticipantId { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
}

public sealed class CombatTurnEndRequest
{
    public string EncounterId { get; set; } = string.Empty;
    public string ParticipantId { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
}

public sealed class CombatNextTurnRequest
{
    public string EncounterId { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
}

public sealed class CombatNextRoundRequest
{
    public string EncounterId { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
}

public sealed class CombatSkipTurnRequest
{
    public string EncounterId { get; set; } = string.Empty;
    public string ParticipantId { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
}

public sealed class CombatDelayTurnRequest
{
    public string EncounterId { get; set; } = string.Empty;
    public string ParticipantId { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
}

public sealed class CombatTurnEngineResponse
{
    public string EncounterId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int RoundNumber { get; set; }
    public int ActiveTurnIndex { get; set; }
    public string ActiveParticipantId { get; set; } = string.Empty;
    public string PreviousParticipantId { get; set; } = string.Empty;
    public bool Changed { get; set; }
    public string Message { get; set; } = string.Empty;
    public CombatEncounterSnapshotResponse Snapshot { get; set; } = new CombatEncounterSnapshotResponse();
    public List<string> Warnings { get; set; } = new List<string>();
}

public sealed class CombatFullSnapshotRequest
{
    public string EncounterId { get; set; } = string.Empty;
    public bool IncludeParticipants { get; set; } = true;
    public bool IncludeTurns { get; set; } = true;
    public bool IncludeRounds { get; set; } = true;
    public bool IncludeActions { get; set; } = true;
    public bool IncludeLogs { get; set; } = true;
    public bool IncludeReplayEvents { get; set; }
    public bool IncludeDiagnostics { get; set; }
    public int LimitLogs { get; set; } = 100;
    public int LimitActions { get; set; } = 100;
    public string RequestId { get; set; } = string.Empty;
}

public sealed class CombatFullSnapshotResponse
{
    public CombatEncounterSummary Encounter { get; set; } = new CombatEncounterSummary();
    public List<CombatParticipantSummary> Participants { get; set; } = new List<CombatParticipantSummary>();
    public CombatRoundSummary CurrentRound { get; set; } = new CombatRoundSummary();
    public CombatTurnSummary CurrentTurn { get; set; } = new CombatTurnSummary();
    public List<CombatInitiativeEntrySummary> InitiativeOrder { get; set; } = new List<CombatInitiativeEntrySummary>();
    public List<CombatActionSummary> RecentActions { get; set; } = new List<CombatActionSummary>();
    public List<CombatLogSummary> RecentLogs { get; set; } = new List<CombatLogSummary>();
    public List<CombatReplayEventSummary> RecentReplayEvents { get; set; } = new List<CombatReplayEventSummary>();
    public CombatDiagnosticsSummary Diagnostics { get; set; } = new CombatDiagnosticsSummary();
    public List<string> Warnings { get; set; } = new List<string>();
    public DateTime BuiltAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class CombatRoundSummary
{
    public string EncounterId { get; set; } = string.Empty;
    public int RoundNumber { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime? EndedAtUtc { get; set; }
    public int TurnCount { get; set; }
    public List<string> CompletedParticipantIds { get; set; } = new List<string>();
}

public sealed class CombatTurnSummary
{
    public string EncounterId { get; set; } = string.Empty;
    public int RoundNumber { get; set; }
    public int TurnIndex { get; set; }
    public string ParticipantId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime StartedAtUtc { get; set; }
    public DateTime? EndedAtUtc { get; set; }
    public bool Skipped { get; set; }
    public string SkipReason { get; set; } = string.Empty;
    public int ActionPointsStarted { get; set; }
    public int ActionPointsSpent { get; set; }
    public int MinorActionPointsStarted { get; set; }
    public int MinorActionPointsSpent { get; set; }
    public int ReactionsUsed { get; set; }
}

public sealed class CombatInitiativeEntrySummary
{
    public string ParticipantId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int Initiative { get; set; }
    public int TieBreaker { get; set; }
    public int OrderIndex { get; set; }
    public bool IsDelayed { get; set; }
    public bool IsSkipped { get; set; }
    public bool IsActive { get; set; }
    public bool IsDefeated { get; set; }
}

public sealed class CombatActionSummary
{
    public string Id { get; set; } = string.Empty;
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
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public string RequestId { get; set; } = string.Empty;
}

public sealed class CombatDiagnosticsRequest
{
    public string EncounterId { get; set; } = string.Empty;
    public bool IncludeEncounterValidation { get; set; } = true;
    public bool IncludeParticipantValidation { get; set; } = true;
    public bool IncludeInitiativeValidation { get; set; } = true;
    public bool IncludeTurnValidation { get; set; } = true;
    public bool IncludeActionValidation { get; set; } = true;
    public bool StrictMode { get; set; }
    public string RequestId { get; set; } = string.Empty;
}

public sealed class CombatDiagnosticsResponse
{
    public string EncounterId { get; set; } = string.Empty;
    public bool IsValid { get; set; }
    public List<CombatDiagnosticsSection> Sections { get; set; } = new List<CombatDiagnosticsSection>();
    public List<CombatValidationIssue> Errors { get; set; } = new List<CombatValidationIssue>();
    public List<CombatValidationIssue> Warnings { get; set; } = new List<CombatValidationIssue>();
    public CombatDiagnosticsSummary Summary { get; set; } = new CombatDiagnosticsSummary();
    public DateTime CheckedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class CombatDiagnosticsSection
{
    public string Section { get; set; } = string.Empty;
    public bool IsValid { get; set; } = true;
    public List<CombatValidationIssue> Errors { get; set; } = new List<CombatValidationIssue>();
    public List<CombatValidationIssue> Warnings { get; set; } = new List<CombatValidationIssue>();
}

public sealed class CombatDiagnosticsSummary
{
    public int ParticipantCount { get; set; }
    public int ActiveParticipantCount { get; set; }
    public int DefeatedParticipantCount { get; set; }
    public int InitiativeEntryCount { get; set; }
    public int RoundNumber { get; set; }
    public int ActiveTurnIndex { get; set; }
    public int ActionCount { get; set; }
    public int LogCount { get; set; }
    public int ErrorCount { get; set; }
    public int WarningCount { get; set; }
}

public sealed class CombatActionDeclareRequest
{
    public string EncounterId { get; set; } = string.Empty;
    public string ActorParticipantId { get; set; } = string.Empty;
    public string ActionType { get; set; } = string.Empty;
    public string ActionName { get; set; } = string.Empty;
    public List<string> TargetParticipantIds { get; set; } = new List<string>();
    public string TargetLocationSummary { get; set; } = string.Empty;
    public int ActionPointCost { get; set; }
    public int MinorActionPointCost { get; set; }
    public int ReactionCost { get; set; }
    public Dictionary<string, object> PayloadSummary { get; set; } = new Dictionary<string, object>();
    public string Notes { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
}

public sealed class CombatActionCompleteRequest
{
    public string EncounterId { get; set; } = string.Empty;
    public string ActionId { get; set; } = string.Empty;
    public string ResultStatus { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
}

public sealed class CombatActionCancelRequest
{
    public string EncounterId { get; set; } = string.Empty;
    public string ActionId { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
}

public sealed class CombatActionSpendRequest
{
    public string EncounterId { get; set; } = string.Empty;
    public string ParticipantId { get; set; } = string.Empty;
    public int ActionPointCost { get; set; }
    public int MinorActionPointCost { get; set; }
    public int ReactionCost { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
}

public sealed class CombatActionEconomyResponse
{
    public string EncounterId { get; set; } = string.Empty;
    public string ActionId { get; set; } = string.Empty;
    public string ActorParticipantId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int ActionPointsRemaining { get; set; }
    public int MinorActionPointsRemaining { get; set; }
    public int ReactionsUsed { get; set; }
    public int ReactionLimit { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string> Warnings { get; set; } = new List<string>();
    public CombatFullSnapshotResponse Snapshot { get; set; } = new CombatFullSnapshotResponse();
}

public sealed class CombatAttackDeclareRequest
{
    public string EncounterId { get; set; } = string.Empty;
    public string ActorParticipantId { get; set; } = string.Empty;
    public string TargetParticipantId { get; set; } = string.Empty;
    public string WeaponDefinitionId { get; set; } = string.Empty;
    public string AttackSkillId { get; set; } = string.Empty;
    public string AttackAttributeId { get; set; } = string.Empty;
    public int AttackBonus { get; set; }
    public int WeaponAccuracyBonus { get; set; }
    public int? TargetDefenseOverride { get; set; }
    public decimal? DistanceMeters { get; set; }
    public int CoverModifier { get; set; }
    public int SituationalModifier { get; set; }
    public bool UseFateEngine { get; set; }
    public bool SpendActionPoint { get; set; }
    public string RequestId { get; set; } = string.Empty;
}

public sealed class CombatAttackResultResponse
{
    public string EncounterId { get; set; } = string.Empty;
    public string ActionId { get; set; } = string.Empty;
    public string ActorParticipantId { get; set; } = string.Empty;
    public string TargetParticipantId { get; set; } = string.Empty;
    public string WeaponDefinitionId { get; set; } = string.Empty;
    public int Roll { get; set; }
    public int NaturalRoll { get; set; }
    public int AttackTotal { get; set; }
    public int TargetDefense { get; set; }
    public string HitResult { get; set; } = CombatHitResultIds.Miss;
    public bool IsHit { get; set; }
    public bool IsCritical { get; set; }
    public bool IsFumble { get; set; }
    public bool IsNaturalCritical { get; set; }
    public bool IsNaturalFumble { get; set; }
    public CombatAttackModifierBreakdown Modifiers { get; set; } = new CombatAttackModifierBreakdown();
    public CombatFateHookResult Fate { get; set; } = new CombatFateHookResult();
    public string Message { get; set; } = string.Empty;
    public List<string> Warnings { get; set; } = new List<string>();
    public CombatFullSnapshotResponse Snapshot { get; set; } = new CombatFullSnapshotResponse();
}

public sealed class CombatAttackModifierBreakdown
{
    public int AttackBonus { get; set; }
    public int WeaponAccuracyBonus { get; set; }
    public int SkillBonus { get; set; }
    public int AttributeBonus { get; set; }
    public int DistanceModifier { get; set; }
    public int CoverModifier { get; set; }
    public int SituationalModifier { get; set; }
    public int FateModifier { get; set; }
    public int TotalModifier { get; set; }
}

public sealed class CombatAttackRollComputation
{
    public int NaturalRoll { get; set; }
    public int Roll { get; set; }
    public int AttackTotal { get; set; }
    public int TargetDefense { get; set; }
    public string HitResult { get; set; } = CombatHitResultIds.Miss;
    public bool IsHit { get; set; }
    public bool IsCritical { get; set; }
    public bool IsFumble { get; set; }
    public bool IsNaturalCritical { get; set; }
    public bool IsNaturalFumble { get; set; }
    public CombatAttackModifierBreakdown Modifiers { get; set; } = new CombatAttackModifierBreakdown();
    public CombatFateHookResult Fate { get; set; } = new CombatFateHookResult();
    public List<string> Warnings { get; set; } = new List<string>();
}

public sealed class CombatDefenseCalculationRequest
{
    public string EncounterId { get; set; } = string.Empty;
    public string TargetParticipantId { get; set; } = string.Empty;
    public string AttackerParticipantId { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = string.Empty;
    public string AttackType { get; set; } = string.Empty;
    public string WeaponDefinitionId { get; set; } = string.Empty;
    public decimal? DistanceMeters { get; set; }
    public string CoverState { get; set; } = string.Empty;
    public int? CoverModifierOverride { get; set; }
    public int? TargetDefenseOverride { get; set; }
    public bool IncludeArmor { get; set; } = true;
    public bool IncludeShield { get; set; } = true;
    public bool IncludeCover { get; set; } = true;
    public bool IncludeDistance { get; set; } = true;
    public bool StrictMode { get; set; }
    public string RequestId { get; set; } = string.Empty;
}

public sealed class CombatDefenseCalculationResult
{
    public string EncounterId { get; set; } = string.Empty;
    public string TargetParticipantId { get; set; } = string.Empty;
    public string AttackerParticipantId { get; set; } = string.Empty;
    public int TargetDefense { get; set; }
    public int BaseDefense { get; set; }
    public int ArmorDefenseBonus { get; set; }
    public int ShieldDefenseBonus { get; set; }
    public int CoverDefenseBonus { get; set; }
    public int DistanceDefenseBonus { get; set; }
    public int SituationalDefenseBonus { get; set; }
    public bool TargetDefenseOverrideUsed { get; set; }
    public List<CombatDefenseEquipmentSummary> ArmorItems { get; set; } = new List<CombatDefenseEquipmentSummary>();
    public List<CombatDefenseEquipmentSummary> ShieldItems { get; set; } = new List<CombatDefenseEquipmentSummary>();
    public List<string> Warnings { get; set; } = new List<string>();
    public List<string> Errors { get; set; } = new List<string>();
    public DateTime CheckedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class CombatParticipantVitalsSetRequest
{
    public string EncounterId { get; set; } = string.Empty;
    public string ParticipantId { get; set; } = string.Empty;
    public int MaxHealth { get; set; }
    public int CurrentHealth { get; set; }
    public int TemporaryHealth { get; set; }
    public int MaxMorale { get; set; }
    public int CurrentMorale { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
}

public sealed class CombatDamageApplyRequest
{
    public string EncounterId { get; set; } = string.Empty;
    public string SourceActionId { get; set; } = string.Empty;
    public string AttackerParticipantId { get; set; } = string.Empty;
    public string TargetParticipantId { get; set; } = string.Empty;
    public int DamageAmount { get; set; }
    public string DamageType { get; set; } = string.Empty;
    public string DamageSource { get; set; } = string.Empty;
    public bool IsCriticalDamage { get; set; }
    public bool IgnoreTemporaryHealth { get; set; }
    public bool AllowAutoDefeat { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
}

public sealed class CombatDamageResultResponse
{
    public string EncounterId { get; set; } = string.Empty;
    public string SourceActionId { get; set; } = string.Empty;
    public string AttackerParticipantId { get; set; } = string.Empty;
    public string TargetParticipantId { get; set; } = string.Empty;
    public int DamageAmount { get; set; }
    public int DamageApplied { get; set; }
    public int DamagePrevented { get; set; }
    public string DamageType { get; set; } = string.Empty;
    public int PreviousHealth { get; set; }
    public int CurrentHealth { get; set; }
    public int PreviousTemporaryHealth { get; set; }
    public int CurrentTemporaryHealth { get; set; }
    public bool TargetDefeated { get; set; }
    public string DefeatedReason { get; set; } = string.Empty;
    public string ActionId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public List<string> Warnings { get; set; } = new List<string>();
    public CombatFullSnapshotResponse Snapshot { get; set; } = new CombatFullSnapshotResponse();
}

public sealed class CombatVitalsSetResponse
{
    public string EncounterId { get; set; } = string.Empty;
    public string ParticipantId { get; set; } = string.Empty;
    public int MaxHealth { get; set; }
    public int CurrentHealth { get; set; }
    public int TemporaryHealth { get; set; }
    public int MaxMorale { get; set; }
    public int CurrentMorale { get; set; }
    public string Message { get; set; } = string.Empty;
    public CombatFullSnapshotResponse Snapshot { get; set; } = new CombatFullSnapshotResponse();
}

public sealed class CombatConditionApplyRequest
{
    public string EncounterId { get; set; } = string.Empty;
    public string TargetParticipantId { get; set; } = string.Empty;
    public string ConditionDefinitionId { get; set; } = string.Empty;
    public string SourceParticipantId { get; set; } = string.Empty;
    public string SourceActionId { get; set; } = string.Empty;
    public int StackCount { get; set; }
    public string DurationMode { get; set; } = string.Empty;
    public int DurationRounds { get; set; }
    public string SeverityOverride { get; set; } = string.Empty;
    public bool IsHiddenFromPlayer { get; set; }
    public string Notes { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
}

public sealed class CombatConditionRemoveRequest
{
    public string EncounterId { get; set; } = string.Empty;
    public string TargetParticipantId { get; set; } = string.Empty;
    public string ConditionInstanceId { get; set; } = string.Empty;
    public string ConditionDefinitionId { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
}

public sealed class CombatConditionListRequest
{
    public string EncounterId { get; set; } = string.Empty;
    public string ParticipantId { get; set; } = string.Empty;
    public bool IncludeRemoved { get; set; }
    public string RequestId { get; set; } = string.Empty;
}

public sealed class CombatConditionResultResponse
{
    public string EncounterId { get; set; } = string.Empty;
    public string ParticipantId { get; set; } = string.Empty;
    public CombatConditionSummary Condition { get; set; } = new CombatConditionSummary();
    public string Message { get; set; } = string.Empty;
    public List<string> Warnings { get; set; } = new List<string>();
    public CombatFullSnapshotResponse Snapshot { get; set; } = new CombatFullSnapshotResponse();
}

public sealed class CombatConditionListResponse
{
    public string EncounterId { get; set; } = string.Empty;
    public string ParticipantId { get; set; } = string.Empty;
    public List<CombatConditionSummary> Conditions { get; set; } = new List<CombatConditionSummary>();
    public List<string> Warnings { get; set; } = new List<string>();
}

public sealed class CombatConditionSummary
{
    public string ConditionInstanceId { get; set; } = string.Empty;
    public string ConditionDefinitionId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ConditionGroup { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public int StackCount { get; set; }
    public int MaxStacks { get; set; }
    public string DurationMode { get; set; } = string.Empty;
    public int RemainingRounds { get; set; }
    public bool IsHiddenFromPlayer { get; set; }
    public bool IsPositive { get; set; }
    public bool IsNegative { get; set; }
    public string Status { get; set; } = string.Empty;
    public int AppliedRoundNumber { get; set; }
    public int AppliedTurnIndex { get; set; }
    public DateTime AppliedAtUtc { get; set; }
}

public sealed class CombatWeaponAttackRequest
{
    public string EncounterId { get; set; } = string.Empty;
    public string ActorParticipantId { get; set; } = string.Empty;
    public string TargetParticipantId { get; set; } = string.Empty;
    public string WeaponItemInstanceId { get; set; } = string.Empty;
    public string WeaponDefinitionId { get; set; } = string.Empty;
    public string AmmoItemInstanceId { get; set; } = string.Empty;
    public string AmmoDefinitionId { get; set; } = string.Empty;
    public string AttackSkillId { get; set; } = string.Empty;
    public string AttackAttributeId { get; set; } = string.Empty;
    public int AttackBonus { get; set; }
    public int? DamageOverride { get; set; }
    public string DamageType { get; set; } = string.Empty;
    public decimal? DistanceMeters { get; set; }
    public int CoverModifier { get; set; }
    public int SituationalModifier { get; set; }
    public bool UseFateEngine { get; set; }
    public bool SpendActionPoint { get; set; }
    public bool AutoApplyDamage { get; set; }
    public string RequestId { get; set; } = string.Empty;
}

public sealed class CombatWeaponAttackResponse
{
    public string EncounterId { get; set; } = string.Empty;
    public string AttackActionId { get; set; } = string.Empty;
    public string DamageActionId { get; set; } = string.Empty;
    public string ActorParticipantId { get; set; } = string.Empty;
    public string TargetParticipantId { get; set; } = string.Empty;
    public string WeaponDefinitionId { get; set; } = string.Empty;
    public string AmmoDefinitionId { get; set; } = string.Empty;
    public CombatAttackResultResponse AttackResult { get; set; } = new CombatAttackResultResponse();
    public CombatDamageResultResponse DamageResult { get; set; } = new CombatDamageResultResponse();
    public CombatWeaponCombatSummary WeaponSummary { get; set; } = new CombatWeaponCombatSummary();
    public CombatAmmoCombatSummary AmmoSummary { get; set; } = new CombatAmmoCombatSummary();
    public CombatDamagePreview DamagePreview { get; set; } = new CombatDamagePreview();
    public List<string> Warnings { get; set; } = new List<string>();
    public string Message { get; set; } = string.Empty;
    public CombatFullSnapshotResponse Snapshot { get; set; } = new CombatFullSnapshotResponse();
}

public sealed class CombatWeaponCombatSummary
{
    public string WeaponItemInstanceId { get; set; } = string.Empty;
    public string WeaponDefinitionId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string WeaponType { get; set; } = string.Empty;
    public string Handedness { get; set; } = string.Empty;
    public string DamageDraft { get; set; } = string.Empty;
    public string AccuracyDraft { get; set; } = string.Empty;
    public List<string> LinkedSkillIds { get; set; } = new List<string>();
    public List<string> EquipmentSlotIds { get; set; } = new List<string>();
}

public sealed class CombatAmmoCombatSummary
{
    public string AmmoItemInstanceId { get; set; } = string.Empty;
    public string AmmoDefinitionId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string AmmoType { get; set; } = string.Empty;
    public bool Compatible { get; set; }
    public string DamageModifierDraft { get; set; } = string.Empty;
    public int Quantity { get; set; }
}

public sealed class CombatDamagePreview
{
    public int BaseDamage { get; set; }
    public int AmmoDamageModifier { get; set; }
    public int FateModifier { get; set; }
    public int CriticalMultiplier { get; set; } = 1;
    public int FinalDamage { get; set; }
    public string DamageType { get; set; } = string.Empty;
    public bool IsDraftBased { get; set; }
    public CombatFateHookResult Fate { get; set; } = new CombatFateHookResult();
}

public sealed class CombatFateHookRequest
{
    public string EncounterId { get; set; } = string.Empty;
    public string RollContext { get; set; } = string.Empty;
    public string ActorParticipantId { get; set; } = string.Empty;
    public string TargetParticipantId { get; set; } = string.Empty;
    public int BaseRoll { get; set; }
    public string DiceExpression { get; set; } = string.Empty;
    public bool UseFateEngine { get; set; }
    public string RequestId { get; set; } = string.Empty;
}

public sealed class CombatFateHookResult
{
    public bool Applied { get; set; }
    public string RollContext { get; set; } = string.Empty;
    public int BaseRoll { get; set; }
    public int FateModifiedRoll { get; set; }
    public int FateModifier { get; set; }
    public string FateSummary { get; set; } = string.Empty;
    public List<CombatFateLayerSummary> FateLayerSummaries { get; set; } = new List<CombatFateLayerSummary>();
    public List<string> Warnings { get; set; } = new List<string>();
}

public sealed class CombatFateLayerSummary
{
    public int LayerIndex { get; set; }
    public string LayerName { get; set; } = string.Empty;
    public string LayerType { get; set; } = string.Empty;
    public int Modifier { get; set; }
    public bool IsEnabled { get; set; }
    public string Summary { get; set; } = string.Empty;
}

public sealed class CombatDefenseEquipmentSummary
{
    public string ItemInstanceId { get; set; } = string.Empty;
    public string DefinitionId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string EquipmentSlotId { get; set; } = string.Empty;
    public int DefenseBonus { get; set; }
    public string Source { get; set; } = string.Empty;
}

public sealed class CombatDistanceModifierResult
{
    public decimal DistanceMeters { get; set; }
    public string DistanceBand { get; set; } = string.Empty;
    public int AttackModifier { get; set; }
    public int DefenseModifier { get; set; }
    public string Warning { get; set; } = string.Empty;
}

public sealed class CombatCoverModifierResult
{
    public string CoverState { get; set; } = string.Empty;
    public int CoverDefenseBonus { get; set; }
    public string Warning { get; set; } = string.Empty;
}

public sealed class CombatMvpSmokeRequest
{
    public string CampaignId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = string.Empty;
    public bool RunWriteSmoke { get; set; }
    public string RequestId { get; set; } = string.Empty;
}

public sealed class CombatMvpSmokeResult
{
    public bool Success { get; set; }
    public List<CombatMvpSmokeStepResult> Steps { get; set; } = new List<CombatMvpSmokeStepResult>();
    public string CreatedEncounterId { get; set; } = string.Empty;
    public List<string> Errors { get; set; } = new List<string>();
    public List<string> Warnings { get; set; } = new List<string>();
    public DateTime CheckedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class CombatMvpSmokeStepResult
{
    public string StepName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string> Errors { get; set; } = new List<string>();
    public List<string> Warnings { get; set; } = new List<string>();
}
