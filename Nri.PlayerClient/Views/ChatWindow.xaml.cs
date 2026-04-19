using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Collections.Specialized;
using System.ComponentModel;
using Nri.PlayerClient.Diagnostics;
using Nri.PlayerClient.ViewModels;

namespace Nri.PlayerClient.Views;

public partial class ChatWindow : Window
{
    private ScrollViewer? _chatScrollViewer;
    private bool _userAtLatest = true;

    public ChatWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            ClientLogService.Instance.Info("ui.chat.window.loaded scroll=true");
            ClientLogService.Instance.Info("chat.window size=400x700");
            _chatScrollViewer = FindDescendantScrollViewer(ChatMessagesList);
            if (_chatScrollViewer != null) _chatScrollViewer.ScrollChanged += OnChatScrollChanged;
            if (DataContext is PlayerMainViewModel vm)
            {
                vm.PropertyChanged += OnVmPropertyChanged;
                vm.ChatMessageRows.CollectionChanged += OnChatRowsChanged;
                vm.NotifyChatWindowOpened();
            }
            ScrollToLatest(force: true);
        };
        Unloaded += (_, _) =>
        {
            if (DataContext is PlayerMainViewModel vm)
            {
                vm.PropertyChanged -= OnVmPropertyChanged;
                vm.ChatMessageRows.CollectionChanged -= OnChatRowsChanged;
            }
            if (_chatScrollViewer != null) _chatScrollViewer.ScrollChanged -= OnChatScrollChanged;
        };
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PlayerMainViewModel.ChatScrollRequestVersion))
        {
            ScrollToLatest(force: true);
        }
    }

    private void OnChatRowsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_userAtLatest)
        {
            ScrollToLatest(force: false);
        }
    }

    private void OnChatScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        var tolerance = 16d;
        _userAtLatest = e.VerticalOffset >= e.ExtentHeight - e.ViewportHeight - tolerance;
    }

    private void ScrollToLatest(bool force)
    {
        if (ChatMessagesList.Items.Count == 0) return;
        if (!force && !_userAtLatest) return;
        var last = ChatMessagesList.Items[ChatMessagesList.Items.Count - 1];
        ChatMessagesList.ScrollIntoView(last);
        _chatScrollViewer?.ScrollToEnd();
    }

    private static ScrollViewer? FindDescendantScrollViewer(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is ScrollViewer sv) return sv;
            var nested = FindDescendantScrollViewer(child);
            if (nested != null) return nested;
        }
        return null;
    }
}
