-- Même logique que le bloc PARRAINAGE dans init.sql (exécuté par parrainage-db-bootstrap si la base n'existait pas).
CREATE DATABASE parrainage_db;
CREATE USER parrainage_user WITH PASSWORD 'Parrainage@2026';
GRANT ALL PRIVILEGES ON DATABASE parrainage_db TO parrainage_user;
\c parrainage_db
GRANT ALL ON SCHEMA public TO parrainage_user;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO parrainage_user;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON SEQUENCES TO parrainage_user;
