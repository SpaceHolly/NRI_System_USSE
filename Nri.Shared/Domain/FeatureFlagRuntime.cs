using System;

namespace Nri.Shared.Domain;

public sealed class FeatureFlagOverrideState : EntityBase
{
    public string FlagName { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public bool Value { get; set; }
    public string Source { get; set; } = "database";
    public string UpdatedByUserId { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string Reason { get; set; } = string.Empty;
}
