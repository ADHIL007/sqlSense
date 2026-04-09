using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using sqlSense.Models;

namespace sqlSense.UI
{
    /// <summary>
    /// Partial class for ViewGraphRenderer: node drag behavior,
    /// hover-based flow highlighting, and entrance animations.
    /// </summary>
    public partial class ViewGraphRenderer
    {
        /// <summary>
        /// Sets up drag behavior and hover flow highlighting for a node card.
        /// </summary>
        private void SetupNodeDrag(Border card, NodeCard node)
        {
            card.Cursor = Cursors.Hand;

            card.MouseEnter += (s, e) =>
            {
                _hoveredNode = node;
                UpdateAllFlowAnimations();
            };
            card.MouseLeave += (s, e) =>
            {
                if (_hoveredNode == node)
                {
                    _hoveredNode = null;
                    UpdateAllFlowAnimations();
                }
            };

            card.MouseLeftButtonDown += (s, e) =>
            {
                IsDraggingNode = true;
                _draggedNode = node;
                _dragStart = e.GetPosition(_flowCanvas);
                _dragNodeStartX = node.X;
                _dragNodeStartY = node.Y;
                card.Cursor = Cursors.SizeAll;
                card.CaptureMouse();
                e.Handled = true;
            };

            card.MouseMove += (s, e) =>
            {
                if (!IsDraggingNode || _draggedNode != node) return;
                var cur = e.GetPosition(_flowCanvas);
                node.X = _dragNodeStartX + (cur.X - _dragStart.X);
                node.Y = _dragNodeStartY + (cur.Y - _dragStart.Y);
                
                // Persist position for re-renders and model saving
                _nodePositionCache[node.Id] = new Point(node.X, node.Y);
                if (_viewModel.Canvas.CurrentViewDefinition != null)
                {
                    _viewModel.Canvas.CurrentViewDefinition.NodePositions[node.Id] = (node.X, node.Y);
                }

                Canvas.SetLeft(card, node.X);
                Canvas.SetTop(card, node.Y);
                foreach (var c in node.OutputConnections) UpdateConnectionPath(c);
                foreach (var c in node.InputConnections) UpdateConnectionPath(c);
            };

            card.MouseLeftButtonUp += (s, e) =>
            {
                if (!IsDraggingNode) return;
                IsDraggingNode = false;
                _draggedNode = null;
                card.Cursor = Cursors.Hand;
                card.ReleaseMouseCapture();
                e.Handled = true;
            };
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
