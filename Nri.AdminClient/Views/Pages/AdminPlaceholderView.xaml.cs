using System.Windows;
using System.Windows.Controls;

namespace Nri.AdminClient.Views.Pages;

public partial class AdminPlaceholderView : UserControl
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(AdminPlaceholderView), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty DescriptionProperty =
        DependencyProperty.Register(nameof(Description), typeof(string), typeof(AdminPlaceholderView), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty PlannedModuleNameProperty =
        DependencyProperty.Register(nameof(PlannedModuleName), typeof(string), typeof(AdminPlaceholderView), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty StatusTextProperty =
        DependencyProperty.Register(nameof(StatusText), typeof(string), typeof(AdminPlaceholderView), new PropertyMetadata("Недоступно / Отложено"));

    public static readonly DependencyProperty RelatedFoundationStageProperty =
        DependencyProperty.Register(nameof(RelatedFoundationStage), typeof(string), typeof(AdminPlaceholderView), new PropertyMetadata("0.10+"));

    public AdminPlaceholderView()
    {
        InitializeComponent();
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public string PlannedModuleName
    {
        get => (string)GetValue(PlannedModuleNameProperty);
        set => SetValue(PlannedModuleNameProperty, value);
    }

    public string StatusText
    {
        get => (string)GetValue(StatusTextProperty);
        set => SetValue(StatusTextProperty, value);
    }

    public string RelatedFoundationStage
    {
        get => (string)GetValue(RelatedFoundationStageProperty);
        set => SetValue(RelatedFoundationStageProperty, value);
    }
}
