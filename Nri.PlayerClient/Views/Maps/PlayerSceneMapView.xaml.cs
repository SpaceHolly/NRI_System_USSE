using System.Windows;
using System.Windows.Controls;
using Nri.PlayerClient.ViewModels;

namespace Nri.PlayerClient.Views.Maps;

public partial class PlayerSceneMapView : UserControl
{
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
}
