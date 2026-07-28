param(
    [string]$InstallDir,
    [string]$Channel = "9.0",
    [string]$BuildDataRoot
)

$ErrorActionPreference = "Stop"

$projectRoot = $PSScriptRoot
if ((Split-Path -Leaf $PSScriptRoot) -ieq "build") {
    $projectRoot = Split-Path -Parent $PSScriptRoot
}

function Resolve-BuildDataRoot {
    param(
        [string]$ProjectRoot,
        [string]$ExplicitRoot
    )

    if (-not [string]::IsNullOrWhiteSpace($ExplicitRoot)) {
        return $ExplicitRoot
    }
    if (-not [string]::IsNullOrWhiteSpace($env:SMART_NAP_BUILD_DATA_ROOT)) {
        return $env:SMART_NAP_BUILD_DATA_ROOT
    }
    if (-not [string]::IsNullOrWhiteSpace($env:CODEX_BUILD_DATA_ROOT)) {
        return (Join-Path $env:CODEX_BUILD_DATA_ROOT "SmartBackgroundNap")
    }
    if (Test-Path -LiteralPath "D:\") {
        return "D:\CodexBuildData\SmartBackgroundNap"
    }

    return (Join-Path $ProjectRoot ".build-data")
}

$buildDataRoot = [System.IO.Path]::GetFullPath((Resolve-BuildDataRoot -ProjectRoot $projectRoot -ExplicitRoot $BuildDataRoot))
$env:SMART_NAP_BUILD_DATA_ROOT = $buildDataRoot

if (-not $InstallDir) {
    $InstallDir = Join-Path $buildDataRoot "dotnet-sdk"
}

New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
New-Item -ItemType Directory -Path $buildDataRoot -Force | Out-Null
$tempRoot = Join-Path $buildDataRoot "temp"
New-Item -ItemType Directory -Path $tempRoot -Force | Out-Null

$installer = Join-Path $tempRoot ("dotnet-install-" + [guid]::NewGuid().ToString("N") + ".ps1")
try {
    Invoke-WebRequest -UseBasicParsing -Uri "https://dot.net/v1/dotnet-install.ps1" -OutFile $installer
    & $installer -Channel $Channel -Architecture x64 -InstallDir $InstallDir -NoPath
    $installExitCode = $LASTEXITCODE
    if ($installExitCode -is [int] -and $installExitCode -ne 0) {
        throw "dotnet-install failed with exit code $installExitCode"
    }

    $dotnet = Join-Path $InstallDir "dotnet.exe"
    if (-not (Test-Path -LiteralPath $dotnet)) {
        throw "dotnet.exe not found after installation: $dotnet"
    }

    [pscustomobject]@{
        DotNet = $dotnet
        InstallDir = $InstallDir
        BuildDataRoot = $buildDataRoot
        Sdks = (& $dotnet --list-sdks) -join "; "
    }
}
finally {
    Remove-Item -LiteralPath $installer -ErrorAction SilentlyContinue
}
