#Requires -RunAsAdministrator
[CmdletBinding(SupportsShouldProcess)]
param (
    [string]$ServiceName = "SystemMonitorAgent",
    [switch]$RemoveEventLogSource
)

$ErrorActionPreference = "Stop"

Write-Host "Uninstalling service $ServiceName"

$service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue

if ($service) {
    if ($service.Status -eq 'Running') {
        if ($PSCmdlet.ShouldProcess("Service '$ServiceName'", "Stop")) {
            Stop-Service -Name $ServiceName -Force
            Write-Host "Stopped service $ServiceName."
        }
    }
    
    if ($PSCmdlet.ShouldProcess("Service '$ServiceName'", "Remove")) {
        $wmiService = Get-WmiObject -Class Win32_Service -Filter "Name='$ServiceName'"
        if ($wmiService) { $wmiService.Delete() | Out-Null }
        Write-Host "Service $ServiceName removed successfully."
    }
} else {
    Write-Host "Service $ServiceName is not installed."
}

if ($RemoveEventLogSource) {
    if ($PSCmdlet.ShouldProcess("Event Log Source '$ServiceName'", "Remove")) {
        if ([System.Diagnostics.EventLog]::SourceExists($ServiceName)) {
            Remove-EventLog -Source $ServiceName
            Write-Host "Removed Event Log source '$ServiceName'."
        }
    }
}
