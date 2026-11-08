#!/usr/bin/env bash
# Tests for check-docs.sh. Exits non-zero when any case fails.
set -uo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
script="$here/../check-docs.sh"
failures=0

make_good_tree() {
  local d="$1"
  mkdir -p "$d/docs/engineering" "$d/deploy"
  cat > "$d/README.md" <<'EOF'
# Demo
## Quick start
## Configuration reference
`POSTGRES_PASSWORD` and `WEB_PORT`
## Testing
## Troubleshooting
## Releases and deployment
## Security and privacy
See [the guide](docs/engineering/guide.md).
EOF
  printf '# Guide\n' > "$d/docs/engineering/guide.md"
  printf '# Docs\n- [guide](engineering/guide.md)\n' > "$d/docs/README.md"
  printf 'services:\n  web:\n    ports:\n      - "${WEB_PORT:-8080}:8080"\n' > "$d/docker-compose.yml"
  printf 'POSTGRES_PASSWORD=<YOUR-PASSWORD>\n' > "$d/.env.example"
}

new_tree() {
  local d
  d="$(mktemp -d)"
  make_good_tree "$d"
  echo "$d"
}

expect() { # name expected dir
  "$script" "$3" >/dev/null 2>&1
  local actual=$?
  if [[ "$actual" -eq "$2" ]]; then
    echo "PASS  $1"
  else
    echo "FAIL  $1 (exit $actual, expected $2)"
    failures=$((failures + 1))
  fi
  rm -rf "$3"
}

d="$(new_tree)"
expect "a complete tree passes" 0 "$d"

d="$(new_tree)"
grep -v '## Troubleshooting' "$d/README.md" > "$d/README.tmp" && mv "$d/README.tmp" "$d/README.md"
expect "a missing section fails" 1 "$d"

d="$(new_tree)"
echo 'NEW_SETTING=1' >> "$d/.env.example"
expect "an undocumented variable fails" 1 "$d"

d="$(new_tree)"
echo 'See [nothing](docs/nope.md).' >> "$d/README.md"
expect "a broken link fails" 1 "$d"

d="$(new_tree)"
echo 'TODO write this part' >> "$d/README.md"
expect "an unfinished marker fails" 1 "$d"

d="$(new_tree)"
printf '# Extra\n' > "$d/docs/engineering/extra.md"
expect "a document missing from the index fails" 1 "$d"

d="$(new_tree)"
rm "$d/README.md"
expect "a missing README fails" 1 "$d"

if [[ "$failures" -gt 0 ]]; then
  echo "$failures case(s) failed"
  exit 1
fi
echo "All cases passed"
