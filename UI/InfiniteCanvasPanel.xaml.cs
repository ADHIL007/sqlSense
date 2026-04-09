using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using sqlSense.ViewModels;

namespace sqlSense.UI
{
    public partial class InfiniteCanvasPanel : UserControl
    {
        private bool _isPanning;
        private Point _panStart;
        private double _panStartX;
        private double _panStartY;

        public ViewGraphRenderer? GraphRenderer { get; set; }

        public MainViewModel? ViewModel => DataContext as MainViewModel;

        // UI Accessors that ViewGraphRenderer uses
        public Canvas CanvasElement => FlowCanvas;
        public Border Container => CanvasContainer;
        public TranslateTransform Translate => CanvasTranslate;

        public InfiniteCanvasPanel()
        {
            InitializeComponent();
        }

        public void CenterCanvas()
        {
            if (CanvasContainer != null && ViewModel != null)
            {
                double w = CanvasContainer.ActualWidth;
                double h = CanvasContainer.ActualHeight;
                CanvasTranslate.X = -(5000 * ViewModel.Canvas.Zoom) + (w / 2);
                CanvasTranslate.Y = -(5000 * ViewModel.Canvas.Zoom) + (h / 2);
            }
        }



        // ═══════════════════════════════════════════════════════════════
        //  TOOLBAR EVENTS
        // ═══════════════════════════════════════════════════════════════

        private void ErdToolbar_ShapeRequested(string shapeType)
        {
            switch (shapeType)
            {
                case "ToggleMode":
                    GraphRenderer?.ToggleDataFlowState();
                    break;
                case "CreateTable":
                    GraphRenderer?.ShowCreateTableOnCanvas();
                    break;
                case "Table":
                    GraphRenderer?.AddTableCardAtCenter();
                    break;
                case "View":
                    ViewModel!.StatusMessage = "Select a View from the Object Explorer to visualize it.";
                    break;
                case "Arrange":
                    GraphRenderer?.AutoArrange();
                    break;
                case "Variable":
                case "IfElse":
                case "While":
                case "Execute":
                case "TryCatch":
                case "Comment":
                case "Text":
                    ViewModel!.StatusMessage = $"{shapeType} shape — coming soon.";
                    break;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  CANVAS NAVIGATION
        // ═══════════════════════════════════════════════════════════════

        private void Canvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (ViewModel == null) return;
            Point mousePos = e.GetPosition(CanvasContainer);
            double oldZoom = ViewModel.Canvas.Zoom;
            double delta = e.Delta > 0 ? 0.1 : -0.1;
            double newZoom = Math.Clamp(oldZoom + delta, 0.1, 5.0);
            double sc = newZoom / oldZoom;
            CanvasTranslate.X = mousePos.X - (mousePos.X - CanvasTranslate.X) * sc;
            CanvasTranslate.Y = mousePos.Y - (mousePos.Y - CanvasTranslate.Y) * sc;
            ViewModel.Canvas.SetZoom(newZoom); 
            UpdateCoordinates(mousePos);
        }

        private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (GraphRenderer?.IsDraggingNode == true) return;
            StartPan(e.GetPosition(CanvasContainer));
            CanvasContainer.Cursor = Cursors.SizeAll;
        }

        private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            Point currentPos = e.GetPosition(CanvasContainer);
            double dist = Math.Sqrt(Math.Pow(currentPos.X - _panStart.X, 2) + Math.Pow(currentPos.Y - _panStart.Y, 2));
            
            StopPan(); 
            CanvasContainer.Cursor = Cursors.Arrow;

            // If it was a quick click (not a drag or pan), show the context menu
            if (dist < 3)
            {
                if (CanvasContainer.ContextMenu != null)
                {
                    CanvasContainer.ContextMenu.PlacementTarget = CanvasContainer;
                    CanvasContainer.ContextMenu.IsOpen = true;
                }
            }
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
            StopPan(); 
            CanvasContainer.Cursor = Cursors.Arrow;
        }

        private void StartPan(Point pos) 
        {
            _isPanning = true; 
            _panStart = pos; 
            _panStartX = CanvasTranslate.X; 
            _panStartY = CanvasTranslate.Y;
            CanvasContainer.CaptureMouse();
        }

        private void StopPan() 
        { 
            _isPanning = false; 
            CanvasContainer.ReleaseMouseCapture(); 
        }

        private void ContextMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item && item.Tag is string shapeType)
            {
                ErdToolbar_ShapeRequested(shapeType);
            }
        }

        private void UpdateCoordinates(Point screenPos) 
        {
            if (ViewModel == null || CoordinateLabel == null) return;
            double z = ViewModel.Canvas.Zoom;
            double cx = (screenPos.X - CanvasTranslate.X) / z - 5000;
            double cy = (screenPos.Y - CanvasTranslate.Y) / z - 5000;
            CoordinateLabel.Text = $"{(int)cx}, {(int)cy}  |  {ViewModel.Canvas.ZoomPercentage}";
        }
    }
}
