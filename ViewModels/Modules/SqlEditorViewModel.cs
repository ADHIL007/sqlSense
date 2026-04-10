using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace sqlSense.ViewModels.Modules
{
    public partial class SqlEditorViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _sqlText = "-- Select a table from the Object Explorer\n-- to preview its data here";

        [ObservableProperty]
        private bool _isVisible = false;

        [ObservableProperty]
        private bool _isMaximized = false;

        public SqlEditorViewModel() { }

        [RelayCommand]
        private void ToggleMaximized()
        {
            IsMaximized = !IsMaximized;
            if (IsMaximized && !IsVisible) IsVisible = true;
        }

        [RelayCommand]
        private void Close()
        {
            IsVisible = false;
            IsMaximized = false;
        }

        [RelayCommand]
        private void Show() => IsVisible = true;
    }
}
