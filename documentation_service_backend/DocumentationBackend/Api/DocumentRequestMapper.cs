using DocumentationBackend.Context;
using DocumentationBackend.Data;
using DocumentationBackend.Data.Entities;

namespace DocumentationBackend.Api;

internal static class DocumentRequestMapper
{
    internal static DocumentRequestResponse ToResponse(
        DocumentRequest r,
        DocumentType? documentType,
        DocumentationUserContext userContext,
        IReadOnlyDictionary<Guid, string> displayNames,
        GeneratedDocument? latestGenerated = null,
        string? documentTemplateName = null)
    {
        var typeLabel = r.IsCustomType
            ? (r.CustomTypeDescription ?? "Autre")
            : (documentType?.Name ?? "—");
        var displayId = !string.IsNullOrEmpty(r.RequestNumber) ? r.RequestNumber! : r.Id.ToString();

        var requesterName = DocumentRequestMappingHelper.ResolveName(displayNames, r.RequesterUserId);
        var employeeName = r.BeneficiaryUserId.HasValue
            ? $"{requesterName} → {DocumentRequestMappingHelper.ResolveName(displayNames, r.BeneficiaryUserId.Value)}"
            : requesterName;

        IReadOnlyList<string> allowed = Array.Empty<string>();
        if (userContext.IsComplete)
            allowed = WorkflowActionPolicy.AllowedActionsForActor(userContext.Role!.Value, r.Status);

        // EmployeeId : personne concernée par la demande (bénéficiaire si renseigné, sinon demandeur).
        var employeeSubjectId = r.BeneficiaryUserId?.ToString() ?? r.RequesterUserId.ToString();

        // Brouillon RH : seuls RH / Admin voient l’identifiant et le statut « généré ». Les autres rôles
        // gardent la demande « en cours » jusqu’à finalisation (PDF officiel après validation RH).
        var effectiveLatest = latestGenerated;
        if (latestGenerated is { Status: GeneratedDocumentStatus.DraftPendingRhReview }
            && userContext.Role is not (AppRole.Rh or AppRole.Admin))
        {
            effectiveLatest = null;
        }

        string? genId = effectiveLatest?.Id.ToString();
        string? genAt = effectiveLatest?.CreatedAt.ToString("O");
        string? docUrl = effectiveLatest is null
            ? null
            : $"/api/documentation/data/generated-documents/{effectiveLatest.Id:D}/file";

        // Libellé métier : dès qu’un PDF final est lié, le pilote / le RH voient « Generated » même si le statut SQL est resté Approved (legacy).
        var statusForApi = effectiveLatest is not null
            ? DocumentRequestStatus.Generated.ToString()
            : r.Status.ToString();

        return new DocumentRequestResponse(
            displayId,
            r.Id.ToString(),
            typeLabel,
            r.CreatedAt.ToString("yyyy-MM-dd"),
            statusForApi,
            employeeName,
            employeeSubjectId,
            r.RequesterUserId.ToString(),
            r.BeneficiaryUserId?.ToString(),
            r.OrganizationalUnitId?.ToString(),
            r.DocumentTypeId?.ToString(),
            r.Reason,
            r.IsCustomType,
            allowed,
            r.RejectionReason,
            r.DecidedAt?.ToString("O"),
            genId,
            genAt,
            docUrl,
            r.DocumentTemplateId?.ToString("D"),
            documentTemplateName,
            r.ComplementaryComments);
    }
}
