using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using sqlSense.Models;

namespace sqlSense.UI
{
    public partial class ViewGraphRenderer
    {
        private NodeCard CreateSourceTableNode(ReferencedTable refTbl, double width, double x, double y, string colorStr)
        {
            var color = (Color)ColorConverter.ConvertFromString(colorStr);
            var lightColor = Color.FromArgb(0x40, color.R, color.G, color.B);

            var deleteBtn = new Button {
                Content = "\uE711", FontFamily = new FontFamily("Segoe MDL2 Assets"),
                Background = Brushes.Transparent, BorderThickness = new Thickness(0),
                Foreground = new SolidColorBrush(Color.FromArgb(0x88, 0x1E, 0x1E, 0x1E)),
                VerticalAlignment = VerticalAlignment.Center, Cursor = Cursors.Hand
            };
            deleteBtn.Click += (s, e) => {
                _viewModel!.Canvas.CurrentViewDefinition!.ReferencedTables.Remove(refTbl);
                _viewModel.Canvas.CurrentViewDefinition.Columns.RemoveAll(c => c.SourceTable == refTbl.Alias);
                RenderViewVisualization(_viewModel.Canvas.CurrentViewDefinition);
            };

            var headerPanel = new DockPanel();
            DockPanel.SetDock(deleteBtn, Dock.Right);
            headerPanel.Children.Add(deleteBtn);
            
            var iconLabelPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            iconLabelPanel.Children.Add(new TextBlock { Text = "\uE80A", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 12, Margin = new Thickness(0,0,8,0), VerticalAlignment = VerticalAlignment.Center });
            iconLabelPanel.Children.Add(new TextBlock { Text = refTbl.DisplayName, FontWeight = FontWeights.Bold, FontFamily = new FontFamily("Segoe UI"), VerticalAlignment = VerticalAlignment.Center });
            headerPanel.Children.Add(iconLabelPanel);

            var header = new Border {
                Background = new SolidColorBrush(color), CornerRadius = new CornerRadius(10, 10, 0, 0),
                Padding = new Thickness(14, 8, 14, 8), Child = headerPanel
            };

            var colPanel = new StackPanel { Margin = new Thickness(0, 5, 0, 5) };
            var allCols = _viewModel!.Canvas.CurrentViewDefinition!.SourceTableAllColumns.GetValueOrDefault(refTbl.FullName, new List<string>());
            foreach (var col in allCols)
            {
                bool isSelected = refTbl.UsedColumns.Contains(col);
                var rowBtn = new Button { Background = Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(0), Cursor = Cursors.Hand, HorizontalContentAlignment = HorizontalAlignment.Stretch };
                var rowPanel = new Border {
                    Background = isSelected ? new SolidColorBrush(lightColor) : Brushes.Transparent,
                    BorderThickness = isSelected ? new Thickness(3,0,0,1) : new Thickness(0,0,0,1), 
                    BorderBrush = isSelected ? new SolidColorBrush(color) : new SolidColorBrush(Color.FromArgb(0x20, 255,255,255)),
                    Margin = new Thickness(0), Padding = new Thickness(12, 6, 12, 6),
                    Child = new TextBlock { Text = col, Foreground = isSelected ? Brushes.White : Brushes.Gray, FontSize = 11, FontFamily = new FontFamily("Consolas"), HorizontalAlignment = HorizontalAlignment.Left }
                };
                rowBtn.Content = rowPanel;
                rowBtn.Click += (s, e) => {
                    if (!refTbl.UsedColumns.Contains(col)) {
                        refTbl.UsedColumns.Add(col);
                        _viewModel.Canvas.CurrentViewDefinition.Columns.Add(new ViewColumnInfo { SourceTable = refTbl.Alias, SourceColumn = col, ColumnName = col });
                    } else {
                        refTbl.UsedColumns.Remove(col);
                        _viewModel.Canvas.CurrentViewDefinition.Columns.RemoveAll(c => c.SourceTable == refTbl.Alias && c.SourceColumn == col);
                    }
                    RenderViewVisualization(_viewModel.Canvas.CurrentViewDefinition);
                };
                colPanel.Children.Add(rowBtn);
            }

            var card = new Border {
                Background = new SolidColorBrush(Color.FromRgb(0x1F, 0x1F, 0x22)),
                BorderBrush = new SolidColorBrush(color), BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(10), MinWidth = width, Effect = new DropShadowEffect { Color = color, BlurRadius = 25, Opacity = 0.3 },
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
            var viewNameBox = new TextBox {
                Text = viewDef.ViewName, Background = Brushes.Transparent, BorderThickness = new Thickness(0), 
                Foreground = Brushes.White, FontWeight = FontWeights.Bold, FontSize = 13, 
                VerticalAlignment = VerticalAlignment.Center, CaretBrush = Brushes.White
            };
            viewNameBox.LostFocus += (s, e) => viewDef.ViewName = viewNameBox.Text;
            var headerPanel = new StackPanel { Orientation = Orientation.Horizontal };
            headerPanel.Children.Add(new TextBlock { Text = "\uE7B3", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 14, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,10,0) });
            headerPanel.Children.Add(viewNameBox);

            var header = new Border {
                Background = new LinearGradientBrush(Color.FromRgb(0xC5, 0x86, 0xC0), Color.FromRgb(0x9C, 0x27, 0xB0), 0),
                CornerRadius = new CornerRadius(10, 10, 0, 0), Padding = new Thickness(14, 8, 14, 8), Child = headerPanel
            };

            var colPanel = new StackPanel { Margin = new Thickness(0, 5, 0, 5) };
            foreach (var col in viewDef.Columns.ToList())
            {
                var row = new Grid { Margin = new Thickness(8, 1, 8, 1) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var icon = new TextBlock { Text = "\uE71A", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 9, Foreground = Brushes.Cyan, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,6,0) };
                var nameBox = new TextBox {
                    Text = string.IsNullOrWhiteSpace(col.Alias) ? col.ColumnName : col.Alias,
                    Background = Brushes.Transparent, BorderThickness = new Thickness(0, 0, 0, 1), BorderBrush = new SolidColorBrush(Color.FromArgb(0x40, 255, 255, 255)),
                    Foreground = Brushes.White, FontSize = 11, FontFamily = new FontFamily("Consolas"), Padding = new Thickness(0)
                };
                nameBox.LostFocus += (s, e) => col.Alias = nameBox.Text;

                var delBtn = new Button {
                    Content = "\uE711", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 10,
                    Background = Brushes.Transparent, BorderThickness = new Thickness(0),
                    Foreground = new SolidColorBrush(Color.FromArgb(0x66, 0xFF, 0x44, 0x44)), Cursor = Cursors.Hand
                };
                var capturedCol = col;
                delBtn.Click += (s, e) => {
                    viewDef.Columns.Remove(capturedCol);
                    var srcTbl = viewDef.ReferencedTables.FirstOrDefault(t => t.Alias == capturedCol.SourceTable);
                    srcTbl?.UsedColumns.Remove(capturedCol.SourceColumn);
                    RenderViewVisualization(viewDef);
                };

                Grid.SetColumn(icon, 0); Grid.SetColumn(nameBox, 1); Grid.SetColumn(delBtn, 2);
                row.Children.Add(icon); row.Children.Add(nameBox); row.Children.Add(delBtn);
                colPanel.Children.Add(row);
            }

            var addExprBtn = new Button {
                Content = "+ Add Expression", FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(0xC5, 0x86, 0xC0)),
                Background = Brushes.Transparent, BorderThickness = new Thickness(0), Cursor = Cursors.Hand, 
                Margin = new Thickness(12, 4, 12, 4), HorizontalAlignment = HorizontalAlignment.Left
            };
            addExprBtn.Click += (s, e) => {
                viewDef.Columns.Add(new ViewColumnInfo { ColumnName = "NewExpr", Expression = "1", Alias = "NewExpr" });
                RenderViewVisualization(viewDef);
            };
            colPanel.Children.Add(addExprBtn);

            var clausePanel = new StackPanel { Margin = new Thickness(12, 6, 12, 10) };
            CanvasUIFactory.AddClauseEditor(clausePanel, "\uE71C", "FILTER / WHERE", viewDef.WhereClause, v => { viewDef.WhereClause = v; RenderViewVisualization(viewDef); });
            CanvasUIFactory.AddClauseEditor(clausePanel, "\uE14C", "GROUP BY", viewDef.GroupByClause, v => { viewDef.GroupByClause = v; RenderViewVisualization(viewDef); });
            CanvasUIFactory.AddClauseEditor(clausePanel, "\uE16E", "HAVING", viewDef.HavingClause, v => { viewDef.HavingClause = v; RenderViewVisualization(viewDef); });
            CanvasUIFactory.AddClauseEditor(clausePanel, "\uE174", "ORDER BY", viewDef.OrderByClause, v => { viewDef.OrderByClause = v; RenderViewVisualization(viewDef); });

            var previewContent = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            previewContent.Children.Add(new TextBlock { Text = "\uE943", FontFamily = new FontFamily("Segoe MDL2 Assets"), Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center });
            previewContent.Children.Add(new TextBlock { Text = "PREVIEW SQL", VerticalAlignment = VerticalAlignment.Center });

            var previewBtn = new Button {
                Content = previewContent, 
                FontSize = 10, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(0x4F, 0xC3, 0xF7)),
                Background = new SolidColorBrush(Color.FromArgb(0x20, 0x4F, 0xC3, 0xF7)), BorderBrush = new SolidColorBrush(Color.FromArgb(0x40, 0x4F, 0xC3, 0xF7)),
                BorderThickness = new Thickness(1), Cursor = Cursors.Hand, Margin = new Thickness(12, 4, 12, 10), Padding = new Thickness(10, 6, 10, 6), HorizontalAlignment = HorizontalAlignment.Stretch
            };
            previewBtn.Click += (s, e) => { _viewModel.SqlEditor.SqlText = viewDef.ToSql(); _viewModel.StatusMessage = "SQL preview updated from canvas."; };

            var card = new Border {
                Background = new SolidColorBrush(Color.FromRgb(0x1F, 0x1F, 0x22)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xC5, 0x86, 0xC0)), BorderThickness = new Thickness(2), 
                CornerRadius = new CornerRadius(10), MinWidth = width, Effect = new DropShadowEffect { Color = Color.FromRgb(0xC5, 0x86, 0xC0), BlurRadius = 30, Opacity = 0.2 },
                Child = new StackPanel { Children = { header, colPanel, clausePanel, previewBtn } }
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
                RenderViewVisualization(_viewModel!.Canvas.CurrentViewDefinition!);
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
            var fig = new PathFigure { StartPoint = new Point(sx, sy), IsClosed = false };
            fig.Segments.Add(new BezierSegment(new Point(sx + dist * 0.5, sy), new Point(tx - dist * 0.5, ty), new Point(tx, ty), true));
            var geo = new PathGeometry(new[] { fig });
            conn.PathElement.Data = geo;
            conn.FlowPathElement.Data = geo;

            if (conn.StartPort != null) {
                Canvas.SetLeft(conn.StartPort, sx - conn.StartPort.Width / 2);
                Canvas.SetTop(conn.StartPort, sy - conn.StartPort.Height / 2);
            }
            if (conn.EndPort != null) {
                Canvas.SetLeft(conn.EndPort, tx - conn.EndPort.Width / 2);
                Canvas.SetTop(conn.EndPort, ty - conn.EndPort.Height / 2);
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
            card.MouseEnter += (s, e) => { _hoveredNode = node; UpdateAllFlowAnimations(); };
            card.MouseLeave += (s, e) => { if (_hoveredNode == node) { _hoveredNode = null; UpdateAllFlowAnimations(); } };

            card.MouseLeftButtonDown += (s, e) => {
                IsDraggingNode = true; _draggedNode = node;
                _dragStart = e.GetPosition(_flowCanvas); _dragNodeStartX = node.X; _dragNodeStartY = node.Y;
                card.Cursor = Cursors.SizeAll; card.CaptureMouse(); e.Handled = true;
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
                if (!IsDraggingNode) return;
                IsDraggingNode = false; _draggedNode = null;
                card.Cursor = Cursors.Hand; card.ReleaseMouseCapture(); e.Handled = true;
            };
        }
    }
}
