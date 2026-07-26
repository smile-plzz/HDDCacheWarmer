using System.Windows;

namespace HDDCacheWarmer.App;

public partial class App : System.Windows.Application
{
    /// <summary>Folder/drive path passed in from the Explorer context menu ("%1"), if any.</summary>
    public static string? LaunchPath { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (e.Args.Length > 0 && !string.IsNullOrWhiteSpace(e.Args[0]))
        {
            var arg = e.Args[0].Trim('"');

            // Silent installer/uninstaller hooks -- see installer\HDDCacheWarmer.iss.
            if (string.Equals(arg, "--register-context-menu", StringComparison.OrdinalIgnoreCase))
            {
                ContextMenuRegistrar.Register();
                Shutdown();
                return;
            }
            if (string.Equals(arg, "--unregister-context-menu", StringComparison.OrdinalIgnoreCase))
            {
                ContextMenuRegistrar.Unregister();
                Shutdown();
                return;
            }

            LaunchPath = arg;
        }
    }
}
