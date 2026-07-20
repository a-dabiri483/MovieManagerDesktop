using System.Windows;
using System.Windows.Input;

namespace MovieManagerDesktop.Views.Dialogs
{
    public partial class ApiSearchDialog : Window
    {
        public ApiSearchDialog()
        {
            InitializeComponent();
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }
    }
}
