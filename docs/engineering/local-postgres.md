# Local PostgreSQL (without Docker)

BranchBooking needs PostgreSQL 16. Use this guide if you do not use Docker.

## 1. Install

| OS | Command or installer |
|---|---|
| Windows | `winget install PostgreSQL.PostgreSQL.16` or the EDB installer from postgresql.org. Keep the superuser password you set. |
| macOS | `brew install postgresql@16 && brew services start postgresql@16` |
| Ubuntu/Debian | `sudo apt install postgresql-16` (use the PostgreSQL apt repository if your release does not ship 16) |

Check: `psql --version` prints `psql (PostgreSQL) 16.x`, and the server listens on `localhost:5432`.

## 2. Create the role and database

```bash
psql -h localhost -U postgres -v app_password='<YOUR-PASSWORD>' -f scripts/db/create-local-db.sql
```

On Linux you may need `sudo -u postgres psql -v app_password='<YOUR-PASSWORD>' -f scripts/db/create-local-db.sql`.

## 3. Give the API and worker the connection string

Either use user-secrets (stored outside the repository, applies in the Development environment):

```bash
dotnet user-secrets set "ConnectionStrings:BranchBooking" "Host=localhost;Port=5432;Database=branchbooking;Username=branchbooking;Password=<YOUR-PASSWORD>" --project src/BranchBooking.Api
dotnet user-secrets set "ConnectionStrings:BranchBooking" "Host=localhost;Port=5432;Database=branchbooking;Username=branchbooking;Password=<YOUR-PASSWORD>" --project src/BranchBooking.Worker
```

Or an environment variable (works in every environment):

```bash
export ConnectionStrings__BranchBooking="Host=localhost;Port=5432;Database=branchbooking;Username=branchbooking;Password=<YOUR-PASSWORD>"      # bash
$env:ConnectionStrings__BranchBooking = "Host=localhost;Port=5432;Database=branchbooking;Username=branchbooking;Password=<YOUR-PASSWORD>"   # PowerShell
```

## Troubleshooting

| Symptom | Likely cause and fix |
|---|---|
| `password authentication failed for user "branchbooking"` | Password in the connection string differs from the role's. Reset: `ALTER ROLE branchbooking PASSWORD '...';` |
| `Connection refused` on port 5432 | Server not running, or running on another port. Check the service and `postgresql.conf` `port`. |
| `database "branchbooking" does not exist` | Step 2 was not run, or ran against another server. |
| Port 5432 already in use when starting Docker | A local server is running. Stop it, or set `POSTGRES_PORT=5433` in `.env`. |
| API exits with `ConnectionStrings:BranchBooking must be set` | Step 3 not done for this project or environment. |
