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
    }
}
