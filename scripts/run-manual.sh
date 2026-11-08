#!/usr/bin/env bash
# Run API, notification Worker and Angular without Docker. Stop with Ctrl+C.
# --mailpit also starts native Mailpit and sends notifications to its local inbox.
set -euo pipefail
cd "$(dirname "$0")/.."

with_mailpit=0
case "${1:-}" in
  --mailpit) with_mailpit=1; shift ;;
  --help|-h) echo "Usage: scripts/run-manual.sh [--mailpit]"; exit 0 ;;
esac
[[ $# -eq 0 ]] || { echo "Usage: scripts/run-manual.sh [--mailpit]" >&2; exit 2; }

for tool in dotnet node npm pgrep; do
  command -v "$tool" >/dev/null || { echo "ERROR: $tool not found. Install .NET SDK 10 and Node.js 22.12+ (and pgrep)." >&2; exit 1; }
done
if [[ "$with_mailpit" -eq 1 ]]; then
  command -v mailpit >/dev/null || { echo "ERROR: install the native Mailpit executable and add it to PATH. See README.md, Mailpit without Docker." >&2; exit 1; }
fi

# API and Worker have separate user-secret stores; an exported connection string is shared by both.
for project in src/BranchBooking.Api src/BranchBooking.Worker; do
  if [[ -z "${ConnectionStrings__BranchBooking:-}" ]] \
     && ! dotnet user-secrets list --project "$project" 2>/dev/null | grep -q "ConnectionStrings:BranchBooking"; then
    echo "ERROR: no database connection string configured for $project."
    echo "Export ConnectionStrings__BranchBooking for both processes, or configure each project's user-secrets."
    exit 1
  fi
done

if [[ "$with_mailpit" -eq 1 ]]; then
  export Notifications__Channel=Smtp
  export Notifications__PublicBaseUrl=http://localhost:4200
  export Notifications__Smtp__Host=127.0.0.1 Notifications__Smtp__Port=1025
  export Notifications__Smtp__Security=None
  export Notifications__Smtp__Username='' Notifications__Smtp__Password=''
  export Notifications__Smtp__SenderEmail=noreply@branchbooking.example
  export Notifications__Smtp__SenderName=BranchBooking
fi

(cd frontend/branchbooking-web && npm ci)
# Build shared projects sequentially before starting the two .NET processes.
dotnet build src/BranchBooking.Api
dotnet build src/BranchBooking.Worker
pids=()
stop_tree() {
  local pid="$1" child
  while IFS= read -r child; do
    [[ -n "$child" ]] && stop_tree "$child"
  done < <(pgrep -P "$pid" || true)
  kill "$pid" 2>/dev/null || true
}
cleanup() {
  trap - EXIT INT TERM
  echo "Shutting down the processes started by this launcher..."
  for pid in "${pids[@]}"; do stop_tree "$pid"; done
  for pid in "${pids[@]}"; do wait "$pid" 2>/dev/null || true; done
}
trap cleanup EXIT
trap 'exit 130' INT
trap 'exit 143' TERM

if [[ "$with_mailpit" -eq 1 ]]; then
  echo "Starting Mailpit: inbox http://localhost:8025, SMTP 127.0.0.1:1025 ..."
  mailpit --listen 127.0.0.1:8025 --smtp 127.0.0.1:1025 &
  pids+=("$!")
fi

echo "Starting API on http://localhost:5080 ..."
dotnet run --project src/BranchBooking.Api --launch-profile http --no-build &
pids+=("$!")
echo "Starting notification Worker ..."
dotnet run --project src/BranchBooking.Worker --no-build &
pids+=("$!")
echo "Starting web on http://localhost:4200 ..."
(cd frontend/branchbooking-web && exec npm start) &
pids+=("$!")

# Stop the other processes if any service exits, including a port conflict in Mailpit.
while true; do
  for pid in "${pids[@]}"; do
    if ! kill -0 "$pid" 2>/dev/null; then
      if wait "$pid"; then exit 0; else exit "$?"; fi
    fi
  done
  sleep 1
done
