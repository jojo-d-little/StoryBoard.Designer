using System;
using System.Collections.Generic;

namespace StoryboardDesigner.App.Models;

public sealed record ActionPayloadSchemaEntry(
    CommandActionType ActionType,
    Type? PayloadType,
    IReadOnlyList<string> OwnedFields);
