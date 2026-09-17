using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.Validation.Contracts;

public sealed record ValidationGamePropertyDescriptor(
    Guid VariableId,
    string VariableName,
    string ScopePath,
    GamePropertyValueRestriction ValueRestriction,
    bool IsShared,
    string? SemanticHint);
