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
                    IsVisible = true,
                    HideUnselectedColumns = true
                }
            };

            var cardGrid = new Grid();
            cardGrid.Children.Add(previewCard);

            var closeBtn = new Button
            {
                Content = CreateCloseIcon(10),
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

        public static Button CreateAddJoinButton(ReferencedTable sourceTbl, Action<ReferencedTable> onClick, Thickness margin)
        {
            var btn = new Button
            {
                Content = "+",
                Width = 24,
                Height = 24,
                Background = new SolidColorBrush(AccentColor),
                Foreground = Brushes.Black,
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                Margin = margin,
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = Cursors.Hand,
                Opacity = 0, // Hidden until hover
                Padding = new Thickness(0, -2, 0, 0),
                Template = CreateCircleButtonTemplate()
            };

            // Hide the FAB when it's disabled
            var style = new Style(typeof(Button));
            var trigger = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
            trigger.Setters.Add(new Setter(UIElement.VisibilityProperty, Visibility.Collapsed));
            style.Triggers.Add(trigger);
            btn.Style = style;

            btn.Click += (s, e) => onClick(sourceTbl);
            return btn;
        }

        private static ControlTemplate CreateCircleButtonTemplate()
        {
            var template = new ControlTemplate(typeof(Button));
            var border = new FrameworkElementFactory(typeof(Border));
            border.Name = "border";
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(12));
            var contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
            contentPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(contentPresenter);
            template.VisualTree = border;

            var trigger = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
            trigger.Setters.Add(new Setter(Border.BackgroundProperty, Brushes.White, "border"));
            template.Triggers.Add(trigger);

            return template;
        }

        public static Border CreateQueryOutputCard(
            ViewDefinitionInfo viewDef, 
            double minWidth, 
            Action onPreviewSql,
            Action onRender,
            bool isDataFlowMode = false)
        {
            if (isDataFlowMode)
            {
                var card = new sqlSense.UI.Controls.TablePreviewCard
                {
                    DataContext = new sqlSense.ViewModels.Modules.TablePreviewViewModel
                    {
                        TableName = "Final Query Output",
                        IsVisible = true,
                        HideUnselectedColumns = true
                    }
                };
                
                return new Border
                {
                    Background = new SolidColorBrush(CardBg),
                    BorderBrush = new SolidColorBrush(AccentColor),
                    BorderThickness = new Thickness(1),
                    MinWidth = minWidth,
                    Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 15, Opacity = 0.4, ShadowDepth = 0 },
                    Child = card
                };
            }

            // Standard mode logic
            var panel = new StackPanel();
            var header = new Border 
            { 
                Background = new SolidColorBrush(HeaderBg),
                Padding = new Thickness(12, 8, 12, 8),
                BorderThickness = new Thickness(0, 0, 0, 1),
                BorderBrush = new SolidColorBrush(BorderColor),
                Child = new TextBlock { Text = "QUERY OUTPUT", FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(AccentColor), FontSize = 11 }
            };
            panel.Children.Add(header);

            var body = new StackPanel { Margin = new Thickness(12) };
            
            var previewBtn = new Button { Content = "PREVIEW SQL", Padding = new Thickness(12, 6, 12, 6), Background = new SolidColorBrush(AccentColor), Foreground = Brushes.Black, FontWeight = FontWeights.SemiBold, FontSize = 10, Margin = new Thickness(0, 0, 0, 8), Cursor = Cursors.Hand };
            previewBtn.Click += (s, e) => onPreviewSql();
            body.Children.Add(previewBtn);

            var renderBtn = new Button { Content = "REFRESH VIEW", Padding = new Thickness(12, 6, 12, 6), Background = Brushes.Transparent, Foreground = new SolidColorBrush(TextSecondary), BorderBrush = new SolidColorBrush(BorderColor), BorderThickness = new Thickness(1), FontSize = 10, Cursor = Cursors.Hand };
            renderBtn.Click += (s, e) => onRender();
            body.Children.Add(renderBtn);

            panel.Children.Add(body);

            return new Border
            {
                Background = new SolidColorBrush(CardBg),
                BorderBrush = new SolidColorBrush(AccentColor),
                BorderThickness = new Thickness(1),
                MinWidth = minWidth,
                Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 15, Opacity = 0.4, ShadowDepth = 0 },
                Child = panel
            };
        }
    }
}
