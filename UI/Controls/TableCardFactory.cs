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

namespace sqlSense.UI.Controls
{
    /// <summary>
    /// Creates standardized, professional dark-themed table cards for the canvas.
    /// Consistent with the application's VS Code–inspired design language.
    /// </summary>
    public static class TableCardFactory
    {
        // Standard theme colors — no rainbow, just clean dark theme
        private static readonly Color HeaderBg = Color.FromRgb(0x2D, 0x2D, 0x30);
        private static readonly Color CardBg = Color.FromRgb(0x25, 0x25, 0x26);
        private static readonly Color BorderColor = Color.FromRgb(0x3C, 0x3C, 0x3C);
        private static readonly Color AccentColor = Color.FromRgb(0x4F, 0xC3, 0xF7);
        private static readonly Color TextPrimary = Color.FromRgb(0xFF, 0xFF, 0xFF);
        private static readonly Color TextSecondary = Color.FromRgb(0xCC, 0xCC, 0xCC);
        private static readonly Color TextMuted = Color.FromRgb(0x88, 0x88, 0x88);
        private static readonly Color SelectedRow = Color.FromRgb(0x09, 0x47, 0x71);

        /// <summary>
        /// Creates a standard table card matching the top-10 records preview style.
        /// Includes a header, column list, and hover-revealed join connector.
        /// </summary>
        public static Border CreateTableCard(
            ReferencedTable refTbl,
            List<string> allColumns,
            double minWidth,
            Action<ReferencedTable> onDelete,
            Action<string, ReferencedTable> onColumnToggle,
            Action<ReferencedTable>? onJoinRequested = null,
            bool isDataFlowMode = false)
        {
            if (isDataFlowMode)
            {
                var previewCard = new sqlSense.UI.Controls.TablePreviewCard
                {
                    Margin = new Thickness(0),
                    DataContext = new sqlSense.ViewModels.Modules.TablePreviewViewModel
                    {
                        TableName = refTbl.DisplayName,
                        IsVisible = true
                    }
                };

                var cardGrid = new Grid();
                cardGrid.Children.Add(previewCard);

                var previewCloseBtn = new Button
                {
                    Content = "\uE711",
                    FontFamily = new FontFamily("Segoe MDL2 Assets"),
                    FontSize = 10,
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Foreground = new SolidColorBrush(TextMuted),
                    Cursor = Cursors.Hand,
                    ToolTip = "Remove table",
                    VerticalAlignment = VerticalAlignment.Top,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 8, 8, 0),
                    Opacity = 0
                };
                previewCloseBtn.Click += (s, e) => onDelete(refTbl);
                cardGrid.Children.Add(previewCloseBtn);

                if (onJoinRequested != null)
                {
                    var leftJoinBtn = CreateAddJoinButton(refTbl, onJoinRequested, new Thickness(-35, 0, 0, 0));
                    leftJoinBtn.HorizontalAlignment = HorizontalAlignment.Left;
                    
                    var rightJoinBtn = CreateAddJoinButton(refTbl, onJoinRequested, new Thickness(0, 0, -35, 0));
                    rightJoinBtn.HorizontalAlignment = HorizontalAlignment.Right;
                    
                    cardGrid.Children.Add(leftJoinBtn);
                    cardGrid.Children.Add(rightJoinBtn);
                    
                    cardGrid.MouseEnter += (s, e) =>
                    {
                        previewCloseBtn.Opacity = 1;
                        leftJoinBtn.Opacity = 1;
                        rightJoinBtn.Opacity = 1;
                    };
                    cardGrid.MouseLeave += (s, e) =>
                    {
                        previewCloseBtn.Opacity = 0;
                        leftJoinBtn.Opacity = 0;
                        rightJoinBtn.Opacity = 0;
                    };
                }
                else
                {
                    cardGrid.MouseEnter += (s, e) => previewCloseBtn.Opacity = 1;
                    cardGrid.MouseLeave += (s, e) => previewCloseBtn.Opacity = 0;
                }

                return new Border
                {
                    Child = cardGrid,
                    MinWidth = minWidth,
                    Effect = new DropShadowEffect
                    {
                        Color = Colors.Black,
                        BlurRadius = 15,
                        Opacity = 0.4,
                        ShadowDepth = 0
                    }
                };
            }

            // ── Header ──
            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var headerLeft = new StackPanel { Orientation = Orientation.Horizontal };
            headerLeft.Children.Add(new TextBlock
            {
                Text = "\uE80A",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 13,
                Foreground = new SolidColorBrush(AccentColor),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            });
            headerLeft.Children.Add(new TextBlock
            {
                Text = refTbl.DisplayName,
                FontSize = 12,
                Foreground = new SolidColorBrush(TextPrimary),
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.SemiBold
            });

            var closeBtn = new Button
            {
                Content = "\uE711",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 10,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = new SolidColorBrush(TextMuted),
                Cursor = Cursors.Hand,
                ToolTip = "Remove table",
                VerticalAlignment = VerticalAlignment.Center,
                Opacity = 0
            };
            closeBtn.Click += (s, e) => onDelete(refTbl);

            Grid.SetColumn(headerLeft, 0);
            Grid.SetColumn(closeBtn, 1);
            headerGrid.Children.Add(headerLeft);
            headerGrid.Children.Add(closeBtn);

            var header = new Border
            {
                Background = new SolidColorBrush(HeaderBg),
                BorderBrush = new SolidColorBrush(BorderColor),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(12, 8, 12, 8),
                Child = headerGrid
            };

            // ── Column List ──
            var colPanel = new StackPanel();
            foreach (var col in allColumns)
            {
                bool isSelected = refTbl.UsedColumns.Contains(col);
                var colBtn = new Button
                {
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(0),
                    Cursor = Cursors.Hand,
                    HorizontalContentAlignment = HorizontalAlignment.Stretch
                };

                var colRow = new Border
                {
                    Background = isSelected
                        ? new SolidColorBrush(SelectedRow)
                        : Brushes.Transparent,
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(0x20, 0xFF, 0xFF, 0xFF)),
                    Padding = new Thickness(12, 6, 12, 6)
                };

                var colContent = new Grid();
                colContent.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                colContent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                if (isSelected)
                {
                    var checkIcon = new TextBlock
                    {
                        Text = "\uE73E",
                        FontFamily = new FontFamily("Segoe MDL2 Assets"),
                        FontSize = 10,
                        Foreground = new SolidColorBrush(AccentColor),
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 8, 0)
                    };
                    Grid.SetColumn(checkIcon, 0);
                    colContent.Children.Add(checkIcon);
                }

                var colText = new TextBlock
                {
                    Text = col,
                    Foreground = isSelected
                        ? new SolidColorBrush(TextPrimary)
                        : new SolidColorBrush(TextSecondary),
                    FontSize = 11,
                    FontFamily = new FontFamily("Consolas"),
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(colText, 1);
                colContent.Children.Add(colText);

                colRow.Child = colContent;
                colBtn.Content = colRow;

                string capturedCol = col;
                colBtn.Click += (s, e) => onColumnToggle(capturedCol, refTbl);

                colPanel.Children.Add(colBtn);
            }

            UIElement bodyElement = colPanel;

            // ── Footer with row count ──
            var footer = new Border
            {
                Background = new SolidColorBrush(HeaderBg),
                BorderBrush = new SolidColorBrush(BorderColor),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(12, 4, 12, 4)
            };
            var footerText = new TextBlock
            {
                Text = $"{allColumns.Count} columns · {refTbl.UsedColumns.Count} selected",
                FontSize = 10,
                Foreground = new SolidColorBrush(TextMuted),
                FontFamily = new FontFamily("Consolas")
            };
            footer.Child = footerText;

            // ── Main card assembly ──
            var mainPanel = new StackPanel();
            mainPanel.Children.Add(header);
            mainPanel.Children.Add(bodyElement);
            mainPanel.Children.Add(footer);

            var card = new Border
            {
                Background = new SolidColorBrush(CardBg),
                BorderBrush = new SolidColorBrush(BorderColor),
                BorderThickness = new Thickness(1),
                MinWidth = minWidth,
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 15,
                    Opacity = 0.4,
                    ShadowDepth = 0
                }
            };

            // ── Wrap in Grid for join connector overlay ──
            var wrapper = new Grid();
            wrapper.Children.Add(mainPanel);

            // Join connector button (appears on right edge on hover)
            if (onJoinRequested != null)
            {
                var joinBtn = CreateJoinConnectorButton(refTbl, onJoinRequested);
                joinBtn.HorizontalAlignment = HorizontalAlignment.Right;
                joinBtn.VerticalAlignment = VerticalAlignment.Center;
                joinBtn.Margin = new Thickness(0, 0, -14, 0);
                wrapper.Children.Add(joinBtn);
            }

            card.Child = wrapper;

            card.MouseEnter += (s, e) =>
            {
                closeBtn.Opacity = 1;
                //card.BorderBrush = new SolidColorBrush(T);
            };
            card.MouseLeave += (s, e) =>
            {
                closeBtn.Opacity = 0;
                card.BorderBrush = new SolidColorBrush(BorderColor);
            };

            return card;
        }

        /// <summary>
        /// Creates the small "+" connector button that appears at the right edge
        /// of a table card on hover. Clicking it triggers the join workflow.
        /// </summary>
        private static Border CreateJoinConnectorButton(
            ReferencedTable refTbl,
            Action<ReferencedTable> onJoinRequested)
        {
            var plusText = new TextBlock
            {
                Text = "+",
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(AccentColor),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var circle = new Border
            {
                Width = 24,
                Height = 24,
                CornerRadius = new CornerRadius(12),
                Background = Brushes.Transparent, // always transparent
                BorderBrush = new SolidColorBrush(AccentColor),
                BorderThickness = new Thickness(1.5),
                Cursor = Cursors.Hand,
                Child = plusText,
                Opacity = 0,
                ToolTip = "Add JOIN"
            };


            {
                circle.Opacity = 1;
                
            };

            circle.MouseLeave += (s, e) =>
            {
                circle.Opacity = 0;
               
            };

            circle.PreviewMouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;
                onJoinRequested(refTbl);
            };

            return circle;
        }

        private static Border CreateAddJoinButton(ReferencedTable refTbl, Action<ReferencedTable> onJoinRequested, Thickness margin)
        {
            var btnBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0xEE, 0x2D, 0x2D, 0x30)),
                BorderBrush = new SolidColorBrush(AccentColor),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(4, 12, 4, 12),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = margin,
                Opacity = 0,
                ToolTip = "Add JOIN"
            };

            var text = new TextBlock
            {
                Text = "+ ADD JOIN +",
                FontSize = 9,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(AccentColor),
                FontFamily = new FontFamily("Consolas")
            };
            text.LayoutTransform = new RotateTransform(-90);

            btnBorder.Child = text;

            btnBorder.MouseEnter += (s, e) =>
            {
                text.Foreground = Brushes.White;
            };
            btnBorder.MouseLeave += (s, e) =>
            {
                text.Foreground = new SolidColorBrush(AccentColor);
            };

            btnBorder.PreviewMouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;
                onJoinRequested(refTbl);
            };

            return btnBorder;
        }

        /// <summary>
        /// Creates the join type picker popup that appears when clicking the "+" connector.
        /// </summary>
        public static Border CreateJoinOptionsPicker(   
            ReferencedTable sourceTable,
            Action<string> onJoinTypeSelected,
            Action onCancel)
        {
            var popup = new Border
            {
                Background = new SolidColorBrush(CardBg),
                BorderBrush = new SolidColorBrush(BorderColor),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(0),
                MinWidth = 160,
                MaxHeight = 350,
                Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 20, Opacity = 0.5 }
            };

            var panel = new StackPanel();

            // Header
            var headerBorder = new Border
            {
                Background = new SolidColorBrush(HeaderBg),
                BorderBrush = new SolidColorBrush(BorderColor),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(12, 8, 12, 8)
            };
            headerBorder.Child = new TextBlock
            {
                Text = $"SELECT JOIN TYPE",
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(TextPrimary),
                FontSize = 11
            };
            panel.Children.Add(headerBorder);

            // Join type buttons
            var joinTypes = new[] { "INNER", "LEFT", "RIGHT", "FULL" };
            foreach (var jt in joinTypes)
            {
                var jtLabel = $"{jt} JOIN";
                var itemBtn = new Button
                {
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(12, 8, 12, 8),
                    Cursor = Cursors.Hand,
                    HorizontalContentAlignment = HorizontalAlignment.Left
                };

                var itemContent = new StackPanel { Orientation = Orientation.Horizontal };
                itemContent.Children.Add(new TextBlock
                {
                    Text = "\uE811", // Merge icon
                    FontFamily = new FontFamily("Segoe MDL2 Assets"),
                    FontSize = 12,
                    Foreground = new SolidColorBrush(AccentColor),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 8, 0)
                });
                itemContent.Children.Add(new TextBlock
                {
                    Text = jtLabel,
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(TextSecondary),
                    VerticalAlignment = VerticalAlignment.Center
                });

                itemBtn.Content = itemContent;
                var capturedJt = jt;
                itemBtn.Click += (s, e) => onJoinTypeSelected(capturedJt);

                panel.Children.Add(itemBtn);

                panel.Children.Add(new Rectangle
                {
                    Height = 1,
                    Fill = new SolidColorBrush(BorderColor),
                    Margin = new Thickness(8, 0, 8, 0)
                });
            }

            var cancelBtn = new Button
            {
                Content = "Cancel",
                FontSize = 10,
                Foreground = new SolidColorBrush(TextMuted),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(8, 4, 8, 8)
            };
            cancelBtn.Click += (s, e) => onCancel();
            panel.Children.Add(cancelBtn);

            popup.Child = panel;
            return popup;
        }

        /// <summary>
        /// Creates a standardized view output node card.
        /// </summary>
        public static Border CreateQueryOutputCard(
            ViewDefinitionInfo viewDef,
            double minWidth,
            Action onPreviewSql,
            Action onRender,
            bool isDataFlowMode = false)
        {
            // ── Header ──
            var viewNameBox = new TextBox
            {
                Text = viewDef.ViewName,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = new SolidColorBrush(TextPrimary),
                FontWeight = FontWeights.SemiBold,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                CaretBrush = Brushes.White
            };
            viewNameBox.LostFocus += (s, e) => viewDef.ViewName = viewNameBox.Text;

            var headerLeft = new StackPanel { Orientation = Orientation.Horizontal };
            headerLeft.Children.Add(new TextBlock
            {
                Text = "\uE71A", // Query/Data icon instead of View icon
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 13,
                Foreground = new SolidColorBrush(AccentColor),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            });
            headerLeft.Children.Add(viewNameBox);

            var header = new Border
            {
                Background = new SolidColorBrush(HeaderBg),
                BorderBrush = new SolidColorBrush(BorderColor),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(12, 8, 12, 8),
                Child = headerLeft
            };

            // ── Column List ──
            var colPanel = new StackPanel();
            foreach (var col in viewDef.Columns.ToList())
            {
                var row = new Grid { Margin = new Thickness(0) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var icon = new TextBlock
                {
                    Text = "\uE71A",
                    FontFamily = new FontFamily("Segoe MDL2 Assets"),
                    FontSize = 9,
                    Foreground = new SolidColorBrush(AccentColor),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(12, 0, 6, 0)
                };

                var nameBox = new TextBox
                {
                    Text = string.IsNullOrWhiteSpace(col.Alias) ? col.ColumnName : col.Alias,
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Foreground = new SolidColorBrush(TextSecondary),
                    FontSize = 11,
                    FontFamily = new FontFamily("Consolas"),
                    Padding = new Thickness(0, 4, 0, 4)
                };
                nameBox.LostFocus += (s, e) => col.Alias = nameBox.Text;

                var delBtn = new Button
                {
                    Content = "\uE711",
                    FontFamily = new FontFamily("Segoe MDL2 Assets"),
                    FontSize = 9,
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Foreground = new SolidColorBrush(TextMuted),
                    Cursor = Cursors.Hand,
                    Opacity = 0,
                    Margin = new Thickness(0, 0, 8, 0)
                };
                var capturedCol = col;
                delBtn.Click += (s, e) =>
                {
                    viewDef.Columns.Remove(capturedCol);
                    var srcTbl = viewDef.ReferencedTables.FirstOrDefault(t => t.Alias == capturedCol.SourceTable);
                    srcTbl?.UsedColumns.Remove(capturedCol.SourceColumn);
                    onRender();
                };

                var rowBorder = new Border
                {
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(0x20, 0xFF, 0xFF, 0xFF)),
                    Padding = new Thickness(0, 2, 0, 2)
                };
                rowBorder.MouseEnter += (s, e) => delBtn.Opacity = 1;
                rowBorder.MouseLeave += (s, e) => delBtn.Opacity = 0;

                Grid.SetColumn(icon, 0);
                Grid.SetColumn(nameBox, 1);
                Grid.SetColumn(delBtn, 2);
                row.Children.Add(icon);
                row.Children.Add(nameBox);
                row.Children.Add(delBtn);
                rowBorder.Child = row;
                colPanel.Children.Add(rowBorder);
            }

            // ── Clause Editors ──
            var clausePanel = new StackPanel { Margin = new Thickness(12, 6, 12, 10) };
            CanvasUIFactory.AddClauseEditor(clausePanel, "\uE71C", "WHERE", viewDef.WhereClause, v => { viewDef.WhereClause = v; onRender(); });
            CanvasUIFactory.AddClauseEditor(clausePanel, "\uE14C", "GROUP BY", viewDef.GroupByClause, v => { viewDef.GroupByClause = v; onRender(); });
            CanvasUIFactory.AddClauseEditor(clausePanel, "\uE16E", "HAVING", viewDef.HavingClause, v => { viewDef.HavingClause = v; onRender(); });
            CanvasUIFactory.AddClauseEditor(clausePanel, "\uE174", "ORDER BY", viewDef.OrderByClause, v => { viewDef.OrderByClause = v; onRender(); });

            // ── Preview button ──
            var previewBtn = new Button
            {
                FontSize = 10,
                Foreground = new SolidColorBrush(TextSecondary),
                Background = new SolidColorBrush(HeaderBg),
                BorderBrush = new SolidColorBrush(BorderColor),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                Margin = new Thickness(12, 4, 12, 10),
                Padding = new Thickness(10, 6, 10, 6),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            var previewContent = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            previewContent.Children.Add(new TextBlock
            {
                Text = "\uE943",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(TextSecondary)
            });
            previewContent.Children.Add(new TextBlock
            {
                Text = "PREVIEW SQL",
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(TextSecondary)
            });
            previewBtn.Content = previewContent;
            previewBtn.Click += (s, e) => onPreviewSql();

            // ── Footer ──
            var footer = new Border
            {
                Background = new SolidColorBrush(HeaderBg),
                BorderBrush = new SolidColorBrush(BorderColor),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(12, 4, 12, 4)
            };
            footer.Child = new TextBlock
            {
                Text = $"QUERY RESULT · {viewDef.Columns.Count} selected fields",
                FontSize = 10,
                Foreground = new SolidColorBrush(TextMuted),
                FontFamily = new FontFamily("Consolas")
            };

            // ── Assemble ──
            var mainPanel = new StackPanel();
            mainPanel.Children.Add(header);
            if (isDataFlowMode)
            {
                var dataBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E)),
                    MinHeight = 150,
                    Padding = new Thickness(10),
                    Child = new TextBlock
                    {
                        Text = "Final Join Render Preview...",
                        Foreground = new SolidColorBrush(TextMuted),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        FontFamily = new FontFamily("Consolas"),
                        FontSize = 10
                    }
                };
                mainPanel.Children.Add(dataBorder);
            }
            else
            {
                mainPanel.Children.Add(colPanel);
                mainPanel.Children.Add(clausePanel);
            }
            mainPanel.Children.Add(previewBtn);
            mainPanel.Children.Add(footer);

            var card = new Border
            {
                Background = new SolidColorBrush(CardBg),
                BorderBrush = new SolidColorBrush(BorderColor),
                BorderThickness = new Thickness(1),
                MinWidth = minWidth,
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 15,
                    Opacity = 0.4,
                    ShadowDepth = 0
                },
                Child = mainPanel
            };

            return card;
        }

        /// <summary>
        /// Creates a standardized join node card.
        /// </summary>
        public static Border CreateJoinCard(
            JoinRelationship join,
            double minWidth,
            List<string> upstreamCols,
            Action onDelete,
            Action onChangeType,
            Action<string, string> onLeftChanged,
            Action<string, string> onRightChanged,
            Action? onShowResult = null)
        {
            // ── Header ──
            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var joinLabel = new TextBlock
            {
                Text = $"{join.JoinType} JOIN",
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(TextPrimary),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center
            };

            var delBtn = new Button
            {
                Content = "\uE711",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 10,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = new SolidColorBrush(TextMuted),
                Cursor = Cursors.Hand,
                ToolTip = "Remove JOIN"
            };
            delBtn.Click += (s, e) => onDelete();

            Grid.SetColumn(joinLabel, 0);
            Grid.SetColumn(delBtn, 1);
            headerGrid.Children.Add(joinLabel);
            headerGrid.Children.Add(delBtn);

            var header = new Border
            {
                Background = new SolidColorBrush(HeaderBg),
                BorderBrush = new SolidColorBrush(BorderColor),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(10, 6, 10, 6),
                Child = headerGrid
            };

            // ── Body ──
            var body = new StackPanel { Margin = new Thickness(10, 8, 10, 8) };

            var leftBox = new TextBox
            {
                Text = $"{join.LeftTableAlias}.{join.LeftColumn}",
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0, 0, 0, 1),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF)),
                Foreground = new SolidColorBrush(TextSecondary),
                FontSize = 11,
                FontFamily = new FontFamily("Consolas"),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };
            leftBox.LostFocus += (s, e) =>
            {
                var parts = leftBox.Text.Split('.', 2);
                if (parts.Length == 2) onLeftChanged(parts[0], parts[1]);
            };
            body.Children.Add(leftBox);

            body.Children.Add(new TextBlock
            {
                Text = "=",
                Foreground = new SolidColorBrush(TextMuted),
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 2, 0, 2)
            });

            var rightBox = new TextBox
            {
                Text = $"{join.RightTableAlias}.{join.RightColumn}",
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0, 0, 0, 1),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF)),
                Foreground = new SolidColorBrush(TextSecondary),
                FontSize = 11,
                FontFamily = new FontFamily("Consolas"),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };
            rightBox.LostFocus += (s, e) =>
            {
                var parts = rightBox.Text.Split('.', 2);
                if (parts.Length == 2) onRightChanged(parts[0], parts[1]);
            };
            body.Children.Add(rightBox);

            var changeBtn = new Button
            {
                Content = "⟳ CHANGE TYPE",
                FontSize = 9,
                Foreground = new SolidColorBrush(TextMuted),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Margin = new Thickness(0, 8, 0, 4),
                Cursor = Cursors.Hand
            };
            changeBtn.Click += (s, e) => onChangeType();
            body.Children.Add(changeBtn);

            // ── Upstream columns ──
            if (upstreamCols.Any())
            {
                var flowBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(0x10, 0xFF, 0xFF, 0xFF)),
                    BorderBrush = new SolidColorBrush(BorderColor),
                    BorderThickness = new Thickness(0, 1, 0, 0)
                };
                var flowPanel = new StackPanel();
                flowPanel.Children.Add(new TextBlock
                {
                    Text = "SELECTED FIELDS",
                    FontSize = 9,
                    Foreground = new SolidColorBrush(TextMuted),
                    Margin = new Thickness(12, 8, 12, 4),
                    FontWeight = FontWeights.SemiBold
                });
                foreach (var c in upstreamCols)
                {
                    flowPanel.Children.Add(new Border
                    {
                        BorderThickness = new Thickness(0, 0, 0, 1),
                        BorderBrush = new SolidColorBrush(Color.FromArgb(0x15, 0xFF, 0xFF, 0xFF)),
                        Padding = new Thickness(12, 5, 12, 5),
                        Child = new TextBlock
                        {
                            Text = c,
                            Foreground = new SolidColorBrush(TextSecondary),
                            FontSize = 11,
                            FontFamily = new FontFamily("Consolas")
                        }
                    });
                }
                flowBorder.Child = flowPanel;
                body.Children.Add(flowBorder);
            }

            // ── Assemble ──
            var mainPanel = new StackPanel();
            mainPanel.Children.Add(header);
            mainPanel.Children.Add(body);

            var innerGrid = new Grid();
            innerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            innerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            
            Grid.SetColumn(mainPanel, 0);
            innerGrid.Children.Add(mainPanel);

            var showResultBtnGrid = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0x10, 0xFF, 0xFF, 0xFF)),
                BorderBrush = new SolidColorBrush(BorderColor),
                BorderThickness = new Thickness(1, 0, 0, 0),
                Cursor = Cursors.Hand,
                Padding = new Thickness(8, 0, 8, 0),
                ToolTip = "Show Joined Result"
            };
            var btnContent = new TextBlock
            {
                Text = "➔",
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = new SolidColorBrush(TextSecondary),
                FontSize = 14
            };
            showResultBtnGrid.Child = btnContent;
            
            showResultBtnGrid.MouseEnter += (s, e) => {
                showResultBtnGrid.Background = new SolidColorBrush(AccentColor);
                btnContent.Foreground = Brushes.Black;
            };
            showResultBtnGrid.MouseLeave += (s, e) => {
                showResultBtnGrid.Background = new SolidColorBrush(Color.FromArgb(0x10, 0xFF, 0xFF, 0xFF));
                btnContent.Foreground = new SolidColorBrush(TextSecondary);
            };
            showResultBtnGrid.MouseLeftButtonDown += (s, e) => {
                e.Handled = true;
                onShowResult?.Invoke();
            };

            Grid.SetColumn(showResultBtnGrid, 1);
            innerGrid.Children.Add(showResultBtnGrid);

            var card = new Border
            {
                Background = new SolidColorBrush(CardBg),
                BorderBrush = new SolidColorBrush(BorderColor),
                BorderThickness = new Thickness(1),
                MinWidth = minWidth,
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 15,
                    Opacity = 0.4,
                    ShadowDepth = 0
                },
                Child = innerGrid
            };

            return card;
        }

        /// <summary>
        /// Creates a join result preview card using the same TablePreviewCard design.
        /// Stays visible permanently on canvas and supports "Add JOIN" connectors for chaining.
        /// </summary>
        public static Border CreateJoinResultCard(
            string title,
            double minWidth,
            Action onClose,
            Action<ReferencedTable>? onJoinRequested = null,
            ReferencedTable? joinSourceTable = null)
        {
            var previewCard = new sqlSense.UI.Controls.TablePreviewCard
            {
                Margin = new Thickness(0),
                DataContext = new sqlSense.ViewModels.Modules.TablePreviewViewModel
                {
                    TableName = title,
                    IsVisible = true
                }
            };

            var cardGrid = new Grid();
            cardGrid.Children.Add(previewCard);

            // Close button (top-right, revealed on hover)
            var closeBtn = new Button
            {
                Content = "\uE711",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 10,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = new SolidColorBrush(TextMuted),
                Cursor = Cursors.Hand,
                ToolTip = "Close result",
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 8, 8, 0),
                Opacity = 0
            };
            closeBtn.Click += (s, e) => onClose();
            cardGrid.Children.Add(closeBtn);

            if (onJoinRequested != null && joinSourceTable != null)
            {
                var leftJoinBtn = CreateAddJoinButton(joinSourceTable, onJoinRequested, new Thickness(-35, 0, 0, 0));
                leftJoinBtn.HorizontalAlignment = HorizontalAlignment.Left;

                var rightJoinBtn = CreateAddJoinButton(joinSourceTable, onJoinRequested, new Thickness(0, 0, -35, 0));
                rightJoinBtn.HorizontalAlignment = HorizontalAlignment.Right;

                cardGrid.Children.Add(leftJoinBtn);
                cardGrid.Children.Add(rightJoinBtn);

                cardGrid.MouseEnter += (s, e) =>
                {
                    closeBtn.Opacity = 1;
                    leftJoinBtn.Opacity = 1;
                    rightJoinBtn.Opacity = 1;
                };
                cardGrid.MouseLeave += (s, e) =>
                {
                    closeBtn.Opacity = 0;
                    leftJoinBtn.Opacity = 0;
                    rightJoinBtn.Opacity = 0;
                };
            }
            else
            {
                cardGrid.MouseEnter += (s, e) => closeBtn.Opacity = 1;
                cardGrid.MouseLeave += (s, e) => closeBtn.Opacity = 0;
            }

            return new Border
            {
                Child = cardGrid,
                MinWidth = minWidth,
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 15,
                    Opacity = 0.4,
                    ShadowDepth = 0
                }
            };
        }

        public static Border CreateJoinColumnPicker(
            ReferencedTable leftTable,
            ReferencedTable rightTable,
            List<string> leftCols,
            List<string> rightCols,
            string suggestedLeftCol,
            string suggestedRightCol,
            Action<string, string> onConfirmed,
            Action onCancel)
        {
            var popup = new Border
            {
                Background = new SolidColorBrush(CardBg),
                BorderBrush = new SolidColorBrush(BorderColor),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(0),
                Width = 450,
                Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 20, Opacity = 0.5 }
            };

            var mainPanel = new StackPanel();

            // Header
            var headerBorder = new Border
            {
                Background = new SolidColorBrush(HeaderBg),
                BorderBrush = new SolidColorBrush(BorderColor),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(12, 8, 12, 8)
            };
            headerBorder.Child = new TextBlock
            {
                Text = "CONFIGURE JOIN CONDITIONS",
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(TextPrimary),
                FontSize = 11
            };
            mainPanel.Children.Add(headerBorder);

            var bodyGrid = new Grid { Margin = new Thickness(12) };
            bodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            bodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            bodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            string currentLeftCol = suggestedLeftCol;
            string currentRightCol = suggestedRightCol;

            var leftPanel = new StackPanel();
            leftPanel.Children.Add(new TextBlock { Text = leftTable.DisplayName, Foreground = new SolidColorBrush(TextPrimary), FontSize = 11, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0,0,0,4) });
            var leftListPanel = new StackPanel();
            var leftScroll = new ScrollViewer { MaxHeight = 150, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = leftListPanel };
            var leftBorder = new Border { BorderBrush = new SolidColorBrush(BorderColor), BorderThickness = new Thickness(1), Child = leftScroll, Background = new SolidColorBrush(Color.FromArgb(0x10, 0, 0, 0)) };
            leftPanel.Children.Add(leftBorder);
            Grid.SetColumn(leftPanel, 0);

            var rightPanel = new StackPanel();
            rightPanel.Children.Add(new TextBlock { Text = rightTable.DisplayName, Foreground = new SolidColorBrush(TextPrimary), FontSize = 11, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0,0,0,4) });
            var rightListPanel = new StackPanel();
            var rightScroll = new ScrollViewer { MaxHeight = 150, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = rightListPanel };
            var rightBorder = new Border { BorderBrush = new SolidColorBrush(BorderColor), BorderThickness = new Thickness(1), Child = rightScroll, Background = new SolidColorBrush(Color.FromArgb(0x10, 0, 0, 0)) };
            rightPanel.Children.Add(rightBorder);
            Grid.SetColumn(rightPanel, 2);

            Action? redrawLeft = null;
            Action? redrawRight = null;

            redrawLeft = () =>
            {
                leftListPanel.Children.Clear();
                foreach (var c in leftCols)
                {
                    bool isSelected = (c == currentLeftCol);
                    var b = new Border
                    {
                        Background = isSelected ? new SolidColorBrush(SelectedRow) : Brushes.Transparent,
                        Padding = new Thickness(8, 4, 8, 4),
                        Cursor = Cursors.Hand
                    };
                    b.Child = new TextBlock { Text = c, Foreground = isSelected ? Brushes.White : new SolidColorBrush(TextSecondary), FontSize = 11, FontFamily = new FontFamily("Consolas") };
                    var captured = c;
                    b.PreviewMouseLeftButtonDown += (s, e) => { currentLeftCol = captured; redrawLeft!(); };
                    if (!isSelected) { b.MouseEnter += (s, e) => b.Background = new SolidColorBrush(Color.FromArgb(0x1A, 0xFF, 0xFF, 0xFF)); b.MouseLeave += (s, e) => b.Background = Brushes.Transparent; }
                    leftListPanel.Children.Add(b);
                }
            };
            redrawLeft();

            redrawRight = () =>
            {
                rightListPanel.Children.Clear();
                foreach (var c in rightCols)
                {
                    bool isSelected = (c == currentRightCol);
                    var b = new Border
                    {
                        Background = isSelected ? new SolidColorBrush(SelectedRow) : Brushes.Transparent,
                        Padding = new Thickness(8, 4, 8, 4),
                        Cursor = Cursors.Hand
                    };
                    b.Child = new TextBlock { Text = c, Foreground = isSelected ? Brushes.White : new SolidColorBrush(TextSecondary), FontSize = 11, FontFamily = new FontFamily("Consolas") };
                    var captured = c;
                    b.PreviewMouseLeftButtonDown += (s, e) => { currentRightCol = captured; redrawRight!(); };
                    if (!isSelected) { b.MouseEnter += (s, e) => b.Background = new SolidColorBrush(Color.FromArgb(0x1A, 0xFF, 0xFF, 0xFF)); b.MouseLeave += (s, e) => b.Background = Brushes.Transparent; }
                    rightListPanel.Children.Add(b);
                }
            };
            redrawRight();

            var centerLabel = new TextBlock { Text = "=", Foreground = new SolidColorBrush(AccentColor), FontWeight = FontWeights.Bold, FontSize = 16, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(12, 16, 12, 0) };
            Grid.SetColumn(centerLabel, 1);

            bodyGrid.Children.Add(leftPanel);
            bodyGrid.Children.Add(centerLabel);
            bodyGrid.Children.Add(rightPanel);

            mainPanel.Children.Add(bodyGrid);

            mainPanel.Children.Add(new TextBlock
            {
                Text = "Need multiple? You can add complex ON conditions (A=B AND X=Y) by editing the text box in the Join Node card afterwards.",
                Foreground = new SolidColorBrush(TextMuted),
                FontSize = 9.5,
                Margin = new Thickness(12, 0, 12, 8),
                TextWrapping = TextWrapping.Wrap
            });

            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(12, 0, 12, 12) };
            
            var cancelBtn = new Button { Content = "Cancel", Margin = new Thickness(0,0,8,0), Padding = new Thickness(12,6,12,6), Background = Brushes.Transparent, Foreground = new SolidColorBrush(TextSecondary), BorderThickness = new Thickness(0), Cursor = Cursors.Hand };
            cancelBtn.Click += (s, e) => onCancel();
            
            var addBtn = new Border { Background = new SolidColorBrush(AccentColor), CornerRadius = new CornerRadius(3), Padding = new Thickness(12,6,12,6), Cursor = Cursors.Hand };
            addBtn.Child = new TextBlock { Text = "CONFIRM JOIN", Foreground = Brushes.Black, FontWeight = FontWeights.SemiBold, FontSize = 11 };
            addBtn.PreviewMouseLeftButtonDown += (s, e) => onConfirmed(currentLeftCol, currentRightCol);

            btnPanel.Children.Add(cancelBtn);
            btnPanel.Children.Add(addBtn);

            mainPanel.Children.Add(btnPanel);
            popup.Child = mainPanel;
            return popup;
        }

        /// <summary>
        /// Creates a standardized table picker popup (for "Add Table" or join target selection).
        /// </summary>
        public static Border CreateTablePicker(
            IEnumerable<Services.TableInfo> tables,
            IEnumerable<ReferencedTable>? existingTables,
            Action<Services.TableInfo> onSelected,
            Action onCancel)
        {
            var popup = new Border
            {
                Background = new SolidColorBrush(CardBg),
                BorderBrush = new SolidColorBrush(BorderColor),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(0),
                Width = 320,
                MaxHeight = 400,
                Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 20, Opacity = 0.5 }
            };

            var mainPanel = new StackPanel();

            // Header
            var headerBorder = new Border
            {
                Background = new SolidColorBrush(HeaderBg),
                BorderBrush = new SolidColorBrush(BorderColor),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(12, 8, 12, 8)
            };
            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.Children.Add(new TextBlock
            {
                Text = "SELECT A TABLE",
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(TextPrimary),
                FontSize = 11
            });
            var closeBtn = new Button
            {
                Content = "\uE711",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 10,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = new SolidColorBrush(TextMuted),
                Cursor = Cursors.Hand
            };
            closeBtn.Click += (s, e) => onCancel();
            Grid.SetColumn(closeBtn, 1);
            headerGrid.Children.Add(closeBtn);
            headerBorder.Child = headerGrid;
            mainPanel.Children.Add(headerBorder);

            // Search box
            var searchContainer = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0x30, 0, 0, 0)),
                Padding = new Thickness(10, 8, 10, 8)
            };
            var searchBox = new TextBox
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = Brushes.White
            };
            var placeholder = new TextBlock
            {
                Text = "Search tables...",
                Foreground = new SolidColorBrush(TextMuted),
                IsHitTestVisible = false
            };
            var searchGrid = new Grid();
            searchGrid.Children.Add(searchBox);
            searchGrid.Children.Add(placeholder);
            searchContainer.Child = searchGrid;
            mainPanel.Children.Add(searchContainer);

            // Table list
            var listPanel = new StackPanel();
            var scroll = new ScrollViewer { MaxHeight = 280, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            scroll.Content = listPanel;
            mainPanel.Children.Add(scroll);

            var existingSet = existingTables != null
                ? new HashSet<string>(existingTables.Select(t => $"{t.Schema}.{t.Name}"))
                : new HashSet<string>();

            void PopulateList(string filter = "")
            {
                listPanel.Children.Clear();
                foreach (var t in tables)
                {
                    string fullName = $"{t.Schema}.{t.Name}";
                    if (existingSet.Contains(fullName)) continue;
                    if (!string.IsNullOrEmpty(filter) && !fullName.Contains(filter, StringComparison.OrdinalIgnoreCase)) continue;

                    var itemBtn = new Button
                    {
                        Background = Brushes.Transparent,
                        BorderThickness = new Thickness(0),
                        Padding = new Thickness(12, 6, 12, 6),
                        Cursor = Cursors.Hand,
                        HorizontalContentAlignment = HorizontalAlignment.Left
                    };
                    var itemContent = new StackPanel { Orientation = Orientation.Horizontal };
                    itemContent.Children.Add(new TextBlock
                    {
                        Text = "\uE80A",
                        FontFamily = new FontFamily("Segoe MDL2 Assets"),
                        Foreground = new SolidColorBrush(TextMuted),
                        Margin = new Thickness(0, 0, 10, 0),
                        VerticalAlignment = VerticalAlignment.Center
                    });
                    itemContent.Children.Add(new TextBlock
                    {
                        Text = fullName,
                        FontSize = 11,
                        Foreground = new SolidColorBrush(TextSecondary),
                        VerticalAlignment = VerticalAlignment.Center
                    });
                    itemBtn.Content = itemContent;
                    var capturedT = t;
                    itemBtn.Click += (s, e) => onSelected(capturedT);
                    listPanel.Children.Add(itemBtn);
                }
            }
            PopulateList();

            searchBox.TextChanged += (s, e) =>
            {
                placeholder.Visibility = string.IsNullOrEmpty(searchBox.Text) ? Visibility.Visible : Visibility.Collapsed;
                PopulateList(searchBox.Text);
            };

            popup.Child = mainPanel;
            return popup;
        }
    }
}
