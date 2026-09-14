$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

if ($env:OS -ne "Windows_NT") { throw "Maverick full launch currently requires Windows." }
if (-not (Get-Command node -ErrorAction SilentlyContinue)) { throw "Node.js is required." }
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw ".NET SDK is required for Maverick Core." }

if (-not (Test-Path "node_modules")) {
  Write-Host "Installing Maverick dependencies..." -ForegroundColor Cyan
  npm install
  if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

Write-Host "Building Maverick Core..." -ForegroundColor Cyan
npm run core:build
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$service = Get-Service -Name "Maverick Core" -ErrorAction SilentlyContinue
if ($null -eq $service) {
  Write-Host "Installing Maverick Core service (Administrator permission required)..." -ForegroundColor Yellow
  $installer = Join-Path $root "scripts\install-core.ps1"
  Start-Process powershell.exe -Verb RunAs -ArgumentList @("-NoProfile","-ExecutionPolicy","Bypass","-File",$installer) -Wait
  $service = Get-Service -Name "Maverick Core" -ErrorAction SilentlyContinue
}

if ($null -ne $service -and $service.Status -ne "Running") {
  Write-Host "Starting Maverick Core..." -ForegroundColor Cyan
  try { Start-Service -Name "Maverick Core" } catch { Write-Host "Core start failed: $($_.Exception.Message)" -ForegroundColor Yellow }
}

Write-Host "Launching full Base Maverick..." -ForegroundColor Green
npm start
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }