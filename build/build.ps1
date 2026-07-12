$ErrorActionPreference = "Stop"

$projectRoot = $PSScriptRoot
if ((Split-Path -Leaf $PSScriptRoot) -ieq "build") {
    $projectRoot = Split-Path -Parent $PSScriptRoot
}

$binDir = Join-Path $projectRoot "bin"

New-Item -ItemType Directory -Path $binDir -Force | Out-Null

$candidatePaths = @(
    (Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"),
    (Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319\csc.exe")
)

$csc = $candidatePaths | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $csc) {
    $cmd = Get-Command csc.exe -ErrorAction SilentlyContinue
    if ($cmd) { $csc = $cmd.Source }
}
if (-not $csc) {
    throw "csc.exe not found. Install .NET Framework developer tools or build with Visual Studio/MSBuild."
}

$icon = Join-Path $projectRoot "assets\smart-background-nap.ico"
$manifest = Join-Path $projectRoot "src\app.manifest"
$assemblyInfo = Join-Path $projectRoot "src\AssemblyInfo.cs"

function Get-RuntimeFile {
    param([string]$Name)

    $rootPath = Join-Path $projectRoot $Name
    if (Test-Path -LiteralPath $rootPath) {
        return $rootPath
    }

    $runtimePath = Join-Path (Join-Path $projectRoot "src\runtime") $Name
    if (Test-Path -LiteralPath $runtimePath) {
        return $runtimePath
    }

    return $rootPath
}

function Build-WinFormsExe {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Source,

        [Parameter(Mandatory = $true)]
        [string]$Output,

        [object[]]$Resources = @()
    )

    if (-not (Test-Path -LiteralPath $Source)) {
        throw "Source not found: $Source"
    }

    $args = @(
        "/nologo",
        "/target:winexe",
        "/optimize+",
        "/platform:anycpu",
        "/out:$Output",
        "/reference:System.dll",
        "/reference:System.Drawing.dll",
        "/reference:System.Windows.Forms.dll"
    )

    if (Test-Path -LiteralPath $icon) {
        $args += "/win32icon:$icon"
    }
    if (Test-Path -LiteralPath $manifest) {
        $args += "/win32manifest:$manifest"
    }

    foreach ($resource in $Resources) {
        if (-not (Test-Path -LiteralPath $resource.Path)) {
            throw "Resource not found: $($resource.Path)"
        }
        $args += "/resource:$($resource.Path),$($resource.Name)"
    }

    if (Test-Path -LiteralPath $assemblyInfo) {
        $args += $assemblyInfo
    }
    $args += $Source

    & $csc @args
    if ($LASTEXITCODE -ne 0) {
        throw "csc failed with exit code $LASTEXITCODE"
    }

    [pscustomobject]@{
        ExePath = $Output
        SizeKB = [math]::Round((Get-Item -LiteralPath $Output).Length / 1KB, 1)
        Compiler = $csc
    }
}

$results = @()
$mainOutput = Join-Path $binDir "SmartBackgroundNap.exe"
$mainResources = @(
    [pscustomobject]@{ Path = (Get-RuntimeFile "background-nap.ps1"); Name = "SmartBackgroundNap.Resources.background_nap_ps1" },
    [pscustomobject]@{ Path = (Get-RuntimeFile "browser-nap.ps1"); Name = "SmartBackgroundNap.Resources.browser_nap_ps1" },
    [pscustomobject]@{ Path = (Get-RuntimeFile "manage-background-nap.ps1"); Name = "SmartBackgroundNap.Resources.manage_background_nap_ps1" },
    [pscustomobject]@{ Path = (Get-RuntimeFile "manage-background-nap-tray.ps1"); Name = "SmartBackgroundNap.Resources.manage_background_nap_tray_ps1" },
    [pscustomobject]@{ Path = (Get-RuntimeFile "smart-background-nap-tray.ps1"); Name = "SmartBackgroundNap.Resources.smart_background_nap_tray_ps1" },
    [pscustomobject]@{ Path = (Get-RuntimeFile "game-session.config.json"); Name = "SmartBackgroundNap.Resources.game_session_config_json" },
    [pscustomobject]@{ Path = (Join-Path $projectRoot "README.md"); Name = "SmartBackgroundNap.Resources.readme_md" },
    [pscustomobject]@{ Path = (Join-Path $projectRoot "docs\SECURITY_MODEL.md"); Name = "SmartBackgroundNap.Resources.security_model_md" },
    [pscustomobject]@{ Path = (Join-Path $projectRoot "assets\smart-background-nap.ico"); Name = "SmartBackgroundNap.Resources.icon_ico" }
)
try {
    $results += Build-WinFormsExe -Source (Join-Path $projectRoot "src\SmartBackgroundNap.cs") -Output $mainOutput -Resources $mainResources
} catch {
    if (-not (Test-Path -LiteralPath $mainOutput)) {
        throw
    }
    Write-Warning "Main app build skipped because the existing EXE may be running: $($_.Exception.Message)"
    $results += [pscustomobject]@{
        ExePath = $mainOutput
        SizeKB = [math]::Round((Get-Item -LiteralPath $mainOutput).Length / 1KB, 1)
        Compiler = $csc
        ReusedExisting = $true
    }
}

try {
    $results += Build-WinFormsExe -Source (Join-Path $projectRoot "src\SmartBackgroundNapTray.cs") -Output (Join-Path $binDir "SmartBackgroundNapTray.exe")
} catch {
    Write-Warning "Legacy tray build skipped: $($_.Exception.Message)"
}

$rootLauncher = Join-Path $projectRoot "SmartBackgroundNap.exe"
try {
    Copy-Item -LiteralPath $mainOutput -Destination $rootLauncher -Force
    $results += [pscustomobject]@{
        ExePath = $rootLauncher
        SizeKB = [math]::Round((Get-Item -LiteralPath $rootLauncher).Length / 1KB, 1)
        Compiler = "copied from bin"
    }
} catch {
    Write-Warning "Root launcher copy skipped: $($_.Exception.Message)"
}

$results
