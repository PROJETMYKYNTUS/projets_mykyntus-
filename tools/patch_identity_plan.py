import pathlib
root = pathlib.Path(r"c:/Users/Pc/Desktop/PROD/projets_mykyntus-")
seed = root / "documentation_service_backend/DocumentationBackend/Infrastructure/DockerDocumentationDemoDataSeed.cs"
text = seed.read_text(encoding="utf-8")
if "admin@kyntus.ma" in text:
    print("seed already patched")
else:
    users = [
        ("11111111-1111-4111-8111-111111111103", "Employé", "Démo", "employee@kyntus.ma", "pilote"),
        ("11111111-1111-4111-8111-111111111104", "Rh", "Démo", "rh@kyntus.ma", "rh"),
        ("11111111-1111-4111-8111-111111111105", "Manager", "Démo", "manager@kyntus.ma", "manager"),
        ("11111111-1111-4111-8111-111111111106", "Coach", "Démo", "coach@kyntus.ma", "coach"),
        ("11111111-1111-4111-8111-111111111107", "Rp", "Démo", "rp@kyntus.ma", "rp"),
        ("11111111-1111-4111-8111-111111111108", "Admin", "Démo", "admin@kyntus.ma", "admin"),
        ("11111111-1111-4111-8111-111111111109", "Audit", "Démo", "audit@kyntus.ma", "audit"),
        ("11111111-1111-4111-8111-111111111110", "Formation", "Démo", "formation@kyntus.ma", "rh"),
    ]
    block = ""
    for uid, prenom, nom, email, role in users:
        block += f"""
                INSERT INTO documentation.directory_users (id, tenant_id, prenom, nom, email, role, manager_id, coach_id, rp_id, pole_id, cellule_id, departement_id, created_at, updated_at)
                SELECT '{uid}'::uuid, 'atlas-tech-demo', '{prenom}', '{nom}', '{email}',
                       '{role}'::documentation.app_role, NULL, NULL, NULL,
                       'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaa01'::uuid, 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaa02'::uuid, 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaa03'::uuid,
                       now(), now()
                WHERE NOT EXISTS (SELECT 1 FROM documentation.directory_users WHERE tenant_id = 'atlas-tech-demo' AND lower(email) = lower('{email}'));
"""
    marker = "WHERE NOT EXISTS (SELECT 1 FROM documentation.directory_users WHERE id = '11111111-1111-4111-8111-111111111102'::uuid);\n\n                INSERT INTO documentation.document_request_sequences"
    repl = "WHERE NOT EXISTS (SELECT 1 FROM documentation.directory_users WHERE id = '11111111-1111-4111-8111-111111111102'::uuid);" + block + "\n                INSERT INTO documentation.document_request_sequences"
    text = text.replace(marker, repl)
    seed.write_text(text, encoding="utf-8")
    print("seed patched")
sql = root / "init/sql/documentation_insert_kyntus_directory_users.sql"
sql.write_text("-- Alignement annuaire documentation (Auth/Planning)\n" + block.replace("                ", ""), encoding="utf-8")
print("sql written", sql)
