using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Nri.AdminClient.ViewModels;

namespace Nri.AdminClient.Views;

public partial class MainShellWindow : Window
{
    private readonly Dictionary<string, DetachedPanelWindow> _panelWindows = new Dictionary<string, DetachedPanelWindow>();
    private bool _isShuttingDown;
    private bool _panelSubscriptionsAttached;

    public MainShellWindow()
    {
        InitializeComponent();
        DataContext = new AdminMainViewModel();
        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    private AdminMainViewModel ViewModel => (AdminMainViewModel)DataContext;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_panelSubscriptionsAttached)
        {
            return;
        }

        foreach (var panel in ViewModel.WorkspacePanels)
        {
            panel.PropertyChanged += OnPanelPropertyChanged;
        }
        _panelSubscriptionsAttached = true;

        SynchronizeDetachedWindows();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        _isShuttingDown = true;
        foreach (var window in _panelWindows.Values.ToList())
        {
            window.BeginProgrammaticClose(attachPanelBack: false);
        }
        _panelWindows.Clear();
        if (_panelSubscriptionsAttached)
        {
            foreach (var panel in ViewModel.WorkspacePanels)
            {
                panel.PropertyChanged -= OnPanelPropertyChanged;
            }
            _panelSubscriptionsAttached = false;
        }
        ViewModel.Shutdown();
    }

    private void OnPanelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isShuttingDown)
        {
            return;
        }

        if (e.PropertyName == nameof(WorkspacePanelDescriptor.IsDetached) || e.PropertyName == nameof(WorkspacePanelDescriptor.IsVisible))
        {
            SynchronizeDetachedWindows();
        }
    }

    private void OnDetachedWindowClosed(object? sender, System.EventArgs e)
    {
        if (sender is DetachedPanelWindow window)
        {
            _panelWindows.Remove(window.PanelId);
        }
    }

    private void SynchronizeDetachedWindows()
    {
        foreach (var panel in ViewModel.WorkspacePanels)
        {
            if (panel.IsDetached && panel.IsVisible)
            {
                if (!_panelWindows.ContainsKey(panel.PanelId))
                {
                    var template = (DataTemplate)FindResource(panel.PanelId + "Template");
                    var window = new DetachedPanelWindow(ViewModel, panel, template) { Owner = this };
                    window.Closed += OnDetachedWindowClosed;
                    _panelWindows[panel.PanelId] = window;
                    window.Show();
                }
            }
            else if (_panelWindows.TryGetValue(panel.PanelId, out var existingWindow))
            {
                _panelWindows.Remove(panel.PanelId);
                if (!existingWindow.IsCloseInProgress)
                {
                    existingWindow.BeginProgrammaticClose(attachPanelBack: false);
                }
            }
        }
    }
}
