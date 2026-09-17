using System.Windows;
using System.IO;
using StoryboardDesigner.App.Services;
using WinForms = System.Windows.Forms;

namespace StoryboardDesigner.App.Views;

public enum CreateProjectDialogMode
{
    Create,
    SaveAs
}

public partial class CreateProjectDialog : Window
{
    private readonly CreateProjectDialogMode _mode;
    private readonly List<StarterSelectionItem> _starterItems = new();
    private string? _externalStarterProjectFilePath;

    public CreateProjectDialog(CreateProjectDialogMode mode = CreateProjectDialogMode.Create, CreateProjectDialogRequest? createRequest = null)
    {
        _mode = mode;
        InitializeComponent();
        ProjectFolder = createRequest?.DefaultProjectFolder ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        ProjectFolderTextBox.Text = ProjectFolder;
        LastStarterBrowseFolder = createRequest?.DefaultStarterBrowseFolder ?? ProjectFolder;
        GameDisplayName = createRequest?.DefaultGameDisplayName?.Trim() ?? string.Empty;
        GameSummary = createRequest?.DefaultGameSummary?.Trim() ?? string.Empty;
        GamePreviewImages = (createRequest?.DefaultGamePreviewImages ?? new List<string>())
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim())
            .ToList();
        GameDisplayNameTextBox.Text = GameDisplayName;
        GameSummaryTextBox.Text = GameSummary;
        GamePreviewImagesTextBox.Text = string.Join(Environment.NewLine, GamePreviewImages);
        ConfigureForMode();

        if (_mode == CreateProjectDialogMode.Create)
        {
            ConfigureStarterSelection(createRequest);
        }
    }

    public string ProjectName { get; private set; } = string.Empty;
    public string ProjectFolder { get; private set; } = string.Empty;
    public string GameDisplayName { get; private set; } = string.Empty;
    public string GameSummary { get; private set; } = string.Empty;
    public List<string> GamePreviewImages { get; private set; } = new();
    public string? StarterProjectFilePath { get; private set; }
    public string LastStarterBrowseFolder { get; private set; } = string.Empty;

    private string DialogActionTitle => _mode == CreateProjectDialogMode.SaveAs ? "Save As" : "Create Project";

    private string BrowseDescription => _mode == CreateProjectDialogMode.SaveAs
        ? "Select where to create the copied project folder"
        : "Select where to create the project folder";

    private void ConfigureForMode()
    {
        if (_mode == CreateProjectDialogMode.SaveAs)
        {
            Title = "Save Project As";
            SubmitButton.Content = "Save As";
            InstructionsTextBlock.Text = "A new folder named after the project will be created under the selected location. The original project files are not modified.";
            StarterLabel.Visibility = Visibility.Collapsed;
            StarterComboBox.Visibility = Visibility.Collapsed;
            BrowseStarterButton.Visibility = Visibility.Collapsed;
            StarterSelectionInfoTextBlock.Visibility = Visibility.Collapsed;
            GameDisplayNameLabel.Visibility = Visibility.Collapsed;
            GameDisplayNameTextBox.Visibility = Visibility.Collapsed;
            GameSummaryLabel.Visibility = Visibility.Collapsed;
            GameSummaryTextBox.Visibility = Visibility.Collapsed;
            GamePreviewImagesLabel.Visibility = Visibility.Collapsed;
            GamePreviewImagesTextBox.Visibility = Visibility.Collapsed;
        }
        else
        {
            Title = "Create New Project";
            SubmitButton.Content = "Create";
            InstructionsTextBlock.Text = "A new folder named after the project will be created under the selected location, then populated from the selected starter (or left blank).";
            StarterLabel.Visibility = Visibility.Visible;
            StarterComboBox.Visibility = Visibility.Visible;
            BrowseStarterButton.Visibility = Visibility.Visible;
            GameDisplayNameLabel.Visibility = Visibility.Visible;
            GameDisplayNameTextBox.Visibility = Visibility.Visible;
            GameSummaryLabel.Visibility = Visibility.Visible;
            GameSummaryTextBox.Visibility = Visibility.Visible;
            GamePreviewImagesLabel.Visibility = Visibility.Visible;
            GamePreviewImagesTextBox.Visibility = Visibility.Visible;
        }
    }

    private void ConfigureStarterSelection(CreateProjectDialogRequest? createRequest)
    {
        _starterItems.Clear();
        _starterItems.Add(new StarterSelectionItem
        {
            DisplayName = "Blank (No Starter)",
            ProjectFilePath = null
        });

        if (createRequest is not null)
        {
            foreach (var option in createRequest.BuiltInStarterProjects)
            {
                _starterItems.Add(new StarterSelectionItem
                {
                    DisplayName = option.DisplayName,
                    ProjectFilePath = option.ProjectFilePath
                });
            }
        }

        StarterComboBox.ItemsSource = _starterItems;
        StarterComboBox.SelectedIndex = 0;
        StarterSelectionInfoTextBlock.Visibility = Visibility.Collapsed;
    }

    private void Browse_OnClick(object sender, RoutedEventArgs e)
    {
        using var dialog = new WinForms.FolderBrowserDialog
        {
            Description = BrowseDescription,
            UseDescriptionForTitle = true,
            InitialDirectory = ProjectFolder
        };

        if (dialog.ShowDialog() == WinForms.DialogResult.OK)
        {
            ProjectFolder = dialog.SelectedPath;
            ProjectFolderTextBox.Text = ProjectFolder;
        }
    }

    private void BrowseStarter_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Storyboard project (*.sbe.json)|*.sbe.json|JSON files (*.json)|*.json",
            Title = "Select Starter Project",
            InitialDirectory = LastStarterBrowseFolder
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        LastStarterBrowseFolder = Path.GetDirectoryName(dialog.FileName) ?? LastStarterBrowseFolder;
        var displayName = BuildExternalStarterDisplayName(dialog.FileName);
        var message = $"Use externally selected starter for this create only?{Environment.NewLine}{Environment.NewLine}Project: {displayName}{Environment.NewLine}Path: {dialog.FileName}";
        var confirm = System.Windows.MessageBox.Show(
            this,
            message,
            "Use External Starter",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        _externalStarterProjectFilePath = dialog.FileName;
        StarterSelectionInfoTextBlock.Text = $"Using external starter for this create only: {displayName}";
        StarterSelectionInfoTextBlock.Visibility = Visibility.Visible;
    }

    private void StarterComboBox_OnSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        // Selecting from built-in list clears any temporary external override.
        _externalStarterProjectFilePath = null;
        StarterSelectionInfoTextBlock.Visibility = Visibility.Collapsed;
    }

    private void Create_OnClick(object sender, RoutedEventArgs e)
    {
        var name = ProjectNameTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            System.Windows.MessageBox.Show(this, "Please enter a project name.", DialogActionTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (string.IsNullOrWhiteSpace(ProjectFolder))
        {
            System.Windows.MessageBox.Show(this, "Please select a folder location.", DialogActionTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        ProjectName = name;
        GameDisplayName = GameDisplayNameTextBox.Text.Trim();
        GameSummary = GameSummaryTextBox.Text.Trim();
        GamePreviewImages = ParsePreviewImages(GamePreviewImagesTextBox.Text);
        StarterProjectFilePath = ResolveSelectedStarterProjectPath();
        DialogResult = true;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private string? ResolveSelectedStarterProjectPath()
    {
        if (_mode != CreateProjectDialogMode.Create)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(_externalStarterProjectFilePath))
        {
            return _externalStarterProjectFilePath;
        }

        return (StarterComboBox.SelectedItem as StarterSelectionItem)?.ProjectFilePath;
    }

    private static string BuildExternalStarterDisplayName(string projectFilePath)
    {
        try
        {
            using var stream = File.OpenRead(projectFilePath);
            using var doc = System.Text.Json.JsonDocument.Parse(stream);
            if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object
                && doc.RootElement.TryGetProperty("name", out var nameProperty)
                && nameProperty.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                var name = nameProperty.GetString()?.Trim();
                if (!string.IsNullOrWhiteSpace(name))
                {
                    return name;
                }
            }
        }
        catch
        {
        }

        return Path.GetFileNameWithoutExtension(projectFilePath);
    }

    private static List<string> ParsePreviewImages(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
        {
            return new List<string>();
        }

        return rawText
            .Split(["\r\n", "\n", ",", ";"], StringSplitOptions.RemoveEmptyEntries)
            .Select(static token => token.Trim())
            .Where(static token => !string.IsNullOrWhiteSpace(token))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private sealed class StarterSelectionItem
    {
        public required string DisplayName { get; init; }

        public required string? ProjectFilePath { get; init; }
    }
}
