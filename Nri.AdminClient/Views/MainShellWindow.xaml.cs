using System.Windows;
using Nri.AdminClient.ViewModels;

namespace Nri.AdminClient.Views;

public partial class MainShellWindow : Window
{
    public MainShellWindow()
    {
        InitializeComponent();
        DataContext = new AdminMainViewModel();
    }
}
