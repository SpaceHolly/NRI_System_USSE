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
        if (DataContext is AdminMultiscaleMapViewModel0218 existing)
        {
            existing.Initialize();
            return;
        }

        if (Window.GetWindow(this)?.DataContext is AdminMainViewModel shell && shell.WorldMap != null)
        {
            DataContext = shell.WorldMap;
            shell.WorldMap.Initialize();
        }
    }

    private void MapCanvas_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Semantic edits are explicit inspector actions in the 0.21.8 workspace.
    }

    private void MarkerCanvasButton_OnClick(object sender, RoutedEventArgs e)
    {
    }

    private void TokenCanvasButton_OnClick(object sender, RoutedEventArgs e)
    {
    }
}
