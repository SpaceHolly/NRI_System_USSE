using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Nri.AdminClient.ViewModels;
using Nri.AssetConfigurators.Wpf.Models;
using Nri.AssetConfigurators.Wpf.Views;
using Nri.Ui.Wpf.Controls;
using Nri.Shared.Contracts;
using AdminApi = Nri.AdminClient.Networking.CommandApi;
using AdminTransport = Nri.AdminClient.Networking.IJsonTcpClient;
using PlayerApi = Nri.PlayerClient.Networking.CommandApi;
using PlayerTransport = Nri.PlayerClient.Networking.IJsonTcpClient;

namespace Nri.AssetConfigurators.Materialization;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var outputPath = args.Length > 0
            ? Path.GetFullPath(args[0])
            : Path.GetFullPath("shared_workspace_materialization_audit.json");
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");

        var audit = new MaterializationAudit();
        var bindingListener = new BindingTraceListener(audit.BindingErrors);
        PresentationTraceSources.DataBindingSource.Listeners.Add(bindingListener);
        PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Warning;

        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.DispatcherUnhandledException += (_, eventArgs) =>
        {
            audit.DispatcherErrors.Add(eventArgs.Exception.ToString());
            eventArgs.Handled = true;
        };

        try
        {
            LoadTheme(app, "Nri.AdminClient", "AdminTheme.xaml");
            audit.AdminThemeLoaded = true;
            MaterializeAdmin(audit);

            LoadTheme(app, "Nri.PlayerClient", "PlayerTheme.xaml");
            audit.PlayerThemeLoaded = true;
            MaterializePlayer(audit);
            audit.BlueprintPanelMaterialized =
                audit.AdminBlueprintPanelMaterialized &&
                audit.PlayerBlueprintPanelMaterialized;
        }
        catch (Exception ex)
        {
            audit.UnhandledErrors.Add(ex.ToString());
            if (ex is System.Windows.Markup.XamlParseException ||
                ex.Message.IndexOf("resource", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                audit.MissingResources.Add(ex.Message);
            }
        }
        finally
        {
            PresentationTraceSources.DataBindingSource.Listeners.Remove(bindingListener);
            app.Shutdown();
        }

        audit.Status = audit.IsPass ? "PASS" : "NOT_PASS";
        WriteAudit(outputPath, audit);
        Console.WriteLine($"0.18.2R.7 shared workspace materialization: {audit.Status}");
        Console.WriteLine($"Artifact: {outputPath}");
        return audit.IsPass ? 0 : 1;
    }

    private static void LoadTheme(Application app, string clientAssembly, string themeFile)
    {
        app.Resources.MergedDictionaries.Clear();
        app.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri(
                "pack://application:,,,/Nri.Ui.Wpf;component/Resources/NriUiResources.xaml",
                UriKind.Absolute)
        });
        app.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri(
                $"pack://application:,,,/{clientAssembly};component/Resources/{themeFile}",
                UriKind.Absolute)
        });
    }

    private static void MaterializeAdmin(MaterializationAudit audit)
    {
        var api = new AdminApi(new AdminFakeTransport());
        var viewModel = new AdminAssetConfiguratorsViewModel(api);
        var host = new Nri.AdminClient.Views.Administration.AdminAssetConfiguratorsView
        {
            DataContext = viewModel
        };

        using (var scope = new MaterializationScope(host))
        {
            var modeTabs = Require<TabControl>(host, "AdminAssetConfigurators_ModeTabs");
            modeTabs.SelectedIndex = 0;
            scope.Update();

            var workspace = Require<AssetConfiguratorWorkspaceView>(
                host,
                "AssetConfiguratorWorkspace_Route");
            audit.AdminSpacecraftMaterialized = SelectAndVerify(
                scope,
                workspace,
                viewModel.Workspace,
                0,
                "AdminAssetConfigurators_SpacecraftName",
                out var adminSpacecraftRecalculated);
            audit.AdminLandMarineMaterialized = SelectAndVerify(
                scope,
                workspace,
                viewModel.Workspace,
                1,
                "AdminAssetConfigurators_LandMarineName",
                out var adminLandMarineRecalculated);
            audit.AdminBuildingMaterialized = SelectAndVerify(
                scope,
                workspace,
                viewModel.Workspace,
                2,
                "AdminAssetConfigurators_BuildingName",
                out var adminBuildingRecalculated);
            audit.AdminImmediateRecalculation =
                adminSpacecraftRecalculated &&
                adminLandMarineRecalculated &&
                adminBuildingRecalculated;

            modeTabs.SelectedIndex = 1;
            scope.Update();
            audit.AdminBlueprintPanelMaterialized =
                FindByAutomationId(host, "AdminAssetBlueprints_List") is ListBox;
        }
    }

    private static void MaterializePlayer(MaterializationAudit audit)
    {
        var api = new PlayerApi(new PlayerFakeTransport());
        var viewModel = new Nri.PlayerClient.ViewModels.PlayerAssetConfiguratorsViewModel(
            api,
            () => string.Empty);
        var host = new Nri.PlayerClient.Views.Engineering.PlayerAssetConfiguratorsView
        {
            DataContext = viewModel
        };

        using (var scope = new MaterializationScope(host))
        {
            var modeTabs = Require<TabControl>(host, "PlayerAssetConfigurators_ModeTabs");
            modeTabs.SelectedIndex = 0;
            scope.Update();

            var workspace = Require<AssetConfiguratorWorkspaceView>(
                host,
                "AssetConfiguratorWorkspace_Route");
            audit.PlayerSpacecraftMaterialized = SelectAndVerify(
                scope,
                workspace,
                viewModel.Workspace,
                0,
                "AdminAssetConfigurators_SpacecraftName",
                out var playerSpacecraftRecalculated);
            audit.PlayerLandMarineMaterialized = SelectAndVerify(
                scope,
                workspace,
                viewModel.Workspace,
                1,
                "AdminAssetConfigurators_LandMarineName",
                out var playerLandMarineRecalculated);
            audit.PlayerBuildingMaterialized = SelectAndVerify(
                scope,
                workspace,
                viewModel.Workspace,
                2,
                "AdminAssetConfigurators_BuildingName",
                out var playerBuildingRecalculated);
            audit.PlayerImmediateRecalculation =
                playerSpacecraftRecalculated &&
                playerLandMarineRecalculated &&
                playerBuildingRecalculated;

            modeTabs.SelectedIndex = 1;
            viewModel.Blueprints.Add(new AssetBlueprintPresentation
            {
                Name = "Материализованный тестовый чертёж",
                ConfiguratorKindLabel = "Космический корабль",
                StatusLabel = "Черновик",
                IsValid = true,
                TotalCost = 100
            });
            scope.Update();
            var blueprintList = Require<ListBox>(host, "PlayerAssetBlueprints_List");
            var blueprintItem = blueprintList.ItemContainerGenerator.ContainerFromIndex(0)
                as ListBoxItem;
            audit.PlayerBlueprintPanelMaterialized =
                blueprintItem != null &&
                AutomationProperties.GetName(blueprintItem)
                    .Contains("Материализованный тестовый чертёж");
            audit.PlayerBlueprintListAccessible = audit.PlayerBlueprintPanelMaterialized;
        }
    }

    private static bool SelectAndVerify(
        MaterializationScope scope,
        AssetConfiguratorWorkspaceView workspace,
        Nri.AssetConfigurators.Wpf.ViewModels.AssetConfiguratorWorkspaceViewModel viewModel,
        int index,
        string expectedFieldId,
        out bool immediateRecalculation)
    {
        immediateRecalculation = false;
        viewModel.SelectedConfiguratorIndex = index;
        var tabs = Require<TabControl>(workspace, "AssetConfiguratorWorkspace_Tools");
        tabs.SelectedIndex = index;
        if (index == 0)
            viewModel.Spacecraft.LoadDemoCommand.Execute(null);
        else if (index == 1)
            viewModel.LandMarine.LoadDemoCommand.Execute(null);
        else
            viewModel.Building.LoadDemoCommand.Execute(null);
        scope.Update();

        var field = FindByAutomationId(workspace, expectedFieldId) as FrameworkElement;
        if (field == null || field.ActualWidth <= 0 || field.ActualHeight <= 0)
            return false;

        var selectedComponents = Require<ListBox>(
            workspace,
            "AdminAssetConfigurators_SelectedComponents");
        var firstItem = selectedComponents.ItemContainerGenerator.ContainerFromIndex(0)
            as ListBoxItem;
        var accessibleComponent =
            selectedComponents.Items.Count > 0 &&
            firstItem != null &&
            !string.IsNullOrWhiteSpace(AutomationProperties.GetName(firstItem));

        Nri.AssetConfigurators.Wpf.ViewModels.AssetConfiguratorToolViewModel tool = index == 0
            ? viewModel.Spacecraft
            : index == 1
                ? viewModel.LandMarine
                : viewModel.Building;
        var summary = Require<TextBox>(workspace, "AdminAssetConfigurators_Summary");
        var row = selectedComponents.Items.OfType<
            Nri.AssetConfigurators.Wpf.ViewModels.ConfiguratorSelectionRow>().FirstOrDefault();
        if (row != null)
        {
            var beforeViewModel = tool.ResultSummary;
            var beforeTextBox = summary.Text;
            tool.IncreaseQuantityCommand.Execute(row);
            scope.Update();
            immediateRecalculation =
                !string.Equals(beforeViewModel, tool.ResultSummary, StringComparison.Ordinal) &&
                !string.Equals(beforeTextBox, summary.Text, StringComparison.Ordinal) &&
                string.Equals(tool.ResultSummary, summary.Text, StringComparison.Ordinal);
        }

        var readableValidation = true;
        if (index == 1 && tool.ValidationMessages.Count > 0)
        {
            var validationSummary = Require<NriValidationSummary>(
                workspace,
                "AdminAssetConfigurators_ValidationSummary");
            scope.Update();
            var visibleMessages = Descendants(validationSummary)
                .OfType<TextBlock>()
                .Select(item => item.Text)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToList();
            readableValidation =
                visibleMessages.Any(item =>
                    tool.ValidationMessages.Any(issue =>
                        string.Equals(issue.Message, item, StringComparison.Ordinal))) &&
                visibleMessages.All(item =>
                    item.IndexOf(
                        "Nri.AssetConfigurators",
                        StringComparison.OrdinalIgnoreCase) < 0);
        }

        return accessibleComponent &&
            immediateRecalculation &&
            readableValidation &&
            Descendants(workspace)
            .OfType<ContentPresenter>()
            .Any(item =>
                Grid.GetColumn(item) == 2 &&
                item.Content != null &&
                item.ContentTemplate != null &&
                VisualTreeHelper.GetChildrenCount(item) > 0);
    }

    private static T Require<T>(DependencyObject root, string automationId)
        where T : DependencyObject
    {
        var value = FindByAutomationId(root, automationId) as T;
        if (value == null)
            throw new InvalidOperationException(
                $"Materialized {typeof(T).Name} not found: {automationId}");
        return value;
    }

    private static DependencyObject? FindByAutomationId(
        DependencyObject root,
        string automationId)
    {
        return Descendants(root).FirstOrDefault(item =>
            item is FrameworkElement element &&
            string.Equals(
                AutomationProperties.GetAutomationId(element),
                automationId,
                StringComparison.Ordinal));
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        yield return root;
        var childCount = VisualTreeHelper.GetChildrenCount(root);
        for (var index = 0; index < childCount; index++)
        {
            foreach (var child in Descendants(VisualTreeHelper.GetChild(root, index)))
                yield return child;
        }
    }

    private static void WriteAudit(string outputPath, MaterializationAudit audit)
    {
        var serializer = new DataContractJsonSerializer(
            typeof(MaterializationAudit),
            new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true });
        using (var stream = File.Create(outputPath))
            serializer.WriteObject(stream, audit);
    }

    private sealed class MaterializationScope : IDisposable
    {
        private readonly Window _window;

        public MaterializationScope(FrameworkElement content)
        {
            _window = new Window
            {
                Content = content,
                Width = 1880,
                Height = 1000,
                Left = -32000,
                Top = -32000,
                ShowActivated = false,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None
            };
            _window.Show();
            Update();
        }

        public void Update()
        {
            _window.ApplyTemplate();
            if (_window.Content is FrameworkElement content)
                content.ApplyTemplate();
            _window.Measure(new Size(1880, 1000));
            _window.Arrange(new Rect(0, 0, 1880, 1000));
            _window.UpdateLayout();
            _window.Dispatcher.Invoke(
                DispatcherPriority.ApplicationIdle,
                new Action(() => { }));
        }

        public void Dispose()
        {
            _window.Close();
        }
    }

    private sealed class BindingTraceListener : TraceListener
    {
        private readonly List<string> _messages;
        private readonly StringBuilder _line = new StringBuilder();

        public BindingTraceListener(List<string> messages)
        {
            _messages = messages;
        }

        public override void Write(string? message)
        {
            if (!string.IsNullOrEmpty(message))
                _line.Append(message);
        }

        public override void WriteLine(string? message)
        {
            if (!string.IsNullOrEmpty(message))
                _line.Append(message);
            var value = _line.ToString().Trim();
            if (value.Length > 0 && !_messages.Contains(value))
                _messages.Add(value);
            _line.Clear();
        }
    }

    private sealed class AdminFakeTransport : AdminTransport
    {
        public string ServerHost => "materialization.test";
        public int ServerPort => 0;
        public void Connect() { }
        public void Disconnect() { }
        public void UpdateEndpoint(string host, int port) { }
        public ResponseEnvelope Send(RequestEnvelope request) => EmptyResponse();
        public void Dispose() { }
    }

    private sealed class PlayerFakeTransport : PlayerTransport
    {
        public string ServerHost => "materialization.test";
        public int ServerPort => 0;
        public void Connect() { }
        public void Disconnect() { }
        public void UpdateEndpoint(string host, int port) { }
        public ResponseEnvelope Send(RequestEnvelope request) => EmptyResponse();
        public void Dispose() { }
    }

    private static ResponseEnvelope EmptyResponse()
    {
        return new ResponseEnvelope
        {
            Status = ResponseStatus.Ok,
            Payload = new Dictionary<string, object>
            {
                ["items"] = Array.Empty<object>()
            }
        };
    }
}

[DataContract]
internal sealed class MaterializationAudit
{
    [DataMember(Name = "adminThemeLoaded", Order = 1)]
    public bool AdminThemeLoaded { get; set; }

    [DataMember(Name = "playerThemeLoaded", Order = 2)]
    public bool PlayerThemeLoaded { get; set; }

    [DataMember(Name = "adminSpacecraftMaterialized", Order = 3)]
    public bool AdminSpacecraftMaterialized { get; set; }

    [DataMember(Name = "adminLandMarineMaterialized", Order = 4)]
    public bool AdminLandMarineMaterialized { get; set; }

    [DataMember(Name = "adminBuildingMaterialized", Order = 5)]
    public bool AdminBuildingMaterialized { get; set; }

    [DataMember(Name = "playerSpacecraftMaterialized", Order = 6)]
    public bool PlayerSpacecraftMaterialized { get; set; }

    [DataMember(Name = "playerLandMarineMaterialized", Order = 7)]
    public bool PlayerLandMarineMaterialized { get; set; }

    [DataMember(Name = "playerBuildingMaterialized", Order = 8)]
    public bool PlayerBuildingMaterialized { get; set; }

    [DataMember(Name = "blueprintPanelMaterialized", Order = 9)]
    public bool BlueprintPanelMaterialized { get; set; }

    [DataMember(Name = "adminBlueprintPanelMaterialized", Order = 10)]
    public bool AdminBlueprintPanelMaterialized { get; set; }

    [DataMember(Name = "playerBlueprintPanelMaterialized", Order = 11)]
    public bool PlayerBlueprintPanelMaterialized { get; set; }

    [DataMember(Name = "adminImmediateRecalculation", Order = 12)]
    public bool AdminImmediateRecalculation { get; set; }

    [DataMember(Name = "playerImmediateRecalculation", Order = 13)]
    public bool PlayerImmediateRecalculation { get; set; }

    [DataMember(Name = "playerBlueprintListAccessible", Order = 14)]
    public bool PlayerBlueprintListAccessible { get; set; }

    [DataMember(Name = "missingResources", Order = 15)]
    public List<string> MissingResources { get; } = new List<string>();

    [DataMember(Name = "bindingErrors", Order = 16)]
    public List<string> BindingErrors { get; } = new List<string>();

    [DataMember(Name = "dispatcherErrors", Order = 17)]
    public List<string> DispatcherErrors { get; } = new List<string>();

    [DataMember(Name = "unhandledErrors", Order = 18)]
    public List<string> UnhandledErrors { get; } = new List<string>();

    [DataMember(Name = "status", Order = 19)]
    public string Status { get; set; } = "NOT_PASS";

    public bool IsPass =>
        AdminThemeLoaded &&
        PlayerThemeLoaded &&
        AdminSpacecraftMaterialized &&
        AdminLandMarineMaterialized &&
        AdminBuildingMaterialized &&
        PlayerSpacecraftMaterialized &&
        PlayerLandMarineMaterialized &&
        PlayerBuildingMaterialized &&
        BlueprintPanelMaterialized &&
        AdminImmediateRecalculation &&
        PlayerImmediateRecalculation &&
        PlayerBlueprintListAccessible &&
        MissingResources.Count == 0 &&
        BindingErrors.Count == 0 &&
        DispatcherErrors.Count == 0 &&
        UnhandledErrors.Count == 0;
}
