using MovieManagerDesktop.Models;
using System.Windows;
using System.Windows.Input;

namespace MovieManagerDesktop.Views
{
    public partial class DataCleanupWindow : Window
    {
        public DataCleanupOptions Options { get; }

        public DataCleanupWindow(DataCleanupOptions options)
        {
            InitializeComponent();
            Options = options;
            DataContext = Options;
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BtnConfirm_Click(object sender, RoutedEventArgs e)
        {
            if (!Options.HasAnySelected)
            {
                return;
            }

            DialogResult = true;
            Close();
        }
    }
}
