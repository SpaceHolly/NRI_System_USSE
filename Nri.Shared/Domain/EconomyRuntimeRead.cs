using System;
using System.Collections.Generic;

namespace Nri.Shared.Domain;

public sealed class EconomyRuntimeListRequest
{
    public string CampaignId { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = string.Empty;
    public string CountryId { get; set; } = string.Empty;
    public string CityStateId { get; set; } = string.Empty;
    public string LocationId { get; set; } = string.Empty;
    public string DefinitionId { get; set; } = string.Empty;
    public bool IncludeArchived { get; set; }
    public int Limit { get; set; } = 100;
    public int Offset { get; set; }
}

public sealed class EconomyRuntimeGetRequest
{
    public string Id { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
}

public sealed class EconomyRuntimeListResponse
{
    public List<EconomyRuntimeStateSummary> Items { get; set; } = new List<EconomyRuntimeStateSummary>();
    public int Total { get; set; }
    public int Limit { get; set; }
    public int Offset { get; set; }
    public bool HasMore { get; set; }
}

public sealed class EconomyRuntimeStateSummary
{
    public string Id { get; set; } = string.Empty;
    public string RuntimeType { get; set; } = string.Empty;
    public string DefinitionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = string.Empty;
    public string CountryId { get; set; } = string.Empty;
    public string CityStateId { get; set; } = string.Empty;
    public string LocationId { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new List<string>();
    public bool IsActive { get; set; }
    public bool IsArchived { get; set; }
    public string Visibility { get; set; } = string.Empty;
}

public sealed class EconomyRuntimeStateDetails
{
    public string Id { get; set; } = string.Empty;
    public string RuntimeType { get; set; } = string.Empty;
    public string DefinitionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string CampaignId { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = string.Empty;
    public string CountryId { get; set; } = string.Empty;
    public string CityStateId { get; set; } = string.Empty;
    public List<string> LocationIds { get; set; } = new List<string>();
    public string LocationId { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new List<string>();
    public string Notes { get; set; } = string.Empty;
    public Dictionary<string, object> PublicFields { get; set; } = new Dictionary<string, object>();
    public Dictionary<string, object> HiddenFields { get; set; } = new Dictionary<string, object>();
    public int SchemaVersion { get; set; }
}

public sealed class EconomyRelationGraphRequest
{
    public string CampaignId { get; set; } = string.Empty;
    public string RootType { get; set; } = string.Empty;
    public string RootId { get; set; } = string.Empty;
    public bool IncludeFactions { get; set; } = true;
    public bool IncludeOrganizations { get; set; } = true;
    public bool IncludeMarkets { get; set; } = true;
    public bool IncludeLaws { get; set; } = true;
    public bool IncludeRestrictions { get; set; } = true;
    public bool IncludeScopes { get; set; } = true;
    public int MaxDepth { get; set; } = 2;
    public int Limit { get; set; } = 200;
}

public sealed class EconomyRelationGraphResponse
{
    public string CampaignId { get; set; } = string.Empty;
    public string RootType { get; set; } = string.Empty;
    public string RootId { get; set; } = string.Empty;
    public List<EconomyRelationNode> Nodes { get; set; } = new List<EconomyRelationNode>();
    public List<EconomyRelationEdge> Edges { get; set; } = new List<EconomyRelationEdge>();
    public List<string> Warnings { get; set; } = new List<string>();
    public DateTime BuiltAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class EconomyRelationNode
{
    public string Id { get; set; } = string.Empty;
    public string RuntimeType { get; set; } = string.Empty;
    public string DefinitionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Visibility { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new List<string>();
}

public sealed class EconomyRelationEdge
{
    public string FromId { get; set; } = string.Empty;
    public string ToId { get; set; } = string.Empty;
    public string RelationType { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty;
    public string SourceField { get; set; } = string.Empty;
    public bool IsExplicit { get; set; }
}
