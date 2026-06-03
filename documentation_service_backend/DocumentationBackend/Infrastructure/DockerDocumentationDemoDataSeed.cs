using DocumentationBackend.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DocumentationBackend.Infrastructure;

/// <summary>
/// Données de référence idempotentes pour un clone + Docker (tenant atlas-tech-demo, UUID aligné sur init/ocelot.gateway.json).
/// </summary>
internal static class DockerDocumentationDemoDataSeed
{
    internal static async Task ApplyIfEnabledAsync(
        IConfiguration configuration,
        DocumentationDbContext db,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        if (!configuration.GetValue("Documentation:DemoDataSeed", false))
            return;

        const string tenant = "atlas-tech-demo";
        try
        {
            await db.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO documentation.organisation_units (id, tenant_id, parent_id, unit_type, code, name, created_at, updated_at)
                SELECT 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaa01'::uuid, 'atlas-tech-demo', NULL, 'pole', 'DEMO-POLE', 'Pôle démo', now(), now()
                WHERE NOT EXISTS (SELECT 1 FROM documentation.organisation_units WHERE tenant_id = 'atlas-tech-demo' AND code = 'DEMO-POLE');

                INSERT INTO documentation.organisation_units (id, tenant_id, parent_id, unit_type, code, name, created_at, updated_at)
                SELECT 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaa02'::uuid, 'atlas-tech-demo', 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaa01'::uuid, 'cellule', 'DEMO-CELL', 'Cellule démo', now(), now()
                WHERE NOT EXISTS (SELECT 1 FROM documentation.organisation_units WHERE tenant_id = 'atlas-tech-demo' AND code = 'DEMO-CELL');

                INSERT INTO documentation.organisation_units (id, tenant_id, parent_id, unit_type, code, name, created_at, updated_at)
                SELECT 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaa03'::uuid, 'atlas-tech-demo', 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaa02'::uuid, 'departement', 'DEMO-DEPT', 'Département démo', now(), now()
                WHERE NOT EXISTS (SELECT 1 FROM documentation.organisation_units WHERE tenant_id = 'atlas-tech-demo' AND code = 'DEMO-DEPT');

                INSERT INTO documentation.directory_users (id, tenant_id, prenom, nom, email, role, manager_id, coach_id, rp_id, pole_id, cellule_id, departement_id, created_at, updated_at)
                SELECT '11111111-1111-4111-8111-111111111101'::uuid, 'atlas-tech-demo', 'Yasmine', 'El Amrani', 'yasmine.elamrani@atlas-tech-demo.dev',
                       'pilote'::documentation.app_role, NULL, NULL, NULL,
                       'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaa01'::uuid, 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaa02'::uuid, 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaa03'::uuid,
                       now(), now()
                WHERE NOT EXISTS (SELECT 1 FROM documentation.directory_users WHERE id = '11111111-1111-4111-8111-111111111101'::uuid);

                INSERT INTO documentation.directory_users (id, tenant_id, prenom, nom, email, role, manager_id, coach_id, rp_id, pole_id, cellule_id, departement_id, created_at, updated_at)
                SELECT '11111111-1111-4111-8111-111111111102'::uuid, 'atlas-tech-demo', 'Fatima', 'Alaoui', 'fatima.alaoui@atlas-tech-demo.dev',
                       'rh'::documentation.app_role, NULL, NULL, NULL,
                       'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaa01'::uuid, 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaa02'::uuid, 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaa03'::uuid,
                       now(), now()
                WHERE NOT EXISTS (SELECT 1 FROM documentation.directory_users WHERE id = '11111111-1111-4111-8111-111111111102'::uuid);
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

                INSERT INTO documentation.document_request_sequences (tenant_id, year, last_value)
                SELECT 'atlas-tech-demo', EXTRACT(year FROM now())::int, 0
                WHERE NOT EXISTS (
                  SELECT 1 FROM documentation.document_request_sequences
                  WHERE tenant_id = 'atlas-tech-demo' AND year = EXTRACT(year FROM now())::int);
                """,
                cancellationToken);

            logger.LogInformation(
                "Documentation:DemoDataSeed — données annuaire / organisation « {Tenant} » vérifiées ou insérées.",
                tenant);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Documentation:DemoDataSeed — échec (tables absentes ou droits PostgreSQL).");
            throw;
        }
    }
}
