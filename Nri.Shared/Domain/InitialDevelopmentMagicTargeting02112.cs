using System;
using System.Collections.Generic;
using System.Linq;

namespace Nri.Shared.Domain;

public static class InitialDevelopmentStatusIds
{
    public const string Pending = "pending";
    public const string Completed = "completed";
    public const string ResetByGm = "reset_by_gm";
}

public static class InitialDevelopmentGrantSources
{
    public const string InitialDevelopment = "initial_development";
}

public sealed class InitialDevelopmentClassSelectionOption
{
    public int ClassCount { get; set; }
    public int RankPerClass { get; set; }
    public bool RequireDistinctClasses { get; set; } = true;
}

public sealed class InitialDevelopmentPolicy
{
    public string PolicyId { get; set; } = string.Empty;
    public string RuleSetId { get; set; } = RuleSetIds.FantasyNriDefault;
    public List<InitialDevelopmentClassSelectionOption> ClassSelectionOptions { get; set; } = new();
    public List<string> AllowedBaseClassNodeIds { get; set; } = new();
    public int MagicMethodGrantRank { get; set; } = 1;
    public List<string> AllowedPrimaryMagicMethodNodeIds { get; set; } = new();
    public int BasicMagicDirectionGrantRank { get; set; } = 1;
    public List<string> AllowedBasicMagicDirectionNodeIds { get; set; } = new();
    public bool MustCompleteBeforeActiveSession { get; set; } = true;
    public int SchemaVersion { get; set; } = 1;
    public long EntityRevision { get; set; } = 1;
}

public sealed class InitialDevelopmentClassGrant
{
    public string DevelopmentNodeId { get; set; } = string.Empty;
    public int Rank { get; set; }
}

public sealed class InitialDevelopmentState
{
    public string Status { get; set; } = InitialDevelopmentStatusIds.Pending;
    public string PolicyId { get; set; } = string.Empty;
    public long PolicyRevision { get; set; }
    public List<InitialDevelopmentClassGrant> SelectedClassGrants { get; set; } = new();
    public string SelectedMagicMethodNodeId { get; set; } = string.Empty;
    public string SelectedBasicMagicDirectionNodeId { get; set; } = string.Empty;
    public string CompletionOperationId { get; set; } = string.Empty;
    public DateTime? CompletedAtUtc { get; set; }
    public string CompletedByUserId { get; set; } = string.Empty;
    public DateTime? ResetAtUtc { get; set; }
    public string ResetByUserId { get; set; } = string.Empty;
    public string ResetReason { get; set; } = string.Empty;
    public long EntityRevision { get; set; } = 1;
}

public static class MagicTargetScopeIds
{
    public const string Self = "self";
    public const string OtherActor = "other_actor";
    public const string Object = "object";
    public const string Position = "position";
    public const string Area = "area";

    public static readonly string[] All = { Self, OtherActor, Object, Position, Area };

    public static bool IsSupported(string value) =>
        All.Contains((value ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase);
}

public sealed class MagicTargetScopeConstraint
{
    public List<string> AllowedTargetScopes { get; set; } = new();
}

public sealed class MagicTargetScopeEvaluation
{
    public bool IsAllowed { get; set; }
    public string RequestedScope { get; set; } = string.Empty;
    public List<string> EffectiveAllowedScopes { get; set; } = new();
    public string PublicReason { get; set; } = string.Empty;
    public string GmDiagnosticReason { get; set; } = string.Empty;
}

public static class MagicTargetScopeEvaluator
{
    public static MagicTargetScopeEvaluation Evaluate(
        IEnumerable<string>? methodAllowedScopes,
        IEnumerable<string>? techniqueAllowedScopes,
        string requestedScope,
        string restrictionDisplayName = "Этот магический метод")
    {
        var all = new HashSet<string>(MagicTargetScopeIds.All, StringComparer.OrdinalIgnoreCase);
        var method = Normalize(methodAllowedScopes, all);
        var technique = Normalize(techniqueAllowedScopes, all);
        var effective = method.Intersect(technique, StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
        var requested = (requestedScope ?? string.Empty).Trim();
        var allowed = MagicTargetScopeIds.IsSupported(requested)
                      && effective.Contains(requested, StringComparer.OrdinalIgnoreCase);
        return new MagicTargetScopeEvaluation
        {
            IsAllowed = allowed,
            RequestedScope = requested,
            EffectiveAllowedScopes = effective,
            PublicReason = allowed
                ? "Выбранная цель допустима."
                : effective.Count == 1 && string.Equals(effective[0], MagicTargetScopeIds.Self, StringComparison.OrdinalIgnoreCase)
                    ? $"{restrictionDisplayName} может применяться только на самого использующего."
                    : "Выбранный тип цели недоступен для этого магического действия.",
            GmDiagnosticReason = allowed
                ? $"scope_allowed:{requested}"
                : $"scope_denied:{requested};effective={string.Join(",", effective)}"
        };
    }

    private static HashSet<string> Normalize(IEnumerable<string>? values, HashSet<string> fallback)
    {
        var normalized = new HashSet<string>((values ?? Array.Empty<string>())
            .Select(x => (x ?? string.Empty).Trim())
            .Where(MagicTargetScopeIds.IsSupported), StringComparer.OrdinalIgnoreCase);
        return normalized.Count == 0
            ? new HashSet<string>(fallback, StringComparer.OrdinalIgnoreCase)
            : normalized;
    }
}
