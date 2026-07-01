using System;
using System.Collections.Generic;

namespace Nri.Shared.Domain;

public sealed class EconomyRuntimeSeedDryRunRequest
{
    public string RuleSetId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string PackId { get; set; } = string.Empty;
    public string PackPath { get; set; } = string.Empty;
    public bool IncludeFactions { get; set; } = true;
    public bool IncludeOrganizations { get; set; } = true;
    public bool IncludeLaws { get; set; } = true;
    public bool IncludeRestrictions { get; set; } = true;
    public bool IncludeMarkets { get; set; } = true;
    public bool IncludeEconomyScopes { get; set; } = true;
    public string ActorUserId { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
}

public sealed class EconomyRuntimeSeedDryRunResult
{
    public bool Success { get; set; }
    public string RuleSetId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string PackId { get; set; } = string.Empty;
    public List<EconomyRuntimeSeedPlannedState> PlannedStates { get; set; } = new List<EconomyRuntimeSeedPlannedState>();
    public List<string> Errors { get; set; } = new List<string>();
    public List<string> Warnings { get; set; } = new List<string>();
    public EconomyRuntimeSeedSummary Summary { get; set; } = new EconomyRuntimeSeedSummary();
    public DateTime CheckedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class EconomyRuntimeSeedPlannedState
{
    public string RuntimeType { get; set; } = string.Empty;
    public string DefinitionId { get; set; } = string.Empty;
    public string ProposedId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = string.Empty;
    public string SourceCategory { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public List<string> Errors { get; set; } = new List<string>();
    public List<string> Warnings { get; set; } = new List<string>();
    public Dictionary<string, object> PreviewData { get; set; } = new Dictionary<string, object>();
}

public sealed class EconomyRuntimeSeedSummary
{
    public int PlannedFactionStates { get; set; }
    public int PlannedOrganizationStates { get; set; }
    public int PlannedLawStates { get; set; }
    public int PlannedRestrictionStates { get; set; }
    public int PlannedMarketStates { get; set; }
    public int PlannedEconomyScopeStates { get; set; }
    public int ErrorCount { get; set; }
    public int WarningCount { get; set; }
}

public sealed class EconomyRuntimeSeedRequest
{
    public string RuleSetId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string PackId { get; set; } = string.Empty;
    public string PackPath { get; set; } = string.Empty;
    public bool IncludeFactions { get; set; } = true;
    public bool IncludeOrganizations { get; set; } = true;
    public bool IncludeLaws { get; set; } = true;
    public bool IncludeRestrictions { get; set; } = true;
    public bool IncludeMarkets { get; set; } = true;
    public bool IncludeEconomyScopes { get; set; } = true;
    public bool RequireDryRunSuccess { get; set; } = true;
    public bool AllowOverwrite { get; set; }
    public bool ValidateOnly { get; set; }
    public string ActorUserId { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
}

public sealed class EconomyRuntimeSeedResult
{
    public bool Success { get; set; }
    public string RuleSetId { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string PackId { get; set; } = string.Empty;
    public List<EconomyRuntimeSeedCreatedState> CreatedStates { get; set; } = new List<EconomyRuntimeSeedCreatedState>();
    public List<EconomyRuntimeSeedSkippedState> SkippedStates { get; set; } = new List<EconomyRuntimeSeedSkippedState>();
    public List<string> Errors { get; set; } = new List<string>();
    public List<string> Warnings { get; set; } = new List<string>();
    public EconomyRuntimeSeedWriteSummary Summary { get; set; } = new EconomyRuntimeSeedWriteSummary();
    public DateTime SeededAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class EconomyRuntimeSeedCreatedState
{
    public string RuntimeType { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public string DefinitionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string CollectionName { get; set; } = string.Empty;
}

public sealed class EconomyRuntimeSeedSkippedState
{
    public string RuntimeType { get; set; } = string.Empty;
    public string ProposedId { get; set; } = string.Empty;
    public string DefinitionId { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

public sealed class EconomyRuntimeSeedWriteSummary
{
    public int CreatedFactions { get; set; }
    public int CreatedOrganizations { get; set; }
    public int CreatedLaws { get; set; }
    public int CreatedRestrictions { get; set; }
    public int CreatedMarkets { get; set; }
    public int CreatedEconomyScopes { get; set; }
    public int SkippedExisting { get; set; }
    public int ErrorCount { get; set; }
    public int WarningCount { get; set; }
}
