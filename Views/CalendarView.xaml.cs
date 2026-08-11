using System.Windows.Controls;
using MovieManagerDesktop.ViewModels;

namespace MovieManagerDesktop.Views
{
    public partial class CalendarView : UserControl
    {
        public CalendarView()
        {
            InitializeComponent();
            DataContext = new CalendarViewModel();
        }
    }
}
