using CommunityToolkit.Mvvm.ComponentModel;
using sqlSense.Models;
using System;

namespace sqlSense.ViewModels.Modules
{
    public partial class ViewCanvasViewModel : ObservableObject
    {
        [ObservableProperty]
        private ViewDefinitionInfo? _currentViewDefinition;

        [ObservableProperty]
        private bool _isVisible = true;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private double _zoom = 1.0;

        [ObservableProperty]
        private string _zoomPercentage = "100%";

        public event Action? OnNewWorkspaceRequested;

        public ViewCanvasViewModel() { }

        public void RequestNewWorkspace()
        {
            CurrentViewDefinition = null;
            IsVisible = true;
            OnNewWorkspaceRequested?.Invoke();
        }

        [RelayCommand] private void ZoomIn() => Zoom = Math.Min(Zoom + 0.1, 5.0);
        [RelayCommand] private void ZoomOut() => Zoom = Math.Max(Zoom - 0.1, 0.1);
        [RelayCommand] private void ZoomReset() => Zoom = 1.0;
        [RelayCommand] private void ZoomFit() => Zoom = 0.5;

        public void SetZoom(double value) => Zoom = Math.Clamp(value, 0.1, 5.0);

        partial void OnZoomChanged(double value)
        {
            ZoomPercentage = $"{(int)(value * 100)}%";
        }
    }
}
