using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace Nri.Ui.Wpf.Controls
{
    internal static class NriAutomation
    {
        internal static void BindName(FrameworkElement element, string propertyName)
        {
            element.SetBinding(
                AutomationProperties.NameProperty,
                new Binding(propertyName) { Source = element, Mode = BindingMode.OneWay });
        }
    }

    internal sealed class NriFrameworkElementAutomationPeer : FrameworkElementAutomationPeer
    {
        internal NriFrameworkElementAutomationPeer(FrameworkElement owner) : base(owner) { }
        protected override string GetClassNameCore() => Owner.GetType().Name;
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
        protected override bool IsControlElementCore() => true;
        protected override bool IsContentElementCore() => true;
    }

    public enum NriStatusKind { Neutral, Info, Success, Warning, Danger, Archived, Hidden, Draft, Published }
    public enum NriFeedbackKind { Info, Success, Warning, Danger }

    public abstract class NriControl : Control
    {
        protected static DependencyProperty Register<TControl, TValue>(string name, TValue defaultValue = default!)
            where TControl : DependencyObject => DependencyProperty.Register(name, typeof(TValue), typeof(TControl), new PropertyMetadata(defaultValue));
        protected override AutomationPeer OnCreateAutomationPeer() => new NriFrameworkElementAutomationPeer(this);
    }

    public class NriAutomationAnchor : ContentControl
    {
        protected override AutomationPeer OnCreateAutomationPeer() => new NriFrameworkElementAutomationPeer(this);
    }

    public class NriPageHeader : NriControl
    {
        static NriPageHeader() { DefaultStyleKeyProperty.OverrideMetadata(typeof(NriPageHeader), new FrameworkPropertyMetadata(typeof(NriPageHeader))); }
        public NriPageHeader() { NriAutomation.BindName(this, nameof(Title)); }
        public static readonly DependencyProperty BreadcrumbsProperty = Register<NriPageHeader, string>(nameof(Breadcrumbs), "");
        public static readonly DependencyProperty TitleProperty = Register<NriPageHeader, string>(nameof(Title), "");
        public static readonly DependencyProperty SubtitleProperty = Register<NriPageHeader, string>(nameof(Subtitle), "");
        public static readonly DependencyProperty StatusTextProperty = Register<NriPageHeader, string>(nameof(StatusText), "");
        public static readonly DependencyProperty PrimaryActionProperty = Register<NriPageHeader, object>(nameof(PrimaryAction));
        public static readonly DependencyProperty SecondaryActionsProperty = Register<NriPageHeader, object>(nameof(SecondaryActions));
        public static readonly DependencyProperty HelpContentProperty = Register<NriPageHeader, object>(nameof(HelpContent));
        public string Breadcrumbs { get => (string)GetValue(BreadcrumbsProperty); set => SetValue(BreadcrumbsProperty, value); }
        public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
        public string Subtitle { get => (string)GetValue(SubtitleProperty); set => SetValue(SubtitleProperty, value); }
        public string StatusText { get => (string)GetValue(StatusTextProperty); set => SetValue(StatusTextProperty, value); }
        public object PrimaryAction { get => GetValue(PrimaryActionProperty); set => SetValue(PrimaryActionProperty, value); }
        public object SecondaryActions { get => GetValue(SecondaryActionsProperty); set => SetValue(SecondaryActionsProperty, value); }
        public object HelpContent { get => GetValue(HelpContentProperty); set => SetValue(HelpContentProperty, value); }
    }

    public class NriCommandBar : NriControl
    {
        static NriCommandBar() { DefaultStyleKeyProperty.OverrideMetadata(typeof(NriCommandBar), new FrameworkPropertyMetadata(typeof(NriCommandBar))); }
        public static readonly DependencyProperty PrimaryActionsProperty = Register<NriCommandBar, object>(nameof(PrimaryActions));
        public static readonly DependencyProperty SecondaryActionsProperty = Register<NriCommandBar, object>(nameof(SecondaryActions));
        public static readonly DependencyProperty DestructiveActionsProperty = Register<NriCommandBar, object>(nameof(DestructiveActions));
        public object PrimaryActions { get => GetValue(PrimaryActionsProperty); set => SetValue(PrimaryActionsProperty, value); }
        public object SecondaryActions { get => GetValue(SecondaryActionsProperty); set => SetValue(SecondaryActionsProperty, value); }
        public object DestructiveActions { get => GetValue(DestructiveActionsProperty); set => SetValue(DestructiveActionsProperty, value); }
    }

    public class NriFormField : ContentControl
    {
        static NriFormField() { DefaultStyleKeyProperty.OverrideMetadata(typeof(NriFormField), new FrameworkPropertyMetadata(typeof(NriFormField))); }
        public NriFormField() { NriAutomation.BindName(this, nameof(Label)); }
        protected override AutomationPeer OnCreateAutomationPeer() => new NriFrameworkElementAutomationPeer(this);
        public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(NriFormField), new PropertyMetadata(""));
        public static readonly DependencyProperty HelpTextProperty = DependencyProperty.Register(nameof(HelpText), typeof(string), typeof(NriFormField), new PropertyMetadata(""));
        public static readonly DependencyProperty WarningTextProperty = DependencyProperty.Register(nameof(WarningText), typeof(string), typeof(NriFormField), new PropertyMetadata(""));
        public static readonly DependencyProperty ErrorTextProperty = DependencyProperty.Register(nameof(ErrorText), typeof(string), typeof(NriFormField), new PropertyMetadata(""));
        public static readonly DependencyProperty IsRequiredProperty = DependencyProperty.Register(nameof(IsRequired), typeof(bool), typeof(NriFormField), new PropertyMetadata(false));
        public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
        public string HelpText { get => (string)GetValue(HelpTextProperty); set => SetValue(HelpTextProperty, value); }
        public string WarningText { get => (string)GetValue(WarningTextProperty); set => SetValue(WarningTextProperty, value); }
        public string ErrorText { get => (string)GetValue(ErrorTextProperty); set => SetValue(ErrorTextProperty, value); }
        public bool IsRequired { get => (bool)GetValue(IsRequiredProperty); set => SetValue(IsRequiredProperty, value); }
    }

    public class NriSearchBox : NriControl
    {
        public static readonly RoutedUICommand ClearTextCommand = new RoutedUICommand("Очистить поиск", nameof(ClearTextCommand), typeof(NriSearchBox));
        static NriSearchBox()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(NriSearchBox), new FrameworkPropertyMetadata(typeof(NriSearchBox)));
            CommandManager.RegisterClassCommandBinding(typeof(NriSearchBox), new CommandBinding(ClearTextCommand, (_, e) => ((NriSearchBox)e.Source).Text = ""));
        }
        public NriSearchBox() { NriAutomation.BindName(this, nameof(Placeholder)); }
        public static readonly DependencyProperty TextProperty = DependencyProperty.Register(nameof(Text), typeof(string), typeof(NriSearchBox), new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
        public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register(nameof(Placeholder), typeof(string), typeof(NriSearchBox), new PropertyMetadata("Поиск"));
        public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
        public string Placeholder { get => (string)GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }
    }

    public class NriFilterBar : ContentControl
    {
        static NriFilterBar() { DefaultStyleKeyProperty.OverrideMetadata(typeof(NriFilterBar), new FrameworkPropertyMetadata(typeof(NriFilterBar))); }
        public static readonly DependencyProperty ActiveSummaryProperty = DependencyProperty.Register(nameof(ActiveSummary), typeof(string), typeof(NriFilterBar), new PropertyMetadata(""));
        public static readonly DependencyProperty ClearAllCommandProperty = DependencyProperty.Register(nameof(ClearAllCommand), typeof(ICommand), typeof(NriFilterBar));
        public string ActiveSummary { get => (string)GetValue(ActiveSummaryProperty); set => SetValue(ActiveSummaryProperty, value); }
        public ICommand ClearAllCommand { get => (ICommand)GetValue(ClearAllCommandProperty); set => SetValue(ClearAllCommandProperty, value); }
    }

    public class NriStatusBadge : NriControl
    {
        static NriStatusBadge() { DefaultStyleKeyProperty.OverrideMetadata(typeof(NriStatusBadge), new FrameworkPropertyMetadata(typeof(NriStatusBadge))); }
        public NriStatusBadge() { NriAutomation.BindName(this, nameof(Text)); }
        public static readonly DependencyProperty TextProperty = Register<NriStatusBadge, string>(nameof(Text), "Статус");
        public static readonly DependencyProperty StatusKindProperty = Register<NriStatusBadge, NriStatusKind>(nameof(StatusKind), NriStatusKind.Neutral);
        public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
        public NriStatusKind StatusKind { get => (NriStatusKind)GetValue(StatusKindProperty); set => SetValue(StatusKindProperty, value); }
    }

    public class NriEmptyState : NriControl
    {
        static NriEmptyState() { DefaultStyleKeyProperty.OverrideMetadata(typeof(NriEmptyState), new FrameworkPropertyMetadata(typeof(NriEmptyState))); }
        public NriEmptyState() { NriAutomation.BindName(this, nameof(Title)); }
        public static readonly DependencyProperty TitleProperty = Register<NriEmptyState, string>(nameof(Title), "Нет данных");
        public static readonly DependencyProperty MessageProperty = Register<NriEmptyState, string>(nameof(Message), "Измените условия или создайте первую запись.");
        public static readonly DependencyProperty PrimaryActionProperty = Register<NriEmptyState, object>(nameof(PrimaryAction));
        public static readonly DependencyProperty SecondaryActionProperty = Register<NriEmptyState, object>(nameof(SecondaryAction));
        public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
        public string Message { get => (string)GetValue(MessageProperty); set => SetValue(MessageProperty, value); }
        public object PrimaryAction { get => GetValue(PrimaryActionProperty); set => SetValue(PrimaryActionProperty, value); }
        public object SecondaryAction { get => GetValue(SecondaryActionProperty); set => SetValue(SecondaryActionProperty, value); }
    }

    public class NriValidationSummary : ItemsControl
    {
        static NriValidationSummary() { DefaultStyleKeyProperty.OverrideMetadata(typeof(NriValidationSummary), new FrameworkPropertyMetadata(typeof(NriValidationSummary))); }
        public NriValidationSummary() { NriAutomation.BindName(this, nameof(Title)); }
        protected override AutomationPeer OnCreateAutomationPeer() => new NriFrameworkElementAutomationPeer(this);
        public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string), typeof(NriValidationSummary), new PropertyMetadata("Проверьте данные"));
        public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    }

    public class NriInspectorDrawer : ContentControl
    {
        public static readonly RoutedUICommand CloseDrawerCommand = new RoutedUICommand("Закрыть инспектор", nameof(CloseDrawerCommand), typeof(NriInspectorDrawer));
        static NriInspectorDrawer()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(NriInspectorDrawer), new FrameworkPropertyMetadata(typeof(NriInspectorDrawer)));
            CommandManager.RegisterClassCommandBinding(typeof(NriInspectorDrawer), new CommandBinding(CloseDrawerCommand, (_, e) => ((NriInspectorDrawer)e.Source).IsOpen = false));
        }
        public NriInspectorDrawer() { NriAutomation.BindName(this, nameof(Header)); }
        protected override AutomationPeer OnCreateAutomationPeer() => new NriFrameworkElementAutomationPeer(this);
        public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(nameof(Header), typeof(string), typeof(NriInspectorDrawer), new PropertyMetadata("Подробности"));
        public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(NriInspectorDrawer), new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
        public string Header { get => (string)GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }
        public bool IsOpen { get => (bool)GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }
    }

    public class NriLoadingOverlay : ContentControl
    {
        static NriLoadingOverlay() { DefaultStyleKeyProperty.OverrideMetadata(typeof(NriLoadingOverlay), new FrameworkPropertyMetadata(typeof(NriLoadingOverlay))); }
        public NriLoadingOverlay() { NriAutomation.BindName(this, nameof(ProgressText)); }
        protected override AutomationPeer OnCreateAutomationPeer() => new NriFrameworkElementAutomationPeer(this);
        public static readonly DependencyProperty IsLoadingProperty = DependencyProperty.Register(nameof(IsLoading), typeof(bool), typeof(NriLoadingOverlay), new PropertyMetadata(false));
        public static readonly DependencyProperty IsBlockingProperty = DependencyProperty.Register(nameof(IsBlocking), typeof(bool), typeof(NriLoadingOverlay), new PropertyMetadata(false));
        public static readonly DependencyProperty ProgressTextProperty = DependencyProperty.Register(nameof(ProgressText), typeof(string), typeof(NriLoadingOverlay), new PropertyMetadata("Загрузка…"));
        public bool IsLoading { get => (bool)GetValue(IsLoadingProperty); set => SetValue(IsLoadingProperty, value); }
        public bool IsBlocking { get => (bool)GetValue(IsBlockingProperty); set => SetValue(IsBlockingProperty, value); }
        public string ProgressText { get => (string)GetValue(ProgressTextProperty); set => SetValue(ProgressTextProperty, value); }
    }

    public sealed class NriRouteStateVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var state = value?.ToString() ?? string.Empty;
            var expected = parameter?.ToString() ?? string.Empty;
            return string.Equals(state, expected, StringComparison.OrdinalIgnoreCase)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }

    public sealed class NriRouteStateHost : ContentControl
    {
        public static readonly DependencyProperty StateProperty = DependencyProperty.Register(
            nameof(State), typeof(string), typeof(NriRouteStateHost),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnStateChanged));
        public static readonly DependencyProperty ActiveStatesProperty = DependencyProperty.Register(
            nameof(ActiveStates), typeof(string), typeof(NriRouteStateHost),
            new PropertyMetadata(string.Empty, OnStateChanged));

        public string State { get => (string)GetValue(StateProperty); set => SetValue(StateProperty, value); }
        public string ActiveStates { get => (string)GetValue(ActiveStatesProperty); set => SetValue(ActiveStatesProperty, value); }

        static NriRouteStateHost()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(NriRouteStateHost), new FrameworkPropertyMetadata(typeof(NriRouteStateHost)));
        }

        private static void OnStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var host = (NriRouteStateHost)d;
            var active = (host.ActiveStates ?? string.Empty)
                .Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Any(item => string.Equals(item, host.State, StringComparison.OrdinalIgnoreCase));
            host.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
            host.IsHitTestVisible = active;
        }
    }

    public sealed class NriRouteStatePresenter : ContentControl
    {
        public static readonly DependencyProperty StateProperty = DependencyProperty.Register(
            nameof(State), typeof(string), typeof(NriRouteStatePresenter),
            new PropertyMetadata(string.Empty, OnStateChanged));
        public static readonly DependencyProperty VisibleForProperty = DependencyProperty.Register(
            nameof(VisibleFor), typeof(string), typeof(NriRouteStatePresenter),
            new PropertyMetadata(string.Empty, OnStateChanged));

        public string State { get => (string)GetValue(StateProperty); set => SetValue(StateProperty, value); }
        public string VisibleFor { get => (string)GetValue(VisibleForProperty); set => SetValue(VisibleForProperty, value); }

        static NriRouteStatePresenter()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(NriRouteStatePresenter), new FrameworkPropertyMetadata(typeof(NriRouteStatePresenter)));
        }

        private static void OnStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var presenter = (NriRouteStatePresenter)d;
            presenter.Visibility = string.Equals(presenter.State, presenter.VisibleFor, StringComparison.OrdinalIgnoreCase)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
    }

    public class NriToastHost : NriControl
    {
        static NriToastHost() { DefaultStyleKeyProperty.OverrideMetadata(typeof(NriToastHost), new FrameworkPropertyMetadata(typeof(NriToastHost))); }
        public NriToastHost() { NriAutomation.BindName(this, nameof(Message)); }
        public static readonly DependencyProperty MessageProperty = Register<NriToastHost, string>(nameof(Message), "Изменения сохранены");
        public static readonly DependencyProperty FeedbackKindProperty = Register<NriToastHost, NriFeedbackKind>(nameof(FeedbackKind), NriFeedbackKind.Success);
        public static readonly DependencyProperty IsOpenProperty = Register<NriToastHost, bool>(nameof(IsOpen), true);
        public string Message { get => (string)GetValue(MessageProperty); set => SetValue(MessageProperty, value); }
        public NriFeedbackKind FeedbackKind { get => (NriFeedbackKind)GetValue(FeedbackKindProperty); set => SetValue(FeedbackKindProperty, value); }
        public bool IsOpen { get => (bool)GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }
    }

    public class NriConfirmationDialog : NriControl
    {
        static NriConfirmationDialog() { DefaultStyleKeyProperty.OverrideMetadata(typeof(NriConfirmationDialog), new FrameworkPropertyMetadata(typeof(NriConfirmationDialog))); }
        public NriConfirmationDialog() { NriAutomation.BindName(this, nameof(Title)); }
        public static readonly DependencyProperty IsOpenProperty = Register<NriConfirmationDialog, bool>(nameof(IsOpen), false);
        public static readonly DependencyProperty TitleProperty = Register<NriConfirmationDialog, string>(nameof(Title), "Подтвердите действие");
        public static readonly DependencyProperty MessageProperty = Register<NriConfirmationDialog, string>(nameof(Message), "Это действие изменит данные.");
        public static readonly DependencyProperty TargetNameProperty = Register<NriConfirmationDialog, string>(nameof(TargetName), "");
        public static readonly DependencyProperty RequiresReasonProperty = Register<NriConfirmationDialog, bool>(nameof(RequiresReason), false);
        public static readonly DependencyProperty ReasonProperty = Register<NriConfirmationDialog, string>(nameof(Reason), "");
        public static readonly DependencyProperty ConfirmCommandProperty = Register<NriConfirmationDialog, ICommand>(nameof(ConfirmCommand));
        public static readonly DependencyProperty CancelCommandProperty = Register<NriConfirmationDialog, ICommand>(nameof(CancelCommand));
        public bool IsOpen { get => (bool)GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }
        public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
        public string Message { get => (string)GetValue(MessageProperty); set => SetValue(MessageProperty, value); }
        public string TargetName { get => (string)GetValue(TargetNameProperty); set => SetValue(TargetNameProperty, value); }
        public bool RequiresReason { get => (bool)GetValue(RequiresReasonProperty); set => SetValue(RequiresReasonProperty, value); }
        public string Reason { get => (string)GetValue(ReasonProperty); set => SetValue(ReasonProperty, value); }
        public ICommand ConfirmCommand { get => (ICommand)GetValue(ConfirmCommandProperty); set => SetValue(ConfirmCommandProperty, value); }
        public ICommand CancelCommand { get => (ICommand)GetValue(CancelCommandProperty); set => SetValue(CancelCommandProperty, value); }
    }

    public sealed class NriOptionItem
    {
        public string Value { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsEnabled { get; set; } = true;
        public string DisabledReason { get; set; } = string.Empty;
        public int DisplayOrder { get; set; }
        public bool IsLegacyUnknown { get; set; }
        public override string ToString() => string.IsNullOrWhiteSpace(DisplayName) ? Value : DisplayName;
    }

    public class NriOptionField : ComboBox
    {
        public NriOptionField()
        {
            DisplayMemberPath = nameof(NriOptionItem.DisplayName);
            SelectedValuePath = nameof(NriOptionItem.Value);
            NriAutomation.BindName(this, nameof(AccessibleName));
        }

        public static readonly DependencyProperty AccessibleNameProperty =
            DependencyProperty.Register(nameof(AccessibleName), typeof(string), typeof(NriOptionField), new PropertyMetadata("Option"));
        public static readonly DependencyProperty PlaceholderProperty =
            DependencyProperty.Register(nameof(Placeholder), typeof(string), typeof(NriOptionField), new PropertyMetadata(""));

        public string AccessibleName { get => (string)GetValue(AccessibleNameProperty); set => SetValue(AccessibleNameProperty, value); }
        public string Placeholder { get => (string)GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }
    }

    public class NriSearchableOptionField : NriOptionField
    {
        private int _acceptedInCurrentFilter;
        private CollectionViewSource? _isolatedItemsSourceView;
        private bool _syncingIsolatedItemsSource;

        public NriSearchableOptionField()
        {
            Loaded += (_, _) => ApplyFilter();
        }

        public static readonly DependencyProperty SearchTextProperty =
            DependencyProperty.Register(nameof(SearchText), typeof(string), typeof(NriSearchableOptionField), new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSearchTextChanged));
        public static readonly DependencyProperty IsSearchEnabledProperty =
            DependencyProperty.Register(nameof(IsSearchEnabled), typeof(bool), typeof(NriSearchableOptionField), new PropertyMetadata(true));
        public static readonly DependencyProperty NoResultsTextProperty =
            DependencyProperty.Register(nameof(NoResultsText), typeof(string), typeof(NriSearchableOptionField), new PropertyMetadata("Нет совпадений"));
        public static readonly DependencyProperty HasNoResultsProperty =
            DependencyProperty.Register(nameof(HasNoResults), typeof(bool), typeof(NriSearchableOptionField), new PropertyMetadata(false));
        public static readonly DependencyProperty MaxResultsProperty =
            DependencyProperty.Register(nameof(MaxResults), typeof(int), typeof(NriSearchableOptionField), new PropertyMetadata(50));

        public string SearchText { get => (string)GetValue(SearchTextProperty); set => SetValue(SearchTextProperty, value); }
        public bool IsSearchEnabled { get => (bool)GetValue(IsSearchEnabledProperty); set => SetValue(IsSearchEnabledProperty, value); }
        public string NoResultsText { get => (string)GetValue(NoResultsTextProperty); set => SetValue(NoResultsTextProperty, value); }
        public bool HasNoResults { get => (bool)GetValue(HasNoResultsProperty); set => SetValue(HasNoResultsProperty, value); }
        public int MaxResults { get => (int)GetValue(MaxResultsProperty); set => SetValue(MaxResultsProperty, value); }

        protected override void OnItemsSourceChanged(IEnumerable oldValue, IEnumerable newValue)
        {
            base.OnItemsSourceChanged(oldValue, newValue);
            if (_syncingIsolatedItemsSource)
            {
                ApplyFilter();
                return;
            }

            if (newValue != null && !ReferenceEquals(newValue, _isolatedItemsSourceView?.View))
            {
                _isolatedItemsSourceView = new CollectionViewSource { Source = newValue };
                _syncingIsolatedItemsSource = true;
                SetCurrentValue(ItemsSourceProperty, _isolatedItemsSourceView.View);
                _syncingIsolatedItemsSource = false;
                return;
            }

            ApplyFilter();
        }

        protected override void OnDropDownOpened(EventArgs e)
        {
            ApplyFilter();
            base.OnDropDownOpened(e);
        }

        private static void OnSearchTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => ((NriSearchableOptionField)d).ApplyFilter();

        private void ApplyFilter()
        {
            _acceptedInCurrentFilter = 0;
            if (ItemsSource != null)
            {
                var view = ItemsSource as ICollectionView ?? CollectionViewSource.GetDefaultView(ItemsSource);
                if (view != null)
                {
                    view.Filter = FilterOption;
                    view.Refresh();
                }
            }
            else
            {
                Items.Filter = FilterOption;
                Items.Refresh();
            }

            HasNoResults = Items.Count == 0;
        }

        private bool FilterOption(object item)
        {
            if (!IsSearchEnabled) return true;
            var query = (SearchText ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(query) && !OptionMatches(item, query)) return false;
            var limit = MaxResults <= 0 ? 50 : MaxResults;
            if (_acceptedInCurrentFilter >= limit) return false;
            _acceptedInCurrentFilter++;
            return true;
        }

        private static bool OptionMatches(object item, string query)
        {
            var option = item as NriOptionItem;
            var fields = option == null
                ? new[] { Convert.ToString(item, CultureInfo.CurrentCulture) ?? string.Empty }
                : new[] { option.DisplayName, option.Description, option.Value };
            return fields.Any(x => !string.IsNullOrWhiteSpace(x) && x.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0);
        }
    }

    public class NriBooleanField : CheckBox
    {
        public NriBooleanField() { NriAutomation.BindName(this, nameof(AccessibleName)); }

        public static readonly DependencyProperty AccessibleNameProperty =
            DependencyProperty.Register(nameof(AccessibleName), typeof(string), typeof(NriBooleanField), new PropertyMetadata("Boolean field"));

        public string AccessibleName { get => (string)GetValue(AccessibleNameProperty); set => SetValue(AccessibleNameProperty, value); }
    }

    public class NriIntegerField : TextBox
    {
        private bool _updatingText;

        public NriIntegerField()
        {
            NriAutomation.BindName(this, nameof(AccessibleName));
            DataObject.AddPastingHandler(this, OnPaste);
        }

        public static readonly DependencyProperty AccessibleNameProperty =
            DependencyProperty.Register(nameof(AccessibleName), typeof(string), typeof(NriIntegerField), new PropertyMetadata("Integer field"));
        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(nameof(Value), typeof(int?), typeof(NriIntegerField), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged));
        public static readonly DependencyProperty MinimumProperty =
            DependencyProperty.Register(nameof(Minimum), typeof(int?), typeof(NriIntegerField), new PropertyMetadata(null, OnRangeChanged));
        public static readonly DependencyProperty MaximumProperty =
            DependencyProperty.Register(nameof(Maximum), typeof(int?), typeof(NriIntegerField), new PropertyMetadata(null, OnRangeChanged));
        public static readonly DependencyProperty StepProperty =
            DependencyProperty.Register(nameof(Step), typeof(int), typeof(NriIntegerField), new PropertyMetadata(1));
        public static readonly DependencyProperty AllowEmptyProperty =
            DependencyProperty.Register(nameof(AllowEmpty), typeof(bool), typeof(NriIntegerField), new PropertyMetadata(true));
        public static readonly DependencyProperty HasValidationErrorProperty =
            DependencyProperty.Register(nameof(HasValidationError), typeof(bool), typeof(NriIntegerField), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
        public static readonly DependencyProperty ValidationMessageProperty =
            DependencyProperty.Register(nameof(ValidationMessage), typeof(string), typeof(NriIntegerField), new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
        public static readonly DependencyProperty UnitLabelProperty =
            DependencyProperty.Register(nameof(UnitLabel), typeof(string), typeof(NriIntegerField), new PropertyMetadata(""));

        public string AccessibleName { get => (string)GetValue(AccessibleNameProperty); set => SetValue(AccessibleNameProperty, value); }
        public int? Value { get => (int?)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
        public int? Minimum { get => (int?)GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
        public int? Maximum { get => (int?)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
        public int Step { get => (int)GetValue(StepProperty); set => SetValue(StepProperty, value); }
        public bool AllowEmpty { get => (bool)GetValue(AllowEmptyProperty); set => SetValue(AllowEmptyProperty, value); }
        public bool HasValidationError { get => (bool)GetValue(HasValidationErrorProperty); set => SetValue(HasValidationErrorProperty, value); }
        public string ValidationMessage { get => (string)GetValue(ValidationMessageProperty); set => SetValue(ValidationMessageProperty, value); }
        public string UnitLabel { get => (string)GetValue(UnitLabelProperty); set => SetValue(UnitLabelProperty, value); }

        protected override void OnTextChanged(TextChangedEventArgs e)
        {
            base.OnTextChanged(e);
            if (!_updatingText) ValidateCurrentText(commit: false);
        }

        protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
        {
            CommitText();
            base.OnLostKeyboardFocus(e);
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                CommitText();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Up)
            {
                Increment(Step <= 0 ? 1 : Step);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Down)
            {
                Increment(-(Step <= 0 ? 1 : Step));
                e.Handled = true;
                return;
            }

            base.OnPreviewKeyDown(e);
        }

        protected override void OnPreviewTextInput(TextCompositionEventArgs e)
        {
            e.Handled = !CouldBeIntegerText(TextBeforeInput(e.Text));
            base.OnPreviewTextInput(e);
        }

        private void OnPaste(object sender, DataObjectPastingEventArgs e)
        {
            if (!e.DataObject.GetDataPresent(DataFormats.Text))
            {
                e.CancelCommand();
                return;
            }

            var text = Convert.ToString(e.DataObject.GetData(DataFormats.Text), CultureInfo.CurrentCulture) ?? string.Empty;
            if (!CouldBeIntegerText(TextBeforeInput(text))) e.CancelCommand();
        }

        private string TextBeforeInput(string input)
        {
            var start = SelectionStart;
            var length = SelectionLength;
            var before = Text ?? string.Empty;
            return before.Remove(start, length).Insert(start, input);
        }

        private void Increment(int delta)
        {
            var baseValue = Value ?? ParseInteger(Text) ?? Minimum ?? 0;
            CommitValue(baseValue + delta, allowClamp: true);
        }

        private void CommitText()
        {
            if (ValidateCurrentText(commit: true))
                UpdateTextFromValue();
        }

        private bool ValidateCurrentText(bool commit)
        {
            var text = (Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                if (AllowEmpty)
                {
                    SetValidation(false, "");
                    if (commit) Value = null;
                    return true;
                }

                SetValidation(true, "Введите целое число.");
                return false;
            }

            var parsed = ParseInteger(text);
            if (!parsed.HasValue)
            {
                SetValidation(true, "Введите целое число.");
                return false;
            }

            if (!IsWithinRange(parsed.Value))
            {
                SetValidation(true, RangeMessage());
                return false;
            }

            SetValidation(false, "");
            if (commit) Value = parsed.Value;
            return true;
        }

        private void CommitValue(int value, bool allowClamp)
        {
            if (allowClamp)
            {
                if (Minimum.HasValue && value < Minimum.Value) value = Minimum.Value;
                if (Maximum.HasValue && value > Maximum.Value) value = Maximum.Value;
            }

            if (!IsWithinRange(value))
            {
                SetValidation(true, RangeMessage());
                return;
            }

            SetValidation(false, "");
            Value = value;
            UpdateTextFromValue();
        }

        private bool IsWithinRange(int value)
            => (!Minimum.HasValue || value >= Minimum.Value) && (!Maximum.HasValue || value <= Maximum.Value);

        private string RangeMessage()
        {
            if (Minimum.HasValue && Maximum.HasValue) return $"Значение должно быть от {Minimum.Value} до {Maximum.Value}.";
            if (Minimum.HasValue) return $"Значение должно быть не меньше {Minimum.Value}.";
            if (Maximum.HasValue) return $"Значение должно быть не больше {Maximum.Value}.";
            return "Значение вне допустимого диапазона.";
        }

        private void SetValidation(bool hasError, string message)
        {
            HasValidationError = hasError;
            ValidationMessage = message;
            AutomationProperties.SetHelpText(this, message);
        }

        private static int? ParseInteger(string text)
        {
            int value;
            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.CurrentCulture, out value)) return value;
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) ? value : (int?)null;
        }

        private static bool CouldBeIntegerText(string text)
            => string.IsNullOrWhiteSpace(text) || text == "-" || ParseInteger(text).HasValue;

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => ((NriIntegerField)d).UpdateTextFromValue();

        private static void OnRangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => ((NriIntegerField)d).ValidateCurrentText(commit: false);

        private void UpdateTextFromValue()
        {
            var expected = Value.HasValue ? Value.Value.ToString(CultureInfo.CurrentCulture) : string.Empty;
            if (Text == expected) return;
            _updatingText = true;
            Text = expected;
            _updatingText = false;
        }
    }

    public class NriDecimalField : TextBox
    {
        private bool _updatingText;

        public NriDecimalField()
        {
            NriAutomation.BindName(this, nameof(AccessibleName));
            DataObject.AddPastingHandler(this, OnPaste);
        }

        public static readonly DependencyProperty AccessibleNameProperty =
            DependencyProperty.Register(nameof(AccessibleName), typeof(string), typeof(NriDecimalField), new PropertyMetadata("Decimal field"));
        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(nameof(Value), typeof(decimal?), typeof(NriDecimalField), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged));
        public static readonly DependencyProperty MinimumProperty =
            DependencyProperty.Register(nameof(Minimum), typeof(decimal?), typeof(NriDecimalField), new PropertyMetadata(null, OnRangeChanged));
        public static readonly DependencyProperty MaximumProperty =
            DependencyProperty.Register(nameof(Maximum), typeof(decimal?), typeof(NriDecimalField), new PropertyMetadata(null, OnRangeChanged));
        public static readonly DependencyProperty StepProperty =
            DependencyProperty.Register(nameof(Step), typeof(decimal), typeof(NriDecimalField), new PropertyMetadata(0.1m));
        public static readonly DependencyProperty AllowEmptyProperty =
            DependencyProperty.Register(nameof(AllowEmpty), typeof(bool), typeof(NriDecimalField), new PropertyMetadata(true));
        public static readonly DependencyProperty HasValidationErrorProperty =
            DependencyProperty.Register(nameof(HasValidationError), typeof(bool), typeof(NriDecimalField), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
        public static readonly DependencyProperty ValidationMessageProperty =
            DependencyProperty.Register(nameof(ValidationMessage), typeof(string), typeof(NriDecimalField), new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
        public static readonly DependencyProperty UnitLabelProperty =
            DependencyProperty.Register(nameof(UnitLabel), typeof(string), typeof(NriDecimalField), new PropertyMetadata(""));

        public string AccessibleName { get => (string)GetValue(AccessibleNameProperty); set => SetValue(AccessibleNameProperty, value); }
        public decimal? Value { get => (decimal?)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
        public decimal? Minimum { get => (decimal?)GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
        public decimal? Maximum { get => (decimal?)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
        public decimal Step { get => (decimal)GetValue(StepProperty); set => SetValue(StepProperty, value); }
        public bool AllowEmpty { get => (bool)GetValue(AllowEmptyProperty); set => SetValue(AllowEmptyProperty, value); }
        public bool HasValidationError { get => (bool)GetValue(HasValidationErrorProperty); set => SetValue(HasValidationErrorProperty, value); }
        public string ValidationMessage { get => (string)GetValue(ValidationMessageProperty); set => SetValue(ValidationMessageProperty, value); }
        public string UnitLabel { get => (string)GetValue(UnitLabelProperty); set => SetValue(UnitLabelProperty, value); }

        protected override void OnTextChanged(TextChangedEventArgs e)
        {
            base.OnTextChanged(e);
            if (!_updatingText) ValidateCurrentText(commit: false);
        }

        protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
        {
            CommitText();
            base.OnLostKeyboardFocus(e);
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                CommitText();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Up)
            {
                Increment(Step == 0m ? 1m : Step);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Down)
            {
                Increment(-(Step == 0m ? 1m : Step));
                e.Handled = true;
                return;
            }

            base.OnPreviewKeyDown(e);
        }

        protected override void OnPreviewTextInput(TextCompositionEventArgs e)
        {
            e.Handled = !CouldBeDecimalText(TextBeforeInput(e.Text));
            base.OnPreviewTextInput(e);
        }

        private void OnPaste(object sender, DataObjectPastingEventArgs e)
        {
            if (!e.DataObject.GetDataPresent(DataFormats.Text))
            {
                e.CancelCommand();
                return;
            }

            var text = Convert.ToString(e.DataObject.GetData(DataFormats.Text), CultureInfo.CurrentCulture) ?? string.Empty;
            if (!CouldBeDecimalText(TextBeforeInput(text))) e.CancelCommand();
        }

        private string TextBeforeInput(string input)
        {
            var start = SelectionStart;
            var length = SelectionLength;
            var before = Text ?? string.Empty;
            return before.Remove(start, length).Insert(start, input);
        }

        private void Increment(decimal delta)
        {
            var baseValue = Value ?? ParseDecimal(Text) ?? Minimum ?? 0m;
            CommitValue(baseValue + delta, allowClamp: true);
        }

        private void CommitText()
        {
            if (ValidateCurrentText(commit: true))
                UpdateTextFromValue();
        }

        private bool ValidateCurrentText(bool commit)
        {
            var text = (Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                if (AllowEmpty)
                {
                    SetValidation(false, "");
                    if (commit) Value = null;
                    return true;
                }

                SetValidation(true, "Введите число.");
                return false;
            }

            var parsed = ParseDecimal(text);
            if (!parsed.HasValue)
            {
                SetValidation(true, "Введите число.");
                return false;
            }

            if (!IsWithinRange(parsed.Value))
            {
                SetValidation(true, RangeMessage());
                return false;
            }

            SetValidation(false, "");
            if (commit) Value = parsed.Value;
            return true;
        }

        private void CommitValue(decimal value, bool allowClamp)
        {
            if (allowClamp)
            {
                if (Minimum.HasValue && value < Minimum.Value) value = Minimum.Value;
                if (Maximum.HasValue && value > Maximum.Value) value = Maximum.Value;
            }

            if (!IsWithinRange(value))
            {
                SetValidation(true, RangeMessage());
                return;
            }

            SetValidation(false, "");
            Value = value;
            UpdateTextFromValue();
        }

        private bool IsWithinRange(decimal value)
            => (!Minimum.HasValue || value >= Minimum.Value) && (!Maximum.HasValue || value <= Maximum.Value);

        private string RangeMessage()
        {
            if (Minimum.HasValue && Maximum.HasValue) return $"Значение должно быть от {Minimum.Value} до {Maximum.Value}.";
            if (Minimum.HasValue) return $"Значение должно быть не меньше {Minimum.Value}.";
            if (Maximum.HasValue) return $"Значение должно быть не больше {Maximum.Value}.";
            return "Значение вне допустимого диапазона.";
        }

        private void SetValidation(bool hasError, string message)
        {
            HasValidationError = hasError;
            ValidationMessage = message;
            AutomationProperties.SetHelpText(this, message);
        }

        private static decimal? ParseDecimal(string text)
        {
            var normalized = (text ?? string.Empty).Trim();
            decimal value;
            if (decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.CurrentCulture, out value)) return value;
            normalized = normalized.Replace(',', '.');
            return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out value) ? value : (decimal?)null;
        }

        private static bool CouldBeDecimalText(string text)
            => string.IsNullOrWhiteSpace(text) || text == "-" || text == "." || text == "," || text == "-." || text == "-," || ParseDecimal(text).HasValue;

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => ((NriDecimalField)d).UpdateTextFromValue();

        private static void OnRangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => ((NriDecimalField)d).ValidateCurrentText(commit: false);

        private void UpdateTextFromValue()
        {
            var expected = Value.HasValue ? Value.Value.ToString(CultureInfo.CurrentCulture) : string.Empty;
            if (Text == expected) return;
            _updatingText = true;
            Text = expected;
            _updatingText = false;
        }
    }

    public class NriBoundedNumberField : NriDecimalField
    {
        public NriBoundedNumberField()
        {
            AccessibleName = "Bounded number field";
        }
    }

    public class NriDateField : DatePicker
    {
        public NriDateField() { NriAutomation.BindName(this, nameof(AccessibleName)); }

        public static readonly DependencyProperty AccessibleNameProperty =
            DependencyProperty.Register(nameof(AccessibleName), typeof(string), typeof(NriDateField), new PropertyMetadata("Date field"));

        public string AccessibleName { get => (string)GetValue(AccessibleNameProperty); set => SetValue(AccessibleNameProperty, value); }
    }

    public class NriDateTimeField : NriControl
    {
        private bool _syncing;

        static NriDateTimeField() { DefaultStyleKeyProperty.OverrideMetadata(typeof(NriDateTimeField), new FrameworkPropertyMetadata(typeof(NriDateTimeField))); }

        public NriDateTimeField() { NriAutomation.BindName(this, nameof(AccessibleName)); }

        public static readonly DependencyProperty AccessibleNameProperty =
            DependencyProperty.Register(nameof(AccessibleName), typeof(string), typeof(NriDateTimeField), new PropertyMetadata("Date/time field"));
        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(nameof(Value), typeof(DateTime?), typeof(NriDateTimeField), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged));
        public static readonly DependencyProperty SelectedDateProperty =
            DependencyProperty.Register(nameof(SelectedDate), typeof(DateTime?), typeof(NriDateTimeField), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnPartChanged));
        public static readonly DependencyProperty HourProperty =
            DependencyProperty.Register(nameof(Hour), typeof(int), typeof(NriDateTimeField), new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnPartChanged));
        public static readonly DependencyProperty MinuteProperty =
            DependencyProperty.Register(nameof(Minute), typeof(int), typeof(NriDateTimeField), new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnPartChanged));
        public static readonly DependencyProperty AllowEmptyProperty =
            DependencyProperty.Register(nameof(AllowEmpty), typeof(bool), typeof(NriDateTimeField), new PropertyMetadata(true));
        public static readonly DependencyProperty HasValidationErrorProperty =
            DependencyProperty.Register(nameof(HasValidationError), typeof(bool), typeof(NriDateTimeField), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
        public static readonly DependencyProperty ValidationMessageProperty =
            DependencyProperty.Register(nameof(ValidationMessage), typeof(string), typeof(NriDateTimeField), new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public IEnumerable<int> Hours { get; } = Enumerable.Range(0, 24).ToArray();
        public IEnumerable<int> Minutes { get; } = Enumerable.Range(0, 60).ToArray();

        public string AccessibleName { get => (string)GetValue(AccessibleNameProperty); set => SetValue(AccessibleNameProperty, value); }
        public DateTime? Value { get => (DateTime?)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
        public DateTime? SelectedDate { get => (DateTime?)GetValue(SelectedDateProperty); set => SetValue(SelectedDateProperty, value); }
        public int Hour { get => (int)GetValue(HourProperty); set => SetValue(HourProperty, value); }
        public int Minute { get => (int)GetValue(MinuteProperty); set => SetValue(MinuteProperty, value); }
        public bool AllowEmpty { get => (bool)GetValue(AllowEmptyProperty); set => SetValue(AllowEmptyProperty, value); }
        public bool HasValidationError { get => (bool)GetValue(HasValidationErrorProperty); set => SetValue(HasValidationErrorProperty, value); }
        public string ValidationMessage { get => (string)GetValue(ValidationMessageProperty); set => SetValue(ValidationMessageProperty, value); }

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (NriDateTimeField)d;
            if (control._syncing) return;
            control._syncing = true;
            var value = control.Value;
            if (value.HasValue)
            {
                control.SelectedDate = value.Value.Date;
                control.Hour = value.Value.Hour;
                control.Minute = value.Value.Minute;
            }
            else
            {
                control.SelectedDate = null;
                control.Hour = 0;
                control.Minute = 0;
            }
            control._syncing = false;
        }

        private static void OnPartChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (NriDateTimeField)d;
            if (control._syncing) return;
            control.CommitParts();
        }

        private void CommitParts()
        {
            if (!SelectedDate.HasValue)
            {
                HasValidationError = !AllowEmpty;
                ValidationMessage = HasValidationError ? "Выберите дату." : string.Empty;
                _syncing = true;
                Value = null;
                _syncing = false;
                return;
            }

            var hour = Math.Max(0, Math.Min(23, Hour));
            var minute = Math.Max(0, Math.Min(59, Minute));
            HasValidationError = false;
            ValidationMessage = string.Empty;
            _syncing = true;
            Value = SelectedDate.Value.Date.AddHours(hour).AddMinutes(minute);
            _syncing = false;
        }
    }

    public class NriDurationField : NriControl
    {
        private bool _syncing;

        static NriDurationField() { DefaultStyleKeyProperty.OverrideMetadata(typeof(NriDurationField), new FrameworkPropertyMetadata(typeof(NriDurationField))); }

        public NriDurationField() { NriAutomation.BindName(this, nameof(AccessibleName)); }

        public static readonly DependencyProperty AccessibleNameProperty =
            DependencyProperty.Register(nameof(AccessibleName), typeof(string), typeof(NriDurationField), new PropertyMetadata("Duration field"));
        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(nameof(Value), typeof(TimeSpan?), typeof(NriDurationField), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged));
        public static readonly DependencyProperty AmountProperty =
            DependencyProperty.Register(nameof(Amount), typeof(int?), typeof(NriDurationField), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnPartChanged));
        public static readonly DependencyProperty UnitProperty =
            DependencyProperty.Register(nameof(Unit), typeof(string), typeof(NriDurationField), new FrameworkPropertyMetadata("minutes", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnPartChanged));
        public static readonly DependencyProperty MinimumMinutesProperty =
            DependencyProperty.Register(nameof(MinimumMinutes), typeof(int?), typeof(NriDurationField), new PropertyMetadata(null, OnPartChanged));
        public static readonly DependencyProperty MaximumMinutesProperty =
            DependencyProperty.Register(nameof(MaximumMinutes), typeof(int?), typeof(NriDurationField), new PropertyMetadata(null, OnPartChanged));
        public static readonly DependencyProperty AllowEmptyProperty =
            DependencyProperty.Register(nameof(AllowEmpty), typeof(bool), typeof(NriDurationField), new PropertyMetadata(true));
        public static readonly DependencyProperty HasValidationErrorProperty =
            DependencyProperty.Register(nameof(HasValidationError), typeof(bool), typeof(NriDurationField), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
        public static readonly DependencyProperty ValidationMessageProperty =
            DependencyProperty.Register(nameof(ValidationMessage), typeof(string), typeof(NriDurationField), new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
        public static readonly DependencyProperty UnitLabelProperty =
            DependencyProperty.Register(nameof(UnitLabel), typeof(string), typeof(NriDurationField), new PropertyMetadata(""));

        public IEnumerable<NriOptionItem> Units { get; } = new[]
        {
            new NriOptionItem { Value = "minutes", DisplayName = "минуты" },
            new NriOptionItem { Value = "hours", DisplayName = "часы" },
            new NriOptionItem { Value = "days", DisplayName = "дни" }
        };

        public string AccessibleName { get => (string)GetValue(AccessibleNameProperty); set => SetValue(AccessibleNameProperty, value); }
        public TimeSpan? Value { get => (TimeSpan?)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
        public int? Amount { get => (int?)GetValue(AmountProperty); set => SetValue(AmountProperty, value); }
        public string Unit { get => (string)GetValue(UnitProperty); set => SetValue(UnitProperty, value); }
        public int? MinimumMinutes { get => (int?)GetValue(MinimumMinutesProperty); set => SetValue(MinimumMinutesProperty, value); }
        public int? MaximumMinutes { get => (int?)GetValue(MaximumMinutesProperty); set => SetValue(MaximumMinutesProperty, value); }
        public bool AllowEmpty { get => (bool)GetValue(AllowEmptyProperty); set => SetValue(AllowEmptyProperty, value); }
        public bool HasValidationError { get => (bool)GetValue(HasValidationErrorProperty); set => SetValue(HasValidationErrorProperty, value); }
        public string ValidationMessage { get => (string)GetValue(ValidationMessageProperty); set => SetValue(ValidationMessageProperty, value); }
        public string UnitLabel { get => (string)GetValue(UnitLabelProperty); set => SetValue(UnitLabelProperty, value); }

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (NriDurationField)d;
            if (control._syncing) return;
            control._syncing = true;
            if (control.Value.HasValue)
            {
                control.Unit = "minutes";
                control.Amount = (int)Math.Round(control.Value.Value.TotalMinutes);
            }
            else
            {
                control.Amount = null;
            }
            control._syncing = false;
        }

        private static void OnPartChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (NriDurationField)d;
            if (control._syncing) return;
            control.CommitParts();
        }

        private void CommitParts()
        {
            if (!Amount.HasValue)
            {
                HasValidationError = !AllowEmpty;
                ValidationMessage = HasValidationError ? "Введите длительность." : string.Empty;
                _syncing = true;
                Value = null;
                _syncing = false;
                return;
            }

            var minutes = Unit == "days" ? Amount.Value * 24 * 60 : Unit == "hours" ? Amount.Value * 60 : Amount.Value;
            if ((MinimumMinutes.HasValue && minutes < MinimumMinutes.Value) || (MaximumMinutes.HasValue && minutes > MaximumMinutes.Value))
            {
                HasValidationError = true;
                ValidationMessage = "Длительность вне допустимого диапазона.";
                return;
            }

            HasValidationError = false;
            ValidationMessage = string.Empty;
            _syncing = true;
            Value = TimeSpan.FromMinutes(minutes);
            _syncing = false;
        }
    }

    public class NriTagEditor : NriControl, INotifyPropertyChanged
    {
        private bool _syncingText;
        private TextBox? _inputBox;

        static NriTagEditor() { DefaultStyleKeyProperty.OverrideMetadata(typeof(NriTagEditor), new FrameworkPropertyMetadata(typeof(NriTagEditor))); }

        public NriTagEditor()
        {
            SelectedTags = new ObservableCollection<string>();
            AddTagCommand = new NriLocalCommand(_ => AddInputTag(), _ => CanAddInputTag());
            RemoveTagCommand = new NriLocalCommand(x => RemoveTag(Convert.ToString(x, CultureInfo.CurrentCulture) ?? string.Empty));
            NriAutomation.BindName(this, nameof(AccessibleName));
        }

        public static readonly DependencyProperty AccessibleNameProperty =
            DependencyProperty.Register(nameof(AccessibleName), typeof(string), typeof(NriTagEditor), new PropertyMetadata("Tags"));
        public static readonly DependencyProperty PlaceholderProperty =
            DependencyProperty.Register(nameof(Placeholder), typeof(string), typeof(NriTagEditor), new PropertyMetadata(""));
        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(nameof(Text), typeof(string), typeof(NriTagEditor), new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnTagTextChanged));
        public static readonly DependencyProperty SelectedTagsProperty =
            DependencyProperty.Register(nameof(SelectedTags), typeof(IList), typeof(NriTagEditor), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedTagsChanged));
        public static readonly DependencyProperty InputTextProperty =
            DependencyProperty.Register(nameof(InputText), typeof(string), typeof(NriTagEditor), new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
        public static readonly DependencyProperty AddTagCommandProperty =
            DependencyProperty.Register(nameof(AddTagCommand), typeof(ICommand), typeof(NriTagEditor), new PropertyMetadata(null));
        public static readonly DependencyProperty RemoveTagCommandProperty =
            DependencyProperty.Register(nameof(RemoveTagCommand), typeof(ICommand), typeof(NriTagEditor), new PropertyMetadata(null));
        public static readonly DependencyProperty AllowCustomTagsProperty =
            DependencyProperty.Register(nameof(AllowCustomTags), typeof(bool), typeof(NriTagEditor), new PropertyMetadata(true));
        public static readonly DependencyProperty SuggestedTagsProperty =
            DependencyProperty.Register(nameof(SuggestedTags), typeof(IEnumerable), typeof(NriTagEditor), new PropertyMetadata(null));
        public static readonly DependencyProperty IsReadOnlyProperty =
            DependencyProperty.Register(nameof(IsReadOnly), typeof(bool), typeof(NriTagEditor), new PropertyMetadata(false));
        public static readonly DependencyProperty AutomationIdPrefixProperty =
            DependencyProperty.Register(nameof(AutomationIdPrefix), typeof(string), typeof(NriTagEditor), new PropertyMetadata("NriTagEditor", OnAutomationIdPrefixChanged));

        public string AccessibleName { get => (string)GetValue(AccessibleNameProperty); set => SetValue(AccessibleNameProperty, value); }
        public string Placeholder { get => (string)GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }
        public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
        public IList SelectedTags { get => (IList)GetValue(SelectedTagsProperty); set => SetValue(SelectedTagsProperty, value); }
        public string InputText { get => (string)GetValue(InputTextProperty); set => SetValue(InputTextProperty, value); }
        public ICommand AddTagCommand { get => (ICommand)GetValue(AddTagCommandProperty); set => SetValue(AddTagCommandProperty, value); }
        public ICommand RemoveTagCommand { get => (ICommand)GetValue(RemoveTagCommandProperty); set => SetValue(RemoveTagCommandProperty, value); }
        public bool AllowCustomTags { get => (bool)GetValue(AllowCustomTagsProperty); set => SetValue(AllowCustomTagsProperty, value); }
        public IEnumerable SuggestedTags { get => (IEnumerable)GetValue(SuggestedTagsProperty); set => SetValue(SuggestedTagsProperty, value); }
        public bool IsReadOnly { get => (bool)GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }
        public string AutomationIdPrefix { get => (string)GetValue(AutomationIdPrefixProperty); set => SetValue(AutomationIdPrefixProperty, value); }
        public string TagInputAutomationId => ScopedAutomationId("Input");
        public string TagAddAutomationId => ScopedAutomationId("Add");
        public string TagRemoveAutomationId => ScopedAutomationId("Remove");
        public event PropertyChangedEventHandler? PropertyChanged;

        public override void OnApplyTemplate()
        {
            if (_inputBox != null) _inputBox.KeyDown -= OnInputKeyDown;
            base.OnApplyTemplate();
            _inputBox = GetTemplateChild("PART_TagInput") as TextBox;
            if (_inputBox != null) _inputBox.KeyDown += OnInputKeyDown;
        }

        private void OnInputKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                AddInputTag();
                e.Handled = true;
            }
        }

        private bool CanAddInputTag()
            => !IsReadOnly && AllowCustomTags && !string.IsNullOrWhiteSpace(InputText);

        private void AddInputTag()
        {
            var tag = NormalizeTag(InputText);
            if (string.IsNullOrWhiteSpace(tag)) return;
            if (IsReadOnly) return;
            if (!SelectedTags.Cast<object>().Select(x => Convert.ToString(x, CultureInfo.CurrentCulture) ?? string.Empty).Any(x => string.Equals(x, tag, StringComparison.OrdinalIgnoreCase)))
                SelectedTags.Add(tag);
            InputText = string.Empty;
            SyncTextFromTags();
        }

        private void RemoveTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag)) return;
            if (IsReadOnly) return;
            var match = SelectedTags.Cast<object>().FirstOrDefault(x => string.Equals(Convert.ToString(x, CultureInfo.CurrentCulture), tag, StringComparison.OrdinalIgnoreCase));
            if (match != null) SelectedTags.Remove(match);
            SyncTextFromTags();
        }

        private static string NormalizeTag(string value)
            => (value ?? string.Empty).Trim().Trim(',').Trim();

        private static void OnTagTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var editor = (NriTagEditor)d;
            if (editor._syncingText) return;
            editor.SyncTagsFromText(Convert.ToString(e.NewValue, CultureInfo.CurrentCulture) ?? string.Empty);
        }

        private static void OnSelectedTagsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var editor = (NriTagEditor)d;
            if (e.OldValue is INotifyCollectionChanged oldCollection) oldCollection.CollectionChanged -= editor.OnTagsChanged;
            if (e.NewValue is INotifyCollectionChanged newCollection) newCollection.CollectionChanged += editor.OnTagsChanged;
            editor.SyncTextFromTags();
        }

        private static void OnAutomationIdPrefixChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var editor = (NriTagEditor)d;
            editor.PropertyChanged?.Invoke(editor, new PropertyChangedEventArgs(nameof(TagInputAutomationId)));
            editor.PropertyChanged?.Invoke(editor, new PropertyChangedEventArgs(nameof(TagAddAutomationId)));
            editor.PropertyChanged?.Invoke(editor, new PropertyChangedEventArgs(nameof(TagRemoveAutomationId)));
        }

        private string ScopedAutomationId(string suffix)
            => string.IsNullOrWhiteSpace(AutomationIdPrefix) ? "NriTagEditor_" + suffix : AutomationIdPrefix + "_" + suffix;

        private void OnTagsChanged(object sender, NotifyCollectionChangedEventArgs e)
            => SyncTextFromTags();

        private void SyncTagsFromText(string text)
        {
            var tags = text.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(NormalizeTag)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var collection = SelectedTags ?? new ObservableCollection<string>();
            collection.Clear();
            foreach (var tag in tags) collection.Add(tag);
            if (!ReferenceEquals(collection, SelectedTags)) SelectedTags = collection;
        }

        private void SyncTextFromTags()
        {
            var text = SelectedTags == null ? string.Empty : string.Join(", ", SelectedTags.Cast<object>().Select(x => Convert.ToString(x, CultureInfo.CurrentCulture)).Where(x => !string.IsNullOrWhiteSpace(x)));
            if (Text == text) return;
            _syncingText = true;
            Text = text;
            _syncingText = false;
        }
    }

    public class NriGeneratedIdentifierField : TextBox
    {
        public NriGeneratedIdentifierField()
        {
            IsReadOnly = true;
            NriAutomation.BindName(this, nameof(AccessibleName));
        }

        public static readonly DependencyProperty AccessibleNameProperty =
            DependencyProperty.Register(nameof(AccessibleName), typeof(string), typeof(NriGeneratedIdentifierField), new PropertyMetadata("Generated identifier"));

        public string AccessibleName { get => (string)GetValue(AccessibleNameProperty); set => SetValue(AccessibleNameProperty, value); }
    }

    public class NriUnknownValueState : NriControl
    {
        static NriUnknownValueState() { DefaultStyleKeyProperty.OverrideMetadata(typeof(NriUnknownValueState), new FrameworkPropertyMetadata(typeof(NriUnknownValueState))); }
        public NriUnknownValueState() { NriAutomation.BindName(this, nameof(Message)); }

        public static readonly DependencyProperty IsOpenProperty = Register<NriUnknownValueState, bool>(nameof(IsOpen), false);
        public static readonly DependencyProperty MessageProperty = Register<NriUnknownValueState, string>(nameof(Message), "Unknown legacy value");
        public static readonly DependencyProperty ValueProperty = Register<NriUnknownValueState, string>(nameof(Value), "");

        public bool IsOpen { get => (bool)GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }
        public string Message { get => (string)GetValue(MessageProperty); set => SetValue(MessageProperty, value); }
        public string Value { get => (string)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    }

    public class NriFieldValidationPresenter : NriControl
    {
        static NriFieldValidationPresenter() { DefaultStyleKeyProperty.OverrideMetadata(typeof(NriFieldValidationPresenter), new FrameworkPropertyMetadata(typeof(NriFieldValidationPresenter))); }
        public NriFieldValidationPresenter() { NriAutomation.BindName(this, nameof(Message)); }

        public static readonly DependencyProperty MessageProperty = Register<NriFieldValidationPresenter, string>(nameof(Message), "");
        public static readonly DependencyProperty WarningProperty = Register<NriFieldValidationPresenter, string>(nameof(Warning), "");
        public static readonly DependencyProperty IsErrorProperty = Register<NriFieldValidationPresenter, bool>(nameof(IsError), true);

        public string Message { get => (string)GetValue(MessageProperty); set => SetValue(MessageProperty, value); }
        public string Warning { get => (string)GetValue(WarningProperty); set => SetValue(WarningProperty, value); }
        public bool IsError { get => (bool)GetValue(IsErrorProperty); set => SetValue(IsErrorProperty, value); }
    }

    public sealed class NriReferenceOption
    {
        public string Id { get; set; } = string.Empty;
        public string CanonicalKey { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string TypeLabel { get; set; } = string.Empty;
        public string StatusLabel { get; set; } = string.Empty;
        public bool IsArchived { get; set; }
        public bool IsMissing { get; set; }
        public bool IsEnabled { get; set; } = true;
        public string DisabledReason { get; set; } = string.Empty;
        public string Summary => string.Join(" · ", new[] { TypeLabel, StatusLabel }.Where(x => !string.IsNullOrWhiteSpace(x)));
        public override string ToString() => string.IsNullOrWhiteSpace(DisplayName) ? Id : DisplayName;
    }

    public class NriReferencePicker : NriControl, INotifyPropertyChanged
    {
        static NriReferencePicker() { DefaultStyleKeyProperty.OverrideMetadata(typeof(NriReferencePicker), new FrameworkPropertyMetadata(typeof(NriReferencePicker))); }
        public NriReferencePicker() { NriAutomation.BindName(this, nameof(DisplayName)); }
        public static readonly DependencyProperty DisplayNameProperty = Register<NriReferencePicker, string>(nameof(DisplayName), "Не выбрано");
        public static readonly DependencyProperty SelectedIdProperty = Register<NriReferencePicker, string>(nameof(SelectedId), "");
        public static readonly DependencyProperty StatusTextProperty = Register<NriReferencePicker, string>(nameof(StatusText), "Доступно");
        public static readonly DependencyProperty ValidationMessageProperty = Register<NriReferencePicker, string>(nameof(ValidationMessage), "");
        public static readonly DependencyProperty IsMultipleProperty = Register<NriReferencePicker, bool>(nameof(IsMultiple), false);
        public static readonly DependencyProperty SearchTextProperty = Register<NriReferencePicker, string>(nameof(SearchText), "");
        public static readonly DependencyProperty OptionsProperty = Register<NriReferencePicker, IEnumerable>(nameof(Options));
        public static readonly DependencyProperty SelectedOptionProperty = Register<NriReferencePicker, object>(nameof(SelectedOption));
        public static readonly DependencyProperty IsLoadingProperty = Register<NriReferencePicker, bool>(nameof(IsLoading), false);
        public static readonly DependencyProperty SearchCommandProperty = Register<NriReferencePicker, ICommand>(nameof(SearchCommand));
        public static readonly DependencyProperty ClearCommandProperty = Register<NriReferencePicker, ICommand>(nameof(ClearCommand));
        public static readonly DependencyProperty OpenCommandProperty = Register<NriReferencePicker, ICommand>(nameof(OpenCommand));
        public static readonly DependencyProperty SelectCommandProperty = Register<NriReferencePicker, ICommand>(nameof(SelectCommand));
        public static readonly DependencyProperty AutomationIdPrefixProperty =
            DependencyProperty.Register(nameof(AutomationIdPrefix), typeof(string), typeof(NriReferencePicker), new PropertyMetadata("", OnAutomationIdPrefixChanged));
        public string DisplayName { get => (string)GetValue(DisplayNameProperty); set => SetValue(DisplayNameProperty, value); }
        public string SelectedId { get => (string)GetValue(SelectedIdProperty); set => SetValue(SelectedIdProperty, value); }
        public string StatusText { get => (string)GetValue(StatusTextProperty); set => SetValue(StatusTextProperty, value); }
        public string ValidationMessage { get => (string)GetValue(ValidationMessageProperty); set => SetValue(ValidationMessageProperty, value); }
        public bool IsMultiple { get => (bool)GetValue(IsMultipleProperty); set => SetValue(IsMultipleProperty, value); }
        public string SearchText { get => (string)GetValue(SearchTextProperty); set => SetValue(SearchTextProperty, value); }
        public IEnumerable Options { get => (IEnumerable)GetValue(OptionsProperty); set => SetValue(OptionsProperty, value); }
        public object SelectedOption { get => GetValue(SelectedOptionProperty); set => SetValue(SelectedOptionProperty, value); }
        public bool IsLoading { get => (bool)GetValue(IsLoadingProperty); set => SetValue(IsLoadingProperty, value); }
        public ICommand SearchCommand { get => (ICommand)GetValue(SearchCommandProperty); set => SetValue(SearchCommandProperty, value); }
        public ICommand ClearCommand { get => (ICommand)GetValue(ClearCommandProperty); set => SetValue(ClearCommandProperty, value); }
        public ICommand OpenCommand { get => (ICommand)GetValue(OpenCommandProperty); set => SetValue(OpenCommandProperty, value); }
        public ICommand SelectCommand { get => (ICommand)GetValue(SelectCommandProperty); set => SetValue(SelectCommandProperty, value); }
        public string AutomationIdPrefix { get => (string)GetValue(AutomationIdPrefixProperty); set => SetValue(AutomationIdPrefixProperty, value); }
        public string ReferenceOpenAutomationId => ScopedAutomationId(MultiDefault("NriReferencePicker_Search", "NriMultiReferencePicker_SearchButton"), "Open");
        public string ReferenceOpenLinkedAutomationId => ScopedAutomationId("NriReferencePicker_Open", "OpenLinked");
        public string ReferenceSearchAutomationId => ScopedAutomationId(MultiDefault("NriReferencePicker_SearchText", "NriMultiReferencePicker_SearchText"), "Search");
        public string ReferenceSearchButtonAutomationId => ScopedAutomationId(MultiDefault("NriReferencePicker_SearchButton", "NriMultiReferencePicker_SearchButton"), "SearchButton");
        public string ReferenceResultsAutomationId => ScopedAutomationId(MultiDefault("NriReferencePicker_Options", "NriMultiReferencePicker_Options"), "Results");
        public string ReferenceClearAutomationId => ScopedAutomationId(MultiDefault("NriReferencePicker_Clear", "NriMultiReferencePicker_ClearAll"), "Clear");
        public string ReferenceSelectAutomationId => ScopedAutomationId(MultiDefault("NriReferencePicker_Select", "NriMultiReferencePicker_Select"), "Select");
        public string ReferenceChipsAutomationId => ScopedAutomationId("NriMultiReferencePicker_SelectedReferences", "Chips");
        public string ReferenceRemoveAutomationId => ScopedAutomationId("NriMultiReferencePicker_Remove", "Remove");
        public event PropertyChangedEventHandler? PropertyChanged;

        private static void OnAutomationIdPrefixChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var picker = (NriReferencePicker)d;
            foreach (var name in new[]
            {
                nameof(ReferenceOpenAutomationId),
                nameof(ReferenceOpenLinkedAutomationId),
                nameof(ReferenceSearchAutomationId),
                nameof(ReferenceSearchButtonAutomationId),
                nameof(ReferenceResultsAutomationId),
                nameof(ReferenceClearAutomationId),
                nameof(ReferenceSelectAutomationId),
                nameof(ReferenceChipsAutomationId),
                nameof(ReferenceRemoveAutomationId)
            })
            {
                picker.PropertyChanged?.Invoke(picker, new PropertyChangedEventArgs(name));
            }
        }

        private string MultiDefault(string singleDefault, string multiDefault)
            => IsMultiple ? multiDefault : singleDefault;

        private string ScopedAutomationId(string defaultId, string suffix)
            => string.IsNullOrWhiteSpace(AutomationIdPrefix) ? defaultId : AutomationIdPrefix + "_" + suffix;
    }

    public class NriMultiReferencePicker : NriReferencePicker
    {
        static NriMultiReferencePicker() { DefaultStyleKeyProperty.OverrideMetadata(typeof(NriMultiReferencePicker), new FrameworkPropertyMetadata(typeof(NriMultiReferencePicker))); }

        public NriMultiReferencePicker()
        {
            IsMultiple = true;
            SelectedReferences = new ObservableCollection<NriReferenceOption>();
        }

        public static readonly DependencyProperty SelectedReferencesProperty =
            DependencyProperty.Register(nameof(SelectedReferences), typeof(IList), typeof(NriMultiReferencePicker), new PropertyMetadata(null));
        public static readonly DependencyProperty AddCommandProperty = Register<NriMultiReferencePicker, ICommand>(nameof(AddCommand));
        public static readonly DependencyProperty RemoveCommandProperty = Register<NriMultiReferencePicker, ICommand>(nameof(RemoveCommand));
        public static readonly DependencyProperty ClearAllCommandProperty = Register<NriMultiReferencePicker, ICommand>(nameof(ClearAllCommand));

        public IList SelectedReferences { get => (IList)GetValue(SelectedReferencesProperty); set => SetValue(SelectedReferencesProperty, value); }
        public ICommand AddCommand { get => (ICommand)GetValue(AddCommandProperty); set => SetValue(AddCommandProperty, value); }
        public ICommand RemoveCommand { get => (ICommand)GetValue(RemoveCommandProperty); set => SetValue(RemoveCommandProperty, value); }
        public ICommand ClearAllCommand { get => (ICommand)GetValue(ClearAllCommandProperty); set => SetValue(ClearAllCommandProperty, value); }
    }

    internal sealed class NriLocalCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Func<object?, bool>? _canExecute;

        internal NriLocalCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
        public void Execute(object? parameter) => _execute(parameter);
        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
