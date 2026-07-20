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
    public partial class NameCleanerToolViewModel : ObservableObject
    {
        private readonly FolderNameCleanerService _cleanerService = new();

        [ObservableProperty]
        private string _selectedFolderPath = string.Empty;

        [ObservableProperty]
        private bool _isProcessing;

        [ObservableProperty]
        private string _statusMessage = "آماده برای پاک‌سازی نام پوشه‌ها";

        [ObservableProperty]
        private int _totalFound;

        [ObservableProperty]
        private int _totalCleaned;

        public ObservableCollection<string> Logs { get; } = new();

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
                Title = "انتخاب پوشه حاوی فیلم‌ها و سریال‌ها"
            };
            if (dialog.ShowDialog() == true)
            {
                SelectedFolderPath = dialog.FolderName;
                Log($"پوشه انتخاب شد: {SelectedFolderPath}");
            }
        }

        public ObservableCollection<FolderRenameItem> PendingItems { get; } = new();
        public ObservableCollection<FolderRenameItem> CompletedItems { get; } = new();

        private bool _isAllSelected = true;
        public bool IsAllSelected
        {
            get => _isAllSelected;
            set
            {
                if (SetProperty(ref _isAllSelected, value))
                {
                    foreach (var item in PendingItems)
                    {
                        item.IsSelected = value;
                    }
                }
            }
        }

        [RelayCommand]
        private void CheckSelectedItems(System.Collections.IList selectedItems)
        {
            if (selectedItems == null) return;
            foreach (var item in selectedItems.Cast<FolderRenameItem>())
            {
                item.IsSelected = true;
            }
        }

        [RelayCommand]
        private void UncheckSelectedItems(System.Collections.IList selectedItems)
        {
            if (selectedItems == null) return;
            foreach (var item in selectedItems.Cast<FolderRenameItem>())
            {
                item.IsSelected = false;
            }
        }

        [RelayCommand]
        private async Task ScanFolderNamesAsync()
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
            TotalCleaned = 0;

            try
            {
                var dirtyItems = await _cleanerService.ScanForDirtyFoldersAsync(SelectedFolderPath);
                
                foreach (var item in dirtyItems)
                {
                    PendingItems.Add(item);
                }

                TotalFound = PendingItems.Count;
                if (TotalFound == 0)
                {
                    StatusMessage = "✅ تمامی پوشه‌ها مرتب هستند و نیازی به تغییر نام ندارند.";
                }
                else
                {
                    StatusMessage = $"✅ {TotalFound} پوشه نیازمند تغییر نام پیدا شد.";
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
        private async Task CleanFolderNamesAsync()
        {
            var itemsToProcess = PendingItems.Where(i => i.IsSelected).ToList();

            if (!itemsToProcess.Any())
            {
                StatusMessage = "⚠️ هیچ پوشه‌ای برای اصلاح انتخاب نشده است.";
                return;
            }

            IsProcessing = true;
            StatusMessage = $"⏳ در حال تغییر نام {itemsToProcess.Count} پوشه...";
            
            try
            {
                var progressReport = new Progress<RenameProgressReport>(report =>
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        report.Item.Status = report.Status;
                        report.Item.ErrorMessage = report.ErrorMessage ?? string.Empty;

                        if (report.Status == RenameStatus.Success || report.Status == RenameStatus.Error)
                        {
                            if (PendingItems.Contains(report.Item))
                            {
                                PendingItems.Remove(report.Item);
                                CompletedItems.Add(report.Item);
                            }
                        }
                    });
                });

                await _cleanerService.CleanFoldersAsync(itemsToProcess, progressReport);

                int successCount = itemsToProcess.Count(i => i.Status == RenameStatus.Success);
                TotalCleaned += successCount;

                StatusMessage = $"✅ عملیات پایان یافت. {successCount} مورد موفق.";
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


        private void Log(string message)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Logs.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
            });
        }
    }
}
