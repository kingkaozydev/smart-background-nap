param(
    [string]$LauncherPath = (Join-Path $PSScriptRoot "..\src\SmartBackgroundNap.cs")
)

$ErrorActionPreference = "Stop"
$launcher = Resolve-Path -LiteralPath $LauncherPath
$project = Resolve-Path -LiteralPath (Join-Path (Split-Path -Parent $launcher) "..\SmartBackgroundNap.csproj")
$runtime = Resolve-Path -LiteralPath (Join-Path (Split-Path -Parent $launcher) "runtime\background-nap.ps1")
$dashboard = Resolve-Path -LiteralPath (Join-Path (Split-Path -Parent $launcher) "launcher\dashboard.html")
$source = Get-Content -LiteralPath $launcher -Raw
$projectSource = Get-Content -LiteralPath $project -Raw
$runtimeSource = Get-Content -LiteralPath $runtime -Raw
$dashboardSource = Get-Content -LiteralPath $dashboard -Raw

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

function Assert-RuntimeContains {
    param(
        [string]$Needle,
        [string]$Message
    )
    if ($runtimeSource -notlike "*$Needle*") {
        throw $Message
    }
}

function Assert-DashboardContains {
    param(
        [string]$Needle,
        [string]$Message
    )
    if (-not $dashboardSource.Contains($Needle)) {
        throw $Message
    }
}

Assert-Contains 'private const string AppVersion = "0.7.2"' "Launcher version was not bumped to 0.7.2."
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
Assert-Contains "CoreProtocolVersion = 1" "Core service protocol v1 contract is missing."
Assert-Contains 'CorePipeName = "SmartNap.Core.v1"' "Core service pipe v1 name is missing."
Assert-Contains "CorePipeMaxMessageBytes" "Core pipe message size limit is missing."
Assert-Contains "CoreContextProviderLegacyBridge" "Core service must identify the current user-session bridge provider."
Assert-Contains "BuildCoreServiceCapabilities" "Core service capabilities must be centralized."
Assert-Contains "getSnapshot" "Core service snapshot capability is missing."
Assert-Contains "getCapabilities" "Core service capabilities handshake is missing."
Assert-Contains "subscribe" "Core service subscribe capability is missing."
Assert-Contains "ping" "Core service ping capability is missing."
Assert-Contains "corePipe.v1" "Core pipe capability marker is missing."
Assert-Contains "sessionAgent.v1" "Session Agent capability marker is missing."
Assert-Contains "publishSessionContext" "Session Agent publish command is missing."
Assert-Contains "getSessionContext" "Session Agent context query is missing."
Assert-Contains "scheduledTaskBridge" "Core service must expose the legacy scheduled-task bridge capability during migration."
Assert-Contains "NamedPipeServerStreamAcl.Create" "Core pipe must be created with an explicit ACL."
Assert-Contains "CreateCorePipeSecurity" "Core pipe security builder is missing."
Assert-Contains "WellKnownSidType.LocalSystemSid" "Core pipe ACL must include LocalSystem."
Assert-Contains "WellKnownSidType.BuiltinAdministratorsSid" "Core pipe ACL must include Administrators."
Assert-Contains "RunCorePipeServerLoop" "Core pipe server loop is missing."
Assert-Contains "HandleCorePipeConnection" "Core pipe connection handler is missing."
Assert-Contains "BuildCorePipeResponse" "Core pipe response envelope is missing."
Assert-Contains "--core-pipe-request" "Core pipe diagnostic CLI command is missing."
Assert-Contains "--session-agent" "Session Agent runtime command is missing."
Assert-Contains "--session-agent-once" "Session Agent one-shot diagnostic command is missing."
Assert-Contains "--install-session-agent" "Session Agent install command is missing."
Assert-Contains "--uninstall-session-agent" "Session Agent uninstall command is missing."
Assert-Contains "--session-agent-status" "Session Agent status command is missing."
Assert-Contains 'SessionAgentTaskName = "SmartBackgroundNapSessionAgent"' "Session Agent task name is missing."
Assert-Contains "SessionAgentLoopMilliseconds" "Session Agent loop cadence is missing."
Assert-Contains "smart-snap-session-agent-latest.json" "Session Agent shared snapshot file is missing."
Assert-Contains "BuildSessionAgentTaskXml" "Session Agent scheduled task XML builder is missing."
Assert-Contains "<LogonTrigger>" "Session Agent task must start on user logon."
Assert-Contains "<RunLevel>LeastPrivilege</RunLevel>" "Session Agent must run least-privileged in the user session."
Assert-Contains "InstallSessionAgent(false)" "Full setup must install the Session Agent task."
Assert-Contains "UninstallSessionAgent()" "Full uninstall must remove the Session Agent task."
Assert-Contains "&& IsTaskInstalled(SessionAgentTaskName)" "Primary task readiness must include the Session Agent task."
Assert-Contains "BuildSessionAgentObservation" "Session Agent observation builder is missing."
Assert-Contains "PublishSessionAgentObservationToCore" "Session Agent must publish observations to the Core pipe."
Assert-Contains "IsSessionForegroundProtectedByRuntime" "Session Agent must reconcile foreground protection with runtime protection state."
Assert-Contains "background-nap-protect-latest.json" "Session Agent must read runtime temporary protection state."
Assert-Contains "NetworkUdpGuardGamePid" "Session Agent foreground protection must account for the active Zero Ping game."
Assert-Contains "BuildSessionContextPayload" "Core snapshot must expose Session Agent context."
Assert-Contains "BuildSessionForegroundPayload" "Core snapshot must expose Session Agent foreground data."
Assert-Contains "SessionAgentTaskInstalled" "Core diagnostics must expose Session Agent task installation state."
Assert-Contains "smart-snap-core-service-latest.json" "Core service shared state file is missing."
Assert-Contains "WriteCoreServiceState" "Core service state writer is missing."
Assert-Contains "CoreServiceSnapshot" "Launcher core service snapshot model is missing."
Assert-Contains "LoadCoreServiceSnapshot" "Launcher must read the core service snapshot."
Assert-Contains "CoreServiceHealth" "Launcher must expose core service health."
Assert-Contains "CoreServiceNeedsAttention" "Launcher must expose core service attention state."
Assert-Contains "action=core-service" "Core service events must reach the launcher activity feed."
Assert-Contains "schtasks.exe" "Core service must broker through the scheduled user-session engine task."
Assert-Contains "GetInstallOwnerLocalAppData" "Core service must resolve the installed user's data root when running as LocalSystem."
Assert-Contains "WasAdminSetupCompletedForCurrentVersion() && ArePrimaryScheduledTasksInstalled() && IsCoreServiceInstalled()" "Admin setup readiness must include the core service."
Assert-Contains "|| !IsCoreServiceRunning()" "Install repair must restart an installed but stopped core service."
Assert-Contains "managed-copy-core-service-restart" "Managed EXE updates must restart the core service after replacing the installed binary."
Assert-Contains "WaitForCoreServiceRunningState(false" "Managed EXE updates must wait for the core service to stop before copying over the installed binary."
Assert-Contains "EnsureInstalledRuntimeReady(false)" "Full setup must verify services after install/update."
Assert-Contains "InstallVerify" "Full setup must write a service verification state."
Assert-Contains "Smart Nap services are installed and running." "Full setup must finish with running-service confirmation."
Assert-Contains "&& IsCoreServiceRunning()" "Admin setup readiness must require the core service to be running."
Assert-Contains "FilesAlreadyMatch(current, target)" "Managed EXE updates must avoid unnecessary service restarts when the installed binary is already current."
Assert-Contains "RestartAutomaticEngineTaskForInstallVerify" "Full setup must recycle the automatic engine task after runtime updates."
Assert-Contains "WaitForFileWriteAfter(scorePath" "Full setup must wait for fresh engine telemetry after install/update."
Assert-Contains '/End /TN " + Quote(AutoTaskName)' "Full setup must stop an already-running engine task so old runtime code is not kept alive."
Assert-RuntimeContains "Find-ExecutableInAliasedGameFolders" "Runtime game path resolution must prioritize known game folders inside game libraries."
Assert-RuntimeContains "resolvedGameExecutablePathCache" "Runtime game path resolution must cache discovered executable paths."
Assert-RuntimeContains "processPathFallbackMissAtByPid" "Runtime process path fallback must not cache missing paths forever."
Assert-RuntimeContains "game-paths.learned.json" "Runtime game path resolution must persist learned executable paths."
Assert-RuntimeContains "Resolve-GameExecutablePathFromSteamManifests" "Runtime game path resolution must read Steam app manifests."
Assert-RuntimeContains "Resolve-GameExecutablePathFromEpicManifests" "Runtime game path resolution must read Epic launcher manifests."
Assert-RuntimeContains "Confirm-ResolvedGameExecutablePath" "Runtime game path resolution must cache and persist successful discoveries through one path."
Assert-RuntimeContains "Test-ResolvedGameExecutablePathPersistable" "Runtime learned game path cache must reject non-game helper processes."
Assert-RuntimeContains "Resolve-InstalledGameExecutablePathByName" "Runtime game path resolution must fall back to installed game libraries by executable name."
Assert-RuntimeContains "Resolve-GameExecutablePathForActiveContext" "Runtime active game contexts must repair missing executable paths before module handoff."
Assert-RuntimeContains "InstalledGameScan" "Runtime learned game path cache must persist installed-library discoveries."
Assert-RuntimeContains "resolved-game-path" "Zero Ping must mark contexts repaired by executable path discovery."
Assert-DashboardContains "v0.7.2 games optimization library" "Games optimization library CSS layer is missing."
Assert-DashboardContains "v0.7.2 games library controller" "Games optimization library controller is missing."
Assert-DashboardContains "gamesToolbar" "Games library search/filter toolbar is missing."
Assert-DashboardContains "setGameLibraryView" "Games grid/list view persistence is missing."
Assert-DashboardContains "v0.7.2 games library density polish" "Games library density polish layer is missing."
Assert-DashboardContains 'body.view-games .gamesGrid[data-view="grid"]{grid-template-columns:repeat(4,minmax(0,1fr))' "Games grid must keep stable columns so one result does not stretch into a banner."
Assert-DashboardContains 'body.view-games .gamesGrid[data-view="grid"] .gamePoster{aspect-ratio:3/4' "Games grid covers must keep a bounded library-card proportion."
Assert-DashboardContains 'body.view-games .gamesGrid[data-view="list"] .gameBody{display:grid!important;grid-template-columns:minmax(0,1fr) minmax(178px,220px)' "Games list rows must separate content from compact actions."
Assert-DashboardContains "gameCardText" "Games list rows must wrap game details in a dedicated content column."
Assert-DashboardContains "gamesContinueIcon" "Games continue banner must render as a compact icon-text-action row."
Assert-DashboardContains "onlyFocusedResult" "Games continue banner must hide when the filter already focuses the single pending result."
Assert-DashboardContains "gameLibraryFilterLabel" "Games filters must render localized labels with counts."
Assert-DashboardContains "renderGameEmptyState" "Games filters must show a contextual empty state."
Assert-DashboardContains "gamePresetTabs" "Guided game preset tabs are missing."
Assert-DashboardContains "gamePresetTechnicalToggle" "Game preset technical details toggle is missing."
Assert-DashboardContains "previousScroll" "Game preset technical details toggle must preserve the review scroll position."
Assert-DashboardContains "gamePresetApplyButton" "Game preset primary CTA must have a stable id for dynamic state."
Assert-DashboardContains "gamePresetSelectionChanged" "Game preset CTA must compare current selection against the applied state."
Assert-DashboardContains "updateGamePresetActionState" "Game preset primary CTA must be driven by real operation state."
Assert-DashboardContains "formatGamePresetHardwareSummary" "Game preset hardware summary must be structured from real PC state."
Assert-DashboardContains "gameRestoreBtnNeutral" "Game preset restore action must use neutral tertiary styling."
Assert-DashboardContains "Nenhum backup válido foi encontrado" "Game preset restore must handle missing backups without pretending restore is available."
Assert-DashboardContains "Informação indisponível" "Game preset technical details must use honest unavailable-state copy instead of fake values."
Assert-Contains "PresetApplied" "Game preset applied state is missing from the WebView model."
Assert-RuntimeContains "PathPending" "Zero Ping QoS must expose pending game path resolution without treating local protection as failed."
Assert-RuntimeContains 'if ($null -eq $Hints -or' "Game path hint collection must accept the first root instead of treating an empty ArrayList as missing."

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

$pipeResponseIndex = $source.IndexOf("private static IDictionary<string, object> BuildCorePipeResponse", [StringComparison]::Ordinal)
if ($pipeResponseIndex -lt 0) {
    throw "Core pipe response builder is missing."
}
$pipeResponseEndIndex = $source.IndexOf("private static bool IsAcceptedCorePipeResponse", $pipeResponseIndex, [StringComparison]::Ordinal)
if ($pipeResponseEndIndex -lt 0) {
    throw "Could not isolate Core pipe response builder."
}
$pipeResponseBlock = $source.Substring($pipeResponseIndex, $pipeResponseEndIndex - $pipeResponseIndex)
foreach ($forbidden in @("optimizeNow", "cancelOperation", "pause", "resume", "restore", "setMode", "setConfiguration", "upsertRule", "deleteRule", "enableModule", "disableModule", "RunApplyNow", "RunRestore")) {
    if ($pipeResponseBlock -like "*$forbidden*") {
        throw "Core pipe v1 must not expose critical operation commands until operation queue/journal are implemented: $forbidden"
    }
}

$sessionAgentIndex = $source.IndexOf("private static RunResult RunSessionAgentHost", [StringComparison]::Ordinal)
if ($sessionAgentIndex -lt 0) {
    throw "Session Agent host function is missing."
}
$sessionAgentEndIndex = $source.IndexOf("private static CoreServiceSnapshot LoadCoreServiceSnapshot", $sessionAgentIndex, [StringComparison]::Ordinal)
if ($sessionAgentEndIndex -lt 0) {
    throw "Could not isolate Session Agent host function."
}
$sessionAgentBlock = $source.Substring($sessionAgentIndex, $sessionAgentEndIndex - $sessionAgentIndex)
foreach ($forbidden in @("RunApplyNow", "RunRestore", "RunPowerShellScript(backgroundScriptPath", "TrySetMemoryPriority", "TrySetIoPriority", "TryClearPowerThrottling", "ActivatePowerPlan")) {
    if ($sessionAgentBlock -like "*$forbidden*") {
        throw "Session Agent must observe only and must not apply privileged changes: $forbidden"
    }
}

Assert-NotContains "Service installed: no." "Safety report still claims there is no Windows service."

if ($projectSource -notlike "*System.IO.Pipes.AccessControl*") {
    throw "Core pipe ACL package reference is missing."
}

"launcher regression guard ok"
