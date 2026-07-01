using System;
using System.Collections.Generic;

namespace Nri.Shared.Domain;

public sealed class UnifiedDefinitionDocument : EntityBase
{
    public string Category { get; set; } = string.Empty;
    public List<string> RuleSetIds { get; set; } = new List<string>();
    public string Name { get; set; } = string.Empty;
    public string PublicDescription { get; set; } = string.Empty;
    public string GMDescription { get; set; } = string.Empty;
    public Dictionary<string, object> ServerOnlyData { get; set; } = new Dictionary<string, object>();
    public Dictionary<string, object> ExtraData { get; set; } = new Dictionary<string, object>();
    public string VisibilityRule { get; set; } = VisibilityRuleIds.Public;
    public List<string> Tags { get; set; } = new List<string>();
    public bool IsArchived { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string SourceDocument { get; set; } = string.Empty;
}

public sealed class DefinitionQuery
{
    public string Category { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = string.Empty;
    public bool IncludeArchived { get; set; }
    public List<string> Tags { get; set; } = new List<string>();
    public string SearchText { get; set; } = string.Empty;
    public int Limit { get; set; } = 100;
    public int Offset { get; set; }
}

public sealed class DefinitionUpsertRequest
{
    public UnifiedDefinitionDocument Definition { get; set; } = new UnifiedDefinitionDocument();
    public long? ExpectedRevision { get; set; }
    public string ActorUserId { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
    public string Operation { get; set; } = "upsert";
}

public static class DefinitionFeatureFlags
{
    public const bool UseUnifiedDefinitionServiceV2 = false;
    public const bool UseDefinitionPackImport = false;
}
