using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Nri.Ui.Wpf.Controls;

namespace Nri.AdminClient.ViewModels;

public sealed class AdminDevelopmentProductNodeVm
{
    public string Kind { get; set; } = "Path";
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string DirectionId { get; set; } = string.Empty;
    public string NodeId { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; } = 164;
    public double Height { get; set; } = 64;
    public string AccentBrush { get; set; } = "#FF57C7ED";
    public string FillBrush { get; set; } = "#FF17263B";
    public string StatusIcon { get; set; } = "→";
    public bool IsInteractive => Kind != "Root" && Kind != "Milestone";
    public Visibility HexVisibility => Kind == "Root" || Kind == "Direction" ? Visibility.Visible : Visibility.Collapsed;
    public Visibility CardVisibility => Kind == "Path" || Kind == "MixedPath" || Kind == "Specialization" ? Visibility.Visible : Visibility.Collapsed;
    public Visibility MilestoneVisibility => Kind == "Milestone" ? Visibility.Visible : Visibility.Collapsed;
    public string AutomationId => "AdminDevelopmentProduct_" + Kind + "_" + (string.IsNullOrWhiteSpace(NodeId) ? DirectionId : NodeId);
}

public sealed class AdminDevelopmentProductEdgeVm
{
    public double X1 { get; set; }
    public double Y1 { get; set; }
    public double X2 { get; set; }
    public double Y2 { get; set; }
    public string Stroke { get; set; } = "#668BC5FF";
    public double Thickness { get; set; } = 3;
}

public sealed class AdminDevelopmentTierVm
{
    public int Tier { get; set; }
    public string Text => Tier.ToString();
    public string Fill => Tier == 10 || Tier == 15 || Tier == 20 ? "#FF2D4162" : "#FF17263B";
    public string Border => Tier == 10 || Tier == 15 || Tier == 20 ? "#FFF3C969" : "#FF3B5475";
}

public partial class AdminMainViewModel
{
    private string _adminDevelopmentProductFocusDirection = string.Empty;
    private AdminDevelopmentProductNodeVm? _selectedAdminDevelopmentProductNode;

    public ObservableCollection<AdminDevelopmentProductNodeVm> AdminDevelopmentProductNodes { get; } = new ObservableCollection<AdminDevelopmentProductNodeVm>();
    public ObservableCollection<AdminDevelopmentProductEdgeVm> AdminDevelopmentProductEdges { get; } = new ObservableCollection<AdminDevelopmentProductEdgeVm>();
    public ObservableCollection<AdminDevelopmentTierVm> AdminDevelopmentProductTiers { get; } = new ObservableCollection<AdminDevelopmentTierVm>();
    public ICommand SelectAdminDevelopmentProductNodeCommand { get; private set; } = null!;
    public ICommand ResetAdminDevelopmentProductFocusCommand { get; private set; } = null!;

    public string AdminDevelopmentProductBreadcrumb => string.IsNullOrWhiteSpace(_adminDevelopmentProductFocusDirection)
        ? "Обзор развития"
        : "Обзор / " + DevelopmentLayoutCanonicalDirections.FirstOrDefault(item => string.Equals(item.DirectionId, _adminDevelopmentProductFocusDirection, StringComparison.OrdinalIgnoreCase))?.FullDisplayName;
    public string AdminDevelopmentProductModeText => string.IsNullOrWhiteSpace(_adminDevelopmentProductFocusDirection) ? "Шесть направлений" : "Фокус направления";
    public Visibility AdminDevelopmentProductInspectorVisibility => _selectedAdminDevelopmentProductNode == null ? Visibility.Collapsed : Visibility.Visible;
    public string AdminDevelopmentProductInspectorTitle => _selectedAdminDevelopmentProductNode?.Title ?? string.Empty;
    public string AdminDevelopmentProductInspectorKind => _selectedAdminDevelopmentProductNode?.Subtitle ?? string.Empty;
    public string AdminDevelopmentProductInspectorSummary => _selectedAdminDevelopmentProductNode == null
        ? string.Empty
        : "Так этот путь и его ступени будут представлены игроку. Внутренние идентификаторы и GM-данные скрыты.";

    private void InitializeAdminDevelopmentProductPreview()
    {
        SelectAdminDevelopmentProductNodeCommand = new RelayCommand<AdminDevelopmentProductNodeVm>(SelectAdminDevelopmentProductNode);
        ResetAdminDevelopmentProductFocusCommand = new RelayCommand(ResetAdminDevelopmentProductFocus);
        for (var tier = 1; tier <= 20; tier++) AdminDevelopmentProductTiers.Add(new AdminDevelopmentTierVm { Tier = tier });
    }

    private void SelectAdminDevelopmentProductNode(AdminDevelopmentProductNodeVm? node)
    {
        if (node == null) return;
        if (node.Kind == "Direction")
        {
            _adminDevelopmentProductFocusDirection = node.DirectionId;
            _selectedAdminDevelopmentProductNode = null;
            RebuildAdminDevelopmentProductPreview();
            return;
        }

        _selectedAdminDevelopmentProductNode = node;
        if (!_developmentLayoutProductPreview && !string.IsNullOrWhiteSpace(node.NodeId))
        {
            var sourceNode = DevelopmentLayoutNodes.FirstOrDefault(item => string.Equals(item.NodeId, node.NodeId, StringComparison.OrdinalIgnoreCase));
            if (sourceNode != null) SelectedDevelopmentLayoutNode = sourceNode;
        }
        NotifyAdminDevelopmentProductState();
    }

    private void ResetAdminDevelopmentProductFocus()
    {
        _adminDevelopmentProductFocusDirection = string.Empty;
        _selectedAdminDevelopmentProductNode = null;
        RebuildAdminDevelopmentProductPreview();
    }

    private void RebuildAdminDevelopmentProductPreview()
    {
        AdminDevelopmentProductNodes.Clear();
        AdminDevelopmentProductEdges.Clear();
        if (DevelopmentLayoutCanonicalDirections.Count == 0)
        {
            NotifyAdminDevelopmentProductState();
            return;
        }

        if (string.IsNullOrWhiteSpace(_adminDevelopmentProductFocusDirection)) BuildAdminDevelopmentProductOverview();
        else BuildAdminDevelopmentProductDirectionFocus();
        NotifyAdminDevelopmentProductState();
    }

    private void BuildAdminDevelopmentProductOverview()
    {
        var root = AddAdminProductNode("Root", ResolveDevelopmentCanonicalRootLabel(SelectedDevelopmentLayoutHexagonId, FindDevelopmentCanonicalRootNode(SelectedDevelopmentLayoutHexagonId)), "Основа", string.Empty, string.Empty, DevelopmentSpatialGeometry.CenterX, DevelopmentSpatialGeometry.CenterY, 190, 82, "#FF55EFB2", "✓");

        foreach (var direction in DevelopmentLayoutCanonicalDirections.OrderBy(item => item.SideIndex).Take(6))
        {
            var productSide = AdminProductSide(direction.DirectionId, direction.SideIndex);
            var p = DevelopmentSpatialGeometry.OverviewDirectionTopLeft(productSide);
            var color = AdminProductSectorColor(productSide);
            var directionNode = AddAdminProductNode("Direction", direction.FullDisplayName, "Направление", direction.DirectionId, direction.DirectionId, p.X + 87d, p.Y + 32d, 174, 64, color, "→");
            AddAdminProductEdge(root, directionNode, color, 3.5);

            var candidates = DevelopmentLayoutNodes
                .Where(node => !node.IsFilteredOut && !node.IsDiagnosticNode && NodeMatchesCanonicalDirection(node, direction.DirectionId) && !IsDevelopmentCanonicalRootNode(node.NodeId, SelectedDevelopmentLayoutHexagonId))
                .GroupBy(node => string.IsNullOrWhiteSpace(node.Branch) ? node.DisplayTitle : node.Branch, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.OrderBy(node => node.RingOrLayer).ThenBy(node => node.DisplayTitle).First())
                .Take(4)
                .ToList();
            for (var index = 0; index < candidates.Count; index++)
            {
                var pathCenter = DevelopmentSpatialGeometry.OverviewPathTopLeft(productSide, index);
                var path = AddAdminProductNode("Path", candidates[index].DisplayTitle, "Путь", candidates[index].NodeId, direction.DirectionId,
                    pathCenter.X + 77d, pathCenter.Y + 29d, 154, 58, color, "▣");
                AddAdminProductEdge(directionNode, path, color, 2.2);
            }
        }
    }

    private void BuildAdminDevelopmentProductDirectionFocus()
    {
        var direction = DevelopmentLayoutCanonicalDirections.FirstOrDefault(item => string.Equals(item.DirectionId, _adminDevelopmentProductFocusDirection, StringComparison.OrdinalIgnoreCase));
        if (direction == null)
        {
            _adminDevelopmentProductFocusDirection = string.Empty;
            BuildAdminDevelopmentProductOverview();
            return;
        }

        var color = AdminProductSectorColor(AdminProductSide(direction.DirectionId, direction.SideIndex));
        var root = AddAdminProductNode("Root", "Новичок", "Основа", string.Empty, string.Empty, 175, 500, 180, 76, "#FF55EFB2", "✓");
        var anchor = AddAdminProductNode("Direction", direction.FullDisplayName, "Направление", direction.DirectionId, direction.DirectionId, 330, 385, 178, 66, color, "✓");
        AddAdminProductEdge(root, anchor, color, 4);

        var candidates = DevelopmentLayoutNodes
            .Where(node => !node.IsFilteredOut && !node.IsDiagnosticNode && NodeMatchesCanonicalDirection(node, direction.DirectionId) && !IsDevelopmentCanonicalRootNode(node.NodeId, SelectedDevelopmentLayoutHexagonId))
            .GroupBy(node => string.IsNullOrWhiteSpace(node.Branch) ? node.DisplayTitle : node.Branch, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderBy(node => node.RingOrLayer).ThenBy(node => node.DisplayTitle).First())
            .Take(6)
            .ToList();
        var positions = new[] { (500d, 250d), (680d, 180d), (840d, 250d), (600d, 390d), (790d, 430d), (450d, 95d), (900d, 95d) };
        AdminDevelopmentProductNodeVm parent = anchor;
        for (var index = 0; index < candidates.Count; index++)
        {
            var p = positions[index];
            var kind = index == 3 ? "Specialization" : "Path";
            var node = AddAdminProductNode(kind, candidates[index].DisplayTitle, kind == "Specialization" ? "Специализация" : "Путь", candidates[index].NodeId, direction.DirectionId, p.Item1, p.Item2, 166, 64, color, index < 2 ? "→" : "▣");
            AddAdminProductEdge(parent, node, color, index < 2 ? 3.4 : 2.3);
            if (index < 4) parent = node;
        }
        var milestone = AddAdminProductNode("Milestone", "Tier 10", "Ключевая ступень", "milestone_warrior_tier_10", direction.DirectionId, 475, 155, 76, 68, "#FFF3C969", "◆");
        AddAdminProductEdge(parent, milestone, "#FFF3C969", 2.4);
    }

    private AdminDevelopmentProductNodeVm AddAdminProductNode(string kind, string title, string subtitle, string nodeId, string directionId, double centerX, double centerY, double width, double height, string accent, string icon)
    {
        var node = new AdminDevelopmentProductNodeVm
        {
            Kind = kind,
            Title = string.IsNullOrWhiteSpace(title) ? "Путь развития" : title,
            Subtitle = subtitle,
            NodeId = nodeId,
            DirectionId = directionId,
            X = centerX - width / 2,
            Y = centerY - height / 2,
            Width = width,
            Height = height,
            AccentBrush = accent,
            StatusIcon = icon
        };
        AdminDevelopmentProductNodes.Add(node);
        return node;
    }

    private void AddAdminProductEdge(AdminDevelopmentProductNodeVm from, AdminDevelopmentProductNodeVm to, string stroke, double thickness)
        => AdminDevelopmentProductEdges.Add(new AdminDevelopmentProductEdgeVm
        {
            X1 = from.X + from.Width / 2,
            Y1 = from.Y + from.Height / 2,
            X2 = to.X + to.Width / 2,
            Y2 = to.Y + to.Height / 2,
            Stroke = stroke,
            Thickness = thickness
        });

    private static string AdminProductSectorColor(int sideIndex)
    {
        return DevelopmentSpatialGeometry.SectorColor(sideIndex);
    }

    private static int AdminProductSide(string directionId, int fallback)
    {
        return DevelopmentSpatialGeometry.ResolveSectorIndex(directionId, fallback);
    }

    private void NotifyAdminDevelopmentProductState()
    {
        Notify(nameof(AdminDevelopmentProductNodes));
        Notify(nameof(AdminDevelopmentProductEdges));
        Notify(nameof(AdminDevelopmentProductBreadcrumb));
        Notify(nameof(AdminDevelopmentProductModeText));
        Notify(nameof(AdminDevelopmentProductInspectorVisibility));
        Notify(nameof(AdminDevelopmentProductInspectorTitle));
        Notify(nameof(AdminDevelopmentProductInspectorKind));
        Notify(nameof(AdminDevelopmentProductInspectorSummary));
    }
}
