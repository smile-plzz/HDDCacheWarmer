namespace HDDCacheWarmer.Core;

/// <summary>
/// Determines the order in which files are read.
/// </summary>
public enum ReadOrder
{
    /// <summary>Directory tree order (fastest to enumerate, good default for HDDs since it roughly follows on-disk layout for freshly written data).</summary>
    TreeOrder,

    /// <summary>Alphabetical by full path.</summary>
    Alphabetical,

    /// <summary>Largest files first.</summary>
    LargestFirst,

    /// <summary>Smallest files first (fastest way to bump the "files processed" counter / warm many small files quickly).</summary>
    SmallestFirst
}

/// <summary>
/// User-configurable options for a cache warming run. Mirrors the "Settings" section of the PRD.
/// </summary>
public sealed class CacheWarmerOptions
{
    /// <summary>Root folder (or drive root) to warm.</summary>
    public string RootPath { get; set; } = string.Empty;

    /// <summary>Whether to recurse into subdirectories.</summary>
    public bool Recursive { get; set; } = true;

    /// <summary>Read buffer size in bytes. Larger buffers generally help sequential HDD throughput up to a point.</summary>
    public int BufferSizeBytes { get; set; } = 4 * 1024 * 1024; // 4 MB default

    /// <summary>Order in which files are visited.</summary>
    public ReadOrder ReadOrder { get; set; } = ReadOrder.TreeOrder;

    /// <summary>
    /// File extension filter. If non-empty, only files whose extension (including the leading dot,
    /// e.g. ".mp4") appear in this set are read. Case-insensitive. Empty set = no filter (read everything).
    /// </summary>
    public HashSet<string> IncludeExtensions { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Folder names or absolute paths to exclude from traversal entirely.</summary>
    public HashSet<string> ExcludedFolders { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Process priority class to run the warming work at, so it does not starve foreground apps.</summary>
    public ProcessPriority Priority { get; set; } = ProcessPriority.BelowNormal;

    /// <summary>Skip files larger than this size, in bytes. 0 = no limit.</summary>
    public long MaxFileSizeBytes { get; set; } = 0;

    /// <summary>Only run when the system is idle (used by the scheduler / tray auto-warm feature).</summary>
    public bool IdleOnly { get; set; } = false;

    public bool ShouldIncludeExtension(string extension)
        => IncludeExtensions.Count == 0 || IncludeExtensions.Contains(extension);

    public bool IsFolderExcluded(string folderFullPath)
    {
        foreach (var excluded in ExcludedFolders)
        {
            if (string.Equals(folderFullPath, excluded, StringComparison.OrdinalIgnoreCase))
                return true;

            // also exclude by bare folder name (e.g. "node_modules", ".git")
            var name = Path.GetFileName(folderFullPath.TrimEnd(Path.DirectorySeparatorChar));
            if (string.Equals(name, excluded, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}

public enum ProcessPriority
{
    Idle,
    BelowNormal,
    Normal
}
