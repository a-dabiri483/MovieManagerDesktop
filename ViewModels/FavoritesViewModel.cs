using System.Threading.Tasks;

namespace MovieManagerDesktop.ViewModels
{
    public class FavoritesViewModel : MoviesViewModel
    {
        public FavoritesViewModel() : base()
        {
            _disableSaveSettings = true;
            PageTitle = "علاقه‌مندی‌ها";
            ListFilterIndex = 1; // 1 = Favorites in MoviesViewModel
        }
    }
}
