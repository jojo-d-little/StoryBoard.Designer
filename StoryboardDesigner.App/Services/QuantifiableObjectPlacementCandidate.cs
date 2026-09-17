using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.Services;

public sealed record QuantifiableObjectPlacementCandidate(
    GameObject SourceObject,
    string SourceScopePath,
    int ScopePriority);
