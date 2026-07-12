param(
    [string]$ConfigPath = (Join-Path $PSScriptRoot "game-session.config.json")
)

$ErrorActionPreference = "Continue"

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

if (-not (Test-Path -LiteralPath $ConfigPath)) {
    [System.Windows.Forms.MessageBox]::Show("Config not found: $ConfigPath", "Smart Background Nap", "OK", "Error") | Out-Null
    exit 1
}

$config = Get-Content -LiteralPath $ConfigPath -Raw | ConvertFrom-Json
$creatorLine = "Criado por KaozyKing | Instagram: @oeduardomacedo | GitHub: kingkaozydev"
$autoTaskName = [string]$config.Automation.TaskName
$trayTaskName = [string]$config.Tray.TaskName
$refreshSeconds = [int]$config.Tray.RefreshSeconds
if ($refreshSeconds -lt 10) { $refreshSeconds = 10 }

$workspace = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$outDir = Join-Path $workspace "outputs"
$logPath = Join-Path $outDir "background-nap-auto.log"
$mainScriptPath = Join-Path $PSScriptRoot "background-nap.ps1"
$managerScriptPath = Join-Path $PSScriptRoot "manage-background-nap.ps1"

$iconPath = Join-Path $PSScriptRoot ([string]$config.Tray.IconPath)
if (-not (Test-Path -LiteralPath $iconPath)) {
    $iconGenerator = Join-Path $PSScriptRoot "new-smart-background-nap-icon.ps1"
    if (Test-Path -LiteralPath $iconGenerator) {
        & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $iconGenerator | Out-Null
    }
}

function Get-AutoTaskStatus {
    $task = Get-ScheduledTask -TaskName $autoTaskName -ErrorAction SilentlyContinue
    if (-not $task) {
        return [pscustomobject]@{
            Installed = $false
            State = "NotInstalled"
            LastRunTime = $null
            LastTaskResult = $null
            NextRunTime = $null
        }
    }

    $info = Get-ScheduledTaskInfo -TaskName $autoTaskName -ErrorAction SilentlyContinue
    [pscustomobject]@{
        Installed = $true
        State = [string]$task.State
        LastRunTime = if ($info) { $info.LastRunTime } else { $null }
        LastTaskResult = if ($info) { $info.LastTaskResult } else { $null }
        NextRunTime = if ($info) { $info.NextRunTime } else { $null }
    }
}

function Get-LastLogLine {
    if (-not (Test-Path -LiteralPath $logPath)) {
        return "No log yet."
    }

    $line = Get-Content -LiteralPath $logPath -Tail 1 -ErrorAction SilentlyContinue
    if (-not $line) { return "No log yet." }
    return [string]$line
}

function Show-Status {
    $status = Get-AutoTaskStatus
    $lastLog = Get-LastLogLine
    $message = @(
        "Smart Background Nap",
        $creatorLine,
        "",
        "Auto task: $($status.Installed)",
        "State: $($status.State)",
        "Last result: $($status.LastTaskResult)",
        "Last run: $($status.LastRunTime)",
        "Next run: $($status.NextRunTime)",
        "",
        "Last log:",
        $lastLog
    ) -join [Environment]::NewLine

    [System.Windows.Forms.MessageBox]::Show($message, "Smart Background Nap", "OK", "Information") | Out-Null
}

function Invoke-ApplyNow {
    $status = Get-AutoTaskStatus
    if ($status.Installed) {
        Start-ScheduledTask -TaskName $autoTaskName
    } else {
        Start-Process -FilePath "powershell.exe" -ArgumentList @(
            "-NoProfile",
            "-ExecutionPolicy", "Bypass",
            "-WindowStyle", "Hidden",
            "-File", "`"$mainScriptPath`"",
            "-Action", "Apply",
            "-StateMode", "Latest",
            "-Quiet"
        ) -WindowStyle Hidden
    }

    $script:notifyIcon.BalloonTipTitle = "Smart Background Nap"
    $script:notifyIcon.BalloonTipText = "Apply requested."
    $script:notifyIcon.ShowBalloonTip(2500)
}

function Open-Log {
    if (-not (Test-Path -LiteralPath $logPath)) {
        New-Item -ItemType File -Path $logPath -Force | Out-Null
    }
    Start-Process -FilePath "notepad.exe" -ArgumentList "`"$logPath`""
}

function Open-Folder {
    Start-Process -FilePath "explorer.exe" -ArgumentList "`"$PSScriptRoot`""
}

function Open-Readme {
    $readme = Join-Path $PSScriptRoot "README.md"
    if (Test-Path -LiteralPath $readme) {
        Start-Process -FilePath "notepad.exe" -ArgumentList "`"$readme`""
    }
}

function Update-Tooltip {
    $status = Get-AutoTaskStatus
    $stateText = if ($status.Installed) { $status.State } else { "not installed" }
    $next = ""
    if ($status.NextRunTime) {
        $next = " next " + $status.NextRunTime.ToString("HH:mm")
    }
    $text = "Smart Background Nap: $stateText$next"
    if ($text.Length -gt 63) {
        $text = $text.Substring(0, 63)
    }
    $script:notifyIcon.Text = $text
}

$script:notifyIcon = New-Object System.Windows.Forms.NotifyIcon
if (Test-Path -LiteralPath $iconPath) {
    $script:notifyIcon.Icon = New-Object System.Drawing.Icon($iconPath)
} else {
    $script:notifyIcon.Icon = [System.Drawing.SystemIcons]::Application
}
$script:notifyIcon.Visible = $true

$menu = New-Object System.Windows.Forms.ContextMenuStrip

$title = New-Object System.Windows.Forms.ToolStripMenuItem("Smart Background Nap")
$title.Enabled = $false
[void]$menu.Items.Add($title)
$creator = New-Object System.Windows.Forms.ToolStripMenuItem($creatorLine)
$creator.Enabled = $false
[void]$menu.Items.Add($creator)
[void]$menu.Items.Add((New-Object System.Windows.Forms.ToolStripSeparator))

$statusItem = New-Object System.Windows.Forms.ToolStripMenuItem("Status")
$statusItem.Add_Click({ Show-Status })
[void]$menu.Items.Add($statusItem)

$applyItem = New-Object System.Windows.Forms.ToolStripMenuItem("Apply now")
$applyItem.Add_Click({ Invoke-ApplyNow })
[void]$menu.Items.Add($applyItem)

$logItem = New-Object System.Windows.Forms.ToolStripMenuItem("Open log")
$logItem.Add_Click({ Open-Log })
[void]$menu.Items.Add($logItem)

$folderItem = New-Object System.Windows.Forms.ToolStripMenuItem("Open folder")
$folderItem.Add_Click({ Open-Folder })
[void]$menu.Items.Add($folderItem)

$readmeItem = New-Object System.Windows.Forms.ToolStripMenuItem("Open README")
$readmeItem.Add_Click({ Open-Readme })
[void]$menu.Items.Add($readmeItem)

[void]$menu.Items.Add((New-Object System.Windows.Forms.ToolStripSeparator))

$exitItem = New-Object System.Windows.Forms.ToolStripMenuItem("Exit tray icon")
$exitItem.Add_Click({
    $script:notifyIcon.Visible = $false
    $script:notifyIcon.Dispose()
    [System.Windows.Forms.Application]::Exit()
})
[void]$menu.Items.Add($exitItem)

$script:notifyIcon.ContextMenuStrip = $menu
$script:notifyIcon.Add_DoubleClick({ Show-Status })

$timer = New-Object System.Windows.Forms.Timer
$timer.Interval = $refreshSeconds * 1000
$timer.Add_Tick({ Update-Tooltip })
$timer.Start()

Update-Tooltip

$script:notifyIcon.BalloonTipTitle = "Smart Background Nap"
$script:notifyIcon.BalloonTipText = "Tray indicator active."
$script:notifyIcon.ShowBalloonTip(2000)

[System.Windows.Forms.Application]::Run()

$timer.Stop()
$timer.Dispose()
$script:notifyIcon.Visible = $false
$script:notifyIcon.Dispose()
