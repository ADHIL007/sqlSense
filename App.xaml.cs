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
        private const string UniqueEventName = "sqlSense_UniqueInstance_Event";
        private const string PipeName = "sqlSense_Communication_Pipe";
        private static System.Threading.Mutex? _mutex;

        protected override void OnStartup(StartupEventArgs e)
        {
            _mutex = new System.Threading.Mutex(true, UniqueEventName, out bool isNewInstance);

            if (!isNewInstance)
            {
                // Send arguments to the existing instance
                if (e.Args.Length > 0)
                {
                    SendArgsToExistingInstance(e.Args[0]);
                }
                Application.Current.Shutdown();
                return;
            }

            base.OnStartup(e);

            if (e.Args.Length > 0)
            {
                this.Properties["FilePath"] = e.Args[0];
            }
        }

        private void SendArgsToExistingInstance(string filePath)
        {
            try
            {
                using var client = new System.IO.Pipes.NamedPipeClientStream(".", PipeName, System.IO.Pipes.PipeDirection.Out);
                client.Connect(500); // 500ms timeout
                using var writer = new System.IO.StreamWriter(client);
                writer.WriteLine(filePath);
                writer.Flush();
            }
            catch
            {
                // If communication fails, just exit quietly
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _mutex?.ReleaseMutex();
            _mutex?.Dispose();
            base.OnExit(e);
        }
    }

}
