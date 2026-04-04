using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using sqlSense.Models;
using sqlSense.ViewModels;
using sqlSense.Views;

namespace sqlSense
{
    // ─── Node Graph Data Structures ───────────────────────────────────
    
    public class NodeCard
    {
        public string Id { get; set; } = "";
        public Border CardElement { get; set; } = null!;
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public Ellipse? OutputPort { get; set; }
        public Ellipse? InputPort { get; set; }
        public List<NodeConnection> OutputConnections { get; set; } = new();
        public List<NodeConnection> InputConnections { get; set; } = new();
        
        // Metadata for editing
        public ReferencedTable? SourceTable { get; set; }
        public bool IsViewNode { get; set; }
    }

    public class NodeConnection
    {
        public NodeCard Source { get; set; } = null!;
        public NodeCard Target { get; set; } = null!;
        public Path PathElement { get; set; } = null!;
        public Border? LabelBadge { get; set; }
        public JoinRelationship? JoinData { get; set; }
        public Color Color { get; set; }
    }

    // ─── Main Window ──────────────────────────────────────────────────

    public partial class MainWindow : Window
    {
        private MainViewModel? _viewModel;

        // Canvas pan state
        private bool _isPanning;
        private Point _panStart;
        private double _panStartX;
        private double _panStartY;

        // Node drag state
        private bool _isDraggingNode;
        private NodeCard? _draggedNode;
        private Point _dragStart;
        private double _dragNodeStartX;
        private double _dragNodeStartY;

        // View visualization
        private readonly List<UIElement> _viewVisualizationElements = new();
        private readonly List<NodeCard> _nodeCards = new();
        private readonly List<NodeConnection> _nodeConnections = new();

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

        private async void ObjectExplorer_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (_viewModel == null) return;
            if (e.NewValue is DatabaseTreeItem item)
            {
                if (item.NodeType == TreeNodeType.Table)
                {
                    ClearViewVisualization();
                    await _viewModel.LoadTableDataAsync(item.DatabaseName, item.SchemaName, item.Tag);
                    PositionTableCardAtViewCenter();
                }
                else if (item.NodeType == TreeNodeType.View)
                {
                    await _viewModel.LoadViewDefinitionAsync(item.DatabaseName, item.SchemaName, item.Tag);
                    if (_viewModel.CurrentViewDefinition != null)
                        RenderViewVisualization(_viewModel.CurrentViewDefinition);
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

        private void ClearViewVisualization()
        {
            foreach (var el in _viewVisualizationElements) FlowCanvas.Children.Remove(el);
            _viewVisualizationElements.Clear();
            _nodeCards.Clear();
            _nodeConnections.Clear();
        }

        private void RenderViewVisualization(ViewDefinitionInfo viewDef)
        {
            ClearViewVisualization();
            if (_viewModel == null || CanvasContainer == null) return;

            double zoom = _viewModel.CanvasZoom;
            double cx = (CanvasContainer.ActualWidth / 2 - CanvasTranslate.X) / zoom;
            double cy = (CanvasContainer.ActualHeight / 2 - CanvasTranslate.Y) / zoom;

            int tableCount = viewDef.ReferencedTables.Count;
            const double cardW = 260;
            const double hGap = 350;
            const double vGap = 30;

            int cols = Math.Min(2, Math.Max(1, tableCount));
            int rows = (int)Math.Ceiling((double)tableCount / cols);
            double gridStartX = cx - hGap / 2 - (cols * cardW + (cols-1)*40);
            double gridStartY = cy - (rows * 180) / 2;

            // ── 1. Create source table node cards ──
            var tableNodes = new Dictionary<string, NodeCard>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < tableCount; i++)
            {
                var refTbl = viewDef.ReferencedTables[i];
                double nx = gridStartX + (i % cols) * (cardW + 40);
                double ny = gridStartY + (i / cols) * 220;

                var node = CreateSourceTableNode(refTbl, cardW, nx, ny);
                tableNodes[refTbl.Alias] = node;
                if (!tableNodes.ContainsKey($"{refTbl.Schema}.{refTbl.Name}"))
                    tableNodes[$"{refTbl.Schema}.{refTbl.Name}"] = node;
            }

            // ── 2. Create view output node card ──
            double viewCardX = cx + hGap / 2;
            double viewCardY = cy - 150;
            var viewNode = CreateViewOutputNode(viewDef, cardW + 60, viewCardX, viewCardY);

            // ── 3. Create connections: source → view ──
            foreach (var refTbl in viewDef.ReferencedTables)
            {
                if (tableNodes.TryGetValue(refTbl.Alias, out var srcNode))
                {
                    CreateNodeConnection(srcNode, viewNode, null, Color.FromRgb(0xC5, 0x86, 0xC0));
                }
            }

            // ── 4. Create JOIN connections between source tables ──
            foreach (var join in viewDef.Joins)
            {
                NodeCard? leftNode = null, rightNode = null;
                tableNodes.TryGetValue(join.LeftTableAlias, out leftNode);
                tableNodes.TryGetValue(join.RightTableAlias, out rightNode);

                if (leftNode != null && rightNode != null)
                {
                    var joinColor = GetJoinColor(join.JoinType);
                    CreateNodeConnection(leftNode, rightNode, join, joinColor);
                }
            }

            // ── 5. Global Actions ──
            var saveBtn = CreateActionButton("\uE105 SYNC TO DATABASE", "#4CAF50", () => {
                _viewModel.SaveViewChangesCommand.Execute(null);
            });
            Canvas.SetLeft(saveBtn, cx - 100);
            Canvas.SetTop(saveBtn, cy + 300);
            FlowCanvas.Children.Add(saveBtn);
            _viewVisualizationElements.Add(saveBtn);

            AnimateAllElements();
        }

        private Color GetJoinColor(string type) => type.ToUpper() switch {
            "INNER" => Color.FromRgb(0x4C, 0xAF, 0x50),
            "LEFT" => Color.FromRgb(0xFF, 0xB7, 0x4D),
            "RIGHT" => Color.FromRgb(0xFF, 0xB7, 0x4D),
            "FULL" => Color.FromRgb(0xF4, 0x43, 0x36),
            _ => Color.FromRgb(0x4C, 0xAF, 0x50)
        };

        // ─── Source Table Node ─────────────────────────────────────────

        private NodeCard CreateSourceTableNode(ReferencedTable refTbl, double width, double x, double y)
        {
            var header = new Border {
                Background = new LinearGradientBrush(Color.FromRgb(0x4F, 0xC3, 0xF7), Color.FromRgb(0x03, 0x9B, 0xE5), 0),
                CornerRadius = new CornerRadius(10, 10, 0, 0),
                Padding = new Thickness(14, 8, 14, 8),
                Child = new DockPanel {
                    Children = {
                        new Button { 
                            Content = "\uE711", FontFamily = new FontFamily("Segoe MDL2 Assets"),
                            Background = Brushes.Transparent, BorderThickness = new Thickness(0),
                            Foreground = new SolidColorBrush(Color.FromArgb(0x88, 0x1E, 0x1E, 0x1E)),
                            VerticalAlignment = VerticalAlignment.Center, DockPanel.SetDock(Dock.Right),
                            Cursor = Cursors.Hand
                        },
                        new TextBlock { Text = "\uE80A ", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 12, VerticalAlignment = VerticalAlignment.Center },
                        new TextBlock { Text = refTbl.DisplayName, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center }
                    }
                }
            };

            var colPanel = new StackPanel { Margin = new Thickness(0, 5, 0, 5) };
            
            // All available columns
            var allCols = _viewModel!.CurrentViewDefinition!.SourceTableAllColumns.GetValueOrDefault(refTbl.FullName, new List<string>());
            foreach (var col in allCols)
            {
                bool isSelected = refTbl.UsedColumns.Contains(col);
                var row = new CheckBox {
                    Content = col, IsChecked = isSelected,
                    Foreground = Brushes.White, FontSize = 10.5, FontFamily = new FontFamily("Consolas"),
                    Margin = new Thickness(12, 2, 12, 2),
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Style = null // Use default for simplicity
                };
                
                row.Click += (s, e) => {
                    if (row.IsChecked == true && !refTbl.UsedColumns.Contains(col)) {
                        refTbl.UsedColumns.Add(col);
                        _viewModel.CurrentViewDefinition.Columns.Add(new ViewColumnInfo { 
                            SourceTable = refTbl.Alias, SourceColumn = col, ColumnName = col });
                    } else if (row.IsChecked == false) {
                        refTbl.UsedColumns.Remove(col);
                        _viewModel.CurrentViewDefinition.Columns.RemoveAll(c => c.SourceTable == refTbl.Alias && c.SourceColumn == col);
                    }
                    RenderViewVisualization(_viewModel.CurrentViewDefinition);
                };
                colPanel.Children.Add(row);
            }

            var card = new Border {
                Background = new SolidColorBrush(Color.FromRgb(0x1F, 0x1F, 0x22)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x4F, 0xC3, 0xF7)),
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(10),
                MinWidth = width, Effect = new DropShadowEffect { BlurRadius = 20, Opacity = 0.5 },
                Child = new StackPanel { Children = { header, colPanel } }
            };

            card.Measure(new Size(width + 20, 1000));
            double h = card.DesiredSize.Height;
            Canvas.SetLeft(card, x); Canvas.SetTop(card, y);
            FlowCanvas.Children.Add(card); _viewVisualizationElements.Add(card);

            var node = new NodeCard { Id = refTbl.Alias, CardElement = card, X = x, Y = y, Width = width, Height = h, SourceTable = refTbl };
            _nodeCards.Add(node); SetupNodeDrag(card, node);
            return node;
        }

        // ─── View Output Node ─────────────────────────────────────────

        private NodeCard CreateViewOutputNode(ViewDefinitionInfo viewDef, double width, double x, double y)
        {
            var header = new Border {
                Background = new LinearGradientBrush(Color.FromRgb(0xC5, 0x86, 0xC0), Color.FromRgb(0x9C, 0x27, 0xB0), 0),
                CornerRadius = new CornerRadius(10, 10, 0, 0),
                Padding = new Thickness(14, 8, 14, 8),
                Child = new TextBlock { Text = "\uE7B3  " + viewDef.ViewName, Foreground = Brushes.White, FontWeight = FontWeights.Bold }
            };

            var colPanel = new StackPanel { Margin = new Thickness(0, 5, 0, 5) };
            foreach (var col in viewDef.Columns)
            {
                var row = new Grid { Margin = new Thickness(12, 1, 12, 1) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                
                var icon = new TextBlock { Text = "\uE71A ", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 9, Foreground = Brushes.Cyan, VerticalAlignment = VerticalAlignment.Center };
                var nameBox = new TextBox { 
                    Text = col.Alias ?? col.ColumnName, Background = Brushes.Transparent, 
                    BorderThickness = new Thickness(0,0,0,1), BorderBrush = new SolidColorBrush(Color.FromArgb(0x40, 255,255,255)),
                    Foreground = Brushes.White, FontSize = 11, FontFamily = new FontFamily("Consolas"),
                    Padding = new Thickness(0)
                };
                nameBox.LostFocus += (s, e) => col.Alias = nameBox.Text;
                
                Grid.SetColumn(icon, 0); Grid.SetColumn(nameBox, 1);
                row.Children.Add(icon); row.Children.Add(nameBox);
                colPanel.Children.Add(row);
            }

            var card = new Border {
                Background = new SolidColorBrush(Color.FromRgb(0x1F, 0x1F, 0x22)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xC5, 0x86, 0xC0)),
                BorderThickness = new Thickness(2), CornerRadius = new CornerRadius(10),
                MinWidth = width, Effect = new DropShadowEffect { Color = Color.FromRgb(0xC5, 0x86, 0xC0), BlurRadius = 30, Opacity = 0.2 },
                Child = new StackPanel { Children = { header, colPanel } }
            };

            card.Measure(new Size(width + 20, 1200));
            double h = card.DesiredSize.Height;
            Canvas.SetLeft(card, x); Canvas.SetTop(card, y);
            FlowCanvas.Children.Add(card); _viewVisualizationElements.Add(card);

            var node = new NodeCard { Id = "VIEW", CardElement = card, X = x, Y = y, Width = width, Height = h, IsViewNode = true };
            _nodeCards.Add(node); SetupNodeDrag(card, node);
            return node;
        }

        private void CreateNodeConnection(NodeCard src, NodeCard tgt, JoinRelationship? join, Color color)
        {
            var p = new Path { Stroke = new SolidColorBrush(color), StrokeThickness = 2.5, Opacity = 0.8, IsHitTestVisible = false };
            FlowCanvas.Children.Add(p); _viewVisualizationElements.Add(p);

            Border? badge = null;
            if (join != null) {
                badge = MakeJoinBadgeInteractive(join, color);
                FlowCanvas.Children.Add(badge); _viewVisualizationElements.Add(badge);
            }

            var conn = new NodeConnection { Source = src, Target = tgt, PathElement = p, LabelBadge = badge, JoinData = join, Color = color };
            src.OutputConnections.Add(conn); tgt.InputConnections.Add(conn); _nodeConnections.Add(conn);
            UpdateConnectionPath(conn);
        }

        private Border MakeJoinBadgeInteractive(JoinRelationship join, Color color)
        {
            var btn = new Button {
                Content = join.JoinType, FontSize = 9, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(color),
                Background = Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(5,2,5,2)
            };
            btn.Click += (s, e) => {
                join.JoinType = join.JoinType.ToUpper() switch { "INNER" => "LEFT", "LEFT" => "RIGHT", "RIGHT" => "FULL", _ => "INNER" };
                RenderViewVisualization(_viewModel!.CurrentViewDefinition!);
            };

            return new Border {
                Background = new SolidColorBrush(Color.FromArgb(0xF0, 0x1E, 0x1E, 0x22)),
                BorderBrush = new SolidColorBrush(color), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4),
                Child = new StackPanel { Children = { 
                    btn, 
                    new TextBlock { Text = $"{join.LeftColumn}={join.RightColumn}", FontSize = 8, Foreground = Brushes.Gray, Margin = new Thickness(5,0,5,3) } 
                } }
            };
        }

        private Border CreateActionButton(string text, string colorHex, Action action)
        {
            var color = (Color)ColorConverter.ConvertFromString(colorHex);
            var btn = new Button {
                Content = text, Background = new SolidColorBrush(color), Foreground = Brushes.White,
                FontWeight = FontWeights.Bold, Padding = new Thickness(20,10,20,10), 
                BorderThickness = new Thickness(0), Cursor = Cursors.Hand, FontSize = 12
            };
            btn.Click += (s, e) => action();
            return new Border { CornerRadius = new CornerRadius(6), Child = btn, ClipToBounds = true, Effect = new DropShadowEffect { Color = color, BlurRadius = 15, Opacity = 0.5 } };
        }

        // ─── Core Path Logic ─────────────────────────────────────────

        private void UpdateConnectionPath(NodeConnection conn)
        {
            double sx = conn.Source.X + conn.Source.Width;
            double sy = conn.Source.Y + conn.Source.Height/2;
            double tx = conn.Target.X;
            double ty = conn.Target.Y + conn.Target.Height/2;

            double dist = Math.Abs(tx - sx);
            var p0 = new Point(sx, sy);
            var p3 = new Point(tx, ty);
            var p1 = new Point(sx + dist * 0.5, sy);
            var p2 = new Point(tx - dist * 0.5, ty);

            var fig = new PathFigure { StartPoint = p0, IsClosed = false };
            fig.Segments.Add(new BezierSegment(p1, p2, p3, true));
            conn.PathElement.Data = new PathGeometry(new[] { fig });

            if (conn.LabelBadge != null) {
                conn.LabelBadge.Measure(new Size(1000,1000));
                Canvas.SetLeft(conn.LabelBadge, (sx+tx)/2 - conn.LabelBadge.DesiredSize.Width/2);
                Canvas.SetTop(conn.LabelBadge, (sy+ty)/2 - conn.LabelBadge.DesiredSize.Height/2);
            }
        }

        private void SetupNodeDrag(Border card, NodeCard node)
        {
            card.MouseLeftButtonDown += (s, e) => {
                _isDraggingNode = true; _draggedNode = node;
                _dragStart = e.GetPosition(FlowCanvas); _dragNodeStartX = node.X; _dragNodeStartY = node.Y;
                card.CaptureMouse(); e.Handled = true;
            };
            card.MouseMove += (s, e) => {
                if (!_isDraggingNode || _draggedNode != node) return;
                var cur = e.GetPosition(FlowCanvas);
                node.X = _dragNodeStartX + (cur.X - _dragStart.X);
                node.Y = _dragNodeStartY + (cur.Y - _dragStart.Y);
                Canvas.SetLeft(card, node.X); Canvas.SetTop(card, node.Y);
                foreach (var c in node.OutputConnections) UpdateConnectionPath(c);
                foreach (var c in node.InputConnections) UpdateConnectionPath(c);
            };
            card.MouseLeftButtonUp += (s, e) => {
                if (_isDraggingNode) { _isDraggingNode = false; card.ReleaseMouseCapture(); }
            };
        }

        private void AnimateAllElements()
        {
            foreach (var el in _viewVisualizationElements) {
                if (el is FrameworkElement fe) {
                    fe.Opacity = 0;
                    fe.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(400)));
                }
            }
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
            if (_isDraggingNode) return;
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