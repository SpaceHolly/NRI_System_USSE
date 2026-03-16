using System.Windows;
using System.Windows.Controls;
using Nri.PlayerClient.ViewModels;

namespace Nri.PlayerClient.Views;

public partial class MainShellWindow : Window
{
    public MainShellWindow()
    {
        InitializeComponent();
        DataContext = new PlayerMainViewModel();
    }

    private void OnAuthPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is PlayerMainViewModel vm && sender is PasswordBox box)
            vm.PasswordText = box.Password;
    }
}
