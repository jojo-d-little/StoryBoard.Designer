using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.ViewModels;

public sealed class ScopedEventSubscriptionEntryNodeViewModel : HierarchyNodeViewModel
{
    public ScopedEventSubscriptionEntryNodeViewModel(EventSubscriptionDefinition entry, ScopedEventSubscriptionsNodeViewModel parent)
        : base(ResolveDisplayName(entry), parent)
    {
        Entry = entry;
        ParentEventSubscriptionsNode = parent;
        NodeTypeLabel = "Event Subscription";
    }

    public EventSubscriptionDefinition Entry { get; }

    public ScopedEventSubscriptionsNodeViewModel ParentEventSubscriptionsNode { get; }

    private static string ResolveDisplayName(EventSubscriptionDefinition entry)
    {
        var subscriptionName = entry.SubscriptionName?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(subscriptionName))
        {
            return subscriptionName;
        }

        return string.IsNullOrWhiteSpace(entry.EventKey) ? "(Missing Event Key)" : entry.EventKey;
    }

    protected override void RenameModel(string newName)
    {
    }
}
