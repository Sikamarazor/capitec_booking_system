#!/usr/bin/env bash
# Starts the stack in its own compose project and ports, runs the browser tests, stops the stack.
# Usage: scripts/e2e.sh [playwright arguments, for example --project=desktop -g smoke]
# Never touches a running development stack: different project name, different ports, its own database volume.
set -euo pipefail

cd "$(dirname "$0")/.."
root="$PWD"

export WEB_PORT="${E2E_WEB_PORT:-18080}"
export API_PORT="${E2E_API_PORT:-15080}"
export POSTGRES_PORT="${E2E_POSTGRES_PORT:-15432}"
compose=(docker compose -p branchbooking-e2e -f docker-compose.yml -f docker-compose.e2e.yml)

created_env=0
if [[ ! -f .env ]]; then
  printf 'POSTGRES_PASSWORD=%s\n' "$(openssl rand -hex 16)" > .env
  created_env=1
fi

cleanup() {
  local status=$1
  # Playwright runs from e2e/, but Compose files, logs and .env are relative to the repository root.
  cd "$root" || return
  if [[ "$status" -ne 0 ]]; then
    "${compose[@]}" logs --no-color > e2e/compose.log 2>&1 || true
    echo "Compose logs saved to e2e/compose.log"
  fi
  "${compose[@]}" down -v --remove-orphans >/dev/null 2>&1 || true
  if [[ "$created_env" -eq 1 ]]; then rm -f .env; fi
}
trap 'cleanup $?' EXIT

"${compose[@]}" up --build -d --wait --wait-timeout 300
scripts/verify-foundation.sh "http://localhost:$WEB_PORT" "http://localhost:$WEB_PORT"

cd e2e
npm ci
npx playwright install ${CI:+--with-deps} chromium
E2E_BASE_URL="http://localhost:$WEB_PORT" npx playwright test "$@"
