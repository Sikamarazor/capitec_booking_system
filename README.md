# BranchBooking

A Capitec-style branch appointment booking system. Customers book, view, reschedule and cancel branch appointments (open an account, card collection, apply for a loan, and more appointment types over time). Confirmations are simulated by default; Brevo email is an optional adapter.

**Stack:** ASP.NET Core Web API (.NET 10) with EF Core and PostgreSQL 16, Angular with Tailwind CSS v4.


## Quick start

Pick one path. Docker is optional.

| | Docker path | Manual path |
|---|---|---|
| Needs | Docker with Compose v2 | .NET SDK 10, Node 22.12+, PostgreSQL 16 |
| Start | `docker compose up --build` | `scripts/run-manual.sh` (or `.ps1`) |
| Open | http://localhost:8080 | http://localhost:4200 |

## Running with Docker (optional)

```bash
cp .env.example .env            # then set your own POSTGRES_PASSWORD (avoid ; = and spaces)
docker compose up --build -d
docker compose ps               # db, api and web should be healthy
scripts/verify-foundation.sh    # PowerShell: scripts/verify-foundation.ps1
```

Open http://localhost:8080. Stop with `docker compose down`; add `-v` to also delete the database volume.

| Service | Port on your machine | Notes |
|---|---|---|
| web (nginx + Angular) | 8080 (`WEB_PORT`) | proxies `/api/` to the API |
| api | 127.0.0.1:5080 (`API_PORT`) | `/api/v1/health`, `/api/v1/health/ready` |
| db (PostgreSQL 16) | 127.0.0.1:5432 (`POSTGRES_PORT`) | data in the `db-data` volume |
| worker | none | background jobs (reminders arrive in WI-403) |

## Running without Docker

1. Install .NET SDK 10, Node 22.12 or newer, and PostgreSQL 16.
2. Create the database and set the connection string: `docs/engineering/local-postgres.md`.
3. Start everything: `scripts/run-manual.sh` (Linux, macOS) or `scripts/run-manual.ps1` (Windows PowerShell).
   Or by hand, in two terminals:
   - `dotnet run --project src/BranchBooking.Api --launch-profile http`
   - `cd frontend/branchbooking-web && npm ci && npm start`
4. Open http://localhost:4200. Check it with `scripts/verify-foundation.sh http://localhost:5080 http://localhost:4200`.

## Testing

Backend (xUnit):

Database-backed tests need a disposable database. Create it once, then point the tests at it
(the name must end in `_test`: the tests delete all rows in it, and refuse to run otherwise):

```bash
# Manual path (local PostgreSQL):  psql -h localhost -U postgres -f scripts/db/create-test-db.sql
# Docker path:                     docker compose exec -T db psql -U branchbooking -d branchbooking < scripts/db/create-test-db.sql
export BRANCHBOOKING_TEST_CONNECTION="Host=localhost;Port=5432;Database=branchbooking_test;Username=branchbooking;Password=<YOUR-PASSWORD>"
```

Without the variable, database tests are skipped, not failed.

Frontend:

```bash
cd frontend/branchbooking-web && npm ci && npx ng test --no-watch && npm run lint
```

No local tools? Run the tests inside containers:

```bash
docker run --rm -v "$PWD":/src -w /src mcr.microsoft.com/dotnet/sdk:10.0 dotnet test BranchBooking.slnx -c Release
docker run --rm -v "$PWD/frontend/branchbooking-web":/app -w /app node:22-alpine sh -c "npm ci && npx ng test --no-watch && npm run lint"
```
