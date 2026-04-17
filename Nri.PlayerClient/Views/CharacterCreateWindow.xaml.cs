using System.Windows;
using Nri.PlayerClient.Diagnostics;

namespace Nri.PlayerClient.Views;

public partial class CharacterCreateWindow : Window
{
    public CharacterCreateWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => ClientLogService.Instance.Info("ui.characterCreate.window.loaded scroll=true");
    }
}
