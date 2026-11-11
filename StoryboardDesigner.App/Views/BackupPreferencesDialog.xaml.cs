using System.IO;
using System.Windows;
using StoryboardDesigner.App.Services;
using WinForms = System.Windows.Forms;

namespace StoryboardDesigner.App.Views;

public partial class BackupPreferencesDialog : Window
{
    public BackupPreferencesDialog(BackupPreferences preferences)
    {
        InitializeComponent();
        BackupIntervalHoursTextBox.Text = preferences.BackupIntervalHours.ToString();
        MaximumBackupCountTextBox.Text = preferences.MaximumBackupCount.ToString();
        BackupRootPathTextBox.Text = string.IsNullOrWhiteSpace(preferences.BackupRootPath)
            ? BackupPreferencesService.DefaultBackupRootPath
            : preferences.BackupRootPath;
    }

    public BackupPreferences Preferences { get; private set; } = new();

    private void Browse_OnClick(object sender, RoutedEventArgs e)
    {
        using var dialog = new WinForms.FolderBrowserDialog
        {
            Description = "Choose the folder where Storyboard Designer stores project backups.",
            SelectedPath = Directory.Exists(BackupRootPathTextBox.Text)
                ? BackupRootPathTextBox.Text
                : BackupPreferencesService.DefaultBackupRootPath,
            ShowNewFolderButton = true
        };

        if (dialog.ShowDialog() == WinForms.DialogResult.OK)
        {
            BackupRootPathTextBox.Text = dialog.SelectedPath;
        }
    }

    private void Save_OnClick(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(BackupIntervalHoursTextBox.Text.Trim(), out var intervalHours)
            || intervalHours is < 2 or > 24)
        {
            System.Windows.MessageBox.Show(this, "Enter a backup interval from 2 through 24 hours.", "Invalid Backup Interval", MessageBoxButton.OK, MessageBoxImage.Information);
            BackupIntervalHoursTextBox.Focus();
            BackupIntervalHoursTextBox.SelectAll();
            return;
        }

        if (!int.TryParse(MaximumBackupCountTextBox.Text.Trim(), out var maximumBackupCount)
            || maximumBackupCount < 1)
        {
            System.Windows.MessageBox.Show(this, "Enter a maximum backup count of at least 1.", "Invalid Retention Count", MessageBoxButton.OK, MessageBoxImage.Information);
            MaximumBackupCountTextBox.Focus();
            MaximumBackupCountTextBox.SelectAll();
            return;
        }

        var backupRootPath = BackupRootPathTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(backupRootPath))
        {
            System.Windows.MessageBox.Show(this, "Choose a folder for project backups.", "Backup Folder Required", MessageBoxButton.OK, MessageBoxImage.Information);
            BackupRootPathTextBox.Focus();
            return;
        }

        try
        {
            backupRootPath = Path.GetFullPath(backupRootPath);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(this, $"The backup folder path is invalid: {ex.Message}", "Invalid Backup Folder", MessageBoxButton.OK, MessageBoxImage.Information);
            BackupRootPathTextBox.Focus();
            BackupRootPathTextBox.SelectAll();
            return;
        }

        Preferences = new BackupPreferences
        {
            BackupIntervalHours = intervalHours,
            MaximumBackupCount = maximumBackupCount,
            BackupRootPath = backupRootPath
        };
        DialogResult = true;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e) => DialogResult = false;
}
