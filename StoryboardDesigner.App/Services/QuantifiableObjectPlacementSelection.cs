using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.Services;

public sealed record QuantifiableObjectPlacementSelection(
    GameObject SourceObject,
    int Quantity);
