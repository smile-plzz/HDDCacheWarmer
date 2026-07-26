using System.IO;
using System.Windows;
using HDDCacheWarmer.Core;
// Folder picking uses System.Windows.Forms.FolderBrowserDialog (see BrowseButton_Click) to avoid
// extra NuGet dependencies. Swap in Microsoft.WindowsAPICodePack.Dialogs.CommonOpenFileDialog
// later for a more modern Explorer-style picker if desired.

namespace HDDCacheWarmer.App;

public partial class MainWindow : Window
{
    private readonly AppSettings _settings = AppSettings.Load();
    private CacheWarmerEngine? _engine;
    private CancellationTokenSource? _cts;

    public MainWindow()
    {
        InitializeComponent();

        if (!string.IsNullOrWhiteSpace(App.LaunchPath))
        {
            FolderTextBox.Text = App.LaunchPath;
            Loaded += async (_, _) => await StartWarmingAsync(App.LaunchPath!);
        }
        else if (_settings.RecentFolders.Count > 0)
        {
            FolderTextBox.Text = _settings.RecentFolders[0];
        }
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        // Uses the classic WinForms folder browser to avoid extra NuGet dependencies.
        // Swap for Microsoft.WindowsAPICodePack.Dialogs.CommonOpenFileDialog for a modern look.
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select a folder to warm",
            UseDescriptionForTitle = true
        };
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            FolderTextBox.Text = dialog.SelectedPath;
        }
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var settingsWindow = new SettingsWindow(_settings) { Owner = this };
        if (settingsWindow.ShowDialog() == true)
        {
            _settings.Save();
        }
    }

    private async void StartButton_Click(object sender, RoutedEventArgs e)
    {
        var path = FolderTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            System.Windows.MessageBox.Show(this, "Please choose a valid folder or drive.", "HDD Cache Warmer",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        await StartWarmingAsync(path);
    }

    private async Task StartWarmingAsync(string path)
    {
        _settings.AddRecentFolder(path);
        _settings.Save();

        _engine = new CacheWarmerEngine();
        _cts = new CancellationTokenSource();

        SetRunningState(isRunning: true);
        SummaryText.Text = string.Empty;

        var progress = new Progress<CacheWarmerProgress>(UpdateProgressUi);
        var options = _settings.ToEngineOptions(path);

        try
        {
            var result = await _engine.RunAsync(options, progress, _cts.Token);
            ShowCompletionSummary(result);
        }
        catch (OperationCanceledException)
        {
            PhaseText.Text = "Cancelled";
            SummaryText.Text = "Operation cancelled by user.";
        }
        catch (Exception ex)
        {
            PhaseText.Text = "Error";
            SummaryText.Text = $"Cache warming failed: {ex.Message}";
        }
        finally
        {
            SetRunningState(isRunning: false);
        }
    }

    private void UpdateProgressUi(CacheWarmerProgress p)
    {
        PhaseText.Text = p.Phase switch
        {
            WarmerPhase.Scanning => "Scanning folder...",
            WarmerPhase.Warming => "Warming cache...",
            WarmerPhase.Paused => "Paused",
            WarmerPhase.Completed => "Completed",
            WarmerPhase.Cancelled => "Cancelled",
            _ => p.Phase.ToString()
        };

        MainProgressBar.Value = p.PercentComplete;
        PercentText.Text = $"{p.PercentComplete:0.0}%";

        CurrentFileText.Text = string.IsNullOrEmpty(p.CurrentFile) ? "-" : p.CurrentFile;
        CurrentDirText.Text = string.IsNullOrEmpty(p.CurrentDirectory) ? "-" : p.CurrentDirectory;
        FilesText.Text = $"{p.FilesProcessed:N0} / {p.TotalFiles:N0}";
        BytesText.Text = $"{FormatBytes(p.BytesRead)} / {FormatBytes(p.TotalBytes)}";
        CurrentSpeedText.Text = $"{p.CurrentReadSpeedMBps:0.0} MB/s";
        AvgSpeedText.Text = $"{p.AverageReadSpeedMBps:0.0} MB/s";
        TimeText.Text = $"{p.Elapsed:hh\\:mm\\:ss} / {(p.EstimatedRemaining.HasValue ? p.EstimatedRemaining.Value.ToString(@"hh\:mm\:ss") : "--:--:--")}";
        SkippedErrorsText.Text = $"{p.SkippedFiles:N0} / {p.ErrorCount:N0}";
    }

    private void ShowCompletionSummary(CacheWarmerResult result)
    {
        PhaseText.Text = "Completed";
        SummaryText.Text =
            $"Processed {result.TotalFilesProcessed:N0} files, read {FormatBytes(result.TotalBytesRead)} " +
            $"in {result.TotalDuration:hh\\:mm\\:ss} (avg {result.AverageThroughputMBps:0.0} MB/s). " +
            $"Skipped: {result.SkippedFiles:N0}. Errors: {result.ErrorCount:N0}.";
    }

    private void PauseButton_Click(object sender, RoutedEventArgs e)
    {
        _engine?.Pause();
        PauseButton.IsEnabled = false;
        ResumeButton.IsEnabled = true;
    }

    private void ResumeButton_Click(object sender, RoutedEventArgs e)
    {
        _engine?.Resume();
        PauseButton.IsEnabled = true;
        ResumeButton.IsEnabled = false;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
    }

    private void HideButton_Click(object sender, RoutedEventArgs e)
    {
        // Minimizes to keep the warming job running in the background; a full tray-icon
        // implementation would hook a NotifyIcon here (see Future Enhancements in the PRD).
        WindowState = WindowState.Minimized;
    }

    private void SetRunningState(bool isRunning)
    {
        StartButton.IsEnabled = !isRunning;
        PauseButton.IsEnabled = isRunning;
        ResumeButton.IsEnabled = false;
        CancelButton.IsEnabled = isRunning;
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double value = bytes;
        int unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        return $"{value:0.##} {units[unit]}";
    }
}
