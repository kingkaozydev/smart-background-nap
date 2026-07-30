param(
    [string]$DashboardPath = (Join-Path $PSScriptRoot "..\src\launcher\dashboard.html"),
    [string]$NodePath = "C:\Users\eduar\.cache\codex-runtimes\codex-primary-runtime\dependencies\node\bin\node.exe"
)

$ErrorActionPreference = "Stop"
$node = Resolve-Path -LiteralPath $NodePath
$dashboard = Resolve-Path -LiteralPath $DashboardPath
$script = Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "launcher-responsive-visual.mjs")

& $node $script $dashboard
if ($LASTEXITCODE -ne 0) {
    throw "launcher responsive visual failed with exit code $LASTEXITCODE"
}
