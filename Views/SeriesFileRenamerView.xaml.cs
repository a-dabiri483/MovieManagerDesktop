using System.Windows.Controls;
using MovieManagerDesktop.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace MovieManagerDesktop.Views
{
    public partial class SeriesFileRenamerView : UserControl
    {
        public SeriesFileRenamerView()
        {
            InitializeComponent();
        }

        private void Border_Drop(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
                if (files != null && files.Length > 0 && DataContext is SeriesFileRenamerViewModel vm)
                {
                    string path = files[0];
                    if (System.IO.Directory.Exists(path))
                    {
                        vm.SelectedFolderPath = path;
                        if (string.IsNullOrEmpty(vm.CustomBaseName))
                        {
                            vm.CustomBaseName = new System.IO.DirectoryInfo(path).Name;
                        }
                        
                        // Automatically trigger scan after drop
                        if (vm.ScanFolderCommand.CanExecute(null))
                        {
                            vm.ScanFolderCommand.Execute(null);
                        }
                    }
                }
            }
        }
    }
}
