using System.IO;
using System.Windows;

namespace DualSync.App;

public partial class App : System.Windows.Application
{
    private Mutex? instance;
    protected override void OnStartup(StartupEventArgs e)
    {
        instance = new Mutex(true, "Local\\DualSync.Desktop", out var created);
        if (!created) { MessageBox.Show("DualSync is already running. Open it from the system tray.", "DualSync"); Shutdown(); return; }
        DispatcherUnhandledException += (_, args) =>
        {
            try { Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DualSync")); File.AppendAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DualSync", "errors.log"), $"{DateTimeOffset.Now:u} {args.Exception}\n"); } catch (IOException) { }
            MessageBox.Show("DualSync encountered an error. " + args.Exception.Message, "DualSync");
            args.Handled = true;
        };
        base.OnStartup(e);
    }
    protected override void OnExit(ExitEventArgs e) { instance?.Dispose(); base.OnExit(e); }
}
