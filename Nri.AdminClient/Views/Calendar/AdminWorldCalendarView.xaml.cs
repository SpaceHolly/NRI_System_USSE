using System.Windows;
using System.Windows.Controls;
using Nri.AdminClient.ViewModels;

namespace Nri.AdminClient.Views.Calendar;

public partial class AdminWorldCalendarView : UserControl
{
    public AdminWorldCalendarView()
    {
        InitializeComponent();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is AdminWorldCalendarViewModel)
        {
            ((AdminWorldCalendarViewModel)DataContext).RefreshFlags();
            return;
        }

        if (Window.GetWindow(this)?.DataContext is AdminMainViewModel shell && shell.WorldCalendar != null)
        {
            DataContext = shell.WorldCalendar;
            shell.WorldCalendar.RefreshFlags();
        }
    }
}
