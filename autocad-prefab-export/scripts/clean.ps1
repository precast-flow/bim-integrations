# Remove all BimPrefabExport build artifacts (local obj/bin + Parallels redirect cache).
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$projectDir = Join-Path $root "src/BimPrefabExport"

foreach ($dir in @("obj", "bin")) {
    $path = Join-Path $projectDir $dir
    if (Test-Path $path) {
        Remove-Item -LiteralPath $path -Recurse -Force
        Write-Host "Removed $path"
    }
}

$redirectRoot = Join-Path $env:LOCALAPPDATA "BimPrefabExport/build"
if (Test-Path $redirectRoot) {
    Remove-Item -LiteralPath $redirectRoot -Recurse -Force
    Write-Host "Removed $redirectRoot"
}

Write-Host "Clean complete. Run .\scripts\build.ps1 to rebuild."
