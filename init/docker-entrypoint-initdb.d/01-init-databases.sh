#!/bin/bash
# Initialise les bases PostgreSQL Kyntus avec des mots de passe fournis via l'environnement.
# Monté dans /docker-entrypoint-initdb.d/ par docker-compose (voir docker-compose.yml).
set -euo pipefail

require_var() {
  local name="$1"
  if [ -z "${!name:-}" ]; then
    echo "Variable d'environnement requise manquante: $name" >&2
    exit 1
  fi
}

require_var KYNTUS_DB_PASSWORD_KYNTUS
require_var KYNTUS_DB_PASSWORD_AUTH
require_var KYNTUS_DB_PASSWORD_FORMATION
require_var KYNTUS_DB_PASSWORD_DOCUMENTATION
require_var KYNTUS_DB_PASSWORD_CONGE
require_var KYNTUS_DB_PASSWORD_PRIME
require_var KYNTUS_DB_PASSWORD_PARRAINAGE
require_var KYNTUS_DB_PASSWORD_DIRECTORY

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" <<-EOSQL
-- ─── BASE KYNTUS (planning) ───────────────────────────────────
CREATE DATABASE kyntus_db;
CREATE USER kyntus_user WITH PASSWORD '${KYNTUS_DB_PASSWORD_KYNTUS}';
GRANT ALL PRIVILEGES ON DATABASE kyntus_db TO kyntus_user;
\c kyntus_db
GRANT ALL ON SCHEMA public TO kyntus_user;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO kyntus_user;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON SEQUENCES TO kyntus_user;

-- ─── BASE AUTH ─────────────────────────────────────
\c postgres
CREATE DATABASE auth_db;
CREATE USER auth_user WITH PASSWORD '${KYNTUS_DB_PASSWORD_AUTH}';
GRANT ALL PRIVILEGES ON DATABASE auth_db TO auth_user;
\c auth_db
GRANT ALL ON SCHEMA public TO auth_user;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO auth_user;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON SEQUENCES TO auth_user;

-- ─── BASE FORMATION ────────────────────────────────
\c postgres
CREATE DATABASE formation_db;
CREATE USER formation_user WITH PASSWORD '${KYNTUS_DB_PASSWORD_FORMATION}';
GRANT ALL PRIVILEGES ON DATABASE formation_db TO formation_user;
\c formation_db
GRANT ALL ON SCHEMA public TO formation_user;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO formation_user;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON SEQUENCES TO formation_user;

-- ─── BASE DOCUMENTATION ────────────────────────────
\c postgres
CREATE DATABASE documentation_db;
CREATE USER documentation_user WITH PASSWORD '${KYNTUS_DB_PASSWORD_DOCUMENTATION}';
GRANT ALL PRIVILEGES ON DATABASE documentation_db TO documentation_user;
\c documentation_db
GRANT ALL ON SCHEMA public TO documentation_user;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO documentation_user;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON SEQUENCES TO documentation_user;

\c postgres
CREATE DATABASE conge_db;
CREATE USER conge_user WITH PASSWORD '${KYNTUS_DB_PASSWORD_CONGE}';
GRANT ALL PRIVILEGES ON DATABASE conge_db TO conge_user;
\c conge_db
GRANT ALL ON SCHEMA public TO conge_user;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO conge_user;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON SEQUENCES TO conge_user;

\c postgres
CREATE DATABASE prime_db;
CREATE USER prime_user WITH PASSWORD '${KYNTUS_DB_PASSWORD_PRIME}';
GRANT ALL PRIVILEGES ON DATABASE prime_db TO prime_user;
\c prime_db
GRANT ALL ON SCHEMA public TO prime_user;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO prime_user;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON SEQUENCES TO prime_user;

-- ─── BASE PARRAINAGE ───────────────────────────────
\c postgres
CREATE DATABASE parrainage_db;
CREATE USER parrainage_user WITH PASSWORD '${KYNTUS_DB_PASSWORD_PARRAINAGE}';
GRANT ALL PRIVILEGES ON DATABASE parrainage_db TO parrainage_user;
\c parrainage_db
GRANT ALL ON SCHEMA public TO parrainage_user;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO parrainage_user;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON SEQUENCES TO parrainage_user;

-- ─── BASE EMPLOYEE DIRECTORY ─────────────────────────
\c postgres
CREATE DATABASE employee_directory_db;
CREATE USER directory_user WITH PASSWORD '${KYNTUS_DB_PASSWORD_DIRECTORY}';
GRANT ALL PRIVILEGES ON DATABASE employee_directory_db TO directory_user;
\c employee_directory_db
GRANT ALL ON SCHEMA public TO directory_user;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO directory_user;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON SEQUENCES TO directory_user;

\c prime_db
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
EOSQL
