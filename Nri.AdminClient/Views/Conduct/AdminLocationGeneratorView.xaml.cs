using System.Windows;
using System.Windows.Controls;
using Nri.AdminClient.ViewModels;

namespace Nri.AdminClient.Views.Conduct;

public partial class AdminLocationGeneratorView : UserControl
{
    public AdminLocationGeneratorView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is AdminLocationGeneratorViewModel vm)
        {
            vm.LoadIfNeeded();
            return;
        }

        if (Window.GetWindow(this)?.DataContext is AdminMainViewModel shell)
        {
            DataContext = shell.LocationGenerator;
            shell.LocationGenerator.LoadIfNeeded();
        }
    }
}
