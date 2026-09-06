using System;
using System.Collections.Generic;
using System.Linq;

namespace Nri.Shared.Domain;

public static class DevelopmentPresentationKinds0215
{
    public const string Root = "Root";
    public const string Direction = "Direction";
    public const string Path = "Path";
    public const string Specialization = "Specialization";
    public const string Milestone = "Milestone";
    public const string Support = "Support";
    public const string MixedPath = "MixedPath";
    public const string InternalProgression = "InternalProgression";
    public const string Diagnostic = "Diagnostic";
}

public static class DevelopmentProductProjectionPolicy0215
{
    // Root + six directions + twenty-four canonical base classes, with a small
    // allowance for focused branch context. This remains a bounded product view.
    public const int OverviewLimit = 48;

    public static string Classify(ClassNodeDefinition node, int groupedCount)
    {
        if (node == null) return DevelopmentPresentationKinds0215.Diagnostic;
        if (IsDiagnostic(node)) return DevelopmentPresentationKinds0215.Diagnostic;
        if (IsRoot(node)) return DevelopmentPresentationKinds0215.Root;
        if (IsMixedPath(node)) return DevelopmentPresentationKinds0215.MixedPath;
        if (Is(node.NodeType, DevelopmentNodeTypes.Specialization) || Is(node.NodeRole, DevelopmentNodeRoleIds.SubbranchLevel) || Is(node.NodeRole, "specialization"))
            return DevelopmentPresentationKinds0215.Specialization;
        if (Is(node.NodeType, DevelopmentNodeTypes.License) || Is(node.NodeRole, DevelopmentNodeRoleIds.UnlockNode) || node.Tier > 0 && node.Tier % 5 == 0)
            return DevelopmentPresentationKinds0215.Milestone;
        if (Is(node.NodeType, DevelopmentNodeTypes.Skill) || Is(node.NodeType, DevelopmentNodeTypes.Training) || Is(node.NodeRole, DevelopmentNodeRoleIds.StandaloneSkill))
            return DevelopmentPresentationKinds0215.Support;
        if (groupedCount > 1 && Is(node.NodeRole, DevelopmentNodeRoleIds.MainBranchLevel))
            return DevelopmentPresentationKinds0215.InternalProgression;
        return DevelopmentPresentationKinds0215.Path;
    }

    public static string StablePathKey(string hexagonId, ClassNodeDefinition node, string canonicalDirectionId)
    {
        var kind = IsMixedPath(node)
            ? DevelopmentPresentationKinds0215.MixedPath
            : Is(node.NodeType, DevelopmentNodeTypes.Specialization) || Is(node.NodeRole, DevelopmentNodeRoleIds.SubbranchLevel) || Is(node.NodeRole, "specialization")
                ? DevelopmentPresentationKinds0215.Specialization
                : Is(node.NodeType, DevelopmentNodeTypes.License) || Is(node.NodeRole, DevelopmentNodeRoleIds.UnlockNode)
                    ? DevelopmentPresentationKinds0215.Milestone
                    : Is(node.NodeType, DevelopmentNodeTypes.Skill) || Is(node.NodeType, DevelopmentNodeTypes.Training) || Is(node.NodeRole, DevelopmentNodeRoleIds.StandaloneSkill)
                        ? DevelopmentPresentationKinds0215.Support
                        : DevelopmentPresentationKinds0215.Path;
        if (node.LayoutGeneratedBy.StartsWith("fantasy_nri_default_core_seed_v1", StringComparison.OrdinalIgnoreCase))
            return kind == DevelopmentPresentationKinds0215.Path
                ? $"product:{hexagonId}:path:{canonicalDirectionId}:{node.NodeId}"
                : $"product:{hexagonId}:{kind.ToLowerInvariant()}:{node.NodeId}";
        if (kind != DevelopmentPresentationKinds0215.Path)
            return $"product:{hexagonId}:{kind.ToLowerInvariant()}:{node.NodeId}";
        var path = First(node.LayoutBranch, node.BranchId, node.ClassId, node.LinkedDefinitionId, node.NodeId);
        return $"product:{hexagonId}:path:{canonicalDirectionId}:{path}";
    }

    public static IReadOnlyList<T> BoundOverview<T>(IEnumerable<T> items, int limit = OverviewLimit)
        => (items ?? Enumerable.Empty<T>()).Take(Math.Max(0, Math.Min(OverviewLimit, limit))).ToList();

    public static bool CanAfford(long availableBalance, int cost)
        => availableBalance >= Math.Max(0, cost);

    public static bool IsPlayerSafeCandidate(ClassNodeDefinition node)
        => node != null && !node.IsArchived && !node.IsHidden && !node.IsGMOnly && node.IsPlayerVisible && !IsDiagnostic(node);

    public static bool IsRoot(ClassNodeDefinition node)
        => node != null && (node.NodeRole == DevelopmentNodeRoleIds.NoviceRoot || node.NodeRole == DevelopmentNodeRoleIds.MagicRoot || node.Ring == 0);

    public static bool IsMixedPath(ClassNodeDefinition node)
    {
        if (node == null) return false;
        if (string.Equals(node.NodeRole, "cross_class", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(node.NodeId, "class_assassin", StringComparison.OrdinalIgnoreCase))
            return true;
        var text = string.Join(" ", new[] { node.BranchId, node.LayoutBranch, node.LayoutGroup, node.NodeRole, node.NodeType });
        return text.IndexOf("mixed", StringComparison.OrdinalIgnoreCase) >= 0
            || text.IndexOf("hybrid", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public static bool IsDiagnostic(ClassNodeDefinition node)
    {
        if (node == null) return true;
        var text = string.Join(" ", new[] { node.NodeId, node.NodeType, node.NodeRole, node.BranchId, node.DirectionId, node.LayoutGroup });
        return text.IndexOf("diagnostic", StringComparison.OrdinalIgnoreCase) >= 0
            || text.IndexOf("performance", StringComparison.OrdinalIgnoreCase) >= 0
            || text.IndexOf("perf_", StringComparison.OrdinalIgnoreCase) >= 0
            || node.NodeRole == DevelopmentNodeRoleIds.HiddenNode;
    }

    public static string RootLabel(string hexagonId)
        => string.Equals(hexagonId, DevelopmentHexagonIds.Magic, StringComparison.OrdinalIgnoreCase) ? "Магия" : "Новичок";

    public static IReadOnlyList<string> MainDirectionLabels { get; } = new[]
    {
        "Сила — Натиск", "Ловкость — Манёвр", "Выносливость — Стойкость",
        "Интеллект — Разум", "Мудрость — Путь", "Харизма — Влияние"
    };

    private static string First(params string[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private static bool Is(string value, string expected)
        => string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);
}

public enum DevelopmentProductMutationDecision0215
{
    Proceed,
    Replay,
    Conflict
}

public static class DevelopmentProductMutationGuard0215
{
    public static DevelopmentProductMutationDecision0215 Evaluate(int currentRevision, int expectedRevision, IEnumerable<string> recentOperationIds, string operationId)
    {
        if (!string.IsNullOrWhiteSpace(operationId) && (recentOperationIds ?? Enumerable.Empty<string>()).Contains(operationId, StringComparer.Ordinal))
            return DevelopmentProductMutationDecision0215.Replay;
        return currentRevision == expectedRevision ? DevelopmentProductMutationDecision0215.Proceed : DevelopmentProductMutationDecision0215.Conflict;
    }
}
