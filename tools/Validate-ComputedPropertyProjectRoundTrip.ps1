[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $LightingEffectsTestProjectPath,

    [Parameter(Mandatory = $true)]
    [string] $WorkshopTutorialProjectPath
)

# The automated round-trip test exercises JsonExportService, the same project
# load/save/clean-export path used by the WPF shell, but does not itself open WPF.
# The second test run launches the actual Designer and opens each isolated copy.

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPaths = @(
    (Resolve-Path -LiteralPath $LightingEffectsTestProjectPath).Path,
    (Resolve-Path -LiteralPath $WorkshopTutorialProjectPath).Path
)

foreach ($projectPath in $projectPaths) {
    if (-not (Test-Path -LiteralPath $projectPath -PathType Leaf)) {
        throw "Project file does not exist: $projectPath"
    }
}

$pathsEnvironmentVariable = 'STORYBOARD_COMPUTED_PROPERTY_ROUNDTRIP_PROJECTS'
$appExecutableEnvironmentVariable = 'STORYBOARD_DESIGNER_APP_EXE'
$priorPaths = [Environment]::GetEnvironmentVariable($pathsEnvironmentVariable, 'Process')
$priorAppExecutable = [Environment]::GetEnvironmentVariable($appExecutableEnvironmentVariable, 'Process')
[Environment]::SetEnvironmentVariable($pathsEnvironmentVariable, [string]::Join([Environment]::NewLine, $projectPaths), 'Process')

try {
    Write-Host 'Running isolated Designer service round trips (load, save, clean export, reload)...'
    & dotnet test (Join-Path $repoRoot 'StoryboardDesigner.App.Tests\StoryboardDesigner.App.Tests.csproj') `
        --no-restore `
        --filter 'FullyQualifiedName~ComputedPropertyProjectRoundTripValidatorTests' `
        --logger 'console;verbosity=detailed' `
        '-p:OutputPath=bin/ComputedPropertyRoundTripValidation/' `
        '-p:UseAppHost=false'
    if ($LASTEXITCODE -ne 0) {
        throw "Designer round-trip validation failed with exit code $LASTEXITCODE."
    }

    $appExecutable = Join-Path $repoRoot 'StoryboardDesigner.App\bin\ComputedPropertyProjectOpenSmoke\StoryboardDesigner.App.exe'
    [Environment]::SetEnvironmentVariable($appExecutableEnvironmentVariable, $appExecutable, 'Process')

    Write-Host 'Building and smoke-opening each isolated project copy through WPF startup...'
    & dotnet test (Join-Path $repoRoot 'StoryboardDesigner.App.SmokeTests\StoryboardDesigner.App.SmokeTests.csproj') `
        --no-restore `
        --filter 'FullyQualifiedName~ComputedPropertyProjectOpenSmokeTests' `
        --logger 'console;verbosity=detailed' `
        '-p:OutputPath=bin/ComputedPropertyProjectOpenSmoke/' `
        '-p:UseAppHost=true'
    if ($LASTEXITCODE -ne 0) {
        throw "WPF project-open smoke validation failed with exit code $LASTEXITCODE."
    }
}
finally {
    [Environment]::SetEnvironmentVariable($pathsEnvironmentVariable, $priorPaths, 'Process')
    [Environment]::SetEnvironmentVariable($appExecutableEnvironmentVariable, $priorAppExecutable, 'Process')
}
