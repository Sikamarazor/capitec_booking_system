#!/usr/bin/env bash
# Run API and Angular dev server without Docker. Stop with Ctrl+C.
set -euo pipefail
cd "$(dirname "$0")/.."

command -v dotnet >/dev/null || { echo "ERROR: .NET SDK 10 not found (https://dotnet.microsoft.com/download)"; exit 1; }
command -v node   >/dev/null || { echo "ERROR: Node.js 22.12+ not found (https://nodejs.org)"; exit 1; }

if [[ -z "${ConnectionStrings__BranchBooking:-}" ]] \
   && ! dotnet user-secrets list --project src/BranchBooking.Api 2>/dev/null | grep -q "ConnectionStrings:BranchBooking"; then
  echo "ERROR: no database connection string configured."
  echo "Set ConnectionStrings__BranchBooking or use user-secrets: see docs/engineering/local-postgres.md"
  exit 1
fi

cleanup() { kill 0 2>/dev/null || true; }
trap cleanup EXIT INT TERM

echo "Starting API on http://localhost:5080 ..."
dotnet run --project src/BranchBooking.Api --launch-profile http &

echo "Starting web on http://localhost:4200 ..."
(cd frontend/branchbooking-web && npm ci && npm start) &

wait
