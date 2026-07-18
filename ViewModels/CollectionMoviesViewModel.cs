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
            
            // Remove "Collection" and "مجموعه" (case-insensitive) to prevent duplicates
            string cleanName = System.Text.RegularExpressions.Regex.Replace(collectionName, @"(?i)collection", "").Trim();
            cleanName = cleanName.Replace("مجموعه", "").Trim();
            
            PageTitle = $"مجموعه {cleanName}";
            CollectionFilter = collectionName;
        }
    }
}
