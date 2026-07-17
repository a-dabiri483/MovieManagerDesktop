using System.Windows.Controls;
using System.Windows;

namespace MovieManagerDesktop.Views
{
    public partial class SelectExistingMediaDialog : Window
    {
        public SelectExistingMediaDialog()
        {
            InitializeComponent();
        }

        private void Header_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
                this.DragMove();
        }

        private void Header_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
        }

        private void Header_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
