using System.Windows.Controls;
using Nri.PlayerClient.ViewModels;

namespace Nri.PlayerClient.Views.Pages;

public partial class PlayerCharacterCreationView : UserControl
{
    public PlayerCharacterCreationView()
    {
        InitializeComponent();
        Loaded += (_, _) => (DataContext as PlayerCharacterCreationViewModel)?.Load();
    }
}
