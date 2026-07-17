using System.Threading.Tasks;

namespace MovieManagerDesktop.ViewModels
{
    public class FavoritesViewModel : MoviesViewModel
    {
        public FavoritesViewModel() : base()
        {
            PageTitle = "علاقه‌مندی‌ها";
            ShowFilters = false;
            ListFilterIndex = 1; // 1 = Favorites in MoviesViewModel
        }
    }
}
