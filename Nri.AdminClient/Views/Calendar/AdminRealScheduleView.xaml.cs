using System.Windows;
using System.Windows.Controls;
using Nri.AdminClient.ViewModels;

namespace Nri.AdminClient.Views.Calendar;

public partial class AdminRealScheduleView : UserControl
{
    public AdminRealScheduleView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is AdminRealScheduleViewModel)
        {
            ((AdminRealScheduleViewModel)DataContext).RefreshFlags();
            return;
        }

        if (Window.GetWindow(this)?.DataContext is AdminMainViewModel shell && shell.RealSchedule != null)
        {
            DataContext = shell.RealSchedule;
            shell.RealSchedule.RefreshFlags();
        }
    }
}
