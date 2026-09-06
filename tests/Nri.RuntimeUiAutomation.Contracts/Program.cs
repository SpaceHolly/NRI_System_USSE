using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Nri.AdminClient.ViewModels;
using Nri.AdminClient.Views.Administration;
using Nri.PlayerClient.ViewModels;
using Nri.PlayerClient.Views.Production;
using Nri.Shared.Contracts;
using AdminApi = Nri.AdminClient.Networking.CommandApi;
using AdminTransport = Nri.AdminClient.Networking.IJsonTcpClient;
using PlayerApi = Nri.PlayerClient.Networking.CommandApi;
using PlayerTransport = Nri.PlayerClient.Networking.IJsonTcpClient;

namespace Nri.RuntimeUiAutomation.Contracts;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var output = Path.GetFullPath(args.Length > 0
            ? args[0]
            : "obj/dev_verify_1/ui_automation_contract_audit.json");
        Directory.CreateDirectory(Path.GetDirectoryName(output) ?? ".");
        var audit = new ContractAudit();
        var bindingErrors = new BindingTraceListener(audit.BindingErrors);
        PresentationTraceSources.DataBindingSource.Listeners.Add(bindingErrors);
        PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Warning;
        var playerLog = Nri.PlayerClient.Diagnostics.ClientLogService.Initialize(
            "RuntimeUiAutomationContracts.Player", true);
        var adminLog = Nri.AdminClient.Diagnostics.ClientLogService.Initialize(
            "RuntimeUiAutomationContracts.Admin", true);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };

        try
        {
            LoadTheme(app, "Nri.PlayerClient", "PlayerTheme.xaml");
            VerifyPlayer(audit);
            LoadTheme(app, "Nri.AdminClient", "AdminTheme.xaml");
            app.Resources["BooleanToVisibilityConverter"] = new BooleanToVisibilityConverter();
            VerifyAdmin(audit);
        }
        catch (Exception ex)
        {
            audit.Errors.Add(ex.ToString());
        }
        finally
        {
            PresentationTraceSources.DataBindingSource.Listeners.Remove(bindingErrors);
            app.Shutdown();
            playerLog.MarkGracefulShutdown("STA contract test complete");
            playerLog.CompleteLifetime();
            adminLog.MarkGracefulShutdown("STA contract test complete");
            adminLog.CompleteLifetime();
        }

        audit.Status = audit.Errors.Count == 0
                       && audit.BindingErrors.Count == 0
                       && audit.Checks.Count >= 20
                       && audit.Checks.Values.All(value => value)
            ? "PASS"
            : "NOT_PASS";
        WriteAudit(output, audit);
        Console.WriteLine($"DEV-VERIFY-1 runtime UI automation contracts: {audit.Status}");
        Console.WriteLine($"Artifact: {output}");
        return audit.Status == "PASS" ? 0 : 1;
    }

    private static void VerifyPlayer(ContractAudit audit)
    {
        var transport = new PlayerFakeTransport();
        var viewModel = new PlayerProductionViewModel(new PlayerApi(transport), () => "character-contract");
        viewModel.RefreshLimitedProductionCommand0196.Execute(null);
        var view = new PlayerProductionView { DataContext = viewModel };

        using var scope = new MaterializationScope(view);
        var selector = RequireVisible<ListBox>(scope, view, "PlayerLimitedProduction_PrototypeSelector");
        Check(audit, "player.selector.stableAutomationId",
            AutomationProperties.GetAutomationId(selector) == "PlayerLimitedProduction_PrototypeSelector");
        Check(audit, "player.selector.readableName",
            !string.IsNullOrWhiteSpace(AutomationProperties.GetName(selector)));
        Check(audit, "player.selector.materialized", selector.Items.Count == 2);

        selector.SelectedIndex = 1;
        selector.ScrollIntoView(selector.SelectedItem);
        scope.Update();
        var selectedCandidate = viewModel.SelectedLimitedCandidate0196;
        Check(audit, "player.selector.selectionCommits",
            selectedCandidate?.PrototypeId == "prototype-contract-b");
        var selectedName = RequireVisible<TextBlock>(
            scope, view, "PlayerLimitedProduction_PrototypeSelector_SelectedName");
        Check(audit, "player.selector.selectedReadableValue",
            selectedName.Text.Contains("Contract B"));
        var selectedContainer = selector.ItemContainerGenerator.ContainerFromIndex(1) as ListBoxItem;
        Check(audit, "player.selector.scrollIntoView", selectedContainer?.IsVisible == true);
        Check(audit, "player.selector.itemReadableName",
            selectedContainer != null
            && AutomationProperties.GetName(selectedContainer).Contains("Contract B"));
        var peer = selectedContainer == null
            ? null
            : new ListBoxItemAutomationPeer(selector.Items[1], new ListBoxAutomationPeer(selector));
        var selectionPattern = peer?.GetPattern(PatternInterface.SelectionItem);
        Check(audit, "player.selector.standardPatternOrFallback",
            selectionPattern != null || selectedContainer?.IsMouseDirectlyOver == true);
        Check(audit, "player.preview.canExecute",
            viewModel.PreviewLimitedProductionCommand0196.CanExecute(null));

        viewModel.RefreshLimitedProductionCommand0196.Execute(null);
        Check(audit, "player.selector.refreshPreservesSelection",
            viewModel.SelectedLimitedCandidate0196?.PrototypeId == "prototype-contract-b");

        var projectList = RequireVisible<ListBox>(scope, view, "PlayerLimitedProduction_ProjectList");
        Check(audit, "player.completedCards.materialized", projectList.Items.Count == 2);
        var completedContainer = projectList.ItemContainerGenerator.ContainerFromIndex(0) as ListBoxItem;
        Check(audit, "player.completedCards.readableName",
            completedContainer != null
            && AutomationProperties.GetName(completedContainer).Contains("Completed contract batch"));

        var stages = RequireVisible<ListBox>(scope, view, "PlayerLimitedProduction_Stages");
        Check(audit, "player.stages.stableAutomationId", stages.Items.Count == 2);
        var stageContainer = stages.ItemContainerGenerator.ContainerFromIndex(0) as ListBoxItem;
        Check(audit, "player.stages.readableName",
            stageContainer != null && !string.IsNullOrWhiteSpace(AutomationProperties.GetName(stageContainer)));
    }

    private static void VerifyAdmin(ContractAudit audit)
    {
        var transport = new AdminFakeTransport();
        var viewModel = new AdminCraftingViewModel(new AdminApi(transport));
        viewModel.SelectedProjectKind = viewModel.ProjectKinds.Single(item => item.Key == "limited_production");
        var view = new AdminCraftingView { DataContext = viewModel };

        using var scope = new MaterializationScope(view);
        var list = RequireVisible<ListBox>(scope, view, "AdminCraftProject_List");
        Check(audit, "admin.projectList.materialized", list.Items.Count == 2);
        list.SelectedIndex = 1;
        list.ScrollIntoView(list.SelectedItem);
        scope.Update();
        Check(audit, "admin.projectList.selectionCommits",
            viewModel.SelectedProject?.ProjectId == "admin-project-b");
        var selectedContainer = list.ItemContainerGenerator.ContainerFromIndex(1) as ListBoxItem;
        Check(audit, "admin.projectList.readableName",
            selectedContainer != null
            && AutomationProperties.GetName(selectedContainer).Contains("Admin contract B"));

        viewModel.RefreshCommand.Execute(null);
        Check(audit, "admin.projectList.refreshPreservesSelection",
            viewModel.SelectedProject?.ProjectId == "admin-project-b");

        var requirements = RequireVisible<ListBox>(scope, view, "AdminCraftProject_Requirements");
        Check(audit, "admin.requirements.materialized", requirements.Items.Count == 1);
        requirements.SelectedIndex = 0;
        scope.Update();
        Check(audit, "admin.requirements.selectionCommits",
            viewModel.SelectedRequirement != null);
        Check(audit, "admin.requirements.canExecute",
            viewModel.CanConfirmSelectedRequirement
            && viewModel.ConfirmRequirementCommand.CanExecute(null));

        var stages = RequireVisible<ListBox>(scope, view, "AdminCraftProject_Stages");
        Check(audit, "admin.stages.stableAutomationId", stages.Items.Count == 2);
        var stageContainer = stages.ItemContainerGenerator.ContainerFromIndex(0) as ListBoxItem;
        Check(audit, "admin.stages.readableName",
            stageContainer != null && !string.IsNullOrWhiteSpace(AutomationProperties.GetName(stageContainer)));

        var history = RequireVisible<ListBox>(scope, view, "AdminCraftProject_Audit");
        Check(audit, "admin.history.stableAutomationId", history.Items.Count == 1);
        var historyContainer = history.ItemContainerGenerator.ContainerFromIndex(0) as ListBoxItem;
        Check(audit, "admin.history.readableName",
            historyContainer != null && !string.IsNullOrWhiteSpace(AutomationProperties.GetName(historyContainer)));
        Check(audit, "admin.complete.canExecute",
            viewModel.CompleteCommand.CanExecute(null));
    }

    private static void Check(ContractAudit audit, string name, bool result)
    {
        audit.Checks[name] = result;
        if (!result)
            audit.Errors.Add("Contract failed: " + name);
    }

    private static void LoadTheme(Application app, string assembly, string file)
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
            Source = new Uri($"pack://application:,,,/{assembly};component/Resources/{file}", UriKind.Absolute)
        });
    }

    private static T RequireVisible<T>(
        MaterializationScope scope,
        DependencyObject root,
        string automationId) where T : FrameworkElement
    {
        var logical = LogicalDescendants(root)
            .OfType<FrameworkElement>()
            .FirstOrDefault(item => AutomationProperties.GetAutomationId(item) == automationId);
        if (logical == null)
            throw new InvalidOperationException("Automation element not found: " + automationId);
        RevealAncestors(logical);
        scope.Update();
        var visible = VisualDescendants(root)
            .OfType<T>()
            .FirstOrDefault(item => AutomationProperties.GetAutomationId(item) == automationId);
        if (visible == null || visible.ActualWidth <= 0 || visible.ActualHeight <= 0)
            throw new InvalidOperationException("Automation element did not materialize: " + automationId);
        return visible;
    }

    private static void RevealAncestors(DependencyObject element)
    {
        var tabs = new Stack<TabItem>();
        for (var current = element; current != null; current = LogicalTreeHelper.GetParent(current))
        {
            if (current is TabItem tab)
                tabs.Push(tab);
        }
        while (tabs.Count > 0)
        {
            var tab = tabs.Pop();
            if (ItemsControl.ItemsControlFromItemContainer(tab) is TabControl owner)
                owner.SelectedItem = tab;
        }
    }

    private static IEnumerable<DependencyObject> LogicalDescendants(DependencyObject root)
    {
        yield return root;
        foreach (var value in LogicalTreeHelper.GetChildren(root))
        {
            if (value is DependencyObject child)
            {
                foreach (var item in LogicalDescendants(child))
                    yield return item;
            }
        }
    }

    private static IEnumerable<DependencyObject> VisualDescendants(DependencyObject root)
    {
        yield return root;
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var index = 0; index < count; index++)
        {
            foreach (var child in VisualDescendants(VisualTreeHelper.GetChild(root, index)))
                yield return child;
        }
    }

    private static void WriteAudit(string path, ContractAudit audit)
    {
        var serializer = new DataContractJsonSerializer(
            typeof(ContractAudit),
            new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true });
        using var stream = File.Create(path);
        serializer.WriteObject(stream, audit);
    }

    private static ResponseEnvelope Ok(Dictionary<string, object>? payload = null)
        => new()
        {
            Status = ResponseStatus.Ok,
            Message = "Contract fixture",
            Payload = payload ?? new Dictionary<string, object> { ["items"] = Array.Empty<object>() }
        };

    private static Dictionary<string, object> Candidate(string id, string name) => new()
    {
        ["prototypeId"] = id,
        ["name"] = name,
        ["blueprintName"] = "Readable " + name,
        ["remainingUnits"] = 3,
        ["producedUnits"] = 0,
        ["status"] = "active"
    };

    private static Dictionary<string, object> Project(string id, string name) => new()
    {
        ["projectId"] = id,
        ["name"] = name,
        ["blueprintName"] = "Readable blueprint",
        ["status"] = "completed",
        ["statusLabel"] = "Completed",
        ["currentStageName"] = "Complete",
        ["batchSize"] = 3,
        ["revision"] = 9,
        ["progressPercent"] = 100,
        ["ownerDisplayName"] = "dev_player",
        ["ownerCharacterDisplayName"] = "Contract Character",
        ["projectTypeLabel"] = "Limited production",
        ["requirements"] = new object[]
        {
            new Dictionary<string, object>
            {
                ["requirementId"] = "requirement-contract",
                ["name"] = "Readable requirement",
                ["status"] = "gm_confirmation",
                ["statusLabel"] = "Needs confirmation",
                ["summary"] = "Server requirement"
            }
        },
        ["resources"] = new object[]
        {
            new Dictionary<string, object>
            {
                ["name"] = "Readable resource", ["quantityRequired"] = 3,
                ["unit"] = "unit", ["status"] = "reserved", ["statusLabel"] = "Reserved"
            }
        },
        ["stages"] = new object[]
        {
            new Dictionary<string, object> { ["name"] = "Stage one", ["status"] = "completed", ["statusLabel"] = "Completed" },
            new Dictionary<string, object> { ["name"] = "Stage two", ["status"] = "completed", ["statusLabel"] = "Completed" }
        },
        ["audit"] = new object[]
        {
            new Dictionary<string, object> { ["action"] = "completed", ["summary"] = "Project completed", ["actorDisplayName"] = "dev_admin" }
        },
        ["result"] = new Dictionary<string, object> { ["name"] = "Readable result", ["quantity"] = 3, ["summary"] = "Three items" }
    };

    private sealed class PlayerFakeTransport : PlayerTransport
    {
        public string ServerHost => "contract.test";
        public int ServerPort => 0;
        public void Connect() { }
        public void Disconnect() { }
        public void UpdateEndpoint(string host, int port) { }
        public void Dispose() { }
        public ResponseEnvelope Send(RequestEnvelope request)
        {
            if (request.Command == CommandNames.ProjectLimitedProductionAvailableList)
                return Ok(new Dictionary<string, object>
                {
                    ["items"] = new object[]
                    {
                        Candidate("prototype-contract-a", "Contract A"),
                        Candidate("prototype-contract-b", "Contract B")
                    }
                });
            if (request.Command == CommandNames.ProjectLimitedProductionList)
                return Ok(new Dictionary<string, object>
                {
                    ["items"] = new object[]
                    {
                        Project("player-project-a", "Completed contract batch"),
                        Project("player-project-b", "Second contract batch")
                    }
                });
            if (request.Command == CommandNames.ProjectLimitedProductionGet)
            {
                var id = request.Payload.TryGetValue("projectId", out var value)
                    ? Convert.ToString(value) ?? "player-project-a"
                    : "player-project-a";
                return Ok(new Dictionary<string, object> { ["item"] = Project(id, id == "player-project-b" ? "Second contract batch" : "Completed contract batch") });
            }
            return Ok();
        }
    }

    private sealed class AdminFakeTransport : AdminTransport
    {
        public string ServerHost => "contract.test";
        public int ServerPort => 0;
        public void Connect() { }
        public void Disconnect() { }
        public void UpdateEndpoint(string host, int port) { }
        public void Dispose() { }
        public ResponseEnvelope Send(RequestEnvelope request)
        {
            if (request.Command == CommandNames.ProjectLimitedProductionList)
                return Ok(new Dictionary<string, object>
                {
                    ["items"] = new object[]
                    {
                        Project("admin-project-a", "Admin contract A"),
                        Project("admin-project-b", "Admin contract B")
                    }
                });
            if (request.Command == CommandNames.ProjectLimitedProductionGet)
            {
                var id = request.Payload.TryGetValue("projectId", out var value)
                    ? Convert.ToString(value) ?? "admin-project-a"
                    : "admin-project-a";
                return Ok(new Dictionary<string, object> { ["item"] = Project(id, id == "admin-project-b" ? "Admin contract B" : "Admin contract A") });
            }
            return Ok();
        }
    }

    private sealed class MaterializationScope : IDisposable
    {
        private readonly Window _window;
        public MaterializationScope(FrameworkElement content)
        {
            _window = new Window
            {
                Content = content,
                Width = 1500,
                Height = 900,
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
            if (_window.Content is FrameworkElement content) content.ApplyTemplate();
            _window.Measure(new Size(1500, 900));
            _window.Arrange(new Rect(0, 0, 1500, 900));
            _window.UpdateLayout();
            _window.Dispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(() => { }));
        }
        public void Dispose() => _window.Close();
    }

    private sealed class BindingTraceListener : TraceListener
    {
        private readonly List<string> _messages;
        private string _line = string.Empty;
        public BindingTraceListener(List<string> messages) => _messages = messages;
        public override void Write(string? message) => _line += message;
        public override void WriteLine(string? message)
        {
            _line += message;
            if (!string.IsNullOrWhiteSpace(_line) && !_messages.Contains(_line.Trim()))
                _messages.Add(_line.Trim());
            _line = string.Empty;
        }
    }
}

[DataContract]
internal sealed class ContractAudit
{
    [DataMember(Name = "milestone", Order = 1)] public string Milestone { get; set; } = "DEV-VERIFY-1";
    [DataMember(Name = "status", Order = 2)] public string Status { get; set; } = "NOT_PASS";
    [DataMember(Name = "staThread", Order = 3)] public bool StaThread { get; set; } = true;
    [DataMember(Name = "actualViewsAndViewModels", Order = 4)] public bool ActualViewsAndViewModels { get; set; } = true;
    [DataMember(Name = "checks", Order = 5)] public Dictionary<string, bool> Checks { get; } = new();
    [DataMember(Name = "bindingErrors", Order = 6)] public List<string> BindingErrors { get; } = new();
    [DataMember(Name = "errors", Order = 7)] public List<string> Errors { get; } = new();
}
