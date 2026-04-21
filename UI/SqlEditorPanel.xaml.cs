using System.Windows.Controls;

namespace sqlSense.UI
{
    public partial class SqlEditorPanel : UserControl
    {
        public SqlEditorPanel()
        {
            InitializeComponent();
            this.Loaded += SqlEditorPanel_Loaded;
        }

        private void SqlEditorPanel_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            UpdateLineNumbers();
        }

        private void EditorTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateLineNumbers();
        }

        private void UpdateLineNumbers()
        {
            if (EditorTextBox == null || LineNumbers == null) return;
            
            int lineCount = EditorTextBox.LineCount;
            if (lineCount == 0) lineCount = 1;

            var sb = new System.Text.StringBuilder();
            for (int i = 1; i <= lineCount; i++)
            {
                sb.AppendLine(i.ToString());
            }
            
            LineNumbers.Text = sb.ToString().TrimEnd('\r', '\n');
        }

        private void EditorTextBox_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (LineNumbersScroll != null)
            {
                LineNumbersScroll.ScrollToVerticalOffset(e.VerticalOffset);
            }
        }
    }
}
