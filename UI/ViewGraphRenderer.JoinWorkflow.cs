using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using sqlSense.Models;
using sqlSense.UI.Controls;
using sqlSense.ViewModels;

namespace sqlSense.UI
{
    /// <summary>
    /// Partial class for ViewGraphRenderer: join workflow orchestration
    /// including join type picker, table picker, column picker, and join result cards.
    /// </summary>
    public partial class ViewGraphRenderer
    {
        // ═══════════════════════════════════════════════════════════════
        //  JOIN WORKFLOW
        // ═══════════════════════════════════════════════════════════════

        private void HandleJoinRequest(ReferencedTable sourceTable, double popupX, double popupY)
        {
            var viewDef = _viewModel.Canvas.CurrentViewDefinition;
            if (viewDef == null) return;

            // Show a popup to select join type first
            ShowJoinPickerPopup(sourceTable, viewDef, popupX, popupY);
        }

        private void AddItemToCanvasWithDrag(Border element, string id, double x, double y)
        {
            element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(element, x);
            Canvas.SetTop(element, y);
            Panel.SetZIndex(element, 9999);
            
            _flowCanvas.Children.Add(element);
            _viewVisualizationElements.Add(element);

            // Create a temporary NodeCard to enable drag behavior
            var tempNode = new NodeCard
            {
                Id = id,
                CardElement = element,
                X = x,
                Y = y,
                Width = element.DesiredSize.Width,
                Height = element.DesiredSize.Height
            };
            
            SetupNodeDrag(element, tempNode);
        }

        private void ShowJoinPickerPopup(ReferencedTable sourceTable, ViewDefinitionInfo viewDef, double x, double y)
        {
            Border? popup = null;
            popup = TableCardFactory.CreateJoinOptionsPicker(
                sourceTable,
                onJoinTypeSelected: async (joinType) => {
                    _flowCanvas.Children.Remove(popup);
                    _viewVisualizationElements.Remove(popup!);

                    if (_viewModel.DbService == null) return;
                    var tables = await _viewModel.DbService.GetAllTablesForDatabaseAsync(viewDef.DatabaseName);

                    Border? tablePopup = null;
                    tablePopup = TableCardFactory.CreateTablePicker(
                        tables,
                        viewDef.ReferencedTables.Where(t => t != sourceTable),
                        onSelected: async (tableInfo) => {
                            _flowCanvas.Children.Remove(tablePopup);
                            _viewVisualizationElements.Remove(tablePopup!);
                            
                            var targetTbl = viewDef.ReferencedTables.FirstOrDefault(t => t.Schema == tableInfo.Schema && t.Name == tableInfo.Name);
                            if (targetTbl == null)
                            {
                                await _viewModel.AddTableToViewAsync(tableInfo.Schema, tableInfo.Name);
                                targetTbl = viewDef.ReferencedTables.Last();
                            }

                            ShowColumnPickerForJoin(sourceTable, targetTbl, joinType, viewDef, x, y);
                        },
                        onCancel: () => {
                            _flowCanvas.Children.Remove(tablePopup);
                            _viewVisualizationElements.Remove(tablePopup!);
                        }
                    );
                    
                    AddItemToCanvasWithDrag(tablePopup, "TablePicker_Temp", x, y);
                },
                onCancel: () => {
                    _flowCanvas.Children.Remove(popup);
                    _viewVisualizationElements.Remove(popup!);
                }
            );

            AddItemToCanvasWithDrag(popup, "JoinTypePicker_Temp", x, y);
        }

        private void ShowColumnPickerForJoin(ReferencedTable leftTable, ReferencedTable rightTable, string joinType, ViewDefinitionInfo viewDef, double x, double y)
        {
            var leftCols = viewDef.SourceTableAllColumns.GetValueOrDefault(leftTable.FullName, new List<string>());
            var rightCols = viewDef.SourceTableAllColumns.GetValueOrDefault(rightTable.FullName, new List<string>());
            var commonCols = leftCols.Intersect(rightCols, StringComparer.OrdinalIgnoreCase).ToList();

            string leftCol = commonCols.FirstOrDefault() ?? (leftCols.FirstOrDefault() ?? "Id");
            string rightCol = commonCols.FirstOrDefault() ?? (rightCols.FirstOrDefault() ?? "Id");

            Border? popup = null;
            popup = TableCardFactory.CreateJoinColumnPicker(
                leftTable, rightTable, leftCols, rightCols, leftCol, rightCol,
                onConfirmed: (acceptedLeftCol, acceptedRightCol) => {
                    _flowCanvas.Children.Remove(popup);
                    _viewVisualizationElements.Remove(popup!);

                    _viewModel.AddJoinRelationship(leftTable.Alias, acceptedLeftCol, rightTable.Alias, acceptedRightCol, joinType);
                    RenderViewVisualization(viewDef);
                    _viewModel.StatusMessage = $"Added {joinType} JOIN: {leftTable.Alias}.{acceptedLeftCol} = {rightTable.Alias}.{acceptedRightCol}";
                },
                onCancel: () => {
                    _flowCanvas.Children.Remove(popup);
                    _viewVisualizationElements.Remove(popup!);
                }
            );

            AddItemToCanvasWithDrag(popup, "JoinColumnPicker_Temp", x, y);
        }

        // ═══════════════════════════════════════════════════════════════
        //  JOIN RESULT CARD
        // ═══════════════════════════════════════════════════════════════

        private async void SpawnJoinResultCard(NodeCard joinNode)
        {
            var viewDef = _viewModel.Canvas.CurrentViewDefinition;
            if (viewDef == null || _viewModel.DbService == null || joinNode.JoinData == null) return;

            // Toggle: if already showing a result card for this join, remove it
            if (_joinResultNodes.TryGetValue(joinNode.Id, out var existingResult))
            {
                RemoveNodeAndConnections(existingResult);
                _joinResultNodes.Remove(joinNode.Id);
                return;
            }

            // Use the right table of the join as the source for further join requests
            var rightTable = viewDef.ReferencedTables.FirstOrDefault(
                t => string.Equals(t.Alias, joinNode.JoinData.RightTableAlias, StringComparison.OrdinalIgnoreCase));

            NodeCard? resultNode = null;
            Border? resultCard = null;

            resultCard = TableCardFactory.CreateJoinResultCard(
                $"{joinNode.JoinData.JoinType} JOIN Result",
                320,
                onClose: () =>
                {
                    if (resultNode != null)
                    {
                        RemoveNodeAndConnections(resultNode);
                        _joinResultNodes.Remove(joinNode.Id);
                    }
                },
                onJoinRequested: rightTable != null ? (sourceTbl) =>
                {
                    double rx2 = (resultNode?.X ?? 0) + (resultNode?.Width ?? 320) + 20;
                    double ry2 = resultNode?.Y ?? 0;
                    HandleJoinRequest(sourceTbl, rx2, ry2);
                } : null,
                joinSourceTable: rightTable
            );

            // Position to the right of the join card
            double rx = joinNode.X + joinNode.Width + 80;
            double ry = joinNode.Y;

            if (_nodePositionCache.TryGetValue($"JoinResult_{joinNode.Id}", out var cachedPos))
            {
                rx = cachedPos.X;
                ry = cachedPos.Y;
            }

            Canvas.SetLeft(resultCard, rx);
            Canvas.SetTop(resultCard, ry);
            Panel.SetZIndex(resultCard, 1000);
            _flowCanvas.Children.Add(resultCard);
            _viewVisualizationElements.Add(resultCard);

            resultCard.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double w = Math.Max(320, resultCard.DesiredSize.Width);
            double h = Math.Max(200, resultCard.DesiredSize.Height);

            resultNode = new NodeCard
            {
                Id = $"JoinResult_{joinNode.Id}",
                CardElement = resultCard,
                X = rx, Y = ry, Width = w, Height = h,
                ParticipatingTables = new HashSet<string>(joinNode.ParticipatingTables, StringComparer.OrdinalIgnoreCase)
            };
            _nodeCards.Add(resultNode);
            SetupNodeDrag(resultCard, resultNode);
            _joinResultNodes[joinNode.Id] = resultNode;

            // Connect join node → result node
            CreateNodeConnection(joinNode, resultNode, null, ConnectionColor);

            resultCard.LayoutUpdated += (s, e) =>
            {
                resultNode.Width = resultCard.ActualWidth > 0 ? resultCard.ActualWidth : resultNode.Width;
                resultNode.Height = resultCard.ActualHeight > 0 ? resultCard.ActualHeight : resultNode.Height;
                foreach (var c in resultNode.OutputConnections) UpdateConnectionPath(c);
                foreach (var c in resultNode.InputConnections) UpdateConnectionPath(c);
            };

            // Animate in
            resultCard.Opacity = 0;
            resultCard.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300)));

            // Execute join query and load data
            try
            {
                string sql = sqlSense.Services.QueryBuilderService.BuildSqlForNode(joinNode, viewDef);
                var dt = await _viewModel.DbService.ExecuteQueryAsync(viewDef.DatabaseName, sql);

                var grid = (Grid)resultCard.Child;
                if (grid.Children[0] is TablePreviewCard previewCard &&
                    previewCard.DataContext is ViewModels.Modules.TablePreviewViewModel vm)
                {
                    vm.TableData = dt;
                    vm.CurrentPage = 1;
                    vm.TotalPages = Math.Max(1, (int)Math.Ceiling((double)dt.Rows.Count / 5.0));
                    
                    // Filter viewDef columns to those belonging to this join branch
                    var participating = joinNode.ParticipatingTables;
                    vm.UsedColumns = viewDef.Columns
                        .Where(c => participating.Contains(c.SourceTable))
                        .Select(c => c.SourceColumn)
                        .ToList();

                    vm.OnColumnToggle = (col) => {
                        // Find which table this column belongs to in this branch
                        // (Simplification: find first table that has this column used)
                        var targetTable = viewDef.ReferencedTables.FirstOrDefault(t => 
                            participating.Contains(t.Alias) && t.UsedColumns.Contains(col));
                        
                        // If we didn't find it in UsedColumns (maybe it was just unticked), 
                        // we'd need to check SourceTableAllColumns.
                        if (targetTable == null) {
                            targetTable = viewDef.ReferencedTables.FirstOrDefault(t => 
                                participating.Contains(t.Alias) && 
                                viewDef.SourceTableAllColumns.GetValueOrDefault(t.FullName, new()).Contains(col));
                        }

                        if (targetTable != null) {
                            // Reuse the existing source table toggle logic (triggering a full re-render is easiest)
                             if (!targetTable.UsedColumns.Contains(col)) {
                                targetTable.UsedColumns.Add(col);
                                viewDef.Columns.Add(new ViewColumnInfo { SourceTable = targetTable.Alias, SourceColumn = col, ColumnName = col });
                            } else {
                                targetTable.UsedColumns.Remove(col);
                                viewDef.Columns.RemoveAll(c => c.SourceTable == targetTable.Alias && c.SourceColumn == col);
                            }
                            RenderViewVisualization(viewDef);
                        }
                    };

                    vm.UpdatePagedData();
                }
            }
            catch { }
        }
    }
}
