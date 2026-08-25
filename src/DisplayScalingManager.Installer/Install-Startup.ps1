<#
.SYNOPSIS
    Registers Display Scaling Manager to run at logon via Task Scheduler.

.DESCRIPTION
    Deployment convenience only — no application logic lives here. Registers a Scheduled
    Task with an "At log on" trigger for the current user, running at normal (non-elevated)
    rights, pointed at the published single-file executable.

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
$trigger = New-ScheduledTaskTrigger -AtLogOn -User $env:USERNAME
$settings = New-ScheduledTaskSettingsSet `
    -AllowStartIfOnBatteries `
    -MultipleInstances IgnoreNew `
    -StartWhenAvailable

Register-ScheduledTask -TaskName $taskName -Action $action -Trigger $trigger -Settings $settings `
    -RunLevel Limited -Force | Out-Null

Write-Host "Registered scheduled task '$taskName' to launch '$ExePath' at logon for $env:USERNAME."
