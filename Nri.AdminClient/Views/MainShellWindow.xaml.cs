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

    private void OnAdminPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox box)
            ViewModel.PasswordText = box.Password;
    }

    private void OnAdminOldPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox box)
            ViewModel.OldPasswordText = box.Password;
    }

    private void OnAdminNewPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox box)
            ViewModel.NewPasswordText = box.Password;
    }

    private void OnResetPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox box)
        {
            ViewModel.SetResetPasswordTextFromUi(box.Password);
        }
    }

    private void OnShellNavigationButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: AdminNavigationItem item }) return;
        if (ViewModel.SelectNavigationItemCommand.CanExecute(item))
        {
            ViewModel.SelectNavigationItemCommand.Execute(item);
            if (item.TargetViewKey == "admin.characters")
            {
                ForceSelectCharactersSection();
            }
            else
            {
                SyncMainSectionTabSelection();
            }
        }
    }

    private void OnOpenCharactersRouteClick(object sender, RoutedEventArgs e)
    {
        ForceSelectCharactersSection();
    }

    private void OnOpenClassesRouteClick(object sender, RoutedEventArgs e)
    {
        ForceSelectSection("admin.classes");
    }

    private void ForceSelectCharactersSection()
    {
        ForceSelectSection("admin.characters");
    }

    private void ForceSelectSection(string section)
    {
        ViewModel.SelectedSection = section;
        SelectMainSectionTab(section);
        Dispatcher.BeginInvoke(new System.Action(() =>
        {
            ViewModel.SelectedSection = section;
            SelectMainSectionTab(section);
        }));
    }

    private void SyncMainSectionTabSelection()
    {
        SelectMainSectionTab(ViewModel.SelectedSection);
    }

    private void SelectMainSectionTab(string section)
    {
        if (string.IsNullOrWhiteSpace(section))
            return;

        var matchingTab = MainSectionTabControl.Items
            .OfType<TabItem>()
            .FirstOrDefault(item => string.Equals(item.Tag as string, section, System.StringComparison.Ordinal));

        if (matchingTab != null && !ReferenceEquals(MainSectionTabControl.SelectedItem, matchingTab))
        {
            MainSectionTabControl.SelectedItem = matchingTab;
            matchingTab.IsSelected = true;
        }

        if (matchingTab != null)
        {
            var index = MainSectionTabControl.Items.IndexOf(matchingTab);
            if (index >= 0 && MainSectionTabControl.SelectedIndex != index)
                MainSectionTabControl.SelectedIndex = index;
        }

        if (!Equals(MainSectionTabControl.SelectedValue, section))
            MainSectionTabControl.SelectedValue = section;

        MainSectionTabControl.ApplyTemplate();
        MainSectionTabControl.UpdateLayout();
        var selectedItemType = MainSectionTabControl.SelectedItem?.GetType().Name ?? "<null>";
        var selectedContentType = MainSectionTabControl.SelectedContent?.GetType().Name ?? "<null>";
        ClientLogService.Instance.Debug($"ui.admin.main-section.visual-sync section={section} selectedValue={MainSectionTabControl.SelectedValue} selectedIndex={MainSectionTabControl.SelectedIndex} selectedItem={selectedItemType} selectedContent={selectedContentType}");
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ClientLogService.Instance.Info("ui.admin.root-scroll.initialized");
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        foreach (var panel in ViewModel.WorkspacePanels)
        {
            panel.PropertyChanged += OnPanelPropertyChanged;
        }

        SynchronizeDetachedWindows();
        ClientLogService.Instance.Info("ui.admin.layout.people-table.fixed");
        ClientLogService.Instance.Info("ui.admin.layout.dice-panel.separate=true");
        ClientLogService.Instance.Info("ui.admin.sections.reachable=true");
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(AdminMainViewModel.SelectedSection)) return;
        var section = ViewModel.SelectedSection;
        Dispatcher.BeginInvoke(new System.Action(() => SelectMainSectionTab(section)), System.Windows.Threading.DispatcherPriority.ContextIdle);
        Dispatcher.BeginInvoke(new System.Action(() => SelectMainSectionTab(section)), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        _isShuttingDown = true;
        ClientLogService.Instance.Info("Main window closing (Admin)");
        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;

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
