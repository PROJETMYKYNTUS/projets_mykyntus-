using ClosedXML.Excel;
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
    public static async Task SeedAsync(PrimeDbContext db, bool includeDemoFiches, CancellationToken cancellationToken = default)
    {
        await SeedOrganizationAsync(db, cancellationToken);
        await SeedRbacAsync(db, cancellationToken);
        await SeedMissingManagerComptableRbacAsync(db, cancellationToken);
        await SeedMissingReferentTechnicalValidateRbacAsync(db, cancellationToken);
        await SeedWorkflowAsync(db, cancellationToken);
        await SeedGlobalPoolWorkflowAsync(db, cancellationToken);
        if (includeDemoFiches)
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
            E("e11", "Karim", "Oufkir", "Comptabilité", null, "d1", "p1", "c1", "karim.oufkir@contactcentre.ma"),
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

            // RH : lecture / config + validation du fichier synthèse globale (pas des fiches service)
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

            // Référent technique : 1er validateur fiche (périmètre service)
            P("Référent technique", "Read",     "Service", now),
            P("Référent technique", "Edit",     "Service", now),
            P("Référent technique", "Validate", "Service", now),

            // Pilote : lecture de sa propre fiche
            P("Pilote", "Read", "Self", now),

            // Audit : lecture globale seule (jamais d'édition)
            P("Audit", "Read", "Global", now),

            // Manager & Comptabilité — fichier global PRIME (validations parallèles + compta)
            P("Manager", "Read", "Global", now),
            P("Manager", "Validate", "Global", now),
            P("Comptabilité", "Read", "Global", now),
            P("Comptabilité", "Validate", "Global", now),
        };

        db.RbacPermissions.AddRange(rows);
        await db.SaveChangesAsync(cancellationToken);
    }

    // -------------------------------------------------------------------
    // 3. Workflow fiches : Référent → Superviseur → Chef de projet + config globale
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
                    ApproverRole = PrimeFicheValidationRoles.ReferentTechnique,
                    FromStatus = PrimeValidationWorkflowService.Pending,
                    ToStatus = PrimeValidationWorkflowService.ReferentTechniqueApproved,
                    IsActive = true,
                    SlaHours = 48,
                    CapturesAmountsOnApproval = true,
                    CreatedAt = now,
                    UpdatedAt = now,
                },
                new WorkflowStepConfigEntity
                {
                    Id = Guid.NewGuid(),
                    SortOrder = 2,
                    ApproverRole = PrimeFicheValidationRoles.Superviseur,
                    FromStatus = PrimeValidationWorkflowService.ReferentTechniqueApproved,
                    ToStatus = PrimeValidationWorkflowService.SuperviseurApproved,
                    IsActive = true,
                    SlaHours = 48,
                    CreatedAt = now,
                    UpdatedAt = now,
                },
                new WorkflowStepConfigEntity
                {
                    Id = Guid.NewGuid(),
                    SortOrder = 3,
                    ApproverRole = PrimeFicheValidationRoles.ChefDeProjet,
                    FromStatus = PrimeValidationWorkflowService.SuperviseurApproved,
                    ToStatus = PrimeValidationWorkflowService.ChefDeProjetApproved,
                    IsActive = true,
                    SlaHours = 72,
                    TerminalApproved = true,
                    CreatedAt = now,
                    UpdatedAt = now,
                });
        }
        else
        {
            await EnsureOperationalFicheWorkflowOnlyAsync(db, now, cancellationToken);
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

    private static async Task SeedGlobalPoolWorkflowAsync(PrimeDbContext db, CancellationToken cancellationToken)
    {
        if (await db.GlobalPoolWorkflowSteps.AnyAsync(cancellationToken)) return;
        var now = DateTimeOffset.UtcNow;
        db.GlobalPoolWorkflowSteps.AddRange(
            new GlobalPoolWorkflowStepEntity
            {
                Id = Guid.NewGuid(),
                SortOrder = 1,
                ApproverRole = "Manager",
                IsRequired = true,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
            },
            new GlobalPoolWorkflowStepEntity
            {
                Id = Guid.NewGuid(),
                SortOrder = 1,
                ApproverRole = "RH",
                IsRequired = true,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
            },
            new GlobalPoolWorkflowStepEntity
            {
                Id = Guid.NewGuid(),
                SortOrder = 2,
                ApproverRole = "Comptabilité",
                IsRequired = true,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
            });
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
            SchemaJson = PrimeDemoTemplateSchema.MinimalRaccSavJson(
                "Grille prime agents — centre d’appels (avril 2026)",
                "Grille"),
            CelluleSaisieJson = "{}",
            UpdatedAt = now,
        };
        db.SupervisorCellulePrimeDrafts.Add(draft);

        using (var wb = new XLWorkbook())
        {
            var ws = wb.AddWorksheet("Synthèse");
            ws.Cell(1, 1).Value = "PRIME — fichier global de démonstration (Manager + RH + Compta)";
            ws.Cell(2, 1).Value = "Période";
            ws.Cell(2, 2).Value = period;
            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            draft.GlobalPoolExcelContent = ms.ToArray();
            draft.GlobalPoolFileName = $"PRIME_synthese_globale_{period}.xlsx";
            draft.GlobalPoolUploadedAt = now;
            draft.GlobalPoolUploadedByUserId = "seed";
        }

        // 6 employés (les Pilotes + autres) pour couvrir 6 statuts
        var fixtures = new (string EmployeeId, string ServiceId, string Status, string? ApproverId, string? RejectedById, string? Reason)[]
        {
            ("e1", "c1", PrimeValidationWorkflowService.Pending,                       null, null, null),
            ("e2", "c1", PrimeValidationWorkflowService.ReferentTechniqueApproved,    "e8", null, null),
            ("e4", "c1", PrimeValidationWorkflowService.SuperviseurApproved,          "e9", null, null),
            ("e8", "c1", PrimeValidationWorkflowService.ChefDeProjetApproved,         "e6", null, null),
            ("e9", "c1", PrimeValidationWorkflowService.ChefDeProjetApproved,         "e6", null, null),
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
    public static async Task SeedMissingManagerComptableRbacAsync(PrimeDbContext db, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        static RbacPermissionEntity P(string role, string action, string scope, DateTimeOffset n) =>
            new() { Id = Guid.NewGuid(), Role = role, Action = action, Scope = scope, IsAllowed = true, CreatedAt = n };

        var toAdd = new List<RbacPermissionEntity>();
        foreach (var role in new[] { "Manager", "Comptabilité" })
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

    /// <summary>Ajoute Read/Validate/Edit Service pour Référent technique (et Coach) sur bases déjà initialisées.</summary>
    public static async Task SeedMissingReferentTechnicalValidateRbacAsync(PrimeDbContext db, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        static RbacPermissionEntity P(string role, string action, string scope, DateTimeOffset n) =>
            new() { Id = Guid.NewGuid(), Role = role, Action = action, Scope = scope, IsAllowed = true, CreatedAt = n };

        var toAdd = new List<RbacPermissionEntity>();
        foreach (var role in new[] { PrimeFicheValidationRoles.ReferentTechnique, "Coach" })
        {
            foreach (var (action, scope) in new[] { ("Read", "Service"), ("Edit", "Service"), ("Validate", "Service") })
            {
                if (await db.RbacPermissions.AnyAsync(
                        x => x.Role == role && x.Action == action && x.Scope == scope,
                        cancellationToken))
                    continue;
                toAdd.Add(P(role, action, scope, now));
            }
        }

        if (toAdd.Count == 0) return;
        db.RbacPermissions.AddRange(toAdd);
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Fiches : Référent → Superviseur → Chef (terminal). Désactive RH/Manager/Compta sur ce flux.
    /// </summary>
    public static async Task EnsureOperationalFicheWorkflowOnlyAsync(
        PrimeDbContext db,
        DateTimeOffset? now = null,
        CancellationToken cancellationToken = default)
    {
        var ts = now ?? DateTimeOffset.UtcNow;
        var steps = await db.WorkflowSteps.ToListAsync(cancellationToken);
        if (steps.Count == 0) return;

        var nonOperational = new HashSet<string>(StringComparer.Ordinal)
        {
            "RH", "Manager", "Comptabilité", "Comptable", "Admin", "Audit", "Pilote",
        };

        foreach (var s in steps)
        {
            if (nonOperational.Contains(s.ApproverRole) ||
                PrimeFicheValidationRoles.IsGlobalPoolStakeholder(s.ApproverRole))
            {
                s.IsActive = false;
                s.TerminalApproved = false;
                s.UpdatedAt = ts;
            }
        }

        var referent = steps.FirstOrDefault(s =>
            string.Equals(s.ApproverRole, PrimeFicheValidationRoles.ReferentTechnique, StringComparison.Ordinal));
        var superviseur = steps.FirstOrDefault(s =>
            string.Equals(s.ApproverRole, PrimeFicheValidationRoles.Superviseur, StringComparison.Ordinal));
        var chef = steps.FirstOrDefault(s =>
            string.Equals(s.ApproverRole, PrimeFicheValidationRoles.ChefDeProjet, StringComparison.Ordinal));

        if (referent is null)
        {
            referent = new WorkflowStepConfigEntity
            {
                Id = Guid.NewGuid(),
                SortOrder = 1,
                ApproverRole = PrimeFicheValidationRoles.ReferentTechnique,
                FromStatus = PrimeValidationWorkflowService.Pending,
                ToStatus = PrimeValidationWorkflowService.ReferentTechniqueApproved,
                IsActive = true,
                SlaHours = 48,
                CapturesAmountsOnApproval = true,
                CreatedAt = ts,
                UpdatedAt = ts,
            };
            db.WorkflowSteps.Add(referent);
            steps.Add(referent);
        }
        else
        {
            referent.IsActive = true;
            referent.SortOrder = 1;
            referent.ToStatus = PrimeValidationWorkflowService.ReferentTechniqueApproved;
            referent.CapturesAmountsOnApproval = true;
            referent.TerminalApproved = false;
            referent.UpdatedAt = ts;
        }

        if (superviseur is null)
        {
            superviseur = new WorkflowStepConfigEntity
            {
                Id = Guid.NewGuid(),
                SortOrder = 2,
                ApproverRole = PrimeFicheValidationRoles.Superviseur,
                FromStatus = PrimeValidationWorkflowService.ReferentTechniqueApproved,
                ToStatus = PrimeValidationWorkflowService.SuperviseurApproved,
                IsActive = true,
                SlaHours = 48,
                CreatedAt = ts,
                UpdatedAt = ts,
            };
            db.WorkflowSteps.Add(superviseur);
            steps.Add(superviseur);
        }
        else
        {
            superviseur.IsActive = true;
            superviseur.SortOrder = 2;
            superviseur.ToStatus = PrimeValidationWorkflowService.SuperviseurApproved;
            superviseur.CapturesAmountsOnApproval = false;
            superviseur.TerminalApproved = false;
            superviseur.UpdatedAt = ts;
        }

        if (chef is null)
        {
            chef = new WorkflowStepConfigEntity
            {
                Id = Guid.NewGuid(),
                SortOrder = 3,
                ApproverRole = PrimeFicheValidationRoles.ChefDeProjet,
                FromStatus = PrimeValidationWorkflowService.SuperviseurApproved,
                ToStatus = PrimeValidationWorkflowService.ChefDeProjetApproved,
                IsActive = true,
                SlaHours = 72,
                TerminalApproved = true,
                CreatedAt = ts,
                UpdatedAt = ts,
            };
            db.WorkflowSteps.Add(chef);
            steps.Add(chef);
        }
        else
        {
            chef.IsActive = true;
            chef.SortOrder = 3;
            chef.ToStatus = PrimeValidationWorkflowService.ChefDeProjetApproved;
            chef.CapturesAmountsOnApproval = false;
            chef.TerminalApproved = true;
            superviseur!.TerminalApproved = false;
            referent!.TerminalApproved = false;
            chef.UpdatedAt = ts;
        }

        WorkflowStepConfigRechain.ApplyToActiveSteps(steps);
        foreach (var s in steps.Where(x => x.IsActive))
            s.UpdatedAt = ts;

        await db.SaveChangesAsync(cancellationToken);
        await SeedMissingReferentTechnicalValidateRbacAsync(db, cancellationToken);
    }
}
