using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MovieManagerDesktop.Messages;
using MovieManagerDesktop.Models;
using MovieManagerDesktop.Services;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace MovieManagerDesktop.ViewModels
{
    public partial class SeriesOrganizerToolViewModel : ObservableObject
    {
        private readonly SeriesOrganizerService _organizerService = new();

        [ObservableProperty]
        private string _selectedFolderPath = string.Empty;

        [ObservableProperty]
        private bool _isProcessing;

        [ObservableProperty]
        private string _statusMessage = "آماده برای سازمان‌دهی سریال‌ها";

        [ObservableProperty]
        private int _totalFound;

        [ObservableProperty]
        private int _totalMoved;

        public ObservableCollection<OrganizerItem> PendingItems { get; } = new();
        public ObservableCollection<OrganizerItem> CompletedItems { get; } = new();

        [RelayCommand]
        private void SelectAllPending()
        {
            foreach (var item in PendingItems) item.IsSelected = true;
        }

        [RelayCommand]
        private void DeselectAllPending()
        {
            foreach (var item in PendingItems) item.IsSelected = false;
        }

        [RelayCommand]
        private void GoBack()
        {
            WeakReferenceMessenger.Default.Send(new NavigationMessage(new ToolsViewModel()));
        }

        [RelayCommand]
        private void SelectFolder()
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "انتخاب پوشه حاوی قسمت‌های سریال"
            };
            if (dialog.ShowDialog() == true)
            {
                SelectedFolderPath = dialog.FolderName;
            }
        }

        [RelayCommand]
        private async Task ScanFolderAsync()
        {
            if (string.IsNullOrWhiteSpace(SelectedFolderPath) || !Directory.Exists(SelectedFolderPath))
            {
                StatusMessage = "❌ مسیر نامعتبر است.";
                return;
            }

            IsProcessing = true;
            StatusMessage = "🔍 در حال اسکن...";
            PendingItems.Clear();
            CompletedItems.Clear();
            TotalFound = 0;
            TotalMoved = 0;

            try
            {
                var items = await _organizerService.ScanFolderAsync(SelectedFolderPath);
                var seriesItems = items.Where(i => i.IsSeries).ToList();
                
                foreach (var item in seriesItems)
                {
                    PendingItems.Add(item);
                }

                TotalFound = PendingItems.Count;
                if (TotalFound == 0)
                {
                    StatusMessage = "✅ هیچ فایل سریالی در این پوشه یافت نشد.";
                }
                else
                {
                    StatusMessage = $"✅ {TotalFound} قسمت سریال برای مرتب‌سازی پیدا شد.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"❌ خطا: {ex.Message}";
            }
            finally
            {
                IsProcessing = false;
            }
        }

        [RelayCommand]
        private async Task OrganizeSeriesAsync()
        {
            var itemsToProcess = PendingItems.Where(i => i.IsSelected && i.Status == OrganizerStatus.Pending).ToList();

            if (!itemsToProcess.Any())
            {
                StatusMessage = "⚠️ هیچ فایلی برای سازمان‌دهی وجود ندارد.";
                return;
            }

            IsProcessing = true;
            StatusMessage = $"⏳ در حال جابه‌جایی {itemsToProcess.Count} فایل...";
            
            try
            {
                var progressReport = new Progress<OrganizerProgressReport>(report =>
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        report.Item.Status = report.Status;
                        report.Item.ErrorMessage = report.ErrorMessage ?? string.Empty;

                        if (report.Status == OrganizerStatus.Moved || report.Status == OrganizerStatus.Skipped || report.Status == OrganizerStatus.Error)
                        {
                            if (PendingItems.Contains(report.Item))
                            {
                                PendingItems.Remove(report.Item);
                                CompletedItems.Add(report.Item);
                            }
                        }
                    });
                });

                await _organizerService.OrganizeAsync(itemsToProcess, SelectedFolderPath, progressReport);

                int successCount = itemsToProcess.Count(i => i.Status == OrganizerStatus.Moved);
                TotalMoved += successCount;

                StatusMessage = $"✅ عملیات پایان یافت. {successCount} فایل منتقل شد.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"❌ خطا: {ex.Message}";
            }
            finally
            {
                IsProcessing = false;
            }
        }
    }
}
