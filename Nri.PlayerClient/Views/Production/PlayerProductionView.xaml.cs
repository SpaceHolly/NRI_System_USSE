using System.Windows;
using System.Windows.Controls;
using Nri.PlayerClient.ViewModels;

namespace Nri.PlayerClient.Views.Production;

public partial class PlayerProductionView : UserControl
{
    public PlayerProductionView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is PlayerProductionViewModel viewModel)
            viewModel.RefreshAll();
    }
}
