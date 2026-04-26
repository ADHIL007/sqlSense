using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using sqlSense.Models;
using sqlSense.Services;
using sqlSense.ViewModels.Modules;
using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;

namespace sqlSense.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        // === Sub-ViewModels (Composition) ===
        public SqlEditorViewModel SqlEditor { get; } = new();
        public TablePreviewViewModel TablePreview { get; } = new();
        public ViewCanvasViewModel Canvas { get; } = new();
        public DatabaseExplorerViewModel Explorer { get; } = new();
        
        public ObservableCollection<ViewDefinitionInfo> OpenWorkbooks { get; } = new();

        [ObservableProperty]
        private ViewDefinitionInfo? _activeWorkbook;

        [ObservableProperty]
        private bool _hasUnsavedChanges;

        [ObservableProperty]
        private string _statusMessage = "Ready";

        [ObservableProperty]
        private string _statusBackground = "#007ACC";

        [ObservableProperty]
        private bool _isAiChatVisible = true;

        [RelayCommand]
        private void ToggleAiChat() => IsAiChatVisible = !IsAiChatVisible;

        partial void OnStatusMessageChanged(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                StatusBackground = "#007ACC";
                return;
            }

            var lower = value.ToLower();
            if (lower.Contains("fail") || lower.Contains("error") || lower.Contains("⚠") || lower.Contains("invalid"))
                StatusBackground = "#CA5100";
            else if (lower.Contains("success") || lower.Contains("saved") || lower.Contains("executed"))
                StatusBackground = "#16825D";
            else
                StatusBackground = "#007ACC";
        }

        [ObservableProperty]
        private string _connectionString = "";

        [ObservableProperty]
        private string _serverName = "";

        private DatabaseService? _dbService;
        public DatabaseService? DbService => _dbService;

        private readonly WorkbookService _workbookService = new();
        private readonly Stack<string> _undoStack = new();
        private readonly Stack<string> _redoStack = new();
        private bool _isPerformingUndoRedo = false;

        public event Action? OnNewWorkspaceRequested;
        public event Action? OnCreateTableRequested;
        public event Action? OnChartNeedsRerender;

        public MainViewModel()
        {
            Canvas.OnNewWorkspaceRequested += () => OnNewWorkspaceRequested?.Invoke();
            Canvas.PropertyChanged += (s, e) => {
                if (e.PropertyName == nameof(Canvas.CurrentViewDefinition)) {
                    SaveWorkbookCommand.NotifyCanExecuteChanged();
                    GenerateViewSqlCommand.NotifyCanExecuteChanged();
                    if (Canvas.CurrentViewDefinition != ActiveWorkbook)
                        ActiveWorkbook = Canvas.CurrentViewDefinition;
                }
            };

            SqlEditor.PropertyChanged += (s, e) => {
                if (e.PropertyName == nameof(SqlEditorViewModel.ViewMode))
                    if (SqlEditor.ShowChart && ActiveWorkbook != null && !SqlEditor.IsChartDisabled)
                        SyncSqlToChart();
            };
        }

        partial void OnActiveWorkbookChanged(ViewDefinitionInfo? value)
        {
            if (value != null)
            {
                if (Canvas.CurrentViewDefinition != value)
                {
                    Canvas.CurrentViewDefinition = value;
                    if (!OpenWorkbooks.Contains(value)) OpenWorkbooks.Add(value);
                }
                SqlEditor.SqlText = string.IsNullOrEmpty(value.SqlDefinition) ? value.ToSql() : value.SqlDefinition;
                Canvas.Zoom = value.CanvasZoom > 0 ? value.CanvasZoom : 1.0;
            }
            else
            {
                Canvas.CurrentViewDefinition = null;
                SqlEditor.SqlText = "";
            }
        }

        public void InitializeServices(string connectionString, string serverName)
        {
            ConnectionString = connectionString;
            ServerName = serverName;
            _dbService = new DatabaseService(connectionString);
            Explorer.Initialize(_dbService, serverName);
        }
    }
}
