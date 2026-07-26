using System.Diagnostics;

namespace HDDCacheWarmer.Core;

/// <summary>
/// Reads every file under <see cref="CacheWarmerOptions.RootPath"/> sequentially so the Windows
/// file system cache gets populated. Supports pause/resume/cancel and periodic progress reporting.
/// Designed to run on a background thread; all callbacks arrive via the supplied IProgress instance
/// (which marshals to the UI thread when constructed with SynchronizationContext capture, as
/// System.Progress&lt;T&gt; does automatically).
/// </summary>
public sealed class CacheWarmerEngine
{
    private readonly ManualResetEventSlim _pauseGate = new(true);
    private volatile bool _isPaused;

    public bool IsPaused => _isPaused;

    public void Pause()
    {
        _isPaused = true;
        _pauseGate.Reset();
    }

    public void Resume()
    {
        _isPaused = false;
        _pauseGate.Set();
    }

    public async Task<CacheWarmerResult> RunAsync(
        CacheWarmerOptions options,
        IProgress<CacheWarmerProgress> progress,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.RootPath) || !Directory.Exists(options.RootPath))
            throw new DirectoryNotFoundException($"Path not found: {options.RootPath}");

        ApplyProcessPriority(options.Priority);

        var errors = new List<FileErrorEntry>();
        long skipped = 0;

        void RecordError(string path, string message)
        {
            Interlocked.Increment(ref skipped);
            lock (errors)
            {
                if (errors.Count < 5000) // cap memory use on pathological cases
                    errors.Add(new FileErrorEntry(path, message));
            }
        }

        // ---- Phase 1: Scan (cheap metadata-only walk to get totals for an accurate ETA) ----
        long totalFiles = 0;
        long totalBytes = 0;
        var scanStopwatch = Stopwatch.StartNew();
        var lastReport = Stopwatch.StartNew();

        var scanned = new List<FileInfo>(capacity: 4096);
        foreach (var entry in FileSystemWalker.Walk(options, RecordError))
        {
            cancellationToken.ThrowIfCancellationRequested();
            _pauseGate.Wait(cancellationToken);

            if (entry is FileInfo fi)
            {
                totalFiles++;
                totalBytes += fi.Length;
                scanned.Add(fi);

                if (lastReport.ElapsedMilliseconds > 200)
                {
                    progress.Report(new CacheWarmerProgress
                    {
                        Phase = WarmerPhase.Scanning,
                        CurrentDirectory = fi.DirectoryName ?? string.Empty,
                        TotalFiles = totalFiles,
                        TotalBytes = totalBytes,
                        Elapsed = scanStopwatch.Elapsed,
                        SkippedFiles = skipped
                    });
                    lastReport.Restart();
                }
            }
        }

        // ---- Phase 2: Warm (sequential reads) ----
        var ordered = FileSystemWalker.ApplyOrder(scanned.Cast<FileSystemInfo>(), options.ReadOrder);

        long filesProcessed = 0;
        long bytesRead = 0;
        long errorCount = 0;

        var overallStopwatch = Stopwatch.StartNew();
        var windowStopwatch = Stopwatch.StartNew();
        long bytesReadSinceLastReport = 0;
        double currentSpeedMBps = 0;

        var buffer = new byte[Math.Max(64 * 1024, options.BufferSizeBytes)];

        foreach (var entry in ordered)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_isPaused)
            {
                progress.Report(BuildProgress(WarmerPhase.Paused, entry, filesProcessed, totalFiles,
                    bytesRead, totalBytes, currentSpeedMBps, overallStopwatch, skipped, errorCount));
                _pauseGate.Wait(cancellationToken);
            }

            var file = (FileInfo)entry;
            try
            {
                using var stream = new FileStream(
                    file.FullName,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite,
                    buffer.Length,
                    FileOptions.SequentialScan);

                int read;
                while ((read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
                {
                    bytesRead += read;
                    bytesReadSinceLastReport += read;
                }

                filesProcessed++;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                errorCount++;
                RecordError(file.FullName, ex.Message);
                filesProcessed++; // still counts as "processed" (attempted)
                continue;
            }

            if (windowStopwatch.ElapsedMilliseconds > 200)
            {
                currentSpeedMBps = (bytesReadSinceLastReport / 1024.0 / 1024.0) / (windowStopwatch.ElapsedMilliseconds / 1000.0);
                bytesReadSinceLastReport = 0;
                windowStopwatch.Restart();

                progress.Report(BuildProgress(WarmerPhase.Warming, file, filesProcessed, totalFiles,
                    bytesRead, totalBytes, currentSpeedMBps, overallStopwatch, skipped, errorCount));
            }
        }

        overallStopwatch.Stop();

        var finalPhase = WarmerPhase.Completed;
        var finalProgress = BuildProgress(finalPhase, null, filesProcessed, totalFiles, bytesRead, totalBytes,
            currentSpeedMBps, overallStopwatch, skipped, errorCount);
        progress.Report(finalProgress);

        var avgThroughput = overallStopwatch.Elapsed.TotalSeconds > 0
            ? (bytesRead / 1024.0 / 1024.0) / overallStopwatch.Elapsed.TotalSeconds
            : 0;

        return new CacheWarmerResult
        {
            TotalFilesProcessed = filesProcessed,
            TotalBytesRead = bytesRead,
            TotalDuration = overallStopwatch.Elapsed,
            AverageThroughputMBps = avgThroughput,
            SkippedFiles = skipped,
            ErrorCount = errorCount,
            FinalPhase = finalPhase,
            Errors = errors
        };
    }

    private static CacheWarmerProgress BuildProgress(
        WarmerPhase phase,
        FileSystemInfo? currentFile,
        long filesProcessed,
        long totalFiles,
        long bytesRead,
        long totalBytes,
        double currentSpeedMBps,
        Stopwatch overallStopwatch,
        long skipped,
        long errorCount)
    {
        var avgSpeed = overallStopwatch.Elapsed.TotalSeconds > 0
            ? (bytesRead / 1024.0 / 1024.0) / overallStopwatch.Elapsed.TotalSeconds
            : 0;

        TimeSpan? eta = null;
        if (currentSpeedMBps > 0.01 && totalBytes > bytesRead)
        {
            var remainingMB = (totalBytes - bytesRead) / 1024.0 / 1024.0;
            eta = TimeSpan.FromSeconds(remainingMB / currentSpeedMBps);
        }

        return new CacheWarmerProgress
        {
            Phase = phase,
            CurrentFile = currentFile?.FullName ?? string.Empty,
            CurrentDirectory = (currentFile as FileInfo)?.DirectoryName ?? string.Empty,
            FilesProcessed = filesProcessed,
            TotalFiles = totalFiles,
            BytesRead = bytesRead,
            TotalBytes = totalBytes,
            CurrentReadSpeedMBps = currentSpeedMBps,
            AverageReadSpeedMBps = avgSpeed,
            Elapsed = overallStopwatch.Elapsed,
            EstimatedRemaining = eta,
            SkippedFiles = skipped,
            ErrorCount = errorCount
        };
    }

    private static void ApplyProcessPriority(ProcessPriority priority)
    {
        try
        {
            using var current = Process.GetCurrentProcess();
            current.PriorityClass = priority switch
            {
                ProcessPriority.Idle => ProcessPriorityClass.Idle,
                ProcessPriority.BelowNormal => ProcessPriorityClass.BelowNormal,
                ProcessPriority.Normal => ProcessPriorityClass.Normal,
                _ => ProcessPriorityClass.BelowNormal
            };
        }
        catch
        {
            // Non-fatal: some sandboxed contexts disallow changing priority class.
        }
    }
}
