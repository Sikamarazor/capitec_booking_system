#!/usr/bin/env bash
# Fails when a TRX result file shows a failed test, no tests, or a skipped test.
# Usage: scripts/ci/assert-tests.sh [--allow-skipped] FILE.trx [FILE.trx ...]
# Skipped tests usually mean BRANCHBOOKING_TEST_CONNECTION was not set (database tests skip without it).
set -euo pipefail

allow_skipped=0
if [[ "${1:-}" == "--allow-skipped" ]]; then
  allow_skipped=1
  shift
fi

if [[ $# -eq 0 ]]; then
  echo "usage: assert-tests.sh [--allow-skipped] FILE.trx [FILE.trx ...]" >&2
  exit 2
fi

# counter FILE NAME: the value of one attribute of the <Counters> element, or nothing.
counter() {
  { grep -o '<Counters[^>]*>' "$1" || true; } | head -n 1 | { grep -o " $2=\"[0-9]*\"" || true; } | head -n 1 | { grep -o '[0-9]*' || true; }
}

fail=0
rows=""

for file in "$@"; do
  if [[ ! -f "$file" ]]; then
    echo "FAIL  $file does not exist"
    fail=1
    continue
  fi

  total="$(counter "$file" total)"
  if [[ -z "$total" ]]; then
    echo "FAIL  $file has no result counters (is it a TRX file?)"
    fail=1
    continue
  fi

  passed="$(counter "$file" passed)"; passed="${passed:-0}"
  failed="$(counter "$file" failed)"; failed="${failed:-0}"
  errors="$(counter "$file" error)"; errors="${errors:-0}"
  skipped="$(counter "$file" notExecuted)"; skipped="${skipped:-0}"

  status="PASS"
  reason=""
  if (( total == 0 )); then status="FAIL"; reason="no tests ran"; fi
  if (( failed + errors > 0 )); then status="FAIL"; reason="$failed failed, $errors errors"; fi
  if (( skipped > 0 && allow_skipped == 0 )); then
    status="FAIL"
    reason="$skipped skipped (is BRANCHBOOKING_TEST_CONNECTION set?)"
  fi

  printf '%s  %s: total %s, passed %s, failed %s, skipped %s %s\n' "$status" "$file" "$total" "$passed" "$failed" "$skipped" "${reason:+($reason)}"
  rows+="| $(basename "$file") | $total | $passed | $failed | $skipped | $status |"$'\n'
  [[ "$status" == "PASS" ]] || fail=1
done

if [[ -n "${GITHUB_STEP_SUMMARY:-}" ]]; then
  {
    echo "### Test results"
    echo
    echo "| File | Total | Passed | Failed | Skipped | Result |"
    echo "|---|---|---|---|---|---|"
    printf '%s' "$rows"
  } >> "$GITHUB_STEP_SUMMARY"
fi

exit "$fail"
