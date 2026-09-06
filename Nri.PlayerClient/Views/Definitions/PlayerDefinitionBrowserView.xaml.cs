using System.Windows.Controls;
using Nri.PlayerClient.ViewModels;

namespace Nri.PlayerClient.Views.Definitions;

public partial class PlayerDefinitionBrowserView : UserControl
{
    public PlayerDefinitionBrowserView()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (DataContext is PlayerDefinitionBrowserViewModel vm && vm.Definitions.Count == 0)
                vm.Refresh();
        };
    }
}
