param(
    [string]$InstallDir,
    [string]$Channel = "9.0"
)

$ErrorActionPreference = "Stop"

$projectRoot = $PSScriptRoot
if ((Split-Path -Leaf $PSScriptRoot) -ieq "build") {
    $projectRoot = Split-Path -Parent $PSScriptRoot
}

if (-not $InstallDir) {
    $InstallDir = Join-Path $projectRoot ".dotnet-sdk"
}

New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null

$installer = Join-Path $env:TEMP ("dotnet-install-" + [guid]::NewGuid().ToString("N") + ".ps1")
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
        Sdks = (& $dotnet --list-sdks) -join "; "
    }
}
finally {
    Remove-Item -LiteralPath $installer -ErrorAction SilentlyContinue
}
