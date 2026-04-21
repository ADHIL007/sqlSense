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
    }
}
