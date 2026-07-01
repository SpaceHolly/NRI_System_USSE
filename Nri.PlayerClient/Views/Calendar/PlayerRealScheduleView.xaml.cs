using System.Windows;
using System.Windows.Controls;
using Nri.PlayerClient.ViewModels;

namespace Nri.PlayerClient.Views.Calendar;

public partial class PlayerRealScheduleView : UserControl
{
    public PlayerRealScheduleView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is PlayerRealScheduleViewModel)
        {
            ((PlayerRealScheduleViewModel)DataContext).RefreshFlags();
            return;
        }

        if (Window.GetWindow(this)?.DataContext is PlayerMainViewModel shell && shell.RealSchedule != null)
        {
            DataContext = shell.RealSchedule;
            shell.RealSchedule.RefreshFlags();
        }
    }
}
