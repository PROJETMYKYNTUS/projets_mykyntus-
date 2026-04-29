-- =============================================================================
-- auth_db — Utilisateurs démo alignés sur :
--   - annuaire Documentation (email + DocumentationDirectoryUserId)
--   - noms de rôles du Planning (binôme) : voir auth_db_roles_planning_binome.sql
--
-- Exécuter sur auth_db après auth_db_roles_planning_binome.sql
-- Mot de passe : DocAlign!2026 (hash ASP.NET Identity PasswordHasher, cf. AuthService)
-- Idempotent sur l’email.
--
-- Connexion login échoue (« Email ou mot de passe incorrect ») :
--   1) AuthService doit utiliser cette même base (chaîne de connexion).
--   2) auth-frontend en local : apiUrl doit pointer vers l’API (ex. http://localhost:5220/api),
--      pas seulement « /api » sans reverse-proxy.
--   3) Redémarrer Auth après correctif VerifyPassword (SuccessRehashNeeded) si hash SQL ancien.
-- =============================================================================

ALTER TABLE public."Users"
  ADD COLUMN IF NOT EXISTS "DocumentationDirectoryUserId" uuid NULL;

-- S’assurer que les rôles Planning existent (script séparé recommandé)
-- \i auth_db_roles_planning_binome.sql

-- Yasmine — alignée annuaire doc (pilote) → rôle Planning exact : "Pilote" (voir route /planning)
INSERT INTO public."Users" (
  "Username", "Email", "PasswordHash", "IsActive", "CreatedAt", "UpdatedAt",
  "RoleId", "RefreshToken", "RefreshTokenExpiryTime", "DocumentationDirectoryUserId"
)
SELECT
  'yasmine.elamrani',
  'yasmine.elamrani@atlas-tech-demo.dev',
  'AQAAAAIAAYagAAAAELVH9tEId8d73DRbZCwy5tOZtLq+2Sz3jbP2qmni21cB+tMAb2kvuMCEfhKJ68czsQ==',
  true, now(), now(),
  r."Id", NULL, NULL,
  '11111111-1111-4111-8111-111111111101'::uuid
FROM public."Roles" r
WHERE r."Name" = 'Pilote'
  AND NOT EXISTS (
    SELECT 1 FROM public."Users" u
    WHERE lower(u."Email") = lower('yasmine.elamrani@atlas-tech-demo.dev')
  );

-- Karim — aligné annuaire doc (manager) → rôle Planning exact : "Manager"
INSERT INTO public."Users" (
  "Username", "Email", "PasswordHash", "IsActive", "CreatedAt", "UpdatedAt",
  "RoleId", "RefreshToken", "RefreshTokenExpiryTime", "DocumentationDirectoryUserId"
)
SELECT
  'karim.tazi',
  'karim.tazi@atlas-tech-demo.dev',
  'AQAAAAIAAYagAAAAELVH9tEId8d73DRbZCwy5tOZtLq+2Sz3jbP2qmni21cB+tMAb2kvuMCEfhKJ68czsQ==',
  true, now(), now(),
  r."Id", NULL, NULL,
  '22222222-2222-4222-8222-222222222201'::uuid
FROM public."Roles" r
WHERE r."Name" = 'Manager'
  AND NOT EXISTS (
    SELECT 1 FROM public."Users" u
    WHERE lower(u."Email") = lower('karim.tazi@atlas-tech-demo.dev')
  );

-- Compte RH admin (Planning : /dashboard et modules RH → rôle exact "RH", pas ROLE_RH_ADMIN)
-- Mot de passe : DocAlign!2026 — même hash que les autres.
-- Liaison Documentation : remplir "DocumentationDirectoryUserId" si un utilisateur annuaire
--   avec rôle rh / email identique existe (sinon laisser NULL ou faire un UPDATE après).
INSERT INTO public."Users" (
  "Username", "Email", "PasswordHash", "IsActive", "CreatedAt", "UpdatedAt",
  "RoleId", "RefreshToken", "RefreshTokenExpiryTime", "DocumentationDirectoryUserId"
)
SELECT
  'rh.admin',
  'rh.admin@atlas-tech-demo.dev',
  'AQAAAAIAAYagAAAAELVH9tEId8d73DRbZCwy5tOZtLq+2Sz3jbP2qmni21cB+tMAb2kvuMCEfhKJ68czsQ==',
  true, now(), now(),
  r."Id", NULL, NULL,
  NULL::uuid
FROM public."Roles" r
WHERE r."Name" = 'RH'
  AND NOT EXISTS (
    SELECT 1 FROM public."Users" u
    WHERE lower(u."Email") = lower('rh.admin@atlas-tech-demo.dev')
  );

-- Si le compte RH existe déjà avec un mauvais rôle, forcer "RH"
UPDATE public."Users" u
SET "RoleId" = (SELECT r."Id" FROM public."Roles" r WHERE r."Name" = 'RH' LIMIT 1)
WHERE lower(u."Email") = lower('rh.admin@atlas-tech-demo.dev')
  AND EXISTS (SELECT 1 FROM public."Roles" r2 WHERE r2."Id" = u."RoleId" AND r2."Name" <> 'RH');

-- Si les lignes existent déjà avec d’anciens rôles (ex. ROLE_PILOT), corriger sans recréer :
UPDATE public."Users" u
SET "RoleId" = (SELECT r."Id" FROM public."Roles" r WHERE r."Name" = 'Pilote' LIMIT 1)
WHERE lower(u."Email") = lower('yasmine.elamrani@atlas-tech-demo.dev')
  AND EXISTS (SELECT 1 FROM public."Roles" r2 WHERE r2."Id" = u."RoleId" AND r2."Name" <> 'Pilote');

UPDATE public."Users" u
SET "RoleId" = (SELECT r."Id" FROM public."Roles" r WHERE r."Name" = 'Manager' LIMIT 1)
WHERE lower(u."Email") = lower('karim.tazi@atlas-tech-demo.dev')
  AND EXISTS (SELECT 1 FROM public."Roles" r2 WHERE r2."Id" = u."RoleId" AND r2."Name" <> 'Manager');

-- Fatima Alaoui — RH Documentation (directory_user déjà défini en base Documentation)
-- UUID annuaire doc fourni : 33333333-3333-4333-8333-333333333301
INSERT INTO public."Users" (
  "Username", "Email", "PasswordHash", "IsActive", "CreatedAt", "UpdatedAt",
  "RoleId", "RefreshToken", "RefreshTokenExpiryTime", "DocumentationDirectoryUserId"
)
SELECT
  'fatima.alaoui',
  'fatima.alaoui@atlas-tech-demo.dev',
  'AQAAAAIAAYagAAAAELVH9tEId8d73DRbZCwy5tOZtLq+2Sz3jbP2qmni21cB+tMAb2kvuMCEfhKJ68czsQ==',
  true, now(), now(),
  r."Id", NULL, NULL,
  '33333333-3333-4333-8333-333333333301'::uuid
FROM public."Roles" r
WHERE r."Name" = 'RH'
  AND NOT EXISTS (
    SELECT 1 FROM public."Users" u
    WHERE lower(u."Email") = lower('fatima.alaoui@atlas-tech-demo.dev')
  );

-- Si Fatima existe déjà, forcer le rôle RH + liaison annuaire doc pour personnaliser l'interface.
UPDATE public."Users" u
SET
  "RoleId" = (SELECT r."Id" FROM public."Roles" r WHERE r."Name" = 'RH' LIMIT 1),
  "DocumentationDirectoryUserId" = '33333333-3333-4333-8333-333333333301'::uuid,
  "IsActive" = true,
  "UpdatedAt" = now()
WHERE lower(u."Email") = lower('fatima.alaoui@atlas-tech-demo.dev');

-- Vérification
SELECT u."Id", u."Username", u."Email", ro."Name" AS role_name, u."DocumentationDirectoryUserId"
FROM public."Users" u
JOIN public."Roles" ro ON ro."Id" = u."RoleId"
WHERE u."Email" ILIKE '%atlas-tech-demo%'
ORDER BY u."Id";
