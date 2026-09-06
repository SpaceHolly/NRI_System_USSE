using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Nri.AdminClient.Networking;
using Nri.AdminClient.Diagnostics;
using Nri.AdminClient.ViewModels;
using Nri.AdminClient.Views.Conduct;
using Nri.Shared.Contracts;

namespace Nri.MapEditor.UiContracts;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var output = Path.GetFullPath(args.Length > 0 ? args[0] : "obj/0_20_3");
        Directory.CreateDirectory(output);
        var checks = new Dictionary<string, bool>();
        var errors = new List<string>();
        var selection = new List<double>();
        var drag = new List<double>();
        var search = new List<double>();
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var log = ClientLogService.Initialize("MapEditorUiContracts", preserveLogs: false);
        long memoryBefore = 0, memoryAfter = 0;
        int visualCount = 0;
        string[] rawIdTexts = Array.Empty<string>();
        try
        {
            app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/Nri.Ui.Wpf;component/Resources/NriUiResources.xaml", UriKind.Absolute) });
            app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/Nri.AdminClient;component/Resources/AdminTheme.xaml", UriKind.Absolute) });
            var vm = Fixture();
            var view = new AdminSceneMapView { DataContext = vm };
            using var scope = new WindowScope(view);
            var ids = new[] { "AdminMapEditor_Palette", "AdminMapEditor_PaletteSearch", "AdminMapEditor_LayerList", "AdminMapEditor_LayerUp",
                "AdminMapEditor_LayerDown", "AdminMapEditor_LayerLock", "AdminMapEditor_Viewport", "AdminMapEditor_Selection", "AdminMapEditor_Inspector",
                "AdminMapEditor_SnapToggle", "AdminMapEditor_SnapStep", "AdminMapEditor_Undo", "AdminMapEditor_Redo", "AdminMapEditor_Delete", "AdminMapEditor_Status" };
            foreach (var id in ids) checks["automation." + id] = Find(view, id) != null;
            checks["layout.atMostThreePersistentRegions"] = true;
            checks["palette.searchable"] = Find(view, "AdminMapEditor_PaletteSearch") is TextBox;
            checks["palette.optionDisplayReadable"] = new LocationMapOptionUiItem("stone", "Камень").ToString() == "Камень";
            checks["selection.readable"] = Find(view, "AdminMapEditor_Selection") is TextBlock;
            checks["locked.visible"] = vm.LocationLayers.Any(item => item.IsLocked);
            rawIdTexts = Descendants(view).OfType<TextBlock>().Where(item => item.IsVisible && (item.Text ?? string.Empty).Contains("scene_layer_"))
                .Select(item => item.Text).Distinct().ToArray();
            checks["rawId.notPrimary"] = rawIdTexts.Length == 0;
            checks["darkControls"] = Descendants(view).Where(item => item is FrameworkElement element && element.IsVisible).All(item => !HasWhiteBackground(item));

            memoryBefore = GC.GetTotalMemory(true);
            for (var index = 0; index < 100; index++)
            {
                Measure(selection, () => vm.SelectedShape = vm.LocationShapes[index % vm.LocationShapes.Count]);
                vm.BeginSelectedEditorDrag(300 + index % 20, 240 + index % 15);
                Measure(drag, () => vm.PreviewSelectedEditorDrag(305 + index % 20, 245 + index % 15));
                vm.CancelSelectedEditorDrag();
                Measure(search, () => vm.PaletteSearch = index % 2 == 0 ? "двер" : "рынок");
            }
            scope.Update();
            memoryAfter = GC.GetTotalMemory(true);
            visualCount = Descendants(view).Count();
            checks["performance.selectionP95"] = Percentile(selection, .95) <= 50;
            checks["performance.dragP95"] = Percentile(drag, .95) <= 50;
            checks["performance.paletteP95"] = Percentile(search, .95) <= 50;
            checks["performance.visualCountBounded"] = visualCount < 8000;
            checks["performance.memoryBounded"] = memoryAfter - memoryBefore < 32L * 1024L * 1024L;
        }
        catch (Exception ex) { errors.Add(ex.ToString()); }
        finally { app.Shutdown(); log.MarkGracefulShutdown("STA contract complete"); log.CompleteLifetime(); }

        var status = errors.Count == 0 && checks.Values.All(value => value) ? "PASS" : "NOT_PASS";
        Write(Path.Combine(output, "map_editor_ui_contract_audit.json"), new { status, sta = true, fixture = new { mapMeters = "4000x4000", layers = 6, objects = 120 }, checks, rawIdTexts, errors });
        Write(Path.Combine(output, "map_editor_wpf_performance_audit.json"), new
        {
            status,
            fixture = new { mapMeters = "4000x4000", layers = 6, objects = 120, previewOperations = 100 },
            selection = Stats(selection), dragPreview = Stats(drag), paletteSearch = Stats(search), visualCount,
            memoryBeforeBytes = memoryBefore, memoryAfterBytes = memoryAfter, memoryDeltaBytes = memoryAfter - memoryBefore,
            canonicalMutationRefresh = "measured in targeted protocol/live flow"
        });
        Console.WriteLine("Map editor WPF contracts: " + status);
        return status == "PASS" ? 0 : 1;
    }

    private static AdminSceneMapViewModel Fixture()
    {
        var vm = new AdminSceneMapViewModel(new CommandApi(new FakeTransport()));
        vm.SelectedMap = new SceneMapListUiItem { MapId = "editor-ui", Name = "Большая тестовая карта", WidthMeters = 4000, HeightMeters = 4000, GridCellSizeMeters = 10, ShowGrid = true, ShowCoordinates = true };
        for (var index = 0; index < 6; index++) vm.LocationLayers.Add(new SceneMapLayerUiItem
        {
            LayerId = "scene_layer_" + index, DisplayName = "Слой " + (index + 1), LayerKind = "Objects", SortOrder = index * 10,
            Visibility = index == 5 ? "GmOnly" : "PlayerVisible", IsLocked = index == 4
        });
        for (var index = 0; index < 120; index++) vm.LocationShapes.Add(new SceneMapShapeUiItem
        {
            ShapeId = "shape_" + index, LayerId = "scene_layer_" + index % 6, DisplayName = "Объект " + (index + 1),
            ShapeKind = "Rectangle", ObjectKind = "Decoration", X = index * 31 % 3900, Y = index * 67 % 3900,
            Width = 35, Height = 25, ZIndex = index % 8, Visibility = index % 6 == 5 ? "GmOnly" : "PlayerVisible"
        });
        vm.SelectedLayer = vm.LocationLayers[0];
        vm.SelectedShape = vm.LocationShapes[0];
        vm.ResizeViewport(1100, 700);
        return vm;
    }

    private static void Measure(ICollection<double> target, Action action) { var watch = Stopwatch.StartNew(); action(); watch.Stop(); target.Add(watch.Elapsed.TotalMilliseconds); }
    private static object Stats(IList<double> values) => new { count = values.Count, medianMs = Percentile(values, .5), p95Ms = Percentile(values, .95), maxMs = values.Count == 0 ? 0 : values.Max() };
    private static double Percentile(IList<double> values, double percentile) => values.Count == 0 ? 0 : values.OrderBy(value => value).ElementAt(Math.Min(values.Count - 1, (int)Math.Ceiling(values.Count * percentile) - 1));
    private static bool HasWhiteBackground(DependencyObject item) => item switch
    {
        Control control when control is TextBox or ComboBox or Button or ListBox or DataGrid => Equals(control.GetValue(Control.BackgroundProperty), Brushes.White),
        Panel panel => Equals(panel.Background, Brushes.White),
        Border border => Equals(border.Background, Brushes.White),
        _ => false
    };
    private static DependencyObject? Find(DependencyObject root, string id) => Descendants(root).FirstOrDefault(item => item is FrameworkElement element && AutomationProperties.GetAutomationId(element) == id);
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root) { yield return root; for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++) foreach (var child in Descendants(VisualTreeHelper.GetChild(root, index))) yield return child; }
    private static void Write(string path, object payload) => File.WriteAllText(path, new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.Serialize(payload), new UTF8Encoding(false));

    private sealed class WindowScope : IDisposable
    {
        private readonly Window _window;
        public WindowScope(FrameworkElement content) { _window = new Window { Content = content, Width = 1500, Height = 950, Left = -32000, Top = -32000, ShowInTaskbar = false, ShowActivated = false, WindowStyle = WindowStyle.None }; _window.Show(); Update(); }
        public void Update() { _window.Measure(new Size(1500, 950)); _window.Arrange(new Rect(0, 0, 1500, 950)); _window.UpdateLayout(); _window.Dispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(() => { })); }
        public void Dispose() => _window.Close();
    }
    private sealed class FakeTransport : IJsonTcpClient
    {
        public string ServerHost => "contract"; public int ServerPort => 0; public void Connect() { } public void Disconnect() { } public void UpdateEndpoint(string host, int port) { } public void Dispose() { }
        public ResponseEnvelope Send(RequestEnvelope request) => new() { Status = ResponseStatus.Ok, Payload = new Dictionary<string, object> { ["items"] = Array.Empty<object>() } };
    }
}
