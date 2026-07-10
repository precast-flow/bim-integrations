#!/usr/bin/env bash
# Wrapper: run from src/BimPrefabExport. Root script: ../../../scripts/clean.sh
set -euo pipefail
root="$(cd "$(dirname "$0")/../../../" && pwd)"
exec "$root/scripts/clean.sh" "$@"
