using Microsoft.EntityFrameworkCore;
using PrimeBackend.Services;

namespace PrimeBackend.Data;

public static class PrimeDbSeeder
{
    /// <summary>
    /// Seed de démarrage : hiérarchie 3 niveaux Pôle → Cellule → Service,
    /// employés (scénario centre d’appels / relation client au Maroc), matrice RBAC,
    /// workflow, config globale et 6 fiches de démo couvrant tous les statuts de validation.
    /// </summary>
    public static async Task SeedAsync(PrimeDbContext db, CancellationToken cancellationToken = default)
    {
        await SeedOrganizationAsync(db, cancellationToken);
        await SeedRbacAsync(db, cancellationToken);
        await SeedMissingManagerComptableRbacAsync(db, cancellationToken);
        await SeedWorkflowAsync(db, cancellationToken);
        await SeedDemoFichesAsync(db, cancellationToken);
    }

    // -------------------------------------------------------------------
    // 1. Organisation (Pôles → Cellules → Services) + Employés
    // -------------------------------------------------------------------
    private static async Task SeedOrganizationAsync(PrimeDbContext db, CancellationToken cancellationToken)
    {
        var d1 = new PoleEntity
        {
            Id = "d1",
            Name = "Relation client & centres d’appels — Casablanca (Maroc)",
            Cellules =
            [
                new CelluleEntity
                {
                    Id = "p1",
                    Name = "Plateforme inbound — grands comptes",
                    PoleId = "d1",
                    Services =
                    [
                        new ServiceEntity { Id = "c1", Name = "Agents 1er niveau (voice / chat)", CelluleId = "p1" },
                        new ServiceEntity { Id = "c2", Name = "Enquêtes NPS & rappels satisfaction", CelluleId = "p1" },
                    ]
                },
                new CelluleEntity
                {
                    Id = "p2",
                    Name = "Réclamations & rétention",
                    PoleId = "d1",
                    Services =
                    [
                        new ServiceEntity { Id = "c3", Name = "Suivi engagements & cantonnement", CelluleId = "p2" },
                    ]
                }
            ]
        };

        var d2 = new PoleEntity
        {
            Id = "d2",
            Name = "Support SI & pilotage qualité",
            Cellules =
            [
                new CelluleEntity
                {
                    Id = "p3",
                    Name = "Infrastructure télécom & réseau",
                    PoleId = "d2",
                    Services =
                    [
                        new ServiceEntity { Id = "c4", Name = "Supervision connectivité & ACD", CelluleId = "p3" }
                    ]
                }
            ]
        };

        db.Poles.AddRange(d1, d2);

        static EmployeeEntity E(
            string id, string fn, string ln, string role, string? parentId,
            string poleId, string celluleId, string serviceId, string email) =>
            new()
            {
                Id = id,
                FirstName = fn,
                LastName = ln,
                Role = role,
                ParentId = parentId,
                PoleId = poleId,
                CelluleId = celluleId,
                ServiceId = serviceId,
                Email = email
            };

        db.Employees.AddRange(
            E("e1", "Yasmine", "El Idrissi", "Pilote", "e8", "d1", "p1", "c1", "yasmine.elidrissi@contactcentre.ma"),
            E("e2", "Mehdi", "Chraibi", "Pilote", "e8", "d1", "p1", "c1", "mehdi.chraibi@contactcentre.ma"),
            E("e3", "Ghita", "Benkirane", "Chef de projet", "e6", "d1", "p1", "c1", "ghita.benkirane@contactcentre.ma"),
            E("e4", "Imane", "Fassi", "Pilote", "e8", "d1", "p1", "c1", "imane.fassi@contactcentre.ma"),
            E("e5", "Latifa", "Mansouri", "RH", null, "d2", "p3", "c4", "latifa.mansouri@contactcentre.ma"),
            E("e6", "Hicham", "Benjelloun", "Chef de projet", null, "d1", "p1", "c1", "hicham.benjelloun@contactcentre.ma"),
            E("e7", "Laila", "Zahidi", "Audit", null, "d1", "p1", "c1", "laila.zahidi@contactcentre.ma"),
            E("e8", "Omar", "Tazi", "Référent technique", "e9", "d1", "p1", "c1", "omar.tazi@contactcentre.ma"),
            E("e9", "Kenza", "Alami", "Superviseur", "e3", "d1", "p1", "c1", "kenza.alami@contactcentre.ma"),
            E("e10", "Nadia", "Benchrif", "Manager", "e6", "d1", "p1", "c1", "nadia.benchrif@contactcentre.ma"),
            E("e11", "Karim", "Oufkir", "Comptable", null, "d1", "p1", "c1", "karim.oufkir@contactcentre.ma"),
            E("e-admin", "Système", "Admin", "Admin", null, "d1", "p1", "c1", "admin@contactcentre.ma")
        );

        await db.SaveChangesAsync(cancellationToken);
    }

    // -------------------------------------------------------------------
    // 2. RBAC : matrice par défaut pour les 7 rôles
    // -------------------------------------------------------------------
    private static async Task SeedRbacAsync(PrimeDbContext db, CancellationToken cancellationToken)
    {
        if (await db.RbacPermissions.AnyAsync(cancellationToken)) return;
        var now = DateTimeOffset.UtcNow;

        static RbacPermissionEntity P(string role, string action, string scope, DateTimeOffset now) =>
            new() { Id = Guid.NewGuid(), Role = role, Action = action, Scope = scope, IsAllowed = true, CreatedAt = now };

        var rows = new List<RbacPermissionEntity>
        {
            // Admin : tout sur tout
            P("Admin", "Read",      "Global",  now),
            P("Admin", "Edit",      "Global",  now),
            P("Admin", "Validate",  "Global",  now),
            P("Admin", "Configure", "Global",  now),

            // RH : lecture globale + validation finale + configuration référentiels
            P("RH", "Read",      "Global",  now),
            P("RH", "Validate",  "Global",  now),
            P("RH", "Configure", "Global",  now),

            // Chef de projet : périmètre Pôle, valide après superviseur
            P("Chef de projet", "Read",     "Pole", now),
            P("Chef de projet", "Edit",     "Pole", now),
            P("Chef de projet", "Validate", "Pole", now),

            // Superviseur : périmètre Cellule
            P("Superviseur", "Read",     "Cellule", now),
            P("Superviseur", "Edit",     "Cellule", now),
            P("Superviseur", "Validate", "Cellule", now),

            // Référent technique : lecture seule (pas de validation / édition métier dans ce flux)
            P("Référent technique", "Read",     "Service", now),

            // Pilote : lecture de sa propre fiche
            P("Pilote", "Read", "Self", now),

            // Audit : lecture globale seule (jamais d'édition)
            P("Audit", "Read", "Global", now),

            // Manager & Comptable — fichier global PRIME (validations parallèles + compta)
            P("Manager", "Read", "Global", now),
            P("Manager", "Validate", "Global", now),
            P("Comptable", "Read", "Global", now),
            P("Comptable", "Validate", "Global", now),
        };

        db.RbacPermissions.AddRange(rows);
        await db.SaveChangesAsync(cancellationToken);
    }

    // -------------------------------------------------------------------
    // 3. Workflow : 4 étapes + config globale singleton
    // -------------------------------------------------------------------
    private static async Task SeedWorkflowAsync(PrimeDbContext db, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        if (!await db.WorkflowSteps.AnyAsync(cancellationToken))
        {
            db.WorkflowSteps.AddRange(
                new WorkflowStepConfigEntity
                {
                    Id = Guid.NewGuid(),
                    SortOrder = 1,
                    ApproverRole = "Superviseur",
                    FromStatus = PrimeValidationWorkflowService.Pending,
                    ToStatus = PrimeValidationWorkflowService.SuperviseurApproved,
                    IsActive = true,
                    SlaHours = 48,
                    CreatedAt = now,
                },
                new WorkflowStepConfigEntity
                {
                    Id = Guid.NewGuid(),
                    SortOrder = 2,
                    ApproverRole = "Chef de projet",
                    FromStatus = PrimeValidationWorkflowService.SuperviseurApproved,
                    ToStatus = PrimeValidationWorkflowService.ChefDeProjetApproved,
                    IsActive = true,
                    SlaHours = 72,
                    CreatedAt = now,
                },
                new WorkflowStepConfigEntity
                {
                    Id = Guid.NewGuid(),
                    SortOrder = 3,
                    ApproverRole = "RH",
                    FromStatus = PrimeValidationWorkflowService.ChefDeProjetApproved,
                    ToStatus = PrimeValidationWorkflowService.RhApproved,
                    IsActive = true,
                    SlaHours = 72,
                    CreatedAt = now,
                });
        }

        if (!await db.WorkflowGlobalConfigs.AnyAsync(cancellationToken))
        {
            db.WorkflowGlobalConfigs.Add(new WorkflowGlobalConfigEntity
            {
                Id = Guid.NewGuid(),
                NotificationsEnabled = true,
                GlobalSlaHours = 72,
                AllowBulkApprove = true,
                RequireRejectReason = true,
                UpdatedAt = now,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    // -------------------------------------------------------------------
    // 4. 6 fiches de démo couvrant tous les statuts du workflow
    // -------------------------------------------------------------------
    private static async Task SeedDemoFichesAsync(PrimeDbContext db, CancellationToken cancellationToken)
    {
        if (await db.SupervisorCellulePrimeDrafts.AnyAsync(cancellationToken)) return;

        var now = DateTimeOffset.UtcNow;
        const string period = "2026-04";
        const string supervisorUserId = "e9";   // Kenza Alami — superviseur de floor (cellule p1)
        const string celluleId = "p1";

        var draft = new SupervisorCellulePrimeDraftEntity
        {
            Id = Guid.NewGuid(),
            SupervisorUserId = supervisorUserId,
            CelluleId = celluleId,
            Period = period,
            TemplateId = "demo-template",
            TemplateDisplayName = "Grille prime agents — centre d’appels (avril 2026)",
            TemplateFormatVersion = 1,
            Status = "Draft",
            SchemaJson = "{\"fields\":[]}",
            CelluleSaisieJson = "{}",
            UpdatedAt = now,
        };
        db.SupervisorCellulePrimeDrafts.Add(draft);

        // 6 employés (les Pilotes + autres) pour couvrir 6 statuts
        var fixtures = new (string EmployeeId, string ServiceId, string Status, string? ApproverId, string? RejectedById, string? Reason)[]
        {
            ("e1", "c1", PrimeValidationWorkflowService.Pending,                       null, null, null),
            ("e2", "c1", PrimeValidationWorkflowService.SuperviseurApproved,          "e9", null, null),
            ("e4", "c1", PrimeValidationWorkflowService.SuperviseurApproved,          "e9", null, null),
            ("e8", "c1", PrimeValidationWorkflowService.ChefDeProjetApproved,         "e6", null, null),
            ("e9", "c1", PrimeValidationWorkflowService.RhApproved,                    "e5", null, null),
            ("e3", "c1", PrimeValidationWorkflowService.Rejected,                      null, "e9", "Écart entre appels traités (ACD) et saisie manuelle indicateur Q3 — à resynchroniser."),
        };

        foreach (var fx in fixtures)
        {
            db.EmployeePrimeServiceFiches.Add(new EmployeePrimeServiceFicheEntity
            {
                Id = Guid.NewGuid(),
                CellulePrimeDraftId = draft.Id,
                SupervisorUserId = supervisorUserId,
                EmployeeId = fx.EmployeeId,
                ServiceId = fx.ServiceId,
                CelluleId = celluleId,
                Period = period,
                ServiceSaisieJson = "{}",
                FillingStatus = "Complete",
                UpdatedAt = now,
                ValidationStatus = fx.Status,
                LastApproverUserId = fx.ApproverId,
                LastApprovedAt = fx.ApproverId is null ? null : now,
                RejectedByUserId = fx.RejectedById,
                RejectedAt = fx.RejectedById is null ? null : now,
                RejectionReason = fx.Reason,
                PrimeAmount = 1200m,
                ChallengeAmount = 300m,
                TotalAmount = 1500m,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Ajoute les permissions Manager / Comptable sur bases déjà initialisées sans ces rôles.</summary>
    private static async Task SeedMissingManagerComptableRbacAsync(PrimeDbContext db, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        static RbacPermissionEntity P(string role, string action, string scope, DateTimeOffset n) =>
            new() { Id = Guid.NewGuid(), Role = role, Action = action, Scope = scope, IsAllowed = true, CreatedAt = n };

        var toAdd = new List<RbacPermissionEntity>();
        foreach (var role in new[] { "Manager", "Comptable" })
        {
            if (await db.RbacPermissions.AnyAsync(x => x.Role == role, cancellationToken)) continue;
            toAdd.AddRange(
            [
                P(role, "Read", "Global", now),
                P(role, "Validate", "Global", now),
            ]);
        }

        if (toAdd.Count == 0) return;
        db.RbacPermissions.AddRange(toAdd);
        await db.SaveChangesAsync(cancellationToken);
    }
}
