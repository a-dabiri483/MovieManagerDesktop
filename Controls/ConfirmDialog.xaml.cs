using System.Windows.Controls;

namespace MovieManagerDesktop.Controls
{
    public partial class ConfirmDialog : UserControl
    {
        public string Message { get; }

        public ConfirmDialog(string message)
        {
            InitializeComponent();
            Message = message;
            DataContext = this;
        }
    }
}
