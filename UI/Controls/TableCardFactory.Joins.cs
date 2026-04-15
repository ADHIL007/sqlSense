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
    public static partial class TableCardFactory
    {
        public static Border CreateJoinCard(
            JoinRelationship join,
            double minWidth,
            List<string> upstreamCols,
            Action<string, string> onLeftChanged,
            Action<string, string> onRightChanged,
            Action onChangeType,
            Action onShowResult,
            Action onDelete)
        {
            var header = new Border
            {
                Background = new SolidColorBrush(HeaderBg),
                BorderBrush = new SolidColorBrush(BorderColor),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(12, 6, 12, 6)
            };
            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            headerGrid.Children.Add(new TextBlock
            {
                Text = $"{join.JoinType} JOIN",
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(AccentColor),
                VerticalAlignment = VerticalAlignment.Center
            });

            var closeBtn = new Button
            {
                Content = CreateCloseIcon(10),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = new SolidColorBrush(TextMuted),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };
            closeBtn.Click += (s, ev) => onDelete();
            Grid.SetColumn(closeBtn, 1);
            headerGrid.Children.Add(closeBtn);

            header.Child = headerGrid;

            var body = new StackPanel { Margin = new Thickness(12, 10, 12, 10) };

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
                Content = "\u27F3 CHANGE TYPE",
                FontSize = 9,
                Foreground = new SolidColorBrush(TextMuted),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Margin = new Thickness(0, 8, 0, 4),
                Cursor = Cursors.Hand
            };
            changeBtn.Click += (s, e) => onChangeType();
            body.Children.Add(changeBtn);

            // \u2500\u2500 Upstream columns \u2500\u2500
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

            // \u2500\u2500 Assemble \u2500\u2500
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
                Text = "\u2794",
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

            void RedrawLeft()
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
                    b.PreviewMouseLeftButtonDown += (s, e) => { currentLeftCol = captured; RedrawLeft(); };

                    if (!isSelected) { 
                        b.MouseEnter += (s, e) => b.Background = new SolidColorBrush(Color.FromArgb(0x1A, 0xFF, 0xFF, 0xFF)); 
                        b.MouseLeave += (s, e) => b.Background = Brushes.Transparent; 
                    }
                    leftListPanel.Children.Add(b);
                }
            }

            void RedrawRight()
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
                    b.PreviewMouseLeftButtonDown += (s, e) => { currentRightCol = captured; RedrawRight(); };

                    if (!isSelected) { 
                        b.MouseEnter += (s, e) => b.Background = new SolidColorBrush(Color.FromArgb(0x1A, 0xFF, 0xFF, 0xFF)); 
                        b.MouseLeave += (s, e) => b.Background = Brushes.Transparent; 
                    }
                    rightListPanel.Children.Add(b);
                }
            }

            RedrawLeft();
            RedrawRight();

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

        public static Border CreateJoinOptionsPicker(ReferencedTable sourceTbl, Action<string> onJoinTypeSelected, Action onCancel)
        {
            var popup = new Border
            {
                Background = new SolidColorBrush(CardBg),
                BorderBrush = new SolidColorBrush(BorderColor),
                BorderThickness = new Thickness(1),
                Width = 150,
                Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 15, Opacity = 0.4, ShadowDepth = 0 }
            };
            var panel = new StackPanel { Margin = new Thickness(0) };
            
            // Header for clarity
            panel.Children.Add(new Border {
                Background = new SolidColorBrush(HeaderBg),
                Padding = new Thickness(12, 6, 12, 6),
                BorderThickness = new Thickness(0, 0, 0, 1),
                BorderBrush = new SolidColorBrush(BorderColor),
                Child = new TextBlock { Text = "SELECT JOIN TYPE", FontSize = 9, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(TextMuted) }
            });

            string[] types = { "INNER", "LEFT", "RIGHT", "FULL" };
            foreach (var type in types)
            {
                var btn = new Button
                {
                    Content = $"{type} JOIN",
                    Padding = new Thickness(12, 8, 12, 8),
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(0x10, 0xFF, 0xFF, 0xFF)),
                    Foreground = new SolidColorBrush(TextSecondary),
                    Cursor = Cursors.Hand,
                    HorizontalContentAlignment = HorizontalAlignment.Left
                };
                var captured = type;
                btn.Click += (s, e) => onJoinTypeSelected(captured);
                panel.Children.Add(btn);
            }

            var cancelBtn = new Button
            {
                Content = "Cancel",
                Padding = new Thickness(12, 8, 12, 8),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = new SolidColorBrush(TextMuted),
                FontSize = 10,
                Cursor = Cursors.Hand,
                HorizontalContentAlignment = HorizontalAlignment.Center
            };
            cancelBtn.Click += (s, e) => onCancel();
            panel.Children.Add(cancelBtn);

            popup.Child = panel;
            return popup;
        }
    }
}
