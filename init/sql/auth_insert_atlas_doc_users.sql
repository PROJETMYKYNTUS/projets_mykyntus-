-- Comptes démo auth_db."Users" (schéma EF AuthService) — idempotent par e-mail.
-- Mot de passe en clair attendu par l’API : DocAlign!2026
-- Hash ASP.NET Core Identity (PasswordHasher<object>, v3 / PBKDF2) — généré pour ce mot de passe.
-- Rôles alignés sur le seed Program.cs : Employee = 1, RH = 2.

INSERT INTO "Users" ("Username", "Email", "PasswordHash", "IsActive", "CreatedAt", "UpdatedAt", "RoleId", "RefreshToken", "RefreshTokenExpiryTime")
SELECT 'fatima.alaoui@atlas-tech-demo.dev',
       'fatima.alaoui@atlas-tech-demo.dev',
       'AQAAAAIAAYagAAAAEB5nE9Iq0Y4Sma7qyTABa5SiO+Yst9ISZt3XiHepPgUlW7fXxxeJ4MgdR0uO1Ai+3g==',
       true,
       (NOW() AT TIME ZONE 'utc'),
       NULL,
       2,
       NULL,
       NULL
WHERE NOT EXISTS (
  SELECT 1 FROM "Users" u WHERE u."Email" = 'fatima.alaoui@atlas-tech-demo.dev'
);

INSERT INTO "Users" ("Username", "Email", "PasswordHash", "IsActive", "CreatedAt", "UpdatedAt", "RoleId", "RefreshToken", "RefreshTokenExpiryTime")
SELECT 'yasmine.elamrani@atlas-tech-demo.dev',
       'yasmine.elamrani@atlas-tech-demo.dev',
       'AQAAAAIAAYagAAAAEB5nE9Iq0Y4Sma7qyTABa5SiO+Yst9ISZt3XiHepPgUlW7fXxxeJ4MgdR0uO1Ai+3g==',
       true,
       (NOW() AT TIME ZONE 'utc'),
       NULL,
       1,
       NULL,
       NULL
WHERE NOT EXISTS (
  SELECT 1 FROM "Users" u WHERE u."Email" = 'yasmine.elamrani@atlas-tech-demo.dev'
);
