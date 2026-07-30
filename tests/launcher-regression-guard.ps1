param(
    [string]$LauncherPath = (Join-Path $PSScriptRoot "..\src\SmartBackgroundNap.cs")
)

$ErrorActionPreference = "Stop"
$launcher = Resolve-Path -LiteralPath $LauncherPath
$source = Get-Content -LiteralPath $launcher -Raw

function Assert-Contains {
    param(
        [string]$Needle,
        [string]$Message
    )
    if ($source -notlike "*$Needle*") {
        throw $Message
    }
}

function Assert-NotContains {
    param(
        [string]$Needle,
        [string]$Message
    )
    if ($source -like "*$Needle*") {
        throw $Message
    }
}

Assert-Contains 'private const string AppVersion = "0.7.0"' "Launcher version was not bumped to 0.7.0."
Assert-Contains "SmartSNAPCoreService" "Core service name is missing."
Assert-Contains "ServiceBase.Run" "Core service host is missing."
Assert-Contains "ServiceController.GetServices" "Core service status must not depend on localized sc.exe text."
Assert-Contains "SmartSnapCoreService : ServiceBase" "Windows Service class is missing."
Assert-Contains "--core-service" "Core service runtime command is missing."
Assert-Contains "--install-core-service" "Core service install command is missing."
Assert-Contains "--uninstall-core-service" "Core service uninstall command is missing."
Assert-Contains "--core-service-status" "Core service status command is missing."
Assert-Contains "RunCoreServicePass" "Core service watchdog pass is missing."
Assert-Contains "CoreServiceStalePassSeconds" "Core service stale threshold is missing."
Assert-Contains "smart-snap-core-service-latest.json" "Core service shared state file is missing."
Assert-Contains "WriteCoreServiceState" "Core service state writer is missing."
Assert-Contains "schtasks.exe" "Core service must broker through the scheduled user-session engine task."
Assert-Contains "GetInstallOwnerLocalAppData" "Core service must resolve the installed user's data root when running as LocalSystem."
Assert-Contains "WasAdminSetupCompletedForCurrentVersion() && ArePrimaryScheduledTasksInstalled() && IsCoreServiceInstalled()" "Admin setup readiness must include the core service."
Assert-Contains "|| !IsCoreServiceRunning()" "Install repair must restart an installed but stopped core service."
Assert-Contains "managed-copy-core-service-restart" "Managed EXE updates must restart the core service after replacing the installed binary."
Assert-Contains "WaitForCoreServiceRunningState(false" "Managed EXE updates must wait for the core service to stop before copying over the installed binary."

$servicePassIndex = $source.IndexOf("private static RunResult RunCoreServicePass", [StringComparison]::Ordinal)
if ($servicePassIndex -lt 0) {
    throw "Core service watchdog function is missing."
}
$nextMethodIndex = $source.IndexOf("private static RunResult InstallAutomatic", $servicePassIndex, [StringComparison]::Ordinal)
if ($nextMethodIndex -lt 0) {
    throw "Could not isolate Core Service watchdog function."
}
$servicePassBlock = $source.Substring($servicePassIndex, $nextMethodIndex - $servicePassIndex)
foreach ($forbidden in @("RunApplyNow", "RunPowerShellScript(backgroundScriptPath", "GetForegroundWindow", "FindWindow", "OpenProcess")) {
    if ($servicePassBlock -like "*$forbidden*") {
        throw "Core service watchdog must not apply foreground/session optimizations directly: $forbidden"
    }
}

Assert-NotContains "Service installed: no." "Safety report still claims there is no Windows service."

"launcher regression guard ok"
