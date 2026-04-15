using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Nri.PlayerClient.Diagnostics;
using Nri.PlayerClient.ViewModels;

namespace Nri.PlayerClient.Views;

public partial class MainShellWindow : Window
{
    public MainShellWindow()
    {
        InitializeComponent();
        DataContext = new PlayerMainViewModel();
        Closing += OnClosing;
    }

    private void OnAuthPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is PlayerMainViewModel vm && sender is PasswordBox box)
            vm.PasswordText = box.Password;
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        ClientLogService.Instance.Info("Main window closing (Player)");
        if (DataContext is PlayerMainViewModel vm)
        {
            vm.Shutdown();
        }
    }
}
