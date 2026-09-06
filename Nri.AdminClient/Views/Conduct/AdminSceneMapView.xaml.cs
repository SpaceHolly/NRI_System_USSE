using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Nri.AdminClient.ViewModels;

namespace Nri.AdminClient.Views.Conduct;

public partial class AdminSceneMapView : UserControl
{
    private bool _isViewportPanning;
    private bool _isEditorDragging;
    private Point _lastViewportPoint;

    public AdminSceneMapView()
    {
        InitializeComponent();
        Focusable = true;
        KeyDown += OnEditorKeyDown;
        Loaded += OnLoaded;
    }

    private void MapCanvas_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not AdminSceneMapViewModel vm || sender is not Canvas canvas || Keyboard.IsKeyDown(Key.Space)) return;
        var point = e.GetPosition(canvas);
        switch ((e.OriginalSource as FrameworkElement)?.DataContext)
        {
            case SceneMapShapeUiItem shape:
                vm.SelectShapeFromUi(shape);
                break;
            case SceneMapAssetInstanceUiItem asset:
                vm.SelectedAssetInstance = asset;
                break;
            case SceneMapTilePatchUiItem patch:
                vm.SelectedTilePatch = patch;
                return;
            default:
                return;
        }

        if (!vm.BeginSelectedEditorDrag(point.X, point.Y)) return;
        _isEditorDragging = true;
        canvas.CaptureMouse();
        Focus();
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

    private void TokenCanvasButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not AdminSceneMapViewModel vm) return;
        if (sender is not FrameworkElement { Tag: SceneTokenUiItem token }) return;
        vm.SelectTokenFromUi(token);
    }

    private void ShapeCanvasButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not AdminSceneMapViewModel vm) return;
        if (sender is not FrameworkElement { Tag: SceneMapShapeUiItem shape }) return;
        vm.SelectShapeFromUi(shape);
    }

    private void TilePatchCanvasButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not AdminSceneMapViewModel vm) return;
        if (sender is not FrameworkElement { Tag: SceneMapTilePatchUiItem patch }) return;
        vm.SelectedTilePatch = patch;
    }

    private void AssetInstanceCanvasButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not AdminSceneMapViewModel vm) return;
        if (sender is not FrameworkElement { Tag: SceneMapAssetInstanceUiItem asset }) return;
        vm.SelectedAssetInstance = asset;
    }

    private void DeleteSelectedEditorObject_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not AdminSceneMapViewModel vm) return;
        var answer = MessageBox.Show(vm.EditorDeleteConfirmationText, "Удаление объекта карты",
            MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (answer == MessageBoxResult.Yes) vm.DeleteSelectedEditorObjectConfirmed();
    }

    private void MapCanvas_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not AdminSceneMapViewModel vm) return;
        if (sender is not Canvas canvas) return;
        if (Keyboard.IsKeyDown(Key.Space)) return;

        if (e.OriginalSource is FrameworkElement { DataContext: SceneMarkerUiItem or SceneTokenUiItem or SceneMapShapeUiItem or SceneMapTilePatchUiItem or SceneMapAssetInstanceUiItem })
            return;

        var point = e.GetPosition(canvas);
        if (vm.HandleLocationMapCanvasClick(point.X, point.Y))
            return;

        vm.PaintFogAtPixel(point.X, point.Y);
    }

    private void MapViewportHost_OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (DataContext is AdminSceneMapViewModel vm)
            vm.ResizeViewport(e.NewSize.Width, e.NewSize.Height);
    }

    private void MapCanvas_OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (DataContext is not AdminSceneMapViewModel vm || sender is not Canvas canvas) return;
        var point = e.GetPosition(canvas);
        vm.ZoomAtPixel(point.X, point.Y, e.Delta);
        e.Handled = true;
    }

    private void MapCanvas_OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Canvas canvas) return;
        if (e.ChangedButton != MouseButton.Middle && !(e.ChangedButton == MouseButton.Left && Keyboard.IsKeyDown(Key.Space))) return;
        _isViewportPanning = true;
        _lastViewportPoint = e.GetPosition(canvas);
        canvas.CaptureMouse();
        e.Handled = true;
    }

    private void MapCanvas_OnMouseMove(object sender, MouseEventArgs e)
    {
        if (DataContext is not AdminSceneMapViewModel vm || sender is not Canvas canvas) return;
        var point = e.GetPosition(canvas);
        vm.UpdateCursor(point.X, point.Y);
        if (_isEditorDragging && e.LeftButton == MouseButtonState.Pressed)
        {
            vm.PreviewSelectedEditorDrag(point.X, point.Y);
            e.Handled = true;
            return;
        }
        if (!_isViewportPanning) return;
        vm.PanViewport(point.X - _lastViewportPoint.X, point.Y - _lastViewportPoint.Y);
        _lastViewportPoint = point;
        e.Handled = true;
    }

    private void MapCanvas_OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is Canvas canvas && _isEditorDragging && e.ChangedButton == MouseButton.Left)
        {
            _isEditorDragging = false;
            (DataContext as AdminSceneMapViewModel)?.CommitSelectedEditorDrag();
            canvas.ReleaseMouseCapture();
            e.Handled = true;
            return;
        }
        EndViewportPan(sender as Canvas);
    }
    private void MapCanvas_OnMouseLeave(object sender, MouseEventArgs e)
    {
        if (_isEditorDragging && e.LeftButton == MouseButtonState.Released)
        {
            _isEditorDragging = false;
            (DataContext as AdminSceneMapViewModel)?.CancelSelectedEditorDrag();
            (sender as Canvas)?.ReleaseMouseCapture();
        }
        if (_isViewportPanning && e.LeftButton == MouseButtonState.Released && e.MiddleButton == MouseButtonState.Released)
            EndViewportPan(sender as Canvas);
    }

    private void OnEditorKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not AdminSceneMapViewModel vm) return;
        if (e.Key == Key.Escape && _isEditorDragging)
        {
            _isEditorDragging = false;
            vm.CancelSelectedEditorDrag();
            MapCanvas.ReleaseMouseCapture();
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Escape)
        {
            vm.CancelEditorInteraction();
            e.Handled = true;
            return;
        }
        var handled = e.Key switch
        {
            Key.Left => vm.NudgeSelectedEditorObject(-1, 0, Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)),
            Key.Right => vm.NudgeSelectedEditorObject(1, 0, Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)),
            Key.Up => vm.NudgeSelectedEditorObject(0, -1, Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)),
            Key.Down => vm.NudgeSelectedEditorObject(0, 1, Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)),
            _ => false
        };
        if (handled) e.Handled = true;
    }

    private void EndViewportPan(Canvas? canvas)
    {
        if (!_isViewportPanning) return;
        _isViewportPanning = false;
        canvas?.ReleaseMouseCapture();
    }
}
