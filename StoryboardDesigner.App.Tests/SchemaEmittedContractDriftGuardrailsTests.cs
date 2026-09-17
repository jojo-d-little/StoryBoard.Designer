using System.Security.Cryptography;
using System.Text;
using Storyboard.SchemaCodegen;

namespace StoryboardDesigner.App.Tests;

public sealed class SchemaEmittedContractDriftGuardrailsTests
{
    private readonly record struct LockManifestEntry(string DtoFileName, string SchemaFileName, string SchemaSha256);

    [Fact]
    public void LockedRuntimeContractDtos_MatchSchemaEmitterOutput_AndContainGeneratedHeader()
    {
        var repoRoot = Program.ResolveProjectRootForGuardrails();
        var schemaRoot = Path.Combine(repoRoot, "Storyboard.Shared.Contracts", "RuntimeContracts", "Schemas");
        var contractDtoRoot = Path.Combine(repoRoot, "Storyboard.Shared.Contracts", "RuntimeContracts", "Dtos");
        var lockManifestPath = Path.Combine(repoRoot, "CodegenManagment", "ContractLock", "locked-contract-dtos.txt");

        AssertLockedArtifactsMatchGeneratedOutput(
            lockManifestPath,
            contractDtoRoot,
            schemaRoot,
            schemaPath => Program.GenerateContractPreviewFromSchemaPath(schemaPath, noComments: false),
            "dto");
    }

    [Fact]
    public void LockedDesignerContractDtos_MatchSchemaEmitterOutput_AndContainGeneratedHeader()
    {
        var repoRoot = Program.ResolveProjectRootForGuardrails();
        var schemaRoot = Path.Combine(repoRoot, "Storyboard.Shared.Contracts", "RuntimeContracts", "Schemas");
        var designerContractDtoRoot = Path.Combine(repoRoot, "StoryboardDesigner.App", "Services", "DesignerJsonContracts");
        var lockManifestPath = Path.Combine(repoRoot, "CodegenManagment", "ContractLock", "locked-designer-contract-dtos.txt");

        AssertLockedArtifactsMatchGeneratedOutput(
            lockManifestPath,
            designerContractDtoRoot,
            schemaRoot,
            schemaPath => Program.GenerateDesignerContractPreviewFromSchemaPath(schemaPath, noComments: false),
            "designer-dto");
    }

    [Fact]
    public void LockedRuntimeContractEnums_MatchSchemaEmitterOutput()
    {
        var repoRoot = Program.ResolveProjectRootForGuardrails();
        var enumSchemaRoot = Path.Combine(repoRoot, "Storyboard.Shared.Contracts", "RuntimeContracts", "Schemas", "Enums");
        var enumRoot = Path.Combine(repoRoot, "Storyboard.Shared.Contracts", "RuntimeContracts", "Enums");
        var lockManifestPath = Path.Combine(repoRoot, "CodegenManagment", "ContractLock", "locked-contract-enums.txt");

        AssertLockedArtifactsMatchGeneratedOutput(
            lockManifestPath,
            enumRoot,
            enumSchemaRoot,
            schemaPath => Program.GenerateEnumPreviewFromSchemaPath(schemaPath, noComments: false),
            "enum");
    }

    [Fact]
    public void LockedRuntimeContractCustomTypes_MatchSchemaEmitterOutput()
    {
        var repoRoot = Program.ResolveProjectRootForGuardrails();
        var customTypeSchemaRoot = Path.Combine(repoRoot, "Storyboard.Shared.Contracts", "RuntimeContracts", "Schemas", "CustomTypes");
        var customTypeRoot = Path.Combine(repoRoot, "Storyboard.Shared.Contracts", "RuntimeContracts", "CustomTypes");
        var lockManifestPath = Path.Combine(repoRoot, "CodegenManagment", "ContractLock", "locked-contract-custom-types.txt");

        if (!File.Exists(lockManifestPath))
        {
            return;
        }

        AssertLockedArtifactsMatchGeneratedOutput(
            lockManifestPath,
            customTypeRoot,
            customTypeSchemaRoot,
            schemaPath => Program.GenerateCustomTypePreviewFromSchemaPath(schemaPath, noComments: false),
            "custom-type");
    }

    [Fact]
    public void LockedHostContractDtos_MatchSchemaEmitterOutput_AndContainGeneratedHeader()
    {
        var repoRoot = Program.ResolveProjectRootForGuardrails();
        var schemaRoot = Path.Combine(repoRoot, "Storyboard.Shared.Contracts", "HostContracts", "Schemas", "Dtos");
        var hostContractDtoRoot = Path.Combine(repoRoot, "Storyboard.Shared.Contracts", "HostContracts");
        var lockManifestPath = Path.Combine(repoRoot, "CodegenManagment", "ContractLock", "locked-host-contract-dtos.txt");

        AssertLockedArtifactsMatchGeneratedOutput(
            lockManifestPath,
            hostContractDtoRoot,
            schemaRoot,
            schemaPath => Program.GenerateContractPreviewFromSchemaPath(schemaPath, noComments: false),
            "host-dto",
            ResolveHostDtoArtifactPath);
    }

    [Fact]
    public void LockedHostContractEnums_MatchSchemaEmitterOutput()
    {
        var repoRoot = Program.ResolveProjectRootForGuardrails();
        var enumSchemaRoot = Path.Combine(repoRoot, "Storyboard.Shared.Contracts", "HostContracts", "Schemas", "Enums");
        var enumRoot = Path.Combine(repoRoot, "Storyboard.Shared.Contracts", "HostContracts", "Enums");
        var lockManifestPath = Path.Combine(repoRoot, "CodegenManagment", "ContractLock", "locked-host-contract-enums.txt");

        AssertLockedArtifactsMatchGeneratedOutput(
            lockManifestPath,
            enumRoot,
            enumSchemaRoot,
            schemaPath => Program.GenerateEnumPreviewFromSchemaPath(schemaPath, noComments: false),
            "host-enum");
    }

    private static void AssertLockedArtifactsMatchGeneratedOutput(
        string lockManifestPath,
        string artifactRoot,
        string schemaRoot,
        Func<string, string> generateExpected,
        string artifactLabel,
        Func<string, LockManifestEntry, string>? resolveArtifactPath = null)
    {
        Assert.True(File.Exists(lockManifestPath), $"Missing lock manifest: {lockManifestPath}");

        var lockedEntries = new List<LockManifestEntry>();
        foreach (var line in File.ReadAllLines(lockManifestPath))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            var parts = trimmed.Split('|', StringSplitOptions.TrimEntries);
            Assert.True(parts.Length == 3,
                $"Lock manifest entry must use 'artifact|schema|sha256' format. Found: '{trimmed}'.");

            Assert.False(string.IsNullOrWhiteSpace(parts[0]), $"Missing artifact file name in lock manifest entry: '{trimmed}'.");
            Assert.False(string.IsNullOrWhiteSpace(parts[1]), $"Missing schema file name in lock manifest entry: '{trimmed}'.");
            Assert.False(string.IsNullOrWhiteSpace(parts[2]), $"Missing schema hash in lock manifest entry: '{trimmed}'.");

            lockedEntries.Add(new LockManifestEntry(parts[0], parts[1], parts[2].ToLowerInvariant()));
        }

        foreach (var entry in lockedEntries)
        {
            var artifactPath = resolveArtifactPath is null
                ? Path.Combine(artifactRoot, entry.DtoFileName)
                : resolveArtifactPath(artifactRoot, entry);
            Assert.True(File.Exists(artifactPath), $"Locked contract {artifactLabel} file not found: {artifactPath}");

            var schemaPath = Path.Combine(schemaRoot, entry.SchemaFileName);
            Assert.True(File.Exists(schemaPath), $"Schema file not found for locked contract {artifactLabel}: {schemaPath}");

            var actualSchemaSha256 = ComputeNormalizedSha256Hex(File.ReadAllText(schemaPath));
            Assert.Equal(entry.SchemaSha256, actualSchemaSha256);

            var expected = NormalizeLineEndings(generateExpected(schemaPath)).TrimEnd();
            var actual = NormalizeLineEndings(File.ReadAllText(artifactPath)).TrimEnd();

            Assert.Equal(expected, actual);
        }
    }

    private static string NormalizeLineEndings(string value)
    {
        return value
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal);
    }

    private static string ComputeNormalizedSha256Hex(string value)
    {
        var normalized = NormalizeLineEndings(value);
        var bytes = Encoding.UTF8.GetBytes(normalized);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string ResolveHostDtoArtifactPath(string artifactRoot, LockManifestEntry entry)
    {
        var dtoFileName = entry.DtoFileName.Replace('/', Path.DirectorySeparatorChar);
        if (dtoFileName.Contains(Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            return Path.Combine(artifactRoot, dtoFileName);
        }

        var schemaDirectory = Path.GetDirectoryName(entry.SchemaFileName.Replace('/', Path.DirectorySeparatorChar));
        if (!string.IsNullOrWhiteSpace(schemaDirectory))
        {
            return Path.Combine(artifactRoot, schemaDirectory, dtoFileName);
        }

        return Path.Combine(artifactRoot, dtoFileName);
    }
}
