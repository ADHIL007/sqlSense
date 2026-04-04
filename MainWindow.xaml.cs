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

        // Canvas pan state
        private bool _isPanning;
        private Point _panStart;
        private double _panStartX;
        private double _panStartY;

        public MainWindow()
        {
            var connectWindow = new ConnectWindow();
            if (connectWindow.ShowDialog() == true)
            {
                InitializeComponent();
                _viewModel = new MainViewModel();
                _viewModel.ConnectionString = connectWindow.ConnectionString;
                _viewModel.ServerName = connectWindow.ServerTextBox.Text;
                _viewModel.StatusMessage = $"Connected to {connectWindow.ServerTextBox.Text}";
                DataContext = _viewModel;

                Loaded += async (_, _) =>
                {
                    _graphRenderer = new ViewGraphRenderer(FlowCanvas, CanvasContainer, CanvasTranslate, _viewModel);
                    await _viewModel.LoadDatabaseTreeAsync();
                    CenterCanvas();
                };
            }
            else
            {
                Application.Current.Shutdown();
            }
        }

        private void CenterCanvas()
        {
            if (CanvasContainer != null)
            {
                double w = CanvasContainer.ActualWidth;
                double h = CanvasContainer.ActualHeight;
                CanvasTranslate.X = -(5000 * _viewModel!.CanvasZoom) + (w / 2);
                CanvasTranslate.Y = -(5000 * _viewModel!.CanvasZoom) + (h / 2);
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

        private void TreeViewItem_RequestBringIntoView(object sender, RequestBringIntoViewEventArgs e)
        {
            // Prevent auto-scrolling when items are selected or expanded
            e.Handled = true;
        }

        private async void ObjectExplorer_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (_viewModel == null) return;
            if (e.NewValue is DatabaseTreeItem item)
            {
                if (item.NodeType == TreeNodeType.Table)
                {
                    _graphRenderer?.ClearViewVisualization();
                    await _viewModel.LoadTableDataAsync(item.DatabaseName, item.SchemaName, item.Tag);
                    PositionTableCardAtViewCenter();
                }
                else if (item.NodeType == TreeNodeType.View)
                {
                    await _viewModel.LoadViewDefinitionAsync(item.DatabaseName, item.SchemaName, item.Tag);
                    if (_viewModel.CurrentViewDefinition != null)
                        _graphRenderer?.RenderViewVisualization(_viewModel.CurrentViewDefinition);
                }
            }
        }

        private void PositionTableCardAtViewCenter()
        {
            if (CanvasContainer == null || TableDataCard == null || _viewModel == null) return;
            double zoom = _viewModel.CanvasZoom;
            double cx = (CanvasContainer.ActualWidth / 2 - CanvasTranslate.X) / zoom;
            double cy = (CanvasContainer.ActualHeight / 2 - CanvasTranslate.Y) / zoom;
            Canvas.SetLeft(TableDataCard, cx - 200);
            Canvas.SetTop(TableDataCard, cy - 100);
        }

        // ═══════════════════════════════════════════════════════════════
        //  VIEW VISUALIZATION — INTERACTIVE NODE GRAPH
        // ═══════════════════════════════════════════════════════════════

        private void DataFlowToggleBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_graphRenderer == null) return;
            _graphRenderer.ToggleDataFlowState();
            DataFlowToggleBtn.Content = _graphRenderer.IsGlobalDataFlowEnabled ? "\uE14F DATA FLOW: ON" : "\uE14F DATA FLOW: OFF";
        }

        // ═══════════════════════════════════════════════════════════════
        //  CANVAS PAN & ZOOM
        // ═══════════════════════════════════════════════════════════════

        private void Canvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (_viewModel == null) return;
            Point mousePos = e.GetPosition(CanvasContainer);
            double oldZoom = _viewModel.CanvasZoom;
            double delta = e.Delta > 0 ? 0.1 : -0.1;
            double newZoom = Math.Clamp(oldZoom + delta, 0.1, 5.0);
            double sc = newZoom / oldZoom;
            CanvasTranslate.X = mousePos.X - (mousePos.X - CanvasTranslate.X) * sc;
            CanvasTranslate.Y = mousePos.Y - (mousePos.Y - CanvasTranslate.Y) * sc;
            _viewModel.SetZoom(newZoom); UpdateCoordinates(mousePos);
        }

        private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_graphRenderer?.IsDraggingNode == true) return;
            StartPan(e.GetPosition(CanvasContainer));
            CanvasContainer.Cursor = Cursors.SizeAll;
        }

        private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            StopPan(); CanvasContainer.Cursor = Cursors.Arrow;
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            Point current = e.GetPosition(CanvasContainer);
            if (_isPanning) {
                CanvasTranslate.X = _panStartX + (current.X - _panStart.X);
                CanvasTranslate.Y = _panStartY + (current.Y - _panStart.Y);
            }
            UpdateCoordinates(current);
        }

        private void Canvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            StartPan(e.GetPosition(CanvasContainer));
            CanvasContainer.Cursor = Cursors.SizeAll;
        }

        private void Canvas_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            StopPan(); CanvasContainer.Cursor = Cursors.Arrow;
        }

        private void StartPan(Point pos) {
            _isPanning = true; _panStart = pos; _panStartX = CanvasTranslate.X; _panStartY = CanvasTranslate.Y;
            CanvasContainer.CaptureMouse();
        }

        private void StopPan() { _isPanning = false; CanvasContainer.ReleaseMouseCapture(); }

        private void UpdateCoordinates(Point screenPos) {
            if (_viewModel == null || CoordinateLabel == null) return;
            double z = _viewModel.CanvasZoom;
            double cx = (screenPos.X - CanvasTranslate.X) / z - 5000;
            double cy = (screenPos.Y - CanvasTranslate.Y) / z - 5000;
            CoordinateLabel.Text = $"{(int)cx}, {(int)cy}  |  {_viewModel.ZoomPercentage}";
        }
    }
}