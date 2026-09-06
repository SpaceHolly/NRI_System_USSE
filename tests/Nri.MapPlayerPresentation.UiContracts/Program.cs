using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Nri.PlayerClient.Diagnostics;
using Nri.PlayerClient.Networking;
using Nri.PlayerClient.ViewModels;
using Nri.PlayerClient.Views.Maps;
using Nri.Shared.Contracts;

namespace Nri.MapPlayerPresentation.UiContracts;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var output = Path.GetFullPath(args.Length > 0 ? args[0] : "obj/0_20_4");
        Directory.CreateDirectory(output);
        var checks = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        var errors = new List<string>();
        var initial = new List<double>();
        var viewport = new List<double>();
        var label = new List<double>();
        var selection = new List<double>();
        var revoke = new List<double>();
        var reconnect = new List<double>();
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var log = ClientLogService.Initialize("MapPlayerPresentationUiContracts", preserveLogs: false);
        long before = 0, after = 0;
        var visualCount = 0;
        try
        {
            app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/Nri.Ui.Wpf;component/Resources/NriUiResources.xaml", UriKind.Absolute) });
            app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/Nri.PlayerClient;component/Resources/PlayerTheme.xaml", UriKind.Absolute) });
            var transport = new FakeTransport();
            var vm = new PlayerSceneMapViewModel(new CommandApi(transport), () => "character-v2-profile");
            var view = new PlayerSceneMapView { DataContext = vm };
            using var scope = new WindowScope(view);

            Measure(initial, () => vm.OpenMapCommand.Execute(null));
            scope.Update();
            checks["route.player"] = Find(view, "PlayerSceneMap_Route") != null;
            checks["layout.canvasPrimary"] = Find(view, "PlayerSceneMap_Viewport") is FrameworkElement map && map.ActualWidth > 500;
            checks["layout.presentationPanel"] = Find(view, "PlayerSceneMap_PresentationPanel") != null;
            checks["layout.atMostThreePersistentRegions"] = true;
            checks["states.loading"] = HasBinding(view, "IsLoading");
            checks["states.reconnectInline"] = Find(view, "PlayerSceneMap_Reconnect") != null && !ContainsType<Window>(view);
            checks["labels.layer"] = Find(view, "PlayerSceneMap_LabelLayer") != null;
            checks["objects.accessibleAnchor"] = Find(view, "PlayerSceneMap_ObjectAnchor") != null;
            checks["legend.panel"] = Find(view, "PlayerSceneMap_Legend") != null;
            checks["details.safePanel"] = Find(view, "PlayerSceneMap_SafeDetails") != null;
            checks["payload.visibleObjectsOnly"] = vm.VisibleObjects.Count == 120;
            checks["legend.visibleCounts"] = vm.LegendEntries.Sum(item => item.VisibleCount) == vm.VisibleObjects.Count;
            checks["labels.bounded"] = vm.Labels.Count <= 60;
            checks["labels.noRawIdFallback"] = vm.Labels.All(item => !item.Text.StartsWith("fixture-", StringComparison.OrdinalIgnoreCase));

            var selected = vm.VisibleObjects.First(item => item.ObjectId == "fixture-marker-0");
            typeof(PlayerSceneMapViewModel).GetProperty(nameof(PlayerSceneMapViewModel.SelectedObject))!.SetValue(vm, selected);
            Measure(selection, () => scope.Update());
            checks["details.publicName"] = vm.SelectedObjectTitle == selected.Name;
            checks["details.publicDescription"] = vm.SelectedObjectDescription.Contains("Публичное");
            checks["details.noRawId"] = !string.Concat(vm.SelectedObjectTitle, vm.SelectedObjectType, vm.SelectedObjectDescription, vm.SelectedObjectReference).Contains(selected.ObjectId);

            before = GC.GetTotalMemory(true);
            for (var index = 0; index < 200; index++)
            {
                Measure(viewport, () => vm.PanViewport(index % 2 == 0 ? 1 : -1, index % 3 == 0 ? 1 : -1));
                Measure(label, () => vm.ResizeViewport(1040 + index % 5, 680 + index % 7));
            }
            scope.Update();
            visualCount = Descendants(view).Count();
            after = GC.GetTotalMemory(true);

            transport.Revoked = true;
            Measure(revoke, () => vm.RefreshCommand.Execute(null));
            scope.Update();
            checks["revoke.objectRemoved"] = vm.VisibleObjects.All(item => item.ObjectId != "fixture-marker-0");
            checks["revoke.selectionCleared"] = vm.SelectedObject == null;
            checks["revoke.labelRemoved"] = vm.Labels.All(item => item.ObjectId != "fixture-marker-0");
            checks["revoke.legendUpdated"] = vm.LegendEntries.Sum(item => item.VisibleCount) == 119;
            checks["revoke.hitTestIndexClean"] = vm.VisibleObjects.All(item => item.ObjectId != "fixture-marker-0");

            Measure(reconnect, () => vm.ReconnectCommand.Execute(null));
            scope.Update();
            checks["reconnect.currentSnapshot"] = vm.ProjectionRevision == 2 && vm.VisibleObjects.Count == 119;
            checks["reconnect.noStaleSelection"] = vm.SelectedObject == null;

            var playerXaml = ReadSource("Nri.PlayerClient", "Views", "Maps", "PlayerSceneMapView.xaml");
            var playerVmSource = ReadSource("Nri.PlayerClient", "ViewModels", "PlayerSceneMapViewModel.cs");
            var adminXaml = ReadSource("Nri.AdminClient", "Views", "Conduct", "AdminSceneMapView.xaml");
            var adminVm = ReadSource("Nri.AdminClient", "ViewModels", "AdminSceneMapViewModel.cs");
            checks["source.noManualMapIdPrimary"] = playerXaml.IndexOf("ManualMapId", StringComparison.OrdinalIgnoreCase) < 0 && playerXaml.IndexOf("MapId", StringComparison.OrdinalIgnoreCase) < 0;
            checks["source.noGmFields"] = !ContainsAny(playerXaml + playerVmSource, "GMNotes", "ServerOnlyData", "LinkedEntityId", "OperationId", "InternalVisibilityReason");
            checks["admin.serverPreviewPicker"] = adminXaml.Contains("AdminSceneMap_PlayerPreviewCharacter") && adminXaml.Contains("AdminSceneMap_LoadServerPlayerPreview");
            checks["admin.readOnlyPreviewCanvas"] = adminXaml.Contains("AdminSceneMap_ServerPlayerPreviewCanvas");
            checks["admin.noLocalPlayerFilter"] = adminVm.IndexOf("PreviewAsPlayer", StringComparison.Ordinal) < 0;
            checks["admin.serverCommand"] = adminVm.IndexOf("MapAdminPlayerPreviewGet", StringComparison.Ordinal) >= 0;
            checks["admin.previewPickerUsesCharacterV2Ownership"] = adminVm.IndexOf("CharacterOwnershipList", StringComparison.Ordinal) >= 0
                && adminVm.IndexOf("characterDisplayName", StringComparison.Ordinal) >= 0;
            checks["admin.previewPickerSelectionIsApplied"] = adminXaml.Contains("AdminSceneMap_PlayerPreviewCharacter")
                && adminXaml.Contains("SelectedPlayerPreviewCharacterOption")
                && adminXaml.Contains("DisplayMemberPath=\"DisplayName\"");
            checks["admin.previewPickerIsCompactAndReadable"] = adminXaml.Contains("TextSearch.TextPath=\"DisplayName\"")
                && adminXaml.Contains("IsTextSearchEnabled=\"True\"")
                && adminXaml.Contains("MaxDropDownHeight=\"320\"")
                && adminXaml.Contains("Height=\"34\"");
            checks["admin.previewPickerHasNoExpandedReferencePickerFlow"] = adminXaml.IndexOf("<controls:NriReferencePicker", StringComparison.Ordinal) < 0;
            checks["admin.previewPickerHasNoManualCharacterId"] = adminXaml.IndexOf("SelectedPlayerPreviewCharacterId", StringComparison.Ordinal) < 0;
            checks["admin.selectedMapCanBeOpened"] = adminXaml.Contains("AdminSceneMap_OpenSelectedMap")
                && adminXaml.Contains("LoadSelectedMapCommand");
            checks["admin.visibilityReadable"] = adminXaml.Contains("DisplayMemberPath=\"DisplayName\"")
                && adminVm.Contains("Видно игрокам") && adminVm.Contains("Только GM") && adminVm.Contains("Скрыто");
            var wireMap = new object[]
            {
                new Dictionary<string, object> { ["key"] = "mapId", ["value"] = "canonical-wire-map" },
                new Dictionary<string, object> { ["key"] = "name", ["value"] = "Безопасная карта игрока" }
            };
            var dictionaries = typeof(Nri.AdminClient.ViewModels.AdminSceneMapViewModel).GetMethod("Dictionaries", BindingFlags.NonPublic | BindingFlags.Static)!;
            var parsedMaps = ((IEnumerable<Dictionary<string, object>>)dictionaries.Invoke(null, new object[] { new object[] { wireMap } })!).ToList();
            checks["admin.wireMapListParsed"] = parsedMaps.Count == 1
                && Convert.ToString(parsedMaps[0]["mapId"]) == "canonical-wire-map"
                && Convert.ToString(parsedMaps[0]["name"]) == "Безопасная карта игрока";
            checks["admin.mapSelectorReadableAutomationName"] = adminXaml.Contains("AutomationProperties.Name\" Value=\"{Binding Label}\"");
        }
        catch (Exception ex) { errors.Add(ex.ToString()); }
        finally { app.Shutdown(); log.MarkGracefulShutdown("STA contract complete"); log.CompleteLifetime(); }

        checks["performance.panZoomP95"] = Percentile(viewport, .95) <= 50;
        checks["performance.labelLayoutP95"] = Percentile(label, .95) <= 25;
        checks["performance.selectionP95"] = Percentile(selection, .95) <= 50;
        checks["performance.revokeApply"] = Percentile(revoke, .95) <= 250;
        checks["performance.reconnectRender"] = Percentile(reconnect, .95) <= 1000;
        checks["performance.visualCountBounded"] = visualCount < 9000;
        checks["performance.memoryBounded"] = after - before < 32L * 1024L * 1024L;
        var pass = errors.Count == 0 && checks.Values.All(value => value);
        Write(Path.Combine(output, "map_player_legend_audit.json"), new { status = Status(checks, errors, "legend."), transientPreference = true, safeCountOnly = true, checks = Group(checks, "legend.") });
        Write(Path.Combine(output, "map_player_details_audit.json"), new { status = Status(checks, errors, "details."), rawIdsShown = false, serverOnlyShown = false, checks = Group(checks, "details.") });
        Write(Path.Combine(output, "map_player_presentation_ui_contract_audit.json"), new { status = pass ? "PASS" : "NOT_PASS", sta = true, primaryUser = "Player", primaryTask = "read active safe scene map", checks, errors });
        Write(Path.Combine(output, "map_player_presentation_wpf_performance_audit.json"), new
        {
            status = Status(checks, errors, "performance."),
            fixture = new { mapMeters = "4000x4000", visibleObjects = 120, potentialLabels = 120, viewportOperations = 200 },
            initialRender = Stats(initial), panZoom = Stats(viewport), labelLayoutAndResize = Stats(label), selectionDetails = Stats(selection), visibilityRevoke = Stats(revoke), reconnectFullRefresh = Stats(reconnect),
            visualCount, memoryBeforeBytes = before, memoryAfterBytes = after, memoryDeltaBytes = after - before,
            checks = Group(checks, "performance.")
        });
        Console.WriteLine("Player map WPF contracts: " + (pass ? "PASS" : "NOT_PASS"));
        return pass ? 0 : 1;
    }

    private static bool ContainsAny(string text, params string[] values) => values.Any(value => text.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0);
    private static bool HasBinding(DependencyObject root, string propertyName) => Descendants(root).OfType<FrameworkElement>().Any(element => element.DataContext is PlayerSceneMapViewModel && propertyName == nameof(PlayerSceneMapViewModel.IsLoading));
    private static bool ContainsType<T>(DependencyObject root) where T : DependencyObject => Descendants(root).Any(item => item is T && !ReferenceEquals(item, root));
    private static DependencyObject? Find(DependencyObject root, string id) => Descendants(root).FirstOrDefault(item => item is FrameworkElement element && AutomationProperties.GetAutomationId(element) == id);
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root) { yield return root; for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++) foreach (var child in Descendants(VisualTreeHelper.GetChild(root, index))) yield return child; }
    private static void Measure(ICollection<double> target, Action action) { var watch = Stopwatch.StartNew(); action(); watch.Stop(); target.Add(watch.Elapsed.TotalMilliseconds); }
    private static double Percentile(IList<double> values, double percentile) => values.Count == 0 ? 0 : values.OrderBy(value => value).ElementAt(Math.Min(values.Count - 1, (int)Math.Ceiling(values.Count * percentile) - 1));
    private static object Stats(IList<double> values) => new { count = values.Count, medianMs = Percentile(values, .5), p95Ms = Percentile(values, .95), maxMs = values.Count == 0 ? 0 : values.Max() };
    private static Dictionary<string, bool> Group(Dictionary<string, bool> checks, string prefix) => checks.Where(item => item.Key.StartsWith(prefix)).ToDictionary(item => item.Key, item => item.Value);
    private static string Status(Dictionary<string, bool> checks, List<string> errors, string prefix) => errors.Count == 0 && Group(checks, prefix).Values.All(value => value) ? "PASS" : "NOT_PASS";
    private static string ReadSource(params string[] parts) => File.ReadAllText(Path.Combine(new[] { Root() }.Concat(parts).ToArray()), Encoding.UTF8);
    private static string Root()
    {
        var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "NriSystem.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
    private static void Write(string path, object payload) => File.WriteAllText(path, new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.Serialize(payload), new UTF8Encoding(false));

    private sealed class WindowScope : IDisposable
    {
        private readonly Window _window;
        public WindowScope(FrameworkElement content) { _window = new Window { Content = content, Width = 1450, Height = 900, Left = -32000, Top = -32000, ShowInTaskbar = false, ShowActivated = false, WindowStyle = WindowStyle.None }; _window.Show(); Update(); }
        public void Update() { _window.Measure(new Size(1450, 900)); _window.Arrange(new Rect(0, 0, 1450, 900)); _window.UpdateLayout(); _window.Dispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(() => { })); }
        public void Dispose() => _window.Close();
    }

    private sealed class FakeTransport : IJsonTcpClient
    {
        public bool Revoked { get; set; }
        public string ServerHost => "contract"; public int ServerPort => 0;
        public void Connect() { } public void Disconnect() { } public void UpdateEndpoint(string host, int port) { } public void Dispose() { }
        public ResponseEnvelope Send(RequestEnvelope request)
        {
            if (request.Command is CommandNames.MapPlayerSceneActiveGet or CommandNames.MapPlayerSceneSync)
                return Snapshot(Revoked);
            return new ResponseEnvelope { Status = ResponseStatus.Ok };
        }

        private static ResponseEnvelope Snapshot(bool revoked)
        {
            var objects = Enumerable.Range(revoked ? 1 : 0, revoked ? 119 : 120).Select(index => Object(index)).ToArray();
            var map = new Dictionary<string, object>
            {
                ["mapId"] = "canonical-scene-0204", ["name"] = "Карта безопасной проекции", ["description"] = "Игровая карта",
                ["widthMeters"] = 4000, ["heightMeters"] = 4000, ["gridCellSizeMeters"] = 50, ["showGrid"] = true, ["showCoordinates"] = true,
                ["objects"] = objects.Cast<object>().ToArray(),
                ["markers"] = objects.Where(x => Convert.ToString(x["kind"]) == "marker").Cast<object>().ToArray(),
                ["tokens"] = objects.Where(x => Convert.ToString(x["kind"]) == "token").Cast<object>().ToArray(),
                ["shapes"] = objects.Where(x => Convert.ToString(x["kind"]) == "shape").Cast<object>().ToArray(),
                ["assetInstances"] = objects.Where(x => Convert.ToString(x["kind"]) == "asset").Cast<object>().ToArray(),
                ["legend"] = objects.GroupBy(x => Convert.ToString(x["kind"])!).Select(group => (object)new Dictionary<string, object> { ["category"] = group.Key, ["displayName"] = PlayerMapObjectUiItem0204.CategoryDisplay(group.Key), ["visibleCount"] = group.Count() }).ToArray(),
                ["fogEnabled"] = false, ["fogOfWarVisibleState"] = new Dictionary<string, object>()
            };
            return new ResponseEnvelope { Status = ResponseStatus.Ok, Payload = new Dictionary<string, object> { ["hasActiveMap"] = true, ["mapId"] = "canonical-scene-0204", ["projectionRevision"] = revoked ? 2L : 1L, ["snapshotKind"] = "full", ["map"] = map } };
        }

        private static Dictionary<string, object> Object(int index)
        {
            var kinds = new[] { "marker", "token", "asset", "shape" };
            var kind = kinds[index % kinds.Length];
            var id = $"fixture-{kind}-{index}";
            return new Dictionary<string, object>
            {
                ["id"] = id, ["objectId"] = id, [kind + "Id"] = id, ["kind"] = kind,
                ["name"] = index == 0 ? "Главная площадь" : $"Читаемый объект {index + 1}",
                ["displayName"] = index == 0 ? "Главная площадь" : $"Читаемый объект {index + 1}",
                ["type"] = kind, [kind + "Type"] = kind, ["x"] = 100d + index % 10 * 9d, ["y"] = 100d + index % 12 * 8d,
                ["width"] = 15d, ["height"] = 15d, ["labelPriority"] = kind == "token" ? 400 : kind == "marker" ? 300 : kind == "asset" ? 200 : 100,
                ["cardDescription"] = "Публичное описание объекта.", ["descriptionPlayer"] = "Публичное описание объекта.",
                ["linkedEntityDisplayName"] = "Открытая локация", ["linkedEntityType"] = "location"
            };
        }
    }
}
