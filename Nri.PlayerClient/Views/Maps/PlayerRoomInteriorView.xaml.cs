using System.Windows;
using System.Windows.Controls;
using Nri.PlayerClient.ViewModels;

namespace Nri.PlayerClient.Views.Maps;

public partial class PlayerRoomInteriorView : UserControl
{
    public PlayerRoomInteriorView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is PlayerRoomInteriorViewModel)
        {
            return;
        }

        if (Window.GetWindow(this)?.DataContext is PlayerMainViewModel shell && shell.RoomInterior != null)
        {
            DataContext = shell.RoomInterior;
        }
    }
}
