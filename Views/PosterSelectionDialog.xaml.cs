using System.Windows;
using System.Windows.Input;
using MovieManagerDesktop.ViewModels;

namespace MovieManagerDesktop.Views
{
    public partial class PosterSelectionDialog : Window
    {
        public PosterSelectionDialog(PosterSelectionViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            
            viewModel.RequestClose = () =>
            {
                Dispatcher.Invoke(() => Close());
            };
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            DragMove();
        }
    }
}
