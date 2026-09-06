using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Nri.PlayerClient.ViewModels;

namespace Nri.PlayerClient.Views.Maps;

public partial class PlayerSceneMapView : UserControl
{
    private bool _isViewportPanning;
    private Point _lastViewportPoint;

    public PlayerSceneMapView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is PlayerSceneMapViewModel)
            return;

        if (Window.GetWindow(this)?.DataContext is PlayerMainViewModel shell && shell.SceneMap != null)
            DataContext = shell.SceneMap;
    }

    private void MapViewportHost_OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (DataContext is PlayerSceneMapViewModel vm)
            vm.ResizeViewport(e.NewSize.Width, e.NewSize.Height);
    }

    private void MapCanvas_OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (DataContext is not PlayerSceneMapViewModel vm || sender is not Canvas canvas) return;
        var point = e.GetPosition(canvas);
        vm.ZoomAtPixel(point.X, point.Y, e.Delta);
        e.Handled = true;
    }

    private void MapCanvas_OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Canvas canvas) return;
        if (e.ChangedButton == MouseButton.Left && !Keyboard.IsKeyDown(Key.Space))
        {
            if (DataContext is PlayerSceneMapViewModel selectionVm)
            {
                var selectionPoint = e.GetPosition(canvas);
                selectionVm.SelectObjectAt(selectionPoint.X, selectionPoint.Y);
                e.Handled = true;
            }
            return;
        }
        if (e.ChangedButton != MouseButton.Middle && !(e.ChangedButton == MouseButton.Left && Keyboard.IsKeyDown(Key.Space))) return;
        _isViewportPanning = true;
        _lastViewportPoint = e.GetPosition(canvas);
        canvas.CaptureMouse();
        e.Handled = true;
    }

    private void MapCanvas_OnMouseMove(object sender, MouseEventArgs e)
    {
        if (DataContext is not PlayerSceneMapViewModel vm || sender is not Canvas canvas) return;
        var point = e.GetPosition(canvas);
        vm.UpdateCursor(point.X, point.Y);
        if (!_isViewportPanning) return;
        vm.PanViewport(point.X - _lastViewportPoint.X, point.Y - _lastViewportPoint.Y);
        _lastViewportPoint = point;
        e.Handled = true;
    }

    private void MapCanvas_OnMouseUp(object sender, MouseButtonEventArgs e) => EndViewportPan(sender as Canvas);
    private void MapCanvas_OnMouseLeave(object sender, MouseEventArgs e)
    {
        if (_isViewportPanning && e.LeftButton == MouseButtonState.Released && e.MiddleButton == MouseButtonState.Released)
            EndViewportPan(sender as Canvas);
    }

    private void EndViewportPan(Canvas? canvas)
    {
        if (!_isViewportPanning) return;
        _isViewportPanning = false;
        canvas?.ReleaseMouseCapture();
    }
}
