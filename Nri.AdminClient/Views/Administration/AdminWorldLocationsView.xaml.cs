using System.Windows.Controls;
using System.Windows.Threading;

namespace Nri.AdminClient.Views.Administration;

public partial class AdminWorldLocationsView : UserControl
{
    public AdminWorldLocationsView()
    {
        InitializeComponent();
        Loaded += (_, _) => Dispatcher.BeginInvoke(new System.Action(() =>
        {
            WorldLocationsTabControl.SelectedItem = WorldMapTab;
        }), DispatcherPriority.Loaded);
    }
}
