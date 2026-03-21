using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Nri.AdminClient.ViewModels;

namespace Nri.AdminClient.Views;

public sealed class DetachedPanelWindow : Window
{
    private bool _isCloseInProgress;
    private bool _isProgrammaticClose;
    private bool _attachOnClose = true;
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
        Closed += OnClosed;
        LocationChanged += (_, _) => PersistBounds();
        SizeChanged += (_, _) => PersistBounds();
    }

    public string PanelId => _panel.PanelId;
    public bool IsCloseInProgress => _isCloseInProgress;

    public void BeginProgrammaticClose(bool attachPanelBack)
    {
        if (_isCloseInProgress)
        {
            return;
        }

        _isProgrammaticClose = true;
        _attachOnClose = attachPanelBack;
        Close();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_isCloseInProgress)
        {
            return;
        }

        _isCloseInProgress = true;
        PersistBounds();

        if (!_isProgrammaticClose && _attachOnClose)
        {
            _viewModel.AttachWorkspacePanelCommand.Execute(_panel.PanelId);
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        PersistBounds();
    }

    private void PersistBounds()
    {
        if (!double.IsNaN(Left) && !double.IsNaN(Top) && Width > 0 && Height > 0)
        {
            _viewModel.UpdatePanelWindowBounds(_panel.PanelId, Left, Top, Width, Height);
        }
    }
}
