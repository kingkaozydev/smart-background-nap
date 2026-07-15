param(
    [ValidateSet("Status", "Apply", "Restore", "Watch", "ForegroundRestore")]
    [string]$Action = "Status",

    [string]$ConfigPath = (Join-Path $PSScriptRoot "game-session.config.json"),

    [string]$StatePath,

    [int]$TargetPid,

    [int]$WatchMinutes = 90,

    [int]$IntervalSeconds = 30,

    [switch]$IncludeForeground,

    [switch]$NoTrimWorkingSet,

    [ValidateSet("Timestamp", "Latest", "None")]
    [string]$StateMode = "Timestamp",

    [string]$LogPath,

    [switch]$Quiet
)

$ErrorActionPreference = "Continue"

if (-not (Test-Path -LiteralPath $ConfigPath)) {
    throw "Config not found: $ConfigPath"
}

$config = Get-Content -LiteralPath $ConfigPath -Raw | ConvertFrom-Json
$nap = $config.BackgroundNap
if (-not $nap -or -not $nap.Enabled) {
    throw "BackgroundNap is disabled or missing in config."
}
$smart = $config.SmartMode

if (-not $LogPath) {
    $workspace = $PSScriptRoot
    $outDir = Join-Path $workspace "outputs"
    $LogPath = Join-Path $outDir "background-nap-auto.log"
} else {
    $outDir = Split-Path -Parent $LogPath
    if (-not $outDir) {
        $outDir = Join-Path $PSScriptRoot "outputs"
        $LogPath = Join-Path $outDir (Split-Path -Leaf $LogPath)
    }
}
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
$protectStatePath = Join-Path $outDir "background-nap-protect-latest.json"
$burstStatePath = Join-Path $outDir "background-nap-burst-latest.json"
$trimStatePath = Join-Path $outDir "background-nap-trim-latest.json"
$scorePath = Join-Path $outDir "background-nap-score-latest.json"
$learningStatePath = Join-Path $outDir "background-nap-learning-latest.json"
$intentStatePath = Join-Path $outDir "background-nap-intent-latest.json"
$foregroundSwitchStatePath = Join-Path $outDir "background-nap-foreground-switch-latest.json"
$gameProfileStatePath = Join-Path $outDir "background-nap-game-profiles-latest.json"
$behaviorStatePath = Join-Path $outDir "background-nap-behavior-latest.json"
$appPolicyStatePath = Join-Path $outDir "background-nap-app-policies.json"
$radarStatePath = Join-Path $outDir "background-nap-radar-latest.json"

$priorityClass = [string]$nap.PriorityClass
$targetPriorityClass = [System.Enum]::Parse([System.Diagnostics.ProcessPriorityClass], $priorityClass, $true)
$useEcoQos = [bool]$nap.EnableEcoQoS
$ignoreTimerResolution = [bool]$nap.IgnoreTimerResolution
$trimWorkingSet = [bool]$nap.TrimWorkingSetOnce -and -not $NoTrimWorkingSet
$trimMinimumMB = [double]$nap.TrimMinimumWorkingSetMB
$skipHighCpu = [bool]$nap.SkipHighCpuPercent
$highCpuThreshold = [double]$nap.HighCpuPercentThreshold
$cpuSampleMilliseconds = [int]$nap.CpuSampleMilliseconds
if ($cpuSampleMilliseconds -lt 250) { $cpuSampleMilliseconds = 250 }
$skipForegroundName = [bool]$nap.SkipForegroundProcessName -and -not $IncludeForeground
$skipWindowsPath = [bool]$nap.SkipWindowsPath
$skipSessionZero = [bool]$nap.SkipSessionZero

$smartForegroundWake = $true
$smartAutoProtect = $true
$smartFullscreenAware = $true
$smartBurstWatcher = $true
$smartNapScore = $true
$autoProtectForegroundMinutes = 2
$autoProtectHighCpuMinutes = 8
$fullscreenTrimMinimumMB = 40.0
$fullscreenHighCpuThreshold = 10.0
$burstCpuThreshold = 1.5
$burstWindowMinutes = 15
$burstRepeatCount = 2
$burstTrimMinimumMB = 30.0
$maxTargetsPerPass = 80
$trimCooldownMinutes = 10
$adaptiveNap = $true
$smartLearning = $false
$learningMinObservations = 3
$learningMaxProfiles = 180
$learningFastWakeThreshold = 3
$learningElevatedFreeMemoryMB = 6144.0
$learningAggressiveFreeMemoryMB = 3072.0
$intentEngine = $true
$intentMinConfidence = 60
$foregroundSwitchAccelerator = $true
$foregroundSwitchWindowSeconds = 90
$foregroundSwitchMinWakes = 2
$foregroundSwitchProtectMinutes = 5
$perGameProfiles = $true
$gameProfileMinObservations = 3
$behaviorEngine = $true
$behaviorMinObservations = 4
$behaviorMaxProfiles = 220
$behaviorDeepConfidence = 72
$behaviorLightConfidence = 62
$behaviorEfficientDeltaMB = 64.0
$behaviorRefaultPenaltyMB = 96.0
$behaviorStableCpuPercent = 0.8
$contentionRadar = $true
$downloadLauncherGuard = $true
$mediaCallProtection = $true
$memoryPressureGovernor = $true
$userAppPolicy = $true
$moderateFreeMemoryMB = 8192.0
$elevatedFreeMemoryMB = 6144.0
$criticalFreeMemoryMB = 3072.0
$deepNapMinimumMB = 180.0
$deepNapMaxCpuPercent = 0.35
$balancedNapMinimumMB = 80.0
$balancedNapMaxCpuPercent = 2.5
$lightNapTrimMinimumMB = 220.0
$balancedNapTrimMinimumMB = 80.0
$deepNapTrimMinimumMB = 45.0
$lightNapPriorityClassName = "BelowNormal"
$balancedNapPriorityClassName = "BelowNormal"
$deepNapPriorityClassName = "Idle"
$lightNapMemoryPriorityName = "BelowNormal"
$balancedNapMemoryPriorityName = "Low"
$deepNapMemoryPriorityName = "VeryLow"
$lightNapIoPriorityName = "Low"
$balancedNapIoPriorityName = "Low"
$deepNapIoPriorityName = "VeryLow"
$realtimeFriendlyDefaults = @("Discord", "Spotify", "WhatsApp", "Telegram", "Slack", "Teams", "steam", "steamwebhelper")
$realtimeFriendlyConfigured = $null
$knownLauncherDefaults = @("steam", "steamwebhelper", "EpicGamesLauncher", "EpicWebHelper", "Battle.net", "EADesktop", "EABackgroundService", "RiotClientServices", "RiotClientUx", "UbisoftConnect", "upc", "GalaxyClient", "GOG Galaxy", "XboxPcApp")
$knownCommunicationDefaults = @("Discord", "Teams", "Slack", "Zoom", "Telegram", "WhatsApp")
$knownMediaDefaults = @("Spotify", "vlc", "mpv", "obs64", "obs32", "Streamlabs Desktop")
$knownGamePathDefaults = @("\steamapps\common\", "\XboxGames\", "\Epic Games\", "\Riot Games\", "\Battle.net\", "\GOG Galaxy\Games\")
$knownLauncherConfigured = $null
$knownCommunicationConfigured = $null
$knownMediaConfigured = $null
$knownGamePathConfigured = $null

if ($smart) {
    if ($smart.PSObject.Properties.Name -contains "ForegroundWakeRestore") { $smartForegroundWake = [bool]$smart.ForegroundWakeRestore }
    if ($smart.PSObject.Properties.Name -contains "AutoProtectActiveApps") { $smartAutoProtect = [bool]$smart.AutoProtectActiveApps }
    if ($smart.PSObject.Properties.Name -contains "AutoProtectForegroundMinutes") { $autoProtectForegroundMinutes = [int]$smart.AutoProtectForegroundMinutes }
    if ($smart.PSObject.Properties.Name -contains "AutoProtectHighCpuMinutes") { $autoProtectHighCpuMinutes = [int]$smart.AutoProtectHighCpuMinutes }
    if ($smart.PSObject.Properties.Name -contains "FullscreenAware") { $smartFullscreenAware = [bool]$smart.FullscreenAware }
    if ($smart.PSObject.Properties.Name -contains "FullscreenTrimMinimumWorkingSetMB") { $fullscreenTrimMinimumMB = [double]$smart.FullscreenTrimMinimumWorkingSetMB }
    if ($smart.PSObject.Properties.Name -contains "FullscreenHighCpuPercentThreshold") { $fullscreenHighCpuThreshold = [double]$smart.FullscreenHighCpuPercentThreshold }
    if ($smart.PSObject.Properties.Name -contains "BurstWatcher") { $smartBurstWatcher = [bool]$smart.BurstWatcher }
    if ($smart.PSObject.Properties.Name -contains "BurstCpuPercentThreshold") { $burstCpuThreshold = [double]$smart.BurstCpuPercentThreshold }
    if ($smart.PSObject.Properties.Name -contains "BurstWindowMinutes") { $burstWindowMinutes = [int]$smart.BurstWindowMinutes }
    if ($smart.PSObject.Properties.Name -contains "BurstRepeatCount") { $burstRepeatCount = [int]$smart.BurstRepeatCount }
    if ($smart.PSObject.Properties.Name -contains "BurstTrimMinimumWorkingSetMB") { $burstTrimMinimumMB = [double]$smart.BurstTrimMinimumWorkingSetMB }
    if ($smart.PSObject.Properties.Name -contains "MaxTargetsPerPass") { $maxTargetsPerPass = [int]$smart.MaxTargetsPerPass }
    if ($smart.PSObject.Properties.Name -contains "TrimCooldownMinutes") { $trimCooldownMinutes = [int]$smart.TrimCooldownMinutes }
    if ($smart.PSObject.Properties.Name -contains "AdaptiveNap") { $adaptiveNap = [bool]$smart.AdaptiveNap }
    if ($smart.PSObject.Properties.Name -contains "LearningEnabled") { $smartLearning = [bool]$smart.LearningEnabled }
    if ($smart.PSObject.Properties.Name -contains "LearningMinObservations") { $learningMinObservations = [int]$smart.LearningMinObservations }
    if ($smart.PSObject.Properties.Name -contains "LearningMaxProfiles") { $learningMaxProfiles = [int]$smart.LearningMaxProfiles }
    if ($smart.PSObject.Properties.Name -contains "LearningFastWakeThreshold") { $learningFastWakeThreshold = [int]$smart.LearningFastWakeThreshold }
    if ($smart.PSObject.Properties.Name -contains "LearningElevatedFreeMemoryMB") { $learningElevatedFreeMemoryMB = [double]$smart.LearningElevatedFreeMemoryMB }
    if ($smart.PSObject.Properties.Name -contains "LearningAggressiveFreeMemoryMB") { $learningAggressiveFreeMemoryMB = [double]$smart.LearningAggressiveFreeMemoryMB }
    if ($smart.PSObject.Properties.Name -contains "IntentEngine") { $intentEngine = [bool]$smart.IntentEngine }
    if ($smart.PSObject.Properties.Name -contains "IntentMinConfidence") { $intentMinConfidence = [int]$smart.IntentMinConfidence }
    if ($smart.PSObject.Properties.Name -contains "ForegroundSwitchAccelerator") { $foregroundSwitchAccelerator = [bool]$smart.ForegroundSwitchAccelerator }
    if ($smart.PSObject.Properties.Name -contains "ForegroundSwitchWindowSeconds") { $foregroundSwitchWindowSeconds = [int]$smart.ForegroundSwitchWindowSeconds }
    if ($smart.PSObject.Properties.Name -contains "ForegroundSwitchMinWakes") { $foregroundSwitchMinWakes = [int]$smart.ForegroundSwitchMinWakes }
    if ($smart.PSObject.Properties.Name -contains "ForegroundSwitchProtectMinutes") { $foregroundSwitchProtectMinutes = [int]$smart.ForegroundSwitchProtectMinutes }
    if ($smart.PSObject.Properties.Name -contains "PerGameProfiles") { $perGameProfiles = [bool]$smart.PerGameProfiles }
    if ($smart.PSObject.Properties.Name -contains "GameProfileMinObservations") { $gameProfileMinObservations = [int]$smart.GameProfileMinObservations }
    if ($smart.PSObject.Properties.Name -contains "BehaviorEngine") { $behaviorEngine = [bool]$smart.BehaviorEngine }
    if ($smart.PSObject.Properties.Name -contains "BehaviorMinObservations") { $behaviorMinObservations = [int]$smart.BehaviorMinObservations }
    if ($smart.PSObject.Properties.Name -contains "BehaviorMaxProfiles") { $behaviorMaxProfiles = [int]$smart.BehaviorMaxProfiles }
    if ($smart.PSObject.Properties.Name -contains "BehaviorDeepConfidence") { $behaviorDeepConfidence = [int]$smart.BehaviorDeepConfidence }
    if ($smart.PSObject.Properties.Name -contains "BehaviorLightConfidence") { $behaviorLightConfidence = [int]$smart.BehaviorLightConfidence }
    if ($smart.PSObject.Properties.Name -contains "BehaviorEfficientDeltaMB") { $behaviorEfficientDeltaMB = [double]$smart.BehaviorEfficientDeltaMB }
    if ($smart.PSObject.Properties.Name -contains "BehaviorRefaultPenaltyMB") { $behaviorRefaultPenaltyMB = [double]$smart.BehaviorRefaultPenaltyMB }
    if ($smart.PSObject.Properties.Name -contains "BehaviorStableCpuPercent") { $behaviorStableCpuPercent = [double]$smart.BehaviorStableCpuPercent }
    if ($smart.PSObject.Properties.Name -contains "ContentionRadar") { $contentionRadar = [bool]$smart.ContentionRadar }
    if ($smart.PSObject.Properties.Name -contains "DownloadLauncherGuard") { $downloadLauncherGuard = [bool]$smart.DownloadLauncherGuard }
    if ($smart.PSObject.Properties.Name -contains "MediaCallProtection") { $mediaCallProtection = [bool]$smart.MediaCallProtection }
    if ($smart.PSObject.Properties.Name -contains "MemoryPressureGovernor") { $memoryPressureGovernor = [bool]$smart.MemoryPressureGovernor }
    if ($smart.PSObject.Properties.Name -contains "UserAppPolicy") { $userAppPolicy = [bool]$smart.UserAppPolicy }
    if ($smart.PSObject.Properties.Name -contains "ModerateFreeMemoryMB") { $moderateFreeMemoryMB = [double]$smart.ModerateFreeMemoryMB }
    if ($smart.PSObject.Properties.Name -contains "ElevatedFreeMemoryMB") { $elevatedFreeMemoryMB = [double]$smart.ElevatedFreeMemoryMB }
    if ($smart.PSObject.Properties.Name -contains "CriticalFreeMemoryMB") { $criticalFreeMemoryMB = [double]$smart.CriticalFreeMemoryMB }
    if ($smart.PSObject.Properties.Name -contains "DeepNapMinimumWorkingSetMB") { $deepNapMinimumMB = [double]$smart.DeepNapMinimumWorkingSetMB }
    if ($smart.PSObject.Properties.Name -contains "DeepNapMaxCpuPercent") { $deepNapMaxCpuPercent = [double]$smart.DeepNapMaxCpuPercent }
    if ($smart.PSObject.Properties.Name -contains "BalancedNapMinimumWorkingSetMB") { $balancedNapMinimumMB = [double]$smart.BalancedNapMinimumWorkingSetMB }
    if ($smart.PSObject.Properties.Name -contains "BalancedNapMaxCpuPercent") { $balancedNapMaxCpuPercent = [double]$smart.BalancedNapMaxCpuPercent }
    if ($smart.PSObject.Properties.Name -contains "LightNapTrimMinimumWorkingSetMB") { $lightNapTrimMinimumMB = [double]$smart.LightNapTrimMinimumWorkingSetMB }
    if ($smart.PSObject.Properties.Name -contains "BalancedNapTrimMinimumWorkingSetMB") { $balancedNapTrimMinimumMB = [double]$smart.BalancedNapTrimMinimumWorkingSetMB }
    if ($smart.PSObject.Properties.Name -contains "DeepNapTrimMinimumWorkingSetMB") { $deepNapTrimMinimumMB = [double]$smart.DeepNapTrimMinimumWorkingSetMB }
    if ($smart.PSObject.Properties.Name -contains "LightNapPriorityClass") { $lightNapPriorityClassName = [string]$smart.LightNapPriorityClass }
    if ($smart.PSObject.Properties.Name -contains "BalancedNapPriorityClass") { $balancedNapPriorityClassName = [string]$smart.BalancedNapPriorityClass }
    if ($smart.PSObject.Properties.Name -contains "DeepNapPriorityClass") { $deepNapPriorityClassName = [string]$smart.DeepNapPriorityClass }
    if ($smart.PSObject.Properties.Name -contains "LightNapMemoryPriority") { $lightNapMemoryPriorityName = [string]$smart.LightNapMemoryPriority }
    if ($smart.PSObject.Properties.Name -contains "BalancedNapMemoryPriority") { $balancedNapMemoryPriorityName = [string]$smart.BalancedNapMemoryPriority }
    if ($smart.PSObject.Properties.Name -contains "DeepNapMemoryPriority") { $deepNapMemoryPriorityName = [string]$smart.DeepNapMemoryPriority }
    if ($smart.PSObject.Properties.Name -contains "LightNapIoPriority") { $lightNapIoPriorityName = [string]$smart.LightNapIoPriority }
    if ($smart.PSObject.Properties.Name -contains "BalancedNapIoPriority") { $balancedNapIoPriorityName = [string]$smart.BalancedNapIoPriority }
    if ($smart.PSObject.Properties.Name -contains "DeepNapIoPriority") { $deepNapIoPriorityName = [string]$smart.DeepNapIoPriority }
    if ($smart.PSObject.Properties.Name -contains "RealtimeFriendlyProcessNames") { $realtimeFriendlyConfigured = @($smart.RealtimeFriendlyProcessNames) }
    if ($smart.PSObject.Properties.Name -contains "KnownLauncherProcessNames") { $knownLauncherConfigured = @($smart.KnownLauncherProcessNames) }
    if ($smart.PSObject.Properties.Name -contains "KnownCommunicationProcessNames") { $knownCommunicationConfigured = @($smart.KnownCommunicationProcessNames) }
    if ($smart.PSObject.Properties.Name -contains "KnownMediaProcessNames") { $knownMediaConfigured = @($smart.KnownMediaProcessNames) }
    if ($smart.PSObject.Properties.Name -contains "KnownGamePathFragments") { $knownGamePathConfigured = @($smart.KnownGamePathFragments) }
    if ($smart.PSObject.Properties.Name -contains "NapScore") { $smartNapScore = [bool]$smart.NapScore }
}
if ($autoProtectForegroundMinutes -lt 1) { $autoProtectForegroundMinutes = 1 }
if ($autoProtectHighCpuMinutes -lt 1) { $autoProtectHighCpuMinutes = 1 }
if ($burstWindowMinutes -lt 1) { $burstWindowMinutes = 1 }
if ($burstRepeatCount -lt 1) { $burstRepeatCount = 1 }
if ($maxTargetsPerPass -lt 1) { $maxTargetsPerPass = 1 }
if ($trimCooldownMinutes -lt 1) { $trimCooldownMinutes = 1 }
if ($learningMinObservations -lt 1) { $learningMinObservations = 1 }
if ($learningMaxProfiles -lt 20) { $learningMaxProfiles = 20 }
if ($learningFastWakeThreshold -lt 1) { $learningFastWakeThreshold = 1 }
if ($learningElevatedFreeMemoryMB -lt 512) { $learningElevatedFreeMemoryMB = 512.0 }
if ($learningAggressiveFreeMemoryMB -lt 256) { $learningAggressiveFreeMemoryMB = 256.0 }
if ($intentMinConfidence -lt 0) { $intentMinConfidence = 0 }
if ($intentMinConfidence -gt 100) { $intentMinConfidence = 100 }
if ($foregroundSwitchWindowSeconds -lt 15) { $foregroundSwitchWindowSeconds = 15 }
if ($foregroundSwitchMinWakes -lt 1) { $foregroundSwitchMinWakes = 1 }
if ($foregroundSwitchProtectMinutes -lt 1) { $foregroundSwitchProtectMinutes = 1 }
if ($gameProfileMinObservations -lt 1) { $gameProfileMinObservations = 1 }
if ($behaviorMinObservations -lt 2) { $behaviorMinObservations = 2 }
if ($behaviorMaxProfiles -lt 40) { $behaviorMaxProfiles = 40 }
if ($behaviorDeepConfidence -lt 0) { $behaviorDeepConfidence = 0 }
if ($behaviorDeepConfidence -gt 100) { $behaviorDeepConfidence = 100 }
if ($behaviorLightConfidence -lt 0) { $behaviorLightConfidence = 0 }
if ($behaviorLightConfidence -gt 100) { $behaviorLightConfidence = 100 }
if ($behaviorEfficientDeltaMB -lt 8.0) { $behaviorEfficientDeltaMB = 8.0 }
if ($behaviorRefaultPenaltyMB -lt 16.0) { $behaviorRefaultPenaltyMB = 16.0 }
if ($behaviorStableCpuPercent -lt 0.0) { $behaviorStableCpuPercent = 0.0 }
if ($moderateFreeMemoryMB -lt 512) { $moderateFreeMemoryMB = 512.0 }
if ($elevatedFreeMemoryMB -lt 512) { $elevatedFreeMemoryMB = 512.0 }
if ($criticalFreeMemoryMB -lt 256) { $criticalFreeMemoryMB = 256.0 }
if ($deepNapMinimumMB -lt 1) { $deepNapMinimumMB = 1.0 }
if ($balancedNapMinimumMB -lt 1) { $balancedNapMinimumMB = 1.0 }
if ($deepNapMaxCpuPercent -lt 0) { $deepNapMaxCpuPercent = 0.0 }
if ($balancedNapMaxCpuPercent -lt 0) { $balancedNapMaxCpuPercent = 0.0 }
if ($lightNapTrimMinimumMB -lt 1) { $lightNapTrimMinimumMB = 1.0 }
if ($balancedNapTrimMinimumMB -lt 1) { $balancedNapTrimMinimumMB = 1.0 }
if ($deepNapTrimMinimumMB -lt 1) { $deepNapTrimMinimumMB = 1.0 }

$protectedNames = New-Object "System.Collections.Generic.HashSet[string]" ([System.StringComparer]::OrdinalIgnoreCase)
@($config.ProtectedProcessNames + $nap.ProtectedProcessNames) | Where-Object { $_ } | ForEach-Object { [void]$protectedNames.Add([string]$_) }

$protectedPathFragments = @($nap.ProtectedPathFragments | Where-Object { $_ } | ForEach-Object { [string]$_ })

$systemNames = New-Object "System.Collections.Generic.HashSet[string]" ([System.StringComparer]::OrdinalIgnoreCase)
@($nap.SystemProcessNames) | Where-Object { $_ } | ForEach-Object { [void]$systemNames.Add([string]$_) }

$realtimeFriendlyNames = New-Object "System.Collections.Generic.HashSet[string]" ([System.StringComparer]::OrdinalIgnoreCase)
$realtimeFriendlySource = if ($realtimeFriendlyConfigured -ne $null) { $realtimeFriendlyConfigured } else { $realtimeFriendlyDefaults }
@($realtimeFriendlySource) | Where-Object { $_ } | ForEach-Object { [void]$realtimeFriendlyNames.Add([string]$_) }

$knownLauncherNames = New-Object "System.Collections.Generic.HashSet[string]" ([System.StringComparer]::OrdinalIgnoreCase)
$knownLauncherSource = if ($knownLauncherConfigured -ne $null) { $knownLauncherConfigured } else { $knownLauncherDefaults }
@($knownLauncherSource) | Where-Object { $_ } | ForEach-Object { [void]$knownLauncherNames.Add([string]$_) }

$knownCommunicationNames = New-Object "System.Collections.Generic.HashSet[string]" ([System.StringComparer]::OrdinalIgnoreCase)
$knownCommunicationSource = if ($knownCommunicationConfigured -ne $null) { $knownCommunicationConfigured } else { $knownCommunicationDefaults }
@($knownCommunicationSource) | Where-Object { $_ } | ForEach-Object { [void]$knownCommunicationNames.Add([string]$_) }

$knownMediaNames = New-Object "System.Collections.Generic.HashSet[string]" ([System.StringComparer]::OrdinalIgnoreCase)
$knownMediaSource = if ($knownMediaConfigured -ne $null) { $knownMediaConfigured } else { $knownMediaDefaults }
@($knownMediaSource) | Where-Object { $_ } | ForEach-Object { [void]$knownMediaNames.Add([string]$_) }

$knownGamePathFragments = @()
$knownGamePathSource = if ($knownGamePathConfigured -ne $null) { $knownGamePathConfigured } else { $knownGamePathDefaults }
@($knownGamePathSource) | Where-Object { $_ } | ForEach-Object { $knownGamePathFragments += [string]$_ }

$script:learningMap = @{}
$script:currentMemoryPressure = [pscustomobject]@{ Level = "Unknown"; FreeMB = -1.0; UsedPercent = -1.0 }
$script:currentLearningSession = [pscustomobject]@{ Name = ""; Kind = "Desktop"; Pressure = "Unknown" }
$script:currentIntent = [pscustomobject]@{ Kind = "Desktop"; Name = ""; Confidence = 0; Signals = @(); Foreground = "" }
$script:foregroundSwitchMap = @{}
$script:gameProfileMap = @{}
$script:behaviorMap = @{}
$script:appPolicyMap = @{}

$memoryPriorityMap = @{
    VeryLow = 1
    Low = 2
    Medium = 3
    BelowNormal = 4
    Normal = 5
}
$memoryPriorityName = [string]$nap.MemoryPriority
if (-not $memoryPriorityMap.ContainsKey($memoryPriorityName)) {
    $memoryPriorityName = "Low"
}
$targetMemoryPriority = [int]$memoryPriorityMap[$memoryPriorityName]
$normalMemoryPriority = [int]$memoryPriorityMap["Normal"]

$ioPriorityMap = @{
    VeryLow = 0
    Low = 1
    Normal = 2
    High = 3
}
$ioPriorityName = [string]$nap.IoPriority
if (-not $ioPriorityMap.ContainsKey($ioPriorityName)) {
    $ioPriorityName = "Low"
}
$useIoPriority = [bool]$nap.EnableIoPriority
$targetIoPriority = [int]$ioPriorityMap[$ioPriorityName]
$normalIoPriority = [int]$ioPriorityMap["Normal"]
$ioPriorityNameByValue = @{}
foreach ($key in $ioPriorityMap.Keys) {
    $ioPriorityNameByValue[[int]$ioPriorityMap[$key]] = [string]$key
}

function Resolve-PriorityClass {
    param(
        [string]$Name,
        [System.Diagnostics.ProcessPriorityClass]$Fallback
    )
    try {
        if (-not [string]::IsNullOrWhiteSpace($Name)) {
            return [System.Enum]::Parse([System.Diagnostics.ProcessPriorityClass], $Name, $true)
        }
    } catch {
    }
    return $Fallback
}

function Resolve-MemoryPriority {
    param(
        [string]$Name,
        [int]$Fallback
    )
    if ($Name -and $memoryPriorityMap.ContainsKey($Name)) {
        return [int]$memoryPriorityMap[$Name]
    }
    return $Fallback
}

function Resolve-IoPriority {
    param(
        [string]$Name,
        [int]$Fallback
    )
    if ($Name -and $ioPriorityMap.ContainsKey($Name)) {
        return [int]$ioPriorityMap[$Name]
    }
    return $Fallback
}

$napTierPriority = @{
    Light = Resolve-PriorityClass -Name $lightNapPriorityClassName -Fallback $targetPriorityClass
    Balanced = Resolve-PriorityClass -Name $balancedNapPriorityClassName -Fallback $targetPriorityClass
    Deep = Resolve-PriorityClass -Name $deepNapPriorityClassName -Fallback $targetPriorityClass
}
$napTierMemory = @{
    Light = Resolve-MemoryPriority -Name $lightNapMemoryPriorityName -Fallback $targetMemoryPriority
    Balanced = Resolve-MemoryPriority -Name $balancedNapMemoryPriorityName -Fallback $targetMemoryPriority
    Deep = Resolve-MemoryPriority -Name $deepNapMemoryPriorityName -Fallback $targetMemoryPriority
}
$napTierIo = @{
    Light = Resolve-IoPriority -Name $lightNapIoPriorityName -Fallback $targetIoPriority
    Balanced = Resolve-IoPriority -Name $balancedNapIoPriorityName -Fallback $targetIoPriority
    Deep = Resolve-IoPriority -Name $deepNapIoPriorityName -Fallback $targetIoPriority
}
$napTierTrimMinimum = @{
    Light = $lightNapTrimMinimumMB
    Balanced = $balancedNapTrimMinimumMB
    Deep = $deepNapTrimMinimumMB
}

$currentProcess = Get-Process -Id $PID
$currentSessionId = $currentProcess.SessionId
$currentPid = $currentProcess.Id
$logicalProcessorCount = [Environment]::ProcessorCount

$cs = @"
using System;
using System.Runtime.InteropServices;

public static class BackgroundNapNative {
    private const UInt32 PROCESS_SET_INFORMATION = 0x0200;
    private const UInt32 PROCESS_QUERY_INFORMATION = 0x0400;
    private const UInt32 PROCESS_SET_QUOTA = 0x0100;
    private const UInt32 PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
    private const Int32 ProcessMemoryPriority = 0;
    private const Int32 ProcessPowerThrottling = 4;
    private const Int32 ProcessIoPriority = 33;
    private const UInt32 PROCESS_POWER_THROTTLING_CURRENT_VERSION = 1;
    private const UInt32 PROCESS_POWER_THROTTLING_EXECUTION_SPEED = 0x1;
    private const UInt32 PROCESS_POWER_THROTTLING_IGNORE_TIMER_RESOLUTION = 0x4;
    private const UInt32 MONITOR_DEFAULTTONEAREST = 0x2;

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORY_PRIORITY_INFORMATION {
        public UInt32 MemoryPriority;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_POWER_THROTTLING_STATE {
        public UInt32 Version;
        public UInt32 ControlMask;
        public UInt32 StateMask;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT {
        public Int32 Left;
        public Int32 Top;
        public Int32 Right;
        public Int32 Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO {
        public UInt32 cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public UInt32 dwFlags;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(UInt32 dwDesiredAccess, bool bInheritHandle, Int32 dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetProcessInformation(IntPtr hProcess, Int32 processInformationClass, IntPtr processInformation, UInt32 processInformationSize);

    [DllImport("ntdll.dll")]
    private static extern Int32 NtSetInformationProcess(IntPtr ProcessHandle, Int32 ProcessInformationClass, ref UInt32 ProcessInformation, UInt32 ProcessInformationLength);

    [DllImport("ntdll.dll")]
    private static extern Int32 NtQueryInformationProcess(IntPtr ProcessHandle, Int32 ProcessInformationClass, out UInt32 ProcessInformation, UInt32 ProcessInformationLength, out UInt32 ReturnLength);

    [DllImport("psapi.dll", SetLastError = true)]
    private static extern bool EmptyWorkingSet(IntPtr hProcess);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern UInt32 GetWindowThreadProcessId(IntPtr hWnd, out UInt32 lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, UInt32 dwFlags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    public static Int32 GetForegroundPid() {
        UInt32 pid;
        IntPtr hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero) {
            return 0;
        }
        GetWindowThreadProcessId(hwnd, out pid);
        return (Int32)pid;
    }

    public static bool IsForegroundFullscreen() {
        IntPtr hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero) {
            return false;
        }

        RECT window;
        if (!GetWindowRect(hwnd, out window)) {
            return false;
        }

        IntPtr monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
        if (monitor == IntPtr.Zero) {
            return false;
        }

        MONITORINFO info = new MONITORINFO();
        info.cbSize = (UInt32)Marshal.SizeOf(typeof(MONITORINFO));
        if (!GetMonitorInfo(monitor, ref info)) {
            return false;
        }

        Int32 tolerance = 2;
        return window.Left <= info.rcMonitor.Left + tolerance &&
               window.Top <= info.rcMonitor.Top + tolerance &&
               window.Right >= info.rcMonitor.Right - tolerance &&
               window.Bottom >= info.rcMonitor.Bottom - tolerance;
    }

    public static Int32 SetMemoryPriority(Int32 pid, UInt32 memoryPriority) {
        IntPtr h = OpenProcess(PROCESS_SET_INFORMATION | PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (h == IntPtr.Zero) {
            return Marshal.GetLastWin32Error();
        }

        MEMORY_PRIORITY_INFORMATION info = new MEMORY_PRIORITY_INFORMATION();
        info.MemoryPriority = memoryPriority;
        IntPtr ptr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(MEMORY_PRIORITY_INFORMATION)));
        try {
            Marshal.StructureToPtr(info, ptr, false);
            bool ok = SetProcessInformation(h, ProcessMemoryPriority, ptr, (UInt32)Marshal.SizeOf(typeof(MEMORY_PRIORITY_INFORMATION)));
            if (!ok) {
                return Marshal.GetLastWin32Error();
            }
            return 0;
        } finally {
            Marshal.FreeHGlobal(ptr);
            CloseHandle(h);
        }
    }

    public static Int32 SetPowerThrottling(Int32 pid, bool ecoQos, bool ignoreTimerResolution, bool restoreNormal) {
        UInt32 mask = 0;
        if (ecoQos) {
            mask |= PROCESS_POWER_THROTTLING_EXECUTION_SPEED;
        }
        if (ignoreTimerResolution) {
            mask |= PROCESS_POWER_THROTTLING_IGNORE_TIMER_RESOLUTION;
        }
        if (mask == 0) {
            return 0;
        }

        IntPtr h = OpenProcess(PROCESS_SET_INFORMATION | PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (h == IntPtr.Zero) {
            return Marshal.GetLastWin32Error();
        }

        PROCESS_POWER_THROTTLING_STATE state = new PROCESS_POWER_THROTTLING_STATE();
        state.Version = PROCESS_POWER_THROTTLING_CURRENT_VERSION;
        state.ControlMask = mask;
        state.StateMask = restoreNormal ? 0 : mask;

        IntPtr ptr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(PROCESS_POWER_THROTTLING_STATE)));
        try {
            Marshal.StructureToPtr(state, ptr, false);
            bool ok = SetProcessInformation(h, ProcessPowerThrottling, ptr, (UInt32)Marshal.SizeOf(typeof(PROCESS_POWER_THROTTLING_STATE)));
            if (!ok) {
                return Marshal.GetLastWin32Error();
            }
            return 0;
        } finally {
            Marshal.FreeHGlobal(ptr);
            CloseHandle(h);
        }
    }

    public static Int32 SetIoPriority(Int32 pid, UInt32 ioPriority) {
        IntPtr h = OpenProcess(PROCESS_SET_INFORMATION | PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (h == IntPtr.Zero) {
            return unchecked((Int32)0xC0000022);
        }

        try {
            return NtSetInformationProcess(h, ProcessIoPriority, ref ioPriority, sizeof(UInt32));
        } finally {
            CloseHandle(h);
        }
    }

    public static Int32 GetIoPriority(Int32 pid, out UInt32 ioPriority) {
        ioPriority = 0;
        UInt32 returnLength = 0;
        IntPtr h = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (h == IntPtr.Zero) {
            return unchecked((Int32)0xC0000022);
        }

        try {
            return NtQueryInformationProcess(h, ProcessIoPriority, out ioPriority, sizeof(UInt32), out returnLength);
        } finally {
            CloseHandle(h);
        }
    }

    public static Int32 TrimWorkingSet(Int32 pid) {
        IntPtr h = OpenProcess(PROCESS_SET_QUOTA | PROCESS_QUERY_INFORMATION | PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (h == IntPtr.Zero) {
            return Marshal.GetLastWin32Error();
        }

        try {
            bool ok = EmptyWorkingSet(h);
            if (!ok) {
                return Marshal.GetLastWin32Error();
            }
            return 0;
        } finally {
            CloseHandle(h);
        }
    }
}
"@

if (-not ("BackgroundNapNative" -as [type])) {
    Add-Type -TypeDefinition $cs -Language CSharp
}

function Convert-Win32Result {
    param([int]$Code)
    if ($Code -eq 0) { return "OK" }
    return "Win32Error=$Code"
}

function Convert-NtStatusResult {
    param([int]$Code)
    if ($Code -eq 0) { return "OK" }
    $unsigned = [BitConverter]::ToUInt32([BitConverter]::GetBytes([int]$Code), 0)
    return ("NtStatus=0x{0:X8}" -f $unsigned)
}

function Get-ForegroundInfo {
    $foregroundPid = [BackgroundNapNative]::GetForegroundPid()
    $proc = $null
    if ($foregroundPid -gt 0) {
        $proc = Get-Process -Id $foregroundPid -ErrorAction SilentlyContinue
    }

    [pscustomobject]@{
        Id = $foregroundPid
        ProcessName = if ($proc) { $proc.ProcessName } else { $null }
        IsFullscreen = if ($smartFullscreenAware) { [BackgroundNapNative]::IsForegroundFullscreen() } else { $false }
        Path = if ($proc) { Get-ProcessPathText -Process $proc } else { $null }
    }
}

function Get-ProcessPriorityText {
    param([System.Diagnostics.Process]$Process)
    try { return [string]$Process.PriorityClass } catch { return $null }
}

function Get-ProcessPathText {
    param([System.Diagnostics.Process]$Process)
    try { return [string]$Process.Path } catch { return $null }
}

function Get-ProcessIoPriorityText {
    param([System.Diagnostics.Process]$Process)
    try {
        $raw = [uint32]0
        $status = [BackgroundNapNative]::GetIoPriority([int]$Process.Id, [ref]$raw)
        if ($status -ne 0) { return $null }
        $value = [int]$raw
        if ($ioPriorityNameByValue.ContainsKey($value)) {
            return [string]$ioPriorityNameByValue[$value]
        }
        return [string]$value
    } catch {
        return $null
    }
}

function Get-ProcessIdentityKey {
    param(
        [System.Diagnostics.Process]$Process,
        [string]$Path
    )

    if ($Path) {
        return ("path:" + $Path.ToLowerInvariant())
    }
    return ("name:" + $Process.ProcessName.ToLowerInvariant())
}

function Get-TrimIdentityKey {
    param(
        [System.Diagnostics.Process]$Process,
        [string]$Path
    )

    if ($Path) {
        return ("pidpath:" + $Process.Id.ToString() + ":" + $Path.ToLowerInvariant())
    }
    return ("pidname:" + $Process.Id.ToString() + ":" + $Process.ProcessName.ToLowerInvariant())
}

function Read-StateArray {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        return @()
    }

    try {
        $data = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
        if ($data -and $data.Items) {
            return @($data.Items)
        }
    } catch {
    }

    return @()
}

function Write-StateArray {
    param(
        [string]$Path,
        [array]$Items
    )

    $state = [pscustomobject]@{
        Timestamp = (Get-Date).ToString("o")
        Items = @($Items)
    }
    $state | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $Path -Encoding UTF8
}

function Write-StateObject {
    param(
        [string]$Path,
        [object]$Value,
        [int]$Depth = 7
    )

    $Value | ConvertTo-Json -Depth $Depth | Set-Content -LiteralPath $Path -Encoding UTF8
}

function Test-NameInSet {
    param(
        [object]$Set,
        [string]$Name
    )

    if (-not $Set -or [string]::IsNullOrWhiteSpace($Name)) { return $false }
    try { return [bool]$Set.Contains($Name) } catch { return $false }
}

function Test-PathContainsFragment {
    param(
        [string]$Path,
        [array]$Fragments
    )

    if ([string]::IsNullOrWhiteSpace($Path)) { return $false }
    foreach ($fragment in @($Fragments)) {
        if ([string]::IsNullOrWhiteSpace([string]$fragment)) { continue }
        if ($Path.IndexOf([string]$fragment, [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
            return $true
        }
    }
    return $false
}

function Normalize-AppIdentityKey {
    param([string]$Key)
    if ([string]::IsNullOrWhiteSpace($Key)) { return "" }
    return $Key.Trim().ToLowerInvariant()
}

function Add-AppIdentityKey {
    param(
        [System.Collections.ArrayList]$Keys,
        [string]$Key
    )
    $normalized = Normalize-AppIdentityKey -Key $Key
    if ([string]::IsNullOrWhiteSpace($normalized)) { return }
    if (-not $Keys.Contains($normalized)) { [void]$Keys.Add($normalized) }
}

function Get-AppIdentityKeysFromText {
    param(
        [string]$ProcessName,
        [string]$Path
    )

    $keys = New-Object System.Collections.ArrayList
    if (-not [string]::IsNullOrWhiteSpace($Path)) {
        Add-AppIdentityKey -Keys $keys -Key ("path:" + $Path)
    }
    if (-not [string]::IsNullOrWhiteSpace($ProcessName)) {
        Add-AppIdentityKey -Keys $keys -Key ("name:" + $ProcessName)
    }
    return @($keys)
}

function Get-LearningKeyFromText {
    param(
        [string]$ProcessName,
        [string]$Path
    )

    $keys = @(Get-AppIdentityKeysFromText -ProcessName $ProcessName -Path $Path)
    if ($keys.Count -gt 0) { return [string]$keys[0] }
    return ""
}

function Get-AppIdentityKeyFromText {
    param(
        [string]$ProcessName,
        [string]$Path
    )

    return Get-LearningKeyFromText -ProcessName $ProcessName -Path $Path
}

function Get-ProcessRole {
    param(
        [string]$ProcessName,
        [string]$Path
    )

    if (Test-NameInSet -Set $knownLauncherNames -Name $ProcessName) { return "Launcher" }
    if (Test-NameInSet -Set $knownCommunicationNames -Name $ProcessName) { return "Communication" }
    if (Test-NameInSet -Set $knownMediaNames -Name $ProcessName) { return "Media" }
    if (Test-PathContainsFragment -Path $Path -Fragments $knownGamePathFragments) { return "GameCandidate" }
    if ($ProcessName -match '^(chrome|msedge|firefox|zen|brave|opera|vivaldi)$') { return "Browser" }
    return "App"
}

function Read-AppPolicyMap {
    $map = @{}
    if (-not $userAppPolicy) { return $map }
    foreach ($item in @(Read-StateArray -Path $appPolicyStatePath)) {
        if (-not $item.Key) { continue }
        $policy = [string]$item.Policy
        if ($policy -notin @("Protect", "Light", "Balanced", "Deep")) { continue }
        $key = Normalize-AppIdentityKey -Key ([string]$item.Key)
        $keys = New-Object System.Collections.ArrayList
        Add-AppIdentityKey -Keys $keys -Key $key
        foreach ($alias in @(Get-AppIdentityKeysFromText -ProcessName ([string]$item.ProcessName) -Path ([string]$item.Path))) {
            Add-AppIdentityKey -Keys $keys -Key ([string]$alias)
        }
        foreach ($policyKey in @($keys)) {
            $map[[string]$policyKey] = [pscustomobject]@{
                Key = [string]$policyKey
                ProcessName = [string]$item.ProcessName
                Path = [string]$item.Path
                Policy = $policy
                UpdatedAt = [string]$item.UpdatedAt
            }
        }
    }
    return $map
}

function Get-AppPolicyForText {
    param(
        [string]$ProcessName,
        [string]$Path
    )

    $keys = @(Get-AppIdentityKeysFromText -ProcessName $ProcessName -Path $Path)
    foreach ($key in $keys) {
        if ($key -and $script:appPolicyMap.ContainsKey($key)) {
            return $script:appPolicyMap[$key]
        }
    }
    $key = if ($keys.Count -gt 0) { [string]$keys[0] } else { "" }
    return [pscustomobject]@{
        Key = $key
        ProcessName = $ProcessName
        Path = $Path
        Policy = "Auto"
        UpdatedAt = ""
    }
}

function Read-ForegroundSwitchMap {
    $map = @{}
    if (-not $foregroundSwitchAccelerator) { return $map }
    $now = Get-Date
    foreach ($item in @(Read-StateArray -Path $foregroundSwitchStatePath)) {
        if (-not $item.Key) { continue }
        $lastObserved = $null
        try { if ($item.LastObservedAt) { $lastObserved = [DateTime]::Parse([string]$item.LastObservedAt, $null, [Globalization.DateTimeStyles]::RoundtripKind) } } catch { $lastObserved = $null }
        if ($lastObserved -and (($now - $lastObserved).TotalHours -gt 24)) { continue }
        $map[[string]$item.Key] = [pscustomobject]@{
            Key = [string]$item.Key
            ProcessName = [string]$item.ProcessName
            Path = [string]$item.Path
            WakeCount = [int]$item.WakeCount
            WindowStartedAt = [string]$item.WindowStartedAt
            LastWakeAt = [string]$item.LastWakeAt
            LastObservedAt = [string]$item.LastObservedAt
            LastPid = [int]$item.LastPid
            ProtectUntil = [string]$item.ProtectUntil
            FastWake = [bool]$item.FastWake
        }
    }
    return $map
}

function Save-ForegroundSwitchMap {
    param([hashtable]$Map)

    if (-not $foregroundSwitchAccelerator) { return }
    $items = @($Map.Values |
        Sort-Object @{ Expression = { if ($_.LastObservedAt) { [string]$_.LastObservedAt } else { "" } }; Descending = $true } |
        Select-Object -First 180)
    Write-StateArray -Path $foregroundSwitchStatePath -Items $items
}

function Add-ForegroundSwitchObservation {
    param(
        [hashtable]$Map,
        [string]$ProcessName,
        [string]$Path,
        [int]$ProcessId,
        [switch]$ForceCount
    )

    if (-not $foregroundSwitchAccelerator) { return $null }
    $key = Get-AppIdentityKeyFromText -ProcessName $ProcessName -Path $Path
    if (-not $key) { return $null }
    $now = Get-Date
    $item = $null
    if ($Map.ContainsKey($key)) {
        $item = $Map[$key]
    } else {
        $item = [pscustomobject]@{
            Key = $key
            ProcessName = $ProcessName
            Path = $Path
            WakeCount = 0
            WindowStartedAt = $now.ToString("o")
            LastWakeAt = ""
            LastObservedAt = ""
            LastPid = 0
            ProtectUntil = ""
            FastWake = $false
        }
    }

    $windowStarted = $now
    try {
        if ($item.WindowStartedAt) { $windowStarted = [DateTime]::Parse([string]$item.WindowStartedAt, $null, [Globalization.DateTimeStyles]::RoundtripKind) }
    } catch { $windowStarted = $now }
    if (($now - $windowStarted).TotalSeconds -gt $foregroundSwitchWindowSeconds) {
        $item.WakeCount = 0
        $item.WindowStartedAt = $now.ToString("o")
    }

    $lastObserved = $null
    try {
        if ($item.LastObservedAt) { $lastObserved = [DateTime]::Parse([string]$item.LastObservedAt, $null, [Globalization.DateTimeStyles]::RoundtripKind) }
    } catch { $lastObserved = $null }
    $duplicatePoll = (-not $ForceCount) -and $lastObserved -and ([int]$item.LastPid -eq $ProcessId) -and (($now - $lastObserved).TotalSeconds -lt 8)
    if (-not $duplicatePoll) {
        $item.WakeCount = [int]$item.WakeCount + 1
        $item.LastWakeAt = $now.ToString("o")
    }

    $item.ProcessName = $ProcessName
    $item.Path = $Path
    $item.LastPid = $ProcessId
    $item.LastObservedAt = $now.ToString("o")
    $item.FastWake = ([int]$item.WakeCount -ge $foregroundSwitchMinWakes)
    if ($item.FastWake) {
        $item.ProtectUntil = $now.AddMinutes($foregroundSwitchProtectMinutes).ToString("o")
    }
    $Map[$key] = $item
    return $item
}

function Get-ForegroundSwitchProfile {
    param(
        [string]$ProcessName,
        [string]$Path
    )

    $key = Get-AppIdentityKeyFromText -ProcessName $ProcessName -Path $Path
    if ($key -and $script:foregroundSwitchMap.ContainsKey($key)) {
        return $script:foregroundSwitchMap[$key]
    }
    return $null
}

function Test-ForegroundSwitchProtected {
    param([object]$Profile)

    if (-not $foregroundSwitchAccelerator -or -not $Profile -or -not $Profile.ProtectUntil) { return $false }
    try {
        $until = [DateTime]::Parse([string]$Profile.ProtectUntil, $null, [Globalization.DateTimeStyles]::RoundtripKind)
        return $until -gt (Get-Date)
    } catch {
        return $false
    }
}

function Read-GameProfileMap {
    $map = @{}
    if (-not $perGameProfiles) { return $map }
    foreach ($item in @(Read-StateArray -Path $gameProfileStatePath)) {
        if (-not $item.Key) { continue }
        $key = [string]$item.Key
        $map[$key] = [pscustomobject]@{
            Key = $key
            Name = [string]$item.Name
            Path = [string]$item.Path
            Observations = [int]$item.Observations
            AvgTargets = [double]$item.AvgTargets
            AvgDeltaMB = [double]$item.AvgDeltaMB
            AvgPressureScore = [double]$item.AvgPressureScore
            AggressionBias = [int]$item.AggressionBias
            LastSeen = [string]$item.LastSeen
        }
    }
    return $map
}

function Save-GameProfileMap {
    param([hashtable]$Map)

    if (-not $perGameProfiles) { return }
    $items = @($Map.Values |
        Sort-Object @{ Expression = { if ($_.LastSeen) { [string]$_.LastSeen } else { "" } }; Descending = $true } |
        Select-Object -First 80)
    Write-StateArray -Path $gameProfileStatePath -Items $items
}

function Get-GameProfileKey {
    param([object]$Intent)

    if (-not $Intent -or [string]::IsNullOrWhiteSpace([string]$Intent.Name)) { return "" }
    if ($Intent.Path) { return "path:" + ([string]$Intent.Path).ToLowerInvariant() }
    return "name:" + ([string]$Intent.Name).ToLowerInvariant()
}

function Get-CurrentGameProfile {
    if (-not $perGameProfiles -or -not $script:currentIntent -or [string]$script:currentIntent.Kind -ne "Gaming") { return $null }
    $key = Get-GameProfileKey -Intent $script:currentIntent
    if ($key -and $script:gameProfileMap.ContainsKey($key)) { return $script:gameProfileMap[$key] }
    return $null
}

function Update-GameProfiles {
    param([array]$Results)

    if (-not $perGameProfiles -or -not $script:currentIntent -or [string]$script:currentIntent.Kind -ne "Gaming") { return }
    $key = Get-GameProfileKey -Intent $script:currentIntent
    if (-not $key) { return }
    $now = (Get-Date).ToString("o")
    $profile = $null
    if ($script:gameProfileMap.ContainsKey($key)) {
        $profile = $script:gameProfileMap[$key]
    } else {
        $profile = [pscustomobject]@{
            Key = $key
            Name = [string]$script:currentIntent.Name
            Path = [string]$script:currentIntent.Path
            Observations = 0
            AvgTargets = 0.0
            AvgDeltaMB = 0.0
            AvgPressureScore = 0.0
            AggressionBias = 0
            LastSeen = $now
        }
    }

    $targetCount = @($Results).Count
    $delta = 0.0
    foreach ($r in @($Results)) {
        if ($r.WorkingSetBeforeMB -ne $null -and $r.WorkingSetAfterMB -ne $null) {
            $d = [double]$r.WorkingSetBeforeMB - [double]$r.WorkingSetAfterMB
            if ($d -gt 0) { $delta += $d }
        }
    }
    $pressureScore = 0.0
    if ([string]$script:currentMemoryPressure.Level -eq "Moderate") { $pressureScore = 1.0 }
    if ([string]$script:currentMemoryPressure.Level -eq "Elevated") { $pressureScore = 2.0 }
    if ([string]$script:currentMemoryPressure.Level -eq "Critical") { $pressureScore = 3.0 }

    $profile.Observations = [int]$profile.Observations + 1
    $profile.Name = [string]$script:currentIntent.Name
    $profile.Path = [string]$script:currentIntent.Path
    $profile.AvgTargets = Update-LearningAverage -OldValue ([double]$profile.AvgTargets) -NewValue ([double]$targetCount) -ObservationCount ([int]$profile.Observations)
    $profile.AvgDeltaMB = Update-LearningAverage -OldValue ([double]$profile.AvgDeltaMB) -NewValue ([double]$delta) -ObservationCount ([int]$profile.Observations)
    $profile.AvgPressureScore = Update-LearningAverage -OldValue ([double]$profile.AvgPressureScore) -NewValue ([double]$pressureScore) -ObservationCount ([int]$profile.Observations)
    $bias = 0
    if ([int]$profile.Observations -ge $gameProfileMinObservations -and [double]$profile.AvgPressureScore -ge 1.4 -and [double]$profile.AvgDeltaMB -ge 120.0) { $bias = 1 }
    if ([int]$profile.Observations -ge ($gameProfileMinObservations + 2) -and [double]$profile.AvgPressureScore -ge 2.0 -and [double]$profile.AvgDeltaMB -ge 260.0) { $bias = 2 }
    $profile.AggressionBias = $bias
    $profile.LastSeen = $now
    $script:gameProfileMap[$key] = $profile
    Save-GameProfileMap -Map $script:gameProfileMap
}

function Read-LearningMap {
    $map = @{}
    if (-not $smartLearning) { return $map }

    foreach ($item in @(Read-StateArray -Path $learningStatePath)) {
        if (-not $item.Key) { continue }
        $key = [string]$item.Key
        $map[$key] = [pscustomobject]@{
            Key = $key
            ProcessName = [string]$item.ProcessName
            Path = [string]$item.Path
            Observations = [int]$item.Observations
            WakeCount = [int]$item.WakeCount
            PressureHitCount = [int]$item.PressureHitCount
            TrimOkCount = [int]$item.TrimOkCount
            CooldownCount = [int]$item.CooldownCount
            DeepCount = [int]$item.DeepCount
            BalancedCount = [int]$item.BalancedCount
            LightCount = [int]$item.LightCount
            AvgWorkingSetMB = [double]$item.AvgWorkingSetMB
            AvgCpuPercent = [double]$item.AvgCpuPercent
            AvgDeltaMB = [double]$item.AvgDeltaMB
            AvgBurstCount = [double]$item.AvgBurstCount
            Aggression = [int]$item.Aggression
            PreferredTier = [string]$item.PreferredTier
            FastWake = [bool]$item.FastWake
            LastReason = [string]$item.LastReason
            LastSession = [string]$item.LastSession
            LastSeen = [string]$item.LastSeen
            LastWakeAt = [string]$item.LastWakeAt
        }
    }
    return $map
}

function Save-LearningMap {
    param([hashtable]$Map)

    if (-not $smartLearning) { return }
    $items = @($Map.Values |
        Sort-Object @{ Expression = { if ($_.LastSeen) { [string]$_.LastSeen } else { "" } }; Descending = $true } |
        Select-Object -First $learningMaxProfiles)
    Write-StateArray -Path $learningStatePath -Items $items
}

function New-LearningProfile {
    param(
        [string]$Key,
        [string]$ProcessName,
        [string]$Path
    )

    [pscustomobject]@{
        Key = $Key
        ProcessName = $ProcessName
        Path = $Path
        Observations = 0
        WakeCount = 0
        PressureHitCount = 0
        TrimOkCount = 0
        CooldownCount = 0
        DeepCount = 0
        BalancedCount = 0
        LightCount = 0
        AvgWorkingSetMB = 0.0
        AvgCpuPercent = 0.0
        AvgDeltaMB = 0.0
        AvgBurstCount = 0.0
        Aggression = 0
        PreferredTier = "Balanced"
        FastWake = $false
        LastReason = "new"
        LastSession = ""
        LastSeen = (Get-Date).ToString("o")
        LastWakeAt = ""
    }
}

function Update-LearningAverage {
    param(
        [double]$OldValue,
        [double]$NewValue,
        [int]$ObservationCount
    )

    if ($ObservationCount -le 1) { return [math]::Round($NewValue, 2) }
    return [math]::Round(($OldValue * 0.72) + ($NewValue * 0.28), 2)
}

function Read-BehaviorMap {
    $map = @{}
    if (-not $behaviorEngine) { return $map }

    foreach ($item in @(Read-StateArray -Path $behaviorStatePath)) {
        if (-not $item.Key) { continue }
        $key = [string]$item.Key
        $map[$key] = [pscustomobject]@{
            Key = $key
            ProcessName = [string]$item.ProcessName
            Path = [string]$item.Path
            Observations = [int]$item.Observations
            TargetCount = [int]$item.TargetCount
            TrimCount = [int]$item.TrimCount
            RefaultCount = [int]$item.RefaultCount
            CooldownCount = [int]$item.CooldownCount
            WakeCount = [int]$item.WakeCount
            AvgWorkingSetMB = [double]$item.AvgWorkingSetMB
            AvgPrivateMemoryMB = [double]$item.AvgPrivateMemoryMB
            AvgCpuPercent = [double]$item.AvgCpuPercent
            AvgBurstCount = [double]$item.AvgBurstCount
            AvgHandleCount = [double]$item.AvgHandleCount
            AvgThreadCount = [double]$item.AvgThreadCount
            AvgTrimDeltaMB = [double]$item.AvgTrimDeltaMB
            AvgRefaultMB = [double]$item.AvgRefaultMB
            WakeBias = [int]$item.WakeBias
            Confidence = [int]$item.Confidence
            AggressionBias = [int]$item.AggressionBias
            PreferredTier = [string]$item.PreferredTier
            LastReason = [string]$item.LastReason
            LastSeen = [string]$item.LastSeen
            LastWakeAt = [string]$item.LastWakeAt
        }
    }
    return $map
}

function Save-BehaviorMap {
    param([hashtable]$Map)

    if (-not $behaviorEngine) { return }
    $items = @($Map.Values |
        Sort-Object @{ Expression = { if ($_.LastSeen) { [string]$_.LastSeen } else { "" } }; Descending = $true } |
        Select-Object -First $behaviorMaxProfiles)
    Write-StateArray -Path $behaviorStatePath -Items $items
}

function New-BehaviorProfile {
    param(
        [string]$Key,
        [string]$ProcessName,
        [string]$Path
    )

    [pscustomobject]@{
        Key = $Key
        ProcessName = $ProcessName
        Path = $Path
        Observations = 0
        TargetCount = 0
        TrimCount = 0
        RefaultCount = 0
        CooldownCount = 0
        WakeCount = 0
        AvgWorkingSetMB = 0.0
        AvgPrivateMemoryMB = 0.0
        AvgCpuPercent = 0.0
        AvgBurstCount = 0.0
        AvgHandleCount = 0.0
        AvgThreadCount = 0.0
        AvgTrimDeltaMB = 0.0
        AvgRefaultMB = 0.0
        WakeBias = 0
        Confidence = 0
        AggressionBias = 0
        PreferredTier = "Balanced"
        LastReason = "new"
        LastSeen = (Get-Date).ToString("o")
        LastWakeAt = ""
    }
}

function Get-BehaviorProfile {
    param(
        [string]$ProcessName,
        [string]$Path
    )

    if (-not $behaviorEngine) { return $null }
    $key = Get-AppIdentityKeyFromText -ProcessName $ProcessName -Path $Path
    if ($key -and $script:behaviorMap.ContainsKey($key)) {
        return $script:behaviorMap[$key]
    }
    return $null
}

function Resolve-BehaviorPreference {
    param([object]$Profile)

    if (-not $Profile) { return }
    $observations = [int]$Profile.Observations
    $targets = [int]$Profile.TargetCount
    $trimCount = [int]$Profile.TrimCount
    $refaultCount = [int]$Profile.RefaultCount
    $wakeCount = [int]$Profile.WakeCount
    $avgCpu = [double]$Profile.AvgCpuPercent
    $avgWorkingSet = [double]$Profile.AvgWorkingSetMB
    $avgDelta = [double]$Profile.AvgTrimDeltaMB
    $avgRefault = [double]$Profile.AvgRefaultMB
    $avgBursts = [double]$Profile.AvgBurstCount

    $confidence = [math]::Min(100, ($observations * 10) + ($targets * 3) + ($trimCount * 4) + ($refaultCount * 10) + ($wakeCount * 10))
    if ($observations -lt $behaviorMinObservations) { $confidence = [math]::Min($confidence, 54) }
    if ($wakeCount -ge 2) { $confidence = [math]::Max($confidence, 68) }

    $wakeBias = [math]::Min(100, ($wakeCount * 18) + ([int][math]::Round(($avgRefault / [math]::Max(1.0, $behaviorRefaultPenaltyMB)) * 42.0)))
    $bias = 0
    $tier = "Balanced"
    $reason = "behavior-learning"

    if ($wakeCount -ge 2) {
        $bias = -2
        $tier = "Light"
        $reason = "behavior-fast-wake"
    } elseif ($observations -ge $behaviorMinObservations -and $avgRefault -ge $behaviorRefaultPenaltyMB) {
        $bias = -2
        $tier = "Light"
        $reason = "behavior-refault-guard"
    } elseif ($observations -ge $behaviorMinObservations -and ($avgRefault -ge ($behaviorRefaultPenaltyMB * 0.55))) {
        $bias = -1
        $tier = "Light"
        $reason = "behavior-refault-guard"
    } elseif ($observations -ge $behaviorMinObservations -and $avgWorkingSet -ge $deepNapMinimumMB -and $avgCpu -le $behaviorStableCpuPercent -and $avgBursts -le 1.2 -and $avgDelta -ge $behaviorEfficientDeltaMB -and $avgRefault -le ($behaviorRefaultPenaltyMB * 0.35)) {
        $bias = 2
        $tier = "Deep"
        $reason = "behavior-proven-idle"
    } elseif ($observations -ge $behaviorMinObservations -and $avgWorkingSet -ge $balancedNapMinimumMB -and $avgCpu -le $balancedNapMaxCpuPercent -and $avgDelta -ge ($behaviorEfficientDeltaMB * 0.35)) {
        $bias = 1
        $tier = "Balanced"
        $reason = "behavior-proven-steady"
    }

    $Profile.WakeBias = [int]$wakeBias
    $Profile.Confidence = [int]$confidence
    $Profile.AggressionBias = [int]$bias
    $Profile.PreferredTier = $tier
    $Profile.LastReason = $reason
}

function Update-BehaviorProfiles {
    param(
        [array]$Rows,
        [array]$Results
    )

    if (-not $behaviorEngine) { return }
    $now = (Get-Date).ToString("o")
    $resultById = @{}
    foreach ($result in @($Results)) {
        if ($result.Id -ne $null) { $resultById[[int]$result.Id] = $result }
    }

    $groups = @{}
    foreach ($row in @($Rows | Where-Object { $_.Candidate -or $resultById.ContainsKey([int]$_.Id) })) {
        $key = Get-AppIdentityKeyFromText -ProcessName ([string]$row.ProcessName) -Path ([string]$row.Path)
        if (-not $key) { continue }
        if (-not $groups.ContainsKey($key)) {
            $groups[$key] = [pscustomobject]@{
                Key = $key
                ProcessName = [string]$row.ProcessName
                Path = [string]$row.Path
                RowCount = 0
                WorkingSetMB = 0.0
                PrivateMemoryMB = 0.0
                CpuPercent = 0.0
                BurstCount = 0
                HandleCount = 0
                ThreadCount = 0
                Targeted = $false
                Trimmed = $false
                Cooldown = $false
                DeltaMB = 0.0
                RefaultMB = 0.0
            }
        }
        $group = $groups[$key]
        $group.RowCount = [int]$group.RowCount + 1
        $group.WorkingSetMB = [double]$group.WorkingSetMB + [double]$row.WorkingSetMB
        $group.PrivateMemoryMB = [double]$group.PrivateMemoryMB + [double]$row.PrivateMemoryMB
        $group.CpuPercent = [double]$group.CpuPercent + [double]$row.CpuPercent
        $group.BurstCount = [int]$group.BurstCount + [int]$row.BurstCount
        $group.HandleCount = [int]$group.HandleCount + [int]$row.HandleCount
        $group.ThreadCount = [int]$group.ThreadCount + [int]$row.ThreadCount

        if ($resultById.ContainsKey([int]$row.Id)) {
            $result = $resultById[[int]$row.Id]
            $group.Targeted = $true
            if ([string]$result.TrimWorkingSet -eq "OK") { $group.Trimmed = $true }
            if ([string]$result.TrimWorkingSet -eq "Cooldown") { $group.Cooldown = $true }

            if ($result.WorkingSetBeforeMB -ne $null -and $result.WorkingSetAfterMB -ne $null) {
                $delta = [double]$result.WorkingSetBeforeMB - [double]$result.WorkingSetAfterMB
                if ($delta -gt 0) { $group.DeltaMB = [double]$group.DeltaMB + $delta }
            }

            if ($result.WorkingSetAfterMB -ne $null) {
                $current = Get-Process -Id ([int]$row.Id) -ErrorAction SilentlyContinue
                if ($current) {
                    $currentMB = [math]::Round($current.WorkingSet64 / 1MB, 1)
                    $refault = [double]$currentMB - [double]$result.WorkingSetAfterMB
                    if ($refault -gt 0) { $group.RefaultMB = [double]$group.RefaultMB + $refault }
                }
            }
        }
    }

    foreach ($group in @($groups.Values)) {
        $key = [string]$group.Key
        $profile = $null
        if ($script:behaviorMap.ContainsKey($key)) {
            $profile = $script:behaviorMap[$key]
        } else {
            $profile = New-BehaviorProfile -Key $key -ProcessName ([string]$group.ProcessName) -Path ([string]$group.Path)
        }

        $profile.ProcessName = [string]$group.ProcessName
        $profile.Path = [string]$group.Path
        $profile.Observations = [int]$profile.Observations + 1
        $profile.AvgWorkingSetMB = Update-LearningAverage -OldValue ([double]$profile.AvgWorkingSetMB) -NewValue ([double]$group.WorkingSetMB) -ObservationCount ([int]$profile.Observations)
        $profile.AvgPrivateMemoryMB = Update-LearningAverage -OldValue ([double]$profile.AvgPrivateMemoryMB) -NewValue ([double]$group.PrivateMemoryMB) -ObservationCount ([int]$profile.Observations)
        $profile.AvgCpuPercent = Update-LearningAverage -OldValue ([double]$profile.AvgCpuPercent) -NewValue ([double]$group.CpuPercent) -ObservationCount ([int]$profile.Observations)
        $profile.AvgBurstCount = Update-LearningAverage -OldValue ([double]$profile.AvgBurstCount) -NewValue ([double]$group.BurstCount) -ObservationCount ([int]$profile.Observations)
        $profile.AvgHandleCount = Update-LearningAverage -OldValue ([double]$profile.AvgHandleCount) -NewValue ([double]$group.HandleCount) -ObservationCount ([int]$profile.Observations)
        $profile.AvgThreadCount = Update-LearningAverage -OldValue ([double]$profile.AvgThreadCount) -NewValue ([double]$group.ThreadCount) -ObservationCount ([int]$profile.Observations)

        if ([bool]$group.Targeted) {
            $profile.TargetCount = [int]$profile.TargetCount + 1
            if ([bool]$group.Trimmed) { $profile.TrimCount = [int]$profile.TrimCount + 1 }
            if ([bool]$group.Cooldown) { $profile.CooldownCount = [int]$profile.CooldownCount + 1 }
            $profile.AvgTrimDeltaMB = Update-LearningAverage -OldValue ([double]$profile.AvgTrimDeltaMB) -NewValue ([double]$group.DeltaMB) -ObservationCount ([int]$profile.TargetCount)
            if ([double]$group.RefaultMB -ge $behaviorRefaultPenaltyMB) { $profile.RefaultCount = [int]$profile.RefaultCount + 1 }
            $profile.AvgRefaultMB = Update-LearningAverage -OldValue ([double]$profile.AvgRefaultMB) -NewValue ([double]$group.RefaultMB) -ObservationCount ([int]$profile.TargetCount)
        }

        Resolve-BehaviorPreference -Profile $profile
        $profile.LastSeen = $now
        $script:behaviorMap[$key] = $profile
    }

    foreach ($result in @($Results)) {
        $key = Get-AppIdentityKeyFromText -ProcessName ([string]$result.ProcessName) -Path ([string]$result.Path)
        if (-not $key -or -not $script:behaviorMap.ContainsKey($key)) { continue }
        $profile = $script:behaviorMap[$key]
        $result.BehaviorObservations = [int]$profile.Observations
        $result.BehaviorWakeCount = [int]$profile.WakeCount
        $result.BehaviorConfidence = [int]$profile.Confidence
        $result.BehaviorBias = [int]$profile.AggressionBias
        $result.BehaviorPreferredTier = [string]$profile.PreferredTier
        $result.BehaviorReason = [string]$profile.LastReason
        $result.BehaviorAvgRefaultMB = [double]$profile.AvgRefaultMB
        $result.BehaviorAvgTrimDeltaMB = [double]$profile.AvgTrimDeltaMB
    }

    Save-BehaviorMap -Map $script:behaviorMap
}

function Add-BehaviorWake {
    param(
        [System.Diagnostics.Process]$Process,
        [string]$Path
    )

    if (-not $behaviorEngine -or -not $Process) { return }
    $key = Get-AppIdentityKeyFromText -ProcessName $Process.ProcessName -Path $Path
    if (-not $key) { return }
    $profile = $null
    if ($script:behaviorMap.ContainsKey($key)) {
        $profile = $script:behaviorMap[$key]
    } else {
        $profile = New-BehaviorProfile -Key $key -ProcessName $Process.ProcessName -Path $Path
    }
    $profile.ProcessName = $Process.ProcessName
    $profile.Path = $Path
    $profile.WakeCount = [int]$profile.WakeCount + 1
    $profile.LastWakeAt = (Get-Date).ToString("o")
    $profile.LastSeen = (Get-Date).ToString("o")
    Resolve-BehaviorPreference -Profile $profile
    $script:behaviorMap[$key] = $profile
    Save-BehaviorMap -Map $script:behaviorMap
}

function Get-SystemMemoryPressure {
    $totalMB = -1.0
    $freeMB = -1.0
    try {
        $os = Get-CimInstance -ClassName Win32_OperatingSystem -ErrorAction Stop
        $totalMB = [double]$os.TotalVisibleMemorySize / 1024.0
        $freeMB = [double]$os.FreePhysicalMemory / 1024.0
    } catch {
        try {
            Add-Type -AssemblyName Microsoft.VisualBasic -ErrorAction Stop
            $info = [Microsoft.VisualBasic.Devices.ComputerInfo]::new()
            $totalMB = [double]$info.TotalPhysicalMemory / 1MB
            $freeMB = [double]$info.AvailablePhysicalMemory / 1MB
        } catch {
            return [pscustomobject]@{ Level = "Unknown"; FreeMB = -1.0; UsedPercent = -1.0 }
        }
    }

    $usedPercent = if ($totalMB -gt 0) { (($totalMB - $freeMB) / $totalMB) * 100.0 } else { -1.0 }
    $level = "Normal"
    $moderateThreshold = [math]::Max([double]$moderateFreeMemoryMB, [double]$learningElevatedFreeMemoryMB)
    $elevatedThreshold = [math]::Max([double]$elevatedFreeMemoryMB, [double]$learningAggressiveFreeMemoryMB)
    $criticalThreshold = [double]$criticalFreeMemoryMB
    if ($freeMB -le $criticalThreshold -or $usedPercent -ge 88.0) {
        $level = "Critical"
    } elseif ($freeMB -le $elevatedThreshold -or $usedPercent -ge 78.0) {
        $level = "Elevated"
    } elseif ($freeMB -le $moderateThreshold -or $usedPercent -ge 70.0) {
        $level = "Moderate"
    }
    return [pscustomobject]@{
        Level = $level
        FreeMB = [math]::Round($freeMB, 1)
        UsedPercent = [math]::Round($usedPercent, 1)
    }
}

function Get-LearningSessionContext {
    param(
        [object]$Foreground,
        [object]$Pressure
    )

    $name = ""
    $kind = "Desktop"
    if ($Foreground -and $Foreground.ProcessName) {
        $name = [string]$Foreground.ProcessName
        if ([bool]$Foreground.IsFullscreen) {
            $kind = "Fullscreen"
        }
    }
    if ($Pressure -and ([string]$Pressure.Level -in @("Elevated", "Critical"))) {
        if ($kind -eq "Desktop") { $kind = "MemoryPressure" }
    }
    return [pscustomobject]@{
        Name = $name
        Kind = $kind
        Pressure = if ($Pressure) { [string]$Pressure.Level } else { "Unknown" }
    }
}

function Get-IntentContext {
    param(
        [object]$Foreground,
        [object]$Pressure,
        [array]$Processes,
        [hashtable]$CpuMap
    )

    if (-not $intentEngine) {
        return [pscustomobject]@{ Kind = "Desktop"; Name = ""; Path = ""; Confidence = 0; Signals = @("disabled"); Foreground = ""; ForegroundPid = 0 }
    }

    $signals = @()
    $kind = "Desktop"
    $name = if ($Foreground -and $Foreground.ProcessName) { [string]$Foreground.ProcessName } else { "" }
    $path = if ($Foreground -and $Foreground.Path) { [string]$Foreground.Path } else { "" }
    $confidence = 20

    $fgRole = Get-ProcessRole -ProcessName $name -Path $path
    if ($Foreground -and [bool]$Foreground.IsFullscreen) {
        $signals += "fullscreen"
        $confidence += 26
        if ($fgRole -eq "GameCandidate") {
            $signals += "known-game-path"
            $confidence += 38
            $kind = "Gaming"
        } elseif ($fgRole -notin @("Browser", "Communication", "Media") -and -not (Test-PathContainsFragment -Path $path -Fragments @("\Windows\", "\Program Files\WindowsApps\"))) {
            $signals += "exclusive-foreground"
            $confidence += 24
            $kind = "Gaming"
        }
    }

    if ($fgRole -in @("Communication", "Media")) {
        $signals += ("foreground-" + $fgRole.ToLowerInvariant())
        $confidence = [math]::Max($confidence, 72)
        $kind = "MediaCall"
    }

    $launcherActivity = 0
    $mediaActivity = 0
    foreach ($p in @($Processes)) {
        $role = Get-ProcessRole -ProcessName $p.ProcessName -Path (Get-ProcessPathText -Process $p)
        $cpu = 0.0
        if ($CpuMap -and $CpuMap.ContainsKey([int]$p.Id)) { $cpu = [double]$CpuMap[[int]$p.Id] }
        if ($role -eq "Launcher" -and $cpu -ge 0.35) { $launcherActivity++ }
        if ($role -in @("Communication", "Media") -and $cpu -ge 0.15) { $mediaActivity++ }
    }
    if ($kind -eq "Desktop" -and $launcherActivity -gt 0) {
        $kind = "DownloadInstall"
        $signals += "launcher-activity"
        $confidence = [math]::Max($confidence, 66)
    }
    if ($kind -eq "Desktop" -and $mediaActivity -gt 0) {
        $kind = "MediaCall"
        $signals += "media-activity"
        $confidence = [math]::Max($confidence, 64)
    }
    if ($Pressure -and [string]$Pressure.Level -in @("Moderate", "Elevated", "Critical")) {
        $signals += ("memory-" + ([string]$Pressure.Level).ToLowerInvariant())
        if ($kind -eq "Desktop") { $kind = "MemoryPressure" }
        if ([string]$Pressure.Level -eq "Moderate") { $confidence = [math]::Max($confidence, 56) }
        if ([string]$Pressure.Level -eq "Elevated") { $confidence = [math]::Max($confidence, 70) }
        if ([string]$Pressure.Level -eq "Critical") { $confidence = [math]::Max($confidence, 84) }
    }
    if ($kind -eq "Gaming" -and $confidence -lt $intentMinConfidence) {
        $kind = "Desktop"
        $signals += "gaming-confidence-below-threshold"
    }
    if ($confidence -gt 100) { $confidence = 100 }

    [pscustomobject]@{
        Kind = $kind
        Name = $name
        Path = $path
        Confidence = [int]$confidence
        Signals = @($signals | Select-Object -Unique)
        Foreground = $name
        ForegroundPid = if ($Foreground) { [int]$Foreground.Id } else { 0 }
    }
}

function Get-GuardDecision {
    param(
        [string]$ProcessName,
        [string]$Path,
        [string]$Role,
        [double]$CpuPercent,
        [int]$BurstCount,
        [object]$Foreground,
        [object]$SwitchProfile
    )

    $isForeground = $Foreground -and $Foreground.ProcessName -and ($ProcessName -ieq [string]$Foreground.ProcessName)
    if ($Path -and $Foreground -and $Foreground.Path) {
        $isForeground = $isForeground -or $Path.Equals([string]$Foreground.Path, [System.StringComparison]::OrdinalIgnoreCase)
    }
    $fastWake = Test-ForegroundSwitchProtected -Profile $SwitchProfile
    $protect = $false
    $reason = ""
    $confidence = 0

    if ($mediaCallProtection -and $Role -in @("Communication", "Media")) {
        if ($isForeground -or $fastWake -or $CpuPercent -ge 0.15 -or $BurstCount -gt 0) {
            $protect = $true
            $reason = if ($Role -eq "Communication") { "MediaCallGuard" } else { "MediaGuard" }
            $confidence = 65
            if ($isForeground) { $confidence += 20 }
            if ($fastWake) { $confidence += 12 }
            if ($CpuPercent -ge 0.5) { $confidence += 8 }
        }
    }

    if (-not $protect -and $downloadLauncherGuard -and $Role -eq "Launcher") {
        if ($CpuPercent -ge 0.35 -or $BurstCount -gt 0 -or $fastWake) {
            $protect = $true
            $reason = "LauncherActivityGuard"
            $confidence = 62
            if ($CpuPercent -ge 1.0) { $confidence += 14 }
            if ($BurstCount -gt 0) { $confidence += 10 }
            if ($fastWake) { $confidence += 8 }
        }
    }

    if ($confidence -gt 100) { $confidence = 100 }
    [pscustomobject]@{
        Protect = $protect
        Reason = $reason
        Confidence = [int]$confidence
        FastWake = [bool]$fastWake
    }
}

function Get-EffectiveMaxTargets {
    $max = [int]$maxTargetsPerPass
    if (-not $memoryPressureGovernor) { return $max }
    switch ([string]$script:currentMemoryPressure.Level) {
        "Moderate" { return [math]::Min(100, $max + 10) }
        "Elevated" { return [math]::Min(120, $max + 22) }
        "Critical" { return [math]::Min(140, $max + 35) }
        default { return $max }
    }
}

function Get-PressureTrimMinimum {
    param(
        [double]$BaseMinimum,
        [string]$Tier
    )

    if (-not $memoryPressureGovernor) { return $BaseMinimum }
    $factor = 1.0
    switch ([string]$script:currentMemoryPressure.Level) {
        "Moderate" { $factor = 0.90 }
        "Elevated" { $factor = 0.76 }
        "Critical" { $factor = 0.62 }
    }
    if ($Tier -eq "Light") { $factor = [math]::Max($factor, 0.82) }
    return [math]::Max(12.0, [math]::Round($BaseMinimum * $factor, 1))
}

function Write-IntentState {
    if (-not $intentEngine -or -not $script:currentIntent) { return }
    Write-StateObject -Path $intentStatePath -Value ([pscustomobject]@{
        Timestamp = (Get-Date).ToString("o")
        Kind = [string]$script:currentIntent.Kind
        Name = [string]$script:currentIntent.Name
        Path = [string]$script:currentIntent.Path
        Confidence = [int]$script:currentIntent.Confidence
        Signals = @($script:currentIntent.Signals)
        MemoryPressure = [string]$script:currentMemoryPressure.Level
        FreeMemoryMB = [double]$script:currentMemoryPressure.FreeMB
    })
}

function Write-ContentionRadar {
    param(
        [array]$Rows,
        [array]$Results
    )

    if (-not $contentionRadar) { return }
    $resultById = @{}
    foreach ($r in @($Results)) { $resultById[[int]$r.Id] = $r }
    $items = @()
    foreach ($row in @($Rows)) {
        $severity = 0.0
        $reason = @()
        if ([double]$row.CpuPercent -ge 1.0) { $severity += ([double]$row.CpuPercent * 8.0); $reason += "cpu" }
        if ([double]$row.WorkingSetMB -ge 300.0) { $severity += ([double]$row.WorkingSetMB / 16.0); $reason += "memory" }
        if ([int]$row.BurstCount -gt 0) { $severity += ([int]$row.BurstCount * 8.0); $reason += "bursts" }
        if ($row.GuardReason) { $severity *= 0.72; $reason += [string]$row.GuardReason }
        if ($resultById.ContainsKey([int]$row.Id)) { $reason += "managed" }
        if ($severity -lt 12.0) { continue }
        $items += [pscustomobject]@{
            Id = [int]$row.Id
            ProcessName = [string]$row.ProcessName
            Role = [string]$row.Role
            Severity = [math]::Round($severity, 1)
            CpuPercent = [double]$row.CpuPercent
            WorkingSetMB = [double]$row.WorkingSetMB
            BurstCount = [int]$row.BurstCount
            Candidate = [bool]$row.Candidate
            GuardReason = [string]$row.GuardReason
            Reason = @($reason | Select-Object -Unique)
        }
    }
    $items = @($items | Sort-Object Severity -Descending | Select-Object -First 18)
    Write-StateObject -Path $radarStatePath -Depth 7 -Value ([pscustomobject]@{
        Timestamp = (Get-Date).ToString("o")
        IntentKind = [string]$script:currentIntent.Kind
        IntentConfidence = [int]$script:currentIntent.Confidence
        MemoryPressure = [string]$script:currentMemoryPressure.Level
        Items = $items
    })
}

function Resolve-LearningPreference {
    param([object]$Profile)

    if (-not $Profile) { return "Balanced" }
    $fastWake = [bool]$Profile.FastWake -or ([int]$Profile.WakeCount -ge $learningFastWakeThreshold)
    if ($fastWake) { return "Light" }
    if ([int]$Profile.Observations -lt $learningMinObservations) { return "Balanced" }

    $aggression = 0
    if ([double]$Profile.AvgWorkingSetMB -ge $balancedNapMinimumMB) { $aggression = 1 }
    if ([double]$Profile.AvgWorkingSetMB -ge $deepNapMinimumMB -and [double]$Profile.AvgCpuPercent -le [math]::Max(1.0, $deepNapMaxCpuPercent) -and [double]$Profile.AvgDeltaMB -ge 10.0) { $aggression = 2 }
    if ([double]$Profile.AvgWorkingSetMB -ge 420.0 -and [double]$Profile.AvgCpuPercent -le 0.6 -and [int]$Profile.PressureHitCount -gt 0) { $aggression = 3 }

    $Profile.Aggression = $aggression
    if ($aggression -ge 2) { return "Deep" }
    if ($aggression -eq 1) { return "Balanced" }
    return "Light"
}

function Update-LearningProfiles {
    param([array]$Results)

    if (-not $smartLearning) { return }
    $pressureLevel = [string]$script:currentMemoryPressure.Level
    foreach ($result in @($Results)) {
        $key = Get-LearningKeyFromText -ProcessName ([string]$result.ProcessName) -Path ([string]$result.Path)
        if (-not $key) { continue }
        $profile = $null
        if ($script:learningMap.ContainsKey($key)) {
            $profile = $script:learningMap[$key]
        } else {
            $profile = New-LearningProfile -Key $key -ProcessName ([string]$result.ProcessName) -Path ([string]$result.Path)
        }

        $profile.ProcessName = [string]$result.ProcessName
        $profile.Path = [string]$result.Path
        $profile.Observations = [int]$profile.Observations + 1
        $profile.AvgWorkingSetMB = Update-LearningAverage -OldValue ([double]$profile.AvgWorkingSetMB) -NewValue ([double]$result.WorkingSetBeforeMB) -ObservationCount ([int]$profile.Observations)
        $profile.AvgCpuPercent = Update-LearningAverage -OldValue ([double]$profile.AvgCpuPercent) -NewValue ([double]$result.CpuPercent) -ObservationCount ([int]$profile.Observations)
        $deltaForLearning = 0.0
        if ($result.WorkingSetBeforeMB -ne $null -and $result.WorkingSetAfterMB -ne $null) {
            $deltaForLearning = [double]$result.WorkingSetBeforeMB - [double]$result.WorkingSetAfterMB
            if ($deltaForLearning -lt 0) { $deltaForLearning = 0.0 }
        }
        $profile.AvgDeltaMB = Update-LearningAverage -OldValue ([double]$profile.AvgDeltaMB) -NewValue $deltaForLearning -ObservationCount ([int]$profile.Observations)
        if ([double]$profile.AvgDeltaMB -lt 0) { $profile.AvgDeltaMB = 0.0 }
        $profile.AvgBurstCount = Update-LearningAverage -OldValue ([double]$profile.AvgBurstCount) -NewValue ([double]$result.BurstCount) -ObservationCount ([int]$profile.Observations)

        if ([string]$result.TrimWorkingSet -eq "OK") { $profile.TrimOkCount = [int]$profile.TrimOkCount + 1 }
        if ([string]$result.TrimWorkingSet -eq "Cooldown") { $profile.CooldownCount = [int]$profile.CooldownCount + 1 }
        if ([string]$result.NapTier -eq "Deep") { $profile.DeepCount = [int]$profile.DeepCount + 1 }
        if ([string]$result.NapTier -eq "Balanced") { $profile.BalancedCount = [int]$profile.BalancedCount + 1 }
        if ([string]$result.NapTier -eq "Light") { $profile.LightCount = [int]$profile.LightCount + 1 }
        if ($pressureLevel -in @("Elevated", "Critical")) { $profile.PressureHitCount = [int]$profile.PressureHitCount + 1 }

        $profile.FastWake = ([int]$profile.WakeCount -ge $learningFastWakeThreshold)
        $profile.PreferredTier = Resolve-LearningPreference -Profile $profile
        $profile.LastReason = [string]$result.Decision
        $profile.LastSession = if ($script:currentLearningSession.Name) { ([string]$script:currentLearningSession.Kind + ":" + [string]$script:currentLearningSession.Name) } else { [string]$script:currentLearningSession.Kind }
        $profile.LastSeen = (Get-Date).ToString("o")
        $script:learningMap[$key] = $profile
    }
    Save-LearningMap -Map $script:learningMap
}

function Add-LearningWake {
    param(
        [System.Diagnostics.Process]$Process,
        [string]$Path
    )

    if (-not $smartLearning -or -not $Process) { return }
    $key = Get-LearningKeyFromText -ProcessName $Process.ProcessName -Path $Path
    if (-not $key) { return }
    $profile = $null
    if ($script:learningMap.ContainsKey($key)) {
        $profile = $script:learningMap[$key]
    } else {
        $profile = New-LearningProfile -Key $key -ProcessName $Process.ProcessName -Path $Path
    }
    $profile.WakeCount = [int]$profile.WakeCount + 1
    $profile.FastWake = ([int]$profile.WakeCount -ge $learningFastWakeThreshold)
    if ($profile.FastWake) {
        $profile.PreferredTier = "Light"
        $profile.Aggression = 0
        $profile.LastReason = "learned-fast-wake"
    }
    $profile.LastWakeAt = (Get-Date).ToString("o")
    $profile.LastSeen = (Get-Date).ToString("o")
    $script:learningMap[$key] = $profile
    Save-LearningMap -Map $script:learningMap
}

function Read-TemporaryProtectMap {
    $map = @{}
    $now = Get-Date
    foreach ($item in @(Read-StateArray -Path $protectStatePath)) {
        if (-not $item.Key -or -not $item.ExpiresAt) { continue }
        try {
            $expires = [DateTime]::Parse([string]$item.ExpiresAt, $null, [Globalization.DateTimeStyles]::RoundtripKind)
            if ($expires -gt $now) {
                $map[[string]$item.Key] = [pscustomobject]@{
                    Key = [string]$item.Key
                    ProcessName = [string]$item.ProcessName
                    Path = [string]$item.Path
                    Reason = [string]$item.Reason
                    ExpiresAt = $expires.ToString("o")
                }
            }
        } catch {
        }
    }
    return $map
}

function Save-TemporaryProtectMap {
    param([hashtable]$Map)
    Write-StateArray -Path $protectStatePath -Items @($Map.Values)
}

function Add-TemporaryProtection {
    param(
        [hashtable]$Map,
        [System.Diagnostics.Process]$Process,
        [string]$Path,
        [string]$Reason,
        [int]$Minutes
    )

    if (-not $smartAutoProtect -or -not $Process) { return }
    $key = Get-ProcessIdentityKey -Process $Process -Path $Path
    $expires = (Get-Date).AddMinutes($Minutes)
    $existing = $Map[$key]
    if ($existing -and $existing.ExpiresAt) {
        try {
            $existingExpires = [DateTime]::Parse([string]$existing.ExpiresAt, $null, [Globalization.DateTimeStyles]::RoundtripKind)
            if ($existingExpires -gt $expires) {
                $expires = $existingExpires
            }
        } catch {
        }
    }

    $Map[$key] = [pscustomobject]@{
        Key = $key
        ProcessName = $Process.ProcessName
        Path = $Path
        Reason = $Reason
        ExpiresAt = $expires.ToString("o")
    }
}

function Test-TemporaryProtected {
    param(
        [hashtable]$Map,
        [System.Diagnostics.Process]$Process,
        [string]$Path
    )

    if (-not $smartAutoProtect -or -not $Process) { return $false }
    $key = Get-ProcessIdentityKey -Process $Process -Path $Path
    return $Map.ContainsKey($key)
}

function Read-BurstMap {
    $map = @{}
    $cutoff = (Get-Date).AddMinutes(-1 * $burstWindowMinutes)
    foreach ($item in @(Read-StateArray -Path $burstStatePath)) {
        if (-not $item.Key -or -not $item.SeenAt) { continue }
        try {
            $seen = [DateTime]::Parse([string]$item.SeenAt, $null, [Globalization.DateTimeStyles]::RoundtripKind)
            if ($seen -ge $cutoff) {
                if (-not $map.ContainsKey([string]$item.Key)) {
                    $map[[string]$item.Key] = New-Object System.Collections.ArrayList
                }
                [void]$map[[string]$item.Key].Add([pscustomobject]@{
                    Key = [string]$item.Key
                    ProcessName = [string]$item.ProcessName
                    Path = [string]$item.Path
                    CpuPercent = [double]$item.CpuPercent
                    SeenAt = $seen.ToString("o")
                })
            }
        } catch {
        }
    }
    return $map
}

function Save-BurstMap {
    param([hashtable]$Map)
    $items = @()
    foreach ($list in $Map.Values) {
        $items += @($list)
    }
    Write-StateArray -Path $burstStatePath -Items $items
}

function Add-BurstObservation {
    param(
        [hashtable]$Map,
        [System.Diagnostics.Process]$Process,
        [string]$Path,
        [double]$CpuPercent
    )

    if (-not $smartBurstWatcher -or -not $Process) { return }
    if ($CpuPercent -lt $burstCpuThreshold) { return }
    $key = Get-ProcessIdentityKey -Process $Process -Path $Path
    if (-not $Map.ContainsKey($key)) {
        $Map[$key] = New-Object System.Collections.ArrayList
    }
    [void]$Map[$key].Add([pscustomobject]@{
        Key = $key
        ProcessName = $Process.ProcessName
        Path = $Path
        CpuPercent = $CpuPercent
        SeenAt = (Get-Date).ToString("o")
    })
}

function Get-BurstCount {
    param(
        [hashtable]$Map,
        [System.Diagnostics.Process]$Process,
        [string]$Path
    )

    if (-not $smartBurstWatcher -or -not $Process) { return 0 }
    $key = Get-ProcessIdentityKey -Process $Process -Path $Path
    if (-not $Map.ContainsKey($key)) { return 0 }
    return @($Map[$key]).Count
}

function Read-TrimMap {
    $map = @{}
    $cutoff = (Get-Date).AddMinutes(-1 * $trimCooldownMinutes)
    foreach ($item in @(Read-StateArray -Path $trimStatePath)) {
        if (-not $item.Key -or -not $item.TrimmedAt) { continue }
        $key = [string]$item.Key
        if (-not ($key.StartsWith("pidpath:") -or $key.StartsWith("pidname:"))) { continue }
        try {
            $trimmed = [DateTime]::Parse([string]$item.TrimmedAt, $null, [Globalization.DateTimeStyles]::RoundtripKind)
            if ($trimmed -ge $cutoff) {
                $map[$key] = [pscustomobject]@{
                    Key = $key
                    ProcessName = [string]$item.ProcessName
                    Path = [string]$item.Path
                    TrimmedAt = $trimmed.ToString("o")
                }
            }
        } catch {
        }
    }
    return $map
}

function Save-TrimMap {
    param([hashtable]$Map)
    Write-StateArray -Path $trimStatePath -Items @($Map.Values)
}

function Test-TrimCooldown {
    param(
        [hashtable]$Map,
        [System.Diagnostics.Process]$Process,
        [string]$Path
    )

    if (-not $Process) { return $false }
    $key = Get-TrimIdentityKey -Process $Process -Path $Path
    return $Map.ContainsKey($key)
}

function Set-TrimCooldown {
    param(
        [hashtable]$Map,
        [System.Diagnostics.Process]$Process,
        [string]$Path
    )

    if (-not $Process) { return }
    $key = Get-TrimIdentityKey -Process $Process -Path $Path
    $Map[$key] = [pscustomobject]@{
        Key = $key
        ProcessName = $Process.ProcessName
        Path = $Path
        TrimmedAt = (Get-Date).ToString("o")
    }
}

function Get-CandidateWeight {
    param([object]$Row)

    $weight = ([double]$Row.WorkingSetMB * 1.0) + ([double]$Row.CpuPercent * 120.0) + ([int]$Row.BurstCount * 140.0)
    if ($Row.GuardReason) {
        $weight *= 0.18
    }
    if ([string]$Row.AppPolicy -eq "Protect") {
        $weight = 0.0
    } elseif ([string]$Row.AppPolicy -eq "Deep") {
        $weight *= 1.25
    } elseif ([string]$Row.AppPolicy -eq "Light") {
        $weight *= 0.72
    }
    if ([bool]$Row.SwitchFastWake) {
        $weight *= 0.58
    }
    if ($realtimeFriendlyNames.Contains([string]$Row.ProcessName)) {
        $weight *= 0.62
    }
    if ($smartLearning -and [int]$Row.LearningObservations -ge $learningMinObservations) {
        if ([bool]$Row.LearningFastWake) {
            $weight *= 0.48
        } elseif (([string]$script:currentMemoryPressure.Level -in @("Elevated", "Critical")) -and [int]$Row.LearningAggression -gt 0) {
            $weight *= (1.0 + ([int]$Row.LearningAggression * 0.18))
        }
    }
    if ($behaviorEngine -and [int]$Row.BehaviorConfidence -ge 45) {
        if ([int]$Row.BehaviorBias -le -2) {
            $weight *= 0.48
        } elseif ([int]$Row.BehaviorBias -eq -1) {
            $weight *= 0.72
        } elseif ([int]$Row.BehaviorBias -eq 1) {
            $weight *= 1.12
        } elseif ([int]$Row.BehaviorBias -ge 2) {
            $weight *= 1.26
        }
    }
    if ([bool]$Row.ForegroundFullscreen) {
        $weight *= 1.15
    }
    if ($memoryPressureGovernor) {
        switch ([string]$script:currentMemoryPressure.Level) {
            "Moderate" { $weight *= 1.08 }
            "Elevated" { $weight *= 1.22 }
            "Critical" { $weight *= 1.38 }
        }
    }
    if ($perGameProfiles -and [int]$Row.GameAggressionBias -gt 0 -and [string]$script:currentIntent.Kind -eq "Gaming") {
        $weight *= (1.0 + ([int]$Row.GameAggressionBias * 0.12))
    }
    return [math]::Round($weight, 3)
}

function Get-NapPolicy {
    param([object]$Row)

    $tier = "Balanced"
    $reason = "steady-background"
    $deepMinimum = $deepNapMinimumMB
    $deepCpuLimit = $deepNapMaxCpuPercent
    $policySource = "auto"
    if ([bool]$Row.ForegroundFullscreen) {
        $deepMinimum = [math]::Min($deepMinimum, 120.0)
        $deepCpuLimit = [math]::Max($deepCpuLimit, 0.75)
    }
    if ($memoryPressureGovernor) {
        switch ([string]$script:currentMemoryPressure.Level) {
            "Moderate" {
                $deepMinimum = [math]::Min($deepMinimum, 180.0)
            }
            "Elevated" {
                $deepMinimum = [math]::Min($deepMinimum, 120.0)
                $deepCpuLimit = [math]::Max($deepCpuLimit, 0.85)
            }
            "Critical" {
                $deepMinimum = [math]::Min($deepMinimum, 80.0)
                $deepCpuLimit = [math]::Max($deepCpuLimit, 1.05)
            }
        }
    }
    if ($perGameProfiles -and [int]$Row.GameAggressionBias -gt 0 -and [string]$script:currentIntent.Kind -eq "Gaming") {
        $deepMinimum = [math]::Min($deepMinimum, 110.0)
        $deepCpuLimit = [math]::Max($deepCpuLimit, 0.9)
    }

    if (-not $adaptiveNap) {
        $reason = "fixed-policy"
    } elseif ([string]$Row.AppPolicy -eq "Light") {
        $tier = "Light"
        $reason = "user-light-policy"
        $policySource = "user"
    } elseif ([string]$Row.AppPolicy -eq "Balanced") {
        $tier = "Balanced"
        $reason = "user-balanced-policy"
        $policySource = "user"
    } elseif ([string]$Row.AppPolicy -eq "Deep") {
        if ([double]$Row.CpuPercent -le [math]::Max(1.0, $deepCpuLimit) -and -not [bool]$Row.SwitchFastWake -and -not $Row.GuardReason -and -not ($realtimeFriendlyNames.Contains([string]$Row.ProcessName))) {
            $tier = "Deep"
            $reason = "user-deep-policy"
        } else {
            $tier = "Balanced"
            $reason = "user-deep-softened"
        }
        $policySource = "user"
    } elseif ($realtimeFriendlyNames.Contains([string]$Row.ProcessName)) {
        $tier = "Light"
        $reason = "realtime-friendly"
    } elseif ([bool]$Row.SwitchFastWake) {
        $tier = "Light"
        $reason = "foreground-fast-wake"
    } elseif ($smartLearning -and [int]$Row.LearningObservations -ge $learningMinObservations -and [bool]$Row.LearningFastWake) {
        $tier = "Light"
        $reason = "learned-fast-wake"
    } elseif ($behaviorEngine -and [int]$Row.BehaviorConfidence -ge $behaviorLightConfidence -and [int]$Row.BehaviorBias -lt 0) {
        $tier = "Light"
        $reason = if ([string]$Row.BehaviorReason) { [string]$Row.BehaviorReason } else { "behavior-guard" }
        $policySource = "behavior"
    } elseif ($behaviorEngine -and [int]$Row.BehaviorObservations -ge $behaviorMinObservations -and [int]$Row.BehaviorConfidence -ge $behaviorDeepConfidence -and [int]$Row.BehaviorBias -ge 2 -and [double]$Row.CpuPercent -le [math]::Max(1.0, $deepCpuLimit) -and -not [bool]$Row.SwitchFastWake -and -not $Row.GuardReason -and -not ($realtimeFriendlyNames.Contains([string]$Row.ProcessName))) {
        $tier = "Deep"
        $reason = if ([string]$Row.BehaviorReason) { [string]$Row.BehaviorReason } else { "behavior-proven-idle" }
        $policySource = "behavior"
    } elseif ($behaviorEngine -and [int]$Row.BehaviorObservations -ge $behaviorMinObservations -and [int]$Row.BehaviorConfidence -ge $behaviorDeepConfidence -and [int]$Row.BehaviorBias -eq 1 -and [double]$Row.WorkingSetMB -ge $balancedNapMinimumMB) {
        $tier = "Balanced"
        $reason = if ([string]$Row.BehaviorReason) { [string]$Row.BehaviorReason } else { "behavior-proven-steady" }
        $policySource = "behavior"
    } elseif ($smartLearning -and [int]$Row.LearningObservations -ge $learningMinObservations -and [int]$Row.LearningAggression -ge 2 -and ([string]$script:currentMemoryPressure.Level -in @("Elevated", "Critical")) -and [double]$Row.CpuPercent -le [math]::Max(1.0, $deepCpuLimit)) {
        $tier = "Deep"
        $reason = "learned-memory-pressure"
    } elseif ($smartLearning -and [int]$Row.LearningObservations -ge $learningMinObservations -and [string]$Row.LearningPreferredTier -eq "Balanced" -and [double]$Row.WorkingSetMB -ge $balancedNapMinimumMB) {
        $tier = "Balanced"
        $reason = "learned-steady-background"
    } elseif ([double]$Row.WorkingSetMB -ge $deepMinimum -and [double]$Row.CpuPercent -le $deepCpuLimit -and [int]$Row.BurstCount -eq 0) {
        $tier = "Deep"
        $reason = if ([bool]$Row.ForegroundFullscreen) { "fullscreen-idle-heavy" } else { "idle-heavy" }
    } elseif ([double]$Row.WorkingSetMB -lt $balancedNapMinimumMB) {
        $tier = "Light"
        $reason = "small-footprint"
    } elseif ([double]$Row.CpuPercent -gt $balancedNapMaxCpuPercent) {
        $tier = "Light"
        $reason = "activity-detected"
    } elseif ([int]$Row.BurstCount -ge $burstRepeatCount) {
        $tier = "Balanced"
        $reason = "bursty-background"
    }
    if ($memoryPressureGovernor -and $tier -eq "Balanced" -and [string]$script:currentMemoryPressure.Level -eq "Critical" -and [double]$Row.WorkingSetMB -ge 220.0 -and [double]$Row.CpuPercent -le 0.4 -and -not [bool]$Row.SwitchFastWake -and -not $Row.GuardReason) {
        $tier = "Deep"
        $reason = "critical-memory-idle-heavy"
        if ($policySource -eq "auto") { $policySource = "governor" }
    }

    [pscustomobject]@{
        Tier = $tier
        Reason = $reason
        PriorityClass = $napTierPriority[$tier]
        MemoryPriority = [int]$napTierMemory[$tier]
        IoPriority = [int]$napTierIo[$tier]
        TrimMinimumMB = Get-PressureTrimMinimum -BaseMinimum ([double]$napTierTrimMinimum[$tier]) -Tier $tier
        Source = $policySource
        LearningSummary = if ($smartLearning -and [int]$Row.LearningObservations -gt 0) { "L " + [string]$Row.LearningPreferredTier } else { "" }
    }
}

function Get-SkipReason {
    param(
        [System.Diagnostics.Process]$Process,
        [object]$Foreground,
        [double]$CpuPercent,
        [hashtable]$ProtectMap,
        [double]$CpuProtectThreshold,
        [string]$Path,
        [object]$AppPolicy,
        [object]$GuardDecision
    )

    if ($Process.Id -eq $currentPid) { return "Self" }
    if ($skipSessionZero -and $Process.SessionId -eq 0) { return "Session0Service" }
    if ($Process.SessionId -ne $currentSessionId) { return "OtherSession" }
    if ($systemNames.Contains($Process.ProcessName)) { return "SystemProcess" }
    if ($protectedNames.Contains($Process.ProcessName)) { return "ProtectedTweakerOrTool" }
    if ($skipForegroundName -and $Foreground.ProcessName -and $Process.ProcessName -ieq $Foreground.ProcessName) { return "ForegroundApp" }
    if ($AppPolicy -and [string]$AppPolicy.Policy -eq "Protect") { return "UserProtectPolicy" }
    if ($GuardDecision -and [bool]$GuardDecision.Protect) { return [string]$GuardDecision.Reason }

    $path = $Path
    if (-not $path) { $path = Get-ProcessPathText -Process $Process }
    if (-not $path) { return "NoAccessiblePath" }

    if (Test-TemporaryProtected -Map $ProtectMap -Process $Process -Path $path) { return "TemporaryActiveApp" }
    if ($skipHighCpu -and $CpuPercent -ge $CpuProtectThreshold) { return "ActiveCpu" }

    foreach ($fragment in $protectedPathFragments) {
        if ($path.IndexOf($fragment, [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
            return "ProtectedPath"
        }
    }

    if ($skipWindowsPath) {
        $win = [System.IO.Path]::GetFullPath($env:WINDIR).TrimEnd('\')
        if ($path.StartsWith($win, [System.StringComparison]::OrdinalIgnoreCase)) {
            return "WindowsPath"
        }
    }

    return $null
}

function Get-ProcessCpuPercentMap {
    $map = @{}
    $first = @(Get-Process -ErrorAction SilentlyContinue | Select-Object Id, CPU)
    $firstById = @{}
    foreach ($p in $first) {
        if ($p.CPU -ne $null) {
            $firstById[[int]$p.Id] = [double]$p.CPU
        }
    }

    Start-Sleep -Milliseconds $cpuSampleMilliseconds
    $sampleSeconds = $cpuSampleMilliseconds / 1000.0
    $second = @(Get-Process -ErrorAction SilentlyContinue | Select-Object Id, CPU)
    foreach ($p in $second) {
        if ($p.CPU -eq $null) { continue }
        $id = [int]$p.Id
        if (-not $firstById.ContainsKey($id)) { continue }
        $delta = [double]$p.CPU - [double]$firstById[$id]
        if ($delta -lt 0) { $delta = 0 }
        $map[$id] = [math]::Round(($delta / $sampleSeconds / $logicalProcessorCount) * 100.0, 2)
    }

    return $map
}

function Get-BackgroundProcessRows {
    $foreground = Get-ForegroundInfo
    $script:currentMemoryPressure = Get-SystemMemoryPressure
    $script:foregroundSwitchMap = Read-ForegroundSwitchMap
    $script:gameProfileMap = Read-GameProfileMap
    $script:behaviorMap = Read-BehaviorMap
    $script:appPolicyMap = Read-AppPolicyMap
    if ($smartLearning) {
        $script:currentLearningSession = Get-LearningSessionContext -Foreground $foreground -Pressure $script:currentMemoryPressure
    }
    $effectiveHighCpuThreshold = $highCpuThreshold
    $effectiveTrimMinimumMB = $trimMinimumMB
    if ($smartFullscreenAware -and $foreground.IsFullscreen) {
        $effectiveHighCpuThreshold = $fullscreenHighCpuThreshold
        $effectiveTrimMinimumMB = $fullscreenTrimMinimumMB
    }
    $effectiveTrimMinimumMB = Get-PressureTrimMinimum -BaseMinimum $effectiveTrimMinimumMB -Tier "Balanced"

    $protectMap = Read-TemporaryProtectMap
    $burstMap = Read-BurstMap

    $foregroundSwitchItem = $null
    if ($foregroundSwitchAccelerator -and $foreground.Id -gt 0 -and $foreground.ProcessName) {
        $foregroundSwitchItem = Add-ForegroundSwitchObservation -Map $script:foregroundSwitchMap -ProcessName ([string]$foreground.ProcessName) -Path ([string]$foreground.Path) -ProcessId ([int]$foreground.Id)
    }

    if ($smartAutoProtect -and $foreground.Id -gt 0) {
        $fgProc = Get-Process -Id $foreground.Id -ErrorAction SilentlyContinue
        if ($fgProc) {
            Add-TemporaryProtection -Map $protectMap -Process $fgProc -Path $foreground.Path -Reason "ForegroundWake" -Minutes $autoProtectForegroundMinutes
            if ($foregroundSwitchItem -and (Test-ForegroundSwitchProtected -Profile $foregroundSwitchItem)) {
                Add-TemporaryProtection -Map $protectMap -Process $fgProc -Path $foreground.Path -Reason "FastForegroundWake" -Minutes $foregroundSwitchProtectMinutes
            }
        }
    }

    $cpuPercentByPid = @{}
    if ($skipHighCpu -or $intentEngine -or $downloadLauncherGuard -or $mediaCallProtection -or $behaviorEngine) {
        $cpuPercentByPid = Get-ProcessCpuPercentMap
    }
    $all = @(Get-Process -ErrorAction SilentlyContinue | Sort-Object ProcessName, Id)
    $script:currentIntent = Get-IntentContext -Foreground $foreground -Pressure $script:currentMemoryPressure -Processes $all -CpuMap $cpuPercentByPid
    Write-IntentState
    $currentGameProfile = Get-CurrentGameProfile
    $rows = @()

    foreach ($p in $all) {
        $cpuPercent = 0.0
        if ($cpuPercentByPid.ContainsKey([int]$p.Id)) {
            $cpuPercent = [double]$cpuPercentByPid[[int]$p.Id]
        }
        $path = Get-ProcessPathText -Process $p
        $role = Get-ProcessRole -ProcessName $p.ProcessName -Path $path
        $switchProfile = Get-ForegroundSwitchProfile -ProcessName $p.ProcessName -Path $path
        $appPolicy = Get-AppPolicyForText -ProcessName $p.ProcessName -Path $path
        $behaviorProfile = Get-BehaviorProfile -ProcessName $p.ProcessName -Path $path
        $privateMemoryMB = 0.0
        $handleCount = 0
        $threadCount = 0
        try { $privateMemoryMB = [math]::Round($p.PrivateMemorySize64 / 1MB, 1) } catch { $privateMemoryMB = 0.0 }
        try { $handleCount = [int]$p.HandleCount } catch { $handleCount = 0 }
        try { $threadCount = @($p.Threads).Count } catch { $threadCount = 0 }

        if ($path -and $smartAutoProtect -and $skipHighCpu -and $cpuPercent -ge $effectiveHighCpuThreshold) {
            Add-TemporaryProtection -Map $protectMap -Process $p -Path $path -Reason "ActiveCpu" -Minutes $autoProtectHighCpuMinutes
        }
        if ($path -and $smartBurstWatcher -and $p.Id -ne $foreground.Id -and $cpuPercent -ge $burstCpuThreshold -and $cpuPercent -lt $effectiveHighCpuThreshold) {
            Add-BurstObservation -Map $burstMap -Process $p -Path $path -CpuPercent $cpuPercent
        }

        $burstCount = if ($path) { Get-BurstCount -Map $burstMap -Process $p -Path $path } else { 0 }
        $guardDecision = Get-GuardDecision -ProcessName $p.ProcessName -Path $path -Role $role -CpuPercent $cpuPercent -BurstCount $burstCount -Foreground $foreground -SwitchProfile $switchProfile
        $skip = Get-SkipReason -Process $p -Foreground $foreground -CpuPercent $cpuPercent -ProtectMap $protectMap -CpuProtectThreshold $effectiveHighCpuThreshold -Path $path -AppPolicy $appPolicy -GuardDecision $guardDecision
        $learningProfile = $null
        if ($smartLearning) {
            $learningKey = Get-LearningKeyFromText -ProcessName $p.ProcessName -Path $path
            if ($learningKey -and $script:learningMap.ContainsKey($learningKey)) {
                $learningProfile = $script:learningMap[$learningKey]
            }
        }
        $rows += [pscustomobject]@{
            Id = $p.Id
            ProcessName = $p.ProcessName
            Candidate = -not $skip
            SkipReason = $skip
            PriorityClass = Get-ProcessPriorityText -Process $p
            IoPriority = Get-ProcessIoPriorityText -Process $p
            WorkingSetMB = [math]::Round($p.WorkingSet64 / 1MB, 1)
            PrivateMemoryMB = $privateMemoryMB
            HandleCount = $handleCount
            ThreadCount = $threadCount
            CpuSeconds = if ($p.CPU -ne $null) { [math]::Round($p.CPU, 1) } else { $null }
            CpuPercent = $cpuPercent
            BurstCount = $burstCount
            ForegroundFullscreen = [bool]$foreground.IsFullscreen
            EffectiveTrimMinimumMB = $effectiveTrimMinimumMB
            SessionId = $p.SessionId
            Path = $path
            AppKey = if ($appPolicy) { [string]$appPolicy.Key } else { Get-AppIdentityKeyFromText -ProcessName $p.ProcessName -Path $path }
            Role = $role
            AppPolicy = if ($appPolicy) { [string]$appPolicy.Policy } else { "Auto" }
            GuardReason = if ($guardDecision) { [string]$guardDecision.Reason } else { "" }
            GuardConfidence = if ($guardDecision) { [int]$guardDecision.Confidence } else { 0 }
            SwitchFastWake = if ($switchProfile) { [bool]$switchProfile.FastWake } else { $false }
            SwitchWakeCount = if ($switchProfile) { [int]$switchProfile.WakeCount } else { 0 }
            IntentKind = if ($script:currentIntent) { [string]$script:currentIntent.Kind } else { "Desktop" }
            IntentConfidence = if ($script:currentIntent) { [int]$script:currentIntent.Confidence } else { 0 }
            GameProfileName = if ($currentGameProfile) { [string]$currentGameProfile.Name } else { "" }
            GameProfileObservations = if ($currentGameProfile) { [int]$currentGameProfile.Observations } else { 0 }
            GameAggressionBias = if ($currentGameProfile) { [int]$currentGameProfile.AggressionBias } else { 0 }
            LearningObservations = if ($learningProfile) { [int]$learningProfile.Observations } else { 0 }
            LearningWakeCount = if ($learningProfile) { [int]$learningProfile.WakeCount } else { 0 }
            LearningFastWake = if ($learningProfile) { [bool]$learningProfile.FastWake } else { $false }
            LearningPreferredTier = if ($learningProfile -and $learningProfile.PreferredTier) { [string]$learningProfile.PreferredTier } else { "" }
            LearningAggression = if ($learningProfile) { [int]$learningProfile.Aggression } else { 0 }
            BehaviorObservations = if ($behaviorProfile) { [int]$behaviorProfile.Observations } else { 0 }
            BehaviorWakeCount = if ($behaviorProfile) { [int]$behaviorProfile.WakeCount } else { 0 }
            BehaviorConfidence = if ($behaviorProfile) { [int]$behaviorProfile.Confidence } else { 0 }
            BehaviorBias = if ($behaviorProfile) { [int]$behaviorProfile.AggressionBias } else { 0 }
            BehaviorPreferredTier = if ($behaviorProfile -and $behaviorProfile.PreferredTier) { [string]$behaviorProfile.PreferredTier } else { "" }
            BehaviorReason = if ($behaviorProfile -and $behaviorProfile.LastReason) { [string]$behaviorProfile.LastReason } else { "" }
            BehaviorAvgRefaultMB = if ($behaviorProfile) { [double]$behaviorProfile.AvgRefaultMB } else { 0.0 }
            BehaviorAvgTrimDeltaMB = if ($behaviorProfile) { [double]$behaviorProfile.AvgTrimDeltaMB } else { 0.0 }
        }
    }

    Save-TemporaryProtectMap -Map $protectMap
    Save-BurstMap -Map $burstMap
    Save-ForegroundSwitchMap -Map $script:foregroundSwitchMap
    return $rows
}

function New-StateSnapshot {
    param([array]$Rows)

    if ($StateMode -eq "None") {
        return $null
    }

    if ($StateMode -eq "Latest") {
        $path = Join-Path $outDir "background-nap-state-latest.json"
    } else {
        $stamp = Get-Date -Format "yyyyMMdd-HHmmss"
        $path = Join-Path $outDir "background-nap-state-$stamp.json"
    }

    $state = [pscustomobject]@{
        Timestamp = (Get-Date).ToString("o")
        ConfigPath = $ConfigPath
        CurrentSessionId = $currentSessionId
        StateMode = $StateMode
        Processes = @($Rows | Where-Object { $_.Candidate } | ForEach-Object {
            [pscustomobject]@{
                Id = $_.Id
                ProcessName = $_.ProcessName
                PriorityClass = $_.PriorityClass
                IoPriority = $_.IoPriority
                WorkingSetMB = $_.WorkingSetMB
                Path = $_.Path
            }
        })
    }
    $state | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $path -Encoding UTF8
    return $path
}

function Write-ApplySummaryLog {
    param([array]$Results)

    $processCount = @($Results).Count
    $appKeys = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
    $before = 0.0
    $after = 0.0
    foreach ($r in @($Results)) {
        $appKey = [string]$r.AppKey
        if ([string]::IsNullOrWhiteSpace($appKey)) {
            if (-not [string]::IsNullOrWhiteSpace([string]$r.Path)) {
                $appKey = "path:" + ([string]$r.Path).ToLowerInvariant()
            } elseif (-not [string]::IsNullOrWhiteSpace([string]$r.ProcessName)) {
                $appKey = "name:" + ([string]$r.ProcessName).ToLowerInvariant()
            } else {
                $appKey = "pid:" + ([string]$r.Id)
            }
        }
        [void]$appKeys.Add($appKey)
        if ($r.WorkingSetBeforeMB -ne $null) { $before += [double]$r.WorkingSetBeforeMB }
        if ($r.WorkingSetAfterMB -ne $null) { $after += [double]$r.WorkingSetAfterMB }
    }
    $count = $appKeys.Count
    $delta = $before - $after
    if ($delta -lt 0) { $delta = 0.0 }
    $light = @($Results | Where-Object { $_.NapTier -eq "Light" }).Count
    $balanced = @($Results | Where-Object { $_.NapTier -eq "Balanced" }).Count
    $deep = @($Results | Where-Object { $_.NapTier -eq "Deep" }).Count
    $trimmed = @($Results | Where-Object { $_.TrimWorkingSet -eq "OK" }).Count
    $cooldown = @($Results | Where-Object { $_.TrimWorkingSet -eq "Cooldown" }).Count
    $fullscreen = @($Results | Where-Object { $_.ForegroundFullscreen } | Select-Object -First 1).Count -gt 0
    $top = @($Results | Sort-Object NapScore -Descending | Select-Object -First 1)
    $topText = if ($top.Count -gt 0 -and $top[0].ProcessName) { " top=$($top[0].ProcessName) score=$($top[0].NapScore)" } else { "" }
    $learningText = ""
    if ($smartLearning) {
        $learningText = " learning=on profiles={0} pressure={1} freeMB={2}" -f $script:learningMap.Count, ([string]$script:currentMemoryPressure.Level), ([math]::Round([double]$script:currentMemoryPressure.FreeMB, 0))
    }
    $behaviorText = ""
    if ($behaviorEngine) {
        $behaviorText = " behavior=on behaviorProfiles={0}" -f $script:behaviorMap.Count
    }
    $intentText = ""
    if ($intentEngine -and $script:currentIntent) {
        $intentText = " intent={0} confidence={1}" -f ([string]$script:currentIntent.Kind), ([int]$script:currentIntent.Confidence)
    }
    $line = "{0} action=apply targets={1} processes={2} beforeMB={3} afterMB={4} deltaMB={5} light={6} balanced={7} deep={8} trimmed={9} cooldown={10} fullscreen={11}{12}{13}{14}{15}" -f (Get-Date).ToString("s"), $count, $processCount, ([math]::Round($before, 1)), ([math]::Round($after, 1)), ([math]::Round($delta, 1)), $light, $balanced, $deep, $trimmed, $cooldown, ([string]$fullscreen).ToLowerInvariant(), $topText, $learningText, $behaviorText, $intentText
    Add-Content -LiteralPath $LogPath -Value $line -Encoding UTF8
}

function Get-NapResultAppKey {
    param([object]$Result)

    $appKey = [string]$Result.AppKey
    if (-not [string]::IsNullOrWhiteSpace($appKey)) { return $appKey }
    if (-not [string]::IsNullOrWhiteSpace([string]$Result.Path)) { return "path:" + ([string]$Result.Path).ToLowerInvariant() }
    if (-not [string]::IsNullOrWhiteSpace([string]$Result.ProcessName)) { return "name:" + ([string]$Result.ProcessName).ToLowerInvariant() }
    return "pid:" + ([string]$Result.Id)
}

function Convert-NapResultGroupToScoreItem {
    param([array]$Group)

    $items = @($Group)
    $primary = @($items | Sort-Object NapScore -Descending | Select-Object -First 1)
    if ($primary.Count -eq 0) { return $null }
    $p = $primary[0]
    $before = 0.0
    $after = 0.0
    $cpu = 0.0
    $bursts = 0
    $score = 0.0
    $ids = @()
    foreach ($item in $items) {
        if ($item.Id -ne $null) { $ids += [int]$item.Id }
        if ($item.WorkingSetBeforeMB -ne $null) { $before += [double]$item.WorkingSetBeforeMB }
        if ($item.WorkingSetAfterMB -ne $null) { $after += [double]$item.WorkingSetAfterMB }
        if ($item.CpuPercent -ne $null) { $cpu += [double]$item.CpuPercent }
        if ($item.BurstCount -ne $null) { $bursts += [int]$item.BurstCount }
        if ($item.NapScore -ne $null) { $score += [double]$item.NapScore }
    }
    $deltaMB = $before - $after
    if ($deltaMB -lt 0) { $deltaMB = 0.0 }
    [pscustomobject]@{
        ProcessName = $p.ProcessName
        Id = $p.Id
        InstanceCount = $items.Count
        ProcessIds = @($ids)
        Score = [math]::Round($score, 1)
        CpuPercent = [math]::Round($cpu, 1)
        BurstCount = $bursts
        WorkingSetBeforeMB = [math]::Round($before, 1)
        WorkingSetAfterMB = [math]::Round($after, 1)
        DeltaMB = [math]::Round($deltaMB, 1)
        Priority = $p.Priority
        MemoryPriority = $p.MemoryPriority
        IoPriority = $p.IoPriority
        PowerThrottling = $p.PowerThrottling
        TrimWorkingSet = $p.TrimWorkingSet
        NapTier = $p.NapTier
        Decision = $p.Decision
        Learning = $p.Learning
        LearningObservations = $p.LearningObservations
        LearningWakeCount = $p.LearningWakeCount
        LearningAggression = $p.LearningAggression
        LearningPressure = $p.LearningPressure
        BehaviorObservations = $p.BehaviorObservations
        BehaviorWakeCount = $p.BehaviorWakeCount
        BehaviorConfidence = $p.BehaviorConfidence
        BehaviorBias = $p.BehaviorBias
        BehaviorPreferredTier = $p.BehaviorPreferredTier
        BehaviorReason = $p.BehaviorReason
        BehaviorAvgRefaultMB = $p.BehaviorAvgRefaultMB
        BehaviorAvgTrimDeltaMB = $p.BehaviorAvgTrimDeltaMB
        ForegroundFullscreen = $p.ForegroundFullscreen
        Role = $p.Role
        AppKey = $p.AppKey
        AppPolicy = $p.AppPolicy
        PolicySource = $p.PolicySource
        GuardReason = $p.GuardReason
        GuardConfidence = $p.GuardConfidence
        SwitchFastWake = $p.SwitchFastWake
        SwitchWakeCount = $p.SwitchWakeCount
        IntentKind = $p.IntentKind
        IntentConfidence = $p.IntentConfidence
        GameProfileName = $p.GameProfileName
        GameProfileObservations = $p.GameProfileObservations
        GameAggressionBias = $p.GameAggressionBias
        Path = $p.Path
    }
}

function Write-NapScore {
    param([array]$Results)

    if (-not $smartNapScore) { return }
    $groups = @{}
    foreach ($result in @($Results)) {
        $key = Get-NapResultAppKey -Result $result
        if (-not $groups.ContainsKey($key)) {
            $groups[$key] = New-Object System.Collections.ArrayList
        }
        [void]$groups[$key].Add($result)
    }
    $items = @($groups.Values | ForEach-Object { Convert-NapResultGroupToScoreItem -Group ([array]$_.ToArray()) } | Where-Object { $_ } | Sort-Object Score -Descending | Select-Object -First 25)

    [pscustomobject]@{
        Timestamp = (Get-Date).ToString("o")
        AppCount = $groups.Count
        ProcessCount = @($Results).Count
        LearningEnabled = [bool]$smartLearning
        LearningProfiles = if ($smartLearning) { [int]$script:learningMap.Count } else { 0 }
        BehaviorEnabled = [bool]$behaviorEngine
        BehaviorProfiles = if ($behaviorEngine) { [int]$script:behaviorMap.Count } else { 0 }
        MemoryPressure = [string]$script:currentMemoryPressure.Level
        FreeMemoryMB = [double]$script:currentMemoryPressure.FreeMB
        IntentKind = if ($script:currentIntent) { [string]$script:currentIntent.Kind } else { "Desktop" }
        IntentName = if ($script:currentIntent) { [string]$script:currentIntent.Name } else { "" }
        IntentConfidence = if ($script:currentIntent) { [int]$script:currentIntent.Confidence } else { 0 }
        IntentSignals = if ($script:currentIntent) { @($script:currentIntent.Signals) } else { @() }
        Items = $items
    } | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $scorePath -Encoding UTF8
}

function Invoke-ApplyOnce {
    param([bool]$SaveState = $true)

    if ($smartLearning) {
        $script:learningMap = Read-LearningMap
        $script:currentMemoryPressure = Get-SystemMemoryPressure
    }

    $rows = @(Get-BackgroundProcessRows)
    $targetLimit = Get-EffectiveMaxTargets
    $targets = @($rows |
        Where-Object { $_.Candidate } |
        Sort-Object @{Expression = { Get-CandidateWeight $_ }; Descending = $true}, @{Expression = "WorkingSetMB"; Descending = $true} |
        Select-Object -First $targetLimit)
    $trimMap = Read-TrimMap
    $state = $null
    if ($SaveState) {
        $state = New-StateSnapshot -Rows $rows
    }

    $results = foreach ($row in $targets) {
        $p = Get-Process -Id $row.Id -ErrorAction SilentlyContinue
        if (-not $p) {
            continue
        }

        $policy = Get-NapPolicy -Row $row
        $priorityStatus = "OK"
        try {
            $p.PriorityClass = $policy.PriorityClass
        } catch {
            $priorityStatus = "Error: $($_.Exception.Message)"
        }

        $memoryStatus = Convert-Win32Result ([BackgroundNapNative]::SetMemoryPriority([int]$p.Id, [uint32]$policy.MemoryPriority))
        $ioStatus = if ($useIoPriority) {
            Convert-NtStatusResult ([BackgroundNapNative]::SetIoPriority([int]$p.Id, [uint32]$policy.IoPriority))
        } else {
            "Disabled"
        }
        $powerStatus = Convert-Win32Result ([BackgroundNapNative]::SetPowerThrottling([int]$p.Id, $useEcoQos, $ignoreTimerResolution, $false))

        $trimThreshold = [double]$row.EffectiveTrimMinimumMB
        if ($policy.Tier -eq "Deep") {
            if ($policy.TrimMinimumMB -lt $trimThreshold) { $trimThreshold = $policy.TrimMinimumMB }
        } else {
            if ($policy.TrimMinimumMB -gt $trimThreshold) { $trimThreshold = $policy.TrimMinimumMB }
        }
        if ($smartBurstWatcher -and [int]$row.BurstCount -ge $burstRepeatCount -and $burstTrimMinimumMB -lt $trimThreshold) {
            $trimThreshold = $burstTrimMinimumMB
        }
        if ($behaviorEngine -and [int]$row.BehaviorConfidence -ge 45) {
            if ([int]$row.BehaviorBias -lt 0) {
                $trimThreshold = [math]::Max($trimThreshold, ([double]$row.WorkingSetMB + 1.0))
            } elseif ([int]$row.BehaviorBias -ge 2) {
                $trimThreshold = [math]::Max(24.0, [math]::Round($trimThreshold * 0.82, 1))
            }
        }

        $trimStatus = "SkippedBelowThreshold"
        if ($trimWorkingSet -and $row.WorkingSetMB -ge $trimThreshold) {
            $trimOnCooldown = if ($row.Path) { Test-TrimCooldown -Map $trimMap -Process $p -Path $row.Path } else { $false }
            if ($trimOnCooldown) {
                $trimStatus = "Cooldown"
            } else {
                $trimStatus = Convert-Win32Result ([BackgroundNapNative]::TrimWorkingSet([int]$p.Id))
                if ($trimStatus -eq "OK" -and $row.Path) {
                    Set-TrimCooldown -Map $trimMap -Process $p -Path $row.Path
                }
            }
        } elseif (-not $trimWorkingSet) {
            $trimStatus = "Disabled"
        }

        Start-Sleep -Milliseconds 20
        $after = Get-Process -Id $p.Id -ErrorAction SilentlyContinue
        $afterMB = if ($after) { [math]::Round($after.WorkingSet64 / 1MB, 1) } else { $null }
        $deltaMB = if ($afterMB -ne $null) { [double]$row.WorkingSetMB - [double]$afterMB } else { 0.0 }
        if ($deltaMB -lt 0) { $deltaMB = 0.0 }
        $tierWeight = if ($policy.Tier -eq "Deep") { 18.0 } elseif ($policy.Tier -eq "Balanced") { 9.0 } else { 3.0 }
        $napScore = [math]::Round(($deltaMB * 0.4) + ([double]$row.CpuPercent * 15.0) + ([int]$row.BurstCount * 10.0) + $tierWeight, 1)

        [pscustomobject]@{
            Id = $row.Id
            ProcessName = $row.ProcessName
            NapTier = $policy.Tier
            Decision = $policy.Reason
            PolicySource = $policy.Source
            Learning = $policy.LearningSummary
            LearningObservations = $row.LearningObservations
            LearningWakeCount = $row.LearningWakeCount
            LearningAggression = $row.LearningAggression
            LearningPressure = [string]$script:currentMemoryPressure.Level
            BehaviorObservations = $row.BehaviorObservations
            BehaviorWakeCount = $row.BehaviorWakeCount
            BehaviorConfidence = $row.BehaviorConfidence
            BehaviorBias = $row.BehaviorBias
            BehaviorPreferredTier = $row.BehaviorPreferredTier
            BehaviorReason = $row.BehaviorReason
            BehaviorAvgRefaultMB = $row.BehaviorAvgRefaultMB
            BehaviorAvgTrimDeltaMB = $row.BehaviorAvgTrimDeltaMB
            Role = $row.Role
            AppKey = $row.AppKey
            AppPolicy = $row.AppPolicy
            GuardReason = $row.GuardReason
            GuardConfidence = $row.GuardConfidence
            SwitchFastWake = $row.SwitchFastWake
            SwitchWakeCount = $row.SwitchWakeCount
            IntentKind = $row.IntentKind
            IntentConfidence = $row.IntentConfidence
            GameProfileName = $row.GameProfileName
            GameProfileObservations = $row.GameProfileObservations
            GameAggressionBias = $row.GameAggressionBias
            Priority = $priorityStatus
            MemoryPriority = $memoryStatus
            IoPriority = $ioStatus
            PowerThrottling = $powerStatus
            TrimWorkingSet = $trimStatus
            WorkingSetBeforeMB = $row.WorkingSetMB
            WorkingSetAfterMB = $afterMB
            PrivateMemoryMB = $row.PrivateMemoryMB
            HandleCount = $row.HandleCount
            ThreadCount = $row.ThreadCount
            CpuPercent = $row.CpuPercent
            BurstCount = $row.BurstCount
            NapScore = $napScore
            ForegroundFullscreen = $row.ForegroundFullscreen
            StatePath = $state
            Path = $row.Path
        }
    }

    Save-TrimMap -Map $trimMap
    Update-BehaviorProfiles -Rows $rows -Results $results
    Write-ContentionRadar -Rows $rows -Results $results
    return $results
}

function Invoke-Restore {
    if (-not $StatePath) {
        $latest = Get-ChildItem -LiteralPath $outDir -Filter "background-nap-state-*.json" -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTime -Descending |
            Select-Object -First 1
        if ($latest) {
            $StatePath = $latest.FullName
        }
    }

    $state = $null
    if ($StatePath -and (Test-Path -LiteralPath $StatePath)) {
        $state = Get-Content -LiteralPath $StatePath -Raw | ConvertFrom-Json
    }

    if (-not $state) {
        throw "No background nap state found to restore."
    }

    foreach ($item in @($state.Processes)) {
        $p = Get-Process -Id $item.Id -ErrorAction SilentlyContinue
        if (-not $p) {
            continue
        }

        $targetPriority = if ($item.PriorityClass) { [string]$item.PriorityClass } else { "Normal" }
        $targetIo = $normalIoPriority
        if ($item.IoPriority -and $ioPriorityMap.ContainsKey([string]$item.IoPriority)) {
            $targetIo = [int]$ioPriorityMap[[string]$item.IoPriority]
        }
        $priorityStatus = "OK"
        try {
            $restorePriority = [System.Enum]::Parse([System.Diagnostics.ProcessPriorityClass], $targetPriority, $true)
            $p.PriorityClass = $restorePriority
        } catch {
            $priorityStatus = "Error: $($_.Exception.Message)"
        }

        [pscustomobject]@{
            Id = $p.Id
            ProcessName = $p.ProcessName
            PriorityRestore = $priorityStatus
            TargetPriority = $targetPriority
            MemoryPriority = Convert-Win32Result ([BackgroundNapNative]::SetMemoryPriority([int]$p.Id, [uint32]$normalMemoryPriority))
            IoPriority = if ($useIoPriority) { Convert-NtStatusResult ([BackgroundNapNative]::SetIoPriority([int]$p.Id, [uint32]$targetIo)) } else { "Disabled" }
            PowerThrottling = Convert-Win32Result ([BackgroundNapNative]::SetPowerThrottling([int]$p.Id, $useEcoQos, $ignoreTimerResolution, $true))
            StatePath = $StatePath
        }
    }
}

function Invoke-ForegroundRestore {
    if (-not $smartForegroundWake) {
        return [pscustomobject]@{ Action = "ForegroundRestore"; Status = "Disabled" }
    }

    $targetPid = $TargetPid
    if ($targetPid -le 0) {
        $targetPid = [BackgroundNapNative]::GetForegroundPid()
    }
    if ($targetPid -le 0 -or $targetPid -eq $currentPid) {
        return [pscustomobject]@{ Action = "ForegroundRestore"; Status = "NoForeground" }
    }

    $p = Get-Process -Id $targetPid -ErrorAction SilentlyContinue
    if (-not $p) {
        return [pscustomobject]@{ Action = "ForegroundRestore"; Status = "ProcessMissing"; Id = $targetPid }
    }
    if ($p.SessionId -ne $currentSessionId) {
        return [pscustomobject]@{ Action = "ForegroundRestore"; Status = "OtherSession"; Id = $targetPid; ProcessName = $p.ProcessName }
    }
    if ($systemNames.Contains($p.ProcessName) -or $protectedNames.Contains($p.ProcessName)) {
        return [pscustomobject]@{ Action = "ForegroundRestore"; Status = "Protected"; Id = $targetPid; ProcessName = $p.ProcessName }
    }

    $path = Get-ProcessPathText -Process $p
    if ($foregroundSwitchAccelerator) {
        $script:foregroundSwitchMap = Read-ForegroundSwitchMap
        $switchItem = Add-ForegroundSwitchObservation -Map $script:foregroundSwitchMap -ProcessName $p.ProcessName -Path $path -ProcessId ([int]$p.Id) -ForceCount
        Save-ForegroundSwitchMap -Map $script:foregroundSwitchMap
        if ($switchItem -and (Test-ForegroundSwitchProtected -Profile $switchItem) -and $smartAutoProtect) {
            $protectMapForWake = Read-TemporaryProtectMap
            Add-TemporaryProtection -Map $protectMapForWake -Process $p -Path $path -Reason "FastForegroundWake" -Minutes $foregroundSwitchProtectMinutes
            Save-TemporaryProtectMap -Map $protectMapForWake
        }
    }
    $currentPriority = Get-ProcessPriorityText -Process $p
    $currentIo = Get-ProcessIoPriorityText -Process $p
    $state = $null
    $statePathToUse = $StatePath
    if (-not $statePathToUse) {
        $latest = Get-ChildItem -LiteralPath $outDir -Filter "background-nap-state-*.json" -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTime -Descending |
            Select-Object -First 1
        if ($latest) { $statePathToUse = $latest.FullName }
    }
    if ($statePathToUse -and (Test-Path -LiteralPath $statePathToUse)) {
        try { $state = Get-Content -LiteralPath $statePathToUse -Raw | ConvertFrom-Json } catch { $state = $null }
    }

    $item = $null
    if ($state -and $state.Processes) {
        $matches = @($state.Processes | Where-Object { [int]$_.Id -eq [int]$p.Id })
        if ($path) {
            $pathMatches = @($matches | Where-Object { $_.Path -and ([string]$_.Path).Equals($path, [System.StringComparison]::OrdinalIgnoreCase) })
            if ($pathMatches.Count -gt 0) { $matches = $pathMatches }
        }
        if ($matches.Count -gt 0) {
            $item = $matches[0]
        }
    }

    $looksNapped = ($currentPriority -in @("Idle", "BelowNormal")) -or ($currentIo -in @("VeryLow", "Low"))
    if (-not $item -and -not $looksNapped) {
        return [pscustomobject]@{ Action = "ForegroundRestore"; Status = "Noop"; Id = $p.Id; ProcessName = $p.ProcessName; Priority = $currentPriority; IoPriority = $currentIo }
    }

    $targetPriority = "Normal"
    if ($item -and $item.PriorityClass -and ([string]$item.PriorityClass) -notin @("Idle", "BelowNormal")) {
        $targetPriority = [string]$item.PriorityClass
    }

    $targetIo = $normalIoPriority
    if ($item -and $item.IoPriority -and $ioPriorityMap.ContainsKey([string]$item.IoPriority)) {
        $savedIo = [string]$item.IoPriority
        if ($savedIo -notin @("VeryLow", "Low")) {
            $targetIo = [int]$ioPriorityMap[$savedIo]
        }
    }

    $priorityStatus = "OK"
    try {
        $restorePriority = [System.Enum]::Parse([System.Diagnostics.ProcessPriorityClass], $targetPriority, $true)
        $p.PriorityClass = $restorePriority
    } catch {
        $priorityStatus = "Error: $($_.Exception.Message)"
    }

    $memoryStatus = Convert-Win32Result ([BackgroundNapNative]::SetMemoryPriority([int]$p.Id, [uint32]$normalMemoryPriority))
    $ioStatus = if ($useIoPriority) { Convert-NtStatusResult ([BackgroundNapNative]::SetIoPriority([int]$p.Id, [uint32]$targetIo)) } else { "Disabled" }
    $powerStatus = Convert-Win32Result ([BackgroundNapNative]::SetPowerThrottling([int]$p.Id, $useEcoQos, $ignoreTimerResolution, $true))

    if ($smartAutoProtect) {
        $protectMap = Read-TemporaryProtectMap
        Add-TemporaryProtection -Map $protectMap -Process $p -Path $path -Reason "ForegroundWake" -Minutes $autoProtectForegroundMinutes
        Save-TemporaryProtectMap -Map $protectMap
    }

    $line = "{0} action=foreground-restore pid={1} process={2} priority={3} io={4}" -f (Get-Date).ToString("s"), $p.Id, $p.ProcessName, $priorityStatus, $ioStatus
    Add-Content -LiteralPath $LogPath -Value $line -Encoding UTF8
    if ($smartLearning) {
        Add-LearningWake -Process $p -Path $path
        $wakeProfile = $null
        $wakeKey = Get-LearningKeyFromText -ProcessName $p.ProcessName -Path $path
        if ($wakeKey -and $script:learningMap.ContainsKey($wakeKey)) { $wakeProfile = $script:learningMap[$wakeKey] }
        $wakeCount = if ($wakeProfile) { [int]$wakeProfile.WakeCount } else { 0 }
        $learnLine = "{0} action=learning event=wake process={1} wakes={2} fastWake={3}" -f (Get-Date).ToString("s"), $p.ProcessName, $wakeCount, ([string]($wakeCount -ge $learningFastWakeThreshold)).ToLowerInvariant()
        Add-Content -LiteralPath $LogPath -Value $learnLine -Encoding UTF8
    }
    if ($behaviorEngine) {
        Add-BehaviorWake -Process $p -Path $path
        $behaviorWakeProfile = Get-BehaviorProfile -ProcessName $p.ProcessName -Path $path
        if ($behaviorWakeProfile) {
            $behaviorLine = "{0} action=behavior event=wake process={1} wakes={2} confidence={3} tier={4}" -f (Get-Date).ToString("s"), $p.ProcessName, ([int]$behaviorWakeProfile.WakeCount), ([int]$behaviorWakeProfile.Confidence), ([string]$behaviorWakeProfile.PreferredTier)
            Add-Content -LiteralPath $LogPath -Value $behaviorLine -Encoding UTF8
        }
    }

    [pscustomobject]@{
        Action = "ForegroundRestore"
        Status = "Restored"
        Id = $p.Id
        ProcessName = $p.ProcessName
        TargetPriority = $targetPriority
        Priority = $priorityStatus
        MemoryPriority = $memoryStatus
        IoPriority = $ioStatus
        PowerThrottling = $powerStatus
        StatePath = $statePathToUse
    }
}

if ($smartLearning) {
    $script:learningMap = Read-LearningMap
    $script:currentMemoryPressure = Get-SystemMemoryPressure
}
if ($behaviorEngine) {
    $script:behaviorMap = Read-BehaviorMap
}

switch ($Action) {
    "Status" {
        Get-BackgroundProcessRows |
            Where-Object { $_.Candidate -or $_.SkipReason -in @("ForegroundApp", "ProtectedTweakerOrTool", "ProtectedPath", "ActiveCpu", "UserProtectPolicy", "MediaCallGuard", "MediaGuard", "LauncherActivityGuard", "TemporaryActiveApp") } |
            Sort-Object @{ Expression = "Candidate"; Descending = $true }, @{ Expression = "WorkingSetMB"; Descending = $true }
    }
    "Apply" {
        $results = @(Invoke-ApplyOnce -SaveState:($StateMode -ne "None"))
        Update-LearningProfiles -Results $results
        Update-GameProfiles -Results $results
        Write-ApplySummaryLog -Results $results
        Write-NapScore -Results $results
        if (-not $Quiet) {
            $results
        }
    }
    "Restore" {
        Invoke-Restore
    }
    "ForegroundRestore" {
        Invoke-ForegroundRestore
    }
    "Watch" {
        if ($WatchMinutes -lt 1) { $WatchMinutes = 1 }
        if ($IntervalSeconds -lt 5) { $IntervalSeconds = 5 }

        $deadline = (Get-Date).AddMinutes($WatchMinutes)
        $first = $true
        while ((Get-Date) -lt $deadline) {
            $saveState = ($StateMode -ne "None") -and ($first -or $StateMode -eq "Latest")
            $results = @(Invoke-ApplyOnce -SaveState:$saveState)
            Update-LearningProfiles -Results $results
            Update-GameProfiles -Results $results
            Write-ApplySummaryLog -Results $results
            Write-NapScore -Results $results
            if (-not $Quiet) {
                $results
            }
            $first = $false
            if ((Get-Date) -lt $deadline) {
                Start-Sleep -Seconds $IntervalSeconds
            }
        }
    }
}
