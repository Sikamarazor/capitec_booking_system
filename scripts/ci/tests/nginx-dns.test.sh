#!/usr/bin/env bash
# Exercise Docker DNS against a real web image, without requiring PostgreSQL or .NET images.
set -euo pipefail
image="${1:?Usage: nginx-dns.test.sh WEB_IMAGE}"
prefix="branchbooking-nginx-test-${RANDOM}-$$"
web="$prefix-web"
api="$prefix-api"
reserve="$prefix-reserve"
network="$prefix-network"
config="$(mktemp)"
cleanup() {
  local status=$?
  if [[ "$status" -ne 0 ]]; then docker logs --tail 40 "$web" 2>&1 || true; fi
  docker rm -f "$web" "$api" "$reserve" >/dev/null 2>&1 || true
  docker network rm "$network" >/dev/null 2>&1 || true
  rm -f "$config"
}
trap cleanup EXIT

docker network create "$network" >/dev/null
docker run -d --name "$web" --network "$network" "$image" >/dev/null
web_get() { docker exec "$web" wget -Y off -T 3 -qO- "http://127.0.0.1:8080$1" 2>/dev/null; }
wait_for() {
  local path="$1" expected="$2" attempt response
  for ((attempt=0; attempt<40; attempt++)); do
    response="$(web_get "$path" || true)"
    if [[ "$response" == *"$expected"* ]]; then return 0; fi
    sleep 0.5
  done
  echo "FAIL expected '$expected' in response for $path" >&2
  return 1
}
assert_web_running() {
  [[ "$(docker inspect --format '{{.State.Running}}' "$web")" == true ]]
}
start_api() {
  local marker="$1"
  cat > "$config" <<CONFIG
pid /tmp/backend.pid;
events {}
http {
  access_log off;
  server {
    listen 8080;
    location / { return 200 "$marker:\$request_uri"; }
  }
}
CONFIG
  chmod 644 "$config"
  docker create --name "$api" --network "$network" --network-alias api \
    --entrypoint nginx "$image" -g 'daemon off;' -c /tmp/backend.conf >/dev/null
  docker cp "$config" "$api:/tmp/backend.conf"
  docker start "$api" >/dev/null
}

wait_for / bb-root
assert_web_running
if web_get /api/v1/health >/dev/null; then
  echo 'FAIL API request should fail while api is absent' >&2
  exit 1
fi
assert_web_running
echo 'PASS web starts and stays running without an API'

path='/api/v1/echo?date=2026-11-10&value=a%2Fb'
start_api first
wait_for "$path" "first:$path"
echo 'PASS late API startup is resolved and the request path and query are preserved'

first_ip="$(docker inspect --format '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}' "$api")"
docker rm -f "$api" >/dev/null
# Occupy the old address so the replacement must be discovered at a new IP.
docker run -d --name "$reserve" --network "$network" --ip "$first_ip" \
  --entrypoint sh "$image" -c 'sleep 120' >/dev/null
if web_get /api/v1/health >/dev/null; then
  echo 'FAIL API request should fail while api is removed' >&2
  exit 1
fi
assert_web_running
start_api second
wait_for "$path" "second:$path"
wait_for / bb-root
assert_web_running
echo 'PASS API recreation recovers without restarting web'
