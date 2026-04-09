using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Data;

namespace sqlSense.ViewModels.Modules
{
    public partial class TablePreviewViewModel : ObservableObject
    {
        [ObservableProperty]
        private DataTable? _tableData;

        [ObservableProperty]
        private DataView? _pagedData;

        [ObservableProperty]
        private string _tableName = "";

        [ObservableProperty]
        private bool _isVisible;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private bool _hideUnselectedColumns;

        public Action<string>? OnColumnToggle { get; set; }
        public List<string> UsedColumns { get; set; } = new();

        [ObservableProperty]
        private int _currentPage = 1;

        [ObservableProperty]
        private int _totalPages = 1;

        [ObservableProperty]
        private string _pageInfo = "Page 1 of 1";

        private const int PageSize = 5;

        public TablePreviewViewModel() { }

        public void Reset()
        {
            TableData = null;
            PagedData = null;
            IsVisible = false;
        }

        public void UpdatePagedData()
        {
            if (TableData == null) return;

            var paged = TableData.Clone();
            int start = (CurrentPage - 1) * PageSize;
            int end = Math.Min(start + PageSize, TableData.Rows.Count);

            for (int i = start; i < end; i++)
            {
                paged.ImportRow(TableData.Rows[i]);
            }

            PagedData = paged.DefaultView;
            PageInfo = $"Page {CurrentPage} of {TotalPages}  ({TableData.Rows.Count} rows)";
        }

        [RelayCommand]
        public void NextPage()
        {
            if (CurrentPage < TotalPages)
            {
                CurrentPage++;
                UpdatePagedData();
            }
        }

        [RelayCommand]
        public void PreviousPage()
        {
            if (CurrentPage > 1)
            {
                CurrentPage--;
                UpdatePagedData();
            }
        }
    }
}
