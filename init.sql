-- ─── BASE KYNTUS ───────────────────────────────────
CREATE DATABASE kyntus_db;
CREATE USER kyntus_user WITH PASSWORD 'Kyntus@2026';
GRANT ALL PRIVILEGES ON DATABASE kyntus_db TO kyntus_user;

-- Permissions sur le schema public
\c kyntus_db
GRANT ALL ON SCHEMA public TO kyntus_user;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO kyntus_user;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON SEQUENCES TO kyntus_user;

-- ─── BASE AUTH ─────────────────────────────────────
CREATE DATABASE auth_db;
CREATE USER auth_user WITH PASSWORD 'Auth@2026';
GRANT ALL PRIVILEGES ON DATABASE auth_db TO auth_user;

-- Permissions sur le schema public
\c auth_db
GRANT ALL ON SCHEMA public TO auth_user;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO auth_user;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON SEQUENCES TO auth_user;