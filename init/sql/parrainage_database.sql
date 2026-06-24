-- Bootstrap PARRAINAGE si la base n'existait pas — mot de passe via variable psql db_password.
SELECT format('CREATE USER parrainage_user WITH PASSWORD %L', :'db_password') WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'parrainage_user')\gexec
CREATE DATABASE parrainage_db;
GRANT ALL PRIVILEGES ON DATABASE parrainage_db TO parrainage_user;
\c parrainage_db
GRANT ALL ON SCHEMA public TO parrainage_user;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO parrainage_user;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON SEQUENCES TO parrainage_user;
