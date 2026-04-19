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
                Content = CreateCloseIcon(10),
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
