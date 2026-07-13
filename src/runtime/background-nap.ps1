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
    if ($smart.PSObject.Properties.Name -contains "NapScore") { $smartNapScore = [bool]$smart.NapScore }
}
if ($autoProtectForegroundMinutes -lt 1) { $autoProtectForegroundMinutes = 1 }
if ($autoProtectHighCpuMinutes -lt 1) { $autoProtectHighCpuMinutes = 1 }
if ($burstWindowMinutes -lt 1) { $burstWindowMinutes = 1 }
if ($burstRepeatCount -lt 1) { $burstRepeatCount = 1 }
if ($maxTargetsPerPass -lt 1) { $maxTargetsPerPass = 1 }
if ($trimCooldownMinutes -lt 1) { $trimCooldownMinutes = 1 }
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
    if ($realtimeFriendlyNames.Contains([string]$Row.ProcessName)) {
        $weight *= 0.62
    }
    if ([bool]$Row.ForegroundFullscreen) {
        $weight *= 1.15
    }
    return [math]::Round($weight, 3)
}

function Get-NapPolicy {
    param([object]$Row)

    $tier = "Balanced"
    $reason = "steady-background"
    $deepMinimum = $deepNapMinimumMB
    $deepCpuLimit = $deepNapMaxCpuPercent
    if ([bool]$Row.ForegroundFullscreen) {
        $deepMinimum = [math]::Min($deepMinimum, 120.0)
        $deepCpuLimit = [math]::Max($deepCpuLimit, 0.75)
    }

    if (-not $adaptiveNap) {
        $reason = "fixed-policy"
    } elseif ($realtimeFriendlyNames.Contains([string]$Row.ProcessName)) {
        $tier = "Light"
        $reason = "realtime-friendly"
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

    [pscustomobject]@{
        Tier = $tier
        Reason = $reason
        PriorityClass = $napTierPriority[$tier]
        MemoryPriority = [int]$napTierMemory[$tier]
        IoPriority = [int]$napTierIo[$tier]
        TrimMinimumMB = [double]$napTierTrimMinimum[$tier]
    }
}

function Get-SkipReason {
    param(
        [System.Diagnostics.Process]$Process,
        [object]$Foreground,
        [double]$CpuPercent,
        [hashtable]$ProtectMap,
        [double]$CpuProtectThreshold
    )

    if ($Process.Id -eq $currentPid) { return "Self" }
    if ($skipSessionZero -and $Process.SessionId -eq 0) { return "Session0Service" }
    if ($Process.SessionId -ne $currentSessionId) { return "OtherSession" }
    if ($systemNames.Contains($Process.ProcessName)) { return "SystemProcess" }
    if ($protectedNames.Contains($Process.ProcessName)) { return "ProtectedTweakerOrTool" }
    if ($skipForegroundName -and $Foreground.ProcessName -and $Process.ProcessName -ieq $Foreground.ProcessName) { return "ForegroundApp" }

    $path = Get-ProcessPathText -Process $Process
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
    $effectiveHighCpuThreshold = $highCpuThreshold
    $effectiveTrimMinimumMB = $trimMinimumMB
    if ($smartFullscreenAware -and $foreground.IsFullscreen) {
        $effectiveHighCpuThreshold = $fullscreenHighCpuThreshold
        $effectiveTrimMinimumMB = $fullscreenTrimMinimumMB
    }

    $protectMap = Read-TemporaryProtectMap
    $burstMap = Read-BurstMap

    if ($smartAutoProtect -and $foreground.Id -gt 0) {
        $fgProc = Get-Process -Id $foreground.Id -ErrorAction SilentlyContinue
        if ($fgProc) {
            Add-TemporaryProtection -Map $protectMap -Process $fgProc -Path $foreground.Path -Reason "ForegroundWake" -Minutes $autoProtectForegroundMinutes
        }
    }

    $cpuPercentByPid = @{}
    if ($skipHighCpu) {
        $cpuPercentByPid = Get-ProcessCpuPercentMap
    }
    $all = @(Get-Process -ErrorAction SilentlyContinue | Sort-Object ProcessName, Id)
    $rows = @()

    foreach ($p in $all) {
        $cpuPercent = 0.0
        if ($cpuPercentByPid.ContainsKey([int]$p.Id)) {
            $cpuPercent = [double]$cpuPercentByPid[[int]$p.Id]
        }
        $path = Get-ProcessPathText -Process $p

        if ($path -and $smartAutoProtect -and $skipHighCpu -and $cpuPercent -ge $effectiveHighCpuThreshold) {
            Add-TemporaryProtection -Map $protectMap -Process $p -Path $path -Reason "ActiveCpu" -Minutes $autoProtectHighCpuMinutes
        }
        if ($path -and $smartBurstWatcher -and $p.Id -ne $foreground.Id -and $cpuPercent -ge $burstCpuThreshold -and $cpuPercent -lt $effectiveHighCpuThreshold) {
            Add-BurstObservation -Map $burstMap -Process $p -Path $path -CpuPercent $cpuPercent
        }

        $burstCount = if ($path) { Get-BurstCount -Map $burstMap -Process $p -Path $path } else { 0 }
        $skip = Get-SkipReason -Process $p -Foreground $foreground -CpuPercent $cpuPercent -ProtectMap $protectMap -CpuProtectThreshold $effectiveHighCpuThreshold
        $rows += [pscustomobject]@{
            Id = $p.Id
            ProcessName = $p.ProcessName
            Candidate = -not $skip
            SkipReason = $skip
            PriorityClass = Get-ProcessPriorityText -Process $p
            IoPriority = Get-ProcessIoPriorityText -Process $p
            WorkingSetMB = [math]::Round($p.WorkingSet64 / 1MB, 1)
            CpuSeconds = if ($p.CPU -ne $null) { [math]::Round($p.CPU, 1) } else { $null }
            CpuPercent = $cpuPercent
            BurstCount = $burstCount
            ForegroundFullscreen = [bool]$foreground.IsFullscreen
            EffectiveTrimMinimumMB = $effectiveTrimMinimumMB
            SessionId = $p.SessionId
            Path = $path
        }
    }

    Save-TemporaryProtectMap -Map $protectMap
    Save-BurstMap -Map $burstMap
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

    $count = @($Results).Count
    $before = 0.0
    $after = 0.0
    foreach ($r in @($Results)) {
        if ($r.WorkingSetBeforeMB -ne $null) { $before += [double]$r.WorkingSetBeforeMB }
        if ($r.WorkingSetAfterMB -ne $null) { $after += [double]$r.WorkingSetAfterMB }
    }
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
    $line = "{0} action=apply targets={1} beforeMB={2} afterMB={3} deltaMB={4} light={5} balanced={6} deep={7} trimmed={8} cooldown={9} fullscreen={10}{11}" -f (Get-Date).ToString("s"), $count, ([math]::Round($before, 1)), ([math]::Round($after, 1)), ([math]::Round($delta, 1)), $light, $balanced, $deep, $trimmed, $cooldown, ([string]$fullscreen).ToLowerInvariant(), $topText
    Add-Content -LiteralPath $LogPath -Value $line -Encoding UTF8
}

function Write-NapScore {
    param([array]$Results)

    if (-not $smartNapScore) { return }
    $items = @($Results | Sort-Object NapScore -Descending | Select-Object -First 25 | ForEach-Object {
        $deltaMB = $null
        if ($_.WorkingSetBeforeMB -ne $null -and $_.WorkingSetAfterMB -ne $null) {
            $deltaMB = [math]::Round(([double]$_.WorkingSetBeforeMB - [double]$_.WorkingSetAfterMB), 1)
            if ($deltaMB -lt 0) { $deltaMB = 0.0 }
        }
        [pscustomobject]@{
            ProcessName = $_.ProcessName
            Id = $_.Id
            Score = $_.NapScore
            CpuPercent = $_.CpuPercent
            BurstCount = $_.BurstCount
            WorkingSetBeforeMB = $_.WorkingSetBeforeMB
            WorkingSetAfterMB = $_.WorkingSetAfterMB
            DeltaMB = $deltaMB
            Priority = $_.Priority
            MemoryPriority = $_.MemoryPriority
            IoPriority = $_.IoPriority
            PowerThrottling = $_.PowerThrottling
            TrimWorkingSet = $_.TrimWorkingSet
            NapTier = $_.NapTier
            Decision = $_.Decision
            ForegroundFullscreen = $_.ForegroundFullscreen
            Path = $_.Path
        }
    })

    [pscustomobject]@{
        Timestamp = (Get-Date).ToString("o")
        Items = $items
    } | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $scorePath -Encoding UTF8
}

function Invoke-ApplyOnce {
    param([bool]$SaveState = $true)

    $rows = @(Get-BackgroundProcessRows)
    $targets = @($rows |
        Where-Object { $_.Candidate } |
        Sort-Object @{Expression = { Get-CandidateWeight $_ }; Descending = $true}, @{Expression = "WorkingSetMB"; Descending = $true} |
        Select-Object -First $maxTargetsPerPass)
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
            Priority = $priorityStatus
            MemoryPriority = $memoryStatus
            IoPriority = $ioStatus
            PowerThrottling = $powerStatus
            TrimWorkingSet = $trimStatus
            WorkingSetBeforeMB = $row.WorkingSetMB
            WorkingSetAfterMB = $afterMB
            CpuPercent = $row.CpuPercent
            BurstCount = $row.BurstCount
            NapScore = $napScore
            ForegroundFullscreen = $row.ForegroundFullscreen
            StatePath = $state
            Path = $row.Path
        }
    }

    Save-TrimMap -Map $trimMap
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

switch ($Action) {
    "Status" {
        Get-BackgroundProcessRows |
            Where-Object { $_.Candidate -or $_.SkipReason -in @("ForegroundApp", "ProtectedTweakerOrTool", "ProtectedPath", "ActiveCpu") } |
            Sort-Object @{ Expression = "Candidate"; Descending = $true }, @{ Expression = "WorkingSetMB"; Descending = $true }
    }
    "Apply" {
        $results = @(Invoke-ApplyOnce -SaveState:($StateMode -ne "None"))
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
