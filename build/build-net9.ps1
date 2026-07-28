param(
    [string]$Configuration = "Release",
    [string]$DotNetPath,
    [string]$BuildDataRoot,
    [switch]$NoRootCopy
)

$ErrorActionPreference = "Stop"

$projectRoot = $PSScriptRoot
if ((Split-Path -Leaf $PSScriptRoot) -ieq "build") {
    $projectRoot = Split-Path -Parent $PSScriptRoot
}

$projectFile = Join-Path $projectRoot "SmartBackgroundNap.csproj"
if (-not (Test-Path -LiteralPath $projectFile)) {
    throw "Project file not found: $projectFile"
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

function Add-TrailingDirectorySeparator {
    param([string]$Path)

    if ($Path.EndsWith([System.IO.Path]::DirectorySeparatorChar) -or
        $Path.EndsWith([System.IO.Path]::AltDirectorySeparatorChar)) {
        return $Path
    }

    return $Path + [System.IO.Path]::DirectorySeparatorChar
}

$buildDataRoot = [System.IO.Path]::GetFullPath((Resolve-BuildDataRoot -ProjectRoot $projectRoot -ExplicitRoot $BuildDataRoot))
$env:SMART_NAP_BUILD_DATA_ROOT = $buildDataRoot
$env:DOTNET_CLI_HOME = Join-Path $buildDataRoot "dotnet-home"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:DOTNET_NOLOGO = "1"
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = "false"
$env:NUGET_PACKAGES = Join-Path $buildDataRoot "nuget\packages"
$env:APPDATA = Join-Path $buildDataRoot "appdata\Roaming"
$env:LOCALAPPDATA = Join-Path $buildDataRoot "appdata\Local"
$msbuildRoot = Join-Path $buildDataRoot "msbuild"
$baseIntermediateOutputPath = Add-TrailingDirectorySeparator (Join-Path $msbuildRoot "obj")
$baseOutputPath = Add-TrailingDirectorySeparator (Join-Path $msbuildRoot "bin")
$projectExtensionsPath = Add-TrailingDirectorySeparator (Join-Path $msbuildRoot "project-extensions")
New-Item -ItemType Directory -Path $buildDataRoot -Force | Out-Null
New-Item -ItemType Directory -Path $env:DOTNET_CLI_HOME -Force | Out-Null
New-Item -ItemType Directory -Path $env:NUGET_PACKAGES -Force | Out-Null
New-Item -ItemType Directory -Path $env:APPDATA -Force | Out-Null
New-Item -ItemType Directory -Path $env:LOCALAPPDATA -Force | Out-Null
New-Item -ItemType Directory -Path $baseIntermediateOutputPath -Force | Out-Null
New-Item -ItemType Directory -Path $baseOutputPath -Force | Out-Null
New-Item -ItemType Directory -Path $projectExtensionsPath -Force | Out-Null

function Find-DotNet {
    $candidates = @()
    if ($DotNetPath) { $candidates += $DotNetPath }
    if ($env:SMART_NAP_DOTNET) { $candidates += $env:SMART_NAP_DOTNET }
    $candidates += (Join-Path $buildDataRoot "dotnet-sdk\dotnet.exe")
    $candidates += (Join-Path $projectRoot ".dotnet-sdk\dotnet.exe")

    $cmd = Get-Command dotnet.exe -ErrorAction SilentlyContinue
    if ($cmd) { $candidates += $cmd.Source }

    foreach ($candidate in $candidates) {
        if (-not $candidate) { continue }
        if (-not (Test-Path -LiteralPath $candidate)) { continue }

        $sdks = & $candidate --list-sdks 2>$null
        if ($LASTEXITCODE -ne 0) { continue }
        if ($sdks | Select-String -Pattern "^9\.") {
            return $candidate
        }
    }

    return $null
}

$dotnet = Find-DotNet
if (-not $dotnet) {
    throw "A .NET 9 SDK was not found. Install the SDK or run build\install-dotnet9-sdk.ps1 first."
}

$publishDir = Join-Path $buildDataRoot "publish\net9-single"
New-Item -ItemType Directory -Path $publishDir -Force | Out-Null

& $dotnet publish $projectFile `
    --nologo `
    --configuration $Configuration `
    --runtime win-x64 `
    --self-contained false `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=false `
    -p:DebugType=none `
    -p:DebugSymbols=false `
    "-p:BaseIntermediateOutputPath=$baseIntermediateOutputPath" `
    "-p:BaseOutputPath=$baseOutputPath" `
    "-p:MSBuildProjectExtensionsPath=$projectExtensionsPath" `
    "-p:RestorePackagesPath=$env:NUGET_PACKAGES" `
    -o $publishDir

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

$publishedExe = Join-Path $publishDir "SmartBackgroundNap.exe"
if (-not (Test-Path -LiteralPath $publishedExe)) {
    throw "Published EXE not found: $publishedExe"
}

$rootExe = Join-Path $projectRoot "SmartBackgroundNap.exe"
if (-not $NoRootCopy) {
    Copy-Item -LiteralPath $publishedExe -Destination $rootExe -Force
}

[pscustomobject]@{
    Target = "net9.0-windows"
    DotNet = $dotnet
    BuildDataRoot = $buildDataRoot
    PublishedExe = $publishedExe
    RootExe = if ($NoRootCopy) { "" } else { $rootExe }
    SizeKB = [math]::Round((Get-Item -LiteralPath $publishedExe).Length / 1KB, 1)
    SHA256 = (Get-FileHash -LiteralPath $publishedExe -Algorithm SHA256).Hash
}
