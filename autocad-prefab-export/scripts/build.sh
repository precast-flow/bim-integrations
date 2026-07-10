#!/usr/bin/env bash
# Release build for AutoCAD NETLOAD (x64). Run after every code change.
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
cd "$root/src/BimPrefabExport"
dotnet build -c Release -p:Platform=x64 "$@"
echo "OK: BimPrefabExport.dll (Release, x64)"
