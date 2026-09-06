using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Nri.PlayerClient.Views.World;

public partial class PlayerWeatherTravelView : UserControl
{
    public PlayerWeatherTravelView() => InitializeComponent();
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext?.GetType().GetProperty("RefreshWeatherTravel0217Command")?.GetValue(DataContext) is ICommand command && command.CanExecute(null)) command.Execute(null);
    }
}
