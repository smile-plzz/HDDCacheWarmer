using System.Security;

namespace HDDCacheWarmer.Core;

/// <summary>
/// Streams file entries from a directory tree without ever materializing the whole tree in memory,
/// so it stays cheap even for directories containing millions of files. Individual inaccessible
/// files/folders are skipped rather than aborting the walk (PRD: "never terminate because of a
/// single unreadable file").
/// </summary>
public static class FileSystemWalker
{
    public static IEnumerable<FileSystemInfo> Walk(CacheWarmerOptions options, Action<string, string>? onError = null)
    {
        var stack = new Stack<string>();
        stack.Push(options.RootPath);

        while (stack.Count > 0)
        {
            var dir = stack.Pop();

            if (options.IsFolderExcluded(dir))
                continue;

            IEnumerable<string> subDirs = Array.Empty<string>();
            IEnumerable<string> files = Array.Empty<string>();

            try
            {
                if (options.Recursive)
                    subDirs = Directory.EnumerateDirectories(dir);
                files = Directory.EnumerateFiles(dir);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or SecurityException)
            {
                onError?.Invoke(dir, ex.Message);
                continue;
            }

            if (options.Recursive)
            {
                foreach (var sub in subDirs)
                {
                    if (!options.IsFolderExcluded(sub))
                        stack.Push(sub);
                }
            }

            foreach (var filePath in files)
            {
                FileInfo info;
                try
                {
                    info = new FileInfo(filePath);
                    if (!info.Exists)
                        continue;
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or SecurityException)
                {
                    onError?.Invoke(filePath, ex.Message);
                    continue;
                }

                var ext = info.Extension;
                if (!options.ShouldIncludeExtension(string.IsNullOrEmpty(ext) ? "(none)" : ext))
                    continue;

                if (options.MaxFileSizeBytes > 0 && info.Length > options.MaxFileSizeBytes)
                    continue;

                yield return info;
            }
        }
    }

    /// <summary>
    /// Applies the requested read order. TreeOrder streams lazily; the other orders require
    /// buffering the full listing to sort, which trades memory for a deliberate read sequence.
    /// </summary>
    public static IEnumerable<FileSystemInfo> ApplyOrder(IEnumerable<FileSystemInfo> files, ReadOrder order)
    {
        return order switch
        {
            ReadOrder.TreeOrder => files,
            ReadOrder.Alphabetical => files.OrderBy(f => f.FullName, StringComparer.OrdinalIgnoreCase),
            ReadOrder.LargestFirst => files.Cast<FileInfo>().OrderByDescending(f => f.Length),
            ReadOrder.SmallestFirst => files.Cast<FileInfo>().OrderBy(f => f.Length),
            _ => files
        };
    }
}
