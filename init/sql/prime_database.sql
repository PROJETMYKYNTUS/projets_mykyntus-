-- Même logique que le bloc PRIME dans init.sql (exécuté par prime-db-bootstrap si la base n’existait pas).
CREATE DATABASE prime_db;
CREATE USER prime_user WITH PASSWORD 'Prime@2026';
GRANT ALL PRIVILEGES ON DATABASE prime_db TO prime_user;
\c prime_db
GRANT ALL ON SCHEMA public TO prime_user;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO prime_user;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON SEQUENCES TO prime_user;
