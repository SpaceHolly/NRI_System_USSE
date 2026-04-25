using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Nri.ChatClient.Diagnostics;
using Nri.ChatClient.ViewModels;

namespace Nri.ChatClient.Views;

public partial class MainShellWindow : Window
{
    private ScrollViewer? _timelineScrollViewer;
    private bool _userAtLatest = true;

    public MainShellWindow()
    {
        InitializeComponent();
        var vm = new ChatClientMainViewModel();
        DataContext = vm;

        Loaded += (_, _) =>
        {
            ClientLogService.Instance.Info("tab.load.render main-window.loaded");
            _timelineScrollViewer = FindDescendantScrollViewer(TimelineList);
            if (_timelineScrollViewer != null) _timelineScrollViewer.ScrollChanged += OnTimelineScrollChanged;
            vm.MergedTimelineRows.CollectionChanged += OnTimelineRowsChanged;
            vm.PropertyChanged += OnVmPropertyChanged;
            ScrollToLatest(force: true);
        };

        Unloaded += (_, _) =>
        {
            if (_timelineScrollViewer != null) _timelineScrollViewer.ScrollChanged -= OnTimelineScrollChanged;
            vm.MergedTimelineRows.CollectionChanged -= OnTimelineRowsChanged;
            vm.PropertyChanged -= OnVmPropertyChanged;
        };
    }

    private void PasswordInput_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is ChatClientMainViewModel vm && sender is PasswordBox box)
        {
            vm.PasswordText = box.Password;
        }
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ChatClientMainViewModel.IsAuthenticated))
        {
            ScrollToLatest(force: true);
        }
    }

    private void OnTimelineRowsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_userAtLatest)
        {
            ScrollToLatest(force: false);
        }
    }

    private void OnTimelineScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        const double tolerance = 16d;
        _userAtLatest = e.VerticalOffset >= e.ExtentHeight - e.ViewportHeight - tolerance;
    }

    private void ScrollToLatest(bool force)
    {
        if (TimelineList.Items.Count == 0)
        {
            return;
        }

        if (!force && !_userAtLatest)
        {
            return;
        }

        var last = TimelineList.Items[TimelineList.Items.Count - 1];
        TimelineList.ScrollIntoView(last);
        _timelineScrollViewer?.ScrollToEnd();
    }

    private static ScrollViewer? FindDescendantScrollViewer(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is ScrollViewer sv)
            {
                return sv;
            }

            var nested = FindDescendantScrollViewer(child);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }
}
