# Releasing and deploying

## Cut a release

1. `main` is green (all CI jobs) and the changelog (`CHANGELOG.md`) lists what changed.
2. Tag the commit and push the tag. Use a pre-release first.
   ```bash
   git switch main && git pull
   git tag -a v0.1.0-rc.1 -m "Release candidate 1"
   git push origin v0.1.0-rc.1
   ```
3. The **Release** workflow runs CI and the end-to-end test on that commit, builds and scans three images, pushes them to
   `ghcr.io/<owner>/branchbooking-api|worker|web` (tags `0.1.0-rc.1` and `sha-<commit>`; final releases also get `0.1`), builds
   the migration bundle and creates a GitHub release (marked pre-release when the tag has a `-`).
4. If any step fails nothing is published; fix the cause and tag again with a new tag (never move a tag).
5. First push of an image: its package in GHCR is private until you change its visibility (repository owner, Packages).

## Deploy (one host with Docker)

```bash
mkdir branchbooking && cd branchbooking
# download docker-compose.release.yml and .env.release.example from the GitHub release
mv .env.release.example .env      # then fill every value
docker login ghcr.io              # only if the packages are private; use a token with read:packages
docker compose -f docker-compose.release.yml pull
```

Create the schema, either way:

- **Single instance, simplest:** set `MIGRATE_ON_STARTUP=true` in `.env`, then start (below).
- **Bundle:** start only the database, run the bundle, then start the rest.
  ```bash
  docker compose -f docker-compose.release.yml up -d db
  # download efbundle and efbundle.sha256 from the release
  sha256sum -c efbundle.sha256 && chmod +x efbundle
  source .env
  docker run --rm --network branchbooking_default -v "$PWD/efbundle:/efbundle:ro" \
    mcr.microsoft.com/dotnet/runtime-deps:10.0 /efbundle \
    --connection "Host=db;Port=5432;Database=$POSTGRES_DB;Username=$POSTGRES_USER;Password=$POSTGRES_PASSWORD"
  ```

Start and check:

```bash
docker compose -f docker-compose.release.yml up -d
docker compose -f docker-compose.release.yml ps          # db, api, web healthy; worker running
curl -s http://localhost:8080/api/v1/health                # shows the version you deployed
```

Put HTTPS in front of port 8080 (a reverse proxy or load balancer). The stack does not terminate TLS.

## Upgrade

```bash
# 1. back up first
docker compose -f docker-compose.release.yml exec -T db sh -c 'pg_dump -U "$POSTGRES_USER" "$POSTGRES_DB"' > backup-$(date +%F).sql
# 2. change BRANCHBOOKING_VERSION in .env, then (run the new efbundle if you use the bundle)
docker compose -f docker-compose.release.yml pull
docker compose -f docker-compose.release.yml up -d
```

## Roll back

Images roll back by setting the previous `BRANCHBOOKING_VERSION` and running `up -d`. Database changes do not roll back by themselves:
if the new release added a migration, restore the backup taken before the upgrade
(`docker compose ... exec -T db sh -c 'psql -U "$POSTGRES_USER" "$POSTGRES_DB"' < backup-YYYY-MM-DD.sql` into an empty database)
or, if the migration is known to be reversible, run the bundle with `--migration <previous migration name>`.
Rehearse this on a copy before relying on it.
