-- =============================================================================
-- auth_db — Alignement démo avec le Planning (binôme) :
--   - Interface RH / Documentation RH : rôle JWT = `RH` (pas ROLE_RH_ADMIN).
--   - Documentation pilote : rôle `ROLE_PILOT` (ou utiliser `Pilote` côté routes Angular si besoin).
--
-- Prérequis : auth_db_roles_planning_binome.sql ; pour ROLE_PILOT :
--   scripts/auth_db_inserts_roles_documentation.sql
-- Idempotent : ré-exécuter reproduit les mêmes RoleId pour ces emails.
-- =============================================================================

-- rh.admin : rôle Planning `RH` (claim JWT = RH — même nom que le binôme).
UPDATE public."Users" u
SET "RoleId" = r."Id", "UpdatedAt" = now()
FROM public."Roles" r
WHERE r."Name" = 'RH'
  AND lower(u."Email") = lower('rh.admin@atlas-tech-demo.dev');

-- yasmine : JWT `ROLE_PILOT` pour la route /documentation/pilot (si vous gardez ce nom côté front).
UPDATE public."Users" u
SET "RoleId" = r."Id", "UpdatedAt" = now()
FROM public."Roles" r
WHERE r."Name" = 'ROLE_PILOT'
  AND lower(u."Email") = lower('yasmine.elamrani@atlas-tech-demo.dev');

-- Vérification :
-- SELECT u."Username", u."Email", ro."Name" AS role_name
-- FROM public."Users" u JOIN public."Roles" ro ON ro."Id" = u."RoleId"
-- WHERE lower(u."Email") IN (
--   lower('rh.admin@atlas-tech-demo.dev'),
--   lower('yasmine.elamrani@atlas-tech-demo.dev')
-- );
