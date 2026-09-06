using System;
using System.Collections.Generic;

namespace Nri.Shared.Domain;

public static class LimitedProductionAuthorizationStatusIds
{
    public const string Active = "active";
    public const string Exhausted = "exhausted";
    public const string Revoked = "revoked";
}

public static class LimitedProductionClaimStatusIds
{
    public const string Reserved = "reserved";
    public const string Released = "released";
    public const string Produced = "produced";
}

public static class ManufacturingBatchResultStatusIds
{
    public const string Completed = "completed";
}

public sealed class LimitedProductionAuthorizationState : EntityBase
{
    public string CampaignId { get; set; } = string.Empty;
    public string PrototypeId { get; set; } = string.Empty;
    public string OwnerCharacterId { get; set; } = string.Empty;
    public string OwnerUserId { get; set; } = string.Empty;
    public string BlueprintDefinitionId { get; set; } = string.Empty;
    public string BlueprintStableKey { get; set; } = string.Empty;
    public string BlueprintName { get; set; } = string.Empty;
    public string ApprovalSourceTestResultId { get; set; } = string.Empty;
    public string ApprovedByUserId { get; set; } = string.Empty;
    public DateTime ApprovedAtUtc { get; set; } = DateTime.UtcNow;
    public int MaxUnits { get; set; } = 3;
    public int ReservedUnits { get; set; }
    public int ProducedUnits { get; set; }
    public string Status { get; set; } = LimitedProductionAuthorizationStatusIds.Active;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string UpdatedByUserId { get; set; } = string.Empty;
    public int Revision { get; set; } = 1;
    public Dictionary<string, object> ServerOnlyData { get; set; } = new();
}

public sealed class LimitedProductionCapacityClaimState : EntityBase
{
    public string CampaignId { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string AuthorizationId { get; set; } = string.Empty;
    public int Units { get; set; }
    public string Status { get; set; } = LimitedProductionClaimStatusIds.Reserved;
    public string ReservationOperationId { get; set; } = string.Empty;
    public string CompletionOperationId { get; set; } = string.Empty;
    public DateTime ReservedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ReleasedAtUtc { get; set; }
    public DateTime? ProducedAtUtc { get; set; }
    public string UpdatedByUserId { get; set; } = string.Empty;
    public int Revision { get; set; } = 1;
}

public sealed class ManufacturingBatchResultState : EntityBase
{
    public string CampaignId { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string AuthorizationId { get; set; } = string.Empty;
    public string OwnerCharacterId { get; set; } = string.Empty;
    public string BlueprintStableKey { get; set; } = string.Empty;
    public string BlueprintVersion { get; set; } = string.Empty;
    public int BlueprintRevision { get; set; }
    public string BlueprintName { get; set; } = string.Empty;
    public int BatchSize { get; set; }
    public List<string> OutputItemInstanceIds { get; set; } = new();
    public List<ProjectMaterialSnapshot0191> ResourcesConsumed { get; set; } = new();
    public string Status { get; set; } = ManufacturingBatchResultStatusIds.Completed;
    public string PublicSummary { get; set; } = string.Empty;
    public string GMSummary { get; set; } = string.Empty;
    public DateTime CompletedAtUtc { get; set; } = DateTime.UtcNow;
    public string CompletedByUserId { get; set; } = string.Empty;
    public string CompletionOperationId { get; set; } = string.Empty;
    public int Revision { get; set; } = 1;
    public Dictionary<string, object> ServerOnlyData { get; set; } = new();
}

public sealed class LimitedProductionSnapshot0196
{
    public string PrototypeId { get; set; } = string.Empty;
    public string ProductionAuthorizationId { get; set; } = string.Empty;
    public int ProductionAuthorizationRevision { get; set; }
    public int BatchSize { get; set; }
    public int MaxUnits { get; set; }
    public int RemainingUnitsAtCreation { get; set; }
    public string OwnerCharacterId { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = string.Empty;
    public List<ProjectMaterialSnapshot0191> PerUnitInputs { get; set; } = new();
    public List<ProjectMaterialSnapshot0191> ScaledInputs { get; set; } = new();
    public string Warning { get; set; } =
        "Ограниченная партия, не серийное производство.";
}
