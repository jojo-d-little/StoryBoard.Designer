using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.ViewModels;

public sealed class ScopedEventSubscriptionsNodeViewModel : HierarchyNodeViewModel
{
    public ScopedEventSubscriptionsNodeViewModel(PropertyResolutionScope scope, IList<EventSubscriptionDefinition> subscriptions, HierarchyNodeViewModel parent)
        : base("Event Subscriptions", parent)
    {
        Scope = scope;
        Subscriptions = subscriptions;
        NodeTypeLabel = "Event Subscriptions";
    }

    public PropertyResolutionScope Scope { get; }

    public IList<EventSubscriptionDefinition> Subscriptions { get; }

    protected override void RenameModel(string newName)
    {
    }
}
