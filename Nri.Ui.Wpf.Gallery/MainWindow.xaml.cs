using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Nri.Ui.Wpf.Patterns;

namespace Nri.Ui.Wpf.Gallery
{
    public partial class MainWindow : Window
    {
        public ICommand ClearFiltersCommand { get; }
        public ICommand ReferenceSearchCommand { get; }
        public ICommand ConfirmArchiveCommand { get; }
        public ICommand CancelArchiveCommand { get; }

        public MainWindow()
        {
            ClearFiltersCommand = new DelegateCommand(() => { PatternSearch.Text = ""; PatternTypeFilter.SelectedIndex = 0; });
            ReferenceSearchCommand = new DelegateCommand(() => ReferenceToast.IsOpen = true);
            ConfirmArchiveCommand = new DelegateCommand(() => { ConfirmationDialog.IsOpen = false; ConfirmationToast.IsOpen = true; });
            CancelArchiveCommand = new DelegateCommand(() => ConfirmationDialog.IsOpen = false);
            InitializeComponent();
            DataContext = this;
            Loaded += (_, __) => ApplyStartupArguments();
        }

        private void ApplyStartupArguments()
        {
            foreach (var argument in Environment.GetCommandLineArgs())
            {
                if (argument.StartsWith("--viewport=", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = argument.Substring(11).Split('x');
                    if (parts.Length == 2 && double.TryParse(parts[0], out var width) && double.TryParse(parts[1], out var height)) { Width = width; Height = height; }
                }
                else if (argument.StartsWith("--scale=", StringComparison.OrdinalIgnoreCase) && double.TryParse(argument.Substring(8), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var scale))
                {
                    GalleryTabs.LayoutTransform = new ScaleTransform(scale, scale);
                }
                else if (argument.StartsWith("--tab=", StringComparison.OrdinalIgnoreCase) && int.TryParse(argument.Substring(6), out var tab))
                {
                    GalleryTabs.SelectedIndex = Math.Max(0, Math.Min(GalleryTabs.Items.Count - 1, tab));
                }
                else if (argument.Equals("--longtext", StringComparison.OrdinalIgnoreCase))
                {
                    LongTextToggle.IsChecked = true;
                }
            }
        }

        private void DensitySelector_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            GalleryTabs.FontSize = DensitySelector.SelectedIndex == 0 ? 13 : 15;
        }

        private void ViewportSelector_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            if (ViewportSelector.SelectedIndex == 0) { Width = 1366; Height = 768; }
            else { Width = 1600; Height = 960; }
        }

        private void ScaleSelector_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            var scale = ScaleSelector.SelectedIndex == 1 ? 1.25 : ScaleSelector.SelectedIndex == 2 ? 1.5 : 1.0;
            GalleryTabs.LayoutTransform = new ScaleTransform(scale, scale);
        }

        private void LongTextToggle_OnChanged(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded || BodyTextSample == null) return;
            BodyTextSample.Text = LongTextToggle.IsChecked == true
                ? "Основной текст может быть длинным: пользователь должен понимать назначение раздела, последствия действия и следующий безопасный шаг даже при узком окне и увеличенном масштабе интерфейса."
                : "Основной текст объясняет содержание простыми словами и помогает принять решение.";
        }

        private void SetState(UIElement visible)
        {
            StateEmpty.Visibility = Visibility.Collapsed;
            StateLoading.Visibility = Visibility.Collapsed;
            StateError.Visibility = Visibility.Collapsed;
            StatePopulated.Visibility = Visibility.Collapsed;
            visible.Visibility = Visibility.Visible;
        }

        private void StateEmpty_OnClick(object sender, RoutedEventArgs e) => SetState(StateEmpty);
        private void StateLoading_OnClick(object sender, RoutedEventArgs e) => SetState(StateLoading);
        private void StateError_OnClick(object sender, RoutedEventArgs e) => SetState(StateError);
        private void StatePopulated_OnClick(object sender, RoutedEventArgs e) => SetState(StatePopulated);
        private void ArchiveButton_OnClick(object sender, RoutedEventArgs e) => ConfirmationDialog.IsOpen = true;
        private void OpenInspector_OnClick(object sender, RoutedEventArgs e) => EntityPattern.IsInspectorOpen = true;

        private sealed class DelegateCommand : ICommand
        {
            private readonly Action _execute;
            public DelegateCommand(Action execute) { _execute = execute; }
            public bool CanExecute(object parameter) => true;
            public void Execute(object parameter) => _execute();
            public event EventHandler CanExecuteChanged { add { } remove { } }
        }
    }
}
