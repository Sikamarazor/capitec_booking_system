#!/usr/bin/env bash
# Checks what is inside a built image. Usage: scripts/ci/check-image.sh api|worker|web IMAGE
# Set EXPECT_LABELS=1 to also require the OCI source and version labels (release builds).
set -uo pipefail

if [[ $# -ne 2 ]]; then
  echo "usage: check-image.sh api|worker|web IMAGE" >&2
  exit 2
fi

component="$1"
image="$2"
fail=0

pass() { echo "PASS  $*"; }
bad() { echo "FAIL  $*"; fail=1; }

# Runs a shell snippet inside the image without starting the application.
in_image() { docker run --rm --entrypoint sh "$image" -c "$1" >/dev/null 2>&1; }

user="$(docker image inspect --format '{{.Config.User}}' "$image")"
case "$user" in
  "" | 0 | root | 0:* | root:*) bad "the image runs as root (User='$user')" ;;
  *) pass "runs as a non-root user ($user)" ;;
esac

if in_image 'test ! -e /app/.env && test ! -e /.env'; then
  pass "no .env file inside the image"
else
  bad "a .env file is inside the image"
fi

case "$component" in
  api | worker)
    dll="BranchBooking.$([[ "$component" == api ]] && echo Api || echo Worker).dll"
    if in_image "test -f /app/$dll"; then pass "/app/$dll is present"; else bad "/app/$dll is missing"; fi
    if in_image 'test -f /usr/share/zoneinfo/Africa/Johannesburg'; then
      pass "time zone data (Africa/Johannesburg) is present"
    else
      bad "time zone data is missing: branch times cannot be computed (Epic 04 open item 4)"
    fi
    ;;
  web)
    if in_image 'grep -q bb-root /usr/share/nginx/html/index.html'; then
      pass "the Angular shell is in place"
    else
      bad "index.html is missing or has no <bb-root>"
    fi
    if in_image 'grep -q "proxy_pass http://api:8080" /etc/nginx/conf.d/default.conf'; then
      pass "nginx proxies /api/ to the api service"
    else
      bad "nginx proxy configuration is missing"
    fi
    if in_image 'grep -q Content-Security-Policy /etc/nginx/snippets/security-headers.conf'; then
      pass "Content-Security-Policy header is configured"
    else
      bad "Content-Security-Policy is missing from the security headers"
    fi
    ;;
  *)
    echo "unknown component: $component" >&2
    exit 2
    ;;
esac

if [[ "${EXPECT_LABELS:-0}" == "1" ]]; then
  for label in org.opencontainers.image.source org.opencontainers.image.version; do
    value="$(docker image inspect --format "{{ index .Config.Labels \"$label\" }}" "$image")"
    if [[ -n "$value" && "$value" != "<no value>" ]]; then pass "label $label = $value"; else bad "label $label is missing"; fi
  done
fi

size="$(docker image inspect --format '{{.Size}}' "$image")"
echo "INFO  image size: $((size / 1024 / 1024)) MiB"

exit "$fail"
