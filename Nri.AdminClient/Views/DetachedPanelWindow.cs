using System;
using System.Windows;
using System.Windows.Controls;
using Nri.AdminClient.ViewModels;

namespace Nri.AdminClient.Views;

public sealed class DetachedPanelWindow : Window
{
    private bool _suppressAttachOnClose;
    private readonly AdminMainViewModel _viewModel;
    private readonly WorkspacePanelDescriptor _panel;

    public DetachedPanelWindow(AdminMainViewModel viewModel, WorkspacePanelDescriptor panel, DataTemplate template)
    {
        _viewModel = viewModel;
        _panel = panel;
        Title = $"NRI / Admin — {panel.Title}";
        Width = panel.WindowWidth;
        Height = panel.WindowHeight;
        Left = panel.WindowLeft;
        Top = panel.WindowTop;
        MinWidth = 720;
        MinHeight = 480;
        DataContext = viewModel;
        Content = new ContentControl { Content = panel, ContentTemplate = template };

        Closing += OnClosing;
        LocationChanged += (_, _) => PersistBounds();
        SizeChanged += (_, _) => PersistBounds();
    }

    public void CloseWithoutAttach()
    {
        _suppressAttachOnClose = true;
        Close();
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        PersistBounds();
        if (!_suppressAttachOnClose)
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
