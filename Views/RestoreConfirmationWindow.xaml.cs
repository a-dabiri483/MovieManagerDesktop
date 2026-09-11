using System.Windows;
using System.Windows.Input;
using MovieManagerDesktop.Models;

namespace MovieManagerDesktop.Views
{
    public partial class RestoreConfirmationWindow : Window
    {
        public BackupInspectionResult InspectionResult { get; }

        public RestoreConfirmationWindow(BackupInspectionResult inspectionResult)
        {
            InitializeComponent();
            InspectionResult = inspectionResult;
            this.DataContext = InspectionResult;
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void BtnConfirm_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
