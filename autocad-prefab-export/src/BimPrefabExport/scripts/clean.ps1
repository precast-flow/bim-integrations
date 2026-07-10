# Wrapper: run from src/BimPrefabExport. Root script: ../../../scripts/clean.ps1
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))
& (Join-Path $root "scripts/clean.ps1") @args
