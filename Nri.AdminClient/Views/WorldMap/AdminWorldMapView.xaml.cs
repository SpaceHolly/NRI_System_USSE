using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Nri.AdminClient.ViewModels;

namespace Nri.AdminClient.Views.WorldMap;

public partial class AdminWorldMapView : UserControl
{
    public AdminWorldMapView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is AdminWorldMapViewModel existing)
        {
            existing.RefreshFlags();
            if (existing.IsWorldMapEnabled)
                existing.RefreshMaps();
            return;
        }

        if (Window.GetWindow(this)?.DataContext is AdminMainViewModel shell && shell.WorldMap != null)
        {
            DataContext = shell.WorldMap;
            shell.WorldMap.RefreshFlags();
            if (shell.WorldMap.IsWorldMapEnabled)
                shell.WorldMap.RefreshMaps();
        }
    }

    private void MapCanvas_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not AdminWorldMapViewModel vm) return;
        if (sender is not Canvas canvas) return;
        if (e.OriginalSource is FrameworkElement { DataContext: WorldMapMarkerUiItem })
            return;

        var point = e.GetPosition(canvas);
        vm.PaintAtPixel(point.X, point.Y);
    }

    private void MarkerCanvasButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not AdminWorldMapViewModel vm) return;
        if (sender is not FrameworkElement { Tag: WorldMapMarkerUiItem marker }) return;
        vm.SelectMarkerFromUi(marker);
    }
}
