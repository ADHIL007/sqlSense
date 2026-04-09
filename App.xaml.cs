using System.Configuration;
using System.Data;
using System.Windows;

namespace sqlSense
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            if (e.Args.Length > 0)
            {
                // Store the file path in properties so MainWindow can access it after initialization
                this.Properties["FilePath"] = e.Args[0];
            }
        }
    }

}
