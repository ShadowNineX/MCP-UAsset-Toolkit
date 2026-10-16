[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string[]]$Configuration = @('Debug', 'Release'),
    [switch]$Publish
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $repositoryRoot 'MCP.UAssetToolkit.slnx'
$serverProject = Join-Path $repositoryRoot 'src/MCP.UAssetToolkit/MCP.UAssetToolkit.csproj'

function Invoke-DotNet([string[]]$Arguments) {
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

Push-Location $repositoryRoot
try {
    Invoke-DotNet -Arguments @('restore', $solution)
    foreach ($buildConfiguration in $Configuration) {
        Invoke-DotNet -Arguments @('build', $solution, '--configuration', $buildConfiguration, '--no-restore', '--no-incremental')
        Invoke-DotNet -Arguments @('test', '--solution', $solution, '--configuration', $buildConfiguration, '--no-build')
    }

    if ($Publish) {
        $publishDirectory = Join-Path $repositoryRoot 'artifacts/publish/Release'
        Invoke-DotNet -Arguments @('publish', $serverProject, '--configuration', 'Release', '--runtime', 'win-x64', '--self-contained', 'true', '--output', $publishDirectory)
    }
}
finally {
    Pop-Location
}
