$ErrorActionPreference = "Stop"

$binDir = Join-Path $PSScriptRoot "bin"

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

$icon = Join-Path $PSScriptRoot "assets\smart-background-nap.ico"

function Build-WinFormsExe {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Source,

        [Parameter(Mandatory = $true)]
        [string]$Output
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
try {
    $results += Build-WinFormsExe -Source (Join-Path $PSScriptRoot "src\SmartBackgroundNap.cs") -Output $mainOutput
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
    $results += Build-WinFormsExe -Source (Join-Path $PSScriptRoot "src\SmartBackgroundNapTray.cs") -Output (Join-Path $binDir "SmartBackgroundNapTray.exe")
} catch {
    Write-Warning "Legacy tray build skipped: $($_.Exception.Message)"
}

$rootLauncher = Join-Path $PSScriptRoot "SmartBackgroundNap.exe"
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
