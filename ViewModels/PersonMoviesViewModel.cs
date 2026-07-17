using System.Threading.Tasks;
using System.Linq;

namespace MovieManagerDesktop.ViewModels
{
    public class PersonMoviesViewModel : MoviesViewModel
    {
        public string PersonName { get; }
        public string PersonType { get; }

        public PersonMoviesViewModel(string personName, string personType) : base()
        {
            PersonName = personName;
            PersonType = personType;
            PageTitle = $"فیلم‌های {PersonName}";
            ShowFilters = false;
            PersonFilterName = personName;
            PersonFilterType = personType;
        }
    }
}
