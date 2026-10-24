using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.Models.Enums;
using StoryboardDesigner.App.Services;
using StoryboardDesigner.App.Validation.Rules.Objects;
using StoryboardDesigner.App.Views;
using Storyboard.Shared.DesignerContracts;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace StoryboardDesigner.App.Tests;

[Collection("WPF STA")]
public sealed class ParticleEffectAuthoringPersistenceTests
{
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
                            PropertyName = "EffectiveWindSpeed"
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
                    button.Content?.ToString() == "currentPhase.EffectiveWindSpeed"
                    && (button.ToolTip?.ToString() ?? string.Empty).Contains("Bound to currentPhase.EffectiveWindSpeed", StringComparison.Ordinal));
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
    public void StandardVariableChooser_ReturnsDirectWellKnownAnchorReference()
    {
        Exception? failure = null;
        StaTestThreadRunner.StartAndWait(new Thread(() =>
        {
            try
            {
                var choice = new StoryboardDesigner.App.ViewModels.GamePropertyChoiceItem
                {
                    Value = "currentPhase.EffectiveWindSpeed",
                    ScopePath = "Current phase",
                    OwnerVariable = "Current phase.EffectiveWindSpeed",
                    Scope = PropertyResolutionScope.PhasePage,
                    ValueRestriction = GamePropertyValueRestriction.Numeric,
                    PropertyName = "EffectiveWindSpeed",
                    EligibleAnchors = ["currentPhase"]
                };
                var context = new GamePropertyPickerContext(
                    new ProjectModel(),
                    null,
                    PropertyResolutionScope.PhasePage,
                    _ => [choice],
                    AllowReferenceModes: true,
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
                    grid.SelectedItem = choice;
                    mode.SelectedIndex = 2;
                    anchor.SelectedItem = "currentPhase";
                    typeof(VariableChooserDialog)
                        .GetMethod("Select_OnClick", BindingFlags.Instance | BindingFlags.NonPublic)!
                        .Invoke(picker, [picker, new RoutedEventArgs(Button.ClickEvent)]);
                };
                timer.Start();
                Assert.True(picker.ShowDialog());
                Assert.Equal("currentPhase", picker.SelectedReference?.AnchorKey);
                Assert.Equal("EffectiveWindSpeed", picker.SelectedReference?.PropertyName);
                Assert.Null(picker.SelectedReference?.OwnerId);
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        }), nameof(StandardVariableChooser_ReturnsDirectWellKnownAnchorReference));

        if (failure is not null) throw failure;
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
                var condition = new ProjectGamePropertyConditionDto
                {
                    Kind = "Compare",
                    GamePropertySource = new ProjectGamePropertySourceReferenceDto
                    {
                        AnchorKey = "currentPhase",
                        PropertyName = "TimeOfDay"
                    },
                    Operator = "Equals",
                    RightOperand = new ProjectGamePropertyOperandDto { ValueKind = "Text", Value = "Evening" }
                };
                var computation = new ProjectGamePropertyComputationDto { ResultKind = "Boolean", Condition = condition };
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
                        var reviewConditionButton = (Button)typeof(GamePropertyComputationEditorDialog)
                            .GetField("EditBooleanConditionButton", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(review)!;
                        Assert.True(reviewConditionButton.IsEnabled);
                        Assert.Equal("Review condition tree...", reviewConditionButton.Content);
                        Assert.Contains("currentPhase.TimeOfDay", ((TextBlock)typeof(GamePropertyComputationEditorDialog)
                            .GetField("BooleanConditionTextBlock", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(review)!).Text);
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

                var conditionReview = new GamePropertyConditionEditorDialog(condition, _ => null, _ => "currentPhase.TimeOfDay", readOnly: true);
                var addButton = (Button)typeof(GamePropertyConditionEditorDialog)
                    .GetField("AddCompareButton", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(conditionReview)!;
                var saveButton = (Button)typeof(GamePropertyConditionEditorDialog)
                    .GetField("SaveButton", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(conditionReview)!;
                Assert.False(addButton.IsEnabled);
                Assert.Equal("Close", saveButton.Content);
                conditionReview.Close();
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
                PropertyName = "EffectiveWindSpeed"
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
            Assert.Equal("EffectiveWindSpeed", loadedSettings["EmissionRateMultiplier"].Binding!.PropertyName);
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
