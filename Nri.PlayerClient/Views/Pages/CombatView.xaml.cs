using System.Windows.Controls;
using System.Windows.Input;
using Nri.PlayerClient.ViewModels;
namespace Nri.PlayerClient.Views.Pages;
public partial class CombatView : UserControl
{
    public CombatView() { InitializeComponent(); }

    private void CombatMapCanvas_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is Canvas canvas && DataContext is PlayerMainViewModel viewModel)
        {
            var point = e.GetPosition(canvas);
            viewModel.MoveCombatTokenToCanvasPoint(point.X, point.Y);
            e.Handled = true;
        }
    }
}
