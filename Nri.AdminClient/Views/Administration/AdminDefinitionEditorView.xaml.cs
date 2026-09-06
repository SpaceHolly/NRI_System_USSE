using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Nri.AdminClient.ViewModels;

namespace Nri.AdminClient.Views.Administration;

public partial class AdminDefinitionEditorView : UserControl
{
    private const double DefaultCollectionWidth = 340;
    private const double MinimumCollectionWidth = 280;
    private const double MaximumCollectionWidth = 460;
    private static readonly string CollectionWidthPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Nri.AdminClient",
        "definition-editor.collection-width.txt");
    private bool _layoutPolicyInitialized;
    private double _previousLayoutWidth;

    public AdminDefinitionEditorView()
    {
        InitializeComponent();
        AdminDefinitionEditorHost.RecordListWidth = new GridLength(ReadCollectionWidth());
        Loaded += OnLoaded;
        SizeChanged += OnSizeChanged;
        DataContextChanged += OnDataContextChanged;
        Unloaded += OnUnloaded;
    }

    private Window? HostWindow { get; set; }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var window = Window.GetWindow(this);
        if (!ReferenceEquals(HostWindow, window))
        {
            if (HostWindow != null)
                HostWindow.Closing -= OnHostWindowClosing;
            HostWindow = window;
            if (HostWindow != null)
                HostWindow.Closing += OnHostWindowClosing;
        }
        if (DataContext is AdminDefinitionEditorViewModel vm && vm.Profiles.Count == 0)
            vm.Refresh();
        ApplyNarrowLayoutPolicy();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e) => ApplyNarrowLayoutPolicy();

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e) => ApplyNarrowLayoutPolicy();

    private void ApplyNarrowLayoutPolicy()
    {
        if (ActualWidth <= 0 || DataContext is not AdminDefinitionEditorViewModel vm)
            return;

        var isNarrow = ActualWidth <= 1400;
        var enteredNarrowLayout = isNarrow &&
            (!_layoutPolicyInitialized || _previousLayoutWidth > 1400);
        _layoutPolicyInitialized = true;
        _previousLayoutWidth = ActualWidth;
        if (!enteredNarrowLayout)
            return;

        vm.IsInspectorOpen = false;
        // Preserve the XAML binding on the host. Assigning the dependency
        // property here would replace that binding with a permanent local value.
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        SaveCollectionWidth();
        if (HostWindow != null)
            HostWindow.Closing -= OnHostWindowClosing;
    }

    private void OnHostWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e) => SaveCollectionWidth();

    private void SaveCollectionWidth()
    {
        if (AdminDefinitionEditorHost.Template?.FindName("RecordColumn", AdminDefinitionEditorHost) is not ColumnDefinition column)
            return;
        var width = ClampCollectionWidth(column.ActualWidth);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CollectionWidthPath)!);
            File.WriteAllText(CollectionWidthPath, width.ToString(CultureInfo.InvariantCulture));
        }
        catch
        {
            // Layout persistence is optional and must not affect editor operation.
        }
    }

    private static double ReadCollectionWidth()
    {
        try
        {
            if (File.Exists(CollectionWidthPath)
                && double.TryParse(File.ReadAllText(CollectionWidthPath), NumberStyles.Float, CultureInfo.InvariantCulture, out var width))
                return ClampCollectionWidth(width);
        }
        catch { }
        return DefaultCollectionWidth;
    }

    private static double ClampCollectionWidth(double width)
        => Math.Max(MinimumCollectionWidth, Math.Min(MaximumCollectionWidth, width));
}
