#Requires -RunAsAdministrator
[CmdletBinding(SupportsShouldProcess)]
param (
    [string]$ServiceName = "SystemMonitorAgent",
    [string]$BinaryPath,
    [string]$EventLogName = "Application"
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($BinaryPath)) {
    $BinaryPath = Join-Path $PSScriptRoot "..\src\SystemMonitorAgent\bin\Release\net8.0\win-x64\publish\SystemMonitorAgent.exe"
    if (-not (Test-Path $BinaryPath)) {
        # Try debug if release not found, for development purposes
        $BinaryPath = Join-Path $PSScriptRoot "..\src\SystemMonitorAgent\bin\Debug\net8.0\SystemMonitorAgent.exe"
    }
}

if (-not (Test-Path $BinaryPath)) {
    Write-Error "Could not find the SystemMonitorAgent executable. Path tested: $BinaryPath"
    exit 1
}

if ($PSCmdlet.ShouldProcess("Event Log Source '$ServiceName'", "Create")) {
    if (-not [System.Diagnostics.EventLog]::SourceExists($ServiceName)) {
        New-EventLog -LogName $EventLogName -Source $ServiceName
        Write-Host "Event Log Source '$ServiceName' created successfully."
    } else {
        Write-Host "Event Log Source '$ServiceName' already exists."
    }
}

if ($PSCmdlet.ShouldProcess("Service '$ServiceName'", "Install")) {
    Write-Host "Installing service $ServiceName from $BinaryPath"

    New-Service -Name $ServiceName -BinaryPathName $BinaryPath -DisplayName "System Monitor Agent" -Description "Monitors system metrics and sends them to a receiver." -StartupType Automatic
}

if ($PSCmdlet.ShouldProcess("Service '$ServiceName'", "Start")) {
    Start-Service -Name $ServiceName

    Write-Host "Service $ServiceName installed and started successfully."
}
