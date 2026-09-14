$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

if ($env:OS -ne "Windows_NT") {
    throw "Maverick currently requires Windows."
}

function Has-Command([string]$name) {
    return $null -ne (Get-Command $name -ErrorAction SilentlyContinue)
}

function Refresh-DotnetPath {
    $candidates = @(
        "$env:ProgramFiles\dotnet",
        "$env:ProgramFiles\dotnet\x64",
        "$env:LOCALAPPDATA\Microsoft\dotnet"
    )

    foreach ($dir in $candidates) {
        if (Test-Path (Join-Path $dir "dotnet.exe")) {
            if ($env:PATH -notlike "*$dir*") {
                $env:PATH = "$dir;$env:PATH"
            }
            return
        }
    }
}

if (-not (Has-Command "node")) {
    Write-Host "Node.js is required. Install Node.js, then run this launcher again." -ForegroundColor Red
    exit 1
}

Refresh-DotnetPath

if (-not (Has-Command "dotnet") -or -not ((& dotnet --list-sdks 2>$null) -match "^8\.") ) {
    if (Has-Command "winget") {
        Write-Host "Installing the required .NET 8 SDK with WinGet..." -ForegroundColor Cyan
        winget install --id Microsoft.DotNet.SDK.8 --exact --source winget --accept-source-agreements --accept-package-agreements
        if ($LASTEXITCODE -ne 0) {
            Write-Host "The .NET 8 SDK installation was not completed." -ForegroundColor Red
            exit $LASTEXITCODE
        }

        Refresh-DotnetPath
    }

    if (-not (Has-Command "dotnet") -or -not ((& dotnet --list-sdks 2>$null) -match "^8\.") ) {
        Write-Host ""
        Write-Host "Maverick needs the .NET 8 SDK." -ForegroundColor Red
        Write-Host "WinGet could not make it available in this terminal." -ForegroundColor Yellow
        Write-Host "Restart PowerShell once, then run: npm run launch" -ForegroundColor Cyan
        Write-Host ""
        exit 1
    }
}

if (-not (Test-Path "node_modules")) {
    Write-Host "Installing Maverick dependencies..." -ForegroundColor Cyan
    npm install
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

# Some modern npm installations require explicit approval for package install scripts.
$electronExe = Join-Path $root "node_modules\electron\dist\electron.exe"
if (-not (Test-Path $electronExe)) {
    Write-Host "Enabling required local package install scripts..." -ForegroundColor Cyan
    try {
        npm approve-scripts electron esbuild electron-winstaller
    } catch {
        # Older npm versions do not support approve-scripts.
    }

    Write-Host "Preparing Electron..." -ForegroundColor Cyan
    npm rebuild electron --foreground-scripts
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

Write-Host ""
Write-Host "Building Maverick Core..." -ForegroundColor Cyan
npm run core:build
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$service = Get-Service -Name "Maverick Core" -ErrorAction SilentlyContinue

if ($null -eq $service) {
    Write-Host "Installing Maverick Core service (Administrator permission required once)..." -ForegroundColor Yellow
    $installer = Join-Path $root "scripts\install-core.ps1"
    Start-Process powershell.exe -Verb RunAs -ArgumentList @(
        "-NoProfile",
        "-ExecutionPolicy","Bypass",
        "-File",$installer
    ) -Wait
    $service = Get-Service -Name "Maverick Core" -ErrorAction SilentlyContinue
}

if ($null -ne $service -and $service.Status -ne "Running") {
    Write-Host "Starting Maverick Core..." -ForegroundColor Cyan
    try {
        Start-Service -Name "Maverick Core"
    } catch {
        Write-Host "Core start failed: $($_.Exception.Message)" -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "Launching full Base Maverick..." -ForegroundColor Green
npm start
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
