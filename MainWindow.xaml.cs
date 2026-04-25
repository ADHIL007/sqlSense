using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using sqlSense.Models;
using sqlSense.UI;
using sqlSense.ViewModels;
using sqlSense.Views;

namespace sqlSense
{


    // ─── Main Window ──────────────────────────────────────────────────

    public partial class MainWindow : Window
    {
        private MainViewModel? _viewModel;

        private ViewGraphRenderer? _graphRenderer;

        public MainWindow()
        {
            var connectWindow = new ConnectWindow();
            if (connectWindow.ShowDialog() == true)
            {
                InitializeComponent();
                _viewModel = new MainViewModel();
                _viewModel.InitializeServices(connectWindow.ConnectionString, connectWindow.ServerTextBox.Text);
                _viewModel.StatusMessage = $"Connected to {connectWindow.ServerTextBox.Text}";
                DataContext = _viewModel;

                Loaded += async (_, _) =>
                {
                    _graphRenderer = new ViewGraphRenderer(CanvasPanel.CanvasElement, CanvasPanel.Container, CanvasPanel.Translate, _viewModel);
                    CanvasPanel.GraphRenderer = _graphRenderer;

                    _viewModel.OnNewWorkspaceRequested += () =>
                    {
                        _graphRenderer?.ClearViewVisualization();
                        CanvasPanel.CenterCanvas();
                    };

                    _viewModel.OnCreateTableRequested += () =>
                    {
                        _graphRenderer?.ShowCreateTableOnCanvas();
                    };

                    _viewModel.PropertyChanged += (s, e) => {
                        if (e.PropertyName == nameof(MainViewModel.ActiveWorkbook))
                        {
                            if (_viewModel.ActiveWorkbook != null)
                                _graphRenderer?.RenderViewVisualization(_viewModel.ActiveWorkbook);
                            else
                                _graphRenderer?.ClearViewVisualization();
                        }
                    };

                    // Re-render chart when SQL code is synced to chart model
                    _viewModel.OnChartNeedsRerender += () =>
                    {
                        if (_viewModel.ActiveWorkbook != null)
                            _graphRenderer?.RenderViewVisualization(_viewModel.ActiveWorkbook);
                    };

                    await _viewModel.LoadDatabaseTreeAsync();
                    
                    // Handle file association (if launched by double-clicking .sqv)
                    if (Application.Current.Properties.Contains("FilePath"))
                    {
                        string path = Application.Current.Properties["FilePath"] as string;
                        if (!string.IsNullOrEmpty(path))
                        {
                            _viewModel.LoadWorkbookFromFile(path);
                        }
                    }
                    else
                    {
                        // Startup State: Auto-initialize as New Workspace
                        _viewModel.NewWorkspaceCommand.Execute(null);
                    }
                    
                    CanvasPanel.CenterCanvas();

                    // Start listening for arguments from other instances
                    _ = StartPipeServer();
                };
            }
            else
            {
                Application.Current.Shutdown();
            }
        }

        private async Task StartPipeServer()
        {
            const string PipeName = "sqlSense_Communication_Pipe";
            while (true)
            {
                try
                {
                    using var server = new System.IO.Pipes.NamedPipeServerStream(PipeName, System.IO.Pipes.PipeDirection.In);
                    await server.WaitForConnectionAsync();
                    using var reader = new System.IO.StreamReader(server);
                    var filePath = await reader.ReadLineAsync();

                    if (!string.IsNullOrEmpty(filePath))
                    {
                        await Dispatcher.InvokeAsync(() =>
                        {
                            _viewModel?.LoadWorkbookFromFile(filePath);
                            
                            // Bring window to front
                            if (this.WindowState == WindowState.Minimized) this.WindowState = WindowState.Normal;
                            this.Activate();
                            this.Focus();
                        });
                    }
                }
                catch
                {
                    await Task.Delay(1000); // Wait before retrying on error
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  TREE EVENTS
        // ═══════════════════════════════════════════════════════════════

        private async void TreeViewItem_Expanded(object sender, RoutedEventArgs e)
        {
            if (_viewModel == null) return;
            if (e.OriginalSource is TreeViewItem tvi && tvi.DataContext is DatabaseTreeItem item)
            {
                if (item.NodeType == TreeNodeType.Database && item.HasDummyChild)
                    await _viewModel.ExpandDatabaseNodeAsync(item);
                else if (item.NodeType == TreeNodeType.Table && item.HasDummyChild)
                    await _viewModel.ExpandTableNodeAsync(item);
            }
        }

        private async void ObjectExplorer_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (_viewModel == null) return;
            if (e.NewValue is DatabaseTreeItem item)
            {
                if (!string.IsNullOrEmpty(item.DatabaseName))
                    _viewModel.Explorer.SelectedDatabaseName = item.DatabaseName;

                if (item.NodeType == TreeNodeType.Table)
                {
                    if (_viewModel.Canvas.CurrentViewDefinition == null)
                    {
                        if (string.IsNullOrEmpty(_viewModel.Explorer.SelectedDatabaseName))
                        {
                            _viewModel.StatusMessage = "Please select a database from the Object Explorer first.";
                            return;
                        }

                        _viewModel.Canvas.CurrentViewDefinition = new ViewDefinitionInfo
                        {
                            DatabaseName = _viewModel.Explorer.SelectedDatabaseName,
                            ViewName = "NewView",
                            SchemaName = "dbo"
                        };
                    }
                    
                    _viewModel.Canvas.IsVisible = true;
                    await _viewModel.AddTableToViewAsync(item.SchemaName, item.Tag);
                    
                    if (_viewModel.Canvas.CurrentViewDefinition != null)
                    {
                        _graphRenderer?.RenderViewVisualization(_viewModel.Canvas.CurrentViewDefinition);
                    }
                }
                else if (item.NodeType == TreeNodeType.View)
                {
                    await _viewModel.LoadViewDefinitionAsync(item.DatabaseName, item.SchemaName, item.Tag);
                    if (_viewModel.Canvas.CurrentViewDefinition != null)
                        _graphRenderer?.RenderViewVisualization(_viewModel.Canvas.CurrentViewDefinition);
                }
                else if (item.NodeType == TreeNodeType.StoredProcedure)
                {
                    // Load SP definition — chart disabled, code-only mode
                    await _viewModel.LoadStoredProcedureAsync(item.DatabaseName, item.SchemaName, item.Tag);
                }
                else if (item.NodeType == TreeNodeType.Function)
                {
                    // Load function definition — chart disabled, code-only mode
                    await _viewModel.LoadFunctionAsync(item.DatabaseName, item.SchemaName, item.Tag);
                }
                else if (item.NodeType == TreeNodeType.StoredProcedureFolder)
                {
                    // Clicking the folder: open a blank SP template in code-only mode
                    string db = item.DatabaseName;
                    _viewModel.SqlEditor.SqlText =
                        $"CREATE OR ALTER PROCEDURE [dbo].[NewProcedure]\r\n" +
                        $"    -- Add parameters here\r\n" +
                        $"    @Param1 INT = 0\r\n" +
                        $"AS\r\n" +
                        $"BEGIN\r\n" +
                        $"    SET NOCOUNT ON;\r\n\r\n" +
                        $"    -- TODO: Add procedure logic here\r\n" +
                        $"    SELECT 1;\r\n" +
                        $"END\r\n";
                    _viewModel.SqlEditor.IsChartDisabled = true;
                    _viewModel.SqlEditor.ViewMode = 1;
                    _viewModel.SqlEditor.IsVisible = true;
                    _viewModel.SqlEditor.LanguageMode = "T-SQL (Procedure)";
                    _viewModel.Canvas.IsVisible = false;
                    _viewModel.StatusMessage = "New stored procedure template ready. Edit and press F5 to execute.";
                }
            }
        }


        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (_viewModel != null && _viewModel.HasUnsavedChanges)
            {
                var result = MessageBox.Show(
                    "You have unsaved changes in your workbook. Do you want to save before closing?",
                    "Save Changes?",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    _viewModel.SaveWorkbookCommand.Execute(null);
                    // If they saved through the command, they might have cancelled the file dialog
                    if (_viewModel.HasUnsavedChanges) e.Cancel = true; 
                }
                else if (result == MessageBoxResult.Cancel)
                {
                    e.Cancel = true;
                }
            }
            base.OnClosing(e);
        }
    }
}