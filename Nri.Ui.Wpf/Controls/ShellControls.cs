using System;
using System.Collections;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;

namespace Nri.Ui.Wpf.Controls
{
    public sealed class NriAccessibleRelationship : ContentControl
    {
        protected override AutomationPeer OnCreateAutomationPeer()
            => new NriAccessibleRelationshipAutomationPeer(this);
    }

    internal sealed class NriAccessibleRelationshipAutomationPeer : FrameworkElementAutomationPeer
    {
        public NriAccessibleRelationshipAutomationPeer(NriAccessibleRelationship owner) : base(owner) { }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;
        protected override string GetClassNameCore() => nameof(NriAccessibleRelationship);
        protected override bool IsControlElementCore() => true;
        protected override bool IsContentElementCore() => true;
    }

    public sealed class NriSemanticRegion : ContentControl
    {
        protected override AutomationPeer OnCreateAutomationPeer()
            => new NriSemanticRegionAutomationPeer(this);
    }

    internal sealed class NriSemanticRegionAutomationPeer : FrameworkElementAutomationPeer
    {
        public NriSemanticRegionAutomationPeer(NriSemanticRegion owner) : base(owner) { }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
        protected override string GetClassNameCore() => nameof(NriSemanticRegion);
        protected override bool IsControlElementCore() => true;
        protected override bool IsContentElementCore() => true;
    }

    public interface IUnsavedChangesAware
    {
        bool HasUnsavedChanges { get; }
        string UnsavedChangesSummary { get; }
        Task<bool> CanNavigateAwayAsync();
    }

    public sealed class NriShellRouteDefinition : INotifyPropertyChanged
    {
        private bool _isSelected;
        private bool _isVisible = true;
        private bool _isEnabled = true;

        public string RouteKey { get; set; } = "";
        public string Title { get; set; } = "";
        public string ShortTitle { get; set; } = "";
        public string Description { get; set; } = "";
        public string IconKey { get; set; } = "";
        public string ApplicationArea { get; set; } = "";
        public string NavigationGroup { get; set; } = "";
        public int DisplayOrder { get; set; }
        public string RequiredRoles { get; set; } = "";
        public string RequiredFeatureFlags { get; set; } = "";
        public string DisabledReason { get; set; } = "";
        public bool IsPlaceholder { get; set; }
        public object? ViewKey { get; set; }

        public bool IsSelected
        {
            get => _isSelected;
            set { if (_isSelected == value) return; _isSelected = value; OnPropertyChanged(); }
        }

        public bool IsVisible
        {
            get => _isVisible;
            set { if (_isVisible == value) return; _isVisible = value; OnPropertyChanged(); }
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set { if (_isEnabled == value) return; _isEnabled = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name ?? string.Empty));
    }

    public abstract class NriShellControl : ContentControl
    {
        protected static DependencyProperty Register<TControl, TValue>(string name, TValue defaultValue = default!)
            where TControl : DependencyObject => DependencyProperty.Register(name, typeof(TValue), typeof(TControl), new PropertyMetadata(defaultValue));

        protected override AutomationPeer OnCreateAutomationPeer() => new NriShellAutomationPeer(this);
    }

    internal sealed class NriShellAutomationPeer : FrameworkElementAutomationPeer
    {
        internal NriShellAutomationPeer(FrameworkElement owner) : base(owner) { }
        protected override string GetClassNameCore() => Owner.GetType().Name;
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Pane;
        protected override bool IsControlElementCore() => true;
        protected override bool IsContentElementCore() => true;
    }

    public sealed class NriShellHeader : NriShellControl
    {
        static NriShellHeader() => DefaultStyleKeyProperty.OverrideMetadata(typeof(NriShellHeader), new FrameworkPropertyMetadata(typeof(NriShellHeader)));
        public NriShellHeader() => AutomationProperties.SetName(this, "Заголовок приложения");
        public static readonly DependencyProperty ProductNameProperty = Register<NriShellHeader, string>(nameof(ProductName), "NRI System USSE");
        public static readonly DependencyProperty ContextTitleProperty = Register<NriShellHeader, string>(nameof(ContextTitle), "Контекст не выбран");
        public static readonly DependencyProperty ContextSubtitleProperty = Register<NriShellHeader, string>(nameof(ContextSubtitle), "");
        public static readonly DependencyProperty ConnectionTextProperty = Register<NriShellHeader, string>(nameof(ConnectionText), "Нет подключения");
        public static readonly DependencyProperty UserDisplayNameProperty = Register<NriShellHeader, string>(nameof(UserDisplayName), "Гость");
        public static readonly DependencyProperty ActionsProperty = Register<NriShellHeader, object>(nameof(Actions));
        public string ProductName { get => (string)GetValue(ProductNameProperty); set => SetValue(ProductNameProperty, value); }
        public string ContextTitle { get => (string)GetValue(ContextTitleProperty); set => SetValue(ContextTitleProperty, value); }
        public string ContextSubtitle { get => (string)GetValue(ContextSubtitleProperty); set => SetValue(ContextSubtitleProperty, value); }
        public string ConnectionText { get => (string)GetValue(ConnectionTextProperty); set => SetValue(ConnectionTextProperty, value); }
        public string UserDisplayName { get => (string)GetValue(UserDisplayNameProperty); set => SetValue(UserDisplayNameProperty, value); }
        public object Actions { get => GetValue(ActionsProperty); set => SetValue(ActionsProperty, value); }
    }

    public sealed class NriModeSwitcher : ListBox
    {
        static NriModeSwitcher() => DefaultStyleKeyProperty.OverrideMetadata(typeof(NriModeSwitcher), new FrameworkPropertyMetadata(typeof(NriModeSwitcher)));
        public static readonly DependencyProperty IsCompactProperty = DependencyProperty.Register(nameof(IsCompact), typeof(bool), typeof(NriModeSwitcher), new PropertyMetadata(false));
        public bool IsCompact { get => (bool)GetValue(IsCompactProperty); set => SetValue(IsCompactProperty, value); }
        protected override AutomationPeer OnCreateAutomationPeer() => new ListBoxAutomationPeer(this);
    }

    public sealed class NriNavigationRail : ListBox
    {
        static NriNavigationRail() => DefaultStyleKeyProperty.OverrideMetadata(typeof(NriNavigationRail), new FrameworkPropertyMetadata(typeof(NriNavigationRail)));
        public static readonly DependencyProperty IsCollapsedProperty = DependencyProperty.Register(nameof(IsCollapsed), typeof(bool), typeof(NriNavigationRail), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
        public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(nameof(Header), typeof(string), typeof(NriNavigationRail), new PropertyMetadata("Разделы"));
        public bool IsCollapsed { get => (bool)GetValue(IsCollapsedProperty); set => SetValue(IsCollapsedProperty, value); }
        public string Header { get => (string)GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }
        protected override AutomationPeer OnCreateAutomationPeer() => new ListBoxAutomationPeer(this);
    }

    public sealed class NriRouteHost : NriShellControl
    {
        static NriRouteHost() => DefaultStyleKeyProperty.OverrideMetadata(typeof(NriRouteHost), new FrameworkPropertyMetadata(typeof(NriRouteHost)));
        public static readonly DependencyProperty RouteTitleProperty = Register<NriRouteHost, string>(nameof(RouteTitle), "Раздел");
        public static readonly DependencyProperty RouteSubtitleProperty = Register<NriRouteHost, string>(nameof(RouteSubtitle), "");
        public static readonly DependencyProperty BreadcrumbsProperty = Register<NriRouteHost, string>(nameof(Breadcrumbs), "");
        public static readonly DependencyProperty HeaderVisibilityProperty = Register<NriRouteHost, Visibility>(nameof(HeaderVisibility), Visibility.Visible);
        public string RouteTitle { get => (string)GetValue(RouteTitleProperty); set => SetValue(RouteTitleProperty, value); }
        public string RouteSubtitle { get => (string)GetValue(RouteSubtitleProperty); set => SetValue(RouteSubtitleProperty, value); }
        public string Breadcrumbs { get => (string)GetValue(BreadcrumbsProperty); set => SetValue(BreadcrumbsProperty, value); }
        public Visibility HeaderVisibility { get => (Visibility)GetValue(HeaderVisibilityProperty); set => SetValue(HeaderVisibilityProperty, value); }
    }

    public sealed class NriActivityDock : NriShellControl
    {
        static NriActivityDock() => DefaultStyleKeyProperty.OverrideMetadata(typeof(NriActivityDock), new FrameworkPropertyMetadata(typeof(NriActivityDock)));
        public static readonly DependencyProperty IsOpenProperty = Register<NriActivityDock, bool>(nameof(IsOpen), true);
        public static readonly DependencyProperty HeaderProperty = Register<NriActivityDock, string>(nameof(Header), "Активность");
        public static readonly DependencyProperty ToggleCommandProperty = Register<NriActivityDock, ICommand>(nameof(ToggleCommand));
        public static readonly DependencyProperty CompactWhenClosedProperty = Register<NriActivityDock, bool>(nameof(CompactWhenClosed), false);
        public bool IsOpen { get => (bool)GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }
        public string Header { get => (string)GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }
        public ICommand ToggleCommand { get => (ICommand)GetValue(ToggleCommandProperty); set => SetValue(ToggleCommandProperty, value); }
        public bool CompactWhenClosed { get => (bool)GetValue(CompactWhenClosedProperty); set => SetValue(CompactWhenClosedProperty, value); }
    }

    public abstract class NriShellDialog : NriShellControl
    {
        public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(NriShellDialog), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
        public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string), typeof(NriShellDialog), new PropertyMetadata("Диалог"));
        public static readonly DependencyProperty MessageProperty = DependencyProperty.Register(nameof(Message), typeof(string), typeof(NriShellDialog), new PropertyMetadata(""));
        public static readonly DependencyProperty CloseCommandProperty = DependencyProperty.Register(nameof(CloseCommand), typeof(ICommand), typeof(NriShellDialog));
        public bool IsOpen { get => (bool)GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }
        public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
        public string Message { get => (string)GetValue(MessageProperty); set => SetValue(MessageProperty, value); }
        public ICommand? CloseCommand { get => (ICommand?)GetValue(CloseCommandProperty); set => SetValue(CloseCommandProperty, value); }

    }

    public sealed class NriConnectionDialog : NriShellDialog
    {
        static NriConnectionDialog() => DefaultStyleKeyProperty.OverrideMetadata(typeof(NriConnectionDialog), new FrameworkPropertyMetadata(typeof(NriConnectionDialog)));
        public NriConnectionDialog() { Title = "Подключение"; AutomationProperties.SetName(this, "Подключение к серверу"); }
    }

    public sealed class NriLoginDialog : NriShellDialog
    {
        static NriLoginDialog() => DefaultStyleKeyProperty.OverrideMetadata(typeof(NriLoginDialog), new FrameworkPropertyMetadata(typeof(NriLoginDialog)));
        public NriLoginDialog() { Title = "Вход"; AutomationProperties.SetName(this, "Вход в систему"); }
    }

    public sealed class NriPermissionState : NriShellControl
    {
        static NriPermissionState() => DefaultStyleKeyProperty.OverrideMetadata(typeof(NriPermissionState), new FrameworkPropertyMetadata(typeof(NriPermissionState)));
        public static readonly DependencyProperty TitleProperty = Register<NriPermissionState, string>(nameof(Title), "Раздел недоступен");
        public static readonly DependencyProperty ExplanationProperty = Register<NriPermissionState, string>(nameof(Explanation), "Недостаточно прав для просмотра.");
        public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
        public string Explanation { get => (string)GetValue(ExplanationProperty); set => SetValue(ExplanationProperty, value); }
    }
}
