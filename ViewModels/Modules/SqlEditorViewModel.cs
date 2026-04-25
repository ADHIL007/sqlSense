using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace sqlSense.ViewModels.Modules
{
    /// <summary>
    /// Manages the SQL editor state, view mode (Chart/Code/Split), and auto-sync toggle.
    /// </summary>
    public partial class SqlEditorViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _sqlText = "-- Select a table from the Object Explorer\n-- to preview its data here";

        [ObservableProperty]
        private bool _isVisible = false;

        [ObservableProperty]
        private bool _isMaximized = false;

        [ObservableProperty]
        private System.Data.DataTable? _queryResults;

        [ObservableProperty]
        private string _queryMessages = "";

        [ObservableProperty]
        private bool _hasResults = false;

        [ObservableProperty]
        private int _selectedTabIndex = 0;

        [ObservableProperty]
        private int _queryRowCount = 0;

        /// <summary>DataView for DataGrid binding — DataGrid needs DefaultView, not DataTable directly.</summary>
        public System.Data.DataView? QueryResultsView => QueryResults?.DefaultView;

        partial void OnQueryResultsChanged(System.Data.DataTable? value)
        {
            OnPropertyChanged(nameof(QueryResultsView));
            QueryRowCount = value?.Rows.Count ?? 0;
        }

        // ─── View Mode (Chart / Code / Split) ─────────────────────────
        // 0 = Visual (Chart), 1 = Code, 2 = Split
        [ObservableProperty]
        private int _viewMode = 0;

        [ObservableProperty]
        private bool _isChartMode = true;

        [ObservableProperty]
        private bool _isCodeMode = false;

        [ObservableProperty]
        private bool _isSplitMode = false;

        [ObservableProperty]
        private bool _isAutoSyncOn = true;

        // Derived visibility helpers for binding
        [ObservableProperty]
        private bool _showChart = true;

        [ObservableProperty]
        private bool _showCode = false;

        // Cursor position display
        [ObservableProperty]
        private string _cursorPosition = "Ln 1, Col 1";

        [ObservableProperty]
        private string _spacesInfo = "Spaces: 4";

        [ObservableProperty]
        private string _languageMode = "T-SQL";

        /// <summary>
        /// When true (e.g. viewing a stored proc/function), the Chart and Split
        /// view modes are disabled — only Code view is available.
        /// </summary>
        [ObservableProperty]
        private bool _isChartDisabled = false;

        partial void OnIsChartDisabledChanged(bool value)
        {
            if (value && (IsChartMode || IsSplitMode))
            {
                // Force Code-only mode
                ViewMode = 1;
                IsVisible = true;
            }
        }

        public SqlEditorViewModel() { }

        partial void OnViewModeChanged(int value)
        {
            IsChartMode = value == 0;
            IsCodeMode = value == 1;
            IsSplitMode = value == 2;
            ShowChart = value == 0 || value == 2;
            ShowCode = value == 1 || value == 2;
        }

        [RelayCommand]
        private void SetChartMode()
        {
            if (IsChartDisabled) return;   // chart disabled for procs/functions
            ViewMode = 0;
            IsMaximized = false;
        }

        [RelayCommand]
        private void SetCodeMode()
        {
            ViewMode = 1;
            IsVisible = true;
            IsMaximized = false;
        }

        [RelayCommand]
        private void SetSplitMode()
        {
            if (IsChartDisabled) return;   // split disabled for procs/functions
            ViewMode = 2;
            IsVisible = true;
            IsMaximized = false;
        }

        [RelayCommand]
        private void ToggleAutoSync()
        {
            IsAutoSyncOn = !IsAutoSyncOn;
        }

        [RelayCommand]
        private void ToggleMaximized()
        {
            IsMaximized = !IsMaximized;
            if (IsMaximized && !IsVisible) IsVisible = true;
        }

        [RelayCommand]
        private void Close()
        {
            // Close editor = go back to chart-only mode
            ViewMode = 0;
            IsVisible = false;
            IsMaximized = false;
        }

        [RelayCommand]
        private void Show() => IsVisible = true;

        [RelayCommand]
        private void HideResults() => HasResults = false;
    }
}
