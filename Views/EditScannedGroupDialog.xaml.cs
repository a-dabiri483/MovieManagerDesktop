using MaterialDesignThemes.Wpf;
using System.Windows;
using System.Windows.Controls;

namespace MovieManagerDesktop.Views
{
    public partial class EditScannedGroupDialog : Window
    {
        public EditScannedGroupDialog()
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

        private void SubmitBtn_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {

        }
    }
}
