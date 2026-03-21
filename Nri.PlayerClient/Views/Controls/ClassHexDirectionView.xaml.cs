using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Nri.PlayerClient.Views.Controls
{
    public partial class ClassHexDirectionView : UserControl
    {
        public ClassHexDirectionView()
        {
            InitializeComponent();
        }

        public ICommand DirectionSelectCommand
        {
            get { return (ICommand)GetValue(DirectionSelectCommandProperty); }
            set { SetValue(DirectionSelectCommandProperty, value); }
        }

        public static readonly DependencyProperty DirectionSelectCommandProperty =
            DependencyProperty.Register(
                nameof(DirectionSelectCommand),
                typeof(ICommand),
                typeof(ClassHexDirectionView),
                new PropertyMetadata(null));
    }
}