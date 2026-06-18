-- Alignement annuaire documentation (Auth/Planning)

INSERT INTO documentation.directory_users (id, tenant_id, prenom, nom, email, role, manager_id, coach_id, rp_id, pole_id, cellule_id, departement_id, created_at, updated_at)
SELECT '11111111-1111-4111-8111-111111111103'::uuid, 'atlas-tech-demo', 'Employé', 'Démo', 'employee@kyntus.ma',
       'pilote'::documentation.app_role, NULL, NULL, NULL,
       'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaa01'::uuid, 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaa02'::uuid, 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaa03'::uuid,
       now(), now()
WHERE NOT EXISTS (SELECT 1 FROM documentation.directory_users WHERE tenant_id = 'atlas-tech-demo' AND lower(email) = lower('employee@kyntus.ma'));

INSERT INTO documentation.directory_users (id, tenant_id, prenom, nom, email, role, manager_id, coach_id, rp_id, pole_id, cellule_id, departement_id, created_at, updated_at)
SELECT '11111111-1111-4111-8111-111111111104'::uuid, 'atlas-tech-demo', 'Rh', 'Démo', 'rh@kyntus.ma',
       'rh'::documentation.app_role, NULL, NULL, NULL,
       'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaa01'::uuid, 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaa02'::uuid, 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaa03'::uuid,
       now(), now()
WHERE NOT EXISTS (SELECT 1 FROM documentation.directory_users WHERE tenant_id = 'atlas-tech-demo' AND lower(email) = lower('rh@kyntus.ma'));

INSERT INTO documentation.directory_users (id, tenant_id, prenom, nom, email, role, manager_id, coach_id, rp_id, pole_id, cellule_id, departement_id, created_at, updated_at)
SELECT '11111111-1111-4111-8111-111111111105'::uuid, 'atlas-tech-demo', 'Manager', 'Démo', 'manager@kyntus.ma',
       'manager'::documentation.app_role, NULL, NULL, NULL,
       'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaa01'::uuid, 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaa02'::uuid, 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaa03'::uuid,
       now(), now()
WHERE NOT EXISTS (SELECT 1 FROM documentation.directory_users WHERE tenant_id = 'atlas-tech-demo' AND lower(email) = lower('manager@kyntus.ma'));

INSERT INTO documentation.directory_users (id, tenant_id, prenom, nom, email, role, manager_id, coach_id, rp_id, pole_id, cellule_id, departement_id, created_at, updated_at)
SELECT '11111111-1111-4111-8111-111111111106'::uuid, 'atlas-tech-demo', 'Coach', 'Démo', 'coach@kyntus.ma',
       'coach'::documentation.app_role, NULL, NULL, NULL,
       'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaa01'::uuid, 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaa02'::uuid, 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaa03'::uuid,
       now(), now()
WHERE NOT EXISTS (SELECT 1 FROM documentation.directory_users WHERE tenant_id = 'atlas-tech-demo' AND lower(email) = lower('coach@kyntus.ma'));

INSERT INTO documentation.directory_users (id, tenant_id, prenom, nom, email, role, manager_id, coach_id, rp_id, pole_id, cellule_id, departement_id, created_at, updated_at)
SELECT '11111111-1111-4111-8111-111111111107'::uuid, 'atlas-tech-demo', 'Rp', 'Démo', 'rp@kyntus.ma',
       'rp'::documentation.app_role, NULL, NULL, NULL,
       'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaa01'::uuid, 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaa02'::uuid, 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaa03'::uuid,
       now(), now()
WHERE NOT EXISTS (SELECT 1 FROM documentation.directory_users WHERE tenant_id = 'atlas-tech-demo' AND lower(email) = lower('rp@kyntus.ma'));

INSERT INTO documentation.directory_users (id, tenant_id, prenom, nom, email, role, manager_id, coach_id, rp_id, pole_id, cellule_id, departement_id, created_at, updated_at)
SELECT '11111111-1111-4111-8111-111111111108'::uuid, 'atlas-tech-demo', 'Admin', 'Démo', 'admin@kyntus.ma',
       'admin'::documentation.app_role, NULL, NULL, NULL,
       'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaa01'::uuid, 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaa02'::uuid, 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaa03'::uuid,
       now(), now()
WHERE NOT EXISTS (SELECT 1 FROM documentation.directory_users WHERE tenant_id = 'atlas-tech-demo' AND lower(email) = lower('admin@kyntus.ma'));

INSERT INTO documentation.directory_users (id, tenant_id, prenom, nom, email, role, manager_id, coach_id, rp_id, pole_id, cellule_id, departement_id, created_at, updated_at)
SELECT '11111111-1111-4111-8111-111111111109'::uuid, 'atlas-tech-demo', 'Audit', 'Démo', 'audit@kyntus.ma',
       'audit'::documentation.app_role, NULL, NULL, NULL,
       'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaa01'::uuid, 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaa02'::uuid, 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaa03'::uuid,
       now(), now()
WHERE NOT EXISTS (SELECT 1 FROM documentation.directory_users WHERE tenant_id = 'atlas-tech-demo' AND lower(email) = lower('audit@kyntus.ma'));

INSERT INTO documentation.directory_users (id, tenant_id, prenom, nom, email, role, manager_id, coach_id, rp_id, pole_id, cellule_id, departement_id, created_at, updated_at)
SELECT '11111111-1111-4111-8111-111111111110'::uuid, 'atlas-tech-demo', 'Formation', 'Démo', 'formation@kyntus.ma',
       'rh'::documentation.app_role, NULL, NULL, NULL,
       'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaa01'::uuid, 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaa02'::uuid, 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaa03'::uuid,
       now(), now()
WHERE NOT EXISTS (SELECT 1 FROM documentation.directory_users WHERE tenant_id = 'atlas-tech-demo' AND lower(email) = lower('formation@kyntus.ma'));

INSERT INTO documentation.directory_users (id, tenant_id, prenom, nom, email, role, manager_id, coach_id, rp_id, pole_id, cellule_id, departement_id, created_at, updated_at)
SELECT '11111111-1111-4111-8111-111111111111'::uuid, 'atlas-tech-demo', 'Superviseur', 'Démo', 'superviseur@kyntus.ma',
       'manager'::documentation.app_role, NULL, NULL, NULL,
       'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaa01'::uuid, 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaa02'::uuid, 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaa03'::uuid,
       now(), now()
WHERE NOT EXISTS (SELECT 1 FROM documentation.directory_users WHERE tenant_id = 'atlas-tech-demo' AND lower(email) = lower('superviseur@kyntus.ma'));
