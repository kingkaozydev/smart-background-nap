param(
    [ValidateSet("Status", "Apply", "Restore", "Watch")]
    [string]$Action = "Status",

    [string]$ConfigPath = (Join-Path $PSScriptRoot "game-session.config.json"),

    [string]$StatePath,

    [int]$WatchMinutes = 90,

    [int]$IntervalSeconds = 30,

    [switch]$IncludeForeground,

    [switch]$NoTrimWorkingSet
)

$ErrorActionPreference = "Continue"

if (-not (Test-Path -LiteralPath $ConfigPath)) {
    throw "Config not found: $ConfigPath"
}

$config = Get-Content -LiteralPath $ConfigPath -Raw | ConvertFrom-Json
$nap = $config.BrowserNap
if (-not $nap -or -not $nap.Enabled) {
    throw "BrowserNap is disabled or missing in config."
}

$workspace = $PSScriptRoot
$outDir = Join-Path $workspace "outputs"
New-Item -ItemType Directory -Path $outDir -Force | Out-Null

$processNames = @($nap.ProcessNames | ForEach-Object { [string]$_ })
$priorityClass = [string]$nap.PriorityClass
$useEcoQos = [bool]$nap.EnableEcoQoS
$ignoreTimerResolution = [bool]$nap.IgnoreTimerResolution
$trimWorkingSet = [bool]$nap.TrimWorkingSetOnce -and -not $NoTrimWorkingSet
$skipForeground = [bool]$nap.SkipForegroundProcess -and -not $IncludeForeground

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
$targetPriorityClass = [System.Enum]::Parse([System.Diagnostics.ProcessPriorityClass], $priorityClass, $true)

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

$cs = @"
using System;
using System.Runtime.InteropServices;

public static class BrowserNapNative {
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

if (-not ("BrowserNapNative" -as [type])) {
    Add-Type -TypeDefinition $cs -Language CSharp
}

function Get-ForegroundInfo {
    $foregroundPid = [BrowserNapNative]::GetForegroundPid()
    $proc = $null
    if ($foregroundPid -gt 0) {
        $proc = Get-Process -Id $foregroundPid -ErrorAction SilentlyContinue
    }

    [pscustomobject]@{
        Id = $foregroundPid
        ProcessName = if ($proc) { $proc.ProcessName } else { $null }
    }
}

function Get-BrowserProcesses {
    $all = @(Get-Process -ErrorAction SilentlyContinue)
    $all | Where-Object { $processNames -contains $_.ProcessName } | Sort-Object ProcessName, Id
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
        $status = [BrowserNapNative]::GetIoPriority([int]$Process.Id, [ref]$raw)
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

function New-StateSnapshot {
    param(
        [array]$Processes,
        [object]$Foreground
    )

    $stamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $path = Join-Path $outDir "browser-nap-state-$stamp.json"
    $state = [pscustomobject]@{
        Timestamp = (Get-Date).ToString("o")
        ConfigPath = $ConfigPath
        Foreground = $Foreground
        Processes = @($Processes | ForEach-Object {
            [pscustomobject]@{
                Id = $_.Id
                ProcessName = $_.ProcessName
                PriorityClass = Get-ProcessPriorityText -Process $_
                IoPriority = Get-ProcessIoPriorityText -Process $_
                WorkingSetMB = [math]::Round($_.WorkingSet64 / 1MB, 1)
                Path = Get-ProcessPathText -Process $_
            }
        })
    }
    $state | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $path -Encoding UTF8
    return $path
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

function Get-StatusRows {
    $foreground = Get-ForegroundInfo
    $skipNames = @()
    if ($skipForeground -and $foreground.ProcessName -and ($processNames -contains $foreground.ProcessName)) {
        $skipNames = @($foreground.ProcessName)
    }

    Get-BrowserProcesses | ForEach-Object {
        [pscustomobject]@{
            Id = $_.Id
            ProcessName = $_.ProcessName
            ForegroundNameSkipped = $skipNames -contains $_.ProcessName
            PriorityClass = Get-ProcessPriorityText -Process $_
            IoPriority = Get-ProcessIoPriorityText -Process $_
            WorkingSetMB = [math]::Round($_.WorkingSet64 / 1MB, 1)
            CpuSeconds = if ($_.CPU -ne $null) { [math]::Round($_.CPU, 1) } else { $null }
            Path = Get-ProcessPathText -Process $_
        }
    }
}

function Invoke-ApplyOnce {
    param([bool]$SaveState = $true)

    $foreground = Get-ForegroundInfo
    $skipNames = @()
    if ($skipForeground -and $foreground.ProcessName -and ($processNames -contains $foreground.ProcessName)) {
        $skipNames = @($foreground.ProcessName)
    }

    $targets = @(Get-BrowserProcesses)
    $state = $null
    if ($SaveState) {
        $state = New-StateSnapshot -Processes $targets -Foreground $foreground
    }

    $results = foreach ($p in $targets) {
        $skipped = $skipNames -contains $p.ProcessName
        $priorityStatus = "Skipped"
        $memoryStatus = "Skipped"
        $ioStatus = "Skipped"
        $powerStatus = "Skipped"
        $trimStatus = "Skipped"
        $beforeMB = [math]::Round($p.WorkingSet64 / 1MB, 1)

        if (-not $skipped) {
            try {
                $p.PriorityClass = $targetPriorityClass
                $priorityStatus = "OK"
            } catch {
                $priorityStatus = "Error: $($_.Exception.Message)"
            }

            $memoryStatus = Convert-Win32Result ([BrowserNapNative]::SetMemoryPriority([int]$p.Id, [uint32]$targetMemoryPriority))
            $ioStatus = if ($useIoPriority) {
                Convert-NtStatusResult ([BrowserNapNative]::SetIoPriority([int]$p.Id, [uint32]$targetIoPriority))
            } else {
                "Disabled"
            }
            $powerStatus = Convert-Win32Result ([BrowserNapNative]::SetPowerThrottling([int]$p.Id, $useEcoQos, $ignoreTimerResolution, $false))

            if ($trimWorkingSet) {
                $trimStatus = Convert-Win32Result ([BrowserNapNative]::TrimWorkingSet([int]$p.Id))
            } else {
                $trimStatus = "Disabled"
            }
        }

        Start-Sleep -Milliseconds 30
        $after = Get-Process -Id $p.Id -ErrorAction SilentlyContinue
        $afterMB = if ($after) { [math]::Round($after.WorkingSet64 / 1MB, 1) } else { $null }

        [pscustomobject]@{
            Id = $p.Id
            ProcessName = $p.ProcessName
            Skipped = $skipped
            Priority = $priorityStatus
            MemoryPriority = $memoryStatus
            IoPriority = $ioStatus
            PowerThrottling = $powerStatus
            TrimWorkingSet = $trimStatus
            WorkingSetBeforeMB = $beforeMB
            WorkingSetAfterMB = $afterMB
            StatePath = $state
        }
    }

    $results
}

function Invoke-Restore {
    $targets = @(Get-BrowserProcesses)
    $state = $null

    if ($StatePath) {
        if (Test-Path -LiteralPath $StatePath) {
            $state = Get-Content -LiteralPath $StatePath -Raw | ConvertFrom-Json
        } else {
            Write-Warning "State file not found: $StatePath. Restoring all configured browser processes to Normal."
        }
    }

    if (-not $state) {
        $latest = Get-ChildItem -LiteralPath $outDir -Filter "browser-nap-state-*.json" -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTime -Descending |
            Select-Object -First 1
        if ($latest) {
            $state = Get-Content -LiteralPath $latest.FullName -Raw | ConvertFrom-Json
            $StatePath = $latest.FullName
        }
    }

    $priorityByPid = @{}
    $ioPriorityByPid = @{}
    if ($state -and $state.Processes) {
        foreach ($item in @($state.Processes)) {
            if ($item.Id -and $item.PriorityClass) {
                $priorityByPid[[string]$item.Id] = [string]$item.PriorityClass
            }
            if ($item.Id -and $item.IoPriority) {
                $ioPriorityByPid[[string]$item.Id] = [string]$item.IoPriority
            }
        }
    }

    foreach ($p in $targets) {
        $targetPriority = "Normal"
        if ($priorityByPid.ContainsKey([string]$p.Id)) {
            $targetPriority = $priorityByPid[[string]$p.Id]
        }
        $targetIo = $normalIoPriority
        if ($ioPriorityByPid.ContainsKey([string]$p.Id) -and $ioPriorityMap.ContainsKey($ioPriorityByPid[[string]$p.Id])) {
            $targetIo = [int]$ioPriorityMap[$ioPriorityByPid[[string]$p.Id]]
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
            MemoryPriority = Convert-Win32Result ([BrowserNapNative]::SetMemoryPriority([int]$p.Id, [uint32]$normalMemoryPriority))
            IoPriority = if ($useIoPriority) { Convert-NtStatusResult ([BrowserNapNative]::SetIoPriority([int]$p.Id, [uint32]$targetIo)) } else { "Disabled" }
            PowerThrottling = Convert-Win32Result ([BrowserNapNative]::SetPowerThrottling([int]$p.Id, $useEcoQos, $ignoreTimerResolution, $true))
            StatePath = $StatePath
        }
    }
}

switch ($Action) {
    "Status" {
        Get-StatusRows
    }
    "Apply" {
        Invoke-ApplyOnce -SaveState $true
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
            Invoke-ApplyOnce -SaveState:$first
            $first = $false
            if ((Get-Date) -lt $deadline) {
                Start-Sleep -Seconds $IntervalSeconds
            }
        }
    }
}
