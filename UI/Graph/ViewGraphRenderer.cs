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
using sqlSense.UI.Controls.TableCards;
using sqlSense.ViewModels;

namespace sqlSense.UI
{
    /// <summary>
    /// Core orchestrator for the view graph canvas.
    /// Partial classes handle specific concerns:
    ///   - ViewGraphRenderer.NodeFactory.cs    → Node creation (source table, join, query output)
    ///   - ViewGraphRenderer.JoinWorkflow.cs   → Join request handling, pickers, result cards
    ///   - ViewGraphRenderer.Connections.cs    → Connection creation, path updates, badges
    ///   - ViewGraphRenderer.Interaction.cs    → Node drag, hover, flow animations
    /// </summary>
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
        private readonly Dictionary<string, NodeCard> _joinResultNodes = new();
        private readonly NodeDataPreviewManager _previewManager;
        private static readonly Dictionary<string, Point> _nodePositionCache = new();


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

        // ═══════════════════════════════════════════════════════════════
        //  CANVAS ELEMENT MANAGEMENT
        // ═══════════════════════════════════════════════════════════════

        public void ClearViewVisualization()
        {
            foreach (var el in _viewVisualizationElements.ToList()) _flowCanvas.Children.Remove(el);
            _viewVisualizationElements.Clear();
            _nodeCards.Clear();
            _nodeConnections.Clear();
            _joinResultNodes.Clear();
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
            
            // Improved auto-layout starting point
            double startX = cx - hGap; 
            double startY = cy - (Math.Min(tableCount, 5) * 350) / 2;
            double currentY = startY;
            double currentX = startX;

            // ── 1. Create source table node cards ──
            var tableNodes = new Dictionary<string, NodeCard>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < tableCount; i++)
            {
                var refTbl = viewDef.ReferencedTables[i];
                string tableId = refTbl.Alias;
                
                double nodeX = currentX;
                double nodeY = currentY;

                // Restore from cache or model if available
                if (_nodePositionCache.TryGetValue(tableId, out Point cpSrc))
                {
                    nodeX = cpSrc.X;
                    nodeY = cpSrc.Y;
                }
                else if (viewDef.NodePositions.TryGetValue(tableId, out var pos))
                {
                    nodeX = pos.X;
                    nodeY = pos.Y;
                    _nodePositionCache[tableId] = new Point(nodeX, nodeY);
                }

                var node = CreateSourceTableNode(refTbl, cardW, nodeX, nodeY);
                node.ParticipatingTables.Add(refTbl.Alias);
                node.Color = "#3C3C3C"; // Standard border color

                // Only increment Y/X for auto-layout if we didn't use a cached position for THIS specific node
                if (!_nodePositionCache.ContainsKey(tableId))
                {
                    currentY += 400; 
                    if ((i + 1) % 5 == 0) // Wrap every 5 tables
                    {
                        currentY = startY;
                        currentX -= (cardW + 100);
                    }
                }

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
                    string joinId = $"Join_{join.LeftTableAlias}_{join.RightTableAlias}";

                    double jx, jy;
                    if (_nodePositionCache.TryGetValue(joinId, out Point cpJoin))
                    {
                        jx = cpJoin.X;
                        jy = cpJoin.Y;
                    }
                    else
                    {
                        double maxRight = Math.Max(leftNode.X + leftNode.Width, rightNode.X + rightNode.Width);
                        jx = maxRight + 150;
                        jy = (leftNode.Y + rightNode.Y) / 2;
                    }

                    var participating = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    participating.UnionWith(leftNode.ParticipatingTables);
                    participating.UnionWith(rightNode.ParticipatingTables);

                    var upstreamCols = viewDef.Columns
                        .Where(c => participating.Contains(c.SourceTable))
                        .Select(c => $"{c.SourceTable}.{c.SourceColumn}")
                        .ToList();

                    var joinCard = CreateJoinNode(join, 220, jx, jy, upstreamCols);
                    joinCard.Id = joinId; // Use consistent ID for caching
                    joinCard.LayoutLevel = newLvl;
                    joinCard.ParticipatingTables = participating;
                    joinCard.JoinData = join;

                    CreateNodeConnection(leftNode, joinCard, null, ConnectionColor);
                    CreateNodeConnection(rightNode, joinCard, null, ConnectionColor);

                    var keysToUpdate = activeOutputNode.Where(k => k.Value == leftNode || k.Value == rightNode).Select(k => k.Key).ToList();
                    foreach (var k in keysToUpdate) activeOutputNode[k] = joinCard;
                }
            }

            /*
            // ── 3. Create view output node card ──
            double maxOutputX = startX;
            foreach (var n in activeOutputNode.Values) {
                if (n.X + n.Width > maxOutputX) maxOutputX = n.X + n.Width;
            }
            double viewCardX = maxOutputX + 150;
            double viewCardY = cy - 150;

            if (_nodePositionCache.TryGetValue("VIEW_OUTPUT", out Point cpOut))
            {
                viewCardX = cpOut.X;
                viewCardY = cpOut.Y;
            }

            var viewNode = CreateQueryOutputNode(viewDef, cardW + 60, viewCardX, viewCardY);
            viewNode.Id = "VIEW_OUTPUT";

            foreach (var finalNode in activeOutputNode.Values.Distinct())
            {
                CreateNodeConnection(finalNode, viewNode, null, ConnectionColor);
            }
            */
            
            AnimateAllElements();
        }

        public void AutoArrange()
        {
            _nodePositionCache.Clear();
            if (_viewModel.Canvas.CurrentViewDefinition != null)
                RenderViewVisualization(_viewModel.Canvas.CurrentViewDefinition);
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
            AddItemToCanvasWithDrag(popup, "TablePicker_Main", cx - (popup.DesiredSize.Width / 2), cy - (popup.DesiredSize.Height / 2));
        }

        // ═══════════════════════════════════════════════════════════════
        //  CREATE TABLE (in-canvas card)
        // ═══════════════════════════════════════════════════════════════

        internal void ShowCreateTableOnCanvas()
        {
            if (_viewModel.DbService == null)
            {
                _viewModel.StatusMessage = "Connect to a database first.";
                return;
            }

            // Ensure we have a workspace / view definition
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

            double zoom = _viewModel.Canvas.Zoom;
            double cx = (_canvasContainer.ActualWidth / 2 - _canvasTranslate.X) / zoom;
            double cy = (_canvasContainer.ActualHeight / 2 - _canvasTranslate.Y) / zoom;

            var card = new CreateTableCard();

            // Wrap in a border so AddItemToCanvasWithDrag can manage it
            var wrapper = new Border
            {
                Child = card,
                Background = System.Windows.Media.Brushes.Transparent,
                CornerRadius = new CornerRadius(0)
            };

            card.OnCreateRequested += async (script, tableName, schemaName) =>
            {
                try
                {
                    _viewModel.StatusMessage = $"Creating table {tableName}...";
                    await _viewModel.DbService!.ExecuteNonQueryAsync(script, _viewModel.Explorer.SelectedDatabaseName);

                    _viewModel.StatusMessage = $"✓ Table {tableName} created successfully.";
                    await _viewModel.LoadDatabaseTreeAsync(); // Refresh Explorer

                    // Remove the card from canvas
                    _flowCanvas.Children.Remove(wrapper);
                    _viewVisualizationElements.Remove(wrapper);

                    // Add the new table to the canvas view
                    await _viewModel.AddTableToViewAsync(schemaName, tableName);
                    if (_viewModel.Canvas.CurrentViewDefinition != null)
                        RenderViewVisualization(_viewModel.Canvas.CurrentViewDefinition);
                }
                catch (Exception ex)
                {
                    _viewModel.StatusMessage = $"Creation Error: {ex.Message}";
                }
            };

            card.OnCancelled += () =>
            {
                _flowCanvas.Children.Remove(wrapper);
                _viewVisualizationElements.Remove(wrapper);
            };

            wrapper.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            AddItemToCanvasWithDrag(wrapper, "CreateTable_Card", cx - 220, cy - 200);
        }
    }
}
