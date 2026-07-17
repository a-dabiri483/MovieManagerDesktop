using System.Threading.Tasks;

namespace MovieManagerDesktop.ViewModels
{
    public class WatchlistViewModel : MoviesViewModel
    {
        public WatchlistViewModel() : base()
        {
            PageTitle = "لیست تماشا";
            ShowFilters = false;
            ListFilterIndex = 2; // 2 = Watchlist in MoviesViewModel
        }
    }
}
