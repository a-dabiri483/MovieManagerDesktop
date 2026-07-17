using System.Windows.Controls;

namespace MovieManagerDesktop.Controls
{
    public partial class AlertDialog : UserControl
    {
        public string Message { get; }

        public AlertDialog(string message)
        {
            InitializeComponent();
            Message = message;
            DataContext = this;
        }
    }
}
