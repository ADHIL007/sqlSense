using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

            for (int i = 0; i < tableCount; i++)
            {
                var refTbl = viewDef.ReferencedTables[i];
                
                // Dynamic HSL Color Generation: Spread Hues across 360 degrees
                // ensures no two tables have the same color regardless of count
                double hue = (i * 137.5) % 360; // Use Golden Angle for high distribution
                string colorStr = HslToRgbHex(hue, 0.65, 0.5); // Vibrant but dark-theme friendly
                
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

                    var joinColor = (Color)ColorConverter.ConvertFromString(leftNode.Color); // Use left node color by default for join lines
                    CreateNodeConnection(leftNode, joinCard, null, (Color)ColorConverter.ConvertFromString(leftNode.Color));
                    CreateNodeConnection(rightNode, joinCard, null, (Color)ColorConverter.ConvertFromString(rightNode.Color));

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
                var finalColor = (Color)ColorConverter.ConvertFromString(finalNode.Color);
                CreateNodeConnection(finalNode, viewNode, null, finalColor);
            }

            // ── 4. Add Table button (below source tables) ──
            var addTableBtn = CreateActionButton("\uE710  ADD TABLE", Color.FromRgb(0x4C, 0xAF, 0x50), gridStartX, currentY + 20);
            addTableBtn.MouseLeftButtonDown += async (s, e) => {
                e.Handled = true;
                if (_viewModel.DbService == null || viewDef == null) return;
                await ShowAddTablePopup(viewDef, gridStartX, currentY + 60);
            };

            // ── 5. Add Join button (below add table) ──
            var addJoinBtn = CreateActionButton("\uE710  ADD JOIN", Color.FromRgb(0xFF, 0xB7, 0x4D), gridStartX, currentY + 62);
            addJoinBtn.MouseLeftButtonDown += (s, e) => {
                e.Handled = true;
                if (viewDef.ReferencedTables.Count < 2) {
                    _viewModel.StatusMessage = "Need at least 2 tables to create a JOIN.";
                    return;
                }
                ShowAddJoinPopup(viewDef, gridStartX, currentY + 100);
            };

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

            // ── Header with delete button ──
            var headerDock = new DockPanel();
            var delJoinBtn = new Button {
                Content = "\uE711", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 10,
                Background = Brushes.Transparent, BorderThickness = new Thickness(0),
                Foreground = new SolidColorBrush(Color.FromArgb(0x88, 0xFF, 0x44, 0x44)),
                Cursor = Cursors.Hand, ToolTip = "Remove this JOIN", VerticalAlignment = VerticalAlignment.Center
            };
            delJoinBtn.Click += (s, e) => {
                _viewModel!.CurrentViewDefinition!.Joins.Remove(join);
                RenderViewVisualization(_viewModel.CurrentViewDefinition);
            };
            DockPanel.SetDock(delJoinBtn, Dock.Right);
            headerDock.Children.Add(delJoinBtn);
            headerDock.Children.Add(new TextBlock { Text = $"{join.JoinType} JOIN", Foreground = new SolidColorBrush(color), FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center });

            var header = new Border {
                Background = new SolidColorBrush(Color.FromArgb(0x40, color.R, color.G, color.B)),
                BorderBrush = new SolidColorBrush(color),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(10, 6, 10, 6),
                CornerRadius = new CornerRadius(8, 8, 0, 0),
                Child = headerDock
            };

            var body = new StackPanel { Margin = new Thickness(10) };

            // ── Editable left column ──
            var leftBox = new TextBox {
                Text = $"{join.LeftTableAlias}.{join.LeftColumn}",
                Background = Brushes.Transparent, BorderThickness = new Thickness(0, 0, 0, 1),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x30, 255, 255, 255)),
                Foreground = Brushes.LightGray, FontSize = 11, FontFamily = new FontFamily("Consolas"),
                HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center
            };
            leftBox.LostFocus += (s, e) => {
                var parts = leftBox.Text.Split('.', 2);
                if (parts.Length == 2) { join.LeftTableAlias = parts[0]; join.LeftColumn = parts[1]; }
            };
            body.Children.Add(leftBox);

            body.Children.Add(new TextBlock { Text = "=", Foreground = Brushes.Gray, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 2, 0, 2) });

            // ── Editable right column ──
            var rightBox = new TextBox {
                Text = $"{join.RightTableAlias}.{join.RightColumn}",
                Background = Brushes.Transparent, BorderThickness = new Thickness(0, 0, 0, 1),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x30, 255, 255, 255)),
                Foreground = Brushes.LightGray, FontSize = 11, FontFamily = new FontFamily("Consolas"),
                HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center
            };
            rightBox.LostFocus += (s, e) => {
                var parts = rightBox.Text.Split('.', 2);
                if (parts.Length == 2) { join.RightTableAlias = parts[0]; join.RightColumn = parts[1]; }
            };
            body.Children.Add(rightBox);

            // ── Interactive type changer ──
            var changeBtn = new Button {
                Content = "⟳ CHANGE TYPE", FontSize = 9, Foreground = new SolidColorBrush(color),
                Background = Brushes.Transparent, BorderThickness = new Thickness(0),
                Margin = new Thickness(0, 8, 0, 6), Cursor = Cursors.Hand
            };
            changeBtn.Click += (s, e) => {
                join.JoinType = join.JoinType.ToUpper() switch { "INNER" => "LEFT", "LEFT" => "RIGHT", "RIGHT" => "FULL", _ => "INNER" };
                RenderViewVisualization(_viewModel!.CurrentViewDefinition!);
            };
            body.Children.Add(changeBtn);

            // ── Flowing columns box ──
            if (upstreamCols.Any())
            {
                var flowBorder = new Border { Background = new SolidColorBrush(Color.FromArgb(0x10, 255, 255, 255)), Margin = new Thickness(-10, 10, -10, -10), CornerRadius = new CornerRadius(0, 0, 8, 8) };
                var flowPanel = new StackPanel();
                flowPanel.Children.Add(new TextBlock { Text = "SELECTED FIELDS", FontSize = 9, Foreground = Brushes.Gray, Margin = new Thickness(12, 10, 12, 5), FontWeight = FontWeights.Bold });
                foreach (var c in upstreamCols)
                {
                    var rowPanel = new Border {
                        Background = new SolidColorBrush(Color.FromArgb(0x20, 0x03, 0x9B, 0xE5)),
                        BorderThickness = new Thickness(3, 0, 0, 1),
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
            // ── Editable Header with view name ──
            var viewNameBox = new TextBox {
                Text = viewDef.ViewName, Background = Brushes.Transparent,
                BorderThickness = new Thickness(0), Foreground = Brushes.White,
                FontWeight = FontWeights.Bold, FontSize = 13, VerticalAlignment = VerticalAlignment.Center,
                CaretBrush = Brushes.White
            };
            viewNameBox.LostFocus += (s, e) => {
                viewDef.ViewName = viewNameBox.Text;
                _viewModel.StatusMessage = $"View renamed to {viewNameBox.Text} (not synced)";
            };
            var headerPanel = new DockPanel();
            headerPanel.Children.Add(new TextBlock { Text = "\uE7B3  ", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 14, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center });
            headerPanel.Children.Add(viewNameBox);

            var header = new Border {
                Background = new LinearGradientBrush(Color.FromRgb(0xC5, 0x86, 0xC0), Color.FromRgb(0x9C, 0x27, 0xB0), 0),
                CornerRadius = new CornerRadius(10, 10, 0, 0),
                Padding = new Thickness(14, 8, 14, 8),
                Child = headerPanel
            };

            // ── Column list with delete buttons ──
            var colPanel = new StackPanel { Margin = new Thickness(0, 5, 0, 5) };
            foreach (var col in viewDef.Columns.ToList())
            {
                var row = new Grid { Margin = new Thickness(8, 1, 8, 1) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var icon = new TextBlock { Text = "\uE71A ", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 9, Foreground = Brushes.Cyan, VerticalAlignment = VerticalAlignment.Center };
                var nameBox = new TextBox {
                    Text = string.IsNullOrWhiteSpace(col.Alias) ? col.ColumnName : col.Alias,
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0, 0, 0, 1), BorderBrush = new SolidColorBrush(Color.FromArgb(0x40, 255, 255, 255)),
                    Foreground = Brushes.White, FontSize = 11, FontFamily = new FontFamily("Consolas"),
                    Padding = new Thickness(0), ToolTip = $"{col.SourceTable}.{col.SourceColumn}"
                };
                nameBox.LostFocus += (s, e) => col.Alias = nameBox.Text;

                var delBtn = new Button {
                    Content = "\uE711", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 10,
                    Background = Brushes.Transparent, BorderThickness = new Thickness(0),
                    Foreground = new SolidColorBrush(Color.FromArgb(0x66, 0xFF, 0x44, 0x44)),
                    Cursor = Cursors.Hand, ToolTip = "Remove column", Padding = new Thickness(2)
                };
                var capturedCol = col;
                delBtn.Click += (s, e) => {
                    viewDef.Columns.Remove(capturedCol);
                    // Also uncheck in source table
                    var srcTbl = viewDef.ReferencedTables.FirstOrDefault(t => t.Alias == capturedCol.SourceTable);
                    srcTbl?.UsedColumns.Remove(capturedCol.SourceColumn);
                    RenderViewVisualization(viewDef);
                };

                Grid.SetColumn(icon, 0); Grid.SetColumn(nameBox, 1); Grid.SetColumn(delBtn, 2);
                row.Children.Add(icon); row.Children.Add(nameBox); row.Children.Add(delBtn);
                colPanel.Children.Add(row);
            }

            // ── Add Expression Column button ──
            var addExprBtn = new Button {
                Content = "+ Add Expression", FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(0xC5, 0x86, 0xC0)),
                Background = Brushes.Transparent, BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand, Margin = new Thickness(12, 4, 12, 4),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            addExprBtn.Click += (s, e) => {
                viewDef.Columns.Add(new ViewColumnInfo {
                    ColumnName = "NewExpr", Expression = "1", Alias = "NewExpr"
                });
                RenderViewVisualization(viewDef);
            };
            colPanel.Children.Add(addExprBtn);

            // ── Clause editors (WHERE, GROUP BY, HAVING, ORDER BY) ──
            var clausePanel = new StackPanel { Margin = new Thickness(12, 6, 12, 10) };
            AddClauseEditor(clausePanel, "\uE71C  WHERE", viewDef.WhereClause, v => viewDef.WhereClause = v);
            AddClauseEditor(clausePanel, "\uE14C  GROUP BY", viewDef.GroupByClause, v => viewDef.GroupByClause = v);
            AddClauseEditor(clausePanel, "\uE16E  HAVING", viewDef.HavingClause, v => viewDef.HavingClause = v);
            AddClauseEditor(clausePanel, "\uE174  ORDER BY", viewDef.OrderByClause, v => viewDef.OrderByClause = v);

            // ── Live SQL Preview button ──
            var previewBtn = new Button {
                Content = "\uE943  PREVIEW SQL", FontFamily = new FontFamily("Segoe MDL2 Assets, Segoe UI"),
                FontSize = 10, FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x4F, 0xC3, 0xF7)),
                Background = new SolidColorBrush(Color.FromArgb(0x20, 0x4F, 0xC3, 0xF7)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x40, 0x4F, 0xC3, 0xF7)),
                BorderThickness = new Thickness(1), Cursor = Cursors.Hand,
                Margin = new Thickness(12, 4, 12, 10), Padding = new Thickness(10, 6, 10, 6),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            previewBtn.Click += (s, e) => {
                _viewModel.SqlText = viewDef.ToSql();
                _viewModel.StatusMessage = "SQL preview updated from canvas.";
            };

            var card = new Border {
                Background = new SolidColorBrush(Color.FromRgb(0x1F, 0x1F, 0x22)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xC5, 0x86, 0xC0)),
                BorderThickness = new Thickness(2), CornerRadius = new CornerRadius(10),
                MinWidth = width, Effect = new DropShadowEffect { Color = Color.FromRgb(0xC5, 0x86, 0xC0), BlurRadius = 30, Opacity = 0.2 },
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

        private void AddClauseEditor(StackPanel parent, string label, string value, Action<string> setter)
        {
            parent.Children.Add(new TextBlock {
                Text = label, FontFamily = new FontFamily("Segoe MDL2 Assets, Segoe UI"),
                Foreground = Brushes.Gray, FontSize = 10, Margin = new Thickness(0, 6, 0, 3)
            });
            var box = new TextBox {
                Text = value, Background = new SolidColorBrush(Color.FromArgb(0x44, 0, 0, 0)),
                Foreground = Brushes.Cyan, FontFamily = new FontFamily("Consolas"), FontSize = 11,
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x40, 255, 255, 255)),
                BorderThickness = new Thickness(1), Padding = new Thickness(6),
                MinHeight = 28, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true
            };
            box.LostFocus += (s, e) => setter(box.Text);
            parent.Children.Add(box);
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

        private Border CreateActionButton(string text, Color color, double x, double y)
        {
            var btn = new Border {
                Background = new SolidColorBrush(Color.FromArgb(0x20, color.R, color.G, color.B)),
                BorderBrush = new SolidColorBrush(color), BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(8), Padding = new Thickness(14, 8, 14, 8),
                Cursor = Cursors.Hand,
                Effect = new DropShadowEffect { Color = color, BlurRadius = 12, Opacity = 0.3 },
                Child = new TextBlock {
                    Text = text, FontFamily = new FontFamily("Segoe MDL2 Assets, Segoe UI"),
                    Foreground = new SolidColorBrush(color), FontSize = 12, FontWeight = FontWeights.Bold
                }
            };
            btn.MouseEnter += (s, e) => btn.Background = new SolidColorBrush(Color.FromArgb(0x40, color.R, color.G, color.B));
            btn.MouseLeave += (s, e) => btn.Background = new SolidColorBrush(Color.FromArgb(0x20, color.R, color.G, color.B));
            Canvas.SetLeft(btn, x); Canvas.SetTop(btn, y);
            _flowCanvas.Children.Add(btn); _viewVisualizationElements.Add(btn);
            return btn;
        }

        private async Task ShowAddTablePopup(ViewDefinitionInfo viewDef, double x, double y)
        {
            if (_viewModel.DbService == null) return;

            var tables = await _viewModel.DbService.GetAllTablesForDatabaseAsync(viewDef.DatabaseName);

            var popup = new Border {
                Background = new SolidColorBrush(Color.FromRgb(0x25, 0x25, 0x28)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)),
                BorderThickness = new Thickness(1.5), CornerRadius = new CornerRadius(8),
                Padding = new Thickness(8), MinWidth = 260, MaxHeight = 350,
                Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 20, Opacity = 0.6 }
            };

            var panel = new StackPanel();
            panel.Children.Add(new TextBlock { Text = "SELECT A TABLE", FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)), FontSize = 10, Margin = new Thickness(4, 2, 4, 6) });

            var scroll = new ScrollViewer { MaxHeight = 280, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var listPanel = new StackPanel();

            foreach (var t in tables)
            {
                // Skip tables already in the view
                if (viewDef.ReferencedTables.Any(r => r.Schema == t.Schema && r.Name == t.Name)) continue;

                var item = new Button {
                    Content = $"{t.Schema}.{t.Name}", FontSize = 11,
                    Foreground = Brushes.LightGray, Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0), Cursor = Cursors.Hand,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    Padding = new Thickness(8, 5, 8, 5)
                };
                item.Click += async (s, e) => {
                    _flowCanvas.Children.Remove(popup);
                    _viewVisualizationElements.Remove(popup);
                    await _viewModel.AddTableToViewAsync(t.Schema, t.Name);
                    RenderViewVisualization(viewDef);
                };
                listPanel.Children.Add(item);
            }
            scroll.Content = listPanel;
            panel.Children.Add(scroll);

            // Close button
            var closeBtn = new Button {
                Content = "CANCEL", FontSize = 9, Foreground = Brushes.Gray,
                Background = Brushes.Transparent, BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand, Margin = new Thickness(0, 6, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            closeBtn.Click += (s, e) => { _flowCanvas.Children.Remove(popup); _viewVisualizationElements.Remove(popup); };
            panel.Children.Add(closeBtn);

            popup.Child = panel;
            Canvas.SetLeft(popup, x); Canvas.SetTop(popup, y);
            Panel.SetZIndex(popup, 9999);
            _flowCanvas.Children.Add(popup); _viewVisualizationElements.Add(popup);
        }

        private void ShowAddJoinPopup(ViewDefinitionInfo viewDef, double x, double y)
        {
            var popup = new Border {
                Background = new SolidColorBrush(Color.FromRgb(0x25, 0x25, 0x28)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xB7, 0x4D)),
                BorderThickness = new Thickness(1.5), CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12), MinWidth = 300,
                Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 20, Opacity = 0.6 }
            };

            var panel = new StackPanel();
            panel.Children.Add(new TextBlock { Text = "CREATE JOIN", FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xB7, 0x4D)), FontSize = 11, Margin = new Thickness(0, 0, 0, 8) });

            var tableAliases = viewDef.ReferencedTables.Select(t => t.Alias).ToArray();

            // Join Type selector
            var joinTypeCombo = new ComboBox { FontSize = 11, Margin = new Thickness(0, 0, 0, 8), Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x22)), Foreground = Brushes.White };
            foreach (var jt in new[] { "INNER", "LEFT", "RIGHT", "FULL" }) joinTypeCombo.Items.Add(jt);
            joinTypeCombo.SelectedIndex = 0;
            panel.Children.Add(new TextBlock { Text = "Join Type", Foreground = Brushes.Gray, FontSize = 9 });
            panel.Children.Add(joinTypeCombo);

            // Left table
            var leftTableCombo = new ComboBox { FontSize = 11, Margin = new Thickness(0, 0, 0, 4), Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x22)), Foreground = Brushes.White };
            foreach (var a in tableAliases) leftTableCombo.Items.Add(a);
            if (tableAliases.Length > 0) leftTableCombo.SelectedIndex = 0;
            panel.Children.Add(new TextBlock { Text = "Left Table", Foreground = Brushes.Gray, FontSize = 9, Margin = new Thickness(0, 4, 0, 0) });
            panel.Children.Add(leftTableCombo);

            var leftColBox = new TextBox { Text = "", FontSize = 11, Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x22)), Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 4), Padding = new Thickness(4) };
            panel.Children.Add(new TextBlock { Text = "Left Column", Foreground = Brushes.Gray, FontSize = 9, Margin = new Thickness(0, 4, 0, 0) });
            panel.Children.Add(leftColBox);

            // Right table
            var rightTableCombo = new ComboBox { FontSize = 11, Margin = new Thickness(0, 0, 0, 4), Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x22)), Foreground = Brushes.White };
            foreach (var a in tableAliases) rightTableCombo.Items.Add(a);
            if (tableAliases.Length > 1) rightTableCombo.SelectedIndex = 1;
            panel.Children.Add(new TextBlock { Text = "Right Table", Foreground = Brushes.Gray, FontSize = 9, Margin = new Thickness(0, 4, 0, 0) });
            panel.Children.Add(rightTableCombo);

            var rightColBox = new TextBox { Text = "", FontSize = 11, Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x22)), Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 8), Padding = new Thickness(4) };
            panel.Children.Add(new TextBlock { Text = "Right Column", Foreground = Brushes.Gray, FontSize = 9, Margin = new Thickness(0, 4, 0, 0) });
            panel.Children.Add(rightColBox);

            // Buttons
            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var cancelBtn = new Button { Content = "CANCEL", FontSize = 9, Foreground = Brushes.Gray, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Cursor = Cursors.Hand, Margin = new Thickness(0, 0, 10, 0) };
            cancelBtn.Click += (s, e) => { _flowCanvas.Children.Remove(popup); _viewVisualizationElements.Remove(popup); };

            var createBtn = new Button {
                Content = "CREATE", FontSize = 10, FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)),
                Background = Brushes.Transparent, BorderThickness = new Thickness(0), Cursor = Cursors.Hand
            };
            createBtn.Click += (s, e) => {
                string leftAlias = leftTableCombo.SelectedItem?.ToString() ?? "";
                string rightAlias = rightTableCombo.SelectedItem?.ToString() ?? "";
                if (string.IsNullOrEmpty(leftAlias) || string.IsNullOrEmpty(rightAlias) ||
                    string.IsNullOrEmpty(leftColBox.Text) || string.IsNullOrEmpty(rightColBox.Text))
                {
                    _viewModel.StatusMessage = "Fill in all fields to create a JOIN.";
                    return;
                }
                _flowCanvas.Children.Remove(popup); _viewVisualizationElements.Remove(popup);
                _viewModel.AddJoinRelationship(leftAlias, leftColBox.Text, rightAlias, rightColBox.Text, joinTypeCombo.SelectedItem?.ToString() ?? "INNER");
                RenderViewVisualization(viewDef);
            };

            btnPanel.Children.Add(cancelBtn);
            btnPanel.Children.Add(createBtn);
            panel.Children.Add(btnPanel);

            popup.Child = panel;
            Canvas.SetLeft(popup, x); Canvas.SetTop(popup, y);
            Panel.SetZIndex(popup, 9999);
            _flowCanvas.Children.Add(popup); _viewVisualizationElements.Add(popup);
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

        private string HslToRgbHex(double h, double s, double l)
        {
            double r, g, b;

            if (s == 0) r = g = b = l;
            else
            {
                double q = l < 0.5 ? l * (1 + s) : l + s - l * s;
                double p = 2 * l - q;
                r = HueToRgb(p, q, h / 360 + 1.0 / 3);
                g = HueToRgb(p, q, h / 360);
                b = HueToRgb(p, q, h / 360 - 1.0 / 3);
            }

            return string.Format("#{0:X2}{1:X2}{2:X2}", (int)(r * 255), (int)(g * 255), (int)(b * 255));
        }

        private double HueToRgb(double p, double q, double t)
        {
            if (t < 0) t += 1;
            if (t > 1) t -= 1;
            if (t < 1.0 / 6) return p + (q - p) * 6 * t;
            if (t < 1.2 / 2) return q;
            if (t < 2.0 / 3) return p + (q - p) * (2.0 / 3 - t) * 6;
            return p;
        }
    }
}
