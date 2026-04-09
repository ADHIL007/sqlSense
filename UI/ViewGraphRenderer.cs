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
    public partial class ViewGraphRenderer
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
            foreach (var el in _viewVisualizationElements.ToList()) _flowCanvas.Children.Remove(el);
            _viewVisualizationElements.Clear();
            _nodeCards.Clear();
            _nodeConnections.Clear();
        }

        public void AddItem(UIElement element, double x, double y, bool trackInVisualization = true)
        {
            Canvas.SetLeft(element, x);
            Canvas.SetTop(element, y);
            Panel.SetZIndex(element, 1000); 
            _flowCanvas.Children.Add(element);
            if (trackInVisualization)
                _viewVisualizationElements.Add(element);
        }

        public void RenderViewVisualization(ViewDefinitionInfo viewDef)
        {
            ClearViewVisualization();

            double zoom = _viewModel.Canvas.Zoom;
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
                
                // Dynamic HSL Color Generation
                double hue = (i * 137.5) % 360; 
                string colorStr = CanvasUIFactory.HslToRgbHex(hue, 0.65, 0.5); 
                
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
            foreach (var join in viewDef.Joins)
            {
                NodeCard? leftNode = null, rightNode = null;
                activeOutputNode.TryGetValue(join.LeftTableAlias, out leftNode);
                activeOutputNode.TryGetValue(join.RightTableAlias, out rightNode);

                if (leftNode != null && rightNode != null)
                {
                    int newLvl = Math.Max(leftNode.LayoutLevel, rightNode.LayoutLevel) + 1;
                    double maxRight = Math.Max(leftNode.X + leftNode.Width, rightNode.X + rightNode.Width);
                    double jx = maxRight + 150;
                    double jy = (leftNode.Y + rightNode.Y) / 2;

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

                    CreateNodeConnection(leftNode, joinCard, null, (Color)ColorConverter.ConvertFromString(leftNode.Color));
                    CreateNodeConnection(rightNode, joinCard, null, (Color)ColorConverter.ConvertFromString(rightNode.Color));

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

            foreach (var finalNode in activeOutputNode.Values.Distinct())
            {
                var finalColor = (Color)ColorConverter.ConvertFromString(finalNode.Color);
                CreateNodeConnection(finalNode, viewNode, null, finalColor);
            }

            // ── 4. Add Table button (below source tables) ──
            var addTableBtn = CanvasUIFactory.CreateActionButton("\uE710", "ADD TABLE", Color.FromRgb(0x4C, 0xAF, 0x50));
            Canvas.SetLeft(addTableBtn, gridStartX); Canvas.SetTop(addTableBtn, currentY + 20);
            Panel.SetZIndex(addTableBtn, 5000);
            _flowCanvas.Children.Add(addTableBtn); _viewVisualizationElements.Add(addTableBtn);
            addTableBtn.PreviewMouseLeftButtonDown += async (s, e) => {
                e.Handled = true;
                if (_viewModel.Canvas.DbService == null || viewDef == null) return;
                await ShowAddTablePopup(viewDef, gridStartX, currentY + 60);
            };

            // ── 5. Add Join button (below add table) ──
            var addJoinBtn = CanvasUIFactory.CreateActionButton("\uE710", "ADD JOIN", Color.FromRgb(0xFF, 0xB7, 0x4D));
            Canvas.SetLeft(addJoinBtn, gridStartX); Canvas.SetTop(addJoinBtn, currentY + 62);
            Panel.SetZIndex(addJoinBtn, 5000);
            _flowCanvas.Children.Add(addJoinBtn); _viewVisualizationElements.Add(addJoinBtn);
            addJoinBtn.PreviewMouseLeftButtonDown += (s, e) => {
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
            var headerDock = new DockPanel();
            var delJoinBtn = new Button {
                Content = "\uE711", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 10,
                Background = Brushes.Transparent, BorderThickness = new Thickness(0),
                Foreground = new SolidColorBrush(Color.FromArgb(0x88, 0xFF, 0x44, 0x44)),
                Cursor = Cursors.Hand, ToolTip = "Remove this JOIN", VerticalAlignment = VerticalAlignment.Center
            };
            delJoinBtn.Click += (s, e) => {
                _viewModel!.Canvas.CurrentViewDefinition!.Joins.Remove(join);
                RenderViewVisualization(_viewModel.Canvas.CurrentViewDefinition);
            };
            DockPanel.SetDock(delJoinBtn, Dock.Right);
            headerDock.Children.Add(delJoinBtn);
            headerDock.Children.Add(new TextBlock { Text = $"{join.JoinType} JOIN", Foreground = new SolidColorBrush(color), FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center });

            var header = new Border {
                Background = new SolidColorBrush(Color.FromArgb(0x40, color.R, color.G, color.B)),
                BorderBrush = new SolidColorBrush(color), BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(10, 6, 10, 6), CornerRadius = new CornerRadius(8, 8, 0, 0),
                Child = headerDock
            };

            var body = new StackPanel { Margin = new Thickness(10) };
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

            var changeBtn = new Button {
                Content = "⟳ CHANGE TYPE", FontSize = 9, Foreground = new SolidColorBrush(color),
                Background = Brushes.Transparent, BorderThickness = new Thickness(0),
                Margin = new Thickness(0, 8, 0, 6), Cursor = Cursors.Hand
            };
            changeBtn.Click += (s, e) => {
                join.JoinType = join.JoinType.ToUpper() switch { "INNER" => "LEFT", "LEFT" => "RIGHT", "RIGHT" => "FULL", _ => "INNER" };
                RenderViewVisualization(_viewModel!.Canvas.CurrentViewDefinition!);
            };
            body.Children.Add(changeBtn);

            if (upstreamCols.Any())
            {
                var flowBorder = new Border { Background = new SolidColorBrush(Color.FromArgb(0x10, 255, 255, 255)), Margin = new Thickness(-10, 10, -10, -10), CornerRadius = new CornerRadius(0, 0, 8, 8) };
                var flowPanel = new StackPanel();
                flowPanel.Children.Add(new TextBlock { Text = "SELECTED FIELDS", FontSize = 9, Foreground = Brushes.Gray, Margin = new Thickness(12, 10, 12, 5), FontWeight = FontWeights.Bold });
                foreach (var c in upstreamCols)
                {
                    var rowPanel = new Border {
                        Background = new SolidColorBrush(Color.FromArgb(0x20, 0x03, 0x9B, 0xE5)),
                        BorderThickness = new Thickness(3, 0, 0, 1), BorderBrush = Brushes.DeepSkyBlue,
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
                BorderBrush = new SolidColorBrush(color), BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(8), MinWidth = width, 
                Effect = new DropShadowEffect { BlurRadius = 15, Opacity = 0.3, Color = color },
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

        private void UpdateAllFlowAnimations()
        {
            var activeConns = new HashSet<NodeConnection>();
            if (!IsGlobalDataFlowEnabled && _hoveredNode != null)
            {
                var seen = new HashSet<NodeCard>();
                var q = new Queue<NodeCard>();
                q.Enqueue(_hoveredNode); seen.Add(_hoveredNode);
                while(q.Count > 0) {
                    var cur = q.Dequeue();
                    foreach (var c in cur.OutputConnections) { activeConns.Add(c); if (seen.Add(c.Target)) q.Enqueue(c.Target); }
                }
                q.Clear(); q.Enqueue(_hoveredNode);
                while(q.Count > 0) {
                    var cur = q.Dequeue();
                    foreach (var c in cur.InputConnections) { activeConns.Add(c); if (seen.Add(c.Source)) q.Enqueue(c.Source); }
                }
            }

            foreach (var conn in _nodeConnections)
            {
                bool isActive = IsGlobalDataFlowEnabled || activeConns.Contains(conn);
                conn.PathElement.Opacity = isActive ? 1.0 : 0.2;
                conn.PathElement.StrokeThickness = isActive ? 3.5 : 2.5;
                if (isActive && !conn.IsAnimating) {
                    conn.FlowPathElement.Visibility = Visibility.Visible;
                    var anim = new DoubleAnimation(16, 0, new Duration(TimeSpan.FromSeconds(0.4))) { RepeatBehavior = RepeatBehavior.Forever };
                    conn.FlowPathElement.BeginAnimation(Shape.StrokeDashOffsetProperty, anim);
                    conn.IsAnimating = true;
                } else if (!isActive && conn.IsAnimating) {
                    conn.FlowPathElement.BeginAnimation(Shape.StrokeDashOffsetProperty, null);
                    conn.FlowPathElement.Visibility = Visibility.Hidden;
                    conn.IsAnimating = false;
                }
            }
        }

        private async Task ShowAddTablePopup(ViewDefinitionInfo viewDef, double x, double y)
        {
            if (_viewModel.Canvas.DbService == null) return;
            var tables = await _viewModel.Canvas.DbService.GetAllTablesForDatabaseAsync(viewDef.DatabaseName);
            var popup = new Border {
                Background = new SolidColorBrush(Color.FromRgb(0x25, 0x25, 0x28)), BorderBrush = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)),
                BorderThickness = new Thickness(1.5), CornerRadius = new CornerRadius(8), Padding = new Thickness(8), MinWidth = 260, MaxHeight = 350,
                Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 20, Opacity = 0.6 }
            };
            var panel = new StackPanel();
            panel.Children.Add(new TextBlock { Text = "SELECT A TABLE", FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)), FontSize = 10 });
            var scroll = new ScrollViewer { MaxHeight = 280, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var listPanel = new StackPanel();
            foreach (var t in tables) {
                if (viewDef.ReferencedTables.Any(r => r.Schema == t.Schema && r.Name == t.Name)) continue;
                
                var itemContent = new StackPanel { Orientation = Orientation.Horizontal };
                itemContent.Children.Add(new TextBlock { Text = "\uE80A", FontFamily = new FontFamily("Segoe MDL2 Assets"), Margin = new Thickness(0,0,10,0), VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.Gray });
                itemContent.Children.Add(new TextBlock { Text = $"{t.Schema}.{t.Name}", FontSize = 11, Foreground = Brushes.LightGray, VerticalAlignment = VerticalAlignment.Center });

                var item = new Button { 
                    Content = itemContent, 
                    Background = Brushes.Transparent, BorderThickness = new Thickness(0), Cursor = Cursors.Hand, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(8, 5, 8, 5) 
                };
                item.Click += async (s, e) => { _flowCanvas.Children.Remove(popup); _viewVisualizationElements.Remove(popup); await _viewModel.AddTableToViewAsync(t.Schema, t.Name); RenderViewVisualization(viewDef); };
                listPanel.Children.Add(item);
            }
            scroll.Content = listPanel; panel.Children.Add(scroll);
            var closeBtn = new Button { Content = "CANCEL", FontSize = 9, Foreground = Brushes.Gray, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Cursor = Cursors.Hand, HorizontalAlignment = HorizontalAlignment.Right };
            closeBtn.Click += (s, e) => { _flowCanvas.Children.Remove(popup); _viewVisualizationElements.Remove(popup); };
            panel.Children.Add(closeBtn); popup.Child = panel;
            Canvas.SetLeft(popup, x); Canvas.SetTop(popup, y); Panel.SetZIndex(popup, 9999);
            _flowCanvas.Children.Add(popup); _viewVisualizationElements.Add(popup);
        }

        private void ShowAddJoinPopup(ViewDefinitionInfo viewDef, double x, double y)
        {
            var popup = new Border {
                Background = new SolidColorBrush(Color.FromRgb(0x25, 0x25, 0x28)), BorderBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xB7, 0x4D)),
                BorderThickness = new Thickness(1.5), CornerRadius = new CornerRadius(8), Padding = new Thickness(12), MinWidth = 300,
                Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 20, Opacity = 0.6 }
            };
            var panel = new StackPanel();
            panel.Children.Add(new TextBlock { Text = "CREATE JOIN", FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xB7, 0x4D)), FontSize = 11 });
            var tableAliases = viewDef.ReferencedTables.Select(t => t.Alias).ToArray();
            var joinTypeCombo = new ComboBox { FontSize = 11, Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x22)), Foreground = Brushes.White };
            foreach (var jt in new[] { "INNER", "LEFT", "RIGHT", "FULL" }) joinTypeCombo.Items.Add(jt);
            joinTypeCombo.SelectedIndex = 0;
            panel.Children.Add(new TextBlock { Text = "Join Type", Foreground = Brushes.Gray, FontSize = 9 }); panel.Children.Add(joinTypeCombo);
            var leftTableCombo = new ComboBox { FontSize = 11, Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x22)), Foreground = Brushes.White };
            foreach (var a in tableAliases) leftTableCombo.Items.Add(a);
            if (tableAliases.Length > 0) leftTableCombo.SelectedIndex = 0;
            panel.Children.Add(new TextBlock { Text = "Left Table", Foreground = Brushes.Gray, FontSize = 9 }); panel.Children.Add(leftTableCombo);
            var leftColBox = new TextBox { Text = "", FontSize = 11, Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x22)), Foreground = Brushes.White };
            panel.Children.Add(new TextBlock { Text = "Left Column", Foreground = Brushes.Gray, FontSize = 9 }); panel.Children.Add(leftColBox);
            var rightTableCombo = new ComboBox { FontSize = 11, Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x22)), Foreground = Brushes.White };
            foreach (var a in tableAliases) rightTableCombo.Items.Add(a);
            if (tableAliases.Length > 1) rightTableCombo.SelectedIndex = 1;
            panel.Children.Add(new TextBlock { Text = "Right Table", Foreground = Brushes.Gray, FontSize = 9 }); panel.Children.Add(rightTableCombo);
            var rightColBox = new TextBox { Text = "", FontSize = 11, Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x22)), Foreground = Brushes.White };
            panel.Children.Add(new TextBlock { Text = "Right Column", Foreground = Brushes.Gray, FontSize = 9 }); panel.Children.Add(rightColBox);
            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var cancelBtn = new Button { Content = "CANCEL", FontSize = 9, Foreground = Brushes.Gray, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Cursor = Cursors.Hand, Margin = new Thickness(0, 0, 10, 0) };
            cancelBtn.Click += (s, e) => { _flowCanvas.Children.Remove(popup); _viewVisualizationElements.Remove(popup); };
            var createBtn = new Button { Content = "CREATE", FontSize = 10, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)), Background = Brushes.Transparent, BorderThickness = new Thickness(0), Cursor = Cursors.Hand };
            createBtn.Click += (s, e) => {
                string leftAlias = leftTableCombo.SelectedItem?.ToString() ?? "";
                string rightAlias = rightTableCombo.SelectedItem?.ToString() ?? "";
                if (string.IsNullOrEmpty(leftAlias) || string.IsNullOrEmpty(rightAlias) || string.IsNullOrEmpty(leftColBox.Text) || string.IsNullOrEmpty(rightColBox.Text)) return;
                _flowCanvas.Children.Remove(popup); _viewVisualizationElements.Remove(popup);
                _viewModel.AddJoinRelationship(leftAlias, leftColBox.Text, rightAlias, rightColBox.Text, joinTypeCombo.SelectedItem?.ToString() ?? "INNER"); RenderViewVisualization(viewDef);
            };
            btnPanel.Children.Add(cancelBtn); btnPanel.Children.Add(createBtn);
            panel.Children.Add(btnPanel); popup.Child = panel;
            Canvas.SetLeft(popup, x); Canvas.SetTop(popup, y); Panel.SetZIndex(popup, 9999);
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

        internal async void AddTableCardAtCenter()
        {
            if (_viewModel.DbService == null) return;

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

            var tables = await _viewModel.DbService.GetAllTablesForDatabaseAsync(_viewModel.Canvas.CurrentViewDefinition.DatabaseName);

            double zoom = _viewModel.Canvas.Zoom;
            double cx = (_canvasContainer.ActualWidth / 2 - _canvasTranslate.X) / zoom;
            double cy = (_canvasContainer.ActualHeight / 2 - _canvasTranslate.Y) / zoom;

            var popup = new Border {
                Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x22)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x00, 0x7A, 0xCC)),
                BorderThickness = new Thickness(1.5), CornerRadius = new CornerRadius(12),
                Padding = new Thickness(0), Width = 320, MaxHeight = 450,
                Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 40, Opacity = 0.5 }
            };

            var mainPanel = new StackPanel();

            // ── Header ──
            var header = new Border {
                Background = new SolidColorBrush(Color.FromRgb(0x00, 0x7A, 0xCC)),
                CornerRadius = new CornerRadius(11, 11, 0, 0), Padding = new Thickness(15, 10, 15, 10)
            };
            var headerContent = new DockPanel();
            var closeBtn = new Button { Content = "\uE711", FontFamily = new FontFamily("Segoe MDL2 Assets"), Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = Brushes.White, Cursor = Cursors.Hand };
            closeBtn.Click += (s, e) => { _flowCanvas.Children.Remove(popup); _viewVisualizationElements.Remove(popup); };
            DockPanel.SetDock(closeBtn, Dock.Right);
            headerContent.Children.Add(closeBtn);
            headerContent.Children.Add(new TextBlock { Text = "ADD TO CANVAS", FontWeight = FontWeights.Bold, Foreground = Brushes.White, FontSize = 12 });
            header.Child = headerContent;
            mainPanel.Children.Add(header);

            // ── Create New Table Row ──
            var createNewBtn = new Button {
                Background = new SolidColorBrush(Color.FromArgb(0x15, 0x4C, 0xAF, 0x50)),
                Padding = new Thickness(15, 12, 15, 12), Cursor = Cursors.Hand, HorizontalContentAlignment = HorizontalAlignment.Left
            };
            var createNewContent = new StackPanel { Orientation = Orientation.Horizontal };
            createNewContent.Children.Add(new TextBlock { Text = "\uE710", FontFamily = new FontFamily("Segoe MDL2 Assets"), Foreground = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)), Margin = new Thickness(0,0,10,0), VerticalAlignment = VerticalAlignment.Center });
            createNewContent.Children.Add(new TextBlock { Text = "Create New Table", Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center });
            createNewBtn.Content = createNewContent;
            createNewBtn.Click += (s, e) => _viewModel.StatusMessage = "Feature coming soon: Visual Table Designer";
            mainPanel.Children.Add(createNewBtn);

            // ── Search Bar ──
            var searchContainer = new Border { Background = new SolidColorBrush(Color.FromArgb(0x30, 0, 0, 0)), Padding = new Thickness(10, 8, 10, 8) };
            var searchBox = new TextBox { Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = Brushes.White };
            var placeholder = new TextBlock { Text = "Search existing tables...", Foreground = Brushes.Gray, IsHitTestVisible = false };
            var searchGrid = new Grid(); searchGrid.Children.Add(searchBox); searchGrid.Children.Add(placeholder);
            searchContainer.Child = searchGrid; mainPanel.Children.Add(searchContainer);

            // ── Table List ──
            var listPanel = new StackPanel();
            var scroll = new ScrollViewer { MaxHeight = 250, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            scroll.Content = listPanel; mainPanel.Children.Add(scroll);

            void PopulateList(string filter = "") {
                listPanel.Children.Clear();
                foreach (var t in tables) {
                    string fullName = $"{t.Schema}.{t.Name}";
                    if (!string.IsNullOrEmpty(filter) && !fullName.Contains(filter, StringComparison.OrdinalIgnoreCase)) continue;
                    var itemBtn = new Button { Background = Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(15, 8, 15, 8), Cursor = Cursors.Hand, HorizontalContentAlignment = HorizontalAlignment.Left };
                    var itemContent = new StackPanel { Orientation = Orientation.Horizontal };
                    itemContent.Children.Add(new TextBlock { Text = "\uE80A", FontFamily = new FontFamily("Segoe MDL2 Assets"), Foreground = Brushes.Gray, Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center });
                    itemContent.Children.Add(new TextBlock { Text = fullName, Foreground = Brushes.LightGray, VerticalAlignment = VerticalAlignment.Center });
                    itemBtn.Content = itemContent;
                    itemBtn.Click += async (sList, eArgs) => { 
                        _flowCanvas.Children.Remove(popup); _viewVisualizationElements.Remove(popup);
                        await _viewModel.AddTableToViewAsync(t.Schema, t.Name); RenderViewVisualization(_viewModel.Canvas.CurrentViewDefinition);
                    };
                    listPanel.Children.Add(itemBtn);
                }
            }
            PopulateList();
            searchBox.TextChanged += (s, e) => {
                placeholder.Visibility = string.IsNullOrEmpty(searchBox.Text) ? Visibility.Visible : Visibility.Collapsed;
                PopulateList(searchBox.Text);
            };

            popup.Child = mainPanel;
            popup.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            AddItem(popup, cx - (popup.DesiredSize.Width / 2), cy - (popup.DesiredSize.Height / 2));
        }
    }
}
