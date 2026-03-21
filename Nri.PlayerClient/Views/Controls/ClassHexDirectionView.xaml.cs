using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Nri.PlayerClient.Views.Controls;

public partial class ClassHexDirectionView : UserControl
{
    public static readonly DependencyProperty DirectionSelectCommandProperty = DependencyProperty.Register(
        nameof(DirectionSelectCommand), typeof(ICommand), typeof(ClassHexDirectionView));

    public ICommand? DirectionSelectCommand
    {
        get => (ICommand?)GetValue(DirectionSelectCommandProperty);
        set => SetValue(DirectionSelectCommandProperty, value);
    }

    public ClassHexDirectionView()
    {
        InitializeComponent();
    }
}
