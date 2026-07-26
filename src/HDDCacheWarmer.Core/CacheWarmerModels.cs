namespace HDDCacheWarmer.Core;

public enum WarmerPhase
{
    /// <summary>Fast pre-scan to count total files/bytes for accurate progress + ETA.</summary>
    Scanning,

    /// <summary>Actively reading file contents.</summary>
    Warming,

    Paused,
    Completed,
    Cancelled,
    Faulted
}

/// <summary>
/// Snapshot of progress, published periodically via IProgress&lt;CacheWarmerProgress&gt;.
/// Matches every field called out in the PRD's "Progress Interface" section.
/// </summary>
public sealed class CacheWarmerProgress
{
    public WarmerPhase Phase { get; init; }
    public string CurrentFile { get; init; } = string.Empty;
    public string CurrentDirectory { get; init; } = string.Empty;

    public long FilesProcessed { get; init; }
    public long TotalFiles { get; init; }

    public long BytesRead { get; init; }
    public long TotalBytes { get; init; }

    public double CurrentReadSpeedMBps { get; init; }
    public double AverageReadSpeedMBps { get; init; }

    public TimeSpan Elapsed { get; init; }
    public TimeSpan? EstimatedRemaining { get; init; }

    public long SkippedFiles { get; init; }
    public long ErrorCount { get; init; }

    public double PercentComplete =>
        TotalBytes > 0 ? Math.Min(100.0, (double)BytesRead / TotalBytes * 100.0)
        : TotalFiles > 0 ? Math.Min(100.0, (double)FilesProcessed / TotalFiles * 100.0)
        : 0.0;
}

/// <summary>Final summary shown on the "Completion" screen.</summary>
public sealed class CacheWarmerResult
{
    public long TotalFilesProcessed { get; init; }
    public long TotalBytesRead { get; init; }
    public TimeSpan TotalDuration { get; init; }
    public double AverageThroughputMBps { get; init; }
    public long SkippedFiles { get; init; }
    public long ErrorCount { get; init; }
    public WarmerPhase FinalPhase { get; init; }
    public IReadOnlyList<FileErrorEntry> Errors { get; init; } = Array.Empty<FileErrorEntry>();
}

public sealed record FileErrorEntry(string Path, string Message);
