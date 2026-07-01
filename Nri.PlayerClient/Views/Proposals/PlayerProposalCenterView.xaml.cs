using Nri.PlayerClient.ViewModels;
using System.Windows.Controls;

namespace Nri.PlayerClient.Views.Proposals;

public partial class PlayerProposalCenterView : UserControl
{
    private bool _loadedOnce;

    public PlayerProposalCenterView()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (_loadedOnce) return;
            _loadedOnce = true;
            if (DataContext is PlayerProposalCenterViewModel vm)
                vm.Refresh();
        };
    }
}
