using System.Windows;
using System.Windows.Controls;
using Nri.AdminClient.ViewModels;

namespace Nri.AdminClient.Views.Maps;

public partial class AdminRoomInteriorView : UserControl
{
    public AdminRoomInteriorView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is AdminRoomInteriorViewModel)
        {
            return;
        }

        if (Window.GetWindow(this)?.DataContext is AdminMainViewModel shell && shell.RoomInterior != null)
        {
            DataContext = shell.RoomInterior;
        }
    }
}
