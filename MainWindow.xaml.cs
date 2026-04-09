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

                    await _viewModel.LoadDatabaseTreeAsync();
                    
                    // Startup State: Auto-initialize as New Workspace
                    _viewModel.NewWorkspaceCommand.Execute(null);
                    CanvasPanel.CenterCanvas();
                };
            }
            else
            {
                Application.Current.Shutdown();
            }
        }

        private void GlobalAddTable_Click(object sender, RoutedEventArgs e)
        {
            _graphRenderer?.AddTableCardAtCenter();
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
                    _graphRenderer?.ClearViewVisualization();
                    await _viewModel.LoadTableDataAsync(item.DatabaseName, item.SchemaName, item.Tag);
                    CanvasPanel.PositionTableCardAtViewCenter();
                }
                else if (item.NodeType == TreeNodeType.View)
                {
                    await _viewModel.LoadViewDefinitionAsync(item.DatabaseName, item.SchemaName, item.Tag);
                    if (_viewModel.CurrentViewDefinition != null)
                        _graphRenderer?.RenderViewVisualization(_viewModel.CurrentViewDefinition);
                }
            }
        }


    }
}