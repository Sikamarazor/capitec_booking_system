#!/usr/bin/env bash
# Tests for assert-tests.sh. Exits non-zero when any case fails.
set -uo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
script="$here/../assert-tests.sh"
fixtures="$here/fixtures"
failures=0

expect_exit() { # name expected command...
  local name="$1" expected="$2"
  shift 2
  "$@" >/dev/null 2>&1
  local actual=$?
  if [[ "$actual" -eq "$expected" ]]; then
    echo "PASS  $name"
  else
    echo "FAIL  $name (exit $actual, expected $expected)"
    failures=$((failures + 1))
  fi
}

expect_exit "all tests passed"                       0 "$script" "$fixtures/passed.trx"
expect_exit "a skipped test fails"                   1 "$script" "$fixtures/skipped.trx"
expect_exit "a skipped test passes when allowed"     0 "$script" --allow-skipped "$fixtures/skipped.trx"
expect_exit "a failed test fails"                    1 "$script" "$fixtures/failed.trx"
expect_exit "no tests at all fails"                  1 "$script" "$fixtures/empty.trx"
expect_exit "a missing file fails"                   1 "$script" "$fixtures/does-not-exist.trx"
expect_exit "a file that is not a TRX fails"         1 "$script" "$fixtures/not-a-trx.txt"
expect_exit "two good files pass"                    0 "$script" "$fixtures/passed.trx" "$fixtures/passed.trx"
expect_exit "one bad file among good ones fails"     1 "$script" "$fixtures/passed.trx" "$fixtures/failed.trx"
expect_exit "no arguments is a usage error"          2 "$script"

if [[ "$failures" -gt 0 ]]; then
  echo "$failures case(s) failed"
  exit 1
fi
echo "All cases passed"
