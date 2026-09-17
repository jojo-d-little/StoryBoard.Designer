using System.Text.RegularExpressions;
using Storyboard.SchemaCodegen;

namespace StoryboardDesigner.App.Tests;

public sealed class SchemaCodegenHardcodedDtoGuardrailsTests
{
    private static readonly Regex HardcodedDtoClassNameEqualsPattern = new(
                @"string\.Equals\(\s*className\s*,\s*""Runtime[A-Za-z0-9_]*Dto""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex HardcodedDtoClassNameSwitchArmPattern = new(
                @"""Runtime[A-Za-z0-9_]*Dto""\s*=>",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    [Fact]
    public void SchemaCodegenProgram_DoesNotHardcodeDtoClassNamesInGenerationBranches()
    {
        var repoRoot = Program.ResolveProjectRootForGuardrails();
        var programPath = Path.Combine(repoRoot, "Storyboard.SchemaCodegen", "Program.cs");

        Assert.True(File.Exists(programPath), $"Schema codegen source not found: {programPath}");

        var source = File.ReadAllText(programPath);

        Assert.DoesNotMatch(HardcodedDtoClassNameEqualsPattern, source);
        Assert.DoesNotMatch(HardcodedDtoClassNameSwitchArmPattern, source);
    }
}
