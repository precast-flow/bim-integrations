# Release build for AutoCAD NETLOAD (x64). Run after every code change.
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Push-Location (Join-Path $root "src/BimPrefabExport")
try {
    dotnet build -c Release -p:Platform=x64 @args
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    Write-Host "OK: BimPrefabExport.dll (Release, x64)"
}
finally {
    Pop-Location
}
