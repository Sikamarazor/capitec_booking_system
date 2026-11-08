#!/usr/bin/env bash
# Fails when the README and the project drift apart.
# Usage: scripts/ci/check-docs.sh [ROOT]     (ROOT defaults to the repository root)
set -uo pipefail

root="${1:-$(cd "$(dirname "$0")/../.." && pwd)}"
cd "$root" || exit 2
fail=0
bad() { echo "FAIL  $*"; fail=1; }

if [[ ! -f README.md ]]; then
  echo "FAIL  README.md is missing"
  exit 1
fi

# 1. The sections a newcomer needs.
for heading in "## Quick start" "## Configuration reference" "## Testing" "## Troubleshooting" \
  "## Releases and deployment" "## Security and privacy"; do
  grep -qF "$heading" README.md || bad "README.md has no \"$heading\" section"
done

# 2. Every environment variable the compose files and env examples use is documented in the README.
collect_variables() {
  local file
  for file in docker-compose.yml docker-compose.e2e.yml deploy/docker-compose.release.yml; do
    if [[ -f "$file" ]]; then { grep -oE '\$\{[A-Z_][A-Z0-9_]*' "$file" || true; } | sed 's/^\${//'; fi
  done
  for file in .env.example deploy/.env.release.example; do
    if [[ -f "$file" ]]; then { grep -oE '^[A-Z_][A-Z0-9_]*=' "$file" || true; } | tr -d '='; fi
  done
}
while IFS= read -r variable; do
  [[ -z "$variable" ]] && continue
  grep -qF "\`$variable\`" README.md || bad "variable $variable is used by a compose or env file but is not documented in README.md"
done < <(collect_variables | sort -u)

# 3. Relative links in the documentation resolve (the planning documents are excluded).
while IFS= read -r file; do
  dir="$(dirname "$file")"
  while IFS= read -r target; do
    case "$target" in
      "" | http* | mailto:* | \#*) continue ;;
    esac
    path="${target%%#*}"
    if [[ "$path" == /* ]]; then candidate="$root$path"; else candidate="$dir/$path"; fi
    [[ -e "$candidate" ]] || bad "$file links to $target, which does not exist"
  done < <({ grep -oE '\]\([^)]+\)' "$file" || true; } | sed -E 's/^\]\(//; s/\)$//; s/ .*$//')
done < <(find . -name '*.md' -not -path '*/node_modules/*' -not -path './.git/*' -not -path './docs/implementation/*' | sort)

# 4. No unfinished markers.
if grep -nE 'TODO|FIXME' README.md CHANGELOG.md docs/*.md docs/engineering/*.md docs/api/*.md 2>/dev/null; then
  bad "unfinished markers (TODO or FIXME) found in the documentation"
fi

# 5. Every document under docs/ (except the planning documents) is listed in docs/README.md.
if [[ -d docs ]]; then
  if [[ ! -f docs/README.md ]]; then
    bad "docs/README.md (the documentation index) is missing"
  else
    while IFS= read -r document; do
      relative="${document#./docs/}"
      grep -qF "$relative" docs/README.md || bad "$document is not listed in docs/README.md"
    done < <(find ./docs -name '*.md' -not -path './docs/implementation/*' -not -path './docs/README.md' | sort)
  fi
fi

if [[ "$fail" -eq 0 ]]; then echo "PASS  documentation is consistent"; fi
exit "$fail"
