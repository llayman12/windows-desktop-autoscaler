<#
.SYNOPSIS
    Registers Display Scaling Manager to run at logon via Task Scheduler.

.DESCRIPTION
    Deployment convenience only — no application logic lives here. Registers a Scheduled
    Task with "At log on" and "On workstation unlock" triggers for the current user, running
    at normal (non-elevated) rights, pointed at the published single-file executable.

    The unlock trigger exists because waking from sleep and unlocking is not a logon event —
    the app itself already reacts to SessionSwitch/DisplaySettingsChanged while running, but
    this covers the case where the process isn't running at all (crashed, task failed to
    launch, etc.) by giving Task Scheduler another chance to relaunch it. MultipleInstances
    IgnoreNew makes firing both triggers back-to-back (or firing unlock while already running)
    a safe no-op.

.PARAMETER ExePath
    Path to the published DisplayScalingManager.App.exe. Defaults to
    %LOCALAPPDATA%\DisplayScalingManager\app\DisplayScalingManager.App.exe.

.EXAMPLE
    .\Install-Startup.ps1 -ExePath "C:\Tools\DisplayScalingManager\DisplayScalingManager.App.exe"
#>
[CmdletBinding()]
param(
    [string]$ExePath = (Join-Path $env:LOCALAPPDATA "DisplayScalingManager\app\DisplayScalingManager.App.exe")
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $ExePath)) {
    throw "Executable not found at '$ExePath'. Publish the app first, or pass -ExePath explicitly."
}

$taskName = "DisplayScalingManager"

$action = New-ScheduledTaskAction -Execute $ExePath
$logonTrigger = New-ScheduledTaskTrigger -AtLogOn -User $env:USERNAME

# New-ScheduledTaskTrigger has no built-in "on unlock" switch; it requires building the
# MSFT_TaskSessionStateChangeTrigger CIM instance directly. StateChange 8 = TASK_SESSION_UNLOCK
# (see TASK_SESSION_STATE_CHANGE_TYPE, learn.microsoft.com/windows/win32/api/taskschd).
$unlockTriggerClass = Get-CimClass -ClassName MSFT_TaskSessionStateChangeTrigger -Namespace Root/Microsoft/Windows/TaskScheduler
$unlockTrigger = New-CimInstance -CimClass $unlockTriggerClass -ClientOnly -Property @{
    StateChange = 8
    UserId      = $env:USERNAME
    Enabled     = $true
}

$settings = New-ScheduledTaskSettingsSet `
    -AllowStartIfOnBatteries `
    -MultipleInstances IgnoreNew `
    -StartWhenAvailable

Register-ScheduledTask -TaskName $taskName -Action $action -Trigger @($logonTrigger, $unlockTrigger) `
    -Settings $settings -RunLevel Limited -Force | Out-Null

Write-Host "Registered scheduled task '$taskName' to launch '$ExePath' at logon and workstation unlock for $env:USERNAME."
