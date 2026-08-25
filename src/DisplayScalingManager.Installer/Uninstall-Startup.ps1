<#
.SYNOPSIS
    Removes the Display Scaling Manager logon scheduled task.

.PARAMETER RemoveData
    Also deletes %LOCALAPPDATA%\DisplayScalingManager (config, state, and logs).
#>
[CmdletBinding()]
param(
    [switch]$RemoveData
)

$ErrorActionPreference = "Stop"
$taskName = "DisplayScalingManager"

$task = Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
if ($task) {
    Unregister-ScheduledTask -TaskName $taskName -Confirm:$false
    Write-Host "Removed scheduled task '$taskName'."
} else {
    Write-Host "Scheduled task '$taskName' was not registered."
}

if ($RemoveData) {
    $dataDir = Join-Path $env:LOCALAPPDATA "DisplayScalingManager"
    if (Test-Path $dataDir) {
        Remove-Item -Path $dataDir -Recurse -Force
        Write-Host "Removed data directory '$dataDir'."
    }
}
