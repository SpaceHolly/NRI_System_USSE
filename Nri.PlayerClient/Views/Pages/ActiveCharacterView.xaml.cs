using System;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Threading;
using Nri.PlayerClient.ViewModels;

namespace Nri.PlayerClient.Views.Pages;

public partial class ActiveCharacterView : UserControl
{
    public ActiveCharacterView()
    {
        InitializeComponent();
    }

    private PlayerMainViewModel? ViewModel => DataContext as PlayerMainViewModel;

    private void PlayerDevelopmentHexagonViewer_FitToView_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        Dispatcher.BeginInvoke(new Action(ScrollDevelopmentViewerToVisibleNodes), DispatcherPriority.ContextIdle);
    }

    private void ScrollDevelopmentViewerToVisibleNodes()
    {
        var vm = ViewModel;
        if (vm == null) return;

        var visible = vm.VisibleDevelopmentCanvasNodes.Where(node => !node.IsFilteredOut).ToList();
        if (visible.Count == 0) visible = vm.VisibleDevelopmentCanvasNodes.ToList();
        if (visible.Count == 0) return;

        PlayerDevelopmentLayoutScrollViewer.UpdateLayout();
        if (Math.Abs(vm.DevelopmentViewerViewportTranslateX) > 0.01 || Math.Abs(vm.DevelopmentViewerViewportTranslateY) > 0.01)
        {
            PlayerDevelopmentLayoutScrollViewer.ScrollToHorizontalOffset(0);
            PlayerDevelopmentLayoutScrollViewer.ScrollToVerticalOffset(0);
            return;
        }

        var minX = visible.Min(node => node.X);
        var minY = visible.Min(node => node.Y);
        var maxX = visible.Max(node => node.X + node.NodeWidth);
        var maxY = visible.Max(node => node.Y + node.NodeHeight);
        var zoom = vm.DevelopmentViewerZoom <= 0 ? 1.0 : vm.DevelopmentViewerZoom;
        var centerX = ((minX + maxX) / 2.0) * zoom;
        var centerY = ((minY + maxY) / 2.0) * zoom;
        var viewportWidth = PlayerDevelopmentLayoutScrollViewer.ViewportWidth > 0 ? PlayerDevelopmentLayoutScrollViewer.ViewportWidth : PlayerDevelopmentLayoutScrollViewer.ActualWidth;
        var viewportHeight = PlayerDevelopmentLayoutScrollViewer.ViewportHeight > 0 ? PlayerDevelopmentLayoutScrollViewer.ViewportHeight : PlayerDevelopmentLayoutScrollViewer.ActualHeight;

        PlayerDevelopmentLayoutScrollViewer.ScrollToHorizontalOffset(Math.Max(0, centerX - viewportWidth / 2.0));
        PlayerDevelopmentLayoutScrollViewer.ScrollToVerticalOffset(Math.Max(0, centerY - viewportHeight / 2.0));
    }
}
