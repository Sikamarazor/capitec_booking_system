#!/usr/bin/env bash
# Usage: scripts/verify-foundation.sh [API_URL] [WEB_URL]
#   Docker stack:  scripts/verify-foundation.sh http://localhost:8080 http://localhost:8080
#   Manual run:    scripts/verify-foundation.sh http://localhost:5080 http://localhost:4200
set -uo pipefail
API_URL="${1:-http://localhost:8080}"
WEB_URL="${2:-http://localhost:8080}"
fail=0

check_status() { # name url expected
  local code
  code=$(curl -s -o /dev/null -w '%{http_code}' --max-time 10 "$2" || echo 000)
  if [[ "$code" == "$3" ]]; then echo "PASS  $1 ($2) -> $code"; else echo "FAIL  $1 ($2) -> $code, expected $3"; fail=1; fi
}

check_status "API liveness"  "$API_URL/api/v1/health"       200
check_status "API readiness" "$API_URL/api/v1/health/ready" 200
check_status "Web index"     "$WEB_URL/"                    200

if curl -s --max-time 10 "$WEB_URL/" | grep -q "bb-root"; then echo "PASS  Web serves the Angular shell"; else echo "FAIL  Web response does not contain bb-root"; fail=1; fi
if curl -s --max-time 10 "$WEB_URL/api/v1/health" | grep -q '"status":"ok"'; then echo "PASS  Web can reach the API"; else echo "FAIL  Web cannot reach the API through its proxy"; fail=1; fi

exit $fail
