using System.Windows;
using Nri.PlayerClient.Diagnostics;

namespace Nri.PlayerClient.Views;

public partial class ChatWindow : Window
{
    public ChatWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => ClientLogService.Instance.Info("ui.chat.window.loaded scroll=true");
    }
}
