using System;
using System.Windows;
using System.Windows.Controls;

namespace sqlSense.UI.Controls
{
    public partial class CanvasToolbar : UserControl
    {
        /// <summary>
        /// Fired when the user clicks one of the toolbar buttons.
        /// The string argument is the shape type key.
        /// </summary>
        public event Action<string>? ShapeRequested;

        public CanvasToolbar()
        {
            InitializeComponent();
        }

        private void TableBtn_Click(object sender, RoutedEventArgs e)
            => ShapeRequested?.Invoke("Table");

        private void ViewBtn_Click(object sender, RoutedEventArgs e)
            => ShapeRequested?.Invoke("View");

        private void ModeToggleBtn_Click(object sender, RoutedEventArgs e)
            => ShapeRequested?.Invoke("ToggleMode");

        /// <summary>
        /// Switches the mode toggle button icon to reflect the current state.
        /// </summary>
        /// <param name="isDataFlowMode">True if currently in data flow mode, false for control flow mode.</param>
        public void SetModeIcon(bool isDataFlowMode)
        {
            var iconKey = isDataFlowMode ? "DataToControlSwitchIcon" : "ControlToDataSwitchIcon";
            var tooltip = isDataFlowMode ? "Switch to Control Flow" : "Switch to Data Flow";

            if (Application.Current.TryFindResource(iconKey) is System.Windows.Controls.ControlTemplate template)
            {
                // Find the Control child inside the button
                if (ModeToggleBtn.Content is System.Windows.Controls.Control iconControl)
                {
                    iconControl.Template = template;
                }
            }
            ModeToggleBtn.ToolTip = tooltip;
        }
        private void VariableBtn_Click(object sender, RoutedEventArgs e)
            => ShapeRequested?.Invoke("Variable");

        private void IfElseBtn_Click(object sender, RoutedEventArgs e)
            => ShapeRequested?.Invoke("IfElse");

        private void WhileBtn_Click(object sender, RoutedEventArgs e)
            => ShapeRequested?.Invoke("While");

        private void ExecuteBtn_Click(object sender, RoutedEventArgs e)
            => ShapeRequested?.Invoke("Execute");

        private void TryCatchBtn_Click(object sender, RoutedEventArgs e)
            => ShapeRequested?.Invoke("TryCatch");

        private void CommentBtn_Click(object sender, RoutedEventArgs e)
            => ShapeRequested?.Invoke("Comment");

        private void TextBtn_Click(object sender, RoutedEventArgs e)
            => ShapeRequested?.Invoke("Text");

        private void ArrangeBtn_Click(object sender, RoutedEventArgs e)
            => ShapeRequested?.Invoke("Arrange");
    }
}
