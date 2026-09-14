$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$core = Join-Path $root "core/Maverick.Core.csproj"
$output = Join-Path $root "dist-core"

if (Test-Path $output) {
    Remove-Item $output -Recurse -Force
}

dotnet restore $core
dotnet publish $core --configuration Release --self-contained false --output $output

$manifest = Get-ChildItem -Path $output -File |
    Where-Object { $_.Name -ne "integrity.json" } |
    ForEach-Object {
        [pscustomobject]@{
            Path = $_.FullName
            Sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName).Hash.ToLowerInvariant()
        }
    }

$manifestPath = Join-Path $output "integrity.json"
$manifest | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath $manifestPath -Encoding UTF8

Write-Host "Core published to $output"
Write-Host "Integrity manifest written to $manifestPath"
