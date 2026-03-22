using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Nri.AdminClient.ViewModels;

namespace Nri.AdminClient.Views;

public partial class MainShellWindow : Window
{
    private readonly Dictionary<string, DetachedPanelWindow> _panelWindows = new();
    private bool _isShuttingDown;

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
        foreach (var panel in ViewModel.WorkspacePanels)
        {
            panel.PropertyChanged += OnPanelPropertyChanged;
        }

        SynchronizeDetachedWindows();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        _isShuttingDown = true;

        foreach (var window in _panelWindows.Values.ToList())
        {
            window.IsProgrammaticClose = true;
            window.Close();
        }

        _panelWindows.Clear();
        ViewModel.Shutdown();
    }

    private void OnPanelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WorkspacePanelDescriptor.IsDetached) ||
            e.PropertyName == nameof(WorkspacePanelDescriptor.IsVisible))
        {
            SynchronizeDetachedWindows();
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
                    var window = new DetachedPanelWindow(ViewModel, panel, template)
                    {
                        Owner = this
                    };

                    window.Closed += (_, _) =>
                    {
                        _panelWindows.Remove(panel.PanelId);

                        if (!_isShuttingDown && panel.IsDetached)
                        {
                            panel.IsDetached = false;
                        }
                    };

                    _panelWindows[panel.PanelId] = window;
                    window.Show();
                }
            }
            else if (_panelWindows.TryGetValue(panel.PanelId, out var existingWindow))
            {
                _panelWindows.Remove(panel.PanelId);
                existingWindow.IsProgrammaticClose = true;
                existingWindow.Close();
            }
        }
    }
}
