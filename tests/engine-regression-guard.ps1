param(
    [string]$RuntimePath = (Join-Path $PSScriptRoot "..\src\runtime\background-nap.ps1")
)

$ErrorActionPreference = "Stop"
$runtime = Resolve-Path -LiteralPath $RuntimePath
$source = Get-Content -LiteralPath $runtime -Raw

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

function Get-FunctionBlock {
    param([string]$Name)
    $pattern = "function\s+$([regex]::Escape($Name))\s*\{"
    $match = [regex]::Match($source, $pattern)
    if (-not $match.Success) {
        throw "Missing function: $Name"
    }

    $start = $match.Index
    $braceStart = $source.IndexOf("{", $match.Index)
    if ($braceStart -lt 0) {
        throw "Missing function body: $Name"
    }

    $depth = 0
    for ($i = $braceStart; $i -lt $source.Length; $i++) {
        $char = $source[$i]
        if ($char -eq "{") {
            $depth++
        } elseif ($char -eq "}") {
            $depth--
            if ($depth -eq 0) {
                return $source.Substring($start, $i - $start + 1)
            }
        }
    }
    throw "Unclosed function body: $Name"
}

[scriptblock]::Create($source) > $null

Assert-Contains "ForegroundTreeProtected" "Foreground tree protection is missing."
Assert-Contains "NewProcessStabilizing" "New process stabilization is missing."
Assert-Contains "operation-budget-softened" "Per-pass Deep budget guard is missing."
Assert-Contains "BudgetSkipped" "Per-pass operation budget skip is missing."
Assert-Contains "TreeRestored" "Foreground tree restore result is missing."
Assert-Contains "Get-RelatedProcessIdSet" "Related process tree traversal is missing."
Assert-Contains "Test-RealGameProcessCandidate" "Real game candidate classifier is missing."
Assert-Contains "Smart(Background)?Nap" "Smart Nap self-process exclusion is missing."
Assert-Contains '$knownGameNames = New-Object' "Known game name set is missing."
Assert-Contains "bf6" "BF6 executable alias is missing from game detection."
Assert-Contains "game-paths.user.json" "Saved game path fallback is missing."
Assert-Contains "Resolve-UserGamePathForProcess" "Protected game path resolver is missing."
Assert-Contains "Resolve-GameExecutablePath" "Generic game executable resolver is missing."
Assert-Contains "Test-GenericGameProcessCandidate" "Generic game process classifier is missing."
Assert-Contains "GenericGameDetection" "Generic game detection flag is missing."
Assert-Contains "Get-CommonGameSearchRoots" "Common game install root search is missing."
Assert-Contains "Get-ProcessGameRootHints" "Related process game root hints are missing."
Assert-Contains "Win32_Process" "Process executable path fallback is missing."
Assert-Contains "VramActionMode" "VRAM action mode config is missing."
Assert-Contains "EALocalHostSvc" "EA launcher local host service must be classified as launcher, not game."
Assert-Contains '"\Electronic Arts\"' "Broad Electronic Arts game path fragment filter is missing."
Assert-Contains "Get-StreamingContext" "Streaming context telemetry is missing."
Assert-Contains "Get-GpuOptimizationContext" "GPU optimization context is missing."
Assert-Contains "Test-StreamingSafeLaneActive" "Streaming safe lane switch is missing."
Assert-Contains "Write-EngineDiagnostic" "Structured engine diagnostics are missing."
Assert-Contains "Rotate-LogFile" "Diagnostic log rotation is missing."
Assert-NotContains "launcher-helper-deep" "Launcher helpers must not receive Deep nap policy."

$cpuAssistBlock = Get-FunctionBlock "Get-CpuBoundAssistContext"
foreach ($needle in @("UdpGuard.GamePid", "Processes", "CPU-bound assist ligado ao jogo protegido")) {
    if ($cpuAssistBlock -notlike "*$needle*") {
        throw "CPU-bound assist context is missing $needle."
    }
}

$cpuBoundBlock = Get-FunctionBlock "Test-CpuBoundBackgroundCandidate"
foreach ($role in @("LauncherHelper", "Professional", "Development")) {
    if ($cpuBoundBlock -notlike "*$role*") {
        throw "CPU-bound background guard does not exclude $role."
    }
}

$affinityBlock = Get-FunctionBlock "Test-StreamerAffinityCandidate"
if ($affinityBlock -notlike '*$Row.Role -eq "StreamHelper"*') {
    throw "Streamer affinity should target StreamHelper explicitly."
}
if ($affinityBlock -like "*LauncherHelper*Get-StreamerAffinityMask*") {
    throw "LauncherHelper must not be grouped into streamer affinity limiting."
}

$restoreBlock = Get-FunctionBlock "Invoke-ForegroundRestore"
foreach ($needle in @("Find-StateProcessItem", "Restore-ProcessRuntimeState", "Get-RelatedProcessIdSet", "ForegroundTreeWake")) {
    if ($restoreBlock -notlike "*$needle*") {
        throw "Foreground restore is missing $needle."
    }
}

$udpGuardBlock = Get-FunctionBlock "Get-UdpGuardContext"
foreach ($needle in @("anchorLooksLikeGame", "Test-RealGameProcessCandidate", "Get-RelatedUdpEndpointSummary", "Resolve-GameExecutablePath", "QosStatus")) {
    if ($udpGuardBlock -notlike "*$needle*") {
        throw "Zero Ping context is missing $needle."
    }
}

$intentBlock = Get-FunctionBlock "Get-IntentContext"
foreach ($needle in @("fgLooksLikeGame", "Test-RealGameProcessCandidate", "Resolve-GameExecutablePath", "udp-game-background")) {
    if ($intentBlock -notlike "*$needle*") {
        throw "Intent context is missing $needle."
    }
}

$assistiveBlock = Get-FunctionBlock "Get-AssistiveUdpEndpointSummaryForGame"
if ($assistiveBlock -like "*'Communication'*") {
    throw "Assistive UDP summary must not use voice/media apps as game evidence."
}
if ($assistiveBlock -like "*Test-NeverGameProcess*") {
    throw "Assistive UDP summary must not reject launcher/helper evidence through game-only exclusion."
}

$rowsBlock = Get-FunctionBlock "Get-BackgroundProcessRows"
foreach ($needle in @("currentStreamingContext", "currentGpuOptimization", "Get-StreamingContext", "Get-GpuOptimizationContext", "StreamingProfile", "GpuOptimizationStatus")) {
    if ($rowsBlock -notlike "*$needle*") {
        throw "Background process rows are missing $needle."
    }
}

$weightBlock = Get-FunctionBlock "Get-CandidateWeight"
foreach ($needle in @("Test-StreamingSafeLaneActive", "GpuOptimizationActive", "LauncherHelper")) {
    if ($weightBlock -notlike "*$needle*") {
        throw "Candidate weighting is missing $needle."
    }
}

$policyBlock = Get-FunctionBlock "Get-NapPolicy"
foreach ($needle in @("Test-StreamingSafeLaneActive", "gpu-workload-helper-containment", "vram-action-helper-containment", "gpu-action")) {
    if ($policyBlock -notlike "*$needle*") {
        throw "Nap policy is missing $needle."
    }
}

$healthBlock = Get-FunctionBlock "Write-EngineHealthState"
foreach ($needle in @("StreamGuard", "GpuOptimization", "LastPassValid", "Write-EngineDiagnostic")) {
    if ($healthBlock -notlike "*$needle*") {
        throw "Engine health state is missing $needle."
    }
}

$snapshotBlock = Get-FunctionBlock "New-StateSnapshot"
foreach ($needle in @("rollbackStatePath", "UdpGameProtected", "GpuHelperPressure", "CpuBoundAssist", "ForegroundTreeProtected")) {
    if ($snapshotBlock -notlike "*$needle*") {
        throw "Rollback snapshot is missing $needle."
    }
}

$scoreBlock = Get-FunctionBlock "Write-NapScore"
foreach ($needle in @("StreamGuardProfile", "GpuOptimizationStatus", "GpuOptimizationReason", "ShaderBoostState", "ShaderBoostReadiness", "ShaderBoostCompilationState")) {
    if ($scoreBlock -notlike "*$needle*") {
        throw "Nap score output is missing $needle."
    }
}

$shaderCoordinatorBlock = Get-FunctionBlock "Get-ShaderBoostCoordinator"
foreach ($needle in @("ShaderBoostCoordinator", "ShaderCapabilityDetector", "ShaderCacheInventory", "ShaderCacheHealthAnalyzer", "ShaderCacheBudgetManager", "ShaderCacheGuardian", "SmartShaderWarmup", "ObserveOnly", "no automatic repair", "CacheScanMode", "FrameStabilityGuard")) {
    if ($shaderCoordinatorBlock -notlike "*$needle*") {
        throw "ShaderBoost coordinator is missing $needle."
    }
}
foreach ($forbidden in @("Remove-Item", "Clear-Item", "Move-Item", "rmdir", "del ")) {
    if ($shaderCoordinatorBlock -like "*$forbidden*") {
        throw "ShaderBoost coordinator must not behave like a shader cache cleaner: $forbidden"
    }
}

$shaderAnchorBlock = Get-FunctionBlock "Get-ShaderBoostGameAnchor"
foreach ($needle in @("UdpGuard.GamePid", "Get-ProcessPathText", "Resolve-GameExecutablePath", 'Source = "ZeroPing"')) {
    if ($shaderAnchorBlock -notlike "*$needle*") {
        throw "ShaderBoost game anchor must reuse Zero Ping game detection without requiring a readable game path: $needle"
    }
}
if ($shaderAnchorBlock -like "*UdpGuard.GamePath))*") {
    throw "ShaderBoost must not require Zero Ping GamePath before accepting the detected game."
}

$shaderInventoryBlock = Get-FunctionBlock "Get-ShaderCacheInventory"
foreach ($needle in @("WindowsManaged", "NvidiaShaderAdapter", "AmdShaderAdapter", "IntelShaderAdapter", "GameManaged", "GameplayActive", "CachedGameplay", "LightGameplay", "shaderBoostInventoryStatePath")) {
    if ($shaderInventoryBlock -notlike "*$needle*") {
        throw "Shader cache inventory is missing $needle."
    }
}
foreach ($forbidden in @("Remove-Item", "Clear-Item", "Move-Item", "rmdir", "del ")) {
    if ($shaderInventoryBlock -like "*$forbidden*") {
        throw "Shader cache inventory must not delete or move caches: $forbidden"
    }
}

$guardBlock = Get-FunctionBlock "Get-GuardDecision"
foreach ($needle in @("ShaderCompilationGuard", "ShaderCompiler")) {
    if ($guardBlock -notlike "*$needle*") {
        throw "Shader compilation protection is missing $needle."
    }
}

$frameStabilityAffinityBlock = Get-FunctionBlock "Test-FrameStabilityAffinityCandidate"
foreach ($needle in @("FrameStabilityGuard", '"Browser"', "UdpGameProtected", "ForegroundTreeProtected", "frameStabilityMinBurstCount")) {
    if ($frameStabilityAffinityBlock -notlike "*$needle*") {
        throw "Frame stability affinity guard is missing $needle."
    }
}

Assert-Contains "OKFrameStability" "Frame stability affinity application result is missing."
Assert-Contains "Test-VramActionCandidate" "VRAM action candidate guard is missing."
Assert-Contains "OKVramAction" "VRAM action affinity application result is missing."

"engine regression guard ok"
