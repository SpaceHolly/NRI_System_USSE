using System.Windows;
using System.Windows.Controls;
using Nri.PlayerClient.ViewModels;

namespace Nri.PlayerClient.Views.Maps;

public partial class PlayerWorldMapView : UserControl
{
    public PlayerWorldMapView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is PlayerMultiscaleMapViewModel0218 existing)
        {
            existing.Initialize();
            return;
        }

        if (Window.GetWindow(this)?.DataContext is PlayerMainViewModel shell && shell.WorldMap != null)
        {
            DataContext = shell.WorldMap;
            shell.WorldMap.Initialize();
        }
    }
}
