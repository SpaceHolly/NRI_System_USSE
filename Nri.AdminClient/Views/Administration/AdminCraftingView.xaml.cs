using System.Windows;
using System.Windows.Controls;
using Nri.AdminClient.ViewModels;

namespace Nri.AdminClient.Views.Administration;

public partial class AdminCraftingView : UserControl
{
    public AdminCraftingView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is AdminCraftingViewModel)
            return;

        if (Window.GetWindow(this)?.DataContext is AdminMainViewModel shell)
            DataContext = shell.Crafting;
    }
}
