$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$core = Join-Path $root "core/Maverick.Core.csproj"
$output = Join-Path $root "dist-core"

dotnet build $core --configuration Release
dotnet publish $core --configuration Release --self-contained false --output $output
Write-Host "Core output: $output"
