using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using sqlSense.Models;
using sqlSense.UI.Controls;

namespace sqlSense.UI
{
    /// <summary>
    /// Partial class for ViewGraphRenderer: node creation logic for
    /// source tables, join nodes, and query output nodes.
    /// </summary>
    public partial class ViewGraphRenderer
    {
        private NodeCard CreateSourceTableNode(ReferencedTable refTbl, double width, double x, double y)
        {
            var allCols = _viewModel!.Canvas.CurrentViewDefinition!.SourceTableAllColumns
                .GetValueOrDefault(refTbl.FullName, new List<string>());

            var card = TableCardFactory.CreateTableCard(
                refTbl, allCols, width,
                onDelete: (tbl) => {
                    _viewModel.Canvas.CurrentViewDefinition!.ReferencedTables.Remove(tbl);
                    _viewModel.Canvas.CurrentViewDefinition.Columns.RemoveAll(c => c.SourceTable == tbl.Alias);
                    _viewModel.NotifyModification();
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
                    _viewModel.NotifyModification();
                    RenderViewVisualization(_viewModel.Canvas.CurrentViewDefinition);
                },
                onJoinRequested: (sourceTbl) => {
                    HandleJoinRequest(sourceTbl, x + width + 20, y);
                },
                onExecuteSql: (sql) => {
                    if (_viewModel?.DbService != null)
                    {
                        var db = _viewModel.Canvas.CurrentViewDefinition?.DatabaseName;
                        System.Threading.Tasks.Task.Run(async () => {
                            try {
                                await _viewModel.DbService.ExecuteNonQueryAsync(sql, db);
                                Application.Current.Dispatcher.Invoke(() => {
                                    _viewModel.StatusMessage = "Data changes saved successfully.";
                                });
                            } catch (Exception ex) {
                                Application.Current.Dispatcher.Invoke(() => {
                                    _viewModel.StatusMessage = $"Save failed: {ex.Message}";
                                });
                            }
                        });
                    }
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
            NodeCard? node = null;
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
                onRightChanged: (alias, col) => { join.RightTableAlias = alias; join.RightColumn = col; },
                onShowResult: () => {
                    if (node != null)
                    {
                        SpawnJoinResultCard(node);
                    }
                }
            );

            card.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double h = card.DesiredSize.Height;
            double w = Math.Max(width, card.DesiredSize.Width);
            Canvas.SetLeft(card, x); Canvas.SetTop(card, y);
            _flowCanvas.Children.Add(card); _viewVisualizationElements.Add(card);

            node = new NodeCard { Id = $"Join_{join.LeftTableAlias}_{join.RightTableAlias}", CardElement = card, X = x, Y = y, Width = w, Height = h };
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
    }
}
