using System.Windows.Controls;

namespace MovieManagerDesktop.Views
{
    public partial class MediaDetailsView : UserControl
    {
        public MediaDetailsView()
        {
            InitializeComponent();
        }

        private void DetailsScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (BackdropTranslate != null)
            {
                // Move backdrop slower than scroll (parallax)
                BackdropTranslate.Y = -e.VerticalOffset * 0.3;
            }
        }
    }
}
