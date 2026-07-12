param(
    [ValidateSet("Install", "Uninstall", "Status", "RunNow")]
    [string]$Action = "Status",

    [string]$ConfigPath = (Join-Path $PSScriptRoot "game-session.config.json"),

    [int]$IntervalMinutes,

    [int]$LogonDelaySeconds,

    [int]$ExecutionTimeLimitMinutes,

    [string]$AppExePath
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $ConfigPath)) {
    throw "Config not found: $ConfigPath"
}

$config = Get-Content -LiteralPath $ConfigPath -Raw | ConvertFrom-Json
$auto = $config.Automation
if (-not $auto) {
    throw "Automation config missing."
}

$taskName = [string]$auto.TaskName
if (-not $IntervalMinutes) { $IntervalMinutes = [int]$auto.IntervalMinutes }
if (-not $LogonDelaySeconds) { $LogonDelaySeconds = [int]$auto.LogonDelaySeconds }
if (-not $ExecutionTimeLimitMinutes) { $ExecutionTimeLimitMinutes = [int]$auto.ExecutionTimeLimitMinutes }

if ($IntervalMinutes -lt 1) { $IntervalMinutes = 1 }
if ($LogonDelaySeconds -lt 0) { $LogonDelaySeconds = 0 }
if ($ExecutionTimeLimitMinutes -lt 1) { $ExecutionTimeLimitMinutes = 1 }

$scriptPath = Join-Path $PSScriptRoot "background-nap.ps1"
if (-not (Test-Path -LiteralPath $scriptPath)) {
    throw "background-nap.ps1 not found: $scriptPath"
}
if (-not $AppExePath) {
    $AppExePath = Join-Path $PSScriptRoot "bin\SmartBackgroundNap.exe"
}

function ConvertTo-XmlText {
    param([string]$Text)
    return [System.Security.SecurityElement]::Escape($Text)
}

function Get-TaskDefinitionXml {
    $sid = [System.Security.Principal.WindowsIdentity]::GetCurrent().User.Value
    $author = "$env:USERDOMAIN\$env:USERNAME"
    $startBoundary = (Get-Date).AddMinutes(1).ToString("s")
    $interval = "PT${IntervalMinutes}M"
    $logonDelay = "PT${LogonDelaySeconds}S"
    $limit = "PT${ExecutionTimeLimitMinutes}M"
    $workDir = Split-Path -Parent $scriptPath
    if (Test-Path -LiteralPath $AppExePath) {
        $command = $AppExePath
        $arguments = '--apply'
    } else {
        $command = "powershell.exe"
        $arguments = '-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "' + $scriptPath + '" -Action Apply -StateMode Latest -Quiet'
    }

@"
<?xml version="1.0" encoding="UTF-16"?>
<Task version="1.4" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
  <RegistrationInfo>
    <Author>$(ConvertTo-XmlText $author)</Author>
    <Description>Smart Background Nap applies low-impact background process tuning for safe user apps while preserving games, Windows processes, configured protected apps, and the active foreground app.</Description>
  </RegistrationInfo>
  <Triggers>
    <LogonTrigger>
      <Enabled>true</Enabled>
      <Delay>$logonDelay</Delay>
    </LogonTrigger>
    <CalendarTrigger>
      <StartBoundary>$startBoundary</StartBoundary>
      <Enabled>true</Enabled>
      <ScheduleByDay>
        <DaysInterval>1</DaysInterval>
      </ScheduleByDay>
      <Repetition>
        <Interval>$interval</Interval>
        <StopAtDurationEnd>false</StopAtDurationEnd>
      </Repetition>
    </CalendarTrigger>
  </Triggers>
  <Principals>
    <Principal id="Author">
      <UserId>$sid</UserId>
      <LogonType>InteractiveToken</LogonType>
      <RunLevel>LeastPrivilege</RunLevel>
    </Principal>
  </Principals>
  <Settings>
    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
    <AllowHardTerminate>true</AllowHardTerminate>
    <StartWhenAvailable>true</StartWhenAvailable>
    <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
    <IdleSettings>
      <StopOnIdleEnd>false</StopOnIdleEnd>
      <RestartOnIdle>false</RestartOnIdle>
    </IdleSettings>
    <AllowStartOnDemand>true</AllowStartOnDemand>
    <Enabled>true</Enabled>
    <Hidden>false</Hidden>
    <RunOnlyIfIdle>false</RunOnlyIfIdle>
    <DisallowStartOnRemoteAppSession>false</DisallowStartOnRemoteAppSession>
    <UseUnifiedSchedulingEngine>true</UseUnifiedSchedulingEngine>
    <WakeToRun>false</WakeToRun>
    <ExecutionTimeLimit>$limit</ExecutionTimeLimit>
    <Priority>7</Priority>
  </Settings>
  <Actions Context="Author">
    <Exec>
      <Command>$(ConvertTo-XmlText $command)</Command>
      <Arguments>$(ConvertTo-XmlText $arguments)</Arguments>
      <WorkingDirectory>$(ConvertTo-XmlText $workDir)</WorkingDirectory>
    </Exec>
  </Actions>
</Task>
"@
}

function Get-TaskStatusObject {
    $task = Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
    if (-not $task) {
        return [pscustomobject]@{
            TaskName = $taskName
            Installed = $false
        }
    }

    $info = Get-ScheduledTaskInfo -TaskName $taskName -ErrorAction SilentlyContinue
    [pscustomobject]@{
        TaskName = $taskName
        Installed = $true
        State = $task.State
        LastRunTime = if ($info) { $info.LastRunTime } else { $null }
        LastTaskResult = if ($info) { $info.LastTaskResult } else { $null }
        NextRunTime = if ($info) { $info.NextRunTime } else { $null }
        IntervalMinutes = $IntervalMinutes
        LogonDelaySeconds = $LogonDelaySeconds
        ExecutionTimeLimitMinutes = $ExecutionTimeLimitMinutes
        ScriptPath = $scriptPath
    }
}

switch ($Action) {
    "Install" {
        $xml = Get-TaskDefinitionXml
        Register-ScheduledTask -TaskName $taskName -Xml $xml -Force | Out-Null
        Start-ScheduledTask -TaskName $taskName
        Get-TaskStatusObject
    }
    "Uninstall" {
        $task = Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
        if ($task) {
            Unregister-ScheduledTask -TaskName $taskName -Confirm:$false
        }
        Get-TaskStatusObject
    }
    "RunNow" {
        $task = Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
        if (-not $task) {
            throw "Task is not installed: $taskName"
        }
        Start-ScheduledTask -TaskName $taskName
        Get-TaskStatusObject
    }
    "Status" {
        Get-TaskStatusObject
    }
}
