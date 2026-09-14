$ErrorActionPreference = "Stop"

if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Run this script from an elevated PowerShell window."
}

$root = Split-Path -Parent $PSScriptRoot
$coreExe = Join-Path $root "dist-core/Maverick.Core.exe"
$manifest = Join-Path $root "dist-core/integrity.json"
$programDataRoot = Join-Path $env:ProgramData "Maverick"
$installedManifest = Join-Path $programDataRoot "integrity.json"

if (-not (Test-Path $coreExe)) { throw "Maverick.Core.exe was not found. Run scripts/build-core.ps1 first." }
if (-not (Test-Path $manifest)) { throw "integrity.json was not found. Run scripts/build-core.ps1 first." }

$serviceName = "Maverick Core"
sc.exe stop "$serviceName" | Out-Null
sc.exe delete "$serviceName" | Out-Null
Start-Sleep -Milliseconds 700

sc.exe create "$serviceName" binPath= "`"$coreExe`"" obj= "NT AUTHORITYLocalService" start= auto DisplayName= "Maverick Core" | Out-Host
sc.exe sidtype "$serviceName" restricted | Out-Host
sc.exe failure "$serviceName" reset= 86400 actions= restart/5000/restart/15000/restart/30000 | Out-Host

New-Item -ItemType Directory -Force -Path $programDataRoot | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $programDataRoot "Quarantine") | Out-Null

icacls $programDataRoot /inheritance:r | Out-Null
icacls $programDataRoot /grant:r "*S-1-5-18:(OI)(CI)(F)" "*S-1-5-32-544:(OI)(CI)(F)" "NT SERVICEMaverick Core:(OI)(CI)(M)" | Out-Null

Copy-Item -LiteralPath $manifest -Destination $installedManifest -Force

icacls $installedManifest /inheritance:r | Out-Null
icacls $installedManifest /grant:r "*S-1-5-18:(F)" "*S-1-5-32-544:(F)" "NT SERVICEMaverick Core:(R)" | Out-Null

sc.exe start "$serviceName" | Out-Host

Write-Host "Maverick Core installed as LocalService with a restricted service SID."
Write-Host "Protected storage: $programDataRoot"