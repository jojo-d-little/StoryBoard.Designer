using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.Services;

public sealed record GameObjectSelectionOptionDiscoveryRequest(
    GameObject CurrentObject,
    GameObjectFeatureRequirements RequiredFeatures = GameObjectFeatureRequirements.None,
    ScopeSearchDepth ScopeSearchDepth = ScopeSearchDepth.LocalRoom,
    GameObjectOptionSourceTarget ScopeSearchType = GameObjectOptionSourceTarget.RealObjects,
    bool ExcludeCurrentObject = true);

public interface IGameObjectSelectionOptionDiscoveryService
{
    IReadOnlyList<GameObjectSelectionOption> Discover(GameObjectSelectionOptionDiscoveryRequest request);
}

