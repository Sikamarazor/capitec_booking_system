#!/usr/bin/env bash
# Fails when any NuGet package (direct or transitive) has a known vulnerability.
# `dotnet list package --vulnerable` always exits 0, so its output is read here. Run after `dotnet restore`.
set -euo pipefail

cd "$(dirname "$0")/../.."

if ! output="$(dotnet list BranchBooking.slnx package --vulnerable --include-transitive 2>&1)"; then
  echo "$output"
  echo "FAIL  dotnet list package failed"
  exit 1
fi

echo "$output"

if grep -q "has the following vulnerable packages" <<<"$output"; then
  echo "FAIL  vulnerable packages found (see above)"
  exit 1
fi

echo "PASS  no vulnerable packages reported"
