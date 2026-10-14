using StoryboardDesigner.App.Models;
using StoryboardDesigner.App.ViewModels;

namespace StoryboardDesigner.App.Tests;

public sealed class MainWindowViewModelSpatialSoundEmitterTests
{
    [Fact]
    public void EditObjectProperties_ProvidesSoundOptionsAndPersistsEmitters()
    {
        var soundId = Guid.NewGuid();
        var propertyId = Guid.NewGuid();
        var sound = new SoundEffectLibraryEntry
        {
            SoundEffectId = soundId,
            SoundEffectKey = "machine.hum",
            DisplayName = "Machine Hum",
            AssetRef = "hum.wav"
        };
        var obj = new GameObject
        {
            Name = "Generator",
            Variables = [new GamePropertyDefinition
            {
                Id = propertyId,
                Name = "isRunning",
                DefaultValue = "false",
                ValueRestriction = GamePropertyValueRestriction.TrueFalse
            }]
        };
        var room = new Room { Name = "Workshop", GameObjects = [obj] };
        var area = new Area { Name = "Area", Rooms = [room] };
        var project = new ProjectModel
        {
            GlobalScope = { SoundEffectLibraryEntries = [sound] },
            Planets = [new Planet { Name = "Planet", Countries = [new Country { Name = "Country", Areas = [area] }] }]
        };
        ScopeHierarchy.AttachParents(project);

        var editedEmitter = new ObjectSoundEmitter
        {
            SoundEffectId = soundId,
            ActivationGamePropertyId = propertyId,
            ActiveInRoom = true,
            ActiveOutOfRoom = true
        };
        var treeContext = new TreeContextInteractionServiceStub
        {
            EditObjectBasicPropertiesHandler = initial =>
            {
                Assert.Contains(initial.SoundEffectChoices!, choice => choice.SoundEffectId == soundId);
                Assert.Contains(initial.SoundEmitterActivationProperties!, property => property.Id == propertyId);
                return initial with { SoundEmitters = [editedEmitter] };
            }
        };
        var viewModel = MainWindowViewModelTestHarness.CreateViewModel(project, treeContext, new ProjectUiServiceStub());
        var rootNode = new ProjectRootNodeViewModel(project);
        var planetNode = new PlanetNodeViewModel(project, project.Planets[0], rootNode);
        var countryNode = new CountryNodeViewModel(project.Planets[0].Countries[0], planetNode);
        var areaNode = new AreaNodeViewModel(area, countryNode);
        var roomNode = new RoomNodeViewModel(room, area, areaNode);
        var objectNode = new GameObjectNodeViewModel(obj, new RoomGameObjectsNodeViewModel(roomNode));

        Assert.True(viewModel.EditObjectBasicProperties(objectNode));
        var persistedEmitter = Assert.Single(obj.SoundEmitters);
        Assert.Equal(soundId, persistedEmitter.SoundEffectId);
        Assert.Equal(propertyId, persistedEmitter.ActivationGamePropertyId);
        Assert.True(persistedEmitter.ActiveOutOfRoom);
    }
}
