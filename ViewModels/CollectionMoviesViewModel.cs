using System.Threading.Tasks;
using System.Linq;

namespace MovieManagerDesktop.ViewModels
{
    public class CollectionMoviesViewModel : MoviesViewModel
    {
        public string CollectionName { get; }

        public CollectionMoviesViewModel(string collectionName) : base()
        {
            CollectionName = collectionName;
            PageTitle = $"مجموعه {CollectionName}";
            ShowFilters = false;
            CollectionFilter = collectionName;
        }
    }
}
