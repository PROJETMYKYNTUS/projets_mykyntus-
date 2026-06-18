-- Comptes démo auth_db."Users" (schéma EF AuthService) — idempotent par e-mail.
-- Mot de passe en clair attendu par l'API : DocAlign!2026
-- Hash ASP.NET Core Identity (PasswordHasher<object>, v3 / PBKDF2) — généré pour ce mot de passe.
-- Rôles alignés sur le seed Program.cs : Employee = 1, RH = 2.
-- SubjectId alignés sur KyntusSubjectIdCatalog (contrainte UNIQUE IX_Users_SubjectId).

INSERT INTO "Users" ("Username", "Email", "PasswordHash", "IsActive", "CreatedAt", "UpdatedAt", "RoleId", "RefreshToken", "RefreshTokenExpiryTime", "SubjectId")
SELECT 'fatima.alaoui@atlas-tech-demo.dev',
       'fatima.alaoui@atlas-tech-demo.dev',
       'AQAAAAIAAYagAAAAEB5nE9Iq0Y4Sma7qyTABa5SiO+Yst9ISZt3XiHepPgUlW7fXxxeJ4MgdR0uO1Ai+3g==',
       true,
       (NOW() AT TIME ZONE 'utc'),
       NULL,
       2,
       NULL,
       NULL,
       '11111111-1111-4111-8111-111111111102'::uuid
WHERE NOT EXISTS (
  SELECT 1 FROM "Users" u WHERE lower(u."Email") = lower('fatima.alaoui@atlas-tech-demo.dev')
);

INSERT INTO "Users" ("Username", "Email", "PasswordHash", "IsActive", "CreatedAt", "UpdatedAt", "RoleId", "RefreshToken", "RefreshTokenExpiryTime", "SubjectId")
SELECT 'yasmine.elamrani@atlas-tech-demo.dev',
       'yasmine.elamrani@atlas-tech-demo.dev',
       'AQAAAAIAAYagAAAAEB5nE9Iq0Y4Sma7qyTABa5SiO+Yst9ISZt3XiHepPgUlW7fXxxeJ4MgdR0uO1Ai+3g==',
       true,
       (NOW() AT TIME ZONE 'utc'),
       NULL,
       1,
       NULL,
       NULL,
       '11111111-1111-4111-8111-111111111101'::uuid
WHERE NOT EXISTS (
  SELECT 1 FROM "Users" u WHERE lower(u."Email") = lower('yasmine.elamrani@atlas-tech-demo.dev')
);

UPDATE "Users"
SET "SubjectId" = '11111111-1111-4111-8111-111111111102'::uuid
WHERE lower("Email") = lower('fatima.alaoui@atlas-tech-demo.dev')
  AND "SubjectId" = '00000000-0000-0000-0000-000000000000'::uuid;

UPDATE "Users"
SET "SubjectId" = '11111111-1111-4111-8111-111111111101'::uuid
WHERE lower("Email") = lower('yasmine.elamrani@atlas-tech-demo.dev')
  AND "SubjectId" = '00000000-0000-0000-0000-000000000000'::uuid;
