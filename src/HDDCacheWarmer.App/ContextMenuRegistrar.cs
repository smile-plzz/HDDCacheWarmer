using Microsoft.Win32;

namespace HDDCacheWarmer.App;

/// <summary>
/// Registers/unregisters "Warm Windows Cache" on the Explorer right-click menu for folders and
/// drives. Writes to HKEY_CURRENT_USER\Software\Classes so no administrator elevation is required,
/// satisfying the PRD's "no administrator privileges required for normal operation" constraint.
/// (Registration itself is a one-time, opt-in action the user triggers from Settings or the
/// installer -- it is still admin-free, just not part of every-day "normal operation".)
/// </summary>
public static class ContextMenuRegistrar
{
    private const string MenuText = "Warm Windows Cache";
    private const string CommandKeyName = "WarmWindowsCache";

    // Directory = right-click on a folder. Drive = right-click on a drive root in Explorer.
    private static readonly string[] TargetRoots =
    {
        @"Software\Classes\Directory\shell",
        @"Software\Classes\Drive\shell"
    };

    public static void Register()
    {
        var exePath = Environment.ProcessPath
            ?? throw new InvalidOperationException("Could not resolve the running executable path.");

        foreach (var root in TargetRoots)
        {
            using var shellKey = Registry.CurrentUser.CreateSubKey(root);
            using var cmdKey = shellKey.CreateSubKey(CommandKeyName);
            cmdKey.SetValue(string.Empty, MenuText);
            cmdKey.SetValue("Icon", $"\"{exePath}\",0");

            using var commandSubKey = cmdKey.CreateSubKey("command");
            // %1 expands to the clicked folder/drive path.
            commandSubKey.SetValue(string.Empty, $"\"{exePath}\" \"%1\"");
        }
    }

    public static void Unregister()
    {
        foreach (var root in TargetRoots)
        {
            using var shellKey = Registry.CurrentUser.OpenSubKey(root, writable: true);
            shellKey?.DeleteSubKeyTree(CommandKeyName, throwOnMissingSubKey: false);
        }
    }

    public static bool IsRegistered()
    {
        using var key = Registry.CurrentUser.OpenSubKey(
            TargetRoots[0] + "\\" + CommandKeyName);
        return key != null;
    }
}
