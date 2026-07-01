using System.Windows;
using System.Windows.Controls;
using Nri.PlayerClient.ViewModels;

namespace Nri.PlayerClient.Views.Calendar;

public partial class PlayerWorldCalendarView : UserControl
{
    public PlayerWorldCalendarView()
    {
        InitializeComponent();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is PlayerWorldCalendarViewModel)
        {
            ((PlayerWorldCalendarViewModel)DataContext).RefreshFlags();
            return;
        }

        if (Window.GetWindow(this)?.DataContext is PlayerMainViewModel shell && shell.WorldCalendar != null)
        {
            DataContext = shell.WorldCalendar;
            shell.WorldCalendar.RefreshFlags();
        }
    }
}
