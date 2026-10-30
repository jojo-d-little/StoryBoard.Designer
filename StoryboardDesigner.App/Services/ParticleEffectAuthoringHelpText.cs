namespace StoryboardDesigner.App.Services;

internal static class ParticleEffectAuthoringHelpText
{
    public static string GetDisplayLabel(ParticleEffectControlMetadata control, string? roleLabel = null)
    {
        if (!string.IsNullOrWhiteSpace(roleLabel)) return roleLabel.Trim();
        var label = string.IsNullOrWhiteSpace(control.Label) ? control.Name : control.Label.Trim();
        return string.IsNullOrWhiteSpace(control.Unit) ? label : $"{label} ({control.Unit.Trim()})";
    }

    public static string GetColorRoleDisplayLabel(ParticleEffectControlMetadata control, bool primary)
    {
        var roleLabel = primary ? control.PrimaryRoleLabel : control.SecondaryRoleLabel;
        return string.IsNullOrWhiteSpace(roleLabel)
            ? primary ? "Primary color" : "Secondary color"
            : roleLabel.Trim();
    }

    public static string GetControlTooltip(ParticleEffectControlMetadata control, string? roleLabel = null, bool isColor = false)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(control.Description)) parts.Add(control.Description.Trim());
        else if (!string.IsNullOrWhiteSpace(control.Name)) parts.Add($"{control.Name} control from the particle effect recipe.");

        if (!string.IsNullOrWhiteSpace(roleLabel))
            parts.Add($"{roleLabel.Trim()} is an independent role with its own literal value or variable binding.");
        if (isColor) parts.Add("Enter an sRGB hex color such as #AABBCC.");

        parts.Add("A usable binding wins. The text is the literal or fallback; with no usable binding or literal, the recipe baseline applies.");
        if (string.Equals(control.Kind, "number", StringComparison.OrdinalIgnoreCase))
            parts.Add("Zero is a real value and follows this control's catalog zero policy.");
        else if (string.Equals(control.Kind, "boolean", StringComparison.OrdinalIgnoreCase))
            parts.Add("False is a real Boolean value.");
        parts.Add("Blank text does not unbind; Default clears both literal and binding.");
        return string.Join(" ", parts);
    }
}
