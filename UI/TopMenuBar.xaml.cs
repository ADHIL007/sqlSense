using System.Windows;
using System.Windows.Controls;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using sqlSense.UI.MenueItems.Settings;

namespace sqlSense.UI
{
    public partial class TopMenuBar : UserControl
    {
        public TopMenuBar()
        {
            InitializeComponent();
        }

        private void MenuItem_Click(object sender, System.Windows.RoutedEventArgs e)
        {

        }

        private void onOptionClick(object sender, System.Windows.RoutedEventArgs e)
        {
            var owner = Window.GetWindow(this);

            var dialog = new optionsDialog
            {
                Owner = owner,
                Width = 850,
                Height = 600
            };
            dialog.ShowDialog();
        }


    }
}
