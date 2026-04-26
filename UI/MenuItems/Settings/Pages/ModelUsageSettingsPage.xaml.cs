using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using sqlSense.Services.Ai;

namespace sqlSense.UI.MenueItems.Settings.Pages
{
    public partial class ModelUsageSettingsPage : UserControl
    {
        private List<ModelUsageDisplayItem> _allItems = new List<ModelUsageDisplayItem>();
        private int _currentPage = 1;
        private int _rowsPerPage = 8;

        public ModelUsageSettingsPage()
        {
            InitializeComponent();
            Loaded += (s, e) => RefreshData();
        }

        private DateTime? GetFilterDate()
        {
            var selected = CmbTimeRange.SelectedIndex;
            switch (selected)
            {
                case 0: return DateTime.UtcNow.Date;
                case 1: return DateTime.UtcNow.AddHours(-24);
                case 2: return DateTime.UtcNow.AddDays(-7);
                case 3: return DateTime.UtcNow.AddDays(-30);
                case 4:
                default: return null;
            }
        }

        private void RefreshData()
        {
            var since = GetFilterDate();
            var summaries = ModelUsageTracker.GetSummaries(since);

            _allItems = summaries.Select(s => new ModelUsageDisplayItem
            {
                Model = s.Model,
                Provider = s.Provider,
                Requests = s.Requests,
                PromptTokens = s.PromptTokens,
                CompletionTokens = s.CompletionTokens,
                TotalTokens = s.TotalTokens,
                LastUsedDisplay = FormatLastUsed(s.LastUsed)
            }).ToList();

            // Update summary cards
            int totalRequests = summaries.Sum(s => s.Requests);
            int totalPrompt = summaries.Sum(s => s.PromptTokens);
            int totalCompletion = summaries.Sum(s => s.CompletionTokens);
            int totalTokens = totalPrompt + totalCompletion;

            TbTotalRequests.Text = FormatNumber(totalRequests);
            TbTotalPromptTokens.Text = FormatNumber(totalPrompt);
            TbTotalCompletionTokens.Text = FormatNumber(totalCompletion);
            TbTotalTokens.Text = FormatNumber(totalTokens);

            // Total row
            TbTotalRowRequests.Text = totalRequests.ToString("N0");
            TbTotalRowPrompt.Text = totalPrompt.ToString("N0");
            TbTotalRowCompletion.Text = totalCompletion.ToString("N0");
            TbTotalRowTokens.Text = totalTokens.ToString("N0");

            TbLastUpdated.Text = $"Last updated: {DateTime.Now:h:mm:ss tt}";

            // Reset to page 1 and render
            _currentPage = 1;
            RenderPage();
        }

        private void RenderPage()
        {
            int totalItems = _allItems.Count;
            int totalPages = Math.Max(1, (int)Math.Ceiling((double)totalItems / _rowsPerPage));
            if (_currentPage > totalPages) _currentPage = totalPages;
            if (_currentPage < 1) _currentPage = 1;

            var pageItems = _allItems
                .Skip((_currentPage - 1) * _rowsPerPage)
                .Take(_rowsPerPage)
                .ToList();

            UsageGrid.ItemsSource = pageItems;

            int start = totalItems == 0 ? 0 : ((_currentPage - 1) * _rowsPerPage) + 1;
            int end = Math.Min(_currentPage * _rowsPerPage, totalItems);
            TbShowingInfo.Text = $"Showing {start} to {end} of {totalItems} models";

            // Update pagination buttons
            UpdatePaginationButtons(totalPages);
        }

        private void UpdatePaginationButtons(int totalPages)
        {
            BtnPage1.Content = "1";
            BtnPage1.Style = _currentPage == 1 ? (Style)FindResource("PageButtonActive") : (Style)FindResource("PageButton");
            BtnPage1.Visibility = totalPages >= 1 ? Visibility.Visible : Visibility.Collapsed;

            if (totalPages >= 2)
            {
                BtnPage2.Content = "2";
                BtnPage2.Style = _currentPage == 2 ? (Style)FindResource("PageButtonActive") : (Style)FindResource("PageButton");
                BtnPage2.Visibility = Visibility.Visible;
            }
            else
            {
                BtnPage2.Visibility = Visibility.Collapsed;
            }

            if (totalPages >= 3)
            {
                BtnPage3.Content = "3";
                BtnPage3.Style = _currentPage == 3 ? (Style)FindResource("PageButtonActive") : (Style)FindResource("PageButton");
                BtnPage3.Visibility = Visibility.Visible;
            }
            else
            {
                BtnPage3.Visibility = Visibility.Collapsed;
            }

            // For pages > 3, shift the visible page numbers
            if (_currentPage > 3)
            {
                BtnPage1.Content = (_currentPage - 2).ToString();
                BtnPage1.Style = (Style)FindResource("PageButton");
                BtnPage2.Content = (_currentPage - 1).ToString();
                BtnPage2.Style = (Style)FindResource("PageButton");
                BtnPage3.Content = _currentPage.ToString();
                BtnPage3.Style = (Style)FindResource("PageButtonActive");
                BtnPage2.Visibility = Visibility.Visible;
                BtnPage3.Visibility = Visibility.Visible;
            }
        }

        private string FormatNumber(int value)
        {
            if (value >= 1_000_000) return $"{value / 1_000_000.0:F1}M";
            if (value >= 1_000) return value.ToString("N0");
            return value.ToString();
        }

        private string FormatLastUsed(DateTime utcTime)
        {
            var local = utcTime.ToLocalTime();
            var span = DateTime.Now - local;

            if (span.TotalMinutes < 1) return "Just now";
            if (span.TotalHours < 1) return $"{(int)span.TotalMinutes} min ago";
            if (span.TotalDays < 1) return local.ToString("h:mm:ss tt");
            if (span.TotalDays < 2) return "Yesterday";
            if (span.TotalDays < 7) return $"{(int)span.TotalDays} days ago";
            return local.ToString("MMM dd, yyyy");
        }

        // ── Event Handlers ──

        private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshData();

        private void TimeRange_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded) RefreshData();
        }

        private void RowsPerPage_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            var item = CmbRowsPerPage.SelectedItem as ComboBoxItem;
            if (item != null && int.TryParse(item.Content.ToString(), out int rows))
            {
                _rowsPerPage = rows;
                _currentPage = 1;
                RenderPage();
            }
        }

        private void PageFirst_Click(object sender, RoutedEventArgs e) { _currentPage = 1; RenderPage(); }
        private void PagePrev_Click(object sender, RoutedEventArgs e) { if (_currentPage > 1) _currentPage--; RenderPage(); }
        private void PageNext_Click(object sender, RoutedEventArgs e)
        {
            int totalPages = Math.Max(1, (int)Math.Ceiling((double)_allItems.Count / _rowsPerPage));
            if (_currentPage < totalPages) _currentPage++;
            RenderPage();
        }
        private void PageLast_Click(object sender, RoutedEventArgs e)
        {
            _currentPage = Math.Max(1, (int)Math.Ceiling((double)_allItems.Count / _rowsPerPage));
            RenderPage();
        }
        private void PageNum_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && int.TryParse(btn.Content.ToString(), out int page))
            {
                _currentPage = page;
                RenderPage();
            }
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var since = GetFilterDate();
                var csv = ModelUsageTracker.ExportCsv(since);
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "CSV Files|*.csv",
                    FileName = $"sqlsense_model_usage_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
                    DefaultExt = ".csv"
                };
                if (dialog.ShowDialog() == true)
                {
                    System.IO.File.WriteAllText(dialog.FileName, csv);
                    MessageBox.Show("Usage data exported successfully.", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearData_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Are you sure you want to clear all model usage data? This action cannot be undone.",
                "Clear Usage Data",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                ModelUsageTracker.ClearAll();
                RefreshData();
            }
        }
    }

    /// <summary>
    /// Display model for the DataGrid with formatted fields.
    /// </summary>
    public class ModelUsageDisplayItem
    {
        public string Model { get; set; }
        public string Provider { get; set; }
        public int Requests { get; set; }
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
        public int TotalTokens { get; set; }
        public string LastUsedDisplay { get; set; }
    }
}
