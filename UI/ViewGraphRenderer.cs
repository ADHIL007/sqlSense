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
using sqlSense.UI.Controls;
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
        public bool IsGlobalDataFlowEnabled { get; private set; } = true;
        private NodeCard? _hoveredNode = null;

        // Standard theme colors
        private static readonly Color StandardBorder = Color.FromRgb(0x3C, 0x3C, 0x3C);
        private static readonly Color StandardAccent = Color.FromRgb(0x4F, 0xC3, 0xF7);
        private static readonly Color ConnectionColor = Color.FromRgb(0x88, 0x88, 0x88);

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

        // ═══════════════════════════════════════════════════════════════
        //  MAIN RENDER
        // ═══════════════════════════════════════════════════════════════

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
            double currentY = cy - (tableCount * 300) / 2;

            // ── 1. Create source table node cards ──
            var tableNodes = new Dictionary<string, NodeCard>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < tableCount; i++)
            {
                var refTbl = viewDef.ReferencedTables[i];
                var node = CreateSourceTableNode(refTbl, cardW, gridStartX, currentY);
                node.ParticipatingTables.Add(refTbl.Alias);
                node.Color = "#3C3C3C"; // Standard border color

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

                    CreateNodeConnection(leftNode, joinCard, null, ConnectionColor);
                    CreateNodeConnection(rightNode, joinCard, null, ConnectionColor);

                    var keysToUpdate = activeOutputNode.Where(k => k.Value == leftNode || k.Value == rightNode).Select(k => k.Key).ToList();
                    foreach (var k in keysToUpdate) activeOutputNode[k] = joinCard;
                }
            }

            // ── 3. Create view output node card ──
            // The user requested not to show the New view popup.
            /*
            double maxOutputX = gridStartX;
            foreach (var n in activeOutputNode.Values) {
                if (n.X + n.Width > maxOutputX) maxOutputX = n.X + n.Width;
            }
            double viewCardX = maxOutputX + 150;
            double viewCardY = cy - 150;
            var viewNode = CreateQueryOutputNode(viewDef, cardW + 60, viewCardX, viewCardY);

            foreach (var finalNode in activeOutputNode.Values.Distinct())
            {
                CreateNodeConnection(finalNode, viewNode, null, ConnectionColor);
            }
            */

            AnimateAllElements();
        }

        public void ToggleDataFlowState()
        {
            IsGlobalDataFlowEnabled = !IsGlobalDataFlowEnabled;
            // Force re-render of nodes so they get the updated mode flag
            if (_viewModel.Canvas.CurrentViewDefinition != null)
                RenderViewVisualization(_viewModel.Canvas.CurrentViewDefinition);
            else
                UpdateAllFlowAnimations();
        }

        // ═══════════════════════════════════════════════════════════════
        //  NODE CREATION (using TableCardFactory)
        // ═══════════════════════════════════════════════════════════════

        private NodeCard CreateSourceTableNode(ReferencedTable refTbl, double width, double x, double y)
        {
            var allCols = _viewModel!.Canvas.CurrentViewDefinition!.SourceTableAllColumns
                .GetValueOrDefault(refTbl.FullName, new List<string>());

            var card = TableCardFactory.CreateTableCard(
                refTbl, allCols, width,
                onDelete: (tbl) => {
                    _viewModel.Canvas.CurrentViewDefinition!.ReferencedTables.Remove(tbl);
                    _viewModel.Canvas.CurrentViewDefinition.Columns.RemoveAll(c => c.SourceTable == tbl.Alias);
                    RenderViewVisualization(_viewModel.Canvas.CurrentViewDefinition);
                },
                onColumnToggle: (col, tbl) => {
                    if (!tbl.UsedColumns.Contains(col)) {
                        tbl.UsedColumns.Add(col);
                        _viewModel.Canvas.CurrentViewDefinition!.Columns.Add(
                            new ViewColumnInfo { SourceTable = tbl.Alias, SourceColumn = col, ColumnName = col });
                    } else {
                        tbl.UsedColumns.Remove(col);
                        _viewModel.Canvas.CurrentViewDefinition!.Columns.RemoveAll(
                            c => c.SourceTable == tbl.Alias && c.SourceColumn == col);
                    }
                    RenderViewVisualization(_viewModel.Canvas.CurrentViewDefinition!);
                },
                onJoinRequested: (sourceTbl) => {
                    HandleJoinRequest(sourceTbl, x + width + 20, y);
                },
                isDataFlowMode: IsGlobalDataFlowEnabled
            );

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

            if (IsGlobalDataFlowEnabled && _viewModel?.DbService != null)
            {
                var viewDef = _viewModel.Canvas.CurrentViewDefinition;
                if (viewDef != null)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var dt = await _viewModel.DbService.GetTableDataAsync(viewDef.DatabaseName, refTbl.Schema, refTbl.Name);
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                var wrapper = (Grid)card.Child;
                                if (wrapper.Children.Count > 0 && wrapper.Children[0] is sqlSense.UI.Controls.TablePreviewCard previewCard)
                                {
                                    if (previewCard.DataContext is sqlSense.ViewModels.Modules.TablePreviewViewModel vm)
                                    {
                                        vm.TableData = dt;
                                        vm.CurrentPage = 1;
                                        vm.TotalPages = Math.Max(1, (int)Math.Ceiling((double)dt.Rows.Count / 5.0));
                                        vm.UpdatePagedData();
                                    }
                                }
                            });
                        }
                        catch { }
                    });
                }
            }

            return node;
        }

        private NodeCard CreateJoinNode(JoinRelationship join, double width, double x, double y, List<string> upstreamCols)
        {
            var card = TableCardFactory.CreateJoinCard(
                join, width, upstreamCols,
                onDelete: () => {
                    _viewModel!.Canvas.CurrentViewDefinition!.Joins.Remove(join);
                    RenderViewVisualization(_viewModel.Canvas.CurrentViewDefinition);
                },
                onChangeType: () => {
                    join.JoinType = join.JoinType.ToUpper() switch {
                        "INNER" => "LEFT", "LEFT" => "RIGHT", "RIGHT" => "FULL", _ => "INNER"
                    };
                    RenderViewVisualization(_viewModel!.Canvas.CurrentViewDefinition!);
                },
                onLeftChanged: (alias, col) => { join.LeftTableAlias = alias; join.LeftColumn = col; },
                onRightChanged: (alias, col) => { join.RightTableAlias = alias; join.RightColumn = col; }
            );

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

        private NodeCard CreateQueryOutputNode(ViewDefinitionInfo viewDef, double width, double x, double y)
        {
            var card = TableCardFactory.CreateQueryOutputCard(
                viewDef, width,
                onPreviewSql: () => {
                    _viewModel.SqlEditor.SqlText = viewDef.ToSql();
                    _viewModel.StatusMessage = "SQL preview updated from canvas.";
                },
                onRender: () => RenderViewVisualization(viewDef),
                isDataFlowMode: IsGlobalDataFlowEnabled
            );

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

        // ═══════════════════════════════════════════════════════════════
        //  JOIN WORKFLOW
        // ═══════════════════════════════════════════════════════════════

        private void HandleJoinRequest(ReferencedTable sourceTable, double popupX, double popupY)
        {
            var viewDef = _viewModel.Canvas.CurrentViewDefinition;
            if (viewDef == null || viewDef.ReferencedTables.Count < 2)
            {
                _viewModel.StatusMessage = "Need at least 2 tables to create a JOIN.";
                return;
            }

            // Show a popup to select join type + target table
            ShowJoinPickerPopup(sourceTable, viewDef, popupX, popupY);
        }

        private void ShowJoinPickerPopup(ReferencedTable sourceTable, ViewDefinitionInfo viewDef, double x, double y)
        {
            Border? popup = null;
            popup = TableCardFactory.CreateJoinTypePicker(
                sourceTable,
                viewDef.ReferencedTables,
                onJoinTypeSelected: (joinType, targetTable) => {
                    _flowCanvas.Children.Remove(popup);
                    _viewVisualizationElements.Remove(popup!);
                    ShowColumnPickerForJoin(sourceTable, targetTable, joinType, viewDef, x, y);
                },
                onCancel: () => {
                    _flowCanvas.Children.Remove(popup);
                    _viewVisualizationElements.Remove(popup!);
                }
            );

            Canvas.SetLeft(popup, x); Canvas.SetTop(popup, y);
            Panel.SetZIndex(popup, 9999);
            _flowCanvas.Children.Add(popup); _viewVisualizationElements.Add(popup);
        }

        private void ShowColumnPickerForJoin(ReferencedTable leftTable, ReferencedTable rightTable, string joinType, ViewDefinitionInfo viewDef, double x, double y)
        {
            // Find common column names as a hint
            var leftCols = viewDef.SourceTableAllColumns.GetValueOrDefault(leftTable.FullName, new List<string>());
            var rightCols = viewDef.SourceTableAllColumns.GetValueOrDefault(rightTable.FullName, new List<string>());
            var commonCols = leftCols.Intersect(rightCols, StringComparer.OrdinalIgnoreCase).ToList();

            // Auto-pick the first common column, or ask user
            string leftCol = commonCols.FirstOrDefault() ?? (leftCols.FirstOrDefault() ?? "Id");
            string rightCol = commonCols.FirstOrDefault() ?? (rightCols.FirstOrDefault() ?? "Id");

            _viewModel.AddJoinRelationship(leftTable.Alias, leftCol, rightTable.Alias, rightCol, joinType);
            RenderViewVisualization(viewDef);
            _viewModel.StatusMessage = $"Added {joinType} JOIN: {leftTable.Alias}.{leftCol} = {rightTable.Alias}.{rightCol}";
        }

        // ═══════════════════════════════════════════════════════════════
        //  ADD TABLE (from toolbar)
        // ═══════════════════════════════════════════════════════════════

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

            Border? popup = null;
            popup = TableCardFactory.CreateTablePicker(
                tables,
                _viewModel.Canvas.CurrentViewDefinition.ReferencedTables,
                onSelected: async (tableInfo) => {
                    _flowCanvas.Children.Remove(popup);
                    _viewVisualizationElements.Remove(popup!);
                    await _viewModel.AddTableToViewAsync(tableInfo.Schema, tableInfo.Name);
                    RenderViewVisualization(_viewModel.Canvas.CurrentViewDefinition);
                },
                onCancel: () => {
                    _flowCanvas.Children.Remove(popup);
                    _viewVisualizationElements.Remove(popup!);
                }
            );

            popup.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            AddItem(popup, cx - (popup.DesiredSize.Width / 2), cy - (popup.DesiredSize.Height / 2));
        }

        // ═══════════════════════════════════════════════════════════════
        //  FLOW ANIMATIONS
        // ═══════════════════════════════════════════════════════════════

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
                conn.PathElement.Opacity = isActive ? 1.0 : 0.3;
                conn.PathElement.StrokeThickness = isActive ? 2.5 : 1.5;
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
