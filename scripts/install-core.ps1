$ErrorActionPreference = "Stop"

if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Run this script from an elevated PowerShell window."
}

$root = Split-Path -Parent $PSScriptRoot
$coreExe = Join-Path $root "dist-core/Maverick.Core.exe"

if (-not (Test-Path $coreExe)) {
    throw "Maverick.Core.exe was not found. Run scripts/build-core.ps1 first."
}

$serviceName = "Maverick Core"
sc.exe stop "$serviceName" | Out-Null
sc.exe delete "$serviceName" | Out-Null
Start-Sleep -Milliseconds 500
sc.exe create "$serviceName" binPath= $coreExe start= auto DisplayName= "Maverick Core"
sc.exe failure "$serviceName" reset= 86400 actions= restart/5000/restart/15000/restart/30000
sc.exe start "$serviceName"

Write-Host "Maverick Core service installed and started."
