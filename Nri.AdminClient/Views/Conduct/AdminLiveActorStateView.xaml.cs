using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Nri.AdminClient.Views.Conduct;

public partial class AdminLiveActorStateView : UserControl
{
    public AdminLiveActorStateView() => InitializeComponent();
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext == null) return;
        var property = DataContext.GetType().GetProperty("RefreshAdminLiveStateCommand");
        if (property?.GetValue(DataContext) is ICommand command && command.CanExecute(null)) command.Execute(null);
    }
}
