$ErrorActionPreference = "Stop"

if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Run this script from an elevated PowerShell window."
}

$root = Split-Path -Parent $PSScriptRoot
$source = Join-Path $root "dist-core"
$installRoot = Join-Path $env:ProgramFiles "Maverick\Core"
$coreExe = Join-Path $installRoot "Maverick.Core.exe"
$manifest = Join-Path $env:ProgramData "Maverick\integrity.json"

if (-not (Test-Path (Join-Path $source "Maverick.Core.exe"))) {
    throw "Maverick.Core.exe was not found. Run scripts/build-core.ps1 first."
}

$serviceName = "Maverick Core"
sc.exe stop "$serviceName" | Out-Null
sc.exe delete "$serviceName" | Out-Null
Start-Sleep -Milliseconds 700

New-Item -ItemType Directory -Force -Path $installRoot | Out-Null
Copy-Item -Path (Join-Path $source "*") -Destination $installRoot -Recurse -Force

New-Item -ItemType Directory -Force -Path (Join-Path $env:ProgramData "Maverick\Quarantine") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $env:ProgramData "Maverick") | Out-Null

$entries = Get-ChildItem -Path $installRoot -File -Recurse | ForEach-Object {
    [pscustomobject]@{
        Path = $_.FullName
        Sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName).Hash.ToLowerInvariant()
    }
}
$entries | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath $manifest -Encoding UTF8

sc.exe create "$serviceName" binPath= "`"$coreExe`"" obj= "NT AUTHORITY\LocalService" start= auto DisplayName= "Maverick Core" | Out-Host
sc.exe sidtype "$serviceName" restricted | Out-Host
sc.exe failure "$serviceName" reset= 86400 actions= restart/5000/restart/15000/restart/30000 | Out-Host

icacls $installRoot /inheritance:r | Out-Null
icacls $installRoot /grant:r "*S-1-5-18:(OI)(CI)(F)" "*S-1-5-32-544:(OI)(CI)(F)" "NT SERVICE\Maverick Core:(OI)(CI)(RX)" | Out-Null

$dataRoot = Join-Path $env:ProgramData "Maverick"
icacls $dataRoot /inheritance:r | Out-Null
icacls $dataRoot /grant:r "*S-1-5-18:(OI)(CI)(F)" "*S-1-5-32-544:(OI)(CI)(F)" "NT SERVICE\Maverick Core:(OI)(CI)(M)" | Out-Null

icacls $manifest /inheritance:r | Out-Null
icacls $manifest /grant:r "*S-1-5-18:(F)" "*S-1-5-32-544:(F)" "NT SERVICE\Maverick Core:(R)" | Out-Null

sc.exe start "$serviceName" | Out-Host

Write-Host "Maverick Core installed to $installRoot"
Write-Host "Service account: LocalService"
Write-Host "Service SID: Restricted"
Write-Host "Integrity manifest: $manifest"