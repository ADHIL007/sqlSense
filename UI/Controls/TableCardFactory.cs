using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using sqlSense.Models;

namespace sqlSense.UI.Controls
{
    /// <summary>
    /// Creates standardized, professional dark-themed table cards for the canvas.
    /// Consistent with the application's VS Code–inspired design language.
    /// </summary>
    public static partial class TableCardFactory
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
                        IsVisible = true,
                        UsedColumns = refTbl.UsedColumns,
                        OnColumnToggle = (col) => onColumnToggle(col, refTbl)
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

            // \u2500\u2500 Header \u2500\u2500
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

            // \u2500\u2500 Column List \u2500\u2500
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
                colContent.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var colNameText = new TextBlock
                {
                    Text = col,
                    FontSize = 11,
                    Foreground = isSelected ? Brushes.White : new SolidColorBrush(TextSecondary),
                    VerticalAlignment = VerticalAlignment.Center,
                    FontFamily = new FontFamily("Consolas")
                };
                Grid.SetColumn(colNameText, 1);
                colContent.Children.Add(colNameText);

                if (isSelected)
                {
                    var checkIcon = new TextBlock
                    {
                        Text = "\uE73E",
                        FontFamily = new FontFamily("Segoe MDL2 Assets"),
                        FontSize = 10,
                        Foreground = Brushes.White,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 8, 0)
                    };
                    Grid.SetColumn(checkIcon, 0);
                    colContent.Children.Add(checkIcon);
                }

                colRow.Child = colContent;
                colBtn.Content = colRow;
                
                var capturedCol = col;
                colBtn.Click += (s, e) => onColumnToggle(capturedCol, refTbl);
                
                colRow.MouseEnter += (s, e) => {
                    if (!isSelected) colRow.Background = new SolidColorBrush(Color.FromArgb(0x1A, 0xFF, 0xFF, 0xFF));
                };
                colRow.MouseLeave += (s, e) => {
                    if (!isSelected) colRow.Background = Brushes.Transparent;
                };

                colPanel.Children.Add(colBtn);
            }

            var scrollViewer = new ScrollViewer
            {
                MaxHeight = 250,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = colPanel
            };

            var mainPanel = new StackPanel();
            mainPanel.Children.Add(header);
            mainPanel.Children.Add(scrollViewer);

            var innerGrid = new Grid();
            innerGrid.Children.Add(mainPanel);

            // Add JOIN button if requested
            Button? joinBtn = null;
            if (onJoinRequested != null)
            {
                joinBtn = CreateAddJoinButton(refTbl, onJoinRequested, new Thickness(0, 0, -35, 0));
                joinBtn.HorizontalAlignment = HorizontalAlignment.Right;
                innerGrid.Children.Add(joinBtn);
            }

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

            card.MouseEnter += (s, e) => {
                closeBtn.Opacity = 1;
                if (joinBtn != null) joinBtn.Opacity = 1;
            };
            card.MouseLeave += (s, e) => {
                closeBtn.Opacity = 0;
                if (joinBtn != null) joinBtn.Opacity = 0;
            };

            return card;
        }
    }
}
