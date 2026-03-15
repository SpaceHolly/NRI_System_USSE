using System.Windows;
using Nri.PlayerClient.ViewModels;

namespace Nri.PlayerClient.Views;

public partial class MainShellWindow : Window
{
    public MainShellWindow()
    {
        InitializeComponent();
        DataContext = new PlayerMainViewModel();
    }
}
