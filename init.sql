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

-- ─── BASE DOCUMENTATION (microservice Documentation) ──
CREATE USER documentation_user WITH PASSWORD 'Htelgroupe!2025';
CREATE DATABASE mykyntus_documentation OWNER documentation_user;
\c mykyntus_documentation
GRANT ALL ON SCHEMA public TO documentation_user;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO documentation_user;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON SEQUENCES TO documentation_user;