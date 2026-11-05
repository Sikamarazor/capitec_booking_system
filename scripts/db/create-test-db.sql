-- Usage (manual path):  psql -h localhost -U postgres -f scripts/db/create-test-db.sql
-- Usage (Docker path):  docker compose exec -T db psql -U branchbooking -d branchbooking < scripts/db/create-test-db.sql
-- Re-running is safe.

SELECT 'CREATE DATABASE branchbooking_test OWNER branchbooking'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'branchbooking_test')
\gexec
