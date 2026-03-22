using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Nri.AdminClient.ViewModels;

namespace Nri.AdminClient.Views;

public sealed class DetachedPanelWindow : Window
{
    private readonly AdminMainViewModel _viewModel;
    private readonly WorkspacePanelDescriptor _panel;

    public bool IsProgrammaticClose { get; set; }

    public DetachedPanelWindow(AdminMainViewModel viewModel, WorkspacePanelDescriptor panel, DataTemplate template)
    {
        _viewModel = viewModel;
        _panel = panel;

        Title = $"НРИ / Панель Админа — {panel.Title}";
        Width = panel.WindowWidth;
        Height = panel.WindowHeight;
        Left = panel.WindowLeft;
        Top = panel.WindowTop;
        MinWidth = 720;
        MinHeight = 480;

        var bg = Application.Current.TryFindResource("BgBrush") as Brush;
        var fg = Application.Current.TryFindResource("TextBrush") as Brush;

        if (bg != null) Background = bg;
        if (fg != null) Foreground = fg;

        DataContext = viewModel;
        Content = new ContentControl
        {
            Content = panel,
            ContentTemplate = template
        };

        Closing += OnClosing;
        Closed += OnClosed;
        LocationChanged += (_, _) => PersistBounds();
        SizeChanged += (_, _) => PersistBounds();
    }

    public string PanelId => _panel.PanelId;

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        PersistBounds();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        PersistBounds();

        if (!IsProgrammaticClose)
        {
            _viewModel.AttachWorkspacePanelCommand.Execute(_panel.PanelId);
        }
    }

    private void PersistBounds()
    {
        if (!double.IsNaN(Left) && !double.IsNaN(Top) && Width > 0 && Height > 0)
        {
            _viewModel.UpdatePanelWindowBounds(_panel.PanelId, Left, Top, Width, Height);
        }
    }
}
