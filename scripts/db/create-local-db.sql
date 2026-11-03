-- Usage:
--   psql -h localhost -U postgres -v app_password='<YOUR-PASSWORD>' -f scripts/db/create-local-db.sql
-- Re-running is safe: existing objects are left alone (an existing role keeps its current password).

SELECT format('CREATE ROLE branchbooking LOGIN PASSWORD %L', :'app_password')
WHERE NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'branchbooking')
\gexec

SELECT 'CREATE DATABASE branchbooking OWNER branchbooking'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'branchbooking')
\gexec
