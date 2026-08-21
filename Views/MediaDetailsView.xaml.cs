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
            // Backdrop is fixed in place for stability
            if (BackdropTranslate != null)
            {
                BackdropTranslate.Y = 0;
            }
        }
    }
}
