using System.Windows;
using HDDCacheWarmer.Core;

namespace HDDCacheWarmer.App;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;

    public SettingsWindow(AppSettings settings)
    {
        InitializeComponent();
        _settings = settings;

        ReadOrderBox.ItemsSource = Enum.GetValues(typeof(ReadOrder));
        PriorityBox.ItemsSource = Enum.GetValues(typeof(ProcessPriority));

        BufferSizeBox.Text = (_settings.BufferSizeBytes / (1024 * 1024)).ToString();
        ReadOrderBox.SelectedItem = _settings.ReadOrder;
        PriorityBox.SelectedItem = _settings.Priority;
        ExtensionsBox.Text = string.Join(",", _settings.IncludeExtensions);
        ExcludedFoldersBox.Text = string.Join(",", _settings.ExcludedFolders);
        MaxFileSizeBox.Text = (_settings.MaxFileSizeBytes / (1024 * 1024)).ToString();
        IdleOnlyBox.IsChecked = _settings.IdleOnly;
        TrayStartupBox.IsChecked = _settings.RunAtStartupInTray;
        ContextMenuBox.IsChecked = ContextMenuRegistrar.IsRegistered();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (int.TryParse(BufferSizeBox.Text, out var bufMb) && bufMb > 0)
            _settings.BufferSizeBytes = bufMb * 1024 * 1024;

        if (ReadOrderBox.SelectedItem is ReadOrder order)
            _settings.ReadOrder = order;

        if (PriorityBox.SelectedItem is ProcessPriority priority)
            _settings.Priority = priority;

        _settings.IncludeExtensions = SplitCsv(ExtensionsBox.Text);
        _settings.ExcludedFolders = SplitCsv(ExcludedFoldersBox.Text);

        if (long.TryParse(MaxFileSizeBox.Text, out var maxMb) && maxMb >= 0)
            _settings.MaxFileSizeBytes = maxMb * 1024 * 1024;

        _settings.IdleOnly = IdleOnlyBox.IsChecked == true;
        _settings.RunAtStartupInTray = TrayStartupBox.IsChecked == true;

        try
        {
            var wantRegistered = ContextMenuBox.IsChecked == true;
            if (wantRegistered && !ContextMenuRegistrar.IsRegistered())
                ContextMenuRegistrar.Register();
            else if (!wantRegistered && ContextMenuRegistrar.IsRegistered())
                ContextMenuRegistrar.Unregister();

            _settings.ContextMenuRegistered = wantRegistered;
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(this, $"Could not update the Explorer context menu: {ex.Message}",
                "HDD Cache Warmer", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private static List<string> SplitCsv(string text) =>
        text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
}
