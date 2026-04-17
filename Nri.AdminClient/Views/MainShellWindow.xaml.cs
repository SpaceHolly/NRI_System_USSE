using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Nri.AdminClient.Diagnostics;
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
        ClientLogService.Instance.Info("ui.admin.root-scroll.initialized");
        foreach (var panel in ViewModel.WorkspacePanels)
        {
            panel.PropertyChanged += OnPanelPropertyChanged;
        }

        SynchronizeDetachedWindows();
        ClientLogService.Instance.Info("ui.admin.layout.people-table.fixed");
        ClientLogService.Instance.Info("ui.admin.layout.dice-panel.separate=true");
        ClientLogService.Instance.Info("ui.admin.sections.reachable=true");
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        _isShuttingDown = true;
        ClientLogService.Instance.Info("Main window closing (Admin)");

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
                    var templateKey = panel.PanelId + "Template";
                    var template = (DataTemplate)FindResource(templateKey);
                    ClientLogService.Instance.Info($"ui-panel template-load panel={panel.PanelId} template={templateKey}");
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
                    ClientLogService.Instance.Info($"ui-panel action=open panel={panel.PanelId}");
                    ClientLogService.Instance.Info($"ui-panel scroll-support panel={panel.PanelId} enabled=true");
                    window.Show();
                }
            }
            else if (_panelWindows.TryGetValue(panel.PanelId, out var existingWindow))
            {
                _panelWindows.Remove(panel.PanelId);
                ClientLogService.Instance.Info($"ui-panel action=close panel={panel.PanelId}");
                existingWindow.IsProgrammaticClose = true;
                existingWindow.Close();
            }
        }
    }
}
