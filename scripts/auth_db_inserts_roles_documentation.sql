-- =============================================================================
-- auth_db — Rôles « documentation custom » (ROLE_*)
-- Le Planning du binôme n’utilise PAS ces noms dans app.routes.ts.
-- Pour la cohérence accès Planning, utilisez plutôt :
--   scripts/auth_db_roles_planning_binome.sql
-- =============================================================================

INSERT INTO public."Roles" ("Name", "Description", "CreatedAt")
SELECT 'ROLE_RH_ADMIN', 'Documentation — interface RH (hors contrat Planning)', now()
WHERE NOT EXISTS (SELECT 1 FROM public."Roles" r WHERE r."Name" = 'ROLE_RH_ADMIN');

INSERT INTO public."Roles" ("Name", "Description", "CreatedAt")
SELECT 'ROLE_PILOT', 'Documentation — pilote (hors contrat Planning)', now()
WHERE NOT EXISTS (SELECT 1 FROM public."Roles" r WHERE r."Name" = 'ROLE_PILOT');
