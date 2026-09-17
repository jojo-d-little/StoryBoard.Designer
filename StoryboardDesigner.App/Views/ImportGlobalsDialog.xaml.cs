using System.Windows;
using StoryboardDesigner.App.Services;

namespace StoryboardDesigner.App.Views;

public partial class ImportGlobalsDialog : Window
{
    public GlobalImportSelectionResult Selection { get; private set; } = new();

    public ImportGlobalsDialog(GlobalImportSelectionRequest request)
    {
        InitializeComponent();

        SourcePathText.Text = $"Source: {request.SourceProjectPath}";

        ConfigureCheckBox(VerbsCheckBox, "Verbs", request.VerbCount);
        ConfigureCheckBox(DirectionalsCheckBox, "Directionals", request.DirectionalCount);
        ConfigureCheckBox(GlobalActionsCheckBox, "Global Actions", request.GlobalActionCount);
        ConfigureCheckBox(GlobalSoundEffectsCheckBox, "Global Sound Effects", request.GlobalSoundEffectCount);
        ConfigureCheckBox(GlobalEventSubscriptionsCheckBox, "Global Event Subscriptions", request.GlobalEventSubscriptionCount);
        ConfigureCheckBox(GlobalTimersCheckBox, "Global Timers", request.GlobalTimerCount);
        ConfigureCheckBox(TemplateObjectsCheckBox, "Template Objects", request.TemplateObjectCount);
        ConfigureCheckBox(RoomTemplatesCheckBox, "Room Templates", request.RoomTemplateCount);
        ConfigureCheckBox(BaseObjectsCheckBox, "Base Objects", request.BaseObjectCount);
        ConfigureCheckBox(GlobalObjectsCheckBox, "Global Objects", request.GlobalObjectCount);
    }

    private static void ConfigureCheckBox(System.Windows.Controls.CheckBox checkBox, string label, int count)
    {
        checkBox.Content = $"{label} ({count})";
        checkBox.IsEnabled = count > 0;
        checkBox.IsChecked = count > 0;
    }

    private void Import_OnClick(object sender, RoutedEventArgs e)
    {
        var importVerbs = VerbsCheckBox.IsChecked == true;
        var importDirectionals = DirectionalsCheckBox.IsChecked == true;
        var importGlobalActions = GlobalActionsCheckBox.IsChecked == true;
        var importGlobalSoundEffects = GlobalSoundEffectsCheckBox.IsChecked == true;
        var importGlobalEventSubscriptions = GlobalEventSubscriptionsCheckBox.IsChecked == true;
        var importGlobalTimers = GlobalTimersCheckBox.IsChecked == true;
        var importTemplates = TemplateObjectsCheckBox.IsChecked == true;
        var importRoomTemplates = RoomTemplatesCheckBox.IsChecked == true;
        var importBaseObjects = BaseObjectsCheckBox.IsChecked == true;
        var importGlobalObjects = GlobalObjectsCheckBox.IsChecked == true;

        if (!importVerbs
            && !importDirectionals
            && !importGlobalActions
            && !importGlobalSoundEffects
            && !importGlobalEventSubscriptions
            && !importGlobalTimers
            && !importTemplates
            && !importRoomTemplates
            && !importBaseObjects
            && !importGlobalObjects)
        {
            System.Windows.MessageBox.Show(this, "Select at least one section to import.", "Import Globals", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var strategy = CollisionStrategyCombo.SelectedIndex == 1
            ? GlobalImportCollisionStrategy.ReplaceExisting
            : GlobalImportCollisionStrategy.KeepExisting;

        Selection = new GlobalImportSelectionResult
        {
            ImportVerbs = importVerbs,
            ImportDirectionals = importDirectionals,
            ImportGlobalActions = importGlobalActions,
            ImportGlobalSoundEffects = importGlobalSoundEffects,
            ImportGlobalEventSubscriptions = importGlobalEventSubscriptions,
            ImportGlobalTimers = importGlobalTimers,
            ImportTemplateObjects = importTemplates,
            ImportRoomTemplates = importRoomTemplates,
            ImportBaseObjects = importBaseObjects,
            ImportGlobalObjects = importGlobalObjects,
            CollisionStrategy = strategy
        };

        DialogResult = true;
    }
}
