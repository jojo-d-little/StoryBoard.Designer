using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Models.Enums;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.Validation.Rules.Objects;
using StoryboardDesigner.App.Views;
using StoryboardDesigner.App.ViewModels;
using Storyboard.Shared.DesignerContracts;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace StoryboardDesigner.App.Tests;

[Collection("WPF STA")]
public sealed class ParticleEffectAuthoringPersistenceTests
{
    private const string TinyPngBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO2F3iUAAAAASUVORK5CYII=";

    [Fact]
    public void ColorPairControl_RendersTwoIndependentlyBindableColorRoles()
    {
        Exception? failure = null;
        StaTestThreadRunner.StartAndWait(new Thread(() =>
        {
            try
            {
                var catalog = new ParticleEffectAuthoringCatalogService();
                var recipe = catalog.GetRecipes(ParticleEffectAuthoringContext.ObjectAttachment)
                    .First(option => catalog.GetControls(option.EffectKey).Any(control =>
                        string.Equals(control.Kind, "colorPair", StringComparison.OrdinalIgnoreCase)));
                var dialog = new ParticleEffectObjectAuthoringDialog(new ParticleEffectAttachment { EffectKey = recipe.EffectKey });
                var panel = (StackPanel)typeof(ParticleEffectObjectAuthoringDialog)
                    .GetField("ControlsPanel", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(dialog)!;
                var rows = panel.Children.OfType<Grid>().ToList();
                var labels = rows.Select(row => row.Children.OfType<TextBlock>().Single().Text).ToList();
                var buttons = rows.SelectMany(row => row.Children.OfType<Button>()).ToList();
                var inputs = rows.SelectMany(row => row.Children.OfType<TextBox>()).ToList();

                var colorControl = catalog.GetControls(recipe.EffectKey).First(control =>
                    string.Equals(control.Kind, "colorPair", StringComparison.OrdinalIgnoreCase));
                Assert.Contains(colorControl.PrimaryRoleLabel!, labels);
                Assert.Contains(colorControl.SecondaryRoleLabel!, labels);
                Assert.All(inputs, input => Assert.Empty(input.Text));
                Assert.Contains(buttons, button => button.Tag?.ToString() == "Colors.Primary" && button.Content?.ToString() == "Choose variable…");
                Assert.Contains(buttons, button => button.Tag?.ToString() == "Colors.Secondary" && button.Content?.ToString() == "Choose variable…");
                Assert.Contains(buttons, button => button.Tag?.ToString() == "Colors.Primary" && button.Content?.ToString() == "Pick...");
                Assert.Contains(buttons, button => button.Tag?.ToString() == "Colors.Secondary" && button.Content?.ToString() == "Pick...");
                Assert.Contains(buttons, button => button.Content?.ToString() == "New computed variable");
                Assert.All(rows.Select(row => row.Children.OfType<TextBlock>().Single()), label => Assert.Equal(TextWrapping.Wrap, label.TextWrapping));
                var colorInputs = rows
                    .Where(row => row.Children.OfType<TextBlock>().Single().Text == colorControl.PrimaryRoleLabel
                        || row.Children.OfType<TextBlock>().Single().Text == colorControl.SecondaryRoleLabel)
                    .Select(row => row.Children.OfType<TextBox>().Single())
                    .ToList();
                Assert.Equal(2, colorInputs.Count);
                Assert.All(colorInputs, input =>
                {
                    var help = input.ToolTip?.ToString() ?? string.Empty;
                    Assert.Contains(colorControl.Description, help, StringComparison.Ordinal);
                    Assert.Contains("#AABBCC", help, StringComparison.Ordinal);
                    Assert.Contains("independent", help, StringComparison.OrdinalIgnoreCase);
                    Assert.Contains("literal or fallback", help, StringComparison.OrdinalIgnoreCase);
                    Assert.Contains("does not unbind", help, StringComparison.OrdinalIgnoreCase);
                });
                var naming = typeof(ParticleEffectObjectAuthoringDialog).GetMethod("BuildSuggestedVariableName", BindingFlags.Static | BindingFlags.NonPublic)!;
                Assert.Equal("particleEffectEmissionRateMultiplier", naming.Invoke(null, ["EmissionRateMultiplier"]));
                Assert.Equal("particleEffectColorsPrimary", naming.Invoke(null, ["Colors.Primary"]));
                dialog.Close();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        }), nameof(ColorPairControl_RendersTwoIndependentlyBindableColorRoles));

        if (failure is not null) throw failure;
    }

    [Fact]
    public void MissingMetadataLabels_FallBackToControlAndRoleNames()
    {
        var control = new ParticleEffectControlMetadata(
            "Tint", "number", "degrees", "  ", "", null, null, "apply", "  ", null);

        Assert.Equal("Tint (degrees)", ParticleEffectAuthoringHelpText.GetDisplayLabel(control));
        Assert.Equal("Primary color", ParticleEffectAuthoringHelpText.GetColorRoleDisplayLabel(control, primary: true));
        Assert.Equal("Secondary color", ParticleEffectAuthoringHelpText.GetColorRoleDisplayLabel(control, primary: false));

        var tooltip = ParticleEffectAuthoringHelpText.GetControlTooltip(control, "Primary color", isColor: true);
        Assert.Contains("Tint control", tooltip, StringComparison.Ordinal);
        Assert.Contains("Primary color is an independent role", tooltip, StringComparison.Ordinal);
        Assert.Contains("#AABBCC", tooltip, StringComparison.Ordinal);
    }

    [Fact]
    public void EnvironmentalRain_ExposesDistinctWindControlsAndEnabledFallbackHelp()
    {
        Exception? failure = null;
        StaTestThreadRunner.StartAndWait(new Thread(() =>
        {
            try
            {
                var catalog = new ParticleEffectAuthoringCatalogService();
                var rain = catalog.GetRecipes(ParticleEffectAuthoringContext.Environmental)
                    .First(option => string.Equals(option.Family, "rain", StringComparison.OrdinalIgnoreCase));
                var windControls = catalog.GetControls(rain.EffectKey)
                    .Where(control => control.Name.StartsWith("Wind", StringComparison.OrdinalIgnoreCase))
                    .ToList();
                Assert.Equal(4, windControls.Count);
                Assert.Equal(
                    new[] { "WindSpeedPxPerSecond", "WindDirectionDegrees", "WindSpeedMultiplier", "WindDirectionOffsetDegrees" }.OrderBy(name => name, StringComparer.Ordinal),
                    windControls.Select(control => control.Name).OrderBy(name => name, StringComparer.Ordinal).ToArray());

                var effect = new EnvironmentalEffectDefinition { Name = "Rain", EffectKey = rain.EffectKey };
                var dialog = new EnvironmentalEffectsAuthoringDialog([effect], selectedEffectId: effect.Id);
                var panel = (StackPanel)typeof(EnvironmentalEffectsAuthoringDialog)
                    .GetField("ControlsPanel", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(dialog)!;
                var rows = panel.Children.OfType<Grid>().ToList();
                var visibleLabels = rows.Select(row => row.Children.OfType<TextBlock>().Single().Text).ToList();

                Assert.All(windControls, control => Assert.Contains(ParticleEffectAuthoringHelpText.GetDisplayLabel(control), visibleLabels));
                Assert.Equal(4, visibleLabels.Where(label => label.Contains("Wind", StringComparison.OrdinalIgnoreCase)).Count());
                Assert.Contains("fallback when no usable binding resolves", ((FrameworkElement)typeof(EnvironmentalEffectsAuthoringDialog)
                    .GetField("EnabledCheckBox", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(dialog)!).ToolTip?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
                Assert.All(rows, row =>
                {
                    var buttons = row.Children.OfType<Button>().ToList();
                    Assert.Contains(buttons, button => button.Content?.ToString() == "Unbind"
                        && (button.ToolTip?.ToString() ?? string.Empty).Contains("keep the literal", StringComparison.OrdinalIgnoreCase));
                    Assert.Contains(buttons, button => button.Content?.ToString() == "Default"
                        && (button.ToolTip?.ToString() ?? string.Empty).Contains("literal and binding", StringComparison.OrdinalIgnoreCase));
                });
                dialog.Close();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        }), nameof(EnvironmentalRain_ExposesDistinctWindControlsAndEnabledFallbackHelp));

        if (failure is not null) throw failure;
    }

    [Fact]
    public void InstalledMetadata_ProvidesCorrectedTintAndStainLabels()
    {
        var catalog = new ParticleEffectAuthoringCatalogService();
        var fog = catalog.GetRecipes().First(recipe => string.Equals(recipe.Family, "fog", StringComparison.OrdinalIgnoreCase));
        var cloud = catalog.GetRecipes().First(recipe => string.Equals(recipe.Family, "cloud", StringComparison.OrdinalIgnoreCase));
        var stain = catalog.GetRecipes().First(recipe => string.Equals(recipe.Family, "surface-mark", StringComparison.OrdinalIgnoreCase));

        Assert.Contains(catalog.GetControls(fog.EffectKey), control => control.Name == "Tint" && control.Label == "Fog tint");
        Assert.Contains(catalog.GetControls(cloud.EffectKey), control => control.Name == "Tint" && control.Label == "Cloud tint");
        Assert.Contains(catalog.GetControls(stain.EffectKey), control => control.Name == "DirectionDegrees"
            && (control.Label == "Mark direction" || control.Label == "Stain direction"));

        var palette = catalog.GetControls(stain.EffectKey).First(control => control.Name == "Colors");
        Assert.Equal("Primary stain color", palette.PrimaryRoleLabel);
        Assert.Equal("Secondary stain color", palette.SecondaryRoleLabel);

        var categories = catalog.GetRecipes().GroupBy(static recipe => recipe.Category)
            .ToDictionary(static group => group.Key, static group => group.Count(), StringComparer.Ordinal);
        Assert.Equal(17, categories["PointParticleEffect"]);
        Assert.Equal(11, categories["AtmosphericParticleEffect"]);
        Assert.Equal(2, categories["AtmosphericOverlayEffect"]);
        Assert.Equal(4, categories["StainEffect"]);
        Assert.Equal(21, catalog.GetRecipes(ParticleEffectAuthoringContext.ObjectAttachment).Count);
        Assert.Equal(13, catalog.GetRecipes(ParticleEffectAuthoringContext.Environmental).Count);
        Assert.All(catalog.GetRecipes(ParticleEffectAuthoringContext.ObjectAttachment), recipe =>
            Assert.Contains(recipe.RenderingMode, new[] { "point", "stain" }));
        Assert.All(catalog.GetRecipes(ParticleEffectAuthoringContext.Environmental), recipe =>
            Assert.Contains(recipe.RenderingMode, new[] { "room", "overlay" }));
        Assert.All(catalog.GetRecipes(), recipe => Assert.Equal(
            recipe.Category switch
            {
                "PointParticleEffect" => "point",
                "AtmosphericParticleEffect" => "room",
                "AtmosphericOverlayEffect" => "overlay",
                "StainEffect" => "stain",
                _ => "unexpected"
            },
            recipe.RenderingMode));
        Assert.Contains(catalog.GetRecipes(), recipe => recipe.EffectKey == "particle.campfire" && recipe.Category == "PointParticleEffect");
        Assert.Contains(catalog.GetRecipes(), recipe => recipe.EffectKey == "particle.rain" && recipe.Category == "AtmosphericParticleEffect");

        var effectsCatalog = new PresentationEffectsCatalogService();
        Assert.Equal("appearance.selection.outline.cyan", effectsCatalog.GetDefaultSelectionOutlineEffectKey());
        Assert.Equal("appearance.selection.outline.amber", effectsCatalog.GetDefaultSecondarySelectionOutlineEffectKey());
        Assert.DoesNotContain("particle.campfire", effectsCatalog.GetObjectCueEligibleEffectKeys());
        Assert.DoesNotContain("particle.rain", effectsCatalog.GetObjectCueEligibleEffectKeys());
    }

    [Fact]
    public void CatalogPopulatesChildCueInheritanceDefaultsAndExplicitSelections()
    {
        Exception? failure = null;
        StaTestThreadRunner.StartAndWait(new Thread(() =>
        {
            try
            {
                var catalog = new PresentationEffectsCatalogService().GetCatalog();
                var eligible = catalog.Categories.Where(category => category.ChildCueInheritanceEligible).ToList();
                var defaults = eligible.Where(category => category.ChildCueInheritanceDefault)
                    .Select(category => category.Category).OrderBy(static category => category, StringComparer.Ordinal).ToArray();
                Assert.Equal(new[] { "Movement", "Scale" }, defaults);

                var dialog = new GameObjectSettingsDialog(CreateSettingsRequest(
                    isPartOfParent: true,
                    placement: new StructuralPartPlacement { Mode = StructuralPartPlacementKind.ParentRelative },
                    inheritParentCueCategories: null));
                var panel = (StackPanel)typeof(GameObjectSettingsDialog)
                    .GetField("ParentCueInheritanceCategoriesPanel", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(dialog)!;
                var checkBoxes = panel.Children.OfType<CheckBox>().ToDictionary(box => box.Content!.ToString()!);
                Assert.Equal(eligible.Select(category => category.Category).OrderBy(static category => category, StringComparer.Ordinal),
                    checkBoxes.Keys.OrderBy(static category => category, StringComparer.Ordinal));
                Assert.DoesNotContain("PointParticleEffect", checkBoxes.Keys);
                Assert.Equal(defaults, checkBoxes.Where(pair => pair.Value.IsChecked == true)
                    .Select(static pair => pair.Key).OrderBy(static category => category, StringComparer.Ordinal).ToArray());
                Assert.Equal(defaults, dialog.Values.InheritParentCueCategories);

                checkBoxes["Movement"].IsChecked = false;
                checkBoxes["Scale"].IsChecked = false;
                checkBoxes["Highlight"].IsChecked = true;
                Assert.Equal(new[] { "Highlight" }, dialog.Values.InheritParentCueCategories);
                Assert.Contains("snaps", checkBoxes["Movement"].ToolTip!.ToString(), StringComparison.OrdinalIgnoreCase);
                Assert.Contains("situational scaling", checkBoxes["Scale"].ToolTip!.ToString(), StringComparison.OrdinalIgnoreCase);
                dialog.Close();

                var emptyDialog = new GameObjectSettingsDialog(CreateSettingsRequest(
                    isPartOfParent: true,
                    placement: new StructuralPartPlacement { Mode = StructuralPartPlacementKind.ParentRelative },
                    inheritParentCueCategories: []));
                var emptyPanel = (StackPanel)typeof(GameObjectSettingsDialog)
                    .GetField("ParentCueInheritanceCategoriesPanel", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(emptyDialog)!;
                Assert.All(emptyPanel.Children.OfType<CheckBox>(), checkBox => Assert.False(checkBox.IsChecked));
                Assert.Empty(emptyDialog.Values.InheritParentCueCategories!);
                emptyDialog.Close();

                var fixedDialog = new GameObjectSettingsDialog(CreateSettingsRequest(
                    isPartOfParent: true,
                    placement: new StructuralPartPlacement
                    {
                        Mode = StructuralPartPlacementKind.RoomCoordinates,
                        RoomCoordinateX = 160,
                        RoomCoordinateY = 240
                    },
                    inheritParentCueCategories: null));
                var inheritancePanel = (FrameworkElement)typeof(GameObjectSettingsDialog)
                    .GetField("ParentCueInheritancePanel", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(fixedDialog)!;
                var fixedHint = (FrameworkElement)typeof(GameObjectSettingsDialog)
                    .GetField("RoomCoordinateCueInheritanceHintTextBlock", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(fixedDialog)!;
                Assert.Equal(Visibility.Collapsed, inheritancePanel.Visibility);
                Assert.Equal(Visibility.Visible, fixedHint.Visibility);
                Assert.Null(fixedDialog.Values.InheritParentCueCategories);
                Assert.Equal(160, fixedDialog.Values.StructuralPartPlacement!.RoomCoordinateX);
                Assert.Equal(240, fixedDialog.Values.StructuralPartPlacement.RoomCoordinateY);
                fixedDialog.Close();

                var actionDialog = new RoomActionEditorDialog(
                    new CommandAction { Name = "Clear selection", ActionType = CommandActionType.ClearObjectPresentationCues },
                    PropertyResolutionScope.Object,
                    new GamePropertyPickerContext(new ProjectModel(), null, PropertyResolutionScope.Object, _ => []),
                    [], [], [], []);
                var selectionChoices = (IReadOnlyList<string>)typeof(RoomActionEditorDialog)
                    .GetMethod("BuildSelectionCueEffectKeyChoices", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(actionDialog, null)!;
                var catalogSelectionKeys = new PresentationEffectsCatalogService().GetCatalog().Effects
                    .Where(effect => effect.Category == "Highlight")
                    .Select(static effect => effect.EffectKey).OrderBy(static key => key, StringComparer.OrdinalIgnoreCase).ToArray();
                Assert.Equal(catalogSelectionKeys, selectionChoices.OrderBy(static key => key, StringComparer.OrdinalIgnoreCase).ToArray());
                Assert.Contains("appearance.selection.outline.cyan", selectionChoices);
                actionDialog.Close();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        }), nameof(CatalogPopulatesChildCueInheritanceDefaultsAndExplicitSelections));

        if (failure is not null) throw failure;
    }

    [Fact]
    public void ExistingAnchorBinding_IsShownByVariableChooserAfterLoading()
    {
        Exception? failure = null;
        StaTestThreadRunner.StartAndWait(new Thread(() =>
        {
            try
            {
                var catalog = new ParticleEffectAuthoringCatalogService();
                var recipe = catalog.GetRecipes(ParticleEffectAuthoringContext.ObjectAttachment)
                    .First(option => catalog.GetControls(option.EffectKey).Any(control =>
                        string.Equals(control.Name, "EmissionRateMultiplier", StringComparison.OrdinalIgnoreCase)));
                var attachment = new ParticleEffectAttachment
                {
                    EffectKey = recipe.EffectKey,
                    Settings = [new ParticleEffectControlSettingDto
                    {
                        ControlKey = "EmissionRateMultiplier",
                        Binding = new ProjectGamePropertySourceReferenceDto
                        {
                            AnchorKey = "currentPhase",
                            PropertyName = "WindSpeed"
                        }
                    }]
                };
                var dialog = new ParticleEffectObjectAuthoringDialog(attachment);
                var panel = (StackPanel)typeof(ParticleEffectObjectAuthoringDialog)
                    .GetField("ControlsPanel", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(dialog)!;
                var rows = panel.Children.OfType<Grid>().ToList();
                var row = rows.Single(item => item.Children.OfType<TextBlock>().Single().Text.StartsWith("Emission rate", StringComparison.OrdinalIgnoreCase));
                Assert.Empty(row.Children.OfType<TextBox>().Single().Text);
                Assert.Contains(row.Children.OfType<Button>(), button =>
                    button.Content?.ToString() == "currentPhase.WindSpeed"
                    && (button.ToolTip?.ToString() ?? string.Empty).Contains("Bound to currentPhase.WindSpeed", StringComparison.Ordinal));
                dialog.Close();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        }), nameof(ExistingAnchorBinding_IsShownByVariableChooserAfterLoading));

        if (failure is not null) throw failure;
    }

    [Fact]
    public void StandardVariableChooser_OffersAndReturnsGeneratedWindSpeedButNotSyntheticEffectiveWindSpeed()
    {
        Exception? failure = null;
        StaTestThreadRunner.StartAndWait(new Thread(() =>
        {
            try
            {
                var (project, page) = CreateGeneratedWindPhaseProject();
                var choices = MainWindowViewModel.BuildComputedPropertyChoices(project, page);
                Assert.DoesNotContain(choices, choice => choice.PropertyName == "EffectiveWindSpeed");
                var choice = Assert.Single(choices, item => item.OwnerId == page.Id && item.PropertyName == "WindSpeed");
                Assert.Contains("currentPhase", choice.EligibleAnchors);
                var context = new GamePropertyPickerContext(
                    project,
                    page,
                    PropertyResolutionScope.PhasePage,
                    _ => choices,
                    AllowReferenceModes: true,
                    RelativeOwnerId: page.Id,
                    AnchorKeys: ["currentPhase"]);
                var picker = new VariableChooserDialog(context, "Choose variable");
                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
                timer.Tick += (_, _) =>
                {
                    timer.Stop();
                    var grid = (DataGrid)typeof(VariableChooserDialog)
                        .GetField("ChoicesDataGrid", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(picker)!;
                    var mode = (ComboBox)typeof(VariableChooserDialog)
                        .GetField("ReferenceModeComboBox", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(picker)!;
                    var anchor = (ComboBox)typeof(VariableChooserDialog)
                        .GetField("AnchorComboBox", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(picker)!;
                    mode.SelectedIndex = 2;
                    anchor.SelectedItem = "currentPhase";
                    grid.SelectedItem = choice;
                    typeof(VariableChooserDialog)
                        .GetMethod("Select_OnClick", BindingFlags.Instance | BindingFlags.NonPublic)!
                        .Invoke(picker, [picker, new RoutedEventArgs(Button.ClickEvent)]);
                };
                timer.Start();
                Assert.True(picker.ShowDialog());
                Assert.Equal("currentPhase", picker.SelectedReference?.AnchorKey);
                Assert.Equal("WindSpeed", picker.SelectedReference?.PropertyName);
                Assert.Null(picker.SelectedReference?.OwnerId);
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        }), nameof(StandardVariableChooser_OffersAndReturnsGeneratedWindSpeedButNotSyntheticEffectiveWindSpeed));

        if (failure is not null) throw failure;
    }

    [Fact]
    public void Fx001_ResolvesActualGeneratedPhasePropertyAndRejectsAbsentSyntheticProperty()
    {
        var (project, _) = CreateGeneratedWindPhaseProject();
        var catalog = new ParticleEffectAuthoringCatalogService();
        var recipe = catalog.GetRecipes(ParticleEffectAuthoringContext.Environmental).First(option =>
            catalog.GetControls(option.EffectKey).Any(control =>
                string.Equals(control.Kind, "number", StringComparison.OrdinalIgnoreCase)));
        var numberControl = catalog.GetControls(recipe.EffectKey).First(control =>
            string.Equals(control.Kind, "number", StringComparison.OrdinalIgnoreCase));

        project.EnvironmentalEffects.Add(new EnvironmentalEffectDefinition
        {
            Name = "Wind source parity",
            EffectKey = recipe.EffectKey,
            Settings = [new ParticleEffectControlSettingDto
            {
                ControlKey = numberControl.Name,
                Binding = new ProjectGamePropertySourceReferenceDto
                {
                    AnchorKey = "currentPhase",
                    PropertyName = "WindSpeed"
                }
            }]
        });
        var rule = new ParticleEffectAuthoringValidationRule(catalog);
        var validDescriptions = rule.Evaluate(ValidationRuleTestContext.Create(project))
            .Select(issue => issue.Description)
            .ToList();
        Assert.DoesNotContain(validDescriptions, description => description.Contains("cannot resolve variable 'WindSpeed'", StringComparison.OrdinalIgnoreCase));

        project.EnvironmentalEffects[0].Settings[0].Binding!.PropertyName = "EffectiveWindSpeed";
        var invalidDescriptions = rule.Evaluate(ValidationRuleTestContext.Create(project))
            .Select(issue => issue.Description)
            .ToList();
        Assert.Contains(invalidDescriptions, description => description.Contains("cannot resolve variable 'EffectiveWindSpeed'", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validation_ReportsUnresolvedWrongKindAndOutOfRangeEffectSettings()
    {
        var catalog = new ParticleEffectAuthoringCatalogService();
        var recipe = catalog.GetRecipes(ParticleEffectAuthoringContext.Environmental)
            .First(option => catalog.GetControls(option.EffectKey).Any(control =>
                string.Equals(control.Kind, "number", StringComparison.OrdinalIgnoreCase)
                && (control.Minimum.HasValue || control.Maximum.HasValue)));
        var numericControl = catalog.GetControls(recipe.EffectKey).First(control =>
            string.Equals(control.Kind, "number", StringComparison.OrdinalIgnoreCase)
            && (control.Minimum.HasValue || control.Maximum.HasValue));
        var secondNumericControl = catalog.GetControls(recipe.EffectKey).First(control =>
            string.Equals(control.Kind, "number", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(control.Name, numericControl.Name, StringComparison.OrdinalIgnoreCase));
        var outsideRange = numericControl.Maximum.HasValue
            ? numericControl.Maximum.Value + 1
            : numericControl.Minimum!.Value - 1;
        var project = new ProjectModel();
        project.GlobalVariables.Add(new GamePropertyDefinition
        {
            Name = "switch",
            DefaultValue = "true",
            ValueRestriction = GamePropertyValueRestriction.TrueFalse
        });
        project.EnvironmentalEffects.Add(new EnvironmentalEffectDefinition
        {
            Name = "Invalid",
            EffectKey = recipe.EffectKey,
            Settings =
            [
                new ParticleEffectControlSettingDto
                {
                    ControlKey = numericControl.Name,
                    Value = outsideRange
                },
                new ParticleEffectControlSettingDto
                {
                    ControlKey = secondNumericControl.Name,
                    Value = 1d,
                    Binding = new ProjectGamePropertySourceReferenceDto { OwnerId = Guid.Empty, PropertyName = "switch" }
                }
            ],
            EnabledBinding = new ProjectGamePropertySourceReferenceDto { AnchorKey = "currentPhase", PropertyName = "missing" }
        });

        var descriptions = new ParticleEffectAuthoringValidationRule(catalog)
            .Evaluate(ValidationRuleTestContext.Create(project))
            .Select(issue => issue.Description)
            .ToList();

        Assert.Contains(descriptions, description => description.Contains("between", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(descriptions, description => description.Contains("requires Numeric", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(descriptions, description => description.Contains("unsupported anchor 'currentPhase'", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GeneratedReadOnlyProperty_AllowsReviewingItsComputationWithoutEditControls()
    {
        Exception? failure = null;
        StaTestThreadRunner.StartAndWait(new Thread(() =>
        {
            try
            {
                var computation = new ProjectGamePropertyComputationV2Dto
                {
                    OutputType = "Boolean",
                    Expression = new ProjectGamePropertyExpressionDto
                    {
                        Kind = "compare", Operator = "Equals",
                        Left = new ProjectGamePropertyExpressionDto { Kind = "property", Source = new ProjectGamePropertySourceReferenceDto { AnchorKey = "currentPhase", PropertyName = "TimeOfDay" } },
                        Right = new ProjectGamePropertyExpressionDto { Kind = "literal", Value = "Evening" }
                    }
                };
                var reviewOpened = false;
                var variableDialog = new VariableEditorDialog(
                    "IsEvening",
                    GamePropertyLifetime.Singleton,
                    "false",
                    GamePropertyValueRestriction.TrueFalse,
                    computation: computation,
                    editComputation: current =>
                    {
                        reviewOpened = true;
                        var review = new GamePropertyComputationEditorDialog(current, _ => null, _ => "currentPhase.TimeOfDay", readOnly: true);
                        Assert.False(((ComboBox)typeof(GamePropertyComputationEditorDialog)
                            .GetField("ResultKindComboBox", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(review)!).IsEnabled);
                        Assert.False(((Button)typeof(GamePropertyComputationEditorDialog)
                            .GetField("ChooseSourceButton", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(review)!).IsEnabled);
                        Assert.Contains("currentPhase.TimeOfDay", ((TextBlock)typeof(GamePropertyComputationEditorDialog)
                            .GetField("ExpressionPreviewTextBlock", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(review)!).Text);
                        Assert.Equal("Close", ((Button)typeof(GamePropertyComputationEditorDialog)
                            .GetField("SaveButton", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(review)!).Content);
                        review.Close();
                        return current;
                    },
                    readOnly: true);

                var reviewButton = (Button)typeof(VariableEditorDialog)
                    .GetField("EditComputationButton", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(variableDialog)!;
                Assert.Equal("Generated Game Property (Read-only)", variableDialog.Title);
                Assert.Equal(Visibility.Visible, ((TextBlock)typeof(VariableEditorDialog)
                    .GetField("GeneratedPropertyInfoTextBlock", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(variableDialog)!).Visibility);
                Assert.True(reviewButton.IsEnabled);
                Assert.Equal("Review computation...", reviewButton.Content);
                reviewButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.True(reviewOpened);
                variableDialog.Close();

            }
            catch (Exception ex)
            {
                failure = ex;
            }
        }), nameof(GeneratedReadOnlyProperty_AllowsReviewingItsComputationWithoutEditControls));

        if (failure is not null) throw failure;
    }

    [Fact]
    public void SaveProjectModel_RoundTripsParticleStructuralEnvironmentalAndImageAuthoring()
    {
        var service = new JsonExportService();
        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var directSource = new ProjectGamePropertySourceReferenceDto
            {
                AnchorKey = "currentPhase",
                PropertyName = "WindSpeed"
            };
            var enabledSource = new ProjectGamePropertySourceReferenceDto
            {
                OwnerId = Guid.Empty,
                PropertyName = "isNight"
            };
            var parent = new GameObject { Name = "Campfire" };
            var child = new GameObject
            {
                Name = "Smoke plume",
                IsPartOfParent = true,
                Placement = new StructuralPartPlacement
                {
                    Mode = StructuralPartPlacementKind.ParentRelative,
                    ParentRelativeOffsetX = 40,
                    ParentRelativeOffsetY = -80
                },
                ParticleEffect = new ParticleEffectAttachment
                {
                    EffectKey = "smoke",
                    OffsetXPx = 2.5,
                    OffsetYPx = -4,
                    Settings = [new ParticleEffectControlSettingDto
                    {
                        ControlKey = "Density",
                        Value = 0.65d,
                        Binding = new ProjectGamePropertySourceReferenceDto { OwnerId = Guid.Empty, PropertyName = "densitySource" }
                    }, new ParticleEffectControlSettingDto
                    {
                        ControlKey = "EmissionRateMultiplier",
                        Binding = directSource
                    }, new ParticleEffectControlSettingDto
                    {
                        ControlKey = "Colors.Primary",
                        Value = "#AA3300",
                        Binding = new ProjectGamePropertySourceReferenceDto { OwnerId = Guid.Empty, PropertyName = "primaryColor" }
                    }, new ParticleEffectControlSettingDto
                    {
                        ControlKey = "Colors.Secondary",
                        Value = "#1166CC",
                        Binding = new ProjectGamePropertySourceReferenceDto { OwnerId = Guid.Empty, PropertyName = "secondaryColor" }
                    }]
                }
            };
            var room = new Room
            {
                Name = "Clearing",
                GameObjects = [parent, child, new GameObject
                {
                    Name = "Wall-mounted sign",
                    IsPartOfParent = true,
                    Placement = new StructuralPartPlacement
                    {
                        Mode = StructuralPartPlacementKind.RoomCoordinates,
                        RoomCoordinateX = 120,
                        RoomCoordinateY = 200
                    }
                }],
                EnvironmentalEffects =
                [
                    new EnvironmentalEffectDefinition
                    {
                        Id = Guid.Parse("e0db76fa-5c2a-4474-8932-a128bac5a4a4"),
                        Name = "Rain",
                        EffectKey = "rain",
                        Applicability = StoryboardDesigner.App.Models.EnvironmentalEffectApplicability.Outdoor,
                        RenderOrder = 12,
                        Enabled = false,
                        EnabledBinding = enabledSource,
                        Settings = [new ParticleEffectControlSettingDto { ControlKey = "WindSpeedMultiplier", Value = 0d }]
                    },
                    new EnvironmentalEffectDefinition
                    {
                        Name = "Recipe default",
                        EffectKey = "rain",
                        Enabled = true
                    }
                ]
            };
            var project = new ProjectModel
            {
                Name = "ParticleAuthoringRoundTrip",
                PhaseBooks = [CreateGeneratedWindPhaseProject().Project.PhaseBooks.Single()],
                GlobalVariables =
                [
                    new GamePropertyDefinition { Name = "isNight", DefaultValue = "true", ValueRestriction = GamePropertyValueRestriction.TrueFalse },
                    new GamePropertyDefinition { Name = "densitySource", DefaultValue = "0.8", ValueRestriction = GamePropertyValueRestriction.Numeric },
                    new GamePropertyDefinition { Name = "primaryColor", DefaultValue = "#AA3300", ValueRestriction = GamePropertyValueRestriction.Unrestricted },
                    new GamePropertyDefinition { Name = "secondaryColor", DefaultValue = "#1166CC", ValueRestriction = GamePropertyValueRestriction.Unrestricted }
                ],
                ParticleEffectImageChoices = [new ParticleEffectImageChoice { EffectKey = "fire", ImageSlot = "flame", AssetPath = "art/fire.webp" }],
                Planets = [new Planet { Name = "World", Countries = [new Country { Name = "Land", Areas = [new Area { Name = "Woods", Rooms = [room] }] }] }]
            };

            var path = Path.Combine(tempRoot, "ParticleAuthoringRoundTrip.sbe.json");
            service.SaveProjectModel(path, project);
            var loaded = service.TryLoadProjectModel(path)!;
            var loadedRoom = loaded.Planets.Single().Countries.Single().Areas.Single().Rooms.Single();
            var loadedChild = loadedRoom.GameObjects.Single(obj => obj.Name == "Smoke plume");
            var loadedRoomPart = loadedRoom.GameObjects.Single(obj => obj.Name == "Wall-mounted sign");
            var loadedRain = loadedRoom.EnvironmentalEffects.Single(effect => effect.Name == "Rain");
            var loadedRecipeDefault = loadedRoom.EnvironmentalEffects.Single(effect => effect.Name == "Recipe default");

            Assert.Equal("smoke", loadedChild.ParticleEffect!.EffectKey);
            Assert.Equal(2.5, loadedChild.ParticleEffect.OffsetXPx);
            var loadedSettings = loadedChild.ParticleEffect.Settings.ToDictionary(setting => setting.ControlKey);
            Assert.Equal(0.65d, ReadNumber(loadedSettings["Density"].Value));
            Assert.Equal("densitySource", loadedSettings["Density"].Binding!.PropertyName);
            Assert.Equal("currentPhase", loadedSettings["EmissionRateMultiplier"].Binding!.AnchorKey);
            Assert.Equal("WindSpeed", loadedSettings["EmissionRateMultiplier"].Binding!.PropertyName);
            Assert.Null(loadedSettings["EmissionRateMultiplier"].Value);
            Assert.Equal("#AA3300", ReadText(loadedSettings["Colors.Primary"].Value));
            Assert.Equal("#1166CC", ReadText(loadedSettings["Colors.Secondary"].Value));
            Assert.Equal("primaryColor", loadedSettings["Colors.Primary"].Binding!.PropertyName);
            Assert.Equal("secondaryColor", loadedSettings["Colors.Secondary"].Binding!.PropertyName);
            Assert.True(loadedChild.IsPartOfParent);
            Assert.Equal(StructuralPartPlacementKind.ParentRelative, loadedChild.Placement.Mode);
            Assert.Equal(40, loadedChild.Placement.ParentRelativeOffsetX);
            Assert.True(loadedRoomPart.IsPartOfParent);
            Assert.Equal(StructuralPartPlacementKind.RoomCoordinates, loadedRoomPart.Placement.Mode);
            Assert.Equal(120, loadedRoomPart.Placement.RoomCoordinateX);
            Assert.Equal(200, loadedRoomPart.Placement.RoomCoordinateY);
            Assert.Equal("rain", loadedRain.EffectKey);
            Assert.Equal(Guid.Parse("e0db76fa-5c2a-4474-8932-a128bac5a4a4"), loadedRain.Id);
            Assert.False(loadedRain.Enabled);
            Assert.Null(loadedRain.EnabledBinding!.AnchorKey);
            Assert.Equal("isNight", loadedRain.EnabledBinding!.PropertyName);
            Assert.Equal(0d, ReadNumber(loadedRain.Settings.Single(setting => setting.ControlKey == "WindSpeedMultiplier").Value));
            Assert.True(loadedRecipeDefault.Enabled);
            Assert.Empty(loadedRecipeDefault.Settings);
            Assert.Null(loadedRecipeDefault.EnabledBinding);
            Assert.Equal("true", loaded.GlobalVariables.Single(variable => variable.Name == "isNight").DefaultValue);
            loaded.GlobalVariables.Single(variable => variable.Name == "isNight").DefaultValue = "false";
            service.SaveProjectModel(path, loaded);
            var reloadedAfterSourceMutation = service.TryLoadProjectModel(path)!;
            Assert.Equal("false", reloadedAfterSourceMutation.GlobalVariables.Single(variable => variable.Name == "isNight").DefaultValue);
            Assert.Equal("isNight", reloadedAfterSourceMutation.Planets.Single().Countries.Single().Areas.Single().Rooms.Single()
                .EnvironmentalEffects.Single(effect => effect.Name == "Rain").EnabledBinding!.PropertyName);
            var savedJson = File.ReadAllText(path);
            Assert.DoesNotContain("elseResult", savedJson, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("art/fire.webp", loaded.ParticleEffectImageChoices.Single().AssetPath);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void SaveReopenAndCleanExport_PreserveNestedParticleIdentityBindingsAndStagedArt()
    {
        var service = new JsonExportService();
        var catalog = new ParticleEffectAuthoringCatalogService();
        var fireRecipe = Assert.Single(catalog.GetRecipes(), recipe => recipe.EffectKey == "particle.campfire");
        var smokeRecipe = Assert.Single(catalog.GetRecipes(), recipe => recipe.EffectKey == "particle.campfire.smoke");
        var cloudRecipe = Assert.Single(catalog.GetRecipes(), recipe => recipe.EffectKey == "particle.cloud");
        Assert.Contains(catalog.GetControls(fireRecipe.EffectKey), control => control.Name == "EmissionRateMultiplier");
        Assert.Contains(catalog.GetControls(fireRecipe.EffectKey), control => control.Name == "SpawnRadiusMultiplier");
        Assert.Contains(catalog.GetControls(fireRecipe.EffectKey), control => control.Name == "Colors" && control.Kind == "colorPair");
        Assert.Contains(catalog.GetControls(smokeRecipe.EffectKey), control => control.Name == "EmissionRateMultiplier");
        Assert.Contains(catalog.GetControls(cloudRecipe.EffectKey), control => control.Name == "CoverageMultiplier");

        var tempRoot = Path.Combine(Path.GetTempPath(), "StoryboardDesigner.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            var imageFolder = Path.Combine(tempRoot, "art");
            Directory.CreateDirectory(imageFolder);
            var customFlameImage = Path.Combine(imageFolder, "custom-flame.png");
            var imageBytes = Convert.FromBase64String(TinyPngBase64);
            File.WriteAllBytes(customFlameImage, imageBytes);

            var parentId = Guid.Parse("a1000000-0000-4000-8000-000000000001");
            var relativePartId = Guid.Parse("a1000000-0000-4000-8000-000000000002");
            var roomCoordinatePartId = Guid.Parse("a1000000-0000-4000-8000-000000000003");
            var unsetInheritancePartId = Guid.Parse("a1000000-0000-4000-8000-000000000004");
            var fireRatePropertyId = Guid.Parse("a1000000-0000-4000-8000-000000000011");
            var fireCorePropertyId = Guid.Parse("a1000000-0000-4000-8000-000000000012");
            var fireEdgePropertyId = Guid.Parse("a1000000-0000-4000-8000-000000000013");
            var planetId = Guid.Parse("a1000000-0000-4000-8000-000000000021");
            var countryId = Guid.Parse("a1000000-0000-4000-8000-000000000022");
            var areaId = Guid.Parse("a1000000-0000-4000-8000-000000000023");
            var roomId = Guid.Parse("a1000000-0000-4000-8000-000000000024");
            var inheritedRainId = Guid.Parse("a1000000-0000-4000-8000-000000000031");
            var roomRainOptOutId = Guid.Parse("a1000000-0000-4000-8000-000000000032");
            var cloudIds = new[]
            {
                Guid.Parse("a1000000-0000-4000-8000-000000000041"),
                Guid.Parse("a1000000-0000-4000-8000-000000000042"),
                Guid.Parse("a1000000-0000-4000-8000-000000000043")
            };

            var parent = new GameObject
            {
                ObjectId = parentId,
                Name = "Hearth",
                IsContainer = true,
                HideContainedObjects = false,
                Variables =
                [
                    new GamePropertyDefinition { Id = fireRatePropertyId, Name = "FireRate", DefaultValue = "0.65", ValueRestriction = GamePropertyValueRestriction.Numeric },
                    new GamePropertyDefinition { Id = fireCorePropertyId, Name = "FireCore", DefaultValue = "#FF6A00", ValueRestriction = GamePropertyValueRestriction.Unrestricted },
                    new GamePropertyDefinition { Id = fireEdgePropertyId, Name = "FireEdge", DefaultValue = "#FFC44D", ValueRestriction = GamePropertyValueRestriction.Unrestricted }
                ]
            };
            var relativePart = new GameObject
            {
                ObjectId = relativePartId,
                Name = "Flame emitter",
                IsPartOfParent = true,
                InheritParentCueCategories = ["Movement", "Scale", "Highlight"],
                Placement = new StructuralPartPlacement
                {
                    Mode = StructuralPartPlacementKind.ParentRelative,
                    ParentRelativeOffsetX = 41.5,
                    ParentRelativeOffsetY = -13.25
                },
                ImageVariants =
                [
                    new ObjectImageVariant
                    {
                        VariantName = "default",
                        ImageLocalAlignmentOffsetX = 2.5,
                        ImageLocalAlignmentOffsetY = -1.25,
                        ImageLocalAlignmentRotationDegrees = 17,
                        IsDefault = true
                    }
                ],
                PointLight = new PointLight
                {
                    OffsetXPx = 3.25,
                    OffsetYPx = -4.5,
                    RadiusPx = 118,
                    DirectionDeg = 42,
                    Color = "#FF8A22",
                    IntensityScale = 0.8
                },
                ParticleEffect = new ParticleEffectAttachment
                {
                    EffectKey = fireRecipe.EffectKey,
                    OffsetXPx = 1.75,
                    OffsetYPx = -2.25,
                    Settings =
                    [
                        new ParticleEffectControlSettingDto
                        {
                            ControlKey = "EmissionRateMultiplier",
                            Value = 0d,
                            Binding = new ProjectGamePropertySourceReferenceDto { OwnerId = parentId, PropertyName = "FireRate" }
                        },
                        new ParticleEffectControlSettingDto
                        {
                            ControlKey = "SpawnRadiusMultiplier",
                            Binding = new ProjectGamePropertySourceReferenceDto { AnchorKey = "currentPhase", PropertyName = "WindSpeed" }
                        },
                        new ParticleEffectControlSettingDto
                        {
                            ControlKey = "Colors.Primary",
                            Value = "#FF6A00",
                            Binding = new ProjectGamePropertySourceReferenceDto { OwnerId = parentId, PropertyName = "FireCore" }
                        },
                        new ParticleEffectControlSettingDto
                        {
                            ControlKey = "Colors.Secondary",
                            Value = "#FFC44D",
                            Binding = new ProjectGamePropertySourceReferenceDto { OwnerId = parentId, PropertyName = "FireEdge" }
                        }
                    ]
                }
            };
            var roomCoordinatePart = new GameObject
            {
                ObjectId = roomCoordinatePartId,
                Name = "Smoke source",
                IsPartOfParent = true,
                InheritParentCueCategories = [],
                Placement = new StructuralPartPlacement
                {
                    Mode = StructuralPartPlacementKind.RoomCoordinates,
                    RoomCoordinateX = 312.75,
                    RoomCoordinateY = 188.5
                },
                ImageVariants =
                [
                    new ObjectImageVariant
                    {
                        VariantName = "default",
                        ImageLocalAlignmentOffsetX = -3.5,
                        ImageLocalAlignmentOffsetY = 4.25,
                        ImageLocalAlignmentRotationDegrees = -8,
                        IsDefault = true
                    }
                ],
                ParticleEffect = new ParticleEffectAttachment
                {
                    EffectKey = smokeRecipe.EffectKey,
                    OffsetXPx = -1.5,
                    OffsetYPx = 2.75,
                    Settings = [new ParticleEffectControlSettingDto { ControlKey = "EmissionRateMultiplier", Value = 0.4d }]
                }
            };
            var unsetInheritancePart = new GameObject
            {
                ObjectId = unsetInheritancePartId,
                Name = "Old unspecialized part",
                IsPartOfParent = true,
                Placement = new StructuralPartPlacement { Mode = StructuralPartPlacementKind.ParentRelative }
            };
            parent.ContainedObjects = [relativePart, roomCoordinatePart, unsetInheritancePart];

            var rainDefaults = new EnvironmentalEffectDefinition
            {
                Id = inheritedRainId,
                Name = "Rain",
                EffectKey = "particle.rain",
                Enabled = true,
                RenderOrder = 7,
                Settings = [new ParticleEffectControlSettingDto { ControlKey = "WindSpeedMultiplier", Value = 0.75d }]
            };
            var roomRainOptOut = new EnvironmentalEffectDefinition
            {
                Id = roomRainOptOutId,
                Name = "Rain",
                EffectKey = "particle.rain",
                Enabled = false,
                RenderOrder = 19
            };
            var clouds = new[]
            {
                new EnvironmentalEffectDefinition
                {
                    Id = cloudIds[0], Name = "Cloud north", EffectKey = cloudRecipe.EffectKey, Enabled = true, RenderOrder = 2,
                    Settings = [new ParticleEffectControlSettingDto { ControlKey = "CoverageMultiplier", Value = 0d }]
                },
                new EnvironmentalEffectDefinition
                {
                    Id = cloudIds[1], Name = "Cloud center", EffectKey = cloudRecipe.EffectKey, Enabled = true, RenderOrder = 5,
                    Settings = [new ParticleEffectControlSettingDto { ControlKey = "CoverageMultiplier", Value = 0.65d }]
                },
                new EnvironmentalEffectDefinition
                {
                    Id = cloudIds[2], Name = "Cloud south", EffectKey = cloudRecipe.EffectKey, Enabled = true, RenderOrder = 8,
                    Settings = [new ParticleEffectControlSettingDto
                    {
                        ControlKey = "CoverageMultiplier",
                        Value = 1.25d,
                        Binding = new ProjectGamePropertySourceReferenceDto { AnchorKey = "currentPhase", PropertyName = "WindSpeed" }
                    }]
                }
            };

            var room = new Room
            {
                Id = roomId,
                Name = "Hearth room",
                GameObjects = [parent],
                EnvironmentalEffects = [roomRainOptOut, .. clouds]
            };
            var area = new Area { Id = areaId, Name = "Courtyard", Rooms = [room], StartingRoomId = roomId };
            var country = new Country { Id = countryId, Name = "Northlands", Areas = [area], StartingAreaName = area.Name };
            var planet = new Planet { Id = planetId, Name = "Ember", Countries = [country], StartingCountryName = country.Name };
            var phaseProject = CreateGeneratedWindPhaseProject().Project;
            var project = new ProjectModel
            {
                Name = "ParticleHardening",
                StartingPlanetName = planet.Name,
                PhaseBooks = phaseProject.PhaseBooks,
                EnvironmentalEffects = [rainDefaults],
                ParticleEffectImageChoices = [new ParticleEffectImageChoice
                {
                    EffectKey = fireRecipe.EffectKey,
                    ImageSlot = "flame-tip",
                    AssetPath = Path.GetRelativePath(tempRoot, customFlameImage).Replace('\\', '/')
                }],
                Planets = [planet]
            };
            ScopeHierarchy.AttachParents(project);
            Assert.Same(parent, relativePart.ParentScope);
            Assert.Same(parent, roomCoordinatePart.ParentScope);

            var path = Path.Combine(tempRoot, "ParticleHardening.sbe.json");
            service.SaveProjectModel(path, project);
            var firstReopen = Assert.IsType<ProjectModel>(service.TryLoadProjectModel(path));
            ScopeHierarchy.AttachParents(firstReopen);
            var firstRoom = firstReopen.Planets.Single().Countries.Single().Areas.Single().Rooms.Single();
            var firstParent = Assert.Single(firstRoom.GameObjects);
            var firstRelativePart = Assert.Single(firstParent.ContainedObjects, child => child.ObjectId == relativePartId);
            var firstRoomCoordinatePart = Assert.Single(firstParent.ContainedObjects, child => child.ObjectId == roomCoordinatePartId);
            var firstUnsetInheritancePart = Assert.Single(firstParent.ContainedObjects, child => child.ObjectId == unsetInheritancePartId);
            Assert.Same(firstParent, firstRelativePart.ParentScope);
            Assert.Same(firstParent, firstRoomCoordinatePart.ParentScope);
            Assert.Equal(parentId, firstParent.ObjectId);
            Assert.Null(firstUnsetInheritancePart.InheritParentCueCategories);
            Assert.Equal(roomId, firstRoom.Id);
            Assert.Equal(fireRatePropertyId, firstParent.Variables.Single(variable => variable.Name == "FireRate").Id);
            Assert.Equal(new[] { "Movement", "Scale", "Highlight" }, firstRelativePart.InheritParentCueCategories);
            Assert.Equal(fireRecipe.EffectKey, firstRelativePart.ParticleEffect!.EffectKey);
            Assert.Equal(41.5, firstRelativePart.Placement.ParentRelativeOffsetX);
            Assert.Equal(-13.25, firstRelativePart.Placement.ParentRelativeOffsetY);
            Assert.Equal(17, firstRelativePart.ImageVariants.Single().ImageLocalAlignmentRotationDegrees);
            Assert.Equal(StructuralPartPlacementKind.RoomCoordinates, firstRoomCoordinatePart.Placement.Mode);
            Assert.Empty(firstRoomCoordinatePart.InheritParentCueCategories!);
            Assert.Equal(312.75, firstRoomCoordinatePart.Placement.RoomCoordinateX);
            Assert.Equal(188.5, firstRoomCoordinatePart.Placement.RoomCoordinateY);
            Assert.Equal(-8, firstRoomCoordinatePart.ImageVariants.Single().ImageLocalAlignmentRotationDegrees);
            Assert.Equal("art/custom-flame.png", firstReopen.ParticleEffectImageChoices.Single().AssetPath);

            service.SaveProjectModel(path, firstReopen);
            var reopenedAgain = Assert.IsType<ProjectModel>(service.TryLoadProjectModel(path));
            ScopeHierarchy.AttachParents(reopenedAgain);
            var reloadedRoom = reopenedAgain.Planets.Single().Countries.Single().Areas.Single().Rooms.Single();
            var reloadedParent = Assert.Single(reloadedRoom.GameObjects);
            var reloadedFlame = Assert.Single(reloadedParent.ContainedObjects, child => child.ObjectId == relativePartId);
            var reloadedSmoke = Assert.Single(reloadedParent.ContainedObjects, child => child.ObjectId == roomCoordinatePartId);
            var reloadedUnsetInheritancePart = Assert.Single(reloadedParent.ContainedObjects, child => child.ObjectId == unsetInheritancePartId);
            var reloadedRain = Assert.Single(reloadedRoom.EnvironmentalEffects, effect => effect.Name == "Rain");
            Assert.Equal(roomRainOptOutId, reloadedRain.Id);
            Assert.False(reloadedRain.Enabled);
            Assert.Empty(reloadedRain.Settings);
            Assert.Null(reloadedRain.EnabledBinding);
            Assert.Equal(inheritedRainId, Assert.Single(reopenedAgain.EnvironmentalEffects, effect => effect.Name == "Rain").Id);
            Assert.Equal(3, reloadedRoom.EnvironmentalEffects.Count(effect => effect.EffectKey == cloudRecipe.EffectKey));
            Assert.Equal(3, reloadedRoom.EnvironmentalEffects.Where(effect => effect.EffectKey == cloudRecipe.EffectKey).Select(effect => effect.Id).Distinct().Count());
            Assert.Equal(0d, ReadNumber(reloadedRoom.EnvironmentalEffects.Single(effect => effect.Id == cloudIds[0]).Settings.Single().Value));
            Assert.Equal(0d, ReadNumber(reloadedFlame.ParticleEffect!.Settings.Single(setting => setting.ControlKey == "EmissionRateMultiplier").Value));
            Assert.Equal("WindSpeed", reloadedFlame.ParticleEffect.Settings.Single(setting => setting.ControlKey == "SpawnRadiusMultiplier").Binding!.PropertyName);
            Assert.Equal(parentId, reloadedFlame.ParticleEffect.Settings.Single(setting => setting.ControlKey == "Colors.Primary").Binding!.OwnerId);
            Assert.Equal("#FFC44D", ReadText(reloadedFlame.ParticleEffect.Settings.Single(setting => setting.ControlKey == "Colors.Secondary").Value));
            Assert.Equal("particle.campfire.smoke", reloadedSmoke.ParticleEffect!.EffectKey);
            Assert.Equal(new[] { "Movement", "Scale", "Highlight" }, reloadedFlame.InheritParentCueCategories);
            Assert.Empty(reloadedSmoke.InheritParentCueCategories!);
            Assert.Null(reloadedUnsetInheritancePart.InheritParentCueCategories);

            service.ExportCleanProjectV1(path, reopenedAgain);
            var runtimeRoot = Path.Combine(tempRoot, "GameRuntimeJson");
            var runtimeProjectPath = Path.Combine(runtimeRoot, "ParticleHardening.sbr.runtime.json");
            var runtimeRoomPath = Path.Combine(runtimeRoot, "Room", roomId.ToString("D").ToUpperInvariant() + ".runtime.json");
            var runtimeParentPath = Path.Combine(runtimeRoot, "GameObject", parentId.ToString("D").ToUpperInvariant() + ".runtime.json");
            using var runtimeProject = JsonDocument.Parse(File.ReadAllText(runtimeProjectPath));
            using var runtimeRoom = JsonDocument.Parse(File.ReadAllText(runtimeRoomPath));
            using var runtimeParent = JsonDocument.Parse(File.ReadAllText(runtimeParentPath));

            using var runtimePlanet = JsonDocument.Parse(File.ReadAllText(Path.Combine(runtimeRoot, "Planet", planetId.ToString("D").ToUpperInvariant() + ".runtime.json")));
            using var runtimeCountry = JsonDocument.Parse(File.ReadAllText(Path.Combine(runtimeRoot, "Country", countryId.ToString("D").ToUpperInvariant() + ".runtime.json")));
            using var runtimeArea = JsonDocument.Parse(File.ReadAllText(Path.Combine(runtimeRoot, "Area", areaId.ToString("D").ToUpperInvariant() + ".runtime.json")));
            Assert.Equal(planetId, runtimePlanet.RootElement.GetProperty("Id").GetGuid());
            Assert.Equal(countryId, runtimeCountry.RootElement.GetProperty("Id").GetGuid());
            Assert.Equal(areaId, runtimeArea.RootElement.GetProperty("Id").GetGuid());
            Assert.Equal(roomId, runtimeRoom.RootElement.GetProperty("Id").GetGuid());
            Assert.Contains(parentId, runtimeRoom.RootElement.GetProperty("gameObjectIds").EnumerateArray().Select(value => value.GetGuid()));

            var cleanParent = runtimeParent.RootElement;
            var cleanParts = cleanParent.GetProperty("containedObjects").EnumerateArray().ToList();
            var cleanFlame = Assert.Single(cleanParts, child => child.GetProperty("Id").GetGuid() == relativePartId);
            var cleanSmoke = Assert.Single(cleanParts, child => child.GetProperty("Id").GetGuid() == roomCoordinatePartId);
            var cleanUnsetInheritancePart = Assert.Single(cleanParts, child => child.GetProperty("Id").GetGuid() == unsetInheritancePartId);
            Assert.Equal(parentId.ToString(), cleanFlame.GetProperty("designTimeParentObjectId").GetString());
            Assert.Equal(parentId.ToString(), cleanSmoke.GetProperty("designTimeParentObjectId").GetString());
            Assert.False(cleanUnsetInheritancePart.TryGetProperty("inheritParentCueCategories", out _));
            Assert.Equal("ParentRelative", cleanFlame.GetProperty("partPlacementMode").GetString());
            Assert.Equal(new[] { "Movement", "Scale", "Highlight" }, cleanFlame.GetProperty("inheritParentCueCategories").EnumerateArray().Select(value => value.GetString()));
            Assert.Equal(parentId, cleanParent.GetProperty("Id").GetGuid());
            Assert.Equal(41.5, cleanFlame.GetProperty("parentRelativeOffsetX").GetDouble());
            Assert.Equal(-13.25, cleanFlame.GetProperty("parentRelativeOffsetY").GetDouble());
            Assert.Equal(17, cleanFlame.GetProperty("imageVariants")[0].GetProperty("imageLocalAlignmentRotationDegrees").GetDouble());
            Assert.Equal(2.5, cleanFlame.GetProperty("imageVariants")[0].GetProperty("imageLocalAlignmentOffsetX").GetDouble());
            Assert.Equal(-1.25, cleanFlame.GetProperty("imageVariants")[0].GetProperty("imageLocalAlignmentOffsetY").GetDouble());
            Assert.Equal("RoomCoordinates", cleanSmoke.GetProperty("partPlacementMode").GetString());
            Assert.Empty(cleanSmoke.GetProperty("inheritParentCueCategories").EnumerateArray());
            Assert.Equal(312.75, cleanSmoke.GetProperty("partRoomCoordinateX").GetDouble());
            Assert.Equal(188.5, cleanSmoke.GetProperty("partRoomCoordinateY").GetDouble());
            Assert.Equal(-8, cleanSmoke.GetProperty("imageVariants")[0].GetProperty("imageLocalAlignmentRotationDegrees").GetDouble());
            Assert.Equal(-3.5, cleanSmoke.GetProperty("imageVariants")[0].GetProperty("imageLocalAlignmentOffsetX").GetDouble());
            Assert.Equal(4.25, cleanSmoke.GetProperty("imageVariants")[0].GetProperty("imageLocalAlignmentOffsetY").GetDouble());

            var cleanPointLight = cleanFlame.GetProperty("pointLight");
            Assert.Equal(3.25, cleanPointLight.GetProperty("offsetXPx").GetDouble());
            Assert.Equal(-4.5, cleanPointLight.GetProperty("offsetYPx").GetDouble());
            Assert.Equal(118, cleanPointLight.GetProperty("radiusPx").GetDouble());
            Assert.Equal("#FF8A22", cleanPointLight.GetProperty("color").GetString());

            var fireEffect = cleanFlame.GetProperty("particleEffect");
            Assert.Equal(fireRecipe.EffectKey, fireEffect.GetProperty("effectKey").GetString());
            Assert.Equal(1.75, fireEffect.GetProperty("offsetXPx").GetDouble());
            Assert.Equal(-2.25, fireEffect.GetProperty("offsetYPx").GetDouble());
            var fireSettings = fireEffect.GetProperty("settings").EnumerateArray().ToDictionary(setting => setting.GetProperty("controlKey").GetString()!);
            Assert.Equal(0d, fireSettings["EmissionRateMultiplier"].GetProperty("value").GetDouble());
            Assert.Equal(parentId, fireSettings["EmissionRateMultiplier"].GetProperty("binding").GetProperty("ownerId").GetGuid());
            Assert.Equal("FireRate", fireSettings["EmissionRateMultiplier"].GetProperty("binding").GetProperty("propertyName").GetString());
            Assert.False(fireSettings["EmissionRateMultiplier"].GetProperty("binding").TryGetProperty("computation", out _));
            Assert.False(fireSettings.ContainsKey("LifetimeMultiplier"));
            Assert.Equal("currentPhase", fireSettings["SpawnRadiusMultiplier"].GetProperty("binding").GetProperty("anchorKey").GetString());
            Assert.Equal("WindSpeed", fireSettings["SpawnRadiusMultiplier"].GetProperty("binding").GetProperty("propertyName").GetString());
            Assert.False(fireSettings["SpawnRadiusMultiplier"].TryGetProperty("value", out _));
            Assert.Equal("#FF6A00", fireSettings["Colors.Primary"].GetProperty("value").GetString());
            Assert.Equal("FireCore", fireSettings["Colors.Primary"].GetProperty("binding").GetProperty("propertyName").GetString());
            Assert.Equal("#FFC44D", fireSettings["Colors.Secondary"].GetProperty("value").GetString());
            Assert.Equal("FireEdge", fireSettings["Colors.Secondary"].GetProperty("binding").GetProperty("propertyName").GetString());
            var cleanProperties = cleanParent.GetProperty("gameProperties").EnumerateArray().ToList();
            Assert.All(cleanProperties, property => Assert.False(property.TryGetProperty("computation", out _)));
            Assert.Equal(fireRatePropertyId, Assert.Single(cleanProperties, property => property.GetProperty("name").GetString() == "FireRate").GetProperty("id").GetGuid());
            Assert.Equal(fireCorePropertyId, Assert.Single(cleanProperties, property => property.GetProperty("name").GetString() == "FireCore").GetProperty("id").GetGuid());
            Assert.Equal(fireEdgePropertyId, Assert.Single(cleanProperties, property => property.GetProperty("name").GetString() == "FireEdge").GetProperty("id").GetGuid());

            var projectRain = Assert.Single(runtimeProject.RootElement.GetProperty("environmentalEffects").EnumerateArray(), effect => effect.GetProperty("name").GetString() == "Rain");
            Assert.Equal(inheritedRainId, projectRain.GetProperty("id").GetGuid());
            Assert.True(projectRain.GetProperty("enabled").GetBoolean());
            Assert.Equal(0.75, projectRain.GetProperty("settings")[0].GetProperty("value").GetDouble());
            var roomEffects = runtimeRoom.RootElement.GetProperty("environmentalEffects").EnumerateArray().ToList();
            var cleanRainOptOut = Assert.Single(roomEffects, effect => effect.GetProperty("name").GetString() == "Rain");
            Assert.Equal(roomRainOptOutId, cleanRainOptOut.GetProperty("id").GetGuid());
            Assert.False(cleanRainOptOut.GetProperty("enabled").GetBoolean());
            Assert.False(cleanRainOptOut.TryGetProperty("settings", out _));
            var cleanClouds = roomEffects.Where(effect => effect.GetProperty("effectKey").GetString() == cloudRecipe.EffectKey).ToList();
            Assert.Equal(3, cleanClouds.Count);
            Assert.Equal(cloudIds.OrderBy(id => id), cleanClouds.Select(effect => effect.GetProperty("id").GetGuid()).OrderBy(id => id));
            Assert.Equal(new[] { 0d, 0.65d, 1.25d }, cleanClouds.Select(effect => effect.GetProperty("settings")[0].GetProperty("value").GetDouble()).OrderBy(value => value));
            Assert.Equal("currentPhase", Assert.Single(cleanClouds, effect => effect.GetProperty("id").GetGuid() == cloudIds[2])
                .GetProperty("settings")[0].GetProperty("binding").GetProperty("anchorKey").GetString());
            Assert.Equal(new[] { "Cloud center", "Cloud north", "Cloud south" }, cleanClouds
                .Select(effect => effect.GetProperty("name").GetString())
                .OrderBy(name => name, StringComparer.Ordinal));
            var cleanSmokeEffect = cleanSmoke.GetProperty("particleEffect");
            Assert.Equal(smokeRecipe.EffectKey, cleanSmokeEffect.GetProperty("effectKey").GetString());
            Assert.Equal(0.4, cleanSmokeEffect.GetProperty("settings")[0].GetProperty("value").GetDouble());

            var choices = runtimeProject.RootElement.GetProperty("particleEffectImageChoices").EnumerateArray().ToList();
            var choice = Assert.Single(choices);
            Assert.Equal(fireRecipe.EffectKey, choice.GetProperty("effectKey").GetString());
            Assert.Equal("flame-tip", choice.GetProperty("imageSlot").GetString());
            var exportedImagePath = choice.GetProperty("assetPath").GetString()!;
            Assert.False(Path.IsPathRooted(exportedImagePath));
            Assert.StartsWith("assets/images/_shared/", exportedImagePath, StringComparison.Ordinal);
            var stagedImage = Path.Combine(runtimeRoot, exportedImagePath.Replace('/', Path.DirectorySeparatorChar));
            Assert.Equal(SHA256.HashData(imageBytes), SHA256.HashData(File.ReadAllBytes(stagedImage)));

            using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(runtimeRoot, "assets", "assets-manifest.json")));
            var imageManifest = Assert.Single(manifest.RootElement.GetProperty("images").EnumerateArray());
            Assert.Equal(exportedImagePath, imageManifest.GetProperty("exportedPath").GetString());
            Assert.Equal(Convert.ToHexString(SHA256.HashData(imageBytes)), imageManifest.GetProperty("sha256").GetString(), ignoreCase: true);
            Assert.Contains(imageManifest.GetProperty("references").EnumerateArray().Select(value => value.GetString()),
                value => value == $"particle-effect:{fireRecipe.EffectKey}:image-slot:flame-tip:choice:0");
            Assert.Empty(manifest.RootElement.GetProperty("unresolvedSources").EnumerateArray());
        }
        finally
        {
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static ObjectBasicPropertiesEditRequest CreateSettingsRequest(
        bool isPartOfParent,
        StructuralPartPlacement placement,
        IReadOnlyList<string>? inheritParentCueCategories)
    {
        return new ObjectBasicPropertiesEditRequest(
            "Child", "", "", false, 1, false, 1, false, 1,
            false, false, false, false, false, false, false, false, false, 1, "GroupedStack",
            false, false, "AllRequired", 1, [], Array.Empty<GameObjectSelectionOption>(), "",
            IsPartOfParent: isPartOfParent,
            StructuralPartPlacement: placement,
            InheritParentCueCategories: inheritParentCueCategories);
    }

    private static (ProjectModel Project, PhaseNode Page) CreateGeneratedWindPhaseProject()
    {
        var book = new PhaseNode { Tier = PhaseTier.Book, PhaseKey = "Book", DisplayName = "Book" };
        var chapter = new PhaseNode { Tier = PhaseTier.Chapter, PhaseKey = "Chapter", DisplayName = "Chapter" };
        var page = new PhaseNode
        {
            Tier = PhaseTier.Page,
            PhaseKey = "Page",
            DisplayName = "Page",
            WindSpeed = 18,
            WindDirection = 90
        };
        Assert.True(book.AddChildScope(chapter));
        Assert.True(chapter.AddChildScope(page));
        var project = new ProjectModel { PhaseBooks = [book] };
        return (project, page);
    }

    private static double ReadNumber(object? value) => value switch
    {
        System.Text.Json.JsonElement element => element.GetDouble(),
        IConvertible convertible => convertible.ToDouble(System.Globalization.CultureInfo.InvariantCulture),
        _ => throw new InvalidOperationException($"Expected numeric setting, got {value?.GetType().Name ?? "null"}.")
    };

    private static string? ReadText(object? value) => value switch
    {
        System.Text.Json.JsonElement element when element.ValueKind == System.Text.Json.JsonValueKind.String => element.GetString(),
        string text => text,
        _ => null
    };
}
