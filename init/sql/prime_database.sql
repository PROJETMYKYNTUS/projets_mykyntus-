-- Bootstrap PRIME si la base n'existait pas — mot de passe via variable psql db_password.
-- Voir init/docker-entrypoint-initdb.d/01-init-databases.sh pour l'initialisation standard.
SELECT format('CREATE USER prime_user WITH PASSWORD %L', :'db_password') WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'prime_user')\gexec
CREATE DATABASE prime_db;
GRANT ALL PRIVILEGES ON DATABASE prime_db TO prime_user;
\c prime_db
GRANT ALL ON SCHEMA public TO prime_user;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO prime_user;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON SEQUENCES TO prime_user;
