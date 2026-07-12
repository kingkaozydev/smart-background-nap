param(
    [ValidateSet("Status", "Apply", "Restore", "Watch")]
    [string]$Action = "Status",

    [string]$ConfigPath = (Join-Path $PSScriptRoot "game-session.config.json"),

    [string]$StatePath,

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

$protectedNames = New-Object "System.Collections.Generic.HashSet[string]" ([System.StringComparer]::OrdinalIgnoreCase)
@($config.ProtectedProcessNames + $nap.ProtectedProcessNames) | Where-Object { $_ } | ForEach-Object { [void]$protectedNames.Add([string]$_) }

$protectedPathFragments = @($nap.ProtectedPathFragments | Where-Object { $_ } | ForEach-Object { [string]$_ })

$systemNames = New-Object "System.Collections.Generic.HashSet[string]" ([System.StringComparer]::OrdinalIgnoreCase)
@($nap.SystemProcessNames) | Where-Object { $_ } | ForEach-Object { [void]$systemNames.Add([string]$_) }

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

    public static Int32 GetForegroundPid() {
        UInt32 pid;
        IntPtr hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero) {
            return 0;
        }
        GetWindowThreadProcessId(hwnd, out pid);
        return (Int32)pid;
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
    return ("NtStatus=0x{0:X8}" -f ([uint32]$Code))
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

function Get-SkipReason {
    param(
        [System.Diagnostics.Process]$Process,
        [object]$Foreground,
        [double]$CpuPercent
    )

    if ($Process.Id -eq $currentPid) { return "Self" }
    if ($skipSessionZero -and $Process.SessionId -eq 0) { return "Session0Service" }
    if ($Process.SessionId -ne $currentSessionId) { return "OtherSession" }
    if ($systemNames.Contains($Process.ProcessName)) { return "SystemProcess" }
    if ($protectedNames.Contains($Process.ProcessName)) { return "ProtectedTweakerOrTool" }
    if ($skipForegroundName -and $Foreground.ProcessName -and $Process.ProcessName -ieq $Foreground.ProcessName) { return "ForegroundApp" }
    if ($skipHighCpu -and $CpuPercent -ge $highCpuThreshold) { return "ActiveCpu" }

    $path = Get-ProcessPathText -Process $Process
    if (-not $path) { return "NoAccessiblePath" }

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
    $cpuPercentByPid = @{}
    if ($skipHighCpu) {
        $cpuPercentByPid = Get-ProcessCpuPercentMap
    }
    $all = @(Get-Process -ErrorAction SilentlyContinue | Sort-Object ProcessName, Id)

    foreach ($p in $all) {
        $cpuPercent = 0.0
        if ($cpuPercentByPid.ContainsKey([int]$p.Id)) {
            $cpuPercent = [double]$cpuPercentByPid[[int]$p.Id]
        }
        $path = Get-ProcessPathText -Process $p
        $skip = Get-SkipReason -Process $p -Foreground $foreground -CpuPercent $cpuPercent
        [pscustomobject]@{
            Id = $p.Id
            ProcessName = $p.ProcessName
            Candidate = -not $skip
            SkipReason = $skip
            PriorityClass = Get-ProcessPriorityText -Process $p
            IoPriority = Get-ProcessIoPriorityText -Process $p
            WorkingSetMB = [math]::Round($p.WorkingSet64 / 1MB, 1)
            CpuSeconds = if ($p.CPU -ne $null) { [math]::Round($p.CPU, 1) } else { $null }
            CpuPercent = $cpuPercent
            SessionId = $p.SessionId
            Path = $path
        }
    }
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
    $line = "{0} action=apply targets={1} beforeMB={2} afterMB={3} deltaMB={4}" -f (Get-Date).ToString("s"), $count, ([math]::Round($before, 1)), ([math]::Round($after, 1)), ([math]::Round($delta, 1))
    Add-Content -LiteralPath $LogPath -Value $line -Encoding UTF8
}

function Invoke-ApplyOnce {
    param([bool]$SaveState = $true)

    $rows = @(Get-BackgroundProcessRows)
    $targets = @($rows | Where-Object { $_.Candidate })
    $state = $null
    if ($SaveState) {
        $state = New-StateSnapshot -Rows $rows
    }

    foreach ($row in $targets) {
        $p = Get-Process -Id $row.Id -ErrorAction SilentlyContinue
        if (-not $p) {
            continue
        }

        $priorityStatus = "OK"
        try {
            $p.PriorityClass = $targetPriorityClass
        } catch {
            $priorityStatus = "Error: $($_.Exception.Message)"
        }

        $memoryStatus = Convert-Win32Result ([BackgroundNapNative]::SetMemoryPriority([int]$p.Id, [uint32]$targetMemoryPriority))
        $ioStatus = if ($useIoPriority) {
            Convert-NtStatusResult ([BackgroundNapNative]::SetIoPriority([int]$p.Id, [uint32]$targetIoPriority))
        } else {
            "Disabled"
        }
        $powerStatus = Convert-Win32Result ([BackgroundNapNative]::SetPowerThrottling([int]$p.Id, $useEcoQos, $ignoreTimerResolution, $false))

        $trimStatus = "SkippedBelowThreshold"
        if ($trimWorkingSet -and $row.WorkingSetMB -ge $trimMinimumMB) {
            $trimStatus = Convert-Win32Result ([BackgroundNapNative]::TrimWorkingSet([int]$p.Id))
        } elseif (-not $trimWorkingSet) {
            $trimStatus = "Disabled"
        }

        Start-Sleep -Milliseconds 20
        $after = Get-Process -Id $p.Id -ErrorAction SilentlyContinue
        $afterMB = if ($after) { [math]::Round($after.WorkingSet64 / 1MB, 1) } else { $null }

        [pscustomobject]@{
            Id = $row.Id
            ProcessName = $row.ProcessName
            Priority = $priorityStatus
            MemoryPriority = $memoryStatus
            IoPriority = $ioStatus
            PowerThrottling = $powerStatus
            TrimWorkingSet = $trimStatus
            WorkingSetBeforeMB = $row.WorkingSetMB
            WorkingSetAfterMB = $afterMB
            StatePath = $state
            Path = $row.Path
        }
    }
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

switch ($Action) {
    "Status" {
        Get-BackgroundProcessRows |
            Where-Object { $_.Candidate -or $_.SkipReason -in @("ForegroundApp", "ProtectedTweakerOrTool", "ProtectedPath", "ActiveCpu") } |
            Sort-Object @{ Expression = "Candidate"; Descending = $true }, @{ Expression = "WorkingSetMB"; Descending = $true }
    }
    "Apply" {
        $results = @(Invoke-ApplyOnce -SaveState:($StateMode -ne "None"))
        Write-ApplySummaryLog -Results $results
        if (-not $Quiet) {
            $results
        }
    }
    "Restore" {
        Invoke-Restore
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
