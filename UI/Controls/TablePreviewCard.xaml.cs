using System.Windows.Controls;

namespace sqlSense.UI.Controls
{
    public partial class TablePreviewCard : UserControl
    {
        public TablePreviewCard()
        {
            InitializeComponent();
        }

        private void PreviewGrid_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            var viewModel = DataContext as ViewModels.Modules.TablePreviewViewModel;
            if (viewModel == null) return;

            string colName = e.PropertyName;
            bool isChecked = viewModel.UsedColumns.Contains(colName);

            var headerStack = new StackPanel { Orientation = Orientation.Horizontal };
            
            var check = new CheckBox
            {
                IsChecked = isChecked,
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0),
                Cursor = System.Windows.Input.Cursors.Hand,
                Style = Application.Current.FindResource(typeof(CheckBox)) as Style // Use theme style
            };

            check.Click += (s, ev) => {
                // We use Click instead of Checked/Unchecked to avoid loops during auto-generation
                viewModel.OnColumnToggle?.Invoke(colName);
            };

            var text = new TextBlock
            {
                Text = colName,
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
                FontWeight = System.Windows.FontWeights.SemiBold,
                Foreground = isChecked ? System.Windows.Media.Brushes.White : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x88, 0x88, 0x88))
            };

            headerStack.Children.Add(check);
            headerStack.Children.Add(text);

            e.Column.Header = headerStack;
        }
    }
}
