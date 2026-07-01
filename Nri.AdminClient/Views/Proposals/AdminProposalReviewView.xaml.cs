using Nri.AdminClient.ViewModels;
using System.Windows.Controls;

namespace Nri.AdminClient.Views.Proposals;

public partial class AdminProposalReviewView : UserControl
{
    private bool _loadedOnce;

    public AdminProposalReviewView()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (_loadedOnce) return;
            _loadedOnce = true;
            if (DataContext is AdminProposalReviewViewModel vm)
                vm.Refresh();
        };
    }
}
