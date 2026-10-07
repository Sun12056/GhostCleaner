# WinCleaner build / test / run / publish helper
# The .NET 8 SDK on this machine lives at D:\DevTools\dotnet (not on C:), so PATH is set explicitly.

param(
    [Parameter(Mandatory = $false)]
    [ValidateSet('build', 'test', 'run', 'publish')]
    [string]$Target = 'build',

    [Parameter(Mandatory = $false)]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'

$dotnetRoot = 'D:\DevTools\dotnet'
if (-not (Test-Path (Join-Path $dotnetRoot 'dotnet.exe'))) {
    throw "dotnet.exe not found in $dotnetRoot - install .NET 8 SDK or update dotnetRoot in this script."
}

$env:Path = $dotnetRoot + [IO.Path]::PathSeparator + $env:Path
$env:DOTNET_ROOT = $dotnetRoot
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
# Keep the NuGet cache on drive D as well
$env:NUGET_PACKAGES = 'D:\DevTools\nuget-packages'
$env:DOTNET_CLI_HOME = 'D:\DevTools\dotnet-home'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root

function Invoke-Step {
    param([string]$Name, [scriptblock]$Action)

    Write-Host "==> $Name" -ForegroundColor Cyan
    & $Action
    if ($LASTEXITCODE -ne 0) { throw "$Name failed (exit code $LASTEXITCODE)" }
}

switch ($Target) {
    'build' {
        Invoke-Step 'Build solution' { dotnet build WinCleaner.sln -c $Configuration --nologo }
    }
    'test' {
        Invoke-Step 'Run unit tests' { dotnet test WinCleaner.sln -c $Configuration --nologo }
    }
    'run' {
        Invoke-Step 'Start WPF app (requires elevation, UAC prompt)' {
            dotnet run --project src/WinCleaner.App/WinCleaner.App.csproj -c $Configuration
        }
    }
    'publish' {
        Invoke-Step 'Publish single-folder build' {
            dotnet publish src/WinCleaner.App/WinCleaner.App.csproj -c $Configuration -o (Join-Path $root 'publish')
        }
        Write-Host "Published to $root\publish" -ForegroundColor Green
    }
}

Write-Host 'Done.' -ForegroundColor Green
