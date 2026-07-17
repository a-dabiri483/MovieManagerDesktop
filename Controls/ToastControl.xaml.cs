using MovieManagerDesktop.Models;
using MovieManagerDesktop.Services;
using System.Windows;
using System.Windows.Controls;

namespace MovieManagerDesktop.Controls
{
    public partial class ToastControl : UserControl
    {
        public ToastControl()
        {
            InitializeComponent();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ToastMessage toast)
            {
                ToastService.Instance.Remove(toast);
            }
        }
    }
}
