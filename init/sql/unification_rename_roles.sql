-- Unification hiérarchie Prime v2 — renommage des rôles (Phase 2B)
-- Exécuter atomiquement sur auth_db, kyntus_db, conge_db

-- auth_db
UPDATE "Roles" SET "Name" = 'Pilote', "Description" = 'Pilote' WHERE "Id" = 1;
UPDATE "Roles" SET "Name" = 'Référent technique', "Description" = 'Référent technique' WHERE "Id" = 4;
UPDATE "Roles" SET "Name" = 'Chef de projet', "Description" = 'Chef de projet' WHERE "Id" = 5;
-- Manager (3) conservé transverse ; Superviseur (9) pour hiérarchique Planning
UPDATE "Users" SET "RoleId" = 9
WHERE "RoleId" = 3
  AND "Email" IN (
    SELECT u."Email" FROM "Users" u
    INNER JOIN kyntus_db_link.users ku ON ku."Email" = u."Email"
    WHERE ku."RoleId" IN (SELECT "Id" FROM kyntus_db_link."Roles" WHERE "Name" IN ('Manager', 'Superviseur'))
  );

-- kyntus_db
UPDATE "Roles" SET "Name" = 'Pilote', "Description" = 'Pilote' WHERE "Name" = 'Employee';
UPDATE "Roles" SET "Name" = 'Référent technique', "Description" = 'Référent technique' WHERE "Name" = 'Coach';
UPDATE "Roles" SET "Name" = 'Chef de projet', "Description" = 'Chef de projet' WHERE "Name" = 'RP';
UPDATE "Roles" SET "Name" = 'Superviseur', "Description" = 'Superviseur de cellule' WHERE "Name" = 'Manager';

-- conge_db
UPDATE employe_snapshots SET role = 'Pilote' WHERE role IN ('Employee', 'Pilote');
UPDATE employe_snapshots SET role = 'Superviseur' WHERE role = 'Manager';
UPDATE employe_snapshots SET role = 'Référent technique' WHERE role = 'Coach';
UPDATE employe_snapshots SET role = 'Chef de projet' WHERE role = 'RP';
