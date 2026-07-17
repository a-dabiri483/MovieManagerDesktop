using System.Windows;
using System.Windows.Controls;

namespace MovieManagerDesktop.Controls
{
    public partial class ShimmerEffect : UserControl
    {
        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.Register("CornerRadius", typeof(CornerRadius), typeof(ShimmerEffect), new PropertyMetadata(new CornerRadius(8)));

        public CornerRadius CornerRadius
        {
            get { return (CornerRadius)GetValue(CornerRadiusProperty); }
            set { SetValue(CornerRadiusProperty, value); }
        }

        public ShimmerEffect()
        {
            InitializeComponent();
        }
    }
}
