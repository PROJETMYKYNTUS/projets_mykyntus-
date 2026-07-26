using Documentation.Domain.Entities;
using Documentation.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Documentation.Infrastructure;

/// <summary>
/// Catalogue documentaire, demandes multi-statuts et audit pour démo Docker prod-like.
/// </summary>
internal static class DockerDocumentationEnrichmentSeed
{
    private const string Tenant = "atlas-tech-demo";
    private const string MarkerCode = "ENRICH-DEMO-V1";

    private static readonly Guid DeptId = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaa03");
    private static readonly Guid EmployeeId = Guid.Parse("11111111-1111-4111-8111-111111111103");
    private static readonly Guid YasmineId = Guid.Parse("11111111-1111-4111-8111-111111111101");
    private static readonly Guid ManagerId = Guid.Parse("11111111-1111-4111-8111-111111111105");
    private static readonly Guid RhId = Guid.Parse("11111111-1111-4111-8111-111111111104");
    private static readonly Guid CoachId = Guid.Parse("11111111-1111-4111-8111-111111111106");
    private static readonly Guid AdminId = Guid.Parse("11111111-1111-4111-8111-111111111108");

    internal static async Task ApplyIfEnabledAsync(
        IConfiguration configuration,
        DocumentationDbContext db,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        if (!IsEnabled(configuration))
            return;

        if (await db.DocumentTypes.AnyAsync(t => t.Code == MarkerCode, cancellationToken))
        {
            logger.LogInformation("Documentation enrichment déjà appliqué.");
            return;
        }

        var now = DateTimeOffset.UtcNow;

        var workflowId = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbb001");
        var workflow = new Workflow
        {
            Id = workflowId,
            Name = "Validation RH standard (démo CC Casablanca)",
            IsDefault = true,
            AuditEnabled = true,
            AuditLogs = true,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Workflows.Add(workflow);

        var stepManagerId = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbb002");
        var stepRhId = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbb003");
        db.WorkflowSteps.AddRange(
            new WorkflowStep
            {
                Id = stepManagerId,
                WorkflowId = workflowId,
                StepOrder = 1,
                StepKey = "manager_validation",
                Name = "Validation manager",
                AssignedRole = AppRole.Manager,
                SlaHours = 48,
                NotificationKey = WorkflowNotificationKey.Email,
                CreatedAt = now,
                UpdatedAt = now,
            },
            new WorkflowStep
            {
                Id = stepRhId,
                WorkflowId = workflowId,
                StepOrder = 2,
                StepKey = "rh_validation",
                Name = "Validation RH",
                AssignedRole = AppRole.Rh,
                SlaHours = 72,
                NotificationKey = WorkflowNotificationKey.Email,
                CreatedAt = now,
                UpdatedAt = now,
            });

        var typeAttestationId = Guid.Parse("cccccccc-cccc-4ccc-8ccc-cccccccccc01");
        var typeSalaireId = Guid.Parse("cccccccc-cccc-4ccc-8ccc-cccccccccc02");
        var typeCnssId = Guid.Parse("cccccccc-cccc-4ccc-8ccc-cccccccccc03");
        var typeStageId = Guid.Parse("cccccccc-cccc-4ccc-8ccc-cccccccccc04");

        db.DocumentTypes.AddRange(
            new DocumentType
            {
                Id = typeAttestationId,
                Code = "ATT-TRAVAIL",
                Name = "Attestation de travail",
                Description = "Attestation employeur — centre d'appels Casablanca (démo)",
                RetentionDays = 365,
                WorkflowId = workflowId,
                IsMandatory = false,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
            },
            new DocumentType
            {
                Id = typeSalaireId,
                Code = "CERT-SALAIRE",
                Name = "Certificat de salaire",
                Description = "Pour démarches bancaires (démo)",
                RetentionDays = 365,
                WorkflowId = workflowId,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
            },
            new DocumentType
            {
                Id = typeCnssId,
                Code = "ATT-CNSS",
                Name = "Attestation CNSS",
                Description = "Attestation affiliation CNSS (démo)",
                RetentionDays = 730,
                WorkflowId = workflowId,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
            },
            new DocumentType
            {
                Id = typeStageId,
                Code = MarkerCode,
                Name = "Convention de stage",
                Description = "Convention stage inbound grands comptes (démo)",
                RetentionDays = 365,
                WorkflowId = workflowId,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
            });

        var templateId = Guid.Parse("dddddddd-dddd-4ddd-8ddd-dddddddddd01");
        var templateVersionId = Guid.Parse("dddddddd-dddd-4ddd-8ddd-dddddddddd02");
        var templateContent =
            """{"type":"html","body":"<h1>Attestation de travail</h1><p>Je soussigné(e), représentant d'Atlas Contact Centre Casablanca, atteste que {{nom}} {{prenom}}, CIN {{cin}}, est employé(e) depuis {{date_entree}}.</p>"}""";

        var template = new DocumentTemplate
        {
            Id = templateId,
            TenantId = Tenant,
            Code = "TMPL-ATT-TRAVAIL",
            Name = "Modèle attestation travail (démo)",
            Description = "Template dynamique HTML — inbound grands comptes",
            Source = "DEMO",
            Kind = DocumentTemplateKind.Dynamic,
            IsActive = true,
            DocumentTypeId = typeAttestationId,
            UpdatedAt = now,
        };
        db.DocumentTemplates.Add(template);

        db.DocumentTemplateVersions.Add(new DocumentTemplateVersion
        {
            Id = templateVersionId,
            TemplateId = templateId,
            TenantId = Tenant,
            VersionNumber = 1,
            Status = "published",
            StructuredContent = templateContent,
            CreatedByUserId = RhId,
            CreatedAt = now,
            PublishedAt = now,
        });

        await db.SaveChangesAsync(cancellationToken);

        template.CurrentVersionId = templateVersionId;
        await db.SaveChangesAsync(cancellationToken);

        db.DocumentTemplateVariables.AddRange(
            new DocumentTemplateVariable
            {
                Id = Guid.Parse("eeeeeeee-eeee-4eee-8eee-eeeeeeeeee01"),
                TemplateId = templateId,
                TemplateVersionId = templateVersionId,
                VariableName = "nom",
                DisplayLabel = "Nom",
                VariableType = "text",
                IsRequired = true,
                FormScope = "db",
                SortOrder = 1,
            },
            new DocumentTemplateVariable
            {
                Id = Guid.Parse("eeeeeeee-eeee-4eee-8eee-eeeeeeeeee02"),
                TemplateId = templateId,
                TemplateVersionId = templateVersionId,
                VariableName = "prenom",
                DisplayLabel = "Prénom",
                VariableType = "text",
                IsRequired = true,
                FormScope = "db",
                SortOrder = 2,
            },
            new DocumentTemplateVariable
            {
                Id = Guid.Parse("eeeeeeee-eeee-4eee-8eee-eeeeeeeeee03"),
                TemplateId = templateId,
                TemplateVersionId = templateVersionId,
                VariableName = "cin",
                DisplayLabel = "CIN",
                VariableType = "text",
                IsRequired = true,
                FormScope = "hr",
                SortOrder = 3,
            },
            new DocumentTemplateVariable
            {
                Id = Guid.Parse("eeeeeeee-eeee-4eee-8eee-eeeeeeeeee04"),
                TemplateId = templateId,
                TemplateVersionId = templateVersionId,
                VariableName = "date_entree",
                DisplayLabel = "Date d'entrée",
                VariableType = "date",
                IsRequired = true,
                FormScope = "hr",
                SortOrder = 4,
            });

        await db.SaveChangesAsync(cancellationToken);

        await SeedDocumentRequestsAsync(db, now, typeAttestationId, typeSalaireId, typeCnssId, templateId, templateVersionId, cancellationToken);
        await SeedAuditLogsAsync(db, now, cancellationToken);

        logger.LogInformation("Documentation enrichment terminé (tenant {Tenant}).", Tenant);
    }

    private static bool IsEnabled(IConfiguration configuration) =>
        configuration.GetValue("Documentation:DemoDataSeed", false)
        && string.Equals(configuration["KYNTUS_DEMO_ENRICHMENT"] ?? "true", "true", StringComparison.OrdinalIgnoreCase);

    private static async Task SeedDocumentRequestsAsync(
        DocumentationDbContext db,
        DateTimeOffset now,
        Guid typeAttestationId,
        Guid typeSalaireId,
        Guid typeCnssId,
        Guid templateId,
        Guid templateVersionId,
        CancellationToken ct)
    {
        var requests = new List<(Guid Id, Guid Requester, Guid Beneficiary, Guid? TypeId, DocumentRequestStatus Status, string Reason)>
        {
            (Guid.Parse("ffffffff-ffff-4fff-8fff-fffffffffff1"), EmployeeId, EmployeeId, typeAttestationId, DocumentRequestStatus.Pending, "Attestation pour banque (démo)"),
            (Guid.Parse("ffffffff-ffff-4fff-8fff-fffffffffff2"), YasmineId, YasmineId, typeSalaireId, DocumentRequestStatus.Pending, "Crédit immobilier (démo)"),
            (Guid.Parse("ffffffff-ffff-4fff-8fff-fffffffffff3"), CoachId, CoachId, typeCnssId, DocumentRequestStatus.Pending, "Renouvellement CNSS (démo)"),
            (Guid.Parse("ffffffff-ffff-4fff-8fff-fffffffffff4"), EmployeeId, EmployeeId, typeAttestationId, DocumentRequestStatus.Pending, "Visa — attestation employeur (démo)"),
            (Guid.Parse("ffffffff-ffff-4fff-8fff-fffffffffff5"), ManagerId, ManagerId, typeSalaireId, DocumentRequestStatus.Approved, "Dossier RH manager (démo)"),
            (Guid.Parse("ffffffff-ffff-4fff-8fff-fffffffffff6"), YasmineId, YasmineId, typeAttestationId, DocumentRequestStatus.Approved, "Attestation validée (démo)"),
            (Guid.Parse("ffffffff-ffff-4fff-8fff-fffffffffff7"), CoachId, CoachId, typeCnssId, DocumentRequestStatus.Rejected, "CNSS — dossier incomplet (démo)"),
        };

        var seq = 1;
        foreach (var r in requests)
        {
            var req = new DocumentRequest
            {
                Id = r.Id,
                TenantId = Tenant,
                RequestNumber = $"REQ-{now.Year}-{seq:D6}",
                RequesterUserId = r.Requester,
                BeneficiaryUserId = r.Beneficiary,
                DocumentTypeId = r.TypeId,
                DocumentTemplateId = r.TypeId == typeAttestationId ? templateId : null,
                Reason = r.Reason,
                Status = r.Status,
                OrganizationalUnitId = DeptId,
                CreatedAt = now.AddDays(-seq),
                UpdatedAt = now.AddDays(-seq),
                DecidedByUserId = r.Status is DocumentRequestStatus.Approved or DocumentRequestStatus.Rejected ? RhId : null,
                DecidedAt = r.Status is DocumentRequestStatus.Approved or DocumentRequestStatus.Rejected ? now.AddDays(-seq + 1) : null,
                RejectionReason = r.Status == DocumentRequestStatus.Rejected ? "Pièce justificative manquante (démo)" : null,
            };
            db.DocumentRequests.Add(req);
            seq++;
        }

        var generatedRequestIds = new[]
        {
            Guid.Parse("ffffffff-ffff-4fff-8fff-000000000801"),
            Guid.Parse("ffffffff-ffff-4fff-8fff-000000000802"),
            Guid.Parse("ffffffff-ffff-4fff-8fff-000000000803"),
            Guid.Parse("ffffffff-ffff-4fff-8fff-000000000804"),
            Guid.Parse("ffffffff-ffff-4fff-8fff-000000000805"),
        };

        var generatedDocumentIds = new[]
        {
            Guid.Parse("88888888-8888-4888-8888-000000000901"),
            Guid.Parse("88888888-8888-4888-8888-000000000902"),
            Guid.Parse("88888888-8888-4888-8888-000000000903"),
            Guid.Parse("88888888-8888-4888-8888-000000000904"),
            Guid.Parse("88888888-8888-4888-8888-000000000905"),
        };

        var generatedRequesters = new[] { EmployeeId, YasmineId, EmployeeId, RhId, ManagerId };
        for (var i = 0; i < generatedRequestIds.Length; i++)
        {
            var reqId = generatedRequestIds[i];
            var requester = generatedRequesters[i];
            var req = new DocumentRequest
            {
                Id = reqId,
                TenantId = Tenant,
                RequestNumber = $"REQ-{now.Year}-{seq:D6}",
                RequesterUserId = requester,
                BeneficiaryUserId = requester,
                DocumentTypeId = typeAttestationId,
                DocumentTemplateId = templateId,
                Reason = "Document généré (démo inbound)",
                Status = DocumentRequestStatus.Generated,
                OrganizationalUnitId = DeptId,
                CreatedAt = now.AddDays(-10 - i),
                UpdatedAt = now.AddDays(-5 - i),
                DecidedByUserId = RhId,
                DecidedAt = now.AddDays(-6 - i),
            };
            db.DocumentRequests.Add(req);

            db.GeneratedDocuments.Add(new GeneratedDocument
            {
                Id = generatedDocumentIds[i],
                DocumentRequestId = reqId,
                OwnerUserId = requester,
                DocumentTypeId = typeAttestationId,
                TemplateVersionId = templateVersionId,
                FileName = $"attestation-demo-{i + 1}.pdf",
                StorageUri = "demo/local/stub",
                MimeType = "application/pdf",
                FileSizeBytes = 1024,
                Status = GeneratedDocumentStatus.Generated,
                VersionNumber = 1,
                ContentGenerated = "Attestation de travail — Atlas Contact Centre Casablanca (démo).",
                ContentFinal = "Attestation de travail — Atlas Contact Centre Casablanca (démo).",
                CreatedAt = now.AddDays(-5 - i),
                UpdatedAt = now.AddDays(-5 - i),
            });
            seq++;
        }

        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedAuditLogsAsync(DocumentationDbContext db, DateTimeOffset now, CancellationToken ct)
    {
        if (await db.AuditLogs.AnyAsync(
                a => a.Details != null && EF.Functions.Like(a.Details, "%DEMO_ENRICHMENT%"), ct))
            return;

        var actions = new[]
        {
            ("DOCUMENT_REQUEST_CREATED", "DocumentRequest"),
            ("DOCUMENT_REQUEST_APPROVED", "DocumentRequest"),
            ("DOCUMENT_GENERATED", "GeneratedDocument"),
            ("DOCUMENT_REQUEST_REJECTED", "DocumentRequest"),
            ("TEMPLATE_PUBLISHED", "DocumentTemplate"),
        };

        for (var i = 0; i < actions.Length; i++)
        {
            var (action, entityType) = actions[i];
            for (var j = 0; j < 3; j++)
            {
                db.AuditLogs.Add(new AuditLog
                {
                    Id = Guid.NewGuid(),
                    TenantId = Tenant,
                    OccurredAt = now.AddDays(-i * 2 - j),
                    ActorUserId = i % 2 == 0 ? RhId : AdminId,
                    Action = action,
                    EntityType = entityType,
                    EntityId = Guid.NewGuid(),
                    Details = $"DEMO_ENRICHMENT — {action} (inbound grands comptes)",
                    Success = true,
                    RequestNumber = $"REQ-{now.Year}-{(i * 3 + j + 1):D6}",
                });
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
