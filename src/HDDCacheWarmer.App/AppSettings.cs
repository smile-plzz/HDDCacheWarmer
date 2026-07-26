using System.IO;
using System.Text.Json;
using HDDCacheWarmer.Core;

namespace HDDCacheWarmer.App;

/// <summary>Persisted user settings, saved to %AppData%\HDDCacheWarmer\settings.json.</summary>
public sealed class AppSettings
{
    public int BufferSizeBytes { get; set; } = 4 * 1024 * 1024;
    public ReadOrder ReadOrder { get; set; } = ReadOrder.TreeOrder;
    public List<string> IncludeExtensions { get; set; } = new();
    public List<string> ExcludedFolders { get; set; } = new() { ".git", "node_modules", "$RECYCLE.BIN", "System Volume Information" };
    public ProcessPriority Priority { get; set; } = ProcessPriority.BelowNormal;
    public long MaxFileSizeBytes { get; set; } = 0;
    public bool IdleOnly { get; set; } = false;
    public bool RunAtStartupInTray { get; set; } = false;
    public bool ContextMenuRegistered { get; set; } = false;

    public List<string> RecentFolders { get; set; } = new();
    public List<string> FavoriteFolders { get; set; } = new();
    public List<ScheduledWarmEntry> ScheduledWarms { get; set; } = new();
    public List<string> AutoWarmOnDriveConnect { get; set; } = new();

    private static string SettingsPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "HDDCacheWarmer", "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                if (loaded != null) return loaded;
            }
        }
        catch
        {
            // Fall back to defaults if the file is corrupt/unreadable.
        }
        return new AppSettings();
    }

    public void Save()
    {
        var dir = Path.GetDirectoryName(SettingsPath)!;
        Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SettingsPath, json);
    }

    public void AddRecentFolder(string path)
    {
        RecentFolders.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        RecentFolders.Insert(0, path);
        if (RecentFolders.Count > 15) RecentFolders.RemoveRange(15, RecentFolders.Count - 15);
    }

    public CacheWarmerOptions ToEngineOptions(string rootPath) => new()
    {
        RootPath = rootPath,
        Recursive = true,
        BufferSizeBytes = BufferSizeBytes,
        ReadOrder = ReadOrder,
        IncludeExtensions = new HashSet<string>(IncludeExtensions, StringComparer.OrdinalIgnoreCase),
        ExcludedFolders = new HashSet<string>(ExcludedFolders, StringComparer.OrdinalIgnoreCase),
        Priority = Priority,
        MaxFileSizeBytes = MaxFileSizeBytes,
        IdleOnly = IdleOnly
    };
}

public sealed record ScheduledWarmEntry(string FolderPath, DayOfWeek[] Days, TimeSpan TimeOfDay);
