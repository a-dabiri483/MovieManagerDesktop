using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MovieManagerDesktop.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace MovieManagerDesktop.ViewModels
{
    public partial class ApiSearchDialogViewModel : ObservableObject
    {
        private readonly IdentifyMediaService _identifyService;

        [ObservableProperty]
        private string _searchQuery = string.Empty;

        [ObservableProperty]
        private bool _isSearching;

        [ObservableProperty]
        private bool _hasSearched;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private bool _isSearchTypeMovie = true;

        [ObservableProperty]
        private bool _isSearchTypeSeries = false;

        [ObservableProperty]
        private bool _isSearchTypeAnime = false;

        public ObservableCollection<TmdbSearchResult> SearchResults { get; } = new();
        public System.Action CloseAction { get; set; }
        public System.Action<TmdbSearchResult> SelectAction { get; set; }

        public ApiSearchDialogViewModel(string initialQuery)
        {
            _identifyService = new IdentifyMediaService();
            SearchQuery = initialQuery;
        }

        [RelayCommand]
        private async Task SearchAsync()
        {
            if (string.IsNullOrWhiteSpace(SearchQuery))
            {
                ToastService.Instance.ShowWarning("لطفاً عبارت جستجو را وارد کنید.");
                return;
            }

            if (IsSearching) return;

            IsSearching = true;
            HasSearched = true;
            StatusMessage = "در حال جستجو در سرور...";
            SearchResults.Clear();
            LoggerService.Info($"[Search] Manual search started for '{SearchQuery}'");

            try
            {
                List<TmdbSearchResult>? results = null;

                if (IsSearchTypeAnime)
                {
                    results = await _identifyService.SearchAnimeManualAsync(SearchQuery);
                }
                else
                {
                    results = await _identifyService.SearchMediaAsync(SearchQuery);
                    if (results != null)
                    {
                        if (IsSearchTypeMovie)
                        {
                            results = results.FindAll(r => (r.MediaType ?? "").ToLower() != "tv" && (r.MediaType ?? "").ToLower() != "series");
                        }
                        else if (IsSearchTypeSeries)
                        {
                            results = results.FindAll(r => (r.MediaType ?? "").ToLower() == "tv" || (r.MediaType ?? "").ToLower() == "series");
                        }
                    }
                }

                if (results != null && results.Count > 0)
                {
                    foreach (var r in results)
                    {
                        SearchResults.Add(r);
                    }
                    StatusMessage = $"{SearchResults.Count} مورد یافت شد.";
                }
                else
                {
                    StatusMessage = $"هیچ نتیجه‌ای برای «{SearchQuery}» یافت نشد.";
                    ToastService.Instance.ShowWarning($"موردی برای «{SearchQuery}» یافت نشد. املای نام یا نوع جستجو را بررسی کنید.");
                }

                LoggerService.Info($"[Search] Found {SearchResults.Count} results for '{SearchQuery}'");
            }
            catch (Exception ex)
            {
                LoggerService.Error($"[Search] Search failed for '{SearchQuery}': {ex.Message}", ex);
                string errMessage = ex.Message.ToLower().Contains("socket") || ex.Message.ToLower().Contains("network") || ex.Message.ToLower().Contains("timeout") || ex.Message.ToLower().Contains("task was canceled")
                    ? "عدم برقراری ارتباط با سرور. لطفاً وضعیت اینترنت یا قندشکن را بررسی کنید."
                    : $"خطا در جستجو: {ex.Message}";
                StatusMessage = errMessage;
                ToastService.Instance.ShowError(errMessage);
            }
            finally
            {
                IsSearching = false;
            }
        }

        [RelayCommand]
        private void SelectResult(TmdbSearchResult result)
        {
            if (result == null) return;
            ToastService.Instance.ShowSuccess($"«{result.Title}» انتخاب شد.");
            SelectAction?.Invoke(result);
            CloseAction?.Invoke();
        }
        
        [RelayCommand]
        private void Cancel()
        {
            CloseAction?.Invoke();
        }
    }
}
