using System.Windows.Controls;
using System.Windows.Input;
using Nri.PlayerClient.ViewModels;

namespace Nri.PlayerClient.Views.Pages;

public partial class PlayerDevelopmentView : UserControl
{
    public PlayerDevelopmentView()
    {
        InitializeComponent();
        Loaded += (_, _) => Focus();
    }

    private PlayerMainViewModel? ViewModel => DataContext as PlayerMainViewModel;

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (ViewModel == null) return;
        switch (e.Key)
        {
            case Key.Escape:
                ViewModel.DevelopmentBackOneLevelCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Home:
            case Key.D0:
            case Key.NumPad0:
                ViewModel.ResetDevelopmentSpatialOverview();
                e.Handled = true;
                break;
            case Key.Left:
                ViewModel.MoveDevelopmentSpatialSelection(-1, 0);
                e.Handled = true;
                break;
            case Key.Right:
                ViewModel.MoveDevelopmentSpatialSelection(1, 0);
                e.Handled = true;
                break;
            case Key.Up:
                ViewModel.MoveDevelopmentSpatialSelection(0, -1);
                e.Handled = true;
                break;
            case Key.Down:
                ViewModel.MoveDevelopmentSpatialSelection(0, 1);
                e.Handled = true;
                break;
        }
    }

    private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (ViewModel == null || Keyboard.Modifiers != ModifierKeys.Control) return;
        (e.Delta > 0 ? ViewModel.DevelopmentZoomInCommand : ViewModel.DevelopmentZoomOutCommand).Execute(null);
        e.Handled = true;
    }

    private void OnPurchaseNodeClick(object sender, System.Windows.RoutedEventArgs e)
    {
        var viewModel = (sender as System.Windows.FrameworkElement)?.DataContext as PlayerMainViewModel
            ?? ViewModel
            ?? System.Windows.Window.GetWindow(this)?.DataContext as PlayerMainViewModel
            ?? System.Windows.Application.Current?.MainWindow?.DataContext as PlayerMainViewModel;
        if (viewModel == null)
        {
            System.Windows.MessageBox.Show(
                "Не удалось открыть подтверждение развития. Обновите персонажа и повторите действие.",
                "Развитие персонажа",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            return;
        }

        viewModel.BuySelectedClassNodeCommand.Execute(null);
    }
}
