using System.Windows.Controls;
using System.Windows.Data;
using System.Linq;
using Nri.AdminClient.Diagnostics;
using Nri.AdminClient.ViewModels;

namespace Nri.AdminClient.Views.Administration;

public partial class AdminClassesSkillsView : UserControl
{
    private DevelopmentHexagonEditorNodeVm? _draggedDevelopmentNode;
    private System.Windows.Point _dragOffset;
    private bool _isDraggingDevelopmentNode;

    public AdminClassesSkillsView() => InitializeComponent();

    private AdminMainViewModel? ViewModel => DataContext as AdminMainViewModel
        ?? System.Windows.Window.GetWindow(this)?.DataContext as AdminMainViewModel;

    private void SelectNodeButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is not System.Windows.FrameworkElement element) return;
        var nodeId = element.Tag?.ToString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(nodeId)) return;
        var vm = ViewModel;
        ClientLogService.Instance.Info($"admin.classes.hexagon.select.click nodeId={nodeId} vm={(vm == null ? "null" : "ok")}");
        if (vm != null)
            vm.SelectedClassNodeId = nodeId;
    }

    private void SelectNodeByInputButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        CommitTextBoxBindings(this);
        var nodeId = SelectedClassNodePicker.SelectedId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(nodeId)) return;
        var vm = ViewModel;
        ClientLogService.Instance.Info($"admin.classes.hexagon.select.input nodeId={nodeId} vm={(vm == null ? "null" : "ok")}");
        if (vm != null)
            vm.SelectedClassNodeId = nodeId;
    }

    private void SelectDevelopmentHexagonButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is not System.Windows.FrameworkElement element) return;
        var hexagonId = element.Tag?.ToString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(hexagonId)) return;
        var vm = ViewModel;
        ClientLogService.Instance.Info($"admin.development.hexagon.tree.select.click hexagonId={hexagonId} vm={(vm == null ? "null" : "ok")} available={string.Join(",", vm?.DevelopmentLayoutHexagons.Select(x => x.HexagonId) ?? System.Linq.Enumerable.Empty<string>())}");
        if (vm == null) return;

        vm.SelectedDevelopmentLayoutHexagonId = hexagonId;
        ClientLogService.Instance.Info($"admin.development.hexagon.tree.select.after requested={hexagonId} selected={vm.SelectedDevelopmentLayoutHexagonId} nodes={vm.DevelopmentLayoutNodes.Count} links={vm.DevelopmentLayoutLinks.Count}");
        Dispatcher.BeginInvoke(new System.Action(ScrollDevelopmentLayoutToVisibleNodes), System.Windows.Threading.DispatcherPriority.ContextIdle);
    }

    private void SaveLayoutButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        CommitTextBoxBindings(this);
        var vm = ViewModel;
        ClientLogService.Instance.Info($"admin.classes.hexagon.layout.save.click vm={(vm == null ? "null" : "ok")} selectedNode={vm?.SelectedClassNodeId}");
        if (vm?.SaveClassNodeLayoutCommand?.CanExecute(null) == true)
            vm.SaveClassNodeLayoutCommand.Execute(null);
    }

    private void DevelopmentLayoutNode_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (ViewModel?.DevelopmentLayoutEditingEnabled != true) return;
        if (sender is not System.Windows.Controls.Button button) return;
        if (button.Tag is not DevelopmentHexagonEditorNodeVm node) return;
        var vm = ViewModel;
        vm?.SelectDevelopmentLayoutNode(node);
        vm?.BeginDevelopmentLayoutNodeDrag(node);
        var pointer = e.GetPosition(DevelopmentLayoutCanvasItems);
        _draggedDevelopmentNode = node;
        _dragOffset = new System.Windows.Point(pointer.X - node.PositionX, pointer.Y - node.PositionY);
        _isDraggingDevelopmentNode = true;
        button.CaptureMouse();
        ClientLogService.Instance.Info($"admin.development.hexagon.drag.start nodeId={node.NodeId} x={node.PositionX} y={node.PositionY}");
        e.Handled = true;
    }

    private void DevelopmentLayoutNode_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (ViewModel?.DevelopmentLayoutEditingEnabled != true) return;
        if (!_isDraggingDevelopmentNode || _draggedDevelopmentNode == null) return;
        if (e.LeftButton != System.Windows.Input.MouseButtonState.Pressed) return;
        var pointer = e.GetPosition(DevelopmentLayoutCanvasItems);
        var x = pointer.X - _dragOffset.X;
        var y = pointer.Y - _dragOffset.Y;
        ViewModel?.MoveDevelopmentLayoutNode(_draggedDevelopmentNode, x, y);
        e.Handled = true;
    }

    private void DevelopmentLayoutNode_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (ViewModel?.DevelopmentLayoutEditingEnabled != true)
        {
            _isDraggingDevelopmentNode = false;
            _draggedDevelopmentNode = null;
            return;
        }
        if (sender is System.Windows.Controls.Button button && button.IsMouseCaptured)
            button.ReleaseMouseCapture();
        if (_draggedDevelopmentNode != null)
        {
            ViewModel?.CommitDevelopmentLayoutNodeDrag(_draggedDevelopmentNode);
            ClientLogService.Instance.Info($"admin.development.hexagon.drag.drop nodeId={_draggedDevelopmentNode.NodeId} x={_draggedDevelopmentNode.PositionX} y={_draggedDevelopmentNode.PositionY}");
        }
        _isDraggingDevelopmentNode = false;
        _draggedDevelopmentNode = null;
        e.Handled = true;
    }

    private void AdminDevelopmentHexagonEditor_FitToView_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        Dispatcher.BeginInvoke(new System.Action(ScrollDevelopmentLayoutToVisibleNodes), System.Windows.Threading.DispatcherPriority.ContextIdle);
    }

    private void ScrollDevelopmentLayoutToVisibleNodes()
    {
        var vm = ViewModel;
        if (vm == null) return;

        if (vm.DevelopmentLayoutCanonicalModeSelected
            && vm.DevelopmentLayoutCanonicalRoots.Count > 0
            && vm.DevelopmentLayoutCanonicalDirections.Count > 0)
        {
            AdminDevelopmentLayoutScrollViewer.UpdateLayout();
            if (System.Math.Abs(vm.DevelopmentLayoutViewportTranslateX) > 0.01 || System.Math.Abs(vm.DevelopmentLayoutViewportTranslateY) > 0.01)
            {
                AdminDevelopmentLayoutScrollViewer.ScrollToHorizontalOffset(0);
                AdminDevelopmentLayoutScrollViewer.ScrollToVerticalOffset(0);
                return;
            }

            var canonicalMinX = System.Math.Min(
                vm.DevelopmentLayoutCanonicalRoots.Min(root => root.X),
                vm.DevelopmentLayoutCanonicalDirections.Min(direction => direction.AnchorX));
            var canonicalMinY = System.Math.Min(
                vm.DevelopmentLayoutCanonicalRoots.Min(root => root.Y),
                vm.DevelopmentLayoutCanonicalDirections.Min(direction => direction.AnchorY));
            var canonicalMaxX = System.Math.Max(
                vm.DevelopmentLayoutCanonicalRoots.Max(root => root.X + root.Width),
                vm.DevelopmentLayoutCanonicalDirections.Max(direction => direction.AnchorX + direction.AnchorWidth));
            var canonicalMaxY = System.Math.Max(
                vm.DevelopmentLayoutCanonicalRoots.Max(root => root.Y + root.Height),
                vm.DevelopmentLayoutCanonicalDirections.Max(direction => direction.AnchorY + direction.AnchorHeight));
            var canonicalZoom = vm.DevelopmentLayoutZoom <= 0 ? 1.0 : vm.DevelopmentLayoutZoom;
            var canonicalCenterX = ((canonicalMinX + canonicalMaxX) / 2.0) * canonicalZoom;
            var canonicalCenterY = ((canonicalMinY + canonicalMaxY) / 2.0) * canonicalZoom;
            var canonicalViewportWidth = AdminDevelopmentLayoutScrollViewer.ViewportWidth > 0 ? AdminDevelopmentLayoutScrollViewer.ViewportWidth : AdminDevelopmentLayoutScrollViewer.ActualWidth;
            var canonicalViewportHeight = AdminDevelopmentLayoutScrollViewer.ViewportHeight > 0 ? AdminDevelopmentLayoutScrollViewer.ViewportHeight : AdminDevelopmentLayoutScrollViewer.ActualHeight;

            AdminDevelopmentLayoutScrollViewer.ScrollToHorizontalOffset(System.Math.Max(0, canonicalCenterX - canonicalViewportWidth / 2.0));
            AdminDevelopmentLayoutScrollViewer.ScrollToVerticalOffset(System.Math.Max(0, canonicalCenterY - canonicalViewportHeight / 2.0));
            return;
        }

        var visible = vm.DevelopmentLayoutNodes.Where(node => !node.IsFilteredOut).ToList();
        if (visible.Count == 0) visible = vm.DevelopmentLayoutNodes.ToList();
        if (visible.Count == 0) return;

        AdminDevelopmentLayoutScrollViewer.UpdateLayout();
        if (System.Math.Abs(vm.DevelopmentLayoutViewportTranslateX) > 0.01 || System.Math.Abs(vm.DevelopmentLayoutViewportTranslateY) > 0.01)
        {
            AdminDevelopmentLayoutScrollViewer.ScrollToHorizontalOffset(0);
            AdminDevelopmentLayoutScrollViewer.ScrollToVerticalOffset(0);
            return;
        }

        var minX = visible.Min(node => node.PositionX);
        var minY = visible.Min(node => node.PositionY);
        var maxX = visible.Max(node => node.PositionX + node.NodeWidth);
        var maxY = visible.Max(node => node.PositionY + node.NodeHeight);
        var zoom = vm.DevelopmentLayoutZoom <= 0 ? 1.0 : vm.DevelopmentLayoutZoom;
        var centerX = ((minX + maxX) / 2.0) * zoom;
        var centerY = ((minY + maxY) / 2.0) * zoom;
        var viewportWidth = AdminDevelopmentLayoutScrollViewer.ViewportWidth > 0 ? AdminDevelopmentLayoutScrollViewer.ViewportWidth : AdminDevelopmentLayoutScrollViewer.ActualWidth;
        var viewportHeight = AdminDevelopmentLayoutScrollViewer.ViewportHeight > 0 ? AdminDevelopmentLayoutScrollViewer.ViewportHeight : AdminDevelopmentLayoutScrollViewer.ActualHeight;

        AdminDevelopmentLayoutScrollViewer.ScrollToHorizontalOffset(System.Math.Max(0, centerX - viewportWidth / 2.0));
        AdminDevelopmentLayoutScrollViewer.ScrollToVerticalOffset(System.Math.Max(0, centerY - viewportHeight / 2.0));
    }

    private static void CommitTextBoxBindings(System.Windows.DependencyObject root)
    {
        for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            if (child is TextBox textBox)
                textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            CommitTextBoxBindings(child);
        }
    }
}
