using System.Windows;
using System.Windows.Controls;

namespace sqlSense.UI.Controls.Database
{
    public partial class ObjectExplorerPanel : UserControl
    {
        public event RoutedEventHandler? ItemExpanded;
        public event RoutedPropertyChangedEventHandler<object>? SelectedItemChanged;

        public ObjectExplorerPanel()
        {
            InitializeComponent();
        }

        private void TreeViewItem_Expanded(object sender, RoutedEventArgs e)
        {
            ItemExpanded?.Invoke(sender, e);
        }

        private void TreeViewItem_RequestBringIntoView(object sender, RequestBringIntoViewEventArgs e)
        {
            // Completely suppress the bring into view behavior on expand
            e.Handled = true;
        }

        private void ObjectExplorer_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            // Only bring selected item into view vertically, not horizontally
            if (e.NewValue != null)
            {
                var container = ObjectExplorer.ItemContainerGenerator.ContainerFromItem(e.NewValue) as TreeViewItem;
                if (container != null)
                {
                    container.BringIntoView(new Rect(0, 0, 0, container.ActualHeight));
                }
            }

            SelectedItemChanged?.Invoke(sender, e);
        }
    }
}