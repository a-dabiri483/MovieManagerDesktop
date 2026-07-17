using CommunityToolkit.Mvvm.ComponentModel;
using MovieManagerDesktop.Data;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace MovieManagerDesktop.ViewModels
{
    public partial class PeopleViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _pageTitle;

        [ObservableProperty]
        private bool _isLoading = false;

        [ObservableProperty]
        private string _searchQuery = string.Empty;

        private readonly string _personType;
        private List<PersonItemViewModel> _allItems = new();

        public ObservableCollection<PersonItemViewModel> People { get; } = new();

        public PeopleViewModel(string personType)
        {
            _personType = personType;
            PageTitle = personType == "Actor" ? "بازیگران" : (personType == "Director" ? "کارگردانان" : "اشخاص");
            _ = LoadPeopleAsync();
        }

        partial void OnSearchQueryChanged(string value)
        {
            FilterPeople();
        }

        public async Task LoadPeopleAsync()
        {
            if (IsLoading) return;
            IsLoading = true;
            
            try
            {
                var items = await Task.Run(() =>
                {
                    using var db = new AppDbContext();
                    var allVideos = db.VideoFiles.ToList();

                    var dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                    foreach (var video in allVideos)
                    {
                        string data = "";
                        if (_personType == "All")
                        {
                            data = $"{video.Actors}، {video.Director}";
                        }
                        else
                        {
                            data = _personType == "Actor" ? video.Actors : video.Director;
                        }

                        if (string.IsNullOrWhiteSpace(data)) continue;

                        var persons = data.Split(new[] { ',', '،' }, StringSplitOptions.RemoveEmptyEntries)
                                          .Select(p => p.Trim())
                                          .Where(p => !string.IsNullOrEmpty(p));

                        foreach (var person in persons)
                        {
                            if (dict.ContainsKey(person))
                                dict[person]++;
                            else
                                dict[person] = 1;
                        }
                    }

                    return dict.OrderByDescending(x => x.Value)
                               .Select(x => new PersonItemViewModel(x.Key, x.Value, _personType))
                               .ToList();
                });

                _allItems = items;
                FilterPeople();
            }
            catch { }
            finally
            {
                IsLoading = false;
            }
        }

        private void FilterPeople()
        {
            People.Clear();
            var query = _searchQuery?.ToLowerInvariant() ?? "";

            var filtered = string.IsNullOrWhiteSpace(query) 
                ? _allItems 
                : _allItems.Where(p => p.Name.ToLowerInvariant().Contains(query));

            foreach (var p in filtered)
            {
                People.Add(p);
            }
        }
    }
}
