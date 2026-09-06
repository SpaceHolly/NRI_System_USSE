using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Nri.Shared.Domain;
using Nri.Ui.Wpf.Controls;

namespace Nri.PlayerClient.ViewModels;

public sealed class PlayerDevelopmentSpatialNodeVm
{
    public string NodeId { get; set; } = string.Empty;
    public string DirectionKey { get; set; } = string.Empty;
    public string PathKey { get; set; } = string.Empty;
    public string Kind { get; set; } = "Path";
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string StatusKey { get; set; } = "locked";
    public string StatusIcon { get; set; } = "▣";
    public string SectorBrush { get; set; } = "#FF6A7F99";
    public string SecondarySectorBrush { get; set; } = "#FF6A7F99";
    public string StatusBrush { get; set; } = "#FF8291A7";
    public string FillBrush { get; set; } = "#FF14253C";
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; } = 164;
    public double Height { get; set; } = 68;
    public double Opacity { get; set; } = 1;
    public bool IsInteractive => Kind == "Direction" ||
                                 (Kind != "Milestone" &&
                                  !string.IsNullOrWhiteSpace(NodeId) &&
                                  !NodeId.StartsWith("context_", StringComparison.OrdinalIgnoreCase));
    public Visibility HexVisibility => Kind == "Root" || Kind == "Direction" ? Visibility.Visible : Visibility.Collapsed;
    public Visibility CardVisibility => Kind == "Path" || Kind == "MixedPath" ? Visibility.Visible : Visibility.Collapsed;
    public Visibility SpecializationVisibility => Kind == "Specialization" ? Visibility.Visible : Visibility.Collapsed;
    public Visibility MilestoneVisibility => Kind == "Milestone" ? Visibility.Visible : Visibility.Collapsed;
    public Visibility MixedAccentVisibility => Kind == "MixedPath" ? Visibility.Visible : Visibility.Collapsed;
    public Visibility DirectionAccentVisibility => Kind == "Direction" ? Visibility.Visible : Visibility.Collapsed;
    public string AutomationId => Kind == "Root"
        ? "PlayerDevelopmentRootNode"
        : string.IsNullOrWhiteSpace(NodeId)
        ? $"PlayerDevelopmentSpatial_{Kind}_{DirectionKey}"
        : $"PlayerDevelopmentSpatial_Node_{NodeId}";
    public string LegacyRootAutomationId => Kind == "Root" && !string.IsNullOrWhiteSpace(NodeId)
        ? $"PlayerDevelopmentSpatial_Node_{NodeId}"
        : string.Empty;
    public string LabelAutomationId => AutomationId + "_Label";
    public string AccessibleStatus => StatusKey switch
    {
        "current" => "Выбрано",
        "acquired" => "Изучено",
        "available" => "Доступно",
        _ => "Закрыто"
    };
}

public sealed class PlayerDevelopmentSpatialEdgeVm
{
    public string SourceNodeId { get; set; } = string.Empty;
    public string TargetNodeId { get; set; } = string.Empty;
    public string SourceTitle { get; set; } = string.Empty;
    public string TargetTitle { get; set; } = string.Empty;
    public string Kind { get; set; } = "route-muted";
    public double X1 { get; set; }
    public double Y1 { get; set; }
    public double X2 { get; set; }
    public double Y2 { get; set; }
    public string Stroke { get; set; } = "#667893B2";
    public double Thickness { get; set; } = 1.5;
    public double Opacity { get; set; } = 0.55;
    public string DashArray { get; set; } = string.Empty;
    public string AutomationId => $"PlayerDevelopmentEdge_{Sanitize(SourceNodeId)}_{Sanitize(TargetNodeId)}";
    public string AccessibleText => $"Связь {SourceTitle} → {TargetTitle}";
    public string GeometryText => FormattableString.Invariant($"{X1:F2},{Y1:F2}|{X2:F2},{Y2:F2}|{Kind}");

    private static string Sanitize(string value)
        => string.Concat((value ?? string.Empty).Select(ch => char.IsLetterOrDigit(ch) ? ch : '_'));
}

public sealed class PlayerDevelopmentTierSegmentVm
{
    public int Tier { get; set; }
    public string Text { get; set; } = string.Empty;
    public string State { get; set; } = "future";
    public string Fill { get; set; } = "#FF1B2A40";
    public string Border { get; set; } = "#FF52657E";
    public double BorderThickness { get; set; } = 1;
    public string AccessibleText { get; set; } = string.Empty;
    public string AutomationId => $"PlayerDevelopment_Tier_{Tier:00}";
}

public partial class PlayerMainViewModel
{
    private const double DevelopmentSpatialWidth = 1000;
    private const double DevelopmentSpatialHeight = 600;
    private readonly Dictionary<string, List<ClassNodeVisualVm>> _developmentOverviewContextByHexagon = new(StringComparer.OrdinalIgnoreCase);
    private string _developmentSelectedDirectionId = string.Empty;
    private string _developmentSelectedPathId = string.Empty;
    private string _developmentSelectedSpecializationId = string.Empty;
    private string _developmentSelectedMixedPathId = string.Empty;

    public ObservableCollection<PlayerDevelopmentSpatialNodeVm> DevelopmentSpatialNodes { get; } = new ObservableCollection<PlayerDevelopmentSpatialNodeVm>();
    public ObservableCollection<PlayerDevelopmentSpatialEdgeVm> DevelopmentSpatialEdges { get; } = new ObservableCollection<PlayerDevelopmentSpatialEdgeVm>();
    public ObservableCollection<PlayerDevelopmentTierSegmentVm> DevelopmentTierSegments { get; } = new ObservableCollection<PlayerDevelopmentTierSegmentVm>();

    public ICommand SelectDevelopmentSpatialNodeCommand { get; private set; } = null!;
    public ICommand DevelopmentBackOneLevelCommand { get; private set; } = null!;
    public ICommand DevelopmentZoomInCommand { get; private set; } = null!;
    public ICommand DevelopmentZoomOutCommand { get; private set; } = null!;

    public Visibility DevelopmentInspectorVisibility => ((_developmentProductViewMode == "path" || _developmentProductViewMode == "mixed_path") && SelectedClassEntry != null)
        || (_developmentProductViewMode == "my_route" && DevelopmentSkillTracks.Count > 0)
        ? Visibility.Visible
        : Visibility.Collapsed;
    public Visibility DevelopmentPathInspectorDetailsVisibility => (_developmentProductViewMode == "path" || _developmentProductViewMode == "mixed_path") && SelectedClassEntry != null
        ? Visibility.Visible
        : Visibility.Collapsed;
    public string DevelopmentInspectorTitle => _developmentProductViewMode == "my_route"
        ? "Освоенные школы и навыки"
        : SelectedClassEntryTitle;
    public string DevelopmentInspectorSummary => _developmentProductViewMode == "my_route"
        ? "Навыки, открытые текущим путём развития персонажа."
        : SelectedClassEntrySummary;
    public Visibility DevelopmentBreadcrumbVisibility => _developmentProductViewMode == "overview"
        ? Visibility.Collapsed
        : Visibility.Visible;
    public Visibility DevelopmentDirectionBreadcrumbVisibility => string.IsNullOrWhiteSpace(_developmentSelectedDirectionId)
        ? Visibility.Collapsed
        : Visibility.Visible;
    public Visibility DevelopmentPathBreadcrumbVisibility => (_developmentProductViewMode == "path" && !string.IsNullOrWhiteSpace(_developmentSelectedPathId))
        || (_developmentProductViewMode == "mixed_path" && !string.IsNullOrWhiteSpace(_developmentSelectedMixedPathId))
        ? Visibility.Visible
        : Visibility.Collapsed;
    public string DevelopmentDirectionBreadcrumb => _developmentProductViewMode == "mixed_path"
        ? MixedDirectionBreadcrumb()
        : ResolveDirectionBreadcrumb(_developmentSelectedDirectionId);
    public string DevelopmentPathBreadcrumb => ResolveDevelopmentSelectedNodeTitle();
    public string DevelopmentFocusMode => _developmentProductViewMode;
    public string DevelopmentSelectedDirectionId => _developmentSelectedDirectionId;
    public string DevelopmentSelectedPathId => _developmentSelectedPathId;
    public string DevelopmentSelectedSpecializationId => _developmentSelectedSpecializationId;
    public string DevelopmentSelectedMixedPathId => _developmentSelectedMixedPathId;
    public string DevelopmentSpatialModeText => _developmentProductViewMode switch
    {
        "direction" => "Фокус направления",
        "path" => "Фокус пути",
        "mixed_path" => "Смешанный путь",
        "my_route" => "Мой путь",
        "available_now" => "Доступно сейчас",
        _ => "Обзор"
    };
    public string DevelopmentSelectedPathKind => SelectedClassEntry == null
        ? "Путь развития"
        : $"{PlayerDevelopmentGraphDisplay.ToReadableType(SelectedClassEntry.NodeTypeLabel)} · {SelectedClassDirectionDisplay}";
    public string DevelopmentSelectedPathTier => SelectedClassEntry == null
        ? "Диапазон не выбран"
        : DevelopmentRankRangeText(SelectedClassEntry);
    public string DevelopmentNextTierText => SelectedClassEntry == null
        ? "Следующая ступень не выбрана"
        : DevelopmentNextStepText(SelectedClassEntry);
    public string DevelopmentTierScaleTitle => SelectedClassEntry == null
        ? "Шкала рангов"
        : $"Шкала рангов {Math.Max(1, SelectedClassEntry.VisibleRankMin)}–{Math.Max(Math.Max(1, SelectedClassEntry.VisibleRankMin), SelectedClassEntry.MaxTier)}";
    public int DevelopmentTierColumnCount => Math.Max(1, DevelopmentTierSegments.Count);
    public Visibility DevelopmentRequestActionVisibility => SelectedClassEntry?.RequiresRequest == true ? Visibility.Visible : Visibility.Collapsed;
    public Visibility DevelopmentGmLegendVisibility => ClassNodes.Any(node => node.RequiresGMApproval || node.RequiresRequest) ? Visibility.Visible : Visibility.Collapsed;
    public string DevelopmentCostStateText => SelectedClassEntry == null
        ? string.Empty
        : SelectedClassEntry.IsCostResolved ? $"Стоимость следующего шага: {SelectedClassEntry.CostText}" : "Стоимость развития пока не утверждена.";
    public string DevelopmentKnownDecisionText => SelectedClassEntry?.KnownDecisionSummary ?? string.Empty;
    public Visibility DevelopmentKnownDecisionVisibility => string.IsNullOrWhiteSpace(DevelopmentKnownDecisionText) ? Visibility.Collapsed : Visibility.Visible;

    private void InitializeDevelopmentSpatialProduct()
    {
        SelectDevelopmentSpatialNodeCommand = new RelayCommand(SelectDevelopmentSpatialNode);
        DevelopmentBackOneLevelCommand = new RelayCommand(DevelopmentBackOneLevel);
        DevelopmentZoomInCommand = new RelayCommand(() => DevelopmentViewerZoom = Math.Min(1.25, DevelopmentViewerZoom + 0.1));
        DevelopmentZoomOutCommand = new RelayCommand(() => DevelopmentViewerZoom = Math.Max(0.9, DevelopmentViewerZoom - 0.1));
        RebuildDevelopmentTierSegments();
    }

    private void SelectDevelopmentSpatialNode(object? parameter)
    {
        if (parameter is not PlayerDevelopmentSpatialNodeVm node) return;
        if (node.Kind == "Direction")
        {
            SetDevelopmentProductView("direction", node.DirectionKey);
            return;
        }
        if (string.IsNullOrWhiteSpace(node.NodeId)) return;
        TrySelectClassNodeById(node.NodeId, updateStatus: true);
        var entry = ClassEntries.FirstOrDefault(item => string.Equals(item.NodeId, node.NodeId, StringComparison.OrdinalIgnoreCase));
        if (entry != null) SelectedClassEntry = entry;
        SetDevelopmentProductView(node.Kind == "MixedPath" ? "mixed_path" : "path", node.DirectionKey, node.NodeId);
    }

    private void DevelopmentBackOneLevel()
    {
        if ((_developmentProductViewMode == "path" || _developmentProductViewMode == "mixed_path") && !string.IsNullOrWhiteSpace(_developmentSelectedDirectionId))
            SetDevelopmentProductView("direction", _developmentSelectedDirectionId);
        else
            SetDevelopmentProductView("overview");
    }

    public void ResetDevelopmentSpatialOverview()
    {
        SetDevelopmentProductView("overview", string.Empty);
    }

    public void MoveDevelopmentSpatialSelection(double horizontal, double vertical)
    {
        if (DevelopmentSpatialNodes.Count == 0) return;
        var selectedId = SelectedClassEntry?.NodeId ?? string.Empty;
        var origin = DevelopmentSpatialNodes.FirstOrDefault(node => string.Equals(node.NodeId, selectedId, StringComparison.OrdinalIgnoreCase))
            ?? DevelopmentSpatialNodes.FirstOrDefault(node => node.Kind == "Root")
            ?? DevelopmentSpatialNodes[0];
        var candidate = DevelopmentSpatialNodes
            .Where(node => !ReferenceEquals(node, origin))
            .Select(node => new
            {
                Node = node,
                Dx = node.X + node.Width / 2d - (origin.X + origin.Width / 2d),
                Dy = node.Y + node.Height / 2d - (origin.Y + origin.Height / 2d)
            })
            .Where(item => item.Dx * horizontal + item.Dy * vertical > 1)
            .OrderBy(item => Math.Atan2(Math.Abs(item.Dx * vertical - item.Dy * horizontal), item.Dx * horizontal + item.Dy * vertical))
            .ThenBy(item => item.Dx * item.Dx + item.Dy * item.Dy)
            .Select(item => item.Node)
            .FirstOrDefault();
        if (candidate != null) SelectDevelopmentSpatialNode(candidate);
    }

    private void RebuildDevelopmentSpatialProduct()
    {
        DevelopmentSpatialNodes.Clear();
        DevelopmentSpatialEdges.Clear();

        var visible = VisibleDevelopmentCanvasNodes
            .Where(node => !node.IsFilteredOut)
            .Where(node => !PlayerDevelopmentLayoutVisualRules.IsDiagnosticToken(node.NodeId, node.Title, node.NodeTypeLabel, node.BranchKey, node.DirectionKey))
            .Where(node => !IsPlayerDevelopmentCanonicalRootNode(node.NodeId, SelectedDevelopmentHexagonId))
            .ToList();
        if (_developmentProductViewMode == "overview" && visible.Count > 0)
            _developmentOverviewContextByHexagon[SelectedDevelopmentHexagonId] = visible.ToList();
        var contextual = BuildDevelopmentSpatialContext(visible);
        var directions = BuildPlayerCanonicalDirections(SelectedDevelopmentHexagonId).ToList();
        var rootLabel = ResolvePlayerDevelopmentCanonicalRootLabel(SelectedDevelopmentHexagonId, FindPlayerDevelopmentCanonicalRootNode(SelectedDevelopmentHexagonId));

        if (_developmentProductViewMode == "mixed_path")
            BuildDevelopmentMixedPathSpatialProduct(rootLabel, directions, contextual);
        else if (_developmentProductViewMode == "path" || _developmentProductViewMode == "direction")
            BuildDevelopmentFocusSpatialProduct(rootLabel, directions, contextual);
        else
            BuildDevelopmentOverviewSpatialProduct(rootLabel, directions, visible);

        RebuildDevelopmentTierSegments();
        Notify(nameof(DevelopmentSpatialNodes));
        Notify(nameof(DevelopmentSpatialEdges));
        Notify(nameof(DevelopmentInspectorVisibility));
        Notify(nameof(DevelopmentPathInspectorDetailsVisibility));
        Notify(nameof(DevelopmentInspectorTitle));
        Notify(nameof(DevelopmentInspectorSummary));
        Notify(nameof(DevelopmentRequestActionVisibility));
        Notify(nameof(DevelopmentGmLegendVisibility));
        Notify(nameof(DevelopmentBreadcrumbVisibility));
        Notify(nameof(DevelopmentDirectionBreadcrumbVisibility));
        Notify(nameof(DevelopmentPathBreadcrumbVisibility));
        Notify(nameof(DevelopmentDirectionBreadcrumb));
        Notify(nameof(DevelopmentPathBreadcrumb));
        Notify(nameof(DevelopmentFocusMode));
        Notify(nameof(DevelopmentSelectedDirectionId));
        Notify(nameof(DevelopmentSelectedPathId));
        Notify(nameof(DevelopmentSelectedSpecializationId));
        Notify(nameof(DevelopmentSelectedMixedPathId));
        Notify(nameof(DevelopmentSpatialModeText));
        Notify(nameof(DevelopmentSelectedPathKind));
        Notify(nameof(DevelopmentSelectedPathTier));
        Notify(nameof(DevelopmentNextTierText));
        Notify(nameof(DevelopmentTierScaleTitle));
        Notify(nameof(DevelopmentTierColumnCount));
        Notify(nameof(DevelopmentCostStateText));
        Notify(nameof(DevelopmentKnownDecisionText));
        Notify(nameof(DevelopmentKnownDecisionVisibility));
    }

    private List<ClassNodeVisualVm> BuildDevelopmentSpatialContext(List<ClassNodeVisualVm> visible)
    {
        if (!_developmentOverviewContextByHexagon.TryGetValue(SelectedDevelopmentHexagonId, out var overview))
            return visible;

        return visible
            .Concat(overview)
            .GroupBy(node => node.NodeId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    private ClassNodeVisualVm? ResolveMixedDevelopmentNode(IEnumerable<ClassNodeVisualVm> visible)
        => visible.FirstOrDefault(node => string.Equals(node.NodeId, _developmentSelectedMixedPathId, StringComparison.OrdinalIgnoreCase))
           ?? visible.FirstOrDefault(IsMixedDevelopmentNode)
           ?? (_developmentOverviewContextByHexagon.TryGetValue(SelectedDevelopmentHexagonId, out var overview)
               ? overview.FirstOrDefault(IsMixedDevelopmentNode)
               : null);

    private string ResolveDevelopmentSelectedNodeTitle()
    {
        var selectedId = _developmentProductViewMode == "mixed_path"
            ? _developmentSelectedMixedPathId
            : _developmentSelectedPathId;
        if (string.IsNullOrWhiteSpace(selectedId)) return string.Empty;

        var current = ClassNodes.FirstOrDefault(node => string.Equals(node.NodeId, selectedId, StringComparison.OrdinalIgnoreCase));
        if (current != null) return current.DisplayTitle;
        if (_developmentOverviewContextByHexagon.TryGetValue(SelectedDevelopmentHexagonId, out var overview))
            return overview.FirstOrDefault(node => string.Equals(node.NodeId, selectedId, StringComparison.OrdinalIgnoreCase))?.DisplayTitle ?? string.Empty;
        return SelectedClassEntry?.DisplayTitle ?? string.Empty;
    }

    private string MixedDirectionBreadcrumb()
    {
        var directions = BuildPlayerCanonicalDirections(SelectedDevelopmentHexagonId).ToList();
        var mixed = ResolveMixedDevelopmentNode(ClassNodes.Concat(_developmentOverviewContextByHexagon.TryGetValue(SelectedDevelopmentHexagonId, out var overview) ? overview : Enumerable.Empty<ClassNodeVisualVm>()));
        var sectorIds = MixedSectorIds(mixed?.CanonicalNodeId).ToArray();
        var first = directions.FirstOrDefault(direction => string.Equals(direction.DirectionId, sectorIds.ElementAtOrDefault(0), StringComparison.OrdinalIgnoreCase));
        var second = directions.FirstOrDefault(direction => string.Equals(direction.DirectionId, sectorIds.ElementAtOrDefault(1), StringComparison.OrdinalIgnoreCase));
        if (first == null || second == null)
            return FormatDevelopmentDirectionLabel(_developmentSelectedDirectionId);
        return $"{FormatSpatialDirectionName(first)} + {FormatSpatialDirectionName(second)}";
    }

    private string ResolveDirectionBreadcrumb(string directionId)
    {
        var direction = BuildPlayerCanonicalDirections(SelectedDevelopmentHexagonId)
            .FirstOrDefault(item => string.Equals(item.DirectionId, directionId, StringComparison.OrdinalIgnoreCase));
        return direction == null ? FormatDevelopmentDirectionLabel(directionId) : FormatSpatialDirectionName(direction);
    }

    private void SynchronizeDevelopmentSpatialSelection(string mode, string directionKey, string nodeId)
    {
        var normalizedMode = string.IsNullOrWhiteSpace(mode) ? "overview" : mode;
        if (normalizedMode == "overview")
        {
            _developmentSelectedDirectionId = string.Empty;
            _developmentSelectedPathId = string.Empty;
            _developmentSelectedSpecializationId = string.Empty;
            _developmentSelectedMixedPathId = string.Empty;
            SelectedClassEntry = null;
            SelectedClassNodeId = string.Empty;
            return;
        }

        _developmentSelectedDirectionId = FirstNonEmpty(directionKey, _developmentViewerFocusedDirectionKey);
        _developmentSelectedSpecializationId = string.Empty;
        if (normalizedMode == "direction")
        {
            _developmentSelectedPathId = string.Empty;
            _developmentSelectedMixedPathId = string.Empty;
            SelectedClassEntry = null;
            SelectedClassNodeId = string.Empty;
            return;
        }

        if (normalizedMode == "mixed_path")
        {
            _developmentSelectedPathId = string.Empty;
            _developmentSelectedMixedPathId = FirstNonEmpty(nodeId, SelectedClassEntry?.NodeId ?? string.Empty);
            return;
        }

        _developmentSelectedMixedPathId = string.Empty;
        _developmentSelectedPathId = FirstNonEmpty(nodeId, SelectedClassEntry?.NodeId ?? string.Empty);
    }

    private void RestoreDevelopmentSpatialSelectionAfterProjection()
    {
        if (_developmentProductViewMode == "direction" || _developmentProductViewMode == "overview")
        {
            SelectedClassEntry = null;
            SelectedClassNodeId = string.Empty;
            return;
        }

        var selectedId = _developmentProductViewMode == "mixed_path"
            ? _developmentSelectedMixedPathId
            : _developmentSelectedPathId;
        if (!string.IsNullOrWhiteSpace(selectedId))
            TrySelectClassNodeById(selectedId, updateStatus: false);
    }

    private void BuildDevelopmentOverviewSpatialProduct(string rootLabel, IReadOnlyList<PlayerCanonicalDirectionDefinition> directions, List<ClassNodeVisualVm> visible)
    {
        var root = AddSpatialNode("Root", rootLabel, "Основа развития", "", "", DevelopmentSpatialGeometry.CenterX - 88d, DevelopmentSpatialGeometry.CenterY - 37d, 176, 74, "acquired", "#FF55EFB2");
        var current = ResolveCurrentDevelopmentNode(visible);
        foreach (var direction in directions.Take(6))
        {
            var sectorIndex = DevelopmentSpatialGeometry.ResolveSectorIndex(direction.DirectionId, direction.SideIndex);
            var color = DevelopmentSpatialGeometry.SectorColor(sectorIndex);
            var directionCenter = DevelopmentSpatialGeometry.OverviewDirectionTopLeft(sectorIndex);
            var directionNode = AddSpatialNode("Direction", FormatSpatialDirectionName(direction), "Направление", direction.DirectionId, direction.DirectionId, directionCenter.X, directionCenter.Y, 160, 60, "acquired", color);
            AddSpatialEdge(root, directionNode, "route-muted");

            var candidates = visible
                .Where(node => PlayerNodeMatchesCanonicalDirection(node, direction.DirectionId))
                .Where(node => !IsMixedDevelopmentNode(node))
                .OrderBy(node => DevelopmentKindRank(node))
                .ThenBy(node => node.SortOrder)
                .ThenBy(node => node.Tier)
                .Take(4)
                .ToList();
            for (var i = 0; i < candidates.Count; i++)
            {
                var node = candidates[i];
                var pathCenter = DevelopmentSpatialGeometry.OverviewPathTopLeft(sectorIndex, i);
                var spatial = AddSpatialFromNode(node, "Path", pathCenter.X, pathCenter.Y, 132, 54, color,
                    node.Tier > 0 ? $"Ранг {node.Tier}/{Math.Max(1, node.MaxTier)}" : NodeStatusLabel(node),
                    current != null && string.Equals(node.NodeId, current.NodeId, StringComparison.OrdinalIgnoreCase));
                AddSpatialEdge(directionNode, spatial, spatial.StatusKey == "current" ? "selected" : "route-muted");
            }
        }
    }

    private void BuildDevelopmentFocusSpatialProduct(string rootLabel, IReadOnlyList<PlayerCanonicalDirectionDefinition> directions, List<ClassNodeVisualVm> visible)
    {
        var focusedDirection = FirstNonEmpty(_developmentViewerFocusedDirectionKey, SelectedClassEntry?.DirectionKey ?? string.Empty, directions.FirstOrDefault()?.DirectionId ?? string.Empty);
        var direction = directions.FirstOrDefault(item => string.Equals(item.DirectionId, focusedDirection, StringComparison.OrdinalIgnoreCase)) ?? directions.FirstOrDefault();
        if (direction == null)
        {
            BuildDevelopmentOverviewSpatialProduct(rootLabel, directions, visible);
            return;
        }

        var color = ProductSectorColor(direction.DirectionId, direction.SideIndex);
        var root = AddSpatialNode("Root", rootLabel, "Основа", "", "", 190, 500, 176, 74, "acquired", "#FF55EFB2");
        var directionNode = AddSpatialNode("Direction", FormatSpatialDirectionName(direction), "Направление", direction.DirectionId, direction.DirectionId, 315, 385, 164, 60, _developmentProductViewMode == "direction" ? "current" : "acquired", color);
        AddSpatialEdge(root, directionNode, "selected");

        var directionNodes = visible.Where(node => PlayerNodeMatchesCanonicalDirection(node, direction.DirectionId) && !IsMixedDevelopmentNode(node)).ToList();
        var selected = string.IsNullOrWhiteSpace(_developmentSelectedPathId) ? null : directionNodes.FirstOrDefault(node => string.Equals(node.NodeId, _developmentSelectedPathId, StringComparison.OrdinalIgnoreCase));
        selected ??= ResolveCurrentDevelopmentNode(directionNodes) ?? directionNodes.OrderBy(node => node.SortOrder).FirstOrDefault();
        if (selected == null) return;

        var pathIsSelected = _developmentProductViewMode == "path";
        var selectedSpatial = AddSpatialFromNode(selected, "Path", 475, 270, 164, 66, color, selected.Tier > 0 ? $"Ранг {selected.Tier}/{Math.Max(20, selected.MaxTier)}" : NodeStatusLabel(selected), pathIsSelected);
        AddSpatialEdge(directionNode, selectedSpatial, pathIsSelected ? "selected" : "available");

        var sibling = directionNodes.Where(node => !string.Equals(node.NodeId, selected.NodeId, StringComparison.OrdinalIgnoreCase)).OrderBy(node => node.SortOrder).FirstOrDefault();
        if (sibling != null)
        {
            var siblingSpatial = AddSpatialFromNode(sibling, "Path", 690, 345, 164, 66, color, "Соседний путь", false, 0.45);
            AddSpatialEdge(directionNode, siblingSpatial, "available");
        }

        var decisions = directionNodes
            .Where(node => !string.Equals(node.NodeId, selected.NodeId, StringComparison.OrdinalIgnoreCase))
            .Where(node => IsDirectDevelopmentDecision(node, selected))
            .OrderBy(DevelopmentDecisionRank)
            .ThenBy(node => node.SortOrder)
            .ThenBy(node => node.Tier)
            .Take(8)
            .ToList();
        for (var i = 0; i < decisions.Count; i++)
        {
            var p = FocusDecisionPosition(i);
            var subtitle = IsMagicInternalDirection(decisions[i])
                ? "Направление магии"
                : decisions[i].Tier > selected.Tier
                ? $"Решение с ранга {decisions[i].Tier}"
                : "Направление класса";
            var decision = AddSpatialFromNode(decisions[i], "Specialization", p.x, p.y, 156, 60, color, subtitle, false);
            AddSpatialEdge(selectedSpatial, decision, decisions[i].CanPurchase ? "available" : "future");
        }

        var mixedDecisions = visible
            .Where(IsMixedDevelopmentNode)
            .Where(node => !string.IsNullOrWhiteSpace(selected.CanonicalNodeId)
                && node.RequiredCanonicalNodeIds
                    .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Any(id => string.Equals(id.Trim(), selected.CanonicalNodeId, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(node => node.SortOrder)
            .Take(2)
            .ToList();
        for (var i = 0; i < mixedDecisions.Count; i++)
        {
            var mixed = mixedDecisions[i];
            var p = FocusDecisionPosition(decisions.Count + i);
            var subtitle = string.IsNullOrWhiteSpace(mixed.KnownDecisionSummary)
                ? "Смешанный путь"
                : mixed.KnownDecisionSummary;
            var mixedSpatial = AddSpatialFromNode(mixed, "MixedPath", p.x, p.y, 190, 66, color, subtitle, false);
            var secondarySector = MixedSectorIds(mixed.CanonicalNodeId).Skip(1).FirstOrDefault();
            var secondaryDirection = directions.FirstOrDefault(item => string.Equals(item.DirectionId, secondarySector, StringComparison.OrdinalIgnoreCase));
            if (secondaryDirection != null)
                mixedSpatial.SecondarySectorBrush = ProductSectorColor(secondaryDirection.DirectionId, secondaryDirection.SideIndex);
            AddSpatialEdge(selectedSpatial, mixedSpatial, mixed.CanPurchase ? "available" : "mixed-b");
        }

        var silhouetteIndex = 0;
        foreach (var other in directions.Where(item => !string.Equals(item.DirectionId, direction.DirectionId, StringComparison.OrdinalIgnoreCase)).Take(3))
        {
            var p = silhouetteIndex++ switch
            {
                0 => (x: 835d, y: 455d),
                1 => (x: 125d, y: 420d),
                _ => (x: 455d, y: 535d)
            };
            AddSpatialNode("Direction", FormatSpatialDirectionName(other), "Ориентир", other.DirectionId, other.DirectionId, p.x, p.y, 154, 56, "locked", ProductSectorColor(other.DirectionId, other.SideIndex), 0.16);
        }
    }

    private static (double x, double y) FocusDecisionPosition(int index)
        => index switch
        {
            0 => (205d, 90d),
            1 => (385d, 70d),
            2 => (565d, 70d),
            3 => (745d, 90d),
            4 => (745d, 190d),
            5 => (745d, 285d),
            6 => (205d, 190d),
            _ => (205d, 285d)
        };

    private void BuildDevelopmentMixedPathSpatialProduct(string rootLabel, IReadOnlyList<PlayerCanonicalDirectionDefinition> directions, List<ClassNodeVisualVm> visible)
    {
        var mixed = ResolveMixedDevelopmentNode(visible);
        if (mixed == null)
        {
            BuildDevelopmentOverviewSpatialProduct(rootLabel, directions, visible);
            return;
        }

        var sectorIds = MixedSectorIds(mixed.CanonicalNodeId).ToArray();
        var first = directions.FirstOrDefault(direction => string.Equals(direction.DirectionId, sectorIds.ElementAtOrDefault(0), StringComparison.OrdinalIgnoreCase));
        var second = directions.FirstOrDefault(direction => string.Equals(direction.DirectionId, sectorIds.ElementAtOrDefault(1), StringComparison.OrdinalIgnoreCase));
        if (first == null || second == null)
        {
            BuildDevelopmentOverviewSpatialProduct(rootLabel, directions, visible);
            return;
        }

        var root = AddSpatialNode("Root", rootLabel, "Основа", "", "", 260, 500, 176, 74, "acquired", "#FF55EFB2");
        var firstDirection = AddSpatialNode("Direction", FormatSpatialDirectionName(first), "Первый сектор", first.DirectionId, first.DirectionId, 285, 395, 180, 60, "acquired", ProductSectorColor(first.DirectionId, first.SideIndex));
        var secondDirection = AddSpatialNode("Direction", FormatSpatialDirectionName(second), "Второй сектор", second.DirectionId, second.DirectionId, 535, 395, 180, 60, "acquired", ProductSectorColor(second.DirectionId, second.SideIndex));
        AddSpatialEdge(root, firstDirection, "selected");
        AddSpatialEdge(root, secondDirection, "available");

        var firstParent = ResolveCanonicalDependency(visible, mixed.CanonicalNodeId == "class_paladin" ? "class_knight" : "class_defender");
        var firstPath = firstParent == null ? null : AddSpatialFromNode(firstParent, "Path", 300, 265, 180, 66, ProductSectorColor(first.DirectionId, first.SideIndex), mixed.CanonicalNodeId == "class_paladin" ? "Рыцарь · ранг 10" : "Защитник · ранг 15", false);
        PlayerDevelopmentSpatialNodeVm? secondPath;
        if (mixed.CanonicalNodeId == "class_paladin")
        {
            var priest = ResolveCanonicalDependency(visible, "class_priest");
            secondPath = priest == null ? null : AddSpatialFromNode(priest, "Path", 520, 265, 180, 66, ProductSectorColor(second.DirectionId, second.SideIndex), "Жрец · ранг 10", false);
        }
        else
        {
            secondPath = AddSpatialNode("RequirementGroup", "Любая стрелковая ветвь · ранг 15", "Лучник · Арбалетчик · Стрелец", "requirement:any_ranged_15", second.DirectionId, 520, 255, 214, 78, "locked", ProductSectorColor(second.DirectionId, second.SideIndex));
        }
        if (firstPath != null) AddSpatialEdge(firstDirection, firstPath, "selected");
        if (secondPath != null) AddSpatialEdge(secondDirection, secondPath, "available");

        var relationship = mixed.CanonicalNodeId == "class_paladin" ? "Рыцарь 10 + Жрец 10" : "Защитник 15 + одна стрелковая ветвь 15";
        var mixedNode = AddSpatialFromNode(mixed, "MixedPath", 408, 105, 220, 78, ProductSectorColor(first.DirectionId, first.SideIndex), relationship, true);
        mixedNode.SecondarySectorBrush = ProductSectorColor(second.DirectionId, second.SideIndex);
        if (firstPath != null) AddSpatialEdge(firstPath, mixedNode, "mixed-a");
        if (secondPath != null) AddSpatialEdge(secondPath, mixedNode, mixed.CanonicalNodeId == "class_wallborn" ? "requirement-any" : "mixed-b");

        foreach (var other in directions.Where(item => !ReferenceEquals(item, first) && !ReferenceEquals(item, second)).Take(4))
        {
            var p = ProductAngle(other.DirectionId, other.AngleDegrees) switch
            {
                < 60 => (x: 830d, y: 235d),
                < 120 => (x: 780d, y: 475d),
                < 180 => (x: 530d, y: 550d),
                _ => (x: 115d, y: 420d)
            };
            AddSpatialNode("Direction", FormatSpatialDirectionName(other), "Контекст", other.DirectionId, other.DirectionId, p.x, p.y, 150, 54, "locked", ProductSectorColor(other.DirectionId, other.SideIndex), 0.14);
        }
    }

    private static string FormatSpatialDirectionName(PlayerCanonicalDirectionDefinition direction)
        => string.IsNullOrWhiteSpace(direction.AtmosphericName)
            ? direction.DisplayName
            : direction.DisplayName + " — " + direction.AtmosphericName;

    private static ClassNodeVisualVm? ResolveCanonicalDependency(IEnumerable<ClassNodeVisualVm> visible, string canonicalNodeId)
        => visible.FirstOrDefault(node => string.Equals(node.CanonicalNodeId, canonicalNodeId, StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<string> MixedSectorIds(string? canonicalNodeId)
    {
        yield return DevelopmentDirectionIds.EnduranceResilience;
        yield return string.Equals(canonicalNodeId, "class_paladin", StringComparison.OrdinalIgnoreCase)
            ? DevelopmentDirectionIds.WisdomPath
            : DevelopmentDirectionIds.DexterityManeuver;
    }

    private PlayerDevelopmentSpatialNodeVm AddSpatialFromNode(ClassNodeVisualVm source, string kind, double x, double y, double width, double height, string sectorBrush, string subtitle, bool forceCurrent = false, double opacity = 1)
    {
        var status = forceCurrent ? "current" : ResolveDevelopmentStatus(source);
        return AddSpatialNode(kind, source.DisplayTitle, subtitle, source.NodeId, source.DirectionKey, x, y, width, height, status, sectorBrush, opacity, source.PresentationKey);
    }

    private PlayerDevelopmentSpatialNodeVm AddSpatialNode(string kind, string title, string subtitle, string nodeId, string directionKey, double centerX, double centerY, double width, double height, string status, string sectorBrush, double opacity = 1, string pathKey = "")
    {
        var node = new PlayerDevelopmentSpatialNodeVm
        {
            Kind = kind,
            Title = title,
            Subtitle = subtitle,
            NodeId = nodeId,
            DirectionKey = directionKey,
            PathKey = pathKey,
            X = centerX - width / 2,
            Y = centerY - height / 2,
            Width = width,
            Height = height,
            StatusKey = status,
            StatusIcon = StatusIcon(status, kind),
            StatusBrush = StatusBrush(status),
            FillBrush = StatusFill(status),
            SectorBrush = sectorBrush,
            SecondarySectorBrush = sectorBrush,
            Opacity = opacity
        };
        DevelopmentSpatialNodes.Add(node);
        return node;
    }

    private void AddSpatialEdge(PlayerDevelopmentSpatialNodeVm source, PlayerDevelopmentSpatialNodeVm target, string kind)
    {
        var sourceCenterX = source.X + source.Width / 2;
        var sourceCenterY = source.Y + source.Height / 2;
        var targetCenterX = target.X + target.Width / 2;
        var targetCenterY = target.Y + target.Height / 2;
        var dx = targetCenterX - sourceCenterX;
        var dy = targetCenterY - sourceCenterY;
        var sourceScale = RectangleBoundaryScale(source.Width, source.Height, dx, dy);
        var targetScale = RectangleBoundaryScale(target.Width, target.Height, dx, dy);
        var edge = new PlayerDevelopmentSpatialEdgeVm
        {
            SourceNodeId = FirstNonEmpty(source.NodeId, source.Kind + "_" + source.DirectionKey),
            TargetNodeId = FirstNonEmpty(target.NodeId, target.Kind + "_" + target.DirectionKey),
            SourceTitle = source.Title,
            TargetTitle = target.Title,
            Kind = kind,
            X1 = sourceCenterX + dx * sourceScale,
            Y1 = sourceCenterY + dy * sourceScale,
            X2 = targetCenterX - dx * targetScale,
            Y2 = targetCenterY - dy * targetScale
        };
        switch (kind)
        {
            case "selected": edge.Stroke = "#FF68F1B6"; edge.Thickness = 3.3; edge.Opacity = 0.9; break;
            case "available": edge.Stroke = "#FF69A9FF"; edge.Thickness = 2.7; edge.Opacity = 0.9; break;
            case "future": edge.Stroke = "#FF8291A7"; edge.Thickness = 1.8; edge.Opacity = 0.65; edge.DashArray = "7 6"; break;
            case "mixed-a": edge.Stroke = "#FFEF6A78"; edge.Thickness = 2.1; edge.Opacity = 0.8; edge.DashArray = "8 5"; break;
            case "mixed-b": edge.Stroke = "#FF57C7ED"; edge.Thickness = 2.1; edge.Opacity = 0.8; edge.DashArray = "8 5"; break;
            case "requirement-any": edge.Stroke = "#FF57C7ED"; edge.Thickness = 2.7; edge.Opacity = 0.9; edge.DashArray = "3 4"; break;
        }
        DevelopmentSpatialEdges.Add(edge);
    }

    private static double RectangleBoundaryScale(double width, double height, double dx, double dy)
    {
        if (Math.Abs(dx) < 0.001 && Math.Abs(dy) < 0.001) return 0;
        var xScale = Math.Abs(dx) < 0.001 ? double.PositiveInfinity : width / 2 / Math.Abs(dx);
        var yScale = Math.Abs(dy) < 0.001 ? double.PositiveInfinity : height / 2 / Math.Abs(dy);
        return Math.Min(xScale, yScale);
    }

    private void RebuildDevelopmentTierSegments()
    {
        DevelopmentTierSegments.Clear();
        var min = Math.Max(1, Math.Min(20, SelectedClassEntry?.VisibleRankMin ?? 1));
        var max = Math.Max(min, Math.Min(20, SelectedClassEntry?.MaxTier ?? 20));
        var current = Math.Max(0, Math.Min(max, SelectedClassEntry?.Tier ?? 0));
        for (var tier = min; tier <= max; tier++)
        {
            var nextTier = current < min ? min : current + 1;
            var state = tier < current ? "completed" : tier == current && current >= min ? "current" : tier == nextTier ? "next" : "future";
            DevelopmentTierSegments.Add(new PlayerDevelopmentTierSegmentVm
            {
                Tier = tier,
                State = state,
                Text = state == "completed" ? "✓" : state == "current" ? "◎" : state == "next" ? "→" : tier.ToString(),
                Fill = state == "completed" ? "#FF1A4A3E" : state == "current" ? "#FF237659" : state == "next" ? "#FF173B66" : "#FF1B2A40",
                Border = state == "completed" || state == "current" ? "#FF36E39D" : state == "next" ? "#FF69A9FF" : "#FF52657E",
                BorderThickness = state == "current" || state == "next" ? 2 : 1,
                AccessibleText = $"Ранг {tier}: {state}"
            });
        }
        Notify(nameof(DevelopmentTierSegments));
        Notify(nameof(DevelopmentTierColumnCount));
        Notify(nameof(DevelopmentTierScaleTitle));
    }

    private static string DevelopmentRankRangeText(ClassEntryVm entry)
    {
        var min = Math.Max(1, entry.VisibleRankMin);
        var max = Math.Max(min, entry.MaxTier);
        if (entry.CanonicalNodeId is "class_archer" or "class_crossbowman" or "class_firearms")
            return $"Ранги {min}–{max} · продолжение пути Стрелка";
        return entry.Tier >= min ? $"Ранги {min}–{max} · текущий ранг {entry.Tier}" : $"Ранги {min}–{max} · путь ещё не начат";
    }

    private static string DevelopmentNextStepText(ClassEntryVm entry)
    {
        if (entry.CanonicalNodeId == "class_shooter") return "6 ранг · выбор специализации";
        if (!entry.IsCostResolved) return "Стоимость развития пока не утверждена.";
        var min = Math.Max(1, entry.VisibleRankMin);
        var max = Math.Max(min, entry.MaxTier);
        var next = Math.Min(max, entry.Tier < min ? min : entry.Tier + 1);
        return $"Ранг {next} · {entry.CostText}";
    }

    private static (double x, double y) Polar(double cx, double cy, double angle, double radius, double yRatio)
    {
        var radians = angle * Math.PI / 180.0;
        return (cx + Math.Cos(radians) * radius, cy + Math.Sin(radians) * radius * yRatio);
    }

    private static double ProductAngle(string directionId, double fallback)
    {
        var key = (directionId ?? string.Empty).ToLowerInvariant();
        if (key.Contains("strength")) return -90;
        if (key.Contains("dexterity") || key.Contains("agility")) return -30;
        if (key.Contains("charisma")) return 30;
        if (key.Contains("wisdom")) return 90;
        if (key.Contains("intellect")) return 150;
        if (key.Contains("endurance") || key.Contains("constitution")) return 210;
        return fallback;
    }

    private static string ProductSectorColor(string directionId, int sideIndex)
        => DevelopmentSpatialGeometry.SectorColor(DevelopmentSpatialGeometry.ResolveSectorIndex(directionId, sideIndex));

    private static bool IsMixedDevelopmentNode(ClassNodeVisualVm node)
        => string.Equals(node.PresentationKind, "MixedPath", StringComparison.OrdinalIgnoreCase)
           || node.CanonicalNodeId is "class_paladin" or "class_wallborn"
           || node.DisplayTitle.IndexOf("Боевой инженер", StringComparison.OrdinalIgnoreCase) >= 0
           || node.DirectionKey.IndexOf("mixed", StringComparison.OrdinalIgnoreCase) >= 0;

    private static int DevelopmentKindRank(ClassNodeVisualVm node)
    {
        var kind = FirstNonEmpty(node.PresentationKind, node.NodeTypeLabel).ToLowerInvariant();
        if (kind.Contains("path") || kind.Contains("class") || kind.Contains("branch")) return 0;
        if (kind.Contains("special")) return 1;
        return 2;
    }

    private static int DevelopmentDecisionRank(ClassNodeVisualVm node)
        => node.PresentationKind.IndexOf("special", StringComparison.OrdinalIgnoreCase) >= 0
            || node.Tier >= 6
            ? 0
            : 1;

    private static bool IsDirectDevelopmentDecision(ClassNodeVisualVm node, ClassNodeVisualVm selected)
        => node.RequiredNodeIds.Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries).Any(id => string.Equals(id.Trim(), selected.NodeId, StringComparison.OrdinalIgnoreCase))
           || node.PresentationKind.IndexOf("special", StringComparison.OrdinalIgnoreCase) >= 0
           || (string.Equals(node.BranchKey, selected.BranchKey, StringComparison.OrdinalIgnoreCase) && node.Tier > selected.Tier);

    private static ClassNodeVisualVm? ResolveCurrentDevelopmentNode(IEnumerable<ClassNodeVisualVm> nodes)
        => nodes.Where(node => node.State.IndexOf("Изучено", StringComparison.OrdinalIgnoreCase) >= 0 || node.State.IndexOf("taken", StringComparison.OrdinalIgnoreCase) >= 0)
            .OrderByDescending(node => node.Tier)
            .ThenBy(node => node.SortOrder)
            .FirstOrDefault()
           ?? nodes.FirstOrDefault(node => node.CanPurchase)
           ?? nodes.FirstOrDefault();

    private static string ResolveDevelopmentStatus(ClassNodeVisualVm node)
    {
        if (node.RequiresGMApproval || node.RequiresRequest) return "gm";
        if (node.State.IndexOf("Изучено", StringComparison.OrdinalIgnoreCase) >= 0 || node.State.IndexOf("taken", StringComparison.OrdinalIgnoreCase) >= 0) return "acquired";
        if (node.CanPurchase) return "available";
        if (node.RequirementSummary.IndexOf("монет", StringComparison.OrdinalIgnoreCase) >= 0 || node.RequirementSummary.IndexOf("currency", StringComparison.OrdinalIgnoreCase) >= 0) return "currency";
        return "locked";
    }

    private static string NodeStatusLabel(ClassNodeVisualVm node) => PlayerDevelopmentGraphDisplay.ToReadableState(node.State);
    private static bool IsMagicInternalDirection(ClassNodeVisualVm node)
        => node.CanonicalNodeId.StartsWith("magic_method_", StringComparison.OrdinalIgnoreCase)
           && node.CanonicalNodeId.IndexOf("_direction_", StringComparison.OrdinalIgnoreCase) >= 0;
    private static string StatusIcon(string status, string kind) => kind == "Milestone" ? "◆" : status switch { "acquired" => "✓", "current" => "◎", "available" => "→", "currency" => "●", "gm" => "GM", _ => "▣" };
    private static string StatusBrush(string status) => status switch { "acquired" or "current" => "#FF36E39D", "available" => "#FF69A9FF", "currency" or "gm" => "#FFF3C969", _ => "#FF8291A7" };
    private static string StatusFill(string status) => status switch { "acquired" => "#FF183B38", "current" => "#FF173B38", "available" => "#FF162A46", "currency" or "gm" => "#FF2D2A20", _ => "#FF111C2C" };
}
