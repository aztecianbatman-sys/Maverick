$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$core = Join-Path $root "core/Maverick.Core.csproj"
$output = Join-Path $root "dist-core"

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "Maverick Core requires the .NET 8 SDK. Install the .NET 8 SDK, then run npm run launch again."
}

$sdks = & dotnet --list-sdks
if ($LASTEXITCODE -ne 0 -or -not ($sdks -match "^8.")) {
    throw "Maverick Core requires a .NET 8 SDK. Installed SDKs: $($sdks -join ', ')"
}

if (Test-Path $output) {
    Remove-Item $output -Recurse -Force
}

dotnet restore $core
if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed." }

dotnet publish $core --configuration Release --self-contained false --output $output
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed." }

$files = Get-ChildItem -Path $output -File
if ($files.Count -eq 0) { throw "Core publish produced no files." }

$manifest = $files |
    Where-Object { $_.Name -ne "integrity.json" } |
    ForEach-Object {
        [pscustomobject]@{
            Path = $_.FullName
            Sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName).Hash.ToLowerInvariant()
        }
    }

$manifestPath = Join-Path $output "integrity.json"
$manifest | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath $manifestPath -Encoding UTF8

Write-Host "Maverick Core published successfully to $output" -ForegroundColor Green
