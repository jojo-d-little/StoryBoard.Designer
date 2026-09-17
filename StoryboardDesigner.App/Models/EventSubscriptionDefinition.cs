using Storyboard.Shared.RuntimeContracts.Enums;

namespace StoryboardDesigner.App.Models;

public sealed class EventSubscriptionDefinition
{
    public Guid Id { get; set; }

    public string EventKey { get; set; } = string.Empty;

    public string SubscriptionName { get; set; } = string.Empty;

    public bool IsEnabled { get; set; } = true;

    public string Lane { get; set; } = "foreground";

    public EventSubscriberDispatchDisposition DispatchDisposition { get; set; } = EventSubscriberDispatchDisposition.bubble;

    public bool SubscriptionVisibleWhenContained { get; set; }

    public SubscriptionSourceMatchMode SubscriptionSourceMatchMode { get; set; } = SubscriptionSourceMatchMode.AnySource;

    public Guid? SubscriptionSourceScopeNodeId { get; set; }

    public SubscriptionSourceMatchMode SubscriptionSecondarySourceMatchMode { get; set; } = SubscriptionSourceMatchMode.AnySource;

    public Guid? SubscriptionSecondarySourceScopeNodeId { get; set; }

    public List<EventActionBindingDefinition> ActionBindings { get; set; } = new();

    public List<EventInputArgumentMappingDefinition> InputArgumentMappings { get; set; } = new();
}
