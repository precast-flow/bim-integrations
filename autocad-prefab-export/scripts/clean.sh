#!/usr/bin/env bash
# Remove all BimPrefabExport build artifacts (local obj/bin).
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
project_dir="$root/src/BimPrefabExport"

for dir in obj bin; do
  if [[ -d "$project_dir/$dir" ]]; then
    rm -rf "$project_dir/$dir"
    echo "Removed $project_dir/$dir"
  fi
done

echo "Clean complete. Run ./scripts/build.sh to rebuild."
