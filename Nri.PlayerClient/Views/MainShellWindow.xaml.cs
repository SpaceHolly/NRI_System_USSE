using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Nri.PlayerClient.Diagnostics;
using Nri.PlayerClient.ViewModels;

namespace Nri.PlayerClient.Views;

public partial class MainShellWindow : Window
{
    private ChatWindow? _chatWindow;
    private CharacterCreateWindow? _characterCreateWindow;

    public MainShellWindow()
    {
        InitializeComponent();
        DataContext = new PlayerMainViewModel();
        Closing += OnClosing;
        Loaded += (_, _) =>
        {
            ClientLogService.Instance.Info("ui.player.root-scroll.initialized");
            ClientLogService.Instance.Info("ui.player.main.loaded scroll=true");
            ClientLogService.Instance.Info("ui.player.sections.visible chat/create/dice/notes/session");
            ClientLogService.Instance.Info("ui.player.main-content.reachable=true");
            ClientLogService.Instance.Info("ui.dice.panel.opened");
        };
    }

    private void OnAuthPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is PlayerMainViewModel vm && sender is PasswordBox box)
            vm.PasswordText = box.Password;
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        ClientLogService.Instance.Info("Main window closing (Player)");
        _chatWindow?.Close();
        _characterCreateWindow?.Close();
        if (DataContext is PlayerMainViewModel vm)
        {
            vm.Shutdown();
        }
    }

    private void OpenChatWindow(object sender, RoutedEventArgs e)
    {
        if (_chatWindow == null || !_chatWindow.IsLoaded)
        {
            _chatWindow = new ChatWindow { Owner = this, DataContext = DataContext };
            _chatWindow.Closed += (_, _) => _chatWindow = null;
            ClientLogService.Instance.Info("ui.window.open player.chat.detached");
            _chatWindow.Show();
        }
        else
        {
            _chatWindow.Activate();
            if (DataContext is PlayerMainViewModel vm) vm.NotifyChatWindowOpened();
        }
    }

    private void OpenCharacterCreateWindow(object sender, RoutedEventArgs e)
    {
        if (_characterCreateWindow == null || !_characterCreateWindow.IsLoaded)
        {
            _characterCreateWindow = new CharacterCreateWindow { Owner = this, DataContext = DataContext };
            _characterCreateWindow.Closed += (_, _) => _characterCreateWindow = null;
            ClientLogService.Instance.Info("ui.window.open player.characterCreate.detached");
            _characterCreateWindow.Show();
        }
        else
        {
            _characterCreateWindow.Activate();
        }
    }
}
