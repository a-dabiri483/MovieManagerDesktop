using System.Windows.Controls;

namespace MovieManagerDesktop.Views
{
    public partial class ScanView : UserControl
    {
        public ScanView()
        {
            InitializeComponent();
        }

        private void ResultsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is ViewModels.ScanViewModel vmContext)
            {
                vmContext.UpdateBulkToolbar();
            }
        }

        private void ResultsDataGrid_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Space && !(e.OriginalSource is TextBox))
            {
                var grid = (DataGrid)sender;
                if (grid.SelectedItems.Count > 0)
                {
                    // Determine new state based on the first selected item
                    bool newState = true;
                    if (grid.SelectedItems[0] is ViewModels.ScannedGroupViewModel first)
                        newState = !first.IsChecked;

                    foreach (var item in grid.SelectedItems)
                    {
                        if (item is ViewModels.ScannedGroupViewModel vm)
                            vm.IsChecked = newState;
                    }
                    e.Handled = true;
                    
                    if (DataContext is ViewModels.ScanViewModel vmContext)
                    {
                        vmContext.UpdateBulkToolbar();
                    }
                }
            }
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        private void CheckBox_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is ViewModels.ScanViewModel vm)
            {
                vm.UpdateBulkToolbar();
            }
        }

        private void TextBox_TextChanged_1(object sender, TextChangedEventArgs e)
        {

        }
    }
}
