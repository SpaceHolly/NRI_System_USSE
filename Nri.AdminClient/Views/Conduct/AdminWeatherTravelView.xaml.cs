using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Nri.AdminClient.Views.Conduct;

public partial class AdminWeatherTravelView : UserControl
{
    public AdminWeatherTravelView() => InitializeComponent();
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext?.GetType().GetProperty("RefreshAdminWeather0217Command")?.GetValue(DataContext) is ICommand command && command.CanExecute(null)) command.Execute(null);
    }
}
