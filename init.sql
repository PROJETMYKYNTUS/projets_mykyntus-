-- ─── BASE KYNTUS ───────────────────────────────────
CREATE DATABASE kyntus_db;
CREATE USER kyntus_user WITH PASSWORD 'Kyntus@2026';
GRANT ALL PRIVILEGES ON DATABASE kyntus_db TO kyntus_user;
\c kyntus_db
GRANT ALL ON SCHEMA public TO kyntus_user;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO kyntus_user;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON SEQUENCES TO kyntus_user;

-- ─── BASE AUTH ─────────────────────────────────────
CREATE DATABASE auth_db;
CREATE USER auth_user WITH PASSWORD 'Auth@2026';
GRANT ALL PRIVILEGES ON DATABASE auth_db TO auth_user;
\c auth_db
GRANT ALL ON SCHEMA public TO auth_user;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO auth_user;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON SEQUENCES TO auth_user;

-- ─── BASE FORMATION ────────────────────────────────
CREATE DATABASE formation_db;
CREATE USER formation_user WITH PASSWORD 'Formation@2026';
GRANT ALL PRIVILEGES ON DATABASE formation_db TO formation_user;
\c formation_db
GRANT ALL ON SCHEMA public TO formation_user;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO formation_user;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON SEQUENCES TO formation_user;

-- ─── BASE DOCUMENTATION ────────────────────────────
CREATE DATABASE documentation_db;
CREATE USER documentation_user WITH PASSWORD 'Documentation@2026';
GRANT ALL PRIVILEGES ON DATABASE documentation_db TO documentation_user;
\c documentation_db
GRANT ALL ON SCHEMA public TO documentation_user;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO documentation_user;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON SEQUENCES TO documentation_user;

-- Schéma métier EF (DocumentationDbContext : HasDefaultSchema("documentation")).
-- Sans cela : 42501 permission denied for schema documentation pour documentation_user.
CREATE SCHEMA IF NOT EXISTS documentation AUTHORIZATION documentation_user;
GRANT USAGE, CREATE ON SCHEMA documentation TO documentation_user;
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA documentation TO documentation_user;
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA documentation TO documentation_user;
ALTER DEFAULT PRIVILEGES IN SCHEMA documentation GRANT ALL ON TABLES TO documentation_user;
ALTER DEFAULT PRIVILEGES IN SCHEMA documentation GRANT ALL ON SEQUENCES TO documentation_user;
ALTER DEFAULT PRIVILEGES FOR ROLE documentation_user IN SCHEMA documentation GRANT ALL ON TABLES TO documentation_user;
ALTER DEFAULT PRIVILEGES FOR ROLE documentation_user IN SCHEMA documentation GRANT ALL ON SEQUENCES TO documentation_user;
ALTER DEFAULT PRIVILEGES FOR ROLE postgres IN SCHEMA documentation GRANT ALL ON TABLES TO documentation_user;
ALTER DEFAULT PRIVILEGES FOR ROLE postgres IN SCHEMA documentation GRANT ALL ON SEQUENCES TO documentation_user;