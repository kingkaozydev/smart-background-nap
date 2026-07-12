$ErrorActionPreference = "Stop"

$src = Join-Path $PSScriptRoot "src\SmartBackgroundNapTray.cs"
$binDir = Join-Path $PSScriptRoot "bin"
$out = Join-Path $binDir "SmartBackgroundNapTray.exe"

if (-not (Test-Path -LiteralPath $src)) {
    throw "Source not found: $src"
}

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
$args = @(
    "/nologo",
    "/target:winexe",
    "/optimize+",
    "/platform:anycpu",
    "/out:$out",
    "/reference:System.dll",
    "/reference:System.Drawing.dll",
    "/reference:System.Windows.Forms.dll"
)

if (Test-Path -LiteralPath $icon) {
    $args += "/win32icon:$icon"
}

$args += $src

& $csc @args
if ($LASTEXITCODE -ne 0) {
    throw "csc failed with exit code $LASTEXITCODE"
}

[pscustomobject]@{
    ExePath = $out
    SizeKB = [math]::Round((Get-Item -LiteralPath $out).Length / 1KB, 1)
    Compiler = $csc
}
