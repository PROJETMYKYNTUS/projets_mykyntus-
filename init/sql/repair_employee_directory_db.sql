-- Réparation idempotente si le volume Postgres a été créé avant l'ajout de employee_directory_db dans init.sql.
-- Usage (depuis la racine du dépôt) :
--   docker exec -i kyntus_db psql -U postgres -v ON_ERROR_STOP=1 -f - < init/sql/repair_employee_directory_db.sql

DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'directory_user') THEN
    CREATE USER directory_user WITH PASSWORD 'Directory@2026';
  ELSE
    ALTER USER directory_user WITH PASSWORD 'Directory@2026';
  END IF;
END $$;

SELECT 'CREATE DATABASE employee_directory_db OWNER directory_user'
WHERE NOT EXISTS (SELECT 1 FROM pg_database WHERE datname = 'employee_directory_db')\gexec

GRANT ALL PRIVILEGES ON DATABASE employee_directory_db TO directory_user;

\c employee_directory_db

GRANT ALL ON SCHEMA public TO directory_user;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO directory_user;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON SEQUENCES TO directory_user;
