# End-to-end tests

Playwright tests in `e2e/` drive the real stack in a browser. Workflow: `.github/workflows/e2e.yml`.

## Run them (Docker path, one command)

```bash
scripts/e2e.sh                       # everything, desktop and mobile viewports
scripts/e2e.sh --project=desktop     # one viewport
scripts/e2e.sh -g "double-clicking"  # one test by name
```

It starts the stack under the compose project `branchbooking-e2e` on ports 18080, 15080 and 15432 (override with `E2E_WEB_PORT`,
`E2E_API_PORT`, `E2E_POSTGRES_PORT`), runs the tests, saves compose logs to `e2e/compose.log` on failure, and removes the stack and its
database volume. Your normal stack is not touched. If `.env` exists its `POSTGRES_PASSWORD` is used; otherwise a temporary one is made.

## Run them against a stack you started yourself (manual path)

```bash
# terminal 1: API with the limits raised
RateLimiting__WritePermitsPerMinute=1000 RateLimiting__LookupPermitsPerMinute=1000 \
  dotnet run --project src/BranchBooking.Api --launch-profile http
# terminal 2: Worker that polls quickly, with reminders off
Notifications__PollSeconds=2 Notifications__Reminders__Enabled=false dotnet run --project src/BranchBooking.Worker
# terminal 3: the web app
cd frontend/branchbooking-web && npm ci && npm start
# terminal 4
cd e2e && npm ci && npx playwright install chromium
E2E_BASE_URL=http://localhost:4200 npx playwright test
```

## Reading a failure

`npx playwright show-report` opens the HTML report; each failed test has a trace (timeline, DOM, network) you can step through.
In CI download the `e2e-evidence` artifact.

| Symptom | Likely cause |
|---|---|
| "No open appointment time was found" | the seed data has no opening hours in the booking window, or the API is not seeded |
| The confirmation message never shows "Demo message" | the Worker is not running, or `Notifications__PollSeconds` is large |
| Accessibility test fails on `color-contrast` | the provisional colour tokens (WI-103) do not meet WCAG AA; fix the tokens, not the test |
| `rate_limited` in a trace | the API was started without the raised limits |
| A selector times out | a page's wording changed; the tests use visible text, labels and `data-day` / `data-slot` |
