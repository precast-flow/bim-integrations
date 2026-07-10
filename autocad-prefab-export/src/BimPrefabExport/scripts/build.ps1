# Wrapper: run from src/BimPrefabExport. Root script: ../../../scripts/build.ps1
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))
& (Join-Path $root "scripts/build.ps1") @args
