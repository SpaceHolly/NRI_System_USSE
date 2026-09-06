using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Nri.AdminClient.ViewModels;
using Nri.PlayerClient.ViewModels;
using Nri.Shared.Contracts;
using AdminApi = Nri.AdminClient.Networking.CommandApi;
using AdminTransport = Nri.AdminClient.Networking.IJsonTcpClient;
using PlayerApi = Nri.PlayerClient.Networking.CommandApi;
using PlayerTransport = Nri.PlayerClient.Networking.IJsonTcpClient;
using AdminLog = Nri.AdminClient.Diagnostics.ClientLogService;
using PlayerLog = Nri.PlayerClient.Diagnostics.ClientLogService;

namespace Nri.MapViewport.UiContracts;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var output = Path.GetFullPath(args.Length > 0 ? args[0] : "obj/0_20_2/map_viewport_ui_contract_audit.json");
        Directory.CreateDirectory(Path.GetDirectoryName(output) ?? ".");
        var checks = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        var errors = new List<string>();
        var bindingErrors = new List<string>();
        var listener = new BindingListener(bindingErrors);
        var adminLog = AdminLog.Initialize("MapViewportUiContract.Admin", preserveLogs: false);
        var playerLog = PlayerLog.Initialize("MapViewportUiContract.Player", preserveLogs: false);
        PresentationTraceSources.DataBindingSource.Listeners.Add(listener);
        PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Warning;
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        try
        {
            LoadTheme(app, "Nri.AdminClient", "AdminTheme.xaml");
            CheckAdmin(checks);
            LoadTheme(app, "Nri.PlayerClient", "PlayerTheme.xaml");
            CheckPlayer(checks);
        }
        catch (Exception ex) { errors.Add(ex.ToString()); }
        finally
        {
            PresentationTraceSources.DataBindingSource.Listeners.Remove(listener);
            app.Shutdown();
            adminLog.MarkGracefulShutdown("STA contract complete");
            adminLog.CompleteLifetime();
            playerLog.MarkGracefulShutdown("STA contract complete");
            playerLog.CompleteLifetime();
        }

        var status = errors.Count == 0 && checks.Count >= 20 && checks.Values.All(value => value) ? "PASS" : "NOT_PASS";
        var audit = new
        {
            status,
            staThread = true,
            sharedTransformType = "Nri.Shared.Utilities.MapViewportState",
            wheelSimulation = "ViewModel ZoomAtPixel at cursor anchor",
            dragSimulation = "ViewModel PanViewport delta",
            checks,
            bindingWarnings = bindingErrors.Distinct().Take(50).ToArray(),
            errors
        };
        File.WriteAllText(output, new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.Serialize(audit), new UTF8Encoding(false));
        Console.WriteLine("Map viewport UI contracts: " + status);
        return status == "PASS" ? 0 : 1;
    }

    private static void CheckAdmin(IDictionary<string, bool> checks)
    {
        var vm = new AdminSceneMapViewModel(new AdminApi(new AdminFakeTransport()));
        vm.SelectedMap = new SceneMapListUiItem { MapId = "ui-contract", Name = "Карта 4 км", WidthMeters = 4000, HeightMeters = 4000, GridCellSizeMeters = 5, ShowGrid = true, ShowCoordinates = true };
        vm.ResizeViewport(1100, 650);
        var view = new Nri.AdminClient.Views.Conduct.AdminSceneMapView { DataContext = vm };
        using var scope = new WindowScope(view);
        checks["admin.materialized"] = true;
        var adminViewport = Find(view, "AdminSceneMap_Viewport") as FrameworkElement;
        checks["admin.viewport"] = adminViewport != null;
        checks["admin.viewportAutomationPeer"] = adminViewport != null && (FrameworkElementAutomationPeer.FromElement(adminViewport) ?? FrameworkElementAutomationPeer.CreatePeerForElement(adminViewport)) != null;
        checks["admin.zoomIn"] = ButtonCanExecute(view, "MapViewport_ZoomIn");
        checks["admin.zoomOut"] = ButtonCanExecute(view, "MapViewport_ZoomOut");
        checks["admin.fit"] = ButtonCanExecute(view, "MapViewport_Fit");
        checks["admin.reset"] = ButtonCanExecute(view, "MapViewport_Reset");
        checks["admin.zoomIndicator"] = Find(view, "MapViewport_ZoomIndicator") is TextBlock;
        checks["admin.coordinateIndicator"] = Find(view, "MapViewport_CoordinateIndicator") is TextBlock;
        checks["admin.gridToggle"] = Find(view, "MapViewport_GridToggle") is CheckBox;
        var before = vm.ZoomIndicator;
        vm.ZoomAtPixel(420, 280, 120);
        vm.UpdateCursor(420, 280);
        checks["admin.wheelChangesZoom"] = vm.ZoomIndicator != before;
        checks["admin.cursorReadable"] = vm.CoordinateIndicator.Contains("X=") && vm.CoordinateIndicator.Contains("Y=");
        var afterZoom = vm.ZoomIndicator;
        vm.PanViewport(-80, 45);
        checks["admin.panKeepsZoom"] = vm.ZoomIndicator == afterZoom;
        vm.FitToMapCommand.Execute(null);
        checks["admin.fitFinite"] = vm.CanZoomIn || vm.CanZoomOut;
        vm.ResetViewCommand.Execute(null);
        checks["admin.resetFinite"] = !string.IsNullOrWhiteSpace(vm.ZoomIndicator);
    }

    private static void CheckPlayer(IDictionary<string, bool> checks)
    {
        var vm = new PlayerSceneMapViewModel(new PlayerApi(new PlayerFakeTransport()), () => string.Empty);
        vm.ResizeViewport(1100, 650);
        var view = new Nri.PlayerClient.Views.Maps.PlayerSceneMapView { DataContext = vm };
        using var scope = new WindowScope(view);
        checks["player.materialized"] = true;
        var playerViewport = Find(view, "PlayerSceneMap_Viewport") as FrameworkElement;
        checks["player.viewport"] = playerViewport != null;
        checks["player.viewportAutomationPeer"] = playerViewport != null && (FrameworkElementAutomationPeer.FromElement(playerViewport) ?? FrameworkElementAutomationPeer.CreatePeerForElement(playerViewport)) != null;
        checks["player.zoomIn"] = ButtonCanExecute(view, "MapViewport_ZoomIn");
        checks["player.zoomOut"] = ButtonCanExecute(view, "MapViewport_ZoomOut");
        checks["player.fit"] = ButtonCanExecute(view, "MapViewport_Fit");
        checks["player.reset"] = ButtonCanExecute(view, "MapViewport_Reset");
        checks["player.zoomIndicator"] = Find(view, "MapViewport_ZoomIndicator") is TextBlock;
        checks["player.coordinateIndicator"] = Find(view, "MapViewport_CoordinateIndicator") is TextBlock;
        checks["player.readOnly"] = Descendants(view).OfType<Button>().All(button =>
            !Contains(button.Content, "Сохран") && !Contains(button.Content, "Добав") && !Contains(button.Content, "Удал") && !Contains(button.Content, "Архив"));
    }

    private static bool Contains(object? value, string token) => (Convert.ToString(value) ?? string.Empty).IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
    private static bool ButtonCanExecute(DependencyObject root, string id) => Find(root, id) is Button button && button.Command != null && button.Command.CanExecute(button.CommandParameter);
    private static DependencyObject? Find(DependencyObject root, string id) => Descendants(root).FirstOrDefault(item => item is FrameworkElement element && AutomationProperties.GetAutomationId(element) == id);
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        yield return root;
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
            foreach (var item in Descendants(VisualTreeHelper.GetChild(root, index))) yield return item;
    }

    private static void LoadTheme(Application app, string assembly, string theme)
    {
        app.Resources.MergedDictionaries.Clear();
        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/Nri.Ui.Wpf;component/Resources/NriUiResources.xaml", UriKind.Absolute) });
        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri($"pack://application:,,,/{assembly};component/Resources/{theme}", UriKind.Absolute) });
    }

    private sealed class WindowScope : IDisposable
    {
        private readonly Window _window;
        public WindowScope(FrameworkElement content)
        {
            _window = new Window { Content = content, Width = 1500, Height = 900, Left = -32000, Top = -32000, ShowInTaskbar = false, ShowActivated = false, WindowStyle = WindowStyle.None };
            _window.Show();
            _window.Measure(new Size(1500, 900));
            _window.Arrange(new Rect(0, 0, 1500, 900));
            _window.UpdateLayout();
            _window.Dispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(() => { }));
        }
        public void Dispose() => _window.Close();
    }

    private sealed class AdminFakeTransport : AdminTransport
    {
        public string ServerHost => "contract"; public int ServerPort => 0; public void Connect() { } public void Disconnect() { } public void UpdateEndpoint(string host, int port) { } public void Dispose() { }
        public ResponseEnvelope Send(RequestEnvelope request) => Empty();
    }
    private sealed class PlayerFakeTransport : PlayerTransport
    {
        public string ServerHost => "contract"; public int ServerPort => 0; public void Connect() { } public void Disconnect() { } public void UpdateEndpoint(string host, int port) { } public void Dispose() { }
        public ResponseEnvelope Send(RequestEnvelope request) => Empty();
    }
    private static ResponseEnvelope Empty() => new() { Status = ResponseStatus.Ok, Payload = new Dictionary<string, object> { ["items"] = Array.Empty<object>() } };
    private sealed class BindingListener : TraceListener
    {
        private readonly IList<string> _messages; private readonly StringBuilder _current = new(); public BindingListener(IList<string> messages) => _messages = messages;
        public override void Write(string? message) { if (message != null) _current.Append(message); }
        public override void WriteLine(string? message) { if (message != null) _current.Append(message); if (_current.Length > 0) _messages.Add(_current.ToString()); _current.Clear(); }
    }
}
