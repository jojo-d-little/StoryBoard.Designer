using Storyboard.Shared.GameStateData;
using Storyboard.Shared.RuntimeContracts;
using Storyboard.Shared.RuntimeContracts.Enums;

/*
namespace StoryboardDesigner.App.Models;

internal sealed class Adapter : IRuntimeScopeNode
{
    private readonly IScopedAwareNode _node;

    public Adapter(IScopedAwareNode node)
    {
        _node = node;
    }

    public ScopeNodeKind ScopeKind => _node.ScopeKind switch
    {
        ScopeNodeKind.RoomTemplates => ScopeNodeKind.Templates,
        _ => _node.ScopeKind
    };

    public string Name => _node.ScopeName;

    public string NameInGame => _node.ScopeNameInGame;

    public Guid? ScopeNodeId => _node switch
    {
        Room room => room.Id,
        GameObject obj => obj.ObjectId,
        _ => RuntimeScopeIdentity.CreateDeterministicScopeNodeId(
            ScopeKind,
            BuildStableScopePath(_node))
    };

    public IEnumerable<string> ScopeTokens => _node.ScopeTokens;

    public Guid? RuntimeParentScopeNodeId => ResolveRuntimeParentScopeNodeId(_node.ParentScope);

    public IRuntimeScopeNode? ParentScope => _node.ParentScope.AsRuntimeScopeNode();

    public IEnumerable<IRuntimeScopeNode> ChildScopes =>
        (_node.ChildScopes ?? Array.Empty<IScopedAwareNode>())
        .Select(static child => child.AsRuntimeScopeNode())
        .OfType<IRuntimeScopeNode>();

    public IEnumerable<string> AdditionalVerbs => _node switch
    {
        ProjectModel project => project.CommandVerbs,
        Planet planet => planet.AdditionalVerbs,
        Country country => country.AdditionalVerbs,
        Area area => area.AdditionalVerbs,
        Room room => room.AdditionalVerbs,
        GameObject obj => obj.AdditionalVerbs,
        _ => Array.Empty<string>()
    };

    public IEnumerable<string> AdditionalDirectionals => _node switch
    {
        ProjectModel project => project.Directionals,
        Planet planet => planet.AdditionalDirectionals,
        Country country => country.AdditionalDirectionals,
        Area area => area.AdditionalDirectionals,
        Room room => room.AdditionalDirectionals,
        GameObject obj => obj.AdditionalDirectionals,
        _ => Array.Empty<string>()
    };

    public IEnumerable<RuntimeDirectionalTraversalMapping> AdditionalDirectionalTraversalMappings => _node switch
    {
        ProjectModel project => project.DirectionalTraversalMappings
            .Select(static mapping => new RuntimeDirectionalTraversalMapping(mapping.Token, mapping.TraversalDirection)),
        Planet planet => planet.AdditionalDirectionalTraversalMappings
            .Select(static mapping => new RuntimeDirectionalTraversalMapping(mapping.Token, mapping.TraversalDirection)),
        Country country => country.AdditionalDirectionalTraversalMappings
            .Select(static mapping => new RuntimeDirectionalTraversalMapping(mapping.Token, mapping.TraversalDirection)),
        Area area => area.AdditionalDirectionalTraversalMappings
            .Select(static mapping => new RuntimeDirectionalTraversalMapping(mapping.Token, mapping.TraversalDirection)),
        Room room => room.AdditionalDirectionalTraversalMappings
            .Select(static mapping => new RuntimeDirectionalTraversalMapping(mapping.Token, mapping.TraversalDirection)),
        GameObject obj => obj.AdditionalDirectionalTraversalMappings
            .Select(static mapping => new RuntimeDirectionalTraversalMapping(mapping.Token, mapping.TraversalDirection)),
        _ => Array.Empty<RuntimeDirectionalTraversalMapping>()
    };

    private static string[] BuildStableScopePath(IScopedAwareNode node)
    {
        var segments = node
            .EnumerateSelfAndAncestors()
            .Reverse()
            .Select(static scope => scope.ScopeName?.Trim() ?? string.Empty)
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .ToArray();

        return segments.Length == 0 ? new[] { "unnamed" } : segments;
    }

    private static Guid? ResolveRuntimeParentScopeNodeId(IScopedAwareNode? parent)
    {
        if (parent is null)
        {
            return null;
        }

        if (parent is ProjectModel)
        {
            return RuntimeObjectIdentity.PlayerObjectRootStableId;
        }

        return parent switch
        {
            Room room => room.Id,
            GameObject obj => obj.ObjectId,
            _ => RuntimeScopeIdentity.CreateDeterministicScopeNodeId(
                parent.ScopeKind switch
                {
                    ScopeNodeKind.RoomTemplates => ScopeNodeKind.Templates,
                    _ => parent.ScopeKind
                },
                BuildStableScopePath(parent))
        };
    }

}

*/


