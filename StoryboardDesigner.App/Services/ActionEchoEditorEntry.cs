namespace StoryboardDesigner.App.Services;

public sealed class ActionEchoEditorEntry
{
    public required string Token { get; init; }
    public required bool IsSupported { get; init; }
    public required string OutcomeLabel { get; init; }
    public string Script { get; set; } = string.Empty;

    public string SupportStatus => IsSupported ? "Supported" : "Unsupported (inert at runtime)";

    public string SupportHint => IsSupported
        ? "This result code is currently supported by the runtime action executor."
        : "This imported result code is preserved for compatibility but is ignored by runtime execution.";

    public string ScriptPreview
    {
        get
        {
            if (string.IsNullOrEmpty(Script))
            {
                return string.Empty;
            }

            var normalized = Script.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
            var firstLine = normalized.Split('\n')[0].Trim();
            return firstLine;
        }
    }
}
