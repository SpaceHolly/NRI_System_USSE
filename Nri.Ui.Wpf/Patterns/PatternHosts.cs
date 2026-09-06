using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace Nri.Ui.Wpf.Patterns
{
    internal sealed class NriPatternAutomationPeer : FrameworkElementAutomationPeer
    {
        internal NriPatternAutomationPeer(FrameworkElement owner) : base(owner) { }
        protected override string GetClassNameCore() => Owner.GetType().Name;
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Pane;
        protected override bool IsControlElementCore() => true;
        protected override bool IsContentElementCore() => true;
    }

    public enum NriContentState { Populated, Empty, NoResults, Loading, Error }

    public abstract class NriPatternHost : Control
    {
        protected static DependencyProperty Region<T>(string name) => DependencyProperty.Register(name, typeof(T), typeof(NriPatternHost));
    }

    public class NriCollectionBrowserHost : Control
    {
        static NriCollectionBrowserHost() { DefaultStyleKeyProperty.OverrideMetadata(typeof(NriCollectionBrowserHost), new FrameworkPropertyMetadata(typeof(NriCollectionBrowserHost))); }
        protected override AutomationPeer OnCreateAutomationPeer() => new NriPatternAutomationPeer(this);
        public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(nameof(Header), typeof(object), typeof(NriCollectionBrowserHost));
        public static readonly DependencyProperty NavigationProperty = DependencyProperty.Register(nameof(Navigation), typeof(object), typeof(NriCollectionBrowserHost));
        public static readonly DependencyProperty ToolbarProperty = DependencyProperty.Register(nameof(Toolbar), typeof(object), typeof(NriCollectionBrowserHost));
        public static readonly DependencyProperty CollectionProperty = DependencyProperty.Register(nameof(Collection), typeof(object), typeof(NriCollectionBrowserHost));
        public static readonly DependencyProperty InspectorProperty = DependencyProperty.Register(nameof(Inspector), typeof(object), typeof(NriCollectionBrowserHost));
        public static readonly DependencyProperty IsInspectorOpenProperty = DependencyProperty.Register(nameof(IsInspectorOpen), typeof(bool), typeof(NriCollectionBrowserHost), new PropertyMetadata(true));
        public static readonly DependencyProperty ContentStateProperty = DependencyProperty.Register(nameof(ContentState), typeof(NriContentState), typeof(NriCollectionBrowserHost), new PropertyMetadata(NriContentState.Populated));
        public object Header { get => GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }
        public object Navigation { get => GetValue(NavigationProperty); set => SetValue(NavigationProperty, value); }
        public object Toolbar { get => GetValue(ToolbarProperty); set => SetValue(ToolbarProperty, value); }
        public object Collection { get => GetValue(CollectionProperty); set => SetValue(CollectionProperty, value); }
        public object Inspector { get => GetValue(InspectorProperty); set => SetValue(InspectorProperty, value); }
        public bool IsInspectorOpen { get => (bool)GetValue(IsInspectorOpenProperty); set => SetValue(IsInspectorOpenProperty, value); }
        public NriContentState ContentState { get => (NriContentState)GetValue(ContentStateProperty); set => SetValue(ContentStateProperty, value); }
    }

    public class NriEntityEditorHost : Control
    {
        static NriEntityEditorHost() { DefaultStyleKeyProperty.OverrideMetadata(typeof(NriEntityEditorHost), new FrameworkPropertyMetadata(typeof(NriEntityEditorHost))); }
        protected override AutomationPeer OnCreateAutomationPeer() => new NriPatternAutomationPeer(this);
        public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(nameof(Header), typeof(object), typeof(NriEntityEditorHost));
        public static readonly DependencyProperty RecordListProperty = DependencyProperty.Register(nameof(RecordList), typeof(object), typeof(NriEntityEditorHost));
        public static readonly DependencyProperty CommandBarProperty = DependencyProperty.Register(nameof(CommandBar), typeof(object), typeof(NriEntityEditorHost));
        public static readonly DependencyProperty FormProperty = DependencyProperty.Register(nameof(Form), typeof(object), typeof(NriEntityEditorHost));
        public static readonly DependencyProperty InspectorProperty = DependencyProperty.Register(nameof(Inspector), typeof(object), typeof(NriEntityEditorHost));
        public static readonly DependencyProperty RecordListWidthProperty = DependencyProperty.Register(nameof(RecordListWidth), typeof(GridLength), typeof(NriEntityEditorHost), new PropertyMetadata(new GridLength(340)));
        public static readonly DependencyProperty IsInspectorOpenProperty = DependencyProperty.Register(nameof(IsInspectorOpen), typeof(bool), typeof(NriEntityEditorHost), new PropertyMetadata(true));
        public static readonly DependencyProperty HasUnsavedChangesProperty = DependencyProperty.Register(nameof(HasUnsavedChanges), typeof(bool), typeof(NriEntityEditorHost), new PropertyMetadata(false));
        public object Header { get => GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }
        public object RecordList { get => GetValue(RecordListProperty); set => SetValue(RecordListProperty, value); }
        public object CommandBar { get => GetValue(CommandBarProperty); set => SetValue(CommandBarProperty, value); }
        public object Form { get => GetValue(FormProperty); set => SetValue(FormProperty, value); }
        public object Inspector { get => GetValue(InspectorProperty); set => SetValue(InspectorProperty, value); }
        public GridLength RecordListWidth { get => (GridLength)GetValue(RecordListWidthProperty); set => SetValue(RecordListWidthProperty, value); }
        public bool IsInspectorOpen { get => (bool)GetValue(IsInspectorOpenProperty); set => SetValue(IsInspectorOpenProperty, value); }
        public bool HasUnsavedChanges { get => (bool)GetValue(HasUnsavedChangesProperty); set => SetValue(HasUnsavedChangesProperty, value); }
    }

    public class NriPlayerDetailHost : Control
    {
        static NriPlayerDetailHost() { DefaultStyleKeyProperty.OverrideMetadata(typeof(NriPlayerDetailHost), new FrameworkPropertyMetadata(typeof(NriPlayerDetailHost))); }
        protected override AutomationPeer OnCreateAutomationPeer() => new NriPatternAutomationPeer(this);
        public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(nameof(Header), typeof(object), typeof(NriPlayerDetailHost));
        public static readonly DependencyProperty SummaryProperty = DependencyProperty.Register(nameof(Summary), typeof(object), typeof(NriPlayerDetailHost));
        public static readonly DependencyProperty FactsProperty = DependencyProperty.Register(nameof(Facts), typeof(object), typeof(NriPlayerDetailHost));
        public static readonly DependencyProperty ActionsProperty = DependencyProperty.Register(nameof(Actions), typeof(object), typeof(NriPlayerDetailHost));
        public static readonly DependencyProperty RelatedContentProperty = DependencyProperty.Register(nameof(RelatedContent), typeof(object), typeof(NriPlayerDetailHost));
        public object Header { get => GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }
        public object Summary { get => GetValue(SummaryProperty); set => SetValue(SummaryProperty, value); }
        public object Facts { get => GetValue(FactsProperty); set => SetValue(FactsProperty, value); }
        public object Actions { get => GetValue(ActionsProperty); set => SetValue(ActionsProperty, value); }
        public object RelatedContent { get => GetValue(RelatedContentProperty); set => SetValue(RelatedContentProperty, value); }
    }

    public class NriDashboardHost : Control
    {
        static NriDashboardHost() { DefaultStyleKeyProperty.OverrideMetadata(typeof(NriDashboardHost), new FrameworkPropertyMetadata(typeof(NriDashboardHost))); }
        protected override AutomationPeer OnCreateAutomationPeer() => new NriPatternAutomationPeer(this);
        public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(nameof(Header), typeof(object), typeof(NriDashboardHost));
        public static readonly DependencyProperty TasksProperty = DependencyProperty.Register(nameof(Tasks), typeof(object), typeof(NriDashboardHost));
        public static readonly DependencyProperty ExceptionsProperty = DependencyProperty.Register(nameof(Exceptions), typeof(object), typeof(NriDashboardHost));
        public static readonly DependencyProperty EventsProperty = DependencyProperty.Register(nameof(Events), typeof(object), typeof(NriDashboardHost));
        public static readonly DependencyProperty QuickActionsProperty = DependencyProperty.Register(nameof(QuickActions), typeof(object), typeof(NriDashboardHost));
        public object Header { get => GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }
        public object Tasks { get => GetValue(TasksProperty); set => SetValue(TasksProperty, value); }
        public object Exceptions { get => GetValue(ExceptionsProperty); set => SetValue(ExceptionsProperty, value); }
        public object Events { get => GetValue(EventsProperty); set => SetValue(EventsProperty, value); }
        public object QuickActions { get => GetValue(QuickActionsProperty); set => SetValue(QuickActionsProperty, value); }
    }

    public class NriOperationalWorkspaceHost : ContentControl
    {
        static NriOperationalWorkspaceHost() { DefaultStyleKeyProperty.OverrideMetadata(typeof(NriOperationalWorkspaceHost), new FrameworkPropertyMetadata(typeof(NriOperationalWorkspaceHost))); }
        public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(nameof(Header), typeof(object), typeof(NriOperationalWorkspaceHost));
        public static readonly DependencyProperty ToolbarProperty = DependencyProperty.Register(nameof(Toolbar), typeof(object), typeof(NriOperationalWorkspaceHost));
        public static readonly DependencyProperty InspectorProperty = DependencyProperty.Register(nameof(Inspector), typeof(object), typeof(NriOperationalWorkspaceHost));
        public static readonly DependencyProperty StatusProperty = DependencyProperty.Register(nameof(Status), typeof(object), typeof(NriOperationalWorkspaceHost));
        public static readonly DependencyProperty IsInspectorOpenProperty = DependencyProperty.Register(nameof(IsInspectorOpen), typeof(bool), typeof(NriOperationalWorkspaceHost), new PropertyMetadata(false));
        public object Header { get => GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }
        public object Toolbar { get => GetValue(ToolbarProperty); set => SetValue(ToolbarProperty, value); }
        public object Inspector { get => GetValue(InspectorProperty); set => SetValue(InspectorProperty, value); }
        public object Status { get => GetValue(StatusProperty); set => SetValue(StatusProperty, value); }
        public bool IsInspectorOpen { get => (bool)GetValue(IsInspectorOpenProperty); set => SetValue(IsInspectorOpenProperty, value); }
    }

    public class NriQueueWorkspaceHost : Control
    {
        static NriQueueWorkspaceHost() { DefaultStyleKeyProperty.OverrideMetadata(typeof(NriQueueWorkspaceHost), new FrameworkPropertyMetadata(typeof(NriQueueWorkspaceHost))); }
        protected override AutomationPeer OnCreateAutomationPeer() => new NriPatternAutomationPeer(this);
        public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(nameof(Header), typeof(object), typeof(NriQueueWorkspaceHost));
        public static readonly DependencyProperty ToolbarProperty = DependencyProperty.Register(nameof(Toolbar), typeof(object), typeof(NriQueueWorkspaceHost));
        public static readonly DependencyProperty QueueProperty = DependencyProperty.Register(nameof(Queue), typeof(object), typeof(NriQueueWorkspaceHost));
        public static readonly DependencyProperty WorkspaceProperty = DependencyProperty.Register(nameof(Workspace), typeof(object), typeof(NriQueueWorkspaceHost));
        public static readonly DependencyProperty InspectorProperty = DependencyProperty.Register(nameof(Inspector), typeof(object), typeof(NriQueueWorkspaceHost));
        public static readonly DependencyProperty StatusProperty = DependencyProperty.Register(nameof(Status), typeof(object), typeof(NriQueueWorkspaceHost));
        public static readonly DependencyProperty QueueWidthProperty = DependencyProperty.Register(nameof(QueueWidth), typeof(GridLength), typeof(NriQueueWorkspaceHost), new PropertyMetadata(new GridLength(420)));
        public static readonly DependencyProperty IsInspectorOpenProperty = DependencyProperty.Register(nameof(IsInspectorOpen), typeof(bool), typeof(NriQueueWorkspaceHost), new PropertyMetadata(false));
        public object Header { get => GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }
        public object Toolbar { get => GetValue(ToolbarProperty); set => SetValue(ToolbarProperty, value); }
        public object Queue { get => GetValue(QueueProperty); set => SetValue(QueueProperty, value); }
        public object Workspace { get => GetValue(WorkspaceProperty); set => SetValue(WorkspaceProperty, value); }
        public object Inspector { get => GetValue(InspectorProperty); set => SetValue(InspectorProperty, value); }
        public object Status { get => GetValue(StatusProperty); set => SetValue(StatusProperty, value); }
        public GridLength QueueWidth { get => (GridLength)GetValue(QueueWidthProperty); set => SetValue(QueueWidthProperty, value); }
        public bool IsInspectorOpen { get => (bool)GetValue(IsInspectorOpenProperty); set => SetValue(IsInspectorOpenProperty, value); }
    }
}
