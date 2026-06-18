-- Réparation droits schéma documentation (PostgreSQL 42501) sur un volume déjà créé.
-- Exécuter en superuser sur la base documentation_db, ex. :
--   Get-Content .\init\sql\repair_documentation_schema_permissions.sql -Raw | docker compose exec -T postgres psql -U postgres -d documentation_db
-- (Après un EnsureCreated, le schéma documentation existe en général déjà.)

CREATE SCHEMA IF NOT EXISTS documentation AUTHORIZATION documentation_user;

GRANT USAGE, CREATE ON SCHEMA documentation TO documentation_user;

-- Si le schéma / objets ont été créés par le superuser postgres (EnsureCreated hors bon utilisateur, import, etc.)
ALTER SCHEMA documentation OWNER TO documentation_user;

GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA documentation TO documentation_user;
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA documentation TO documentation_user;
GRANT ALL PRIVILEGES ON ALL FUNCTIONS IN SCHEMA documentation TO documentation_user;

ALTER DEFAULT PRIVILEGES FOR ROLE postgres IN SCHEMA documentation GRANT ALL ON TABLES TO documentation_user;
ALTER DEFAULT PRIVILEGES FOR ROLE postgres IN SCHEMA documentation GRANT ALL ON SEQUENCES TO documentation_user;
ALTER DEFAULT PRIVILEGES FOR ROLE documentation_user IN SCHEMA documentation GRANT ALL ON TABLES TO documentation_user;
ALTER DEFAULT PRIVILEGES FOR ROLE documentation_user IN SCHEMA documentation GRANT ALL ON SEQUENCES TO documentation_user;
