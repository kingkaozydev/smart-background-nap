param(
    [ValidateSet("Install", "Uninstall", "Status", "RunNow")]
    [string]$Action = "Status",

    [string]$ConfigPath = (Join-Path $PSScriptRoot "game-session.config.json")
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $ConfigPath)) {
    throw "Config not found: $ConfigPath"
}

$config = Get-Content -LiteralPath $ConfigPath -Raw | ConvertFrom-Json
if (-not $config.Tray) {
    throw "Tray config missing."
}

$taskName = [string]$config.Tray.TaskName
$scriptPath = Join-Path $PSScriptRoot "smart-background-nap-tray.ps1"
if (-not (Test-Path -LiteralPath $scriptPath)) {
    throw "Tray script not found: $scriptPath"
}
$appExePath = Join-Path $PSScriptRoot "bin\SmartBackgroundNap.exe"
$exePath = Join-Path $PSScriptRoot "bin\SmartBackgroundNapTray.exe"
$autoTaskName = [string]$config.Automation.TaskName
$managerPath = Join-Path $PSScriptRoot "manage-background-nap.ps1"
$workspace = $PSScriptRoot
$logPath = Join-Path $workspace "outputs\background-nap-auto.log"
$readmePath = Join-Path $PSScriptRoot "README.md"
$iconPath = Join-Path $PSScriptRoot ([string]$config.Tray.IconPath)

function ConvertTo-XmlText {
    param([string]$Text)
    return [System.Security.SecurityElement]::Escape($Text)
}

function Get-TrayTaskDefinitionXml {
    $sid = [System.Security.Principal.WindowsIdentity]::GetCurrent().User.Value
    $author = "$env:USERDOMAIN\$env:USERNAME"
    $workDir = $PSScriptRoot
    if (Test-Path -LiteralPath $appExePath) {
        $command = $appExePath
        $arguments = '--tray'
    } elseif (Test-Path -LiteralPath $exePath) {
        $command = $exePath
        $arguments = '--auto-task "' + $autoTaskName + '" --manager "' + $managerPath + '" --log "' + $logPath + '" --folder "' + $PSScriptRoot + '" --readme "' + $readmePath + '" --icon "' + $iconPath + '"'
    } else {
        $command = "powershell.exe"
        $arguments = '-NoProfile -STA -ExecutionPolicy Bypass -WindowStyle Hidden -File "' + $scriptPath + '"'
    }

@"
<?xml version="1.0" encoding="UTF-16"?>
<Task version="1.4" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
  <RegistrationInfo>
    <Author>$(ConvertTo-XmlText $author)</Author>
    <Description>Smart Background Nap tray indicator. Shows automation status and quick actions.</Description>
  </RegistrationInfo>
  <Triggers>
    <LogonTrigger>
      <Enabled>true</Enabled>
      <Delay>PT20S</Delay>
    </LogonTrigger>
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
    <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
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

function Get-TrayStatusObject {
    $task = Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
    $trayProcess = @()
    $trayProcess += @(Get-CimInstance Win32_Process -Filter "Name = 'powershell.exe'" -ErrorAction SilentlyContinue |
        Where-Object { $_.CommandLine -like "*smart-background-nap-tray.ps1*" })
    $trayProcess += @(Get-CimInstance Win32_Process -Filter "Name = 'SmartBackgroundNap.exe'" -ErrorAction SilentlyContinue |
        Where-Object { $_.CommandLine -like "*--tray*" })
    $trayProcess += @(Get-CimInstance Win32_Process -Filter "Name = 'SmartBackgroundNapTray.exe'" -ErrorAction SilentlyContinue)

    if (-not $task) {
        return [pscustomobject]@{
            TaskName = $taskName
            Installed = $false
            TrayProcessCount = $trayProcess.Count
            LaunchPath = if (Test-Path -LiteralPath $appExePath) { $appExePath } elseif (Test-Path -LiteralPath $exePath) { $exePath } else { $scriptPath }
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
        TrayProcessCount = $trayProcess.Count
        LaunchPath = if (Test-Path -LiteralPath $appExePath) { $appExePath } elseif (Test-Path -LiteralPath $exePath) { $exePath } else { $scriptPath }
    }
}

switch ($Action) {
    "Install" {
        $xml = Get-TrayTaskDefinitionXml
        Register-ScheduledTask -TaskName $taskName -Xml $xml -Force | Out-Null
        Start-ScheduledTask -TaskName $taskName
        Start-Sleep -Seconds 2
        Get-TrayStatusObject
    }
    "Uninstall" {
        $task = Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
        if ($task) {
            Stop-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
            Unregister-ScheduledTask -TaskName $taskName -Confirm:$false
        }

        $trayProcesses = @(Get-CimInstance Win32_Process -Filter "Name = 'powershell.exe'" -ErrorAction SilentlyContinue |
            Where-Object { $_.CommandLine -like "*smart-background-nap-tray.ps1*" })
        $trayProcesses += @(Get-CimInstance Win32_Process -Filter "Name = 'SmartBackgroundNap.exe'" -ErrorAction SilentlyContinue |
            Where-Object { $_.CommandLine -like "*--tray*" })
        $trayProcesses += @(Get-CimInstance Win32_Process -Filter "Name = 'SmartBackgroundNapTray.exe'" -ErrorAction SilentlyContinue)
        foreach ($proc in $trayProcesses) {
            Stop-Process -Id $proc.ProcessId -Force -ErrorAction SilentlyContinue
        }

        Get-TrayStatusObject
    }
    "RunNow" {
        $task = Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
        if (-not $task) {
            throw "Tray task is not installed: $taskName"
        }
        Start-ScheduledTask -TaskName $taskName
        Start-Sleep -Seconds 2
        Get-TrayStatusObject
    }
    "Status" {
        Get-TrayStatusObject
    }
}
