#!/usr/bin/env bash
# Regenerating .xnb files requires Windows MGCB (effect processors).
set -euo pipefail
cd "$(dirname "$0")/.."

case "$(uname -s)" in
  MINGW*|MSYS*|CYGWIN*|Windows_NT)
    powershell -NoProfile -ExecutionPolicy Bypass -File "$(dirname "$0")/build-content.ps1"
    ;;
  *)
    echo "Prebuilt MonoGame content must be rebuilt on Windows."
    echo "Run: ./scripts/build-content.ps1"
    echo "Then commit Content/Prebuilt/"
    exit 1
    ;;
esac
