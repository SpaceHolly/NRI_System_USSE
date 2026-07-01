using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Nri.AdminClient.ViewModels;

namespace Nri.AdminClient.Views.Conduct;

public partial class AdminSceneMapView : UserControl
{
    public AdminSceneMapView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is AdminSceneMapViewModel)
            return;

        if (Window.GetWindow(this)?.DataContext is AdminMainViewModel shell && shell.SceneMap != null)
        {
            DataContext = shell.SceneMap;
        }
    }

    private void MarkerCanvasButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not AdminSceneMapViewModel vm) return;
        if (sender is not FrameworkElement { Tag: SceneMarkerUiItem marker }) return;
        vm.SelectMarkerFromUi(marker);
    }

    private void MapCanvas_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not AdminSceneMapViewModel vm) return;
        if (sender is not Canvas canvas) return;

        if (e.OriginalSource is FrameworkElement { DataContext: SceneMarkerUiItem })
            return;

        var point = e.GetPosition(canvas);
        vm.PaintFogAtPixel(point.X, point.Y);
    }
}
