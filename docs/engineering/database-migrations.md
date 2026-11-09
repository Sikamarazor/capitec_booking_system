# Database migrations

The schema is managed with EF Core migrations in `src/BranchBooking.Infrastructure/Persistence/Migrations/`.
The Docker demo stack and the Development environment apply them automatically on API start
(`Database:MigrateOnStartup`). A real production deployment keeps that setting off and applies them as a release step.

## One-time setup

```bash
dotnet tool restore                 # installs the dotnet-ef version pinned in .config/dotnet-tools.json
```

Every `dotnet ef` command needs the connection string in the environment (this is the design-time factory's only input):

```bash
export ConnectionStrings__BranchBooking="Host=localhost;Port=5432;Database=branchbooking;Username=branchbooking;Password=<YOUR-PASSWORD>"      # bash
$env:ConnectionStrings__BranchBooking = "Host=localhost;Port=5432;Database=branchbooking;Username=branchbooking;Password=<YOUR-PASSWORD>"   # PowerShell
```

## Everyday commands (manual path, local SDK)

```bash
# Add a migration after changing entities or configurations
dotnet ef migrations add <Name> --project src/BranchBooking.Infrastructure --startup-project src/BranchBooking.Api --output-dir Persistence/Migrations

# Apply all pending migrations
dotnet ef database update --project src/BranchBooking.Infrastructure --startup-project src/BranchBooking.Api

# Go back to a named migration (or "0" for an empty database)
dotnet ef database update <MigrationName> --project src/BranchBooking.Infrastructure --startup-project src/BranchBooking.Api

# Remove the newest migration if it has not been applied anywhere
dotnet ef migrations remove --project src/BranchBooking.Infrastructure --startup-project src/BranchBooking.Api

# Fail if the model has changes with no migration (used in CI)
dotnet ef migrations has-pending-model-changes --project src/BranchBooking.Infrastructure --startup-project src/BranchBooking.Api

# SQL for a release: every migration, safe to run repeatedly
dotnet ef migrations script --idempotent --project src/BranchBooking.Infrastructure --startup-project src/BranchBooking.Api -o artifacts/migrate.sql
```

## Without a local SDK (Docker path)

```bash
docker run --rm --network host -v "$PWD":/src -w /src \
  -e ConnectionStrings__BranchBooking="Host=localhost;Port=5432;Database=branchbooking;Username=branchbooking;Password=<YOUR-PASSWORD>" \
  mcr.microsoft.com/dotnet/sdk:10.0 \
  sh -c "dotnet tool restore && dotnet ef database update --project src/BranchBooking.Infrastructure --startup-project src/BranchBooking.Api"
```

(`--network host` works on Linux; on Docker Desktop use `Host=host.docker.internal` and drop it.) Or simply run `docker compose up --build -d`, which applies migrations when the API starts.

## Rules

- Never edit a migration that has been applied outside your machine; add a new one.
- Always read the generated `Up` and `Down` before committing. Check that no data-losing operation (drop column, drop table) appears unintentionally.
- Keep the constraint names in `DatabaseConstraints.cs` in step with the configurations; application code recognises errors by those names.
- Two instances may start together: EF locks while migrating, so the second waits and then finds nothing pending.
