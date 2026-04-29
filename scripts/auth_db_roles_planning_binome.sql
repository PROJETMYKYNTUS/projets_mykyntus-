-- =============================================================================
-- auth_db — Rôles alignés sur le frontend Planning (binôme)
-- Source : projet_kyntus_service_planning-frontend/src/app/app.routes.ts
-- Le JWT reprend Role."Name" tel quel ; AuthGuard fait includes() SENSIBLE À LA CASSE.
--
-- Noms utilisés dans les routes (à respecter caractère par caractère) :
--   Admin, RH, Employee, Manager, Coach, RP, Pilote, Audit, Equipe_Formation
--
-- Inscription Auth (AuthenticationService) : rôle par défaut attendu = "Employee"
--   (voir AuthService/Service/AuthenticationService.cs — GetByNameAsync("Employee")).
--
-- ANOMALIE binôme : route /reclamations contient 'employee' (minuscule) alors que
--   /dashboard-employee utilise 'Employee'. Un seul Role.Name ne peut pas matcher
--   les deux ; en pratique utilisez "Employee" pour le dashboard employé.
-- =============================================================================

INSERT INTO public."Roles" ("Name", "Description", "CreatedAt")
SELECT v."Name", v."Description", now()
FROM (VALUES
  ('Admin', 'Administrateur'),
  ('RH', 'Ressources humaines'),
  ('Employee', 'Employé (défaut inscription Auth + dashboard employé)'),
  ('Manager', 'Manager'),
  ('Coach', 'Coach'),
  ('RP', 'Responsable point de vente'),
  ('Pilote', 'Pilote / employé terrain'),
  ('Audit', 'Audit'),
  ('Equipe_Formation', 'Équipe formation')
) AS v("Name", "Description")
WHERE NOT EXISTS (SELECT 1 FROM public."Roles" r WHERE r."Name" = v."Name");
