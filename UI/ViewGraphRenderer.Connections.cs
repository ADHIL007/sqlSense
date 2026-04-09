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

namespace sqlSense.UI
{
    /// <summary>
    /// Partial class for ViewGraphRenderer: connection creation,
    /// bezier path updates, join badges, port circles, and node removal.
    /// </summary>
    public partial class ViewGraphRenderer
    {
        // Standard connection colors
        private static readonly Color ConnGray = Color.FromRgb(0x88, 0x88, 0x88);
        private static readonly Color ConnBorder = Color.FromRgb(0x3C, 0x3C, 0x3C);
        private static readonly Color ConnCardBg = Color.FromRgb(0x1E, 0x1E, 0x22);

        private void CreateNodeConnection(NodeCard src, NodeCard tgt, JoinRelationship? join, Color color)
        {
            var strokeBrush = new SolidColorBrush(color);

            var p = new Path
            {
                Stroke = strokeBrush,
                StrokeThickness = 1.5,
                Opacity = 0.6,
                IsHitTestVisible = false
            };
            var flowP = new Path
            {
                Stroke = new SolidColorBrush(Color.FromRgb(0x4F, 0xC3, 0xF7)),
                StrokeThickness = 2.5,
                Opacity = 1.0,
                IsHitTestVisible = false,
                Visibility = Visibility.Hidden
            };
            flowP.StrokeDashArray = new DoubleCollection { 4, 4 };

            _flowCanvas.Children.Add(p);
            _viewVisualizationElements.Add(p);
            _flowCanvas.Children.Add(flowP);
            _viewVisualizationElements.Add(flowP);

            // Port circles at connection endpoints
            var portBrush = new SolidColorBrush(color);
            var portStroke = new SolidColorBrush(ConnCardBg);

            var startCircle = new Ellipse
            {
                Width = 8,
                Height = 8,
                Fill = portBrush,
                Stroke = portStroke,
                StrokeThickness = 2,
                IsHitTestVisible = false
            };
            var endCircle = new Ellipse
            {
                Width = 8,
                Height = 8,
                Fill = portBrush,
                Stroke = portStroke,
                StrokeThickness = 2,
                IsHitTestVisible = false
            };

            _flowCanvas.Children.Add(startCircle);
            _viewVisualizationElements.Add(startCircle);
            _flowCanvas.Children.Add(endCircle);
            _viewVisualizationElements.Add(endCircle);

            Border? badge = null;
            if (join != null)
            {
                badge = CreateJoinBadge(join);
                _flowCanvas.Children.Add(badge);
                _viewVisualizationElements.Add(badge);
            }

            var conn = new NodeConnection
            {
                Source = src,
                Target = tgt,
                PathElement = p,
                FlowPathElement = flowP,
                StartPort = startCircle,
                EndPort = endCircle,
                LabelBadge = badge,
                JoinData = join,
                Color = color
            };

            src.OutputConnections.Add(conn);
            tgt.InputConnections.Add(conn);
            _nodeConnections.Add(conn);
            UpdateConnectionPath(conn);
        }

        /// <summary>
        /// Creates a standardized, interactive join badge positioned at the midpoint of a connection.
        /// </summary>
        private Border CreateJoinBadge(JoinRelationship join)
        {
            var badgeBtn = new Button
            {
                Content = join.JoinType,
                FontSize = 9,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(6, 2, 6, 2),
                Cursor = Cursors.Hand,
                ToolTip = "Click to cycle join type"
            };
            badgeBtn.Click += (s, e) =>
            {
                join.JoinType = join.JoinType.ToUpper() switch
                {
                    "INNER" => "LEFT",
                    "LEFT" => "RIGHT",
                    "RIGHT" => "FULL",
                    _ => "INNER"
                };
                RenderViewVisualization(_viewModel!.Canvas.CurrentViewDefinition!);
            };

            var conditionText = new TextBlock
            {
                Text = $"{join.LeftColumn} = {join.RightColumn}",
                FontSize = 8,
                Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)),
                Margin = new Thickness(6, 0, 6, 3),
                FontFamily = new FontFamily("Consolas")
            };

            var badgePanel = new StackPanel();
            badgePanel.Children.Add(badgeBtn);
            badgePanel.Children.Add(conditionText);

            return new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0xF0, 0x25, 0x25, 0x26)),
                BorderBrush = new SolidColorBrush(ConnBorder),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Child = badgePanel
            };
        }

        /// <summary>
        /// Updates the bezier curve path and port positions for a connection.
        /// </summary>
        private void UpdateConnectionPath(NodeConnection conn)
        {
            double sW = conn.Source.CardElement.ActualWidth > 0 ? conn.Source.CardElement.ActualWidth : conn.Source.Width;
            double sH = conn.Source.CardElement.ActualHeight > 0 ? conn.Source.CardElement.ActualHeight : conn.Source.Height;
            double sx = conn.Source.X + sW;
            double sy = conn.Source.Y + sH / 2;

            double tW = conn.Target.CardElement.ActualWidth > 0 ? conn.Target.CardElement.ActualWidth : conn.Target.Width;
            double tH = conn.Target.CardElement.ActualHeight > 0 ? conn.Target.CardElement.ActualHeight : conn.Target.Height;
            double tx = conn.Target.X;
            double ty = conn.Target.Y + tH / 2;

            double dist = Math.Abs(tx - sx);
            var fig = new PathFigure { StartPoint = new Point(sx, sy), IsClosed = false };
            fig.Segments.Add(new BezierSegment(
                new Point(sx + dist * 0.5, sy),
                new Point(tx - dist * 0.5, ty),
                new Point(tx, ty), true));
            var geo = new PathGeometry(new[] { fig });
            conn.PathElement.Data = geo;
            conn.FlowPathElement.Data = geo;

            if (conn.StartPort != null)
            {
                Canvas.SetLeft(conn.StartPort, sx - conn.StartPort.Width / 2);
                Canvas.SetTop(conn.StartPort, sy - conn.StartPort.Height / 2);
            }
            if (conn.EndPort != null)
            {
                Canvas.SetLeft(conn.EndPort, tx - conn.EndPort.Width / 2);
                Canvas.SetTop(conn.EndPort, ty - conn.EndPort.Height / 2);
            }
            if (conn.LabelBadge != null)
            {
                conn.LabelBadge.Measure(new Size(1000, 1000));
                Canvas.SetLeft(conn.LabelBadge, (sx + tx) / 2 - conn.LabelBadge.DesiredSize.Width / 2);
                Canvas.SetTop(conn.LabelBadge, (sy + ty) / 2 - conn.LabelBadge.DesiredSize.Height / 2);
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  NODE & CONNECTION REMOVAL
        // ═══════════════════════════════════════════════════════════════

        private void RemoveNodeAndConnections(NodeCard node)
        {
            // Remove all input connections
            foreach (var c in node.InputConnections.ToList())
            {
                _flowCanvas.Children.Remove(c.PathElement);
                _flowCanvas.Children.Remove(c.FlowPathElement);
                _viewVisualizationElements.Remove(c.PathElement);
                _viewVisualizationElements.Remove(c.FlowPathElement);
                if (c.LabelBadge != null)
                {
                    _flowCanvas.Children.Remove(c.LabelBadge);
                    _viewVisualizationElements.Remove(c.LabelBadge);
                }
                c.Source.OutputConnections.Remove(c);
                _nodeConnections.Remove(c);
            }
            // Remove all output connections
            foreach (var c in node.OutputConnections.ToList())
            {
                _flowCanvas.Children.Remove(c.PathElement);
                _flowCanvas.Children.Remove(c.FlowPathElement);
                _viewVisualizationElements.Remove(c.PathElement);
                _viewVisualizationElements.Remove(c.FlowPathElement);
                if (c.LabelBadge != null)
                {
                    _flowCanvas.Children.Remove(c.LabelBadge);
                    _viewVisualizationElements.Remove(c.LabelBadge);
                }
                c.Target.InputConnections.Remove(c);
                _nodeConnections.Remove(c);
            }
            // Remove card element
            _flowCanvas.Children.Remove(node.CardElement);
            _viewVisualizationElements.Remove(node.CardElement);
            _nodeCards.Remove(node);
        }
    }
}
