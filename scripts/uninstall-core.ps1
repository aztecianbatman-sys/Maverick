$ErrorActionPreference = "Stop"

if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Run this script from an elevated PowerShell window."
}

$serviceName = "Maverick Core"
sc.exe stop "$serviceName" | Out-Null
sc.exe delete "$serviceName"
Write-Host "Maverick Core service removed."
