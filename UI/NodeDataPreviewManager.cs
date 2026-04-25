using System;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using sqlSense.Models;
using sqlSense.Services;
using sqlSense.Services.Sql;

namespace sqlSense.UI
{
    public class NodeDataPreviewManager
    {
        private readonly Canvas _flowCanvas;
        private readonly DataGrid _dataGrid;
        private readonly Border _previewCard;
        private NodeCard? _currentNode;

        // For dragging behavior
        private bool _isDragging;
        private Point _dragStart;
        private double _startX, _startY;

        public bool IsVisible => _previewCard.Visibility == Visibility.Visible;

        public NodeDataPreviewManager(Canvas flowCanvas)
        {
            _flowCanvas = flowCanvas;

            // 1. DataGrid - matching dark theme
            _dataGrid = new DataGrid
            {
                IsReadOnly = true,
                AutoGenerateColumns = true,
                MaxHeight = 350,
                MaxWidth = 700,
                ColumnWidth = new DataGridLength(150),
                MinHeight = 60,
                MinWidth = 150,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                Background = Brushes.Transparent, // Parent handles background
                Foreground = Brushes.LightGray,
                BorderThickness = new Thickness(0),
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                HorizontalGridLinesBrush = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)),
                RowBackground = new SolidColorBrush(Color.FromRgb(0x25, 0x25, 0x26)),
                AlternatingRowBackground = new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x30)),
                FontSize = 10.5,
                FontFamily = new FontFamily("Consolas"),
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
            };

            var columnHeaderStyle = new Style(typeof(DataGridColumnHeader));
            columnHeaderStyle.Setters.Add(new Setter(DataGridColumnHeader.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33))));
            columnHeaderStyle.Setters.Add(new Setter(DataGridColumnHeader.ForegroundProperty, new SolidColorBrush(Color.FromRgb(0x4F, 0xC3, 0xF7))));
            columnHeaderStyle.Setters.Add(new Setter(DataGridColumnHeader.PaddingProperty, new Thickness(8, 4, 8, 4)));
            columnHeaderStyle.Setters.Add(new Setter(DataGridColumnHeader.FontWeightProperty, FontWeights.Bold));
            _dataGrid.Resources.Add(typeof(DataGridColumnHeader), columnHeaderStyle);
            
            _dataGrid.AutoGeneratingColumn += (s, e) => {
                if (e.Column is DataGridTextColumn textColumn)
                {
                    var style = new Style(typeof(TextBlock));
                    style.Setters.Add(new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis));
                    style.Setters.Add(new Setter(TextBlock.ToolTipProperty, new Binding("Text") { RelativeSource = RelativeSource.Self }));
                    textColumn.ElementStyle = style;
                }

                if (_currentNode?.SourceTable != null)
                {
                    var nodeColor = (Color)ColorConverter.ConvertFromString(_currentNode.Color);
                    var style = new Style(typeof(DataGridColumnHeader));
                    style.Setters.Add(new Setter(DataGridColumnHeader.BackgroundProperty, new SolidColorBrush(nodeColor)));
                    style.Setters.Add(new Setter(DataGridColumnHeader.ForegroundProperty, Brushes.White));
                    style.Setters.Add(new Setter(DataGridColumnHeader.PaddingProperty, new Thickness(8,4,8,4)));
                    style.Setters.Add(new Setter(DataGridColumnHeader.FontWeightProperty, FontWeights.Bold));
                    e.Column.HeaderStyle = style;
                }
            };

            // 2. Main Card - Very minimal, only padding around the table
            _previewCard = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x4F, 0xC3, 0xF7)),
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(1), // Professional hairline gap
                Child = new Border {
                    Padding = new Thickness(5),
                    Child = _dataGrid
                },
                Effect = new DropShadowEffect { BlurRadius = 35, Opacity = 0.8, Color = Colors.Black, ShadowDepth = 0 },
                Visibility = Visibility.Collapsed,
                Cursor = Cursors.SizeAll,
                IsHitTestVisible = true
            };

            // Drag behavior
            _previewCard.MouseLeftButtonDown += (s, e) => {
                if (e.OriginalSource is DependencyObject depObj)
                {
                    DependencyObject? curr = depObj;
                    while (curr != null && curr != _previewCard)
                    {
                        if (curr is Thumb || curr is ScrollBar || curr is RepeatButton || curr is DataGridColumnHeader)
                        {
                            return; // Allow column resizing and scrolling
                        }
                        curr = VisualTreeHelper.GetParent(curr);
                    }
                }
                
                _isDragging = true;
                _dragStart = e.GetPosition(_flowCanvas);
                _startX = Canvas.GetLeft(_previewCard);
                _startY = Canvas.GetTop(_previewCard);
                _previewCard.CaptureMouse();
                e.Handled = true; // Prevents "Click Outside" from hiding immediately if we click on card
            };
            _previewCard.MouseMove += (s, e) => {
                if (!_isDragging) return;
                var cur = e.GetPosition(_flowCanvas);
                Canvas.SetLeft(_previewCard, _startX + (cur.X - _dragStart.X));
                Canvas.SetTop(_previewCard, _startY + (cur.Y - _dragStart.Y));
            };
            _previewCard.MouseLeftButtonUp += (s, e) => {
                if (_isDragging) { _isDragging = false; _previewCard.ReleaseMouseCapture(); }
            };

            _flowCanvas.Children.Add(_previewCard);
            Panel.SetZIndex(_previewCard, 10000); // Always on top
        }

        public async void Toggle(NodeCard node, ViewDefinitionInfo viewDef, DatabaseService db)
        {
            if (_currentNode == node && _previewCard.Visibility == Visibility.Visible)
            {
                Hide();
                return;
            }

            _currentNode = node;
            string sql = QueryBuilderService.BuildSqlForNode(node, viewDef);

            // Apply node colors
            var nodeColor = (Color)ColorConverter.ConvertFromString(node.Color);
            _previewCard.BorderBrush = new SolidColorBrush(nodeColor);
            _previewCard.Effect = new DropShadowEffect { Color = nodeColor, BlurRadius = 40, Opacity = 0.4, ShadowDepth = 0 };
            
            // Initial positioning
            Canvas.SetLeft(_previewCard, node.X + node.Width + 25);
            Canvas.SetTop(_previewCard, node.Y - 20);
            _previewCard.Visibility = Visibility.Visible;

            try
            {
                DataTable dt = await db.ExecuteQueryAsync(viewDef.DatabaseName, sql);
                _dataGrid.ItemsSource = dt.DefaultView;
            }
            catch { /* Handled by DatabaseService Logging */ }
        }

        public void Hide()
        {
            _previewCard.Visibility = Visibility.Collapsed;
            _currentNode = null;
        }

        public bool IsOwnedElement(DependencyObject? originalSource)
        {
            // Traverse up to see if the clicked element is part of THE preview card
            DependencyObject? curr = originalSource;
            while (curr != null)
            {
                if (curr == _previewCard) return true;
                curr = VisualTreeHelper.GetParent(curr);
            }
            return false;
        }
    }
}
