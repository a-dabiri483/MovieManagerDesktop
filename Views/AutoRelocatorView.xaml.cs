using System.Windows;
using System.Windows.Controls;
using MovieManagerDesktop.ViewModels;

namespace MovieManagerDesktop.Views
{
    public partial class AutoRelocatorView : UserControl
    {
        public AutoRelocatorView()
        {
            InitializeComponent();
        }

        private void TabBroken_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is AutoRelocatorViewModel vm)
            {
                vm.SelectedTabIndex = 0;
            }
        }

        private void TabTrash_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is AutoRelocatorViewModel vm)
            {
                vm.SelectedTabIndex = 1;
            }
        }
    }
}
