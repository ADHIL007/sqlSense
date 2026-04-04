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

namespace sqlSense.UI
{
    public class ViewGraphRenderer
    {
        private readonly Canvas _flowCanvas;
        private readonly FrameworkElement _canvasContainer;
        private readonly TranslateTransform _canvasTranslate;
        private readonly MainViewModel _viewModel;

        // Node drag state
        public bool IsDraggingNode { get; private set; }
        private NodeCard? _draggedNode;
        private Point _dragStart;
        private double _dragNodeStartX;
        private double _dragNodeStartY;
        
        // Data flow state
        public bool IsGlobalDataFlowEnabled { get; private set; } = false;
        private NodeCard? _hoveredNode = null;

        // View visualization
        private readonly List<UIElement> _viewVisualizationElements = new();
        private readonly List<NodeCard> _nodeCards = new();
        private readonly List<NodeConnection> _nodeConnections = new();
        private readonly NodeDataPreviewManager _previewManager;

        public ViewGraphRenderer(Canvas flowCanvas, FrameworkElement canvasContainer, TranslateTransform canvasTranslate, MainViewModel viewModel)
        {
            _flowCanvas = flowCanvas;
            _canvasContainer = canvasContainer;
            _canvasTranslate = canvasTranslate;
            _viewModel = viewModel;
            _previewManager = new NodeDataPreviewManager(flowCanvas);

            // Click outside to close preview
            _flowCanvas.PreviewMouseDown += (s, e) => {
                if (_previewManager.IsVisible && !_previewManager.IsOwnedElement(e.OriginalSource as DependencyObject))
                {
                    _previewManager.Hide();
                }
            };
        }

        public void ClearViewVisualization()
        {
            foreach (var el in _viewVisualizationElements) _flowCanvas.Children.Remove(el);
            _viewVisualizationElements.Clear();
            _nodeCards.Clear();
            _nodeConnections.Clear();
        }

        public void RenderViewVisualization(ViewDefinitionInfo viewDef)
        {
            ClearViewVisualization();

            double zoom = _viewModel.CanvasZoom;
            double cx = (_canvasContainer.ActualWidth / 2 - _canvasTranslate.X) / zoom;
            double cy = (_canvasContainer.ActualHeight / 2 - _canvasTranslate.Y) / zoom;

            int tableCount = viewDef.ReferencedTables.Count;
            const double cardW = 280;
            const double hGap = 450; 
            
            double gridStartX = cx - hGap; 
            double currentY = cy - (tableCount * 300) / 2; // Approximate starting Y

            // ── 1. Create source table node cards ──
            var tableNodes = new Dictionary<string, NodeCard>(StringComparer.OrdinalIgnoreCase);

            // Modern Palette for source tables
            var palette = new string[] { "#8E44AD", "#27AE60", "#D35400", "#C0392B", "#16A085", "#2980B9", "#F39C12", "#E91E63", "#673AB7" };

            for (int i = 0; i < tableCount; i++)
            {
                var refTbl = viewDef.ReferencedTables[i];
                string colorStr = palette[Math.Abs(refTbl.Alias.GetHashCode()) % palette.Length];
                var node = CreateSourceTableNode(refTbl, cardW, gridStartX, currentY, colorStr);
                node.ParticipatingTables.Add(refTbl.Alias);
                node.Color = colorStr;

                currentY += node.Height + 50; 

                tableNodes[refTbl.Alias] = node;
                if (!tableNodes.ContainsKey($"{refTbl.Schema}.{refTbl.Name}"))
                    tableNodes[$"{refTbl.Schema}.{refTbl.Name}"] = node;
            }

            var activeOutputNode = new Dictionary<string, NodeCard>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in tableNodes) activeOutputNode[kvp.Key] = kvp.Value;

            // ── 2. Create Join Nodes hierarchically ──
            int maxJoinLevel = 0;
            foreach (var join in viewDef.Joins)
            {
                NodeCard? leftNode = null, rightNode = null;
                activeOutputNode.TryGetValue(join.LeftTableAlias, out leftNode);
                activeOutputNode.TryGetValue(join.RightTableAlias, out rightNode);

                if (leftNode != null && rightNode != null)
                {
                    int newLvl = Math.Max(leftNode.LayoutLevel, rightNode.LayoutLevel) + 1;
                    maxJoinLevel = Math.Max(maxJoinLevel, newLvl);

                    double maxRight = Math.Max(leftNode.X + leftNode.Width, rightNode.X + rightNode.Width);
                    double jx = maxRight + 150;
                    double jy = (leftNode.Y + rightNode.Y) / 2;

                    // Get columns flowing through this join
                    var participating = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    participating.UnionWith(leftNode.ParticipatingTables);
                    participating.UnionWith(rightNode.ParticipatingTables);

                    var upstreamCols = viewDef.Columns
                        .Where(c => participating.Contains(c.SourceTable))
                        .Select(c => $"{c.SourceTable}.{c.SourceColumn}")
                        .ToList();

                    var joinCard = CreateJoinNode(join, 220, jx, jy, upstreamCols);
                    joinCard.LayoutLevel = newLvl;
                    joinCard.ParticipatingTables = participating;
                    joinCard.JoinData = join;

                    var joinColor = GetJoinColor(join.JoinType);
                    CreateNodeConnection(leftNode, joinCard, null, joinColor);
                    CreateNodeConnection(rightNode, joinCard, null, joinColor);

                    // Re-route active outputs for any alias currently pointing to left or right node
                    var keysToUpdate = activeOutputNode.Where(k => k.Value == leftNode || k.Value == rightNode).Select(k => k.Key).ToList();
                    foreach (var k in keysToUpdate) activeOutputNode[k] = joinCard;
                }
            }

            // ── 3. Create view output node card ──
            double maxOutputX = gridStartX;
            foreach (var n in activeOutputNode.Values) {
                if (n.X + n.Width > maxOutputX) maxOutputX = n.X + n.Width;
            }
            double viewCardX = maxOutputX + 150;
            double viewCardY = cy - 150;
            var viewNode = CreateViewOutputNode(viewDef, cardW + 60, viewCardX, viewCardY);

            // Connect final roots to view node
            foreach (var finalNode in activeOutputNode.Values.Distinct())
            {
                CreateNodeConnection(finalNode, viewNode, null, Color.FromRgb(0xC5, 0x86, 0xC0));
            }

            AnimateAllElements();
        }

        public void ToggleDataFlowState()
        {
            IsGlobalDataFlowEnabled = !IsGlobalDataFlowEnabled;
            UpdateAllFlowAnimations();
        }

        private Color GetJoinColor(string type) => type.ToUpper() switch {
            "INNER" => Color.FromRgb(0x4C, 0xAF, 0x50),
            "LEFT" => Color.FromRgb(0xFF, 0xB7, 0x4D),
            "RIGHT" => Color.FromRgb(0xFF, 0xB7, 0x4D),
            "FULL" => Color.FromRgb(0xF4, 0x43, 0x36),
            _ => Color.FromRgb(0x4C, 0xAF, 0x50)
        };

        private NodeCard CreateJoinNode(JoinRelationship join, double width, double x, double y, List<string> upstreamCols)
        {
            var color = GetJoinColor(join.JoinType);
            
            var header = new Border {
                Background = new SolidColorBrush(Color.FromArgb(0x40, color.R, color.G, color.B)),
                BorderBrush = new SolidColorBrush(color),
                BorderThickness = new Thickness(0,0,0,1),
                Padding = new Thickness(10, 6, 10, 6),
                CornerRadius = new CornerRadius(8, 8, 0, 0),
                Child = new TextBlock { Text = $"{join.JoinType} JOIN", Foreground = new SolidColorBrush(color), FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center }
            };

            var body = new StackPanel { Margin = new Thickness(10) };
            body.Children.Add(new TextBlock { Text = $"{join.LeftTableAlias}.{join.LeftColumn}", Foreground = Brushes.LightGray, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center });
            body.Children.Add(new TextBlock { Text = "=", Foreground = Brushes.Gray, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0,2,0,2) });
            body.Children.Add(new TextBlock { Text = $"{join.RightTableAlias}.{join.RightColumn}", Foreground = Brushes.LightGray, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center });

            // Interactive type changer
            var changeBtn = new Button {
                Content = "TAP TO CHANGE", FontSize = 9, Foreground = Brushes.Gray,
                Background = Brushes.Transparent, BorderThickness = new Thickness(0),
                Margin = new Thickness(0, 10, 0, 10), Cursor = Cursors.Hand
            };
            changeBtn.Click += (s, e) => {
                join.JoinType = join.JoinType.ToUpper() switch { "INNER" => "LEFT", "LEFT" => "RIGHT", "RIGHT" => "FULL", _ => "INNER" };
                RenderViewVisualization(_viewModel!.CurrentViewDefinition!);
            };
            body.Children.Add(changeBtn);

            // Flowing columns box
            if (upstreamCols.Any())
            {
                var flowBorder = new Border { Background = new SolidColorBrush(Color.FromArgb(0x10, 255, 255, 255)), Margin = new Thickness(-10, 10, -10, -10), CornerRadius = new CornerRadius(0,0,8,8) };
                var flowPanel = new StackPanel();
                flowPanel.Children.Add(new TextBlock { Text = "SELECTED FIELDS", FontSize = 9, Foreground = Brushes.Gray, Margin = new Thickness(12,10,12,5), FontWeight = FontWeights.Bold });
                foreach (var c in upstreamCols)
                {
                   var rowPanel = new Border {
                       Background = new SolidColorBrush(Color.FromArgb(0x20, 0x03, 0x9B, 0xE5)),
                       BorderThickness = new Thickness(3,0,0,1), 
                       BorderBrush = Brushes.DeepSkyBlue,
                       Padding = new Thickness(12, 6, 12, 6),
                       Child = new TextBlock { Text = c, Foreground = Brushes.White, FontSize = 11, FontFamily = new FontFamily("Consolas"), HorizontalAlignment = HorizontalAlignment.Left }
                   };
                   flowPanel.Children.Add(rowPanel);
                }
                flowBorder.Child = flowPanel;
                body.Children.Add(flowBorder);
            }

            var card = new Border {
                Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x22)),
                BorderBrush = new SolidColorBrush(color),
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(8),
                MinWidth = width, Effect = new DropShadowEffect { BlurRadius = 15, Opacity = 0.3, Color = color },
                Child = new StackPanel { Children = { header, body } }
            };

            card.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double h = card.DesiredSize.Height;
            double w = Math.Max(width, card.DesiredSize.Width);
            Canvas.SetLeft(card, x); Canvas.SetTop(card, y);
            _flowCanvas.Children.Add(card); _viewVisualizationElements.Add(card);

            var node = new NodeCard { Id = Guid.NewGuid().ToString(), CardElement = card, X = x, Y = y, Width = w, Height = h };
            _nodeCards.Add(node); SetupNodeDrag(card, node);
            
            card.LayoutUpdated += (s, e) => {
                node.Width = card.ActualWidth > 0 ? card.ActualWidth : node.Width;
                node.Height = card.ActualHeight > 0 ? card.ActualHeight : node.Height;
                foreach (var c in node.OutputConnections) UpdateConnectionPath(c);
                foreach (var c in node.InputConnections) UpdateConnectionPath(c);
            };

            return node;
        }

        private NodeCard CreateSourceTableNode(ReferencedTable refTbl, double width, double x, double y, string colorStr)
        {
            var color = (Color)ColorConverter.ConvertFromString(colorStr);
            var lightColor = Color.FromArgb(0x40, color.R, color.G, color.B);

            // Build header with delete button
            var deleteBtn = new Button {
                Content = "\uE711", FontFamily = new FontFamily("Segoe MDL2 Assets"),
                Background = Brushes.Transparent, BorderThickness = new Thickness(0),
                Foreground = new SolidColorBrush(Color.FromArgb(0x88, 0x1E, 0x1E, 0x1E)),
                VerticalAlignment = VerticalAlignment.Center, Cursor = Cursors.Hand
            };
            deleteBtn.Click += (s, e) => {
                _viewModel!.CurrentViewDefinition!.ReferencedTables.Remove(refTbl);
                _viewModel.CurrentViewDefinition.Columns.RemoveAll(c => c.SourceTable == refTbl.Alias);
                RenderViewVisualization(_viewModel.CurrentViewDefinition);
            };

            var headerPanel = new DockPanel();
            DockPanel.SetDock(deleteBtn, Dock.Right);
            headerPanel.Children.Add(deleteBtn);
            headerPanel.Children.Add(new TextBlock { Text = "\uE80A ", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
            headerPanel.Children.Add(new TextBlock { Text = refTbl.DisplayName, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center });

            var header = new Border {
                Background = new SolidColorBrush(color),
                CornerRadius = new CornerRadius(10, 10, 0, 0),
                Padding = new Thickness(14, 8, 14, 8),
                Child = headerPanel
            };

            var colPanel = new StackPanel { Margin = new Thickness(0, 5, 0, 5) };
            
            // All available columns
            var allCols = _viewModel!.CurrentViewDefinition!.SourceTableAllColumns.GetValueOrDefault(refTbl.FullName, new List<string>());
            foreach (var col in allCols)
            {
                bool isSelected = refTbl.UsedColumns.Contains(col);
                
                var rowBtn = new Button { Background = Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(0), Cursor = Cursors.Hand, HorizontalContentAlignment = HorizontalAlignment.Stretch };
                var rowPanel = new Border {
                    Background = isSelected ? lightColor : Brushes.Transparent,
                    BorderThickness = isSelected ? new Thickness(3,0,0,1) : new Thickness(0,0,0,1), 
                    BorderBrush = isSelected ? new SolidColorBrush(color) : new SolidColorBrush(Color.FromArgb(0x20, 255,255,255)),
                    Margin = new Thickness(0), Padding = new Thickness(12, 6, 12, 6),
                    Child = new TextBlock { Text = col, Foreground = isSelected ? Brushes.White : Brushes.Gray, FontSize = 11, FontFamily = new FontFamily("Consolas"), HorizontalAlignment = HorizontalAlignment.Left }
                };
                rowBtn.Content = rowPanel;

                rowBtn.Click += (s, e) => {
                    if (!refTbl.UsedColumns.Contains(col)) {
                        refTbl.UsedColumns.Add(col);
                        _viewModel.CurrentViewDefinition.Columns.Add(new ViewColumnInfo { 
                            SourceTable = refTbl.Alias, SourceColumn = col, ColumnName = col });
                    } else {
                        refTbl.UsedColumns.Remove(col);
                        _viewModel.CurrentViewDefinition.Columns.RemoveAll(c => c.SourceTable == refTbl.Alias && c.SourceColumn == col);
                    }
                    RenderViewVisualization(_viewModel.CurrentViewDefinition);
                };
                colPanel.Children.Add(rowBtn);
            }

            var card = new Border {
                Background = new SolidColorBrush(Color.FromRgb(0x1F, 0x1F, 0x22)),
                BorderBrush = new SolidColorBrush(color),
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(10),
                MinWidth = width, Effect = new DropShadowEffect { Color = color, BlurRadius = 25, Opacity = 0.3 },
                Child = new StackPanel { Children = { header, colPanel } }
            };

            card.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double h = card.DesiredSize.Height;
            double w = Math.Max(width, card.DesiredSize.Width);
            Canvas.SetLeft(card, x); Canvas.SetTop(card, y);
            _flowCanvas.Children.Add(card); _viewVisualizationElements.Add(card);

            var node = new NodeCard { Id = refTbl.Alias, CardElement = card, X = x, Y = y, Width = w, Height = h, SourceTable = refTbl };
            _nodeCards.Add(node); SetupNodeDrag(card, node);
            
            card.LayoutUpdated += (s, e) => {
                node.Width = card.ActualWidth > 0 ? card.ActualWidth : node.Width;
                node.Height = card.ActualHeight > 0 ? card.ActualHeight : node.Height;
                foreach (var c in node.OutputConnections) UpdateConnectionPath(c);
            };

            return node;
        }

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
                    Text = string.IsNullOrWhiteSpace(col.Alias) ? col.ColumnName : col.Alias, 
                    Background = Brushes.Transparent, 
                    BorderThickness = new Thickness(0,0,0,1), BorderBrush = new SolidColorBrush(Color.FromArgb(0x40, 255,255,255)),
                    Foreground = Brushes.White, FontSize = 11, FontFamily = new FontFamily("Consolas"),
                    Padding = new Thickness(0)
                };
                nameBox.LostFocus += (s, e) => col.Alias = nameBox.Text;
                
                Grid.SetColumn(icon, 0); Grid.SetColumn(nameBox, 1);
                row.Children.Add(icon); row.Children.Add(nameBox);
                colPanel.Children.Add(row);
            }

            var filterPanel = new StackPanel { Margin = new Thickness(12, 10, 12, 10) };
            filterPanel.Children.Add(new TextBlock { Text = "\uE71C  WHERE Filter", FontFamily = new FontFamily("Segoe MDL2 Assets"), Foreground = Brushes.Gray, FontSize = 10, Margin = new Thickness(0,0,0,4) });
            var whereBox = new TextBox { 
                Text = viewDef.WhereClause, Background = new SolidColorBrush(Color.FromArgb(0x44, 0,0,0)), 
                Foreground = Brushes.Cyan, FontFamily = new FontFamily("Consolas"), FontSize = 11,
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x40, 255,255,255)), BorderThickness = new Thickness(1),
                Padding = new Thickness(6), MinHeight = 40, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true
            };
            whereBox.LostFocus += (s, e) => viewDef.WhereClause = whereBox.Text;
            filterPanel.Children.Add(whereBox);

            var card = new Border {
                Background = new SolidColorBrush(Color.FromRgb(0x1F, 0x1F, 0x22)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xC5, 0x86, 0xC0)),
                BorderThickness = new Thickness(2), CornerRadius = new CornerRadius(10),
                MinWidth = width, Effect = new DropShadowEffect { Color = Color.FromRgb(0xC5, 0x86, 0xC0), BlurRadius = 30, Opacity = 0.2 },
                Child = new StackPanel { Children = { header, colPanel, filterPanel } }
            };

            card.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double h = card.DesiredSize.Height;
            double w = Math.Max(width, card.DesiredSize.Width);
            Canvas.SetLeft(card, x); Canvas.SetTop(card, y);
            _flowCanvas.Children.Add(card); _viewVisualizationElements.Add(card);

            var node = new NodeCard { Id = "VIEW", CardElement = card, X = x, Y = y, Width = w, Height = h, IsViewNode = true };
            _nodeCards.Add(node); SetupNodeDrag(card, node);
            
            card.LayoutUpdated += (s, e) => {
                node.Width = card.ActualWidth > 0 ? card.ActualWidth : node.Width;
                node.Height = card.ActualHeight > 0 ? card.ActualHeight : node.Height;
                foreach (var c in node.InputConnections) UpdateConnectionPath(c);
            };
            
            return node;
        }

        private void CreateNodeConnection(NodeCard src, NodeCard tgt, JoinRelationship? join, Color color)
        {
            var p = new Path { Stroke = new SolidColorBrush(color), StrokeThickness = 2.5, Opacity = 0.8, IsHitTestVisible = false };
            var flowP = new Path { Stroke = Brushes.White, StrokeThickness = 3.5, Opacity = 1.0, IsHitTestVisible = false, Visibility = Visibility.Hidden };
            flowP.StrokeDashArray = new DoubleCollection { 4, 4 };
            _flowCanvas.Children.Add(p); _viewVisualizationElements.Add(p);
            _flowCanvas.Children.Add(flowP); _viewVisualizationElements.Add(flowP);

            var startCircle = new Ellipse { Width = 10, Height = 10, Fill = new SolidColorBrush(color), Stroke = new SolidColorBrush(Color.FromRgb(0x1F,0x1F,0x22)), StrokeThickness = 2, IsHitTestVisible = false };
            var endCircle = new Ellipse { Width = 10, Height = 10, Fill = new SolidColorBrush(color), Stroke = new SolidColorBrush(Color.FromRgb(0x1F,0x1F,0x22)), StrokeThickness = 2, IsHitTestVisible = false };
            _flowCanvas.Children.Add(startCircle); _viewVisualizationElements.Add(startCircle);
            _flowCanvas.Children.Add(endCircle); _viewVisualizationElements.Add(endCircle);

            Border? badge = null;
            if (join != null) {
                badge = MakeJoinBadgeInteractive(join, color);
                _flowCanvas.Children.Add(badge); _viewVisualizationElements.Add(badge);
            }

            var conn = new NodeConnection { Source = src, Target = tgt, PathElement = p, FlowPathElement = flowP, StartPort = startCircle, EndPort = endCircle, LabelBadge = badge, JoinData = join, Color = color };
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

        private void UpdateConnectionPath(NodeConnection conn)
        {
            double sW = conn.Source.CardElement.ActualWidth > 0 ? conn.Source.CardElement.ActualWidth : conn.Source.Width;
            double sH = conn.Source.CardElement.ActualHeight > 0 ? conn.Source.CardElement.ActualHeight : conn.Source.Height;
            double sx = conn.Source.X + sW;
            double sy = conn.Source.Y + sH / 2;

            double tW = conn.Target.CardElement.ActualWidth > 0 ? conn.Target.CardElement.ActualWidth : conn.Target.Width;
            double tH = conn.Target.CardElement.ActualHeight > 0 ? conn.Target.CardElement.ActualHeight : conn.Target.Height;
            double tx = conn.Target.X;
            double ty = conn.Target.Y + tH / 2;

            double dist = Math.Abs(tx - sx);
            var p0 = new Point(sx, sy);
            var p3 = new Point(tx, ty);
            var p1 = new Point(sx + dist * 0.5, sy);
            var p2 = new Point(tx - dist * 0.5, ty);

            var fig = new PathFigure { StartPoint = p0, IsClosed = false };
            fig.Segments.Add(new BezierSegment(p1, p2, p3, true));
            var geo = new PathGeometry(new[] { fig });
            conn.PathElement.Data = geo;
            conn.FlowPathElement.Data = geo;

            if (conn.StartPort != null)
            {
                Canvas.SetLeft(conn.StartPort, p0.X - conn.StartPort.Width / 2);
                Canvas.SetTop(conn.StartPort, p0.Y - conn.StartPort.Height / 2);
            }
            if (conn.EndPort != null)
            {
                Canvas.SetLeft(conn.EndPort, p3.X - conn.EndPort.Width / 2);
                Canvas.SetTop(conn.EndPort, p3.Y - conn.EndPort.Height / 2);
            }

            if (conn.LabelBadge != null) {
                conn.LabelBadge.Measure(new Size(1000,1000));
                Canvas.SetLeft(conn.LabelBadge, (sx+tx)/2 - conn.LabelBadge.DesiredSize.Width/2);
                Canvas.SetTop(conn.LabelBadge, (sy+ty)/2 - conn.LabelBadge.DesiredSize.Height/2);
            }
        }

        private void SetupNodeDrag(Border card, NodeCard node)
        {
            card.Cursor = Cursors.Hand;
            
            card.MouseEnter += (s, e) => {
                _hoveredNode = node;
                UpdateAllFlowAnimations();
            };
            card.MouseLeave += (s, e) => {
                if (_hoveredNode == node) { 
                    _hoveredNode = null; 
                    UpdateAllFlowAnimations(); 
                }
            };

            card.MouseLeftButtonDown += (s, e) => {
                IsDraggingNode = true; _draggedNode = node;
                _dragStart = e.GetPosition(_flowCanvas); _dragNodeStartX = node.X; _dragNodeStartY = node.Y;
                card.Cursor = Cursors.SizeAll;
                card.CaptureMouse(); e.Handled = true;
            };
            card.MouseMove += (s, e) => {
                if (!IsDraggingNode || _draggedNode != node) return;
                var cur = e.GetPosition(_flowCanvas);
                node.X = _dragNodeStartX + (cur.X - _dragStart.X);
                node.Y = _dragNodeStartY + (cur.Y - _dragStart.Y);
                Canvas.SetLeft(card, node.X); Canvas.SetTop(card, node.Y);
                foreach (var c in node.OutputConnections) UpdateConnectionPath(c);
                foreach (var c in node.InputConnections) UpdateConnectionPath(c);
            };
            card.MouseLeftButtonUp += (s, e) => {
                if (IsDraggingNode) { 
                    var upPos = e.GetPosition(_flowCanvas);
                    bool wasClick = Math.Abs(upPos.X - _dragStart.X) < 3 && Math.Abs(upPos.Y - _dragStart.Y) < 3;
                    
                    IsDraggingNode = false; 
                    card.Cursor = Cursors.Hand; 
                    card.ReleaseMouseCapture(); 

                    if (wasClick && _viewModel.DbService != null && _viewModel.CurrentViewDefinition != null)
                    {
                        _previewManager.Toggle(node, _viewModel.CurrentViewDefinition, _viewModel.DbService);
                    }
                }
            };
        }

        private void UpdateAllFlowAnimations()
        {
            var activeConns = new HashSet<NodeConnection>();
            if (!IsGlobalDataFlowEnabled && _hoveredNode != null)
            {
                var seen = new HashSet<NodeCard>();
                var q = new Queue<NodeCard>();

                // Downstream traversal
                q.Enqueue(_hoveredNode); seen.Add(_hoveredNode);
                while(q.Count > 0) {
                    var cur = q.Dequeue();
                    foreach (var c in cur.OutputConnections) {
                        activeConns.Add(c);
                        if (seen.Add(c.Target)) q.Enqueue(c.Target);
                    }
                }
                
                // Upstream traversal
                q.Clear();
                q.Enqueue(_hoveredNode);
                while(q.Count > 0) {
                    var cur = q.Dequeue();
                    foreach (var c in cur.InputConnections) {
                        activeConns.Add(c);
                        if (seen.Add(c.Source)) q.Enqueue(c.Source);
                    }
                }
            }

            foreach (var conn in _nodeConnections)
            {
                bool isHoverMode = (!IsGlobalDataFlowEnabled && _hoveredNode != null);
                bool isActive = IsGlobalDataFlowEnabled || activeConns.Contains(conn);

                double targetOpac = isHoverMode && !isActive ? 0.2 : 0.8;
                if (isActive) targetOpac = 1.0;
                conn.PathElement.Opacity = targetOpac;
                conn.PathElement.StrokeThickness = isActive ? 3.5 : 2.5;

                if (isActive && !conn.IsAnimating)
                {
                    conn.FlowPathElement.Visibility = Visibility.Visible;
                    var anim = new DoubleAnimation(16, 0, new Duration(TimeSpan.FromSeconds(0.4))) { RepeatBehavior = RepeatBehavior.Forever };
                    conn.FlowPathElement.BeginAnimation(Shape.StrokeDashOffsetProperty, anim);
                    conn.IsAnimating = true;
                }
                else if (!isActive && conn.IsAnimating)
                {
                    conn.FlowPathElement.BeginAnimation(Shape.StrokeDashOffsetProperty, null);
                    conn.FlowPathElement.Visibility = Visibility.Hidden;
                    conn.IsAnimating = false;
                }
            }
        }

        private void AnimateAllElements()
        {
            foreach (var el in _viewVisualizationElements) {
                if (el is FrameworkElement fe) {
                    fe.Opacity = 0;
                    fe.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(400)));
                }
            }
        }
    }
}
