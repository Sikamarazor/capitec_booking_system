# Continuous integration

Workflow: `.github/workflows/ci.yml`. It runs on every pull request, on pushes to `main`, and when the release workflow calls it.

| Job | What it checks | Run it yourself |
|---|---|---|
| Backend | restore, build with warnings as errors, NuGet audit, unit tests, integration tests against PostgreSQL, no skipped tests, no unmigrated model changes | see below |
| Frontend | `npm ci`, lint, unit tests, production build, `npm audit` for production dependencies | `cd frontend/branchbooking-web && npm ci && npm run lint && npm run test:ci && npm run build` |
| Scripts and workflows | ShellCheck, the tests for the CI scripts, `actionlint` | `scripts/ci/tests/assert-tests.test.sh` |

## Running the backend job locally

```bash
dotnet restore BranchBooking.slnx && dotnet build BranchBooking.slnx -c Release --no-restore
scripts/ci/audit-dotnet.sh

# a disposable database whose name ends in _test (choose your own password; avoid ; = and spaces)
docker run -d --name bb-ci-db -e POSTGRES_USER=branchbooking -e POSTGRES_PASSWORD='<YOUR-PASSWORD>' \
  -e POSTGRES_DB=branchbooking_test -p 127.0.0.1:5432:5432 postgres:16-alpine
export BRANCHBOOKING_TEST_CONNECTION="Host=127.0.0.1;Port=5432;Database=branchbooking_test;Username=branchbooking;Password=<YOUR-PASSWORD>"

dotnet test tests/BranchBooking.UnitTests -c Release --no-build --logger "trx;LogFileName=unit.trx" --results-directory TestResults
dotnet test tests/BranchBooking.IntegrationTests -c Release --no-build --logger "trx;LogFileName=integration.trx" --results-directory TestResults
scripts/ci/assert-tests.sh TestResults/unit.trx TestResults/integration.trx
docker rm -f bb-ci-db
```

## When a check fails

| Symptom | Cause and fix |
|---|---|
| `FAIL ... skipped (is BRANCHBOOKING_TEST_CONNECTION set?)` | The database step failed or was skipped. Look at the "Start PostgreSQL" step. |
| `has-pending-model-changes` fails | An entity or configuration changed without a migration. Add one (`docs/engineering/database-migrations.md`). |
| NuGet audit fails | A package has a known vulnerability. Update it; if no fix exists, record why in the pull request and pin the decision in `docs/engineering/ci.md` before overriding. |
| `npm audit` fails | Same. `npm audit fix` for direct dependencies; otherwise wait for the upstream fix or replace the package. Never add `--force`. |
| Frontend tests fail only in CI | Run `npm ci` (not `npm install`) and the same Node version as `.nvmrc`. |
